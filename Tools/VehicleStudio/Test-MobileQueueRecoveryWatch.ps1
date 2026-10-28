#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperWatch=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationQueueRecoveryWatch.cs'))
$camperWatch=[regex]::Replace($camperWatch,'(?m)^using [^;]+;\r?\n','')
$camperFixture=@'
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
// Window/owner/recovery operations are doubles; polling and cleanup are actual.
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
namespace UnityEngine{static class Time{public static float realtimeSinceStartup;}}
class EntityPlayer{public FixtureWorld world=new FixtureWorld();}
class EntityPlayerLocal:EntityPlayer{}
class RebirthCamperStationAddress{}
class RebirthStationGridAdmission{public string CreationId="creation";public bool TryGetMobileAddress(out RebirthCamperStationAddress address){address=new RebirthCamperStationAddress();return true;}}
static class RebirthStationLiveAccess{public static object Lease=new object();public static bool TryGetMobileLease(EntityPlayer p,RebirthCamperStationAddress a,string c,out object station,out object lease){station=new object();lease=Lease;return Lease!=null;}}
class FixtureWorld{public bool IsRemote()=>false;public EntityPlayer GetPrimaryPlayer()=>new EntityPlayerLocal();}
class GameManager{public static GameManager Instance=new GameManager();public FixtureWorld World=new FixtureWorld();}
static class Log{public static void Warning(string text){}}
static class ModEvents{
 public struct SGameUpdateData{}
 public delegate void ModEventHandlerDelegate<T>(ref T data);
 public static FixtureUpdate GameUpdate=new FixtureUpdate();
 public class FixtureUpdate{public int Registrations;ModEventHandlerDelegate<SGameUpdateData> handlers;public void RegisterHandler(ModEventHandlerDelegate<SGameUpdateData> h){Registrations++;handlers+=h;}public void Tick(){var d=new SGameUpdateData();handlers?.Invoke(ref d);}}
}
static class RebirthCamperStationWindowSession{public static object Lease;public static bool TryGetLeaseIdentity(EntityPlayer p,out object identity){identity=Lease;return Lease!=null;}public static bool TryGetCurrent(EntityPlayer p,out object station,out object address){station=new object();address=new object();return Lease!=null;}}
static class RebirthCamperStationSaveEvidence{public static bool Pending;public static bool IsPending(object s)=>Pending;}
static class RebirthCamperStationCompletionCallsite{public static bool IsHeld(object s)=>false;}
class FixtureProgression{public Dictionary<string,RebirthStationGridAdmission> StationPreparations=new Dictionary<string,RebirthStationGridAdmission>{{"remote",new RebirthStationGridAdmission()}};public Dictionary<string,object> CamperStationPublications=new Dictionary<string,object>{{"job",new object()},{"remote",new object()}};}
class FixtureOwner{public FixtureProgression Progression=new FixtureProgression();}
static class RebirthWorldCharacterService{public static bool TryGet(EntityPlayer p,out FixtureOwner owner){owner=new FixtureOwner();if(p is EntityPlayerLocal)owner.Progression.CamperStationPublications.Remove("remote");return true;}}
class RebirthCamperStationQueueRecovery:IDisposable{
 public static int Starts;public static RebirthCamperStationQueueRecovery Last;
 public bool CanContinue=true;public int Failures=2,Calls,Disposals;public bool FailDispose;
 public static bool TryGetRecoveryLease(EntityPlayer p,RebirthStationGridAdmission a,out object station,out RebirthCamperStationAddress address,out object lease){a.TryGetMobileAddress(out address);return RebirthStationLiveAccess.TryGetMobileLease(p,address,a.CreationId,out station,out lease);}
 public static bool TryBegin(EntityPlayer p,string j,out RebirthCamperStationQueueRecovery op){Starts++;op=Last=new RebirthCamperStationQueueRecovery();return true;}
 public bool TryRecover(){Calls++;return Calls>Failures;}
 public void Dispose(){if(FailDispose){FailDispose=false;throw new Exception("fixture disposal failure");}Disposals++;}
}
public static class RecoveryWatchFixture{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Tick(float seconds=.3f){UnityEngine.Time.realtimeSinceStartup+=seconds;ModEvents.GameUpdate.Tick();}
 public static void Run(){
 RebirthCamperStationQueueRecoveryWatch.Install();RebirthCamperStationQueueRecoveryWatch.Install();Check(ModEvents.GameUpdate.Registrations==1,"idempotent installation");
 Tick();Check(RebirthCamperStationQueueRecovery.Starts==0,"no window no recovery");
 RebirthCamperStationWindowSession.Lease=new object();ThreadManager.Main=false;Tick();Check(RebirthCamperStationQueueRecovery.Starts==0,"thread gate");ThreadManager.Main=true;
 RebirthCamperStationSaveEvidence.Pending=true;Tick();Check(RebirthCamperStationQueueRecovery.Starts==0,"pending save does not consume job attempt");RebirthCamperStationSaveEvidence.Pending=false;
 Tick();var first=RebirthCamperStationQueueRecovery.Last;Tick();Tick();Tick();Tick();
 Check(first.Calls==3&&first.Disposals==1&&RebirthCamperStationQueueRecovery.Starts==1,"one original operation across grant retries and no same-lease replay");
 RebirthCamperStationWindowSession.Lease=new object();Tick();var changed=RebirthCamperStationQueueRecovery.Last;changed.CanContinue=false;Tick();Tick();Check(changed.Disposals==1&&changed.Calls==0&&RebirthCamperStationQueueRecovery.Starts==2,"invalid operation stops without replay");
 RebirthCamperStationWindowSession.Lease=new object();Tick();var timed=RebirthCamperStationQueueRecovery.Last;Tick(31);Tick();Check(timed.Disposals==1&&timed.Calls==0&&RebirthCamperStationQueueRecovery.Starts==3,"timeout not restarted in same lease");
 RebirthCamperStationWindowSession.Lease=new object();Tick();var release=RebirthCamperStationQueueRecovery.Last;release.FailDispose=true;RebirthCamperStationWindowSession.Lease=null;Tick();Tick();Check(release.Disposals==1,"failed cleanup retains operation for retry");
 RebirthCamperStationWindowSession.Lease=null;Tick();
 var remotePlayer=new EntityPlayer();var initial=RebirthCamperStationQueueRecovery.Starts;
 RebirthCamperStationSaveEvidence.Pending=true;Check(!RebirthCamperStationQueueRecoveryWatch.TryObserveRemote(remotePlayer,"remote"),"remote pending waits without attempt");RebirthCamperStationSaveEvidence.Pending=false;
 Check(RebirthCamperStationQueueRecoveryWatch.TryObserveRemote(remotePlayer,"remote"),"remote exact lease retained");var remoteOp=RebirthCamperStationQueueRecovery.Last;remoteOp.FailDispose=true;
 RebirthStationLiveAccess.Lease=new object();Tick();Check(remoteOp.Disposals==0&&!RebirthCamperStationQueueRecoveryWatch.TryObserveRemote(remotePlayer,"remote"),"failed cleanup blocks replacement lease");Tick();
 Check(remoteOp.Disposals==1&&RebirthCamperStationQueueRecoveryWatch.TryObserveRemote(remotePlayer,"remote"),"new lease can recover only after cleanup");var nextRemote=RebirthCamperStationQueueRecovery.Last;Tick(31);Tick();
 Check(nextRemote.Disposals==1&&!RebirthCamperStationQueueRecoveryWatch.TryObserveRemote(remotePlayer,"remote"),"remote timeout no same lease replay");
 Console.WriteLine("PASS actual recovery polling: thread/window gates, one operation through save retries, invalidation, timeout/no replay and retained cleanup retry. Owner/native recovery doubles; no gameplay proof.");}}
'@
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperWatch)
[RecoveryWatchFixture]::Run()
