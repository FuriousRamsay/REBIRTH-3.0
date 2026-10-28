#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationCancellationCheckpoint.cs')).Replace('using System;','').Replace('using System.Xml.Linq;','')
$camperFixture=@'
using System;using System.Collections.Generic;using System.Xml.Linq;
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public class ItemStack { public ItemStack Clone()=>new ItemStack(); }
public class RecipeQueueItem { public int Value=1,Multiplier=1,Quality=2;public Recipe Recipe=new Recipe(); }
public class Player { public int entityId=99; }
public static class RebirthCamperStationWindowSession { public static object Lease=new object();public static bool TryGetLeaseIdentity(Player p,out object lease){lease=Lease;return lease!=null;} }
public class Address { public string Installation="install"; }
public class Recipe {public string Job="job";} public static class RebirthStationGridQueue {public static bool TryGetJobId(Recipe r,out string job){job=r.Job;return true;}} public class Admission {public bool MatchesQueuedQuality(Recipe r,int n,int q)=>r.Job==JobId&&n>0&&q==2; public string JobId="job",CreationId="creation"; }
public class Progression { public Dictionary<string,object> CamperStationCompletions=new Dictionary<string,object>(),CamperStationTrainingPlans=new Dictionary<string,object>();public Dictionary<string,RebirthStationTerminalIntent> StationTerminalIntents=new Dictionary<string,RebirthStationTerminalIntent>();public Dictionary<string,RebirthStationCancellationRefund> StationCancellationRefunds=new Dictionary<string,RebirthStationCancellationRefund>();public Dictionary<string,RebirthStationCancellationAttempt> StationCancellationAttempts=new Dictionary<string,RebirthStationCancellationAttempt>(); }
public class State { public ItemStack[] Input=new ItemStack[0];public RecipeQueueItem[] Queue=new[]{new RecipeQueueItem()};public object CraftCompleteList;public int Content=5,Output=3; }
public static class RebirthCamperStationQueuedAuthority { public class Binding { public State Station=new State();public Player Player=new Player();public Address Address=new Address();public Admission Admission=new Admission();public object Owner=new object(),Identity=new object(),Publication=new object(),Vehicle=new object();public Progression Progression=new Progression();public int QueuedOwner=7;public bool Valid=true;public bool IsCurrent()=>Valid; } }
public static class RebirthStationLiveAccess { public static bool TryGetMobileLease(Player p,Address a,string creation,out State station,out object lease){station=Current.Station;return RebirthCamperStationWindowSession.TryGetLeaseIdentity(p,out lease)&&Valid;}public static RebirthCamperStationQueuedAuthority.Binding Current;public static bool Valid=true;public static bool TryResolveMobile(Player p,Address a,string c,out State s,out object owner){s=Current.Station;owner=Current.Owner;return Valid;} }
public static class RebirthCamperStationCompletionCallsite { public static bool Held;public static bool IsHeld(State s)=>Held; }
public static class RebirthCamperStationSaveEvidence { public static bool Pending;public static bool IsPending(State s)=>Pending; }
public static class RebirthWorldCharacterRepository { public static bool Saved=true;public static bool HasSavedCamperStationPublication(object i,Admission a,object p)=>Saved; }
public class RebirthStationTerminalIntent { public int Actor;public RebirthStationTerminalIntent Clone()=>new RebirthStationTerminalIntent{Actor=Actor};public static bool TryCreate(Admission a,bool done,int actor,int remaining,out RebirthStationTerminalIntent i){i=new RebirthStationTerminalIntent{Actor=actor};return true;} }
public class RebirthStationCancellationRefund { public int Amount=4;public RebirthStationCancellationRefund Clone()=>new RebirthStationCancellationRefund{Amount=Amount};public static bool TryCreate(Admission a,object definitions,RebirthStationTerminalIntent i,out RebirthStationCancellationRefund r){r=new RebirthStationCancellationRefund();return true;} }
public static class XUiM_Recipes { public static object GetRecipes()=>new object(); }
public static class RebirthCamperStationCancellationQueuePlan { public static bool TryBuild(RecipeQueueItem[] q,Admission a,RebirthStationTerminalIntent i,int actor,out RecipeQueueItem[] replacement){replacement=new[]{new RecipeQueueItem{Value=0}};return true;} }
public class RebirthStationNativeQueueSnapshot { private int value;public static bool TryCapture(RecipeQueueItem[] q,out RebirthStationNativeQueueSnapshot s){s=new RebirthStationNativeQueueSnapshot{value=q[0].Value};return true;}public bool Matches(RecipeQueueItem[] q)=>q[0].Value==value; }
public class RebirthStationTerminalSnapshot { private int output;public static bool TryCapture(State s,out RebirthStationTerminalSnapshot t){t=new RebirthStationTerminalSnapshot{output=s.Output};return true;}public bool Matches(State s)=>s.Output==output; }
public static class RebirthStationCompletionReceipt { public static bool Clear=true;public static bool HasNoJobReceipt(object rows,string j)=>Clear; }
public class RebirthStationCancellationAttempt { public int Value=1;public static Action OnCreate;public RebirthStationCancellationAttempt Clone()=>new RebirthStationCancellationAttempt{Value=Value};public static bool TryCreate(Admission a,RebirthStationTerminalIntent i,RebirthStationCancellationRefund r,RebirthStationNativeQueueSnapshot b,RebirthStationNativeQueueSnapshot after,RebirthStationTerminalSnapshot t,out RebirthStationCancellationAttempt attempt){attempt=new RebirthStationCancellationAttempt();OnCreate?.Invoke();return true;} }
public class RebirthCamperModuleRuntime { public class Installed { public ItemStack InstalledItem=new ItemStack(); }public class Manifest { public Installed Find(string i)=>new Installed(); }public Manifest Stations=new Manifest();public static RebirthCamperModuleRuntime For(object vehicle)=>new RebirthCamperModuleRuntime(); }
public static class RebirthCamperStationRecoveryImage { public static string Digest(State s,ItemStack i)=>s.Content+":"+s.Output+":"+s.Queue[0].Value; }
public static class CancellationCheckpointFixture {
 private static void Check(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(){var b=new RebirthCamperStationQueuedAuthority.Binding();RebirthStationLiveAccess.Current=b;
  Check(RebirthCamperStationCancellationCheckpoint.TryCreate(b,out var proposed)&&proposed.MatchesBefore()&&b.Station.Queue[0].Value==1,"proposal only, unchanged queue");
  var copy=proposed.CopyRefund();copy.Amount=99;Check(proposed.CopyRefund().Amount==4,"detached refund copies");var choice=proposed.CopyIntent();choice.Actor=7;Check(proposed.CopyIntent().Actor==99,"current requester choice immutable");
  Check(proposed.MatchesPlannedQueue(new[]{new RecipeQueueItem{Value=0}})&&!proposed.MatchesPlannedQueue(b.Station.Queue),"after queue distinct");
  b.Station.Content++;Check(!proposed.MatchesBefore(),"input/fuel/tools content change invalidates proposal");b.Station.Content--;b.Station.Output++;Check(!proposed.MatchesBefore(),"output change invalidates");b.Station.Output--;
  RebirthWorldCharacterRepository.Saved=false;Check(!proposed.MatchesBefore()&&!RebirthCamperStationCancellationCheckpoint.TryCreate(b,out _),"missing primary paid publication denied");RebirthWorldCharacterRepository.Saved=true;
  RebirthCamperStationWindowSession.Lease=new object();Check(!proposed.MatchesBefore(),"reopened window cannot reuse original proposal");
  RebirthCamperStationSaveEvidence.Pending=true;Check(!RebirthCamperStationCancellationCheckpoint.TryCreate(b,out _),"pending native save denied");RebirthCamperStationSaveEvidence.Pending=false;
  RebirthStationCancellationAttempt.OnCreate=()=>b.Valid=false;Check(!RebirthCamperStationCancellationCheckpoint.TryCreate(b,out _),"authority rechecked after adapters");b.Valid=true;RebirthStationCancellationAttempt.OnCreate=null;
  RebirthStationCompletionReceipt.Clear=false;Check(!RebirthCamperStationCancellationCheckpoint.TryCreate(b,out _),"completion receipt blocks cancellation");RebirthStationCompletionReceipt.Clear=true;
  Console.WriteLine("PASS actual cancellation checkpoint: immutable copies/no mutation, before/after correlation, full station changes, primary publication, pending save, late authority change and completed receipt. Native/codecs/access adapters doubled; no durable cancellation or gameplay proof.");
 }
}
'@
$camperFixture=$camperFixture.Replace('public bool IsCurrent()=>Valid;','public bool IsCurrent()=>Valid;public bool IsOwnerCurrent()=>Valid;')
$camperFixture=$camperFixture.Replace('public int Actor;','public int Actor;public XElement Write()=>new XElement("choice",Actor);')
$camperFixture=$camperFixture.Replace('public int Amount=4;','public int Amount=4;public XElement Write()=>new XElement("refund",Amount);')
$camperFixture=$camperFixture.Replace('public int Value=1;public static Action OnCreate;','public int Value=1;public XElement Write()=>new XElement("attempt",Value);public static Action OnCreate;')
$camperFixture=[regex]::Replace($camperFixture,'\bState\b','RebirthCamperStationState')
$camperFixture+=@'
public static class RebirthCamperStationSnapshot {
 public static RebirthCamperStationState Capture(string installation,RebirthCamperStationState s)=>s;
 public static RebirthCamperStationState Restore(string installation,ItemStack item,RebirthCamperStationState s)=>new RebirthCamperStationState{Queue=s.Queue,Content=s.Content,Output=s.Output};
}
public static class RebirthCamperStationCounterRefund {public static bool TryBuild(RebirthCamperStationState state,RebirthStationCancellationRefund refund,out ItemStack[] input){input=state.Input;return true;}}
'@
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperSource)
[CancellationCheckpointFixture]::Run()
