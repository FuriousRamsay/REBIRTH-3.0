using System;
using System.Collections.Generic;
public struct Vector3i {}
public class WorldBase {public bool remote=true;public bool IsRemote(){return remote;}}
public class World:WorldBase {public EntityPlayerLocal primary;public EntityPlayerLocal GetPrimaryPlayer(){return primary;}}
public class EntityAlive {}
public class EntityPlayerLocal:EntityAlive {public World world;public int entityId=1;}
public class PersistentPlayerData {public object PrimaryId=new object();}
public class PlayerList {public PersistentPlayerData indexed;public PersistentPlayerData GetPlayerDataFromEntityID(int id){return indexed;}}
public class GameManager {public static GameManager Instance;public World World;public PlayerList list=new PlayerList();public PersistentPlayerData local=new PersistentPlayerData();public PlayerList GetPersistentPlayerList(){return list;}public PersistentPlayerData GetPersistentLocalPlayer(){return local;}}
public class Channel {public bool bad;public bool IsDisconnected(){return bad;}}
public class ConnectionManager {public bool IsServer;public bool IsConnected=true;public Channel[] channels=new[]{new Channel()};public int sent;public Channel[] GetConnectionToServer(){return channels;}public void SendToServer(object p){sent++;}}
public static class SingletonMonoBehaviour<T> {public static T Instance;}
public class NetPackage {public int Channel=0;}
public class NetPackageRebirthWorkstationOwnerRequest:NetPackage {public static object identity;public void Setup(Vector3i p,int id,object uid){identity=uid;}}
public static class NetPackageManager {public static int created;public static T GetPackage<T>() where T:new(){created++;return new T();}}
public static class Time {public static float realtimeSinceStartup;}
public static class LogisticsTransferService {
// HELPER
}
public static class Service {
 public static bool serverWorld;public static object Sync=new object();public static Dictionary<Vector3i,int> States=new Dictionary<Vector3i,int>();public static Dictionary<Vector3i,float> NextStateRequestTime=new Dictionary<Vector3i,float>();
// REQUEST
}
public static class Checks {
 static void Assert(bool v,string m){if(!v)throw new Exception(m);}
 public static void Main(){
 var w=new World();var p=new EntityPlayerLocal{world=w};w.primary=p;var g=new GameManager{World=w};GameManager.Instance=g;
 var c=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=c;var pos=new Vector3i();
 c.channels[0].bad=true;Service.RequestStateIfNeeded(w,pos,p);Assert(c.sent==0&&Service.NextStateRequestTime.Count==0,"unavailable transport throttled request");
 c.channels[0].bad=false;Service.RequestStateIfNeeded(w,pos,p);Assert(c.sent==1&&ReferenceEquals(NetPackageRebirthWorkstationOwnerRequest.identity,g.local.PrimaryId),"local join fallback");
 int created=NetPackageManager.created;Service.RequestStateIfNeeded(w,pos,p);Assert(c.sent==1&&created==NetPackageManager.created,"throttle allocates/sends");
 Time.realtimeSinceStartup=4;g.list.indexed=new PersistentPlayerData();Service.RequestStateIfNeeded(w,pos,p);Assert(c.sent==2&&ReferenceEquals(NetPackageRebirthWorkstationOwnerRequest.identity,g.list.indexed.PrimaryId),"indexed precedence");
 Time.realtimeSinceStartup=8;Service.States[pos]=1;created=NetPackageManager.created;Service.RequestStateIfNeeded(w,pos,p);Assert(c.sent==2&&created==NetPackageManager.created,"known state sends/allocates");
 Service.States.Clear();Service.NextStateRequestTime.Clear();g.list.indexed=null;w.primary=new EntityPlayerLocal{world=w};Service.RequestStateIfNeeded(w,pos,p);Assert(c.sent==2,"used different local identity");w.primary=p;
 c.IsServer=true;Service.RequestStateIfNeeded(w,pos,p);Assert(c.sent==2,"server connection");c.IsServer=false;
 g.World=new World();Service.RequestStateIfNeeded(w,pos,p);Assert(c.sent==2,"stale world");g.World=w;
 Service.serverWorld=true;Service.RequestStateIfNeeded(w,pos,p);Assert(c.sent==2,"authority world");Service.serverWorld=false;
 SingletonMonoBehaviour<ConnectionManager>.Instance=null;Service.RequestStateIfNeeded(w,pos,p);Assert(c.sent==2,"null connection");
 Console.WriteLine("PASS: workstation join identity, transport, throttle/known-state allocation, world and role isolation");
 }
}
