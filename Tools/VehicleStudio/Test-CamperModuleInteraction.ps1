$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperModuleInteraction.cs'))
$doubles=@'
namespace HarmonyLib {
 public sealed class HarmonyPatch:System.Attribute { public HarmonyPatch(System.Type type,string method){} }
 public static class AccessTools {
  public delegate ref F FieldRef<T,F>(T target);
  public static FieldRef<EntityVehicle,string> FieldRefAccess<T,F>(string name){return Pending;}
  static ref string Pending(EntityVehicle v){return ref v.Pending;}
 }
}
public static class ThreadManager {public static bool IsMainThread(){return true;}}
public sealed class World {public bool Remote;public EntityPlayerLocal Player;public bool IsRemote(){return Remote;}public EntityPlayerLocal GetPrimaryPlayer(){return Player;}}
public sealed class Inventory {public bool Busy;public bool IsHoldingItemActionRunning(){return Busy;}}
public sealed class EntityPlayerLocal {public Inventory inventory=new Inventory();}
public sealed class VehicleModel {public EntityVehicle CurrentVehicle;}
public sealed class XUi {public bool IsUsingItemActionEntryUse;public VehicleModel Vehicle=new VehicleModel();}
public sealed class LocalPlayerUI {public static LocalPlayerUI Ui=new LocalPlayerUI();public XUi xui=new XUi();public static LocalPlayerUI GetUIForPlayer(EntityPlayerLocal p){return Ui;}}
public sealed class EntityVehicle {
 public string Pending; public World world=new World();public bool Driven; public int Stops;
 public bool IsDriven(){return Driven;}public void StopUIInteraction(){Stops++;LockManager.Instance.Held=null;}
}
public sealed class LockManager {
 public static LockManager Instance=new LockManager(); public EntityVehicle Held; public int Requests;public System.Action<EntityVehicle> Granted;
 public bool IsLockedByLocalPlayer(EntityVehicle v,ushort channel){return object.ReferenceEquals(v,Held);}
 public void LockRequestLocal(EntityVehicle v,ushort channel){Requests++;if(Granted!=null){Held=v;Granted(v);}}
}
public static class RebirthCamperRemoteStationClient {public static bool IsReady;}
public static class RebirthCamperRemoteStationTransport {public static bool IsRegistered;}
public static class RebirthCamperRadial {
 public const string Craft="craft";
 public static bool AllowedValue=true;public static System.Action<EntityVehicle,EntityPlayerLocal,string> OpenSession;
 public static bool Allowed(EntityVehicle v,EntityPlayerLocal p){return AllowedValue;}
 public static bool IsCommand(System.ReadOnlySpan<char> command){return command.ToString()=="storage";}
}
public sealed class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
public static class CamperInteractionChecks {
 static void Reject(System.Action action){try{action();}catch(System.InvalidOperationException){return;}throw new System.Exception("Unheld native grant admitted.");}
 public static void Run(){
  var player=new EntityPlayerLocal();var vehicle=new EntityVehicle();int opens=0;
  RebirthCamperRadial.OpenSession=(v,p,c)=>{if(!RebirthCamperModuleInteraction.Current(v,p))throw new System.Exception("Open preceded native lease.");opens++;};
  RebirthCamperModuleInteraction.Request(vehicle,player,"storage");
  if(LockManager.Instance.Requests!=1||vehicle.Pending!="storage"||opens!=0)throw new System.Exception("Request opened before native grant.");
  Reject(()=>RebirthCamperModuleInteraction.Granted(vehicle,player,"storage"));
  LockManager.Instance.Held=vehicle;RebirthCamperModuleInteraction.Granted(vehicle,player,"storage");
  var oldRelease=RebirthCamperModuleInteraction.CaptureRelease(vehicle,player);
  var oldCurrent=RebirthCamperModuleInteraction.CaptureCurrent(vehicle,player);
  RebirthCamperModuleInteraction.Request(vehicle,player,"storage");
  if(opens!=1||LockManager.Instance.Requests!=1)throw new System.Exception("Duplicate native lease requested.");
  RebirthCamperModuleInteraction.Release(vehicle,player);RebirthCamperModuleInteraction.Release(vehicle,player);
  if(vehicle.Stops!=1||RebirthCamperModuleInteraction.Current(vehicle,player))throw new System.Exception("Lease close unlocked twice or remained active.");
  LockManager.Instance.Held=vehicle;RebirthCamperRadial.OpenSession=(v,p,c)=>{throw new System.InvalidOperationException("open fault");};
  Reject(()=>RebirthCamperModuleInteraction.Granted(vehicle,player,"storage"));
  if(vehicle.Stops!=2)throw new System.Exception("Open fault stranded native lock.");
  RebirthCamperRadial.OpenSession=(v,p,c)=>{};LockManager.Instance.Held=vehicle;
  RebirthCamperModuleInteraction.Granted(vehicle,player,"storage");
  oldRelease();
  if(oldCurrent()||vehicle.Stops!=2||!RebirthCamperModuleInteraction.Current(vehicle,player))throw new System.Exception("Late close released a replacement lease.");
  LockManager.Instance.Held=new EntityVehicle();RebirthCamperModuleInteraction.Release(vehicle,player);
  if(vehicle.Stops!=2||LockManager.Instance.Held==null)throw new System.Exception("Close unlocked a different native interaction.");
  vehicle.Driven=true;RebirthCamperModuleInteraction.Request(vehicle,player,"storage");
  vehicle.Driven=false;vehicle.world.Remote=true;RebirthCamperModuleInteraction.Request(vehicle,player,"storage");
  if(LockManager.Instance.Requests!=2)throw new System.Exception("Moving vehicle requested management or remote management did not request native grant.");
  RebirthCamperModuleInteraction.Request(vehicle,player,"craft");
  if(LockManager.Instance.Requests!=2)throw new System.Exception("Unsupported remote crafting use requested a lease.");
  RebirthCamperRemoteStationClient.IsReady=true;RebirthCamperRemoteStationTransport.IsRegistered=true;
  RebirthCamperModuleInteraction.Request(vehicle,player,"craft");
  if(LockManager.Instance.Requests!=3)throw new System.Exception("Complete remote crafting adapters did not request native lease.");
  RebirthCamperRadial.AllowedValue=false;RebirthCamperModuleInteraction.Request(vehicle,player,"storage");
  if(LockManager.Instance.Requests!=3)throw new System.Exception("Unauthorized remote management requested a native lease.");
  // Native local/server dispatch can grant synchronously inside Request. The
  // actual coordinator must enter the picker with the lock and vehicle model
  // already bound, and unwind it if opening throws in that same callback.
  var synchronous=new EntityVehicle();RebirthCamperRadial.AllowedValue=true;int synchronousOpens=0;
  RebirthCamperRadial.OpenSession=(v,p,c)=>{if(!RebirthCamperModuleInteraction.Current(v,p)||!object.ReferenceEquals(LocalPlayerUI.Ui.xui.Vehicle.CurrentVehicle,v))throw new System.Exception("Synchronous grant published before native authority/model");synchronousOpens++;};
  LockManager.Instance.Granted=v=>RebirthCamperModuleInteraction.Granted(v,player,v.Pending);
  RebirthCamperModuleInteraction.Request(synchronous,player,"storage");
  if(synchronousOpens!=1||LockManager.Instance.Requests!=4)throw new System.Exception("Synchronous request did not open one native session");
  RebirthCamperModuleInteraction.Release(synchronous,player);
  RebirthCamperRadial.OpenSession=(v,p,c)=>{throw new System.InvalidOperationException("synchronous picker failure");};
  Reject(()=>RebirthCamperModuleInteraction.Request(synchronous,player,"storage"));
  if(synchronous.Stops!=2||LockManager.Instance.Held!=null||RebirthCamperModuleInteraction.Current(synchronous,player))throw new System.Exception("Synchronous picker failure stranded native lock or coordinator lease");
 }
}
'@
Add-Type -TypeDefinition ($source+"`n"+$doubles)
[CamperInteractionChecks]::Run()
Write-Output 'PASS: actual native lease coordinator waits for grant, preserves exact leases, supports synchronous local native grant, binds vehicle model before picker, and releases on synchronous picker failure. Existing remote/moving/admission regressions retained. Native adapters are doubles; no gameplay acceptance.'
