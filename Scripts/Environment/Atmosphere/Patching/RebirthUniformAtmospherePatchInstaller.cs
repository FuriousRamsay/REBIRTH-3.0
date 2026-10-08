using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#nullable disable

public static class RebirthUniformAtmospherePatchInstaller
{
    private const string HarmonyId = "rebirth.uniformatmosphere.3.1";
    private static readonly HarmonyLib.Harmony HarmonyInstance = new HarmonyLib.Harmony(HarmonyId);
    private static bool installed;

    public static string Install()
    {
        if (GameManager.IsDedicatedServer)
            return "[UniformAtmosphere] dedicated server: rendering patch not installed.";
        if (installed)
            return "[UniformAtmosphere] patch already installed.";

        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(
                HarmonyInstance,
                typeof(RebirthUniformAtmosphereSpectrumPatch));
            installed = true;
            return "[UniformAtmosphere] installed direct biome-spectrum getter patch.";
        }
        catch (Exception ex)
        {
            // A rendering option must never abort the rest of the REBIRTH bootstrap.
            string failure = "[UniformAtmosphere] disabled after patch failure: "
                + ex.GetType().Name + ": " + ex.Message;
            Log.Error(failure);
            return failure;
        }
    }
}

/// <summary>
/// 3.1 stable calls the five BiomeAtmosphereEffects spectrum getters directly.
/// Patch those getters instead of transpiling WorldEnvironment.SpectrumsFrameUpdate.
/// </summary>
[RebirthManualHarmonyPatch]
[HarmonyPatch]
internal static class RebirthUniformAtmosphereSpectrumPatch
{
    [HarmonyPrepare]
    private static bool PrepareForManualInstall()
    {
        return !RebirthHarmonyBootstrap.IsApplyingPatchAll;
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(BiomeAtmosphereEffects), nameof(BiomeAtmosphereEffects.GetSkyColorSpectrum));
        yield return AccessTools.Method(typeof(BiomeAtmosphereEffects), nameof(BiomeAtmosphereEffects.GetSunColorSpectrum));
        yield return AccessTools.Method(typeof(BiomeAtmosphereEffects), nameof(BiomeAtmosphereEffects.GetMoonColorSpectrum));
        yield return AccessTools.Method(typeof(BiomeAtmosphereEffects), nameof(BiomeAtmosphereEffects.GetFogColorSpectrum));
        yield return AccessTools.Method(typeof(BiomeAtmosphereEffects), nameof(BiomeAtmosphereEffects.GetFogFadeColorSpectrum));
    }

    [HarmonyPrefix]
    private static bool Prefix(
        BiomeAtmosphereEffects __instance,
        float __0,
        MethodBase __originalMethod,
        ref Color __result)
    {
        if (!RebirthUniformAtmospherePolicy.Enabled
            || GameManager.IsDedicatedServer
            || __instance == null
            || __originalMethod == null)
        {
            return true;
        }

        AtmosphereEffect.ESpecIdx spectrumIndex;
        switch (__originalMethod.Name)
        {
            case nameof(BiomeAtmosphereEffects.GetSkyColorSpectrum):
                spectrumIndex = AtmosphereEffect.ESpecIdx.Sky;
                break;
            case nameof(BiomeAtmosphereEffects.GetSunColorSpectrum):
                spectrumIndex = AtmosphereEffect.ESpecIdx.Sun;
                break;
            case nameof(BiomeAtmosphereEffects.GetMoonColorSpectrum):
                spectrumIndex = AtmosphereEffect.ESpecIdx.Moon;
                break;
            case nameof(BiomeAtmosphereEffects.GetFogColorSpectrum):
                spectrumIndex = AtmosphereEffect.ESpecIdx.Fog;
                break;
            case nameof(BiomeAtmosphereEffects.GetFogFadeColorSpectrum):
                spectrumIndex = AtmosphereEffect.ESpecIdx.FogFade;
                break;
            default:
                return true;
        }

        __result = RebirthUniformAtmospherePolicy.GetColorFromSpectrum(
            __instance,
            __instance.currentBiomeIntensity,
            __0,
            spectrumIndex);
        return false;
    }
}
