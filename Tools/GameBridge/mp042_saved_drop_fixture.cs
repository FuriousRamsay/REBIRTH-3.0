// AUTHORED_NOT_RUN. Complete production native-save adapter. Native codecs,
// chunk scheduling and physical region publication remain explicit adapters.
using System;
using System.IO;
using System.Threading;
class ThreadManager {internal static bool IsMainThread()=>true;}
class ItemStack {internal string Rich="rich";internal int count=3;internal bool IsEmpty()=>count==0;}
class Entity {internal World world;internal int entityId;}
class EntityPlayer:Entity {internal int Died=2;internal string Creation="creation";internal bool IsSpawned()=>true;internal bool IsDead()=>false;}
class EntityPlayerLocal:EntityPlayer {}
class EntityItem:Entity {internal int entityClass=9,belongsPlayerId=1;internal bool addedToChunk=true,Unloaded;internal Coords chunkPosAddedEntityTo=new Coords();internal ItemStack itemStack=new ItemStack();internal bool IsMarkedForUnload()=>Unloaded;}
class Coords {internal int x,z;}
class Chunk {internal int X,Z;internal bool InProgressUnloading,IsLocked,InProgressSaving,NeedsDecoration,InProgressDecorating,NeedsLightCalculation,InProgressLighting;}
class World {internal EntityPlayer Primary;internal Chunk Chunk=new Chunk();internal Cache ChunkCache=new Cache();internal System.Collections.Generic.Dictionary<int,Entity> Entities=new System.Collections.Generic.Dictionary<int,Entity>();internal bool IsRemote()=>false;internal Entity GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;internal EntityPlayer GetPrimaryPlayer()=>Primary;internal object GetChunkSync(int x,int z)=>Chunk;}
class Cache {internal object ChunkProvider=new ChunkProviderGenerateWorld();}
class ChunkProviderGenerateWorld {internal RegionFileManager m_RegionFileManager=new RegionFileManager();}
class RegionFileAccessSectorBased {}
class RegionFileManager {internal object regionFileAccess=new RegionFileAccessSectorBased(),saveLock=new object();internal string saveDirectory=Path.Combine(Path.GetTempPath(),"rebirth-saved-drop-native-adapter");internal int Calls;internal void SaveChunkSnapshot(Chunk chunk,bool force){Calls++;Model.DuringCapture?.Invoke();if(Model.ThrowSave)throw new IOException("native save exception");RebirthStationSnapshotEvidence.Captured=true;try{RebirthStationSnapshotEvidence.Active?.Capture?.Invoke(new RebirthStationSnapshotEvidence.EntityImage());}catch{}}}
static class RegionFile {internal static string ConstructFullFilePath(string root,int x,int z,string extension)=>Path.Combine(root,"r."+x+"."+z+"."+extension);}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer p)=>p.Creation;}
static class RebirthStationGridIngredients {internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.Rich==b.Rich&&a.count==b.count;}
static class RemoteResourceTransactions {internal static byte[] CaptureOriginalNativeFrame(ItemStack[] a,ref long remaining)=>System.Text.Encoding.UTF8.GetBytes(a[0].Rich+"|"+a[0].count);}
static class RebirthCamperInventorySnapshot {internal static ItemStack[] Restore(byte[] frame){var a=System.Text.Encoding.UTF8.GetString(frame).Split('|');return new[]{new ItemStack{Rich=a[0],count=int.Parse(a[1])}};}}
enum StreamModeWrite {Persistency}
sealed class NativeBlob:MemoryStream {internal static int Live;bool disposed;internal NativeBlob(){Live++;}protected override void Dispose(bool managed){if(!disposed){Live--;disposed=true;}base.Dispose(managed);}}
class EntityCreationData {internal NativeBlob entityData=new NativeBlob(),bagData=new NativeBlob();readonly EntityItem item;internal EntityCreationData(EntityItem entity,StreamModeWrite mode){item=entity;}internal void write(PooledBinaryWriter writer,StreamModeWrite mode){if(Model.ThrowCodec)throw new IOException("native codec exception");writer.Stream.WriteByte((byte)item.entityId);writer.Stream.WriteByte((byte)item.itemStack.count);}}
class PooledBinaryWriter:IDisposable {internal Stream Stream;internal void SetBaseStream(Stream stream){Stream=stream;}internal void Flush(){Stream.Flush();}public void Dispose(){}}
static class MemoryPools {internal static Pool poolBinaryWriter=new Pool();internal class Pool {internal PooledBinaryWriter AllocSync(bool reset)=>new PooledBinaryWriter();}}
static class RebirthLogisticsParcelBaselines {internal static bool CanAdmitNativeSnapshot(RegionFileManager r)=>Model.QueueRoom;internal static bool NativeQueueFits(RegionFileManager r)=>Model.QueueRoom;}
public interface IRebirthStationSnapshotByteBudget {int MaximumSnapshotBytes{get;}bool TryReserveAllocation(long n,out IDisposable allocation);}
class Budget:IRebirthStationSnapshotByteBudget {internal long Live;internal bool Available=true;public int MaximumSnapshotBytes=>8*1024*1024;sealed class Hold:IDisposable {internal Budget B;internal long N;public void Dispose(){if(B!=null){B.Live-=N;B=null;}}}public bool TryReserveAllocation(long n,out IDisposable allocation){allocation=null;if(!Available)return false;Live+=n;allocation=new Hold{B=this,N=n};return true;}}
static class RebirthStationSnapshotEvidence {
    internal static bool Captured,Published;internal static Request Active;
    internal sealed class PublishedSnapshot {}
    internal sealed class EntityImage {}
    internal sealed class Request:IDisposable {internal Func<bool> Current;internal Action<EntityImage> Capture;internal IDisposable Allocation;internal bool Closed;public void Dispose(){Closed=true;Allocation?.Dispose();Allocation=null;}}
    internal static Request WatchEntity(object w,object c,int x,int z,int id,int type,byte[] frame,Func<bool> current,IRebirthStationSnapshotByteBudget budget,Action<EntityImage> captured=null){if(!budget.TryReserveAllocation(frame.Length,out var allocation))return null;return Active=new Request{Current=current,Allocation=allocation,Capture=captured};}
    internal static bool TryGetSerializedEntity(Request request,out EntityImage image){image=null;if(!Captured||request==null||request.Closed||!request.Current())return false;image=new EntityImage();return true;}
    internal static bool TryGetPublishedEntity(Request request,out PublishedSnapshot proof){proof=Captured&&Published&&request!=null&&!request.Closed&&request.Current()?new PublishedSnapshot():null;return proof!=null;}
}
static class Model {internal static bool ThrowCodec,ThrowSave,QueueRoom=true;internal static Action DuringCapture;}
// ACTUAL_SAVED_DROP
class Program {
    static int assertions;static void Need(bool value,string label){assertions++;if(!value)throw new Exception(label);}
    static void Main(){
        for(int variant=0;variant<12;variant++){
            Model.ThrowCodec=Model.ThrowSave=false;Model.QueueRoom=true;Model.DuringCapture=null;RebirthStationSnapshotEvidence.Captured=RebirthStationSnapshotEvidence.Published=false;
            var world=new World();var actor=new EntityPlayerLocal{world=world,entityId=1};world.Primary=actor;world.Entities[1]=actor;
            var item=new EntityItem{world=world,entityId=20};world.Entities[20]=item;GameManager.Instance.World=world;var budget=new Budget();bool current=true;
            var regions=((ChunkProviderGenerateWorld)world.ChunkCache.ChunkProvider).m_RegionFileManager;
            if(variant==1)item.itemStack.Rich="foreign";if(variant==2)item.belongsPlayerId=99;if(variant==3)item.addedToChunk=false;
            if(variant==4)world.Chunk.InProgressSaving=true;if(variant==5)Model.QueueRoom=false;if(variant==6)budget.Available=false;
            if(variant==7)Model.ThrowCodec=true;if(variant==8)Model.ThrowSave=true;if(variant==9)Model.DuringCapture=()=>actor.Died++;
            if(variant==10)regions.regionFileAccess=new object();if(variant==11)current=false;
            using(var observation=RebirthCookingSavedDropObservation.Begin(actor,item,new ItemStack(),()=>current,budget)){
                Need((observation!=null)==(variant==0),"native admission/recovery shape "+variant);
                if(observation!=null){Need(!observation.Saved(out _),"serialization alone accepted as saved drop");RebirthStationSnapshotEvidence.Published=true;Need(observation.Saved(out _),"positive native publication unavailable");var copy=observation.CopyNativeFrame();copy[0]=0;Need(observation.CopyNativeFrame()[0]==20,"caller mutated retained native ECD frame");current=false;Need(!observation.Saved(out _),"revoked original exposed saved proof");}
            }
            Need(budget.Live==0&&NativeBlob.Live==0,"native evidence allocation/blob leaked "+variant);
            if(variant!=4)Need(!world.Chunk.InProgressSaving,"native save flag not restored "+variant);
            if(variant==8)Need(regions.Calls==1,"native save exception fixture did not enter actual save call");
        }
        Console.WriteLine("MP042 actual saved drop adapter assertions="+assertions+"; physical region and exact native target NOT RUN");
    }
}
