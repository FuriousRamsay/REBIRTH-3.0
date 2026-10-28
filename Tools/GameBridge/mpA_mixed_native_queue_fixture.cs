using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using HarmonyLib;
// ACTUAL_TOOL_SOURCE
// ACTUAL_BONUS_SOURCE
internal static class RebirthRecyclerRuntime
{
 // ACTUAL_GROUP
 private sealed class Processing {internal bool Active;internal int NativeOwnerFrame=-1;}
 private static readonly ConditionalWeakTable<TileEntityWorkstation,Processing> processing=new ConditionalWeakTable<TileEntityWorkstation,Processing>();
 // ACTUAL_PROCESSING_PREDICATE
 internal static bool IsMarked(Recipe r)=>r?.Special==true;
 private static bool Supports(TileEntityWorkstation s)=>true;
 private static bool ValidRow(TileEntityWorkstation s,RecipeQueueItem r)=>true;
 private static bool Read(Recipe r,out Group g){g=new Group{Id=r.Group,Original=new ItemValue(3),Owner="fixture",Creation="fixture"};return IsMarked(r);}
 internal static bool TryGroup(Recipe r,out Guid id){id=r?.Group??Guid.Empty;return IsMarked(r);}
 private static string Identity(EntityPlayer p)=>"fixture";
 internal static void Refresh(EntityPlayer p,TileEntityWorkstation s){}
 // ACTUAL_PROCESS_SOURCE
}
internal static class RebirthRecyclerQueueProcess
{
 // ACTUAL_PREFIX
 // ACTUAL_FINALIZER
}
public sealed class Recipe {public int craftingToolType,itemValueType=2,count=1,craftExpGain;public bool Special,Managed,IsScrap,HasQuality;public Guid Group=Guid.NewGuid();public List<ItemStack> ingredients=new List<ItemStack>();public string GetName()=>"fixture";}
public sealed class RecipeQueueItem {public Recipe Recipe;public int Multiplier,Quality,StartingEntityId,AmountToRepair;public float CraftingTimeLeft,OneItemCraftTime;public bool IsCrafting;public object RepairItem;}
public sealed class ItemClass {public static ItemClass[] list={null,new ItemClass{Name="tool"},new ItemClass{Name="output"},new ItemClass{Name="source"}};public string Name;public bool HasQuality;public static ItemClass GetForId(int t)=>list[t];public string GetItemName()=>Name;}
public sealed class ItemValue {public int type;public float UseTimes;public int MaxUseTimes=100;public ItemClass ItemClass=>global::ItemClass.list[type];public ItemValue(int t,bool ignored=false){type=t;}public ItemValue(int t,int a,int b){type=t;}public ItemValue Clone()=>new ItemValue(type){UseTimes=UseTimes,MaxUseTimes=MaxUseTimes};}
public sealed class ItemStack
{
 public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public bool IsEmpty()=>count<=0||itemValue==null;public ItemStack Clone()=>new ItemStack(itemValue?.Clone(),count);public static ItemStack[] Clone(ItemStack[] a)=>a.Select(s=>s.Clone()).ToArray();
 // DISCLOSED native output insertion double, actual production coordinators own calls.
 public static int Capacity=100;public static int AddToItemStackArray(ItemStack[] a,ItemStack value){if(a[0].count+value.count>Capacity)return -1;a[0]=new ItemStack(value.itemValue.Clone(),a[0].count+value.count);return 0;}
}
public sealed class Properties {public string GetString(string k)=>"60";}
public sealed class Block {public Properties Properties=new Properties();public string GetBlockName()=>"fixture";}
public class EntityPlayer {public string Creation="fixture";public World world;public int entityId;public bool IsSpawned()=>true;public bool IsDead()=>false;}
public sealed class EntityPlayerLocal:EntityPlayer {}
public sealed class PlayerList {public List<EntityPlayer> list=new List<EntityPlayer>();}
public sealed class World {public bool Remote;public PlayerList Players=new PlayerList();public bool IsRemote()=>Remote;public EntityPlayer Primary=new EntityPlayerLocal();public EntityPlayer GetPrimaryPlayer()=>Primary;public EntityPlayer GetEntity(int id)=>null;public TileEntityWorkstation GetTileEntity(object position)=>throw new Exception("Unexpected bonus settlement branch");}
public sealed class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
public static class ThreadManager {public static bool IsMainThread()=>true;}
public static class RebirthCamperCharacterAccess {public static string CurrentCreation(EntityPlayer p)=>p.Creation;}
public sealed class LockManager {public static LockManager Instance;public Single singleLocks=new Single();public sealed class LockEntry {public LockEntry(object s,int n){}}public sealed class Single {public bool TryGetByValue(LockEntry e,out int n){n=0;return false;}}}
public class TileEntityWorkstation
{
 public enum Module {Fuel=3}public bool[] isModuleUsed=new bool[4];public bool isBurning=true;public bool IsBurning=>isBurning;public bool bUserAccessing;
 public object ToWorldPos()=>throw new Exception("Unexpected bonus settlement branch");
 public RecipeQueueItem[] Queue;private RecipeQueueItem[] queue {get=>Queue;set=>Queue=value;}
 public ItemStack[] Tools={new ItemStack(new ItemValue(1),1)},Output={new ItemStack(new ItemValue(2),0)},Input=new ItemStack[0];private ItemStack[] output=>Output;public Block block=new Block();public int NativeCompletions;
 public bool hasRecipeInQueue()=>Queue!=null&&Queue.Any(r=>r?.Recipe!=null&&r.Multiplier>0);
 public void setModified(){}public void AddCraftComplete(int p,ItemValue v,string n,string a,int xp,int count){NativeCompletions++;}
 // ACTUAL_NATIVE_CYCLE
 // ACTUAL_NATIVE_TRANSLATED
 public void HandleRecipeQueue(float elapsed)
 {
  object state;if(!RebirthRecyclerQueueProcess.Prefix(this,elapsed,true,out state))return;
  try{NativeBody(elapsed);}finally{RebirthRecyclerQueueProcess.Finalizer(null,state);}
 }
}
public sealed class Vehicle {public World world;}
public sealed class RebirthCamperStationState:TileEntityWorkstation {public Vehicle BoundVehicle;}
public static class RebirthCamperStationGridCustody {public static bool IsHeld(object s)=>false;}
public sealed class RebirthCamperModuleRuntime {public bool TransferPending;public static RebirthCamperModuleRuntime For(object v)=>new RebirthCamperModuleRuntime();public void Persist(bool b){}}
public static class RebirthRemoteResourceSourceCustody {public static bool BlocksStation(TileEntityWorkstation s)=>false;}
public static class RebirthRemoteResourceFetchedStationOriginal {public static bool BlocksStation(TileEntityWorkstation s)=>false;}
public static class RebirthRemoteResourcePlacedStationCustody {public static bool Blocks(TileEntityWorkstation s)=>false;}
public static class RebirthRecyclerTransport {public static bool IsNativeOwner(TileEntityWorkstation s,EntityPlayer p)=>true;}
public static class RebirthStationToolAvailability {public static bool HasRequirement(ItemStack[] a,Recipe r)=>r.craftingToolType==0||a.Any(s=>s.count>0&&s.itemValue.type==r.craftingToolType&&(s.itemValue.MaxUseTimes<=0||s.itemValue.UseTimes<s.itemValue.MaxUseTimes));}
public static class RebirthStationObservationDispatcher {public static bool CanProcessNativeQueue(TileEntityWorkstation s)=>true;}
public static class RebirthCookingHeat {public static bool Managed(Recipe r)=>r?.Managed==true;public static void TickTile(TileEntityWorkstation s,float t){throw new Exception("Unexpected cooking branch");}}
public static class RebirthRecyclerRules
{
 public sealed class Rule {public IReadOnlyList<string> RequiredTools=new[]{"tool"};}
 public static bool TryResolve(int t,string n,out Rule r){r=new Rule();return true;}
 public static bool ToolsHealthy(ItemStack[] tools,Rule r,out string a,out string b){a=b=null;return tools.Any(s=>s.itemValue.MaxUseTimes<=0||s.itemValue.UseTimes<s.itemValue.MaxUseTimes);}
}
public static class RebirthRecyclerOwnerDelivery {public static bool Begin(EntityPlayer p,RebirthCamperStationState s,Guid g,ItemStack[] i,RecipeQueueItem[] q,IList<ItemStack> b,out string reason,Guid cancel,ItemStack[] o,ItemStack[] tools){reason=null;throw new Exception("Unexpected bonus branch");}public static bool TryDeliverPlaced(EntityPlayer p,TileEntityWorkstation s,IList<ItemStack> b,out string reason){reason=null;throw new Exception("Unexpected bonus branch");}}
public static class UnityEngine {public static class Time {public static int frameCount=10;}}
public static class GameSparksCollector {public enum GSDataKey {CraftedItems}public static void IncrementCounter(GSDataKey k,string n,int c){}}
namespace HarmonyLib
{
 public static class AccessTools {public static FieldInfo Field(Type t,string n)=>t.GetField(n);public static MethodInfo Method(Type t,string n)=>t.GetMethod(n,BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);}
 public sealed class CodeInstruction {public OpCode opcode;public object operand;public List<object> labels=new List<object>(),blocks=new List<object>();public CodeInstruction(OpCode op){opcode=op;}public CodeInstruction(CodeInstruction c){opcode=c.opcode;operand=c.operand;}}
}
internal static class Program
{
 private static void Need(bool b,string s){if(!b)throw new Exception(s);}
 private static RecipeQueueItem Row(bool special,float seconds,int tool=1)=>new RecipeQueueItem{Recipe=new Recipe{Special=special,craftingToolType=tool},Multiplier=1,CraftingTimeLeft=seconds,OneItemCraftTime=seconds};
 private static TileEntityWorkstation Station(params RecipeQueueItem[] queue)=>new TileEntityWorkstation{Queue=queue};
 public static void Main()
 {
  var timer=typeof(RecipeQueueItem).GetField(nameof(RecipeQueueItem.CraftingTimeLeft));var gate=typeof(TileEntityWorkstation).GetMethod(nameof(TileEntityWorkstation.hasRecipeInQueue));
  var shape=new[]{new CodeInstruction(OpCodes.Ldfld){operand=typeof(TileEntityWorkstation).GetField(nameof(TileEntityWorkstation.isBurning))},new CodeInstruction(OpCodes.Ldfld){operand=typeof(TileEntityWorkstation).GetField(nameof(TileEntityWorkstation.bUserAccessing))},new CodeInstruction(OpCodes.Stfld){operand=timer},new CodeInstruction(OpCodes.Stfld){operand=timer},new CodeInstruction(OpCodes.Stfld){operand=timer},new CodeInstruction(OpCodes.Callvirt){operand=gate}};
  var rewritten=RebirthRecyclerNativeLoop.Rewrite(shape).ToArray();Need(rewritten.Count(c=>Equals(c.operand,AccessTools.Method(typeof(RebirthRecyclerNativeLoop),nameof(RebirthRecyclerNativeLoop.SetTime))))==3&&rewritten.Count(c=>Equals(c.operand,AccessTools.Method(typeof(RebirthRecyclerNativeLoop),nameof(RebirthRecyclerNativeLoop.Continue))))==1&&rewritten.Count(c=>Equals(c.operand,AccessTools.Method(typeof(RebirthRecyclerNativeLoop),nameof(RebirthRecyclerNativeLoop.UserAccessing))))==1&&rewritten.Count(c=>Equals(c.operand,AccessTools.Method(typeof(RebirthRecyclerNativeLoop),nameof(RebirthRecyclerNativeLoop.BurningForPaidCompletion))))==1,"Actual transpiler shape changed");
  foreach(bool remote in new[]{false,true})
  {
   GameManager.Instance.World=new World{Remote=remote};UnityEngine.Time.frameCount++;
   // Native final application adds OneItemCraftTime before cycling. Preserve
   // its exact carryover semantics: 50 covers this four-row duration5 chain.
   var s=Station(Row(true,5),Row(false,5),Row(true,5),Row(false,5));s.bUserAccessing=remote;s.HandleRecipeQueue(50);
   Need(s.Output[0].count==4&&!s.hasRecipeInQueue(),"Mixed ordinary/marked chain lost native carryover/output");Need(s.Tools[0].itemValue.UseTimes==20,"Mixed chain tool work lost/doubled");
   s.HandleRecipeQueue(50);Need(s.Output[0].count==4&&s.Tools[0].itemValue.UseTimes==20,"Duplicate invocation repeated output/work");
   if(remote)
   {
    var duplicate=Station(Row(true,5));UnityEngine.Time.frameCount++;duplicate.HandleRecipeQueue(1);float remaining=duplicate.Queue.Last().CraftingTimeLeft;duplicate.HandleRecipeQueue(1);
    Need(duplicate.Queue.Last().CraftingTimeLeft==remaining&&duplicate.Tools[0].itemValue.UseTimes==1,"Outer same-frame duplicate guard was weakened");
    Need(!RebirthRecyclerRuntime.ProcessNativeCarryover(duplicate,1)&&duplicate.Queue.Last().CraftingTimeLeft==remaining,"Unscoped/reused native budget gained work");
   }
  }
  GameManager.Instance.World=new World();var exact=Station(Row(false,2));exact.Tools[0].itemValue.UseTimes=98;exact.HandleRecipeQueue(3);Need(exact.Output[0].count==1&&exact.Tools[0].itemValue.UseTimes==100,"Exactly earned native completion blocked by final tool break");
  var full=Station(Row(false,2));full.Tools[0].itemValue.UseTimes=98;ItemStack.Capacity=0;full.HandleRecipeQueue(3);Need(full.Output[0].count==0&&full.Tools[0].itemValue.UseTimes==100,"Output-full work/wear wrong");
  float stalledTimer=full.Queue.Last().CraftingTimeLeft;full.HandleRecipeQueue(3);Need(full.Tools[0].itemValue.UseTimes==100&&full.Queue.Last().CraftingTimeLeft==stalledTimer,"Output-full retry repeated wear or banked stalled time");ItemStack.Capacity=100;full.HandleRecipeQueue(3);Need(full.Output[0].count==1,"Earned output-full retry did not release exact unit");
  var exactZero=Station(Row(false,3),Row(false,2));exactZero.Tools[0].itemValue.UseTimes=98;ItemStack.Capacity=0;
  exactZero.HandleRecipeQueue(2);Need(exactZero.Queue.Last().CraftingTimeLeft==0,"Exact native zero setup");
  exactZero.HandleRecipeQueue(30);Need(exactZero.Queue.Last().CraftingTimeLeft==-float.Epsilon,"Zero retry banked elapsed or never entered strict native completion");
  ItemStack.Capacity=100;exactZero.HandleRecipeQueue(30);Need(exactZero.Output[0].count==1&&exactZero.Queue.Last().CraftingTimeLeft==3,"Zero earned retry funded unworked next recipe");
  var markedFull=Station(Row(true,2));markedFull.Tools[0].itemValue.UseTimes=98;ItemStack.Capacity=0;markedFull.HandleRecipeQueue(3);Need(markedFull.Queue.Last().CraftingTimeLeft==0&&markedFull.Tools[0].itemValue.UseTimes==100,"Marked output-full failed to retain fully worked row");
  ItemStack.Capacity=100;markedFull.HandleRecipeQueue(3);Need(markedFull.Output[0].count==1&&markedFull.Tools[0].itemValue.UseTimes==100,"Marked completed row repeated wear or lost output");
  var unworked=Station(Row(false,0));unworked.Tools[0].itemValue.UseTimes=100;unworked.HandleRecipeQueue(3);Need(unworked.Output[0].count==0,"Uninitialized zero-time row acquired earned permission");
  var next=Station(Row(false,3),Row(false,2));next.Tools[0].itemValue.UseTimes=98;next.HandleRecipeQueue(50);Need(next.Output[0].count==1&&next.Queue.Last().CraftingTimeLeft==3,"Earned output authorized unworked next recipe");
  Console.WriteLine("PASS exact extracted native C# loop/cycle + actual production hook/runtime/tool accounting with disclosed native item/rules/authority doubles; NOT installed IL/game qualification");
 }
}

// Earned durability activation is deliberately doubled OFF in this mixed-loop fixture.
// Separate native publication/remote protocol qualification is still required.
internal static class RebirthProductiveEarnedCoordinator
{
 internal static bool Enabled=>false;
 internal static bool IsExecutingMobilePaidZeroCompletion(TileEntityWorkstation s)=>false;
 internal static bool IsExecutingPaidNativeZeroCompletion(TileEntityWorkstation s)=>false;
 internal sealed class Attempt {}
 internal static void BeforeNativeTimerWrite(TileEntityWorkstation s,RecipeQueueItem r){}
 internal static bool IsNativeTerminalTimer(TileEntityWorkstation s,RecipeQueueItem r)=>false;
 internal static void ObserveNativeReturned(TileEntityWorkstation s,bool returned){}
 internal static void ObserveDeferredNativeTimer(TileEntityWorkstation s,RecipeQueueItem r,float before,float target){}
 internal static bool DeferNativeSuccessorTimer(TileEntityWorkstation s,RecipeQueueItem r,float before,float target)=>false;
 internal static bool HoldTimer(TileEntityWorkstation s,RecipeQueueItem r)=>false;
 internal static bool BlocksNewWork(TileEntityWorkstation s)=>false;
 internal static bool PrepareFinal(TileEntityWorkstation s,RecipeQueueItem r,ref float target,float work,ItemStack[] tools,out Attempt attempt){attempt=null;return true;}
 internal static void Applied(Attempt attempt){}
 internal static void ObserveEntered(TileEntityWorkstation s){}
 internal static bool AllowsCompletion(TileEntityWorkstation s,RecipeQueueItem r)=>true;
 internal static bool HasSavedEarned(TileEntityWorkstation s,RecipeQueueItem r)=>false;
}

// This cohort has zero bonus rewards and disabled earned-protocol activation.
// Unexpected settlement entry fails loudly; no synthetic payment/receipt is issued.
internal static class RebirthRecyclerPlacedSettlement {
 internal static bool HoldsStation(TileEntityWorkstation station)=>false;
 internal static void BeginViewedCompletion(params object[] args){throw new Exception("Unexpected bonus settlement branch");}
 internal static void BeginAuthorityRemoteCompletion(params object[] args){throw new Exception("Unexpected bonus settlement branch");}
 internal static bool BeginAuthorityAction(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,string action,ItemStack[] input,RecipeQueueItem[] queue,IList<ItemStack> bonuses,ItemStack[] output,ItemStack[] tools,Func<bool> current,out string reason,bool completion,float seconds){reason=null;throw new Exception("Unexpected bonus settlement branch");}
 internal static bool BeginRemoteAction(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,Guid group,string action,int slot,int units,ItemStack[] input,RecipeQueueItem[] queue,IList<ItemStack> bonuses,ItemStack[] output,ItemStack[] tools,Func<bool> current,out string reason,bool completion,float seconds){reason=null;throw new Exception("Unexpected bonus settlement branch");}
}
internal static class RebirthRemoteResourcePlacedStationClientCustody {internal static bool Blocks(TileEntityWorkstation station)=>false;}
internal static class RebirthProductiveEarnedRemoteHost {internal static bool BlocksQueue(TileEntityWorkstation station)=>true;}
internal static class RebirthProductiveEarnedRemoteClient {internal static bool HasPending(TileEntityWorkstation station)=>false;internal static bool IsExecuting(TileEntityWorkstation station)=>false;}