using System;using System.Collections.Generic;
enum RebirthVehicleAssemblyAction {Install,Pickup}enum RebirthVehiclePartSourceLocation {None}enum RebirthVehicleAssemblyCarrier {RepairableBlock,VehicleEntity}enum RebirthVehicleRequestOutcome {Applied,Indeterminate}
struct Vector3i {}
class ItemStack {public bool IsEmpty(){return false;}public ItemStack Clone(){return new ItemStack();}}
class RebirthInstalledVehiclePart {public string ItemName="part";}
class RebirthVehicleAssembly {public long Revision;public Guid AssemblyId;public RebirthVehicleAssembly DeepClone(){return this;}}
class World {public bool Remote;public bool IsRemote(){return Remote;}}
class EntityPlayerLocal {public World world=new World();public int entityId;}
class UI {public EntityPlayerLocal entityPlayer=new EntityPlayerLocal();}class XUi {public UI playerUI=new UI();}
class PersistentPlayerData {public object PrimaryId=new object();}
class GameManager {public static GameManager Instance=new GameManager();public PersistentPlayerData Data=new PersistentPlayerData();public PersistentPlayerData GetPersistentLocalPlayer(){return Data;}}
class SingletonMonoBehaviour<T> {public static T Instance;}
class ConnectionManager {public bool IsServer,Ready;public int Sent;public void SendToServer(object o){Sent++;}}
class NetPackageRebirthVehicleAssemblyRequest {public object Setup(params object[] args){return this;}}
static class NetPackageManager {public static T GetPackage<T>() where T:new(){return new T();}}
static class RebirthMusicLibraryClient {public static bool CanSend(ConnectionManager c,object r){return c!=null&&!c.IsServer&&c.Ready;}}
static class Localization {public static string Get(string s){return s;}}
static class RebirthVehicleAssemblyService {
public static int Calls;public static RebirthVehicleAssembly ProcessEntity(World w,EntityPlayerLocal p,int entity,Guid id,long rev,RebirthVehicleAssemblyAction action,string slot,RebirthInstalledVehiclePart part,RebirthVehiclePartSourceLocation loc,int source,out string message,out RebirthVehicleRequestOutcome outcome){Calls++;message="";outcome=RebirthVehicleRequestOutcome.Applied;return null;}
public static RebirthVehicleAssembly Process(World w,EntityPlayerLocal p,Vector3i pos,Guid id,long rev,RebirthVehicleAssemblyAction action,string slot,RebirthInstalledVehiclePart part,RebirthVehiclePartSourceLocation loc,int source,out string message,out RebirthVehicleRequestOutcome outcome){return ProcessEntity(w,p,0,id,rev,action,slot,part,loc,source,out message,out outcome);}}
class Check {
class PendingClientRequest {public Check Controller;public RebirthVehicleAssemblyCarrier Carrier;public Vector3i Position;public int EntityId;public long ExpectedRevision;public ItemStack ReservedCursorItem,PickedCursorItem;}
static object PendingClientSync=new object();static Dictionary<Guid,PendingClientRequest> PendingClientRequests=new Dictionary<Guid,PendingClientRequest>();
XUi xui=new XUi();Guid pendingRequestId;long pendingExpectedRevision;RebirthVehicleAssembly assembly;RebirthVehicleAssemblyCarrier carrier;Vector3i position;int entityId;int reserved,cleared,received;bool failReserve;
ItemStack ReserveOneCursorItem(string name){reserved++;return failReserve?null:new ItemStack();}void ClearFailedPickedCursorItem(ItemStack s){cleared++;}void SetStatus(string s){}void Refresh(){}void Receive(params object[] args){received++;}
// METHODS
static void Main(){
for(int i=0;i<8;i++){PendingClientRequests.Clear();GameManager.Instance=new GameManager();var c=new Check();var connection=new ConnectionManager{IsServer=false,Ready=false};SingletonMonoBehaviour<ConnectionManager>.Instance=connection;c.xui.playerUI.entityPlayer.world.Remote=true;
if(i==0)SingletonMonoBehaviour<ConnectionManager>.Instance=null;if(i==1)c.xui.playerUI.entityPlayer.world=null;if(i==2)c.xui.playerUI.entityPlayer.world.Remote=false;if(i==3)connection.IsServer=true;if(i==4)GameManager.Instance.Data=null;if(i==5)GameManager.Instance.Data.PrimaryId=null;if(i==6)c.xui.playerUI.entityPlayer=null;
c.Send(RebirthVehicleAssemblyAction.Pickup,"s",new RebirthInstalledVehiclePart(),true,RebirthVehiclePartSourceLocation.None,-1,new ItemStack());if(c.reserved!=0||c.cleared!=0||PendingClientRequests.Count!=0||connection.Sent!=0||c.received!=0)throw new Exception("Failed guard "+i);}
foreach(bool remote in new[]{false,true}){PendingClientRequests.Clear();GameManager.Instance=new GameManager();var c=new Check();c.xui.playerUI.entityPlayer.world.Remote=remote;var connection=new ConnectionManager{IsServer=!remote,Ready=true};SingletonMonoBehaviour<ConnectionManager>.Instance=connection;c.Send(RebirthVehicleAssemblyAction.Install,"s",new RebirthInstalledVehiclePart(),true,RebirthVehiclePartSourceLocation.None,-1);if(c.reserved!=1||PendingClientRequests.Count!=1||connection.Sent!=(remote?1:0)||c.received!=(remote?0:1))throw new Exception("Valid dispatch");}
PendingClientRequests.Clear();GameManager.Instance=new GameManager();SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true,Ready=true};var failed=new Check{failReserve=true};failed.Send(RebirthVehicleAssemblyAction.Install,"s",null,true,RebirthVehiclePartSourceLocation.None,-1);if(failed.reserved!=1||PendingClientRequests.Count!=0||failed.received!=0)throw new Exception("Reservation failure created request");var busy=new Check{pendingRequestId=Guid.NewGuid()};busy.Send(RebirthVehicleAssemblyAction.Install,"s",null,true,RebirthVehiclePartSourceLocation.None,-1);if(busy.reserved!=0||PendingClientRequests.Count!=0)throw new Exception("Busy request duplicated");
Console.WriteLine("PASS: actual vehicle Send rejects eight unavailable contexts before reservation/pending/send and leaves cursor untouched; valid host/client each dispatch once; busy and failed reservation create no request. Transport/native inventory/server processing substituted.");}}
