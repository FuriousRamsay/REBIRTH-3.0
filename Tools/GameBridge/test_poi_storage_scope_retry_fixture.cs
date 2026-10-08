using System;
using System.Collections;
using System.Collections.Generic;
struct Vector3i {public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
class World {public EntityPlayerLocal Primary;public EntityPlayerLocal GetPrimaryPlayer(){return Primary;}} class GameManager {public static GameManager Instance=new GameManager();public World World;}
class PrefabInstance {public int id=1;public Vector3i boundingBoxPosition=new Vector3i(1,2,3),boundingBoxSize=new Vector3i(4,5,6);}
class EntityPlayerLocal {public World world;public bool Dead;public bool IsDead(){return Dead;}}
static class RebirthGameBridgeCombat {public static int Threats;public static List<int> AwakeThreats(EntityPlayerLocal p,float radius,bool unused){var list=new List<int>();for(int i=0;i<Threats;i++)list.Add(i);return list;}}
class Storage {
 private World custodyWorld; private PrefabInstance boundPoi;
 public string Failure {get;private set;} public bool RetryAfterThreat {get;private set;}
 // PRODUCTION_CLASS
 public void Bind(World world,PrefabInstance poi){custodyWorld=world;boundPoi=poi;}
 public string Context(EntityPlayerLocal player,PrefabInstance poi,World world,int id,Vector3i origin,Vector3i size){return TripContextFailure(player,poi,world,id,origin,size);}
 public bool Threat(EntityPlayerLocal player){return ThreatInterrupt(player,14f,"threat");}
}
class FakeStorage {public bool RetryAfterThreat;public string Failure;}
class BranchCheck {
 // PRODUCTION_BRANCHES
}
class Checks {
 static void A(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
 var world=new World();var poi=new PrefabInstance();var p=new EntityPlayerLocal{world=world};var s=new Storage();s.Bind(world,poi);GameManager.Instance.World=world;world.Primary=p;int id=poi.id;var origin=poi.boundingBoxPosition;var size=poi.boundingBoxSize;
 A(s.Context(p,poi,world,id,origin,size)==null,"original bound scope");p.world=new World();A(s.Context(p,poi,world,id,origin,size)!=null,"same player replacement world");p.world=world;poi.id++;A(s.Context(p,poi,world,id,origin,size)!=null,"same POI changed id");poi.id=id;poi.boundingBoxPosition=new Vector3i(9,2,3);A(s.Context(p,poi,world,id,origin,size)!=null,"same POI changed origin");poi.boundingBoxPosition=origin;poi.boundingBoxSize=new Vector3i(9,5,6);A(s.Context(p,poi,world,id,origin,size)!=null,"same POI changed size");poi.boundingBoxSize=size;s.Bind(world,new PrefabInstance());A(s.Context(p,poi,world,id,origin,size)!=null,"rebound storage refuses old object");s.Bind(world,poi);p.Dead=true;A(s.Context(p,poi,world,id,origin,size)!=null,"dead trip refuses");p.Dead=false;
 GameManager.Instance.World=new World();A(s.Context(p,poi,world,id,origin,size)!=null,"retained old world refuses native world replacement");RebirthGameBridgeCombat.Threats=1;A(!s.Threat(p)&&!s.RetryAfterThreat,"native world replacement never enables retry");GameManager.Instance.World=world;world.Primary=new EntityPlayerLocal{world=world};A(s.Context(p,poi,world,id,origin,size)!=null,"retained old player refuses native primary replacement");A(!s.Threat(p)&&!s.RetryAfterThreat,"native primary replacement never enables retry");world.Primary=p;
 RebirthGameBridgeCombat.Threats=0;A(!s.Threat(p)&&!s.RetryAfterThreat,"absence of threat never enables retry");p.Dead=true;RebirthGameBridgeCombat.Threats=1;A(!s.Threat(p)&&!s.RetryAfterThreat,"dead player never enables retry");p.Dead=false;p.world=new World();A(!s.Threat(p)&&!s.RetryAfterThreat,"changed world never enables retry");p.world=world;A(s.Threat(p)&&s.RetryAfterThreat&&s.Failure=="threat","actual awake refusal enables retry");
 foreach(bool second in new[]{false,true}) {A(BranchCheck.Run(second,true)=="fatal-after-budget:4","only three continuation retries then failure");A(BranchCheck.Run(second,false)=="fatal-after-budget:0","fatal failure never consumes retry");}
 Console.WriteLine("PASS 19 actual trip-context/threat predicates and both actual World storage-failure branches: replacement world/POI binding, no/dead/world threat refusal, three bounded retry continuations and fatal precedence. Native world/combat/coroutine services doubled.");
 }
}
