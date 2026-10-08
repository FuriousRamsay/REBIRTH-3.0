using System.Linq;
using System;using System.IO;
public static class RemoteResourceClientTransactionCoordinator {public static bool Pending;public static bool HasPendingInventoryOperation(EntityPlayerLocal p)=>Pending;}
public enum RebirthGearOwnerReceipt {Rejected=-1,Applied=1,Applying=2}
public class RebirthGearTransferState {public string CreationId="creation";public long ExpectedRevision=2;public string PreparationRequestDigest;public string TransactionId="11111111111111111111111111111111";public System.Xml.Linq.XElement ToXml()=>new("offer",new System.Xml.Linq.XAttribute("transaction",TransactionId),new System.Xml.Linq.XAttribute("digest",PreparationRequestDigest??""));public static bool TryRead(System.Xml.Linq.XElement x,out RebirthGearTransferState o){o=new(){TransactionId=(string)x.Attribute("transaction"),PreparationRequestDigest=string.IsNullOrEmpty((string)x.Attribute("digest"))?null:(string)x.Attribute("digest")};return o.TransactionId!=null;}public bool TryGetPlan(out Plan p){p=new Plan();return true;}}
public class Plan {public object GearBefore;public static bool Before=true,After=true;public bool MatchesBefore(object bag,object belt,object gear,int b,int t)=>Before;public bool MatchesAppliedInventory(object bag,object belt)=>After;}
public class RebirthStablePlayerIdentity {public string CanonicalId="local";public static bool Available=true;public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity i){i=new();return Available;}}
public class Identity {public string CombinedString="local";}
public class World {public WorldState worldState=new();public bool Remote=true;public bool IsRemote()=>Remote;public EntityPlayerLocal Player;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public object GetEntity(int id)=>Player;}
public class Buffs {public System.Collections.Generic.Dictionary<string,float> CVars=new();public float Value;public int Writes;public Action OnWrite;public void RemoveCustomVar(string key){CVars.Remove(key);OnWrite?.Invoke();}public float GetCustomVar(string key)=>key.StartsWith("_rbgearintent_")?(CVars.TryGetValue(key,out var v)?v:0):Value;public void SetCustomVar(string key,float v,bool sync){if(key.StartsWith("_rbgearintent_"))CVars[key]=v;else Value=v;Writes++;OnWrite?.Invoke();}}
public class Grid {public object[] items=new object[65];}
public class Bag {public bool Running;public bool IsHoldingItemActionRunning()=>Running;public Grid ItemGrid=new();}
public class EntityPlayerLocal {public PlayerUI PlayerUI=new();public int entityId=1;public World world;public Buffs Buffs=new();public Bag bag=new(),inventory=new();public bool Dead,Spawned=true;public bool IsDead()=>Dead;public bool IsSpawned()=>Spawned;}
public class GameManager {public static GameManager Instance;public World World;public Identity Identity=new();public int Saves;public bool Fail;public Action OnSave;public Identity getPersistentPlayerID(object o)=>Identity;public void PlayerSpawnedInWorld(ClientInfo c,RespawnType r,Vector3i pos,int id){}public void SaveLocalPlayerData(){Saves++;OnSave?.Invoke();if(!Fail){RebirthGearPlayerFileWitness.Saved=(int)World.Player.Buffs.Value;RebirthGearPreparationPlayerFileWitness.Saved=World.Player.Buffs.CVars.Count>0;}}}
public static class GameIO {public static string Root=Path.GetTempPath();public static string GetPlayerDataDir()=>Root;}
public static class RebirthCharacterCreationHoldService {public static bool Held;public static bool IsHeld(EntityPlayerLocal p)=>Held;}
public static class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld()=>Enabled;}
public class Cell {public string ItemData="";public int Count;}
public class RebirthGearInventorySnapshot {public static bool Different;public int OwnedBeltSlots=4;public Cell[] Bag=System.Linq.Enumerable.Range(0,65).Select(_=>new Cell()).ToArray(),Belt=System.Linq.Enumerable.Range(0,20).Select(_=>new Cell()).ToArray();public bool IsUsableSource(bool bag,int index)=>index>=0&&index<(bag?Bag.Length:OwnedBeltSlots);public static bool Valid=true;public static bool TryCapture(object[] b,object[] t,int n,out RebirthGearInventorySnapshot s){s=new();if(Different)s.Bag[64].Count=1;return Valid;}}
public static class RebirthGearPlayerFileWitness {public static bool HasRetired(RebirthStablePlayerIdentity i,RebirthGearTransferState o,bool a)=>Saved==(a?1:-1)&&!RebirthGearPreparationPlayerFileWitness.Saved;public static int Saved;public static bool HasRejected(RebirthStablePlayerIdentity i,RebirthGearTransferState o)=>Saved==-1;public static bool HasApplied(RebirthStablePlayerIdentity i,RebirthGearTransferState o)=>Saved==1;public static bool HasApplying(RebirthStablePlayerIdentity i,RebirthGearTransferState o)=>Saved==2;}
public class Stack {public bool Empty=true;public bool IsEmpty()=>Empty;}
public class Drag {public Stack CurrentStack=new();}
public class Xui {public bool IsUsingItemActionEntryUse;public Drag DragAndDropWindow=new();}
public class PlayerUI {public Xui xui=new();}
public static class RebirthBackpackLibraryReservation {public static bool Held;public static bool IsHeld(EntityPlayerLocal p)=>Held;}
public class Connection {public bool Disconnected;public bool IsDisconnected()=>Disconnected;}
public class ConnectionManager {public System.Collections.Generic.List<object> Sent=new();public Action OnSend;public void SendToServer(object packet){Sent.Add(packet);OnSend?.Invoke();}public bool IsServer;public Connection[] connectionToServer=new[]{new Connection()};}
public static class SingletonMonoBehaviour<T> {public static T Instance;}
public static class RebirthSurvivorClientState {public static string Creation="creation";public static string GetProjectedCreationId(EntityPlayerLocal p)=>Creation;public static string GetProjectedGearItem(EntityPlayerLocal p,string slot)=>"gear";}
public static class RebirthSurvivorRequestScope {public static bool TryNormalize(string value,out string normalized){normalized=value;return !string.IsNullOrEmpty(value);}public static bool Matches(string a,string b)=>a==b&&!string.IsNullOrEmpty(a);}
public static class RebirthToolbeltCapacity {public static int GetOwnedSlotCount(EntityPlayerLocal p,int n)=>4;}
public class RebirthGearPreparationIntent {public Guid TransactionId=Guid.Parse("22222222222222222222222222222222");public string CreationId="creation",Id="original";public static bool TryCreate(string c,Guid t,long r,int type,ushort seed,bool bag,int index,RebirthGearInventorySnapshot snap,out RebirthGearPreparationIntent i){i=null;throw new NotSupportedException("Candidate domain is outside this fixture; use actual intent fixture.");}public static bool TryCreateUnequip(string c,Guid t,long r,string slot,RebirthGearInventorySnapshot snapshot,out RebirthGearPreparationIntent i){i=null;throw new NotSupportedException("Candidate domain is outside this fixture; use actual intent fixture.");}public static bool Image=true;public System.Xml.Linq.XElement Write()=>new("intent",new System.Xml.Linq.XAttribute("creation",CreationId),new System.Xml.Linq.XAttribute("id",Id));public static bool TryRead(System.Xml.Linq.XElement x,out RebirthGearPreparationIntent value){value=new(){CreationId=(string)x.Attribute("creation"),Id=(string)x.Attribute("id")};return value.Id!=null&&value.CreationId!=null;}public bool MatchesInventory(RebirthGearInventorySnapshot s)=>Image;}
public static class RebirthGearPreparationOfferBinding {public static bool Exact=true;public static Action OnCheck;public static bool MatchesBound(RebirthGearPreparationIntent i,RebirthGearInventorySnapshot s,RebirthGearTransferState o,Guid world){OnCheck?.Invoke();return Exact;}}
public class WorldState {public string Guid=System.Guid.NewGuid().ToString("N");}
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public static class RebirthGearPreparationPlayerFileWitness {public static float Phase;public static bool OriginalPhase;public static bool TryReadOriginalPhase(RebirthStablePlayerIdentity o,Guid w,out string marker,out RebirthGearPreparationIntent intent,out RebirthGearInventorySnapshot inventory,out float receipt){intent=new();RebirthGearPreparationMarker.TryEncode(w,4,intent,out marker);inventory=new();receipt=Phase;return OriginalPhase;}public static bool Saved;public static bool HasOriginal(RebirthStablePlayerIdentity owner,Guid world,string marker,RebirthGearPreparationIntent intent)=>Saved&&RebirthGearPreparationIntent.Image;}
public enum EnumGamePrefs {GameGuidClient}
public static class GamePrefs {public static string WorldOverride;public static string GetString(EnumGamePrefs pref)=>WorldOverride??GameManager.Instance.World.worldState.Guid;}
public static class NetPackageManager {public static bool Missing;public static int GetPackageId(Type t){if(Missing)throw new Exception("mapping");return 1;}public static T GetPackage<T>()where T:new()=>new();}
public class NetPackagePlayerData {public string Marker;public static Action OnSetup;public NetPackagePlayerData Setup(EntityPlayerLocal p){Marker=System.Linq.Enumerable.FirstOrDefault(p.Buffs.CVars.Keys,k=>k.StartsWith("_rbgearintent_"));OnSetup?.Invoke();return this;}}
public class NetPackageRebirthGearPreparationRequest {public int Player;public string Marker;public NetPackageRebirthGearPreparationRequest Setup(int id,string marker){Player=id;Marker=marker;return this;}}
public class NetPackageRebirthGearOfferFragment {}
class Program{
 static int checks;static EntityPlayerLocal player;static RebirthGearTransferState offer=new();static bool current;
 static void Reset(){NetPackageManager.Missing=false;NetPackagePlayerData.OnSetup=null;GamePrefs.WorldOverride=null;ThreadManager.Main=true;RebirthGearPreparationPlayerFileWitness.Saved=false;SingletonMonoBehaviour<ConnectionManager>.Instance=new();RebirthGearPreparationIntent.Image=true;RebirthGearPreparationOfferBinding.Exact=true;RebirthGearPreparationOfferBinding.OnCheck=null;RebirthSurvivorClientState.Creation="creation";RemoteResourceClientTransactionCoordinator.Pending=false;RebirthGearOwnerReservation.ResetSession();RebirthBackpackLibraryReservation.Held=false;player=new();var world=new World{Player=player};player.world=world;GameManager.Instance=new(){World=world};current=true;Plan.Before=true;Plan.After=true;RebirthStablePlayerIdentity.Available=true;RebirthCharacterCreationHoldService.Held=false;RebirthSurvivorMode.Enabled=true;RebirthGearInventorySnapshot.Valid=true;RebirthGearPlayerFileWitness.Saved=0;GameIO.Root=Path.GetTempPath();}
 static bool Save(RebirthGearOwnerReceipt stage)=>RebirthGearOwnerSaveCheckpoint.TryPersist(player,offer,stage,()=>current);
 static void Check(bool x,string why){if(!x)throw new Exception(why);checks++;}
 static void ColdHoldChecks(){
 foreach(float stage in new float[]{0,-1,1,2}){
 Reset();RebirthGearPreparationPlayerFileWitness.OriginalPhase=true;RebirthGearPreparationPlayerFileWitness.Phase=stage;RebirthGearInventorySnapshot.Different=false;
 var connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];var intent=new RebirthGearPreparationIntent();RebirthGearPreparationMarker.TryEncode(Guid.Parse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient)),4,intent,out var marker);player.Buffs.CVars[marker]=1;player.Buffs.Value=stage;
 Check(RebirthGearOwnerReservation.TryRestoreOriginal(player,connection)&&RebirthGearOwnerReservation.MatchesIntent(player,connection,intent),"same original cold stage holds before retained offer");
 Check(RebirthGearOwnerReservation.TryRestoreOriginal(player,connection),"same original cold hold idempotent");
 Check(!RebirthGearOwnerReservation.TryGetInitialIntent(player,connection,out _),"cold restored hold cannot downgrade into initial save retry");
 Check(RebirthGearPreparationClient.TryRequestSaved(player)&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sent.Count==2&&
 SingletonMonoBehaviour<ConnectionManager>.Instance.Sent[0] is NetPackagePlayerData upload&&upload.Marker==marker&&
 SingletonMonoBehaviour<ConnectionManager>.Instance.Sent[1] is NetPackageRebirthGearPreparationRequest query&&query.Marker==marker&&GameManager.Instance.Saves==0,"cold original query snapshots native player first without initial save or replacement marker");
 Check(!RebirthGearPreparationClient.TryRequestSaved(player)&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sent.Count==2,"cold duplicate original query throttled");
 var coldOffer=new RebirthGearTransferState{PreparationRequestDigest=new string('a',64)};
 RebirthGearPreparationRecoveryBinding.Valid=false;Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,coldOffer)&&RebirthGearOwnerReservation.IsHeld(player),"invalid original recovery binding refuses adoption while held");
 RebirthGearPreparationRecoveryBinding.Valid=true;RebirthGearPreparationRecoveryBinding.OnCheck=()=>RebirthGearInventorySnapshot.Different=true;
 Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,coldOffer),"physical change during cold binding refuses adoption");RebirthGearInventorySnapshot.Different=false;RebirthGearPreparationRecoveryBinding.OnCheck=null;
 Check(RebirthGearOwnerReservation.TryAcquire(player,connection,coldOffer)&&RebirthGearOwnerReservation.Matches(player,connection,coldOffer),"same original retained offer adopted at saved native phase");
 RebirthGearInventorySnapshot.Different=true;Check(!RebirthGearOwnerReservation.TryRestoreOriginal(player,connection)&&RebirthGearOwnerReservation.IsHeld(player),"changed physical tail cannot rebind cold hold");RebirthGearInventorySnapshot.Different=false;
 player.Buffs.Value=stage==2?1:2;Check(!RebirthGearOwnerReservation.TryRestoreOriginal(player,connection),"live receipt differs from same final file stage");
 }
 Reset();RebirthGearPreparationPlayerFileWitness.OriginalPhase=false;Check(!RebirthGearOwnerReservation.TryRestoreOriginal(player,SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0])&&!RebirthGearOwnerReservation.IsHeld(player),"missing original cold file cannot invent hold intent");
 }
 static void TerminalChecks(){
 Reset();var connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];var intent=new RebirthGearPreparationIntent();var bound=new RebirthGearTransferState{PreparationRequestDigest=new string('a',64)};
 RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);RebirthGearOwnerReservation.TryAcquire(player,connection,bound);
 RebirthGearTerminalClient.Marker="_rbgearintent_test";RebirthGearTerminalClient.Value=new(){CreationId=bound.CreationId,TransactionId=bound.TransactionId,GearRevision=3,PreparationRequestDigest=bound.PreparationRequestDigest,Applied=true};
 player.Buffs.CVars[RebirthGearTerminalClient.Marker]=1;player.Buffs.Value=1;RebirthGearPlayerFileWitness.Saved=1;RebirthGearPreparationPlayerFileWitness.Saved=true;
 GameManager.Instance.Fail=true;Check(!RebirthGearTerminalOwnerCheckpoint.TryPersist(player,connection,bound)&&RebirthGearOwnerReservation.IsHeld(player)&&player.Buffs.CVars.Count==0,"uncertain terminal save retains hold and same retirement intent");
 GameManager.Instance.Fail=false;Check(RebirthGearTerminalOwnerCheckpoint.TryPersist(player,connection,bound)&&RebirthGearTerminalOwnerCheckpoint.HasSavedRetirement(player,bound,true)&&RebirthGearOwnerReservation.IsHeld(player),"same original terminal save retry proves absence without release");
 int saves=GameManager.Instance.Saves;Check(RebirthGearTerminalOwnerCheckpoint.TryPersist(player,connection,bound)&&GameManager.Instance.Saves==saves,"saved terminal absence retry idempotent");
 player.Buffs.CVars["_RBGEARINTENT_future"]=0;Check(!RebirthGearTerminalOwnerCheckpoint.TryPersist(player,connection,bound),"competing zero family key prevents terminal cleanup");player.Buffs.CVars.Clear();
 Plan.After=false;Check(!RebirthGearTerminalOwnerCheckpoint.TryPersist(player,connection,bound),"changed live postimage prevents cleanup");Plan.After=true;
 connection.Disconnected=true;Check(!RebirthGearTerminalOwnerCheckpoint.TryPersist(player,connection,bound),"disconnected original connection prevents cleanup");connection.Disconnected=false;
 Check(!RebirthGearOwnerReservation.ReleaseSettledWithTransport(player,connection,bound,()=>true,()=>false)&&RebirthGearOwnerReservation.IsHeld(player),"failed inbox finalization retains original hold");
 Check(RebirthGearOwnerReservation.ReleaseSettledWithTransport(player,connection,bound,()=>true,()=>true)&&!RebirthGearOwnerReservation.IsHeld(player),"transport finalized before hold release");
 RebirthGearTerminalClient.Value.Applied=false;Check(!RebirthGearTerminalOwnerCheckpoint.TryPersist(player,connection,bound),"terminal outcome conflicting with local receipt refuses");
 }
 static void BoundReservationChecks(){
 Reset();var bound=new RebirthGearTransferState{PreparationRequestDigest=new string('a',64)};var connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];var intent=new RebirthGearPreparationIntent();
 Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,bound)&&!RebirthGearOwnerReservation.IsHeld(player),"bound offer cannot bypass original early hold");
 GamePrefs.WorldOverride="unknown";Check(!RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent),"early original hold requires saved server world identity");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);GamePrefs.WorldOverride=Guid.NewGuid().ToString("N");
 Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,bound)&&RebirthGearOwnerReservation.IsHeld(player),"saved server world change cannot adopt bound offer or release hold");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);RebirthGearPreparationOfferBinding.OnCheck=()=>GamePrefs.WorldOverride=Guid.NewGuid().ToString("N");
 Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,bound)&&RebirthGearOwnerReservation.IsHeld(player),"world changes during binding withhold adoption");
 }
 static Connection PrepareBeforeProjection(){
  Reset();RebirthGearInventorySnapshot.Different=false;var connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];
  var intent=new RebirthGearPreparationIntent();RebirthGearPreparationPlayerFileWitness.OriginalPhase=true;
  RebirthGearPreparationPlayerFileWitness.Phase=0;player.Buffs.Value=0;
  RebirthGearPreparationMarker.TryEncode(Guid.Parse(player.world.worldState.Guid),4,intent,out var marker);
  player.Buffs.CVars[marker]=1;RebirthSurvivorClientState.Creation="";RebirthCharacterCreationHoldService.Held=true;
  return connection;
 }
 static void BeforeProjectionChecks(){
  var connection=PrepareBeforeProjection();
  Check(RebirthGearOwnerReservation.TryRestoreOriginalBeforeProjection(player,connection)&&RebirthGearOwnerReservation.IsHeld(player),"exact original file/live grids acquire custody before owner publication");
  Check(!RebirthGearOwnerReservation.TryGetInitialIntent(player,connection,out _),"early restored custody remains cold not initial preimage retry");
  Check(RebirthGearOwnerReservation.TryGetHeldOriginalCreation(player,out var retainedCreation)&&retainedCreation=="creation","unknown projection exposes original identity only for native maintenance");
  var bound=new RebirthGearTransferState{PreparationRequestDigest="original"};
  Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,bound),"unknown server creation cannot authorize cold offer adoption");
  RebirthSurvivorClientState.Creation="creation";RebirthCharacterCreationHoldService.Held=false;
  Check(RebirthGearOwnerReservation.TryAcquire(player,connection,bound),"matching server publication later permits exact original cold adoption");
  connection=PrepareBeforeProjection();RebirthSurvivorClientState.Creation="other";
  Check(!RebirthGearOwnerReservation.TryRestoreOriginalBeforeProjection(player,connection)&&!RebirthGearOwnerReservation.IsHeld(player),"known different creation refuses early hold");
  connection=PrepareBeforeProjection();RebirthGearPreparationPlayerFileWitness.OriginalPhase=false;
  Check(!RebirthGearOwnerReservation.TryRestoreOriginalBeforeProjection(player,connection)&&!RebirthGearOwnerReservation.IsHeld(player),"missing final original cannot invent early hold");
  connection=PrepareBeforeProjection();RebirthGearInventorySnapshot.Different=true;
  Check(!RebirthGearOwnerReservation.TryRestoreOriginalBeforeProjection(player,connection),"different live physical tail refuses early hold");
  connection=PrepareBeforeProjection();
  Check(!RebirthGearOwnerReservation.TryRestoreOriginalBeforeProjection(player,new object()),"foreign native session refuses early hold");
  connection=PrepareBeforeProjection();RebirthBackpackLibraryReservation.Held=true;
  Check(!RebirthGearOwnerReservation.TryRestoreOriginalBeforeProjection(player,connection),"other resource custody refuses early hold");
  connection=PrepareBeforeProjection();RebirthGearColdSpawnAdmissionPatch.Prefix(GameManager.Instance,null,RespawnType.JoinMultiplayer,player.entityId);
  Check(RebirthGearOwnerReservation.IsHeld(player),"actual local join hook acquires original custody before native observers");
  connection=PrepareBeforeProjection();RebirthGearColdSpawnAdmissionPatch.Prefix(GameManager.Instance,new ClientInfo(),RespawnType.JoinMultiplayer,player.entityId);
  Check(!RebirthGearOwnerReservation.IsHeld(player),"remote sender spawn cannot invoke local cold admission");
  connection=PrepareBeforeProjection();RebirthGearColdSpawnAdmissionPatch.Prefix(GameManager.Instance,null,RespawnType.EnterMultiplayer,player.entityId);
  Check(!RebirthGearOwnerReservation.IsHeld(player),"new multiplayer spawn remains native");
  connection=PrepareBeforeProjection();RebirthGearColdSpawnAdmissionPatch.Prefix(GameManager.Instance,null,RespawnType.JoinMultiplayer,player.entityId+1);
  Check(!RebirthGearOwnerReservation.IsHeld(player),"other entity spawn cannot acquire primary original custody");
  Reset();
 }
 static void EarlyIntentChecks(){
 Reset();var intent=new RebirthGearPreparationIntent();var connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];
 Check(RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent)&&RebirthGearOwnerReservation.IsHeld(player),"original pre-offer inventory held");
 Check(RebirthGearOwnerReservation.TryGetInitialIntent(player,connection,out var retained)&&!ReferenceEquals(retained,intent)&&System.Xml.Linq.XNode.DeepEquals(retained.Write(),intent.Write()),"initial retry exposes detached same original");
 Check(!RebirthGearOwnerReservation.TryGetInitialIntent(player,new object(),out _),"foreign session cannot obtain initial retry intent");
 Check(RebirthGearOwnerReservation.BlocksInventory(player.bag,true)&&RebirthGearOwnerReservation.BlocksInventory(player.inventory,false),"early hold blocks native bag and belt");
 Check(RebirthGearOwnerReservation.MatchesIntent(player,connection,intent)&&!RebirthGearOwnerReservation.Matches(player,connection,offer),"unadopted intent not matching complete offer");
 Check(RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent),"original early hold idempotent");
 Check(!RebirthGearOwnerReservation.TryAcquireIntent(player,connection,new(){Id="replacement"})&&RebirthGearOwnerReservation.MatchesIntent(player,connection,intent),"early intent not evicted by replacement");
 RebirthGearPreparationOfferBinding.Exact=false;Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,offer)&&RebirthGearOwnerReservation.IsHeld(player),"mismatched offer cannot replace original hold");
 RebirthGearPreparationOfferBinding.Exact=true;Check(RebirthGearOwnerReservation.TryAcquire(player,connection,offer)&&RebirthGearOwnerReservation.Matches(player,connection,offer)&&RebirthGearOwnerReservation.MatchesIntent(player,connection,intent),"exact adoption retains original hold and intent");
 Check(!RebirthGearOwnerReservation.TryGetInitialIntent(player,connection,out _),"adopted offer cannot expose initial preimage retry");
 Check(!RebirthGearOwnerReservation.ReleaseSettled(player,connection,offer,()=>false)&&RebirthGearOwnerReservation.IsHeld(player),"adopted original hold requires terminal saved proof");
 Check(RebirthGearOwnerReservation.ReleaseSettled(player,connection,offer,()=>true)&&!RebirthGearOwnerReservation.IsHeld(player),"adopted original terminal releases hold");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];
 Check(!RebirthGearOwnerReservation.TryAcquireIntent(player,new object(),intent),"foreign native connection cannot acquire original hold");
 RebirthSurvivorClientState.Creation="other";Check(!RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent),"foreign creation refuses early hold");RebirthSurvivorClientState.Creation="creation";
 RebirthGearPreparationIntent.Image=false;Check(!RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent),"changed original image refuses early hold");RebirthGearPreparationIntent.Image=true;
 connection.Disconnected=true;Check(!RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent),"disconnected session refuses early hold");connection.Disconnected=false;
 RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);connection.Disconnected=true;
 Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,offer)&&RebirthGearOwnerReservation.IsHeld(player),"disconnect blocks adoption while retaining original hold");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);
 RebirthGearPreparationOfferBinding.OnCheck=()=>SingletonMonoBehaviour<ConnectionManager>.Instance=new();
 Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,offer)&&RebirthGearOwnerReservation.IsHeld(player),"connection replacement during binding cannot adopt");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];RemoteResourceClientTransactionCoordinator.Pending=true;
 Check(!RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent),"resource custody prevents early hold");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];player.PlayerUI.xui.DragAndDropWindow.CurrentStack.Empty=false;
 Check(!RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent),"active cursor prevents early hold");
 }
 static void AdoptionReadinessChecks(){
 Reset();var intent=new RebirthGearPreparationIntent();var connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);player.Dead=true;
 Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,offer)&&RebirthGearOwnerReservation.IsHeld(player),"dead owner retains intent but cannot adopt");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);RebirthGearPreparationOfferBinding.OnCheck=()=>player.Dead=true;
 Check(!RebirthGearOwnerReservation.TryAcquire(player,connection,offer)&&RebirthGearOwnerReservation.IsHeld(player),"readiness lost during binding cannot adopt");
 }
 static void OriginalCheckpointChecks(){
 Reset();var intent=new RebirthGearPreparationIntent();var connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];var world=Guid.Parse(player.world.worldState.Guid);RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);
 Check(RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,world)&&GameManager.Instance.Saves==1&&RebirthGearOwnerReservation.IsHeld(player),"saved original checkpoint retains hold");
 Check(RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,world)&&GameManager.Instance.Saves==1,"exact original saved retry skips native write");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];world=Guid.Parse(player.world.worldState.Guid);RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);GameManager.Instance.Fail=true;
 Check(!RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,world)&&player.Buffs.CVars.Count==1&&RebirthGearOwnerReservation.IsHeld(player),"unknown original save retains native marker and hold");
 GameManager.Instance.Fail=false;Check(RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,world)&&player.Buffs.CVars.Count==1,"original checkpoint retries same marker");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];world=Guid.Parse(player.world.worldState.Guid);RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);player.Buffs.CVars["_rbgearintent_v2_unknown"]=1;
 Check(!RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,world)&&GameManager.Instance.Saves==0,"competing unknown marker never overwritten");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];world=Guid.Parse(player.world.worldState.Guid);RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);player.Buffs.Value=2;
 Check(!RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,world)&&GameManager.Instance.Saves==0,"applying cannot be downgraded to initial intent");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);
 Check(!RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,Guid.NewGuid())&&player.Buffs.Writes==0,"foreign native world cannot mark original intent");
 world=Guid.Parse(player.world.worldState.Guid);ThreadManager.Main=false;Check(!RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,world)&&player.Buffs.Writes==0,"worker cannot save original intent");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];world=Guid.Parse(player.world.worldState.Guid);RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);player.Buffs.OnWrite=()=>SingletonMonoBehaviour<ConnectionManager>.Instance=new();
 Check(!RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,world)&&GameManager.Instance.Saves==0&&RebirthGearOwnerReservation.IsHeld(player),"connection loss after marking prevents save and preserves hold");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];world=Guid.Parse(player.world.worldState.Guid);RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);GameManager.Instance.OnSave=()=>player.world.worldState.Guid=Guid.NewGuid().ToString("N");
 Check(!RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,world)&&RebirthGearOwnerReservation.IsHeld(player),"world identity changed during save cannot certify checkpoint");
 }
 static void OriginalSavedMarkerRestoreChecks(){
 Reset();var intent=new RebirthGearPreparationIntent();var connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];var world=Guid.Parse(player.world.worldState.Guid);RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);RebirthGearPreparationPlayerFileWitness.Saved=true;
 Check(RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,world)&&player.Buffs.CVars.Count==1&&GameManager.Instance.Saves==0,"saved original marker restored in live owner before upload without rewriting");
 }
 static void OriginalClientWorldScopeChecks(){
 Reset();var intent=new RebirthGearPreparationIntent();var connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];var serverWorld=Guid.NewGuid();GamePrefs.WorldOverride=serverWorld.ToString("N");RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);
 Check(RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,serverWorld),"native handshake savedworld rather than clientgenerated WorldState binds original");
 Reset();connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];serverWorld=Guid.NewGuid();GamePrefs.WorldOverride=serverWorld.ToString("N");RebirthGearOwnerReservation.TryAcquireIntent(player,connection,intent);GameManager.Instance.OnSave=()=>GamePrefs.WorldOverride=Guid.NewGuid().ToString("N");
 Check(!RebirthGearPreparationOwnerCheckpoint.TryPersist(player,connection,intent,serverWorld)&&RebirthGearOwnerReservation.IsHeld(player),"native handshake world changed during save refuses witness");
 }
 static void ClientProducerChecks(){
 Reset();var intent=new RebirthGearPreparationIntent();var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
 Check(RebirthGearPreparationClient.TryRequestOriginal(player,intent)&&manager.Sent.Count==2&&RebirthGearOwnerReservation.IsHeld(player)&&GameManager.Instance.Saves==1,"original saved held request queues two native messages");
 Check(manager.Sent[0] is NetPackagePlayerData snapshot&&manager.Sent[1] is NetPackageRebirthGearPreparationRequest request&&snapshot.Marker==request.Marker&&request.Player==player.entityId,"native snapshot precedes exact original request");
 Check(!RebirthGearPreparationClient.TryRequestOriginal(player,intent)&&manager.Sent.Count==2,"immediate client retry throttled without replacing intent");
 Reset();manager=SingletonMonoBehaviour<ConnectionManager>.Instance;NetPackageManager.Missing=true;Check(!RebirthGearPreparationClient.TryRequestOriginal(player,intent)&&!RebirthGearOwnerReservation.IsHeld(player)&&manager.Sent.Count==0,"missing mapping before original hold/save");
 Reset();manager=SingletonMonoBehaviour<ConnectionManager>.Instance;GameManager.Instance.Fail=true;Check(!RebirthGearPreparationClient.TryRequestOriginal(player,intent)&&RebirthGearOwnerReservation.IsHeld(player)&&manager.Sent.Count==0,"uncertain original save retains hold but sends nothing");
 Reset();manager=SingletonMonoBehaviour<ConnectionManager>.Instance;NetPackagePlayerData.OnSetup=()=>GamePrefs.WorldOverride=Guid.NewGuid().ToString("N");Check(!RebirthGearPreparationClient.TryRequestOriginal(player,intent)&&RebirthGearOwnerReservation.IsHeld(player)&&manager.Sent.Count==0,"world changed while taking native snapshot refuses sending");
 Reset();manager=SingletonMonoBehaviour<ConnectionManager>.Instance;manager.OnSend=()=>manager.connectionToServer[0].Disconnected=true;Check(!RebirthGearPreparationClient.TryRequestOriginal(player,intent)&&manager.Sent.Count==1&&RebirthGearOwnerReservation.IsHeld(player),"disconnect after snapshot leaves original hold and refuses request");
 Reset();manager=SingletonMonoBehaviour<ConnectionManager>.Instance;player.Dead=true;Check(!RebirthGearPreparationClient.TryRequestOriginal(player,intent)&&manager.Sent.Count==0,"dead owner cannot start original request");
 Reset();manager=SingletonMonoBehaviour<ConnectionManager>.Instance;GamePrefs.WorldOverride="unknown";Check(!RebirthGearPreparationClient.TryRequestOriginal(player,intent)&&manager.Sent.Count==0,"unknown native saved world refuses original producer");
 }
 static void Main(){
 ColdHoldChecks();
 TerminalChecks();
 ClientProducerChecks();
 OriginalClientWorldScopeChecks();
 OriginalSavedMarkerRestoreChecks();
 OriginalCheckpointChecks();
 AdoptionReadinessChecks();
 BoundReservationChecks();BeforeProjectionChecks();EarlyIntentChecks();
 Reset();Check(Save(RebirthGearOwnerReceipt.Applying)&&player.Buffs.Value==2&&GameManager.Instance.Saves==1,"applying not saved");
 Check(Save(RebirthGearOwnerReceipt.Applied)&&player.Buffs.Value==1&&RebirthGearPlayerFileWitness.Saved==1,"applied not saved");
 var saves=GameManager.Instance.Saves;Check(Save(RebirthGearOwnerReceipt.Applied)&&GameManager.Instance.Saves==saves,"durable retry rewrote");
 Check(!Save(RebirthGearOwnerReceipt.Applying)&&player.Buffs.Value==1,"applied downgraded");
 Reset();Check(!Save(RebirthGearOwnerReceipt.Applied)&&GameManager.Instance.Saves==0,"applied without intent saved");
 Reset();GameManager.Instance.Fail=true;Check(!Save(RebirthGearOwnerReceipt.Applying)&&player.Buffs.Value==2,"swallowed save failure accepted or marker lost");
 GameManager.Instance.Fail=false;Check(Save(RebirthGearOwnerReceipt.Applying),"intent retry stuck");
 GameManager.Instance.Fail=true;Check(!Save(RebirthGearOwnerReceipt.Applied)&&player.Buffs.Value==1,"failed applied save accepted or marker lost");
 GameManager.Instance.Fail=false;Check(Save(RebirthGearOwnerReceipt.Applied),"unsaved applied retry stuck");
 Reset();Plan.Before=false;Check(!Save(RebirthGearOwnerReceipt.Applying)&&player.Buffs.Writes==0,"conflicting initial preimage marked");
 Reset();player.Buffs.Value=2;Plan.After=false;Check(!Save(RebirthGearOwnerReceipt.Applied)&&player.Buffs.Value==2,"conflicting postimage marked");
 Reset();player.Buffs.Value=-1;Check(!Save(RebirthGearOwnerReceipt.Applying)&&player.Buffs.Writes==0,"rejection resurrected");
 Reset();player.Buffs.Value=3;Check(!Save(RebirthGearOwnerReceipt.Applying),"unknown marker accepted");
 Reset();current=false;Check(!Save(RebirthGearOwnerReceipt.Applying)&&player.Buffs.Writes==0,"lost session marked");
 Reset();player.Buffs.OnWrite=()=>current=false;Check(!Save(RebirthGearOwnerReceipt.Applying)&&GameManager.Instance.Saves==0&&player.Buffs.Value==2,"lost binding after marker saved or rolled back");
 Reset();GameManager.Instance.OnSave=()=>GameManager.Instance.Identity.CombinedString="other";Check(!Save(RebirthGearOwnerReceipt.Applying),"changed native owner accepted");
 Reset();GameManager.Instance.Identity.CombinedString="other";Check(!Save(RebirthGearOwnerReceipt.Applying)&&player.Buffs.Writes==0,"foreign native identity marked");
 Reset();RebirthCharacterCreationHoldService.Held=true;Check(!Save(RebirthGearOwnerReceipt.Applying),"held creation marked");
 Reset();GameManager.Instance.OnSave=()=>GameIO.Root=Path.Combine(Path.GetTempPath(),"other");Check(!Save(RebirthGearOwnerReceipt.Applying),"changed save root accepted");
 Reset();var session=new object();RemoteResourceClientTransactionCoordinator.Pending=true;Check(!RebirthGearOwnerReservation.TryAcquire(player,session,offer),"pending resource operation acquired gear custody");RemoteResourceClientTransactionCoordinator.Pending=false;Check(RebirthGearOwnerReservation.TryAcquire(player,session,offer),"valid reservation refused");
 Check(RebirthGearOwnerReservation.Matches(player,session,offer)&&RebirthGearOwnerReservation.TryAcquire(player,session,offer),"same offer retry refused");
 Check(!RebirthGearOwnerReservation.TryAcquire(player,new object(),offer),"other session acquired");
 Check(!RebirthGearOwnerReservation.TryAcquire(player,session,new(){TransactionId="other"}),"different offer replaced");
 Check(!RebirthGearOwnerReservation.ReleaseSettled(player,session,offer,()=>false)&&RebirthGearOwnerReservation.IsHeld(player),"unverified release cleared");
 Check(!RebirthGearOwnerReservation.ReleaseSettled(player,session,offer,()=>{GameManager.Instance.World=new World();return true;}),"changed world released");
 Reset();Check(RebirthGearOwnerReservation.TryAcquire(player,session,offer)&&RebirthGearOwnerReservation.ReleaseSettled(player,session,offer,()=>true)&&!RebirthGearOwnerReservation.IsHeld(player),"settled release failed");
 Reset();player.PlayerUI.xui.DragAndDropWindow.CurrentStack.Empty=false;Check(!RebirthGearOwnerReservation.TryAcquire(player,session,offer),"active cursor acquired");
 Reset();player.inventory.Running=true;Check(!RebirthGearOwnerReservation.TryAcquire(player,session,offer),"running held action acquired");
 Reset();RebirthBackpackLibraryReservation.Held=true;Check(!RebirthGearOwnerReservation.TryAcquire(player,session,offer),"library conflict acquired");
 Reset();RebirthGearOwnerReservation.TryAcquire(player,session,offer);RebirthGearOwnerReservation.ResetSession();Check(!RebirthGearOwnerReservation.IsHeld(player),"shutdown references retained");
 Reset();RebirthGearOwnerReservation.TryAcquire(player,session,offer);
 Check(RebirthGearOwnerReservation.BlocksInventory(player.bag,true)&&RebirthGearOwnerReservation.BlocksInventory(player.inventory,false),"held native inventories not blocked");
 Check(!RebirthGearOwnerReservation.BlocksInventory(player.bag,false)&&!RebirthGearOwnerReservation.BlocksInventory(player.inventory,true),"wrong inventory area blocked");
 Check(!RebirthGearOwnerReservation.BlocksInventory(new Bag(),true)&&!RebirthGearOwnerReservation.BlocksInventory(null,true),"foreign inventory blocked");
 GameManager.Instance.World=new World();Check(!RebirthGearOwnerReservation.BlocksInventory(player.bag,true),"retired world inventory blocked");
 Reset();Check(Save(RebirthGearOwnerReceipt.Rejected)&&player.Buffs.Value==-1&&RebirthGearPlayerFileWitness.Saved==-1,"untouched rejection not saved");
 var rejectedSaves=GameManager.Instance.Saves;Check(Save(RebirthGearOwnerReceipt.Rejected)&&GameManager.Instance.Saves==rejectedSaves,"saved rejected retry rewrote native file");Check(!Save(RebirthGearOwnerReceipt.Applying)&&!Save(RebirthGearOwnerReceipt.Applied),"rejected outcome resurrected");
 Reset();player.Buffs.Value=2;Check(!Save(RebirthGearOwnerReceipt.Rejected)&&player.Buffs.Writes==0,"applying intent downgraded to rejection");
 Reset();player.Buffs.Value=1;Check(!Save(RebirthGearOwnerReceipt.Rejected)&&player.Buffs.Writes==0,"applied downgraded to rejection");
 Reset();Plan.Before=false;Check(!Save(RebirthGearOwnerReceipt.Rejected)&&player.Buffs.Writes==0,"changed original plan inputs marked rejected");
 Reset();GameManager.Instance.Fail=true;Check(!Save(RebirthGearOwnerReceipt.Rejected)&&player.Buffs.Value==-1,"uncertain rejected save lost marker or accepted");
 GameManager.Instance.Fail=false;Check(Save(RebirthGearOwnerReceipt.Rejected),"unsaved rejection retry stuck");
 Plan.Before=false;Check(!Save(RebirthGearOwnerReceipt.Rejected),"rejection retry skipped live original input evidence");
 Reset();GameManager.Instance.OnSave=()=>current=false;Check(!Save(RebirthGearOwnerReceipt.Rejected)&&player.Buffs.Value==-1,"retired scope claimed rejection saved"); Console.WriteLine("PASS "+checks+" actual native checkpoint checks with game, receipt-file, inventory and identity doubles; no native save execution.");
 }
}
public class NetPackageRebirthGearBoundSettled {}
public class RebirthGearSettlement {public string CreationId,TransactionId,PreparationRequestDigest;public long GearRevision;public bool Applied;public bool MatchesOriginalMarker(string m)=>m==RebirthGearTerminalClient.Marker;public System.Xml.Linq.XElement Write()=>new("terminal",new System.Xml.Linq.XAttribute("applied",Applied));}
public static class RebirthGearTerminalClient {public static string Marker;public static RebirthGearSettlement Value;public static bool TryGetCurrent(EntityPlayerLocal p,out string m,out RebirthGearSettlement t){m=Marker;t=Value;return t!=null;}}public static class RebirthGearPreparationRecoveryBinding {public static bool Valid=true;public static Action OnCheck;public static bool TryRecoverOriginal(RebirthGearPreparationIntent i,RebirthGearTransferState o,RebirthGearInventorySnapshot s,Guid w,int n,float r,out RebirthGearInventorySnapshot before){before=new();OnCheck?.Invoke();return Valid;}}
// New candidate-producer dependencies compile only; this fixture validates actual
// reservation/checkpoint behavior, not native selection or candidate domain rules.
public class ItemValue {public int type=1;public ushort Seed=1;public object ItemClass=new();public bool IsEmpty()=>false;}
public static class RebirthNativeItemCodec {public static string Encode(ItemValue v)=>throw new NotSupportedException("Native codec outside this fixture");}
public static class RebirthBackpackLibraryClientViews {public static bool TryGetRevision(World w,int id,out long r){r=-1;return false;}}

public class ClientInfo{}
public enum RespawnType{JoinMultiplayer,EnterMultiplayer}
public struct Vector3i{}
namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name,Type[] parameters){}}public class HarmonyPrefix:Attribute{}public class HarmonyPriority:Attribute{public HarmonyPriority(int p){}}public static class Priority{public const int First=800;}}