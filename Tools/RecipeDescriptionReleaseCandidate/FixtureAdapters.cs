using System;
using System.Collections.Generic;
public class Recipe { public int itemValueType; public string Name="unknown"; public string GetName()=>Name; }
public static class CraftingManager { public static Recipe Current; public static Recipe GetRecipe(string s)=>Current; }
public class ItemValue { public ItemValue(){} public ItemValue(int t){type=t;ItemClass=ItemClass.Current?.ItemClass;} public int type; public ItemClass ItemClass; public bool IsEmpty()=>ItemClass==null; }
public class ItemClass { public static ItemClass GetForId(int t)=>Current?.ItemClass; public static ItemValue Current; public static bool Throws; public bool block; public string key; public bool IsBlock()=>block; public string GetItemDescriptionKey()=>key; public static ItemValue GetItem(string s,bool b){if(Throws)throw new Exception();return Current;} }
public class Block { public string DescriptionKey; public static Block[] list; }
public static class Localization { public static Dictionary<string,string> Text=new Dictionary<string,string>(); public static string Get(string k)=>Text.TryGetValue(k,out var v)?v:k; }
public static class RecipeDescriptionChecks {
public static int Run(){int n=0;Action<string,string> check=(a,b)=>{if(a!=b)throw new Exception("Expected "+b+" got "+a);n++;};
Localization.Text["ammoAPGroupDesc"]="Armor-piercing ammunition."; ItemClass.Current=new ItemValue{type=7,ItemClass=new ItemClass{key="ammoAPGroupDesc"}};
check(RebirthRecipeDescriptionText.Get("ammo9mmBulletAP"),"Armor-piercing ammunition.");
Localization.Text["ammo9mmBulletAPDesc"]="Fallback";check(RebirthRecipeDescriptionText.Get("ammo9mmBulletAP"),"Fallback");
ItemClass.Current.ItemClass.key="missing";check(RebirthRecipeDescriptionText.Get("ammo9mmBulletAP"),"Fallback");
ItemClass.Current.ItemClass.block=true;Block.list=new[]{new Block{DescriptionKey="effectiveBlock"}};ItemClass.Current.type=0;Localization.Text["effectiveBlock"]="Actual block description";check(RebirthRecipeDescriptionText.Get("blockAlias"),"Actual block description");
ItemClass.Current.type=100;check(RebirthRecipeDescriptionText.Get("unknown"),"");Block.list=null;check(RebirthRecipeDescriptionText.Get("unknown"),"");
ItemClass.Throws=true;check(RebirthRecipeDescriptionText.Get("ammo9mmBulletAP"),"Fallback");ItemClass.Throws=false;ItemClass.Current=null;check(RebirthRecipeDescriptionText.Get("ammo9mmBulletAP"),"Fallback");
check(RebirthRecipeDescriptionText.Get(null),"");check(RebirthRecipeDescriptionText.Get("  "),"");Localization.Text["blankDesc"]=" ";check(RebirthRecipeDescriptionText.Get("blank"),"");Localization.Text["caseDesc"]="CASEDESC";check(RebirthRecipeDescriptionText.Get("case"),"");CraftingManager.Current=new Recipe{itemValueType=7};ItemClass.Current=new ItemValue{type=7,ItemClass=new ItemClass{key="ammoAPGroupDesc"}};check(RebirthRecipeDescriptionText.Get("ammo_variant"),"Armor-piercing ammunition.");check(RebirthRecipeDescriptionText.GetForRecipe(null),"");ItemClass.Current=null;CraftingManager.Current=null;Localization.Text["xuiRebirthRecipeDescriptionFallback"]="Inspect ingredients below.";check(RebirthRecipeDescriptionText.GetForRecipe(new Recipe()),"Inspect ingredients below.");Localization.Text.Remove("xuiRebirthRecipeDescriptionFallback");check(RebirthRecipeDescriptionText.GetForRecipe(new Recipe()),"Check the ingredients and crafting requirements below.");return n;}
}


public class RebirthSkillDefinition { public string NameKey; }
public static class RebirthSurvivorDefinitionRegistry { public static Dictionary<string,string> Keys=new Dictionary<string,string>(); public static bool TryGetSkill(string id,out RebirthSkillDefinition skill){skill=null;if(!Keys.TryGetValue(id,out var key))return false;skill=new RebirthSkillDefinition{NameKey=key};return true;} }
public static class ActualSkillDescriptionChecks { public static int Run(){Localization.Text["xuiRebirthSkillMedicine"]="Medicine";RebirthSurvivorDefinitionRegistry.Keys["skill.medicine"]="xuiRebirthSkillMedicine";if(RebirthSkillDisplayNames.Get("skill.medicine")!="Medicine")throw new Exception();RebirthSurvivorDefinitionRegistry.Keys.Clear();if(RebirthSkillDisplayNames.Get("skill.medicine")!="Medicine")throw new Exception();if(RebirthSkillDisplayNames.Get("skill.armor_proficiency")!="Armor Proficiency")throw new Exception();return 3;} }
