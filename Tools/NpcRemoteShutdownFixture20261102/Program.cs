using System;
using System.Collections.Generic;
using System.IO;

enum RebirthNpcLifecycleState { Cold,BootstrapRegistered,WorldActive,WorldStopping,GameStopping }
sealed class World(bool remote) { public bool IsRemote()=>remote; }
sealed class GameManager { public static GameManager Instance=new(); public World World; }
static class GameIO { public static string Directory=Path.GetFullPath("fixture-save"); public static string GetSaveGameDir()=>Directory; }
static class ThreadManager { public static bool Main=true; public static bool IsMainThread()=>Main; }
static class Log { public static void Warning(string text){} }
static class RebirthNpcSaveQuarantineBundle
{
    public static bool AdmissionEnabled=true,Accept=true;
    public static int RetireCalls,AdmitCalls;
    public static World Original,Incoming;public static string Directory;
    public static void RetireEndedClientProjections()=>RetireCalls++;
    public static bool TryAdmitWorld(World original,string directory,World incoming,out string error)
    {AdmitCalls++;Original=original;Incoming=incoming;Directory=directory;error=Accept?"":"fixture custody refusal";return Accept;}
}
static class RebirthNpcPersistenceCoordinator
{
    public static bool LastSaveSucceeded=true;public static int Calls;
    public static Queue<bool> Results=new();
    public static bool SaveAll(out string error){Calls++;error="fixture";return LastSaveSucceeded=Results.Count==0||Results.Dequeue();}
}
static class RebirthNpcPreparedSchedulerAdmission { public static int Calls;public static void RetireSavedWorld(World world,bool saved)=>Calls++; }
static class RebirthNpcLifecycleQualificationService
{public static void RecordTransition(string reason,object snapshot,string error){}public static void AuditWorldReset(string reason){} }

partial class Probe
{
    static readonly object Sync=new();static readonly HashSet<int> ActiveEntityIds=new();
    static World activeWorldReference,completedShutdownWorld;
    static string activeSaveDirectory="",completedShutdownSave="",lastTransition="";
    static bool shutdownInProgress,LiveProjection;static int LegacyResets,HostResets;
    static long gameStopCount,worldStopCount;
    static RebirthNpcLifecycleState state;
    static object GetSnapshot()=>new();
    // Boundary double: the unchanged production dog Reset guard refuses live
    // projections. Full 14-domain transaction semantics are NOT simulated here.
    static void ResetUnexpectedWorldScopedState(bool retirePersistent=true)
    {LegacyResets++;if(LiveProjection)throw new InvalidOperationException("Original dog capacity projection requires typed retirement");}
    static bool ResetWorldScopedState(bool gameShutdown,bool saved,World world,string directory){HostResets++;return saved;}
    static void Reset(bool remote=true)
    {
        var world=new World(remote);GameManager.Instance.World=activeWorldReference=world;
        GameIO.Directory=activeSaveDirectory=Path.GetFullPath("fixture-save");
        completedShutdownWorld=null;completedShutdownSave="";lastTransition="";shutdownInProgress=false;
        ActiveEntityIds.Clear();ActiveEntityIds.Add(171);LiveProjection=true;LegacyResets=HostResets=0;
        gameStopCount=worldStopCount=0;state=RebirthNpcLifecycleState.WorldActive;ThreadManager.Main=true;
        RebirthNpcPersistenceCoordinator.Calls=0;RebirthNpcPersistenceCoordinator.LastSaveSucceeded=true;RebirthNpcPersistenceCoordinator.Results.Clear();
        RebirthNpcPreparedSchedulerAdmission.Calls=0;
        RebirthNpcSaveQuarantineBundle.AdmissionEnabled=RebirthNpcSaveQuarantineBundle.Accept=true;
        RebirthNpcSaveQuarantineBundle.RetireCalls=RebirthNpcSaveQuarantineBundle.AdmitCalls=0;
        RebirthNpcSaveQuarantineBundle.Original=RebirthNpcSaveQuarantineBundle.Incoming=null;
    }
    static int failures,checks;
    static void Check(string name,Action test)
    {checks++;try{test();Console.WriteLine("PASS "+name);}catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+": "+ex.Message);}}
    static void Must(bool value,string reason){if(!value)throw new Exception(reason);}
    static void Main(string[] args)
    {
        Check("remote shutdown retains exact original without legacy reset",()=>{Reset();var old=activeWorldReference;ShutdownWorld(false);Must(ReferenceEquals(old,activeWorldReference)&&ActiveEntityIds.Contains(171)&&LiveProjection,"lost original");Must(LegacyResets==0&&RebirthNpcPersistenceCoordinator.Calls==0,"legacy reset / sidecar save");Must(worldStopCount==1&&state==RebirthNpcLifecycleState.BootstrapRegistered,"shutdown accounting");});
        Check("remote shutdown duplicate event is idempotent",()=>{Reset();ShutdownWorld(false);ShutdownWorld(false);Must(worldStopCount==1,"duplicate stop");});
        Check("remote game shutdown retains original",()=>{Reset();var old=activeWorldReference;ShutdownWorld(true);Must(ReferenceEquals(old,activeWorldReference)&&gameStopCount==1&&state==RebirthNpcLifecycleState.GameStopping,"game stop");});
        Check("remote next-world admission uses retained source",()=>{Reset();var old=activeWorldReference;var path=activeSaveDirectory;var next=new World(true);GameManager.Instance.World=next;QualifyWholeBundleAdmission();Must(RebirthNpcSaveQuarantineBundle.AdmitCalls==1&&ReferenceEquals(RebirthNpcSaveQuarantineBundle.Original,old)&&ReferenceEquals(RebirthNpcSaveQuarantineBundle.Incoming,next)&&RebirthNpcSaveQuarantineBundle.Directory==path,"typed client admission bypassed");});
        Check("remote admission refusal propagates and retains source",()=>{Reset();var old=activeWorldReference;GameManager.Instance.World=new World(true);RebirthNpcSaveQuarantineBundle.Accept=false;bool refused=false;try{QualifyWholeBundleAdmission();}catch(InvalidOperationException){refused=true;}Must(refused&&ReferenceEquals(old,activeWorldReference)&&LegacyResets==0,"custody refusal bypassed");});
        Check("bootstrap without native world skips admission",()=>{Reset();GameManager.Instance.World=null;QualifyWholeBundleAdmission();Must(RebirthNpcSaveQuarantineBundle.RetireCalls==0&&RebirthNpcSaveQuarantineBundle.AdmitCalls==0,"bootstrap observer called");});
        Check("disabled admission remains disabled",()=>{Reset();RebirthNpcSaveQuarantineBundle.AdmissionEnabled=false;QualifyWholeBundleAdmission();Must(RebirthNpcSaveQuarantineBundle.AdmitCalls==0,"activation changed");});
        Check("host admission still uses original bundle",()=>{Reset(false);GameManager.Instance.World=new World(false);QualifyWholeBundleAdmission();Must(RebirthNpcSaveQuarantineBundle.AdmitCalls==1,"host skipped");});
        Check("host successful save clears only after reset",()=>{Reset(false);ShutdownWorld(false);Must(RebirthNpcPersistenceCoordinator.Calls==1&&HostResets==1&&activeWorldReference==null&&ActiveEntityIds.Count==0,"host save changed");});
        Check("host failed save retries once and retains custody",()=>{Reset(false);var old=activeWorldReference;RebirthNpcPersistenceCoordinator.Results.Enqueue(false);RebirthNpcPersistenceCoordinator.Results.Enqueue(false);ShutdownWorld(false);Must(RebirthNpcPersistenceCoordinator.Calls==2&&ReferenceEquals(old,activeWorldReference)&&ActiveEntityIds.Count==1&&state==RebirthNpcLifecycleState.WorldStopping,"failed host custody");});
        Check("host successful retry retires saved world",()=>{Reset(false);RebirthNpcPersistenceCoordinator.Results.Enqueue(false);RebirthNpcPersistenceCoordinator.Results.Enqueue(true);ShutdownWorld(false);Must(RebirthNpcPersistenceCoordinator.Calls==2&&activeWorldReference==null,"retry changed");});
        Check("off-main-thread shutdown leaves original untouched",()=>{Reset();var old=activeWorldReference;ThreadManager.Main=false;ShutdownWorld(false);Must(ReferenceEquals(old,activeWorldReference)&&worldStopCount==0&&LegacyResets==0,"off-thread mutation");});
        Check("shutdown without world retains original",()=>{Reset();var old=activeWorldReference;GameManager.Instance.World=null;ShutdownWorld(false);Must(ReferenceEquals(old,activeWorldReference)&&LegacyResets==0,"null-world loss");});
        Check("shutdown mismatch preserves outgoing source",()=>{Reset();var old=activeWorldReference;GameManager.Instance.World=new World(true);ShutdownWorld(false);Must(ReferenceEquals(old,activeWorldReference)&&LegacyResets==0&&worldStopCount==0,"wrong-world mutation");});
        Console.WriteLine($"{checks-failures}/{checks} passed; {failures} failures");
        bool baseline=args.Length>0&&args[0]=="baseline";
        Environment.ExitCode=baseline?(failures==5?0:1):(failures==0?0:1);
    }
}
