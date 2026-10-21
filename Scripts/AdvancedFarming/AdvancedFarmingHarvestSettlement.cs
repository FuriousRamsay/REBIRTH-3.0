using System;
using System.IO;
using HarmonyLib;

#nullable disable

// Harvest-domain native source identity. This is groundwork for the original-action
// receipt protocol; no producer calls it until that protocol is fully wired.
internal static partial class AdvancedFarmingHarvestSettlement
{
    private const string SourcePrefix="RebirthHarvestSourceV1:";
    private const string CommitPrefix="RebirthHarvestCommitV1:";
    internal sealed class SourceBefore { internal BlockValue Block;internal TileEntity Tile;internal object NativeMutation; }
    private static string SourceKey(Vector3i local)=>SourcePrefix+local.x+":"+local.y+":"+local.z;
    private static bool SourceChunk(WorldBase world,Vector3i position,out Chunk chunk,out Vector3i local)
    {
        chunk=world?.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
        local=new Vector3i(position.x&15,position.y,position.z&15);
        return chunk!=null&&!chunk.InProgressUnloading&&!chunk.IsLocked&&!chunk.NeedsDecoration&&!chunk.InProgressDecorating;
    }
    private static bool SameSourceStage(BlockValue before,BlockValue after)=>before.type==after.type&&before.meta==after.meta&&before.meta2==after.meta2&&before.rotation==after.rotation;
    internal static string ReadSource(WorldBase world,Vector3i position,out bool malformed)
    {
        malformed=false;
        if(!SourceChunk(world,position,out var chunk,out var local)||!chunk.ChunkCustomData.dict.TryGetValue(SourceKey(local),out var marker))return null;
        var bytes=marker?.data;
        // Fixed scalar payload, never deserialize arbitrary native objects.
        if(bytes==null||bytes.Length!=22||bytes[0]!=1){malformed=true;return null;}
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
        {
            reader.ReadByte();var id=new Guid(reader.ReadBytes(16));ushort type=reader.ReadUInt16();byte meta=reader.ReadByte(),meta2=reader.ReadByte(),rotation=reader.ReadByte();
            var block=world.GetBlock(position);
            if(id==Guid.Empty||type!=block.type||meta!=block.meta||meta2!=block.meta2||rotation!=block.rotation){malformed=true;return null;}
            return id.ToString("N");
        }
    }
    internal static string EnsureSource(WorldBase world,Vector3i position)
    {
        if(world==null||!ReferenceEquals(world,GameManager.Instance?.World)||world.IsRemote()||!ThreadManager.IsMainThread()||!SourceChunk(world,position,out var chunk,out var local))return null;
        string current=ReadSource(world,position,out bool malformed);
        if(current!=null||malformed)return current;
        var block=world.GetBlock(position);
        if(!(block.Block is BlockPlantGrowingRebirth)&&!(block.Block is BlockWildCropPickupRebirth)&&!(block.Block is BlockFarmPlotRebirth))return null;
        var id=Guid.NewGuid();
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write((byte)1);writer.Write(id.ToByteArray());writer.Write(checked((ushort)block.type));writer.Write(checked((byte)block.meta));writer.Write(checked((byte)block.meta2));writer.Write(checked((byte)block.rotation));writer.Flush();
            string key=SourceKey(local);chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=stream.ToArray()});chunk.isModified=true;
        }
        return id.ToString("N");
    }
    internal static bool MatchesSource(WorldBase world,Vector3i position,string incarnation)=>!string.IsNullOrEmpty(incarnation)&&ReadSource(world,position,out _)==incarnation;
    internal static bool BindDestination(World world,Vector3i position,Guid operation,BlockValue expected)
    {
        if(operation==Guid.Empty||world==null||!ReferenceEquals(GameManager.Instance?.World,world)||world.IsRemote()||!ThreadManager.IsMainThread()||world.GetBlock(position).rawData!=expected.rawData||!SourceChunk(world,position,out var chunk,out var local))return false;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write((byte)1);writer.Write(operation.ToByteArray());writer.Write(checked((ushort)expected.type));writer.Write(checked((byte)expected.meta));writer.Write(checked((byte)expected.meta2));writer.Write(checked((byte)expected.rotation));writer.Flush();
            string key=SourceKey(local);chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=stream.ToArray()});chunk.isModified=true;
        }
        return true;
    }
    internal static bool RecordCommit(World world,Vector3i position,Guid operation,Guid source,uint original,uint destination)
    {
        if(operation==Guid.Empty||source==Guid.Empty||!ReferenceEquals(GameManager.Instance?.World,world)||world.IsRemote()||!ThreadManager.IsMainThread()||world.GetBlock(position).rawData!=destination||!SourceChunk(world,position,out var chunk,out _))return false;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {writer.Write(operation.ToByteArray());writer.Write(source.ToByteArray());writer.Write(original);writer.Write(destination);writer.Flush();string key=CommitPrefix+operation.ToString("N");chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=stream.ToArray()});chunk.isModified=true;}
        return true;
    }
    internal static bool HasCommit(World world,Vector3i position,Guid operation,Guid source,uint original,uint destination)
    {
        // Serializer observation runs while our native InProgressSaving flag is
        // set. The immutable commit marker may be read during that save.
        var chunk=world?.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
        if(chunk==null||chunk.InProgressUnloading||!chunk.ChunkCustomData.dict.TryGetValue(CommitPrefix+operation.ToString("N"),out var marker)||marker?.data?.Length!=40)return false;
        using(var stream=new MemoryStream(marker.data,false))using(var reader=new BinaryReader(stream))return new Guid(reader.ReadBytes(16))==operation&&new Guid(reader.ReadBytes(16))==source&&reader.ReadUInt32()==original&&reader.ReadUInt32()==destination;
    }
    internal static void SourceRecorded(World world,Vector3i position,Guid operation)
    {if(!ReferenceEquals(GameManager.Instance?.World,world)||world.IsRemote()||!SourceChunk(world,position,out var chunk,out _))return;string key=CommitPrefix+operation.ToString("N");if(chunk.ChunkCustomData.dict.ContainsKey(key)){chunk.ChunkCustomData.Remove(key);chunk.isModified=true;}}
    internal static void SourceCommitted(Chunk chunk,WorldBase world,Vector3i local,SourceBefore before)
    {
        if(chunk==null||world==null||before==null)return;
        var after=chunk.GetBlock(local);
        string key=SourceKey(local);
        if(before.Block.Block is not BlockPlantGrowingRebirth&&before.Block.Block is not BlockWildCropPickupRebirth&&after.Block is not BlockPlantGrowingRebirth&&after.Block is not BlockWildCropPickupRebirth&&!chunk.ChunkCustomData.dict.ContainsKey(key))return;
        // Damage alone is the same physical stage. Replant/growth, replacement,
        // orientation change and replacement tile rotate identity. Unload does not.
        if(SameSourceStage(before.Block,after)&&ReferenceEquals(before.Tile,chunk.GetTileEntity(local)))return;
        AdvancedFarmingHarvestProtocol.SourceChanged(world,new Vector3i(chunk.X*16+local.x,local.y,chunk.Z*16+local.z));
        if(world.IsRemote())return;
        if(chunk.ChunkCustomData.dict.ContainsKey(key)){chunk.ChunkCustomData.Remove(key);chunk.isModified=true;}
    }
    internal static void InstallSourceIdentity(Harmony harmony)=>RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(AdvancedFarmingHarvestSourceCommit));
}

[HarmonyPatch(typeof(Chunk),"SetBlock",new[]{typeof(WorldBase),typeof(int),typeof(int),typeof(int),typeof(BlockValue),typeof(bool),typeof(bool),typeof(bool),typeof(bool),typeof(int)})]
internal static class AdvancedFarmingHarvestSourceCommit
{
    private static void Prefix(Chunk __instance,WorldBase __0,int __1,int __2,int __3,out AdvancedFarmingHarvestSettlement.SourceBefore __state)
    {var local=new Vector3i(__1,__2,__3);__state=new AdvancedFarmingHarvestSettlement.SourceBefore{Block=__instance.GetBlock(local),Tile=__instance.GetTileEntity(local)};__state.NativeMutation=AdvancedFarmingHarvestSettlement.EnterNativeMutation(__instance,__0,local,__state);}
    private static void Postfix(Chunk __instance,WorldBase __0,int __1,int __2,int __3,bool __runOriginal,AdvancedFarmingHarvestSettlement.SourceBefore __state)
    {var local=new Vector3i(__1,__2,__3);AdvancedFarmingHarvestSettlement.ObserveNativeMutation(__instance,__0,local,__runOriginal,__state.NativeMutation);AdvancedFarmingHarvestSettlement.SourceCommitted(__instance,__0,local,__state);}
    private static Exception Finalizer(Chunk __instance,WorldBase __0,int __1,int __2,int __3,AdvancedFarmingHarvestSettlement.SourceBefore __state,Exception __exception)
    {
        if(__exception!=null)try{AdvancedFarmingHarvestSettlement.SourceCommitted(__instance,__0,new Vector3i(__1,__2,__3),__state);}catch(Exception error){Log.Warning("[REBIRTH Farming] original harvest source invalidation retained: "+error.GetType().Name);}
        return __exception;
    }
}
