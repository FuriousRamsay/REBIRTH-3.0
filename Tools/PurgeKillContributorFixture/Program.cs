using System;using System.Collections.Generic;
class Entity {public World world;public int entityId;}
class EntityAlive:Entity {public Entity entityThatKilledMe;}
class EntityPlayer:EntityAlive {}
class EntityPlayerLocal:EntityPlayer {}
class EntityList {public Dictionary<int,Entity> dict=new Dictionary<int,Entity>();}
class World {public EntityList Entities=new EntityList();public bool Remote;public List<EntityPlayer> Locals=new List<EntityPlayer>();public bool IsRemote()=>Remote;public List<EntityPlayer> GetLocalPlayers()=>Locals;}
class GameManager {public static GameManager Instance;public static bool IsDedicatedServer;public World World;}
class ClientInfo {public bool loginDone;public int entityId;public string Key;}
class Clients {public Dictionary<int,ClientInfo> Values=new Dictionary<int,ClientInfo>();public ClientInfo ForEntityId(int id)=>Values.TryGetValue(id,out var c)?c:null;}
class ConnectionManager {public bool IsServer;public Clients Clients=new Clients();}
class SingletonMonoBehaviour<T> {public static T Instance;}
class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
class RebirthPurgeReleasePolicy {public static bool Enabled=true;}
class Options {public bool IsPurge=true;}
class RebirthSandboxOptionManager {public static Options Current=new Options();}
class RebirthStablePlayerIdentity {public string StorageKey;public static string Local=new string('b',64);public static bool TryFromClientInfo(ClientInfo c,out RebirthStablePlayerIdentity i){i=c.Key==null?null:new RebirthStablePlayerIdentity{StorageKey=c.Key};return i!=null;}public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity i){i=Local==null?null:new RebirthStablePlayerIdentity{StorageKey=Local};return i!=null;}}
class Program
{
 static int checks;static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static void Main()
 {
  var world=new World();GameManager.Instance=new GameManager{World=world};var manager=new ConnectionManager{IsServer=true};SingletonMonoBehaviour<ConnectionManager>.Instance=manager;
  var player=new EntityPlayer{world=world,entityId=123};world.Entities.dict[123]=player;var victim=new EntityAlive{world=world,entityThatKilledMe=player};var client=new ClientInfo{entityId=123,loginDone=true,Key=new string('a',64)};manager.Clients.Values[123]=client;
  Check(RebirthPurgeKillContributor.Capture(victim)==client.Key,"registered server killer resolves stable identity");
  client.loginDone=false;Check(RebirthPurgeKillContributor.Capture(victim)==null,"not logged-in peer withheld");client.loginDone=true;
  client.entityId=124;Check(RebirthPurgeKillContributor.Capture(victim)==null,"mismatched client runtime identity withheld");client.entityId=123;
  world.Entities.dict[123]=new EntityPlayer{world=world,entityId=123};Check(RebirthPurgeKillContributor.Capture(victim)==null,"reused runtime ID cannot transfer kill");world.Entities.dict[123]=player;
  world.Remote=true;Check(RebirthPurgeKillContributor.Capture(victim)==null,"remote client cannot mint credit");world.Remote=false;
  manager.IsServer=false;Check(RebirthPurgeKillContributor.Capture(victim)==null,"non-authoritative owner withheld");manager.IsServer=true;
  ThreadManager.Main=false;Check(RebirthPurgeKillContributor.Capture(victim)==null,"worker cannot capture native player attribution");ThreadManager.Main=true;
  GameManager.Instance.World=new World();Check(RebirthPurgeKillContributor.Capture(victim)==null,"replaced saved world withheld");GameManager.Instance.World=world;
  victim.entityThatKilledMe=new EntityAlive{world=world};Check(RebirthPurgeKillContributor.Capture(victim)==null,"non-player source earns no player credit");
  manager.Clients.Values.Clear();var local=new EntityPlayerLocal{world=world,entityId=123};world.Entities.dict[123]=local;victim.entityThatKilledMe=local;
  Check(RebirthPurgeKillContributor.Capture(victim)==null,"unregistered local platform participant withheld");world.Locals.Add(local);
  Check(RebirthPurgeKillContributor.Capture(victim)==RebirthStablePlayerIdentity.Local,"actual registered local player resolves platform identity");
  GameManager.IsDedicatedServer=true;Check(RebirthPurgeKillContributor.Capture(victim)==null,"dedicated server cannot impersonate local player");GameManager.IsDedicatedServer=false;
  RebirthSandboxOptionManager.Current.IsPurge=false;Check(RebirthPurgeKillContributor.Capture(victim)==null,"None QoL remains outside Purge rewards");RebirthSandboxOptionManager.Current.IsPurge=true;
  RebirthPurgeReleasePolicy.Enabled=false;Check(RebirthPurgeKillContributor.Capture(victim)==null,"unreleased implementation cannot mint rewards");
  Console.WriteLine("RESULT "+checks+" PASS; production contributor helper; explicit native world/client/platform doubles; no live multiplayer proof.");
 }
}