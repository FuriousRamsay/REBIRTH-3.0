using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Multi-ingredient recipes: a staged SET of N one-unit staging assignments bound to one queue operation.
// Source-complete: key grammar, exact-cover validation, N-unit queue original (units, receipts, debit). The in-game batch
// still has to exercise it (see the P7 doc invariants review).
internal static class RebirthNpcRecipeIngredientSet
{
    internal const bool Qualified=true;
    internal const int MaxUnits=4;
    // recipe-queue:<stagedId>[+<stagedId>...] ; the single-id form is the existing key.
    internal static bool TryParseKey(string key,out ulong[] ids)
    {
        ids=null;
        if(key==null||!key.StartsWith("recipe-queue:",StringComparison.Ordinal)||key.Length>=128)return false;
        var parts=key.Substring(13).Split('+');
        if(parts.Length<1||(parts.Length>1&&!Qualified)||parts.Length>MaxUnits)return false;
        var list=new ulong[parts.Length];
        for(int i=0;i<parts.Length;i++)
            if(!ulong.TryParse(parts[i],NumberStyles.None,CultureInfo.InvariantCulture,out list[i])||list[i]==0)return false;
        if(list.Distinct().Count()!=list.Length)return false;
        ids=list;return true;
    }
    internal static bool ShapeSupported(Recipe recipe)
    {
        if(recipe?.ingredients==null||recipe.ingredients.Count<1||Wildcard(recipe))return false;
        int units=0;
        foreach(var ingredient in recipe.ingredients)
        {
            if(ingredient?.itemValue==null||ingredient.count<1||ingredient.count>MaxUnits)return false;
            units+=ingredient.count;if(units>MaxUnits)return false;
        }
        return Qualified?true:recipe.ingredients.Count==1&&units==1;
    }
    // Category recipes (any forge/campfire material, material-based) accept interchangeable items, so a staged unit cannot be matched
    // to one ingredient by item type; they stay refused.
    internal static bool Wildcard(Recipe recipe)=>recipe!=null&&(recipe.wildcardForgeCategory||recipe.wildcardCampfireCategory||recipe.materialBasedRecipe);
    // Exact cover: the staged one-unit frames must equal the recipe's ingredient multiset (by item type), no more, no less.
    // Returns null when exact, otherwise the refusal reason.
    internal static string ValidateStagedSet(Recipe recipe,IList<string> stagedFrames)
    {
        if(!ShapeSupported(recipe))return "The recipe ingredient shape is not supported.";
        if(stagedFrames==null||stagedFrames.Count<1||stagedFrames.Count>MaxUnits)return "The staged ingredient set is empty or too large.";
        var need=new Dictionary<int,int>();
        foreach(var ingredient in recipe.ingredients){need.TryGetValue(ingredient.itemValue.type,out int n);need[ingredient.itemValue.type]=n+ingredient.count;}
        foreach(string frame in stagedFrames)
        {
            var stack=RebirthHumanEquipment.Decode(frame);
            if(stack==null||stack.IsEmpty()||stack.count!=1||stack.itemValue==null||
                !string.Equals(RebirthHumanEquipment.Frame(stack),frame,StringComparison.Ordinal))return "A staged ingredient is not an exact single unit.";
            if(!need.TryGetValue(stack.itemValue.type,out int left)||left<1)return "A staged ingredient does not belong to the recipe or is staged twice.";
            var match=recipe.ingredients.First(x=>x.itemValue.type==stack.itemValue.type);
            if((stack.itemValue.HasModSlots&&stack.itemValue.HasMods())||!RebirthCookingItemStats.FullIngredient(stack.itemValue)||
                !RebirthCookingItemStats.Compatible(stack.itemValue,match.itemValue))return "A staged ingredient is modified, partial or not compatible with the recipe.";
            need[stack.itemValue.type]=left-1;
        }
        return need.Values.Any(x=>x!=0)?"The staged set does not cover every recipe ingredient.":null;
    }
}
