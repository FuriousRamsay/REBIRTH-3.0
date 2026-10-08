using System;
class World{public bool IsRemote(){return true;}}
class EntityPlayerLocal{public World world=new World();public int entityId;}
class ItemValue{}
class NetPackage{public int Channel;}
class NetPackageRebirthMusicLibraryRequest:NetPackage{public NetPackageRebirthMusicLibraryRequest Setup(params object[] args){return this;}}
class NetPackagePlayerInventory:NetPackage{public NetPackagePlayerInventory(){Channel=1;}public NetPackagePlayerInventory Setup(params object[] args){return this;}}
class NetPackageManager{public static T GetPackage<T>()where T:new(){return new T();}}
class Channel{public bool disconnected;public bool IsDisconnected(){return disconnected;}}
class SingletonMonoBehaviour<T>{public static T Instance;}
class ConnectionManager{public bool IsServer,IsConnected=true;public Channel[] channels=new[]{new Channel(),new Channel()};public int sent;public Channel[] GetConnectionToServer(){return channels;}public void SendToServer(NetPackage p){sent++;}}
class RebirthSurvivorSupportUiFeedback{public static int failures;public static void Receive(bool ok,string s){if(!ok)failures++;}}
class Localization{public static string Get(string k){return k;}}
class Check{static long Revision;static string CreationId="id";static bool Scope=true,PendingTransfer;static bool EnsureCurrent(EntityPlayerLocal p){return Scope;}
// HELPER
static void Dispatch(ConnectionManager requestedConnection,int operation){SingletonMonoBehaviour<ConnectionManager>.Instance=requestedConnection;var player=new EntityPlayerLocal();int index=0;ItemValue item=null;
// REMOTE_BODY
}
static int Main(){for(int op=0;op<4;op++)for(int mode=0;mode<7;mode++){var c=new ConnectionManager();switch(mode){case 1:c.IsConnected=false;break;case 2:c.channels=null;break;case 3:c.channels[0]=null;break;case 4:c.channels[0].disconnected=true;break;case 5:c.channels[1].disconnected=true;break;case 6:c.IsServer=true;break;}RebirthSurvivorSupportUiFeedback.failures=0;Dispatch(c,op);bool good=mode==0||(mode==5&&op!=1);if(c.sent!=(good?(op==1?2:1):0)||RebirthSurvivorSupportUiFeedback.failures!=(good?0:1))throw new Exception("mode "+mode+" op "+op);}Dispatch(null,1);Scope=false;for(int op=0;op<4;op++){var c=new ConnectionManager();RebirthSurvivorSupportUiFeedback.failures=0;Dispatch(c,op);if(c.sent!=(op==0?1:0)||RebirthSurvivorSupportUiFeedback.failures!=(op==0?0:1))throw new Exception("stale scope operation "+op);}Scope=true;PendingTransfer=true;for(int op=1;op<4;op++){var c=new ConnectionManager();Dispatch(c,op);if(c.sent!=0)throw new Exception("pending transfer operation "+op);}Console.WriteLine("PASS: actual common/remote dispatch: stale scope and pending mutations refused; refresh allowed; every request channel checked, insert inventory channel checked before sends. Scope resolver doubled, separately tested.");return 0;}}
