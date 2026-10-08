using System.IO;using System.Reflection;using System;using System.Collections;using System.Collections.Generic;using System.Linq;
struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 Lerp(Vector2 a,Vector2 b,float t){return new Vector2(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t);}public static float Distance(Vector2 a,Vector2 b){return (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}public static explicit operator Vector2(Vector3 a){return new Vector2(a.x,a.y);}}
struct Vector3 {public static Vector3 up{get{return new Vector3(0,1);}}public float magnitude{get{return (float)Math.Sqrt(sqrMagnitude);}}public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}public float x,y,z;public Vector3(float a,float b,float c=0){x=a;y=b;z=c;}public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}public float sqrMagnitude{get{return x*x+y*y+z*z;}}}
static class Mathf {public const float Rad2Deg=57.29578f;public static int FloorToInt(float n){return (int)Math.Floor(n);}public static float Atan2(float a,float b){return (float)Math.Atan2(a,b);}public static float Abs(float a){return Math.Abs(a);}public static float DeltaAngle(float a,float b){return b-a;}public static float Max(float a,float b){return Math.Max(a,b);}public static float Min(float a,float b){return Math.Min(a,b);}public static float Clamp(float a,float min,float max){return Math.Max(min,Math.Min(max,a));}}
class WaitForSeconds {public readonly float Seconds;public WaitForSeconds(float seconds){Seconds=seconds;}}
static class Time {public static int frameCount;public static float realtimeSinceStartup;}
static class Application {public static bool isFocused=true;}
static class Input {public static Vector3 mousePosition;}
class NativeCamera {public Action Project;public Vector3 ScreenToWorldPoint(Vector2 p){Project?.Invoke();return new Vector3(p.x,p.y);}}class UICamera{public NativeCamera Camera=new NativeCamera();public Action GetCamera;public NativeCamera cachedCamera{get{GetCamera?.Invoke();return Camera;}}}class SoftCursor{public UICamera Camera=new UICamera();public Action GetUiCamera;public UICamera uiCamera{get{GetUiCamera?.Invoke();return Camera;}}public int Writes;public Vector3 LastPosition;public Vector3 Position{set{LastPosition=value;Writes++;}}}
class PlayerAction {public Action OnUpdate;public string Name;public int Updates;public float Last;public void UpdateWithValue(float value,ulong tick,float delta){Updates++;Last=value;OnUpdate?.Invoke();}}
class Permanent {public PlayerAction Inventory=new PlayerAction{Name="Inventory"};}
class PlayerActionsLocal {public PlayerAction Primary=new PlayerAction{Name="Primary"},Activate=new PlayerAction{Name="Activate"},MoveForward=new PlayerAction{Name="Forward"},Run=new PlayerAction{Name="Run"},MoveLeft=new PlayerAction{Name="Left"},MoveRight=new PlayerAction{Name="Right"},Jump=new PlayerAction{Name="Jump"};public Permanent PermanentActions=new Permanent();}
static class RebirthGameBridgeInput {
 private static readonly Dictionary<PlayerAction,Held> held=new Dictionary<PlayerAction,Held>();public static float Pace=.55f;public static bool CursorActive{get;private set;}public static Vector2 CursorPosition{get;private set;}private static Vector3 realMouseAtActivation;
 public static PlayerAction Click=new PlayerAction{Name="gui.LeftClick"},Slot=new PlayerAction{Name="InventorySlot2"};public static PlayerActionsLocal Local=new PlayerActionsLocal();public static PlayerActionsLocal LocalActions(){return Local;}public static IEnumerable<PlayerAction> FindByKey(string key){return new[]{Slot};}public static PlayerAction Find(string name){if(name=="gui.LeftClick")return Click;Slot.Name=name;return Slot;}
 // PRODUCTION_INPUT
 public static void Tick(PlayerAction action){UpdateBindingsPrefix(action,1,.1f);}public static bool CursorCheck(){return CheckCursorActive();}public static Vector2 Screen(){var p=new Vector2(900,900);GetScreenPositionPostfix(ref p);return p;}public static void MoveCursor(SoftCursor cursor){HandleMovementPostfix(cursor);}public static bool NativeShift(bool physical){ShiftPostfix(ref physical);return physical;}
}
class ItemClass {public string Name="crate";public int Type=1;public string GetItemName(){return Name;}public string GetLocalizedItemName(){return Name;}public static ItemValue GetItem(string name,bool unused){return new ItemValue{ItemClass=new ItemClass{Name=name}};}}
class ItemValue {public float UseTimes,MaxUseTimes=100;public string Variant="a";public int type=1;public ItemClass ItemClass=new ItemClass();public ItemValue(){}public ItemValue(int type,int a,int b,bool c){this.type=type;}public static void Write(ItemValue v,BinaryWriter w){w.Write(v.type);w.Write(v.UseTimes);w.Write(v.MaxUseTimes);w.Write(v.Variant??"");w.Write(v.ItemClass?.Name??"");}public bool IsEmpty(){return false;}}
class ItemStack {public static ItemStack[] Clone(ItemStack[] a){return a.Select(s=>s==null?null:s.Clone()).ToArray();}public int count=1;public ItemValue itemValue=new ItemValue();public bool IsEmpty(){return count<=0;}public ItemStack Clone(){return new ItemStack{count=count,itemValue=new ItemValue{type=itemValue.type,ItemClass=itemValue.ItemClass,UseTimes=itemValue.UseTimes,MaxUseTimes=itemValue.MaxUseTimes,Variant=itemValue.Variant}};}public ItemStack(){}public ItemStack(ItemValue value,int count){itemValue=value;}}
class Hand {public bool Fault;public int Changes;public void OnSlotChanged(int slot){if(Fault)throw new InvalidOperationException();Changes++;}}
class Inventory {public int holdingItemIdx,SelectedSlot;public Hand Hand=new Hand();public Grid ItemGrid;public Inventory(){ItemGrid=new Grid{items=Items};}public ItemStack[] Items=Enumerable.Range(0,20).Select(x=>new ItemStack{count=0}).ToArray();public ItemStack GetItem(int slot){return Items[slot];}}
class Grid{public ItemStack[] items;}class Bag {public Grid ItemGrid=new Grid{items=new[]{new ItemStack()}};public bool AddItem(ItemStack stack){return true;}}
class World {public TEFeatureStorage Storage;public TEFeatureRebirthPoiCrateIdentity Marker;public float GetTerrainHeight(int x,int z){return 0;}public BlockValue GetBlock(Vector3i p){return Block;}public BlockValue Block=new BlockValue();public BlockValue GetBlock(int x,int y,int z){return Block;}public EntityPlayerLocal Primary;public Action PrimaryRead;public EntityPlayerLocal GetPrimaryPlayer(){var p=Primary;PrimaryRead?.Invoke();return p;}} class GameManager {public static GameManager Instance=new GameManager();public World World;} class EntityPlayerLocal {public Vector3 position,rotation;public bool onGround=true;public Stats Stats=new Stats();public World world=new World();public Inventory inventory=new Inventory();public Bag bag=new Bag();public bool Dead;public bool IsDead(){return Dead;}}
class ToolbeltConfig {public static ToolbeltConfig CurrentConfig=new ToolbeltConfig();public int SlotsPerBar=10;}
static class RebirthGameBridgeNeeds {
 public static int CountItem(EntityPlayerLocal p,string name){return p.bag.ItemGrid.items.Where(s=>s!=null&&!s.IsEmpty()&&s.itemValue.ItemClass.Name==name).Sum(s=>s.count);}public static bool Supply=false,Assigned;public static int ToolbeltSize(EntityPlayerLocal p){return 20;}private static string HeldName(EntityPlayerLocal p){return p.inventory.GetItem(p.inventory.holdingItemIdx).itemValue.ItemClass.GetItemName();}public static bool HandsBusy(EntityPlayerLocal p){return false;}public static IEnumerator WaitHandsFree(EntityPlayerLocal p,float timeout){yield return null;}private static IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
 public static bool RealSlots;public static int ToolbeltSlot(EntityPlayerLocal p,Func<ItemClass,bool> want,out string name,Func<ItemStack,bool> match=null){name="crate";if(!RealSlots)return Assigned?0:-1;for(int i=0;i<20;i++){var s=p.inventory.GetItem(i);if(!s.IsEmpty()&&want(s.itemValue.ItemClass)&&(match==null||match(s))){name=s.itemValue.ItemClass.GetItemName();return i;}}return -1;}private static string BestInBag(EntityPlayerLocal p,Func<ItemClass,bool> want,Func<ItemStack,bool> match){if(!RealSlots)return "crate";foreach(var s in p.bag.ItemGrid.items)if(s!=null&&!s.IsEmpty()&&want(s.itemValue.ItemClass)&&(match==null||match(s)))return s.itemValue.ItemClass.GetItemName();return null;}private static int FreeableToolbeltSlot(EntityPlayerLocal p,Func<ItemClass,bool> want){return 0;}
 // PRODUCTION_NEEDS
}
static class RebirthGameBridgeUi {
 public static object OpenWindowIds(){return null;}public static bool AnyModalOpen(){return false;}public static IEnumerator LookAtWindow(){yield return null;}public static IEnumerator CloseMenus(){yield return null;}public static bool TryFindEmptyToolbeltSlot(out Vector2 point,out int slot){point=new Vector2(20,20);slot=0;return true;}public static bool TryFindToolbeltSlot(int slot,out Vector2 point){point=new Vector2();return true;}public static bool TryFindEmptyBackpackSlot(out Vector2 point){point=new Vector2();return true;}public static bool TryFindItemSlot(string name,bool belt,out Vector2 point,Func<ItemStack,bool> match){point=new Vector2(10,10);return true;}public static ItemStack CursorItem;public static ItemStack HeldItem(){return CursorItem;}public static bool IsWindowOpen(string window){return false;}public static bool TryFindById(string id,out Vector2 p){p=new Vector2();return false;}public static bool TryFindByText(string window,string text,out Vector2 p){p=new Vector2();return false;}public static string HeldItemName(){return CursorItem==null?null:"carried";}
 public class Rect {public Vector2 center;}
 public class NativeGrid {public Action Visibility;public bool HasVisibleCenter(int i){Visibility?.Invoke();return true;}}
 public class NativeUi {public PlayerUi playerUI;}public class PlayerUi{public EntityPlayerLocal entityPlayer;}
 public class XUiC_Backpack{}public class XUiC_RebirthCraftingInventoryScroll{public bool IsSlotVisible(int i){return true;}}
 public class NativeView {public Action ReadVisible;public bool Visible=true;public bool IsVisible{get{ReadVisible?.Invoke();return Visible;}}}
 public class XUiC_TextInput{public NativeUi xui;public NativeView ViewComponent=new NativeView();public int Sets,Triggers;public Action SetText,Trigger;public string Text{set{Sets++;SetText?.Invoke();}}public void TriggerOnChangeHandler(bool ignored){Triggers++;Trigger?.Invoke();}}
 public static string TextId="rebirthCraftingRecipeSearch";public static int RecipeLookups;public static bool TryFindDepositSlot(TEFeatureStorage s,out Vector2 point,Func<ItemStack,bool> retain=null){point=new Vector2(1,1);return true;}public static bool TryFindRecipe(string window,string item,out Vector2 point){RecipeLookups++;point=new Vector2(1,1);return true;}public static XUiC_TextInput TextControl;public static XUiC_RebirthContextSlot ContextControl;static T FindAncestor<T>(object o) where T:class{return null;}static T FindDescendant<T>(object o) where T:class{return null;}
 public class XUiC_ItemStack {public enum StackLocationTypes{ToolBelt,Backpack,LootContainer}public StackLocationTypes StackLocation;public NativeUi xui;public bool Foreign;public ItemStack ItemStack;public int SlotNumber;public T GetParentByType<T>() where T:class {return !Foreign&&typeof(T)==typeof(XUiC_Backpack)?new XUiC_Backpack() as T:null;}}
 public class XUiC_RebirthContextSlot:XUiC_ItemStack {public bool IsBackpack,IsPresented;public int Scrolls;public Action Scroll;public NativeGrid Grid=new NativeGrid();public void Scrolled(float d){Scrolls++;Scroll?.Invoke();}}
 public class FakeJson{readonly Dictionary<string,object> values=new Dictionary<string,object>();public object this[string key]{get{object v;return values.TryGetValue(key,out v)?v:null;}set{values[key]=value;}}}public class Node {public FakeJson Json=new FakeJson();public object Controller;public bool HasRect=true;public string Path;public Rect ScreenRect;}
 public static float SlotOffset;public static bool HideSlot,ForeignOnly,ForeignSpoofLocation;
 static IEnumerable<Node> Collect(string path,bool all){var p=GameManager.Instance.World?.GetPrimaryPlayer();if(p==null)yield break;if(path=="crafting"&&TextControl!=null){var n=new Node{Controller=TextControl};n.Json["id"]=TextId;yield return n;yield break;}if(path==null&&ContextControl!=null){yield return new Node{Controller=ContextControl};yield break;}if(path=="dragAndDrop"){if(CursorItem!=null)yield return new Node{Path="dragAndDrop/0",Controller=new XUiC_ItemStack{xui=new NativeUi{playerUI=new PlayerUi{entityPlayer=p}},ItemStack=CursorItem},ScreenRect=new Rect()};yield break;}if(HideSlot)yield break;for(int group=0;group<2;group++){var slots=group==0?p.inventory.ItemGrid.items:p.bag.ItemGrid.items;for(int i=0;i<slots.Length;i++)yield return new Node{Path=(group==0?"toolbelt/":"bag/inventory/")+i,Controller=new XUiC_ItemStack{xui=new NativeUi{playerUI=new PlayerUi{entityPlayer=p}},Foreign=ForeignOnly,StackLocation=ForeignOnly&&!ForeignSpoofLocation?XUiC_ItemStack.StackLocationTypes.LootContainer:group==0?XUiC_ItemStack.StackLocationTypes.ToolBelt:XUiC_ItemStack.StackLocationTypes.Backpack,ItemStack=slots[i],SlotNumber=i},ScreenRect=new Rect{center=new Vector2((group==0?20:10)+i*2+SlotOffset,group==0?20:10)}};}}
 // PRODUCTION_UI
}
struct Vector3i {public Vector3 ToVector3(){return new Vector3(x,y,z);}public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
class Stats {public Stat Stamina=new Stat();}class Stat{public float Value=100,ModifiedMax=100;}
class Material {public string id="wood",DamageCategory="wood";}class Block {public Material blockMaterial=new Material();public string GetBlockName(){return "cntWoodWritableCrate";}public string GetActivationText(World w,BlockValue b,Vector3i pos,EntityPlayerLocal p){return "Locked";}}
class BlockValue {public bool isair;public Block Block=new Block();}
class BridgeRequest {public string Method,Path;public bool IsAbandoned;public void Complete(object value,int code=200){}}
class JObject:Dictionary<string,object>{}
static class RebirthGameBridgeTerrain {public class Line{public bool JumpNeeded,Safe=true;public float SafeLength=3;public string Why;}public static Action Probe;public static Vector3 BestDirection(Vector3 p,Vector3 to,float length,out Line line){Probe?.Invoke();line=new Line();return to;}}
class RebirthGameBridgePoiStorage {
 public const string CrateName="cntWoodWritableCrate";
 private readonly List<Vector3i> crates=new List<Vector3i>();
 private readonly Dictionary<Vector3i,TEFeatureStorage> placedCrateFeatures=new Dictionary<Vector3i,TEFeatureStorage>();
 private readonly Dictionary<Vector3i,TileEntityComposite> placementParents=new Dictionary<Vector3i,TileEntityComposite>();
 private readonly Dictionary<Vector3i,TEFeatureRebirthPoiCrateIdentity> placementMarkers=new Dictionary<Vector3i,TEFeatureRebirthPoiCrateIdentity>();
 private readonly Dictionary<Vector3i,Guid> placedCrateIds=new Dictionary<Vector3i,Guid>();
 private readonly HashSet<Guid> publishedPlacementIds=new HashSet<Guid>();
 private readonly HashSet<string> retainedSupplyKeys=new HashSet<string>();
 private PrefabInstance boundPoi;private World custodyWorld;private bool expectationsLoaded=true;private string expectationFailure,recoveryFailure;
 private Vector3? anchor=new Vector3();public string Failure{get;private set;}public bool RetryAfterThreat{get;private set;}
 private RebirthGameBridgePoiTransferPhase activeTransferPhase;public RebirthGameBridgePoiTransferPhase Phase{get{return activeTransferPhase;}}
 public RebirthGameBridgePoiStorage(EntityPlayerLocal p,PrefabInstance poi){custodyWorld=p.world;boundPoi=poi;crates.Add(new Vector3i());placedCrateIds[new Vector3i()]=p.world.Marker.PlacementId;publishedPlacementIds.Add(p.world.Marker.PlacementId);}
 public IEnumerator RefreshExpectations(EntityPlayerLocal p,PrefabInstance poi){yield break;}public bool Recover(EntityPlayerLocal p,PrefabInstance poi){return true;}
 private bool HasCargo(EntityPlayerLocal p){return p.bag.ItemGrid.items.Any(s=>s!=null&&!s.IsEmpty());}private bool FitsCargo(EntityPlayerLocal p,TEFeatureStorage t){return t!=null;}
 private bool RetainSupply(ItemStack s){return false;}private void RememberDisplacedSupplies(ItemStack[] s,EntityPlayerLocal p){}
 private static bool IsPlacementSite(EntityPlayerLocal p,Vector3i s,Vector3 e){return false;}private static bool HasReadyPlacementItem(EntityPlayerLocal p,int slot){return false;}
 private static bool IsNewPlacementWitness(EntityPlayerLocal p,Vector3i s,TEFeatureStorage t,TEFeatureRebirthPoiCrateIdentity m){return false;}
 private static TEFeatureStorage Loot(EntityPlayerLocal p,Vector3i s){return p.world.Storage;}
 private static string CompletionFailure(string f,bool dead,bool cargo){return f;}
 private static string ConservationKey(ItemValue v){return v.type+"/"+v.Variant+"/"+v.UseTimes;}
 // PRODUCTION_DEPOSIT
 // PRODUCTION_THREAT
 // PRODUCTION_CONTEXT
 // PRODUCTION_CURSOR
 // PRODUCTION_SNAPSHOT
 // PRODUCTION_CUSTODY
 // PRODUCTION_PLACED
 // PRODUCTION_TARGET
 // PRODUCTION_COUNTS
public static string OwnedItemKey(ItemStack s){return s==null||s.IsEmpty()?null:s.itemValue.ItemClass.Name+"/"+s.itemValue.Variant+"/"+s.itemValue.UseTimes;}private static string RestoreKey(ItemStack s){return OwnedItemKey(s);}
// PRODUCTION_WEAR
// PRODUCTION_CRAFT
}
static class RebirthGameBridgePlayer {
static int activeMoves,movementGeneration;public static string LastWalkResult;public static int Turns;public static bool Open,DoorPresent;public static float SmoothTurn(EntityPlayerLocal p,float yaw,float pitch,float time=.12f){Turns++;p.rotation=new Vector3(pitch,yaw);return 0;}public static float SmoothAimAt(EntityPlayerLocal p,Vector3 point,float time=.12f){Turns++;return 0;}public static bool DoorIsOpen(EntityPlayerLocal p,Vector3i b){return Open;}public static bool DoorAhead(EntityPlayerLocal p,out Vector3i b){b=new Vector3i();return DoorPresent;}static int BlockHitPoints(EntityPlayerLocal p,Vector3i b){return 100;}static string StripColors(string s){return s;}static bool CrosshairOnBlock(EntityPlayerLocal p,Vector3i b){return true;}static JObject DescribeTarget(EntityPlayerLocal p){return new JObject();}static object Vec(Vector3 p){return p;}static float Round(float f){return f;}
public static IEnumerator TurnToRoutine(EntityPlayerLocal p,Func<Vector3> point,float yaw,float pitch,float seconds,RebirthGameBridgeInput.OwnedInputScope scope){yield break;}
public static IEnumerator ApproachAndActivate(EntityPlayerLocal p,Vector3i b,Action<string> log,float range,float timeout,RebirthGameBridgeInput.OwnedInputScope scope){yield break;}
}
static class RebirthGameBridgePath {public class Step{public Vector3 Pos;public bool Door;public Vector3i DoorBlock;}public static string LastFailure;public static List<Step> Planned;public static Action Planning;static List<Step> Plan(EntityPlayerLocal p,Vector3 from,Vector3 to){Planning?.Invoke();return Planned;}
public static Vector3? StandSpot(EntityPlayerLocal p,Vector3i s){return p.position;}public static IEnumerator GoTo(EntityPlayerLocal p,Vector3 to,float stop,float timeout,Action<string> log,Action<bool> done,RebirthGameBridgeInput.OwnedInputScope scope){done(true);yield break;}
}
class Driver:IDisposable {public static Action BeforeInjection,AfterInjection;private readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();public Driver(IEnumerator root){stack.Push(root);}public bool Step(){while(stack.Count>0){var it=stack.Peek();if(!it.MoveNext()){(stack.Pop() as IDisposable)?.Dispose();continue;}var child=it.Current as IEnumerator;if(child!=null){stack.Push(child);continue;}Time.frameCount++;Time.realtimeSinceStartup+=.1f;BeforeInjection?.Invoke();RebirthGameBridgeInput.Tick(RebirthGameBridgeInput.Click);RebirthGameBridgeInput.Tick(RebirthGameBridgeInput.Slot);AfterInjection?.Invoke();return true;}return false;}public void Dispose(){while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}}
class PrefabInstance {public int id=1;public Vector3i boundingBoxPosition,boundingBoxSize;}
class TileEntityComposite{}
class TEFeatureStorage {public Grid ItemGrid=new Grid{items=new[]{new ItemStack{count=0}}};public TileEntityComposite Parent=new TileEntityComposite();}
class TEFeatureRebirthPoiCrateIdentity {public Guid PlacementId=Guid.NewGuid();public TileEntityComposite Parent;public bool Owner=true,Binding=true;public bool IsUnbound{get{return !Binding;}}public bool MatchesPoi(PrefabInstance p){return Binding;}public bool HasLocalOwner(EntityPlayerLocal p){return Owner;}}
static class RebirthPoiCrateIdentity {public static TEFeatureRebirthPoiCrateIdentity Resolve(EntityPlayerLocal p,Vector3i pos){return p.world.Marker;}public static bool RequestBinding(EntityPlayerLocal p,PrefabInstance poi,Vector3i pos,Guid id){return true;}}
class XUi:RebirthGameBridgeUi.NativeUi{public TEFeatureStorage LootContainer;public XUiC_DragAndDropWindow DragAndDropWindow;}
class XUiC_ItemStack:RebirthGameBridgeUi.XUiC_ItemStack{}
class XUiC_DragAndDropWindow {public XUi xui;public XUiC_ItemStack ItemStackControl;public bool Empty=true;public Action Read;public bool IsEmpty(){Read?.Invoke();return Empty;}}
class LocalPlayerUI {public XUi xui;public static LocalPlayerUI Current;public static LocalPlayerUI GetUIForPlayer(EntityPlayerLocal p){return Current;}}
static class RebirthGameBridgeCombat {public static Func<bool> Threat;public static List<object> AwakeThreats(EntityPlayerLocal p,float radius,bool b){return Threat!=null&&Threat()?new List<object>{new object()}:new List<object>();}}
static class RebirthGameBridgeEntry {public class Entry{public bool Roofed;public Vector3 Outside;}public static List<Entry> FindEntrances(EntityPlayerLocal p,PrefabInstance poi){return new List<Entry>();}public static bool Roofed(EntityPlayerLocal p,Vector3 point){return false;}}
class SnapshotWriter:BinaryWriter {public SnapshotWriter():base(new MemoryStream()){}public void SetBaseStream(Stream s){OutStream=s;}}
static class MemoryPools {public class Pool{public SnapshotWriter AllocSync(bool b){return new SnapshotWriter();}}public static Pool poolBinaryWriter=new Pool();}
class CallerBranches { // PRODUCTION_BRANCHES
}
class Checks {
 static int ChecksRun;static void A(bool b,string m){ChecksRun++;if(!b)throw new Exception(m);}static void Reset(){RebirthGameBridgeInput.ReleaseAll();RebirthGameBridgeInput.LockCursor=false;Time.frameCount=0;Time.realtimeSinceStartup=0;Input.mousePosition=new Vector3();Application.isFocused=true;RebirthGameBridgeUi.CursorItem=null;RebirthGameBridgeInput.Click.Updates=RebirthGameBridgeInput.Slot.Updates=0;RebirthGameBridgeNeeds.Assigned=false;}
 static EntityPlayerLocal NewPlayer(){Reset();var p=new EntityPlayerLocal();p.world.Primary=p;GameManager.Instance.World=p.world;return p;}
 static ItemStack Tool(string name,string variant="a"){return new ItemStack{itemValue=new ItemValue{ItemClass=new ItemClass{Name=name},Variant=variant}};}
 static void StepAll(Driver d,EntityPlayerLocal p,Action tick=null){int n=0;while(d.Step()){if(RebirthGameBridgeInput.IsHeld(RebirthGameBridgeInput.Slot))p.inventory.holdingItemIdx=RebirthGameBridgeInput.Slot.Name=="InventorySlot1"?0:1;tick?.Invoke();if(++n>1200)throw new Exception("timeout");}}
 static EntityPlayerLocal Fresh(){Driver.BeforeInjection=null;Driver.AfterInjection=null;RebirthGameBridgeInput.Click.OnUpdate=null;var p=NewPlayer();RebirthGameBridgeUi.TextControl=null;RebirthGameBridgeUi.ContextControl=null;RebirthGameBridgeInput.Slot.Name="InventorySlot2";RebirthGameBridgeNeeds.RealSlots=true;RebirthGameBridgePlayer.Turns=0;RebirthGameBridgePlayer.Open=false;RebirthGameBridgePlayer.DoorPresent=false;RebirthGameBridgeUi.SlotOffset=0;RebirthGameBridgeUi.HideSlot=false;RebirthGameBridgeUi.ForeignOnly=false;RebirthGameBridgeUi.ForeignSpoofLocation=false;RebirthGameBridgeTerrain.Probe=null;RebirthGameBridgePath.Planning=null;return p;}
 static IEnumerable<PlayerAction> Actions(){var a=RebirthGameBridgeInput.Local;return new[]{RebirthGameBridgeInput.Click,RebirthGameBridgeInput.Slot,a.Primary,a.Activate,a.MoveForward,a.Run,a.MoveLeft,a.MoveRight,a.Jump,a.PermanentActions.Inventory};}
 static void NativeTick(){foreach(var a in Actions())RebirthGameBridgeInput.Tick(a);}
 static void Sweep(string label,Func<EntityPlayerLocal,RebirthGameBridgeInput.OwnedInputScope,IEnumerator> make,Action<EntityPlayerLocal> setup=null,Action<EntityPlayerLocal> tick=null){
 int yields=0;var p=Fresh();setup?.Invoke(p);var root=new RebirthGameBridgeInput.OwnedInputScope(p,()=>true);using(var d=new Driver(make(p,root))){while(d.Step()){if(RebirthGameBridgeInput.IsHeld(RebirthGameBridgeInput.Slot))p.inventory.holdingItemIdx=RebirthGameBridgeInput.Slot.Name=="InventorySlot1"?0:1;tick?.Invoke(p);if(++yields>500)throw new Exception("sweep baseline timeout "+label);}}root.Dispose();
 for(int cut=0;cut<yields;cut++){p=Fresh();setup?.Invoke(p);root=new RebirthGameBridgeInput.OwnedInputScope(p,()=>true);using(var d=new Driver(make(p,root))){for(int n=0;n<=cut;n++){if(!d.Step())throw new Exception("early "+label);if(RebirthGameBridgeInput.IsHeld(RebirthGameBridgeInput.Slot))p.inventory.holdingItemIdx=RebirthGameBridgeInput.Slot.Name=="InventorySlot1"?0:1;tick?.Invoke(p);}int turns=RebirthGameBridgePlayer.Turns;GameManager.Instance.World=new World();int updates=Actions().Sum(a=>a.Updates);NativeTick();if(Actions().Sum(a=>a.Updates)!=updates)throw new Exception("late native injection "+label);StepAll(d,p);if(RebirthGameBridgePlayer.Turns!=turns)throw new Exception("late camera "+label);}root.Dispose();if(Actions().Any(RebirthGameBridgeInput.IsHeld))throw new Exception("owned hold leaked "+label);}
 A(yields>0,label+" every yielded boundary native replacement blocks further action/camera and releases only owned holds ("+yields+")");
 }
 static IEnumerator FaultAfterPickup(RebirthGameBridgeInput.OwnedInputScope scope){scope.TryHoldSeconds(RebirthGameBridgeInput.Click,0);RebirthGameBridgeUi.CursorItem=new ItemStack();yield return null;throw new InvalidOperationException("fixture move fault");}
 static void Main(){
 foreach(string scenario in new[]{"before","shift","cursor","missing","metadata","distribution","forward","native","ordinary","cancel"}){
 var p=Fresh();RebirthGameBridgeInput.Shift=false;RebirthGameBridgeCombat.Threat=null;p.bag.ItemGrid.items=new[]{Tool("cargo"),new ItemStack{count=0}};
 p.world.Storage=new TEFeatureStorage();p.world.Marker=new TEFeatureRebirthPoiCrateIdentity{Parent=p.world.Storage.Parent};
 var xui=new XUi{playerUI=new RebirthGameBridgeUi.PlayerUi{entityPlayer=p},LootContainer=p.world.Storage};var cursor=new XUiC_DragAndDropWindow{xui=xui,ItemStackControl=new XUiC_ItemStack{xui=xui}};xui.DragAndDropWindow=cursor;LocalPlayerUI.Current=new LocalPlayerUI{xui=xui};
 var poi=new PrefabInstance();var storage=new RebirthGameBridgePoiStorage(p,poi);bool awake=false,armed=false,done=true;int calls=0;RebirthGameBridgeCombat.Threat=()=>awake||(scenario=="shift"&&storage.Phase!=null);
 if(scenario=="missing")xui.DragAndDropWindow=null;
 Driver.BeforeInjection=()=>{if(storage.Phase==null||armed||!RebirthGameBridgeInput.IsHeld(RebirthGameBridgeInput.Click))return;armed=true;
 if(scenario=="before"||scenario=="cursor"||scenario=="metadata"||scenario=="distribution"||scenario=="ordinary")awake=true;
 if(scenario=="cursor")xui.DragAndDropWindow=new XUiC_DragAndDropWindow{xui=xui,ItemStackControl=new XUiC_ItemStack{xui=xui}};
 if(scenario=="metadata")p.bag.ItemGrid.items[0].itemValue.Variant="changed";
 if(scenario=="distribution"){p.world.Storage.ItemGrid.items[0]=p.bag.ItemGrid.items[0];p.bag.ItemGrid.items[0]=new ItemStack{count=0};}
 if(scenario=="ordinary"){awake=false;var field=typeof(RebirthGameBridgePoiTransferPhase).GetField("root",BindingFlags.NonPublic|BindingFlags.Instance);((RebirthGameBridgeInput.OwnedInputScope)field.GetValue(storage.Phase)).Refuse("input admission refused");}
 if(scenario=="native")RebirthGameBridgeInput.Click.OnUpdate=()=>{throw new InvalidOperationException("native forwarding uncertain");};
 };
 Driver.AfterInjection=()=>{if(scenario=="forward"&&RebirthGameBridgeInput.Click.Updates>0)awake=true;};
 bool fault=false;using(var d=new Driver(storage.Deposit(p,poi,s=>{},b=>{done=b;calls++;}))){try{int n=0;while(d.Step()){if(scenario=="cancel"&&storage.Phase!=null)break;if(++n>500)throw new Exception("timeout");}}catch(InvalidOperationException){fault=true;}}
 bool shouldRetry=scenario=="before"||scenario=="shift";
 A(storage.RetryAfterThreat==shouldRetry,"full actual Deposit typed retry "+scenario);
 A(storage.Phase==null,"full Deposit exact phase detached after "+scenario);
 if(shouldRetry)A(RebirthGameBridgeInput.Click.Updates==0&&!done&&calls==1,"genuine threat returns incomplete zero-forward retry");
 if(scenario=="native")A(fault&&RebirthGameBridgeInput.Click.Updates==1,"native forward throw is uncertain and cannot retry");
 }
 A(CallerBranches.Run(false,true).Contains("after-budget:4")&&CallerBranches.Run(true,true).Contains("after-budget:4"),"two actual World retry branches allow only three continuations");
 Console.WriteLine("PASS "+ChecksRun+" proposed full Deposit/Threat/Input/UI source checks; native world/UI/serializer/threat scheduling doubled; proposal ONLY, no native loot/conservation claim.");
 }
}
