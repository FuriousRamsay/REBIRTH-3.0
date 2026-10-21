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

public static partial class RebirthNpcWorkCheckpointPersistenceStore
{
    private const int FormatVersion = 1;
    private const string StoreId = "npc-work-checkpoints";
    private const string FileName = "RebirthNpcWorkCheckpoints.xml";
    private static readonly object Sync = new object();
    private static World boundWorld;
    private static string boundPath;
    private static bool BoundCompatible=>boundWorld==null||(ReferenceEquals(boundWorld,GameManager.Instance?.World)&&boundPath==GetPath());
    private static void BindCurrent()
    {
        if(!BoundCompatible||GameManager.Instance?.World==null)throw new InvalidOperationException("NPC cache belongs to another retained original world/save");
        boundWorld=GameManager.Instance.World;boundPath=GetPath();
    }
    private static List<RebirthNpcWorkRecoveryCheckpoint> Loaded = new List<RebirthNpcWorkRecoveryCheckpoint>();
    private static bool loaded, reconciled;
    private static long nextSaveUtcTicks;
    private static long lastSavedTelemetryRevision = -1L;
    private static long loads, saves, suspendedRecoveries, ignoredTerminal, missingAssignments, rejected;

    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        lock (Sync)
        {
            if(!BoundCompatible)throw new InvalidOperationException("Retained cache scope differs; original retirement required");
            if (loaded) return;
            BindCurrent();
        }
        string path = GetPath();
        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path) && !File.Exists(path + ".bak"))
        {
            if(!RebirthNpcPersistenceFile.CanInitializeEmpty(path)||Loaded.Count!=0||RebirthNpcWorkExecutionTelemetry.Snapshot().Length!=0)return;
            RebirthNpcPersistenceSchemaTelemetry.RecordLoad(StoreId, path, FormatVersion, FormatVersion, "new");
            lock(Sync)loaded=true;
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
        if(nodes.Count>512){RebirthNpcPersistenceFile.BlockWrite(path,"Checkpoint custody exceeds lossless supported bound");return;}
        lock (Sync)
        {
            if(Loaded.Count!=0)throw new InvalidOperationException("Retained checkpoint originals cannot be overwritten during ordinary load");
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
            loaded=true;
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
        lock(Sync)if(!loaded)throw new InvalidOperationException("Checkpoint persistence load remains unresolved");
        if (!force && RebirthNpcWorkExecutionTelemetry.Revision == lastSavedTelemetryRevision) return;
        string path = GetPath();
        if (string.IsNullOrEmpty(path)) return;
        RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded, path);
        using var recoveryWriter = RebirthNpcPersistenceFile.BeginWrite(path);
        long capturedRevision = RebirthNpcWorkExecutionTelemetry.Revision;
        RebirthNpcWorkRecoveryCheckpoint[] snapshots = CaptureCheckpointRows();
        if(snapshots.Length>512)throw new InvalidOperationException("Checkpoint custody exceeds lossless publication bound");
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
        if(capturedRevision!=RebirthNpcWorkExecutionTelemetry.Revision)throw new InvalidOperationException("Checkpoint telemetry changed before publication");
        RebirthNpcPersistenceFile.SaveAtomic(path, document);
        if(capturedRevision!=RebirthNpcWorkExecutionTelemetry.Revision)throw new InvalidOperationException("Checkpoint telemetry changed during publication; latest remains unsaved");
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
        if(!TryReset(save))throw new InvalidOperationException("Checkpoint original custody retained; typed retirement required");
    }
    internal static bool CanRetireBacking
    {
        get{lock(Sync)try
        {
            if(Loaded.Exists(row=>row.Phase!=RebirthNpcWorkExecutorPhase.Terminal)||RebirthNpcWorkExecutionTelemetry.Snapshot().Length!=0)return false;
            if(boundWorld==null)return !loaded&&Loaded.Count==0;
            if(!loaded||string.IsNullOrEmpty(boundPath)||RebirthNpcPersistenceFile.IsRecoveryFenced(boundPath))return false;
            var rows=CaptureCheckpointRows();long revision=RebirthNpcWorkExecutionTelemetry.Revision;
            if(!File.Exists(boundPath))return rows.Length==0&&RebirthNpcPersistenceFile.CanInitializeEmpty(boundPath);
            var primary=RebirthNpcRecoveryCurrentBacking.Read(boundPath);if(!Validate(primary))return false;
            var saved=new List<RebirthNpcWorkRecoveryCheckpoint>();
            foreach(XmlElement row in primary.DocumentElement.SelectNodes("checkpoint"))saved.Add(new RebirthNpcWorkRecoveryCheckpoint{AssignmentId=ulong.Parse(row.GetAttribute("assignment"),CultureInfo.InvariantCulture),ExecutorId=row.GetAttribute("executor"),StableTargetId=row.GetAttribute("target"),Phase=(RebirthNpcWorkExecutorPhase)int.Parse(row.GetAttribute("phase"),CultureInfo.InvariantCulture),Progress=float.Parse(row.GetAttribute("progress"),CultureInfo.InvariantCulture),RetryCount=int.Parse(row.GetAttribute("retries"),CultureInfo.InvariantCulture),UpdatedUtcTicks=long.Parse(row.GetAttribute("updated"),CultureInfo.InvariantCulture)});
            return RecoveryRowsImage(saved)==RecoveryRowsImage(rows)&&revision==RebirthNpcWorkExecutionTelemetry.Revision;
        }catch{return false;}}
    }
    public static bool TryReset(bool save)
    {
        if(save)try{Save();}catch{return false;}
        lock (Sync)
        {
            if(!CanRetireBacking)return false;
            loaded = false;boundWorld=null;boundPath=null;
            reconciled = false;
            Loaded.Clear();
            nextSaveUtcTicks = 0;
            lastSavedTelemetryRevision = -1L;
            return true;
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
        if(!RebirthNpcWorkReleaseGate.Enabled)return; // Keep recovered original checkpoint custody held.
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

    private static string RecoveryRowsImage(IEnumerable<RebirthNpcWorkRecoveryCheckpoint> rows)
    {
        var ordered=new List<RebirthNpcWorkRecoveryCheckpoint>(rows);ordered.Sort((a,b)=>a.AssignmentId.CompareTo(b.AssignmentId));var text=new StringBuilder();
        foreach(var row in ordered)text.Append(row.AssignmentId.ToString(CultureInfo.InvariantCulture)).Append('|').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(row.ExecutorId??string.Empty))).Append('|').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(row.StableTargetId??string.Empty))).Append('|').Append((byte)row.Phase).Append('|').Append(row.Progress.ToString("R",CultureInfo.InvariantCulture)).Append('|').Append(row.RetryCount).Append('|').Append(row.UpdatedUtcTicks).Append('\n');return text.ToString();
    }
    internal static bool TryPrepareRecovery(XmlDocument document,out RebirthNpcPreparedRecovery candidate,out RebirthNpcWorkRecoveryCheckpoint[] records,out string error)
    {
        candidate=null;records=null;error=string.Empty;if(!BoundCompatible){error="recovery cannot replace another original world cache";return false;}if(!Validate(document)||document.DocumentElement.SelectNodes("checkpoint").Count>512){error="checkpoint semantic/lossless-bound validation failed";return false;}
        var next=new List<RebirthNpcWorkRecoveryCheckpoint>();foreach(XmlElement row in document.DocumentElement.SelectNodes("checkpoint"))next.Add(new RebirthNpcWorkRecoveryCheckpoint{AssignmentId=ulong.Parse(row.GetAttribute("assignment"),CultureInfo.InvariantCulture),ExecutorId=row.GetAttribute("executor"),StableTargetId=row.GetAttribute("target"),Phase=(RebirthNpcWorkExecutorPhase)int.Parse(row.GetAttribute("phase"),CultureInfo.InvariantCulture),Progress=float.Parse(row.GetAttribute("progress"),CultureInfo.InvariantCulture),RetryCount=int.Parse(row.GetAttribute("retries"),CultureInfo.InvariantCulture),UpdatedUtcTicks=long.Parse(row.GetAttribute("updated"),CultureInfo.InvariantCulture)});
        long telemetry=RebirthNpcWorkExecutionTelemetry.Revision;string before;bool wasLoaded,wasReconciled;
        lock(Sync)
        {
            if(RebirthNpcWorkExecutionTelemetry.Snapshot().Length!=0){error="native work execution telemetry remains active";return false;}
            before=RecoveryRowsImage(Loaded);if(Loaded.Exists(row=>row.Phase!=RebirthNpcWorkExecutorPhase.Terminal)&&before!=RecoveryRowsImage(next)){error="existing nonterminal saved checkpoint custody cannot be replaced";return false;}
            wasLoaded=loaded;wasReconciled=reconciled;
        }
        var detached=new List<RebirthNpcWorkRecoveryCheckpoint>();foreach(var row in next)detached.Add(new RebirthNpcWorkRecoveryCheckpoint{AssignmentId=row.AssignmentId,ExecutorId=row.ExecutorId,StableTargetId=row.StableTargetId,Phase=row.Phase,Progress=row.Progress,RetryCount=row.RetryCount,UpdatedUtcTicks=row.UpdatedUtcTicks});records=detached.ToArray();
        Func<bool> current=()=>{lock(Sync)return BoundCompatible&&loaded==wasLoaded&&reconciled==wasReconciled&&RebirthNpcWorkExecutionTelemetry.Revision==telemetry&&RebirthNpcWorkExecutionTelemetry.Snapshot().Length==0&&RecoveryRowsImage(Loaded)==before;};
        candidate=new RebirthNpcPreparedRecovery(FileName,current,()=>{lock(Sync){if(!current())return false;Loaded=next;BindCurrent();loaded=true;reconciled=false;lastSavedTelemetryRevision=telemetry;nextSaveUtcTicks=0;return true;}});return true;
    }
    internal static bool ValidateRecoveryDocument(XmlDocument document)=>Validate(document);
    private static bool Validate(XmlDocument document)
    {
        if (document == null || document.DocumentElement == null ||
            document.DocumentElement.Name != "rebirthNpcWorkCheckpoints") return false;
        int format = ParseInt(document.DocumentElement.GetAttribute("format"), 0);
        if(format<1||format>FormatVersion)return false;
        var assignments=new HashSet<ulong>();
        foreach(XmlNode node in document.DocumentElement.ChildNodes)
        {
            var row=node as XmlElement;if(row==null)continue;
            ulong assignment;int phase,retries;long updated;float progress;
            if(row.Name!="checkpoint"||row.HasChildNodes||
                !ulong.TryParse(row.GetAttribute("assignment"),NumberStyles.Integer,CultureInfo.InvariantCulture,out assignment)||assignment==0||!assignments.Add(assignment)||
                !int.TryParse(row.GetAttribute("phase"),NumberStyles.Integer,CultureInfo.InvariantCulture,out phase)||phase<0||phase>byte.MaxValue||!Enum.IsDefined(typeof(RebirthNpcWorkExecutorPhase),(byte)phase)||
                !float.TryParse(row.GetAttribute("progress"),NumberStyles.Float,CultureInfo.InvariantCulture,out progress)||float.IsNaN(progress)||float.IsInfinity(progress)||progress<0||progress>1||
                !int.TryParse(row.GetAttribute("retries"),NumberStyles.Integer,CultureInfo.InvariantCulture,out retries)||retries<0||
                !long.TryParse(row.GetAttribute("updated"),NumberStyles.Integer,CultureInfo.InvariantCulture,out updated)||updated<0||updated>DateTime.MaxValue.Ticks||
                row.GetAttribute("executor").Length>4096||row.GetAttribute("target").Length>4096)return false;
        }
        return true;
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
