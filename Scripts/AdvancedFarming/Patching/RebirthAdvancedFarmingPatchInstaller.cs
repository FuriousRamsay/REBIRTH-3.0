using HarmonyLib;
using System;

#nullable disable

public static class RebirthAdvancedFarmingPatchInstaller
{
    public const string HarmonyId = "rebirth.fresh.advancedfarming.3_1";
    private static readonly Harmony s_harmony = new Harmony(HarmonyId);
    private static bool s_tileEntityPatchInstalled;
    private static bool s_cropHarvestPatchInstalled;
    private static bool s_workstationHeatPatchInstalled;
    private static bool s_worldSetBlockPatchInstalled;
    private static bool s_lightProcessorPatchInstalled;
    private static bool s_weatherCommandPatchInstalled;
    private static int s_lightProcessorInstalledMethods;
    private static int s_workstationHeatInstalledMethods;

    public static bool IsTileEntityPatchInstalled { get { return s_tileEntityPatchInstalled; } }
    public static bool IsCropHarvestPatchInstalled { get { return s_cropHarvestPatchInstalled; } }
    public static bool IsWorkstationHeatPatchInstalled { get { return s_workstationHeatPatchInstalled; } }
    public static bool IsWorldSetBlockPatchInstalled { get { return s_worldSetBlockPatchInstalled; } }
    public static bool IsLightProcessorPatchInstalled { get { return s_lightProcessorPatchInstalled; } }
    public static bool IsWeatherCommandPatchInstalled { get { return s_weatherCommandPatchInstalled; } }
    public static int LightProcessorInstalledMethods { get { return s_lightProcessorInstalledMethods; } }
    public static int WorkstationHeatInstalledMethods { get { return s_workstationHeatInstalledMethods; } }

    public static string Install()
    {
        string tileResult = InstallTileEntityPatch();
        string cropResult = InstallCropHarvestPatch();
        string heatResult = InstallWorkstationHeatPatch();
        string worldSetBlockResult = InstallWorldSetBlockPatch();
        string lightProcessorResult = InstallLightProcessorPatch();
        string weatherCommandResult = InstallWeatherCommandPatch();
        return tileResult + "\n" + cropResult + "\n" + heatResult + "\n" + worldSetBlockResult + "\n" + lightProcessorResult + "\n" + weatherCommandResult + "\n[AdvancedFarmingPatchInstaller] Runtime state is controlled by RebirthSandboxCode.";
    }

    public static string InstallTileEntityPatch()
    {
        if (s_tileEntityPatchInstalled)
            return "[AdvancedFarmingPatchInstaller] TileEntity.InstantiateFromRead patch already installed.";

        s_tileEntityPatchInstalled = TryPatchClass(typeof(RebirthAdvancedFarmingTileEntityInstantiateBinding));
        return s_tileEntityPatchInstalled
            ? "[AdvancedFarmingPatchInstaller] installed explicit TileEntity.InstantiateFromRead(PooledBinaryReader, StreamModeRead, TileEntityType, Chunk, int[], Func<int,int,int,BlockValue>) patch."
            : "[AdvancedFarmingPatchInstaller] explicit TileEntity.InstantiateFromRead patch failed.";
    }

    public static string InstallWorkstationHeatPatch()
    {
        if (s_workstationHeatPatchInstalled)
            return "[AdvancedFarmingPatchInstaller] Workstation heat patch already installed.";

        s_workstationHeatPatchInstalled = TryPatchClass(typeof(RebirthAdvancedFarmingWorkstationHeatBinding));
        s_workstationHeatInstalledMethods = s_workstationHeatPatchInstalled ? 1 : 0;
        return "[AdvancedFarmingPatchInstaller] workstation heat patch installedMethods="
            + s_workstationHeatInstalledMethods
            + " failedMethods=" + (s_workstationHeatPatchInstalled ? 0 : 1)
            + " target=TileEntityWorkstation.UpdateTick(World)";
    }

    public static string InstallWorldSetBlockPatch()
    {
        // ChunkCluster.SetBlock owns Advanced Farming exposure-cache invalidation and
        // dynamic SUN refresh queuing through its explicit 3.1 binding.
        s_worldSetBlockPatchInstalled = false;
        return "[AdvancedFarmingPatchInstaller] World.SetBlock/SetBlocksRPC patch skipped; explicit ChunkCluster.SetBlock(Vector3i, bool, BlockValue, bool, sbyte, bool, bool, bool, bool, int) binding owns cache invalidation.";
    }

    public static string InstallLightProcessorPatch()
    {
        if (s_lightProcessorPatchInstalled)
            return "[AdvancedFarmingPatchInstaller] LightProcessor dynamic opacity patches already installed.";

        Type[] patchClasses =
        {
            typeof(RebirthAdvancedFarmingRefreshSunlightBinding),
            typeof(RebirthAdvancedFarmingRefreshLightBinding),
            typeof(RebirthAdvancedFarmingSpreadLightRecursiveBinding),
            typeof(RebirthAdvancedFarmingUnspreadLightRecursiveBinding),
            typeof(RebirthAdvancedFarmingDoorSetOpenBinding),
            typeof(RebirthAdvancedFarmingDoorReadBinding),
            typeof(RebirthAdvancedFarmingChunkSetBlockBinding)
        };

        int installed = 0;
        for (int i = 0; i < patchClasses.Length; i++)
        {
            if (TryPatchClass(patchClasses[i]))
                installed++;
        }

        s_lightProcessorInstalledMethods = installed;
        s_lightProcessorPatchInstalled = installed == patchClasses.Length;
        return "[AdvancedFarmingPatchInstaller] explicit 3.1 light/door/block bindings installedMethods="
            + installed + " failedMethods=" + (patchClasses.Length - installed);
    }

    public static string InstallWeatherCommandPatch()
    {
        if (s_weatherCommandPatchInstalled)
            return "[AdvancedFarmingPatchInstaller] ConsoleCmdWeather rain-sync patch already installed.";

        s_weatherCommandPatchInstalled = TryPatchClass(typeof(RebirthAdvancedFarmingWeatherCommandBinding));
        return s_weatherCommandPatchInstalled
            ? "[AdvancedFarmingPatchInstaller] explicit ConsoleCmdWeather.Execute(List<string>, CommandSenderInfo) rain-sync patch installed."
            : "[AdvancedFarmingPatchInstaller] explicit ConsoleCmdWeather.Execute(List<string>, CommandSenderInfo) rain-sync patch failed.";
    }

    public static string InstallCropHarvestPatch()
    {
        // 3.1 does not expose the former BlockCropsGrown activation target.
        s_cropHarvestPatchInstalled = false;
        RebirthCropActivationHarvestPolicy.Disable();
        return "[AdvancedFarmingPatchInstaller] 3.1: obsolete BlockCropsGrown activation patch remains disabled.";
    }

    private static bool TryPatchClass(Type patchClass)
    {
        try
        {
            s_harmony.CreateClassProcessor(patchClass).Patch();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("[AdvancedFarmingPatchInstaller] Failed explicit patch class "
                + patchClass.FullName + ": " + ex);
            return false;
        }
    }

    public static string Uninstall()
    {
        s_harmony.UnpatchSelf();
        s_tileEntityPatchInstalled = false;
        s_cropHarvestPatchInstalled = false;
        s_workstationHeatPatchInstalled = false;
        s_worldSetBlockPatchInstalled = false;
        s_lightProcessorPatchInstalled = false;
        s_weatherCommandPatchInstalled = false;
        s_lightProcessorInstalledMethods = 0;
        s_workstationHeatInstalledMethods = 0;
        RebirthCropActivationHarvestPolicy.Disable();
        return "[AdvancedFarmingPatchInstaller] uninstalled Advanced Farming Harmony patches.";
    }

    public static string Status()
    {
        return "[AdvancedFarmingPatchInstaller] harmonyId=" + HarmonyId
            + " tileEntityPatchInstalled=" + s_tileEntityPatchInstalled
            + " cropHarvestPatchInstalled=" + s_cropHarvestPatchInstalled
            + " workstationHeatPatchInstalled=" + s_workstationHeatPatchInstalled
            + " worldSetBlockPatchInstalled=" + s_worldSetBlockPatchInstalled
            + " lightProcessorPatchInstalled=" + s_lightProcessorPatchInstalled
            + " weatherCommandPatchInstalled=" + s_weatherCommandPatchInstalled
            + " lightProcessorPatchInstalledMethods=" + s_lightProcessorInstalledMethods
            + " workstationHeatPatchInstalledMethods=" + s_workstationHeatInstalledMethods
            + " cropHarvestEnabled=" + RebirthCropActivationHarvestPolicy.Enabled;
    }
}
