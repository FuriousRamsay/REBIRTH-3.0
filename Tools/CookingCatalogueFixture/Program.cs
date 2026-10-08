using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Xml.Linq;
public class EntityPlayer {public HashSet<string> Knowledge=new HashSet<string>();}
public class ItemClass {public string Name,MetabolismType;public static Dictionary<int,ItemClass> Items=new Dictionary<int,ItemClass>();public string GetItemName()=>Name;public static ItemClass GetForId(int n)=>Items.TryGetValue(n,out var v)?v:null;public static ItemValue GetItem(string n)=>new ItemValue {ItemClass=new ItemClass {Name=n},type=99};}
public class ItemValue {public int type;public ItemClass ItemClass;}
public class ItemStack {public ItemValue itemValue;public bool IsEmpty()=>itemValue==null;public ItemStack Clone()=>this;}
public class Recipe {public int itemValueType,count,craftingToolType,craftingTier;public string craftingArea,tags;public float craftingTime,craftExpGain;public object Effects;public bool UseIngredientModifier;public List<ItemStack> ingredients=new List<ItemStack>();public string GetName()=>ItemClass.GetForId(itemValueType)?.Name;}
public class RebirthLiteratureDefinition {public string Kind,MarkerId,KnowledgeId;}
public class Rule {public string KnowledgeId;}
public static class RebirthProgressionRuntimeConfig {public static Dictionary<string,Rule> Rules=new Dictionary<string,Rule>();public static bool TryGetRecipeRule(string n,out Rule r)=>Rules.TryGetValue(n,out r);public static bool TryGetLiterature(string n,out RebirthLiteratureDefinition d){d=new RebirthLiteratureDefinition {Kind="recipe",KnowledgeId="card:"+n};return true;}}
public static class RebirthKnowledgeService {public static bool HasKnowledge(EntityPlayer p,string n)=>p?.Knowledge.Contains(n)==true;}
public class Policy {public string Value,PrimarySkill;public bool IsDisabled=>Value=="disabled";public bool IsUniversal=>Value=="universal";}
public static class RebirthCraftingProgressionRegistry {
 public static Dictionary<string,Policy> Values=XDocument.Load(Path.Combine(AppContext.BaseDirectory,"policy.xml")).Descendants("recipe").ToDictionary(e=>(string)e.Attribute("id"),e=>new Policy {Value=(string)e.Attribute("policy"),PrimarySkill=(string)e.Attribute("primary_skill")});
 public static bool TryGetRecipe(string n,out Policy p)=>Values.TryGetValue(n,out p);
}
public static class RebirthServiceCraftSkillService {public static string ClassifyRecipe(Recipe r)=>RebirthCraftingProgressionRegistry.Values.TryGetValue(r.GetName(),out var p)&&!string.IsNullOrEmpty(p.PrimarySkill)?p.PrimarySkill:r.GetName().StartsWith("drink")?"skill.drink_preparation":"skill.cooking";}
public static class RebirthCraftOutcomeService {public static bool IsWellPreparedRecipe(string n)=>false;}
public class RebirthConsumableDefinition {public bool IsFood,IsDrink;}
public static class RebirthConsumableResolver {public static bool TryResolve(ItemClass i,out RebirthConsumableDefinition d){d=new RebirthConsumableDefinition {IsDrink=i.MetabolismType=="Drink",IsFood=i.MetabolismType=="Food"};return d.IsDrink||d.IsFood;}public static bool TryResolve(ItemValue v,out RebirthConsumableDefinition d)=>TryResolve(v.ItemClass,out d);}
public static class RebirthCookingItemStats {public static bool FullIngredient(ItemValue v)=>true;}
public static class RebirthCookingRecipeLookup {public static bool IsCookingIngredient(int t)=>true;}
public class XUi {public UI playerUI=new UI();public Inventory PlayerInventory=new Inventory();}
public class UI {public EntityPlayer entityPlayer;}
public class Inventory {public int GetItemCount(ItemValue v)=>1;}
public static class RebirthSurvivorUiText {public static string L(string key,string fallback)=>fallback;}
public static class Log {public static void Error(string s)=>throw new Exception(s);}
class Program {
 static void AuditNativeDrinkCorpus(){
 string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"Config","_Survivor","crafting_progression.xml"))){var parent=Directory.GetParent(root);if(parent==null)throw new Exception("project root missing");root=parent.FullName;}
 var nativeRecipes=XDocument.Load(Path.GetFullPath(Path.Combine(root,"../../Data/Config/recipes.xml"))).Root.Elements("recipe").ToList();
 var nativeItems=XDocument.Load(Path.GetFullPath(Path.Combine(root,"../../Data/Config/items.xml"))).Root.Elements("item").ToDictionary(e=>(string)e.Attribute("name"));
 var metabolism=XDocument.Load(Path.Combine(root,"Config","_Metabolism","items.xml"));
 string TypeOf(string name,HashSet<string> seen){
  if(!seen.Add(name))throw new Exception("cyclic native item inheritance "+name);
  var patch=metabolism.Root.Elements("append").Where(e=>(string)e.Attribute("xpath")=="/items/item[@name='"+name+"']").SelectMany(e=>e.Elements("property")).LastOrDefault(e=>(string)e.Attribute("name")=="RebirthMetabolismType");
  if(patch!=null)return (string)patch.Attribute("value");
  if(!nativeItems.TryGetValue(name,out var item))return null;
  var direct=item.Elements("property").LastOrDefault(e=>(string)e.Attribute("name")=="RebirthMetabolismType");if(direct!=null)return (string)direct.Attribute("value");
  var extends=item.Elements("property").FirstOrDefault(e=>(string)e.Attribute("name")=="Extends");return extends==null?null:TypeOf((string)extends.Attribute("value"),seen);
 }
 int corpus=0,id=100;
 foreach(var pair in RebirthCraftingProgressionRegistry.Values.Where(p=>p.Key.StartsWith("drink")&&p.Key!="drinkJarEmpty")){
  string name=pair.Key;var matches=nativeRecipes.Where(e=>(string)e.Attribute("name")==name).ToList();Check(matches.Count==1,"native recipe identity drift "+name);var node=matches[0];
  var recipe=Make(id++,name);recipe.craftingArea=(string)node.Attribute("craft_area")??"";ItemClass.Items[recipe.itemValueType].MetabolismType=TypeOf(name,new HashSet<string>());
  Check(ItemClass.Items[recipe.itemValueType].MetabolismType=="Drink","native/metabolism inheritance no Drink type "+name);
  Check(RebirthCookingCatalogue.IsCooking(recipe)&&RebirthCookingCatalogue.IsDrink(recipe),"drink excluded or miscategorized "+name);
  var dish=RebirthCookingCatalogue.Get(name);Check(dish!=null,"missing runtime dish "+name);
  bool campfire=name!="drinkJarBeer";Check(RebirthCookingCatalogue.AtStation(recipe,"campfire")==campfire,"wrong campfire applicability "+name);
  if(name=="drinkJarBoiledWater")Check(RebirthCookingCatalogue.Known(new EntityPlayer(),recipe),"fresh player water hidden");
  else{
   var fresh=new EntityPlayer();Check(!RebirthCookingCatalogue.Known(fresh,recipe),"unknown gated drink exposed "+name);
   Check(dish.Cards.Count==1,"missing/ambiguous drink card "+name);fresh.Knowledge.Add("card:"+dish.Cards[0]);Check(RebirthCookingCatalogue.Known(fresh,recipe),"studied drink hidden "+name);
  }
  Console.WriteLine("CORPUS "+name+" station="+dish.Station+" skill="+pair.Value.PrimarySkill+" campfire="+campfire);corpus++;
 }
 Check(corpus==13,"native drink corpus coverage changed; review added/removed drinks");
 } static int count;static void Check(bool v,string s){if(!v)throw new Exception(s);count++;}
 static Recipe Make(int id,string name){ItemClass.Items[id]=new ItemClass {Name=name,MetabolismType=name.StartsWith("drink")?"Drink":"Food"};return new Recipe {itemValueType=id,craftingArea="campfire"};}
 static void Main(){
 var player=new EntityPlayer();var water=Make(1,"drinkJarBoiledWater");var coffee=Make(2,"drinkJarCoffee");var food=Make(3,"foodBakedPotato");
 Check(RebirthCraftingProgressionRegistry.Values[water.GetName()].IsUniversal,"actual water policy not universal");
 Check(RebirthCookingCatalogue.Known(player,water),"universal water hidden behind card");
 Check(RebirthCookingCatalogue.IsDrink(water),"Cooking-trained water not in Drinks");
 Check(RebirthServiceCraftSkillService.ClassifyRecipe(water)=="skill.cooking","water skill changed");
 Check(RebirthCookingCatalogue.IsCooking(water)&&RebirthCookingCatalogue.AtStation(water,"campfire"),"water not a campfire recipe");
 Check(!RebirthCookingCatalogue.Known(player,coffee),"undiscovered coffee leaked");
 player.Knowledge.Add("recipebook.cooking.basics");Check(!RebirthCookingCatalogue.Known(player,coffee),"broad knowledge bypassed recipe card");
 var card=RebirthCookingCatalogue.Get(coffee.GetName()).Cards.First();
 player.Knowledge.Add("card:"+card);Check(RebirthCookingCatalogue.Known(player,coffee),"studied coffee still hidden");
 Check(RebirthCookingCatalogue.IsDrink(coffee)&&RebirthCookingCatalogue.AtStation(coffee,"campfire"),"coffee missing drink station category");
 Check(!RebirthCookingCatalogue.IsDrink(food)&&RebirthCookingCatalogue.Known(player,food),"basic food regressed");
 var disabled=Make(4,"fixtureDisabled");RebirthCraftingProgressionRegistry.Values[disabled.GetName()]=new Policy {Value="disabled"};Check(!RebirthCookingCatalogue.Known(player,disabled),"disabled recipe exposed");
 Check(!RebirthCookingCatalogue.Known(player,null)&&!RebirthCookingCatalogue.IsDrink(null),"null accepted");
  AuditNativeDrinkCorpus();
 Console.WriteLine("PASS "+count+" actual production catalogue checks with native-type doubles and real recipe policy/runtime XML; no native UI/crafting validation.");
 }
}