using Audio;
using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthVehicleContentAvailability : byte
{
    Enabled = 0,
    DisabledMissingEntity = 1,
    DisabledMissingBlock = 2,
    DisabledInvalidBinding = 3,
    DisabledByConfiguration = 4
}

public enum RebirthVehicleAssemblyCarrier : byte
{
    RepairableBlock = 0,
    VehicleEntity = 1
}

public enum RebirthVehicleAssemblyAction : byte
{
    Read = 0,
    Install = 1,
    Remove = 2,
    Hotwire = 3,
    Replace = 4,
    Siphon = 5,
    Pickup = 6,
    Repair = 7,
    Refuel = 8
}

public enum RebirthVehiclePartSourceLocation : byte
{
    None = 0,
    Backpack = 1,
    Toolbelt = 2
}

public sealed class RebirthVehicleSlotDefinition
{
    public readonly string SlotId;
    public readonly string ItemName;
    public readonly string DisplayKey;
    public readonly bool Required;
    public readonly float EffectScale;

    public RebirthVehicleSlotDefinition(string slotId, string itemName, string displayKey, bool required, float effectScale)
    {
        SlotId = slotId;
        ItemName = itemName;
        DisplayKey = displayKey;
        Required = required;
        EffectScale = effectScale;
    }
}

public sealed class RebirthVehicleFamilyDefinition
{
    public readonly string FamilyId;
    public readonly string DisplayKey;
    public readonly RebirthVehicleSlotDefinition[] Slots;

    public RebirthVehicleFamilyDefinition(string familyId, string displayKey, RebirthVehicleSlotDefinition[] slots)
    {
        FamilyId = familyId;
        DisplayKey = displayKey;
        Slots = slots ?? new RebirthVehicleSlotDefinition[0];
    }
}

public sealed class RebirthVehicleContentDefinition
{
    public readonly string ContentId;
    public readonly string FamilyId;
    public readonly string RepairableBlockName;
    public readonly string EntityClassName;
    public RebirthVehicleContentAvailability Availability;
    public string AvailabilityReason;

    public RebirthVehicleContentDefinition(string contentId, string familyId, string blockName, string entityClassName, RebirthVehicleContentAvailability availability)
    {
        ContentId = contentId;
        FamilyId = familyId;
        RepairableBlockName = blockName;
        EntityClassName = entityClassName;
        Availability = availability;
        AvailabilityReason = string.Empty;
    }
}

public sealed class RebirthInstalledVehiclePart
{
    public string SlotId;
    public string ItemName;
    public int Quality;
    public float UseTimes;
    public float Roll;
    public string SerializedItemValue;

    public RebirthInstalledVehiclePart Clone()
    {
        return new RebirthInstalledVehiclePart
        {
            SlotId = SlotId,
            ItemName = ItemName,
            Quality = Quality,
            UseTimes = UseTimes,
            Roll = Roll,
            SerializedItemValue = SerializedItemValue
        };
    }
}

public static class RebirthVehiclePartMetadata
{
    public const string RollKey = "RebirthVehiclePartRoll";
    private const int RollScale = 1000000;

    public static float ReadOrCreateRoll(ItemValue value, World world)
    {
        if (value == null) return 0f;
        int stored;
        if (value.TryGetMetadata(RollKey, out stored))
            return Mathf.Clamp01(stored / (float)RollScale);

        int quality = Mathf.Clamp((int)value.Quality, 1, 6);
        float min;
        float max;
        GetRollRangeForQuality(quality, out min, out max);
        // Preserve a valid 2.6 roll when importing its native ItemValue. Keeping
        // the old keys in the item codec alone would otherwise silently reroll it.
        int legacyVersion, legacyQuality;
        float legacyScore;
        if (value.TryGetMetadata("rb_vperf_ver", out legacyVersion) && legacyVersion == 1 &&
            value.TryGetMetadata("rb_vperf_q", out legacyQuality) && legacyQuality == quality &&
            value.TryGetMetadata("rb_vperf_score", out legacyScore) &&
            !float.IsNaN(legacyScore) && !float.IsInfinity(legacyScore) &&
            legacyScore >= min - .0001f && legacyScore <= max + .0001f)
        {
            WriteRoll(value, legacyScore);
            return Mathf.Clamp01(legacyScore);
        }
        float t = world != null ? world.GetGameRandom().RandomFloat : UnityEngine.Random.value;
        float roll = Mathf.Clamp01(Mathf.Lerp(min, max, t));
        value.SetMetadata(RollKey, Mathf.Clamp(Mathf.RoundToInt(roll * RollScale), 0, RollScale));
        return roll;
    }


    private static void GetRollRangeForQuality(int quality, out float min, out float max)
    {
        switch (Mathf.Clamp(quality, 1, 6))
        {
            case 2: min = 0.12f; max = 0.28f; return;
            case 3: min = 0.32f; max = 0.48f; return;
            case 4: min = 0.52f; max = 0.68f; return;
            case 5: min = 0.72f; max = 0.88f; return;
            case 6: min = 0.92f; max = 1.00f; return;
            default: min = 0f; max = 0f; return;
        }
    }

    public static void WriteRoll(ItemValue value, float roll)
    {
        if (value != null)
            value.SetMetadata(RollKey, Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(roll) * RollScale), 0, RollScale));
    }
}

public sealed class RebirthVehiclePerformanceSnapshot
{
    public long Revision;
    public bool CanOperate;
    public float AccelerationMultiplier = 1f;
    public float TorqueMultiplier = 1f;
    public float FuelUseMultiplier = 1f;
    public float TurboFuelUseMultiplier = 1f;
    public float VelocityMultiplier = 1f;
    public float TurboVelocityMultiplier = 1f;
    public float ReverseVelocityMultiplier = 1f;
    public float ResponseMultiplier = 1f;
    public float HillClimbMultiplier = 1f;
    public float HandlingMultiplier = 1f;
    public float BrakingMultiplier = 1f;
    public int Hash;
}

public sealed class RebirthVehicleBaseStats
{
    public float MotorTorqueForward, MotorTorqueBackward, MotorTorqueTurboForward, MotorTorqueTurboBackward;
    public float VelocityForward, VelocityBackward, VelocityTurboForward, VelocityTurboBackward;
    public float FuelKmPerLiter, FuelCapacity, BrakeTorque, SteerRate, SteerCenteringRate;
}

public static class RebirthVehicleBaseStatsRegistry
{
    public static RebirthVehicleBaseStats Get(string contentId)
    {
        switch (contentId)
        {
            case "BaseBicycle": return S(500f,180f,750f,180f,6f,4f,8.5f,4f,0f,0f,3000f,130f,90f);
            case "BaseMinibike": return S(400f,200f,560f,200f,7f,4f,9.2f,4f,.4f,40f,3000f,130f,90f);
            case "BaseMotorcycle": return S(1400f,500f,2100f,650f,9.8f,6f,14f,8f,.2f,120f,3000f,130f,90f);
            case "Camper80s":
            case "Base4x4": return S(3500f,1500f,4500f,2000f,10f,8f,14f,10f,.1f,400f,6000f,130f,90f);
            case "BaseGyrocopter": return S(1f,1f,2f,2f,9f,9f,15f,9f,.15f,80f,10f,130f,90f);
            default: return new RebirthVehicleBaseStats();
        }
    }
    private static RebirthVehicleBaseStats S(float mtf,float mtb,float mttf,float mttb,float vf,float vb,float vtf,float vtb,float fuel,float cap,float brake,float steer,float center)
    {
        return new RebirthVehicleBaseStats { MotorTorqueForward=mtf,MotorTorqueBackward=mtb,MotorTorqueTurboForward=mttf,MotorTorqueTurboBackward=mttb,VelocityForward=vf,VelocityBackward=vb,VelocityTurboForward=vtf,VelocityTurboBackward=vtb,FuelKmPerLiter=fuel,FuelCapacity=cap,BrakeTorque=brake,SteerRate=steer,SteerCenteringRate=center };
    }
}

public sealed class RebirthVehicleAssembly
{
    public const int CurrentSchemaVersion = 4;
    public Guid AssemblyId = Guid.Empty;
    public string ContentId = string.Empty;
    public string FamilyId = string.Empty;
    public int SchemaVersion = CurrentSchemaVersion;
    public long Revision;
    // Saved with the assembly, not only in the request journal. Empty for legacy saves.
    public Guid PendingInventoryTransferId = Guid.Empty;
    public Guid LastAppliedInventoryTransferId = Guid.Empty;
    public float FuelPercent = -1f;
    public readonly Dictionary<string, RebirthInstalledVehiclePart> Parts = new Dictionary<string, RebirthInstalledVehiclePart>(StringComparer.Ordinal);
    public RebirthVehiclePerformanceSnapshot Performance = new RebirthVehiclePerformanceSnapshot();

    public RebirthVehicleAssembly DeepClone()
    {
        RebirthVehicleAssembly clone = new RebirthVehicleAssembly
        {
            AssemblyId = AssemblyId,
            ContentId = ContentId,
            FamilyId = FamilyId,
            SchemaVersion = SchemaVersion,
            Revision = Revision,
            PendingInventoryTransferId = PendingInventoryTransferId,
            LastAppliedInventoryTransferId = LastAppliedInventoryTransferId,
            FuelPercent = FuelPercent
        };
        foreach (KeyValuePair<string, RebirthInstalledVehiclePart> pair in Parts)
            clone.Parts[pair.Key] = pair.Value != null ? pair.Value.Clone() : null;
        clone.Performance = RebirthVehiclePerformanceCompiler.Compile(clone);
        return clone;
    }
}

public static class RebirthVehicleDefinitionRegistry
{
    private static readonly Dictionary<string, RebirthVehicleFamilyDefinition> Families = new Dictionary<string, RebirthVehicleFamilyDefinition>(StringComparer.Ordinal);
    private static readonly Dictionary<string, RebirthVehicleContentDefinition> Contents = new Dictionary<string, RebirthVehicleContentDefinition>(StringComparer.Ordinal);
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        RegisterFamilies();
        RegisterContent();
        Validate();
    }

    public static RebirthVehicleFamilyDefinition GetFamily(string id)
    {
        Initialize();
        RebirthVehicleFamilyDefinition value;
        return id != null && Families.TryGetValue(id, out value) ? value : null;
    }

    public static RebirthVehicleContentDefinition GetContent(string id)
    {
        Initialize();
        RebirthVehicleContentDefinition value;
        return id != null && Contents.TryGetValue(id, out value) ? value : null;
    }

    public static RebirthVehicleContentDefinition GetContentForBlock(string blockName)
    {
        Initialize();
        if (string.IsNullOrEmpty(blockName)) return null;
        foreach (RebirthVehicleContentDefinition content in Contents.Values)
            if (string.Equals(content.RepairableBlockName, blockName, StringComparison.Ordinal)) return content;
        return null;
    }

    public static RebirthVehicleContentDefinition GetContentForEntity(string entityClassName)
    {
        Initialize();
        foreach (RebirthVehicleContentDefinition content in Contents.Values)
            if (string.Equals(content.EntityClassName, entityClassName, StringComparison.Ordinal)) return content;
        return null;
    }

    public static RebirthVehicleFamilyDefinition[] GetFamiliesSnapshot()
    {
        Initialize();
        RebirthVehicleFamilyDefinition[] values = new RebirthVehicleFamilyDefinition[Families.Count];
        Families.Values.CopyTo(values, 0);
        return values;
    }

    public static RebirthVehicleContentDefinition[] GetContentsSnapshot()
    {
        Initialize();
        RebirthVehicleContentDefinition[] values = new RebirthVehicleContentDefinition[Contents.Count];
        Contents.Values.CopyTo(values, 0);
        return values;
    }

    public static bool IsFamilyRuntimeEnabled(string familyId)
    {
        Initialize();
        if (string.IsNullOrEmpty(familyId)) return false;
        foreach (RebirthVehicleContentDefinition content in Contents.Values)
            if (content.Availability == RebirthVehicleContentAvailability.Enabled &&
                string.Equals(content.FamilyId, familyId, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static RebirthVehicleSlotDefinition S(string id, string item, float effect = 1f)
    {
        return new RebirthVehicleSlotDefinition(id, item, "xuiRebirthVehicleSlot" + id, true, effect);
    }

    private static RebirthVehicleSlotDefinition[] Wheels(string prefix, string item, int count)
    {
        RebirthVehicleSlotDefinition[] result = new RebirthVehicleSlotDefinition[count];
        for (int i = 0; i < count; i++) result[i] = S(prefix + (i + 1), item, 1f / Math.Max(1, count));
        return result;
    }

    private static RebirthVehicleSlotDefinition[] Join(params RebirthVehicleSlotDefinition[][] arrays)
    {
        int total = 0;
        for (int i = 0; i < arrays.Length; i++) total += arrays[i] != null ? arrays[i].Length : 0;
        RebirthVehicleSlotDefinition[] result = new RebirthVehicleSlotDefinition[total];
        int at = 0;
        for (int i = 0; i < arrays.Length; i++)
        {
            RebirthVehicleSlotDefinition[] array = arrays[i];
            if (array == null) continue;
            Array.Copy(array, 0, result, at, array.Length);
            at += array.Length;
        }
        return result;
    }

    private static RebirthVehicleSlotDefinition[] CarCore(string engine, string radiator, string wheel, int wheelCount)
    {
        return Join(new[]
        {
            S("HeadlightLeft", "resourceHeadlight"), S("HeadlightRight", "resourceHeadlight"),
            S("Battery", "carBattery"), S("SparkPlugs", "partSparkPlugs_FR"),
            S("Engine", engine), S("Starter", "partCarStarter_FR"),
            S("FuelPump", "partFuelPump_FR"), S("Radiator", radiator),
            S("Carburetor", "partCarburetor_FR"), S("Transmission", "partCarTransmission_FR"),
            S("Alternator", "partAlternator_FR")
        }, Wheels("Wheel", wheel, wheelCount));
    }

    private static void F(string id, string displayKey, RebirthVehicleSlotDefinition[] slots)
    {
        Families[id] = new RebirthVehicleFamilyDefinition(id, displayKey, slots);
    }

    private static void RegisterFamilies()
    {
        F("BicycleRepair", "xuiRebirthVehicleFamilyBicycle", Wheels("Wheel", "partBicycleWheel_FR", 2));
        F("MinibikeRepair", "xuiRebirthVehicleFamilyMinibike", Join(new[] { S("Headlight", "resourceHeadlight"), S("Battery", "carBattery"), S("Engine", "smallEngine") }, Wheels("Wheel", "partMinibikeWheel_FR", 2)));
        F("MotorcycleRepair", "xuiRebirthVehicleFamilyMotorcycle", Join(new[] { S("Headlight", "resourceHeadlight"), S("Battery", "carBattery"), S("SparkPlugs", "partSparkPlugs_FR"), S("Engine", "partMotorcycleEngine_FR") }, Wheels("Wheel", "partMotorcycleWheel_FR", 2)));
        F("QuadRepair", "xuiRebirthVehicleFamilyQuad", Join(new[] { S("HeadlightLeft", "resourceHeadlight"), S("HeadlightRight", "resourceHeadlight"), S("Battery", "carBattery"), S("SparkPlugs", "partSparkPlugs_FR"), S("Engine", "smallEngine") }, Wheels("Wheel", "partCarWheel_FR", 4)));
        F("V6CarRepair", "xuiRebirthVehicleFamilyV6Car", CarCore("partV6Engine_FR", "partCarRadiator_FR", "partCarWheel_FR", 4));
        F("V8CarRepair", "xuiRebirthVehicleFamilyV8Car", CarCore("partV8Engine_FR", "partCarRadiator_FR", "partCarWheel_FR", 4));
        F("V6TruckRepair", "xuiRebirthVehicleFamilyV6Truck", CarCore("partV6Engine_FR", "partTruckRadiator_FR", "partTruckWheel2_FR", 4));
        F("V8TruckRepair", "xuiRebirthVehicleFamilyV8Truck", CarCore("partV8Engine_FR", "partTruckRadiator_FR", "partTruckWheel2_FR", 4));
        F("V8MilitaryTruckRepair", "xuiRebirthVehicleFamilyMilitaryTruck", CarCore("partV8Engine_FR", "partTruckRadiator_FR", "partMilitaryTruckWheel_FR", 4));
        F("6WheelerRepair", "xuiRebirthVehicleFamilySixWheeler", CarCore("partV8Engine_FR", "partTruckRadiator_FR", "partHeavyWheel_FR", 6));
        F("6WheelerMilitaryRepair", "xuiRebirthVehicleFamilySixWheelerMilitary", CarCore("partV8Engine_FR", "partTruckRadiator_FR", "partMilitaryTruckWheel_FR", 6));
        F("8WheelerRepair", "xuiRebirthVehicleFamilyEightWheeler", CarCore("partV8Engine_FR", "partTruckRadiator_FR", "partHeavyWheel_FR", 8));
        F("8WheelerMilitaryRepair", "xuiRebirthVehicleFamilyEightWheelerMilitary", CarCore("partV8Engine_FR", "partTruckRadiator_FR", "partMilitaryTruckWheel_FR", 8));
        F("RVRepair", "xuiRebirthVehicleFamilyRV", CarCore("partV8Engine_FR", "partTruckRadiator_FR", "partTruckWheel2_FR", 4));
        F("HeliRepair", "xuiRebirthVehicleFamilyHelicopter", new[] { S("Battery", "carBattery"), S("SparkPlugs", "partSparkPlugs_FR"), S("Engine", "partVehicleHelicopterEngineRebirth"), S("Starter", "partCarStarter_FR"), S("FuelPump", "partFuelPump_FR"), S("Alternator", "partAlternator_FR"), S("RotorMain", "partVehicleHelicopterRotorRebirth"), S("RotorTail", "partVehicleHelicopterTailRotorRebirth"), S("Transmission", "partVehicleHelicopterTransmissionRebirth") });
        F("GyroRepair", "xuiRebirthVehicleFamilyGyrocopter", Join(new[] { S("Headlight", "resourceHeadlight"), S("SparkPlugs", "partSparkPlugs_FR"), S("Battery", "carBattery"), S("Engine", "smallEngine"), S("Starter", "partCarStarter_FR"), S("FuelPump", "partFuelPump_FR") }, Wheels("Wheel", "partGyroWheel_FR", 3)));
    }

    private static void C(string id, string family, string block, string entity, RebirthVehicleContentAvailability availability)
    {
        Contents[id] = new RebirthVehicleContentDefinition(id, family, block, entity, availability);
    }

    private static void RegisterContent()
    {
        C("BaseBicycle", "BicycleRepair", "repairableVehicleBicycleRebirth", "vehicleBicycle", RebirthVehicleContentAvailability.Enabled);
        C("BaseMinibike", "MinibikeRepair", "repairableVehicleMinibikeRebirth", "vehicleMinibike", RebirthVehicleContentAvailability.Enabled);
        C("BaseMotorcycle", "MotorcycleRepair", "repairableVehicleMotorcycleRebirth", "vehicleMotorcycle", RebirthVehicleContentAvailability.Enabled);
        C("Base4x4", "V8TruckRepair", "repairableVehicle4x4Rebirth", "vehicleTruck4x4", RebirthVehicleContentAvailability.Enabled);
        C("Camper80s", "V8TruckRepair", string.Empty, "rebirthCamper80s", RebirthVehicleContentAvailability.Enabled);
        C("BaseGyrocopter", "GyroRepair", "repairableVehicleGyrocopterRebirth", "vehicleGyrocopter", RebirthVehicleContentAvailability.Enabled);
        C("FutureQuad", "QuadRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
        C("FutureV6Car", "V6CarRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
        C("FutureV8Car", "V8CarRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
        C("FutureV6Truck", "V6TruckRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
        C("FutureMilitaryTruck", "V8MilitaryTruckRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
        C("FutureSixWheeler", "6WheelerRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
        C("FutureSixWheelerMilitary", "6WheelerMilitaryRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
        C("FutureEightWheeler", "8WheelerRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
        C("FutureEightWheelerMilitary", "8WheelerMilitaryRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
        C("FutureRV", "RVRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
        C("FutureHelicopter", "HeliRepair", string.Empty, string.Empty, RebirthVehicleContentAvailability.DisabledMissingEntity);
    }

    private static void Validate()
    {
        foreach (RebirthVehicleFamilyDefinition family in Families.Values)
        {
            HashSet<string> slots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < family.Slots.Length; i++)
            {
                RebirthVehicleSlotDefinition slot = family.Slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.SlotId) || string.IsNullOrEmpty(slot.ItemName))
                    Log.Error("[REBIRTH Vehicle] invalid slot definition family=" + family.FamilyId + " index=" + i);
                else if (!slots.Add(slot.SlotId))
                    Log.Error("[REBIRTH Vehicle] duplicate slot id family=" + family.FamilyId + " slot=" + slot.SlotId);
            }
        }

        foreach (RebirthVehicleContentDefinition content in Contents.Values)
        {
            if (!Families.ContainsKey(content.FamilyId))
            {
                content.Availability = RebirthVehicleContentAvailability.DisabledInvalidBinding;
                content.AvailabilityReason = "missing family " + content.FamilyId;
                continue;
            }
            if (content.Availability != RebirthVehicleContentAvailability.Enabled) continue;
            if (string.IsNullOrEmpty(content.RepairableBlockName) && content.ContentId != "Camper80s")
            {
                content.Availability = RebirthVehicleContentAvailability.DisabledMissingBlock;
                content.AvailabilityReason = "missing repairable block";
                continue;
            }
            if (string.IsNullOrEmpty(content.EntityClassName))
            {
                content.Availability = RebirthVehicleContentAvailability.DisabledMissingEntity;
                content.AvailabilityReason = "missing entity";
            }
        }
    }

    public static string ValidateRuntimeBindings()
    {
        Initialize();
        StringBuilder result = new StringBuilder();
        int errors = 0;
        foreach (RebirthVehicleFamilyDefinition family in Families.Values)
        {
            if (!IsFamilyRuntimeEnabled(family.FamilyId)) continue;
            for (int i = 0; i < family.Slots.Length; i++)
            {
                RebirthVehicleSlotDefinition slot = family.Slots[i];
                if (slot == null || ItemClass.GetItemClass(slot.ItemName, false) == null)
                {
                    errors++;
                    result.AppendLine("missing item family=" + family.FamilyId + " slot=" + (slot != null ? slot.SlotId : "<null>") + " item=" + (slot != null ? slot.ItemName : "<null>"));
                }
            }
        }
        foreach (RebirthVehicleContentDefinition content in Contents.Values)
        {
            if (content.Availability != RebirthVehicleContentAvailability.Enabled) continue;
            bool entityOnly = content.ContentId == "Camper80s";
            Block block = entityOnly ? null : Block.GetBlockByName(content.RepairableBlockName, false);
            if (!entityOnly && block == null)
            {
                errors++;
                content.Availability = RebirthVehicleContentAvailability.DisabledMissingBlock;
                content.AvailabilityReason = "runtime block missing";
                result.AppendLine("missing block content=" + content.ContentId + " block=" + content.RepairableBlockName);
                continue;
            }

            string blockEntityClass = entityOnly ? content.EntityClassName : block.Properties.Values.ContainsKey("vehicle_entity_class")
                ? block.Properties.Values["vehicle_entity_class"]
                : string.Empty;
            if (string.IsNullOrEmpty(blockEntityClass) ||
                !string.Equals(blockEntityClass, content.EntityClassName, StringComparison.Ordinal))
            {
                errors++;
                content.Availability = RebirthVehicleContentAvailability.DisabledInvalidBinding;
                content.AvailabilityReason = "repairable block entity binding mismatch";
                result.AppendLine("invalid block entity binding content=" + content.ContentId +
                    " block=" + content.RepairableBlockName +
                    " xmlEntity=" + (string.IsNullOrEmpty(blockEntityClass) ? "<missing>" : blockEntityClass) +
                    " registeredEntity=" + content.EntityClassName);
                continue;
            }

            int entityId = EntityClass.GetId(blockEntityClass);
            EntityClass entityDefinition = entityId != -1 ? EntityClass.GetEntityClass(entityId) : null;
            if (entityDefinition == null)
            {
                errors++;
                content.Availability = RebirthVehicleContentAvailability.DisabledMissingEntity;
                content.AvailabilityReason = "runtime entity missing";
                result.AppendLine("missing entity content=" + content.ContentId + " entity=" + blockEntityClass);
                continue;
            }
            if (entityDefinition.classname == null || !typeof(EntityVehicle).IsAssignableFrom(entityDefinition.classname))
            {
                errors++;
                content.Availability = RebirthVehicleContentAvailability.DisabledInvalidBinding;
                string configuredClass = entityDefinition.Properties.Values.ContainsKey(EntityClass.PropClass)
                    ? entityDefinition.Properties.Values[EntityClass.PropClass]
                    : "<missing Class property>";
                content.AvailabilityReason = "invalid runtime class " + configuredClass;
                result.AppendLine("invalid entity class content=" + content.ContentId +
                    " entity=" + blockEntityClass +
                    " id=" + entityId +
                    " class=" + configuredClass);
            }
        }
        return "runtimeValidation errors=" + errors + (result.Length > 0 ? Environment.NewLine + result.ToString().TrimEnd() : string.Empty);
    }
}

public static class RebirthVehiclePerformanceCompiler
{
    private static bool IsLightEngineFamily(string familyId)
    {
        return string.Equals(familyId, "MinibikeRepair", StringComparison.Ordinal) ||
               string.Equals(familyId, "MotorcycleRepair", StringComparison.Ordinal) ||
               string.Equals(familyId, "QuadRepair", StringComparison.Ordinal) ||
               string.Equals(familyId, "GyroRepair", StringComparison.Ordinal);
    }

    public static RebirthVehiclePerformanceSnapshot Compile(RebirthVehicleAssembly assembly)
    {
        RebirthVehiclePerformanceSnapshot result = new RebirthVehiclePerformanceSnapshot();
        if (assembly == null) return result;
        result.Revision = assembly.Revision;
        RebirthVehicleFamilyDefinition family = RebirthVehicleDefinitionRegistry.GetFamily(assembly.FamilyId);
        if (family == null) return result;
        result.CanOperate = RebirthVehicleConditionIntegration.IsOperational(assembly);
        float wheelScore = 0f;
        int wheels = 0;
        for (int i = 0; i < family.Slots.Length; i++)
        {
            RebirthVehicleSlotDefinition slot = family.Slots[i];
            string id = slot.SlotId;
            bool wheel = id.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) >= 0;
            if (wheel) wheels++;
            RebirthInstalledVehiclePart part;
            if (!assembly.Parts.TryGetValue(slot.SlotId, out part) || part == null) continue;
            float score = string.Equals(slot.ItemName, part.ItemName, StringComparison.Ordinal) &&
                !float.IsNaN(part.Roll) && !float.IsInfinity(part.Roll) &&
                RebirthVehicleConditionIntegration.IsPartUsable(part) ? Mathf.Clamp01(part.Roll) : 0f;
            if (wheel) { wheelScore += score; continue; }
            if (id == "Engine")
            {
                result.AccelerationMultiplier += score * 2f;
                if (IsLightEngineFamily(family.FamilyId))
                {
                    result.VelocityMultiplier += score * 0.4f;
                    result.TurboVelocityMultiplier += score * 0.5f;
                    result.HillClimbMultiplier += score * 0.5f;
                }
            }
            else if (id == "SparkPlugs") result.TorqueMultiplier += score * 1.2f;
            else if (id == "FuelPump") result.FuelUseMultiplier -= score * 0.5f;
            else if (id == "Carburetor") result.TurboFuelUseMultiplier -= score * 0.6f;
            else if (id == "Transmission") result.VelocityMultiplier += score * 0.4f;
            else if (id == "Alternator") result.TurboVelocityMultiplier += score * 0.5f;
            else if (id == "Battery") result.ReverseVelocityMultiplier += score;
            else if (id == "Starter") result.ResponseMultiplier += score * 1.5f;
            else if (id == "Radiator") result.HillClimbMultiplier += score * 0.5f;
        }
        if (wheels > 0)
        {
            float average = wheelScore / wheels;
            result.HandlingMultiplier += average;
            result.BrakingMultiplier += average * 2f;
        }
        result.FuelUseMultiplier = Mathf.Max(0.1f, result.FuelUseMultiplier);
        result.TurboFuelUseMultiplier = Mathf.Max(0.1f, result.TurboFuelUseMultiplier);
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + assembly.AssemblyId.GetHashCode();
            hash = hash * 31 + assembly.Revision.GetHashCode();
            hash = hash * 31 + result.AccelerationMultiplier.GetHashCode();
            hash = hash * 31 + result.VelocityMultiplier.GetHashCode();
            hash = hash * 31 + result.HandlingMultiplier.GetHashCode();
            result.Hash = hash;
        }
        return result;
    }
}

