using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using UnityEngine;

#nullable disable

public enum RebirthNpcCreationReason : byte { AmbientPromotion=0, ScriptedCreation=1, CompanionDeployment=2, Migration=3, AdministrativeRepair=4 }
public enum RebirthNpcLegacyFieldDisposition : byte { Map=0, IntentionalDiscard=1, HardFailure=2 }

public sealed class RebirthNpcIdentityRecord { public RebirthNpcStableId StableNpcId; public string Category; public string Species; public string GeneratedOrAssignedDisplayName; public uint NameRevision; public long CreationWorldTime; public RebirthNpcCreationReason CreationReason; public string OriginSpawnGroup; public string OriginPrefab; public string OriginScenario; }
public sealed class RebirthNpcProfileBindingRecord { public string ProfileId; public int ProfileSchemaVersionAtBind; public string ProfileContentHashAtBind; public string ProfileCompatibilityClass; public uint LastSuccessfulResolutionRevision; }
public sealed class RebirthNpcLifecycleRecord { public string LifecycleDomain; public uint LifecycleRevision; public long? PersistentSinceWorldTime; public string DismissalState; public string RemovalReason; public bool TombstoneState; public long? TombstoneWorldTime; }
public sealed class RebirthNpcOwnershipRecord { public string OwnershipState; public string OwnerPlatformIdOrPersistentPlayerId; public string OwnerCharacterIdIfRequired; public uint OwnerRevision; public long HiredWorldTime; public long? DismissedWorldTime; public string PermissionPolicyId; public string PartyAccessMode; }
public sealed class RebirthNpcOrderRecord { public string OrderState; public uint OrderRevision; public string OrderTarget; public string PreviousOrderState; }
public sealed class RebirthNpcPresenceRecord { public string PresenceState; public uint PresenceRevision; public string TransitionReason; public long TransitionStartedWorldTime; public long? ExpectedReturnWorldTime; public int? SuspendedRuntimeEntityId; public uint EmbodimentGeneration; public string LastKnownChunkKey; }
public sealed class RebirthNpcTransformRecord { public Vector3 WorldPosition; public float RotationYaw; public float? OptionalRotationPitch; public string ChunkKey; public Vector3? LastSafePosition; public long? LastSafePositionWorldTime; public Vector3? AnchorPosition; public float? AnchorRotation; public uint TransformRevision; }
public sealed class RebirthNpcVitalStateRecord { public int CurrentHealth; public int MaximumHealthAtSave; public float? CurrentStaminaIfApplicable; public string DeathOrIncapacitationState; public string[] ActivePersistentBuffSet; public uint VitalsRevision; }
public sealed class RebirthNpcInventorySlotRecord { public int SlotIndex; public string ItemValueSerialization; public int ItemCount; public string MetadataOrCustomData; public bool Locked; public bool Reserved; }
public sealed class RebirthNpcInventoryRecord { public int InventorySchemaVersion; public int SlotCount; public RebirthNpcInventorySlotRecord[] SlotRecords; public RebirthNpcInventorySlotRecord[] OverflowRecords; public uint InventoryRevision; public string ContentChecksum; }
public sealed class RebirthNpcEquipmentSlotRecord { public string SlotId; public string ItemValueSerialization; public int ItemCount; public string MetadataOrCustomData; }
public sealed class RebirthNpcEquipmentRecord { public int EquipmentSchemaVersion; public RebirthNpcEquipmentSlotRecord[] EquipmentSlots; public string ActiveHeldSlot; public string ActiveAmmoStateIfNotNative; public uint EquipmentRevision; }
public sealed class RebirthNpcRespawnRecord { public string RespawnPolicyId; public string RespawnState; public long? DeathWorldTime; public long? EligibleRespawnWorldTime; public string PreferredAnchorType; public string PreferredAnchorIdentity; public Vector3? PreferredAnchorPosition; public float? PreferredAnchorRotation; public Vector3? FallbackPosition; public string RetainedInventoryPolicy; public uint RespawnRevision; public int AttemptCount; public string LastFailureReason; }
public sealed class RebirthNpcContractRecord { public string ContractState; public string ContractId; public long StartedWorldTime; public long? ExpiresWorldTime; public int WageAmount; public long? NextWageWorldTime; public string ExpiryDisposition; public uint ContractRevision; }
public sealed class RebirthNpcWorkRecord { public string WorkState; public string AssignmentId; public string WorkType; public string TargetReference; public string[] ReservationIds; public string ProgressCheckpoint; public string ResourceSourcePolicy; public string OutputDestinationPolicy; public uint WorkRevision; }
public sealed class RebirthNpcMissionRecord { public string MissionState; public string MissionId; public long StartedWorldTime; public long? ExpectedReturnWorldTime; public string Outcome; public uint MissionRevision; }
public sealed class RebirthNpcControllerFragment { public string ControllerTypeId; public int FragmentVersion; public int FragmentLength; public string FragmentChecksum; public byte[] Bytes; public bool Required; }
public sealed class RebirthNpcControllerFragmentSet { public int FragmentCount; public RebirthNpcControllerFragment[] Fragments; }
public sealed class RebirthNpcAuditRecord { public string CreatedByBuild; public string LastWrittenByBuild; public string LastMutationKind; public long LastMutationWorldTime; public long LastMutationServerTick; public string LastKnownOwnerNameForDiagnosticsOnly; public string[] RecoveryFlags; public string MigrationSource; public string MigrationFingerprint; }
public sealed class RebirthNpcDomainRevisionManifest { public string DomainId; public string FileName; public uint DomainRevision; public long Length; public string Sha256; public bool Required; }

/// <summary>Canonical Architecture §40.5 NPC aggregate envelope over versioned domain stores.</summary>
public sealed class RebirthNpcPersistentRecord
{
    public RebirthNpcIdentityRecord Identity;
    public RebirthNpcProfileBindingRecord Profile;
    internal RebirthHumanNpcAppearanceDescriptor? HumanAppearance;
    internal RebirthNpcNativeReconstruction NativeReconstruction;
    public RebirthNpcLifecycleRecord Lifecycle;
    public RebirthNpcOwnershipRecord Ownership;
    public RebirthNpcOrderRecord Order;
    public RebirthNpcPresenceRecord Presence;
    public RebirthNpcTransformRecord Transform;
    public RebirthNpcVitalStateRecord Vitals;
    public RebirthNpcInventoryRecord Inventory;
    public RebirthNpcEquipmentRecord Equipment;
    public RebirthNpcRespawnRecord Respawn;
    public RebirthNpcContractRecord Contract;
    public RebirthNpcWorkRecord Work;
    public RebirthNpcMissionRecord Mission;
    public RebirthNpcControllerFragmentSet ControllerFragments;
    public RebirthNpcAuditRecord Audit;
    public RebirthDogPersistentRecord Dog;
    public RebirthBoundUndeadPersistentRecord BoundUndead;
    public RebirthNpcDomainRevisionManifest[] DomainManifest;
    // Unknown optional record-level XML is preserved byte-for-text (OuterXml) across
    // read/write so forward-compatible extensions are not silently destroyed. Unknown
    // elements marked required="1" are rejected instead of being ignored.
    public string[] UnknownOptionalElements;
    public uint AggregateRevision;
    public string AggregateChecksum;
}

public sealed class RebirthNpcAggregateQualificationSnapshot { public bool IsValid; public int Records; public int ManifestDomains; public int ConsistencyFailures; public int MigrationMappings; public int UnsupportedFields; public int Quarantined; public int DuplicateImports; public string Detail; }

public static class RebirthNpcAggregatePersistenceStore
{
    public const int CurrentFormat = 4;
    public const string FileName = "RebirthNpcPersistentRecords.xml";
    private static readonly object Sync = new object();
    private static Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord> Records = new Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord>();
    // Records from an unscoped/foreign sidecar are never authoritative merely because
    // they exist on disk. Dogs are held here until a real entity from THIS save proves
    // the same embedded StableId. This is intentionally process-local and is cleared
    // on every world reset; the original XML is preserved as the normal .bak on save.
    private static Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord> QuarantinedRecords =
        new Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord>();
    // Cached projections are immutable and belong to exactly one loaded save.
    private static readonly Dictionary<RebirthNpcStableId, RebirthNpcPersistentRecordView> ReadViews =
        new Dictionary<RebirthNpcStableId, RebirthNpcPersistentRecordView>();
    private static RebirthNpcPersistentRecordView[] orderedReadViews;
    private static bool loaded, dirty;
    private static long retryLoadAfterUtcTicks;
    private static long storeEpoch;
    private static string loadedSaveDirectory = string.Empty;
    private static string loadedSaveScope = string.Empty;
    private static string provenanceState = "unloaded";
    private static int quarantinedDogRecords, rejectedForeignRecords, promotedQuarantinedDogs;
    private static long loads, saves, recovered, consistencyFailures, duplicateImports;

    public static void EnsureLoaded()
    {
        RebirthNpcSaveScopeSnapshot scope = RebirthNpcSaveScope.ObserveCurrent();
        string saveDirectory = scope != null ? (scope.SaveDirectory ?? string.Empty) : string.Empty;
        string saveScope = scope != null ? (scope.Fingerprint ?? string.Empty) : string.Empty;
        if (string.IsNullOrEmpty(saveDirectory))
        {
            // A menu/unavailable-world read must never expose the previous save.
            lock (Sync)
            {
                if (loaded || Records.Count != 0 || QuarantinedRecords.Count != 0 || loadedSaveDirectory.Length != 0)
                {
                    Records.Clear(); QuarantinedRecords.Clear(); InvalidateAllReadViewsLocked();
                    loaded = false; dirty = false; retryLoadAfterUtcTicks = 0; unchecked { storeEpoch++; }
                    loadedSaveDirectory = string.Empty; loadedSaveScope = string.Empty; provenanceState = "no-current-save";
                }
            }
            return;
        }

        bool importLegacy = false;
        lock (Sync)
        {
            bool sameScope = string.Equals(loadedSaveDirectory, saveDirectory, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(loadedSaveScope, saveScope, StringComparison.OrdinalIgnoreCase);
            if (loaded && sameScope) return;
            if (!sameScope)
            {
                Records.Clear(); QuarantinedRecords.Clear(); InvalidateAllReadViewsLocked();
                dirty = false; loaded = false; retryLoadAfterUtcTicks = 0; unchecked { storeEpoch++; }
                loadedSaveDirectory = saveDirectory; loadedSaveScope = saveScope;
                provenanceState = "boundary-reset";
            }
            long now = DateTime.UtcNow.Ticks;
            if (now < retryLoadAfterUtcTicks) return;
            string path = Path.Combine(saveDirectory, FileName);
            bool unscopedIsUntrusted = scope != null && scope.IsSecondOrLaterSave;
            if (TryLoad(path, "primary", saveScope, unscopedIsUntrusted) ||
                TryLoad(path + ".bak", "backup", saveScope, unscopedIsUntrusted))
            {
                loaded = true; retryLoadAfterUtcTicks = 0; importLegacy = true;
            }
            else if (Directory.Exists(saveDirectory) && ConfirmedAbsent(path) && ConfirmedAbsent(path + ".bak"))
            {
                Records.Clear(); QuarantinedRecords.Clear(); InvalidateAllReadViewsLocked();
                dirty = false; loaded = true; retryLoadAfterUtcTicks = 0;
                provenanceState = "new-empty"; importLegacy = true;
            }
            else
            {
                // Corrupt, unsupported, denied and unavailable are not an empty world.
                // Keep writes closed and retry without reparsing files on every getter.
                loaded = false; provenanceState = "load-failed-retryable";
                retryLoadAfterUtcTicks = now + TimeSpan.TicksPerSecond;
            }
        }
        if (importLegacy) RebirthNpcLegacy26ImportService.TryImportSupportedFiles();
    }

    private static bool ConfirmedAbsent(string path)
    {
        try { File.GetAttributes(path); return false; }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static void InvalidateReadViewLocked(RebirthNpcStableId id)
    {
        ReadViews.Remove(id); orderedReadViews = null;
    }

    private static void InvalidateAllReadViewsLocked()
    {
        ReadViews.Clear(); orderedReadViews = null;
    }

    private static RebirthNpcPersistentRecordView GetReadViewLocked(RebirthNpcStableId id, RebirthNpcPersistentRecord record)
    {
        RebirthNpcPersistentRecordView view;
        if (!ReadViews.TryGetValue(id, out view))
        {
            view = new RebirthNpcPersistentRecordView(record);
            ReadViews.Add(id, view);
        }
        return view;
    }

    public static bool TryGetView(RebirthNpcStableId id, out RebirthNpcPersistentRecordView view)
    {
        EnsureLoaded();
        lock (Sync)
        {
            RebirthNpcPersistentRecord record;
            if (!loaded || !Records.TryGetValue(id, out record)) { view = null; return false; }
            view = GetReadViewLocked(id, record); return true;
        }
    }

    public static RebirthNpcPersistentRecordView[] SnapshotViews()
    {
        EnsureLoaded();
        lock (Sync)
        {
            if (!loaded) return new RebirthNpcPersistentRecordView[0];
            if (orderedReadViews == null)
            {
                orderedReadViews = new RebirthNpcPersistentRecordView[Records.Count];
                int index = 0;
                foreach (KeyValuePair<RebirthNpcStableId, RebirthNpcPersistentRecord> pair in Records)
                    orderedReadViews[index++] = GetReadViewLocked(pair.Key, pair.Value);
                Array.Sort(orderedReadViews, (x, y) => string.CompareOrdinal(x.Identity.StableNpcId.ToString(), y.Identity.StableNpcId.ToString()));
            }
            // Callers may reorder their array, but not the shared order or any record.
            return (RebirthNpcPersistentRecordView[])orderedReadViews.Clone();
        }
    }

    public static RebirthNpcPersistentRecordView[] QuarantinedDogViews()
    {
        EnsureLoaded();
        lock (Sync)
        {
            var result = new List<RebirthNpcPersistentRecordView>();
            if (loaded)
                foreach (RebirthNpcPersistentRecord record in QuarantinedRecords.Values)
                    if (IsDogRecord(record)) result.Add(new RebirthNpcPersistentRecordView(record));
            result.Sort((x, y) => string.CompareOrdinal(x.Identity.StableNpcId.ToString(), y.Identity.StableNpcId.ToString()));
            return result.ToArray();
        }
    }

    /// <summary>Legacy cold API: every returned graph is detached. Prefer SnapshotViews for read-only gameplay.</summary>
    public static RebirthNpcPersistentRecord[] Snapshot()
    {
        EnsureLoaded();
        lock (Sync)
        {
            if (!loaded) return new RebirthNpcPersistentRecord[0];
            List<RebirthNpcPersistentRecord> records = SortedRecords();
            var result = new RebirthNpcPersistentRecord[records.Count];
            for (int i = 0; i < result.Length; i++) result[i] = RebirthNpcAggregateRecordCopy.Copy(records[i]);
            return result;
        }
    }

    public static RebirthNpcPersistentRecord[] QuarantinedDogSnapshot()
    {
        EnsureLoaded();
        lock (Sync)
        {
            List<RebirthNpcPersistentRecord> result = new List<RebirthNpcPersistentRecord>();
            foreach (RebirthNpcPersistentRecord record in QuarantinedRecords.Values)
                if (loaded && IsDogRecord(record)) result.Add(RebirthNpcAggregateRecordCopy.Copy(record));
            result.Sort((x, y) => string.CompareOrdinal(x.Identity.StableNpcId.ToString(), y.Identity.StableNpcId.ToString()));
            return result.ToArray();
        }
    }

    /// <summary>
    /// A quarantined durable dog record may become authoritative only when a physical
    /// dog entity from the current save presents the same embedded StableId.
    /// </summary>
    public static bool TryPromoteQuarantinedDogRecord(RebirthNpcStableId id, out RebirthNpcPersistentRecord record)
    {
        record = null;
        if (id.IsEmpty) return false;
        EnsureLoaded();
        lock (Sync)
        {
            RebirthNpcPersistentRecord candidate;
            if (!loaded || !QuarantinedRecords.TryGetValue(id, out candidate) || !IsDogRecord(candidate))
                return false;

            QuarantinedRecords.Remove(id);
            Records[id] = candidate;
            candidate.AggregateRevision = Math.Max(1U, candidate.AggregateRevision + 1U);
            candidate.AggregateChecksum = ComputeRecordChecksum(candidate);
            dirty = true;
            promotedQuarantinedDogs++;
            InvalidateReadViewLocked(id);
            record = RebirthNpcAggregateRecordCopy.Copy(candidate);
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH NPC Aggregate] promoted quarantined dog after current-save entity proof stableId=" +
                    id + " saveScope=" + ShortScope(loadedSaveScope) + "."); }
            return true;
        }
    }

    public static string GetSaveScopeReport()
    {
        EnsureLoaded();
        lock (Sync)
        {
            int quarantinedDogs = 0;
            foreach (RebirthNpcPersistentRecord record in QuarantinedRecords.Values)
                if (IsDogRecord(record)) quarantinedDogs++;
            RebirthNpcSaveScopeSnapshot scope = RebirthNpcSaveScope.ObserveCurrent();
            return "scope=" + ShortScope(loadedSaveScope) +
                " processGeneration=" + (scope != null ? scope.ProcessSaveGeneration : 0) +
                " provenance=" + provenanceState +
                " quarantinedDogs=" + quarantinedDogs +
                " rejectedForeign=" + rejectedForeignRecords +
                " promotedDogs=" + promotedQuarantinedDogs;
        }
    }

    /// <summary>Legacy cold API: mutation of a returned record cannot change store authority.</summary>
    public static bool TryGet(RebirthNpcStableId id, out RebirthNpcPersistentRecord record)
    {
        EnsureLoaded();
        lock (Sync)
        {
            RebirthNpcPersistentRecord owned;
            if (!loaded || !Records.TryGetValue(id, out owned)) { record = null; return false; }
            record = RebirthNpcAggregateRecordCopy.Copy(owned); return true;
        }
    }

    public static bool Remove(RebirthNpcStableId id)
    {
        EnsureLoaded();
        lock (Sync)
        {
            if (!loaded || !Records.Remove(id)) return false;
            InvalidateReadViewLocked(id);
            dirty = true;
            return true;
        }
    }

    /// <summary>
    /// Mutate an isolated candidate under the store lock. Exceptions/rejected ingress
    /// publish nothing. A second typed copy severs references retained by the callback
    /// (including externally supplied nested arrays/objects) before authority changes.
    /// </summary>
    public static bool Mutate(RebirthNpcStableId id, Action<RebirthNpcPersistentRecord> mutator)
    {
        if (id.IsEmpty || mutator == null) return false;
        EnsureLoaded();
        lock (Sync)
        {
            if (!loaded) return false;
            long epoch = storeEpoch;
            RebirthNpcPersistentRecord previous;
            Records.TryGetValue(id, out previous);
            RebirthNpcPersistentRecord candidate = previous != null ? RebirthNpcAggregateRecordCopy.Copy(previous) :
                CreateEmpty(id, DateTime.UtcNow.Ticks);
            mutator(candidate);
            PublishCandidateLocked(id, previous, candidate, epoch);
            return true;
        }
    }

    /// <summary>
    /// Compare-and-mutate an existing row. A stale revision, missing row, or declined
    /// callback publishes nothing. Callers must still own any external native effects.
    /// </summary>
    // Once-only appearance publication: primary disk commit precedes replacing authority memory.
    // Does not create NPC identities, profiles or population records.
    internal static bool TryCommitResolvedAppearance(RebirthNpcStableId id, uint expectedRevision,
        RebirthHumanNpcAppearanceDescriptor original, RebirthHumanNpcAppearanceDescriptor proposed,
        out RebirthHumanNpcAppearanceDescriptor committed, out string error)
    {
        committed=original;error=string.Empty;
        if(!original.IsValid||!proposed.IsValid||proposed.Resolved==null||
            !original.TryWithResolved(proposed.Resolved,out var bound)||!bound.Equals(proposed))
        {error="resolved-appearance-binding-invalid";return false;}
        EnsureLoaded();
        var scope=RebirthNpcSaveScope.ObserveCurrent();
        lock(Sync)
        {
            if(!loaded||string.IsNullOrEmpty(scope.SaveDirectory)||
                !string.Equals(loadedSaveDirectory,scope.SaveDirectory,StringComparison.OrdinalIgnoreCase)||
                !string.Equals(loadedSaveScope,scope.Fingerprint??string.Empty,StringComparison.OrdinalIgnoreCase))
            {error="resolved-appearance-save-unavailable-or-changed";return false;}
            if(!Records.TryGetValue(id,out var previous))
            {error="resolved-appearance-existing-person-required";return false;}
            // Retry/concurrent resolution returns the durably owned value, never the new proposal.
            if(previous.HumanAppearance.HasValue&&previous.HumanAppearance.Value.Resolved!=null)
            {committed=previous.HumanAppearance.Value;return true;}
            if(previous.AggregateRevision!=expectedRevision||
                (previous.HumanAppearance.HasValue&&!previous.HumanAppearance.Value.Equals(original)))
            {error="resolved-appearance-original-revision-changed";return false;}
            var candidate=RebirthNpcAggregateRecordCopy.Copy(previous);
            candidate.HumanAppearance=proposed;
            candidate.AggregateRevision=Math.Max(1U,unchecked(previous.AggregateRevision+1U));
            if(!ValidatePublication(candidate,id,out error))return false;
            candidate.AggregateChecksum=ComputeRecordChecksum(candidate);
            var merged=new Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord>(Records);
            merged[id]=candidate;
            var ordered=new List<RebirthNpcPersistentRecord>(merged.Values);
            ordered.Sort((x,y)=>string.CompareOrdinal(x.Identity.StableNpcId.ToString(),y.Identity.StableNpcId.ToString()));
            bool backupPending;
            try {backupPending=WriteRecordsLocked(ordered,loadedSaveDirectory,loadedSaveScope);}
            catch(Exception ex){error="resolved-appearance-publication-failed:"+ex.GetType().Name+":"+ex.Message;return false;}
            Records=merged;InvalidateAllReadViewsLocked();dirty=backupPending;
            provenanceState=backupPending?"scoped-current-backup-pending":"scoped-current";
            saves++;committed=proposed;RecordWriteTelemetry(loadedSaveDirectory);return true;
        }
    }
    public static bool TryMutateExisting(RebirthNpcStableId id, uint expectedRevision, Func<RebirthNpcPersistentRecord, bool> mutator)
    {
        if (id.IsEmpty || mutator == null) return false;
        EnsureLoaded();
        lock (Sync)
        {
            RebirthNpcPersistentRecord previous;
            if (!loaded || !Records.TryGetValue(id, out previous) || previous.AggregateRevision != expectedRevision)
                return false;
            long epoch = storeEpoch;
            RebirthNpcPersistentRecord candidate = RebirthNpcAggregateRecordCopy.Copy(previous);
            string error;
            if (!ValidatePublication(candidate, id, out error))
                throw new InvalidDataException("NPC aggregate existing state rejected: " + error);
            if (!mutator(candidate)) return false;
            PublishCandidateLocked(id, previous, candidate, epoch);
            return true;
        }
    }

    private static void PublishCandidateLocked(RebirthNpcStableId id, RebirthNpcPersistentRecord previous,
        RebirthNpcPersistentRecord candidate, long expectedEpoch)
    {
        string error;
        if (!ValidatePublication(candidate, id, out error))
            throw new InvalidDataException("NPC aggregate mutation rejected: " + error);
        RebirthNpcPersistentRecord current;
        Records.TryGetValue(id, out current);
        if (!loaded || storeEpoch != expectedEpoch || !ReferenceEquals(previous, current))
            throw new InvalidOperationException("NPC aggregate changed during a reentrant mutation; candidate not published.");
        uint previousRevision = previous != null ? previous.AggregateRevision : 0U;
        candidate.AggregateRevision = Math.Max(1U, unchecked(previousRevision + 1U));
        candidate.AggregateChecksum = ComputeRecordChecksum(candidate);
        Records[id] = RebirthNpcAggregateRecordCopy.Copy(candidate);
        InvalidateReadViewLocked(id); dirty = true;
    }

    private static bool ValidatePublication(RebirthNpcPersistentRecord record, RebirthNpcStableId expectedId, out string error)
    {
        error = string.Empty;
        if (record == null || record.Identity == null || record.Identity.StableNpcId.IsEmpty ||
            !record.Identity.StableNpcId.Equals(expectedId) || record.Profile == null || record.Lifecycle == null ||
            record.Presence == null || record.Transform == null || record.Vitals == null || record.Audit == null)
        { error = "missing-core-or-changed-identity"; return false; }
        if (record.HumanAppearance.HasValue && !record.HumanAppearance.Value.IsValid)
        { error = "invalid-human-appearance"; return false; }
        if (record.NativeReconstruction != null && !record.NativeReconstruction.Matches(record))
        { error = "native-reconstruction-binding-mismatch"; return false; }
        if (!Enum.IsDefined(typeof(RebirthNpcCreationReason), record.Identity.CreationReason))
        { error = "invalid-creation-reason"; return false; }
        if (record.Dog != null)
        {
            if (!Enum.IsDefined(typeof(RebirthCompanionBehaviorMode), record.Dog.CombatMode) ||
                !Enum.IsDefined(typeof(RebirthDogLifecycleKind), record.Dog.Lifecycle))
            { error = "invalid-dog-enum"; return false; }
            // Command normalization belongs to authoritative ingress, never a getter.
            RebirthDogTrainingCommandIds.EnsureBasic(record.Dog.LearnedCommands);
        }
        if (!RebirthNpcAggregateRecordCopy.HasFiniteNumbers(record))
        { error = "non-finite-numeric-state"; return false; }
        return true;
    }

    public static void MarkDirty()
    {
        EnsureLoaded();
        lock (Sync) { if (!loaded) return; InvalidateAllReadViewsLocked(); dirty = true; }
    }

    public static bool UpsertMigrated(RebirthNpcPersistentRecord record, string fingerprint, out bool duplicate, out string error)
    {
        duplicate = false; error = string.Empty;
        if (record == null || record.Identity == null || record.Identity.StableNpcId.IsEmpty)
        { error = "invalid-migrated-record"; return false; }
        EnsureLoaded();
        lock (Sync)
        {
            if (!loaded) { error = "aggregate-not-loaded"; return false; }
            RebirthNpcStableId id = record.Identity.StableNpcId;
            RebirthNpcPersistentRecord old;
            if (Records.TryGetValue(id, out old))
            {
                if (old.Audit != null && string.Equals(old.Audit.MigrationFingerprint, fingerprint, StringComparison.Ordinal))
                { duplicate = true; duplicateImports++; return true; }
                error = "stable-id-conflict"; return false;
            }
            RebirthNpcPersistentRecord candidate = RebirthNpcAggregateRecordCopy.Copy(record);
            if (!ValidatePublication(candidate, id, out error)) return false;
            candidate.AggregateRevision = 1; candidate.AggregateChecksum = ComputeRecordChecksum(candidate);
            Records.Add(id, candidate); InvalidateReadViewLocked(id); dirty = true; return true;
        }
    }

    /// <summary>
    /// Stage the entire legacy file against one save and publish its complete aggregate
    /// before installing any new authority. Changed content under an existing identity
    /// is a visible conflict, never a second NPC or an implicit overwrite.
    /// </summary>
    public static bool TryCommitMigration(RebirthNpcPersistentRecord[] proposed, string expectedDirectory,
        string expectedScope, out RebirthNpcStableId[] insertedIds, out int duplicateRows, out string error)
    {
        insertedIds = new RebirthNpcStableId[0]; duplicateRows = 0; error = string.Empty;
        if (proposed == null) { error = "migration-batch-null"; return false; }
        EnsureLoaded();
        lock (Sync)
        {
            if (!loaded || !string.Equals(loadedSaveDirectory, expectedDirectory, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(loadedSaveScope, expectedScope, StringComparison.OrdinalIgnoreCase))
            { error = "migration-save-unavailable-or-changed"; return false; }
            var merged = new Dictionary<RebirthNpcStableId, RebirthNpcPersistentRecord>(Records);
            var additions = new List<RebirthNpcPersistentRecord>();
            for (int i = 0; i < proposed.Length; i++)
            {
                RebirthNpcPersistentRecord candidate = RebirthNpcAggregateRecordCopy.Copy(proposed[i]);
                if (candidate == null || candidate.Identity == null ||
                    !ValidatePublication(candidate, candidate.Identity.StableNpcId, out error))
                { error = "invalid-migration-row-" + i + ":" + error; return false; }
                RebirthNpcStableId id = candidate.Identity.StableNpcId;
                RebirthNpcPersistentRecord previous;
                if (merged.TryGetValue(id, out previous))
                {
                    if (previous.Audit != null && string.Equals(previous.Audit.MigrationFingerprint,
                        candidate.Audit.MigrationFingerprint, StringComparison.Ordinal))
                    { duplicateRows++; continue; }
                    error = "stable-id-conflict:" + id; return false;
                }
                candidate.AggregateRevision = 1;
                candidate.AggregateChecksum = ComputeRecordChecksum(candidate);
                merged.Add(id, candidate); additions.Add(candidate);
            }
            if (additions.Count == 0 && !dirty) { duplicateImports += duplicateRows; return true; }
            RebirthNpcStableId[] committedIds = new RebirthNpcStableId[additions.Count];
            for (int i = 0; i < additions.Count; i++) committedIds[i] = additions[i].Identity.StableNpcId;
            var ordered = new List<RebirthNpcPersistentRecord>(merged.Values);
            ordered.Sort((x, y) => string.CompareOrdinal(x.Identity.StableNpcId.ToString(), y.Identity.StableNpcId.ToString()));
            bool backupPending;
            try { backupPending = WriteRecordsLocked(ordered, expectedDirectory, expectedScope); }
            catch (Exception ex) { error = "migration-publication-failed:" + ex.GetType().Name + ":" + ex.Message; return false; }
            // All candidate containers were allocated before publication. One reference
            // swap installs the full batch; no rollback sweep can remove another writer's row.
            Records = merged; InvalidateAllReadViewsLocked(); insertedIds = committedIds;
            dirty = backupPending; provenanceState = backupPending ? "scoped-current-backup-pending" : "scoped-current";
            saves++; duplicateImports += duplicateRows;
            RecordWriteTelemetry(expectedDirectory);
            return true;
        }
    }

    public static RebirthNpcStableId[] CaptureMigrationBoundary()
    {
        EnsureLoaded();
        lock (Sync)
        {
            RebirthNpcStableId[] ids = new RebirthNpcStableId[Records.Count];
            Records.Keys.CopyTo(ids, 0);
            return ids;
        }
    }

    public static void RollbackMigrationToBoundary(RebirthNpcStableId[] boundary)
    {
        EnsureLoaded();
        HashSet<RebirthNpcStableId> keep = new HashSet<RebirthNpcStableId>(boundary ?? new RebirthNpcStableId[0]);
        lock (Sync)
        {
            List<RebirthNpcStableId> remove = new List<RebirthNpcStableId>();
            foreach (RebirthNpcStableId id in Records.Keys) if (!keep.Contains(id)) remove.Add(id);
            for (int i = 0; i < remove.Count; i++) { Records.Remove(remove[i]); InvalidateReadViewLocked(remove[i]); }
            if (remove.Count > 0) dirty = true;
        }
    }

    public static void CaptureRuntimeEnvelopes()
    {
        EnsureLoaded();
        RebirthNpcLegacy26ImportService.TryImportSupportedFiles();
        long captureEpoch;
        lock (Sync)
        {
            if (!loaded) throw new InvalidOperationException("Aggregate load unavailable; capture refused.");
            captureEpoch = storeEpoch;
        }
        RebirthNpcDomainRevisionManifest[] manifest=CaptureDomainManifest(); RebirthNpcRuntimeState[] states=RebirthNpcRuntimeRegistry.GetSnapshot(); long now=DateTime.UtcNow.Ticks;
        lock(Sync)
        {
            if (!loaded || storeEpoch != captureEpoch)
                throw new InvalidOperationException("Aggregate save changed during runtime capture; capture refused.");
            var staged = new Dictionary<RebirthNpcStableId, RebirthNpcPersistentRecord>();
            for(int i=0;i<states.Length;i++)
            {
                RebirthNpcRuntimeState s=states[i];
                // A held offworld candidate must not overwrite its original saved person.
                if(s.PreparedRestorationPending) continue;
                RebirthNpcPersistentRecord r;
                if(!Records.TryGetValue(s.StableId,out r)) r=CreateEmpty(s.StableId,now); else r=RebirthNpcAggregateRecordCopy.Copy(r);
                r.Identity.Category=ResolveCategory(s.ProfileId); r.Identity.GeneratedOrAssignedDisplayName=RebirthNpcWorldIntegrationService.GetDisplayName(s.StableId);
                if (s.HasHumanAppearance)
                {
                    if (r.HumanAppearance.HasValue && r.HumanAppearance.Value.Resolved != null && !r.HumanAppearance.Value.Equals(s.HumanAppearance))
                        throw new InvalidDataException("Aggregate capture refused a rerolled resolved NPC appearance.");
                    r.HumanAppearance=s.HumanAppearance;
                }
                r.Profile.ProfileId=s.ProfileId; r.Profile.ProfileSchemaVersionAtBind=RebirthNpcRuntimeState.CurrentSchemaVersion; r.Profile.LastSuccessfulResolutionRevision=s.Revision;
                r.Lifecycle.LifecycleDomain="persistent"; r.Lifecycle.LifecycleRevision=s.Revision;
                r.Ownership=s.OwnershipKind==RebirthNpcOwnershipKind.None?null:new RebirthNpcOwnershipRecord{OwnershipState=s.OwnershipKind.ToString(),OwnerPlatformIdOrPersistentPlayerId=s.OwnerId??string.Empty,OwnerRevision=s.Revision,PermissionPolicyId="rebirth.default",PartyAccessMode="owner-and-party"};
                r.Order=s.Order==RebirthNpcOrderState.None?null:new RebirthNpcOrderRecord{OrderState=s.Order.ToString(),OrderRevision=s.Revision};
                r.Presence.PresenceState=s.Presence.ToString();r.Presence.PresenceRevision=s.Revision;r.Presence.TransitionReason="runtime-capture";
                r.Transform.AnchorPosition=s.HasGuardPosition?(Vector3?)s.GuardPosition:null;
                if(!s.HasGuardPosition)r.Transform.AnchorRotation=null;
                RebirthNpcAggregateTransformCapture.TryCapture(GameManager.Instance?.World,s,r.Transform);
                RebirthNpcNativeReconstructionCapture.TryCaptureCheckpoint(s,r);
                r.Audit.LastWrittenByBuild="REBIRTH-3.0-ACIP-11";r.Audit.LastMutationKind="aggregate-capture";r.Audit.LastMutationWorldTime=now;
                r.DomainManifest=manifest;
                string error;
                if (!ValidatePublication(r, s.StableId, out error))
                    throw new InvalidDataException("NPC aggregate runtime capture rejected: " + error);
                r.AggregateRevision=Math.Max(1,r.AggregateRevision+1);r.AggregateChecksum=ComputeRecordChecksum(r);
                staged.Add(s.StableId, r);
            }
            if (RebirthNpcPersistenceCoordinator.IsCheckpointWrite)
            {
                // Unloaded companions keep their last durable owner/inventory/transform.
                // Only refresh the file-generation manifest for these records, otherwise a
                // complete checkpoint can contain yesterday's hashes on an unloaded dog.
                foreach (var pair in Records)
                {
                    if (staged.ContainsKey(pair.Key)) continue;
                    var copy = RebirthNpcAggregateRecordCopy.Copy(pair.Value);
                    copy.DomainManifest = manifest;
                    string error;
                    if (!ValidatePublication(copy, pair.Key, out error))
                        throw new InvalidDataException("NPC unloaded aggregate manifest capture rejected: " + error);
                    copy.AggregateRevision = Math.Max(1, copy.AggregateRevision + 1);
                    copy.AggregateChecksum = ComputeRecordChecksum(copy);
                    staged.Add(pair.Key, copy);
                }
            }
            // Publish only after the complete captured set passed validation.
            foreach (var pair in staged) { Records[pair.Key] = pair.Value; InvalidateReadViewLocked(pair.Key); }
            dirty=true;
        }
    }

    public static void Save()
    {
        RebirthNpcSaveScopeSnapshot scope = RebirthNpcSaveScope.ObserveCurrent();
        if (scope == null || string.IsNullOrEmpty(scope.SaveDirectory)) return;

        // Refuse to capture/write if GameIO has already moved to another save but this
        // store still represents an older save. This is the final guard against a
        // shutdown-order race copying one world's durable companions into another.
        lock (Sync)
        {
            if (loaded && !string.IsNullOrEmpty(loadedSaveDirectory) &&
                (!string.Equals(loadedSaveDirectory, scope.SaveDirectory, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(loadedSaveScope, scope.Fingerprint ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
            {
                string refusal = "REFUSED cross-save aggregate write loaded='" + loadedSaveDirectory +
                    "' current='" + scope.SaveDirectory + "'. Stale records were not saved.";
                Log.Error("[REBIRTH NPC Aggregate] " + refusal);
                throw new InvalidOperationException(refusal);
            }
        }

        CaptureRuntimeEnvelopes();
        lock (Sync)
        {
            if (!loaded || !string.Equals(loadedSaveDirectory, scope.SaveDirectory, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(loadedSaveScope, scope.Fingerprint ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Aggregate save scope changed before publication; write refused.");
            if (!dirty) return;
            bool backupPending = WriteRecordsLocked(SortedRecords(), loadedSaveDirectory, loadedSaveScope);
            dirty = backupPending;
            provenanceState = backupPending ? "scoped-current-backup-pending" : "scoped-current";
            saves++;
            RecordWriteTelemetry(loadedSaveDirectory);
        }
    }

    private static void RecordWriteTelemetry(string directory)
    {
        // Diagnostic bookkeeping must not turn an already committed primary into
        // a reported failed transaction or allow an in-memory rollback behind it.
        try { RebirthNpcPersistenceSchemaTelemetry.RecordLoad("aggregate", Path.Combine(directory, FileName), CurrentFormat, CurrentFormat, "primary"); }
        catch { }
    }

    // Caller owns Sync and has already checked the target save identity. No authority
    // changes before successful publication. The XML format/WriteRecord codec is unchanged.
    private static bool WriteRecordsLocked(IList<RebirthNpcPersistentRecord> records, string directory, string saveScope)
    {
        string path = Path.Combine(directory, FileName);
        Directory.CreateDirectory(directory);
        string tmp = path + ".tmp";
        XmlDocument document = new XmlDocument();
        XmlElement root = document.CreateElement("rebirthNpcPersistentRecords");
        root.SetAttribute("format", CurrentFormat.ToString(CultureInfo.InvariantCulture));
        root.SetAttribute("writtenUtcTicks", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        root.SetAttribute("saveScope", saveScope ?? string.Empty);
        document.AppendChild(root);
        for (int i = 0; i < records.Count; i++) root.AppendChild(WriteRecord(document, records[i]));
        using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        { document.Save(output); output.Flush(true); }
        ValidateStagedAggregate(tmp, saveScope, records.Count);
        bool quarantineRewrite = provenanceState.IndexOf("quarantined", StringComparison.OrdinalIgnoreCase) >= 0;
        if (quarantineRewrite && File.Exists(path)) File.Copy(path, path + ".legacy-quarantine", true);
        string error;
        if (!RebirthDurableFileCommit.TryPublish(tmp, path, out error))
            throw new IOException("Aggregate publication failed: " + error);
        // The primary has committed. A backup-refresh failure is retryable housekeeping,
        // NOT a failed import that can justify rolling memory back behind committed bytes.
        bool backupPending = false;
        if (quarantineRewrite)
        {
            try { File.Copy(path, path + ".bak", true); }
            catch (Exception ex)
            {
                backupPending = true;
                try { Log.Warning("[REBIRTH NPC Aggregate] primary committed; scoped backup refresh pending: " + ex.Message); } catch { }
            }
        }
        return backupPending;
    }

    private static void ValidateStagedAggregate(string path, string expectedScope, int expectedCount)
    {
        XmlDocument document = new XmlDocument(); document.Load(path);
        XmlElement root = document.DocumentElement;
        if (root == null || root.Name != "rebirthNpcPersistentRecords" ||
            root.GetAttribute("format") != CurrentFormat.ToString(CultureInfo.InvariantCulture) ||
            !string.Equals(root.GetAttribute("saveScope"), expectedScope ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Staged aggregate identity/format mismatch.");
        var ids = new HashSet<RebirthNpcStableId>();
        foreach (XmlElement element in root.SelectNodes("record"))
        {
            RebirthNpcPersistentRecord record = ReadRecord(element);
            string error;
            if (record == null || record.Identity == null ||
                !ValidatePublication(record, record.Identity.StableNpcId, out error) ||
                !string.Equals(record.AggregateChecksum,ComputeRecordChecksum(record),StringComparison.OrdinalIgnoreCase) ||
                !ids.Add(record.Identity.StableNpcId))
                throw new InvalidDataException("Staged aggregate contains an invalid or duplicate record.");
        }
        if (ids.Count != expectedCount) throw new InvalidDataException("Staged aggregate record count changed.");
    }

    public static bool ValidateAtomicConsistency(out string detail)
    {
        EnsureLoaded();RebirthNpcPersistenceFileSnapshot[] files=RebirthNpcPersistenceCoordinator.CaptureFileSnapshots();var byName=new Dictionary<string,RebirthNpcPersistenceFileSnapshot>(StringComparer.OrdinalIgnoreCase);for(int i=0;i<files.Length;i++)byName[files[i].FileName]=files[i];int failures=0;RebirthNpcPersistentRecord[] records=Snapshot();for(int i=0;i<records.Length;i++){var m=records[i].DomainManifest??new RebirthNpcDomainRevisionManifest[0];for(int j=0;j<m.Length;j++){RebirthNpcPersistenceFileSnapshot f;if(!byName.TryGetValue(m[j].FileName,out f)){if(m[j].Required)failures++;continue;}if(m[j].Required&&!f.Exists){failures++;continue;}if(f.Exists&&(f.Length!=m[j].Length||!string.Equals(f.Sha256,m[j].Sha256,StringComparison.OrdinalIgnoreCase)))failures++;}if(!string.Equals(records[i].AggregateChecksum,ComputeRecordChecksum(records[i]),StringComparison.OrdinalIgnoreCase))failures++;}if(failures>0)consistencyFailures+=failures;detail="records="+records.Length+" consistencyFailures="+failures;return failures==0;}

    public static string GetReport(){string d;bool ok=ValidateAtomicConsistency(out d);return "[REBIRTH NPC ACIP-11 Aggregate] format="+CurrentFormat+" loaded="+loaded+" records="+Snapshot().Length+" loads="+loads+" saves="+saves+" recovered="+recovered+" duplicateImports="+duplicateImports+" consistency="+(ok?"PASS":"FAIL")+" "+GetSaveScopeReport()+" "+d;}
    public static void Reset(bool saveFirst){if(saveFirst)Save();lock(Sync){Records.Clear();QuarantinedRecords.Clear();loaded=false;dirty=false;InvalidateAllReadViewsLocked();retryLoadAfterUtcTicks=0;unchecked{storeEpoch++;}loadedSaveDirectory=string.Empty;loadedSaveScope=string.Empty;provenanceState="reset";}}

    private static RebirthNpcPersistentRecord CreateEmpty(RebirthNpcStableId id,long now){return new RebirthNpcPersistentRecord{Identity=new RebirthNpcIdentityRecord{StableNpcId=id,CreationReason=RebirthNpcCreationReason.ScriptedCreation,CreationWorldTime=now,NameRevision=1,Species="human"},Profile=new RebirthNpcProfileBindingRecord(),Lifecycle=new RebirthNpcLifecycleRecord{LifecycleDomain="persistent",PersistentSinceWorldTime=now},Presence=new RebirthNpcPresenceRecord{PresenceState="Unknown",TransitionStartedWorldTime=now,EmbodimentGeneration=1},Transform=new RebirthNpcTransformRecord(),Vitals=new RebirthNpcVitalStateRecord{DeathOrIncapacitationState="alive",ActivePersistentBuffSet=new string[0]},ControllerFragments=new RebirthNpcControllerFragmentSet{Fragments=new RebirthNpcControllerFragment[0]},Audit=new RebirthNpcAuditRecord{CreatedByBuild="REBIRTH-3.0-ACIP-11",LastWrittenByBuild="REBIRTH-3.0-ACIP-11",RecoveryFlags=new string[0]},DomainManifest=new RebirthNpcDomainRevisionManifest[0],UnknownOptionalElements=new string[0],AggregateRevision=1};}
    private static string ResolveCategory(string profile){try{return RebirthNpcProfileRegistry.ResolveRequired(profile).Category.ToString();}catch{return "Unknown";}}
    private static RebirthNpcDomainRevisionManifest[] CaptureDomainManifest()
    {
        RebirthNpcPersistenceFileSnapshot[] files = RebirthNpcPersistenceCoordinator.CaptureFileSnapshots();
        var result = new List<RebirthNpcDomainRevisionManifest>();
        for (int i = 0; i < files.Length; i++)
        {
            RebirthNpcPersistenceFileSnapshot f = files[i];
            if (f == null || string.Equals(f.FileName, FileName, StringComparison.OrdinalIgnoreCase)) continue;
            bool required = f.Exists || WasPreviouslyRequiredDomain(f.FileName);
            result.Add(new RebirthNpcDomainRevisionManifest { DomainId=Path.GetFileNameWithoutExtension(f.FileName),
                FileName=f.FileName, DomainRevision=1, Length=f.Length, Sha256=f.Sha256??string.Empty, Required=required });
        }
        return result.ToArray();
    }
    private static bool WasPreviouslyRequiredDomain(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        lock (Sync)
        {
            foreach (RebirthNpcPersistentRecord record in Records.Values)
            {
                RebirthNpcDomainRevisionManifest[] manifest = record != null ? record.DomainManifest : null;
                for (int i = 0; manifest != null && i < manifest.Length; i++)
                {
                    RebirthNpcDomainRevisionManifest domain = manifest[i];
                    if (domain != null && domain.Required &&
                        string.Equals(domain.FileName ?? string.Empty, fileName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }
    }
    private static List<RebirthNpcPersistentRecord> SortedRecords(){var a=new List<RebirthNpcPersistentRecord>(Records.Values);a.Sort((x,y)=>string.CompareOrdinal(x.Identity.StableNpcId.ToString(),y.Identity.StableNpcId.ToString()));return a;}
    private static string PathFor(string name){string d=GameIO.GetSaveGameDir();return string.IsNullOrEmpty(d)?string.Empty:Path.Combine(d,name);}
    private static bool TryLoad(string path, string source, string expectedScope, bool unscopedIsUntrusted)
    {
        if (!File.Exists(path)) return false;
        try
        {
            XmlDocument d = new XmlDocument();
            d.Load(path);
            XmlElement root = d.DocumentElement;
            int format;
            if (root == null || root.Name != "rebirthNpcPersistentRecords" ||
                !int.TryParse(root.GetAttribute("format"), out format) ||
                format < 1 || format > CurrentFormat)
                throw new InvalidDataException("unsupported aggregate format");

            string fileScope = root.GetAttribute("saveScope") ?? string.Empty;
            bool hasScope = !string.IsNullOrWhiteSpace(fileScope);
            bool foreignScope = hasScope && !string.Equals(fileScope, expectedScope ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            bool quarantineLegacyDogs = !hasScope && unscopedIsUntrusted;

            var stagedRecords = new Dictionary<RebirthNpcStableId, RebirthNpcPersistentRecord>();
            var stagedQuarantine = new Dictionary<RebirthNpcStableId, RebirthNpcPersistentRecord>();
            var seenIds = new HashSet<RebirthNpcStableId>();
            int quarantinedThisLoad = 0;
            int rejectedThisLoad = 0;

            foreach (XmlElement e in root.SelectNodes("record"))
            {
                RebirthNpcPersistentRecord r = ReadRecord(e);
                string validationError;
                if (r == null || r.Identity == null || !ValidatePublication(r, r.Identity.StableNpcId, out validationError))
                    throw new InvalidDataException("invalid aggregate record");
                if (!seenIds.Add(r.Identity.StableNpcId)) throw new InvalidDataException("duplicate aggregate identity");

                if (foreignScope)
                {
                    stagedQuarantine[r.Identity.StableNpcId] = r;
                    rejectedThisLoad++;
                    continue;
                }

                if (quarantineLegacyDogs && IsDogRecord(r))
                {
                    stagedQuarantine[r.Identity.StableNpcId] = r;
                    quarantinedThisLoad++;
                    continue;
                }

                stagedRecords[r.Identity.StableNpcId] = r;
            }

            Records = stagedRecords; QuarantinedRecords = stagedQuarantine; InvalidateAllReadViewsLocked();
            dirty = false;

            if (foreignScope)
            {
                rejectedForeignRecords += rejectedThisLoad;
                provenanceState = "foreign-scope-quarantined";
                dirty = true;
                Log.Warning("[REBIRTH NPC Aggregate] foreign saveScope rejected file='" + path +
                            "' fileScope=" + ShortScope(fileScope) +
                            " currentScope=" + ShortScope(expectedScope) +
                            " records=" + rejectedThisLoad + ".");
            }
            else if (quarantineLegacyDogs && quarantinedThisLoad > 0)
            {
                quarantinedDogRecords += quarantinedThisLoad;
                provenanceState = "legacy-cross-save-dogs-quarantined";
                dirty = true; // normal save rewrites a clean, scoped active aggregate
                Log.Warning("[REBIRTH NPC Aggregate] unscoped legacy dog records quarantined after save switch file='" +
                            path + "' dogs=" + quarantinedThisLoad +
                            ". They will not count/show/repair unless a real current-save dog proves the same embedded StableId.");
            }
            else
            {
                provenanceState = hasScope ? "scoped-current" : "legacy-first-save-trusted";
                if (!hasScope) dirty = true; // stamp provenance on the next normal save
            }

            if (format < CurrentFormat)
            {
                // Supported older formats are loaded through compatibility defaults and then
                // rewritten only by the normal save path; never mutate the source file in-place
                // during load.
                dirty = true;
                provenanceState = provenanceState + "-migrate-v" + format.ToString(CultureInfo.InvariantCulture);
            }
            loadedSaveScope = expectedScope ?? string.Empty;
            loads++;
            if (source == "backup") recovered++;
            RebirthNpcPersistenceSchemaTelemetry.RecordLoad("aggregate", PathFor(FileName), format, CurrentFormat, source);
            return true;
        }
        catch (Exception ex)
        {
            RebirthNpcPersistenceSchemaTelemetry.RecordRejected("aggregate", PathFor(FileName), CurrentFormat, ex.Message);
            return false;
        }
    }

    private static bool IsDogRecord(RebirthNpcPersistentRecord record)
    {
        if (record == null) return false;
        if (record.Dog != null) return true;
        if (record.Profile != null &&
            string.Equals(record.Profile.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase))
            return true;
        return record.Identity != null &&
            string.Equals(record.Identity.Category ?? string.Empty, RebirthNpcCategory.DogCompanion.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ShortScope(string value)
    {
        if (string.IsNullOrEmpty(value)) return "<none>";
        return value.Length <= 12 ? value : value.Substring(0, 12);
    }
    private static XmlElement WriteRecord(XmlDocument d, RebirthNpcPersistentRecord r)
    {
        XmlElement e = d.CreateElement("record");
        e.SetAttribute("stableId", r.Identity.StableNpcId.ToString());
        e.SetAttribute("category", r.Identity.Category ?? "");
        e.SetAttribute("species", r.Identity.Species ?? "");
        e.SetAttribute("name", r.Identity.GeneratedOrAssignedDisplayName ?? "");
        e.SetAttribute("nameRevision", r.Identity.NameRevision.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("creationWorldTime", r.Identity.CreationWorldTime.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("creationReason", r.Identity.CreationReason.ToString());
        e.SetAttribute("originSpawnGroup", r.Identity.OriginSpawnGroup ?? "");
        e.SetAttribute("originPrefab", r.Identity.OriginPrefab ?? "");
        e.SetAttribute("originScenario", r.Identity.OriginScenario ?? "");
        e.SetAttribute("profileId", r.Profile.ProfileId ?? "");
        e.SetAttribute("profileSchema", r.Profile.ProfileSchemaVersionAtBind.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("profileHash", r.Profile.ProfileContentHashAtBind ?? "");
        e.SetAttribute("profileCompatibility", r.Profile.ProfileCompatibilityClass ?? "");
        e.SetAttribute("profileResolutionRevision", r.Profile.LastSuccessfulResolutionRevision.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("lifecycle", r.Lifecycle.LifecycleDomain ?? "");
        e.SetAttribute("lifecycleRevision", r.Lifecycle.LifecycleRevision.ToString(CultureInfo.InvariantCulture));
        if(r.Lifecycle.PersistentSinceWorldTime.HasValue)e.SetAttribute("persistentSince",r.Lifecycle.PersistentSinceWorldTime.Value.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("dismissalState",r.Lifecycle.DismissalState??""); e.SetAttribute("removalReason",r.Lifecycle.RemovalReason??"");
        e.SetAttribute("tombstone",r.Lifecycle.TombstoneState?"1":"0"); if(r.Lifecycle.TombstoneWorldTime.HasValue)e.SetAttribute("tombstoneTime",r.Lifecycle.TombstoneWorldTime.Value.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("presence", r.Presence.PresenceState ?? "");
        e.SetAttribute("presenceRevision",r.Presence.PresenceRevision.ToString(CultureInfo.InvariantCulture)); e.SetAttribute("presenceReason",r.Presence.TransitionReason??"");
        e.SetAttribute("presenceStarted",r.Presence.TransitionStartedWorldTime.ToString(CultureInfo.InvariantCulture)); if(r.Presence.ExpectedReturnWorldTime.HasValue)e.SetAttribute("presenceReturn",r.Presence.ExpectedReturnWorldTime.Value.ToString(CultureInfo.InvariantCulture));
        if(r.Presence.SuspendedRuntimeEntityId.HasValue)e.SetAttribute("suspendedEntity",r.Presence.SuspendedRuntimeEntityId.Value.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("embodimentGeneration",r.Presence.EmbodimentGeneration.ToString(CultureInfo.InvariantCulture)); e.SetAttribute("lastKnownChunk",r.Presence.LastKnownChunkKey??"");
        e.SetAttribute("revision", r.AggregateRevision.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("checksum", r.AggregateChecksum ?? "");
        e.SetAttribute("migrationSource", r.Audit.MigrationSource ?? "");
        e.SetAttribute("migrationFingerprint", r.Audit.MigrationFingerprint ?? "");

        if (r.HumanAppearance.HasValue) e.AppendChild(RebirthNpcAppearanceBinaryCodec.WriteAggregate(d, r.HumanAppearance.Value));
        if (r.NativeReconstruction != null) e.AppendChild(r.NativeReconstruction.WriteAggregate(d));
        if (r.Ownership != null)
        {
            XmlElement x = d.CreateElement("ownership");
            x.SetAttribute("state", r.Ownership.OwnershipState ?? "");
            x.SetAttribute("owner", r.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? ""); x.SetAttribute("character",r.Ownership.OwnerCharacterIdIfRequired??"");
            x.SetAttribute("revision", r.Ownership.OwnerRevision.ToString(CultureInfo.InvariantCulture)); x.SetAttribute("hired",r.Ownership.HiredWorldTime.ToString(CultureInfo.InvariantCulture));
            if(r.Ownership.DismissedWorldTime.HasValue)x.SetAttribute("dismissed",r.Ownership.DismissedWorldTime.Value.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("permission",r.Ownership.PermissionPolicyId??""); x.SetAttribute("partyAccess",r.Ownership.PartyAccessMode??"");
            e.AppendChild(x);
        }
        if (r.Order != null)
        {
            XmlElement x = d.CreateElement("order");
            x.SetAttribute("state", r.Order.OrderState ?? "");
            x.SetAttribute("target", r.Order.OrderTarget ?? "");
            x.SetAttribute("previous", r.Order.PreviousOrderState ?? "");
            x.SetAttribute("revision", r.Order.OrderRevision.ToString(CultureInfo.InvariantCulture));
            e.AppendChild(x);
        }
        if (r.Transform != null)
        {
            XmlElement x = d.CreateElement("transform");
            x.SetAttribute("position", FormatVector(r.Transform.WorldPosition));
            x.SetAttribute("yaw", r.Transform.RotationYaw.ToString("R", CultureInfo.InvariantCulture));
            if(r.Transform.OptionalRotationPitch.HasValue)x.SetAttribute("pitch",r.Transform.OptionalRotationPitch.Value.ToString("R",CultureInfo.InvariantCulture));
            x.SetAttribute("revision", r.Transform.TransformRevision.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(r.Transform.ChunkKey)) x.SetAttribute("chunk", r.Transform.ChunkKey);
            if (r.Transform.LastSafePosition.HasValue) x.SetAttribute("lastSafe", FormatVector(r.Transform.LastSafePosition.Value));
            if (r.Transform.LastSafePositionWorldTime.HasValue) x.SetAttribute("lastSafeTime", r.Transform.LastSafePositionWorldTime.Value.ToString(CultureInfo.InvariantCulture));
            if (r.Transform.AnchorPosition.HasValue) x.SetAttribute("anchor", FormatVector(r.Transform.AnchorPosition.Value));
            if(r.Transform.AnchorRotation.HasValue)x.SetAttribute("anchorRotation",r.Transform.AnchorRotation.Value.ToString("R",CultureInfo.InvariantCulture));
            e.AppendChild(x);
        }
        if (r.Vitals != null)
        {
            XmlElement x = d.CreateElement("vitals");
            x.SetAttribute("health", r.Vitals.CurrentHealth.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("maxHealth", r.Vitals.MaximumHealthAtSave.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("state", r.Vitals.DeathOrIncapacitationState ?? "");
            if(r.Vitals.CurrentStaminaIfApplicable.HasValue)x.SetAttribute("stamina",r.Vitals.CurrentStaminaIfApplicable.Value.ToString("R",CultureInfo.InvariantCulture));
            x.SetAttribute("buffs",string.Join("|",r.Vitals.ActivePersistentBuffSet??new string[0]));
            x.SetAttribute("revision", r.Vitals.VitalsRevision.ToString(CultureInfo.InvariantCulture));
            e.AppendChild(x);
        }
        if (r.Inventory != null)
        {
            XmlElement x = d.CreateElement("inventory");
            x.SetAttribute("schema", r.Inventory.InventorySchemaVersion.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("slots", r.Inventory.SlotCount.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("revision", r.Inventory.InventoryRevision.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("checksum", r.Inventory.ContentChecksum ?? "");
            WriteSlots(d, x, r.Inventory.SlotRecords, "slot");
            WriteSlots(d, x, r.Inventory.OverflowRecords, "overflow");
            e.AppendChild(x);
        }
        if (r.Equipment != null)
        {
            XmlElement x = d.CreateElement("equipment");
            x.SetAttribute("schema", r.Equipment.EquipmentSchemaVersion.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("revision", r.Equipment.EquipmentRevision.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("held",r.Equipment.ActiveHeldSlot??""); x.SetAttribute("ammo",r.Equipment.ActiveAmmoStateIfNotNative??"");
            foreach (var q in r.Equipment.EquipmentSlots ?? new RebirthNpcEquipmentSlotRecord[0])
            {
                XmlElement slot = d.CreateElement("slot");
                slot.SetAttribute("id", q.SlotId ?? "");
                slot.SetAttribute("item", q.ItemValueSerialization ?? "");
                slot.SetAttribute("count", q.ItemCount.ToString(CultureInfo.InvariantCulture));
                slot.SetAttribute("metadata", q.MetadataOrCustomData ?? "");
                x.AppendChild(slot);
            }
            e.AppendChild(x);
        }
        if (r.Respawn != null)
        {
            XmlElement x = d.CreateElement("respawn");
            x.SetAttribute("policy", r.Respawn.RespawnPolicyId ?? "");
            x.SetAttribute("state", r.Respawn.RespawnState ?? "");
            x.SetAttribute("anchorType", r.Respawn.PreferredAnchorType ?? "");
            x.SetAttribute("anchorId", r.Respawn.PreferredAnchorIdentity ?? "");
            if (r.Respawn.PreferredAnchorPosition.HasValue) x.SetAttribute("anchorPosition", FormatVector(r.Respawn.PreferredAnchorPosition.Value));
            if (r.Respawn.PreferredAnchorRotation.HasValue) x.SetAttribute("anchorRotation", r.Respawn.PreferredAnchorRotation.Value.ToString("R", CultureInfo.InvariantCulture));
            x.SetAttribute("retainedInventory", r.Respawn.RetainedInventoryPolicy ?? "");
            x.SetAttribute("revision", r.Respawn.RespawnRevision.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("attempts", r.Respawn.AttemptCount.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("lastFailure", r.Respawn.LastFailureReason ?? "");
            if (r.Respawn.DeathWorldTime.HasValue) x.SetAttribute("death", r.Respawn.DeathWorldTime.Value.ToString(CultureInfo.InvariantCulture));
            if (r.Respawn.EligibleRespawnWorldTime.HasValue) x.SetAttribute("eligible", r.Respawn.EligibleRespawnWorldTime.Value.ToString(CultureInfo.InvariantCulture));
            if (r.Respawn.FallbackPosition.HasValue) x.SetAttribute("fallback", FormatVector(r.Respawn.FallbackPosition.Value));
            e.AppendChild(x);
        }
        if (r.Contract != null)
        {
            XmlElement x=d.CreateElement("contract"); x.SetAttribute("state",r.Contract.ContractState??""); x.SetAttribute("id",r.Contract.ContractId??"");
            x.SetAttribute("started",r.Contract.StartedWorldTime.ToString(CultureInfo.InvariantCulture)); if(r.Contract.ExpiresWorldTime.HasValue)x.SetAttribute("expires",r.Contract.ExpiresWorldTime.Value.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("wage",r.Contract.WageAmount.ToString(CultureInfo.InvariantCulture)); if(r.Contract.NextWageWorldTime.HasValue)x.SetAttribute("nextWage",r.Contract.NextWageWorldTime.Value.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("expiryDisposition",r.Contract.ExpiryDisposition??""); x.SetAttribute("revision",r.Contract.ContractRevision.ToString(CultureInfo.InvariantCulture)); e.AppendChild(x);
        }
        if (r.Work != null)
        {
            XmlElement x=d.CreateElement("work"); x.SetAttribute("state",r.Work.WorkState??""); x.SetAttribute("assignment",r.Work.AssignmentId??""); x.SetAttribute("type",r.Work.WorkType??""); x.SetAttribute("target",r.Work.TargetReference??"");
            x.SetAttribute("reservations",string.Join("|",r.Work.ReservationIds??new string[0])); x.SetAttribute("checkpoint",r.Work.ProgressCheckpoint??""); x.SetAttribute("sourcePolicy",r.Work.ResourceSourcePolicy??""); x.SetAttribute("outputPolicy",r.Work.OutputDestinationPolicy??""); x.SetAttribute("revision",r.Work.WorkRevision.ToString(CultureInfo.InvariantCulture)); e.AppendChild(x);
        }
        if (r.Mission != null)
        {
            XmlElement x=d.CreateElement("mission"); x.SetAttribute("state",r.Mission.MissionState??""); x.SetAttribute("id",r.Mission.MissionId??""); x.SetAttribute("started",r.Mission.StartedWorldTime.ToString(CultureInfo.InvariantCulture)); if(r.Mission.ExpectedReturnWorldTime.HasValue)x.SetAttribute("return",r.Mission.ExpectedReturnWorldTime.Value.ToString(CultureInfo.InvariantCulture)); x.SetAttribute("outcome",r.Mission.Outcome??""); x.SetAttribute("revision",r.Mission.MissionRevision.ToString(CultureInfo.InvariantCulture)); e.AppendChild(x);
        }
        if (r.ControllerFragments != null)
        {
            XmlElement x=d.CreateElement("controllerFragments");
            foreach(RebirthNpcControllerFragment f in r.ControllerFragments.Fragments??new RebirthNpcControllerFragment[0])
            {
                if(f==null)continue; XmlElement q=d.CreateElement("fragment"); q.SetAttribute("type",f.ControllerTypeId??""); q.SetAttribute("version",f.FragmentVersion.ToString(CultureInfo.InvariantCulture));
                q.SetAttribute("length",f.FragmentLength.ToString(CultureInfo.InvariantCulture)); q.SetAttribute("checksum",f.FragmentChecksum??""); q.SetAttribute("required",f.Required?"1":"0"); q.InnerText=Convert.ToBase64String(f.Bytes??new byte[0]); x.AppendChild(q);
            }
            e.AppendChild(x);
        }
        if (r.Audit != null)
        {
            XmlElement x=d.CreateElement("audit"); x.SetAttribute("createdBy",r.Audit.CreatedByBuild??""); x.SetAttribute("writtenBy",r.Audit.LastWrittenByBuild??""); x.SetAttribute("mutation",r.Audit.LastMutationKind??"");
            x.SetAttribute("worldTime",r.Audit.LastMutationWorldTime.ToString(CultureInfo.InvariantCulture)); x.SetAttribute("serverTick",r.Audit.LastMutationServerTick.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("ownerName",r.Audit.LastKnownOwnerNameForDiagnosticsOnly??""); x.SetAttribute("recoveryFlags",string.Join("|",r.Audit.RecoveryFlags??new string[0])); e.AppendChild(x);
        }
        if (r.Dog != null)
            e.AppendChild(RebirthDogPersistentRecord.WriteXml(d, r.Dog));
        if (r.BoundUndead != null)
            e.AppendChild(RebirthBoundUndeadPersistentRecord.WriteXml(d, r.BoundUndead));

        XmlElement m = d.CreateElement("domainManifest");
        foreach (var q in r.DomainManifest ?? new RebirthNpcDomainRevisionManifest[0])
        {
            XmlElement x = d.CreateElement("domain");
            x.SetAttribute("id", q.DomainId ?? "");
            x.SetAttribute("file", q.FileName ?? "");
            x.SetAttribute("revision", q.DomainRevision.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("length", q.Length.ToString(CultureInfo.InvariantCulture));
            x.SetAttribute("sha256", q.Sha256 ?? "");
            x.SetAttribute("required", q.Required ? "1" : "0");
            m.AppendChild(x);
        }
        e.AppendChild(m);
        foreach (string rawExtension in r.UnknownOptionalElements ?? new string[0])
        {
            if (string.IsNullOrWhiteSpace(rawExtension)) continue;
            try
            {
                XmlDocument extensionDocument = new XmlDocument();
                extensionDocument.LoadXml(rawExtension);
                XmlNode extension = d.ImportNode(extensionDocument.DocumentElement, true);
                e.AppendChild(extension);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Stored optional aggregate extension is malformed.", ex);
            }
        }
        return e;
    }

    private static string FormatVector(Vector3 value)
    {
        return value.x.ToString("R", CultureInfo.InvariantCulture) + "," +
               value.y.ToString("R", CultureInfo.InvariantCulture) + "," +
               value.z.ToString("R", CultureInfo.InvariantCulture);
    }

    private static Vector3 ParseVector3(string value)
    {
        string[] p = (value ?? string.Empty).Split(',');
        if (p.Length != 3) return Vector3.zero;
        float x, y, z;
        if (!float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
            !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
            !float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return Vector3.zero;
        return new Vector3(x, y, z);
    }

    private static void WriteSlots(XmlDocument d,XmlElement parent,RebirthNpcInventorySlotRecord[] slots,string name){foreach(var s in slots??new RebirthNpcInventorySlotRecord[0]){XmlElement x=d.CreateElement(name);x.SetAttribute("index",s.SlotIndex.ToString(CultureInfo.InvariantCulture));x.SetAttribute("item",s.ItemValueSerialization??"");x.SetAttribute("count",s.ItemCount.ToString(CultureInfo.InvariantCulture));x.SetAttribute("metadata",s.MetadataOrCustomData??"");x.SetAttribute("locked",s.Locked?"1":"0");x.SetAttribute("reserved",s.Reserved?"1":"0");parent.AppendChild(x);}}
    private static RebirthNpcPersistentRecord ReadRecord(XmlElement e)
    {
        RebirthNpcStableId id; uint rev;
        if (!RebirthNpcStableId.TryParse(e.GetAttribute("stableId"), out id) || !uint.TryParse(e.GetAttribute("revision"), out rev)) return null;
        long now = DateTime.UtcNow.Ticks;
        RebirthNpcPersistentRecord r = CreateEmpty(id, now);
        r.Identity.Category = e.GetAttribute("category");
        r.Identity.Species = e.GetAttribute("species");
        r.Identity.GeneratedOrAssignedDisplayName = e.GetAttribute("name");
        uint nameRevision; if(uint.TryParse(e.GetAttribute("nameRevision"),out nameRevision))r.Identity.NameRevision=nameRevision;
        long creationTime; if(long.TryParse(e.GetAttribute("creationWorldTime"),out creationTime))r.Identity.CreationWorldTime=creationTime;
        RebirthNpcCreationReason cr; if (Enum.TryParse(e.GetAttribute("creationReason"), true, out cr)) r.Identity.CreationReason = cr;
        r.Identity.OriginSpawnGroup=e.GetAttribute("originSpawnGroup"); r.Identity.OriginPrefab=e.GetAttribute("originPrefab"); r.Identity.OriginScenario=e.GetAttribute("originScenario");
        if (!RebirthNpcAppearanceBinaryCodec.TryReadAggregate(e, out var savedAppearance)) return null;
        r.HumanAppearance = savedAppearance;
        if (!RebirthNpcNativeReconstruction.TryReadAggregate(e, out var reconstruction)) return null;
        r.NativeReconstruction = reconstruction;
        r.Profile.ProfileId = e.GetAttribute("profileId");
        int ps; int.TryParse(e.GetAttribute("profileSchema"), out ps); r.Profile.ProfileSchemaVersionAtBind = ps;
        r.Profile.ProfileContentHashAtBind=e.GetAttribute("profileHash"); r.Profile.ProfileCompatibilityClass=e.GetAttribute("profileCompatibility"); uint prr;if(uint.TryParse(e.GetAttribute("profileResolutionRevision"),out prr))r.Profile.LastSuccessfulResolutionRevision=prr;
        r.Lifecycle.LifecycleDomain = e.GetAttribute("lifecycle"); uint lr;if(uint.TryParse(e.GetAttribute("lifecycleRevision"),out lr))r.Lifecycle.LifecycleRevision=lr; long ltime;if(long.TryParse(e.GetAttribute("persistentSince"),out ltime))r.Lifecycle.PersistentSinceWorldTime=ltime;
        r.Lifecycle.DismissalState=e.GetAttribute("dismissalState");r.Lifecycle.RemovalReason=e.GetAttribute("removalReason");r.Lifecycle.TombstoneState=e.GetAttribute("tombstone")=="1";if(long.TryParse(e.GetAttribute("tombstoneTime"),out ltime))r.Lifecycle.TombstoneWorldTime=ltime;
        r.Presence.PresenceState = e.GetAttribute("presence"); uint prev;if(uint.TryParse(e.GetAttribute("presenceRevision"),out prev))r.Presence.PresenceRevision=prev;r.Presence.TransitionReason=e.GetAttribute("presenceReason");
        long pt;if(long.TryParse(e.GetAttribute("presenceStarted"),out pt))r.Presence.TransitionStartedWorldTime=pt;if(long.TryParse(e.GetAttribute("presenceReturn"),out pt))r.Presence.ExpectedReturnWorldTime=pt;
        int se;if(int.TryParse(e.GetAttribute("suspendedEntity"),out se))r.Presence.SuspendedRuntimeEntityId=se;uint eg;if(uint.TryParse(e.GetAttribute("embodimentGeneration"),out eg))r.Presence.EmbodimentGeneration=eg;r.Presence.LastKnownChunkKey=e.GetAttribute("lastKnownChunk");
        r.AggregateRevision = rev; r.AggregateChecksum = e.GetAttribute("checksum");
        r.Audit.MigrationSource = e.GetAttribute("migrationSource"); r.Audit.MigrationFingerprint = e.GetAttribute("migrationFingerprint");

        XmlElement o = e.SelectSingleNode("ownership") as XmlElement;
        if (o != null)
        {
            uint rr; uint.TryParse(o.GetAttribute("revision"), out rr);
            long hired=0,dismissed; long.TryParse(o.GetAttribute("hired"),out hired);
            r.Ownership = new RebirthNpcOwnershipRecord { OwnershipState = o.GetAttribute("state"), OwnerPlatformIdOrPersistentPlayerId = o.GetAttribute("owner"), OwnerCharacterIdIfRequired=o.GetAttribute("character"), OwnerRevision = rr, HiredWorldTime=hired, PermissionPolicyId=o.GetAttribute("permission"), PartyAccessMode=o.GetAttribute("partyAccess") };
            if(long.TryParse(o.GetAttribute("dismissed"),out dismissed))r.Ownership.DismissedWorldTime=dismissed;
        }
        XmlElement ord = e.SelectSingleNode("order") as XmlElement;
        if (ord != null)
        {
            uint rr; uint.TryParse(ord.GetAttribute("revision"), out rr);
            r.Order = new RebirthNpcOrderRecord { OrderState = ord.GetAttribute("state"), OrderTarget = ord.GetAttribute("target"), PreviousOrderState = ord.GetAttribute("previous"), OrderRevision = rr };
        }
        XmlElement tx = e.SelectSingleNode("transform") as XmlElement;
        if (tx != null)
        {
            uint rr; uint.TryParse(tx.GetAttribute("revision"), out rr);
            r.Transform.WorldPosition = ParseVector3(tx.GetAttribute("position"));
            float yaw; if (float.TryParse(tx.GetAttribute("yaw"), NumberStyles.Float, CultureInfo.InvariantCulture, out yaw)) r.Transform.RotationYaw = yaw;
            float pitch; if(float.TryParse(tx.GetAttribute("pitch"),NumberStyles.Float,CultureInfo.InvariantCulture,out pitch))r.Transform.OptionalRotationPitch=pitch;
            r.Transform.TransformRevision = rr;
            if (tx.HasAttribute("chunk"))
            {
                r.Transform.ChunkKey = tx.GetAttribute("chunk");
                if (r.Presence != null) r.Presence.LastKnownChunkKey = r.Transform.ChunkKey;
            }
            if (tx.HasAttribute("lastSafe")) r.Transform.LastSafePosition = ParseVector3(tx.GetAttribute("lastSafe"));
            long lastSafeTime;
            if (tx.HasAttribute("lastSafeTime") && long.TryParse(tx.GetAttribute("lastSafeTime"), NumberStyles.Integer, CultureInfo.InvariantCulture, out lastSafeTime))
                r.Transform.LastSafePositionWorldTime = lastSafeTime;
            if (tx.HasAttribute("anchor")) r.Transform.AnchorPosition = ParseVector3(tx.GetAttribute("anchor"));
            float anchorRotation;if(float.TryParse(tx.GetAttribute("anchorRotation"),NumberStyles.Float,CultureInfo.InvariantCulture,out anchorRotation))r.Transform.AnchorRotation=anchorRotation;
        }
        XmlElement vit = e.SelectSingleNode("vitals") as XmlElement;
        if (vit != null)
        {
            int h, mh; uint rr; int.TryParse(vit.GetAttribute("health"), out h); int.TryParse(vit.GetAttribute("maxHealth"), out mh); uint.TryParse(vit.GetAttribute("revision"), out rr);
            r.Vitals.CurrentHealth = h; r.Vitals.MaximumHealthAtSave = mh; r.Vitals.DeathOrIncapacitationState = vit.GetAttribute("state"); r.Vitals.VitalsRevision = rr;
            float stamina;if(float.TryParse(vit.GetAttribute("stamina"),NumberStyles.Float,CultureInfo.InvariantCulture,out stamina))r.Vitals.CurrentStaminaIfApplicable=stamina;
            string buffs=vit.GetAttribute("buffs");r.Vitals.ActivePersistentBuffSet=string.IsNullOrEmpty(buffs)?new string[0]:buffs.Split('|');
        }
        XmlElement inv = e.SelectSingleNode("inventory") as XmlElement;
        if (inv != null)
        {
            int schema, slotCount; uint rr; int.TryParse(inv.GetAttribute("schema"), out schema); int.TryParse(inv.GetAttribute("slots"), out slotCount); uint.TryParse(inv.GetAttribute("revision"), out rr);
            r.Inventory = new RebirthNpcInventoryRecord
            {
                InventorySchemaVersion = schema, SlotCount = slotCount, InventoryRevision = rr, ContentChecksum = inv.GetAttribute("checksum"),
                SlotRecords = ReadSlots(inv, "slot"), OverflowRecords = ReadSlots(inv, "overflow")
            };
        }
        XmlElement eq = e.SelectSingleNode("equipment") as XmlElement;
        if(eq!=null)
        {
            int schema;uint rr;int.TryParse(eq.GetAttribute("schema"),out schema);uint.TryParse(eq.GetAttribute("revision"),out rr);
            var slots=new List<RebirthNpcEquipmentSlotRecord>();
            var slotIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(XmlElement x in eq.SelectNodes("slot"))
            {
                int count;if(!int.TryParse(x.GetAttribute("count"),NumberStyles.Integer,CultureInfo.InvariantCulture,out count)||count<0)
                    throw new InvalidDataException("Invalid NPC equipment item count.");
                if(!slotIds.Add(x.GetAttribute("id")))throw new InvalidDataException("Duplicate NPC equipment slot identity.");
                slots.Add(new RebirthNpcEquipmentSlotRecord{SlotId=x.GetAttribute("id"),ItemValueSerialization=x.GetAttribute("item"),ItemCount=count,MetadataOrCustomData=x.GetAttribute("metadata")});
            }
            r.Equipment=new RebirthNpcEquipmentRecord{EquipmentSchemaVersion=schema,EquipmentRevision=rr,ActiveHeldSlot=eq.GetAttribute("held"),ActiveAmmoStateIfNotNative=eq.GetAttribute("ammo"),EquipmentSlots=slots.ToArray()};
        }
        XmlElement resp = e.SelectSingleNode("respawn") as XmlElement;
        if (resp != null)
        {
            uint rr; int attempts; uint.TryParse(resp.GetAttribute("revision"), out rr); int.TryParse(resp.GetAttribute("attempts"), out attempts);
            long death, eligible;
            r.Respawn = new RebirthNpcRespawnRecord
            {
                RespawnPolicyId = resp.GetAttribute("policy"), RespawnState = resp.GetAttribute("state"), PreferredAnchorType = resp.GetAttribute("anchorType"),
                PreferredAnchorIdentity = resp.GetAttribute("anchorId"), RetainedInventoryPolicy = resp.GetAttribute("retainedInventory"), RespawnRevision = rr, AttemptCount = attempts, LastFailureReason = resp.GetAttribute("lastFailure")
            };
            if (long.TryParse(resp.GetAttribute("death"), out death)) r.Respawn.DeathWorldTime = death;
            if (long.TryParse(resp.GetAttribute("eligible"), out eligible)) r.Respawn.EligibleRespawnWorldTime = eligible;
            if (resp.HasAttribute("anchorPosition")) r.Respawn.PreferredAnchorPosition = ParseVector3(resp.GetAttribute("anchorPosition"));
            float anchorRotation;
            if (float.TryParse(resp.GetAttribute("anchorRotation"), NumberStyles.Float, CultureInfo.InvariantCulture, out anchorRotation)) r.Respawn.PreferredAnchorRotation = anchorRotation;
            if (resp.HasAttribute("fallback")) r.Respawn.FallbackPosition = ParseVector3(resp.GetAttribute("fallback"));
        }
        XmlElement contract=e.SelectSingleNode("contract") as XmlElement;
        if(contract!=null)
        {
            long started=0,expires,nextWage;int wage=0;uint rr=0;long.TryParse(contract.GetAttribute("started"),out started);int.TryParse(contract.GetAttribute("wage"),out wage);uint.TryParse(contract.GetAttribute("revision"),out rr);
            r.Contract=new RebirthNpcContractRecord{ContractState=contract.GetAttribute("state"),ContractId=contract.GetAttribute("id"),StartedWorldTime=started,WageAmount=wage,ExpiryDisposition=contract.GetAttribute("expiryDisposition"),ContractRevision=rr};
            if(long.TryParse(contract.GetAttribute("expires"),out expires))r.Contract.ExpiresWorldTime=expires;if(long.TryParse(contract.GetAttribute("nextWage"),out nextWage))r.Contract.NextWageWorldTime=nextWage;
        }
        XmlElement work=e.SelectSingleNode("work") as XmlElement;
        if(work!=null)
        {
            uint rr=0;uint.TryParse(work.GetAttribute("revision"),out rr);string reservations=work.GetAttribute("reservations");
            r.Work=new RebirthNpcWorkRecord{WorkState=work.GetAttribute("state"),AssignmentId=work.GetAttribute("assignment"),WorkType=work.GetAttribute("type"),TargetReference=work.GetAttribute("target"),ReservationIds=string.IsNullOrEmpty(reservations)?new string[0]:reservations.Split('|'),ProgressCheckpoint=work.GetAttribute("checkpoint"),ResourceSourcePolicy=work.GetAttribute("sourcePolicy"),OutputDestinationPolicy=work.GetAttribute("outputPolicy"),WorkRevision=rr};
        }
        XmlElement mission=e.SelectSingleNode("mission") as XmlElement;
        if(mission!=null)
        {
            long started=0,ret;uint rr=0;long.TryParse(mission.GetAttribute("started"),out started);uint.TryParse(mission.GetAttribute("revision"),out rr);
            r.Mission=new RebirthNpcMissionRecord{MissionState=mission.GetAttribute("state"),MissionId=mission.GetAttribute("id"),StartedWorldTime=started,Outcome=mission.GetAttribute("outcome"),MissionRevision=rr};if(long.TryParse(mission.GetAttribute("return"),out ret))r.Mission.ExpectedReturnWorldTime=ret;
        }
        XmlElement fragments=e.SelectSingleNode("controllerFragments") as XmlElement;
        if(fragments!=null)
        {
            var values=new List<RebirthNpcControllerFragment>();
            foreach(XmlElement x in fragments.SelectNodes("fragment"))
            {
                int version=0,length=0;int.TryParse(x.GetAttribute("version"),out version);int.TryParse(x.GetAttribute("length"),out length);byte[] bytes;
                try{bytes=Convert.FromBase64String(x.InnerText??string.Empty);}catch{bytes=new byte[0];}
                values.Add(new RebirthNpcControllerFragment{ControllerTypeId=x.GetAttribute("type"),FragmentVersion=version,FragmentLength=length,FragmentChecksum=x.GetAttribute("checksum"),Required=x.GetAttribute("required")=="1",Bytes=bytes});
            }
            r.ControllerFragments=new RebirthNpcControllerFragmentSet{FragmentCount=values.Count,Fragments=values.ToArray()};
        }
        XmlElement audit=e.SelectSingleNode("audit") as XmlElement;
        if(audit!=null)
        {
            long wt=0,tick=0;long.TryParse(audit.GetAttribute("worldTime"),out wt);long.TryParse(audit.GetAttribute("serverTick"),out tick);string flags=audit.GetAttribute("recoveryFlags");
            r.Audit.CreatedByBuild=audit.GetAttribute("createdBy");r.Audit.LastWrittenByBuild=audit.GetAttribute("writtenBy");r.Audit.LastMutationKind=audit.GetAttribute("mutation");r.Audit.LastMutationWorldTime=wt;r.Audit.LastMutationServerTick=tick;r.Audit.LastKnownOwnerNameForDiagnosticsOnly=audit.GetAttribute("ownerName");r.Audit.RecoveryFlags=string.IsNullOrEmpty(flags)?new string[0]:flags.Split('|');
        }
        XmlElement dog = e.SelectSingleNode("dog") as XmlElement;
        if (dog != null) r.Dog = RebirthDogPersistentRecord.ReadXml(dog);
        XmlElement boundUndead = e.SelectSingleNode("boundUndead") as XmlElement;
        if (boundUndead != null) r.BoundUndead = RebirthBoundUndeadPersistentRecord.ReadXml(boundUndead);

        var manifest = new List<RebirthNpcDomainRevisionManifest>();
        foreach (XmlElement x in e.SelectNodes("domainManifest/domain"))
        {
            uint dr; long len; uint.TryParse(x.GetAttribute("revision"), out dr); long.TryParse(x.GetAttribute("length"), out len);
            manifest.Add(new RebirthNpcDomainRevisionManifest { DomainId = x.GetAttribute("id"), FileName = x.GetAttribute("file"), DomainRevision = dr, Length = len, Sha256 = x.GetAttribute("sha256"), Required = x.GetAttribute("required") == "1" });
        }
        r.DomainManifest = manifest.ToArray();

        HashSet<string> knownChildren = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ownership","order","transform","vitals","inventory","equipment","respawn",
            "contract","work","mission","controllerFragments","audit","dog","boundUndead",
            "domainManifest","humanAppearance","nativeReconstruction"
        };
        List<string> unknownOptional = new List<string>();
        foreach (XmlNode childNode in e.ChildNodes)
        {
            XmlElement child = childNode as XmlElement;
            if (child == null || knownChildren.Contains(child.Name)) continue;
            bool requiredExtension = child.GetAttribute("required") == "1" ||
                string.Equals(child.GetAttribute("required"), "true", StringComparison.OrdinalIgnoreCase);
            if (requiredExtension)
                throw new InvalidDataException("Unknown required NPC aggregate extension: " + child.Name);
            unknownOptional.Add(child.OuterXml);
        }
        r.UnknownOptionalElements = unknownOptional.ToArray();
        return r;
    }

    private static RebirthNpcInventorySlotRecord[] ReadSlots(XmlElement parent, string name)
    {
        List<RebirthNpcInventorySlotRecord> result = new List<RebirthNpcInventorySlotRecord>();
        foreach (XmlElement x in parent.SelectNodes(name))
        {
            int index, count;
            if(!int.TryParse(x.GetAttribute("index"),NumberStyles.Integer,CultureInfo.InvariantCulture,out index)||
                !int.TryParse(x.GetAttribute("count"),NumberStyles.Integer,CultureInfo.InvariantCulture,out count)||count<0)
                throw new InvalidDataException("Invalid NPC inventory slot index/count.");
            result.Add(new RebirthNpcInventorySlotRecord
            {
                SlotIndex = index, ItemValueSerialization = x.GetAttribute("item"), ItemCount = count, MetadataOrCustomData = x.GetAttribute("metadata"),
                Locked = x.GetAttribute("locked") == "1", Reserved = x.GetAttribute("reserved") == "1"
            });
        }
        return result.ToArray();
    }

    private static string EquipmentFingerprint(RebirthNpcEquipmentRecord equipment)
    {
        if (equipment == null) return string.Empty;
        StringBuilder b = new StringBuilder();
        b.Append(equipment.EquipmentRevision).Append('|').Append(equipment.ActiveHeldSlot ?? string.Empty)
            .Append('|').Append(equipment.ActiveAmmoStateIfNotNative ?? string.Empty);
        RebirthNpcEquipmentSlotRecord[] slots = equipment.EquipmentSlots ?? new RebirthNpcEquipmentSlotRecord[0];
        for (int i = 0; i < slots.Length; i++)
        {
            RebirthNpcEquipmentSlotRecord slot = slots[i]; if (slot == null) continue;
            b.Append('|').Append(slot.SlotId ?? string.Empty).Append(':')
                .Append(slot.ItemValueSerialization ?? string.Empty).Append(':').Append(slot.ItemCount).Append(':').Append(slot.MetadataOrCustomData ?? string.Empty);
        }
        return HashText(b.ToString());
    }

    private static string ComputeRecordChecksum(RebirthNpcPersistentRecord r)
    {
        StringBuilder b=new StringBuilder();
        b.Append(r.Identity!=null?r.Identity.StableNpcId.ToString():"").Append('|').Append(r.Identity!=null?r.Identity.NameRevision:0)
         .Append('|').Append(r.Profile!=null?r.Profile.ProfileId:"").Append(':').Append(r.Profile!=null?r.Profile.LastSuccessfulResolutionRevision:0)
         .Append('|').Append(r.Lifecycle!=null?r.Lifecycle.LifecycleRevision:0).Append(':').Append(r.Lifecycle!=null&&r.Lifecycle.TombstoneState?"1":"0")
         .Append('|').Append(r.Presence!=null?r.Presence.PresenceRevision:0).Append(':').Append(r.Presence!=null?r.Presence.EmbodimentGeneration:0)
         .Append('|').Append(r.Ownership!=null?r.Ownership.OwnerRevision:0).Append('|').Append(r.Order!=null?r.Order.OrderRevision:0)
         .Append('|').Append(r.Transform!=null?r.Transform.TransformRevision:0).Append('|').Append(r.Vitals!=null?r.Vitals.VitalsRevision:0)
         .Append('|').Append(r.Inventory!=null?r.Inventory.ContentChecksum:"").Append('|').Append(r.Equipment!=null?EquipmentFingerprint(r.Equipment):"")
         .Append('|').Append(r.Respawn!=null?r.Respawn.RespawnRevision:0).Append('|').Append(r.Contract!=null?r.Contract.ContractRevision:0)
         .Append('|').Append(r.Work!=null?r.Work.WorkRevision:0).Append('|').Append(r.Mission!=null?r.Mission.MissionRevision:0)
         .Append('|').Append(r.Dog!=null?r.Dog.Revision:0)
         .Append('|').Append(r.BoundUndead==null?"":r.BoundUndead.Revision.ToString(CultureInfo.InvariantCulture)+":"+r.BoundUndead.ConditioningProgress.ToString("R",CultureInfo.InvariantCulture)+":"+(r.BoundUndead.IsBound?"1":"0")+":"+(r.BoundUndead.Lifecycle??string.Empty));
        if (r.HumanAppearance.HasValue)
            b.Append("|appearance:").Append(RebirthNpcAppearanceBinaryCodec.WriteAggregate(new XmlDocument(), r.HumanAppearance.Value).GetAttribute("payload"));
        if (r.NativeReconstruction != null)
            b.Append("|nativeReconstruction:").Append(r.NativeReconstruction.Write().ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
        string[] extensions = r.UnknownOptionalElements ?? new string[0];
        for (int i = 0; i < extensions.Length; i++)
            b.Append("|ext:").Append(extensions[i] ?? string.Empty);
        using(SHA256 h=SHA256.Create())return Hex(h.ComputeHash(Encoding.UTF8.GetBytes(b.ToString())));
    }
    internal static string HashText(string value){using(SHA256 h=SHA256.Create())return Hex(h.ComputeHash(Encoding.UTF8.GetBytes(value??"")));}
    private static string Hex(byte[] b){var s=new StringBuilder(b.Length*2);for(int i=0;i<b.Length;i++)s.Append(b[i].ToString("x2",CultureInfo.InvariantCulture));return s.ToString();}
}

public sealed class RebirthNpcLegacyFieldRule { public string LegacyField; public string TargetPath; public RebirthNpcLegacyFieldDisposition Disposition; public string Reason; }
public sealed class RebirthNpcLegacy26Record { public string LegacyTypeId; public string LegacyIdentity; public IDictionary<string,string> Values; public string SourceFile; public int SourceIndex; }

/// <summary>Comprehensive allow-listed REBIRTH 2.6 NPC import with idempotence, audit, rollback and quarantine.</summary>
public static class RebirthNpcLegacy26ImportService
{
    private static readonly object Sync=new object();
    private static readonly Dictionary<string,RebirthNpcLegacyFieldRule> Rules=new Dictionary<string,RebirthNpcLegacyFieldRule>(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] CandidateFiles={"RebirthNpcLegacy26.xml","RebirthNpcLegacyNPCs.xml","rebirth_npc_2_6.xml"};
    private static bool initialized,scanComplete,scanInProgress; private static long scanEpoch,nextScanUtcTicks; private static string scanDirectory=string.Empty,scanScope=string.Empty; private static long attempted,imported,duplicates,unsupported,discarded,hardFailures,quarantined,rollbacks;
    public static void EnsureInitialized(){lock(Sync){if(initialized)return;initialized=true;AddMappings();}}
    public static RebirthNpcLegacyFieldRule[] GetRules()
    {
        EnsureInitialized();
        lock (Sync)
        {
            var result = new RebirthNpcLegacyFieldRule[Rules.Count]; int index = 0;
            foreach (RebirthNpcLegacyFieldRule rule in Rules.Values)
                result[index++] = new RebirthNpcLegacyFieldRule { LegacyField = rule.LegacyField,
                    TargetPath = rule.TargetPath, Disposition = rule.Disposition, Reason = rule.Reason };
            Array.Sort(result, (x, y) => string.CompareOrdinal(x.LegacyField, y.LegacyField));
            return result;
        }
    }
    public static void TryImportSupportedFiles()
    {
        EnsureInitialized();
        RebirthNpcSaveScopeSnapshot scope = RebirthNpcSaveScope.ObserveCurrent();
        if (scope == null || string.IsNullOrEmpty(scope.SaveDirectory)) return;
        long epoch;
        lock (Sync)
        {
            if (!string.Equals(scanDirectory, scope.SaveDirectory, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(scanScope, scope.Fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                scanDirectory = scope.SaveDirectory; scanScope = scope.Fingerprint;
                scanComplete = false; scanInProgress = false; nextScanUtcTicks = 0; unchecked { scanEpoch++; }
            }
            if (scanComplete || scanInProgress || DateTime.UtcNow.Ticks < nextScanUtcTicks) return;
            scanInProgress = true; epoch = scanEpoch;
        }
        bool complete = false;
        try
        {
            if (!Directory.Exists(scope.SaveDirectory)) return;
            complete = true;
            for (int i = 0; i < CandidateFiles.Length; i++)
            {
                string path = Path.Combine(scope.SaveDirectory, CandidateFiles[i]);
                try { File.GetAttributes(path); }
                catch (FileNotFoundException) { continue; }
                if (!ImportFile(path, scope.SaveDirectory, scope.Fingerprint)) complete = false;
            }
        }
        catch (Exception ex)
        {
            complete = false;
            Log.Warning("[REBIRTH NPC Migration] scan remains retryable: " + ex.GetType().Name + ":" + ex.Message);
        }
        finally
        {
            lock (Sync)
            {
                if (scanEpoch == epoch)
                {
                    scanInProgress = false; scanComplete = complete;
                    nextScanUtcTicks = complete ? 0 : DateTime.UtcNow.AddSeconds(5).Ticks;
                }
            }
        }
    }

    public static bool ImportFile(string path)
    {
        RebirthNpcSaveScopeSnapshot scope = RebirthNpcSaveScope.ObserveCurrent();
        if (scope == null || string.IsNullOrEmpty(scope.SaveDirectory)) return false;
        return ImportFile(path, scope.SaveDirectory, scope.Fingerprint);
    }

    private static bool ImportFile(string path, string targetDirectory, string targetScope)
    {
        attempted++;
        try
        {
            // Preserve the input for inspection; import never edits or restores over it.
            string rollback = path + ".acip11.rollback.bak";
            if (!File.Exists(rollback)) File.Copy(path, rollback, false);
            XmlDocument document = new XmlDocument(); document.Load(path);
            XmlNodeList nodes = document.SelectNodes("//npc");
            var sources = new List<RebirthNpcLegacy26Record>();
            var staged = new List<RebirthNpcPersistentRecord>();
            int index = 0;
            // Whole-file preflight includes conversion and typed ingress, before any
            // aggregate publication. A row without a stable legacy ID is not row identity.
            if (nodes != null) foreach (XmlElement element in nodes)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (XmlAttribute attribute in element.Attributes) values[attribute.Name] = attribute.Value;
                foreach (XmlElement field in element.SelectNodes("field")) values[field.GetAttribute("name")] = field.GetAttribute("value");
                string identity = ResolveLegacyIdentity(values);
                var source = new RebirthNpcLegacy26Record { LegacyTypeId = Get(values, "type", "rebirth.npc.legacy"),
                    LegacyIdentity = identity, Values = values, SourceFile = path, SourceIndex = index++ };
                string error; RebirthNpcPersistentRecord record;
                if (!ValidateRecord(source, out error) || !TryBuildImportRecord(source, out record, out error))
                { Quarantine(path, element.OuterXml, "preflight-row-" + source.SourceIndex + ":" + error); quarantined++; return false; }
                sources.Add(source); staged.Add(record);
            }
            RebirthNpcStableId[] inserted; int duplicateRows; string commitError;
            if (!RebirthNpcAggregatePersistenceStore.TryCommitMigration(staged.ToArray(), targetDirectory, targetScope,
                out inserted, out duplicateRows, out commitError))
            { rollbacks++; Quarantine(path, string.Empty, commitError); quarantined++; return false; }
            imported += inserted.Length; duplicates += duplicateRows;
            try
            {
                var pendingAudit = new HashSet<RebirthNpcStableId>(inserted);
                for (int i = 0; i < staged.Count; i++)
                    if (pendingAudit.Remove(staged[i].Identity.StableNpcId))
                        AppendAudit(sources[i], staged[i].Identity.StableNpcId, staged[i].Audit.MigrationFingerprint, targetDirectory);
            }
            catch
            {
                // Audit is best effort after commit, never grounds to report rollback.
            }
            return true;
        }
        catch (Exception ex)
        {
            rollbacks++; Quarantine(path, string.Empty, ex.GetType().Name + ":" + ex.Message);
            return false;
        }
    }

    private static string ResolveLegacyIdentity(IDictionary<string, string> values)
    {
        string value;
        foreach (string key in new[] { "stableId", "id", "entityId" })
            if (values.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
        return string.Empty;
    }

    private static bool ValidateRecord(RebirthNpcLegacy26Record source,out string error)
    {
        error=string.Empty;
        if(source==null||source.Values==null){error="legacy-record-null";return false;}
        EnsureInitialized();
        foreach(var p in source.Values)
        {
            RebirthNpcLegacyFieldRule rule;
            if(!Rules.TryGetValue(p.Key,out rule)){error="unsupported-field:"+p.Key;return false;}
            if(rule.Disposition==RebirthNpcLegacyFieldDisposition.HardFailure){error="hard-failure-field:"+p.Key;return false;}
        }
        return true;
    }
    private static bool TryBuildImportRecord(RebirthNpcLegacy26Record source, out RebirthNpcPersistentRecord result, out string error)
    {
        result=null;error=string.Empty;if(source==null||source.Values==null){error="legacy-record-null";return false;}if(string.IsNullOrWhiteSpace(source.LegacyIdentity)){error="legacy-stable-identity-required";return false;}EnsureInitialized();var mapped=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(var p in source.Values){RebirthNpcLegacyFieldRule rule;if(!Rules.TryGetValue(p.Key,out rule)){unsupported++;error="unsupported-field:"+p.Key;return false;}if(rule.Disposition==RebirthNpcLegacyFieldDisposition.HardFailure){hardFailures++;error="hard-failure-field:"+p.Key;return false;}if(rule.Disposition==RebirthNpcLegacyFieldDisposition.IntentionalDiscard){discarded++;continue;}mapped[rule.TargetPath]=p.Value;}
        string fingerprint=RebirthNpcAggregatePersistenceStore.HashText((source.LegacyTypeId??"")+"|"+(source.LegacyIdentity??"")+"|"+Canonical(source.Values));RebirthNpcStableId id=DeterministicId(source.LegacyTypeId,source.LegacyIdentity);long now=DateTime.UtcNow.Ticks;var r=new RebirthNpcPersistentRecord{Identity=new RebirthNpcIdentityRecord{StableNpcId=id,Category=Get(mapped,"identity.category","Survivor"),Species=Get(mapped,"identity.species","human"),GeneratedOrAssignedDisplayName=Get(mapped,"identity.displayName","Migrated Survivor"),NameRevision=1,CreationWorldTime=ParseLong(Get(mapped,"identity.creationWorldTime","0"),now),CreationReason=RebirthNpcCreationReason.Migration,OriginSpawnGroup=Get(mapped,"identity.originSpawnGroup",""),OriginPrefab=Get(mapped,"identity.originPrefab",""),OriginScenario=Get(mapped,"identity.originScenario","")},Profile=new RebirthNpcProfileBindingRecord{ProfileId=Get(mapped,"profile.profileId",MapProfile(Get(mapped,"identity.category","Survivor"))),ProfileSchemaVersionAtBind=RebirthNpcRuntimeState.CurrentSchemaVersion,ProfileCompatibilityClass="rebirth-2.6",LastSuccessfulResolutionRevision=1},Lifecycle=new RebirthNpcLifecycleRecord{LifecycleDomain="persistent",LifecycleRevision=1,PersistentSinceWorldTime=now,DismissalState=Get(mapped,"lifecycle.dismissalState","active")},Presence=new RebirthNpcPresenceRecord{PresenceState=Get(mapped,"presence.state","Unloaded"),PresenceRevision=1,TransitionReason="migration",TransitionStartedWorldTime=now,EmbodimentGeneration=1,LastKnownChunkKey=Get(mapped,"presence.chunkKey","")},Transform=new RebirthNpcTransformRecord{WorldPosition=ParseVector(Get(mapped,"transform.position","0,0,0")),RotationYaw=ParseFloat(Get(mapped,"transform.yaw","0")),ChunkKey=Get(mapped,"transform.chunkKey",""),TransformRevision=1},Vitals=new RebirthNpcVitalStateRecord{CurrentHealth=ParseInt(Get(mapped,"vitals.health","100"),100),MaximumHealthAtSave=ParseInt(Get(mapped,"vitals.maxHealth","100"),100),DeathOrIncapacitationState=Get(mapped,"vitals.state","alive"),ActivePersistentBuffSet=Split(Get(mapped,"vitals.persistentBuffs","")),VitalsRevision=1},ControllerFragments=new RebirthNpcControllerFragmentSet{Fragments=new RebirthNpcControllerFragment[0]},Audit=new RebirthNpcAuditRecord{CreatedByBuild="REBIRTH-3.0-ACIP-11",LastWrittenByBuild="REBIRTH-3.0-ACIP-11",LastMutationKind="legacy-2.6-import",LastMutationWorldTime=now,MigrationSource="REBIRTH-2.6",MigrationFingerprint=fingerprint,RecoveryFlags=new string[0]},DomainManifest=new RebirthNpcDomainRevisionManifest[0],AggregateRevision=1};
        string owner=Get(mapped,"ownership.playerPersistentId","");if(!string.IsNullOrEmpty(owner))r.Ownership=new RebirthNpcOwnershipRecord{OwnershipState=Get(mapped,"ownership.state","Owned"),OwnerPlatformIdOrPersistentPlayerId=owner,OwnerCharacterIdIfRequired=Get(mapped,"ownership.characterId",""),OwnerRevision=1,HiredWorldTime=ParseLong(Get(mapped,"ownership.hiredWorldTime","0"),0),PermissionPolicyId=Get(mapped,"ownership.permissionPolicyId","rebirth.default"),PartyAccessMode=Get(mapped,"ownership.partyAccessMode","owner-and-party")};string order=Get(mapped,"order.state","");if(!string.IsNullOrEmpty(order))r.Order=new RebirthNpcOrderRecord{OrderState=order,OrderTarget=Get(mapped,"order.target",""),PreviousOrderState=Get(mapped,"order.previous","") ,OrderRevision=1};
        r.Inventory=BuildInventory(mapped);r.Equipment=BuildEquipment(mapped);r.Work=BuildWork(mapped);r.Respawn=BuildRespawn(mapped);r.Contract=BuildContract(mapped);r.Mission=BuildMission(mapped);
        result = r;
        return true;
    }
    public static bool TryImportRecord(RebirthNpcLegacy26Record source, out string error)
    {
        RebirthNpcPersistentRecord record;
        if (!TryBuildImportRecord(source, out record, out error)) return false;
        RebirthNpcSaveScopeSnapshot scope = RebirthNpcSaveScope.ObserveCurrent();
        RebirthNpcStableId[] inserted; int duplicateRows;
        if (scope == null || string.IsNullOrEmpty(scope.SaveDirectory)) { error = "current-save-unavailable"; return false; }
        if (!RebirthNpcAggregatePersistenceStore.TryCommitMigration(new[] { record }, scope.SaveDirectory,
            scope.Fingerprint, out inserted, out duplicateRows, out error)) return false;
        imported += inserted.Length; duplicates += duplicateRows;
        if (inserted.Length != 0) AppendAudit(source, record.Identity.StableNpcId, record.Audit.MigrationFingerprint, scope.SaveDirectory);
        return true;
    }
    public static string GetReport(){return "[REBIRTH NPC ACIP-11 Migration] rules="+GetRules().Length+" attempted="+attempted+" imported="+imported+" duplicates="+duplicates+" unsupported="+unsupported+" discarded="+discarded+" hardFailures="+hardFailures+" quarantined="+quarantined+" rollbacks="+rollbacks;}
    public static RebirthNpcAggregateQualificationSnapshot Qualify(){string d;bool consistent=RebirthNpcAggregatePersistenceStore.ValidateAtomicConsistency(out d);return new RebirthNpcAggregateQualificationSnapshot{IsValid=consistent&&Rules.Count>=45,Records=RebirthNpcAggregatePersistenceStore.Snapshot().Length,ManifestDomains=RebirthNpcPersistenceCoordinator.CaptureFileSnapshots().Length,ConsistencyFailures=consistent?0:1,MigrationMappings=Rules.Count,UnsupportedFields=(int)unsupported,Quarantined=(int)quarantined,DuplicateImports=(int)duplicates,Detail=d};}
    public static void ResetForWorldChange(){lock(Sync){scanComplete=false;scanInProgress=false;nextScanUtcTicks=0;scanDirectory=string.Empty;scanScope=string.Empty;unchecked{scanEpoch++;}}}
    private static void AddMappings(){Map("stableId","identity.legacyStableId");Map("id","identity.legacyId");Map("entityId","identity.legacyEntityId");Map("type","identity.legacyType");Map("category","identity.category");Map("species","identity.species");Map("entityName","identity.displayName");Map("displayName","identity.displayName");Map("creationWorldTime","identity.creationWorldTime");Map("spawnGroup","identity.originSpawnGroup");Map("originPrefab","identity.originPrefab");Map("scenario","identity.originScenario");Map("profile","profile.profileId");Map("profileId","profile.profileId");Map("ownerId","ownership.playerPersistentId");Map("ownerPlatformId","ownership.playerPersistentId");Map("ownerCharacterId","ownership.characterId");Map("ownershipState","ownership.state");Map("hiredWorldTime","ownership.hiredWorldTime");Map("permissionPolicy","ownership.permissionPolicyId");Map("partyAccess","ownership.partyAccessMode");Map("currentOrder","order.state");Map("orderTarget","order.target");Map("previousOrder","order.previous");Map("presence","presence.state");Map("chunkKey","presence.chunkKey");Map("position","transform.position");Map("worldPosition","transform.position");Map("rotationYaw","transform.yaw");Map("health","vitals.health");Map("maxHealth","vitals.maxHealth");Map("lifeState","vitals.state");Map("persistentBuffs","vitals.persistentBuffs");Map("inventory","inventory.serialized");Map("inventorySlots","inventory.slots");Map("inventoryOverflow","inventory.overflow");Map("inventoryRevision","inventory.revision");Map("equipment","equipment.serialized");Map("heldSlot","equipment.heldSlot");Map("ammoState","equipment.ammoState");Map("profession","profession.definitionId");Map("professionXp","progression.professionXp");Map("attributeXp","progression.attributeXp");Map("weaponXp","progression.weaponXp");Map("certifications","progression.certifications");Map("specializations","progression.specializations");Map("socialReputation","social.reputation");Map("relationshipMemory","social.memory");Map("faction","organization.factionId");Map("settlementId","organization.settlementId");Map("workState","work.state");Map("workType","work.type");Map("workTarget","work.target");Map("workProgress","work.progress");Map("respawnState","respawn.state");Map("respawnTime","respawn.eligibleTime");Map("contractState","contract.state");Map("contractExpiry","contract.expiry");Map("wage","contract.wage");Map("missionState","mission.state");Map("missionId","mission.id");Discard("transientEntityId","Transient entity IDs are not durable identity.");Discard("pathNode","Transient pathfinding state is intentionally discarded.");Discard("animationTime","Transient presentation time is intentionally discarded.");Hard("unknownRequiredFragment","Required unknown controller fragments block migration.");}
    private static void Map(string f,string t){Rules[f]=new RebirthNpcLegacyFieldRule{LegacyField=f,TargetPath=t,Disposition=RebirthNpcLegacyFieldDisposition.Map};}private static void Discard(string f,string reason){Rules[f]=new RebirthNpcLegacyFieldRule{LegacyField=f,Disposition=RebirthNpcLegacyFieldDisposition.IntentionalDiscard,Reason=reason};}private static void Hard(string f,string reason){Rules[f]=new RebirthNpcLegacyFieldRule{LegacyField=f,Disposition=RebirthNpcLegacyFieldDisposition.HardFailure,Reason=reason};}
    private static RebirthNpcStableId DeterministicId(string legacyType,string legacyIdentity){byte[] b;using(SHA256 h=SHA256.Create())b=h.ComputeHash(Encoding.UTF8.GetBytes("REBIRTH-2.6|"+(legacyType??"").Trim().ToLowerInvariant()+"|"+(legacyIdentity??"").Trim().ToLowerInvariant()));ulong hi=BitConverter.ToUInt64(b,0),lo=BitConverter.ToUInt64(b,8);if(hi==0&&lo==0)lo=1;return new RebirthNpcStableId(hi,lo);}
    private static string Canonical(IDictionary<string,string> v){var k=new List<string>(v.Keys);k.Sort(StringComparer.OrdinalIgnoreCase);var b=new StringBuilder();for(int i=0;i<k.Count;i++)b.Append(k[i]).Append('=').Append(v[k[i]]).Append(';');return b.ToString();}
    private static RebirthNpcInventoryRecord BuildInventory(IDictionary<string,string> m){string s=Get(m,"inventory.serialized","");if(string.IsNullOrEmpty(s))return null;var slots=new List<RebirthNpcInventorySlotRecord>();string[] p=s.Split('|');for(int i=0;i<p.Length;i++)if(!string.IsNullOrEmpty(p[i]))slots.Add(new RebirthNpcInventorySlotRecord{SlotIndex=i,ItemValueSerialization=p[i],ItemCount=1});return new RebirthNpcInventoryRecord{InventorySchemaVersion=1,SlotCount=slots.Count,SlotRecords=slots.ToArray(),OverflowRecords=new RebirthNpcInventorySlotRecord[0],InventoryRevision=(uint)ParseInt(Get(m,"inventory.revision","1"),1),ContentChecksum=RebirthNpcAggregatePersistenceStore.HashText(s)};}
    private static RebirthNpcEquipmentRecord BuildEquipment(IDictionary<string,string> m){string s=Get(m,"equipment.serialized","");if(string.IsNullOrEmpty(s))return null;var slots=new List<RebirthNpcEquipmentSlotRecord>();
            string[] p=s.Split('|');for(int i=0;i<p.Length;i++)if(!string.IsNullOrEmpty(p[i]))slots.Add(new RebirthNpcEquipmentSlotRecord{SlotId="legacy-"+i,ItemValueSerialization=p[i],ItemCount=1});return new RebirthNpcEquipmentRecord{EquipmentSchemaVersion=1,EquipmentSlots=slots.ToArray(),ActiveHeldSlot=Get(m,"equipment.heldSlot",""),ActiveAmmoStateIfNotNative=Get(m,"equipment.ammoState",""),EquipmentRevision=1};}
    private static RebirthNpcWorkRecord BuildWork(IDictionary<string,string> m){string s=Get(m,"work.state","");return string.IsNullOrEmpty(s)?null:new RebirthNpcWorkRecord{WorkState=s,WorkType=Get(m,"work.type",""),TargetReference=Get(m,"work.target",""),ProgressCheckpoint=Get(m,"work.progress",""),ReservationIds=new string[0],WorkRevision=1};}
    private static RebirthNpcRespawnRecord BuildRespawn(IDictionary<string,string> m){string s=Get(m,"respawn.state","");return string.IsNullOrEmpty(s)?null:new RebirthNpcRespawnRecord{RespawnState=s,EligibleRespawnWorldTime=ParseLong(Get(m,"respawn.eligibleTime","0"),0),RespawnPolicyId="legacy-category-policy",RetainedInventoryPolicy="preserve",RespawnRevision=1};}
    private static RebirthNpcContractRecord BuildContract(IDictionary<string,string> m){string s=Get(m,"contract.state","");return string.IsNullOrEmpty(s)?null:new RebirthNpcContractRecord{ContractState=s,ExpiresWorldTime=ParseLong(Get(m,"contract.expiry","0"),0),WageAmount=ParseInt(Get(m,"contract.wage","0"),0),ContractRevision=1};}
    private static RebirthNpcMissionRecord BuildMission(IDictionary<string,string> m){string s=Get(m,"mission.state","");return string.IsNullOrEmpty(s)?null:new RebirthNpcMissionRecord{MissionState=s,MissionId=Get(m,"mission.id",""),MissionRevision=1};}
    private static string MapProfile(string c){if(c.IndexOf("bandit",StringComparison.OrdinalIgnoreCase)>=0)return "bandit.standard";if(c.IndexOf("dog",StringComparison.OrdinalIgnoreCase)>=0)return "companion.dog";if(c.IndexOf("panther",StringComparison.OrdinalIgnoreCase)>=0)return "companion.panther";return "survivor.persistent";}
    private static void AppendAudit(RebirthNpcLegacy26Record s,RebirthNpcStableId id,string fp,string targetDirectory=null){try{string dir=targetDirectory??GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(dir))return;File.AppendAllText(Path.Combine(dir,"RebirthNpcLegacyMigration.audit.log"),DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)+" source="+Path.GetFileName(s.SourceFile)+" index="+s.SourceIndex+" stableId="+id+" fingerprint="+fp+Environment.NewLine,new UTF8Encoding(false));}catch{}}
    private static void Quarantine(string source,string payload,string reason){try{string dir=Path.Combine(Path.GetDirectoryName(source),"RebirthNpcMigrationQuarantine");Directory.CreateDirectory(dir);string p=Path.Combine(dir,"record-"+DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)+".txt");File.WriteAllText(p,"reason="+reason+Environment.NewLine+payload,new UTF8Encoding(false));}catch{}}
    private static string Get(IDictionary<string,string> d,string k,string f){string v;return d!=null&&d.TryGetValue(k,out v)?v:f;}private static long ParseLong(string s,long f){long v;return long.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:f;}private static int ParseInt(string s,int f){int v;return int.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:f;}private static float ParseFloat(string s){float v;return float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:0;}private static Vector3 ParseVector(string s){string[] p=(s??"").Split(',');return p.Length>=3?new Vector3(ParseFloat(p[0]),ParseFloat(p[1]),ParseFloat(p[2])):Vector3.zero;}private static string[] Split(string s){return string.IsNullOrEmpty(s)?new string[0]:s.Split(',');}
}
