using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#nullable disable

/// <summary>
/// Explicit server-authoritative Advanced Farming planting and watering operations.
/// Target keys are farm:plant:&lt;seed-item-key&gt; and farm:water.
/// </summary>
public sealed class RebirthNpcAdvancedFarmingPlantingWateringAdapter : IRebirthNpcFarmingWorkAdapter
{
    private enum OperationKind : byte { Plant = 0, Water = 1 }
    private sealed class Pending
    {
        public OperationKind Kind;
        public Vector3i Position;
        public BlockValue ExpectedTarget;
        public BlockValue PlantValue;
        public string SeedItemKey;
        public bool SeedReserved;
        public int WaterDepletion;
    }

    private readonly object sync = new object();
    private readonly Dictionary<ulong, Pending> pending = new Dictionary<ulong, Pending>();
    private static long planted, watered, rejected, compensated;

    public string AdapterId { get { return "rebirth.farming.advanced-plant-water"; } }
    public int Priority { get { return 600; } }

    public bool CanHandle(RebirthNpcConcreteWorkContext context)
    {
        string key = context != null && context.Assignment != null ? context.Assignment.TargetKey : string.Empty;
        return AdvancedFarmingRuntimePolicy.Enabled && !string.IsNullOrEmpty(key) &&
            (key.StartsWith("farm:plant:", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(key, "farm:water", StringComparison.OrdinalIgnoreCase));
    }

    public RebirthNpcWorkMutationResult BeginFarming(RebirthNpcConcreteWorkContext context)
    {
        Pending operation;
        string detail;
        if (!TryPrepare(context, out operation, out detail))
        {
            Interlocked.Increment(ref rejected);
            return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, detail);
        }

        if (operation.Kind == OperationKind.Plant)
        {
            if (!RebirthNpcInventoryTransactionService.TryReserve(context.Assignment.NpcId,
                operation.SeedItemKey, 1))
            {
                Interlocked.Increment(ref rejected);
                return Failed(RebirthNpcWorkFailureCategory.MissingInput,
                    "NPC inventory does not contain an unreserved planting seed.");
            }
            operation.SeedReserved = true;
        }

        lock (sync) pending[context.Assignment.AssignmentId] = operation;
        return RebirthNpcWorkMutationResult.Continue(0.25f,
            operation.Kind == OperationKind.Plant ? "Advanced Farming planting prepared." :
            "Advanced Farming watering prepared.");
    }

    public RebirthNpcWorkMutationResult TickFarming(RebirthNpcConcreteWorkContext context)
    {
        Pending operation;
        lock (sync)
        {
            if (!pending.TryGetValue(context.Assignment.AssignmentId, out operation))
                return Failed(RebirthNpcWorkFailureCategory.Interrupted,
                    "Advanced Farming operation state is unavailable.");
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote())
            return Suspend("Authoritative world is unavailable.");

        BlockValue live = world.GetBlock(operation.Position);
        if (!SameBlockRevision(live, operation.ExpectedTarget))
        {
            ReleaseSeed(context, operation);
            Remove(context.Assignment.AssignmentId);
            Interlocked.Increment(ref rejected);
            return Failed(RebirthNpcWorkFailureCategory.TargetChanged,
                "Farming target changed before mutation commit.");
        }

        return operation.Kind == OperationKind.Plant
            ? CommitPlant(context, operation, world)
            : CommitWater(context, operation, world);
    }

    public void CancelFarming(RebirthNpcConcreteWorkContext context, string reason)
    {
        if (context == null || context.Assignment == null) return;
        Pending operation;
        lock (sync)
        {
            pending.TryGetValue(context.Assignment.AssignmentId, out operation);
            pending.Remove(context.Assignment.AssignmentId);
        }
        if (operation != null) ReleaseSeed(context, operation);
    }

    public static string GetReport()
    {
        return "[REBIRTH NPC Advanced Farming Plant/Water] planted=" + Interlocked.Read(ref planted) +
            " watered=" + Interlocked.Read(ref watered) + " rejected=" + Interlocked.Read(ref rejected) +
            " compensated=" + Interlocked.Read(ref compensated);
    }

    private RebirthNpcWorkMutationResult CommitPlant(RebirthNpcConcreteWorkContext context,
        Pending operation, World world)
    {
        if (operation.SeedReserved)
        {
            RebirthNpcInventoryTransactionService.ReleaseReservation(context.Assignment.NpcId,
                operation.SeedItemKey, 1);
            operation.SeedReserved = false;
        }

        uint inventoryRevision = RebirthNpcInventoryTransactionService.GetRevision(context.Assignment.NpcId);
        RebirthNpcInventoryTransaction debit = new RebirthNpcInventoryTransaction(context.OperationId,
            context.Assignment.NpcId, inventoryRevision, context.AuthorityKey,
            new [] { new RebirthNpcInventoryMutation(operation.SeedItemKey, -1) });
        uint revision;
        RebirthNpcInventoryTransactionResult debitResult = RebirthNpcInventoryTransactionService.Apply(debit, out revision);
        if (debitResult != RebirthNpcInventoryTransactionResult.Applied &&
            debitResult != RebirthNpcInventoryTransactionResult.Replayed)
        {
            Remove(context.Assignment.AssignmentId);
            Interlocked.Increment(ref rejected);
            return Failed(MapInventoryFailure(debitResult), "NPC planting seed debit failed: " + debitResult + ".");
        }

        world.SetBlockRPC(new BlockChangeInfo((BlockValueRef)operation.Position, operation.PlantValue,
            !Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.IsExposureNeutralPlantLikeSwap(
                operation.ExpectedTarget, operation.PlantValue)));

        BlockValue placed = world.GetBlock(operation.Position);
        if (!SameBlockRevision(placed, operation.PlantValue))
        {
            uint refundRevision = RebirthNpcInventoryTransactionService.GetRevision(context.Assignment.NpcId);
            RebirthNpcInventoryTransaction refund = new RebirthNpcInventoryTransaction(
                DeriveCompensationId(context.OperationId), context.Assignment.NpcId, refundRevision,
                context.AuthorityKey, new [] { new RebirthNpcInventoryMutation(operation.SeedItemKey, 1) });
            uint ignored;
            RebirthNpcInventoryTransactionResult refundResult =
                RebirthNpcInventoryTransactionService.Apply(refund, out ignored);
            if (refundResult == RebirthNpcInventoryTransactionResult.Applied ||
                refundResult == RebirthNpcInventoryTransactionResult.Replayed)
                Interlocked.Increment(ref compensated);
            Remove(context.Assignment.AssignmentId);
            Interlocked.Increment(ref rejected);
            return Failed(RebirthNpcWorkFailureCategory.TargetChanged,
                "Plant placement was not retained; seed refund result=" + refundResult + ".");
        }

        Remove(context.Assignment.AssignmentId);
        Interlocked.Increment(ref planted);
        return new RebirthNpcWorkMutationResult
        {
            Disposition = RebirthNpcWorkExecutionDisposition.Completed,
            Detail = "Planted Advanced Farming seed.", ProgressDelta = 0.75f,
            MutationCommitted = true,
            Consumed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                { { operation.SeedItemKey, 1 } }
        };
    }

    private RebirthNpcWorkMutationResult CommitWater(RebirthNpcConcreteWorkContext context,
        Pending operation, World world)
    {
        TileEntityPlantGrowingRebirth plant = world.GetTileEntity(operation.Position) as TileEntityPlantGrowingRebirth;
        BlockPlantGrowingRebirth crop = world.GetBlock(operation.Position).Block as BlockPlantGrowingRebirth;
        if (plant == null || crop == null)
        {
            Remove(context.Assignment.AssignmentId);
            Interlocked.Increment(ref rejected);
            return Failed(RebirthNpcWorkFailureCategory.InvalidTarget,
                "Watering target no longer has an Advanced Farming plant tile entity.");
        }
        if (plant.bWatered)
        {
            Remove(context.Assignment.AssignmentId);
            return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Completed,
                Detail = "Crop is already watered for its current growth cycle.", ProgressDelta = 0.75f };
        }
        if (!RebirthUtilities.TryConsumePlantWater(world, operation.Position, operation.WaterDepletion))
        {
            Remove(context.Assignment.AssignmentId);
            Interlocked.Increment(ref rejected);
            return Failed(RebirthNpcWorkFailureCategory.MissingInput,
                "No eligible storage-first water provider contains enough water.");
        }

        plant.bWatered = true;
        plant.LastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed();
        plant.setModified();
        AdvancedFarmingHoverTextService.InvalidatePlantState(operation.Position);
        AdvancedFarmingSyncService.SendPlant(operation.Position, world.GetBlock(operation.Position).type,
            plant.AccumulatedTicks, plant.bWatered);

        Remove(context.Assignment.AssignmentId);
        Interlocked.Increment(ref watered);
        return new RebirthNpcWorkMutationResult
        {
            Disposition = RebirthNpcWorkExecutionDisposition.Completed,
            Detail = "Watered Advanced Farming crop using storage-first provider priority.",
            ProgressDelta = 0.75f, MutationCommitted = true,
            Consumed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                { { "advanced-farming-water", operation.WaterDepletion } }
        };
    }

    private static bool TryPrepare(RebirthNpcConcreteWorkContext context, out Pending operation,
        out string detail)
    {
        operation = null; detail = string.Empty;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote()) { detail = "Authoritative world is unavailable."; return false; }
        if (context == null || context.Assignment == null || context.Target == null)
        { detail = "Farming context is unavailable."; return false; }

        Vector3i pos = ToBlockPosition(context.Target.Position);
        string key = context.Assignment.TargetKey ?? string.Empty;
        BlockValue current = world.GetBlock(pos);

        if (key.StartsWith("farm:plant:", StringComparison.OrdinalIgnoreCase))
        {
            string seedKey = key.Substring("farm:plant:".Length).Trim();
            if (seedKey.Length == 0) { detail = "Planting target key does not contain a seed item key."; return false; }
            if (!current.isair) { detail = "Planting target is not empty."; return false; }
            ItemValue seedItem = ItemClass.GetItem(seedKey);
            BlockValue plantValue = Block.GetBlockValue(seedKey);
            BlockPlantGrowingRebirth plantBlock = plantValue.Block as BlockPlantGrowingRebirth;
            if (seedItem.IsEmpty() || plantValue.isair || plantBlock == null)
            { detail = "Seed item does not resolve to an Advanced Farming seed-stage block."; return false; }
            if (!plantBlock.CanPlaceBlockAt(world, pos, plantValue, false))
            { detail = "Seed cannot be planted at the requested target."; return false; }
            operation = new Pending { Kind = OperationKind.Plant, Position = pos,
                ExpectedTarget = current, PlantValue = plantValue, SeedItemKey = seedKey };
            return true;
        }

        if (string.Equals(key, "farm:water", StringComparison.OrdinalIgnoreCase))
        {
            BlockPlantGrowingRebirth crop = current.Block as BlockPlantGrowingRebirth;
            TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
            if (crop == null || te == null) { detail = "Watering target is not an active Advanced Farming crop."; return false; }
            if (crop.IsFullyGrownCrop(current)) { detail = "Fully grown crops do not require a new water cycle."; return false; }
            operation = new Pending { Kind = OperationKind.Water, Position = pos,
                ExpectedTarget = current, WaterDepletion = 1 };
            return true;
        }

        detail = "Unsupported Advanced Farming operation target key.";
        return false;
    }

    private void Remove(ulong assignmentId) { lock (sync) pending.Remove(assignmentId); }
    private static void ReleaseSeed(RebirthNpcConcreteWorkContext context, Pending operation)
    {
        if (operation == null || !operation.SeedReserved || context == null || context.Assignment == null) return;
        RebirthNpcInventoryTransactionService.ReleaseReservation(context.Assignment.NpcId,
            operation.SeedItemKey, 1);
        operation.SeedReserved = false;
    }
    private static Vector3i ToBlockPosition(Vector3 position)
    { return new Vector3i(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z)); }
    private static bool SameBlockRevision(BlockValue a, BlockValue b)
    { return a.type == b.type && a.meta == b.meta && a.meta2 == b.meta2 && a.rotation == b.rotation; }
    private static Guid DeriveCompensationId(Guid operationId)
    {
        byte[] bytes = operationId.ToByteArray();
        bytes[0] ^= 0x5A; bytes[5] ^= 0xC3; bytes[10] ^= 0x7E; bytes[15] ^= 0x19;
        return new Guid(bytes);
    }
    private static RebirthNpcWorkMutationResult Failed(RebirthNpcWorkFailureCategory failure, string detail)
    { return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Failed,
        FailureCategory = failure, Detail = detail ?? string.Empty }; }
    private static RebirthNpcWorkMutationResult Suspend(string detail)
    { return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Suspend,
        FailureCategory = RebirthNpcWorkFailureCategory.TargetUnavailable, Detail = detail ?? string.Empty }; }
    private static RebirthNpcWorkFailureCategory MapInventoryFailure(RebirthNpcInventoryTransactionResult result)
    {
        if (result == RebirthNpcInventoryTransactionResult.AuthorityDenied)
            return RebirthNpcWorkFailureCategory.AuthorizationDenied;
        if (result == RebirthNpcInventoryTransactionResult.InsufficientQuantity)
            return RebirthNpcWorkFailureCategory.MissingInput;
        return RebirthNpcWorkFailureCategory.InventoryUnavailable;
    }
}
