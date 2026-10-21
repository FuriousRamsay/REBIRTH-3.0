using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;

// Native chunk metadata owns generic placed-workstation identity. No fake item,
// queue row or separate inventory repository participates in admission.
internal static class RebirthLogisticsEndpointIdentity
{
    private const string Prefix="RebirthLogisticsStationV1:";
    private static bool installed;
    private static readonly List<Admission> pending=new List<Admission>();
    private static string Key(Vector3i local)=>Prefix+local.x+":"+local.y+":"+local.z;
    private static byte[] Encode(Guid id,ushort block)
    {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write((byte)1);writer.Write(id.ToByteArray());writer.Write(block);writer.Flush();return stream.ToArray();}}
    private static string Read(ChunkCustomData marker,ushort block)
    {
        var bytes=marker?.data;if(bytes?.Length!=19||bytes[0]!=1||BitConverter.ToUInt16(bytes,17)!=block||marker.TriggerWriteDataDelegate!=null||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork)return null;
        var idBytes=new byte[16];Array.Copy(bytes,1,idBytes,0,16);var id=new Guid(idBytes);return id==Guid.Empty?null:id.ToString("N");
    }
    internal sealed class Admission:IDisposable
    {
        internal World World;internal Chunk Chunk;internal TileEntityWorkstation Tile;internal Vector3i Position;
        internal ushort Block;internal string Id,Directory;internal ChunkCustomData Marker;internal byte[] MarkerImage,Span;
        internal long Offset=-1;internal DateTime Expires;internal Func<bool> Authorized;internal Func<bool> ProviderCurrent;
        internal RebirthStationSnapshotEvidence.Request Request;internal bool Closed;
        internal bool Current()=>!Closed&&ThreadManager.IsMainThread()&&Expires>DateTime.UtcNow&&Authorized?.Invoke()==true&&ProviderCurrent?.Invoke()==true&&
            ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&!Chunk.InProgressUnloading&&
            ReferenceEquals(World.GetChunkSync(Chunk.X,Chunk.Z),Chunk)&&ReferenceEquals(World.GetTileEntity(Position),Tile)&&ReferenceEquals(Tile.GetChunk(),Chunk)&&
            World.GetBlock(Position).type==Block&&Chunk.ChunkCustomData.dict.TryGetValue(Key(Tile.localChunkPos),out var marker)&&ReferenceEquals(marker,Marker)&&Read(marker,Block)==Id&&marker.data.SequenceEqual(MarkerImage);
        internal bool Saved()
        {
            if(!Current()||Request==null||Span==null||!RebirthStationSnapshotEvidence.TryCopy(Request,out var snapshot)||!RebirthStationSnapshotEvidence.HasPublished(Request)||Offset<8||Offset>snapshot.Length-Span.Length)return false;
            for(int i=0;i<Span.Length;i++)if(snapshot[(int)Offset+i]!=Span[i])return false;
            return Current();
        }
        public void Dispose(){if(Closed)return;Closed=true;Request?.Dispose();pending.Remove(this);}
    }
    // Initial admission alone may create a new token. Existing corrupt/unknown
    // metadata remains unknown; it must not be silently rebound to a new job.
    internal static bool TryGetIncarnation(World world,TileEntityWorkstation tile,out string incarnation)
    {
        incarnation=null;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||tile==null||!ReferenceEquals(GameManager.Instance?.World,world)||
            !ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()),tile))return false;
        var chunk=tile.GetChunk();
        if(chunk==null||chunk.InProgressUnloading||!ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk)||
            !chunk.ChunkCustomData.dict.TryGetValue(Key(tile.localChunkPos),out var marker))return false;
        incarnation=Read(marker,checked((ushort)world.GetBlock(tile.ToWorldPos()).type));return incarnation!=null;
    }
    internal static bool TryGetNativeMarker(World world,TileEntityWorkstation tile,out string key,out ChunkCustomData marker)
    {
        key=null;marker=null;
        if(!TryGetIncarnation(world,tile,out var incarnation))return false;
        var chunk=tile.GetChunk();string nativeKey=Key(tile.localChunkPos);
        if(!chunk.ChunkCustomData.dict.TryGetValue(nativeKey,out var current)||Read(current,checked((ushort)world.GetBlock(tile.ToWorldPos()).type))!=incarnation)return false;
        key=nativeKey;marker=current;return true;
    }
    internal static Admission Begin(World world,TileEntityWorkstation tile,Func<bool> authorizedCurrent)
    {
        if(!installed||RebirthLogisticsPlanning.IsActive||!ThreadManager.IsMainThread()||world==null||world.IsRemote()||tile==null||authorizedCurrent?.Invoke()!=true||
            !ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()),tile))return null;
        foreach(var stale in pending.ToArray())if(stale.Expires<=DateTime.UtcNow||!ReferenceEquals(stale.World,world))stale.Dispose();
        if(pending.Count>=4)return null;
        var chunk=tile.GetChunk();var position=tile.ToWorldPos();var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
        if(chunk==null||chunk.InProgressUnloading||!ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk)||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory))return null;
        ushort block=checked((ushort)world.GetBlock(position).type);string key=Key(tile.localChunkPos);
        if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))
        {
            marker=new ChunkCustomData(key,ulong.MaxValue,false){data=Encode(Guid.NewGuid(),block)};
            chunk.ChunkCustomData.Set(key,marker);chunk.isModified=true;
        }
        string id=Read(marker,block);if(id==null)return null;
        string directory=Path.GetFullPath(regions.saveDirectory);var access=regions.regionFileAccess;
        var result=new Admission{World=world,Chunk=chunk,Tile=tile,Position=position,Block=block,Id=id,Marker=marker,MarkerImage=(byte[])marker.data.Clone(),Directory=directory,
            Expires=DateTime.UtcNow.AddSeconds(30),Authorized=authorizedCurrent};
        result.ProviderCurrent=()=>ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory;
        if(!result.Current())return null;
        result.Request=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,result.Current,marker);if(result.Request==null)return null;
        pending.Add(result);
        try
        {
            lock(chunk){if(!result.Current()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting){result.Dispose();return null;}chunk.InProgressSaving=true;}
            try{if(!result.Current()){result.Dispose();return null;}regions.SaveChunkSnapshot(chunk,true);}
            finally{lock(chunk)chunk.InProgressSaving=false;}
            return result;
        }
        catch{result.Dispose();return null;}
    }
    private static void Invalidate(Chunk chunk,Vector3i local)
    {
        var world=GameManager.Instance?.World;
        if(!installed||!ThreadManager.IsMainThread()||world==null||world.IsRemote()||chunk==null||chunk.InProgressUnloading||!ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk))return;
        string key=Key(local);if(chunk.ChunkCustomData.dict.ContainsKey(key)){chunk.ChunkCustomData.Remove(key);chunk.isModified=true;}
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.logistics.station.identity.v1");
        try{foreach(var type in new[]{typeof(BlockCommit),typeof(Remove),typeof(RemoveAll),typeof(Replace),typeof(MarkerWrite)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);ModEvents.GameUpdate.RegisterHandler(Update);installed=true;}
        catch{harmony.UnpatchSelf();throw;}
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        foreach(var request in pending.ToArray())
            try{if(!request.Current())request.Dispose();}catch{request.Dispose();}
    }
    [HarmonyPatch(typeof(Chunk),"SetBlock",new[]{typeof(WorldBase),typeof(int),typeof(int),typeof(int),typeof(BlockValue),typeof(bool),typeof(bool),typeof(bool),typeof(bool),typeof(int)})]
    private static class BlockCommit
    {
        private static void Prefix(Chunk __instance,int __1,int __2,int __3,out BlockValue __state)=>__state=__instance.GetBlock(new Vector3i(__1,__2,__3));
        private static void Postfix(Chunk __instance,int __1,int __2,int __3,BlockValue __state)
        {var local=new Vector3i(__1,__2,__3);if(__instance.GetBlock(local).type!=__state.type)Invalidate(__instance,local);}
        private static Exception Finalizer(Chunk __instance,int __1,int __2,int __3,BlockValue __state,Exception __exception)
        {if(__exception!=null)try{Postfix(__instance,__1,__2,__3,__state);}catch{}return __exception;}
    }
    [HarmonyPatch(typeof(TileEntity),nameof(TileEntity.OnRemove))]
    private static class Remove
    {private static void Prefix(TileEntity __instance){if(__instance is TileEntityWorkstation)Invalidate(__instance.GetChunk(),__instance.localChunkPos);}}
    [HarmonyPatch(typeof(Chunk),nameof(Chunk.RemoveAllTileEntities))]
    private static class RemoveAll
    {
        private static void Prefix(Chunk __instance)
        {
            // Native method directly clears the TE dictionary without OnRemove.
            // Only exact live, non-unloading chunks can invalidate identities.
            foreach(var tile in __instance.tileEntities.list)if(tile is TileEntityWorkstation)Invalidate(__instance,tile.localChunkPos);
        }
    }
    [HarmonyPatch(typeof(Chunk),nameof(Chunk.AddTileEntity))]
    private static class Replace
    {
        private static void Prefix(Chunk __instance,TileEntity _te)
        {if(_te!=null&&__instance.tileEntities.dict.TryGetValue(_te.localChunkPos,out var old)&&old!=null&&!ReferenceEquals(old,_te))Invalidate(__instance,_te.localChunkPos);}
    }
    [HarmonyPatch(typeof(ChunkCustomData),nameof(ChunkCustomData.Write))]
    private static class MarkerWrite
    {
        private static void Prefix(ChunkCustomData __instance,BinaryWriter _bw,out long __state)=>__state=RebirthStationSnapshotEvidence.StartStationSpan(__instance,_bw.BaseStream);
        private static void Postfix(ChunkCustomData __instance,BinaryWriter _bw,long __state)
        {
            if(__state<8)return;
            try
            {
                foreach(var request in pending)if(ReferenceEquals(request.Marker,__instance)&&request.Current())
                {
                    var stream=_bw.BaseStream;long end=stream.Position;long length=end-__state;if(length<=0||length>1024||!stream.CanRead||!stream.CanSeek)return;
                    var bytes=new byte[(int)length];try{stream.Position=__state;int offset=0;while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count<=0)return;offset+=count;}}finally{stream.Position=end;}
                    request.Offset=__state;request.Span=bytes;
                }
            }
            catch{ /* Failed evidence must not change native serialization. */ }
        }
    }
}
