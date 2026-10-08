using System;
class World {public bool Remote=true;public bool IsRemote(){return Remote;}}
class EntityPlayer {public World world=new World();public int entityId;}
class EntityPlayerLocal:EntityPlayer {}
class ItemValue {public int type;public ushort Seed;}
class NetPackage {public int Channel;}
class NetPackagePlayerInventory:NetPackage {public NetPackagePlayerInventory(){Channel=1;}public NetPackagePlayerInventory Setup(params object[] a){return this;}}
class NetPackageRebirthSurvivorSupportActionRequest:NetPackage {public NetPackageRebirthSurvivorSupportActionRequest Setup(params object[] a){return this;}}
enum RebirthSurvivorSupportAction {ListenAudiobook, ConsumeSupportItem}
class NetPackageManager {public static T GetPackage<T>()where T:new(){return new T();}}
class ConnectionManager {public bool IsServer;public bool Ready=true,InventoryReady=true;public int Sent;public void SendToServer(NetPackage p){Sent++;}}
class SingletonMonoBehaviour<T>{public static T Instance;}
class RebirthMusicLibraryClient {public static bool CanSend(ConnectionManager c,NetPackage p){return c!=null&&!c.IsServer&&c.Ready&&(p.Channel!=1||c.InventoryReady);}}
class RebirthCookingLiterature {public static bool IsCard(ItemValue v){return false;}}
class RebirthLiteratureStudySessionService {public static int Starts;public static void BeginClientTracking(EntityPlayer p,ItemValue v){Starts++;}}
class RebirthTraitSupportService {public static int Reads;public static bool ConsumeAndApplyMatchingInventoryItem(EntityPlayer p,int t,ushort s,out string m){Reads++;m="ok";return true;}}
class RebirthSurvivorSupportUiFeedback {public static void Receive(bool ok,string message){}}
class Localization {public static string Get(string k){return k;}}
class Check {
// METHODS
static void Main(){for(int mode=0;mode<9;mode++){
 var c=new ConnectionManager();EntityPlayer p=new EntityPlayerLocal();
 if(mode==1)c=null;if(mode==2)c.Ready=false;if(mode==3)c.InventoryReady=false;if(mode==4)c.IsServer=true;
 if(mode==5){p.world.Remote=false;c.IsServer=true;}if(mode==6){p.world.Remote=false;c=null;}
 if(mode==7)p.world=null;if(mode==8)p=new EntityPlayer();
 SingletonMonoBehaviour<ConnectionManager>.Instance=c;RebirthLiteratureStudySessionService.Starts=0;RebirthTraitSupportService.Reads=0;
 Dispatch(p,new ItemValue());
 bool valid=mode==0||mode==3||mode==5;
 if(RebirthLiteratureStudySessionService.Starts!=0||RebirthTraitSupportService.Reads!=(mode==5?1:0)||(c!=null&&c.Sent!=((mode==0||mode==3)?1:0)))throw new Exception("mode "+mode);
}Console.WriteLine("PASS actual trait dispatch: remote/host success and six invalid contexts do not track, send or read");}
}
