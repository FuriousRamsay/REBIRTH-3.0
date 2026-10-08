using System;
class World{public bool IsRemote(){return true;}}
class EntityPlayer{public int entityId;}
class EntityPlayerLocal:EntityPlayer{public World world=new World();public int entityId;}
class ItemValue{public int type;public ushort Seed;}
enum RebirthSurvivorSupportAction{ListenAudiobook}
class SingletonMonoBehaviour<T>{public static ConnectionManager Instance;}
class RebirthAudiobookListeningSessionService{public static bool TryBegin(EntityPlayer p,int t,ushort s,out string m){m="";return true;}}
class NetPackage{public int Channel;}
class NetPackageRebirthSurvivorSupportActionRequest:NetPackage{public NetPackageRebirthSurvivorSupportActionRequest Setup(params object[] args){return this;}}
class NetPackagePlayerInventory:NetPackage{public NetPackagePlayerInventory(){Channel=1;}public NetPackagePlayerInventory Setup(params object[] args){return this;}}
class NetPackageManager{public static T GetPackage<T>()where T:new(){return new T();}}
class Channel{public bool disconnected;public bool IsDisconnected(){return disconnected;}}
class ConnectionManager{public bool IsServer,IsConnected=true;public Channel[] channels=new[]{new Channel(),new Channel()};public int sent;public Channel[] GetConnectionToServer(){return channels;}public void SendToServer(NetPackage p){sent++;}}
class RebirthSurvivorSupportUiFeedback{public static int failures;public static void Receive(bool ok,string s){if(!ok)failures++;}}
class Localization{public static string Get(string k){return k;}}
class RebirthMusicLibraryClient{
// HELPER
}
class Check{
// DISPATCH
static int Main(){for(int mode=0;mode<7;mode++){var c=new ConnectionManager();switch(mode){case 1:c.IsConnected=false;break;case 2:c.channels=null;break;case 3:c.channels[0]=null;break;case 4:c.channels[0].disconnected=true;break;case 5:c.channels[1].disconnected=true;break;case 6:c.channels=new Channel[0];break;}SingletonMonoBehaviour<ConnectionManager>.Instance=c;RebirthSurvivorSupportUiFeedback.failures=0;Dispatch(new EntityPlayerLocal(),new ItemValue());if(c.sent!=(mode==0?2:0)||RebirthSurvivorSupportUiFeedback.failures!=(mode==0?0:1))throw new Exception("channel "+mode);}SingletonMonoBehaviour<ConnectionManager>.Instance=null;Dispatch(new EntityPlayerLocal(),new ItemValue());var server=new ConnectionManager{IsServer=true};SingletonMonoBehaviour<ConnectionManager>.Instance=server;Dispatch(new EntityPlayerLocal(),new ItemValue());if(server.sent!=0)throw new Exception("host sent request");Console.WriteLine("PASS: audiobook remote dispatch checks both channels before sending; null connection and host path handled");return 0;}}
