using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// Targeted native snapshot scheduling only. Queueing or draining is never a receipt.
internal static class RebirthGearRecoverySnapshotRequest
{
    private static World rateWorld;
    private static object rateManager;
    private static readonly Dictionary<long,double> nextByChunk=new Dictionary<long,double>();
    internal static bool TryQueueOriginal(EntityPlayer player,ClientInfo sender,string creation,string transaction,Guid publication)
    {
        if(!ThreadManager.IsMainThread())return false;
        try
        {
            var game=GameManager.Instance;var world=player?.world;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(game==null||world==null||!ReferenceEquals(game.World,world)||world.IsRemote()||manager==null||!manager.IsServer||
                !RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var record)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||identity.StorageKey!=record.StablePlayerKey)return false;
            var pending=record.Support.PendingGearTransfer;
            if(pending==null||pending.TransactionId!=transaction||record.Support.GearTransferPhase!=RebirthGearTransferPhase.GearCommitted||
                pending.HasRecoveryPublicationReceipt(publication)||!pending.TryGetRecoveryAttempt(publication,out var attempt)||
                !pending.TryGetPlan(out var plan)||!pending.TryGetRecoveryManifest(out var manifest))return false;
            var entity=world.GetEntity(attempt.EntityId);
            var marker=(entity as EntityRebirthGearRecoveryBackpack)?.RecoveryIdentity??(entity as EntityRebirthGearRecoveryItem)?.RecoveryIdentity;
            var group=manifest.ToXml().Elements().SingleOrDefault(e=>(string)e.Attribute("id")==publication.ToString("N"));
            if(entity==null||!entity.addedToChunk||marker==null||group==null||marker.OwnerKey!=identity.StorageKey||marker.CreationId!=pending.CreationId||
                marker.TransactionId.ToString("N")!=transaction||marker.PublicationId!=publication)return false;
            var indices=group.Elements().Select(e=>int.Parse((string)e.Attribute("index"),System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if(indices.Length==0||marker.EntryIndex!=indices[0])return false;
            bool backpack=(string)group.Attribute("kind")=="backpack";
            var items=backpack?entity.bag?.ItemGrid?.items:new[]{(entity as EntityItem)?.itemStack};
            if(items==null||items.Length<indices.Length)return false;
            for(int i=0;i<indices.Length;i++)if(items[i]==null||items[i].count!=plan.Recovery[indices[i]].Item.Count||RebirthNativeItemCodec.Encode(items[i].itemValue)!=plan.Recovery[indices[i]].Item.ItemData)return false;
            for(int i=indices.Length;i<items.Length;i++)if(items[i]!=null&&!items[i].IsEmpty())return false;
            var location=entity.chunkPosAddedEntityTo;var cluster=world.ChunkCache;
            var provider=cluster?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
            if(regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrWhiteSpace(regions.saveDirectory))return false;
            string directory=Path.GetFullPath(regions.saveDirectory);var access=regions.regionFileAccess;
            var chunk=cluster.GetChunkSync(location.x,location.z);
            if(chunk==null)return false;long key=chunk.Key;
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(game.World,world)&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&ReferenceEquals(player.world,world)&&
                ReferenceEquals(cluster,world.ChunkCache)&&ReferenceEquals(provider,cluster.ChunkProvider)&&ReferenceEquals(regions,provider.m_RegionFileManager)&&
                ReferenceEquals(access,regions.regionFileAccess)&&Path.GetFullPath(regions.saveDirectory)==directory&&
                ReferenceEquals(world.GetEntity(attempt.EntityId),entity)&&entity.addedToChunk&&entity.chunkPosAddedEntityTo.x==location.x&&entity.chunkPosAddedEntityTo.z==location.z&&
                ReferenceEquals(cluster.GetChunkSync(location.x,location.z),chunk)&&chunk.Key==key&&
                RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var live)&&ReferenceEquals(live,record)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&RebirthWorldCharacterService.TryGetIdentity(player,out var liveIdentity)&&liveIdentity!=null&&liveIdentity.StorageKey==identity.StorageKey&&
                ReferenceEquals(record.Support.PendingGearTransfer,pending)&&RebirthGearTransferSavedWitness.Matches(record.Support,creation,pending,RebirthGearTransferPhase.GearCommitted);
            if(!current()||!RebirthWorldCharacterRepository.HasSavedGearTransfer(identity,pending,RebirthGearTransferPhase.GearCommitted)||!current())return false;
            double now=(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
            if(!ReferenceEquals(rateWorld,world)||!ReferenceEquals(rateManager,manager)){nextByChunk.Clear();rateWorld=world;rateManager=manager;}
            if(nextByChunk.TryGetValue(key,out var next)&&now<next)return false;
            if(!nextByChunk.ContainsKey(key)&&nextByChunk.Count>=128)
            {
                var expired=new List<long>();foreach(var pair in nextByChunk)if(pair.Value<=now)expired.Add(pair.Key);
                foreach(var old in expired)nextByChunk.Remove(old);
                if(nextByChunk.Count>=128)return false;
            }
            lock(chunk)
            {
                if(!current()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)return false;
                chunk.InProgressSaving=true;
            }
            nextByChunk[key]=now+5; // Failed/uncertain queue effects are throttled too.
            try
            {
                if(!current())return false;
                regions.SaveChunkSnapshot(chunk,true);
                return current();
            }
            finally{lock(chunk){chunk.InProgressSaving=false;}}
        }
        catch{return false;} // Original attempt and pending receipt remain untouched.
    }
}