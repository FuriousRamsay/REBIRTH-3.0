using System;using System.Text;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;
// Adapters: prior actual insertion/saved initial receipt validation, engine object model,
// native read fence and accounting. Actual callback bodies retain all stream,
// factory/read/return/identity/exception decisions. No fake missing authority.
enum StreamModeRead{Persistency,FromClient}enum TileEntityType{Workstation,Other}
struct Vector3i{public static bool operator ==(Vector3i a,Vector3i b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator !=(Vector3i a,Vector3i b)=>!(a==b);internal int x,y,z;internal Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
class PooledBinaryReader{internal Stream BaseStream;}
class ChunkCustomData{internal object TriggerWriteDataDelegate;internal bool isSavedToNetwork;internal ulong expiresInWorldTime=ulong.MaxValue;internal byte[] data=new byte[]{9};}
class CustomData{internal Dictionary<string,ChunkCustomData> dict=new Dictionary<string,ChunkCustomData>();}
class Chunk{internal int X,Z;internal CustomData ChunkCustomData=new CustomData();internal TileEntityWorkstation Tile;internal TileEntity GetTileEntity(Vector3i p)=>Tile;}
class TileEntity{internal Chunk Chunk;internal Chunk GetChunk()=>Chunk;internal Vector3i localChunkPos;internal Vector3i Position=new Vector3i(0,1,0);internal Vector3i ToWorldPos()=>Position;}
class RecipeQueueItem{internal object Recipe=new object();internal int Multiplier=2;}
class TileEntityWorkstation:TileEntity{internal RecipeQueueItem[] Queue=new[]{new RecipeQueueItem()};}
class RegionFileAccessAbstract{}
class RegionFileChunkReader{internal RegionFileAccessAbstract regionFileAccess;internal PooledBinaryReader loadChunkReader;internal Stream loadChunkMemoryStream;}
class ChunkSnapshotUtil{internal RegionFileChunkReader chunkReader;}
static class WorldChunkCache{internal static int extractX(long k)=>0;internal static int extractZ(long k)=>0;}
static class RebirthProductiveEarnedUiDeferredTransport{internal static bool TryGetObservedNativeReadFence(TileEntityWorkstation t,out long f){f=17;return true;}}
static partial class RebirthProductiveEarnedUiQueueOwner{internal static string NativeBinding(RecipeQueueItem r)=>"actual-row";}
static class RebirthProductiveEarnedObservationBudget{internal static bool TryResizeDetached(IDisposable m,long n)=>true;}
static partial class RebirthProductiveEarnedUiQueueOwner
{
    sealed class InitialSavedNative{internal Vector3i Position=new Vector3i(0,1,0);internal long Offset=16;internal int SnapshotLength=32,TileLength=4;internal string SnapshotDigest;}
    sealed class InitialNativeOriginal{internal bool Active=true,Installed;internal int Loads,Slot,OriginalCount=2;internal string ReceiptPath="",OriginalPath="",Save="",Owner="",Creation="";internal string Region,MarkerKey="marker",RowBinding,RowBindingDigest=InitialHash(Encoding.UTF8.GetBytes("actual-row"));internal byte[] MarkerImage=new byte[]{9};
        internal ChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;internal InitialSavedNative Native=new InitialSavedNative();internal Chunk Chunk;internal TileEntityWorkstation Tile;internal RecipeQueueItem Row;internal ChunkCustomData Marker;internal long Fence;internal IDisposable Memory;
        internal bool LoadCurrent()=>Active;internal void Release(){} }
    sealed class InitialNativeLoading{internal InitialNativeOriginal Original;internal PooledBinaryReader Reader;internal Chunk Chunk;internal TileEntityWorkstation Tile;internal bool Stream,Factory,Returned,Instantiated;internal int Reads;internal long Fence;}
    static readonly object initialLoadGate=new object();static readonly List<InitialNativeOriginal> initialNativeOriginals=new List<InitialNativeOriginal>();
    [ThreadStatic]static InitialNativeLoading[] initialLoading;[ThreadStatic]static InitialNativeLoading initialInstantiating;
    static bool InitialReceiptCurrent(InitialNativeOriginal s)=>s.Active;
    static string InitialHash(byte[] bytes){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));}
    static bool OrdinaryInitial(RecipeQueueItem row)=>row?.Recipe!=null;
    // ACTUAL_CALLBACKS
    sealed class Flow{internal InitialNativeOriginal Source;internal PooledBinaryReader Reader;internal Chunk Chunk;internal TileEntityWorkstation Tile;internal object Load,Factory,Read;}
    static Flow Start(int fault)
    {
        initialNativeOriginals.Clear();initialLoading=null;initialInstantiating=null;
        byte[] bytes=Enumerable.Range(0,24).Select(i=>(byte)i).ToArray();var stream=new MemoryStream(bytes,false);var reader=new PooledBinaryReader{BaseStream=stream};
        var access=new RegionFileAccessAbstract();var nativeReader=new RegionFileChunkReader{regionFileAccess=access,loadChunkReader=reader,loadChunkMemoryStream=stream};
        var s=new InitialNativeOriginal{Region=Path.GetFullPath("synthetic-region"),Access=access,Util=new ChunkSnapshotUtil{chunkReader=nativeReader}};
        using(var sha=SHA256.Create())s.Native.SnapshotDigest=Convert.ToBase64String(sha.ComputeHash(bytes));if(fault==1)s.Native.SnapshotDigest="changed";
        initialNativeOriginals.Add(s);var chunk=new Chunk();var tile=new TileEntityWorkstation{Chunk=chunk};chunk.Tile=tile;chunk.ChunkCustomData.dict["marker"]=new ChunkCustomData();
        if(fault==12)tile.Position=new Vector3i(0,2,0);if(fault==13)s.RowBindingDigest="changed";if(fault==14)s.OriginalCount=1;var f=new Flow{Source=s,Reader=reader,Chunk=chunk,Tile=tile};f.Load=EnterInitialNativeLoad(s.Util,s.Region,0,true);
        ObserveInitialNativeLoadStream(nativeReader,reader,fault==2?s.Region+"-wrong":s.Region,0,0,"7rg",47,true);
        reader.BaseStream.Position=fault==3?9:8;f.Factory=EnterInitialNativeInstantiate(reader,StreamModeRead.Persistency,TileEntityType.Workstation,chunk,fault!=4);
        f.Read=EnterInitialPersistencyRead(tile,reader,StreamModeRead.Persistency,fault!=5);
        if(fault==6)EnterInitialPersistencyRead(tile,reader,StreamModeRead.Persistency,true); // Duplicate cannot create a second read.
        reader.BaseStream.Position=fault==7?13:12;ObserveInitialPersistencyRead(reader,true,f.Read);
        ObserveInitialNativeInstantiate(fault==8?new TileEntityWorkstation{Chunk=chunk}:tile,reader,chunk,true,f.Factory);
        EndInitialNativeInstantiate(f.Factory,fault==9?new Exception("native"):null);
        if(fault==10)chunk.ChunkCustomData.dict["marker"].data=new byte[]{8};
        reader.BaseStream.Position=24;ObserveInitialNativeLoad(fault==11?new Chunk():chunk,true,f.Load);return f;
    }
    static void Check(bool value){if(!value)throw new Exception("Native Initial lineage assertion failed");}
    static void Main()
    {
        var positive=Start(0);Check(positive.Source.Installed&&ReferenceEquals(positive.Source.Tile,positive.Tile)&&ReferenceEquals(positive.Source.Row,positive.Tile.Queue[0])&&positive.Source.Fence==17);
        EndInitialNativeLoad(positive.Load);Check(positive.Source.Loads==0&&initialLoading==null&&initialInstantiating==null);
        foreach(int fault in new[]{1,2,3,4,5,7,8,9,10,11,12,13,14}){var f=Start(fault);Check(!f.Source.Installed);EndInitialNativeLoad(f.Load);}
        var duplicate=Start(6);Check(duplicate.Source.Installed);EndInitialNativeLoad(duplicate.Load);
        var fresh=Start(0);EndInitialNativeLoad(fresh.Load);var again=EnterInitialNativeLoad(fresh.Source.Util,fresh.Source.Region,0,true);Check(again!=null&&!fresh.Source.Installed&&fresh.Source.Row==null);EndInitialNativeLoad(again);
        var outer=Start(0);EndInitialNativeLoad(outer.Load,new Exception("outer native load"));Check(!outer.Source.Installed);
        Console.WriteLine("18 native initial queue callback assertions passed");
    }
}
