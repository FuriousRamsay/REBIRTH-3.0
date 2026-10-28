using System;
// AUTHORED_NOT_RUN. Actual resume service; native restoration/persistence adapters.
internal class Entity {internal World world;internal int entityId=1;}
internal sealed class EntityRebirthHumanoidNPC:Entity {internal bool IsPreparedRestorationPending=true,Ready=true;internal bool CanPublishPreparedRestoration()=>Ready;}
internal sealed class World {internal bool Remote;internal Entity Body;internal bool IsRemote()=>Remote;internal Entity GetEntity(int id)=>Body;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal struct RebirthNpcStableId {internal static bool TryParse(string text,out RebirthNpcStableId stable){stable=new RebirthNpcStableId();return text=="original";}}
internal sealed class Request {internal bool IsPublishing;internal string StableId="original";}
internal static class RebirthNpcAggregatePersistenceStore {internal static bool Found=true;internal static bool TryGet(RebirthNpcStableId stable,out object person){person=new object();return Found;}}
internal static class RebirthNpcNativeReconstructionRestore {
 internal static bool Allow=true;internal static int Calls;internal static Action During;
 internal static bool TryRestore(EntityRebirthHumanoidNPC actor,object person,out string reason){Calls++;During?.Invoke();reason="adapter";return Allow;}
}
internal static class RebirthNpcWorldIntegrationService {
 internal static Request Original=new Request();internal static bool Confirm=true;internal static int ConfirmCalls;
 internal static bool TryConfirmConstructedSpawn(string replay,Entity candidate,out Request request){ConfirmCalls++;request=Original;return Confirm;}
// ACTUAL_CONSTRUCTED_RESTORE
}
internal static class Program {
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static EntityRebirthHumanoidNPC Fresh(){
  var world=new World();GameManager.Instance.World=world;ThreadManager.Main=true;
  RebirthNpcWorldIntegrationService.Confirm=true;RebirthNpcWorldIntegrationService.Original=new Request();RebirthNpcWorldIntegrationService.ConfirmCalls=0;
  RebirthNpcAggregatePersistenceStore.Found=true;RebirthNpcNativeReconstructionRestore.Allow=true;RebirthNpcNativeReconstructionRestore.Calls=0;RebirthNpcNativeReconstructionRestore.During=null;
  return new EntityRebirthHumanoidNPC{world=world};
 }
 private static bool Run(Entity candidate)=>RebirthNpcWorldIntegrationService.TryRestoreConstructedSpawn("original",candidate,out _);
 public static void Main(){
  var actor=Fresh();ThreadManager.Main=false;Need(!Run(actor)&&RebirthNpcWorldIntegrationService.ConfirmCalls==0,"worker touched saved intent");
  actor=Fresh();GameManager.Instance.World=new World();Need(!Run(actor),"old world admitted");
  actor=Fresh();actor.world.Remote=true;Need(!Run(actor),"remote restored");
  actor=Fresh();actor.world.Body=actor;Need(!Run(actor)&&RebirthNpcWorldIntegrationService.ConfirmCalls==0,"already published candidate restored offworld");
  actor=Fresh();actor.IsPreparedRestorationPending=false;Need(!Run(actor),"unheld actor restored");
  actor=Fresh();RebirthNpcWorldIntegrationService.Confirm=false;Need(!Run(actor)&&RebirthNpcNativeReconstructionRestore.Calls==0,"unconfirmed saved construction restored");
  actor=Fresh();RebirthNpcWorldIntegrationService.Original.IsPublishing=true;Need(!Run(actor)&&RebirthNpcNativeReconstructionRestore.Calls==0,"uncertain publication restored");
  actor=Fresh();RebirthNpcWorldIntegrationService.Original.StableId="foreign";Need(!Run(actor),"invalid original identity restored");
  actor=Fresh();RebirthNpcAggregatePersistenceStore.Found=false;Need(!Run(actor),"missing aggregate restored");
  actor=Fresh();RebirthNpcNativeReconstructionRestore.Allow=false;Need(!Run(actor)&&RebirthNpcNativeReconstructionRestore.Calls==1,"failed native restore reported complete");
  RebirthNpcNativeReconstructionRestore.Allow=true;Need(Run(actor)&&RebirthNpcNativeReconstructionRestore.Calls==2,"same candidate resume unavailable");
  actor=Fresh();RebirthNpcNativeReconstructionRestore.During=()=>GameManager.Instance.World=new World();Need(!Run(actor),"world changed inside native restore accepted");
  actor=Fresh();var original=actor;RebirthNpcNativeReconstructionRestore.During=()=>original.world.Body=original;Need(!Run(actor),"native publication during restore accepted");
  actor=Fresh();actor.Ready=false;Need(!Run(actor),"native result without independent publication qualification accepted");
  Console.WriteLine("PASS actual constructed-original restore admission; EntityFactory/native hydration/durable file NOT RUN");
 }
}
