using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 XUiC_WorkstationFuelGrid_Update: while a station window is open the window runs the queue, so the tile's own
/// auto-off (RebirthWorkstationFuelPreservation) never sees the work end. When the open window goes from having work
/// (a crafting card, or items in the input) to having none, the station turns itself off (owner 2026-11-08: Circular
/// Saw kept burning after finishing). A station switched on with no work stays on, as in 2.6.
/// </summary>
[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup), nameof(XUiC_WorkstationWindowGroup.Update))]
internal static class RebirthWorkstationWindowAutoOffPatch
{
    private static bool HasWork(XUiC_WorkstationWindowGroup window)
    {
        var cards = window.craftingQueue?.GetRecipesToCraft();
        if (cards != null)
            foreach (var card in cards)
                if (card != null && card.IsCrafting && card.GetRecipe() != null) return true;
        var input = window.inputWindow?.GetSlots();
        if (input != null)
            foreach (var stack in input)
                if (stack != null && !stack.IsEmpty()) return true;
        return false;
    }

    private static void Prefix(XUiC_WorkstationWindowGroup __instance, out bool __state)
        => __state = __instance?.fuelWindow != null && __instance.WorkstationData?.GetIsBurning() == true && HasWork(__instance);

    private static void Postfix(XUiC_WorkstationWindowGroup __instance, bool __state)
    {
        if (__state && __instance.fuelWindow != null && __instance.WorkstationData?.GetIsBurning() == true && !HasWork(__instance))
            __instance.fuelWindow.TurnOff();
    }
}

[Preserve]
public sealed class RebirthWorkstationWindowAutoOffModApi : IModApi
{
    public void InitMod(Mod mod)
        => RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.workstation-window-auto-off"), typeof(RebirthWorkstationWindowAutoOffPatch));
}
