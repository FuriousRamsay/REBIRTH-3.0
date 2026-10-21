using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;

// Boss-domain original identities and positive native chunk publication only.
// Missing chunks/entities/captures never authorize another spawn or a reward.
internal static class RebirthBossNativePublication
{
    private sealed class Bound {internal World World;internal string Directory;internal RebirthBossEventInstance Event;internal RebirthBossSpawnOriginal Original;internal Entity Entity;internal RebirthStationSnapshotEvidence.Request Watch;internal Chunk Chunk;internal float Started;internal bool Death;}
    private static readonly Dictionary<Entity,Bound> bound=new Dictionary<Entity,Bound>();
    private sealed class Removing {internal World World;internal RebirthBossEventInstance Event;internal RebirthBossSpawnOriginal Original;internal Chunk Chunk;internal RebirthStationSnapshotEvidence.Request Watch;internal float Started;}
    private static readonly Dictionary<Guid,Removing> removing=new Dictionary<Guid,Removing>();
    private static World scope;private static string directory;private static float nextUpdate;private static int reservedFloor;private static bool idsIndexed;
    internal static void Install(Harmony harmony)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthStationSnapshotUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthStationChunkSerializedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthStationSnapshotResetPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthStationSnapshotWritePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthStationRegionPublishedPatch));
    }
    private static void Scope(World world)
    {if(ReferenceEquals(scope,world)&&directory==GameIO.GetSaveGameDir())return;foreach(var item in bound.Values)item.Watch?.Dispose();bound.Clear();foreach(var item in removing.Values)item.Watch?.Dispose();removing.Clear();nextUpdate=0f;reservedFloor=0;idsIndexed=false;scope=world;directory=GameIO.GetSaveGameDir();}
    internal static string Key(RebirthBossSpawnOriginal original)=>"rebirth.boss.original."+original.Operation.ToString("N");
    internal static byte[] Stamp(World world,RebirthBossEventInstance e,RebirthBossSpawnOriginal original)
    {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write((byte)1);writer.Write(world.worldState.Guid);writer.Write(e.EventId.ToByteArray());writer.Write(e.Progression.OwnerStableId);writer.Write(original.Operation.ToByteArray());writer.Write(original.EntityId);writer.Write(original.EntityClass);writer.Write(original.Role);writer.Write(original.WorldTimeBorn);writer.Flush();return stream.ToArray();}}
    private static bool AliveIdentity(EntityAlive entity,RebirthBossSpawnOriginal original,bool write)
    {
        if(entity?.Buffs==null)return false;byte[] bytes=original.Operation.ToByteArray();
        for(int i=0;i<8;i++){string key="rebirthBossOriginal"+i;float expected=1+bytes[i*2]+256*bytes[i*2+1];if(write)entity.Buffs.SetCustomVar(key,expected);else if(entity.Buffs.GetCustomVar(key)!=expected)return false;}return true;
    }
    internal static bool ValidProof(string proof)=>proof!=null&&(proof.Length==0||proof.Length==64&&proof.All(value=>value>='0'&&value<='9'||value>='a'&&value<='f'));
    internal static void DestroyDetached(World world,Entity entity)
    {if(entity!=null&&!entity.addedToChunk&&world.GetEntity(entity.entityId)==null&&entity.RootTransform!=null)UnityEngine.Object.Destroy(entity.RootTransform.gameObject);}
    internal static void Prepare(RebirthBossSpawnOriginal original,Entity entity)
    {
        if(original.Phase!=0||entity.entityId!=original.EntityId||entity.entityClass!=original.EntityClass)throw new InvalidDataException();
        if(original.EntityId==int.MaxValue)throw new InvalidDataException("Native boss ID exhausted");reservedFloor=Math.Max(reservedFloor,original.EntityId+1);
        original.WorldTimeBorn=entity.WorldTimeBorn;
        if(entity is EntityAlive alive&&!AliveIdentity(alive,original,true))throw new InvalidDataException("Native boss GUID unsupported");
        var native=new EntityCreationData(entity);if(native.entityData==null||native.entityData.Length>ushort.MaxValue)throw new InvalidDataException("Native boss body exceeds codec");
        using(var blob=StreamUtils.ToBlob(writer=>native.write(writer,StreamModeWrite.Persistency)))original.PreparedNative=blob.ToArray();
        if(!ValidPrepared(original))throw new InvalidDataException("Native boss original unsupported");
    }
    internal static bool ValidPrepared(RebirthBossSpawnOriginal original)
    {
        try{if(original?.PreparedNative==null||original.PreparedNative.Length==0||original.PreparedNative.Length>262144)return false;
        using(var stream=new MemoryStream(original.PreparedNative,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {reader.SetBaseStream(stream);var native=new EntityCreationData();native.read(reader,StreamModeRead.Persistency);return stream.Position==stream.Length&&native.id==original.EntityId&&native.entityClass==original.EntityClass&&native.pos.Equals(original.Position)&&NativeBirth(native,out ulong birth)&&birth==original.WorldTimeBorn&&native.entityData!=null&&native.entityData.Length<=ushort.MaxValue;}}
        catch{return false;}
    }
    internal static bool CanResumePreparedOriginal(RebirthBossEventInstance e,RebirthBossSpawnOriginal original)
    {
        if(e==null||original==null||original.Abandoned||original.Phase!=0||original.NativeProof.Length!=0||original.Killed||original.RemovalInvoking||original.NativeRemoved)return false;
        if(original.Role==4){var source=e.SpawnOriginals.Find(value=>value.Operation==original.ParentOperation&&value.Role==1);return source!=null&&source.Killed&&source.DeathPublished&&source.DeathProof.Length==64;}
        return original.Role==3?e.State==RebirthBossEventState.RewardPending&&!e.RewardIssued:original.Role>=0&&original.Role<=2&&e.NativeBatchPrepared&&(e.State==RebirthBossEventState.Spawning||e.State==RebirthBossEventState.Active);
    }
    private static void ResumePrepared(World world,RebirthBossEventInstance e,RebirthBossSpawnOriginal original)
    {
        if(!CanResumePreparedOriginal(e,original)||!ValidPrepared(original)||world.GetEntity(original.EntityId)!=null)return;
        if(original.Role==4){var parent=e.SpawnOriginals.Find(value=>value.Operation==original.ParentOperation&&value.Role==1);if(parent==null||EntityClass.FromString(parent.SupportLootClass)!=original.EntityClass)return;}
        var chunk=world.GetChunkFromWorldPos((int)Math.Floor(original.Position.x),(int)Math.Floor(original.Position.z)) as Chunk;
        if(chunk==null||chunk.InProgressUnloading||chunk.entityStubs.Exists(value=>value.id==original.EntityId))return;
        var owner=RebirthBossEventIdentity.FindPlayer(world,e.Progression.OwnerStableId);if(owner==null&&original.Role<=2)return;
        Entity entity=null;
        try
        {
        using(var stream=new MemoryStream(original.PreparedNative,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {reader.SetBaseStream(stream);var native=new EntityCreationData();native.read(reader,StreamModeRead.Persistency);entity=EntityFactory.CreateEntity(native);}
        if(entity==null||entity.entityId!=original.EntityId||entity.entityClass!=original.EntityClass)return;
        // Phase zero is positively never-invoked. Reuse its full frozen ECD;
        // phase one, including missing native output, NEVER enters this lane.
        original.Phase=1;if(!RebirthBossEventPersistence.Checkpoint())return;
        Bind(world,e,original,entity);if(original.Role==3)RebirthBossEventRewardService.BindOwner(world,e,entity);
        world.SpawnEntityInWorld(entity);if(!ReferenceEquals(world.GetEntity(original.EntityId),entity))return;
        if(entity is EntityAlive alive)RebirthBossEventSpawner.FinishOriginal(world,e,original,alive,owner,true);
        if(original.Role==4)entity.transform.localScale=new UnityEngine.Vector3(1.25f,1.25f,1.25f);
        original.Phase=2;RebirthBossEventPersistence.Checkpoint();
        }
        finally{DestroyDetached(world,entity);}
    }
    internal static void Bind(World world,RebirthBossEventInstance e,RebirthBossSpawnOriginal original,Entity entity)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||original.Phase!=1||entity.entityId!=original.EntityId||entity.entityClass!=original.EntityClass)throw new InvalidOperationException("Boss original scope changed");
        Scope(world);if(entity is EntityAlive alive&&!AliveIdentity(alive,original,true))throw new InvalidOperationException("Boss original native identity unavailable");
        var item=new Bound{World=world,Directory=directory,Event=e,Original=original,Entity=entity};bound[entity]=item;
        // Stamp before the original native spawn; it cannot prove an entity exists.
        var chunk=world.GetChunkFromWorldPos((int)Math.Floor(original.Position.x),(int)Math.Floor(original.Position.z)) as Chunk;
        if(chunk==null||chunk.InProgressUnloading||!EnsureStamp(item,chunk))throw new InvalidOperationException("Boss original chunk unavailable");
    }
    private static bool EnsureStamp(Bound item,Chunk chunk)
    {
        string key=Key(item.Original);byte[] expected=Stamp(item.World,item.Event,item.Original);
        if(chunk.ChunkCustomData.dict.TryGetValue(key,out var existing))return existing.data!=null&&existing.data.SequenceEqual(expected)&&existing.TriggerWriteDataDelegate==null&&existing.expiresInWorldTime==ulong.MaxValue&&!existing.isSavedToNetwork;
        chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=expected});chunk.isModified=true;return true;
    }
    private static bool Current(Bound item)
    {
        if(!ThreadManager.IsMainThread()||!RebirthBossEventPersistence.IsReady||!ReferenceEquals(scope,item.World)||item.Directory!=GameIO.GetSaveGameDir()||!ReferenceEquals(GameManager.Instance?.World,item.World)||!ReferenceEquals(item.World.GetEntity(item.Original.EntityId),item.Entity)||item.Entity.entityClass!=item.Original.EntityClass||(item.Entity is EntityContainerAbs container&&container.bRemoved)||item.Entity.IsMarkedForUnload()||!item.Entity.addedToChunk)return false;
        if(item.Entity is EntityAlive alive&&!AliveIdentity(alive,item.Original,false))return false;
        if(item.Death&&(!item.Original.Killed||!(item.Entity is EntityAlive corpse)||corpse.Health>0))return false;
        return item.Chunk==null||ReferenceEquals(item.World.GetChunkSync(item.Chunk.X,item.Chunk.Z),item.Chunk)&&!item.Chunk.InProgressUnloading&&item.Entity.chunkPosAddedEntityTo.x==item.Chunk.X&&item.Entity.chunkPosAddedEntityTo.z==item.Chunk.Z;
    }
    internal static bool Matches(World world,RebirthBossEventInstance e,RebirthBossSpawnOriginal original,Entity entity)
    {
        if(world==null||entity==null||!ReferenceEquals(entity.world,world)||entity.entityId!=original.EntityId||entity.entityClass!=original.EntityClass||entity.WorldTimeBorn!=original.WorldTimeBorn)return false;
        if(entity is EntityAlive alive&&AliveIdentity(alive,original,false))return true;
        if(entity is EntityAlive unknown)for(int i=0;i<8;i++)if(unknown.Buffs==null||unknown.Buffs.GetCustomVar("rebirthBossOriginal"+i)!=0f)return false;
        var chunk=entity.addedToChunk?world.GetChunkSync(entity.chunkPosAddedEntityTo.x,entity.chunkPosAddedEntityTo.z) as Chunk:world.GetChunkFromWorldPos((int)Math.Floor(entity.position.x),(int)Math.Floor(entity.position.z)) as Chunk;
        return chunk!=null&&chunk.ChunkCustomData.dict.TryGetValue(Key(original),out var marker)&&marker.data!=null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.TriggerWriteDataDelegate==null&&marker.data.SequenceEqual(Stamp(world,e,original));
    }
    internal static bool RebindRuntime(World world,RebirthBossEventInstance e,RebirthBossSpawnOriginal original,Entity entity)
    {if(!Matches(world,e,original,entity))return false;return !(entity is EntityAlive alive)||AliveIdentity(alive,original,true);}
    internal static void ReserveOriginalIds()
    {if(!idsIndexed){foreach(var e in RebirthBossEventRegistry.SnapshotEvents())foreach(var original in e.SpawnOriginals)if(original.EntityId<int.MaxValue)reservedFloor=Math.Max(reservedFloor,original.EntityId+1);idsIndexed=true;}if(reservedFloor>EntityFactory.nextEntityID)EntityFactory.nextEntityID=reservedFloor;}
    internal static void ObserveWorld(World world)
    {try{Scope(world);if(world?.IsRemote()==false&&RebirthBossEventPersistence.IsReady)ReserveOriginalIds();}catch{foreach(var item in bound.Values)item.Watch?.Dispose();foreach(var item in removing.Values)item.Watch?.Dispose();bound.Clear();removing.Clear();scope=null;directory=null;nextUpdate=0f;}}
    internal static void Pump(World world)
    {
        if(world?.IsRemote()!=false||!RebirthBossEventPersistence.IsReady)return;Scope(world);if(UnityEngine.Time.realtimeSinceStartup<nextUpdate)return;nextUpdate=UnityEngine.Time.realtimeSinceStartup+1f;
        AdoptLegacy(world);AdoptLegacyRewards(world);StageOwedSupportLoot(world);PumpRemovals(world);
        foreach(var e in RebirthBossEventRegistry.SnapshotEvents())foreach(var original in e.SpawnOriginals)
        {if(original.EntityId>=EntityFactory.nextEntityID&&original.EntityId<int.MaxValue)EntityFactory.nextEntityID=original.EntityId+1;}
        foreach(var e in RebirthBossEventRegistry.SnapshotEvents())foreach(var original in e.SpawnOriginals)
        {
            if(original.Abandoned)continue;if(original.Phase==0){try{ResumePrepared(world,e,original);}catch{/* Original attempted phase is retained. */}continue;}if(original.Phase==3){var existing=world.GetEntity(original.EntityId);if(existing is EntityAlive loaded&&Matches(world,e,original,existing)){AliveIdentity(loaded,original,true);if((original.Role==2||original.Role==1&&original.SupportLootClass.Length!=0)&&original.Killed&&!original.DeathPublished&&loaded.Health<=0&&!bound.ContainsKey(existing))bound[existing]=new Bound{World=world,Directory=directory,Event=e,Original=original,Entity=existing,Death=true};}continue;}var entity=world.GetEntity(original.EntityId);if(!Matches(world,e,original,entity))continue;
            if(entity is EntityAlive restored)AliveIdentity(restored,original,true);
            if(original.Role<=2&&!original.Killed&&e.State!=RebirthBossEventState.Cancelled&&e.State!=RebirthBossEventState.Failed&&entity is EntityAlive alive)RebirthBossEventSpawner.FinishOriginal(world,e,original,alive,RebirthBossEventIdentity.FindPlayer(world,e.Progression.OwnerStableId),false);
            if(!bound.ContainsKey(entity))bound[entity]=new Bound{World=world,Directory=directory,Event=e,Original=original,Entity=entity};
        }
        foreach(var e in RebirthBossEventRegistry.SnapshotEvents())if(e.State==RebirthBossEventState.Spawning&&e.NativeBatchPrepared&&e.SpawnOriginals.Count!=0&&!e.SpawnOriginals.Exists(value=>value.Role<=2&&value.Phase<2)){e.State=RebirthBossEventState.Active;RebirthBossEventPersistence.Checkpoint();}
        foreach(var item in bound.Values.ToArray())try
        {
            if(item.Original.Phase==3&&!item.Death||!Current(item)){item.Watch?.Dispose();bound.Remove(item.Entity);continue;}
            if(item.Watch!=null)
            {
                if(RebirthStationSnapshotEvidence.HasPublished(item.Watch)&&RebirthStationSnapshotEvidence.TryCopy(item.Watch,out var bytes)&&TryPublishedEntity(bytes,item.Chunk.X,item.Chunk.Z,item.Original,Key(item.Original),Stamp(world,item.Event,item.Original),out string proof,false,item.Death))
                {
                    if(item.Death){item.Original.DeathPublished=true;item.Original.DeathProof=proof;item.Original.DeathProofKind=1;}else{item.Original.NativeProof=proof;item.Original.Phase=3;}
                    if(!RebirthBossEventPersistence.Checkpoint()){if(item.Death){item.Original.DeathPublished=false;item.Original.DeathProof=string.Empty;item.Original.DeathProofKind=0;}else{item.Original.Phase=2;item.Original.NativeProof=string.Empty;}continue;}
                    item.Watch.Dispose();bound.Remove(item.Entity);continue;
                }
                if(UnityEngine.Time.realtimeSinceStartup-item.Started>15f){item.Watch.Dispose();item.Watch=null;item.Chunk=null;}continue;
            }
            var chunk=world.GetChunkSync(item.Entity.chunkPosAddedEntityTo.x,item.Entity.chunkPosAddedEntityTo.z) as Chunk;var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
            if(chunk==null||chunk.InProgressUnloading||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||!EnsureStamp(item,chunk))continue;
            var access=regions.regionFileAccess;string saveDirectory=Path.GetFullPath(regions.saveDirectory);item.Chunk=chunk;
            Func<bool> current=()=>Current(item)&&ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==saveDirectory;
            item.Watch=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,current,item.Entity);if(item.Watch==null){item.Chunk=null;continue;}item.Started=UnityEngine.Time.realtimeSinceStartup;
            lock(chunk){if(!current()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting){item.Watch.Dispose();item.Watch=null;item.Chunk=null;continue;}chunk.InProgressSaving=true;}
            try{if(current())regions.SaveChunkSnapshot(chunk,true);}finally{lock(chunk)chunk.InProgressSaving=false;}
        }
        catch{item.Watch?.Dispose();item.Watch=null;item.Chunk=null;/* Preserve original invocation and source custody. */}
    }
    internal static bool BeginRemoval(World world,RebirthBossEventInstance e,RebirthBossSpawnOriginal original,Entity entity)
    {
        if(original.Phase!=3||original.NativeRemoved||!Matches(world,e,original,entity)||!entity.addedToChunk)return false;
        if(original.RemovalInvoking)return RebirthBossEventPersistence.Checkpoint();
        original.RemovalInvoking=true;original.RemovalX=entity.chunkPosAddedEntityTo.x;original.RemovalZ=entity.chunkPosAddedEntityTo.z;
        return RebirthBossEventPersistence.Checkpoint();
    }
    internal static void ObserveRemoved(World world,Entity entity,EnumRemoveEntityReason reason)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||entity==null||reason==EnumRemoveEntityReason.Unloaded||!RebirthBossEventPersistence.IsReady)return;
            foreach(var e in RebirthBossEventRegistry.SnapshotEvents())
            {var original=e.SpawnOriginals.Find(value=>value.EntityId==entity.entityId);if(original!=null&&Matches(world,e,original,entity)){CaptureDeadRemoval(world,e,original,entity);BeginRemoval(world,e,original,entity);return;}}
        }catch{/* Missing removal proof retains the original native obligation. */}
    }
    private static void PumpRemovals(World world)
    {
        foreach(var e in RebirthBossEventRegistry.SnapshotEvents())foreach(var original in e.SpawnOriginals)
        {
            if(original.NativeRemoved)
            {
                if(!original.DeathPublished){string death=RemovedDeathProof(original);if(death!=null){original.DeathProof=death;original.DeathProofKind=2;original.DeathPublished=true;if(!RebirthBossEventPersistence.Checkpoint()){original.DeathProof=string.Empty;original.DeathProofKind=0;original.DeathPublished=false;continue;}}}
                // A crash after native absence publication may precede the domain
                // membership update. Reconcile that update without another effect.
                if(original.Role==3?e.RewardEntityId>=0||e.RewardBlockPlaced:RebirthBossEventRegistry.TryGetEntity(original.EntityId,out _)||e.SupportEntityIds.Contains(original.EntityId)||e.RegularEntityIds.Contains(original.EntityId))RebirthBossEventRewardService.CompleteOriginalRemoval(world,e,original);
                if(removing.TryGetValue(original.Operation,out var retired)){retired.Watch?.Dispose();removing.Remove(original.Operation);}
                continue;
            }
            if(!original.RemovalInvoking||original.NativeRemoved||original.Phase!=3)continue;
            var live=world.GetEntity(original.EntityId);
            if(live!=null){if(original.DeadRemovalFrame!=null&&(!(live is EntityAlive corpse)||corpse.Health>0))continue;if(Matches(world,e,original,live)&&RebirthBossEventPersistence.Checkpoint())world.RemoveEntity(live.entityId,EnumRemoveEntityReason.Despawned);continue;}
            if(!removing.ContainsKey(original.Operation))removing[original.Operation]=new Removing{World=world,Event=e,Original=original};
        }
        foreach(var item in removing.Values.ToArray())try
        {
            var original=item.Original;
            Func<bool> current=()=>ReferenceEquals(GameManager.Instance?.World,world)&&RebirthBossEventPersistence.IsReady&&original.RemovalInvoking&&!original.NativeRemoved&&original.Phase==3&&world.GetEntity(original.EntityId)==null&&item.Chunk!=null&&!item.Chunk.InProgressUnloading&&ReferenceEquals(world.GetChunkSync(original.RemovalX,original.RemovalZ),item.Chunk)&&item.Chunk.ChunkCustomData.dict.TryGetValue(Key(original),out var marker)&&marker.data!=null&&marker.data.SequenceEqual(Stamp(world,item.Event,original));
            if(item.Watch!=null)
            {
                if(current()&&RebirthStationSnapshotEvidence.HasPublished(item.Watch)&&RebirthStationSnapshotEvidence.TryCopy(item.Watch,out var bytes)&&TryPublishedEntity(bytes,original.RemovalX,original.RemovalZ,original,Key(original),Stamp(world,item.Event,original),out string proof,true))
                {
                    original.NativeRemoved=true;original.RemovalProof=proof;
                    if(!RebirthBossEventPersistence.Checkpoint()){original.NativeRemoved=false;original.RemovalProof=string.Empty;continue;}
                    string death=RemovedDeathProof(original);if(death!=null&&!original.DeathPublished){original.DeathProof=death;original.DeathProofKind=2;original.DeathPublished=true;if(!RebirthBossEventPersistence.Checkpoint()){original.DeathPublished=false;original.DeathProofKind=0;original.DeathProof=string.Empty;continue;}}
                    item.Watch.Dispose();removing.Remove(original.Operation);RebirthBossEventRewardService.CompleteOriginalRemoval(world,item.Event,original);continue;
                }
                if(!current()||UnityEngine.Time.realtimeSinceStartup-item.Started>15f){item.Watch.Dispose();item.Watch=null;item.Chunk=null;}continue;
            }
            item.Chunk=world.GetChunkSync(original.RemovalX,original.RemovalZ) as Chunk;var chunk=item.Chunk;var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
            if(!current()||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased))continue;
            var access=regions.regionFileAccess;string path=Path.GetFullPath(regions.saveDirectory);Func<bool> providerCurrent=()=>current()&&ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==path;
            item.Watch=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,providerCurrent,item);if(item.Watch==null)continue;item.Started=UnityEngine.Time.realtimeSinceStartup;
            lock(chunk){if(!providerCurrent()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting){item.Watch.Dispose();item.Watch=null;continue;}chunk.InProgressSaving=true;}
            try{if(providerCurrent())regions.SaveChunkSnapshot(chunk,true);}finally{lock(chunk)chunk.InProgressSaving=false;}
        }
        catch{item.Watch?.Dispose();item.Watch=null;item.Chunk=null;}
    }
    internal static void AdoptLegacy(World world)
    {
        foreach(var e in RebirthBossEventRegistry.SnapshotEvents())
        {
            if(e.State!=RebirthBossEventState.Active||e.SpawnOriginals.Count!=0)continue;
            var selected=new List<Tuple<EntityAlive,RebirthBossSpawnOriginal>>();bool unknown=false;
            foreach(var pair in RebirthBossEventRegistry.SnapshotEntities())
            {
                var state=pair.Value;if(state.EventId!=e.EventId)continue;var entity=world.GetEntity(pair.Key) as EntityAlive;
                if(entity==null||entity.IsDead()||entity.entityClass<0||!ReferenceEquals(entity.world,world)){unknown=true;break;}
                for(int i=0;i<8;i++)if(entity.Buffs==null||entity.Buffs.GetCustomVar("rebirthBossOriginal"+i)!=0f)unknown=true;
                if(unknown)break;
                selected.Add(Tuple.Create(entity,new RebirthBossSpawnOriginal{Operation=Guid.NewGuid(),EntityId=entity.entityId,EntityClass=entity.entityClass,Role=(int)state.Role,Position=entity.position,Phase=0,AdoptionVersion=1}));
            }
            if(unknown||selected.Count==0||!selected.Exists(value=>value.Item2.Role==2&&value.Item2.EntityId==e.BossEntityId))continue;
            try
            {
                foreach(var pair in selected){Prepare(pair.Item2,pair.Item1);pair.Item2.Phase=2;e.SpawnOriginals.Add(pair.Item2);}
                // No native Spawn call exists in this migration. The exact living
                // loaded identity still needs its own future native publication.
                e.NativeBatchPrepared=true;RebirthBossEventPersistence.Checkpoint();
            }
            catch{/* Retain unknown adoption; never assign another GUID. */}
        }
    }
    private static void AdoptLegacyRewards(World world)
    {
        foreach(var e in RebirthBossEventRegistry.SnapshotEvents())
        {
            if(!e.RewardIssued||!e.RewardBlockPlaced||e.RewardEntityId<0||e.SpawnOriginals.Exists(value=>value.Role==3))continue;
            // Legacy data contains an ID/class but no independent original
            // incarnation. Assigning a new GUID to the loaded object would
            // adopt a possible replacement and make later expiry destructive.
            if(e.FailureReason=="legacy-reward-entity-retained-unproven-incarnation")continue;
            e.FailureReason="legacy-reward-entity-retained-unproven-incarnation";
            RebirthBossEventPersistence.MarkDirty();
        }
    }
    private static void CaptureDeadRemoval(World world,RebirthBossEventInstance e,RebirthBossSpawnOriginal original,Entity entity)
    {
        // Only this actual native removal callback may capture the alternative.
        // Existing manual/cancel removal intent, alive source or mere absence cannot.
        if(original.Phase!=3||!original.Killed||original.DeathPublished||original.RemovalInvoking||original.Role>2||e.State==RebirthBossEventState.Cancelled||e.State==RebirthBossEventState.Failed||e.State==RebirthBossEventState.CleaningUp||!(entity is EntityAlive alive)||alive.Health>0||!ReferenceEquals(world.GetEntity(entity.entityId),entity))return;
        var native=new EntityCreationData(entity);if(native.entityData==null||native.entityData.Length>ushort.MaxValue)return;
        using(var blob=StreamUtils.ToBlob(writer=>native.write(writer,StreamModeWrite.Persistency)))original.DeadRemovalFrame=blob.ToArray();
        using(var hash=System.Security.Cryptography.SHA256.Create())original.DeadRemovalHash=BitConverter.ToString(hash.ComputeHash(original.DeadRemovalFrame)).Replace("-","").ToLowerInvariant();
        if(!ValidDeadRemoval(original)){original.DeadRemovalFrame=null;original.DeadRemovalHash=string.Empty;}
    }
    internal static bool ValidDeadRemoval(RebirthBossSpawnOriginal original)
    {
        try
        {
            var frame=original?.DeadRemovalFrame;if(original==null||!original.Killed||original.Phase!=3||original.Role>2||frame==null||frame.Length==0||frame.Length>262144||original.DeadRemovalHash.Length!=64)return false;
            using(var hash=System.Security.Cryptography.SHA256.Create())if(BitConverter.ToString(hash.ComputeHash(frame)).Replace("-","").ToLowerInvariant()!=original.DeadRemovalHash)return false;
            using(var stream=new MemoryStream(frame,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);var native=new EntityCreationData();native.read(reader,StreamModeRead.Persistency);return stream.Position==stream.Length&&native.id==original.EntityId&&native.entityClass==original.EntityClass&&NativeBirth(native,out ulong birth)&&birth==original.WorldTimeBorn&&native.stats?.Health!=null&&!float.IsNaN(native.stats.Health.Value)&&!float.IsInfinity(native.stats.Health.Value)&&native.stats.Health.Value<=0f;}
        }catch{return false;}
    }
    internal static string RemovedDeathProof(RebirthBossSpawnOriginal original)
    {
        if(original==null||!original.NativeRemoved||!original.RemovalInvoking||original.NativeProof.Length!=64||original.RemovalProof.Length!=64||!ValidDeadRemoval(original))return null;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {writer.Write(original.Operation.ToByteArray());writer.Write(original.WorldTimeBorn);writer.Write(original.NativeProof);writer.Write(original.RemovalProof);writer.Write(original.DeadRemovalHash);writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
    }
    internal static bool StageSupportLoot(World world,RebirthBossEventInstance e,EntityAlive killed,RebirthBossEventEntityState state,string lootClass)
    {
        if(!RebirthBossEventPersistence.IsReady||world?.IsRemote()!=false||killed==null||e==null||state==null||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetEntity(killed.entityId),killed)||state?.EventId!=e?.EventId||state.Role!=RebirthBossEventRole.Support)return false;
        var source=e.SpawnOriginals.Find(value=>value.EntityId==killed.entityId&&value.Role==1);
        if(source==null||source.AdoptionVersion!=0&&source.Phase!=3||!Matches(world,e,source,killed))return false;
        if(source.SupportLootClass.Length==0){source.SupportLootClass=lootClass;source.SupportLootPosition=killed.GetPosition();source.SupportLootPosition.y+=0.9f;}
        source.Killed=true;
        // From here this exact original owns the custom output or its retained
        // uncertainty. It must not ALSO take native fallback on an I/O exception.
        if(!RebirthBossEventPersistence.Checkpoint())return true;
        TryPrepareSupportLoot(world,e,source);return true;
    }
    private static void StageOwedSupportLoot(World world)
    {foreach(var e in RebirthBossEventRegistry.SnapshotEvents())foreach(var source in e.SpawnOriginals.ToArray())if(source.Role==1&&source.Killed&&source.DeathPublished&&source.SupportLootClass.Length!=0)TryPrepareSupportLoot(world,e,source);}
    private static void TryPrepareSupportLoot(World world,RebirthBossEventInstance e,RebirthBossSpawnOriginal source)
    {
        if(e.SpawnOriginals.Exists(value=>value.Role==4&&value.ParentOperation==source.Operation))return;
        if(e.SpawnOriginals.Count>=580){e.FailureReason="support-loot-original-retained-capacity";RebirthBossEventPersistence.MarkDirty();return;}
        if(source.SupportLootClass!="EntityLootContainerRegular"&&source.SupportLootClass!="EntityLootContainerStrong"&&source.SupportLootClass!="EntityLootContainerBoss")return;
        EntityLootContainer bag=null;
        try
        {
            int type=EntityClass.FromString(source.SupportLootClass);if(EntityClass.GetEntityClass(type)==null){e.FailureReason="support-loot-original-retained-unsupported-class";RebirthBossEventPersistence.MarkDirty();return;}
            bag=EntityFactory.CreateEntity(type,source.SupportLootPosition,UnityEngine.Vector3.zero) as EntityLootContainer;if(bag==null)return;
            var original=new RebirthBossSpawnOriginal{Operation=Guid.NewGuid(),ParentOperation=source.Operation,Role=4,EntityId=bag.entityId,EntityClass=bag.entityClass,Position=bag.position};Prepare(original,bag);e.SpawnOriginals.Add(original);RebirthBossEventPersistence.Checkpoint();
        }
        catch{e.FailureReason="support-loot-original-retained-preparation";RebirthBossEventPersistence.MarkDirty();}
        finally{DestroyDetached(world,bag);}
    }
    internal static void SuppressRetainedNativeLoot(World world,EntityAlive killed)
    {
        if(!RebirthBossEventPersistence.IsReady||world?.IsRemote()!=false||killed==null)return;
        foreach(var e in RebirthBossEventRegistry.SnapshotEvents())
        {var original=e.SpawnOriginals.Find(value=>value.EntityId==killed.entityId&&value.Killed&&(value.Role==1&&value.SupportLootClass.Length!=0||value.Role==2&&e.Options!=null&&e.Options.Rewards));if(original!=null&&Matches(world,e,original,killed)){killed.lootDropProb=0f;return;}}
    }
    internal static bool AllowOriginalLootLock(Entity entity)
    {
        if(entity==null)return true;var world=entity.world as World;
        foreach(var e in RebirthBossEventRegistry.SnapshotEvents())
        {var original=e.SpawnOriginals.Find(value=>value.Role==4&&value.EntityId==entity.entityId);if(original==null)continue;return world!=null&&!world.IsRemote()&&RebirthBossEventPersistence.IsReady&&original.Phase==3&&!original.RemovalInvoking&&!original.NativeRemoved&&Matches(world,e,original,entity);}
        return true;
    }
    internal static object CaptureAuditState(EntityPlayer actor,Guid eventId)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||actor==null||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||eventId==Guid.Empty)return new{scope=false};
        var e=RebirthBossEventRegistry.SnapshotEvents().Find(value=>value.EventId==eventId);bool archived=false;
        if(e==null){e=RebirthBossEventRegistry.SnapshotArchivedEvents().Find(value=>value.EventId==eventId);archived=e!=null;}
        if(e==null)return new{scope=true,found=false,authoritative=!world.IsRemote(),storeReady=RebirthBossEventPersistence.IsReady};
        if(e.Progression?.OwnerStableId!=RebirthBossEventIdentity.GetStableId(actor))return new{scope=false,found=false};
        var originals=new List<object>();
        foreach(var original in e.SpawnOriginals)
        {
            var live=world.GetEntity(original.EntityId);bool matches=false;try{matches=Matches(world,e,original,live);}catch{}
            string image=null;if(original.PreparedNative!=null)using(var hash=System.Security.Cryptography.SHA256.Create())image=BitConverter.ToString(hash.ComputeHash(original.PreparedNative)).Replace("-","").ToLowerInvariant();
            originals.Add(new{operation=original.Operation.ToString("N"),parent=original.ParentOperation==Guid.Empty?null:original.ParentOperation.ToString("N"),nativeId=original.EntityId,nativeClass=original.EntityClass,nativeBorn=original.WorldTimeBorn,role=original.Role,phase=original.Phase,adoption=original.AdoptionVersion,abandoned=original.Abandoned,preparedFingerprint=image,nativeProof=original.NativeProof,callbackObserved=original.Killed,deathPublished=original.DeathPublished,deathProof=original.DeathProof,deathProofKind=original.DeathProofKind,removedDeadFrameFingerprint=original.DeadRemovalHash,removalInvoking=original.RemovalInvoking,nativeRemoved=original.NativeRemoved,removalProof=original.RemovalProof,currentLoaded=live!=null,currentIdentityMatches=matches,owedSupportClass=original.SupportLootClass});
        }
        return new{scope=true,found=true,archived,authoritative=!world.IsRemote(),storeReady=RebirthBossEventPersistence.IsReady,eventId=e.EventId.ToString("N"),state=e.State.ToString(),batchPrepared=e.NativeBatchPrepared,rewardIssued=e.RewardIssued,rewardPlaced=e.RewardBlockPlaced,rewardId=e.RewardEntityId,retainedReason=e.FailureReason,legacyBlockHeld=e.RewardBlockPlaced&&e.RewardEntityId<0,originals=originals.ToArray()};
    }
    // Native v47 declared custom-data and ECD arrays; no byte-pattern searching.
    internal static bool TryPublishedEntity(byte[] bytes,int x,int z,RebirthBossSpawnOriginal original,string markerKey,byte[] stamp,out string proof,bool absent=false,bool death=false)
    {
        proof=null;if(Chunk.CurrentSaveVersion!=47||bytes==null||bytes.Length<20||bytes.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes||bytes[0]!=116||bytes[1]!=116||bytes[2]!=99||bytes[3]!=0||BitConverter.ToUInt32(bytes,4)!=47)return false;
        try{using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);stream.Position=8;if(reader.ReadInt32()!=x||reader.ReadInt32()!=0||reader.ReadInt32()!=z)return false;Skip(stream,8);
            for(int i=0;i<64;i++)if(Flag(reader)){Skip(stream,Flag(reader)?1024:1);if(Flag(reader))Skip(stream,3072);}Channel(reader,stream,1);Skip(stream,256+256+32+256+1536+2);
            int custom=reader.ReadUInt16();bool marker=false;
            for(int i=0;i<custom;i++){int length=StringLength(reader);if(length<0||length>4096)return false;string key=new UTF8Encoding(false,true).GetString(reader.ReadBytes(length));ulong expires=reader.ReadUInt64();bool network=Flag(reader);int size=reader.ReadUInt16();var data=reader.ReadBytes(size);if(data.Length!=size)return false;if(key==markerKey){if(marker||expires!=ulong.MaxValue||network||!data.SequenceEqual(stamp))return false;marker=true;}}
            if(!marker)return false;Skip(stream,768);Channel(reader,stream,1);Channel(reader,stream,1);Channel(reader,stream,2);Channel(reader,stream,6);Channel(reader,stream,2);Flag(reader);
            int count=reader.ReadInt32();if(count<0||count>4096)return false;byte[] frame=null;
            for(int i=0;i<count;i++){long start=stream.Position;var native=new EntityCreationData();native.read(reader,StreamModeRead.Persistency);if(native.id!=original.EntityId)continue;if(death&&(!original.Killed||native.stats?.Health==null||float.IsNaN(native.stats.Health.Value)||float.IsInfinity(native.stats.Health.Value)||native.stats.Health.Value>0f))return false;if(absent||frame!=null||native.entityClass!=original.EntityClass||!NativeBirth(native,out ulong birth)||birth!=original.WorldTimeBorn)return false;long end=stream.Position;if(end-start<=0||end-start>262144)return false;frame=new byte[(int)(end-start)];Buffer.BlockCopy(bytes,(int)start,frame,0,frame.Length);}
            if(!absent&&frame==null)return false;if(absent)frame=Array.Empty<byte>();using(var result=new MemoryStream())using(var writer=new BinaryWriter(result,Encoding.UTF8,true)){writer.Write(absent);writer.Write(death);writer.Write(stamp.Length);writer.Write(stamp);writer.Write(x);writer.Write(z);writer.Write(frame.Length);writer.Write(frame);writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(result.ToArray())).Replace("-","").ToLowerInvariant();}return true;
        }}catch{return false;}
    }
    internal static bool NativeBirth(EntityCreationData native,out ulong birth)
    {
        birth=0;try{var body=native?.entityData?.ToArray();if(body==null||body.Length<9||native.readFileVersion<15)return false;using(var stream=new MemoryStream(body,false))using(var reader=new BinaryReader(stream)){var source=(EnumSpawnerSource)reader.ReadByte();if(source==EnumSpawnerSource.Biome)Skip(stream,12);birth=reader.ReadUInt64();return true;}}catch{return false;}
    }
    private static bool Flag(BinaryReader reader){byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value==1;}
    private static void Channel(BinaryReader reader,Stream stream,int bytes){for(int i=0;i<64;i++)Skip(stream,Flag(reader)?bytes:1024*bytes);}
    private static void Skip(Stream stream,int count){if(count<0||stream.Position>stream.Length-count)throw new EndOfStreamException();stream.Position+=count;}
    private static int StringLength(BinaryReader reader){uint value=0;for(int i=0;i<5;i++){byte item=reader.ReadByte();if(i==4&&(item&240)!=0)throw new InvalidDataException();value|=(uint)(item&127)<<(7*i);if((item&128)==0)return checked((int)value);}throw new InvalidDataException();}
}
