using System;
public class EntityPlayerLocal{public int entityId=7;public World world;public bool Dead,Spawned=true;public bool IsSpawned()=>Spawned;public bool IsDead()=>Dead;}
public class World{public bool Remote;public EntityPlayerLocal Player,Entity;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public EntityPlayerLocal GetEntity(int id)=>Entity;}
public class GameManager{public static GameManager Instance;public World World;}
public class SingletonMonoBehaviour<T>{public static T Instance;}
public class Peer{public bool Dead;public bool IsDisconnected()=>Dead;}
public class ConnectionManager{public bool IsServer=true;public Peer[] connectionToServer;public int Sent;public void SendToServer(object p){Sent++;}}
public static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
public static class RebirthSurvivorMode{public static bool Enabled=true;public static bool IsEnabledForCurrentWorld()=>Enabled;}
public class Record{public bool IsComplete=true;public object Support=new();public Origin Origin=new();}
public class Origin{public string CreationId;}
public static class RebirthWorldCharacterRepository{public static bool IsServerAuthority=true,Current=true;public static bool IsCurrentCachedRecord(Record r)=>Current;}
public static class RebirthWorldCharacterService{public static Record Value;public static bool TryGet(EntityPlayerLocal p,out Record r){r=Value;return r!=null;}}
public static class RebirthSurvivorClientState{public static string Projection;public static string GetProjectedCreationId(EntityPlayerLocal p)=>Projection;}
public enum RebirthBackpackLibraryViewStatus{Unavailable,NoBackpack,Ready}
public class RebirthBackpackLibraryView{public string CreationId;public long GearRevision;}
public class RebirthBackpackSellStashView{public string CreationId;public long GearRevision;}
public static class RebirthBackpackLibraryViewCodec{public const int MaxBytes=100;public static bool TryEncode(RebirthBackpackLibraryView v,out byte[] b){b=new byte[]{1};return true;}public static bool TryDecode(byte[] b,out RebirthBackpackLibraryView v){v=null;return false;}}
public static class RebirthBackpackSellStashViewCodec{public const int MaxBytes=100;public static bool TryEncode(RebirthBackpackSellStashView v,out byte[] b){b=new byte[]{1};return true;}public static bool TryDecode(byte[] b,out RebirthBackpackSellStashView v){v=null;return false;}}
public static class RebirthBackpackLibraryServer{
public static bool Backpack;
public static RebirthBackpackLibraryViewStatus GetLocalViewStatus(EntityPlayerLocal p,string c,out RebirthBackpackLibraryView v,out long r){r=5;v=Backpack?new(){CreationId=c,GearRevision=r}:null;return Backpack?RebirthBackpackLibraryViewStatus.Ready:RebirthBackpackLibraryViewStatus.NoBackpack;}
public static RebirthBackpackLibraryViewStatus GetLocalSellStashViewStatus(EntityPlayerLocal p,string c,out RebirthBackpackSellStashView v,out long r){r=5;v=Backpack?new(){CreationId=c,GearRevision=r}:null;return Backpack?RebirthBackpackLibraryViewStatus.Ready:RebirthBackpackLibraryViewStatus.NoBackpack;}}
public static class Log{public static void Warning(string s){}}
public static class NetPackageManager{public static int GetPackageId(Type t)=>1;public static T GetPackage<T>() where T:new()=>new();}
public class NetPackageRebirthBackpackLibraryViewRequest{public object Setup(int p,string c,Guid g)=>this;}
public class NetPackageRebirthBackpackSellStashViewRequest{public object Setup(int p,string c,Guid g)=>this;}
public class NetPackageRebirthBackpackLibraryView{}
public class NetPackageRebirthBackpackSellStashView{}
static class Program{
static int checks;static void C(bool b,string n){if(!b)throw new Exception(n);checks++;}
static void Main(){foreach(var creation in new[]{Guid.NewGuid().ToString("N"),"legacy-"+new string('a',64)}){
var player=new EntityPlayerLocal();var world=new World{Player=player,Entity=player};player.world=world;
void Reset(){RebirthBackpackLibraryClientViews.Reset();RebirthBackpackSellStashClientViews.Reset();world.Remote=false;world.Entity=player;player.world=world;player.Dead=false;player.Spawned=true;GameManager.Instance=new(){World=world};SingletonMonoBehaviour<ConnectionManager>.Instance=new();ThreadManager.Main=RebirthSurvivorMode.Enabled=RebirthWorldCharacterRepository.IsServerAuthority=RebirthWorldCharacterRepository.Current=true;RebirthWorldCharacterService.Value=new(){Origin=new(){CreationId=creation}};RebirthSurvivorClientState.Projection=null;RebirthBackpackLibraryServer.Backpack=false;}
Reset();C(RebirthBackpackLibraryClientViews.Request(world,7)&&RebirthBackpackSellStashClientViews.Request(world,7),"host no backpack requests");
C(RebirthBackpackLibraryClientViews.IsNoBackpack(world,7)&&RebirthBackpackSellStashClientViews.IsNoBackpack(world,7),"no backpack status");
C(RebirthBackpackLibraryClientViews.TryGetRevision(world,7,out var rev)&&rev==5,"host revision without projection");
C(!RebirthBackpackLibraryClientViews.TryGet(world,7,out _)&&!RebirthBackpackSellStashClientViews.TryGet(world,7,out _),"no backpack no view");
Reset();RebirthBackpackLibraryServer.Backpack=true;C(RebirthBackpackLibraryClientViews.Request(world,7)&&RebirthBackpackSellStashClientViews.Request(world,7),"host ready requests");C(RebirthBackpackLibraryClientViews.TryGet(world,7,out var a)&&a.CreationId==creation&&RebirthBackpackSellStashClientViews.TryGet(world,7,out var b)&&b.CreationId==creation,"host ready views");
foreach(var fault in new[]{"dead","spawn","entity","world","authority","cache","record","creation","thread","mode","server"}){
Reset();switch(fault){case "dead":player.Dead=true;break;case "spawn":player.Spawned=false;break;case "entity":world.Entity=new();break;case "world":GameManager.Instance.World=new();break;case "authority":RebirthWorldCharacterRepository.IsServerAuthority=false;break;case "cache":RebirthWorldCharacterRepository.Current=false;break;case "record":RebirthWorldCharacterService.Value.IsComplete=false;break;case "creation":RebirthWorldCharacterService.Value.Origin.CreationId="broken";break;case "thread":ThreadManager.Main=false;break;case "mode":RebirthSurvivorMode.Enabled=false;break;case "server":SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;break;}
C(!RebirthBackpackLibraryClientViews.Request(world,7)&&!RebirthBackpackSellStashClientViews.Request(world,7),"host refuses "+fault);}
Reset();C(!RebirthBackpackLibraryClientViews.Request(world,8)&&!RebirthBackpackSellStashClientViews.Request(world,8),"wrong id");
Reset();world.Remote=true;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer=new[]{new Peer()};RebirthWorldCharacterService.Value=null;RebirthSurvivorClientState.Projection=creation;C(RebirthBackpackLibraryClientViews.Request(world,7)&&RebirthBackpackSellStashClientViews.Request(world,7)&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sent==2,"remote original projection route");C(!RebirthBackpackLibraryClientViews.TryGet(world,7,out _)&&!RebirthBackpackSellStashClientViews.TryGet(world,7,out _),"remote request no invented view");
}Console.WriteLine("PASS "+checks+" actual whole client views/cache/response; native DTO/server/transport adapters, no native item validation");}}
