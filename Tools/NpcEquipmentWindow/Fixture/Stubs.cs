using System;using System.Collections.Generic;using System.Linq;using System.Xml.Linq;
static class ThreadManager {public static bool IsMainThread()=>true;}
class World {public EntityRebirthHumanoidNPC Actor;public bool Remote;public bool IsRemote()=>Remote;public object GetEntity(int id)=>Actor!=null&&Actor.entityId==id?Actor:null;}
class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
class EntityRebirthHumanoidNPC {public Hand Hand=new Hand();public bool AimingGun;public World world;public int entityId=1;public Runtime RebirthRuntimeState=new Runtime();public Inventory inventory=new Inventory(8),equipment=new Inventory(13);public Inventory bag=new Inventory(16);}
class Runtime {public RebirthNpcStableId StableId=new RebirthNpcStableId("npc-one");}
struct RebirthNpcStableId {public string Value;public RebirthNpcStableId(string value){Value=value;}public override string ToString()=>Value;}
class Inventory {public Grid ItemGrid;public Inventory(int count){ItemGrid=new Grid{items=new ItemStack[count]};}}
class Grid {public ItemStack[] items;}
class ItemValue {public ItemClass ItemClass=new ItemClass();public bool IsEmpty()=>Image==null;public string Image;public ItemValue(string image){Image=image;}}
class ItemStack {public int count;public ItemValue itemValue;public string Key;public ItemStack(string key,int count,string image){Key=key;this.count=count;itemValue=new ItemValue(image);}public bool IsEmpty()=>count<=0;}
static class RebirthNativeItemCodec {public static string Encode(ItemValue value)=>value.Image;}
static class RebirthHumanEquipment {public static bool Human(EntityRebirthHumanoidNPC npc)=>true;public static ItemStack Decode(string text){var a=text.Split('|');return new ItemStack(a[0],int.Parse(a[1]),a[2]);}}
static class RebirthNpcInventoryPersistenceStore {public static int Dirty;public static void EnsureLoaded(){}public static void MarkDirty(){Dirty++;}}
class Person {public Reconstruction NativeReconstruction;}
class Reconstruction {public XElement Root;public XElement Write()=>new XElement(Root);}
static class RebirthNpcAggregatePersistenceStore {public static Person Saved;public static bool TryGet(RebirthNpcStableId id,out Person person){person=Saved;return person!=null;}}
class RebirthNpcNativeStackRecord {
 public Guid StackId;public string ItemKey,Payload;public int Count;
 public XElement Write()=>new XElement("nativeStack",new XAttribute("id",StackId),new XAttribute("item",ItemKey),new XAttribute("count",Count),new XAttribute("payload",Payload));
 public static bool TryRead(XElement node,out RebirthNpcNativeStackRecord record){record=new RebirthNpcNativeStackRecord{StackId=Guid.Parse((string)node.Attribute("id")),ItemKey=(string)node.Attribute("item"),Count=(int)node.Attribute("count"),Payload=(string)node.Attribute("payload")};return true;}
 public static bool TryCapture(Guid id,RebirthNpcStableId owner,ItemStack stack,out RebirthNpcNativeStackRecord record){record=new RebirthNpcNativeStackRecord{StackId=id,ItemKey=stack.Key,Count=stack.count,Payload=stack.itemValue.Image};return true;}
}
class RebirthNpcNativeStackSet {
 public uint Revision;public List<RebirthNpcNativeStackRecord> Records;
 public XElement Write()=>new XElement("nativeStacks",new XAttribute("revision",Revision),Records.OrderBy(r=>r.StackId).Select(r=>r.Write()));
 public bool FitsQuantities(Dictionary<string,int> quantities)=>Records.GroupBy(r=>r.ItemKey).All(g=>quantities.TryGetValue(g.Key,out int count)&&count>=g.Sum(r=>r.Count));
 public static bool TryCreate(RebirthNpcStableId owner,uint revision,IEnumerable<RebirthNpcNativeStackRecord> records,out RebirthNpcNativeStackSet set){set=new RebirthNpcNativeStackSet{Revision=revision,Records=records.ToList()};return set.Records.Select(r=>r.StackId).Distinct().Count()==set.Records.Count;}
}
public static partial class RebirthNpcInventoryTransactionService {
 private sealed class InventoryState {internal Dictionary<string,int> Quantities=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase),Reservations=new Dictionary<string,int>();internal uint Revision;internal RebirthNpcNativeStackSet NativeStacks;}
 private static object Sync=new object(),generationEpoch=new object();private static InventoryState state=new InventoryState();private static InventoryState GetOrCreate(RebirthNpcStableId id)=>state;
 internal static void Reset(){state=new InventoryState();RebirthNpcAggregatePersistenceStore.Saved=null;}
 internal static int Count(string key)=>state.Quantities.TryGetValue(key,out int n)?n:0;
 internal static void Quantity(string key,int n)=>state.Quantities[key]=n;
 internal static void Reserve(string key,int n)=>state.Reservations[key]=n;
 internal static XElement Native()=>state.NativeStacks.Write();
 internal static uint Revision=>state.Revision;
 internal static void Seed(RebirthNpcNativeStackSet set){state.NativeStacks=set;state.Revision=set.Revision;}
}

class ItemClass {public ItemAction[] Actions=new ItemAction[]{new ItemActionDynamicMelee()};}
class ItemAction {}
class ItemActionMelee:ItemAction {}
class ItemActionDynamicMelee:ItemAction {}
class ItemActionZoom:ItemAction {public class ItemActionDataZoom {public bool bZoomInProgress,aimingValue;public object aimingCoroutine;}}
enum ItemActionFiringState {Off,On}
class ItemActionRanged:ItemAction {public class ItemActionDataRanged {public ItemActionFiringState state;public bool isReloading,isWeaponReloading,isChangingAmmoType,burstShotStarted,burstShotFinished;}}
class Hand {public bool IsSwitching,Busy;public object switchingCoroutine,holstering;public HoldingMode mode;public enum HoldingMode {Current,Bare,Transient}public HeldData Held=new HeldData();public bool IsActionRunning()=>Busy;}
class HeldData {public ItemValue itemValue=new ItemValue("hand");public object[] actionData=Array.Empty<object>();}
