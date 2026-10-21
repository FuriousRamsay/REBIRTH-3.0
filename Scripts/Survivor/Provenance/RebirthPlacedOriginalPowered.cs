using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// Read-only, demand-scoped exact native tile serialization. It is not payment
// authority; the placement journal and same-snapshot region publication own that.
internal static class RebirthPlacedOriginalPowered
{
    [ThreadStatic] internal static bool Reading;
    internal static byte[] Image(TileEntity tile)
    {
        if(tile==null)return null;
        bool previous=Reading;try{Reading=true;using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);tile.write(writer,StreamModeWrite.Persistency);writer.Flush();return stream.Length>0&&stream.Length<=256*1024?stream.ToArray():null;}}catch{return null;}finally{Reading=previous;}
    }
    internal static bool CompleteFreshSwitch(World world,RebirthPlacedOriginal.Record r,PlatformUserIdentifierAbs owner)
    {
        var block=world.GetBlock(r.Position).Block;if(block?.GetType()!=typeof(BlockSwitch))return true;
        if(r.Phase!=3||owner==null||RebirthPlacedOriginal.HostSourceBirth(world,r.Position)!=r.Operation.ToString("N")||world.GetTileEntity(r.Position)!=null||!PowerManager.HasInstance||PowerManager.Instance.PowerItemDictionary.ContainsKey(r.Position))return false;
        var chunk=world.GetChunkSync(World.toChunkXZ(r.X),World.toChunkXZ(r.Z)) as Chunk;if(chunk==null||chunk.InProgressUnloading)return false;TileEntityPowered tile=null;
        try
        {
            tile=((BlockSwitch)block).CreateTileEntity(chunk);if(!(tile is TileEntityPoweredTrigger trigger))return false;
            tile.SetDisableModifiedCheck(true);tile.localChunkPos=World.toBlock(r.Position);tile.IsPlayerPlaced=true;trigger.SetOwner(owner);tile.InitializePowerData();chunk.AddTileEntity(tile);tile.SetDisableModifiedCheck(false);tile.SetModified();return ReferenceEquals(world.GetTileEntity(r.Position),tile);
        }
        catch{return false;}
        finally{tile?.SetDisableModifiedCheck(false);}
    }
    internal static bool Current(World world,Vector3i position,TileEntity tile,byte[] image)
        =>tile!=null&&image!=null&&ReferenceEquals(world.GetTileEntity(position),tile)&&ReferenceEquals(tile.chunk,world.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)))&&image.SequenceEqual(Image(tile)??Array.Empty<byte>());
}
[HarmonyPatch]
internal static class RebirthPlacedOriginalPoweredSerialized
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var type in typeof(TileEntityPowered).Assembly.GetTypes())
        {if(!typeof(TileEntityPowered).IsAssignableFrom(type)&&!typeof(TileEntityLight).IsAssignableFrom(type))continue;var method=AccessTools.DeclaredMethod(type,nameof(TileEntity.write),new[]{typeof(PooledBinaryWriter),typeof(StreamModeWrite)});if(method!=null)yield return method;}
    }
    private static void Prefix(TileEntity __instance,PooledBinaryWriter __0,StreamModeWrite __1,MethodBase __originalMethod,out long __state)
    {__state=-1;if(RebirthPlacedOriginalPowered.Reading||__1!=StreamModeWrite.Persistency)return;long start=RebirthStationSnapshotEvidence.StartStationSpan(__instance,__0.BaseStream);if(start<0)return;if(__instance.GetType().GetMethod(nameof(TileEntity.write),new[]{typeof(PooledBinaryWriter),typeof(StreamModeWrite)}).DeclaringType==__originalMethod.DeclaringType)__state=start;}
    private static void Postfix(TileEntity __instance,PooledBinaryWriter __0,long __state)
    {if(__state<0)return;RebirthStationSnapshotEvidence.CaptureStationSpan(__instance,__0.BaseStream,__state,true);RebirthStationSnapshotEvidence.CaptureStationSpan(__instance,__0.BaseStream,__state,false);}
}
