using System;
using System.Collections.Generic;

#nullable disable

public enum RebirthIngestionKind : byte
{
    Fluid = 0,
    Food = 1,
    Supplement = 2
}

public enum RebirthMetabolismSourceKind : byte
{
    HeldToolbelt = 0,
    Backpack = 1,
    HydrationSlot = 2
}

public enum RebirthMetabolismConsumeFailure : byte
{
    None = 0,
    InvalidPlayer = 1,
    InvalidItem = 2,
    NotAuthoritative = 3,
    TooFull = 4,
    AlreadyHydrated = 5,
    NoLiquid = 6,
    ItemChanged = 7,
    SlotMismatch = 8,
    UnsafeAutoSip = 9,
    Cooldown = 10,
    Nauseated = 11,
    IngestionQueueFull = 12
}

public struct RebirthConsumeResult
{
    public bool Success;
    public RebirthMetabolismConsumeFailure Failure;
    public float ConsumedMl;
    public float RemainingMl;
    public int AddedEntryId;
    public bool CreatedEmptyItem;
    public string Message;
}

public sealed class RebirthConsumableDefinition
{
    public string ItemName = string.Empty;
    public bool IsMetabolismItem;
    public bool IsDrink;
    public bool IsFood;
    public bool IsSupplement;
    public RebirthIngestionKind Kind;
    public string LiquidProfile = string.Empty;
    public string DigestionProfile = string.Empty;
    public string FoodSafetyProfile = string.Empty;
    public string SupplementProfile = string.Empty;
    public float ContainerCapacityMl;
    public float InitialVolumeMl;
    public float ManualSipMl;
    public float AutoSipMl;
    public bool HydrationEquippable;
    public bool AutoSipSafe;
    public bool ReusableContainer;
    public string EmptyItem = string.Empty;
    public bool AllowConsumeWhenHydrated;
    public float NutritionUnits;
    public float EnergyUnits;
    public float FoodWaterMl;
    public float StomachVolumeMl;
    public float LiquidHydrationYield = 1f;

    /// <summary>Independent copy (all fields are plain values or immutable strings).</summary>
    public RebirthConsumableDefinition Clone() { return (RebirthConsumableDefinition)MemberwiseClone(); }
}

public sealed class RebirthIngestionEntry
{
    public int EntryId;
    public RebirthIngestionKind Kind;
    public string SourceItemName = string.Empty;
    public string LiquidProfileId = string.Empty;
    public string DigestionProfileId = string.Empty;
    public string FoodSafetyProfileId = string.Empty;
    public string ContaminationProfileId = string.Empty;
    public ulong IngestedWorldTime;

    // Stomach compartment. These values are what contribute to the visible stomach/fullness bar.
    public float RemainingFluidVolumeMl;
    public float RemainingSolidVolumeMl;
    public float RemainingNutritionUnits;
    public float RemainingEnergyUnits;

    // Intestinal compartment. Material must leave the stomach before it can be absorbed into
    // the player's body reserves. Intestinal contents do not count toward stomach fullness.
    public float IntestinalFluidVolumeMl;
    public float IntestinalSolidVolumeMl;
    public float IntestinalNutritionUnits;
    public float IntestinalEnergyUnits;

    // Intestinal residence state. Gastric output becomes visible intestine content first;
    // it cannot be absorbed until it has spent a readable amount of active real time here.
    public float IntestinalFluidResidenceSecondsRemaining;
    public float IntestinalNutritionResidenceSecondsRemaining;
    // Set on the first real nutrient absorption so one consumed food creates at most one meaningful-meal event.
    public bool MeaningfulMealCredited;

    // Gastric residence/emptying state. Hold timers are real-time gameplay seconds so digestion
    // remains readable and consistent across 60/90/120-minute day settings.
    public float FluidHoldSecondsRemaining;
    public float SolidHoldSecondsRemaining;
    public float FluidGastricHalfTimeSecondsSnapshot;
    public bool FluidIsMealBound;

    public float FluidYieldMultiplierSnapshot = 1f;
    public float NutrientYieldMultiplierSnapshot = 1f;
    public float DigestionRateMultiplierSnapshot = 1f;
    public float PreparedMealEnergyEfficiencyMultiplier = 1f;
    // Chunk K: craft-time prepared-drink duration carried into digestion; activation occurs once on first intestinal fluid absorption.
    public float PreparedDrinkSpecialEffectDurationSeconds;
    public bool PreparedDrinkSpecialEffectActivated;
}

public sealed class RebirthMetabolismTimedEffect
{
    public string ProfileId = string.Empty;
    public float RemainingRealSeconds;
    public int Stacks = 1;
}

public struct RebirthMetabolismModifiers
{
    public float HydrationRequirement;
    public float FoodRequirement;
    public float FluidAbsorption;
    public float FluidUtilization;
    public float NutrientUtilization;
    public float DigestionSpeed;
    public float GutResilience;
    public float StomachCapacity;
    public float DigestiveBaselineOffset;

    // Survivor condition/Trait layer. These stay neutral in Base Game progression worlds.
    public float TotalHydrationDemand;
    public float TotalNutritionDemand;
    public float EnergyUse;
    public float EnergyRecovery;
    public float HeatPenalty;
    public float ColdPenalty;
    public float DigestiveRecovery;

    public static RebirthMetabolismModifiers Neutral(float stomachCapacity)
    {
        return new RebirthMetabolismModifiers
        {
            HydrationRequirement = 1f,
            FoodRequirement = 1f,
            FluidAbsorption = 1f,
            FluidUtilization = 1f,
            NutrientUtilization = 1f,
            DigestionSpeed = 1f,
            GutResilience = 1f,
            StomachCapacity = stomachCapacity,
            DigestiveBaselineOffset = 0f,
            TotalHydrationDemand = 1f,
            TotalNutritionDemand = 1f,
            EnergyUse = 1f,
            EnergyRecovery = 1f,
            HeatPenalty = 1f,
            ColdPenalty = 1f,
            DigestiveRecovery = 1f
        };
    }
}

public struct RebirthMetabolismSnapshot
{
    // Recipient identity and monotonic publication sequence prevent a listen host from
    // displaying another connected player when revisions happen to match.
    public int OwnerEntityId;
    public string CreationId; // Network projection scope; not persisted metabolism state.
    public long Sequence;
    public int Revision;
    public float Hydration;
    public float HydrationMax;
    public float Food;
    public float FoodMax;
    public float Energy;
    public float EnergyMax;
    public float PendingHydration;
    public float PendingFood;
    public float PendingEnergy;
    public float ProjectedHydration;
    public float ProjectedFood;
    public float ProjectedEnergy;
    public float StomachFluidMl;
    public float StomachSolidMl;
    public float FullnessMl;
    public float StomachCapacityMl;
    public float IntestinalFluidMl;
    public float IntestinalSolidMl;
    public float IntestinalCapacityMl;
    public float IntestinalNutritionUnits;
    public float IntestinalEnergyUnits;
    public float IntestinalContentPercent;
    public float IntestinalFluidResidenceSecondsRemaining;
    public float IntestinalNutritionResidenceSecondsRemaining;
    // Share of all currently pending hydration/nutrition/beverage-Energy that has already reached
    // the intestine. Kept for detailed diagnostics/backward-compatible bindings.
    public float IntestinalStagePercent;
    // What actually moved during the latest metabolism tick. The HUD uses these as secondary
    // flow diagnostics; the main intestine meter is always based on persistent physical contents.
    public float GastricFluidTransferMlPerRealMinute;
    public float GastricSolidTransferMlPerRealMinute;
    public float FluidAbsorbedMlPerRealMinute;
    public float HydrationGainPointsPerRealMinute;
    public float HydrationNetPointsPerRealMinute;
    public float NutritionAbsorbedUnitsPerRealMinute;
    public float NutritionGainPointsPerRealMinute;
    public float NutritionNetPointsPerRealMinute;
    public float BeverageEnergyGainPerRealMinute;
    public float IntestinalActivityPercent;
    public float GastricHoldSecondsRemaining;
    public float FluidGastricHoldSecondsRemaining;
    public float SolidGastricHoldSecondsRemaining;
    public float GastricEmptyingMlPerRealMinute;
    public float DigestiveHealth;
    public float HydrationLossMlPerReal60Minutes;
    public float FoodUsePerReal60Minutes;
    public float EnergyUsePerRealMinute;
    public float EnergyRecoveryPerRealMinute;
    public float EnergyRecoveryNutritionUsePerRealMinute;
    public float EnergyRecoveryHydrationMultiplier;
    public float StaminaRecoveryMultiplier;
    public float FluidAbsorptionMlPerRealMinute;
    public float SolidGastricEmptyingMlPerRealMinute;
    public float NutrientAbsorptionUnitsPerRealMinute;
    public float HydrationRequirementMultiplier;
    public float FoodRequirementMultiplier;
    public float FluidAbsorptionMultiplier;
    public float FluidUtilizationMultiplier;
    public float NutrientUtilizationMultiplier;
    public float DigestionSpeedMultiplier;
    public float GutResilienceMultiplier;
    public bool AutoSipEnabled;
    public string HydrationSlotItemName;
    public float HydrationSlotVolumeMl;
    public float HydrationSlotCapacityMl;
    public string HydrationSlotLiquidProfile;

    // Presentation-only summary of the most recent ingestion entry that still has material
    // in either the stomach or intestinal compartment. This lets the narrow Character tab
    // show the actual item icon/name without replicating the whole ingestion list.
    public string ActiveIntakeItemName;
    public float ActiveIntakeRemainingMl;

    public float FullnessPercent
    {
        get { return StomachCapacityMl > 0.001f ? UnityEngine.Mathf.Clamp01(FullnessMl / StomachCapacityMl) : 0f; }
    }
}

public sealed class RebirthMetabolismState
{
    public const int CurrentVersion = 9;

    public int Version = CurrentVersion;
    public bool Initialized;
    /// <summary>
    /// Technical Survivor creation generation that owns this fresh metabolism state.
    /// Empty means a pre-Survivor/legacy metabolism save. It is never client-authored.
    /// </summary>
    public string SurvivorCreationId = string.Empty;
    public float DigestiveHealth = 100f;
    public float Energy = 100f;
    public bool StartupSanitized;
    public readonly List<RebirthIngestionEntry> IngestionEntries = new List<RebirthIngestionEntry>();
    public readonly List<RebirthMetabolismTimedEffect> TimedEffects = new List<RebirthMetabolismTimedEffect>();
    public bool AutoSipEnabled = true;
    public float AutoSipCooldownRemainingRealSeconds;
    public ItemStack HydrationSlotItem = ItemStack.Empty.Clone();
    public ulong LastProcessedWorldTime;
    public int NextEntryId = 1;

    // Runtime-only fields.
    public float NextRealTickTime;
    public float LastRealProcessTime;
    public float LastStamina = -1f;
    public float SmoothedActivity;
    // Runtime-only dysentery episode edge state. The base dysentery attack buff is the cadence
    // source; REBIRTH alternates each new attack between intestinal diarrhea and vomiting.
    public bool DysenteryAttackWasActive;
    public bool DysenteryNextAttackVomiting;
    public int Revision;
    public int LastReplicatedRevision = -1;
    public bool Dirty;
    public int RuntimeEntityId = -1;

    // Latest real flow rates. Runtime-only: they are diagnostic/UI observations, not save data.
    public float LastGastricFluidTransferMlPerRealMinute;
    public float LastGastricSolidTransferMlPerRealMinute;
    public float LastFluidAbsorbedMlPerRealMinute;
    public float LastHydrationGainPointsPerRealMinute;
    public float LastNutritionAbsorbedUnitsPerRealMinute;
    public float LastNutritionGainPointsPerRealMinute;
    public float LastBeverageEnergyGainPerRealMinute;
    public float LastEnergyRecoveryNutritionUsePerRealMinute;
    public float LastIntestinalActivityPercent;

    public int AllocateEntryId()
    {
        int id = NextEntryId;
        if (NextEntryId == int.MaxValue)
            NextEntryId = 1;
        else
            NextEntryId++;
        return id;
    }

    public void Touch()
    {
        Dirty = true;
        unchecked { Revision++; }
        if (Revision < 0) Revision = 1;
    }
}
