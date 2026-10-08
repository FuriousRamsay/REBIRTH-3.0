using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

#nullable disable

public sealed class RebirthNpcWorkRecoveryCheckpoint
{
    public ulong AssignmentId;
    public string ExecutorId;
    public string StableTargetId;
    public RebirthNpcWorkExecutorPhase Phase;
    public float Progress;
    public int RetryCount;
    public long UpdatedUtcTicks;
}

public static class RebirthNpcWorkCheckpointPersistenceStore
{
    private const int FormatVersion = 1;
    private const string StoreId = "npc-work-checkpoints";
    private const string FileName = "RebirthNpcWorkCheckpoints.xml";
    private static readonly object Sync = new object();
    private static readonly List<RebirthNpcWorkRecoveryCheckpoint> Loaded = new List<RebirthNpcWorkRecoveryCheckpoint>();
    private static bool loaded, reconciled;
    private static long nextSaveUtcTicks;
    private static long lastSavedTelemetryRevision = -1L;
    private static long loads, saves, suspendedRecoveries, ignoredTerminal, missingAssignments, rejected;

    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        lock (Sync)
        {
            if (loaded) return;
            loaded = true;
        }
        string path = GetPath();
        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path) && !File.Exists(path + ".bak"))
        {
            RebirthNpcPersistenceSchemaTelemetry.RecordLoad(StoreId, path, FormatVersion, FormatVersion, "new");
            return;
        }
        XmlDocument document;
        string source, error;
        if (!RebirthNpcPersistenceFile.TryLoad(path, Validate, out document, out source, out error))
        {
            rejected++;
            RebirthNpcPersistenceSchemaTelemetry.RecordRejected(StoreId, path, FormatVersion, error);
            return;
        }
        int format = ParseInt(document.DocumentElement.GetAttribute("format"), 1);
        XmlNodeList nodes = document.DocumentElement.SelectNodes("checkpoint");
        lock (Sync)
        {
            Loaded.Clear();
            int start = Math.Max(0, nodes.Count - 512);
            for (int i = start; i < nodes.Count; i++)
            {
                XmlElement e = nodes[i] as XmlElement;
                ulong assignment;
                if (e == null || !ulong.TryParse(e.GetAttribute("assignment"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out assignment) || assignment == 0) continue;
                float progress;
                float.TryParse(e.GetAttribute("progress"), NumberStyles.Float, CultureInfo.InvariantCulture, out progress);
                Loaded.Add(new RebirthNpcWorkRecoveryCheckpoint
                {
                    AssignmentId = assignment,
                    ExecutorId = e.GetAttribute("executor"),
                    StableTargetId = e.GetAttribute("target"),
                    Phase = (RebirthNpcWorkExecutorPhase)ParseInt(e.GetAttribute("phase"), 0),
                    Progress = Math.Max(0f, Math.Min(1f, progress)),
                    RetryCount = Math.Max(0, ParseInt(e.GetAttribute("retries"), 0)),
                    UpdatedUtcTicks = ParseLong(e.GetAttribute("updated"))
                });
            }
        }
        loads++;
        RebirthNpcPersistenceSchemaTelemetry.RecordLoad(StoreId, path, format, FormatVersion, source);
    }

    public static void Tick()
    {
        EnsureLoaded();
        if (!reconciled) ReconcileLoaded();
        long now = DateTime.UtcNow.Ticks;
        if (now < nextSaveUtcTicks) return;
        nextSaveUtcTicks = now + TimeSpan.FromSeconds(15).Ticks;
        if (RebirthNpcWorkExecutionTelemetry.Revision == lastSavedTelemetryRevision) return;
        Save();
    }

    public static void Save()
    {
        Save(RebirthNpcPersistenceCoordinator.IsCheckpointWrite);
    }

    private static void Save(bool force)
    {
        EnsureLoaded();
        long capturedRevision = RebirthNpcWorkExecutionTelemetry.Revision;
        if (!force && capturedRevision == lastSavedTelemetryRevision) return;
        string path = GetPath();
        if (string.IsNullOrEmpty(path)) return;
        RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded, path);
        RebirthNpcWorkRecoveryCheckpoint[] snapshots = CaptureCheckpointRows();
        XmlDocument document = new XmlDocument();
        XmlElement root = document.CreateElement("rebirthNpcWorkCheckpoints");
        root.SetAttribute("format", FormatVersion.ToString(CultureInfo.InvariantCulture));
        root.SetAttribute("savedUtcTicks", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        document.AppendChild(root);
        for (int i = 0; i < snapshots.Length; i++)
        {
            RebirthNpcWorkRecoveryCheckpoint s = snapshots[i];
            XmlElement e = document.CreateElement("checkpoint");
            e.SetAttribute("assignment", s.AssignmentId.ToString(CultureInfo.InvariantCulture));
            e.SetAttribute("executor", s.ExecutorId ?? string.Empty);
            e.SetAttribute("target", s.StableTargetId ?? string.Empty);
            e.SetAttribute("phase", ((byte)s.Phase).ToString(CultureInfo.InvariantCulture));
            e.SetAttribute("progress", s.Progress.ToString("R", CultureInfo.InvariantCulture));
            e.SetAttribute("retries", s.RetryCount.ToString(CultureInfo.InvariantCulture));
            e.SetAttribute("updated", s.UpdatedUtcTicks.ToString(CultureInfo.InvariantCulture));
            root.AppendChild(e);
        }
        RebirthNpcPersistenceFile.SaveAtomic(path, document);
        saves++;
        lastSavedTelemetryRevision = capturedRevision;
    }

    private static RebirthNpcWorkRecoveryCheckpoint[] CaptureCheckpointRows()
    {
        var merged = new Dictionary<ulong, RebirthNpcWorkRecoveryCheckpoint>();
        lock (Sync)
        {
            // Before the recovery scheduler has run, saving must not erase its loaded work.
            if (!reconciled) foreach (var row in Loaded) merged[row.AssignmentId] = row;
        }
        foreach (var row in RebirthNpcWorkExecutionTelemetry.Snapshot())
            merged[row.AssignmentId] = new RebirthNpcWorkRecoveryCheckpoint { AssignmentId = row.AssignmentId,
                ExecutorId = row.ExecutorId, StableTargetId = row.StableTargetId, Phase = row.Phase,
                Progress = row.Progress, RetryCount = row.RetryCount, UpdatedUtcTicks = row.UpdatedUtcTicks };
        var ids = new List<ulong>(merged.Keys); ids.Sort();
        var result = new RebirthNpcWorkRecoveryCheckpoint[ids.Count];
        for (int i = 0; i < ids.Count; i++) result[i] = merged[ids[i]];
        return result;
    }

    public static void Reset(bool save)
    {
        if (save) Save();
        lock (Sync)
        {
            loaded = false;
            reconciled = false;
            Loaded.Clear();
            nextSaveUtcTicks = 0;
            lastSavedTelemetryRevision = -1L;
        }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder b = new StringBuilder("[REBIRTH NPC Work Checkpoint Persistence] loaded=")
                .Append(loaded).Append(" reconciled=").Append(reconciled).Append(" pendingLoaded=").Append(Loaded.Count)
                .Append(" loads=").Append(loads).Append(" saves=").Append(saves)
                .Append(" suspendedRecoveries=").Append(suspendedRecoveries)
                .Append(" ignoredTerminal=").Append(ignoredTerminal).Append(" missingAssignments=").Append(missingAssignments)
                .Append(" rejected=").Append(rejected);
            for (int i = 0; i < Loaded.Count; i++) b.AppendLine().Append("  assignment=").Append(Loaded[i].AssignmentId)
                .Append(" executor=").Append(Loaded[i].ExecutorId).Append(" phase=").Append(Loaded[i].Phase)
                .Append(" progress=").Append(Loaded[i].Progress.ToString("0.000", CultureInfo.InvariantCulture));
            return b.ToString();
        }
    }

    private static void ReconcileLoaded()
    {
        RebirthNpcWorkRecoveryCheckpoint[] checkpoints;
        lock (Sync)
        {
            if (reconciled) return;
            reconciled = true;
            checkpoints = Loaded.ToArray();
        }
        for (int i = 0; i < checkpoints.Length; i++)
        {
            RebirthNpcWorkAssignment assignment;
            if (!RebirthNpcWorkAssignmentService.TryGet(checkpoints[i].AssignmentId, out assignment))
            {
                missingAssignments++;
                continue;
            }
            if (assignment.Status == RebirthNpcWorkAssignmentStatus.Completed ||
                assignment.Status == RebirthNpcWorkAssignmentStatus.Cancelled ||
                assignment.Status == RebirthNpcWorkAssignmentStatus.Failed)
            {
                ignoredTerminal++;
                continue;
            }
            if (assignment.Status == RebirthNpcWorkAssignmentStatus.Executing ||
                assignment.Status == RebirthNpcWorkAssignmentStatus.Reserved)
            {
                RebirthNpcWorkAssignment updated;
                if (RebirthNpcWorkAssignmentService.TryTransition(assignment.AssignmentId, assignment.Revision,
                    RebirthNpcWorkAssignmentStatus.Suspended,
                    "Recovered from an interrupted non-resumable work checkpoint; manual or scheduler retry required.", out updated))
                    suspendedRecoveries++;
                RebirthNpcWorkReservationService.ReleaseAssignment(assignment.AssignmentId,
                    "Recovered interrupted work checkpoint.");
            }
        }
        Save(true);
    }

    private static bool Validate(XmlDocument document)
    {
        if (document == null || document.DocumentElement == null ||
            document.DocumentElement.Name != "rebirthNpcWorkCheckpoints") return false;
        int format = ParseInt(document.DocumentElement.GetAttribute("format"), 0);
        return format >= 1 && format <= FormatVersion;
    }

    private static string GetPath()
    {
        string directory = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(directory) ? string.Empty : Path.Combine(directory, FileName);
    }

    private static int ParseInt(string value, int fallback)
    {
        int parsed;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
    }

    private static long ParseLong(string value)
    {
        long parsed;
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0L;
    }
}
