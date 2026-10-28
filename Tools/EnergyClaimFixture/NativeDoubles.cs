// Explicit dependency doubles. No real Harmony/network/physics/native save proof.
using System.Reflection;
namespace UnityEngine {
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public float sqrMagnitude=>x*x+y*y+z*z;public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);public static bool operator ==(Vector3 a,Vector3 b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator !=(Vector3 a,Vector3 b)=>!(a==b);public override bool Equals(object o)=>o is Vector3 v&&this==v;public override int GetHashCode()=>HashCode.Combine(x,y,z);}
 public struct Quaternion {public float x,y,z,w;public Quaternion(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}public static float Angle(Quaternion a,Quaternion b)=>a==b?0:180;public static bool operator ==(Quaternion a,Quaternion b)=>a.x==b.x&&a.y==b.y&&a.z==b.z&&a.w==b.w;public static bool operator !=(Quaternion a,Quaternion b)=>!(a==b);public override bool Equals(object o)=>o is Quaternion q&&this==q;public override int GetHashCode()=>HashCode.Combine(x,y,z,w);}
 public static class Mathf {public static float Abs(float n)=>Math.Abs(n);public static float Max(float a,float b)=>Math.Max(a,b);}
 public static class Time {public static float realtimeSinceStartup;}
}
namespace UnityEngine.Scripting {public sealed class PreserveAttribute:Attribute {}}
namespace HarmonyLib {public sealed class Harmony {public Harmony(string id){}}[AttributeUsage(AttributeTargets.Class,AllowMultiple=true)]public sealed class HarmonyPatch:Attribute {public HarmonyPatch(){}public HarmonyPatch(Type t,string n){}public HarmonyPatch(Type t,string n,Type[] p){}}}
public static class RebirthHarmonyBootstrap {public static readonly List<Type> Types=new();public static void PatchClassOnce(HarmonyLib.Harmony h,Type t)=>Types.Add(t);}
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public struct Vector3i {public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}public UnityEngine.Vector3 ToVector3Center()=>new(x+.5f,y+.5f,z+.5f);}
public enum EnumLandClaimOwner {None,Self,Ally,Other}
public enum EnumGameStats {GameModeId,LandClaimSize,LandClaimDeadZone}
public static class GameStats {public static int GetInt(EnumGameStats s)=>s==EnumGameStats.GameModeId?1:s==EnumGameStats.LandClaimSize?31:5;}
public sealed class PersistentPlayerData {public int EntityId;public bool Expired;public List<Vector3i> LPBlocks=new();public HashSet<PersistentPlayerData> Allies=new();public bool IsAlly(PersistentPlayerData p)=>Allies.Contains(p);}
public sealed class PersistentPlayerList {public Dictionary<object,PersistentPlayerData> Players=new();public PersistentPlayerData GetPlayerData(object id)=>Players.GetValueOrDefault(id);public PersistentPlayerData GetPlayerDataFromEntityID(int id)=>Players.Values.FirstOrDefault(p=>p.EntityId==id);}
public class Entity {public int entityId;public World world;public UnityEngine.Vector3 position;public UnityEngine.Quaternion qrotation=new(0,0,0,1);public Vector3i GetBlockPosition()=>new((int)position.x,(int)position.y,(int)position.z);}
public class EntityVehicle:Entity {public object Owner;public VehicleData vehicle=new();public object GetOwner()=>Owner;public void OnEntityUnload(){}}
public class EntityPlayer:Entity {public Party Party;public Inventory inventory=new();}
public class Party {public HashSet<int> Members=new();public bool ContainsMember(int id)=>Members.Contains(id);}
public class Inventory {public object holdingItemItemValue=new();}
public class VehicleData {public ItemValue Item=new();public ItemValue GetUpdatedItemValue()=>Item;}
public class ItemValue {public Dictionary<string,string> Metadata=new();public bool TryGetMetadata(string key,out string value)=>Metadata.TryGetValue(key,out value);}
public class WorldState {public string Guid="fixture-world";}
public class EntityList {public List<Entity> list=new();}
public class Chunk {}
public class World {public bool Remote,Trader;public WorldState worldState=new();public EntityList Entities=new();public PersistentPlayerData PhysicalOwner;public bool IsRemote()=>Remote;public Entity GetEntity(int id)=>Entities.list.FirstOrDefault(e=>e.entityId==id);public bool IsWithinTraderArea(Vector3i p)=>Trader;public bool IsLandProtectionValidForPlayer(PersistentPlayerData p)=>!p.Expired;
 public EnumLandClaimOwner GetLandClaimOwner(Vector3i p,PersistentPlayerData r)=>PhysicalOwner==null?EnumLandClaimOwner.None:r==PhysicalOwner?EnumLandClaimOwner.Self:r!=null&&PhysicalOwner.IsAlly(r)?EnumLandClaimOwner.Ally:EnumLandClaimOwner.Other;
 public EnumLandClaimOwner GetLandClaimOwner(Chunk c,Vector3i p,PersistentPlayerData r,int size,int zone,bool key)=>GetLandClaimOwner(p,r);
 public bool CanPlaceBlockAt(Vector3i p,PersistentPlayerData r,bool traderAllowed=false)=>true;public bool CanPickupBlockAt(Vector3i p,PersistentPlayerData r)=>true;public bool IsMyLandProtectedBlock(Vector3i p,PersistentPlayerData r)=>false;public bool IsEmptyPosition(Vector3i p)=>true;
 public bool CanPlaceLandProtectionBlockAt(Vector3i p,PersistentPlayerData r)=>true;public float GetLandProtectionHardnessModifier(Chunk c,Vector3i p,PersistentPlayerData r,int size)=>1;public float GetLandProtectionHardnessModifierForPlayer(PersistentPlayerData p)=>4;
 public bool GetLandClaimOwnerInParty(EntityPlayer p,PersistentPlayerData r)=>false;public bool GetLandClaimOwnerInParty(Chunk c,EntityPlayer p,Vector3i pos,PersistentPlayerData r,int size,int zone)=>false;
 public bool IsLandProtectedBlock(Chunk c,Vector3i p,PersistentPlayerData r,int size,int zone,bool key)=>false;public bool IsMyLandClaimInChunk(Chunk c,Vector3i p,PersistentPlayerData r,int size,int zone,bool key)=>false;}
public class GameManager {public static GameManager Instance=new();public World World;public PersistentPlayerList Players=new();public PersistentPlayerList GetPersistentPlayerList()=>Players;}
public enum RebirthCamperEnergyMode:byte {Travel,Deploying,Camp,Packing}
public static class RebirthCamperEnergyRuntime {public static bool IsRegistered=true;public static RebirthCamperEnergyMode Mode=RebirthCamperEnergyMode.Camp;public static long Revision=10;public static float Range=10;public static bool TryObserveMode(EntityVehicle v,out RebirthCamperEnergyMode mode,out long revision,out float range){mode=Mode;revision=Revision;range=Range;return IsRegistered;}}
public static class RebirthCamperEnergyImmobility {public static bool Pinned=true;public static int Checks;public static bool CampClaimReady(EntityVehicle v){Checks++;return !v.world.IsRemote()&&Pinned&&RebirthCamperEnergyRuntime.Mode==RebirthCamperEnergyMode.Camp;}}
public static class RebirthCamperEnergyConfiguration {public static Configuration Current=new();public class Configuration {public float Range=10;}}
public static class RebirthCamperRadial {public static bool IsCamper(EntityVehicle v)=>v!=null;}
public enum PassiveEffects {LandClaimDamageModifier}
public static class EffectManager {public static float GetValue(PassiveEffects e,object item,float value,EntityPlayer player)=>value;}
public static class ModEvents {public struct SGameUpdateData{}public struct SWorldShuttingDownData{}public delegate void Update(ref SGameUpdateData d);public delegate void Shutdown(ref SWorldShuttingDownData d);public static UpdateEvent GameUpdate=new();public static ShutdownEvent WorldShuttingDown=new();public class UpdateEvent {public List<Update> Handlers=new();public void RegisterHandler(Update u)=>Handlers.Add(u);public void Fire(){var d=new SGameUpdateData();foreach(var h in Handlers)h(ref d);}}public class ShutdownEvent {public List<Shutdown> Handlers=new();public void RegisterHandler(Shutdown u)=>Handlers.Add(u);}}
public class ConnectionManager {public bool IsServer=true;public List<NetPackage> Sent=new();public void SendPackage(NetPackage p)=>Sent.Add(p);}
public static class SingletonMonoBehaviour<T> where T:new() {public static T Instance=new();}
public enum NetPackageDirection {ToClient,ToServer}
public class NetPackage {public virtual NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){}public virtual void ProcessPackage(World w,GameManager g){}}
public static class NetPackageManager {public static T GetPackage<T>() where T:new()=>new();}
public class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){}}
public class PooledBinaryWriter:BinaryWriter {public PooledBinaryWriter(Stream s):base(s){}}
public static class Log {public static void Warning(string s){}}
