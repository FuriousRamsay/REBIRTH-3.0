using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Owner rule (2026-11-07): holding E always brings up the radial menu. Natively a radial with a single enabled command
/// runs it the instant E is pressed (so a light that only offered "pick up" was picked up before a hold could open the
/// menu). Now a single command behaves like several: a quick tap runs it, holding opens the wheel, and releasing the
/// held wheel without choosing does nothing. Only while the radial button is still held; other callers are unchanged.
/// </summary>
internal static class RebirthRadialHoldAlwaysOpens
{
    [System.ThreadStatic] internal static bool SuppressInstant;
    [System.ThreadStatic] internal static bool Suppressed;
}

[HarmonyPatch(typeof(XUiC_Radial), nameof(XUiC_Radial.SetCommonData))]
internal static class RebirthRadialSingleEntryPatch
{
    private static void Prefix(XUiC_Radial __instance, bool _hasSpecialActionPriorToRadialVisibility)
    {
        RebirthRadialHoldAlwaysOpens.Suppressed = false;
        var input = __instance.xui?.playerUI?.playerInput;
        RebirthRadialHoldAlwaysOpens.SuppressInstant = input != null && !_hasSpecialActionPriorToRadialVisibility
            && __instance.currentEnabledEntriesCount() == 1 && __instance.radialButtonPressed(input);
    }

    private static void Postfix(XUiC_Radial __instance)
    {
        bool suppressed = RebirthRadialHoldAlwaysOpens.Suppressed;
        RebirthRadialHoldAlwaysOpens.SuppressInstant = false;
        RebirthRadialHoldAlwaysOpens.Suppressed = false;
        if (!suppressed) return;
        // The native single-entry path returned before laying out the wheel: show the one entry and leave nothing
        // pre-selected, so Update runs it on a quick release and a held wheel closes unless it is chosen.
        __instance.selectedIndex = int.MinValue;
        for (int j = 0; j < __instance.menuItem.Length; j++)
        {
            __instance.menuItem[j].ViewComponent.IsVisible = j < 1;
            __instance.menuItem[j].ViewComponent.Enabled = j < 1;
        }
    }
}

[HarmonyPatch(typeof(XUiC_Radial), nameof(XUiC_Radial.CallContextAction))]
internal static class RebirthRadialSingleEntryCallPatch
{
    private static bool Prefix()
    {
        if (!RebirthRadialHoldAlwaysOpens.SuppressInstant) return true;
        RebirthRadialHoldAlwaysOpens.SuppressInstant = false;
        RebirthRadialHoldAlwaysOpens.Suppressed = true;
        return false;
    }
}

[Preserve]
public sealed class RebirthRadialHoldAlwaysOpensModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        var harmony = new Harmony("rebirth.radial-hold");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthRadialSingleEntryPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthRadialSingleEntryCallPatch));
    }
}
