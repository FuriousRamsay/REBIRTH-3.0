// Compile actual cold watcher unchanged; native/file/owner services are explicit doubles.
public static class ThreadManager { public static bool IsMainThread()=>true; }
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
public static class Log { public static int Warnings;public static void Warning(string value){Warnings++;} }
public static class ModEvents {
    public struct SGameUpdateData {}
    public delegate void ModEventHandlerDelegate<T>(ref T data);
    public static UpdateEvent GameUpdate=new UpdateEvent();
    public class UpdateEvent {
        public ModEventHandlerDelegate<SGameUpdateData> Callback;
        public void RegisterHandler(ModEventHandlerDelegate<SGameUpdateData> callback){Callback+=callback;}
        public void Tick(){UnityEngine.Time.realtimeSinceStartup+=1;var data=new SGameUpdateData();Callback(ref data);}
    }
}
public class EntityPlayer {}
public class EntityPlayerLocal:EntityPlayer {}
public class World { public EntityPlayer Player=new EntityPlayer();public EntityPlayer GetPrimaryPlayer()=>Player; }
public class GameManager { public static GameManager Instance=new GameManager();public World World=new World(); }
public struct RebirthCamperStationAddress { public int Id; }
public class RebirthStationGridAdmission {
    public int Id;public string JobId,CreationId="creation";public bool TryGetMobileAddress(out RebirthCamperStationAddress address){address=new RebirthCamperStationAddress{Id=Id};return true;}
    public static bool TryReadStored(XElement node,out RebirthStationGridAdmission admission){admission=new RebirthStationGridAdmission{Id=(int)node.Attribute("id"),JobId=(string)node.Parent.Attribute("job")};return true;}
}
public class Archive { public bool IsMobile=true,IsCompletion;public string JobId;public int Id=1;public XElement Write()=>new XElement("archive",new XAttribute("job",JobId??""),new XElement("stationAdmission",new XAttribute("id",Id))); }
public class Progression {
    public Dictionary<string,object> CamperStationRefundDeliveries=new Dictionary<string,object>();
    public Dictionary<string,Archive> StationRefundArchives=new Dictionary<string,Archive>();
    public Dictionary<string,RebirthStationGridAdmission> StationPreparations=new Dictionary<string,RebirthStationGridAdmission>();
}
public class Identity {public string StorageKey="owner";}
public static class RebirthCamperRemoteStationTransport {public static bool Valid=true;public static bool TryGetCurrent(EntityPlayer p,out object s,out RebirthCamperStationAddress a,out string c,out object l){s=RebirthCamperStationWindowSession.Station;a=new RebirthCamperStationAddress{Id=1};c="creation";l=RebirthCamperStationWindowSession.Lease;return Valid;}}
public static class RebirthCamperStationRemoteCancellationWatch {public static bool Watching;public static bool IsWatching(string job)=>Watching;}
public class Owner { public Progression Progression=new Progression(); }
public static class RebirthWorldCharacterService {
    public static bool TryGetIdentity(EntityPlayer p,out Identity i){i=new Identity();return true;}public static Owner Owner=new Owner();public static bool TryGet(EntityPlayer player,out Owner owner){owner=Owner;return true;}
}
public static class RebirthCamperStationWindowSession {
    public static object Lease=new object(),Station=new object();public static int Id=1;
    public static bool TryGetLeaseIdentity(EntityPlayer player,out object lease){lease=Lease;return lease!=null;}
    public static bool TryGetCurrent(EntityPlayer player,out object station,out RebirthCamperStationAddress address){station=Station;address=new RebirthCamperStationAddress{Id=Id};return Lease!=null;}
}
public static class RebirthCamperStationSaveEvidence { public static bool Pending;public static bool IsPending(object station)=>Pending; }
public static class RebirthCamperStationCancellationWatch { public static HashSet<string> Owned=new HashSet<string>();public static bool IsWatching(string job)=>Owned.Contains(job); }
public class RebirthCamperStationRefundRecovery {
    public static int Begins,Disposals,DisposeFailures,Receipt=1,Applications;public static bool Continue=true,ApplyReady;
    public bool CanContinue=>Continue;
    public bool TryApply(){if(Receipt!=3||!ApplyReady)return false;Receipt=1;Applications++;return true;} // explicit persisted prepared/before witness double
    public static bool TryBegin(EntityPlayer player,string job,out RebirthCamperStationRefundRecovery recovery)
    {Begins++;recovery=Receipt==3||Receipt==1?new RebirthCamperStationRefundRecovery():null;return false;}
    public void Dispose(){Disposals++;if(DisposeFailures>0){DisposeFailures--;throw new Exception("cleanup");}}
}
public static class RebirthCamperStationRefundArchiveService {
    public static int Calls;public static bool Saved,Flush;
    public static bool TryArchive(EntityPlayer player,string job,RebirthCamperStationRefundRecovery recovery=null){
        Calls++;var state=RebirthWorldCharacterService.Owner.Progression;
        if(RebirthCamperStationRefundRecovery.Receipt!=1)return false;
        if(!state.StationRefundArchives.ContainsKey(job)){
            if(!Saved||recovery==null)return false;
            state.StationRefundArchives[job]=new Archive{JobId=job};RebirthCamperStationRefundRecovery.Continue=false;
        }
        return Flush;
    }
}
public static class MobileRefundRecoveryWatchFixture {
    private static void Check(bool value,string label){if(!value)throw new Exception(label);}
    private static void Add(string job,int id){var p=RebirthWorldCharacterService.Owner.Progression;p.CamperStationRefundDeliveries[job]=new object();p.StationPreparations[job]=new RebirthStationGridAdmission{Id=id,JobId=job};}
    public static void Run(){
        Add("other",2);Add("owned",1);Add("cold",1);RebirthCamperStationCancellationWatch.Owned.Add("owned");
        RebirthCamperStationRefundRecoveryWatch.Install();RebirthCamperStationSaveEvidence.Pending=true;
        ModEvents.GameUpdate.Tick();Check(RebirthCamperStationRefundRecovery.Begins==0,"pending native save does not consume attempt");
        RebirthCamperStationSaveEvidence.Pending=false;ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationRefundRecovery.Begins==1,"matching station only; skip original; retain failed begin original");
        RebirthCamperStationRefundArchiveService.Saved=true;ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationRefundRecovery.Disposals==0,"cached archive retries despite CanContinue false");
        RebirthCamperStationRefundArchiveService.Flush=true;RebirthCamperStationRefundRecovery.DisposeFailures=2;ModEvents.GameUpdate.Tick();
        int begins=RebirthCamperStationRefundRecovery.Begins;RebirthCamperStationWindowSession.Lease=new object();ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationRefundRecovery.Begins==begins,"lease change cannot overwrite failed cleanup");
        ModEvents.GameUpdate.Tick();Check(RebirthCamperStationRefundRecovery.Disposals>=3,"cleanup retried until success");
        // Fresh scope with missing/duplicate receipt produces no retained operation or credit route.
        RebirthWorldCharacterService.Owner=new Owner();Add("zero",1);RebirthCamperStationRefundRecovery.Receipt=0;RebirthCamperStationWindowSession.Lease=new object();ModEvents.GameUpdate.Tick();
        ModEvents.GameUpdate.Tick();Check(RebirthCamperStationRefundRecovery.Applications==0&&!RebirthWorldCharacterService.Owner.Progression.StationRefundArchives.ContainsKey("zero"),"zero receipt without fresh native before witness cannot archive or deliver");
        int calls=RebirthCamperStationRefundArchiveService.Calls;
        RebirthWorldCharacterService.Owner=new Owner();Add("two",1);RebirthCamperStationRefundRecovery.Receipt=2;RebirthCamperStationWindowSession.Lease=new object();ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();Check(calls==RebirthCamperStationRefundArchiveService.Calls,"duplicate receipt cannot archive or deliver");
        RebirthWorldCharacterService.Owner=new Owner();Add("timeout",1);RebirthCamperStationRefundRecovery.Receipt=1;RebirthCamperStationRefundRecovery.Continue=true;RebirthCamperStationRefundArchiveService.Saved=false;RebirthCamperStationWindowSession.Lease=new object();ModEvents.GameUpdate.Tick();
        begins=RebirthCamperStationRefundRecovery.Begins;UnityEngine.Time.realtimeSinceStartup+=31;ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();Check(begins==RebirthCamperStationRefundRecovery.Begins,"timeout cannot replay same lease");
        RebirthWorldCharacterService.Owner=new Owner();Add("native-prepared",1);RebirthCamperStationRefundRecovery.Receipt=3;RebirthCamperStationRefundRecovery.ApplyReady=true;RebirthCamperStationRefundRecovery.Continue=true;RebirthCamperStationRefundArchiveService.Saved=true;RebirthCamperStationWindowSession.Lease=new object();
        ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();ModEvents.GameUpdate.Tick();
        Check(RebirthCamperStationRefundRecovery.Applications==1,"guarded persisted prepared3 original application exactly once before saved archival");
        RebirthWorldCharacterService.Owner=new Owner();Add("remote",1);RebirthCamperStationRefundRecovery.Receipt=3;RebirthCamperStationRefundRecovery.ApplyReady=false;RebirthCamperStationRefundRecovery.Continue=true;RebirthCamperStationRefundArchiveService.Saved=false;RebirthCamperStationWindowSession.Lease=new object();var remote=new EntityPlayer();begins=RebirthCamperStationRefundRecovery.Begins;Check(!RebirthCamperStationRefundRecoveryWatch.TryObserveRemote(remote,"remote"),"remote prepared observation waits");Check(!RebirthCamperStationRefundRecoveryWatch.TryObserveRemote(remote,"remote")&&RebirthCamperStationRefundRecovery.Begins==begins+1,"remote retained original begin not repeated");RebirthCamperStationRefundRecovery.ApplyReady=true;RebirthCamperStationRefundArchiveService.Saved=true;RebirthCamperStationRefundArchiveService.Flush=false;Check(!RebirthCamperStationRefundRecoveryWatch.TryObserveRemote(remote,"remote")&&RebirthWorldCharacterService.Owner.Progression.StationRefundArchives.ContainsKey("remote"),"remote failed flush retains archive");RebirthCamperStationRefundArchiveService.Flush=true;Check(RebirthCamperStationRefundRecoveryWatch.TryObserveRemote(remote,"remote"),"remote terminal history retries after active records retired");
        RebirthWorldCharacterService.Owner=new Owner();Add("remote-owned",1);RebirthCamperStationRemoteCancellationWatch.Watching=true;begins=RebirthCamperStationRefundRecovery.Begins;Check(!RebirthCamperStationRefundRecoveryWatch.TryObserveRemote(remote,"remote-owned")&&RebirthCamperStationRefundRecovery.Begins==begins,"remote original watch excluded");RebirthCamperStationRemoteCancellationWatch.Watching=false;RebirthCamperRemoteStationTransport.Valid=false;Check(!RebirthCamperStationRefundRecoveryWatch.TryObserveRemote(remote,"remote-owned")&&RebirthCamperStationRefundRecovery.Begins==begins,"unauthenticated remote poll denied");
        Console.WriteLine("PASS mobile refund cold watcher actual-source fixture");
    }
}
