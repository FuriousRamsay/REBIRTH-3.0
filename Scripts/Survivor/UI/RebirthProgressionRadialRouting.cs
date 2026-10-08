using System;
using HarmonyLib;
#nullable disable

// Only the native menu radial callback owns this route; keyboard B keeps its exception.
[HarmonyPatch(typeof(XUiC_Radial), "handleMenuCommand")]
internal static class RebirthProgressionRadialMenuRoutePatch
{
    private static bool Prefix(XUiC_Radial __instance, XUiC_Radial _sender, int _commandIndex)
    {
        if (_commandIndex != 1 && _commandIndex != 3) return true;
        RebirthProgressionWindowRouting.Witness witness;
        var admission = RebirthProgressionWindowRouting.Resolve(_sender, out witness);
        if (admission == RebirthProgressionWindowRouting.Admission.Native) return true;
        if (admission == RebirthProgressionWindowRouting.Admission.Blocked) return false;
        if (!ReferenceEquals(__instance, _sender) || RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return false;
        var manager = _sender.xui?.playerUI?.windowManager;
        if (manager == null || !ReferenceEquals(manager, witness.Manager)) return false;
        bool open = manager.IsWindowOpen(XUiC_RebirthSurvivorCharacter.WindowGroupId);
        if (!RebirthProgressionWindowRouting.StillCurrent(_sender, witness)) return false;
        if (open) manager.Close(XUiC_RebirthSurvivorCharacter.WindowGroupId);
        else RebirthCraftingNavigationService.Navigate(_sender, _commandIndex == 1
            ? RebirthCraftingNavigationService.Destination.Character
            : RebirthCraftingNavigationService.Destination.Skills);
        return false;
    }
}
