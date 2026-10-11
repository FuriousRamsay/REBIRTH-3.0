using System;using System.Collections.Generic;using System.Reflection;
class Program{
 static int checks;static void Check(bool ok,string n){if(!ok)throw new Exception(n);checks++;Console.WriteLine("PASS "+n);}
 static object Hook(Type t,string n,params object[] a)=>t.GetMethod(n,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,a);
 static void Main(){var options=RebirthSandboxOptionManager.Current;var normal=new Quest{Normal=true};var other=new Quest();
 options.IsPurge=false;Check((bool)Hook(typeof(RebirthPurgeNewTraderQuestHook),"Prefix",normal,Quest.QuestSource.Trader),"None retains trader acceptance");
 options.IsPurge=true;Check(!(bool)Hook(typeof(RebirthPurgeNewTraderQuestHook),"Prefix",normal,Quest.QuestSource.Trader),"Purge refuses new trader journal entry");
 Check((bool)Hook(typeof(RebirthPurgeNewTraderQuestHook),"Prefix",normal,Quest.QuestSource.Load),"existing owned quest loading preserved");
 Check(!(bool)Hook(typeof(RebirthPurgeNewTraderQuestHook),"Prefix",normal,Quest.QuestSource.PartyShare),"shared trader job refused");
 Check((bool)Hook(typeof(RebirthPurgeNewTraderQuestHook),"Prefix",other,Quest.QuestSource.PartyShare),"unrelated shared quest preserved");
 object[] args={new NetPackageSharedQuest.SharedQuestData{questID="normal",questGiverID=4},true};
 Check(!(bool)Hook(typeof(RebirthPurgeShareEntryHook),"Prefix",args)&&!(bool)args[1],"new shared trader offer refused");
 args=new object[]{new NetPackageSharedQuest.SharedQuestData{questID="other",questGiverID=-1},true};Check((bool)Hook(typeof(RebirthPurgeShareEntryHook),"Prefix",args),"nontrader shared offer retained");
 args=new object[]{new List<Quest>{normal}};Check(!(bool)Hook(typeof(RebirthPurgeCachedTraderListingHook),"Prefix",args)&&((List<Quest>)args[0]).Count==0,"cached trader offers emptied");
 args=new object[]{new List<Quest>{normal}};Hook(typeof(RebirthPurgeCachedTraderListingHook),"Postfix",args);Check(((List<Quest>)args[0]).Count==0,"late offer generation cannot restore jobs");
 var trader=new EntityTrader{activeQuests=new List<Quest>{normal}};Check(!(bool)Hook(typeof(RebirthPurgeTraderListingHook),"Prefix",trader)&&trader.activeQuests.Count==0,"received trader offers cleared");
 var buffs=new EntityBuffs{parent=new EntityPlayer()};buffs.Active.Add("buffSnow_Hazard");buffs.Active.Add("buffBurningHazardPlayer");buffs.Vars.Add("$SnowHazardTimer");buffs.Vars.Add("hunger");RebirthPurgeBiomeHazardPolicy.Clean(buffs);
 Check(!buffs.Active.Contains("buffSnow_Hazard")&&buffs.Active.Contains("buffBurningHazardPlayer")&&!buffs.Vars.Contains("$SnowHazardTimer")&&buffs.Vars.Contains("hunger"),"biome cleanup preserves fire trap and survival state");
 Check(RebirthPurgeBiomeHazardPolicy.Suppress(buffs,"buffDesert_Hazard01")&&!RebirthPurgeBiomeHazardPolicy.Suppress(buffs,"buffBurningHazardPlayer"),"exact biome family suppression");
 Check(!RebirthPurgeBiomeHazardPolicy.Suppress(new EntityBuffs{parent=new object()},"buffSnow_Hazard"),"nonplayer effects retained");
 Check(!(bool)Hook(typeof(RebirthPurgeServerShareHook),"Prefix",new NetPackageSharedQuest.SharedQuestData{questID="normal"}),"server refuses new trader share");
 Check((bool)Hook(typeof(RebirthPurgeServerShareHook),"Prefix",new NetPackageSharedQuest.SharedQuestData{questID="normal",questEvent=NetPackageSharedQuest.SharedQuestData.SharedQuestEvents.RemoveQuest}),"owned share removal preserved");
 Check((bool)Hook(typeof(RebirthPurgeServerShareHook),"Prefix",new NetPackageSharedQuest.SharedQuestData{questID="other",questGiverID=-1}),"server unrelated shares preserved");
 var local=new EntityPlayerLocal{PlayerUI=new LocalPlayerUI{xui=new Xui{Dialog=new Dialog{Respondent=new EntityTrader()}}}};
 Check(!(bool)Hook(typeof(RebirthPurgeDialogOfferHook),"Prefix",new DialogActionAddQuest{Quest=other},local),"trader dialog refused before offer window");
 local.PlayerUI.xui.Dialog.Respondent=new object();Check((bool)Hook(typeof(RebirthPurgeDialogOfferHook),"Prefix",new DialogActionAddQuest{Quest=other},local),"nontrader dialog quest preserved");
 options.IsPurge=false;Check(!RebirthPurgeBiomeHazardPolicy.Suppress(buffs,"buffSnow_Hazard"),"None biome hazards retained");
 Console.WriteLine("RESULT "+checks+" PASS; production hook bodies with native API doubles; no native hook installation or game launch.");}}
class RebirthSandboxOptionManager{public static readonly RebirthSandboxOptionManager Current=new();public bool IsPurge;public int Revision;}
static class RebirthPurgeReleasePolicy{public static bool Enabled=false;}
class Vector3i{}public class Mod{}interface IModApi{void InitMod(Mod m);}
class EntityPlayer{public World world;}class EntityPlayerLocal:EntityPlayer{public LocalPlayerUI PlayerUI;}class LocalPlayerUI{public Xui xui;}class Xui{public Dialog Dialog;}class Dialog{public object Respondent;}class World{public GameRandom GetGameRandom()=>new GameRandom();public BiomeDefinition GetBiome(int x,int z)=>new BiomeDefinition{GameStageBonus=x>0?30:0};public bool Remote;public bool IsRemote()=>Remote;public void GetMobRandomSpawnPosWithWater(){}public void GetRandomSpawnPositionInAreaMinMaxToPlayers(){}}
class EntityBuffs{public object parent;public enum BuffStatus{FailedGameStat}public HashSet<string> Active=new(),Vars=new();public bool HasBuff(string n)=>Active.Contains(n);public void RemoveBuff(string n)=>Active.Remove(n);public void RemoveCustomVar(string n)=>Vars.Remove(n);public void AddBuff(string n,Vector3i p,int id,bool a,bool b,float f){}public void Tick(){}}
class Quest{public enum QuestSource{Trader,PartyShare,Load}public enum PositionDataTypes{TraderPosition}public Dictionary<PositionDataTypes,object> PositionData=new();public bool Normal;}
static class QuestClass{public static Quest CreateQuest(string id)=>new(){Normal=id=="normal"};}
class QuestJournal{public void AddQuest(Quest q,Quest.QuestSource s){}public bool AddSharedQuestEntry(NetPackageSharedQuest.SharedQuestData d)=>true;}
class QuestEventManager{public List<Quest> GetQuestList(World w,int n,int p)=>new();}
class EntityTrader{public List<Quest> activeQuests;public List<Quest> PopulateActiveQuests()=>new();public void SetActiveQuests(){}}
class PartyQuests{public void AcceptSharedQuest(SharedQuestEntry q){}}
class SharedQuestEntry{public Quest Quest;}
class NetPackageSharedQuest{public class SharedQuestData{public enum SharedQuestEvents{ShareQuest,RemoveQuest,AddSharedMember,RemoveSharedMember}public SharedQuestEvents questEvent;public string questID;public int questGiverID;}}
class GameManager{public static GameManager Instance=new();public World World;public void QuestShareServer(NetPackageSharedQuest.SharedQuestData d){}}class DialogActionAddQuest{public Quest Quest;public void PerformAction(EntityPlayer p){}}
static class RebirthUtilities{public static bool IsVanillaTrader(int n)=>n==4;}
static class RebirthTraderJobCompletionStats{public static bool IsRecognizedNormalTraderJob(Quest q)=>q.Normal;}
static class ModEvents{public struct SGameStartingData{}public delegate void Handler(ref SGameStartingData d);public static Event GameStarting=new();public class Event{public void RegisterHandler(Handler h){}}}
namespace UnityEngine.Scripting{class PreserveAttribute:Attribute{}}
namespace HarmonyLib{enum MethodType{Constructor}class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n){}public HarmonyPatch(Type t,MethodType n,Type[] p){}public HarmonyPatch(Type t,string n,Type[] a){}}class HarmonyPriority:Attribute{public HarmonyPriority(int n){}}static class Priority{public const int First=1,Last=0;}class Harmony{public Harmony(string id){}public Harmony CreateClassProcessor(Type t)=>this;public void Patch(){}public void UnpatchSelf(){}}}
enum RebirthSpawnSurface{Biome,WanderingHorde,BloodMoon,Sleeper,EventSpawn}
class RebirthSpawnContext{public RebirthSpawnSurface Surface;public string Biome;public RebirthSpawnProgressionMode ProgressionMode;public int GameStage,NativeStage;public bool DeferCompositionUntilPosition;public string HistoryKey,RequestedGroup;}
static class RebirthSpawnCompositionRuntimeIntegration{public static RebirthSpawnContext Current;static readonly Stack<RebirthSpawnContext> Saved=new();public static bool IsAuthoritative(World w)=>w!=null&&!w.IsRemote()&&ReferenceEquals(GameManager.Instance.World,w);public static void Push(RebirthSpawnContext c){Saved.Push(Current);Current=c;}public static void Pop(){Current=Saved.Pop();}public static string BiomeName(World w,UnityEngine.Vector3 p)=>p.x>0?"desert":"pine_forest";}
namespace UnityEngine{struct Vector3{public float x,z;}}enum RebirthSpawnProgressionMode{Biome,Gamestage}
class BiomeDefinition{public float GameStageBonus;}
class AIDirector{public World World;}
class AIDirectorPlayerState{}
class AIWanderingHordeSpawner{public enum SpawnType{Bandits,Zombies}public delegate void HordeArrivedDelegate();}
class AIDirectorBloodMoonParty{public World spawnWorld;public UnityEngine.Vector3 spawnBasePos;public void InitParty(){}}
class AIDirectorGameStagePartySpawner{public int CalcPartyLevel()=>10000;public void SetPartyLevel(int _partyLevel){}}
class GameStageDefinition{public class Stage{public int stageNum;}public List<Stage> stages=new();public Stage GetStage(int stage)=>null;}
class SleeperVolume{public UnityEngine.Vector3 BoxMin;public void UpdatePlayerTouched(World _world,EntityPlayer _playerTouched){}public int GetGameStageAround(EntityPlayer player)=>10000;}class Entity{public UnityEngine.Vector3 position;}
class GameRandom{public float RandomFloat=>0.5f;}
class EntityClass{public bool bIsEnemyEntity=true,bIsAnimalEntity;public static Dictionary<int,EntityClass> Classes=new(){{1,new EntityClass()},{2,new EntityClass{bIsAnimalEntity=true}},{3,new EntityClass{bIsEnemyEntity=false}},{42,new EntityClass()}};public static EntityClass GetEntityClass(int id)=>Classes.TryGetValue(id,out var e)?e:null;}
class EntityFactory{public static Entity CreateEntity(int _et,UnityEngine.Vector3 _transformPos,UnityEngine.Vector3 _rotation,int _spawnById,string _spawnByName)=>null;}
class RebirthSpawnTrace{public int EntityClassId=42;}
static class RebirthSpawnCompositionService{public static RebirthSpawnContext Last;public static int Calls;public static bool Fail;public static bool TrySelect(RebirthSpawnContext c,Func<double> r,out RebirthSpawnTrace t){Calls++;Last=c;t=new RebirthSpawnTrace();return !Fail;}public static bool IsForbiddenRestrictedSpawnEntity(int id)=>false;}
namespace GameEvent.SequenceActions{class OwnerSequence{public Entity Target;public UnityEngine.Vector3 TargetPosition;}class ActionBaseSpawn{public bool useEntityGroup;public OwnerSequence Owner;public void OnPerformAction(){}public void SpawnEntity(){}}}