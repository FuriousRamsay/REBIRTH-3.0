using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

#nullable disable

public sealed class RebirthNpcPersistenceSchemaState
{
    public string StoreId;
    public string FileName;
    public string FullPath;
    public int CurrentFormat;
    public int LoadedFormat;
    public string LoadSource;
    public bool UpgradePending;
    public long Loads;
    public long CurrentLoads;
    public long LegacyLoads;
    public long UpgradeSnapshots;
    public long RecoveredLoads;
    public long RejectedLoads;
    public string LastIssue;
}

public static class RebirthNpcPersistenceSchemaTelemetry
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthNpcPersistenceSchemaState> States =
        new Dictionary<string, RebirthNpcPersistenceSchemaState>(StringComparer.Ordinal);

    public static void RecordLoad(string storeId, string path, int loadedFormat, int currentFormat, string source)
    {
        // "new" is only an absence observation, never evidence that a previously rejected
        // primary/backup has been repaired (the primary may have been quarantined).
        if (string.Equals(source, "primary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, "backup", StringComparison.OrdinalIgnoreCase))
            RebirthNpcPersistenceFile.VerifiedRead(path);
        lock (Sync)
        {
            RebirthNpcPersistenceSchemaState state = GetOrCreate(storeId, path, currentFormat);
            state.LoadedFormat = loadedFormat;
            state.LoadSource = source ?? string.Empty;
            state.UpgradePending = loadedFormat > 0 && loadedFormat < currentFormat;
            state.Loads++;
            if (state.UpgradePending) state.LegacyLoads++; else state.CurrentLoads++;
            if (string.Equals(source, "backup", StringComparison.Ordinal)) state.RecoveredLoads++;
            state.LastIssue = string.Empty;
        }
    }

    public static void RecordRejected(string storeId, string path, int currentFormat, string issue)
    {
        RebirthNpcPersistenceFile.BlockWrite(path, issue);
        lock (Sync)
        {
            RebirthNpcPersistenceSchemaState state = GetOrCreate(storeId, path, currentFormat);
            state.RejectedLoads++;
            state.LastIssue = issue ?? string.Empty;
        }
    }

    public static void PrepareUpgradeSnapshot(string storeId, string path, int currentFormat)
    {
        lock (Sync)
        {
            RebirthNpcPersistenceSchemaState state = GetOrCreate(storeId, path, currentFormat);
            if (!state.UpgradePending || state.LoadedFormat <= 0 || !File.Exists(path)) return;
            string snapshot = path + ".format" + state.LoadedFormat.ToString(CultureInfo.InvariantCulture) + ".migration.bak";
            if (!File.Exists(snapshot))
            {
                File.Copy(path, snapshot, false);
                state.UpgradeSnapshots++;
            }
            state.UpgradePending = false;
        }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[REBIRTH NPC] Persistence schema telemetry");
            if (States.Count == 0)
            {
                sb.Append("  no persistence stores have been loaded");
                return sb.ToString();
            }
            foreach (KeyValuePair<string, RebirthNpcPersistenceSchemaState> pair in States)
            {
                RebirthNpcPersistenceSchemaState s = pair.Value;
                sb.Append("  ").Append(s.StoreId)
                  .Append(" file=").Append(s.FileName)
                  .Append(" loaded=").Append(s.LoadedFormat)
                  .Append(" current=").Append(s.CurrentFormat)
                  .Append(" source=").Append(string.IsNullOrEmpty(s.LoadSource) ? "none" : s.LoadSource)
                  .Append(" upgradePending=").Append(s.UpgradePending)
                  .Append(" loads=").Append(s.Loads)
                  .Append(" legacy=").Append(s.LegacyLoads)
                  .Append(" recovered=").Append(s.RecoveredLoads)
                  .Append(" rejected=").Append(s.RejectedLoads)
                  .Append(" snapshots=").Append(s.UpgradeSnapshots);
                if (!string.IsNullOrEmpty(s.LastIssue)) sb.Append(" lastIssue=").Append(s.LastIssue);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }
    }


    public static string GetHealthReport()
    {
        lock (Sync)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[REBIRTH NPC] Persistence health");
            if (States.Count == 0)
            {
                sb.Append("  no persistence stores have been loaded");
                return sb.ToString();
            }

            foreach (KeyValuePair<string, RebirthNpcPersistenceSchemaState> pair in States)
            {
                RebirthNpcPersistenceSchemaState state = pair.Value;
                string path = state.FullPath ?? string.Empty;
                bool primary = !string.IsNullOrEmpty(path) && File.Exists(path);
                bool backup = !string.IsNullOrEmpty(path) && File.Exists(path + ".bak");
                int quarantineCount = CountFiles(path, ".corrupt.*");
                int migrationCount = CountFiles(path, ".format*.migration.bak");
                string status = GetHealthStatus(state, primary, backup);

                sb.Append("  ").Append(state.StoreId)
                  .Append(" status=").Append(status)
                  .Append(" primary=").Append(primary)
                  .Append(" backup=").Append(backup)
                  .Append(" quarantined=").Append(quarantineCount)
                  .Append(" migrations=").Append(migrationCount)
                  .Append(" loaded=").Append(state.LoadedFormat)
                  .Append(" current=").Append(state.CurrentFormat)
                  .Append(" source=").Append(string.IsNullOrEmpty(state.LoadSource) ? "none" : state.LoadSource)
                  .Append(" upgradePending=").Append(state.UpgradePending);
                if (!string.IsNullOrEmpty(state.LastIssue)) sb.Append(" issue=").Append(state.LastIssue);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }
    }

    private static string GetHealthStatus(RebirthNpcPersistenceSchemaState state, bool primary, bool backup)
    {
        if (state.RejectedLoads > 0 && !primary && !backup) return "FAILED";
        if (string.Equals(state.LoadSource, "backup", StringComparison.Ordinal)) return "RECOVERED";
        if (state.UpgradePending) return "MIGRATION_PENDING";
        if (!primary && !backup && state.Loads == 0) return "NEW";
        if (!primary && backup) return "BACKUP_ONLY";
        if (primary) return "HEALTHY";
        return "UNKNOWN";
    }

    private static int CountFiles(string path, string suffixPattern)
    {
        try
        {
            if (string.IsNullOrEmpty(path)) return 0;
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return 0;
            string pattern = Path.GetFileName(path) + suffixPattern;
            return Directory.GetFiles(directory, pattern).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static RebirthNpcPersistenceSchemaState GetOrCreate(string storeId, string path, int currentFormat)
    {
        RebirthNpcPersistenceSchemaState state;
        if (!States.TryGetValue(storeId, out state))
        {
            state = new RebirthNpcPersistenceSchemaState { StoreId = storeId ?? "unknown" };
            States[state.StoreId] = state;
        }
        state.FileName = string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path);
        state.FullPath = path ?? string.Empty;
        state.CurrentFormat = currentFormat;
        return state;
    }
}
