using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
class Vec { public int x,y,z; public Vec(int a=0,int b=0,int c=0){x=a;y=b;z=c;} public static Vec operator +(Vec a,Vec b)=>new Vec(a.x+b.x,a.y+b.y,a.z+b.z);public override bool Equals(object o)=>o is Vec v&&x==v.x&&y==v.y&&z==v.z;public override int GetHashCode()=>x; }
class Definition { public Vec startPos=new Vec(),size=new Vec(10,10,10);public short spawnCountMin=1,spawnCountMax=2;public int flags;public string minScript; }
class Prefab {public bool bTraderArea;public string PrefabName="house";public List<Definition> SleeperVolumeList=new List<Definition>{new Definition()};}
class PrefabInstance {public Prefab prefab=new Prefab();public Vec boundingBoxPosition=new Vec(),boundingBoxSize=new Vec(10,10,10);public byte rotation;public List<SleeperVolume> sleeperVolumes=new List<SleeperVolume>();}
class Biome {public string m_sBiomeName="forest";}
class WorldState {public string Guid=System.Guid.NewGuid().ToString("N").ToUpperInvariant();}
class World {public WorldState worldState=new WorldState();public ulong worldTime=1;public Dictionary<int,EntityAlive> Entities=new Dictionary<int,EntityAlive>();public List<SleeperVolume> Volumes=new List<SleeperVolume>(); public bool IsRemote()=>false;public EntityAlive GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;public int FindSleeperVolume(Vec min,Vec max)=>Volumes.FindIndex(v=>v.BoxMin.Equals(min)&&v.BoxMax.Equals(max));public SleeperVolume GetSleeperVolume(int id)=>id>=0&&id<Volumes.Count?Volumes[id]:null;public Biome GetBiome(int x,int z)=>new Biome();}
class EntityBuffs {public Dictionary<string,float> Vars=new Dictionary<string,float>();public bool HasCustomVar(string key)=>Vars.ContainsKey(key);public float GetCustomVar(string key)=>Vars[key];public void SetCustomVar(string key,float value,bool netSync){Vars[key]=value;} }
class EntityAlive {public EntityBuffs Buffs=new EntityBuffs();public World world;public int entityId;public bool Dead;public bool IsDead()=>Dead;}
class EntityPlayer:EntityAlive {public bool IsSpectator;public bool IsAlive()=>!Dead;public Vec GetBlockPosition()=>new Vec();}
class Script {public bool Running;public bool IsRunning()=>Running;}
class Handle {public bool IsCompleted=true;public Action onComplete;}
class SleeperVolume {public PrefabInstance prefabInstance;public Vec BoxMin=new Vec(),BoxMax=new Vec(10,10,10);public short spawnCountMin=1,spawnCountMax=2;public int flags,numSpawned;public bool isSpawning,wasCleared;public Script minScript;public Dictionary<int,object> respawnMap=new Dictionary<int,object>();public HashSet<int> pendingSpawnMap=new HashSet<int>();public Queue<Handle> pendingSpawnOps=new Queue<Handle>();}
class Decorator {public PrefabInstance Prefab;public PrefabInstance GetPrefabFromWorldPos(int x,int z)=>Prefab;}
class GameStateManager {public bool IsGameStarted()=>true;}
class GameManager {public static GameManager Instance;public World World;public bool IsStartingGame;public GameStateManager gameStateManager=new GameStateManager();public bool IsEditMode()=>false;public Decorator Decorator=new Decorator();public Decorator GetDynamicPrefabDecorator()=>Decorator;}
class ConnectionManager {public bool IsServer=true;}
class SingletonMonoBehaviour<T> {public static T Instance;}
class ThreadManager {public static bool IsMainThread()=>true;}
class GameIO {public static string GetSaveGameDir()=>"unused";}
class Log {public static void Warning(string s){} }

internal class RebirthPurgeObjectiveFrame {internal const int MaximumEligible=200000;}
static class Program
{
    static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
    static void Main()
    {
        RebirthPurgeSupplyPolicy.Rules rules;string xml="<purge_supplies version='1' baseKills='75' increment='5' maximumBonus='150' flightRadius='100'/>";
        Check(RebirthPurgeSupplyPolicy.TryParse(xml,out rules),"historical quota rules parsed");
        Check(rules.Target(0)==75&&rules.Target(1)==80&&rules.Target(30)==225&&rules.Target(long.MaxValue)==225,"historical bonus cap projects exact quota without overflow");
        RebirthPurgeSupplyPolicy.Redemption result;
        Check(rules.TryRedeem(74,0,32,out result)&&result.Drops==0&&result.Credits==74,"below-quota credits retained");
        Check(rules.TryRedeem(75,0,32,out result)&&result.Drops==1&&result.Credits==0&&result.EarnedDrops==1,"first quota redeemed exactly");
        Check(rules.TryRedeem(155,0,32,out result)&&result.Drops==2&&result.Credits==0&&result.EarnedDrops==2,"each increasing quota deducted once");
        Check(rules.TryRedeem(4650,0,32,out result)&&result.Drops==31&&result.Credits==0&&result.EarnedDrops==31,"entire progression reaches 225 kill plateau");
        Check(rules.TryRedeem(long.MaxValue,31,2,out result)&&result.Drops==2&&result.Credits==long.MaxValue-450,"bounded redemption prevents unbounded processing");
        Check(!rules.TryRedeem(225,long.MaxValue,32,out result)&&result==null,"unrepresentable next reward withheld");
        Check(!rules.TryRedeem(-1,0,32,out result)&&!rules.TryRedeem(100,0,129,out result),"negative credits and oversized processing batch refused");
        Check(!RebirthPurgeSupplyPolicy.TryParse(xml.Replace("maximumBonus='150'","maximumBonus='151'"),out _),"ambiguous historical increment cap refused");
        Check(!RebirthPurgeSupplyPolicy.TryParse(xml.Replace("version='1'","version='2'"),out _),"unknown tuning schema withheld");
        Check(!RebirthPurgeSupplyPolicy.TryParse(xml.Replace("flightRadius='100'","flightRadius='1001'"),out _),"unbounded flight radius refused");
        Check(!RebirthPurgeSupplyPolicy.TryParse("<!DOCTYPE purge_supplies [<!ENTITY x '75'>]>"+xml.Replace("75","&x;"),out _),"external/entity processing forbidden");
        Console.WriteLine("RESULT "+checks+" PASS; production tuning/quota arithmetic; no kill attribution or aircraft delivery claim.");
    }
}