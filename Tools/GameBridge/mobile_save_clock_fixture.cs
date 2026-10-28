using System;
using System.Collections.Generic;
class IRebirthStationSnapshotByteBudget {}
static class ThreadManager {public static bool IsMainThread()=>true;}
namespace UnityEngine {static class Time {public static float realtimeSinceStartup=1;}}
class ItemStack {}
class RebirthCamperStationAddress {public bool IsValid=true;public Guid Installation=Guid.NewGuid();}
class EntityVehicle {public RebirthCamperStationState State;}
class RebirthCamperStationState {public Guid InstallationId=Guid.NewGuid();public ulong lastTickTime=42;public EntityVehicle BoundVehicle;}
class Installation {public ItemStack InstalledItem=new ItemStack();public RebirthCamperStationState State;}
static class RebirthCamperCraftingStation {public const int ModuleSlots=10;}
class Stations {public Installation Find(Guid id)=>new Installation();public Installation GetSlot(int slot)=>null;}
class RebirthCamperModuleRuntime {
 public Stations Stations=new Stations();private EntityVehicle vehicle;
 public static RebirthCamperModuleRuntime For(EntityVehicle v)=>new RebirthCamperModuleRuntime{vehicle=v};
 public void Persist(bool force){if(vehicle.State.lastTickTime!=0)MobileSaveClockFixture.PersistedNonzero=true;}
 public void PersistProcessingRecovery(IRebirthStationSnapshotByteBudget b,out IDisposable a){a=null;Persist(true);}
}
static class RebirthCamperStationSnapshot {public static byte[] CaptureCharged(Guid id,RebirthCamperStationState s,IRebirthStationSnapshotByteBudget b,out IDisposable a){a=null;return BitConverter.GetBytes(s.lastTickTime);}}
static class RebirthCamperSavedVehicle {
 public static object RequestSave(EntityVehicle v,IRebirthStationSnapshotByteBudget b){v.State.lastTickTime=0;return new object();}
}
public static class MobileSaveClockFixture {
 static Dictionary<RebirthCamperStationState,Request> pending=new Dictionary<RebirthCamperStationState,Request>();
 public static bool PersistedNonzero;
 class Request:IDisposable {
  public IRebirthStationSnapshotByteBudget Budget;public EntityVehicle Vehicle;public RebirthCamperStationState State;public RebirthCamperStationAddress Address;
  public float Deadline;public Func<bool> CurrentSession;public ItemStack InstalledItem;public byte[] Snapshot;public object Expected;
  public IDisposable ModuleAllocation,ItemAllocation,SnapshotAllocation;
  public void Dispose(){pending.Remove(State);}
 }
 static void RegisterVehiclePending(EntityVehicle vehicle,Request request){}
 static bool IsPending(RebirthCamperStationState s)=>pending.ContainsKey(s);
 static ItemStack CloneInstalled(ItemStack i,IRebirthStationSnapshotByteBudget b,out IDisposable a){a=null;return i;}
 static bool Current(Request r)=>r.CurrentSession()&&r.State.lastTickTime==BitConverter.ToUInt64(r.Snapshot,0);
 // ACTUAL_BEGIN
 // OLD_COUNTEREXAMPLE
 public static void Run(){
  var oldState=new RebirthCamperStationState();oldState.BoundVehicle=new EntityVehicle{State=oldState};bool failed=false;
  try{BeginCoreBeforeFix(oldState,new RebirthCamperStationAddress(),()=>true);}catch(InvalidOperationException){failed=true;}
  if(!failed)throw new Exception("Original ordering did not reproduce self-invalidating snapshot");
  PersistedNonzero=false;
  var state=new RebirthCamperStationState();state.BoundVehicle=new EntityVehicle{State=state};
  using(var request=BeginCore(state,new RebirthCamperStationAddress(),()=>true)){
   if(PersistedNonzero||BitConverter.ToUInt64(request.Snapshot,0)!=0||!Current(request))throw new Exception("Snapshot clock changed under pending tick");
  }
  var refused=new RebirthCamperStationState();refused.BoundVehicle=new EntityVehicle{State=refused};
  try{BeginCore(refused,new RebirthCamperStationAddress(),()=>false);throw new Exception("Unauthorized request entered");}catch(InvalidOperationException){}
  if(refused.lastTickTime!=42)throw new Exception("Unauthorized request changed clock");
  Console.WriteLine("PASS actual BeginCore with explicit native clock/save adapters: original ordering fails, fixed snapshot survives pending tick, refusal leaves clock unchanged. Native gameplay NOT RUN.");
 }
}
