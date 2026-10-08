using System;
using System.Collections.Generic;
public struct Vector3i {}
public class World {public bool remote=true;public bool IsRemote(){return remote;}public object GetTileEntity(Vector3i p){return new TileEntityWorkstation();}}
public class TileEntityWorkstation {}
public class EntityPlayerLocal {public World world;public int entityId=1;}
public class PersistentPlayerData {public object PrimaryId=new object();}
public class GameManager {public static GameManager Instance;public World World;public PersistentPlayerData local=new PersistentPlayerData();public PersistentPlayerData GetPersistentLocalPlayer(){return local;}}
public class Channel {public bool bad;public bool IsDisconnected(){return bad;}}
public class ConnectionManager {public bool IsServer;public bool IsConnected=true;public bool throwSend;public Channel[] channels=new[]{new Channel()};public int sent;public Channel[] GetConnectionToServer(){return channels;}public void SendToServer(object p){sent++;if(throwSend)throw new Exception("ambiguous send");}}
public static class SingletonMonoBehaviour<T> {public static T Instance;}
public class NetPackage {public int Channel=0;}
public class NetPackageRebirthWorkstationSecurityAction:NetPackage {public NetPackageRebirthWorkstationSecurityAction Setup(Vector3i p,int id,object uid,RebirthWorkstationSecurityAction a,string w){return this;}}
public static class NetPackageManager {public static T GetPackage<T>() where T:new(){return new T();}}
public static class Time {public static float realtimeSinceStartup;}
public static class Localization {public static string Get(string s){return s;}}
public enum RebirthWorkstationSecurityAction:byte {Lock,Unlock,SetPassword,TryPassword}
public static class RebirthWorkstationCredentials {public static bool IsToken(string s){return s=="epoch";}public static string Request(string e,string id,byte a,string p){return "wire";}public static bool TryReadRequest(string w,byte a,out string e,out string id,out string c){e="epoch";id="id";c="";return true;}}
public static class LogisticsTransferService {
// HELPER
}
public static class Service {
 public class SecurityState {public bool ProtocolSupported=true;public string Epoch="epoch";}
 public static object Sync=new object();public static Dictionary<Vector3i,SecurityState> States=new Dictionary<Vector3i,SecurityState>();public static Dictionary<string,float> PendingReplies=new Dictionary<string,float>();public static List<string> Notices=new List<string>();public static int serverCalls;
 static SecurityState GetOrMigrateState(World w,Vector3i p,TileEntityWorkstation t,PersistentPlayerData d,bool b){return new SecurityState();}
 static void RequestStateIfNeeded(World w,Vector3i p,EntityPlayerLocal e){}
 static void QueueNotice(string s){Notices.Add(s);}
 static string ResultMessage(int i){return "result";}
 static bool ProcessSecurityAction(World w,Vector3i p,int id,object uid,RebirthWorkstationSecurityAction a,string wire){serverCalls++;return true;}
 public static bool Run(Vector3i p,EntityPlayerLocal e,RebirthWorkstationSecurityAction a){return SubmitAction(p,e,a,"");}
// REQUEST
}
public static class Checks {
 static void Check(int mode,RebirthWorkstationSecurityAction action){
 var w=new World{remote=mode!=7};var p=new EntityPlayerLocal{world=w};GameManager.Instance=new GameManager{World=w};
 var c=new ConnectionManager{IsServer=mode==7};SingletonMonoBehaviour<ConnectionManager>.Instance=c;
 Service.States.Clear();Service.PendingReplies.Clear();Service.Notices.Clear();Service.serverCalls=0;Service.States[new Vector3i()]=new Service.SecurityState();
 if(mode==0)SingletonMonoBehaviour<ConnectionManager>.Instance=null;
 if(mode==1)c.IsConnected=false;if(mode==2)c.channels=null;if(mode==3)c.channels=new Channel[0];if(mode==4)c.channels[0]=null;if(mode==5)c.channels[0].bad=true;
 if(mode==8)p.world=new World();if(mode==9)c.IsServer=true;if(mode==10)c.throwSend=true;
 bool result=false,threw=false;try{result=Service.Run(new Vector3i(),p,action);}catch{threw=true;}
 bool sent=mode==6||mode==10,host=mode==7;
 if(c.sent!=(sent?1:0)||Service.serverCalls!=(host?1:0)||Service.PendingReplies.Count!=(sent?1:0)||result!=(mode==6||host)||threw!=(mode==10))throw new Exception("mode "+mode+" action "+action);
 if(mode>=1&&mode<=5&&(Service.Notices.Count!=1||Service.Notices[0]!="xuiRebirthWorkstationConnectionUnavailable"))throw new Exception("Incorrect transport feedback");
 }
 public static void Main(){foreach(RebirthWorkstationSecurityAction a in Enum.GetValues(typeof(RebirthWorkstationSecurityAction)))for(int i=0;i<11;i++)Check(i,a);Console.WriteLine("PASS: four security actions x11 transport/world/role modes; ambiguous send retains pending identity");}
}
