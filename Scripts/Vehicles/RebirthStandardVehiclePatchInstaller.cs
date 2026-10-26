using HarmonyLib;

public static class RebirthStandardVehiclePatchInstaller
{
    private static readonly object Sync=new object();
    private static readonly Harmony Harmony=new Harmony("rebirth.standardvehicles.production");
    private static bool installed;
    public static void Install()
    {
        lock(Sync)
        {
            if(installed)return;
            RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthStandardVehicleRearLights));
            RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthTruck4x4HeadlightSpill));
            RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthVehicleLightToggleTrace));
            RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(Rebirth.Vehicles.Authoring.SeatPoseGameplayPatch));
            RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthVehicleLockRequestNullProbe));
            RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthVehicleLockResponseNullProbe));
            installed=true;
        }
    }
}
