using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using UnityEngine;

#nullable disable

public static class RebirthMetabolismConfig
{
    public static float UpdateRealSeconds = 1f;
    public static int MaxIngestionEntries = 64;
    public static float MlPerHydrationPoint = 25f;
    public static float BaseStomachCapacityMl = 1500f;
    public static float BaseIntestinalCapacityMl = 1000f;
    public static float ComfortableFullness = 0.70f;
    public static float FullThreshold = 0.85f;
    public static float HardFullness = 1.00f;
    // A meal is accepted in full when at least this fraction of its physical volume can fit.
    // The small remainder represents normal stomach stretch/overfill rather than deleting part of a meal.
    public static float FoodMinimumFitFraction = 0.80f;

    // Survival demand uses active real play time. World day length never changes these rates.
    public static float BaseFluidNeedMlPerReal60Minutes = 900f;

    public static float ClearLiquidHoldRealSeconds = 20f;
    public static float ClearLiquidGastricHalfTimeRealSeconds = 120f;
    public static float ClearLiquidWithFoodHalfTimeMultiplier = 1.60f;
    public static float MealFluidHoldRealSeconds = 45f;
    public static float MealFluidGastricHalfTimeRealSeconds = 300f;
    public static float IntestinalFluidResidenceRealSeconds = 30f;
    public static float BaseIntestinalFluidAbsorptionMlPerRealMinute = 112.5f;
    public static float AutoSipStartProjectedPercent = 0.72f;
    public static float AutoSipStopProjectedPercent = 0.85f;
    public static float AutoSipDefaultMl = 50f;
    public static float AutoSipCooldownRealSeconds = 25f;
    public static float CriticalDehydrationHealthLossPerRealMinute = 0.067f;

    public static float RequirementMinMultiplier = 0.70f;
    public static float RequirementMaxMultiplier = 1.40f;
    public static float UtilizationMinMultiplier = 0.70f;
    public static float UtilizationMaxMultiplier = 1.30f;
    public static float AbsorptionMinMultiplier = 0.50f;
    public static float AbsorptionMaxMultiplier = 1.75f;

    // 100 Nutrition is a reserve. Idle metabolism only spends a modest portion per real hour;
    // additional activity cost comes primarily from converting Nutrition back into Energy.
    public static float BaseFoodNeedUnitsPerReal60Minutes = 30f;

    public static float EnergyMax = 100f;
    public static float EnergyLightUsePerRealMinute = 1.50f;
    public static float EnergyModerateUsePerRealMinute = 4.00f;
    public static float EnergyHighUsePerRealMinute = 8.00f;
    public static float EnergyExtremeUsePerRealMinute = 12.00f;
    public static float EnergyRecoveryPerRealMinute = 4.00f;
    // In addition to movement-tier drain, net Stamina actually spent between metabolism ticks
    // carries a direct Energy cost. One complete 100-point Stamina bar therefore costs 5 Energy.
    public static float EnergyPerStaminaSpent = 0.05f;
    public static float FoodUnitsPerEnergyRecovered = 0.20f;
    public static float EnergyRecoveryFoodFloorPercent = 0.00f;

    // Nutrition supplies the Energy reserve. Hydration controls how efficiently that
    // Nutrition can support usable Energy. Even at zero Nutrition/Hydration, the
    // sustainable ceiling never drops below 10; current Energy can still be spent to 0.
    public static float EnergyMinimumSustainableCap = 10f;
    public static float EnergyHydrationFullEfficiencyPercent = 0.75f;
    public static float EnergyHydrationMediumEfficiencyPercent = 0.50f;
    public static float EnergyHydrationCriticalEfficiencyPercent = 0.25f;
    public static float EnergyHydrationEfficiencyAtMedium = 0.85f;
    public static float EnergyHydrationEfficiencyAtCritical = 0.60f;
    public static float EnergyHydrationEfficiencyAtZero = 0.25f;

    public static float EnergyLowThreshold = 75f;
    public static float EnergyCriticalThreshold = 25f;
    public static float StaminaRegenAtLowEnergy = 1.00f;
    public static float StaminaRegenAtCriticalEnergy = 0.50f;
    public static float StaminaRegenAtZeroEnergy = 0.20f;
    public static float FoodRequirementMinMultiplier = 0.70f;
    public static float FoodRequirementMaxMultiplier = 1.40f;

    public static float BaseDigestiveHealth = 100f;
    public static float BaseSolidGastricEmptyingMlPerRealMinute = 45f;
    public static float BaseIntestinalNutrientAbsorptionUnitsPerRealMinute = 2.25f;
    public static float IntestinalNutritionResidenceRealSeconds = 60f;
    public static float LightFoodGastricLagRealSeconds = 60f;
    public static float NormalFoodGastricLagRealSeconds = 90f;
    public static float DenseFoodGastricLagRealSeconds = 120f;
    public static float HeavyFoodGastricLagRealSeconds = 180f;
    public static float DigestionSpeedMinMultiplier = 0.50f;
    public static float DigestionSpeedMaxMultiplier = 1.75f;
    public static float GutResilienceMinMultiplier = 0.50f;
    public static float GutResilienceMaxMultiplier = 1.75f;

    // Metabolism supplement durations are real active-play seconds too.
    public static float DigestiveEnzymesRealSeconds = 300f;
    public static float ElectrolytesRealSeconds = 900f;
    public static float ProbioticsRealSeconds = 3600f;
    public static float PrebioticRealSeconds = 1800f;

    private static bool loaded;

    public static float GetEnergyHydrationEfficiency(float hydrationPercent)
    {
        float p = Mathf.Clamp01(hydrationPercent);
        float full = Mathf.Clamp01(EnergyHydrationFullEfficiencyPercent);
        float medium = Mathf.Clamp(EnergyHydrationMediumEfficiencyPercent, 0f, full);
        float critical = Mathf.Clamp(EnergyHydrationCriticalEfficiencyPercent, 0f, medium);

        if (p >= full)
            return 1f;
        if (p >= medium)
        {
            float t = full > medium ? Mathf.InverseLerp(medium, full, p) : 1f;
            return Mathf.Lerp(EnergyHydrationEfficiencyAtMedium, 1f, t);
        }
        if (p >= critical)
        {
            float t = medium > critical ? Mathf.InverseLerp(critical, medium, p) : 1f;
            return Mathf.Lerp(EnergyHydrationEfficiencyAtCritical, EnergyHydrationEfficiencyAtMedium, t);
        }

        float c = critical > 0.001f ? p / critical : 0f;
        return Mathf.Lerp(EnergyHydrationEfficiencyAtZero, EnergyHydrationEfficiencyAtCritical, c);
    }

    public static float GetEnergyReserveCeiling(float hydrationPercent, float nutritionPercent)
    {
        float nutrition = Mathf.Clamp01(nutritionPercent);
        float hydrationEfficiency = GetEnergyHydrationEfficiency(hydrationPercent);
        float raw = EnergyMax * nutrition * hydrationEfficiency;
        float minimum = Mathf.Clamp(EnergyMinimumSustainableCap, 0f, EnergyMax);
        return Mathf.Clamp(raw, minimum, EnergyMax);
    }

    public static void Load()
    {
        if (loaded) return;
        Dictionary<FieldInfo, object> prior = CapturePublishedConfig();
        try
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", "zzz_REBIRTH__3_0");
            string path = Path.Combine(root, "Config", "_Metabolism", "metabolism.xml");
            if (!File.Exists(path))
            {
                loaded = true;
                Log.Warning("[REBIRTH Metabolism] Config not found, using defaults: " + path);
                return;
            }

            XElement doc = XDocument.Load(path).Root;
            if (doc == null) throw new InvalidDataException("metabolism config root is missing");
            XElement global = doc.Element("global");
            XElement hyd = doc.Element("hydration");
            XElement nutrition = doc.Element("nutrition");
            XElement digestion = doc.Element("digestion");
            XElement energy = doc.Element("energy");
            XElement supplements = doc.Element("supplements");

            // Reject malformed/non-finite authoring before publishing any new field.
            ValidateFiniteNumericAttributes(global, "global");
            ValidateFiniteNumericAttributes(hyd, "hydration");
            ValidateFiniteNumericAttributes(nutrition, "nutrition");
            ValidateFiniteNumericAttributes(digestion, "digestion");
            ValidateFiniteNumericAttributes(energy, "energy");
            ValidateFiniteNumericAttributes(supplements, "supplements");

            UpdateRealSeconds = F(global, "updateRealSeconds", UpdateRealSeconds, 0.1f, 10f);
            MaxIngestionEntries = I(global, "maxIngestionEntries", MaxIngestionEntries, 4, 64);
            MlPerHydrationPoint = F(global, "mlPerHydrationPoint", MlPerHydrationPoint, 1f, 100f);
            BaseStomachCapacityMl = F(global, "baseStomachCapacityMl", BaseStomachCapacityMl, 250f, 4000f);
            BaseIntestinalCapacityMl = F(global, "baseIntestinalCapacityMl", BaseIntestinalCapacityMl, 250f, 4000f);
            ComfortableFullness = F(global, "comfortableFullness", ComfortableFullness, 0f, 1f);
            FullThreshold = F(global, "fullThreshold", FullThreshold, ComfortableFullness, 1f);
            HardFullness = F(global, "hardFullness", HardFullness, FullThreshold, 1.5f);
            FoodMinimumFitFraction = F(global, "foodMinimumFitFraction", FoodMinimumFitFraction, 0.50f, 1.00f);

            BaseFluidNeedMlPerReal60Minutes = F(hyd, "baseFluidNeedMlPerReal60Minutes", BaseFluidNeedMlPerReal60Minutes, 0f, 10000f);
            ClearLiquidHoldRealSeconds = F(hyd, "clearLiquidHoldRealSeconds", ClearLiquidHoldRealSeconds, 0f, 1800f);
            ClearLiquidGastricHalfTimeRealSeconds = F(hyd, "clearLiquidGastricHalfTimeRealSeconds", ClearLiquidGastricHalfTimeRealSeconds, 5f, 3600f);
            ClearLiquidWithFoodHalfTimeMultiplier = F(hyd, "clearLiquidWithFoodHalfTimeMultiplier", ClearLiquidWithFoodHalfTimeMultiplier, 1f, 4f);
            MealFluidHoldRealSeconds = F(hyd, "mealFluidHoldRealSeconds", MealFluidHoldRealSeconds, 0f, 1800f);
            MealFluidGastricHalfTimeRealSeconds = F(hyd, "mealFluidGastricHalfTimeRealSeconds", MealFluidGastricHalfTimeRealSeconds, 5f, 7200f);
            IntestinalFluidResidenceRealSeconds = F(hyd, "intestinalFluidResidenceRealSeconds", IntestinalFluidResidenceRealSeconds, 0f, 1800f);
            BaseIntestinalFluidAbsorptionMlPerRealMinute = F(hyd, "baseIntestinalFluidAbsorptionMlPerRealMinute", BaseIntestinalFluidAbsorptionMlPerRealMinute, 1f, 2000f);
            AutoSipStartProjectedPercent = F(hyd, "autosipStartProjectedPercent", AutoSipStartProjectedPercent, 0f, 1f);
            AutoSipStopProjectedPercent = F(hyd, "autosipStopProjectedPercent", AutoSipStopProjectedPercent, AutoSipStartProjectedPercent, 1f);
            AutoSipDefaultMl = F(hyd, "autosipDefaultMl", AutoSipDefaultMl, 1f, 500f);
            AutoSipCooldownRealSeconds = F(hyd, "autosipCooldownRealSeconds", AutoSipCooldownRealSeconds, 0f, 600f);
            CriticalDehydrationHealthLossPerRealMinute = F(hyd, "criticalHealthLossPerRealMinute", CriticalDehydrationHealthLossPerRealMinute, 0f, 10f);
            RequirementMinMultiplier = F(hyd, "requirementMinMultiplier", RequirementMinMultiplier, 0.1f, 1f);
            RequirementMaxMultiplier = F(hyd, "requirementMaxMultiplier", RequirementMaxMultiplier, 1f, 4f);
            UtilizationMinMultiplier = F(hyd, "utilizationMinMultiplier", UtilizationMinMultiplier, 0.1f, 1f);
            UtilizationMaxMultiplier = F(hyd, "utilizationMaxMultiplier", UtilizationMaxMultiplier, 1f, 4f);
            AbsorptionMinMultiplier = F(hyd, "absorptionMinMultiplier", AbsorptionMinMultiplier, 0.1f, 1f);
            AbsorptionMaxMultiplier = F(hyd, "absorptionMaxMultiplier", AbsorptionMaxMultiplier, 1f, 4f);

            BaseFoodNeedUnitsPerReal60Minutes = F(nutrition, "baseFoodNeedUnitsPerReal60Minutes", BaseFoodNeedUnitsPerReal60Minutes, 0f, 1000f);
            FoodRequirementMinMultiplier = F(nutrition, "requirementMinMultiplier", FoodRequirementMinMultiplier, 0.1f, 1f);
            FoodRequirementMaxMultiplier = F(nutrition, "requirementMaxMultiplier", FoodRequirementMaxMultiplier, 1f, 4f);

            EnergyMax = F(energy, "max", EnergyMax, 10f, 500f);
            EnergyLightUsePerRealMinute = F(energy, "lightUsePerRealMinute", EnergyLightUsePerRealMinute, 0f, 20f);
            EnergyModerateUsePerRealMinute = F(energy, "moderateUsePerRealMinute", EnergyModerateUsePerRealMinute, EnergyLightUsePerRealMinute, 20f);
            EnergyHighUsePerRealMinute = F(energy, "highUsePerRealMinute", EnergyHighUsePerRealMinute, EnergyModerateUsePerRealMinute, 20f);
            EnergyExtremeUsePerRealMinute = F(energy, "extremeUsePerRealMinute", EnergyExtremeUsePerRealMinute, EnergyHighUsePerRealMinute, 20f);
            EnergyRecoveryPerRealMinute = F(energy, "recoveryPerRealMinute", EnergyRecoveryPerRealMinute, 0f, 20f);
            EnergyPerStaminaSpent = F(energy, "energyPerStaminaSpent", EnergyPerStaminaSpent, 0f, 1f);
            FoodUnitsPerEnergyRecovered = F(energy, "foodUnitsPerEnergyRecovered", FoodUnitsPerEnergyRecovered, 0f, 5f);
            EnergyRecoveryFoodFloorPercent = F(energy, "recoveryFoodFloorPercent", EnergyRecoveryFoodFloorPercent, 0f, 1f);
            EnergyMinimumSustainableCap = F(energy, "minimumSustainableCap", EnergyMinimumSustainableCap, 0f, EnergyMax);
            EnergyHydrationFullEfficiencyPercent = F(energy, "hydrationFullEfficiencyPercent", EnergyHydrationFullEfficiencyPercent, 0.01f, 1f);
            EnergyHydrationMediumEfficiencyPercent = F(energy, "hydrationMediumEfficiencyPercent", EnergyHydrationMediumEfficiencyPercent, 0f, EnergyHydrationFullEfficiencyPercent);
            EnergyHydrationCriticalEfficiencyPercent = F(energy, "hydrationCriticalEfficiencyPercent", EnergyHydrationCriticalEfficiencyPercent, 0f, EnergyHydrationMediumEfficiencyPercent);
            EnergyHydrationEfficiencyAtMedium = F(energy, "hydrationEfficiencyAtMedium", EnergyHydrationEfficiencyAtMedium, 0f, 1f);
            EnergyHydrationEfficiencyAtCritical = F(energy, "hydrationEfficiencyAtCritical", EnergyHydrationEfficiencyAtCritical, 0f, EnergyHydrationEfficiencyAtMedium);
            EnergyHydrationEfficiencyAtZero = F(energy, "hydrationEfficiencyAtZero", EnergyHydrationEfficiencyAtZero, 0f, EnergyHydrationEfficiencyAtCritical);
            EnergyLowThreshold = F(energy, "lowThreshold", EnergyLowThreshold, 1f, EnergyMax);
            EnergyCriticalThreshold = F(energy, "criticalThreshold", EnergyCriticalThreshold, 0f, EnergyLowThreshold);
            StaminaRegenAtLowEnergy = F(energy, "staminaRegenAtLowEnergy", StaminaRegenAtLowEnergy, 0.05f, 1f);
            StaminaRegenAtCriticalEnergy = F(energy, "staminaRegenAtCriticalEnergy", StaminaRegenAtCriticalEnergy, 0.05f, StaminaRegenAtLowEnergy);
            StaminaRegenAtZeroEnergy = F(energy, "staminaRegenAtZeroEnergy", StaminaRegenAtZeroEnergy, 0.05f, StaminaRegenAtCriticalEnergy);

            BaseDigestiveHealth = F(digestion, "baseDigestiveHealth", BaseDigestiveHealth, 0f, 100f);
            BaseSolidGastricEmptyingMlPerRealMinute = F(digestion, "baseSolidGastricEmptyingMlPerRealMinute", BaseSolidGastricEmptyingMlPerRealMinute, 1f, 1000f);
            BaseIntestinalNutrientAbsorptionUnitsPerRealMinute = F(digestion, "baseIntestinalNutrientAbsorptionUnitsPerRealMinute", BaseIntestinalNutrientAbsorptionUnitsPerRealMinute, 0.1f, 100f);
            IntestinalNutritionResidenceRealSeconds = F(digestion, "intestinalNutritionResidenceRealSeconds", IntestinalNutritionResidenceRealSeconds, 0f, 3600f);
            LightFoodGastricLagRealSeconds = F(digestion, "lightFoodGastricLagRealSeconds", LightFoodGastricLagRealSeconds, 0f, 3600f);
            NormalFoodGastricLagRealSeconds = F(digestion, "normalFoodGastricLagRealSeconds", NormalFoodGastricLagRealSeconds, 0f, 3600f);
            DenseFoodGastricLagRealSeconds = F(digestion, "denseFoodGastricLagRealSeconds", DenseFoodGastricLagRealSeconds, 0f, 3600f);
            HeavyFoodGastricLagRealSeconds = F(digestion, "heavyFoodGastricLagRealSeconds", HeavyFoodGastricLagRealSeconds, 0f, 3600f);
            DigestionSpeedMinMultiplier = F(digestion, "digestionSpeedMinMultiplier", DigestionSpeedMinMultiplier, 0.1f, 1f);
            DigestionSpeedMaxMultiplier = F(digestion, "digestionSpeedMaxMultiplier", DigestionSpeedMaxMultiplier, 1f, 4f);
            GutResilienceMinMultiplier = F(digestion, "gutResilienceMinMultiplier", GutResilienceMinMultiplier, 0.1f, 1f);
            GutResilienceMaxMultiplier = F(digestion, "gutResilienceMaxMultiplier", GutResilienceMaxMultiplier, 1f, 4f);

            DigestiveEnzymesRealSeconds = F(supplements, "digestiveEnzymesRealSeconds", DigestiveEnzymesRealSeconds, 0f, 86400f);
            ElectrolytesRealSeconds = F(supplements, "electrolytesRealSeconds", ElectrolytesRealSeconds, 0f, 86400f);
            ProbioticsRealSeconds = F(supplements, "probioticsRealSeconds", ProbioticsRealSeconds, 0f, 172800f);
            PrebioticRealSeconds = F(supplements, "prebioticRealSeconds", PrebioticRealSeconds, 0f, 172800f);

            loaded = true;
            { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Metabolism] Config loaded. hydration/60real=" + BaseFluidNeedMlPerReal60Minutes.ToString("0", CultureInfo.InvariantCulture)
                + "mL nutrition/60real=" + BaseFoodNeedUnitsPerReal60Minutes.ToString("0.#", CultureInfo.InvariantCulture)
                + " energyRecovery/min=" + EnergyRecoveryPerRealMinute.ToString("0.##", CultureInfo.InvariantCulture)
                + " stomach=" + BaseStomachCapacityMl.ToString("0", CultureInfo.InvariantCulture) + "mL"
                + " intestineRef=" + BaseIntestinalCapacityMl.ToString("0", CultureInfo.InvariantCulture) + "mL"
                + " gutResidence=" + IntestinalFluidResidenceRealSeconds.ToString("0", CultureInfo.InvariantCulture)
                + "/" + IntestinalNutritionResidenceRealSeconds.ToString("0", CultureInfo.InvariantCulture) + "s"); }
        }
        catch (Exception ex)
        {
            RestorePublishedConfig(prior);
            loaded = false;
            Log.Error("[REBIRTH Metabolism] Config load failed; previous complete configuration retained: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static Dictionary<FieldInfo, object> CapturePublishedConfig()
    {
        Dictionary<FieldInfo, object> result = new Dictionary<FieldInfo, object>();
        FieldInfo[] fields = typeof(RebirthMetabolismConfig).GetFields(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < fields.Length; i++)
            if (!fields[i].IsInitOnly) result[fields[i]] = fields[i].GetValue(null);
        return result;
    }

    private static void RestorePublishedConfig(Dictionary<FieldInfo, object> snapshot)
    {
        if (snapshot == null) return;
        foreach (KeyValuePair<FieldInfo, object> pair in snapshot)
            pair.Key.SetValue(null, pair.Value);
    }

    private static void ValidateFiniteNumericAttributes(XElement element, string section)
    {
        if (element == null) return;
        foreach (XAttribute attribute in element.Attributes())
        {
            float value;
            if (!float.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidDataException("invalid finite metabolism config value " + section + "." + attribute.Name.LocalName);
        }
    }

    public static float ClampRequirement(float value) { return Mathf.Clamp(value, RequirementMinMultiplier, RequirementMaxMultiplier); }
    public static float ClampFoodRequirement(float value) { return Mathf.Clamp(value, FoodRequirementMinMultiplier, FoodRequirementMaxMultiplier); }
    public static float ClampUtilization(float value) { return Mathf.Clamp(value, UtilizationMinMultiplier, UtilizationMaxMultiplier); }
    public static float ClampAbsorption(float value) { return Mathf.Clamp(value, AbsorptionMinMultiplier, AbsorptionMaxMultiplier); }
    public static float ClampDigestion(float value) { return Mathf.Clamp(value, DigestionSpeedMinMultiplier, DigestionSpeedMaxMultiplier); }
    public static float ClampGutResilience(float value) { return Mathf.Clamp(value, GutResilienceMinMultiplier, GutResilienceMaxMultiplier); }

    public static float GetFoodGastricLagRealSeconds(string profile)
    {
        string p = (profile ?? string.Empty).Trim();
        if (p.Equals("liquidMeal", StringComparison.OrdinalIgnoreCase)) return MealFluidHoldRealSeconds;
        if (p.Equals("light", StringComparison.OrdinalIgnoreCase)) return LightFoodGastricLagRealSeconds;
        if (p.Equals("dense", StringComparison.OrdinalIgnoreCase)) return DenseFoodGastricLagRealSeconds;
        if (p.Equals("heavy", StringComparison.OrdinalIgnoreCase)) return HeavyFoodGastricLagRealSeconds;
        return NormalFoodGastricLagRealSeconds;
    }

    public static float GetFluidGastricHalfTimeRealSeconds(bool mealBound)
    {
        return mealBound ? MealFluidGastricHalfTimeRealSeconds : ClearLiquidGastricHalfTimeRealSeconds;
    }

    public static float GetDigestionProfileMultiplier(string profile)
    {
        string p = (profile ?? string.Empty).Trim();
        if (p.Equals("liquidMeal", StringComparison.OrdinalIgnoreCase)) return 1.40f;
        if (p.Equals("light", StringComparison.OrdinalIgnoreCase)) return 1.20f;
        if (p.Equals("dense", StringComparison.OrdinalIgnoreCase)) return 0.80f;
        if (p.Equals("heavy", StringComparison.OrdinalIgnoreCase)) return 0.60f;
        return 1.00f;
    }

    private static float F(XElement e, string name, float fallback, float min, float max)
    {
        if (e == null) return fallback;
        XAttribute a = e.Attribute(name);
        float v;
        if (a == null || !float.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return fallback;
        if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException("non-finite metabolism config value " + name);
        return Mathf.Clamp(v, min, max);
    }

    private static int I(XElement e, string name, int fallback, int min, int max)
    {
        if (e == null) return fallback;
        XAttribute a = e.Attribute(name);
        int v;
        if (a == null || !int.TryParse(a.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return fallback;
        return Math.Max(min, Math.Min(max, v));
    }
}
