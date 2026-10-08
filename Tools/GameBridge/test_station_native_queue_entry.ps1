#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Crafting/UI/RebirthStationNativeQueueEntry.cs'))
$fixture=@"
public class RebirthStationGridAdmission{public string JobId;public bool MatchesQueuedQuality(Recipe r,int m,int q){return r.Bound&&m==1&&q==4;}}public class RebirthStationTerminalIntent{public bool IsCompletion,Valid=true;public System.Xml.Linq.XElement Write(){return new System.Xml.Linq.XElement("intent",new System.Xml.Linq.XAttribute("actor",42),new System.Xml.Linq.XAttribute("valid",Valid));}public static bool TryRead(System.Xml.Linq.XElement n,RebirthStationGridAdmission a,out RebirthStationTerminalIntent i){i=new RebirthStationTerminalIntent{Valid=(bool)n.Attribute("valid")};return i.Valid;}}public class Recipe{public string JobId="job";public bool Marked=true,Bound=true,Resolved=true,Job=true;public int count=12,Tier=4;public float craftingTime=8.5f;}
public class RecipeQueueItem{public object RepairItem;public ushort AmountToRepair;public Recipe Recipe;public short Multiplier;public byte Quality;public float CraftingTimeLeft,OneItemCraftTime;public int StartingEntityId;public bool IsCrafting;}
public static class RebirthStationGridQueue{
 public class DefinitionBinding{public int Tier;}
 public static bool IsMarked(Recipe r)=>r.Marked;
 public static bool TryGetJobId(Recipe r,out string job){job=r.JobId;return r.Job;}
 public static bool TryGetDefinitionBinding(Recipe r,IList<Recipe> d,out DefinitionBinding b){b=new DefinitionBinding{Tier=r.Tier};return r.Bound;}
 public static bool HasResolvedDefinition(Recipe r)=>r.Resolved;
}
public static class QueueEntryFixture{
 static int checks;static void Check(bool b,string n){if(!b)throw new Exception(n);checks++;}
 public static string Run(){var recipe=new Recipe();var defs=new List<Recipe>{recipe};RecipeQueueItem entry;
 Check(RebirthStationNativeQueueEntry.TryCreate(recipe,defs,42,out entry)&&ReferenceEquals(entry.Recipe,recipe)&&entry.Multiplier==1&&entry.Quality==4&&entry.StartingEntityId==42&&!entry.IsCrafting&&entry.CraftingTimeLeft==8.5f&&entry.OneItemCraftTime==8.5f,"saved whole-batch native fields");
 Check(!RebirthStationNativeQueueEntry.TryCreate(recipe,defs,0,out entry)&&entry==null,"invalid starting actor");
 recipe.Marked=false;Check(!RebirthStationNativeQueueEntry.TryCreate(recipe,defs,42,out entry),"unmarked");recipe.Marked=true;
 recipe.Job=false;Check(!RebirthStationNativeQueueEntry.TryCreate(recipe,defs,42,out entry),"missing job");recipe.Job=true;
 recipe.Bound=false;Check(!RebirthStationNativeQueueEntry.TryCreate(recipe,defs,42,out entry),"unbound definition");recipe.Bound=true;
 recipe.Resolved=false;Check(!RebirthStationNativeQueueEntry.TryCreate(recipe,defs,42,out entry),"unresolved definition");recipe.Resolved=true;
 foreach(float time in new[]{float.NaN,float.PositiveInfinity,-1f}){recipe.craftingTime=time;Check(!RebirthStationNativeQueueEntry.TryCreate(recipe,defs,42,out entry),"invalid time");}recipe.craftingTime=8.5f;
 foreach(int count in new[]{0,32768}){recipe.count=count;Check(!RebirthStationNativeQueueEntry.TryCreate(recipe,defs,42,out entry),"invalid count");}recipe.count=12;
 recipe.Tier=7;Check(!RebirthStationNativeQueueEntry.TryCreate(recipe,defs,42,out entry),"invalid quality");
 var job=Guid.NewGuid();int index;var queue=new[]{new RecipeQueueItem(),new RecipeQueueItem(),new RecipeQueueItem(),new RecipeQueueItem()};
 Check(RebirthStationNativeQueueEntry.TryFindVacantSlot(queue,job,out index)&&index==3,"last empty native slot selected");
 queue[3].Recipe=new Recipe{Marked=false};queue[3].Multiplier=1;Check(RebirthStationNativeQueueEntry.TryFindVacantSlot(queue,job,out index)&&index==2,"active slot preserved");
 queue[0].Recipe=new Recipe{JobId=job.ToString("N")};Check(!RebirthStationNativeQueueEntry.TryFindVacantSlot(queue,job,out index)&&index==-1,"duplicate marked job refused");queue[0].Recipe=null;
 queue[0].Recipe=new Recipe{Job=false};Check(!RebirthStationNativeQueueEntry.TryFindVacantSlot(queue,job,out index),"malformed marked job refused");queue[0].Recipe=null;
 queue[0]=null;Check(!RebirthStationNativeQueueEntry.TryFindVacantSlot(queue,job,out index),"incomplete native queue refused");queue[0]=new RecipeQueueItem();
 for(int i=0;i<3;i++)queue[i].RepairItem=new object();Check(!RebirthStationNativeQueueEntry.TryFindVacantSlot(queue,job,out index),"repair-owned slots not overwritten");
 queue=new[]{new RecipeQueueItem(),new RecipeQueueItem(),new RecipeQueueItem(),new RecipeQueueItem()};recipe=new Recipe{JobId=job.ToString("N")};RebirthStationNativeQueueEntry.TryCreate(recipe,defs,42,out entry);RecipeQueueItem[] staged;
 Check(RebirthStationNativeQueueEntry.TryBuildReplacement(queue,job,entry,out staged)&&staged[3].IsCrafting&&!entry.IsCrafting&&queue[3].Recipe==null&&!ReferenceEquals(staged,queue),"replacement last slot activates detached entry without native mutation");
 queue[3].Recipe=new Recipe{Marked=false};queue[3].Multiplier=2;queue[3].CraftingTimeLeft=19;
 Check(RebirthStationNativeQueueEntry.TryBuildReplacement(queue,job,entry,out staged)&&!staged[2].IsCrafting&&ReferenceEquals(staged[3],queue[3])&&staged[3].Multiplier==2&&staged[3].CraftingTimeLeft==19,"replacement preserves occupied native entry and order");
 Check(!RebirthStationNativeQueueEntry.TryBuildReplacement(queue,Guid.NewGuid(),entry,out staged)&&staged==null,"replacement job mismatch refused");
 entry.RepairItem=new object();Check(!RebirthStationNativeQueueEntry.TryBuildReplacement(queue,job,entry,out staged),"repair entry cannot replace recipe slot");
 var cancellation=new RebirthStationTerminalIntent();var admission=new RebirthStationGridAdmission{JobId=job.ToString("N")};var target=new RecipeQueueItem{Recipe=new Recipe{JobId=admission.JobId},Multiplier=1,Quality=4,StartingEntityId=42,IsCrafting=true,CraftingTimeLeft=3};var unrelated=new RecipeQueueItem{Recipe=new Recipe{Marked=false},Multiplier=2,StartingEntityId=99,CraftingTimeLeft=19,RepairItem=new object(),AmountToRepair=7};queue=new[]{unrelated,target};
 Check(RebirthStationNativeQueueEntry.TryBuildCancellation(queue,admission,cancellation,42,out staged)&&ReferenceEquals(staged[0],unrelated)&&staged[1].Recipe==null&&staged[1].StartingEntityId==-1&&!staged[1].IsCrafting&&queue[1].Recipe!=null&&target.CraftingTimeLeft==3,"cancellation stages only exact job and preserves unrelated native entry");
 cancellation.IsCompletion=true;Check(!RebirthStationNativeQueueEntry.TryBuildCancellation(queue,admission,cancellation,42,out staged),"completion choice cannot cancel");cancellation.IsCompletion=false;
 Check(!RebirthStationNativeQueueEntry.TryBuildCancellation(queue,admission,cancellation,99,out staged),"different actor cannot cancel");target.Quality=5;Check(!RebirthStationNativeQueueEntry.TryBuildCancellation(queue,admission,cancellation,42,out staged),"saved quality mismatch cannot cancel");target.Quality=4;
 target.Multiplier=2;Check(!RebirthStationNativeQueueEntry.TryBuildCancellation(queue,admission,cancellation,42,out staged),"inflated multiplier cannot cancel");target.Multiplier=1;
 queue=new[]{target,target};Check(!RebirthStationNativeQueueEntry.TryBuildCancellation(queue,admission,cancellation,42,out staged),"duplicate cancellation job refused");queue=new[]{unrelated};Check(!RebirthStationNativeQueueEntry.TryBuildCancellation(queue,admission,cancellation,42,out staged),"missing queue does not create refund or cancellation proof");
 queue=new[]{target};target.OneItemCraftTime=float.NaN;Check(!RebirthStationNativeQueueEntry.TryBuildCancellation(queue,admission,cancellation,42,out staged),"malformed timer held");target.OneItemCraftTime=8;
 cancellation.Valid=false;Check(!RebirthStationNativeQueueEntry.TryBuildCancellation(queue,admission,cancellation,42,out staged),"unbound cancellation intent held");
 return "PASS "+checks+" actual queue-entry builder checks with explicit native recipe/queue/catalogue doubles; native serialization/publication not exercised";
 }
}
"@
Add-Type -TypeDefinition ($source+$fixture)
[QueueEntryFixture]::Run()