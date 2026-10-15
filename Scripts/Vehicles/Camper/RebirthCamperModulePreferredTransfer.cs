using HarmonyLib;

// This native method is not virtual. Module slots are presentation clones,
// so its direct AddItemToBackpack path would duplicate an installed module.
[HarmonyPatch(typeof(XUiC_BasePartStack), nameof(XUiC_BasePartStack.HandleMoveToPreferredLocation))]
public static class RebirthCamperModulePreferredTransfer
{
    private static bool Prefix(XUiC_BasePartStack __instance)
    {
        var module = __instance as XUiC_RebirthCamperModuleSlot;
        if (module == null) return true;
        module.TransferPreferred();
        return false;
    }
}
