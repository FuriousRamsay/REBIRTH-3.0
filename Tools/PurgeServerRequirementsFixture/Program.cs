using System;using System.Collections.Generic;
namespace UnityEngine {struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;} } }
struct Vector3i {public Vector3i(UnityEngine.Vector3 value){} }
class ThreadManager {public static bool IsMainThread()=>true;}
class WorldState {public string Guid=System.Guid.NewGuid().ToString("N").ToUpperInvariant();}
class World {public WorldState worldState=new WorldState();public bool Remote;public EntityPlayerLocal Player;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public EntityPlayerLocal GetEntity(int id)=>Player!=null&&Player.entityId==id?Player:null;}
class EntityPlayer {public World world;public EntityBuffs Buffs=new EntityBuffs();public Progression Progression=new Progression();public Inventory inventory=new Inventory();public Equipment equipment=new Equipment();public QuestJournal QuestJournal;public int entityId=1;public bool Dead;public bool IsDead()=>Dead;public bool IsSpawned()=>true;} class EntityPlayerLocal:EntityPlayer {}
class GameManager {public static GameManager Instance;public World World;}
class NativeConnection {public bool Disconnected;public bool IsDisconnected()=>Disconnected;}
class ConnectionManager {public Clients Clients=new Clients();public bool IsServer;public NativeConnection[] connectionToServer=new[]{new NativeConnection()};}
class SingletonMonoBehaviour<T> {public static T Instance;}
enum EnumGamePrefs {GameGuidClient}
class GamePrefs {public static string Guid;public static string GetString(EnumGamePrefs key)=>Guid;}
class QuestClass {public int DifficultyTier=2;public bool Available=true;public bool CanActivate()=>Available;}
class QuestJournal {public EntityPlayerLocal OwnerPlayer;public List<Quest> quests=new List<Quest>();public Quest ActiveQuest,TrackedQuest;public int Refreshes,History;public void RefreshTracked(){Refreshes++;}public void AddPOIToTraderData(int tier,UnityEngine.Vector3 a,UnityEngine.Vector3 b){History++;}}
class Quest {public enum QuestState {InProgress,Failed}public enum PositionDataTypes {POIPosition,TraderPosition}public string ID="clear",QuestUniqueId="original-quest";public int QuestCode=-12;public List<Quests.Requirements.BaseRequirement> Requirements=new List<Quests.Requirements.BaseRequirement>();public Dictionary<string,string> DataVariables=new Dictionary<string,string>();public string ParseVariable(string value){if(value!=null&&value.Contains("{")){int start=value.IndexOf("{")+1;int end=value.IndexOf("}",start);if(end!=-1&&DataVariables.TryGetValue(value.Substring(start,end-start),out var resolved))return resolved;}return value;}public int SharedOwnerID=-1;public System.Collections.Generic.List<ObjectiveRallyPoint> Objectives=new System.Collections.Generic.List<ObjectiveRallyPoint>();public byte CurrentPhase;public QuestState CurrentState=QuestState.InProgress;public bool RallyMarkerActivated,Tracked,RequirementsFlag=true;public QuestJournal OwnerJournal;public QuestClass QuestClass=new QuestClass();public Dictionary<PositionDataTypes,UnityEngine.Vector3> PositionData=new Dictionary<PositionDataTypes,UnityEngine.Vector3>();public int Removed;public bool CheckRequirements()=>RequirementsFlag;public void RemoveMapObject(){Removed++;}}
class ObjectiveRallyPoint {public static ObjectiveRallyPoint OutstandingRallyPoint;public Quest OwnerQuest;public bool Complete;public byte Phase,CurrentValue;public int Party,Finished;public bool ThrowParty;public string activateEvent="event";public void HandleParty(){Party++;if(ThrowParty)throw new Exception("native party failure");}public void RallyPointActivated(){Finished++;CurrentValue=1;OutstandingRallyPoint=null;}}
class GameEventManager {public static GameEventManager Current=new GameEventManager();public int Events;public void HandleAction(string key,object a,EntityPlayerLocal b,bool twitchActivated,Vector3i position){Events++;}}
class ClientInfo {public int entityId=1;public PlayerDataFile latestPlayerData;}
class PlayerDataFile {public int id=1;public QuestJournal questJournal;}
class Clients {public ClientInfo Sender;public ClientInfo ForEntityId(int id)=>Sender!=null&&Sender.entityId==id?Sender:null;}
class GameIO {public static string Save;public static string GetSaveGameDir()=>Save;}
class EntityBuffs {public HashSet<string> Values=new HashSet<string>();public bool HasBuff(string value)=>Values.Contains(value);}
class Progression {public int Level=10;public int GetLevel()=>Level;}
enum EquipmentSlots {Head,Body,Count}
class ItemClass {public static Dictionary<string,ItemValue> Items=new Dictionary<string,ItemValue>();public static ItemValue GetItem(string id)=>Items.TryGetValue(id,out var item)?item:new ItemValue();}
class ItemClassArmor:ItemClass {public EquipmentSlots EquipSlot;}
class ItemValue {public int type;public ItemClass ItemClass;}
class Hand {public ItemValue BareHandItemValue=new ItemValue{type=1};}
class Inventory {public Hand Hand=new Hand();public ItemValue holdingItemItemValue=new ItemValue{type=1};}
class Equipment {public ItemValue[] Items=new[]{new ItemValue(),new ItemValue()};public ItemValue GetSlotItem(int index)=>index>=Items.Length?new ItemValue():Items[index];}
class EnumUtils {public static T Parse<T>(string value)=>(T)Enum.Parse(typeof(T),value,true);}
// Actual assembly also has an unrelated global RequirementGroup; keep its name collision represented.
class RequirementGroup { public string unrelated; }
namespace Quests.Requirements {
 class BaseRequirement {public string ID,Value;public int Phase;public virtual bool CheckRequirement()=>throw new Exception("Local native requirement must never be called");}
 class RequirementBuff:BaseRequirement {}
 class RequirementLevel:BaseRequirement {public int expectedLevel;}
 class RequirementHolding:BaseRequirement {public ItemValue expectedItem=new ItemValue();}
 class RequirementWearing:BaseRequirement {public ItemValue expectedItem=new ItemValue();}
 class RequirementGroup:BaseRequirement {public enum GroupOperator {AND,OR}public GroupOperator Operator;public List<BaseRequirement> ChildRequirements=new List<BaseRequirement>();}
 class CustomRequirement:RequirementBuff {}
}
static class Program
{
 static int checks;static void Check(bool value,string text){if(!value)throw new Exception("FAIL "+text);checks++;Console.WriteLine("PASS "+text);}
 static void Main()
 {
  var quest=new Quest();var player=new EntityPlayer();bool result;
  Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&result,"empty native requirement list satisfied without local UI");
  var buff=new Quests.Requirements.RequirementBuff{ID="well"};quest.Requirements.Add(buff);Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&!result,"missing actual server buff is known false");player.Buffs.Values.Add("well");Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&result,"actual server buff satisfies requirement");
  quest.Requirements.Clear();quest.Requirements.Add(new Quests.Requirements.RequirementLevel{Value="12",expectedLevel=0});Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&!result,"server resolves native value instead of uninitialized expectedLevel cache");player.Progression.Level=12;Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&result,"actual server progression level satisfies native comparison");
  quest.DataVariables["minimum"]="13";quest.Requirements[0].Value="prefix{minimum}suffix";Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&!result,"native first-variable replacement preserves whole-value semantics");
  quest.Requirements.Clear();quest.Requirements.Add(new Quests.Requirements.RequirementHolding{ID=""});Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&result,"empty holding ID uses qualified native bare hand item value");ItemClass.Items["club"]=new ItemValue{type=2};quest.Requirements[0].ID="club";Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&!result,"wrong actual held item fails native type comparison");player.inventory.holdingItemItemValue=new ItemValue{type=2};Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&result,"actual held item succeeds without client UI");
  ItemClass.Items["hat"]=new ItemValue{type=3,ItemClass=new ItemClassArmor{EquipSlot=EquipmentSlots.Head}};quest.Requirements.Clear();quest.Requirements.Add(new Quests.Requirements.RequirementWearing{ID="hat"});Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&!result,"missing native equipment slot fails wearing");player.equipment.Items[0]=new ItemValue{type=3};Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&result,"native armor slot type matches original wearing semantics");
  ItemClass.Items["invalidSlot"]=new ItemValue{type=4,ItemClass=new ItemClassArmor{EquipSlot=EquipmentSlots.Count}};quest.Requirements[0].ID="invalidSlot";Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&!result,"native Count slot remains known false");
  var group=new Quests.Requirements.RequirementGroup{Value="OR"};group.ChildRequirements.Add(new Quests.Requirements.RequirementBuff{ID="missing"});group.ChildRequirements.Add(buff);quest.Requirements.Clear();quest.Requirements.Add(group);Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&result,"OR resolved from native value instead of uninitialized group Operator");group.Value="AND";Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&!result,"native AND short-circuit remains false");
  group.Value="OR";group.ChildRequirements.Clear();group.ChildRequirements.Add(buff);group.ChildRequirements.Add(new Quests.Requirements.CustomRequirement());Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&result,"native OR short-circuit does not inspect unexecuted unknown child");group.ChildRequirements.Reverse();Check(!RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result),"custom polymorphic requirement refuses unqualified server execution");
  group.ChildRequirements.Clear();group.ChildRequirements.Add(group);Check(!RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result),"cyclic requirement group is bounded and unavailable");
  quest.Requirements.Clear();quest.Requirements.Add(new Quests.Requirements.CustomRequirement{Phase=2});Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&result,"irrelevant native phase requirement is not evaluated");
  quest.Requirements.Clear();group.ChildRequirements.Clear();group.Value="AND";group.ChildRequirements.Add(new Quests.Requirements.RequirementBuff{ID="missing",Phase=99});quest.Requirements.Add(group);Check(RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result)&&!result,"child phase is not independently filtered within native group");
  group.Value="invalid";Check(!RebirthPoiServerQuestRequirements.TryCheck(quest,player,out result),"malformed native group operator unavailable");
  Console.WriteLine("RESULT "+checks+" PASS; actual production server requirement evaluator; native requirement/player/item methods explicitly doubled; no local-only requirement calls or caches used.");
 }
}