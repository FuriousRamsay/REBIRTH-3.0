using System;using System.Collections.Generic;using System.Reflection;
namespace HarmonyLib { public static class Priority{public const int First=800;} public class HarmonyMethod{public int priority;public HarmonyMethod(Type t,string n){}} public class Harmony{public Harmony(string s){}public void Patch(MethodBase m,HarmonyMethod prefix=null,HarmonyMethod finalizer=null,HarmonyMethod transpiler=null,HarmonyMethod postfix=null){}public void UnpatchSelf(){}} }
namespace UnityEngine.Scripting{public class PreserveAttribute:Attribute{}}
namespace UnityEngine{public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>default;}}
public interface IModApi{void InitMod(Mod m);}public class Mod{}
public struct Vector3i{public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
public struct BlockValueRef{public Vector3i BlockPosition;public bool TryGetBlockPos(out Vector3i p){p=BlockPosition;return true;}}
public struct BlockValue{public Block Block;public bool ischild;public int type,damage;public bool isair=>Block==null;}
public class MultiBlock{public Vector3i Parent;public Vector3i GetParentPos(Vector3i p,BlockValue b)=>Parent;}
public class Properties{public string Shell;public string GetString(string s)=>Shell;}
public class Block{public MultiBlock multiBlockPos;public int MaxDamage=800;public Properties Properties=new();}
public class BlockWorkstation:Block{public void PlaceBlock(){}public void OnBlockRemoved(){}}
public class TileEntityWorkstation{public bool IsPlayerPlaced;}
public class WorldBase{public bool Remote;public Dictionary<Vector3i,BlockValue> Blocks=new();public Dictionary<Vector3i,TileEntityWorkstation> Stations=new();public Dictionary<int,object> Players=new();public bool IsRemote()=>Remote;public BlockValue GetBlock(Vector3i p)=>Blocks.TryGetValue(p,out var v)?v:default;public object GetTileEntity(Vector3i p)=>Stations.TryGetValue(p,out var v)?v:null;public object GetEntity(int id)=>Players.TryGetValue(id,out var p)?p:null;}
namespace HarmonyLib {
 public class CodeInstruction{public System.Reflection.Emit.OpCode opcode;public object operand;public CodeInstruction(System.Reflection.Emit.OpCode op,object value=null){opcode=op;operand=value;}public bool IsLdloc()=>opcode.Name.StartsWith("ldloc")&&!opcode.Name.StartsWith("ldloca");}
 public static class AccessTools{public static MethodInfo Method(Type t,string n)=>t.GetMethod(n,BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);public static FieldInfo Field(Type t,string n)=>t.GetField(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static);}
}
public class Explosion{public WorldBase world;public int entityId;public void AttackBlocks(){}}
public class BlockChangeInfo{public BlockValueRef blockValueRef;public BlockValue blockValue;}
internal static class RebirthWorldStationMigration{internal static void Install(HarmonyLib.Harmony h){}}
public class EntityVehicle{public EntityPlayer Driver; public object GetFirstAttached()=>Driver;}
public class God{public bool Value;}
public class Inventory{public ItemClass holdingItem;}
public class EntityAlive{}
public static class BlockPlacement{public class Result{public Vector3i blockPos;}}
public static class RebirthBlockPickupPatchInstaller{public static bool Active=true;}
public class EntityPlayer:EntityAlive{public int entityId;public God IsGodMode=new();public Inventory inventory=new();}
public class ItemClass{public string Name;public string GetItemName()=>Name;public static ItemValue GetItem(string n)=>new(){type=string.IsNullOrEmpty(n)?0:1};}
public class ItemValue{public int type;}
public class ItemStack{public int Count;public ItemStack(ItemValue v,int n){Count=n;}}
public class PlatformUserIdentifierAbs{public string Id;}
public class Persistent{public PlatformUserIdentifierAbs PrimaryId;}
public class PlayerList{public Dictionary<int,Persistent> Players=new();public Persistent GetPlayerDataFromEntityID(int id)=>Players.TryGetValue(id,out var p)?p:null;}
public class GameManager{public static GameManager Instance=new();public PlayerList Users=new();public int Drops;public PlayerList GetPersistentPlayerList()=>Users;public void ItemDropServer(ItemStack s,UnityEngine.Vector3 p,UnityEngine.Vector3 random,int actor,float lifetime,bool relative){Drops+=s.Count;}}
public static class RebirthWorkstationSecurityService{public static void RegisterPlaced(WorldBase w,Vector3i p,EntityAlive e){}public static void Remove(Vector3i p){}public static Dictionary<Vector3i,PlatformUserIdentifierAbs> Owners=new();public static bool TryGetOwner(Vector3i p,out PlatformUserIdentifierAbs o)=>Owners.TryGetValue(p,out o);public static bool IsOwner(Vector3i p,PlatformUserIdentifierAbs u)=>u!=null&&Owners.TryGetValue(p,out var o)&&o.Id==u.Id;}
class Program
{
 static int passed;static void Check(bool b,string n){if(!b)throw new Exception(n);passed++;}
 static void Main(){var pos=new Vector3i(10,4,6);var world=new WorldBase();var player=new EntityPlayer{entityId=1};world.Players[1]=player;GameManager.Instance.Users.Players[1]=new(){PrimaryId=new(){Id="one"}};var block=new BlockWorkstation();var value=new BlockValue{Block=block,type=10};world.Blocks[pos]=value;world.Stations[pos]=new(){IsPlayerPlaced=true};var hit=new BlockValueRef{BlockPosition=pos};int result=123;
 Check(!RebirthWorkstationDamagePolicy.BeforeDamage(world,hit,value,5,1,ref result)&&result==0,"Unknown placed owner denied");
 RebirthWorkstationSecurityService.Owners[pos]=new(){Id="two"};Check(!RebirthWorkstationDamagePolicy.BeforeDamage(world,hit,value,5,1,ref result),"Other owner denied");
 Check(RebirthWorkstationExplosionProtection.Filter(20,new Explosion{world=world,entityId=1},new BlockChangeInfo{blockValueRef=hit,blockValue=value})==0,"Explosion cannot damage another owner station");
 world.Players[9]=new EntityVehicle{Driver=player};Check(!RebirthWorkstationDamagePolicy.BeforeDamage(world,hit,value,5,9,ref result),"Other owner vehicle damage denied");
 player.IsGodMode.Value=true;Check(RebirthWorkstationDamagePolicy.BeforeDamage(world,hit,value,5,1,ref result),"Actual god mode allowed");player.IsGodMode.Value=false;
 RebirthWorkstationSecurityService.Owners[pos]=new(){Id="one"};Check(RebirthWorkstationDamagePolicy.BeforeDamage(world,hit,value,5,1,ref result),"Owner allowed");
 RebirthWorkstationSecurityService.Owners.Clear();world.Stations[pos].IsPlayerPlaced=false;Check(RebirthWorkstationDamagePolicy.BeforeDamage(world,hit,value,5,1,ref result),"World station allowed");world.Stations[pos].IsPlayerPlaced=true;
 Check(RebirthWorkstationDamagePolicy.BeforeDamage(world,hit,value,-5,1,ref result),"Repair allowed");Check(RebirthWorkstationDamagePolicy.BeforeDamage(world,hit,value,5,42,ref result),"Environmental damage native");
 var child=new Vector3i(11,4,6);block.multiBlockPos=new(){Parent=pos};var cv=value;cv.ischild=true;Check(!RebirthWorkstationDamagePolicy.BeforeDamage(world,new(){BlockPosition=child},cv,5,1,ref result),"Child uses owner root");
 world.Stations[pos].IsPlayerPlaced=false;block.Properties.Shell="Repairable";player.inventory.holdingItem=new(){Name="meleeToolSalvageT1Wrench"};
 void Hit(bool removed,bool harvest=true){int damage=10;RebirthWorkstationSalvage.BeforeDamage(world,hit,value,ref damage,1,harvest,out var state);if(removed)world.Blocks[pos]=default;RebirthWorkstationSalvage.AfterDamage(null,state);world.Blocks[pos]=value;}
 Hit(false);Check(GameManager.Instance.Drops==0,"No award before destruction");Hit(true);Check(GameManager.Instance.Drops==1,"World wrench awards exactly one");
 world.Stations[pos].IsPlayerPlaced=true;Hit(true);Check(GameManager.Instance.Drops==1,"Placed station never shell");player.IsGodMode.Value=true;Hit(true);Check(GameManager.Instance.Drops==1,"God mode does not bypass salvage exclusion");world.Stations[pos].IsPlayerPlaced=false;player.IsGodMode.Value=false;
 player.inventory.holdingItem.Name="meleeToolAxe";Hit(true);Check(GameManager.Instance.Drops==1,"Wrong tool excluded");player.inventory.holdingItem.Name="meleeToolSalvageT2Ratchet";Hit(true);Check(GameManager.Instance.Drops==2,"Ratchet supported");player.inventory.holdingItem.Name="meleeToolSalvageT3ImpactDriver";Hit(true);Check(GameManager.Instance.Drops==3,"Impact supported");Hit(true,false);Check(GameManager.Instance.Drops==3,"Harvest flag required");
 int d=5;RebirthWorkstationSalvage.BeforeDamage(world,hit,value,ref d,1,true,out var outer);Check(d==5,"Native damage retained");RebirthWorkstationSalvage.BeforeDamage(world,hit,value,ref d,1,true,out var inner);world.Blocks[pos]=default;RebirthWorkstationSalvage.AfterDamage(null,inner);RebirthWorkstationSalvage.AfterDamage(null,outer);Check(GameManager.Instance.Drops==4,"Nested native overrides single award");world.Blocks[pos]=value;
 RebirthWorkstationSalvage.BeforeDamage(world,hit,value,ref d,1,true,out var failed);world.Blocks[pos]=default;RebirthWorkstationSalvage.AfterDamage(new Exception(),failed);Check(GameManager.Instance.Drops==4,"Exceptional damage no award");world.Blocks[pos]=value;world.Remote=true;Hit(true);Check(GameManager.Instance.Drops==4,"Remote cannot award");world.Remote=false;world.Stations.Clear();Hit(true);Check(GameManager.Instance.Drops==4,"Missing provenance cannot award");
 Console.WriteLine($"PASS {passed} actual-source station ownership/salvage checks (offline doubles; no gameplay claim)."); }
}
