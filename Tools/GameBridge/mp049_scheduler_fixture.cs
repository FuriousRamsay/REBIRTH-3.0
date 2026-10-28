// AUTHORED_NOT_RUN. Complete production scheduler; explicit native entity/thread adapters.
// ACTUAL_SCHEDULER
internal sealed class World {
 internal bool Remote;internal readonly System.Collections.Generic.Dictionary<int,EntityRebirthNPC> Bodies=new System.Collections.Generic.Dictionary<int,EntityRebirthNPC>();
 internal bool IsRemote()=>Remote;internal EntityRebirthNPC GetEntity(int id)=>Bodies.TryGetValue(id,out var actor)?actor:null;
}
internal sealed class EntityRebirthNPC {
 internal World world;internal int entityId,Calls;internal bool Dead,hasAI=true,Accept;internal System.Action During;
 internal bool IsDead()=>Dead;internal bool TryAdmitPreparedAi(string replay,uint generation){Calls++;During?.Invoke();return Accept;}
}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class Log {internal static void Warning(string message){}}
internal static class Program {
 private static void Need(bool value,string message){if(!value)throw new System.Exception(message);}
 private static EntityRebirthNPC Actor(World world,int id){var actor=new EntityRebirthNPC{world=world,entityId=id};world.Bodies[id]=actor;return actor;}
 public static void Main(){
  var world=new World();GameManager.Instance.World=world;var actor=Actor(world,1);
  ThreadManager.Main=false;Need(!RebirthNpcPreparedSchedulerAdmission.TrySchedule(actor,"original",1),"worker scheduled");ThreadManager.Main=true;
  GameManager.Instance.World=new World();Need(!RebirthNpcPreparedSchedulerAdmission.TrySchedule(actor,"original",1),"stale world scheduled");GameManager.Instance.World=world;
  world.Bodies[1]=new EntityRebirthNPC();Need(!RebirthNpcPreparedSchedulerAdmission.TrySchedule(actor,"original",1),"rebound native body scheduled");world.Bodies[1]=actor;
  actor.Dead=true;Need(!RebirthNpcPreparedSchedulerAdmission.TrySchedule(actor,"original",1),"dead body scheduled");actor.Dead=false;
  world.Remote=true;Need(!RebirthNpcPreparedSchedulerAdmission.TrySchedule(actor,"original",1),"remote authority scheduled");world.Remote=false;
  Need(RebirthNpcPreparedSchedulerAdmission.TrySchedule(actor,"original",1)&&RebirthNpcPreparedSchedulerAdmission.TrySchedule(actor,"original",1),"original dedup admission refused");
  ThreadManager.Main=false;RebirthNpcPreparedSchedulerAdmission.Tick();RebirthNpcPreparedSchedulerAdmission.RetireSavedWorld(world,true);Need(actor.Calls==0,"worker invoked native admission");ThreadManager.Main=true;
  RebirthNpcPreparedSchedulerAdmission.Tick();Need(actor.Calls==1,"failed native admission not retained exactly once");
  var second=Actor(world,2);Need(RebirthNpcPreparedSchedulerAdmission.TrySchedule(second,"second",2),"second original refused");
  actor.During=()=>{RebirthNpcPreparedSchedulerAdmission.Tick();throw new System.InvalidOperationException("native callback failure");};
  RebirthNpcPreparedSchedulerAdmission.Tick();Need(actor.Calls==2&&second.Calls==1,"nested tick consumed retry or skipped batch");
  actor.During=null;actor.Accept=second.Accept=true;RebirthNpcPreparedSchedulerAdmission.Tick();Need(actor.Calls==3&&second.Calls==2,"original retries lost");RebirthNpcPreparedSchedulerAdmission.Tick();Need(actor.Calls==3&&second.Calls==2,"successful original repeated");
  actor.Accept=false;Need(RebirthNpcPreparedSchedulerAdmission.TrySchedule(actor,"retained",3),"retained original refused");
  var next=new World();GameManager.Instance.World=next;RebirthNpcPreparedSchedulerAdmission.Tick();Need(actor.Calls==3,"departing native body invoked");
  ThreadManager.Main=false;Need(!RebirthNpcPreparedSchedulerAdmission.RetireUnexpectedWorld(world,next,"old","new"),"worker retired original");ThreadManager.Main=true;
  GameManager.Instance.World=world;RebirthNpcPreparedSchedulerAdmission.Tick();Need(actor.Calls==4,"worker retirement lost retained original");
  RebirthNpcPreparedSchedulerAdmission.RetireSavedWorld(world,false);RebirthNpcPreparedSchedulerAdmission.Tick();Need(actor.Calls==5,"failed save retired original");
  RebirthNpcPreparedSchedulerAdmission.RetireSavedWorld(world,true);RebirthNpcPreparedSchedulerAdmission.Tick();Need(actor.Calls==5,"positive saved retirement still invoked original");
  System.Console.WriteLine("PASS actual prepared scheduler scope/retry/reentry; native actor proof and full lifecycle NOT RUN");
 }
}
