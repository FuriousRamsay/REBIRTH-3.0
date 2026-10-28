using System;
// AUTHORED_NOT_RUN. Actual send/receive scope predicate; explicit native adapters.
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class World {internal bool Remote=true;internal EntityPlayerLocal Primary,Registered;internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Primary;internal object GetEntity(int id)=>Registered;}
internal sealed class EntityPlayerLocal {internal World world;internal int entityId=1,Died;internal bool Dead;internal string Creation="creation",Owner="owner";internal bool IsDead()=>Dead;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayerLocal actor)=>actor.Creation;}
internal sealed class RebirthStablePlayerIdentity {internal string CanonicalId;internal static bool TryResolveServerEntity(EntityPlayerLocal actor,out RebirthStablePlayerIdentity owner){owner=new RebirthStablePlayerIdentity{CanonicalId=actor.Owner};return actor.Owner!=null;}}
internal static class RebirthCookingPull {
 internal sealed class Original {internal EntityPlayerLocal Player;internal World World;internal string Owner,Creation;internal int Deaths=-1;internal bool MetadataOnly;}
// ACTUAL_PULL_SESSION
 internal static bool Current(Original original)=>CurrentPullSession(original);
 internal static bool Delivery(Original original)=>CurrentDeliverySession(original);
}
internal static class Program {
 static void Need(bool b,string m){if(!b)throw new Exception(m);}
 public static void Main(){
  for(int variant=0;variant<9;variant++){
   ThreadManager.Main=true;var world=new World();var actor=new EntityPlayerLocal{world=world};world.Primary=world.Registered=actor;GameManager.Instance.World=world;
   var original=new RebirthCookingPull.Original{Player=actor,World=world,Owner=actor.Owner,Creation=actor.Creation};
   Need(RebirthCookingPull.Current(original),"valid retained original refused");
   if(variant==0)actor.Owner="other";if(variant==1)actor.Creation="other";if(variant==2)actor.Dead=true;if(variant==3)world.Registered=new EntityPlayerLocal{world=world};
   if(variant==4)world.Primary=new EntityPlayerLocal{world=world};if(variant==5)GameManager.Instance.World=new World();if(variant==6)actor.world=new World();if(variant==7)world.Remote=false;if(variant==8)ThreadManager.Main=false;
   Need(RebirthCookingPull.Current(original)==(variant==7),"current local owner or stale original qualification differs "+variant);
  }
  var liveWorld=new World();var liveActor=new EntityPlayerLocal{world=liveWorld,Died=3};liveWorld.Primary=liveWorld.Registered=liveActor;GameManager.Instance.World=liveWorld;ThreadManager.Main=true;
  var liveOriginal=new RebirthCookingPull.Original{Player=liveActor,World=liveWorld,Owner=liveActor.Owner,Creation=liveActor.Creation,Deaths=3};
  Need(RebirthCookingPull.Delivery(liveOriginal),"same admitted life must permit delivery scope");
  liveActor.Died++;Need(RebirthCookingPull.Current(liveOriginal)&&!RebirthCookingPull.Delivery(liveOriginal),"respawn permits metadata reconciliation but cannot borrow original UI delivery");
  liveOriginal.Deaths=-1;Need(!RebirthCookingPull.Delivery(liveOriginal),"legacy unknown life cannot authorize native delivery");
  Console.WriteLine("PASS actual cooking pull metadata/delivery life predicates with native identity/world doubles; no transfer/persistence evidence");
 }
}
