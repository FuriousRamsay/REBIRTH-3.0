using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

// RP29 / C007-F17: immutable, store-cached read projections. These types retain
// no mutable DTO, array, set, or byte-buffer reference supplied by a writer.
// Strings and Unity value structs are safe to copy by value. Persistence still
// uses the original DTOs and unchanged XML format. Field coverage is checked by
// continuation04_source_checks.py; no runtime reflection or XML cloning occurs.
public sealed class RebirthNpcReadOnlyArray<T> : IEnumerable<T>
{
    private readonly T[] values;
    // A differently typed source (mutable DTO -> immutable view) uses this factory.
    private RebirthNpcReadOnlyArray(T[] ownedValues) { values = ownedValues; }
    internal static RebirthNpcReadOnlyArray<T> CopyFrom<TSource>(TSource[] source, Func<TSource, T> copy)
    {
        T[] result = new T[source.Length];
        for (int i = 0; i < source.Length; i++) result[i] = copy(source[i]);
        return new RebirthNpcReadOnlyArray<T>(result);
    }
    public int Length { get { return values.Length; } }
    public int Count { get { return values.Length; } }
    public T this[int index] { get { return values[index]; } }
    public IEnumerator<T> GetEnumerator() { return ((IEnumerable<T>)values).GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
}

public sealed class RebirthNpcReadOnlyStringSet : IEnumerable<string>
{
    private readonly HashSet<string> values;
    internal RebirthNpcReadOnlyStringSet(HashSet<string> source)
    {
        values = source == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) :
            new HashSet<string>(source, source.Comparer);
    }
    public int Count { get { return values.Count; } }
    public bool Contains(string value) { return values.Contains(value); }
    public IEnumerator<string> GetEnumerator() { return values.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
}

public sealed class RebirthNpcIdentityRecordView
{
    public readonly RebirthNpcStableId StableNpcId;
    public readonly string Category;
    public readonly string Species;
    public readonly string GeneratedOrAssignedDisplayName;
    public readonly uint NameRevision;
    public readonly long CreationWorldTime;
    public readonly RebirthNpcCreationReason CreationReason;
    public readonly string OriginSpawnGroup;
    public readonly string OriginPrefab;
    public readonly string OriginScenario;
    internal RebirthNpcIdentityRecordView(RebirthNpcIdentityRecord source)
    {
        StableNpcId = source.StableNpcId;
        Category = source.Category;
        Species = source.Species;
        GeneratedOrAssignedDisplayName = source.GeneratedOrAssignedDisplayName;
        NameRevision = source.NameRevision;
        CreationWorldTime = source.CreationWorldTime;
        CreationReason = source.CreationReason;
        OriginSpawnGroup = source.OriginSpawnGroup;
        OriginPrefab = source.OriginPrefab;
        OriginScenario = source.OriginScenario;
    }
}

public sealed class RebirthNpcProfileBindingRecordView
{
    public readonly string ProfileId;
    public readonly int ProfileSchemaVersionAtBind;
    public readonly string ProfileContentHashAtBind;
    public readonly string ProfileCompatibilityClass;
    public readonly uint LastSuccessfulResolutionRevision;
    internal RebirthNpcProfileBindingRecordView(RebirthNpcProfileBindingRecord source)
    {
        ProfileId = source.ProfileId;
        ProfileSchemaVersionAtBind = source.ProfileSchemaVersionAtBind;
        ProfileContentHashAtBind = source.ProfileContentHashAtBind;
        ProfileCompatibilityClass = source.ProfileCompatibilityClass;
        LastSuccessfulResolutionRevision = source.LastSuccessfulResolutionRevision;
    }
}

public sealed class RebirthNpcLifecycleRecordView
{
    public readonly string LifecycleDomain;
    public readonly uint LifecycleRevision;
    public readonly long? PersistentSinceWorldTime;
    public readonly string DismissalState;
    public readonly string RemovalReason;
    public readonly bool TombstoneState;
    public readonly long? TombstoneWorldTime;
    internal RebirthNpcLifecycleRecordView(RebirthNpcLifecycleRecord source)
    {
        LifecycleDomain = source.LifecycleDomain;
        LifecycleRevision = source.LifecycleRevision;
        PersistentSinceWorldTime = source.PersistentSinceWorldTime;
        DismissalState = source.DismissalState;
        RemovalReason = source.RemovalReason;
        TombstoneState = source.TombstoneState;
        TombstoneWorldTime = source.TombstoneWorldTime;
    }
}

public sealed class RebirthNpcOwnershipRecordView
{
    public readonly string OwnershipState;
    public readonly string OwnerPlatformIdOrPersistentPlayerId;
    public readonly string OwnerCharacterIdIfRequired;
    public readonly uint OwnerRevision;
    public readonly long HiredWorldTime;
    public readonly long? DismissedWorldTime;
    public readonly string PermissionPolicyId;
    public readonly string PartyAccessMode;
    internal RebirthNpcOwnershipRecordView(RebirthNpcOwnershipRecord source)
    {
        OwnershipState = source.OwnershipState;
        OwnerPlatformIdOrPersistentPlayerId = source.OwnerPlatformIdOrPersistentPlayerId;
        OwnerCharacterIdIfRequired = source.OwnerCharacterIdIfRequired;
        OwnerRevision = source.OwnerRevision;
        HiredWorldTime = source.HiredWorldTime;
        DismissedWorldTime = source.DismissedWorldTime;
        PermissionPolicyId = source.PermissionPolicyId;
        PartyAccessMode = source.PartyAccessMode;
    }
}

public sealed class RebirthNpcOrderRecordView
{
    public readonly string OrderState;
    public readonly uint OrderRevision;
    public readonly string OrderTarget;
    public readonly string PreviousOrderState;
    internal RebirthNpcOrderRecordView(RebirthNpcOrderRecord source)
    {
        OrderState = source.OrderState;
        OrderRevision = source.OrderRevision;
        OrderTarget = source.OrderTarget;
        PreviousOrderState = source.PreviousOrderState;
    }
}

public sealed class RebirthNpcPresenceRecordView
{
    public readonly string PresenceState;
    public readonly uint PresenceRevision;
    public readonly string TransitionReason;
    public readonly long TransitionStartedWorldTime;
    public readonly long? ExpectedReturnWorldTime;
    public readonly int? SuspendedRuntimeEntityId;
    public readonly uint EmbodimentGeneration;
    public readonly string LastKnownChunkKey;
    internal RebirthNpcPresenceRecordView(RebirthNpcPresenceRecord source)
    {
        PresenceState = source.PresenceState;
        PresenceRevision = source.PresenceRevision;
        TransitionReason = source.TransitionReason;
        TransitionStartedWorldTime = source.TransitionStartedWorldTime;
        ExpectedReturnWorldTime = source.ExpectedReturnWorldTime;
        SuspendedRuntimeEntityId = source.SuspendedRuntimeEntityId;
        EmbodimentGeneration = source.EmbodimentGeneration;
        LastKnownChunkKey = source.LastKnownChunkKey;
    }
}

public sealed class RebirthNpcTransformRecordView
{
    public readonly Vector3 WorldPosition;
    public readonly float RotationYaw;
    public readonly float? OptionalRotationPitch;
    public readonly string ChunkKey;
    public readonly Vector3? LastSafePosition;
    public readonly long? LastSafePositionWorldTime;
    public readonly Vector3? AnchorPosition;
    public readonly float? AnchorRotation;
    public readonly uint TransformRevision;
    internal RebirthNpcTransformRecordView(RebirthNpcTransformRecord source)
    {
        WorldPosition = source.WorldPosition;
        RotationYaw = source.RotationYaw;
        OptionalRotationPitch = source.OptionalRotationPitch;
        ChunkKey = source.ChunkKey;
        LastSafePosition = source.LastSafePosition;
        LastSafePositionWorldTime = source.LastSafePositionWorldTime;
        AnchorPosition = source.AnchorPosition;
        AnchorRotation = source.AnchorRotation;
        TransformRevision = source.TransformRevision;
    }
}

public sealed class RebirthNpcVitalStateRecordView
{
    public readonly int CurrentHealth;
    public readonly int MaximumHealthAtSave;
    public readonly float? CurrentStaminaIfApplicable;
    public readonly string DeathOrIncapacitationState;
    public readonly RebirthNpcReadOnlyArray<string> ActivePersistentBuffSet;
    public readonly uint VitalsRevision;
    internal RebirthNpcVitalStateRecordView(RebirthNpcVitalStateRecord source)
    {
        CurrentHealth = source.CurrentHealth;
        MaximumHealthAtSave = source.MaximumHealthAtSave;
        CurrentStaminaIfApplicable = source.CurrentStaminaIfApplicable;
        DeathOrIncapacitationState = source.DeathOrIncapacitationState;
        ActivePersistentBuffSet = source.ActivePersistentBuffSet == null ? null : RebirthNpcReadOnlyArray<string>.CopyFrom(source.ActivePersistentBuffSet, value => value);
        VitalsRevision = source.VitalsRevision;
    }
}

public sealed class RebirthNpcInventorySlotRecordView
{
    public readonly int SlotIndex;
    public readonly string ItemValueSerialization;
    public readonly int ItemCount;
    public readonly string MetadataOrCustomData;
    public readonly bool Locked;
    public readonly bool Reserved;
    internal RebirthNpcInventorySlotRecordView(RebirthNpcInventorySlotRecord source)
    {
        SlotIndex = source.SlotIndex;
        ItemValueSerialization = source.ItemValueSerialization;
        ItemCount = source.ItemCount;
        MetadataOrCustomData = source.MetadataOrCustomData;
        Locked = source.Locked;
        Reserved = source.Reserved;
    }
}

public sealed class RebirthNpcInventoryRecordView
{
    public readonly int InventorySchemaVersion;
    public readonly int SlotCount;
    public readonly RebirthNpcReadOnlyArray<RebirthNpcInventorySlotRecordView> SlotRecords;
    public readonly RebirthNpcReadOnlyArray<RebirthNpcInventorySlotRecordView> OverflowRecords;
    public readonly uint InventoryRevision;
    public readonly string ContentChecksum;
    internal RebirthNpcInventoryRecordView(RebirthNpcInventoryRecord source)
    {
        InventorySchemaVersion = source.InventorySchemaVersion;
        SlotCount = source.SlotCount;
        SlotRecords = source.SlotRecords == null ? null : RebirthNpcReadOnlyArray<RebirthNpcInventorySlotRecordView>.CopyFrom(source.SlotRecords, value => value == null ? null : new RebirthNpcInventorySlotRecordView(value));
        OverflowRecords = source.OverflowRecords == null ? null : RebirthNpcReadOnlyArray<RebirthNpcInventorySlotRecordView>.CopyFrom(source.OverflowRecords, value => value == null ? null : new RebirthNpcInventorySlotRecordView(value));
        InventoryRevision = source.InventoryRevision;
        ContentChecksum = source.ContentChecksum;
    }
}

public sealed class RebirthNpcEquipmentSlotRecordView
{
    public readonly string SlotId;
    public readonly string ItemValueSerialization;
    public readonly int ItemCount;
    public readonly string MetadataOrCustomData;
    internal RebirthNpcEquipmentSlotRecordView(RebirthNpcEquipmentSlotRecord source)
    {
        SlotId = source.SlotId;
        ItemValueSerialization = source.ItemValueSerialization;
        ItemCount = source.ItemCount;
        MetadataOrCustomData = source.MetadataOrCustomData;
    }
}

public sealed class RebirthNpcEquipmentRecordView
{
    public readonly int EquipmentSchemaVersion;
    public readonly RebirthNpcReadOnlyArray<RebirthNpcEquipmentSlotRecordView> EquipmentSlots;
    public readonly string ActiveHeldSlot;
    public readonly string ActiveAmmoStateIfNotNative;
    public readonly uint EquipmentRevision;
    internal RebirthNpcEquipmentRecordView(RebirthNpcEquipmentRecord source)
    {
        EquipmentSchemaVersion = source.EquipmentSchemaVersion;
        EquipmentSlots = source.EquipmentSlots == null ? null : RebirthNpcReadOnlyArray<RebirthNpcEquipmentSlotRecordView>.CopyFrom(source.EquipmentSlots, value => value == null ? null : new RebirthNpcEquipmentSlotRecordView(value));
        ActiveHeldSlot = source.ActiveHeldSlot;
        ActiveAmmoStateIfNotNative = source.ActiveAmmoStateIfNotNative;
        EquipmentRevision = source.EquipmentRevision;
    }
}

public sealed class RebirthNpcRespawnRecordView
{
    public readonly string RespawnPolicyId;
    public readonly string RespawnState;
    public readonly long? DeathWorldTime;
    public readonly long? EligibleRespawnWorldTime;
    public readonly string PreferredAnchorType;
    public readonly string PreferredAnchorIdentity;
    public readonly Vector3? PreferredAnchorPosition;
    public readonly float? PreferredAnchorRotation;
    public readonly Vector3? FallbackPosition;
    public readonly string RetainedInventoryPolicy;
    public readonly uint RespawnRevision;
    public readonly int AttemptCount;
    public readonly string LastFailureReason;
    internal RebirthNpcRespawnRecordView(RebirthNpcRespawnRecord source)
    {
        RespawnPolicyId = source.RespawnPolicyId;
        RespawnState = source.RespawnState;
        DeathWorldTime = source.DeathWorldTime;
        EligibleRespawnWorldTime = source.EligibleRespawnWorldTime;
        PreferredAnchorType = source.PreferredAnchorType;
        PreferredAnchorIdentity = source.PreferredAnchorIdentity;
        PreferredAnchorPosition = source.PreferredAnchorPosition;
        PreferredAnchorRotation = source.PreferredAnchorRotation;
        FallbackPosition = source.FallbackPosition;
        RetainedInventoryPolicy = source.RetainedInventoryPolicy;
        RespawnRevision = source.RespawnRevision;
        AttemptCount = source.AttemptCount;
        LastFailureReason = source.LastFailureReason;
    }
}

public sealed class RebirthNpcContractRecordView
{
    public readonly string ContractState;
    public readonly string ContractId;
    public readonly long StartedWorldTime;
    public readonly long? ExpiresWorldTime;
    public readonly int WageAmount;
    public readonly long? NextWageWorldTime;
    public readonly string ExpiryDisposition;
    public readonly uint ContractRevision;
    internal RebirthNpcContractRecordView(RebirthNpcContractRecord source)
    {
        ContractState = source.ContractState;
        ContractId = source.ContractId;
        StartedWorldTime = source.StartedWorldTime;
        ExpiresWorldTime = source.ExpiresWorldTime;
        WageAmount = source.WageAmount;
        NextWageWorldTime = source.NextWageWorldTime;
        ExpiryDisposition = source.ExpiryDisposition;
        ContractRevision = source.ContractRevision;
    }
}

public sealed class RebirthNpcWorkRecordView
{
    public readonly string WorkState;
    public readonly string AssignmentId;
    public readonly string WorkType;
    public readonly string TargetReference;
    public readonly RebirthNpcReadOnlyArray<string> ReservationIds;
    public readonly string ProgressCheckpoint;
    public readonly string ResourceSourcePolicy;
    public readonly string OutputDestinationPolicy;
    public readonly uint WorkRevision;
    internal RebirthNpcWorkRecordView(RebirthNpcWorkRecord source)
    {
        WorkState = source.WorkState;
        AssignmentId = source.AssignmentId;
        WorkType = source.WorkType;
        TargetReference = source.TargetReference;
        ReservationIds = source.ReservationIds == null ? null : RebirthNpcReadOnlyArray<string>.CopyFrom(source.ReservationIds, value => value);
        ProgressCheckpoint = source.ProgressCheckpoint;
        ResourceSourcePolicy = source.ResourceSourcePolicy;
        OutputDestinationPolicy = source.OutputDestinationPolicy;
        WorkRevision = source.WorkRevision;
    }
}

public sealed class RebirthNpcMissionRecordView
{
    public readonly string MissionState;
    public readonly string MissionId;
    public readonly long StartedWorldTime;
    public readonly long? ExpectedReturnWorldTime;
    public readonly string Outcome;
    public readonly uint MissionRevision;
    internal RebirthNpcMissionRecordView(RebirthNpcMissionRecord source)
    {
        MissionState = source.MissionState;
        MissionId = source.MissionId;
        StartedWorldTime = source.StartedWorldTime;
        ExpectedReturnWorldTime = source.ExpectedReturnWorldTime;
        Outcome = source.Outcome;
        MissionRevision = source.MissionRevision;
    }
}

public sealed class RebirthNpcControllerFragmentView
{
    public readonly string ControllerTypeId;
    public readonly int FragmentVersion;
    public readonly int FragmentLength;
    public readonly string FragmentChecksum;
    public readonly RebirthNpcReadOnlyArray<byte> Bytes;
    public readonly bool Required;
    internal RebirthNpcControllerFragmentView(RebirthNpcControllerFragment source)
    {
        ControllerTypeId = source.ControllerTypeId;
        FragmentVersion = source.FragmentVersion;
        FragmentLength = source.FragmentLength;
        FragmentChecksum = source.FragmentChecksum;
        Bytes = source.Bytes == null ? null : RebirthNpcReadOnlyArray<byte>.CopyFrom(source.Bytes, value => value);
        Required = source.Required;
    }
}

public sealed class RebirthNpcControllerFragmentSetView
{
    public readonly int FragmentCount;
    public readonly RebirthNpcReadOnlyArray<RebirthNpcControllerFragmentView> Fragments;
    internal RebirthNpcControllerFragmentSetView(RebirthNpcControllerFragmentSet source)
    {
        FragmentCount = source.FragmentCount;
        Fragments = source.Fragments == null ? null : RebirthNpcReadOnlyArray<RebirthNpcControllerFragmentView>.CopyFrom(source.Fragments, value => value == null ? null : new RebirthNpcControllerFragmentView(value));
    }
}

public sealed class RebirthNpcAuditRecordView
{
    public readonly string CreatedByBuild;
    public readonly string LastWrittenByBuild;
    public readonly string LastMutationKind;
    public readonly long LastMutationWorldTime;
    public readonly long LastMutationServerTick;
    public readonly string LastKnownOwnerNameForDiagnosticsOnly;
    public readonly RebirthNpcReadOnlyArray<string> RecoveryFlags;
    public readonly string MigrationSource;
    public readonly string MigrationFingerprint;
    internal RebirthNpcAuditRecordView(RebirthNpcAuditRecord source)
    {
        CreatedByBuild = source.CreatedByBuild;
        LastWrittenByBuild = source.LastWrittenByBuild;
        LastMutationKind = source.LastMutationKind;
        LastMutationWorldTime = source.LastMutationWorldTime;
        LastMutationServerTick = source.LastMutationServerTick;
        LastKnownOwnerNameForDiagnosticsOnly = source.LastKnownOwnerNameForDiagnosticsOnly;
        RecoveryFlags = source.RecoveryFlags == null ? null : RebirthNpcReadOnlyArray<string>.CopyFrom(source.RecoveryFlags, value => value);
        MigrationSource = source.MigrationSource;
        MigrationFingerprint = source.MigrationFingerprint;
    }
}

public sealed class RebirthNpcDomainRevisionManifestView
{
    public readonly string DomainId;
    public readonly string FileName;
    public readonly uint DomainRevision;
    public readonly long Length;
    public readonly string Sha256;
    public readonly bool Required;
    internal RebirthNpcDomainRevisionManifestView(RebirthNpcDomainRevisionManifest source)
    {
        DomainId = source.DomainId;
        FileName = source.FileName;
        DomainRevision = source.DomainRevision;
        Length = source.Length;
        Sha256 = source.Sha256;
        Required = source.Required;
    }
}

public sealed class RebirthNpcPersistentRecordView
{
    public readonly RebirthNpcIdentityRecordView Identity;
    public readonly RebirthNpcProfileBindingRecordView Profile;
    public readonly RebirthNpcLifecycleRecordView Lifecycle;
    public readonly RebirthNpcOwnershipRecordView Ownership;
    public readonly RebirthNpcOrderRecordView Order;
    public readonly RebirthNpcPresenceRecordView Presence;
    public readonly RebirthNpcTransformRecordView Transform;
    public readonly RebirthNpcVitalStateRecordView Vitals;
    public readonly RebirthNpcInventoryRecordView Inventory;
    public readonly RebirthNpcEquipmentRecordView Equipment;
    public readonly RebirthNpcRespawnRecordView Respawn;
    public readonly RebirthNpcContractRecordView Contract;
    public readonly RebirthNpcWorkRecordView Work;
    public readonly RebirthNpcMissionRecordView Mission;
    public readonly RebirthNpcControllerFragmentSetView ControllerFragments;
    public readonly RebirthNpcAuditRecordView Audit;
    public readonly RebirthDogPersistentRecordView Dog;
    public readonly RebirthBoundUndeadPersistentRecordView BoundUndead;
    public readonly RebirthNpcReadOnlyArray<RebirthNpcDomainRevisionManifestView> DomainManifest;
    public readonly RebirthNpcReadOnlyArray<string> UnknownOptionalElements;
    public readonly uint AggregateRevision;
    public readonly string AggregateChecksum;
    internal RebirthNpcPersistentRecordView(RebirthNpcPersistentRecord source)
    {
        Identity = source.Identity == null ? null : new RebirthNpcIdentityRecordView(source.Identity);
        Profile = source.Profile == null ? null : new RebirthNpcProfileBindingRecordView(source.Profile);
        Lifecycle = source.Lifecycle == null ? null : new RebirthNpcLifecycleRecordView(source.Lifecycle);
        Ownership = source.Ownership == null ? null : new RebirthNpcOwnershipRecordView(source.Ownership);
        Order = source.Order == null ? null : new RebirthNpcOrderRecordView(source.Order);
        Presence = source.Presence == null ? null : new RebirthNpcPresenceRecordView(source.Presence);
        Transform = source.Transform == null ? null : new RebirthNpcTransformRecordView(source.Transform);
        Vitals = source.Vitals == null ? null : new RebirthNpcVitalStateRecordView(source.Vitals);
        Inventory = source.Inventory == null ? null : new RebirthNpcInventoryRecordView(source.Inventory);
        Equipment = source.Equipment == null ? null : new RebirthNpcEquipmentRecordView(source.Equipment);
        Respawn = source.Respawn == null ? null : new RebirthNpcRespawnRecordView(source.Respawn);
        Contract = source.Contract == null ? null : new RebirthNpcContractRecordView(source.Contract);
        Work = source.Work == null ? null : new RebirthNpcWorkRecordView(source.Work);
        Mission = source.Mission == null ? null : new RebirthNpcMissionRecordView(source.Mission);
        ControllerFragments = source.ControllerFragments == null ? null : new RebirthNpcControllerFragmentSetView(source.ControllerFragments);
        Audit = source.Audit == null ? null : new RebirthNpcAuditRecordView(source.Audit);
        Dog = source.Dog == null ? null : new RebirthDogPersistentRecordView(source.Dog);
        BoundUndead = source.BoundUndead == null ? null : new RebirthBoundUndeadPersistentRecordView(source.BoundUndead);
        DomainManifest = source.DomainManifest == null ? null : RebirthNpcReadOnlyArray<RebirthNpcDomainRevisionManifestView>.CopyFrom(source.DomainManifest, value => value == null ? null : new RebirthNpcDomainRevisionManifestView(value));
        UnknownOptionalElements = source.UnknownOptionalElements == null ? null : RebirthNpcReadOnlyArray<string>.CopyFrom(source.UnknownOptionalElements, value => value);
        AggregateRevision = source.AggregateRevision;
        AggregateChecksum = source.AggregateChecksum;
    }
}

public sealed class RebirthDogPersistentRecordView
{
    public readonly int SchemaVersion;
    public readonly string BreedId;
    public readonly bool IsTamedWild;
    public readonly string SourceEntityClass;
    public readonly string TamedEntityClass;
    public readonly string SpeciesCategory;
    public readonly int AnimalCapacityCost;
    public readonly long TamedUtcTicks;
    public readonly long LastCareUtcTicks;
    public readonly RebirthCompanionBehaviorMode CombatMode;
    public readonly bool AttackStopped;
    public readonly int Level;
    public readonly int MiningLevel;
    public readonly int KillCount;
    public readonly float Training;
    public readonly float Bond;
    public readonly bool LegacyCommandGrandfathered;
    public readonly RebirthNpcReadOnlyStringSet LearnedCommands;
    public readonly bool OfflineParked;
    public readonly Vector3? OfflineParkPosition;
    public readonly RebirthDogLifecycleKind Lifecycle;
    public readonly string PickupNonce;
    public readonly uint PickupRevision;
    public readonly bool PickupNonceConsumed;
    public readonly bool PickupRecoveryPending;
    public readonly string PickupRecoveryNonce;
    public readonly string PickupRecoveryStage;
    public readonly long PickupRecoveryUtcTicks;
    public readonly Vector3? StayPosition;
    public readonly bool GuardIsStationaryStay;
    public readonly Vector3? RespawnFallback;
    public readonly uint Revision;
    internal RebirthDogPersistentRecordView(RebirthDogPersistentRecord source)
    {
        SchemaVersion = source.SchemaVersion;
        BreedId = source.BreedId;
        IsTamedWild = source.IsTamedWild;
        SourceEntityClass = source.SourceEntityClass;
        TamedEntityClass = source.TamedEntityClass;
        SpeciesCategory = source.SpeciesCategory;
        AnimalCapacityCost = source.AnimalCapacityCost;
        TamedUtcTicks = source.TamedUtcTicks;
        LastCareUtcTicks = source.LastCareUtcTicks;
        CombatMode = source.CombatMode;
        AttackStopped = source.AttackStopped;
        Level = source.Level;
        MiningLevel = source.MiningLevel;
        KillCount = source.KillCount;
        Training = source.Training;
        Bond = source.Bond;
        LegacyCommandGrandfathered = source.LegacyCommandGrandfathered;
        LearnedCommands = new RebirthNpcReadOnlyStringSet(source.LearnedCommands);
        OfflineParked = source.OfflineParked;
        OfflineParkPosition = source.OfflineParkPosition;
        Lifecycle = source.Lifecycle;
        PickupNonce = source.PickupNonce;
        PickupRevision = source.PickupRevision;
        PickupNonceConsumed = source.PickupNonceConsumed;
        PickupRecoveryPending = source.PickupRecoveryPending;
        PickupRecoveryNonce = source.PickupRecoveryNonce;
        PickupRecoveryStage = source.PickupRecoveryStage;
        PickupRecoveryUtcTicks = source.PickupRecoveryUtcTicks;
        StayPosition = source.StayPosition;
        GuardIsStationaryStay = source.GuardIsStationaryStay;
        RespawnFallback = source.RespawnFallback;
        Revision = source.Revision;
    }
}

public sealed class RebirthBoundUndeadPersistentRecordView
{
    public readonly int SchemaVersion;
    public readonly string SourceEntityClass;
    public readonly string TierId;
    public readonly int CapacityCost;
    public readonly float ConditioningProgress;
    public readonly string ConditioningStage;
    public readonly bool IsBound;
    public readonly bool IsSummoned;
    public readonly string Lifecycle;
    public readonly int LastEntityId;
    public readonly int KillCount;
    public readonly float Training;
    public readonly float BindingStability;
    public readonly long BoundUtcTicks;
    public readonly long LastConditioningUtcTicks;
    public readonly byte BoundFactionId;
    public readonly byte BoundFactionRank;
    public readonly bool AttackStopped;
    public readonly uint Revision;
    internal RebirthBoundUndeadPersistentRecordView(RebirthBoundUndeadPersistentRecord source)
    {
        SchemaVersion = source.SchemaVersion;
        SourceEntityClass = source.SourceEntityClass;
        TierId = source.TierId;
        CapacityCost = source.CapacityCost;
        ConditioningProgress = source.ConditioningProgress;
        ConditioningStage = source.ConditioningStage;
        IsBound = source.IsBound;
        IsSummoned = source.IsSummoned;
        Lifecycle = source.Lifecycle;
        LastEntityId = source.LastEntityId;
        KillCount = source.KillCount;
        Training = source.Training;
        BindingStability = source.BindingStability;
        BoundUtcTicks = source.BoundUtcTicks;
        LastConditioningUtcTicks = source.LastConditioningUtcTicks;
        BoundFactionId = source.BoundFactionId;
        BoundFactionRank = source.BoundFactionRank;
        AttackStopped = source.AttackStopped;
        Revision = source.Revision;
    }
}

