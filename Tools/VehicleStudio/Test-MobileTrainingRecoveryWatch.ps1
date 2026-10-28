#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperWatch=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperStationTrainingRecoveryWatch.cs'))
$camperWatch=[regex]::Replace($camperWatch,'(?m)^using [^;]+;\r?\n','')
$camperFixture=@'
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
// Window/owner/recovery operations are doubles; polling and cleanup are actual.
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
namespace UnityEngine{static class Time{public static float realtimeSinceStartup;}}
class EntityPlayer{public FixtureWorld world=new FixtureWorld();}
class EntityPlayerLocal:EntityPlayer{}
class FixtureWorld{public bool HasPrimary=true;public EntityPlayer GetPrimaryPlayer()=>HasPrimary?new EntityPlayerLocal():null;public bool IsRemote()=>false;}
class GameManager{public static GameManager Instance=new GameManager();public FixtureWorld World=new FixtureWorld();}
static class Log{public static void Warning(string text){}}
static class ModEvents{
 public struct SGameUpdateData{}
 public delegate void ModEventHandlerDelegate<T>(ref T data);
 public static FixtureUpdate GameUpdate=new FixtureUpdate();
 public class FixtureUpdate{public int Registrations;ModEventHandlerDelegate<SGameUpdateData> handlers;public void RegisterHandler(ModEventHandlerDelegate<SGameUpdateData> h){Registrations++;handlers+=h;}public void Tick(){var d=new SGameUpdateData();handlers?.Invoke(ref d);}}
}
struct RebirthCamperStationAddress{public int Id;}
static class RebirthCamperStationWindowSession{public static object Lease;public static bool TryGetLeaseIdentity(EntityPlayer p,out object identity){identity=p==null?null:Lease;return identity!=null;}public static bool TryGetCurrent(EntityPlayer p,out object station,out RebirthCamperStationAddress address){station=new object();address=new RebirthCamperStationAddress{Id=1};return p!=null;}}
class RebirthStationGridAdmission{public string CreationId="creation";public bool TryGetMobileAddress(out RebirthCamperStationAddress address){address=new RebirthCamperStationAddress{Id=1};return true;}public static bool TryReadStored(XElement node,out RebirthStationGridAdmission admission){admission=new RebirthStationGridAdmission();return true;}}
static class RebirthStationLiveAccess{public static bool TryGetMobileLease(EntityPlayer player,RebirthCamperStationAddress address,string creation,out object station,out object lease){station=new object();lease=RebirthCamperStationWindowSession.Lease;return lease!=null;}}
class FixtureArchive{public bool IsCompletion=true;public string JobId="job";public XElement Write()=>new XElement("archive",new XElement("stationAdmission"));}
class FixtureProgression{public Dictionary<string,object> CamperStationTrainingPlans=new Dictionary<string,object>{{"job",new object()}};public Dictionary<string,RebirthStationGridAdmission> StationPreparations=new Dictionary<string,RebirthStationGridAdmission>{{"job",new RebirthStationGridAdmission()}};public Dictionary<string,FixtureArchive> StationRefundArchives=new Dictionary<string,FixtureArchive>();}
static class RebirthCamperStationSaveEvidence{public static bool IsPending(object station)=>false;}
static class RebirthCamperStationCompletionWatch{public static bool IsWatching(string job)=>false;}
class RebirthCamperStationCompletionArchive:IDisposable{public static int Begins,Calls,Disposals;public static bool TryBegin(EntityPlayer p,string job,out RebirthCamperStationCompletionArchive archive){Begins++;archive=new RebirthCamperStationCompletionArchive();return false;}public bool TryArchive(){Calls++;return true;}public void Dispose(){Disposals++;}}
class FixtureOwner{public FixtureProgression Progression=new FixtureProgression();}
static class RebirthWorldCharacterService{public static bool TryGet(EntityPlayer p,out FixtureOwner owner){owner=new FixtureOwner();return true;}}
class RebirthCamperStationTrainingRecovery:IDisposable{
 public static int Starts;public static RebirthCamperStationTrainingRecovery Last;
 public bool CanContinue=true;public int Failures=2,Calls,Disposals;public bool FailDispose;
 public bool HasRetired=>Calls>Failures;
 public static bool TryBegin(EntityPlayer p,string j,out RebirthCamperStationTrainingRecovery op){Starts++;op=Last=new RebirthCamperStationTrainingRecovery();return true;}
 public bool TryGrant(){Calls++;return Calls>Failures;}
 public void Dispose(){if(FailDispose){FailDispose=false;throw new Exception("fixture disposal failure");}Disposals++;}
}
public static class RecoveryWatchFixture{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Tick(float seconds=.3f){UnityEngine.Time.realtimeSinceStartup+=seconds;ModEvents.GameUpdate.Tick();}
 public static void Run(){
 RebirthCamperStationTrainingRecoveryWatch.Install();RebirthCamperStationTrainingRecoveryWatch.Install();Check(ModEvents.GameUpdate.Registrations==1,"idempotent installation");
 Tick();Check(RebirthCamperStationTrainingRecovery.Starts==0,"no window no recovery");
 RebirthCamperStationWindowSession.Lease=new object();ThreadManager.Main=false;Tick();Check(RebirthCamperStationTrainingRecovery.Starts==0,"thread gate");ThreadManager.Main=true;
 Tick();var first=RebirthCamperStationTrainingRecovery.Last;Tick();Tick();Tick();Tick();Tick();
 Check(first.Calls==3&&first.Disposals==1&&RebirthCamperStationTrainingRecovery.Starts==1,"one original operation across grant retries and no same-lease replay");
 Check(RebirthCamperStationCompletionArchive.Begins==1&&RebirthCamperStationCompletionArchive.Calls==1&&RebirthCamperStationCompletionArchive.Disposals==1,"cold retirement disposes original before one retained archive operation");
 RebirthCamperStationWindowSession.Lease=new object();Tick();var changed=RebirthCamperStationTrainingRecovery.Last;changed.CanContinue=false;Tick();Tick();Check(changed.Disposals==1&&changed.Calls==0&&RebirthCamperStationTrainingRecovery.Starts==2,"invalid operation stops without replay");
 RebirthCamperStationWindowSession.Lease=new object();Tick();var timed=RebirthCamperStationTrainingRecovery.Last;Tick(31);Tick();Check(timed.Disposals==1&&timed.Calls==0&&RebirthCamperStationTrainingRecovery.Starts==3,"timeout not restarted in same lease");
 RebirthCamperStationWindowSession.Lease=new object();Tick();var release=RebirthCamperStationTrainingRecovery.Last;release.FailDispose=true;RebirthCamperStationWindowSession.Lease=null;Tick();Tick();Check(release.Disposals==1,"failed cleanup retains operation for retry");
 var remote=new EntityPlayer();RebirthCamperStationWindowSession.Lease=new object();GameManager.Instance.World.HasPrimary=false;
 Check(RebirthCamperStationTrainingRecoveryWatch.TryObserveRemote(remote,"job"),"authenticated server remote recovery admitted");
 int remoteStarts=RebirthCamperStationTrainingRecovery.Starts;Tick();var remoteRecovery=RebirthCamperStationTrainingRecovery.Last;
 Tick();Tick();Tick();Tick();Check(remoteRecovery.Calls==3&&remoteRecovery.Disposals==1&&RebirthCamperStationTrainingRecovery.Starts==remoteStarts+1,"remote original retained through native retirement and archive handoff");
 Check(!RebirthCamperStationTrainingRecoveryWatch.TryObserveRemote(remote,"job"),"same remote lease does not replay completed/timed-out attempt");
 Console.WriteLine("PASS actual recovery polling: thread/window gates, one operation through save retries, invalidation, timeout/no replay and retained cleanup retry. Owner/native recovery doubles; no gameplay proof.");}}
'@
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperWatch)
[RecoveryWatchFixture]::Run()
