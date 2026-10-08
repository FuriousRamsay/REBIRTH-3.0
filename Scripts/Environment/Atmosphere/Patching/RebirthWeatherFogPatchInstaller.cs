using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

#nullable disable

public static class RebirthWeatherFogPatchInstaller
{
    private const string HarmonyId = "rebirth.weatherfog.3.1";
    private static readonly HarmonyLib.Harmony HarmonyInstance = new HarmonyLib.Harmony(HarmonyId);
    private static bool installed;

    public static string Install()
    {
        if (installed) return "[WeatherFog] patch already installed.";
        HarmonyInstance.CreateClassProcessor(typeof(RebirthWeatherFogEnvironmentPatch)).Patch();
        installed = true;
        return "[WeatherFog] installed narrow atmospheric fog target patch.";
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        MethodInfo setFogDensity = AccessTools.Method(typeof(SkyManager), nameof(SkyManager.SetFogDensity), new[] { typeof(float) });
        MethodInfo vanillaLerp = AccessTools.Method(typeof(Mathf), nameof(Mathf.Lerp), new[] { typeof(float), typeof(float), typeof(float) });
        MethodInfo replacement = AccessTools.Method(
            typeof(RebirthWeatherFogPolicy),
            nameof(RebirthWeatherFogPolicy.LerpAtmosphericFog),
            new[] { typeof(float), typeof(float), typeof(float) });
        if (setFogDensity == null || vanillaLerp == null || replacement == null)
            throw new InvalidOperationException("REBIRTH Weather Fog method signatures are unavailable.");

        bool replaced = false;
        for (int i = 1; i < codes.Count; i++)
        {
            if (!codes[i].Calls(setFogDensity)) continue;
            for (int j = i - 1; j >= 0 && j >= i - 8; j--)
            {
                if (!codes[j].Calls(vanillaLerp)) continue;
                codes[j].operand = replacement;
                replaced = true;
                break;
            }
            if (replaced) break;
        }
        if (!replaced)
            throw new InvalidOperationException("REBIRTH Weather Fog could not locate the final fog-density interpolation call.");
        return codes;
    }
}

[HarmonyPatch(typeof(WorldEnvironment), nameof(WorldEnvironment.SpectrumsFrameUpdate))]
internal static class RebirthWeatherFogEnvironmentPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return RebirthWeatherFogPatchInstaller.Transpiler(instructions);
    }
}
