using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// A station shows only the tool slots it uses (owner request 2026-11-07): the Circular Saw needs one blade, so its two
/// spare slots are hidden. A slot is hidden when it is empty, accepts only required items, and has no tool assigned
/// (from required_tools or, in the station workspace, from the station's recipes). A slot holding an item always shows.
/// If a grid has no tool assigned at all, nothing is hidden.
/// </summary>
internal static class RebirthStationToolSlotVisibility
{
    internal static void Apply(XUiC_WorkstationGrid grid)
    {
        if (!(grid is XUiC_WorkstationToolGrid tools) || tools.itemControllers == null) return;
        bool anyAssigned = false;
        foreach (var controller in tools.itemControllers)
            if (controller is XUiC_RequiredItemStack r && r.allowedItemClasses.Count > 0) { anyAssigned = true; break; }
        if (!anyAssigned) return;
        ItemStack[] slots = tools.GetSlots();
        for (int i = 0; i < tools.itemControllers.Length; i++)
        {
            var view = tools.itemControllers[i]?.ViewComponent;
            if (view == null) continue;
            bool occupied = slots != null && i < slots.Length && slots[i] != null && !slots[i].IsEmpty();
            var required = tools.itemControllers[i] as XUiC_RequiredItemStack;
            bool unused = required != null && required.RequiredItemOnly && required.allowedItemClasses.Count == 0;
            // Hidden through the per-frame cell guard, so a binding or native refresh cannot show it again between
            // grid updates (owner 2026-11-08: the Circular Saw spare slots flickered in and out).
            if (required != null) RebirthUnusedToolCells.Mark(required, unused && !occupied);
        }
    }
}

[HarmonyPatch(typeof(XUiC_WorkstationGrid), nameof(XUiC_WorkstationGrid.SetSlots))]
internal static class RebirthStationToolSlotVisibilitySetSlotsPatch
{
    private static void Postfix(XUiC_WorkstationGrid __instance) => RebirthStationToolSlotVisibility.Apply(__instance);
}

[HarmonyPatch(typeof(XUiC_WorkstationGrid), nameof(XUiC_WorkstationGrid.OnOpen))]
internal static class RebirthStationToolSlotVisibilityOpenPatch
{
    private static void Postfix(XUiC_WorkstationGrid __instance) => RebirthStationToolSlotVisibility.Apply(__instance);
}

[Preserve]
public sealed class RebirthStationToolSlotVisibilityModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        var harmony = new Harmony("rebirth.station-tool-slots");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthStationToolSlotVisibilitySetSlotsPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthStationToolSlotVisibilityOpenPatch));
    }
}
