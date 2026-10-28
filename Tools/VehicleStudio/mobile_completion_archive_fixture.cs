// Actual production archive operation; native, codec and primary-file adapters doubled.
public static class ThreadManager { public static bool IsMainThread()=>true; }
public class ItemStack { public ItemStack Clone()=>new ItemStack(); }
public class Recipe { public string Job; }
public class RecipeQueueItem { public Recipe Recipe; }
public class Buffs { public Dictionary<string,float> CVars=new Dictionary<string,float>{{"native",1}}; }
public class EntityPlayer { public World world;public int entityId=1;public bool IsDead()=>false;public Buffs Buffs=new Buffs(); }
public class EntityPlayerLocal:EntityPlayer {}
public class World { public EntityPlayer Player;public EntityPlayer GetPrimaryPlayer()=>Player;public bool IsRemote()=>false;public EntityPlayer GetEntity(int id)=>Player?.entityId==id?Player:null; }
public class Vehicle { public int entityId=4; }
public class GameManager { public static GameManager Instance=new GameManager();public World World;public Id getPersistentPlayerID(object value)=>new Id();public class Id { public string CombinedString="canonical"; } }
public static class GameIO { public static string GetSaveGameDir()=>"save"; }
public class RebirthStablePlayerIdentity { public string StorageKey="owner",CanonicalId="canonical"; }
public struct RebirthCamperStationAddress { public int Installation;public int Vehicle; }
public class RebirthCamperStationState { public Vehicle BoundVehicle=new Vehicle();public RecipeQueueItem[] Queue=new RecipeQueueItem[0];public List<string> CraftCompleteList=new List<string>(); }
public class RebirthCamperModuleRuntime { public static RebirthCamperModuleRuntime For(Vehicle vehicle)=>new RebirthCamperModuleRuntime();public Installations Stations=new Installations();public class Installations { public Installation Find(int id)=>new Installation(); }public class Installation { public ItemStack InstalledItem=new ItemStack(); } }
public class Record { protected XElement image;public Record(string name){image=new XElement(name);}public Record(XElement node){image=new XElement(node);}public XElement Write()=>new XElement(image); }
public class RebirthStationGridAdmission:Record { public const string Job="0123456789abcdef0123456789abcdef";public string JobId=>Job;public string CreationId="creation";public RebirthStationGridAdmission():base("stationAdmission"){}public RebirthStationGridAdmission Clone()=>new RebirthStationGridAdmission();public bool TryGetMobileAddress(out RebirthCamperStationAddress address){address=new RebirthCamperStationAddress{Installation=1,Vehicle=2};return true;}public static bool TryReadStored(XElement node,out RebirthStationGridAdmission value){value=new RebirthStationGridAdmission();return node!=null;} }
public class RebirthCamperStationPublicationRecord:Record { public RebirthCamperStationPublicationRecord():base("camperStationPublication"){image.SetAttributeValue("vehicleClass",9);}public RebirthCamperStationPublicationRecord Clone()=>new RebirthCamperStationPublicationRecord();public bool MatchesSaveScope(string value)=>value=="save";public static bool TryRead(XElement node,RebirthStationGridAdmission a,out RebirthCamperStationPublicationRecord value){value=new RebirthCamperStationPublicationRecord();return node!=null;} }
public class RebirthCamperStationCompletionRecord:Record { public RebirthCamperStationCompletionRecord():base("camperStationCompletion"){}public RebirthCamperStationCompletionRecord Clone()=>new RebirthCamperStationCompletionRecord();public static bool TryRead(XElement node,RebirthStationGridAdmission a,RebirthCamperStationPublicationRecord q,out RebirthCamperStationCompletionRecord value){value=new RebirthCamperStationCompletionRecord();return node!=null;} }
public class RebirthCamperStationTrainingRecord:Record { public string Receipt="award";public static bool HasTraining=true;public RebirthCamperStationTrainingRecord():base("camperStationTraining"){}public RebirthCamperStationTrainingRecord Clone()=>new RebirthCamperStationTrainingRecord();public Inputs RestoreInputs()=>new Inputs();public class Inputs{public bool HasTraining=>RebirthCamperStationTrainingRecord.HasTraining;}public static bool TryRead(XElement node,RebirthStationGridAdmission a,RebirthCamperStationPublicationRecord q,RebirthCamperStationCompletionRecord c,out RebirthCamperStationTrainingRecord value){value=new RebirthCamperStationTrainingRecord();return node!=null;} }
public class RebirthStationTerminalIntent:Record { public bool IsCompletion=true;public RebirthStationTerminalIntent():base("stationTerminalIntent"){}public RebirthStationTerminalIntent Clone()=>new RebirthStationTerminalIntent();public static bool TryRead(XElement node,RebirthStationGridAdmission a,out RebirthStationTerminalIntent value){value=new RebirthStationTerminalIntent();return node!=null;} }
public class RebirthStationRefundArchive:Record {
    public static bool Capacity=true;public bool IsCompletion=true;public RebirthStationRefundArchive(XElement node):base(node){}public RebirthStationRefundArchive Clone()=>new RebirthStationRefundArchive(Write());
    public static bool TryRead(XElement node,string owner,out RebirthStationRefundArchive value){value=new RebirthStationRefundArchive(node);return true;}
    public static bool TryCreate(string owner,RebirthStationGridAdmission a,RebirthCamperStationPublicationRecord q,RebirthCamperStationCompletionRecord c,RebirthCamperStationTrainingRecord t,RebirthStationTerminalIntent i,out RebirthStationRefundArchive value){value=new RebirthStationRefundArchive(new XElement("stationRefundArchive",a.Write(),q.Write(),c.Write(),t.Write(),i.Write()));return true;}
    public static XElement WriteAll(Dictionary<string,RebirthStationRefundArchive> records,string owner){if(!Capacity)throw new Exception("capacity");return new XElement("archives");}
}
public class RebirthWorldCharacterRecord { public State Progression=new State(); }
public class State {
    public Dictionary<string,RebirthStationGridAdmission> StationPreparations=new Dictionary<string,RebirthStationGridAdmission>();
    public Dictionary<string,RebirthCamperStationPublicationRecord> CamperStationPublications=new Dictionary<string,RebirthCamperStationPublicationRecord>();
    public Dictionary<string,RebirthCamperStationCompletionRecord> CamperStationCompletions=new Dictionary<string,RebirthCamperStationCompletionRecord>();
    public Dictionary<string,RebirthCamperStationTrainingRecord> CamperStationTrainingPlans=new Dictionary<string,RebirthCamperStationTrainingRecord>();
    public Dictionary<string,RebirthStationTerminalIntent> StationTerminalIntents=new Dictionary<string,RebirthStationTerminalIntent>();
    public Dictionary<string,RebirthStationRefundArchive> StationRefundArchives=new Dictionary<string,RebirthStationRefundArchive>();
    public Dictionary<string,object> StationPublications=new Dictionary<string,object>(),StationCancellationAttempts=new Dictionary<string,object>(),StationCancellationRefunds=new Dictionary<string,object>(),StationCancellationPublications=new Dictionary<string,object>(),StationCompletionPublications=new Dictionary<string,object>(),StationCompletionExpectationProjections=new Dictionary<string,object>(),CamperStationCancellations=new Dictionary<string,object>(),CamperStationRefundDeliveries=new Dictionary<string,object>(),StationRefundDeliveries=new Dictionary<string,object>();
    public HashSet<string> SkillAwardReceipts=new HashSet<string>{"award"};
}
public static class RebirthWorldCharacterService { public static RebirthWorldCharacterRecord Owner=new RebirthWorldCharacterRecord();public static bool Flush;public static int Flushes;public static bool TryGet(EntityPlayer player,out RebirthWorldCharacterRecord owner){owner=Owner;return true;}public static bool TryGetIdentity(EntityPlayer player,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}public static void MarkDirty(RebirthWorldCharacterRecord owner,string reason){}public static bool FlushPlayer(EntityPlayer player,string reason){Flushes++;return Flush;} }
public static class RebirthWorldCharacterRepository { public static bool TrainingSaved=true,IntentSaved=true,ArchiveSaved;public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord owner)=>ReferenceEquals(owner,RebirthWorldCharacterService.Owner);public static bool HasSavedCamperStationTrainingPlan(RebirthStablePlayerIdentity identity,RebirthStationGridAdmission a,RebirthCamperStationPublicationRecord q,RebirthCamperStationCompletionRecord c,RebirthCamperStationTrainingRecord t,bool award)=>TrainingSaved;public static bool HasSavedStationTerminalIntent(RebirthStablePlayerIdentity identity,RebirthStationGridAdmission a,RebirthStationTerminalIntent i)=>IntentSaved;public static bool HasSavedStationRefundArchive(RebirthStablePlayerIdentity identity,RebirthStationRefundArchive a)=>ArchiveSaved; }
public static class RebirthCamperStationWindowSession { public static object Lease=new object();public static RebirthCamperStationState Station=new RebirthCamperStationState();public static bool TryGetCurrent(EntityPlayer player,out RebirthCamperStationState station,out RebirthCamperStationAddress address){station=Station;address=new RebirthCamperStationAddress{Installation=1,Vehicle=2};return true;}public static bool TryGetLeaseIdentity(EntityPlayer player,out object lease){lease=Lease;return true;} }
public static class RebirthStationLiveAccess { public static bool TryGetMobileLease(EntityPlayer player,RebirthCamperStationAddress address,string creation,out RebirthCamperStationState station,out object lease){station=RebirthCamperStationWindowSession.Station;lease=RebirthCamperStationWindowSession.Lease;return lease!=null;}public static bool TryResolveMobile(EntityPlayer player,RebirthCamperStationAddress address,string creation,out RebirthCamperStationState station,out RebirthWorldCharacterRecord owner){station=RebirthCamperStationWindowSession.Station;owner=RebirthWorldCharacterService.Owner;return true;} }
public static class RebirthCharacterCreationHoldService { public static bool IsHeld(EntityPlayer player)=>false; }
public static class RebirthCamperVehicleIdentity { public static bool IsUniqueCurrent(Vehicle vehicle,int address)=>true; }
public static class RebirthStationGridIngredients { public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>true; }
public static class RebirthCamperStationCompletionCallsite { public static bool IsHeld(RebirthCamperStationState station)=>false; }
public static class RebirthCamperStationNativeRewardReceipt { public static float Receipt=1;public static string Key(RebirthStationGridAdmission admission)=>"native";public static bool HasSaved(RebirthStablePlayerIdentity identity,RebirthStationGridAdmission a,bool applied)=>Receipt==1; }
public static class RebirthStationCompletionReceipt { public static bool HasNoJobReceipt(List<string> list,string job)=>!list.Contains(job); }
public static class RebirthStationGridQueue { public static bool IsMarked(Recipe recipe)=>recipe!=null;public static bool TryGetJobId(Recipe recipe,out string job){job=recipe?.Job;return job!=null;} }
public static class RebirthCamperStationRecoveryImage { public static string Live="retired";public static string Digest(RebirthCamperStationState state,ItemStack item)=>Live; }
public static class RebirthCamperStationSaveEvidence {
    public static bool Pending,Ready,ThrowBegin;public static int Begins,Disposals;public static bool IsPending(RebirthCamperStationState station)=>Pending;
    public class Request { public bool Closed;public void Dispose(){Disposals++;Closed=true;} }
    public class Publication { public RebirthCamperStationAddress Address=new RebirthCamperStationAddress{Installation=1,Vehicle=2};public int VehicleEntityId=4,VehicleClass=9;public string SaveDirectory="save";public RebirthCamperStationState RestoreStationSnapshot()=>new RebirthCamperStationState();public string RecoveryDigest()=>"retired"; }
    public static Request BeginRecovery(EntityPlayer player,RebirthCamperStationState station,RebirthCamperStationAddress address,Func<bool> current){Begins++;if(ThrowBegin)throw new Exception("native begin");return new Request();}
    public static bool TryObserve(Request request,out Publication publication){publication=Ready?new Publication():null;return Ready;}
}
public static class RebirthStationPreparationReservation { public static int Releases;public static bool TryReleaseSettled(World world,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission a,Func<bool> saved){if(!saved())return false;Releases++;return true;} }
public static class RebirthCamperStationQueuedAuthority { public static int Releases;public static bool TryReleaseArchived(RebirthCamperStationState station,RebirthWorldCharacterRecord owner,RebirthStablePlayerIdentity identity,RebirthStationRefundArchive archive,Func<bool> saved){if(!saved())return false;Releases++;return true;} }
public static class MobileCompletionArchiveFixture {
    private static void Check(bool value,string label){if(!value)throw new Exception(label);}
    private static EntityPlayerLocal Setup(){
        var player=new EntityPlayerLocal{world=new World()};player.world.Player=player;GameManager.Instance.World=player.world;
        RebirthWorldCharacterService.Owner=new RebirthWorldCharacterRecord();var s=RebirthWorldCharacterService.Owner.Progression;var job=RebirthStationGridAdmission.Job;
        s.StationPreparations[job]=new RebirthStationGridAdmission();s.CamperStationPublications[job]=new RebirthCamperStationPublicationRecord();s.CamperStationCompletions[job]=new RebirthCamperStationCompletionRecord();s.CamperStationTrainingPlans[job]=new RebirthCamperStationTrainingRecord();s.StationTerminalIntents[job]=new RebirthStationTerminalIntent();return player;
    }
    public static void Run(){
        var player=Setup();string job=RebirthStationGridAdmission.Job;
        RebirthStationRefundArchive.Capacity=false;Check(!RebirthCamperStationCompletionArchive.TryBegin(player,job,out var op)&&op==null,"capacity before native side effects");RebirthStationRefundArchive.Capacity=true;
        RebirthCamperStationNativeRewardReceipt.Receipt=2;Check(!RebirthCamperStationCompletionArchive.TryBegin(player,job,out op),"uncertain reward receipt refused");RebirthCamperStationNativeRewardReceipt.Receipt=1;
        RebirthWorldCharacterRepository.TrainingSaved=false;Check(!RebirthCamperStationCompletionArchive.TryBegin(player,job,out op),"saved award/training required");RebirthWorldCharacterRepository.TrainingSaved=true;
        RebirthWorldCharacterService.Owner.Progression.SkillAwardReceipts.Clear();Check(!RebirthCamperStationCompletionArchive.TryBegin(player,job,out op),"live award receipt required");RebirthWorldCharacterService.Owner.Progression.SkillAwardReceipts.Add("award");
        RebirthCamperStationWindowSession.Station.CraftCompleteList.Add(job);Check(!RebirthCamperStationCompletionArchive.TryBegin(player,job,out op),"retire native receipt first");RebirthCamperStationWindowSession.Station.CraftCompleteList.Clear();
        RebirthCamperStationSaveEvidence.ThrowBegin=true;Check(!RebirthCamperStationCompletionArchive.TryBegin(player,job,out op)&&op!=null,"retain original before native begin failure");RebirthCamperStationSaveEvidence.ThrowBegin=false;
        Check(!op.TryArchive()&&RebirthWorldCharacterService.Owner.Progression.StationPreparations.ContainsKey(job),"fresh native save precedes owner retirement");
        RebirthCamperStationSaveEvidence.Ready=true;Check(!op.TryArchive(),"failed flush retains archive");
        var s=RebirthWorldCharacterService.Owner.Progression;Check(s.StationRefundArchives.ContainsKey(job)&&!s.StationPreparations.ContainsKey(job)&&s.CamperStationTrainingPlans.Count==0,"five active parents migrated once");
        Check(RebirthStationPreparationReservation.Releases==0&&RebirthCamperStationQueuedAuthority.Releases==0,"no claims released before primary archive confirmation");
        RebirthCamperStationRecoveryImage.Live="unrelated output fuel advanced";
        RebirthCamperStationWindowSession.Station.Queue=new[]{new RecipeQueueItem{Recipe=new Recipe{Job="another-job"}}};
        Check(op.CanContinue,"unrelated native output/fuel/queue progression allowed after proof");
        RebirthCamperStationWindowSession.Station.Queue=new[]{new RecipeQueueItem{Recipe=new Recipe{Job=job}}};
        Check(!op.CanContinue&&!op.TryArchive(),"target job reappearance denied");
        RebirthCamperStationWindowSession.Station.Queue=new[]{new RecipeQueueItem{Recipe=new Recipe{Job="another-job"}}};
        RebirthWorldCharacterService.Flush=true;Check(!op.TryArchive(),"flush alone insufficient");RebirthWorldCharacterRepository.ArchiveSaved=true;Check(op.TryArchive(),"primary archive confirms exact claim and binding release");
        Check(RebirthStationPreparationReservation.Releases==1&&RebirthCamperStationQueuedAuthority.Releases==1,"each terminal release once");Check(op.TryArchive(),"settled original idempotent");op.Dispose();
        Check(RebirthCamperStationCompletionArchive.TryBegin(player,job,out var cold),"cached archive cold retry");RebirthCamperStationWindowSession.Lease=new object();Check(!cold.CanContinue&&!cold.TryArchive(),"replacement lease cannot authorize original");cold.Dispose();
        var remote=new EntityPlayer{world=player.world};remote.world.Player=remote;
        Check(RebirthCamperStationCompletionArchive.TryBegin(remote,job,out var remoteArchive),"server remote cached history admitted without host primaryplayer identity");
        Check(remoteArchive.TryArchive(),"remote exact lease/native receipt/history confirms claims without rewards");remoteArchive.Dispose();
        Console.WriteLine("PASS actual mobile completion archive source fixture");
    }
}
