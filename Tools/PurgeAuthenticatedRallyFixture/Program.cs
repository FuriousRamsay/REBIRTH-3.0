using System;using System.Collections.Generic;
namespace UnityEngine {struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;} } }
struct Vector3i {public Vector3i(UnityEngine.Vector3 value){} }
class ThreadManager {public static bool IsMainThread()=>true;}
class WorldState {public string Guid=System.Guid.NewGuid().ToString("N").ToUpperInvariant();}
class World {public WorldState worldState=new WorldState();public bool Remote;public EntityPlayerLocal Player;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public EntityPlayerLocal GetEntity(int id)=>Player!=null&&Player.entityId==id?Player:null;}
class EntityPlayer {public World world;public QuestJournal QuestJournal;public int entityId=1;public bool Dead;public bool IsDead()=>Dead;public bool IsSpawned()=>true;} class EntityPlayerLocal:EntityPlayer {}
class GameManager {public static GameManager Instance;public World World;}
class NativeConnection {public bool Disconnected;public bool IsDisconnected()=>Disconnected;}
class ConnectionManager {public Clients Clients=new Clients();public bool IsServer;public NativeConnection[] connectionToServer=new[]{new NativeConnection()};}
class SingletonMonoBehaviour<T> {public static T Instance;}
enum EnumGamePrefs {GameGuidClient}
class GamePrefs {public static string Guid;public static string GetString(EnumGamePrefs key)=>Guid;}
class QuestClass {public int DifficultyTier=2;public bool Available=true;public bool CanActivate()=>Available;}
class QuestJournal {public EntityPlayerLocal OwnerPlayer;public List<Quest> quests=new List<Quest>();public Quest ActiveQuest,TrackedQuest;public int Refreshes,History;public void RefreshTracked(){Refreshes++;}public void AddPOIToTraderData(int tier,UnityEngine.Vector3 a,UnityEngine.Vector3 b){History++;}}
class Quest {public enum QuestState {InProgress,Failed}public enum PositionDataTypes {POIPosition,TraderPosition}public string ID="clear",QuestUniqueId="original-quest";public int QuestCode=-12;public int SharedOwnerID=-1;public System.Collections.Generic.List<ObjectiveRallyPoint> Objectives=new System.Collections.Generic.List<ObjectiveRallyPoint>();public byte CurrentPhase;public QuestState CurrentState=QuestState.InProgress;public bool RallyMarkerActivated,Tracked,Requirements=true;public QuestJournal OwnerJournal;public QuestClass QuestClass=new QuestClass();public Dictionary<PositionDataTypes,UnityEngine.Vector3> PositionData=new Dictionary<PositionDataTypes,UnityEngine.Vector3>();public int Removed,PartyPruned;public void RemoveSharedNotInRange(){PartyPruned++;}public bool CheckRequirements()=>Requirements;public void RemoveMapObject(){Removed++;}}
class ObjectiveRallyPoint {public static ObjectiveRallyPoint OutstandingRallyPoint;public Quest OwnerQuest;public bool Complete;public byte Phase,CurrentValue;public int Party,Finished;public bool ThrowParty;public string activateEvent="event";public void HandleParty(){Party++;if(ThrowParty)throw new Exception("native party failure");}public void RallyPointActivated(){Finished++;CurrentValue=1;OutstandingRallyPoint=null;}}
class GameEventManager {public static GameEventManager Current=new GameEventManager();public int Events;public void HandleAction(string key,object a,EntityPlayerLocal b,bool twitchActivated,Vector3i position){Events++;}}
class ClientInfo {public int entityId=1;public PlayerDataFile latestPlayerData;}
class PlayerDataFile {public int id=1;public QuestJournal questJournal;}
class Clients {public ClientInfo Sender;public ClientInfo ForEntityId(int id)=>Sender!=null&&Sender.entityId==id?Sender:null;}
class GameIO {public static string Save;public static string GetSaveGameDir()=>Save;}
static class Program
{
 static int checks;static void Check(bool value,string text){if(!value)throw new Exception("FAIL "+text);checks++;Console.WriteLine("PASS "+text);}
 sealed class Native:System.Collections.IEnumerator {public Action Effect;int step;public object Current=>null;public bool MoveNext(){if(step++==0){Effect?.Invoke();return true;}return false;}public void Reset()=>throw new NotSupportedException();}
 static void Main()
 {
  GameIO.Save=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"PurgeAuthenticatedRally_"+Guid.NewGuid().ToString("N"));
  var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;var game=new GameManager{World=world};GameManager.Instance=game;
  var journal=new QuestJournal();player.QuestJournal=journal;var quest=new Quest{OwnerJournal=journal};journal.quests.Add(quest);var poi=new UnityEngine.Vector3(10,2,30);quest.PositionData.Add(Quest.PositionDataTypes.POIPosition,poi);quest.Objectives.Add(new ObjectiveRallyPoint{OwnerQuest=quest});
  var sender=new ClientInfo{latestPlayerData=new PlayerDataFile{questJournal=journal}};var manager=new ConnectionManager{IsServer=true};manager.Clients.Sender=sender;SingletonMonoBehaviour<ConnectionManager>.Instance=manager;
  var request=Guid.NewGuid();var saved=Guid.Parse(world.worldState.Guid);
  bool Capture(out RebirthPoiAuthenticatedRallyScope value)=>RebirthPoiAuthenticatedRallyScope.TryCapture(world,sender,request,saved,1,quest.ID,quest.QuestUniqueId,quest.QuestCode,quest.CurrentPhase,poi,out value);
  Check(Capture(out var scope)&&scope.IsOriginalCurrent&&journal.OwnerPlayer==null,"actual authenticated server scope reads original journal without local OwnerPlayer methods");
  Check(!RebirthPoiAuthenticatedRallyScope.TryCapture(world,new ClientInfo{latestPlayerData=sender.latestPlayerData},request,saved,1,quest.ID,quest.QuestUniqueId,quest.QuestCode,0,poi,out _),"spoofed native sender reference refused");
  Check(!RebirthPoiAuthenticatedRallyScope.TryCapture(world,sender,request,Guid.NewGuid(),1,quest.ID,quest.QuestUniqueId,quest.QuestCode,0,poi,out _),"wrong server saved world refused");
  Check(!RebirthPoiAuthenticatedRallyScope.TryCapture(world,sender,request,saved,1,"wrong",quest.QuestUniqueId,quest.QuestCode,0,poi,out _),"client requested class not present in authenticated native journal refused");
  var originalData=sender.latestPlayerData;sender.latestPlayerData=new PlayerDataFile{questJournal=journal};Check(!scope.IsOriginalCurrent,"replacement native player data invalidates original callback even same journal");sender.latestPlayerData=originalData;
  quest.CurrentPhase++;Check(!scope.IsOriginalCurrent,"native quest phase change invalidates original callback");quest.CurrentPhase--;
  quest.SharedOwnerID=2;Check(!Capture(out _),"shared nonowner cannot authorize original reset");quest.SharedOwnerID=-1;
  quest.Objectives.Add(new ObjectiveRallyPoint{OwnerQuest=quest});Check(!Capture(out _),"ambiguous active native rally objective refused");quest.Objectives.RemoveAt(1);
  Check(!RebirthPoiAuthenticatedRallyScope.ValidIdentifier("bad\ud800",512)&&!RebirthPoiAuthenticatedRallyScope.ValidIdentifier("bad\n",512),"invalid UTF16 and control identifiers rejected before authorization");
  RebirthPoiWorldBinding.TryCapture(world,out var binding);Check(RebirthPoiWorldStore.TryOpen(binding,out var store)==RebirthPoiStoreResult.Published,"real durable original server world opened");
  var identity=new RebirthPoiIdentity("house",10,2,30,0,10,10,10,"forest");store.TryDiscover(store.Published,identity);
  var plan=new RebirthPoiResetPlan(request,RebirthPoiResetCaller.Quest,new string('a',64),new long[]{1},new int[]{7},Array.Empty<int>());var native=new Native();double time=0;
  var batch=new RebirthPoiResetBatchCallerProtocol(store,store.Published,new[]{new RebirthPoiResetBatchEntry(identity,plan)},native,()=>time);
  Check(!scope.TryCompleteOriginal(batch,out _),"sender authentication alone cannot produce completion before actual original batch");
  native.Effect=()=>{batch.ChunkCopied(identity,world,request,1,true);batch.ChunkRegenerated(identity,world,request,1,true);batch.VolumeReset(identity,world,request,7);batch.TriggersRefreshed(identity,world,request,plan.Manifest);};batch.MoveNext();batch.MoveNext();
  Check(scope.TryCompleteOriginal(batch,out var receipt)&&receipt.Request==request&&receipt.World==saved&&receipt.QuestCode==-12,"actual completed production batch creates original authenticated rally receipt with negative native quest code");
  Check(scope.TryCompleteOriginal(batch,out var duplicate)&&ReferenceEquals(receipt,duplicate),"duplicate original request retains exact immutable completion receipt");
  RebirthPoiResetBatchCallerProtocol CompleteOther(RebirthPoiIdentity target,RebirthPoiResetCaller caller)
  {
   store.TryDiscover(store.Published,target);var otherPlan=new RebirthPoiResetPlan(request,caller,new string('b',64),new long[]{2},new int[]{8},Array.Empty<int>());var otherNative=new Native();var other=new RebirthPoiResetBatchCallerProtocol(store,store.Published,new[]{new RebirthPoiResetBatchEntry(target,otherPlan)},otherNative,()=>time);
   otherNative.Effect=()=>{other.ChunkCopied(target,world,request,2,true);other.ChunkRegenerated(target,world,request,2,true);other.VolumeReset(target,world,request,8);if(otherPlan.RequiresTriggerRefresh)other.TriggersRefreshed(target,world,request,otherPlan.Manifest);};other.MoveNext();other.MoveNext();return other;
  }
  var unrelated=CompleteOther(new RebirthPoiIdentity("elsewhere",100,2,100,0,10,10,10,"forest"),RebirthPoiResetCaller.Quest);Check(!scope.TryCompleteOriginal(unrelated,out _),"completed same request nonce in unrelated native POI cannot satisfy original rally");
  var natural=CompleteOther(new RebirthPoiIdentity("overlap",10,2,30,0,10,10,10,"forest"),RebirthPoiResetCaller.NaturalRespawn);Check(!scope.TryCompleteOriginal(natural,out _),"completed nonquest native generation cannot satisfy original rally even same position and nonce");
  sender.latestPlayerData=new PlayerDataFile{questJournal=journal};Check(!scope.TryCompleteOriginal(batch,out _),"late native journal replacement cannot deliver original completion");sender.latestPlayerData=originalData;
  GameManager.Instance=new GameManager{World=world};Check(!scope.TryCompleteOriginal(batch,out _),"replacement native manager invalidates late server outcome");GameManager.Instance=game;
  var wire=new RebirthPoiRallyWireFrame(Guid.NewGuid(),Guid.NewGuid(),1,"clear","original-quest",-12,1,new UnityEngine.Vector3(10,2,30));var wireBytes=RebirthPoiRallyWireFrame.Encode(wire);RebirthPoiRallyWireFrame parsed;
  Check(RebirthPoiRallyWireFrame.TryDecode(wireBytes,out parsed)&&wire.SameRequest(parsed)&&parsed.Status==RebirthPoiRallyWireStatus.Request,"bounded remote request roundtrips exact original scope");
  var completedWire=wire.Reply(RebirthPoiRallyWireStatus.Completed,4);Check(RebirthPoiRallyWireFrame.TryDecode(RebirthPoiRallyWireFrame.Encode(completedWire),out parsed)&&wire.SameRequest(parsed)&&parsed.Revision==4,"confirmed original reply preserves immutable request and revision");
  Check(wire.SameRequest(wire.Reply(RebirthPoiRallyWireStatus.Refused))&&wire.SameRequest(wire.Reply(RebirthPoiRallyWireStatus.Unknown)),"refusal and uncertainty retain original correlation");
  for(int n=0;n<wireBytes.Length;n++){var truncated=new byte[n];Array.Copy(wireBytes,truncated,n);Check(!RebirthPoiRallyWireFrame.TryDecode(truncated,out parsed),"truncated remote scope refused "+n);}
  var invalid=new byte[wireBytes.Length+1];Array.Copy(wireBytes,invalid,wireBytes.Length);Check(!RebirthPoiRallyWireFrame.TryDecode(invalid,out parsed),"trailing remote bytes refused");invalid=(byte[])wireBytes.Clone();invalid[4]=255;Check(!RebirthPoiRallyWireFrame.TryDecode(invalid,out parsed),"unknown remote outcome cannot authorize success");invalid=(byte[])wireBytes.Clone();invalid[4]=(byte)RebirthPoiRallyWireStatus.Completed;Check(!RebirthPoiRallyWireFrame.TryDecode(invalid,out parsed),"zero completion revision refused");
  Check(!wire.SameRequest(new RebirthPoiRallyWireFrame(Guid.NewGuid(),wire.World,wire.Player,wire.QuestId,wire.Unique,wire.QuestCode,wire.Phase,wire.Poi)),"foreign original request cannot match remote outcome");
  Check(!wire.SameRequest(new RebirthPoiRallyWireFrame(wire.Request,Guid.NewGuid(),wire.Player,wire.QuestId,wire.Unique,wire.QuestCode,wire.Phase,wire.Poi)),"foreign saved world cannot match remote outcome");
  bool invalidWire=false;try{new RebirthPoiRallyWireFrame(wire.Request,wire.World,wire.Player,wire.QuestId,wire.Unique,wire.QuestCode,wire.Phase,new UnityEngine.Vector3(float.NaN,2,30));}catch(ArgumentException){invalidWire=true;}Check(invalidWire,"nonfinite remote location refused");
  Console.WriteLine("RESULT "+checks+" PASS; production authenticated scope and original batch/domain/store; real files; native sender/journal/world/effect calls explicitly doubled; mutation hooks and full rally eligibility absent.");
 }
}