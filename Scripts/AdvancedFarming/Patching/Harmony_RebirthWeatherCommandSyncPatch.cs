using System.Collections.Generic;

#nullable disable

public static class Harmony_RebirthWeatherCommandSyncPatch
{
    public static void Prefix(List<string> _params)
    {
        if (!ChangesRainOverride(_params))
            return;

        // Capture the final interval under the old override before ConsoleCmdWeather
        // changes it. This prevents a newly forced rain state from being applied
        // retroactively to time that elapsed while it was dry, and preserves the
        // final legitimate interval when forced rain is disabled.
        AdvancedFarmingWeatherSyncService.FlushLoadedPlotRainExposureBeforeOverrideChange();
    }

    public static void Postfix(List<string> _params)
    {
        if (!ChangesRainOverride(_params))
            return;

        AdvancedFarmingWeatherSyncService.BroadcastRainOverride();
    }

    private static bool ChangesRainOverride(List<string> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return false;

        string operation = parameters[0];
        return operation.EqualsCaseInsensitive("rain")
            || operation.EqualsCaseInsensitive("defaults")
            || operation.EqualsCaseInsensitive("d");
    }
}
