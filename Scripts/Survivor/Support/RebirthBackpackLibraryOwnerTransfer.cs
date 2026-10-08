using System;

public enum RebirthBackpackLibraryOwnerResult { Pending, Applied, Rejected, Indeterminate }
// Only call for an authenticated server offer, with owner interaction reservation held.
// Applied is local only: server must verify saved native player data before journal commit.
public static class RebirthBackpackLibraryOwnerTransfer
{
    public static string ReceiptKey(RebirthBackpackLibraryReceipt receipt)=>"rbLibrary_"+receipt.CreationId+"_"+receipt.TransactionId;
    private static bool Same(ItemStack actual,ItemStack expected)
    {
        if(actual==null||expected==null||actual.count!=expected.count||actual.count<0)return false;
        if(actual.count==0)return true;
        return actual.itemValue!=null&&expected.itemValue!=null&&
            string.Equals(RebirthNativeItemCodec.Encode(actual.itemValue),RebirthNativeItemCodec.Encode(expected.itemValue),StringComparison.Ordinal);
    }
    // Recovery requires the same authenticated reservation as Apply. Never repeats the setter.
    public static bool RecoverApplied(EntityPlayerLocal player,Guid currentCreation,RebirthBackpackLibraryReceipt offer)
        =>RecoverApplied(player,currentCreation.ToString("N"),offer);
    public static bool RecoverApplied(EntityPlayerLocal player,string currentCreation,RebirthBackpackLibraryReceipt offer)
    {
        if(player==null||player.world==null||player.Buffs==null||offer==null||
            !RebirthSurvivorRequestScope.Matches(currentCreation,offer.CreationId)||!RebirthBackpackLibraryReservation.Matches(player,currentCreation,offer)||!player.IsSpawned()||player.IsDead()||
            !RebirthSurvivorMode.IsEnabledForCurrentWorld()||RebirthCharacterCreationHoldService.IsHeld(player))return false;
        string key=ReceiptKey(offer);
        if(player.Buffs.GetCustomVar(key)!=2f)return false;
        try
        {
            var slots=offer.IsCursor?new[]{player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack}:offer.IsBag?player.bag?.ItemGrid.items:player.inventory?.ItemGrid.items;
            if(slots==null)return false;
            int owned=offer.IsCursor?1:offer.IsBag?slots.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,slots.Length);
            int index=offer.InventorySlot;
            if(index<0||index>=owned||!offer.TryGetImages(out _,out _,out _,out var after)||!Same(slots[index],after))return false;
            player.Buffs.SetCustomVar(key,1f,true);
            return true;
        }
        catch{return false;}
    }
    public static RebirthBackpackLibraryOwnerResult Apply(EntityPlayerLocal player,Guid currentCreation,
        RebirthBackpackLibraryReceipt offer)=>Apply(player,currentCreation.ToString("N"),offer);
    public static RebirthBackpackLibraryOwnerResult Apply(EntityPlayerLocal player,string currentCreation,
        RebirthBackpackLibraryReceipt offer)
    {
        if(player==null||player.world==null||player.Buffs==null||offer==null||
            !RebirthSurvivorRequestScope.Matches(currentCreation,offer.CreationId)||!RebirthBackpackLibraryReservation.Matches(player,currentCreation,offer))return RebirthBackpackLibraryOwnerResult.Pending;
        string receipt=ReceiptKey(offer);
        float previous=player.Buffs.GetCustomVar(receipt);
        if(previous==1f)return RebirthBackpackLibraryOwnerResult.Applied;
        if(previous==-1f)return RebirthBackpackLibraryOwnerResult.Rejected;
        if(previous!=0f)return RebirthBackpackLibraryOwnerResult.Indeterminate;
        if(!player.IsSpawned()||player.IsDead()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
            RebirthCharacterCreationHoldService.IsHeld(player))return RebirthBackpackLibraryOwnerResult.Pending;
        var slots=offer.IsCursor?new[]{player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack}:offer.IsBag?player.bag?.ItemGrid.items:player.inventory?.ItemGrid.items;
        if(slots==null)return RebirthBackpackLibraryOwnerResult.Pending;
        int owned=offer.IsCursor?1:offer.IsBag?slots.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,slots.Length);
        int index=offer.InventorySlot;
        if(index<0||index>=owned)return RebirthBackpackLibraryOwnerResult.Pending;
        var locks=offer.IsBag?player.bag.LockedSlots:null;
        if(locks!=null&&index<locks.Length&&locks[index])return RebirthBackpackLibraryOwnerResult.Pending;
        try
        {
            if(!offer.TryGetImages(out _,out _,out var before,out var after)||!Same(slots[index],before))
            {
                player.Buffs.SetCustomVar(receipt,-1f,true);
                return RebirthBackpackLibraryOwnerResult.Rejected;
            }
            // A native setter may change inventory and then throw from a listener.
            // The applying receipt prevents another request from repeating an uncertain mutation.
            player.Buffs.SetCustomVar(receipt,2f,true);
            if(offer.IsCursor)player.PlayerUI.xui.DragAndDropWindow.SetCurrentStack(after.Clone());
            else if(offer.IsBag)player.bag.SetSlot(index,after.Clone());else player.inventory.SetItem(index,after.Clone());
            player.Buffs.SetCustomVar(receipt,1f,true);
            return RebirthBackpackLibraryOwnerResult.Applied;
        }
        catch(Exception){return RebirthBackpackLibraryOwnerResult.Indeterminate;}
    }
}

