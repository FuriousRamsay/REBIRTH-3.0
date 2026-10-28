// AUTHORED_NOT_RUN. Native object/roster/queue adapters below do not establish
// saved owner debit, endpoint publication, or end-to-end multiplayer proof.
using System;using System.IO;using System.Collections.Generic;using System.Linq;using System.Reflection;using System.Runtime.CompilerServices;using System.Threading;using HarmonyLib;
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]sealed class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n){}}}
interface INetConnection{bool IsDisconnected();}
class NetConnectionAbs:INetConnection
{internal readonly List<NetPackage> receivedPackages=new List<NetPackage>();internal ClientInfo cInfo;internal bool Disconnected;public bool IsDisconnected()=>Disconnected;public void GetPackages(){}}
class ClientInfo{internal int entityId=7;internal bool loginDone=true,disconnecting;internal INetConnection[] netConnection;}
class Clients{internal ClientInfo Peer;internal ClientInfo ForEntityId(int id)=>Peer?.entityId==id?Peer:null;}
class ConnectionManager{internal bool IsServer=true,IsConnected=true,ThrowSend;internal int Sent;internal Clients Clients=new Clients();internal INetConnection[] connectionToServer;internal void SendToServer(NetPackage p,bool flag){Sent++;if(ThrowSend)throw new IOException("Native send outcome unknown");}}
static class SingletonMonoBehaviour<T>{internal static T Instance;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class EntityPlayer{internal World world;internal int entityId=7,Died;internal bool Dead;internal string Creation="creation";internal bool IsDead()=>Dead;}
class EntityPlayerLocal:EntityPlayer{}
public class World{internal bool Remote;internal EntityPlayer Actor;internal bool IsRemote()=>Remote;internal EntityPlayer GetEntity(int id)=>Actor?.entityId==id?Actor:null;internal EntityPlayerLocal GetPrimaryPlayer()=>Actor as EntityPlayerLocal;}
public class GameManager{internal static GameManager Instance;internal World World;internal Action OnSave;internal int Saves;internal void SaveLocalPlayerData(){Saves++;OnSave?.Invoke();}}
public class NetPackage
{internal ClientInfo Sender;internal int Channel,inSendQueuesCount;internal bool EntityValid=true,UserValid=true;public virtual void ProcessPackage(World w,GameManager g){}public bool ValidEntityIdForSender(int id)=>EntityValid&&Sender?.entityId==id;public bool ValidUserIdForSender(object user)=>UserValid;}
class NetPackagePlayerData:NetPackage{internal static Action OnSetup;internal NetPackagePlayerData Setup(EntityPlayerLocal p){OnSetup?.Invoke();return this;}}
static class NetPackageManager{internal static int Allocated,Freed;internal static T GetPackage<T>()where T:NetPackage,new(){Allocated++;return new T();}internal static void FreePackage(NetPackage p){Freed++;}}
static class LogisticsTransferService{internal static bool ClientChannelReady(ConnectionManager manager,NetPackage packet)=>true;}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer player)=>player.Creation;}
static class RebirthLogisticsOwnerReceipt
{
 // ACTUAL_OWNER_UPLOAD
 internal static void Upload(EntityPlayerLocal p)=>SaveAndUpload(p);
}
enum QuickStackRadialAction{Deposit}
static class RebirthCamperRemovalParcelTransport{internal static bool IsPacket(NetPackage packet)=>false;}
static class RebirthRemoteResourceNetworkScope{internal static bool IsPacket(NetPackage packet)=>false;}
class NetPackageRebirthLogisticsHeaderRequest:NetPackage
{int entity=7;object user=new object();string text;RebirthLogisticsHeaderAdmission.RequestMode mode;
 // ACTUAL_HeaderRequest
}
class NetPackageRebirthLogisticsHeaderAck:NetPackage
{Guid operation;string binding,nonce;int state,entity;
 // ACTUAL_HeaderAck
}
class NetPackageRebirthLogisticsNativeBodyIntent:NetPackage
{int entity=7,total,count,index;object user=new object();Guid operation;string hash,text;
 // ACTUAL_NativeBodyIntent
}
class NetPackageRebirthLogisticsNativeBodyAck:NetPackage
{Guid operation;string binding,hash,nonce;int stage;
 // ACTUAL_NativeBodyAck
}
class NetPackageRebirthLogisticsNativeBodyQuery:NetPackage
{int entity=7;object user=new object();Guid operation;string command,hash,nonce;
 // ACTUAL_NativeBodyQuery
}
class NetPackageRebirthLogisticsHistoricalZero:NetPackage
{int entity=7,total,index,count;object user=new object();Guid operation;string nonce,digest,text;
 // ACTUAL_HistoricalZero
}
class NetPackageRebirthLogisticsHistoricalZeroAck:NetPackage
{Guid operation;string nonce,digest;
 // ACTUAL_HistoricalZeroAck
}
class NetPackageRebirthLogisticsBodyReferenceNotice:NetPackage
{RebirthLogisticsLoadedBodyNotice.Notice notice=new RebirthLogisticsLoadedBodyNotice.Notice();
 // ACTUAL_BodyReferenceNotice
}
class NetPackageRebirthLogisticsBodyReferenceAck:NetPackage
{Guid operation,incarnation,nonce;int life;string digest;bool suppressed;
 // ACTUAL_BodyReferenceAck
}
static class RebirthLogisticsHeaderAdmission
{internal enum RequestMode:byte{Prepare,Query,Cancel}internal static void Receive(ClientInfo sender,string text,RequestMode mode){RebirthLogisticsOriginalTransport.Calls++;}internal static void Ack(World world,Guid op,string binding,string nonce,int state,int entity){RebirthLogisticsOriginalTransport.Calls++;}}
static class RebirthLogisticsNativeBodyIntent
{internal static void Receive(ClientInfo sender,Guid op,string hash,int total,int count,int index,string text){RebirthLogisticsOriginalTransport.Calls++;}internal static void Query(ClientInfo sender,Guid op,string command,string hash,string nonce){RebirthLogisticsOriginalTransport.Calls++;}internal static void Ack(World world,Guid op,string binding,string hash,int stage,string nonce){RebirthLogisticsOriginalTransport.Calls++;}}
static class RebirthLogisticsHistoricalBeforeCapsule
{internal static void Receive(ClientInfo sender,Guid op,string nonce,string digest,int total,int index,int count,string text){RebirthLogisticsOriginalTransport.Calls++;}internal static void Acknowledge(World world,Guid op,string nonce,string digest){RebirthLogisticsOriginalTransport.Calls++;}}
static class RebirthLogisticsLoadedBodyNotice
{internal sealed class Notice:IDisposable{internal World ReceivedWorld;internal ConnectionManager ReceivedManager;internal INetConnection ReceivedConnection;public void Dispose(){}}
 // ACTUAL_NOTICE_CONNECTION
 internal static bool CheckNotice(Notice n)=>ClientConnectionCurrent(n);
 internal static void Receive(World world,Notice n,NetPackage packet){RebirthLogisticsOriginalTransport.Calls++;}internal static void Ack(ClientInfo sender,Guid op,Guid incarnation,Guid nonce,int life,string digest,bool suppressed){RebirthLogisticsOriginalTransport.Calls++;}}
static class RebirthLogisticsOriginalTransport
{
 internal enum Mode:byte{Prepare,Query,Cancel}
 internal static int Calls;
 // ACTUAL_HELPERS
 internal static bool CanSendNative(INetConnection c)=>c!=null&&!c.IsDisconnected();
 internal static void Receive(EntityPlayer a,Guid op,ulong epoch,int request,QuickStackRadialAction action,string target,string selection,Mode mode){Calls++;}
 internal static void ReceivePart(World w,Guid op,string digest,int index,int count,string text){Calls++;}
 internal static void ReceivePhysicalEvidence(EntityPlayer a,Guid op,int stage,string digest,int total,int index,int count,string text){Calls++;}
 internal static void ReceivePhysicalAck(World w,Guid op,string binding,int stage){Calls++;}
 internal static void ReceivePhysicalTerminalEvidence(World w,Guid op,string binding,string result,string proof,int deaths){Calls++;}
}
public sealed class NetPackageRebirthLogisticsOriginalRequest:NetPackage
{int entity=7,request=1;object user=new object();Guid operation=Guid.NewGuid();ulong epoch=1;QuickStackRadialAction action;string target="",selection="";RebirthLogisticsOriginalTransport.Mode mode;
 // ACTUAL_OriginalRequest
}
public sealed class NetPackageRebirthLogisticsOriginalResult:NetPackage
{Guid operation;string digest,text;int index,count;
 // ACTUAL_OriginalResult
}
public sealed class NetPackageRebirthLogisticsPhysicalEvidence:NetPackage
{int entity=7,stage,total,index,count;object user=new object();Guid operation;string digest,text;
 // ACTUAL_PhysicalEvidence
}
public sealed class NetPackageRebirthLogisticsPhysicalAck:NetPackage
{Guid operation;string binding;int stage;
 // ACTUAL_PhysicalAck
}
public sealed class NetPackageRebirthLogisticsTerminalPhysical:NetPackage
{Guid operation;string binding,result,proof;int deaths;
 // ACTUAL_TerminalPhysical
}
class NetPackageRemoteResourceConsumeRequest:NetPackage{}class NetPackageRemoteResourceConsumeResult:NetPackage{}
class NetPackageRebirthCookingPullOriginal:NetPackage{}class NetPackageRebirthCookingPulledOriginal:NetPackage{}class NetPackageRebirthCookingPullNoPayment:NetPackage{}
class NetPackageRebirthCookingGridOwnerWitness:NetPackage{}class NetPackageRebirthCookingGridOwnerWitnessResult:NetPackage{}
class NetPackageRebirthMetabolismConsumeRequest:NetPackage{}class NetPackageRebirthNpcConsumptionOwner:NetPackage{}class NetPackageRebirthNpcConsumptionOwnerReturn:NetPackage{}class NetPackageRebirthDogHireOwnerGrant:NetPackage{}class NetPackageRebirthActionPaymentOwnerGrant:NetPackage{}
class NetPackageRebirthUiQueueInsertionRequestV1:NetPackage{}class NetPackageRebirthUiQueueInsertionReplyV1:NetPackage{}
static class RebirthRecyclerPlacedSettlement{internal static bool IsRecyclerPurposePackage(NetPackage p)=>false;}
static partial class RebirthRemoteResourcePlacedStationOriginal{internal static bool IsCancellationJoinPacket(NetPackage p)=>false;}
static class RebirthProductiveEarnedUiCoordinator{internal static bool IsPendingQueueHandoffNative(NetPackage p)=>false;}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{private static bool IsHydrationEnvelope(NetPackage p)=>false;}
// ACTUAL_DEQUEUE
static class Program
{
 static int assertions;static World world;static GameManager game;static ConnectionManager manager;static ClientInfo peer;static NetConnectionAbs connection;
 static void Require(bool value,string reason){assertions++;if(!value)throw new Exception(reason);}
 static void Reset(bool server)
 {
  ThreadManager.Main=true;world=new World{Remote=!server};world.Actor=new EntityPlayerLocal{world=world};game=new GameManager{World=world};GameManager.Instance=game;NetPackageManager.Allocated=NetPackageManager.Freed=0;NetPackagePlayerData.OnSetup=null;
  connection=new NetConnectionAbs();peer=new ClientInfo{netConnection=new INetConnection[]{connection}};connection.cInfo=server?peer:null;
  manager=new ConnectionManager{IsServer=server,connectionToServer=new INetConnection[]{connection}};manager.Clients.Peer=peer;SingletonMonoBehaviour<ConnectionManager>.Instance=manager;RebirthLogisticsOriginalTransport.Calls=0;
 }
 // Invoke the actual observer around the native queue adapter. We never write
 // its private receivedConnections table or bypass its World/queue checks.
 static void Dequeue(NetPackage packet)
 {
  var type=typeof(RebirthProductiveEarnedUiDeferredTransport).GetNestedType("ReceiveDequeue",BindingFlags.NonPublic);
  var flags=BindingFlags.Static|BindingFlags.NonPublic;connection.receivedPackages.Add(packet);
  object[] args={connection,true,null};type.GetMethod("Prefix",flags).Invoke(null,args);var state=args[2];
  try
  {
   var destination=new List<NetPackage>(connection.receivedPackages);connection.receivedPackages.Clear();
   type.GetMethod("Postfix",flags).Invoke(null,new object[]{destination,true,state});
  }
  finally{type.GetMethod("Finalizer",flags).Invoke(null,new object[]{null,state});}
 }
 static NetPackage Packet(int kind)
 {switch(kind){case 0:return new NetPackageRebirthLogisticsOriginalRequest();case 1:return new NetPackageRebirthLogisticsPhysicalEvidence();case 2:return new NetPackageRebirthLogisticsOriginalResult();case 3:return new NetPackageRebirthLogisticsPhysicalAck();case 4:return new NetPackageRebirthLogisticsTerminalPhysical();case 5:return new NetPackageRebirthLogisticsHeaderRequest();case 6:return new NetPackageRebirthLogisticsHeaderAck();case 7:return new NetPackageRebirthLogisticsNativeBodyIntent();case 8:return new NetPackageRebirthLogisticsNativeBodyAck();case 9:return new NetPackageRebirthLogisticsNativeBodyQuery();case 10:return new NetPackageRebirthLogisticsHistoricalZero();case 11:return new NetPackageRebirthLogisticsHistoricalZeroAck();case 12:return new NetPackageRebirthLogisticsBodyReferenceNotice();default:return new NetPackageRebirthLogisticsBodyReferenceAck();}}
 static bool Server(int kind)=>kind<2||kind==5||kind==7||kind==9||kind==10||kind==13;
 static void Main()
 {
  for(int kind=0;kind<14;kind++)
  {
   Reset(Server(kind));var packet=Packet(kind);packet.Sender=peer;Dequeue(packet);packet.ProcessPackage(world,game);Require(RebirthLogisticsOriginalTransport.Calls==1,"Actual logistics handler refused current dequeued connection "+kind);
   Reset(Server(kind));packet=Packet(kind);packet.Sender=peer;packet.ProcessPackage(world,game);Require(RebirthLogisticsOriginalTransport.Calls==0,"Unobserved package entered handler "+kind);
   Reset(Server(kind));packet=Packet(kind);packet.Sender=peer;Dequeue(packet);var replacement=new NetConnectionAbs();if(Server(kind))peer.netConnection[0]=replacement;else manager.connectionToServer[0]=replacement;packet.ProcessPackage(world,game);Require(RebirthLogisticsOriginalTransport.Calls==0,"Old connection callback entered new session "+kind);
   Reset(Server(kind));packet=Packet(kind);packet.Sender=peer;Dequeue(packet);packet.Channel=1;packet.ProcessPackage(world,game);Require(RebirthLogisticsOriginalTransport.Calls==0,"Unexpected channel entered handler "+kind);
   Reset(Server(kind));packet=Packet(kind);packet.Sender=peer;Dequeue(packet);connection.Disconnected=true;packet.ProcessPackage(world,game);Require(RebirthLogisticsOriginalTransport.Calls==0,"Disconnected callback entered handler "+kind);
   Reset(Server(kind));packet=Packet(kind);packet.Sender=peer;Dequeue(packet);GameManager.Instance=new GameManager{World=world};packet.ProcessPackage(world,game);Require(RebirthLogisticsOriginalTransport.Calls==0,"Replaced callback manager entered handler "+kind);
   Reset(Server(kind));packet=Packet(kind);packet.Sender=peer;Dequeue(packet);ThreadManager.Main=false;packet.ProcessPackage(world,game);Require(RebirthLogisticsOriginalTransport.Calls==0,"Off-thread callback entered handler "+kind);
  }
  Reset(true);Require(RebirthLogisticsOriginalTransport.TryCurrentPeer(world.Actor,out var actualPeer,out var actualConnection)&&ReferenceEquals(actualPeer,peer)&&ReferenceEquals(actualConnection,connection),"Current native peer lookup");
  peer.netConnection=new INetConnection[0];Require(!RebirthLogisticsOriginalTransport.TryCurrentPeer(world.Actor,out _,out _),"Empty native connection array must refuse without indexing");
  Reset(true);peer.loginDone=false;Require(!RebirthLogisticsOriginalTransport.TryCurrentPeer(world.Actor,out _,out _),"Incomplete login cannot bind original");
  Reset(true);peer.disconnecting=true;Require(!RebirthLogisticsOriginalTransport.TryCurrentPeer(world.Actor,out _,out _),"Disconnecting peer cannot bind original");
  Reset(true);world.Actor=new EntityPlayer{world=world};var stale=new EntityPlayer{world=world};Require(!RebirthLogisticsOriginalTransport.TryCurrentPeer(stale,out _,out _),"Replaced body cannot bind original");
  Reset(false);var notice=new RebirthLogisticsLoadedBodyNotice.Notice{ReceivedWorld=world,ReceivedManager=manager,ReceivedConnection=connection};Require(RebirthLogisticsLoadedBodyNotice.CheckNotice(notice),"Current retained notice connection");
  manager.connectionToServer[0]=new NetConnectionAbs();Require(!RebirthLogisticsLoadedBodyNotice.CheckNotice(notice),"Retained notice cannot apply across replacement connection");
  manager.connectionToServer[0]=connection;GameManager.Instance.World=new World();Require(!RebirthLogisticsLoadedBodyNotice.CheckNotice(notice),"Retained notice cannot apply across World replacement");
  Reset(false);RebirthLogisticsOwnerReceipt.Upload((EntityPlayerLocal)world.Actor);Require(game.Saves==1&&manager.Sent==1&&NetPackageManager.Freed==0,"Actual original save/upload route");
  Reset(false);game.OnSave=()=>manager.connectionToServer[0]=new NetConnectionAbs();RejectUpload();Require(manager.Sent==0&&NetPackageManager.Allocated==0,"Changed connection during native save cannot enter upload");
  Reset(false);NetPackagePlayerData.OnSetup=()=>world.Actor.Died++;RejectUpload();Require(manager.Sent==0&&NetPackageManager.Freed==1,"Changed life during native packet capture releases only unsent packet");
  Reset(false);NetPackagePlayerData.OnSetup=()=>GameManager.Instance.World=new World();RejectUpload();Require(manager.Sent==0&&NetPackageManager.Freed==1,"Changed World during native packet capture cannot send");
  Reset(false);manager.ThrowSend=true;RejectUpload();Require(manager.Sent==1&&NetPackageManager.Freed==0,"Ambiguous native queue entry is never freed or replayed by upload");
  Reset(false);game.OnSave=()=>SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();RejectUpload();Require(manager.Sent==0&&NetPackageManager.Allocated==0,"Replaced native manager during save cannot send");
  // RESOURCE_SCOPE_TESTS
  Console.WriteLine("Native transport scope: "+assertions+" assertions passed; native persistence and multiplayer are separate evidence.");
 }
 static void RejectUpload(){try{RebirthLogisticsOwnerReceipt.Upload((EntityPlayerLocal)world.Actor);}catch(IOException){return;}throw new Exception("Changed native upload scope must retain original");}
}
