using System;
using System.IO;

// Read-only observation, NOT a durable delivery receipt or permission to respawn.
internal static class RebirthRecoveryWorldDiskEvidence
{
    // Records only authenticated, original saved publication evidence. Never spawns.
    internal static bool TryRecordReceipt(EntityPlayer player,ClientInfo sender,string creationId,string transactionId,Guid publicationId,int chunkX,int chunkY,int chunkZ,int backpackClassId,int itemClassId)
    {
        if(!ThreadManager.IsMainThread()||!RebirthRemoteGearInventorySource.TryResolve(player,sender,creationId,out var record)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||identity.StorageKey!=record.StablePlayerKey)return false;
        var original=record.Support.PendingGearTransfer;string key=identity.StorageKey;
        if(original==null||original.TransactionId!=transactionId||original.HasRecoveryPublicationReceipt(publicationId))return false;
        Func<RebirthGearTransferState,bool> current=pending=>ThreadManager.IsMainThread()&&
            RebirthRemoteGearInventorySource.TryResolve(player,sender,creationId,out var live)&&ReferenceEquals(live,record)&&
            RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&RebirthWorldCharacterService.TryGetIdentity(player,out var liveIdentity)&&liveIdentity!=null&&liveIdentity.StorageKey==key&&record.StablePlayerKey==key&&
            ReferenceEquals(record.Support.PendingGearTransfer,pending)&&RebirthGearTransferSavedWitness.Matches(record.Support,creationId,pending,RebirthGearTransferPhase.GearCommitted);
        if(!current(original)||!TryObserve(player,sender,creationId,transactionId,publicationId,chunkX,chunkY,chunkZ,backpackClassId,itemClassId)||!current(original))return false;
        return RebirthGearTransferJournal.RecordRecoveryPublicationReceipt(record.Support,transactionId,creationId,publicationId,()=>
        {
            var updated=record.Support.PendingGearTransfer;
            if(updated==null||!updated.HasRecoveryPublicationReceipt(publicationId)||!current(updated))return false;
            RebirthWorldCharacterService.MarkDirty(record,"remote-gear-recovery-publication");
            try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"remote-gear-recovery-publication");}catch{}
            return current(updated)&&RebirthWorldCharacterRepository.HasSavedGearTransfer(identity,updated,RebirthGearTransferPhase.GearCommitted)&&current(updated);
        });
    }
    internal static bool TryObserve(EntityPlayer player,ClientInfo sender,string creationId,string transactionId,Guid publicationId,int chunkX,int chunkY,int chunkZ,int backpackClassId,int itemClassId)
    {
        if(!ThreadManager.IsMainThread())return false;
        try
        {
            var game=GameManager.Instance;var world=player?.world;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(game==null||world==null||!ReferenceEquals(game.World,world)||world.IsRemote()||manager==null||!manager.IsServer||
                !RebirthRemoteGearInventorySource.TryResolve(player,sender,creationId,out var record)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||identity.StorageKey!=record.StablePlayerKey)return false;
            var pending=record.Support.PendingGearTransfer;string ownerKey=identity.StorageKey;
            if(pending==null||pending.TransactionId!=transactionId||record.Support.GearTransferPhase!=RebirthGearTransferPhase.GearCommitted||
                !pending.TryGetRecoveryAttempt(publicationId,out var attempt)||attempt.OriginalOwnerEntityId<=0)return false;
            var cluster=world.ChunkCache;var provider=cluster?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
            if(regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrWhiteSpace(regions.saveDirectory))return false;
            string directory=Path.GetFullPath(regions.saveDirectory);var access=regions.regionFileAccess;
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(game.World,world)&&ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&
                ReferenceEquals(world,player.world)&&ReferenceEquals(cluster,world.ChunkCache)&&ReferenceEquals(provider,cluster.ChunkProvider)&&ReferenceEquals(regions,provider.m_RegionFileManager)&&ReferenceEquals(access,regions.regionFileAccess)&&
                Path.GetFullPath(regions.saveDirectory)==directory&&RebirthRemoteGearInventorySource.TryResolve(player,sender,creationId,out var live)&&ReferenceEquals(live,record)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&RebirthWorldCharacterService.TryGetIdentity(player,out var liveIdentity)&&liveIdentity!=null&&liveIdentity.StorageKey==ownerKey&&record.StablePlayerKey==ownerKey&&
                ReferenceEquals(pending,record.Support.PendingGearTransfer)&&RebirthGearTransferSavedWitness.Matches(record.Support,creationId,pending,RebirthGearTransferPhase.GearCommitted);
            if(!current()||!RebirthWorldCharacterRepository.HasSavedGearTransfer(identity,pending,RebirthGearTransferPhase.GearCommitted)||!current())return false;
            int regionX=chunkX>>5,regionZ=chunkZ>>5;
            if(!RebirthRecoveryRegionCapture.TryCapture(directory,regionX,regionZ,chunkX&31,chunkZ&31,current,out var frame)||!current()||
                !RebirthRecoveryChunkPayload.TryDecodeNative(frame,out uint version,out var payload)||!current()||
                !RebirthRecoveryNativeChunkEvidence.TryMatch(payload,version,chunkX,chunkY,chunkZ,pending,publicationId,ownerKey,backpackClassId,itemClassId,current,out _)||
                !current()||!RebirthWorldCharacterRepository.HasSavedGearTransfer(identity,pending,RebirthGearTransferPhase.GearCommitted)||!current())return false;
            return true;
        }
        catch{return false;}
    }
}