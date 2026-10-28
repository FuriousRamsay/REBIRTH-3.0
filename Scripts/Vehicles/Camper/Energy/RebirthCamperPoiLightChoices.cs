using System;
using System.Collections.Generic;

internal enum RebirthCamperPoiLightChoice:byte {Automatic=0,On=1,Off=2}

// Physical light choices are shared by every camper. The existing eight-byte
// provenance marker remains unchanged; this separate record binds its stamp.
internal static class RebirthCamperPoiLightChoices
{
    private const string Prefix="rebirth.poi.light.choice.v1:";
    private static string Key(Vector3i p)=>Prefix+p.x+":"+p.y+":"+p.z;
    private static bool Current(World world,TileEntity tile,out Chunk chunk,out BlockValue block,out uint stamp)
    {
        chunk=null;block=default(BlockValue);stamp=0;
        if(world==null||tile==null||!ReferenceEquals(GameManager.Instance?.World,world)||tile.chunk==null)return false;
        chunk=tile.chunk;var position=tile.ToWorldPos();
        if(!ReferenceEquals(world.GetTileEntity(position),tile)||!ReferenceEquals(chunk.GetTileEntity(tile.localChunkPos),tile)||
            !ReferenceEquals(world.ChunkCache?.GetChunkSync(World.toChunkXZ(position.x),0,World.toChunkXZ(position.z)),chunk))return false;
        block=world.GetBlock(position);
        if(block.ischild||!(block.Block is BlockLight||block.Block is BlockPoweredLight))return false;
        stamp=RebirthCamperWorldLighting.Stamp(tile);return stamp!=0;
    }
    internal static RebirthCamperPoiLightChoice Read(World world,TileEntity tile)
    {
        if(!Current(world,tile,out var chunk,out var block,out uint stamp)||
            !chunk.ChunkCustomData.dict.TryGetValue(Key(tile.localChunkPos),out var record))return RebirthCamperPoiLightChoice.Automatic;
        var data=record?.data;
        if(data==null||data.Length!=9||data[0]!=1||data[1]<1||data[1]>2||
            (data[2]|data[3]<<8)!=block.type||data[4]!=block.rotation||BitConverter.ToUInt32(data,5)!=stamp)
            return RebirthCamperPoiLightChoice.Automatic;
        return (RebirthCamperPoiLightChoice)data[1];
    }
    // Caller owns actor and claim authorization; this method owns incarnation
    // checks and the server-side native chunk publication only.
    internal static bool Set(World world,TileEntity tile,bool on)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
            !Current(world,tile,out var chunk,out var block,out uint stamp))return false;
        var mode=on?RebirthCamperPoiLightChoice.On:RebirthCamperPoiLightChoice.Off;
        if(Read(world,tile)==mode)return true;
        var data=new byte[9];data[0]=1;data[1]=(byte)mode;data[2]=(byte)block.type;data[3]=(byte)(block.type>>8);data[4]=block.rotation;
        Array.Copy(BitConverter.GetBytes(stamp),0,data,5,4);
        string key=Key(tile.localChunkPos);
        chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=data});
        chunk.isModified=true;RebirthCamperWorldLighting.InvalidateDiscovery();return true;
    }
    internal static void Clear(Chunk chunk,Vector3i local)
    {
        if(!ThreadManager.IsMainThread()||chunk==null||!chunk.ChunkCustomData.dict.ContainsKey(Key(local)))return;
        chunk.ChunkCustomData.Remove(Key(local));
        chunk.isModified=true;RebirthCamperWorldLighting.InvalidateDiscovery();
    }
    internal sealed class Plan
    {
        internal readonly HashSet<Vector3i> Selected=new HashSet<Vector3i>();
        internal int PinnedOnLights,SelectableLights,SupportedLights;
    }
    internal static Plan Select(IEnumerable<Vector3i> existingRanked,Func<Vector3i,RebirthCamperPoiLightChoice> choice,int requested)
    {
        if(existingRanked==null)throw new ArgumentNullException(nameof(existingRanked));
        if(choice==null)throw new ArgumentNullException(nameof(choice));
        var result=new Plan();var seen=new HashSet<Vector3i>();var automatic=new List<Vector3i>();
        foreach(var position in existingRanked)
        {
            if(!seen.Add(position))continue;
            result.SupportedLights++;
            var selected=choice(position);
            if(selected==RebirthCamperPoiLightChoice.Off)continue;
            result.SelectableLights++;
            if(selected==RebirthCamperPoiLightChoice.On){result.Selected.Add(position);result.PinnedOnLights++;}
            else automatic.Add(position);
        }
        int remaining=Math.Max(0,requested)-result.PinnedOnLights;
        for(int i=0;i<automatic.Count&&i<remaining;i++)result.Selected.Add(automatic[i]);
        return result;
    }
}
