// AUTHORED_NOT_RUN. Actual configured scrap helper, eligibility and native
// AddCraftComplete argument matcher. Native current/source and recipe outputs
// registry are explicit adapters; no native callback/action runs here.
using System;using System.Collections.Generic;using System.Linq;using System.Text;using System.Xml.Linq;
class ItemClass{internal bool HasQuality;internal string Name="originalIngredient";internal string GetItemName()=>Name;}
class ItemValue{internal int type=1,Quality;internal ItemClass ItemClass=new ItemClass();internal bool IsEmpty()=>type==0;internal int GetItemId()=>type;}
class ItemStack{internal int count=1;internal ItemValue itemValue=new ItemValue();internal bool IsEmpty()=>count<1||itemValue==null||itemValue.IsEmpty();internal ItemStack Clone()=>new ItemStack{count=count,itemValue=itemValue};}
class Recipe{internal bool IsScrap,Configured,Batch;internal int itemValueType=1,craftExpGain=10,count=2;internal string Name="recipe";internal List<ItemStack> ingredients=new List<ItemStack>{new ItemStack()};internal string GetName()=>Name;}
class CraftCompleteData{internal int CrafterEntityID=7,CraftExpGain=10,RecipeUsedCount=1;internal string RecipeName="recipe",ItemScrapped="originalIngredient";internal ItemStack CraftedItemStack=new ItemStack{count=2};internal CraftCompleteData(){}internal CraftCompleteData(int owner,ItemStack stack,string recipe,string scrap,int xp,int used){CrafterEntityID=owner;CraftedItemStack=stack;RecipeName=recipe;ItemScrapped=scrap;CraftExpGain=xp;RecipeUsedCount=used;}}
class TileEntityWorkstation{}
class RebirthCamperStationState{}
class WorkstationData{internal object TileEntity=new TileEntityWorkstation();}
class XUiC_WorkstationWindowGroup{internal WorkstationData WorkstationData=new WorkstationData();}
class Window{internal object Controller=new XUiC_WorkstationWindowGroup();}
class XUiC_RecipeStack{internal Recipe recipe=new Recipe();internal ItemValue OriginalItem,outputItemValue=new ItemValue();internal int AmountToRepair;internal Window windowGroup=new Window();}
static class RebirthRecipeOutputs{internal static bool HasOutputs(Recipe recipe)=>recipe.Configured;}
static class RebirthSurvivorMode{internal static bool IsEnabledForCurrentWorld()=>true;}
static class RebirthCookingBatch{internal static bool IsBatch(Recipe recipe)=>recipe.Batch;}
static class RebirthServiceCraftSkillService{internal static string ClassifyRecipe(Recipe recipe)=>recipe.IsScrap?"":"skill";}
// Native body/argument serialization remain explicit adapters. The whole
// production causal Add-description member is composed below, not reimplemented.
static class RebirthProductiveEarnedNativeBody{
 internal class Body{internal CraftCompleteData[] Completions;}
 internal static CraftCompleteData[] Initial=new CraftCompleteData[0];internal static Body Encoded;
 internal static bool TryRead(byte[] bytes,int offset,out Body body,out object ignored){body=new Body{Completions=Initial};ignored=null;return bytes!=null&&bytes.Length==1;}
 internal static byte[] Encode(Body body,int unused,out int offset){Encoded=body;offset=1;return new byte[]{9};}
}
static class RebirthRemoteResourcePlacedStationNativeReader{internal static CraftCompleteData Argument=new CraftCompleteData();internal static bool TryReadCompletionArgument(byte[] bytes,out CraftCompleteData value){value=Argument;return bytes.Length==1;}}
// ACTUAL_SCRAP_HELPER
static class Fixture{
 // ACTUAL_ELIGIBLE
 // ACTUAL_HOST_ARGUMENT
 // ACTUAL_MEMBER_DELTA
 // ACTUAL_ADD_BODY
 // Native complete-list serialization and cloning are explicit adapters.
 static byte[] Complete(CraftCompleteData[] rows)=>Encoding.UTF8.GetBytes(string.Join("|",rows.Select(row=>row.CrafterEntityID+":"+row.CraftedItemStack.itemValue.type+":"+row.CraftedItemStack.count+":"+row.ItemScrapped)));
 static byte[] Row(CraftCompleteData row)=>Complete(new[]{row});
 static CraftCompleteData[] Copy(CraftCompleteData[] rows)=>rows.Select(row=>new CraftCompleteData(row.CrafterEntityID,row.CraftedItemStack.Clone(),row.RecipeName,row.ItemScrapped,row.CraftExpGain,row.RecipeUsedCount)).ToArray();
 static byte[] ReadBytes(XElement node,string name)=>Convert.FromBase64String(node.Element(name).Value);
 static XElement Delta(CraftCompleteData[] before,CraftCompleteData[] after)=>new XElement("member",new XAttribute("contribution",2),new XElement("beforeComplete",Convert.ToBase64String(Complete(before))),new XElement("afterComplete",Convert.ToBase64String(Complete(after))));
 class Observation{internal XUiC_RecipeStack Card=new XUiC_RecipeStack();internal int OutputQuality=3;}
 class Frame{internal Observation Observation=new Observation();}
 class NativeAddOriginal{
  internal Recipe Recipe;internal Frame frame=new Frame();internal bool Live=true;
  private bool CurrentAtNativeAdd(TileEntityWorkstation station,int owner)=>Live;
  // ACTUAL_NATIVE_ARGUMENTS
 }
 static int checks;static void Need(bool value,string text){if(!value)throw new Exception(text);checks++;}
 static void Main(){
  var card=new XUiC_RecipeStack();Need(Eligible(card),"ordinary configured source unchanged");card.recipe.IsScrap=true;Need(!Eligible(card),"unconfigured scrap excluded");
  card.recipe.Configured=true;Need(Eligible(card),"configured native scrap source admitted without invented skill classification");
  card.recipe.ingredients.Clear();Need(!Eligible(card),"missing original native ingredient excluded");card.recipe.ingredients.Add(new ItemStack{count=0});Need(!Eligible(card),"empty original ingredient excluded");card.recipe.ingredients[0].count=1;
  var source=new NativeAddOriginal{Recipe=card.recipe};var item=source.frame.Observation.Card.outputItemValue;var station=new TileEntityWorkstation();
  Need(source.MatchesNativeAdd(station,1,item,"recipe","originalIngredient",10,2),"exact real native scrap item name");
  Need(!source.MatchesNativeAdd(station,1,item,"recipe","",10,2),"ordinary Add name cannot substitute for scrap");
  Need(!source.MatchesNativeAdd(station,1,item,"recipe","otherIngredient",10,2),"foreign scrap ingredient refused");
  Need(!source.MatchesNativeAdd(station,1,new ItemValue(),"recipe","originalIngredient",10,2),"equal generated primary cannot replace native item reference");
  Need(!source.MatchesNativeAdd(station,1,item,"recipe","originalIngredient",11,2),"foreign native XP arguments refused");
  Need(!source.MatchesNativeAdd(station,1,item,"recipe","originalIngredient",10,3),"foreign native count refused");
  source.Live=false;Need(!source.MatchesNativeAdd(station,1,item,"recipe","originalIngredient",10,2),"stale actual source refused");source.Live=true;
  source.Recipe.IsScrap=false;Need(source.MatchesNativeAdd(station,1,item,"recipe","",10,2)&&!source.MatchesNativeAdd(station,1,item,"recipe","originalIngredient",10,2),"ordinary native Add empty scrap name preserved");
  card.recipe.IsScrap=card.recipe.Configured=true;card.OriginalItem=new ItemValue();Need(!Eligible(card),"native repair identity remains excluded");card.OriginalItem=null;
  ((XUiC_WorkstationWindowGroup)card.windowGroup.Controller).WorkstationData.TileEntity=new RebirthCamperStationState();Need(!Eligible(card),"mobile native purpose remains separate");
  var hostArgument=new CraftCompleteData();Need(NativeArgumentMatches(hostArgument,card.recipe,7),"exact actual host native scrap argument");
  hostArgument.ItemScrapped="";Need(!NativeArgumentMatches(hostArgument,card.recipe,7),"empty host scrap name refused");hostArgument.ItemScrapped="otherIngredient";Need(!NativeArgumentMatches(hostArgument,card.recipe,7),"foreign host scrap name refused");
  card.recipe.IsScrap=false;hostArgument.ItemScrapped="";Need(NativeArgumentMatches(hostArgument,card.recipe,7),"ordinary host native argument preserved");
  card.recipe.IsScrap=true;var primary=new ItemStack{count=2};var empty=new CraftCompleteData[0];var added=new[]{new CraftCompleteData()};Need(MemberDelta(Delta(empty,added),empty,card.recipe,7,primary),"actual scrap singleton native delta");
  var previous=new[]{new CraftCompleteData()};var merged=new[]{new CraftCompleteData{CraftedItemStack=new ItemStack{count=4}}};Need(MemberDelta(Delta(previous,merged),previous,card.recipe,7,primary),"actual matching scrap aggregate native delta");
  var ordinary=new[]{new CraftCompleteData{ItemScrapped=""}};Need(MemberDelta(Delta(ordinary,ordinary.Concat(added).ToArray()),ordinary,card.recipe,7,primary),"same item ordinary receipt remains separate from scrap");Need(!MemberDelta(Delta(ordinary,merged),ordinary,card.recipe,7,primary),"ordinary aggregate cannot substitute for scrap aggregate");
  XElement AddDescription(CraftCompleteData[] before,CraftCompleteData[] after,CraftCompleteData aggregate)=>new XElement("deferredNativeContribution",
   new XElement("nativeAddArgument",Convert.ToBase64String(new byte[]{1})),new XElement("beforeComplete",Convert.ToBase64String(Complete(before))),
   new XElement("afterComplete",Convert.ToBase64String(Complete(after))),new XElement("aggregate",Convert.ToBase64String(Row(aggregate))));
  var description=AddDescription(empty,added,added[0]);
  Need(TryDescribeRemoteNativeAddBody(card.recipe,7,new byte[]{1},1,description,out var encoded,out int offset)&&encoded.Length==1&&offset==1&&RebirthProductiveEarnedNativeBody.Encoded.Completions.Length==1,"actual causal Add body description");
  Need(!TryDescribeRemoteNativeAddBody(card.recipe,8,new byte[]{1},1,description,out _,out _),"foreign creator cannot describe native Add");
  Need(!TryDescribeRemoteNativeAddBody(card.recipe,7,new byte[0],1,description,out _,out _),"unread native Before refused");
  Need(!TryDescribeRemoteNativeAddBody(card.recipe,7,new byte[]{1},1,AddDescription(previous,merged,merged[0]),out _,out _),"mismatched full native Before refused");
  Need(!TryDescribeRemoteNativeAddBody(card.recipe,7,new byte[]{1},1,AddDescription(empty,merged,merged[0]),out _,out _),"invented aggregate increment refused");
  RebirthProductiveEarnedNativeBody.Initial=previous;
  Need(TryDescribeRemoteNativeAddBody(card.recipe,7,new byte[]{1},1,AddDescription(previous,merged,merged[0]),out _,out _)&&RebirthProductiveEarnedNativeBody.Encoded.Completions[0].CraftedItemStack.count==4,"exact causal native coalescing description");
  Console.WriteLine("PASS "+checks+" actual native scrap route assertions");
 }
}
