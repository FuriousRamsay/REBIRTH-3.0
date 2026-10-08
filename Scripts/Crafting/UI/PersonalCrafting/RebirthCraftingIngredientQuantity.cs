using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Single validated quantity calculation for Rebirth personal-crafting presentation and max-count logic.
/// Keeps the native ingredient-count effect and crafting-input modifier in one place and never
/// permits a wrapped/negative total to reach inventory preflight.
/// </summary>
public static class RebirthCraftingIngredientQuantity
{
    public static bool TryResolvePerBatch(EntityPlayer player, Recipe recipe, ItemStack ingredient, int craftingTier, out int quantity)
    {
        quantity = 0;
        if (recipe == null || ingredient == null || ingredient.IsEmpty() || ingredient.itemValue == null || ingredient.itemValue.IsEmpty())
            return false;
        if (ingredient.count <= 0) return false;

        double value = ingredient.count;
        try
        {
            if (recipe.UseIngredientModifier && ingredient.itemValue.ItemClass != null)
            {
                string itemName = ingredient.itemValue.ItemClass.GetItemName();
                float effected = EffectManager.GetValue(
                    PassiveEffects.CraftingIngredientCount,
                    _originalValue: ingredient.count,
                    _entity: player,
                    _recipe: recipe,
                    tags: FastTags<TagGroup.Global>.Parse(itemName),
                    craftingTier: Math.Max(1, craftingTier));
                if (float.IsNaN(effected) || float.IsInfinity(effected) || effected < 0f) return false;
                value = effected;

                if (value > 0d)
                {
                    float modifier = XUiM_Recipes.GetCraftingInputModifier(recipe);
                    if (float.IsNaN(modifier) || float.IsInfinity(modifier) || modifier < 0f) return false;
                    value *= modifier;
                    if (XUiM_Recipes.CraftingInputModifier > 0f) value = Math.Max(1d, value);
                }
            }
        }
        catch
        {
            return false;
        }

        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > int.MaxValue) return false;
        quantity = (int)value;
        return true;
    }

    public static bool TryResolveTotal(EntityPlayer player, Recipe recipe, ItemStack ingredient, int craftingTier, int batches, out int total)
    {
        total = 0;
        if (batches <= 0 || !TryResolvePerBatch(player, recipe, ingredient, craftingTier, out int perBatch)) return false;
        long wide = (long)perBatch * batches;
        if (wide < 0L || wide > int.MaxValue) return false;
        total = (int)wide;
        return true;
    }

    public static bool SameRecipeIdentity(ItemValue a, ItemValue b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null || a.IsEmpty() || b.IsEmpty() || a.type != b.type) return false;
        try { return a.EqualsForMerging(b); }
        catch { return a.type == b.type && a.Meta == b.Meta && a.Quality == b.Quality && Math.Abs(a.UseTimes - b.UseTimes) < 0.0001f; }
    }
}
