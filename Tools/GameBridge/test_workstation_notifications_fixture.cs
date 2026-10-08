using System;
using System.Collections.Generic;
public struct Vector3i {public object ToVector3(){return null;}}
public class EntityPlayerLocal {}
public class World {public EntityPlayerLocal player=new EntityPlayerLocal();public EntityPlayerLocal GetPrimaryPlayer(){return player;}}
public class GameManager {public static GameManager Instance=new GameManager();public World World=new World();public static List<string> shown=new List<string>();public static void ShowTooltip(EntityPlayerLocal p,string n){shown.Add(n);}}
public static class Time {public static float realtimeSinceStartup;}
public static class Localization {public static string Get(string s){return s;}}
public static class RemoteResourceIdentity {public static object Workstation(Vector3i p){return null;}}
public static class RemoteResourceLiveSync {public static int changes;public static void NotifySourceChanged(object id,object pos,string why){changes++;}}
public static class RemoteResourceClientAvailability {public static int clears,requests;public static void Clear(){clears++;}public static void InvalidateAndRequest(EntityPlayerLocal p){requests++;}}
public static class Service {
 public static object Sync=new object();public static bool serverWorld;
 public static HashSet<Vector3i> PendingAccessChanges=new HashSet<Vector3i>();
 public static Dictionary<string,float> PendingReplies=new Dictionary<string,float>();
 public static Queue<string> Notices=new Queue<string>();
 static void QueueNotice(string s){Notices.Enqueue(s);}
// PUMP
}
public static class Checks {
 static void Assert(bool v,string m){if(!v)throw new Exception(m);}
 public static void Main(){
 Service.PendingReplies["a"]=15;Service.PendingReplies["b"]=20;
 for(int i=0;i<100;i++){Time.realtimeSinceStartup=14;Service.PumpNotifications();}
 Assert(Service.PendingReplies.Count==2&&GameManager.shown.Count==0,"premature expiration");
 Time.realtimeSinceStartup=15;Service.PumpNotifications();Assert(Service.PendingReplies.Count==1&&Service.PendingReplies.ContainsKey("b")&&GameManager.shown.Count==1,"boundary timeout");
 Service.PumpNotifications();Assert(GameManager.shown.Count==1,"duplicate notice");
 Service.PendingReplies["c"]=20;Time.realtimeSinceStartup=20;Service.PumpNotifications();Assert(Service.PendingReplies.Count==0&&GameManager.shown.Count==2,"batched expiration");
 Service.Notices.Enqueue("first");Service.Notices.Enqueue("second");Service.PumpNotifications();Assert(GameManager.shown[2]=="first"&&Service.Notices.Count==1,"queue order");
 Service.serverWorld=true;GameManager.Instance.World.player=null;Service.PendingAccessChanges.Add(new Vector3i());Service.PumpNotifications();Assert(RemoteResourceLiveSync.changes==1&&Service.Notices.Count==1,"dedicated access flush");
 Service.serverWorld=false;GameManager.Instance.World.player=new EntityPlayerLocal();Service.PendingAccessChanges.Add(new Vector3i());Service.PumpNotifications();Assert(RemoteResourceClientAvailability.clears==1&&RemoteResourceClientAvailability.requests==1&&GameManager.shown[3]=="second","client refresh");
 Console.WriteLine("PASS: pending replies, boundary/batched expiry, notice order and dedicated/client access updates");
 }
}