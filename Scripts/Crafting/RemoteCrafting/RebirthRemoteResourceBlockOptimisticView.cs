using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;

// Client view evidence only. No inventory, payment, host target or saved-world
// authority is derived from this transient original optimistic-view witness.
internal static class RebirthRemoteResourceBlockOptimisticView
{
    private sealed class ReferenceComparer:IEqualityComparer<object>
    {internal static readonly ReferenceComparer Instance=new ReferenceComparer();public new bool Equals(object a,object b)=>ReferenceEquals(a,b);public int GetHashCode(object value)=>RuntimeHelpers.GetHashCode(value);}
    private sealed class Watch
    {internal RebirthRemoteResourceClientOriginal.Intent Original;internal RebirthRemoteResourceBlockNativeCapture.Scope Actual;internal World World;internal INetConnection Connection;internal string Directory;internal volatile bool Valid=true;internal bool Finalized;internal Cell[] Cells;}
    private sealed class Cell
    {
        internal Watch Watch;internal Vector3i Position;internal Chunk Chunk;internal ChunkBlockLayer Layer;internal ChunkBlockChannel Damage,Density,Texture;
        internal uint Raw;internal int DamageValue;internal sbyte DensityValue;internal long Paint;
        internal int X=>Position.x&15;internal int Z=>Position.z&15;internal int Offset=>X+(Z<<4)+(Position.y&3)*256;
    }
    private static readonly object Gate=new object();private static readonly Dictionary<Guid,Watch> watches=new Dictionary<Guid,Watch>();
    private static readonly Dictionary<object,List<Cell>> index=new Dictionary<object,List<Cell>>(ReferenceComparer.Instance);
    private static int count;private static bool installed;private static World world;private static string directory;
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.remote.resources.original.block.view.v1");
        foreach(var type in new[]{typeof(RebirthRemoteBlockViewChunkCell),typeof(RebirthRemoteBlockViewChunkWhole),typeof(RebirthRemoteBlockViewChannelCell),
            typeof(RebirthRemoteBlockViewChannelWhole),typeof(RebirthRemoteBlockViewChannelIndexed),typeof(RebirthRemoteBlockViewLayerCell),typeof(RebirthRemoteBlockViewLayerWhole)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        installed=true;
    }
    private static bool Ready(Chunk chunk)=>chunk!=null&&!chunk.InProgressUnloading&&!chunk.InProgressNetworking&&!chunk.InProgressRegeneration&&!chunk.InProgressCopying&&!chunk.InProgressDecorating&&!chunk.InProgressWaterSim;
    private static void Bind(World current)
    {
        string path=GameIO.GetSaveGameDir();if(ReferenceEquals(world,current)&&directory==path)return;
        watches.Clear();index.Clear();Volatile.Write(ref count,0);world=current;directory=path;
    }
    private static void Add(object key,Cell cell)
    {if(key==null)return;if(!index.TryGetValue(key,out var cells))index.Add(key,cells=new List<Cell>());if(!cells.Contains(cell))cells.Add(cell);}
    private static void Remove(Watch watch)
    {
        watches.Remove(watch.Original.Operation);
        foreach(var pair in index.ToArray()){pair.Value.RemoveAll(cell=>ReferenceEquals(cell.Watch,watch));if(pair.Value.Count==0)index.Remove(pair.Key);}
        Volatile.Write(ref count,watches.Count);
    }
    internal static void Retire(RebirthRemoteResourceClientOriginal.Intent original)
    {lock(Gate){if(watches.TryGetValue(original.Operation,out var watch)&&watch.Original.Digest==original.Digest)Remove(watch);}}
    internal static void Tick(World current)
    {
        if(Volatile.Read(ref count)==0)return;
        INetConnection connection=null;bool connected=false;
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;connection=manager?.connectionToServer?[0];
            connected=manager?.IsConnected==true&&connection!=null&&!connection.IsDisconnected();
        }
        catch{/* A disposed/replaced transport is unknown view continuity, never an update-loop failure. */}
        lock(Gate)
        {
            Bind(current);
            foreach(var watch in watches.Values)if(!connected||!ReferenceEquals(connection,watch.Connection))watch.Valid=false;
        }
    }
    internal static bool Capture(RebirthRemoteResourceBlockNativeCapture.Scope actual)
    {
        try
        {
            if(!installed||!ThreadManager.IsMainThread()||actual?.Invoked!=true||!actual.TryGet(out var command,out _)||
                !RebirthRemoteResourceClientOriginal.Current(actual.Player,actual.Original)||!RebirthRemoteResourceBlockProjection.TryCoordinates(actual.Original,command,out var positions))return false;
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0];if(connection==null||connection.IsDisconnected())return false;
            var watch=new Watch{Original=actual.Original,Actual=actual,World=actual.World,Connection=connection,Directory=GameIO.GetSaveGameDir()};
            if(!ReadCells(watch,positions,out var cells))return false;watch.Cells=cells;
            lock(Gate)
            {
                Bind(actual.World);
                if(watches.ContainsKey(actual.Original.Operation))return false;
                foreach(var stale in watches.Values.Where(value=>!value.Valid).ToArray())Remove(stale);
                if(watches.Count>=48)return false; // 32 financial originals plus 16 terminal view retries.
                watches.Add(actual.Original.Operation,watch);
                foreach(var cell in cells){Add(cell.Chunk,cell);Add(cell.Layer,cell);Add(cell.Damage,cell);Add(cell.Density,cell);Add(cell.Texture,cell);}
                Volatile.Write(ref count,watches.Count);return true;
            }
        }
        catch{return false;}
    }
    private static bool ReadCells(Watch watch,Vector3i[] positions,out Cell[] cells)
    {
        cells=null;var result=new List<Cell>();
        foreach(var position in positions)
        {
            var chunk=watch.World.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
            if(!Ready(chunk)||chunk.X!=World.toChunkXZ(position.x)||chunk.Z!=World.toChunkXZ(position.z)||chunk.m_BlockLayers?.Length!=64||chunk.chnTextures?.Length!=1||chunk.chnDensity==null||chunk.chnDamage==null||chunk.chnTextures[0]==null)return false;
            var block=watch.World.GetBlock(position);
            result.Add(new Cell{Watch=watch,Position=position,Chunk=chunk,Layer=chunk.m_BlockLayers[position.y>>2],Damage=chunk.chnDamage,Density=chunk.chnDensity,Texture=chunk.chnTextures[0],
                Raw=block.rawData,DamageValue=block.damage,DensityValue=watch.World.GetDensity(position),Paint=watch.World.ChunkCache.GetTextureFull(position)});
        }
        cells=result.ToArray();return true;
    }
    internal static bool FinalizeCapture(RebirthRemoteResourceBlockNativeCapture.Scope actual)
    {
        try
        {
            Watch watch;lock(Gate){if(!watches.TryGetValue(actual.Original.Operation,out watch)||!ReferenceEquals(watch.Actual,actual)||!watch.Valid)return false;}
            if(!actual.TryGet(out _,out _)||!ReadCells(watch,watch.Cells.Select(cell=>cell.Position).ToArray(),out var after))return false;
            if(after.Where((cell,i)=>!ReferenceEquals(cell.Chunk,watch.Cells[i].Chunk)||!ReferenceEquals(cell.Damage,watch.Cells[i].Damage)||
                !ReferenceEquals(cell.Density,watch.Cells[i].Density)||!ReferenceEquals(cell.Texture,watch.Cells[i].Texture)).Any()){watch.Valid=false;return false;}
            lock(Gate)
            {
                if(!watch.Valid||!watches.TryGetValue(actual.Original.Operation,out var registered)||!ReferenceEquals(registered,watch))return false;
                Remove(watch);watch.Cells=after;watch.Finalized=true;watches.Add(actual.Original.Operation,watch);
                foreach(var cell in after){Add(cell.Chunk,cell);Add(cell.Layer,cell);Add(cell.Damage,cell);Add(cell.Density,cell);Add(cell.Texture,cell);}Volatile.Write(ref count,watches.Count);
            }
            if(Current(watch))return true;watch.Valid=false;return false;
        }
        catch{return false;}
    }
    private static bool Current(Watch watch)
    {
        try{if(CurrentCore(watch))return true;watch.Valid=false;return false;}catch{watch.Valid=false;return false;}
    }
    private static bool CurrentCore(Watch watch)
    {
        if(!watch.Valid||!watch.Finalized||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,watch.World)||watch.World.IsRemote()!=true||watch.World.worldState.Guid!=watch.Original.World||watch.Directory!=GameIO.GetSaveGameDir()||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],watch.Connection)||watch.Connection.IsDisconnected())return false;
        foreach(var cell in watch.Cells)
        {
            if(!ReferenceEquals(watch.World.GetChunkSync(World.toChunkXZ(cell.Position.x),World.toChunkXZ(cell.Position.z)),cell.Chunk)||!Ready(cell.Chunk)||cell.Chunk.X!=World.toChunkXZ(cell.Position.x)||cell.Chunk.Z!=World.toChunkXZ(cell.Position.z)||
                !ReferenceEquals(cell.Chunk.m_BlockLayers?[cell.Position.y>>2],cell.Layer)||!ReferenceEquals(cell.Chunk.chnDensity,cell.Density)||!ReferenceEquals(cell.Chunk.chnDamage,cell.Damage)||
                cell.Chunk.chnTextures?.Length!=1||!ReferenceEquals(cell.Chunk.chnTextures[0],cell.Texture))return false;
            var block=watch.World.GetBlock(cell.Position);
            if(block.rawData!=cell.Raw||block.damage!=cell.DamageValue||watch.World.GetDensity(cell.Position)!=cell.DensityValue||watch.World.ChunkCache.GetTextureFull(cell.Position)!=cell.Paint)return false;
        }
        return watch.Valid;
    }
    internal static bool Uninterrupted(RebirthRemoteResourceClientOriginal.Intent original)
    {Watch watch;lock(Gate){if(!watches.TryGetValue(original.Operation,out watch)||watch.Original.Digest!=original.Digest)return false;}return Current(watch);}
    internal static bool MatchesProjection(World current,IReadOnlyList<BlockChangeInfo> changes)
    {
        if(!ThreadManager.IsMainThread()||current?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,current))return false;
        foreach(var change in changes)
        {
            var position=change.blockValueRef.BlockPosition;var chunk=current.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
            if(!Ready(chunk))return false;var block=current.GetBlock(position);
            if(block.rawData!=change.blockValue.rawData||block.damage!=change.blockValue.damage||current.GetDensity(position)!=change.density||current.ChunkCache.GetTextureFull(position)!=change.textureFull[0])return false;
        }
        return true;
    }
    internal static void Changed(object native,int x,int y,int z,bool whole=false,int offset=-1)
    {
        if(native==null||Volatile.Read(ref count)==0)return;
        lock(Gate)
        {
            if(!index.TryGetValue(native,out var cells))return;
            foreach(var cell in cells)
            {
                if(!whole&&(offset>=0?cell.Offset!=offset:cell.X!=x||cell.Position.y!=y||cell.Z!=z))continue;
                if(whole||!RebirthRemoteResourceBlockProjection.IsApplyingOriginal(cell.Watch.Original)&&!RebirthRemoteResourceBlockNativeCapture.IsApplyingOptimistic(cell.Watch.Original))cell.Watch.Valid=false;
            }
        }
    }
}
[HarmonyPatch]
internal static class RebirthRemoteBlockViewChunkCell
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(Chunk).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(method=>new[]{"SetBlockRaw","SetDensity","SetDamage","SetTextureFull","GetSetTextureFullArray","SetBlockFaceTexture"}.Contains(method.Name));
    [HarmonyPriority(Priority.First+3)]private static void Prefix(Chunk __instance,int _x,int _y,int _z)=>RebirthRemoteResourceBlockOptimisticView.Changed(__instance,_x,_y,_z);
}
[HarmonyPatch]
internal static class RebirthRemoteBlockViewChunkWhole
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(Chunk).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(method=>new[]{"read","load","Reset","CopyMeshDataFrom"}.Contains(method.Name));
    [HarmonyPriority(Priority.First+3)]private static void Prefix(Chunk __instance)=>RebirthRemoteResourceBlockOptimisticView.Changed(__instance,0,0,0,true);
}
[HarmonyPatch]
internal static class RebirthRemoteBlockViewChannelCell
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(ChunkBlockChannel).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(method=>method.Name=="Set"||method.Name=="GetSet");
    [HarmonyPriority(Priority.First+3)]private static void Prefix(ChunkBlockChannel __instance,int _x,int _y,int _z)=>RebirthRemoteResourceBlockOptimisticView.Changed(__instance,_x,_y,_z);
}
[HarmonyPatch]
internal static class RebirthRemoteBlockViewChannelWhole
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(ChunkBlockChannel).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(method=>new[]{"Read","CopyFrom","FreeLayers","SetHalf","fillSameValue","freeLayer","onLayerRead"}.Contains(method.Name));
    [HarmonyPriority(Priority.First+3)]private static void Prefix(ChunkBlockChannel __instance)=>RebirthRemoteResourceBlockOptimisticView.Changed(__instance,0,0,0,true);
}
[HarmonyPatch(typeof(ChunkBlockChannel),"getSetData")]
internal static class RebirthRemoteBlockViewChannelIndexed
{
    [HarmonyPriority(Priority.First+3)]private static void Prefix(ChunkBlockChannel __instance,int _idx,int _offs)
    {
        int bytes=__instance.bytesPerVal;
        if(bytes<1||_idx<0||_idx%bytes!=0||_offs<0||_offs>=1024){RebirthRemoteResourceBlockOptimisticView.Changed(__instance,0,0,0,true);return;}
        RebirthRemoteResourceBlockOptimisticView.Changed(__instance,_offs&15,(_idx/bytes)*4+(_offs>>8),(_offs>>4)&15);
    }
}
[HarmonyPatch(typeof(ChunkBlockLayer),nameof(ChunkBlockLayer.SetAt),new[]{typeof(int),typeof(uint)})]
internal static class RebirthRemoteBlockViewLayerCell
{
    [HarmonyPriority(Priority.First+3)]private static void Prefix(ChunkBlockLayer __instance,int offs)=>RebirthRemoteResourceBlockOptimisticView.Changed(__instance,0,0,0,false,offs);
}
[HarmonyPatch]
internal static class RebirthRemoteBlockViewLayerWhole
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(ChunkBlockLayer).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(method=>new[]{"Read","Reset","CopyFrom"}.Contains(method.Name));
    [HarmonyPriority(Priority.First+3)]private static void Prefix(ChunkBlockLayer __instance)=>RebirthRemoteResourceBlockOptimisticView.Changed(__instance,0,0,0,true);
}
