using System;
using System.Collections.Generic;
using System.Linq;

// One native inventory replacement after ALL exact debits validate. Local success
// still requires native player save and independent saved-receipt verification.
public static class RebirthVehicleOwnerDebitBatch
{
    public static RebirthVehicleOwnerTransferResult Apply(EntityPlayerLocal player, Guid currentCreation,
        Guid offeredCreation, IEnumerable<RebirthVehicleTransferItem> operations, Func<bool> isCurrentSession)
        =>Apply(player,currentCreation.ToString("N"),offeredCreation.ToString("N"),operations,isCurrentSession);
    public static RebirthVehicleOwnerTransferResult Apply(EntityPlayerLocal player, string currentCreation,
        string offeredCreation, IEnumerable<RebirthVehicleTransferItem> operations, Func<bool> isCurrentSession)
    {
        if (player?.Buffs == null || !RebirthVehicleOwnerCustody.CreationMatches(player,currentCreation,offeredCreation,operations)
            || operations == null || isCurrentSession == null || !isCurrentSession())
            return RebirthVehicleOwnerTransferResult.Pending;
        var items = operations.Take(257).ToArray();
        if (items.Length == 0 || items.Length > 256 || items.Any(i => i == null || !i.Debit)
            || items.Select(i => i.Source).Distinct().Count() != 1
            || items.Select(i => i.Slot).Distinct().Count() != items.Length
            || items.Select(i => i.ReceiptId).Distinct().Count() != items.Length)
            return RebirthVehicleOwnerTransferResult.Indeterminate;
        var keys = items.Select(i => "rbVehicle_" + i.ReceiptId.ToString("N")).ToArray();
        var previous = keys.Select(player.Buffs.GetCustomVar).ToArray();
        RebirthVehicleOwnerCustody.TryGetPlan(player,items,out var boundPlan);
        if(previous.Any(value=>value==2f)&&boundPlan!=null)
        {
            if(!RebirthVehicleOwnerOriginal.ResolveApplying(player,boundPlan,items,out var retry))return RebirthVehicleOwnerTransferResult.Indeterminate;
            if(!retry)return RebirthVehicleOwnerTransferResult.Applied;
            previous=previous.Select(_=>0f).ToArray();
        }
        if(previous.All(value=>value==1f)&&boundPlan!=null)return RebirthVehicleOwnerOriginal.ResolveApplying(player,boundPlan,items,out var completedRetry)&&!completedRetry?
            RebirthVehicleOwnerTransferResult.Applied:RebirthVehicleOwnerTransferResult.Indeterminate;
        if (previous.All(v => v == 1f)) return RebirthVehicleOwnerCheckpoint.Persist(player,items.Select(i=>i.ReceiptId),1f,isCurrentSession)?RebirthVehicleOwnerTransferResult.Applied:RebirthVehicleOwnerTransferResult.Indeterminate;
        if (previous.All(v => v == -1f)) return RebirthVehicleOwnerCheckpoint.Persist(player,items.Select(i=>i.ReceiptId),-1f,isCurrentSession)?RebirthVehicleOwnerTransferResult.Rejected:RebirthVehicleOwnerTransferResult.Indeterminate;
        if (previous.Any(v => v != 0f)) return RebirthVehicleOwnerTransferResult.Indeterminate;
        if (!player.IsSpawned() || player.IsDead() || !RebirthSurvivorMode.IsEnabledForCurrentWorld()&&boundPlan?.NativeDeaths!=player.Died)
            return RebirthVehicleOwnerTransferResult.Pending;
        bool bag = items[0].Source == RebirthVehiclePartSourceLocation.Backpack;
        if(!RebirthVehicleInventoryWrite.CanMutate(player,bag))return RebirthVehicleOwnerTransferResult.Pending;
        var live = bag ? player.bag?.ItemGrid.items : player.inventory?.ItemGrid.items;
        if (live == null) return RebirthVehicleOwnerTransferResult.Pending;
        var beforeBag=RebirthVehicleInventoryWrite.Snapshot(player.bag.ItemGrid.items);var beforeBelt=RebirthVehicleInventoryWrite.Snapshot(player.inventory.ItemGrid.items);
        bool FullBefore()=>RebirthVehicleInventoryWrite.SameSlots(beforeBag,player.bag?.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(beforeBelt,player.inventory?.ItemGrid.items);
        int owned = bag ? live.Length : RebirthToolbeltCapacity.GetOwnedSlotCount(player, live.Length);
        if (owned < 0 || owned > live.Length) return RebirthVehicleOwnerTransferResult.Pending;
        bool valid = true;
        foreach (var item in items)
        {
            int slot = item.Slot;
            if (slot < 0 || slot >= owned || RebirthVehicleNativePolicy.IsLocked(player,bag,slot)
                || live[slot] == null || live[slot].IsEmpty() || live[slot].count < item.Count
                || RebirthNativeItemCodec.Encode(live[slot].itemValue) != item.ItemData)
            { valid = false; break; }
        }
        ItemStack[] candidate = null;
        if (valid)
        {
            candidate = new ItemStack[live.Length];
            for (int i = 0; i < live.Length; i++) candidate[i] = live[i]?.Clone() ?? ItemStack.Empty.Clone();
            foreach (var item in items)
            {
                candidate[item.Slot].count -= item.Count;
                if (candidate[item.Slot].count == 0) candidate[item.Slot] = ItemStack.Empty.Clone();
            }
        }
        if (!isCurrentSession()) return RebirthVehicleOwnerTransferResult.Pending;
        try
        {
            if(!FullBefore())return RebirthVehicleOwnerTransferResult.Indeterminate;
            var expected=valid?RebirthVehicleInventoryWrite.Snapshot(candidate):null;
            var expectedBag=bag?expected:beforeBag;var expectedBelt=bag?beforeBelt:expected;
            if(valid&&boundPlan!=null&&!RebirthVehicleOwnerOriginal.Prepare(player,boundPlan,items,bag,candidate))return RebirthVehicleOwnerTransferResult.Indeterminate;
            if(!FullBefore()||!isCurrentSession())return RebirthVehicleOwnerTransferResult.Indeterminate;
            foreach (string key in keys) player.Buffs.SetCustomVar(key, valid ? 2f : -1f, true);
            if (!valid) return FullBefore()&&keys.All(key=>player.Buffs.GetCustomVar(key)==-1f)&&RebirthVehicleOwnerCheckpoint.Persist(player,items.Select(i=>i.ReceiptId),-1f,isCurrentSession) ? RebirthVehicleOwnerTransferResult.Rejected : RebirthVehicleOwnerTransferResult.Indeterminate;
            if(!RebirthVehicleOwnerCheckpoint.Persist(player,items.Select(i=>i.ReceiptId),2f,isCurrentSession))return RebirthVehicleOwnerTransferResult.Indeterminate;
            if (!isCurrentSession() || keys.Any(key=>player.Buffs.GetCustomVar(key)!=2f) || !RebirthVehicleInventoryWrite.CanMutate(player,bag) ||
                !FullBefore())
                return RebirthVehicleOwnerTransferResult.Indeterminate;
            // Inventory.SetSlots can throw after partially applying toolbelt slots;
            // every receipt is already uncertain, so no automatic retry can debit again.
            if(RebirthVehicleOwnerCustody.NativeCanEnter(player,bag))RebirthVehicleOwnerCustody.WriteSlots(player,bag,candidate,items,isCurrentSession);
            else if (bag) player.bag.SetSlots(candidate);
            else player.inventory.SetSlots(candidate);
            if (!isCurrentSession()) return RebirthVehicleOwnerTransferResult.Indeterminate;
            if(!RebirthVehicleInventoryWrite.SameSlots(expectedBag,player.bag?.ItemGrid.items)||!RebirthVehicleInventoryWrite.SameSlots(expectedBelt,player.inventory?.ItemGrid.items))return RebirthVehicleOwnerTransferResult.Indeterminate;
            foreach (string key in keys) player.Buffs.SetCustomVar(key, 1f, true);
            return RebirthVehicleInventoryWrite.SameSlots(expectedBag,player.bag?.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(expectedBelt,player.inventory?.ItemGrid.items)&&
                keys.All(key=>player.Buffs.GetCustomVar(key)==1f)&&RebirthVehicleOwnerCheckpoint.Persist(player,items.Select(i=>i.ReceiptId),1f,isCurrentSession) ? RebirthVehicleOwnerTransferResult.Applied : RebirthVehicleOwnerTransferResult.Indeterminate;
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Vehicle] owner debit batch requires reconciliation " + ex.GetType().Name + ": " + ex.Message);
            return RebirthVehicleOwnerTransferResult.Indeterminate;
        }
    }
}
