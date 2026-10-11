using System;
using UnityEngine;
public static partial class RebirthBackpackLibraryServer
{
    public static bool PrepareBatchSale(EntityPlayer player,ClientInfo sender,string creation,long revision,int traderId,out RebirthBackpackLibraryReceipt receipt)
    {
        receipt=null;RebirthWorldCharacterRecord record;
        bool local=player is EntityPlayerLocal&&sender==null;
        if(!(local?ResolveLocal((EntityPlayerLocal)player,creation,out record):Resolve(player,sender,creation,out record))||player.IsDead()||!player.IsSpawned()||RebirthCharacterCreationHoldService.IsHeld(player))return false;
        var trader=player.world.GetEntity(traderId) as EntityTrader;
        if(trader==null||trader.IsDead()||(trader.position-player.position).sqrMagnitude>100f||trader.TraderData?.TraderInfo?.AllowSell!=true)return false;
        var state=record.Support;
        if(state.GearRevision!=revision||state.PendingLibraryTransfer!=null||state.PendingGearTransfer!=null||state.PendingMusicTransfer!=null||
            !state.EquippedGearBySlot.TryGetValue("backpack",out var item)||string.IsNullOrEmpty(item)||!state.EquippedGearItemDataBySlot.TryGetValue("backpack",out var data)||!RebirthNativeItemCodec.TryDecode(data,out var pack)||pack.ItemClass?.GetItemName()!=item)return false;
        var bag=local?player.bag?.ItemGrid.items:RebirthPlayerDataInventory.ReadSlots(sender.latestPlayerData,true);
        if(!RebirthStashBatchPrice.Plan(player,trader.TraderData,pack,out var after,out _,out int total)||
            !RebirthBackpackLibraryReceipt.TryCreateSale(creation,revision,pack,after,bag,player.bag?.LockedSlots,total,out var planned,traderId)||
            !RebirthBackpackLibraryJournal.Prepare(state,planned,creation,()=>Save(player,record)))return false;
        receipt=planned;return true;
    }
}
