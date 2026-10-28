// Actual production reward access/update methods and fire replay comparator are inserted.
// Native world/entities, registry and notifications are deliberately doubled.
using System;using System.Collections.Generic;
struct Vector3i{public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
class Entity{public int entityId,entityClass,spawnById;public string spawnByName;public bool spawnByAllowShare;public World world;public Vector3i position;public bool Unloading;public bool IsMarkedForUnload()=>Unloading;}
class EntityPlayer:Entity{public string Stable;}
class EntityLootContainer:Entity{public bool bRemoved;}
class EntitySupplyCrate:Entity{public object Contents;}
enum EnumRemoveEntityReason{Despawned}
class Chunk{public bool InProgressUnloading;}
struct BlockValue{public bool isair;public static BlockValue Air=>new BlockValue{isair=true};}
struct BlockValueRef{public Vector3i p;public static explicit operator BlockValueRef(Vector3i p)=>new BlockValueRef{p=p};}
class World{public ulong worldTime;public bool Remote;public Chunk Chunk;public bool IsRemote()=>Remote;public Dictionary<int,Entity> Entities=new Dictionary<int,Entity>();public Entity GetEntity(int id){Entity e;return Entities.TryGetValue(id,out e)?e:null;}public void RemoveEntity(int id,EnumRemoveEntityReason reason){Entities.Remove(id);}public static Vector3i worldToBlockPos(Vector3i p)=>p;public Chunk GetChunkFromWorldPos(int x,int z)=>Chunk;public BlockValue Block;public BlockValue GetBlock(Vector3i p)=>Block;public void SetBlockRPC(BlockValueRef p,BlockValue value){Block=value;}}
static class EntityClass{public static int FromString(string s)=>7;}
class Progression{public string OwnerStableId,OwnerDisplayName;}
class RebirthBossSpawnOriginal{public int Role;public byte Phase;public string NativeProof="";public bool RemovalInvoking,NativeRemoved;}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
class GameManager{public static GameManager Instance=new GameManager();public World World;}
static class RebirthBossNativePublication{public static bool Matching=true,RemovalAllowed=true;public static bool AllowOriginalLootLock(Entity entity)=>true;public static bool Matches(World w,RebirthBossEventInstance e,RebirthBossSpawnOriginal o,Entity value)=>Matching;public static bool BeginRemoval(World w,RebirthBossEventInstance e,RebirthBossSpawnOriginal o,Entity value)=>RemovalAllowed;}
static class Log{public static void Warning(string message){}}
class RebirthBossEventInstance{public Guid EventId=Guid.NewGuid();public string FailureReason;public List<RebirthBossSpawnOriginal> SpawnOriginals=new List<RebirthBossSpawnOriginal>();public bool RewardIssued,RewardBlockPlaced;public int RewardEntityId;public ulong RewardExpiresWorldTime;public Vector3i RewardBlockPosition;public Progression Progression;}
static class RebirthBossEventRegistry{public static List<RebirthBossEventInstance> Events=new List<RebirthBossEventInstance>();public static List<RebirthBossEventInstance> SnapshotEvents()=>Events;}
static class RebirthBossEventPersistence{public static bool IsReady=true;public static void MarkDirty(){}}
static class RebirthBossEventIdentity{public static string GetStableId(EntityPlayer p)=>p.Stable;public static EntityPlayer FindPlayer(World w,string id){foreach(var e in w.Entities.Values)if(e is EntityPlayer&&((EntityPlayer)e).Stable==id)return (EntityPlayer)e;return null;}}
static class Reward{
 const string RewardCrateClassName="RB_EventLootContainer";
 static bool IsOwnedLegacyRewardBlock(RebirthBossEventInstance e,Vector3i p,BlockValue b)=>!b.isair;
 static void MarkRemoved(World w,RebirthBossEventInstance e){e.RewardBlockPlaced=false;e.RewardEntityId=-1;}
 // PRODUCTION_REWARD_METHODS
}
struct RebirthFireDeltaEntry{public int Value;}
static class Fire{
 // PRODUCTION_PENDING_DELTA
 public static void Verify(){var entries=new List<PendingDelta>();for(int i=0;i<100;i++)entries.Add(new PendingDelta{Revision=12,ArrivalOrder=i,Entry=new RebirthFireDeltaEntry{Value=i}});entries.Sort(delegate(PendingDelta a,PendingDelta b){/* PRODUCTION_COMPARATOR */});for(int i=0;i<entries.Count;i++)if(entries[i].Entry.Value!=i)throw new Exception("same batch source order lost");}
}
static class Program{
 static void Need(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Main(){Fire.Verify();Need(true,"actual replay comparator preserves equal-revision arrival order");var w=new World{worldTime=10};var owner=new EntityPlayer{entityId=50,Stable="stable-owner",world=w};var other=new EntityPlayer{entityId=51,Stable="other",world=w};w.Entities[50]=owner;w.Entities[51]=other;var crate=new EntityLootContainer{entityId=80,entityClass=7,spawnById=-1,world=w};w.Entities[80]=crate;var e=new RebirthBossEventInstance{RewardIssued=true,RewardBlockPlaced=true,RewardEntityId=80,RewardExpiresWorldTime=20,Progression=new Progression{OwnerStableId="stable-owner"}};RebirthBossEventRegistry.Events.Add(e);GameManager.Instance.World=w;var original=new RebirthBossSpawnOriginal{Role=3,Phase=3,NativeProof=new string('a',64)};e.SpawnOriginals.Add(original);
 e.SpawnOriginals.Clear();Need(!Reward.AllowRewardLock(crate,50),"legacy numeric ID without published original fails closed");e.SpawnOriginals.Add(original);
 original.NativeProof="";Need(!Reward.AllowRewardLock(crate,50),"missing native publication proof fails closed");original.NativeProof=new string('a',64);
 GameManager.Instance.World=new World();Need(!Reward.AllowRewardLock(crate,50),"foreign current world cannot borrow reward access");GameManager.Instance.World=w;
 ThreadManager.Main=false;Need(!Reward.AllowRewardLock(crate,50),"off-thread reward access refused");ThreadManager.Main=true;
 Need(!Reward.AllowRewardLock(crate,51)&&Reward.AllowRewardLock(crate,50)&&crate.spawnById==50,"native disk omission is rebound only for stable owner");w.Entities.Remove(50);owner.entityId=60;w.Entities[60]=owner;Need(Reward.AllowRewardLock(crate,60)&&crate.spawnById==60,"reconnected owner new entity ID admitted");
 original.Phase=2;Need(!Reward.AllowRewardLock(crate,60),"unpublished original cannot open");e.SpawnOriginals[0].Phase=3;RebirthBossNativePublication.Matching=false;Need(!Reward.AllowRewardLock(crate,60),"reused native ID cannot borrow event permission");RebirthBossNativePublication.Matching=true;e.SpawnOriginals[0].RemovalInvoking=true;Need(!Reward.AllowRewardLock(crate,60),"retiring original cannot reopen");original.RemovalInvoking=false;
 w.Entities.Remove(80);w.worldTime=25;Reward.Update(w,e);Need(e.RewardBlockPlaced&&e.RewardEntityId==80,"unloaded expiry retains durable removal obligation");w.Entities[80]=crate;Need(!Reward.AllowRewardLock(crate,60),"expired reloaded reward cannot open");Reward.Update(w,e);Need(!e.RewardBlockPlaced&&w.GetEntity(80)==null,"loaded expiry removes exact native reward");
 var orphan=new EntityLootContainer{entityId=90,entityClass=7,world=w};Need(!Reward.AllowRewardLock(orphan,60),"orphan event crate fails closed");var ordinary=new EntityLootContainer{entityId=91,entityClass=8,world=w};Need(Reward.AllowRewardLock(ordinary,51),"ordinary crate unchanged");
 var contents=new object();var legacy=new EntitySupplyCrate{entityId=100,world=w,Contents=contents};w.Entities[100]=legacy;e.RewardBlockPlaced=true;e.RewardEntityId=100;e.RewardExpiresWorldTime=40;Reward.Update(w,e);Need(ReferenceEquals(legacy.Contents,contents)&&ReferenceEquals(w.GetEntity(100),legacy),"legacy native contents retained in place");RebirthBossEventPersistence.IsReady=false;Need(!Reward.AllowRewardLock(legacy,60),"failed persistence recovery denies event loot");Console.WriteLine("Native objects/coordinator doubled; gameplay and topology NOT RUN.");}
}
