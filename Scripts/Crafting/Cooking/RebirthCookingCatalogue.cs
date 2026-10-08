using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

#nullable disable

public static class RebirthCookingCatalogue
{
    public sealed class Dish
    {
        public string Item, Notes, Herbs, Station, Tool;
        public readonly List<string> Books = new List<string>(), Magazines = new List<string>(), Cards = new List<string>();
        public readonly Dictionary<string, string[]> Substitutes = new Dictionary<string, string[]>();
    }
    private static Dictionary<string, Dish> dishes;
    private static readonly HashSet<string> preparationReferences=new HashSet<string>(StringComparer.Ordinal);
    public static bool IsPreparationReference(string itemId)
    { Get(null); return !string.IsNullOrEmpty(itemId)&&preparationReferences.Contains(itemId); }
    private static readonly Dictionary<string,string> effects = new Dictionary<string,string>(StringComparer.Ordinal);
    private static readonly Dictionary<string,string[]> techniqueHerbs = new Dictionary<string,string[]>(StringComparer.Ordinal);
    public static bool HerbTechniqueApplies(string magazine,Recipe recipe,Dish dish)
    {
        Get(null);
        return magazine!=null&&dish!=null&&techniqueHerbs.TryGetValue(magazine,out var herbs)&&recipe.ingredients.Any(i=>herbs.Contains(i.itemValue.ItemClass.GetItemName())&&dish.Herbs.IndexOf(i.itemValue.ItemClass.GetItemName().Replace("foodCrop",""),StringComparison.OrdinalIgnoreCase)>=0);
    }
    public static string Effect(string magazine) { Get(null); return magazine != null && effects.TryGetValue(magazine, out var value) ? value : ""; }
    public static string Benefit(string magazine) => Effect(magazine) == "T" ? RebirthSurvivorUiText.L("xuiRebirthCookingTechniqueTime", "20% less cooking time") : Effect(magazine) == "H" ? RebirthSurvivorUiText.L("xuiRebirthCookingTechniqueHerb", "+1 comfort with a compatible herb") : Effect(magazine) == "Q" ? RebirthSurvivorUiText.L("xuiRebirthCookingTechniqueComfort", "+1 comfort") : RebirthSurvivorUiText.L("xuiRebirthCookingTechniqueNone", "No technique bonus");
    public static Dish Get(string name)
    {
        if (dishes == null)
        {
            dishes = new Dictionary<string, Dish>(StringComparer.OrdinalIgnoreCase);
            string path = Path.Combine(Path.GetDirectoryName(typeof(RebirthCookingCatalogue).Assembly.Location), "Config", "_Cooking", "runtime.xml");
            try
            {
                var root = XDocument.Load(path).Root;
                foreach (var e in root.Elements("technique"))
                {
                    string id=(string)e.Attribute("item");effects[id]=(string)e.Attribute("effect");
                    techniqueHerbs[id]=((string)e.Attribute("herbs")??"").Split(',');
                }
                foreach (XElement e in root.Elements("dish"))
                {
                    var d = new Dish { Item = (string)e.Attribute("item"), Notes = (string)e.Attribute("substitutions"), Herbs=(string)e.Attribute("herbs")??"", Station=(string)e.Attribute("station"), Tool=(string)e.Attribute("tool") };
                    d.Books.AddRange(e.Elements("book").Select(n => (string)n.Attribute("item")));
                    d.Magazines.AddRange(e.Elements("magazine").Select(n => (string)n.Attribute("item")));
                    d.Cards.AddRange(e.Elements("card").Select(n => (string)n.Attribute("item")));
                    foreach(string reference in d.Books)preparationReferences.Add(reference);
                    foreach(string reference in d.Magazines)preparationReferences.Add(reference);
                    foreach (var s in e.Elements("substitute")) d.Substitutes[(string)s.Attribute("ingredient")] = ((string)s.Attribute("choices")).Split(',');
                    dishes[d.Item] = d;
                }
            }
            catch (Exception ex) { Log.Error("[REBIRTH Cooking] Could not load cooking catalogue: " + ex.Message); }
        }
        return name != null && dishes.TryGetValue(name, out Dish value) ? value : null;
    }
    public static bool Studied(EntityPlayer player, string item)
    {
        RebirthLiteratureDefinition definition;
        if (!RebirthProgressionRuntimeConfig.TryGetLiterature(item, out definition)) return false;
        return RebirthKnowledgeService.HasKnowledge(player, definition.Kind == "theory" ? definition.MarkerId : definition.KnowledgeId);
    }
    // These everyday preparations deliberately have no recipe cards or discovery gate.
    private static readonly HashSet<string> BasicRecipes = new HashSet<string>(StringComparer.Ordinal)
    {
        "foodBakedPotato", "foodEggBoiled", "foodBoiledMeat", "foodCharredMeat",
        "foodGrilledMeat", "foodCornMeal", "foodCornOnTheCob"
    };
    public static bool Known(EntityPlayer player, Recipe r)
    {
        if (r == null) return false;
        // The declared universal policy outranks recipe-card metadata. A reference
        // card can teach technique without turning everyday boiling into a discovery gate.
        if (RebirthCraftingProgressionRegistry.TryGetRecipe(r.GetName(), out var policy) && policy != null)
        {
            if (policy.IsDisabled) return false;
            if (policy.IsUniversal) return true;
        }
        if (BasicRecipes.Contains(r.GetName())) return true;
        var d = Get(r.GetName());
        if(RebirthKnowledgeService.HasKnowledge(player,"cooking.recipe."+r.GetName()))return true;
        // Older individual recipe cards grant recipe.* rather than the generated card marker.
        if(RebirthProgressionRuntimeConfig.TryGetRecipeRule(r.GetName(),out var specificRule) &&
            specificRule.KnowledgeId?.StartsWith("recipe.",StringComparison.Ordinal)==true &&
            RebirthKnowledgeService.HasKnowledge(player,specificRule.KnowledgeId))return true;
        // Card dishes are learned from their specific card or by discovering that dish.
        // Background grants and broad legacy aliases are not card-read markers.
        if(d!=null&&d.Cards.Count>0)return d.Cards.Exists(c=>Studied(player,c));
        return RebirthProgressionRuntimeConfig.TryGetRecipeRule(r.GetName(),out var rule)&&
            !string.IsNullOrEmpty(rule.KnowledgeId)&&RebirthKnowledgeService.HasKnowledge(player,rule.KnowledgeId);
    }
    public static string Reference(XUi xui, List<string> items)
    {
        if (items == null) return null;
        foreach (string id in items)
            if (Studied(xui.playerUI.entityPlayer, id) && xui.PlayerInventory.GetItemCount(ItemClass.GetItem(id)) > 0) return id;
        return null;
    }
    public static bool IsHerb(ItemStack stack)
    {
        string name = stack?.itemValue?.ItemClass?.GetItemName() ?? "";
        return name.IndexOf("Basil", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Dill", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Sage", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    public static bool IsIngredient(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty()) return true;
        if (!RebirthCookingItemStats.FullIngredient(stack.itemValue))return false;
        if (IsHerb(stack)) return true;
        string name = stack.itemValue.ItemClass.GetItemName();
        // Recipe inputs include flour, flowers and water as well as directly edible items.
        if (RebirthCookingRecipeLookup.IsCookingIngredient(stack.itemValue.type)) return true;
        RebirthConsumableDefinition d;
        return RebirthConsumableResolver.TryResolve(stack.itemValue, out d) && (d.IsFood || d.IsDrink);
    }
    public static bool IsCooking(Recipe r)
    {
        if (r == null || r.GetName()=="foodCharredMeat" || RebirthCraftOutcomeService.IsWellPreparedRecipe(r.GetName())) return false;
        string skill = RebirthServiceCraftSkillService.ClassifyRecipe(r);
        return skill == "skill.cooking" || skill == "skill.drink_preparation";
    }
    public static bool IsDrink(Recipe recipe)
    {
        if (recipe == null) return false;
        // UI categories describe the output, independently of the skill it trains.
        // Boiled water deliberately trains Cooking, but belongs in Drinks.
        ItemClass output = ItemClass.GetForId(recipe.itemValueType);
        if (output != null && RebirthConsumableResolver.TryResolve(output, out var consumable))
            return consumable.IsDrink;
        return RebirthServiceCraftSkillService.ClassifyRecipe(recipe) == "skill.drink_preparation";
    }
    public static Recipe ForStation(Recipe recipe, string station)
    {
        var d=Get(recipe.GetName());
        if(d==null||string.IsNullOrEmpty(d.Station))return recipe;
        return new Recipe{itemValueType=recipe.itemValueType,count=recipe.count,craftingArea=station,craftingToolType=string.IsNullOrEmpty(d.Tool)?0:ItemClass.GetItem(d.Tool).type,craftingTime=recipe.craftingTime,craftExpGain=recipe.craftExpGain,tags=recipe.tags,Effects=recipe.Effects,craftingTier=recipe.craftingTier,UseIngredientModifier=false,ingredients=recipe.ingredients.Select(i=>i.Clone()).ToList()};
    }
    public static bool AtStation(Recipe r, string station)
    {
        string area=Get(r.GetName())?.Station ?? r.craftingArea ?? "";
        // Cold assembly can use any cooking work surface. Milling remains at its own station.
        if(area=="cold")return true;
        if(area==station||(station=="cntWoodBurningStove"&&area=="campfire"))return true;
        string tool=Get(r.GetName())?.Tool;
        int type=tool==null?r.craftingToolType:ItemClass.GetItem(tool).type;
        return station=="WorkbenchGasStove001_FR"&&area=="campfire"&&type==ItemClass.GetItem("toolCookingPot").type;
    }
}
