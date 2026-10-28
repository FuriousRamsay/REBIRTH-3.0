using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;using System.Text;using System.Security.Cryptography;using System.Xml;using System.Xml.Linq;
// AUTHORED_NOT_RUN. Actual journal and store; native consumption/identity/item codec are doubles.
namespace UnityEngine{static class Time{public static float realtimeSinceStartup;}}
class WorldState{public string Guid="world";}
class World {public WorldState worldState=new WorldState();public EntityPlayer Player;public bool IsRemote()=>false;public EntityPlayer GetEntity(int id)=>Player?.entityId==id?Player:null;public EntityPlayerLocal GetPrimaryPlayer()=>Player as EntityPlayerLocal;}
class EntityPlayer {public World world;public int entityId=1,Died;public bool IsDead()=>false;}
class EntityPlayerLocal:EntityPlayer{}
class PlayerData{public PlatformUserIdentifierAbs PrimaryId=new PlatformUserIdentifierAbs();}
class PlayerList{public PlayerData GetPlayerDataFromEntityID(int id)=>new PlayerData();}
class GameManager{public static GameManager Instance=new GameManager();public World World;public PlayerList GetPersistentPlayerList()=>new PlayerList();}
interface INetConnection{bool IsDisconnected();}
class ClientInfo{public bool loginDone=true,disconnecting;public INetConnection[] netConnection;}
class Clients{public ClientInfo ForEntityId(int id)=>null;}
class ConnectionManager{public bool IsServer=true;public Clients Clients=new Clients();}
static class RebirthCamperCharacterAccess{public static string CurrentCreation(EntityPlayer player)=>"fixture-life";}
static class GameIO{public static string Root;public static string GetSaveGameDir()=>Root;}
static class RemoteResourceWorldIdentity{public static bool TryGet(World world,out string key){key=new string('a',64);return ReferenceEquals(GameManager.Instance.World,world);}}
static class SingletonMonoBehaviour<T>where T:new(){public static T Instance=new T();}
static class ThreadManager{public static bool IsMainThread()=>true;}
static class RebirthCamperInventorySnapshot{
public static byte[] Capture(ItemStack[] values){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){foreach(var v in values)w.Write(v.count);w.Flush();return s.ToArray();}}
public static ItemStack[] Restore(byte[] bytes){using(var s=new MemoryStream(bytes))using(var r=new BinaryReader(s)){var values=new List<ItemStack>();while(s.Position<s.Length)values.Add(new ItemStack{count=r.ReadInt32()});return values.ToArray();}}}
class PlatformUserIdentifierAbs {public string CombinedString="owner";}
enum RemoteResourceClientOperation:byte {Craft=1,CookingPull=5}
enum RemoteResourceTransactionOutcomeState:byte {Unknown,Success,Failed}
class ItemStack {public int count;public ItemStack Clone(){return new ItemStack{count=count};}}
static class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld(){return Enabled;}}
static class RemoteResourcesRuntimePolicy {public static bool Enabled=true;}
static class RemoteResourceTransactions {
public static int Calls; public static int Mode;
public class ConsumptionResult {public bool Success;public string FailureReason="";}
public static ConsumptionResult TryConsume(EntityPlayer p,IList<ItemStack> needs,int multiplier,List<ItemStack> removed,bool local){Calls++;if(Mode==0)throw new Exception("before debit");removed.Add(new ItemStack{count=3});if(Mode==1)throw new Exception("after debit");return new ConsumptionResult{Success=Mode==2,FailureReason="failed"};}}
// PRODUCTION_METHOD
// PRODUCTION_STORE
class Check {
static int assertions;
static void Need(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
static void NewSave(){GameIO.Root=@"\\?\"+Path.Combine(Path.GetTempPath(),"rebirth-outcome-"+Guid.NewGuid().ToString("N"));string path=Path.Combine(GameIO.Root,"RebirthData","RemoteResources");Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"worldIdentity.xml"),"<remoteResourceWorld version=\"1\" key=\""+new string('a',64)+"\" />");}
// Fixture-only historical migration seed: actual store Capture/entered/observed
// publishers describe the pre-cutover state. Reflection inserts only its old
// warm journal indexes. This is not a production legacy-admission API.
static RemoteResourceTransactionOutcome SeedHistorical(World world,EntityPlayerLocal player,PlatformUserIdentifierAbs user,ulong request,RemoteResourceClientOperation kind,ItemStack[] needs,int mode){
 var type=typeof(RemoteResourceTransactionOutcomeJournal);var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
 type.GetMethod("BindWorld",flags).Invoke(null,new object[]{world});
 RemoteResourceLegacyOutcomeStore.Entry entry;
 Need(RemoteResourceLegacyOutcomeStore.Entry.Capture(world,player,user,1,request,kind,needs,out entry)&&entry.TryEnter(player),"historical fixture entry");
 var removed=new List<ItemStack>();RemoteResourceTransactionOutcome observed;
 RemoteResourceTransactions.Mode=mode;
 try{var native=RemoteResourceTransactions.TryConsume(player,needs,1,removed,false);observed=new RemoteResourceTransactionOutcome{State=native.Success?RemoteResourceTransactionOutcomeState.Success:RemoteResourceTransactionOutcomeState.Failed,Removed=removed};}
 catch{observed=new RemoteResourceTransactionOutcome{State=RemoteResourceTransactionOutcomeState.Unknown,Removed=removed};}
 entry.Observe(observed);Need(entry.PublishObserved(),"historical observed publication");
 string key=(string)type.GetMethod("BuildKey",flags).Invoke(null,new object[]{user,(ulong)1,request,kind});
 ((System.Collections.IDictionary)type.GetField("Outcomes",flags).GetValue(null))[key]=observed;
 ((System.Collections.IDictionary)type.GetField("Originals",flags).GetValue(null))[key]=entry;
 using(var hash=SHA256.Create())((System.Collections.IDictionary)type.GetField("OriginalRequirements",flags).GetValue(null))[key]=Convert.ToBase64String(hash.ComputeHash(RebirthCamperInventorySnapshot.Capture(needs)));
 return observed;
}
static void Main(){var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance.World=world;var user=new PlatformUserIdentifierAbs();var needs=new[]{new ItemStack{count=3}};NewSave();
#if LEGACY_CAPACITY
 int maximum=(int)typeof(RemoteResourceTransactionOutcomeJournal).GetField("MaxEntries",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetRawConstantValue();
 for(ulong request=1;request<=(ulong)maximum;request++)SeedHistorical(world,player,user,request,RemoteResourceClientOperation.Craft,needs,2);
 int calls=RemoteResourceTransactions.Calls;
 Need(RemoteResourceTransactionOutcomeJournal.Resolve(world,player,user,1,(ulong)maximum+1,RemoteResourceClientOperation.Craft,needs,false).State==RemoteResourceTransactionOutcomeState.Unknown&&RemoteResourceTransactions.Calls==calls,"fresh legacy cannot bypass capacity/cutover");
 Need(RemoteResourceTransactionOutcomeJournal.Resolve(world,player,user,1,1,RemoteResourceClientOperation.Craft,null,true).Removed.Single().count==3,"oldest historical original retained");
 Need(RemoteResourceTransactionOutcomeJournal.Resolve(world,player,user,1,1,RemoteResourceClientOperation.Craft,new[]{new ItemStack{count=2}},false).State==RemoteResourceTransactionOutcomeState.Unknown&&RemoteResourceTransactions.Calls==calls,"changed identity stays unknown");
#else
 foreach(var operation in new[]{RemoteResourceClientOperation.Craft,RemoteResourceClientOperation.CookingPull}){
  RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload(world);NewSave();RemoteResourceTransactions.Calls=0;
  foreach(bool query in new[]{false,true,false})Need(RemoteResourceTransactionOutcomeJournal.Resolve(world,player,user,1,99,operation,needs,query).State==RemoteResourceTransactionOutcomeState.Unknown&&RemoteResourceTransactions.Calls==0,"fresh legacy cannot remove or declare unused");
  Need(!Directory.Exists(Path.Combine(GameIO.Root,"RebirthData","RemoteResources","LegacyOutcomes")),"fresh refusal must not create a source original");
  for(int mode=0;mode<4;mode++){
   ulong request=(ulong)mode+1;var historical=SeedHistorical(world,player,user,request,operation,needs,mode);int calls=RemoteResourceTransactions.Calls;
   foreach(bool query in new[]{true,false}){
    var replay=RemoteResourceTransactionOutcomeJournal.Resolve(world,player,user,1,request,operation,needs,query);
    Need(replay.State==historical.State&&replay.Removed.Count==historical.Removed.Count&&RemoteResourceTransactions.Calls==calls,"historical query/replay changed or reissued native removal");
    if(replay.Removed.Count>0)replay.Removed[0].count=999;
   }
   Need(RemoteResourceTransactionOutcomeJournal.Resolve(world,player,user,1,request,operation,new[]{new ItemStack{count=2}},false).State==RemoteResourceTransactionOutcomeState.Unknown&&RemoteResourceTransactions.Calls==calls,"changed historical requirements must retain Unknown");
  }
  RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload(world);int beforeCold=RemoteResourceTransactions.Calls;
  Need(RemoteResourceTransactionOutcomeJournal.Resolve(world,player,user,1,1,operation,needs,false).State==RemoteResourceTransactionOutcomeState.Unknown&&RemoteResourceTransactions.Calls==beforeCold,"cold descriptive old file cannot authorize a new removal");
 }
#endif
 Console.WriteLine(assertions+" actual legacy cutover/historical retention assertions; native source durability remains NOT_RUN");}}
