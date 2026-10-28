using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Change native prefab admission defaults, never continually override lights.
internal static class RebirthCamperWorldLighting
{
    [ThreadStatic] internal static Prefab Admission;
    private static bool installed;
    private static readonly ConcurrentQueue<WeakReference<Chunk>> pending=new ConcurrentQueue<WeakReference<Chunk>>();
    private const string MarkerPrefix="rebirth.poi.light.v1:";
    private sealed class Migration {public Migration(){}internal bool Done,Queued;internal long Enqueued;}
    private sealed class Discovery {public Discovery(){}internal World World;internal int Generation;internal TileEntityLight[] Lights;internal Chunk[] Chunks;internal int MinX,MinZ,MaxX,MaxZ;}
    private static readonly ConditionalWeakTable<Chunk,Migration> migrated=new ConditionalWeakTable<Chunk,Migration>();
    private static readonly ConditionalWeakTable<PrefabInstance,Discovery> discovery=new ConditionalWeakTable<PrefabInstance,Discovery>();
    private static int discoveryGeneration;
    internal static int DiscoveryGeneration=>System.Threading.Volatile.Read(ref discoveryGeneration);
    internal static void InvalidateDiscovery()=>System.Threading.Interlocked.Increment(ref discoveryGeneration);
    internal static void Install(Harmony harmony)
    {
        if(installed)return;
        if(harmony==null)throw new ArgumentNullException(nameof(harmony));
        foreach(var method in RebirthCamperWorldLightPrefab.Targets())
            if(method==null)throw new MissingMethodException("Native prefab lighting admission unavailable.");
        if(AccessTools.DeclaredMethod(typeof(Chunk),"SetBlock",new[]{typeof(WorldBase),typeof(int),typeof(int),typeof(int),typeof(BlockValue),typeof(bool),typeof(bool),typeof(bool),typeof(bool),typeof(int)})==null)
            throw new MissingMethodException("Native chunk light admission unavailable.");
        if(AccessTools.DeclaredMethod(typeof(Chunk),"read",new[]{typeof(PooledBinaryReader),typeof(uint),typeof(bool)})==null||
            AccessTools.DeclaredMethod(typeof(GameManager),nameof(GameManager.SetBlocksRPC),new[]{typeof(List<BlockChangeInfo>),typeof(PlatformUserIdentifierAbs)})==null||
            AccessTools.DeclaredMethod(typeof(Chunk),nameof(Chunk.AddTileEntity),new[]{typeof(TileEntity)})==null)
            throw new MissingMethodException("Native loaded/network light admission unavailable.");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperWorldLightPrefab));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperWorldLightAdmission));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperWorldLightLoaded));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperWorldLightRpc));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperWorldPoweredLightTile));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperAuthoredLightIndexChanged));
        ModEvents.GameUpdate.RegisterHandler(Update);ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);installed=true;
    }
    private static string Key(Vector3i p)=>MarkerPrefix+p.x+":"+p.y+":"+p.z;
    private static bool Trader(WorldBase world,Vector3i position)=>Admission?.bTraderArea==true||world is World current&&current.IsWithinTraderArea(position);
    private static byte[] Image(BlockValue value,byte provenance)
    {var bytes=new byte[8];bytes[0]=(byte)value.type;bytes[1]=(byte)(value.type>>8);bytes[2]=(byte)value.rotation;bytes[3]=provenance;Array.Copy(Guid.NewGuid().ToByteArray(),0,bytes,4,4);bytes[4]|=1;return bytes;}
    internal static uint Stamp(TileEntity light)
    {return light?.chunk!=null&&light.chunk.ChunkCustomData.dict.TryGetValue(Key(light.localChunkPos),out var marker)&&marker?.data?.Length==8?BitConverter.ToUInt32(marker.data,4):0;}
    internal static void Capture(Chunk chunk,WorldBase world,int x,int y,int z,BlockValue value,BlockValue previous)
    {
        bool currentLight=value.Block is BlockLight||value.Block is BlockPoweredLight;
        bool previousLight=previous.Block is BlockLight||previous.Block is BlockPoweredLight;
        if(!currentLight&&!previousLight)return;
        var local=new Vector3i(x,y,z);string key=Key(local);
        if((value.type!=previous.type||value.rotation!=previous.rotation)&&
            (currentLight||previousLight))
            InvalidateDiscovery();
        if(Admission!=null&&!world.IsRemote()&&!world.IsEditor()&&currentLight&&!value.ischild)
        {
            RebirthCamperPoiLightChoices.Clear(chunk,local);
            chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=Image(value,(byte)(Trader(world,chunk.GetWorldPos()+local)?2:1))});
            DefaultPoweredTile(chunk,chunk.GetTileEntity(local));
            chunk.isModified=true;
        }
        else if(previous.type!=value.type||previous.rotation!=value.rotation)
        {RebirthCamperPoiLightChoices.Clear(chunk,local);if(chunk.ChunkCustomData.dict.ContainsKey(key))chunk.ChunkCustomData.Remove(key);chunk.isModified=true;InvalidateDiscovery();}
    }
    internal static void Queue(Chunk chunk)
    {
        var state=migrated.GetOrCreateValue(chunk);lock(state)
        {state.Done=false;if(state.Queued)return;state.Queued=true;state.Enqueued=System.Diagnostics.Stopwatch.GetTimestamp();pending.Enqueue(new WeakReference<Chunk>(chunk));}
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        if(pending.IsEmpty)return;
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||world.IsEditor())return;
        for(int i=0;i<2&&pending.TryDequeue(out var weak);i++)
            if(weak.TryGetTarget(out var chunk))
            {
                var current=world.ChunkCache?.GetChunkSync(chunk.X,chunk.Y,chunk.Z);
                var state=migrated.GetOrCreateValue(chunk);
                if(ReferenceEquals(current,chunk)){lock(state)state.Queued=false;Migrate(world,chunk);}
                else if(current==null&&(System.Diagnostics.Stopwatch.GetTimestamp()-state.Enqueued)/(double)System.Diagnostics.Stopwatch.Frequency<60)pending.Enqueue(weak);
                else lock(state)state.Queued=false;
            }
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
    {while(pending.TryDequeue(out _)){} }
    internal static void Migrate(World world,Chunk chunk)
    {
        if(!ThreadManager.IsMainThread()||world.IsRemote())return;
        var decorator=GameManager.Instance.GetDynamicPrefabDecorator();
        var prefabs=decorator?.allPrefabs;if(prefabs==null)return;
        var state=migrated.GetOrCreateValue(chunk);if(state.Done)return;
        if(!prefabs.Any())return;
        bool measure=RebirthGameBridgeFrames.IsRecording;
        long started=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        int scanned=0,overlapping=0,created=0;
        bool pendingAuthoredData=false;
        // Some native prefab lamps are blocks with no instantiated light tile.
        // Recover only exact authored BlockLight matches in this loaded chunk;
        // the normal migration below then stamps and switches them off.
        var origin=chunk.GetWorldPos();
        var nearby=new List<PrefabInstance>();
        decorator.GetPrefabsAtXZ(origin.x,origin.x+15,origin.z,origin.z+15,nearby);
        foreach(var poi in nearby)
        {
            if(poi?.prefab==null||poi.prefab.bTraderArea)continue;
            var a=poi.boundingBoxPosition;var b=poi.boundingBoxSize;
            int minX=Math.Max(origin.x,a.x),maxX=Math.Min(origin.x+15,a.x+b.x-1);
            int minZ=Math.Max(origin.z,a.z),maxZ=Math.Min(origin.z+15,a.z+b.z-1);
            if(minX>maxX||minZ>maxZ)continue;
            if(measure)overlapping++;
            if(poi.prefab.blockCells==null){pendingAuthoredData=true;continue;}
            foreach(var offset in RebirthCamperAuthoredLightIndex.Lights(poi.prefab))
                {
                    int x=a.x+offset.x,y=a.y+offset.y,z=a.z+offset.z;
                    if(x<minX||x>maxX||z<minZ||z>maxZ)continue;
                    if(measure)scanned++;
                    var position=new Vector3i(x,y,z);
                    var value=chunk.GetBlock(new Vector3i(x-origin.x,y,z-origin.z));
                    if(value.ischild||value.Block is not BlockLight lamp||!lamp.HasTileEntity)continue;
                    var authored=poi.prefab.GetBlock(x-a.x,y-a.y,z-a.z);
                    if(authored.type!=value.type||authored.rotation!=value.rotation)continue;
                    if(world.GetTileEntity(position)!=null||world.IsWithinTraderArea(position)||
                        world.GetLandClaimOwner(position,null)!=EnumLandClaimOwner.None||
                        RebirthPlacedOriginal.IsUnpublished(world,position))continue;
                    try
                    {
                        var light=lamp.CreateTileEntity(chunk) as TileEntityLight;
                        if(light==null)continue;
                        light.localChunkPos=World.toBlock(position);
                        chunk.AddTileEntity(light);light.SetModified();chunk.isModified=true;
                        if(measure)created++;
                    }
                    catch(Exception error){Log.Warning("[REBIRTH CamperEnergy] Authored lamp tile unavailable at "+position+": "+error.GetType().Name);}
                }
        }
        foreach(var tile in chunk.tileEntities.list.ToArray())
        {
            bool plain=tile is TileEntityLight;var powered=tile as TileEntityPoweredBlock;
            if(!plain&&(powered==null||powered.IsPlayerPlaced))continue;
            var position=tile.ToWorldPos();var value=world.GetBlock(position);
            if(RebirthPlacedOriginal.IsUnpublished(world,position)){pendingAuthoredData=true;continue;}
            if(!(value.Block is BlockLight)&&!(value.Block is BlockPoweredLight)||value.ischild||chunk.ChunkCustomData.dict.ContainsKey(Key(tile.localChunkPos)))continue;
            if(world.IsWithinTraderArea(position)||world.GetLandClaimOwner(position,null)!=EnumLandClaimOwner.None)continue;
            // Preserve original tie order only when overlapping POIs make it
            // relevant; the common single-POI chunk needs no world-wide search.
            var poi=nearby.Count==1?(Inside(nearby[0],position)?nearby[0]:null):
                nearby.Count==0?null:prefabs.FirstOrDefault(p=>p?.prefab!=null&&Inside(p,position));
            if(poi?.prefab==null||poi.prefab.bTraderArea)continue;
            if(poi.prefab.blockCells==null){pendingAuthoredData=true;continue;}
            // Decorator stores GetPrefabRotated; GetBlock itself handles its
            // shared-data RotateCoords/ApplyRotation. Do not rotate twice.
            var offset=position-poi.boundingBoxPosition;var authored=poi.prefab.GetBlock(offset.x,offset.y,offset.z);
            if(!(authored.Block is BlockLight)&&!(authored.Block is BlockPoweredLight)||authored.type!=value.type||authored.rotation!=value.rotation)continue;
            // Legacy structural heuristic grants ONLY reversible light control.
            bool retain=RebirthCamperEnergyDeviceAdapter.TryRetainPlainProvenance(world,position,value,out uint stamp);
            if(!retain&&powered!=null)powered.IsToggled=false;
            byte next=(byte)(value.meta&~3);if(!retain&&next!=value.meta){value.meta=next;world.SetBlockRPC(position,value);}
            var image=Image(value,3);if(retain)Array.Copy(BitConverter.GetBytes(stamp),0,image,4,4);
            chunk.ChunkCustomData.Set(Key(tile.localChunkPos),new ChunkCustomData(Key(tile.localChunkPos),ulong.MaxValue,false){data=image});
            chunk.isModified=true;
            InvalidateDiscovery();
        }
        // A loaded chunk can precede its decorator prefab's block data. Do not
        // permanently cache that temporary inability as a completed migration.
        state.Done=!pendingAuthoredData;
        if(measure)
        {
            double elapsed=1000.0*(System.Diagnostics.Stopwatch.GetTimestamp()-started)/System.Diagnostics.Stopwatch.Frequency;
            if(elapsed>=15)
                Log.Out("[REBIRTH LightMigrationPerf] frame="+UnityEngine.Time.frameCount+
                    " chunk="+chunk.X+":"+chunk.Z+" ms="+elapsed.ToString("F2")+
                    " overlappingPoi="+overlapping+" scanned="+scanned+" created="+created+
                    " pending="+pendingAuthoredData);
        }
    }
    internal static void MigratePoi(World world,PrefabInstance poi)
    {
        if(poi?.prefab==null)return;var a=poi.boundingBoxPosition;var b=poi.boundingBoxSize;
        for(int x=World.toChunkXZ(a.x);x<=World.toChunkXZ(a.x+b.x-1);x++)
            for(int z=World.toChunkXZ(a.z);z<=World.toChunkXZ(a.z+b.z-1);z++)
                if(world.ChunkCache?.GetChunkSync(x,0,z) is Chunk chunk)Migrate(world,chunk);
    }
    private static bool Inside(PrefabInstance p,Vector3i v)
    {var a=p.boundingBoxPosition;var b=p.boundingBoxSize;return v.x>=a.x&&v.x<a.x+b.x&&v.y>=a.y&&v.y<a.y+b.y&&v.z>=a.z&&v.z<a.z+b.z;}
    // Ordinary lamps inside wireless reach are independent of the POI switch.
    // Authorize before recording a stable identity for exact restoration;
    // discovery itself never turns a lamp on.
    // A player-placed BlockLight can have no native TileEntityLight. Install the
    // block's own native light tile only for a persistent, matching placement
    // marker in reach; then the existing reversible plain-light path applies.
    private static void MaterializePlacedLightTiles(World world,Chunk chunk,UnityEngine.Vector3 center,float range,Func<Vector3i,bool> authorized,Func<Vector3i,bool> footprint=null)
    {
        foreach(var key in chunk.ChunkCustomData.dict.Keys.Where(value=>value.StartsWith(RebirthPlacedWorkmanshipIdentity.Prefix,StringComparison.Ordinal)).ToArray())
        {
            var coordinates=key.Substring(RebirthPlacedWorkmanshipIdentity.Prefix.Length).Split(':');
            if(coordinates.Length!=3||!int.TryParse(coordinates[0],out int x)||!int.TryParse(coordinates[1],out int y)||!int.TryParse(coordinates[2],out int z)||
                x<0||x>15||z<0||z>15)continue;
            var origin=chunk.GetWorldPos();var position=new Vector3i(origin.x+x,y,origin.z+z);
            if(!(footprint!=null?footprint(position):RebirthCamperEnergyBounds.ContainsDistance(RebirthCamperEnergyBounds.Distance(center,position.ToVector3Center()),range))||
                authorized?.Invoke(position)!=true||RebirthPlacedWorkmanshipIdentity.Read(world,position)==null||world.GetTileEntity(position)!=null)continue;
            var block=world.GetBlock(position);
            if(block.ischild||block.Block is not BlockLight lamp||!block.Block.HasTileEntity||world.IsWithinTraderArea(position))continue;
            TileEntityLight tile=null;
            try
            {
                tile=lamp.CreateTileEntity(chunk);tile.localChunkPos=World.toBlock(position);
                chunk.AddTileEntity(tile);tile.SetModified();chunk.isModified=true;
            }
            catch(Exception error){Log.Warning("[REBIRTH CamperEnergy] Native placed lamp tile unavailable at "+position+": "+error.GetType().Name);}
        }
    }
    // Native additions are an incarnation boundary even when the replacement
    // has the same block type/rotation. The caller is the existing placement hook.
    internal static void RecordPlacedPoiLight(Block block,WorldBase worldBase,Chunk chunk,Vector3i position,BlockValue placed,PlatformUserIdentifierAbs actor)
    {
        if(actor==null||chunk==null||Admission!=null||placed.ischild||!(block is BlockLight||block is BlockPoweredLight)||
            !ThreadManager.IsMainThread()||!(worldBase is World world)||world.IsRemote()||world.IsEditor()||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||!ReferenceEquals(GameManager.Instance?.World,world)||
            !ReferenceEquals(world.ChunkCache?.GetChunkFromWorldPos(position),chunk)||world.IsWithinTraderArea(position))return;
        var current=world.GetBlock(position);
        if(current.ischild||current.type!=placed.type||current.rotation!=placed.rotation||!ReferenceEquals(current.Block,block))return;
        var local=World.toBlock(position);
        RebirthCamperPoiLightChoices.Clear(chunk,local);
        chunk.ChunkCustomData.Set(Key(local),new ChunkCustomData(Key(local),ulong.MaxValue,false){data=Image(current,3)});
        chunk.isModified=true;InvalidateDiscovery();
    }
    // Called at the adapter's existing admission cadence, never per frame. Scan
    // only loaded footprint chunks and existing placement records/native tiles.
    internal static void AdmitPlacedPoiTarget(World world,PrefabInstance poi,TileEntity tile,Func<Vector3i,bool> authorized)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||world.IsEditor()||tile?.chunk==null||
            !ReferenceEquals(GameManager.Instance?.World,world)||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
            poi?.prefab==null||poi.prefab.bTraderArea||authorized==null)return;
        var chunk=tile.chunk;var position=tile.ToWorldPos();
        if(!Inside(poi,position)||world.IsWithinTraderArea(position)||!authorized(position)||
            !ReferenceEquals(world.GetTileEntity(position),tile)||!ReferenceEquals(world.ChunkCache?.GetChunkFromWorldPos(position),chunk))return;
        bool placed=RebirthPlacedWorkmanshipIdentity.Read(world,position)!=null||tile is TileEntityPoweredBlock nativePlaced&&nativePlaced.IsPlayerPlaced;
        if(!placed)return;
        var value=world.GetBlock(position);
        if(value.ischild||!(value.Block is BlockLight&&tile is TileEntityLight||value.Block is BlockPoweredLight&&tile is TileEntityPoweredBlock))return;
        string key=Key(tile.localChunkPos);
        if(chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)&&marker?.data?.Length==8&&(marker.data[3]==1||marker.data[3]==2))return;
        if(marker?.data?.Length==8&&marker.data[3]==3&&(marker.data[0]|marker.data[1]<<8)==value.type&&marker.data[2]==value.rotation&&BitConverter.ToUInt32(marker.data,4)!=0)return;
        RebirthCamperPoiLightChoices.Clear(chunk,tile.localChunkPos);
        chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=Image(value,3)});
        chunk.isModified=true;InvalidateDiscovery();
    }
    internal static void AdmitPlacedPoiLights(World world,PrefabInstance poi,Func<Vector3i,bool> authorized)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||world.IsEditor()||
            !ReferenceEquals(GameManager.Instance?.World,world)||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
            poi?.prefab==null||poi.prefab.bTraderArea||authorized==null)return;
        var a=poi.boundingBoxPosition;var b=poi.boundingBoxSize;
        if(b.x<=0||b.y<=0||b.z<=0)return;
        for(int x=World.toChunkXZ(a.x);x<=World.toChunkXZ(a.x+b.x-1);x++)
            for(int z=World.toChunkXZ(a.z);z<=World.toChunkXZ(a.z+b.z-1);z++)
            {
                if(world.ChunkCache?.GetChunkSync(x,0,z) is not Chunk chunk)continue;
                MaterializePlacedLightTiles(world,chunk,UnityEngine.Vector3.zero,0,authorized,p=>Inside(poi,p));
                foreach(var tile in chunk.tileEntities.list.ToArray())
                {
                    if(tile==null||!ReferenceEquals(tile.chunk,chunk))continue;
                    AdmitPlacedPoiTarget(world,poi,tile,authorized);
                }
            }
    }
    internal static IEnumerable<TileEntityLight> PlacedLights(World world,UnityEngine.Vector3 center,float range,Func<TileEntityLight,bool> authorized,Func<Vector3i,bool> authorizeMissingTile)
    {
        if(world==null||world.IsRemote()||!ThreadManager.IsMainThread())yield break;
        int minX=World.toChunkXZ(UnityEngine.Mathf.FloorToInt(center.x-range)),maxX=World.toChunkXZ(UnityEngine.Mathf.CeilToInt(center.x+range));
        int minZ=World.toChunkXZ(UnityEngine.Mathf.FloorToInt(center.z-range)),maxZ=World.toChunkXZ(UnityEngine.Mathf.CeilToInt(center.z+range));
        for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
        {
            if(world.ChunkCache?.GetChunkSync(x,0,z) is not Chunk chunk)continue;
            MaterializePlacedLightTiles(world,chunk,center,range,authorizeMissingTile);
            foreach(var tile in chunk.tileEntities.list)
            {
                if(tile is not TileEntityLight light||authorized?.Invoke(light)!=true)continue;
                var pos=light.ToWorldPos();var value=world.GetBlock(pos);
                if(value.ischild||value.Block is not BlockLight||world.IsWithinTraderArea(pos)||
                    !RebirthCamperEnergyBounds.ContainsDistance(RebirthCamperEnergyBounds.Distance(center,pos.ToVector3Center()),range))continue;
                string key=Key(light.localChunkPos);
                if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))
                {
                    chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=Image(value,3)});
                    chunk.isModified=true;
                    InvalidateDiscovery();
                }
                if(Stamp(light)!=0)yield return light;
            }
        }
    }
    internal static IEnumerable<TileEntityLight> Lights(World world,PrefabInstance poi)
    {
        if(poi?.prefab==null||poi.prefab.bTraderArea||world==null)return Array.Empty<TileEntityLight>();
        var cached=discovery.GetOrCreateValue(poi);int generation=System.Threading.Volatile.Read(ref discoveryGeneration);
        var a=poi.boundingBoxPosition;var b=poi.boundingBoxSize;
        int minX=World.toChunkXZ(a.x),maxX=World.toChunkXZ(a.x+b.x-1);
        int minZ=World.toChunkXZ(a.z),maxZ=World.toChunkXZ(a.z+b.z-1);
        if(ReferenceEquals(cached.World,world)&&cached.Lights!=null&&cached.Generation==generation&&
            cached.MinX==minX&&cached.MaxX==maxX&&cached.MinZ==minZ&&cached.MaxZ==maxZ&&cached.Chunks!=null)
        {
            bool same=true;int index=0;
            for(int x=minX;x<=maxX&&same;x++)for(int z=minZ;z<=maxZ;z++)
                if(!ReferenceEquals(cached.Chunks[index++],world.ChunkCache?.GetChunkSync(x,0,z))){same=false;break;}
            if(same)return cached.Lights;
        }
        var result=new List<TileEntityLight>();var chunks=new Chunk[(maxX-minX+1)*(maxZ-minZ+1)];int chunkIndex=0;
        for(int x=minX;x<=maxX;x++)
            for(int z=minZ;z<=maxZ;z++)
            {
                var chunk=world.ChunkCache?.GetChunkSync(x,0,z) as Chunk;chunks[chunkIndex++]=chunk;if(chunk==null)continue;
                foreach(var tile in chunk.tileEntities.list)
                {
                    if(!(tile is TileEntityLight light)||!Inside(poi,tile.ToWorldPos()))continue;
                    var value=world.GetBlock(tile.ToWorldPos());
                    if(!(value.Block is BlockLight)||value.ischild||world.IsWithinTraderArea(tile.ToWorldPos())||
                        !chunk.ChunkCustomData.dict.TryGetValue(Key(tile.localChunkPos),out var marker)||marker?.data?.Length!=8||marker.data[3]==2||
                        (marker.data[0]|marker.data[1]<<8)!=value.type||marker.data[2]!=value.rotation)continue;
                    result.Add(light);
                }
            }
        cached.World=world;cached.Generation=generation;cached.Lights=result.ToArray();cached.Chunks=chunks;
        cached.MinX=minX;cached.MaxX=maxX;cached.MinZ=minZ;cached.MaxZ=maxZ;return cached.Lights;
    }
    // Explicit /energy devices observation only. Include excluded native light
    // tiles so a reported pendant can be identified without changing its state.
    internal static Newtonsoft.Json.Linq.JArray DescribeCandidates(World world,PrefabInstance poi,int limit)
    {
        var rows=new Newtonsoft.Json.Linq.JArray();
        if(world==null||poi?.prefab==null||limit<=0)return rows;
        var a=poi.boundingBoxPosition;var b=poi.boundingBoxSize;
        for(int x=World.toChunkXZ(a.x);x<=World.toChunkXZ(a.x+b.x-1)&&rows.Count<limit;x++)
            for(int z=World.toChunkXZ(a.z);z<=World.toChunkXZ(a.z+b.z-1)&&rows.Count<limit;z++)
            {
                if(!(world.ChunkCache?.GetChunkSync(x,0,z) is Chunk chunk))continue;
                foreach(var tile in chunk.tileEntities.list)
                {
                    var position=tile.ToWorldPos();if(!Inside(poi,position))continue;
                    var value=world.GetBlock(position);
                    if(!(value.Block is BlockLight)&&!(value.Block is BlockPoweredLight))continue;
                    chunk.ChunkCustomData.dict.TryGetValue(Key(tile.localChunkPos),out var marker);
                    bool valid=marker?.data?.Length==8;
                    string reason=value.ischild?"child":world.IsWithinTraderArea(position)?"trader":
                        !valid?(world.GetLandClaimOwner(position,null)!=EnumLandClaimOwner.None?"missing_provenance_claimed":"missing_provenance"):
                        marker.data[3]==2?"trader_provenance":(marker.data[0]|marker.data[1]<<8)!=value.type||marker.data[2]!=value.rotation?
                        "provenance_type_or_rotation_mismatch":value.Block is BlockLight&&!(tile is TileEntityLight)?"wrong_native_light_tile":"provenance_ready";
                    rows.Add(new Newtonsoft.Json.Linq.JObject{
                        ["position"]=position.ToString(),["block"]=value.Block.GetBlockName(),["blockClass"]=value.Block.GetType().Name,
                        ["tileType"]=tile.GetType().Name,["meta"]=value.meta,["marker"]=valid,["reason"]=reason,
                        ["authoredDataLoaded"]=poi.prefab.blockCells!=null});
                    if(rows.Count>=limit)break;
                }
            }
        return rows;
    }
    internal static void ApplyDefault(WorldBase world,Vector3i position,ref BlockValue value)
    {
        var prefab=Admission;
        // Editor exports and all subsequent native/player/camper toggles stay
        // unchanged. TraderArea is the exact native prefab flag, not a name.
        if(prefab==null||world==null||Trader(world,position)||world.IsEditor()||world.IsRemote()||value.ischild||
            !(value.Block is BlockLight)&&!(value.Block is BlockPoweredLight))return;
        // bit0 is the native original-state bit; bit1 is the current light bit.
        // Native BlockLight.OnBlockAdded will capture this default normally.
        value.meta=(byte)(value.meta&~3);
    }
    internal static void DefaultPoweredTile(Chunk chunk,TileEntity tile)
    {
        if(tile==null||Admission==null||Trader(GameManager.Instance?.World,chunk.GetWorldPos()+tile.localChunkPos)||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||GameManager.Instance?.World?.IsEditor()==true||
            !(tile is TileEntityPoweredBlock powered)||powered.IsPlayerPlaced||
            !(chunk.GetBlock(tile.localChunkPos).Block is BlockPoweredLight))return;
        powered.IsToggled=false;
    }
}

[HarmonyPatch]
internal static class RebirthCamperWorldLightPrefab
{
    internal static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.DeclaredMethod(typeof(Prefab),"CopyIntoRPC",new[]{typeof(GameManager),typeof(Vector3i),typeof(bool)});
        yield return AccessTools.DeclaredMethod(typeof(Prefab),"CopyIntoLocal",new[]{typeof(ChunkCluster),typeof(Vector3i),typeof(bool),typeof(bool),typeof(FastTags<TagGroup.Global>)});
        yield return AccessTools.DeclaredMethod(typeof(Prefab),"CopyBlocksIntoChunkNoEntities",new[]{typeof(World),typeof(Chunk),typeof(Vector3i),typeof(bool),typeof(FastTags<TagGroup.Global>)});
    }
    private static IEnumerable<MethodBase> TargetMethods()=>Targets();
    private static void Prefix(Prefab __instance,out Prefab __state)
    {__state=RebirthCamperWorldLighting.Admission;RebirthCamperWorldLighting.Admission=__instance;}
    private static Exception Finalizer(Exception __exception,Prefab __state)
    {RebirthCamperWorldLighting.Admission=__state;return __exception;}
}

[HarmonyPatch(typeof(Chunk),"SetBlock",new[]{typeof(WorldBase),typeof(int),typeof(int),typeof(int),typeof(BlockValue),typeof(bool),typeof(bool),typeof(bool),typeof(bool),typeof(int)})]
internal static class RebirthCamperWorldLightAdmission
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Chunk __instance,WorldBase __0,int __1,int __2,int __3,ref BlockValue __4)
    {
        if(RebirthCamperWorldLighting.Admission!=null&&!RebirthCamperWorldLighting.Admission.bTraderArea&&!__0.IsRemote()&&!__0.IsEditor()&&
            (__4.Block is BlockLight||__4.Block is BlockPoweredLight))
        {
            var position=__instance.GetWorldPos()+new Vector3i(__1,__2,__3);
            if(!(__0 is World world&&world.IsWithinTraderArea(position)))
                RebirthCamperEnergyDeviceAdapter.OnPlainReset(__0,position);
            RebirthCamperWorldLighting.ApplyDefault(__0,position,ref __4);
        }
    }
    private static void Postfix(Chunk __instance,WorldBase __0,int __1,int __2,int __3,BlockValue __4,BlockValue __result)
    {
        if(__4.Block is BlockLight||__4.Block is BlockPoweredLight||__result.Block is BlockLight||__result.Block is BlockPoweredLight)
            RebirthCamperWorldLighting.Capture(__instance,__0,__1,__2,__3,__4,__result);
    }
}

[HarmonyPatch(typeof(Chunk),"read",new[]{typeof(PooledBinaryReader),typeof(uint),typeof(bool)})]
internal static class RebirthCamperWorldLightLoaded
{
    private static void Postfix(Chunk __instance,bool __2){if(!__2)RebirthCamperWorldLighting.Queue(__instance);}
}

// Native SetBlocksRPC applies the local changes then publishes the same list.
// Correct its scoped prefab payload too, so peers receive the admitted OFF state.
[HarmonyPatch(typeof(GameManager),nameof(GameManager.SetBlocksRPC),new[]{typeof(List<BlockChangeInfo>),typeof(PlatformUserIdentifierAbs)})]
internal static class RebirthCamperWorldLightRpc
{
    private static void Prefix(GameManager __instance,List<BlockChangeInfo> __0)
    {
        if(RebirthCamperWorldLighting.Admission==null||__0==null)return;
        foreach(var change in __0)if(change!=null&&change.bChangeBlockValue)
        {if(!change.blockValueRef.TryGetBlockPos(out var position))continue;var value=change.blockValue;RebirthCamperWorldLighting.ApplyDefault(__instance.World,position,ref value);change.blockValue=value;}
    }
}

[HarmonyPatch(typeof(Chunk),nameof(Chunk.AddTileEntity),new[]{typeof(TileEntity)})]
internal static class RebirthCamperWorldPoweredLightTile
{
    private static void Prefix(Chunk __instance,TileEntity __0)=>RebirthCamperWorldLighting.DefaultPoweredTile(__instance,__0);
    private static void Postfix(TileEntity __0)
    {if(__0 is TileEntityLight)RebirthCamperWorldLighting.InvalidateDiscovery();}
}
