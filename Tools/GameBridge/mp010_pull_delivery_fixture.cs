// AUTHORED_NOT_RUN. Actual delivery/retirement and local completion methods;
// native item codec, owner scope, journals and grid receipts are explicit adapters.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
class ItemStack {internal string Tag="rich";internal int count=3;internal bool IsEmpty()=>count==0;}
class EntityPlayerLocal {internal World world=new World();internal int Died;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayerLocal player)=>"creation";}
static class GameIO{internal static string GetSaveGameDir()=>"fixture";}
namespace UnityEngine{static class Time{internal static float realtimeSinceStartup;}}
class World {internal State worldState=new State();internal class State {internal string Guid="world";}}
class RebirthStablePlayerIdentity {internal string CanonicalId="owner";internal static bool TryResolveServerEntity(EntityPlayerLocal player,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}}
static class RebirthCamperTransferJournal {
 internal enum Phase {Prepared,Uncertain,Applied,Rejected}
 internal class Record {internal Guid Operation;internal string OwnerCreation="creation";internal byte[] Before,After;internal Phase State=Phase.Uncertain;}
}
class Journal {
 internal RebirthCamperTransferJournal.Record Record;internal bool FailAdvance;internal int Reads,MissingAtRead,ThrowAtRead;
 internal RebirthCamperTransferJournal.Record Read(Guid operation){Reads++;if(Reads==ThrowAtRead)throw new IOException("injected history conflict");return Reads==MissingAtRead?null:Record;}
 internal RebirthCamperTransferJournal.Record Advance(Guid operation,RebirthCamperTransferJournal.Phase before,RebirthCamperTransferJournal.Phase after){if(FailAdvance)throw new IOException("injected metadata publication failure");if(Record.State!=before)throw new Exception("wrong previous phase");Record.State=after;return Record;}
}
static class RebirthCookingRemoteAdmission {internal static XElement ReadProtocolXml(string value,int limit){if(value.Length>limit)throw new InvalidDataException();return XElement.Parse(value);}}
static class RemoteResourceDiagnostics {internal static void Write(string value){}}
static class RebirthCookingGridCustody {
 internal static bool ReturnHistoricalFetched(EntityPlayerLocal player,long request,IList<ItemStack> items,int sourceDeaths)=>false; // Actual new recipient path has a separate composed cohort.
 internal static bool Claimed,Completed;
 internal static bool HasFetchClaim(EntityPlayerLocal player,long request,out bool complete){complete=Completed;return Claimed;}
 internal static bool RetireFetchedTerminal(EntityPlayerLocal player,long request,string sourceWorld,string sourceOperation,string sourceBinding,string child,string hash,string proof,string disposition)=>false; // Actual receipt retirement has its own full-source fixture.
}
static class RebirthCamperInventorySnapshot {
 internal static Action AfterRestore;
 internal static byte[] Capture(ItemStack[] items){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write(items.Length);foreach(var item in items){writer.Write(item.Tag);writer.Write(item.count);}writer.Flush();return stream.ToArray();}}
 internal static ItemStack[] Restore(byte[] image){using(var reader=new BinaryReader(new MemoryStream(image))){var result=new ItemStack[reader.ReadInt32()];for(int i=0;i<result.Length;i++)result[i]=new ItemStack{Tag=reader.ReadString(),count=reader.ReadInt32()};AfterRestore?.Invoke();return result;}}
}
public static partial class RebirthCookingPull {
 internal const int MaximumIngredientRequests=12;
 // ACTUAL_ORIGINAL
 private static readonly Dictionary<long,Original> waiting=new Dictionary<long,Original>();
 private static World bootstrapWorld;private static string bootstrapDirectory;private static float bootstrapAt;private static int MetadataSends;
 private static void Send(Original original){if(!original.MetadataOnly)throw new Exception("fixture only adapts metadata transport");MetadataSends++;original.Next=UnityEngine.Time.realtimeSinceStartup+3;}
 private static Journal resultJournal,requestJournal;private static bool Current=true;private static Guid operation=Guid.NewGuid();
 private static bool CurrentPullSession(Original original)=>Current;
 // ACTUAL_DELIVERY_SESSION
 private static Journal Requests()=>requestJournal;private static Journal Results()=>resultJournal;
 private static Guid Operation(EntityPlayerLocal player,string owner,long request)=>operation;
 // ACTUAL_COMPLETE
 private static Original Setup(bool noPayment=false){
  Current=true;waiting.Clear();RebirthCamperInventorySnapshot.AfterRestore=null;RebirthCookingGridCustody.Claimed=RebirthCookingGridCustody.Completed=false;
  var original=new Original{Player=new EntityPlayerLocal(),Owner="owner",Creation="creation",Deaths=0,Request=17};original.World=original.Player.world;
  var root=new XElement("cookingPullOriginal",new XAttribute("version",2),new XAttribute("deaths",0),new XAttribute("creation","creation"),new XAttribute("world","world"),new XAttribute("owner","owner"),new XAttribute("request",17),
   new XAttribute("sourceWorld","source-world"),new XAttribute("sourceOperation","source-op"),new XAttribute("sourceBinding","source-binding"),
   new XAttribute("noPayment",noPayment),new XAttribute("error",noPayment?"known refusal":""),new XElement("items",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(noPayment?new ItemStack[0]:new[]{new ItemStack()}))));
  var image=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
  resultJournal=new Journal{Record=new RebirthCamperTransferJournal.Record{Operation=operation,Before=image,After=image}};
  requestJournal=new Journal{Record=new RebirthCamperTransferJournal.Record{Operation=operation,Before=image,After=image}};waiting[17]=original;return original;
 }
 private static void Need(bool value,string error){if(!value)throw new Exception(error);}
 internal static void Exercise(){
  ExerciseSendRecovery();
  var terminalRetry=Setup();terminalRetry.Needs=new List<ItemStack>{new ItemStack()};
  var terminalRequest=new XElement("cookingPullOriginal",new XAttribute("version",2),new XAttribute("deaths",0),new XAttribute("request",17),new XAttribute("world","world"),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XElement("needs",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(terminalRetry.Needs.ToArray()))));
  byte[] terminalBytes=Encoding.UTF8.GetBytes(terminalRequest.ToString(SaveOptions.DisableFormatting));requestJournal.Record.Before=terminalBytes;requestJournal.Record.After=terminalBytes;requestJournal.Record.State=RebirthCamperTransferJournal.Phase.Applied;
  MetadataSends=0;bootstrapWorld=null;UnityEngine.Time.realtimeSinceStartup=0;
  Need(RequeryGridTerminal(terminalRetry.Player,17)&&MetadataSends==1&&waiting[17].MetadataOnly&&waiting[17].Callback==null&&!CurrentDeliverySession(waiting[17]),"applied request must requery exact terminal without native delivery callback");
  Need(requestJournal.Record.State==RebirthCamperTransferJournal.Phase.Applied&&resultJournal.Record.State==RebirthCamperTransferJournal.Phase.Uncertain,"metadata terminal query must not demote or retire local journals");
  Need(RequeryGridTerminal(terminalRetry.Player,17)&&MetadataSends==1,"terminal requery must retain same paced wait");
  var ended=Setup();int endedCalls=0;ended.Callback=(id,items,error)=>endedCalls++;ended.Player.Died++;
  DeliverOriginalResult(ended,new List<ItemStack>{new ItemStack()},"");
  Need(endedCalls==0&&waiting.ContainsKey(17)&&ended.Callback==null&&resultJournal.Record.State==RebirthCamperTransferJournal.Phase.Uncertain,"ended life callback retires while exact paid custody waits for new native return witness");
  ended=Setup();ended.Deaths=-1;ended.Callback=(id,items,error)=>endedCalls++;
  DeliverOriginalResult(ended,new List<ItemStack>{new ItemStack()},"");Need(endedCalls==0&&waiting.ContainsKey(17),"unknown legacy life cannot grant saved items");
  var o=Setup();int calls=0;o.Callback=(id,items,error)=>{calls++;RebirthCookingGridCustody.Claimed=true;throw new Exception("after original grid admission");};
  DeliverOriginalResult(o,new List<ItemStack>{new ItemStack()},"");Need(calls==1&&waiting.ContainsKey(17)&&o.Callback==null,"callback failure forgot custody or retained replay delegate");
  DeliverOriginalResult(o,new List<ItemStack>{new ItemStack()},"");Need(calls==1,"duplicate result repeated callback");
  RebirthCookingGridCustody.Completed=true;requestJournal.FailAdvance=true;
  DeliverOriginalResult(o,new List<ItemStack>{new ItemStack()},"");Need(waiting.ContainsKey(17)&&resultJournal.Record.State==RebirthCamperTransferJournal.Phase.Applied&&requestJournal.Record.State==RebirthCamperTransferJournal.Phase.Uncertain,"partial local metadata publication erased wait");
  requestJournal.FailAdvance=false;DeliverOriginalResult(o,new List<ItemStack>{new ItemStack()},"");Need(!waiting.ContainsKey(17)&&calls==1,"metadata retry replayed grant or failed to retire");
  o=Setup();calls=0;o.Callback=(id,items,error)=>calls++;DeliverOriginalResult(o,new List<ItemStack>{new ItemStack()},"");
  Need(waiting.ContainsKey(17)&&calls==1,"opaque callback return was treated as delivery receipt");DeliverOriginalResult(o,new List<ItemStack>{new ItemStack()},"");Need(calls==1,"opaque callback automatically replayed");
  o=Setup();var offered=new ItemStack();o.Callback=(id,items,error)=>{items[0].Tag="changed";items[0].count=0;};DeliverOriginalResult(o,new List<ItemStack>{offered},"");Need(offered.Tag=="rich"&&offered.count==3,"UI callback mutated retained result payload");
  o=Setup();calls=0;o.Callback=(id,items,error)=>calls++;Current=false;DeliverOriginalResult(o,new List<ItemStack>{new ItemStack()},"");Need(calls==0&&waiting.ContainsKey(17),"stale session invoked callback");
  o=Setup();calls=0;o.Callback=(id,items,error)=>calls++;RebirthCamperInventorySnapshot.AfterRestore=()=>o.Player.Died++;DeliverOriginalResult(o,new List<ItemStack>{new ItemStack()},"");Need(calls==0&&waiting.ContainsKey(17)&&o.Callback==null,"life changed during native item hydration reached old callback");
  o=Setup();resultJournal.Record=null;Need(!RetireTerminalResult(o,"source-world","source-op","source-binding")&&waiting.ContainsKey(17),"terminal reply without local evidence erased original");
  foreach(int mismatch in new[]{0,1,2}){
   o=Setup();RebirthCookingGridCustody.Claimed=RebirthCookingGridCustody.Completed=true;
   Need(!RetireTerminalResult(o,mismatch==0?"other":"source-world",mismatch==1?"other":"source-op",mismatch==2?"other":"source-binding")&&waiting.ContainsKey(17),"foreign terminal source retired original");
  }
  o=Setup();RebirthCookingGridCustody.Claimed=true;Need(!RetireTerminalResult(o,"source-world","source-op","source-binding")&&waiting.ContainsKey(17),"pending grid claim mistaken for delivered receipt");
  RebirthCookingGridCustody.Completed=true;Need(RetireTerminalResult(o,"source-world","source-op","source-binding")&&!waiting.ContainsKey(17),"matching positive original terminal failed");
  o=Setup(true);Need(RetireTerminalResult(o,"source-world","source-op","source-binding")&&!waiting.ContainsKey(17),"saved empty no-payment original could not retire");
  o=Setup(true);requestJournal.FailAdvance=true;Need(!RetireTerminalResult(o,"source-world","source-op","source-binding")&&waiting.ContainsKey(17),"no-payment metadata failure discarded wait");
  Console.WriteLine("PASS actual cooking pull callback/local-journal retirement with native adapters; full protocol/persistence/gameplay NOT RUN");
 }
 private static void ExerciseSendRecovery(){
  foreach(int version in new[]{1,2})for(int variant=0;variant<15;variant++){
   var o=Setup();o.Deaths=version==1?-1:0;o.Needs=new List<ItemStack>{new ItemStack()};
   var root=new XElement("cookingPullOriginal",new XAttribute("version",version),version==2?new XAttribute("deaths",0):null,new XAttribute("request",17),new XAttribute("world","world"),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XElement("needs",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(o.Needs.ToArray()))));
   var bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));requestJournal.Record.Before=bytes;requestJournal.Record.After=bytes;
   requestJournal.Record.State=RebirthCamperTransferJournal.Phase.Prepared;
   Need(PrepareOriginalSend(o)&&requestJournal.Record.State==RebirthCamperTransferJournal.Phase.Uncertain,"prepared crash could not recover exact original");
   Need(PrepareOriginalSend(o),"same uncertain original could not retry");
   switch(variant){
    case 0:o.Needs[0].Tag="changed";break;case 1:o.Needs[0].count++;break;case 2:requestJournal.Record.OwnerCreation="foreign";break;
    case 3:requestJournal.Record.Operation=Guid.NewGuid();break;case 4:requestJournal.Record.After=new byte[]{1};break;
    case 5:requestJournal.Record.State=RebirthCamperTransferJournal.Phase.Applied;break;case 6:requestJournal.Record.State=RebirthCamperTransferJournal.Phase.Rejected;break;
    case 7:requestJournal.Record=null;break;case 8:Current=false;break;case 9:o.Request=0;break;case 10:o.Needs.Clear();break;
    case 11:requestJournal.Record.State=RebirthCamperTransferJournal.Phase.Prepared;requestJournal.FailAdvance=true;break;
    case 12:requestJournal.MissingAtRead=requestJournal.Reads+2;break;
    case 13:requestJournal.ThrowAtRead=requestJournal.Reads+2;break;
    case 14:o.Deaths++;break;
   }
   Need(!PrepareOriginalSend(o),"invalid original became sendable "+variant);
   if(variant==11)Need(requestJournal.Record.State==RebirthCamperTransferJournal.Phase.Prepared,"failed publication lost prepared evidence");
  }
 }
}
// ACTUAL_DELIVERY
static class Program {public static void Main()=>RebirthCookingPull.Exercise();}
