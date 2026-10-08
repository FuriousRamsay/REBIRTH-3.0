using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Source-level suppression for native top-right gameplay HUD controllers while the
/// Rebirth Inventory/Crafting or Character surface owns the screen.
///
/// The native location and tracker controllers intentionally turn themselves visible from
/// their own Update paths whenever the HUD is enabled. Hiding them from the Crafting
/// controller after the fact therefore creates a visibility tug-of-war and visible flashing.
/// These narrowly scoped prefixes stop those native Update bodies only while Personal
/// Crafting or Character is actually open, and force the corresponding root invisible before returning.
/// </summary>
[Preserve]
public static class RebirthPersonalCraftingHudSuppressionInstaller
{
    private static bool installed;
    private static readonly Harmony Harmony = new Harmony("rebirth.personal-crafting.hud-suppression");

    public static void EnsureInstalled()
    {
        if (installed)
            return;

        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthPersonalCraftingLocationUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthPersonalCraftingQuestTrackerUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthPersonalCraftingRecipeTrackerUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthPersonalCraftingSetAllChildrenDirtyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthPersonalCraftingRecipeStackPresentationPatch));
        installed = true;
    }

    public static bool ShouldSuppress
    {
        get
        {
            XUiC_RebirthPersonalCrafting active = XUiC_RebirthPersonalCrafting.ActiveInstance;
            return RebirthContextNavigationService.Session != null || (XUiC_RebirthCookingWorkspace.ActiveInstance?.IsCookingOpen == true) || RebirthScreenLayout.ActiveCount > 0 || (XUiC_RebirthItemEditorHeader.ActiveInstance?.IsEditorOpen == true)
                || (active != null && active.State.IsOpen)
                || (XUiC_RebirthSurvivorCharacter.ActiveInstance != null && XUiC_RebirthSurvivorCharacter.ActiveInstance.IsCharacterWindowOpen);
        }
    }

    public static void ForceHidden(XUiController controller)
    {
        if (controller == null || controller.ViewComponent == null)
            return;

        // Do not deactivate the GameObject here. The prefix itself is the source-level gate,
        // so an ordinary visibility write is enough and avoids an active-state fight.
        if (controller.ViewComponent.IsVisible) controller.ViewComponent.IsVisible = false;
        if (controller.ViewComponent.Enabled) controller.ViewComponent.Enabled = false;
    }
}

[HarmonyPatch(typeof(XUiC_Location), nameof(XUiC_Location.Update))]
internal static class RebirthPersonalCraftingLocationUpdatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(XUiC_Location __instance)
    {
        if (!RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress)
            return true;

        RebirthPersonalCraftingHudSuppressionInstaller.ForceHidden(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_QuestTrackerWindow), nameof(XUiC_QuestTrackerWindow.Update))]
internal static class RebirthPersonalCraftingQuestTrackerUpdatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(XUiC_QuestTrackerWindow __instance)
    {
        if (!RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress)
            return true;

        RebirthPersonalCraftingHudSuppressionInstaller.ForceHidden(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_RecipeTrackerWindow), nameof(XUiC_RecipeTrackerWindow.Update))]
internal static class RebirthPersonalCraftingRecipeTrackerUpdatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(XUiC_RecipeTrackerWindow __instance)
    {
        if (!RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress)
            return true;

        RebirthPersonalCraftingHudSuppressionInstaller.ForceHidden(__instance);
        return false;
    }
}

/// <summary>
/// Native RecipeStack cancellation finishes by invalidating the entire window group through
/// XUiController.SetAllChildrenDirty(). In the V3.2 mod reference this member is callable but is
/// not exposed as overridable, so intercept that one broad invalidation at the base method instead.
/// The native refund, recipe removal and queue compaction all run normally; only the redundant
/// whole-window dirty cascade is skipped while the Rebirth queue is inside its scoped mutation.
/// </summary>
[HarmonyPatch(typeof(XUiController), nameof(XUiController.SetAllChildrenDirty))]
internal static class RebirthPersonalCraftingSetAllChildrenDirtyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(XUiController __instance)
    {
        if(__instance is XUiC_RebirthCookingStation cooking)
        {
            cooking.GetChildByType<XUiC_RebirthCookingWorkspace>()?.RefreshStationState();
            return false;
        }
        XUiC_RebirthPersonalCrafting personalCrafting = __instance as XUiC_RebirthPersonalCrafting;
        if (personalCrafting == null || !personalCrafting.IsIncrementalQueueMutationActive)
            return true;

        return false;
    }
}


/// <summary>
/// The native XUiC_RecipeStack.Update path calls its private updateRecipeData() method every
/// visible frame for the active crafting entry. Rebirth queue cards have their own stable visible
/// presentation, so those native writes are both redundant and harmful: they repeatedly toggle the
/// hidden/offscreen native itemIcon/count/timer/cancel widgets and force NGUI to process needless
/// widget state changes while the card is crafting. Skip only that native presentation method for
/// Rebirth queue entries. Craft timing, output, completion, cancellation and refund remain native.
/// </summary>
[HarmonyPatch(typeof(XUiC_RecipeStack), "updateRecipeData")]
internal static class RebirthPersonalCraftingRecipeStackPresentationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(XUiC_RecipeStack __instance)
    {
        return !(__instance is XUiC_RebirthCraftingQueueEntry);
    }
}
