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
            Harmony.CreateClassProcessor(typeof(RebirthStandardVehicleRearLights)).Patch();
            installed=true;
        }
    }
}
