using System.IO;
// Native/world/codec/file/receipt/transport doubles. No native-runtime proof.
class ItemClass {public int MaxCount=10;public bool Stackable=true,Container=true;public bool CanStack()=>Stackable;public bool CanPlaceInContainer()=>Container;}
class ItemClassArmor:ItemClass {public string ReplaceByTag;public FastTags<TagGroup.Global> ItemTags;}
class ItemValue
{
    public int type;public string Metadata="";public bool HasQuality;public ItemClass ItemClass=new ItemClass();public ItemClass ItemClassOrMissing=>type==0?null:ItemClass;
    public bool IsEmpty()=>type==0;public ItemValue Clone()=>new ItemValue{type=type,Metadata=Metadata,HasQuality=HasQuality,ItemClass=ItemClass};
    public static ItemValue ReadOrNull(PooledBinaryReader r)=>new ItemValue{type=r.ReadInt32(),Metadata=r.ReadString()};
}
class ItemStack
{
    public int count;public ItemValue itemValue;public static ItemStack Empty=>new ItemStack(new ItemValue(),0);
    public ItemStack(ItemValue v,int c){itemValue=v;count=c;}public bool IsEmpty()=>count==0||itemValue.IsEmpty();
    public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);
    public bool CanMoveTo(XUiC_ItemStack.StackLocationTypes l,int slot=-1)=>slot!=-99;
}
class XUiController{public XUi xui;}
class XUiC_ItemStack:XUiController
{
    public enum StackLocationTypes{Backpack,LootContainer,ToolBelt,CollectorCatalysts}
    public ItemStack ItemStack;public bool AllowDropping=true,SwapAllowed=true;public int OverrideStackCount=-1,SlotNumber;public StackLocationTypes StackLocation;
    public bool CanSwap(ItemStack s)=>SwapAllowed;
}
enum EquipmentSlots{Head=0,Count=13}
class XUiC_EquipmentStack:XUiController{public EquipmentSlots EquipSlot;public ItemStack ItemStack;public bool AllowRemoveItem=true;public void SwapItem(){}}
class XUi{public PlayerUI playerUI=new PlayerUI();public Drag DragAndDropWindow=new Drag();}
class PlayerUI{public EntityPlayerLocal entityPlayer;}class Drag{public ItemStack CurrentStack=ItemStack.Empty;}
class EntityPlayerLocal{public int entityId=7;public World world;public Equipment equipment=new Equipment();public bool Dead;public bool IsDead()=>Dead;}
class World{public EntityPlayerLocal Player;public WorldState worldState=new WorldState();public bool Remote=true;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public object GetEntity(int id)=>Player?.entityId==id?Player:null;}
class WorldState{public string Guid=System.Guid.NewGuid().ToString();}
class GameManager{public static GameManager Instance=new GameManager();public World World;}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
static class GameIO{public static string Directory="fixture-save";public static string GetSaveGameDir()=>Directory;}
class RebirthStablePlayerIdentity{public string StorageKey="fixture-owner";public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity i){i=new RebirthStablePlayerIdentity();return true;}}
static class RebirthSurvivorMode{public static bool Enabled=true;public static bool IsEnabledForCurrentWorld()=>Enabled;}
static class RebirthCharacterCreationHoldService{public static bool Held;public static bool IsHeld(EntityPlayerLocal p)=>Held;}
static class RebirthSurvivorClientState{public static string Creation="11111111111111111111111111111111";public static string GetProjectedCreationId(EntityPlayerLocal p)=>Creation;}
class Origin{public string CreationId=RebirthSurvivorClientState.Creation;}
class RebirthWorldCharacterRecord{public bool IsComplete=true;public Origin Origin=new Origin();}
static class RebirthWorldCharacterService{public static bool TryGet(EntityPlayerLocal p,out RebirthWorldCharacterRecord r){r=new RebirthWorldCharacterRecord();return true;}}
static class RebirthWorldCharacterRepository{public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>r!=null;}
enum StreamModeWrite{Persistency}
class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter():base(new MemoryStream()){}public void SetBaseStream(Stream s){OutStream=s;}}
class ReaderStream:Stream
{
    public Stream Target=new MemoryStream();public override bool CanRead=>Target.CanRead;public override bool CanSeek=>Target.CanSeek;public override bool CanWrite=>false;public override long Length=>Target.Length;public override long Position{get=>Target.Position;set=>Target.Position=value;}
    public override int Read(byte[] b,int o,int n)=>Target.Read(b,o,n);public override long Seek(long n,SeekOrigin o)=>Target.Seek(n,o);public override void Flush(){}public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();
}
class PooledBinaryReader:BinaryReader{private readonly ReaderStream stream;public PooledBinaryReader():this(new ReaderStream()){}private PooledBinaryReader(ReaderStream s):base(s){stream=s;}public void SetBaseStream(Stream s){stream.Target=s;}}
class Pool<T> where T:new(){public T AllocSync(bool sync)=>new T();}
static class MemoryPools{public static Pool<PooledBinaryWriter> poolBinaryWriter=new Pool<PooledBinaryWriter>();public static Pool<PooledBinaryReader> poolBinaryReader=new Pool<PooledBinaryReader>();}
struct Vector2i{public int x,y;}
static class StreamUtils{public static Vector2i ReadVector2i(BinaryReader r)=>new Vector2i{x=r.ReadInt32(),y=r.ReadInt32()};}
static class RebirthNativeItemCodec
{
    public static string Encode(ItemValue v){using var s=new MemoryStream();using var w=new BinaryWriter(s);w.Write(v.type);w.Write(v.Metadata);return Convert.ToBase64String(s.ToArray());}
}
// Fixture serializer models only native v6/grid13 row boundary and opaque footer.
// Full native ItemValue/paint/cosmetic ABI is not exercised.
class Equipment
{
    public static int Writes;public ItemStack[] Slots=Enumerable.Range(0,13).Select(_=>ItemStack.Empty).ToArray();
    public ItemValue GetSlotItem(int i)=>Slots[i].itemValue;
    public void Write(PooledBinaryWriter w,StreamModeWrite mode){Writes++;w.Write((byte)6);w.Write((ushort)2);w.Write(13);w.Write(1);w.Write((short)13);foreach(var s in Slots){w.Write((ushort)s.count);if(s.count>0)w.Write(Convert.FromBase64String(RebirthNativeItemCodec.Encode(s.itemValue)));}w.Write("opaque-native-footer");}
}
class PlayerDataFile{public MemoryStream equipmentData;}
static class RebirthGearNativePlayerFile{public static PlayerDataFile Saved;public static bool TryRead(RebirthStablePlayerIdentity i,out PlayerDataFile s){s=Saved;return s!=null;}}
static class RebirthVehicleClientCustody
{
    public static bool Live,Pending,Receipt;public static Guid Operation;public static int Admissions,Settlements;
    public static bool HasLiveCursorCustody(EntityPlayerLocal p)=>Live;
    public static bool IsPendingCursorHandoff(EntityPlayerLocal p)=>Pending;
    public static bool HasSavedHandoff(EntityPlayerLocal p,Guid op,PlayerDataFile saved)=>Receipt&&op==Operation&&ReferenceEquals(saved,RebirthGearNativePlayerFile.Saved);
    public static bool BeforeDestinationDrop(XUiController t,ItemStack a,ItemStack c,IRebirthVehicleCursorDestinationWitness w,out Guid op){Admissions++;op=Operation;return true;}
    public static void AfterDestinationDrop(EntityPlayerLocal p,Guid op){Settlements++;}
    public static void Warn(Exception e){}
}
class TagGroup{public class Global{}}
struct FastTags<T>{public static FastTags<T> Parse(string s)=>new FastTags<T>();public bool Test_AnySet(FastTags<T> s)=>true;}
namespace HarmonyLib{class Harmony{public void Patch(object m,HarmonyMethod prefix=null,HarmonyMethod finalizer=null){}}class HarmonyMethod{public HarmonyMethod(Type t,string n){}}static class AccessTools{public static object Method(Type t,string n)=>null;}}
