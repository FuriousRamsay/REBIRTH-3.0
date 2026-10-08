using System;using System.Collections.Generic;using System.Globalization;using System.Linq;
public class EntityPlayer{}public class EntityPlayerLocal:EntityPlayer{}
public class XUi{public UI playerUI=new UI();}public class UI{public EntityPlayerLocal entityPlayer=new EntityPlayerLocal();}
public class Recipe{public int count=1;public string GetName(){return "fixture";}public ItemClass GetOutputItemClass(){return ItemClass.Output;}}
public class ItemClass{public static ItemClass Output=new ItemClass();public bool HasQuality;public string GetLocalizedItemName(){return "Bandage";}}
public class XUiC_RebirthPersonalCrafting{public bool CraftingRequirementsValid(Recipe r){return true;}}
public class Bridge{public bool CanCraft(Recipe r,int tier){return true;}}
public class XUiC_RebirthCraftingRecipeDetails{public int SelectedCraftingTier=1;public Bridge CommandBridge=new Bridge();}
public static class RebirthCraftingRequirementProjectionService{public class Requirement{public bool HasEnough=true;}}
public static class RebirthCraftOutcomeService{public class Snapshot{public string QualityRange="2–3";}public static Snapshot Build(EntityPlayer p,Recipe r,int b,int t){return new Snapshot();}}
public static class RebirthServiceCraftSkillService{public static string Skill="skill.medicine";public static string ClassifyRecipe(string s){return Skill;}}
public static class RebirthSkillAptitudeTraitFactory{public static string SkillIconKey(string s){return "medicine";}}
public static class XUiM_Recipes{public static bool GetRecipeIsUnlocked(XUi x,Recipe r){return true;}public static int GetRecipeCraftOutputCount(XUi x,Recipe r){return 2;}}
public static class RebirthSurvivorMode{public static bool IsEnabledForCurrentWorld(){return true;}}
public class RebirthCapabilityEvaluation{public bool IsAllowed=true;public string FirstMissingReason;}
public static class RebirthCapabilityService{public static RebirthCapabilityEvaluation EvaluateRecipe(EntityPlayer p,string s){return new RebirthCapabilityEvaluation();}}
public static class Localization{public static Dictionary<string,string> Values=new Dictionary<string,string>();public static string Get(string id){string s;return id!=null&&Values.TryGetValue(id,out s)?s:id;}}
public static class RebirthSurvivorUiText{
// UI_L
}
public class RebirthCraftSkillPreview{public class Snapshot{public float Gain;}public Snapshot Get(EntityPlayerLocal p,Recipe r,int b,bool book,int outputs=0){return new Snapshot{Gain=.0125f};}public static string SkillText(EntityPlayer p,string id,Snapshot s){return "Medicine 25.000";}
// GAIN_METHOD
}
// VIEW_MODEL
public static class RebirthCookingCatalogue{public static string EffectValue;public static string Effect(string id){return EffectValue;}
// TECHNIQUE
}
public class Prep{public bool IsPreparing;public float Percent;public float Progress(float t){return Percent;}}
public static class Cooking{public static Prep Preparation=new Prep();public static bool preparingRequest,ActiveBook;public static string ActiveMagazine,benefitBook,benefitMagazine;public static object Ready;public static float Now;public static string LastText,LastCaption;static void Text(string id,string text){LastText=text;}static void Caption(string id,string text){LastCaption=text;}
// REFERENCE
public static void RenderPrep(float remaining){
// PREPARATION
}
public static void RenderBenefits(){
// BENEFIT_LINES
}
}
public static class Checks{static int count;static void A(bool v,string s){if(!v)throw new Exception(s);count++;}static void Main(){
// TEMPLATES
var x=new XUi();var r=new Recipe();var details=new XUiC_RebirthCraftingRecipeDetails();var vm=RebirthCraftingOutcomeViewModel.Build(x,null,details,r,3,null,new RebirthCraftSkillPreview());A(vm.XpTitle=="SKILL GAIN"&&vm.Explanation=="Skill gain includes your current learning bonuses.","practice text");A(vm.ResultsValue=="Bandage  x6"&&vm.XpValue.Contains("+0.013"),"numeric semantics changed");
ItemClass.Output.HasQuality=true;vm=RebirthCraftingOutcomeViewModel.Build(x,null,details,r,1,null,null);A(vm.Explanation=="Quality: 2–3 • Skill gain includes your current learning bonuses.","quality learning template");RebirthServiceCraftSkillService.Skill="";vm=RebirthCraftingOutcomeViewModel.Build(x,null,details,r,1,null,null);A(vm.SkillValue=="No associated skill"&&vm.Explanation=="Quality: 2–3","no skill/quality branch");Localization.Values["xuiRebirthCraftSkillGainTitle"]="APTITUDE";vm=RebirthCraftingOutcomeViewModel.Build(null,null,null,null,1,null,null);A(vm.XpTitle=="APTITUDE","localized title unused");Localization.Values["xuiRebirthCraftSkillGainTitle"]=" ";A(RebirthCraftingOutcomeViewModel.Build(null,null,null,null,1,null,null).XpTitle=="SKILL GAIN","blank title fallback");
Cooking.RenderPrep(0);A(Cooking.LastText=="Optional: prepare for 10 seconds; benefits last 5 minutes."&&Cooking.LastCaption=="PREPARE","optional text");Cooking.RenderPrep(125);A(Cooking.LastText=="Preparation active for 02:05; you can refresh it."&&Cooking.LastCaption=="PREPARE AGAIN","active clock");Cooking.Preparation.IsPreparing=true;Cooking.Preparation.Percent=.425f;Cooking.RenderPrep(0);A(Cooking.LastText=="Preparing… 42%","progress changed");Cooking.preparingRequest=true;Cooking.RenderPrep(0);A(Cooking.LastText=="Confirming preparation…","pending priority");Localization.Values["xuiRebirthCookingConfirmingPreparation"]="WAITING";Cooking.RenderPrep(0);A(Cooking.LastText=="WAITING","translated pending text unused");
foreach(var pair in new[]{new[]{"T","20% less cooking time"},new[]{"H","+1 comfort with a compatible herb"},new[]{"Q","+1 comfort"},new[]{"?","No technique bonus"}}){RebirthCookingCatalogue.EffectValue=pair[0];A(RebirthCookingCatalogue.Benefit("mag")==pair[1],"technique effect text");}
A(Cooking.ReferenceText(null,null,"Book")=="[B58CFF]Book[-]\nNo reference","missing reference");Localization.Values["candidate"]="Food Guide";A(Cooking.ReferenceText(null,new List<string>{"candidate"},"Book").EndsWith("Food Guide"),"candidate name lost");
Cooking.benefitBook="book";Cooking.benefitMagazine="mag";Cooking.Ready=null;RebirthCookingCatalogue.EffectValue="Q";Cooking.RenderBenefits();A(Cooking.LastText=="Book: +20% skill gain\nMagazine: +1 comfort","available benefits");Cooking.Ready=new object();Cooking.ActiveBook=true;Cooking.ActiveMagazine="mag";Cooking.RenderBenefits();A(Cooking.LastText=="Active: +20% skill gain • +1 comfort","active benefits");Cooking.ActiveBook=false;Cooking.RenderBenefits();A(Cooking.LastText=="Active: +1 comfort","mag-only separator");Cooking.ActiveMagazine=null;Cooking.ActiveBook=true;Cooking.RenderBenefits();A(Cooking.LastText=="Active: +20% skill gain","book-only separator");
Console.WriteLine("PASS "+count+" extracted production Expected Outcome and cooking text branches using actual 23 localization entries; numeric preview/native services doubled; no game execution.");}}