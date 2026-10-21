// EXPLICIT DOUBLES: native/world/stomach-free fixture, not actual game execution.
// Bank generation follows cached current native methods; item codec below is a
// deterministic complete-data DOUBLE, not RebirthNativeItemCodec native ABI proof.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

// Boundary DOUBLE: this fixture's actors are normal owners. Actual cached-owner
// Engineer policy is linked separately in Tools/EnergyBoundsRangeFixture.
public static class RebirthCamperEnergyBoundsRangePolicy
{public static float ForVehicle(EntityVehicle vehicle)=>RebirthCamperEnergyConfiguration.Current.Range;}
// Capacity boundary constants only; no actor background qualification evidence.
public static class RebirthCamperEnergyCapacity {public const int Normal=3,Electrician=6;}

namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string method){}} }
namespace UnityEngine.Scripting { public sealed class PreserveAttribute:Attribute{} }
namespace UnityEngine
{
    [Flags] public enum RigidbodyConstraints {None=0,FreezePositionX=2,FreezePositionY=4,FreezePositionZ=8,FreezeRotationX=16,FreezeRotationY=32,FreezeRotationZ=64,FreezeAll=126}
    public class Rigidbody {public Vector3 position,velocity,angularVelocity;public Quaternion rotation=Quaternion.identity;public bool isKinematic;public RigidbodyConstraints constraints;}
    // Explicit Euler surrogate; not native quaternion or physics proof.
    public struct Quaternion {private Vector3 angles;public static Quaternion identity=>new Quaternion();public static Quaternion Euler(Vector3 v)=>new Quaternion {angles=v};public Vector3 eulerAngles=>angles;public static float Angle(Quaternion a,Quaternion b)=>(float)Math.Sqrt((a.angles-b.angles).sqrMagnitude);public static bool operator==(Quaternion a,Quaternion b)=>a.angles==b.angles;public static bool operator!=(Quaternion a,Quaternion b)=>!(a==b);public override bool Equals(object o)=>o is Quaternion q&&this==q;public override int GetHashCode()=>angles.GetHashCode();}
    public static class Time {public static float realtimeSinceStartup;}
    public static class Mathf
    {public static int Min(int a,int b)=>Math.Min(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Abs(float v)=>Math.Abs(v);public static float Max(float a,float b)=>Math.Max(a,b);public static bool Approximately(float a,float b)=>Math.Abs(a-b)<0.000001f;public static int FloorToInt(float n)=>(int)Math.Floor(n);public static int CeilToInt(float n)=>(int)Math.Ceiling(n);}
    public struct Vector3
    {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero=>new Vector3();public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static bool operator==(Vector3 a,Vector3 b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator!=(Vector3 a,Vector3 b)=>!(a==b);public override bool Equals(object o)=>o is Vector3 v&&this==v;public override int GetHashCode()=>HashCode.Combine(x,y,z);public float sqrMagnitude=>x*x+y*y+z*z;public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
}
public struct Vector2i {public int x,y;public Vector2i(int x,int y){this.x=x;this.y=y;}}
public struct Vector3i {public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}public static bool operator==(Vector3i a,Vector3i b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator!=(Vector3i a,Vector3i b)=>!(a==b);public override bool Equals(object o)=>o is Vector3i p&&this==p;public override int GetHashCode()=>HashCode.Combine(x,y,z);}
public static class ThreadManager {public static bool IsMainThread()=>true;}
public sealed class GameTimer {public static GameTimer Instance=new GameTimer();public ulong ticks=100;}
public sealed class Properties
{public Dictionary<string,string> Values=new Dictionary<string,string>();public string GetString(string name)=>Values.TryGetValue(name,out var v)?v:null;}
public class Block
{
    public static readonly Block[] list=new Block[16];public Properties Properties=new Properties();
    public string Name;
    public static BlockValue GetBlockValue(string name){for(int i=1;i<list.Length;i++)if(list[i]?.Name==name)return new BlockValue {type=i};return new BlockValue();}
}
public class BlockPowerSource:Block {public string SlotItemName;public int OutputPerStack;}
public struct BlockValue {public int type;public Block Block=>Block.list[type];}
public sealed class ItemClass
{
    public static readonly Dictionary<int,ItemClass> Classes=new Dictionary<int,ItemClass>();
    public int Id;public string Name;public int NativeMaxUse;
    public string GetItemName()=>Name;
    public static ItemClass GetItemClass(string name)=>Classes.Values.FirstOrDefault(v=>v.Name==name);
}
public sealed class ItemValue
{
    public static ItemValue None=>new ItemValue();public bool IsEmpty()=>type==0;
    public int type,Seed,Quality=6;public float UseTimes;public byte[] TextureFullArray=new byte[0];
    public Dictionary<string,string> Metadata=new Dictionary<string,string>(StringComparer.Ordinal);
    public ItemClass ItemClass=>global::ItemClass.Classes.TryGetValue(type,out var value)?value:null;
    public int MaxUseTimes=>ItemClass?.NativeMaxUse??0;
    public ItemValue Clone()=>new ItemValue {type=type,Seed=Seed,Quality=Quality,UseTimes=UseTimes,TextureFullArray=(byte[])TextureFullArray.Clone(),Metadata=new Dictionary<string,string>(Metadata,StringComparer.Ordinal)};
    public bool TryGetMetadata(string key,out string value)=>Metadata.TryGetValue(key,out value);
    public void SetMetadata(string key,string value)=>Metadata[key]=value;
}
// Native bag/grid/lock shape doubles for pure custody frame matching only.
public sealed class PackedBoolArray {private readonly bool[] bits;public PackedBoolArray(bool[] values){bits=(bool[])values.Clone();}public int Length=>bits.Length;public bool this[int i]=>bits[i];}
public sealed class ItemStackGrid {public ItemStack[] items;}
public sealed class Bag {public ItemStackGrid ItemGrid;public PackedBoolArray LockedSlots;}
// Transport bound boundary double; actual CamperFrame Read/Write is extracted.
public static class RebirthCamperRemoteTransport
{internal const int MaximumFrame=8*1024*1024;internal static void AssertFrame(byte[] bytes){if(bytes==null||bytes.Length>MaximumFrame)throw new InvalidDataException("Explicit transport bound double.");}}
public sealed class ItemStack
{
    public ItemValue itemValue;public int count;
    public ItemStack(ItemValue value,int count){itemValue=value;this.count=count;}
    public static ItemStack Empty=>new ItemStack(new ItemValue(),0);
    public bool IsEmpty()=>count<=0||itemValue==null||itemValue.type==0;
    public ItemStack Clone()=>new ItemStack(itemValue?.Clone(),count);
    public static ItemStack[] Clone(IList<ItemStack> items)=>items.Select(v=>v.Clone()).ToArray();
    public static ItemStack[] CreateArray(int count)=>Enumerable.Range(0,count).Select(_=>Empty).ToArray();
}
public static class RebirthNativeItemCodec
{
    private sealed class Frame
    {public int Type{get;set;}public int Seed{get;set;}public int Quality{get;set;}public float Use{get;set;}public byte[] Paint{get;set;}public SortedDictionary<string,string> Metadata{get;set;}}
    public static string Encode(ItemValue value)=>Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Frame {Type=value.type,Seed=value.Seed,Quality=value.Quality,Use=value.UseTimes,Paint=value.TextureFullArray,Metadata=new SortedDictionary<string,string>(value.Metadata,StringComparer.Ordinal)}));
    public static bool TryDecode(string encoded,out ItemValue value)
    {
        value=null;try
        {var f=JsonSerializer.Deserialize<Frame>(Convert.FromBase64String(encoded));if(f==null||!ItemClass.Classes.ContainsKey(f.Type)||f.Paint==null||f.Metadata==null)return false;
            value=new ItemValue {type=f.Type,Seed=f.Seed,Quality=f.Quality,UseTimes=f.Use,TextureFullArray=f.Paint,Metadata=new Dictionary<string,string>(f.Metadata,StringComparer.Ordinal)};
            if(Encode(value)!=encoded){value=null;return false;}return true;}catch{return false;}
    }
}
public sealed class PooledBinaryWriter:BinaryWriter
{public PooledBinaryWriter():base(new MemoryStream(),Encoding.UTF8,true){}public void SetBaseStream(Stream stream)=>OutStream=stream;}
public sealed class PooledBinaryReader:IDisposable
{
    private BinaryReader reader;
    public void SetBaseStream(Stream stream)=>reader=new BinaryReader(stream,Encoding.UTF8,true);
    public int ReadInt32()=>reader.ReadInt32();public long ReadInt64()=>reader.ReadInt64();public byte ReadByte()=>reader.ReadByte();
    public float ReadSingle()=>reader.ReadSingle();public double ReadDouble()=>reader.ReadDouble();public ushort ReadUInt16()=>reader.ReadUInt16();
    public bool ReadBoolean()=>reader.ReadBoolean();public byte[] ReadBytes(int count)=>reader.ReadBytes(count);public string ReadString()=>reader.ReadString();
    public void Dispose()=>reader?.Dispose();
}
public sealed class WriterPool {public PooledBinaryWriter AllocSync(bool clear)=>new PooledBinaryWriter();}
public sealed class ReaderPool {public PooledBinaryReader AllocSync(bool clear)=>new PooledBinaryReader();}
public static class MemoryPools {public static WriterPool poolBinaryWriter=new WriterPool();public static ReaderPool poolBinaryReader=new ReaderPool();}

public class PowerSource
{
    public ushort BlockID,OutputPerStack,MaxPower,CurrentPower,MaxOutput;public bool isOn;
    public ItemStack[] Stacks=ItemStack.CreateArray(6);
    public virtual void SetValuesFromBlock(){MaxPower=ushort.Parse(Block.list[BlockID].Properties.GetString("MaxPower")??"60000");}
    public void SetSlots(ItemStack[] items){Stacks=items;MaxOutput=0;foreach(var item in items)if(!item.IsEmpty())MaxOutput+=(ushort)(OutputPerStack*(0.5f+0.5f*item.itemValue.Quality/6f));if(this is PowerSolarPanel)MaxPower=MaxOutput;}
    public virtual void TickPowerGeneration(){}
}
public sealed class PowerGenerator:PowerSource
{
    public ushort CurrentFuel,MaxFuel;public float OutputPerFuel;
    public override void SetValuesFromBlock(){base.SetValuesFromBlock();var p=Block.list[BlockID].Properties;MaxFuel=ushort.Parse(p.GetString("MaxFuel"));OutputPerFuel=float.Parse(p.GetString("OutputPerFuel"),System.Globalization.CultureInfo.InvariantCulture);}
    public override void TickPowerGeneration(){if(MaxPower-CurrentPower>=OutputPerFuel&&CurrentFuel>0){CurrentFuel--;CurrentPower+=(ushort)OutputPerFuel;}}
}
public sealed partial class PowerBatteryBank:PowerSource
{
    public ushort InputPerTick=5,ChargePerInput=1,LastInputAmount;
    public int NativeChanges;public void SendHasLocalChangesToRoot(){NativeChanges++;}
    public ushort OutputPerCharge;
    public override void SetValuesFromBlock(){base.SetValuesFromBlock();OutputPerCharge=ushort.Parse(Block.list[BlockID].Properties.GetString("OutputPerCharge"));}
    public override void TickPowerGeneration()
    {ushort needed=(ushort)(MaxPower-CurrentPower);ushort charges=(ushort)(needed/OutputPerCharge);if(needed<OutputPerCharge)return;
        foreach(var item in Stacks){int n=(int)Math.Min(item.itemValue.MaxUseTimes-item.itemValue.UseTimes,charges);if(n>0){item.itemValue.UseTimes+=n;CurrentPower+=(ushort)(n*OutputPerCharge);break;}}}
}
public sealed class PowerSolarPanel:PowerSource {public bool HasLight;public override void TickPowerGeneration(){if(HasLight)CurrentPower=MaxOutput;}}
public sealed class Chunk {public enum LIGHT_TYPE {SUN}public byte Sun=15;public byte GetLight(int x,int y,int z,LIGHT_TYPE type)=>Sun;}
public class World
{
    public bool Remote;public bool Day=true;public Chunk Chunk=new Chunk();public Dictionary<int,Entity> Entities=new Dictionary<int,Entity>();
    public bool IsRemote()=>Remote;public Entity GetEntity(int id)=>Entities.TryGetValue(id,out var entity)?entity:null;public bool IsDaytime()=>Day;public object GetChunkFromWorldPos(Vector3i pos)=>Chunk;
}
public class Entity {public World world;public int entityId;public UnityEngine.Vector3 position;}
public class EntityPlayer:Entity {public bool Dead;public virtual bool IsDead()=>Dead;}
public sealed class EntityPlayerLocal:EntityPlayer {public Entity AttachedToEntity;public Hit HitInfo;}
// EXPLICIT physics/rotation doubles: no native solver or real quaternion qualification.
public sealed class RigidBody:UnityEngine.Rigidbody {}
public static class Origin {public static UnityEngine.Vector3 position;}
public sealed class Vehicle
{public ItemValue Item=new ItemValue();public string Name="rebirthCamper80s";public float Fuel=345,MaximumFuel=1200;public ItemValue GetUpdatedItemValue()=>Item;public string GetName()=>Name;public float GetFuelPercent()=>Fuel/MaximumFuel;public float GetHealthPercent()=>0.17f;public float GetFuelLevel()=>Fuel;public float GetMaxFuelLevel()=>MaximumFuel;}
public sealed class EntityVehicle:Entity
{
    public bool IsEngineRunning;public static float VehicleFuelUsageModifier=1;
    public bool IsLocked()=>false;public bool hasLock()=>false;public bool isAllowedUser(string owner)=>owner==Owner;
    public Vehicle vehicle=new Vehicle();public RigidBody vehicleRB=new RigidBody();public Entity[] Seats=new Entity[4];public int Health=17,MaximumHealth=100,SyncCount;public string Owner="owner";public bool ThrowNextSync;
    public UnityEngine.Quaternion qrotation=UnityEngine.Quaternion.identity;public void SetPosition(UnityEngine.Vector3 p,bool update=true){position=p;}public void SetRotation(UnityEngine.Vector3 p){qrotation=UnityEngine.Quaternion.Euler(p);}public void PhysicsFixedUpdate(){}public void OnEntityUnload(){}public Entity GetAttached(int slot)=>Seats[slot];public string GetOwner()=>Owner;public Vehicle GetVehicle()=>vehicle;public int GetMaxHealth()=>MaximumHealth;
    public void SendSyncData(ushort flags){if(ThrowNextSync){ThrowNextSync=false;throw new IOException("Explicit injected native sync failure.");}SyncCount++;}
    public int AttachEntityToSelf(Entity _entity,int slot=-1)=>slot;public void Update(){}
}
public sealed class Hit {public bool bHitValid;public Transform transform;public string tag;}
public sealed class Transform {public EntityVehicle Vehicle;public T GetComponent<T>() where T:class=>Vehicle as T;}
public static class GameUtils {public static Transform GetHitRootTransform(string tag,Transform transform)=>transform;}
public sealed class PersistentData {public string PrimaryId="owner",NativeId="owner";}
public sealed class PersistentList {public PersistentData Data=new PersistentData();public PersistentData GetPlayerDataFromEntityID(int id)=>Data;}
public sealed class GameManager {public static GameManager Instance=new GameManager();public World World;public PersistentList Players=new PersistentList();public PersistentList GetPersistentPlayerList()=>Players;}
public sealed class VehicleManager {public static VehicleManager Instance=new VehicleManager();public int Saves;public void TriggerSave()=>Saves++;}
public static class RebirthCamperRadial
{public static bool IsCamper(EntityVehicle v)=>v?.vehicle?.GetName()=="rebirthCamper80s";public static bool IsDriver(EntityVehicle v,EntityPlayer p)=>v!=null&&p!=null&&ReferenceEquals(v.GetAttached(0),p);}
public static class RebirthCamperSavedVehicle
{public sealed class Expected {}public static int Requests;public static Expected RequestSave(EntityVehicle vehicle){Requests++;return new Expected();}}
// Transport/storage boundary DOUBLE. Does not validate actual chunk framing or
// native 255-key metadata limits; the actual storage helper needs its own fixture.
internal static class RebirthCamperEnergyDeviceLeaseStorage
{
    internal static int MaximumBytes=>524288;internal static int Writes;
    internal static byte[] Read(ItemValue value)=>value.TryGetMetadata("RebirthCamperEnergyDeviceLease",out var s)?Convert.FromBase64String(s):Array.Empty<byte>();
    internal static void Write(ItemValue value,byte[] bytes){Writes++;value.SetMetadata("RebirthCamperEnergyDeviceLease",Convert.ToBase64String(bytes));}
}
public static class RebirthCamperEnergyDeviceAdapter {public static void Configure(float poiRange,float defenseRange,int ordinaryLightWatts=1){}}
public sealed class StationState {public ulong lastTickTime;public int Effects;public void TickMobile(EntityVehicle owner){if(lastTickTime==0){lastTickTime=GameTimer.Instance.ticks;return;}Effects+=(int)(GameTimer.Instance.ticks-lastTickTime);lastTickTime=GameTimer.Instance.ticks;}public void BindVehicle(EntityVehicle vehicle){}}
public sealed class StationSlot {public StationState State=new StationState();}
public sealed class StationSlots {public StationSlot[] Slots={new StationSlot()};public StationSlot GetSlot(int i)=>i<Slots.Length?Slots[i]:null;}
public static class RebirthCamperCraftingStation {public const int ModuleSlots=1;}
public sealed partial class RebirthCamperModuleRuntime
{
    private static Dictionary<EntityVehicle,RebirthCamperModuleRuntime> owners=new Dictionary<EntityVehicle,RebirthCamperModuleRuntime>();
    public static int ForCalls;public Guid Identity=Guid.NewGuid();public bool TransferPending;public StationSlots Stations=new StationSlots();public int Persists;
    private readonly EntityVehicle vehicle;private ulong lastProcessingTick;private bool energyClockHeld;
    private RebirthCamperModuleRuntime(EntityVehicle vehicle){this.vehicle=vehicle;}
    public static RebirthCamperModuleRuntime For(EntityVehicle vehicle){ForCalls++;if(!owners.TryGetValue(vehicle,out var v))owners[vehicle]=v=new RebirthCamperModuleRuntime(vehicle);return v;}
    internal void BindStations(){}internal void Persist()=>Persists++;
}
public sealed class XUiView {public Vector2i Size=new Vector2i(180,44),Position;}
public sealed class XUiController {public XUiView ViewComponent=new XUiView();}
public sealed class PlayerUi {public EntityPlayerLocal entityPlayer;}
public sealed class XUi {public PlayerUi playerUI=new PlayerUi();}
public enum HUDStatTypes {VehicleFuel,VehicleHealth}
public class XUiC_HUDStatBar
{
    public HUDStatTypes statType;public EntityVehicle vehicle;public EntityPlayerLocal localPlayer;public float currentValue;public XUi xui=new XUi();public XUiView ViewComponent=new XUiView();
    public virtual void Update(float dt){}public void RefreshBindings(){}public XUiController GetChildById(string name)=>new XUiController();
    public virtual bool GetBindingValueInternal(ref string value,string name){value="native-base-double";return true;}
}
public static class Localization {public static string Get(string key)=>key switch {
    "xuiRebirthVehicleHudFuel"=>"FUEL", "xuiRebirthEnergyHudTravel"=>"TRAVEL",
    "xuiRebirthEnergyHudCamp"=>"CAMP", "xuiRebirthEnergyHudDeploying"=>"DEPLOYING {0}s",
    "xuiRebirthEnergyHudPacking"=>"PACKING {0}s", _=>key};}
