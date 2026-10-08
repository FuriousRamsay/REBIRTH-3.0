using UnityEngine;

#nullable disable

public static class RebirthUniformAtmospherePolicy
{
    private static readonly BiomeIntensity PineForestIntensity =
        new BiomeIntensity((byte)BiomeDefinition.BiomeType.PineForest);

    private static bool enabled;

    public static bool Enabled { get { return enabled; } }

    public static void SetEnabled(bool value)
    {
        enabled = value;
    }

    public static Color GetColorFromSpectrum(
        BiomeAtmosphereEffects atmosphereEffects,
        BiomeIntensity activeBiomeIntensity,
        float dayTimeScalar,
        AtmosphereEffect.ESpecIdx spectrumIndex)
    {
        if (!enabled || GameManager.IsDedicatedServer || atmosphereEffects == null)
            return atmosphereEffects != null
                ? atmosphereEffects.getColorFromSpectrum(activeBiomeIntensity, dayTimeScalar, spectrumIndex)
                : Color.clear;

        return atmosphereEffects.getColorFromSpectrum(PineForestIntensity, dayTimeScalar, spectrumIndex);
    }
}
