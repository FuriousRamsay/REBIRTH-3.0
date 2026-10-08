using System;

// Server retirement acknowledgment only; no item moves, offers or revision increment.
internal static class RebirthRemoteGearRefusalRetirement
{
    internal static bool TryConfirm(EntityPlayer player,ClientInfo sender,string marker,out RebirthGearPreparationRefusal retired)
    {
        retired=null;
        try
        {
            if(player==null||sender==null||!ThreadManager.IsMainThread()||
                !RebirthGearPreparationMarker.TryRead(marker,1f,out var savedWorld,out _,out var intent))return false;
            var game=GameManager.Instance;var world=game?.World;var state=world?.worldState;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(state==null||world.IsRemote()||manager==null||!manager.IsServer||manager.Clients==null||
                !ReferenceEquals(manager.Clients.ForEntityId(player.entityId),sender)||
                !Guid.TryParse(state.Guid,out var nativeWorld)||nativeWorld!=savedWorld||
                !RebirthRemoteGearInventorySource.TryResolve(player,sender,intent.CreationId,out var record)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var owner)||owner==null||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var peer)||peer==null||
                owner.CanonicalId!=peer.CanonicalId||owner.StorageKey!=peer.StorageKey)return false;
            var support=record.Support;var refusal=support.PendingGearPreparationRefusal??support.LastGearPreparationRefusal;
            if(refusal==null||!refusal.MatchesOriginal(marker))return false;
            var uploaded=sender.latestPlayerData;string stateGuid=state.Guid;
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&
                ReferenceEquals(game.World,world)&&!world.IsRemote()&&ReferenceEquals(world.worldState,state)&&state.Guid==stateGuid&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&manager.Clients!=null&&
                ReferenceEquals(manager.Clients.ForEntityId(player.entityId),sender)&&ReferenceEquals(sender.latestPlayerData,uploaded)&&
                RebirthRemoteGearInventorySource.TryResolve(player,sender,intent.CreationId,out var live)&&ReferenceEquals(live,record)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&ReferenceEquals(record.Support,support)&&
                RebirthWorldCharacterService.TryGetIdentity(player,out var identity)&&identity!=null&&identity.CanonicalId==owner.CanonicalId&&identity.StorageKey==owner.StorageKey&&
                RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var client)&&client!=null&&client.CanonicalId==owner.CanonicalId&&client.StorageKey==owner.StorageKey&&
                support.GearRevision==refusal.ObservedRevision&&support.PendingGearTransfer==null&&support.PendingLibraryTransfer==null&&support.PendingMusicTransfer==null&&
                (ReferenceEquals(support.PendingGearPreparationRefusal,refusal)||support.PendingGearPreparationRefusal==null&&ReferenceEquals(support.LastGearPreparationRefusal,refusal))&&
                (RebirthGearPreparationRefusalPlayerFileWitness.HasRejectedOriginal(owner,refusal)&&
                 RebirthGearPreparationRefusalPlayerFileWitness.MatchesRejectedOriginalData(uploaded,refusal)||
                 support.PendingGearPreparationRefusal==null&&ReferenceEquals(support.LastGearPreparationRefusal,refusal)&&
                 RebirthWorldCharacterRepository.HasSavedGearPreparationRefusalRetirement(owner,refusal)&&
                 RebirthGearPreparationRefusalPlayerFileWitness.HasRetired(owner,refusal)&&
                 RebirthGearPreparationRefusalPlayerFileWitness.MatchesRetiredData(uploaded,refusal));
            if(!current()||!RebirthWorldCharacterRepository.HasSavedGearRefusalRetirementBase(owner,record,refusal)||!current())return false;
            support.LastGearPreparationRefusal=refusal;support.PendingGearPreparationRefusal=null;
            if(!current())return false;
            record.Touch("remote-gear-refusal-retired");
            try{RebirthWorldCharacterRepository.SaveIfDirty(owner,"remote-gear-refusal-retired");}catch{}
            if(!current()||!RebirthWorldCharacterRepository.HasSavedGearPreparationRefusalRetirement(owner,refusal)||!current())return false;
            retired=refusal;return true;
        }
        catch{return false;} // SAME terminal remains staged after an uncertain write.
    }
}