using System;
using System.Xml.Linq;

// Server-only decision helper. No offer, item effects, reply or owner release.
internal static class RebirthRemoteGearPreparationRefusal
{
    internal static bool TryRecordStale(EntityPlayer player,ClientInfo sender,string marker,out RebirthGearPreparationRefusal refusal)
    {
        refusal=null;
        try
        {
            if(player==null||sender==null||!ThreadManager.IsMainThread()||
                !RebirthGearPreparationMarker.TryRead(marker,1f,out var savedWorld,out _,out var intent))return false;
            var game=GameManager.Instance;var world=game?.World;var nativeState=world?.worldState;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(world==null||nativeState==null||world.IsRemote()||manager==null||!manager.IsServer||manager.Clients==null||
                !ReferenceEquals(manager.Clients.ForEntityId(player.entityId),sender)||
                !Guid.TryParse(nativeState.Guid,out var nativeWorld)||nativeWorld!=savedWorld||
                !RebirthRemoteGearInventorySource.TryResolve(player,sender,intent.CreationId,out var record)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var authenticated)||authenticated==null||
                identity.CanonicalId!=authenticated.CanonicalId||identity.StorageKey!=authenticated.StorageKey)return false;
            var support=record.Support;long revision=support.GearRevision;string nativeGuid=nativeState.Guid;
            var candidate=support.PendingGearPreparationRefusal;
            if(candidate==null&&!RebirthGearPreparationRefusal.TryCreateStale(marker,revision,out candidate))return false;
            if(!candidate.MatchesOriginal(marker)||candidate.ObservedRevision!=revision||
                !candidate.MatchesSupport(intent.CreationId,revision,
                    support.PendingGearTransfer!=null||support.PendingLibraryTransfer!=null||support.PendingMusicTransfer!=null,
                    support.LastGearSettlement?.TransactionId))return false;
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&
                ReferenceEquals(game.World,world)&&!world.IsRemote()&&ReferenceEquals(world.worldState,nativeState)&&nativeState.Guid==nativeGuid&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&manager.Clients!=null&&
                ReferenceEquals(manager.Clients.ForEntityId(player.entityId),sender)&&
                RebirthRemoteGearInventorySource.TryResolve(player,sender,intent.CreationId,out var live)&&ReferenceEquals(live,record)&&
                ReferenceEquals(record.Support,support)&&support.GearRevision==revision&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&
                RebirthWorldCharacterService.TryGetIdentity(player,out var owner)&&owner!=null&&owner.CanonicalId==identity.CanonicalId&&owner.StorageKey==identity.StorageKey&&
                RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var peer)&&peer!=null&&peer.CanonicalId==identity.CanonicalId&&peer.StorageKey==identity.StorageKey&&
                support.PendingGearTransfer==null&&support.PendingLibraryTransfer==null&&support.PendingMusicTransfer==null&&
                (support.PendingGearPreparationRefusal==null||ReferenceEquals(support.PendingGearPreparationRefusal,candidate))&&
                support.LastGearSettlement?.TransactionId!=intent.TransactionId.ToString("N")&&
                RebirthGearPreparationPlayerFileWitness.TryReadOriginalPhase(identity,savedWorld,out var savedMarker,out var savedIntent,out var savedImage,out var phase)&&
                savedMarker==marker&&phase==0f&&XNode.DeepEquals(savedIntent.Write(),intent.Write())&&intent.MatchesInventory(savedImage)&&
                RebirthRemoteGearInventorySource.TryCapture(player,sender,intent.CreationId,out var uploaded)&&intent.MatchesInventory(uploaded)&&
                RebirthWorldCharacterRepository.HasSavedUnpreparedGearBase(identity,record,marker);
            if(!current())return false;
            if(support.PendingGearPreparationRefusal==null)
            {support.PendingGearPreparationRefusal=candidate;}
            if(!current())return false;
            // An uncertain save may have cleared dirty bookkeeping; retry the SAME original.
            record.Touch("remote-gear-unprepared-stale-refusal");
            try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"remote-gear-unprepared-stale-refusal");}catch{}
            if(!current()||!RebirthWorldCharacterRepository.HasSavedGearPreparationRefusal(identity,candidate)||!current())return false;
            refusal=candidate;return true;
        }
        catch{return false;} // Uncertain write retains the SAME original refusal for retry.
    }
}