#if DEBUG
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// Diagnostic-only rendered-light shadow suppression for powered block lights.
/// This does not alter chunk BLOCK light, power state, crop logic, or Advanced Farming
/// exposure rules; it only forces Unity Light shadows to None for LightLOD instances
/// attached to BlockPoweredLight block entities while the diagnostic flag is enabled.
///
/// The Harmony postfix is installed only while suppression is active, so normal gameplay
/// has no always-patched per-frame diagnostic wrapper on LightLOD.FrameUpdate.
/// </summary>
[HarmonyPatch(typeof(LightLOD), nameof(LightLOD.FrameUpdate))]
public static class Harmony_RebirthPoweredLightShadowDiagnosticsPatch
{
    private const string HarmonyId = "rebirth.fresh.advancedfarming.poweredlightshadows";
    private static readonly Harmony s_harmony = new Harmony(HarmonyId);
    private static bool s_patchInstalled;

    /// <summary>
    /// Diagnostic only. When true, powered block LightLOD instances are forced to
    /// LightShadows.None after vanilla LightLOD.FrameUpdate runs. Leave false for normal play.
    /// </summary>
    public static bool DisablePoweredLightShadows;

    public static bool PatchInstalled { get { return s_patchInstalled; } }

    public static void Postfix(LightLOD __instance)
    {
        if (!DisablePoweredLightShadows)
            return;

        SuppressPoweredLightShadow(__instance);
    }

    public static string SetDisabled(bool disabled)
    {
        if (disabled)
        {
            string installResult = EnsurePatchInstalled();
            DisablePoweredLightShadows = true;
            int suppressed = ApplyImmediateSuppression();
            return installResult + " Powered-light rendered shadows disabled immediately for " + suppressed + " currently shadow-casting powered LightLOD instance(s).";
        }

        DisablePoweredLightShadows = false;
        RemovePatchIfInstalled();
        int restored = ApplyImmediateRestore();
        return "Powered-light rendered shadows enabled; diagnostic LightLOD.FrameUpdate postfix removed. Restored rendered shadow state on " + restored + " powered LightLOD instance(s).";
    }

    public static string Toggle()
    {
        return SetDisabled(!DisablePoweredLightShadows);
    }

    public static string BuildStatus()
    {
        int detected;
        int enabled;
        int shadowCasting;
        GetScenePoweredLightCounts(out detected, out enabled, out shadowCasting);

        return "[AdvancedFarming] poweredLightShadowsEnabled=" + (!DisablePoweredLightShadows)
            + " suppressionActive=" + DisablePoweredLightShadows
            + " patchInstalled=" + s_patchInstalled
            + " detectedPoweredLightLods=" + detected
            + " enabledPoweredLightLods=" + enabled
            + " currentlyShadowCasting=" + shadowCasting
            + " (diagnostic only; affects rendered Unity shadows, not BLOCK light or crop logic).";
    }

    public static int ApplyImmediateSuppression()
    {
        if (GameManager.IsDedicatedServer)
            return 0;

        LightLOD[] lightLods = UnityEngine.Object.FindObjectsByType<LightLOD>(FindObjectsSortMode.None);
        int suppressed = 0;
        for (int i = 0; i < lightLods.Length; i++)
        {
            if (SuppressPoweredLightShadow(lightLods[i]))
                suppressed++;
        }

        return suppressed;
    }

    public static int ApplyImmediateRestore()
    {
        if (GameManager.IsDedicatedServer)
            return 0;

        LightLOD[] lightLods = UnityEngine.Object.FindObjectsByType<LightLOD>(FindObjectsSortMode.None);
        int restored = 0;
        for (int i = 0; i < lightLods.Length; i++)
        {
            LightLOD lightLod = lightLods[i];
            if (lightLod == null || !IsPoweredBlockLight(lightLod))
                continue;

            Light light = lightLod.GetLight();
            if (light == null)
                continue;

            bool changed = false;
            if (light.shadows != lightLod.shadowStateMaster)
            {
                light.shadows = lightLod.shadowStateMaster;
                changed = true;
            }

            if (light.shadowStrength != lightLod.shadowStrengthMaster)
            {
                light.shadowStrength = lightLod.shadowStrengthMaster;
                changed = true;
            }

            if (changed)
                restored++;
        }

        return restored;
    }

    private static string EnsurePatchInstalled()
    {
        if (s_patchInstalled)
            return "Powered-light rendered-shadow diagnostic patch already active.";

        try
        {
            s_harmony.CreateClassProcessor(typeof(Harmony_RebirthPoweredLightShadowDiagnosticsPatch)).Patch();
            s_patchInstalled = true;
            return "Powered-light rendered-shadow diagnostic patch installed.";
        }
        catch
        {
            s_patchInstalled = false;
            return "Powered-light rendered-shadow diagnostic patch install failed: Harmony patch exception.";
        }
    }

    private static void RemovePatchIfInstalled()
    {
        if (!s_patchInstalled)
            return;

        s_harmony.UnpatchSelf();
        s_patchInstalled = false;
    }

    private static bool SuppressPoweredLightShadow(LightLOD lightLod)
    {
        if (lightLod == null || !IsPoweredBlockLight(lightLod))
            return false;

        Light light = lightLod.GetLight();
        if (light == null)
            return false;

        bool changed = false;
        if (light.shadows != LightShadows.None)
        {
            light.shadows = LightShadows.None;
            changed = true;
        }

        if (light.shadowStrength != 0f)
        {
            light.shadowStrength = 0f;
            changed = true;
        }

        return changed;
    }

    private static void GetScenePoweredLightCounts(out int detected, out int enabled, out int shadowCasting)
    {
        detected = 0;
        enabled = 0;
        shadowCasting = 0;

        if (GameManager.IsDedicatedServer)
            return;

        LightLOD[] lightLods = UnityEngine.Object.FindObjectsByType<LightLOD>(FindObjectsSortMode.None);
        for (int i = 0; i < lightLods.Length; i++)
        {
            LightLOD lightLod = lightLods[i];
            if (!IsPoweredBlockLight(lightLod))
                continue;

            detected++;
            Light light = lightLod.GetLight();
            if (light == null)
                continue;

            if (light.enabled)
                enabled++;
            if (light.shadows != LightShadows.None)
                shadowCasting++;
        }
    }

    private static bool IsPoweredBlockLight(LightLOD lightLod)
    {
        if (lightLod == null || lightLod.bed == null)
            return false;

        Block block = lightLod.bed.blockValue.Block;
        return block is BlockPoweredLight;
    }
}
#endif
