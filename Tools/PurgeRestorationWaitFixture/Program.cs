using System;
using System.Collections.Generic;

// Source fixture: real production witness/connection, minimal native state doubles.
// This exercises admission invariants; it does not certify in-game execution.
class EntityAlive { public int entityId,entityClass=1;public World world;public bool dead;public Guid Token;public bool IsDead()=>dead; }
class EntityClass { public string entityClassName;public static Dictionary<int,EntityClass> list=new(){{1,new EntityClass{entityClassName="zombieArlene"}}}; }
class World { public Dictionary<int,EntityAlive> Entities=new();public EntityAlive GetEntity(int id)=>Entities.GetValueOrDefault(id);public bool IsRemote()=>false; }
class GameManager { public static GameManager Instance=new();public World World; }
static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
class SleeperVolume
{
    public struct RespawnData { public string className;public int spawnPointIndex; }
    public static int TickSpawnCount;
    public int spawnDelay=1,numSpawned=2;
    public List<int> respawnList=new(){4,10},spawnsAvailable=new(){0,1,9};
    public Dictionary<int,RespawnData> respawnMap=new(){{4,new(){className="zombieArlene",spawnPointIndex=9}},{10,new(){className="zombieArlene",spawnPointIndex=1}}};
    public HashSet<int> pendingSpawnMap=new();
}
class RebirthPoiIdentity { }
class RebirthPoiActorObservation
{
    public Guid Token;public int EntityId,SpawnPoint;public string ClassName;public bool Dead;
    public RebirthPoiActorObservation RestoreVerifiedNativeParticipant(int id,Guid receipt)=>new(){Token=Token,EntityId=id,ClassName=ClassName,SpawnPoint=SpawnPoint};
}
static class RebirthPoiNativeActorStamp
{
    public static bool TryAssignVerifiedRestoration(EntityAlive e,Guid token,out Guid assigned){e.Token=token;assigned=token;return true;}
}
static class RebirthPurgeReleasePolicy { public static bool Enabled=>true; }
class RebirthPoiNativeEvidence
{
    public static RebirthPoiNativeEvidence Instance=new();public int Withheld,Restored;
    public RebirthPoiActorObservation Actor;
    public RebirthPoiNativeRestorationWitness BeginRestoration(World w,SleeperVolume v,int id,SleeperVolume.RespawnData native)=>new(w,v,id,native,Actor,()=>true);
    public bool Owns(SleeperVolume v,RebirthPoiIdentity id)=>true;
    public void Withhold(World w,SleeperVolume v){Withheld++;}
    public bool IsAcknowledged(SleeperVolume v,RebirthPoiActorObservation o)=>true;
    public bool NativeRestored(World w,SleeperVolume v,EntityAlive e,RebirthPoiActorObservation old,RebirthPoiActorObservation next){Restored++;Actor=next;return true;}
}
class Fixture
{
    public World World=new();public SleeperVolume Volume=new();
    public Fixture()
    {
        ThreadManager.Main=true;GameManager.Instance.World=World;SleeperVolume.TickSpawnCount=100;
        RebirthPoiNativeRestorationConnection.Reset();RebirthPoiNativeEvidence.Instance=new(){Actor=new(){Token=Guid.NewGuid(),EntityId=10,ClassName="zombieArlene",SpawnPoint=1}};
    }
    public RebirthPoiNativeRestorationWitness Begin()=>RebirthPoiNativeRestorationConnection.BeforeUpdate(World,Volume);
    public void Wait()
    {
        // Exact installed UpdateSpawn CheckSpawnPos(false) branch.
        Volume.spawnDelay=2;Volume.respawnList.RemoveAt(Volume.respawnList.Count-1);
        Volume.spawnsAvailable.Remove(1);Volume.spawnsAvailable.Add(1);Volume.respawnList.Add(10);
    }
    public EntityAlive Replace()
    {
        Volume.spawnDelay=2;Volume.respawnList.Remove(10);Volume.respawnMap.Remove(10);
        Volume.respawnMap.Add(11,new(){className="zombieArlene",spawnPointIndex=1});Volume.pendingSpawnMap.Add(11);SleeperVolume.TickSpawnCount++;
        var entity=new EntityAlive{entityId=11,world=World};World.Entities.Add(11,entity);return entity;
    }
}
class Program
{
    static int checks;
    static void Check(bool yes,string name){if(!yes)throw new Exception(name);checks++;}
    static void Main()
    {
        var f=new Fixture();var original=RebirthPoiNativeEvidence.Instance.Actor;var first=f.Begin();f.Wait();
        RebirthPoiNativeRestorationConnection.AfterUpdate(f.World,f.Volume,first,null);
        Check(first.NativeDeferred,"positive requeue is a native wait");
        Check(RebirthPoiNativeEvidence.Instance.Withheld==0,"wait must not fault the POI");
        Check(!RebirthPoiNativeRestorationConnection.HasPending(new()),"wait creates no replacement obligation");
        Check(RebirthPoiNativeEvidence.Instance.Restored==0&&ReferenceEquals(original,RebirthPoiNativeEvidence.Instance.Actor),"wait cannot mint a receipt or change original");
        f.Volume.spawnDelay=1;var second=f.Begin();var entity=f.Replace();
        RebirthPoiNativeRestorationConnection.ActualSpawn(f.World,f.Volume,entity);
        RebirthPoiNativeRestorationConnection.AfterUpdate(f.World,f.Volume,second,null);
        Check(!second.NativeDeferred&&RebirthPoiNativeEvidence.Instance.Restored==1,"later exact native replacement succeeds");
        Check(entity.Token==original.Token&&RebirthPoiNativeEvidence.Instance.Actor.EntityId==11,"replacement preserves original token");
        Check(!RebirthPoiNativeRestorationConnection.HasUnresolved(new()),"later replacement is not held by old wait");

        var changes=new Dictionary<string,Action<Fixture>>
        {
            ["class changed"]=x=>x.Volume.respawnMap[10]=new(){className="wrong",spawnPointIndex=1},
            ["point changed"]=x=>x.Volume.respawnMap[10]=new(){className="zombieArlene",spawnPointIndex=2},
            ["member removed"]=x=>x.Volume.respawnMap.Remove(4),
            ["member added"]=x=>x.Volume.respawnMap.Add(12,new()),
            ["pending changed"]=x=>x.Volume.pendingSpawnMap.Add(12),
            ["original pending"]=x=>x.Volume.pendingSpawnMap.Add(10),
            ["native counter changed"]=x=>SleeperVolume.TickSpawnCount++,
            ["spawned count changed"]=x=>x.Volume.numSpawned++,
            ["queue shortened"]=x=>x.Volume.respawnList.Remove(4),
            ["queue target changed"]=x=>x.Volume.respawnList[1]=4,
            ["native delay wrong"]=x=>x.Volume.spawnDelay=3,
            ["point not requeued"]=x=>x.Volume.spawnsAvailable.Remove(1),
            ["original now exists"]=x=>x.World.Entities.Add(10,new(){entityId=10,world=x.World}),
            ["world changed"]=x=>GameManager.Instance.World=new(),
            ["off main thread"]=x=>ThreadManager.Main=false
        };
        foreach(var change in changes)
        {
            f=new();var witness=f.Begin();f.Wait();change.Value(f);
            RebirthPoiNativeRestorationConnection.AfterUpdate(f.World,f.Volume,witness,null);
            Check(RebirthPoiNativeEvidence.Instance.Withheld==1,"ambiguous wait refused: "+change.Key);
            Check(RebirthPoiNativeEvidence.Instance.Restored==0,"no receipt on refusal: "+change.Key);
        }
        f=new();var exceptional=f.Begin();f.Wait();RebirthPoiNativeRestorationConnection.AfterUpdate(f.World,f.Volume,exceptional,new Exception("native failure"));
        Check(RebirthPoiNativeEvidence.Instance.Withheld==1,"native exception still faults");
        f=new();var callback=f.Begin();f.Wait();RebirthPoiNativeRestorationConnection.ActualSpawn(f.World,f.Volume,new(){entityId=12,world=f.World});
        RebirthPoiNativeRestorationConnection.AfterUpdate(f.World,f.Volume,callback,null);
        Check(RebirthPoiNativeEvidence.Instance.Withheld==1,"unexpected synchronous callback still faults");
        Console.WriteLine($"PASS {checks} production-source checks; native gameplay acceptance NOT RUN");
    }
}
