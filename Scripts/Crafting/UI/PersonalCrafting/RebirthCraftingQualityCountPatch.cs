using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Runtime.CompilerServices;
using UnityEngine;

// Native RecipeCraftCount calculates against the maximum tier. Keep its behavior, but use
// the Rebirth selector's tier and the same validated quantity plan used by requirements.
[HarmonyPatch(typeof(XUiC_RecipeCraftCount), "calcMaxCraftable")]
public static class RebirthCraftingQualityCountPatch
{
    private sealed class Demand
    {
        public ItemValue Item;
        public bool Quality;
        public long PerBatch;
    }

    private sealed class DetailReference
    {
        internal XUiC_RebirthCraftingRecipeDetails Details;
    }
    private static readonly ConditionalWeakTable<XUiC_RebirthPersonalCrafting, DetailReference> DetailCache = new ConditionalWeakTable<XUiC_RebirthPersonalCrafting, DetailReference>();
    private static DetailReference FindDetails(XUiC_RebirthPersonalCrafting owner) => new DetailReference { Details = owner.GetChildByType<XUiC_RebirthCraftingRecipeDetails>() };
    public static bool Prefix(XUiC_RecipeCraftCount __instance, ref int __result)
    {
        Recipe recipe = __instance.recipe;
        if (recipe == null) return true;
        var owner = __instance.GetParentByType<XUiC_RebirthPersonalCrafting>();
        var details = owner != null ? DetailCache.GetValue(owner, FindDetails).Details : null;
        
        if (details == null || recipe == null || details.SelectedRecipe != recipe) return true;

        EntityPlayer player = __instance.xui.playerUI.entityPlayer;
        var stacks = __instance.xui.PlayerInventory.GetAllItemStacks();
        var demands = new List<Demand>();
        foreach (var ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.IsEmpty() || ingredient.itemValue == null || ingredient.itemValue.IsEmpty()) continue;
            if (!RebirthCraftingIngredientQuantity.TryResolvePerBatch(player, recipe, ingredient, details.SelectedCraftingTier, out int perBatch))
            {
                __result = 0;
                return false;
            }
            bool quality = ingredient.itemValue.HasQuality;
            if (quality && perBatch == 0) perBatch = 1;
            if (perBatch == 0) continue; // explicit free-crafting modifier

            Demand demand = null;
            for (int i = 0; i < demands.Count; i++)
                if (demands[i].Quality == quality && RebirthCraftingIngredientQuantity.SameRecipeIdentity(demands[i].Item, ingredient.itemValue)) { demand = demands[i]; break; }
            if (demand == null)
            {
                demand = new Demand { Item = ingredient.itemValue, Quality = quality };
                demands.Add(demand);
            }
            demand.PerBatch += perBatch;
            if (demand.PerBatch > int.MaxValue) { __result = 0; return false; }
        }

        if (XUiM_Recipes.CraftingInputModifier == 0 || demands.Count == 0) { __result = 10000; return false; }

        int maximum = 10000;
        for (int i = 0; i < demands.Count; i++)
        {
            Demand demand = demands[i];
            long available = 0;
            for (int s = 0; s < stacks.Count; s++)
            {
                ItemStack stack = stacks[s];
                if (stack == null || stack.IsEmpty() || stack.itemValue == null || !RebirthCraftingIngredientQuantity.SameRecipeIdentity(stack.itemValue, demand.Item)) continue;
                if (demand.Quality)
                {
                    if (stack.itemValue.HasModSlots && stack.itemValue.HasMods()) continue;
                    available++;
                }
                else available += stack.count;
            }
            int count = demand.PerBatch <= 0 ? 10000 : (int)Math.Min(10000L, available / demand.PerBatch);
            maximum = Math.Min(maximum, count);
        }
        __result = Math.Max(0, maximum);
        return false;
    }
}
