using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RemoteResourceClientOperation : byte
{
    Craft = 1,
    ItemRepair = 2,
    BlockRepair = 3,
    BlockUpgrade = 4,
    CookingPull = 5
}

public enum RemoteResourceTransactionRequestMode : byte
{
    ExecuteOrReplay = 1,
    QueryOnly = 2
}

public enum RemoteResourceTransactionOutcomeState : byte
{
    Unknown = 0,
    Success = 1,
    Failed = 2
}

/// <summary>
/// Dedicated-client preflight for UI actions whose vanilla code queues the action before
/// inventory removal. The client sends only the remote shortfall; the authoritative
/// server re-discovers/revalidates current sources and consumes that shortfall before the
/// client is allowed to run the vanilla queue insertion path.
/// </summary>
public static class RemoteResourceClientTransactionCoordinator
{
    private sealed class PendingCraft
    {
        public ulong RequestId;
        public ItemActionEntryCraft Entry;
        public Recipe Recipe;
        public int CraftCount;
        public List<ItemStack> FullRequirements;
    }

    private sealed class PendingRepair
    {
        public ulong RequestId;
        public ItemActionEntryRepair Entry;
        public int ItemType;
        public float UseTimes;
        public List<ItemStack> FullRequirements;
    }

    private sealed class PendingBlockAction
    {
        public ulong RequestId;
        public ItemActionRepair Action;
        public ItemActionData ActionData;
        public Vector3i TargetPos;
        public RemoteResourceClientOperation Operation;
        public List<ItemStack> FullRequirements;
    }

    private sealed class PendingLease
    {
        public ulong RequestId;
        public ulong SessionEpoch;
        public RemoteResourceClientOperation Operation;
        public float NextQueryAt;
        public int QueryAttempts;
    }

    private const float OutcomeQueryDelaySeconds = 3f;
    private const int MaxPendingTransactions = 128;
    private static readonly Dictionary<ulong, PendingLease> pendingLeases = new Dictionary<ulong, PendingLease>();
    // A client process restart must not reuse the previous process's server journal keys.
    private static ulong clientSessionEpoch = CreateSessionEpoch();

    private static readonly Dictionary<ulong, PendingCraft> pendingCraft = new Dictionary<ulong, PendingCraft>();
    private static readonly Dictionary<ulong, PendingRepair> pendingRepair = new Dictionary<ulong, PendingRepair>();
    private static readonly Dictionary<ulong, PendingBlockAction> pendingBlock = new Dictionary<ulong, PendingBlockAction>();
    private static readonly HashSet<ItemActionEntryCraft> craftWaiting = new HashSet<ItemActionEntryCraft>();
    private static readonly HashSet<ItemActionEntryRepair> repairWaiting = new HashSet<ItemActionEntryRepair>();
    private static readonly HashSet<ItemActionData> blockWaiting = new HashSet<ItemActionData>();
    private static long nextRequestId;
    [ThreadStatic] private static ItemActionEntryCraft resumeCraft;
    [ThreadStatic] private static ItemActionEntryRepair resumeRepair;
    [ThreadStatic] private static ItemActionData resumeBlockRepair;
    [ThreadStatic] private static ItemActionData resumeBlockUpgrade;

    [ThreadStatic] private static int outcomeDeliveryDepth;
    // A validated terminal reply still owns its resources after the lease is removed,
    // until replay/refund callbacks finish. New gear/library custody must wait.
    public static bool HasPendingInventoryOperation(EntityPlayerLocal player)
        => player?.world!=null&&GameManager.Instance!=null&&
            ReferenceEquals(GameManager.Instance.World,player.world)&&
            ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&
            (pendingLeases.Count>0||outcomeDeliveryDepth>0||RemoteResourceClientGrantContext.Active);

    public static bool IsDedicatedClient
    {
        get
        {
            ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            return connection != null && !connection.IsServer;
        }
    }

    // Fix38: a requirements refresh must not clamp the requested count after the server
    // reserves remote ingredients but before its matching grant arrives. This is read-only;
    // the existing receipt/selection checks still decide whether the request may commit.
    internal static bool HasPendingCraftForUi(XUi ui)
    {
        if (ui == null || !IsDedicatedClient) return false;
        if (ReferenceEquals(resumeCraft?.ItemController?.xui, ui)) return true;
        foreach (PendingCraft pending in pendingCraft.Values)
            if (pending != null && pendingLeases.ContainsKey(pending.RequestId) &&
                ReferenceEquals(pending.Entry?.ItemController?.xui, ui)) return true;
        return false;
    }

    public static void Clear()
    {
        pendingLeases.Clear();
        unchecked { clientSessionEpoch++; }
        if (clientSessionEpoch == 0) clientSessionEpoch = 1;
        pendingCraft.Clear();
        pendingRepair.Clear();
        pendingBlock.Clear();
        craftWaiting.Clear();
        repairWaiting.Clear();
        blockWaiting.Clear();
        resumeCraft = null;
        resumeRepair = null;
        resumeBlockRepair = null;
        resumeBlockUpgrade = null;
        RemoteResourceClientGrantContext.End();
    }

    public static bool BeforeCraftActivated(ItemActionEntryCraft entry)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || entry == null) return true;
        if (!IsDedicatedClient)
        {
            EntityPlayerLocal local = entry.ItemController != null && entry.ItemController.xui != null &&
                entry.ItemController.xui.playerUI != null ? entry.ItemController.xui.playerUI.entityPlayer : null;
            if (local != null) RemoteResourceSnapshotCache.ForceRebuild(local);
            return true;
        }
        if (ReferenceEquals(resumeCraft, entry)) return true;
        if (craftWaiting.Contains(entry)) return false;

        XUiC_RecipeEntry recipeEntry = entry.ItemController as XUiC_RecipeEntry;
        Recipe recipe = recipeEntry != null ? recipeEntry.Recipe : null;
        XUi xui = entry.ItemController != null ? entry.ItemController.xui : null;
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (recipe == null || xui == null || player == null || entry.craftCountControl == null) return true;

        // Let the vanilla helper build the exact modified ingredient list. If the current
        // replicated availability does not satisfy it, vanilla handles the disabled action.
        if (!entry.hasItems(xui, recipe)) return true;
        int craftCount = Math.Max(1, entry.craftCountControl.Count);
        List<ItemStack> requirements = CloneRequirements(entry.tempIngredientList, craftCount);
        if (requirements.Count == 0 || HasAllLocal(player, requirements)) return true;

        XUiC_WorkstationInputGrid workstationInput = entry.craftCountControl.WindowGroup != null
            ? entry.craftCountControl.WindowGroup.Controller.GetChildByType<XUiC_WorkstationInputGrid>()
            : null;
        if (workstationInput != null && workstationInput.HasItems(entry.tempIngredientList, craftCount)) return true;

        List<ItemStack> remoteShortfall;
        if (!TryBuildRemoteShortfall(player, requirements, out remoteShortfall))
        {
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            return false;
        }
        if (remoteShortfall.Count == 0) return true;

        ulong requestId = NextRequestId();
        if (!BeginLease(requestId, RemoteResourceClientOperation.Craft)) return false;
        pendingCraft[requestId] = new PendingCraft
        {
            RequestId = requestId,
            Entry = entry,
            Recipe = recipe,
            CraftCount = craftCount,
            FullRequirements = requirements
        };
        craftWaiting.Add(entry);
        if (!SendRequest(player, requestId, RemoteResourceClientOperation.Craft, remoteShortfall))
        {
            pendingCraft.Remove(requestId);
            AbandonLease(requestId);
            craftWaiting.Remove(entry);
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            return false;
        }
        RemoteResourceDiagnostics.Write("craft preflight request=" + requestId + " remoteTypes=" + remoteShortfall.Count);
        return false;
    }

    public static bool BeforeItemRepairActivated(ItemActionEntryRepair entry)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || entry == null) return true;
        if (!IsDedicatedClient)
        {
            EntityPlayerLocal local = entry.ItemController != null && entry.ItemController.xui != null &&
                entry.ItemController.xui.playerUI != null ? entry.ItemController.xui.playerUI.entityPlayer : null;
            if (local != null) RemoteResourceSnapshotCache.ForceRebuild(local);
            return true;
        }
        if (ReferenceEquals(resumeRepair, entry)) return true;
        if (repairWaiting.Contains(entry)) return false;

        XUi xui = entry.ItemController != null ? entry.ItemController.xui : null;
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (player == null) return true;
        ItemStack itemStack = ItemStack.Empty.Clone();
        XUiC_EquipmentStack equipment = entry.ItemController as XUiC_EquipmentStack;
        XUiC_ItemStack grid = entry.ItemController as XUiC_ItemStack;
        if (equipment != null) itemStack = equipment.ItemStack;
        else if (grid != null) itemStack = grid.ItemStack;
        if (itemStack == null || itemStack.IsEmpty()) return true;
        ItemClass itemClass = ItemClass.GetForId(itemStack.itemValue.type);
        if (itemClass == null || itemClass.RepairTools == null || itemClass.RepairTools.Length == 0) return true;
        ItemClass repairTool = ItemClass.GetItemClass(itemClass.RepairTools[0].Value);
        if (repairTool == null) return true;
        int maxRepairTools = Convert.ToInt32(Math.Ceiling((double)Mathf.CeilToInt(itemStack.itemValue.UseTimes) / repairTool.RepairAmount.Value));
        int availableRepairTools = xui.PlayerInventory.GetItemCount(new ItemValue(repairTool.Id));
        int count = Mathf.Min(Math.Max(0, maxRepairTools), Math.Max(0, availableRepairTools));
        // Vanilla repairs as much as the currently available tool count allows rather than
        // requiring enough material for a full repair. Preserve that behavior here.
        if (count == 0) return true;
        ItemStack requirement = new ItemStack(new ItemValue(repairTool.Id), count);
        List<ItemStack> full = new List<ItemStack> { requirement };
        if (HasAllLocal(player, full)) return true;
        List<ItemStack> shortfall;
        if (!TryBuildRemoteShortfall(player, full, out shortfall) || shortfall.Count == 0)
        {
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            return false;
        }

        ulong requestId = NextRequestId();
        if (!BeginLease(requestId, RemoteResourceClientOperation.ItemRepair)) return false;
        pendingRepair[requestId] = new PendingRepair
        {
            RequestId = requestId,
            Entry = entry,
            ItemType = itemStack.itemValue.type,
            UseTimes = itemStack.itemValue.UseTimes,
            FullRequirements = full
        };
        repairWaiting.Add(entry);
        if (!SendRequest(player, requestId, RemoteResourceClientOperation.ItemRepair, shortfall))
        {
            pendingRepair.Remove(requestId);
            AbandonLease(requestId);
            repairWaiting.Remove(entry);
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
        }
        return false;
    }

    public static bool BeforeBlockExecute(ItemActionRepair action, ItemActionData actionData, bool released)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || !IsDedicatedClient || action == null || actionData == null || released)
            return true;
        if (ReferenceEquals(resumeBlockRepair, actionData)) return true;
        if (blockWaiting.Contains(actionData)) return false;

        EntityPlayerLocal player = actionData.invData != null ? actionData.invData.holdingEntity as EntityPlayerLocal : null;
        if (player == null || player.HitInfo == null || !player.HitInfo.bHitValid) return true;
        if (Time.time - actionData.lastUseTime < action.Delay) return true;
        if (actionData.invData.itemValue.MaxUseTimes > 0 && actionData.invData.itemValue.UseTimes >= actionData.invData.itemValue.MaxUseTimes)
            return true;
        if (actionData.invData.itemValue.UseTimes == 0f && actionData.invData.itemValue.MaxUseTimes == 0) return true;
        if (player.HitInfo.hit.distanceSq > Constants.cDigAndBuildDistance * Constants.cDigAndBuildDistance) return true;
        if (World.SandboxUseTraderArea == TraderAreaStates.Default && actionData.invData.world.IsWithinTraderArea(player.HitInfo.hit.blockPos)) return true;
        if (!GameUtils.IsBlockOrTerrain(player.HitInfo.tag)) return true;

        Vector3i target;
        List<ItemStack> requirements;
        if (!TryBuildBlockRepairRequirements(action, actionData, out target, out requirements) || requirements.Count == 0)
            return true;
        if (HasAllLocal(player, requirements)) return true;

        List<ItemStack> shortfall;
        if (!TryBuildRemoteShortfall(player, requirements, out shortfall) || shortfall.Count == 0)
        {
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            RemoteResourceLiveSync.DispatchLocalUi(player);
            return false;
        }

        ulong requestId = NextRequestId();
        if (!BeginLease(requestId, RemoteResourceClientOperation.BlockRepair)) return false;
        pendingBlock[requestId] = new PendingBlockAction
        {
            RequestId = requestId,
            Action = action,
            ActionData = actionData,
            TargetPos = target,
            Operation = RemoteResourceClientOperation.BlockRepair,
            FullRequirements = requirements
        };
        blockWaiting.Add(actionData);
        if (!SendRequest(player, requestId, RemoteResourceClientOperation.BlockRepair, shortfall))
        {
            pendingBlock.Remove(requestId);
            AbandonLease(requestId);
            blockWaiting.Remove(actionData);
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            return false;
        }
        RemoteResourceDiagnostics.Write("block repair preflight request=" + requestId + " remoteTypes=" + shortfall.Count);
        return false;
    }

    public static bool BeforeBlockHoldingUpdate(ItemActionRepair action, ItemActionData actionData)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || !IsDedicatedClient || action == null || actionData == null)
            return true;
        if (ReferenceEquals(resumeBlockUpgrade, actionData)) return true;
        if (blockWaiting.Contains(actionData)) return false;
        if (!action.bUpgradeCountChanged) return true;

        EntityPlayerLocal player = actionData.invData != null ? actionData.invData.holdingEntity as EntityPlayerLocal : null;
        if (player == null || actionData.invData.world == null) return true;
        BlockValue blockValue = actionData.invData.world.GetBlock(action.blockTargetPos);
        int requiredHits;
        if (blockValue.Block == null || !int.TryParse(blockValue.Block.Properties.GetString("UpgradeBlock", "UpgradeHitCount"), out requiredHits))
            return true;
        requiredHits = requiredHits + action.hitCountOffset < 1f ? 1 : (int)(requiredHits + action.hitCountOffset);
        if (action.blockUpgradeCount < requiredHits) return true;

        ItemStack requirement;
        if (!RemoteResourceActionConsumerPatchInstaller.TryBuildUpgradeRequirement(action, blockValue, out requirement))
            return true;
        List<ItemStack> requirements = new List<ItemStack> { requirement };
        if (HasAllLocal(player, requirements)) return true;

        List<ItemStack> shortfall;
        if (!TryBuildRemoteShortfall(player, requirements, out shortfall) || shortfall.Count == 0)
        {
            action.blockUpgradeCount = 0;
            action.bUpgradeCountChanged = false;
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            RemoteResourceLiveSync.DispatchLocalUi(player);
            return false;
        }

        ulong requestId = NextRequestId();
        if (!BeginLease(requestId, RemoteResourceClientOperation.BlockUpgrade)) return false;
        pendingBlock[requestId] = new PendingBlockAction
        {
            RequestId = requestId,
            Action = action,
            ActionData = actionData,
            TargetPos = action.blockTargetPos,
            Operation = RemoteResourceClientOperation.BlockUpgrade,
            FullRequirements = requirements
        };
        blockWaiting.Add(actionData);
        if (!SendRequest(player, requestId, RemoteResourceClientOperation.BlockUpgrade, shortfall))
        {
            pendingBlock.Remove(requestId);
            AbandonLease(requestId);
            blockWaiting.Remove(actionData);
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            action.blockUpgradeCount = 0;
            action.bUpgradeCountChanged = false;
            return false;
        }
        RemoteResourceDiagnostics.Write("block upgrade preflight request=" + requestId + " remoteTypes=" + shortfall.Count);
        return false;
    }

    public static void ReceiveResult(
        ulong sessionEpoch,
        ulong requestId,
        RemoteResourceClientOperation operation,
        RemoteResourceTransactionOutcomeState outcome,
        string failureReason,
        List<ItemStack> removedRemote)
    {
        PendingLease lease;
        if (!pendingLeases.TryGetValue(requestId, out lease) || lease == null ||
            lease.SessionEpoch != sessionEpoch || lease.Operation != operation) return;
        if (outcome == RemoteResourceTransactionOutcomeState.Unknown)
        {
            lease.NextQueryAt = Time.unscaledTime + OutcomeQueryDelaySeconds;
            RemoteResourceDiagnostics.Write("transaction outcome still unknown request=" + requestId + " epoch=" + sessionEpoch);
            return;
        }

        outcomeDeliveryDepth++;
        try
        {
        pendingLeases.Remove(requestId);
        bool success = outcome == RemoteResourceTransactionOutcomeState.Success;
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer()
            : null;
        if (operation == RemoteResourceClientOperation.Craft)
        {
            PendingCraft pending;
            if (!pendingCraft.TryGetValue(requestId, out pending)) return;
            pendingCraft.Remove(requestId);
            craftWaiting.Remove(pending.Entry);
            if (!success)
            {
                if (removedRemote != null && removedRemote.Count > 0)
                    RefundRemoteToLocal(player, removedRemote, "server reported a failed transaction after authoritative removal");
                FailUi(player, failureReason);
                return;
            }
            XUiC_RecipeEntry current = pending.Entry != null ? pending.Entry.ItemController as XUiC_RecipeEntry : null;
            if (current == null || current.Recipe != pending.Recipe || pending.Entry.craftCountControl == null ||
                pending.Entry.craftCountControl.Count != pending.CraftCount)
            {
                RefundRemoteToLocal(player, removedRemote, "craft selection changed during remote resource preflight");
                return;
            }
            if (!CanCoverWithGrant(player, pending.FullRequirements, removedRemote))
            {
                RefundRemoteToLocal(player, removedRemote, "local crafting resources changed during remote resource preflight");
                return;
            }
            bool consumedGrant = false;
            RemoteResourceClientGrantContext.Begin(removedRemote);
            try
            {
                resumeCraft = pending.Entry;
                pending.Entry.OnActivated();
                consumedGrant = RemoteResourceClientGrantContext.Consumed;
            }
            finally
            {
                resumeCraft = null;
                RemoteResourceClientGrantContext.End();
            }
            if (!consumedGrant)
            {
                RefundRemoteToLocal(player, removedRemote, "craft action did not commit after remote resource preflight");
                return;
            }
        }
        else if (operation == RemoteResourceClientOperation.ItemRepair)
        {
            PendingRepair pending;
            if (!pendingRepair.TryGetValue(requestId, out pending)) return;
            pendingRepair.Remove(requestId);
            repairWaiting.Remove(pending.Entry);
            if (!success)
            {
                if (removedRemote != null && removedRemote.Count > 0)
                    RefundRemoteToLocal(player, removedRemote, "server reported a failed transaction after authoritative removal");
                FailUi(player, failureReason);
                return;
            }
            ItemStack current = GetControllerStack(pending.Entry != null ? pending.Entry.ItemController : null);
            if (current == null || current.IsEmpty() || current.itemValue.type != pending.ItemType ||
                Math.Abs(current.itemValue.UseTimes - pending.UseTimes) > 0.001f)
            {
                RefundRemoteToLocal(player, removedRemote, "repair item changed during remote resource preflight");
                return;
            }
            if (!CanCoverWithGrant(player, pending.FullRequirements, removedRemote))
            {
                RefundRemoteToLocal(player, removedRemote, "local item-repair resources changed during remote resource preflight");
                return;
            }
            bool consumedGrant = false;
            RemoteResourceClientGrantContext.Begin(removedRemote);
            try
            {
                resumeRepair = pending.Entry;
                pending.Entry.OnActivated();
                consumedGrant = RemoteResourceClientGrantContext.Consumed;
            }
            finally
            {
                resumeRepair = null;
                RemoteResourceClientGrantContext.End();
            }
            if (!consumedGrant)
            {
                RefundRemoteToLocal(player, removedRemote, "item repair did not commit after remote resource preflight");
                return;
            }
        }
        else if (operation == RemoteResourceClientOperation.BlockRepair || operation == RemoteResourceClientOperation.BlockUpgrade)
        {
            PendingBlockAction pending;
            if (!pendingBlock.TryGetValue(requestId, out pending)) return;
            pendingBlock.Remove(requestId);
            blockWaiting.Remove(pending.ActionData);
            if (!success)
            {
                if (removedRemote != null && removedRemote.Count > 0)
                    RefundRemoteToLocal(player, removedRemote, "server reported a failed transaction after authoritative removal");
                if (pending.Operation == RemoteResourceClientOperation.BlockUpgrade && pending.Action != null)
                {
                    pending.Action.blockUpgradeCount = 0;
                    pending.Action.bUpgradeCountChanged = false;
                }
                if (pending.ActionData != null) pending.ActionData.lastUseTime = Time.time;
                FailUi(player, failureReason);
                return;
            }
            if (!ValidateBlockAction(pending))
            {
                RefundRemoteToLocal(player, removedRemote, "block target changed during remote resource preflight");
                return;
            }
            if (!CanCoverWithGrant(player, pending.FullRequirements, removedRemote))
            {
                RefundRemoteToLocal(player, removedRemote, "local block-action resources changed during remote resource preflight");
                return;
            }
            bool consumedGrant = false;
            RemoteResourceClientGrantContext.Begin(removedRemote);
            try
            {
                if (pending.Operation == RemoteResourceClientOperation.BlockRepair)
                {
                    resumeBlockRepair = pending.ActionData;
                    pending.Action.ExecuteAction(pending.ActionData, false);
                }
                else
                {
                    resumeBlockUpgrade = pending.ActionData;
                    pending.Action.OnHoldingUpdate(pending.ActionData);
                }
                consumedGrant = RemoteResourceClientGrantContext.Consumed;
            }
            finally
            {
                resumeBlockRepair = null;
                resumeBlockUpgrade = null;
                RemoteResourceClientGrantContext.End();
            }
            if (!consumedGrant)
            {
                RefundRemoteToLocal(player, removedRemote, "block action did not commit after remote resource preflight");
                return;
            }
        }
        RemoteResourceClientAvailability.InvalidateAndRequest(player);
        }
        finally { outcomeDeliveryDepth--; }
    }

    private static bool TryBuildBlockRepairRequirements(
        ItemActionRepair action, ItemActionData actionData, out Vector3i target, out List<ItemStack> requirements)
    {
        target = Vector3i.zero;
        requirements = new List<ItemStack>();
        EntityPlayerLocal player = actionData != null && actionData.invData != null
            ? actionData.invData.holdingEntity as EntityPlayerLocal : null;
        if (player == null || !player.HitInfo.bHitValid) return false;
        target = player.HitInfo.hit.blockPos;
        BlockValue blockValue = actionData.invData.world.GetBlock(target);
        if (blockValue.ischild)
        {
            target = blockValue.Block.multiBlockPos.GetParentPos(target, blockValue);
            blockValue = actionData.invData.world.GetBlock(target);
        }
        Block block = blockValue.Block;
        if (block == null || !block.CanRepair(blockValue)) return false;
        int repairedDamage = Utils.FastMin((int)action.repairAmount, blockValue.damage);
        float repairFraction = (float)repairedDamage / block.MaxDamage;
        List<Block.SItemNameCount> repairItems = block.RepairItems;
        if (block.RepairItemsMeshDamage != null && block.shape.UseRepairDamageState(blockValue))
        {
            repairFraction = 1f;
            repairItems = block.RepairItemsMeshDamage;
        }
        if (repairItems == null) return false;
        ItemActionRepair.InventoryDataRepair data = actionData as ItemActionRepair.InventoryDataRepair;
        bool sameProgress = data != null && data.lastHitPosition == target && data.lastHitBlockValue.type == blockValue.type &&
            ReferenceEquals(data.lastRepairItems, repairItems) && data.lastRepairItemsPercents != null &&
            data.lastRepairItemsPercents.Length == repairItems.Count;
        float resourceScale = block.ResourceScale;
        for (int i = 0; i < repairItems.Count; i++)
        {
            float pendingFraction = sameProgress ? data.lastRepairItemsPercents[i] : 0f;
            if (pendingFraction > 0f) continue;
            float scaled = repairItems[i].Count * repairFraction * resourceScale;
            int count = Utils.FastMax((int)scaled, 1);
            ItemValue value = ItemClass.GetItem(repairItems[i].ItemName);
            if (value == null || value.IsEmpty()) continue;
            requirements.Add(new ItemStack(value, count));
        }
        return true;
    }

    private static bool ValidateBlockAction(PendingBlockAction pending)
    {
        if (pending == null || pending.Action == null || pending.ActionData == null || pending.ActionData.invData == null ||
            pending.ActionData.invData.world == null) return false;
        BlockValue blockValue = pending.ActionData.invData.world.GetBlock(pending.TargetPos);
        if (blockValue.Block == null) return false;
        if (pending.Operation == RemoteResourceClientOperation.BlockUpgrade)
        {
            ItemStack current;
            if (!RemoteResourceActionConsumerPatchInstaller.TryBuildUpgradeRequirement(pending.Action, blockValue, out current)) return false;
            return RequirementsEquivalent(pending.FullRequirements, new List<ItemStack> { current });
        }
        List<ItemStack> currentRequirements;
        Vector3i currentTarget;
        if (!TryBuildBlockRepairRequirements(pending.Action, pending.ActionData, out currentTarget, out currentRequirements)) return false;
        return currentTarget == pending.TargetPos && RequirementsEquivalent(pending.FullRequirements, currentRequirements);
    }

    private static bool RequirementsEquivalent(IList<ItemStack> a, IList<ItemStack> b)
    {
        Dictionary<int, int> left = Aggregate(a);
        Dictionary<int, int> right = Aggregate(b);
        if (left.Count != right.Count) return false;
        foreach (KeyValuePair<int, int> pair in left)
        {
            int value;
            if (!right.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
        }
        return true;
    }

    private static Dictionary<int, int> Aggregate(IList<ItemStack> values)
    {
        Dictionary<int, int> result = new Dictionary<int, int>();
        for (int i = 0; values != null && i < values.Count; i++)
        {
            ItemStack stack = values[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
            int current;
            result.TryGetValue(stack.itemValue.type, out current);
            result[stack.itemValue.type] = current + stack.count;
        }
        return result;
    }

    private static bool CanCoverWithGrant(EntityPlayerLocal player, IList<ItemStack> requirements, IList<ItemStack> removedRemote)
    {
        if (player == null) return false;
        Dictionary<int, int> remoteByType = Aggregate(removedRemote);
        Dictionary<int, int> requiredByType = Aggregate(requirements);
        foreach (KeyValuePair<int, int> pair in requiredByType)
        {
            int remote;
            remoteByType.TryGetValue(pair.Key, out remote);
            int local = CountConsumableLocal(player, new ItemValue(pair.Key));
            if (local + remote < pair.Value) return false;
        }
        return true;
    }

    private static void RefundRemoteToLocal(EntityPlayerLocal player, List<ItemStack> removedRemote, string reason)
    {
        if (player == null || removedRemote == null) return;
        for (int i = 0; i < removedRemote.Count; i++)
        {
            ItemStack remaining = removedRemote[i] != null ? removedRemote[i].Clone() : ItemStack.Empty.Clone();
            if (remaining.IsEmpty() || remaining.count <= 0) continue;
            if (player.inventory != null && player.inventory.AddItem(remaining)) continue;
            if (player.bag != null && player.bag.AddItem(remaining)) continue;
            RemoteResourceDiagnostics.Write("WARNING unable to refund preflight itemType=" + remaining.itemValue.type + " x" + remaining.count);
        }
        RemoteResourceDiagnostics.Write("preflight resources refunded locally: " + (reason ?? string.Empty));
        RemoteResourceLiveSync.DispatchLocalUi(player);
        RemoteResourceClientAvailability.InvalidateAndRequest(player);
    }

    private static ItemStack GetControllerStack(XUiController controller)
    {
        XUiC_EquipmentStack equipment = controller as XUiC_EquipmentStack;
        if (equipment != null) return equipment.ItemStack;
        XUiC_ItemStack grid = controller as XUiC_ItemStack;
        return grid != null ? grid.ItemStack : null;
    }

    private static void FailUi(EntityPlayerLocal player, string reason)
    {
        RemoteResourceDiagnostics.Write("client preflight failed: " + (reason ?? string.Empty));
        if (player != null)
        {
            GameManager.ShowTooltip(player, Localization.Get("ttMissingCraftingResources"));
            RemoteResourceClientAvailability.InvalidateAndRequest(player);
            RemoteResourceLiveSync.DispatchLocalUi(player);
        }
    }

    public static void UpdatePendingTransactions()
    {
        if (!IsDedicatedClient || pendingLeases.Count == 0) return;
        float now = Time.unscaledTime;
        List<ulong> due = null;
        foreach (KeyValuePair<ulong, PendingLease> pair in pendingLeases)
        {
            PendingLease lease = pair.Value;
            if (lease != null && now >= lease.NextQueryAt)
            {
                if (due == null) due = new List<ulong>();
                due.Add(pair.Key);
            }
        }
        if (due == null) return;
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        for (int i = 0; i < due.Count; i++)
        {
            PendingLease lease;
            if (!pendingLeases.TryGetValue(due[i], out lease) || lease == null) continue;
            lease.NextQueryAt = now + OutcomeQueryDelaySeconds;
            lease.QueryAttempts++;
            SendRequest(player, lease.RequestId, lease.Operation, new List<ItemStack>(),
                RemoteResourceTransactionRequestMode.QueryOnly, lease.SessionEpoch);
        }
    }

    private static bool BeginLease(ulong requestId, RemoteResourceClientOperation operation)
    {
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(player==null||RebirthBackpackLibraryReservation.BlocksResourceUse(player))return false;
        if (pendingLeases.Count >= MaxPendingTransactions)
        {
            RemoteResourceDiagnostics.Write("client transaction rejected: pending outcome journal is full");
            return false;
        }
        pendingLeases[requestId] = new PendingLease
        {
            RequestId = requestId,
            SessionEpoch = clientSessionEpoch,
            Operation = operation,
            NextQueryAt = Time.unscaledTime + OutcomeQueryDelaySeconds,
            QueryAttempts = 0
        };
        return true;
    }

    private static void AbandonLease(ulong requestId)
    {
        pendingLeases.Remove(requestId);
    }

    private static ulong CreateSessionEpoch()
    {
        ulong value = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
        return value == 0 ? 1UL : value;
    }

    private static bool SendRequest(EntityPlayerLocal player, ulong requestId, RemoteResourceClientOperation operation, List<ItemStack> shortfall,
        RemoteResourceTransactionRequestMode mode = RemoteResourceTransactionRequestMode.ExecuteOrReplay, ulong epoch = 0)
    {
        GameManager game = GameManager.Instance;
        PersistentPlayerData persistent = game != null ? game.GetPersistentLocalPlayer() : null;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (player == null || player.world == null || !player.world.IsRemote() || game == null ||
            !ReferenceEquals(game.World, player.world) || persistent?.PrimaryId == null ||
            connection == null || connection.IsServer || !connection.IsConnected)
        {
            RemoteResourceDiagnostics.Write("client preflight request could not be sent: connection identity unavailable");
            return false;
        }
        var packet = NetPackageManager.GetPackage<NetPackageRemoteResourceConsumeRequest>();
        var channels = connection.GetConnectionToServer();
        int channel = packet.Channel;
        if (channels == null || channel < 0 || channel >= channels.Length ||
            channels[channel] == null || channels[channel].IsDisconnected()) return false;
        packet.Setup(player.entityId, persistent.PrimaryId, epoch == 0 ? clientSessionEpoch : epoch,
            requestId, operation, mode, shortfall);
        // A send exception is ambiguous: retain its existing lease for query-only recovery.
        connection.SendToServer(packet);
        return true;
    }

    private static ulong NextRequestId()
    {
        return unchecked((ulong)Interlocked.Increment(ref nextRequestId));
    }

    private static List<ItemStack> CloneRequirements(IList<ItemStack> source, int multiplier)
    {
        Dictionary<int, ItemStack> aggregate = new Dictionary<int, ItemStack>();
        for (int i = 0; source != null && i < source.Count; i++)
        {
            ItemStack stack = source[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
            int count = stack.count * multiplier;
            ItemStack current;
            if (aggregate.TryGetValue(stack.itemValue.type, out current)) current.count += count;
            else aggregate[stack.itemValue.type] = new ItemStack(stack.itemValue.Clone(), count);
        }
        return new List<ItemStack>(aggregate.Values);
    }

    private static bool HasAllLocal(EntityPlayerLocal player, IList<ItemStack> requirements)
    {
        for (int i = 0; requirements != null && i < requirements.Count; i++)
            if (CountConsumableLocal(player, requirements[i].itemValue) < requirements[i].count) return false;
        return true;
    }

    private static bool TryBuildRemoteShortfall(EntityPlayerLocal player, IList<ItemStack> requirements, out List<ItemStack> shortfall)
    {
        shortfall = new List<ItemStack>();
        for (int i = 0; requirements != null && i < requirements.Count; i++)
        {
            ItemStack required = requirements[i];
            int local = CountConsumableLocal(player, required.itemValue);
            int needed = Math.Max(0, required.count - local);
            if (needed == 0) continue;
            if (RemoteResourceClientAvailability.GetCount(player, required.itemValue) < needed) return false;
            shortfall.Add(new ItemStack(required.itemValue.Clone(), needed));
        }
        return true;
    }

    public static int CountConsumableLocal(EntityPlayer player, ItemValue itemValue)
    {
        if (player == null || itemValue == null || itemValue.IsEmpty() || RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return 0;
        int total = 0;
        ItemStack[] bag = player.bag != null ? player.bag.ItemGrid.items : null;
        // Backpack slot locks are organization/Quick Stack locks, not vanilla crafting
        // exclusion locks. Base 3.1 Bag.GetItemCount/DecItem consume from these slots, so
        // the remote-resource preflight must count them too or dedicated clients are
        // incorrectly told they cannot craft recipes that vanilla can satisfy locally.
        for (int i = 0; bag != null && i < bag.Length; i++)
        {
            ItemStack stack = bag[i];
            if (Matches(stack, itemValue)) total += stack.count;
        }
        int slots = player.inventory != null ? player.inventory.Length : 0;
        for (int i = 0; i < slots; i++)
        {
            ItemStack stack = player.inventory.GetItemStack(i);
            if (Matches(stack, itemValue)) total += stack.count;
        }
        return total;
    }

    private static bool Matches(ItemStack stack, ItemValue itemValue)
    {
        return stack != null && !stack.IsEmpty() && stack.count > 0 && stack.itemValue.type == itemValue.type &&
               (!stack.itemValue.HasModSlots || !stack.itemValue.HasMods());
    }
}

public static class RemoteResourceClientGrantContext
{
    [ThreadStatic] private static List<ItemStack> granted;
    [ThreadStatic] private static Dictionary<int, int> counts;
    [ThreadStatic] private static bool consumed;

    public static bool Active { get { return granted != null; } }
    public static bool Consumed { get { return consumed; } }

    public static void Begin(List<ItemStack> removedRemote)
    {
        granted = removedRemote != null ? new List<ItemStack>() : new List<ItemStack>();
        counts = new Dictionary<int, int>();
        consumed = false;
        for (int i = 0; removedRemote != null && i < removedRemote.Count; i++)
        {
            ItemStack stack = removedRemote[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
            granted.Add(stack.Clone());
            int current;
            counts.TryGetValue(stack.itemValue.type, out current);
            counts[stack.itemValue.type] = current + stack.count;
        }
    }

    public static void End()
    {
        granted = null;
        counts = null;
        consumed = false;
    }

    public static int GetCount(ItemValue itemValue)
    {
        if (!Active || itemValue == null || itemValue.IsEmpty()) return 0;
        int value;
        return counts.TryGetValue(itemValue.type, out value) ? value : 0;
    }

    public static void AppendStacks(List<ItemStack> destination)
    {
        if (!Active || destination == null) return;
        for (int i = 0; i < granted.Count; i++) destination.Add(granted[i].Clone());
    }

    public static bool CanCover(EntityPlayerLocal player, ItemStack requirement)
    {
        if (!Active || player == null || requirement == null || requirement.IsEmpty() || requirement.count <= 0 || RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return false;
        int remote;
        counts.TryGetValue(requirement.itemValue.type, out remote);
        return RemoteResourceClientTransactionCoordinator.CountConsumableLocal(player, requirement.itemValue) + remote >= requirement.count;
    }

    public static bool HasItems(EntityPlayerLocal player, IList<ItemStack> requirements, int multiplier)
    {
        if (!Active || player == null || requirements == null || multiplier < 1 || RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return false;
        Dictionary<int, int> requiredByType = new Dictionary<int, int>();
        Dictionary<int, ItemValue> values = new Dictionary<int, ItemValue>();
        for (int i = 0; i < requirements.Count; i++)
        {
            ItemStack requirement = requirements[i];
            if (requirement == null || requirement.IsEmpty() || requirement.count <= 0) continue;
            int current;
            requiredByType.TryGetValue(requirement.itemValue.type, out current);
            requiredByType[requirement.itemValue.type] = current + requirement.count * multiplier;
            values[requirement.itemValue.type] = requirement.itemValue;
        }
        foreach (KeyValuePair<int, int> pair in requiredByType)
        {
            int remote;
            counts.TryGetValue(pair.Key, out remote);
            if (RemoteResourceClientTransactionCoordinator.CountConsumableLocal(player, values[pair.Key]) + remote < pair.Value)
                return false;
        }
        return true;
    }

    public static bool ConsumeLocalRemainder(EntityPlayerLocal player, IList<ItemStack> requirements, int multiplier, IList<ItemStack> removedItems)
    {
        if (!Active || player == null || requirements == null || multiplier < 1 || RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return false;
        Dictionary<int, int> remoteUseByType = new Dictionary<int, int>();
        Dictionary<int, int> localNeedByType = new Dictionary<int, int>();
        Dictionary<int, ItemValue> values = new Dictionary<int, ItemValue>();
        for (int i = 0; i < requirements.Count; i++)
        {
            ItemStack requirement = requirements[i];
            if (requirement == null || requirement.IsEmpty() || requirement.count <= 0) continue;
            int total = requirement.count * multiplier;
            int alreadyRemote;
            remoteUseByType.TryGetValue(requirement.itemValue.type, out alreadyRemote);
            int availableRemote;
            counts.TryGetValue(requirement.itemValue.type, out availableRemote);
            int useRemote = Math.Min(total, Math.Max(0, availableRemote - alreadyRemote));
            remoteUseByType[requirement.itemValue.type] = alreadyRemote + useRemote;
            int currentLocal;
            localNeedByType.TryGetValue(requirement.itemValue.type, out currentLocal);
            localNeedByType[requirement.itemValue.type] = currentLocal + total - useRemote;
            values[requirement.itemValue.type] = requirement.itemValue;
        }
        foreach (KeyValuePair<int, int> pair in localNeedByType)
            if (pair.Value > 0 && RemoteResourceClientTransactionCoordinator.CountConsumableLocal(player, values[pair.Key]) < pair.Value)
                return false;

        foreach (KeyValuePair<int, int> pair in localNeedByType)
            if (pair.Value > 0 && !ConsumeLocal(player, values[pair.Key], pair.Value, removedItems)) return false;
        foreach (KeyValuePair<int, int> pair in remoteUseByType)
        {
            int available;
            counts.TryGetValue(pair.Key, out available);
            counts[pair.Key] = Math.Max(0, available - pair.Value);
        }
        if (removedItems != null)
            for (int i = 0; i < granted.Count; i++) removedItems.Add(granted[i].Clone());
        if (player.PlayerUI != null && player.PlayerUI.xui != null && player.PlayerUI.xui.PlayerInventory != null)
        {
            player.PlayerUI.xui.PlayerInventory.dispatchBackpackItemsChanged();
            player.PlayerUI.xui.PlayerInventory.dispatchToolbeltItemsChanged();
        }
        consumed = true;
        return true;
    }

    private static bool ConsumeLocal(EntityPlayerLocal player, ItemValue itemValue, int count, IList<ItemStack> removedItems)
    {
        int left = count;
        ItemStack[] bag = player.bag.ItemGrid.items;
        for (int i = 0; bag != null && i < bag.Length && left > 0; i++)
        {
            ItemStack stack = bag[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemValue.type ||
                (stack.itemValue.HasModSlots && stack.itemValue.HasMods())) continue;
            int take = Math.Min(left, stack.count);
            ItemStack after = stack.Clone();
            after.count -= take;
            if (after.count <= 0) after = ItemStack.Empty.Clone();
            player.bag.SetSlot(i, after);
            if (removedItems != null) removedItems.Add(new ItemStack(stack.itemValue.Clone(), take));
            left -= take;
        }
        int slots = player.inventory.Length;
        for (int i = 0; i < slots && left > 0; i++)
        {
            ItemStack stack = player.inventory.GetItemStack(i);
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemValue.type ||
                (stack.itemValue.HasModSlots && stack.itemValue.HasMods())) continue;
            int take = Math.Min(left, stack.count);
            ItemStack after = stack.Clone();
            after.count -= take;
            if (after.count <= 0) after = ItemStack.Empty.Clone();
            player.inventory.SetItem(i, after);
            if (removedItems != null) removedItems.Add(new ItemStack(stack.itemValue.Clone(), take));
            left -= take;
        }
        return left == 0;
    }
}

[HarmonyPatch(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.OnActivated))]
internal static class RemoteResourceCraftPreflightPatch
{
    private static bool Prefix(ItemActionEntryCraft __instance)
    { return RemoteResourceClientTransactionCoordinator.BeforeCraftActivated(__instance); }
}

[HarmonyPatch(typeof(ItemActionEntryRepair), nameof(ItemActionEntryRepair.OnActivated))]
internal static class RemoteResourceItemRepairPreflightPatch
{
    private static bool Prefix(ItemActionEntryRepair __instance)
    { return RemoteResourceClientTransactionCoordinator.BeforeItemRepairActivated(__instance); }
}

[HarmonyPatch(typeof(ItemActionRepair), nameof(ItemActionRepair.ExecuteAction))]
internal static class RemoteResourceBlockRepairPreflightPatch
{
    private static bool Prefix(ItemActionRepair __instance, ItemActionData _actionData, bool _bReleased)
    { return RemoteResourceClientTransactionCoordinator.BeforeBlockExecute(__instance, _actionData, _bReleased); }
}

[HarmonyPatch(typeof(ItemActionRepair), nameof(ItemActionRepair.OnHoldingUpdate))]
internal static class RemoteResourceBlockUpgradePreflightPatch
{
    private static bool Prefix(ItemActionRepair __instance, ItemActionData _actionData)
    { return RemoteResourceClientTransactionCoordinator.BeforeBlockHoldingUpdate(__instance, _actionData); }
}

[HarmonyPatch(typeof(XUiC_WorkstationInputGrid), nameof(XUiC_WorkstationInputGrid.RemoveItems))]
internal static class RemoteResourceWorkstationInputGrantPatch
{
    private static bool Prefix(XUiC_WorkstationInputGrid __instance, IList<ItemStack> _itemStacks, int _multiplier, IList<ItemStack> _removedItems)
    {
        if (!RemoteResourceClientGrantContext.Active || __instance == null || __instance.xui == null || __instance.xui.playerUI == null) return true;
        EntityPlayerLocal player = __instance.xui.playerUI.entityPlayer;
        RemoteResourceClientGrantContext.ConsumeLocalRemainder(player, _itemStacks, _multiplier, _removedItems);
        return false;
    }
}

[Preserve]
public sealed class NetPackageRemoteResourceConsumeRequest : NetPackage
{
    private const int MaxRequirementStacks = 64;
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private ulong sessionEpoch;
    private ulong requestId;
    private RemoteResourceClientOperation operation;
    private RemoteResourceTransactionRequestMode mode;
    private List<ItemStack> requirements = new List<ItemStack>();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRemoteResourceConsumeRequest Setup(int id, PlatformUserIdentifierAbs uid, ulong epoch, ulong request,
        RemoteResourceClientOperation op, RemoteResourceTransactionRequestMode requestMode, List<ItemStack> items)
    {
        playerId = id; userId = uid; sessionEpoch = epoch; requestId = request; operation = op; mode = requestMode;
        if (items != null && items.Count > MaxRequirementStacks)
            throw new InvalidDataException("Remote Resources requirement count exceeds packet bound.");
        requirements = CloneStacks(items);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        playerId = binary.ReadInt32(); userId = PlatformUserIdentifierAbs.FromStream(binary);
        sessionEpoch = binary.ReadUInt64(); requestId = binary.ReadUInt64();
        operation = (RemoteResourceClientOperation)binary.ReadByte();
        mode = (RemoteResourceTransactionRequestMode)binary.ReadByte();
        ItemStack[] values = GameUtils.ReadItemStack(binary);
        if (values != null && values.Length > MaxRequirementStacks)
            throw new InvalidDataException("Remote Resources requirement count exceeds packet bound.");
        requirements = values != null ? new List<ItemStack>(values) : new List<ItemStack>();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); PooledBinaryWriter binary = writer;
        binary.Write(playerId); userId.ToStream(binary); binary.Write(sessionEpoch); binary.Write(requestId);
        binary.Write((byte)operation); binary.Write((byte)mode);
        GameUtils.WriteItemStack(binary, requirements ?? new List<ItemStack>());
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || sessionEpoch == 0 || requestId == 0 || requirements == null ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId) ||
            operation < RemoteResourceClientOperation.Craft || operation > RemoteResourceClientOperation.BlockUpgrade ||
            mode < RemoteResourceTransactionRequestMode.ExecuteOrReplay || mode > RemoteResourceTransactionRequestMode.QueryOnly) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId);
        if (player == null || persistent == null || persistent.PrimaryId == null || !persistent.PrimaryId.Equals(userId)) return;

        RemoteResourceTransactionOutcome outcome = RemoteResourceTransactionOutcomeJournal.Resolve(
            world, player, userId, sessionEpoch, requestId, operation, requirements,
            mode == RemoteResourceTransactionRequestMode.QueryOnly);

        RemoteResourceDiagnostics.Write("preflight op=" + operation + " request=" + requestId + " epoch=" + sessionEpoch +
            " outcome=" + outcome.State + " removed=" + outcome.Removed.Count +
            " reason='" + (outcome.Reason ?? string.Empty) + "'");
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRemoteResourceConsumeResult>()
                .Setup(sessionEpoch, requestId, operation, outcome.State, outcome.Reason, outcome.Removed),
            _attachedToEntityId: playerId);
        if (outcome.State == RemoteResourceTransactionOutcomeState.Failed)
            RemoteResourceLiveSync.PushSnapshot(player, "failed transaction preflight");
    }
    public int GetLength() { return 0; }

    private static List<ItemStack> CloneStacks(IList<ItemStack> items)
    {
        List<ItemStack> result = new List<ItemStack>();
        for (int i = 0; items != null && i < items.Count; i++)
            if (items[i] != null) result.Add(items[i].Clone());
        return result;
    }
}

[Preserve]
public sealed class NetPackageRemoteResourceConsumeResult : NetPackage
{
    private const int MaxReasonLength = 512;
    private const int MaxRemovedStacks = 64;
    private ulong sessionEpoch;
    private ulong requestId;
    private RemoteResourceClientOperation operation;
    private RemoteResourceTransactionOutcomeState outcome;
    private string reason = string.Empty;
    private List<ItemStack> removed = new List<ItemStack>();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRemoteResourceConsumeResult Setup(ulong epoch, ulong request, RemoteResourceClientOperation op,
        RemoteResourceTransactionOutcomeState state, string why, List<ItemStack> items)
    {
        if (items != null && items.Count > MaxRemovedStacks)
            throw new InvalidDataException("Remote Resources removed-stack count exceeds packet bound.");
        sessionEpoch = epoch; requestId = request; operation = op; outcome = state; reason = ClampReason(why); removed = CloneStacks(items); return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        sessionEpoch = binary.ReadUInt64(); requestId = binary.ReadUInt64(); operation = (RemoteResourceClientOperation)binary.ReadByte();
        outcome = (RemoteResourceTransactionOutcomeState)binary.ReadByte(); reason = RebirthSurvivorNetworkCodec.ReadBoundedString(binary, MaxReasonLength);
        ItemStack[] values = GameUtils.ReadItemStack(binary);
        if (values != null && values.Length > MaxRemovedStacks)
            throw new InvalidDataException("Remote Resources removed-stack count exceeds packet bound.");
        removed = values != null ? new List<ItemStack>(values) : new List<ItemStack>();
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); PooledBinaryWriter binary = writer;
        if (removed != null && removed.Count > MaxRemovedStacks)
            throw new InvalidDataException("Remote Resources removed-stack count exceeds packet bound.");
        binary.Write(sessionEpoch); binary.Write(requestId); binary.Write((byte)operation); binary.Write((byte)outcome);
        RebirthSurvivorNetworkCodec.WriteString(binary, reason, MaxReasonLength);
        GameUtils.WriteItemStack(binary, removed ?? new List<ItemStack>());
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    { RemoteResourceClientTransactionCoordinator.ReceiveResult(sessionEpoch, requestId, operation, outcome, reason, removed); }
    public int GetLength() { return 0; }

    private static string ClampReason(string value)
    {
        value = value ?? string.Empty;
        return value.Length <= MaxReasonLength ? value : value.Substring(0, MaxReasonLength);
    }

    private static List<ItemStack> CloneStacks(IList<ItemStack> items)
    {
        List<ItemStack> result = new List<ItemStack>();
        for (int i = 0; items != null && i < items.Count; i++)
            if (items[i] != null) result.Add(items[i].Clone());
        return result;
    }
}

internal sealed class RemoteResourceTransactionOutcome
{
    public RemoteResourceTransactionOutcomeState State;
    public string Reason = string.Empty;
    public List<ItemStack> Removed = new List<ItemStack>();

    public RemoteResourceTransactionOutcome Clone()
    {
        RemoteResourceTransactionOutcome copy = new RemoteResourceTransactionOutcome { State = State, Reason = Reason ?? string.Empty };
        for (int i = 0; Removed != null && i < Removed.Count; i++) if (Removed[i] != null) copy.Removed.Add(Removed[i].Clone());
        return copy;
    }
}

internal static class RemoteResourceTransactionOutcomeJournal
{
    private const int MaxEntries = 2048;
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, RemoteResourceTransactionOutcome> Outcomes =
        new Dictionary<string, RemoteResourceTransactionOutcome>(StringComparer.Ordinal);
    private static readonly Queue<string> Order = new Queue<string>();
    private static readonly Dictionary<string, object> InFlight = new Dictionary<string, object>(StringComparer.Ordinal);
    private static World boundWorld;

    public static RemoteResourceTransactionOutcome Resolve(World world, EntityPlayer player, PlatformUserIdentifierAbs userId,
        ulong sessionEpoch, ulong requestId, RemoteResourceClientOperation operation, IList<ItemStack> requirements, bool queryOnly)
    {
        string key = BuildKey(userId, sessionEpoch, requestId, operation);
        object claim = new object();
        lock (Gate)
        {
            BindWorld(world);
            RemoteResourceTransactionOutcome existing;
            if (Outcomes.TryGetValue(key, out existing)) return existing.Clone();
            if (queryOnly || InFlight.ContainsKey(key)) return new RemoteResourceTransactionOutcome
            {
                State = RemoteResourceTransactionOutcomeState.Unknown,
                Reason = "Authoritative transaction outcome is not available yet."
            };
            InFlight.Add(key, claim);
        }

        RemoteResourceTransactionOutcome terminal;
        // Keep confirmed removals available even if a later source throws.
        var removed = new List<ItemStack>();
        try
        {
            if (!RemoteResourcesRuntimePolicy.Enabled ||
                (operation == RemoteResourceClientOperation.CookingPull && !RebirthSurvivorMode.IsEnabledForCurrentWorld()))
            {
                terminal = new RemoteResourceTransactionOutcome
                {
                    State = RemoteResourceTransactionOutcomeState.Failed,
                    Reason = "Remote Resources is disabled"
                };
            }
            else if (requirements == null || requirements.Count == 0)
            {
                terminal = new RemoteResourceTransactionOutcome
                {
                    State = RemoteResourceTransactionOutcomeState.Failed,
                    Reason = "Remote Resources request contained no consumable requirements"
                };
            }
            else
            {
                RemoteResourceTransactions.ConsumptionResult result = RemoteResourceTransactions.TryConsume(
                    player, requirements, 1, removed, false);
                terminal = new RemoteResourceTransactionOutcome
                {
                    State = result.Success ? RemoteResourceTransactionOutcomeState.Success : RemoteResourceTransactionOutcomeState.Failed,
                    Reason = result.FailureReason ?? string.Empty,
                    Removed = CloneStacks(removed)
                };
            }
        }
        catch (Exception ex)
        {
            terminal = new RemoteResourceTransactionOutcome
            {
                State = RemoteResourceTransactionOutcomeState.Failed,
                Reason = "Remote Resources transaction failed before a result could be published: " + ex.GetType().Name,
                Removed = CloneStacks(removed)
            };
        }

        lock (Gate)
        {
            // Completion cannot rebind a world or consume a replacement request's
            // claim after unload/reset, even if the same World instance is reused.
            object currentClaim;
            if (!ReferenceEquals(boundWorld, world) || !InFlight.TryGetValue(key, out currentClaim) ||
                !ReferenceEquals(currentClaim, claim)) return new RemoteResourceTransactionOutcome
            {
                State = RemoteResourceTransactionOutcomeState.Unknown,
                Reason = "Transaction scope changed before its removal outcome could be published."
            };
            InFlight.Remove(key);
            RemoteResourceTransactionOutcome existing;
            if (Outcomes.TryGetValue(key, out existing)) return existing.Clone();
            Outcomes[key] = terminal.Clone();
            Order.Enqueue(key);
            while (Outcomes.Count > MaxEntries && Order.Count > 0)
            {
                string oldest = Order.Dequeue();
                Outcomes.Remove(oldest);
            }
            return terminal.Clone();
        }
    }

    // Opaque, server-local handle. Detached snapshots cannot mutate the retained
    // removal outcome; the handle expires on eviction or world reset.
    private sealed class RetainedHandle
    {
        internal World World;
        internal string Key;
        internal RemoteResourceTransactionOutcome Outcome;
    }

    internal static bool TryGetRetained(World world, PlatformUserIdentifierAbs authenticatedUserId,
        ulong epoch, ulong request, RemoteResourceClientOperation operation,
        out RemoteResourceTransactionOutcome snapshot, out object handle)
    {
        snapshot = null; handle = null;
        if (world == null || authenticatedUserId == null || string.IsNullOrEmpty(authenticatedUserId.CombinedString) ||
            epoch == 0 || request == 0 || operation < RemoteResourceClientOperation.Craft ||
            operation > RemoteResourceClientOperation.CookingPull) return false;
        lock (Gate)
        {
            // Read-only lookup must never bind/reset a journal for a stale caller.
            if (!ReferenceEquals(boundWorld, world)) return false;
            string key = BuildKey(authenticatedUserId, epoch, request, operation);
            RemoteResourceTransactionOutcome retained;
            if (!Outcomes.TryGetValue(key, out retained) || retained == null ||
                retained.State == RemoteResourceTransactionOutcomeState.Unknown ||
                retained.Removed == null || retained.Removed.Count == 0) return false;
            snapshot = retained.Clone();
            handle = new RetainedHandle { World = world, Key = key, Outcome = retained };
            return true;
        }
    }

    internal static bool MatchesRetained(World world, PlatformUserIdentifierAbs authenticatedUserId,
        ulong epoch, ulong request, RemoteResourceClientOperation operation, object handle)
    {
        RetainedHandle retainedHandle = handle as RetainedHandle;
        if (retainedHandle == null || world == null || authenticatedUserId == null ||
            epoch == 0 || request == 0 || operation < RemoteResourceClientOperation.Craft ||
            operation > RemoteResourceClientOperation.CookingPull) return false;
        lock (Gate)
        {
            RemoteResourceTransactionOutcome retained;
            return ReferenceEquals(boundWorld, world) && ReferenceEquals(retainedHandle.World, world) &&
                retainedHandle.Key == BuildKey(authenticatedUserId, epoch, request, operation) &&
                Outcomes.TryGetValue(retainedHandle.Key, out retained) && ReferenceEquals(retained, retainedHandle.Outcome);
        }
    }
    public static void ResetForWorldUnload()
    {
        lock (Gate)
        {
            Outcomes.Clear(); Order.Clear(); InFlight.Clear(); boundWorld = null;
        }
    }

    private static void BindWorld(World world)
    {
        if (ReferenceEquals(boundWorld, world)) return;
        Outcomes.Clear(); Order.Clear(); InFlight.Clear(); boundWorld = world;
    }

    private static string BuildKey(PlatformUserIdentifierAbs userId, ulong epoch, ulong request, RemoteResourceClientOperation operation)
    {
        string player = userId != null ? userId.CombinedString : string.Empty;
        return player + "|" + epoch + "|" + request + "|" + (byte)operation;
    }

    private static List<ItemStack> CloneStacks(IList<ItemStack> items)
    {
        List<ItemStack> result = new List<ItemStack>();
        for (int i = 0; items != null && i < items.Count; i++)
            if (items[i] != null) result.Add(items[i].Clone());
        return result;
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
internal static class RemoteResourceClientTransactionUpdatePatch
{
    private static void Postfix()
    { RemoteResourceClientTransactionCoordinator.UpdatePendingTransactions(); }
}

public static class RemoteResourceClientTransactionPatchInstaller
{
    private static bool installed;
    public static void Install()
    {
        if (installed) return;
        installed = true;
        Harmony harmony = new Harmony("rebirth.remote.resources.client.transactions.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceCraftPreflightPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceItemRepairPreflightPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceBlockRepairPreflightPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceBlockUpgradePreflightPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceWorkstationInputGrantPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceClientTransactionUpdatePatch));
    }
}
