using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthVehicleSourceCategory : byte
{
    Repairable = 0,
    Wreck = 1,
    Lootable = 2,
    Car = 3,
    Truck = 4,
    Motorcycle = 5,
    Aircraft = 6
}

public sealed class RebirthVehiclePartDefinition
{
    public readonly string PartId;
    public readonly string ItemName;
    public readonly string[] SlotIds;
    public readonly string[] FamilyIds;
    public readonly string PerformanceRoleId;
    public readonly string DisplayKey;
    public readonly string IconProperty;

    public RebirthVehiclePartDefinition(string partId, string itemName, string[] slotIds, string[] familyIds, string roleId)
    {
        PartId = partId;
        ItemName = itemName;
        SlotIds = slotIds ?? new string[0];
        FamilyIds = familyIds ?? new string[0];
        PerformanceRoleId = roleId ?? "General";
        DisplayKey = itemName;
        IconProperty = "CustomIcon";
    }
}

public static class RebirthVehiclePartDefinitionRegistry
{
    private static readonly Dictionary<string, RebirthVehiclePartDefinition> ByItem = new Dictionary<string, RebirthVehiclePartDefinition>(StringComparer.Ordinal);
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        Dictionary<string, HashSet<string>> slots = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> families = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (RebirthVehicleFamilyDefinition family in RebirthVehicleDefinitionRegistry.GetFamiliesSnapshot())
        {
            for (int i = 0; i < family.Slots.Length; i++)
            {
                RebirthVehicleSlotDefinition slot = family.Slots[i];
                HashSet<string> slotSet;
                if (!slots.TryGetValue(slot.ItemName, out slotSet)) slots[slot.ItemName] = slotSet = new HashSet<string>(StringComparer.Ordinal);
                slotSet.Add(slot.SlotId);
                HashSet<string> familySet;
                if (!families.TryGetValue(slot.ItemName, out familySet)) families[slot.ItemName] = familySet = new HashSet<string>(StringComparer.Ordinal);
                familySet.Add(family.FamilyId);
            }
        }
        foreach (KeyValuePair<string, HashSet<string>> pair in slots)
        {
            string item = pair.Key;
            string role = InferRole(item);
            ByItem[item] = new RebirthVehiclePartDefinition(item, item, ToArray(pair.Value), ToArray(families[item]), role);
        }
    }

    private static string InferRole(string item)
    {
        string value = item != null ? item.ToLowerInvariant() : string.Empty;
        if (value.Contains("wheel")) return "Handling";
        if (value.Contains("engine")) return "Power";
        if (value.Contains("transmission")) return "Transmission";
        if (value.Contains("radiator")) return "Cooling";
        if (value.Contains("battery") || value.Contains("alternator") || value.Contains("starter") || value.Contains("spark")) return "Electrical";
        if (value.Contains("fuel") || value.Contains("carburetor")) return "Fuel";
        if (value.Contains("rotor")) return "Lift";
        if (value.Contains("headlight")) return "Lighting";
        return "General";
    }

    private static string[] ToArray(HashSet<string> values)
    {
        string[] result = new string[values.Count];
        values.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    public static RebirthVehiclePartDefinition GetByItem(string itemName)
    {
        Initialize();
        RebirthVehiclePartDefinition value;
        return itemName != null && ByItem.TryGetValue(itemName, out value) ? value : null;
    }

    public static RebirthVehiclePartDefinition[] Snapshot()
    {
        Initialize();
        RebirthVehiclePartDefinition[] result = new RebirthVehiclePartDefinition[ByItem.Count];
        ByItem.Values.CopyTo(result, 0);
        return result;
    }

    public static bool IsCompatible(string itemName, string familyId, string slotId)
    {
        RebirthVehiclePartDefinition part = GetByItem(itemName);
        if (part == null) return false;
        return Array.IndexOf(part.FamilyIds, familyId) >= 0 && Array.IndexOf(part.SlotIds, slotId) >= 0;
    }

    public static string ValidateRuntimeBindings()
    {
        Initialize();
        List<string> errors = new List<string>();
        foreach (RebirthVehiclePartDefinition part in ByItem.Values)
        {
            bool runtimeEnabled = false;
            for (int i = 0; i < part.FamilyIds.Length; i++)
                if (RebirthVehicleDefinitionRegistry.IsFamilyRuntimeEnabled(part.FamilyIds[i]))
                {
                    runtimeEnabled = true;
                    break;
                }
            if (!runtimeEnabled) continue;

            if (ItemClass.GetItemClass(part.ItemName, false) == null) errors.Add("missing item " + part.ItemName);
            if (part.FamilyIds.Length == 0) errors.Add("part has no family " + part.PartId);
            if (part.SlotIds.Length == 0) errors.Add("part has no slots " + part.PartId);
        }
        return errors.Count == 0 ? "vehicle part definitions valid count=" + ByItem.Count : "vehicle part definition errors: " + string.Join("; ", errors.ToArray());
    }
}

public sealed class RebirthVehicleSourceProfile
{
    public readonly string ProfileId;
    public readonly RebirthVehicleSourceCategory Category;
    public readonly string[] FamilyIds;
    public readonly string[] EligiblePartItems;
    public readonly float DefaultPartChance;
    public readonly int MinimumCount;
    public readonly int MaximumCount;
    public readonly int MinimumQuality;
    public readonly int MaximumQuality;
    public readonly string AcquisitionEvent;
    public readonly string RequiredTag;
    public readonly string ToolCategory;
    public readonly bool ServerAuthoritative;

    public RebirthVehicleSourceProfile(string profileId, RebirthVehicleSourceCategory category, string[] familyIds, string[] eligibleParts, float chance, int minimumCount, int maximumCount, int minimumQuality, int maximumQuality, string acquisitionEvent, string requiredTag, string toolCategory)
    {
        ProfileId = profileId;
        Category = category;
        FamilyIds = familyIds ?? new string[0];
        EligiblePartItems = eligibleParts ?? new string[0];
        DefaultPartChance = chance;
        MinimumCount = minimumCount;
        MaximumCount = maximumCount;
        MinimumQuality = minimumQuality;
        MaximumQuality = maximumQuality;
        AcquisitionEvent = acquisitionEvent ?? "Destroy";
        RequiredTag = requiredTag ?? "salvageHarvest";
        ToolCategory = toolCategory ?? "Disassemble";
        ServerAuthoritative = true;
    }

    public string Describe()
    {
        return ProfileId + " category=" + Category + " chance=" + DefaultPartChance + " count=" + MinimumCount + "-" + MaximumCount + " quality=" + MinimumQuality + "-" + MaximumQuality + " families=" + string.Join(",", FamilyIds) + " parts=" + EligiblePartItems.Length + " event=" + AcquisitionEvent + " tool=" + ToolCategory + " server=" + ServerAuthoritative;
    }
}

public static class RebirthVehicleSourceProfileRegistry
{
    private static readonly Dictionary<string, RebirthVehicleSourceProfile> Profiles = new Dictionary<string, RebirthVehicleSourceProfile>(StringComparer.Ordinal);
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        RebirthVehiclePartDefinitionRegistry.Initialize();
        Add("repairable", RebirthVehicleSourceCategory.Repairable, 0.50f, 1, 1, 1, 6,
            "BicycleRepair", "MinibikeRepair", "MotorcycleRepair", "QuadRepair", "V6CarRepair", "V8CarRepair", "V6TruckRepair", "V8TruckRepair", "V8MilitaryTruckRepair", "6WheelerRepair", "6WheelerMilitaryRepair", "8WheelerRepair", "8WheelerMilitaryRepair", "RVRepair", "HeliRepair", "GyroRepair");
        Add("car-wreck", RebirthVehicleSourceCategory.Car, 0.10f, 1, 1, 1, 5, "V6CarRepair", "V8CarRepair");
        Add("truck-wreck", RebirthVehicleSourceCategory.Truck, 0.10f, 1, 1, 1, 5, "V6TruckRepair", "V8TruckRepair", "V8MilitaryTruckRepair", "6WheelerRepair", "6WheelerMilitaryRepair", "8WheelerRepair", "8WheelerMilitaryRepair", "RVRepair");
        Add("motorcycle-wreck", RebirthVehicleSourceCategory.Motorcycle, 0.10f, 1, 1, 1, 5, "BicycleRepair", "MinibikeRepair", "MotorcycleRepair", "QuadRepair");
        Add("aircraft-wreck", RebirthVehicleSourceCategory.Aircraft, 0.08f, 1, 1, 1, 5, "HeliRepair", "GyroRepair");
        Add("generic-lootable", RebirthVehicleSourceCategory.Lootable, 0.08f, 1, 1, 1, 4,
            "BicycleRepair", "MinibikeRepair", "MotorcycleRepair", "QuadRepair", "V6CarRepair", "V8CarRepair", "V6TruckRepair", "V8TruckRepair", "V8MilitaryTruckRepair", "6WheelerRepair", "6WheelerMilitaryRepair", "8WheelerRepair", "8WheelerMilitaryRepair", "RVRepair", "HeliRepair", "GyroRepair");
    }

    private static void Add(string id, RebirthVehicleSourceCategory category, float chance, int minimumCount, int maximumCount, int minimumQuality, int maximumQuality, params string[] families)
    {
        if (chance < 0f || chance > 1f) throw new ArgumentOutOfRangeException("chance");

        List<string> enabledFamilies = new List<string>();
        for (int i = 0; i < (families != null ? families.Length : 0); i++)
            if (RebirthVehicleDefinitionRegistry.IsFamilyRuntimeEnabled(families[i]))
                enabledFamilies.Add(families[i]);
        string[] activeFamilies = enabledFamilies.ToArray();
        if (activeFamilies.Length == 0) return;

        HashSet<string> parts = new HashSet<string>(StringComparer.Ordinal);
        RebirthVehiclePartDefinition[] definitions = RebirthVehiclePartDefinitionRegistry.Snapshot();
        for (int i = 0; i < definitions.Length; i++)
            for (int j = 0; j < definitions[i].FamilyIds.Length; j++)
                if (Array.IndexOf(activeFamilies, definitions[i].FamilyIds[j]) >= 0) { parts.Add(definitions[i].ItemName); break; }
        string[] eligible = new string[parts.Count];
        parts.CopyTo(eligible);
        Array.Sort(eligible, StringComparer.Ordinal);
        Profiles[id] = new RebirthVehicleSourceProfile(id, category, activeFamilies, eligible, chance, minimumCount, maximumCount, minimumQuality, maximumQuality, "Destroy", "salvageHarvest", "Disassemble");
    }

    public static RebirthVehicleSourceProfile[] Snapshot()
    {
        Initialize();
        RebirthVehicleSourceProfile[] values = new RebirthVehicleSourceProfile[Profiles.Count];
        Profiles.Values.CopyTo(values, 0);
        return values;
    }

    public static string Validate()
    {
        Initialize();
        List<string> errors = new List<string>();
        foreach (RebirthVehicleSourceProfile profile in Profiles.Values)
        {
            if (profile.DefaultPartChance < 0f || profile.DefaultPartChance > 1f) errors.Add(profile.ProfileId + " probability outside 0-1");
            if (profile.MinimumCount < 0 || profile.MaximumCount < profile.MinimumCount) errors.Add(profile.ProfileId + " invalid count range");
            if (profile.MinimumQuality < 1 || profile.MaximumQuality < profile.MinimumQuality) errors.Add(profile.ProfileId + " invalid quality range");
            if (profile.EligiblePartItems.Length == 0) errors.Add(profile.ProfileId + " has no eligible parts");
            for (int i = 0; i < profile.FamilyIds.Length; i++) if (RebirthVehicleDefinitionRegistry.GetFamily(profile.FamilyIds[i]) == null) errors.Add(profile.ProfileId + " missing family " + profile.FamilyIds[i]);
            for (int i = 0; i < profile.EligiblePartItems.Length; i++) if (RebirthVehiclePartDefinitionRegistry.GetByItem(profile.EligiblePartItems[i]) == null) errors.Add(profile.ProfileId + " unknown part " + profile.EligiblePartItems[i]);
        }
        return errors.Count == 0 ? "vehicle source profiles valid count=" + Profiles.Count : "vehicle source profile errors: " + string.Join("; ", errors.ToArray());
    }
}
