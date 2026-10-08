#if !DEBUG
#nullable disable

/// <summary>
/// Release compatibility facade. Manual harvest test patches and scaffolds are omitted from Release builds.
/// </summary>
public static class RebirthHarvestSystemsManualPatchInstaller
{
    public const string HarmonyId = "rebirth.fresh.harvest.systems.manual";
    public static bool IsCropPatchInstalled { get { return false; } }
    public static bool IsAttackHarvestPatchInstalled { get { return false; } }
    public static bool IsStumpHarvestContextPatchInstalled { get { return false; } }
    public static bool AnyInstalled { get { return false; } }
    private static string Disabled() { return "[RebirthHarvestSystems] Manual test patches require a Debug build."; }
    public static string InstallCropPatch() { return Disabled(); }
    public static string InstallAttackHarvestPatch() { return Disabled(); }
    public static string InstallStumpHarvestContextPatch() { return Disabled(); }
    public static string InstallAll() { return Disabled(); }
    public static string UninstallAll() { return Disabled(); }
    public static string GetStatusReport() { return Disabled(); }
}
#endif
