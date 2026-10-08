using System;
class World { public EntityPlayerLocal player=new EntityPlayerLocal(); public EntityPlayerLocal GetPrimaryPlayer(){return player;} }
class EntityPlayerLocal {public int entityId=1; public object bag=new object();}
class PersistentPlayerData {public object PrimaryId=new object();}
class GameManager { public static GameManager Instance=new GameManager(); public World World=new World(); public PersistentPlayerData persistent=new PersistentPlayerData(); public PersistentPlayerData GetPersistentLocalPlayer(){return persistent;} public static int tips;public static void ShowTooltip(EntityPlayerLocal p,string text){tips++;}}
class Localization{public static string Get(string k){return k;}}
class Time{public static float unscaledTime;}
enum QuickStackRadialAction{Deposit}
class LogisticsPreviewData{}
class Channel{public bool disconnected;public bool IsDisconnected(){return disconnected;}}
class NetPackage{public int Channel;}
class NetPackageLogisticsPreviewRequest:NetPackage{public NetPackageLogisticsPreviewRequest Setup(params object[] args){return this;}}
class NetPackageManager{public static int channel;public static T GetPackage<T>() where T:NetPackage,new(){return new T{Channel=channel};}}
class ConnectionManager{public bool IsServer,IsConnected=true;public Channel[] channels=new[]{new Channel()};public int sends;public Channel[] GetConnectionToServer(){return channels;}public void SendToServer(NetPackage p){sends++;}}
class SingletonMonoBehaviour<T>{public static ConnectionManager Instance;}
class LogisticsTransferService{
// CHANNEL_HELPER
}
class LogisticsPreviewService{
private static Action<QuickStackRadialAction,LogisticsPreviewData> callback;private static QuickStackRadialAction pending;private static World requestWorld;private static ulong requestEpoch=1,nextRequestId,pendingRequestId;private static float requestDeadline;private const float RequestTimeoutSeconds=5;
public static int builds;public static LogisticsPreviewData Build(World w,EntityPlayerLocal p,QuickStackRadialAction a,string t){builds++;return new LogisticsPreviewData();}
// PREVIEW_METHODS
public static void AssertClear(){if(callback!=null||pendingRequestId!=0||requestWorld!=null)throw new Exception("pending not cleared");}
}
class Check{
static void Require(bool b,string m){if(!b)throw new Exception(m);}
static int Main(){
for(int mode=0;mode<10;mode++){
GameManager.Instance=new GameManager();GameManager.tips=0;LogisticsPreviewService.builds=0;NetPackageManager.channel=0;
var c=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=c;
switch(mode){case 0:SingletonMonoBehaviour<ConnectionManager>.Instance=null;break;case 1:c.IsConnected=false;break;case 2:c.channels=null;break;case 3:c.channels[0]=null;break;case 4:c.channels[0].disconnected=true;break;case 5:NetPackageManager.channel=2;break;case 6:GameManager.Instance.persistent.PrimaryId=null;break;case 7:GameManager.Instance.World.player.bag=null;break;case 8:c.IsServer=true;break;}
int calls=0;LogisticsPreviewService.Request(QuickStackRadialAction.Deposit,"",(a,d)=>{Require(d!=null,"empty result missing");calls++;});
if(mode<8){Require(calls==1&&c.sends==0&&GameManager.tips==1,"unavailable mode "+mode);LogisticsPreviewService.AssertClear();}
else if(mode==8){Require(calls==1&&c.sends==0&&LogisticsPreviewService.builds==1,"host");LogisticsPreviewService.AssertClear();}
else {Require(calls==0&&c.sends==1,"client dispatch");Time.unscaledTime+=6;LogisticsPreviewService.UpdatePendingRequest();Require(calls==1,"timeout callback");LogisticsPreviewService.AssertClear();}
}
Console.WriteLine("PASS: unavailable preview channels clear pending and complete once; host builds locally; client dispatch/timeout retained");return 0;}}
