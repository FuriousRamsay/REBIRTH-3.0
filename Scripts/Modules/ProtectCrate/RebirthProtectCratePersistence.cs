using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#nullable disable

internal sealed class RebirthProtectCratePersistenceRecord
{
    public int CrateEntityId;
    public bool SpawnsGenerated;
    public int TriggerPlayerEntityId = -1;
    public int TriggerGameStage;
    public int CrateMaxHealth;
    public readonly List<int> ZombieEntityIds = new List<int>();
}

internal static class RebirthProtectCratePersistence
{
    private const uint Magic = 0x43504252U; // RBPC
    private const ushort Version = 1;
    private const float SaveIntervalSeconds = 5f;
    private const int MaximumCrateRecords = 4096;
    private const int MaximumDefendersPerCrate = 256;

    private static readonly Dictionary<int, RebirthProtectCratePersistenceRecord> recordsByCrate
        = new Dictionary<int, RebirthProtectCratePersistenceRecord>();
    private static readonly Dictionary<int, int> crateByDefender
        = new Dictionary<int, int>();

    private static bool dirty;
    private static bool serverActive;
    private static bool persistenceReady;
    private static string persistenceFailure = string.Empty;
    private static float nextSave;

    private static string PathName
    {
        get { return Path.Combine(GameIO.GetSaveGameDir(), "RebirthProtectCrate.dat"); }
    }

    public static void Load()
    {
        Reset();
        serverActive = true;
        persistenceReady = false;

        string path = PathName;
        string backup = path + ".bak";
        if (!File.Exists(path) && !File.Exists(backup))
        {
            persistenceReady = true;
            persistenceFailure = string.Empty;
            nextSave = Time.realtimeSinceStartup + SaveIntervalSeconds;
            return;
        }

        Exception primaryFailure = null;
        if (File.Exists(path))
        {
            try
            {
                LoadFile(path);
                persistenceReady = true;
                persistenceFailure = string.Empty;
                nextSave = Time.realtimeSinceStartup + SaveIntervalSeconds;
                return;
            }
            catch (Exception ex)
            {
                primaryFailure = ex;
                Log.Warning("[REBIRTH ProtectCrate] persistence load failed: " + ex.Message);
                recordsByCrate.Clear();
                crateByDefender.Clear();
            }
        }

        if (File.Exists(backup))
        {
            try
            {
                LoadFile(backup);
                persistenceReady = true;
                persistenceFailure = primaryFailure != null
                    ? "recovered-from-backup-after:" + primaryFailure.GetType().Name
                    : string.Empty;
                nextSave = Time.realtimeSinceStartup + SaveIntervalSeconds;
                return;
            }
            catch (Exception backupEx)
            {
                Log.Warning("[REBIRTH ProtectCrate] backup persistence load failed: " + backupEx.Message);
                recordsByCrate.Clear();
                crateByDefender.Clear();
                persistenceFailure = "primary=" +
                    (primaryFailure != null ? primaryFailure.GetType().Name : "missing") +
                    ";backup=" + backupEx.GetType().Name;
            }
        }
        else
        {
            persistenceFailure = "primary=" +
                (primaryFailure != null ? primaryFailure.GetType().Name : "missing") +
                ";backup=missing";
        }

        // Fail closed. Existing unreadable storage must never be reinterpreted as
        // a brand-new world because doing so can generate a duplicate defence set.
        persistenceReady = false;
        nextSave = Time.realtimeSinceStartup + SaveIntervalSeconds;
    }

    public static void Reset()
    {
        recordsByCrate.Clear();
        crateByDefender.Clear();
        dirty = false;
        serverActive = false;
        persistenceReady = false;
        persistenceFailure = string.Empty;
        nextSave = 0f;
    }

    public static bool CanGenerate(out string reason)
    {
        if (!serverActive)
        {
            reason = "persistence-server-inactive";
            return false;
        }
        if (!persistenceReady)
        {
            reason = "persistence-unavailable:" + (persistenceFailure ?? string.Empty);
            return false;
        }
        reason = null;
        return true;
    }

    public static bool TryReserveGenerated(
        int crateEntityId,
        int triggerPlayerEntityId,
        int triggerGameStage,
        int crateMaxHealth,
        out string reason)
    {
        reason = null;
        if (crateEntityId <= 0 || crateMaxHealth <= 0)
        {
            reason = "reservation-invalid-input";
            return false;
        }
        if (!CanGenerate(out reason))
            return false;

        RebirthProtectCratePersistenceRecord existing;
        bool hadExisting = recordsByCrate.TryGetValue(crateEntityId, out existing);
        if (hadExisting && existing != null && existing.SpawnsGenerated)
        {
            reason = "reservation-already-generated";
            return false;
        }
        if (!hadExisting && recordsByCrate.Count >= MaximumCrateRecords)
        {
            reason = "reservation-crate-cap";
            return false;
        }

        bool dirtyBefore = dirty;
        RebirthProtectCratePersistenceRecord backup = hadExisting && existing != null
            ? CloneRecord(existing)
            : null;
        RebirthProtectCratePersistenceRecord record = hadExisting && existing != null
            ? existing
            : new RebirthProtectCratePersistenceRecord { CrateEntityId = crateEntityId };
        recordsByCrate[crateEntityId] = record;

        record.SpawnsGenerated = true;
        record.TriggerPlayerEntityId = triggerPlayerEntityId;
        record.TriggerGameStage = triggerGameStage;
        record.CrateMaxHealth = crateMaxHealth;
        dirty = true;

        string saveFailure;
        if (TrySaveNow(out saveFailure))
            return true;

        // The durable reservation was not acknowledged. Roll the in-memory view
        // back so callers may retry later, but do not spawn any native defenders.
        if (backup != null)
            recordsByCrate[crateEntityId] = backup;
        else
            recordsByCrate.Remove(crateEntityId);
        RebuildDefenderIndex();
        dirty = dirtyBefore;
        reason = "reservation-save-failed:" + saveFailure;
        return false;
    }

    public static bool TryRestoreCrateState(int crateEntityId, RebirthProtectedCrateState state)
    {
        if (state == null || crateEntityId <= 0) return false;

        RebirthProtectCratePersistenceRecord record;
        if (!recordsByCrate.TryGetValue(crateEntityId, out record)) return false;

        state.Triggered = state.Triggered || record.SpawnsGenerated;
        state.TriggerPlayerEntityId = record.TriggerPlayerEntityId;
        state.TriggerGameStage = record.TriggerGameStage;
        state.CrateMaxHealth = record.CrateMaxHealth;

        for (int i = 0; i < record.ZombieEntityIds.Count; i++)
        {
            int zombieEntityId = record.ZombieEntityIds[i];
            if (zombieEntityId > 0 && !state.ZombieEntityIds.Contains(zombieEntityId))
            {
                state.ZombieEntityIds.Add(zombieEntityId);
            }
        }

        return record.SpawnsGenerated;
    }

    public static int GetCrateForDefender(int zombieEntityId)
    {
        int crateEntityId;
        return zombieEntityId > 0 && crateByDefender.TryGetValue(zombieEntityId, out crateEntityId)
            ? crateEntityId
            : -1;
    }

    public static void RecordGenerated(RebirthProtectedCrateState state)
    {
        if (state == null || state.CrateEntityId <= 0 || !state.Triggered) return;
        if (state.ZombieEntityIds.Count > MaximumDefendersPerCrate)
            throw new InvalidOperationException("Protect Crate defender cap exceeded before persistence.");

        RebirthProtectCratePersistenceRecord record = GetOrCreateRecord(state.CrateEntityId);
        bool changed = !record.SpawnsGenerated
            || record.TriggerPlayerEntityId != state.TriggerPlayerEntityId
            || record.TriggerGameStage != state.TriggerGameStage
            || record.CrateMaxHealth != state.CrateMaxHealth;

        record.SpawnsGenerated = true;
        record.TriggerPlayerEntityId = state.TriggerPlayerEntityId;
        record.TriggerGameStage = state.TriggerGameStage;
        record.CrateMaxHealth = state.CrateMaxHealth;

        for (int i = 0; i < state.ZombieEntityIds.Count; i++)
        {
            int zombieEntityId = state.ZombieEntityIds[i];
            if (zombieEntityId <= 0) continue;
            changed |= AttachDefender(record, zombieEntityId);
        }

        if (changed) dirty = true;
    }

    public static bool RecordDefender(int crateEntityId, int zombieEntityId)
    {
        if (crateEntityId <= 0 || zombieEntityId <= 0) return false;

        RebirthProtectCratePersistenceRecord record = GetOrCreateRecord(crateEntityId);
        bool alreadyPresent = record.ZombieEntityIds.Contains(zombieEntityId);
        if (!alreadyPresent && record.ZombieEntityIds.Count >= MaximumDefendersPerCrate)
        {
            Log.Warning("[REBIRTH ProtectCrate] defender persistence cap reached crate=" + crateEntityId);
            return false;
        }

        bool changed = !record.SpawnsGenerated;
        record.SpawnsGenerated = true;
        changed |= AttachDefender(record, zombieEntityId);
        if (changed) dirty = true;
        return true;
    }

    public static void RemoveDefender(int zombieEntityId)
    {
        int crateEntityId;
        if (zombieEntityId <= 0 || !crateByDefender.TryGetValue(zombieEntityId, out crateEntityId)) return;

        crateByDefender.Remove(zombieEntityId);
        RebirthProtectCratePersistenceRecord record;
        if (recordsByCrate.TryGetValue(crateEntityId, out record))
        {
            record.ZombieEntityIds.Remove(zombieEntityId);
        }
        dirty = true;
    }

    public static void RemoveCrate(int crateEntityId)
    {
        RebirthProtectCratePersistenceRecord record;
        if (crateEntityId <= 0 || !recordsByCrate.TryGetValue(crateEntityId, out record)) return;

        for (int i = 0; i < record.ZombieEntityIds.Count; i++)
        {
            crateByDefender.Remove(record.ZombieEntityIds[i]);
        }

        recordsByCrate.Remove(crateEntityId);
        dirty = true;
    }

    public static void SaveIfDue(World world)
    {
        if (!dirty || world == null || world.IsRemote() || Time.realtimeSinceStartup < nextSave) return;
        string ignored;
        TrySaveNow(out ignored);
    }

    public static void TrySaveNow()
    {
        string ignored;
        TrySaveNow(out ignored);
    }

    public static bool TrySaveNow(out string failure)
    {
        failure = null;
        if (!serverActive)
        {
            failure = "server-inactive";
            return false;
        }
        if (!persistenceReady)
        {
            failure = "persistence-unavailable:" + (persistenceFailure ?? string.Empty);
            return false;
        }
        try
        {
            Save();
            return true;
        }
        catch (Exception ex)
        {
            failure = ex.GetType().Name + ":" + ex.Message;
            Log.Warning("[REBIRTH ProtectCrate] persistence save failed: " + ex.Message);
            nextSave = Time.realtimeSinceStartup + SaveIntervalSeconds;
            return false;
        }
    }

    public static void Save()
    {
        if (!serverActive) return;
        string path = PathName;
        string temp = path + ".tmp";
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        List<RebirthProtectCratePersistenceRecord> records
            = new List<RebirthProtectCratePersistenceRecord>(recordsByCrate.Values);
        records.Sort(delegate(RebirthProtectCratePersistenceRecord left, RebirthProtectCratePersistenceRecord right)
        {
            return left.CrateEntityId.CompareTo(right.CrateEntityId);
        });

        if (records.Count > MaximumCrateRecords)
            throw new InvalidDataException("Protect Crate record cap exceeded.");
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i] == null || records[i].ZombieEntityIds.Count > MaximumDefendersPerCrate)
                throw new InvalidDataException("Protect Crate defender cap exceeded.");
        }

        using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(records.Count);

            for (int i = 0; i < records.Count; i++)
            {
                RebirthProtectCratePersistenceRecord record = records[i];
                writer.Write(record.CrateEntityId);
                writer.Write(record.SpawnsGenerated);
                writer.Write(record.TriggerPlayerEntityId);
                writer.Write(record.TriggerGameStage);
                writer.Write(record.CrateMaxHealth);
                writer.Write(record.ZombieEntityIds.Count);
                for (int j = 0; j < record.ZombieEntityIds.Count; j++)
                {
                    writer.Write(record.ZombieEntityIds[j]);
                }
            }

            writer.Flush();
            stream.Flush(true);
        }

        string publishError;
        if (!RebirthDurableFileCommit.TryPublish(temp, path, out publishError))
            throw new IOException("Protect Crate durable publication failed: " + publishError);

        dirty = false;
        persistenceReady = true;
        persistenceFailure = string.Empty;
        nextSave = Time.realtimeSinceStartup + SaveIntervalSeconds;
    }

    private static void LoadFile(string path)
    {
        using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
        {
            if (reader.ReadUInt32() != Magic)
            {
                throw new InvalidDataException("Invalid Protect Crate persistence file.");
            }

            ushort fileVersion = reader.ReadUInt16();
            if (fileVersion < 1 || fileVersion > Version)
            {
                throw new InvalidDataException("Unsupported Protect Crate persistence version " + fileVersion + ".");
            }

            int recordCount = ReadCount(reader, MaximumCrateRecords, "crate");
            for (int i = 0; i < recordCount; i++)
            {
                RebirthProtectCratePersistenceRecord record = new RebirthProtectCratePersistenceRecord
                {
                    CrateEntityId = reader.ReadInt32(),
                    SpawnsGenerated = reader.ReadBoolean(),
                    TriggerPlayerEntityId = reader.ReadInt32(),
                    TriggerGameStage = reader.ReadInt32(),
                    CrateMaxHealth = reader.ReadInt32()
                };

                if (record.CrateEntityId <= 0)
                {
                    throw new InvalidDataException("Invalid Protect Crate entity id.");
                }

                int defenderCount = ReadCount(reader, MaximumDefendersPerCrate, "defender");
                for (int j = 0; j < defenderCount; j++)
                {
                    int zombieEntityId = reader.ReadInt32();
                    if (zombieEntityId <= 0 || record.ZombieEntityIds.Contains(zombieEntityId)) continue;
                    record.ZombieEntityIds.Add(zombieEntityId);
                    crateByDefender[zombieEntityId] = record.CrateEntityId;
                }

                recordsByCrate[record.CrateEntityId] = record;
            }
        }

        dirty = false;
    }

    private static RebirthProtectCratePersistenceRecord CloneRecord(RebirthProtectCratePersistenceRecord source)
    {
        RebirthProtectCratePersistenceRecord copy = new RebirthProtectCratePersistenceRecord
        {
            CrateEntityId = source.CrateEntityId,
            SpawnsGenerated = source.SpawnsGenerated,
            TriggerPlayerEntityId = source.TriggerPlayerEntityId,
            TriggerGameStage = source.TriggerGameStage,
            CrateMaxHealth = source.CrateMaxHealth
        };
        copy.ZombieEntityIds.AddRange(source.ZombieEntityIds);
        return copy;
    }

    private static void RebuildDefenderIndex()
    {
        crateByDefender.Clear();
        foreach (RebirthProtectCratePersistenceRecord record in recordsByCrate.Values)
        {
            if (record == null) continue;
            for (int i = 0; i < record.ZombieEntityIds.Count; i++)
                crateByDefender[record.ZombieEntityIds[i]] = record.CrateEntityId;
        }
    }

    private static RebirthProtectCratePersistenceRecord GetOrCreateRecord(int crateEntityId)
    {
        RebirthProtectCratePersistenceRecord record;
        if (!recordsByCrate.TryGetValue(crateEntityId, out record))
        {
            if (recordsByCrate.Count >= MaximumCrateRecords)
                throw new InvalidOperationException("Protect Crate record cap reached.");
            record = new RebirthProtectCratePersistenceRecord { CrateEntityId = crateEntityId };
            recordsByCrate[crateEntityId] = record;
            dirty = true;
        }
        return record;
    }

    private static bool AttachDefender(RebirthProtectCratePersistenceRecord record, int zombieEntityId)
    {
        bool changed = false;
        int previousCrateEntityId;
        if (crateByDefender.TryGetValue(zombieEntityId, out previousCrateEntityId)
            && previousCrateEntityId != record.CrateEntityId)
        {
            RebirthProtectCratePersistenceRecord previousRecord;
            if (recordsByCrate.TryGetValue(previousCrateEntityId, out previousRecord))
            {
                previousRecord.ZombieEntityIds.Remove(zombieEntityId);
            }
            changed = true;
        }

        crateByDefender[zombieEntityId] = record.CrateEntityId;
        if (!record.ZombieEntityIds.Contains(zombieEntityId))
        {
            record.ZombieEntityIds.Add(zombieEntityId);
            changed = true;
        }

        return changed;
    }

    private static int ReadCount(BinaryReader reader, int maximum, string label)
    {
        int value = reader.ReadInt32();
        if (value < 0 || value > maximum)
        {
            throw new InvalidDataException("Invalid Protect Crate " + label + " count " + value + ".");
        }
        return value;
    }
}
