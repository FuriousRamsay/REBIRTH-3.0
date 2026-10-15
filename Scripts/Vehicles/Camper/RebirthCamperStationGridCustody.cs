using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

// Native save/checkpoint operations own an exact grid image for a short phase.
// Refuse gestures before the native cursor/backpack can change, then permit
// ordinary workstation editing as soon as that original custody is released.
internal static class RebirthCamperStationGridCustody
{
    internal static bool IsHeld(RebirthCamperStationState station)
    {
        if(RebirthCamperStationNativeActorPayment.IsHeld(station))return true;
#if REBIRTH_CAMPER_DIAGNOSTICS
        return false;
#else
        return station != null && (RebirthCamperStationSaveEvidence.IsPending(station) ||
            RebirthCamperStationCompletionCallsite.IsHeld(station) ||
            RebirthCamperStationCancellationRemoval.IsHeld(station));
#endif
    }
    internal static bool BeforeGesture(XUiController controller)
    {
        var window = controller?.GetParentByType<XUiC_WorkstationWindowGroup>();
        var station = window?.WorkstationData?.TileEntity as RebirthCamperStationState;
        if (!IsHeld(station)) return true;
        var player = controller.xui?.playerUI?.entityPlayer;
        if (player != null) GameManager.ShowTooltip(player, Localization.Get("xuiRebirthCamperStationSaving"));
        return false;
    }
}

[HarmonyPatch]
internal static class RebirthCamperStationGridGesture
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "HandleStackSwap", "SwapItem", "HandlePartialStackPickup", "HandleDropOne", "HandleMoveToPreferredLocation" })
        {
            var method = AccessTools.Method(typeof(XUiC_ItemStack), name);
            if (method == null) throw new InvalidOperationException("Native station gesture missing: " + name);
            yield return method;
        }
        // This native subclass has its own drop-one body.
        var drop=AccessTools.DeclaredMethod(typeof(XUiC_RequiredItemStack), "HandleDropOne");
        if(drop==null)throw new InvalidOperationException("Native required-slot drop gesture missing.");
        yield return drop;
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_ItemStack __instance) => RebirthCamperStationGridCustody.BeforeGesture(__instance);
}

[HarmonyPatch]
internal static class RebirthCamperStationGridDirectInsert
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in new[] { typeof(XUiC_WorkstationGrid), typeof(XUiC_WorkstationToolGrid), typeof(XUiC_WorkstationFuelGrid), typeof(XUiC_RequiredItemStack) })
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (method.DeclaringType == type && (method.Name == "AddItem" || method.Name == "TryStackItem" || method.Name == "TryAddTool" || method.Name == "TryStack"))
                {
                    if(method.ReturnType!=typeof(bool))throw new InvalidOperationException("Native grid insert signature changed: "+type.Name+"."+method.Name);
                    yield return method;
                }
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiController __instance, ref bool __result)
    {
        if (RebirthCamperStationGridCustody.BeforeGesture(__instance)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(XUiM_Workstation), nameof(XUiM_Workstation.SetIsBurning))]
internal static class RebirthCamperStationGridFuelSwitch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiM_Workstation __instance)
        => !RebirthCamperStationGridCustody.IsHeld(__instance?.TileEntity as RebirthCamperStationState);
}
