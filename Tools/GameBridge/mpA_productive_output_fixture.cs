// Native timer ordering, tool storage, and item equality doubles. Actual source
// methods are extracted by the companion runner. Not native IL/save/network proof.
public sealed class Recipe {public int craftingToolType;public bool Special,Managed;}
public sealed class ItemClass {public static ItemClass GetForId(int id)=>new ItemClass();public string GetItemName()=>"tool";}
public sealed class Block {}
public sealed class ItemStack {public int count;public static ItemStack Empty=>new ItemStack();public ItemStack Clone()=>new ItemStack{count=count};}
public sealed class RecipeQueueItem {public Recipe Recipe;public int Multiplier=1;public float CraftingTimeLeft;}
public sealed class World {public bool Remote;public bool IsRemote()=>Remote;public object GetPrimaryPlayer()=>null;}
public sealed class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
public sealed class TileEntityWorkstation {
 public bool bUserAccessing,isBurning;
 public ItemStack[] Tools={new ItemStack{count=100}};public Block block=new Block();public RecipeQueueItem[] Queue;
 public bool hasRecipeInQueue()=>Queue!=null&&Queue.Length>0&&Queue[Queue.Length-1].Recipe!=null;
 public void setModified(){}
}
public static class RebirthRecyclerTransport {public static bool IsNativeOwner(TileEntityWorkstation s,object p)=>false;}
public static class RebirthStationToolAvailability {public static bool HasRequirement(ItemStack[] t,Recipe r)=>r.craftingToolType==0||t!=null&&t.Length>0&&t[0].count>0;}
public static class RebirthRecyclerBonuses {
 public static float Worn;
 public static float WearRatePerSecond(Block b)=>1;
 public static float MaxProcessingSeconds(ItemStack[] t,IReadOnlyList<string> required,float rate)=>required.Count==0?float.MaxValue:t[0].count;
 public static ItemStack[] ApplyWear(ItemStack[] t,IReadOnlyList<string> required,float rate,float seconds){if(required.Count>0){Worn+=seconds;t[0].count-=(int)seconds;}return t;}
}
public static class RebirthStationObservationDispatcher {public static bool Allowed=true;public static bool CanProcessNativeQueue(TileEntityWorkstation s)=>Allowed;}
public static class RebirthRecyclerRuntime {public static float Budget;public static bool IsMarked(Recipe r)=>r.Special;public static void ProcessNativeCarryover(TileEntityWorkstation s,float t){if(!RebirthRecyclerNativeLoop.TryConsumeNativeCarryover(s,t))throw new Exception("Native credit reused or mismatched");Budget+=t;}}
public static class RebirthCookingHeat {public static float Budget;public static bool Managed(Recipe r)=>r.Managed;public static void TickTile(TileEntityWorkstation s,float t){Budget+=t;}}
public static class RebirthStationGridIngredients {public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a?.count==b?.count;}
public static class MpAProductiveOutputFixture {
 /* ACTUAL_SPARSE_METHODS */
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 public static void Main(){
  var row=new RecipeQueueItem{Recipe=new Recipe{craftingToolType=1},CraftingTimeLeft=8};
  var station=new TileEntityWorkstation{Queue=new[]{row}};var frame=RebirthRecyclerNativeLoop.Enter(station);
  try{
   RebirthRecyclerNativeLoop.SetTime(row,5,station);Check(row.CraftingTimeLeft==5&&RebirthRecyclerBonuses.Worn==3,"productive initial decrement");
   row.CraftingTimeLeft=-5;RebirthRecyclerNativeLoop.SetTime(row,3,station);Check(row.CraftingTimeLeft==3&&RebirthRecyclerBonuses.Worn==8,"carryover counts next-unit work once");
   station.Tools[0].count=1;row.CraftingTimeLeft=4;RebirthRecyclerNativeLoop.SetTime(row,-2,station);Check(row.CraftingTimeLeft==3,"break truncates work to remaining tool durability");
   Check(!RebirthRecyclerNativeLoop.Continue(station),"broken tool blocks inner-loop output");
   station.Tools[0].count=2;row.Recipe=new Recipe{craftingToolType=1};row.Multiplier=1;row.CraftingTimeLeft=2;
   RebirthRecyclerNativeLoop.SetTime(row,-3,station);Check(station.Tools[0].count==0&&RebirthRecyclerNativeLoop.Continue(station),"Exactly earned completion survives final tool break");
   // Output-full native retry has no timer write and no new productive work.
   float earnedWear=RebirthRecyclerBonuses.Worn;Check(RebirthRecyclerNativeLoop.Continue(station)&&RebirthRecyclerBonuses.Worn==earnedWear,"Output-full earned retry adds no wear");
   row.Multiplier=2;Check(!RebirthRecyclerNativeLoop.Continue(station),"Earned unit cannot authorize unworked next application");
   row.Recipe=new Recipe{Special=true};row.CraftingTimeLeft=8;RebirthRecyclerNativeLoop.SetTime(row,5,station);Check(row.CraftingTimeLeft==8&&RebirthRecyclerRuntime.Budget==3,"specialized row receives positive carryover without native output");
   row.Recipe=new Recipe{Managed=true};RebirthRecyclerNativeLoop.SetTime(row,8,station);Check(RebirthCookingHeat.Budget==0,"zero budget does not fabricate work");
   row.Recipe=new Recipe();float old=RebirthRecyclerBonuses.Worn;RebirthRecyclerNativeLoop.SetTime(row,5,station);Check(RebirthRecyclerBonuses.Worn==old,"no required degradable tool adds no wear");
  }finally{RebirthRecyclerNativeLoop.Leave(frame);}
  var before=new[]{new ItemStack{count=7},new ItemStack{count=1},new ItemStack{count=0}};
  var after=new[]{new ItemStack{count=7},new ItemStack{count=3},new ItemStack{count=2}};Sparse(before,after);
  var live=new[]{new ItemStack{count=99},new ItemStack{count=1},new ItemStack{count=0}};
  Check(Delta(live,before,after,false),"unrelated slot is outside original sparse delta");
  live[1].count=3;Check(Mixed(live,before,after)&&!Delta(live,before,after,true),"partial original setter outcome remains recoverable");
  live[2].count=2;Check(Delta(live,before,after,true),"all original native postimages terminal");
  live[1].count=4;Check(!Mixed(live,before,after),"unknown changed destination never accepted");
  Console.WriteLine("PASS actual native-loop work accounting and sparse original delta methods with disclosed doubles");
 }
}

// Earned durability activation is deliberately doubled OFF in this mixed-loop fixture.
// Separate native publication/remote protocol qualification is still required.
internal static class RebirthProductiveEarnedCoordinator
{
 // No adopted native or mobile zero-completion capability in this cohort.
 internal static bool IsExecutingMobilePaidZeroCompletion(TileEntityWorkstation station)=>false;
 internal static bool IsExecutingPaidNativeZeroCompletion(TileEntityWorkstation station)=>false;
 internal sealed class Attempt {}
 internal static void BeforeNativeTimerWrite(TileEntityWorkstation s,RecipeQueueItem r){}
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
