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

static class Program
{
    static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
    sealed class Native:System.Collections.IEnumerator {internal Action Execute;bool advanced;public object Current=>null;public bool MoveNext(){if(!advanced){advanced=true;Execute();}return false;}public void Reset()=>throw new NotSupportedException();}
    static void Main()
    {
        var tx=Guid.NewGuid();var plan=new RebirthPoiResetPlan(tx,RebirthPoiResetCaller.Region,new string('a',64),new long[]{12,13},Array.Empty<int>(),Array.Empty<int>(),Array.Empty<RebirthPoiAuthoredResetExpectation>(),true,RebirthPoiResetChunkEffect.Removed);
        Check(plan.IsAuthored&&plan.ChunkEffect==RebirthPoiResetChunkEffect.Removed&&!plan.RequiresTriggerRefresh,"deletion declares separate native effects");
        var xml=RebirthPoiResetPlan.Write(plan);Check((string)xml.Attribute("version")=="4"&&RebirthPoiResetPlan.Read(xml).Canonical==plan.Canonical,"deletion intent cold-roundtrips explicit schema");
        var wrong=new System.Xml.Linq.XElement(xml);wrong.SetAttributeValue("chunkEffect",1);bool refused=false;try{RebirthPoiResetPlan.Read(wrong);}catch(FormatException){refused=true;}Check(refused,"deletion schema cannot silently become rebuilding");
        refused=false;try{new RebirthPoiResetPlan(tx,RebirthPoiResetCaller.Quest,new string('a',64),new long[]{12},Array.Empty<int>(),Array.Empty<int>(),Array.Empty<RebirthPoiAuthoredResetExpectation>(),true,RebirthPoiResetChunkEffect.Removed);}catch(ArgumentException){refused=true;}Check(refused,"quest cannot claim deletion instead of required rebuilding");
        for(int mode=0;mode<4;mode++)
        {
            string dir=Path.Combine(Path.GetTempPath(),"rebirth-deletion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);object scope=new object();
            try
            {
                var binding=new RebirthPoiWorldBinding(scope,Guid.NewGuid().ToString("N").ToUpperInvariant(),dir,()=>true);RebirthPoiWorldStore store;Check(RebirthPoiWorldStore.TryOpen(binding,out store)==RebirthPoiStoreResult.Published,"deletion world opened "+mode);
                var identity=new RebirthPoiIdentity("decoration",0,0,0,0,16,16,16,"forest");store.TryDiscover(store.Published,identity,true);
                var native=new Native();double clock=0;var protocol=new RebirthPoiResetCallerProtocol(store,store.Published,identity,plan,native,()=>clock);
                int test=mode;native.Execute=()=>{
                    if(test==0){protocol.ChunkRemoved(scope,tx,12,true);protocol.ChunkRemoved(scope,tx,13,true);}
                    if(test==1)protocol.ChunkRemoved(scope,tx,12,true);
                    if(test==2){protocol.ChunkCopied(scope,tx,12,true);protocol.ChunkCopied(scope,tx,13,true);protocol.ChunkRegenerated(scope,tx,12,true);protocol.ChunkRegenerated(scope,tx,13,true);}
                    if(test==3){protocol.ChunkRemoved(scope,tx,12,true);protocol.ChunkRemoved(scope,tx,13,false);}
                };
                for(int n=0;n<8;n++){clock++;protocol.MoveNext();protocol.Poll();}
                RebirthPoiClearanceRecord record;store.Published.TryGet(identity,out record);
                Check(test==0?protocol.State==RebirthPoiResetProtocolState.Completed&&record.Epoch==1&&record.LastResetDisposition==RebirthPoiResetDisposition.Completed:protocol.State==RebirthPoiResetProtocolState.Unknown&&record.State==RebirthPoiClearanceState.ResetPending,"positive deletion proof required; missing, rebuild-only and failed deletion withheld "+mode);
                if(test==0){RebirthPoiWorldStore cold;Check(RebirthPoiWorldStore.TryOpen(binding,out cold)==RebirthPoiStoreResult.Published&&cold.Published.TryGet(identity,out record)&&record.Epoch==1&&record.LastAuthoredReset.OriginalPlan.ChunkEffect==RebirthPoiResetChunkEffect.Removed,"deletion completion survives actual cold save load");}
                protocol.Dispose();
            }
            finally{Directory.Delete(dir,true);}
        }
        Console.WriteLine("RESULT "+checks+" PASS; production typed deletion intent/protocol/store/domain with real temporary files; native deletion effects doubled.");
    }
}