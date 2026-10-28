using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Security.Cryptography;
using Row=RecipeQueueItem;
// AUTHORED_NOT_RUN. Complete production Handle; native services are adapters.
internal sealed class ItemValue {
 internal Dictionary<string,string> Metadata=new Dictionary<string,string>();internal ItemValue(int type=1){}
 internal bool TryGetMetadata(string key,out string text)=>Metadata.TryGetValue(key,out text);
 internal bool TryGetMetadata(string key,out int value){value=0;return Metadata.TryGetValue(key,out var text)&&int.TryParse(text,out value);}
 internal void SetMetadata(string key,int n){Metadata[key]=n.ToString();}
 internal void SetMetadata(string key,string text){Metadata[key]=text;}
 internal bool HasMetadata(string key)=>Metadata.ContainsKey(key);
 internal ItemValue Clone()=>new ItemValue{Metadata=new Dictionary<string,string>(Metadata)};
}
internal sealed class ItemStack {internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue v,int n){itemValue=v;count=n;}internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);internal bool IsEmpty()=>count==0;}
internal sealed class Recipe {internal int count=1;internal List<ItemStack> ingredients=new List<ItemStack>();internal bool Ready;internal string GetName()=>"recipe";}
internal sealed class RecipeQueueItem {internal Recipe Recipe;internal int Multiplier=1;internal byte Quality;}
internal class TileEntityWorkstation {internal Row[] Queue;internal long lastTickTime;internal Block block=new Block();internal void ResetTickTime(){}internal void setModified(){}}
internal sealed class RebirthCamperStationState:TileEntityWorkstation {}
internal sealed class Block {internal string GetBlockName()=>"station-block";}
internal sealed class World {internal State worldState=new State();internal sealed class State {internal string Guid=System.Guid.NewGuid().ToString("N");}}
internal sealed class EntityPlayer {internal World world=new World();internal int entityId=1,Died=2;internal bool IsDead()=>false;internal Belt inventory=new Belt();internal sealed class Belt {internal int Length=10;}}
internal static class ThreadManager {internal static bool IsMainThread()=>true;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class GameIO {internal static string GetSaveGameDir()=>"fixture-save";}
internal sealed class RebirthStablePlayerIdentity {internal string CanonicalId="owner",StorageKey="storage-owner";}
internal sealed class Ticket {internal string StationPosition="station",Recipe="recipe";internal int Portions=1,OutputCount=1;}
internal sealed class Character {internal Progress Progression=new Progress();internal sealed class Progress {internal Memory Cooking=new Memory();}internal sealed class Memory {internal Dictionary<string,Ticket> Tickets=new Dictionary<string,Ticket>();}}
internal static class RebirthWorldCharacterService {internal static bool TryGetIdentity(EntityPlayer p,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return Model.Identity;}}
internal static class RebirthCookingHeat {internal static bool Managed(Recipe r)=>r?.ingredients?.Count>0;internal static bool Ready(Recipe r)=>r.Ready;}
internal static class RebirthRecipeOutputs {internal static void ClearFrozen(ItemValue value){}}
internal static partial class RebirthCookingRemoteAdmission {internal const string Pending="pending";internal static string SourceHash(ItemStack[] bag,ItemStack[] belt,bool[] locks,int size)=>Model.Source;}
internal static class RebirthRemoteResourceSourceCustody {internal static bool BlocksStation(TileEntityWorkstation station)=>Model.Blocked;}
// Native connection identity/current policy is an explicit service adapter.
internal sealed class RebirthRemoteResourceNetworkScope {internal bool Current=>Model.NetworkCurrent;internal static RebirthRemoteResourceNetworkScope Capture(EntityPlayer actor)=>new RebirthRemoteResourceNetworkScope();}
internal static class RebirthLogisticsEndpointIdentity {internal static bool TryGetIncarnation(World world,TileEntityWorkstation station,out string birth){birth=Model.Birth;return Model.BirthSaved;}}
internal static class RebirthCookingHiddenOutputRecovery {internal static bool TryStationBirth(EntityPlayer actor,TileEntityWorkstation station,string location,string creation,out string birth){birth=Model.Birth;return Model.BirthSaved;}internal static bool StationBirthMatches(EntityPlayer actor,TileEntityWorkstation station,string location,XElement original)=>Model.BirthSaved&&(string)original.Attribute("birth")==Model.Birth;}
internal static class Log {internal static void Warning(string message){}}
internal static class RebirthCamperInventorySnapshot {
 internal static byte[] Capture(ItemStack[] items)=>Encoding.UTF8.GetBytes(string.Join(",",items.Select(i=>i.count)));
 internal static ItemStack[] Restore(byte[] bytes){string s=Encoding.UTF8.GetString(bytes);return s.Length==0?new ItemStack[0]:s.Split(',').Select(n=>new ItemStack(new ItemValue(),int.Parse(n))).ToArray();}
}
internal sealed class RebirthCamperTransferJournal {
 internal enum Phase {Prepared,Uncertain,Applied,Rejected}
 internal sealed class Record {internal Guid Operation,Vehicle;internal string OwnerCreation;internal byte[] Before,After;internal Phase State;}
 internal readonly List<Record> Rows=new List<Record>();internal int Prepared;
 internal Record Read(Guid id)=>Rows.SingleOrDefault(r=>r.Operation==id);
 internal IEnumerable<Record> FindApplied(Guid parent,string owner)=>Rows.Where(r=>r.Vehicle==parent&&r.OwnerCreation==owner&&r.State==Phase.Applied);
 internal IEnumerable<Record> FindRejected(Guid parent,string owner)=>Rows.Where(r=>r.Vehicle==parent&&r.OwnerCreation==owner&&r.State==Phase.Rejected);
 internal Record FindPending(Guid parent)=>Rows.SingleOrDefault(r=>r.Vehicle==parent&&(r.State==Phase.Prepared||r.State==Phase.Uncertain));
 internal void AssertAvailable(Guid parent,Guid id){if(FindPending(parent)!=null)throw new Exception("unresolved original");}
 internal Record Prepare(Guid id,Guid parent,string owner,long rev,byte[] before,byte[] after){Prepared++;var r=new Record{Operation=id,Vehicle=parent,OwnerCreation=owner,Before=(byte[])before.Clone(),After=(byte[])after.Clone(),State=Phase.Prepared};Rows.Add(r);Model.AfterPrepare?.Invoke();return r;}
 internal Record Advance(Guid id,Phase expected,Phase next){var r=Read(id);if(r.State!=expected)throw new Exception("phase mismatch");r.State=next;return r;}
}
internal static partial class RebirthCookingRemoteAdmissionBackend {
 private static readonly Dictionary<string,Row[]> Snapshots=new Dictionary<string,Row[]>();
 internal static bool Current(EntityPlayer p,string location,string creation,out TileEntityWorkstation station,out Character owner){station=Model.Station;owner=Model.Character;return Model.Current&&location=="station"&&creation=="creation";}
 internal static bool Held(TileEntityWorkstation station)=>false;
 internal static string Queue(Row[] rows){string s=new XElement("queue",rows.Select(r=>new XElement("row",new XAttribute("n",r.Multiplier),new XAttribute("q",r.Quality),r.Recipe==null?null:new XElement("recipe",new XAttribute("count",r.Recipe.count),r.Recipe.ingredients.Select(i=>new XElement("input",new XAttribute("n",i.count),i.itemValue.Metadata.OrderBy(k=>k.Key).Select(k=>new XElement("meta",new XAttribute("key",k.Key),k.Value)))))))).ToString(SaveOptions.DisableFormatting);Snapshots[s]=Clone(rows);return s;}
 internal static Row[] ReadQueue(string s)=>Clone(Snapshots[s]);
 private static Row[] Clone(Row[] rows)=>rows.Select(r=>new Row{Multiplier=r.Multiplier,Quality=r.Quality,Recipe=r.Recipe==null?null:new Recipe{count=r.Recipe.count,Ready=r.Recipe.Ready,ingredients=r.Recipe.ingredients.Select(i=>i.Clone()).ToList()}}).ToArray();
 internal static ItemStack[] PaidInputs(Guid token,string world,string creation,string location,string owner)=>new[]{new ItemStack(new ItemValue(),5)};
 internal static bool Published(Guid op,EntityPlayer actor,TileEntityWorkstation station,string image,Func<bool> current,Recipe canonical)=>current()&&(image.Contains("rebirth.cooking.queue.cancellation")?Model.PauseSaved:Model.EmptySaved);
}
internal static class RebirthCamperStationPlayerSavedSource {internal static bool TryCopySavedSource(RebirthStablePlayerIdentity identity,out ItemStack[] bag,out ItemStack[] belt,out bool[] locks){bag=new[]{new ItemStack(new ItemValue(),1)};belt=new ItemStack[0];locks=new[]{false};return true;}}
internal static class RebirthRecyclerOwnerDelivery {
 internal static bool Plan(ItemStack[] bag,ItemStack[] belt,bool[] locks,object x,int size,IList<ItemStack> outputs,out ItemStack[] afterBag,out ItemStack[] afterBelt){Model.PlannedRefund=outputs.Sum(i=>i.count);afterBag=bag.Concat(outputs).ToArray();afterBelt=belt;return Model.Room;}
 internal static bool PlanWithOverflow(ItemStack[] bag,ItemStack[] belt,bool[] locks,object x,int size,IList<ItemStack> outputs,out ItemStack[] afterBag,out ItemStack[] afterBelt,out ItemStack[] overflow){Model.PlannedRefund=outputs.Sum(i=>i.count);afterBag=Model.Room?bag.Concat(outputs).ToArray():bag;afterBelt=belt;overflow=Model.Room?new ItemStack[0]:outputs.Select(i=>i.Clone()).ToArray();return true;}
}
internal static class RebirthCookingOverflowOriginal {
 internal static bool Saved;internal static int Advances;
 internal static bool Advance(RebirthCamperTransferJournal.Record parent,EntityPlayer actor,string owner,string creation,ItemStack[] outputs,int deaths,int entity,Func<bool> current){Advances++;return Saved&&current();}
 internal static bool Terminal(RebirthCamperTransferJournal.Record parent,EntityPlayer actor,string owner,string creation,ItemStack[] outputs,int deaths,int entity)=>Saved;
}
internal static class RebirthCookingOwnerDelta {
 internal sealed class Plan {internal Guid Operation;internal string World,Creation,Owner,SourceHash;internal int Deaths,OverflowOwnerEntity;internal ItemStack[] BeforeBag,AfterBag,BeforeBelt,AfterBelt,Overflow;}
 private static readonly Dictionary<string,Plan> Plans=new Dictionary<string,Plan>();
 internal static string Encode(Plan p){string s=p.Operation.ToString("N");Plans[s]=p;return s;}internal static Plan Decode(string s)=>Plans[s];
 internal static bool Saved(RebirthStablePlayerIdentity identity,Plan p,int phase)=>Model.OwnerPhase==phase;
 internal static bool TryReadUnspentSource(RebirthStablePlayerIdentity identity,Plan p,out ItemStack[] bag,out ItemStack[] belt,out bool[] locks){RebirthCamperStationPlayerSavedSource.TryCopySavedSource(identity,out bag,out belt,out locks);return Model.NoStart;}
 internal static bool ObserveRejected(RebirthStablePlayerIdentity identity,RebirthCamperTransferJournal.Record rejected)=>Model.RejectionAcknowledged;
}
internal static class Model {
 internal static Action AfterPrepare;
 internal static string Birth;internal static bool BirthSaved,NetworkCurrent;
 internal static EntityPlayer Player;internal static TileEntityWorkstation Station;internal static Character Character;internal static Guid Token;
 internal static bool Current,Identity,Blocked,PauseSaved,EmptySaved,Room,NoStart,RejectionAcknowledged;internal static int OwnerPhase,PlannedRefund;internal static string Source;
 internal static void Reset(){Current=Identity=Room=true;Blocked=PauseSaved=EmptySaved=NoStart=RejectionAcknowledged=false;OwnerPhase=PlannedRefund=0;Source="original-source";Player=new EntityPlayer();Token=Guid.NewGuid();Character=new Character();Character.Progression.Cooking.Tickets[Token.ToString("N")]=new Ticket();var item=new ItemStack(new ItemValue(),2);item.itemValue.Metadata["rebirth.cooking.queue.token"]=Token.ToString("N");item.itemValue.Metadata["rebirth.cooking.registeredOwner"]="owner";Station=new TileEntityWorkstation{Queue=new[]{new Row{Recipe=new Recipe{ingredients=new List<ItemStack>{item}}}}};}
}
internal static partial class RebirthCookingRemoteCancellation {
 private const string Marker="rebirth.cooking.queue.cancellation";
 private static RebirthCamperTransferJournal cancellation,delta;
 private static RebirthCamperTransferJournal Journal()=>cancellation;private static RebirthCamperTransferJournal Deltas()=>delta;
// ACTUAL_TOKEN
// ACTUAL_REFUNDS_HANDLE
 private static string Call()=>Handle(Model.Player,Model.Token.ToString("N"),"creation","station");
 private static void Reset(){Model.Reset();Model.Birth="saved-native-birth";Model.BirthSaved=true;Model.NetworkCurrent=true;Model.AfterPrepare=null;RebirthCookingOverflowOriginal.Saved=false;RebirthCookingOverflowOriginal.Advances=0;GameManager.Instance.World=Model.Player.world;cancellation=new RebirthCamperTransferJournal();delta=new RebirthCamperTransferJournal();}
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 internal static void Exercise(){
  Reset();Model.BirthSaved=false;Need(Call()==RebirthCookingRemoteAdmission.Pending&&cancellation.Prepared==0&&delta.Prepared==0,"unsaved source incarnation admitted cancellation");
  Reset();Model.PauseSaved=true;Call();Model.Birth="replacement";Need(Call()==RebirthCookingRemoteAdmission.Pending&&delta.Prepared==1,"cold replacement source inherited cancellation");Model.Birth="saved-native-birth";Need(Call().StartsWith("cancel:0:"),"original source birth failed requery");
  var missingBirth=XElement.Parse(Encoding.UTF8.GetString(cancellation.Rows.Single().Before));missingBirth.Attribute("birth").Remove();cancellation.Rows.Single().Before=Encoding.UTF8.GetBytes(missingBirth.ToString(SaveOptions.DisableFormatting));cancellation.Rows.Single().After=(byte[])cancellation.Rows.Single().Before.Clone();Need(Call()==RebirthCookingRemoteAdmission.Pending&&delta.Prepared==1,"old unqualified cancellation gained replacement source authority");
  Reset();Need(Call()==RebirthCookingRemoteAdmission.Pending&&cancellation.Rows.Single().State==RebirthCamperTransferJournal.Phase.Prepared&&delta.Rows.Count==0,"unsaved pause permitted refund planning");
  Model.PauseSaved=true;Need(Call().StartsWith("cancel:0:")&&Model.PlannedRefund==5&&delta.Rows.Count==1,"canonical paid payload not retained");
  var original=delta.Rows.Single().Operation;Model.OwnerPhase=1;Need(Call().StartsWith("cancel:1:")&&delta.Rows.Single().State==RebirthCamperTransferJournal.Phase.Uncertain,"owner phase-one gate lost");
  Model.OwnerPhase=2;Need(Call()==RebirthCookingRemoteAdmission.Pending&&delta.Rows.Single().State==RebirthCamperTransferJournal.Phase.Applied&&Model.Station.Queue.All(r=>r.Recipe==null)&&cancellation.Rows.Single().State==RebirthCamperTransferJournal.Phase.Uncertain,"unsaved station removal treated as complete");
  Model.EmptySaved=true;string completed=Call();Need(completed.StartsWith("cancelled:")&&Call()==completed&&cancellation.Prepared==1&&delta.Prepared==1&&delta.Rows.Single().Operation==original,"lost terminal reply repeated source or refund plan");
  Model.Player.Died++;Model.OwnerPhase=0;Need(Call()==completed&&cancellation.Prepared==1&&delta.Prepared==1,"earlier-life Applied originals replanned or required owner credit replay");
  Reset();Model.PauseSaved=true;Call();Model.OwnerPhase=1;Call();Model.OwnerPhase=2;Call();Need(delta.Rows.Single().State==RebirthCamperTransferJournal.Phase.Applied&&cancellation.Rows.Single().State==RebirthCamperTransferJournal.Phase.Uncertain,"historical station-removal fixture did not reach saved owner credit");
  var retainedWorld=Model.Player.world;Model.Player=new EntityPlayer{world=retainedWorld,Died=3};Model.OwnerPhase=0;Model.EmptySaved=true;Need(Call().StartsWith("cancelled:")&&cancellation.Prepared==1&&delta.Prepared==1&&cancellation.Rows.Single().State==RebirthCamperTransferJournal.Phase.Applied,"new body could not finish saved removal metadata without refund replay");
  Reset();Model.PauseSaved=true;Need(Call().StartsWith("cancel:0:"),"initial refund plan missing");Model.Source="changed-source";
  Need(Call().StartsWith("cancel:0:")&&delta.Rows.Single().State==RebirthCamperTransferJournal.Phase.Prepared,"unknown native no-start evidence rejected original");
  Model.NoStart=true;Need(Call().StartsWith("retry-cancel:")&&delta.Rows.Single().State==RebirthCamperTransferJournal.Phase.Rejected,"positive no-start refusal missing");
  Need(Call().StartsWith("retry-cancel:")&&delta.Prepared==1,"cold missing hint bypassed rejected acknowledgement");
  Model.RejectionAcknowledged=true;Need(Call().StartsWith("cancel:0:")&&delta.Prepared==2,"acknowledged rejection could not replan once");
  Reset();Model.PauseSaved=true;Model.Room=false;Need(Call().StartsWith("cancel:0:")&&delta.Prepared==1&&Model.Station.Queue[0].Recipe!=null&&RebirthCookingOverflowOriginal.Advances==0,"overflow plan skipped saved native owner admission");
  Model.OwnerPhase=1;Need(Call().StartsWith("cancel:1:")&&RebirthCookingOverflowOriginal.Advances==0,"overflow began before saved owner delta");
  Model.OwnerPhase=2;Need(Call()==RebirthCookingRemoteAdmission.Pending&&Model.Station.Queue[0].Recipe!=null&&RebirthCookingOverflowOriginal.Advances==1,"unsaved overflow retired paid source");
  RebirthCookingOverflowOriginal.Saved=true;Need(Call()==RebirthCookingRemoteAdmission.Pending&&Model.Station.Queue[0].Recipe==null,"saved overflow did not require saved source removal");
  Model.EmptySaved=true;Need(Call().StartsWith("cancelled:")&&delta.Prepared==1&&cancellation.Prepared==1,"overflow terminal replanned owner refund");
  for(int variant=0;variant<5;variant++){
   Reset();if(variant==0)Model.Current=false;if(variant==1)Model.Identity=false;if(variant==2)Model.Blocked=true;if(variant==3)Model.Character.Progression.Cooking.Tickets.Clear();if(variant==4)Model.Station.Queue[0].Recipe.ingredients[0].itemValue.Metadata["rebirth.cooking.registeredOwner"]="foreign";
   Need(Call()==RebirthCookingRemoteAdmission.Pending&&cancellation.Prepared==0&&delta.Prepared==0,"unqualified cancellation published original "+variant);
  }
  Reset();Model.PauseSaved=true;Call();Model.Player.Died++;Model.OwnerPhase=1;Need(Call()==RebirthCookingRemoteAdmission.Pending&&delta.Rows.Single().State==RebirthCamperTransferJournal.Phase.Prepared,"another life advanced retained owner refund");
  Reset();Call();Model.Station.Queue[0].Multiplier=2;Model.PauseSaved=true;
  Need(Call()==RebirthCookingRemoteAdmission.Pending&&delta.Prepared==0&&cancellation.Rows.Single().State==RebirthCamperTransferJournal.Phase.Prepared,"changed station queue authorized refund");
  Reset();Model.PauseSaved=true;Call();var hint=new ItemValue();hint.Metadata["rebirth.cooking.cancellation.delta"]=Guid.NewGuid().ToString("N");
  Need(Handle(Model.Player,Model.Token.ToString("N"),"creation","station",hint)==RebirthCookingRemoteAdmission.Pending&&delta.Prepared==1,"foreign expected refund hint replanned");
  for(int variant=0;variant<3;variant++)
  {
   Reset();string before=RebirthCookingRemoteAdmissionBackend.Queue(Model.Station.Queue);
   Model.AfterPrepare=()=>{if(variant==0)Model.Player.Died++;if(variant==1)Model.Birth="replacement-birth";if(variant==2)Model.NetworkCurrent=false;};
   Need(Call()==RebirthCookingRemoteAdmission.Pending&&cancellation.Prepared==1&&delta.Prepared==0&&RebirthCookingRemoteAdmissionBackend.Queue(Model.Station.Queue)==before,
    "canonical cancellation mutated source after original publication changed life/birth/channel "+variant);
  }
  LegacyCases();
  Console.WriteLine("PASS actual complete cooking cancellation Handle/legacy producer; native saved files/codecs/services and remote topology remain adapters/NOT RUN");
 }
 private static void LegacyCases(){
  Reset();Model.Station.Queue[0].Recipe.ingredients[0].itemValue.Metadata.Remove("rebirth.cooking.queue.token");Model.Character.Progression.Cooking.Tickets.Clear();
  var row=Model.Station.Queue[0];row.Recipe.ingredients[0].itemValue.Metadata["rich"]="keep";string signature=LegacySignature(row.Recipe,row.Multiplier,row.Quality);Need(signature!=null,"legacy selector absent");
  row.Recipe.ingredients[0].itemValue.SetMetadata("rebirth.cooking.queue.elapsed",12);Need(LegacySignature(row.Recipe,row.Multiplier,row.Quality)==signature,"mutable clock changed legacy selector");
  Model.Token=Guid.NewGuid();var hint=new ItemValue();hint.Metadata["rebirth.cooking.cancellation.legacy"]=signature;
  Func<string> call=()=>Handle(Model.Player,Model.Token.ToString("N"),"creation","station",hint);
  Need(call()==RebirthCookingRemoteAdmission.Pending&&cancellation.Prepared==1&&delta.Prepared==0&&Token(Model.Station.Queue[0].Recipe,out var adopted)&&adopted==Model.Token,"legacy row cleared/credited before saved pause");
  Model.PauseSaved=true;Need(call().StartsWith("cancel:0:")&&Model.PlannedRefund==2&&delta.Prepared==1,"legacy refund did not use exact frozen native input");Model.OwnerPhase=1;Need(call().StartsWith("cancel:1:"),"legacy owner phase-one handshake lost");Model.OwnerPhase=2;Need(call()==RebirthCookingRemoteAdmission.Pending&&Model.Station.Queue[0].Recipe==null,"legacy saved credit did not retain saved removal boundary");Model.EmptySaved=true;string terminal=call();Need(terminal.StartsWith("cancelled:")&&call()==terminal&&cancellation.Prepared==1&&delta.Prepared==1,"legacy terminal reply replayed original effects");
  Need(Handle(Model.Player,Model.Token.ToString("N"),"creation","station")==terminal,"cold tagged legacy original lost canonical query route");
  Reset();var duplicateRow=Model.Station.Queue[0];duplicateRow.Recipe.ingredients[0].itemValue.Metadata.Remove("rebirth.cooking.queue.token");Model.Station.Queue=RebirthCookingRemoteAdmissionBackend.ReadQueue(RebirthCookingRemoteAdmissionBackend.Queue(new[]{duplicateRow,duplicateRow}));signature=LegacySignature(duplicateRow.Recipe,duplicateRow.Multiplier,duplicateRow.Quality);hint=new ItemValue();hint.Metadata["rebirth.cooking.cancellation.legacy"]=signature;hint.SetMetadata("rebirth.cooking.cancellation.legacySlot",1);Model.Token=Guid.NewGuid();
  string retainedFirst=RebirthCookingRemoteAdmissionBackend.Queue(new[]{Model.Station.Queue[0]});Need(Handle(Model.Player,Model.Token.ToString("N"),"creation","station",hint)==RebirthCookingRemoteAdmission.Pending&&cancellation.Prepared==1&&!Token(Model.Station.Queue[0].Recipe,out _)&&Token(Model.Station.Queue[1].Recipe,out var selectedToken)&&selectedToken==Model.Token,"explicit native slot did not select exactly one identical legacy row");
  hint.SetMetadata("rebirth.cooking.cancellation.legacySlot",0);Model.PauseSaved=true;Need(Handle(Model.Player,Model.Token.ToString("N"),"creation","station",hint)==RebirthCookingRemoteAdmission.Pending&&delta.Prepared==0,"same legacy original changed its frozen queue slot");hint.SetMetadata("rebirth.cooking.cancellation.legacySlot",1);Handle(Model.Player,Model.Token.ToString("N"),"creation","station",hint);Model.OwnerPhase=1;Handle(Model.Player,Model.Token.ToString("N"),"creation","station",hint);Model.OwnerPhase=2;Handle(Model.Player,Model.Token.ToString("N"),"creation","station",hint);Need(Model.Station.Queue[1].Recipe==null&&RebirthCookingRemoteAdmissionBackend.Queue(new[]{Model.Station.Queue[0]})==retainedFirst&&delta.Prepared==1&&cancellation.Rows.Single().State==RebirthCamperTransferJournal.Phase.Uncertain,"selected legacy cancellation changed its identical neighbour, duplicated refund or bypassed saved removal");Model.EmptySaved=true;Need(Handle(Model.Player,Model.Token.ToString("N"),"creation","station",hint).StartsWith("cancelled:")&&delta.Prepared==1,"selected legacy cancellation lost saved removal completion");
  for(int variant=0;variant<5;variant++){
   Reset();var input=Model.Station.Queue[0].Recipe.ingredients[0];input.itemValue.Metadata.Remove("rebirth.cooking.queue.token");signature=LegacySignature(Model.Station.Queue[0].Recipe,1,0);hint=new ItemValue();hint.Metadata["rebirth.cooking.cancellation.legacy"]=signature;var request=Guid.NewGuid();
   switch(variant){case 0:Model.Station.Queue=new[]{Model.Station.Queue[0],Model.Station.Queue[0]};break;case 1:input.itemValue.Metadata["rebirth.cooking.registeredOwner"]="foreign";break;
    case 2:input.itemValue.Metadata["rich"]="changed";break;case 3:Model.Station.Queue[0].Quality=1;break;case 4:input.itemValue.Metadata["rebirth.cooking.queue.token"]="malformed";break;}
   Need(Handle(Model.Player,request.ToString("N"),"creation","station",hint)==RebirthCookingRemoteAdmission.Pending&&cancellation.Prepared==0&&delta.Prepared==0,"ambiguous/foreign/changed legacy source admitted "+variant);
  }
  Reset();Model.Station.Queue[0].Recipe.ingredients[0].itemValue.Metadata.Remove("rebirth.cooking.queue.token");signature=LegacySignature(Model.Station.Queue[0].Recipe,1,0);hint=new ItemValue();hint.Metadata["rebirth.cooking.cancellation.legacy"]=signature;var originalToken=Guid.NewGuid();Handle(Model.Player,originalToken.ToString("N"),"creation","station",hint);
  var original=XElement.Parse(Encoding.UTF8.GetString(cancellation.Rows.Single().Before));Model.Station.Queue=RebirthCookingRemoteAdmissionBackend.ReadQueue(original.Element("before").Value);Model.PauseSaved=true;
  var currentBytes=(byte[])cancellation.Rows.Single().Before.Clone();var oldBeforeTag=new XElement(original);oldBeforeTag.Attribute("legacyTagProtocol").Remove();cancellation.Rows.Single().Before=cancellation.Rows.Single().After=Encoding.UTF8.GetBytes(oldBeforeTag.ToString(SaveOptions.DisableFormatting));
  Need(Handle(Model.Player,originalToken.ToString("N"),"creation","station",hint)==RebirthCookingRemoteAdmission.Pending&&delta.Prepared==0&&!Token(Model.Station.Queue[0].Recipe,out _),"old unqualified legacy original selected a later unlabelled matching row");cancellation.Rows.Single().Before=cancellation.Rows.Single().After=currentBytes;
  Need(Handle(Model.Player,originalToken.ToString("N"),"creation","station",hint)=="cancel-unused:"+originalToken.ToString("N")&&cancellation.Rows.Single().State==RebirthCamperTransferJournal.Phase.Rejected&&delta.Prepared==0&&!Token(Model.Station.Queue[0].Recipe,out _),"versioned Prepared/exact-Before recovery failed or tagged a later matching row");
  Reset();Model.Station.Queue[0].Recipe.ingredients[0].itemValue.Metadata.Remove("rebirth.cooking.queue.token");signature=LegacySignature(Model.Station.Queue[0].Recipe,1,0);hint=new ItemValue();hint.Metadata["rebirth.cooking.cancellation.legacy"]=signature;originalToken=Guid.NewGuid();string reentrant=null;Model.AfterPrepare=()=>{Model.AfterPrepare=null;reentrant=Handle(Model.Player,originalToken.ToString("N"),"creation","station",hint);};
  Need(Handle(Model.Player,originalToken.ToString("N"),"creation","station",hint)==RebirthCookingRemoteAdmission.Pending&&reentrant==RebirthCookingRemoteAdmission.Pending&&cancellation.Rows.Single().State==RebirthCamperTransferJournal.Phase.Prepared&&delta.Prepared==0&&Token(Model.Station.Queue[0].Recipe,out var freshTagged)&&freshTagged==originalToken,"reentrant status preempted fresh legacy publication or entered owner effects");
  Reset();Model.Station.Queue[0].Recipe.ingredients[0].itemValue.Metadata.Remove("rebirth.cooking.queue.token");signature=LegacySignature(Model.Station.Queue[0].Recipe,1,0);hint=new ItemValue();hint.Metadata["rebirth.cooking.cancellation.legacy"]=signature;hint.SetMetadata("rebirth.cooking.cancellation.legacySlot",0);originalToken=Guid.NewGuid();string unchanged=RebirthCookingRemoteAdmissionBackend.Queue(Model.Station.Queue);Model.AfterPrepare=()=>Model.Player.Died++;
  Need(Handle(Model.Player,originalToken.ToString("N"),"creation","station",hint)==RebirthCookingRemoteAdmission.Pending&&cancellation.Rows.Single().State==RebirthCamperTransferJournal.Phase.Rejected&&RebirthCookingRemoteAdmissionBackend.Queue(Model.Station.Queue)==unchanged&&delta.Prepared==0,"warm pre-tag scope loss did not retain exact no-effect rejection");Model.AfterPrepare=null;
  Need(Handle(Model.Player,originalToken.ToString("N"),"creation","station",hint)=="cancel-unused:"+originalToken.ToString("N")&&cancellation.Prepared==1&&delta.Prepared==0,"saved no-tag rejection permitted replay or failed terminal query");hint.SetMetadata("rebirth.cooking.cancellation.legacySlot",1);Need(Handle(Model.Player,originalToken.ToString("N"),"creation","station",hint)==RebirthCookingRemoteAdmission.Pending,"changed unused original slot was accepted");hint.SetMetadata("rebirth.cooking.cancellation.legacySlot",0);
  var legacyOld=XElement.Parse(Encoding.UTF8.GetString(cancellation.Rows.Single().Before));legacyOld.Attribute("legacyTagProtocol").Remove();cancellation.Rows.Single().Before=cancellation.Rows.Single().After=Encoding.UTF8.GetBytes(legacyOld.ToString(SaveOptions.DisableFormatting));Need(Handle(Model.Player,originalToken.ToString("N"),"creation","station",hint)==RebirthCookingRemoteAdmission.Pending,"legacy rejection without protocol declaration inferred no tag");
 }
}
internal static class Program {public static void Main()=>RebirthCookingRemoteCancellation.Exercise();}
