$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$root=Join-Path $PSScriptRoot '../..'
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperRemoteStationTransport.cs'))
$updates=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperRemoteStationUpdates.cs'))
$updates=$updates.Substring(0,$updates.IndexOf('[Preserve]'))
$envelope=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/RebirthCamperRemoteStationEnvelope.cs'))
$envelope=$envelope.Substring(0,$envelope.IndexOf('[Preserve]'))
$envelope=[regex]::Replace($envelope,'(?m)^using [^;]+;\r?\n','')
$source=[regex]::Replace($source,'(?m)^using [^;]+;\r?\n','')
$updates=[regex]::Replace($updates,'(?m)^using [^;]+;\r?\n','')
$fixture=@'
using System;using System.Collections;using System.Collections.Generic;using UnityEngine;
namespace UnityEngine {public static class Time {public static float realtimeSinceStartup;}}
public static class ThreadManager {public static bool IsMainThread()=>true;}
public class World {public EntityPlayer Player;public EntityVehicle Vehicle;public object GetEntity(int id)=>id==8?(object)Player:Vehicle;public bool IsRemote()=>false;}
public class EntityPlayer {public int entityId=8;}
public class Bag {public Grid ItemGrid=new Grid();}public class Grid {public ItemStack[] items=new ItemStack[0];}
public class EntityVehicle {public World world;public int entityId=7;public Bag bag=new Bag();}
public struct RebirthCamperStationAddress {public Guid Vehicle,Installation;public bool IsValid=>Vehicle!=Guid.Empty&&Installation!=Guid.Empty;public RebirthCamperStationAddress(Guid v,Guid i){Vehicle=v;Installation=i;}}
public class ItemStack {public static ItemStack Empty=new ItemStack();}
public class RebirthCamperStationState {public EntityVehicle BoundVehicle;public Guid InstallationId;public bool bUserAccessing=true;public bool IsBurning;public long Revision=1;public void ResetTickTime(){}}
public class Installation {public Guid Id=>State.InstallationId;public ItemStack InstalledItem=new ItemStack();public RebirthCamperStationState State;}
public class Stations {public Installation Installed;public Installation Find(Guid id)=>Installed!=null&&Installed.Id==id?Installed:null;}
public class RebirthCamperModuleRuntime {public int Persists;public bool FailPersist;public object Storage=new object();public Guid Identity=Guid.NewGuid();public bool TransferPending;public Stations Stations=new Stations();public static RebirthCamperModuleRuntime Runtime=new RebirthCamperModuleRuntime();public static RebirthCamperModuleRuntime For(EntityVehicle v)=>Runtime;public void Persist(bool force,ushort flags){if(FailPersist)throw new InvalidOperationException("Persist failure");Persists++;}}
public class ClientInfo {public int entityId=8;public int Packets;public void SendPackage(object p){Packets++;}}
public class Clients {public ClientInfo Sender;public ClientInfo ForEntityId(int id)=>Sender;}
public class ConnectionManager {public Clients Clients=new Clients();public NetPackageRebirthCamperStationSessionResult Last;public void SendPackage(NetPackageRebirthCamperStationSessionResult p,int _attachedToEntityId){Last=p;}}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public static class RebirthCamperRemoteAdmission {public static bool Allowed=true;public static bool CanAccess(EntityVehicle v,EntityPlayer p,ClientInfo c,Func<bool> auth)=>Allowed&&auth();}
public static class RebirthCamperStationWindowSession {public static bool IsInUse(RebirthCamperStationState s)=>false;}
public class RebirthCamperRemoteStationCommand {}
public class NetPackageRebirthCamperStationSession {}public class NetPackageRebirthCamperStationSessionResult {public bool Accepted;public Guid Token;public NetPackageRebirthCamperStationSessionResult Setup(int v,Guid t,Guid identity,byte action,bool accepted,byte[] snapshot){Token=t;Accepted=accepted;return this;}}
public class RebirthCamperTransferSnapshot {public static byte[] Capture(object storage,Stations stations,ItemStack[] items,ItemStack cursor)=>new byte[]{1};}
public static class RebirthCamperStationGridCustody {public static bool Held;public static bool IsHeld(RebirthCamperStationState state)=>Held;}
public class NetPackageRebirthCamperStationUpdate {public object Setup(int v,Guid t,Guid i,Guid s,byte[] item,byte[] snapshot)=>this;}
public static class NetPackageManager {public static T GetPackage<T>() where T:new()=>new T();}
public class GameManager {public static GameManager Instance=new GameManager();public World World;public List<IEnumerator> Watches=new List<IEnumerator>();public void StartCoroutine(IEnumerator task){Watches.Add(task);}}
public static class RebirthCamperStationSnapshot {public static byte[] Capture(Guid id,RebirthCamperStationState state)=>new byte[]{1};}
public static class RebirthCamperInventorySnapshot {public static byte[] Capture(ItemStack[] items)=>new byte[]{1};}
public static class RebirthCamperRemoteTransport {public static void AssertFrame(byte[] frame){}}
public static class Log {public static void Warning(string message){}}
public static class LeaseFixture {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static void Run(){
  var world=new World();var vehicle=new EntityVehicle{world=world};var player=new EntityPlayer();var sender=new ClientInfo();var runtime=RebirthCamperModuleRuntime.Runtime;
  world.Player=player;world.Vehicle=vehicle;
  var state=new RebirthCamperStationState{BoundVehicle=vehicle,InstallationId=Guid.NewGuid()};runtime.Stations.Installed=new Installation{State=state};GameManager.Instance.World=world;SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Sender=sender;
  var address=new RebirthCamperStationAddress(runtime.Identity,state.InstallationId);int commands=0,observed=0;
  RebirthCamperRemoteStationTransport.ReceiveUpdate=(v,t,i,item,s)=>{};
  RebirthCamperRemoteStationTransport.ReceiveSnapshot=(v,t,s,a,ok)=>{};
  RebirthCamperRemoteStationTransport.Register(p=>"creation",(p,a,c,l,r)=>{commands++;return true;},(p,s)=>observed++);
  var token=Guid.NewGuid();var identity=RebirthCamperRemoteStationTransport.Admit(vehicle,player,sender,address,token);
  Check(RebirthCamperRemoteStationTransport.TryResolve(player,address,"creation",out var resolved,out var lease)&&ReferenceEquals(state,resolved)&&ReferenceEquals(identity,lease)&&!state.bUserAccessing,"Exact server lease failed/native tick flag held");
  Check(!RebirthCamperRemoteStationTransport.TryResolve(new EntityPlayer(),address,"creation",out _,out _),"Reused player id authenticated another object");
  var watch=GameManager.Instance.Watches[0];Check(watch.MoveNext()&&sender.Packets==1&&observed==1,"Initial selected-station stream/recovery observation absent");
  Time.realtimeSinceStartup=.3f;watch.MoveNext();Check(sender.Packets==1&&observed==1,"Unchanged snapshot/recovery flooded native network");
  state.Revision++;Time.realtimeSinceStartup=.6f;watch.MoveNext();Check(sender.Packets==2,"Station revision did not publish selected state");
  runtime.TransferPending=true;Time.realtimeSinceStartup=1.2f;Check(watch.MoveNext()&&!RebirthCamperRemoteStationTransport.IsCurrent(player,address,"creation",identity)&&sender.Packets==2,"Pending cursor custody admitted command or stream");
  runtime.TransferPending=false;Time.realtimeSinceStartup=1.5f;watch.MoveNext();Check(RebirthCamperRemoteStationTransport.IsCurrent(player,address,"creation",identity)&&observed==2,"Temporary custody discarded native lease/recovery poll");
  Check(RebirthCamperRemoteStationTransport.Execute(player,token,new RebirthCamperRemoteStationCommand())&&commands==1,"Current command failed");
  RebirthCamperRemoteStationTransport.Release(player,token);Check(!RebirthCamperRemoteStationTransport.IsCurrent(player,address,"creation",identity),"Released opaque lease retained authority");
  var replacement=Guid.NewGuid();var next=RebirthCamperRemoteStationTransport.Admit(vehicle,player,sender,address,replacement);
  RebirthCamperRemoteStationTransport.Release(player,token);Check(RebirthCamperRemoteStationTransport.IsCurrent(player,address,"creation",next),"Late close removed replacement session");
  var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
  RebirthCamperStationGridCustody.Held=true;
  RebirthCamperRemoteStationTransport.HandleSession(world,sender,8,7,replacement,runtime.Identity,state.InstallationId,3,null);
  Check(!connection.Last.Accepted&&runtime.Persists==0&&RebirthCamperRemoteStationTransport.IsCurrent(player,address,"creation",next),"Save custody acknowledged close or released server lease");
  RebirthCamperStationGridCustody.Held=false;runtime.FailPersist=true;
  RebirthCamperRemoteStationTransport.HandleSession(world,sender,8,7,replacement,runtime.Identity,state.InstallationId,3,null);
  Check(!connection.Last.Accepted&&runtime.Persists==0&&RebirthCamperRemoteStationTransport.IsCurrent(player,address,"creation",next),"Failed persist acknowledged close or released server lease");
  runtime.FailPersist=false;
  RebirthCamperRemoteStationTransport.HandleSession(world,sender,8,7,replacement,runtime.Identity,state.InstallationId,3,null);
  Check(connection.Last.Accepted&&runtime.Persists==1&&!RebirthCamperRemoteStationTransport.IsCurrent(player,address,"creation",next),"Close did not persist before acknowledgement/release");
  RebirthCamperRemoteStationTransport.HandleSession(world,sender,8,7,replacement,runtime.Identity,state.InstallationId,3,null);
  Check(connection.Last.Accepted&&runtime.Persists==1,"Duplicate close repeated persistence");
  var thirdToken=Guid.NewGuid();next=RebirthCamperRemoteStationTransport.Admit(vehicle,player,sender,address,thirdToken);
  RebirthCamperRemoteStationTransport.HandleSession(world,sender,8,7,replacement,runtime.Identity,state.InstallationId,3,null);
  Check(runtime.Persists==1&&RebirthCamperRemoteStationTransport.IsCurrent(player,address,"creation",next),"Old close token persisted or released replacement station");
  SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Sender=new ClientInfo();Check(!RebirthCamperRemoteStationTransport.IsCurrent(player,address,"creation",next),"Changed native connection retained authority");
 }
}
'@
Add-Type -TypeDefinition ($fixture+"`n"+$source+"`n"+$updates+"`n"+$envelope)
[LeaseFixture]::Run()
Write-Output 'PASS actual remote server lease/stream/envelope: exact actor, revision stream, custody hold/resume; CloseSession rejects held custody and failed persist, persists before release/ack, duplicate and stale closes preserve replacement lease. Native/network/persist adapters doubled; gameplay unqualified.'
