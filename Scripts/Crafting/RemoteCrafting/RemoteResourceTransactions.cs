using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Server-side resource queries and complete-plan consumption. Availability queries use
/// aggregate counts from eligible loaded sources. Mutations traverse real slots in stable
/// source/slot order and revalidate every source revision immediately before commit.
/// </summary>
public static class RemoteResourceTransactions
{
    public sealed class ConsumptionResult
    {
        public bool Success;
        public string FailureReason;
        public int LocalItemsConsumed;
        public int RemoteItemsConsumed;
        public int SourcesChanged;
    }

    public sealed class ReturnReceipt
    {
        public IRemoteResourceSource Source;
        public bool Toolbelt;
        public int Slot;
        public ItemStack Item;
        public int Remaining;
        public int Restore(EntityPlayer player, ItemStack item)
        {
            if (Remaining <= 0 || item == null || item.IsEmpty() || Item.itemValue.type != item.itemValue.type ||
                !RebirthCookingItemStats.Compatible(Item.itemValue, item.itemValue)) return 0;
            ItemStack live = Source != null ? Source.Slots[Slot] : Toolbelt ? player.inventory.GetItemStack(Slot) : player.bag.ItemGrid.items[Slot];
            if (!live.IsEmpty() && (!live.itemValue.EqualsForMerging(Item.itemValue) || !RebirthCookingItemStats.Compatible(live.itemValue, Item.itemValue))) return 0;
            int limit = Item.itemValue.ItemClass.Stacknumber.Value;
            int count = Math.Min(Math.Min(Remaining, item.count), Math.Max(0, limit - (live.IsEmpty() ? 0 : live.count)));
            if (count <= 0) return 0;
            var after = live.IsEmpty() ? new ItemStack(Item.itemValue.Clone(), count) : live.Clone();
            if (!live.IsEmpty()) after.count += count;
            if (Source != null) { Source.SetSlot(Slot, after); Source.MarkModified(); RemoteResourceLiveSync.NotifySourcesChanged(new List<IRemoteResourceSource>{Source}, "cooking ingredient return"); }
            else if (Toolbelt) player.inventory.SetItem(Slot, after);
            else player.bag.SetSlot(Slot, after);
            Remaining -= count;
            return count;
        }
    }

    private sealed class SlotTake
    {
        public IRemoteResourceSource Source;
        public int Slot;
        public int Count;
        public int Revision;
        public int ItemType;
        public int OriginalCount;
        public byte[] ItemPayload;
    }

    private sealed class LocalTake
    {
        public bool Toolbelt;
        public int Slot;
        public int Count;
        public int ItemType;
        public int OriginalCount;
        public byte[] ItemPayload;
    }

    private sealed class Plan
    {
        public readonly List<LocalTake> Local = new List<LocalTake>();
        public readonly List<SlotTake> Remote = new List<SlotTake>();
        public readonly HashSet<string> SourceIds = new HashSet<string>(StringComparer.Ordinal);
    }

    public static int CountRemote(EntityPlayer player, ItemValue itemValue, bool exactOwner = false)
    {
        if (player == null || itemValue == null || itemValue.IsEmpty()) return 0;
        if (!exactOwner) return RemoteResourceSnapshotCache.Get(player).GetCount(itemValue);

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        List<IRemoteResourceSource> sources = RemoteResourceSourceDiscovery.ResolveNearby(
            world, player, RemoteResourcesRuntimePolicy.Radius);
        int total = 0;
        for (int i = 0; i < sources.Count; i++)
        {
            string reason;
            IRemoteResourceSource source = sources[i];
            if (!RemoteResourceEligibility.CanUse(source, player, true, true, out reason)) continue;
            ItemStack[] slots = source.Slots;
            PackedBoolArray locks = source.SlotLocks;
            for (int slot = 0; slots != null && slot < slots.Length; slot++)
            {
                if (IsLocked(locks, slot)) continue;
                ItemStack stack = slots[slot];
                if (MatchesConsumable(stack, itemValue)) total += stack.count;
            }
        }
        return total;
    }

    public static bool HasLocalItems(EntityPlayer player, IList<ItemStack> requirements, int multiplier = 1)
    {
        if (player == null || requirements == null || multiplier < 1 || RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return false;
        Dictionary<int, ItemStack> aggregated = AggregateRequirements(requirements, multiplier);
        foreach (ItemStack required in aggregated.Values)
            if (CountLocal(player, required.itemValue) < required.count) return false;
        return true;
    }

    public static bool HasItems(EntityPlayer player, IList<ItemStack> requirements, int multiplier = 1, bool includeLocal = true)
    {
        if (player == null || requirements == null || multiplier < 1 || RebirthBackpackLibraryReservation.BlocksResourceUse(player)) return false;
        Dictionary<int, ItemStack> aggregated = AggregateRequirements(requirements, multiplier);
        foreach (ItemStack required in aggregated.Values)
        {
            int local = includeLocal ? CountLocal(player, required.itemValue) : 0;
            if (local + CountRemote(player, required.itemValue) < required.count) return false;
        }
        return true;
    }

    public static ConsumptionResult TryConsume(
        EntityPlayer player,
        IList<ItemStack> requirements,
        int multiplier = 1,
        IList<ItemStack> removedItems = null,
        bool includeLocal = true, IList<ReturnReceipt> returnReceipts = null)
    {
        ConsumptionResult result = new ConsumptionResult();
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer)
        {
            result.FailureReason = "server authority required";
            return result;
        }
        if (player == null || requirements == null || multiplier < 1)
        {
            result.FailureReason = "invalid request";
            return result;
        }

        if(RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        {
            result.FailureReason=Localization.Get("xuiRebirthLibraryTransferPending");return result;
        }

        Plan plan = new Plan();
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        // Resolve authoritative sources once for the entire transaction. Recipes often
        // contain several ingredient types; rediscovering the same containers once per
        // ingredient creates avoidable chunk/entity scans.
        List<IRemoteResourceSource> remoteSources = null;
        Dictionary<int, ItemStack> aggregated = AggregateRequirements(requirements, multiplier);
        foreach (ItemStack required in aggregated.Values)
        {
            int needed = required.count;
            if (includeLocal) needed = PlanLocal(player, required.itemValue, needed, plan);
            if (needed > 0)
            {
                if(remoteSources==null)remoteSources=RemoteResourceSourceDiscovery.ResolveNearby(world,player,RemoteResourcesRuntimePolicy.Radius);
                needed = PlanRemote(player, required.itemValue, needed, plan, remoteSources);
            }
            if (needed > 0)
            {
                result.FailureReason = "insufficient " + required.itemValue.ItemClass.GetItemName();
                return result;
            }
        }

        if (RemoteResourceDiagnostics.Enabled)
        {
            for (int i = 0; i < plan.Local.Count; i++)
            {
                LocalTake take = plan.Local[i];
                RemoteResourceDiagnostics.Write("plan local=" + (take.Toolbelt ? "toolbelt" : "backpack") +
                    " slot=" + take.Slot + " itemType=" + take.ItemType + " take=" + take.Count +
                    " liveCount=" + take.OriginalCount);
            }
            for (int i = 0; i < plan.Remote.Count; i++)
            {
                SlotTake take = plan.Remote[i];
                RemoteResourceDiagnostics.Write("plan remote source=" + take.Source.StableId +
                    " kind=" + take.Source.Kind + " slot=" + take.Slot + " itemType=" + take.ItemType +
                    " take=" + take.Count + " liveCount=" + take.OriginalCount + " revision=" + take.Revision);
            }
        }

        if (!Validate(player, plan, out result.FailureReason))
        {
            RemoteResourceDiagnostics.Write("transaction validation failed: " + result.FailureReason);
            return result;
        }

        // Commit only after the complete plan has been validated. The game does not expose
        // one common transactional inventory for player bag/toolbelt plus every TE/entity
        // inventory, so this executes on the authoritative game thread and revision-checks
        // all source inventories immediately before mutation.
        for (int i = 0; i < plan.Local.Count; i++)
        {
            LocalTake take = plan.Local[i];
            ItemStack live = take.Toolbelt
                ? player.inventory.GetItemStack(take.Slot)
                : player.bag.ItemGrid.items[take.Slot];
            ItemStack receiptItem = new ItemStack(live.itemValue.Clone(), take.Count);
            ItemStack after = live.Clone();
            after.count -= take.Count;
            if (after.count <= 0) after = ItemStack.Empty.Clone();
            if (take.Toolbelt) player.inventory.SetItem(take.Slot, after);
            else player.bag.SetSlot(take.Slot, after);
            result.LocalItemsConsumed += take.Count;
            if (removedItems != null) removedItems.Add(receiptItem);
            returnReceipts?.Add(new ReturnReceipt {Toolbelt=take.Toolbelt,Slot=take.Slot,Item=receiptItem.Clone(),Remaining=take.Count});
        }

        HashSet<string> marked = new HashSet<string>(StringComparer.Ordinal);
        List<IRemoteResourceSource> changedSources = new List<IRemoteResourceSource>();
        var stagedNpcItems = new Dictionary<string, List<ItemStack>>(StringComparer.Ordinal);
        for (int i = 0; i < plan.Remote.Count; i++)
        {
            SlotTake take = plan.Remote[i];
            ItemStack live = take.Source.Slots[take.Slot];
            ItemStack receiptItem = new ItemStack(live.itemValue.Clone(), take.Count);
            ItemStack after = live.Clone();
            after.count -= take.Count;
            if (after.count <= 0) after = ItemStack.Empty.Clone();
            take.Source.SetSlot(take.Slot, after);
            ItemStack removed = receiptItem;
            returnReceipts?.Add(new ReturnReceipt {Source=take.Source,Slot=take.Slot,Item=receiptItem.Clone(),Remaining=take.Count});
            if (take.Source is RemoteNpcResourceSource)
            {
                List<ItemStack> staged;
                if (!stagedNpcItems.TryGetValue(take.Source.StableId, out staged))
                    stagedNpcItems.Add(take.Source.StableId, staged = new List<ItemStack>());
                staged.Add(removed);
            }
            else
            {
                result.RemoteItemsConsumed += take.Count;
                if (removedItems != null) removedItems.Add(removed);
            }
            if (marked.Add(take.Source.StableId)) changedSources.Add(take.Source);
        }

        bool sourceCommitFailed = false;
        RemoteResourceLiveSync.BeginBatch();
        try
        {
            for (int i = 0; i < changedSources.Count; i++)
            {
                changedSources[i].MarkModified();
                RemoteNpcResourceSource npcSource = changedSources[i] as RemoteNpcResourceSource;
                if (npcSource != null && !npcSource.LastCommitSucceeded)
                {
                    result.FailureReason = "companion inventory commit failed: " + npcSource.LastCommitError;
                    sourceCommitFailed = true;
                }
                else if (npcSource != null)
                {
                    List<ItemStack> committedItems;
                    if (stagedNpcItems.TryGetValue(npcSource.StableId, out committedItems))
                        PublishCommittedItems(committedItems, result, removedItems);
                }
            }
        }
        finally
        {
            RemoteResourceLiveSync.EndBatch();
        }
        RemoteResourceLiveSync.NotifySourcesChanged(changedSources, sourceCommitFailed ? "transaction commit failed" : "transaction commit");
        if (sourceCommitFailed) return result;

        result.SourcesChanged = marked.Count;
        result.Success = true;
        RemoteResourceDiagnostics.Write("transaction committed local=" + result.LocalItemsConsumed +
            " remote=" + result.RemoteItemsConsumed + " sources=" + result.SourcesChanged);
        return result;
    }

    // Staged companion slots are not removed items until their authority accepts the commit.
    internal static void PublishCommittedItems(IList<ItemStack> committed, ConsumptionResult result, IList<ItemStack> removedItems)
    {
        for (int i = 0; i < committed.Count; i++)
        {
            ItemStack stack = committed[i];
            result.RemoteItemsConsumed += stack.count;
            if (removedItems != null) removedItems.Add(stack);
        }
    }

    private static int PlanLocal(EntityPlayer player, ItemValue itemValue, int needed, Plan plan)
    {
        ItemStack[] bag = player.bag.ItemGrid.items;
        // Match vanilla player-inventory crafting: backpack slot locks do not protect
        // ingredients from crafting. Locks remain respected on REMOTE source inventories.
        for (int slot = 0; bag != null && slot < bag.Length && needed > 0; slot++)
        {
            ItemStack stack = bag[slot];
            if (!MatchesConsumable(stack, itemValue)) continue;
            int take = Math.Min(needed, stack.count);
            byte[] payload=RebirthResourceItemFingerprint.Capture(stack.itemValue);
            if(payload==null)return needed;
            plan.Local.Add(new LocalTake { Toolbelt = false, Slot = slot, Count = take, ItemType = stack.itemValue.type, OriginalCount = stack.count, ItemPayload = payload });
            needed -= take;
        }

        int toolbeltSlots = player.inventory != null
            ? RebirthToolbeltCapacity.GetOwnedSlotCount(player, player.inventory.ItemGrid.items.Length) : 0;
        for (int slot = 0; slot < toolbeltSlots && needed > 0; slot++)
        {
            ItemStack stack = player.inventory.GetItemStack(slot);
            if (!MatchesConsumable(stack, itemValue)) continue;
            int take = Math.Min(needed, stack.count);
            byte[] payload=RebirthResourceItemFingerprint.Capture(stack.itemValue);
            if(payload==null)return needed;
            plan.Local.Add(new LocalTake { Toolbelt = true, Slot = slot, Count = take, ItemType = stack.itemValue.type, OriginalCount = stack.count, ItemPayload = payload });
            needed -= take;
        }
        return needed;
    }

    private static int PlanRemote(
        EntityPlayer player, ItemValue itemValue, int needed, Plan plan, List<IRemoteResourceSource> sources)
    {
        if (sources == null) return needed;
        for (int i = 0; i < sources.Count && needed > 0; i++)
        {
            IRemoteResourceSource source = sources[i];
            string reason;
            if (!RemoteResourceEligibility.CanUse(source, player, true, false, out reason)) continue;
            int revision = source.Revision;
            ItemStack[] slots = source.Slots;
            PackedBoolArray locks = source.SlotLocks;
            for (int slot = 0; slots != null && slot < slots.Length && needed > 0; slot++)
            {
                if (IsLocked(locks, slot)) continue;
                ItemStack stack = slots[slot];
                if (!MatchesConsumable(stack, itemValue)) continue;
                int take = Math.Min(needed, stack.count);
                byte[] payload=RebirthResourceItemFingerprint.Capture(stack.itemValue);
                if(payload==null)return needed;
                plan.Remote.Add(new SlotTake
                {
                    Source = source,
                    Slot = slot,
                    Count = take,
                    Revision = revision,
                    ItemType = stack.itemValue.type,
                    OriginalCount = stack.count,
                    ItemPayload = payload
                });
                plan.SourceIds.Add(source.StableId);
                needed -= take;
            }
        }
        return needed;
    }

    private static bool Validate(EntityPlayer player, Plan plan, out string reason)
    {
        reason = string.Empty;
        for (int i = 0; i < plan.Local.Count; i++)
        {
            LocalTake take = plan.Local[i];
            int limit = take.Toolbelt
                ? (player.inventory != null ? RebirthToolbeltCapacity.GetOwnedSlotCount(player, player.inventory.ItemGrid.items.Length) : 0)
                : (player.bag != null ? player.bag.ItemGrid.items.Length : 0);
            if (take.Slot < 0 || take.Slot >= limit)
            { reason = "player inventory capacity changed during transaction"; return false; }
            ItemStack live = take.Toolbelt
                ? player.inventory.GetItemStack(take.Slot)
                : player.bag.ItemGrid.items[take.Slot];
            if (live == null || live.IsEmpty() || live.itemValue.type != take.ItemType || live.count != take.OriginalCount
                || !RebirthResourceItemFingerprint.Matches(live.itemValue,take.ItemPayload))
            {
                reason = "player inventory changed during transaction";
                return false;
            }
        }

        HashSet<string> checkedSources = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < plan.Remote.Count; i++)
        {
            SlotTake take = plan.Remote[i];
            if (checkedSources.Add(take.Source.StableId))
            {
                RemoteNpcResourceSource npcSource = take.Source as RemoteNpcResourceSource;
                if (npcSource != null && !npcSource.IsSnapshotCurrent())
                {
                    reason = "companion inventory changed during transaction";
                    return false;
                }
                string sourceReason;
                if (!RemoteResourceEligibility.CanUse(take.Source, player, true, false, out sourceReason))
                {
                    reason = "source became unavailable: " + sourceReason;
                    return false;
                }
                if (take.Source.Revision != take.Revision)
                {
                    reason = "source inventory changed during transaction";
                    return false;
                }
            }
            var currentSlots=take.Source.Slots;
            if(currentSlots==null||take.Slot<0||take.Slot>=currentSlots.Length)
            {reason="source inventory capacity changed during transaction";return false;}
            ItemStack live = currentSlots[take.Slot];
            if (live == null || live.IsEmpty() || live.itemValue.type != take.ItemType || live.count != take.OriginalCount
                || !RebirthResourceItemFingerprint.Matches(live.itemValue,take.ItemPayload))
            {
                reason = "source slot changed during transaction";
                return false;
            }
        }
        return true;
    }

    private static Dictionary<int, ItemStack> AggregateRequirements(IList<ItemStack> requirements, int multiplier)
    {
        Dictionary<int, ItemStack> result = new Dictionary<int, ItemStack>();
        for (int i = 0; requirements != null && i < requirements.Count; i++)
        {
            ItemStack required = requirements[i];
            if (required == null || required.IsEmpty() || required.count <= 0) continue;
            ItemStack current;
            int count = required.count * multiplier;
            if (result.TryGetValue(required.itemValue.type, out current)) current.count += count;
            else result[required.itemValue.type] = new ItemStack(required.itemValue.Clone(), count);
        }
        return result;
    }

    private static int CountLocal(EntityPlayer player, ItemValue itemValue)
    {
        int total = 0;
        ItemStack[] bag = player.bag.ItemGrid.items;
        for (int i = 0; bag != null && i < bag.Length; i++)
            if (MatchesConsumable(bag[i], itemValue)) total += bag[i].count;
        int toolbeltSlots = player.inventory != null
            ? RebirthToolbeltCapacity.GetOwnedSlotCount(player, player.inventory.ItemGrid.items.Length) : 0;
        for (int i = 0; i < toolbeltSlots; i++)
        {
            ItemStack stack = player.inventory.GetItemStack(i);
            if (MatchesConsumable(stack, itemValue)) total += stack.count;
        }
        return total;
    }

    private static bool MatchesConsumable(ItemStack stack, ItemValue required)
    {
        return stack != null && !stack.IsEmpty() && stack.count > 0 &&
               stack.itemValue.type == required.type &&
               (!required.HasMetadata("rebirth.cooking.fullIngredient") || RebirthCookingItemStats.FullIngredient(stack.itemValue)) &&
               (!stack.itemValue.HasModSlots || !stack.itemValue.HasMods());
    }

    private static bool IsLocked(PackedBoolArray locks, int slot)
    {
        return locks != null && slot >= 0 && slot < locks.Length && locks[slot];
    }
}
