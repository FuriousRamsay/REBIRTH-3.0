$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperRemoteAdmission.cs'))
$doubles=@'
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread(){return Main;}}
public sealed class User {public string Id;public override bool Equals(object value){return value is User&&((User)value).Id==Id;}public override int GetHashCode(){return Id.GetHashCode();}}
public struct Vec {public float x;public float sqrMagnitude{get{return x*x;}}public static Vec operator -(Vec a,Vec b){return new Vec{x=a.x-b.x};}}
public sealed class World {public bool Remote;public System.Collections.Generic.Dictionary<int,object> Entities=new System.Collections.Generic.Dictionary<int,object>();public bool IsRemote(){return Remote;}public object GetEntity(int id){object e;return Entities.TryGetValue(id,out e)?e:null;}}
public sealed class Vehicle {public User OwnerId;}
public sealed class EntityVehicle {public Vehicle vehicle=new Vehicle();public World world;public int entityId;public Vec position;public bool Locked,Driven,Allowed;
 public bool IsLocked(){return Locked;}public bool IsDriven(){return Driven;}public bool hasLock(){return true;}public bool isAllowedUser(User u){return Allowed;}}
public sealed class EntityPlayer {public World world;public int entityId;public Vec position;public bool Dead;public bool IsDead(){return Dead;}}
public sealed class ClientInfo {public User InternalId;public int entityId;}
public static class RebirthCamperRadial {public static bool IsCamper(EntityVehicle v){return true;}}
public sealed class OneToManyDictionary<TKey,TValue> {public System.Collections.Generic.Dictionary<TValue,TKey> Owners=new System.Collections.Generic.Dictionary<TValue,TKey>();public bool TryGetByValue(TValue value,out TKey key){return Owners.TryGetValue(value,out key);}}
public sealed class LockManager {
 public static LockManager Instance=new LockManager();public OneToManyDictionary<int,LockEntry> Map=new OneToManyDictionary<int,LockEntry>();
 public struct LockEntry {public EntityVehicle Target;public ushort Channel;public LockEntry(EntityVehicle target,ushort channel){Target=target;Channel=channel;}}
}
namespace HarmonyLib {public static class AccessTools {
 public delegate ref F FieldRef<T,F>(T target);
 public static FieldRef<LockManager,OneToManyDictionary<int,LockManager.LockEntry>> FieldRefAccess<T,F>(string name){return Map;}
 static ref OneToManyDictionary<int,LockManager.LockEntry> Map(LockManager target){return ref target.Map;}
}}
public static class CamperRemoteChecks {
 static void Check(bool wanted,EntityVehicle v,EntityPlayer p,ClientInfo sender,System.Func<bool> auth){if(RebirthCamperRemoteAdmission.CanAccess(v,p,sender,auth)!=wanted)throw new System.Exception("Remote native admission mismatch.");}
 public static void Run(){
  var world=new World();var user=new User{Id="owner"};var v=new EntityVehicle{world=world,entityId=1,Locked=true};v.vehicle.OwnerId=user;
  var p=new EntityPlayer{world=world,entityId=2};var sender=new ClientInfo{entityId=2,InternalId=user};world.Entities[1]=v;world.Entities[2]=p;
  System.Func<bool> auth=()=>true;Check(false,v,p,sender,auth);
  var entry=new LockManager.LockEntry(v,0);LockManager.Instance.Map.Owners[entry]=3;Check(false,v,p,sender,auth);
  LockManager.Instance.Map.Owners[entry]=2;Check(true,v,p,sender,auth);
  sender.InternalId=new User{Id="visitor"};Check(false,v,p,sender,auth);v.Allowed=true;Check(true,v,p,sender,auth);
  v.Driven=true;Check(false,v,p,sender,auth);v.Driven=false;
  p.position=new Vec{x=9};Check(false,v,p,sender,auth);p.position=new Vec();
  sender.entityId=4;Check(false,v,p,sender,auth);sender.entityId=2;
  world.Entities[1]=new EntityVehicle();Check(false,v,p,sender,auth);world.Entities[1]=v;
  world.Remote=true;Check(false,v,p,sender,auth);world.Remote=false;
  ThreadManager.Main=false;Check(false,v,p,sender,auth);ThreadManager.Main=true;
  Check(false,v,p,sender,()=>false);int reads=0;Check(false,v,p,sender,()=>++reads==1);
 }
}
'@
Add-Type -TypeDefinition ($source+"`n"+$doubles)
[CamperRemoteChecks]::Run()
Write-Output 'PASS: production remote admission requires exact native lock actor/channel, sender entity, native owner/allowlist permissions, alive world identity, distance, stationary vehicle and stable authenticated character. Native adapters are explicit doubles; no multiplayer gameplay acceptance.'
