// AUTHORED_NOT_RUN. Actual whole service and disk outcomes; native binding,
// native publication, actor scope and grid journal are explicit adapters.
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;
enum RemoteResourceSourceKind{StaticContainer=1,WorkstationOutput=2,VehicleStorage=3,DroneStorage=4}
class ItemValue{internal string Rich="plain";internal ItemClass ItemClass=new ItemClass();internal ItemValue Clone()=>new ItemValue{Rich=Rich};}
class Limit{internal int Value=10;}class ItemClass{internal Limit Stacknumber=new Limit();}
class ItemStack{internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue value,int count){itemValue=value;this.count=count;}internal bool IsEmpty()=>count<=0;internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);internal static ItemStack[] Clone(ItemStack[] items)=>items.Select(value=>value.Clone()).ToArray();}
class ItemStackGrid{internal object holder;}class Bag{internal ItemStackGrid ItemGrid;}
class TEFeatureStorage{internal object Parent;}class TileEntityComposite{}
class TileEntityWorkstation{internal ItemStack[] Output{get=>Model.Slots;set{if(RebirthCookingNativeReturn.Consume(this,value)){Model.Slots=ItemStack.Clone(value);Model.Calls++;}}}}
class EntityVehicle{}class EntityDrone{}
class EntityPlayerLocal{internal World world;internal int entityId=1,Died=1;internal bool IsDead()=>false;}
class World{internal EntityPlayerLocal Player;internal bool IsRemote()=>false;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;internal object GetEntity(int id)=>Player;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class GameIO{internal static string Save;internal static string GetSaveGameDir()=>Save;}
class RebirthRemoteResourceNetworkScope{internal static bool Live=true;internal bool Current=>Live;internal static RebirthRemoteResourceNetworkScope Capture(EntityPlayerLocal player)=>new RebirthRemoteResourceNetworkScope();}
static class RebirthStationGridIngredients{internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count&&(a.IsEmpty()&&b.IsEmpty()||a.itemValue.Rich==b.itemValue.Rich);}
static class RebirthCamperInventorySnapshot{internal static byte[] Capture(ItemStack[] items)=>Encoding.UTF8.GetBytes(string.Join(";",items.Select(value=>value.count+":"+value.itemValue.Rich)));internal static ItemStack[] Restore(byte[] data)=>Encoding.UTF8.GetString(data).Split(';').Select(value=>{var pair=value.Split(':');return new ItemStack(new ItemValue{Rich=pair[1]},int.Parse(pair[0]));}).ToArray();}
static class RemoteResourceTransactions{internal static long MeasureOriginalNativeFrame(byte[] bytes)=>bytes.Length*4;}
static class RebirthRemoteResourceDurableJournal{internal static string Digest(string text){using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}}
static class RebirthRemoteResourceSourceCustody{internal static bool Blocks(object target)=>Model.Foreign;}
static class RebirthCookingRemoteAdmission{internal static XElement ReadProtocolXml(string text,int maximum){if(Encoding.UTF8.GetByteCount(text)>maximum)throw new IOException();return XElement.Parse(text);}}
static class RebirthCookingNpcReturn{internal readonly struct Result{internal readonly int Count;internal readonly bool Pending;internal Result(int count,bool pending){Count=count;Pending=pending;}}}
class RebirthCamperTransferJournal
{
 internal enum Phase{Prepared,Uncertain,Applied,Rejected}
 internal sealed class Record{internal Guid Operation;internal byte[] Before,After;internal Phase State=Phase.Uncertain;}
 internal static Record ReadExisting(string directory,Guid operation)=>Model.Record?.Operation==operation?Model.Record:null;
}
static class RebirthCookingGridCustody
{
 internal static bool NativeSourceReturnPending(Guid operation,out RebirthCamperTransferJournal.Record record){record=NativeSourceReturnOriginals().SingleOrDefault(value=>value.Operation==operation);return record!=null;}
 internal static IEnumerable<RebirthCamperTransferJournal.Record> NativeSourceReturnOriginals(){if(Model.Record!=null&&(Model.Record.State==RebirthCamperTransferJournal.Phase.Prepared||Model.Record.State==RebirthCamperTransferJournal.Phase.Uncertain))yield return Model.Record;}
 internal static bool TryNativeSourceReturn(Guid operation,out EntityPlayerLocal player,out RebirthCamperTransferJournal.Record record){player=Model.Player;record=NativeSourceReturnOriginals().SingleOrDefault(value=>value.Operation==operation);return record!=null;}
}
class RebirthRemoteResourceNativeBinding
{
 internal object Endpoint;internal World World;internal ItemStackGrid Grid;internal TileEntityWorkstation Station=>Endpoint as TileEntityWorkstation;
 internal bool Current()=>ReferenceEquals(GameManager.Instance.World,World)&&ReferenceEquals(Endpoint,Model.Endpoint)&&Model.Incarnation=="original-incarnation";
 internal int Revision=>Model.Slots.Sum(value=>value.count);internal ItemStack[] CopySlots()=>Current()?ItemStack.Clone(Model.Slots):null;internal bool[] CopyLocks()=>Current()?(bool[])Model.Locks.Clone():null;
 internal bool IsAuthorized(EntityPlayerLocal player,out string reason){reason=null;return Current()&&Model.Authorized;}
 internal void SetSlot(int slot,ItemStack item){var argument=item.Clone();if(!RebirthCookingNativeReturn.Consume(Grid,slot,argument))return;Model.Calls++;if(Model.Mode=="suppressed")return;Model.Slots[slot]=argument;if(Model.Mode=="throw-after")throw new IOException();}
 internal void MarkModified(){Model.Modified++;}
 internal static bool TryResolve(World world,string id,RemoteResourceSourceKind kind,string incarnation,out RebirthRemoteResourceNativeBinding value){value=null;if(id!="source"||incarnation!=Model.Incarnation||!ReferenceEquals(world,GameManager.Instance.World))return false;value=new RebirthRemoteResourceNativeBinding{World=world,Endpoint=Model.Endpoint,Grid=Model.Grid};return value.Current();}
}
static class RebirthRemoteResourceNativePublication
{
 internal sealed class Watch:IDisposable{internal Func<bool> Scope;internal bool Current()=>Scope();internal bool Saved()=>Current()&&Model.Saved;public void Dispose(){}}
 internal static Watch Begin(RebirthRemoteResourceNativeBinding source,Func<bool> current){Model.Watches++;return new Watch{Scope=current};}
}
static class RebirthProductiveEarnedObservationBudget{sealed class Memory:IDisposable{public void Dispose(){}}internal static bool Available=true;internal static bool TryRetainDetached(long bytes,out IDisposable memory){memory=Available?new Memory():null;return memory!=null;}}
static class Model
{
 internal static EntityPlayerLocal Player;internal static object Endpoint;internal static ItemStackGrid Grid;internal static ItemStack[] Slots;internal static bool[] Locks;internal static string Incarnation,Mode;internal static bool Authorized,Foreign,Saved;internal static int Calls,Modified,Watches;internal static RebirthCamperTransferJournal.Record Record;
 internal static void Reset(RemoteResourceSourceKind kind=RemoteResourceSourceKind.StaticContainer){var world=new World();Player=new EntityPlayerLocal{world=world};world.Player=Player;GameManager.Instance.World=world;Endpoint=kind==RemoteResourceSourceKind.WorkstationOutput?(object)new TileEntityWorkstation():kind==RemoteResourceSourceKind.VehicleStorage?new EntityVehicle():kind==RemoteResourceSourceKind.DroneStorage?new EntityDrone():new TileEntityComposite();Grid=new ItemStackGrid{holder=Endpoint};Slots=new[]{new ItemStack(new ItemValue(),2),new ItemStack(new ItemValue{Rich="other"},5)};Locks=new[]{false,true};Incarnation="original-incarnation";Mode="normal";Authorized=Saved=true;Foreign=false;Calls=Modified=Watches=0;Record=null;RebirthRemoteResourceNetworkScope.Live=true;RebirthProductiveEarnedObservationBudget.Available=true;}
}
// ACTUAL_NATIVE_RETURN
static class Program
{
 static int assertions;static void Need(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 static XElement Admit(RemoteResourceSourceKind kind=RemoteResourceSourceKind.StaticContainer,int count=3)
 {
  Need(RebirthCookingNativeReturn.Plan(Model.Player,"source",kind,"original-incarnation",0,new ItemStack(new ItemValue(),count),out var native)==count,"fresh native plan count");
  var selected=new XElement("sourceReturn",new XAttribute("origin","original-source-slot"),new XAttribute("slot",0),native,new XElement("items",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new[]{new ItemStack(new ItemValue(),count)}))));
  var root=new XElement("cookingGridOriginal",new XAttribute("action","source-return"),new XAttribute("deaths",Model.Player.Died),new XElement("beforeActor","same"),new XElement("afterActor","same"),selected);
  byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));Model.Record=new RebirthCamperTransferJournal.Record{Operation=Guid.NewGuid(),Before=bytes,After=(byte[])bytes.Clone()};RebirthCookingNativeReturn.Refresh();return selected;
 }
 static void Main()
 {
  GameIO.Save=Path.Combine(Path.GetTempPath(),"rebirth-native-return-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(GameIO.Save);
  foreach(RemoteResourceSourceKind kind in Enum.GetValues(typeof(RemoteResourceSourceKind)))
  {
   Model.Reset(kind);var selected=Admit(kind);var operation=Model.Record.Operation;
   Need(RebirthCookingNativeReturn.Blocks(Model.Endpoint),"pending native source lacks hold");
   Need(RebirthCookingNativeReturn.Advance(operation)==1&&Model.Calls==1&&Model.Slots[0].count==5&&Model.Slots[1].count==5&&Model.Locks[1],"fresh additive source return/preserved neighbour");
   Need(RebirthCookingNativeReturn.Advance(operation)==1&&Model.Calls==1,"published source return repeated setter");
   Need(RebirthCookingNativeReturn.TryReadOutcome(operation,selected,out var outcome)&&!outcome.Pending&&outcome.Count==3,"saved native outcome missing");
   Model.Record.State=RebirthCamperTransferJournal.Phase.Applied;Need(!RebirthCookingNativeReturn.Blocks(Model.Endpoint),"terminal grid child retained native hold");
   Need(RebirthCookingNativeReturn.TryReadOutcome(operation,selected,out outcome)&&outcome.Count==3,"Applied source outcome cannot hydrate off-bag metadata");
  }
  Model.Reset();Admit();Model.Saved=false;Need(RebirthCookingNativeReturn.Advance(Model.Record.Operation)==-1&&Model.Calls==1,"unsaved native effect acknowledged");Model.Saved=true;Need(RebirthCookingNativeReturn.Advance(Model.Record.Operation)==1&&Model.Calls==1,"native save retry repeated setter");
  foreach(string mode in new[]{"suppressed","throw-after"}){Model.Reset();Admit();Model.Mode=mode;Need(RebirthCookingNativeReturn.Advance(Model.Record.Operation)==-1&&Model.Calls==1,"unknown native call acknowledged");Model.Mode="normal";int phase=RebirthCookingNativeReturn.Advance(Model.Record.Operation);Need(Model.Calls==1&&(mode=="suppressed"?phase==-1:phase==1),"unknown native call repeated instead of observing exact after");}
  Model.Reset();Admit();Model.Slots[0].count++;Need(RebirthCookingNativeReturn.Advance(Model.Record.Operation)==0&&Model.Calls==0,"changed native Before did not refuse without invocation");
  Model.Reset();Admit();Model.Authorized=false;Need(RebirthCookingNativeReturn.Advance(Model.Record.Operation)==0&&Model.Calls==0,"revoked native authorization called setter");
  Model.Reset();Admit();RebirthRemoteResourceNetworkScope.Live=false;Need(RebirthCookingNativeReturn.Advance(Model.Record.Operation)==-1&&Model.Calls==0,"replaced native network scope called setter");
  Model.Reset();Admit();Model.Player.Died++;Need(RebirthCookingNativeReturn.Advance(Model.Record.Operation)==-1&&Model.Calls==0,"new life started original native return");
  Model.Reset();Admit();Model.Incarnation="replacement";Need(RebirthCookingNativeReturn.Advance(Model.Record.Operation)==-1&&Model.Calls==0,"replacement native endpoint inherited return");
  Model.Reset();var cold=Admit();var oldOperation=Model.Record.Operation;Model.Saved=false;RebirthCookingNativeReturn.Advance(oldOperation);var nextWorld=new World();Model.Player=new EntityPlayerLocal{world=nextWorld};nextWorld.Player=Model.Player;GameManager.Instance.World=nextWorld;Model.Saved=true;Need(RebirthCookingNativeReturn.Advance(oldOperation)==-1&&Model.Calls==1,"cold entered-without-outcome repeated native deposit");
  Model.Reset();Admit();Model.Record.State=RebirthCamperTransferJournal.Phase.Prepared;Need(RebirthCookingNativeReturn.Advance(Model.Record.Operation)==-1&&Model.Calls==0,"Prepared owner original admitted source effect");
  Model.Reset();Model.Locks[0]=true;Need(RebirthCookingNativeReturn.Plan(Model.Player,"source",RemoteResourceSourceKind.StaticContainer,"original-incarnation",0,new ItemStack(new ItemValue(),3),out _)==0,"locked native slot admitted source return");
  Model.Reset();Model.Foreign=true;Need(RebirthCookingNativeReturn.Plan(Model.Player,"source",RemoteResourceSourceKind.StaticContainer,"original-incarnation",0,new ItemStack(new ItemValue(),3),out _)==0,"foreign source hold admitted source return");
  Console.WriteLine("PASS actual native source return assertions="+assertions+"; native saved publication/binding and grid journal are adapters");
 }
}
