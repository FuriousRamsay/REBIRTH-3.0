using UnityEngine;
using UnityEngine.Rendering;

#nullable disable

public static class RebirthPitchBlackPolicy
{
    private const float NightFadeInStartHour = 19f;
    private const float NightFadeInEndHour = 23f;
    private const float NightFadeOutStartHour = 5f;
    private const float NightFadeOutEndHour = 7f;

    private static readonly Color NearBlack = new Color(0.01f, 0.01f, 0.01f, 1f);
    private static bool enabled;
    private static bool overrideApplied;

    private static AmbientMode nativeAmbientMode;
    private static Color nativeAmbientLight;
    private static float nativeAmbientIntensity;
    private static float nativeReflectionIntensity;
    private static Color nativeSky;
    private static Color nativeSun;
    private static Color nativeSunLight;
    private static Color nativeMoon;
    private static Color nativeFog;

    private static AmbientMode appliedAmbientMode;
    private static Color appliedAmbientLight;
    private static float appliedAmbientIntensity;
    private static float appliedReflectionIntensity;
    private static Color appliedSky;
    private static Color appliedSun;
    private static Color appliedSunLight;
    private static Color appliedMoon;
    private static Color appliedFog;
    private static bool appliedSkyField;
    private static bool appliedSunLightField;

    public static bool Enabled { get { return enabled; } }

    public static void SetEnabled(bool value)
    {
        if (enabled == value) return;
        enabled = value;
        if (!enabled && !GameManager.IsDedicatedServer)
            RestoreOwnedValues();
    }

    /// <summary>
    /// The postfix owns a small set of render fields. Restore those fields before
    /// the next native environment pass so a native pass that does not rewrite a
    /// particular value cannot make REBIRTH capture its own previous override as
    /// the next baseline. Restoration is conditional on the field still matching
    /// the value REBIRTH wrote, so a later writer is not overwritten here.
    /// </summary>
    public static void BeforeNativeEnvironmentUpdate()
    {
        if (GameManager.IsDedicatedServer) return;
        RestoreOwnedValues();
    }

    public static void Apply(World world, EntityPlayerLocal localPlayer)
    {
        if (GameManager.IsDedicatedServer)
            return;

        if (!enabled || world == null || localPlayer == null)
        {
            RestoreOwnedValues();
            return;
        }

        float currentHour = GetCurrentHour(world.worldTime);
        float nightAlpha = ComputeNightAlpha(currentHour);
        if (nightAlpha <= 0f)
        {
            RestoreOwnedValues();
            return;
        }

        CaptureNativeValues();
        bool bloodMoon = SkyManager.IsBloodMoonVisible();

        appliedSkyField = !bloodMoon;
        if (appliedSkyField)
        {
            appliedSky = Color.Lerp(nativeSky, Color.black, nightAlpha);
            SkyManager.SetSkyColor(appliedSky);
        }

        appliedSun = Color.Lerp(nativeSun, Color.black, nightAlpha);
        SkyManager.SetSunColor(appliedSun);
        appliedSunLightField = SkyManager.sunLight != null;
        if (appliedSunLightField)
        {
            appliedSunLight = Color.Lerp(nativeSunLight, Color.black, nightAlpha);
            SkyManager.sunLight.color = appliedSunLight;
        }

        appliedMoon = Color.Lerp(nativeMoon, Color.black, nightAlpha);
        SkyManager.SetMoonLightColor(appliedMoon);

        appliedFog = Color.Lerp(nativeFog, NearBlack, nightAlpha);
        SkyManager.SetFogColor(appliedFog);

        appliedAmbientMode = AmbientMode.Flat;
        appliedAmbientLight = Color.Lerp(nativeAmbientLight, NearBlack, nightAlpha);
        appliedAmbientIntensity = Mathf.Lerp(nativeAmbientIntensity, 0f, nightAlpha);
        appliedReflectionIntensity = Mathf.Lerp(nativeReflectionIntensity, 0f, nightAlpha);
        RenderSettings.ambientMode = appliedAmbientMode;
        RenderSettings.ambientLight = appliedAmbientLight;
        RenderSettings.ambientIntensity = appliedAmbientIntensity;
        RenderSettings.reflectionIntensity = appliedReflectionIntensity;
        overrideApplied = true;
    }

    private static void CaptureNativeValues()
    {
        nativeAmbientMode = RenderSettings.ambientMode;
        nativeAmbientLight = RenderSettings.ambientLight;
        nativeAmbientIntensity = RenderSettings.ambientIntensity;
        nativeReflectionIntensity = RenderSettings.reflectionIntensity;
        nativeSky = SkyManager.GetSkyColor();
        nativeSun = SkyManager.GetSunLightColor();
        nativeSunLight = SkyManager.sunLight != null ? SkyManager.sunLight.color : nativeSun;
        nativeMoon = SkyManager.moonLightColor;
        nativeFog = SkyManager.GetFogColor();
    }

    private static void RestoreOwnedValues()
    {
        if (!overrideApplied)
            return;

        if (RenderSettings.ambientMode == appliedAmbientMode)
            RenderSettings.ambientMode = nativeAmbientMode;
        if (ColorApproximately(RenderSettings.ambientLight, appliedAmbientLight))
            RenderSettings.ambientLight = nativeAmbientLight;
        if (Mathf.Approximately(RenderSettings.ambientIntensity, appliedAmbientIntensity))
            RenderSettings.ambientIntensity = nativeAmbientIntensity;
        if (Mathf.Approximately(RenderSettings.reflectionIntensity, appliedReflectionIntensity))
            RenderSettings.reflectionIntensity = nativeReflectionIntensity;

        if (appliedSkyField && ColorApproximately(SkyManager.GetSkyColor(), appliedSky))
            SkyManager.SetSkyColor(nativeSky);
        if (ColorApproximately(SkyManager.GetSunLightColor(), appliedSun))
            SkyManager.SetSunColor(nativeSun);
        if (appliedSunLightField && SkyManager.sunLight != null
            && ColorApproximately(SkyManager.sunLight.color, appliedSunLight))
            SkyManager.sunLight.color = nativeSunLight;
        if (ColorApproximately(SkyManager.moonLightColor, appliedMoon))
            SkyManager.SetMoonLightColor(nativeMoon);
        if (ColorApproximately(SkyManager.GetFogColor(), appliedFog))
            SkyManager.SetFogColor(nativeFog);

        overrideApplied = false;
        appliedSkyField = false;
        appliedSunLightField = false;
    }

    private static bool ColorApproximately(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r)
            && Mathf.Approximately(a.g, b.g)
            && Mathf.Approximately(a.b, b.b)
            && Mathf.Approximately(a.a, b.a);
    }

    private static float GetCurrentHour(ulong worldTime)
    {
        return GameUtils.WorldTimeToHours(worldTime) + GameUtils.WorldTimeToMinutes(worldTime) / 60f;
    }

    private static float ComputeNightAlpha(float hour)
    {
        if (hour >= NightFadeInStartHour)
            return Smooth01(Mathf.InverseLerp(NightFadeInStartHour, NightFadeInEndHour, hour));
        if (hour < NightFadeOutEndHour)
            return 1f - Smooth01(Mathf.InverseLerp(NightFadeOutStartHour, NightFadeOutEndHour, hour));
        return 0f;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
