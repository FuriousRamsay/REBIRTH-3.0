// AUTHORED_NOT_RUN. Actual eligibility predicate; native UI/type identities doubled.
using System;
class ItemValue {internal int type;internal bool IsEmpty()=>type==0;}
class Recipe {internal bool IsScrap,Batch;}
class Group {internal object Controller;}
class WorkstationData {internal object TileEntity=new object();}
class XUiC_WorkstationWindowGroup {internal WorkstationData WorkstationData=new WorkstationData();}
class RebirthCamperStationState {}
class XUiC_RecipeStack {internal Recipe recipe=new Recipe();internal int AmountToRepair;internal ItemValue OriginalItem;internal Group windowGroup=new Group{Controller=new XUiC_WorkstationWindowGroup()};}
static class RebirthSurvivorMode {internal static bool Enabled=true;internal static bool IsEnabledForCurrentWorld()=>Enabled;}
static class RebirthCookingBatch {internal static bool IsBatch(Recipe recipe)=>recipe.Batch;}
static class RebirthServiceCraftSkillService {internal static string ClassifyRecipe(Recipe recipe)=>"mechanic";}
static class RebirthRecipeOutputIntegration {internal static bool HasNativeScrapOutputs(Recipe recipe)=>false;} // Configured scrap purpose has its own actual-member cohort.
class Program {
 // ACTUAL_ELIGIBLE
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
  Need(!Eligible(null),"missing native card admitted");
  var card=new XUiC_RecipeStack();Need(Eligible(card),"ordinary crafting excluded");
  card.AmountToRepair=1;card.OriginalItem=new ItemValue{type=7};Need(!Eligible(card),"active repair admitted as crafting");
  card.AmountToRepair=0;Need(!Eligible(card),"native full-inventory repair retry admitted as ordinary crafting");
  card.OriginalItem=new ItemValue();Need(Eligible(card),"empty native repair identity blocked ordinary crafting");
  card.recipe.IsScrap=true;Need(!Eligible(card),"scrap admitted");card.recipe.IsScrap=false;
  card.recipe.Batch=true;Need(!Eligible(card),"batch cooking admitted");card.recipe.Batch=false;
  ((XUiC_WorkstationWindowGroup)card.windowGroup.Controller).WorkstationData.TileEntity=new RebirthCamperStationState();
  Need(!Eligible(card),"camper acquired unrelated ordinary crafting custody");
  Console.WriteLine("PASS actual completion eligibility with native UI doubles; actual repair retry gameplay NOT RUN");
 }
}
