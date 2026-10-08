using System;
using System.Collections.Generic;
public class PlatformUserIdentifierAbs {}
public struct Vector3i {}
public class Block {}
public struct BlockValue {public Block Block;public int type;}
public struct BlockActivationCommand {public string text;public bool enabled;public BlockActivationCommand(string t,string i,bool e){text=t;enabled=e;}}
public class WorldBase {public bool remote=true;public bool IsRemote(){return remote;}public object GetTileEntity(Vector3i p){return new TileEntityWorkstation();}}
public class World:WorldBase {public EntityPlayerLocal primary;public EntityPlayerLocal GetPrimaryPlayer(){return primary;}}
public class TileEntityWorkstation {}
public class EntityAlive {}
public class EntityPlayer:EntityAlive {public World world;public int entityId=1;}
public class EntityPlayerLocal:EntityPlayer {}
public class PersistentPlayerData {public PlatformUserIdentifierAbs PrimaryId=new PlatformUserIdentifierAbs();}
public class PlayerList {public PersistentPlayerData indexed;public PersistentPlayerData GetPlayerDataFromEntityID(int id){return indexed;}}
public class GameManager {public static GameManager Instance;public World World;public PlayerList list=new PlayerList();public PersistentPlayerData local=new PersistentPlayerData();public PlayerList GetPersistentPlayerList(){return list;}public PersistentPlayerData GetPersistentLocalPlayer(){return local;}}
public static class RebirthBlockPickupService {public const string CommandName="pickup";}
public static class RebirthBlockPickupAccessService {public static bool IsAdminOrEditor(WorldBase w,EntityPlayer p){return false;}}
public static class Service {
 class CommandCacheEntry {public BlockActivationCommand[] Source,Result;public int SourceLength;}
 static Dictionary<int,CommandCacheEntry> CommandCache=new Dictionary<int,CommandCacheEntry>();
 public static PlatformUserIdentifierAbs owner;public static bool locked;
 static void RequestStateIfNeeded(WorldBase w,Vector3i p,EntityAlive e){}
 static bool IsOwner(Vector3i p,PlatformUserIdentifierAbs u){return u!=null&&ReferenceEquals(u,owner);}
 static bool IsUserAllowed(Vector3i p,PlatformUserIdentifierAbs u){return IsOwner(p,u);}
 static bool IsLocked(Vector3i p){return locked;}
 static bool HasPassword(Vector3i p){return true;}
 static bool CanOpen(WorldBase w,Vector3i p,EntityPlayer e,PlatformUserIdentifierAbs u){return IsOwner(p,u);}
 static bool CanPickup(WorldBase w,Vector3i p,TileEntityWorkstation t,PersistentPlayerData d,EntityPlayer e,bool migrate,out string denial){denial="";return d!=null&&IsOwner(p,d.PrimaryId);}
// METHODS
}
public static class Checks {
 static void Run(int mode){
 var w=new World();var p=new EntityPlayerLocal{world=w};w.primary=p;var g=new GameManager{World=w};GameManager.Instance=g;Service.owner=g.local.PrimaryId;Service.locked=mode==1;
 if(mode==2)w.primary=new EntityPlayerLocal{world=w};if(mode==3)w.remote=false;if(mode==4)g.World=new World();if(mode==5)p.world=new World();
 if(mode==6)g.list.indexed=new PersistentPlayerData();if(mode==7){g.list.indexed=new PersistentPlayerData();Service.owner=g.list.indexed.PrimaryId;}
 var source=new[]{new BlockActivationCommand("open","",false),new BlockActivationCommand("pickup","",false)};
 var r=Service.ConfigureCommands(source,w,new Vector3i(),new BlockValue{Block=new Block(),type=1},p);
 bool recognized=mode==0||mode==1||mode==7;
 if(r[0].enabled!=recognized||r[1].enabled!=recognized||r[2].enabled!=(recognized&&!Service.locked)||r[3].enabled!=(recognized&&Service.locked))throw new Exception("mode "+mode);
 if(source[0].enabled||source[1].enabled)throw new Exception("Mutated original command array");
 }
 public static void Main(){for(int i=0;i<8;i++)Run(i);Console.WriteLine("PASS: owner menu join fallback, lock/unlock, indexed precedence, wrong-player/world and host exclusion");}
}
