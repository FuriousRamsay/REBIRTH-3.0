using System;
using System.IO;
using System.Collections.Generic;
// Native lifecycle and World/GameIO are doubles; production registration/service/store runs.
namespace UnityEngine.Scripting { class PreserveAttribute : Attribute { } }
public interface IModApi { void InitMod(Mod mod); } public class Mod {public string Path=System.IO.Path.GetTempPath();}
class World { public WorldState worldState=new WorldState(); public bool IsRemote()=>false; }
class WorldState { public string Guid=System.Guid.NewGuid().ToString("N").ToUpperInvariant(); }
class GameStateManager { public bool IsGameStarted()=>true; }
class GameManager { public static GameManager Instance; public World World; public bool IsStartingGame=false; public GameStateManager gameStateManager=new GameStateManager(); public bool IsEditMode()=>false; }
class ConnectionManager { public bool IsServer=true; }
class SingletonMonoBehaviour<T> { public static T Instance; }
class ThreadManager { public static bool IsMainThread()=>true; }
class GameIO { public static string Path; public static string GetSaveGameDir()=>Path; }
class Log { public static void Warning(string message) { } }
static class ModEvents
{
    public delegate void ModEventHandlerDelegate<T>(ref T data) where T:struct;
    public class Event<T> where T:struct
    {
        public readonly List<ModEventHandlerDelegate<T>> Handlers=new List<ModEventHandlerDelegate<T>>();
        public bool ThrowNext;
        public void RegisterHandler(ModEventHandlerDelegate<T> handler) { if(ThrowNext){ThrowNext=false;throw new IOException("register");} Handlers.Add(handler); }
        public void Invoke(T data) { foreach(var h in Handlers) h(ref data); }
    }
    public struct SGameStartingData { public bool AsServer; }
    public struct SGameStartDoneData { }
    public struct SGameUpdateData { }
    public struct SWorldShuttingDownData { }
    public struct SGameShutdownData { }
    public static readonly Event<SGameStartingData> GameStarting=new Event<SGameStartingData>();
    public static readonly Event<SGameStartDoneData> GameStartDone=new Event<SGameStartDoneData>();
    public static readonly Event<SGameUpdateData> GameUpdate=new Event<SGameUpdateData>();
    public static readonly Event<SWorldShuttingDownData> WorldShuttingDown=new Event<SWorldShuttingDownData>();
    public static readonly Event<SGameShutdownData> GameShutdown=new Event<SGameShutdownData>();
}
static class Program
{
    static int checks;
    static void Check(bool yes,string name) { if(!yes)throw new Exception("FAIL "+name); checks++;Console.WriteLine("PASS "+name); }
    static RebirthPoiWorldBinding Binding(Func<bool> current=null)
    {
        string root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rebirth-purge-lifecycle-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        return new RebirthPoiWorldBinding(new object(),Guid.NewGuid().ToString("N").ToUpperInvariant(),root,current??(()=>true));
    }
    static void Main()
    {
        RebirthPoiWorldStore store=null; double time=0; int captures=0,opens=0; bool ready=false; var binding=Binding();
        var life=new RebirthPoiWorldLifecycle(()=>{captures++;return ready?binding:null;},()=>time,
            (RebirthPoiWorldBinding b,out RebirthPoiWorldStore s)=>{opens++;return RebirthPoiWorldStore.TryOpen(b,out s);},_=>{});
        life.Begin(false); life.StartDone(); life.Tick(); Check(life.State==RebirthPoiLifecycleState.Off && captures==0 && opens==0,"client never creates world ledger");
        life.Begin(true); life.Tick(); Check(captures==0,"server waits actual StartDone");
        life.StartDone(); life.Tick(); Check(life.State==RebirthPoiLifecycleState.WaitingForBinding && captures==1 && opens==0,"StartDone before native binding ready");
        for(int i=0;i<500;i++)life.Tick(); Check(captures==1 && opens==0,"no per-frame readiness disk scan");
        ready=true; time=1; life.Tick(); Check(life.State==RebirthPoiLifecycleState.Ready && opens==1 && life.TryGetStore(out store),"actual store opened when binding ready");
        for(int i=0;i<1000;i++){time+=0.25;life.Tick();} Check(opens==1,"ready ticks do not reopen files");
        Check(store.Published.Count==0,"no automatic POI clear discovery or reset");
        life.Stop(); life.StartDone(); life.Tick(); Check(life.State==RebirthPoiLifecycleState.Off && !life.TryGetStore(out _),"late done after shutdown cannot reopen");
        int diagnostics=0;time=0;
        var waiting=new RebirthPoiWorldLifecycle(()=>null,()=>time,null,_=>diagnostics++); waiting.Begin(true); waiting.StartDone(); waiting.Tick();time=120;waiting.Tick();
        Check(waiting.State==RebirthPoiLifecycleState.Refused && diagnostics==1,"bounded readiness timeout refuses empty fallback");
        waiting.Tick(); Check(diagnostics==1,"readiness refusal diagnostic not repeated");
        var malformed=Binding(); Directory.CreateDirectory(malformed.Directory); File.WriteAllText(System.IO.Path.Combine(malformed.Directory,"world.xml"),"broken"); int malformedOpens=0;time=0;
        var bad=new RebirthPoiWorldLifecycle(()=>malformed,()=>time,(RebirthPoiWorldBinding b,out RebirthPoiWorldStore s)=>{malformedOpens++;return RebirthPoiWorldStore.TryOpen(b,out s);},_=>{});
        bad.Begin(true);bad.StartDone();bad.Tick();time=1000;bad.Tick();
        Check(bad.State==RebirthPoiLifecycleState.Refused && bad.LastResult==RebirthPoiStoreResult.Corrupt && malformedOpens==1,"corrupt load latched not retried repaired or reset");
        var missing=Binding();Directory.CreateDirectory(missing.Directory);File.WriteAllText(System.IO.Path.Combine(missing.Directory,"orphan"),"retained");time=0;
        var absent=new RebirthPoiWorldLifecycle(()=>missing,()=>time,null,_=>{});absent.Begin(true);absent.StartDone();absent.Tick();
        Check(absent.State==RebirthPoiLifecycleState.Refused && absent.LastResult==RebirthPoiStoreResult.Missing,"missing with artifacts does not create empty world");
        var pendingBinding=Binding(); bool armed=true;time=0;int attempts=0;
        var pending=new RebirthPoiWorldLifecycle(()=>pendingBinding,()=>time,(RebirthPoiWorldBinding b,out RebirthPoiWorldStore s)=>RebirthPoiWorldStore.TryOpen(b,out s,stage=>{if(stage=="afterManifestCandidateWritten"){attempts++;if(armed)throw new IOException("fault");}}),_=>{});
        pending.Begin(true);pending.StartDone();pending.Tick();
        Check(pending.State==RebirthPoiLifecycleState.PendingRetry && !pending.TryGetStore(out _) && attempts==1,"uncertain first candidate retained and unavailable to gameplay");
        string candidate=File.ReadAllText(System.IO.Path.Combine(pendingBinding.Directory,"world.xml.candidate"));
        time=1;pending.Tick();Check(attempts==1,"retry time gated");
        time=2;pending.Tick();Check(attempts==2 && pending.State==RebirthPoiLifecycleState.PendingRetry,"retry original failed candidate");
        time=5;pending.Tick();Check(attempts==2,"retry backoff extends after failure");
        armed=false;time=6;pending.Tick();Check(pending.State==RebirthPoiLifecycleState.Ready && pending.TryGetStore(out store) && store.Published.Manifest==candidate,"original candidate publishes after retry");
                var activeBinding=Binding(); bool operationFault=false; time=0;
        var active=new RebirthPoiWorldLifecycle(()=>activeBinding,()=>time,(RebirthPoiWorldBinding b,out RebirthPoiWorldStore s)=>RebirthPoiWorldStore.TryOpen(b,out s,stage=>{if(operationFault && stage=="afterManifestCandidateWritten")throw new IOException("active fault");}),_=>{});
        active.Begin(true);active.StartDone();active.Tick();active.TryGetStore(out var activeStore);operationFault=true;
        var activePoi=new RebirthPoiIdentity("house",1,2,3,0,4,5,6,"forest");
        Check(activeStore.TryDiscover(activeStore.Published,activePoi)==RebirthPoiStoreResult.Uncertain && !active.TryGetStore(out _),"ready store later pending is immediately withheld");
        time=0.25;active.Tick();Check(active.State==RebirthPoiLifecycleState.PendingRetry,"ready store pending joins lifecycle retry");
        operationFault=false;time=2.25;active.Tick();Check(active.State==RebirthPoiLifecycleState.Ready && active.TryGetStore(out activeStore) && activeStore.Published.Count==1,"later pending candidate resumes original store");
        bool live=true;var scopeBinding=Binding(()=>live);time=0;
        var scoped=new RebirthPoiWorldLifecycle(()=>scopeBinding,()=>time,null,_=>{});scoped.Begin(true);scoped.StartDone();scoped.Tick();live=false;
        Check(!scoped.TryGetStore(out _),"stale world never exposes old store");time=0.25;scoped.Tick();
        Check(scoped.State==RebirthPoiLifecycleState.Refused && scoped.LastResult==RebirthPoiStoreResult.StaleScope,"replacement world cannot receive old cache");
        RebirthPoiWorldLifecycle race=null;time=0;
        race=new RebirthPoiWorldLifecycle(()=>binding,()=>time,(RebirthPoiWorldBinding b,out RebirthPoiWorldStore s)=>{var result=RebirthPoiWorldStore.TryOpen(b,out s);race.Begin(false);return result;},_=>{});
        race.Begin(true);race.StartDone();race.Tick();Check(race.State==RebirthPoiLifecycleState.Off && !race.TryGetStore(out _),"late opener result cannot publish replacement session");
        var shutdownBinding=Binding();time=0;bool shutdownFault=true;
        var shutdown=new RebirthPoiWorldLifecycle(()=>shutdownBinding,()=>time,(RebirthPoiWorldBinding b,out RebirthPoiWorldStore s)=>RebirthPoiWorldStore.TryOpen(b,out s,stage=>{if(shutdownFault && stage=="afterManifestCandidateWritten")throw new IOException("shutdown fault");}),_=>{});
        shutdown.Begin(true);shutdown.StartDone();shutdown.Tick();string retainedPath=System.IO.Path.Combine(shutdownBinding.Directory,"world.xml.candidate");shutdown.Stop();
        Check(shutdown.State==RebirthPoiLifecycleState.Off && File.Exists(retainedPath),"ambiguous shutdown candidate preserved");
        Check(RebirthPoiWorldStore.TryOpen(shutdownBinding,out var restart)==RebirthPoiStoreResult.Uncertain && restart.HasPending,"shutdown candidate recoverable original world");
        Check(restart.TryRetryPending()==RebirthPoiStoreResult.Published,"shutdown persisted candidate exact retry");
        // Native API registration path and partial-registration retry.
        ModEvents.GameUpdate.ThrowNext=true; var api=new RebirthPoiWorldModApi();try{api.InitMod(new Mod());}catch(IOException){}api.InitMod(new Mod());api.InitMod(new Mod());
        Check(ModEvents.GameStarting.Handlers.Count==1 && ModEvents.GameStartDone.Handlers.Count==1 && ModEvents.GameUpdate.Handlers.Count==1 && ModEvents.WorldShuttingDown.Handlers.Count==1 && ModEvents.GameShutdown.Handlers.Count==1,"ModApi partial retry registrations exactly once");
        GameIO.Path=Binding().SaveDirectory;var world=new World();GameManager.Instance=new GameManager{World=world};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
        ModEvents.GameStarting.Invoke(new ModEvents.SGameStartingData{AsServer=false});ModEvents.GameStartDone.Invoke(new ModEvents.SGameStartDoneData());ModEvents.GameUpdate.Invoke(new ModEvents.SGameUpdateData());
        Check(RebirthPoiWorldLifecycle.Instance.State==RebirthPoiLifecycleState.Off,"native event client disabled");
                GameManager.Instance.IsStartingGame=true;
        ModEvents.GameStarting.Invoke(new ModEvents.SGameStartingData{AsServer=true});ModEvents.GameStartDone.Invoke(new ModEvents.SGameStartDoneData());ModEvents.GameUpdate.Invoke(new ModEvents.SGameUpdateData());
        Check(RebirthPoiWorldLifecycle.Instance.State==RebirthPoiLifecycleState.WaitingForBinding && !Directory.Exists(System.IO.Path.Combine(GameIO.Path,"RebirthData","Purge","Clearance")),"native StartDone does not open before IsStartingGame clears");
        GameManager.Instance.IsStartingGame=false;System.Threading.Thread.Sleep(1100);ModEvents.GameUpdate.Invoke(new ModEvents.SGameUpdateData());
        Check(RebirthPoiWorldLifecycle.Instance.TryGetStore(out var nativeStore) && nativeStore.Published.WorldId==Guid.ParseExact(world.worldState.Guid,"N"),"native event path actual store binding publication");
        ModEvents.WorldShuttingDown.Invoke(new ModEvents.SWorldShuttingDownData());ModEvents.GameShutdown.Invoke(new ModEvents.SGameShutdownData());
        Check(RebirthPoiWorldLifecycle.Instance.State==RebirthPoiLifecycleState.Off,"native shutdown events release service");
        Console.WriteLine("RESULT "+checks+" PASS; production ModApi/lifecycle/store; native events/binding methods doubled; actual temp I/O.");
    }
}
internal static class RebirthPurgeMilestoneService {internal static bool TryPreserveBeforeRegression(RebirthPoiWorldSnapshot snapshot)=>true;internal static void Reset(){}internal static void Pulse(){}}
internal static class RebirthPurgeReleasePolicy {internal const bool Enabled=true;}
internal sealed class RebirthSandboxOptionManager {internal static readonly RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();internal bool PoiClearTrackingEnabled=true;}
internal static class RebirthPurgeObjectivePolicy {internal static void Load(string path){}}
internal static class RebirthPurgeDiscoveryPolicy {internal static void Load(string path){}}
internal static class RebirthPurgeSupplyPolicy {internal static void Load(string path){}}
internal static class RebirthPurgeDiscoveryService {internal static void Reset(){}internal static void Pulse(){}}
internal static class RebirthPurgeSupplyService {internal static void Reset(){}internal static void Pulse(){}}
internal sealed class RebirthPurgeObjectiveProgress {internal static readonly RebirthPurgeObjectiveProgress Instance=new RebirthPurgeObjectiveProgress();internal void Reset(){}internal void Pulse(){}}
internal sealed class RebirthPurgePoiCensus {internal static readonly RebirthPurgePoiCensus Instance=new RebirthPurgePoiCensus();internal void Reset(){}internal void Pulse(){}}
internal static class RebirthPurgeSupplyCoordinator {internal static void Reset(){}internal static void Pulse(){}}

internal static class RebirthPurgeClearNotification { internal static void Reset(){} internal static void Pulse(){} }
internal static class RebirthPurgeHudProgress { internal static void Pulse(){} }
