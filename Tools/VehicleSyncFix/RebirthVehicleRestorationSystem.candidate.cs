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
            default: min = 0.02f; max = 0.08f; return;
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
            if (string.IsNullOrEmpty(content.RepairableBlockName))
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
            Block block = Block.GetBlockByName(content.RepairableBlockName, false);
            if (block == null)
            {
                errors++;
                content.Availability = RebirthVehicleContentAvailability.DisabledMissingBlock;
                content.AvailabilityReason = "runtime block missing";
                result.AppendLine("missing block content=" + content.ContentId + " block=" + content.RepairableBlockName);
                continue;
            }

            string blockEntityClass = block.Properties.Values.ContainsKey("vehicle_entity_class")
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
        float wheelScore = 0f;
        int wheels = 0;
        for (int i = 0; i < family.Slots.Length; i++)
        {
            RebirthVehicleSlotDefinition slot = family.Slots[i];
            RebirthInstalledVehiclePart part;
            if (!assembly.Parts.TryGetValue(slot.SlotId, out part) || part == null) continue;
            float score = Mathf.Clamp01(part.Roll);
            string id = slot.SlotId;
            if (id.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) >= 0) { wheelScore += score; wheels++; continue; }
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

public static class RebirthVehicleAssemblySerializer
{
    private const int Magic = 0x52564241;
    public const int MaximumBytes = 262144;
    public static string ReadBoundedPayload(BinaryReader reader, int maxBytes)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        if (maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        uint length = 0;
        for (int shift = 0; shift <= 28; shift += 7)
        {
            byte next = reader.ReadByte();
            if (shift == 28 && next > 7) throw new InvalidDataException("Invalid vehicle payload length prefix.");
            length |= (uint)(next & 127) << shift;
            if ((next & 128) != 0) continue;
            if (length > maxBytes) throw new InvalidDataException("Vehicle payload exceeds size limit.");
            byte[] bytes = reader.ReadBytes((int)length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }
        throw new InvalidDataException("Invalid vehicle payload length prefix.");
    }

    public static void ValidateSize(RebirthVehicleAssembly assembly)
    {
        if (assembly == null) return;
        long size = 4 + 4 + 16 + 8 + 4 + 4 + 32 + StringBytes(assembly.ContentId) + StringBytes(assembly.FamilyId);
        if (assembly.Parts.Count > 64) throw new InvalidDataException("Too many vehicle parts.");
        foreach (var pair in assembly.Parts)
        {
            var part = pair.Value;
            size += StringBytes(pair.Key) + StringBytes(part?.ItemName) + 12 + StringBytes(part?.SerializedItemValue);
            if (size > MaximumBytes) throw new InvalidDataException("Vehicle assembly exceeds persistence size limit.");
        }
        if (size > MaximumBytes) throw new InvalidDataException("Vehicle assembly exceeds persistence size limit.");
    }
    private static long StringBytes(string value)
    {
        int bytes = Encoding.UTF8.GetByteCount(value ?? string.Empty);
        int prefix = 1; for (uint remaining = (uint)bytes; remaining >= 128; remaining >>= 7) prefix++;
        return (long)bytes + prefix;
    }


    public static void Write(BinaryWriter writer, RebirthVehicleAssembly assembly)
    {
        if (writer == null) throw new ArgumentNullException("writer");
        if (assembly == null) assembly = new RebirthVehicleAssembly();
        ValidateSize(assembly);
        writer.Write(Magic);
        writer.Write(RebirthVehicleAssembly.CurrentSchemaVersion);
        writer.Write(assembly.AssemblyId.ToByteArray());
        writer.Write(assembly.ContentId ?? string.Empty);
        writer.Write(assembly.FamilyId ?? string.Empty);
        writer.Write(assembly.Revision);
        writer.Write(Mathf.Clamp01(assembly.FuelPercent < 0f ? 0f : assembly.FuelPercent));
        writer.Write(assembly.Parts.Count);
        foreach (KeyValuePair<string, RebirthInstalledVehiclePart> pair in assembly.Parts)
        {
            RebirthInstalledVehiclePart part = pair.Value;
            writer.Write(pair.Key ?? string.Empty);
            writer.Write(part != null ? part.ItemName ?? string.Empty : string.Empty);
            writer.Write(part != null ? part.Quality : 0);
            writer.Write(part != null ? part.UseTimes : 0f);
            writer.Write(part != null ? part.Roll : 0f);
            writer.Write(part != null ? part.SerializedItemValue ?? string.Empty : string.Empty);
        }
        writer.Write(assembly.PendingInventoryTransferId.ToByteArray());
        writer.Write(assembly.LastAppliedInventoryTransferId.ToByteArray());
    }

    public static RebirthVehicleAssembly Read(BinaryReader reader)
    {
        if (reader == null) throw new ArgumentNullException("reader");
        if (reader.ReadInt32() != Magic) throw new InvalidDataException("Invalid REBIRTH vehicle assembly marker.");
        int version = reader.ReadInt32();
        if (version < 1 || version > RebirthVehicleAssembly.CurrentSchemaVersion) throw new InvalidDataException("Unsupported REBIRTH vehicle assembly version " + version);
        RebirthVehicleAssembly assembly = new RebirthVehicleAssembly();
        assembly.SchemaVersion = version;
        assembly.AssemblyId = new Guid(reader.ReadBytes(16));
        assembly.ContentId = ReadBoundedPayload(reader, MaximumBytes);
        assembly.FamilyId = ReadBoundedPayload(reader, MaximumBytes);
        assembly.Revision = reader.ReadInt64();
        assembly.FuelPercent = version >= 2 ? Mathf.Clamp01(reader.ReadSingle()) : -1f;
        int count = reader.ReadInt32();
        if (count < 0 || count > 64) throw new InvalidDataException("Invalid vehicle part count " + count);
        for (int i = 0; i < count; i++)
        {
            RebirthInstalledVehiclePart part = new RebirthInstalledVehiclePart();
            part.SlotId = ReadBoundedPayload(reader, MaximumBytes);
            part.ItemName = ReadBoundedPayload(reader, MaximumBytes);
            part.Quality = reader.ReadInt32();
            part.UseTimes = reader.ReadSingle();
            part.Roll = reader.ReadSingle();
            if (version >= 3)
            {
                part.SerializedItemValue = ReadBoundedPayload(reader, MaximumBytes);
                if (part.SerializedItemValue.Length > 262144) throw new InvalidDataException("Vehicle part item payload exceeds limit.");
            }
            if (string.IsNullOrEmpty(part.SlotId) || assembly.Parts.ContainsKey(part.SlotId))
                throw new InvalidDataException("Invalid or duplicate vehicle part slot.");
            assembly.Parts.Add(part.SlotId, part);
        }
        if (version >= 4)
        {
            byte[] pending = reader.ReadBytes(16), applied = reader.ReadBytes(16);
            if (pending.Length != 16 || applied.Length != 16) throw new EndOfStreamException();
            assembly.PendingInventoryTransferId = new Guid(pending);
            assembly.LastAppliedInventoryTransferId = new Guid(applied);
        }
        assembly.Performance = RebirthVehiclePerformanceCompiler.Compile(assembly);
        return assembly;
    }

    public static string ToBase64(RebirthVehicleAssembly assembly)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            Write(writer, assembly);
            writer.Flush();
            return Convert.ToBase64String(ms.ToArray());
        }
    }

    public static RebirthVehicleAssembly FromBase64(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length > 349528) throw new InvalidDataException("Vehicle assembly text exceeds size limit.");
        return ReadPayload(Convert.FromBase64String(value));
    }

    // Both native tile-entity envelopes and item metadata contain exactly one assembly.
    public static RebirthVehicleAssembly ReadPayload(byte[] bytes)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Vehicle assembly exceeds size limit.");
        using (MemoryStream ms = new MemoryStream(bytes, false))
        using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8, true))
        {
            RebirthVehicleAssembly assembly = Read(reader);
            if (ms.Position != ms.Length) throw new InvalidDataException("Trailing vehicle assembly data.");
            return assembly;
        }
    }
}

public sealed class TileEntityRepairableVehicleRebirth : TileEntity
{
    public RebirthVehicleAssembly Assembly = new RebirthVehicleAssembly();

    public TileEntityRepairableVehicleRebirth(Chunk chunk) : base(chunk) { }

    public override TileEntityType GetTileEntityType()
    {
        return (TileEntityType)RebirthUtilities.TileEntityRebirth.TileEntityRepairableVehicleRebirth;
    }

    public void EnsureInitialized(string contentId, BlockValue blockValue, WorldBase world)
    {
        RebirthVehicleDefinitionRegistry.Initialize();
        RebirthVehicleContentDefinition content = RebirthVehicleDefinitionRegistry.GetContent(contentId);
        if (Assembly == null) Assembly = new RebirthVehicleAssembly();
        if (Assembly.AssemblyId == Guid.Empty) Assembly.AssemblyId = Guid.NewGuid();
        if (string.IsNullOrEmpty(Assembly.ContentId)) Assembly.ContentId = contentId ?? string.Empty;
        if (string.IsNullOrEmpty(Assembly.FamilyId) && content != null) Assembly.FamilyId = content.FamilyId;
        if (Assembly.FuelPercent < 0f && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            Assembly.FuelPercent = CreateInitialFuelPercent(blockValue, world);
        Assembly.SchemaVersion = RebirthVehicleAssembly.CurrentSchemaVersion;
        Assembly.Performance = RebirthVehiclePerformanceCompiler.Compile(Assembly);
    }

    private static float CreateInitialFuelPercent(BlockValue blockValue, WorldBase world)
    {
        Block block = blockValue.Block;
        if (block == null) return 0f;
        float minimum = ReadBlockFloat(block, "RebirthVehicleFuelMinPercent", 0f);
        float maximum = ReadBlockFloat(block, "RebirthVehicleFuelMaxPercent", 0.15f);
        minimum = Mathf.Clamp01(minimum);
        maximum = Mathf.Clamp(maximum, minimum, 1f);
        if (maximum <= minimum) return minimum;
        World gameWorld = world as World;
        float random = gameWorld != null ? gameWorld.GetGameRandom().RandomFloat : UnityEngine.Random.value;
        return Mathf.Lerp(minimum, maximum, Mathf.Clamp01(random));
    }

    private static float ReadBlockFloat(Block block, string key, float fallback)
    {
        if (block == null || block.Properties == null || block.Properties.Values == null || !block.Properties.Values.ContainsKey(key)) return fallback;
        float parsed;
        return float.TryParse(block.Properties.Values[key], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed) ? parsed : fallback;
    }

    public override void read(PooledBinaryReader br, StreamModeRead mode)
    {
        base.read(br, mode);
        BinaryReader reader = (BinaryReader)br;
        int length = reader.ReadInt32();
        if (length < 0 || length > 262144) throw new InvalidDataException("Invalid repairable vehicle payload length " + length);
        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException();
        Assembly = RebirthVehicleAssemblySerializer.ReadPayload(bytes);
        RebirthVehicleAssemblyIndex.RegisterBlock(ToWorldPos(), this);
    }

    public override void write(PooledBinaryWriter bw, StreamModeWrite mode)
    {
        base.write(bw, mode);
        BinaryWriter writer = (BinaryWriter)bw;
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter payload = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            RebirthVehicleAssemblySerializer.Write(payload, Assembly);
            payload.Flush();
            byte[] bytes = ms.ToArray();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }

    public override void OnLoad()
    {
        base.OnLoad();
        RebirthVehicleAssemblyIndex.RegisterBlock(ToWorldPos(), this);
    }

    public override void OnDestroy()
    {
        RebirthVehicleAssemblyIndex.UnregisterBlock(ToWorldPos(), this);
        base.OnDestroy();
    }
}

public static class RebirthVehicleAssemblyIndex
{
    private static readonly Dictionary<Vector3i, TileEntityRepairableVehicleRebirth> Blocks = new Dictionary<Vector3i, TileEntityRepairableVehicleRebirth>();
    private static readonly Dictionary<int, RebirthVehicleAssembly> Entities = new Dictionary<int, RebirthVehicleAssembly>();
    private static readonly Dictionary<Guid, string> Carriers = new Dictionary<Guid, string>();
    private static readonly object Sync = new object();

    public static void RegisterBlock(Vector3i pos, TileEntityRepairableVehicleRebirth te)
    {
        if (te == null || te.Assembly == null || te.Assembly.AssemblyId == Guid.Empty) return;
        lock (Sync)
        {
            Blocks[pos] = te;
            Carriers[te.Assembly.AssemblyId] = "block:" + pos.x + ":" + pos.y + ":" + pos.z;
        }
    }

    public static void UnregisterBlock(Vector3i pos, TileEntityRepairableVehicleRebirth te)
    {
        lock (Sync)
        {
            TileEntityRepairableVehicleRebirth current;
            if (Blocks.TryGetValue(pos, out current) && ReferenceEquals(current, te)) Blocks.Remove(pos);
        }
    }

    public static void RegisterEntity(EntityVehicle entity, RebirthVehicleAssembly assembly)
    {
        if (entity == null || assembly == null || assembly.AssemblyId == Guid.Empty) return;
        if (!RebirthVehicleEntityPersistence.WriteAssemblyToVehicleItem(entity, assembly))
            throw new InvalidOperationException("Vehicle assembly metadata could not be written; registration was not published.");
        lock (Sync)
        {
            Entities[entity.entityId] = assembly;
            Carriers[assembly.AssemblyId] = "entity:" + entity.entityId;
        }
    }

    public static void UnregisterEntity(EntityVehicle entity)
    {
        if (entity == null) return;
        lock (Sync) { Entities.Remove(entity.entityId); }
    }

    public static bool TryGetEntity(EntityVehicle entity, out RebirthVehicleAssembly assembly)
    {
        assembly = null;
        if (entity == null) return false;
        lock (Sync) return Entities.TryGetValue(entity.entityId, out assembly);
    }

    public static bool TryGetEntity(int entityId, out RebirthVehicleAssembly assembly)
    {
        lock (Sync) return Entities.TryGetValue(entityId, out assembly);
    }

    // Recovery lookup scans the loaded index only when a plan needs resolution.
    // Carriers is a diagnostic last-writer map and cannot prove uniqueness.
    public static bool TryLocateTransferTarget(RebirthVehicleTransferTarget target, Guid assemblyId,
        out Vector3i position, out int entityId, out RebirthVehicleAssembly snapshot)
    {
        position = default(Vector3i); entityId = -1; snapshot = null;
        if (target == null || assemblyId == Guid.Empty) return false;
        lock (Sync)
        {
            int matches = 0;
            bool selected = false;
            Vector3i foundPosition = default(Vector3i);
            int foundEntity = -1;
            RebirthVehicleAssembly found = null;
            foreach (var pair in Blocks)
            {
                var assembly = pair.Value?.Assembly;
                if (assembly == null || assembly.AssemblyId != assemblyId) continue;
                matches++;
                if (target.Carrier == RebirthVehicleAssemblyCarrier.RepairableBlock
                    && pair.Key.x == target.X && pair.Key.y == target.Y && pair.Key.z == target.Z)
                { selected = true; foundPosition = pair.Key; found = assembly; }
            }
            foreach (var pair in Entities)
            {
                var assembly = pair.Value;
                if (assembly == null || assembly.AssemblyId != assemblyId) continue;
                matches++;
                if (target.Carrier == RebirthVehicleAssemblyCarrier.VehicleEntity)
                { selected = true; foundEntity = pair.Key; found = assembly; }
            }
            if (matches != 1 || !selected || found == null) return false;
            snapshot = found.DeepClone(); position = foundPosition; entityId = foundEntity;
            return true;
        }
    }

    public static void CommitEntity(EntityVehicle entity, RebirthVehicleAssembly assembly)
    {
        if (entity == null || assembly == null) return;
        RebirthVehicleAssemblySerializer.ValidateSize(assembly);
        assembly.Revision++;
        assembly.Performance = RebirthVehiclePerformanceCompiler.Compile(assembly);
        RegisterEntity(entity, assembly);
        // Sync flag 4 is the native 3.1 vehicle ItemValue channel. The REBIRTH
        // assembly is stored in that ItemValue's metadata and therefore follows
        // the same replication and vehicles.dat persistence path as the vehicle.
        entity.SendSyncData(4);
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Blocks.Clear();
            Entities.Clear();
            Carriers.Clear();
        }
    }

    public static string Inspect(Guid assemblyId)
    {
        lock (Sync)
        {
            string carrier;
            if (!Carriers.TryGetValue(assemblyId, out carrier)) return "assembly not loaded: " + assemblyId;
            RebirthVehicleAssembly assembly = null;
            foreach (TileEntityRepairableVehicleRebirth te in Blocks.Values) if (te != null && te.Assembly != null && te.Assembly.AssemblyId == assemblyId) { assembly = te.Assembly; break; }
            if (assembly == null) foreach (RebirthVehicleAssembly candidate in Entities.Values) if (candidate != null && candidate.AssemblyId == assemblyId) { assembly = candidate; break; }
            if (assembly == null) return "carrier index without assembly: " + assemblyId + " " + carrier;
            return Describe(assembly, carrier);
        }
    }

    public static string VerifyAll()
    {
        lock (Sync)
        {
            HashSet<Guid> ids = new HashSet<Guid>();
            int duplicates = 0;
            int invalid = 0;
            foreach (TileEntityRepairableVehicleRebirth te in Blocks.Values)
            {
                if (te == null || te.Assembly == null || te.Assembly.AssemblyId == Guid.Empty) { invalid++; continue; }
                if (!ids.Add(te.Assembly.AssemblyId)) duplicates++;
            }
            foreach (RebirthVehicleAssembly assembly in Entities.Values)
            {
                if (assembly == null || assembly.AssemblyId == Guid.Empty) { invalid++; continue; }
                if (!ids.Add(assembly.AssemblyId)) duplicates++;
            }
            return "verify loaded=" + ids.Count + " duplicates=" + duplicates + " invalid=" + invalid + " journal=" + RebirthVehicleRequestJournal.Count;
        }
    }

    private static string Describe(RebirthVehicleAssembly assembly, string carrier)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("assembly=").Append(assembly.AssemblyId).Append(" carrier=").Append(carrier).Append(" content=").Append(assembly.ContentId).Append(" family=").Append(assembly.FamilyId).Append(" revision=").Append(assembly.Revision).Append(" parts=").Append(assembly.Parts.Count).Append(" performanceHash=").Append(assembly.Performance != null ? assembly.Performance.Hash : 0);
        foreach (KeyValuePair<string, RebirthInstalledVehiclePart> pair in assembly.Parts)
            sb.AppendLine().Append("  ").Append(pair.Key).Append("=").Append(pair.Value != null ? pair.Value.ItemName : "<null>").Append(" q=").Append(pair.Value != null ? pair.Value.Quality : 0).Append(" roll=").Append(pair.Value != null ? pair.Value.Roll.ToString("0.000") : "0");
        return sb.ToString();
    }

    public static string Audit()
    {
        lock (Sync)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("repairableBlocks=").Append(Blocks.Count).Append(" entityAssemblies=").Append(Entities.Count).Append(" carriers=").Append(Carriers.Count);
            foreach (KeyValuePair<Guid, string> pair in Carriers) sb.AppendLine().Append(pair.Key).Append(" ").Append(pair.Value);
            return sb.ToString();
        }
    }
}

public class BlockRepairableVehicleRebirth : Block
{
    private string contentId;
    private BlockActivationCommand[] activationCommands;

    public BlockRepairableVehicleRebirth() { HasTileEntity = true; }

    public override void Init()
    {
        base.Init();
        contentId = Properties.Values.ContainsKey("RebirthVehicleContentId") ? Properties.Values["RebirthVehicleContentId"] : string.Empty;
        activationCommands = new[] { new BlockActivationCommand("Restore", "wrench", true, false) };
    }

    public override void OnBlockAdded(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue, PlatformUserIdentifierAbs addedByPlayer)
    {
        base.OnBlockAdded(world, chunk, blockPos, blockValue, addedByPlayer);
        TileEntityRepairableVehicleRebirth te = world.GetTileEntity(blockPos) as TileEntityRepairableVehicleRebirth;
        if (te == null)
        {
            te = new TileEntityRepairableVehicleRebirth(chunk);
            te.localChunkPos = World.toBlock(blockPos);
            chunk.AddTileEntity(te);
        }
        te.EnsureInitialized(contentId, blockValue, world);
        te.SetModified();
        RebirthVehicleAssemblyIndex.RegisterBlock(blockPos, te);
    }

    public override void OnBlockLoaded(WorldBase world, Vector3i blockPos, BlockValue blockValue)
    {
        base.OnBlockLoaded(world, blockPos, blockValue);
        TileEntityRepairableVehicleRebirth te = world.GetTileEntity(blockPos) as TileEntityRepairableVehicleRebirth;
        if (te != null) { te.EnsureInitialized(contentId, blockValue, world); RebirthVehicleAssemblyIndex.RegisterBlock(blockPos, te); }
    }

    public override void OnBlockRemoved(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        TileEntityRepairableVehicleRebirth te = world.GetTileEntity(blockPos) as TileEntityRepairableVehicleRebirth;
        if (te != null) RebirthVehicleAssemblyIndex.UnregisterBlock(blockPos, te);
        chunk.RemoveTileEntityAt<TileEntityRepairableVehicleRebirth>((World)world, World.toBlock(blockPos));
        base.OnBlockRemoved(world, chunk, blockPos, blockValue);
    }

    public override bool HasBlockActivationCommands(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing) { return true; }
    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing) { return activationCommands; }

    public override string GetActivationText(WorldBase world, BlockValue blockValue, Vector3i blockPos, EntityAlive entityFocusing)
    {
        TileEntityRepairableVehicleRebirth te = world.GetTileEntity(blockPos) as TileEntityRepairableVehicleRebirth;
        if (te == null || te.Assembly == null) return Localization.Get("xuiRebirthVehicleRestore");
        RebirthVehicleFamilyDefinition family = RebirthVehicleDefinitionRegistry.GetFamily(te.Assembly.FamilyId);
        int installed = te.Assembly.Parts.Count;
        int required = family != null ? family.Slots.Length : 0;
        return string.Format(Localization.Get("xuiRebirthVehicleRestoreProgress"), installed, required);
    }

    public override bool OnBlockActivated(string commandName, WorldBase world, Vector3i blockPos, BlockValue blockValue, EntityPlayerLocal player)
    {
        if (player == null) return false;
        if (blockValue.ischild)
        {
            Vector3i parent = blockValue.Block.multiBlockPos.GetParentPos(blockPos, blockValue);
            return OnBlockActivated(commandName, world, parent, world.GetBlock(parent), player);
        }
        TileEntityRepairableVehicleRebirth te = world.GetTileEntity(blockPos) as TileEntityRepairableVehicleRebirth;
        if (te == null) return false;
        te.EnsureInitialized(contentId, blockValue, world);
        XUiC_RebirthVehicleRestoration.Open(player.PlayerUI.xui, blockPos, te.Assembly);
        return true;
    }
}

public static class RebirthVehicleInventoryTransactions
{
    public static bool TryTakeExactPart(EntityPlayer player, string itemName, out RebirthInstalledVehiclePart installed)
    {
        installed = null;
        if (player == null || string.IsNullOrEmpty(itemName)) return false;
        ItemStack[] bag = player.bag.ItemGrid.items;
        PackedBoolArray locks = player.bag.LockedSlots;
        for (int i = 0; bag != null && i < bag.Length; i++)
        {
            if (locks != null && i < locks.Length && locks[i]) continue;
            ItemStack stack = bag[i];
            if (!Matches(stack, itemName)) continue;
            installed = FromItemValue(stack.itemValue, player.world as World);
            ItemStack after = stack.Clone();
            after.count--;
            if (after.count <= 0) after = ItemStack.Empty.Clone();
            player.bag.SetSlot(i, after);
            return true;
        }
        int slots = player.inventory != null ? player.inventory.Length : 0;
        for (int i = 0; i < slots; i++)
        {
            ItemStack stack = player.inventory.GetItemStack(i);
            if (!Matches(stack, itemName)) continue;
            installed = FromItemValue(stack.itemValue, player.world as World);
            ItemStack after = stack.Clone();
            after.count--;
            if (after.count <= 0) after = ItemStack.Empty.Clone();
            player.inventory.SetItem(i, after);
            return true;
        }
        return false;
    }

    public static bool TryTakeExactPartAt(EntityPlayer player, RebirthVehiclePartSourceLocation sourceLocation, int sourceSlot, string itemName, out RebirthInstalledVehiclePart installed)
    {
        installed = null;
        if (player == null || sourceSlot < 0 || string.IsNullOrEmpty(itemName)) return false;
        if (sourceLocation == RebirthVehiclePartSourceLocation.Backpack)
        {
            ItemStack[] bag = player.bag.ItemGrid.items;
            PackedBoolArray locks = player.bag.LockedSlots;
            if (bag == null || sourceSlot >= bag.Length || (locks != null && sourceSlot < locks.Length && locks[sourceSlot])) return false;
            ItemStack stack = bag[sourceSlot];
            if (!Matches(stack, itemName)) return false;
            installed = FromItemValue(stack.itemValue, player.world as World);
            ItemStack after = stack.Clone();
            after.count--;
            if (after.count <= 0) after = ItemStack.Empty.Clone();
            player.bag.SetSlot(sourceSlot, after);
            return true;
        }
        if (sourceLocation == RebirthVehiclePartSourceLocation.Toolbelt)
        {
            int count = player.inventory != null ? player.inventory.Length : 0;
            if (sourceSlot >= count) return false;
            ItemStack stack = player.inventory.GetItemStack(sourceSlot);
            if (!Matches(stack, itemName)) return false;
            installed = FromItemValue(stack.itemValue, player.world as World);
            ItemStack after = stack.Clone();
            after.count--;
            if (after.count <= 0) after = ItemStack.Empty.Clone();
            player.inventory.SetItem(sourceSlot, after);
            return true;
        }
        return false;
    }

    public static bool TryTakeMatchingOfferedPart(EntityPlayer player, string itemName, RebirthInstalledVehiclePart offered, out RebirthInstalledVehiclePart installed)
    {
        installed = null;
        if (player == null || offered == null || string.IsNullOrEmpty(itemName)) return false;
        RebirthInstalledVehiclePart sanitized = SanitizeOfferedPart(offered, itemName);
        if (sanitized == null) return false;
        ItemStack[] bag = player.bag != null ? player.bag.ItemGrid.items : null;
        PackedBoolArray locks = player.bag != null ? player.bag.LockedSlots : null;
        for (int i=0; bag!=null && i<bag.Length; i++)
        {
            if (locks!=null && i<locks.Length && locks[i]) continue;
            ItemStack stack=bag[i]; if(!Matches(stack,itemName))continue;
            RebirthInstalledVehiclePart candidate=FromItemValue(stack.itemValue,player.world as World);
            if(!SamePart(candidate,sanitized))continue;
            ItemStack after=stack.Clone();after.count--;if(after.count<=0)after=ItemStack.Empty.Clone();player.bag.SetSlot(i,after);installed=candidate;return true;
        }
        int count=player.inventory!=null?player.inventory.Length:0;
        for(int i=0;i<count;i++)
        {
            ItemStack stack=player.inventory.GetItemStack(i);if(!Matches(stack,itemName))continue;
            RebirthInstalledVehiclePart candidate=FromItemValue(stack.itemValue,player.world as World);
            if(!SamePart(candidate,sanitized))continue;
            ItemStack after=stack.Clone();after.count--;if(after.count<=0)after=ItemStack.Empty.Clone();player.inventory.SetItem(i,after);installed=candidate;return true;
        }
        return false;
    }

    private static bool SamePart(RebirthInstalledVehiclePart a, RebirthInstalledVehiclePart b)
    {
        return a!=null&&b!=null&&string.Equals(a.ItemName,b.ItemName,StringComparison.Ordinal)&&a.Quality==b.Quality&&Mathf.Abs(a.UseTimes-b.UseTimes)<0.001f&&Mathf.Abs(a.Roll-b.Roll)<0.0001f && (string.IsNullOrEmpty(b.SerializedItemValue) || string.Equals(a.SerializedItemValue,b.SerializedItemValue,StringComparison.Ordinal));
    }

    public static bool TryTakeSimpleItem(EntityPlayer player, string itemName)
    {
        RebirthInstalledVehiclePart ignored;
        return TryTakeExactPart(player, itemName, out ignored);
    }

    public static bool TryGivePart(EntityPlayer player, RebirthInstalledVehiclePart part)
    {
        if (player == null || part == null) return false;
        ItemStack stack = CreateItemStack(part);
        if (stack == null || stack.IsEmpty()) return false;
        if (player.bag.AddItem(stack) || player.inventory.AddItem(stack)) return true;
        GameManager.Instance.ItemDropServer(stack, player.position + Vector3.up, Vector3.zero, player.entityId, 60f, false);
        return true;
    }

    public static bool TryGiveSimpleItem(EntityPlayer player, string itemName, int count)
    {
        if (player == null || string.IsNullOrEmpty(itemName) || count <= 0) return false;
        ItemValue value = ItemClass.GetItem(itemName, false);
        if (value == null || value.IsEmpty()) return false;
        ItemStack stack = new ItemStack(value, count);
        if (player.bag.AddItem(stack) || player.inventory.AddItem(stack)) return true;
        GameManager.Instance.ItemDropServer(stack, player.position + Vector3.up, Vector3.zero, player.entityId, 60f, false);
        return true;
    }

    /// <summary>
    /// Transactional inventory credit used before a vehicle/world commit. Unlike the normal
    /// convenience credit, this never falls back to a world drop: a world drop cannot be
    /// recalled safely if the later vehicle mutation fails.
    /// </summary>
    public static bool TryGivePartNoDrop(EntityPlayer player, RebirthInstalledVehiclePart part)
    {
        if (player == null || part == null) return false;
        ItemStack stack = CreateItemStack(part);
        return TryGiveStackNoDrop(player, stack);
    }

    public static bool TryGiveSimpleItemNoDrop(EntityPlayer player, string itemName, int count)
    {
        if (player == null || string.IsNullOrEmpty(itemName) || count <= 0) return false;
        ItemValue value = ItemClass.GetItem(itemName, false);
        if (value == null || value.IsEmpty()) return false;
        return TryGiveStackNoDrop(player, new ItemStack(value, count));
    }

    public static bool TryTakeSimpleItems(EntityPlayer player, string itemName, int count)
    {
        if (player == null || string.IsNullOrEmpty(itemName) || count <= 0) return false;
        ItemValue value = ItemClass.GetItem(itemName, false);
        if (value == null || value.IsEmpty()) return false;

        ItemStack[] bagBefore = CloneSlots(player.bag != null ? player.bag.ItemGrid.items : null);
        ItemStack[] beltBefore = CloneSlots(player.inventory != null ? player.inventory.ItemGrid.items : null);
        int remaining = count;
        try
        {
            if (player.bag != null)
            {
                ItemStack[] slots = player.bag.ItemGrid.items;
                for (int i = 0; slots != null && i < slots.Length && remaining > 0; i++)
                {
                    ItemStack current = slots[i];
                    if (current == null || current.IsEmpty() || current.itemValue == null ||
                        current.itemValue.type != value.type) continue;
                    int take = Math.Min(remaining, current.count);
                    ItemStack next = current.Clone();
                    next.count -= take;
                    player.bag.SetSlot(i, next.count > 0 ? next : ItemStack.Empty.Clone());
                    remaining -= take;
                }
            }
            if (player.inventory != null)
            {
                int slots = player.inventory.Length;
                for (int i = 0; i < slots && remaining > 0; i++)
                {
                    ItemStack current = player.inventory.GetItemStack(i);
                    if (current == null || current.IsEmpty() || current.itemValue == null ||
                        current.itemValue.type != value.type) continue;
                    int take = Math.Min(remaining, current.count);
                    ItemStack next = current.Clone();
                    next.count -= take;
                    player.inventory.SetItem(i, next.count > 0 ? next : ItemStack.Empty.Clone());
                    remaining -= take;
                }
            }
            if (remaining == 0) return true;
        }
        catch
        {
            RestoreSlots(player, bagBefore, beltBefore);
            throw;
        }

        RestoreSlots(player, bagBefore, beltBefore);
        return false;
    }

    public static int CountSimpleItem(EntityPlayer player, string itemName)
    {
        if (player == null || string.IsNullOrEmpty(itemName)) return 0;
        ItemValue value = ItemClass.GetItem(itemName, false);
        if (value == null || value.IsEmpty()) return 0;
        int count = 0;
        ItemStack[] bag = player.bag != null ? player.bag.ItemGrid.items : null;
        for (int i = 0; bag != null && i < bag.Length; i++)
            if (bag[i] != null && !bag[i].IsEmpty() && bag[i].itemValue != null && bag[i].itemValue.type == value.type)
                count += bag[i].count;
        int beltCount = player.inventory != null ? player.inventory.Length : 0;
        for (int i = 0; i < beltCount; i++)
        {
            ItemStack stack = player.inventory.GetItemStack(i);
            if (stack != null && !stack.IsEmpty() && stack.itemValue != null && stack.itemValue.type == value.type)
                count += stack.count;
        }
        return count;
    }

    private static bool TryGiveStackNoDrop(EntityPlayer player, ItemStack stack)
    {
        if (player == null || stack == null || stack.IsEmpty()) return false;
        ItemStack[] bagBefore = CloneSlots(player.bag != null ? player.bag.ItemGrid.items : null);
        ItemStack[] beltBefore = CloneSlots(player.inventory != null ? player.inventory.ItemGrid.items : null);
        ItemStack remaining = stack.Clone();
        try
        {
            if (player.bag != null && player.bag.AddItem(remaining)) return true;
            if (remaining.count <= 0) return true;
            if (player.inventory != null && player.inventory.AddItem(remaining)) return true;
            if (remaining.count <= 0) return true;
        }
        catch
        {
            RestoreSlots(player, bagBefore, beltBefore);
            throw;
        }
        RestoreSlots(player, bagBefore, beltBefore);
        return false;
    }

    private static ItemStack[] CloneSlots(ItemStack[] source)
    {
        if (source == null) return new ItemStack[0];
        ItemStack[] result = new ItemStack[source.Length];
        for (int i = 0; i < source.Length; i++)
            result[i] = source[i] != null ? source[i].Clone() : ItemStack.Empty.Clone();
        return result;
    }

    private static void RestoreSlots(EntityPlayer player, ItemStack[] bag, ItemStack[] belt)
    {
        if (player == null) return;
        if (player.bag != null)
            for (int i = 0; bag != null && i < bag.Length; i++) player.bag.SetSlot(i, bag[i] ?? ItemStack.Empty.Clone());
        if (player.inventory != null)
            for (int i = 0; belt != null && i < belt.Length; i++) player.inventory.SetItem(i, belt[i] ?? ItemStack.Empty.Clone());
    }

    public static RebirthInstalledVehiclePart FromItemStack(ItemStack stack, World world)
    {
        return stack == null || stack.IsEmpty() ? null : FromItemValue(stack.itemValue, world);
    }

    public static ItemStack CreateItemStack(RebirthInstalledVehiclePart part)
    {
        if (part == null || string.IsNullOrEmpty(part.ItemName)) return ItemStack.Empty.Clone();
        if (!string.IsNullOrEmpty(part.SerializedItemValue))
        {
            ItemValue restored;
            if (!RebirthNativeItemCodec.TryDecode(part.SerializedItemValue, out restored) ||
                !string.Equals(restored.ItemClass.GetItemName(), part.ItemName, StringComparison.Ordinal))
                throw new InvalidDataException("Invalid installed vehicle part item payload.");
            return new ItemStack(restored, 1);
        }
        ItemClass itemClass = ItemClass.GetItemClass(part.ItemName, false);
        if (itemClass == null) return ItemStack.Empty.Clone();
        ItemValue value = new ItemValue(itemClass.Id);
        value.Quality = (byte)Mathf.Clamp(part.Quality, 1, 6);
        float maxUseTimes = value.MaxUseTimes;
        value.UseTimes = maxUseTimes > 0f ? Mathf.Clamp(part.UseTimes, 0f, maxUseTimes) : Mathf.Max(0f, part.UseTimes);
        RebirthVehiclePartMetadata.WriteRoll(value, Mathf.Clamp01(part.Roll));
        return new ItemStack(value, 1);
    }

    public static RebirthInstalledVehiclePart SanitizeOfferedPart(RebirthInstalledVehiclePart offered, string expectedItemName)
    {
        if (offered == null || string.IsNullOrEmpty(expectedItemName) || !string.Equals(offered.ItemName, expectedItemName, StringComparison.Ordinal)) return null;
        if (!string.IsNullOrEmpty(offered.SerializedItemValue))
        {
            ItemValue restored; int storedRoll;
            if (!RebirthNativeItemCodec.TryDecode(offered.SerializedItemValue, out restored) ||
                !string.Equals(restored.ItemClass.GetItemName(), expectedItemName, StringComparison.Ordinal) ||
                !restored.TryGetMetadata(RebirthVehiclePartMetadata.RollKey, out storedRoll)) return null;
            return FromItemValue(restored, null);
        }
        ItemClass itemClass = ItemClass.GetItemClass(expectedItemName, false);
        if (itemClass == null) return null;
        ItemValue value = new ItemValue(itemClass.Id);
        value.Quality = (byte)Mathf.Clamp(offered.Quality, 1, 6);
        float maxUseTimes = value.MaxUseTimes;
        value.UseTimes = maxUseTimes > 0f ? Mathf.Clamp(offered.UseTimes, 0f, maxUseTimes) : Mathf.Max(0f, offered.UseTimes);
        return new RebirthInstalledVehiclePart
        {
            ItemName = expectedItemName,
            Quality = value.Quality,
            UseTimes = value.UseTimes,
            Roll = Mathf.Clamp01(offered.Roll)
        };
    }

    public static bool Matches(ItemStack stack, string itemName)
    {
        return stack != null && !stack.IsEmpty() && stack.itemValue != null && stack.itemValue.ItemClass != null && string.Equals(stack.itemValue.ItemClass.GetItemName(), itemName, StringComparison.Ordinal);
    }

    private static RebirthInstalledVehiclePart FromItemValue(ItemValue value, World world)
    {
        if (value == null || value.ItemClass == null) return null;
        int quality = Math.Max(1, (int)value.Quality);
        float roll = RebirthVehiclePartMetadata.ReadOrCreateRoll(value, world);
        return new RebirthInstalledVehiclePart { ItemName = value.ItemClass.GetItemName(), Quality = quality, UseTimes = value.UseTimes, Roll = roll, SerializedItemValue = RebirthNativeItemCodec.Encode(value) };
    }
}

public static class RebirthVehicleAssemblyService
{
    // EntityVehicle.AddFuelFromInventory converts 25 ammoGasCan items into one
    // vehicle fuel unit. Siphoning an entity must use the inverse conversion
    // against its actual fuel level; tank capacities differ by vehicle.
    private const float GasItemsPerVehicleFuelUnit = 25f;

    private static readonly HashSet<Guid> ConversionLocks = new HashSet<Guid>();
    private static readonly object Sync = new object();

    public static RebirthVehicleAssembly Process(World world, EntityPlayer player, Vector3i pos, Guid assemblyId, long expectedRevision, RebirthVehicleAssemblyAction action, string slotId, RebirthInstalledVehiclePart offeredPart, RebirthVehiclePartSourceLocation sourceLocation, int sourceSlot, out string message, out RebirthVehicleRequestOutcome outcome)
    {
        message = string.Empty;
        outcome = RebirthVehicleRequestOutcome.Rejected;
        if (world == null || player == null) { message = "invalid request"; return null; }
        TileEntityRepairableVehicleRebirth te = world.GetTileEntity(pos) as TileEntityRepairableVehicleRebirth;
        if (te == null || te.Assembly == null) { message = Localization.Get("xuiRebirthVehicleMissing"); return null; }
        // Work against a detached assembly. Inventory/world side effects may still need
        // compensation, but failed pre-commit work must not mutate the indexed tile state
        // merely because te.Assembly and the local variable referenced the same object.
        RebirthVehicleAssembly assembly = te.Assembly.DeepClone();
        if (assemblyId != Guid.Empty && assembly.AssemblyId != assemblyId) { message = Localization.Get("xuiRebirthVehicleIdentityMismatch"); return assembly.DeepClone(); }
        if (action != RebirthVehicleAssemblyAction.Read && expectedRevision != assembly.Revision) { message = Localization.Get("xuiRebirthVehicleChanged"); return assembly.DeepClone(); }
        RebirthVehicleFamilyDefinition family = RebirthVehicleDefinitionRegistry.GetFamily(assembly.FamilyId);
        if (family == null) { message = Localization.Get("xuiRebirthVehicleInvalidDefinition"); return assembly.DeepClone(); }
        if (action == RebirthVehicleAssemblyAction.Read) { outcome = RebirthVehicleRequestOutcome.ReadOnly; return assembly.DeepClone(); }
        if (assembly.PendingInventoryTransferId != Guid.Empty)
        {
            message = Localization.Get("xuiRebirthVehicleTransferPending");
            return assembly.DeepClone();
        }
        if (action == RebirthVehicleAssemblyAction.Siphon)
        {
            int gas = Mathf.RoundToInt(Mathf.Clamp01(assembly.FuelPercent) * 500f);
            if (gas <= 0) { message = Localization.Get("xuiRebirthVehicleNoFuel"); return assembly.DeepClone(); }
            if (!RebirthVehicleInventoryTransactions.TryGiveSimpleItemNoDrop(player, "ammoGasCan", gas))
            { message = Localization.Get("xuiRebirthVehicleInventoryFull"); return assembly.DeepClone(); }
            RebirthVehicleAssembly beforeAssembly = te.Assembly.DeepClone();
            try
            {
                assembly.FuelPercent = 0f;
                Commit(te, assembly);
            }
            catch (Exception ex)
            {
                string compensationError;
                bool restored = TryRestoreBlockAssembly(te, beforeAssembly, out compensationError);
                bool reclaimed = false;
                try { reclaimed = RebirthVehicleInventoryTransactions.TryTakeSimpleItems(player, "ammoGasCan", gas); }
                catch (Exception reclaimEx) { compensationError = AppendFailure(compensationError, "siphon credit reclaim", reclaimEx); }
                outcome = restored && reclaimed ? RebirthVehicleRequestOutcome.Rejected : RebirthVehicleRequestOutcome.Indeterminate;
                message = "Repairable vehicle siphon failed" + (outcome == RebirthVehicleRequestOutcome.Indeterminate ? " with incomplete compensation" : " and was rolled back") +
                    ": " + ex.GetType().Name + ": " + ex.Message + (string.IsNullOrEmpty(compensationError) ? string.Empty : "; " + compensationError);
                return beforeAssembly.DeepClone();
            }
            outcome = RebirthVehicleRequestOutcome.Applied;
            message = string.Format(Localization.Get("xuiRebirthVehicleSiphoned"), gas);
            return assembly.DeepClone();
        }
        RebirthVehicleSlotDefinition slot = FindSlot(family, slotId);
        if (action == RebirthVehicleAssemblyAction.Install || action == RebirthVehicleAssemblyAction.Replace)
        {
            if (slot == null) { message = Localization.Get("xuiRebirthVehicleInvalidSlot"); return assembly.DeepClone(); }
            RebirthInstalledVehiclePart oldPart = null;
            assembly.Parts.TryGetValue(slot.SlotId, out oldPart);
            RebirthInstalledVehiclePart newPart;
            if (!TryAcquirePart(world, player, slot, offeredPart, sourceLocation, sourceSlot, out newPart))
            { message = string.Format(Localization.Get("xuiRebirthVehicleNeedPart"), Localization.Get(slot.DisplayKey)); return assembly.DeepClone(); }
            bool oldPartCredited = false;
            if (oldPart != null)
            {
                try { oldPartCredited = RebirthVehicleInventoryTransactions.TryGivePartNoDrop(player, oldPart); }
                catch { oldPartCredited = false; }
                if (!oldPartCredited)
                {
                    RebirthVehicleInventoryTransactions.TryGivePart(player, newPart);
                    message = Localization.Get("xuiRebirthVehicleInventoryFull");
                    return assembly.DeepClone();
                }
            }
            RebirthVehicleAssembly beforeAssembly = te.Assembly.DeepClone();
            try
            {
                newPart.SlotId = slot.SlotId;
                assembly.Parts[slot.SlotId] = newPart;
                Commit(te, assembly);
            }
            catch (Exception ex)
            {
                string compensationError;
                bool restored = TryRestoreBlockAssembly(te, beforeAssembly, out compensationError);
                bool reclaimedOld = !oldPartCredited;
                if (oldPartCredited)
                {
                    try
                    {
                        RebirthInstalledVehiclePart ignored;
                        reclaimedOld = RebirthVehicleInventoryTransactions.TryTakeMatchingOfferedPart(player, oldPart.ItemName, oldPart, out ignored);
                    }
                    catch (Exception reclaimEx) { compensationError = AppendFailure(compensationError, "old-part reclaim", reclaimEx); }
                }
                bool refundedNew = false;
                try { refundedNew = RebirthVehicleInventoryTransactions.TryGivePart(player, newPart); }
                catch (Exception refundEx) { compensationError = AppendFailure(compensationError, "new-part refund", refundEx); }
                outcome = restored && reclaimedOld && refundedNew ? RebirthVehicleRequestOutcome.Rejected : RebirthVehicleRequestOutcome.Indeterminate;
                message = "Repairable vehicle part publication failed" + (outcome == RebirthVehicleRequestOutcome.Indeterminate ? " with incomplete compensation" : " and was rolled back") +
                    ": " + ex.GetType().Name + ": " + ex.Message + (string.IsNullOrEmpty(compensationError) ? string.Empty : "; " + compensationError);
                return beforeAssembly.DeepClone();
            }
            outcome = RebirthVehicleRequestOutcome.Applied;
            RebirthSkillEventRouter.OnMechanicsCompleted(player, oldPart == null ? "install" : "replace", 1f, "block:" + assembly.ContentId + ":" + slot.SlotId);
            message = oldPart == null ? Localization.Get("xuiRebirthVehiclePartInstalled") : Localization.Get("xuiRebirthVehiclePartReplaced");
        }
        else if (action == RebirthVehicleAssemblyAction.Remove || action == RebirthVehicleAssemblyAction.Pickup)
        {
            RebirthInstalledVehiclePart part;
            if (slot == null || !assembly.Parts.TryGetValue(slot.SlotId, out part)) { message = Localization.Get("xuiRebirthVehicleSlotEmpty"); return assembly.DeepClone(); }
            bool credited = false;
            if (action == RebirthVehicleAssemblyAction.Remove)
            {
                try { credited = RebirthVehicleInventoryTransactions.TryGivePartNoDrop(player, part); }
                catch { credited = false; }
                if (!credited)
                {
                    message = Localization.Get("xuiRebirthVehicleInventoryFull");
                    return assembly.DeepClone();
                }
            }
            RebirthVehicleAssembly beforeAssembly = te.Assembly.DeepClone();
            try
            {
                assembly.Parts.Remove(slot.SlotId);
                Commit(te, assembly);
            }
            catch (Exception ex)
            {
                string compensationError;
                bool restored = TryRestoreBlockAssembly(te, beforeAssembly, out compensationError);
                bool reclaimed = !credited;
                if (credited)
                {
                    try
                    {
                        RebirthInstalledVehiclePart ignored;
                        reclaimed = RebirthVehicleInventoryTransactions.TryTakeMatchingOfferedPart(player, part.ItemName, part, out ignored);
                    }
                    catch (Exception reclaimEx) { compensationError = AppendFailure(compensationError, "removed-part reclaim", reclaimEx); }
                }
                outcome = restored && reclaimed ? RebirthVehicleRequestOutcome.Rejected : RebirthVehicleRequestOutcome.Indeterminate;
                message = "Repairable vehicle part removal failed" + (outcome == RebirthVehicleRequestOutcome.Indeterminate ? " with incomplete compensation" : " and was rolled back") +
                    ": " + ex.GetType().Name + ": " + ex.Message + (string.IsNullOrEmpty(compensationError) ? string.Empty : "; " + compensationError);
                return beforeAssembly.DeepClone();
            }
            outcome = RebirthVehicleRequestOutcome.Applied;
            message = Localization.Get("xuiRebirthVehiclePartRemoved");
        }
        else if (action == RebirthVehicleAssemblyAction.Hotwire)
        {
            string missing;
            if (!IsComplete(assembly, family, out missing)) { message = string.Format(Localization.Get("xuiRebirthVehicleIncomplete"), missing); return assembly.DeepClone(); }
            RebirthVehicleContentDefinition content = RebirthVehicleDefinitionRegistry.GetContent(assembly.ContentId);
            if (content == null || content.Availability != RebirthVehicleContentAvailability.Enabled) { message = Localization.Get("xuiRebirthVehicleUnavailable"); return assembly.DeepClone(); }
            if (!RebirthVehicleEntityFactory.HasPersistentOwner(player))
            { message = Localization.Get("xuiRebirthVehicleOwnerUnavailable"); return assembly.DeepClone(); }
            RebirthInstalledVehiclePart hotwireKit;
            if (!RebirthVehicleInventoryTransactions.TryTakeExactPart(player, "HotwireKit", out hotwireKit))
            { message = Localization.Get("xuiRebirthVehicleNeedHotwireKit"); return assembly.DeepClone(); }
            bool conversionRollbackComplete;
            if (!TryConvert(world, pos, te, player, content, out message, out conversionRollbackComplete))
            {
                // Refund only when the conversion service proved that its world/entity side
                // effects were fully rolled back. An uncertain conversion must retain the debit
                // until recovery resolves the actual world outcome.
                if (!conversionRollbackComplete)
                {
                    outcome = RebirthVehicleRequestOutcome.Indeterminate;
                    message = (message ?? Localization.Get("xuiRebirthVehicleSpawnFailed")) + "; conversion rollback is indeterminate";
                    return assembly.DeepClone();
                }
                bool refunded = false;
                try { refunded = RebirthVehicleInventoryTransactions.TryGivePart(player, hotwireKit); }
                catch { refunded = false; }
                if (!refunded)
                {
                    outcome = RebirthVehicleRequestOutcome.Indeterminate;
                    message = (message ?? Localization.Get("xuiRebirthVehicleSpawnFailed")) + "; hotwire-kit compensation failed";
                }
                return assembly.DeepClone();
            }
            outcome = RebirthVehicleRequestOutcome.Applied;
            RebirthSkillEventRouter.OnMechanicsCompleted(player, "hotwire", 1f, "convert:" + assembly.ContentId);
            return assembly.DeepClone();
        }
        return assembly.DeepClone();
    }

    public static RebirthVehicleAssembly ProcessEntity(World world, EntityPlayer player, int entityId, Guid assemblyId, long expectedRevision, RebirthVehicleAssemblyAction action, string slotId, RebirthInstalledVehiclePart offeredPart, RebirthVehiclePartSourceLocation sourceLocation, int sourceSlot, out string message, out RebirthVehicleRequestOutcome outcome)
    {
        message = string.Empty;
        outcome = RebirthVehicleRequestOutcome.Rejected;
        EntityVehicle entity = world != null ? world.GetEntity(entityId) as EntityVehicle : null;
        RebirthVehicleAssembly indexedAssembly;
        if (world == null || player == null || entity == null || !RebirthVehicleAssemblyIndex.TryGetEntity(entity, out indexedAssembly) || indexedAssembly == null)
        { message = Localization.Get("xuiRebirthVehicleMissing"); return null; }
        // Keep the indexed/persisted assembly immutable until CommitEntity publishes the
        // completed transaction. This also prevents SyncFuelFromEntity from becoming an
        // unjournalled mutation on a rejected request.
        RebirthVehicleAssembly assembly = indexedAssembly.DeepClone();
        SyncFuelFromEntity(entity, assembly);
        if (assemblyId != Guid.Empty && assembly.AssemblyId != assemblyId) { message = Localization.Get("xuiRebirthVehicleIdentityMismatch"); return assembly.DeepClone(); }
        if (action != RebirthVehicleAssemblyAction.Read && expectedRevision != assembly.Revision) { message = Localization.Get("xuiRebirthVehicleChanged"); return assembly.DeepClone(); }
        RebirthVehicleFamilyDefinition family = RebirthVehicleDefinitionRegistry.GetFamily(assembly.FamilyId);
        if (family == null) { message = Localization.Get("xuiRebirthVehicleInvalidDefinition"); return assembly.DeepClone(); }
        if (action == RebirthVehicleAssemblyAction.Read) { outcome = RebirthVehicleRequestOutcome.ReadOnly; return assembly.DeepClone(); }
        if (assembly.PendingInventoryTransferId != Guid.Empty)
        {
            message = Localization.Get("xuiRebirthVehicleTransferPending");
            return assembly.DeepClone();
        }
        if (action == RebirthVehicleAssemblyAction.Hotwire) { message = Localization.Get("xuiRebirthVehicleAlreadyConverted"); return assembly.DeepClone(); }
        if (action == RebirthVehicleAssemblyAction.Repair)
        {
            int maximumHealth = Math.Max(1, entity.GetMaxHealth());
            if (entity.Health >= maximumHealth) { message = Localization.Get("xuiRebirthVehicleAlreadyRepaired"); return assembly.DeepClone(); }
            RebirthInstalledVehiclePart repairKit;
            if (!RebirthVehicleInventoryTransactions.TryTakeExactPart(player, "resourceRepairKit", out repairKit))
            { message = Localization.Get("xuiRebirthVehicleNeedRepairKit"); return assembly.DeepClone(); }

            int healthBeforeRepair = entity.Health;
            RebirthVehicleAssembly beforeAssembly = indexedAssembly.DeepClone();
            try
            {
                int rebirthRepairHealth = RebirthServiceCraftSkillService.AdjustVehicleRepairAmount(player, 2500);
                entity.Health = Math.Min(maximumHealth, entity.Health + rebirthRepairHealth);
                RebirthVehicleAssemblyIndex.CommitEntity(entity, assembly);
            }
            catch (Exception ex)
            {
                bool restored = TryRestoreEntityMutation(entity, beforeAssembly, healthBeforeRepair, null, out string restoreError);
                bool refunded = false;
                try { refunded = RebirthVehicleInventoryTransactions.TryGivePart(player, repairKit); }
                catch (Exception refundEx) { restoreError = AppendFailure(restoreError, "repair-kit refund", refundEx); }
                outcome = restored && refunded ? RebirthVehicleRequestOutcome.Rejected : RebirthVehicleRequestOutcome.Indeterminate;
                message = "Vehicle repair failed" + (outcome == RebirthVehicleRequestOutcome.Indeterminate ? " with incomplete compensation" : " and was rolled back") +
                    ": " + ex.GetType().Name + ": " + ex.Message + (string.IsNullOrEmpty(restoreError) ? string.Empty : "; " + restoreError);
                return beforeAssembly.DeepClone();
            }

            outcome = RebirthVehicleRequestOutcome.Applied;
            RebirthSkillEventRouter.OnMechanicsCompleted(player, "repair", Mathf.Clamp01((float)(entity.Health-healthBeforeRepair)/maximumHealth), "entity:" + assembly.ContentId + ":" + entityId);
            entity.PlayOneShot("craft_complete_item", false);
            message = Localization.Get("xuiRebirthVehicleRepaired");
            return assembly.DeepClone();
        }
        if (action == RebirthVehicleAssemblyAction.Refuel)
        {
            if (entity.vehicle == null) { message = Localization.Get("xuiRebirthVehicleCannotRefuel"); return assembly.DeepClone(); }
            float beforeFuel = entity.vehicle.GetFuelLevel();
            if (entity.vehicle.GetFuelPercent() >= 1f) { message = Localization.Get("xuiRebirthVehicleFuelFull"); return assembly.DeepClone(); }
            int gasBefore = RebirthVehicleInventoryTransactions.CountSimpleItem(player, "ammoGasCan");
            RebirthVehicleAssembly beforeAssembly = indexedAssembly.DeepClone();
            try
            {
                if (!entity.AddFuelFromInventory(player)) { message = Localization.Get("xuiRebirthVehicleNeedFuel"); return assembly.DeepClone(); }
                SyncFuelFromEntity(entity, assembly);
                if (entity.vehicle.GetFuelLevel() <= beforeFuel)
                {
                    int gasAfterNoFuel = RebirthVehicleInventoryTransactions.CountSimpleItem(player, "ammoGasCan");
                    int consumedWithoutFuel = Math.Max(0, gasBefore - gasAfterNoFuel);
                    if (consumedWithoutFuel > 0)
                    {
                        bool refunded = RebirthVehicleInventoryTransactions.TryGiveSimpleItem(player, "ammoGasCan", consumedWithoutFuel);
                        if (!refunded) outcome = RebirthVehicleRequestOutcome.Indeterminate;
                    }
                    message = Localization.Get("xuiRebirthVehicleNeedFuel");
                    return assembly.DeepClone();
                }
                RebirthVehicleAssemblyIndex.CommitEntity(entity, assembly);
            }
            catch (Exception ex)
            {
                int gasAfter = RebirthVehicleInventoryTransactions.CountSimpleItem(player, "ammoGasCan");
                int consumed = Math.Max(0, gasBefore - gasAfter);
                bool restored = TryRestoreEntityMutation(entity, beforeAssembly, null, beforeFuel, out string restoreError);
                bool refunded = consumed == 0;
                if (consumed > 0)
                {
                    try { refunded = RebirthVehicleInventoryTransactions.TryGiveSimpleItem(player, "ammoGasCan", consumed); }
                    catch (Exception refundEx) { restoreError = AppendFailure(restoreError, "fuel refund", refundEx); }
                }
                outcome = restored && refunded ? RebirthVehicleRequestOutcome.Rejected : RebirthVehicleRequestOutcome.Indeterminate;
                message = "Vehicle refuel failed" + (outcome == RebirthVehicleRequestOutcome.Indeterminate ? " with incomplete compensation" : " and was rolled back") +
                    ": " + ex.GetType().Name + ": " + ex.Message + (string.IsNullOrEmpty(restoreError) ? string.Empty : "; " + restoreError);
                return beforeAssembly.DeepClone();
            }
            outcome = RebirthVehicleRequestOutcome.Applied;
            message = Localization.Get("xuiRebirthVehicleRefueled");
            return assembly.DeepClone();
        }
        if (action == RebirthVehicleAssemblyAction.Siphon)
        {
            // Use the actual entity fuel level, not FuelPercent * 500. A full
            // minibike tank is 40 fuel units (1,000 gas items), while the 4x4
            // tank is 400 fuel units (10,000 gas items).
            float beforeFuel = entity.vehicle != null ? entity.vehicle.GetFuelLevel() : 0f;
            int gas = Mathf.RoundToInt(Mathf.Max(0f, beforeFuel) * GasItemsPerVehicleFuelUnit);
            if (gas <= 0) { message = Localization.Get("xuiRebirthVehicleNoFuel"); return assembly.DeepClone(); }
            // Do not create a world drop before the vehicle commit: it cannot be recalled
            // if a later native/index publication fails.
            if (!RebirthVehicleInventoryTransactions.TryGiveSimpleItemNoDrop(player, "ammoGasCan", gas))
            { message = Localization.Get("xuiRebirthVehicleInventoryFull"); return assembly.DeepClone(); }

            RebirthVehicleAssembly beforeAssembly = indexedAssembly.DeepClone();
            try
            {
                entity.vehicle.SetFuelLevel(0f);
                assembly.FuelPercent = 0f;
                RebirthVehicleAssemblyIndex.CommitEntity(entity, assembly);
            }
            catch (Exception ex)
            {
                bool restored = TryRestoreEntityMutation(entity, beforeAssembly, null, beforeFuel, out string restoreError);
                bool reclaimed = false;
                try { reclaimed = RebirthVehicleInventoryTransactions.TryTakeSimpleItems(player, "ammoGasCan", gas); }
                catch (Exception reclaimEx) { restoreError = AppendFailure(restoreError, "siphon credit reclaim", reclaimEx); }
                outcome = restored && reclaimed ? RebirthVehicleRequestOutcome.Rejected : RebirthVehicleRequestOutcome.Indeterminate;
                message = "Vehicle siphon failed" + (outcome == RebirthVehicleRequestOutcome.Indeterminate ? " with incomplete compensation" : " and was rolled back") +
                    ": " + ex.GetType().Name + ": " + ex.Message + (string.IsNullOrEmpty(restoreError) ? string.Empty : "; " + restoreError);
                return beforeAssembly.DeepClone();
            }
            outcome = RebirthVehicleRequestOutcome.Applied;
            message = string.Format(Localization.Get("xuiRebirthVehicleSiphoned"), gas);
            return assembly.DeepClone();
        }
        RebirthVehicleSlotDefinition slot = FindSlot(family, slotId);
        if (slot == null) { message = Localization.Get("xuiRebirthVehicleInvalidSlot"); return assembly.DeepClone(); }
        if (action == RebirthVehicleAssemblyAction.Install || action == RebirthVehicleAssemblyAction.Replace)
        {
            RebirthInstalledVehiclePart oldPart = null;
            assembly.Parts.TryGetValue(slot.SlotId, out oldPart);
            RebirthInstalledVehiclePart newPart;
            if (!TryAcquirePart(world, player, slot, offeredPart, sourceLocation, sourceSlot, out newPart))
            { message = string.Format(Localization.Get("xuiRebirthVehicleNeedPart"), Localization.Get(slot.DisplayKey)); return assembly.DeepClone(); }

            bool oldPartCredited = false;
            if (oldPart != null)
            {
                try { oldPartCredited = RebirthVehicleInventoryTransactions.TryGivePartNoDrop(player, oldPart); }
                catch { oldPartCredited = false; }
                if (!oldPartCredited)
                {
                    RebirthVehicleInventoryTransactions.TryGivePart(player, newPart);
                    message = Localization.Get("xuiRebirthVehicleInventoryFull");
                    return assembly.DeepClone();
                }
            }

            RebirthVehicleAssembly beforeAssembly = indexedAssembly.DeepClone();
            try
            {
                newPart.SlotId = slot.SlotId;
                assembly.Parts[slot.SlotId] = newPart;
                RebirthVehicleAssemblyIndex.CommitEntity(entity, assembly);
            }
            catch (Exception ex)
            {
                string compensationError = string.Empty;
                bool restored = TryRestoreEntityMutation(entity, beforeAssembly, null, null, out compensationError);
                bool reclaimedOld = !oldPartCredited;
                if (oldPartCredited)
                {
                    try
                    {
                        RebirthInstalledVehiclePart reclaimed;
                        reclaimedOld = RebirthVehicleInventoryTransactions.TryTakeMatchingOfferedPart(
                            player, oldPart.ItemName, oldPart, out reclaimed);
                    }
                    catch (Exception reclaimEx) { compensationError = AppendFailure(compensationError, "old-part reclaim", reclaimEx); }
                }
                bool refundedNew = false;
                try { refundedNew = RebirthVehicleInventoryTransactions.TryGivePart(player, newPart); }
                catch (Exception refundEx) { compensationError = AppendFailure(compensationError, "new-part refund", refundEx); }
                outcome = restored && reclaimedOld && refundedNew ? RebirthVehicleRequestOutcome.Rejected : RebirthVehicleRequestOutcome.Indeterminate;
                message = "Vehicle part publication failed" + (outcome == RebirthVehicleRequestOutcome.Indeterminate ? " with incomplete compensation" : " and was rolled back") +
                    ": " + ex.GetType().Name + ": " + ex.Message + (string.IsNullOrEmpty(compensationError) ? string.Empty : "; " + compensationError);
                return beforeAssembly.DeepClone();
            }
            outcome = RebirthVehicleRequestOutcome.Applied;
            RebirthSkillEventRouter.OnMechanicsCompleted(player, oldPart == null ? "install" : "replace", 1f, "entity:" + assembly.ContentId + ":" + slot.SlotId);
            message = oldPart == null ? Localization.Get("xuiRebirthVehiclePartInstalled") : Localization.Get("xuiRebirthVehiclePartReplaced");
        }
        else if (action == RebirthVehicleAssemblyAction.Remove || action == RebirthVehicleAssemblyAction.Pickup)
        {
            RebirthInstalledVehiclePart part;
            if (!assembly.Parts.TryGetValue(slot.SlotId, out part)) { message = Localization.Get("xuiRebirthVehicleSlotEmpty"); return assembly.DeepClone(); }
            bool credited = false;
            if (action == RebirthVehicleAssemblyAction.Remove)
            {
                try { credited = RebirthVehicleInventoryTransactions.TryGivePartNoDrop(player, part); }
                catch { credited = false; }
                if (!credited)
                {
                    message = Localization.Get("xuiRebirthVehicleInventoryFull");
                    return assembly.DeepClone();
                }
            }
            RebirthVehicleAssembly beforeAssembly = indexedAssembly.DeepClone();
            try
            {
                assembly.Parts.Remove(slot.SlotId);
                RebirthVehicleAssemblyIndex.CommitEntity(entity, assembly);
            }
            catch (Exception ex)
            {
                string compensationError = string.Empty;
                bool restored = TryRestoreEntityMutation(entity, beforeAssembly, null, null, out compensationError);
                bool reclaimed = !credited;
                if (credited)
                {
                    try
                    {
                        RebirthInstalledVehiclePart ignored;
                        reclaimed = RebirthVehicleInventoryTransactions.TryTakeMatchingOfferedPart(
                            player, part.ItemName, part, out ignored);
                    }
                    catch (Exception reclaimEx) { compensationError = AppendFailure(compensationError, "removed-part reclaim", reclaimEx); }
                }
                outcome = restored && reclaimed ? RebirthVehicleRequestOutcome.Rejected : RebirthVehicleRequestOutcome.Indeterminate;
                message = "Vehicle part removal failed" + (outcome == RebirthVehicleRequestOutcome.Indeterminate ? " with incomplete compensation" : " and was rolled back") +
                    ": " + ex.GetType().Name + ": " + ex.Message + (string.IsNullOrEmpty(compensationError) ? string.Empty : "; " + compensationError);
                return beforeAssembly.DeepClone();
            }
            outcome = RebirthVehicleRequestOutcome.Applied;
            message = Localization.Get("xuiRebirthVehiclePartRemoved");
        }
        return assembly.DeepClone();
    }

    private static bool TryRestoreEntityMutation(EntityVehicle entity, RebirthVehicleAssembly beforeAssembly,
        int? health, float? fuelLevel, out string error)
    {
        error = string.Empty;
        bool ok = true;
        if (entity == null || beforeAssembly == null)
        {
            error = "entity rollback snapshot unavailable";
            return false;
        }
        if (health.HasValue)
        {
            try { entity.Health = health.Value; }
            catch (Exception ex) { ok = false; error = AppendFailure(error, "health rollback", ex); }
        }
        if (fuelLevel.HasValue && entity.vehicle != null)
        {
            try { entity.vehicle.SetFuelLevel(fuelLevel.Value); }
            catch (Exception ex) { ok = false; error = AppendFailure(error, "fuel rollback", ex); }
        }
        try
        {
            RebirthVehicleAssemblyIndex.RegisterEntity(entity, beforeAssembly.DeepClone());
            entity.SendSyncData(4);
        }
        catch (Exception ex)
        {
            ok = false;
            error = AppendFailure(error, "assembly rollback", ex);
        }
        return ok;
    }

    private static string AppendFailure(string current, string operation, Exception ex)
    {
        string detail = operation + "=" + (ex != null ? ex.GetType().Name + ": " + ex.Message : "failed");
        return string.IsNullOrEmpty(current) ? detail : current + "; " + detail;
    }

    private static void SyncFuelFromEntity(EntityVehicle entity, RebirthVehicleAssembly assembly)
    {
        if (entity == null || entity.vehicle == null || assembly == null) return;
        float maximum = entity.vehicle.GetMaxFuelLevel();
        assembly.FuelPercent = maximum > 0f ? Mathf.Clamp01(entity.vehicle.GetFuelLevel() / maximum) : 0f;
    }

    private static bool TryAcquirePart(World world, EntityPlayer player, RebirthVehicleSlotDefinition slot, RebirthInstalledVehiclePart offeredPart, RebirthVehiclePartSourceLocation sourceLocation, int sourceSlot, out RebirthInstalledVehiclePart installed)
    {
        installed = null;
        if (slot == null) return false;
        if (sourceLocation != RebirthVehiclePartSourceLocation.None)
            return RebirthVehicleInventoryTransactions.TryTakeExactPartAt(player, sourceLocation, sourceSlot, slot.ItemName, out installed);
        if (offeredPart != null)
        {
            // A local listen/single-player server may trust its own cursor object. A
            // dedicated server never creates a vehicle part solely from client metadata:
            // it must find and debit the matching authoritative inventory instance.
            if (player is EntityPlayerLocal && player.world != null && !player.world.IsRemote())
            {
                installed = RebirthVehicleInventoryTransactions.SanitizeOfferedPart(offeredPart, slot.ItemName);
                return installed != null;
            }
            return RebirthVehicleInventoryTransactions.TryTakeMatchingOfferedPart(player, slot.ItemName, offeredPart, out installed);
        }
        return RebirthVehicleInventoryTransactions.TryTakeExactPart(player, slot.ItemName, out installed);
    }

    private static bool TryRestoreBlockAssembly(TileEntityRepairableVehicleRebirth te,
        RebirthVehicleAssembly beforeAssembly, out string error)
    {
        error = string.Empty;
        if (te == null || beforeAssembly == null)
        {
            error = "block assembly rollback snapshot unavailable";
            return false;
        }
        try
        {
            te.Assembly = beforeAssembly.DeepClone();
            te.SetModified();
            RebirthVehicleAssemblyIndex.RegisterBlock(te.ToWorldPos(), te);
            return true;
        }
        catch (Exception ex)
        {
            error = "block assembly rollback=" + ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static void Commit(TileEntityRepairableVehicleRebirth te, RebirthVehicleAssembly assembly)
    {
        RebirthVehicleAssemblySerializer.ValidateSize(assembly);
        assembly.Revision++;
        assembly.Performance = RebirthVehiclePerformanceCompiler.Compile(assembly);
        te.Assembly = assembly;
        te.SetModified();
        RebirthVehicleAssemblyIndex.RegisterBlock(te.ToWorldPos(), te);
    }

    private static RebirthVehicleSlotDefinition FindSlot(RebirthVehicleFamilyDefinition family, string slotId)
    {
        if (family == null || string.IsNullOrEmpty(slotId)) return null;
        for (int i = 0; i < family.Slots.Length; i++) if (string.Equals(family.Slots[i].SlotId, slotId, StringComparison.Ordinal)) return family.Slots[i];
        return null;
    }

    private static bool IsComplete(RebirthVehicleAssembly assembly, RebirthVehicleFamilyDefinition family, out string missing)
    {
        missing = string.Empty;
        for (int i = 0; i < family.Slots.Length; i++)
        {
            RebirthVehicleSlotDefinition slot = family.Slots[i];
            if (!slot.Required || assembly.Parts.ContainsKey(slot.SlotId)) continue;
            missing = Localization.Get(slot.DisplayKey);
            return false;
        }
        return true;
    }

    private static bool TryConvert(World world, Vector3i pos, TileEntityRepairableVehicleRebirth te, EntityPlayer player, RebirthVehicleContentDefinition content, out string message, out bool rollbackComplete)
    {
        message = string.Empty;
        rollbackComplete = true;
        Guid id = te.Assembly.AssemblyId;
        lock (Sync) { if (!ConversionLocks.Add(id)) { message = Localization.Get("xuiRebirthVehicleConversionBusy"); return false; } }
        EntityVehicle entity = null;
        RebirthVehicleAssembly transferred = null;
        BlockValue sourceBlock = BlockValue.Air;
        bool sourceBlockRemoved = false;
        try
        {
            sourceBlock = world.GetBlock(pos);
            if (sourceBlock.isair || sourceBlock.Block == null)
            {
                message = Localization.Get("xuiRebirthVehicleMissing");
                return false;
            }

            RebirthVehicleContentDefinition sourceContent = RebirthVehicleDefinitionRegistry.GetContentForBlock(sourceBlock.Block.GetBlockName());
            string sourceEntityClass = sourceBlock.Block.Properties.Values.ContainsKey("vehicle_entity_class")
                ? sourceBlock.Block.Properties.Values["vehicle_entity_class"]
                : string.Empty;
            if (sourceContent == null ||
                sourceContent.Availability != RebirthVehicleContentAvailability.Enabled ||
                !string.Equals(sourceContent.ContentId, content.ContentId, StringComparison.Ordinal) ||
                string.IsNullOrEmpty(sourceEntityClass) ||
                !string.Equals(sourceEntityClass, sourceContent.EntityClassName, StringComparison.Ordinal) ||
                !string.Equals(sourceEntityClass, content.EntityClassName, StringComparison.Ordinal))
            {
                Log.Error("[REBIRTH Vehicle] conversion binding mismatch assembly=" + id +
                    " block=" + sourceBlock.Block.GetBlockName() +
                    " content=" + content.ContentId +
                    " xmlEntity=" + (string.IsNullOrEmpty(sourceEntityClass) ? "<missing>" : sourceEntityClass) +
                    " registeredEntity=" + content.EntityClassName);
                message = Localization.Get("xuiRebirthVehicleInvalidDefinition");
                return false;
            }

            // Preserve the 2.6 contract: the repairable block's
            // vehicle_entity_class property is the authoritative entity to spawn.
            entity = RebirthVehicleEntityFactory.CreateFromRepairableBlock(
                world,
                sourceEntityClass,
                pos,
                sourceBlock.rotation);
            if (entity == null) { message = Localization.Get("xuiRebirthVehicleSpawnFailed"); return false; }

            transferred = te.Assembly.DeepClone();
            int maximumDurability = Math.Max(1, sourceBlock.Block.MaxDamage);
            int currentDurability = Mathf.Clamp(maximumDurability - sourceBlock.damage, 1, maximumDurability);
            RebirthVehicleEntityFactory.ApplyRestoredState(entity, currentDurability, maximumDurability, transferred.FuelPercent);
            RebirthVehicleEntityFactory.AssignOwner(entity, player);
            entity.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);

            // The repairable block and the resulting vehicle occupy the same world location.
            // Remove the complete multiblock first, then spawn the entity at the dedicated
            // repairable-block transform so physics cannot relocate it around the source block.
            world.SetBlockRPC((BlockValueRef)pos, BlockValue.Air);
            sourceBlockRemoved = true;
            if (!world.GetBlock(pos).isair)
                throw new InvalidOperationException("Repairable vehicle block was not removed before entity spawn.");

            RebirthVehicleAssemblyIndex.RegisterEntity(entity, transferred);
            world.SpawnEntityInWorld(entity);
            message = Localization.Get("xuiRebirthVehicleConversionComplete");
            return true;
        }
        catch (Exception ex)
        {
            if (entity != null)
            {
                try
                {
                    RebirthVehicleAssemblyIndex.UnregisterEntity(entity);
                    Entity live = world.GetEntity(entity.entityId);
                    if (live != null) world.RemoveEntity(entity.entityId, EnumRemoveEntityReason.Despawned);
                }
                catch (Exception removeEx)
                {
                    rollbackComplete = false;
                    Log.Error("[REBIRTH Vehicle] conversion entity rollback failed assembly=" + id + " " +
                        removeEx.GetType().Name + ": " + removeEx.Message);
                }
            }
            if (sourceBlockRemoved && world.GetBlock(pos).isair && !sourceBlock.isair)
            {
                try
                {
                    world.SetBlockRPC((BlockValueRef)pos, sourceBlock);
                    TileEntityRepairableVehicleRebirth restored = world.GetTileEntity(pos) as TileEntityRepairableVehicleRebirth;
                    if (restored == null)
                        throw new InvalidOperationException("Restored repairable block did not recreate its tile entity.");
                    if (transferred != null)
                    {
                        restored.Assembly = transferred.DeepClone();
                        restored.SetModified();
                        RebirthVehicleAssemblyIndex.RegisterBlock(pos, restored);
                    }
                }
                catch (Exception restoreEx)
                {
                    rollbackComplete = false;
                    Log.Error("[REBIRTH Vehicle] conversion block rollback failed assembly=" + id + " " + restoreEx.GetType().Name + ": " + restoreEx.Message);
                }
            }
            else if (sourceBlockRemoved && sourceBlock.isair)
            {
                rollbackComplete = false;
            }
            Log.Error("[REBIRTH Vehicle] conversion failed assembly=" + id + " rollbackComplete=" + rollbackComplete +
                " " + ex.GetType().Name + ": " + ex.Message);
            message = Localization.Get("xuiRebirthVehicleSpawnFailed");
            return false;
        }
        finally { lock (Sync) ConversionLocks.Remove(id); }
    }
}

public static class RebirthVehicleEntityFactory
{
    public static void Initialize() { }

    public static EntityVehicle CreateFromRepairableBlock(World world, string entityClassName, Vector3i blockPosition, byte blockRotation)
    {
        Vector3 position = new Vector3(blockPosition.x, blockPosition.y + 0.25f, blockPosition.z);
        Vector3 rotation = new Vector3(0f, GetRepairableBlockYaw(blockRotation), 0f);
        return Create(world, entityClassName, position, rotation);
    }

    public static EntityVehicle Create(World world, string entityClassName, Vector3 position)
    {
        return Create(world, entityClassName, position, Vector3.zero);
    }

    public static EntityVehicle Create(World world, string entityClassName, Vector3 position, Vector3 rotation)
    {
        if (world == null || string.IsNullOrEmpty(entityClassName)) return null;

        // Match the vanilla 3.1 vehicle-placement path: resolve the registered
        // entity-class key by its exact XML name instead of assuming a raw hash
        // is a usable runtime definition.
        int classId = EntityClass.GetId(entityClassName);
        if (classId == -1)
        {
            Log.Error("[REBIRTH Vehicle] entity class is not registered name=" + entityClassName);
            return null;
        }

        EntityClass entityDefinition = EntityClass.GetEntityClass(classId);
        if (entityDefinition == null)
        {
            Log.Error("[REBIRTH Vehicle] entity definition is missing name=" + entityClassName + " id=" + classId);
            return null;
        }

        string configuredClass = entityDefinition.Properties.Values.ContainsKey(EntityClass.PropClass)
            ? entityDefinition.Properties.Values[EntityClass.PropClass]
            : "<missing Class property>";
        if (entityDefinition.classname == null)
        {
            // EntityFactory instantiates the prefab before discovering that the
            // C# class is invalid, which leaves an orphaned, non-interactable
            // visual object. Reject the conversion before calling the factory.
            Log.Error("[REBIRTH Vehicle] entity runtime class is unresolved name=" +
                entityClassName + " id=" + classId + " class=" + configuredClass);
            return null;
        }
        if (!typeof(EntityVehicle).IsAssignableFrom(entityDefinition.classname))
        {
            Log.Error("[REBIRTH Vehicle] entity runtime class is not a vehicle name=" +
                entityClassName + " id=" + classId + " class=" + configuredClass);
            return null;
        }

        return EntityFactory.CreateEntity(classId, position, rotation) as EntityVehicle;
    }

    private static float GetRepairableBlockYaw(byte blockRotation)
    {
        switch (blockRotation)
        {
            case 0: return 0f;
            case 1: return 90f;
            case 2: return 180f;
            case 3: return 270f;
            case 24: return 45f;
            case 25: return 135f;
            case 26: return 225f;
            case 27: return 315f;
            default: return 0f;
        }
    }

    public static void ApplyRestoredState(EntityVehicle entity, int currentDurability, int maximumDurability, float fuelPercent)
    {
        if (entity == null) return;
        maximumDurability = Math.Max(1, maximumDurability);
        currentDurability = Mathf.Clamp(currentDurability, 1, maximumDurability);
        entity.Stats.Health.BaseMax = maximumDurability;
        entity.Stats.Health.OriginalMax = maximumDurability;
        entity.Health = currentDurability;
        if (entity.vehicle != null)
            entity.vehicle.SetFuelLevel(Mathf.Clamp01(fuelPercent < 0f ? 0f : fuelPercent) * entity.vehicle.GetMaxFuelLevel());
    }

    public static bool HasPersistentOwner(EntityPlayer player)
    {
        return player != null && GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId != null;
    }

    public static void AssignOwner(EntityVehicle entity, EntityPlayer player)
    {
        if (entity == null || player == null || entity.vehicle == null)
            throw new InvalidOperationException("Vehicle owner assignment requires a valid vehicle and player.");
        PersistentPlayerData data = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if (data == null || data.PrimaryId == null)
            throw new InvalidOperationException("Vehicle persistent owner is unavailable.");
        entity.vehicle.OwnerId = data.PrimaryId;
        entity.belongsPlayerId = player.entityId;
    }
}

public static class RebirthVehicleEntityPersistence
{
    private const string AssemblyMetadataKey = "RebirthVehicleAssembly";
    private const int LegacyMarker = 0x52564245;
    private const int MaximumPayloadLength = 262144;

    public static bool WriteAssemblyToVehicleItem(EntityVehicle entity, RebirthVehicleAssembly assembly)
    {
        if (entity == null || entity.vehicle == null || assembly == null || assembly.AssemblyId == Guid.Empty)
            return false;

        try
        {
            ItemValue vehicleItem = entity.vehicle.GetUpdatedItemValue();
            if (vehicleItem == null)
                return false;

            vehicleItem.SetMetadata(AssemblyMetadataKey, RebirthVehicleAssemblySerializer.ToBase64(assembly));
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Vehicle] failed to persist assembly metadata entity=" + entity.entityId +
                " " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    public static bool TryReadAssemblyFromVehicleItem(EntityVehicle entity, out RebirthVehicleAssembly assembly)
    {
        assembly = null;
        if (entity == null || entity.vehicle == null)
            return false;

        try
        {
            ItemValue vehicleItem = entity.vehicle.GetUpdatedItemValue();
            string payload;
            if (vehicleItem == null ||
                !vehicleItem.TryGetMetadata(AssemblyMetadataKey, out payload) ||
                string.IsNullOrEmpty(payload))
                return false;

            assembly = RebirthVehicleAssemblySerializer.FromBase64(payload);
            if (assembly == null || assembly.AssemblyId == Guid.Empty || string.IsNullOrEmpty(assembly.ContentId))
            {
                assembly = null;
                return false;
            }

            assembly.Performance = RebirthVehiclePerformanceCompiler.Compile(assembly);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Vehicle] failed to restore assembly metadata entity=" + entity.entityId +
                " " + ex.GetType().Name + ": " + ex.Message);
            assembly = null;
            return false;
        }
    }

    public static void BeforeWriteSyncData(EntityVehicle entity, BinaryWriter writer, ushort syncFlags)
    {
        if (entity == null || writer == null || (syncFlags & 4) == 0)
            return;

        RebirthVehicleAssembly assembly;
        if (RebirthVehicleAssemblyIndex.TryGetEntity(entity, out assembly) && assembly != null)
            WriteAssemblyToVehicleItem(entity, assembly);
    }

    public static void AfterReadSyncData(EntityVehicle entity, BinaryReader reader, ushort syncFlags)
    {
        if (entity == null || reader == null)
            return;

        RebirthVehicleAssembly assembly;
        if ((syncFlags & 4) != 0 && TryReadAssemblyFromVehicleItem(entity, out assembly))
        {
            RebirthVehicleAssemblyIndex.RegisterEntity(entity, assembly);
            return;
        }

        // Migrate vehicles written by the previous experimental persistence
        // implementation, which appended a marker and payload after native sync data.
        if ((syncFlags & 0x4000) == 0)
            return;

        Stream stream = reader.BaseStream;
        long start = stream != null && stream.CanSeek ? stream.Position : -1L;
        try
        {
            int marker = reader.ReadInt32();
            if (marker != LegacyMarker)
            {
                if (start >= 0L) stream.Position = start;
                return;
            }

            int length = reader.ReadInt32();
            if (length < 0 || length > MaximumPayloadLength)
                throw new InvalidDataException("Invalid legacy entity assembly payload length " + length);

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException();

            assembly = RebirthVehicleAssemblySerializer.ReadPayload(bytes);

            RebirthVehicleAssemblyIndex.RegisterEntity(entity, assembly);
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Vehicle] migrated legacy entity assembly to native vehicle metadata entity=" + entity.entityId); }
        }
        catch
        {
            if (start >= 0L) stream.Position = start;
        }
    }

    public static void AfterEntityAdded(EntityVehicle entity)
    {
        if (entity == null)
            return;

        RebirthVehicleAssembly existing;
        if (RebirthVehicleAssemblyIndex.TryGetEntity(entity, out existing) && existing != null)
            return;

        RebirthVehicleAssembly restored;
        if (TryReadAssemblyFromVehicleItem(entity, out restored))
            RebirthVehicleAssemblyIndex.RegisterEntity(entity, restored);
    }

    public static void AfterEntityUnload(EntityVehicle entity)
    {
        if (entity == null) return;

        // Remote clients do not own VehicleManager persistence, so their runtime
        // entry can be released immediately. On the server, VehicleManager creates
        // the persisted EntityCreationData only after OnEntityUnload returns.
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool remote = entity.world != null ? entity.world.IsRemote()
            : connection != null && !connection.IsServer;
        if (remote) RebirthVehicleAssemblyIndex.UnregisterEntity(entity);
    }

    public static void AfterVehicleRemoved(EntityVehicle vehicle)
    {
        // VehicleManager.RemoveTrackedVehicle has already copied the complete entity
        // into EntityCreationData. The assembly is embedded in the native vehicle
        // ItemValue metadata before that copy is made.
        RebirthVehicleAssemblyIndex.UnregisterEntity(vehicle);
    }
}

public static class RebirthVehicleRuntimePerformance
{
    public struct PhysicsState
    {
        public bool Captured;
        public Vehicle Vehicle;
        public float MotorTorqueForward, MotorTorqueBackward, MotorTorqueTurboForward, MotorTorqueTurboBackward;
        public float VelocityMaxForward, VelocityMaxBackward, VelocityMaxTurboForward, VelocityMaxTurboBackward;
        public float BrakeTorque, SteerRate, SteerCenteringRate;
        public float ResponseMultiplier, ResponseTarget, ResponseBaseRate;
    }

    internal static bool HasPhysicsModifiers(RebirthVehiclePerformanceSnapshot p)
    {
        return p != null && (p.AccelerationMultiplier != 1f || p.TorqueMultiplier != 1f ||
            p.VelocityMultiplier != 1f || p.TurboVelocityMultiplier != 1f ||
            p.ReverseVelocityMultiplier != 1f || p.BrakingMultiplier != 1f ||
            p.HandlingMultiplier != 1f || p.HillClimbMultiplier != 1f || !(p.ResponseMultiplier <= 1f));
    }

    public static void BeforePhysics(EntityVehicle entity, out PhysicsState state)
    {
        state = default(PhysicsState);
        if (entity == null || entity.vehicle == null || entity.isEntityRemote) return;
        RebirthVehicleAssembly assembly;
        if (!RebirthVehicleAssemblyIndex.TryGetEntity(entity, out assembly) || assembly == null || assembly.Performance == null) return;

        Vehicle vehicle = entity.vehicle;
        RebirthVehiclePerformanceSnapshot p = assembly.Performance;
        if (!HasPhysicsModifiers(p)) return;
        state = new PhysicsState
        {
            Captured = true,
            Vehicle = vehicle,
            MotorTorqueForward = vehicle.MotorTorqueForward,
            MotorTorqueBackward = vehicle.MotorTorqueBackward,
            MotorTorqueTurboForward = vehicle.MotorTorqueTurboForward,
            MotorTorqueTurboBackward = vehicle.MotorTorqueTurboBackward,
            VelocityMaxForward = vehicle.VelocityMaxForward,
            VelocityMaxBackward = vehicle.VelocityMaxBackward,
            VelocityMaxTurboForward = vehicle.VelocityMaxTurboForward,
            VelocityMaxTurboBackward = vehicle.VelocityMaxTurboBackward,
            BrakeTorque = vehicle.brakeTorque,
            SteerRate = vehicle.steerRate,
            SteerCenteringRate = vehicle.steerCenteringRate,
            ResponseMultiplier = p.ResponseMultiplier
        };

        float selectedVelocity = entity.vehicle.IsTurbo ? vehicle.VelocityMaxTurboForward * p.TurboVelocityMultiplier : vehicle.VelocityMaxForward * p.VelocityMultiplier;
        float speedRatio = selectedVelocity > 0.01f ? Mathf.Clamp01(Mathf.Abs(vehicle.CurrentForwardVelocity) / selectedVelocity) : 0f;
        float lowSpeedFactor = 1f - speedRatio;
        // Terrain sampling cannot affect torque when the hill multiplier is neutral.
        float uphillFactor = p.HillClimbMultiplier == 1f ? 0f : GetUphillFactor(entity);

        float forwardTorqueMultiplier = p.AccelerationMultiplier;
        forwardTorqueMultiplier *= 1f + (p.TorqueMultiplier - 1f) * lowSpeedFactor;
        forwardTorqueMultiplier *= 1f + (p.HillClimbMultiplier - 1f) * uphillFactor;
        float reverseTorqueMultiplier = p.AccelerationMultiplier * (1f + (p.TorqueMultiplier - 1f) * lowSpeedFactor);

        vehicle.MotorTorqueForward *= forwardTorqueMultiplier;
        vehicle.MotorTorqueTurboForward *= forwardTorqueMultiplier;
        vehicle.MotorTorqueBackward *= reverseTorqueMultiplier;
        vehicle.MotorTorqueTurboBackward *= reverseTorqueMultiplier;
        vehicle.VelocityMaxForward *= p.VelocityMultiplier;
        vehicle.VelocityMaxTurboForward *= p.TurboVelocityMultiplier;
        vehicle.VelocityMaxBackward *= p.ReverseVelocityMultiplier;
        vehicle.VelocityMaxTurboBackward *= p.ReverseVelocityMultiplier;
        vehicle.brakeTorque *= p.BrakingMultiplier;
        vehicle.steerRate *= p.HandlingMultiplier;
        vehicle.steerCenteringRate *= p.HandlingMultiplier;

        if (p.ResponseMultiplier > 1f)
        {
            state.ResponseTarget = (entity.vehicle.IsTurbo ? vehicle.VelocityMaxTurboForward : vehicle.VelocityMaxForward) * vehicle.EffectVelocityMaxPer;
            state.ResponseBaseRate = state.ResponseTarget > entity.velocityMax ? 2.5f : 1.5f;
        }
    }

    public static void AfterPhysics(EntityVehicle entity, PhysicsState state)
    {
        RestorePhysics(entity, state);
        if (entity == null || !state.Captured) return;
        if (state.ResponseMultiplier > 1f && state.ResponseTarget > 0.01f)
        {
            float extraRate = (state.ResponseMultiplier - 1f) * state.ResponseBaseRate;
            entity.velocityMax = Mathf.MoveTowards(entity.velocityMax, state.ResponseTarget, extraRate * Time.fixedDeltaTime);
        }
    }

    public static void RestorePhysics(EntityVehicle entity, PhysicsState state)
    {
        if (!state.Captured || state.Vehicle == null) return;
        Vehicle vehicle = state.Vehicle;
        vehicle.MotorTorqueForward = state.MotorTorqueForward;
        vehicle.MotorTorqueBackward = state.MotorTorqueBackward;
        vehicle.MotorTorqueTurboForward = state.MotorTorqueTurboForward;
        vehicle.MotorTorqueTurboBackward = state.MotorTorqueTurboBackward;
        vehicle.VelocityMaxForward = state.VelocityMaxForward;
        vehicle.VelocityMaxBackward = state.VelocityMaxBackward;
        vehicle.VelocityMaxTurboForward = state.VelocityMaxTurboForward;
        vehicle.VelocityMaxTurboBackward = state.VelocityMaxTurboBackward;
        vehicle.brakeTorque = state.BrakeTorque;
        vehicle.steerRate = state.SteerRate;
        vehicle.steerCenteringRate = state.SteerCenteringRate;

    }

    public static void BeforeFuel(VPEngine engine, out float originalFuelKmPerL)
    {
        originalFuelKmPerL = 0f;
        if (engine == null || engine.vehicle == null || engine.fuelKmPerL <= 0f) return;
        RebirthVehicleAssembly assembly;
        EntityVehicle entity = engine.vehicle.entity as EntityVehicle;
        if (entity == null || !RebirthVehicleAssemblyIndex.TryGetEntity(entity, out assembly) || assembly == null || assembly.Performance == null) return;
        originalFuelKmPerL = engine.fuelKmPerL;
        float consumptionMultiplier = assembly.Performance.FuelUseMultiplier;
        if (engine.vehicle.IsTurbo) consumptionMultiplier *= assembly.Performance.TurboFuelUseMultiplier;
        // Survivor Trait fuel efficiency is resolved from the authoritative completed character of
        // the player currently attached to this vehicle. The vehicle system remains the sole fuel writer.
        consumptionMultiplier *= RebirthTraitGameplayModifierService.GetVehicleFuelUseMultiplier(entity);
        engine.fuelKmPerL = originalFuelKmPerL / Mathf.Max(0.1f, consumptionMultiplier);
    }

    public static void AfterFuel(VPEngine engine, float originalFuelKmPerL)
    {
        RestoreFuel(engine, originalFuelKmPerL);
    }

    public static void RestoreFuel(VPEngine engine, float originalFuelKmPerL)
    {
        if (engine != null && originalFuelKmPerL > 0f) engine.fuelKmPerL = originalFuelKmPerL;
    }

    private static float GetUphillFactor(EntityVehicle entity)
    {
        if (entity == null || entity.PhysicsTransform == null || entity.movementInput == null || entity.movementInput.moveForward <= 0f) return 0f;
        Vector3 forward = entity.PhysicsTransform.forward;
        float pitch = Mathf.Clamp01(Vector3.Dot(forward, Vector3.up) * 3f);
        RaycastHit hit;
        float ground = 0f;
        if (Physics.Raycast(entity.PhysicsTransform.position + Vector3.up * 0.75f, Vector3.down, out hit, 3f))
        {
            Vector3 uphill = Vector3.ProjectOnPlane(Vector3.up, hit.normal);
            if (uphill.sqrMagnitude > 0.000001f)
            {
                uphill.Normalize();
                float facing = Mathf.Clamp01(Vector3.Dot(forward, uphill));
                float slope = Mathf.Clamp01((1f - Vector3.Dot(hit.normal, Vector3.up)) * 6f);
                ground = facing * slope;
            }
        }
        return Mathf.Clamp01(Mathf.Max(pitch, ground));
    }
}

public enum RebirthVehicleRequestOutcome : byte { Rejected = 0, Applied = 1, ReadOnly = 2, Indeterminate = 3 }

public sealed class RebirthVehicleRequestResult
{
    public RebirthVehicleAssemblyCarrier Carrier;
    public Vector3i Position;
    public int EntityId;
    public RebirthVehicleAssembly Snapshot;
    public string Message;
    public string ActorId;
    public string Fingerprint;
    public RebirthVehicleRequestOutcome Outcome;
}

public static class RebirthVehicleRequestJournal
{
    private static readonly Dictionary<Guid, RebirthVehicleRequestResult> Completed = new Dictionary<Guid, RebirthVehicleRequestResult>();
    private static readonly Queue<Guid> Order = new Queue<Guid>();
    private static readonly Dictionary<Guid, string> InFlight = new Dictionary<Guid, string>();
    private static readonly object Sync = new object();
    private const int MaxEntries = 512;
    private const string JournalFileName = "RebirthVehicleRequestJournal.xml";
    private static string loadedDirectory = string.Empty;
    private static bool loadFailed;
    private static long generation;
    public static bool IsAvailable { get { EnsureLoaded(); lock (Sync) return !loadFailed && loadedDirectory.Length > 0; } }
    public static int Count { get { EnsureLoaded(); lock (Sync) return Completed.Count; } }

    public static bool TryGet(Guid requestId, out RebirthVehicleRequestResult result)
    {
        result = null;
        if (requestId == Guid.Empty) return false;
        EnsureLoaded();
        lock (Sync)
        {
            RebirthVehicleRequestResult stored;
            if (!Completed.TryGetValue(requestId, out stored)) return false;
            result = CloneResult(stored);
            return true;
        }
    }

    public static bool TryGet(Guid requestId, string actorId, string fingerprint, out RebirthVehicleRequestResult result)
    {
        if (!TryGet(requestId, out result)) return false;
        return result != null && string.Equals(result.ActorId ?? string.Empty, actorId ?? string.Empty, StringComparison.Ordinal) &&
            string.Equals(result.Fingerprint ?? string.Empty, fingerprint ?? string.Empty, StringComparison.Ordinal);
    }

    public static bool TryBegin(Guid requestId, string actorId, string fingerprint, out RebirthVehicleRequestResult completed, out long admittedGeneration)
    {
        admittedGeneration = -1;
        completed = null;
        if (requestId == Guid.Empty) return false;
        EnsureLoaded();
        string key = (actorId ?? string.Empty) + "|" + (fingerprint ?? string.Empty);
        lock (Sync)
        {
            if (loadFailed || loadedDirectory.Length == 0) return false;
            RebirthVehicleRequestResult stored;
            if (Completed.TryGetValue(requestId, out stored))
            {
                completed = CloneResult(stored);
                return false;
            }
            string active;
            if (InFlight.TryGetValue(requestId, out active))
                return false; // concurrent duplicate/conflict cannot execute a second mutation
            admittedGeneration = generation;
            InFlight[requestId] = key;
            return true;
        }
    }

    public static void Abort(Guid requestId, long expectedGeneration)
    {
        if (requestId == Guid.Empty) return;
        lock (Sync) { if (generation == expectedGeneration) InFlight.Remove(requestId); }
    }

    public static bool Remember(Guid requestId, RebirthVehicleRequestResult result, long expectedGeneration)
    {
        if (requestId == Guid.Empty || result == null) return false;
        bool changed = false;
        lock (Sync)
        {
            if (generation != expectedGeneration || loadFailed || loadedDirectory.Length == 0 ||
                !string.Equals(loadedDirectory, GameIO.GetSaveGameDir(), StringComparison.OrdinalIgnoreCase)) return false;
            InFlight.Remove(requestId);
            if (!Completed.ContainsKey(requestId))
            {
                Completed[requestId] = CloneResult(result);
                Order.Enqueue(requestId);
                while (Order.Count > MaxEntries) Completed.Remove(Order.Dequeue());
                changed = true;
            }
            // Serialize publication, temp-file use and reset against this journal.
            if (changed) Persist(loadedDirectory);
            return true;
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            unchecked { generation++; }
            Completed.Clear(); Order.Clear(); InFlight.Clear(); loadedDirectory = string.Empty;
        }
    }

    private static RebirthVehicleRequestResult CloneResult(RebirthVehicleRequestResult result)
    {
        if (result == null) return null;
        return new RebirthVehicleRequestResult
        {
            Carrier = result.Carrier,
            Position = result.Position,
            EntityId = result.EntityId,
            Snapshot = result.Snapshot != null ? result.Snapshot.DeepClone() : null,
            Message = result.Message ?? string.Empty,
            ActorId = result.ActorId ?? string.Empty,
            Fingerprint = result.Fingerprint ?? string.Empty,
            Outcome = result.Outcome
        };
    }

    private static void EnsureLoaded()
    {
        string directory = GameIO.GetSaveGameDir() ?? string.Empty;
        lock (Sync)
        {
            if (directory.Length == 0)
            {
                unchecked { generation++; }
                Completed.Clear(); Order.Clear(); InFlight.Clear();
                loadedDirectory = string.Empty;
                loadFailed = true;
                return;
            }
            if (string.Equals(loadedDirectory, directory, StringComparison.OrdinalIgnoreCase)) return;
            unchecked { generation++; }
            Completed.Clear(); Order.Clear(); InFlight.Clear(); loadedDirectory = directory; loadFailed = false;
            string path = Path.Combine(directory, JournalFileName);
            if (!File.Exists(path))
            {
                // A backup or staged write means this is not a proven fresh journal.
                loadFailed = File.Exists(path + ".bak") || File.Exists(path + ".tmp");
                return;
            }
            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(path);
                XmlElement root = document.DocumentElement;
                int format;
                if (root == null || root.Name != "rebirthVehicleRequestJournal" ||
                    !int.TryParse(root.GetAttribute("format"), out format) || format != 1)
                    throw new InvalidDataException("unsupported vehicle request journal format");
                HashSet<Guid> seen = new HashSet<Guid>();
                foreach (XmlNode node in root.ChildNodes)
                {
                    XmlElement entry = node as XmlElement;
                    if (entry == null) continue;
                    if (entry.Name != "request" || string.IsNullOrWhiteSpace(entry.GetAttribute("actor")) ||
                        string.IsNullOrWhiteSpace(entry.GetAttribute("fingerprint")))
                        throw new InvalidDataException("invalid vehicle request identity");
                    Guid id;
                    int carrierValue, entityId, outcomeValue, x, y, z;
                    if (!Guid.TryParse(entry.GetAttribute("id"), out id) || id == Guid.Empty ||
                        !int.TryParse(entry.GetAttribute("carrier"), out carrierValue) ||
                        carrierValue < byte.MinValue || carrierValue > byte.MaxValue ||
                        !Enum.IsDefined(typeof(RebirthVehicleAssemblyCarrier), (byte)carrierValue) ||
                        !int.TryParse(entry.GetAttribute("entityId"), out entityId) ||
                        !int.TryParse(entry.GetAttribute("outcome"), out outcomeValue) ||
                        outcomeValue < byte.MinValue || outcomeValue > byte.MaxValue ||
                        !Enum.IsDefined(typeof(RebirthVehicleRequestOutcome), (byte)outcomeValue) ||
                        !int.TryParse(entry.GetAttribute("x"), out x) ||
                        !int.TryParse(entry.GetAttribute("y"), out y) ||
                        !int.TryParse(entry.GetAttribute("z"), out z))
                        throw new InvalidDataException("invalid vehicle request record");
                    RebirthVehicleAssembly snapshot = null;
                    string payload = entry.GetAttribute("snapshot") ?? string.Empty;
                    if (payload.Length > 0)
                    {
                        snapshot = RebirthVehicleAssemblySerializer.FromBase64(payload);
                        if (snapshot == null) throw new InvalidDataException("invalid vehicle request snapshot");
                    }
                    if (!seen.Add(id)) throw new InvalidDataException("duplicate vehicle request id");
                    Completed[id] = new RebirthVehicleRequestResult
                    {
                        Carrier = (RebirthVehicleAssemblyCarrier)carrierValue,
                        Position = new Vector3i(x, y, z),
                        EntityId = entityId,
                        Snapshot = snapshot,
                        Message = entry.GetAttribute("message") ?? string.Empty,
                        ActorId = entry.GetAttribute("actor") ?? string.Empty,
                        Fingerprint = entry.GetAttribute("fingerprint") ?? string.Empty,
                        Outcome = (RebirthVehicleRequestOutcome)outcomeValue
                    };
                    Order.Enqueue(id);
                    while (Order.Count > MaxEntries) Completed.Remove(Order.Dequeue());
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[REBIRTH Vehicle] request journal load failed: " + ex.GetType().Name + ": " + ex.Message);
                Completed.Clear(); Order.Clear(); loadFailed = true;
            }
        }
    }

    private static void CommitJournalFile(string temp, string path)
    {
        // Never remove the last committed journal before its replacement is installed.
        // If replacement is unsupported or fails, retain the primary and report failure.
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }

    private static void Persist(string directory)
    {
        // Called under Sync with the directory whose records are being published.
        try
        {
            if (loadFailed || string.IsNullOrEmpty(directory) ||
                !string.Equals(directory, loadedDirectory, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(directory, GameIO.GetSaveGameDir(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Vehicle journal save session changed.");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, JournalFileName);
            string temp = path + ".tmp";
            XmlDocument document = new XmlDocument();
            XmlElement root = document.CreateElement("rebirthVehicleRequestJournal");
            root.SetAttribute("format", "1");
            root.SetAttribute("writtenUtcTicks", DateTime.UtcNow.Ticks.ToString());
            document.AppendChild(root);
            lock (Sync)
            {
                foreach (Guid id in Order)
                {
                    RebirthVehicleRequestResult result;
                    if (!Completed.TryGetValue(id, out result) || result == null) continue;
                    XmlElement entry = document.CreateElement("request");
                    entry.SetAttribute("id", id.ToString("D"));
                    entry.SetAttribute("carrier", ((int)result.Carrier).ToString());
                    entry.SetAttribute("x", result.Position.x.ToString());
                    entry.SetAttribute("y", result.Position.y.ToString());
                    entry.SetAttribute("z", result.Position.z.ToString());
                    entry.SetAttribute("entityId", result.EntityId.ToString());
                    entry.SetAttribute("outcome", ((int)result.Outcome).ToString());
                    entry.SetAttribute("actor", result.ActorId ?? string.Empty);
                    entry.SetAttribute("fingerprint", result.Fingerprint ?? string.Empty);
                    entry.SetAttribute("message", result.Message ?? string.Empty);
                    entry.SetAttribute("snapshot", result.Snapshot != null
                        ? RebirthVehicleAssemblySerializer.ToBase64(result.Snapshot) : string.Empty);
                    root.AppendChild(entry);
                }
            }
            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                document.Save(stream);
                stream.Flush(true);
            }
            CommitJournalFile(temp, path);
        }
        catch (Exception ex)
        {
            // Keep known in-memory outcomes for duplicate replies, but admit no more mutations.
            lock (Sync) loadFailed = true;
            Log.Warning("[REBIRTH Vehicle] request journal save failed; new requests blocked: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

[Preserve]
public sealed class NetPackageRebirthVehicleAssemblyRequest : NetPackage
{
    private Guid requestId;
    private const byte WireVersion=2;
    private string expectedCreationId;
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private RebirthVehicleAssemblyCarrier carrier;
    private Vector3i position;
    private int entityId;
    private Guid assemblyId;
    private long expectedRevision;
    private RebirthVehicleAssemblyAction action;
    private string slotId;
    private RebirthInstalledVehiclePart offeredPart;
    private RebirthVehiclePartSourceLocation sourceLocation;
    private int sourceSlot;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthVehicleAssemblyRequest Setup(Guid requestedRequestId, int id, PersistentPlayerData data, RebirthVehicleAssemblyCarrier requestedCarrier, Vector3i pos, int requestedEntityId, Guid aid, long revision, RebirthVehicleAssemblyAction requestedAction, string requestedSlot, RebirthInstalledVehiclePart requestedOfferedPart, RebirthVehiclePartSourceLocation requestedSourceLocation, int requestedSourceSlot, string requestedCreationId)
    {
        expectedCreationId=requestedCreationId??string.Empty;
        if(expectedCreationId.Length>0&&!RebirthSurvivorRequestScope.TryNormalize(expectedCreationId,out expectedCreationId))
            throw new InvalidDataException("Invalid vehicle request character.");
        requestId = requestedRequestId;
        playerId = id;
        userId = data.PrimaryId;
        carrier = requestedCarrier;
        position = pos;
        entityId = requestedEntityId;
        assemblyId = aid;
        expectedRevision = revision;
        action = requestedAction;
        slotId = requestedSlot ?? string.Empty;
        offeredPart = requestedOfferedPart != null ? requestedOfferedPart.Clone() : null;
        sourceLocation = requestedSourceLocation;
        sourceSlot = requestedSourceSlot;
        return this;
    }

    public override void read(PooledBinaryReader br)
    {
        BinaryReader r = (BinaryReader)br;
        expectedCreationId=null;requestId=Guid.Empty;
        if(r.ReadByte()!=WireVersion)throw new InvalidDataException("Unsupported vehicle request version.");
        string creation=RebirthSurvivorNetworkCodec.ReadBoundedString(r,71);
        if(creation.Length>0&&!RebirthSurvivorRequestScope.TryNormalize(creation,out creation))throw new InvalidDataException("Invalid vehicle request character.");
        expectedCreationId=creation;
        requestId = new Guid(r.ReadBytes(16));
        playerId = r.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(r);
        carrier = (RebirthVehicleAssemblyCarrier)r.ReadByte();
        position = new Vector3i(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
        entityId = r.ReadInt32();
        assemblyId = new Guid(r.ReadBytes(16));
        expectedRevision = r.ReadInt64();
        action = (RebirthVehicleAssemblyAction)r.ReadByte();
        slotId = r.ReadString();
        if (r.ReadBoolean())
        {
            offeredPart = new RebirthInstalledVehiclePart
            {
                ItemName = r.ReadString(),
                Quality = r.ReadInt32(),
                UseTimes = r.ReadSingle(),
                Roll = r.ReadSingle()
            };
        }
        else offeredPart = null;
        sourceLocation = (RebirthVehiclePartSourceLocation)r.ReadByte();
        sourceSlot = r.ReadInt32();
        string itemPayload = RebirthVehicleAssemblySerializer.ReadBoundedPayload(r, RebirthVehicleAssemblySerializer.MaximumBytes);
        if (itemPayload.Length > 262144) throw new InvalidDataException("Vehicle request item payload exceeds limit.");
        if (offeredPart != null) offeredPart.SerializedItemValue = itemPayload;
    }

    public override void write(PooledBinaryWriter bw)
    {
        base.write(bw);
        BinaryWriter w = (BinaryWriter)bw;
        w.Write(WireVersion);
        RebirthSurvivorNetworkCodec.WriteString(w,expectedCreationId??string.Empty,71);
        w.Write(requestId.ToByteArray());
        w.Write(playerId);
        userId.ToStream(w);
        w.Write((byte)carrier);
        w.Write(position.x);
        w.Write(position.y);
        w.Write(position.z);
        w.Write(entityId);
        w.Write(assemblyId.ToByteArray());
        w.Write(expectedRevision);
        w.Write((byte)action);
        w.Write(slotId ?? string.Empty);
        w.Write(offeredPart != null);
        if (offeredPart != null)
        {
            w.Write(offeredPart.ItemName ?? string.Empty);
            w.Write(offeredPart.Quality);
            w.Write(offeredPart.UseTimes);
            w.Write(offeredPart.Roll);
        }
        w.Write((byte)sourceLocation);
        w.Write(sourceSlot);
        w.Write(offeredPart != null ? offeredPart.SerializedItemValue ?? string.Empty : string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null || expectedCreationId == null) return;
        if(RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            RebirthWorldCharacterRecord ownerRecord;
            if(!RebirthWorldCharacterService.TryGet(player,out ownerRecord)||ownerRecord==null||!ownerRecord.IsComplete||
                !RebirthSurvivorRequestScope.Matches(expectedCreationId,ownerRecord.Origin?.CreationId))return;
        }
        else if(expectedCreationId.Length!=0)return;
        var replyConnection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (replyConnection == null || !replyConnection.IsServer) return;
        NetPackageRebirthVehicleAssemblySnapshot reply;
        try
        {
            // Establish reply capability before journal admission or gameplay mutation.
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthVehicleAssemblySnapshot));
            reply = NetPackageManager.GetPackage<NetPackageRebirthVehicleAssemblySnapshot>();
            if (reply == null) return;
        }
        catch (Exception)
        {
            return;
        }
        string actorId = userId != null ? userId.ToString() : string.Empty;
        string fingerprint = BuildFingerprint();
        RebirthVehicleRequestResult completed;
        if (RebirthVehicleRequestJournal.TryGet(requestId, out completed))
        {
            if (!string.Equals(completed.ActorId ?? string.Empty, actorId, StringComparison.Ordinal) ||
                !string.Equals(completed.Fingerprint ?? string.Empty, fingerprint, StringComparison.Ordinal) ||
                !ReauthorizeReply(world, player)) return;
            TrySendReply(replyConnection, reply, requestId, completed.Carrier, completed.Position, completed.EntityId, completed.Snapshot, completed.Message, completed.Outcome);
            return;
        }
        long requestGeneration;
        if (!RebirthVehicleRequestJournal.TryBegin(requestId, actorId, fingerprint, out completed, out requestGeneration))
        {
            if (!RebirthVehicleRequestJournal.IsAvailable)
            {
                // The request may already exist in an unreadable journal: never refund/retry it as rejected.
                TrySendReply(replyConnection, reply, 
                        requestId, carrier, position, entityId, null,
                        "Vehicle request history is unavailable. Ask the server administrator to recover it before retrying.",
                        RebirthVehicleRequestOutcome.Indeterminate);
                return;
            }
            if (completed != null && string.Equals(completed.ActorId ?? string.Empty, actorId, StringComparison.Ordinal) &&
                string.Equals(completed.Fingerprint ?? string.Empty, fingerprint, StringComparison.Ordinal) &&
                ReauthorizeReply(world, player))
                TrySendReply(replyConnection, reply, requestId, completed.Carrier, completed.Position, completed.EntityId, completed.Snapshot, completed.Message, completed.Outcome);
            return;
        }

        try
        {
            EntityVehicle targetEntity = carrier == RebirthVehicleAssemblyCarrier.VehicleEntity ? world.GetEntity(entityId) as EntityVehicle : null;
            Vector3 targetPosition = carrier == RebirthVehicleAssemblyCarrier.VehicleEntity && targetEntity != null ? targetEntity.position : position.ToVector3();
            if (Vector3.Distance(player.position, targetPosition) > 8f) { RebirthVehicleRequestJournal.Abort(requestId, requestGeneration); return; }
            if (targetEntity != null && action != RebirthVehicleAssemblyAction.Read && targetEntity.vehicle != null &&
                targetEntity.vehicle.OwnerId != null && !targetEntity.vehicle.OwnerId.Equals(userId)) { RebirthVehicleRequestJournal.Abort(requestId, requestGeneration); return; }
            string message;
            RebirthVehicleRequestOutcome outcome;
            RebirthVehicleAssembly snapshot = carrier == RebirthVehicleAssemblyCarrier.VehicleEntity
                ? RebirthVehicleAssemblyService.ProcessEntity(world, player, entityId, assemblyId, expectedRevision, action, slotId, offeredPart, sourceLocation, sourceSlot, out message, out outcome)
                : RebirthVehicleAssemblyService.Process(world, player, position, assemblyId, expectedRevision, action, slotId, offeredPart, sourceLocation, sourceSlot, out message, out outcome);
            if (!RebirthVehicleRequestJournal.Remember(requestId, new RebirthVehicleRequestResult { Carrier = carrier, Position = position, EntityId = entityId, Snapshot = snapshot != null ? snapshot.DeepClone() : null, Message = message, ActorId = actorId, Fingerprint = fingerprint, Outcome = outcome }, requestGeneration)) return;
            TrySendReply(replyConnection, reply, requestId, carrier, position, entityId, snapshot, message, outcome);
        }
        catch (Exception ex)
        {
            // Never convert an unknown server-side commit into a retryable rejection. Cache and
            // return an indeterminate terminal result so replay is stable and the client does not
            // automatically refund a resource that may already have committed.
            RebirthVehicleAssembly uncertain = null;
            try
            {
                if (carrier == RebirthVehicleAssemblyCarrier.VehicleEntity)
                {
                    EntityVehicle entity = world.GetEntity(entityId) as EntityVehicle;
                    RebirthVehicleAssembly live;
                    if (entity != null && RebirthVehicleAssemblyIndex.TryGetEntity(entity, out live) && live != null)
                        uncertain = live.DeepClone();
                }
                else
                {
                    TileEntityRepairableVehicleRebirth te = world.GetTileEntity(position) as TileEntityRepairableVehicleRebirth;
                    if (te != null && te.Assembly != null) uncertain = te.Assembly.DeepClone();
                }
            }
            catch { }
            string failureMessage = "Vehicle action outcome is indeterminate: " + ex.GetType().Name + ": " + ex.Message;
            RebirthVehicleRequestResult result = new RebirthVehicleRequestResult
            {
                Carrier = carrier, Position = position, EntityId = entityId, Snapshot = uncertain,
                Message = failureMessage, ActorId = actorId, Fingerprint = fingerprint,
                Outcome = RebirthVehicleRequestOutcome.Indeterminate
            };
            if (!RebirthVehicleRequestJournal.Remember(requestId, result, requestGeneration)) return;
            TrySendReply(replyConnection, reply, 
                    requestId, carrier, position, entityId, uncertain, failureMessage,
                    RebirthVehicleRequestOutcome.Indeterminate);
        }
    }

    private void TrySendReply(ConnectionManager connection, NetPackageRebirthVehicleAssemblySnapshot reply,
        Guid responseId, RebirthVehicleAssemblyCarrier responseCarrier, Vector3i responsePosition,
        int responseEntityId, RebirthVehicleAssembly snapshot, string message, RebirthVehicleRequestOutcome outcome)
    {
        try
        {
            connection.SendPackage(reply.Setup(responseId, responseCarrier, responsePosition,
                responseEntityId, snapshot, message, outcome), _attachedToEntityId: playerId);
        }
        catch (Exception)
        {
            // Transport/serialization failure preserves the journal result for authenticated replay.
        }
    }

    private bool ReauthorizeReply(World world,EntityPlayer player)
    {
        if(world==null||player==null)return false;
        EntityVehicle targetEntity=carrier==RebirthVehicleAssemblyCarrier.VehicleEntity
            ? world.GetEntity(entityId) as EntityVehicle : null;
        Vector3 targetPosition=carrier==RebirthVehicleAssemblyCarrier.VehicleEntity&&targetEntity!=null
            ? targetEntity.position : position.ToVector3();
        if(Vector3.Distance(player.position,targetPosition)>8f)return false;
        if(targetEntity!=null&&action!=RebirthVehicleAssemblyAction.Read&&targetEntity.vehicle!=null&&
            targetEntity.vehicle.OwnerId!=null&&!targetEntity.vehicle.OwnerId.Equals(userId))return false;
        return true;
    }

    private string BuildFingerprint()
    {
        return (expectedCreationId??string.Empty) + "|" + ((byte)carrier) + "|" + position + "|" + entityId + "|" + assemblyId + "|" + expectedRevision + "|" +
            ((byte)action) + "|" + (slotId ?? string.Empty) + "|" + ((byte)sourceLocation) + "|" + sourceSlot + "|" +
            (offeredPart != null ? (string.IsNullOrEmpty(offeredPart.SerializedItemValue) ? string.Empty : offeredPart.SerializedItemValue + ":") + (offeredPart.ItemName ?? string.Empty) + ":" + offeredPart.Quality + ":" + offeredPart.UseTimes + ":" + offeredPart.Roll : string.Empty);
    }

    public int GetLength() { return 8193 + RebirthSurvivorNetworkCodec.EstimateString(expectedCreationId??string.Empty,71) + Encoding.UTF8.GetByteCount(offeredPart?.SerializedItemValue ?? string.Empty); }
}

[Preserve]
public sealed class NetPackageRebirthVehicleAssemblySnapshot : NetPackage
{
    private Guid requestId;
    private RebirthVehicleAssemblyCarrier carrier;
    private Vector3i position;
    private int entityId;
    private string payload;
    private string message;
    private RebirthVehicleRequestOutcome outcome;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRebirthVehicleAssemblySnapshot Setup(Guid requestedRequestId, RebirthVehicleAssemblyCarrier requestedCarrier, Vector3i pos, int requestedEntityId, RebirthVehicleAssembly assembly, string resultMessage, RebirthVehicleRequestOutcome resultOutcome)
    {
        requestId = requestedRequestId; carrier = requestedCarrier; position = pos; entityId = requestedEntityId; payload = assembly != null ? RebirthVehicleAssemblySerializer.ToBase64(assembly) : string.Empty; message = resultMessage ?? string.Empty; outcome = resultOutcome; return this;
    }

    public override void read(PooledBinaryReader br) { BinaryReader r = (BinaryReader)br; requestId = new Guid(r.ReadBytes(16)); carrier = (RebirthVehicleAssemblyCarrier)r.ReadByte(); position = new Vector3i(r.ReadInt32(), r.ReadInt32(), r.ReadInt32()); entityId = r.ReadInt32(); payload = RebirthVehicleAssemblySerializer.ReadBoundedPayload(r, 349528); message = r.ReadString(); outcome = (RebirthVehicleRequestOutcome)r.ReadByte(); }
    public override void write(PooledBinaryWriter bw) { base.write(bw); BinaryWriter w = (BinaryWriter)bw; w.Write(requestId.ToByteArray()); w.Write((byte)carrier); w.Write(position.x); w.Write(position.y); w.Write(position.z); w.Write(entityId); w.Write(payload ?? string.Empty); w.Write(message ?? string.Empty); w.Write((byte)outcome); }
    public override void ProcessPackage(World world, GameManager callbacks) { XUiC_RebirthVehicleRestoration.Receive(requestId, carrier, position, entityId, RebirthVehicleAssemblySerializer.FromBase64(payload), message, outcome); }
    public int GetLength() { return 64 + Encoding.UTF8.GetByteCount(payload ?? string.Empty) + Encoding.UTF8.GetByteCount(message ?? string.Empty); }
}

[Preserve]
public sealed class XUiC_RebirthVehicleRestorationSlot : XUiC_BasePartStack
{
    private XUiC_RebirthVehicleRestoration owner;
    private RebirthVehicleSlotDefinition definition;
    private ItemClass expectedItemClass;

    public string RestorationSlotId { get { return definition != null ? definition.SlotId : string.Empty; } }
    public string ExpectedItemName { get { return definition != null ? definition.ItemName : string.Empty; } }

    public void Configure(XUiC_RebirthVehicleRestoration requestedOwner, RebirthVehicleSlotDefinition requestedDefinition, RebirthInstalledVehiclePart installedPart, int slotNumber)
    {
        owner = requestedOwner;
        definition = requestedDefinition;
        expectedItemClass = definition != null && !string.IsNullOrEmpty(definition.ItemName) ? ItemClass.GetItemClass(definition.ItemName, false) : null;
        SlotNumber = slotNumber;
        StackLocation = XUiC_ItemStack.StackLocationTypes.Part;
        InfoWindow = null;
        SetEmptySpriteName();
        ItemStack = installedPart != null ? RebirthVehicleInventoryTransactions.CreateItemStack(installedPart) : ItemStack.Empty.Clone();
        string tooltip = ItemStack.IsEmpty()
            ? (expectedItemClass != null ? expectedItemClass.GetLocalizedItemName() : (definition != null ? Localization.Get(definition.DisplayKey) : string.Empty))
            : (ItemClass != null ? ItemClass.GetLocalizedItemName() : string.Empty);
        if(installedPart!=null)tooltip += "\n" + string.Format(Localization.Get("xuiRebirthVehiclePartScoreValue"),Mathf.RoundToInt(Mathf.Clamp01(installedPart.Roll)*100f)) + "\n" + Localization.Get("xuiRebirthVehiclePartScoreHint");
        ViewComponent.ToolTip = tooltip;
        ViewComponent.IsVisible = definition != null;
        RefreshBindings();
    }

    public void HideSlot()
    {
        owner = null;
        definition = null;
        expectedItemClass = null;
        InfoWindow = null;
        ItemStack = ItemStack.Empty.Clone();
        ViewComponent.ToolTip = string.Empty;
        ViewComponent.IsVisible = false;
    }

    public override string GetAtlas()
    {
        return ItemStack == null || ItemStack.IsEmpty() ? "ItemIconAtlasGreyscale" : "ItemIconAtlas";
    }

    public override string GetPartName()
    {
        if (ItemStack != null && !ItemStack.IsEmpty() && ItemClass != null) return ItemClass.GetLocalizedItemName();
        if (expectedItemClass != null) return expectedItemClass.GetLocalizedItemName();
        return definition != null ? Localization.Get(definition.DisplayKey) : string.Empty;
    }

    public override void SetEmptySpriteName()
    {
        emptySpriteName = string.Empty;
        if (expectedItemClass == null || definition == null || string.IsNullOrEmpty(definition.ItemName)) return;

        ItemValue expectedValue = ItemClass.GetItem(definition.ItemName, false);
        emptySpriteName = expectedValue != null && !expectedValue.IsEmpty()
            ? expectedValue.GetPropertyOverride("CustomIcon", expectedItemClass.GetIconName())
            : expectedItemClass.GetIconName();
    }

    public override bool CanSwap(ItemStack stack)
    {
        return owner != null && owner.CanInteract && RebirthVehicleInventoryTransactions.Matches(stack, ExpectedItemName);
    }

    public override bool CanRemove()
    {
        return owner != null && owner.CanInteract && ItemStack != null && !ItemStack.IsEmpty();
    }

    public override void SwapItem()
    {
        if (owner == null || !owner.CanInteract) return;
        ItemStack cursor = owner.GetCursorStack();
        if (cursor != null && !cursor.IsEmpty())
        {
            if (!CanSwap(cursor))
            {
                owner.ShowWrongPart(this, cursor);
                return;
            }
            owner.RequestInstallFromCursor(this, cursor);
            return;
        }
        if (CanRemove()) owner.RequestPickup(this);
    }

    public void RequestPreferredRemove()
    {
        if (CanRemove()) owner.RequestRemove(this);
    }
}

[Preserve]
public sealed class XUiC_RebirthVehicleStatHover : XUiController
{
    private string tooltipText = string.Empty;

    public void SetTooltip(string text)
    {
        tooltipText = text ?? string.Empty;
        if (ViewComponent != null) ViewComponent.ToolTip = tooltipText;
        RefreshBindings();
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        if (string.Equals(bindingName, "stattooltip", StringComparison.OrdinalIgnoreCase))
        {
            value = tooltipText;
            return true;
        }
        return base.GetBindingValueInternal(ref value, bindingName);
    }
}

[Preserve]
public sealed class XUiC_RebirthVehicleRestoration : XUiController
{
    public const string WindowGroupNamePrefix = "rebirthVehicleRestorationRows";
    private sealed class PendingClientRequest
    {
        public XUiC_RebirthVehicleRestoration Controller;
        public RebirthVehicleAssemblyAction Action;
        public RebirthVehicleAssemblyCarrier Carrier;
        public Vector3i Position;
        public int EntityId;
        public long ExpectedRevision;
        public ItemStack ReservedCursorItem;
        public ItemStack PickedCursorItem;
        public EntityPlayerLocal Owner;
        public object OwnerWorld;
        public string OwnerCreation;
        public bool HasConfirmedReply;
        public RebirthVehicleAssembly ConfirmedSnapshot;
        public string ConfirmedMessage;
        public RebirthVehicleRequestOutcome ConfirmedOutcome;
    }

    private static XUiC_RebirthVehicleRestoration current;
    private static readonly Dictionary<Guid, PendingClientRequest> PendingClientRequests = new Dictionary<Guid, PendingClientRequest>();
    private static readonly object PendingClientSync = new object();
    private RebirthVehicleAssemblyCarrier carrier;
    private Vector3i position;
    private int entityId;
    private EntityVehicle entityCarrier;
    private RebirthVehicleAssembly assembly;
    private Guid pendingRequestId;
    private Guid lastCompletedRequestId;
    private long pendingExpectedRevision;
    private XUiV_Label title;
    private XUiV_Label status;
    private XUiV_Label progress;
    private XUiV_Label durabilityValue;
    private XUiV_Label fuelValue;
    private readonly Dictionary<string, XUiV_Label> statValues = new Dictionary<string, XUiV_Label>(StringComparer.Ordinal);
    private readonly Dictionary<string, XUiC_RebirthVehicleStatHover> statHoverTargets = new Dictionary<string, XUiC_RebirthVehicleStatHover>(StringComparer.Ordinal);
    private XUiController assemblyPage;
    private XUiController statsPage;
    private bool showStatsPage;
    private XUiV_Label instructions;
    private XUiController siphonButton;
    private XUiController hotwireButton;
    private XUiController repairButton;
    private XUiController refuelButton;
    private bool cursorStateCaptured;
    private bool wasCursorHidden;
    private bool wasCursorLocked;
    private readonly List<XUiC_RebirthVehicleRestorationSlot> slots = new List<XUiC_RebirthVehicleRestorationSlot>();
    private readonly List<XUiV_Label> slotLabels = new List<XUiV_Label>();

    public bool CanInteract { get { return pendingRequestId == Guid.Empty; } }

    public override void Init()
    {
        base.Init();
        current = this;
        title = GetLabel("vehicleTitle");
        status = GetLabel("vehicleStatus");
        progress = GetLabel("vehicleProgress");
        durabilityValue = GetLabel("vehicleDurabilityValue");
        fuelValue = GetLabel("vehicleFuelValue");
        string[] statIds = { "Acceleration", "LowTorque", "HighTorque", "FuelEfficiency", "TurboFuelEfficiency", "TopSpeed", "TurboTopSpeed", "ReverseSpeed", "Response", "HillClimb", "Handling", "Braking" };
        for (int statIndex = 0; statIndex < statIds.Length; statIndex++)
        {
            string statId = statIds[statIndex];
            statValues[statId] = GetLabel("vehicleStat" + statId);
            statHoverTargets[statId] = GetChildById("vehicleStatRow" + statId) as XUiC_RebirthVehicleStatHover;
        }
        ApplyStatTooltips();
        assemblyPage = GetChildById("vehicleAssemblyPage");
        statsPage = GetChildById("vehicleStatsPage");
        XUiController assemblyTab = ResolveClickable("vehicleAssemblyTab");
        XUiController statsTab = ResolveClickable("vehicleStatsTab");
        if (assemblyTab != null) assemblyTab.OnPress += delegate { showStatsPage = false; ApplyPageVisibility(); };
        if (statsTab != null) statsTab.OnPress += delegate { showStatsPage = true; ApplyPageVisibility(); };
        instructions = GetLabel("vehicleInstructions");
        siphonButton = ResolveClickable("vehicleSiphon");
        hotwireButton = ResolveClickable("vehicleHotwire");
        repairButton = ResolveClickable("vehicleRepair");
        refuelButton = ResolveClickable("vehicleRefuel");
        XUiC_RebirthVehicleRestorationSlot[] discoveredSlots = GetChildrenByType<XUiC_RebirthVehicleRestorationSlot>();
        for (int i = 0; i < 20; i++)
        {
            XUiController slotController = GetChildById("vehiclePart" + i);
            XUiC_RebirthVehicleRestorationSlot resolvedSlot = slotController as XUiC_RebirthVehicleRestorationSlot;
            if (resolvedSlot == null && discoveredSlots != null && i < discoveredSlots.Length) resolvedSlot = discoveredSlots[i];
            slots.Add(resolvedSlot);
            slotLabels.Add(GetLabel("vehicleSlot" + i));
            if (slots[i] != null)
            {
                slots[i].SlotNumber = i;
                slots[i].StackLocation = XUiC_ItemStack.StackLocationTypes.Part;
                slots[i].InfoWindow = null;
            }
        }
        if (siphonButton != null) siphonButton.OnPress += delegate { Send(RebirthVehicleAssemblyAction.Siphon, string.Empty, null, false, RebirthVehiclePartSourceLocation.None, -1); };
        if (hotwireButton != null) hotwireButton.OnPress += delegate { Send(RebirthVehicleAssemblyAction.Hotwire, string.Empty, null, false, RebirthVehiclePartSourceLocation.None, -1); };
        if (repairButton != null) repairButton.OnPress += delegate { Send(RebirthVehicleAssemblyAction.Repair, string.Empty, null, false, RebirthVehiclePartSourceLocation.None, -1); };
        if (refuelButton != null) refuelButton.OnPress += delegate { Send(RebirthVehicleAssemblyAction.Refuel, string.Empty, null, false, RebirthVehiclePartSourceLocation.None, -1); };
        XUiController close = GetChildById("vehicleClose");
        if (close != null) close.OnPress += delegate { CloseWindow(); };
    }

    public override void OnOpen()
    {
        base.OnOpen();
        current = this;
        windowGroup.isEscClosable = false;

        CursorControllerAbs cursor = xui != null && xui.playerUI != null ? xui.playerUI.CursorController : null;
        if (cursor != null)
        {
            wasCursorHidden = cursor.GetCursorHidden();
            wasCursorLocked = cursor.Locked;
            cursorStateCaptured = true;
            cursor.Locked = false;
            cursor.SetCursorHidden(false);
            cursor.ResetToCenter();
        }

        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (player != null)
        {
            player.SetControllable(false);
            player.ClearMovementInputs();
        }

        if (pendingRequestId == Guid.Empty)
        {
            lastCompletedRequestId = Guid.Empty;
            pendingExpectedRevision = 0L;
        }
        if (instructions != null) instructions.Text = Localization.Get("xuiRebirthVehicleSlotInstructions");
        Refresh();
    }

    public override void OnClose()
    {
        if (ReferenceEquals(current, this)) current = null;

        RebirthVehicleAssemblyCarrier closingCarrier = carrier;
        EntityVehicle closingEntity = entityCarrier;
        entityCarrier = null;

        CursorControllerAbs cursor = xui != null && xui.playerUI != null ? xui.playerUI.CursorController : null;
        if (cursorStateCaptured && cursor != null)
        {
            cursor.SetCursorHidden(wasCursorHidden);
            cursor.Locked = wasCursorLocked;
        }
        cursorStateCaptured = false;

        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (player != null) player.SetControllable(true);
        base.OnClose();

        if (closingCarrier == RebirthVehicleAssemblyCarrier.VehicleEntity)
        {
            if (closingEntity != null)
                closingEntity.StopUIInteraction();
            else if (LockManager.Instance != null)
                LockManager.Instance.UnlockRequestLocal();
        }
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        RetryConfirmedReply();
        if (windowGroup == null || !windowGroup.isShowing || xui == null || xui.playerUI == null) return;

        if (carrier == RebirthVehicleAssemblyCarrier.VehicleEntity &&
            (entityCarrier == null || !entityCarrier.CheckUIInteraction()))
        {
            CloseWindow();
            return;
        }

        if (XUiUtils.HotkeysAllowedFor(viewComponent) &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased ||
             xui.playerUI.playerInput.GUIActions.Cancel.WasReleased))
            CloseWindow();
    }

    private static bool TryGetRequestCharacter(EntityPlayerLocal player,out string creation)
    {
        creation=string.Empty;
        if(player?.world==null)return false;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return true;
        string current=null;
        if(player.world.IsRemote())current=RebirthSurvivorClientState.GetProjectedCreationId(player);
        else
        {
            RebirthWorldCharacterRecord record;
            if(RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.IsComplete)
                current=record.Origin?.CreationId;
        }
        return RebirthSurvivorRequestScope.TryNormalize(current,out creation);
    }
    private void RetryConfirmedReply()
    {
        if (pendingRequestId == Guid.Empty || xui?.PlayerInventory == null) return;
        PendingClientRequest pending;
        lock (PendingClientSync)
        {
            if (!PendingClientRequests.TryGetValue(pendingRequestId,out pending) ||
                !ReferenceEquals(pending.Controller,this) || !pending.HasConfirmedReply) return;
        }
        // Replay only the authenticated result already received; never resend the mutation.
        Receive(pendingRequestId,pending.Carrier,pending.Position,pending.EntityId,
            pending.ConfirmedSnapshot,pending.ConfirmedMessage,pending.ConfirmedOutcome);
    }
    private void CloseWindow()
    {
        if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null && windowGroup != null)
            xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }

    private static string ResolveWindowGroupName(RebirthVehicleAssembly value)
    {
        RebirthVehicleFamilyDefinition family = value != null
            ? RebirthVehicleDefinitionRegistry.GetFamily(value.FamilyId)
            : null;
        int count = family != null ? family.Slots.Length : 20;
        int rows = Math.Max(1, Math.Min(4, (count + 4) / 5));
        return WindowGroupNamePrefix + rows;
    }

    private static XUiC_RebirthVehicleRestoration FindController(XUi targetXui, RebirthVehicleAssembly value)
    {
        if (targetXui == null) return null;
        XUiController group = targetXui.FindWindowGroupByName(ResolveWindowGroupName(value));
        return group != null ? group.GetChildByType<XUiC_RebirthVehicleRestoration>() : null;
    }

    public static void Open(XUi targetXui, Vector3i pos, RebirthVehicleAssembly initial)
    {
        XUiC_RebirthVehicleRestoration controller = FindController(targetXui, initial);
        if (controller == null) { Log.Error("[REBIRTH Vehicle] restoration XUi controller not found."); return; }
        if (!controller.CanInteract)
        {
            GameManager.ShowTooltip(targetXui.playerUI.entityPlayer, Localization.Get("xuiRebirthVehicleRequestPending"));
            return;
        }
        controller.carrier = RebirthVehicleAssemblyCarrier.RepairableBlock;
        controller.position = pos;
        controller.entityId = -1;
        controller.entityCarrier = null;
        controller.assembly = initial != null ? initial.DeepClone() : null;
        current = controller;
        targetXui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, true, true);
        controller.Send(RebirthVehicleAssemblyAction.Read, string.Empty, null, false, RebirthVehiclePartSourceLocation.None, -1);
    }

    public static bool OpenEntity(XUi targetXui, EntityVehicle entity, RebirthVehicleAssembly initial)
    {
        XUiC_RebirthVehicleRestoration controller = FindController(targetXui, initial);
        if (controller == null || entity == null)
        {
            Log.Error("[REBIRTH Vehicle] live vehicle restoration XUi controller not found.");
            return false;
        }
        if (!controller.CanInteract)
        {
            GameManager.ShowTooltip(targetXui.playerUI.entityPlayer, Localization.Get("xuiRebirthVehicleRequestPending"));
            return false;
        }
        controller.carrier = RebirthVehicleAssemblyCarrier.VehicleEntity;
        controller.position = Vector3i.zero;
        controller.entityId = entity.entityId;
        controller.entityCarrier = entity;
        controller.assembly = initial != null ? initial.DeepClone() : null;
        current = controller;
        targetXui.Vehicle.CurrentVehicle = entity;
        targetXui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, true, true);
        Manager.BroadcastPlayByLocalPlayer(entity.position, "UseActions/service_vehicle");
        controller.Send(RebirthVehicleAssemblyAction.Read, string.Empty, null, false, RebirthVehiclePartSourceLocation.None, -1);
        return true;
    }

    public static void Receive(Guid responseRequestId, RebirthVehicleAssemblyCarrier responseCarrier, Vector3i pos, int responseEntityId, RebirthVehicleAssembly snapshot, string message, RebirthVehicleRequestOutcome outcome)
    {
        PendingClientRequest pending = null;
        if (responseRequestId != Guid.Empty)
        {
            lock (PendingClientSync)
            {
                PendingClientRequests.TryGetValue(responseRequestId, out pending);
            }
        }
        // Every read/mutation snapshot is a reply to Send. Untracked replies are
        // stale (for example after world reset), never updates for the current UI.
        if (pending == null) return;
        XUiC_RebirthVehicleRestoration controller = pending.Controller;
        if (controller == null || controller.carrier != responseCarrier) return;
        if (!ReferenceEquals(pending.Owner,controller.xui?.playerUI?.entityPlayer) || pending.Owner == null ||
            !ReferenceEquals(pending.OwnerWorld,pending.Owner.world)) return;
        string currentCreation;
        if (!TryGetRequestCharacter(pending.Owner,out currentCreation) ||
            !string.Equals(pending.OwnerCreation,currentCreation,StringComparison.Ordinal)) return;
        if (responseRequestId != Guid.Empty && controller.lastCompletedRequestId == responseRequestId) return;
        if (controller.pendingRequestId != Guid.Empty && responseRequestId != Guid.Empty && controller.pendingRequestId != responseRequestId) return;
        if (responseCarrier == RebirthVehicleAssemblyCarrier.RepairableBlock && controller.position != pos) return;
        if (responseCarrier == RebirthVehicleAssemblyCarrier.VehicleEntity && controller.entityId != responseEntityId) return;

        if (pending != null)
        {
            if (pending.Carrier != responseCarrier ||
                (responseCarrier == RebirthVehicleAssemblyCarrier.RepairableBlock && pending.Position != pos) ||
                (responseCarrier == RebirthVehicleAssemblyCarrier.VehicleEntity && pending.EntityId != responseEntityId)) return;
            lock (PendingClientSync)
            {
                PendingClientRequest active;
                if (!PendingClientRequests.TryGetValue(responseRequestId, out active) || !ReferenceEquals(active, pending)) return;
                if (pending.HasConfirmedReply)
                {
                    if (outcome != pending.ConfirmedOutcome) return;
                    // A repeated envelope must not replace the retained definitive result.
                    snapshot = pending.ConfirmedSnapshot;
                    message = pending.ConfirmedMessage;
                }
                // An uncertain or unsupported outcome cannot settle inventory custody.
                // Keep this request available for a later authenticated definitive reply.
                if ((outcome == RebirthVehicleRequestOutcome.ReadOnly && pending.Action != RebirthVehicleAssemblyAction.Read) ||
                    (outcome != RebirthVehicleRequestOutcome.Applied &&
                    outcome != RebirthVehicleRequestOutcome.Rejected &&
                    outcome != RebirthVehicleRequestOutcome.ReadOnly))
                {
                    controller.SetStatus(string.IsNullOrEmpty(message)
                        ? Localization.Get("xuiRebirthVehicleDeliveryUnknown") : message);
                    return;
                }
                bool returnsItem = outcome == RebirthVehicleRequestOutcome.Rejected &&
                    pending.ReservedCursorItem != null && !pending.ReservedCursorItem.IsEmpty();
                bool deliversItem = outcome == RebirthVehicleRequestOutcome.Applied &&
                    pending.PickedCursorItem != null && !pending.PickedCursorItem.IsEmpty();
                if ((returnsItem || deliversItem) &&
                    (controller.xui == null || controller.xui.PlayerInventory == null))
                {
                    if (!pending.HasConfirmedReply)
                    {
                        pending.ConfirmedSnapshot = snapshot != null ? snapshot.DeepClone() : null;
                        pending.ConfirmedMessage = message;
                        pending.ConfirmedOutcome = outcome;
                        pending.HasConfirmedReply = true;
                    }
                    controller.SetStatus(Localization.Get("xuiRebirthVehicleInventoryUnavailable"));
                    return;
                }
                PendingClientRequests.Remove(responseRequestId);
            }
        }

        long expectedRevision = pending != null ? pending.ExpectedRevision : controller.pendingExpectedRevision;
        bool mutationSucceeded = outcome == RebirthVehicleRequestOutcome.Applied;
        bool mutationKnownRejected = outcome == RebirthVehicleRequestOutcome.Rejected;
        if (pending != null && pending.ReservedCursorItem != null && !pending.ReservedCursorItem.IsEmpty() && mutationKnownRejected)
            controller.RestoreReservedCursorItem(pending.ReservedCursorItem);
        if (pending != null && pending.PickedCursorItem != null && !pending.PickedCursorItem.IsEmpty() && mutationSucceeded)
            controller.DeliverConfirmedPickup(pending.PickedCursorItem);

        controller.lastCompletedRequestId = responseRequestId;
        controller.pendingRequestId = Guid.Empty;
        controller.pendingExpectedRevision = 0L;
        controller.assembly = snapshot;
        controller.SetStatus(message);
        controller.Refresh();
    }

    public void RequestInstallFromCursor(XUiC_RebirthVehicleRestorationSlot slot, ItemStack cursor)
    {
        if (slot == null || cursor == null || cursor.IsEmpty() || !CanInteract) return;
        RebirthInstalledVehiclePart offered = RebirthVehicleInventoryTransactions.FromItemStack(cursor, xui.playerUI.entityPlayer.world as World);
        if (offered == null || !string.Equals(offered.ItemName, slot.ExpectedItemName, StringComparison.Ordinal))
        {
            ShowWrongPart(slot, cursor);
            return;
        }
        Send(IsOccupied(slot.RestorationSlotId) ? RebirthVehicleAssemblyAction.Replace : RebirthVehicleAssemblyAction.Install, slot.RestorationSlotId, offered, true, RebirthVehiclePartSourceLocation.None, -1);
    }

    public void RequestRemove(XUiC_RebirthVehicleRestorationSlot slot)
    {
        if (slot == null || !CanInteract || string.IsNullOrEmpty(slot.RestorationSlotId)) return;
        Send(RebirthVehicleAssemblyAction.Remove, slot.RestorationSlotId, null, false, RebirthVehiclePartSourceLocation.None, -1);
    }

    public void RequestPickup(XUiC_RebirthVehicleRestorationSlot slot)
    {
        if (slot == null || !CanInteract || string.IsNullOrEmpty(slot.RestorationSlotId) || slot.ItemStack == null || slot.ItemStack.IsEmpty()) return;
        XUiC_DragAndDropWindow dragAndDrop = GetDragAndDropWindow();
        if (dragAndDrop == null || (dragAndDrop.CurrentStack != null && !dragAndDrop.CurrentStack.IsEmpty())) return;

        ItemStack picked = slot.ItemStack.Clone();
        // Retain a private delivery snapshot; no player-owned item exists until acknowledgement.
        Send(RebirthVehicleAssemblyAction.Pickup, slot.RestorationSlotId, null, false, RebirthVehiclePartSourceLocation.None, -1, picked);
    }

    public bool TryShiftInstallFromInventory(XUiC_ItemStack source)
    {
        if (!ReferenceEquals(current, this) || windowGroup == null || !windowGroup.isShowing || !CanInteract || source == null || source.StackLock || source.ItemStack == null || source.ItemStack.IsEmpty()) return false;
        if (source.StackLocation != XUiC_ItemStack.StackLocationTypes.Backpack && source.StackLocation != XUiC_ItemStack.StackLocationTypes.ToolBelt) return false;
        ItemClass itemClass = source.ItemStack.itemValue != null ? source.ItemStack.itemValue.ItemClass : null;
        if (itemClass == null) return false;
        string itemName = itemClass.GetItemName();
        RebirthVehicleFamilyDefinition family = GetFamily();
        if (family == null) return false;
        RebirthVehicleSlotDefinition firstMatching = null;
        for (int i = 0; i < family.Slots.Length; i++)
        {
            RebirthVehicleSlotDefinition slot = family.Slots[i];
            if (!string.Equals(slot.ItemName, itemName, StringComparison.Ordinal)) continue;
            if (firstMatching == null) firstMatching = slot;
            if (!IsOccupied(slot.SlotId))
            {
                RebirthVehiclePartSourceLocation sourceLocation = source.StackLocation == XUiC_ItemStack.StackLocationTypes.Backpack
                    ? RebirthVehiclePartSourceLocation.Backpack
                    : RebirthVehiclePartSourceLocation.Toolbelt;
                Send(RebirthVehicleAssemblyAction.Install, slot.SlotId, null, false, sourceLocation, source.SlotNumber);
                return true;
            }
        }
        if (firstMatching != null)
        {
            SetStatus(Localization.Get("xuiRebirthVehicleMatchingSlotsFilled"));
            Manager.PlayInsidePlayerHead("ui_denied");
            return true;
        }
        return false;
    }

    public void ShowWrongPart(XUiC_RebirthVehicleRestorationSlot slot, ItemStack cursor)
    {
        string expected = slot != null ? slot.GetPartName() : string.Empty;
        SetStatus(string.Format(Localization.Get("xuiRebirthVehicleWrongPart"), expected));
        Manager.PlayInsidePlayerHead("ui_denied");
    }

    private void Send(RebirthVehicleAssemblyAction action, string slotId, RebirthInstalledVehiclePart offeredPart, bool reserveCursorItem, RebirthVehiclePartSourceLocation sourceLocation, int sourceSlot, ItemStack pickedCursorItem = null)
    {
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (player == null || persistent == null || persistent.PrimaryId == null)
        {
            return;
        }
        if (pendingRequestId != Guid.Empty)
        {
            SetStatus(Localization.Get("xuiRebirthVehicleRequestPending"));
            return;
        }

        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool remote = player.world != null && player.world.IsRemote();
        NetPackageRebirthVehicleAssemblyRequest request = null;
        if (remote)
        {
            try
            {
                NetPackageManager.GetPackageId(typeof(NetPackageRebirthVehicleAssemblyRequest));
                NetPackageManager.GetPackageId(typeof(NetPackageRebirthVehicleAssemblySnapshot));
                request = NetPackageManager.GetPackage<NetPackageRebirthVehicleAssemblyRequest>();
            }
            catch (Exception)
            {
                SetStatus(Localization.Get("xuiRebirthVehicleConnectionUnavailable"));
                return;
            }
        }
        if (player.world == null || (remote ? !RebirthMusicLibraryClient.CanSend(connection, request) : connection == null || !connection.IsServer))
        {
            SetStatus(Localization.Get("xuiRebirthVehicleConnectionUnavailable"));
            return;
        }

        string ownerCreation;
        if (!TryGetRequestCharacter(player,out ownerCreation))
        {
            SetStatus(Localization.Get("xuiRebirthVehicleCharacterUnavailable"));
            return;
        }
        ItemStack reservedCursorItem = null;
        if (reserveCursorItem)
        {
            reservedCursorItem = ReserveOneCursorItem(offeredPart != null ? offeredPart.ItemName : string.Empty);
            if (reservedCursorItem == null || reservedCursorItem.IsEmpty())
            {
                SetStatus(Localization.Get("xuiRebirthVehicleCursorChanged"));
                return;
            }
        }

        pendingRequestId = Guid.NewGuid();
        pendingExpectedRevision = assembly != null ? assembly.Revision : 0L;
        Guid id = assembly != null ? assembly.AssemblyId : Guid.Empty;
        long revision = pendingExpectedRevision;
        lock (PendingClientSync)
        {
            PendingClientRequests[pendingRequestId] = new PendingClientRequest
            {
                Controller = this,
                Owner = player, OwnerWorld = player.world, OwnerCreation = ownerCreation,
                Action = action,
                Carrier = carrier,
                Position = position,
                EntityId = entityId,
                ExpectedRevision = revision,
                ReservedCursorItem = reservedCursorItem,
                PickedCursorItem = pickedCursorItem != null ? pickedCursorItem.Clone() : null
            };
        }

        if (!remote)
        {
            string message;
            RebirthVehicleRequestOutcome outcome;
            RebirthVehicleAssembly snapshot;
            try
            {
                snapshot = carrier == RebirthVehicleAssemblyCarrier.VehicleEntity
                    ? RebirthVehicleAssemblyService.ProcessEntity((World)player.world, player, entityId, id, revision, action, slotId, offeredPart, sourceLocation, sourceSlot, out message, out outcome)
                    : RebirthVehicleAssemblyService.Process((World)player.world, player, position, id, revision, action, slotId, offeredPart, sourceLocation, sourceSlot, out message, out outcome);
            }
            catch (Exception ex)
            {
                snapshot = assembly != null ? assembly.DeepClone() : null;
                message = "Vehicle action outcome is indeterminate: " + ex.GetType().Name + ": " + ex.Message;
                outcome = RebirthVehicleRequestOutcome.Indeterminate;
            }
            Receive(pendingRequestId, carrier, position, entityId, snapshot, message, outcome);
        }
        else
        {
            try
            {
                connection.SendToServer(
                    request.Setup(
                        pendingRequestId, player.entityId, persistent, carrier, position, entityId, id, revision, action, slotId, offeredPart, sourceLocation, sourceSlot, ownerCreation));
            }
            catch (Exception)
            {
                // A transport exception does not prove the server rejected the request.
                // Keep the exact pending identity and reserved cursor custody for reconciliation.
                SetStatus(Localization.Get("xuiRebirthVehicleDeliveryUnknown"));
                Refresh();
                return;
            }
        }
        Refresh();
    }

    public ItemStack GetCursorStack()
    {
        XUiC_DragAndDropWindow dragAndDrop = GetDragAndDropWindow();
        return dragAndDrop != null ? dragAndDrop.CurrentStack : null;
    }

    private XUiC_DragAndDropWindow GetDragAndDropWindow()
    {
        return xui != null ? xui.GetChildByType<XUiC_DragAndDropWindow>() : null;
    }

    private ItemStack ReserveOneCursorItem(string itemName)
    {
        XUiC_DragAndDropWindow dragAndDrop = GetDragAndDropWindow();
        ItemStack cursor = dragAndDrop != null ? dragAndDrop.CurrentStack : null;
        if (!RebirthVehicleInventoryTransactions.Matches(cursor, itemName)) return null;
        ItemStack reserved = new ItemStack(cursor.itemValue.Clone(), 1);
        ItemStack after = cursor.Clone();
        after.count--;
        if (after.count <= 0) after = ItemStack.Empty.Clone();
        dragAndDrop.SetCurrentStack(after, false);
        return reserved;
    }

    private void RestoreReservedCursorItem(ItemStack reserved)
    {
        if (reserved == null || reserved.IsEmpty() || xui == null || xui.PlayerInventory == null) return;
        ItemStack restore = reserved.Clone();
        if (!xui.PlayerInventory.AddItem(restore)) xui.PlayerInventory.DropItem(restore);
    }

    private void DeliverConfirmedPickup(ItemStack picked)
    {
        if (picked == null || picked.IsEmpty()) return;
        XUiC_DragAndDropWindow cursor = GetDragAndDropWindow();
        if (windowGroup != null && windowGroup.isShowing && cursor != null &&
            (cursor.CurrentStack == null || cursor.CurrentStack.IsEmpty()))
        {
            cursor.SetCurrentStack(picked.Clone(), false);
            return;
        }
        // A closed window or occupied cursor must not overwrite the player's other item.
        RestoreReservedCursorItem(picked);
    }

    private bool IsOccupied(string slotId)
    {
        return assembly != null && !string.IsNullOrEmpty(slotId) && assembly.Parts.ContainsKey(slotId);
    }

    private RebirthVehicleFamilyDefinition GetFamily()
    {
        return assembly != null ? RebirthVehicleDefinitionRegistry.GetFamily(assembly.FamilyId) : null;
    }

    private void Refresh()
    {
        RebirthVehicleFamilyDefinition family = GetFamily();
        if (title != null) title.Text = family != null ? Localization.Get(family.DisplayKey) : Localization.Get("xuiRebirthVehicleRestorationTitle");
        int installedCount = assembly != null ? assembly.Parts.Count : 0;
        int requiredCount = family != null ? family.Slots.Length : 0;
        if (progress != null) progress.Text = string.Format(Localization.Get("xuiRebirthVehicleProgress"), installedCount, requiredCount);

        int currentDurability;
        int maximumDurability;
        float fuelPercent;
        ReadDisplayedCondition(out currentDurability, out maximumDurability, out fuelPercent);
        if (durabilityValue != null) durabilityValue.Text = currentDurability + " / " + maximumDurability;
        if (fuelValue != null) fuelValue.Text = Mathf.RoundToInt(Mathf.Clamp01(fuelPercent) * 100f) + "%";

        RebirthVehiclePerformanceSnapshot compiled = assembly != null ? assembly.Performance : null;
        RefreshVehicleStats(compiled);
        ApplyPageVisibility();

        bool usesFuel = family != null && !string.Equals(family.FamilyId, "BicycleRepair", StringComparison.Ordinal);
        XUiController siphonRoot = GetChildById("vehicleSiphon");
        if (siphonRoot != null) siphonRoot.ViewComponent.IsVisible = usesFuel;
        XUiController hotwireRoot = GetChildById("vehicleHotwire");
        if (hotwireRoot != null) hotwireRoot.ViewComponent.IsVisible = carrier == RebirthVehicleAssemblyCarrier.RepairableBlock;
        XUiController repairRoot = GetChildById("vehicleRepair");
        if (repairRoot != null) repairRoot.ViewComponent.IsVisible = carrier == RebirthVehicleAssemblyCarrier.VehicleEntity;
        XUiController refuelRoot = GetChildById("vehicleRefuel");
        if (refuelRoot != null) refuelRoot.ViewComponent.IsVisible = usesFuel && carrier == RebirthVehicleAssemblyCarrier.VehicleEntity;

        for (int i = 0; i < slots.Count; i++)
        {
            bool visible = family != null && i < family.Slots.Length;
            RebirthVehicleSlotDefinition slot = visible ? family.Slots[i] : null;
            RebirthInstalledVehiclePart part = null;
            if (visible && assembly != null) assembly.Parts.TryGetValue(slot.SlotId, out part);
            if (slots[i] != null)
            {
                if (visible) slots[i].Configure(this, slot, part, i);
                else slots[i].HideSlot();
            }
            if (slotLabels[i] != null)
            {
                slotLabels[i].IsVisible = visible;
                slotLabels[i].Text = visible ? FormatSlot(slot, part) : string.Empty;
            }
        }
    }

    private void ApplyPageVisibility()
    {
        if (assemblyPage != null && assemblyPage.ViewComponent != null) assemblyPage.ViewComponent.IsVisible = !showStatsPage;
        if (statsPage != null && statsPage.ViewComponent != null) statsPage.ViewComponent.IsVisible = showStatsPage;
        if (showStatsPage) ApplyStatTooltips();
    }

    private void ApplyStatTooltips()
    {
        RebirthVehicleFamilyDefinition family = GetFamily();
        SetStatTooltip("Acceleration", BuildStatTooltip("Acceleration", family));
        SetStatTooltip("LowTorque", BuildStatTooltip("LowTorque", family));
        SetStatTooltip("HighTorque", BuildStatTooltip("HighTorque", family));
        SetStatTooltip("FuelEfficiency", BuildStatTooltip("FuelEfficiency", family));
        SetStatTooltip("TurboFuelEfficiency", BuildStatTooltip("TurboFuelEfficiency", family));
        SetStatTooltip("TopSpeed", BuildStatTooltip("TopSpeed", family));
        SetStatTooltip("TurboTopSpeed", BuildStatTooltip("TurboTopSpeed", family));
        SetStatTooltip("ReverseSpeed", BuildStatTooltip("ReverseSpeed", family));
        SetStatTooltip("Response", BuildStatTooltip("Response", family));
        SetStatTooltip("HillClimb", BuildStatTooltip("HillClimb", family));
        SetStatTooltip("Handling", BuildStatTooltip("Handling", family));
        SetStatTooltip("Braking", BuildStatTooltip("Braking", family));
    }

    private void SetStatTooltip(string id, string text)
    {
        XUiC_RebirthVehicleStatHover row;
        if (statHoverTargets.TryGetValue(id, out row) && row != null) row.SetTooltip(text);
    }

    private static bool IsLightEngineFamily(string familyId)
    {
        return string.Equals(familyId, "MinibikeRepair", StringComparison.Ordinal) ||
               string.Equals(familyId, "MotorcycleRepair", StringComparison.Ordinal) ||
               string.Equals(familyId, "QuadRepair", StringComparison.Ordinal) ||
               string.Equals(familyId, "GyroRepair", StringComparison.Ordinal);
    }

    private static string BuildStatTooltip(string statId, RebirthVehicleFamilyDefinition family)
    {
        List<string> contributors = new List<string>();
        string familyId = family != null ? family.FamilyId : string.Empty;
        bool lightEngine = IsLightEngineFamily(familyId);

        if (statId == "Acceleration") AddContributor(contributors, family, "Engine");
        else if (statId == "LowTorque")
        {
            AddContributor(contributors, family, "Engine");
            AddContributor(contributors, family, "SparkPlugs");
        }
        else if (statId == "HighTorque") AddContributor(contributors, family, "Engine");
        else if (statId == "FuelEfficiency") AddContributor(contributors, family, "FuelPump");
        else if (statId == "TurboFuelEfficiency") { AddContributor(contributors, family, "FuelPump"); AddContributor(contributors, family, "Carburetor"); }
        else if (statId == "TopSpeed")
        {
            if (lightEngine) AddContributor(contributors, family, "Engine");
            AddContributor(contributors, family, "Transmission");
        }
        else if (statId == "TurboTopSpeed")
        {
            if (lightEngine) AddContributor(contributors, family, "Engine");
            AddContributor(contributors, family, "Alternator");
        }
        else if (statId == "ReverseSpeed") AddContributor(contributors, family, "Battery");
        else if (statId == "Response") AddContributor(contributors, family, "Starter");
        else if (statId == "HillClimb")
        {
            AddContributor(contributors, family, "Engine");
            AddContributor(contributors, family, "SparkPlugs");
            AddContributor(contributors, family, "Radiator");
        }
        else if (statId == "Handling" || statId == "Braking") AddWheelContributor(contributors, family);

        string description;
        switch (statId)
        {
            case "Acceleration": description = Localization.Get("xuiRebirthVehicleStatHintAcceleration"); break;
            case "LowTorque": description = Localization.Get("xuiRebirthVehicleStatHintLowTorque"); break;
            case "HighTorque": description = Localization.Get("xuiRebirthVehicleStatHintHighTorque"); break;
            case "FuelEfficiency": description = Localization.Get("xuiRebirthVehicleStatHintFuelEfficiency"); break;
            case "TurboFuelEfficiency": description = Localization.Get("xuiRebirthVehicleStatHintTurboFuelEfficiency"); break;
            case "TopSpeed": description = Localization.Get("xuiRebirthVehicleStatHintTopSpeed"); break;
            case "TurboTopSpeed": description = Localization.Get("xuiRebirthVehicleStatHintTurboTopSpeed"); break;
            case "ReverseSpeed": description = Localization.Get("xuiRebirthVehicleStatHintReverseSpeed"); break;
            case "Response": description = Localization.Get("xuiRebirthVehicleStatHintResponse"); break;
            case "HillClimb": description = Localization.Get("xuiRebirthVehicleStatHintHillClimb"); break;
            case "Handling": description = Localization.Get("xuiRebirthVehicleStatHintHandling"); break;
            case "Braking": description = Localization.Get("xuiRebirthVehicleStatHintBraking"); break;
            default: description = string.Empty; break;
        }

        string contributorLine;
        if (contributors.Count > 0)
        {
            contributorLine = string.Format(Localization.Get("xuiRebirthVehicleStatContributors"),string.Join(", ", contributors.ToArray()));
        }
        else
        {
            contributorLine = Localization.Get("xuiRebirthVehicleStatNoContributors");
        }
        return contributorLine + "\n" + description;
    }

    private static void AddContributor(List<string> result, RebirthVehicleFamilyDefinition family, string slotId)
    {
        if (result == null || family == null || family.Slots == null) return;
        for (int i = 0; i < family.Slots.Length; i++)
        {
            RebirthVehicleSlotDefinition slot = family.Slots[i];
            if (slot == null || !string.Equals(slot.SlotId, slotId, StringComparison.Ordinal)) continue;
            ItemClass itemClass = ItemClass.GetItemClass(slot.ItemName, false);
            string name = itemClass != null ? itemClass.GetLocalizedItemName() : Localization.Get(slot.DisplayKey);
            if (!string.IsNullOrEmpty(name) && !result.Contains(name)) result.Add(name);
            return;
        }
    }

    private static void AddWheelContributor(List<string> result, RebirthVehicleFamilyDefinition family)
    {
        if (result == null || family == null || family.Slots == null) return;
        for (int i = 0; i < family.Slots.Length; i++)
        {
            RebirthVehicleSlotDefinition slot = family.Slots[i];
            if (slot == null || slot.SlotId.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) < 0) continue;
            ItemClass itemClass = ItemClass.GetItemClass(slot.ItemName, false);
            string name = itemClass != null ? itemClass.GetLocalizedItemName() : Localization.Get(slot.DisplayKey);
            if (!string.IsNullOrEmpty(name) && !result.Contains(name)) result.Add(name);
        }
    }

    private static string GreenModified(float original, float modified, string format, string suffix)
    {
        string left = original.ToString(format) + suffix;
        if (Mathf.Approximately(original, modified)) return left;
        return left + "  ([00FF00]" + modified.ToString(format) + suffix + "[-])";
    }

    private void SetStat(string id, string text)
    {
        XUiV_Label label;
        if (statValues.TryGetValue(id, out label) && label != null) label.Text = text;
    }

    private void RefreshVehicleStats(RebirthVehiclePerformanceSnapshot p)
    {
        RebirthVehicleBaseStats b = RebirthVehicleBaseStatsRegistry.Get(assembly != null ? assembly.ContentId : string.Empty);
        if (p == null) p = new RebirthVehiclePerformanceSnapshot();

        // Display the actual XML/runtime values wherever the game exposes one.  These are
        // the unmodified vehicle value followed by the installed randomized-parts result.
        // Torque is compacted to kN·m only for readability; the underlying value is unchanged.
        float baseLowTorque = b.MotorTorqueForward * 0.001f;
        float modifiedLowTorque = b.MotorTorqueForward * p.AccelerationMultiplier * p.TorqueMultiplier * 0.001f;
        float baseHighTorque = b.MotorTorqueTurboForward * 0.001f;
        float modifiedHighTorque = b.MotorTorqueTurboForward * p.AccelerationMultiplier * 0.001f;
        const float baseAccelerationRate = 2.5f;
        float modifiedAccelerationRate = baseAccelerationRate + Mathf.Max(0f, p.AccelerationMultiplier - 1f) * 6f;
        SetStat("Acceleration", GreenModified(baseAccelerationRate, modifiedAccelerationRate, "0.##", " m/s²"));
        SetStat("LowTorque", GreenModified(baseLowTorque, modifiedLowTorque, "0.##", " kN·m"));
        SetStat("HighTorque", GreenModified(baseHighTorque, modifiedHighTorque, "0.##", " kN·m"));

        // fuelKmPerL is the exact game-scaled economy field used by VPEngine.  The tank uses
        // arbitrary fuel units, so do not claim that the denominator is a real litre.
        float normalEconomy = p.FuelUseMultiplier > 0f ? b.FuelKmPerLiter / p.FuelUseMultiplier : b.FuelKmPerLiter;
        float combinedTurboUse = p.FuelUseMultiplier * p.TurboFuelUseMultiplier;
        float turboBaseEconomy = b.FuelKmPerLiter * 0.5f;
        float turboEconomy = combinedTurboUse > 0f ? b.FuelKmPerLiter / (2f * combinedTurboUse) : turboBaseEconomy;
        SetStat("FuelEfficiency", GreenModified(b.FuelKmPerLiter, normalEconomy, "0.###", " km/L"));
        SetStat("TurboFuelEfficiency", GreenModified(turboBaseEconomy, turboEconomy, "0.###", " km/L"));

        SetStat("TopSpeed", GreenModified(b.VelocityForward, b.VelocityForward * p.VelocityMultiplier, "0.##", " m/s"));
        SetStat("TurboTopSpeed", GreenModified(b.VelocityTurboForward, b.VelocityTurboForward * p.TurboVelocityMultiplier, "0.##", " m/s"));
        SetStat("ReverseSpeed", GreenModified(b.VelocityBackward, b.VelocityBackward * p.ReverseVelocityMultiplier, "0.##", " m/s"));

        // EntityVehicle moves its velocity cap toward the target at 2.5 m/s² while speeding
        // up.  The starter scales that real runtime response rate.
        const float baseThrottleResponse = 2.5f;
        SetStat("Response", GreenModified(baseThrottleResponse, baseThrottleResponse * p.ResponseMultiplier, "0.##", " m/s²"));

        // These percentages are comparative capability ratings, not bonus percentages.
        // The strongest stock ground-vehicle baseline in the supported set is 100%.
        const float hillReferenceTorque = 3500f;
        const float brakingReferenceTorque = 6000f;
        float baseHillRating = hillReferenceTorque > 0f ? b.MotorTorqueForward / hillReferenceTorque * 100f : 0f;
        float modifiedHillRating = hillReferenceTorque > 0f ? b.MotorTorqueForward * p.AccelerationMultiplier * p.TorqueMultiplier * p.HillClimbMultiplier / hillReferenceTorque * 100f : baseHillRating;
        float baseBrakingRating = brakingReferenceTorque > 0f ? b.BrakeTorque / brakingReferenceTorque * 100f : 0f;
        float modifiedBrakingRating = brakingReferenceTorque > 0f ? b.BrakeTorque * p.BrakingMultiplier / brakingReferenceTorque * 100f : baseBrakingRating;
        SetStat("HillClimb", GreenModified(baseHillRating, modifiedHillRating, "0.#", "%"));
        SetStat("Handling", GreenModified(b.SteerRate, b.SteerRate * p.HandlingMultiplier, "0.##", " °/s"));
        SetStat("Braking", GreenModified(baseBrakingRating, modifiedBrakingRating, "0.#", "%"));
    }

    private void ReadDisplayedCondition(out int currentDurability, out int maximumDurability, out float fuelPercent)
    {
        currentDurability = 0;
        maximumDurability = 0;
        fuelPercent = assembly != null && assembly.FuelPercent >= 0f ? assembly.FuelPercent : 0f;
        World world = xui != null && xui.playerUI != null && xui.playerUI.entityPlayer != null ? xui.playerUI.entityPlayer.world as World : null;
        if (world == null) return;
        if (carrier == RebirthVehicleAssemblyCarrier.RepairableBlock)
        {
            BlockValue blockValue = world.GetBlock(position);
            if (blockValue.Block != null)
            {
                maximumDurability = Math.Max(0, blockValue.Block.MaxDamage);
                currentDurability = Mathf.Clamp(maximumDurability - blockValue.damage, 0, maximumDurability);
            }
            return;
        }
        EntityVehicle entity = world.GetEntity(entityId) as EntityVehicle;
        if (entity == null) return;
        currentDurability = Math.Max(0, entity.Health);
        maximumDurability = Math.Max(0, Mathf.RoundToInt(entity.Stats.Health.Max));
        if (entity.vehicle != null)
        {
            float maximumFuel = entity.vehicle.GetMaxFuelLevel();
            fuelPercent = maximumFuel > 0f ? Mathf.Clamp01(entity.vehicle.GetFuelLevel() / maximumFuel) : 0f;
        }
    }

    private XUiController ResolveClickable(string id)
    {
        XUiController root = GetChildById(id);
        if (root == null) return null;
        XUiController clickable = root.GetChildById("clickable");
        return clickable ?? root;
    }

    private static string FormatSlot(RebirthVehicleSlotDefinition slot, RebirthInstalledVehiclePart part)
    {
        if (slot == null) return string.Empty;
        string name = Localization.Get(slot.DisplayKey);
        if (part == null) return name + "\n" + Localization.Get("xuiRebirthVehicleEmpty");
        return name + "\nQ" + part.Quality + "  " + string.Format(Localization.Get("xuiRebirthVehiclePartScoreValue"),Mathf.RoundToInt(Mathf.Clamp01(part.Roll)*100f));
    }

    private XUiV_Label GetLabel(string id)
    {
        XUiController controller = GetChildById(id);
        return controller != null ? controller.ViewComponent as XUiV_Label : null;
    }

    private void SetStatus(string value)
    {
        if (status != null) status.Text = value ?? string.Empty;
    }

    public static void ClearPendingClientRequests()
    {
        List<PendingClientRequest> pending;
        lock (PendingClientSync)
        {
            pending = new List<PendingClientRequest>(PendingClientRequests.Values);
            PendingClientRequests.Clear();
        }
        for (int i = 0; i < pending.Count; i++)
        {
            PendingClientRequest request = pending[i];
            if (request != null && request.Controller != null && request.ReservedCursorItem != null && !request.ReservedCursorItem.IsEmpty())
                request.Controller.RestoreReservedCursorItem(request.ReservedCursorItem);
            if (request != null && request.Controller != null)
            {
                request.Controller.pendingRequestId = Guid.Empty;
                request.Controller.pendingExpectedRevision = 0L;
            }
        }
        current = null;
    }

    public static bool TryHandleInventoryShiftClick(XUiC_ItemStack source)
    {
        return current != null && current.TryShiftInstallFromInventory(source);
    }
}

public static class RebirthVehicleEntityUiIntegration
{
    private static bool TryResolveAssembly(EntityVehicle entity, out RebirthVehicleAssembly assembly)
    {
        assembly = null;
        if (entity == null) return false;
        if (RebirthVehicleAssemblyIndex.TryGetEntity(entity, out assembly) && assembly != null)
            return true;

        if (RebirthVehicleEntityPersistence.TryReadAssemblyFromVehicleItem(entity, out assembly) && assembly != null)
        {
            RebirthVehicleAssemblyIndex.RegisterEntity(entity, assembly);
            return true;
        }

        string entityClassName = string.Empty;
        try
        {
            // EntityClass IDs are signed string hashes in 3.1 and valid vehicle IDs
            // can be negative. Resolve through the supported lookup instead of
            // treating the ID as a non-negative list index.
            EntityClass entityClass = EntityClass.GetEntityClass(entity.entityClass);
            if (entityClass != null)
                entityClassName = entityClass.entityClassName;
        }
        catch { }

        RebirthVehicleContentDefinition content = RebirthVehicleDefinitionRegistry.GetContentForEntity(entityClassName);
        if (content == null || content.Availability != RebirthVehicleContentAvailability.Enabled)
            return false;

        Vehicle vehicle = entity.GetVehicle();
        assembly = new RebirthVehicleAssembly
        {
            AssemblyId = Guid.NewGuid(),
            ContentId = content.ContentId,
            FamilyId = content.FamilyId,
            FuelPercent = vehicle != null && vehicle.GetMaxFuelLevel() > 0f
                ? Mathf.Clamp01(vehicle.GetFuelLevel() / vehicle.GetMaxFuelLevel())
                : -1f
        };
        assembly.Performance = RebirthVehiclePerformanceCompiler.Compile(assembly);
        RebirthVehicleAssemblyIndex.RegisterEntity(entity, assembly);
        return true;
    }

    public static bool BeforeEntityLockedLocal(
        EntityVehicle __instance,
        bool _success,
        PooledBinaryReader _brServerContext,
        ushort _channel, ref bool result)
    {
        // Preserve installed lock admission and response framing. Only replace successful
        // service interaction after the local lock response has been received.
        if (__instance == null || !_success || _channel == 1)
            return true;

        if (!string.Equals(__instance.transientLockCommand, "service", StringComparison.Ordinal))
            return true;

        RebirthVehicleAssembly assembly;
        if (!TryResolveAssembly(__instance, out assembly))
            return true;

        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer()
            : null;
        LocalPlayerUI playerUi = player != null ? LocalPlayerUI.GetUIForPlayer(player) : null;
        if (playerUi == null || playerUi.xui == null)
            return true;

        // Match native EntityVehicle response before replacing StartInteraction.
        // Once consumed, do not fall through and read the same grid twice.
        __instance.bag.ItemGrid.ReadInto(_brServerContext, StreamModeRead.FromServer);
        result = XUiC_RebirthVehicleRestoration.OpenEntity(
            playerUi.xui,
            __instance,
            assembly);
        return false;
    }
}

[Preserve]
public sealed class ConsoleCmdRebirthVehicle : ConsoleCmdAbstract
{
    public override string getDescription() { return "Audits and verifies REBIRTH vehicle restoration definitions and assemblies."; }
    public override string getHelp() { return "rbvehicle audit | families | content | parts | sources | verifyall | validate | inspect <assembly-guid> | persistence-test | concurrency-test"; }
    public override string[] getCommands() { return new[] { "rbvehicle" }; }
    public override void Execute(List<string> args, CommandSenderInfo senderInfo)
    {
        RebirthVehicleDefinitionRegistry.Initialize();
        string command = args != null && args.Count > 0 ? args[0].ToLowerInvariant() : "audit";
        if (command == "families")
        {
            foreach (RebirthVehicleFamilyDefinition f in RebirthVehicleDefinitionRegistry.GetFamiliesSnapshot()) SdtdConsole.Instance.Output(f.FamilyId + " slots=" + f.Slots.Length);
        }
        else if (command == "content")
        {
            foreach (RebirthVehicleContentDefinition c in RebirthVehicleDefinitionRegistry.GetContentsSnapshot()) SdtdConsole.Instance.Output(c.ContentId + " family=" + c.FamilyId + " state=" + c.Availability + " block=" + c.RepairableBlockName + " entity=" + c.EntityClassName + " reason=" + c.AvailabilityReason);
        }
        else if (command == "parts")
        {
            foreach (RebirthVehiclePartDefinition p in RebirthVehiclePartDefinitionRegistry.Snapshot()) SdtdConsole.Instance.Output(p.PartId + " item=" + p.ItemName + " role=" + p.PerformanceRoleId + " families=" + string.Join(",", p.FamilyIds));
        }
        else if (command == "sources")
        {
            foreach (RebirthVehicleSourceProfile p in RebirthVehicleSourceProfileRegistry.Snapshot()) SdtdConsole.Instance.Output(p.Describe());
        }
        else if (command == "verifyall") SdtdConsole.Instance.Output(RebirthVehicleAssemblyIndex.VerifyAll());
        else if (command == "validate")
        {
            SdtdConsole.Instance.Output(RebirthVehicleDefinitionRegistry.ValidateRuntimeBindings());
            SdtdConsole.Instance.Output(RebirthVehiclePartDefinitionRegistry.ValidateRuntimeBindings());
            SdtdConsole.Instance.Output(RebirthVehicleSourceProfileRegistry.Validate());
        }
        else if (command == "inspect")
        {
            Guid id;
            SdtdConsole.Instance.Output(args != null && args.Count > 1 && Guid.TryParse(args[1], out id) ? RebirthVehicleAssemblyIndex.Inspect(id) : "usage: rbvehicle inspect <assembly-guid>");
        }
        else if (command == "persistence-test") SdtdConsole.Instance.Output(RebirthVehicleSelfTests.PersistenceRoundTrip());
        else if (command == "concurrency-test") SdtdConsole.Instance.Output(RebirthVehicleSelfTests.ConcurrencyContract());
        else SdtdConsole.Instance.Output(RebirthVehicleAssemblyIndex.Audit());
    }
}

public static class RebirthVehicleSelfTests
{
    public static string PersistenceRoundTrip()
    {
        RebirthVehicleAssembly source = new RebirthVehicleAssembly { AssemblyId = Guid.NewGuid(), ContentId = "BaseMotorcycle", FamilyId = "MotorcycleRepair", Revision = 7 };
        source.Parts["Engine"] = new RebirthInstalledVehiclePart { SlotId = "Engine", ItemName = "partMotorcycleEngine_FR", Quality = 5, UseTimes = 12.5f, Roll = 0.731f };
        string payload = RebirthVehicleAssemblySerializer.ToBase64(source);
        RebirthVehicleAssembly copy = RebirthVehicleAssemblySerializer.FromBase64(payload);
        bool pass = copy != null && copy.AssemblyId == source.AssemblyId && copy.Revision == source.Revision && copy.Parts.ContainsKey("Engine") && Mathf.Abs(copy.Parts["Engine"].Roll - 0.731f) < 0.0001f;
        return "persistence-test " + (pass ? "PASS" : "FAIL") + " bytes=" + Convert.FromBase64String(payload).Length;
    }

    public static string ConcurrencyContract()
    {
        // Never insert synthetic transactions into the active world's replay history.
        return "concurrency-test NOT RUN: run Tools/GameBridge/test_vehicle_journal_load.mjs offline; live journal unchanged.";
    }
}

[Preserve]
public sealed class RebirthVehicleRestorationModApi : IModApi
{
    private static bool initialized;
    public void InitMod(Mod modInstance)
    {
        if (initialized) return;
        initialized = true;
        RebirthVehicleRestorationInstaller.Install();
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.GameStartDone.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartDoneData>(OnGameStartDone));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        RebirthVehicleAssemblyIndex.Clear();
        RebirthVehicleRequestJournal.Clear();
        XUiC_RebirthVehicleRestoration.ClearPendingClientRequests();
    }

    private static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
    {
        // ItemClass, Block and EntityClass registries are populated only after
        // ModManager.LoadPatchStuff has completed. Validating at GameStarting
        // reports every XML-backed definition as missing even when it exists.
        string bindingReport = RebirthVehicleDefinitionRegistry.ValidateRuntimeBindings();
        if (!string.Equals(bindingReport, "runtimeValidation errors=0", StringComparison.Ordinal))
            Log.Warning("[REBIRTH Vehicle] " + bindingReport);
        else if (RebirthLogSettings.RuntimeInstallLoggingEnabled)
            Log.Out("[REBIRTH Vehicle] " + bindingReport);

        string partReport = RebirthVehiclePartDefinitionRegistry.ValidateRuntimeBindings();
        if (!partReport.StartsWith("vehicle part definitions valid count=", StringComparison.Ordinal))
            Log.Warning("[REBIRTH Vehicle] " + partReport);
        else if (RebirthLogSettings.RuntimeInstallLoggingEnabled)
            Log.Out("[REBIRTH Vehicle] " + partReport);

        string sourceReport = RebirthVehicleSourceProfileRegistry.Validate();
        if (!sourceReport.StartsWith("vehicle source profiles valid count=", StringComparison.Ordinal))
            Log.Warning("[REBIRTH Vehicle] " + sourceReport);
        else if (RebirthLogSettings.RuntimeInstallLoggingEnabled)
            Log.Out("[REBIRTH Vehicle] " + sourceReport);
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        // Do not clear RebirthVehicleAssemblyIndex here. In 3.1 this event fires
        // before World.UnloadWorld and VehicleManager.Cleanup serialize vehicles.dat.
        ClearTransientState();
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        // GameShutdown also fires before VehicleManager.Cleanup. The next
        // GameStarting event performs the authoritative registry reset.
        ClearTransientState();
    }

    private static void ClearTransientState()
    {
        RebirthVehicleRequestJournal.Clear();
        XUiC_RebirthVehicleRestoration.ClearPendingClientRequests();
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.WriteSyncData), new Type[]
{
    typeof(PooledBinaryWriter),
    typeof(ushort)
})]
internal static class RebirthVehicleWriteSyncPatch
{
    private static void Prefix(EntityVehicle __instance, BinaryWriter _bw, ushort syncFlags)
    {
        RebirthVehicleEntityPersistence.BeforeWriteSyncData(__instance, _bw, syncFlags);
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.ReadSyncData), new Type[]
{
    typeof(PooledBinaryReader),
    typeof(ushort),
    typeof(int)
})]
internal static class RebirthVehicleReadSyncPatch
{
    private static void Postfix(EntityVehicle __instance, BinaryReader _br, ushort syncFlags)
    {
        RebirthVehicleEntityPersistence.AfterReadSyncData(__instance, _br, syncFlags);
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.OnAddedToWorld))]
internal static class RebirthVehicleAddedToWorldPatch
{
    private static void Postfix(EntityVehicle __instance)
    {
        RebirthVehicleEntityPersistence.AfterEntityAdded(__instance);
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.PhysicsFixedUpdate))]
internal static class RebirthVehiclePhysicsPatch
{
    private static void Prefix(EntityVehicle __instance, out RebirthVehicleRuntimePerformance.PhysicsState __state)
    {
        RebirthVehicleRuntimePerformance.BeforePhysics(__instance, out __state);
    }

    private static void Postfix(EntityVehicle __instance, RebirthVehicleRuntimePerformance.PhysicsState __state)
    {
        RebirthVehicleRuntimePerformance.AfterPhysics(__instance, __state);
    }

    private static Exception Finalizer(Exception __exception, EntityVehicle __instance, RebirthVehicleRuntimePerformance.PhysicsState __state)
    {
        if (__exception != null) RebirthVehicleRuntimePerformance.RestorePhysics(__instance, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(VPEngine), nameof(VPEngine.Update))]
internal static class RebirthVehicleFuelPerformancePatch
{
    private static void Prefix(VPEngine __instance, out float __state)
    {
        RebirthVehicleRuntimePerformance.BeforeFuel(__instance, out __state);
    }

    private static void Postfix(VPEngine __instance, float __state)
    {
        RebirthVehicleRuntimePerformance.AfterFuel(__instance, __state);
    }

    private static Exception Finalizer(Exception __exception, VPEngine __instance, float __state)
    {
        if (__exception != null) RebirthVehicleRuntimePerformance.RestoreFuel(__instance, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.OnEntityUnload))]
internal static class RebirthVehicleUnloadPatch
{
    private static void Postfix(EntityVehicle __instance)
    {
        RebirthVehicleEntityPersistence.AfterEntityUnload(__instance);
    }
}

[HarmonyPatch(typeof(VehicleManager), nameof(VehicleManager.RemoveTrackedVehicle), new Type[]
{
    typeof(EntityVehicle),
    typeof(EnumRemoveEntityReason)
})]
internal static class RebirthVehicleManagerRemovePatch
{
    private static void Postfix(EntityVehicle _vehicle, EnumRemoveEntityReason _reason)
    {
        RebirthVehicleEntityPersistence.AfterVehicleRemoved(_vehicle);
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.OnLockResponseLocal), new Type[]
{
    typeof(bool),
    typeof(PooledBinaryReader),
    typeof(ushort)
})]
internal static class RebirthVehicleLockedLocalPatch
{
    private static bool Prefix(
        EntityVehicle __instance,
        bool _success,
        PooledBinaryReader _brServerContext,
        ushort _channel, ref bool __result)
    {
        return RebirthVehicleEntityUiIntegration.BeforeEntityLockedLocal(
            __instance,
            _success,
            _brServerContext,
            _channel, ref __result);
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
internal static class RebirthVehicleInventoryShiftClickPatch
{
    private static bool Prefix(XUiC_ItemStack __instance)
    {
        return !XUiC_RebirthVehicleRestoration.TryHandleInventoryShiftClick(__instance);
    }
}

[HarmonyPatch(typeof(XUiC_BasePartStack), nameof(XUiC_BasePartStack.HandleMoveToPreferredLocation))]
internal static class RebirthVehiclePartSlotShiftClickPatch
{
    private static bool Prefix(XUiC_BasePartStack __instance)
    {
        XUiC_RebirthVehicleRestorationSlot slot = __instance as XUiC_RebirthVehicleRestorationSlot;
        if (slot == null) return true;
        slot.RequestPreferredRemove();
        return false;
    }
}

public static class RebirthVehicleRestorationInstaller
{
    private static bool installed;

    public static void Install()
    {
        if (installed) return;
        installed = true;
        RebirthVehicleDefinitionRegistry.Initialize();
        RebirthVehiclePartDefinitionRegistry.Initialize();
        RebirthVehicleSourceProfileRegistry.Initialize();
        RebirthVehicleEntityFactory.Initialize();

        Harmony harmony = new Harmony("rebirth.vehicle.restoration.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehicleWriteSyncPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehicleReadSyncPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehicleAddedToWorldPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehiclePhysicsPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehicleFuelPerformancePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehicleUnloadPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehicleManagerRemovePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehicleLockedLocalPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehicleInventoryShiftClickPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehiclePartSlotShiftClickPatch));

        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Vehicle] restoration system initialized families=" + RebirthVehicleDefinitionRegistry.GetFamiliesSnapshot().Length + " content=" + RebirthVehicleDefinitionRegistry.GetContentsSnapshot().Length); }
    }
}
