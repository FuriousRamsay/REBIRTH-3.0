// AUTHORED_NOT_RUN. Native effects/persistence below are disclosed adapters.
using System;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;using System.Text;using System.Xml.Linq;
// ACTUAL_COMMAND
class EntityPlayer{internal int entityId=1,Died;internal World world;internal bool Dead;internal bool IsDead()=>Dead;}
class World{internal EntityPlayer Player;internal bool Remote;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Player;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
struct RebirthCamperStationAddress{internal int Id;internal bool IsValid=>Id!=0;public override string ToString()=>"station-"+Id;}
class RebirthStablePlayerIdentity{internal string StorageKey=new string('a',64),CanonicalId="native-owner";}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer p)=>"creation";}
static class RebirthSurvivorMode{internal static bool Enabled;internal static bool IsEnabledForCurrentWorld()=>Enabled;}
class ItemClass{internal string Name="recipe";internal string GetItemName()=>Name;}
class ItemValue
{
 internal ItemClass ItemClass=new ItemClass();internal Dictionary<string,object> Metadata=new Dictionary<string,object>();
 internal bool TryGetMetadata<T>(string key,out T value){if(Metadata.TryGetValue(key,out var result)&&result is T exact){value=exact;return true;}value=default(T);return false;}
}
class ItemStack
{
 internal ItemValue itemValue=new ItemValue();internal int count;internal static ItemStack Empty=new ItemStack();
 internal bool IsEmpty()=>count==0;internal ItemStack Clone()=>new ItemStack{itemValue=itemValue,count=count};
}
class Recipe{internal string Name="recipe";internal Guid Job;internal bool Recycler;internal List<ItemStack> ingredients=new List<ItemStack>();internal string GetName()=>Name;}
class RecipeQueueItem{internal Recipe Recipe;internal int Multiplier,Quality;}
class RebirthCamperStationState{internal RecipeQueueItem[] Queue=Array.Empty<RecipeQueueItem>();internal ItemStack[] Input=new[]{new ItemStack{count=5}};}
static class RebirthStationGridIngredients{internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count&&a.itemValue.ItemClass.Name==b.itemValue.ItemClass.Name;}
static class XUiM_Recipes{internal static List<Recipe> Definitions;internal static List<Recipe> GetRecipes()=>Definitions;}
static class RebirthCamperStationGridCustody{internal static bool Held;internal static bool IsHeld(RebirthCamperStationState s)=>Held;}
static class RebirthCamperRemoteStationPlayerSource{internal static bool ActorSource;internal static bool TryResolve(EntityPlayer p,RebirthCamperStationState s,out bool actor){actor=ActorSource;return true;}internal static bool IsCurrent(EntityPlayer p,RebirthCamperStationState s)=>RebirthCamperRemoteStationTransport.Valid;}
static class Probe
{
 internal static int Effects,Refusals;internal static bool ServiceRefuses,BeforeEntryRefuses;
 internal static RebirthCamperRemoteStationCommandStatus.State Status=RebirthCamperRemoteStationCommandStatus.State.Applied;
 internal static RebirthCamperStationState Before,After;internal static RebirthCamperRemoteStationCommand Last;
 internal static bool Apply(RebirthCamperRemoteStationCommand c,Action enter)
 {
  if(BeforeEntryRefuses)return false;enter();Effects++;Last=c;
  if(ServiceRefuses){Status=RebirthCamperRemoteStationCommandStatus.State.Unknown;return false;}return true;
 }
}
static class RebirthCamperRemoteStationCommandStatusStore
{
 internal enum AdmissionDisposition{NewlyPublished,ExistingStatusOnly}
 internal sealed class FreshAdmission{internal bool Used;}
 static readonly Dictionary<Guid,string> Originals=new Dictionary<Guid,string>();static readonly HashSet<Guid> Invoked=new HashSet<Guid>(),Refused=new HashSet<Guid>();
 internal static int MetadataWrites;
 internal static void Clear(){Originals.Clear();Invoked.Clear();Refused.Clear();MetadataWrites=0;}
 internal static bool TryAdmit(EntityPlayer p,RebirthCamperStationAddress a,string c,object lease,RebirthCamperRemoteStationCommand q,out object ignored,out AdmissionDisposition d,out FreshAdmission fresh)
 {ignored=null;fresh=null;string binding=RebirthCamperRemoteStationCommandStatus.Binding(q,new string('a',64),c,a);d=AdmissionDisposition.ExistingStatusOnly;if(Originals.TryGetValue(q.Operation,out var old))return old==binding;Originals.Add(q.Operation,binding);d=AdmissionDisposition.NewlyPublished;fresh=new FreshAdmission();return true;}
 internal static bool InvokeFresh(FreshAdmission fresh,Func<bool> current,Func<bool> action){if(fresh==null||fresh.Used||!current())return false;fresh.Used=true;foreach(var op in Originals.Keys)Invoked.Add(op);return action();}
 internal static bool TryReadExact(EntityPlayer p,RebirthCamperStationAddress a,string c,object lease,RebirthCamperRemoteStationCommand q,out string binding)=>Originals.TryGetValue(q.Operation,out binding)&&binding==RebirthCamperRemoteStationCommandStatus.Binding(q,new string('a',64),c,a);
 internal static void TrySaveNeverInvoked(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q){MetadataWrites++;}
 internal static bool TryReadBeforeEffectRefusal(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q,out int deaths,out bool candidate){deaths=p.Died;candidate=false;return Refused.Contains(q.Operation);}
 internal static void TryArchiveBeforeEffectIdentity(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q){MetadataWrites++;}
 internal static bool HasInvocation(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q)=>Invoked.Contains(q.Operation);
 internal static bool TryReadAdmittedDeaths(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q,out int deaths){deaths=p.Died;return true;}
 internal static void TryArchiveAppliedIdentity(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q,RebirthCamperRemoteStationCommandStatus.Observation o){MetadataWrites++;}
 internal static void TryArchiveRejectedIdentity(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q,RebirthCamperRemoteStationCommandStatus.Observation o){MetadataWrites++;}
 internal static void TrySaveBeforeEffectRefusal(RebirthCamperRemoteStationActions.BeforeEffectRefusal source){if(source!=null){Refused.Add(source.Command.Operation);Probe.Refusals++;}}
}
static class RebirthCamperRemoteStationTransport
{
 internal sealed class Lease{internal EntityPlayer Player;internal Guid Token;internal RebirthCamperStationAddress Address;internal string Creation="creation";}
 static readonly Dictionary<int,Lease> leases=new Dictionary<int,Lease>();internal static bool Valid=true;internal static RebirthCamperStationState Station=new RebirthCamperStationState();
 static Func<EntityPlayer,RebirthCamperStationAddress,string,object,RebirthCamperRemoteStationCommand,bool> command=RebirthCamperRemoteStationActions.FixtureHandle;
 static bool Current(Lease lease)=>Valid&&!lease.Player.IsDead();
 internal static object Bind(EntityPlayer p,Guid token,RebirthCamperStationAddress address){leases.Clear();var lease=new Lease{Player=p,Token=token,Address=address};leases.Add(p.entityId,lease);return lease;}
 internal static bool IsCurrent(EntityPlayer p,RebirthCamperStationAddress a,string c,object lease)=>Valid&&leases.TryGetValue(p.entityId,out var actual)&&ReferenceEquals(actual,lease)&&actual.Address.Equals(a)&&actual.Creation==c&&Current(actual);
 internal static bool TryResolve(EntityPlayer p,RebirthCamperStationAddress a,string c,out RebirthCamperStationState s,out object lease){s=Station;lease=leases[p.entityId];return IsCurrent(p,a,c,lease);}
 internal static bool TryGetCurrent(EntityPlayer p,out RebirthCamperStationState s,out RebirthCamperStationAddress a,out string c,out object lease){var actual=leases[p.entityId];s=Station;a=actual.Address;c=actual.Creation;lease=actual;return Current(actual);}
 // ACTUAL_EXECUTE
}
static class RebirthCamperStationFuelOriginal
{
 internal static bool Begin(EntityPlayer p,RebirthCamperStationState s,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q,Action enter)=>Probe.Apply(q,enter);
 internal static bool HasOriginal(RebirthCamperStationAddress a,Guid operation)=>Probe.Last!=null&&Probe.Last.Operation==operation;
 internal static RebirthCamperRemoteStationCommandStatus.State ReadStatus(EntityPlayer p,RebirthCamperStationAddress a,string c,RebirthCamperRemoteStationCommand q,out string proof,out int deaths){proof="saved-station-original";deaths=p.Died;return Probe.Status;}
}
static class RebirthCamperStationNativeCrafting
{
 internal static bool Craft(EntityPlayer p,RebirthCamperStationAddress a,string c,Guid job,Recipe r,int batches,int tier,RebirthCamperRemoteStationCommand q,Action enter)=>Probe.Apply(q,enter);
 internal static bool Cancel(EntityPlayer p,RebirthCamperStationAddress a,string c,Guid job,RebirthCamperRemoteStationCommand q,Action enter)=>Probe.Apply(q,enter);
 internal static bool TryJob(Recipe recipe,out Guid job){job=recipe?.Job??Guid.Empty;return recipe!=null&&!recipe.Recycler;}
}
static class RebirthCamperStationNativeActorPayment
{
 internal sealed class Retained{internal RebirthCamperStationState Before,After;internal string Proof="pending";internal int OriginalDeaths;internal RebirthCamperTransferJournal.Phase Phase;}
 internal static bool RemoteIdentity(EntityPlayer p,out RebirthStablePlayerIdentity owner,out object ignored){owner=new RebirthStablePlayerIdentity();ignored=null;return true;}
 internal static bool CraftPlayer(EntityPlayer p,RebirthCamperStationAddress a,string c,Guid job,Recipe r,int batches,int tier){Probe.Effects++;return !Probe.ServiceRefuses;}
 internal static bool TryReadCommandStatusOriginal(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,Guid effect,out Retained result){result=new Retained{Before=Probe.Before,After=Probe.After,OriginalDeaths=p.Died,Phase=RebirthCamperTransferJournal.Phase.Prepared};return Probe.Status==RebirthCamperRemoteStationCommandStatus.State.Pending;}
}
static class RebirthCamperTransferJournal{internal enum Phase{Rejected,Prepared,Uncertain,Other}}
static class RebirthRemoteResourceStationPaymentWitness
{
 internal sealed class Saved{internal int OriginalDeaths;internal string OwnerKey=new string('a',64),Creation="creation",Address,Proof="saved-owner";internal RebirthCamperStationState RestoreBefore()=>Probe.Before;internal RebirthCamperStationState RestoreAfter()=>Probe.After;}
 internal static bool TryReadSavedPayment(World w,Guid effect,string address,string owner,string c,out Saved result){result=new Saved{OriginalDeaths=w.Player.Died,Address=address};return Probe.Status==RebirthCamperRemoteStationCommandStatus.State.Applied;}
}
static class RebirthRecyclerRuntime
{
 internal const string Prefix="recycler.";internal static bool Supports(RebirthCamperStationState s)=>true;
 internal static bool TryCancel(EntityPlayer p,RebirthCamperStationState s,Guid job,Func<bool> current,out object ignored){ignored=null;Probe.Effects++;return current()&&!Probe.ServiceRefuses;}
 internal static bool TryQueue(EntityPlayer p,RebirthCamperStationState s,int slot,int count,Guid operation,Func<bool> current,out object ignored){ignored=null;Probe.Effects++;return current()&&!Probe.ServiceRefuses;}
 internal static Guid CancelOperation(Guid job)=>job;
 internal static bool TryGroup(Recipe recipe,out Guid group){group=recipe?.Job??Guid.Empty;return recipe?.Recycler==true;}
 internal static bool ValidRow(RebirthCamperStationState s,RecipeQueueItem row)=>row?.Recipe!=null;
}
static class RebirthStationPreparationService
{
 internal static Func<EntityPlayer,RebirthCamperStationState,bool> RemoteUsesPlayerPayment;
 internal static bool CanPrepareMobile(EntityPlayer p,RebirthCamperStationAddress a,string c,Recipe r,int b,int t)=>!Probe.ServiceRefuses;
 internal static bool TryPrepareMobile(EntityPlayer p,RebirthCamperStationAddress a,string c,Guid job,Recipe r,int b,out object ignored,int t){ignored=null;Probe.Effects++;return !Probe.ServiceRefuses;}
}
static class RebirthCamperStationPublicationWatch{internal static bool TryPublishPrepared(EntityPlayer p,RebirthCamperStationAddress a,string c,Guid job)=>!Probe.ServiceRefuses;}
static class RebirthCamperRemoteStationSurvivorStatus{internal static RebirthCamperRemoteStationCommandStatus.State Read(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q,out string proof){proof="saved-survivor";return Probe.ServiceRefuses?RebirthCamperRemoteStationCommandStatus.State.Unknown:Probe.Status;}}
static class RebirthCamperRemoteStationActions
{
 static Func<EntityPlayer,RebirthCamperStationAddress,string,object,Guid,bool> cancel=(p,a,c,l,j)=>{Probe.Effects++;return !Probe.ServiceRefuses;};
 internal static bool FixtureHandle(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q)=>HandleTracked(p,a,c,l,q);
 // ACTUAL_ACTIONS_BODY
}
// ACTUAL_STATUS
class Program
{
 static int cases;static EntityPlayer player;static RebirthCamperStationAddress address;static Guid token;static object lease;
 static void Check(bool condition,string name){if(!condition)throw new Exception(name);cases++;}
 static void Setup(bool survivor=false)
 {
  var world=new World();player=new EntityPlayer{world=world};world.Player=player;GameManager.Instance.World=world;
  address=new RebirthCamperStationAddress{Id=1};token=Guid.NewGuid();lease=RebirthCamperRemoteStationTransport.Bind(player,token,address);
  RebirthCamperRemoteStationTransport.Valid=true;RebirthCamperStationGridCustody.Held=false;ThreadManager.Main=true;
  RebirthCamperRemoteStationTransport.Station=new RebirthCamperStationState();RebirthSurvivorMode.Enabled=survivor;
  RebirthCamperRemoteStationPlayerSource.ActorSource=false;XUiM_Recipes.Definitions=new List<Recipe>{new Recipe()};
  Probe.Effects=Probe.Refusals=0;Probe.Last=null;Probe.ServiceRefuses=Probe.BeforeEntryRefuses=false;Probe.Status=RebirthCamperRemoteStationCommandStatus.State.Applied;
  Probe.Before=new RebirthCamperStationState();Probe.After=new RebirthCamperStationState();RebirthCamperRemoteStationCommandStatusStore.Clear();
 }
 static RebirthCamperRemoteStationCommand Command(RebirthCamperRemoteStationCommandKind kind)
 {var op=Guid.NewGuid();return kind==RebirthCamperRemoteStationCommandKind.FuelOn||kind==RebirthCamperRemoteStationCommandKind.FuelOff?RebirthCamperRemoteStationCommand.Fuel(op,kind==RebirthCamperRemoteStationCommandKind.FuelOn):kind==RebirthCamperRemoteStationCommandKind.ScrapCancel?new RebirthCamperRemoteStationCommand(kind,op,op,"",0,0):new RebirthCamperRemoteStationCommand(kind,op,op,"recipe",2,0);}
 static void QueueEvidence(RebirthCamperRemoteStationCommand q,bool recycler,bool cancel)
 {
  var token=new ItemStack{count=1};token.itemValue.Metadata[RebirthRecyclerRuntime.Prefix+"owner"]="native-owner";
  int batches=cancel?2:q.Batches;
  token.itemValue.Metadata[RebirthRecyclerRuntime.Prefix+"creation"]="creation";token.itemValue.Metadata[RebirthRecyclerRuntime.Prefix+"units"]=batches;
  var row=new RecipeQueueItem{Recipe=new Recipe{Job=q.Job,Recycler=recycler,ingredients=new List<ItemStack>{token}},Multiplier=batches,Quality=q.Tier};
  Probe.Before.Queue=cancel?new[]{row}:new RecipeQueueItem[1];Probe.After.Queue=cancel?new RecipeQueueItem[1]:new[]{row};
  if(recycler&&!cancel)Probe.After.Input[0].count-=q.Batches;
 }
 static void Main()
 {
  foreach(bool survivor in new[]{false,true})foreach(var kind in new[]{RebirthCamperRemoteStationCommandKind.Craft,RebirthCamperRemoteStationCommandKind.FuelOn,RebirthCamperRemoteStationCommandKind.FuelOff})
  {
   Setup(survivor);var q=Command(kind);Check(RebirthCamperRemoteStationTransport.Execute(player,token,q)&&Probe.Effects==1,"actual handler dispatch "+kind+" survivor="+survivor);
   Check(RebirthCamperRemoteStationCommandStatus.ReadStatus(player,address,"creation",lease,q,out var observed)==RebirthCamperRemoteStationCommandStatus.State.Applied&&observed.AuthorizesCommandRetirement,"actual exact status after handler");
   int writes=RebirthCamperRemoteStationCommandStatusStore.MetadataWrites;
   Check(RebirthCamperRemoteStationCommandStatus.ObserveStatus(player,address,"creation",lease,q,out var readOnly)==RebirthCamperRemoteStationCommandStatus.State.Applied&&readOnly.RequestOperation==q.Operation&&RebirthCamperRemoteStationCommandStatusStore.MetadataWrites==writes&&Probe.Effects==1,"native diagnostic uses actual status qualification without archive/save or effects");
   Check(RebirthCamperRemoteStationTransport.Execute(player,token,q)&&Probe.Effects==1,"duplicate Execute queries status without another native handler");
  }
  foreach(bool survivor in new[]{false,true})foreach(var kind in new[]{RebirthCamperRemoteStationCommandKind.CraftPlayer,RebirthCamperRemoteStationCommandKind.Cancel,RebirthCamperRemoteStationCommandKind.ScrapQueue,RebirthCamperRemoteStationCommandKind.ScrapCancel})
  {
   Setup(survivor);var q=Command(kind);RebirthCamperRemoteStationPlayerSource.ActorSource=kind==RebirthCamperRemoteStationCommandKind.CraftPlayer;
   QueueEvidence(q,kind==RebirthCamperRemoteStationCommandKind.ScrapQueue||kind==RebirthCamperRemoteStationCommandKind.ScrapCancel,kind==RebirthCamperRemoteStationCommandKind.Cancel||kind==RebirthCamperRemoteStationCommandKind.ScrapCancel);
   Check(RebirthCamperRemoteStationTransport.Execute(player,token,q)&&Probe.Effects==1,"actual remaining handler dispatch "+kind+" survivor="+survivor);
   Check(RebirthCamperRemoteStationCommandStatus.ReadStatus(player,address,"creation",lease,q,out var outcome)==RebirthCamperRemoteStationCommandStatus.State.Applied&&outcome.AuthorizesCommandRetirement,"actual matching saved status for "+kind);
   Check(RebirthCamperRemoteStationTransport.Execute(player,token,q)&&Probe.Effects==1,"duplicate cannot repeat "+kind);
  }
  Setup();var craft=Command(RebirthCamperRemoteStationCommandKind.Craft);Probe.BeforeEntryRefuses=true;
  Check(!RebirthCamperRemoteStationTransport.Execute(player,token,craft)&&Probe.Effects==0&&Probe.Refusals==1,"actual tracked before-effect refusal");
  Check(RebirthCamperRemoteStationCommandStatus.ReadStatus(player,address,"creation",lease,craft,out _)==RebirthCamperRemoteStationCommandStatus.State.Rejected,"saved before-effect refusal status");
  Setup();craft=Command(RebirthCamperRemoteStationCommandKind.Craft);Probe.ServiceRefuses=true;
  Check(!RebirthCamperRemoteStationTransport.Execute(player,token,craft)&&Probe.Effects==1&&Probe.Refusals==0,"service false after entry cannot mint unused");
  Check(RebirthCamperRemoteStationCommandStatus.ReadStatus(player,address,"creation",lease,craft,out _)==RebirthCamperRemoteStationCommandStatus.State.Unknown,"uncertain entered effect remains unknown");
  Setup();craft=Command(RebirthCamperRemoteStationCommandKind.Craft);Check(!RebirthCamperRemoteStationTransport.Execute(player,Guid.NewGuid(),craft)&&Probe.Effects==0,"foreign token never dispatches");
  Setup();craft=Command(RebirthCamperRemoteStationCommandKind.Craft);RebirthCamperRemoteStationTransport.Valid=false;
  Check(!RebirthCamperRemoteStationTransport.Execute(player,token,craft)&&Probe.Effects==0,"expired lease never dispatches");
  Setup();craft=Command(RebirthCamperRemoteStationCommandKind.Craft);RebirthCamperRemoteStationTransport.Execute(player,token,craft);
  var changed=new RebirthCamperRemoteStationCommand(craft.Kind,craft.Operation,craft.Job,"changed",craft.Batches,craft.Tier);
  Check(!RebirthCamperRemoteStationTransport.Execute(player,token,changed)&&Probe.Effects==1&&RebirthCamperRemoteStationCommandStatus.ReadStatus(player,address,"creation",lease,changed,out _)==RebirthCamperRemoteStationCommandStatus.State.Unknown,"changed request cannot borrow original ID or status");
  Setup();var fuel=Command(RebirthCamperRemoteStationCommandKind.FuelOn);Probe.Status=RebirthCamperRemoteStationCommandStatus.State.Pending;
  RebirthCamperRemoteStationTransport.Execute(player,token,fuel);
  Check(!RebirthCamperRemoteStationTransport.Execute(player,token,fuel)&&Probe.Effects==1&&RebirthCamperRemoteStationCommandStatus.ReadStatus(player,address,"creation",lease,fuel,out var pending)==RebirthCamperRemoteStationCommandStatus.State.Pending&&!pending.AuthorizesCommandRetirement,"pending original is queried without replay or retirement");
  Console.WriteLine("MP071 actual Execute/HandleTracked/Handle/ReadStatus: "+cases+" assertions passed; native/durable adapters remain doubles.");
 }
}
