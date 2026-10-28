#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperSource=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperStationTrainingRecovery.cs'))
$camperStart=$camperSource.IndexOf('    private void TryBeginRetirement(')
$camperEnd=$camperSource.LastIndexOf('}')
if($camperStart -lt 0 -or $camperEnd -le $camperStart){throw 'Cold retirement methods missing.'}
$camperFixture=@'
using System;
using System.Collections.Generic;
using System.Linq;
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public class EntityPlayer { }
public class ItemStack { public ItemStack Clone()=>new ItemStack(); }
public class CraftCompleteData { public string Job; }
public class Admission { public string JobId="job"; }
public class RebirthCamperStationAddress { public string Installation="installation"; }
public class RebirthCamperStationState { public List<CraftCompleteData> CraftCompleteList=new List<CraftCompleteData>();public int Changes;public void SetModified(){Changes++;} }
public static class RebirthStationCompletionReceipt {
 public class Identity { public string Job; }
 public static bool TryReadIdentity(CraftCompleteData CraftCompleteData,out Identity receipt){receipt=new Identity{Job=CraftCompleteData.Job};return CraftCompleteData.Job!=null;}
}
public static class RebirthCamperStationSnapshot {
 public static List<CraftCompleteData> Capture(string i,RebirthCamperStationState s)=>new List<CraftCompleteData>(s.CraftCompleteList);
 public static RebirthCamperStationState Restore(string i,ItemStack item,List<CraftCompleteData> rows)=>new RebirthCamperStationState{CraftCompleteList=new List<CraftCompleteData>(rows)};
}
public static class RebirthCamperStationRecoveryImage { public static string Digest(RebirthCamperStationState s,ItemStack i)=>string.Join(",",s.CraftCompleteList.Select(r=>r.Job)); }
public static class RebirthCamperStationNativeRewardReceipt { public static bool Saved=true;public static bool HasSaved(object i,Admission a,bool applied)=>Saved; }
public static class RebirthCamperStationSaveEvidence {
 public class Request:IDisposable { public bool Closed;public void Dispose(){if(FailDispose)throw new Exception("release failed");Closed=true;} }
 public class Publication { public RebirthCamperStationAddress Address;public string Digest;public string RecoveryDigest()=>Digest; }
 public static bool FailBegin,FailDispose,Ready;public static int Begins;public static Publication Next;
 public static Request BeginRetirement(RebirthCamperStationState s,RebirthCamperStationAddress a,Func<bool> current){Begins++;if(FailBegin)throw new Exception("save request failed");return new Request();}
 public static bool TryObserve(Request r,out Publication p){p=Next;return Ready;}
}
public static class RebirthCamperStationCompletionCallsite {
 public static ColdFixture Held;public static int Releases;public static bool FailRelease;
 public static bool TryHoldRecovery(RebirthCamperStationState s,ColdFixture f){if(Held!=null)return false;Held=f;return true;}
 public static bool TryReleaseRecovery(RebirthCamperStationState s,ColdFixture f){if(FailRelease||Held!=f||!f.IsRetirementSavedFor(s))return false;Releases++;Held=null;return true;}
}
public class ColdFixture {
 private EntityPlayer player=new EntityPlayer();private object identity=new object(),lease=new object();
 private Admission admission=new Admission();private ItemStack installed=new ItemStack();private RebirthCamperStationAddress address=new RebirthCamperStationAddress();
 private RebirthCamperStationState station=new RebirthCamperStationState();private bool Valid=true,closed,retiring,retirementSaved;
 private string retirementDigest;private RebirthCamperStationSaveEvidence.Request request=null,retirementRequest;
 private RebirthCamperStationSaveEvidence.Publication retirementProof;
 private bool IsCurrent()=>Valid&&!closed&&(!retiring||RebirthCamperStationCompletionCallsite.Held==this&&RebirthCamperStationRecoveryImage.Digest(station,installed)==retirementDigest);
METHOD_BODY
 private static void Check(bool v,string n){if(!v)throw new Exception(n);}
 private static ColdFixture Create(){RebirthCamperStationCompletionCallsite.Held=null;RebirthCamperStationSaveEvidence.Ready=false;RebirthCamperStationSaveEvidence.FailBegin=false;RebirthCamperStationSaveEvidence.FailDispose=false;RebirthCamperStationCompletionCallsite.FailRelease=false;var f=new ColdFixture();f.station.CraftCompleteList.Add(new CraftCompleteData{Job="job"});f.station.CraftCompleteList.Add(new CraftCompleteData{Job="other"});return f;}
 public static void Run(){
  var f=Create();RebirthCamperStationNativeRewardReceipt.Saved=false;f.TryBeginRetirement();Check(!f.retiring&&f.station.CraftCompleteList.Count==2,"unconfirmed reward cannot remove receipt");RebirthCamperStationNativeRewardReceipt.Saved=true;
  RebirthCamperStationSaveEvidence.FailBegin=true;try{f.TryBeginRetirement();}catch{}
  Check(f.retiring&&f.station.CraftCompleteList.Count==1&&f.station.CraftCompleteList[0].Job=="other","failed save retains hold and unrelated CraftCompleteData");
  RebirthCamperStationSaveEvidence.FailBegin=false;Check(!f.TryConfirmRetirement(),"pending publication held");Check(f.station.Changes==1,"retry never removes again");
  RebirthCamperStationSaveEvidence.Ready=true;RebirthCamperStationSaveEvidence.Next=new RebirthCamperStationSaveEvidence.Publication{Address=f.address,Digest="wrong"};Check(!f.TryConfirmRetirement()&&!f.retirementSaved,"wrong saved image denied");
  f.retirementProof=null;RebirthCamperStationSaveEvidence.Next.Digest="other";RebirthCamperStationCompletionCallsite.FailRelease=true;Check(!f.TryConfirmRetirement()&&f.retirementSaved,"release failure retains saved hold");
  RebirthCamperStationSaveEvidence.FailDispose=true;try{f.Dispose();}catch{}Check(!f.closed,"failed dispose remains retryable");RebirthCamperStationSaveEvidence.FailDispose=false;f.Dispose();Check(f.closed,"closed operation retained");
  Check(!f.TryResume(new EntityPlayer(),new object(),f.address),"different actor denied");Check(!f.TryResume(f.player,new object(),new RebirthCamperStationAddress()),"different address denied");
  Check(f.TryResume(f.player,new object(),f.address),"saved hold resumes exact owner with new lease");RebirthCamperStationCompletionCallsite.FailRelease=false;Check(f.TryConfirmRetirement(),"confirmed hold released after resume");Check(f.station.Changes==1,"resume never removes or awards again");
  f=Create();f.TryBeginRetirement();f.Dispose();f.Valid=false;Check(!f.TryResume(f.player,new object(),f.address)&&f.closed,"stale authority stays closed");
  Console.WriteLine("PASS actual cold retirement methods: durable reward prerequisite, one removal, save-request failure/retry, wrong image, release/dispose failure, exact owner/address resume and stale authority. Native/save/access adapters doubled; no gameplay acceptance.");
 }
}
'@
Add-Type -TypeDefinition $camperFixture.Replace('METHOD_BODY',$camperSource.Substring($camperStart,$camperEnd-$camperStart))
[ColdFixture]::Run()
