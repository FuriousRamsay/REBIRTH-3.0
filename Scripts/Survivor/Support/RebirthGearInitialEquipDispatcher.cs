using System;

// Initial action only. Once acquired, the existing hold retains the SAME intent
// through save/upload failure and the continuation pump owns all retries.
internal static class RebirthGearInitialEquipDispatcher
{
    internal static bool TryBeginUnequip(EntityPlayerLocal player,string slot)=>TryBeginUnequip(player,slot,out _);
    internal static bool TryBeginUnequip(EntityPlayerLocal player,string slot,out Guid transaction)
    {
        transaction=Guid.Empty;
        try
        {
            if(!RebirthGearPreparationClient.TryCreateUnequipOriginal(player,slot,Guid.NewGuid(),out var original))return false;
            bool queued=RebirthGearPreparationClient.TryRequestOriginal(player,original);
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peers=manager?.connectionToServer;
            bool admitted=queued||(peers!=null&&peers.Length>0&&peers[0]!=null&&
                RebirthGearOwnerReservation.MatchesIntent(player,peers[0],original));
            if(admitted)transaction=original.TransactionId;return admitted;
        }
        catch{return false;}
    }
    internal static bool TryBegin(EntityPlayerLocal player,ItemValue selected,bool bag,int index)
    {
        try
        {
            if(player==null||selected==null||selected.IsEmpty()||selected.ItemClass==null||
                !RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(selected.ItemClass.GetItemName(),out var profile)||
                profile==null||profile.Kind!="survivor_gear")return false;
            if(!RebirthGearPreparationClient.TryCreateOriginal(player,selected,bag,index,Guid.NewGuid(),out var original))return false;
            bool queued=RebirthGearPreparationClient.TryRequestOriginal(player,original);
            // A refused/uncertain upload may have durably or transiently acquired
            // custody. That original must continue; never fall back to legacy use.
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var peers=manager?.connectionToServer;
            return queued||(peers!=null&&peers.Length>0&&peers[0]!=null&&
                RebirthGearOwnerReservation.MatchesIntent(player,peers[0],original));
        }
        catch{return false;}
    }
}