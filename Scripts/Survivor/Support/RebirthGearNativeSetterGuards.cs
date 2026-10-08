using System;
using HarmonyLib;

[HarmonyPatch(typeof(Bag), nameof(Bag.SetSlot), new Type[] { typeof(int), typeof(ItemStack) })]
internal static class RebirthGearBagSlotGuard
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance, int index, ItemStack _stack)
        => !RebirthGearOwnerReservation.BlocksInventory(__instance, true) ||
            RebirthGearNativeWritePermit.TryConsume(RebirthGearNativeWritePermit.Kind.BagSlot,
                __instance, index, _stack, _stack?.count ?? -1);
}
[HarmonyPatch(typeof(Bag), nameof(Bag.SetSlots), new Type[] { typeof(ItemStack[]), typeof(bool) })]
internal static class RebirthGearBagBackingGuard
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance, ItemStack[] _slots, bool _resize)
        => !RebirthGearOwnerReservation.BlocksInventory(__instance, true) ||
            (_resize && RebirthGearNativeWritePermit.TryConsume(RebirthGearNativeWritePermit.Kind.BagBacking,
                __instance, -1, _slots, _slots?.Length ?? -1));
}
[HarmonyPatch(typeof(Inventory), nameof(Inventory.SetItem), new Type[] { typeof(int), typeof(ItemValue), typeof(int) })]
internal static class RebirthGearBeltSlotGuard
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool Prefix(Inventory __instance, int _idx, ItemValue _itemValue, int _count)
        => !RebirthGearOwnerReservation.BlocksInventory(__instance, false) ||
            RebirthGearNativeWritePermit.TryConsume(RebirthGearNativeWritePermit.Kind.BeltSlot,
                __instance, _idx, _itemValue, _count);
}
[HarmonyPatch(typeof(Inventory), nameof(Inventory.SetSlots), new Type[] { typeof(ItemStack[]) })]
internal static class RebirthGearBeltBackingGuard
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool Prefix(Inventory __instance)
        => !RebirthGearOwnerReservation.BlocksInventory(__instance, false);
}