using System;using System.Collections.Generic;
namespace UnityEngine {struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;} } }
struct Vector3i {public Vector3i(UnityEngine.Vector3 value){} }
class ThreadManager {public static bool IsMainThread()=>true;}
class WorldState {public string Guid=System.Guid.NewGuid().ToString("N").ToUpperInvariant();}
class World {public WorldState worldState=new WorldState();public bool Remote;public EntityPlayerLocal Player;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public EntityPlayerLocal GetEntity(int id)=>Player!=null&&Player.entityId==id?Player:null;}
class EntityPlayerLocal {public World world;public int entityId=1;public bool Dead;public bool IsDead()=>Dead;public bool IsSpawned()=>true;}
class GameManager {public static GameManager Instance;public World World;}
class NativeConnection {public bool Disconnected;public bool IsDisconnected()=>Disconnected;}
class ConnectionManager {public bool IsServer;public NativeConnection[] connectionToServer=new[]{new NativeConnection()};}
class SingletonMonoBehaviour<T> {public static T Instance;}
enum EnumGamePrefs {GameGuidClient}
class GamePrefs {public static string Guid;public static string GetString(EnumGamePrefs key)=>Guid;}
class QuestClass {public int DifficultyTier=2;public bool Available=true;public bool CanActivate()=>Available;}
class QuestJournal {public EntityPlayerLocal OwnerPlayer;public List<Quest> quests=new List<Quest>();public Quest ActiveQuest,TrackedQuest;public int Refreshes,History;public void RefreshTracked(){Refreshes++;}public void AddPOIToTraderData(int tier,UnityEngine.Vector3 a,UnityEngine.Vector3 b){History++;}}
class Quest {public enum QuestState {InProgress,Failed}public enum PositionDataTypes {POIPosition,TraderPosition}public string ID="clear",QuestUniqueId="original-quest";public int QuestCode=-12;public byte CurrentPhase;public QuestState CurrentState=QuestState.InProgress;public bool RallyMarkerActivated,Tracked,Requirements=true;public QuestJournal OwnerJournal;public QuestClass QuestClass=new QuestClass();public Dictionary<PositionDataTypes,UnityEngine.Vector3> PositionData=new Dictionary<PositionDataTypes,UnityEngine.Vector3>();public int Removed;public bool CheckRequirements()=>Requirements;public void RemoveMapObject(){Removed++;}}
class ObjectiveRallyPoint {public static ObjectiveRallyPoint OutstandingRallyPoint;public Quest OwnerQuest;public bool Complete;public byte CurrentValue;public int Party,Finished;public bool ThrowParty;public string activateEvent="event";public void HandleParty(){Party++;if(ThrowParty)throw new Exception("native party failure");}public void RallyPointActivated(){Finished++;CurrentValue=1;OutstandingRallyPoint=null;}}
class GameEventManager {public static GameEventManager Current=new GameEventManager();public int Events;public void HandleAction(string key,object a,EntityPlayerLocal b,bool twitchActivated,Vector3i position){Events++;}}
static class Program
{
 static int checks;static void Check(bool value,string text){if(!value)throw new Exception("FAIL "+text);checks++;Console.WriteLine("PASS "+text);}
 sealed class Fixture
 {
  public World World;public Quest Quest;public ObjectiveRallyPoint Objective;public ConnectionManager Connection;public Guid SavedGuid;public RebirthPoiOriginalRallyRequest Request;public UnityEngine.Vector3 Poi=new UnityEngine.Vector3(10,2,30);
  public Fixture(bool server=false){World=new World{Remote=!server};World.Player=new EntityPlayerLocal{world=World};GameManager.Instance=new GameManager{World=World};Connection=new ConnectionManager{IsServer=server};SingletonMonoBehaviour<ConnectionManager>.Instance=Connection;SavedGuid=server?Guid.Parse(World.worldState.Guid):Guid.NewGuid();GamePrefs.Guid=SavedGuid.ToString("N").ToUpperInvariant();var journal=new QuestJournal{OwnerPlayer=World.Player};Quest=new Quest{OwnerJournal=journal};journal.quests.Add(Quest);Quest.PositionData.Add(Quest.PositionDataTypes.POIPosition,Poi);Quest.PositionData.Add(Quest.PositionDataTypes.TraderPosition,new UnityEngine.Vector3(1,2,3));Objective=new ObjectiveRallyPoint{OwnerQuest=Quest};ObjectiveRallyPoint.OutstandingRallyPoint=null;GameEventManager.Current.Events=0;}
  public bool Capture()=>RebirthPoiOriginalRallyRequest.TryCapture(Objective,Poi,out Request);
  public RebirthPoiRallyCompletionReceipt Receipt()=>new RebirthPoiRallyCompletionReceipt(Request.RequestId,SavedGuid,Quest.QuestUniqueId,Quest.QuestCode,World.Player.entityId,4);
 }
 static void Main()
 {
  var f=new Fixture();Check(f.Capture()&&f.Request.SavedWorldId==f.SavedGuid&&f.Request.SavedWorldId!=Guid.Parse(f.World.worldState.Guid),"client captures actual native server handshake rather than generated remote WorldState GUID");Check(f.Objective.Party==0&&f.Quest.Removed==0&&!f.Quest.RallyMarkerActivated&&f.Quest.OwnerJournal.ActiveQuest==null,"capture leaves native success effects untouched");var receipt=f.Receipt();Check(f.Request.TryApplyConfirmed(receipt)&&f.Request.State==RebirthPoiRallyApplicationState.Applied,"correlated explicit server producer token applies native success branch");Check(f.Objective.Party==1&&f.Quest.Removed==1&&f.Quest.RallyMarkerActivated&&ReferenceEquals(f.Quest.OwnerJournal.ActiveQuest,f.Quest)&&f.Quest.Tracked&&ReferenceEquals(f.Quest.OwnerJournal.TrackedQuest,f.Quest)&&f.Quest.OwnerJournal.Refreshes==1&&f.Quest.OwnerJournal.History==1&&f.Objective.Finished==1&&GameEventManager.Current.Events==1,"party map tracking trader history rally completion and activation event retained once");Check(!f.Request.TryApplyConfirmed(receipt)&&f.Objective.Party==1&&GameEventManager.Current.Events==1,"duplicate completion cannot replay native party or event side effects");
  f=new Fixture(true);Check(f.Capture()&&f.Request.SavedWorldId==Guid.Parse(f.World.worldState.Guid),"listen server captures actual saved server WorldState GUID");
  f=new Fixture();f.Capture();Check(f.Request.RefuseOriginal(f.Request.RequestId,f.SavedGuid)&&f.Objective.Party==0&&f.Quest.Removed==0&&!f.Quest.RallyMarkerActivated,"original correlated refusal preserves native rally quest map and party state");Check(!f.Request.TryApplyConfirmed(f.Receipt()),"refused request cannot later apply success");
  f=new Fixture();f.Capture();var wrong=new RebirthPoiRallyCompletionReceipt(Guid.NewGuid(),f.SavedGuid,f.Quest.QuestUniqueId,f.Quest.QuestCode,1,4);Check(!f.Request.TryApplyConfirmed(wrong)&&f.Objective.Party==0,"wrong original request nonce cannot apply native completion");
  f=new Fixture();f.Capture();wrong=new RebirthPoiRallyCompletionReceipt(f.Request.RequestId,Guid.Parse(f.World.worldState.Guid),f.Quest.QuestUniqueId,f.Quest.QuestCode,1,4);Check(!f.Request.TryApplyConfirmed(wrong),"generated remote world ID cannot substitute for saved server identity");
  f=new Fixture();f.Capture();f.Connection.connectionToServer[0]=new NativeConnection();Check(!f.Request.TryApplyConfirmed(f.Receipt())&&f.Objective.Party==0,"replacement native connection invalidates late original outcome");
  f=new Fixture();f.Capture();GameManager.Instance.World=new World();Check(!f.Request.TryApplyConfirmed(f.Receipt()),"replacement world invalidates original callback");
  f=new Fixture();f.Capture();GamePrefs.Guid=Guid.NewGuid().ToString("N");Check(!f.Request.TryApplyConfirmed(f.Receipt()),"changed native server handshake identity invalidates original outcome");
  f=new Fixture();f.Capture();f.Quest.QuestUniqueId="another-quest";Check(!f.Request.TryApplyConfirmed(f.Receipt()),"replacement native quest identity invalidates same objective object");
  f=new Fixture();f.Capture();f.Quest.OwnerJournal.ActiveQuest=new Quest();Check(!f.Request.TryApplyConfirmed(f.Receipt())&&f.Objective.Party==0,"late completion cannot overwrite another active native quest");
  f=new Fixture();f.Capture();f.Objective.ThrowParty=true;Check(!f.Request.TryApplyConfirmed(f.Receipt())&&f.Request.State==RebirthPoiRallyApplicationState.Unknown&&f.Request.NativeFailure!=null&&f.Objective.Party==1,"native success exception becomes explicit unknown with original failure");Check(!f.Request.TryApplyConfirmed(f.Receipt())&&f.Objective.Party==1,"partial native success is never replayed or rolled back speculatively");
  f=new Fixture();f.Quest.QuestClass.Available=false;Check(!f.Capture(),"initial native quest class availability retained");
  f=new Fixture();f.Quest.Requirements=false;Check(!f.Capture(),"initial native quest requirements retained");
  Console.WriteLine("RESULT "+checks+" PASS; actual production native owner scope/success method; native world/quest/events/transport doubled; server token producer and native hooks absent.");
 }
}
