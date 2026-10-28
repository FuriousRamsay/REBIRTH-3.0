// AUTHORED_NOT_RUN. Entire production dispatcher; explicit durable/native adapters.
// ACTUAL_DISPATCHER
internal class Entity {internal World world;internal int entityId;}
internal sealed class EntityRebirthNPC:Entity {
 internal bool Dead,Unload,Release=true;internal int Releases;internal bool IsDead()=>Dead;internal bool IsMarkedForUnload()=>Unload;
 internal bool TryReleasePreparedRestoration(string replay){Releases++;return Release;}
}
internal sealed class World {internal bool Remote;internal Entity Body;internal bool IsRemote()=>Remote;internal Entity GetEntity(int id)=>Body!=null&&Body.entityId==id?Body:null;}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal struct RebirthNpcStableId {
 internal int Value;internal static bool TryParse(string text,out RebirthNpcStableId id){id=new RebirthNpcStableId();if(text!="original")return false;id.Value=1;return true;}
}
internal sealed class Request {internal string StableId="original",Profile="human";internal bool IsAttempted=true,IsConstructed=true;internal int NativeEntityId=1;}
internal sealed class Completion {internal RebirthNpcStableId Stable;internal int NativeId=1;}
internal sealed class Person {}
internal static class RebirthNpcAggregatePersistenceStore {internal static bool Found=true;internal static bool TryGet(RebirthNpcStableId stable,out Person person){person=new Person();return Found;}}
internal static class RebirthNpcPreparedPersonAdmission {internal static bool Valid=true;internal static bool TryValidate(Person person,out string profile,out string reason,bool observedLive){profile="human";reason="adapter";return Valid;}}
internal static class RebirthNpcWorldIntegrationService {
 internal static Request Pending;internal static Completion Completed;internal static bool Complete=true;internal static int Calls;internal static System.Action DuringComplete;
 internal static bool TryGetPreparedRequest(string replay,out Request request){request=Pending;return request!=null;}
 internal static bool TryGetCompletedBinding(string replay,out Completion completion){completion=Completed;return completion!=null;}
 internal static bool TryCompletePublishedExistingPerson(string replay,World world,Entity candidate,out string reason){Calls++;DuringComplete?.Invoke();reason="adapter";return Complete;}
}
internal static class Program {
 private static void Need(bool value,string message){if(!value)throw new System.Exception(message);}
 private static bool Run()=>RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out _,out _);
 public static void Main(){
  var world=new World();var actor=new EntityRebirthNPC{world=world,entityId=1};world.Body=actor;GameManager.Instance.World=world;
  RebirthNpcWorldIntegrationService.Pending=new Request();
  ThreadManager.Main=false;Need(!Run()&&actor.Releases==0,"worker released");ThreadManager.Main=true;
  world.Remote=true;Need(!Run(),"remote released");world.Remote=false;
  var request=RebirthNpcWorldIntegrationService.Pending;request.IsAttempted=false;Need(!Run(),"fresh reconstruction admitted");request.IsAttempted=true;request.IsConstructed=false;Need(!Run(),"uncertain construction replayed");request.IsConstructed=true;
  RebirthNpcWorldIntegrationService.Complete=false;Need(!Run()&&actor.Releases==0,"unsaved completion released");
  RebirthNpcWorldIntegrationService.Complete=true;actor.Release=false;Need(!Run()&&actor.Releases==1,"held native release reported complete");actor.Release=true;Need(Run()&&actor.Releases==2,"exact original release retry missing");
  actor.Dead=true;Need(!Run()&&actor.Releases==2,"dead body released");actor.Dead=false;actor.Unload=true;Need(!Run(),"unloading body released");actor.Unload=false;
  world.Body=new Entity{world=world,entityId=1};Need(!Run(),"non-NPC rebound body released");world.Body=actor;
  RebirthNpcWorldIntegrationService.Pending=null;RebirthNpcWorldIntegrationService.Completed=new Completion();
  Need(Run()&&actor.Releases==3,"saved completed original skipped native release");
  RebirthNpcWorldIntegrationService.Complete=false;Need(!Run()&&actor.Releases==3,"completed key bypassed positive current saved proof");
  System.Console.WriteLine("PASS actual reconciliation-to-release dispatcher; native release/durable completion integration NOT RUN");
 }
}
