// AUTHORED_NOT_RUN. Complete production hidden-output server and installed
// native/UI guards. Journal IO, native file receipts and save scheduling adapt.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using HarmonyLib;
using Directory=NativeDirectory;
namespace HarmonyLib {class HarmonyPatch:Attribute {internal HarmonyPatch(Type t,string name){}}class HarmonyPriority:Attribute {internal HarmonyPriority(int p){}}static class Priority {internal const int First=1;}}
namespace UnityEngine {static class Time {internal static float realtimeSinceStartup;}}
static class ThreadManager {internal static bool IsMainThread()=>true;}
class ItemValue {internal Dictionary<string,string> Values=new Dictionary<string,string>();internal bool HasMetadata(string key)=>Values.ContainsKey(key);internal bool TryGetMetadata(string key,out string value)=>Values.TryGetValue(key,out value);internal void SetMetadata(string key,string value){Values[key]=value;}}
class ItemStack {internal string Rich="rich";internal int count;internal bool IsEmpty()=>count==0;internal ItemStack Clone()=>new ItemStack{Rich=Rich,count=count};internal static ItemStack[] Clone(ItemStack[] a)=>a.Select(i=>i.Clone()).ToArray();internal static ItemStack[] CreateArray(int n)=>Enumerable.Range(0,n).Select(i=>new ItemStack{Rich="",count=0}).ToArray();}
class TileEntityWorkstation {internal ItemStack[] Output;internal string Location="station",QueueKey="queue";internal int Dirty;internal void setModified(){Dirty++;}}
class RebirthCamperStationState:TileEntityWorkstation {}
class EntityPlayer {internal World world;internal int entityId=1,Died=2;internal Belt inventory=new Belt();internal class Belt {internal int Length=10;}}
class World {internal State worldState=new State();internal bool Remote;internal class State {internal string Guid="11111111111111111111111111111111";}internal bool IsRemote()=>Remote;internal TileEntityWorkstation GetTileEntity(int position)=>position==1?Model.Station:null;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
class NetPackageTileEntity {internal int teWorldPos=1;internal void ProcessPackage(World world,GameManager callbacks){}}
class XUiM_Workstation {internal TileEntityWorkstation TileEntity;internal void SetOutputStacks(ItemStack[] items){}}
static class GameIO {internal static string GetSaveGameDir()=>"hidden-output-adapter-save";}
static class RebirthCookingStationLocator {internal static string Encode(TileEntityWorkstation station)=>station.Location;}
class RebirthStablePlayerIdentity {internal string CanonicalId="owner";}
static class RebirthWorldCharacterService {internal static bool TryGetIdentity(EntityPlayer actor,out RebirthStablePlayerIdentity id){id=new RebirthStablePlayerIdentity{CanonicalId=Model.Owner};return Model.Identity;}}
static class RebirthCookingRemoteAdmission {internal const string Pending="pending";internal static string SourceHash(ItemStack[] bag,ItemStack[] belt,bool[] locks,int count)=>Model.Source;}
static class RebirthCookingRemoteAdmissionBackend {
    internal static bool Current(EntityPlayer actor,string location,string creation,out TileEntityWorkstation station,out object record){station=Model.Station;record=null;return Model.Current&&ReferenceEquals(actor,Model.Actor)&&ReferenceEquals(GameManager.Instance.World,actor.world)&&location==station.Location&&creation=="creation";}
    internal static Guid StationKey(string world,string location){using(var sha=SHA256.Create())return new Guid(sha.ComputeHash(Encoding.UTF8.GetBytes(world+location)).Take(16).ToArray());}
}
static class RebirthStationGridIngredients {internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.Rich==b.Rich&&a.count==b.count;}
static class RebirthCamperInventorySnapshot {
    internal static byte[] Capture(ItemStack[] items)=>Encoding.UTF8.GetBytes(new XElement("items",items.Select(i=>new XElement("item",new XAttribute("rich",i.Rich),new XAttribute("count",i.count)))).ToString(SaveOptions.DisableFormatting));
    internal static ItemStack[] Restore(byte[] bytes)=>XElement.Parse(Encoding.UTF8.GetString(bytes)).Elements().Select(n=>new ItemStack{Rich=(string)n.Attribute("rich"),count=(int)n.Attribute("count")}).ToArray();
}
static class RebirthStationPublicationRecord {internal static string Digest(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}}
static class Log {internal static void Warning(string text){}}
static class NativeDirectory {
    internal static bool Exists(string path)=>RebirthCamperTransferJournal.Files.ContainsKey(path);
    internal static IEnumerable<string> EnumerateFiles(string path,string pattern)=>RebirthCamperTransferJournal.Files[path].Keys.Select(id=>Path.Combine(path,id.ToString("N")+".0.rcj"));
}
class RebirthCamperTransferJournal {
    internal enum Phase {Prepared,Uncertain,Applied,Rejected}
    internal sealed class Record {internal Guid Operation,Vehicle;internal string OwnerCreation;internal long Revision;internal Phase State;internal byte[] Before,After;}
    internal static Dictionary<string,Dictionary<Guid,Record>> Files=new Dictionary<string,Dictionary<Guid,Record>>();internal static Action<string,Record> AfterPrepare;
    readonly string path;internal RebirthCamperTransferJournal(string value){path=value;if(!Files.ContainsKey(path))Files[path]=new Dictionary<Guid,Record>();}
    internal Record Read(Guid id)=>ReadExisting(path,id);internal static Record ReadExisting(string p,Guid id)=>Files.TryGetValue(p,out var rows)&&rows.TryGetValue(id,out var r)?r:null;
    internal IEnumerable<Record> ReadForIndex()=>Files[path].Values.ToArray();
    internal Record FindPending(Guid key)=>Files[path].Values.SingleOrDefault(r=>r.Vehicle==key&&(r.State==Phase.Prepared||r.State==Phase.Uncertain));
    internal IEnumerable<Record> FindApplied(Guid key,string creation)=>Files[path].Values.Where(r=>r.Vehicle==key&&r.OwnerCreation==creation&&r.State==Phase.Applied).ToArray();
    internal IEnumerable<Record> FindRejected(Guid key,string creation)=>Files[path].Values.Where(r=>r.Vehicle==key&&r.OwnerCreation==creation&&r.State==Phase.Rejected).ToArray();
    internal void AssertAvailable(Guid key,Guid permitted){if(FindPending(key)!=null&&FindPending(key).Operation!=permitted)throw new InvalidDataException();}
    internal Record Prepare(Guid id,Guid key,string creation,long revision,byte[] before,byte[] after){var old=Read(id);if(old!=null){if(!old.Before.SequenceEqual(before)||!old.After.SequenceEqual(after))throw new InvalidDataException();return old;}var r=new Record{Operation=id,Vehicle=key,OwnerCreation=creation,Revision=revision,Before=(byte[])before.Clone(),After=(byte[])after.Clone()};Files[path].Add(id,r);AfterPrepare?.Invoke(path,r);return r;}
    internal Record Advance(Guid id,Phase expected,Phase next){var r=Read(id);if(r==null||r.State!=expected)throw new InvalidDataException();r.State=next;return r;}
}
static class RebirthLogisticsEndpointIdentity {
    internal static void Install(){}internal static bool TryGetIncarnation(World world,TileEntityWorkstation station,out string birth){birth=Model.Birth;return birth!=null;}
    internal sealed class Admission:IDisposable {internal string Id;internal bool Closed,Seen;internal DateTime Expires;internal bool Current()=>!Closed&&Model.Current;internal bool Saved(){if(!Seen){Seen=true;return false;}return Current()&&Model.IdentitySaved;}public void Dispose(){Closed=true;}}
    internal static Admission Begin(World world,TileEntityWorkstation station,Func<bool> current){if(!current())return null;Model.IdentityBegins++;if(Model.Birth==null)Model.Birth=Guid.NewGuid().ToString("N");return new Admission{Id=Model.Birth};}
}
static class RebirthCookingOutputPlanPublication {
    sealed class Native:IDisposable {internal static int Live;internal Native(){Live++;}public void Dispose(){Live--;}}
    internal static string QueueKey(TileEntityWorkstation station)=>station.QueueKey;
    internal static IDisposable BeginOutputPublication(EntityPlayer actor,TileEntityWorkstation station,Func<bool> current,out Func<bool> saved){bool empty=station.Output.All(i=>i.IsEmpty());saved=()=>current()&&(empty?Model.EmptySaved:Model.BeforeSaved);return new Native();}
}
static class RebirthCamperStationPlayerSavedSource {internal static bool TryCopySavedSource(RebirthStablePlayerIdentity id,out ItemStack[] bag,out ItemStack[] belt,out bool[] locks){bag=new[]{new ItemStack{count=1}};belt=new ItemStack[0];locks=new[]{false};return true;}}
static class RebirthRecyclerOwnerDelivery {
    internal static bool PlanWithOverflow(ItemStack[] bag,ItemStack[] belt,bool[] locks,object unused,int limit,IList<ItemStack> outputs,out ItemStack[] afterBag,out ItemStack[] afterBelt,out ItemStack[] overflow){Model.Refund=outputs.Sum(i=>i.count);afterBag=ItemStack.Clone(bag);if(Model.Room)afterBag[0].count+=Model.Refund;afterBelt=belt;overflow=Model.Room?new ItemStack[0]:outputs.Select(i=>i.Clone()).ToArray();return true;}
}
static class RebirthCookingOwnerDelta {
    internal sealed class Plan {internal Guid Operation;internal string World,Owner,Creation,SourceHash;internal int Deaths,OverflowOwnerEntity;internal ItemStack[] BeforeBag,AfterBag,BeforeBelt,AfterBelt,Overflow;}
    static Dictionary<string,Plan> plans=new Dictionary<string,Plan>();internal static string Encode(Plan p){var text=p.Operation.ToString("N");plans[text]=p;return text;}internal static Plan Decode(string text)=>plans[text];
    internal static bool Saved(RebirthStablePlayerIdentity id,Plan p,int phase)=>p.Owner==id.CanonicalId&&p.Deaths==Model.Actor.Died&&Model.OwnerPhase==phase;
    internal static bool TryReadUnspentSource(RebirthStablePlayerIdentity id,Plan p,out ItemStack[] b,out ItemStack[] t,out bool[] l){RebirthCamperStationPlayerSavedSource.TryCopySavedSource(id,out b,out t,out l);return Model.NoStart;}
    internal static bool ObserveRejected(RebirthStablePlayerIdentity id,RebirthCamperTransferJournal.Record r)=>Model.RejectedSaved;
}
static class RebirthCookingOverflowOriginal {
    internal static int Calls;internal static bool Advance(RebirthCamperTransferJournal.Record p,EntityPlayer a,string o,string c,ItemStack[] i,int d,int e,Func<bool> current){Calls++;return Model.DropSaved&&current();}
    internal static bool Terminal(RebirthCamperTransferJournal.Record p,EntityPlayer a,string o,string c,ItemStack[] i,int d,int e)=>Model.DropSaved;
}
static class Model {
    internal static EntityPlayer Actor;internal static TileEntityWorkstation Station;internal static bool Current,Identity,IdentitySaved,BeforeSaved,EmptySaved,Room,DropSaved,NoStart,RejectedSaved;
    internal static string Owner,Birth,Source;internal static int OwnerPhase,Refund,IdentityBegins;
}
// ACTUAL_SERVER
internal static partial class RebirthCookingHiddenOutputRecovery {internal static bool ClientHold;internal static bool Held(TileEntityWorkstation station)=>ClientHold||ServerHeld(station);}
// ACTUAL_OUTPUT_GUARD
// ACTUAL_BODY_GUARD
class Program {
    static int assertions;static string request;static void Need(bool value,string label){assertions++;if(!value)throw new Exception(label);}
    static string PathFor(string name)=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name);
    static RebirthCamperTransferJournal.Record Parent=>RebirthCamperTransferJournal.Files[PathFor("CookingHiddenOutputs")].Values.Single();
    static RebirthCamperTransferJournal.Record Delta=>RebirthCamperTransferJournal.Files[PathFor("CookingHiddenOutputOwners")].Values.Single();
    static string Call(ItemValue hint=null)=>RebirthCookingHiddenOutputRecovery.Handle(Model.Actor,request,"creation","station",hint);
    static void Reset(){GameManager.Instance.World=null;RebirthCookingHiddenOutputRecovery.TickServer();RebirthCamperTransferJournal.Files.Clear();RebirthCamperTransferJournal.AfterPrepare=null;UnityEngine.Time.realtimeSinceStartup=0;
        var world=new World();Model.Actor=new EntityPlayer{world=world};GameManager.Instance.World=world;Model.Station=new TileEntityWorkstation{Output=new[]{new ItemStack{Rich="A",count=5},new ItemStack{Rich="B",count=2}}};
        Model.Current=Model.Identity=Model.IdentitySaved=Model.Room=true;Model.BeforeSaved=Model.EmptySaved=Model.DropSaved=Model.NoStart=Model.RejectedSaved=false;Model.Owner="owner";Model.Birth=Guid.NewGuid().ToString("N");Model.Source=new string('a',64);Model.OwnerPhase=Model.Refund=Model.IdentityBegins=RebirthCookingOverflowOriginal.Calls=0;request=Guid.NewGuid().ToString("N");RebirthCookingHiddenOutputRecovery.ClientHold=false;}
    static string Admit(){Need(Call()==RebirthCookingRemoteAdmission.Pending,"identity publication first");return Call();}
    static ItemValue Hint(bool ack=false){var hint=new ItemValue();hint.SetMetadata("rebirth.cooking.hidden.original",Parent.Operation.ToString("N"));hint.SetMetadata("rebirth.cooking.hidden.delta",Delta.Operation.ToString("N"));if(ack)hint.SetMetadata("rebirth.cooking.hidden.ack",Delta.Operation.ToString("N"));return hint;}
    static void Main(){
        Reset();Need(Admit()==RebirthCookingRemoteAdmission.Pending&&Parent.State==RebirthCamperTransferJournal.Phase.Prepared&&Model.Station.Output[0].count==5,"unsaved native output before image permitted owner plan");
        Model.BeforeSaved=true;Need(Call().StartsWith("hidden:0:")&&Model.Refund==7&&Model.Station.Output[0].count==5,"full exact hidden source not retained before owner credit");
        Need(!RebirthCookingHiddenOutputWrite.Fixture(new XUiM_Workstation{TileEntity=Model.Station})&&!RebirthCookingHiddenOutputNativeBody.Fixture(new NetPackageTileEntity(),Model.Actor.world),"held native/UI body could overwrite original source");
        Model.Actor.world.Remote=true;Need(RebirthCookingHiddenOutputNativeBody.Fixture(new NetPackageTileEntity(),Model.Actor.world),"server-to-client replica application blocked");Model.Actor.world.Remote=false;
        Model.OwnerPhase=1;Need(Call(Hint()).StartsWith("hidden:1:"),"saved phase one not observed");Model.OwnerPhase=2;
        Need(Call(Hint())==RebirthCookingRemoteAdmission.Pending&&Model.Station.Output.All(i=>i.IsEmpty())&&Parent.State==RebirthCamperTransferJournal.Phase.Uncertain,"source removal did not await native saved empty output");
        Model.EmptySaved=true;Need(Call(Hint()).StartsWith("hidden-done:")&&Parent.State==RebirthCamperTransferJournal.Phase.Applied&&RebirthCookingHiddenOutputRecovery.ServerHeld(Model.Station),"terminal source must stay held until projection ack");
        Need(Call(Hint(true)).StartsWith("hidden-done:")&&RebirthCookingHiddenOutputRecovery.ServerHeld(Model.Station),"unsaved retirement accepted projection ack");
        Model.OwnerPhase=3;Need(Call(Hint(true))=="hidden-acked:"+Parent.Operation.ToString("N")&&!RebirthCookingHiddenOutputRecovery.ServerHeld(Model.Station),"saved retirement/projection ack did not release original source");
        Reset();Model.BeforeSaved=true;Model.Room=false;Admit();Need(Call().StartsWith("hidden:0:")&&RebirthCookingOverflowOriginal.Calls==0,"overflow started before saved owner delta");Model.OwnerPhase=1;Call();Model.OwnerPhase=2;
        Need(Call()==RebirthCookingRemoteAdmission.Pending&&Model.Station.Output[0].count==5&&RebirthCookingOverflowOriginal.Calls==1,"unsaved overflow cleared source");Model.DropSaved=true;Need(Call()==RebirthCookingRemoteAdmission.Pending&&Model.Station.Output.All(i=>i.IsEmpty()),"saved overflow/source-removal coupling missing");Model.EmptySaved=true;Need(Call().StartsWith("hidden-done:"),"overflow parent terminal missing");
        Reset();Admit();Model.Station.Output[0].count++;Model.BeforeSaved=true;Need(Call()==RebirthCookingRemoteAdmission.Pending&&RebirthCamperTransferJournal.Files[PathFor("CookingHiddenOutputOwners")].Count==0,"changed source replaced original output image");
        Reset();Model.BeforeSaved=true;Admit();Model.Owner="foreign";Need(Call()==RebirthCookingRemoteAdmission.Pending&&Model.Station.Output[0].count==5,"foreign owner adopted paid source");
        Reset();Admit();Model.Birth=null;int before=Model.IdentityBegins;Need(Call()==RebirthCookingRemoteAdmission.Pending&&Model.IdentityBegins==before&&RebirthCookingHiddenOutputRecovery.ServerHeld(Model.Station),"missing old birth minted replacement identity");
        Reset();Model.BeforeSaved=true;string nested1=null,nested2=null;RebirthCamperTransferJournal.AfterPrepare=(path,record)=>{if(path==PathFor("CookingHiddenOutputs")){nested1=Call();nested2=Call();}};
        Admit();Need(nested1==RebirthCookingRemoteAdmission.Pending&&nested2==RebirthCookingRemoteAdmission.Pending&&RebirthCamperTransferJournal.Files[PathFor("CookingHiddenOutputs")].Count==1,"reentry released outer gate or minted duplicate original");
        Reset();Model.BeforeSaved=true;Admit();Model.Source=new string('b',64);Model.NoStart=true;Need(Call().StartsWith("hidden-retry:")&&Delta.State==RebirthCamperTransferJournal.Phase.Rejected,"positive no-start source change did not retain exact rejection");Need(Call(Hint()).StartsWith("hidden-retry:"),"rejected hinted plan bypassed client acknowledgement");
        Reset();Model.BeforeSaved=true;Admit();Model.OwnerPhase=1;Call();Model.OwnerPhase=2;Call();Model.EmptySaved=true;Call();Model.Actor.Died++;
        Need(Call(Hint()).StartsWith("hidden-done:")&&Call(Hint(true)).StartsWith("hidden-acked:"),"earlier-life positive terminal could not retire only metadata");
        Reset();Model.Station.Output=ItemStack.CreateArray(2);Need(Call()=="hidden-empty:"+request&&Model.IdentityBegins==0,"ordinary empty station unnecessarily admitted native identity");
        Console.WriteLine("MP042 actual hidden output server/guards assertions="+assertions+"; native files, save bodies and genuine topology NOT RUN");
    }
}
