using System;using System.Linq;using System.Collections.Generic;
class ItemStack{}
class Recipe{public int Id,craftingToolType;public bool Eligible,Cooking,Match,Tool,Known;public string Method;}
class Tools{public bool HasRequirement(Recipe r){return r.Tool;}}
class Station{public string Workstation="campfire";public Tools toolWindow=new Tools();}
class PlayerUI{public object entityPlayer;}
class XUi{public PlayerUI playerUI=new PlayerUI();}
static class XUiM_Recipes{public static List<Recipe> Recipes;public static List<Recipe> GetRecipes(){return Recipes;}}
static class RebirthCookingCatalogue{public static int Clones;public static Recipe ForStation(Recipe r,string s){Clones++;return r;}public static bool AtStation(Recipe r,string s){return r.Eligible;}public static bool IsCooking(Recipe r){return r.Cooking;}public static bool Known(object p,Recipe r){return r.Known;}}
static class RebirthCookingHeatRules{public static string Method(Recipe r){return r.Method;}}
class Subject{public Station station=new Station();public XUi xui=new XUi();public Recipe selected;public bool hiddenRecipe;public string Method="Pot";public int LastAssigned=-1;public List<int> Visits=new List<int>();
bool Matches(Recipe r,List<ItemStack> inputs,bool ghosts){Visits.Add(r.Id);if(r.Match)LastAssigned=r.Id;return r.Match;}
public Recipe Old(List<ItemStack> inputs){
        foreach(var source in XUiM_Recipes.GetRecipes().OrderByDescending(r=>r.craftingToolType!=0))
        {
            var r=RebirthCookingCatalogue.ForStation(source,station.Workstation);
            if(RebirthCookingCatalogue.IsCooking(r)&&RebirthCookingCatalogue.AtStation(source,station.Workstation)&&Matches(r,inputs,false)&&(station.toolWindow==null||station.toolWindow.HasRequirement(r))&&(selected!=null||RebirthCookingHeatRules.Method(r)==Method)){hiddenRecipe=!RebirthCookingCatalogue.Known(xui.playerUI.entityPlayer,r);return r;}
        }
return null;}
// PRODUCTION_METHOD
public Recipe Run(List<ItemStack> inputs){return ResolveKnownRecipe(XUiM_Recipes.Recipes,inputs);}
public IEnumerable<Recipe> All(){return StationRecipesInPriorityOrder(XUiM_Recipes.Recipes);}}
class Check{static void Main(){var rng=new Random(813);var inputs=new List<ItemStack>();int oldClones=0,newClones=0;for(int trial=0;trial<500;trial++){var recipes=new List<Recipe>();for(int i=0;i<120;i++)recipes.Add(new Recipe{Id=i,craftingToolType=rng.Next(2),Eligible=rng.Next(4)==0,Cooking=rng.Next(2)==0,Match=rng.Next(5)==0,Tool=rng.Next(2)==0,Known=rng.Next(2)==0,Method=rng.Next(2)==0?"Pot":"Pan"});XUiM_Recipes.Recipes=recipes;var old=new Subject();var updated=new Subject();if(trial%3==0){old.station.toolWindow=null;updated.station.toolWindow=null;}RebirthCookingCatalogue.Clones=0;var a=old.Old(inputs);oldClones+=RebirthCookingCatalogue.Clones;RebirthCookingCatalogue.Clones=0;var b=updated.Run(inputs);newClones+=RebirthCookingCatalogue.Clones;if(!object.ReferenceEquals(a,b)||old.hiddenRecipe!=updated.hiddenRecipe||old.LastAssigned!=updated.LastAssigned||!old.Visits.SequenceEqual(updated.Visits))throw new Exception("different recipe/side effects "+trial);if(!recipes.OrderByDescending(r=>r.craftingToolType!=0).Where(r=>r.Eligible).SequenceEqual(updated.All()))throw new Exception("different full candidate sequence "+trial);}
if(newClones>=oldClones)throw new Exception("No clone reduction");Console.WriteLine("PASS: 500 randomized old/new selection and match-order comparisons; stub clone calls "+oldClones+" -> "+newClones+" (not an in-game timing measurement)");}}
