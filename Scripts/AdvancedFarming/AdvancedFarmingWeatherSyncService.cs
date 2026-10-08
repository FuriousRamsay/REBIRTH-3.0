using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Synchronizes the base-game console weather rain override to remote clients.
/// 3.0's NetPackageWeather contains biome weather parameters but not the static
/// WeatherManager.forceRain field changed by "weather rain" on the server.
/// </summary>
public static class AdvancedFarmingWeatherSyncService
{
    public static void FlushLoadedPlotRainExposureBeforeOverrideChange()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || !AdvancedFarmingRuntimePolicy.Enabled)
            return;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;

        List<AdvancedFarmingWaterProviderEntry> plots;
        try
        {
            plots = AdvancedFarmingWaterProviderRegistry.CollectAllFarmPlots(world);
        }
        catch (Exception ex)
        {
            Log.Warning("[AdvancedFarmingWeatherSync] Could not enumerate loaded farm plots before weather override change: {0}", ex.Message);
            return;
        }

        int now = RebirthUtilities.TotalGameSecondsPassed();
        for (int i = 0; i < plots.Count; i++)
        {
            AdvancedFarmingWaterProviderEntry entry = plots[i];
            if (entry == null || entry.Plot == null)
                continue;

            try
            {
                AdvancedFarmingCatchupService.ProcessFarmPlot(world, entry.Pos, entry.BlockValue, entry.Plot, now);
            }
            catch (Exception ex)
            {
                Log.Warning("[AdvancedFarmingWeatherSync] Could not flush farm-plot rain exposure at {0}: {1}", entry.Pos, ex.Message);
            }
        }
    }

    public static void BroadcastRainOverride()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer)
            return;

        try
        {
            connection.SendPackage(
                NetPackageManager.GetPackage<NetPackageWeatherRainOverrideRebirth>()
                    .Setup(WeatherManager.forceRain));
        }
        catch (Exception ex)
        {
            Log.Warning("[AdvancedFarmingWeatherSync] Could not broadcast forced-rain override: {0}", ex.Message);
        }
    }
}
