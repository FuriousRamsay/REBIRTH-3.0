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
    private sealed class Discovery {public Discovery(){}internal World World;internal float Time;internal TileEntityLight[] Lights;}
    private static readonly ConditionalWeakTable<Chunk,Migration> migrated=new ConditionalWeakTable<Chunk,Migration>();
    private static readonly ConditionalWeakTable<PrefabInstance,Discovery> discovery=new ConditionalWeakTable<PrefabInstance,Discovery>();
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
        var local=new Vector3i(x,y,z);string key=Key(local);
        if(Admission!=null&&!world.IsRemote()&&!world.IsEditor()&&(value.Block is BlockLight||value.Block is BlockPoweredLight)&&!value.ischild)
        {
            chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=Image(value,(byte)(Trader(world,chunk.GetWorldPos()+local)?2:1))});
            DefaultPoweredTile(chunk,chunk.GetTileEntity(local));
            chunk.isModified=true;
        }
        else if(previous.type!=value.type&&chunk.ChunkCustomData.dict.ContainsKey(key))
        {chunk.ChunkCustomData.Remove(key);chunk.isModified=true;}
    }
    internal static void Queue(Chunk chunk)
    {
        var state=migrated.GetOrCreateValue(chunk);lock(state)
        {state.Done=false;if(state.Queued)return;state.Queued=true;state.Enqueued=System.Diagnostics.Stopwatch.GetTimestamp();pending.Enqueue(new WeakReference<Chunk>(chunk));}
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
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
        var prefabs=GameManager.Instance.GetDynamicPrefabDecorator()?.allPrefabs;if(prefabs==null)return;
        var state=migrated.GetOrCreateValue(chunk);if(state.Done)return;
        foreach(var tile in chunk.tileEntities.list.ToArray())
        {
            bool plain=tile is TileEntityLight;var powered=tile as TileEntityPoweredBlock;
            if(!plain&&(powered==null||powered.IsPlayerPlaced))continue;
            var position=tile.ToWorldPos();var value=world.GetBlock(position);
            if(!(value.Block is BlockLight)&&!(value.Block is BlockPoweredLight)||value.ischild||chunk.ChunkCustomData.dict.ContainsKey(Key(tile.localChunkPos)))continue;
            if(world.IsWithinTraderArea(position)||world.GetLandClaimOwner(position,null)!=EnumLandClaimOwner.None)continue;
            var poi=prefabs.FirstOrDefault(p=>p?.prefab!=null&&Inside(p,position));if(poi==null||poi.prefab.bTraderArea)continue;
            if(poi.prefab.blockCells==null)continue;
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
        }
        state.Done=true;
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
    internal static IEnumerable<TileEntityLight> Lights(World world,PrefabInstance poi)
    {
        if(poi?.prefab==null||poi.prefab.bTraderArea||world==null)return Array.Empty<TileEntityLight>();
        var cached=discovery.GetOrCreateValue(poi);float now=UnityEngine.Time.realtimeSinceStartup;
        if(ReferenceEquals(cached.World,world)&&cached.Lights!=null&&now>=cached.Time&&now-cached.Time<1f)
            return cached.Lights.Where(t=>ReferenceEquals(world.GetTileEntity(t.ToWorldPos()),t)&&world.GetBlock(t.ToWorldPos()).Block is BlockLight).ToArray();
        var result=new List<TileEntityLight>();var a=poi.boundingBoxPosition;var b=poi.boundingBoxSize;
        for(int x=World.toChunkXZ(a.x);x<=World.toChunkXZ(a.x+b.x-1);x++)
            for(int z=World.toChunkXZ(a.z);z<=World.toChunkXZ(a.z+b.z-1);z++)
            {
                var chunk=world.ChunkCache?.GetChunkSync(x,0,z) as Chunk;if(chunk==null)continue;
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
        cached.World=world;cached.Time=now;cached.Lights=result.ToArray();return cached.Lights;
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
        var position=__instance.GetWorldPos()+new Vector3i(__1,__2,__3);
        if(RebirthCamperWorldLighting.Admission!=null&&!RebirthCamperWorldLighting.Admission.bTraderArea&&!__0.IsRemote()&&!__0.IsEditor()&&
            (__4.Block is BlockLight||__4.Block is BlockPoweredLight)&&!(__0 is World world&&world.IsWithinTraderArea(position)))
            RebirthCamperEnergyDeviceAdapter.OnPlainReset(__0,position);
        RebirthCamperWorldLighting.ApplyDefault(__0,position,ref __4);
    }
    private static void Postfix(Chunk __instance,WorldBase __0,int __1,int __2,int __3,BlockValue __4,BlockValue __result)
        =>RebirthCamperWorldLighting.Capture(__instance,__0,__1,__2,__3,__4,__result);
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
{private static void Prefix(Chunk __instance,TileEntity __0)=>RebirthCamperWorldLighting.DefaultPoweredTile(__instance,__0);}
