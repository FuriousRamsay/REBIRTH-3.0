using System;using System.Collections.Generic;using System.IO;using System.Text;
class Program
{
 static int checks;static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static void Main()
 {
  var id=Guid.NewGuid();var key=new string('a',64);var world=new World{worldState=new WorldState{Guid=id.ToString("N")}};GameManager.Instance=new GameManager{World=world};var manager=new ConnectionManager{IsServer=true};SingletonMonoBehaviour<ConnectionManager>.Instance=manager;
  var owner=new EntityPlayer{world=world,entityId=1,Key=key,EntityName="Owner"};var other=new EntityPlayer{world=world,entityId=2,Key=new string('b',64)};var crate=new EntitySupplyCrate{world=world,entityId=10,WorldTimeBorn=42};
  world.Entities.dict[1]=owner;world.Entities.dict[2]=other;world.Entities.dict[10]=crate;
  var pending=new RebirthPurgeSupplyAccount(key,75,75,1,0,1);RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{key,pending}};
  var stamp=new RebirthPurgeSupplyCrateStamp(id,key,1,pending.Token(id));Check(RebirthPurgeSupplyCrateSerialization.TryAttach(crate,stamp,new object(),()=>true),"actual original authority attaches earned owned crate");
  Check(RebirthPurgeSupplyAccess.AllowOpen(crate,owner),"owner can open accepted native delivery without a disk-save barrier");
  string text="Search";RebirthPurgeSupplyAccess.AfterActivationText(crate,ref text,true);
  Check(text=="Reserved for Owner\nSearch","owned delivery shows reservation without saving-state UI");
  bool result=true;Check(RebirthPurgeSupplyAccess.BeforeLock(crate,1,ref result),"owner reaches normal native loot opening");
  var client=new ClientInfo{entityId=1,loginDone=true};manager.Clients.Map[1]=client;
  LockManager.Instance.singleLocks.Map[new LockManager.LockEntry(crate,0)]=1;
  var bag=new NetPackageBag{entityId=10,Sender=client};
  Check(RebirthPurgeSupplyAccess.BeforeBag(bag,world),"native owner lock allows bag update before account reconciliation");
  pending.TryComplete(id,pending.Token(id),out var delivered);RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{key,delivered}};
  Check(RebirthPurgeSupplyAccess.AllowOpen(crate,owner),"confirmed original completion permits actual stable owner");
  Check(crate.spawnById==1&&crate.spawnByName=="Owner"&&!crate.spawnByAllowShare,"native owner display fields project authenticated player without replacing stable authority");
  text="Search";RebirthPurgeSupplyAccess.AfterActivationText(crate,ref text,true);
  Check(text=="Reserved for Owner\nSearch","completed owned crate uses native owner projection and shared activation text");
  text="";RebirthPurgeSupplyAccess.AfterActivationText(crate,ref text,true);Check(text=="","absent native interaction is not invented by presentation");
  Check(!RebirthPurgeSupplyAccess.AllowOpen(crate,other),"another stable owner cannot loot completed reserved crate");
  Check(RebirthPurgeSupplyAccess.BeforeBag(bag,world),"authenticated current owner with native lock may send normal bag update");
  bag.Sender=new ClientInfo{entityId=1,loginDone=true};
  Check(!RebirthPurgeSupplyAccess.BeforeBag(bag,world),"unregistered connection with reused player ID cannot mutate bag");
  bag.Sender=client;client.loginDone=false;Check(!RebirthPurgeSupplyAccess.BeforeBag(bag,world),"incomplete login cannot mutate bag");client.loginDone=true;
  LockManager.Instance.singleLocks.Map[new LockManager.LockEntry(crate,0)]=2;Check(!RebirthPurgeSupplyAccess.BeforeBag(bag,world),"another native edit-lock owner cannot submit bag update");LockManager.Instance.singleLocks.Map.Clear();
  Check(!RebirthPurgeSupplyAccess.BeforeBag(bag,world),"missing native edit lease cannot submit bag update");
  world.Entities.dict[1]=other;Check(!RebirthPurgeSupplyAccess.AllowOpen(crate,owner),"stale player runtime instance cannot claim stable owner");world.Entities.dict[1]=owner;
  var normal=new EntitySupplyCrate{world=world,entityId=11};world.Entities.dict[11]=normal;
  Check(RebirthPurgeSupplyAccess.AllowOpen(normal,other),"unowned native crates retain normal interaction");
  RebirthPurgeSupplyService.Published=null;Check(RebirthPurgeSupplyAccess.AllowOpen(crate,owner),"saved owner remains able to open when reward accounting is unavailable");
  RebirthSandboxOptionManager.Current.IsPurge=false;Check(RebirthPurgeSupplyAccess.AllowOpen(crate,other),"None QoL preserves native access rather than enabling Purge restrictions");
  Console.WriteLine("RESULT "+checks+" PASS; production saved authority and access decisions; native entity, connection, stable identity and lock APIs explicitly doubled. No live network, Harmony ordering or game proof.");
 }
}
class Log{public static void Warning(string text){}}
class Entity{public World world;public int entityId;public ulong WorldTimeBorn;public int spawnById;public string spawnByName,EntityName;public bool spawnByAllowShare;}
class EntityAlive:Entity{}class EntitySupplyCrate:EntityAlive{}class EntityPlayer:EntityAlive{public string Key;}
class WorldState{public string Guid;}class EntityList{public Dictionary<int,Entity> dict=new Dictionary<int,Entity>();}
class World{public WorldState worldState;public EntityList Entities=new EntityList();public bool IsRemote()=>false;public Entity GetEntity(int id)=>Entities.dict.TryGetValue(id,out var found)?found:null;}
class GameManager{public static GameManager Instance;public World World;}class ClientInfo{public int entityId;public bool loginDone;}
class ClientList{public Dictionary<int,ClientInfo> Map=new Dictionary<int,ClientInfo>();public ClientInfo ForEntityId(int id)=>Map.TryGetValue(id,out var found)?found:null;}
class ConnectionManager{public bool IsServer;public ClientList Clients=new ClientList();}
class SingletonMonoBehaviour<T>{public static T Instance;}class ThreadManager{public static bool IsMainThread()=>true;}
class NetPackageBag{public int entityId;public ClientInfo Sender;}class NetPackageDamageEntity{public int entityId;}
class LockManager{public readonly struct LockEntry{public readonly Entity Target;public readonly ushort Channel;public LockEntry(Entity target,ushort channel){Target=target;Channel=channel;}}
 public static LockManager Instance=new LockManager();public Locks singleLocks=new Locks();public class Locks{public Dictionary<LockEntry,int> Map=new Dictionary<LockEntry,int>();public bool TryGetByValue(LockEntry entry,out int id)=>Map.TryGetValue(entry,out id);}}
internal static class RebirthPurgeKillContributor{internal static string Identify(EntityPlayer player,World expected)=>player!=null&&ReferenceEquals(expected,player.world)&&ReferenceEquals(expected.GetEntity(player.entityId),player)?player.Key:null;}
internal static class RebirthPurgeSupplyService{internal static IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> Published;}
internal static class RebirthPurgeReleasePolicy{internal static bool Enabled=true;}internal class RebirthSandboxOptionManager{internal static readonly RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();internal bool IsPurge=true;}
enum StreamModeRead{Persistency,FromServer,FromClient}enum StreamModeWrite{Persistency,ToServer,ToClient}
class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream stream):base(stream,Encoding.UTF8,true){}}
class PooledBinaryReader:BinaryReader{public PooledBinaryReader(Stream stream):base(stream,Encoding.UTF8,true){}}
namespace UnityEngine.Scripting{class PreserveAttribute:Attribute{}}namespace HarmonyLib{}
class Localization{public static string Get(string key)=>key=="xuiRebirthPurgeSupplyReserved"?"Reserved for {0}":key== "xuiRebirthPurgeSupplySaving"?"Saving earned supply delivery…":"its owner";}
