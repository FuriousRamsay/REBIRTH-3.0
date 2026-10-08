using System;using System.Collections.Generic;
class Channel{public bool Closed;public bool IsDisconnected(){return Closed;}}
class NetPackage{public int Channel;}
class ConnectionManager{public bool IsServer,IsConnected=true,Throw;public Channel[] Channels=new[]{new Channel()};public int Sends;public Channel[] GetConnectionToServer(){return Channels;}public void SendToServer(NetPackage packet){Sends++;if(Throw)throw new Exception("ambiguous");}}
class Bag{} class EntityPlayerLocal{public int entityId=7;public Bag bag=new Bag();} class PersistentPlayerData{}
enum QuickStackRadialAction{Deposit,CompanionInventoryPull=8}
enum LogisticsTransferRequestMode{ExecuteOrReplay,QueryOnly}
class NetPackageLogisticsTransferRequest:NetPackage{public static ulong Epoch;public static int Id;public NetPackageLogisticsTransferRequest Setup(int p,PersistentPlayerData uid,QuickStackRadialAction a,string target,string source,Bag bag,int id,ulong epoch,LogisticsTransferRequestMode mode){Epoch=epoch;Id=id;return this;}}
class NetPackageManager{public static T GetPackage<T>()where T:new(){return new T();}}
static class Time{public static float unscaledTime=10;}
static class QuickStackHotkeyDiagnostics{public static bool Enabled;public static void Write(string s){}}
static class Localization{public static string Get(string key){return key;}}
static class GameManager{public static int Tooltips;public static void ShowTooltip(EntityPlayerLocal p,string s){Tooltips++;}}
class Subject{public static int nextRequestId,pendingRequestId,lastAppliedResultRequestId;public static ulong clientRequestEpoch=123,pendingRequestEpoch;static float pendingRequestDeadline;static bool pendingRequestWasHotkey;static QuickStackRadialAction pendingAction;static string pendingTargetId,pendingSourceKeys;static Action transferCompleted;const float PendingOutcomeQuerySeconds=3;
// HELPERS
public static void Run(ConnectionManager connection){var player=new EntityPlayerLocal();var persistent=new PersistentPlayerData();var action=QuickStackRadialAction.CompanionInventoryPull;string selectedTargetId="N:dog",selectedSourceKeys="0|7|3|item";bool hotkeyAll=false;Action onCompleted=null;
// REQUEST_BODY
}
public static void Reset(){nextRequestId=0;pendingRequestId=0;lastAppliedResultRequestId=0;clientRequestEpoch=123;pendingRequestEpoch=0;GameManager.Tooltips=0;}}
class Check{static void Main(){for(int mode=0;mode<8;mode++){Subject.Reset();var c=new ConnectionManager();if(mode==1)c.IsConnected=false;if(mode==2)c.Channels=null;if(mode==3)c.Channels=new Channel[0];if(mode==4)c.Channels[0]=null;if(mode==5)c.Channels[0].Closed=true;if(mode==6)c.Throw=true;if(mode==7){Subject.nextRequestId=int.MaxValue;Subject.lastAppliedResultRequestId=int.MaxValue;}try{Subject.Run(c);if(mode==6)throw new Exception("missing exception");}catch(Exception e){if(mode!=6||e.Message!="ambiguous")throw;}bool queued=mode==0||mode>=6;if(c.Sends!=(queued?1:0)||Subject.pendingRequestId!=(queued?1:0)||GameManager.Tooltips!=(queued?0:1))throw new Exception("pending registration "+mode);if(mode==7&&(Subject.clientRequestEpoch==123||Subject.clientRequestEpoch==0||Subject.lastAppliedResultRequestId!=0||NetPackageLogisticsTransferRequest.Epoch!=Subject.clientRequestEpoch))throw new Exception("wrap scope");if(mode==6&&Subject.pendingRequestEpoch!=123)throw new Exception("ambiguous lease lost");}Console.WriteLine("PASS: unavailable channels create no pending transfer; queued/ambiguous sends retain identity; ID wrap rotates scope and response ordering");}}
