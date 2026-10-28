// AUTHORED_NOT_RUN. Actual event-world predicate; native identities doubled.
using System;
using System.Collections.Generic;
class World {internal readonly Dictionary<int,EntityAlive> Bodies=new Dictionary<int,EntityAlive>();internal EntityAlive GetEntity(int id)=>Bodies.TryGetValue(id,out var actor)?actor:null;}
class EntityAlive {internal World world;internal int entityId;}
class MinEventParams {internal EntityAlive Self,Instigator;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class Actual {
 // ACTUAL_EVENT_WORLD
 internal static void Run(){
  var a=new World();var b=new World();var actor=new EntityAlive{world=a,entityId=3};a.Bodies[3]=actor;
  var context=new MinEventParams{Self=actor};GameManager.Instance.World=a;
  Require(IsCurrentEventWorld(context,a),"current native actor refused");
  GameManager.Instance.World=b;Require(!IsCurrentEventWorld(context,a)&&!IsCurrentEventWorld(context,b),"old native actor crossed world");
  actor.world=b;b.Bodies[3]=new EntityAlive{world=b,entityId=3};Require(!IsCurrentEventWorld(context,b),"reused numeric entity identity admitted");
  b.Bodies[3]=actor;Require(IsCurrentEventWorld(context,b),"actual new body map refused");
  context.Self=null;context.Instigator=actor;Require(IsCurrentEventWorld(context,b),"original instigator fallback lost");
  context.Self=new EntityAlive{world=a,entityId=3};Require(!IsCurrentEventWorld(context,b),"foreign Self borrowed current instigator");
  context.Self=null;ThreadManager.Main=false;Require(!IsCurrentEventWorld(context,b),"off-thread native event admitted");ThreadManager.Main=true;
  Require(!IsCurrentEventWorld(null,b)&&!IsCurrentEventWorld(context,null),"missing original admitted");
  Console.WriteLine("PASS actual event-world predicate with native identity doubles; not min-event execution");
 }
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
class Program {static void Main()=>Actual.Run();}
