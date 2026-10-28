using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
// AUTHORED_NOT_RUN. Full ReceiveServer; native proof/codec/store adapters.
internal sealed class World {internal EntityPlayer Actor;internal bool Remote;internal State worldState=new State();internal sealed class State {internal string Guid=System.Guid.NewGuid().ToString("N");}internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Actor;}
internal sealed class EntityPlayer {internal World world;internal int Died=2;internal bool Dead;internal string Owner="owner",Creation="creation";internal bool IsDead()=>Dead;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class ThreadManager {internal static bool IsMainThread()=>true;}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer actor)=>actor.Creation;}
internal sealed class RebirthStablePlayerIdentity {internal string CanonicalId;}
internal sealed class ItemValue {internal string Image;internal void SetMetadata(string key,string value){}}
internal static class RebirthRepairSignatureService {internal static int Calculations;internal static bool TryApplyOriginalOutcome(EntityPlayer p,ItemValue input,ItemValue native,out ItemValue corrected){Calculations++;corrected=new ItemValue{Image="corrected-output"};return true;}}
internal static class RebirthRemoteResourceOwnerAction {internal static bool TryRetireRepairMarkers(EntityPlayer p,ItemValue value,string payment,string proof,out ItemValue clean){clean=value;return true;}}
internal static class RebirthEquipmentRepairTrainingService {
 internal static readonly System.Collections.Generic.HashSet<Guid> Paid=new System.Collections.Generic.HashSet<Guid>();internal static int Awards;
 internal static bool TryAwardOriginal(EntityPlayer player,RebirthRepairOriginalCorrection.Receipt r){if(Paid.Add(r.Operation))Awards++;return true;}
}
internal static class Log {internal static void Warning(string message){}}
internal static class RebirthRepairOriginalCorrection {
 private const string AppliedKey="applied";
// ACTUAL_RECEIPT
 private static Receipt stored,lastReply;private static int saves,replies,ended;
 private static bool queue,absent,payment,beforeProof,afterProof,failPublished;private static World world;
 private static void WorldScope(World w){}
 private static bool TryRepairIdentity(EntityPlayer p,out RebirthStablePlayerIdentity id){id=new RebirthStablePlayerIdentity{CanonicalId=p.Owner};return true;}
 private static string PathFor(Receipt r,bool server)=>Path.Combine(Path.GetTempPath(),"rebirth-uncreated-server-fixture-"+r.Operation.ToString("N"));
 private static string Encode(Receipt r)=>new XElement("adapter",typeof(Receipt).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).OrderBy(f=>f.Name).Select(f=>new XElement(f.Name,f.GetValue(r)?.ToString()??""))).ToString(SaveOptions.DisableFormatting);
 private static Receipt Decode(string text){var root=XElement.Parse(text);var r=new Receipt();foreach(var f in typeof(Receipt).GetFields(BindingFlags.Instance|BindingFlags.NonPublic)){string s=root.Element(f.Name).Value;f.SetValue(r,f.FieldType==typeof(Guid)?(object)Guid.Parse(s):f.FieldType==typeof(bool)?bool.Parse(s):f.FieldType==typeof(int)?int.Parse(s):(object)s);}return r;}
 private static bool Read(Receipt key,bool server,out Receipt r){r=null;if(stored==null)return false;if(new[]{"Operation","World","Owner","Creation","Deaths","Input","Expected","Amount","Payment","PaidInput"}.Any(name=>{var f=typeof(Receipt).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);return !Equals(f.GetValue(key),f.GetValue(stored));}))return false;r=Decode(Encode(stored));return true;}
 private static bool Save(Receipt r,bool server,string expected=null){if(failPublished&&r.Published)return false;if(expected!=null&&(stored==null||Encode(stored)!=expected))return false;stored=Decode(Encode(r));saves++;return true;}
 private static void Reply(EntityPlayer player,Receipt r){lastReply=Decode(Encode(r));replies++;}
 private static bool QueueProof(EntityPlayer p,Receipt r)=>queue;private static bool QueueAbsentProof(EntityPlayer p,Receipt r,bool laterLife=false)=>absent&&(!laterLife||p.Died>r.Deaths&&r.Cancelled&&r.CancelReturned);
 private static bool TryAuthorizeOriginalPayment(EntityPlayer p,Receipt r,out string proof){proof="";return payment;}
 private static bool BaseOnlyNativeProof(EntityPlayer p,Receipt r,bool after,out ItemValue value){ended++;value=ReadItem(after?r.After:r.Expected);return p.Died>r.Deaths&&r.Admitted&&(after?afterProof:beforeProof);}
 private static bool TryMergeBaseOnly(ItemValue value,Receipt r,out ItemValue merged){merged=value;return r.BaseOnly&&r.Admitted&&r.Before==r.After;}
 private static ItemValue ReadItem(string text)=>new ItemValue{Image=text};private static string Image(ItemValue value)=>value.Image;
 private static bool ValidNativeOutput(Receipt r,ItemValue value)=>value.Image==r.Expected;
 private static bool NativeProof(EntityPlayer p,Receipt r,bool after){if(after&&r.After!="corrected-output"||!after&&r.Before!=r.Expected)throw new Exception("wrong native proof target");return after?afterProof:beforeProof;}
 private static bool TryMerge(ItemValue native,Receipt r,out ItemValue result){result=ReadItem(r.After);return r.Before==r.Expected&&r.After=="corrected-output";}
// ACTUAL_SERVER
// ACTUAL_HISTORICAL_CANCEL
// ACTUAL_ENDED_BASE
 private static Receipt Reset(){
  world=new World();world.Actor=new EntityPlayer{world=world};GameManager.Instance.World=world;
  stored=lastReply=null;saves=replies=ended=0;queue=absent=payment=beforeProof=afterProof=failPublished=false;
  RebirthRepairSignatureService.Calculations=RebirthEquipmentRepairTrainingService.Awards=0;RebirthEquipmentRepairTrainingService.Paid.Clear();
  return new Receipt{Operation=Guid.NewGuid(),World=world.worldState.Guid,Owner="owner",Creation="creation",Deaths=2,Input="native-input",Expected="native-output",Amount=50};
 }
 private static void Call(Receipt r)=>ReceiveServer(world,1,Encode(r));
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 internal static void Exercise(){
  var request=Reset();Call(request);Need(stored==null&&saves==0,"missing native queued proof admitted repair");queue=true;Call(request);Need(stored==null,"unpaid original admitted repair");payment=true;Call(request);Need(stored.Admitted&&stored.Before.Length==0&&replies==1,"native paid original admission missing");
  request=Decode(Encode(stored));request.Before="forged-output";request.OutputInvoking=true;beforeProof=true;Call(request);Need(stored.After.Length==0&&RebirthRepairSignatureService.Calculations==0,"invalid native output entered original correction");
  beforeProof=false;request.Before=request.Expected;Call(request);Need(stored.After.Length==0&&RebirthRepairSignatureService.Calculations==0,"interrupted output invocation treated as delivery without positive native proof");
  beforeProof=true;Call(request);Need(stored.NativeDelivered&&!stored.OutputInvoking&&stored.After=="corrected-output"&&RebirthRepairSignatureService.Calculations==1&&!stored.Published,"positive native output failed original correction");
  Call(request);Need(RebirthRepairSignatureService.Calculations==1,"duplicate output reply recomputed original correction");
  afterProof=true;failPublished=true;Call(request);Need(!stored.Published&&RebirthEquipmentRepairTrainingService.Awards==1,"publication failure lost retained unacknowledged award");failPublished=false;Call(request);
  Need(stored.Published&&RebirthEquipmentRepairTrainingService.Awards==1,"saved award retry duplicated original training");Call(request);Need(RebirthEquipmentRepairTrainingService.Awards==1&&lastReply.Published,"terminal reply replay mutated original award");
  request=Reset();queue=payment=true;Call(request);request=Decode(Encode(stored));request.Cancelled=true;Call(request);Need(!stored.Cancelled,"missing queue-absence proof cancelled original");absent=true;Call(request);Need(!stored.Cancelled&&!stored.CancelConfirmed,"queue absence certified unknown native cancellation return");request.CancelReturned=true;Call(request);Need(stored.Cancelled&&stored.CancelReturned&&stored.CancelConfirmed&&RebirthRepairSignatureService.Calculations==0,"positive cancellation withdrawal failed");
  request=Reset();queue=payment=true;Call(request);request=Decode(Encode(stored));request.Cancelled=true;world.Actor.Died++;absent=true;Call(request);Need(!stored.CancelConfirmed,"later life inferred a missing native cancel return");
  request.CancelReturned=true;absent=false;Call(request);Need(!stored.CancelConfirmed,"later life skipped positive current native queue proof");absent=true;Call(request);Need(stored.CancelConfirmed&&stored.CancelReturned&&RebirthRepairSignatureService.Calculations==0&&RebirthEquipmentRepairTrainingService.Awards==0,"positive old-life cancellation metadata invoked correction/training or stayed held");
  Call(request);Need(lastReply.CancelConfirmed&&RebirthRepairSignatureService.Calculations==0,"historical cancellation terminal replay changed native outcome");
  request=Reset();request.Cancelled=request.CancelReturned=true;world.Actor.Died++;absent=true;Call(request);Need(stored?.CancelConfirmed==true&&!stored.Admitted,"unadmitted local-kit cancellation minted admission or could not retire metadata");
  request=Reset();request.Cancelled=request.CancelReturned=true;request.Payment="unproved paid source";world.Actor.Died++;absent=true;Call(request);Need(stored==null,"historical cancellation manufactured absent paid source admission");
  request=Reset();queue=payment=true;Call(request);world.Actor.Died++;Call(request);Need(ended==1&&RebirthRepairSignatureService.Calculations==0,"ended-life request entered same-life correction");
  beforeProof=afterProof=true;Call(request);Need(stored.BaseOnly&&stored.BaseOnlyConfirmed&&stored.Before==stored.After&&RebirthEquipmentRepairTrainingService.Awards==0,"already admitted local-kit base could not settle metadata without native correction/training");
  for(int variant=0;variant<4;variant++){
   request=Reset();queue=payment=true;if(variant==0)world.Actor.Owner="foreign";if(variant==1)world.Actor.Dead=true;if(variant==2)world.Actor.Creation="foreign";if(variant==3)GameManager.Instance.World=new World();
   Call(request);Need(stored==null&&saves==0,"foreign request published repair original "+variant);
  }
  Console.WriteLine("PASS actual full repair ReceiveServer lifecycle; native proof/store/codec/training are explicit adapters, real topology NOT RUN");
 }
}
internal static class Program {public static void Main()=>RebirthRepairOriginalCorrection.Exercise();}
