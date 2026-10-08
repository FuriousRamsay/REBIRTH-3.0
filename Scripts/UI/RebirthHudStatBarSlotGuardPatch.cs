using HarmonyLib;

#nullable disable

/// <summary>The installed game uses real ItemGrid slots and a separate Hand transient state.
/// Preserve valid focused slots; never substitute a removed dummy-slot index during rebinding.</summary>
[HarmonyPatch(typeof(XUiC_HUDStatBar), "setupActiveItemEntry")]
public static class RebirthHudStatBarSlotGuardPatch
{
    [HarmonyPrefix]
    public static bool Prefix(XUiC_HUDStatBar __instance)
    {
        Inventory inventory = __instance.localPlayer?.inventory;
        if (inventory == null) return true; // Native already handles a missing local player.
        int index = __instance.currentSlotIndex;
        if (index >= 0 && index < inventory.Length) return true;
        int selected = inventory.SelectedSlot;
        if (selected >= 0 && selected < inventory.Length)
        {
            __instance.currentSlotIndex = selected;
            return true;
        }
        __instance.itemClass = null;
        __instance.attackAction = null;
        __instance.activeAmmoItemValue = ItemValue.None;
        __instance.currentAmmoCount = 0;
        return false;
    }
}