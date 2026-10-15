using HarmonyLib;

// Quest packages carry per-quest identity. Native CanStackWith compares type
// and capacity, so merging them discards one package's identity.
[HarmonyPatch(typeof(ItemStack), nameof(ItemStack.CanStackWith), new[] { typeof(ItemStack), typeof(bool) })]
public static class RebirthFetchStackGuard
{
    [HarmonyPostfix]
    public static void Postfix(ItemStack __instance, ItemStack __0, ref bool __result)
    {
        if (!__result) return;
        if (__instance?.itemValue?.ItemClass is ItemClassQuest ||
            __0?.itemValue?.ItemClass is ItemClassQuest)
            __result = false;
    }
}

// Native Bag/Inventory/XUiM_PlayerInventory.TryStackItem bypass CanStackWith:
// they compare only type, then ask the destination for partial capacity.
// Keep the incoming count untouched when refusing; the caller retains custody.
[HarmonyPatch(typeof(ItemStack), nameof(ItemStack.CanStackPartly))]
public static class RebirthFetchPartialStackGuard
{
    [HarmonyPrefix]
    public static bool Prefix(ItemStack __instance, ref bool __result)
    {
        if (__instance?.itemValue?.ItemClass is not ItemClassQuest) return true;
        __result = false;
        return false;
    }
}

// The count-only full-capacity method also omits ItemClass.CanStack. Empty
// destinations still use native acceptance; this prevents adding to a package.
[HarmonyPatch(typeof(ItemStack), nameof(ItemStack.CanStack), new[] { typeof(int) })]
public static class RebirthFetchFullStackGuard
{
    [HarmonyPrefix]
    public static bool Prefix(ItemStack __instance, ref bool __result)
    {
        if (__instance?.itemValue?.ItemClass is not ItemClassQuest) return true;
        __result = false;
        return false;
    }
}

// Native StackTransferCount also computes a type-only merge amount directly.
[HarmonyPatch(typeof(ItemStack), nameof(ItemStack.StackTransferCount))]
public static class RebirthFetchTransferCountGuard
{
    [HarmonyPrefix]
    public static bool Prefix(ItemStack __instance, ItemStack __0, ref int __result)
    {
        if (!(__instance?.itemValue?.ItemClass is ItemClassQuest) &&
            !(__0?.itemValue?.ItemClass is ItemClassQuest)) return true;
        __result = 0;
        return false;
    }
}
