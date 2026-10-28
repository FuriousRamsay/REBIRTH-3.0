$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperRemoteStationClient.cs')).Replace('using System;','').Replace('using System.Collections.Generic;','')
$fixture=@'
using System;using System.Collections.Generic;
namespace UnityEngine {public static class Time {public static float realtimeSinceStartup;}}
public static class ThreadManager {public static bool IsMainThread()=>true;}
public struct Vec {public float sqrMagnitude=>0;public static Vec operator -(Vec a,Vec b)=>a;}
public class World {public EntityPlayerLocal Player;public EntityVehicle Vehicle;public bool IsRemote()=>true;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public object GetEntity(int id)=>Vehicle;}
public class EntityVehicle {public World world;public Vec position;public int entityId=7;}
public class EntityPlayerLocal {public World world;public Vec position;public int entityId=8;public bool IsDead()=>false;}
public class ItemClass {public string GetLocalizedItemName()=>"Forge";}
public class ItemValue {public ItemClass ItemClass=new ItemClass();}
public class ItemStack {public ItemValue itemValue=new ItemValue();public int count;public ItemStack Clone()=>new ItemStack{count=count};}
public class Recipe {public string GetName()=>"testRecipe";}
public class RebirthCamperStationState {public EntityVehicle BoundVehicle;public Guid InstallationId;public long Revision;public ItemStack[] Fuel,Input,Tools,Output;public object[] Queue;public float currentBurnTimeLeft;public float[] currentMeltTimesLeft;public bool isBurning,isBesideWater;public ulong lastTickTime;public void RestoreRevision(long value){Revision=value;}}
public class Installation {public Guid Id=>State.InstallationId;public ItemStack InstalledItem=new ItemStack();public RebirthCamperStationState State;}
public class Stations {public Installation Installed;public Installation Find(Guid id)=>Installed!=null&&Installed.Id==id?Installed:null;public Installation GetSlot(int slot)=>slot==0?Installed:null;}
public class RebirthCamperStationInstallations:Stations {}
public static class RebirthCamperCraftingStation {public const int ModuleSlots=4;}
public class Storage {public Guid VehicleIdentity=Guid.NewGuid();}
public class RebirthCamperTransferSnapshot {public Storage Storage=new Storage();public RebirthCamperStationInstallations Stations=new RebirthCamperStationInstallations();}
public class Controller {public Dictionary<Type,object> Parts=new Dictionary<Type,object>();public T GetChildByType<T>() where T:class=>Parts.TryGetValue(typeof(T),out var x)?x as T:null;public T GetParentByType<T>() where T:class=>GetChildByType<T>();}
public class GUIWindow {}
public class XUiWindowGroup:GUIWindow {public Controller Controller=new Controller();}
public class WindowManager {public GUIWindow Window;public bool Opened,Modal,EscClosable;public bool IsWindowOpen(string name)=>Opened;public bool TryGetWindow(string name,out GUIWindow w){w=Window;return true;}public void Open(GUIWindow w,bool modal,bool notEscClosable){Opened=true;Modal=modal;EscClosable=!notEscClosable;}public void Close(GUIWindow w){Opened=false;Fixture.Picker.Close();}public void Escape(){if(Opened&&EscClosable)Close(Window);}}
public class XUi:Controller {public PlayerUI playerUI=new PlayerUI();}
public class PlayerUI {public EntityPlayerLocal entityPlayer;}
public class LocalPlayerUI {public static LocalPlayerUI Ui;public WindowManager windowManager=new WindowManager();public XUi xui=new XUi();public static LocalPlayerUI GetUIForPlayer(EntityPlayerLocal player)=>Ui;}
public class XUiC_RebirthCamperStationPicker {public Action<Guid> Select;public Action Close;public bool Retain;public bool IsOverviewCurrent=>!Retain;public Guid[] Ids;public void Bind(Guid[] ids,string[] names,Func<bool> current,Action<Guid> select,Action close,bool overview=false,Func<Guid,Installation> inspection=null){Ids=ids;Select=select;Close=()=>{if(!Retain)close();};}public void RetainLeaseForSelection(){Retain=true;}public void SelectInstallation(Guid id){if(id!=Guid.Empty)Select(id);}}
public static class RebirthCamperOverviewSnapshotPolicy {public static bool IsFresh(RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after)=>after!=null;}
public static class RebirthCamperStationSelectionIntent {public static Guid Take(EntityPlayerLocal p,EntityVehicle v)=>Guid.Empty;}
public class Model {public RebirthCamperStationState TileEntity;}
public class XUiC_WorkstationWindowGroup:Controller {public Model WorkstationData=new Model();}
public class XUiC_ItemStack:Controller {public XUi xui;public bool IsLocked;public bool Allowed=true;public int SlotNumber;public ItemStack ItemStack=new ItemStack{count=3};public bool CanSwap(ItemStack item)=>Allowed;}
public class XUiC_WorkstationFuelGrid {}public class XUiC_WorkstationToolGrid {}public class XUiC_WorkstationOutputGrid {}public class XUiC_WorkstationInputGrid {}public class XUiC_WorkstationMaterialInputGrid {}
public class XUiC_DragAndDropWindow {public ItemStack CurrentStack=new ItemStack{count=5};}
public static class RebirthCamperModuleInteraction {public static int Generation,Releases;public static bool Current(EntityVehicle v,EntityPlayerLocal p)=>true;public static Func<bool> CaptureCurrent(EntityVehicle v,EntityPlayerLocal p){int g=Generation;return()=>g==Generation;}public static Action CaptureRelease(EntityVehicle v,EntityPlayerLocal p){int g=Generation;return()=>{if(g==Generation)Releases++;};}}
public static class RebirthCamperStationAccess {public const string PickerWindow="picker";}
public struct RebirthCamperStationAddress {public RebirthCamperStationAddress(Guid v,Guid i){}}
public static class RebirthCamperStationWindowSession {public static Action Close;public static int RecoveryCloses;public static XUiC_WorkstationWindowGroup Window=new XUiC_WorkstationWindowGroup();public static void OpenRemote(EntityVehicle v,EntityPlayerLocal p,Installation i,RebirthCamperStationAddress a,Func<bool> current,Action release){i.State.BoundVehicle=v;Window.WorkstationData.TileEntity=i.State;Close=release;}public static XUiC_WorkstationWindowGroup RemoteWindow(EntityPlayerLocal p,RebirthCamperStationState s)=>Window;public static bool CloseRemoteForRecovery(EntityPlayerLocal p,RebirthCamperStationState s){if(!ReferenceEquals(Window.WorkstationData.TileEntity,s))return false;RecoveryCloses++;Close();return true;}}
public static class RebirthCamperStationQueueProjection {public static int Refreshes;public static void Refresh(XUiC_WorkstationWindowGroup w,RebirthCamperStationState s){Refreshes++;}}
public enum RebirthCamperRemoteStationGrid:byte {Fuel,Input,Tools,Output}public enum RebirthCamperRemoteStationGesture:byte {Swap,Half,One,Preferred}
public enum RebirthCamperRemoteStationCommandKind:byte {Craft,Cancel}
public class RebirthCamperRemoteStationCommand {public RebirthCamperRemoteStationCommandKind Kind;public Guid Job;public RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind k,Guid op,Guid job,string recipe,int batches,int tier){Kind=k;Job=job;}}
public static class RebirthCamperRemoteStationTransport {public static bool IsRegistered=true;public const byte QuerySession=0,OpenSession=1,StationCommand=2,CloseSession=3,FuelSwitch=4;public static Action<EntityVehicle,Guid,RebirthCamperTransferSnapshot,byte,bool> ReceiveSnapshot;public static Action<EntityVehicle,Guid,Guid,ItemStack,RebirthCamperStationState> ReceiveUpdate;}
public static class RebirthCamperRemoteManagement {public static int Requests,GridCloseCalls;public static bool ThrowGridClose,ReceiptPending=true;public static Action Recovery;public static Action<RebirthCamperTransferSnapshot> Updated;public static bool BeginStationGrid(EntityVehicle v,EntityPlayerLocal p,XUiC_DragAndDropWindow c,RebirthCamperTransferSnapshot s,Guid i,RebirthCamperRemoteStationGrid g,int slot,RebirthCamperRemoteStationGesture gesture,ItemStack expected,Func<bool> current,Action<RebirthCamperTransferSnapshot> updated,Action recovery,out Action close){Requests++;Updated=updated;Recovery=recovery;close=()=>{GridCloseCalls++;if(ThrowGridClose)throw new InvalidOperationException("grid close fault");};return true;}}
public class NetPackageRebirthCamperStationSession {public Guid Token;public byte Action;public RebirthCamperRemoteStationCommand Command;public NetPackageRebirthCamperStationSession Setup(int p,int v,Guid t,Guid vi,Guid si,byte a,RebirthCamperRemoteStationCommand c=null,bool burning=false){Token=t;Action=a;Command=c;return this;}}
public static class NetPackageManager {public static T GetPackage<T>() where T:new()=>new T();}
public class ConnectionManager {public int Sent,FailedCloseSends;public bool ThrowCloseSend;public NetPackageRebirthCamperStationSession Last;public void SendToServer(NetPackageRebirthCamperStationSession p,bool now){if(ThrowCloseSend&&p.Action==3){FailedCloseSends++;throw new InvalidOperationException("close transport fault");}Sent++;Last=p;}}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public class GameManager {public static GameManager Instance=new GameManager();public World World;public static void ShowTooltip(EntityPlayerLocal p,string s){}}
public static class Localization {public static string Get(string s)=>s;}
public static class Log {public static int Warnings;public static void Warning(string s){Warnings++;}}
public static class Fixture {
 public static XUiC_RebirthCamperStationPicker Picker;
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static NetPackageRebirthCamperStationSession Last=>SingletonMonoBehaviour<ConnectionManager>.Instance.Last;
 public static void Run(){
  var world=new World();var player=new EntityPlayerLocal{world=world};var vehicle=new EntityVehicle{world=world};world.Player=player;world.Vehicle=vehicle;GameManager.Instance.World=world;
  Picker=new XUiC_RebirthCamperStationPicker();var group=new XUiWindowGroup();group.Controller.Parts[typeof(XUiC_RebirthCamperStationPicker)]=Picker;LocalPlayerUI.Ui=new LocalPlayerUI();LocalPlayerUI.Ui.windowManager.Window=group;
  RebirthCamperRemoteStationClient.Install();RebirthCamperRemoteStationClient.OpenPicker(vehicle,player);var token=Last.Token;
  Check(LocalPlayerUI.Ui.windowManager.Modal&&LocalPlayerUI.Ui.windowManager.EscClosable,"Actual picker open suppressed native Escape close");
  var snapshot=new RebirthCamperTransferSnapshot();var state=new RebirthCamperStationState{InstallationId=Guid.NewGuid(),Revision=1};snapshot.Stations.Installed=new Installation{State=state};
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,Guid.NewGuid(),snapshot,0,true);Check(Picker.Ids[0]==Guid.Empty,"Wrong token admitted picker");
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,token,snapshot,0,true);Picker.Select(state.InstallationId);Check(Last.Action==1,"Selection did not request native server admission");
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,token,snapshot,1,true);Check(ReferenceEquals(RebirthCamperStationWindowSession.Window.WorkstationData.TileEntity,state)&&RebirthCamperModuleInteraction.Releases==0,"Picker transition lost native lease");
  var stack=new XUiC_ItemStack{xui=LocalPlayerUI.Ui.xui};stack.xui.playerUI.entityPlayer=player;stack.Parts[typeof(XUiC_WorkstationWindowGroup)]=RebirthCamperStationWindowSession.Window;stack.Parts[typeof(XUiC_WorkstationInputGrid)]=new XUiC_WorkstationInputGrid();stack.xui.Parts[typeof(XUiC_DragAndDropWindow)]=new XUiC_DragAndDropWindow();
  Check(!RebirthCamperRemoteStationClient.Transfer(stack,RebirthCamperRemoteStationGesture.Swap)&&RebirthCamperRemoteManagement.Requests==1&&stack.ItemStack.count==3,"Native grid callback mutated detached cell");
  RebirthCamperRemoteStationClient.Transfer(stack,RebirthCamperRemoteStationGesture.One);Check(RebirthCamperRemoteManagement.Requests==1,"Concurrent grid request escaped hold");
  int completed=0;int sent=SingletonMonoBehaviour<ConnectionManager>.Instance.Sent;
  Check(RebirthCamperRemoteStationClient.IsPending(player,state),"Switch failed to observe pending grid publication");
  bool refused=false;try{RebirthCamperRemoteStationClient.CloseForSwitch(player,state,()=>completed++);}catch(InvalidOperationException){refused=true;}
  Check(refused&&completed==0&&RebirthCamperModuleInteraction.Releases==0&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sent==sent,"Pending grid switched or released early");
  RebirthCamperRemoteManagement.Updated(snapshot);Check(RebirthCamperStationQueueProjection.Refreshes==1,"Applied grid failed native presentation");
  Check(RebirthCamperRemoteStationClient.Craft(player,state,new Recipe(),2,0)&&Last.Command.Kind==RebirthCamperRemoteStationCommandKind.Craft,"Craft did not dispatch bounded command");
  Check(!RebirthCamperRemoteStationClient.Cancel(player,state,Guid.NewGuid().ToString()),"Concurrent command escaped hold");
  Check(RebirthCamperRemoteStationClient.IsPending(player,state),"Switch failed to observe pending station command");
  sent=SingletonMonoBehaviour<ConnectionManager>.Instance.Sent;refused=false;
  try{RebirthCamperRemoteStationClient.CloseForSwitch(player,state,()=>completed++);}catch(InvalidOperationException){refused=true;}
  Check(refused&&completed==0&&RebirthCamperModuleInteraction.Releases==0&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sent==sent,"Pending command switched or released early");
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,token,snapshot,2,true);Check(RebirthCamperRemoteStationClient.Cancel(player,state,Guid.NewGuid().ToString())&&Last.Command.Kind==RebirthCamperRemoteStationCommandKind.Cancel,"Cancel did not dispatch original job");
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,token,snapshot,2,true);
  var next=new RebirthCamperStationState{InstallationId=state.InstallationId,Revision=3,isBurning=true};RebirthCamperRemoteStationTransport.ReceiveUpdate(vehicle,token,snapshot.Storage.VehicleIdentity,new ItemStack(),next);Check(state.Revision==3&&state.isBurning,"Authoritative update not projected");
  next.Revision=2;next.isBurning=false;RebirthCamperRemoteStationTransport.ReceiveUpdate(vehicle,token,snapshot.Storage.VehicleIdentity,new ItemStack(),next);Check(state.Revision==3&&state.isBurning,"Stale stream rewound station");
  Check(!RebirthCamperRemoteStationClient.IsPending(player,state),"Settled station remained pending");
  var close=RebirthCamperStationWindowSession.Close;
  RebirthCamperRemoteStationClient.CloseForSwitch(player,state,()=>completed++);
  Check(Last.Action==3&&completed==0&&RebirthCamperModuleInteraction.Releases==0,"Switch released native lease before close acknowledgement");
  sent=SingletonMonoBehaviour<ConnectionManager>.Instance.Sent;
  RebirthCamperRemoteStationClient.RetrySwitchClose(player,state);
  Check(SingletonMonoBehaviour<ConnectionManager>.Instance.Sent==sent,"Missing acknowledgement flooded close requests");
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,Guid.NewGuid(),snapshot,3,true);
  Check(completed==0&&RebirthCamperModuleInteraction.Releases==0,"Wrong close token released lease");
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,token,null,3,false);
  RebirthCamperRemoteStationClient.RetrySwitchClose(player,state);
  Check(completed==0&&RebirthCamperModuleInteraction.Releases==0&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sent==sent,"Rejected close released or retried before delay");
  UnityEngine.Time.realtimeSinceStartup=.6f;RebirthCamperRemoteStationClient.RetrySwitchClose(player,state);
  Check(SingletonMonoBehaviour<ConnectionManager>.Instance.Sent==sent+1&&Last.Token==token,"Rejected close did not retry exact token");
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,token,snapshot,3,true);
  Check(completed==1&&RebirthCamperModuleInteraction.Releases==1,"Acknowledged close failed one exact release");
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,token,snapshot,3,true);close();
  Check(completed==1&&RebirthCamperModuleInteraction.Releases==1,"Duplicate acknowledgement or delayed native close released twice");
  RebirthCamperModuleInteraction.Generation++;Picker.Retain=false;
  RebirthCamperRemoteStationClient.OpenPicker(vehicle,player);var replacement=Last.Token;
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,token,snapshot,3,true);close();
  Check(RebirthCamperModuleInteraction.Releases==1,"Late close released replacement native lease");
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,replacement,snapshot,0,true);Picker.Select(state.InstallationId);
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,replacement,snapshot,1,true);
  RebirthCamperStationWindowSession.Close();Check(RebirthCamperModuleInteraction.Releases==2&&Last.Action==3,"Ordinary close behavior changed after switching");
  Picker.Retain=false;RebirthCamperModuleInteraction.Generation++;
  RebirthCamperRemoteStationClient.OpenPicker(vehicle,player);
  LocalPlayerUI.Ui.windowManager.Escape();
  Check(!LocalPlayerUI.Ui.windowManager.Opened&&RebirthCamperModuleInteraction.Releases==3&&Last.Action==3,"Escape failed to close picker and release exact native lease");
  LocalPlayerUI.Ui.windowManager.Escape();Check(RebirthCamperModuleInteraction.Releases==3,"Repeated Escape released picker twice");
  // Fault schedules invoke the actual client Close callback; native window and
  // durable journal services are explicit doubles and are not certified here.
  Picker.Retain=false;RebirthCamperModuleInteraction.Generation++;
  RebirthCamperRemoteStationClient.OpenPicker(vehicle,player);var failureToken=Last.Token;
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,failureToken,snapshot,0,true);Picker.Select(state.InstallationId);
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,failureToken,snapshot,1,true);
  RebirthCamperRemoteStationClient.Transfer(stack,RebirthCamperRemoteStationGesture.Swap);
  var failedRecovery=RebirthCamperRemoteManagement.Recovery;var faultClose=RebirthCamperStationWindowSession.Close;
  RebirthCamperRemoteManagement.ThrowGridClose=true;sent=SingletonMonoBehaviour<ConnectionManager>.Instance.Sent;
  int releases=RebirthCamperModuleInteraction.Releases;int recoveryCloses=RebirthCamperStationWindowSession.RecoveryCloses;
  failedRecovery();
  Check(RebirthCamperModuleInteraction.Releases==releases+1&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sent==sent
   &&RebirthCamperRemoteManagement.ReceiptPending,"GridClose callback fault skipped exact native release or changed durable custody");
  failedRecovery();faultClose();
  Check(RebirthCamperModuleInteraction.Releases==releases+1&&RebirthCamperStationWindowSession.RecoveryCloses==recoveryCloses+1,"Duplicate recovery/close callback released or closed twice");
  RebirthCamperRemoteManagement.ThrowGridClose=false;Picker.Retain=false;RebirthCamperModuleInteraction.Generation++;
  RebirthCamperRemoteStationClient.OpenPicker(vehicle,player);var transportToken=Last.Token;
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,transportToken,snapshot,0,true);Picker.Select(state.InstallationId);
  RebirthCamperRemoteStationTransport.ReceiveSnapshot(vehicle,transportToken,snapshot,1,true);
  failedRecovery();faultClose();
  Check(RebirthCamperModuleInteraction.Releases==releases+1&&RebirthCamperStationWindowSession.RecoveryCloses==recoveryCloses+1,"Stale error callback closed replacement session");
  RebirthCamperRemoteStationClient.Transfer(stack,RebirthCamperRemoteStationGesture.One);
  var transportRecovery=RebirthCamperRemoteManagement.Recovery;var transportClose=RebirthCamperStationWindowSession.Close;
  SingletonMonoBehaviour<ConnectionManager>.Instance.ThrowCloseSend=true;transportRecovery();
  Check(RebirthCamperModuleInteraction.Releases==releases+2&&SingletonMonoBehaviour<ConnectionManager>.Instance.FailedCloseSends==1
   &&RebirthCamperRemoteManagement.ReceiptPending,"Close transport fault skipped native release or changed durable custody");
  transportRecovery();transportClose();
  Check(RebirthCamperModuleInteraction.Releases==releases+2&&SingletonMonoBehaviour<ConnectionManager>.Instance.FailedCloseSends==1,"Duplicate transport recovery retried closed session");
  SingletonMonoBehaviour<ConnectionManager>.Instance.ThrowCloseSend=false;
  Check(Log.Warnings>=2,"Callback/transport failures were not reported");
 }
}
'@
Add-Type -TypeDefinition ($fixture+"`n"+$source)
[Fixture]::Run()
Write-Output 'PASS actual remote client coordinator: token admission, picker transition, grid/command holds, projection, stale revisions, exact switch acknowledgement, rejection retry delay, duplicate/stale recovery safety and native release after grid-close callback/transport failure. Native/window coordinator/journal/network adapters doubled; multiplayer gameplay remains unqualified.'
