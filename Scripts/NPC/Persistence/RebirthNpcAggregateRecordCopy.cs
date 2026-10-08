using System;
using UnityEngine;

#nullable disable

// Explicit typed ownership copies. Used at mutation/import boundaries and by the
// legacy detached-copy APIs, never by an unchanged cached read-view lookup.
internal static class RebirthNpcAggregateRecordCopy
{
    private static T[] CopyArray<T>(T[] source, Func<T, T> copy)
    {
        if (source == null) return null;
        T[] result = new T[source.Length];
        for (int i = 0; i < source.Length; i++) result[i] = copy(source[i]);
        return result;
    }
    internal static RebirthNpcIdentityRecord Copy(RebirthNpcIdentityRecord source)
    {
        if (source == null) return null;
        RebirthNpcIdentityRecord result = new RebirthNpcIdentityRecord();
        result.StableNpcId = source.StableNpcId;
        result.Category = source.Category;
        result.Species = source.Species;
        result.GeneratedOrAssignedDisplayName = source.GeneratedOrAssignedDisplayName;
        result.NameRevision = source.NameRevision;
        result.CreationWorldTime = source.CreationWorldTime;
        result.CreationReason = source.CreationReason;
        result.OriginSpawnGroup = source.OriginSpawnGroup;
        result.OriginPrefab = source.OriginPrefab;
        result.OriginScenario = source.OriginScenario;
        return result;
    }
    internal static RebirthNpcProfileBindingRecord Copy(RebirthNpcProfileBindingRecord source)
    {
        if (source == null) return null;
        RebirthNpcProfileBindingRecord result = new RebirthNpcProfileBindingRecord();
        result.ProfileId = source.ProfileId;
        result.ProfileSchemaVersionAtBind = source.ProfileSchemaVersionAtBind;
        result.ProfileContentHashAtBind = source.ProfileContentHashAtBind;
        result.ProfileCompatibilityClass = source.ProfileCompatibilityClass;
        result.LastSuccessfulResolutionRevision = source.LastSuccessfulResolutionRevision;
        return result;
    }
    internal static RebirthNpcLifecycleRecord Copy(RebirthNpcLifecycleRecord source)
    {
        if (source == null) return null;
        RebirthNpcLifecycleRecord result = new RebirthNpcLifecycleRecord();
        result.LifecycleDomain = source.LifecycleDomain;
        result.LifecycleRevision = source.LifecycleRevision;
        result.PersistentSinceWorldTime = source.PersistentSinceWorldTime;
        result.DismissalState = source.DismissalState;
        result.RemovalReason = source.RemovalReason;
        result.TombstoneState = source.TombstoneState;
        result.TombstoneWorldTime = source.TombstoneWorldTime;
        return result;
    }
    internal static RebirthNpcOwnershipRecord Copy(RebirthNpcOwnershipRecord source)
    {
        if (source == null) return null;
        RebirthNpcOwnershipRecord result = new RebirthNpcOwnershipRecord();
        result.OwnershipState = source.OwnershipState;
        result.OwnerPlatformIdOrPersistentPlayerId = source.OwnerPlatformIdOrPersistentPlayerId;
        result.OwnerCharacterIdIfRequired = source.OwnerCharacterIdIfRequired;
        result.OwnerRevision = source.OwnerRevision;
        result.HiredWorldTime = source.HiredWorldTime;
        result.DismissedWorldTime = source.DismissedWorldTime;
        result.PermissionPolicyId = source.PermissionPolicyId;
        result.PartyAccessMode = source.PartyAccessMode;
        return result;
    }
    internal static RebirthNpcOrderRecord Copy(RebirthNpcOrderRecord source)
    {
        if (source == null) return null;
        RebirthNpcOrderRecord result = new RebirthNpcOrderRecord();
        result.OrderState = source.OrderState;
        result.OrderRevision = source.OrderRevision;
        result.OrderTarget = source.OrderTarget;
        result.PreviousOrderState = source.PreviousOrderState;
        return result;
    }
    internal static RebirthNpcPresenceRecord Copy(RebirthNpcPresenceRecord source)
    {
        if (source == null) return null;
        RebirthNpcPresenceRecord result = new RebirthNpcPresenceRecord();
        result.PresenceState = source.PresenceState;
        result.PresenceRevision = source.PresenceRevision;
        result.TransitionReason = source.TransitionReason;
        result.TransitionStartedWorldTime = source.TransitionStartedWorldTime;
        result.ExpectedReturnWorldTime = source.ExpectedReturnWorldTime;
        result.SuspendedRuntimeEntityId = source.SuspendedRuntimeEntityId;
        result.EmbodimentGeneration = source.EmbodimentGeneration;
        result.LastKnownChunkKey = source.LastKnownChunkKey;
        return result;
    }
    internal static RebirthNpcTransformRecord Copy(RebirthNpcTransformRecord source)
    {
        if (source == null) return null;
        RebirthNpcTransformRecord result = new RebirthNpcTransformRecord();
        result.WorldPosition = source.WorldPosition;
        result.RotationYaw = source.RotationYaw;
        result.OptionalRotationPitch = source.OptionalRotationPitch;
        result.ChunkKey = source.ChunkKey;
        result.LastSafePosition = source.LastSafePosition;
        result.LastSafePositionWorldTime = source.LastSafePositionWorldTime;
        result.AnchorPosition = source.AnchorPosition;
        result.AnchorRotation = source.AnchorRotation;
        result.TransformRevision = source.TransformRevision;
        return result;
    }
    internal static RebirthNpcVitalStateRecord Copy(RebirthNpcVitalStateRecord source)
    {
        if (source == null) return null;
        RebirthNpcVitalStateRecord result = new RebirthNpcVitalStateRecord();
        result.CurrentHealth = source.CurrentHealth;
        result.MaximumHealthAtSave = source.MaximumHealthAtSave;
        result.CurrentStaminaIfApplicable = source.CurrentStaminaIfApplicable;
        result.DeathOrIncapacitationState = source.DeathOrIncapacitationState;
        result.ActivePersistentBuffSet = CopyArray(source.ActivePersistentBuffSet, value => value);
        result.VitalsRevision = source.VitalsRevision;
        return result;
    }
    internal static RebirthNpcInventorySlotRecord Copy(RebirthNpcInventorySlotRecord source)
    {
        if (source == null) return null;
        RebirthNpcInventorySlotRecord result = new RebirthNpcInventorySlotRecord();
        result.SlotIndex = source.SlotIndex;
        result.ItemValueSerialization = source.ItemValueSerialization;
        result.ItemCount = source.ItemCount;
        result.MetadataOrCustomData = source.MetadataOrCustomData;
        result.Locked = source.Locked;
        result.Reserved = source.Reserved;
        return result;
    }
    internal static RebirthNpcInventoryRecord Copy(RebirthNpcInventoryRecord source)
    {
        if (source == null) return null;
        RebirthNpcInventoryRecord result = new RebirthNpcInventoryRecord();
        result.InventorySchemaVersion = source.InventorySchemaVersion;
        result.SlotCount = source.SlotCount;
        result.SlotRecords = CopyArray(source.SlotRecords, value => Copy(value));
        result.OverflowRecords = CopyArray(source.OverflowRecords, value => Copy(value));
        result.InventoryRevision = source.InventoryRevision;
        result.ContentChecksum = source.ContentChecksum;
        return result;
    }
    internal static RebirthNpcEquipmentSlotRecord Copy(RebirthNpcEquipmentSlotRecord source)
    {
        if (source == null) return null;
        RebirthNpcEquipmentSlotRecord result = new RebirthNpcEquipmentSlotRecord();
        result.SlotId = source.SlotId;
        result.ItemValueSerialization = source.ItemValueSerialization;
        result.ItemCount = source.ItemCount;
        result.MetadataOrCustomData = source.MetadataOrCustomData;
        return result;
    }
    internal static RebirthNpcEquipmentRecord Copy(RebirthNpcEquipmentRecord source)
    {
        if (source == null) return null;
        RebirthNpcEquipmentRecord result = new RebirthNpcEquipmentRecord();
        result.EquipmentSchemaVersion = source.EquipmentSchemaVersion;
        result.EquipmentSlots = CopyArray(source.EquipmentSlots, value => Copy(value));
        result.ActiveHeldSlot = source.ActiveHeldSlot;
        result.ActiveAmmoStateIfNotNative = source.ActiveAmmoStateIfNotNative;
        result.EquipmentRevision = source.EquipmentRevision;
        return result;
    }
    internal static RebirthNpcRespawnRecord Copy(RebirthNpcRespawnRecord source)
    {
        if (source == null) return null;
        RebirthNpcRespawnRecord result = new RebirthNpcRespawnRecord();
        result.RespawnPolicyId = source.RespawnPolicyId;
        result.RespawnState = source.RespawnState;
        result.DeathWorldTime = source.DeathWorldTime;
        result.EligibleRespawnWorldTime = source.EligibleRespawnWorldTime;
        result.PreferredAnchorType = source.PreferredAnchorType;
        result.PreferredAnchorIdentity = source.PreferredAnchorIdentity;
        result.PreferredAnchorPosition = source.PreferredAnchorPosition;
        result.PreferredAnchorRotation = source.PreferredAnchorRotation;
        result.FallbackPosition = source.FallbackPosition;
        result.RetainedInventoryPolicy = source.RetainedInventoryPolicy;
        result.RespawnRevision = source.RespawnRevision;
        result.AttemptCount = source.AttemptCount;
        result.LastFailureReason = source.LastFailureReason;
        return result;
    }
    internal static RebirthNpcContractRecord Copy(RebirthNpcContractRecord source)
    {
        if (source == null) return null;
        RebirthNpcContractRecord result = new RebirthNpcContractRecord();
        result.ContractState = source.ContractState;
        result.ContractId = source.ContractId;
        result.StartedWorldTime = source.StartedWorldTime;
        result.ExpiresWorldTime = source.ExpiresWorldTime;
        result.WageAmount = source.WageAmount;
        result.NextWageWorldTime = source.NextWageWorldTime;
        result.ExpiryDisposition = source.ExpiryDisposition;
        result.ContractRevision = source.ContractRevision;
        return result;
    }
    internal static RebirthNpcWorkRecord Copy(RebirthNpcWorkRecord source)
    {
        if (source == null) return null;
        RebirthNpcWorkRecord result = new RebirthNpcWorkRecord();
        result.WorkState = source.WorkState;
        result.AssignmentId = source.AssignmentId;
        result.WorkType = source.WorkType;
        result.TargetReference = source.TargetReference;
        result.ReservationIds = CopyArray(source.ReservationIds, value => value);
        result.ProgressCheckpoint = source.ProgressCheckpoint;
        result.ResourceSourcePolicy = source.ResourceSourcePolicy;
        result.OutputDestinationPolicy = source.OutputDestinationPolicy;
        result.WorkRevision = source.WorkRevision;
        return result;
    }
    internal static RebirthNpcMissionRecord Copy(RebirthNpcMissionRecord source)
    {
        if (source == null) return null;
        RebirthNpcMissionRecord result = new RebirthNpcMissionRecord();
        result.MissionState = source.MissionState;
        result.MissionId = source.MissionId;
        result.StartedWorldTime = source.StartedWorldTime;
        result.ExpectedReturnWorldTime = source.ExpectedReturnWorldTime;
        result.Outcome = source.Outcome;
        result.MissionRevision = source.MissionRevision;
        return result;
    }
    internal static RebirthNpcControllerFragment Copy(RebirthNpcControllerFragment source)
    {
        if (source == null) return null;
        RebirthNpcControllerFragment result = new RebirthNpcControllerFragment();
        result.ControllerTypeId = source.ControllerTypeId;
        result.FragmentVersion = source.FragmentVersion;
        result.FragmentLength = source.FragmentLength;
        result.FragmentChecksum = source.FragmentChecksum;
        result.Bytes = CopyArray(source.Bytes, value => value);
        result.Required = source.Required;
        return result;
    }
    internal static RebirthNpcControllerFragmentSet Copy(RebirthNpcControllerFragmentSet source)
    {
        if (source == null) return null;
        RebirthNpcControllerFragmentSet result = new RebirthNpcControllerFragmentSet();
        result.FragmentCount = source.FragmentCount;
        result.Fragments = CopyArray(source.Fragments, value => Copy(value));
        return result;
    }
    internal static RebirthNpcAuditRecord Copy(RebirthNpcAuditRecord source)
    {
        if (source == null) return null;
        RebirthNpcAuditRecord result = new RebirthNpcAuditRecord();
        result.CreatedByBuild = source.CreatedByBuild;
        result.LastWrittenByBuild = source.LastWrittenByBuild;
        result.LastMutationKind = source.LastMutationKind;
        result.LastMutationWorldTime = source.LastMutationWorldTime;
        result.LastMutationServerTick = source.LastMutationServerTick;
        result.LastKnownOwnerNameForDiagnosticsOnly = source.LastKnownOwnerNameForDiagnosticsOnly;
        result.RecoveryFlags = CopyArray(source.RecoveryFlags, value => value);
        result.MigrationSource = source.MigrationSource;
        result.MigrationFingerprint = source.MigrationFingerprint;
        return result;
    }
    internal static RebirthNpcDomainRevisionManifest Copy(RebirthNpcDomainRevisionManifest source)
    {
        if (source == null) return null;
        RebirthNpcDomainRevisionManifest result = new RebirthNpcDomainRevisionManifest();
        result.DomainId = source.DomainId;
        result.FileName = source.FileName;
        result.DomainRevision = source.DomainRevision;
        result.Length = source.Length;
        result.Sha256 = source.Sha256;
        result.Required = source.Required;
        return result;
    }
    internal static RebirthNpcPersistentRecord Copy(RebirthNpcPersistentRecord source)
    {
        if (source == null) return null;
        RebirthNpcPersistentRecord result = new RebirthNpcPersistentRecord();
        result.Identity = Copy(source.Identity);
        result.Profile = Copy(source.Profile);
        result.NativeReconstruction = source.NativeReconstruction; // Immutable composed evidence, detached XML writes.
        result.HumanAppearance = source.HumanAppearance; // Immutable composed descriptor.
        result.Lifecycle = Copy(source.Lifecycle);
        result.Ownership = Copy(source.Ownership);
        result.Order = Copy(source.Order);
        result.Presence = Copy(source.Presence);
        result.Transform = Copy(source.Transform);
        result.Vitals = Copy(source.Vitals);
        result.Inventory = Copy(source.Inventory);
        result.Equipment = Copy(source.Equipment);
        result.Respawn = Copy(source.Respawn);
        result.Contract = Copy(source.Contract);
        result.Work = Copy(source.Work);
        result.Mission = Copy(source.Mission);
        result.ControllerFragments = Copy(source.ControllerFragments);
        result.Audit = Copy(source.Audit);
        result.Dog = Copy(source.Dog);
        result.BoundUndead = Copy(source.BoundUndead);
        result.DomainManifest = CopyArray(source.DomainManifest, value => Copy(value));
        result.UnknownOptionalElements = CopyArray(source.UnknownOptionalElements, value => value);
        result.AggregateRevision = source.AggregateRevision;
        result.AggregateChecksum = source.AggregateChecksum;
        return result;
    }
    internal static RebirthDogPersistentRecord Copy(RebirthDogPersistentRecord source)
    {
        if (source == null) return null;
        RebirthDogPersistentRecord result = new RebirthDogPersistentRecord();
        result.SchemaVersion = source.SchemaVersion;
        result.BreedId = source.BreedId;
        result.IsTamedWild = source.IsTamedWild;
        result.SourceEntityClass = source.SourceEntityClass;
        result.TamedEntityClass = source.TamedEntityClass;
        result.SpeciesCategory = source.SpeciesCategory;
        result.AnimalCapacityCost = source.AnimalCapacityCost;
        result.TamedUtcTicks = source.TamedUtcTicks;
        result.LastCareUtcTicks = source.LastCareUtcTicks;
        result.CombatMode = source.CombatMode;
        result.AttackStopped = source.AttackStopped;
        result.Level = source.Level;
        result.MiningLevel = source.MiningLevel;
        result.KillCount = source.KillCount;
        result.Training = source.Training;
        result.Bond = source.Bond;
        result.LegacyCommandGrandfathered = source.LegacyCommandGrandfathered;
        result.LearnedCommands.Clear();
        if (source.LearnedCommands != null) result.LearnedCommands.UnionWith(source.LearnedCommands);
        result.OfflineParked = source.OfflineParked;
        result.OfflineParkPosition = source.OfflineParkPosition;
        result.Lifecycle = source.Lifecycle;
        result.PickupNonce = source.PickupNonce;
        result.PickupRevision = source.PickupRevision;
        result.PickupNonceConsumed = source.PickupNonceConsumed;
        result.PickupRecoveryPending = source.PickupRecoveryPending;
        result.PickupRecoveryNonce = source.PickupRecoveryNonce;
        result.PickupRecoveryStage = source.PickupRecoveryStage;
        result.PickupRecoveryUtcTicks = source.PickupRecoveryUtcTicks;
        result.StayPosition = source.StayPosition;
        result.GuardIsStationaryStay = source.GuardIsStationaryStay;
        result.RespawnFallback = source.RespawnFallback;
        result.Revision = source.Revision;
        return result;
    }
    internal static RebirthBoundUndeadPersistentRecord Copy(RebirthBoundUndeadPersistentRecord source)
    {
        if (source == null) return null;
        RebirthBoundUndeadPersistentRecord result = new RebirthBoundUndeadPersistentRecord();
        result.SchemaVersion = source.SchemaVersion;
        result.SourceEntityClass = source.SourceEntityClass;
        result.TierId = source.TierId;
        result.CapacityCost = source.CapacityCost;
        result.ConditioningProgress = source.ConditioningProgress;
        result.ConditioningStage = source.ConditioningStage;
        result.IsBound = source.IsBound;
        result.IsSummoned = source.IsSummoned;
        result.Lifecycle = source.Lifecycle;
        result.LastEntityId = source.LastEntityId;
        result.KillCount = source.KillCount;
        result.Training = source.Training;
        result.BindingStability = source.BindingStability;
        result.BoundUtcTicks = source.BoundUtcTicks;
        result.LastConditioningUtcTicks = source.LastConditioningUtcTicks;
        result.BoundFactionId = source.BoundFactionId;
        result.BoundFactionRank = source.BoundFactionRank;
        result.AttackStopped = source.AttackStopped;
        result.Revision = source.Revision;
        return result;
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
    internal static bool HasFiniteNumbers(RebirthNpcPersistentRecord record)
    {
        if (record.Transform != null && !Finite(record.Transform.WorldPosition)) return false;
        if (record.Transform != null && !Finite(record.Transform.RotationYaw)) return false;
        if (record.Transform != null && (record.Transform.OptionalRotationPitch.HasValue && !Finite(record.Transform.OptionalRotationPitch.Value))) return false;
        if (record.Transform != null && (record.Transform.LastSafePosition.HasValue && !Finite(record.Transform.LastSafePosition.Value))) return false;
        if (record.Transform != null && (record.Transform.AnchorPosition.HasValue && !Finite(record.Transform.AnchorPosition.Value))) return false;
        if (record.Transform != null && (record.Transform.AnchorRotation.HasValue && !Finite(record.Transform.AnchorRotation.Value))) return false;
        if (record.Vitals != null && (record.Vitals.CurrentStaminaIfApplicable.HasValue && !Finite(record.Vitals.CurrentStaminaIfApplicable.Value))) return false;
        if (record.Respawn != null && (record.Respawn.PreferredAnchorPosition.HasValue && !Finite(record.Respawn.PreferredAnchorPosition.Value))) return false;
        if (record.Respawn != null && (record.Respawn.PreferredAnchorRotation.HasValue && !Finite(record.Respawn.PreferredAnchorRotation.Value))) return false;
        if (record.Respawn != null && (record.Respawn.FallbackPosition.HasValue && !Finite(record.Respawn.FallbackPosition.Value))) return false;
        if (record.Dog != null && !Finite(record.Dog.Training)) return false;
        if (record.Dog != null && !Finite(record.Dog.Bond)) return false;
        if (record.Dog != null && (record.Dog.OfflineParkPosition.HasValue && !Finite(record.Dog.OfflineParkPosition.Value))) return false;
        if (record.Dog != null && (record.Dog.StayPosition.HasValue && !Finite(record.Dog.StayPosition.Value))) return false;
        if (record.Dog != null && (record.Dog.RespawnFallback.HasValue && !Finite(record.Dog.RespawnFallback.Value))) return false;
        if (record.BoundUndead != null && !Finite(record.BoundUndead.ConditioningProgress)) return false;
        if (record.BoundUndead != null && !Finite(record.BoundUndead.Training)) return false;
        if (record.BoundUndead != null && !Finite(record.BoundUndead.BindingStability)) return false;
        return true;
    }
}
