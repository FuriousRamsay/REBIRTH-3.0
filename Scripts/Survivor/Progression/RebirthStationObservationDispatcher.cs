using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Live authority-side observation of SAVED preparations; never creates a paid job or
// mutates inventory. Publication is a prerequisite, not permission to settle rewards.
public static class RebirthStationObservationDispatcher
{
    // Preparation only: caller invokes on the game authority thread. No debit/queue
    // mutation is authorized by this result; mutation requires a held station reservation.
    public static bool TrySavePreparation(EntityPlayer owner, RebirthStationGridAdmission admission)
    {
        if (System.Threading.Thread.CurrentThread.ManagedThreadId != authorityThreadId
            || owner == null || admission == null || !ReferenceEquals(owner.world, authorityWorld)
            || !RebirthWorldCharacterRepository.IsServerAuthority
            || owner.world == null || owner.world.IsRemote() || owner.IsDead()
            || !ReferenceEquals(GameManager.Instance?.World, owner.world)
            || !ReferenceEquals(owner.world.GetEntity(owner.entityId), owner)
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld()
            || !RebirthWorldCharacterService.TryGet(owner, out var record)
            || record == null || !record.IsComplete || record.Origin == null || record.Progression == null
            || !RebirthStationGridAdmission.TryNormalizeCreation(record.Origin.CreationId, out var creation)
            || creation != admission.CreationId
            || !RebirthWorldCharacterRepository.HasExclusiveStationPreparation(record, admission)) return false;
        if(!RebirthWorldCharacterService.TryGetIdentity(owner,out var identity)||identity==null||
            identity.StorageKey!=record.StablePlayerKey||identity.CanonicalId!=record.StablePlayerId||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return false;
        var originalProgression=record.Progression;
        bool discovery=originalProgression.StationDiscoveryAdmissions.TryGetValue(admission.JobId,out var frozen);
        RebirthStationDiscoveryAuthorityScope scope=null;
        if(discovery&&!RebirthStationDiscoveryPreparationPair.TryValidateLive(owner,admission,frozen,out scope))return false;
        Func<bool> sameBinding=()=>ReferenceEquals(record.Progression,originalProgression)&&
            record.Progression.StationDiscoveryAdmissions.TryGetValue(admission.JobId,out var liveBinding)==discovery&&
            (!discovery||liveBinding!=null&&scope.IsCurrent()&&System.Xml.Linq.XNode.DeepEquals(liveBinding.Write(),frozen.Write()));
        if(!RebirthWorldCharacterRepository.TryGetSavedStationPreparationIntent(identity,admission,out var originalIntent)||
            !originalIntent.MatchesCached(record,admission)||!sameBinding())return false;
        World admittedWorld=owner.world;
        Func<bool> current=()=>ReferenceEquals(owner.world,admittedWorld)&&IsCurrentAuthorityThread(admittedWorld)&&!owner.IsDead()&&
            ReferenceEquals(owner.world.GetEntity(owner.entityId),owner)&&record.IsComplete&&record.Progression!=null&&
            record.StablePlayerKey==identity.StorageKey&&record.StablePlayerId==identity.CanonicalId&&
            RebirthWorldCharacterService.TryGetIdentity(owner,out var liveIdentity)&&liveIdentity!=null&&
            liveIdentity.StorageKey==identity.StorageKey&&liveIdentity.CanonicalId==identity.CanonicalId&&
            RebirthWorldCharacterService.TryGet(owner,out var live)&&ReferenceEquals(live,record)&&
            RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&
            RebirthSurvivorRequestScope.Matches(creation,record.Origin?.CreationId)&&
            RebirthWorldCharacterRepository.HasExclusiveStationPreparation(record,admission)&&sameBinding()&&originalIntent.MatchesCached(record,admission);
        return RebirthStationPreparationPersistence.TryRegister(record.Progression.StationPreparations,
            creation, admission, () =>
            {
                if(!current())return false;
                RebirthWorldCharacterService.MarkDirty(record, "station-preparation-register");
                try
                {
                    return RebirthWorldCharacterService.FlushPlayer(owner, "station-preparation-register")&&
                        current()&&RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,admission)&&
                        (!discovery||RebirthWorldCharacterRepository.HasSavedStationDiscoveryAdmission(identity,admission,frozen))&&current();
                }
                catch{return false;}
            });
    }
    private sealed class Entry
    {
        public World World; public EntityPlayer Owner; public RebirthStationGridAdmission Admission;
        public Chunk Chunk; public TileEntityWorkstation Tile; public int X,Y,Z; public string Block; public float Expires;
        public RebirthStationSnapshotEvidence.Request Watch;
    }
    private static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
    private static readonly HashSet<string> verifiedQueuedJobs=new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string,Tuple<TileEntityWorkstation,int>> verifiedQueuedStations=new Dictionary<string,Tuple<TileEntityWorkstation,int>>(StringComparer.Ordinal);
    private static readonly Dictionary<string,RebirthStationGridAdmission> verifiedQueuedAdmissions=new Dictionary<string,RebirthStationGridAdmission>(StringComparer.Ordinal);
    private static readonly Dictionary<string,RebirthWorldCharacterRecord> verifiedQueuedOwners=new Dictionary<string,RebirthWorldCharacterRecord>(StringComparer.Ordinal);
    private static bool installed; private static float next;
    private static int authorityThreadId; private static World authorityWorld;
    internal static bool IsCurrentAuthorityThread(World world)
    {
        return world!=null&&authorityThreadId!=0&&
            System.Threading.Thread.CurrentThread.ManagedThreadId==authorityThreadId&&
            ReferenceEquals(world,authorityWorld)&&ReferenceEquals(world,GameManager.Instance?.World)&&
            !world.IsRemote()&&RebirthWorldCharacterRepository.IsServerAuthority&&
            RebirthSurvivorMode.IsEnabledForCurrentWorld();
    }
    public static void Install()
    {if(installed)return;installed=true;ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Tick));}
    // Marked generic jobs wait for a verified native publication receipt; vanilla jobs pass.
    internal static bool CanProcessNativeQueue(TileEntityWorkstation station)
    {
        if(station?.Queue==null)return true;
        foreach(var entry in station.Queue)
        {
            if(entry?.Recipe==null||!RebirthStationGridQueue.IsMarked(entry.Recipe))continue;
            if(!IsCurrentAuthorityThread(GameManager.Instance?.World)||
                !RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var job)||!verifiedQueuedJobs.Contains(job)||
                !verifiedQueuedStations.TryGetValue(job,out var binding)||!ReferenceEquals(binding.Item1,station)||
                binding.Item2!=entry.StartingEntityId||entry.Multiplier!=1||
                !verifiedQueuedAdmissions.TryGetValue(job,out var admission)||!admission.MatchesQueuedQuality(entry.Recipe,entry.Multiplier,entry.Quality))return false;
            if(!verifiedQueuedOwners.TryGetValue(job,out var owner)||owner?.Progression==null||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                owner.Progression.StationTerminalIntents.ContainsKey(job))return false;
            int occurrences=0;
            foreach(var other in station.Queue)
                if(other?.Recipe!=null&&RebirthStationGridQueue.TryGetJobId(other.Recipe,out var otherJob)&&otherJob==job)
                    if(++occurrences>1)return false;
        }
        return true;
    }
    internal static bool TryGetVerifiedQueuedAdmission(TileEntityWorkstation station,RecipeQueueItem queued,
        out RebirthStationGridAdmission admission)
    {
        admission=null;
        if(station?.Queue==null||queued?.Recipe==null||!CanProcessNativeQueue(station)||
            !RebirthStationGridQueue.TryGetJobId(queued.Recipe,out var job)||
            !verifiedQueuedAdmissions.TryGetValue(job,out var stored))return false;
        bool found=false;foreach(var entry in station.Queue)if(ReferenceEquals(entry,queued)){found=true;break;}
        if(!found)return false;admission=stored.Clone();return true;
    }
    // Register before the publisher's final notification, avoiding the periodic scan delay.
    internal static bool TryWatchQueued(EntityPlayer player,RebirthStationGridAdmission admission)
    {
        if(player==null||admission==null||!IsCurrentAuthorityThread(player.world)||
            entries.ContainsKey(admission.JobId)||entries.Count>=RebirthStationSnapshotEvidence.MaximumRequests||
            !RebirthWorldCharacterService.TryGet(player,out var record)||record?.Progression==null||
            !RebirthWorldCharacterRepository.HasExclusiveStationPreparation(record,admission)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||
            !RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,admission))return false;
        var image=admission.Write();var world=player.world;
        var e=new Entry{World=world,Owner=player,Admission=admission.Clone(),
            X=(int)image.Attribute("x"),Y=(int)image.Attribute("y"),Z=(int)image.Attribute("z"),
            Block=(string)image.Attribute("block"),Expires=Time.realtimeSinceStartup+60f};
        e.Chunk=world.GetChunkSync(e.X>>4,e.Z>>4) as Chunk;
        if(e.Chunk==null||!Observe(e))return false;
        var tile=world.GetTileEntity(new Vector3i(e.X,e.Y,e.Z)) as TileEntityWorkstation;
        e.Tile=tile;
        e.Watch=RebirthStationSnapshotEvidence.Watch(world,e.Chunk,e.X>>4,e.Z>>4,()=>Observe(e),tile,
            (inputs,queue)=>RebirthStationSerializedContents.Matches(inputs,queue,e.Admission,e.Admission.CreationId,
                e.X,e.Y,e.Z,e.Block,e.Owner.entityId));
        if(e.Watch==null)return false;entries.Add(admission.JobId,e);return true;
    }
    private static bool Observe(Entry e)
    {
        if(e==null||!ReferenceEquals(GameManager.Instance?.World,e.World)||e.World.IsRemote()||
            !ReferenceEquals(e.Owner.world,e.World)||!ReferenceEquals(e.World.GetEntity(e.Owner.entityId),e.Owner)||
            !ReferenceEquals(e.World.GetChunkSync(e.X>>4,e.Z>>4),e.Chunk)||
            !RebirthWorldCharacterService.TryGet(e.Owner,out var record)||record==null||!record.IsComplete||
            record.Origin==null||record.Progression==null||!RebirthStationGridAdmission.TryNormalizeCreation(record.Origin.CreationId,out var creation)||
            creation!=e.Admission.CreationId||!record.Progression.StationPreparations.TryGetValue(e.Admission.JobId,out var saved)||
            !System.Xml.Linq.XNode.DeepEquals(saved.Write(),e.Admission.Write()))return false;
        // Cross-owner preparations at one station cannot independently claim its inputs.
        if(e.World.Players?.list==null)return false;
        foreach(var otherPlayer in e.World.Players.list)
            if(RebirthWorldCharacterService.TryGet(otherPlayer,out var otherRecord)&&otherRecord?.Progression!=null)
                foreach(var other in otherRecord.Progression.StationPreparations.Values)
                    if(other!=null&&other.SharesStation(e.Admission)&&
                        (other.JobId!=e.Admission.JobId||other.CreationId!=e.Admission.CreationId))return false;
        var pos=new Vector3i(e.X,e.Y,e.Z);var tile=e.World.GetTileEntity(pos) as TileEntityWorkstation;
        if(tile==null||tile.Queue==null||e.World.GetBlock(pos).Block?.GetBlockName()!=e.Block)return false;
        RecipeQueueItem match=null;int occurrences=0;
        foreach(var q in tile.Queue)if(q?.Recipe!=null&&RebirthStationGridQueue.TryGetJobId(q.Recipe,out var id)&&id==e.Admission.JobId){match=q;occurrences++;}
        if(occurrences!=1||match.StartingEntityId!=e.Owner.entityId||tile.MaterialNames==null||
            !e.Admission.TryGetPhysicalSlotCount(out var admittedSlots)||admittedSlots!=tile.InputSlotCount||
            !RebirthStationInputLayout.TryReadPhysical(tile.Input,tile.InputSlotCount,tile.MaterialNames.Length,out var physical))return false;
        return e.Admission.MatchesQueuedObservation(creation,e.X,e.Y,e.Z,e.Block,physical,match.Recipe,match.Multiplier);
    }
    public static bool HasPublishedObservation(EntityPlayer owner,string job)
    {return job!=null&&entries.TryGetValue(job,out var e)&&ReferenceEquals(e.Owner,owner)&&Observe(e)&&RebirthStationSnapshotEvidence.HasPublished(e.Watch);}
    private static bool HasLivePublishedQueue(EntityPlayer owner,RebirthStationGridAdmission admission)
    {
        if(owner==null||admission==null||!IsCurrentAuthorityThread(owner.world))return false;
        var image=admission.Write();
        var entry=new Entry{World=owner.world,Owner=owner,Admission=admission,
            X=(int)image.Attribute("x"),Y=(int)image.Attribute("y"),Z=(int)image.Attribute("z"),
            Block=(string)image.Attribute("block")};
        entry.Chunk=owner.world.GetChunkSync(entry.X>>4,entry.Z>>4) as Chunk;
        return entry.Chunk!=null&&Observe(entry);
    }
    // Recovery classification only. Even an exact published queue is not an output receipt.
    public static bool TryRevalidatePublishedPreparation(EntityPlayer owner,string job)
    {
        if(owner==null||job==null||!RebirthWorldCharacterRepository.IsServerAuthority||
            !RebirthWorldCharacterService.TryGet(owner,out var record)||record==null||!record.IsComplete||record.Progression==null||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||
            !record.Progression.StationPreparations.TryGetValue(job,out var admission)||
            !record.Progression.StationPublications.TryGetValue(job,out var publication))return false;
        return IsCurrentAuthorityThread(owner.world)&&RebirthWorldCharacterService.TryGetIdentity(owner,out var identity)&&identity!=null&&
            identity.StorageKey==record.StablePlayerKey&&identity.CanonicalId==record.StablePlayerId&&
            HasLivePublishedQueue(owner,admission)&&
            RebirthWorldCharacterRepository.HasExclusiveStationPreparation(record,admission)&&
            RebirthWorldCharacterRepository.HasSavedStationPublication(identity,admission,publication)&&
            publication.Revalidate(GameIO.GetSaveGameDir(),admission);
    }
    private static bool PersistPublication(Entry e)
    {
        if(!RebirthStationSnapshotEvidence.TryGetPublished(e.Watch,out var proof)||
            !ReferenceEquals(GameManager.Instance?.World,e.World)||!RebirthWorldCharacterService.TryGet(e.Owner,out var record)||
            record?.Progression==null||!record.Progression.StationPreparations.TryGetValue(e.Admission.JobId,out var admission)||
            !System.Xml.Linq.XNode.DeepEquals(admission.Write(),e.Admission.Write()))return false;
        if(!record.Progression.StationPublications.ContainsKey(admission.JobId)){
            if(!RebirthStationPublicationRecord.TryCreate(GameIO.GetSaveGameDir(),admission,e.Owner.entityId,proof,out var publication))return false;
            record.Progression.StationPublications.Add(admission.JobId,publication);
            RebirthWorldCharacterService.MarkDirty(record,"station-native-queued-published");
        }
        // Failed save keeps the exact pending phase for retry; no queue/item replay occurs.
        if(record.Dirty&&!RebirthWorldCharacterService.FlushPlayer(e.Owner,"station-native-queued-published"))return false;
        if(RebirthWorldCharacterService.TryGetIdentity(e.Owner,out var identity)&&
            record.Progression.StationPublications.TryGetValue(admission.JobId,out var receipt)&&
            RebirthWorldCharacterRepository.HasSavedStationPublication(identity,admission,receipt))
        {if(e.Tile==null)return false;verifiedQueuedStations[admission.JobId]=Tuple.Create(e.Tile,e.Owner.entityId);verifiedQueuedAdmissions[admission.JobId]=admission.Clone();verifiedQueuedOwners[admission.JobId]=record;verifiedQueuedJobs.Add(admission.JobId);return true;}
        RebirthWorldCharacterService.MarkDirty(record,"station-publication-readback-pending");
        return false;
    }    private static void Clear(){authorityThreadId=0;authorityWorld=null;verifiedQueuedJobs.Clear();verifiedQueuedStations.Clear();verifiedQueuedAdmissions.Clear();verifiedQueuedOwners.Clear();foreach(var e in entries.Values)e.Watch.Dispose();entries.Clear();}
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;
        if(world==null||world.IsRemote()||!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld()) {Clear();return;}
        if(authorityWorld!=null&&!ReferenceEquals(authorityWorld,world))Clear();
        authorityThreadId=System.Threading.Thread.CurrentThread.ManagedThreadId;authorityWorld=world;
        float now=Time.realtimeSinceStartup;if(now<next)return;next=now+1f;
        foreach(var key in entries.Keys.ToArray()){
            var entry=entries[key];bool saved=PersistPublication(entry);
            if(saved||entry.Expires<now||(!RebirthStationSnapshotEvidence.HasPublished(entry.Watch)&&!Observe(entry))){entry.Watch.Dispose();entries.Remove(key);}
        }
        if(world.Players?.list==null)return;
        foreach(var player in world.Players.list)
        {
            if(entries.Count>=RebirthStationSnapshotEvidence.MaximumRequests)break;
            if(!RebirthWorldCharacterService.TryGet(player,out var record)||record==null||!record.IsComplete||record.Progression==null)continue;
            foreach(var admission in record.Progression.StationPreparations.Values)
            {
                if(entries.Count>=RebirthStationSnapshotEvidence.MaximumRequests)break;
                if(admission==null||entries.ContainsKey(admission.JobId))continue;
                if(verifiedQueuedOwners.TryGetValue(admission.JobId,out var previousOwner)&&!ReferenceEquals(previousOwner,record))
                {verifiedQueuedJobs.Remove(admission.JobId);verifiedQueuedStations.Remove(admission.JobId);verifiedQueuedAdmissions.Remove(admission.JobId);verifiedQueuedOwners.Remove(admission.JobId);}
                if(record.Progression.StationPublications.ContainsKey(admission.JobId))
                {if(!verifiedQueuedJobs.Contains(admission.JobId)&&TryRevalidatePublishedPreparation(player,admission.JobId))
                    {var savedImage=admission.Write();var savedTile=world.GetTileEntity(new Vector3i((int)savedImage.Attribute("x"),(int)savedImage.Attribute("y"),(int)savedImage.Attribute("z"))) as TileEntityWorkstation;
                     if(savedTile!=null){verifiedQueuedStations[admission.JobId]=Tuple.Create(savedTile,player.entityId);verifiedQueuedAdmissions[admission.JobId]=admission.Clone();verifiedQueuedOwners[admission.JobId]=record;verifiedQueuedJobs.Add(admission.JobId);}}continue;} // Recovery validates existing publication; never re-enqueue.
                var image=admission.Write();var e=new Entry{World=world,Owner=player,Admission=admission.Clone(),
                    X=(int)image.Attribute("x"),Y=(int)image.Attribute("y"),Z=(int)image.Attribute("z"),Block=(string)image.Attribute("block"),Expires=now+60f};
                e.Chunk=world.GetChunkSync(e.X>>4,e.Z>>4) as Chunk;if(e.Chunk==null||!Observe(e))continue;
                if(!RebirthWorldCharacterRepository.HasExclusiveStationPreparation(record,admission))continue;
                RebirthWorldCharacterService.MarkDirty(record,"station-observation-preparation" );
                if(!RebirthWorldCharacterService.FlushPlayer(player,"station-observation-preparation"))continue;
                var tile=world.GetTileEntity(new Vector3i(e.X,e.Y,e.Z)) as TileEntityWorkstation;
                e.Tile=tile;
        e.Watch=RebirthStationSnapshotEvidence.Watch(world,e.Chunk,e.X>>4,e.Z>>4,()=>Observe(e),tile,
                    (inputs,queue)=>RebirthStationSerializedContents.Matches(inputs,queue,e.Admission,e.Admission.CreationId,e.X,e.Y,e.Z,e.Block,e.Owner.entityId));
                if(e.Watch!=null)entries.Add(admission.JobId,e);
            }
        }
    }
}