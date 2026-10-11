using System;using System.Collections.Generic;using System.IO;
class Program {
 static int n;static void Check(bool ok,string why){if(!ok)throw new Exception(why);n++;Console.WriteLine("PASS "+why);}
 static void Tick(){UnityEngine.Time.realtimeSinceStartup+=2;RebirthPurgeHudProgress.Pulse();}
 static void Main(){
 var dir=Path.Combine(Path.GetTempPath(),"rb-purge-hud-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(dir,"Config"));File.WriteAllText(Path.Combine(dir,"Config","_purge_supplies.xml"),"<purge_supplies version='1' baseKills='75' increment='5' maximumBonus='150' flightRadius='100'/>");RebirthPurgeSupplyPolicy.Load(dir);
 var world=new World();GameManager.Instance.World=world;var p=new EntityPlayerLocal{Key="owner"};world.aiDirector.Tracked.trackedPlayers.list.Add(new Tracked{Player=p});
 RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{p.Key,new RebirthPurgeSupplyAccount{EarnedDrops=1,ObservedCredits=130,SpentCredits=75}}};
 RebirthPurgeReleasePolicy.Enabled=false;Tick();Check(p.Buffs.Writes==0,"disabled release performs no projection");RebirthPurgeReleasePolicy.Enabled=true;
 RebirthSandboxOptionManager.Current.IsPurge=false;Tick();Check(p.Buffs.Writes==0,"None leaves normal HUD state alone");RebirthSandboxOptionManager.Current.IsPurge=true;
 Tick();Check(p.Buffs.GetCustomVar("_rbPurgeSupplyRemaining")==25&&p.Buffs.GetCustomVar("_rbPurgeSupplyTarget")==80,"HUD derives remaining kills from persisted balance and escalating policy");int writes=p.Buffs.Writes;Tick();Check(p.Buffs.Writes==writes,"unchanged counters produce no repeated synchronized writes");
 RebirthPurgeObjectiveProgress.Instance.Published=new Dictionary<string,RebirthPurgeObjectiveProgress.BiomeProgress>{{"forest",new(){Cleared=3,Eligible=12}}};Check(RebirthPurgeHudProgress.Text(p)=="3/12 25/80","local HUD reads current biome counts");
 world.Remote=true;RebirthPoiMapSync.LocalObjectives=new RebirthPurgeObjectiveFrame{Known=true,Biomes=new(){{"forest",new(){Cleared=4,Eligible=12}}}};Check(RebirthPurgeHudProgress.Text(p)=="4/12 25/80","remote HUD uses received objective counts");Tick();Check(p.Buffs.Writes==writes,"remote world never authors synchronized counters");
 RebirthPoiMapSync.LocalObjectives=null;Check(RebirthPurgeHudProgress.Text(p).StartsWith("—/—"),"unknown objective snapshot never claims completion");world.Remote=false;
 RebirthPurgeSupplyService.Published=null;Tick();Check(p.Buffs.GetCustomVar("_rbPurgeSupplyKnown")==0&&RebirthPurgeHudProgress.Text(p).EndsWith("—/—"),"missing account publication hides stale supply values");
 world=new World();GameManager.Instance.World=world;var peers=new List<EntityPlayerLocal>();for(int i=0;i<20;i++){var peer=new EntityPlayerLocal{Key="p"+i};peers.Add(peer);world.aiDirector.Tracked.trackedPlayers.list.Add(new Tracked{Player=peer});}RebirthPurgeSupplyService.Published=new();Tick();Check(peers.FindAll(x=>x.Buffs.Writes>0).Count==16,"projection visits at most 16 players in a pulse");Tick();Check(peers.TrueForAll(x=>x.Buffs.GetCustomVar("_rbPurgeSupplyTarget")==75),"round robin eventually projects every connected player");Check(RebirthPurgeHudProgress.Text(null)=="pending","null player remains pending");
 Console.WriteLine("RESULT "+n+" PASS; production HUD/policy; native world, network CVar store and objective/account providers doubled.");
 }
}
namespace UnityEngine{static class Time{public static float realtimeSinceStartup;}}
static class RebirthPurgeReleasePolicy{public static bool Enabled=true;}
class RebirthSandboxOptionManager{public static RebirthSandboxOptionManager Current=new();public bool IsPurge=true;}
class GameManager{public static GameManager Instance=new();public World World;}
class World{public bool Remote;public bool IsRemote()=>Remote;public Director aiDirector=new();public Biome GetBiome(int x,int z)=>new();}
class Biome{public string m_sBiomeName="forest";}
class Director{public AIDirectorPlayerManagementComponent Tracked=new();public T GetComponent<T>()=>(T)(object)Tracked;}
class AIDirectorPlayerManagementComponent{public TrackedList trackedPlayers=new();}
class TrackedList{public List<Tracked> list=new();}
class Tracked{public EntityPlayer Player;}
class EntityPlayer{public string Key;public Vars Buffs=new();public Position position=new();}
class EntityPlayerLocal:EntityPlayer{}
class Position{public float x,z;}
class Vars{Dictionary<string,float> data=new();public int Writes;public bool HasCustomVar(string k)=>data.ContainsKey(k);public float GetCustomVar(string k)=>data.TryGetValue(k,out var v)?v:0;public void SetCustomVar(string k,float v,bool sync){if(!sync)throw new Exception("unsynchronized");Writes++;data[k]=v;}}
static class RebirthPurgeKillContributor{public static string Identify(EntityPlayer p,World w)=>p.Key;}
static class RebirthPurgeSupplyService{public static Dictionary<string,RebirthPurgeSupplyAccount> Published;}
class RebirthPurgeSupplyAccount{public long EarnedDrops,ObservedCredits,SpentCredits;}
class RebirthPurgeObjectiveProgress{public static RebirthPurgeObjectiveProgress Instance=new();public Dictionary<string,BiomeProgress> Published;public class BiomeProgress{public int Cleared,Eligible;}}
class RebirthPurgeObjectiveFrame{public bool Known;public Dictionary<string,Biome> Biomes;public class Biome{public int Cleared,Eligible;}}
static class RebirthPoiMapSync{public static RebirthPurgeObjectiveFrame LocalObjectives;}
static class Localization{public static string Get(string key)=>key=="xuiRebirthPurgeHudProgress"?"{0}/{1} {2}/{3}":"pending";}
static class Log{public static void Warning(string text){}}
