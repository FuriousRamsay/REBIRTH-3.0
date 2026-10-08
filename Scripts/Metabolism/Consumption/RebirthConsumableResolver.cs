using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#nullable disable

public static class RebirthConsumableResolver
{
    private static readonly Dictionary<int, RebirthConsumableDefinition> Cache = new Dictionary<int, RebirthConsumableDefinition>();

    public static void ClearCache()
    {
        Cache.Clear();
        RebirthCookingBatch.ClearBatchDefinitions();
    }

    public static bool TryResolve(ItemValue itemValue, out RebirthConsumableDefinition definition)
    {
        definition = null;
        if (itemValue == null || itemValue.IsEmpty() || itemValue.ItemClass == null)
            return false;
        bool resolved = TryResolve(itemValue.ItemClass, out definition);
        if (resolved) RebirthCookingBatch.Apply(itemValue, ref definition);
        return resolved;
    }

    public static bool TryResolve(ItemClass itemClass, out RebirthConsumableDefinition definition)
    {
        definition = null;
        if (itemClass == null)
            return false;

        if (Cache.TryGetValue(itemClass.Id, out definition))
            return definition != null && definition.IsMetabolismItem;

        definition = Build(itemClass);
        Cache[itemClass.Id] = definition;
        return definition != null && definition.IsMetabolismItem;
    }

    public static RebirthConsumableDefinition Build(ItemClass itemClass)
    {
        if (itemClass == null || itemClass.Properties == null)
            return null;

        string type = Get(itemClass, "RebirthMetabolismType", string.Empty).Trim();
        if (string.IsNullOrEmpty(type))
            return null;
        if (!type.Equals("Drink", StringComparison.OrdinalIgnoreCase) &&
            !type.Equals("Food", StringComparison.OrdinalIgnoreCase) &&
            !type.Equals("Mixed", StringComparison.OrdinalIgnoreCase) &&
            !type.Equals("Supplement", StringComparison.OrdinalIgnoreCase))
            return null;

        string invalidNumeric;
        if (!ValidateFiniteNonNegativeProperties(itemClass, out invalidNumeric))
        {
            Log.Error("[REBIRTH Metabolism] invalid consumable numeric property item=" + itemClass.GetItemName() + " property=" + invalidNumeric);
            return null;
        }

        RebirthConsumableDefinition d = new RebirthConsumableDefinition();
        d.ItemName = itemClass.GetItemName();
        d.IsMetabolismItem = true;
        d.IsDrink = type.Equals("Drink", StringComparison.OrdinalIgnoreCase);
        d.IsFood = type.Equals("Food", StringComparison.OrdinalIgnoreCase) || type.Equals("Mixed", StringComparison.OrdinalIgnoreCase);
        d.IsSupplement = type.Equals("Supplement", StringComparison.OrdinalIgnoreCase);
        d.Kind = d.IsDrink ? RebirthIngestionKind.Fluid : d.IsSupplement ? RebirthIngestionKind.Supplement : RebirthIngestionKind.Food;

        d.LiquidProfile = Get(itemClass, "RebirthLiquidProfile", d.IsDrink ? "cleanWater" : string.Empty);
        d.DigestionProfile = Get(itemClass, "RebirthDigestionProfile", d.IsFood ? "normal" : string.Empty);
        d.FoodSafetyProfile = Get(itemClass, "RebirthFoodSafetyProfile", "safe");
        d.SupplementProfile = Get(itemClass, "RebirthSupplementProfile", string.Empty);
        d.ContainerCapacityMl = GetFloat(itemClass, "RebirthContainerCapacityMl", 0f);
        d.InitialVolumeMl = GetFloat(itemClass, "RebirthInitialVolumeMl", d.ContainerCapacityMl);
        d.ManualSipMl = GetFloat(itemClass, "RebirthManualSipMl", d.IsDrink ? 125f : 0f);
        d.AutoSipMl = GetFloat(itemClass, "RebirthAutoSipMl", d.IsDrink ? RebirthMetabolismConfig.AutoSipDefaultMl : 0f);
        d.HydrationEquippable = GetBool(itemClass, "RebirthHydrationEquippable", false);
        d.AutoSipSafe = GetBool(itemClass, "RebirthAutoSipSafe", d.HydrationEquippable);
        d.ReusableContainer = GetBool(itemClass, "RebirthReusableContainer", false);
        d.EmptyItem = Get(itemClass, "RebirthEmptyItem", string.Empty);
        d.AllowConsumeWhenHydrated = GetBool(itemClass, "RebirthAllowConsumeWhenHydrated", false);
        d.NutritionUnits = GetFloat(itemClass, "RebirthNutritionUnits", 0f);
        d.EnergyUnits = GetFloat(itemClass, "RebirthEnergyUnits", 0f);
        d.FoodWaterMl = GetFloat(itemClass, "RebirthFoodWaterMl", 0f);
        d.StomachVolumeMl = GetFloat(itemClass, "RebirthStomachVolumeMl", d.IsFood ? 200f : 0f);
        d.LiquidHydrationYield = GetFloat(itemClass, "RebirthLiquidHydrationYield", 1f);

        if (d.InitialVolumeMl > d.ContainerCapacityMl && d.ContainerCapacityMl > 0f)
            d.InitialVolumeMl = d.ContainerCapacityMl;
        if (d.InitialVolumeMl < 0f) d.InitialVolumeMl = 0f;
        if (d.ManualSipMl < 0f) d.ManualSipMl = 0f;
        if (d.AutoSipMl < 0f) d.AutoSipMl = 0f;
        if (d.StomachVolumeMl < 0f) d.StomachVolumeMl = 0f;
        if (d.FoodWaterMl < 0f) d.FoodWaterMl = 0f;
        if (d.NutritionUnits < 0f) d.NutritionUnits = 0f;
        if (d.EnergyUnits < 0f) d.EnergyUnits = 0f;
        if (d.LiquidHydrationYield < 0f) d.LiquidHydrationYield = 0f;
        return d;
    }


    public static bool HasMetabolismAuthoring(ItemClass itemClass)
    {
        return !string.IsNullOrWhiteSpace(Get(itemClass, "RebirthMetabolismType", string.Empty));
    }

    public static bool TryValidateAuthoring(ItemClass itemClass, out string error)
    {
        error = string.Empty;
        if (itemClass == null) { error = "item is null"; return false; }
        string type = Get(itemClass, "RebirthMetabolismType", string.Empty).Trim();
        if (string.IsNullOrEmpty(type)) { error = "metabolism type is missing"; return false; }
        if (!type.Equals("Drink", StringComparison.OrdinalIgnoreCase) &&
            !type.Equals("Food", StringComparison.OrdinalIgnoreCase) &&
            !type.Equals("Mixed", StringComparison.OrdinalIgnoreCase) &&
            !type.Equals("Supplement", StringComparison.OrdinalIgnoreCase))
        {
            error = "unsupported metabolism type '" + type + "'";
            return false;
        }
        string invalidKey;
        if (!ValidateFiniteNonNegativeProperties(itemClass, out invalidKey))
        {
            error = "invalid finite non-negative numeric property " + invalidKey;
            return false;
        }
        return true;
    }

    private static readonly string[] NumericPropertyKeys =
    {
        "RebirthContainerCapacityMl", "RebirthInitialVolumeMl", "RebirthManualSipMl", "RebirthAutoSipMl",
        "RebirthNutritionUnits", "RebirthEnergyUnits", "RebirthFoodWaterMl", "RebirthStomachVolumeMl",
        "RebirthLiquidHydrationYield", "RebirthHydrationCostMl"
    };

    private static bool ValidateFiniteNonNegativeProperties(ItemClass itemClass, out string invalidKey)
    {
        invalidKey = string.Empty;
        string[] keys = NumericPropertyKeys;
        for (int i = 0; i < keys.Length; i++)
        {
            string raw = Get(itemClass, keys[i], null);
            if (raw == null) continue;
            float value;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                invalidKey = keys[i];
                return false;
            }
        }
        return true;
    }

    public static string Get(ItemClass itemClass, string key, string fallback)
    {
        if (itemClass == null || itemClass.Properties == null || itemClass.Properties.Values == null)
            return fallback;
        string value;
        return itemClass.Properties.Values.TryGetValue(key, out value) ? value : fallback;
    }

    public static float GetFloat(ItemClass itemClass, string key, float fallback)
    {
        string raw = Get(itemClass, key, null);
        float value;
        return raw != null && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    public static bool GetBool(ItemClass itemClass, string key, bool fallback)
    {
        string raw = Get(itemClass, key, null);
        if (raw == null)
            return fallback;
        bool value;
        if (bool.TryParse(raw, out value))
            return value;
        if (raw == "1") return true;
        if (raw == "0") return false;
        return fallback;
    }
}

public static class RebirthLiquidContainerService
{
    public const string VolumeKey = "RebirthMetLiquidVolumeMl";
    public const string ProfileKey = "RebirthMetLiquidProfile";
    public const string VersionKey = "RebirthMetContainerVersion";

    public static float GetRemainingMl(ItemValue itemValue, RebirthConsumableDefinition definition)
    {
        if (itemValue == null || definition == null)
            return 0f;

        float capacity = Mathf.Max(0f, definition.ContainerCapacityMl);
        float value;
        if (itemValue.TryGetMetadata(VolumeKey, out value) && !float.IsNaN(value) && !float.IsInfinity(value))
            return Mathf.Clamp(value, 0f, capacity);

        // Invalid/missing metadata is observed as legacy state, not trusted. Reconstruct from
        // degradation/authoring without mutating; NormalizeState is the explicit repair boundary.
        // Observation only: legacy degradation can reconstruct volume without mutating the item.
        if (capacity > 0f && itemValue.UseTimes > 0.001f)
            return Mathf.Clamp(capacity - itemValue.UseTimes, 0f, capacity);

        return Mathf.Clamp(definition.InitialVolumeMl, 0f, capacity);
    }

    public static bool NormalizeState(ItemValue itemValue, RebirthConsumableDefinition definition, out float remainingMl)
    {
        remainingMl = GetRemainingMl(itemValue, definition);
        if (itemValue == null || definition == null || !definition.IsDrink)
            return false;
        bool changed = false;
        float stored;
        if (!itemValue.TryGetMetadata(VolumeKey, out stored) || Mathf.Abs(stored - remainingMl) > 0.01f)
        {
            itemValue.SetMetadata(VolumeKey, remainingMl);
            changed = true;
        }
        string profile;
        string expectedProfile = definition.LiquidProfile ?? string.Empty;
        if (!itemValue.TryGetMetadata(ProfileKey, out profile) || !string.Equals(profile, expectedProfile, StringComparison.Ordinal))
        {
            itemValue.SetMetadata(ProfileKey, expectedProfile);
            changed = true;
        }
        int version;
        if (!itemValue.TryGetMetadata(VersionKey, out version) || version != 2)
        {
            itemValue.SetMetadata(VersionKey, 2);
            changed = true;
        }
        if (SynchronizeDegradation(itemValue, definition, remainingMl))
            changed = true;
        return changed;
    }

    public static string GetLiquidProfile(ItemValue itemValue, RebirthConsumableDefinition definition)
    {
        string value;
        if (itemValue != null && itemValue.TryGetMetadata(ProfileKey, out value) && !string.IsNullOrEmpty(value))
            return value;
        return definition != null ? definition.LiquidProfile ?? string.Empty : string.Empty;
    }

    public static void SetRemainingMl(ItemValue itemValue, RebirthConsumableDefinition definition, float ml)
    {
        if (itemValue == null || definition == null)
            return;
        float max = Mathf.Max(0f, definition.ContainerCapacityMl);
        float remaining = Mathf.Clamp(ml, 0f, max);
        itemValue.SetMetadata(VolumeKey, remaining);
        itemValue.SetMetadata(ProfileKey, definition.LiquidProfile ?? string.Empty);
        itemValue.SetMetadata(VersionKey, 2);
        SynchronizeDegradation(itemValue, definition, remaining);
    }

    /// <summary>
    /// Mirrors liquid volume into native degradation state so every standard item-slot
    /// durability renderer can represent bottle fullness.  UseTimes is the amount
    /// consumed; DegradationMax is authored to equal container capacity in mL.
    /// Metadata remains authoritative so older saves and non-visual systems stay safe.
    /// </summary>
    public static bool SynchronizeDegradation(ItemValue itemValue, RebirthConsumableDefinition definition, float remainingMl)
    {
        if (itemValue == null || definition == null || !definition.IsDrink)
            return false;

        float capacity = Mathf.Max(0f, definition.ContainerCapacityMl);
        if (capacity <= 0f)
            return false;

        float desiredUseTimes = Mathf.Clamp(capacity - remainingMl, 0f, capacity);
        if (Mathf.Abs(itemValue.UseTimes - desiredUseTimes) <= 0.01f)
            return false;

        itemValue.UseTimes = desiredUseTimes;
        return true;
    }

    public static float GetFill01(ItemValue itemValue, RebirthConsumableDefinition definition)
    {
        if (definition == null || definition.ContainerCapacityMl <= 0f)
            return 0f;
        return Mathf.Clamp01(GetRemainingMl(itemValue, definition) / definition.ContainerCapacityMl);
    }

    public static string FormatVolume(float ml)
    {
        ml = Mathf.Max(0f, ml);
        if (ml >= 1000f)
            return (ml / 1000f).ToString("0.##", CultureInfo.InvariantCulture) + " L";
        return Mathf.RoundToInt(ml).ToString(CultureInfo.InvariantCulture) + " mL";
    }

    public static bool IsPartial(ItemValue itemValue, RebirthConsumableDefinition definition)
    {
        if (itemValue == null || definition == null || !definition.IsDrink)
            return false;
        float remaining = GetRemainingMl(itemValue, definition);
        return remaining > 0.01f && remaining < definition.ContainerCapacityMl - 0.01f;
    }
}
