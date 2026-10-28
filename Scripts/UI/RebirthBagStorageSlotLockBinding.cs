using HarmonyLib;

// Native OnClose clears Bag while its standard-controls callback still exists.
// Only availability of that absent storage is handled here; live locks stay native.
[HarmonyPatch(typeof(XUiC_BagContainer), nameof(XUiC_BagContainer.GetBindingValueInternal))]
internal static class RebirthBagStorageSlotLockBinding
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    internal static bool Prefix(XUiC_BagContainer __instance, ref string __0, string __1, ref bool __result)
    {
        if (__1 != "hasslotlocks" || __instance == null) return true;
        var bag = __instance.Bag;
        if (bag != null && bag.ItemGrid != null) return true;
        __0 = bool.FalseString;
        __result = true;
        return false;
    }
}
