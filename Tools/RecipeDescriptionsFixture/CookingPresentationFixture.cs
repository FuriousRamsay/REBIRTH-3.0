using System;using System.Collections.Generic;using System.Globalization;using System.Linq;
public static class Localization{public static Dictionary<string,string> Values=new Dictionary<string,string>();public static string Get(string id){string s;return id!=null&&Values.TryGetValue(id,out s)?s:id;}}
public static class RebirthSurvivorUiText{
// UI_L
}
public class ItemClass{public string Name;public string GetItemName(){return Name;}public static string GetItem(string id){return id;}}
public class ItemValue{public ItemClass ItemClass=new ItemClass{Name="ingredient"};}
public class Recipe{public int count=2;public string GetName(){return "food";}}
public class Station{public bool Valid=true;public string Failure="Needs a pot";public bool CraftingRequirementsValid(Recipe r){return Valid;}public string CraftingRequirementsInvalidMessage(Recipe r){return Failure;}}
public class Prep{public bool IsPreparing;}
public static class RebirthCookingBatch{public static bool Heat;public static bool NeedsHeat(Recipe r){return Heat;}}
public static class RebirthCookingHeat{public static string FormatDuration(float seconds){return seconds.ToString("0")+"s";}}
public class Food{public float NutritionUnits=12.5f,InitialVolumeMl=500,FoodWaterMl=80;public bool IsDrink;}
public class Mood{public float BaseMoodInfluence=2.5f;}
public class Preview{public float craftingTime=7;}
public class Inventory{public int GetItemCount(string id){return 4;}}
public class XUi{public Inventory PlayerInventory=new Inventory();}
public static class Render{public static Dictionary<string,string> Labels=new Dictionary<string,string>();public static Recipe result=new Recipe();public static bool loadedIngredients=true,milling,hasAlternatives,hiddenRecipe;public static object selected;public static string missing;public static Prep Preparation=new Prep();public static Station station=new Station();public static int batch=3;public static XUi xui=new XUi();static void Text(string id,string value){Labels[id]=value;}static void Caption(string id,string value){Labels[id]=value;}static string[] AvailableSubstitutes(int i){return hasAlternatives?new[]{"a"}:new string[0];}
public static void Outcome(){
// OUTCOME
}
public static string Tooltip(ItemValue value,int need,int have,string[] swaps){
// TOOLTIP
return tip;}
public static string Stats(bool hasFood,bool hasMood,Food food,Mood mood){string stats="";var preview=new Preview();
// STATS
return stats;}
public static void Comfort(bool hasMood,string comfortPreview,string comfortReason){
// COMFORT
}
public static void Choice(bool valid,bool used,string id){int i=0;
// CHOICE
}
public static void Names(){
// NAMES
}
public static void Titles(){
// TITLES
}}
public static class Checks{static int count;static void A(bool v,string s){if(!v)throw new Exception(s);count++;}static string S(string key){return Render.Labels[key];}static void Main(){
// TEMPLATES
Localization.Values["ingredient"]="Water";Localization.Values["swap"]="Mineral Water";Localization.Values["food"]="Stew";
Render.Outcome();A(S("outcomeStatus")=="[70C47E]Ready to cook[-]","ready cook");Render.milling=true;Render.Outcome();A(S("outcomeStatus")=="[70C47E]Ready to process[-]","milling ready");Render.milling=false;Render.missing="Cooking Pot";Render.Outcome();A(S("outcomeStatus")=="[FF9696]MISSING COOKING POT[-]"&&S("outcomeHint")=="","missing tool priority/markup");Render.Preparation.IsPreparing=true;Render.Outcome();A(S("outcomeStatus").Contains("MISSING"),"tool priority over preparing");Render.missing=null;Render.Outcome();A(S("outcomeStatus")=="Preparing","preparing");Render.Preparation.IsPreparing=false;Render.result=null;Render.Outcome();A(S("outcomeStatus")=="Add ingredients"&&S("outcomeHint")=="Choose a recipe or assemble ingredients.","empty result hint");Render.result=new Recipe();Render.loadedIngredients=false;Render.Outcome();A(S("outcomeStatus")=="Ingredients not loaded"&&S("outcomeHint")=="Pull ingredients or place them in the grid.","unloaded ingredients");Render.loadedIngredients=true;Render.station.Valid=false;Render.Outcome();A(S("outcomeStatus")=="[F07070]Needs a pot[-]","native requirement message");Render.station.Valid=true;RebirthCookingBatch.Heat=false;Render.Outcome();A(S("outcomeHint")=="Cold preparation — no fuel required.","cold hint");Render.milling=true;Render.Outcome();A(S("outcomeHint")=="Milling requires no fuel.","milling fuel hint");Render.milling=false;Render.selected=null;Render.Outcome();A(S("ingredientGuide")=="Combine ingredients to discover a dish or make an improvised meal.","discovery guide");Render.milling=true;Render.Outcome();A(S("ingredientGuide")=="Combine the correct ingredients to discover a milling recipe.","milling guide");Render.milling=false;Render.selected=new object();Render.hasAlternatives=true;Render.Outcome();A(S("ingredientGuide").StartsWith("Select a swap icon"),"swap guide");Render.hasAlternatives=false;Render.Outcome();A(S("ingredientGuide")=="","no alternatives");
A(Render.Tooltip(null,3,4,null)=="","empty tooltip");A(Render.Tooltip(new ItemValue(),3,4,new[]{"swap"})=="Water\nRequired: 3 | Available: 4\nSubstitutions: Mineral Water\nUse the swap button to choose.","multiline tooltip/item names");A(Render.Tooltip(new ItemValue(),int.MaxValue,0,null).Contains("2147483647"),"large quantity intact");
var food=new Food();var mood=new Mood();A(Render.Stats(true,true,food,mood)=="Nutrition: 12.5    Water: 80 mL\nBase comfort: 2.5\nOutput: 6    Cook time: 21s","food stats values");food.IsDrink=true;A(Render.Stats(true,false,food,mood)=="Nutrition: 12.5    Water: 500 mL\nOutput: 6    Cook time: 21s","drink water branch");A(Render.Stats(false,false,food,mood)=="\nOutput: 6    Cook time: 21s","nonfood output stats");Render.Comfort(false,"x","y");A(S("actualComfort")=="","no mood");Render.Comfort(true,"","y");A(S("actualComfort")=="Your comfort: …","comfort pending");Render.Comfort(true,"+3","Satisfying");A(S("actualComfort")=="Your comfort: +3 — Satisfying","comfort values/reason");Render.Choice(true,true,"ingredient");A(S("choice0")=="Water — already used","used alternative");Render.Choice(true,false,"ingredient");A(S("choice0")=="Water (4)","available alternative");Render.Choice(false,false,"ingredient");A(S("choice0")=="","invalid alternative");
Render.result=null;Render.Names();A(S("resultName")=="Add ingredients"&&S("outcomeName")=="No meal selected","empty names");Render.result=new Recipe();Render.hiddenRecipe=true;Render.Names();A(S("resultName")=="Unfamiliar dish"&&S("outcomeName")=="Unfamiliar dish ×6","unfamiliar count");Render.hiddenRecipe=false;Render.Names();A(S("outcomeName")=="Stew ×6","localized item count");Render.Titles();A(S("stationQueueTitle")=="COOKING AREA"&&S("cook")=="COOK"&&S("outcomeStatusTitle")=="COOK STATUS","cook titles");Render.milling=true;Render.Titles();A(S("stationQueueTitle")=="PROCESSING AREA"&&S("cook")=="PROCESS"&&S("outcomeStatusTitle")=="PROCESS STATUS","milling titles");Localization.Values["xuiRebirthCookingIngredientsNotLoaded"]="LOAD THEM";Render.milling=false;Render.loadedIngredients=false;Render.Outcome();A(S("outcomeStatus")=="LOAD THEM","actual localization used");Localization.Values["xuiRebirthCookingIngredientsNotLoaded"]="xuiRebirthCookingIngredientsNotLoaded";Render.Outcome();A(S("outcomeStatus")=="Ingredients not loaded","echoed-key fallback");
Console.WriteLine("PASS "+count+" extracted actual cooking Render cases plus42 connected/unique/nonempty localization keys and newline decoding; native queue/services doubled, no game execution.");}}