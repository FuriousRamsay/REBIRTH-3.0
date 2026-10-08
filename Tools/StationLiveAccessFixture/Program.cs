using System;
using System.Collections.Generic;
public struct Vector3i {public int x,y,z;public Vector ToVector3()=>new Vector();}
public struct Vector {public float sqrMagnitude;public static Vector operator -(Vector a,Vector b)=>a;}
public class EntityPlayer {public World world;public int entityId=1;public Vector position;public bool Dead;public bool IsDead()=>Dead;}
public class TileEntityWorkstation {public bool Shared;public bool IsSharedLock(int channel)=>Shared;}
public class World {
 public EntityPlayer Player;public TileEntityWorkstation Station=new();
 public EntityPlayer GetEntity(int id)=>Player;
 public object GetTileEntity(Vector3i pos)=>Station;
 public EntityPlayer GetPrimaryPlayer()=>Player;
}
public class GameManager {
 public static GameManager Instance=new();public World World;
 public PlayerList GetPersistentPlayerList()=>new();public PlayerData GetPersistentLocalPlayer()=>new();
}
public class PlayerList {public PlayerData GetPlayerDataFromEntityID(int id)=>new();}
public class PlayerData {public object PrimaryId=new();}
public class Identity {public string StorageKey="key",CanonicalId="id";}
public class Origin {public string CreationId="creation";}
public class RebirthWorldCharacterRecord {public object Progression=new();public bool IsComplete=true;public Origin Origin=new();public string StablePlayerKey="key",StablePlayerId="id";}
public static class RebirthWorldCharacterService {
 public static RebirthWorldCharacterRecord Record;public static Identity Identity;
 public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return r!=null;}
 public static bool TryGetIdentity(EntityPlayer p,out Identity i){i=Identity;return i!=null;}
}
public static class RebirthWorldCharacterRepository {public static bool Current=true;public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>Current;}
public static class RebirthStationObservationDispatcher {public static bool Authority=true;public static bool IsCurrentAuthorityThread(World w)=>Authority&&w!=null;}
public static class RebirthSurvivorRequestScope {public static bool Matches(string a,string b)=>a==b;}
public enum RebirthSecureAccessPurpose {WorkstationAccess}
public static class RebirthWorkstationSecurityService {
 public static bool Allowed=true;
 public static bool CanAccessWorkstation(World w,Vector3i v,EntityPlayer p,object id,bool unused,RebirthSecureAccessPurpose purpose,out string reason){reason="";return Allowed;}
}
public class LockEntry {public object Target;public int Channel;}
public class LockTable {
 public List<LockEntry> Entries;
 public bool TryGetByKey(int id,out List<LockEntry> e){e=Entries;return e!=null;}
}
public class LockManager {public static LockManager Instance;public LockTable singleLocks=new();}
class Program {
 static int count;
 static EntityPlayer Setup() {
  var w=new World();var p=new EntityPlayer{world=w};w.Player=p;GameManager.Instance.World=w;
  RebirthWorldCharacterService.Record=new();RebirthWorldCharacterService.Identity=new();
  RebirthWorldCharacterRepository.Current=true;RebirthStationObservationDispatcher.Authority=true;
  RebirthWorkstationSecurityService.Allowed=true;
  LockManager.Instance=new();LockManager.Instance.singleLocks.Entries=new(){new LockEntry{Target=w.Station,Channel=0}};
  return p;
 }
 static void Check(bool value,string why){if(!value)throw new Exception(why);count++;Console.WriteLine("PASS "+why);}
 static bool Resolve(EntityPlayer p) {
  var ok=RebirthStationLiveAccess.TryResolve(p,new Vector3i(),"creation",out var station,out var record);
  if(!ok&&(station!=null||record!=null))throw new Exception("Failed admission leaked outputs");
  return ok;
 }
 static void Main() {
  var p=Setup();Check(Resolve(p),"valid exact owner station access");
  p=Setup();RebirthWorldCharacterRepository.Current=false;Check(!Resolve(p),"stale cached character refused");
  p=Setup();RebirthWorldCharacterService.Identity.StorageKey="other";Check(!Resolve(p),"storage identity mismatch refused");
  p=Setup();RebirthWorldCharacterService.Identity.CanonicalId="other";Check(!Resolve(p),"canonical identity mismatch refused");
  p=Setup();RebirthWorldCharacterService.Identity=null;Check(!Resolve(p),"missing identity refused");
  foreach(float distance in new[]{float.NaN,float.PositiveInfinity,65f}) {
   p=Setup();p.position=new Vector{sqrMagnitude=distance};Check(!Resolve(p),"invalid or outside distance "+distance);
  }
  p=Setup();p.position=new Vector{sqrMagnitude=64};Check(Resolve(p),"native eight metre boundary admitted");
  p=Setup();LockManager.Instance.singleLocks.Entries[0].Channel=1;Check(!Resolve(p),"wrong lock channel refused");
  p=Setup();LockManager.Instance.singleLocks.Entries[0].Target=new TileEntityWorkstation();Check(!Resolve(p),"different locked station refused");
  p=Setup();p.world.Station.Shared=true;Check(!Resolve(p),"shared station lock refused");
  p=Setup();RebirthWorkstationSecurityService.Allowed=false;Check(!Resolve(p),"security denial respected");
  p=Setup();RebirthStationObservationDispatcher.Authority=false;Check(!Resolve(p),"authority thread denial respected");
  p=Setup();p.world.Player=new EntityPlayer{world=p.world};Check(!Resolve(p),"replaced native player object refused");
  p=Setup();RebirthWorldCharacterService.Record.Origin.CreationId="other";Check(!Resolve(p),"changed survivor creation refused");
  Console.WriteLine(count+" actual production gate checks; native world, identity, lock, distance and security doubles.");
 }
}