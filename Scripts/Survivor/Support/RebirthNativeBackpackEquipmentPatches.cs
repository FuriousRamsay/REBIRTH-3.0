using HarmonyLib;

#nullable disable

// Preserve existing equipped items on load and allow removal. Only new native
// backpack admission is suppressed; REBIRTH gear has its own custody protocol.
[HarmonyPatch]
public static class RebirthNativeBackpackEquipmentPatches
{
    [HarmonyPatch(typeof(XUiM_PlayerEquipment), nameof(XUiM_PlayerEquipment.EquipItem))]
    [HarmonyPrefix]
    public static bool EquipItemPrefix(ItemStack _stack, ref ItemStack __result)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() || !IsNativeBackpack(_stack)) return true;
        // EquipItem's native refusal contract returns the original offered stack.
        __result = _stack;
        return false;
    }

    [HarmonyPatch(typeof(XUiC_EquipmentStack), "SwapItem")]
    [HarmonyPrefix]
    public static bool SwapItemPrefix(XUiC_EquipmentStack __instance)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() || __instance.EquipSlot != EquipmentSlots.Backpack) return true;
        var drag = __instance.xui?.DragAndDropWindow;
        // Empty cursor is an unequip operation and must remain possible for old saves.
        return drag != null && (drag.CurrentStack == null || drag.CurrentStack.IsEmpty());
    }

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.GetItemDescriptionKey))]
    [HarmonyPostfix]
    public static void DescriptionKeyPostfix(ItemClass __instance, ref string __result)
    {
        if (RebirthSurvivorMode.IsEnabledForCurrentWorld()
            && __instance is ItemClassArmor armor && armor.EquipSlot == EquipmentSlots.Backpack)
            __result = "xuiRebirthNativeBackpackDescription";
    }
    public static bool IsNativeBackpack(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty()) return false;
        var armor = stack.itemValue?.ItemClass as ItemClassArmor;
        return armor != null && armor.EquipSlot == EquipmentSlots.Backpack;
    }
}