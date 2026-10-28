using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
// AUTHORED_NOT_RUN. Actual transport/admission/envelope/fuel bodies. Native locks,
// entities, persistence, codecs and network calls below are explicit adapters.
namespace UnityEngine.Scripting {public class PreserveAttribute:Attribute{}}
namespace UnityEngine {public static class Time {public static float realtimeSinceStartup;}}
public interface IMemoryPoolableObject{void Reset();void Cleanup();}
public interface INetConnection{bool IsDisconnected();}
public class NativeConnection:INetConnection{public bool Disconnected;public bool IsDisconnected()=>Disconnected;}
public class ClientInfo{public int entityId=1;public object InternalId=new object();public bool loginDone=true,disconnecting;public Action OnSend;public INetConnection[] netConnection={new NativeConnection()};public void SendPackage(NetPackage p){OnSend?.Invoke();SingletonMonoBehaviour<ConnectionManager>.Instance.Sent++;}}
public class Clients{public ClientInfo Peer;public ClientInfo ForEntityId(int id)=>id==1?Peer:null;}
public class ConnectionManager{public bool IsServer=true;public Clients Clients=new Clients();public int Sent;public void SendPackage(NetPackage p,int _attachedToEntityId){Sent++;}}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
public class WorldState{public string Guid="world";}
public class World{public bool Remote;public WorldState worldState=new WorldState();public EntityPlayer Player;public EntityVehicle Vehicle;public bool IsRemote()=>Remote;public object GetEntity(int id)=>id==1?(object)Player:id==2?Vehicle:null;}
public class GameManager{public static GameManager Instance;public World World;public void StartCoroutine(IEnumerator i){}}
public struct Position{public float sqrMagnitude;public static Position operator-(Position a,Position b)=>new Position{sqrMagnitude=0};}
public class EntityPlayer{public int entityId=1,Died;public World world;public bool Dead;public string Creation="creation";public Position position;public bool IsDead()=>Dead;}
public class VehicleData{public object OwnerId;}
public class Grid{public ItemStack[] items=Array.Empty<ItemStack>();}
public class Bag{public Grid ItemGrid=new Grid();}
public class EntityVehicle{public int entityId=2;public World world;public Position position;public Bag bag=new Bag();public VehicleData vehicle=new VehicleData();public bool IsLocked()=>false;public bool hasLock()=>false;public bool isAllowedUser(object user)=>false;}
public class ItemStack{public static ItemStack Empty=new ItemStack();}
public class RebirthCamperStationState{public EntityVehicle BoundVehicle;public Guid InstallationId;public long Revision;public bool bUserAccessing,IsBurning;public int Resets;public void ResetTickTime(){Resets++;}}
public class Installed{public Guid Id;public ItemStack InstalledItem=new ItemStack();public RebirthCamperStationState State=new RebirthCamperStationState();}
public class Stations{public Guid Id=Guid.NewGuid();public Installed Installed=new Installed();public Installed Find(Guid id)=>id==Id?Installed:null;}
public class RebirthCamperModuleRuntime{public static RebirthCamperModuleRuntime Value;public Guid Identity=Guid.NewGuid();public bool TransferPending;public object Storage=new object();public Stations Stations=new Stations();public int Saves;public Action OnPersist;public static RebirthCamperModuleRuntime For(EntityVehicle v)=>Value;public void Persist(bool force,int purpose=0){Saves++;OnPersist?.Invoke();}}
public struct RebirthCamperStationAddress{public Guid Vehicle,Installation;public RebirthCamperStationAddress(Guid v,Guid i){Vehicle=v;Installation=i;}public bool IsValid=>Vehicle!=Guid.Empty&&Installation!=Guid.Empty;public override bool Equals(object o)=>o is RebirthCamperStationAddress a&&a.Vehicle==Vehicle&&a.Installation==Installation;public override int GetHashCode()=>Vehicle.GetHashCode()^Installation.GetHashCode();}
public static class RebirthCamperPhysicalInteraction{public static Action OnTake;public static Func<bool> TakeServerGuard(EntityVehicle v,EntityPlayer p,Guid id){OnTake?.Invoke();return ()=>true;}}
public static class RebirthCamperStationWindowSession{public static bool IsInUse(RebirthCamperStationState s)=>false;}
public static class RebirthCamperStationGridCustody{public static bool Held;public static bool IsHeld(RebirthCamperStationState s)=>Held;}
public static class RebirthCamperRemoteTransport{public static void AssertFrame(byte[] b){}}
public static class RebirthCamperStationSnapshot{public static Action OnCapture;public static byte[] Capture(Guid id,RebirthCamperStationState state){OnCapture?.Invoke();return new byte[]{1};}}
public static class RebirthCamperInventorySnapshot{public static Action OnCapture;public static byte[] Capture(ItemStack[] items){OnCapture?.Invoke();return new byte[]{1};}}
public class NetPackageRebirthCamperStationUpdate:NetPackage{public NetPackageRebirthCamperStationUpdate Setup(int v,Guid t,Guid id,Guid station,byte[] item,byte[] state)=>this;}
public class RebirthCamperTransferSnapshot{public static Action OnCapture;public static int Captures;public static byte[] Capture(object storage,Stations stations,ItemStack[] bag,ItemStack cursor){Captures++;OnCapture?.Invoke();return new byte[]{1};}public static RebirthCamperTransferSnapshot Restore(Guid id,byte[] b)=>new RebirthCamperTransferSnapshot();}
public static class Log{public static void Warning(string s){}}
public static class RebirthCamperRadial{public static bool IsCamper(EntityVehicle v)=>true;public static bool IsDriver(EntityVehicle v,EntityPlayer p)=>false;}
public class LockManager{public static LockManager Instance=new LockManager();public class LockEntry{public LockEntry(EntityVehicle v,int channel){}}}
public class NativeLocks{public bool TryGetByValue(LockManager.LockEntry key,out int owner){owner=1;return true;}}
public static class RebirthCamperRemoteAdmission{static readonly Func<LockManager,NativeLocks> locks=m=>new NativeLocks();
// ACTUAL_ADMISSION
}
public enum NetPackageDirection{ToServer,ToClient}
public class PooledBinaryReader:BinaryReader{public PooledBinaryReader(Stream s):base(s){}}
public class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream s):base(s){}}
public class NetPackage{public ClientInfo Sender;public bool EntityValid=true;public virtual NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){}public virtual void ProcessPackage(World w,GameManager m){}public bool ValidEntityIdForSender(int id)=>EntityValid;}
public static class NetPackageManager{public static T GetPackage<T>() where T:new()=>new T();}
public static class CamperFrame{public static byte[] Read(PooledBinaryReader r)=>r.ReadBytes(r.ReadInt32());public static void Write(PooledBinaryWriter w,byte[] bytes){w.Write(bytes.Length);w.Write(bytes);}}
public class NetPackageRebirthCamperStationCommandResultV2:NetPackage{public NetPackageRebirthCamperStationCommandResultV2 Setup(int v,Guid t,Guid i,Guid op,bool ok,byte[] state,byte action)=>this;}
public static class RebirthCamperRemoteStationCommandStatus{public enum State{Unknown,Pending,Applied,Rejected}public sealed class Observation{public bool AuthorizesCommandRetirement;}public static bool Supported,Authorizes=true;public static State Result;public static Action OnRead;public static bool HasNativeOriginal(RebirthCamperRemoteStationCommand c)=>Supported&&c!=null;public static State ReadStatus(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q,out Observation result){OnRead?.Invoke();result=new Observation{AuthorizesCommandRetirement=Authorizes};return Result;}}
public class NetPackageRebirthCamperStationCommandStatusResult:NetPackage{public NetPackageRebirthCamperStationCommandStatusResult Setup(int v,Guid t,Guid i,Guid station,RebirthCamperRemoteStationCommand q,RebirthCamperRemoteStationCommandStatus.State status,byte[] bytes)=>this;}
public static class RebirthCamperRemoteStationCommandStatusStore{public sealed class FreshAdmission{}public enum AdmissionDisposition{NewlyPublished,ExistingStatusOnly}public static int QueryRefusals;public static bool TryRefuseUnreceived(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q){QueryRefusals++;return true;}public static bool TryAdmit(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q,out object result,out AdmissionDisposition d,out FreshAdmission fresh){result=null;d=AdmissionDisposition.NewlyPublished;fresh=new FreshAdmission();return true;}public static bool InvokeFresh(FreshAdmission fresh,Func<bool> current,Func<bool> action)=>current()&&action();}
// ACTUAL_COMMAND
// ACTUAL_TRANSPORT
// ACTUAL_ENVELOPE
// ACTUAL_FUEL_PACKAGE
// ACTUAL_FUEL_STATE
public static partial class RebirthCamperRemoteStationTransport{
 // ACTUAL_STATUS_QUERY
 // ACTUAL_WATCH
 internal static IEnumerator FixtureWatch(object lease)=>Watch((Lease)lease);
 internal static void FixtureObserve(Action<EntityPlayer,RebirthCamperStationState> callback){observeTerminal=callback;}
 internal static void FixtureClear(){leases.Clear();}
}
public static class Program{
 static EntityPlayer player;static EntityVehicle vehicle;static World world;static ClientInfo peer;static GameManager manager;static ConnectionManager connection;static RebirthCamperModuleRuntime runtime;static RebirthCamperStationAddress address;static Guid token;static object lease;static int Commands;static Action OnCommand;
 static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Setup(){
  RebirthCamperRemoteStationTransport.FixtureClear();ThreadManager.Main=true;Commands=0;OnCommand=null;
  RebirthCamperRemoteStationTransport.FixtureObserve(null);Time.realtimeSinceStartup=10;RebirthCamperStationSnapshot.OnCapture=RebirthCamperInventorySnapshot.OnCapture=null;
  RebirthCamperRemoteStationCommandStatus.Supported=false;RebirthCamperRemoteStationCommandStatus.OnRead=null;RebirthCamperRemoteStationCommandStatus.Result=RebirthCamperRemoteStationCommandStatus.State.Unknown;RebirthCamperRemoteStationCommandStatus.Authorizes=true;
  RebirthCamperStationGridCustody.Held=false;RebirthCamperPhysicalInteraction.OnTake=null;RebirthCamperTransferSnapshot.OnCapture=null;RebirthCamperTransferSnapshot.Captures=0;
  world=new World();player=new EntityPlayer{world=world};vehicle=new EntityVehicle{world=world};world.Player=player;world.Vehicle=vehicle;
  manager=new GameManager{World=world};GameManager.Instance=manager;connection=new ConnectionManager();peer=new ClientInfo();connection.Clients.Peer=peer;SingletonMonoBehaviour<ConnectionManager>.Instance=connection;
  runtime=new RebirthCamperModuleRuntime();RebirthCamperModuleRuntime.Value=runtime;address=new RebirthCamperStationAddress(runtime.Identity,runtime.Stations.Id);token=Guid.NewGuid();
  runtime.Stations.Installed.Id=address.Installation;runtime.Stations.Installed.State.InstallationId=address.Installation;runtime.Stations.Installed.State.BoundVehicle=vehicle;
  lease=RebirthCamperRemoteStationTransport.Admit(vehicle,player,peer,address,token);
  Need(RebirthCamperRemoteStationTransport.IsCurrent(player,address,player.Creation,lease),"fresh actual lease unavailable");
 }
 static void Break(int n){switch(n){
  case 0:player.Died++;break;case 1:player.Dead=true;break;case 2:player.Creation="replacement";break;
  case 3:peer.netConnection[0]=new NativeConnection();break;case 4:((NativeConnection)peer.netConnection[0]).Disconnected=true;break;
  case 5:peer.netConnection=Array.Empty<INetConnection>();break;case 6:peer.netConnection=null;break;
  case 7:peer.loginDone=false;break;case 8:peer.disconnecting=true;break;case 9:connection.Clients.Peer=new ClientInfo();break;
  case 10:SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();break;case 11:connection.IsServer=false;break;
  case 12:GameManager.Instance=new GameManager{World=world};break;case 13:manager.World=new World();break;
  case 14:world.worldState=new WorldState();break;case 15:world.worldState.Guid="replacement";break;
  case 16:world.Player=new EntityPlayer{world=world};break;case 17:world.Vehicle=new EntityVehicle{world=world};break;
  case 18:player.world=new World();break;case 19:vehicle.world=new World();break;case 20:ThreadManager.Main=false;break;
 }}
 static RebirthCamperRemoteStationCommand Command()=>new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.Craft,Guid.NewGuid(),Guid.NewGuid(),"recipe",1,0);
 static void Dispatch(byte action,RebirthCamperRemoteStationCommand command=null,Guid? operation=null){RebirthCamperRemoteStationTransport.HandleSession(world,peer,1,2,token,address.Vehicle,address.Installation,action,command,true,operation??Guid.Empty);}
 public static void Main(){
  // Dispatcher/service adapter only; the real fuel original is composed in
  // mp071_fuel_original. This cohort exercises the actual envelope lifecycle.
  RebirthCamperRemoteStationTransport.Register(p=>p.Creation,(p,a,c,l,q)=>{Commands++;OnCommand?.Invoke();if(q.IsFuel){RebirthCamperStationFuelState.Apply(runtime.Stations.Installed.State,q.Kind==RebirthCamperRemoteStationCommandKind.FuelOn);runtime.Persist(true);}return true;});
  for(int n=0;n<21;n++){
   Setup();Break(n);Need(!RebirthCamperRemoteStationTransport.IsCurrent(player,address,"creation",lease),"expired lease survived "+n);
   Dispatch(RebirthCamperRemoteStationTransport.StationCommand,Command());Need(Commands==0&&runtime.Saves==0,"expired command mutated "+n);
   Dispatch(RebirthCamperRemoteStationTransport.FuelSwitch,null,Guid.NewGuid());Need(!runtime.Stations.Installed.State.IsBurning&&runtime.Saves==0,"expired fuel mutated "+n);
  }
  Setup();Dispatch(RebirthCamperRemoteStationTransport.StationCommand,Command());Need(Commands==1&&connection.Sent==1,"normal command lost");
  Setup();Dispatch(RebirthCamperRemoteStationTransport.FuelSwitch,null,Guid.NewGuid());Need(runtime.Stations.Installed.State.IsBurning&&runtime.Saves==1&&connection.Sent==1,"normal fuel lost");
  Dispatch(RebirthCamperRemoteStationTransport.FuelSwitch,null,Guid.NewGuid());Need(runtime.Stations.Installed.State.Resets==1,"same state reset native fuel clock");
  Setup();Dispatch(RebirthCamperRemoteStationTransport.FuelSwitch);Need(runtime.Saves==0&&!runtime.Stations.Installed.State.IsBurning,"uncorrelated fuel mutated");
  for(int n=0;n<20;n++){
   Setup();int vector=n;OnCommand=()=>Break(vector);Dispatch(RebirthCamperRemoteStationTransport.StationCommand,Command());Need(Commands==1&&connection.Sent==0,"stale command postimage sent "+n);
   Setup();runtime.OnPersist=()=>Break(vector);Dispatch(RebirthCamperRemoteStationTransport.FuelSwitch,null,Guid.NewGuid());Need(runtime.Saves==1&&connection.Sent==0,"stale fuel postimage sent "+n);
  }
  Setup();RebirthCamperTransferSnapshot.OnCapture=()=>player.Died++;Dispatch(RebirthCamperRemoteStationTransport.StationCommand,Command());Need(connection.Sent==0,"reentrant snapshot delivered across life");
  Setup();RebirthCamperStationGridCustody.Held=true;Dispatch(RebirthCamperRemoteStationTransport.CloseSession);Need(runtime.Saves==0&&RebirthCamperRemoteStationTransport.IsCurrent(player,address,player.Creation,lease),"held close released original");
  RebirthCamperStationGridCustody.Held=false;Dispatch(RebirthCamperRemoteStationTransport.CloseSession);Need(runtime.Saves==1&&!RebirthCamperRemoteStationTransport.IsCurrent(player,address,player.Creation,lease),"normal close retained lease");
  Setup();runtime.OnPersist=()=>{throw new IOException("native persistence failure");};Dispatch(RebirthCamperRemoteStationTransport.CloseSession);Need(RebirthCamperRemoteStationTransport.IsCurrent(player,address,player.Creation,lease),"failed close released lease");
  Setup();RebirthCamperRemoteStationTransport.Release(player,token);RebirthCamperPhysicalInteraction.OnTake=()=>player.Died++;bool refused=false;try{RebirthCamperRemoteStationTransport.Admit(vehicle,player,peer,address,token);}catch(InvalidOperationException){refused=true;}Need(refused&&!RebirthCamperRemoteStationTransport.TryGetCurrent(player,out _,out _,out _,out _),"admission rebound changed life");
  Setup();var legacy=new NetPackageRebirthCamperStationSession().Setup(1,2,token,address.Vehicle,address.Installation,RebirthCamperRemoteStationTransport.StationCommand,Command());legacy.Sender=peer;legacy.ProcessPackage(world,new GameManager{World=world});Need(Commands==0,"foreign legacy callback dispatched");legacy.ProcessPackage(world,manager);Need(Commands==1,"current legacy callback lost");legacy.Reset();legacy.ProcessPackage(world,manager);Need(Commands==1,"pooled legacy command retained");
  Setup();var fuel=new NetPackageRebirthCamperFuelSwitchV2().Setup(1,2,token,address.Vehicle,address.Installation,Guid.NewGuid(),true);fuel.Sender=peer;fuel.ProcessPackage(world,new GameManager{World=world});Need(runtime.Saves==0,"foreign fuel callback dispatched");fuel.EntityValid=false;fuel.ProcessPackage(world,manager);Need(runtime.Saves==0,"foreign sender fuel dispatched");fuel.EntityValid=true;fuel.ProcessPackage(world,manager);Need(runtime.Saves==1,"current fuel callback lost");fuel.Reset();fuel.ProcessPackage(world,manager);Need(runtime.Saves==1,"pooled fuel retained");
  foreach(RebirthCamperRemoteStationCommandStatus.State outcome in Enum.GetValues(typeof(RebirthCamperRemoteStationCommandStatus.State))){Setup();RebirthCamperRemoteStationCommandStatus.Supported=true;RebirthCamperRemoteStationCommandStatus.Result=outcome;RebirthCamperRemoteStationTransport.QueryOriginalStatus(world,peer,1,2,token,address.Vehicle,address.Installation,Command());Need(connection.Sent==1&&Commands==0&&runtime.Saves==0,"status query replayed effect or failed typed response");}
  Setup();RebirthCamperRemoteStationCommandStatus.Supported=true;RebirthCamperRemoteStationCommandStatus.Result=RebirthCamperRemoteStationCommandStatus.State.Rejected;int refusedQueries=RebirthCamperRemoteStationCommandStatusStore.QueryRefusals;
  RebirthCamperRemoteStationTransport.QueryOriginalStatus(world,peer,1,2,token,address.Vehicle,address.Installation,Command(),true);Need(RebirthCamperRemoteStationCommandStatusStore.QueryRefusals==refusedQueries+1&&Commands==0&&runtime.Saves==0&&connection.Sent==1,"qualified status-only declaration bypassed refusal store or entered native effects");
  Setup();RebirthCamperRemoteStationCommandStatus.Supported=true;refusedQueries=RebirthCamperRemoteStationCommandStatusStore.QueryRefusals;RebirthCamperRemoteStationTransport.QueryOriginalStatus(world,peer,1,2,Guid.NewGuid(),address.Vehicle,address.Installation,Command(),true);Need(RebirthCamperRemoteStationCommandStatusStore.QueryRefusals==refusedQueries&&Commands==0,"foreign status declaration reached original reservation");
  for(int n=0;n<21;n++){Setup();RebirthCamperRemoteStationCommandStatus.Supported=true;int vector=n;RebirthCamperRemoteStationCommandStatus.OnRead=()=>Break(vector);RebirthCamperRemoteStationTransport.QueryOriginalStatus(world,peer,1,2,token,address.Vehicle,address.Installation,Command());Need(connection.Sent==0&&Commands==0&&runtime.Saves==0,"status crossed changed scope "+n);}
  Setup();RebirthCamperRemoteStationCommandStatus.Supported=true;RebirthCamperRemoteStationCommandStatus.Result=RebirthCamperRemoteStationCommandStatus.State.Applied;RebirthCamperRemoteStationCommandStatus.Authorizes=false;RebirthCamperRemoteStationTransport.QueryOriginalStatus(world,peer,1,2,token,address.Vehicle,address.Installation,Command());Need(connection.Sent==0,"unqualified terminal status sent");
  Setup();RebirthCamperRemoteStationCommandStatus.Supported=true;RebirthCamperRemoteStationCommandStatus.Result=RebirthCamperRemoteStationCommandStatus.State.Applied;RebirthCamperTransferSnapshot.OnCapture=()=>RebirthCamperModuleRuntime.Value=new RebirthCamperModuleRuntime{Identity=runtime.Identity,Stations=runtime.Stations};RebirthCamperRemoteStationTransport.QueryOriginalStatus(world,peer,1,2,token,address.Vehicle,address.Installation,Command());Need(connection.Sent==0,"old runtime status snapshot crossed same-ID runtime replacement");
  WatchCases();
  Console.WriteLine("PASS actual camper session admission/life/connection/dispatch/close/status/watch source vectors; native persistence and remote topology are separate evidence");
 }
 static void WatchCases(){
  Setup();var watch=RebirthCamperRemoteStationTransport.FixtureWatch(lease);Need(watch.MoveNext()&&connection.Sent==1,"normal watcher did not publish");Time.realtimeSinceStartup+=.1f;Need(watch.MoveNext()&&connection.Sent==1,"watch ignored cadence");Time.realtimeSinceStartup+=.3f;Need(watch.MoveNext()&&connection.Sent==1,"unchanged revision republished");runtime.Stations.Installed.State.Revision++;Time.realtimeSinceStartup+=.3f;Need(watch.MoveNext()&&connection.Sent==2,"changed revision lost");((IDisposable)watch).Dispose();Need(!RebirthCamperRemoteStationTransport.TryGetCurrent(player,out _,out _,out _,out _),"disposed watcher retained lease");
  for(int n=0;n<21;n++){
   Setup();int vector=n;RebirthCamperRemoteStationTransport.FixtureObserve((p,s)=>Break(vector));watch=RebirthCamperRemoteStationTransport.FixtureWatch(lease);Need(!watch.MoveNext()&&connection.Sent==0,"terminal callback scope crossed watcher "+n);
   Setup();RebirthCamperStationSnapshot.OnCapture=()=>Break(vector);watch=RebirthCamperRemoteStationTransport.FixtureWatch(lease);Need(!watch.MoveNext()&&connection.Sent==0,"station capture scope crossed watcher "+n);
   Setup();RebirthCamperInventorySnapshot.OnCapture=()=>Break(vector);watch=RebirthCamperRemoteStationTransport.FixtureWatch(lease);Need(!watch.MoveNext()&&connection.Sent==0,"item capture scope crossed watcher "+n);
  }
  Setup();RebirthCamperStationSnapshot.OnCapture=()=>{throw new IOException("native snapshot transient");};watch=RebirthCamperRemoteStationTransport.FixtureWatch(lease);Need(watch.MoveNext()&&connection.Sent==0&&RebirthCamperRemoteStationTransport.IsCurrent(player,address,player.Creation,lease),"transient snapshot error destroyed live lease");RebirthCamperStationSnapshot.OnCapture=null;Time.realtimeSinceStartup+=1;Need(watch.MoveNext()&&connection.Sent==1,"snapshot publication did not recover on same watcher");((IDisposable)watch).Dispose();
  Setup();peer.OnSend=()=>{throw new IOException("native send transient");};watch=RebirthCamperRemoteStationTransport.FixtureWatch(lease);Need(watch.MoveNext()&&connection.Sent==0&&RebirthCamperRemoteStationTransport.IsCurrent(player,address,player.Creation,lease),"transient send lost lease");peer.OnSend=null;Time.realtimeSinceStartup+=1;Need(watch.MoveNext()&&connection.Sent==1,"same watcher did not retry read-only send");((IDisposable)watch).Dispose();
  Setup();RebirthCamperStationSnapshot.OnCapture=()=>runtime.Stations.Installed.State.Revision++;watch=RebirthCamperRemoteStationTransport.FixtureWatch(lease);Need(watch.MoveNext()&&connection.Sent==0,"mixed revision snapshot sent");RebirthCamperStationSnapshot.OnCapture=null;Time.realtimeSinceStartup+=1;Need(watch.MoveNext()&&connection.Sent==1,"stable next revision not published");((IDisposable)watch).Dispose();
  Setup();RebirthCamperInventorySnapshot.OnCapture=()=>RebirthCamperModuleRuntime.Value=new RebirthCamperModuleRuntime{Identity=runtime.Identity,Stations=runtime.Stations};watch=RebirthCamperRemoteStationTransport.FixtureWatch(lease);Need(!watch.MoveNext()&&connection.Sent==0,"same-ID runtime replacement crossed watcher");
  Setup();RebirthCamperRemoteStationTransport.FixtureObserve((p,s)=>{RebirthCamperRemoteStationTransport.Release(player,token);token=Guid.NewGuid();lease=RebirthCamperRemoteStationTransport.Admit(vehicle,player,peer,address,token);});watch=RebirthCamperRemoteStationTransport.FixtureWatch(lease);Need(!watch.MoveNext()&&RebirthCamperRemoteStationTransport.IsCurrent(player,address,player.Creation,lease),"old watcher erased replacement lease");
 }
}
