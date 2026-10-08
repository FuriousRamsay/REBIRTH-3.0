using System;using System.Collections.Generic;
class EntityPlayer {public World world;public int entityId=7;}
class World {public bool Remote;public EntityPlayer Player;public bool IsRemote(){return Remote;}public object GetEntity(int id){return Player;}}
class GameManager {public static GameManager Instance=new GameManager();public World World;}
class Time {public static float realtimeSinceStartup;}
class RebirthStablePlayerIdentity {public static bool TryResolveServerEntity(EntityPlayer p,out RebirthStablePlayerIdentity id){id=new RebirthStablePlayerIdentity();return true;}}
class RebirthWorldCharacterRecord {public bool Dirty;public long Revision=8;}
class RebirthWorldCharacterRepository {public static bool Saved=true;public static RebirthWorldCharacterRecord Record=new RebirthWorldCharacterRecord();public static bool SaveIfDirty(RebirthStablePlayerIdentity id,string reason){return Saved;}public static bool TryGet(RebirthStablePlayerIdentity id,out RebirthWorldCharacterRecord r){r=Record;return true;}}
class RebirthSurvivorNetworkService {public static bool Success;public static int Sends;public static bool SendOwnerState(EntityPlayer p,long rev,bool force,string reason){if(!force||rev!=8)throw new Exception("send contract");Sends++;return Success;}}
class Actual {
 static object Gate=new object();static Dictionary<int,EntityPlayer> PendingOwnerPublications=new Dictionary<int,EntityPlayer>();static Dictionary<string,int> WindowAwards=new Dictionary<string,int>();static float nextOwnerPublicationAttempt;
 // SOURCE
 public static int Count{get{return PendingOwnerPublications.Count;}}
}
class Check {
 static void Assert(bool v,string why){if(!v)throw new Exception(why);}
 static void Main(){
 var w=new World();var p=new EntityPlayer{world=w};w.Player=p;GameManager.Instance.World=w;
 Actual.QueueOwnerPublication(p);Actual.FlushOwnerPublications();Assert(Actual.Count==1&&RebirthSurvivorNetworkService.Sends==1,"failed send retained");
 Time.realtimeSinceStartup=.5f;Actual.FlushOwnerPublications();Assert(RebirthSurvivorNetworkService.Sends==1,"cadence");
 Time.realtimeSinceStartup=1;RebirthSurvivorNetworkService.Success=true;Actual.FlushOwnerPublications();Assert(Actual.Count==0&&RebirthSurvivorNetworkService.Sends==2,"success drained");
 Actual.QueueOwnerPublication(p);w.Player=new EntityPlayer{world=w};Time.realtimeSinceStartup=2;Actual.FlushOwnerPublications();Assert(Actual.Count==0&&RebirthSurvivorNetworkService.Sends==2,"recycled entity refused");
 w.Player=p;Actual.QueueOwnerPublication(p);GameManager.Instance.World=new World{Player=p};Time.realtimeSinceStartup=3;Actual.FlushOwnerPublications();Assert(Actual.Count==0&&RebirthSurvivorNetworkService.Sends==2,"different world refused");
 GameManager.Instance.World=w;Actual.QueueOwnerPublication(p);Actual.ClearRuntimeAntiRepeat();Assert(Actual.Count==0,"reset clears");
 RebirthWorldCharacterRepository.Record.Dirty=true;RebirthWorldCharacterRepository.Saved=false;Actual.QueueOwnerPublication(p);Time.realtimeSinceStartup=4;Actual.FlushOwnerPublications();Assert(Actual.Count==1&&RebirthSurvivorNetworkService.Sends==2,"failed save prevents publication");RebirthWorldCharacterRepository.Saved=true;Time.realtimeSinceStartup=5;Actual.FlushOwnerPublications();Assert(Actual.Count==0&&RebirthSurvivorNetworkService.Sends==3,"save retry permits publication");
 w.Remote=true;Actual.QueueOwnerPublication(p);Assert(Actual.Count==0,"client queue refused");
 Console.WriteLine("PASS actual owner scheduler: failed-send retention, one-second cadence, success drain, recycled entity/world rejection, reset and client refusal. Native world/identity/repository/network doubled.");
 }
}