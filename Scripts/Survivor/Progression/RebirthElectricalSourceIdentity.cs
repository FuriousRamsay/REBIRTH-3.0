using System;
using System.IO;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

// Native chunk-owned source identity. Unloading never removes a sidecar record.
// A carried configuration deferred by the sidecar limit remains on its exact
// placed native source, rather than disappearing with the consumed item.
internal static class RebirthElectricalSourceIdentity
{
    private const string Prefix="RebirthElectricalSourceV1:";
    internal sealed class CommitBefore{internal BlockValue Block;internal TileEntity Tile;}
    // Scalar JSON image avoids serializing native Vector3i computed properties.
    private sealed class DeferredImage
    {
        public int Clr,X,Y,Z,ServiceCount;public RebirthElectricalServiceKind Kind;
        public float Condition,Workmanship,Skill;public ulong LastService;
        public ushort SourceBlock;public string Source,Author,Background,Bonus;
        public DeferredImage(){}
        internal DeferredImage(RebirthElectricalServiceRecord r)
        {Clr=r.ClrIdx;X=r.Position.x;Y=r.Position.y;Z=r.Position.z;Kind=r.Kind;Condition=r.Condition;Workmanship=r.Workmanship;ServiceCount=r.ServiceCount;LastService=r.LastServiceWorldTime;SourceBlock=r.SourceBlock;Source=r.SourceIncarnation;Author=r.ConfiguredByStableId;Background=r.ConfiguredByBackgroundId;Bonus=r.ConfiguredByBonusId;Skill=r.ConfiguredSkillValue;}
        internal RebirthElectricalServiceRecord Record()=>new RebirthElectricalServiceRecord{ClrIdx=Clr,Position=new Vector3i(X,Y,Z),Kind=Kind,Condition=Condition,Workmanship=Workmanship,ServiceCount=ServiceCount,LastServiceWorldTime=LastService,SourceBlock=SourceBlock,SourceIncarnation=Source,ConfiguredByStableId=Author,ConfiguredByBackgroundId=Background,ConfiguredByBonusId=Bonus,ConfiguredSkillValue=Skill};
    }
    private sealed class QueueState{internal bool Queued;}
    private static readonly ConditionalWeakTable<Chunk,QueueState> queued=new ConditionalWeakTable<Chunk,QueueState>();
    private static readonly ConcurrentQueue<WeakReference<Chunk>> pending=new ConcurrentQueue<WeakReference<Chunk>>();
    [ThreadStatic] private static Upgrade upgrading;
    private sealed class Upgrade{internal WorldBase World;internal Vector3i Position;internal Upgrade Previous;}
    private static string Key(Vector3i local)=>Prefix+local.x+":"+local.y+":"+local.z;
    internal static void Install(Harmony harmony){RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthElectricalSourceBlockCommit));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthElectricalSourceChunkRead));}
    internal static void Queue(Chunk chunk){if(chunk==null)return;var state=queued.GetOrCreateValue(chunk);lock(state){if(state.Queued)return;state.Queued=true;pending.Enqueue(new WeakReference<Chunk>(chunk));}}
    internal static void PromoteLoaded(World world)
    {
        for(int i=0;i<2&&pending.TryDequeue(out var weak);i++)
        {
            if(!weak.TryGetTarget(out var chunk))continue;var state=queued.GetOrCreateValue(chunk);lock(state)state.Queued=false;
            if(chunk.InProgressUnloading||!ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk))continue;
            bool retry=false;
            foreach(var tile in chunk.tileEntities.list)
            {
                if(!(tile is TileEntityPowered))continue;
                try{var r=ReadDeferred(world,tile.ToWorldPos());if(r!=null&&!RebirthInfrastructureWorkService.TryPromoteDeferred(world,r))retry=true;}
                catch(Exception ex){Log.Warning("[REBIRTH Survivor Infrastructure] deferred source retained: "+ex.Message);}
            }
            if(retry)Queue(chunk);
        }
    }
    internal static object BeginUpgrade(WorldBase world,Vector3i position)
    {var scope=new Upgrade{World=world,Position=position,Previous=upgrading};upgrading=scope;return scope;}
    internal static bool UpgradeActive(Vector3i position)=>upgrading!=null&&ReferenceEquals(upgrading.World,GameManager.Instance?.World)&&upgrading.Position==position;
    internal static void EndUpgrade(object scope)
    {
        if(!(scope is Upgrade value)||!ReferenceEquals(upgrading,value))return;
        upgrading=value.Previous;
        if(!RebirthInfrastructureWorkService.IsElectricalBlock(value.World.GetBlock(value.Position).Block))RebirthInfrastructureWorkService.RemoveRecord(value.Position);
    }
    private static bool TryChunk(WorldBase world,Vector3i position,out Chunk chunk,out Vector3i local)
    {
        chunk=world?.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
        local=new Vector3i(position.x&15,position.y,position.z&15);
        return chunk!=null&&!chunk.InProgressUnloading;
    }
    internal static string NativeKey(Vector3i position)=>Key(new Vector3i(position.x&15,position.y,position.z&15));
    internal static byte[] CopyMarker(WorldBase world,Vector3i position)
    {if(Read(world,position)==null||!TryChunk(world,position,out var chunk,out var local))return null;return (byte[])chunk.ChunkCustomData.dict[Key(local)].data.Clone();}
    internal static string Read(WorldBase world,Vector3i position)
    {
        if(!TryChunk(world,position,out var chunk,out var local)||!chunk.ChunkCustomData.dict.TryGetValue(Key(local),out var marker))return null;
        byte[] bytes=marker?.data;
        if(bytes==null||bytes.Length<22||bytes.Length>16406||BitConverter.ToUInt16(bytes,16)!=world.GetBlock(position).type)return null;
        int length=BitConverter.ToInt32(bytes,18);if(length<0||length!=bytes.Length-22)return null;
        var idBytes=new byte[16];Array.Copy(bytes,idBytes,16);var id=new Guid(idBytes);return id==Guid.Empty?null:id.ToString("N");
    }
    internal static string Ensure(WorldBase world,Vector3i position)
    {
        string id=Read(world,position);if(id!=null)return id;
        if(world==null||world.IsRemote()||!ThreadManager.IsMainThread()||!TryChunk(world,position,out _,out _)||!RebirthInfrastructureWorkService.IsElectricalBlock(world.GetBlock(position).Block))return null;
        id=Guid.NewGuid().ToString("N");Write(world,position,id,null);return id;
    }
    private static void Write(WorldBase world,Vector3i position,string id,RebirthElectricalServiceRecord deferred)
    {
        if(!TryChunk(world,position,out var chunk,out var local))throw new InvalidOperationException("Electrical source chunk unavailable.");
        byte[] payload=deferred==null?new byte[0]:Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new DeferredImage(deferred)));
        if(payload.Length>16384)throw new InvalidDataException("Deferred electrical configuration exceeds native marker bound.");
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(Guid.ParseExact(id,"N").ToByteArray());writer.Write(checked((ushort)world.GetBlock(position).type));writer.Write(payload.Length);writer.Write(payload);writer.Flush();
            string key=Key(local);chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=stream.ToArray()});chunk.isModified=true;
        }
    }
    internal static byte[] DescribeDeferredMarker(RebirthElectricalServiceRecord record)
    {
        byte[] payload=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new DeferredImage(record)));
        if(payload.Length>16384)throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {writer.Write(Guid.ParseExact(record.SourceIncarnation,"N").ToByteArray());writer.Write(record.SourceBlock);writer.Write(payload.Length);writer.Write(payload);writer.Flush();return stream.ToArray();}
    }
    internal static bool BindOriginalPlacement(WorldBase world,Vector3i position,RebirthElectricalServiceRecord record)
    {
        string id=RebirthPlacedOriginal.HostSourceBirth(world,position);if(id==null||!RebirthInfrastructureWorkService.IsElectricalBlock(world.GetBlock(position).Block))return false;
        record.SourceIncarnation=id;record.SourceBlock=checked((ushort)world.GetBlock(position).type);Write(world,position,id,record);return Matches(world,record);
    }
    internal static bool Bind(WorldBase world,Vector3i position,RebirthElectricalServiceRecord record)
    {
        string id=Ensure(world,position);if(id==null)return false;
        record.SourceIncarnation=id;record.SourceBlock=checked((ushort)world.GetBlock(position).type);return true;
    }
    internal static bool Matches(WorldBase world,RebirthElectricalServiceRecord record)
        =>record!=null&&!string.IsNullOrEmpty(record.SourceIncarnation)&&record.SourceBlock==world?.GetBlock(record.Position).type&&Read(world,record.Position)==record.SourceIncarnation;
    internal static bool ProvenReplaced(WorldBase world,RebirthElectricalServiceRecord record)
    {
        if(record==null||!TryChunk(world,record.Position,out var chunk,out _)||chunk.IsLocked||chunk.NeedsDecoration||chunk.InProgressDecorating)return false;
        var block=world.GetBlock(record.Position);
        if(!RebirthInfrastructureWorkService.IsElectricalBlock(block.Block))return true;
        string current=Read(world,record.Position);
        if(string.IsNullOrEmpty(current)||string.IsNullOrEmpty(record.SourceIncarnation))return false;
        if(current!=record.SourceIncarnation)return true;
        if(record.SourceBlock!=block.type)RebirthInfrastructureWorkService.RebindUpgrade(world,record.Position,current,checked((ushort)block.type));
        return false;
    }
    internal static bool Defer(WorldBase world,RebirthElectricalServiceRecord record)
    {if(!Matches(world,record))return false;Write(world,record.Position,record.SourceIncarnation,record);if(TryChunk(world,record.Position,out var chunk,out _))Queue(chunk);return true;}
    internal static RebirthElectricalServiceRecord ReadDeferred(WorldBase world,Vector3i position)
    {
        if(Read(world,position)==null||!TryChunk(world,position,out var chunk,out var local))return null;
        var bytes=chunk.ChunkCustomData.dict[Key(local)].data;int count=BitConverter.ToInt32(bytes,18);if(count==0)return null;
        var record=JsonConvert.DeserializeObject<DeferredImage>(Encoding.UTF8.GetString(bytes,22,count))?.Record();
        if(record==null||record.Position!=position||!Matches(world,record)||!RebirthInfrastructureWorkService.ValidRecord(record))throw new InvalidDataException("Deferred source configuration invalid.");
        return record;
    }
    internal static void ClearDeferred(WorldBase world,RebirthElectricalServiceRecord record)
    {
        if(RebirthPlacedOriginal.IsUnpublished(world,record.Position)||!Matches(world,record)||!TryChunk(world,record.Position,out var chunk,out var local))return;
        if(chunk.ChunkCustomData.dict.TryGetValue(Key(local),out var marker)&&marker?.data?.Length>22)
            Write(world,record.Position,record.SourceIncarnation,null);
    }
    internal static void Committed(Chunk chunk,WorldBase world,int x,int y,int z,BlockValue before,TileEntity beforeTile=null,bool observedTile=false)
    {
        if(world==null||world.IsRemote()||world.IsEditor()||!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World))return;
        var local=new Vector3i(x,y,z);var position=chunk.GetWorldPos()+local;var after=chunk.GetBlock(local);
        if(before.type==after.type&&(!observedTile||ReferenceEquals(beforeTile,chunk.GetTileEntity(local))))return; // power/meta changes are the same source
        bool upgraded=upgrading!=null&&ReferenceEquals(upgrading.World,world)&&upgrading.Position==position&&
            RebirthInfrastructureWorkService.IsElectricalBlock(before.Block)&&RebirthInfrastructureWorkService.IsElectricalBlock(after.Block);
        string key=Key(local);string previous=null;RebirthElectricalServiceRecord deferred=null;
        if(upgraded&&chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)&&marker?.data?.Length>=22)
        {
            var bytes=new byte[16];Array.Copy(marker.data,bytes,16);var id=new Guid(bytes);if(id!=Guid.Empty)previous=id.ToString("N");
            int length=BitConverter.ToInt32(marker.data,18);if(length>0&&length==marker.data.Length-22&&length<=16384)
                deferred=JsonConvert.DeserializeObject<DeferredImage>(Encoding.UTF8.GetString(marker.data,22,length))?.Record();
        }
        if(!upgraded)RebirthInfrastructureWorkService.RemoveRecord(position);
        if(chunk.ChunkCustomData.dict.ContainsKey(key)){chunk.ChunkCustomData.Remove(key);chunk.isModified=true;}
        if(!after.ischild&&RebirthInfrastructureWorkService.IsElectricalBlock(after.Block))
        {
            string id=previous??Guid.NewGuid().ToString("N");
            if(deferred!=null){deferred.SourceBlock=checked((ushort)after.type);deferred.SourceIncarnation=id;}
            Write(world,position,id,deferred);if(deferred!=null)Queue(chunk);
            if(upgraded)RebirthInfrastructureWorkService.RebindUpgrade(world,position,id,checked((ushort)after.type));
        }
    }
}

[HarmonyPatch(typeof(Chunk),"SetBlock",new[]{typeof(WorldBase),typeof(int),typeof(int),typeof(int),typeof(BlockValue),typeof(bool),typeof(bool),typeof(bool),typeof(bool),typeof(int)})]
internal static class RebirthElectricalSourceBlockCommit
{
    private static void Prefix(Chunk __instance,int __1,int __2,int __3,out RebirthElectricalSourceIdentity.CommitBefore __state)
    {var local=new Vector3i(__1,__2,__3);__state=new RebirthElectricalSourceIdentity.CommitBefore{Block=__instance.GetBlock(local),Tile=__instance.GetTileEntity(local)};}
    private static void Postfix(Chunk __instance,WorldBase __0,int __1,int __2,int __3,RebirthElectricalSourceIdentity.CommitBefore __state)
        =>RebirthElectricalSourceIdentity.Committed(__instance,__0,__1,__2,__3,__state.Block,__state.Tile,true);
    private static Exception Finalizer(Chunk __instance,WorldBase __0,int __1,int __2,int __3,RebirthElectricalSourceIdentity.CommitBefore __state,Exception __exception)
    {if(__exception!=null&&__state!=null)try{RebirthElectricalSourceIdentity.Committed(__instance,__0,__1,__2,__3,__state.Block,__state.Tile,true);}catch{}return __exception;}
}

[HarmonyPatch(typeof(Chunk),"read",new[]{typeof(PooledBinaryReader),typeof(uint),typeof(bool)})]
internal static class RebirthElectricalSourceChunkRead
{private static void Postfix(Chunk __instance,bool __2){if(!__2)RebirthElectricalSourceIdentity.Queue(__instance);}}
