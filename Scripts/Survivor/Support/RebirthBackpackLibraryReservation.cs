using System;
using System.Collections.Generic;
using System.Xml.Linq;

// Session reservation only; durable unresolved custody remains in the character journal.
// Native cursor/action guards consult this; full automatic inventory/lifecycle coverage remains pending.
public static class RebirthBackpackLibraryReservation
{
    private sealed class Entry {public object World;public RebirthBackpackLibraryReceipt Receipt;public HashSet<int> Slots;}
    private static readonly Dictionary<EntityPlayerLocal,Entry> Entries=new Dictionary<EntityPlayerLocal,Entry>();
    private static bool Current(EntityPlayerLocal player,string creation,RebirthBackpackLibraryReceipt receipt)
        =>player!=null&&player.world!=null&&GameManager.Instance!=null&&
        ReferenceEquals(player.world,GameManager.Instance.World)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
        receipt!=null&&RebirthSurvivorRequestScope.Matches(creation,receipt.CreationId);
    public static bool TryAcquire(EntityPlayerLocal player,Guid creation,RebirthBackpackLibraryReceipt receipt)
        =>TryAcquire(player,creation.ToString("N"),receipt);
    public static bool TryAcquire(EntityPlayerLocal player,string creation,RebirthBackpackLibraryReceipt receipt)
    {
        if(!Current(player,creation,receipt))return false;
        if(Entries.TryGetValue(player,out var old))return ReferenceEquals(old.World,player.world)&&
            XNode.DeepEquals(old.Receipt.ToXml(),receipt.ToXml());
        var xui=player.PlayerUI?.xui;
        if(!player.IsSpawned()||player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player)||
            !RebirthSurvivorMode.IsEnabledForCurrentWorld()||RebirthGearOwnerReservation.IsHeld(player)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)||player.inventory==null||
            player.inventory.IsHoldingItemActionRunning()||xui==null||xui.IsUsingItemActionEntryUse||
            xui.DragAndDropWindow==null||xui.DragAndDropWindow.CurrentStack==null||
            (!receipt.IsCursor&&!xui.DragAndDropWindow.CurrentStack.IsEmpty()))return false;
        var slots=new HashSet<int>{receipt.InventorySlot}; if(receipt.IsBatchSale){if(!receipt.TryGetWallet(out var wallet))return false;slots.Clear();foreach(var change in wallet)slots.Add(change.Slot);} Entries.Add(player,new Entry{World=player.world,Receipt=receipt,Slots=slots});return true;
    }
    public static bool IsHeld(EntityPlayerLocal player)
        =>player!=null&&Entries.TryGetValue(player,out var entry)&&ReferenceEquals(entry.World,player.world);
    public static bool IsReservedSlot(EntityPlayerLocal player,bool bag,int slot)
        =>player!=null&&slot>=0&&Entries.TryGetValue(player,out var entry)&&ReferenceEquals(entry.World,player.world)&&
        !entry.Receipt.IsCursor&&entry.Receipt.IsBag==bag&&entry.Slots.Contains(slot);

    public static bool IsReservedInventorySlot(object inventory,bool bag,int slot)
    {
        foreach(var pair in Entries)if(pair.Value.Receipt.IsBag==bag&&ReferenceEquals(pair.Value.World,pair.Key.world)&&ReferenceEquals(inventory,bag?(object)pair.Key.bag:pair.Key.inventory))return pair.Value.Slots.Contains(slot);
        return false;
    }
    public static bool BlocksHeldUse(EntityPlayerLocal player)
        =>player?.inventory!=null&&IsReservedSlot(player,false,player.inventory.holdingItemIdx);
    public static bool TryGetReservedSlot(object inventory,bool bag,out int slot)
    {
        slot=-1;if(inventory==null)return false;
        foreach(var pair in Entries)
        {
            if(pair.Value.Receipt.IsCursor||pair.Value.Receipt.IsBag!=bag||!ReferenceEquals(pair.Value.World,pair.Key.world))continue;
            object actual=bag?(object)pair.Key.bag:pair.Key.inventory;
            if(!ReferenceEquals(actual,inventory))continue;
            slot=pair.Value.Receipt.InventorySlot;return true;
        }
        return false;
    }
    public static bool BlocksResourceUse(EntityPlayer player)
    {
        if(player==null)return false;
        if(IsHeld(player as EntityPlayerLocal)||RebirthGearOwnerReservation.IsHeld(player as EntityPlayerLocal))return true;
        return RebirthWorldCharacterRepository.IsServerAuthority&&RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
            RebirthWorldCharacterService.TryGet(player,out var record)&&(record?.Support?.PendingLibraryTransfer!=null||record?.Support?.PendingGearTransfer!=null);
    }
    public static bool Matches(EntityPlayerLocal player,Guid creation,RebirthBackpackLibraryReceipt receipt)
        =>Matches(player,creation.ToString("N"),receipt);
    public static bool Matches(EntityPlayerLocal player,string creation,RebirthBackpackLibraryReceipt receipt)
        =>Current(player,creation,receipt)&&Entries.TryGetValue(player,out var entry)&&
        ReferenceEquals(entry.World,player.world)&&XNode.DeepEquals(entry.Receipt.ToXml(),receipt.ToXml());
    // Only authenticated authoritative settlement may release a live reservation.
    public static bool ReleaseSettled(EntityPlayerLocal player,Guid creation,RebirthBackpackLibraryReceipt receipt,
        Func<RebirthBackpackLibraryReceipt,bool> verifyAuthoritativeSettlement)
        =>ReleaseSettled(player,creation.ToString("N"),receipt,verifyAuthoritativeSettlement);
    public static bool ReleaseSettled(EntityPlayerLocal player,string creation,RebirthBackpackLibraryReceipt receipt,
        Func<RebirthBackpackLibraryReceipt,bool> verifyAuthoritativeSettlement)
    {
        if(!Matches(player,creation,receipt)||verifyAuthoritativeSettlement==null)return false;
        var original=Entries[player];
        try {if(!verifyAuthoritativeSettlement(receipt))return false;}
        catch{return false;}
        if(!Matches(player,creation,receipt)||!Entries.TryGetValue(player,out var current)||
            !ReferenceEquals(current,original))return false;
        return Entries.Remove(player);
    }
    public static void ResetSession(){Entries.Clear();}
    // World shutdown forgets session references, never cancels/refunds persistent intent.
    public static void ForgetWorld(object world)
    {
        if(world==null)return;
        var remove=new List<EntityPlayerLocal>();
        foreach(var pair in Entries)if(ReferenceEquals(pair.Value.World,world))remove.Add(pair.Key);
        foreach(var player in remove)Entries.Remove(player);
    }
}
