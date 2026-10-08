using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#nullable disable

/// <summary>
/// Server-authoritative Advanced Farming harvest adapter. It deliberately handles only
/// completed REBIRTH player-grown crops because that path has a verified deterministic
/// one-item harvest and seed-stage replant contract.
/// </summary>
public sealed class RebirthNpcAdvancedFarmingHarvestAdapter : IRebirthNpcFarmingWorkAdapter
{
    private sealed class Pending
    {
        public Vector3i Position;
        public BlockValue ExpectedCrop;
        public BlockValue ReplantValue;
        public string ItemKey;
        public int Quantity;
    }

    private readonly object sync = new object();
    private readonly Dictionary<ulong, Pending> pending = new Dictionary<ulong, Pending>();
    private static long completed, rejected, compensated;

    public string AdapterId { get { return "rebirth.farming.advanced-harvest"; } }
    public int Priority { get { return 500; } }

    public bool CanHandle(RebirthNpcConcreteWorkContext context)
    {
        if (context == null || context.Assignment == null || context.Target == null ||
            !AdvancedFarmingRuntimePolicy.Enabled || GameManager.Instance == null ||
            GameManager.Instance.World == null || GameManager.Instance.World.IsRemote())
            return false;

        World world = GameManager.Instance.World;
        Vector3i pos = ToBlockPosition(context.Target.Position);
        BlockValue value = world.GetBlock(pos);
        BlockPlantGrowingRebirth crop = value.Block as BlockPlantGrowingRebirth;
        return crop != null && crop.IsFullyGrownCrop(value) &&
            RebirthUtilities.IsPlayerGrownHarvestCrop(value);
    }

    public RebirthNpcWorkMutationResult BeginFarming(RebirthNpcConcreteWorkContext context)
    {
        string detail;
        Pending operation;
        if (!TryPrepare(context, out operation, out detail))
        {
            Interlocked.Increment(ref rejected);
            return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, detail);
        }

        lock (sync) pending[context.Assignment.AssignmentId] = operation;
        return RebirthNpcWorkMutationResult.Continue(0.25f, "Advanced Farming harvest prepared.");
    }

    public RebirthNpcWorkMutationResult TickFarming(RebirthNpcConcreteWorkContext context)
    {
        Pending operation;
        lock (sync)
        {
            if (!pending.TryGetValue(context.Assignment.AssignmentId, out operation))
                return Failed(RebirthNpcWorkFailureCategory.Interrupted, "Advanced Farming harvest state is unavailable.");
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote())
            return Suspend("Authoritative world is unavailable.");

        BlockValue live = world.GetBlock(operation.Position);
        if (!SameBlockRevision(live, operation.ExpectedCrop))
        {
            Interlocked.Increment(ref rejected);
            return Failed(RebirthNpcWorkFailureCategory.TargetChanged, "Crop changed before harvest commit.");
        }

        // Mutate the world first. If inventory credit fails, restore only while the seed-stage
        // value is still unchanged, avoiding overwrite of an intervening world mutation.
        world.SetBlockRPC(new BlockChangeInfo((BlockValueRef)operation.Position,
            operation.ReplantValue, !Harmony_RebirthAdvancedFarmingLightStateRefreshPatch
                .IsExposureNeutralPlantLikeSwap(operation.ExpectedCrop, operation.ReplantValue)));

        BlockValue replanted = world.GetBlock(operation.Position);
        if (!SameBlockRevision(replanted, operation.ReplantValue))
        {
            Interlocked.Increment(ref rejected);
            return Failed(RebirthNpcWorkFailureCategory.TargetChanged, "Crop replant mutation was not retained.");
        }

        uint inventoryRevision = RebirthNpcInventoryTransactionService.GetRevision(context.Assignment.NpcId);
        RebirthNpcInventoryTransaction transaction = new RebirthNpcInventoryTransaction(
            context.OperationId, context.Assignment.NpcId, inventoryRevision, context.AuthorityKey,
            new [] { new RebirthNpcInventoryMutation(operation.ItemKey, operation.Quantity) });

        uint resultingRevision;
        RebirthNpcInventoryTransactionResult result = RebirthNpcInventoryTransactionService.Apply(transaction, out resultingRevision);
        if (result != RebirthNpcInventoryTransactionResult.Applied &&
            result != RebirthNpcInventoryTransactionResult.Replayed)
        {
            BlockValue current = world.GetBlock(operation.Position);
            if (SameBlockRevision(current, operation.ReplantValue))
            {
                world.SetBlockRPC(new BlockChangeInfo((BlockValueRef)operation.Position,
                    operation.ExpectedCrop, !Harmony_RebirthAdvancedFarmingLightStateRefreshPatch
                        .IsExposureNeutralPlantLikeSwap(operation.ReplantValue, operation.ExpectedCrop)));
                Interlocked.Increment(ref compensated);
            }
            Interlocked.Increment(ref rejected);
            return Failed(MapInventoryFailure(result), "NPC harvest output credit failed: " + result + ".");
        }

        lock (sync) pending.Remove(context.Assignment.AssignmentId);
        Interlocked.Increment(ref completed);
        return new RebirthNpcWorkMutationResult
        {
            Disposition = RebirthNpcWorkExecutionDisposition.Completed,
            Detail = "Harvested and replanted Advanced Farming crop.",
            ProgressDelta = 0.75f,
            MutationCommitted = true,
            Produced = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                { { operation.ItemKey, operation.Quantity } }
        };
    }

    public void CancelFarming(RebirthNpcConcreteWorkContext context, string reason)
    {
        if (context == null || context.Assignment == null) return;
        lock (sync) pending.Remove(context.Assignment.AssignmentId);
    }

    public static string GetReport()
    {
        return "[REBIRTH NPC Advanced Farming Harvest] completed=" + Interlocked.Read(ref completed) +
            " rejected=" + Interlocked.Read(ref rejected) +
            " compensated=" + Interlocked.Read(ref compensated);
    }

    private static bool TryPrepare(RebirthNpcConcreteWorkContext context, out Pending operation, out string detail)
    {
        operation = null; detail = string.Empty;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote()) { detail = "Authoritative world is unavailable."; return false; }

        Vector3i pos = ToBlockPosition(context.Target.Position);
        BlockValue cropValue = world.GetBlock(pos);
        BlockPlantGrowingRebirth crop = cropValue.Block as BlockPlantGrowingRebirth;
        if (crop == null || !crop.IsFullyGrownCrop(cropValue) || !RebirthUtilities.IsPlayerGrownHarvestCrop(cropValue))
        { detail = "Target is not a completed REBIRTH player-grown crop."; return false; }

        string seedName = RebirthUtilities.GetSeedItemNameFromPlantBlock(cropValue);
        BlockValue seedBlock = string.IsNullOrEmpty(seedName) ? BlockValue.Air : Block.GetBlockValue(seedName);
        if (seedBlock.isair) { detail = "Crop seed-stage block could not be resolved."; return false; }

        string itemKey;
        if (!TryResolveHarvestItem(cropValue, out itemKey))
        { detail = "Crop has no deterministic harvest item."; return false; }

        seedBlock.rotation = cropValue.rotation;
        seedBlock.meta = cropValue.meta;
        seedBlock.meta2 = 0;
        operation = new Pending { Position = pos, ExpectedCrop = cropValue, ReplantValue = seedBlock,
            ItemKey = itemKey, Quantity = 1 };
        return true;
    }

    private static bool TryResolveHarvestItem(BlockValue cropValue, out string itemKey)
    {
        itemKey = string.Empty;
        Block block = cropValue.Block;
        if (block == null || block.itemsToDrop == null) return false;
        List<Block.SItemDropProb> drops;
        if (!block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest, out drops) || drops == null) return false;
        for (int i = 0; i < drops.Count; i++)
        {
            Block.SItemDropProb drop = drops[i];
            if (string.IsNullOrEmpty(drop.name) ||
                drop.name.IndexOf("planted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (!string.IsNullOrEmpty(drop.tag) && drop.tag.IndexOf("Seed", StringComparison.OrdinalIgnoreCase) >= 0))
                continue;
            ItemValue value = drop.name == "*" ? cropValue.ToItemValue() : ItemClass.GetItem(drop.name);
            if (value.IsEmpty() || value.ItemClass == null || string.IsNullOrEmpty(value.ItemClass.Name)) continue;
            itemKey = value.ItemClass.Name;
            return true;
        }
        return false;
    }

    private static Vector3i ToBlockPosition(Vector3 position)
    { return new Vector3i(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z)); }

    private static bool SameBlockRevision(BlockValue a, BlockValue b)
    { return a.type == b.type && a.meta == b.meta && a.meta2 == b.meta2 && a.rotation == b.rotation; }

    private static RebirthNpcWorkMutationResult Failed(RebirthNpcWorkFailureCategory failure, string detail)
    { return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Failed,
        FailureCategory = failure, Detail = detail ?? string.Empty }; }

    private static RebirthNpcWorkMutationResult Suspend(string detail)
    { return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Suspend,
        FailureCategory = RebirthNpcWorkFailureCategory.TargetUnavailable, Detail = detail ?? string.Empty }; }

    private static RebirthNpcWorkFailureCategory MapInventoryFailure(RebirthNpcInventoryTransactionResult result)
    {
        if (result == RebirthNpcInventoryTransactionResult.AuthorityDenied) return RebirthNpcWorkFailureCategory.AuthorizationDenied;
        if (result == RebirthNpcInventoryTransactionResult.RevisionConflict ||
            result == RebirthNpcInventoryTransactionResult.ReservationConflict) return RebirthNpcWorkFailureCategory.InventoryUnavailable;
        return RebirthNpcWorkFailureCategory.OutputBlocked;
    }
}
