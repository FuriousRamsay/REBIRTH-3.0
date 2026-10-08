using HarmonyLib;

#nullable disable

public static class RebirthVehicleRespawnPatchInstaller
{
    public const string HarmonyId = "rebirth.vehicle-respawn.3.1";

    private static bool installed;
    private static readonly Harmony Harmony = new Harmony(HarmonyId);

    public static bool IsInstalled
    {
        get { return installed; }
    }

    public static string Install()
    {
        if (installed)
            return "[REBIRTH Vehicle Respawn] loot-container 2.6 downgrade compatibility already installed.";

        RebirthHarmonyBootstrap.PatchClassOnce(
            Harmony,
            typeof(RebirthVehicleLootContainerDowngradePatch));

        installed = true;
        return "[REBIRTH Vehicle Respawn] installed 2.6-compatible loot-container downgrade path.";
    }
}
