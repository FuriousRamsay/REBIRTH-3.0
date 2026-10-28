#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperSource=[IO.File]::ReadAllText((Join-Path $camperRoot 'Scripts/Vehicles/Camper/RebirthCamperStationSaveEvidence.cs'))
$camperStart=$camperSource.IndexOf('    public sealed class Request : IDisposable')
$camperEnd=$camperSource.IndexOf('    public sealed class Publication',$camperStart)
if($camperStart -lt 0 -or $camperEnd -le $camperStart){throw 'Production request source not found.'}
$camperRequest=$camperSource.Substring($camperStart,$camperEnd-$camperStart)
$camperFixture=@'
using System;
using System.Runtime.CompilerServices;
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public class EntityVehicle {}
public class ItemStack {}
public struct RebirthCamperStationAddress {}
public static class RebirthCamperSavedVehicle
{
 public class Expected {} public static int Verifications;
 public static bool Verify(EntityVehicle v,Expected e,Func<bool> current){Verifications++;return current();}
}
public class RebirthCamperStationState
{
 public float lastTickTime=99;public int Failures,Notifications;
 public void setModified(){Notifications++;if(Failures-->0)throw new InvalidOperationException("native dirty failure");}
}
public static class ReleaseFixture
{
 private static readonly ConditionalWeakTable<RebirthCamperStationState,Request> pending=new ConditionalWeakTable<RebirthCamperStationState,Request>();
 public class Publication { public static int Created;public Publication(Request request){Created++;} }
REQUEST_BODY
OBSERVE_BODY
 private static bool Current(Request request)=>!request.Closed&&request.CurrentSession();
 private static void Check(bool value,string name){if(!value)throw new Exception(name);}
 public static void Run()
 {
  var state=new RebirthCamperStationState{Failures=1};var request=new Request{State=state};pending.Add(state,request);
  ThreadManager.Main=false;
  try{request.Dispose();throw new Exception("wrong thread accepted");}catch(InvalidOperationException){}
  Check(!request.Closed&&state.Notifications==0&&pending.TryGetValue(state,out _),"wrong thread preserves request");ThreadManager.Main=true;
  try{request.Dispose();throw new Exception("native failure ignored");}catch(InvalidOperationException){}
  Check(!request.Closed&&pending.TryGetValue(state,out var retained)&&ReferenceEquals(retained,request),"failure retains exact pending request");
  request.Dispose();Check(request.Closed&&!pending.TryGetValue(state,out _)&&state.lastTickTime==0&&state.Notifications==2,"successful retry closes after notification");
  request.Dispose();Check(state.Notifications==2,"closed release is idempotent");
  var newer=new Request{State=state};pending.Add(state,newer);var stale=new Request{State=state};stale.Dispose();
  Check(stale.Closed&&pending.TryGetValue(state,out retained)&&ReferenceEquals(retained,newer)&&state.Notifications==2,"stale cleanup cannot release newer request");
  state=new RebirthCamperStationState{Failures=1};request=new Request{State=state,CurrentSession=()=>true};pending.Add(state,request);
  try{TryObserve(request,out _);throw new Exception("cleanup failure ignored");}catch(InvalidOperationException){}
  Check(request.Issued&&request.ConfirmedPublication!=null&&!request.Closed&&RebirthCamperSavedVehicle.Verifications==1,"verified result retained through cleanup failure");
  Check(TryObserve(request,out var proof)&&proof!=null&&request.Closed&&Publication.Created==1&&RebirthCamperSavedVehicle.Verifications==1,"retry delivers original proof without another verification or publication");
  Check(!TryObserve(request,out _),"publication delivered only once");
  bool current=true;state=new RebirthCamperStationState{Failures=1};request=new Request{State=state,CurrentSession=()=>current};pending.Add(state,request);
  try{TryObserve(request,out _);}catch(InvalidOperationException){}current=false;
  Check(!TryObserve(request,out _)&&request.ConfirmedPublication==null&&request.Closed,"changed session cannot claim retained result");
  Console.WriteLine("PASS actual save-request cleanup: thread refusal, native notification failure/retry, idempotence and newer pending owner protection. Native station doubled; no gameplay proof.");
 }
}
'@
$camperObserveStart=$camperSource.IndexOf('    public static bool TryObserve(')
$camperObserveEnd=$camperSource.IndexOf('    internal static bool IsPending(',$camperObserveStart)
if($camperObserveStart -lt 0 -or $camperObserveEnd -le $camperObserveStart){throw 'Production observation source missing.'}
Add-Type -TypeDefinition $camperFixture.Replace('REQUEST_BODY',$camperRequest).Replace('OBSERVE_BODY',$camperSource.Substring($camperObserveStart,$camperObserveEnd-$camperObserveStart)) -IgnoreWarnings -WarningAction SilentlyContinue
[ReleaseFixture]::Run()
