using System;
// AUTHORED_NOT_RUN. Actual release method, explicit native/save/equipment adapters.
internal sealed class World {internal bool Remote;internal EntityRebirthNPC Body;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Body!=null&&Body.entityId==id?Body:null;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal enum RebirthNpcPresenceState {Active,Suspended}
internal sealed class Runtime {internal string StableId="original",ProfileId="human";internal bool PreparedRestorationPending=true;}
internal class EntityRebirthNPC {
 internal World world;internal int entityId=1;internal bool Dead,Unload,hasAI,rebirthPreparedRestorationPending=true,Qualified=true;
 internal uint rebirthRestoredGeneration=7;internal Runtime RebirthRuntimeState=new Runtime();internal Action Refresh,Broadcast;
 internal bool IsDead()=>Dead;internal bool IsMarkedForUnload()=>Unload;
 internal bool CanPublishPreparedRestoration()=>Qualified&&ReferenceEquals(world,GameManager.Instance.World);
 internal void BroadcastRebirthState()=>Broadcast?.Invoke();
// ACTUAL_NATIVE_RELEASE
}
internal sealed class EntityRebirthHumanoidNPC:EntityRebirthNPC {internal void RefreshRebirthModelEquipment()=>Refresh?.Invoke();}
internal sealed class Binding {internal string Stable="original",Profile="human";internal int NativeId=1;internal uint Generation=7;}
internal static class RebirthNpcWorldIntegrationService {internal static bool Found=true;internal static Binding Completion=new Binding();internal static bool TryGetCompletedBinding(string replay,out Binding binding){binding=Completion;return Found;}}
internal sealed class Result {internal bool Succeeded;}
internal static class RebirthNpcTransactions {
 internal static bool Allow=true;internal static RebirthNpcPresenceState Last;internal static Result SetPresence(Runtime state,RebirthNpcPresenceState presence){Last=presence;return new Result{Succeeded=Allow};}
}
internal static class RebirthNpcAggregatePersistenceStore {internal static bool Found=true;internal static bool TryGet(string stable,out object person){person=new object();return Found;}}
internal static class RebirthNpcPreparedHandMaterialization {internal static bool Ready=true;internal static bool IsReady(EntityRebirthHumanoidNPC actor,object person)=>Ready;}
internal sealed class ConnectionManager {internal bool IsServer=true;}
internal static class SingletonMonoBehaviour<T> where T:new() {internal static T Instance=new T();}
internal static class RebirthNpcPersistenceCoordinator {internal static bool IsRecoveryInstalling;}
internal static class RebirthNpcLiveRecoveryRuntime {internal static void DeferNativeActivation(EntityRebirthNPC actor){}}
internal static class RebirthNpcLifecycle {internal static int Activations;internal static void OnEntityActivated(int id){Activations++;}}
internal static class RebirthNpcLifecycleQualificationService {internal static void ObserveEntityActivated(int id,string stable){}}
internal static class RebirthNpcPreparedPhysicalHold {
 internal static bool Release=true;internal static int Releases,Suspends;internal static bool TryRelease(EntityRebirthHumanoidNPC actor,uint generation){Releases++;return Release;}
 internal static void TrySuspendAgain(EntityRebirthHumanoidNPC actor){Suspends++;actor.hasAI=false;}
}
internal static class RebirthNpcPreparedSchedulerAdmission {internal static bool Allow=true;internal static int Scheduled;internal static bool TrySchedule(EntityRebirthNPC actor,string replay,uint generation){Scheduled++;return Allow;}}
internal static class Program {
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static EntityRebirthHumanoidNPC Fresh(){
  ThreadManager.Main=true;var world=new World();var actor=new EntityRebirthHumanoidNPC{world=world};world.Body=actor;GameManager.Instance.World=world;
  RebirthNpcWorldIntegrationService.Found=true;RebirthNpcWorldIntegrationService.Completion=new Binding();RebirthNpcTransactions.Allow=true;
  RebirthNpcAggregatePersistenceStore.Found=true;RebirthNpcPreparedHandMaterialization.Ready=true;RebirthNpcPreparedPhysicalHold.Release=true;
  RebirthNpcPreparedPhysicalHold.Releases=RebirthNpcPreparedPhysicalHold.Suspends=RebirthNpcPreparedSchedulerAdmission.Scheduled=0;
  RebirthNpcPreparedSchedulerAdmission.Allow=true;return actor;
 }
 private static void Retained(EntityRebirthHumanoidNPC actor,string label){Need(actor.rebirthPreparedRestorationPending&&actor.RebirthRuntimeState.PreparedRestorationPending&&!actor.hasAI&&RebirthNpcPreparedPhysicalHold.Suspends==1,label);}
 public static void Main(){
  var actor=Fresh();ThreadManager.Main=false;Need(!actor.TryReleasePreparedRestoration("original")&&RebirthNpcPreparedPhysicalHold.Releases==0,"worker release");
  actor=Fresh();actor.rebirthPreparedRestorationPending=false;GameManager.Instance.World=new World();Need(!actor.TryReleasePreparedRestoration("original"),"already released stale native actor accepted");
  actor=Fresh();RebirthNpcWorldIntegrationService.Found=false;Need(!actor.TryReleasePreparedRestoration("original")&&RebirthNpcPreparedPhysicalHold.Releases==0,"missing completion released");
  actor=Fresh();RebirthNpcWorldIntegrationService.Completion.Generation=8;Need(!actor.TryReleasePreparedRestoration("original"),"wrong generation released");
  actor=Fresh();var original=actor;actor.Refresh=()=>original.Qualified=false;Need(!actor.TryReleasePreparedRestoration("original")&&RebirthNpcPreparedPhysicalHold.Releases==0,"equipment changed qualification");Retained(actor,"equipment mismatch lost hold");
  actor=Fresh();original=actor;actor.Refresh=()=>original.world.Body=null;Need(!actor.TryReleasePreparedRestoration("original")&&RebirthNpcPreparedPhysicalHold.Releases==0,"equipment rebound native actor");Retained(actor,"rebound lost pending original");
  actor=Fresh();actor.Refresh=()=>{throw new NullReferenceException("native equipment callback");};Need(!actor.TryReleasePreparedRestoration("original"),"equipment exception escaped");Retained(actor,"non-filtered failure lost hold");
  actor=Fresh();RebirthNpcPreparedPhysicalHold.Release=false;Need(!actor.TryReleasePreparedRestoration("original"),"failed physical release accepted");Retained(actor,"physical refusal lost hold");
  actor=Fresh();actor.Broadcast=()=>{throw new NullReferenceException("transport callback");};Need(!actor.TryReleasePreparedRestoration("original"),"broadcast exception escaped");Retained(actor,"broadcast failure lost hold");
  actor=Fresh();RebirthNpcPreparedSchedulerAdmission.Allow=false;Need(!actor.TryReleasePreparedRestoration("original"),"failed scheduler reported release");Retained(actor,"scheduler failure lost hold");
  actor=Fresh();Need(actor.TryReleasePreparedRestoration("original")&&!actor.rebirthPreparedRestorationPending&&!actor.RebirthRuntimeState.PreparedRestorationPending&&!actor.hasAI&&RebirthNpcPreparedSchedulerAdmission.Scheduled==1,"positive release missing queued original");
  Need(actor.TryReleasePreparedRestoration("original")&&RebirthNpcPreparedSchedulerAdmission.Scheduled==1,"released retry scheduled again");
  Console.WriteLine("PASS actual native release control flow; real physics/equipment/durable journal NOT RUN");
 }
}
