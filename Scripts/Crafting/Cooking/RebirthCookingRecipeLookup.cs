using System;
using System.Collections.Generic;
using HarmonyLib;

// Native recipe identity only; knowledge and capability checks remain live per player.
public static class RebirthCookingRecipeLookup
{
    private static object source;
    private static int count=-1;
    private static readonly Dictionary<string,Recipe> recipes=new Dictionary<string,Recipe>(StringComparer.Ordinal);
    public static Recipe Find(string name)
    {
        if(string.IsNullOrEmpty(name))return null;
        var current=XUiM_Recipes.GetRecipes();
        if(current==null)return null;
        if(!ReferenceEquals(source,current)||count!=current.Count)
        {
            recipes.Clear();
            foreach(var recipe in current)
            {
                if(recipe==null)continue;
                string id=recipe.GetName();
                if(id!=null&&!recipes.ContainsKey(id))recipes.Add(id,recipe);
            }
            source=current;count=current.Count;
        }
        recipes.TryGetValue(name,out var result);return result;
    }
    private static object ingredientSource;
    private static int ingredientCount=-1;
    private static readonly HashSet<int> cookingIngredients=new HashSet<int>();
    public static bool IsCookingIngredient(int itemType)
    {
        var current=XUiM_Recipes.GetRecipes();
        if(current==null)return false;
        if(!ReferenceEquals(ingredientSource,current)||ingredientCount!=current.Count)
        {
            cookingIngredients.Clear();
            foreach(var recipe in current)
                if(RebirthCookingCatalogue.IsCooking(recipe)&&recipe.ingredients!=null)
                    foreach(var ingredient in recipe.ingredients)
                        if(ingredient?.itemValue!=null)cookingIngredients.Add(ingredient.itemValue.type);
            ingredientSource=current;ingredientCount=current.Count;
        }
        return cookingIngredients.Contains(itemType);
    }
    public static void Reset()
    {
        source=null;count=-1;recipes.Clear();
        ingredientSource=null;ingredientCount=-1;cookingIngredients.Clear();
    }
}
[HarmonyPatch(typeof(CraftingManager),nameof(CraftingManager.PostInit))]
internal static class RebirthCookingRecipeLookupPostInit
{
    private static void Postfix()=>RebirthCookingRecipeLookup.Reset();
}
[HarmonyPatch(typeof(CraftingManager),nameof(CraftingManager.ClearAllRecipes))]
internal static class RebirthCookingRecipeLookupClear
{
    private static void Prefix()=>RebirthCookingRecipeLookup.Reset();
}