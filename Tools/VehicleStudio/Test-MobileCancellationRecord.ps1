#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperCode="using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Security.Cryptography;using System.Text;using System.Xml.Linq;`n"
foreach($camperRelative in @('Scripts/Vehicles/Camper/RebirthCamperStationAddress.cs','Scripts/Vehicles/Camper/RebirthCamperStationCancellationRecord.cs')){
 $camperCode += [regex]::Replace([IO.File]::ReadAllText((Join-Path $camperRoot $camperRelative)),'(?m)^using [^;]+;\r?\n','')+"`n"
}
$camperFixture=@'
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public class RebirthStationGridAdmission {
 public string JobId="job",CreationId="creation";public RebirthCamperStationAddress Address;
 public XElement Write()=>new XElement("admission",new XAttribute("job",JobId),new XAttribute("creation",CreationId),new XAttribute("address",Address));
 public bool TryGetMobileAddress(out RebirthCamperStationAddress a){a=Address;return true;}
}
public class RebirthCamperStationPublicationRecord {
 public bool Valid=true;public XElement Image;public XElement Write()=>new XElement(Image);
 public bool MatchesSaveScope(string scope)=>scope=="primary";
 public static bool TryRead(XElement n,RebirthStationGridAdmission a,out RebirthCamperStationPublicationRecord p){p=new RebirthCamperStationPublicationRecord{Image=n};return n!=null&&(string)n.Attribute("valid")=="yes";}
}
public class RebirthStationTerminalIntent {public bool IsCompletion;public XElement Image=new XElement("intent",new XAttribute("actor",99));public XElement Write()=>new XElement(Image);}
public class RebirthStationCancellationRefund {public XElement Image=new XElement("refund");public XElement Write()=>new XElement(Image);}
public class RebirthStationCancellationAttempt {public bool Valid=true;public XElement Image=new XElement("attempt");public XElement Write()=>new XElement(Image);public static bool TryRead(XElement n,RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationCancellationRefund r,out RebirthStationCancellationAttempt t){t=new RebirthStationCancellationAttempt();return n!=null&&i!=null&&!i.IsCompletion&&r!=null&&n.Name=="attempt";}}
public class Recipe {public string Job;}public class QueueRow {public Recipe Recipe;}public static class RebirthStationGridQueue {public static bool IsMarked(Recipe r)=>r!=null;public static bool TryGetJobId(Recipe r,out string job){job=r.Job;return job!=null;}}public static class RebirthStationCompletionReceipt {public static bool HasNoJobReceipt(object receipts,string job)=>receipts==null;}public class Station {public QueueRow[] Queue=new[]{new QueueRow()};public object CraftCompleteList;}
public class Vehicle {public int entityId=99;}
public class Binding {public RebirthStationGridAdmission Admission;public RebirthCamperStationPublicationRecord Publication;public RebirthCamperStationAddress Address;public Vehicle Vehicle=new Vehicle();}
public class RebirthCamperStationCancellationCheckpoint {
 public Binding Binding;public bool Planned=true;public RebirthStationTerminalIntent Intent=new RebirthStationTerminalIntent();public RebirthStationCancellationRefund Refund=new RebirthStationCancellationRefund();public RebirthStationCancellationAttempt Attempt=new RebirthStationCancellationAttempt();
 public RebirthStationTerminalIntent CopyIntent()=>Intent;public RebirthStationCancellationRefund CopyRefund()=>Refund;public RebirthStationCancellationAttempt CopyAttempt()=>Attempt;
 public bool MatchesPlannedQueue(object queue)=>Planned;public string StationDigest(Station station)=>new string('A',64);
}
public class RebirthCamperStationSaveEvidence {public class Publication {
 public RebirthCamperStationAddress Address;public string SaveDirectory="primary",StationDigest=new string('B',64),VehicleEntityDigest=new string('C',64),VehicleBagDigest=new string('D',64),Recovery=new string('A',64);public int VehicleEntityId=99,VehicleClass=5;
 public Station Saved=new Station();public Station RestoreStationSnapshot()=>Saved;public string RecoveryDigest()=>Recovery;
}}
public class RebirthCamperStationCancellationRemoval {public bool Confirmed=true;public RebirthCamperStationSaveEvidence.Publication Proof;public bool TryGetPublication(RebirthCamperStationCancellationCheckpoint c,out RebirthCamperStationSaveEvidence.Publication p){p=Proof;return Confirmed;}public bool IsConfirmedFor(RebirthCamperStationCancellationCheckpoint c)=>Confirmed;}
public static class CancellationRecordFixture {
 static void Check(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(){
  var address=new RebirthCamperStationAddress(Guid.NewGuid(),Guid.NewGuid());
  var a=new RebirthStationGridAdmission{Address=address};var q=new RebirthCamperStationPublicationRecord{Image=new XElement("queued",new XAttribute("valid","yes"),new XAttribute("vehicleId",11),new XAttribute("vehicleClass",5),new XAttribute("queuedOwner",7),new XAttribute("saveScope",new string('E',64)))};
  var c=new RebirthCamperStationCancellationCheckpoint{Binding=new Binding{Admission=a,Publication=q,Address=address}};var op=new RebirthCamperStationCancellationRemoval{Proof=new RebirthCamperStationSaveEvidence.Publication{Address=address}};
  Check(RebirthCamperStationCancellationRecord.TryCreate(c,op,out var record),"original confirmed removal mints record");
  Check(record.MatchesCurrentCancellation(op.Proof,q),"fresh current cancellation allows unrelated native state");op.Proof.Saved.Queue[0].Recipe=new Recipe{Job="other-job"};Check(record.MatchesCurrentCancellation(op.Proof,q),"other paid job advancement allowed");op.Proof.Saved.Queue[0].Recipe=new Recipe{Job="job"};Check(!record.MatchesCurrentCancellation(op.Proof,q),"cancelled job reappearance refused");op.Proof.Saved.Queue[0].Recipe=null;op.Proof.Saved.CraftCompleteList=new object();Check(!record.MatchesCurrentCancellation(op.Proof,q),"target completion receipt refused");op.Proof.Saved.CraftCompleteList=null;
  var node=record.Write();Check((int)node.Attribute("queuedOwner")==7&&(int)node.Attribute("cancelledOwner")==99&&(int)node.Attribute("nativeVehicleId")==99,"historical queue actor distinct from cancelling actor and native ID");
  Check(RebirthCamperStationCancellationRecord.TryRead(node,a,q,c.Intent,c.Refund,c.Attempt,out var parsed),"exact parent-bound record roundtrip");
  foreach(var field in new[]{"job","creation","admission","queued","intent","refund","attempt","stationAddress","stationRecovery","stationSnapshot","entitySnapshot","bagSnapshot","saveScope","queuedOwner","cancelledOwner","nativeVehicleId","vehicleClass","version","phase"}){
   var bad=new XElement(node);bad.SetAttributeValue(field,"invalid");Check(!RebirthCamperStationCancellationRecord.TryRead(bad,a,q,c.Intent,c.Refund,c.Attempt,out _),"changed field denied: "+field);
  }
  var clone=record.Clone().Write();clone.SetAttributeValue("job","changed");Check(record.JobId=="job","immutable record copies");
  op.Confirmed=false;Check(!RebirthCamperStationCancellationRecord.TryCreate(c,op,out _),"unconfirmed removal denied");op.Confirmed=true;c.Planned=false;Check(!RebirthCamperStationCancellationRecord.TryCreate(c,op,out _),"wrong saved queue denied");c.Planned=true;
  op.Proof.VehicleEntityId=100;Check(!RebirthCamperStationCancellationRecord.TryCreate(c,op,out _),"original mint requires current native vehicle");op.Proof.VehicleEntityId=99;
  c.Intent.IsCompletion=true;Check(!RebirthCamperStationCancellationRecord.TryRead(node,a,q,c.Intent,c.Refund,c.Attempt,out _),"completion cannot be cancellation");c.Intent.IsCompletion=false;
  op.Proof.VehicleEntityId=300;Check(record.MatchesRecoveryImage(op.Proof,q),"recovery correlation tolerates changed runtime ID");op.Proof.Address=new RebirthCamperStationAddress(Guid.NewGuid(),address.Installation);Check(!record.MatchesRecoveryImage(op.Proof,q),"different stable vehicle denied");op.Proof.Address=address;op.Proof.Recovery=new string('F',64);Check(!record.MatchesRecoveryImage(op.Proof,q),"different recovery image denied");
  var records=new Dictionary<string,RebirthCamperStationCancellationRecord>{{"job",record}};var admissions=new Dictionary<string,RebirthStationGridAdmission>{{"job",a}};var publications=new Dictionary<string,RebirthCamperStationPublicationRecord>{{"job",q}};var intents=new Dictionary<string,RebirthStationTerminalIntent>{{"job",c.Intent}};var refunds=new Dictionary<string,RebirthStationCancellationRefund>{{"job",c.Refund}};var attempts=new Dictionary<string,RebirthStationCancellationAttempt>{{"job",c.Attempt}};
  var section=RebirthCamperStationCancellationRecord.WriteAll(records,admissions,publications,intents,refunds,attempts);var progression=new XElement("progression",section);
  Check(RebirthCamperStationCancellationRecord.ReadAll(progression,admissions,publications,intents,refunds,attempts,out var loaded)&&loaded.Count==1,"section roundtrip");
  Check(RebirthCamperStationCancellationRecord.ReadAll(new XElement("progression"),admissions,publications,intents,refunds,attempts,out loaded)&&loaded.Count==0,"older saves default empty");
  section.Add(record.Write());Check(!RebirthCamperStationCancellationRecord.ReadAll(progression,admissions,publications,intents,refunds,attempts,out _),"duplicate job denied");
  ThreadManager.Main=false;Check(!record.MatchesRecoveryImage(op.Proof,q),"off-thread correlation refused");
  Console.WriteLine("PASS actual mobile cancellation record codec: immutable full bindings, changed-field refusal, confirmed original mint, distinct actors/runtime ID, recovery image correlation, section roundtrip/legacy default and duplicate rejection. Parent/native publication adapters doubled; no owner-file I/O, refund or gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition ($camperCode+$camperFixture)
[CancellationRecordFixture]::Run()
