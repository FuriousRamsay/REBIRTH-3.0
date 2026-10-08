using System;

#nullable disable

/// <summary>Resolve only ready, explicitly owned REBIRTH recipe unlock policy. Native compatibility recipes stay native.</summary>
public static class RebirthOwnedRecipeUnlockResolution
{
    public static bool TryResolve(Recipe recipe, EntityPlayer player, out bool unlocked)
    {
        unlocked = false;
        if (recipe == null || player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()
            || !RebirthCraftingProgressionRegistry.IsReady || !RebirthCapabilityRegistry.IsReady) return false;
        string name = recipe.GetName();
        if (string.IsNullOrEmpty(name)) return false;
        RebirthCraftingProgressionDefinition policy;
        if (!RebirthCraftingProgressionRegistry.TryGetRecipe(name, out policy) || policy == null
            || !string.Equals(policy.RecipeId, name, StringComparison.OrdinalIgnoreCase)) return false;
        if (policy.IsDisabled) return true;
        if (policy.IsUniversal) { unlocked = true; return true; }
        if (!policy.IsGated || !policy.HasLiveCapability) return false;
        RebirthCapabilityDefinition capability;
        if (!RebirthCapabilityRegistry.TryGet(policy.CapabilityId, out capability) || capability == null
            || !string.Equals(capability.TargetType, "recipe", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(capability.TargetId, name, StringComparison.OrdinalIgnoreCase)) return false;
        RebirthCapabilityEvaluation evaluation = RebirthStationDiscoveryScope.Matches(recipe, player)
            ? RebirthCapabilityService.EvaluateRecipeForDiscovery(player, name)
            : RebirthCapabilityService.EvaluateRecipe(player, name);
        if (evaluation == null) return false;
        unlocked = evaluation.IsAllowed;
        return true;
    }
}
