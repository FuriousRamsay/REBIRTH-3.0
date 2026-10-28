#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperSource=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperStationReceiptRetirement.cs'))
$camperFixture=@'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
public static class ThreadManager { public static bool IsMainThread()=>true; }
public class CraftCompleteData { public string Job; }
public class RebirthCamperStationState
{
 public List<CraftCompleteData> CraftCompleteList=new List<CraftCompleteData>();public int Changes;
 public void SetModified(){Changes++;}
}
public static class RebirthStationCompletionReceipt
{
 public class Identity { public string Job; }
 public static bool TryReadIdentity(CraftCompleteData row,out Identity i){i=new Identity{Job=row.Job};return row.Job!=null;}
}
public class Admission { public string JobId="job"; }
public class Progression
{
 public Dictionary<string,RebirthCamperStationTrainingRecord> CamperStationTrainingPlans=new Dictionary<string,RebirthCamperStationTrainingRecord>();
 public Dictionary<string,object> StationTerminalIntents=new Dictionary<string,object>();
}
public class Binding
{
 public Admission Admission=new Admission();public RebirthCamperStationState Station=new RebirthCamperStationState();
 public Progression Progression=new Progression();public object Address,Identity,Publication;public bool Valid=true;public bool IsCurrent()=>Valid;
}
public static class RebirthCamperStationCompletionCapture
{
 public class SuccessfulOutput { public bool Valid=true;public bool MatchesRetiredOutput(RebirthCamperStationState s)=>Valid&&s.CraftCompleteList.All(row=>row.Job!="job"); }
}
public class RebirthCamperStationTrainingPlan
{
 public Binding Binding=new Binding();public RebirthCamperStationCompletionCapture.SuccessfulOutput Completed=new RebirthCamperStationCompletionCapture.SuccessfulOutput();
 public object Publication;public bool HasTraining=true;public bool IsCurrent()=>Binding.Valid&&Binding.Station.CraftCompleteList.Count(row=>row.Job=="job")==1;
}
public class RebirthCamperStationTrainingRecord
{
 public string Value="original";public XElement Write()=>new XElement("training",Value);public RebirthCamperStationTrainingRecord Clone()=>new RebirthCamperStationTrainingRecord{Value=Value};
 public static bool TryCreate(RebirthCamperStationTrainingPlan p,out RebirthCamperStationTrainingRecord r){r=new RebirthCamperStationTrainingRecord();return p.IsCurrent();}
}
public class RebirthCamperStationNativeRewardDelivery { public bool Confirmed=true;public bool IsConfirmedFor(RebirthCamperStationTrainingPlan p)=>Confirmed; }
public static class RebirthWorldCharacterRepository
{
 public static bool Saved=true;
 public static bool HasSavedCamperStationTrainingPlan(object i,Admission a,object q,object c,RebirthCamperStationTrainingRecord t,bool award)=>Saved;
 public static bool HasSavedStationTerminalIntent(object i,Admission a,object choice)=>Saved;
}
public static class RebirthCamperStationSaveEvidence
{
 public class Request:IDisposable { public void Dispose(){} }
 public class Publication { public RebirthCamperStationState Snapshot;public RebirthCamperStationState RestoreStationSnapshot()=>Snapshot; }
 public static bool Ready,ThrowBegin,BadSnapshot;public static int Starts;public static RebirthCamperStationState State;
 public static Request BeginRetirement(RebirthCamperStationState s,object address,Func<bool> current)
 {Starts++;if(ThrowBegin){ThrowBegin=false;throw new Exception("save request failure");}if(!current())throw new Exception("stale");State=s;return new Request();}
 public static bool TryObserve(Request r,out Publication p)
 {p=new Publication{Snapshot=BadSnapshot?new RebirthCamperStationState{CraftCompleteList=new List<CraftCompleteData>{new CraftCompleteData{Job="job"}}}:State};return Ready;}
}
public static class RebirthCamperStationCompletionCallsite
{
 public static int Releases;
 internal static bool TryReleaseRetired(RebirthCamperStationCompletionCapture.SuccessfulOutput output,RebirthCamperStationReceiptRetirement r)
 {if(!r.IsSavedFor(output))return false;Releases++;return true;}
}
SOURCE_BODY
public static class RetirementFixture
{
 private static void Check(bool value,string name){if(!value)throw new Exception(name);}
 private static RebirthCamperStationTrainingPlan Plan()
 {
  var p=new RebirthCamperStationTrainingPlan();p.Binding.Station.CraftCompleteList.Add(new CraftCompleteData{Job="job"});
  p.Binding.Station.CraftCompleteList.Add(new CraftCompleteData{Job="other"});p.Binding.Progression.CamperStationTrainingPlans.Add("job",new RebirthCamperStationTrainingRecord());p.Binding.Progression.StationTerminalIntents.Add("job",new object());return p;
 }
 public static void Run()
 {
  var p=Plan();var delivery=new RebirthCamperStationNativeRewardDelivery{Confirmed=false};
  Check(!RebirthCamperStationReceiptRetirement.TryBegin(p,delivery,out _)&&p.Binding.Station.Changes==0,"unconfirmed reward cannot retire");delivery.Confirmed=true;
  Check(RebirthCamperStationReceiptRetirement.TryBegin(p,delivery,out var r),"original retirement begins");
  Check(p.Binding.Station.CraftCompleteList.Count==1&&p.Binding.Station.CraftCompleteList[0].Job=="other"&&p.Binding.Station.Changes==1,"only original receipt removed once");
  Check(!r.IsSavedFor(p.Completed)&&!r.TryConfirm()&&RebirthCamperStationCompletionCallsite.Releases==0,"pending save cannot release");
  RebirthCamperStationSaveEvidence.Ready=true;RebirthCamperStationSaveEvidence.BadSnapshot=true;
  Check(!r.TryConfirm()&&RebirthCamperStationCompletionCallsite.Releases==0,"saved receipt still present cannot release");RebirthCamperStationSaveEvidence.BadSnapshot=false;
  Check(r.TryConfirm()&&r.IsSavedFor(p.Completed)&&RebirthCamperStationCompletionCallsite.Releases==1,"exact saved post-removal confirms release");
  r.Dispose();Check(!r.IsCurrent(),"disposed operation has no authority");
  p=Plan();RebirthCamperStationSaveEvidence.ThrowBegin=true;
  Check(!RebirthCamperStationReceiptRetirement.TryBegin(p,delivery,out r)&&r!=null&&p.Binding.Station.Changes==1,"save request exception retains mutated operation");
  Check(r.TryConfirm()&&p.Binding.Station.Changes==1,"retry requests save without repeating removal");
  p=Plan();Check(RebirthCamperStationReceiptRetirement.TryBegin(p,delivery,out r),"changed-owner operation begins");p.Binding.Valid=false;
  int releases=RebirthCamperStationCompletionCallsite.Releases;Check(!r.TryConfirm()&&RebirthCamperStationCompletionCallsite.Releases==releases,"changed owner cannot release");
  p=Plan();Check(RebirthCamperStationReceiptRetirement.TryBegin(p,delivery,out r),"training custody check begins");
  p.Binding.Progression.CamperStationTrainingPlans["job"].Value="changed";
  Check(!r.TryConfirm()&&RebirthCamperStationCompletionCallsite.Releases==releases,"changed training cannot release");
  p.Binding.Progression.CamperStationTrainingPlans["job"].Value="original";RebirthWorldCharacterRepository.Saved=false;
  Check(!r.TryConfirm()&&RebirthCamperStationCompletionCallsite.Releases==releases,"missing primary owner witness cannot release");RebirthWorldCharacterRepository.Saved=true;
  Console.WriteLine("PASS actual receipt retirement: native reward prerequisite, only original row removal, pending/bad snapshot refusal, saved confirmation, retained request failure and owner invalidation. Native persistence/release adapters doubled; no gameplay proof.");
 }
}
'@
Add-Type -TypeDefinition $camperFixture.Replace('SOURCE_BODY',[regex]::Replace($camperSource,'(?m)^using [^;]+;\r?\n','')) -IgnoreWarnings -WarningAction SilentlyContinue
[RetirementFixture]::Run()
