using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Native TileEntity_FuelChanged / TileEntity_InputChanged refresh their grid and then mark the WHOLE station window
/// dirty. A burning station does that every time a fuel item is used, redrawing every recipe row, queue card and tool
/// slot (owner 2026-11-08: flicker across station windows). In REBIRTH station windows only the changed panel is
/// redrawn; other windows keep the native behaviour.
/// </summary>
internal static class RebirthStationScopedRedraw
{
    internal static bool Applies(XUiC_WorkstationWindowGroup window)
        => window is XUiC_RebirthStationWorkspace || window is XUiC_RebirthCookingStation;

    internal static void Refresh(XUiC_WorkstationGrid grid, ItemStack[] stacks)
    {
        if (grid == null) return;
        grid.SetSlots(stacks);
        grid.IsDirty = true;
        // Only the panel that holds the grid (burn time, Turn On/Off, input hints) is redrawn.
        var panel = grid.Parent ?? grid;
        panel.SetAllChildrenDirty();
    }
}

[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup), nameof(XUiC_WorkstationWindowGroup.TileEntity_FuelChanged))]
internal static class RebirthStationScopedFuelRedrawPatch
{
    private static bool Prefix(XUiC_WorkstationWindowGroup __instance)
    {
        if (!RebirthStationScopedRedraw.Applies(__instance) || __instance.WorkstationData == null) return true;
        if (__instance.fuelWindow != null) RebirthStationScopedRedraw.Refresh(__instance.fuelWindow, __instance.WorkstationData.GetFuelStacks());
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup), nameof(XUiC_WorkstationWindowGroup.TileEntity_InputChanged))]
internal static class RebirthStationScopedInputRedrawPatch
{
    private static bool Prefix(XUiC_WorkstationWindowGroup __instance)
    {
        if (!RebirthStationScopedRedraw.Applies(__instance) || __instance.WorkstationData == null) return true;
        if (__instance.inputWindow != null) RebirthStationScopedRedraw.Refresh(__instance.inputWindow, __instance.WorkstationData.GetInputStacks());
        return false;
    }
}

[Preserve]
public sealed class RebirthStationScopedRedrawModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        var harmony = new Harmony("rebirth.station-scoped-redraw");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthStationScopedFuelRedrawPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthStationScopedInputRedrawPatch));
    }
}
