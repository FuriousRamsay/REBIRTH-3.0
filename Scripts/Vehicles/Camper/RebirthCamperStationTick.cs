using System;
using HarmonyLib;

[HarmonyPatch(typeof(EntityVehicle),"Update")]
public static class RebirthCamperStationTick
{
    private static void Postfix(EntityVehicle __instance)
    {
        if(!Rebirth.Vehicles.Camper.RebirthCamperModulePatchInstaller.IsInstalled
            || !ThreadManager.IsMainThread() || __instance.world==null || __instance.world.IsRemote()
            || __instance.vehicle==null || !__instance.vehicle.GetName().Equals("rebirthCamper80s",StringComparison.OrdinalIgnoreCase))return;
        if(!ReferenceEquals(__instance.world.GetEntity(__instance.entityId),__instance))return;
        string modules;
        if(!__instance.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperModules",out modules))return;
        var runtime=RebirthCamperModuleRuntime.For(__instance);
        if(!RebirthCamperStationFuelOriginal.RestorePending(__instance))return;
        if(!RebirthCamperStationNativeActorPayment.RestorePending(__instance))return;
        runtime.TickStations();
    }
}
