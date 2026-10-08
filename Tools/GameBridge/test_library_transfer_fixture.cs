using System;
using World=PlayerWorld;using System.IO;using System.Text;using System.Xml;
using System.Collections.Generic;
using System.Linq;using System.Xml.Linq;using System.Globalization;
// Disclosed doubles: native item serialization/codec/policy are not exercised by this fixture.
public class ItemClass {public string GetIconName(){return "packIcon";}public static ItemValue GetItem(string id,bool missing){return new ItemValue{Type=1};}public int MaxCount=5;public string GetItemName(){return "pack";}}
public class ItemValue {
 public bool HasModSlots;public bool HasMods(){return false;}public ItemClass ItemClass=new ItemClass();
 public Dictionary<string,string> Metadata=new Dictionary<string,string>();public bool IsEmpty(){return Type==0;}
 public int type{get{return Type;}}public ushort Seed;public int Type;public string Payload; public ItemStack[] Slots;
 public ItemValue Clone(){return new ItemValue{Type=Type,Payload=Payload,Slots=Slots,Metadata=new Dictionary<string,string>(Metadata)};}
 public static void Write(ItemValue v,BinaryWriter w){w.Write(v.Type);w.Write(v.Payload??"");w.Write(v.Metadata.Count);foreach(var p in v.Metadata){w.Write(p.Key);w.Write(p.Value);}}
}
public class ItemStack {
 public ItemStack(){}public ItemStack(ItemValue v,int n){itemValue=v;count=n;}
 public int count;public ItemValue itemValue;
 public static ItemStack Empty=new ItemStack{itemValue=new ItemValue()};
 public bool IsEmpty(){return count==0;}
 public ItemStack Clone(){return new ItemStack{count=count,itemValue=itemValue.Clone()};}
 public bool CanMoveTo(XUiC_ItemStack.StackLocationTypes location){return true;}public bool CanStackPartly(ref int n){n=Math.Min(n,Math.Max(0,5-count));return n>0;}
 public bool CanStackWith(ItemStack s){return itemValue.Type==s.itemValue.Type&&count+s.count<=5;}
}
public static class RebirthBackpackLibraryPolicy { public const int MaxSlots=48; public static int CapacityForBackpack(string id){return id=="pack"?1:0;} public static bool IsLearningMaterial(ItemValue v){return v!=null&&v.Type==1;} }
public static class RebirthBackpackLibraryContents {
 public const string MetadataKey="rebirth.backpack.library.v1";
 public static bool FailWrite;
 public static bool TryRead(ItemValue b,out ItemStack[] c){c=null;if(b==null||b.Slots==null)return false;c=new ItemStack[b.Slots.Length];for(int i=0;i<c.Length;i++)c[i]=b.Slots[i].Clone();return true;}
 public static bool TryWrite(ItemValue b,ItemStack[] c,out ItemValue next){next=null;if(FailWrite)return false;next=b.Clone();next.Slots=c;return true;}
}
public static class RebirthBackpackSellStashPolicy {public const int MaxSlots=80;public static int TestCapacity=1;public static int CapacityForBackpack(string id)=>id=="pack"?TestCapacity:0;public static bool IsStorableItem(ItemValue v){return v!=null&&!v.IsEmpty()&&v.ItemClass!=null;}}
public static class RebirthBackpackSellStashContents {
 public const string MetadataKey="rebirth.backpack.sellstash.v1";
 public static bool TryRead(ItemValue b,out ItemStack[] c){return RebirthBackpackLibraryContents.TryRead(b,out c);}
 public static bool TryWrite(ItemValue b,ItemStack[] c,out ItemValue next){return RebirthBackpackLibraryContents.TryWrite(b,c,out next);}
}
// Actual outcome journal below; source consumption, policy and native user identity are doubles.
public enum RemoteResourceTransactionOutcomeState:byte {Unknown=0,Success=1,Failed=2}
public static class RemoteResourcesRuntimePolicy {public static bool Enabled=true;}
public static class RemoteResourceTransactions {
 public static Action ConsumptionHook;
 public sealed class ConsumptionResult {public bool Success;public string FailureReason;}
 public static ConsumptionResult TryConsume(EntityPlayer player,IList<ItemStack> requirements,int multiplier,List<ItemStack> removed,bool ignored){foreach(var item in requirements)removed.Add(item.Clone());var hook=ConsumptionHook;ConsumptionHook=null;if(hook!=null)hook();return new ConsumptionResult{Success=true};}
}
// OUTCOME_JOURNAL_CLASS
// Native authentication and world-identity persistence are disclosed doubles.
public static class RebirthRemoteGearInventorySource {
 internal static bool TryResolve(EntityPlayer player,ClientInfo sender,string creation,out RebirthWorldCharacterRecord record){record=RebirthWorldCharacterService.Record;return player!=null&&sender?.InternalId!=null&&sender.entityId==player.entityId&&GameManager.Instance!=null&&ReferenceEquals(GameManager.Instance.World,player.world)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&record?.Support!=null&&record.IsComplete&&RebirthSurvivorRequestScope.Matches(creation,record.Origin.CreationId)&&RebirthWorldCharacterRepository.IsServerAuthority;}
}
public static class RemoteResourceWorldIdentity {public static string Key=new string('a',64);internal static bool TryGet(World world,out string key){key=Key;return key!=null&&ReferenceEquals(world,GameManager.Instance.World);}}
// REFUND_PRODUCER_CLASS
public enum RemoteResourceClientOperation:byte {Craft=1,ItemRepair=2,BlockRepair=3,BlockUpgrade=4,CookingPull=5}
public sealed class RefundIdentityDouble {public string StorageKey;}
public static class RepositoryRefundWitnessFixture {
 public static bool serverAuthority=true,Loaded=true,Migrated,ChangedRoot;
 public static int PathReads;public static RebirthWorldCharacterRecord Record;private static readonly object gate=new object();
 private static string GetPath(string key){PathReads++;return ChangedRoot&&PathReads>1?"changed/final.xml":"current/final.xml";}
 private static object GetWriteLock(string key)=>gate;
 private static bool TryLoadValidatedRecord(string path,RefundIdentityDouble identity,out RebirthWorldCharacterRecord record,out bool migrated,out string error,out bool custody){
  record=Record;migrated=Migrated;error="";custody=true;return Loaded;
 }
 // REFUND_WITNESS_METHOD
}
// REFUND_CHECKPOINT_CLASS
// REFUND_CUSTODY_CLASS
// REFUND_SUPPORT_CLASS
// REFUND_JOURNAL_CLASS
// REFUND_RECORD_CLASS
// REMOTE_GRANT_CLASS
// QUICK_CONTINUATION_CLASS
// SECTION_DESTINATION_CLASS
// SELL_UI_CLASS
// SELL_PREPARE_CLASS
// SELL_CLIENT_VIEWS_CLASS
// SELL_VIEW_TRANSPORT_CLASSES
// SELL_VIEW_CACHE_CLASS
// SELL_VIEW_RESPONSE_CLASS
// SELL_VIEW_CODEC_CLASS
// SELL_VIEW_CLASS
// SELL_TRANSFER_CLASS
// SELL_CONSERVATION_CLASS
// PRODUCTION_CLASS
// CONSERVATION_CLASS
// Bounded codec double: deterministic payload/image tokens, detached decode; no real native binary.
public static class RebirthNativeItemCodec {
 static Dictionary<string,ItemValue> images=new Dictionary<string,ItemValue>();
 static ItemValue Copy(ItemValue v){var n=v.Clone();if(v.Slots!=null)n.Slots=v.Slots.Select(s=>new ItemStack(Copy(s.itemValue),s.count)).ToArray();return n;}
 public static string Encode(ItemValue v){
  using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
   ItemValue.Write(v,w);w.Write(v.Slots==null?-1:v.Slots.Length);if(v.Slots!=null)foreach(var s in v.Slots){w.Write(s.count);w.Write(Encode(s.itemValue));}
   w.Flush();string text=Convert.ToBase64String(m.ToArray());images[text]=Copy(v);return text;
  }
 }
 public static bool TryDecode(string text,out ItemValue v){v=null;if(text==null||text.Length>262144||!images.TryGetValue(text,out var n)||n.IsEmpty())return false;v=Copy(n);return true;}
}
public interface INetConnection {bool IsDisconnected();}
public class TestConnection : INetConnection {public bool Disconnected;public bool IsDisconnected(){return Disconnected;}}
public class ConnectionManager {public bool IsServer;public INetConnection[] connectionToServer;public List<NetPackage> Sent=new List<NetPackage>();public List<int> Targets=new List<int>();public int FailSendAt;public void SendToServer(NetPackage package){SendPackage(package,-1);}public void SendPackage(NetPackage package,int _attachedToEntityId){if(FailSendAt==Sent.Count+1)throw new IOException("fixture queue failure");Sent.Add(package);Targets.Add(_attachedToEntityId);}}
public class SingletonMonoBehaviour<T> {public static T Instance;}
public static class RebirthGearOwnerReservation {
 public static bool Held;
 public static bool IsHeld(EntityPlayerLocal player){return Held&&player!=null;}
 public static bool BlocksInventory(object inventory,bool bag){return Held&&inventory!=null;}
 public static void ResetSession(){Held=false;}
}
public static class RebirthSurvivorClientState {public static string Creation;public static string GetProjectedCreationId(EntityPlayer player){return Creation;}}
public enum NetPackageDirection {ToClient,ToServer,Both}
public abstract class NetPackage {public ClientInfo Sender;protected bool ValidEntityIdForSender(int id){return Sender!=null&&Sender.entityId==id;}public abstract NetPackageDirection PackageDirection{get;}public abstract void read(PooledBinaryReader r);public virtual void write(PooledBinaryWriter w){((BinaryWriter)w).Write((ushort)123);}public abstract void ProcessPackage(World world,GameManager callbacks);}
public class Grid {public ItemStack[] items;}
public class SwitchStream:Stream {
 public Stream Target;
 public override bool CanRead{get{return Target==null||Target.CanRead;}}public override bool CanSeek{get{return Target==null||Target.CanSeek;}}public override bool CanWrite{get{return Target==null||Target.CanWrite;}}
 public override long Length{get{return Target.Length;}}public override long Position{get{return Target.Position;}set{Target.Position=value;}}
 public override int Read(byte[] b,int o,int n){return Target.Read(b,o,n);}public override void Write(byte[] b,int o,int n){Target.Write(b,o,n);}
 public override long Seek(long n,SeekOrigin o){return Target.Seek(n,o);}public override void SetLength(long n){Target.SetLength(n);}public override void Flush(){if(Target!=null)Target.Flush();}
}
public class ReaderPool {public PooledBinaryReader AllocSync(bool reset){return new PooledBinaryReader(new SwitchStream());}}
public class WriterPool {public PooledBinaryWriter AllocSync(bool reset){return new PooledBinaryWriter(new SwitchStream());}}
public static class MemoryPools {public static ReaderPool poolBinaryReader=new ReaderPool();public static WriterPool poolBinaryWriter=new WriterPool();}
public class PooledBinaryReader : BinaryReader {public PooledBinaryReader(Stream s):base(s,Encoding.UTF8,true){}public void SetBaseStream(Stream s){((SwitchStream)BaseStream).Target=s;}}
public class PooledBinaryWriter : BinaryWriter {public PooledBinaryWriter(Stream s):base(s,Encoding.UTF8,true){}public void SetBaseStream(Stream s){((SwitchStream)BaseStream).Target=s;}}
public static class NetPackageManager {public static bool Missing;public static Type MissingType;public static int GetPackageId(Type type){if(Missing||type==MissingType)throw new InvalidOperationException("fixture missing mapping");return 123;}public static T GetPackage<T>()where T:NetPackage,new(){return new T();}}
public static class Log {public static void Warning(string value){}}
// RECOVERY_REQUEST_CLASS
// RECOVERY_DELIVERY_CLASS
// OFFER_PACKAGE_CLASS
// PREPARE_PACKAGE_CLASS
// CLIENT_OFFERS_CLASS
// OWNER_INBOX_CLASS
// OFFER_CHUNK_CLASS
// OFFER_INBOX_CLASS
// OFFER_ASSEMBLY_CLASS
// WIRE_CODEC_CLASS
// VIEW_TRANSPORT_CLASSES
// CLIENT_VIEWS_CLASS
// LIBRARY_VIEW_RESPONSE_CLASS
// LIBRARY_VIEW_CACHE_CLASS
// LIBRARY_VIEW_CODEC_CLASS
// LIBRARY_VIEW_CLASS
// RECEIPT_CLASS
// PERSISTENCE_CLASS
public class RebirthWorldSupportState {public XElement RemoteResourceRefundJournalImage;
 public long GearRevision;public RebirthBackpackLibraryPhase LibraryTransferPhase;
 public RebirthBackpackLibrarySettlement LastLibrarySettlement;public RebirthBackpackLibraryReceipt PendingLibraryTransfer;public object PendingGearTransfer,PendingMusicTransfer;
 public Dictionary<string,string> EquippedGearBySlot=new Dictionary<string,string>();
 public Dictionary<string,string> EquippedGearItemDataBySlot=new Dictionary<string,string>();
}
// SETTLEMENT_TRANSPORT_CLASSES
// SETTLEMENT_CLASS
// JOURNAL_CLASS
// Native inventory/CVar doubles can throw after a setter mutates its slot.
public class Buffs {public Dictionary<string,float> Vars=new Dictionary<string,float>();public float GetCustomVar(string k){return Vars.TryGetValue(k,out float n)?n:0;}public void SetCustomVar(string k,float n,bool sync){Vars[k]=n;}}
public class PlayerWorld {public bool Remote;public bool IsRemote(){return Remote;}public EntityPlayerLocal GetPrimaryPlayer(){return Player as EntityPlayerLocal;}public EntityPlayer Player;public object GetEntity(int id){return Player!=null&&Player.entityId==id?Player:null;}}
public static class Localization {public static string Get(string key){return key;}}
public class NetPackagePlayerData {}
public class GameManager {public int NativeSaves;public Action SaveHook;public void SaveLocalPlayerData(){NativeSaves++;if(SaveHook!=null)SaveHook();}public int Checkpoints;public void TriggerSendOfLocalPlayerDataFile(float delay){Checkpoints++;}public InternalPlayerId HostIdentity=new InternalPlayerId();public InternalPlayerId getPersistentPlayerID(ClientInfo ignored){return HostIdentity;}public void doSendLocalInventory(EntityPlayerLocal player){}public static int Tooltips;public static void ShowTooltip(EntityPlayerLocal player,string text){Tooltips++;}public static GameManager Instance;public PlayerWorld World;}
public class Slots {public int Length=>Items.Length;public ItemStack GetItemStack(int i)=>Items[i];public Grid ItemGrid{get{return new Grid{items=Items};}}public int holdingItemIdx;public ItemValue holdingItemItemValue;public void SetHoldingItemIdx(int index){holdingItemIdx=index;holdingItemItemValue=Items[index].itemValue;}public bool ActionRunning;public bool IsHoldingItemActionRunning(){return ActionRunning;}public ItemStack[] Items;public bool[] LockedSlots;public bool ThrowAfter;public int Writes;public ItemStack[] GetSlots(){return Items;}public void SetSlot(int i,ItemStack v){Items[i]=v;Writes++;if(ThrowAfter)throw new Exception("listener after mutation");}public void SetItem(int i,ItemStack v){SetSlot(i,v);}}
public class ItemInventoryData {public ItemStack itemStack;}
public class Inventory : Slots {public int Length{get{return Items.Length;}}public ItemStack GetStackAt(int i){return Items[i];}public void CallOnToolbeltChangedInternal(){Events++;}public int PUBLIC_SLOTS{get{return Items.Length-1;}}public bool CanMoveToSlot(ItemStack stack,int i){return true;}public EntityAlive entity;public int Events;public ItemInventoryData[] slots{get{return Items.Select(s=>new ItemInventoryData{itemStack=s}).ToArray();}}public void notifyListeners(){Events++;}public void SetItem(int i,ItemValue v,int n){base.SetItem(i,new ItemStack(v,n));}}
public class Bag : Slots {public int Events;public void onBackpackChanged(){Events++;}}
public class EntityAlive {public bool bPlayerStatsChanged,isEntityRemote;}
public class EntityPlayer : EntityAlive {public Slots bag,inventory=new Slots();public PlayerWorld world=new PlayerWorld();public int entityId=1;public bool Dead,Held;public bool IsDead(){return Dead;}}
public class EntityPlayerLocal : EntityPlayer {public Buffs Buffs=new Buffs();public PlayerUI PlayerUI=new PlayerUI{xui=new XUi()};public bool IsSpawned(){return true;}}
public static class RebirthSurvivorMode {public static bool IsEnabledForCurrentWorld(){return true;}}
public static class RebirthCharacterCreationHoldService {public static bool IsHeld(EntityPlayer p){return p.Held;}}
public static class RebirthToolbeltCapacity {public static int Owned=4;public static int GetSlotsForPlayer(EntityPlayer p){return GetOwnedSlotCount(p,p.inventory.Items.Length);}public static int GetOwnedSlotCount(EntityPlayer p,int actual){return Math.Min(Owned,actual);}}
// OWNER_CLASS
// Saved-file and buff adapters are doubles; no real player files are loaded.
public static class EntityBuffs {public const int Version=2;}
public class BuffValue {public void Read(BinaryReader r,int version){r.ReadInt32();}}
public class InternalPlayerId {public string CombinedString="fixture";}
public class ClientInfo {public int entityId=1;public PlayerDataFile latestPlayerData;public InternalPlayerId InternalId=new InternalPlayerId();}
public static class GameIO {public static string SaveRoot="fixture-save";public static string GetSaveGameDir(){return SaveRoot;}public static string GetPlayerDataDir(){return "fixture-only";}}
public class PlayerDataFile {
 public bool bLoaded;public MemoryStream buffData;public Slots bag;public ItemStack[] inventory;
 public static PlayerDataFile Loaded;
 public void Load(string directory,string owner){if(Loaded==null)return;bLoaded=Loaded.bLoaded;buffData=Loaded.buffData;bag=Loaded.bag;inventory=Loaded.inventory;}
}
public static class RebirthPlayerDataInventory {
 public static ItemStack[] ReadSlots(PlayerDataFile data,bool bag) {
  var items=bag?data?.bag?.Items:data?.inventory;
  return items==null?null:items.Select(s=>s?.Clone()).ToArray();
 }
}
// EVIDENCE_CLASS
// Character repository/save adapters are doubles, with phase-specific failure injection.
public class Origin {public string CreationId;}
public class RebirthWorldCharacterRecord {public string StablePlayerKey,StablePlayerId;public RebirthWorldSupportState Support;public Origin Origin=new Origin();public bool IsComplete=true;}
public class RebirthStablePlayerIdentity {public string StorageKey,CanonicalId;}
public static class RebirthWorldCharacterService {
 public static RebirthWorldCharacterRecord Record;
 public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return r!=null;}
 public static bool TryGetIdentity(EntityPlayer p,out RebirthStablePlayerIdentity i){i=new RebirthStablePlayerIdentity{StorageKey=Record?.StablePlayerKey,CanonicalId=Record?.StablePlayerId};return true;}
 public static void MarkDirty(RebirthWorldCharacterRecord r,string reason){}
}
public static class RebirthWorldCharacterRepository {
 public static bool IsServerAuthority=true;public static int Saves,FailAt;
  public static bool RefundWrite=true;public static XElement SavedRefund;public static Action RefundSaveHook;
 public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>ReferenceEquals(r,RebirthWorldCharacterService.Record);
 public static bool HasSavedRemoteResourceRefunds(RebirthStablePlayerIdentity i,string c,XElement image)=>image!=null&&SavedRefund!=null&&XNode.DeepEquals(image,SavedRefund);
 public static bool SaveIfDirty(RebirthStablePlayerIdentity i,string reason){
  bool saved=++Saves!=FailAt;
  if(reason=="remote-resource-refund-registration"){
   var image=RebirthWorldCharacterService.Record.Support.RemoteResourceRefundJournalImage;
   RefundSaveHook?.Invoke();if(saved&&RefundWrite)SavedRefund=new XElement(image);
  }return saved;
 }
}
// SERVER_CLASS
// RESERVATION_CLASS
// Cooking compatibility/profile double; actual Count/Take/planning/admission body below.
public static class RebirthCookingItemStats {public static bool Compatible(ItemValue a,ItemValue b){return a.Type==b.Type&&a.Payload==b.Payload;}public static bool FullIngredient(ItemValue value){return true;}}
// COOKING_INGREDIENTS_CLASS
public class XUiM_PlayerInventory {public EntityPlayerLocal localPlayer;}
public class PlayerUI {public WindowManager windowManager=new WindowManager();public XUi xui;public EntityPlayerLocal entityPlayer;}
public class DragWindow {public ItemStack CurrentStack=ItemStack.Empty.Clone();}
public class PlayerInventoryDouble {public void dispatchBackpackItemsChanged(){}public void dispatchToolbeltItemsChanged(){}public int Adds;public bool AddItem(ItemStack stack,bool sound){Adds++;return true;}}
public static class RebirthLiteratureService {public static bool HasMatchingInventoryItem(EntityPlayer player,int type,ushort seed){return false;}}
public static class RebirthSurvivorSupportUiFeedback {public static void Receive(bool result,string message){}}
public class XUi {public PlayerInventoryDouble PlayerInventory=new PlayerInventoryDouble();public bool IsUsingItemActionEntryUse;public DragWindow DragAndDropWindow=new DragWindow();public PlayerUI playerUI;}
public class XUiC_ItemStack {public enum StackLocationTypes {Backpack,ToolBelt}public bool IsDirty;public XUi xui;}
public class XUiC_Creative2Stack : XUiC_ItemStack {}
public class BaseItemActionEntry {public XUiC_ItemStack ItemController;}
public static class RebirthStationGridIngredients {public static bool IsSameStackSnapshot(ItemStack a,ItemStack b){return a!=null&&b!=null&&a.count==b.count&&RebirthNativeItemCodec.Encode(a.itemValue)==RebirthNativeItemCodec.Encode(b.itemValue);}}
public class RebirthAudiobookDefinition {public string SourceLiteratureId;}
public class RebirthLiteratureDefinition {public string Kind;}
public static class RebirthProgressionRuntimeConfig {public static string PurposeKind;public static bool PurposeAudio;
public static bool TryGetAudiobook(string id,out RebirthAudiobookDefinition value){value=PurposeAudio&&id=="pack"?new RebirthAudiobookDefinition{SourceLiteratureId="sourceBook"}:null;return value!=null;}
public static bool TryGetLiterature(string id,out RebirthLiteratureDefinition value){value=!string.IsNullOrEmpty(id)&&PurposeKind!=null?new RebirthLiteratureDefinition{Kind=PurposeKind}:null;return value!=null;}
}
// UI adapters compile the real controller; they do not simulate native rendering or click dispatch.
public delegate void XUiEvent_OnPressEventHandler(XUiController sender,int button);
public class Vector2i{public int x,y;public Vector2i(int a,int b){x=a;y=b;}} public class TransformDouble{public UnityEngine.Vector3 localScale;} public class XUiView {public bool Enabled;public bool IsVisible;public string ToolTip;public Vector2i Position,Size;public TransformDouble UiTransform;public void TryUpdatePosition(){}}
public class XUiV_Label:XUiView {public string Text;}
public class XUiV_Sprite:XUiView {public string SpriteName;}
public class WindowManager {public int Closes;public void Close(string name){Closes++;}}
public enum EDragType{DragStart,DragEnd} public static class RebirthInventoryDropHitTest{public static bool TryGetLocalPointer(XUiController c,out Vector2i p){p=new Vector2i(120,-80);return c!=null;}public static XUiController Target;public static bool IsOver(XUiController c)=>c!=null&&ReferenceEquals(c,Target);} public class XUiController {public event Action<XUiController,EDragType,UnityEngine.Vector2> OnDrag;public int DragBindings=>OnDrag?.GetInvocationList().Length??0;public void Drag(EDragType type){OnDrag?.Invoke(this,type,new UnityEngine.Vector2());}public virtual void Cleanup(){}public virtual void OnClose(){}
 public XUi xui;public bool IsOpen;public XUiView ViewComponent;
 public event XUiEvent_OnPressEventHandler OnPress;
 public virtual void Init(){}public virtual void OnOpen(){}public virtual void Update(float dt){}
 public readonly Dictionary<string,XUiController> Controls=new Dictionary<string,XUiController>();
 public void Press(){if(OnPress!=null)OnPress(this,0);}
 public XUiController GetChildById(string id){return Controls.TryGetValue(id,out var c)?c:null;}
}
public class XUiC_SimpleButton:XUiController {public event XUiEvent_OnPressEventHandler OnPressed;public void Click(){if(OnPressed!=null)OnPressed(this,0);}}
// STUDY_INTENT_CLASS
public static class RebirthScreenLayout{public static void GetScreenBounds(XUi ui,out Vector2i p,out Vector2i s){p=new Vector2i(0,0);s=new Vector2i(1920,1080);}} public static class XUiC_RebirthCraftingItemContext{public static string ResolveDescription(ItemStack s,XUi ui)=>"description-double";} namespace UnityEngine {public struct Vector2{}public enum KeyCode{LeftShift,RightShift}public static class Input{public static bool Shift;public static bool GetKey(KeyCode k)=>Shift;}public struct Vector3{public Vector3(float a,float b,float c){}}public static class Mathf{public static int RoundToInt(float f)=>(int)Math.Round(f);}public static class Time {public static float unscaledTime;}}
public static class ItemActionStudyLiteratureRebirth {public static int Dispatches;internal static void Dispatch(EntityPlayer p,ItemValue item){Dispatches++;}}
// LIBRARY_UI_CLASS
public static class Check {
// ACTION_PREFIX
// PARTIAL_STACK_PREFIX
// BAG_ADD_PREFIX
// BELT_PLACEMENT_PREFIX
// BELT_ADD_PREFIX
// BELT_PARTIAL_PREFIX
// MATERIAL_PREFIX
// INSTANT_METHODS
 static int Dispatches;static void Dispatch(EntityPlayer player,ItemValue value){Dispatches++;}
// CURSOR_PREFIX
 static void A(bool b,string n){if(!b)throw new Exception(n);}
 static ItemStack S(int count,string data="receipt"){return new ItemStack{count=count,itemValue=new ItemValue{Type=1,Payload=data}};}
 static RebirthBackpackLibraryOwnerResult ApplyReserved(EntityPlayerLocal player,Guid creation,RebirthBackpackLibraryReceipt offer){
  player.world.Player=player;GameManager.Instance=new GameManager{World=player.world};
  RebirthBackpackLibraryReservation.TryAcquire(player,creation,offer);
  return RebirthBackpackLibraryOwnerTransfer.Apply(player,creation,offer);
 }
 static byte[] Evidence(string key,float value,bool duplicate=false){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write((byte)2);w.Write((ushort)1);w.Write(123);w.Write((ushort)(duplicate?2:1));w.Write(key);w.Write(value);if(duplicate){w.Write(key);w.Write(value);}w.Flush();return m.ToArray();}}
 public static void Main(){
 var outcomeWorld=new World();var outcomeUser=new InternalPlayerId{CombinedString="retained-owner"};
 RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();
 object retainedHandle;RemoteResourceTransactionOutcome retainedSnapshot;
 A(!RemoteResourceTransactionOutcomeJournal.TryGetRetained(outcomeWorld,outcomeUser,1,1,RemoteResourceClientOperation.Craft,out retainedSnapshot,out retainedHandle),"retained lookup never creates source outcome");
 RemoteResourceTransactionOutcomeJournal.Resolve(outcomeWorld,new EntityPlayer(),outcomeUser,1,1,RemoteResourceClientOperation.Craft,new[]{S(2)},false);
 A(RemoteResourceTransactionOutcomeJournal.TryGetRetained(outcomeWorld,outcomeUser,1,1,RemoteResourceClientOperation.Craft,out retainedSnapshot,out retainedHandle),"authoritative retained removal snapshot available");
 retainedSnapshot.Removed[0].count=1;
 A(RemoteResourceTransactionOutcomeJournal.TryGetRetained(outcomeWorld,outcomeUser,1,1,RemoteResourceClientOperation.Craft,out retainedSnapshot,out var secondHandle)&&retainedSnapshot.Removed[0].count==2,"returned snapshot cannot mutate retained source outcome");
 A(RemoteResourceTransactionOutcomeJournal.MatchesRetained(outcomeWorld,outcomeUser,1,1,RemoteResourceClientOperation.Craft,retainedHandle),"opaque retained handle matches exact identity");
 A(!RemoteResourceTransactionOutcomeJournal.MatchesRetained(outcomeWorld,new InternalPlayerId{CombinedString="other-owner"},1,1,RemoteResourceClientOperation.Craft,retainedHandle)&&!RemoteResourceTransactionOutcomeJournal.MatchesRetained(outcomeWorld,outcomeUser,1,2,RemoteResourceClientOperation.Craft,retainedHandle)&&!RemoteResourceTransactionOutcomeJournal.MatchesRetained(outcomeWorld,outcomeUser,1,1,RemoteResourceClientOperation.ItemRepair,retainedHandle),"retained handle refuses changed owner request and operation");
 A(!RemoteResourceTransactionOutcomeJournal.TryGetRetained(new World(),outcomeUser,1,1,RemoteResourceClientOperation.Craft,out retainedSnapshot,out secondHandle)&&RemoteResourceTransactionOutcomeJournal.MatchesRetained(outcomeWorld,outcomeUser,1,1,RemoteResourceClientOperation.Craft,retainedHandle),"foreign world lookup cannot reset original journal");
 A(!RemoteResourceTransactionOutcomeJournal.MatchesRetained(outcomeWorld,outcomeUser,1,1,RemoteResourceClientOperation.Craft,new object()),"invented retained handle refused");
 RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();
 RemoteResourceTransactionOutcomeJournal.Resolve(outcomeWorld,new EntityPlayer(),outcomeUser,1,1,RemoteResourceClientOperation.Craft,new[]{S(2)},false);
 A(!RemoteResourceTransactionOutcomeJournal.MatchesRetained(outcomeWorld,outcomeUser,1,1,RemoteResourceClientOperation.Craft,retainedHandle),"reset and identical re-registration expire previous handle");
 RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();
 A(!RemoteResourceTransactionOutcomeJournal.TryGetRetained(outcomeWorld,outcomeUser,0,1,RemoteResourceClientOperation.Craft,out retainedSnapshot,out retainedHandle)&&!RemoteResourceTransactionOutcomeJournal.TryGetRetained(outcomeWorld,null,1,1,RemoteResourceClientOperation.Craft,out retainedSnapshot,out retainedHandle),"retained lookup refuses missing identity and zero epoch");
 RemoteResourceTransactionOutcomeJournal.Resolve(outcomeWorld,new EntityPlayer(),outcomeUser,2,1,RemoteResourceClientOperation.Craft,new[]{S(2)},false);
 A(RemoteResourceTransactionOutcomeJournal.TryGetRetained(outcomeWorld,outcomeUser,2,1,RemoteResourceClientOperation.Craft,out retainedSnapshot,out retainedHandle),"eviction candidate retained");
 for(ulong outcomeRequest=2;outcomeRequest<=2049;outcomeRequest++)RemoteResourceTransactionOutcomeJournal.Resolve(outcomeWorld,new EntityPlayer(),outcomeUser,2,outcomeRequest,RemoteResourceClientOperation.Craft,new[]{S(2)},false);
 A(!RemoteResourceTransactionOutcomeJournal.MatchesRetained(outcomeWorld,outcomeUser,2,1,RemoteResourceClientOperation.Craft,retainedHandle),"journal eviction expires retained handle");
 RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();
 var replacementWorld=new World();
 RemoteResourceTransactions.ConsumptionHook=()=>{RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();RemoteResourceTransactionOutcomeJournal.Resolve(replacementWorld,new EntityPlayer(),outcomeUser,3,2,RemoteResourceClientOperation.Craft,new[]{S(1)},false);};
 var staleOutcome=RemoteResourceTransactionOutcomeJournal.Resolve(outcomeWorld,new EntityPlayer(),outcomeUser,3,1,RemoteResourceClientOperation.Craft,new[]{S(2)},false);
 A(staleOutcome.State==RemoteResourceTransactionOutcomeState.Unknown&&staleOutcome.Removed.Count==0,"old world completion cannot publish actionable removal outcome");
 A(RemoteResourceTransactionOutcomeJournal.TryGetRetained(replacementWorld,outcomeUser,3,2,RemoteResourceClientOperation.Craft,out retainedSnapshot,out retainedHandle)&&retainedSnapshot.Removed[0].count==1,"old completion preserves replacement world journal");
 RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();
 RemoteResourceTransactions.ConsumptionHook=()=>{RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();RemoteResourceTransactionOutcomeJournal.Resolve(outcomeWorld,new EntityPlayer(),outcomeUser,4,1,RemoteResourceClientOperation.Craft,new[]{S(1)},false);};
 staleOutcome=RemoteResourceTransactionOutcomeJournal.Resolve(outcomeWorld,new EntityPlayer(),outcomeUser,4,1,RemoteResourceClientOperation.Craft,new[]{S(2)},false);
 A(staleOutcome.State==RemoteResourceTransactionOutcomeState.Unknown,"reset same world and key cannot reuse previous request claim");
 A(RemoteResourceTransactionOutcomeJournal.TryGetRetained(outcomeWorld,outcomeUser,4,1,RemoteResourceClientOperation.Craft,out retainedSnapshot,out retainedHandle)&&retainedSnapshot.Removed[0].count==1,"replacement exact request outcome survives stale completion");
 RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();
 Console.WriteLine("PASS 4 actual journal reset/publication race assertions; reentrant consumption hook doubles unload timing. Stale removals still need durable source reconciliation."); Console.WriteLine("PASS 11 retained outcome assertions: detached snapshot, exact identity, read-only foreign-world refusal, invented handle, reset, malformed scope and eviction; native identity/source-consumption adapters doubled. No refund delivery or source-removal durability proof."); var producerCreation=Guid.NewGuid().ToString("N");var producerPlayer=new EntityPlayer();producerPlayer.world.Player=producerPlayer;
 GameManager.Instance=new GameManager{World=producerPlayer.world};
 var producerSender=new ClientInfo{entityId=producerPlayer.entityId,InternalId=outcomeUser};
 RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord{StablePlayerKey=new string('b',64),StablePlayerId=outcomeUser.CombinedString,Origin=new Origin{CreationId=producerCreation},Support=new RebirthWorldSupportState()};
 RemoteResourceTransactionOutcomeJournal.Resolve(producerPlayer.world,producerPlayer,outcomeUser,5,1,RemoteResourceClientOperation.Craft,new[]{S(2)},false);
 RemoteResourceRefundRecord preparedRefund;bool preparedPending;int unusedChecks=0;
 A(!RemoteResourceRefundProducer.TryPrepare(producerPlayer,producerSender,producerCreation,5,1,RemoteResourceClientOperation.Craft,r=>false,out preparedRefund,out preparedPending)&&RebirthWorldCharacterService.Record.Support.RemoteResourceRefundJournalImage==null,"producer refuses missing unused proof without registration");
 A(RemoteResourceRefundProducer.TryPrepare(producerPlayer,producerSender,producerCreation,5,1,RemoteResourceClientOperation.Craft,r=>{unusedChecks++;r.TryGetStacks(out var stacks);return stacks[0].count==2;},out preparedRefund,out preparedPending)&&preparedPending&&preparedRefund!=null&&unusedChecks==1,"producer persists full authoritative removal under owner identity");
 int producerSaves=RebirthWorldCharacterRepository.Saves;
 A(RemoteResourceRefundProducer.TryPrepare(producerPlayer,producerSender,producerCreation,5,1,RemoteResourceClientOperation.Craft,r=>true,out preparedRefund,out preparedPending)&&preparedPending&&RebirthWorldCharacterRepository.Saves==producerSaves,"producer retry retains saved exact registration without duplicate write");
 producerSender.InternalId=new InternalPlayerId{CombinedString="spoofed-owner"};
 A(!RemoteResourceRefundProducer.TryPrepare(producerPlayer,producerSender,producerCreation,5,1,RemoteResourceClientOperation.Craft,r=>{throw new Exception("must not verify spoofed owner");},out preparedRefund,out preparedPending),"producer rejects mismatched authenticated owner");producerSender.InternalId=outcomeUser;
 RemoteResourceTransactionOutcomeJournal.Resolve(producerPlayer.world,producerPlayer,outcomeUser,5,2,RemoteResourceClientOperation.Craft,new[]{S(2)},false);
 A(!RemoteResourceRefundProducer.TryPrepare(producerPlayer,producerSender,producerCreation,5,2,RemoteResourceClientOperation.Craft,r=>{RemoteResourceTransactionOutcomeJournal.ResetForWorldUnload();return true;},out preparedRefund,out preparedPending)&&RebirthWorldCharacterRepository.Saves==producerSaves,"producer rechecks retained removal after proof callback before write");
 RebirthWorldCharacterRepository.Saves=0;RebirthWorldCharacterRepository.SavedRefund=null;RebirthWorldCharacterService.Record=null;
 Console.WriteLine("PASS 5 actual refund producer assertions: proof refusal, exact retained grant, durable checkpoint retry, owner mismatch and callback reset; native authentication/world identity/save/unused-proof adapters doubled. No live delivery."); var refundWorld=new string('a',64);var refundOwner=new string('b',64);var refundCreation=Guid.NewGuid().ToString("N");
 var refundStack=S(2);refundStack.itemValue.Payload="exact-refund";refundStack.itemValue.Metadata["custom"]="preserved";
 A(RemoteResourceRefundRecord.TryCreate(refundWorld,refundOwner,refundCreation,7,9,RemoteResourceClientOperation.Craft,new[]{refundStack},out var refundRecord),"refund record creates exact unused grant");
 refundStack.count=1;refundStack.itemValue.Metadata["custom"]="changed";refundRecord.TryGetStacks(out var refundItems);
 A(refundItems[0].count==2&&refundItems[0].itemValue.Metadata["custom"]=="preserved","refund record detached source count and metadata");
 var refundXml=refundRecord.ToXml();refundXml.Element("stack").SetAttributeValue("count",1);refundRecord.TryGetStacks(out refundItems);
 A(refundItems[0].count==2,"refund XML copy cannot change retained image");
 A(RemoteResourceRefundRecord.TryRead(refundRecord.ToXml(),refundWorld,refundOwner,refundCreation,out var refundReload)&&refundReload.SessionEpoch==7&&refundReload.RequestId==9,"refund record reload binds exact scope/request");
 A(!RemoteResourceRefundRecord.TryRead(refundRecord.ToXml(),new string('c',64),refundOwner,refundCreation,out _)&&
   !RemoteResourceRefundRecord.TryRead(refundRecord.ToXml(),refundWorld,new string('c',64),refundCreation,out _)&&
   !RemoteResourceRefundRecord.TryRead(refundRecord.ToXml(),refundWorld,refundOwner,Guid.NewGuid().ToString("N"),out _),"refund record rejects world owner and character substitution");
 refundXml=refundRecord.ToXml();refundXml.SetAttributeValue("extra",1);A(!RemoteResourceRefundRecord.TryRead(refundXml,refundWorld,refundOwner,refundCreation,out _),"refund record rejects unexpected shape");
 refundXml=refundRecord.ToXml();refundXml.Element("stack").SetAttributeValue("count",6);A(!RemoteResourceRefundRecord.TryRead(refundXml,refundWorld,refundOwner,refundCreation,out _),"refund record rejects native stack overflow");
 refundXml=refundRecord.ToXml();refundXml.Element("stack").SetAttributeValue("data","invalid");A(!RemoteResourceRefundRecord.TryRead(refundXml,refundWorld,refundOwner,refundCreation,out _),"refund record rejects corrupt native item");
 A(!RemoteResourceRefundRecord.TryCreate(refundWorld,refundOwner,refundCreation,0,9,RemoteResourceClientOperation.Craft,new[]{S(1)},out _)&&
   !RemoteResourceRefundRecord.TryCreate(refundWorld,refundOwner,refundCreation,7,0,RemoteResourceClientOperation.Craft,new[]{S(1)},out _)&&
   !RemoteResourceRefundRecord.TryCreate(refundWorld,refundOwner,refundCreation,7,9,(RemoteResourceClientOperation)99,new[]{S(1)},out _),"refund record rejects invalid epoch request and operation");
 A(!RemoteResourceRefundRecord.TryCreate(refundWorld,refundOwner,refundCreation,7,9,RemoteResourceClientOperation.Craft,new ItemStack[0],out _)&&
   !RemoteResourceRefundRecord.TryCreate(refundWorld,refundOwner,refundCreation,7,9,RemoteResourceClientOperation.Craft,Enumerable.Repeat(S(1),65).ToArray(),out _),"refund record enforces stack-count bounds");
 Console.WriteLine("PASS 10 exact refund record assertions; native item codec doubled, no persistence or delivery integration.");
 A(RemoteResourceRefundJournal.TryCreate(refundWorld,refundOwner,refundCreation,out var refundJournal),"refund journal exact scope");
 int refundSaves=0;XElement savedRefund=null;
 Func<XElement,bool> refundSave=x=>{refundSaves++;savedRefund=new XElement(x);return true;};
 A(!refundJournal.TryRegister(refundRecord,x=>false,()=>true)&&refundJournal.Pending().Length==0,"failed refund registration publishes nothing");
 A(!refundJournal.TryRegister(refundRecord,x=>{throw new IOException("save");},()=>true)&&refundJournal.Pending().Length==0,"throwing refund save publishes nothing");
 A(refundJournal.TryRegister(refundRecord,refundSave,()=>true)&&refundJournal.Pending().Length==1&&refundSaves==1,"refund register saves before pending publication");
 A(refundJournal.TryRegister(refundRecord,refundSave,()=>true)&&refundSaves==1,"duplicate refund registration does not save or duplicate");
 RemoteResourceRefundRecord.TryCreate(refundWorld,refundOwner,refundCreation,7,9,RemoteResourceClientOperation.Craft,new[]{S(1)},out var conflictingRefund);
 A(!refundJournal.TryRegister(conflictingRefund,refundSave,()=>true)&&refundJournal.Pending().Length==1,"same refund key with changed items refused");
 A(!refundJournal.TrySettle(refundRecord,r=>false,refundSave,()=>true)&&refundJournal.Pending().Length==1,"refund settlement without owner evidence remains pending");
 A(!refundJournal.TrySettle(refundRecord,r=>true,x=>false,()=>true)&&refundJournal.Pending().Length==1,"failed refund terminal save remains pending");
 A(RemoteResourceRefundJournal.TryRead(savedRefund,refundWorld,refundOwner,refundCreation,out var reloadedRefund)&&reloadedRefund.Pending().Length==1,"pending refund reload retains exact item record");
 A(refundJournal.TrySettle(refundRecord,r=>true,refundSave,()=>true)&&refundJournal.Pending().Length==0,"verified refund settlement saves terminal identity");
 A(refundJournal.TryRegister(refundRecord,refundSave,()=>true)&&refundJournal.Pending().Length==0&&refundSaves==2,"settled duplicate does not re-credit refund");
 A(RemoteResourceRefundJournal.TryRead(savedRefund,refundWorld,refundOwner,refundCreation,out reloadedRefund)&&
   reloadedRefund.TryRegister(refundRecord,refundSave,()=>true)&&reloadedRefund.Pending().Length==0,"settled reload preserves deduplication");
 var duplicateRefundXml=refundJournal.ToXml();duplicateRefundXml.Add(new XElement(duplicateRefundXml.Element("entry")));
 A(!RemoteResourceRefundJournal.TryRead(duplicateRefundXml,refundWorld,refundOwner,refundCreation,out _),"duplicate serialized refund identity refused");
 RemoteResourceRefundRecord.TryCreate(refundWorld,refundOwner,refundCreation,7,10,RemoteResourceClientOperation.Craft,new[]{S(1)},out var otherRefund);
 bool refundCurrent=true;
 A(!refundJournal.TryRegister(otherRefund,x=>{refundCurrent=false;return true;},()=>refundCurrent)&&refundJournal.Pending().Length==0,"changed context after refund save publishes no old-session pending item");
 bool nestedRefundAccepted=true;
 A(refundJournal.TryRegister(otherRefund,x=>{nestedRefundAccepted=refundJournal.TryRegister(otherRefund,refundSave,()=>true);return true;},()=>true)&&!nestedRefundAccepted&&refundJournal.Pending().Length==1,"refund journal refuses reentrant register");
 RemoteResourceRefundJournal.TryCreate(refundWorld,refundOwner,refundCreation,out var fullRefundJournal);
 for(ulong refundId=1;refundId<=128;refundId++){
  RemoteResourceRefundRecord.TryCreate(refundWorld,refundOwner,refundCreation,20,refundId,RemoteResourceClientOperation.Craft,new[]{S(1)},out var capacityRefund);
  A(fullRefundJournal.TryRegister(capacityRefund,x=>true,()=>true),"refund capacity setup");
 }
 RemoteResourceRefundRecord.TryCreate(refundWorld,refundOwner,refundCreation,20,129,RemoteResourceClientOperation.Craft,new[]{S(1)},out var overflowRefund);
 bool overflowRefundSaved=false;
 A(!fullRefundJournal.TryRegister(overflowRefund,x=>{overflowRefundSaved=true;return true;},()=>true)&&
   !overflowRefundSaved&&fullRefundJournal.Pending().Length==128,"full refund journal refuses before save without evicting pending items");
 Console.WriteLine("PASS refund journal saturation retains all 128 pending identities without eviction or overflow save.");
 var refundSupport=new XElement("support",refundJournal.ToXml());
 A(RemoteResourceRefundSupportPersistence.TryRead(new XElement("support"),refundOwner,refundCreation,out var oldRefundSupport)&&oldRefundSupport==null,"legacy support without refund remains compatible");
 A(RemoteResourceRefundSupportPersistence.TryRead(refundSupport,refundOwner,refundCreation,out var restoredRefundSupport)&&
   XNode.DeepEquals(restoredRefundSupport,refundJournal.ToXml()),"refund support preserves pending and settled entries");
 restoredRefundSupport.Element("entry").SetAttributeValue("settled",false);
 A(!XNode.DeepEquals(restoredRefundSupport,refundSupport.Element("remoteResourceRefunds")),"refund support output detached from saved image");
 A(!RemoteResourceRefundSupportPersistence.TryRead(refundSupport,new string('c',64),refundCreation,out _)&&
   !RemoteResourceRefundSupportPersistence.TryRead(refundSupport,refundOwner,Guid.NewGuid().ToString("N"),out _),"refund support refuses owner and character mismatch");
 var duplicateSupport=new XElement(refundSupport);duplicateSupport.Add(refundJournal.ToXml());
 A(!RemoteResourceRefundSupportPersistence.TryRead(duplicateSupport,refundOwner,refundCreation,out _),"refund support refuses duplicate custody section");
 A(RemoteResourceRefundSupportPersistence.Write(null,refundOwner,refundCreation)==null&&
   XNode.DeepEquals(RemoteResourceRefundSupportPersistence.Write(refundJournal.ToXml(),refundOwner,refundCreation),refundJournal.ToXml()),"refund support writer preserves optional and exact images");
 bool refundWriteRejected=false;try{RemoteResourceRefundSupportPersistence.Write(refundJournal.ToXml(),new string('c',64),refundCreation);}catch(InvalidDataException){refundWriteRejected=true;}
 A(refundWriteRejected,"refund support writer refuses wrong owner rather than discarding items");
 A(RepositoryCustodyFixture.HasPendingItemCustody(new XDocument(new XElement("rebirthWorldCharacter",refundSupport))),"final pending refund section prevents older backup replacement");
 var settledRefundSupport=new XElement("support",new XElement("remoteResourceRefunds",new XAttribute("version",1)));
 A(RepositoryCustodyFixture.HasPendingItemCustody(new XDocument(new XElement("rebirthWorldCharacter",settledRefundSupport))),"even malformed or settled refund journal protects retained identity from backup rollback");
 A(!RepositoryCustodyFixture.HasPendingItemCustody(new XDocument(new XElement("rebirthWorldCharacter",new XElement("support"))))&&
   !RepositoryCustodyFixture.HasPendingItemCustody(null),"legacy empty support has no new custody marker");
 Console.WriteLine("PASS 3 actual repository refund custody detection assertions; file recovery itself not executed.");
 var refundIdentity=new RefundIdentityDouble{StorageKey=refundOwner};
 RepositoryRefundWitnessFixture.Record=new RebirthWorldCharacterRecord{Origin=new Origin{CreationId=refundCreation},Support=new RebirthWorldSupportState{RemoteResourceRefundJournalImage=refundJournal.ToXml()}};
 A(RepositoryRefundWitnessFixture.HasSavedRemoteResourceRefunds(refundIdentity,refundCreation,refundJournal.ToXml()),"refund witness accepts exact validated final image");
 RepositoryRefundWitnessFixture.Loaded=false;
 A(!RepositoryRefundWitnessFixture.HasSavedRemoteResourceRefunds(refundIdentity,refundCreation,refundJournal.ToXml()),"refund witness refuses absent final despite cache image");
 RepositoryRefundWitnessFixture.Loaded=true;RepositoryRefundWitnessFixture.Migrated=true;
 A(!RepositoryRefundWitnessFixture.HasSavedRemoteResourceRefunds(refundIdentity,refundCreation,refundJournal.ToXml()),"refund witness refuses migrated final image");
 RepositoryRefundWitnessFixture.Migrated=false;
 A(!RepositoryRefundWitnessFixture.HasSavedRemoteResourceRefunds(new RefundIdentityDouble{StorageKey=new string('c',64)},refundCreation,refundJournal.ToXml()),"refund witness refuses wrong enclosing owner");
 var witnessChanged=refundJournal.ToXml();witnessChanged.Elements("entry").Last().SetAttributeValue("settled",true);
 A(!RepositoryRefundWitnessFixture.HasSavedRemoteResourceRefunds(refundIdentity,refundCreation,witnessChanged),"refund witness refuses different saved settlement stage");
 RepositoryRefundWitnessFixture.Record.Origin.CreationId=Guid.NewGuid().ToString("N");
 A(!RepositoryRefundWitnessFixture.HasSavedRemoteResourceRefunds(refundIdentity,refundCreation,refundJournal.ToXml()),"refund witness refuses final character replacement");
 RepositoryRefundWitnessFixture.Record.Origin.CreationId=refundCreation;RepositoryRefundWitnessFixture.ChangedRoot=true;RepositoryRefundWitnessFixture.PathReads=0;
 A(!RepositoryRefundWitnessFixture.HasSavedRemoteResourceRefunds(refundIdentity,refundCreation,refundJournal.ToXml()),"refund witness refuses world save-root replacement during readback");
 RepositoryRefundWitnessFixture.ChangedRoot=false;
 Console.WriteLine("PASS 7 actual saved refund witness assertions; native final-file loader, owner identity and save-root resolution doubled.");
 int beforeRefundRegistrationSaves=RebirthWorldCharacterRepository.Saves;int beforeRefundRegistrationFailAt=RebirthWorldCharacterRepository.FailAt;
 var beforeRefundRegistrationRecord=RebirthWorldCharacterService.Record;
 var registrationRecord=new RebirthWorldCharacterRecord{StablePlayerKey=refundOwner,StablePlayerId="refund-owner",Origin=new Origin{CreationId=refundCreation},Support=new RebirthWorldSupportState()};
 var registrationIdentity=new RebirthStablePlayerIdentity{StorageKey=refundOwner,CanonicalId="refund-owner"};
 RebirthWorldCharacterService.Record=registrationRecord;RebirthWorldCharacterRepository.SavedRefund=null;
 RebirthWorldCharacterRepository.FailAt=RebirthWorldCharacterRepository.Saves+1;
 A(!RemoteResourceRefundSaveCheckpoint.TryPersist(registrationRecord,registrationIdentity,refundWorld,refundRecord,()=>true,out _)&&
   registrationRecord.Support.RemoteResourceRefundJournalImage!=null,"refund failed save retains candidate custody without readiness");
 RebirthWorldCharacterRepository.FailAt=0;
 A(RemoteResourceRefundSaveCheckpoint.TryPersist(registrationRecord,registrationIdentity,refundWorld,refundRecord,()=>true,out var registrationPending)&&registrationPending,"refund exact retry persists retained pending candidate");
 int registrationSaves=RebirthWorldCharacterRepository.Saves;
 A(RemoteResourceRefundSaveCheckpoint.TryPersist(registrationRecord,registrationIdentity,refundWorld,refundRecord,()=>true,out registrationPending)&&registrationPending&&RebirthWorldCharacterRepository.Saves==registrationSaves,"refund durable duplicate does not rewrite or add credit");
 A(!RemoteResourceRefundSaveCheckpoint.TryPersist(registrationRecord,new RebirthStablePlayerIdentity{StorageKey=refundOwner,CanonicalId="other"},refundWorld,refundRecord,()=>true,out _),"refund registration refuses canonical owner mismatch");
 A(!RemoteResourceRefundSaveCheckpoint.TryPersist(registrationRecord,registrationIdentity,new string('c',64),refundRecord,()=>true,out _),"refund registration refuses world binding mismatch");
 A(!RemoteResourceRefundSaveCheckpoint.TryPersist(registrationRecord,registrationIdentity,refundWorld,refundRecord,()=>false,out _),"refund registration refuses retired authenticated context");
 RebirthWorldCharacterRepository.RefundWrite=false;
 A(!RemoteResourceRefundSaveCheckpoint.TryPersist(registrationRecord,registrationIdentity,refundWorld,otherRefund,()=>true,out _)&&registrationRecord.Support.RemoteResourceRefundJournalImage.Elements("entry").Count()==2,"refund save return without final witness retains uncertain candidate");
 RebirthWorldCharacterRepository.RefundWrite=true;
 bool nestedRegistration=true;
 RebirthWorldCharacterRepository.RefundSaveHook=()=>nestedRegistration=RemoteResourceRefundSaveCheckpoint.TryPersist(registrationRecord,registrationIdentity,refundWorld,otherRefund,()=>true,out _);
 A(RemoteResourceRefundSaveCheckpoint.TryPersist(registrationRecord,registrationIdentity,refundWorld,otherRefund,()=>true,out registrationPending)&&registrationPending&&!nestedRegistration,"refund checkpoint rejects save callback reentry and retries uncertain record");
 RebirthWorldCharacterRepository.RefundSaveHook=null;RebirthWorldCharacterRepository.SavedRefund=null;
 RebirthWorldCharacterRepository.RefundSaveHook=()=>RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord();
 A(!RemoteResourceRefundSaveCheckpoint.TryPersist(registrationRecord,registrationIdentity,refundWorld,otherRefund,()=>true,out _),"refund checkpoint refuses owner record replacement during save");
 RebirthWorldCharacterRepository.RefundSaveHook=null;RebirthWorldCharacterService.Record=beforeRefundRegistrationRecord;RebirthWorldCharacterRepository.SavedRefund=null;RebirthWorldCharacterRepository.Saves=beforeRefundRegistrationSaves;RebirthWorldCharacterRepository.FailAt=beforeRefundRegistrationFailAt;
 var changedRegistration=new RebirthWorldCharacterRecord{StablePlayerKey=refundOwner,StablePlayerId="refund-owner",Origin=new Origin{CreationId=refundCreation},Support=new RebirthWorldSupportState()};
 RebirthWorldCharacterService.Record=changedRegistration;
 RebirthWorldCharacterRepository.RefundSaveHook=()=>changedRegistration.Support.RemoteResourceRefundJournalImage.Element("entry").Element("remoteResourceRefund").Element("stack").SetAttributeValue("count",3);
 A(!RemoteResourceRefundSaveCheckpoint.TryPersist(changedRegistration,registrationIdentity,refundWorld,refundRecord,()=>true,out _),"refund checkpoint refuses mutated staged contents even if saved witness matches changed image");
 RebirthWorldCharacterRepository.RefundSaveHook=null;RebirthWorldCharacterService.Record=beforeRefundRegistrationRecord;RebirthWorldCharacterRepository.SavedRefund=null;RebirthWorldCharacterRepository.Saves=beforeRefundRegistrationSaves;RebirthWorldCharacterRepository.FailAt=beforeRefundRegistrationFailAt;
 Console.WriteLine("PASS refund staged image integrity during native save callback; repository adapter doubled.");
 Console.WriteLine("PASS 9 refund checkpoint assertions: failed/uncertain persistence, durable duplicate, identity/world/context, reentry and replaced record; repository save/readback/authentication adapters doubled.");
 Console.WriteLine("PASS 7 refund support persistence assertions: legacy compatibility, exact detached image, owner/character, duplicate section, optional write and invalid-write refusal; no native repository I/O.");
 Console.WriteLine("PASS 15 refund journal assertions: save failure, duplicate/conflict, retained settlement identity, reload, context change and reentrancy; save and owner-proof callbacks doubled.");
 var grantPlayer=new EntityPlayerLocal{bag=new Bag{Items=new[]{S(2)}},inventory=new Slots{Items=new[]{ItemStack.Empty.Clone()}}};
 var grantRequirements=new List<ItemStack>{S(3)};var grantRemoved=new List<ItemStack>();
 RemoteResourceClientGrantContext.Begin(new List<ItemStack>{S(1)});RebirthGearOwnerReservation.Held=true;
 A(RemoteResourceClientTransactionCoordinator.CountConsumableLocal(grantPlayer,S(1).itemValue)==0,"gear custody excludes local crafting count");
 A(!RemoteResourceClientGrantContext.CanCover(grantPlayer,S(1))&&!RemoteResourceClientGrantContext.HasItems(grantPlayer,grantRequirements,1),"gear custody refuses grant coverage even for remote-only requirement");
 A(!RemoteResourceClientGrantContext.ConsumeLocalRemainder(grantPlayer,grantRequirements,1,grantRemoved)&&grantPlayer.bag.Items[0].count==2&&
   RemoteResourceClientGrantContext.GetCount(S(1).itemValue)==1&&!RemoteResourceClientGrantContext.Consumed&&grantRemoved.Count==0,"gear custody refuses grant spending without changing inventory or grant");
 RebirthGearOwnerReservation.Held=false;
 A(!RemoteResourceClientGrantContext.ConsumeLocalRemainder(grantPlayer,grantRequirements,0,grantRemoved)&&grantPlayer.bag.Items[0].count==2,"invalid grant multiplier refuses spending");
 A(RemoteResourceClientGrantContext.HasItems(grantPlayer,grantRequirements,1)&&RemoteResourceClientGrantContext.ConsumeLocalRemainder(grantPlayer,grantRequirements,1,grantRemoved)&&
   grantPlayer.bag.Items[0].IsEmpty()&&RemoteResourceClientGrantContext.Consumed&&grantRemoved.Sum(i=>i.count)==3,"unheld remote grant and local remainder consume normally");
 RemoteResourceClientGrantContext.End();
 Console.WriteLine("PASS actual remote grant custody: local count, remote coverage, no held consumption, multiplier refusal and ordinary settlement; native inventory/services doubled.");
 var mergeSource=S(3);var mergeCells=new[]{S(4),ItemStack.Empty.Clone()};
 A(RebirthBackpackSectionDestination.TryFind(mergeSource,2,i=>mergeCells[i],null,out var mergeDestination,out var mergeQuantity)&&mergeDestination==0&&mergeQuantity==1&&mergeSource.count==3&&mergeCells[0].count==4,"destination chooses exact partial merge without mutation");
 A(RebirthBackpackSectionDestination.TryFind(mergeSource,2,i=>mergeCells[i],i=>i==0,out mergeDestination,out mergeQuantity)&&mergeDestination==1&&mergeQuantity==3,"locked merge skipped for empty slot");
 mergeCells[0].itemValue.Metadata["different"]="identity";
 A(RebirthBackpackSectionDestination.TryFind(mergeSource,2,i=>mergeCells[i],null,out mergeDestination,out mergeQuantity)&&mergeDestination==1,"different metadata cannot merge");
 A(!RebirthBackpackSectionDestination.TryFind(mergeSource,1,i=>mergeCells[i],null,out mergeDestination,out mergeQuantity)&&mergeDestination==-1&&mergeQuantity==0,"incompatible full destination refused");
 var salePack=new ItemValue{Type=8,Slots=new[]{ItemStack.Empty.Clone()}};salePack.Metadata[RebirthBackpackLibraryContents.MetadataKey]="unchanged-theory-library";
 var saleSource=new ItemStack(new ItemValue{Type=2,Payload="arbitrary-sale-item"},3);ItemValue saleNext;ItemStack saleRest;
 A(RebirthBackpackSellStashTransfer.TryDeposit(salePack,saleSource,0,2,out saleNext,out saleRest)&&saleRest.count==1&&saleNext.Slots[0].count==2,"sale stash accepts non-literature item");
 A(saleNext.Metadata[RebirthBackpackLibraryContents.MetadataKey]=="unchanged-theory-library"&&salePack.Slots[0].IsEmpty()&&saleSource.count==3,"sale transfer preserves theory metadata and original inputs");
 A(RebirthBackpackSellStashTransfer.TryWithdraw(saleNext,0,2,ItemStack.Empty.Clone(),out var saleAfter,out var saleReturned)&&saleReturned.count==2&&saleReturned.itemValue.Payload=="arbitrary-sale-item"&&saleAfter.Slots[0].IsEmpty(),"sale withdrawal preserves full identity");
 A(!RebirthBackpackSellStashTransfer.TryWithdraw(saleNext,0,3,ItemStack.Empty.Clone(),out _,out _),"sale withdrawal cannot overdebit");
 var tampered=saleNext.Clone();tampered.Metadata[RebirthBackpackLibraryContents.MetadataKey]="changed-theory-library";
 A(!RebirthBackpackSellStashConservation.IsConserved(salePack,tampered,saleSource,saleRest),"sale conservation refuses theory metadata tamper");
 A(RebirthBackpackSellStashView.TryCreate(Guid.NewGuid(),4,saleNext,true,out var saleCodecView),"sale codec display setup");
 RebirthBackpackSellStashView saleDisplayDecoded=null;
 A(RebirthBackpackSellStashViewCodec.TryEncode(saleCodecView,out var saleDisplayWire)&&saleDisplayWire[0]==83&&RebirthBackpackSellStashViewCodec.TryDecode(saleDisplayWire,out saleDisplayDecoded)&&saleDisplayDecoded.TransferPending&&saleDisplayDecoded.GearRevision==4,"sale display codec roundtrip and discriminator");
 A(saleDisplayDecoded.TryGetSlot(0,out var saleDisplayItem)&&saleDisplayItem.count==2&&saleDisplayItem.itemValue.Payload=="arbitrary-sale-item","sale display wire preserves unrestricted item identity");
 A(!RebirthBackpackLibraryViewCodec.TryDecode(saleDisplayWire,out _),"Theory display codec refuses sale payload");
 A(!RebirthBackpackSellStashViewCodec.TryDecode(saleDisplayWire.Skip(1).ToArray(),out _),"sale display refuses missing section discriminator");
 A(!RebirthBackpackSellStashViewCodec.TryDecode(saleDisplayWire.Take(saleDisplayWire.Length-1).ToArray(),out _)&&!RebirthBackpackSellStashViewCodec.TryDecode(saleDisplayWire.Concat(new byte[]{0}).ToArray(),out _),"sale display refuses truncated and trailing payload");
 var legacySaleCreation="legacy-"+new string('c',64);
 A(RebirthBackpackSellStashView.TryCreate(legacySaleCreation,4,saleNext,false,out saleCodecView)&&RebirthBackpackSellStashViewCodec.TryEncode(saleCodecView,out saleDisplayWire)&&RebirthBackpackSellStashViewCodec.TryDecode(saleDisplayWire,out saleDisplayDecoded)&&saleDisplayDecoded.CreationId==legacySaleCreation,"sale display preserves complete legacy character identity");
 var saleSession=new object();var saleCacheCreation=saleCodecView.CreationId;var saleCache=new RebirthBackpackSellStashViewCache(saleSession,saleCacheCreation);var saleRequest=Guid.NewGuid();
 A(saleCache.BeginRequest(saleSession,saleCacheCreation,saleRequest),"sale cache request binds current owner session");
 A(RebirthBackpackSellStashViewResponse.TryCreate(saleCacheCreation,saleRequest,4,RebirthBackpackLibraryViewStatus.Ready,saleCodecView,out var saleReply),"sale owner display response ready");
 using(var memory=new MemoryStream()){
  using(var writer=new BinaryWriter(memory,Encoding.UTF8,true))saleReply.Write(writer);
  A(memory.Length==saleReply.BodyLength,"sale display response exact native body length");memory.Position=0;
  using(var reader=new BinaryReader(memory,Encoding.UTF8,true)){A(RebirthBackpackSellStashViewResponse.TryRead(reader,out var decodedSaleReply)&&memory.Position==memory.Length&&decodedSaleReply.Deliver(saleCache,saleSession),"sale response read and scoped delivery");}
  memory.Position=0;using(var reader=new BinaryReader(memory,Encoding.UTF8,true))A(!RebirthBackpackLibraryViewResponse.TryRead(reader,out _),"Theory response refuses sale envelope including section discriminator");
 }
 A(saleCache.TryGet(saleSession,saleCacheCreation,out var cachedSale)&&cachedSale.GearRevision==4,"sale cache holds current view");
 var nextSaleRequest=Guid.NewGuid();A(saleCache.BeginRequest(saleSession,saleCacheCreation,nextSaleRequest)&&!saleCache.Receive(new object(),saleCacheCreation,nextSaleRequest,saleCodecView)&&!saleCache.Receive(saleSession,saleCacheCreation,saleRequest,saleCodecView),"sale cache refuses replaced session and stale request");
 A(RebirthBackpackSellStashView.TryCreate(saleCacheCreation,3,saleNext,false,out var olderSale)&&!saleCache.Receive(saleSession,saleCacheCreation,nextSaleRequest,olderSale),"sale cache refuses older gear revision");
 A(RebirthBackpackSellStashViewResponse.TryCreate(saleCacheCreation,nextSaleRequest,-1,RebirthBackpackLibraryViewStatus.Unavailable,null,out var unavailableSaleReply)&&!unavailableSaleReply.Deliver(saleCache,saleSession)&&saleCache.TryGet(saleSession,saleCacheCreation,out cachedSale),"unavailable sale reply preserves existing display");
 A(saleCache.ReceiveNoBackpack(saleSession,saleCacheCreation,nextSaleRequest,5)&&saleCache.IsNoBackpack(saleSession,saleCacheCreation)&&!saleCache.TryGet(saleSession,saleCacheCreation,out _),"fresh no-backpack sale reply clears display");
 var saleCreation=Guid.NewGuid().ToString("N");RebirthBackpackLibraryReceipt saleReceipt;
 A(RebirthBackpackLibraryReceipt.TryCreateSellStash(Guid.NewGuid(),saleCreation,3,true,0,0,2,true,salePack,saleSource,out saleReceipt)&&saleReceipt.IsSellStash,"sale receipt plans unrestricted item under explicit section");
 var quickOwner=new object();var quickWorld=new object();
 A(RebirthBackpackQuickTransferContinuation.TryCreate(quickOwner,quickWorld,saleReceipt,saleSource,out var quick),"quick continuation captures exact receipt source");
 saleReceipt.TryGetImages(out _,out var quickPack,out _,out var quickSource);
 var quickSettlement=RebirthBackpackLibrarySettlement.Create(saleReceipt,true);
 A(!quick.TryAdvance(quickOwner,quickWorld,saleCreation,4,true,quickSettlement,quickPack,quickSource,out _),"quick continuation waits pending journal");
 A(!quick.TryAdvance(new object(),quickWorld,saleCreation,4,false,quickSettlement,quickPack,quickSource,out _),"quick continuation rejects changed owner");
 A(!quick.TryAdvance(quickOwner,quickWorld,saleCreation,5,false,quickSettlement,quickPack,quickSource,out _),"quick continuation rejects newer revision");
 var changedQuickSource=quickSource.Clone();changedQuickSource.itemValue.Payload="replaced";
 A(!quick.TryAdvance(quickOwner,quickWorld,saleCreation,4,false,quickSettlement,quickPack,changedQuickSource,out _),"quick continuation rejects replaced remainder");
 A(!quick.TryAdvance(quickOwner,quickWorld,saleCreation,4,false,RebirthBackpackLibrarySettlement.Create(saleReceipt,false),quickPack,quickSource,out _),"quick continuation rejects unapplied outcome");
 A(quick.TryAdvance(quickOwner,quickWorld,saleCreation,4,false,quickSettlement,quickPack,quickSource,out var quickRemainder)&&quickRemainder.count==1,"quick continuation admits exact settled remainder");
 A(!quick.TryAdvance(quickOwner,quickWorld,saleCreation,4,false,quickSettlement,quickPack,quickSource,out _),"quick continuation consumes settlement once");
 A(!RebirthBackpackQuickTransferContinuation.TryCreate(quickOwner,quickWorld,saleReceipt,changedQuickSource,out _),"quick continuation refuses wrong initial source");
 A(RebirthBackpackLibraryReceipt.TryCreateSellStash(Guid.NewGuid(),saleCreation,4,true,1,0,1,false,saleNext,ItemStack.Empty.Clone(),out var quickWithdrawReceipt)&&RebirthBackpackQuickTransferContinuation.TryCreate(quickOwner,quickWorld,quickWithdrawReceipt,saleNext.Slots[0],out var unusedQuick),"withdraw continuation captures stored source");
 RebirthBackpackQuickTransferContinuation.TryCreate(quickOwner,quickWorld,quickWithdrawReceipt,saleNext.Slots[0],out var quickWithdraw);
 quickWithdrawReceipt.TryGetImages(out _,out var quickWithdrawPack,out _,out _);
 A(quickWithdraw.TryAdvance(quickOwner,quickWorld,saleCreation,5,false,RebirthBackpackLibrarySettlement.Create(quickWithdrawReceipt,true),quickWithdrawPack,quickWithdrawPack.Slots[0],out var withdrawnRemainder)&&withdrawnRemainder.count==1,"withdraw continuation admits exact stored remainder");
 RebirthBackpackQuickTransferContinuation.TryCreate(quickOwner,quickWorld,saleReceipt,saleSource,out var projectedQuick);
 var projectedCells=quickPack.Slots.Select(c=>c.Clone()).ToArray();projectedCells[0].count--;
 A(!projectedQuick.TryAdvanceProjection(quickOwner,quickWorld,saleCreation,4,false,quickSettlement,"pack",projectedCells,quickSource,out _),"quick projection refuses changed destination section");
 projectedCells=quickPack.Slots.Select(c=>c.Clone()).ToArray();
 A(!projectedQuick.TryAdvanceProjection(quickOwner,new object(),saleCreation,4,false,quickSettlement,"pack",projectedCells,quickSource,out _),"quick projection refuses changed world");
 A(!projectedQuick.TryAdvanceProjection(quickOwner,quickWorld,saleCreation,4,false,quickSettlement,"other-pack",projectedCells,quickSource,out _),"quick projection refuses changed backpack type");
 A(projectedQuick.TryAdvanceProjection(quickOwner,quickWorld,saleCreation,4,false,quickSettlement,"pack",projectedCells,quickSource,out var projectedRemaining)&&projectedRemaining.count==1,"quick projection admits exact settled section and source");
 A(!projectedQuick.TryAdvanceProjection(quickOwner,quickWorld,saleCreation,4,false,quickSettlement,"pack",projectedCells,quickSource,out _),"quick projection consumes settlement once");
 Console.WriteLine("PASS 5 owner projection continuation assertions: complete section, world, pack type, exact remainder, duplicate; projection never supplies inventory authority.");
 Console.WriteLine("PASS 11 quick continuation assertions: exact receipt source, pending, owner, revision, metadata, rejected outcome, settled remainder, duplicate, initial source and withdrawal; native adapters doubled.");
 A((string)saleReceipt.ToXml().Attribute("version")=="2"&&(string)saleReceipt.ToXml().Attribute("section")=="sell","sale receipt uses explicit version2 section");
 A(RebirthBackpackLibraryWireCodec.TryEncode(saleReceipt,out var saleWire)&&RebirthBackpackLibraryWireCodec.TryDecode(saleWire,out var saleDecoded)&&saleDecoded.IsSellStash&&XNode.DeepEquals(saleReceipt.ToXml(),saleDecoded.ToXml()),"sale section survives real bounded wire codec roundtrip");
 var badSale=saleReceipt.ToXml();badSale.SetAttributeValue("section","unknown");A(!RebirthBackpackLibraryReceipt.TryRead(badSale,out _),"unknown receipt section refused");
 badSale=saleReceipt.ToXml();badSale.SetAttributeValue("section",null);A(!RebirthBackpackLibraryReceipt.TryRead(badSale,out _),"v2 missing section refused");
 badSale=saleReceipt.ToXml();badSale.SetAttributeValue("version",1);A(!RebirthBackpackLibraryReceipt.TryRead(badSale,out _),"legacy schema refuses extra section attribute");
 badSale.SetAttributeValue("section",null);A(!RebirthBackpackLibraryReceipt.TryRead(badSale,out _),"sale receipt cannot be reinterpreted as legacy Theory receipt");
 var saleState=new RebirthWorldSupportState{GearRevision=3};saleState.EquippedGearBySlot["backpack"]="pack";saleState.EquippedGearItemDataBySlot["backpack"]=RebirthNativeItemCodec.Encode(salePack);
 A(RebirthBackpackLibraryJournal.Prepare(saleState,saleReceipt,saleCreation,()=>true)&&saleState.PendingLibraryTransfer.IsSellStash,"shared custody journal persists explicit sale receipt");
 A(RebirthBackpackLibraryJournal.MarkOwnerApplied(saleState,saleReceipt.TransactionId,saleCreation,r=>r.IsSellStash,()=>true),"sale journal requires explicit saved-owner checkpoint callback");
 A(RebirthBackpackLibraryJournal.CommitBackpack(saleState,saleReceipt.TransactionId,saleCreation,()=>true)&&saleState.GearRevision==4,"sale backpack commit advances shared gear revision once");
 A(RebirthBackpackLibraryJournal.Finish(saleState,saleReceipt.TransactionId,saleCreation,r=>r.IsSellStash,()=>true)&&saleState.PendingLibraryTransfer==null,"sale journal finishes through existing durable settlement");
 var pack=new ItemValue{Type=8,Slots=new[]{ItemStack.Empty.Clone(),S(2)}};var source=S(4);ItemValue next;ItemStack rest;
 A(RebirthBackpackLibraryTransfer.TryDeposit(pack,source,0,3,out next,out rest),"deposit");
 A(rest.count==1&&next.Slots[0].count==3&&source.count==4&&pack.Slots[0].IsEmpty(),"deposit conservation and isolation");
 A(next.Slots[0].itemValue.Payload==source.itemValue.Payload,"payload");
 A(!RebirthBackpackLibraryTransfer.TryDeposit(pack,S(2,"other"),1,1,out next,out rest)&&next==null&&rest==null,"different metadata refused");
 A(RebirthBackpackLibraryTransfer.TryDeposit(pack,source,1,3,out next,out rest)&&next.Slots[1].count==5&&rest.count==1,"matching merge");
 A(!RebirthBackpackLibraryTransfer.TryDeposit(pack,source,1,4,out next,out rest),"overfull refused");
 A(!RebirthBackpackLibraryTransfer.TryDeposit(pack,source,0,5,out next,out rest),"overdebit refused");
 A(!RebirthBackpackLibraryTransfer.TryDeposit(pack,source,-1,1,out next,out rest),"badslot");
 A(!RebirthBackpackLibraryTransfer.TryDeposit(pack,S(6),0,6,out next,out rest)&&next==null&&rest==null,"empty slot stacklimit");
 var oversized=new ItemValue{Type=8,Slots=new[]{S(6)}};A(!RebirthBackpackLibraryTransfer.TryWithdraw(oversized,0,6,ItemStack.Empty.Clone(),out next,out rest),"empty destination stacklimit");
 RebirthBackpackLibraryContents.FailWrite=true;
 A(!RebirthBackpackLibraryTransfer.TryDeposit(pack,source,0,1,out next,out rest)&&next==null&&rest==null&&source.count==4,"failed encode retains source");
 RebirthBackpackLibraryContents.FailWrite=false;
 var dest=S(1);
 A(RebirthBackpackLibraryTransfer.TryWithdraw(pack,1,2,dest,out next,out rest)&&next.Slots[1].IsEmpty()&&rest.count==3&&dest.count==1&&pack.Slots[1].count==2,"withdraw conservation/isolation");
 A(!RebirthBackpackLibraryTransfer.TryWithdraw(pack,1,1,S(1,"different"),out next,out rest),"withdraw metadata mismatch");
 RebirthBackpackLibraryContents.FailWrite=true;
 A(!RebirthBackpackLibraryTransfer.TryWithdraw(pack,1,1,dest,out next,out rest)&&next==null&&rest==null&&dest.count==1&&pack.Slots[1].count==2,"failed withdraw unchanged");
 var initial=new ItemValue{Type=8,Payload="outer",Slots=new[]{S(2)}};
 var changed=initial.Clone();changed.Slots=new[]{S(3)};
 A(RebirthBackpackLibraryConservation.IsConserved(initial,changed,S(2),S(1)),"combined balance");
 A(!RebirthBackpackLibraryConservation.IsConserved(initial,changed,S(2),S(2)),"duplicated material rejected");
 A(!RebirthBackpackLibraryConservation.IsConserved(initial,changed,S(2),ItemStack.Empty.Clone()),"lost material rejected");
 changed.Payload="different outer";A(!RebirthBackpackLibraryConservation.IsConserved(initial,changed,S(2),S(1)),"backpack payload replacement rejected");
 changed.Payload="outer";changed.Metadata["unrelated"]="changed";A(!RebirthBackpackLibraryConservation.IsConserved(initial,changed,S(2),S(1)),"outer metadata change rejected");changed.Metadata.Clear();changed.Metadata[RebirthBackpackLibraryContents.MetadataKey]="nested";A(RebirthBackpackLibraryConservation.IsConserved(initial,changed,S(2),S(1)),"library metadata excluded only from outer identity");
 changed.Slots=new[]{S(3,"different")};A(!RebirthBackpackLibraryConservation.IsConserved(initial,changed,S(2),S(1)),"learning payload substitution rejected");
 RebirthBackpackLibraryContents.FailWrite=false;
 var receiptPack=new ItemValue{Type=8,Slots=new[]{ItemStack.Empty.Clone()}};var receiptSource=S(3);
 RebirthBackpackLibraryReceipt receipt,restored;
 A(RebirthBackpackLibraryReceipt.TryCreate(Guid.NewGuid(),Guid.NewGuid(),5,true,0,0,2,true,receiptPack,receiptSource,out receipt),"scoped receipt create");
 A(RebirthBackpackLibraryReceipt.TryRead(receipt.ToXml(),out restored)&&restored.Quantity==2,"receipt roundtrip");
 string migratedCreation="legacy-"+new string('a',64);RebirthBackpackLibraryReceipt migratedReceipt,migratedDecoded;
 A(RebirthBackpackLibraryReceipt.TryCreate(Guid.NewGuid(),migratedCreation,5,true,0,0,2,true,receiptPack,receiptSource,out migratedReceipt),"legacy receipt create");
 byte[] migratedBytes;A(RebirthBackpackLibraryWireCodec.TryEncode(migratedReceipt,out migratedBytes)&&RebirthBackpackLibraryWireCodec.TryDecode(migratedBytes,out migratedDecoded)&&migratedDecoded.CreationId==migratedCreation,"legacy receipt XML wire preserves full identity");
 var legacyOfferSession=new object();var legacyOfferInbox=new RebirthBackpackLibraryOfferInbox(legacyOfferSession);var legacyOfferTransaction=Guid.Parse(migratedReceipt.TransactionId);
 A(legacyOfferInbox.TryBegin(legacyOfferSession,migratedCreation,legacyOfferTransaction,migratedBytes.Length),"legacy offer inbox begin");
 A(!legacyOfferInbox.TryBegin(new object(),migratedCreation,legacyOfferTransaction,migratedBytes.Length)&&!legacyOfferInbox.TryBegin(legacyOfferSession,"legacy-"+new string('b',64),legacyOfferTransaction,migratedBytes.Length),"legacy offer session and character isolation");
 for(int offset=0,index=0;offset<migratedBytes.Length;offset+=RebirthBackpackLibraryOfferAssembly.ChunkBytes,index++){
 var part=migratedBytes.Skip(offset).Take(RebirthBackpackLibraryOfferAssembly.ChunkBytes).ToArray();
 A(legacyOfferInbox.TryAdd(legacyOfferSession,migratedCreation,legacyOfferTransaction,index,part)&&legacyOfferInbox.TryAdd(legacyOfferSession,migratedCreation,legacyOfferTransaction,index,part),"legacy offer identical duplicate accepted");
 RebirthBackpackLibraryOfferChunk legacyChunk,decodedLegacyChunk;A(RebirthBackpackLibraryOfferChunk.TryCreate(migratedCreation,legacyOfferTransaction,migratedBytes.Length,index,part,out legacyChunk),"legacy chunk create");
 using(var chunkStream=new MemoryStream()){
 using(var writer=new BinaryWriter(chunkStream,Encoding.UTF8,true))legacyChunk.Write(writer);
 A(chunkStream.Length==legacyChunk.BodyLength&&chunkStream.ToArray()[0]==2,"legacy chunk exact version2 length");chunkStream.Position=0;
 using(var reader=new BinaryReader(chunkStream,Encoding.UTF8,true))A(RebirthBackpackLibraryOfferChunk.TryRead(reader,out decodedLegacyChunk)&&decodedLegacyChunk.CreationId==migratedCreation&&decodedLegacyChunk.Deliver(legacyOfferInbox,legacyOfferSession),"legacy chunk roundtrip delivery");
 var badLength=chunkStream.ToArray();badLength[1]=72;using(var bad=new MemoryStream(badLength))using(var reader=new BinaryReader(bad))A(!RebirthBackpackLibraryOfferChunk.TryRead(reader,out decodedLegacyChunk),"legacy chunk identity length refusal");
 }
 part[0]^=1;A(!legacyOfferInbox.TryAdd(legacyOfferSession,migratedCreation,legacyOfferTransaction,index,part),"legacy offer conflicting duplicate refused");
 }
 A(legacyOfferInbox.TryFinish(legacyOfferSession,migratedCreation,legacyOfferTransaction,out migratedDecoded)&&migratedDecoded.CreationId==migratedCreation,"legacy offer full assembly");
 A(!legacyOfferInbox.Clear(legacyOfferSession,migratedCreation,Guid.NewGuid())&&legacyOfferInbox.Clear(legacyOfferSession,migratedCreation,legacyOfferTransaction)&&!legacyOfferInbox.TryFinish(legacyOfferSession,migratedCreation,legacyOfferTransaction,out migratedDecoded),"legacy offer authenticated clear scope");
 var migratedSettlement=RebirthBackpackLibrarySettlement.Create(migratedReceipt,true);RebirthBackpackLibrarySettlement migratedOutcome;
 A(RebirthBackpackLibrarySettlement.TryRead(new XElement("support",migratedSettlement.Write()),6,out migratedOutcome)&&migratedOutcome.CreationId==migratedCreation&&migratedOutcome.Applied,"legacy settlement roundtrip");
 var malformedLegacy=migratedReceipt.ToXml();malformedLegacy.SetAttributeValue("creation","legacy-"+new string('A',64));
 A(!RebirthBackpackLibraryReceipt.TryRead(malformedLegacy,out migratedDecoded)&&migratedDecoded==null,"noncanonical migration identity refused");
 var malformedSettlement=migratedSettlement.Write();malformedSettlement.SetAttributeValue("creation","legacy-short");
 A(!RebirthBackpackLibrarySettlement.TryRead(new XElement("support",malformedSettlement),6,out migratedOutcome)&&migratedOutcome==null,"malformed settlement identity refused");
 var detached=receipt.ToXml();detached.SetAttributeValue("quantity",1);A(!RebirthBackpackLibraryReceipt.TryRead(detached,out restored),"tampered quantity conflicts with images");
 A(receipt.Quantity==2&&RebirthBackpackLibraryReceipt.TryRead(receipt.ToXml(),out restored),"XML clone isolation");
 detached=receipt.ToXml();detached.SetAttributeValue("revision",long.MaxValue);A(!RebirthBackpackLibraryReceipt.TryRead(detached,out restored),"revision overflow refused");
 detached=receipt.ToXml();detached.SetAttributeValue("bag",false);detached.SetAttributeValue("inventory",18);A(!RebirthBackpackLibraryReceipt.TryRead(detached,out restored),"belt dummy index refused");
 detached=receipt.ToXml();detached.SetAttributeValue("extra",1);A(!RebirthBackpackLibraryReceipt.TryRead(detached,out restored),"extra shape refused");
 receipt.TryGetImages(out var viewBefore,out var viewAfter,out var viewInventoryBefore,out var viewInventoryAfter);
 RebirthBackpackLibraryView libraryView;A(RebirthBackpackLibraryView.TryCreate(Guid.Parse(receipt.CreationId),5,viewAfter,true,out libraryView)&&libraryView.Capacity==viewAfter.Slots.Length&&libraryView.OccupiedSlots==1&&libraryView.TransferPending&&libraryView.GearRevision==5,"library view exact additional capacity and occupancy");
 ItemStack viewSlot;A(libraryView.TryGetSlot(0,out viewSlot)&&viewSlot.count==2,"library view exact full stored stack");viewSlot.count=0;viewSlot.itemValue.Payload="view mutation";A(libraryView.TryGetSlot(0,out viewSlot)&&viewSlot.count==2&&viewSlot.itemValue.Payload!="view mutation","library view slot access detached");
 ItemValue viewSource;A(RebirthNativeItemCodec.TryDecode(RebirthNativeItemCodec.Encode(viewAfter),out viewSource),"view source detached codec adapter");A(RebirthBackpackLibraryView.TryCreate(Guid.Parse(receipt.CreationId),5,viewSource,false,out libraryView),"view detached source setup");viewSource.Slots[0].count=0;A(libraryView.TryGetSlot(0,out viewSlot)&&viewSlot.count==2,"source mutation does not change view");
 byte[] displayBytes;RebirthBackpackLibraryView decodedView;A(RebirthBackpackLibraryViewCodec.TryEncode(libraryView,out displayBytes)&&RebirthBackpackLibraryViewCodec.TryDecode(displayBytes,out decodedView)&&decodedView.Capacity==libraryView.Capacity&&decodedView.CreationId==libraryView.CreationId&&decodedView.GearRevision==5&&decodedView.TryGetSlot(0,out viewSlot)&&viewSlot.count==2,"library display bounded exact roundtrip");
 A(!RebirthBackpackLibraryViewCodec.TryDecode(displayBytes.Take(displayBytes.Length-1).ToArray(),out decodedView)&&!RebirthBackpackLibraryViewCodec.TryDecode(displayBytes.Concat(new byte[]{0}).ToArray(),out decodedView),"display truncated or trailing bytes refused");
 A(!RebirthBackpackLibraryViewCodec.TryDecode(new byte[RebirthBackpackLibraryViewCodec.MaxBytes+1],out decodedView),"oversized display refused");
 A(displayBytes[0]==1,"GUID display retains version1 wire format");
 RebirthBackpackLibraryView migratedView;byte[] migratedDisplay;
 A(RebirthBackpackLibraryView.TryCreate(migratedCreation,5,viewAfter,true,out migratedView)&&RebirthBackpackLibraryViewCodec.TryEncode(migratedView,out migratedDisplay),"legacy display encode");
 A(RebirthBackpackLibraryViewCodec.TryEncode(migratedView,out migratedDisplay)&&migratedDisplay[0]==2&&RebirthBackpackLibraryViewCodec.TryDecode(migratedDisplay,out decodedView)&&decodedView.CreationId==migratedCreation&&decodedView.TransferPending&&decodedView.TryGetSlot(0,out viewSlot)&&viewSlot.count==2,"legacy display version2 exact roundtrip");
 A(!RebirthBackpackLibraryViewCodec.TryDecode(migratedDisplay.Take(migratedDisplay.Length-1).ToArray(),out decodedView)&&!RebirthBackpackLibraryViewCodec.TryDecode(migratedDisplay.Concat(new byte[]{0}).ToArray(),out decodedView),"legacy display strict framing");
 var invalidLegacyDisplay=(byte[])migratedDisplay.Clone();invalidLegacyDisplay[1]=72;
 A(!RebirthBackpackLibraryViewCodec.TryDecode(invalidLegacyDisplay,out decodedView),"legacy identity length bound");
 invalidLegacyDisplay=(byte[])migratedDisplay.Clone();invalidLegacyDisplay[12]=(byte)'A';
 A(!RebirthBackpackLibraryViewCodec.TryDecode(invalidLegacyDisplay,out decodedView),"legacy display uppercase hash rejected");
 var migratedSession=new object();var migratedCache=new RebirthBackpackLibraryViewCache(migratedSession,migratedCreation);var migratedRequest=Guid.NewGuid();
 A(migratedCache.BeginRequest(migratedSession,migratedCreation,migratedRequest),"legacy cache request");
 A(!migratedCache.Receive(new object(),migratedCreation,migratedRequest,migratedView)&&!migratedCache.Receive(migratedSession,"legacy-"+new string('b',64),migratedRequest,migratedView)&&!migratedCache.Receive(migratedSession,migratedCreation,Guid.NewGuid(),migratedView),"legacy cache rejects wrong session owner or request");
 A(migratedCache.Receive(migratedSession,migratedCreation,migratedRequest,migratedView)&&migratedCache.TryGet(migratedSession,migratedCreation,out decodedView)&&decodedView.CreationId==migratedCreation,"legacy cache receive");
 A(!migratedCache.Receive(migratedSession,migratedCreation,migratedRequest,migratedView),"legacy cache duplicate response refused");
 migratedRequest=Guid.NewGuid();A(migratedCache.BeginRequest(migratedSession,migratedCreation,migratedRequest)&&!migratedCache.ReceiveNoBackpack(migratedSession,migratedCreation,migratedRequest,4)&&migratedCache.ReceiveNoBackpack(migratedSession,migratedCreation,migratedRequest,6)&&migratedCache.IsNoBackpack(migratedSession,migratedCreation)&&!migratedCache.TryGet(migratedSession,migratedCreation,out decodedView),"legacy cache monotonic no-backpack response");
 migratedCache.Reset();A(!migratedCache.IsNoBackpack(migratedSession,migratedCreation),"legacy cache reset");
 var badDisplay=(byte[])displayBytes.Clone();badDisplay[25]=2;A(!RebirthBackpackLibraryViewCodec.TryDecode(badDisplay,out decodedView),"display invalid pending flag refused");
 A(!libraryView.TryGetSlot(-1,out viewSlot)&&!libraryView.TryGetSlot(libraryView.Capacity,out viewSlot),"view slot bounds refused");
 A(!RebirthBackpackLibraryView.TryCreate(Guid.Empty,5,viewAfter,false,out libraryView)&&!RebirthBackpackLibraryView.TryCreate(Guid.NewGuid(),-1,viewAfter,false,out libraryView),"view invalid owner revision refused");
 var viewSession=new object();var viewCreation=Guid.Parse(receipt.CreationId);var cache=new RebirthBackpackLibraryViewCache(viewSession,viewCreation);var oldViewRequest=Guid.NewGuid();var newViewRequest=Guid.NewGuid();
 A(RebirthBackpackLibraryView.TryCreate(viewCreation,5,viewAfter,true,out libraryView),"view cache current snapshot setup");
for(int displayIndex=0;displayIndex<libraryView.Capacity;displayIndex++)
 {ItemStack exact;string displayId;int displayCount;A(libraryView.TryGetSlot(displayIndex,out exact)&&libraryView.TryGetDisplaySlot(displayIndex,out displayId,out displayCount)&&displayCount==exact.count&&displayId==(exact.IsEmpty()?string.Empty:exact.itemValue.ItemClass.GetItemName()),"scalar library presentation matches detached native slot");}
 string invalidDisplay;int invalidCount;A(!libraryView.TryGetDisplaySlot(-1,out invalidDisplay,out invalidCount)&&!libraryView.TryGetDisplaySlot(libraryView.Capacity,out invalidDisplay,out invalidCount),"scalar display rejects out of capacity");
 
 A(cache.BeginRequest(viewSession,viewCreation,oldViewRequest)&&cache.BeginRequest(viewSession,viewCreation,newViewRequest)&&!cache.Receive(viewSession,viewCreation,oldViewRequest,libraryView),"late superseded view response refused");
 A(!cache.Receive(new object(),viewCreation,newViewRequest,libraryView)&&!cache.Receive(viewSession,Guid.NewGuid(),newViewRequest,libraryView),"view response wrong session/character refused");
 A(cache.Receive(viewSession,viewCreation,newViewRequest,libraryView)&&cache.TryGet(viewSession,viewCreation,out decodedView)&&ReferenceEquals(decodedView,libraryView)&&!cache.Receive(viewSession,viewCreation,newViewRequest,libraryView),"view response accepted once and immutable cache access");
 A(RebirthBackpackLibraryView.TryCreate(viewCreation,4,viewAfter,false,out decodedView)&&cache.BeginRequest(viewSession,viewCreation,oldViewRequest)&&!cache.Receive(viewSession,viewCreation,oldViewRequest,decodedView),"older gear revision cannot replace view");
 A(RebirthBackpackLibraryView.TryCreate(viewCreation,5,viewAfter,false,out decodedView)&&cache.Receive(viewSession,viewCreation,oldViewRequest,decodedView)&&cache.TryGet(viewSession,viewCreation,out decodedView)&&!decodedView.TransferPending,"same revision pending status refresh accepted only on current request");
 var clearRequest=Guid.NewGuid();A(cache.BeginRequest(viewSession,viewCreation,clearRequest)&&!cache.ReceiveNoBackpack(new object(),viewCreation,clearRequest,6)&&!cache.ReceiveNoBackpack(viewSession,viewCreation,clearRequest,4),"empty view wrong scope/older revision refused");
 A(cache.ReceiveNoBackpack(viewSession,viewCreation,clearRequest,6)&&!cache.TryGet(viewSession,viewCreation,out decodedView),"authoritative no backpack clears visible library");
 A(cache.IsNoBackpack(viewSession,viewCreation)&&!cache.IsNoBackpack(new object(),viewCreation)&&!cache.IsNoBackpack(viewSession,Guid.NewGuid()),"no backpack display status is owner-session scoped");
 A(cache.BeginRequest(viewSession,viewCreation,Guid.NewGuid())&&!cache.Receive(viewSession,viewCreation,clearRequest,libraryView),"delayed old view cannot undo empty clear");
 var afterClearRequest=Guid.NewGuid();A(cache.BeginRequest(viewSession,viewCreation,afterClearRequest)&&!cache.Receive(viewSession,viewCreation,afterClearRequest,libraryView),"empty clear retains revision floor");
 A(!cache.TryGet(viewSession,Guid.NewGuid(),out decodedView),"new character cannot access old view");cache.Reset();A(!cache.TryGet(viewSession,viewCreation,out decodedView),"view reset clears display");
 var responseCache=new RebirthBackpackLibraryViewCache(viewSession,viewCreation);var responseRequest=Guid.NewGuid();A(responseCache.BeginRequest(viewSession,viewCreation,responseRequest),"response request setup");RebirthBackpackLibraryViewResponse displayResponse,decodedResponse;
 A(RebirthBackpackLibraryViewResponse.TryCreate(viewCreation,responseRequest,5,RebirthBackpackLibraryViewStatus.Ready,libraryView,out displayResponse),"ready response scope setup");
 using(var responseStream=new MemoryStream()){using(var writer=new BinaryWriter(responseStream,Encoding.UTF8,true))displayResponse.Write(writer);A(responseStream.Length==displayResponse.BodyLength,"display response exact length");responseStream.Position=0;using(var reader=new BinaryReader(responseStream,Encoding.UTF8,true))A(RebirthBackpackLibraryViewResponse.TryRead(reader,out decodedResponse)&&decodedResponse.Deliver(responseCache,viewSession),"display response roundtrip and cache delivery");}
 var legacyResponseToken=Guid.NewGuid();migratedCache.Reset();A(migratedCache.BeginRequest(migratedSession,migratedCreation,legacyResponseToken),"legacy response cache setup");
 RebirthBackpackLibraryViewResponse legacyResponse;
 A(RebirthBackpackLibraryViewResponse.TryCreate(migratedCreation,legacyResponseToken,5,RebirthBackpackLibraryViewStatus.Ready,migratedView,out legacyResponse),"legacy response create");
 using(var stream=new MemoryStream()){
 using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))legacyResponse.Write(writer);
 A(stream.Length==legacyResponse.BodyLength&&stream.ToArray()[0]==2,"legacy response exact version2 length");stream.Position=0;
 using(var reader=new BinaryReader(stream,Encoding.UTF8,true))A(RebirthBackpackLibraryViewResponse.TryRead(reader,out decodedResponse)&&decodedResponse.CreationId==migratedCreation&&decodedResponse.Deliver(migratedCache,migratedSession),"legacy response roundtrip delivery");
 var wrongOwner=stream.ToArray();wrongOwner[9]=(byte)'b';using(var bad=new MemoryStream(wrongOwner))using(var reader=new BinaryReader(bad))A(!RebirthBackpackLibraryViewResponse.TryRead(reader,out decodedResponse),"legacy envelope inner view mismatch refused");
 var wrongLength=stream.ToArray();wrongLength[1]=72;using(var bad=new MemoryStream(wrongLength))using(var reader=new BinaryReader(bad))A(!RebirthBackpackLibraryViewResponse.TryRead(reader,out decodedResponse),"legacy response identity bound");
 }
 var noPackRequest=Guid.NewGuid();A(responseCache.BeginRequest(viewSession,viewCreation,noPackRequest)&&RebirthBackpackLibraryViewResponse.TryCreate(viewCreation,noPackRequest,6,RebirthBackpackLibraryViewStatus.NoBackpack,null,out displayResponse)&&displayResponse.Deliver(responseCache,viewSession)&&!responseCache.TryGet(viewSession,viewCreation,out decodedView),"no backpack response clears matched cache");
 A(!RebirthBackpackLibraryViewResponse.TryCreate(Guid.NewGuid(),Guid.NewGuid(),5,RebirthBackpackLibraryViewStatus.Ready,libraryView,out displayResponse)&&!RebirthBackpackLibraryViewResponse.TryCreate(viewCreation,Guid.NewGuid(),5,(RebirthBackpackLibraryViewStatus)99,null,out displayResponse),"response mismatched character/status refused");
 byte[] wireBytes;A(RebirthBackpackLibraryWireCodec.TryEncode(receipt,out wireBytes)&&RebirthBackpackLibraryWireCodec.TryDecode(wireBytes,out restored)&&XNode.DeepEquals(receipt.ToXml(),restored.ToXml()),"wire exact receipt roundtrip");
 wireBytes=Encoding.UTF8.GetBytes(receipt.ToXml().ToString(SaveOptions.DisableFormatting)+new string(' ',20000));
 var inboxSession=new object();var inbox=new RebirthBackpackLibraryOfferInbox(inboxSession);var wireCreation=Guid.Parse(receipt.CreationId);var wireTransaction=Guid.Parse(receipt.TransactionId);
 A(!inbox.TryBegin(new object(),wireCreation,wireTransaction,wireBytes.Length),"inbox old session refused");
 A(inbox.TryBegin(inboxSession,wireCreation,wireTransaction,wireBytes.Length)&&inbox.TryBegin(inboxSession,wireCreation,wireTransaction,wireBytes.Length),"inbox same offer retry");
 A(!inbox.TryBegin(inboxSession,wireCreation,Guid.NewGuid(),wireBytes.Length)&&!inbox.TryBegin(inboxSession,wireCreation,wireTransaction,wireBytes.Length+1),"inbox competing offer or length cannot replace unfinished assembly");
 A(!inbox.TryAdd(inboxSession,Guid.NewGuid(),wireTransaction,0,new byte[16384])&&!inbox.Clear(new object(),wireCreation,wireTransaction),"inbox wrong scope cannot add or clear");
 A(inbox.Clear(inboxSession,wireCreation,wireTransaction)&&!inbox.TryFinish(inboxSession,wireCreation,wireTransaction,out restored),"inbox scoped cleanup releases assembly");
 A(inbox.TryBegin(inboxSession,wireCreation,Guid.NewGuid(),wireBytes.Length),"inbox explicitly cleared session accepts next offer");
 var offerAssembly=new RebirthBackpackLibraryOfferAssembly(Guid.Parse(receipt.CreationId),Guid.Parse(receipt.TransactionId),wireBytes.Length);
 A(!offerAssembly.TryFinish(out restored),"incomplete offer refused");
 int chunkCount=(wireBytes.Length+RebirthBackpackLibraryOfferAssembly.ChunkBytes-1)/RebirthBackpackLibraryOfferAssembly.ChunkBytes;
 for(int ci=chunkCount-1;ci>=0;ci--){int offset=ci*RebirthBackpackLibraryOfferAssembly.ChunkBytes;var part=new byte[Math.Min(RebirthBackpackLibraryOfferAssembly.ChunkBytes,wireBytes.Length-offset)];Buffer.BlockCopy(wireBytes,offset,part,0,part.Length);A(offerAssembly.TryAdd(ci,part)&&offerAssembly.TryAdd(ci,part),"out of order and duplicate chunks accepted");part[0]^=1;A(!offerAssembly.TryAdd(ci,part),"conflicting chunk refused");}
 A(offerAssembly.TryFinish(out restored)&&XNode.DeepEquals(restored.ToXml(),receipt.ToXml()),"assembled offer preserves exact receipt");
 var firstCompleted=restored;A(offerAssembly.TryFinish(out restored)&&ReferenceEquals(firstCompleted,restored),"completed offer reuses validated immutable receipt");
 A(!offerAssembly.TryAdd(-1,new byte[1])&&!offerAssembly.TryAdd(chunkCount,new byte[1])&&!offerAssembly.TryAdd(0,new byte[0]),"invalid chunk bounds refused");
 var framedInbox=new RebirthBackpackLibraryOfferInbox(inboxSession);
 for(int ci=chunkCount-1;ci>=0;ci--){int offset=ci*RebirthBackpackLibraryOfferAssembly.ChunkBytes;var part=new byte[Math.Min(RebirthBackpackLibraryOfferAssembly.ChunkBytes,wireBytes.Length-offset)];Buffer.BlockCopy(wireBytes,offset,part,0,part.Length);RebirthBackpackLibraryOfferChunk frame,decodedFrame;A(RebirthBackpackLibraryOfferChunk.TryCreate(wireCreation,wireTransaction,wireBytes.Length,ci,part,out frame),"chunk create");part[0]^=1;using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))frame.Write(writer);A(stream.Length==frame.BodyLength,"exact chunk body length");var all=stream.ToArray();stream.Position=0;using(var reader=new BinaryReader(stream,Encoding.UTF8,true))A(RebirthBackpackLibraryOfferChunk.TryRead(reader,out decodedFrame)&&decodedFrame.Deliver(framedInbox,inboxSession),"framed chunk decode and deliver");using(var shortStream=new MemoryStream(all.Take(all.Length-1).ToArray()))using(var reader=new BinaryReader(shortStream))A(!RebirthBackpackLibraryOfferChunk.TryRead(reader,out decodedFrame)&&decodedFrame==null,"truncated chunk refused");}}
 A(framedInbox.TryFinish(inboxSession,wireCreation,wireTransaction,out restored)&&XNode.DeepEquals(restored.ToXml(),receipt.ToXml()),"complete framed inbox handoff exact receipt");
 var inboxOwner=new EntityPlayerLocal();inboxOwner.world.Player=inboxOwner;var priorWorld=GameManager.Instance?.World;GameManager.Instance=GameManager.Instance??new GameManager();GameManager.Instance.World=inboxOwner.world;
 var ownerOfferInbox=new RebirthBackpackLibraryOwnerInbox(inboxOwner,wireCreation,inboxSession);
 for(int ci=0;ci<chunkCount;ci++){int offset=ci*RebirthBackpackLibraryOfferAssembly.ChunkBytes;var part=new byte[Math.Min(RebirthBackpackLibraryOfferAssembly.ChunkBytes,wireBytes.Length-offset)];Buffer.BlockCopy(wireBytes,offset,part,0,part.Length);RebirthBackpackLibraryOfferChunk frame;A(RebirthBackpackLibraryOfferChunk.TryCreate(wireCreation,wireTransaction,wireBytes.Length,ci,part,out frame),"owner chunk create");A(!ownerOfferInbox.Receive(inboxOwner,Guid.NewGuid(),inboxSession,frame)&&!ownerOfferInbox.Receive(inboxOwner,wireCreation,new object(),frame),"owner replacement creation/session refused");A(ownerOfferInbox.Receive(inboxOwner,wireCreation,inboxSession,frame),"current owner receives chunk");}
 A(ownerOfferInbox.TryGetOffer(inboxOwner,wireCreation,inboxSession,wireTransaction,out restored)&&XNode.DeepEquals(restored.ToXml(),receipt.ToXml()),"current owner gets complete offer without inventory mutation");
 GameManager.Instance.World=new PlayerWorld();A(!ownerOfferInbox.TryGetOffer(inboxOwner,wireCreation,inboxSession,wireTransaction,out restored)&&restored==null,"old world offer inaccessible");GameManager.Instance.World=inboxOwner.world;
 inboxOwner.world.Player=new EntityPlayerLocal();A(!ownerOfferInbox.TryGetOffer(inboxOwner,wireCreation,inboxSession,wireTransaction,out restored),"replaced live entity offer inaccessible");inboxOwner.world.Player=inboxOwner;
 A(!ownerOfferInbox.Clear(new object(),wireTransaction)&&ownerOfferInbox.Clear(inboxSession,wireTransaction),"owner cleanup exact session required");GameManager.Instance.World=priorWorld;
 GameManager.Instance.World=inboxOwner.world;inboxOwner.world.Remote=true;var clientConnection=new TestConnection();SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{connectionToServer=new INetConnection[]{clientConnection}};RebirthSurvivorClientState.Creation=wireCreation.ToString("N");
 RebirthBackpackLibraryOfferChunk clientFrame=null;
 for(int ci=0;ci<chunkCount;ci++){int offset=ci*RebirthBackpackLibraryOfferAssembly.ChunkBytes;var part=new byte[Math.Min(RebirthBackpackLibraryOfferAssembly.ChunkBytes,wireBytes.Length-offset)];Buffer.BlockCopy(wireBytes,offset,part,0,part.Length);A(RebirthBackpackLibraryOfferChunk.TryCreate(wireCreation,wireTransaction,wireBytes.Length,ci,part,out clientFrame),"client frame create");A(!RebirthBackpackLibraryClientOffers.Receive(inboxOwner.world,2,clientFrame),"wrong target player refused");A(RebirthBackpackLibraryClientOffers.Receive(inboxOwner.world,1,clientFrame),"actual client facade receives scoped chunk");}
 A(RebirthBackpackLibraryClientOffers.TryGetOffer(inboxOwner.world,1,wireTransaction,out restored)&&XNode.DeepEquals(restored.ToXml(),receipt.ToXml()),"actual client facade complete offer");
 clientConnection.Disconnected=true;A(!RebirthBackpackLibraryClientOffers.TryGetOffer(inboxOwner.world,1,wireTransaction,out restored)&&restored==null,"disconnected client offer refused");clientConnection.Disconnected=false;
 RebirthSurvivorClientState.Creation=Guid.NewGuid().ToString("N");A(!RebirthBackpackLibraryClientOffers.Receive(inboxOwner.world,1,clientFrame)&&!RebirthBackpackLibraryClientOffers.TryGetOffer(inboxOwner.world,1,wireTransaction,out restored),"stale projected character refused");RebirthSurvivorClientState.Creation=wireCreation.ToString("N");
 SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0]=new TestConnection();A(!RebirthBackpackLibraryClientOffers.TryGetOffer(inboxOwner.world,1,wireTransaction,out restored),"new connection cannot retrieve old inbox");A(RebirthBackpackLibraryClientOffers.Receive(inboxOwner.world,1,clientFrame)&&!RebirthBackpackLibraryClientOffers.TryGetOffer(inboxOwner.world,1,wireTransaction,out restored),"new connection starts fresh incomplete assembly");
 SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;A(!RebirthBackpackLibraryClientOffers.Receive(inboxOwner.world,1,clientFrame),"server role refuses client offers");SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;
 RebirthBackpackLibraryClientOffers.Reset();
 for(int ci=chunkCount-1;ci>=0;ci--){int offset=ci*RebirthBackpackLibraryOfferAssembly.ChunkBytes;var part=new byte[Math.Min(RebirthBackpackLibraryOfferAssembly.ChunkBytes,wireBytes.Length-offset)];Buffer.BlockCopy(wireBytes,offset,part,0,part.Length);A(RebirthBackpackLibraryOfferChunk.TryCreate(wireCreation,wireTransaction,wireBytes.Length,ci,part,out clientFrame),"package frame setup");var sent=new NetPackageRebirthBackpackLibraryOfferChunk().Setup(1,clientFrame);A(sent.PackageDirection==NetPackageDirection.ToClient,"native offer direction");using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))sent.write(writer);A(stream.Length==sent.GetLength(),"native offer exact serialized length");stream.Position=0;var received=new NetPackageRebirthBackpackLibraryOfferChunk();using(var reader=new PooledBinaryReader(stream)){A(reader.ReadUInt16()==123,"native package ID frame");received.read(reader);A(stream.Position==stream.Length,"native offer consumes exact body");}received.ProcessPackage(inboxOwner.world,GameManager.Instance);bool invalidRead=false;using(var broken=new MemoryStream(new byte[]{1,0,0,0,99}))using(var reader=new PooledBinaryReader(broken)){try{received.read(reader);}catch(InvalidDataException){invalidRead=true;}}A(invalidRead,"pooled native package malformed version refused");bool staleWrite=false;using(var rejectedStream=new MemoryStream())using(var writer=new PooledBinaryWriter(rejectedStream)){try{received.write(writer);}catch(InvalidOperationException){staleWrite=true;}A(rejectedStream.Length==0,"failed read does not retain writable payload");}A(staleWrite,"pooled payload cleared after failed read");received.ProcessPackage(inboxOwner.world,GameManager.Instance);}}
 A(RebirthBackpackLibraryClientOffers.TryGetOffer(inboxOwner.world,1,wireTransaction,out restored)&&XNode.DeepEquals(restored.ToXml(),receipt.ToXml()),"actual native package methods deliver exact assembled receipt");
 A(RebirthBackpackLibraryClientOffers.TryGetCurrentOffer(inboxOwner.world,1,out restored)&&restored.TransactionId==wireTransaction.ToString("N"),"complete native offer discoverable without caller transaction");A(!RebirthBackpackLibraryClientOffers.TryGetCurrentOffer(inboxOwner.world,2,out restored),"wrong owner cannot discover current offer");
 var preparePacket=new NetPackageRebirthBackpackLibraryPrepareRequest().Setup(1,wireCreation,5,true,0,0,1,true);
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))preparePacket.write(writer);A(stream.Length==44&&stream.Length==preparePacket.GetLength(),"prepare native exact packet length");stream.Position=0;using(var reader=new PooledBinaryReader(stream)){reader.ReadUInt16();preparePacket.read(reader);A(stream.Position==stream.Length,"prepare exact body consumed");}}
 bool invalidPrepare=false;try{preparePacket.Setup(1,Guid.Empty,5,true,0,0,1,true);}catch(ArgumentException){invalidPrepare=true;}A(invalidPrepare,"empty preparation creation refused");
 preparePacket.Setup(1,wireCreation,5,true,0,0,1,true);bool truncatedPrepare=false;var badPreparePrefix=new byte[20];Buffer.BlockCopy(wireCreation.ToByteArray(),0,badPreparePrefix,4,16);using(var broken=new MemoryStream(badPreparePrefix))using(var reader=new PooledBinaryReader(broken)){try{preparePacket.read(reader);}catch(EndOfStreamException){truncatedPrepare=true;}}A(truncatedPrepare,"truncated preparation refused");bool writablePrepare=false;using(var stream=new MemoryStream())using(var writer=new PooledBinaryWriter(stream)){try{preparePacket.write(writer);writablePrepare=true;}catch(InvalidOperationException){}}A(!writablePrepare,"failed preparation read discards prior writable payload");
 var ownerRecoveryManager=SingletonMonoBehaviour<ConnectionManager>.Instance;int beforeOwnerRecovery=ownerRecoveryManager.Sent.Count;
 A(!RebirthBackpackLibraryClientOffers.RequestRecovery(inboxOwner.world,2)&&ownerRecoveryManager.Sent.Count==beforeOwnerRecovery,"recovery wrong owner sends nothing");
 NetPackageManager.Missing=true;A(!RebirthBackpackLibraryClientOffers.RequestRecovery(inboxOwner.world,1)&&ownerRecoveryManager.Sent.Count==beforeOwnerRecovery,"recovery missing mapping sends nothing");NetPackageManager.Missing=false;
 A(RebirthBackpackLibraryClientOffers.RequestRecovery(inboxOwner.world,1)&&ownerRecoveryManager.Sent.Count==beforeOwnerRecovery+1&&ownerRecoveryManager.Sent.Last() is NetPackageRebirthBackpackLibraryRecoveryRequest,"owner can request existing offer recovery");
 A(RebirthBackpackLibraryClientOffers.TryGetCurrentOffer(inboxOwner.world,1,out restored)&&restored.TransactionId==wireTransaction.ToString("N")&&!RebirthBackpackLibraryReservation.IsHeld(inboxOwner),"recovery request preserves known offer and acquires no custody");
 RebirthBackpackLibraryReservation.ResetSession();A(RebirthBackpackLibraryReservation.TryAcquire(inboxOwner,wireCreation,receipt),"settlement exact client reservation setup");
 A(!RebirthBackpackLibraryClientOffers.RequestSettlement(inboxOwner.world,1,wireTransaction),"unapplied owner cannot request settlement");
 A(!RebirthBackpackLibraryClientOffers.RequestOwnerCheckpoint(inboxOwner.world,1,wireTransaction)&&GameManager.Instance.Checkpoints==0,"unapplied receipt cannot schedule checkpoint");
 receipt.TryGetImages(out _,out _,out var clientBeforeSlot,out var clientAfterSlot);inboxOwner.bag=new Bag{Items=new[]{clientBeforeSlot.Clone()}};
 NetPackageManager.Missing=true;A(RebirthBackpackLibraryClientOffers.ApplyOffer(inboxOwner.world,1,wireTransaction)==RebirthBackpackLibraryOwnerResult.Pending&&inboxOwner.bag.Items[0].count==clientBeforeSlot.count,"client missing settlement mapping prevents debit");NetPackageManager.Missing=false;
 A(RebirthBackpackLibraryClientOffers.ApplyOffer(inboxOwner.world,2,wireTransaction)==RebirthBackpackLibraryOwnerResult.Pending,"client application wrong owner refused");
 A(RebirthBackpackLibraryClientOffers.ApplyOffer(inboxOwner.world,1,wireTransaction)==RebirthBackpackLibraryOwnerResult.Applied&&inboxOwner.bag.Items[0].count==clientAfterSlot.count&&RebirthBackpackLibraryReservation.IsHeld(inboxOwner),"authenticated client application holds reservation awaiting settlement");
 A(RebirthBackpackLibraryClientOffers.ApplyOffer(inboxOwner.world,1,wireTransaction)==RebirthBackpackLibraryOwnerResult.Applied&&inboxOwner.bag.Items[0].count==clientAfterSlot.count,"client application duplicate cannot repeat debit");
 NetPackageManager.Missing=true;A(!RebirthBackpackLibraryClientOffers.RequestOwnerCheckpoint(inboxOwner.world,1,wireTransaction)&&GameManager.Instance.Checkpoints==0,"missing player-data mapping schedules nothing");NetPackageManager.Missing=false;
 A(!RebirthBackpackLibraryClientOffers.RequestOwnerCheckpoint(inboxOwner.world,2,wireTransaction),"wrong owner cannot schedule checkpoint");
 A(RebirthBackpackLibraryClientOffers.RequestOwnerCheckpoint(inboxOwner.world,1,wireTransaction)&&GameManager.Instance.Checkpoints==1&&RebirthBackpackLibraryReservation.IsHeld(inboxOwner),"applied receipt schedules native full-data checkpoint without releasing custody"); A(RebirthBackpackLibraryClientOffers.RequestSettlement(inboxOwner.world,1,wireTransaction),"applied owner requests fresh saved settlement");
 var settlementRequest=(NetPackageRebirthBackpackLibrarySettleRequest)SingletonMonoBehaviour<ConnectionManager>.Instance.Sent.Last();
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))settlementRequest.write(writer);A(stream.Length==39&&stream.Length==settlementRequest.GetLength()&&settlementRequest.PackageDirection==NetPackageDirection.ToServer,"settlement request exact framing/direction");}
 RebirthBackpackLibraryOwnerResult advanceResult;int beforeAdvanceCount=inboxOwner.bag.Items[0].count;int beforeAdvanceCheckpoint=GameManager.Instance.Checkpoints;
 A(RebirthBackpackLibraryClientOffers.AdvanceCurrentOffer(inboxOwner.world,1,out advanceResult)&&advanceResult==RebirthBackpackLibraryOwnerResult.Applied&&GameManager.Instance.Checkpoints==beforeAdvanceCheckpoint+1&&inboxOwner.bag.Items[0].count==beforeAdvanceCount&&RebirthBackpackLibraryReservation.IsHeld(inboxOwner),"current-offer interaction schedules checkpoint and settlement without duplicate debit or early release");
 A(!RebirthBackpackLibraryClientOffers.AdvanceCurrentOffer(inboxOwner.world,2,out advanceResult)&&advanceResult==RebirthBackpackLibraryOwnerResult.Pending,"wrong owner cannot advance current offer");
 A(!RebirthBackpackLibraryClientOffers.ReceiveSettlement(inboxOwner.world,2,wireCreation,wireTransaction,6,true)&&RebirthBackpackLibraryReservation.IsHeld(inboxOwner),"wrong target cannot release reservation");
 A(!RebirthBackpackLibraryClientOffers.ReceiveSettlement(inboxOwner.world,1,wireCreation,wireTransaction,5,true)&&!RebirthBackpackLibraryClientOffers.ReceiveSettlement(inboxOwner.world,1,wireCreation,wireTransaction,6,false)&&RebirthBackpackLibraryReservation.IsHeld(inboxOwner),"wrong revision/outcome retains reservation");
 var settledPacket=new NetPackageRebirthBackpackLibrarySettled().Setup(1,RebirthBackpackLibrarySettlement.Create(receipt,true));
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))settledPacket.write(writer);A(stream.Length==47&&stream.Length==settledPacket.GetLength()&&settledPacket.PackageDirection==NetPackageDirection.ToClient,"settled response exact framing/direction");stream.Position=2;var receivedSettlement=new NetPackageRebirthBackpackLibrarySettled();using(var reader=new PooledBinaryReader(stream))receivedSettlement.read(reader);receivedSettlement.ProcessPackage(inboxOwner.world,GameManager.Instance);}
 A(!RebirthBackpackLibraryReservation.IsHeld(inboxOwner)&&!RebirthBackpackLibraryClientOffers.TryGetOffer(inboxOwner.world,1,wireTransaction,out restored),"native settled reply releases exact reservation and offer transport");
 A(!RebirthBackpackLibraryClientOffers.ReceiveSettlement(inboxOwner.world,1,wireCreation,wireTransaction,6,true),"settlement duplicate cannot release another operation");
 A(!RebirthBackpackLibraryClientOffers.TryGetCurrentOffer(inboxOwner.world,1,out restored),"settled offer is no longer discoverable");
 RebirthBackpackLibrarySettlement confirmedOutcome;
 A(RebirthBackpackLibraryClientOffers.TryGetSettlement(inboxOwner.world,1,wireTransaction,out confirmedOutcome)&&confirmedOutcome.Applied&&confirmedOutcome.GearRevision==6,"exact confirmed outcome remains available after offer retirement");
 A(!RebirthBackpackLibraryClientOffers.TryGetSettlement(inboxOwner.world,2,wireTransaction,out confirmedOutcome)&&confirmedOutcome==null,"wrong owner cannot query completed outcome");
 A(!RebirthBackpackLibraryClientOffers.TryGetSettlement(inboxOwner.world,1,Guid.NewGuid(),out confirmedOutcome)&&confirmedOutcome==null,"unrelated transaction cannot satisfy continuation");
 RebirthBackpackLibraryClientViews.Reset();var viewManager=SingletonMonoBehaviour<ConnectionManager>.Instance;
 A(!RebirthBackpackLibraryClientViews.Request(inboxOwner.world,2),"view wrong owner request refused");
 NetPackageManager.MissingType=typeof(NetPackageRebirthBackpackLibraryView);A(!RebirthBackpackLibraryClientViews.Request(inboxOwner.world,1),"view missing reply mapping refuses before request admission");NetPackageManager.MissingType=null;
A(RebirthBackpackLibraryClientViews.Request(inboxOwner.world,1),"view request uses native owner session");
 A(!RebirthBackpackLibraryClientViews.Request(inboxOwner.world,1),"outstanding remote view refresh coalesces without replacing reply token");
 var sentViewRequest=(NetPackageRebirthBackpackLibraryViewRequest)viewManager.Sent.Last();Guid transportToken;
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))sentViewRequest.write(writer);A(stream.Length==sentViewRequest.GetLength()&&sentViewRequest.GetLength()==38&&sentViewRequest.PackageDirection==NetPackageDirection.ToServer,"view request exact fixed native framing");stream.Position=6;using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){A(new Guid(reader.ReadBytes(16))==wireCreation,"view request creation wire scope");transportToken=new Guid(reader.ReadBytes(16));}}
 A(RebirthBackpackLibraryViewResponse.TryCreate(wireCreation,transportToken,5,RebirthBackpackLibraryViewStatus.Ready,libraryView,out displayResponse),"view transport response setup");
 var viewPacket=new NetPackageRebirthBackpackLibraryView().Setup(1,displayResponse);var receivedView=new NetPackageRebirthBackpackLibraryView();
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))viewPacket.write(writer);A(stream.Length==viewPacket.GetLength()&&viewPacket.PackageDirection==NetPackageDirection.ToClient,"view response exact native length/direction");stream.Position=2;using(var reader=new PooledBinaryReader(stream))receivedView.read(reader);A(stream.Position==stream.Length,"view packet body fully consumed");receivedView.ProcessPackage(inboxOwner.world,GameManager.Instance);}
 A(RebirthBackpackLibraryClientViews.TryGet(inboxOwner.world,1,out decodedView)&&decodedView.GearRevision==5,"view native delivery fills owner cache");
 int beforePrepareRequests=viewManager.Sent.Count;A(!RebirthBackpackLibraryClientOffers.RequestPrepare(inboxOwner.world,2,true,0,0,1,true),"preparation wrong owner refused");A(!RebirthBackpackLibraryClientOffers.RequestPrepare(inboxOwner.world,1,true,0,0,0,true),"preparation zero quantity refused");A(!RebirthBackpackLibraryClientOffers.RequestPrepare(inboxOwner.world,1,true,0,decodedView.Capacity,1,true),"preparation out of library capacity refused");A(!RebirthBackpackLibraryClientOffers.RequestPrepare(inboxOwner.world,1,true,0,0,1,true)&&viewManager.Sent.Count==beforePrepareRequests,"pending display cannot admit another preparation");
 A(!RebirthBackpackLibraryClientViews.Receive(inboxOwner.world,1,displayResponse)&&!RebirthBackpackLibraryClientViews.Receive(inboxOwner.world,2,displayResponse),"view duplicate/wrong target refused");
 A(RebirthBackpackLibraryView.TryCreate(wireCreation,5,viewAfter,false,out libraryView),"idle preparation view setup");
 A(RebirthBackpackLibraryClientViews.Request(inboxOwner.world,1),"idle preparation view request");
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))viewManager.Sent.Last().write(writer);stream.Position=22;using(var reader=new BinaryReader(stream,Encoding.UTF8,true))transportToken=new Guid(reader.ReadBytes(16));}
 A(RebirthBackpackLibraryViewResponse.TryCreate(wireCreation,transportToken,5,RebirthBackpackLibraryViewStatus.Ready,libraryView,out displayResponse)&&RebirthBackpackLibraryClientViews.Receive(inboxOwner.world,1,displayResponse),"authenticated idle view accepted");
 int inventoryBeforeRequest=inboxOwner.bag.Items[0].count;beforePrepareRequests=viewManager.Sent.Count;
 A(RebirthBackpackLibraryClientOffers.RequestPrepare(inboxOwner.world,1,true,0,0,1,true)&&viewManager.Sent.Count==beforePrepareRequests+1,"valid owner preparation queues one request");
 A(inboxOwner.bag.Items[0].count==inventoryBeforeRequest&&!RebirthBackpackLibraryReservation.IsHeld(inboxOwner),"request admission does not debit or acquire custody");
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))viewManager.Sent.Last().write(writer);stream.Position=2;using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){A(reader.ReadInt32()==1&&new Guid(reader.ReadBytes(16))==wireCreation&&reader.ReadInt64()==5&&reader.ReadBoolean()&&reader.ReadInt32()==0&&reader.ReadInt32()==0&&reader.ReadInt32()==1&&reader.ReadBoolean(),"owner request preserves entity creation revision slots quantity and deposit");}}
 beforePrepareRequests=viewManager.Sent.Count;NetPackageManager.Missing=true;A(!RebirthBackpackLibraryClientOffers.RequestPrepare(inboxOwner.world,1,true,0,0,1,true)&&viewManager.Sent.Count==beforePrepareRequests,"missing request mapping sends nothing");NetPackageManager.Missing=false;
 inboxOwner.PlayerUI.xui.DragAndDropWindow.CurrentStack=S(1);A(!RebirthBackpackLibraryClientOffers.RequestPrepare(inboxOwner.world,1,true,0,0,1,true)&&viewManager.Sent.Count==beforePrepareRequests,"busy cursor sends no preparation");inboxOwner.PlayerUI.xui.DragAndDropWindow.CurrentStack=ItemStack.Empty.Clone();
 viewManager.connectionToServer[0]=new TestConnection();A(!RebirthBackpackLibraryClientViews.TryGet(inboxOwner.world,1,out decodedView),"view reconnect cannot reuse stale cache");
 A(RebirthBackpackLibraryClientViews.Request(inboxOwner.world,1)&&!RebirthBackpackLibraryClientViews.TryGet(inboxOwner.world,1,out decodedView),"view new session starts empty");
 NetPackageManager.Missing=true;A(!RebirthBackpackLibraryClientViews.Request(inboxOwner.world,1),"view missing mapping fails before request send");NetPackageManager.Missing=false;
 RebirthBackpackLibraryClientViews.Reset();
 RebirthBackpackLibraryClientOffers.Reset();A(!RebirthBackpackLibraryClientOffers.TryGetOffer(inboxOwner.world,1,wireTransaction,out restored),"client reset releases transport");GameManager.Instance.World=priorWorld;inboxOwner.world.Remote=false;
 var wrongAssembly=new RebirthBackpackLibraryOfferAssembly(Guid.NewGuid(),Guid.Parse(receipt.TransactionId),wireBytes.Length);
 for(int ci=0;ci<chunkCount;ci++){int offset=ci*RebirthBackpackLibraryOfferAssembly.ChunkBytes;var part=new byte[Math.Min(RebirthBackpackLibraryOfferAssembly.ChunkBytes,wireBytes.Length-offset)];Buffer.BlockCopy(wireBytes,offset,part,0,part.Length);A(wrongAssembly.TryAdd(ci,part),"wrong-scope transport accepts bytes only");}
 A(!wrongAssembly.TryFinish(out restored)&&restored==null,"assembled wrong character refused");
 A(!wrongAssembly.TryFinish(out restored)&&restored==null,"invalid completed offer remains refused");
 A(!RebirthBackpackLibraryWireCodec.TryDecode(new byte[RebirthBackpackLibraryWireCodec.MaxBytes+1],out restored)&&restored==null,"oversized wire refused");
 A(!RebirthBackpackLibraryWireCodec.TryDecode(new byte[]{255,255},out restored),"invalid UTF8 refused");
 A(!RebirthBackpackLibraryWireCodec.TryDecode(Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY y SYSTEM 'file:///not-read'>]><x>&y;</x>"),out restored),"wire DTD refused");
 A(!RebirthBackpackLibraryWireCodec.TryDecode(Encoding.UTF8.GetBytes(receipt.ToXml().ToString()+"<extra/>"),out restored),"multiple wire roots refused");
 var wireTamper=receipt.ToXml();wireTamper.SetAttributeValue("quantity",3);A(!RebirthBackpackLibraryWireCodec.TryDecode(Encoding.UTF8.GetBytes(wireTamper.ToString()),out restored),"wire invalid plan refused");
 A(!RebirthBackpackLibraryReceipt.TryCreate(Guid.Empty,Guid.NewGuid(),5,true,0,0,2,true,receiptPack,receiptSource,out restored),"empty transaction refused");
 A(receipt.TryGetImages(out var beforePack,out var afterPack,out var beforeSlot,out var afterSlot)&&afterSlot.count==1,"fresh images");
 afterSlot.count=99;A(receipt.TryGetImages(out beforePack,out afterPack,out beforeSlot,out afterSlot)&&afterSlot.count==1,"image isolation");
 A(RebirthBackpackLibraryReceipt.TryCreate(Guid.NewGuid(),Guid.NewGuid(),6,true,0,0,2,false,afterPack,ItemStack.Empty.Clone(),out restored),"withdraw receipt");
 var support=new XElement("support");RebirthBackpackLibraryPhase phase;
 A(RebirthBackpackLibraryPersistence.TryRead(support,5,out restored,out phase)&&restored==null&&phase==RebirthBackpackLibraryPhase.Prepared,"old save default");
 support.Add(RebirthBackpackLibraryPersistence.Write(5,receipt,RebirthBackpackLibraryPhase.Prepared));
 A(RebirthBackpackLibraryPersistence.TryRead(support,5,out restored,out phase)&&restored.TransactionId==receipt.TransactionId,"pending reload");
 A(!RebirthBackpackLibraryPersistence.TryRead(support,6,out restored,out phase),"uncommitted revision mismatch");
 support.ReplaceNodes(RebirthBackpackLibraryPersistence.Write(6,receipt,RebirthBackpackLibraryPhase.BackpackCommitted));
 A(RebirthBackpackLibraryPersistence.TryRead(support,6,out restored,out phase)&&phase==RebirthBackpackLibraryPhase.BackpackCommitted,"committed revision reload");
 support.Add(new XElement(support.Element("libraryTransfers")));A(!RebirthBackpackLibraryPersistence.TryRead(support,6,out restored,out phase),"duplicate journal refused");
 support.ReplaceNodes(new XElement("libraryTransfers",new XAttribute("version",1),new XAttribute("phase",1)));A(!RebirthBackpackLibraryPersistence.TryRead(support,5,out restored,out phase),"orphan phase refused");
 support.Element("libraryTransfers").SetAttributeValue("phase",99);A(!RebirthBackpackLibraryPersistence.TryRead(support,5,out restored,out phase),"unknown phase refused");
 var state=new RebirthWorldSupportState{GearRevision=5};state.EquippedGearBySlot["backpack"]="pack";
 receipt.TryGetImages(out beforePack,out afterPack,out beforeSlot,out afterSlot);
 string initialData=RebirthNativeItemCodec.Encode(beforePack);state.EquippedGearItemDataBySlot["backpack"]=initialData;
 string creation=receipt.CreationId,transaction=receipt.TransactionId;
 A(!RebirthBackpackLibraryJournal.Prepare(state,receipt,creation,()=>false)&&state.PendingLibraryTransfer==null,"prepare save rollback");
 A(!RebirthBackpackLibraryJournal.Prepare(state,receipt,Guid.NewGuid().ToString("N"),()=>{throw new Exception("should not save");}),"wrong character");
 state.PendingMusicTransfer=new object();A(!RebirthBackpackLibraryJournal.Prepare(state,receipt,creation,()=>true),"competing music custody");state.PendingMusicTransfer=null;
 A(RebirthBackpackLibraryJournal.Prepare(state,receipt,creation,()=>true),"prepare journal");
 A(RebirthBackpackLibraryJournal.Prepare(state,receipt,creation,()=>true),"same intent retry");
 A(!RebirthBackpackLibraryJournal.CommitBackpack(state,transaction,creation,()=>true),"cannot commit before owner evidence");
 A(!RebirthBackpackLibraryJournal.MarkOwnerApplied(state,transaction,creation,r=>false,()=>true),"missing durable evidence");
 A(!RebirthBackpackLibraryJournal.MarkOwnerApplied(state,transaction,creation,r=>true,()=>false)&&state.LibraryTransferPhase==RebirthBackpackLibraryPhase.Prepared,"owner phase rollback");
 A(RebirthBackpackLibraryJournal.MarkOwnerApplied(state,transaction,creation,r=>true,()=>true),"saved owner applied");
 A(!RebirthBackpackLibraryJournal.CommitBackpack(state,transaction,creation,()=>{throw new Exception("save failure");})&&state.GearRevision==5&&state.EquippedGearItemDataBySlot["backpack"]==initialData,"pack save exception rollback");
 A(RebirthBackpackLibraryJournal.CommitBackpack(state,transaction,creation,()=>true)&&state.GearRevision==6,"pack commit");
 A(RebirthBackpackLibraryJournal.CommitBackpack(state,transaction,creation,()=>true)&&state.GearRevision==6,"duplicate commit no new revision");
 A(!RebirthBackpackLibraryJournal.Finish(state,transaction,creation,r=>false,()=>true)&&state.PendingLibraryTransfer!=null,"finish requires saved owner");
 A(!RebirthBackpackLibraryJournal.Finish(state,transaction,creation,r=>true,()=>false)&&state.LibraryTransferPhase==RebirthBackpackLibraryPhase.BackpackCommitted&&state.PendingLibraryTransfer!=null&&state.LastLibrarySettlement==null,"finish failure retains custody and no terminal outcome");
 A(RebirthBackpackLibraryJournal.Finish(state,transaction,creation,r=>true,()=>true)&&state.PendingLibraryTransfer==null,"settled receipt");
 A(!RebirthBackpackLibraryJournal.Finish(state,transaction,creation,r=>true,()=>true),"settled replay rejected");
 A(state.LastLibrarySettlement!=null&&state.LastLibrarySettlement.Applied&&state.LastLibrarySettlement.GearRevision==6,"finish retained durable applied terminal receipt");
 var settledXml=new XElement("support",state.LastLibrarySettlement.Write());RebirthBackpackLibrarySettlement settledCopy;
 A(RebirthBackpackLibrarySettlement.TryRead(settledXml,6,out settledCopy)&&settledCopy.TransactionId==transaction&&settledCopy.Applied,"settlement XML roundtrip");
 A(!RebirthBackpackLibrarySettlement.TryRead(settledXml,5,out settledCopy),"settlement cannot exceed state revision");
 settledXml.Add(state.LastLibrarySettlement.Write());A(!RebirthBackpackLibrarySettlement.TryRead(settledXml,6,out settledCopy),"duplicate settlement refused");
 A(RebirthBackpackLibrarySettlement.TryRead(new XElement("support"),0,out settledCopy)&&settledCopy==null,"old saves need no terminal receipt");
 state=new RebirthWorldSupportState{GearRevision=5};state.EquippedGearBySlot["backpack"]="pack";state.EquippedGearItemDataBySlot["backpack"]=initialData;
 A(RebirthBackpackLibraryJournal.Prepare(state,receipt,creation,()=>true),"rejection setup");
 A(!RebirthBackpackLibraryJournal.CancelRejected(state,transaction,creation,r=>false,()=>true),"timeout not rejection");
 A(!RebirthBackpackLibraryJournal.CancelRejected(state,transaction,creation,r=>true,()=>false)&&state.PendingLibraryTransfer!=null&&state.GearRevision==5&&state.LastLibrarySettlement==null,"cancel save failure retains intent without terminal outcome");
 A(RebirthBackpackLibraryJournal.CancelRejected(state,transaction,creation,r=>true,()=>true)&&state.GearRevision==6&&state.PendingLibraryTransfer==null,"saved rejection releases custody");
 A(state.LastLibrarySettlement!=null&&!state.LastLibrarySettlement.Applied&&state.LastLibrarySettlement.GearRevision==6,"durable rejection terminal outcome retained");
 var legacyOwner=new EntityPlayerLocal{bag=new Slots{Items=new[]{receiptSource.Clone()}}};legacyOwner.world.Player=legacyOwner;GameManager.Instance=new GameManager{World=legacyOwner.world};
 A(!RebirthBackpackLibraryReservation.TryAcquire(legacyOwner,"legacy-"+new string('b',64),migratedReceipt)&&RebirthBackpackLibraryReservation.TryAcquire(legacyOwner,migratedCreation,migratedReceipt),"legacy exact reservation scope");
 A(RebirthBackpackLibraryOwnerTransfer.Apply(legacyOwner,"legacy-"+new string('b',64),migratedReceipt)==RebirthBackpackLibraryOwnerResult.Pending&&legacyOwner.bag.Writes==0,"legacy wrong owner cannot mutate");
 A(RebirthBackpackLibraryOwnerTransfer.Apply(legacyOwner,migratedCreation,migratedReceipt)==RebirthBackpackLibraryOwnerResult.Applied&&legacyOwner.bag.Writes==1,"legacy reserved owner application");
 A(RebirthBackpackLibraryOwnerTransfer.Apply(legacyOwner,migratedCreation,migratedReceipt)==RebirthBackpackLibraryOwnerResult.Applied&&legacyOwner.bag.Writes==1,"legacy owner replay no second write");
 A(!RebirthBackpackLibraryReservation.ReleaseSettled(legacyOwner,migratedCreation,migratedReceipt,r=>false)&&RebirthBackpackLibraryReservation.ReleaseSettled(legacyOwner,migratedCreation,migratedReceipt,r=>true),"legacy settlement verification required");
 var legacyOwnerSession=new object();var legacyOwnerInbox=new RebirthBackpackLibraryOwnerInbox(legacyOwner,migratedCreation,legacyOwnerSession);
 for(int offset=0,index=0;offset<migratedBytes.Length;offset+=RebirthBackpackLibraryOfferAssembly.ChunkBytes,index++){
 var part=migratedBytes.Skip(offset).Take(RebirthBackpackLibraryOfferAssembly.ChunkBytes).ToArray();RebirthBackpackLibraryOfferChunk chunk;
 A(RebirthBackpackLibraryOfferChunk.TryCreate(migratedCreation,Guid.Parse(migratedReceipt.TransactionId),migratedBytes.Length,index,part,out chunk)&&!legacyOwnerInbox.Receive(legacyOwner,migratedCreation,new object(),chunk)&&legacyOwnerInbox.Receive(legacyOwner,migratedCreation,legacyOwnerSession,chunk),"legacy owner inbox authenticated session");}
 A(legacyOwnerInbox.TryGetOffer(legacyOwner,migratedCreation,legacyOwnerSession,Guid.Parse(migratedReceipt.TransactionId),out migratedDecoded)&&migratedDecoded.CreationId==migratedCreation,"legacy owner inbox complete offer");
 var player=new EntityPlayerLocal{bag=new Slots{Items=new[]{S(3)},LockedSlots=new bool[1]}};var ownerCreation=Guid.Parse(receipt.CreationId);
 A(RebirthBackpackLibraryOwnerTransfer.Apply(player,ownerCreation,receipt)==RebirthBackpackLibraryOwnerResult.Pending&&player.bag.Writes==0&&player.Buffs.Vars.Count==0,"unreserved owner application refused");
 A(ApplyReserved(player,Guid.NewGuid(),receipt)==RebirthBackpackLibraryOwnerResult.Pending&&player.bag.Writes==0,"owner creation gate");
 player.bag.LockedSlots[0]=true;A(ApplyReserved(player,ownerCreation,receipt)==RebirthBackpackLibraryOwnerResult.Pending&&player.Buffs.Vars.Count==0,"locked native slot pending");player.bag.LockedSlots[0]=false;
 A(ApplyReserved(player,ownerCreation,receipt)==RebirthBackpackLibraryOwnerResult.Applied&&player.bag.Items[0].count==1&&player.bag.Writes==1,"exact owner debit");
 A(ApplyReserved(player,ownerCreation,receipt)==RebirthBackpackLibraryOwnerResult.Applied&&player.bag.Writes==1,"owner replay no debit");
 player=new EntityPlayerLocal{bag=new Slots{Items=new[]{S(3,"different")}}};A(ApplyReserved(player,ownerCreation,receipt)==RebirthBackpackLibraryOwnerResult.Rejected&&player.bag.Writes==0,"wrong item payload rejected");
 player=new EntityPlayerLocal{bag=new Slots{Items=new[]{S(3)},ThrowAfter=true}};
 A(ApplyReserved(player,ownerCreation,receipt)==RebirthBackpackLibraryOwnerResult.Indeterminate&&player.bag.Items[0].count==1&&player.Buffs.GetCustomVar(RebirthBackpackLibraryOwnerTransfer.ReceiptKey(receipt))==2,"setter exception retains applying receipt");
 A(ApplyReserved(player,ownerCreation,receipt)==RebirthBackpackLibraryOwnerResult.Indeterminate&&player.bag.Writes==1,"uncertain owner replay blocked");
 A(!RebirthBackpackLibraryOwnerTransfer.RecoverApplied(player,Guid.NewGuid(),receipt),"recovery wrong creation");
 player.bag.Items[0]=S(1,"wrong");A(!RebirthBackpackLibraryOwnerTransfer.RecoverApplied(player,ownerCreation,receipt),"recovery wrong payload retains uncertain receipt");
 player.bag.Items[0]=S(3);A(!RebirthBackpackLibraryOwnerTransfer.RecoverApplied(player,ownerCreation,receipt),"unchanged before image not applied");
 player.bag.Items[0]=S(1);A(RebirthBackpackLibraryOwnerTransfer.RecoverApplied(player,ownerCreation,receipt)&&player.bag.Writes==1,"exact after image recovery without setter");
 A(!RebirthBackpackLibraryOwnerTransfer.RecoverApplied(player,ownerCreation,receipt),"settled recovery not repeated");
 A(ApplyReserved(player,ownerCreation,receipt)==RebirthBackpackLibraryOwnerResult.Applied&&player.bag.Writes==1,"recovered replay no mutation");
 RebirthBackpackLibraryReceipt withdraw;receipt.TryGetImages(out beforePack,out afterPack,out beforeSlot,out afterSlot);
 A(RebirthBackpackLibraryReceipt.TryCreate(Guid.NewGuid(),ownerCreation,5,false,0,0,2,false,afterPack,ItemStack.Empty.Clone(),out withdraw),"belt credit offer");
 player=new EntityPlayerLocal{inventory=new Slots{Items=new[]{ItemStack.Empty.Clone()}}};
 A(ApplyReserved(player,ownerCreation,withdraw)==RebirthBackpackLibraryOwnerResult.Applied&&player.inventory.Items[0].count==2,"belt credit exact slot");
 var evidenceKey=RebirthBackpackLibraryOwnerTransfer.ReceiptKey(receipt);
 var bytes=Evidence(evidenceKey,1);
 A(RebirthBackpackLibrarySavedEvidence.ContainsReceipt(bytes,receipt,true),"saved applied receipt");
 A(!RebirthBackpackLibrarySavedEvidence.ContainsReceipt(Evidence(evidenceKey,2),receipt,true),"applying not durable applied");
 A(!RebirthBackpackLibrarySavedEvidence.ContainsReceipt(Evidence(evidenceKey,1,true),receipt,true),"duplicate saved receipt refused");
 A(!RebirthBackpackLibrarySavedEvidence.ContainsReceipt(Evidence(evidenceKey+"wrong",1),receipt,true),"saved scope mismatch");
 A(!RebirthBackpackLibrarySavedEvidence.ContainsReceipt(bytes.Take(bytes.Length-1).ToArray(),receipt,true),"truncated saved data");
 A(!RebirthBackpackLibrarySavedEvidence.ContainsReceipt(bytes.Concat(new byte[]{0}).ToArray(),receipt,true),"trailing saved data");
 receipt.TryGetImages(out beforePack,out afterPack,out beforeSlot,out afterSlot);
 var saved=new PlayerDataFile{bLoaded=true,buffData=new MemoryStream(bytes),bag=new Slots{Items=new[]{afterSlot.Clone()}}};
 A(RebirthBackpackLibrarySavedEvidence.Matches(saved,receipt,true),"saved exact after image");
 saved.bag.Items[0]=S(2);A(!RebirthBackpackLibrarySavedEvidence.Matches(saved,receipt,true),"saved quantity mismatch");
 saved.bag.Items[0]=S(1,"wrong");A(!RebirthBackpackLibrarySavedEvidence.Matches(saved,receipt,true),"saved payload mismatch");
 saved.buffData=new MemoryStream(Evidence(evidenceKey,-1));A(RebirthBackpackLibrarySavedEvidence.Matches(saved,receipt,false),"saved rejection needs no after image");
 saved.bLoaded=false;A(!RebirthBackpackLibrarySavedEvidence.Matches(saved,receipt,false),"unloaded file refused");
 PlayerDataFile.Loaded=null;A(!RebirthBackpackLibrarySavedEvidence.TryVerifyFromDisk(new ClientInfo(),receipt,true),"missing saved file");
 saved.bLoaded=true;saved.buffData=new MemoryStream(bytes);saved.bag.Items[0]=afterSlot.Clone();PlayerDataFile.Loaded=saved;
 A(RebirthBackpackLibrarySavedEvidence.TryVerifyFromDisk(new ClientInfo(),receipt,true),"fresh load adapter");
 A(!RebirthBackpackLibrarySavedEvidence.TryVerifyFromDisk(new ClientInfo{InternalId=null},receipt,true),"missing owner refused");
 var serverPlayer=new EntityPlayer();serverPlayer.world.Player=serverPlayer;GameManager.Instance=new GameManager{World=serverPlayer.world};var sender=new ClientInfo{latestPlayerData=new PlayerDataFile{bag=new Slots{Items=new[]{S(3)}},inventory=new[]{ItemStack.Empty.Clone()}}};
 state=new RebirthWorldSupportState{GearRevision=5};state.EquippedGearBySlot["backpack"]="pack";state.EquippedGearItemDataBySlot["backpack"]=initialData;
 RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord{Support=state,Origin=new Origin{CreationId=creation}};
 var priorSellViewPack=state.EquippedGearItemDataBySlot["backpack"];state.EquippedGearItemDataBySlot["backpack"]=RebirthNativeItemCodec.Encode(saleNext);
 var sellViewSaveCount=RebirthWorldCharacterRepository.Saves;
 A(RebirthBackpackLibraryServer.GetSellStashViewStatus(serverPlayer,sender,creation,out var authoritativeSellView,out var authoritativeSellRevision)==RebirthBackpackLibraryViewStatus.Ready&&authoritativeSellView.OccupiedSlots==1&&authoritativeSellRevision==5,"authenticated sale display reads equipped backpack section");
 A(RebirthWorldCharacterRepository.Saves==sellViewSaveCount&&state.PendingLibraryTransfer==null,"sale display never saves or prepares custody");
 sender.entityId=2;A(RebirthBackpackLibraryServer.GetSellStashViewStatus(serverPlayer,sender,creation,out authoritativeSellView,out authoritativeSellRevision)==RebirthBackpackLibraryViewStatus.Unavailable&&authoritativeSellView==null,"sale display refuses spoofed sender");sender.entityId=1;
 A(RebirthBackpackLibraryServer.GetSellStashViewStatus(serverPlayer,sender,Guid.NewGuid().ToString("N"),out authoritativeSellView,out authoritativeSellRevision)==RebirthBackpackLibraryViewStatus.Unavailable,"sale display refuses wrong character");
 state.EquippedGearBySlot.Remove("backpack");state.EquippedGearItemDataBySlot.Remove("backpack");
 A(RebirthBackpackLibraryServer.GetSellStashViewStatus(serverPlayer,sender,creation,out authoritativeSellView,out authoritativeSellRevision)==RebirthBackpackLibraryViewStatus.NoBackpack&&authoritativeSellView==null&&authoritativeSellRevision==5,"sale display distinguishes absent backpack");
 state.EquippedGearBySlot["backpack"]="pack";state.EquippedGearItemDataBySlot["backpack"]=priorSellViewPack;
 var salePreparePacket=new NetPackageRebirthBackpackSellStashPrepareRequest().Setup(serverPlayer.entityId,creation,4,true,0,0,2,true);salePreparePacket.Sender=sender;
 salePreparePacket.ProcessPackage(serverPlayer.world,GameManager.Instance);A(state.PendingLibraryTransfer==null,"sale prepare packet refuses stale gear revision");
 salePreparePacket.Setup(serverPlayer.entityId,creation,5,true,0,0,2,true);sender.entityId=2;salePreparePacket.ProcessPackage(serverPlayer.world,GameManager.Instance);A(state.PendingLibraryTransfer==null,"sale prepare packet refuses spoofed sender");sender.entityId=1;
 NetPackageManager.Missing=true;salePreparePacket.ProcessPackage(serverPlayer.world,GameManager.Instance);A(state.PendingLibraryTransfer==null,"sale prepare mapping failure creates no journal intent");NetPackageManager.Missing=false;
 using(var memory=new MemoryStream()){
  using(var writer=new PooledBinaryWriter(memory))salePreparePacket.write(writer);A(memory.Length==salePreparePacket.GetLength(),"sale preparation packet exact native body length");
  memory.Position=2;var readSalePacket=new NetPackageRebirthBackpackSellStashPrepareRequest();using(var reader=new PooledBinaryReader(memory)){readSalePacket.read(reader);A(memory.Position==memory.Length,"sale preparation reader consumes exact body");}
 }
 bool badSaleSlot=false;try{new NetPackageRebirthBackpackSellStashPrepareRequest().Setup(1,creation,5,true,0,80,1,true);}catch(ArgumentException){badSaleSlot=true;}A(badSaleSlot,"sale preparation refuses slot beyond80-cell policy bound");
 RebirthBackpackLibraryReceipt serverOffer;
 var currentWorld=serverPlayer.world;
 GameManager.Instance.World=new PlayerWorld();A(!RebirthBackpackLibraryServer.Prepare(serverPlayer,sender,creation,5,true,0,0,2,true,out serverOffer),"old world owner refused");GameManager.Instance.World=currentWorld;
 currentWorld.Player=new EntityPlayer();A(!RebirthBackpackLibraryServer.Prepare(serverPlayer,sender,creation,5,true,0,0,2,true,out serverOffer),"replacement entity same ID refused");currentWorld.Player=serverPlayer;
 sender.entityId=2;A(!RebirthBackpackLibraryServer.Prepare(serverPlayer,sender,creation,5,true,0,0,2,true,out serverOffer),"server sender mismatch");sender.entityId=1;
 A(!RebirthBackpackLibraryServer.Prepare(serverPlayer,sender,Guid.NewGuid().ToString("N"),5,true,0,0,2,true,out serverOffer),"server creation mismatch");
 A(!RebirthBackpackLibraryServer.Prepare(serverPlayer,sender,creation,4,true,0,0,2,true,out serverOffer),"server stale revision");
 RebirthWorldCharacterRepository.FailAt=1;A(!RebirthBackpackLibraryServer.Prepare(serverPlayer,sender,creation,5,true,0,0,2,true,out serverOffer)&&state.PendingLibraryTransfer==null,"server failed prepare no offer");RebirthWorldCharacterRepository.FailAt=0;
 var preparationManager=new ConnectionManager{IsServer=true};SingletonMonoBehaviour<ConnectionManager>.Instance=preparationManager;
 var serverPreparePacket=new NetPackageRebirthBackpackLibraryPrepareRequest().Setup(serverPlayer.entityId,Guid.Parse(creation),4,true,0,0,2,true);serverPreparePacket.Sender=sender;
 serverPreparePacket.ProcessPackage(currentWorld,GameManager.Instance);A(state.PendingLibraryTransfer==null&&preparationManager.Sent.Count==0,"native initial request rejects stale revision");
 serverPreparePacket.Setup(serverPlayer.entityId,Guid.Parse(creation),5,true,0,0,2,true);sender.entityId=2;serverPreparePacket.ProcessPackage(currentWorld,GameManager.Instance);A(state.PendingLibraryTransfer==null,"native initial request rejects spoofed sender");sender.entityId=1;
 NetPackageManager.Missing=true;serverPreparePacket.ProcessPackage(currentWorld,GameManager.Instance);A(state.PendingLibraryTransfer==null,"native initial offer mapping failure prepares no intent");NetPackageManager.Missing=false;
 preparationManager.FailSendAt=1;serverPreparePacket.ProcessPackage(currentWorld,GameManager.Instance);
 A(state.PendingLibraryTransfer!=null&&state.LibraryTransferPhase==RebirthBackpackLibraryPhase.Prepared&&state.GearRevision==5&&sender.latestPlayerData.bag.Items[0].count==3,"initial request send failure retains saved intent without inventory debit");
 A(RebirthBackpackLibraryServer.GetRecoveryOffer(serverPlayer,sender,creation,out serverOffer)&&serverOffer!=null,"initial packet durable offer remains recoverable");preparationManager.FailSendAt=0;
 int beforeViewSaves=RebirthWorldCharacterRepository.Saves;RebirthBackpackLibraryView serverView;
 A(RebirthBackpackLibraryServer.GetView(serverPlayer,sender,creation,out serverView)&&serverView.CreationId==creation&&serverView.GearRevision==5&&serverView.TransferPending&&serverView.Capacity==1&&serverView.OccupiedSlots==0&&RebirthWorldCharacterRepository.Saves==beforeViewSaves,"authoritative pending library display reads current pack without saves or debits");
 sender.entityId=2;A(!RebirthBackpackLibraryServer.GetView(serverPlayer,sender,creation,out serverView)&&serverView==null,"display wrong owner refused");sender.entityId=1;
 var savedOrigin=RebirthWorldCharacterService.Record.Origin;RebirthWorldCharacterService.Record.Origin=null;A(!RebirthBackpackLibraryServer.GetView(serverPlayer,sender,creation,out serverView),"display missing character origin refuses without exception");RebirthWorldCharacterService.Record.Origin=savedOrigin;
 A(!RebirthBackpackLibraryServer.GetView(serverPlayer,sender,Guid.NewGuid().ToString("N"),out serverView),"display replaced character refused");
 var viewServerManager=new ConnectionManager{IsServer=true};SingletonMonoBehaviour<ConnectionManager>.Instance=viewServerManager;
 var serverViewRequest=new NetPackageRebirthBackpackLibraryViewRequest().Setup(1,Guid.Parse(creation),Guid.NewGuid());serverViewRequest.Sender=sender;
 int beforeViewRequestSaves=RebirthWorldCharacterRepository.Saves;var beforeViewRequestPending=state.PendingLibraryTransfer;
 NetPackageManager.MissingType=typeof(NetPackageRebirthBackpackLibraryView);serverViewRequest.ProcessPackage(serverPlayer.world,GameManager.Instance);A(viewServerManager.Sent.Count==0&&RebirthWorldCharacterRepository.Saves==beforeViewRequestSaves&&ReferenceEquals(state.PendingLibraryTransfer,beforeViewRequestPending),"server missing reply mapping refuses without send or journal change");NetPackageManager.MissingType=null;
 serverViewRequest.ProcessPackage(serverPlayer.world,GameManager.Instance);
 A(viewServerManager.Sent.Count==1&&viewServerManager.Sent[0] is NetPackageRebirthBackpackLibraryView&&viewServerManager.Targets[0]==1&&RebirthWorldCharacterRepository.Saves==beforeViewRequestSaves&&ReferenceEquals(state.PendingLibraryTransfer,beforeViewRequestPending),"native server view request sends only owner with no save/journal mutation");
 sender.entityId=2;serverViewRequest.ProcessPackage(serverPlayer.world,GameManager.Instance);A(viewServerManager.Sent.Count==1,"view sender mismatch cannot send owner data");sender.entityId=1;
 using(var stream=new MemoryStream(new byte[]{1,0,0,0,0}))using(var reader=new PooledBinaryReader(stream)){bool refused=false;try{serverViewRequest.read(reader);}catch(InvalidDataException){refused=true;}A(refused,"truncated pooled view request refused");}
 serverViewRequest.ProcessPackage(serverPlayer.world,GameManager.Instance);A(viewServerManager.Sent.Count==1,"failed pooled read cannot replay prior view request");
 var legacyPriorRecord=RebirthWorldCharacterService.Record;var legacyServerState=new RebirthWorldSupportState{GearRevision=5};
 legacyServerState.EquippedGearBySlot["backpack"]="pack";legacyServerState.EquippedGearItemDataBySlot["backpack"]=initialData;
 RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord{Support=legacyServerState,Origin=new Origin{CreationId=migratedCreation}};
 RebirthBackpackLibraryReceipt legacyServerOffer;RebirthBackpackLibraryView legacyServerView;
 A(RebirthBackpackLibraryServer.GetView(serverPlayer,sender,migratedCreation,out legacyServerView)&&legacyServerView.CreationId==migratedCreation,"legacy authoritative view");
 A(!RebirthBackpackLibraryServer.Prepare(serverPlayer,sender,"legacy-"+new string('b',64),5,true,0,0,2,true,out legacyServerOffer)&&legacyServerState.PendingLibraryTransfer==null,"legacy server wrong creation refused");
 A(RebirthBackpackLibraryServer.Prepare(serverPlayer,sender,migratedCreation,5,true,0,0,2,true,out legacyServerOffer)&&legacyServerOffer.CreationId==migratedCreation,"legacy authenticated server preparation");
 var legacyServerTransaction=legacyServerOffer.TransactionId;
 A(RebirthBackpackLibraryServer.GetRecoveryOffer(serverPlayer,sender,migratedCreation,out migratedDecoded)&&migratedDecoded.TransactionId==legacyServerTransaction,"legacy server recovery retains transaction");
 int legacySentBefore=viewServerManager.Sent.Count;A(RebirthBackpackLibraryRecoveryDelivery.TrySend(serverPlayer,sender,migratedCreation)&&viewServerManager.Sent.Count>legacySentBefore,"legacy recovery delivery emits chunks");
 A(!RebirthBackpackLibraryJournal.MarkOwnerApplied(legacyServerState,legacyServerTransaction,migratedCreation,r=>false,()=>true),"legacy journal still requires saved owner evidence");
 A(RebirthBackpackLibraryJournal.MarkOwnerApplied(legacyServerState,legacyServerTransaction,migratedCreation,r=>true,()=>true)&&RebirthBackpackLibraryJournal.CommitBackpack(legacyServerState,legacyServerTransaction,migratedCreation,()=>true)&&RebirthBackpackLibraryJournal.Finish(legacyServerState,legacyServerTransaction,migratedCreation,r=>true,()=>true),"legacy journal terminal phases");
 RebirthBackpackLibrarySettlement legacyServerSettlement;
 A(RebirthBackpackLibraryServer.TryGetSettlement(serverPlayer,sender,migratedCreation,legacyServerTransaction,out legacyServerSettlement)&&legacyServerSettlement.CreationId==migratedCreation&&legacyServerSettlement.Applied,"legacy server settlement lookup");
 var migratedViewPlayer=new EntityPlayerLocal();migratedViewPlayer.world.Player=migratedViewPlayer;
 var migratedPriorWorld=GameManager.Instance.World;var migratedPriorManager=SingletonMonoBehaviour<ConnectionManager>.Instance;var migratedPriorProjection=RebirthSurvivorClientState.Creation;
 GameManager.Instance.World=migratedViewPlayer.world;RebirthSurvivorClientState.Creation=migratedCreation;
 SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true};RebirthBackpackLibraryClientViews.Reset();
 A(RebirthBackpackLibraryClientViews.Request(migratedViewPlayer.world,migratedViewPlayer.entityId)&&RebirthBackpackLibraryClientViews.TryGet(migratedViewPlayer.world,migratedViewPlayer.entityId,out decodedView)&&decodedView.CreationId==migratedCreation,"legacy host facade authoritative view");
 migratedViewPlayer.world.Remote=true;var migratedRemoteManager=new ConnectionManager{connectionToServer=new INetConnection[]{new TestConnection()}};SingletonMonoBehaviour<ConnectionManager>.Instance=migratedRemoteManager;RebirthBackpackLibraryClientViews.Reset();
 A(RebirthBackpackLibraryClientViews.Request(migratedViewPlayer.world,migratedViewPlayer.entityId),"legacy remote view request");Guid migratedRemoteToken;
 var migratedRequestPacket=(NetPackageRebirthBackpackLibraryViewRequest)migratedRemoteManager.Sent.Last();
 using(var stream=new MemoryStream()){
 using(var writer=new PooledBinaryWriter(stream))migratedRequestPacket.write(writer);
 A(stream.Length==migratedRequestPacket.GetLength()&&stream.Length==111,"legacy request exact extended framing");stream.Position=6;
 using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){A(RebirthBackpackLibraryCreationWire.Read(reader)==migratedCreation,"legacy request complete creation identity");migratedRemoteToken=new Guid(reader.ReadBytes(16));}
 stream.Position=2;var readRequest=new NetPackageRebirthBackpackLibraryViewRequest();using(var reader=new PooledBinaryReader(stream))readRequest.read(reader);A(stream.Position==stream.Length,"legacy request consumes exact frame");
 }
 A(RebirthBackpackLibraryViewResponse.TryCreate(migratedCreation,migratedRemoteToken,legacyServerView.GearRevision,RebirthBackpackLibraryViewStatus.Ready,legacyServerView,out legacyResponse)&&RebirthBackpackLibraryClientViews.Receive(migratedViewPlayer.world,migratedViewPlayer.entityId,legacyResponse)&&RebirthBackpackLibraryClientViews.TryGet(migratedViewPlayer.world,migratedViewPlayer.entityId,out decodedView),"legacy remote facade receives matching view");
 RebirthBackpackLibraryClientOffers.Reset();migratedViewPlayer.bag=new Slots{Items=new[]{receiptSource.Clone()}};
 A(RebirthBackpackLibraryClientOffers.RequestPrepare(migratedViewPlayer.world,migratedViewPlayer.entityId,true,0,0,2,true),"legacy remote preparation facade");
 var legacyPreparePacket=(NetPackageRebirthBackpackLibraryPrepareRequest)migratedRemoteManager.Sent.Last();
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))legacyPreparePacket.write(writer);A(stream.Length==117&&stream.Length==legacyPreparePacket.GetLength(),"legacy preparation frame length");stream.Position=2;using(var reader=new PooledBinaryReader(stream))new NetPackageRebirthBackpackLibraryPrepareRequest().read(reader);A(stream.Position==stream.Length,"legacy preparation frame consumption");}
 A(RebirthBackpackLibraryClientOffers.RequestRecovery(migratedViewPlayer.world,migratedViewPlayer.entityId),"legacy recovery facade");
 var legacyRecoveryPacket=(NetPackageRebirthBackpackLibraryRecoveryRequest)migratedRemoteManager.Sent.Last();
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))legacyRecoveryPacket.write(writer);A(stream.Length==95&&stream.Length==legacyRecoveryPacket.GetLength(),"legacy recovery frame length");stream.Position=2;using(var reader=new PooledBinaryReader(stream))new NetPackageRebirthBackpackLibraryRecoveryRequest().read(reader);A(stream.Position==stream.Length,"legacy recovery frame consumption");}
 for(int offset=0,index=0;offset<migratedBytes.Length;offset+=RebirthBackpackLibraryOfferAssembly.ChunkBytes,index++){
 RebirthBackpackLibraryOfferChunk part;A(RebirthBackpackLibraryOfferChunk.TryCreate(migratedCreation,Guid.Parse(migratedReceipt.TransactionId),migratedBytes.Length,index,migratedBytes.Skip(offset).Take(RebirthBackpackLibraryOfferAssembly.ChunkBytes).ToArray(),out part)&&RebirthBackpackLibraryClientOffers.Receive(migratedViewPlayer.world,migratedViewPlayer.entityId,part),"legacy remote offer facade receives chunks");}
 var legacyRemoteTransaction=Guid.Parse(migratedReceipt.TransactionId);
 A(RebirthBackpackLibraryClientOffers.ApplyOffer(migratedViewPlayer.world,migratedViewPlayer.entityId,legacyRemoteTransaction)==RebirthBackpackLibraryOwnerResult.Applied&&RebirthBackpackLibraryClientOffers.ApplyOffer(migratedViewPlayer.world,migratedViewPlayer.entityId,legacyRemoteTransaction)==RebirthBackpackLibraryOwnerResult.Applied&&migratedViewPlayer.bag.Writes==1,"legacy remote owner applies exactly once");
 A(RebirthBackpackLibraryClientOffers.RequestOwnerCheckpoint(migratedViewPlayer.world,migratedViewPlayer.entityId,legacyRemoteTransaction)&&RebirthBackpackLibraryClientOffers.RequestSettlement(migratedViewPlayer.world,migratedViewPlayer.entityId,legacyRemoteTransaction)&&RebirthBackpackLibraryReservation.IsHeld(migratedViewPlayer),"legacy checkpoint and settlement requests retain reservation");
 var legacySettlePacket=(NetPackageRebirthBackpackLibrarySettleRequest)migratedRemoteManager.Sent.Last();
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))legacySettlePacket.write(writer);A(stream.Length==112&&stream.Length==legacySettlePacket.GetLength(),"legacy settlement request length");stream.Position=2;using(var reader=new PooledBinaryReader(stream))new NetPackageRebirthBackpackLibrarySettleRequest().read(reader);A(stream.Position==stream.Length,"legacy settle frame consumption");}
 A(!RebirthBackpackLibraryClientOffers.ReceiveSettlement(migratedViewPlayer.world,migratedViewPlayer.entityId,"legacy-"+new string('b',64),legacyRemoteTransaction,6,true)&&RebirthBackpackLibraryReservation.IsHeld(migratedViewPlayer),"legacy wrong settlement owner cannot release reservation");
 var legacyReply=new NetPackageRebirthBackpackLibrarySettled().Setup(migratedViewPlayer.entityId,RebirthBackpackLibrarySettlement.Create(migratedReceipt,true));
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))legacyReply.write(writer);A(stream.Length==120&&stream.Length==legacyReply.GetLength(),"legacy settled reply length");stream.Position=2;var receivedReply=new NetPackageRebirthBackpackLibrarySettled();using(var reader=new PooledBinaryReader(stream))receivedReply.read(reader);receivedReply.ProcessPackage(migratedViewPlayer.world,GameManager.Instance);}
 A(!RebirthBackpackLibraryReservation.IsHeld(migratedViewPlayer)&&RebirthBackpackLibraryClientOffers.TryGetSettlement(migratedViewPlayer.world,migratedViewPlayer.entityId,legacyRemoteTransaction,out legacyServerSettlement)&&legacyServerSettlement.CreationId==migratedCreation,"legacy reply releases matching reservation and exposes outcome");
 RebirthBackpackLibraryClientOffers.Reset();
 RebirthSurvivorClientState.Creation="legacy-"+new string('b',64);A(!RebirthBackpackLibraryClientViews.TryGet(migratedViewPlayer.world,migratedViewPlayer.entityId,out decodedView),"legacy remote cache invalidated by character replacement");
 RebirthBackpackLibraryClientViews.Reset();GameManager.Instance.World=migratedPriorWorld;SingletonMonoBehaviour<ConnectionManager>.Instance=migratedPriorManager;RebirthSurvivorClientState.Creation=migratedPriorProjection;
 RebirthWorldCharacterService.Record=legacyPriorRecord;
 long hostViewRevision;
 var host=new EntityPlayerLocal();host.world.Player=host;var serverViewWorld=GameManager.Instance.World;GameManager.Instance.World=host.world;RebirthSurvivorClientState.Creation=creation;
  serverOffer.TryGetImages(out beforePack,out afterPack,out beforeSlot,out afterSlot);
 PlayerDataFile.Loaded=null;
 A(!RebirthBackpackLibrarySavedEvidence.TryVerifyLocalFromDisk(host,serverOffer,true),"host missing saved evidence refused");
 PlayerDataFile.Loaded=new PlayerDataFile{bLoaded=true,buffData=new MemoryStream(Evidence(RebirthBackpackLibraryOwnerTransfer.ReceiptKey(serverOffer),1)),bag=new Slots{Items=new[]{afterSlot.Clone()}}};
 A(RebirthBackpackLibrarySavedEvidence.TryVerifyLocalFromDisk(host,serverOffer,true),"host saved receipt and exact after-image verified");
 var hostIdentity=GameManager.Instance.HostIdentity;GameManager.Instance.HostIdentity=null;
 A(!RebirthBackpackLibrarySavedEvidence.TryVerifyLocalFromDisk(host,serverOffer,true),"host missing native identity refused");GameManager.Instance.HostIdentity=hostIdentity;
 host.world.Remote=true;A(!RebirthBackpackLibrarySavedEvidence.TryVerifyLocalFromDisk(host,serverOffer,true),"remote player cannot use host saved proof");host.world.Remote=false;
 PlayerDataFile.Loaded=null;
 var hostSaleBefore=state.EquippedGearItemDataBySlot["backpack"];state.EquippedGearItemDataBySlot["backpack"]=RebirthNativeItemCodec.Encode(saleNext);RebirthBackpackSellStashClientViews.Reset();
 var hostSaleSaveCount=RebirthWorldCharacterRepository.Saves;
 A(RebirthBackpackSellStashClientViews.Request(host.world,host.entityId)&&RebirthBackpackSellStashClientViews.TryGet(host.world,host.entityId,out var hostSaleDisplay)&&hostSaleDisplay.OccupiedSlots==1,"actual host sale view facade reads authoritative projection");
 A(RebirthWorldCharacterRepository.Saves==hostSaleSaveCount,"host sale facade display never saves character");
 host.world.Player=new EntityPlayerLocal();A(!RebirthBackpackSellStashClientViews.TryGet(host.world,host.entityId,out _),"sale facade cannot reuse replaced primary player");host.world.Player=host;
 {
  var beforeSaleBag=host.bag;host.bag=new Bag{Items=new[]{new ItemStack(new ItemValue{Type=2,Payload="sale-inspection"},3)}};
  var saleWindow=new XUiC_RebirthBackpackSellStash{IsOpen=true,xui=new XUi{playerUI=new PlayerUI{entityPlayer=host}}};
  var saleBagChoice=new XUiController{ViewComponent=new XUiView()};var saleInspectLabel=new XUiV_Label();var saleClose=new XUiC_SimpleButton();
  saleWindow.Controls["sellAvailable0"]=saleBagChoice;saleWindow.Controls["sellInspectName"]=new XUiController{ViewComponent=saleInspectLabel};saleWindow.Controls["sellClose"]=saleClose;
  saleWindow.Init();saleWindow.Init();saleWindow.OnOpen();saleBagChoice.Press();
  var selectedSaleBag=typeof(XUiC_RebirthBackpackSellStash).GetField("selectedBag",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
  A((int)selectedSaleBag.GetValue(saleWindow)==0&&saleInspectLabel.Text.Contains("3")&&host.bag.Items[0].count==3,"sale UI inspects arbitrary carried item without transferring it");
  host.bag.Items[0].count=2;saleWindow.Update(1f);A((int)selectedSaleBag.GetValue(saleWindow)==-1,"sale UI invalidates changed carried selection");
  saleClose.Click();A(saleWindow.xui.playerUI.windowManager.Closes==1,"sale UI repeated Init binds close once");saleWindow.Cleanup();saleClose.Click();A(saleWindow.xui.playerUI.windowManager.Closes==1,"sale UI Cleanup removes close callback");
  var quickRecordBefore=RebirthWorldCharacterService.Record;var quickSaveCount=RebirthWorldCharacterRepository.Saves;
  var quickState=new RebirthWorldSupportState{GearRevision=5};quickState.EquippedGearBySlot["backpack"]="pack";quickState.EquippedGearItemDataBySlot["backpack"]=RebirthNativeItemCodec.Encode(salePack);
  RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord{Support=quickState,Origin=new Origin{CreationId=creation}};RebirthBackpackSellStashClientViews.Reset();
  var quickWindow=new XUiC_RebirthBackpackSellStash{IsOpen=true,xui=new XUi{playerUI=new PlayerUI{entityPlayer=host}}};var quickChoice=new XUiController{ViewComponent=new XUiView()};quickWindow.Controls["sellAvailable0"]=quickChoice;var quickStore=new XUiView();var quickTake=new XUiView();quickWindow.Controls["sellDeposit"]=new XUiController{ViewComponent=quickStore};quickWindow.Controls["sellWithdraw"]=new XUiController{ViewComponent=quickTake};
  quickWindow.Init();quickWindow.OnOpen();host.bag.Items[0].count=1;UnityEngine.Input.Shift=true;quickChoice.Press();
  A(quickState.PendingLibraryTransfer==null,"Shift-click refuses changed item before transfer request");UnityEngine.Input.Shift=false;quickChoice.Press();A(quickStore.Enabled&&!quickTake.Enabled,"valid carried selection enables Store and disables Take");UnityEngine.Input.Shift=true;
  host.bag.Items[0].count=2;quickWindow.OnOpen();quickChoice.Press();UnityEngine.Input.Shift=false;
  A(quickState.PendingLibraryTransfer!=null&&quickState.PendingLibraryTransfer.IsSellStash&&quickState.PendingLibraryTransfer.Quantity==2&&host.bag.Items[0].count==2&&quickState.GearRevision==5,"actual Shift-click creates one sale intent without premature inventory debit");A(!quickStore.Enabled&&!quickTake.Enabled,"pending custody disables Store and Take");
  quickState.PendingLibraryTransfer=null;quickWindow.OnClose();
  RebirthBackpackSellStashPolicy.TestCapacity=2;
  var multiSource=new ItemStack(new ItemValue{Type=2,Payload="multi-stack"},2);
  var multiTarget=multiSource.Clone();multiTarget.count=4;
  var multiPack=new ItemValue{Type=8,Slots=new[]{multiTarget,ItemStack.Empty.Clone()}};
  quickState.GearRevision=5;quickState.EquippedGearItemDataBySlot["backpack"]=RebirthNativeItemCodec.Encode(multiPack);
  host.bag.Items[0]=multiSource.Clone();quickWindow.OnOpen();UnityEngine.Input.Shift=true;quickChoice.Press();UnityEngine.Input.Shift=false;
  var multiFirst=quickState.PendingLibraryTransfer;
  A(multiFirst!=null&&multiFirst.Quantity==1&&multiFirst.LibrarySlot==0,"quick Shift starts fitting merge only");
  quickWindow.Update(1f);A(ReferenceEquals(multiFirst,quickState.PendingLibraryTransfer)&&host.bag.Items[0].count==2,"quick pending merge cannot prepare remainder");
  multiFirst.TryGetImages(out _,out _,out _,out var multiRemaining);host.bag.Items[0]=multiRemaining.Clone();
  A(RebirthBackpackLibraryJournal.MarkOwnerApplied(quickState,multiFirst.TransactionId,creation,r=>true,()=>true)&&
    RebirthBackpackLibraryJournal.CommitBackpack(quickState,multiFirst.TransactionId,creation,()=>true)&&
    RebirthBackpackLibraryJournal.Finish(quickState,multiFirst.TransactionId,creation,r=>true,()=>true),"fixture settles first merge through actual journal with doubled saved evidence");
  RebirthBackpackSellStashClientViews.Request(host.world,host.entityId);quickWindow.Update(1f);
  var multiSecond=quickState.PendingLibraryTransfer;
  A(multiSecond!=null&&multiSecond.TransactionId!=multiFirst.TransactionId&&multiSecond.Quantity==1&&multiSecond.LibrarySlot==1&&multiSecond.ExpectedGearRevision==6&&host.bag.Items[0].count==1,"quick confirmed merge prepares exact remainder into next slot without premature debit");
  quickWindow.Update(1f);A(ReferenceEquals(multiSecond,quickState.PendingLibraryTransfer),"quick duplicate update cannot prepare competing remainder");
  quickWindow.OnClose();
  var quickOwnerField=typeof(XUiC_RebirthBackpackSellStash).GetField("quickOwner",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
  A(quickOwnerField.GetValue(quickWindow)==null&&ReferenceEquals(multiSecond,quickState.PendingLibraryTransfer),"closing quick transfer cancels continuation without cancelling journal");
  RebirthBackpackSellStashPolicy.TestCapacity=1;quickState.EquippedGearItemDataBySlot["backpack"]=RebirthNativeItemCodec.Encode(salePack);quickState.GearRevision=7;
  host.bag.Items[0]=multiSource.Clone();host.bag.Items[0].itemValue.Payload="sale-inspection";
  Console.WriteLine("PASS actual Sell Shift multi-cell controller continuation: pending refusal, durable journal settlement, fresh next-slot prepare, duplicate update, close preserves custody; native saved evidence doubled.");
  quickState.PendingLibraryTransfer=null;quickState.LibraryTransferPhase=RebirthBackpackLibraryPhase.Prepared;
  var dragPreviewView=new XUiView();var dragPreviewLabel=new XUiV_Label();
  quickWindow.Controls["sellBody"]=new XUiController{ViewComponent=new XUiView()};
  quickWindow.Controls["sellDragPreview"]=new XUiController{ViewComponent=dragPreviewView};
  quickWindow.Controls["sellDragPreviewIcon"]=new XUiController{ViewComponent=new XUiV_Sprite()};
  quickWindow.Controls["sellDragPreviewCount"]=new XUiController{ViewComponent=dragPreviewLabel};
  var dropCell=new XUiController{ViewComponent=new XUiView{IsVisible=true}};quickWindow.Controls["sellChoose0"]=dropCell;quickWindow.Init();quickWindow.Init();A(quickChoice.DragBindings==1&&dropCell.DragBindings==1,"repeated Init leaves one drag handler per surface");quickWindow.OnOpen();RebirthInventoryDropHitTest.Target=dropCell;
  quickChoice.Drag(EDragType.DragStart);A(dragPreviewView.IsVisible&&dragPreviewView.Position.x==136&&dragPreviewView.Position.y==-96&&dragPreviewLabel.Text=="2","drag preview follows local pointer with detached count");host.bag.Items[0].count=1;quickChoice.Drag(EDragType.DragEnd);A(quickState.PendingLibraryTransfer==null,"drag refuses changed source count");A(!dragPreviewView.IsVisible,"refused drag hides item preview");
  host.bag.Items[0].count=2;quickWindow.OnOpen();quickChoice.Drag(EDragType.DragStart);RebirthBackpackSellStashClientViews.Request(host.world,host.entityId);quickChoice.Drag(EDragType.DragEnd);A(quickState.PendingLibraryTransfer==null,"drag refuses replaced displayed view even at same revision");
  quickWindow.OnOpen();quickChoice.Drag(EDragType.DragStart);quickChoice.Drag(EDragType.DragEnd);A(quickState.PendingLibraryTransfer!=null&&quickState.PendingLibraryTransfer.IsSellStash&&quickState.PendingLibraryTransfer.LibrarySlot==0&&host.bag.Items[0].count==2,"drag submits exact target sale receipt without direct inventory mutation");
  A(!dragPreviewView.IsVisible,"accepted drag hides item preview");Console.WriteLine("PASS detached drag preview: pointer position/count and hide after refused/accepted drop; pointer geometry adapter doubled.");
  RebirthInventoryDropHitTest.Target=null;
  quickWindow.Cleanup();A(quickChoice.DragBindings==0&&dropCell.DragBindings==0,"Cleanup removes all drag handlers");RebirthWorldCharacterService.Record=quickRecordBefore;RebirthWorldCharacterRepository.Saves=quickSaveCount;RebirthBackpackSellStashClientViews.Reset();
  host.bag=beforeSaleBag;
 }
 {
  var withdrawalRecordBefore=RebirthWorldCharacterService.Record;var withdrawalBagBefore=host.bag;
  foreach(bool selling in new[]{false,true})
  {
   var withdrawalSource=new ItemStack(new ItemValue{Type=1,Payload="withdraw-learning"},3);
   var withdrawalTarget=withdrawalSource.Clone();withdrawalTarget.count=4;
   var withdrawalPack=new ItemValue{Type=8,Slots=new[]{withdrawalSource.Clone()}};
   var withdrawalState=new RebirthWorldSupportState{GearRevision=12};withdrawalState.EquippedGearBySlot["backpack"]="pack";
   withdrawalState.EquippedGearItemDataBySlot["backpack"]=RebirthNativeItemCodec.Encode(withdrawalPack);
   RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord{Support=withdrawalState,Origin=new Origin{CreationId=creation}};
   host.bag=new Bag{Items=new[]{withdrawalTarget.Clone(),ItemStack.Empty.Clone()}};
   RebirthBackpackLibraryClientViews.Reset();RebirthBackpackSellStashClientViews.Reset();
   XUiController withdrawalWindow=selling?(XUiController)new XUiC_RebirthBackpackSellStash():new XUiC_RebirthBackpackLibrary();
   withdrawalWindow.IsOpen=true;withdrawalWindow.xui=new XUi{playerUI=new PlayerUI{entityPlayer=host}};
   string withdrawalPrefix=selling?"sell":"theory";
   var withdrawalChoice=new XUiController{ViewComponent=new XUiView()};
   withdrawalWindow.Controls[withdrawalPrefix+"Choose0"]=withdrawalChoice;
   withdrawalWindow.Init();withdrawalWindow.OnOpen();
   var withdrawalType=withdrawalWindow.GetType();var privateMembers=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
   withdrawalType.GetField("nextFinish",privateMembers).SetValue(withdrawalWindow,DateTime.MaxValue);
   UnityEngine.Input.Shift=true;withdrawalChoice.Press();UnityEngine.Input.Shift=false;
   var firstWithdrawal=withdrawalState.PendingLibraryTransfer;
   A(firstWithdrawal!=null&&!firstWithdrawal.Deposit&&firstWithdrawal.IsSellStash==selling&&firstWithdrawal.Quantity==1&&firstWithdrawal.InventorySlot==0&&host.bag.Items[0].count==4,
     withdrawalPrefix+" Shift withdrawal first fits carried merge without debit");
   withdrawalWindow.Update(1f);
   A(ReferenceEquals(firstWithdrawal,withdrawalState.PendingLibraryTransfer),withdrawalPrefix+" pending withdrawal refuses competing prepare");
   firstWithdrawal.TryGetImages(out _,out _,out _,out var firstWithdrawalAfter);host.bag.Items[0]=firstWithdrawalAfter.Clone();
   A(RebirthBackpackLibraryJournal.MarkOwnerApplied(withdrawalState,firstWithdrawal.TransactionId,creation,r=>true,()=>true)&&
     RebirthBackpackLibraryJournal.CommitBackpack(withdrawalState,firstWithdrawal.TransactionId,creation,()=>true)&&
     RebirthBackpackLibraryJournal.Finish(withdrawalState,firstWithdrawal.TransactionId,creation,r=>true,()=>true),
     withdrawalPrefix+" first withdrawal journal settles with doubled save evidence");
   if(selling)RebirthBackpackSellStashClientViews.Request(host.world,host.entityId);else RebirthBackpackLibraryClientViews.Request(host.world,host.entityId);
   withdrawalWindow.Update(1f);
   var secondWithdrawal=withdrawalState.PendingLibraryTransfer;
   A(secondWithdrawal!=null&&!secondWithdrawal.Deposit&&secondWithdrawal.TransactionId!=firstWithdrawal.TransactionId&&secondWithdrawal.ExpectedGearRevision==13&&
     secondWithdrawal.Quantity==2&&secondWithdrawal.InventorySlot==1&&host.bag.Items[1].IsEmpty(),
     withdrawalPrefix+" settled withdrawal prepares remaining two into next carried slot without debit");
   withdrawalType.GetField("nextFinish",privateMembers).SetValue(withdrawalWindow,DateTime.MaxValue);
   secondWithdrawal.TryGetImages(out _,out _,out _,out var secondWithdrawalAfter);host.bag.Items[1]=secondWithdrawalAfter.Clone();
   A(RebirthBackpackLibraryJournal.MarkOwnerApplied(withdrawalState,secondWithdrawal.TransactionId,creation,r=>true,()=>true)&&
     RebirthBackpackLibraryJournal.CommitBackpack(withdrawalState,secondWithdrawal.TransactionId,creation,()=>true)&&
     RebirthBackpackLibraryJournal.Finish(withdrawalState,secondWithdrawal.TransactionId,creation,r=>true,()=>true),
     withdrawalPrefix+" remaining withdrawal journal settles");
   if(selling)RebirthBackpackSellStashClientViews.Request(host.world,host.entityId);else RebirthBackpackLibraryClientViews.Request(host.world,host.entityId);
   withdrawalWindow.Update(1f);withdrawalWindow.Update(1f);
   A(withdrawalState.PendingLibraryTransfer==null&&host.bag.Items[0].count==5&&host.bag.Items[1].count==2&&
     withdrawalType.GetField("quickOwner",privateMembers).GetValue(withdrawalWindow)==null,
     withdrawalPrefix+" exhausted stored stack ends continuation without replay");
   RebirthNativeItemCodec.TryDecode(withdrawalState.EquippedGearItemDataBySlot["backpack"],out var exhaustedPack);
   A(exhaustedPack.Slots[0].IsEmpty(),withdrawalPrefix+" withdrawn stored source conserved");
   withdrawalWindow.OnClose();withdrawalWindow.Cleanup();
  }
  RebirthWorldCharacterService.Record=withdrawalRecordBefore;host.bag=withdrawalBagBefore;
  RebirthBackpackLibraryClientViews.Reset();RebirthBackpackSellStashClientViews.Reset();
  Console.WriteLine("PASS actual Theory AND Sell Shift withdrawals: carried partial merge, pending refusal, journal settlement, next-slot remainder, exhaustion and conservation; native inventory/save adapters doubled.");
 }
 RebirthBackpackSellStashClientViews.Reset();A(!RebirthBackpackSellStashClientViews.TryGet(host.world,host.entityId,out _),"sale facade reset drops view");
 state.EquippedGearItemDataBySlot["backpack"]=hostSaleBefore;
 int beforeHostViewSaves=RebirthWorldCharacterRepository.Saves;int beforeHostPackets=viewServerManager.Sent.Count;
 RebirthBackpackLibraryClientViews.Reset();
 A(RebirthBackpackLibraryClientViews.Request(host.world,host.entityId)&&RebirthBackpackLibraryClientViews.TryGet(host.world,host.entityId,out decodedView)&&decodedView.GearRevision==5&&decodedView.TransferPending,"host direct view uses current authoritative library projection");
 A(RebirthWorldCharacterRepository.Saves==beforeHostViewSaves&&viewServerManager.Sent.Count==beforeHostPackets,"host display has no network send or custody save");
 A(RebirthBackpackLibraryServer.GetLocalViewStatus(host,Guid.NewGuid().ToString("N"),out decodedView,out hostViewRevision)==RebirthBackpackLibraryViewStatus.Unavailable,"host wrong character refuses view");
 host.world.Player=new EntityPlayerLocal();A(RebirthBackpackLibraryServer.GetLocalViewStatus(host,creation,out decodedView,out hostViewRevision)==RebirthBackpackLibraryViewStatus.Unavailable&&!RebirthBackpackLibraryClientViews.TryGet(host.world,host.entityId,out decodedView),"replaced local primary player cannot reuse host display");host.world.Player=host;
 {
  var studyPlayer=new EntityPlayerLocal{inventory=new Slots{Items=new[]{ItemStack.Empty.Clone(),ItemStack.Empty.Clone()}}};
  var priorKind=RebirthProgressionRuntimeConfig.PurposeKind;RebirthProgressionRuntimeConfig.PurposeKind="theory";
  RebirthBackpackLibraryView studyView;
  A(RebirthBackpackLibraryView.TryCreate(Guid.Parse(creation),5,afterPack,false,out studyView),"study view");
  RebirthBackpackLibraryStudyIntent intent;
  RebirthGearOwnerReservation.Held=true;
  A(!RebirthBackpackLibraryStudyIntent.TryCreate(studyPlayer,studyView,0,0,out intent),"gear hold blocks new study intent");
  A(RebirthBackpackLibraryReservation.BlocksResourceUse(studyPlayer),"gear hold blocks shared resource use");
  RebirthGearOwnerReservation.Held=false;
  A(RebirthBackpackLibraryStudyIntent.TryCreate(studyPlayer,studyView,0,0,out intent),"one-book study intent");
  RebirthBackpackLibraryReceipt studyReceipt;
  A(RebirthBackpackLibraryReceipt.TryCreate(Guid.NewGuid(),Guid.Parse(creation),5,false,0,0,1,false,afterPack,ItemStack.Empty.Clone(),out studyReceipt),"study withdrawal receipt");
  RebirthBackpackLibraryReceipt saleStudyReceipt;
  A(RebirthBackpackLibraryReceipt.TryCreateSellStash(Guid.NewGuid(),creation,5,false,0,0,1,false,afterPack,ItemStack.Empty.Clone(),out saleStudyReceipt),"sale-section book withdrawal receipt");
  A(!intent.TryBind(studyPlayer,saleStudyReceipt),"study cannot bind otherwise matching Sell Stash receipt");
  A(intent.TryBind(studyPlayer,studyReceipt),"exact study receipt matches");
  var studyOutcome=RebirthBackpackLibrarySettlement.Create(studyReceipt,true);
  ItemStack studyBook;ItemValue unusedPackBefore,unusedPackAfter;ItemStack unusedBefore,studyAfter;
  studyReceipt.TryGetImages(out unusedPackBefore,out unusedPackAfter,out unusedBefore,out studyAfter);
  A(!intent.TryGetSettledBook(studyPlayer,studyOutcome,out studyBook),"settlement alone does not manufacture physical book");
  studyPlayer.inventory.Items[0]=studyAfter.Clone();
  RebirthGearOwnerReservation.Held=true;
  A(!intent.TryGetSettledBook(studyPlayer,studyOutcome,out studyBook),"gear hold blocks settled book admission");
  RebirthGearOwnerReservation.Held=false;
  A(intent.TryGetSettledBook(studyPlayer,studyOutcome,out studyBook)&&studyBook.count==1,"matching settled physical book admitted");
  studyBook.count=99;A(studyPlayer.inventory.Items[0].count==1,"returned study book detached");
  studyPlayer.inventory.Items[0].count=2;
  A(!intent.TryGetSettledBook(studyPlayer,studyOutcome,out studyBook),"changed destination stack refused");
  studyPlayer.inventory.Items[0]=studyAfter.Clone();
  A(!intent.TryGetSettledBook(studyPlayer,RebirthBackpackLibrarySettlement.Create(studyReceipt,false),out studyBook),"rejected transfer cannot start study");
  A(!RebirthBackpackLibraryStudyIntent.TryCreate(studyPlayer,studyView,0,0,out var occupiedIntent),"occupied toolbelt refused");
  A(!intent.TryBind(new EntityPlayerLocal(),studyReceipt),"replacement owner refused");
  RebirthBackpackLibraryReceipt otherStudyReceipt;
  A(RebirthBackpackLibraryReceipt.TryCreate(Guid.NewGuid(),Guid.Parse(creation),5,false,0,0,1,false,afterPack,ItemStack.Empty.Clone(),out otherStudyReceipt)&&!intent.TryBind(studyPlayer,otherStudyReceipt),"intent cannot switch transaction");
  RebirthProgressionRuntimeConfig.PurposeKind=priorKind;
 }
 {
  var lifecycle=new XUiC_RebirthBackpackLibrary{xui=new XUi{playerUI=new PlayerUI{entityPlayer=host}}};
  var plainClose=new XUiController();lifecycle.Controls["theoryClose"]=plainClose;
  lifecycle.Init();lifecycle.Init();plainClose.Press();
  A(lifecycle.xui.playerUI.windowManager.Closes==1,"library repeated Init leaves one plain close handler");
  lifecycle.Cleanup();plainClose.Press();
  A(lifecycle.xui.playerUI.windowManager.Closes==1,"library Cleanup removes prior handler");
  var simpleClose=new XUiC_SimpleButton();lifecycle.Controls["theoryClose"]=simpleClose;
  lifecycle.Init();lifecycle.Init();simpleClose.Click();
  A(lifecycle.xui.playerUI.windowManager.Closes==2,"library simplebutton binds native OnPressed once");
  plainClose.Press();A(lifecycle.xui.playerUI.windowManager.Closes==2,"replaced control remains unwired");
  lifecycle.Cleanup();simpleClose.Click();A(lifecycle.xui.playerUI.windowManager.Closes==2,"simplebutton Cleanup unwires");
 }
 {
  var originalBag=host.bag;host.bag=new Bag{Items=new[]{S(3)}};
  var gridUi=new XUiC_RebirthBackpackLibrary{IsOpen=true,xui=new XUi{playerUI=new PlayerUI{entityPlayer=host}}};
  var bagChoice=new XUiController{ViewComponent=new XUiView()};gridUi.Controls["theoryAvailable0"]=bagChoice;
  var selectedLabel=new XUiV_Label();gridUi.Controls["theoryInspectName"]=new XUiController{ViewComponent=selectedLabel};
  gridUi.Init();gridUi.OnOpen();bagChoice.Press();
  var selectedBagField=typeof(XUiC_RebirthBackpackLibrary).GetField("selectedBag",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
  A((int)selectedBagField.GetValue(gridUi)==0&&selectedLabel.Text.Contains("3"),"full backpack grid selects and inspects exact carried stack");
  host.bag.ItemGrid.items[0]=S(2);gridUi.Update(1f);
  A((int)selectedBagField.GetValue(gridUi)==-1,"changed carried stack invalidates inspection and deposit selection");
  var nonTheoryPurpose=new XUiV_Label();typeof(XUiC_RebirthBackpackLibrary).GetField("purpose",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(gridUi,nonTheoryPurpose);
  host.bag.Items[0]=new ItemStack(new ItemValue{Type=2},1);gridUi.Update(1f);bagChoice.Press();
  A(nonTheoryPurpose.Text=="xuiRebirthTheoryNotMaterial","Theory inspector explains ordinary item storage restriction without claiming learning reference");
  gridUi.OnClose();A((int)selectedBagField.GetValue(gridUi)==-1,"close clears carried selection");gridUi.Cleanup();
  host.bag=originalBag;
 }
 var ui=new XUiC_RebirthBackpackLibrary{IsOpen=true,xui=new XUi{playerUI=new PlayerUI{entityPlayer=host}}};
 ui.Init();ui.OnOpen();
 var outstandingField=typeof(XUiC_RebirthBackpackLibrary).GetField("requestOutstanding",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var selectedField=typeof(XUiC_RebirthBackpackLibrary).GetField("selected",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var displayedField=typeof(XUiC_RebirthBackpackLibrary).GetField("displayed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 outstandingField.SetValue(ui,true);selectedField.SetValue(ui,2);ui.OnOpen();
 A((bool)outstandingField.GetValue(ui)&&((int)selectedField.GetValue(ui))==-1,"actual UI reopen clears selection without dropping same-owner pending admission");
 outstandingField.SetValue(ui,false);selectedField.SetValue(ui,2);ui.Update(5f);
 A((int)selectedField.GetValue(ui)==2&&displayedField.GetValue(ui)!=null,"actual UI ordinary same-revision view refresh preserves selection");
 var purposePackOriginal=state.EquippedGearItemDataBySlot["backpack"];state.EquippedGearItemDataBySlot["backpack"]=RebirthNativeItemCodec.Encode(afterPack);A(RebirthBackpackLibraryClientViews.Request(host.world,host.entityId),"occupied purpose fixture projection");
 var purposeLabel=new XUiV_Label();typeof(XUiC_RebirthBackpackLibrary).GetField("purpose",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ui,purposeLabel);
 selectedField.SetValue(ui,0);var renderMethod=typeof(XUiC_RebirthBackpackLibrary).GetMethod("Render",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 RebirthProgressionRuntimeConfig.PurposeKind="theory";renderMethod.Invoke(ui,null);A(purposeLabel.Text=="xuiRebirthTheoryPurposeTheory","actual selected title presents theory purpose");
 RebirthProgressionRuntimeConfig.PurposeKind="discovery";renderMethod.Invoke(ui,null);A(purposeLabel.Text=="xuiRebirthTheoryPurposeRecipe","actual selected title presents recipe purpose");
 RebirthProgressionRuntimeConfig.PurposeAudio=true;renderMethod.Invoke(ui,null);A(purposeLabel.Text=="xuiRebirthTheoryPurposeAudio xuiRebirthTheoryPurposeRecipe","actual audiobook resolves source discovery purpose");
 RebirthProgressionRuntimeConfig.PurposeKind="other";renderMethod.Invoke(ui,null);A(purposeLabel.Text=="xuiRebirthTheoryPurposeAudio xuiRebirthTheoryPurposeReference","actual unknown literature kind uses reference purpose");
 selectedField.SetValue(ui,-1);renderMethod.Invoke(ui,null);A(purposeLabel.Text=="xuiRebirthTheoryPurposeSelect","actual empty selection asks for a stored title");
 RebirthProgressionRuntimeConfig.PurposeKind=null;RebirthProgressionRuntimeConfig.PurposeAudio=false;
 state.EquippedGearItemDataBySlot["backpack"]=purposePackOriginal;RebirthBackpackLibraryClientViews.Request(host.world,host.entityId);
 var replacementUiOwner=new EntityPlayerLocal{entityId=99,world=new PlayerWorld()};outstandingField.SetValue(ui,true);ui.xui.playerUI.entityPlayer=replacementUiOwner;ui.Update(1f);
 A(!(bool)outstandingField.GetValue(ui)&&(int)selectedField.GetValue(ui)==-1&&displayedField.GetValue(ui)==null,"actual UI replacement owner clears old local admission and display");
 typeof(XUiC_RebirthBackpackLibrary).GetMethod("Withdraw",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(ui,new object[]{false});
 A((string)typeof(XUiC_RebirthBackpackLibrary).GetField("feedback",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ui)=="xuiRebirthTheoryChanged","actual unavailable owner-view click supplies refusal feedback");
 var budgetField=typeof(XUiC_RebirthBackpackLibrary).GetField("continuationRemaining",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var finishField=typeof(XUiC_RebirthBackpackLibrary).GetField("nextFinish",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 budgetField.SetValue(ui,2);ui.IsOpen=false;ui.Update(10f);A((int)budgetField.GetValue(ui)==2,"closed actual library UI never advances continuation budget");
 ui.IsOpen=true;finishField.SetValue(ui,DateTime.MinValue);ui.Update(1f);A((int)budgetField.GetValue(ui)==2,"automatic library continuation waits for two-second cadence");
 ui.Update(1f);A((int)budgetField.GetValue(ui)==1,"actual library continuation consumes one bounded attempt");
 finishField.SetValue(ui,DateTime.MinValue);ui.Update(2f);A((int)budgetField.GetValue(ui)==0,"actual library continuation exhausts budget");
 ui.Update(10f);A((int)budgetField.GetValue(ui)==0,"exhausted library budget does not restart itself");
  GameManager.Instance.World=new PlayerWorld();A(!RebirthBackpackLibraryClientViews.Request(host.world,host.entityId)&&!RebirthBackpackLibraryClientViews.TryGet(host.world,host.entityId,out decodedView),"host old world display refused");GameManager.Instance.World=host.world;
 host.world.Remote=true;A(!RebirthBackpackLibraryClientViews.Request(host.world,host.entityId),"server role in remote world refuses local host bypass");host.world.Remote=false;
 RebirthBackpackLibraryClientViews.Reset();GameManager.Instance.World=serverViewWorld;
 var recoveryManager=new ConnectionManager{IsServer=true};SingletonMonoBehaviour<ConnectionManager>.Instance=recoveryManager;int savesBeforeMapping=RebirthWorldCharacterRepository.Saves;NetPackageManager.Missing=true;
 A(!RebirthBackpackLibraryRecoveryDelivery.TrySend(serverPlayer,sender,creation)&&recoveryManager.Sent.Count==0&&RebirthWorldCharacterRepository.Saves==savesBeforeMapping,"recovery missing mapping refuses before save/queue");NetPackageManager.Missing=false;
 A(RebirthBackpackLibraryRecoveryDelivery.TrySend(serverPlayer,sender,creation)&&recoveryManager.Sent.Count>0&&recoveryManager.Targets.All(id=>id==serverPlayer.entityId)&&state.GearRevision==5&&state.PendingLibraryTransfer.TransactionId==serverOffer.TransactionId,"recovery queues owner offer without custody mutation");int firstQueued=recoveryManager.Sent.Count;
 A(RebirthBackpackLibraryRecoveryDelivery.TrySend(serverPlayer,sender,creation)&&recoveryManager.Sent.Count==2*firstQueued&&state.GearRevision==5,"recovery resend same intent");
 recoveryManager.FailSendAt=recoveryManager.Sent.Count+1;A(!RebirthBackpackLibraryRecoveryDelivery.TrySend(serverPlayer,sender,creation)&&state.PendingLibraryTransfer.TransactionId==serverOffer.TransactionId&&state.LibraryTransferPhase==RebirthBackpackLibraryPhase.Prepared,"recovery queue failure retains pending intent");recoveryManager.FailSendAt=0;
 sender.entityId=2;int beforeInvalidQueue=recoveryManager.Sent.Count;A(!RebirthBackpackLibraryRecoveryDelivery.TrySend(serverPlayer,sender,creation)&&recoveryManager.Sent.Count==beforeInvalidQueue,"invalid sender receives no recovery chunks");sender.entityId=1;
 var recoveryRequest=new NetPackageRebirthBackpackLibraryRecoveryRequest().Setup(serverPlayer.entityId,creation);A(recoveryRequest.PackageDirection==NetPackageDirection.ToServer,"recovery request direction");
 using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream))recoveryRequest.write(writer);A(stream.Length==22&&stream.Length==recoveryRequest.GetLength(),"fixed recovery request length");stream.Position=2;var decodedRequest=new NetPackageRebirthBackpackLibraryRecoveryRequest{Sender=sender};using(var reader=new PooledBinaryReader(stream)){decodedRequest.read(reader);A(stream.Position==stream.Length,"recovery request consumes exact body");}int queued=recoveryManager.Sent.Count;decodedRequest.ProcessPackage(serverPlayer.world,GameManager.Instance);A(recoveryManager.Sent.Count>queued,"actual recovery request dispatch queues offer");sender.entityId=2;queued=recoveryManager.Sent.Count;decodedRequest.ProcessPackage(serverPlayer.world,GameManager.Instance);A(recoveryManager.Sent.Count==queued,"request sender spoof refused");sender.entityId=1;bool truncated=false;using(var shortStream=new MemoryStream(new byte[]{1,0,0,0,1}))using(var reader=new PooledBinaryReader(shortStream)){try{decodedRequest.read(reader);}catch(InvalidDataException){truncated=true;}}A(truncated,"truncated recovery GUID refused");decodedRequest.ProcessPackage(serverPlayer.world,GameManager.Instance);A(recoveryManager.Sent.Count==queued,"failed pooled recovery read cannot replay old creation");}
 var badRecovery=new NetPackageRebirthBackpackLibraryRecoveryRequest().Setup(1,"invalid");badRecovery.Sender=sender;int beforeBadRecovery=recoveryManager.Sent.Count;badRecovery.ProcessPackage(serverPlayer.world,GameManager.Instance);A(recoveryManager.Sent.Count==beforeBadRecovery,"invalid recovery GUID refused");
 A(RebirthBackpackLibraryServer.GetRecoveryOffer(serverPlayer,sender,creation,out restored)&&XNode.DeepEquals(restored.ToXml(),serverOffer.ToXml())&&state.GearRevision==5,"reconnect exact pending intent without revision change");
 RebirthWorldCharacterRepository.FailAt=RebirthWorldCharacterRepository.Saves+1;
 A(!RebirthBackpackLibraryServer.GetRecoveryOffer(serverPlayer,sender,creation,out restored)&&restored==null&&state.PendingLibraryTransfer.TransactionId==serverOffer.TransactionId&&state.GearRevision==5,"recovery save failure retains intent and offers nothing");RebirthWorldCharacterRepository.FailAt=0;
 long recoveryRevision=state.GearRevision;state.GearRevision++;
 A(!RebirthBackpackLibraryServer.GetRecoveryOffer(serverPlayer,sender,creation,out restored)&&restored==null,"recovery stale journal revision refused");state.GearRevision=recoveryRevision;
 sender.entityId=2;A(!RebirthBackpackLibraryServer.GetRecoveryOffer(serverPlayer,sender,creation,out restored)&&restored==null,"recovery sender mismatch refused");sender.entityId=1;
 A(!RebirthBackpackLibraryServer.GetRecoveryOffer(serverPlayer,sender,Guid.NewGuid().ToString("N"),out restored)&&restored==null,"recovery replacement character refused");
 A(!RebirthBackpackLibraryServer.Prepare(serverPlayer,sender,creation,5,true,0,0,1,true,out restored)&&state.PendingLibraryTransfer.TransactionId==serverOffer.TransactionId,"server pending not replaced");
 RebirthWorldCharacterRepository.IsServerAuthority=false;A(!RebirthBackpackLibraryServer.Advance(serverPlayer,sender,creation,serverOffer.TransactionId,true),"non-authoritative coordinator refused");RebirthWorldCharacterRepository.IsServerAuthority=true;
 A(!RebirthBackpackLibraryServer.Advance(serverPlayer,sender,creation,Guid.NewGuid().ToString("N"),true),"wrong pending transaction refused");
 PlayerDataFile.Loaded=null;A(!RebirthBackpackLibraryServer.Advance(serverPlayer,sender,creation,serverOffer.TransactionId,true)&&state.LibraryTransferPhase==RebirthBackpackLibraryPhase.Prepared,"server ACK without saved proof refused");
 serverOffer.TryGetImages(out beforePack,out afterPack,out beforeSlot,out afterSlot);
 PlayerDataFile.Loaded=new PlayerDataFile{bLoaded=true,buffData=new MemoryStream(Evidence(RebirthBackpackLibraryOwnerTransfer.ReceiptKey(serverOffer),1)),bag=new Slots{Items=new[]{afterSlot.Clone()}}};
 state.EquippedGearItemDataBySlot["backpack"]=RebirthNativeItemCodec.Encode(new ItemValue{Type=8,Payload="replacement",Slots=new[]{ItemStack.Empty.Clone()}});
 A(!RebirthBackpackLibraryServer.GetRecoveryOffer(serverPlayer,sender,creation,out restored)&&restored==null,"recovery replacement backpack image refused");
 A(!RebirthBackpackLibraryServer.Advance(serverPlayer,sender,creation,serverOffer.TransactionId,true)&&state.GearRevision==5,"replacement backpack refused");state.EquippedGearItemDataBySlot["backpack"]=initialData;
 RebirthWorldCharacterRepository.FailAt=RebirthWorldCharacterRepository.Saves+2;
 A(!RebirthBackpackLibraryServer.Advance(serverPlayer,sender,creation,serverOffer.TransactionId,true)&&state.LibraryTransferPhase==RebirthBackpackLibraryPhase.OwnerApplied&&state.GearRevision==5,"server commit failure retains owner custody");
 RebirthWorldCharacterRepository.FailAt=0;
 A(RebirthBackpackLibraryServer.GetRecoveryOffer(serverPlayer,sender,creation,out restored)&&XNode.DeepEquals(restored.ToXml(),serverOffer.ToXml())&&state.LibraryTransferPhase==RebirthBackpackLibraryPhase.OwnerApplied&&state.GearRevision==5,"owner-applied reconnect preserves intent and phase");
 RebirthWorldCharacterRepository.FailAt=RebirthWorldCharacterRepository.Saves+3;
 A(!RebirthBackpackLibraryServer.Advance(serverPlayer,sender,creation,serverOffer.TransactionId,true)&&state.LibraryTransferPhase==RebirthBackpackLibraryPhase.BackpackCommitted&&state.GearRevision==6,"finish save failure retains committed intent");RebirthWorldCharacterRepository.FailAt=0;
 A(RebirthBackpackLibraryServer.GetRecoveryOffer(serverPlayer,sender,creation,out restored)&&XNode.DeepEquals(restored.ToXml(),serverOffer.ToXml())&&state.LibraryTransferPhase==RebirthBackpackLibraryPhase.BackpackCommitted&&state.GearRevision==6,"committed reconnect offers same intent without recommit");
  var localSettlementWorld=GameManager.Instance.World;GameManager.Instance.World=host.world;
 var localSavedEvidence=PlayerDataFile.Loaded;PlayerDataFile.Loaded=null;
 A(!RebirthBackpackLibraryServer.AdvanceLocal(host,creation,serverOffer.TransactionId,true)&&state.PendingLibraryTransfer!=null,"host settlement without saved evidence remains pending");PlayerDataFile.Loaded=localSavedEvidence;
 A(!RebirthBackpackLibraryServer.AdvanceLocal(host,Guid.NewGuid().ToString("N"),serverOffer.TransactionId,true),"host settlement wrong creation refused");
  RebirthWorldCharacterRepository.FailAt=RebirthWorldCharacterRepository.Saves+1;
 var hostPriorTerminal=state.LastLibrarySettlement;
 A(!RebirthBackpackLibraryServer.AdvanceLocal(host,creation,serverOffer.TransactionId,true)&&state.PendingLibraryTransfer!=null&&state.LibraryTransferPhase==RebirthBackpackLibraryPhase.BackpackCommitted&&state.GearRevision==6&&ReferenceEquals(state.LastLibrarySettlement,hostPriorTerminal),"host failed terminal save preserves pending committed custody and previous outcome");
 RebirthBackpackLibrarySettlement failedHostTerminal;
 A(!RebirthBackpackLibraryServer.TryGetLocalSettlement(host,creation,serverOffer.TransactionId,out failedHostTerminal),"host failed save cannot fabricate terminal confirmation");
 RebirthWorldCharacterRepository.FailAt=0;
 A(RebirthBackpackLibraryServer.AdvanceLocal(host,creation,serverOffer.TransactionId,true)&&state.GearRevision==6&&state.PendingLibraryTransfer==null,"host saved proof settles shared journal");
 int localSettledSaves=RebirthWorldCharacterRepository.Saves;
 RebirthBackpackLibrarySettlement localTerminal;
 A(RebirthBackpackLibraryServer.TryGetLocalSettlement(host,creation,serverOffer.TransactionId,out localTerminal)&&localTerminal.Applied&&localTerminal.GearRevision==6,"host lost confirmation recoverable from durable outcome");
 A(!RebirthBackpackLibraryServer.AdvanceLocal(host,creation,serverOffer.TransactionId,true)&&RebirthWorldCharacterRepository.Saves==localSettledSaves&&state.GearRevision==6,"host terminal retry cannot repeat commit or save");
 A(!RebirthBackpackLibraryServer.TryGetLocalSettlement(host,creation,Guid.NewGuid().ToString("N"),out localTerminal),"host terminal wrong transaction refused");
  var localOriginalSupport=RebirthWorldCharacterService.Record.Support;
 var localRejectState=new RebirthWorldSupportState{GearRevision=6};localRejectState.EquippedGearBySlot["backpack"]="pack";localRejectState.EquippedGearItemDataBySlot["backpack"]=initialData;
 RebirthWorldCharacterService.Record.Support=localRejectState;host.bag=new Bag{Items=new[]{S(2)}};
 RebirthBackpackLibraryReceipt hostRejectedOffer;
 host.PlayerUI.xui.DragAndDropWindow.CurrentStack=S(1);A(!RebirthBackpackLibraryServer.PrepareLocal(host,creation,6,true,0,0,1,true,out hostRejectedOffer)&&localRejectState.PendingLibraryTransfer==null&&host.bag.Items[0].count==2,"host busy cursor creates no intent or item change");host.PlayerUI.xui.DragAndDropWindow.CurrentStack=ItemStack.Empty.Clone();
 host.PlayerUI.xui.IsUsingItemActionEntryUse=true;A(!RebirthBackpackLibraryServer.PrepareLocal(host,creation,6,true,0,0,1,true,out hostRejectedOffer)&&localRejectState.PendingLibraryTransfer==null,"host active inventory use creates no intent");host.PlayerUI.xui.IsUsingItemActionEntryUse=false;
 A(RebirthBackpackLibraryServer.PrepareLocal(host,creation,6,true,0,0,1,true,out hostRejectedOffer)&&localRejectState.PendingLibraryTransfer!=null&&host.bag.Items[0].count==2,"host preparation saves intent without moving inventory");
 A(!RebirthBackpackLibraryServer.PrepareLocal(host,creation,6,true,0,0,1,true,out restored),"host preparation cannot replace pending intent");
  RebirthBackpackLibraryReceipt hostRecovery;
 A(RebirthBackpackLibraryServer.GetLocalRecoveryOffer(host,creation,out hostRecovery)&&XNode.DeepEquals(hostRecovery.ToXml(),hostRejectedOffer.ToXml())&&localRejectState.GearRevision==6&&host.bag.Items[0].count==2,"host recovery reuses exact intent without moving items");
 RebirthWorldCharacterRepository.FailAt=RebirthWorldCharacterRepository.Saves+1;
 A(!RebirthBackpackLibraryServer.GetLocalRecoveryOffer(host,creation,out hostRecovery)&&hostRecovery==null&&localRejectState.PendingLibraryTransfer.TransactionId==hostRejectedOffer.TransactionId,"host recovery save failure keeps intent and sends no offer");RebirthWorldCharacterRepository.FailAt=0;
 A(!RebirthBackpackLibraryServer.GetLocalRecoveryOffer(host,Guid.NewGuid().ToString("N"),out hostRecovery),"host recovery wrong creation refused");
 PlayerDataFile.Loaded=null;A(!RebirthBackpackLibraryServer.AdvanceLocal(host,creation,hostRejectedOffer.TransactionId,false)&&localRejectState.PendingLibraryTransfer!=null,"host rejection needs saved proof");
 PlayerDataFile.Loaded=new PlayerDataFile{bLoaded=true,buffData=new MemoryStream(Evidence(RebirthBackpackLibraryOwnerTransfer.ReceiptKey(hostRejectedOffer),-1))};
 RebirthWorldCharacterRepository.FailAt=RebirthWorldCharacterRepository.Saves+1;
 A(!RebirthBackpackLibraryServer.AdvanceLocal(host,creation,hostRejectedOffer.TransactionId,false)&&localRejectState.GearRevision==6&&localRejectState.PendingLibraryTransfer!=null&&localRejectState.LastLibrarySettlement==null,"host rejected save failure preserves original intent and revision");RebirthWorldCharacterRepository.FailAt=0;
 A(RebirthBackpackLibraryServer.AdvanceLocal(host,creation,hostRejectedOffer.TransactionId,false)&&localRejectState.GearRevision==7&&localRejectState.PendingLibraryTransfer==null&&localRejectState.EquippedGearItemDataBySlot["backpack"]==initialData&&host.bag.Items[0].count==2,"host durable rejection leaves all items unchanged");
 A(RebirthBackpackLibraryServer.TryGetLocalSettlement(host,creation,hostRejectedOffer.TransactionId,out localTerminal)&&!localTerminal.Applied,"host rejected outcome recoverable");
  RebirthBackpackLibraryReceipt hostAppliedOffer;
 A(RebirthBackpackLibraryServer.PrepareLocal(host,creation,7,true,0,0,1,true,out hostAppliedOffer),"host application prepares next operation");
 PlayerDataFile.Loaded=null;RebirthBackpackLibraryOwnerResult hostApplyResult;
 A(!RebirthBackpackLibraryServer.ApplyLocalPending(host,creation,out hostApplyResult)&&hostApplyResult==RebirthBackpackLibraryOwnerResult.Applied&&host.bag.Items[0].count==1&&RebirthBackpackLibraryReservation.IsHeld(host)&&localRejectState.PendingLibraryTransfer!=null,"host applied inventory remains reserved until native saved proof");
 int hostSaves=GameManager.Instance.NativeSaves;A(!RebirthBackpackLibraryServer.AdvanceLocalInteraction(host,creation,out hostApplyResult)&&GameManager.Instance.NativeSaves==hostSaves+1&&RebirthBackpackLibraryReservation.IsHeld(host)&&host.bag.Items[0].count==1,"native save without fresh proof retains host custody");
 GameManager.Instance.SaveHook=()=>PlayerDataFile.Loaded=new PlayerDataFile{bLoaded=true,buffData=new MemoryStream(Evidence(RebirthBackpackLibraryOwnerTransfer.ReceiptKey(hostAppliedOffer),1)),bag=new Slots{Items=new[]{host.bag.Items[0].Clone()}}};
 A(RebirthBackpackLibraryServer.AdvanceLocalInteraction(host,creation,out hostApplyResult)&&host.bag.Items[0].count==1&&!RebirthBackpackLibraryReservation.IsHeld(host)&&localRejectState.GearRevision==8&&localRejectState.PendingLibraryTransfer==null,"host native checkpoint with verified fresh proof settles without repeated debit");GameManager.Instance.SaveHook=null;
 RebirthWorldCharacterService.Record.Support=localOriginalSupport;PlayerDataFile.Loaded=localSavedEvidence;
 GameManager.Instance.World=localSettlementWorld;
 A(!RebirthBackpackLibraryServer.Advance(serverPlayer,sender,creation,serverOffer.TransactionId,true)&&state.GearRevision==6,"server terminal replay no second commit");
 RebirthBackpackLibrarySettlement serverSettlement;int settlementSaves=RebirthWorldCharacterRepository.Saves;
 A(RebirthBackpackLibraryServer.TryGetSettlement(serverPlayer,sender,creation,serverOffer.TransactionId,out serverSettlement)&&serverSettlement.Applied&&serverSettlement.GearRevision==6&&RebirthWorldCharacterRepository.Saves==settlementSaves,"lost reply can query durable outcome without second commit or save");
 sender.entityId=2;A(!RebirthBackpackLibraryServer.TryGetSettlement(serverPlayer,sender,creation,serverOffer.TransactionId,out serverSettlement),"settlement wrong owner refused");sender.entityId=1;
 A(!RebirthBackpackLibraryServer.TryGetSettlement(serverPlayer,sender,creation,Guid.NewGuid().ToString("N"),out serverSettlement),"settlement wrong transaction refused");
 var settleManager=new ConnectionManager{IsServer=true};SingletonMonoBehaviour<ConnectionManager>.Instance=settleManager;
 var settleRetry=new NetPackageRebirthBackpackLibrarySettleRequest().Setup(1,Guid.Parse(creation),Guid.Parse(serverOffer.TransactionId),true);settleRetry.Sender=sender;
 int beforeSettlementRetrySaves=RebirthWorldCharacterRepository.Saves;
 settleRetry.ProcessPackage(serverPlayer.world,GameManager.Instance);settleRetry.ProcessPackage(serverPlayer.world,GameManager.Instance);
 A(settleManager.Sent.Count==2&&settleManager.Sent.All(p=>p is NetPackageRebirthBackpackLibrarySettled)&&settleManager.Targets.All(id=>id==1)&&RebirthWorldCharacterRepository.Saves==beforeSettlementRetrySaves&&state.GearRevision==6,"lost settled reply retries send durable outcome without save or move");
 sender.entityId=2;settleRetry.ProcessPackage(serverPlayer.world,GameManager.Instance);A(settleManager.Sent.Count==2,"settle wrong sender sends no result");sender.entityId=1;
 var wrongOutcome=new NetPackageRebirthBackpackLibrarySettleRequest().Setup(1,Guid.Parse(creation),Guid.Parse(serverOffer.TransactionId),false);wrongOutcome.Sender=sender;wrongOutcome.ProcessPackage(serverPlayer.world,GameManager.Instance);A(settleManager.Sent.Count==2,"settle opposite claimed result cannot confirm");
 using(var stream=new MemoryStream(new byte[]{1,0,0,0}))using(var reader=new PooledBinaryReader(stream)){bool refused=false;try{settleRetry.read(reader);}catch(EndOfStreamException){refused=true;}catch(InvalidDataException){refused=true;}A(refused,"truncated settlement request refused");}
 settleRetry.ProcessPackage(serverPlayer.world,GameManager.Instance);A(settleManager.Sent.Count==2,"failed pooled settlement read cannot replay prior intent");
 A(RebirthBackpackLibraryServer.GetView(serverPlayer,sender,creation,out serverView)&&serverView.GearRevision==6&&!serverView.TransferPending&&serverView.OccupiedSlots==1&&serverView.TryGetSlot(0,out viewSlot)&&viewSlot.count==2,"settled library display reads committed contents");
 var savedPackItem=state.EquippedGearBySlot["backpack"];var savedPackData=state.EquippedGearItemDataBySlot["backpack"];state.EquippedGearBySlot.Remove("backpack");state.EquippedGearItemDataBySlot.Remove("backpack");long emptyViewRevision;
 A(RebirthBackpackLibraryServer.GetViewStatus(serverPlayer,sender,creation,out serverView,out emptyViewRevision)==RebirthBackpackLibraryViewStatus.NoBackpack&&serverView==null&&emptyViewRevision==6,"authoritative missing backpack distinct empty response");
 state.EquippedGearBySlot["backpack"]=savedPackItem;A(RebirthBackpackLibraryServer.GetViewStatus(serverPlayer,sender,creation,out serverView,out emptyViewRevision)==RebirthBackpackLibraryViewStatus.Unavailable&&emptyViewRevision==-1,"missing physical image not reported empty");state.EquippedGearItemDataBySlot["backpack"]=savedPackData;
 var reserved=new EntityPlayerLocal{bag=new Bag{Items=new[]{S(2),S(2)}},inventory=new Slots{Items=new[]{ItemStack.Empty.Clone()}}};reserved.world.Player=reserved;GameManager.Instance.World=reserved.world;
 A(Prefix(null)&&Prefix(new XUiC_ItemStack()),"no player cursor unchanged");
 var cursor=new XUiC_ItemStack{xui=new XUi{playerUI=new PlayerUI{entityPlayer=reserved}}};
 A(Prefix(cursor),"unreserved cursor allowed");
 var action=new BaseItemActionEntry{ItemController=cursor};A(ActionPrefix(action)&&GameManager.Tooltips==0,"unreserved item action allows");
 reserved.inventory.ActionRunning=true;A(!RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt)&&!RebirthBackpackLibraryReservation.IsHeld(reserved),"running held action prevents new reservation");reserved.inventory.ActionRunning=false;
 reserved.PlayerUI.xui.IsUsingItemActionEntryUse=true;A(!RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt),"running inventory use prevents reservation");reserved.PlayerUI.xui.IsUsingItemActionEntryUse=false;
 reserved.PlayerUI.xui.DragAndDropWindow.CurrentStack=S(1);A(!RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt),"occupied cursor prevents reservation");reserved.PlayerUI.xui.DragAndDropWindow.CurrentStack=ItemStack.Empty.Clone();
 RemoteResourceClientTransactionCoordinator.pendingLeases.Add(1,new object());
 A(RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(reserved)&&
   !RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt),"pending remote lease prevents new library custody");
 var foreignGrantOwner=new EntityPlayerLocal{world=reserved.world};
 A(!RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(foreignGrantOwner),"remote lease gate excludes foreign owner");
 RemoteResourceClientTransactionCoordinator.pendingLeases.Clear();RemoteResourceClientTransactionCoordinator.outcomeDeliveryDepth=1;
 A(RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(reserved)&&
   !RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt),"terminal delivery without lease still prevents new library custody");
 RemoteResourceClientTransactionCoordinator.outcomeDeliveryDepth=0;RemoteResourceClientGrantContext.Begin(new List<ItemStack>{S(1)});
 A(!RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt),"active grant prevents new library custody");
 RemoteResourceClientGrantContext.End();
 Console.WriteLine("PASS pending resource admission: actual owner/world gate and library reservation reject lease, terminal delivery and active grant; lease/delivery fields doubled.");
 A(RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt),"reservation acquire");
 A(!Prefix(cursor),"reserved cursor mutation refused before body");
 A(RebirthBackpackLibraryReservation.BlocksResourceUse(reserved),"local reservation blocks direct resource planners");
 var materialModel=new XUiM_PlayerInventory{localPlayer=reserved};bool materialsAvailable=true;
 A(!MaterialPrefix(materialModel,ref materialsAvailable)&&!materialsAvailable,"reserved native material admission refuses before debit");
 A(!RebirthBackpackLibraryReservation.BlocksHeldUse(reserved),"bag reservation does not block held toolbelt");
 var cookingNeeds=new List<ItemStack>{S(1)};var cookingRemoved=new List<ItemStack>{S(1,"existing")};int cookingWrites=reserved.bag.Writes;
 A(RebirthCookingLocalIngredients.Count(reserved,S(1).itemValue)==0&&!RebirthCookingLocalIngredients.Take(reserved,cookingNeeds,cookingRemoved)&&reserved.bag.Writes==cookingWrites&&cookingRemoved.Count==1&&cookingRemoved[0].itemValue.Payload=="existing","reserved actual cooking count/debit refuses without inventory or removallist change");

 var nativeBag=(Bag)reserved.bag;var incoming=S(3);(bool anyMoved,bool allMoved) merge=(false,false);
 A(!PartialStackPrefix(nativeBag,0,incoming,ref merge)&&merge.anyMoved&&merge.allMoved&&incoming.count==0&&nativeBag.Items[0].count==2&&nativeBag.Items[1].count==5&&nativeBag.Events==1,"partial stack skips reserved slot and fills other slot with exact remainder/event");
 incoming=S(3);merge=(true,true);A(!PartialStackPrefix(nativeBag,0,incoming,ref merge)&&!merge.anyMoved&&!merge.allMoved&&incoming.count==3&&nativeBag.Items[0].count==2&&nativeBag.Events==1,"no unreserved capacity preserves incoming stack");
 A(PartialStackPrefix(new Bag{Items=new[]{S(1)}},0,incoming,ref merge),"unreserved bag retains native method");
 bool added=true;incoming=S(2);A(!BagAddPrefix(nativeBag,incoming,ref added)&&!added&&incoming.count==2&&nativeBag.Items[0].count==2,"normal add refuses reserved-only capacity without debit");
 nativeBag.Items[1]=ItemStack.Empty.Clone();A(!BagAddPrefix(nativeBag,incoming,ref added)&&added&&ReferenceEquals(nativeBag.Items[1],incoming)&&incoming.count==2&&nativeBag.Items[0].count==2&&nativeBag.Events==2,"normal add places exact native input in unreserved empty slot");
 incoming=S(2);A(!BagAddPrefix(nativeBag,incoming,ref added)&&added&&incoming.count==0&&nativeBag.Items[1].count==4&&nativeBag.Items[0].count==2&&nativeBag.Events==3,"normal add whole merge consumes exact remainder only in other slot");


 int reservedIndex;A(RebirthBackpackLibraryReservation.TryGetReservedSlot(reserved.bag,true,out reservedIndex)==(reserved.bag!=null),"reserved bag instance lookup");
 A(!RebirthBackpackLibraryReservation.TryGetReservedSlot(new Slots(),true,out reservedIndex),"different inventory object not reserved");

 A(!ActionPrefix(action)&&GameManager.Tooltips==1,"reserved item action refuses with pending notice");
 var creative=new XUiC_Creative2Stack{xui=cursor.xui};var learning=S(1);
 A(!StudyInstant(reserved,learning,false,creative)&&creative.xui.PlayerInventory.Adds==0&&Dispatches==0,"reserved creative study cannot copy or dispatch");
 A(!AudioInstant(reserved,learning,false,cursor)&&!SupportInstant(reserved,learning,false,cursor)&&Dispatches==0&&learning.count==1,"reserved audio/support no dispatch or debit");

 A(RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt)&&RebirthBackpackLibraryReservation.IsHeld(reserved),"reservation same intent retry");
 A(!RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,withdraw),"reservation conflicting intent refused");
 A(!RebirthBackpackLibraryReservation.ReleaseSettled(reserved,ownerCreation,receipt,r=>false)&&RebirthBackpackLibraryReservation.IsHeld(reserved),"timeout not settlement");
 A(!RebirthBackpackLibraryReservation.ReleaseSettled(reserved,ownerCreation,receipt,r=>{throw new Exception();})&&RebirthBackpackLibraryReservation.IsHeld(reserved),"settlement exception retains reservation");
 A(RebirthBackpackLibraryReservation.ReleaseSettled(reserved,ownerCreation,receipt,r=>true)&&!RebirthBackpackLibraryReservation.IsHeld(reserved),"authoritative settlement releases");
 A(Prefix(cursor),"settled cursor allowed");
 A(!RebirthBackpackLibraryReservation.BlocksResourceUse(reserved),"settled resource use resumes");
 A(RebirthCookingLocalIngredients.Count(reserved,S(1).itemValue)==6,"unreserved actual cooking counts full local materials");
 cookingNeeds=new List<ItemStack>{S(4)};
 A(RebirthCookingLocalIngredients.Take(reserved,cookingNeeds,cookingRemoved)&&cookingRemoved.Count==3&&cookingRemoved.Skip(1).Sum(s=>s.count)==4&&reserved.bag.GetSlots().Sum(s=>s.count)==2,"unreserved actual cooking aggregate debit and removed provenance conserved");
 cookingWrites=reserved.bag.Writes;cookingNeeds=new List<ItemStack>{S(99)};var insufficient=new List<ItemStack>();
 A(!RebirthCookingLocalIngredients.Take(reserved,cookingNeeds,insufficient)&&insufficient.Count==0&&reserved.bag.Writes==cookingWrites,"insufficient actual cooking no partial debit");

 RebirthWorldCharacterService.Record.Support.PendingLibraryTransfer=receipt;
 A(RebirthBackpackLibraryReservation.BlocksResourceUse(serverPlayer),"authoritative pending intent blocks remote-owner planners");
 A(!RebirthCookingLocalIngredients.Take(serverPlayer,cookingNeeds,insufficient)&&insufficient.Count==0,"authoritative pending remote owner cooking refused before slots");
 RebirthWorldCharacterService.Record.Support.PendingLibraryTransfer=null;
 A(MaterialPrefix(materialModel,ref materialsAvailable),"settled native material admission restored");
 A(ActionPrefix(action)&&GameManager.Tooltips==1,"settled item action allows without notice");
 A(StudyInstant(reserved,learning,false,creative)&&creative.xui.PlayerInventory.Adds==1&&Dispatches==1,"unreserved creative ownership and dispatch preserved");
 A(AudioInstant(reserved,learning,false,cursor)&&SupportInstant(reserved,learning,false,cursor)&&Dispatches==3,"unreserved audio/support dispatch preserved");
 A(RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt),"reservation reacquire");
 RebirthBackpackLibraryReservation.ForgetWorld(new PlayerWorld());A(RebirthBackpackLibraryReservation.IsHeld(reserved),"other world cleanup retains reservation");
 RebirthBackpackLibraryReservation.ForgetWorld(reserved.world);A(!RebirthBackpackLibraryReservation.IsHeld(reserved),"exact world cleanup");
 reserved.inventory=new Inventory{entity=reserved,Items=new[]{S(2),S(1),S(5),S(5),ItemStack.Empty.Clone()}};
 A(RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,withdraw),"belt reservation setup");
 bool beltPlaced=true;var beltInput=S(2);
 A(!BeltPlacementPrefix((Inventory)reserved.inventory,0,ref beltPlaced)&&!beltPlaced&&beltInput.count==2&&reserved.inventory.Items[0].count==2,"reserved explicit belt placement refuses without item debit");
 A(BeltPlacementPrefix((Inventory)reserved.inventory,1,ref beltPlaced),"other explicit belt slot native placement allowed");
 A(BeltPlacementPrefix(new Inventory(),0,ref beltPlaced),"other inventory explicit placement unchanged");
 var belt=(Inventory)reserved.inventory;int addSlot=-99;bool addOk=false;var newBeltItem=S(2);
 A(!BeltAddPrefix(belt,newBeltItem,0,belt.Length,ref addSlot,ref addOk)&&addOk&&addSlot==1&&belt.Items[0].count==2&&belt.Items[1].count==3&&newBeltItem.count==2&&belt.Events==1,"normal belt merge skips reserved and preserves native input count");
 belt.Items[1]=S(5);newBeltItem=S(2);A(!BeltAddPrefix(belt,newBeltItem,0,belt.Length,ref addSlot,ref addOk)&&!addOk&&addSlot==-1&&newBeltItem.count==2&&belt.Items[4].IsEmpty(),"normal belt add excludes dummy and preserves refused input");
 belt.Items[1]=ItemStack.Empty.Clone();A(!BeltAddPrefix(belt,newBeltItem,0,belt.Length,ref addSlot,ref addOk)&&addOk&&addSlot==1&&belt.Items[1].count==2&&belt.Items[0].count==2,"normal belt empty placement uses other owned slot");
 RebirthToolbeltCapacity.Owned=1;newBeltItem=S(1);A(!BeltAddPrefix(belt,newBeltItem,0,belt.Length,ref addSlot,ref addOk)&&!addOk&&newBeltItem.count==1,"normal belt add excludes unowned slots");RebirthToolbeltCapacity.Owned=4;

 newBeltItem=S(4);merge=(false,false);int eventsBefore=belt.Events;
 A(!BeltPartialPrefix(belt,0,newBeltItem,ref merge)&&merge.anyMoved&&!merge.allMoved&&newBeltItem.count==1&&belt.Items[0].count==2&&belt.Items[1].count==5&&belt.Events==eventsBefore+1,"partial belt merge skips reserved and keeps exact remainder/event");
 RebirthToolbeltCapacity.Owned=1;newBeltItem=S(1);merge=(true,true);A(!BeltPartialPrefix(belt,0,newBeltItem,ref merge)&&!merge.anyMoved&&!merge.allMoved&&newBeltItem.count==1,"partial belt excludes reserved/unowned capacity");RebirthToolbeltCapacity.Owned=4;
 reserved.inventory.holdingItemIdx=0;A(RebirthBackpackLibraryReservation.BlocksHeldUse(reserved)&&RebirthBackpackLibraryReservation.IsReservedSlot(reserved,false,0),"exact reserved belt use blocked");
 reserved.inventory.holdingItemIdx=1;A(!RebirthBackpackLibraryReservation.BlocksHeldUse(reserved)&&!RebirthBackpackLibraryReservation.IsReservedSlot(reserved,true,0),"other belt slot and bag unaffected");
 RebirthBackpackLibraryReservation.ResetSession();
 A(RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt),"shutdown reservation setup");
 state.PendingLibraryTransfer=receipt;
 RebirthBackpackLibraryReservation.ResetSession();
 A(!RebirthBackpackLibraryReservation.IsHeld(reserved)&&state.PendingLibraryTransfer==receipt,"session reset leaves durable custody intent untouched");
 A(RebirthBackpackLibraryReservation.TryAcquire(reserved,ownerCreation,receipt),"same durable intent can rehydrate session after reset");
 RebirthBackpackLibraryReservation.ResetSession();
 Console.WriteLine("PASS actual library transfer/conservation/receipt/persistence/journal/owner/saved evidence/server: item balance, detached images, tamper bounds, phases, scope, save failures, duplicate commit and rejection; native codecs/save/network adapters doubled.");
 }
}







