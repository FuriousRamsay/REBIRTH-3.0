using HarmonyLib;

// One availability rule for physical admission and the station tool requirement UI.
internal static class RebirthStationToolAvailability
{
    internal static bool HasRequirement(ItemStack[] tools, Recipe recipe)
    {
        if (recipe == null) return false;
        if (recipe.craftingToolType == 0) return true;
        if (tools == null) return false;
        foreach (var stack in tools)
        {
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != recipe.craftingToolType) continue;
            var item = stack.itemValue;
            float maximum = item.MaxUseTimes;
            if (maximum <= 0f || item.UseTimes < maximum) return true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_WorkstationToolGrid), nameof(XUiC_WorkstationToolGrid.HasRequirement))]
internal static class RebirthStationToolAvailabilityPatch
{
    private static void Postfix(XUiC_WorkstationToolGrid __instance, Recipe __0, ref bool __result)
    {
        // Restrict this to the non-cooking REBIRTH station workspace.
        if (__result && __instance.WindowGroup?.Controller is XUiC_RebirthStationWorkspace)
            __result = RebirthStationToolAvailability.HasRequirement(__instance.GetSlots(), __0);
    }
}
[HarmonyPatch(typeof(XUiC_RecipeStack), nameof(XUiC_RecipeStack.Update))]
internal static class RebirthStationOpenQueueToolHoldPatch
{
    private static bool Prefix(XUiC_RecipeStack __instance, Recipe ___recipe)
    {
        if (!__instance.IsCrafting || ___recipe == null || ___recipe.IsScrap ||
            !(__instance.WindowGroup?.Controller is XUiC_RebirthStationWorkspace station)) return true;
        var tools = station.GetChildByType<XUiC_WorkstationToolGrid>();
        return RebirthStationToolAvailability.HasRequirement(tools?.GetSlots(), ___recipe);
    }
}
