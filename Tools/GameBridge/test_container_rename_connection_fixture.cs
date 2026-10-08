using System;
public class XUiController {}
public class GUIWindow {}
public class World {public bool remote;public bool IsRemote(){return remote;}}
public class EntityPlayerLocal {public World world;public int entityId=1;}
public class PersistentPlayerData {public object PrimaryId=new object();}
public class GameManager {public static GameManager Instance;public World World;public PersistentPlayerData persistent=new PersistentPlayerData();public PersistentPlayerData GetPersistentLocalPlayer(){return persistent;}public static int tips;public static void ShowTooltip(EntityPlayerLocal p,string m,string a,string b){tips++;}}
public static class Localization {public static string Get(string s){return s;}}
public class Channel {public bool bad;public bool IsDisconnected(){return bad;}}
public class ConnectionManager {public bool IsServer;public bool IsConnected=true;public Channel[] channels=new[]{new Channel()};public int sent;public Channel[] GetConnectionToServer(){return channels;}public void SendToServer(object p){sent++;}}
public static class SingletonMonoBehaviour<T> {public static T Instance;}
public class NetPackage {public int Channel=0;}
public class NetPackageRebirthRenameContainer:NetPackage {public NetPackageRebirthRenameContainer Setup(int p,int id,PersistentPlayerData d,string n){return this;}}
public static class NetPackageManager {public static T GetPackage<T>() where T:new(){return new T();}}
public static class RebirthContainerRenameService {public static int mutations;public static string NormalizeName(string n){return n.Trim();}public static void ProcessServerRequest(World w,int p,int id,object uid,string n){mutations++;}}
public static class LogisticsTransferService {
// HELPER
}
public class WindowManager {public int closed;public void Close(GUIWindow w){closed++;}}
public class PlayerUI {public EntityPlayerLocal entityPlayer;public WindowManager windowManager=new WindowManager();}
public class XUi {public PlayerUI playerUI;}
public class TextInput {public string Text="My supplies";}
public class Rename {
 public XUi xui;public TextInput txtContainerName=new TextInput();public GUIWindow windowGroup=new GUIWindow();public int blockPos;
// CONFIRM
}
public static class Checks {
 static void Run(int mode){
 var w=new World{remote=mode!=7};var c=new ConnectionManager{IsServer=mode==7};
 GameManager.Instance=new GameManager{World=w};GameManager.tips=0;RebirthContainerRenameService.mutations=0;
 var ui=new Rename{xui=new XUi{playerUI=new PlayerUI{entityPlayer=new EntityPlayerLocal{world=w}}}};
 SingletonMonoBehaviour<ConnectionManager>.Instance=c;
 if(mode==0)SingletonMonoBehaviour<ConnectionManager>.Instance=null;
 if(mode==1)c.IsConnected=false;
 if(mode==2)c.channels=null;
 if(mode==3)c.channels=new Channel[0];
 if(mode==4)c.channels[0]=null;
 if(mode==5)c.channels[0].bad=true;
 if(mode==8)GameManager.Instance.World=new World();
 if(mode==9)c.IsServer=true;
 ui.BtnConfirm_OnPressed(null,0);
 bool valid=mode==6||mode==7;
 if(ui.xui.playerUI.windowManager.closed!=(valid?1:0)||c.sent!=(mode==6?1:0)||RebirthContainerRenameService.mutations!=(mode==7?1:0)||GameManager.tips!=(valid?0:1)||ui.txtContainerName.Text!="My supplies")throw new Exception("mode "+mode);
 }
 public static void Main(){for(int i=0;i<10;i++)Run(i);Console.WriteLine("PASS: rename host/client, missing/disconnected channels, wrong world/role, retained input");}
}
