using System;
using System.Collections.Generic;
class Actor { public string className; }
class Handle { public bool IsCompleted; public object onComplete; }
class Script { public bool Running; public bool IsRunning()=>Running; }
class SleeperVolume {
 public bool wasCleared,isSpawning; public int planned;
 public Dictionary<int,Actor> respawnMap=new(); public List<Handle> pendingSpawnOps=new(); public Script minScript;
 public int GetAliveCount()=>planned+respawnMap.Count;
}
static class Program {
 static int checks;
 static void Check(bool ok,string name) { if(!ok)throw new Exception(name); checks++;Console.WriteLine("PASS "+name); }
 static void Main() {
 var room=new SleeperVolume();
 Check(!RebirthPurgeSleeperPolicy.CombatSettled(room),"empty unactivated room cannot clear");
 room.respawnMap[1]=new Actor{className="animalChicken"};
 Check(RebirthPurgeSleeperPolicy.CombatSettled(room),"only harmless retained animals are combat settled");
 Check(RebirthPurgeSleeperPolicy.ExcludedNativeMembers(room)==1,"scanner excludes harmless member");
 room.planned=1;Check(!RebirthPurgeSleeperPolicy.CombatSettled(room),"planned spawn blocks clearance");room.planned=0;
 room.isSpawning=true;Check(!RebirthPurgeSleeperPolicy.CombatSettled(room),"active spawning blocks clearance");room.isSpawning=false;
 room.respawnMap[2]=new Actor{className="zombieArlene"};Check(!RebirthPurgeSleeperPolicy.CombatSettled(room),"live zombie blocks clearance");room.respawnMap.Remove(2);
 room.respawnMap[2]=new Actor{className="animalZombieBear"};Check(!RebirthPurgeSleeperPolicy.CombatSettled(room),"zombie animal blocks clearance");room.respawnMap.Remove(2);
 room.minScript=new Script{Running=true};Check(!RebirthPurgeSleeperPolicy.CombatSettled(room),"running script blocks clearance");room.minScript.Running=false;
 var handle=new Handle();room.pendingSpawnOps.Add(handle);Check(!RebirthPurgeSleeperPolicy.CombatSettled(room),"unfinished async spawn blocks clearance");
 handle.IsCompleted=true;handle.onComplete=new object();Check(!RebirthPurgeSleeperPolicy.CombatSettled(room),"pending callback blocks clearance");
 handle.onComplete=null;Check(RebirthPurgeSleeperPolicy.CombatSettled(room),"completed native handles do not block clearance");
 Console.WriteLine(checks+" checks passed using production policy");
 }
}
