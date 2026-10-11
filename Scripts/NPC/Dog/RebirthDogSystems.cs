using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

#nullable disable

public enum RebirthDogLifecycleKind : byte
{
    Active = 0,
    PickedUp = 1,
    AwaitingRespawn = 2,
    // Terminal administrative removal. Kept distinct from AwaitingRespawn so a
    // killall-purged dog can never be offered as Report for Duty or consume a slot.
    Removed = 3
}

public static class RebirthDogTrainingCommandIds
{
    public const string Follow = "dog.follow";
    public const string Stay = "dog.stay";
    public const string AttackToggle = "dog.attack_toggle";
    public const string OwnerPositionStay = "dog.owner_position_stay";
    public const string GuardArea = "dog.guard_area";
    public const string Hunting = "dog.hunting";

    public static void EnsureBasic(HashSet<string> learned)
    {
        if (learned == null) return;
        learned.Add(Follow); learned.Add(Stay); learned.Add(AttackToggle);
    }

    public static void EnsureLegacyFull(HashSet<string> learned)
    {
        if (learned == null) return;
        EnsureBasic(learned);
        learned.Add(OwnerPositionStay); learned.Add(GuardArea); learned.Add(Hunting);
    }
}

/// <summary>
/// Dog-specific component stored inside the canonical NPC aggregate.  This is
/// deliberately small: common ownership/order/identity/vitals live in the
/// aggregate, while dog-only behavior/progression/lifecycle data lives here.
/// </summary>
public sealed class RebirthDogPersistentRecord
{
    public const int CurrentSchemaVersion = 5;

    public int SchemaVersion = CurrentSchemaVersion;
    public string BreedId = string.Empty;
    public bool IsTamedWild;
    public string SourceEntityClass = string.Empty;
    public string TamedEntityClass = string.Empty;
    public string SpeciesCategory = string.Empty;
    public int AnimalCapacityCost;
    public long TamedUtcTicks;
    public long LastCareUtcTicks;
    public RebirthCompanionBehaviorMode CombatMode = RebirthCompanionBehaviorMode.FullControl;
    public bool AttackStopped;
    public int Level = 1;
    public int MiningLevel;
    public int KillCount;
    public float Training;
    public float Bond;
    public bool LegacyCommandGrandfathered;
    public readonly HashSet<string> LearnedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool OfflineParked;
    public Vector3? OfflineParkPosition;
    public RebirthDogLifecycleKind Lifecycle = RebirthDogLifecycleKind.Active;
    public string PickupNonce = string.Empty;
    public uint PickupRevision;
    public bool PickupNonceConsumed;
    // RP23 write-ahead recovery marker. A pickup intent is persisted before the
    // capsule/in-world lifecycle transaction begins and remains until every projection
    // reaches one coherent terminal state.
    public bool PickupRecoveryPending;
    public string PickupRecoveryNonce = string.Empty;
    public string PickupRecoveryStage = string.Empty;
    public long PickupRecoveryUtcTicks;
    public Vector3? StayPosition;
    // v240 used Guard internally for the second 2.6 Stay command (owner-position hold).
    // Persist the distinction so that command remains strictly stationary while a true
    // Guard Area order can use bounded combat movement. Missing v1 data defaults true.
    public bool GuardIsStationaryStay = true;
    public Vector3? RespawnFallback;
    public uint Revision;

    public RebirthDogPersistentRecord()
    {
        RebirthDogTrainingCommandIds.EnsureBasic(LearnedCommands);
    }

    public static XmlElement WriteXml(XmlDocument document, RebirthDogPersistentRecord value)
    {
        XmlElement e = document.CreateElement("dog");
        value = value ?? new RebirthDogPersistentRecord();
        e.SetAttribute("schema", value.SchemaVersion.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("breed", value.BreedId ?? string.Empty);
        e.SetAttribute("tamedWild", value.IsTamedWild ? "1" : "0");
        e.SetAttribute("sourceEntityClass", value.SourceEntityClass ?? string.Empty);
        e.SetAttribute("tamedEntityClass", value.TamedEntityClass ?? string.Empty);
        e.SetAttribute("speciesCategory", value.SpeciesCategory ?? string.Empty);
        e.SetAttribute("animalCapacityCost", Math.Max(0,value.AnimalCapacityCost).ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("tamedUtcTicks", Math.Max(0L,value.TamedUtcTicks).ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("lastCareUtcTicks", Math.Max(0L,value.LastCareUtcTicks).ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("combatMode", value.CombatMode.ToString());
        e.SetAttribute("attackStopped", value.AttackStopped ? "1" : "0");
        e.SetAttribute("level", value.Level.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("miningLevel", value.MiningLevel.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("kills", value.KillCount.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("training", Mathf.Clamp(value.Training, 0f, 100f).ToString("R", CultureInfo.InvariantCulture));
        e.SetAttribute("bond", Mathf.Clamp(value.Bond, 0f, 100f).ToString("R", CultureInfo.InvariantCulture));
        e.SetAttribute("legacyCommandGrandfathered", value.LegacyCommandGrandfathered ? "1" : "0");
        RebirthDogTrainingCommandIds.EnsureBasic(value.LearnedCommands);
        List<string> learned = new List<string>(value.LearnedCommands);
        learned.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < learned.Count; i++)
        {
            XmlElement learnedCommand = document.CreateElement("learnedCommand");
            learnedCommand.SetAttribute("id", learned[i]);
            e.AppendChild(learnedCommand);
        }
        e.SetAttribute("offlineParked", value.OfflineParked ? "1" : "0");
        if (value.OfflineParkPosition.HasValue) e.SetAttribute("offlineParkPos", FormatVector(value.OfflineParkPosition.Value));
        e.SetAttribute("lifecycle", value.Lifecycle.ToString());
        e.SetAttribute("pickupNonce", value.PickupNonce ?? string.Empty);
        e.SetAttribute("pickupRevision", value.PickupRevision.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("pickupConsumed", value.PickupNonceConsumed ? "1" : "0");
        e.SetAttribute("pickupRecoveryPending", value.PickupRecoveryPending ? "1" : "0");
        e.SetAttribute("pickupRecoveryNonce", value.PickupRecoveryNonce ?? string.Empty);
        e.SetAttribute("pickupRecoveryStage", value.PickupRecoveryStage ?? string.Empty);
        e.SetAttribute("pickupRecoveryUtcTicks", Math.Max(0L, value.PickupRecoveryUtcTicks).ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("revision", value.Revision.ToString(CultureInfo.InvariantCulture));
        if (value.StayPosition.HasValue) e.SetAttribute("stay", FormatVector(value.StayPosition.Value));
        e.SetAttribute("guardStationaryStay", value.GuardIsStationaryStay ? "1" : "0");
        if (value.RespawnFallback.HasValue) e.SetAttribute("respawnFallback", FormatVector(value.RespawnFallback.Value));
        return e;
    }

    public static RebirthDogPersistentRecord ReadXml(XmlElement e)
    {
        RebirthDogPersistentRecord r = new RebirthDogPersistentRecord();
        if (e == null) return r;
        int schema = 1, level, miningLevel, kills;
        uint pickupRevision, revision;
        int.TryParse(e.GetAttribute("schema"), out schema);
        int sourceSchema = Math.Max(1, schema);
        r.SchemaVersion = CurrentSchemaVersion;
        r.BreedId = e.GetAttribute("breed") ?? string.Empty;
        if(sourceSchema>=4)
        {
            r.IsTamedWild=e.GetAttribute("tamedWild")=="1";r.SourceEntityClass=e.GetAttribute("sourceEntityClass")??string.Empty;r.TamedEntityClass=e.GetAttribute("tamedEntityClass")??string.Empty;r.SpeciesCategory=e.GetAttribute("speciesCategory")??string.Empty;
            int capacity;long ticks;if(int.TryParse(e.GetAttribute("animalCapacityCost"),NumberStyles.Integer,CultureInfo.InvariantCulture,out capacity))r.AnimalCapacityCost=Math.Max(0,capacity);
            if(long.TryParse(e.GetAttribute("tamedUtcTicks"),NumberStyles.Integer,CultureInfo.InvariantCulture,out ticks))r.TamedUtcTicks=Math.Max(0L,ticks);
            if(long.TryParse(e.GetAttribute("lastCareUtcTicks"),NumberStyles.Integer,CultureInfo.InvariantCulture,out ticks))r.LastCareUtcTicks=Math.Max(0L,ticks);
        }
        RebirthCompanionBehaviorMode mode;
        if (Enum.TryParse(e.GetAttribute("combatMode"), true, out mode)) r.CombatMode = mode;
        r.AttackStopped = e.GetAttribute("attackStopped") == "1";
        if (int.TryParse(e.GetAttribute("level"), out level)) r.Level = Mathf.Clamp(level, 0, 10);
        if (int.TryParse(e.GetAttribute("miningLevel"), out miningLevel)) r.MiningLevel = Math.Max(0, miningLevel);
        if (int.TryParse(e.GetAttribute("kills"), out kills)) r.KillCount = Math.Max(0, kills);
        float training, bond;
        if (float.TryParse(e.GetAttribute("training"), NumberStyles.Float, CultureInfo.InvariantCulture, out training)) r.Training = Mathf.Clamp(training, 0f, 100f);
        if (float.TryParse(e.GetAttribute("bond"), NumberStyles.Float, CultureInfo.InvariantCulture, out bond)) r.Bond = Mathf.Clamp(bond, 0f, 100f);
        if (sourceSchema >= 3) r.LegacyCommandGrandfathered = e.GetAttribute("legacyCommandGrandfathered") == "1";
        if (sourceSchema >= 3)
        {
            r.LearnedCommands.Clear();
            foreach (XmlNode child in e.ChildNodes)
            {
                XmlElement learned = child as XmlElement;
                if (learned == null || !string.Equals(learned.Name, "learnedCommand", StringComparison.OrdinalIgnoreCase)) continue;
                string id = (learned.GetAttribute("id") ?? string.Empty).Trim();
                if (id.Length > 0) r.LearnedCommands.Add(id);
            }
            RebirthDogTrainingCommandIds.EnsureBasic(r.LearnedCommands);
        }
        else
        {
            // Existing owned dogs retain the complete pre-Chunk-B command surface.
            r.Training = 50f;
            r.Bond = 50f;
            r.LegacyCommandGrandfathered = true;
            RebirthDogTrainingCommandIds.EnsureLegacyFull(r.LearnedCommands);
        }
        r.OfflineParked = e.GetAttribute("offlineParked") == "1";
        Vector3 offlineV;
        if (TryParseVector(e.GetAttribute("offlineParkPos"), out offlineV)) r.OfflineParkPosition = offlineV;
        RebirthDogLifecycleKind lifecycle;
        if (Enum.TryParse(e.GetAttribute("lifecycle"), true, out lifecycle)) r.Lifecycle = lifecycle;
        r.PickupNonce = e.GetAttribute("pickupNonce") ?? string.Empty;
        if (uint.TryParse(e.GetAttribute("pickupRevision"), out pickupRevision)) r.PickupRevision = pickupRevision;
        r.PickupNonceConsumed = e.GetAttribute("pickupConsumed") == "1";
        if (sourceSchema >= 5)
        {
            r.PickupRecoveryPending = e.GetAttribute("pickupRecoveryPending") == "1";
            r.PickupRecoveryNonce = e.GetAttribute("pickupRecoveryNonce") ?? string.Empty;
            r.PickupRecoveryStage = e.GetAttribute("pickupRecoveryStage") ?? string.Empty;
            long recoveryTicks;
            if (long.TryParse(e.GetAttribute("pickupRecoveryUtcTicks"), NumberStyles.Integer, CultureInfo.InvariantCulture, out recoveryTicks))
                r.PickupRecoveryUtcTicks = Math.Max(0L, recoveryTicks);
        }
        if (uint.TryParse(e.GetAttribute("revision"), out revision)) r.Revision = revision;
        Vector3 v;
        if (TryParseVector(e.GetAttribute("stay"), out v)) r.StayPosition = v;
        // Schema v1 Guard records were all produced by "Stay Where I'm Standing".
        // Preserve that behavior when old saves are upgraded.
        r.GuardIsStationaryStay = !e.HasAttribute("guardStationaryStay") || e.GetAttribute("guardStationaryStay") == "1";
        if (TryParseVector(e.GetAttribute("respawnFallback"), out v)) r.RespawnFallback = v;
        return r;
    }

    private static string FormatVector(Vector3 v)
    {
        return v.x.ToString("R", CultureInfo.InvariantCulture) + "," +
               v.y.ToString("R", CultureInfo.InvariantCulture) + "," +
               v.z.ToString("R", CultureInfo.InvariantCulture);
    }

    private static bool TryParseVector(string text, out Vector3 v)
    {
        v = Vector3.zero;
        string[] p = (text ?? string.Empty).Split(',');
        float x, y, z;
        if (p.Length != 3 ||
            !float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
            !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
            !float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;
        v = new Vector3(x, y, z);
        return true;
    }
}

public sealed class RebirthDogInventorySnapshot
{
    public readonly RebirthNpcStableId StableId;
    public readonly ItemStack[] Slots;
    public readonly PackedBoolArray Locks;
    public readonly uint Revision;

    public RebirthDogInventorySnapshot(RebirthNpcStableId id, ItemStack[] slots, PackedBoolArray locks, uint revision)
    {
        StableId = id;
        Slots = slots ?? new ItemStack[0];
        Locks = locks ?? new PackedBoolArray(Slots.Length);
        Revision = revision;
    }
}

/// <summary>Persistent dog state facade over RebirthNpcAggregatePersistenceStore.</summary>
public static class RebirthDogStateService
{
    public const int InventorySlots = 10;

    public static bool IsDog(RebirthNpcPersistentRecordView record)
    {
        return record != null && record.Profile != null &&
            string.Equals(record.Profile.ProfileId, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase);
    }

    // Compatibility for store-owned mutation callbacks and cold detached DTO callers.
    public static bool IsDog(RebirthNpcPersistentRecord record)
    {
        return record != null && record.Profile != null &&
            string.Equals(record.Profile.ProfileId, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTamedWild(RebirthNpcPersistentRecord record)
    { return IsDog(record) && record.Dog != null && record.Dog.IsTamedWild; }

    /// <summary>Legacy cold API. Both returned DTOs belong to the caller, not the store.</summary>
    public static bool TryGet(RebirthNpcStableId id, out RebirthNpcPersistentRecord record, out RebirthDogPersistentRecord dog)
    {
        dog = null;
        if (!RebirthNpcAggregatePersistenceStore.TryGet(id, out record) || !IsDog(record)) return false;
        dog = record.Dog;
        return dog != null;
    }

    /// <summary>Legacy initializer returning a detached copy. Prefer EnsureView for reads.</summary>
    public static RebirthDogPersistentRecord Ensure(EntityRebirthDogCompanion entity)
    {
        if (EnsureView(entity) == null) return null;
        RebirthNpcPersistentRecord record; RebirthDogPersistentRecord dog;
        return TryGet(entity.RebirthRuntimeState.StableId, out record, out dog) ? dog : null;
    }

    public static bool IsTamedWild(RebirthNpcPersistentRecordView record)
    { return IsDog(record) && record.Dog != null && record.Dog.IsTamedWild; }

    public static bool IsTamedWild(RebirthNpcStableId id)
    { RebirthNpcPersistentRecordView r;RebirthDogPersistentRecordView d;return TryGetView(id,out r,out d)&&d!=null&&d.IsTamedWild; }

    public static bool TryGetView(RebirthNpcStableId id, out RebirthNpcPersistentRecordView record, out RebirthDogPersistentRecordView dog)
    {
        dog = null;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(id, out record) || !IsDog(record)) return false;
        dog = record.Dog;
        return dog != null;
    }

    public static RebirthDogPersistentRecordView EnsureView(EntityRebirthDogCompanion entity)
    {
        if (entity == null || entity.RebirthRuntimeState == null) return null;
        RebirthNpcStableId id = entity.RebirthRuntimeState.StableId;
        // Only the physical current-save dog may promote its quarantined legacy row.
        RebirthNpcPersistentRecord promoted;
        RebirthNpcAggregatePersistenceStore.TryPromoteQuarantinedDogRecord(id, out promoted);
        string breedId = ResolveBreedId(entity);
        if (!RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord record)
        {
            EnsureCommonRecord(record, breedId, entity);
        })) return null;
        RebirthNpcPersistentRecordView view; RebirthDogPersistentRecordView dog;
        return TryGetView(id, out view, out dog) ? dog : null;
    }

    public static void EnsureCommonRecord(RebirthNpcPersistentRecord record, string breedId, EntityRebirthDogCompanion entity)
    {
        if (record == null) return;
        if (record.Identity == null) record.Identity = new RebirthNpcIdentityRecord();
        record.Identity.Category = RebirthNpcCategory.DogCompanion.ToString();
        bool tamedWild = record.Dog != null && record.Dog.IsTamedWild;
        record.Identity.Species = tamedWild && !string.IsNullOrWhiteSpace(record.Dog.SpeciesCategory) ? record.Dog.SpeciesCategory : "dog";
        if (record.Profile == null) record.Profile = new RebirthNpcProfileBindingRecord();
        record.Profile.ProfileId = RebirthDogDefinitions.ProfileId;
        if (record.Presence == null) record.Presence = new RebirthNpcPresenceRecord();
        if (record.Transform == null) record.Transform = new RebirthNpcTransformRecord();
        if (record.Vitals == null) record.Vitals = new RebirthNpcVitalStateRecord { ActivePersistentBuffSet = new string[0] };
        if (record.Dog == null) record.Dog = new RebirthDogPersistentRecord();
        if (!record.Dog.IsTamedWild && !string.IsNullOrWhiteSpace(breedId)) record.Dog.BreedId = breedId;
        if (record.Dog.Level < 1) record.Dog.Level = 1;
        EnsureInventoryRecord(record);
        if (entity != null)
        {
            // Never let a transient/broken live projection overwrite the last durable safe
            // location. v281-v283 proved that an entity can briefly retain a logical position
            // after it has fallen out of native chunk ownership (or fallen to world-bottom).
            // In that state the roster/marker would otherwise persist a synthetic location
            // after the physical dog disappears.
            if (IsPersistableLiveProjection(entity))
            {
                record.Transform.WorldPosition = entity.position;
                record.Transform.LastSafePosition = entity.position;
                record.Transform.LastSafePositionWorldTime = DateTime.UtcNow.Ticks;
                string physicalChunkKey = entity.chunkPosAddedEntityTo.x.ToString(CultureInfo.InvariantCulture) + "," +
                    entity.chunkPosAddedEntityTo.z.ToString(CultureInfo.InvariantCulture);
                record.Transform.ChunkKey = physicalChunkKey;
                record.Presence.LastKnownChunkKey = physicalChunkKey;
            }
            record.Vitals.CurrentHealth = Math.Max(0, entity.Health);
            record.Vitals.MaximumHealthAtSave = Math.Max(1, entity.GetMaxHealth());
            record.Vitals.DeathOrIncapacitationState = entity.IsDead() ? "dead" : "alive";
        }
    }

    /// <summary>
    /// True only when the live dog is represented by the same physical entity in World.Entities
    /// and in the native chunk that owns its current position. This is intentionally stricter
    /// than checking Entity.position alone; persistence must never advertise a ghost projection.
    /// </summary>
    public static bool IsPersistableLiveProjection(EntityRebirthDogCompanion entity)
    {
        if (entity == null || entity.world == null || entity.IsDead() || entity.IsMarkedForUnload())
            return false;
        if (entity.position.y <= 1f || float.IsNaN(entity.position.x) || float.IsNaN(entity.position.y) ||
            float.IsNaN(entity.position.z) || float.IsInfinity(entity.position.x) ||
            float.IsInfinity(entity.position.y) || float.IsInfinity(entity.position.z))
            return false;

        World world = entity.world;
        Entity worldEntity = world.GetEntity(entity.entityId);
        if (!ReferenceEquals(worldEntity, entity))
            return false;
        if (!entity.addedToChunk)
            return false;

        int positionChunkX = World.toChunkXZ(Utils.Fastfloor(entity.position.x));
        int positionChunkZ = World.toChunkXZ(Utils.Fastfloor(entity.position.z));
        if (entity.chunkPosAddedEntityTo.x != positionChunkX || entity.chunkPosAddedEntityTo.z != positionChunkZ)
            return false;

        Chunk chunk = world.GetChunkSync(positionChunkX, positionChunkZ) as Chunk;
        if (chunk == null || chunk.InProgressUnloading || chunk.entityLists == null)
            return false;

        for (int layer = 0; layer < chunk.entityLists.Length; layer++)
        {
            List<Entity> list = chunk.entityLists[layer];
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                Entity candidate = list[i];
                if (ReferenceEquals(candidate, entity) || (candidate != null && candidate.entityId == entity.entityId))
                    return true;
            }
        }
        return false;
    }

    public static void EnsureInventoryRecord(RebirthNpcPersistentRecord record)
    {
        if (record.Inventory == null)
            record.Inventory = new RebirthNpcInventoryRecord { InventorySchemaVersion = 1, SlotCount = InventorySlots, InventoryRevision = 1 };
        record.Inventory.InventorySchemaVersion = Math.Max(1, record.Inventory.InventorySchemaVersion);
        record.Inventory.SlotCount = InventorySlots;
        RebirthNpcInventorySlotRecord[] existing = record.Inventory.SlotRecords ?? new RebirthNpcInventorySlotRecord[0];
        bool canonical = existing.Length == InventorySlots && record.Inventory.OverflowRecords != null;
        if (canonical)
        {
            for (int i = 0; i < existing.Length; i++)
                if (existing[i] == null || existing[i].SlotIndex != i) { canonical = false; break; }
            if (canonical) return;
        }
        RebirthNpcInventorySlotRecord[] slots = new RebirthNpcInventorySlotRecord[InventorySlots];
        for (int i = 0; i < InventorySlots; i++) slots[i] = new RebirthNpcInventorySlotRecord { SlotIndex = i };
        for (int i = 0; i < existing.Length; i++)
        {
            RebirthNpcInventorySlotRecord s = existing[i];
            if (s != null && s.SlotIndex >= 0 && s.SlotIndex < InventorySlots) slots[s.SlotIndex] = s;
        }
        record.Inventory.SlotRecords = slots;
        if (record.Inventory.OverflowRecords == null) record.Inventory.OverflowRecords = new RebirthNpcInventorySlotRecord[0];
    }

    public static float GetTraining(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        return TryGetView(id, out r, out d) ? Mathf.Clamp(d.Training, 0f, 100f) : 0f;
    }

    public static float GetBond(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        return TryGetView(id, out r, out d) ? Mathf.Clamp(d.Bond, 0f, 100f) : 0f;
    }

    public static bool KnowsCommand(RebirthNpcStableId id, string commandId)
    {
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        if (!TryGetView(id, out r, out d) || d == null || string.IsNullOrEmpty(commandId)) return false;
        return d.LearnedCommands.Contains(commandId);
    }

    public static int GetLearnedCommandCount(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        if (!TryGetView(id, out r, out d) || d == null) return 0;
        return d.LearnedCommands.Count;
    }

    public static bool IsLegacyCommandGrandfathered(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        return TryGetView(id, out r, out d) && d != null && d.LegacyCommandGrandfathered;
    }

    public static bool ApplyTrainingProgress(RebirthNpcStableId id, float trainingDelta, float bondDelta, IEnumerable<string> commandsToLearn)
    {
        bool changed = false;
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!IsDog(r)) return;
            if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
            float oldTraining = r.Dog.Training, oldBond = r.Dog.Bond;
            r.Dog.Training = Mathf.Clamp(r.Dog.Training + Math.Max(0f, trainingDelta), 0f, 100f);
            r.Dog.Bond = Mathf.Clamp(r.Dog.Bond + Math.Max(0f, bondDelta), 0f, 100f);
            RebirthDogTrainingCommandIds.EnsureBasic(r.Dog.LearnedCommands);
            if (commandsToLearn != null)
                foreach (string raw in commandsToLearn)
                {
                    string commandId = (raw ?? string.Empty).Trim();
                    if (commandId.Length > 0) r.Dog.LearnedCommands.Add(commandId);
                }
            changed = Math.Abs(r.Dog.Training - oldTraining) > 0.0001f || Math.Abs(r.Dog.Bond - oldBond) > 0.0001f || commandsToLearn != null;
            if (changed) unchecked { r.Dog.Revision++; }
        });
        return changed;
    }

    public static RebirthCompanionBehaviorMode GetCombatMode(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        return TryGetView(id, out r, out d) ? d.CombatMode : RebirthCompanionBehaviorMode.FullControl;
    }

    public static bool IsAttackStopped(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        return TryGetView(id, out r, out d) && d.AttackStopped;
    }

    public static bool SetCombatMode(RebirthNpcStableId id, RebirthCompanionBehaviorMode mode)
    {
        bool changed = false;
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!IsDog(r)) return;
            if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
            if (r.Dog.CombatMode == mode) return;
            r.Dog.CombatMode = mode; unchecked { r.Dog.Revision++; } changed = true;
        });
        return changed;
    }

    public static bool SetStayPosition(RebirthNpcStableId id, Vector3 position)
    {
        bool changed = false;
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!IsDog(r)) return;
            if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
            if (r.Dog.StayPosition.HasValue && (r.Dog.StayPosition.Value - position).sqrMagnitude < 0.0001f) return;
            r.Dog.StayPosition = position;
            unchecked { r.Dog.Revision++; }
            changed = true;
        });
        return changed;
    }

    public static bool ClearStayPosition(RebirthNpcStableId id)
    {
        bool changed = false;
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!IsDog(r) || r.Dog == null || !r.Dog.StayPosition.HasValue) return;
            r.Dog.StayPosition = null;
            unchecked { r.Dog.Revision++; }
            changed = true;
        });
        return changed;
    }

    public static bool IsGuardStationaryStay(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        return TryGetView(id, out r, out d) && d.GuardIsStationaryStay;
    }

    public static bool SetGuardStationaryStay(RebirthNpcStableId id, bool stationary)
    {
        bool changed = false;
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!IsDog(r)) return;
            if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
            if (r.Dog.GuardIsStationaryStay == stationary) return;
            r.Dog.GuardIsStationaryStay = stationary;
            unchecked { r.Dog.Revision++; }
            changed = true;
        });
        return changed;
    }

    public static bool SetAttackStopped(RebirthNpcStableId id, bool stopped)
    {
        bool changed = false;
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!IsDog(r)) return;
            if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
            if (r.Dog.AttackStopped == stopped) return;
            r.Dog.AttackStopped = stopped; unchecked { r.Dog.Revision++; } changed = true;
        });
        int entityId;
        if (changed && RebirthNpcRuntimeRegistry.TryGetEntityId(id, out entityId))
        {
            EntityRebirthDogCompanion dog = GameManager.Instance?.World?.GetEntity(entityId) as EntityRebirthDogCompanion;
            if (dog != null && stopped) RebirthDogRuntimeService.ClearCombat(dog);
        }
        return changed;
    }

    public static void SetOwnerCombatMode(EntityPlayer owner, RebirthCompanionBehaviorMode mode)
    {
        if (owner == null) return;
        string ownerId;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(owner, out ownerId)) return;
        if (owner.Buffs != null) owner.Buffs.SetCustomVar("varNPCModMode", mode == RebirthCompanionBehaviorMode.FullControl ? 0f : 1f);
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView r = records[i];
            if (!IsDog(r) || r.Ownership == null ||
                !string.Equals(r.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase)) continue;
            SetCombatMode(r.Identity.StableNpcId, mode);
        }
    }

    public static void SetOwnerAttackStopped(EntityPlayer owner, bool stopped)
    {
        if (owner == null) return;
        string ownerId;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(owner, out ownerId)) return;
        if (owner.Buffs != null) owner.Buffs.SetCustomVar("varNPCModStopAttacking", stopped ? 1f : 0f);
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView r = records[i];
            if (!IsDog(r) || r.Ownership == null ||
                !string.Equals(r.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase)) continue;
            SetAttackStopped(r.Identity.StableNpcId, stopped);
        }
    }

    public static void CaptureRuntime(EntityRebirthDogCompanion entity)
    {
        if (entity == null || entity.RebirthRuntimeState == null) return;
        RebirthNpcStableId id = entity.RebirthRuntimeState.StableId;
        string breed = ResolveBreedId(entity);
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            EnsureCommonRecord(r, breed, entity);
            if (r.Presence != null) r.Presence.PresenceState = entity.RebirthRuntimeState.Presence.ToString();
            if (r.Order == null && entity.RebirthRuntimeState.Order != RebirthNpcOrderState.None) r.Order = new RebirthNpcOrderRecord();
            if (r.Order != null) r.Order.OrderState = entity.RebirthRuntimeState.Order.ToString();
            // StayPosition is an order anchor, not a continuously sampled position.
            // 2.6 captured it when Stay was issued. Re-sampling every persistence tick
            // makes a pushed/fighting dog slowly move its own home point.
            if (entity.RebirthRuntimeState.Order == RebirthNpcOrderState.Stay && !r.Dog.StayPosition.HasValue)
                r.Dog.StayPosition = entity.position;
            else if (entity.RebirthRuntimeState.Order == RebirthNpcOrderState.Guard && entity.RebirthRuntimeState.HasGuardPosition)
                r.Transform.AnchorPosition = entity.RebirthRuntimeState.GuardPosition;
            unchecked { r.Dog.Revision++; }
        });
    }

    public static string ResolveBreedId(EntityRebirthDogCompanion dog)
    {
        if (dog == null) return string.Empty;
        try
        {
            EntityClass cls = EntityClass.list[dog.entityClass];
            RebirthDogBreedDefinition breed;
            if (cls != null && RebirthDogDefinitions.TryGetByEntityClass(cls.entityClassName, out breed)) return breed.BreedId;
        }
        catch { }
        return string.Empty;
    }

    public static bool TryGetBreed(RebirthNpcStableId id, out RebirthDogBreedDefinition breed)
    {
        breed = null;
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        return TryGetView(id, out r, out d) && RebirthDogDefinitions.TryGetByBreedId(d.BreedId, out breed);
    }

    public static int GetLevel(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        return TryGetView(id, out r, out d) ? Mathf.Clamp(d.Level, 1, 10) : 1;
    }

    public static int GetKills(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecordView r; RebirthDogPersistentRecordView d;
        return TryGetView(id, out r, out d) ? Math.Max(0, d.KillCount) : 0;
    }
}

/// <summary>Exact ten-slot dog storage and lock authority.</summary>
public static class RebirthDogInventoryService
{
    public const int Capacity = RebirthDogStateService.InventorySlots;

    public static RebirthDogInventorySnapshot GetSnapshot(RebirthNpcStableId id)
    {
        RebirthNpcPersistentRecordView record; RebirthDogPersistentRecordView dog;
        if (!RebirthDogStateService.TryGetView(id, out record, out dog))
            return new RebirthDogInventorySnapshot(id, EmptySlots(), new PackedBoolArray(Capacity), 0);
        // Missing inventory is represented as empty; reads must not create authority.
        if (record.Inventory == null || record.Inventory.SlotRecords == null)
            return new RebirthDogInventorySnapshot(id, EmptySlots(), new PackedBoolArray(Capacity),
                record.Inventory != null ? record.Inventory.InventoryRevision : 0U);
        ItemStack[] slots = EmptySlots();
        PackedBoolArray locks = new PackedBoolArray(Capacity);
        for (int i = 0; i < record.Inventory.SlotRecords.Length; i++)
        {
            RebirthNpcInventorySlotRecordView s = record.Inventory.SlotRecords[i];
            if (s == null || s.SlotIndex < 0 || s.SlotIndex >= Capacity) continue;
            locks[s.SlotIndex] = s.Locked;
            // Preserve the previous normalizer's last-slot-wins semantics, including
            // a duplicate whose final entry is empty or cannot be decoded.
            if (slots[s.SlotIndex] != null && !slots[s.SlotIndex].IsEmpty())
                slots[s.SlotIndex] = ItemStack.Empty.Clone();
            if (s.ItemCount <= 0 || string.IsNullOrEmpty(s.ItemValueSerialization)) continue;
            ItemValue value;
            if (!TryDeserializeItemValue(s.ItemValueSerialization, out value) || value.IsEmpty()) continue;
            slots[s.SlotIndex] = new ItemStack(value, s.ItemCount);
        }
        return new RebirthDogInventorySnapshot(id, slots, locks, record.Inventory.InventoryRevision);
    }

    public static bool HasAnyItems(RebirthNpcStableId id)
    {
        ItemStack[] slots = GetSnapshot(id).Slots;
        for (int i = 0; i < slots.Length; i++) if (slots[i] != null && !slots[i].IsEmpty()) return true;
        return false;
    }

    public static bool ToggleLock(RebirthNpcStableId id, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= Capacity) return false;
        bool changed = false;
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!RebirthDogStateService.IsDog(r)) return;
            RebirthDogStateService.EnsureInventoryRecord(r);
            RebirthNpcInventorySlotRecord s = r.Inventory.SlotRecords[slotIndex];
            s.Locked = !s.Locked; unchecked { r.Inventory.InventoryRevision++; } changed = true;
        });
        if (changed) Invalidate(id, "dog slot lock toggled");
        return changed;
    }

    public static bool CommitWorkingSlots(RebirthNpcStableId id, uint expectedRevision, ItemStack[] slots, PackedBoolArray locks, out string error)
    {
        error = string.Empty;
        if (slots == null || slots.Length != Capacity) { error = "dog inventory requires exactly 10 slots"; return false; }
        RebirthNpcPersistentRecordView observed; RebirthDogPersistentRecordView observedDog;
        if (!RebirthDogStateService.TryGetView(id, out observed, out observedDog))
        { error = "target is not a dog"; return false; }
        string commitError = string.Empty;
        bool committed = RebirthNpcAggregatePersistenceStore.TryMutateExisting(id, observed.AggregateRevision, delegate(RebirthNpcPersistentRecord r)
        {
            if (!RebirthDogStateService.IsDog(r)) { commitError = "target is not a dog"; return false; }
            // Revision zero is an observed empty/uninitialized state, not a wildcard.
            // Compare before normalization creates revision one.
            uint currentRevision = r.Inventory != null ? r.Inventory.InventoryRevision : 0U;
            if (currentRevision != expectedRevision) { commitError = "inventory revision changed"; return false; }
            RebirthDogStateService.EnsureInventoryRecord(r);
            for (int i = 0; i < Capacity; i++)
            {
                RebirthNpcInventorySlotRecord dst = r.Inventory.SlotRecords[i];
                ItemStack src = slots[i];
                dst.Locked = locks != null && i < locks.Length && locks[i];
                dst.Reserved = false;
                if (src == null || src.IsEmpty() || src.count <= 0)
                {
                    dst.ItemValueSerialization = string.Empty; dst.ItemCount = 0; dst.MetadataOrCustomData = string.Empty;
                }
                else
                {
                    dst.ItemValueSerialization = SerializeItemValue(src.itemValue);
                    dst.ItemCount = src.count;
                    dst.MetadataOrCustomData = src.itemValue.ItemClass != null ? src.itemValue.ItemClass.Name : string.Empty;
                }
            }
            unchecked { r.Inventory.InventoryRevision++; }
            r.Inventory.ContentChecksum = ComputeChecksum(r.Inventory.SlotRecords);
            return true;
        });
        error = !committed && string.IsNullOrEmpty(commitError) ? "inventory aggregate changed before commit" : commitError;
        if (committed)
        {
            Invalidate(id, "dog inventory committed");
            SynchronizeActiveInventoryEffects(id);
        }
        return committed;
    }

    public static bool CanAcceptAll(RebirthNpcStableId id, ItemStack stack)
    {
        if (stack == null || stack.IsEmpty() || stack.count <= 0) return false;
        RebirthDogInventorySnapshot snapshot = GetSnapshot(id);
        int remaining = stack.count;
        int stackMax = stack.itemValue.ItemClass != null ? Math.Max(1, stack.itemValue.ItemClass.Stacknumber.Value) : 1;
        for (int i = 0; i < Capacity && remaining > 0; i++)
        {
            ItemStack target = snapshot.Slots[i];
            if (target == null || target.IsEmpty() || !target.CanStackWith(stack)) continue;
            int targetMax = target.itemValue.ItemClass != null ? Math.Max(1, target.itemValue.ItemClass.Stacknumber.Value) : stackMax;
            remaining -= Math.Min(remaining, Math.Max(0, targetMax - target.count));
        }
        for (int i = 0; i < Capacity && remaining > 0; i++)
        {
            ItemStack target = snapshot.Slots[i];
            if (target != null && !target.IsEmpty()) continue;
            remaining -= Math.Min(remaining, stackMax);
        }
        return remaining <= 0;
    }

    private static void SynchronizeActiveInventoryEffects(RebirthNpcStableId id)
    {
        int entityId;
        if (!RebirthNpcRuntimeRegistry.TryGetEntityId(id, out entityId)) return;
        EntityRebirthDogCompanion dog = GameManager.Instance?.World?.GetEntity(entityId) as EntityRebirthDogCompanion;
        if (dog != null) RebirthDogResistanceService.Synchronize(dog);
    }

    public static bool TryRemove(RebirthNpcStableId id, int slotIndex, int count, bool respectLock, out ItemStack removed, int expectedItemType = 0)
    {
        removed = ItemStack.Empty.Clone();
        if (slotIndex < 0 || slotIndex >= Capacity || count <= 0) return false;
        RebirthDogInventorySnapshot snapshot = GetSnapshot(id);
        if (respectLock && snapshot.Locks != null && snapshot.Locks[slotIndex]) return false;
        ItemStack source = snapshot.Slots[slotIndex];
        if (source == null || source.IsEmpty() || source.count <= 0) return false;
        // A delayed UI request must not consume a different item now occupying this slot.
        if (expectedItemType != 0 && source.itemValue.type != expectedItemType) return false;
        int take = Math.Min(count, source.count);
        removed = source.Clone(); removed.count = take;
        ItemStack remaining = source.Clone(); remaining.count -= take;
        snapshot.Slots[slotIndex] = remaining.count > 0 ? remaining : ItemStack.Empty.Clone();
        string error;
        if (!CommitWorkingSlots(id, snapshot.Revision, snapshot.Slots, snapshot.Locks, out error))
        { removed = ItemStack.Empty.Clone(); return false; }
        return true;
    }

    public static bool TryAddAtRevision(RebirthNpcStableId id, uint expectedRevision, ItemStack stack, out int added)
    {
        added = 0;
        if (expectedRevision == 0 || stack == null || stack.IsEmpty() || stack.count <= 0) return false;
        RebirthDogInventorySnapshot snapshot = GetSnapshot(id);
        if (snapshot.Revision != expectedRevision) return false;
        ItemStack remaining = stack.Clone();
        for (int i = 0; i < Capacity && remaining.count > 0; i++)
        {
            ItemStack target = snapshot.Slots[i];
            if (target == null || target.IsEmpty() || !target.CanStackWith(remaining)) continue;
            int room = Math.Max(0, target.itemValue.ItemClass.Stacknumber.Value - target.count);
            int move = Math.Min(room, remaining.count);
            if (move <= 0) continue;
            target.count += move; remaining.count -= move; added += move;
        }
        for (int i = 0; i < Capacity && remaining.count > 0; i++)
        {
            ItemStack target = snapshot.Slots[i];
            if (target != null && !target.IsEmpty()) continue;
            int max = Math.Max(1, remaining.itemValue.ItemClass.Stacknumber.Value);
            int move = Math.Min(max, remaining.count);
            ItemStack placed = remaining.Clone(); placed.count = move;
            snapshot.Slots[i] = placed; remaining.count -= move; added += move;
        }
        if (remaining.count > 0 || added != stack.count) { added = 0; return false; }
        string error;
        if (!CommitWorkingSlots(id, expectedRevision, snapshot.Slots, snapshot.Locks, out error)) { added = 0; return false; }
        return true;
    }

    public static bool TryAdd(RebirthNpcStableId id, ItemStack stack, out int added)
    {
        added = 0;
        if (stack == null || stack.IsEmpty() || stack.count <= 0) return false;
        RebirthDogInventorySnapshot snapshot = GetSnapshot(id);
        ItemStack remaining = stack.Clone();
        // Existing matching stacks first, including locked destination slots.  Lock protects automatic removal, not stacking in.
        for (int i = 0; i < Capacity && remaining.count > 0; i++)
        {
            ItemStack target = snapshot.Slots[i];
            if (target == null || target.IsEmpty() || !target.CanStackWith(remaining)) continue;
            int room = Math.Max(0, target.itemValue.ItemClass.Stacknumber.Value - target.count);
            int move = Math.Min(room, remaining.count);
            if (move <= 0) continue;
            target.count += move; remaining.count -= move; added += move;
        }
        for (int i = 0; i < Capacity && remaining.count > 0; i++)
        {
            ItemStack target = snapshot.Slots[i];
            if (target != null && !target.IsEmpty()) continue;
            int max = Math.Max(1, remaining.itemValue.ItemClass.Stacknumber.Value);
            int move = Math.Min(max, remaining.count);
            ItemStack placed = remaining.Clone(); placed.count = move;
            snapshot.Slots[i] = placed; remaining.count -= move; added += move;
        }
        if (added <= 0) return false;
        string error;
        if (!CommitWorkingSlots(id, snapshot.Revision, snapshot.Slots, snapshot.Locks, out error)) { added = 0; return false; }
        return true;
    }

    public static bool TransferToPlayer(EntityPlayer player, RebirthNpcStableId id, int slotIndex, int count, int expectedItemType)
    {
        if (player?.bag == null || count <= 0 || expectedItemType <= 0) return false;
        ItemStack removed;
        if (!TryRemove(id, slotIndex, count, true, out removed, expectedItemType)) return false;
        ItemStack remaining = removed.Clone();
        LogisticsTransferService.MoveIntoBag(player, remaining);
        int moved = removed.count - remaining.count;
        if (remaining.count > 0)
        {
            int restored;
            TryAdd(id, remaining, out restored);
            remaining.count -= restored;
            if (remaining.count > 0)
                GameManager.Instance.ItemDropServer(remaining, player.position, Vector3.zero,
                    player.entityId, 120f, false);
        }
        return moved > 0;
    }

    public static bool ConsumeOne(RebirthNpcStableId id, string itemName)
    {
        RebirthDogInventorySnapshot snapshot = GetSnapshot(id);
        for (int i = 0; i < snapshot.Slots.Length; i++)
        {
            ItemStack s = snapshot.Slots[i];
            if (s == null || s.IsEmpty() || s.itemValue.ItemClass == null ||
                !string.Equals(s.itemValue.ItemClass.Name, itemName, StringComparison.OrdinalIgnoreCase)) continue;
            ItemStack ignored;
            return TryRemove(id, i, 1, false, out ignored);
        }
        return false;
    }

    public static bool UseOne(EntityPlayer player, RebirthNpcStableId id, int slotIndex, int expectedItemType)
    {
        if (player == null || slotIndex < 0 || slotIndex >= Capacity) return false;
        RebirthDogInventorySnapshot snapshot = GetSnapshot(id);
        ItemStack source = snapshot.Slots[slotIndex];
        if (source == null || source.IsEmpty() || source.itemValue.type != expectedItemType || source.itemValue.ItemClass == null || source.itemValue.ItemClass.Actions == null || source.itemValue.ItemClass.Actions.Length == 0) return false;
        ItemAction action = source.itemValue.ItemClass.Actions[0];
        if (!(action is ItemActionEat)) return false;
        RebirthConsumableDefinition consumption;
        if (action is ItemActionConsumeMetabolismRebirth &&
            RebirthConsumableResolver.TryResolve(source.itemValue, out consumption) && consumption != null)
        {
            RebirthConsumeResult result = RebirthMetabolismService.ConsumeExternalSource(player, source,
                delegate(ItemStack expected, ItemStack replacement, ItemStack siblings)
                {
                    ItemStack[] planned;
                    if (!RebirthCompanionConsumptionSlots.TryPlan(snapshot.Slots, snapshot.Locks, slotIndex,
                        expected, replacement, siblings, out planned)) return false;
                    string error;
                    return CommitWorkingSlots(id, snapshot.Revision, planned, snapshot.Locks, out error);
                });
            RebirthCompanionService.SendConsumptionResult(player, result);
            return result.Success;
        }
        var treatment = action as ItemActionUseMedRebirth;
        if (treatment != null && !treatment.CanUseTreatment(player)) return false;
        ItemStack removed;
        if (!TryRemove(id, slotIndex, 1, false, out removed, expectedItemType)) return false;
        bool success = false;
        try { success = action.ExecuteInstantAction(player, removed, false, null); }
        catch (Exception ex) { Log.Warning("[REBIRTH Dog] inventory Use failed: " + ex.Message); }
        if (!success) { int restored; TryAdd(id, removed, out restored); }
        return success;
    }

    private static ItemStack[] EmptySlots()
    {
        ItemStack[] result = new ItemStack[Capacity];
        for (int i = 0; i < result.Length; i++) result[i] = ItemStack.Empty.Clone();
        return result;
    }

    private static string SerializeItemValue(ItemValue value)
    {
        using (MemoryStream stream = new MemoryStream())
        using (PooledBinaryWriter writer = MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            writer.SetBaseStream(stream);
            ItemValue.Write(value, writer);
            writer.Flush();
            return Convert.ToBase64String(stream.ToArray());
        }
    }

    private static bool TryDeserializeItemValue(string text, out ItemValue value)
    {
        value = ItemValue.None;
        try
        {
            byte[] bytes = Convert.FromBase64String(text ?? string.Empty);
            using (MemoryStream stream = new MemoryStream(bytes))
            using (PooledBinaryReader reader = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                ItemValue read = ItemValue.ReadOrNull(reader);
                if (read == null) return false;
                value = read;
                return true;
            }
        }
        catch { return false; }
    }

    private static string ComputeChecksum(RebirthNpcInventorySlotRecord[] slots)
    {
        StringBuilder b = new StringBuilder();
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            RebirthNpcInventorySlotRecord s = slots[i]; if (s == null) continue;
            b.Append(s.SlotIndex).Append('|').Append(s.ItemValueSerialization).Append('|').Append(s.ItemCount).Append('|').Append(s.Locked ? 1 : 0).Append(';');
        }
        return b.ToString().GetHashCode().ToString("X8", CultureInfo.InvariantCulture);
    }

    private static void Invalidate(RebirthNpcStableId id, string reason)
    {
        int entityId;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(id, out entityId))
        {
            Entity e = GameManager.Instance?.World?.GetEntity(entityId);
            RemoteResourceLiveSync.NotifySourceChanged("N:" + id, e != null ? e.position : Vector3.zero, reason);
        }
        else RemoteResourceSnapshotCache.InvalidateSource("N:" + id);
    }
}

/// <summary>
/// Dog-specific movement, target acquisition, autonomous healing and progression.
/// High-level Follow/Stay/Guard commands still use the existing authenticated
/// command framework; this service adds animal recovery and combat policy.
/// </summary>
public static class RebirthDogRuntimeService
{
    private sealed class RuntimeCache
    {
        public long NextMovementTicks;
        public long NextCombatTicks;
        public long NextHealTicks;
        public long NextPersistTicks;
        public long NextWanderTicks;
        public Vector3 LastPosition;
        public long LastMovedTicks;
        public long NextRecoveryTicks;
        public int CombatTargetId = -1;
        public long CombatTargetSinceTicks;
        public long CombatLastProgressTicks;
        public float CombatLastDistance = float.MaxValue;
        public long NextPathRequestTicks;
        public bool HasPathDestination;
        public Vector3 LastPathDestination;
        public Vector3 SeekOffset;
        public string LastMovementDecision = "none";
        public string LastCombatDecision = "none";
        public int LastCombatCandidateId = -1;
        public string LastCombatCandidateReason = "none";
        public int PathRequests;
        public int PathSkips;
        public long LastPathRequestTicks;
        public Vector3 LastRequestedDestination;
        // Manual/automatic recall guard. Recall now resolves a supported standing point
        // synchronously before relocation. This remains as an emergency fall-through guard:
        // it evaluates the dog's CURRENT X/Z so normal Follow movement can never be snapped
        // back to the original recall point.
        public long RecallGroundGuardUntilTicks;
        public bool HasRecallGroundTarget;
        public Vector3 RecallGroundTarget;
    }

    private static readonly Dictionary<int, RuntimeCache> Cache = new Dictionary<int, RuntimeCache>();
    private static readonly object Sync = new object();
    private static readonly FastTags<TagGroup.Global> NoMeleeTrackingTag = FastTags<TagGroup.Global>.Parse("nomeleetracking");

    // Combat radii are sandbox-configurable. Positional order and combat mode remain
    // independent: Stay never moves, Guard may pursue only inside Guard Distance, and
    // Full Control/Hunting define both acquisition and continued-pursuit distance.
    public const float OfflineParkDefenseRadius = 3f;

    private static float GuardRadius { get { return RebirthSandboxOptionManager.Current.GuardDistance; } }
    private static float FullControlRadius { get { return RebirthSandboxOptionManager.Current.FullControlDistance; } }
    private static float HuntingRadius { get { return RebirthSandboxOptionManager.Current.HuntingDistance; } }

    private const float FollowStopDistance = 3.5f;
    private const float StayReturnDistance = 6f;
    private const float AttackProgressEpsilon = 0.75f;
    private const float AttackNoProgressTimeoutSeconds = 8f;

    public static void Tick(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.IsDead() || dog.RebirthRuntimeState == null ||
            !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

        long now = DateTime.UtcNow.Ticks;
        RuntimeCache c = GetCache(dog.entityId, dog.position, now);
        // Hot ticks reuse the immutable revision view. Initialization and the existing
        // five-second CaptureRuntime path own writes; do not clone/checksum every frame.
        RebirthNpcPersistentRecordView aggregateView; RebirthDogPersistentRecordView dogRecord;
        if (!RebirthDogStateService.TryGetView(dog.RebirthRuntimeState.StableId, out aggregateView, out dogRecord))
            dogRecord = RebirthDogStateService.EnsureView(dog);
        if (dogRecord == null) return;

        // Legacy compatibility presentation/state is synchronized from the canonical
        // aggregate rather than used as authority.
        SynchronizeLegacyBehaviorBuffs(dog, dogRecord);
        RebirthAnimalHandlingService.TickDogTraining(dog, dogRecord);

        if (dogRecord.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn)
        {
            RebirthDogLifecycleService.MaintainAwaitingRespawnProjection(dog);
            // 2.6 parity: an off-duty/awaiting-respawn companion receives a small
            // 3-health pulse every ~3 seconds only while it is on the owner's CURRENT
            // bed/bedroll spawn point. It does not instantly recover on Report for Duty.
            if (now >= c.NextHealTicks)
            {
                c.NextHealTicks = now + TimeSpan.FromSeconds(3).Ticks;
                TickAwaitingRespawnHealing(dog);
            }
            if (now >= c.NextPersistTicks)
            {
                c.NextPersistTicks = now + TimeSpan.FromSeconds(5).Ticks;
                RebirthDogStateService.CaptureRuntime(dog);
            }
            lock (Sync) Cache[dog.entityId] = c;
            return;
        }

        // A long-range recall can expose a bad collider/chunk handoff by allowing the dog
        // to drop below the surface immediately after relocation. Recover only the vertical
        // failure and preserve the exact recalled X/Z.
        TickRecallGroundGuard(dog, c, now);

        // Native approach AI can submit movement between the 250 ms policy ticks.
        // Enforce a strict hold every live update while a Stay dog is already on its
        // captured block. Stay is non-combat, so target state is also cleared here.
        MaintainStationaryAnchorLock(dog, dogRecord);

        if (now >= c.NextMovementTicks)
        {
            c.NextMovementTicks = now + TimeSpan.FromMilliseconds(250).Ticks;
            TickMovement(dog, c, dogRecord, now);
            // Movement may publish offline-park/return state. Immutable views are
            // point-in-time: combat in this same policy update needs the new revision.
            if (!RebirthDogStateService.TryGetView(dog.RebirthRuntimeState.StableId,
                out aggregateView, out dogRecord)) return;
        }
        if (now >= c.NextCombatTicks)
        {
            c.NextCombatTicks = now + TimeSpan.FromMilliseconds(250).Ticks;
            TickCombat(dog, c, dogRecord, now);
        }
        if (now >= c.NextHealTicks)
        {
            // 2.6 checked autonomous companion recovery every 60 game ticks
            // (roughly three seconds), not every live-update/1.25 seconds.
            c.NextHealTicks = now + TimeSpan.FromSeconds(3).Ticks;
            TickHealing(dog, dogRecord);
            RebirthDogResistanceService.Synchronize(dog);
        }
        if (now >= c.NextPersistTicks)
        {
            c.NextPersistTicks = now + TimeSpan.FromSeconds(5).Ticks;
            SyncProgressionFromCvars(dog);
            RebirthDogStateService.CaptureRuntime(dog);
        }

        lock (Sync) Cache[dog.entityId] = c;
    }

    private const float FriendlyPassThroughDistance = 5f;

    /// <summary>
    /// 2.6 NPCUtils.CheckCollision parity. A nearby friendly player disables the dog's
    /// physics collision object and LargeEntityBlocker and projects IsNoCollisionMode.
    /// This allows both physical player clipping and weapon traces to pass through the dog.
    /// </summary>
    public static void TickFriendlyPlayerPassThrough(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.world == null) return;
        bool friendlyPlayerNear = false;
        try
        {
            var players = dog.world.Players != null ? dog.world.Players.list : null;
            if (players != null)
            {
                float maxSqr = FriendlyPassThroughDistance * FriendlyPassThroughDistance;
                for (int i = 0; i < players.Count; i++)
                {
                    EntityPlayer player = players[i];
                    if (player == null || player.IsDead() || (player.position - dog.position).sqrMagnitude > maxSqr) continue;
                    string relationReason;
                    if (RebirthDogCombatRelationService.AreFriendly(dog, player, out relationReason))
                    {
                        friendlyPlayerNear = true;
                        break;
                    }
                }
            }
        }
        catch { }

        SetFriendlyPlayerPassThrough(dog, friendlyPlayerNear);
    }

    /// <summary>
    /// Applies the same 2.6 friendly-player pass-through state immediately. Recall calls this
    /// before changing position so a dog cannot shove the owner during the destination frame.
    /// </summary>
    public static void SetFriendlyPlayerPassThrough(EntityRebirthDogCompanion dog, bool enabled)
    {
        if (dog == null) return;
        bool collisionEnabled = !enabled;
        try
        {
            Transform blocker = dog.GetRebirthCollisionBlocker();
            if (blocker != null && blocker.gameObject.activeSelf != collisionEnabled)
                blocker.gameObject.SetActive(collisionEnabled);
        }
        catch { }
        try
        {
            if (dog.PhysicsTransform != null && dog.PhysicsTransform.gameObject.activeSelf != collisionEnabled)
                dog.PhysicsTransform.gameObject.SetActive(collisionEnabled);
        }
        catch { }
        try { dog.IsNoCollisionMode.Value = enabled; } catch { }
    }

    public static void RestoreDogCollision(EntityRebirthDogCompanion dog)
    {
        if (dog == null) return;
        try
        {
            Transform blocker = dog.GetRebirthCollisionBlocker();
            if (blocker != null && !blocker.gameObject.activeSelf) blocker.gameObject.SetActive(true);
        }
        catch { }
        try
        {
            if (dog.PhysicsTransform != null && !dog.PhysicsTransform.gameObject.activeSelf)
                dog.PhysicsTransform.gameObject.SetActive(true);
        }
        catch { }
        try { dog.IsNoCollisionMode.Value = false; } catch { }
    }

    public static void Remove(int entityId) { lock (Sync) Cache.Remove(entityId); }
    public static void Reset() { lock (Sync) Cache.Clear(); }

    public static void ClearCombat(EntityRebirthDogCompanion dog)
    {
        if (dog == null) return;
        dog.SetAttackTarget(null, 0);
        dog.SetRevengeTarget(null);
        try
        {
            if (dog.getNavigator() != null) dog.getNavigator().clearPath();
            if (dog.moveHelper != null) dog.moveHelper.Stop();
        }
        catch { }
        RuntimeCache c;
        lock (Sync)
        {
            if (Cache.TryGetValue(dog.entityId, out c))
            {
                c.CombatTargetId = -1;
                c.CombatTargetSinceTicks = 0L;
                c.CombatLastProgressTicks = 0L;
                c.CombatLastDistance = float.MaxValue;
            }
        }
    }

    public static bool Recall(EntityRebirthDogCompanion dog, EntityPlayer owner)
    {
        if (!CanRecallToOwner(dog, owner, false)) return false;

        // Automatic owner-teleport recovery remains Follow-only. This preserves the
        // authored meaning of Stay/Guard during normal travel.
        ClearCombat(dog);
        if (!TryCompleteRecallRecovery(dog, owner, false))
            return RebirthDogChunkObserverService.RequestLiveFollowRecovery(dog, owner);

        return true;
    }

    /// <summary>
    /// Manual mass-recall shortcut. Unlike automatic Follow recovery, this intentionally
    /// recalls every active owned dog. Stay/Guard are preserved, but their anchor is moved
    /// to the recalled position so they do not immediately try to return to the old site.
    /// </summary>
    public static bool RecallFromShortcut(EntityRebirthDogCompanion dog, EntityPlayer owner)
    {
        if (!CanRecallToOwner(dog, owner, true)) return false;

        ClearCombat(dog);
        if (!TryCompleteRecallRecovery(dog, owner, true))
            return RebirthDogChunkObserverService.RequestLiveRecallRecovery(dog, owner);

        return true;
    }

    internal static bool TryCompleteFollowRecovery(EntityRebirthDogCompanion dog, EntityPlayer owner)
    {
        return TryCompleteRecallRecovery(dog, owner, false);
    }

    internal static bool TryCompleteManualRecallRecovery(EntityRebirthDogCompanion dog, EntityPlayer owner)
    {
        return TryCompleteRecallRecovery(dog, owner, true);
    }

    private static bool TryCompleteRecallRecovery(EntityRebirthDogCompanion dog, EntityPlayer owner, bool allowAnchoredOrders)
    {
        if (!CanRecallToOwner(dog, owner, allowAnchoredOrders)) return false;

        // 2.6 TeleportToPlayer recovered companions to a safe owner-relative position. Resolve
        // support BEFORE moving the live dog so it never spends several seconds suspended at
        // the owner's transform waiting for gravity/ground repair. Disable collision after the
        // destination is known but before relocation so the exact-X/Z case cannot push the player.
        Vector3 target;
        if (!TryResolveRecallDestination(dog, owner, out target)) return false;
        RebirthCompanionCollisionService.PrepareForRecall(dog, owner);
        if (!TryRelocateLiveDog(dog, target)) return false;

        try
        {
            if (dog.getNavigator() != null) dog.getNavigator().clearPath();
            if (dog.moveHelper != null) dog.moveHelper.Stop();
            dog.motion = Vector3.zero;
        }
        catch { }

        if (allowAnchoredOrders)
            ReanchorOrderAfterManualRecall(dog, target);

        ArmRecallGroundGuard(dog, target);
        RebirthCompanionRecallDebug.TraceTeleport("DOG_RECALL_COMPLETE", dog, owner);

        // Only publish the new durable position after the live entity is physically resident
        // in the destination chunk. This prevents the Companions list/nav marker from
        // advertising a position at which no dog entity actually exists.
        RebirthDogStateService.CaptureRuntime(dog);
        return true;
    }

    private static bool TryResolveRecallDestination(EntityRebirthDogCompanion dog, EntityPlayer owner, out Vector3 target)
    {
        target = owner != null ? owner.position : Vector3.zero;
        if (dog == null || owner == null || dog.world == null || dog.RebirthRuntimeState == null) return false;

        // Preserve the current Recall contract first: exact owner X/Z, but with a supported
        // standing Y resolved synchronously. This removes the airborne pause without changing
        // the horizontal recall destination the player already expects.
        if (TryFindSafeAtExactXZ(dog.world, owner.position, out target)) return true;

        // If exact X/Z is genuinely not standable (tight geometry, edge/collider case), fall
        // back to the old safe owner-relative behavior instead of placing the dog in mid-air.
        // StableId/entityId spread a mass recall across nearby cells.
        RebirthNpcStableId stableId = dog.RebirthRuntimeState.StableId;
        return TryFindSafeNearOwner(dog.world, owner, stableId, dog.entityId, out target);
    }

    private static bool CanRecallToOwner(EntityRebirthDogCompanion dog, EntityPlayer owner, bool allowAnchoredOrders)
    {
        if (dog == null || owner == null || !RebirthDogLifecycleService.IsOwnedBy(dog, owner)) return false;
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        if (state == null || state.Presence != RebirthNpcPresenceState.Active || dog.IsDead()) return false;
        if (allowAnchoredOrders) return true;

        // Automatic owner teleport keeps Stay/Guard where the player deliberately anchored
        // them. Only the explicit mass-recall shortcut overrides that positioning rule.
        return state.Order != RebirthNpcOrderState.Stay && state.Order != RebirthNpcOrderState.Guard;
    }

    private static void ReanchorOrderAfterManualRecall(EntityRebirthDogCompanion dog, Vector3 target)
    {
        if (dog == null || dog.RebirthRuntimeState == null) return;
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        RebirthNpcStableId stableId = state.StableId;

        if (state.Order == RebirthNpcOrderState.Stay)
        {
            RebirthDogStateService.SetStayPosition(stableId, target);
            RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
            {
                if (r == null) return;
                if (r.Transform == null) r.Transform = new RebirthNpcTransformRecord();
                r.Transform.AnchorPosition = target;
                r.Transform.AnchorRotation = Mathf.Repeat(dog.rotation.y, 360f);
                unchecked { r.Transform.TransformRevision++; }
            });
        }
        else if (state.Order == RebirthNpcOrderState.Guard)
        {
            // Guard's live navigation leash comes from RuntimeState.GuardPosition. Re-submit
            // the same order with the new anchor so both runtime and persistence agree.
            try { dog.SetRebirthOrder(RebirthNpcOrderState.Guard, target, true); } catch { }
            RebirthNpcAggregatePersistenceStore.Mutate(stableId, delegate(RebirthNpcPersistentRecord r)
            {
                if (r == null) return;
                if (r.Transform == null) r.Transform = new RebirthNpcTransformRecord();
                r.Transform.AnchorPosition = target;
                r.Transform.AnchorRotation = Mathf.Repeat(dog.rotation.y, 360f);
                unchecked { r.Transform.TransformRevision++; }
            });
        }
    }

    internal static void ArmRecallGroundGuardForRecovery(EntityRebirthDogCompanion dog, Vector3 target)
    {
        ArmRecallGroundGuard(dog, target);
    }

    private static void ArmRecallGroundGuard(EntityRebirthDogCompanion dog, Vector3 target)
    {
        if (dog == null) return;
        long now = DateTime.UtcNow.Ticks;
        RuntimeCache c = GetCache(dog.entityId, dog.position, now);
        c.RecallGroundTarget = target;
        c.HasRecallGroundTarget = true;
        c.RecallGroundGuardUntilTicks = now + TimeSpan.FromSeconds(8).Ticks;
        lock (Sync) Cache[dog.entityId] = c;
    }

    private static void TickRecallGroundGuard(EntityRebirthDogCompanion dog, RuntimeCache c, long now)
    {
        if (dog == null || c == null || !c.HasRecallGroundTarget) return;
        if (now > c.RecallGroundGuardUntilTicks)
        {
            c.HasRecallGroundTarget = false;
            return;
        }

        // IMPORTANT: never return a moving Follow companion to the original recall point.
        // The previous guard coupled fall detection to IsPersistableLiveProjection(). When a
        // dog crossed a chunk boundary immediately after recall, transient native-chunk
        // reconciliation made that projection check false and the guard repeatedly snapped
        // the dog back to RecallGroundTarget even though its Y position was perfectly valid.
        //
        // A real fall-through failure is purely geometric: at the dog's CURRENT X/Z there is
        // a valid supported standing position materially ABOVE the dog. Recover only that
        // vertical failure, at that current X/Z. Chunk/persistence state is deliberately not
        // part of this decision.
        Vector3 before = dog.position;
        Vector3 safe;
        if (!TryFindSafeAtExactXZ(dog.world, before, out safe)) return;

        float recoveryRise = safe.y - before.y;
        if (recoveryRise <= 0.35f) return;

        if (!TryRelocateLiveDog(dog, safe)) return;
        try
        {
            dog.motion = Vector3.zero;
            if (dog.getNavigator() != null) dog.getNavigator().clearPath();
            if (dog.moveHelper != null) dog.moveHelper.Stop();
        }
        catch { }

        RebirthDogStateService.CaptureRuntime(dog);
        RebirthCompanionRecallDebug.TraceGroundRecovery(dog, before, safe);
    }

    /// <summary>
    /// Moves an already-live dog across 3.1 native chunk ownership synchronously.
    /// SetPosition alone is not sufficient for long-range relocation because the old chunk
    /// still owns the entity until World.TickEntity performs its later reconciliation.
    /// </summary>
    private static bool TryRelocateLiveDog(EntityRebirthDogCompanion dog, Vector3 target)
    {
        World world = dog != null ? dog.world : null;
        if (world == null || !dog.IsSpawned()) return false;

        // Do not publish/move into a destination that is still streaming. The pending
        // recovery observer keeps the source entity alive and retries on the normal dog tick.
        if (!world.IsChunkAreaLoaded(target)) return false;

        int targetChunkX = World.toChunkXZ(Utils.Fastfloor(target.x));
        int targetChunkZ = World.toChunkXZ(Utils.Fastfloor(target.z));
        Chunk targetChunk = world.GetChunkSync(targetChunkX, targetChunkZ) as Chunk;
        if (targetChunk == null || targetChunk.InProgressUnloading) return false;

        Vector3 originalPosition = dog.position;
        bool originalAddedToChunk = dog.addedToChunk;
        int originalChunkX = dog.chunkPosAddedEntityTo.x;
        int originalChunkZ = dog.chunkPosAddedEntityTo.z;
        Chunk originalChunk = originalAddedToChunk
            ? world.GetChunkSync(originalChunkX, originalChunkZ) as Chunk
            : null;
        bool changingChunk = !originalAddedToChunk || originalChunkX != targetChunkX || originalChunkZ != targetChunkZ;

        // If the source chunk is already in its unload transition, do not race that
        // teardown. The retained recovery observer will cause the original serialized dog
        // to materialize again and Synchronize will retry once both source and destination
        // are stable. Moving during InProgressUnloading can otherwise leave the old chunk
        // serializing a stale reference while the destination also owns the same entity.
        if (originalAddedToChunk && (originalChunk == null || originalChunk.InProgressUnloading))
            return false;

        try
        {
            if (changingChunk)
            {
                if (originalAddedToChunk && originalChunk != null)
                    originalChunk.RemoveEntityFromChunk(dog);
                // Even if the source chunk is already disappearing, the destination must
                // take explicit ownership rather than treating stale x/z membership as valid.
                dog.addedToChunk = false;
            }

            dog.SetPosition(target, true);
            dog.position = target;

            if (!dog.addedToChunk)
                targetChunk.AddEntityToChunk(dog);
            else
                targetChunk.AdJustEntityTracking(dog);

            // AddEntityToChunk does not itself mark the destination dirty, while this dog is
            // a saveable entity. Mark it so a save cannot leave the dog serialized only in
            // the source chunk after a hard relocation.
            targetChunk.isModified = true;

            // EntityEnemy/NPC network tracking is range-based. Reset only the distributor
            // entry (not the World entity) after a hard cross-chunk move so remote clients
            // receive an unload from the old tracking position followed by a spawn at the
            // new one. This also avoids the 3.1 encoded-position ordering race where a
            // teleported entity can fall out of the 80 m tracked set before its teleport
            // packet is delivered.
            ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (changingChunk && connection != null && connection.IsServer && !connection.IsSinglePlayer &&
                world.entityDistributer != null && world.entityDistributer.FindEntry(dog) != null)
            {
                world.entityDistributer.Remove(dog, EnumRemoveEntityReason.Unloaded);
                world.entityDistributer.Add(dog);
            }

            if (!RebirthDogStateService.IsPersistableLiveProjection(dog))
                throw new InvalidOperationException("relocated dog failed physical projection validation");

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Dog] live relocation failed entity=" + dog.entityId +
                " target=" + target + " reason=" + ex.Message);

            // Best-effort rollback so a failed relocation does not itself orphan the entity
            // from native chunk serialization/tracking.
            try
            {
                if (dog.addedToChunk && (dog.chunkPosAddedEntityTo.x != originalChunkX || dog.chunkPosAddedEntityTo.z != originalChunkZ))
                {
                    Chunk currentChunk = world.GetChunkSync(dog.chunkPosAddedEntityTo.x, dog.chunkPosAddedEntityTo.z) as Chunk;
                    if (currentChunk != null) currentChunk.RemoveEntityFromChunk(dog);
                    dog.addedToChunk = false;
                }
                dog.SetPosition(originalPosition, true);
                dog.position = originalPosition;
                if (originalAddedToChunk && originalChunk != null && !dog.addedToChunk)
                {
                    originalChunk.AddEntityToChunk(dog);
                    originalChunk.isModified = true;
                }
            }
            catch { }
            return false;
        }
    }

    public static EntityPlayer ResolveOwnerPublic(World world, string ownerId)
    {
        return ResolveOwner(world, ownerId);
    }

    public static bool IsUnavailable(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.IsDead() || dog.RebirthRuntimeState == null) return true;
        RebirthNpcPersistentRecordView record;
        RebirthDogPersistentRecordView dogRecord;
        return !RebirthDogStateService.TryGetView(dog.RebirthRuntimeState.StableId, out record, out dogRecord) ||
               !LifecycleAvailable(dog, dogRecord);
    }

    private static RuntimeCache GetCache(int entityId, Vector3 position, long now)
    {
        lock (Sync)
        {
            RuntimeCache c;
            if (!Cache.TryGetValue(entityId, out c))
            {
                c = new RuntimeCache
                {
                    LastPosition = position,
                    LastMovedTicks = now,
                    CombatLastProgressTicks = now
                };
                Cache[entityId] = c;
            }
            return c;
        }
    }

    private static bool LifecycleAvailable(EntityRebirthDogCompanion dog, RebirthDogPersistentRecordView record)
    {
        if (dog == null || record == null || record.Lifecycle != RebirthDogLifecycleKind.Active) return false;
        if (dog.Buffs == null) return true;
        if (dog.Buffs.GetCustomVar("onMission") == 1f) return false;
        if (dog.Buffs.GetCustomVar("$FR_NPC_Respawn") == 1f) return false;
        if (dog.Buffs.GetCustomVar("$FR_NPC_Hidden") == 1f) return false;
        return true;
    }

    private static bool IncapacitatedForMovement(EntityRebirthDogCompanion dog)
    {
        if (dog == null) return true;
        try
        {
            if (dog.emodel != null && dog.emodel.IsRagdollActive) return true;
            if (dog.sleepingOrWakingUp) return true;
            if (dog.bodyDamage.CurrentStun != EnumEntityStunType.None) return true;
            if (dog.Jumping && !dog.isSwimming) return true;
        }
        catch { }
        return false;
    }

    private static bool StandStill(EntityRebirthDogCompanion dog)
    {
        return dog != null && dog.Buffs != null && dog.Buffs.HasBuff("FuriousRamsayStandStill");
    }

    private static bool AttackHalted(EntityRebirthDogCompanion dog, RebirthDogPersistentRecordView record)
    {
        if (dog == null || record == null) return true;
        if (record.AttackStopped) return true;
        if (dog.Buffs == null) return false;
        return dog.Buffs.HasBuff("FuriousRamsayBuffPauseAttack") ||
               dog.Buffs.HasBuff("buffNPCModStopAttacking") ||
               dog.Buffs.HasBuff("FuriousRamsayStandStill");
    }

    private static void SynchronizeLegacyBehaviorBuffs(EntityRebirthDogCompanion dog, RebirthDogPersistentRecordView record)
    {
        if (dog == null || dog.Buffs == null || record == null) return;
        try
        {
            SyncBuff(dog, "buffNPCModFullControlMode", record.CombatMode == RebirthCompanionBehaviorMode.FullControl);
            SyncBuff(dog, "buffNPCModThreatControlMode", record.CombatMode == RebirthCompanionBehaviorMode.Hunting);
            SyncBuff(dog, "buffNPCModStopAttacking", record.AttackStopped);

            // Maintain the legacy CVar projection for dialog/buff compatibility.
            // The modern aggregate remains the authority; these values are never
            // read back into persistent state.
            int legacyOrder = 0;
            RebirthNpcRuntimeState runtime = dog.RebirthRuntimeState;
            if (record.OfflineParked) legacyOrder = 10; // EntityUtilities.Orders.TempStay
            else if (runtime != null)
            {
                if (runtime.Order == RebirthNpcOrderState.Follow) legacyOrder = 1;
                else if (runtime.Order == RebirthNpcOrderState.Stay) legacyOrder = 2;
                else if (runtime.Order == RebirthNpcOrderState.Guard) legacyOrder = 9;
            }
            dog.Buffs.SetCustomVar("CurrentOrder", legacyOrder);
            EntityPlayer owner = runtime != null ? ResolveOwner(dog.world, runtime.OwnerId) : null;
            if (owner != null) dog.Buffs.SetCustomVar("$Leader", owner.entityId);
            dog.Buffs.SetCustomVar("$FR_NPC_Respawn", record.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn ? 1f : 0f);
        }
        catch { }
    }

    private static void SyncBuff(EntityRebirthDogCompanion dog, string name, bool wanted)
    {
        if (BuffManager.GetBuff(name) == null) return;
        bool has = dog.Buffs.HasBuff(name);
        if (wanted && !has) dog.Buffs.AddBuff(name);
        else if (!wanted && has) dog.Buffs.RemoveBuff(name);
    }

    private static void MaintainStationaryAnchorLock(EntityRebirthDogCompanion dog, RebirthDogPersistentRecordView dogRecord)
    {
        RebirthNpcRuntimeState state = dog != null ? dog.RebirthRuntimeState : null;
        if (!IsStationaryHold(state, dogRecord)) return;

        // A player-facing Stay is a non-combat hold. Clear both native target channels every
        // tick so base EAI cannot reintroduce retaliation between our slower combat ticks.
        dog.SetAttackTarget(null, 0);
        dog.SetRevengeTarget(null);

        Vector3? anchor = state.Order == RebirthNpcOrderState.Stay
            ? dogRecord.StayPosition
            : (state.HasGuardPosition ? (Vector3?)state.GuardPosition : null);
        if (!anchor.HasValue || CenterBlockPosition(dog.position) != CenterBlockPosition(anchor.Value)) return;
        StopMovement(dog, "stationary anchor lock");
    }

    private static void TickMovement(EntityRebirthDogCompanion dog, RuntimeCache c,
        RebirthDogPersistentRecordView dogRecord, long now)
    {
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        if (state == null || !LifecycleAvailable(dog, dogRecord)) { StopMovement(dog, "dog unavailable"); return; }

        if ((dog.position - c.LastPosition).sqrMagnitude > 0.35f * 0.35f)
        {
            c.LastPosition = dog.position;
            c.LastMovedTicks = now;
        }

        EntityPlayer owner = ResolveOwner(dog.world, state.OwnerId);

        // 2.6 TempStay semantics: a following dog whose owner is offline parks at the
        // current location and defends only the immediate area. The persisted Follow
        // order is retained so reconnect restores behavior without inventing a new order.
        if (state.OwnershipKind == RebirthNpcOwnershipKind.Player && owner == null)
        {
            if (state.Order == RebirthNpcOrderState.Follow)
            {
                Vector3 park = dogRecord.OfflineParkPosition ?? dog.position;
                if (!dogRecord.OfflineParked) EnsureOfflineParked(state.StableId, park);

                // TempStay still defends its immediate area. Do not continuously
                // clear the native ApproachAndAttackTarget path while a valid target
                // exists; once combat ends, return to the exact parked position.
                if (dog.GetAttackTarget() == null)
                {
                    if ((dog.position - park).sqrMagnitude > StayReturnDistance * StayReturnDistance &&
                        !IncapacitatedForMovement(dog))
                        RequestDogMove(dog, park);
                    else
                        StopMovement(dog, "owner offline TempStay");
                }
            }
            return;
        }

        if (owner != null && dogRecord.OfflineParked)
            ClearOfflineParked(state.StableId);

        if (owner != null && owner.AttachedToEntity != null)
        {
            StopMovement(dog, "owner mounted/attached");
            return;
        }

        if (IncapacitatedForMovement(dog))
        {
            StopMovement(dog, "dog incapacitated");
            return;
        }

        // Follow is intentionally NOT halted by StopAttacking/PauseAttack. This is a
        // production 2.6 distinction. Only StandStill blocks owner-follow.
        if (StandStill(dog))
        {
            dog.SetRevengeTarget(null);
            dog.SetAttackTarget(null, 0);
            StopMovement(dog, "stand-still");
            return;
        }

        // Follow owns proximity to the player even if combat is currently active. If the
        // player gets more than 35 m away, Recall immediately instead of letting a target
        // pursuit strand the companion behind. Recall is transient and preserves Follow.
        if (state.Order == RebirthNpcOrderState.Follow && owner != null &&
            Vector3.Distance(dog.position, owner.position) > RebirthCompanionService.AutomaticFollowRecallDistance)
        {
            if (Recall(dog, owner))
            {
                c.LastPosition = owner.position;
                c.LastMovedTicks = now;
                c.NextRecoveryTicks = now + TimeSpan.FromSeconds(2).Ticks;
                return;
            }
        }

        bool stationaryHold = IsStationaryHold(state, dogRecord);
        if (stationaryHold)
        {
            // Stay means stay, not guard-in-place. Both player-facing Stay commands suppress
            // combat completely while still allowing the second variant to walk back to the
            // owner's captured anchor before holding there.
            dog.SetAttackTarget(null, 0);
            dog.SetRevengeTarget(null);
            if (state.Order == RebirthNpcOrderState.Stay)
                TickStay(dog, dogRecord);
            else if (state.HasGuardPosition)
                TickGuard(dog, state.GuardPosition);
            else
                StopMovement(dog, "stationary hold without anchor");
            return;
        }

        EntityAlive attackTarget = dog.GetAttackTarget();
        if (attackTarget != null)
        {
            bool dogSees;
            bool ownerSees;
            bool retaliationTarget = dog.GetRevengeTarget() == attackTarget;
            if (CanUseCombatTarget(dog, owner, attackTarget, state, dogRecord, retaliationTarget,
                out dogSees, out ownerSees))
            {
                TickMoveToAttackTarget(dog, owner, attackTarget, c, now, dogSees, ownerSees);
                return;
            }

            // Follow is the default responsibility. A stale/out-of-envelope/occluded target
            // must not hold the movement task hostage while vanilla ApproachAndAttackTarget
            // keeps a path alive behind our policy layer.
            DropCombatTarget(dog, c, true, "target no longer visible/in combat envelope");
        }

        if (state.Order == RebirthNpcOrderState.Follow && owner != null)
        {
            TickFollow(dog, owner, c, now);
            return;
        }

        if (state.Order == RebirthNpcOrderState.Guard && state.HasGuardPosition)
        {
            TickGuard(dog, state.GuardPosition);
            return;
        }

        // Unowned/hireable dogs retain the old idle wander fallback.
        if (state.OwnershipKind == RebirthNpcOwnershipKind.None)
            TickWander(dog, c, now);
    }

    private static void TickFollow(EntityRebirthDogCompanion dog, EntityPlayer owner, RuntimeCache c, long now)
    {
        float distance = Vector3.Distance(dog.position, owner.position);
        if (distance <= FollowStopDistance)
        {
            StopMovement(dog, "inside follow distance");
            // Live-update facing owns idle Follow rotation. Calling RotateTo here only
            // every 250 ms produced visible 8-degree steps in 3.1.
            return;
        }

        bool blocked = false;
        try { blocked = dog.moveHelper != null && dog.moveHelper.BlockedTime > 3f; } catch { }
        bool noProgress = distance > 8f && now - c.LastMovedTicks > TimeSpan.FromSeconds(3).Ticks;
        if ((blocked || noProgress) && now >= c.NextRecoveryTicks)
        {
            c.NextRecoveryTicks = now + TimeSpan.FromSeconds(8).Ticks;
            Vector3 safe;
            if (TryFindSafeNear(dog.world, owner.position + owner.transform.forward * 2f, out safe) ||
                TryFindSafeNear(dog.world, owner.position + owner.transform.right * 2f, out safe))
            {
                if (TryRelocateLiveDog(dog, safe))
                {
                    StopMovement(dog, "follow stuck recovery");
                    RebirthDogStateService.CaptureRuntime(dog);
                    c.LastPosition = safe;
                    c.LastMovedTicks = now;
                    return;
                }
            }
        }

        Face(dog, owner.position);
        RequestDogMoveTracked(dog, owner.position, owner.motion, c, now);
    }

    /// <summary>
    /// Returns the center of the world block containing <paramref name="position"/>.
    /// This reproduces the 2.6 companion CenterPosition behavior without depending
    /// on the legacy RebirthUtilities helper, which is not present in the 3.1 branch.
    /// X/Z are centered on the block and Y is floored to the block level.
    /// </summary>
    public static Vector3 CenterBlockPosition(Vector3 position)
    {
        return new Vector3(
            Mathf.FloorToInt(position.x) + 0.5f,
            Mathf.FloorToInt(position.y),
            Mathf.FloorToInt(position.z) + 0.5f);
    }

    private const double MovementTraceDurationSeconds = 12.0;
    private const double MovementTraceSampleSeconds = 0.25;

    private sealed class MovementTrace
    {
        public int EntityId;
        public long EndTicks;
        public long NextSampleTicks;
        public int Samples;
        public string RequestedBy;
        public bool HasPrevious;
        public Vector3 PreviousPosition;
        public float PreviousYaw;
    }

    private static readonly object MovementTraceSync = new object();
    private static MovementTrace ActiveMovementTrace;

    /// <summary>
    /// Resolves the final 2.6 Follow/Stay/Guard facing with the engine-native RotateTo
    /// contract. In 2.6 the values 360/360 were per-call rotation clamps, not degrees per
    /// second. Multiplying 360 by Time.deltaTime in the earlier 3.1 port made the dog turn
    /// roughly sixty times more slowly than the authored behavior.
    /// </summary>
    public static void TickSmoothStationaryFacing(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null || dog.IsDead()) return;
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        if (dog.GetAttackTarget() != null) return;

        Vector3 lookPoint;

        if (state.Order == RebirthNpcOrderState.Follow)
        {
            EntityPlayer owner = ResolveOwner(dog.world, state.OwnerId);
            if (owner == null || Vector3.Distance(dog.position, owner.position) > FollowStopDistance) return;
            lookPoint = owner.position;
        }
        else if (state.Order == RebirthNpcOrderState.Stay || state.Order == RebirthNpcOrderState.Guard)
        {
            RebirthNpcPersistentRecordView record;
            RebirthDogPersistentRecordView dogRecord;
            if (!RebirthDogStateService.TryGetView(state.StableId, out record, out dogRecord) ||
                record == null || record.Transform == null || !record.Transform.AnchorRotation.HasValue) return;

            Vector3 anchor;
            if (state.Order == RebirthNpcOrderState.Stay)
            {
                if (dogRecord == null || !dogRecord.StayPosition.HasValue) return;
                anchor = dogRecord.StayPosition.Value;
            }
            else
            {
                if (!state.HasGuardPosition) return;
                anchor = state.GuardPosition;
            }

            // Position recovery owns rotation until the dog is back in the captured block.
            if (CenterBlockPosition(dog.position) != CenterBlockPosition(anchor)) return;

            float targetYaw = Mathf.Repeat(record.Transform.AnchorRotation.Value, 360f);
            float radians = targetYaw * Mathf.Deg2Rad;
            lookPoint = dog.position + new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * 4f;
        }
        else
        {
            return;
        }

        Vector3 planarLook = lookPoint - dog.position;
        planarLook.y = 0f;
        // RotateTo has no stable facing solution when the owner and dog occupy nearly the
        // same X/Z point (for example immediately after Recall). Avoid the rapid spin loop.
        if (planarLook.sqrMagnitude < 0.75f * 0.75f) return;

        dog.SetLookPosition(lookPoint);
        dog.RotateTo(lookPoint.x, lookPoint.y, lookPoint.z, 360f, 360f);
    }

    public static bool StartMovementDebug(EntityPlayer player, int requestedDogEntityId, out string reason)
    {
        reason = string.Empty;
        if (player == null || player.world == null)
        {
            reason = "[REBIRTH Dog Movement] player/world unavailable.";
            return false;
        }

        string ownerId;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(player, out ownerId) || string.IsNullOrWhiteSpace(ownerId))
        {
            reason = "[REBIRTH Dog Movement] could not resolve persistent owner id.";
            return false;
        }

        EntityRebirthDogCompanion selected = null;
        if (requestedDogEntityId > 0)
        {
            selected = player.world.GetEntity(requestedDogEntityId) as EntityRebirthDogCompanion;
            if (selected == null || selected.RebirthRuntimeState == null ||
                !string.Equals(selected.RebirthRuntimeState.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase))
            {
                reason = "[REBIRTH Dog Movement] requested entity is not a loaded dog owned by this player.";
                return false;
            }
        }
        else
        {
            float bestDistanceSq = float.MaxValue;
            RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
            for (int i = 0; i < states.Length; i++)
            {
                RebirthNpcRuntimeState state = states[i];
                if (state == null ||
                    !string.Equals(state.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase))
                    continue;

                int entityId;
                if (!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out entityId)) continue;
                EntityRebirthDogCompanion candidate = player.world.GetEntity(entityId) as EntityRebirthDogCompanion;
                if (candidate == null || candidate.IsDead()) continue;
                float d = (candidate.position - player.position).sqrMagnitude;
                if (d < bestDistanceSq)
                {
                    bestDistanceSq = d;
                    selected = candidate;
                }
            }
        }

        if (selected == null)
        {
            reason = "[REBIRTH Dog Movement] no loaded owned dog was found.";
            return false;
        }

        long now = DateTime.UtcNow.Ticks;
        lock (MovementTraceSync)
        {
            ActiveMovementTrace = new MovementTrace
            {
                EntityId = selected.entityId,
                EndTicks = now + TimeSpan.FromSeconds(MovementTraceDurationSeconds).Ticks,
                NextSampleTicks = now,
                Samples = 0,
                RequestedBy = ownerId
            };
        }

        reason = "[REBIRTH Dog Movement] tracing entity=" + selected.entityId +
                 " name=\"" + (selected.EntityName ?? string.Empty) + "\" stableId=" +
                 selected.RebirthRuntimeState.StableId + " for " +
                 MovementTraceDurationSeconds.ToString("0", CultureInfo.InvariantCulture) +
                 "s at " + MovementTraceSampleSeconds.ToString("0.00", CultureInfo.InvariantCulture) +
                 "s intervals. Issue Stay/Stay-At-Owner during this capture.";
        return true;
    }

    public static void TickMovementDebug(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null) return;

        MovementTrace trace;
        long now = DateTime.UtcNow.Ticks;
        lock (MovementTraceSync)
        {
            trace = ActiveMovementTrace;
            if (trace == null || trace.EntityId != dog.entityId) return;
            if (now >= trace.EndTicks)
            {
                ActiveMovementTrace = null;
                Log.Out("[REBIRTH Dog Movement] COMPLETE entity=" + dog.entityId +
                        " samples=" + trace.Samples + " finalPos=" + FormatMovementVector(dog.position) +
                        " finalYaw=" + FormatMovementFloat(dog.rotation.y));
                return;
            }
            if (now < trace.NextSampleTicks) return;
            trace.NextSampleTicks = now + TimeSpan.FromSeconds(MovementTraceSampleSeconds).Ticks;
            trace.Samples++;
        }

        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        RebirthNpcPersistentRecordView record;
        RebirthDogPersistentRecordView dogRecord;
        RebirthDogStateService.TryGetView(state.StableId, out record, out dogRecord);

        bool hasAnchor = false;
        Vector3 anchor = Vector3.zero;
        if (state.Order == RebirthNpcOrderState.Stay && dogRecord != null && dogRecord.StayPosition.HasValue)
        {
            anchor = dogRecord.StayPosition.Value;
            hasAnchor = true;
        }
        else if (state.Order == RebirthNpcOrderState.Guard && state.HasGuardPosition)
        {
            anchor = state.GuardPosition;
            hasAnchor = true;
        }

        float? desiredYaw = record != null && record.Transform != null
            ? record.Transform.AnchorRotation
            : null;
        float yawDelta = desiredYaw.HasValue
            ? Mathf.DeltaAngle(dog.rotation.y, desiredYaw.Value)
            : 0f;

        EntityAlive attack = dog.GetAttackTarget();
        EntityAlive revenge = dog.GetRevengeTarget();
        float blockedTime = 0f;
        try { blockedTime = dog.moveHelper != null ? dog.moveHelper.BlockedTime : 0f; } catch { }

        RuntimeCache runtimeCache = GetCache(dog.entityId, dog.position, now);
        EntityPlayer owner = ResolveOwner(dog.world, state.OwnerId);
        float ownerDistance = owner != null ? Vector3.Distance(dog.position, owner.position) : -1f;
        float combatRadius = dogRecord != null ? GetBaseAcquisitionDistance(state, dogRecord) : -1f;
        float attackDistance = attack != null ? Vector3.Distance(dog.position, attack.position) : -1f;
        float ownerAttackDistance = attack != null && owner != null ? Vector3.Distance(owner.position, attack.position) : -1f;
        bool attackDogLos = attack != null && SafeCanSee(dog, attack);
        bool attackOwnerNativeLos = false;
        bool attackOwnerBodyLos = false;
        bool attackOwnerLos = attack != null && owner != null &&
            SafeOwnerCanSeeTarget(owner, attack, out attackOwnerNativeLos, out attackOwnerBodyLos);
        float movedSinceSample = trace.HasPrevious ? Vector3.Distance(trace.PreviousPosition, dog.position) : 0f;
        float yawStep = trace.HasPrevious ? Mathf.DeltaAngle(trace.PreviousYaw, dog.rotation.y) : 0f;
        trace.PreviousPosition = dog.position;
        trace.PreviousYaw = dog.rotation.y;
        trace.HasPrevious = true;
        double pathAgeMs = runtimeCache.LastPathRequestTicks > 0
            ? TimeSpan.FromTicks(Math.Max(0L, now - runtimeCache.LastPathRequestTicks)).TotalMilliseconds
            : -1.0;

        Log.Out("[REBIRTH Dog Movement] sample=" + trace.Samples +
                " entity=" + dog.entityId +
                " stableId=" + state.StableId +
                " order=" + state.Order +
                " pos=" + FormatMovementVector(dog.position) +
                " moved=" + FormatMovementFloat(movedSinceSample) +
                " yaw=" + FormatMovementFloat(dog.rotation.y) +
                " yawStep=" + FormatMovementFloat(yawStep) +
                " ownerPos=" + (owner != null ? FormatMovementVector(owner.position) : "<offline>") +
                " ownerDist=" + (owner != null ? FormatMovementFloat(ownerDistance) : "<offline>") +
                " combatMode=" + (dogRecord != null ? dogRecord.CombatMode.ToString() : "<none>") +
                " combatRadius=" + (combatRadius >= 0f ? FormatMovementFloat(combatRadius) : "-") +
                " attackDist=" + (attackDistance >= 0f ? FormatMovementFloat(attackDistance) : "-") +
                " ownerAttackDist=" + (ownerAttackDistance >= 0f ? FormatMovementFloat(ownerAttackDistance) : "-") +
                " dogLOS=" + (attack != null ? attackDogLos.ToString() : "-") +
                " ownerLOS=" + (attack != null && owner != null ? attackOwnerLos.ToString() : "-") +
                " ownerNativeLOS=" + (attack != null && owner != null ? attackOwnerNativeLos.ToString() : "-") +
                " ownerBodyLOS=" + (attack != null && owner != null ? attackOwnerBodyLos.ToString() : "-") +
                " decision=\"" + (runtimeCache.LastMovementDecision ?? string.Empty) + "\"" +
                " pathDest=" + (runtimeCache.HasPathDestination ? FormatMovementVector(runtimeCache.LastPathDestination) : "<none>") +
                " requestedDest=" + (runtimeCache.LastPathRequestTicks > 0 ? FormatMovementVector(runtimeCache.LastRequestedDestination) : "<none>") +
                " pathAgeMs=" + (pathAgeMs >= 0 ? pathAgeMs.ToString("0", CultureInfo.InvariantCulture) : "-") +
                " pathRequests=" + runtimeCache.PathRequests +
                " pathSkips=" + runtimeCache.PathSkips +
                " anchor=" + (hasAnchor ? FormatMovementVector(anchor) : "<none>") +
                " anchorYaw=" + (desiredYaw.HasValue ? FormatMovementFloat(desiredYaw.Value) : "<none>") +
                " yawDelta=" + (desiredYaw.HasValue ? FormatMovementFloat(yawDelta) : "<none>") +
                " motion=" + FormatMovementVector(dog.motion) +
                " blocked=" + FormatMovementFloat(blockedTime) +
                " attack=" + (attack != null ? attack.entityId.ToString(CultureInfo.InvariantCulture) : "-") +
                " revenge=" + (revenge != null ? revenge.entityId.ToString(CultureInfo.InvariantCulture) : "-"));
    }

    private static string FormatMovementVector(Vector3 value)
    {
        return "(" + FormatMovementFloat(value.x) + "," + FormatMovementFloat(value.y) + "," +
               FormatMovementFloat(value.z) + ")";
    }

    private static string FormatMovementFloat(float value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static void TickStay(EntityRebirthDogCompanion dog, RebirthDogPersistentRecordView dogRecord)
    {
        if (!dogRecord.StayPosition.HasValue)
        {
            RebirthNpcStableId id = dog.RebirthRuntimeState.StableId;
            RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
            {
                if (r != null && r.Dog != null)
                {
                    r.Dog.StayPosition = dog.position;
                    unchecked { r.Dog.Revision++; }
                }
            });
            StopMovement(dog, "stay position captured");
            return;
        }

        // 2.6 parity: the legacy companion EAI compared the block-centered current
        // position to the block-centered saved guard position. A dog displaced into a
        // different block was ordered back; there was no 6 m free-roam stay radius.
        Vector3 home = CenterBlockPosition(dogRecord.StayPosition.Value);
        Vector3 current = CenterBlockPosition(dog.position);
        if (current == home)
        {
            StopMovement(dog, "inside stay block");
            return;
        }
        RequestDogMove(dog, home);
    }

    private static void TickGuard(EntityRebirthDogCompanion dog, Vector3 guardPosition)
    {
        // Guard returns to its captured anchor whenever it has no valid combat target.
        // The second 2.6 Stay command also uses this anchor internally, but is marked as
        // a stationary hold and therefore never leaves the block to pursue a target.
        Vector3 home = CenterBlockPosition(guardPosition);
        Vector3 current = CenterBlockPosition(dog.position);
        if (current == home)
        {
            StopMovement(dog, "inside guard block");
            return;
        }
        RequestDogMove(dog, home);
    }

    private static bool IsStationaryHold(RebirthNpcRuntimeState state, RebirthDogPersistentRecordView dogRecord)
    {
        if (state == null || dogRecord == null) return false;
        if (state.Order == RebirthNpcOrderState.Stay) return true;
        return state.Order == RebirthNpcOrderState.Guard && dogRecord.GuardIsStationaryStay;
    }

    private static bool WithinGuardLeash(Vector3 guardPosition, Vector3 targetPosition)
    {
        Vector2 delta = new Vector2(targetPosition.x - guardPosition.x, targetPosition.z - guardPosition.z);
        return delta.sqrMagnitude <= GuardRadius * GuardRadius;
    }

    private static void TickMoveToAttackTarget(EntityRebirthDogCompanion dog, EntityPlayer owner,
        EntityAlive target, RuntimeCache c, long now, bool dogSees, bool ownerSees)
    {
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        RebirthNpcPersistentRecordView record; RebirthDogPersistentRecordView dogRecord;
        if (state == null || !RebirthDogStateService.TryGetView(state.StableId, out record, out dogRecord) ||
            !LifecycleAvailable(dog, dogRecord) || AttackHalted(dog, dogRecord) ||
            IsStationaryHold(state, dogRecord) ||
            (state.Order != RebirthNpcOrderState.Follow && state.Order != RebirthNpcOrderState.Guard))
            return;

        // The caller already performed the authoritative visibility/range check for this tick.
        // Re-check only when invoked with an invalid sight state so future call sites cannot
        // accidentally resurrect through-wall pursuit.
        if (!dogSees && !ownerSees)
        {
            DropCombatTarget(dog, c, true, "lost dog/owner line of sight");
            return;
        }

        // Exact legacy split: once the dog itself has line of sight, native
        // ApproachAndAttackTarget owns melee approach/action. If only the owner can see the
        // hostile, this movement task supplies the owner-vision pursuit path.
        if (dogSees) return;

        Face(dog, target.position);
        RequestDogMoveTracked(dog, target.position, target.motion, c, now);

        float distance = Vector3.Distance(dog.position, target.position);
        TrackCombatProgress(c, target.entityId, distance, now);
        if (now - c.CombatLastProgressTicks > TimeSpan.FromSeconds(AttackNoProgressTimeoutSeconds).Ticks)
        {
            DropCombatTarget(dog, c, true, "attack target unreachable");
        }
    }

    private static void TickWander(EntityRebirthDogCompanion dog, RuntimeCache c, long now)
    {
        if (now < c.NextWanderTicks) return;
        if (dog.GetAttackTarget() != null) return;
        if (dog.Buffs != null && dog.Buffs.HasBuff("buffTalkingTo")) return;
        try
        {
            if (dog.sleepingOrWakingUp || (dog.bodyDamage.CurrentStun != EnumEntityStunType.None)) return;
            if (dog.GetTicksNoPlayerAdjacent() >= 120) return;
        }
        catch { }

        c.NextWanderTicks = now + TimeSpan.FromSeconds(3.5 + UnityEngine.Random.value * 4.5).Ticks;
        Vector2 offset = UnityEngine.Random.insideUnitCircle * 10f;
        Vector3 destination = dog.position + new Vector3(offset.x, 0f, offset.y);
        try { dog.FindPath(destination, Math.Max(0.05f, dog.moveSpeed), true, null); } catch { }
    }

    private static void TickCombat(EntityRebirthDogCompanion dog, RuntimeCache c,
        RebirthDogPersistentRecordView dogRecord, long now)
    {
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        if (state == null || !LifecycleAvailable(dog, dogRecord) || IncapacitatedForMovement(dog))
        {
            c.LastCombatDecision = "clear: dog unavailable/incapacitated";
            ClearCombat(dog);
            return;
        }

        if (AttackHalted(dog, dogRecord))
        {
            c.LastCombatDecision = "clear: attacking halted";
            ClearCombat(dog);
            return;
        }

        EntityPlayer owner = ResolveOwner(dog.world, state.OwnerId);
        if (state.OwnershipKind == RebirthNpcOwnershipKind.Player && owner == null && !dogRecord.OfflineParked)
        {
            c.LastCombatDecision = "clear: player owner unavailable";
            ClearCombat(dog);
            return;
        }

        bool stationaryHold = IsStationaryHold(state, dogRecord);
        if (stationaryHold)
        {
            // Stay is explicitly non-combat. Do not call ClearCombat here because the second
            // Stay variant may still be navigating back to its captured owner position; only
            // clear combat state and leave positional movement intact.
            dog.SetAttackTarget(null, 0);
            dog.SetRevengeTarget(null);
            ResetCombatTracking(c);
            c.LastCombatDecision = "clear: stationary hold";
            return;
        }

        // Defensive retaliation is independent from proactive acquisition, but it still has
        // to satisfy the same physical visibility and mode/order distance envelope. Being hit
        // does not give a companion permanent x-ray knowledge of the attacker.
        EntityAlive revenge = dog.GetRevengeTarget();
        if (revenge != null)
        {
            bool revengeDogSees;
            bool revengeOwnerSees;
            string revengeReason;
            if (CanUseCombatTarget(dog, owner, revenge, state, dogRecord, true,
                out revengeDogSees, out revengeOwnerSees, out revengeReason))
            {
                c.LastCombatCandidateId = revenge.entityId;
                c.LastCombatCandidateReason = "eligible retaliation";
                c.LastCombatDecision = "use revenge target id=" + revenge.entityId;
                SetCombatTarget(dog, c, revenge, 400, now);
                return;
            }
            c.LastCombatCandidateId = revenge.entityId;
            c.LastCombatCandidateReason = revengeReason;
            c.LastCombatDecision = "drop revenge id=" + revenge.entityId + " reason=" + revengeReason;
            dog.SetRevengeTarget(null);
        }

        // Revalidate an existing target continuously. This was the important 3.0 regression:
        // acquisition checked LOS/range once, then native ApproachAndAttackTarget could keep
        // pursuing that stale target behind blocks or far outside Full Control/Hunting.
        EntityAlive current = dog.GetAttackTarget();
        if (current != null)
        {
            bool currentDogSees;
            bool currentOwnerSees;
            string currentReason;
            bool retaliationTarget = dog.GetRevengeTarget() == current;
            if (!CanUseCombatTarget(dog, owner, current, state, dogRecord, retaliationTarget,
                out currentDogSees, out currentOwnerSees, out currentReason))
            {
                c.LastCombatCandidateId = current.entityId;
                c.LastCombatCandidateReason = currentReason;
                c.LastCombatDecision = "drop current id=" + current.entityId + " reason=" + currentReason;
                DropCombatTarget(dog, c, true, "combat revalidation failed: " + currentReason);
            }
            else
            {
                c.LastCombatCandidateId = current.entityId;
                c.LastCombatCandidateReason = "eligible current target";
                c.LastCombatDecision = "retain current target id=" + current.entityId;
                float distance = Vector3.Distance(dog.position, current.position);
                TrackCombatProgress(c, current.entityId, distance, now);
                if (now - c.CombatLastProgressTicks > TimeSpan.FromSeconds(AttackNoProgressTimeoutSeconds).Ticks)
                {
                    c.LastCombatDecision = "drop current id=" + current.entityId + " reason=attack-timeout";
                    DropCombatTarget(dog, c, true, "attack timeout");
                }
                else
                {
                    return;
                }
            }
        }

        // Production 2.6 searched a broad 100m area and then applied the actual mode policy.
        // Keep the broad candidate gather, but the configured Full Control/Hunting distance is
        // now a hard combat envelope: a target cannot remain valid merely because it was once
        // acquired inside the radius.
        Bounds bounds = new Bounds(dog.position, Vector3.one * 200f);
        List<Entity> entities = dog.world.GetEntitiesInBounds(typeof(EntityAlive), bounds, new List<Entity>());
        entities.Sort(delegate(Entity a, Entity b)
        {
            float da = a == null ? float.MaxValue : (a.position - dog.position).sqrMagnitude;
            float db = b == null ? float.MaxValue : (b.position - dog.position).sqrMagnitude;
            return da.CompareTo(db);
        });

        EntityAlive selected = null;
        c.LastCombatCandidateId = -1;
        c.LastCombatCandidateReason = "no candidates";
        for (int i = 0; i < entities.Count; i++)
        {
            EntityAlive candidate = entities[i] as EntityAlive;
            bool dogSees;
            bool ownerSees;
            string candidateReason;
            bool eligible = CanUseCombatTarget(dog, owner, candidate, state, dogRecord, false,
                out dogSees, out ownerSees, out candidateReason);
            if (candidate != null)
            {
                c.LastCombatCandidateId = candidate.entityId;
                c.LastCombatCandidateReason = candidateReason;
            }
            if (!eligible) continue;

            selected = candidate;
            break;
        }

        if (selected != null)
        {
            c.LastCombatDecision = "select candidate id=" + selected.entityId;
            c.LastCombatCandidateReason = "eligible";
            SetCombatTarget(dog, c, selected, 200, now);
        }
        else
        {
            c.LastCombatDecision = "no eligible candidate";
        }
    }

    private static bool CanUseCombatTarget(EntityRebirthDogCompanion dog, EntityPlayer owner, EntityAlive target,
        RebirthNpcRuntimeState state, RebirthDogPersistentRecordView dogRecord, bool retaliation,
        out bool dogSees, out bool ownerSees)
    {
        string reason;
        return CanUseCombatTarget(dog, owner, target, state, dogRecord, retaliation,
            out dogSees, out ownerSees, out reason);
    }

    private static bool CanUseCombatTarget(EntityRebirthDogCompanion dog, EntityPlayer owner, EntityAlive target,
        RebirthNpcRuntimeState state, RebirthDogPersistentRecordView dogRecord, bool retaliation,
        out bool dogSees, out bool ownerSees, out string reason)
    {
        dogSees = false;
        ownerSees = false;
        reason = "eligible";
        if (dog == null) { reason = "dog-null"; return false; }
        if (target == null) { reason = "target-null"; return false; }
        if (target.IsDead()) { reason = "target-dead"; return false; }
        if (state == null) { reason = "runtime-state-null"; return false; }
        if (dogRecord == null) { reason = "dog-record-null"; return false; }
        if (IsSleepingTarget(target)) { reason = "target-sleeping"; return false; }

        string relationReason;
        if (!RebirthDogCombatRelationService.CanDogTarget(dog, target, owner, retaliation, out relationReason))
        {
            reason = "relation:" + relationReason;
            return false;
        }
        if (IsVerticallyInvalidMeleeTarget(dog, target))
        {
            reason = "vertical-melee-filter";
            return false;
        }

        if (state.Order == RebirthNpcOrderState.Guard &&
            (!state.HasGuardPosition || !WithinGuardLeash(state.GuardPosition, target.position)))
        {
            reason = "outside-guard-leash";
            return false;
        }

        // Apply the cheap owner/dog/guard distance union before any LOS/raycast work. The
        // broad 100 m gather is retained for 2.6 behavior, but out-of-policy candidates do
        // not pay native visibility cost.
        if (!IsInsideCombatEnvelope(dog, owner, target, state, dogRecord))
        {
            reason = "outside-combat-envelope";
            return false;
        }

        // Preserve the 2.6 split: the companion itself still uses CanSee. Owner sight remains
        // the deliberate exception that can validate a hostile the dog cannot currently see.
        //
        // 3.1's native CanEntityBeSeen is head-to-head only. Around corners that can report false
        // while the player can plainly see the target's torso/side. Use the native result first,
        // then a bounded multi-point body visibility fallback that keeps the same player-head
        // origin, view-cone/range limits and Voxel.Raycast obstruction rules. Opaque geometry
        // therefore still blocks sight; this only fixes partially exposed targets.
        dogSees = SafeCanSee(dog, target);
        ownerSees = owner != null && !(target is EntitySupplyCrate) && SafeOwnerCanSeeTarget(owner, target);
        if (!dogSees && !ownerSees)
        {
            reason = "not-visible-to-dog-or-owner";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Exact production-policy snapshot used by the rbdog targeting trace. This deliberately
    /// calls the same CanUseCombatTarget predicate as live acquisition instead of reproducing
    /// the rules in the debug command, so the log tells us why THIS build accepted/rejected
    /// the target at that instant.
    /// </summary>
    public static string GetCombatTargetDebugSnapshot(EntityRebirthDogCompanion dog, EntityPlayer owner, EntityAlive target)
    {
        if (dog == null) return "POLICY{eligible=0 reason=dog-null}";
        RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
        RebirthNpcPersistentRecordView record = null;
        RebirthDogPersistentRecordView dogRecord = null;
        bool gotRecord = state != null && RebirthDogStateService.TryGetView(state.StableId, out record, out dogRecord);
        if (!gotRecord || dogRecord == null) return "POLICY{eligible=0 reason=dog-record-unavailable}";

        bool dogSees;
        bool ownerSees;
        string proactiveReason;
        bool proactive = CanUseCombatTarget(dog, owner, target, state, dogRecord, false,
            out dogSees, out ownerSees, out proactiveReason);

        bool ownerNativeSees = false;
        bool ownerBodySees = false;
        if (owner != null && target != null && !(target is EntitySupplyCrate))
            SafeOwnerCanSeeTarget(owner, target, out ownerNativeSees, out ownerBodySees);

        bool retaliation = dog.GetRevengeTarget() == target;
        bool revengeDogSees = false;
        bool revengeOwnerSees = false;
        string retaliationReason = "not-current-revenge";
        bool retaliationEligible = false;
        if (retaliation)
            retaliationEligible = CanUseCombatTarget(dog, owner, target, state, dogRecord, true,
                out revengeDogSees, out revengeOwnerSees, out retaliationReason);

        float radius = GetBaseAcquisitionDistance(state, dogRecord);
        float dogTarget = target != null ? Vector3.Distance(dog.position, target.position) : -1f;
        float ownerTarget = owner != null && target != null ? Vector3.Distance(owner.position, target.position) : -1f;
        float dogOwner = owner != null ? Vector3.Distance(dog.position, owner.position) : -1f;

        string runtimeDecision = "none";
        int runtimeCandidate = -1;
        string runtimeCandidateReason = "none";
        long nextCombatTicks = 0L;
        lock (Sync)
        {
            RuntimeCache cache;
            if (Cache.TryGetValue(dog.entityId, out cache) && cache != null)
            {
                runtimeDecision = cache.LastCombatDecision ?? "none";
                runtimeCandidate = cache.LastCombatCandidateId;
                runtimeCandidateReason = cache.LastCombatCandidateReason ?? "none";
                nextCombatTicks = cache.NextCombatTicks;
            }
        }

        double nextCombatMs = Math.Max(0d, (nextCombatTicks - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerMillisecond);
        StringBuilder b = new StringBuilder(500);
        b.Append("POLICY{eligible=").Append(proactive ? "1" : "0")
         .Append(" reason=").Append(proactiveReason)
         .Append(" dogLOS=").Append(dogSees ? "1" : "0")
         .Append(" ownerLOS=").Append(ownerSees ? "1" : "0")
         .Append(" ownerNativeLOS=").Append(ownerNativeSees ? "1" : "0")
         .Append(" ownerBodyLOS=").Append(ownerBodySees ? "1" : "0")
         .Append(" retaliation=").Append(retaliation ? "1" : "0")
         .Append(" retaliationEligible=").Append(retaliationEligible ? "1" : "0")
         .Append(" retaliationReason=").Append(retaliationReason)
         .Append(" radius=").Append(radius.ToString("0.00", CultureInfo.InvariantCulture))
         .Append(" dogTarget=").Append(dogTarget.ToString("0.00", CultureInfo.InvariantCulture))
         .Append(" ownerTarget=").Append(ownerTarget.ToString("0.00", CultureInfo.InvariantCulture))
         .Append(" dogOwner=").Append(dogOwner.ToString("0.00", CultureInfo.InvariantCulture))
         .Append(" order=").Append(state != null ? state.Order.ToString() : "<none>")
         .Append(" mode=").Append(dogRecord.CombatMode.ToString())
         .Append(" attackStopped=").Append(dogRecord.AttackStopped ? "1" : "0")
         .Append("} RUNTIME{decision=\"").Append(runtimeDecision).Append("\"")
         .Append(" candidate=").Append(runtimeCandidate)
         .Append(" candidateReason=\"").Append(runtimeCandidateReason).Append("\"")
         .Append(" nextCombatMs=").Append(nextCombatMs.ToString("0", CultureInfo.InvariantCulture)).Append('}');
        return b.ToString();
    }

    private static bool IsInsideCombatEnvelope(EntityRebirthDogCompanion dog, EntityPlayer owner, EntityAlive target,
        RebirthNpcRuntimeState state, RebirthDogPersistentRecordView dogRecord)
    {
        float radius = GetBaseAcquisitionDistance(state, dogRecord);
        float dogTargetDistance = Vector3.Distance(dog.position, target.position);

        if (dogRecord.OfflineParked || owner == null)
            return dogTargetDistance <= radius;

        float ownerTargetDistance = Vector3.Distance(owner.position, target.position);

        // Full Control is the restrained owner-centric mode. While Following, the companion
        // must first remain inside the configured owner envelope, and the hostile itself must
        // also be inside that same envelope. This makes Follow the default responsibility and
        // prevents a visible/stale target from dragging the dog farther away than the option.
        if (state.Order == RebirthNpcOrderState.Follow &&
            dogRecord.CombatMode == RebirthCompanionBehaviorMode.FullControl)
        {
            float dogOwnerDistance = Vector3.Distance(dog.position, owner.position);
            return dogOwnerDistance <= radius && ownerTargetDistance <= radius;
        }

        // Hunting is intentionally more autonomous, but its configured distance is still a
        // real limit. Owner vision may validate a target near the owner, while the dog's own
        // vision may validate one near the dog; neither path may exceed Hunting Distance.
        return dogTargetDistance <= radius || ownerTargetDistance <= radius;
    }

    private static void DropCombatTarget(EntityRebirthDogCompanion dog, RuntimeCache c, bool clearRevenge, string reason)
    {
        if (dog == null) return;
        dog.SetAttackTarget(null, 0);
        if (clearRevenge) dog.SetRevengeTarget(null);
        try
        {
            if (dog.getNavigator() != null) dog.getNavigator().clearPath();
            if (dog.moveHelper != null) dog.moveHelper.Stop();
        }
        catch { }
        if (c != null)
        {
            ResetCombatTracking(c);
            c.LastMovementDecision = "combat dropped: " + (reason ?? string.Empty);
        }
    }

    private static float GetBaseAcquisitionDistance(RebirthNpcRuntimeState state, RebirthDogPersistentRecordView dogRecord)
    {
        if (dogRecord == null) return FullControlRadius;
        if (dogRecord.OfflineParked) return OfflineParkDefenseRadius;
        return dogRecord.CombatMode == RebirthCompanionBehaviorMode.Hunting
            ? HuntingRadius
            : FullControlRadius;
    }

    private static bool IsCandidateAllowed(EntityRebirthDogCompanion dog, EntityAlive candidate,
        EntityPlayer owner, bool retaliation)
    {
        string reason;
        return RebirthDogCombatRelationService.CanDogTarget(dog, candidate, owner, retaliation, out reason);
    }

    private static bool IsSleepingTarget(EntityAlive candidate)
    {
        // Project rule: companions never proactively attack sleepers. Once the target's
        // engine sleeping flag clears after wake-up, normal Full Control/Hunting policy
        // applies immediately. Keep the legacy snake exception because 2.6 explicitly
        // allowed snakes through the sleeping-target gate.
        return candidate != null && candidate.IsSleeping && !(candidate is EntityAnimalSnake);
    }


    private static bool IsVerticallyInvalidMeleeTarget(EntityRebirthDogCompanion dog, EntityAlive candidate)
    {
        if (dog == null || candidate == null || candidate.EntityClass == null) return false;
        try
        {
            if (candidate.HasAnyTags(NoMeleeTrackingTag) && dog.position.y < candidate.position.y - 4f)
                return true;
        }
        catch { }
        return false;
    }

    private static bool SafeCanSee(EntityAlive observer, EntityAlive target)
    {
        if (observer == null || target == null) return false;
        try { return observer.CanSee(target); }
        catch { return false; }
    }

    private static bool SafeCanEntityBeSeen(EntityAlive observer, EntityAlive target)
    {
        if (observer == null || target == null) return false;
        try { return observer.CanEntityBeSeen(target); }
        catch { return false; }
    }

    private static bool SafeOwnerCanSeeTarget(EntityPlayer owner, EntityAlive target)
    {
        bool nativeSeen;
        bool bodySeen;
        return SafeOwnerCanSeeTarget(owner, target, out nativeSeen, out bodySeen);
    }

    private static bool SafeOwnerCanSeeTarget(EntityPlayer owner, EntityAlive target,
        out bool nativeSeen, out bool bodySeen)
    {
        nativeSeen = false;
        bodySeen = false;
        if (owner == null || target == null) return false;

        nativeSeen = SafeCanEntityBeSeen(owner, target);
        if (nativeSeen) return true;

        bodySeen = SafeOwnerCanSeeTargetBody(owner, target);
        return bodySeen;
    }

    private static bool SafeOwnerCanSeeTargetBody(EntityPlayer owner, EntityAlive target)
    {
        if (owner == null || target == null || owner.world == null) return false;

        try
        {
            Bounds bounds = target.boundingBox;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            // Center/upper/lower torso plus four lateral silhouette samples. Sampling remains
            // inside the target bounds so a successful ray must actually reach exposed body space.
            if (OwnerVisibilityRayHitsTarget(owner, target, center)) return true;

            Vector3 upper = center + Vector3.up * (extents.y * 0.45f);
            if (OwnerVisibilityRayHitsTarget(owner, target, upper)) return true;

            Vector3 lower = center - Vector3.up * (extents.y * 0.35f);
            if (OwnerVisibilityRayHitsTarget(owner, target, lower)) return true;

            float x = extents.x * 0.55f;
            if (x > 0.02f)
            {
                if (OwnerVisibilityRayHitsTarget(owner, target, center + Vector3.right * x)) return true;
                if (OwnerVisibilityRayHitsTarget(owner, target, center - Vector3.right * x)) return true;
            }

            float z = extents.z * 0.55f;
            if (z > 0.02f)
            {
                if (OwnerVisibilityRayHitsTarget(owner, target, center + Vector3.forward * z)) return true;
                if (OwnerVisibilityRayHitsTarget(owner, target, center - Vector3.forward * z)) return true;
            }
        }
        catch { }

        return false;
    }

    private static bool OwnerVisibilityRayHitsTarget(EntityPlayer owner, EntityAlive target, Vector3 point)
    {
        Vector3 origin = owner.getHeadPosition();
        Vector3 direction = point - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f) return true;

        float seeDistance;
        try { seeDistance = owner.GetSeeDistance(); }
        catch { seeDistance = FullControlRadius; }

        if (distance > seeDistance) return false;
        try
        {
            if (!owner.IsInViewCone(point)) return false;
        }
        catch { }

        Ray ray = new Ray(origin, direction);
        ray.origin += direction.normalized * -0.1f;

        int modelLayer = owner.GetModelLayer();
        try
        {
            owner.SetModelLayer(2);
            if (!Voxel.Raycast(owner.world, ray, distance + 0.75f, -1612492821, 64, 0f))
                return false;

            Transform hitTransform = Voxel.voxelRayHitInfo.transform;
            if (hitTransform == null) return false;

            string tag = Voxel.voxelRayHitInfo.tag ?? string.Empty;
            if (tag == "E_Vehicle")
            {
                EntityVehicle collisionEntity = EntityVehicle.FindCollisionEntity(hitTransform);
                return collisionEntity != null && collisionEntity.IsAttached(target);
            }

            if (tag.StartsWith("E_BP_", StringComparison.Ordinal))
                hitTransform = GameUtils.GetHitRootTransform(tag, hitTransform);

            return hitTransform == target.transform;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { owner.SetModelLayer(modelLayer); } catch { }
        }
    }

    private static void SetCombatTarget(EntityRebirthDogCompanion dog, RuntimeCache c,
        EntityAlive target, int ticks, long now)
    {
        if (dog == null || target == null) return;
        dog.SetAttackTarget(target, ticks);
        try { dog.ConditionalTriggerSleeperWakeUp(); } catch { }
        float distance = Vector3.Distance(dog.position, target.position);
        c.CombatTargetId = target.entityId;
        c.CombatTargetSinceTicks = now;
        c.CombatLastProgressTicks = now;
        c.CombatLastDistance = distance;
    }

    private static void TrackCombatProgress(RuntimeCache c, int targetId, float distance, long now)
    {
        if (c.CombatTargetId != targetId)
        {
            c.CombatTargetId = targetId;
            c.CombatTargetSinceTicks = now;
            c.CombatLastProgressTicks = now;
            c.CombatLastDistance = distance;
            return;
        }
        if (distance + AttackProgressEpsilon < c.CombatLastDistance)
        {
            c.CombatLastDistance = distance;
            c.CombatLastProgressTicks = now;
        }
    }

    private static void ResetCombatTracking(RuntimeCache c)
    {
        c.CombatTargetId = -1;
        c.CombatTargetSinceTicks = 0L;
        c.CombatLastProgressTicks = 0L;
        c.CombatLastDistance = float.MaxValue;
    }

    private static void RequestDogMoveTracked(
        EntityRebirthDogCompanion dog,
        Vector3 destination,
        Vector3 targetVelocity,
        RuntimeCache cache,
        long now)
    {
        if (dog == null || dog.IsDead() || cache == null) return;

        // Modern equivalent of the 2.6 relocate/pathCounter + target-velocity pathing.
        // Keep focus current every movement tick, but avoid submitting a fresh path
        // every 250 ms unless the moving target has materially changed position.
        Vector3 predicted = destination;
        Vector3 planarVelocity = targetVelocity;
        planarVelocity.y = 0f;
        if (planarVelocity.sqrMagnitude > 0.01f)
            predicted += Vector3.ClampMagnitude(planarVelocity, 8f) * 0.35f;

        // Preserve the old bounded seek-offset escape behavior for large downward
        // path transitions without reviving the obsolete EAI state machine.
        if (predicted.y - dog.position.y < -8f)
        {
            if (cache.SeekOffset == Vector3.zero || UnityEngine.Random.value < 0.2f)
            {
                cache.SeekOffset.x = Mathf.Clamp(cache.SeekOffset.x + UnityEngine.Random.Range(-0.3f, 0.3f), -1.5f, 1.5f);
                cache.SeekOffset.z = Mathf.Clamp(cache.SeekOffset.z + UnityEngine.Random.Range(-0.3f, 0.3f), -1.5f, 1.5f);
            }
            predicted += cache.SeekOffset;
        }
        else
        {
            cache.SeekOffset = Vector3.zero;
        }

        try { if (dog.moveHelper != null) dog.moveHelper.SetFocusPos(destination); } catch { }

        bool destinationChanged = !cache.HasPathDestination ||
                                  (predicted - cache.LastPathDestination).sqrMagnitude > 1.5f * 1.5f;
        if (!destinationChanged && now < cache.NextPathRequestTicks)
        {
            cache.PathSkips++;
            cache.LastMovementDecision = "path-held: destination unchanged";
            return;
        }

        cache.HasPathDestination = true;
        cache.LastPathDestination = predicted;
        cache.NextPathRequestTicks = now + TimeSpan.FromMilliseconds(750).Ticks;
        cache.LastMovementDecision = destinationChanged ? "path-request: destination changed" : "path-request: refresh interval";
        RequestDogMove(dog, predicted);
    }

    private static void RequestDogMove(EntityRebirthDogCompanion dog, Vector3 destination)
    {
        if (dog == null || dog.IsDead()) return;
        lock (Sync)
        {
            RuntimeCache cache;
            if (Cache.TryGetValue(dog.entityId, out cache) && cache != null)
            {
                cache.PathRequests++;
                cache.LastPathRequestTicks = DateTime.UtcNow.Ticks;
                cache.LastRequestedDestination = destination;
                cache.HasPathDestination = true;
                cache.LastPathDestination = destination;
                if (string.IsNullOrEmpty(cache.LastMovementDecision) || cache.LastMovementDecision == "none")
                    cache.LastMovementDecision = "path-request";
            }
        }
        try
        {
            // Production 2.6 companion Follow/MoveToTarget both requested the
            // animal's aggro movement speed. Keep that behavioral contract instead
            // of routing dogs through the generic human NPC adapter's base moveSpeed.
            dog.FindPath(destination, dog.GetMoveSpeedAggro(), true, null);
        }
        catch
        {
            try { if (dog.moveHelper != null) dog.moveHelper.SetMoveTo(destination, true); } catch { }
        }
    }

    private static void Face(EntityRebirthDogCompanion dog, Vector3 position)
    {
        try
        {
            dog.SetLookPosition(position);
            dog.RotateTo(position.x, position.y + 2f, position.z, 8f, 8f);
        }
        catch { }
    }

    private static void StopMovement(EntityRebirthDogCompanion dog, string reason)
    {
        if (dog == null) return;
        lock (Sync)
        {
            RuntimeCache cache;
            if (Cache.TryGetValue(dog.entityId, out cache) && cache != null)
            {
                cache.LastMovementDecision = "stop: " + (reason ?? string.Empty);
                cache.HasPathDestination = false;
            }
        }
        IRebirthNpcMovementAdapter adapter = RebirthNpcMovementAdapterRegistry.Current;
        if (adapter != null) { adapter.Stop(dog, reason); return; }
        try
        {
            if (dog.getNavigator() != null) dog.getNavigator().clearPath();
            if (dog.moveHelper != null) dog.moveHelper.Stop();
        }
        catch { }
    }

    private static void EnsureOfflineParked(RebirthNpcStableId id, Vector3 position)
    {
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!RebirthDogStateService.IsDog(r)) return;
            if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
            if (r.Dog.OfflineParked) return;
            r.Dog.OfflineParked = true;
            r.Dog.OfflineParkPosition = position;
            unchecked { r.Dog.Revision++; }
        });
    }

    private static void ClearOfflineParked(RebirthNpcStableId id)
    {
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!RebirthDogStateService.IsDog(r) || r.Dog == null || !r.Dog.OfflineParked) return;
            r.Dog.OfflineParked = false;
            r.Dog.OfflineParkPosition = null;
            unchecked { r.Dog.Revision++; }
        });
    }

    private static void TickHealing(EntityRebirthDogCompanion dog, RebirthDogPersistentRecordView dogRecord)
    {
        if (dog == null || dogRecord == null || dogRecord.Lifecycle != RebirthDogLifecycleKind.Active ||
            dog.Health <= 0 || dog.Buffs == null) return;

        // The native loot view mirrors RebirthDogInventoryService while it is open.
        // Do not consume meat behind that locked editor or the two views could race.
        if (RebirthDogStorageService.IsOpenServer(dog.entityId)) return;

        // Exact 2.6 animal-healing policy: do not queue another raw-meat heal while
        // buffHealHealth is already processing, and subtract any medical regeneration
        // already pending before deciding whether another 140-health treatment is needed.
        int missingHealth = dog.GetMaxHealth() - dog.Health + (int)Math.Floor(dog.Stats.Health.MaxModifier);
        bool healing = dog.Buffs.HasBuff("buffHealHealth");
        float pendingHealing = dog.Buffs.GetCustomVar("medicalRegHealthAmount");
        float healingLeft = missingHealth - pendingHealing;
        if (missingHealth <= 0 || healingLeft < 140f || healing) return;

        RebirthNpcStableId id = dog.RebirthRuntimeState.StableId;
        if (!RebirthDogInventoryService.ConsumeOne(id, RebirthDogLifecycleService.HireItemName)) return;
        // 2.6 does not directly add health here. It only queues the same medical
        // regeneration buff used by companion consumables, so recovery happens at the
        // buff's normal rate instead of jumping by 45 HP every poll. The buff is part of
        // REBIRTH's required config and is validated at packaging time.
        dog.Buffs.SetCustomVar("$medicRegHealthIncreaseSpeed", 4f);
        dog.Buffs.AddBuff("FuriousRamsayRawMeatHealTrigger");
        if (BuffManager.GetBuff("FuriousRamsayHealParticle") != null)
            dog.Buffs.AddBuff("FuriousRamsayHealParticle");
        RebirthDogStateService.CaptureRuntime(dog);
    }

    private static void TickAwaitingRespawnHealing(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.Health <= 0 || dog.Buffs == null || dog.RebirthRuntimeState == null) return;
        if (!TryApplyBedrollAoe(dog)) return;
        RebirthDogStateService.CaptureRuntime(dog);
    }

    private static bool TryApplyBedrollAoe(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.Buffs == null || dog.RebirthRuntimeState == null ||
            dog.Buffs.GetCustomVar("$FR_NPC_Respawn") != 1f) return false;
        EntityPlayer owner = ResolveOwner(dog.world, dog.RebirthRuntimeState.OwnerId);
        if (owner == null || owner.SpawnPoints == null || owner.SpawnPoints.Count == 0) return false;
        try
        {
            // EntityPlayerLocal.GetSpawnPoint() is just SpawnPoints[0] plus block-center
            // offset. Use the same authoritative list on the server so this follows the
            // player's CURRENT bed/bedroll spawn rather than a stale persisted bed position.
            SpawnPosition spawnPoint = new SpawnPosition(
                owner.SpawnPoints[0].ToVector3() + new Vector3(0.5f, 0f, 0.5f), 0f);
            if (spawnPoint.IsUndef() || (dog.position - spawnPoint.position).sqrMagnitude >= 9f) return false;
            if (BuffManager.GetBuff("buffBedrollAOEEffect") == null) return false;
            dog.Buffs.AddBuff("buffBedrollAOEEffect");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SyncProgressionFromCvars(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null) return;
        int cvarKills = Math.Max(0, Mathf.RoundToInt(dog.Buffs.GetCustomVar("$varNumKills")));
        RebirthNpcStableId id = dog.RebirthRuntimeState.StableId;
        RebirthNpcPersistentRecordView record; RebirthDogPersistentRecordView state;
        int persisted = RebirthDogStateService.TryGetView(id, out record, out state) ? state.KillCount : 0;
        if (cvarKills <= persisted) return;
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!RebirthDogStateService.IsDog(r)) return;
            if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
            r.Dog.KillCount = cvarKills;
            int level = LevelForKills(cvarKills);
            if (level > r.Dog.Level) r.Dog.Level = level;
            unchecked { r.Dog.Revision++; }
        });
        ApplyProgressionCvars(dog);
    }

    public static void RecordKill(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
        RebirthNpcStableId id = dog.RebirthRuntimeState.StableId;
        RebirthNpcAggregatePersistenceStore.Mutate(id, delegate(RebirthNpcPersistentRecord r)
        {
            if (!RebirthDogStateService.IsDog(r)) return;
            if (r.Dog == null) r.Dog = new RebirthDogPersistentRecord();
            r.Dog.KillCount++;
            int level = LevelForKills(r.Dog.KillCount);
            if (level > r.Dog.Level) r.Dog.Level = level;
            unchecked { r.Dog.Revision++; }
        });
        ApplyProgressionCvars(dog);
    }

    public static void ApplyProgressionCvars(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null) return;
        int level = RebirthDogStateService.GetLevel(dog.RebirthRuntimeState.StableId);
        int kills = RebirthDogStateService.GetKills(dog.RebirthRuntimeState.StableId);
        dog.Buffs.SetCustomVar("$FR_NPC_Level", level);
        dog.Buffs.SetCustomVar("$varNumKills", kills);
        RebirthNpcPersistentRecordView rec; RebirthDogPersistentRecordView dogRec;
        int miningLevel = RebirthDogStateService.TryGetView(dog.RebirthRuntimeState.StableId, out rec, out dogRec) ? dogRec.MiningLevel : 0;
        dog.Buffs.SetCustomVar("$FR_NPC_MiningLevel", miningLevel);
        dog.Buffs.SetCustomVar("NPCCompanionFlag", 1f);
        // The 2.6 dog stat XML keys its health/damage table from _difficulty.
        // EntityNPCRebirth used to keep that compatibility CVar populated; the
        // dedicated 3.1 dog runtime must do it explicitly.
        dog.Buffs.SetCustomVar("_difficulty", GamePrefs.GetInt(EnumGamePrefs.GameDifficulty));
    }

    public static int LevelForKills(int kills)
    {
        if (kills > 2250) return 10;
        if (kills > 1800) return 9;
        if (kills > 1400) return 8;
        if (kills > 1050) return 7;
        if (kills > 750) return 6;
        if (kills > 500) return 5;
        if (kills > 300) return 4;
        if (kills > 150) return 3;
        if (kills > 50) return 2;
        return 1;
    }

    private static EntityPlayer ResolveOwner(World world, string ownerId)
    {
        if (world == null || string.IsNullOrEmpty(ownerId)) return null;
        List<EntityPlayer> players = world.GetPlayers();
        for (int i = 0; players != null && i < players.Count; i++)
        {
            string id;
            if (RebirthDogLifecycleService.TryResolveOwnerId(players[i], out id) &&
                string.Equals(id, ownerId, StringComparison.OrdinalIgnoreCase)) return players[i];
        }
        return null;
    }

    /// <summary>
    /// Resolves a real two-block-high ground position around the owner. StableId + salt rotate
    /// the preferred offset so several recovered dogs do not all materialize in one spot.
    /// </summary>
    public static bool TryFindSafeNearOwner(World world, EntityPlayer owner, RebirthNpcStableId stableId, int salt, out Vector3 safe)
    {
        safe = Vector3.zero;
        if (world == null || owner == null) return false;

        Vector3 forward = owner.transform != null ? owner.transform.forward : Vector3.forward;
        Vector3 right = owner.transform != null ? owner.transform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        if (right.sqrMagnitude < 0.01f) right = Vector3.right;
        forward.Normalize();
        right.Normalize();

        Vector3[] offsets = new[]
        {
            forward * 2.5f,
            right * 2.5f,
            -right * 2.5f,
            -forward * 2.5f,
            forward * 2.5f + right * 2.5f,
            forward * 2.5f - right * 2.5f,
            -forward * 2.5f + right * 2.5f,
            -forward * 2.5f - right * 2.5f,
            forward * 4.0f,
            right * 4.0f,
            -right * 4.0f,
            -forward * 4.0f
        };

        int hash = stableId.GetHashCode() ^ (salt * 397);
        int start = (hash & 0x7fffffff) % offsets.Length;
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 desired = owner.position + offsets[(start + i) % offsets.Length];
            if (!world.IsChunkAreaLoaded(desired)) continue;
            if (TryFindSafeNear(world, desired, out safe) && safe.y > 1f)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Finds the nearest two-block-high supported position at the requested X/Z without
    /// introducing a horizontal offset. Used only as a fall-through correction after recall.
    /// </summary>
    public static bool TryFindSafeAtExactXZ(World world, Vector3 desired, out Vector3 safe)
    {
        safe = desired;
        if (world == null || !world.IsChunkAreaLoaded(desired)) return false;

        int x = Mathf.FloorToInt(desired.x);
        int z = Mathf.FloorToInt(desired.z);
        int baseY = Mathf.FloorToInt(desired.y);
        int[] offsets = { 0, 1, -1, 2, -2, 3, -3, 4, -4, 5, -5, 6, -6, 7, -7, 8, -8, 9, -9, 10, -10, 11, -11, 12, -12 };
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3i at = new Vector3i(x, baseY + offsets[i], z);
            BlockValue feet = world.GetBlock(at);
            BlockValue head = world.GetBlock(at + Vector3i.up);
            BlockValue below = world.GetBlock(at + Vector3i.down);
            if (feet.isair && head.isair && !below.isair)
            {
                safe = new Vector3(desired.x, at.y + 0.05f, desired.z);
                return true;
            }
        }
        return false;
    }

    public static bool TryFindSafeNear(World world, Vector3 desired, out Vector3 safe)
    {
        safe = desired;
        if (world == null) return false;
        Vector3i block = new Vector3i(Mathf.FloorToInt(desired.x), Mathf.FloorToInt(desired.y), Mathf.FloorToInt(desired.z));
        for (int y = 3; y >= -5; y--)
        {
            Vector3i at = new Vector3i(block.x, block.y + y, block.z);
            BlockValue feet = world.GetBlock(at);
            BlockValue head = world.GetBlock(at + Vector3i.up);
            BlockValue below = world.GetBlock(at + Vector3i.down);
            if (feet.isair && head.isair && !below.isair)
            {
                safe = new Vector3(at.x + 0.5f, at.y + 0.05f, at.z + 0.5f);
                return true;
            }
        }
        return false;
    }
}

public static class RebirthDogDiagnostics
{
    public static string GetReport()
    {
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        int dogs = 0, active = 0, picked = 0, awaiting = 0;
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView r = records[i]; if (!RebirthDogStateService.IsDog(r) || r.Dog == null) continue;
            dogs++;
            if (r.Dog.Lifecycle == RebirthDogLifecycleKind.Active) active++;
            else if (r.Dog.Lifecycle == RebirthDogLifecycleKind.PickedUp) picked++;
            else if (r.Dog.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn) awaiting++;
            b.Append("\n  id=").Append(r.Identity.StableNpcId).Append(" breed=").Append(r.Dog.BreedId)
             .Append(" lifecycle=").Append(r.Dog.Lifecycle).Append(" order=").Append(r.Order != null ? r.Order.OrderState : "None")
             .Append(" mode=").Append(r.Dog.CombatMode).Append(" stopped=").Append(r.Dog.AttackStopped)
             .Append(" level=").Append(r.Dog.Level).Append(" kills=").Append(r.Dog.KillCount)
             .Append(" inventoryRev=").Append(r.Inventory != null ? r.Inventory.InventoryRevision : 0);
        }
        return "[REBIRTH Dog] records=" + dogs + " active=" + active + " pickedUp=" + picked + " awaitingRespawn=" + awaiting + b;
    }
}
