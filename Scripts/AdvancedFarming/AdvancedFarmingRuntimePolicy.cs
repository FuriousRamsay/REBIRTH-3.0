using System;
using System.Text;

#nullable disable

/// <summary>
/// Runtime policy for the Advanced Farming feature.
/// The authoritative enabled state is supplied by RebirthSandboxOptionManager.
/// </summary>
public static class AdvancedFarmingRuntimePolicy
{
    private static bool s_enabled = true;
    private static int s_growthSeconds = 90 * 60;
    private static int s_maxCatchupPlantsPerWake = 64;
    private static int s_tickJitterSpread = 600;
    private static int s_minimumNaturalSunlight = 8;
    private static int s_policyVersion;
    private static bool s_diagnosticHardDisableAll;

    public static bool Enabled { get { return s_enabled && !s_diagnosticHardDisableAll; } }
    public static bool ConfiguredEnabled { get { return s_enabled; } }
    public static bool DiagnosticHardDisableAll { get { return s_diagnosticHardDisableAll; } }
    public static int GrowthMinutes { get { return Math.Max(1, s_growthSeconds / 60); } }
    public static int GrowthSeconds { get { return s_growthSeconds; } }
    public static int MaxCatchupPlantsPerWake { get { return s_maxCatchupPlantsPerWake; } }
    public static int TickJitterSpread { get { return s_tickJitterSpread; } }
    public static int MinimumNaturalSunlight { get { return s_minimumNaturalSunlight; } }
    public static int PolicyVersion { get { return s_policyVersion; } }

    public static void SetDiagnosticHardDisableAll(bool enabled, string reason)
    {
        if (s_diagnosticHardDisableAll == enabled)
            return;

        s_diagnosticHardDisableAll = enabled;
        s_policyVersion++;

        // Do not clear provider/heat registries here: those represent loaded world state and
        // may not repopulate until chunks/TEs refresh. This diagnostic gate only suppresses
        // runtime processing and Harmony behaviors for isolation tests.
        AdvancedFarmingLightService.ClearCache();
        AdvancedFarmingTemperatureService.ClearCache();
        AdvancedFarmingHoverTextService.ClearConditionCache();
        AdvancedFarmingDynamicLightOpacityService.ClearPendingSunlightRefreshes();
        AdvancedFarmingDoorVisualRefreshService.ClearPendingVisualRefreshes();

        Log.Out("[AdvancedFarming] DiagnosticHardDisableAll=" + s_diagnosticHardDisableAll + " reason=" + (reason ?? string.Empty));
    }

    public static void SetEnabled(bool enabled)
    {
        SetEnabledLocal(enabled);
        AdvancedFarmingSyncService.BroadcastRuntimePolicy();
    }

    public static void SetEnabledFromNetwork(bool enabled)
    {
        SetEnabledLocal(enabled);
    }

    public static void SetEnabledFromRebirthSandbox(bool enabled)
    {
        SetEnabledLocal(enabled);
    }

    private static void SetEnabledLocal(bool enabled)
    {
        if (s_enabled == enabled)
        {
            RebirthVariables.customAdvancedFarming = enabled;
            return;
        }

        s_enabled = enabled;
        s_policyVersion++;
        RebirthVariables.customAdvancedFarming = enabled;

        AdvancedFarmingLightService.ClearCache();
        AdvancedFarmingTemperatureService.ClearCache();
        AdvancedFarmingHoverTextService.ClearConditionCache();
        AdvancedFarmingDynamicLightOpacityService.ClearPendingSunlightRefreshes();
        AdvancedFarmingDoorVisualRefreshService.ClearPendingVisualRefreshes();
        AdvancedFarmingCatchupService.ResetSchedulingState();

        if (enabled)
            RebirthCropActivationHarvestPolicy.EnableManualTest(0, 0, true);
        else
            RebirthCropActivationHarvestPolicy.Disable();
    }

    public static void SetGrowthMinutes(int minutes)
    {
        if (minutes < 1) minutes = 1;
        if (minutes > 10080) minutes = 10080;
        SetGrowthSeconds(minutes * 60);
        RebirthVariables.customAdvancedFarmingTime = minutes;
    }

    public static void SetGrowthSeconds(int seconds)
    {
        SetGrowthSecondsLocal(seconds);
        AdvancedFarmingSyncService.BroadcastRuntimePolicy();
    }

    public static void SetGrowthSecondsFromNetwork(int seconds)
    {
        SetGrowthSecondsLocal(seconds);
    }

    private static void SetGrowthSecondsLocal(int seconds)
    {
        if (seconds < 1) seconds = 1;
        if (seconds > 10080 * 60) seconds = 10080 * 60;
        s_growthSeconds = seconds;
        s_policyVersion++;
        RebirthVariables.customAdvancedFarmingTime = Math.Max(1, (seconds + 59) / 60);
        AdvancedFarmingHoverTextService.ClearConditionCache();
    }

    public static void SetMinimumNaturalSunlight(int value)
    {
        SetMinimumNaturalSunlightLocal(value);
        AdvancedFarmingSyncService.BroadcastRuntimePolicy();
    }

    public static void SetMinimumNaturalSunlightFromNetwork(int value)
    {
        SetMinimumNaturalSunlightLocal(value);
    }

    private static void SetMinimumNaturalSunlightLocal(int value)
    {
        if (value < 0) value = 0;
        if (value > 15) value = 15;
        if (s_minimumNaturalSunlight == value)
            return;

        s_minimumNaturalSunlight = value;
        s_policyVersion++;
        RebirthVariables.customAdvancedFarmingMinimumNaturalSunlight = value;
        AdvancedFarmingLightService.ClearCache();
        AdvancedFarmingHoverTextService.ClearConditionCache();
    }

    public static int GetRequiredNaturalSunlight(int configuredRequiredLight)
    {
        int required = configuredRequiredLight;
        if (required < 0) required = 0;
        if (required > 15) required = 15;
        return Math.Max(required, s_minimumNaturalSunlight);
    }

    public static int GetEffectiveGrowthSeconds()
    {
#if DEBUG
        if (RebirthVariables.testCropGrowth)
            return 1;
#endif
        return Math.Max(1, s_growthSeconds);
    }

    public static int GetEffectiveGrowthSeconds(WorldBase world, Vector3i pos)
    {
        return RebirthFoodFarmingButcherySignatureService.GetEffectiveCropGrowthSeconds(world, pos, GetEffectiveGrowthSeconds());
    }

    public static int GetActiveDirectMaxElapsedSeconds()
    {
        int effectiveSeconds = GetEffectiveGrowthSeconds();

        if (effectiveSeconds <= 10)
            return 3;

        if (effectiveSeconds <= 60)
            return 6;

        return 12;
    }

    public static int GetActiveLightTrustedSeconds()
    {
        return GetEffectiveGrowthSeconds() <= 60 ? 1 : 30;
    }

    public static int GetMaxCatchupGrantSeconds()
    {
        int effectiveSeconds = GetEffectiveGrowthSeconds();

        if (effectiveSeconds <= 60)
            return Math.Max(1, effectiveSeconds * 8);

        return Math.Min(86400, Math.Max(60, effectiveSeconds * 4));
    }

    public static int GetMaxSharedCatchupSteps()
    {
        int effectiveSeconds = GetEffectiveGrowthSeconds();

        if (effectiveSeconds <= 60)
            return Math.Max(60, effectiveSeconds * 8);

        return Math.Min(86400, Math.Max(4096, effectiveSeconds * 4));
    }

    public static int GetMinAreaProcessSeconds()
    {
        return GetEffectiveGrowthSeconds() <= 60 ? 1 : 3;
    }

    public static ulong GetPlantProgressGrantCapTicks()
    {
        // Display prediction is now decoupled from the simulation wake cadence.
        // The hover path clamps to the current stage rate, so this deliberately avoids
        // forcing the simulation to wake every displayed second.
        return ulong.MaxValue;
    }

    public static ulong GetPlantWakeDelay(Vector3i pos, ulong configuredDelay)
    {
        int effectiveSeconds = GetEffectiveGrowthSeconds();

        if (effectiveSeconds <= 10)
            return GetJitteredDelay(pos, 40UL, 20);

        if (effectiveSeconds <= 60)
            return GetJitteredDelay(pos, 100UL, 40);

        return GetJitteredDelay(pos, 200UL, 100);
    }

    public static ulong GetJitteredDelay(Vector3i pos, ulong baseDelay)
    {
        return GetJitteredDelay(pos, baseDelay, s_tickJitterSpread);
    }

    public static ulong GetJitteredDelay(Vector3i pos, ulong baseDelay, int spread)
    {
        if (spread <= 0)
            return baseDelay;

        unchecked
        {
            int h = pos.x * 73856093 ^ pos.y * 19349663 ^ pos.z * 83492791;
            uint magnitude = h == int.MinValue
                ? 0x80000000u
                : (uint)(h < 0 ? -h : h);
            return baseDelay + (ulong)(magnitude % (uint)spread);
        }
    }

    public static string Status()
    {
        return "[AdvancedFarming] enabled=" + Enabled
            + " configuredEnabled=" + s_enabled
            + " hardDisableAll=" + s_diagnosticHardDisableAll
            + " growthSeconds=" + s_growthSeconds
            + " growthMinutesApprox=" + GrowthMinutes
            + " effectiveGrowthSeconds=" + GetEffectiveGrowthSeconds()
            + " maxCatchupPlantsPerWake=" + s_maxCatchupPlantsPerWake
            + " tickJitterSpread=" + s_tickJitterSpread
            + " minimumNaturalSunlight=" + s_minimumNaturalSunlight
            + " activeWakeDelayPolicy=relaxed"
            + " activeMaxElapsedSeconds=" + GetActiveDirectMaxElapsedSeconds()
            + " displayPredictionCapTicks=" + GetPlantProgressGrantCapTicks()
            + " policyVersion=" + s_policyVersion
#if DEBUG
            + " testCropGrowth=" + RebirthVariables.testCropGrowth
#endif
            ;
    }
}

/// <summary>
/// Minimal compatibility surface for old Advanced Farming code paths.
/// Custom options will be wired in a later pass; these defaults make the feature functional now.
/// </summary>
public static class RebirthVariables
{
#if DEBUG
    public static bool testCropGrowth = false;
#endif

    public static bool customAdvancedFarming = true;
    public static int customAdvancedFarmingTime = 90;
    public static int customAdvancedFarmingMinimumNaturalSunlight = 8;
    // 2.6 compatibility surface. The 3.0 sandbox manager owns the authoritative value.
    public static int customJobsToNextTier = 10;
    public static float takeDelay = 0.5f;

    public static string customScenario = string.Empty;
    public static bool customMLPDarkness = false;
}
