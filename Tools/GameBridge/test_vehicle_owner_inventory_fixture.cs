using System.Collections.Generic;
using System;using System.IO;using System.Linq;using System.Globalization;using System.Xml.Linq;
class Block {public const int ItemsStartHere=1000;}
class Utils {public static int FastMin(int a,int b){return Math.Min(a,b);}}
public class ItemClass {public int MaxCount=100;public static ItemClass GetForId(int id){return new ItemClass();}}
public class ItemValue {public int type=1001;public int TextureFullArray;public bool IsShapeHelperBlock;public string Data="item";public ItemClass ItemClass=new ItemClass();public ItemValue Clone(){return (ItemValue)MemberwiseClone();}}
public class XUiC_ItemStack {public enum StackLocationTypes{Backpack}}
public class ItemStack {public int count;public ItemValue itemValue;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public static ItemStack Empty=new ItemStack(new ItemValue{type=0},0);public bool IsEmpty(){return count==0||itemValue.type==0;}public ItemStack Clone(){return new ItemStack(itemValue.Clone(),count);}public bool CanMoveTo(XUiC_ItemStack.StackLocationTypes t){return true;}
// NATIVE
}
public enum RebirthVehiclePartSourceLocation:byte {None,Backpack,Toolbelt}
static class RebirthNativeItemCodec {public static string Encode(ItemValue v){return v.Data;}public static bool TryDecode(string data,out ItemValue value){value=data=="item"?new ItemValue():null;return value!=null;}}

public class Grid {public ItemStack[] items;}
public class Buffs {public Dictionary<string,float> Values=new Dictionary<string,float>();public bool Suppress;public Action<float> AfterWrite;public float GetCustomVar(string k){float v;return Values.TryGetValue(k,out v)?v:0;}public void SetCustomVar(string k,float v,bool sync){if(Suppress)return;Values[k]=v;if(AfterWrite!=null)AfterWrite(v);}}
public class Bag {public ItemStack[] slots=new[]{new ItemStack(new ItemValue(),10)};public Grid ItemGrid{get{return new Grid{items=slots};}}public bool[] LockedSlots=new bool[1];public int Writes;public bool ThrowAfter;public Action AfterWrite;public ItemStack[] GetSlots(){return slots;}public void SetSlots(ItemStack[] s){slots=s;Writes++;if(AfterWrite!=null)AfterWrite();if(ThrowAfter)throw new Exception("listener");}}
public class Inventory {public int Writes;public Grid ItemGrid{get{return new Grid{items=slots};}}public void SetSlots(ItemStack[] s){slots=s;Writes++;}public ItemStack[] slots=new[]{new ItemStack(new ItemValue(),10)};public ItemStack[] GetSlots(){return slots;}public void SetItem(int i,ItemStack s){slots[i]=s;}}
public class EntityPlayerLocal {public int Died;public Buffs Buffs=new Buffs();public Bag bag=new Bag();public Inventory inventory=new Inventory();public bool IsSpawned(){return true;}public bool IsDead(){return false;}}
static class RebirthSurvivorMode {public static bool IsEnabledForCurrentWorld(){return true;}}
static class RebirthToolbeltCapacity {public static int GetOwnedSlotCount(EntityPlayerLocal p,int count){return count;}}
static class Log {public static void Error(string text){}}
// Explicit native guard/snapshot comparison doubles; Snapshot/SameSlots bodies
// below are extracted from production. This does not qualify live holds.
static class RebirthStationGridIngredients {internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b){return a!=null&&b!=null&&a.count==b.count&&a.itemValue.type==b.itemValue.type&&a.itemValue.Data==b.itemValue.Data;}}
static class RebirthVehicleInventoryWrite {internal static bool CanMutate(EntityPlayerLocal p,bool bag){return true;}
// WRITE_VERIFIER_METHODS
}

// Bounded historical standalone cohort: native custody/original recovery is
// excluded. The new joint cohort embeds actual WAL/checkpoint implementations.
public class RebirthVehicleTransferPlan {public int NativeDeaths=-1;}
static class RebirthVehicleNativePolicy {internal static bool IsLocked(EntityPlayerLocal player,bool bag,int slot)=>bag&&player.bag.LockedSlots!=null&&slot<player.bag.LockedSlots.Length&&player.bag.LockedSlots[slot];}
static class RebirthVehicleOwnerCustody {
internal static bool CreationMatches(EntityPlayerLocal player,string current,string offered,IEnumerable<RebirthVehicleTransferItem> items)=>RebirthSurvivorRequestScope.Matches(current,offered);
internal static bool TryGetPlan(EntityPlayerLocal player,IEnumerable<RebirthVehicleTransferItem> items,out RebirthVehicleTransferPlan plan){plan=null;return false;}
internal static bool NativeCanEnter(EntityPlayerLocal player,bool bag)=>false;
internal static void WriteSlots(EntityPlayerLocal player,bool bag,ItemStack[] candidate,IEnumerable<RebirthVehicleTransferItem> items,Func<bool> current){throw new InvalidOperationException("Native grant excluded from bounded cohort");}}
static class RebirthVehicleOwnerOriginal {
internal static bool ResolveApplying(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,RebirthVehicleTransferItem[] items,out bool retry){retry=false;return false;}
internal static bool Prepare(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,RebirthVehicleTransferItem[] items,bool bag,ItemStack[] candidate)=>false;}
static class RebirthVehicleOwnerCheckpoint {
internal static bool Persist(EntityPlayerLocal player,IEnumerable<Guid> ids,float stage,Func<bool> current)=>current()&&ids.All(id=>player.Buffs.GetCustomVar("rbVehicle_"+id.ToString("N"))==stage);}
// SOURCE
class Test {
static RebirthVehicleTransferItem Op(bool debit,int count,int slot=0){return new RebirthVehicleTransferItem(Guid.NewGuid(),debit,"item",count,debit?RebirthVehiclePartSourceLocation.Backpack:RebirthVehiclePartSourceLocation.None,debit?slot:-1);}
static ItemStack Stack(int count){return new ItemStack(new ItemValue(),count);}
static void Main(){
var cross=new EntityPlayerLocal();var crossCreation=Guid.NewGuid();var crossDebit=Op(true,2);cross.bag.AfterWrite=()=>cross.inventory.slots[0].count++;
if(RebirthVehicleOwnerTransfer.Apply(cross,crossCreation,crossCreation,crossDebit,()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate||cross.Buffs.GetCustomVar("rbVehicle_"+crossDebit.ReceiptId.ToString("N"))!=2f)throw new Exception("Single native debit accepted an unrelated belt callback edit");
cross=new EntityPlayerLocal();var crossOps=new[]{Op(true,2)};cross.bag.AfterWrite=()=>cross.inventory.slots[0].itemValue.Data="unrelated";
if(RebirthVehicleOwnerDebitBatch.Apply(cross,crossCreation,crossCreation,crossOps,()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate||cross.Buffs.GetCustomVar("rbVehicle_"+crossOps[0].ReceiptId.ToString("N"))!=2f)throw new Exception("Batch native debit accepted unrelated belt metadata");
cross=new EntityPlayerLocal();crossDebit=Op(true,2);cross.Buffs.AfterWrite=stage=>{if(stage==1f)cross.inventory.slots[0].count++;};
if(RebirthVehicleOwnerTransfer.Apply(cross,crossCreation,crossCreation,crossDebit,()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate)throw new Exception("Terminal receipt callback changed unrelated inventory");
var guarded=new EntityPlayerLocal();var guardedCreation=Guid.NewGuid();var guardedDebit=Op(true,2);guarded.Buffs.Suppress=true;
if(RebirthVehicleOwnerTransfer.Apply(guarded,guardedCreation,guardedCreation,guardedDebit,()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate||guarded.bag.Writes!=0)throw new Exception("Suppressed uncertain receipt admitted debit");
guarded=new EntityPlayerLocal();guardedDebit=Op(true,2);guarded.Buffs.AfterWrite=v=>{if(v==2f)guarded.bag.slots[0].count=9;};
if(RebirthVehicleOwnerTransfer.Apply(guarded,guardedCreation,guardedCreation,guardedDebit,()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate||guarded.bag.Writes!=0)throw new Exception("Receipt callback changed preimage before debit");
guarded=new EntityPlayerLocal();guardedDebit=Op(true,2);guarded.bag.AfterWrite=()=>guarded.bag.slots[0].itemValue.Data="aliased-native-edit";
if(RebirthVehicleOwnerTransfer.Apply(guarded,guardedCreation,guardedCreation,guardedDebit,()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate||guarded.Buffs.GetCustomVar("rbVehicle_"+guardedDebit.ReceiptId.ToString("N"))!=2f)throw new Exception("Aliased native argument bypassed full postimage");
guarded=new EntityPlayerLocal();guarded.Buffs.Suppress=true;
if(RebirthVehicleOwnerDebitBatch.Apply(guarded,guardedCreation,guardedCreation,new[]{Op(true,2)},()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate||guarded.bag.Writes!=0)throw new Exception("Batch uncertain receipt suppression admitted debit");
var owner=new EntityPlayerLocal();var creation=Guid.NewGuid();var debit=Op(true,2);if(RebirthVehicleOwnerTransfer.Apply(owner,creation,creation,debit,()=>true)!=RebirthVehicleOwnerTransferResult.Applied||owner.bag.slots[0].count!=8)throw new Exception("Owner debit failed");if(RebirthVehicleOwnerTransfer.Apply(owner,creation,creation,debit,()=>true)!=RebirthVehicleOwnerTransferResult.Applied||owner.bag.Writes!=1)throw new Exception("Owner replay moved items");
owner=new EntityPlayerLocal();owner.bag.ThrowAfter=true;debit=Op(true,2);if(RebirthVehicleOwnerTransfer.Apply(owner,creation,creation,debit,()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate||owner.bag.slots[0].count!=8)throw new Exception("Listener ambiguity not retained");if(RebirthVehicleOwnerTransfer.Apply(owner,creation,creation,debit,()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate||owner.bag.Writes!=1)throw new Exception("Uncertain debit retried");
owner=new EntityPlayerLocal();debit=Op(true,2);bool session=true;owner.bag.AfterWrite=()=>session=false;
if(RebirthVehicleOwnerTransfer.Apply(owner,creation,creation,debit,()=>session)!=RebirthVehicleOwnerTransferResult.Indeterminate||owner.bag.Writes!=1||owner.Buffs.GetCustomVar("rbVehicle_"+debit.ReceiptId.ToString("N"))!=2f)throw new Exception("Session change after debit not retained uncertain");
session=true;if(RebirthVehicleOwnerTransfer.Apply(owner,creation,creation,debit,()=>session)!=RebirthVehicleOwnerTransferResult.Indeterminate||owner.bag.Writes!=1)throw new Exception("Session-changed debit repeated");
owner=new EntityPlayerLocal();if(RebirthVehicleOwnerTransfer.Apply(owner,creation,creation,Op(true,1),()=>false)!=RebirthVehicleOwnerTransferResult.Pending||owner.bag.Writes!=0||owner.Buffs.Values.Count!=0)throw new Exception("Stale single transfer moved items");
string migrated="legacy-"+new string('a',64);owner=new EntityPlayerLocal();debit=Op(true,2);
if(RebirthVehicleOwnerTransfer.Apply(owner,migrated,migrated,debit,()=>true)!=RebirthVehicleOwnerTransferResult.Applied||owner.bag.Writes!=1)throw new Exception("Migrated debit failed");
if(RebirthVehicleOwnerTransfer.Apply(owner,"legacy-"+new string('b',64),migrated,debit,()=>true)!=RebirthVehicleOwnerTransferResult.Pending||owner.bag.Writes!=1)throw new Exception("Migrated mismatch replay accepted");
owner=new EntityPlayerLocal();if(RebirthVehicleOwnerDebitBatch.Apply(owner,migrated,migrated,new[]{Op(true,2)},()=>true)!=RebirthVehicleOwnerTransferResult.Applied||owner.bag.Writes!=1)throw new Exception("Migrated batch failed");
owner=new EntityPlayerLocal();var fullCredit=Op(false,100);if(RebirthVehicleOwnerTransfer.Apply(owner,creation,creation,fullCredit,()=>true)!=RebirthVehicleOwnerTransferResult.Pending||owner.Buffs.Values.Count!=0||owner.bag.Writes!=0)throw new Exception("Full bag changed state");
if(RebirthVehicleOwnerTransfer.Apply(owner,creation,Guid.NewGuid(),Op(true,1),()=>true)!=RebirthVehicleOwnerTransferResult.Pending||owner.bag.Writes!=0)throw new Exception("Wrong character moved items");
var rejected=Op(true,11);if(RebirthVehicleOwnerTransfer.Apply(owner,creation,creation,rejected,()=>true)!=RebirthVehicleOwnerTransferResult.Rejected)throw new Exception("Invalid debit not rejected");owner.bag.slots[0].count=20;if(RebirthVehicleOwnerTransfer.Apply(owner,creation,creation,rejected,()=>true)!=RebirthVehicleOwnerTransferResult.Rejected||owner.bag.Writes!=0)throw new Exception("Rejected operation became valid");
owner=new EntityPlayerLocal();owner.bag.slots=new[]{Stack(10),Stack(20)};var batchOps=new[]{Op(true,3,0),Op(true,4,1)};if(RebirthVehicleOwnerDebitBatch.Apply(owner,creation,creation,batchOps,()=>true)!=RebirthVehicleOwnerTransferResult.Applied||owner.bag.slots[0].count!=7||owner.bag.slots[1].count!=16||owner.bag.Writes!=1)throw new Exception("Batch debit failed");if(RebirthVehicleOwnerDebitBatch.Apply(owner,creation,creation,batchOps,()=>true)!=RebirthVehicleOwnerTransferResult.Applied||owner.bag.Writes!=1)throw new Exception("Batch replay debited twice");
owner=new EntityPlayerLocal();owner.bag.slots=new[]{Stack(10),Stack(2)};batchOps=new[]{Op(true,3,0),Op(true,4,1)};if(RebirthVehicleOwnerDebitBatch.Apply(owner,creation,creation,batchOps,()=>true)!=RebirthVehicleOwnerTransferResult.Rejected||owner.bag.Writes!=0||owner.bag.slots[0].count!=10)throw new Exception("Partial invalid batch escaped");
owner=new EntityPlayerLocal();owner.bag.slots=new[]{Stack(10),Stack(20)};owner.bag.ThrowAfter=true;batchOps=new[]{Op(true,3,0),Op(true,4,1)};if(RebirthVehicleOwnerDebitBatch.Apply(owner,creation,creation,batchOps,()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate||owner.bag.slots[1].count!=16)throw new Exception("Batch listener ambiguity lost");if(RebirthVehicleOwnerDebitBatch.Apply(owner,creation,creation,batchOps,()=>true)!=RebirthVehicleOwnerTransferResult.Indeterminate||owner.bag.Writes!=1)throw new Exception("Uncertain batch repeated");
owner=new EntityPlayerLocal();batchOps=new[]{Op(true,1)};if(RebirthVehicleOwnerDebitBatch.Apply(owner,creation,creation,batchOps,()=>false)!=RebirthVehicleOwnerTransferResult.Pending||owner.bag.Writes!=0||owner.Buffs.Values.Count!=0)throw new Exception("Stale batch moved items");
ItemStack[] candidate;var live=new[]{Stack(90),ItemStack.Empty.Clone()};var result=RebirthVehicleOwnerInventoryPlan.Prepare(live,Op(false,110),2,null,out candidate);if(result!=RebirthVehicleInventoryPlanResult.Ready||candidate[0].count!=100||candidate[1].count!=100||live[0].count!=90||live[1].count!=0)throw new Exception("Split credit or input isolation failed");
result=RebirthVehicleOwnerInventoryPlan.Prepare(live,Op(false,111),2,null,out candidate);if(result!=RebirthVehicleInventoryPlanResult.Pending||candidate!=null||live[0].count!=90)throw new Exception("Partial credit escaped");
result=RebirthVehicleOwnerInventoryPlan.Prepare(live,Op(false,110),1,null,out candidate);if(result!=RebirthVehicleInventoryPlanResult.Pending)throw new Exception("Hidden slot credited");
result=RebirthVehicleOwnerInventoryPlan.Prepare(live,Op(true,90),2,null,out candidate);if(result!=RebirthVehicleInventoryPlanResult.Ready||!candidate[0].IsEmpty()||live[0].count!=90)throw new Exception("Debit isolation failed");
if(RebirthVehicleOwnerInventoryPlan.Prepare(live,Op(true,91),2,null,out candidate)!=RebirthVehicleInventoryPlanResult.Rejected||RebirthVehicleOwnerInventoryPlan.Prepare(live,Op(true,1),2,i=>true,out candidate)!=RebirthVehicleInventoryPlanResult.Rejected)throw new Exception("Short/locked debit accepted");
live[0].itemValue.Data="different-metadata";if(RebirthVehicleOwnerInventoryPlan.Prepare(live,Op(true,1),2,null,out candidate)!=RebirthVehicleInventoryPlanResult.Rejected)throw new Exception("Different metadata debited");
result=RebirthVehicleOwnerInventoryPlan.Prepare(live,Op(false,100),2,null,out candidate);if(result!=RebirthVehicleInventoryPlanResult.Ready||candidate[0].count!=90||candidate[1].count!=100)throw new Exception("Different metadata merged");
Console.WriteLine("PASS actual vehicle owner inventory planner and native stack capacity methods: split credit, insufficient space no mutation, owned-slot bounds, exact debit, locks/count and metadata conservation. Owner apply/replay/full/character/rejection/listener-failure checks PASS; native registry/codec/setters and buffs substituted.");}
}
