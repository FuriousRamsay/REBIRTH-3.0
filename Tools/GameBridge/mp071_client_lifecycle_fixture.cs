// Complete actual client poll/close/switch methods; native scope, package send,
// pointer and release callbacks are explicit adapters. AUTHORED_NOT_RUN.
using System;
using System.Collections.Generic;
class EntityPlayerLocal {internal int entityId=1;}
class EntityVehicle {internal int entityId=2;}
class World {internal bool IsRemote()=>true;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World=new World();}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class UnityEngine {internal static class Time {internal static float realtimeSinceStartup;}}
static class Log {internal static void Warning(string text){}}
static class ModEvents {internal struct SGameUpdateData {}}
static class RebirthCamperRemoteStationTransport {internal const byte StationCommand=2,FuelSwitch=4,CloseSession=3;}
class RebirthCamperRemoteStationCommand {}
static class RebirthCamperRemoteStationCommandStatus {internal static bool HasNativeOriginal(RebirthCamperRemoteStationCommand c)=>c!=null;}
class RebirthCamperStationState {internal Guid InstallationId=Guid.NewGuid();}
class XUiC_WorkstationWindowGroup {}
static class RebirthCamperStationNativeActorPayment {internal static int Suspended;internal static void Suspend(RebirthCamperStationState s){Suspended++;}}
struct RebirthCamperStationAddress {internal RebirthCamperStationAddress(Guid v,Guid i){}}
static class RebirthCamperRemoteCommandOutbox {internal static bool NeverAttempted;internal static Action DuringResolve;internal static bool DeclaresAdmissionProtocol(EntityPlayerLocal p,RebirthCamperStationAddress a,RebirthCamperRemoteStationCommand c)=>false;internal static bool TryResolveNeverAttempted(EntityPlayerLocal p,RebirthCamperStationAddress a,RebirthCamperRemoteStationCommand c){DuringResolve?.Invoke();return NeverAttempted;}}
class Storage {internal Guid VehicleIdentity=Guid.NewGuid();}
class RebirthCamperTransferSnapshot {internal Storage Storage=new Storage();}
static class RebirthCamperStationPointerTransition
{internal static int Cancels;internal static bool Throw;internal static Action DuringCancel;internal static void Cancel(EntityPlayerLocal p,EntityVehicle v){Cancels++;DuringCancel?.Invoke();if(Throw)throw new InvalidOperationException("pointer");}}
class ConnectionManager {internal int Queries;internal void SendToServer(object packet,bool reliable){Queries++;}}
static class SingletonMonoBehaviour<T> where T:new() {internal static T Instance=new T();}
static class NetPackageManager {internal static T GetPackage<T>() where T:new()=>new T();}
class NetPackageRebirthCamperStationCommandStatusQuery {internal object Setup(int p,int v,Guid token,Guid vehicle,Guid installation,RebirthCamperRemoteStationCommand c,bool declaration=false)=>this;}
static class Fixture
{
    private sealed class Session
    {
        internal EntityPlayerLocal Player=new EntityPlayerLocal();internal EntityVehicle Vehicle=new EntityVehicle();internal bool Live=true,Closed,CommandPending,CommandUncertain,GridPending,SwitchClosePending;
        internal Guid Token=Guid.NewGuid(),Installation=Guid.NewGuid();internal RebirthCamperTransferSnapshot Snapshot=new RebirthCamperTransferSnapshot();
        internal byte PendingAction=2;internal RebirthCamperRemoteStationCommand PendingCommand;internal float NextCommandStatus,NextSwitchClose;
        internal Action NativeRelease,GridClose,SwitchClosed;
    }
    private static readonly Dictionary<int,Session> sessions=new Dictionary<int,Session>();
    private static bool IsReady=true,sendThrows;private static int sends,releases,gridCloses;
    private static bool Current(Session s)=>s!=null&&s.Live&&!s.Closed;
    private static Session Find(EntityPlayerLocal p,RebirthCamperStationState state)=>p!=null&&sessions.TryGetValue(p.entityId,out var s)&&Current(s)?s:null;
    private static void Send(Session s,byte action){sends++;if(sendThrows)throw new InvalidOperationException("ambiguous close send");}
    // ACTUAL_POLL
    // ACTUAL_CLOSE
    // ACTUAL_SWITCH
    // ACTUAL_RETRY
    // ACTUAL_FINISH_SWITCH
    private static void Need(bool b,string why){if(!b)throw new Exception(why);}
    private static Session Reset()
    {
        sessions.Clear();sends=releases=gridCloses=RebirthCamperStationPointerTransition.Cancels=0;sendThrows=RebirthCamperStationPointerTransition.Throw=false;RebirthCamperStationPointerTransition.DuringCancel=null;
        GameManager.Instance.World=new World();UnityEngine.Time.realtimeSinceStartup=10;SingletonMonoBehaviour<ConnectionManager>.Instance.Queries=0;
        RebirthCamperRemoteCommandOutbox.NeverAttempted=false;RebirthCamperRemoteCommandOutbox.DuringResolve=null;
        var s=new Session{NativeRelease=()=>releases++,GridClose=()=>gridCloses++};sessions.Add(1,s);return s;
    }
    private static void Poll(){var data=new ModEvents.SGameUpdateData();PollOriginalCommands(ref data);}
    public static void Main()
    {
        var s=Reset();s.Live=false;Poll();Need(s.Closed&&sessions.Count==0&&releases==1&&gridCloses==1&&sends==0,"expired idle session blocks reopen");Poll();Need(releases==1,"expired native release repeated");
        s=Reset();s.CommandPending=true;s.PendingCommand=new RebirthCamperRemoteStationCommand();s.Live=false;GameManager.Instance.World=null;Poll();Need(s.Closed&&sessions.Count==0&&s.PendingCommand!=null&&s.CommandPending,"world end drops original or retains session");
        s=Reset();s.GridClose=()=>{gridCloses++;throw new InvalidOperationException("grid callback");};Close(s);Need(s.Closed&&sessions.Count==0&&releases==1,"grid exception prevented release");
        s=Reset();RebirthCamperStationPointerTransition.Throw=true;bool threw=false;try{Close(s);}catch(InvalidOperationException){threw=true;}Need(threw&&s.Closed&&sessions.Count==0&&releases==1,"pointer exception prevented session retirement");
        s=Reset();s.Live=false;RebirthCamperStationPointerTransition.Throw=true;Poll();Need(s.Closed&&sessions.Count==0&&releases==1,"poll did not contain cleanup exception");
        s=Reset();sendThrows=true;Close(s);Need(s.Closed&&sessions.Count==0&&releases==1&&sends==1,"ambiguous close send prevents local retirement");
        s=Reset();var replacement=new Session();s.GridClose=()=>sessions[1]=replacement;Close(s);Need(ReferenceEquals(sessions[1],replacement),"old close erased replacement session");
        s=Reset();s.NativeRelease=()=>{releases++;throw new InvalidOperationException("native release");};s.Live=false;Poll();Need(sessions.Count==0&&s.Closed&&releases==1,"native release exception prevents retirement");
        s=Reset();s.CommandPending=true;s.PendingCommand=new RebirthCamperRemoteStationCommand();Poll();Poll();Need(SingletonMonoBehaviour<ConnectionManager>.Instance.Queries==1&&sends==0,"status pump mutates or bypasses throttle");UnityEngine.Time.realtimeSinceStartup+=2;Poll();Need(SingletonMonoBehaviour<ConnectionManager>.Instance.Queries==2,"same original query not retried");
        s=Reset();sendThrows=true;int completed=0;CloseForSwitch(s.Player,new RebirthCamperStationState(),()=>completed++);Need(!s.Closed&&sessions.Count==1&&releases==0&&completed==0&&s.SwitchClosed!=null&&sends==1,"failed switch close released cursor or lost continuation");
        RetrySwitchClose(s.Player,new RebirthCamperStationState());Need(sends==1,"switch retry ignored pacing");UnityEngine.Time.realtimeSinceStartup+=2;sendThrows=false;RetrySwitchClose(s.Player,new RebirthCamperStationState());Need(sends==2&&s.SwitchClosePending&&releases==0&&completed==0,"switch retry did not retain original lease until reply");
        s=Reset();s.CommandPending=true;threw=false;try{CloseForSwitch(s.Player,new RebirthCamperStationState(),()=>{});}catch(InvalidOperationException){threw=true;}Need(threw&&sends==0&&releases==0,"unsettled command permitted switch");
        s=Reset();s.CommandPending=true;s.CommandUncertain=true;s.PendingCommand=new RebirthCamperRemoteStationCommand();RebirthCamperRemoteCommandOutbox.NeverAttempted=true;Poll();Need(!s.CommandPending&&!s.CommandUncertain&&s.PendingCommand==null&&SingletonMonoBehaviour<ConnectionManager>.Instance.Queries==0&&sends==0&&releases==0,"saved local never-attempted proof failed to retire UI without transport/effect/release");
        s=Reset();s.CommandPending=true;s.PendingCommand=new RebirthCamperRemoteStationCommand();RebirthCamperRemoteCommandOutbox.NeverAttempted=true;RebirthCamperRemoteCommandOutbox.DuringResolve=()=>s.Live=false;Poll();Need(s.CommandPending&&s.PendingCommand!=null&&SingletonMonoBehaviour<ConnectionManager>.Instance.Queries==0,"local proof crossed expired presentation scope");
        s=Reset();int finished=0;s.SwitchClosed=()=>finished++;s.NativeRelease=()=>{releases++;throw new InvalidOperationException("native release after close");};threw=false;try{FinishSwitchClose(s);}catch(InvalidOperationException){threw=true;}Need(threw&&s.Closed&&sessions.Count==0&&finished==1&&s.SwitchClosed==null,"accepted switch close lost workstation cleanup after native release failure");FinishSwitchClose(s);Need(finished==1&&releases==1,"accepted switch close cleanup replayed");
        s=Reset();finished=0;s.SwitchClosed=()=>finished++;replacement=new Session();sessions[1]=replacement;FinishSwitchClose(s);Need(ReferenceEquals(sessions[1],replacement)&&finished==1,"old accepted close erased replacement client session");
        WindowReleaseFixture.Run();
        Console.WriteLine("MP071 actual client lifecycle scenarios: PASS (native callbacks and transport are adapters)");
    }
}
static class WindowReleaseFixture
{
    private sealed class Session {internal EntityVehicle Vehicle;internal EntityPlayerLocal Player;internal RebirthCamperStationState State;internal bool SwitchRequested;}
    private static Dictionary<XUiC_WorkstationWindowGroup,Session> sessions=new Dictionary<XUiC_WorkstationWindowGroup,Session>();
    private static Dictionary<EntityVehicle,Dictionary<Guid,XUiC_WorkstationWindowGroup>> leases=new Dictionary<EntityVehicle,Dictionary<Guid,XUiC_WorkstationWindowGroup>>();
    private static Dictionary<EntityPlayerLocal,XUiC_WorkstationWindowGroup> playerWindows=new Dictionary<EntityPlayerLocal,XUiC_WorkstationWindowGroup>();
    // ACTUAL_WINDOW_RELEASE
    private static void Need(bool b,string why){if(!b)throw new Exception(why);}
    private static XUiC_WorkstationWindowGroup Bind(out Session s)
    {
        sessions.Clear();leases.Clear();playerWindows.Clear();RebirthCamperStationNativeActorPayment.Suspended=0;RebirthCamperStationPointerTransition.Throw=false;RebirthCamperStationPointerTransition.DuringCancel=null;
        s=new Session{Vehicle=new EntityVehicle(),Player=new EntityPlayerLocal(),State=new RebirthCamperStationState()};var w=new XUiC_WorkstationWindowGroup();sessions[w]=s;leases[s.Vehicle]=new Dictionary<Guid,XUiC_WorkstationWindowGroup>{{s.State.InstallationId,w}};playerWindows[s.Player]=w;return w;
    }
    internal static void Run()
    {
        var w=Bind(out var s);Release(w);Release(w);Need(sessions.Count==0&&playerWindows.Count==0&&leases[s.Vehicle].Count==0&&RebirthCamperStationNativeActorPayment.Suspended==1,"normal workstation release incomplete or repeated");
        w=Bind(out s);RebirthCamperStationPointerTransition.Throw=true;bool threw=false;try{Release(w);}catch(InvalidOperationException){threw=true;}Need(threw&&sessions.Count==0&&playerWindows.Count==0&&leases[s.Vehicle].Count==0&&RebirthCamperStationNativeActorPayment.Suspended==1,"pointer failure stranded workstation association");
        w=Bind(out s);s.SwitchRequested=true;RebirthCamperStationPointerTransition.Throw=true;Release(w);Need(sessions.Count==0,"switch release unexpectedly cancelled pointer transition");
        w=Bind(out s);var replacement=new XUiC_WorkstationWindowGroup();playerWindows[s.Player]=replacement;threw=false;try{Release(w);}catch(InvalidOperationException){threw=true;}Need(threw&&sessions.ContainsKey(w)&&ReferenceEquals(playerWindows[s.Player],replacement)&&RebirthCamperStationNativeActorPayment.Suspended==0,"foreign player association was released");
        w=Bind(out s);replacement=new XUiC_WorkstationWindowGroup();var replacementSession=new Session{Vehicle=s.Vehicle,Player=s.Player,State=s.State};RebirthCamperStationPointerTransition.DuringCancel=()=>{leases[s.Vehicle][s.State.InstallationId]=replacement;playerWindows[s.Player]=replacement;sessions[replacement]=replacementSession;};Release(w);Need(!sessions.ContainsKey(w)&&sessions.ContainsKey(replacement)&&ReferenceEquals(playerWindows[s.Player],replacement)&&ReferenceEquals(leases[s.Vehicle][s.State.InstallationId],replacement)&&RebirthCamperStationNativeActorPayment.Suspended==0,"old workstation cleanup erased or suspended reentrant replacement");
    }
}
