using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// A second native workstation view restores non-food campfire recipes. It uses
// the same server lock, tile, fuel, tools, queue and output as ordinary crafting.
public static class RebirthCampfireProcessing
{
    public const string Command = "rebirthCampfireProcessing";
    public const string Window = "rebirthCampfireProcessingWindow";
    private static TileEntityWorkstation pending;
    private static float expires;
    public static bool Active;
    public static bool Eligible(Block block) => RebirthSurvivorMode.IsEnabledForCurrentWorld()
        && (block.GetBlockName() == "campfire" || block.GetBlockName() == "cntWoodBurningStove"
            || block.GetBlockName() == "WorkbenchGasStove001_FR");
    public static bool IsFood(Recipe recipe) => recipe != null &&
        (RebirthCookingCatalogue.IsCooking(recipe) || RebirthCraftOutcomeService.IsWellPreparedRecipe(recipe.GetName())
            || recipe.GetName() == "foodCharredMeat");
    public static bool Request(WorldBase world, Vector3i pos, EntityPlayerLocal player)
    {
        var tile = world.GetTileEntity(pos) as TileEntityWorkstation;
        if (tile == null) return false;
        if (tile.Queue != null && tile.Queue.Any(q => q != null && RebirthCookingHeat.Managed(q.Recipe)))
        {
            GameManager.ShowTooltip(player, Localization.Get("rebirthProcessingFinishFood"));
            return false;
        }
        pending = tile; expires = Time.realtimeSinceStartup + 10;
        player.AimingGun = false;
        // Reuse the native activation entry point (also avoids binding the game's
        // newer ReadOnlySpan overload from this net481 mod assembly).
        world.GetBlock(pos).Block.OnBlockActivated(world, pos, world.GetBlock(pos), player);
        return true;
    }
    public static bool Open(TileEntityWorkstation tile, bool success)
    {
        if (pending != tile) return true;
        pending = null;
        if (!success || Time.realtimeSinceStartup > expires) return true;
        var ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        if (!ui.windowManager.TryGetWindow(Window, out var window)
            || !(window is XUiWindowGroup group)
            || !(group.Controller is XUiC_WorkstationWindowGroup controller)) return true;
        controller.SetTileEntity(tile);
        ui.windowManager.Open(Window, true);
        return false;
    }
}

[Preserve]
public sealed class XUiC_RebirthCampfireProcessing : XUiC_WorkstationWindowGroup
{
    public override void OnOpen() { RebirthCampfireProcessing.Active = true; base.OnOpen(); categoryList?.TrySetCategory("CFChemicals"); }
    public override void OnClose() { try { base.OnClose(); } finally { RebirthCampfireProcessing.Active = false; } }
    public override bool CraftingRequirementsValid(Recipe recipe) =>
        !RebirthCampfireProcessing.IsFood(recipe) && base.CraftingRequirementsValid(recipe);
}

[HarmonyPatch(typeof(BlockWorkstation), nameof(BlockWorkstation.GetBlockActivationCommands))]
public static class RebirthCampfireProcessingCommands
{
    public static void Postfix(BlockWorkstation __instance, ref BlockActivationCommand[] __result)
    {
        if (!RebirthCampfireProcessing.Eligible(__instance)) return;
        var commands = new List<BlockActivationCommand>(__result ?? BlockActivationCommand.Empty);
        commands.Add(new BlockActivationCommand(RebirthCampfireProcessing.Command, "chemistry", true));
        __result = commands.ToArray();
    }
}
[HarmonyPatch(typeof(BlockWorkstation), nameof(BlockWorkstation.OnBlockActivated),
    new[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
public static class RebirthCampfireProcessingActivate
{
    public static bool Prefix(string __0, WorldBase __1, Vector3i __2, BlockValue __3, EntityPlayerLocal __4, ref bool __result)
    {
        if (__0 != RebirthCampfireProcessing.Command || !RebirthCampfireProcessing.Eligible(__3.Block)) return true;
        __result = RebirthCampfireProcessing.Request(__1, __2, __4);
        return false;
    }
}
[HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.OnLockResponseLocal))]
public static class RebirthCampfireProcessingLock
{
    public static bool Prefix(TileEntityWorkstation __instance, bool __0, ref bool __result)
    {
        bool runNative = RebirthCampfireProcessing.Open(__instance, __0);
        if (!runNative) __result = true; // custom window accepted the granted native lock
        return runNative;
    }
}
// Filter the owning recipe-list instance, never global ingredient queries used by item actions.
[HarmonyPatch(typeof(XUiC_RecipeList), nameof(XUiC_RecipeList.BuildRecipeInfosList))]
public static class RebirthCampfireProcessingRecipeFilter
{
    public static void Prefix(XUiC_RecipeList __instance)
    {
        if (__instance == null ||
            !(__instance.WindowGroup?.Controller is XUiC_RebirthCampfireProcessing) ||
            __instance.recipes == null)
            return;
        __instance.recipes = __instance.recipes.Where(r => r != null &&
            r.craftingArea == "campfire" && !RebirthCampfireProcessing.IsFood(r)).ToList();
        if (__instance.CurrentRecipe != null && !__instance.recipes.Contains(__instance.CurrentRecipe))
            __instance.CurrentRecipe = null;
    }
}
