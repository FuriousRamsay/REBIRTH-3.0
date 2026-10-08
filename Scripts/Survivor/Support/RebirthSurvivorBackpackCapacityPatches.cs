using HarmonyLib;

#nullable disable

/// <summary>
/// Installed build 25661859 owns bag storage through ItemStackGrid. In Survivor mode,
/// use the existing gear transaction's capacity and occupied-tail protection instead
/// of vanilla BagSize equipment effects and automatic overflow dropping.
/// </summary>
[HarmonyPatch(typeof(EntityPlayer))]
public static class RebirthSurvivorBackpackCapacityPatches
{
    [HarmonyPatch(nameof(EntityPlayer.UpdateBagpackSize))]
    [HarmonyPrefix]
    public static bool UpdateBagpackSizePrefix(EntityPlayer __instance)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) return true;
        if (__instance == null || __instance.bag == null || __instance.isEntityRemote) return false;
        // Until the authoritative owner projection arrives, preserve the received bag.
        int desired;
        if (!RebirthSurvivorGearService.TryGetDesiredPhysicalBagSlots(__instance, out desired)) return false;
        RebirthTraitGameplayModifierService.SyncNativeCarryCapacityForBag(__instance);
        RebirthSurvivorGearService.ReconcilePhysicalBagCapacity(__instance, desired, false);
        return false;
    }

    [HarmonyPatch(nameof(EntityPlayer.CalcCurrentBackpackSize))]
    [HarmonyPrefix]
    public static bool CalcCurrentBackpackSizePrefix(EntityPlayer __instance, ref int __result)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) return true;
        if (__instance == null || __instance.isEntityRemote) { __result = -1; return false; }
        int desired;
        if (RebirthSurvivorGearService.TryGetDesiredPhysicalBagSlots(__instance, out desired))
            __result = desired;
        else
            __result = __instance.bag != null ? __instance.bag.SlotCount : -1;
        return false;
    }
}