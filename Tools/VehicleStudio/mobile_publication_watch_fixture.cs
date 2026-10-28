// Explicit native/payment/save adapters. The production watcher itself is compiled unchanged.
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
public sealed class World
{
    public bool Remote;
    public EntityPlayer Player;
    public bool IsRemote()=>Remote;
    public EntityPlayer GetEntity(int id)=>Player!=null&&Player.entityId==id?Player:null;
}
public sealed class EntityPlayer { public World world;public int entityId=1;public bool Dead;public bool IsDead()=>Dead; }
public sealed class GameManager { public static GameManager Instance=new GameManager();public World World; }
public struct RebirthCamperStationAddress { }
public sealed class RebirthStationGridAdmission
{
    public string JobId;
    public string CreationId="creation";
    public RebirthStationGridAdmission Clone()=>new RebirthStationGridAdmission{JobId=JobId};
}
public static class ModEvents
{
    public struct SGameUpdateData { }
    public delegate void ModEventHandlerDelegate<T>(ref T data);
    public static readonly UpdateEvent GameUpdate=new UpdateEvent();
    public sealed class UpdateEvent
    {
        public int Registrations;public ModEventHandlerDelegate<SGameUpdateData> Handler;
        public void RegisterHandler(ModEventHandlerDelegate<SGameUpdateData> callback){Registrations++;Handler+=callback;}
        public void Tick(){var data=new SGameUpdateData();Handler?.Invoke(ref data);}
    }
}
public static class Log { public static int Warnings;public static void Warning(string message){Warnings++;} }
public static class RebirthCamperStationSaveEvidence
{
    public sealed class Publication { }
    public sealed class Request : IDisposable
    {
        public bool Closed;public int Disposals,DisposeFailures;
        public void Dispose(){if(DisposeFailures>0){DisposeFailures--;throw new InvalidOperationException("fixture release failure");}if(!Closed){Closed=true;Disposals++;}}
    }
    public static bool Ready;public static int Observations;public static bool Throw;
    public static int CompletionStarts;public static Request LastCompletion;
    public static Request BeginCompletion(RebirthCamperStationCompletionCapture.SuccessfulOutput completed)
    {CompletionStarts++;return LastCompletion=new Request();}
    public static bool TryObserve(Request request,out Publication proof)
    {
        Observations++;proof=null;if(Throw)throw new InvalidOperationException("fixture save failure");
        if(!Ready)return false;proof=new Publication();request.Dispose();return true;
    }
}
public static class RebirthStationPreparationService
{
    public static bool Allow=true;public static int Attempts;public static RebirthCamperStationSaveEvidence.Request Last;
    internal static bool TryPublishMobilePrepared(EntityPlayer player,RebirthCamperStationAddress address,string creation,Guid job,
        out RebirthStationGridAdmission attempted,out RebirthCamperStationSaveEvidence.Request request)
    {
        attempted=null;request=null;if(!Allow)return false;
        Attempts++;attempted=new RebirthStationGridAdmission{JobId=job.ToString("N")};request=Last=new RebirthCamperStationSaveEvidence.Request();return true;
    }
}
public static class RebirthCamperStationPublicationService
{
    public static int Records;public static int Failures;
    public static bool TryRecord(EntityPlayer player,RebirthStationGridAdmission admission,RebirthCamperStationSaveEvidence.Publication proof)
    {Records++;if(Failures>0){Failures--;return false;}return true;}
}
public static class RebirthCamperStationQueuedAuthority
{
    public static int Registrations;
    public static bool TryRegister(EntityPlayer player,RebirthStationGridAdmission admission,RebirthCamperStationSaveEvidence.Publication proof)
    {Registrations++;return true;}
}
public static class MobilePublicationWatchFixture
{
    private static void Check(bool value,string name){if(!value)throw new Exception(name);}
    private static EntityPlayer Player()
    {
        var world=new World();var player=new EntityPlayer{world=world};world.Player=player;GameManager.Instance.World=world;return player;
    }
    private static void Tick(float advance=.3f){UnityEngine.Time.realtimeSinceStartup+=advance;ModEvents.GameUpdate.Tick();}
    public static void Run()
    {
        var player=Player();var address=new RebirthCamperStationAddress();var job=Guid.NewGuid();
        Check(!RebirthCamperStationPublicationWatch.TryPublishPrepared(player,address,"creation",job),"uninstalled refusal");
        RebirthCamperStationPublicationWatch.Install();RebirthCamperStationPublicationWatch.Install();
        Check(ModEvents.GameUpdate.Registrations==1,"idempotent install");
        ThreadManager.Main=false;
        Check(!RebirthCamperStationPublicationWatch.TryPublishPrepared(player,address,"creation",job),"wrong thread refusal");ThreadManager.Main=true;
        Check(RebirthCamperStationPublicationWatch.TryPublishPrepared(player,address,"creation",job),"original publication starts");
        var request=RebirthStationPreparationService.Last;
        Check(!RebirthCamperStationPublicationWatch.TryPublishPrepared(player,address,"creation",job)&&RebirthStationPreparationService.Attempts==1,"duplicate cannot replay payment");
        Tick();Check(RebirthCamperStationPublicationService.Records==0&&!request.Closed,"unconfirmed save grants nothing");
        RebirthCamperStationSaveEvidence.Ready=true;RebirthCamperStationPublicationService.Failures=2;
        Tick();var observations=RebirthCamperStationSaveEvidence.Observations;
        Tick();Tick();
        Check(RebirthCamperStationPublicationService.Records==3&&RebirthCamperStationSaveEvidence.Observations==observations&&
            RebirthStationPreparationService.Attempts==1&&request.Disposals==1,"owner save retries reuse proof without payment or repeated observation");
        Tick();Check(RebirthCamperStationPublicationService.Records==3,"completed observation removed");
        job=Guid.NewGuid();Check(RebirthCamperStationPublicationWatch.TryPublishPrepared(player,address,"creation",job),"world-change request starts");
        request=RebirthStationPreparationService.Last;GameManager.Instance.World=new World();Tick();
        Check(request.Closed&&RebirthCamperStationPublicationService.Records==3,"world change disposes without credit");
        player=Player();job=Guid.NewGuid();RebirthCamperStationSaveEvidence.Ready=false;
        Check(RebirthCamperStationPublicationWatch.TryPublishPrepared(player,address,"creation",job),"timeout request starts");
        request=RebirthStationPreparationService.Last;Tick(31);
        Check(request.Closed&&RebirthCamperStationPublicationService.Records==3,"timeout disposes without replay or credit");
        job=Guid.NewGuid();Check(RebirthCamperStationPublicationWatch.TryPublishPrepared(player,address,"creation",job),"exception request starts");
        request=RebirthStationPreparationService.Last;RebirthCamperStationSaveEvidence.Throw=true;Tick();
        Check(request.Closed&&RebirthCamperStationPublicationService.Records==3&&Log.Warnings>=3,"save exception retains uncertainty without credit");
        RebirthCamperStationSaveEvidence.Throw=false;job=Guid.NewGuid();
        Check(RebirthCamperStationPublicationWatch.TryPublishPrepared(player,address,"creation",job),"publication cleanup retry starts");
        request=RebirthStationPreparationService.Last;request.DisposeFailures=2;GameManager.Instance.World=new World();
        int attempts=RebirthStationPreparationService.Attempts,warnings=Log.Warnings;
        Tick();Tick();Check(!request.Closed&&Log.Warnings==warnings+2,"publication cleanup failure logs once");
        Tick();Check(request.Closed&&request.Disposals==1&&RebirthStationPreparationService.Attempts==attempts&&
            RebirthCamperStationPublicationService.Records==3,"publication cleanup retries same request without payment or credit");
        Console.WriteLine("PASS actual mobile publication watcher: install/thread/duplicate gates, pending save, owner retry with one proof, completion cleanup, world change, timeout and exception. Native payment/save adapters doubled; no gameplay proof.");
    }
}
public static class RebirthCamperStationCompletionCapture
{
    public class Binding { public EntityPlayer Player=new EntityPlayer();public RebirthCamperStationAddress Address;public RebirthStationGridAdmission Admission=new RebirthStationGridAdmission{JobId=Guid.NewGuid().ToString("N")}; }
    public class SuccessfulOutput { public Binding Binding=new Binding();public bool Valid=true;public bool MatchesLiveOutput()=>Valid; }
}
public static class RebirthCamperStationCompletionService
{
    public static int Records,Failures;
    public static bool TryRecord(RebirthCamperStationCompletionCapture.SuccessfulOutput completed,RebirthCamperStationSaveEvidence.Publication proof)
    {Records++;if(Failures>0){Failures--;return false;}return true;}
}
public class RebirthCamperStationTrainingPlan
{
 public static int Created;
 public static bool TryCreate(RebirthCamperStationCompletionCapture.SuccessfulOutput completed,out RebirthCamperStationTrainingPlan plan)
 {Created++;plan=new RebirthCamperStationTrainingPlan();return completed.Valid;}
}
public static class RebirthCamperStationTrainingService
{
 public static int Calls,Failures;public static RebirthCamperStationTrainingPlan First;
 public static bool TryGrant(RebirthCamperStationTrainingPlan plan)
 {Calls++;if(First==null)First=plan;else if(!ReferenceEquals(First,plan))throw new Exception("training plan changed during retry");if(Failures>0){Failures--;return false;}return true;}
}
public static class RebirthCamperStationCompletionIntentService
{
 public static int Calls,Failures=2;
 public static bool TrySave(RebirthCamperStationTrainingPlan plan)
 {Calls++;if(Failures>0){Failures--;return false;}return true;}
}
public sealed class RebirthCamperStationNativeRewardDelivery
{
 public static bool TryBegin(RebirthCamperStationTrainingPlan p,out RebirthCamperStationNativeRewardDelivery delivery)
 {delivery=new RebirthCamperStationNativeRewardDelivery();return true;}
 public bool TryDeliver()=>true;
}
public sealed class RebirthCamperStationReceiptRetirement:IDisposable
{
 public static bool TryBegin(RebirthCamperStationTrainingPlan p,RebirthCamperStationNativeRewardDelivery d,out RebirthCamperStationReceiptRetirement r)
 {r=new RebirthCamperStationReceiptRetirement();return true;}
 public bool IsCurrent()=>true;public bool TryConfirm()=>true;public void Dispose(){}
}
public class RebirthCamperStationCompletionArchive:IDisposable
{
 public static int Begins,Calls,Disposals;public static int Failures=2,DisposeFailures=1;
 public static bool Continue=true;public bool CanContinue=>Continue;
 public static bool TryBegin(EntityPlayer player,string job,out RebirthCamperStationCompletionArchive archive)
 {Begins++;archive=new RebirthCamperStationCompletionArchive();return false;}
 public bool TryArchive(){Calls++;if(Failures>0){Failures--;return false;}return true;}
 public void Dispose(){if(DisposeFailures>0){DisposeFailures--;throw new Exception("archive dispose");}Disposals++;}
}
public static class RebirthCamperStationWindowSession
{
 public static object Lease=new object();public static bool TryGetLeaseIdentity(EntityPlayer player,out object lease){lease=Lease;return lease!=null;}
}
public static class RebirthStationLiveAccess {public static bool TryGetMobileLease(EntityPlayer player,RebirthCamperStationAddress address,string creation,out object station,out object lease){station=new object();lease=RebirthCamperStationWindowSession.Lease;return lease!=null;}}
public static class MobileCompletionWatchFixture
{
    private static void Check(bool value,string name){if(!value)throw new Exception(name);}
    private static void Tick(float advance=.3f){UnityEngine.Time.realtimeSinceStartup+=advance;ModEvents.GameUpdate.Tick();}
    public static void Run()
    {
        var completed=new RebirthCamperStationCompletionCapture.SuccessfulOutput();
        Check(!RebirthCamperStationCompletionWatch.TryWatch(completed),"completion uninstalled refusal");
        RebirthCamperStationCompletionWatch.Install();RebirthCamperStationCompletionWatch.Install();
        Check(ModEvents.GameUpdate.Registrations==2,"completion idempotent registration");
        ThreadManager.Main=false;Check(!RebirthCamperStationCompletionWatch.TryWatch(completed),"completion wrong-thread refusal");ThreadManager.Main=true;
        RebirthCamperStationSaveEvidence.Throw=false;RebirthCamperStationSaveEvidence.Ready=false;
        Check(RebirthCamperStationCompletionWatch.TryWatch(completed),"completion watch begins");
        Check(RebirthCamperStationCompletionWatch.TryWatch(completed)&&RebirthCamperStationSaveEvidence.CompletionStarts==1,"same event idempotent");
        var substituted=new RebirthCamperStationCompletionCapture.SuccessfulOutput();substituted.Binding.Admission.JobId=completed.Binding.Admission.JobId;
        Check(!RebirthCamperStationCompletionWatch.TryWatch(substituted),"replacement event refused");
        Tick();Check(RebirthCamperStationCompletionService.Records==0,"unconfirmed completion grants no record");
        RebirthCamperStationSaveEvidence.Ready=true;RebirthCamperStationCompletionService.Failures=2;RebirthCamperStationTrainingService.Failures=2;
        Tick();int observations=RebirthCamperStationSaveEvidence.Observations;Tick();Tick();Tick();
        Check(RebirthCamperStationCompletionService.Records==3&&RebirthCamperStationSaveEvidence.Observations==observations&&
            RebirthCamperStationSaveEvidence.CompletionStarts==1,"completion owner retries reuse one proof and one save request");
        Tick();Tick();Check(RebirthCamperStationTrainingPlan.Created==1&&RebirthCamperStationTrainingService.Calls==3&&
            RebirthCamperStationCompletionService.Records==3,"training retries retain one frozen plan and saved completion");
        Tick();Check(RebirthCamperStationCompletionIntentService.Calls==3&&RebirthCamperStationTrainingService.Calls==3,
            "completion intent save retries never repeat granted training");
        Tick();Tick();Tick();Tick();Tick();Tick();
        Check(RebirthCamperStationCompletionArchive.Begins==1&&RebirthCamperStationCompletionArchive.Calls==3&&
            RebirthCamperStationCompletionArchive.Disposals==1&&!RebirthCamperStationCompletionWatch.IsWatching(completed.Binding.Admission.JobId),
            "retired original retains one failed-begin archive through save/disposal retries before removal");
        completed=new RebirthCamperStationCompletionCapture.SuccessfulOutput();
        Check(RebirthCamperStationCompletionWatch.TryWatch(completed),"changed-event watch starts");
        var request=RebirthCamperStationSaveEvidence.LastCompletion;completed.Valid=false;Tick();
        Check(request.Closed&&RebirthCamperStationCompletionService.Records==3,"changed completion stops without acknowledgement");
        completed=new RebirthCamperStationCompletionCapture.SuccessfulOutput();
        Check(RebirthCamperStationCompletionWatch.TryWatch(completed),"completion timeout watch starts");
        request=RebirthCamperStationSaveEvidence.LastCompletion;Tick(31);
        Check(request.Closed&&RebirthCamperStationCompletionService.Records==3,"completion timeout does not acknowledge or release custody");
        completed=new RebirthCamperStationCompletionCapture.SuccessfulOutput();
        Check(RebirthCamperStationCompletionWatch.TryWatch(completed),"cleanup retry watch starts");
        request=RebirthCamperStationSaveEvidence.LastCompletion;request.DisposeFailures=2;completed.Valid=false;
        int warnings=Log.Warnings,records=RebirthCamperStationCompletionService.Records,starts=RebirthCamperStationSaveEvidence.CompletionStarts;
        Tick();Tick();Check(!request.Closed&&Log.Warnings==warnings+2,"cleanup retries log release failure once plus original interruption");
        Tick();Check(request.Closed&&request.Disposals==1&&RebirthCamperStationCompletionService.Records==records&&
            RebirthCamperStationSaveEvidence.CompletionStarts==starts,"cleanup retry retains original request without resuming awards or saves");
        completed=new RebirthCamperStationCompletionCapture.SuccessfulOutput();
        RebirthCamperStationTrainingService.First=null;RebirthCamperStationCompletionArchive.Continue=false;
        RebirthCamperStationCompletionArchive.DisposeFailures=2;
        Check(RebirthCamperStationCompletionWatch.TryWatch(completed),"new original completion handoff starts");
        Tick();Tick();Tick(); // native retired, old requests disposed, one retained invalid archive
        int archiveCalls=RebirthCamperStationCompletionArchive.Calls;
        RebirthCamperStationWindowSession.Lease=new object();Tick();Tick();
        Check(RebirthCamperStationCompletionWatch.IsWatching(completed.Binding.Admission.JobId)&&
            RebirthCamperStationCompletionArchive.Calls==archiveCalls,"new lease cannot handoff until original archive cleanup succeeds");
        Tick();Check(!RebirthCamperStationCompletionWatch.IsWatching(completed.Binding.Admission.JobId),"disposed invalid original hands durable records to cold observer");
        completed=new RebirthCamperStationCompletionCapture.SuccessfulOutput();RebirthCamperStationTrainingService.First=null;
        Check(RebirthCamperStationCompletionWatch.TryWatch(completed),"missing window handoff starts");Tick();Tick();
        RebirthCamperStationWindowSession.Lease=null;Tick();
        Check(!RebirthCamperStationCompletionWatch.IsWatching(completed.Binding.Admission.JobId),"retired original with missing lease removes only observer after old request cleanup");
        Console.WriteLine("PASS actual mobile completion watcher: install/thread gates, original event identity, pending save, retries with one proof, event change and timeout. Event/native save/record services doubled; no gameplay proof.");
    }
}
