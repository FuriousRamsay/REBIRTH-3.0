// Actual RestoreCore + actual journal/durable publisher; native items/world/settings/save observer doubled.
using System;using System.IO;using Newtonsoft.Json;
struct Vector3{public float x,y,z;public float sqrMagnitude=>x*x+y*y+z*z;public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3{x=a.x-b.x,y=a.y-b.y,z=a.z-b.z};}
struct Vector3i{public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}public Vector3 ToVector3()=>new Vector3{x=x,y=y,z=z};}
struct Vector2i{public int x,y;public Vector2i(int a,int b){x=a;y=b;}}
class EntityPlayer{public Vector3 position;}
class Block{public string GetBlockName()=>"box";}
class BlockValue{public Block Block=new Block();public uint rawData=10;}
class World{public class State{public string Guid;}public State worldState=new State();public TileEntityComposite Tile;public bool IsRemote()=>false;public TileEntityComposite GetTileEntity(Vector3i p)=>Tile;public EntityPlayer GetEntity(int id)=>new EntityPlayer();public BlockValue GetBlock(Vector3i p)=>new BlockValue();}
class GameManager{public static GameManager Instance=new GameManager();public World World;}
class ItemStack{public int count;}
class Grid{public Vector2i ContainerSize=new Vector2i(1,1);public ItemStack[] items=new[]{new ItemStack()};public int Resizes;public void Resize(Vector2i size){ContainerSize=size;Resizes++;}}
class TEFeatureStorage{public Grid ItemGrid=new Grid();public bool IsEmpty(){foreach(var i in ItemGrid.items)if(i.count>0)return false;return true;}public void SetModified(){}}
class TEFeatureRebirthContainerName{public string Incarnation=Guid.NewGuid().ToString("N"),PackedReceipt="",Name;public void BindPackedReceipt(string t){if(PackedReceipt!=""&&PackedReceipt!=t)throw new Exception();PackedReceipt=t;}public void SetCustomName(string n){Name=n;}}
class OwnerData{public string CombinedString="owner";}
class TileEntityComposite{public OwnerData Owner=new OwnerData();public bool PlayerPlaced=true;public TEFeatureStorage Storage=new TEFeatureStorage();public TEFeatureRebirthContainerName Name=new TEFeatureRebirthContainerName();public bool IsUserAccessing()=>false;public T GetFeature<T>() where T:class=>Name as T;public bool TryGetSelfOrFeature<T>(out T value)where T:class{value=Storage as T;return value!=null;}}
enum RebirthSecureAccessPurpose{DirectInteraction}
static class RebirthSecureAccessPolicy{public static bool CanAccess(EntityPlayer p,TileEntityComposite t,RebirthSecureAccessPurpose purpose,out string reason){reason="";return true;}}
static class QuickStackAcceptedCategoryRegistry{public static bool Fail;public static int Value;public static void SetMask(Vector3i p,int m){if(Fail)throw new IOException("injected settings failure");Value=m;}}
static class RemoteResourceIdentity{public static string Static(Vector3i p)=>"destination";}
static class RemoteResourceStateStore{public static void RestorePackedState(string id,bool active,string[] exclusions){}}
static class RebirthPackedContainerPublication{public static bool Recovering;public static int Observed;public static void InvalidateRecoveryIndex(){}public static void RecoverAfterRead(TEFeatureRebirthContainerName n){}public static void Observe(World w,TileEntityComposite t,TEFeatureStorage s,TEFeatureRebirthContainerName n,RebirthPackedContainerReceiptJournal.Record r,string path){Observed++;}}
static class RebirthContainerPackUp{
 internal const int MaxPayload=131072;internal static string Root;
 // PRODUCTION_PAYLOAD
 internal static string RecordPath(string token)=>Path.Combine(Root,token+".json");
 static Payload Decode(string json)=>JsonConvert.DeserializeObject<Payload>(json);
 static ItemStack[] DecodeItems(string text)=>JsonConvert.DeserializeObject<ItemStack[]>(text);
 internal static string EncodeItems(ItemStack[] items)=>JsonConvert.SerializeObject(items);
 // PRODUCTION_RESTORE
 internal static void Run(World w,Vector3i p,string token,bool recovery)=>RestoreCore(w,p,1,token,recovery);
}
// PRODUCTION_JOURNAL
// PRODUCTION_DURABLE
static class Program{
 static void Need(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Main(string[] args){RebirthContainerPackUp.Root=args[0];string token=Guid.NewGuid().ToString("N");var w=new World{Tile=new TileEntityComposite()};w.worldState.Guid=Guid.NewGuid().ToString("N");GameManager.Instance.World=w;var pos=new Vector3i(0,0,0);var payload=new RebirthContainerPackUp.Payload{Token=token,Block="box",Width=1,Height=1,Items=RebirthContainerPackUp.EncodeItems(new[]{new ItemStack{count=27}}),Name="kept",Categories=4};string path=RebirthContainerPackUp.RecordPath(token);File.WriteAllText(path,JsonConvert.SerializeObject(payload));QuickStackAcceptedCategoryRegistry.Fail=true;try{RebirthContainerPackUp.Run(w,pos,token,false);}catch(IOException){}Need(w.Tile.Storage.ItemGrid.items[0].count==27&&w.Tile.Name.PackedReceipt==token&&RebirthPackedContainerReceiptJournal.Read(path).State==RebirthPackedContainerReceiptJournal.Phase.Reserved,"settings failure retains original destination and published contents");int resizes=w.Tile.Storage.ItemGrid.Resizes;QuickStackAcceptedCategoryRegistry.Fail=false;RebirthContainerPackUp.Run(w,pos,token,true);Need(w.Tile.Storage.ItemGrid.Resizes==resizes&&w.Tile.Storage.ItemGrid.items[0].count==27&&w.Tile.Name.Name=="kept"&&RebirthPackedContainerReceiptJournal.Read(path).State==RebirthPackedContainerReceiptJournal.Phase.Applied,"recovery repairs settings without repeating item publication");var old=w.Tile;w.Tile=new TileEntityComposite();bool refused=false;try{RebirthContainerPackUp.Run(w,pos,token,true);}catch{refused=true;}Need(refused&&w.Tile.Storage.IsEmpty(),"same-address replacement does not redeem original incarnation");w.Tile=old;var r=RebirthPackedContainerReceiptJournal.Read(path);r.NativeGeneration=1;r.NativeSnapshotDigest=new string('a',64);r.NativeTileDigest=new string('b',64);RebirthPackedContainerReceiptJournal.Advance(path,r,RebirthPackedContainerReceiptJournal.Phase.Saved);old.Storage.ItemGrid.items[0].count=0;RebirthContainerPackUp.Run(w,pos,token,true);Need(old.Storage.IsEmpty(),"saved receipt never republishes items after legitimate consumption");Console.WriteLine("Core recovery only: native serialization, final region proof and native item encoding remain separate qualification.");}
}
