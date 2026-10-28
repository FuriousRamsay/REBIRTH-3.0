#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationOpenQueueProcessing.cs'))
$camperSource=[regex]::Replace($camperSource,'(?m)^using [^;]+;\r?\n','')
$camperFixture=@'
using System;using System.Reflection;using System.Runtime.CompilerServices;
public class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string n){} }
public static class AccessTools { public static MethodInfo Method(Type t,string n)=>t.GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic); }
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public static class RebirthCamperStationSaveEvidence {public static bool IsPending(RebirthCamperStationState s)=>false;}
public static class RebirthCamperStationCompletionCallsite {public static bool IsHeld(RebirthCamperStationState s)=>false;}
public static class RebirthCamperStationGridCustody {public static bool Held;public static bool IsHeld(RebirthCamperStationState s)=>Held;}
public static class RebirthCamperStationQueuedAuthority {public static bool Current=true;public static bool CanProcessNativeQueue(RebirthCamperStationState s)=>Current;}
public static class RebirthCamperRemoteStationTransport {public static bool ServerLease;public static bool HasServerLease(RebirthCamperStationState s)=>ServerLease;}
public class World {public bool Remote;public bool IsRemote()=>Remote;}
public class EntityVehicle {public World world;}
public class RebirthCamperStationState {public EntityVehicle BoundVehicle; public ulong lastTickTime=10;public bool bUserAccessing=true;public int Output,Input; }
public class XUiM_Workstation { public RebirthCamperStationState TileEntity; }
public class XUiC_WorkstationWindowGroup { public XUiM_Workstation WorkstationData;public int Input=7,Output,Commits;public bool Fail;
 public void OnOpen(){} private void syncTEfromUI(){Commits++;var s=WorkstationData.TileEntity;s.Input=Input;s.Output=Output;s.lastTickTime=0;if(Fail)throw new Exception("commit");} }
public static class RebirthCamperStationWindowSession { public static bool Current=true,InUse=true;public static bool Admitted(XUiC_WorkstationWindowGroup w,RebirthCamperStationState s)=>Current&&ReferenceEquals(w.WorkstationData.TileEntity,s);public static bool IsInUse(RebirthCamperStationState s)=>InUse; }
public static class RebirthCamperStationQueueProjection { public static bool Fail;public static int Refreshes;public static void Refresh(XUiC_WorkstationWindowGroup w,RebirthCamperStationState s){Refreshes++;if(Fail)throw new InvalidOperationException("projection");w.Output=s.Output;w.Input=s.Input;} }
public static class OpenQueueProcessingFixture {
 static void Check(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(){var s=new RebirthCamperStationState();var w=new XUiC_WorkstationWindowGroup{WorkstationData=new XUiM_Workstation{TileEntity=s}};
  try{RebirthCamperStationOpenQueueProcessing.Begin(s);throw new Exception("unregistered admitted");}catch(InvalidOperationException){}
  RebirthCamperRemoteStationTransport.ServerLease=true;s.bUserAccessing=false;
  Check(RebirthCamperStationOpenQueueProcessing.Begin(s)==null&&w.Commits==0&&!s.bUserAccessing,"remote server lease required a nonexistent UI or stopped native processing");
  RebirthCamperRemoteStationTransport.ServerLease=false;s.bUserAccessing=true;
  s.BoundVehicle=new EntityVehicle{world=new World{Remote=true}};
  var syncArgs=new object[]{w,null};
  Check(!(bool)typeof(RebirthCamperStationOpenQueueSync).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,syncArgs),"Remote window adopted stale full UI inventories");
  s.BoundVehicle=null;
  RebirthCamperStationGridCustody.Held=true;
  Check(!(bool)typeof(RebirthCamperStationOpenQueueSync).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,null}),"Save custody accepted native UI writeback");
  RebirthCamperStationGridCustody.Held=false;
  RebirthCamperStationOpenQueueProcessing.Register(w);Check(RebirthCamperStationOpenQueueProcessing.OwnsQueue(s),"owns exact live window");w.Input=9;
  var scope=RebirthCamperStationOpenQueueProcessing.Begin(s);Check(w.Commits==1&&s.Input==9&&s.lastTickTime==10&&!s.bUserAccessing,"commit edits before shared clock without timer reset");s.Output=3;s.lastTickTime=15;scope.Dispose();scope.Dispose();Check(s.bUserAccessing&&w.Output==3&&s.lastTickTime==15&&RebirthCamperStationQueueProjection.Refreshes==2,"restore access/project once without changing vehicle clock");
  w.Fail=true;try{RebirthCamperStationOpenQueueProcessing.Begin(s);throw new Exception("commit failure accepted");}catch(TargetInvocationException){}Check(s.bUserAccessing&&s.lastTickTime==15,"failed commit restores clock and never releases access flag");w.Fail=false;
  try{using(RebirthCamperStationOpenQueueProcessing.Begin(s)){s.Output=4;throw new ApplicationException();}}catch(ApplicationException){}Check(s.bUserAccessing&&w.Output==4,"partial native completion projected on processing exception");
  RebirthCamperStationQueuedAuthority.Current=false;try{RebirthCamperStationOpenQueueProcessing.Begin(s);throw new Exception("changed authority accepted");}catch(InvalidOperationException){}Check(s.lastTickTime==0&&s.bUserAccessing,"post-commit authority denial resets clock without processing");RebirthCamperStationQueuedAuthority.Current=true;
  RebirthCamperStationQueueProjection.Fail=true;try{using(RebirthCamperStationOpenQueueProcessing.Begin(s)){s.Output=5;}}catch(InvalidOperationException){}Check(!RebirthCamperStationOpenQueueProcessing.CanCommit(s)&&s.bUserAccessing,"failed output projection blocks stale grid writeback");try{RebirthCamperStationOpenQueueProcessing.Begin(s);throw new Exception("failed presentation reused");}catch(InvalidOperationException){}
  RebirthCamperStationWindowSession.Current=false;try{RebirthCamperStationOpenQueueProcessing.Begin(s);throw new Exception("stale session admitted");}catch(InvalidOperationException){}RebirthCamperStationWindowSession.Current=true;
  RebirthCamperStationWindowSession.InUse=false;Check(RebirthCamperStationOpenQueueProcessing.Begin(new RebirthCamperStationState())==null&&!RebirthCamperStationOpenQueueProcessing.OwnsQueue(s),"closed window keeps native vehicle path");
  ThreadManager.Main=false;try{RebirthCamperStationOpenQueueProcessing.Begin(s);throw new Exception("off-thread accepted");}catch(InvalidOperationException){}
  Console.WriteLine("PASS actual open-window processor: exact lease, pre-tick grid commit, shared timer, scoped native access flag, exception-safe output projection, stale/closed/thread refusal. Native grid/session adapters doubled; gameplay remains unverified.");
 }
}
'@
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperSource)
[OpenQueueProcessingFixture]::Run()
