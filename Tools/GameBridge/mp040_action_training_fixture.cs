// AUTHORED_NOT_RUN. Production training stage and award adapter; native/save adapters explicit.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
// ACTUAL_ACTION_TRAINING

enum RebirthElectricalServiceKind {Device}
class EntityPlayer {internal string Account="owner",Creation="creation";}
class Position {internal int x=1,y=2,z=3;}
class Candidate {internal int ClrIdx;internal Position Position=new Position();}
static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class GameIO {internal static string Root;internal static string GetSaveGameDir()=>Root;}
static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer actor)=>actor.Creation;}
class RebirthStablePlayerIdentity {
 internal string CanonicalId;
 internal static bool TryResolveServerEntity(EntityPlayer actor,out RebirthStablePlayerIdentity owner){owner=new RebirthStablePlayerIdentity{CanonicalId=actor.Account};return actor.Account!=null;}
}
static class RebirthSkillTrainingProfileRegistry {
 internal static bool Available=true;internal static float MaxDiscreteAward=.6f;
 internal static bool TryGet(string key,out object profile){profile=Available?new object():null;return Available;}
}
static class RebirthSkillAwardService {
 internal static readonly HashSet<string> Applied=new HashSet<string>(),Saved=new HashSet<string>();
 internal static bool Save=true;internal static int Calls,Effects;internal static float LastRaw;
 internal static bool TryAwardDurable(EntityPlayer actor,string skill,float raw,string source,float interval,string receipt,out float applied,out float attribute){
  Calls++;LastRaw=raw;applied=attribute=0;
  if(Applied.Add(receipt)){Effects++;applied=raw;}
  if(!Save)return false;Saved.Add(receipt);return true;
 }
}
static class RebirthWorldCharacterRepository {
 internal static bool FinalReadback=true;
 internal static bool HasSavedElectricalActionTraining(RebirthStablePlayerIdentity owner,string creation,Guid operation)=>FinalReadback&&RebirthSkillAwardService.Saved.Contains("electrical-action:"+operation.ToString("N"));
}
static class RebirthProductiveEarnedObservationBudget {
 sealed class Lease:IDisposable {public void Dispose(){}}
 internal static bool Available=true;
 internal static bool TryRetainDetached(long bytes,out IDisposable lease){lease=Available?new Lease():null;return lease!=null;}
}
static class RebirthPhase9TechnicalTrainingService {
 static readonly Dictionary<RebirthElectricalServiceKind,float> ElectricalComplexity=new Dictionary<RebirthElectricalServiceKind,float>();
 internal static float electricalComplexityGain=.15f,electricalDefaultComplexity=3f;
 static void Ensure(){}
// ACTUAL_ELECTRICAL_AWARD
}
public static partial class RemoteResourceTransactions {
 internal enum ActionPaymentState {Prepared,Paid,Used}
 internal sealed class Leg {internal bool AfterSaved=true;}
 internal sealed class ActionPayment {
  internal Guid Operation=Guid.NewGuid();internal bool Current=true,PublicationReturned=true,ServiceAfterSaved=true,TargetAfterSaved=true,OwnerAfterSaved=true,Terminal;
  internal bool TrainingRequired=true;internal ActionPaymentState State=ActionPaymentState.Paid;internal readonly List<Leg> Legs=new List<Leg>{new Leg()};
  internal EntityPlayer Actor=new EntityPlayer();internal Candidate ServiceCandidate=new Candidate();internal float TrainingRaw;
  internal string Save,Account="owner",Creation="creation";internal readonly Dictionary<string,XElement> Events=new Dictionary<string,XElement>();
  internal long TrainingNextAttempt;
  internal bool TargetCurrent()=>Current;
 }
 static readonly object actionOwnerGate=new object();static readonly List<ActionPayment> actionPayments=new List<ActionPayment>();
 internal static bool FailSavedEvent;
 static void ActionRecord(ActionPayment payment,string stage,XElement detail){
  if(stage=="training-saved"&&FailSavedEvent)throw new IOException("injected metadata publication failure");
  if(payment.Events.TryGetValue(stage,out var prior)){if(!XNode.DeepEquals(prior,detail))throw new Exception("changed event");return;}
  payment.Events.Add(stage,new XElement(detail));
 }
 static void ActionPath(string path){}
 static byte[] ActionBytes(string path,int expected){var bytes=File.ReadAllBytes(path);if(bytes.Length!=expected)throw new IOException();return bytes;}
 static XElement ReadActionBoundedXml(byte[] bytes,int maximum)=>XElement.Parse(System.Text.Encoding.UTF8.GetString(bytes));
 static XElement ReadRemoteActionOriginal(string text)=>XElement.Parse(text);
 internal static bool Complete(ActionPayment payment)=>CompleteActionTraining(payment);
 internal static float Read(XElement original,Guid operation)=>ReadActionTraining(original,operation);
 internal static XElement Image(Guid operation,float raw)=>ActionTrainingImage(operation,raw);
 internal static ISet<string> Protect(HashSet<string> receipts)=>ProtectActionTrainingReceipts(receipts);
 internal static void Retain(ActionPayment payment)=>actionPayments.Add(payment);
}
static class Program {
 static void Need(bool value,string error){if(!value)throw new Exception(error);}
 static RemoteResourceTransactions.ActionPayment New(float raw=.45f)=>new RemoteResourceTransactions.ActionPayment{Save=Path.GetFullPath(GameIO.Root),TrainingRaw=raw};
 static void Terminal(RemoteResourceTransactions.ActionPayment payment){
  string directory=Path.Combine(GameIO.Root,"RebirthData","ActionPayments",payment.Operation.ToString("N"));Directory.CreateDirectory(directory);
  var original=new XElement("resourceActionOriginal",new XAttribute("operation",payment.Operation.ToString("N")),new XAttribute("save",Path.GetFullPath(GameIO.Root)),RemoteResourceTransactions.Image(payment.Operation,payment.TrainingRaw));
  var head=new XElement("actionPaymentEvent",new XAttribute("version",1),new XAttribute("sequence",12),new XAttribute("stage","used"),new XElement("original",original.ToString(SaveOptions.DisableFormatting)));
  File.WriteAllText(Path.Combine(directory,"current.xml"),head.ToString(SaveOptions.DisableFormatting));File.Copy(Path.Combine(directory,"current.xml"),Path.Combine(directory,"000012-used.xml"));
 }
 public static void Main(){
  GameIO.Root=Path.Combine(Path.GetTempPath(),"rebirth-training-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(GameIO.Root);
  try{Run();}finally{Directory.Delete(GameIO.Root,true);}
 }
 static void Run(){
  float frozen=RebirthPhase9TechnicalTrainingService.CaptureElectricalOriginalTraining(RebirthElectricalServiceKind.Device);
  Need(Math.Abs(frozen-.45f)<.00001,"original discrete normalization changed");
  RebirthPhase9TechnicalTrainingService.electricalComplexityGain=.5f;RebirthSkillTrainingProfileRegistry.MaxDiscreteAward=.1f;
  Need(RebirthPhase9TechnicalTrainingService.CaptureElectricalOriginalTraining(RebirthElectricalServiceKind.Device)==.1f,"configured discrete cap bypassed");
  for(int variant=0;variant<7;variant++){
   var payment=New(frozen);int calls=RebirthSkillAwardService.Calls;
   if(variant==0)payment.Current=false;if(variant==1)payment.State=RemoteResourceTransactions.ActionPaymentState.Prepared;
   if(variant==2)payment.PublicationReturned=false;if(variant==3)payment.ServiceAfterSaved=false;if(variant==4)payment.TargetAfterSaved=false;
   if(variant==5)payment.OwnerAfterSaved=false;if(variant==6)payment.Legs[0].AfterSaved=false;
   Need(!RemoteResourceTransactions.Complete(payment)&&RebirthSkillAwardService.Calls==calls,"unqualified payment awarded training");
  }
  var originalPayment=New(frozen);RebirthSkillAwardService.Save=false;int effects=RebirthSkillAwardService.Effects;
  var modeOff=New(0f);modeOff.TrainingRequired=false;int modeOffCalls=RebirthSkillAwardService.Calls;
  Need(RemoteResourceTransactions.Complete(modeOff)&&modeOff.Events.ContainsKey("training-not-required")&&RebirthSkillAwardService.Calls==modeOffCalls,"mode-off service attempted training or blocked completion");
  Need(!RemoteResourceTransactions.Complete(originalPayment)&&!originalPayment.Events.ContainsKey("training-saved"),"failed saved award terminalized");
  Need(RebirthSkillAwardService.Effects==effects+1&&Math.Abs(RebirthSkillAwardService.LastRaw-frozen)<.00001,"original reward repriced");
  int callsBeforeThrottle=RebirthSkillAwardService.Calls;Need(!RemoteResourceTransactions.Complete(originalPayment)&&RebirthSkillAwardService.Calls==callsBeforeThrottle,"failed save retried every frame");
  RebirthSkillAwardService.Save=true;RebirthWorldCharacterRepository.FinalReadback=false;
  originalPayment.TrainingNextAttempt=0;
  Need(!RemoteResourceTransactions.Complete(originalPayment)&&RebirthSkillAwardService.Effects==effects+1,"unread final award duplicated or accepted");
  RebirthWorldCharacterRepository.FinalReadback=true;RemoteResourceTransactions.FailSavedEvent=true;
  originalPayment.TrainingNextAttempt=0;
  try{RemoteResourceTransactions.Complete(originalPayment);throw new Exception("missing metadata fault");}catch(IOException){}
  RemoteResourceTransactions.FailSavedEvent=false;
  originalPayment.TrainingNextAttempt=0;
  Need(RemoteResourceTransactions.Complete(originalPayment)&&RemoteResourceTransactions.Complete(originalPayment),"saved original retry failed");
  Need(RebirthSkillAwardService.Effects==effects+1&&originalPayment.Events.Count==2,"retry repeated effect or event");
  var image=new XElement("resourceActionOriginal",RemoteResourceTransactions.Image(originalPayment.Operation,frozen));
  Need(RemoteResourceTransactions.Read(image,originalPayment.Operation)==frozen,"frozen original read changed amount");
  for(int variant=0;variant<5;variant++){
   var changed=new XElement(image);var training=changed.Element("training");
   if(variant==0)training.Remove();if(variant==1)changed.Add(new XElement(training));if(variant==2)training.SetAttributeValue("receipt","foreign");
   if(variant==3)training.SetAttributeValue("raw","NaN");if(variant==4)training.Add(new XElement("unexpected"));
   bool refused=false;try{RemoteResourceTransactions.Read(changed,originalPayment.Operation);}catch(InvalidDataException){refused=true;}Need(refused,"malformed training original accepted");
  }
  string receipt="electrical-action:"+originalPayment.Operation.ToString("N");var receipts=new HashSet<string>{receipt,"unrelated","electrical-action:invalid"};
  Need(RemoteResourceTransactions.Protect(receipts).Contains(receipt),"missing cold journal lost receipt");Terminal(originalPayment);
  Need(!RemoteResourceTransactions.Protect(receipts).Contains(receipt),"exact terminal failed to release receipt");
  string directory=Path.Combine(GameIO.Root,"RebirthData","ActionPayments",originalPayment.Operation.ToString("N"));
  File.WriteAllText(Path.Combine(directory,"current.xml.tmp"),"unknown");Need(RemoteResourceTransactions.Protect(receipts).Contains(receipt),"staged unknown lost receipt");File.Delete(Path.Combine(directory,"current.xml.tmp"));
  RebirthProductiveEarnedObservationBudget.Available=false;Need(RemoteResourceTransactions.Protect(receipts).Contains(receipt),"budget refusal lost receipt");RebirthProductiveEarnedObservationBudget.Available=true;
  RemoteResourceTransactions.Retain(originalPayment);Need(RemoteResourceTransactions.Protect(receipts).Contains(receipt),"live unresolved original lost receipt");
  Need(RemoteResourceTransactions.Protect(receipts).Contains("electrical-action:invalid"),"malformed retained receipt discarded");
  Console.WriteLine("PASS actual electrical training stage/adapter/retention with explicit award/native/journal/readback adapters; game/save integration NOT RUN");
 }
}
