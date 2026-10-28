// Production watch compiled unchanged. Native/persistence adapters are explicit doubles.
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
public static class Log { public static void Warning(string value){} }
public static class ModEvents
{
    public struct SGameUpdateData {}
    public delegate void ModEventHandlerDelegate<T>(ref T data);
    public static UpdateEvent GameUpdate=new UpdateEvent();
    public class UpdateEvent {
        public ModEventHandlerDelegate<SGameUpdateData> Callback;
        public void RegisterHandler(ModEventHandlerDelegate<SGameUpdateData> callback){Callback+=callback;}
        public void Tick(){UnityEngine.Time.realtimeSinceStartup+=1;var data=new SGameUpdateData();Callback(ref data);}
    }
}
public class ItemStack {}
public class Grid { public ItemStack[] items=new ItemStack[2]; }
public class Bag { public Grid ItemGrid=new Grid();public bool[] LockedSlots; }
public class World { public EntityPlayerLocal Player;public EntityPlayerLocal GetPrimaryPlayer()=>Player; }
public class EntityPlayerLocal { public Bag bag=new Bag();public World world; }
public class Admission { public string JobId; }
public class RebirthCamperStationQueuedAuthority {
    public class Binding { public Admission Admission=new Admission();public EntityPlayerLocal Player;public object Station=new object();public Progression Progression=new Progression(); }
}
public class Progression {public Dictionary<string,object> StationRefundArchives=new Dictionary<string,object>();}
public static class RebirthCamperStationWindowSession {public static object Lease=new object();public static bool TryGetLeaseIdentity(EntityPlayerLocal player,out object lease){lease=Lease;return lease!=null;}}
public class RebirthCamperStationCancellationCheckpoint {
    public RebirthCamperStationQueuedAuthority.Binding Binding;
    public object CopyRefund()=>new object();
    public static bool TryCreate(RebirthCamperStationQueuedAuthority.Binding binding,out RebirthCamperStationCancellationCheckpoint checkpoint)
    {checkpoint=new RebirthCamperStationCancellationCheckpoint{Binding=binding};return true;}
}
public class RebirthStationRefundBackpackPlan {
    public static bool Room=true;
    public static bool TryCreate(ItemStack[] slots,int count,Func<int,bool> locked,object refund,out RebirthStationRefundBackpackPlan plan)
    {plan=new RebirthStationRefundBackpackPlan();return Room;}
    public bool MatchesBefore(ItemStack[] slots,int count,Func<int,bool> locked)=>Room;
}
public class RebirthCamperStationCancellationSave {
    public static int Begins,Saves;public static bool Ready;
    public static bool TryBegin(RebirthCamperStationCancellationCheckpoint cp,out RebirthCamperStationCancellationSave op)
    {Begins++;op=new RebirthCamperStationCancellationSave();return false;}
    public bool TrySave(){Saves++;return Ready;}
}
public class RebirthCamperStationCancellationRemoval {
    public static int Begins,Releases;public static bool Confirm,Published,Release;
    public static bool TryBegin(RebirthCamperStationCancellationCheckpoint cp,RebirthCamperStationCancellationSave save,out RebirthCamperStationCancellationRemoval op)
    {Begins++;op=new RebirthCamperStationCancellationRemoval();return false;}
    public bool TryConfirm()=>Confirm;
    public bool TrySavePublication()=>Published;
    public bool TryReleaseSettled(RebirthCamperStationRefundDelivery delivery){Releases++;return Release;}
}
public class RebirthCamperStationRefundDeliveryRecord {}
public class RebirthCamperStationRefundRecovery {
    public static int Begins,Disposals;
    public static bool Continue=true;public bool CanContinue=>Continue;public static int DisposeFailures;
    public static bool TryBegin(EntityPlayerLocal player,string job,out RebirthCamperStationRefundRecovery recovery)
    {Begins++;recovery=new RebirthCamperStationRefundRecovery();return false;}
    public void Dispose(){if(DisposeFailures>0){DisposeFailures--;throw new Exception("cleanup");}Disposals++;}
}
public static class RebirthCamperStationRefundArchiveService {
    public static int Calls;public static bool Ready;
    public static bool TryArchive(EntityPlayerLocal player,string job,RebirthCamperStationRefundRecovery recovery=null){Calls++;return Ready;}
}
public class RebirthCamperStationRefundDelivery {
    public static int Begins,Applications;public static bool Saved;private bool issued;
    public static bool TryBegin(RebirthCamperStationCancellationCheckpoint cp,RebirthCamperStationCancellationRemoval removal,out RebirthCamperStationRefundDelivery op)
    {Begins++;op=new RebirthCamperStationRefundDelivery();return false;}
    public bool TryApply(){if(issued)return false;issued=true;Applications++;return false;}
    public bool TryGetSaved(out RebirthCamperStationRefundDeliveryRecord saved){saved=Saved?new RebirthCamperStationRefundDeliveryRecord():null;return Saved;}
}
public static class MobileCancellationWatchFixture {
    private static void Check(bool value,string label){if(!value)throw new Exception(label);}
    public static void Run(){
        var player=new EntityPlayerLocal{world=new World()};player.world.Player=player;
        var binding=new RebirthCamperStationQueuedAuthority.Binding{Player=player};binding.Admission.JobId="fixture-job";
        RebirthCamperStationCancellationWatch.Install();
        RebirthStationRefundBackpackPlan.Room=false;
        Check(!RebirthCamperStationCancellationWatch.TryWatch(binding),"full bag must reject before any checkpoint side effect");
        Check(RebirthCamperStationCancellationSave.Begins==0,"no staged payment on full bag");
        RebirthStationRefundBackpackPlan.Room=true;Check(RebirthCamperStationCancellationWatch.TryWatch(binding),"admit");
        Check(RebirthCamperStationCancellationWatch.IsWatching("fixture-job"),"original owner visible on main thread");
        ThreadManager.Main=false;Check(!RebirthCamperStationCancellationWatch.IsWatching("fixture-job"),"offthread lookup refused");ThreadManager.Main=true;
        ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationCancellationSave.Begins==1,"retain failed save begin original");
        RebirthCamperStationCancellationSave.Ready=true;RebirthStationRefundBackpackPlan.Room=false;ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationCancellationRemoval.Begins==0,"full bag must preserve queue before removal");
        RebirthStationRefundBackpackPlan.Room=true;ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationCancellationRemoval.Begins==1,"retain failed removal begin original");
        RebirthCamperStationCancellationRemoval.Confirm=true;ModEvents.GameUpdate.Tick();Check(RebirthCamperStationRefundDelivery.Begins==0,"publication before payment");
        RebirthCamperStationCancellationRemoval.Published=true;ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationRefundDelivery.Begins==1&&RebirthCamperStationRefundDelivery.Applications==1,"retain failed delivery begin; one application");
        RebirthCamperStationRefundDelivery.Saved=true;ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationCancellationRemoval.Releases==1,"saved refund before release");
        RebirthCamperStationRefundDelivery.Saved=false;RebirthCamperStationCancellationRemoval.Release=true;ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationCancellationRemoval.Releases==2,"retry exact settled release without reconfirming terminal delivery");
        int releases=RebirthCamperStationCancellationRemoval.Releases;ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();
        Check(releases==RebirthCamperStationCancellationRemoval.Releases&&RebirthCamperStationRefundRecovery.Begins==1,"postrelease recovery retained through failed begin and archive retry");
        RebirthCamperStationRefundArchiveService.Ready=true;ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationRefundRecovery.Disposals==1,"archive confirmed before recovery disposal");
        int archives=RebirthCamperStationRefundArchiveService.Calls;ModEvents.GameUpdate.Tick();Check(archives==RebirthCamperStationRefundArchiveService.Calls,"terminal entry removed after archive");
        binding=new RebirthCamperStationQueuedAuthority.Binding{Player=player};binding.Admission.JobId="handoff-job";
        RebirthCamperStationRefundDelivery.Saved=true;RebirthCamperStationRefundRecovery.Continue=false;RebirthCamperStationRefundRecovery.DisposeFailures=2;
        Check(RebirthCamperStationCancellationWatch.TryWatch(binding),"released cancellation handoff starts");
        ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();
        archives=RebirthCamperStationRefundArchiveService.Calls;
        RebirthCamperStationWindowSession.Lease=new object();ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationCancellationWatch.IsWatching("handoff-job")&&archives==RebirthCamperStationRefundArchiveService.Calls,"invalid released recovery cleanup failure retains original owner, no archival or credit replay");
        ModEvents.GameUpdate.Tick();Check(!RebirthCamperStationCancellationWatch.IsWatching("handoff-job"),"disposed released recovery hands durable refund to cold watcher");
        Console.WriteLine("PASS mobile cancellation watch source fixture");
    }
}
