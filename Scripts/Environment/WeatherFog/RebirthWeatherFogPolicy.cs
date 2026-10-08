using UnityEngine;

#nullable disable

public static class RebirthWeatherFogPolicy
{
    private static RebirthWeatherFogBehavior behavior = RebirthWeatherFogBehavior.Dynamic;
    private static RebirthWeatherFogIntensity intensity = RebirthWeatherFogIntensity.Normal;

    public static RebirthWeatherFogBehavior Behavior { get { return behavior; } }
    public static RebirthWeatherFogIntensity Intensity { get { return intensity; } }

    public static void SetOptions(RebirthWeatherFogBehavior newBehavior, RebirthWeatherFogIntensity newIntensity)
    {
        behavior = newBehavior < RebirthWeatherFogBehavior.Dynamic || newBehavior > RebirthWeatherFogBehavior.Disabled
            ? RebirthWeatherFogBehavior.Dynamic : newBehavior;
        intensity = newIntensity < RebirthWeatherFogIntensity.None || newIntensity > RebirthWeatherFogIntensity.VeryHeavy
            ? RebirthWeatherFogIntensity.Normal : newIntensity;
    }

    public static float LerpAtmosphericFog(float currentDensity, float vanillaTarget, float transition)
    {
        if (GameManager.IsDedicatedServer || IsUnderwaterCamera())
            return Mathf.Lerp(currentDensity, vanillaTarget, transition);

        float target;
        switch (behavior)
        {
            case RebirthWeatherFogBehavior.Disabled:
                target = 0f;
                break;
            case RebirthWeatherFogBehavior.Static:
                target = GetStaticTarget(intensity);
                break;
            default:
                target = Mathf.Clamp01(vanillaTarget * GetDynamicMultiplier(intensity));
                break;
        }
        return Mathf.Lerp(currentDensity, target, transition);
    }

    private static bool IsUnderwaterCamera()
    {
        GameManager gameManager = GameManager.Instance;
        World world = gameManager != null ? gameManager.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        return player != null && player.IsUnderwaterCamera;
    }

    private static float GetDynamicMultiplier(RebirthWeatherFogIntensity value)
    {
        // Exact user-defined fog intensity scale. Dynamic mode multiplies the
        // vanilla atmospheric target by the selected value.
        switch (value)
        {
            case RebirthWeatherFogIntensity.None: return 0f;
            case RebirthWeatherFogIntensity.VeryLow: return 0.10f;
            case RebirthWeatherFogIntensity.Low: return 0.175f;
            case RebirthWeatherFogIntensity.Normal: return 0.30f;
            case RebirthWeatherFogIntensity.Heavy: return 0.45f;
            case RebirthWeatherFogIntensity.VeryHeavy: return 0.60f;
            default: return 0.30f;
        }
    }

    private static float GetStaticTarget(RebirthWeatherFogIntensity value)
    {
        // Static mode uses the same exact scale as fixed fog-density targets.
        switch (value)
        {
            case RebirthWeatherFogIntensity.None: return 0f;
            case RebirthWeatherFogIntensity.VeryLow: return 0.10f;
            case RebirthWeatherFogIntensity.Low: return 0.175f;
            case RebirthWeatherFogIntensity.Normal: return 0.30f;
            case RebirthWeatherFogIntensity.Heavy: return 0.45f;
            case RebirthWeatherFogIntensity.VeryHeavy: return 0.60f;
            default: return 0.30f;
        }
    }

}
