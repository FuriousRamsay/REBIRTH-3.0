using System;
class World {public bool Remote=true;public bool IsRemote(){return Remote;}}
class EntityPlayer {public World world=new World();public int entityId;}
class EntityPlayerLocal:EntityPlayer {public PlayerUI PlayerUI=new PlayerUI();}
class PlayerUI {public GUIWindowManager windowManager=new GUIWindowManager();}
class GUIWindowManager {public string Open,ThrowOn;public bool IsWindowOpen(string id){if(id==ThrowOn)throw new Exception("lookup unavailable");return id==Open;}}
class XUiC_LootWindowGroup {public const string ID="loot";}
class ItemValue {public int type;public ushort Seed;}
class NetPackage {public int Channel;}
class NetPackagePlayerInventory:NetPackage {public NetPackagePlayerInventory(){Channel=1;}public NetPackagePlayerInventory Setup(params object[] a){return this;}}
class NetPackageRebirthSurvivorSupportActionRequest:NetPackage {public static bool ThrowSetup;public object[] Args;public NetPackageRebirthSurvivorSupportActionRequest Setup(params object[] a){if(ThrowSetup)throw new Exception("setup");Args=a;return this;}}
enum RebirthSurvivorSupportAction {ListenAudiobook, SetLiteratureActivity}
class NetPackageManager {public static bool ThrowFactory,ReturnNull;public static T GetPackage<T>()where T:class,new(){if(ThrowFactory)throw new Exception("factory");return ReturnNull?null:new T();}}
class ConnectionManager {public bool IsServer;public bool Ready=true,InventoryReady=true;public int Sent;public bool ThrowSend;public NetPackage Last;public void SendToServer(NetPackage p){if(ThrowSend)throw new Exception("send");Sent++;Last=p;}}
class SingletonMonoBehaviour<T>{public static T Instance;}
class RebirthMusicLibraryClient {public static bool CanSend(ConnectionManager c,NetPackage p){return c!=null&&p!=null&&!c.IsServer&&c.Ready&&(p.Channel!=1||c.InventoryReady);}}
class RebirthCookingLiterature {public static bool IsCard(ItemValue v){return false;}}
class RebirthLiteratureStudySessionService {public static int Starts;public static void BeginClientTracking(EntityPlayer p,ItemValue v){Starts++;}}
class RebirthRageCapsuleService {public static int Reads;public static bool Consume(EntityPlayer p,int t,ushort s,out string m){Reads++;m="ok";return true;}}
class RebirthSurvivorSupportUiFeedback {public static void Receive(bool ok,string message){}}
class Localization {public static string Get(string k){return k;}}
class Check {
static void SetActivityHint(EntityPlayerLocal p,bool slow){RebirthRageCapsuleService.Reads++;}
// METHODS
static void Main(){for(int mode=0;mode<9;mode++){
 var c=new ConnectionManager();EntityPlayerLocal p=new EntityPlayerLocal();
 if(mode==1)c=null;if(mode==2)c.Ready=false;if(mode==3)c.InventoryReady=false;if(mode==4)c.IsServer=true;
 if(mode==5){p.world.Remote=false;c.IsServer=true;}if(mode==6){p.world.Remote=false;c=null;}
 if(mode==7)p.world=null;if(mode==8)p=null;
 SingletonMonoBehaviour<ConnectionManager>.Instance=c;RebirthLiteratureStudySessionService.Starts=0;RebirthRageCapsuleService.Reads=0;
 SendClientActivityHint(p,true);
 bool valid=mode==0||mode==3||mode==5;
 if(RebirthLiteratureStudySessionService.Starts!=0||RebirthRageCapsuleService.Reads!=(mode==5?1:0)||(c!=null&&c.Sent!=((mode==0||mode==3)?1:0)))throw new Exception("mode "+mode);
}
for(int failure=0;failure<4;failure++){
 var connection=new ConnectionManager();
 SingletonMonoBehaviour<ConnectionManager>.Instance=connection;
 var player=new EntityPlayerLocal();player.entityId=42;
 NetPackageManager.ThrowFactory=failure==0;
 NetPackageManager.ReturnNull=failure==1;
 NetPackageRebirthSurvivorSupportActionRequest.ThrowSetup=failure==2;
 connection.ThrowSend=failure==3;
 SendClientActivityHint(player,true);
 if(connection.Sent!=0)throw new Exception("failed hint sent "+failure);
 NetPackageManager.ThrowFactory=false;NetPackageManager.ReturnNull=false;
 NetPackageRebirthSurvivorSupportActionRequest.ThrowSetup=false;connection.ThrowSend=false;
 SendClientActivityHint(player,false);
 var sent=connection.Last as NetPackageRebirthSurvivorSupportActionRequest;
 if(connection.Sent!=1||sent==null||(int)sent.Args[0]!=42||(string)sent.Args[3]!="normal")
     throw new Exception("later hint did not recover with current activity "+failure);
}
var owner=new EntityPlayerLocal();
if(IsBusyUiOpen(owner)||IsBusyUiOpen(null))throw new Exception("idle or absent owner busy");
owner.PlayerUI.windowManager.Open="rebirthBackpackLibrary";
if(!IsBusyUiOpen(owner))throw new Exception("library must slow held study");
owner.PlayerUI.windowManager.ThrowOn="inventory";
if(!IsBusyUiOpen(owner))throw new Exception("earlier lookup failure hid library");
owner.PlayerUI.windowManager.Open="rebirthProgressionExplorer";
if(!IsBusyUiOpen(owner))throw new Exception("existing explorer classification lost");
owner.PlayerUI.windowManager.ThrowOn=null;
foreach(var group in BusyWindowGroups){owner.PlayerUI.windowManager.Open=group;if(!IsBusyUiOpen(owner))throw new Exception("busy group ignored: "+group);}
owner.PlayerUI.windowManager.Open="rebirthContextNavigation";if(IsBusyUiOpen(owner))throw new Exception("passive context navigation should not slow study");
owner.PlayerUI.windowManager.Open="unrelatedHud";if(IsBusyUiOpen(owner))throw new Exception("passive HUD should not slow study");
Console.WriteLine("PASS all "+BusyWindowGroups.Length+" actual reading busy-window entries including new backpack/music/audio/station contexts; passive HUD/navigation remain ordinary study. Native window manager doubled.");owner.PlayerUI.windowManager.Open=null;
if(IsBusyUiOpen(owner))throw new Exception("closed library remained busy");
owner.PlayerUI.windowManager=null;
if(IsBusyUiOpen(owner))throw new Exception("absent manager busy");
owner.PlayerUI=null;
if(IsBusyUiOpen(owner))throw new Exception("absent UI busy");
Console.WriteLine("PASS actual study hint: factory/null/setup/send failures contained, later current-state send recovered; transport and busy UI regressions pass");}
}
