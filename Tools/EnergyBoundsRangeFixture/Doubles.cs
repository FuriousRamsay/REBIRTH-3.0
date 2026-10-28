// Explicit native identity/world/background-cache/projection doubles. No disk,
// networking, native rendering/claims/physics or initialized game ABI evidence.
namespace UnityEngine {public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public float sqrMagnitude=>x*x+y*y+z*z;public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);}}
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public class EntityPlayer {public int entityId;public World world;public bool Dead;public bool IsDead()=>Dead;}
public sealed class EntityVehicle {public int entityId;public World world;public string Owner="owner";public UnityEngine.Vector3 position;public string GetOwner()=>Owner;}
public sealed class World {public bool Remote;public Dictionary<int,object> Entities=new();public bool IsRemote()=>Remote;public object GetEntity(int id)=>Entities.GetValueOrDefault(id);}
public sealed class PersistentPlayerData {public string PrimaryId;public int EntityId;}
public sealed class Players {public Dictionary<string,PersistentPlayerData> Entries=new();public PersistentPlayerData GetPlayerData(string id)=>Entries.GetValueOrDefault(id);public PersistentPlayerData GetPlayerDataFromEntityID(int id)=>Entries.Values.FirstOrDefault(p=>p.EntityId==id);}
public sealed class GameManager {public static GameManager Instance=new();public World World;public Players Players=new();public Players GetPersistentPlayerList()=>Players;}
public sealed class ConnectionManager {public bool IsServer=true;}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new();}
public static class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld()=>Enabled;}
public static class RebirthCharacterCreationHoldService {public static HashSet<EntityPlayer> Held=new();public static bool IsHeld(EntityPlayer actor)=>Held.Contains(actor);}
public sealed class RebirthStablePlayerIdentity {public string StorageKey;public static bool Allowed=true;public static bool TryFromPersistentPlayer(PersistentPlayerData owner,out RebirthStablePlayerIdentity identity){identity=owner?.PrimaryId==null?null:new(){StorageKey=owner.PrimaryId};return Allowed&&identity!=null;}}
public sealed class OriginRecord {public string BackgroundId;}
public sealed class RebirthWorldCharacterRecord {public string StablePlayerKey;public bool IsComplete=true,Current=true;public OriginRecord Origin=new();}
public static class RebirthWorldCharacterRepository {public static Dictionary<string,RebirthWorldCharacterRecord> Cache=new();public static bool TryGetCurrentCached(RebirthStablePlayerIdentity identity,out RebirthWorldCharacterRecord record){record=Cache.GetValueOrDefault(identity.StorageKey);return record?.IsComplete==true;}public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord record)=>record?.Current==true&&Cache.GetValueOrDefault(record.StablePlayerKey)==record;}
// Current live actor/cache association boundary, never a production policy copy.
public static class RebirthWorldCharacterService {public static bool TryGet(EntityPlayer player,out RebirthWorldCharacterRecord record){var data=GameManager.Instance.Players.GetPlayerDataFromEntityID(player.entityId);record=data==null?null:RebirthWorldCharacterRepository.Cache.GetValueOrDefault(data.PrimaryId);return record!=null;}}
public static class RebirthCamperClaimProjection {public static bool Available;public static float Radius;public static bool TryReadRadius(EntityVehicle vehicle,out float radius){radius=Available?Radius:0;return Available;}}
public sealed class ItemStack {public ItemStack Clone()=>new();public static ItemStack[] Clone(ItemStack[] value)=>(ItemStack[])value.Clone();}
