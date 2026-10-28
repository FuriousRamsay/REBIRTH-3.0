using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

#nullable disable

public sealed class RebirthNpcPersistenceFileSnapshot
{
    public string FileName;
    public bool Exists;
    public long Length;
    public string Sha256;
    public bool BackupExists;
    public long BackupLength;
}


/// <summary>
/// Save-timeline guard for every REBIRTH NPC sidecar. 7DTD save rollback can restore the
/// world files without restoring mod sidecars; checkpointing them by World.worldTime keeps
/// the sidecars on the same branch as the world instead of letting newer NPC ownership/state
/// bleed into an older save.
/// </summary>
public static partial class RebirthNpcPersistenceTimelineService
{
    private const string TimelineDirectoryName = "RebirthNpcTimeline";
    private const int MaxCheckpoints = 16;
    private static readonly object TimelineSync = new object();
    private static bool alignmentAttempted;
    private static bool alignmentFailed;
    private static bool rollbackUnresolved;
    public static bool IsAlignmentFailed { get { lock(TimelineSync)return alignmentFailed||rollbackUnresolved; } }
    private static bool rollbackDetected;
    private static ulong currentWorldTime;
    private static ulong newestCheckpoint;
    private static ulong restoredCheckpoint;
    private static string lastDetail = "not checked";

    private static readonly string[] SidecarNames =
    {
        "RebirthNpcStableIdentities.xml",
        "rebirth_npc_inventory.xml",
        "rebirth_npc_equipment.xml",
        "RebirthNpcExecutionState.xml",
        "rebirth_npc_settlement.xml",
        "RebirthNpcWorkCheckpoints.xml",
        "RebirthNpcWorkOutcomes.xml",
        "RebirthNpcSocialState.xml",
        "RebirthNpcCombatState.xml",
        "RebirthNpcProgression.xml",
        "RebirthNpcAdvancedProgression.xml",
        "RebirthNpcPersistentRecords.xml",
        "RebirthNpcIdentityAudit.xml",
        "RebirthNpcLifecycleGameplay.xml",
        "RebirthNpcWorldIntegration.xml",
        "RebirthNpcEconomyOffline.xml",
        RebirthNpcExternalInventoryTransferCoordinator.JournalFileName
    };

    public static string[] GetSidecarNames()
    {
        string[] copy = new string[SidecarNames.Length];
        Array.Copy(SidecarNames, copy, SidecarNames.Length);
        return copy;
    }

    internal static bool CanAdoptRecovery(out string error)
    {
        lock(TimelineSync)
        {
            try{RequireTimelineScope();}catch(Exception exception){error=exception.Message;return false;}
            error=string.Empty;if(alignmentFailed||rollbackUnresolved){error=lastDetail;return false;}
            var world=GameManager.Instance?.World;string directory=GameIO.GetSaveGameDir();
            if(world?.IsRemote()!=false||string.IsNullOrEmpty(directory)){error="Native recovery world is not ready";return false;}
            string root=Path.Combine(directory,TimelineDirectoryName);
            if(Directory.Exists(root))
            {
                var checkpoints=EnumerateCheckpoints(root);
                if(checkpoints.Count!=0&&checkpoints[checkpoints.Count-1]>world.worldTime)
                {alignmentFailed=true;lastDetail="Native timeline rollback conflicts with pending recovery; retained for original branch reconciliation";error=lastDetail;return false;}
            }
            return true;
        }
    }    public static void EnsureAligned()
    {
        lock (TimelineSync)
        {
            RequireTimelineScope();
            if(alignmentFailed||rollbackUnresolved)throw new InvalidOperationException("NPC timeline alignment remains unresolved: "+lastDetail);
            if (alignmentAttempted) return;

            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            string saveDir = GameIO.GetSaveGameDir();
            try{if(!string.IsNullOrEmpty(saveDir)&&Directory.Exists(saveDir))RecoverInterruptedTimelineRestore(saveDir);}
            catch(Exception failure){alignmentFailed=true;lastDetail="interrupted checkpoint restore retained: "+failure.Message;throw;}
            var recovery=string.IsNullOrEmpty(saveDir)?null:RebirthNpcLiveRecoveryGroup.Read(saveDir);
            if(recovery!=null)
            {
                bool installed=recovery.Phase==2;foreach(var member in recovery.Members)installed&=RebirthNpcRecoveryPublications.IsInstalled(Path.Combine(saveDir,member.File));
                string recoveryError=string.Empty;if(!installed||!CanAdoptRecovery(out recoveryError))throw new InvalidOperationException("Pending recovery must reconcile before timeline alignment: "+recoveryError);
                alignmentAttempted=true;currentWorldTime=world.worldTime;lastDetail="current installed recovery generation "+recovery.Generation+" retained without backup/timeline reinstallation";return;
            }
            if(!string.IsNullOrEmpty(saveDir))foreach(string file in SidecarNames)
                if(File.Exists(RebirthNpcPersistenceFile.RecoveryIntentPath(Path.Combine(saveDir,file))))
                    throw new InvalidOperationException("Pending NPC recovery must reconcile before timeline alignment: "+file);
            if (world == null || string.IsNullOrEmpty(saveDir))
            {
                lastDetail = "world/save path not ready";
                return;
            }

            alignmentAttempted = true;
            currentWorldTime = world.worldTime;
            string root = Path.Combine(saveDir, TimelineDirectoryName);
            if (!Directory.Exists(root))
            {
                newestCheckpoint = 0UL;
                restoredCheckpoint = 0UL;
                lastDetail = "legacy/untracked sidecars; first v237 checkpoint not written yet";
                { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH NPC Timeline] " + lastDetail + " worldTime=" + currentWorldTime.ToString(CultureInfo.InvariantCulture)); }
                return;
            }

            List<ulong> checkpoints = EnumerateCheckpoints(root);
            if (checkpoints.Count == 0)
            {
                lastDetail = "timeline directory exists but has no checkpoints";
                return;
            }

            newestCheckpoint = checkpoints[checkpoints.Count - 1];
            if (newestCheckpoint <= currentWorldTime)
            {
                lastDetail = "aligned";
                return;
            }

            rollbackDetected = true;
            ulong candidate = 0UL;
            for (int i = 0; i < checkpoints.Count; i++)
            {
                if (checkpoints[i] <= currentWorldTime)
                    candidate = checkpoints[i];
                else
                    break;
            }

            if (candidate == 0UL && currentWorldTime != 0UL)
            {
                rollbackUnresolved=true;
                lastDetail = "rollback detected but no checkpoint exists at/before restored world time; sidecars left untouched for manual recovery";
                Log.Warning("[REBIRTH NPC Timeline] " + lastDetail
                    + " worldTime=" + currentWorldTime.ToString(CultureInfo.InvariantCulture)
                    + " newestCheckpoint=" + newestCheckpoint.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (candidate == 0UL && currentWorldTime == 0UL)
            {
                rollbackUnresolved=true;
                lastDetail = "rollback to worldTime 0 with no matching checkpoint; sidecars left untouched for manual recovery";
                Log.Warning("[REBIRTH NPC Timeline] " + lastDetail);
                return;
            }

            RebirthNpcSaveScopeSnapshot saveScope = RebirthNpcSaveScope.ObserveCurrent();
            string checkpointScope = ReadCheckpointSaveScope(root, candidate);
            bool checkpointHasScope = !string.IsNullOrWhiteSpace(checkpointScope);
            bool checkpointForeign = checkpointHasScope && saveScope != null &&
                !string.Equals(checkpointScope, saveScope.Fingerprint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            // Legacy checkpoints have no durable save provenance. Do not restore them
            // automatically in the provenance-capable build; a scoped checkpoint will be
            // created on the next successful save.
            bool legacyCheckpointUntrusted = !checkpointHasScope;

            if (checkpointForeign || legacyCheckpointUntrusted)
            {
                rollbackUnresolved=true;
                lastDetail = checkpointForeign
                    ? "rollback checkpoint belongs to another save scope; restore refused"
                    : "unscoped legacy rollback checkpoint refused because save provenance is unknown";
                Log.Warning("[REBIRTH NPC Timeline] " + lastDetail
                    + " worldTime=" + currentWorldTime.ToString(CultureInfo.InvariantCulture)
                    + " checkpoint=" + candidate.ToString(CultureInfo.InvariantCulture));
                return;
            }

            try { RestoreCheckpoint(saveDir, root, candidate); }
            catch(Exception error)
            {
                alignmentFailed=true;
                lastDetail="checkpoint restore failed; original recovery evidence retained: "+error.GetType().Name;
                throw;
            }
            restoredCheckpoint = candidate;
            lastDetail = "rollback restored checkpoint " + candidate.ToString(CultureInfo.InvariantCulture);
            Log.Warning("[REBIRTH NPC Timeline] rollback detected: worldTime="
                + currentWorldTime.ToString(CultureInfo.InvariantCulture)
                + " newestCheckpoint=" + newestCheckpoint.ToString(CultureInfo.InvariantCulture)
                + " restoredCheckpoint=" + restoredCheckpoint.ToString(CultureInfo.InvariantCulture));
        }
    }

    public static void CheckpointAll()
    {
        lock (TimelineSync)
        {
            RequireTimelineScope();
            if(alignmentFailed||rollbackUnresolved)throw new InvalidOperationException("Unresolved original NPC rollback cannot publish a fresh checkpoint: "+lastDetail);
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            string saveDir = GameIO.GetSaveGameDir();
            if (world == null || string.IsNullOrEmpty(saveDir)) return;

            ulong worldTime = world.worldTime;
            string root = Path.Combine(saveDir, TimelineDirectoryName);
            Directory.CreateDirectory(root);
            // Every save publishes an immutable generation. Multiple saves at the same
            // worldTime therefore remain distinct instead of deleting/replacing a prior
            // complete checkpoint in place.
            string generationId = DateTime.UtcNow.Ticks.ToString("D19", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            string checkpoint = Path.Combine(root,
                worldTime.ToString("D20", CultureInfo.InvariantCulture) + "-g-" + generationId);
            string staging = checkpoint + ".staging";
            Directory.CreateDirectory(staging);
            try
            {
                StringBuilder manifest = new StringBuilder();
                for (int i = 0; i < SidecarNames.Length; i++)
                {
                    string src = Path.Combine(saveDir, SidecarNames[i]);
                    if (!File.Exists(src))
                        throw new InvalidDataException("required NPC sidecar missing: " + SidecarNames[i]);
                    string dst = Path.Combine(staging, SidecarNames[i]);
                    File.Copy(src, dst, false);
                    FileInfo info = new FileInfo(dst);
                    manifest.Append(SidecarNames[i]).Append('|').Append(info.Length.ToString(CultureInfo.InvariantCulture))
                        .Append('|').Append(HashFile(dst)).AppendLine();
                }
                File.WriteAllText(Path.Combine(staging, "_manifest.sha256"), manifest.ToString(), new UTF8Encoding(false));
                RebirthNpcSaveScopeSnapshot saveScope = RebirthNpcSaveScope.ObserveCurrent();
                File.WriteAllText(Path.Combine(staging, "_checkpoint.txt"),
                    "worldTime=" + worldTime.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                    "generation=" + generationId + Environment.NewLine +
                    "utc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + Environment.NewLine +
                    "saveScope=" + (saveScope != null ? (saveScope.Fingerprint ?? string.Empty) : string.Empty) + Environment.NewLine,
                    new UTF8Encoding(false));
                string detail;
                if (!ValidateCheckpoint(staging, out detail)) throw new InvalidDataException(detail);
                // Complete marker is the publication boundary and is written only after every
                // required member and hash has validated.
                File.WriteAllText(Path.Combine(staging, "_complete.txt"), "complete", new UTF8Encoding(false));
                // Directory publication is last. A staging directory is never enumerated
                // as complete, and an existing immutable generation is never overwritten.
                Directory.Move(staging, checkpoint);
            }
            catch
            {
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                throw;
            }

            currentWorldTime = worldTime;
            newestCheckpoint = Math.Max(newestCheckpoint, worldTime);
            lastDetail = "checkpoint " + worldTime.ToString(CultureInfo.InvariantCulture) +
                " generation=" + generationId;
            Prune(root);
        }
    }

    public static string GetReport()
    {
        lock (TimelineSync)
        {
            return "[REBIRTH NPC Timeline] worldTime=" + currentWorldTime.ToString(CultureInfo.InvariantCulture)
                + " newestCheckpoint=" + newestCheckpoint.ToString(CultureInfo.InvariantCulture)
                + " rollbackDetected=" + rollbackDetected
                + " unresolvedRollback=" + rollbackUnresolved
                + " restoredCheckpoint=" + restoredCheckpoint.ToString(CultureInfo.InvariantCulture)
                + " detail=" + lastDetail;
        }
    }

    public static void ResetForWorld()
    {
        lock (TimelineSync)
        {
            if(!CanRetireTimeline)throw new InvalidOperationException("Original NPC timeline diagnostic requires typed retention");
            alignmentAttempted = false;
            alignmentFailed = false;
            rollbackUnresolved = false;
            rollbackDetected = false;
            currentWorldTime = 0UL;
            newestCheckpoint = 0UL;
            restoredCheckpoint = 0UL;
            lastDetail = "not checked";
            timelineWorld=null;timelineDirectory=null;
        }
    }

    private static List<ulong> EnumerateCheckpoints(string root)
    {
        List<ulong> result = new List<ulong>();
        string[] dirs = Directory.GetDirectories(root);
        for (int i = 0; i < dirs.Length; i++)
        {
            ulong value;
            string detail;
            string name = Path.GetFileName(dirs[i]) ?? string.Empty;
            int generationMarker = name.IndexOf("-g-", StringComparison.OrdinalIgnoreCase);
            string worldTimeText = generationMarker > 0 ? name.Substring(0, generationMarker) : name;
            if (ulong.TryParse(worldTimeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                File.Exists(Path.Combine(dirs[i], "_complete.txt")) && ValidateCheckpoint(dirs[i], out detail))
                result.Add(value);
        }
        result.Sort();
        return result;
    }

    private static string ResolveCheckpointDirectory(string root, ulong checkpointWorldTime, bool newest)
    {
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return string.Empty;
        string prefix = checkpointWorldTime.ToString("D20", CultureInfo.InvariantCulture);
        List<string> matches = new List<string>();
        string[] dirs = Directory.GetDirectories(root);
        for (int i = 0; i < dirs.Length; i++)
        {
            string name = Path.GetFileName(dirs[i]) ?? string.Empty;
            if (!string.Equals(name, prefix, StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith(prefix + "-g-", StringComparison.OrdinalIgnoreCase)) continue;
            string detail;
            if (!File.Exists(Path.Combine(dirs[i], "_complete.txt")) || !ValidateCheckpoint(dirs[i], out detail)) continue;
            matches.Add(dirs[i]);
        }
        if (matches.Count == 0) return string.Empty;
        matches.Sort(StringComparer.OrdinalIgnoreCase);
        return newest ? matches[matches.Count - 1] : matches[0];
    }

    private static string ReadCheckpointSaveScope(string root, ulong checkpointWorldTime)
    {
        try
        {
            string checkpoint = ResolveCheckpointDirectory(root, checkpointWorldTime, true);
            if (string.IsNullOrEmpty(checkpoint)) return string.Empty;
            string metadata = Path.Combine(checkpoint, "_checkpoint.txt");
            if (!File.Exists(metadata)) return string.Empty;
            string[] lines = File.ReadAllLines(metadata);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                if (line.StartsWith("saveScope=", StringComparison.OrdinalIgnoreCase))
                    return line.Substring("saveScope=".Length).Trim();
            }
        }
        catch { }
        return string.Empty;
    }

    private static void RestoreCheckpoint(string saveDir, string root, ulong checkpointWorldTime)
    {
        string checkpoint = ResolveCheckpointDirectory(root, checkpointWorldTime, true);
        if (string.IsNullOrEmpty(checkpoint))
            throw new DirectoryNotFoundException("checkpoint generation is unavailable for worldTime " +
                checkpointWorldTime.ToString(CultureInfo.InvariantCulture));
        string validationDetail = string.Empty;
        if (!File.Exists(Path.Combine(checkpoint, "_complete.txt")) || !ValidateCheckpoint(checkpoint, out validationDetail))
            throw new InvalidDataException("checkpoint is incomplete: " + validationDetail);
        // Stage and verify the whole generation before touching any live sidecar.
        string operationId = Guid.NewGuid().ToString("N");
        string staging = Path.Combine(saveDir, ".rebirth-npc-restore-staging-" + operationId);
        string rollback = Path.Combine(saveDir, ".rebirth-npc-restore-rollback-" + operationId);
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(rollback);
        HashSet<string> priorPrimary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> priorBackup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool publicationStarted=false;
        bool retainRollback=false;
        try
        {
            for (int i = 0; i < SidecarNames.Length; i++)
            {
                string name = SidecarNames[i];
                string src = Path.Combine(checkpoint, name);
                string staged = Path.Combine(staging, name);
                if (!File.Exists(src)) throw new InvalidDataException("restore source missing: " + name);
                CopyRestoreDurable(src, staged);
                if (!string.Equals(HashFile(src), HashFile(staged), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("restore staging hash mismatch: " + name);

                string dst = Path.Combine(saveDir, name);
                string bak = dst + ".bak";
                if (File.Exists(dst))
                {
                    priorPrimary.Add(name);
                    CopyRestoreDurable(dst, Path.Combine(rollback, name));
                }
                if (File.Exists(bak))
                {
                    priorBackup.Add(name);
                    CopyRestoreDurable(bak, Path.Combine(rollback, name + ".bak"));
                }
            }

            // Publish all primaries first, then advance backups to the same generation.
            // Any exception rolls the complete set back to its exact pre-restore membership.
            PrepareRestoreJournal(saveDir,staging,rollback,priorPrimary,priorBackup);
            publicationStarted=true;
            for (int i = 0; i < SidecarNames.Length; i++)
            {
                string name = SidecarNames[i];
                PublishRestoredFile(Path.Combine(staging, name), Path.Combine(saveDir, name));
            }
            for (int i = 0; i < SidecarNames.Length; i++)
            {
                string live = Path.Combine(saveDir, SidecarNames[i]);
                PublishRestoredFile(live, live + ".bak");
            }
            CompleteRestoreJournal(rollback,"published");
        }
        catch (Exception restoreFailure)
        {
            // A staging/copy failure has not changed any live file. Membership
            // sets are incomplete then and must never authorize deleting files.
            if(!publicationStarted)throw;
            // A complete positive publication must not be undone merely
            // because a later observation/cleanup failed.
            if(HasPublishedRestoreTerminal(rollback)){retainRollback=true;throw;}
            List<string> rollbackErrors = new List<string>();
            for (int i = 0; i < SidecarNames.Length; i++)
            {
                string name = SidecarNames[i];
                TryRestorePrior(Path.Combine(saveDir, name), Path.Combine(rollback, name),
                    priorPrimary.Contains(name), rollbackErrors);
                TryRestorePrior(Path.Combine(saveDir, name + ".bak"), Path.Combine(rollback, name + ".bak"),
                    priorBackup.Contains(name), rollbackErrors);
            }
            if (rollbackErrors.Count > 0)
            {
                retainRollback=true;
                throw new IOException("NPC checkpoint restore failed (" + restoreFailure.GetType().Name +
                    ") and rollback was incomplete: " + string.Join(" | ", rollbackErrors.ToArray()), restoreFailure);
            }
            try{CompleteRestoreJournal(rollback,"rolledBack");}
            catch{retainRollback=true;throw;}
            throw;
        }
        finally
        {
            try
            {
                if(!retainRollback&&Directory.Exists(rollback))
                {
                    if(publicationStarted)RetireRestoreJournal(staging,rollback);
                    else{CleanupRestoreDirectory(staging,false);CleanupRestoreDirectory(rollback,true);}
                }
            }catch{/* Complete terminal evidence or failed capture remains for qualified recovery. */}
        }
    }

    private static void PublishRestoredFile(string source, string destination)
    {
        string temp = destination + ".restore."+Guid.NewGuid().ToString("N")+".tmp";
        CopyRestoreDurable(source,temp);
        if (!string.Equals(HashFile(source), HashFile(temp), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("restore publication hash mismatch: " + Path.GetFileName(destination));
        if(!RebirthDurableFileCommit.TryPublish(temp,destination,out var error))
            throw new IOException("NPC restore publication failed: "+error);
        ObserveRestoreScenario("publish-after",destination);
    }

    private static void TryRestorePrior(string destination, string rollbackSource, bool existed,
        List<string> errors)
    {
        try
        {
            if (existed)
            {
                if (!File.Exists(rollbackSource))
                    throw new FileNotFoundException("rollback member missing", rollbackSource);
                PublishRestoredFile(rollbackSource, destination);
            }
            else if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
        catch (Exception ex)
        {
            errors.Add(Path.GetFileName(destination) + "=" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private static bool ValidateCheckpoint(string directory, out string detail)
    {
        detail = string.Empty;
        try
        {
            string manifestPath = Path.Combine(directory, "_manifest.sha256");
            if (!File.Exists(manifestPath)) { detail = "manifest missing"; return false; }
            Dictionary<string, string> expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(manifestPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] fields = (lines[i] ?? string.Empty).Split('|');
                if (fields.Length != 3) { detail = "invalid manifest row"; return false; }
                expected[fields[0]] = fields[2];
            }
            for (int i = 0; i < SidecarNames.Length; i++)
            {
                string hash; string path = Path.Combine(directory, SidecarNames[i]);
                if (!expected.TryGetValue(SidecarNames[i], out hash) || !File.Exists(path))
                { detail = "required member missing: " + SidecarNames[i]; return false; }
                if (!string.Equals(hash, HashFile(path), StringComparison.OrdinalIgnoreCase))
                { detail = "hash mismatch: " + SidecarNames[i]; return false; }
            }
            return true;
        }
        catch (Exception ex) { detail = ex.GetType().Name + ": " + ex.Message; return false; }
    }

    private static string HashFile(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder b = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) b.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return b.ToString();
        }
    }

    private static void Prune(string root)
    {
        List<ulong> checkpoints = EnumerateCheckpoints(root);
        while (checkpoints.Count > MaxCheckpoints)
        {
            ulong oldest = checkpoints[0];
            checkpoints.RemoveAt(0);
            string dir = ResolveCheckpointDirectory(root, oldest, false);
            try { if (!string.IsNullOrEmpty(dir)) Directory.Delete(dir, true); } catch { }
        }
    }
}

public sealed class RebirthNpcPersistenceStoreCommitResult
{
    public string StoreName { get; internal set; }
    public bool Succeeded { get; internal set; }
    public string Detail { get; internal set; }
}

/// <summary>Central ordering boundary for all durable NPC stores.</summary>
public static class RebirthNpcPersistenceCoordinator
{
    private static readonly object Sync = new object();
    // Many human AI tasks ask this every Unity frame. Cache only a missing
    // group; an actual group is always revalidated from disk. Every in-process
    // publication/retirement invalidates the negative observation. Its lifetime
    // is the exact World/save, not one frame: absence is stable until a writer
    // publishes. The recovery driver also reads disk independently.
    private static World absentRecoveryWorld;
    private static string absentRecoveryDirectory;
    internal static void InvalidateRecoveryAbsence()
    {
        lock(Sync){absentRecoveryWorld=null;absentRecoveryDirectory=null;}
    }
    [ThreadStatic] private static bool checkpointWrite;
    internal static bool IsCheckpointWrite => checkpointWrite;
    private sealed class RecoveryFlags
    {internal World World;internal string Directory;internal bool Active,Unresolved,ProjectionPending;internal RecoveryLease Owner;}
    private static readonly List<RecoveryFlags> RecoveryScopes=new List<RecoveryFlags>();
    private static RecoveryFlags Flags(World world,string directory,bool create)
    {
        lock(Sync)
        {
        // The common case has no recovery scope. Avoid normalizing the save
        // path on every GameUpdate merely to search an empty collection.
        if(!create&&RecoveryScopes.Count==0)return null;
        if(world==null||string.IsNullOrEmpty(directory))return null;
        directory=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        foreach(var scope in RecoveryScopes)if(ReferenceEquals(scope.World,world)&&string.Equals(scope.Directory,directory,StringComparison.OrdinalIgnoreCase))return scope;
        if(!create)return null;
        RecoveryScopes.RemoveAll(scope=>!scope.Active&&!scope.Unresolved&&!scope.ProjectionPending);
        if(RecoveryScopes.Count>=16)throw new InvalidOperationException("Retained NPC recovery-scope capacity; no original was evicted.");
        var next=new RecoveryFlags{World=world,Directory=directory};RecoveryScopes.Add(next);return next;
        }
    }
    private static RecoveryFlags CurrentFlags(bool create)
    {
        lock(Sync)if(!create&&RecoveryScopes.Count==0)return null;
        return Flags(GameManager.Instance?.World,RebirthNpcSaveScope.CurrentDirectory(),create);
    }
    private static bool recoveryActive=>CurrentFlags(false)?.Active==true;
    internal static bool HasActiveRecoveryWriterLease
    {get{lock(Sync){var scope=CurrentFlags(false);return scope?.Active==true&&scope.Owner!=null;}}}
    private static bool recoveryUnresolved=>CurrentFlags(false)?.Unresolved==true;
    private static bool recoveryProjectionPending=>CurrentFlags(false)?.ProjectionPending==true;
    internal static bool IsRecoveryInstalling
    {
        get
        {
            if(RebirthNpcPersistenceFile.HasQuarantineWriterFence||RebirthNpcLiveRecoveryRuntime.HasUnretiredForeignScope)return true;
            lock(Sync)if(recoveryActive||recoveryUnresolved)return true;
            World world=GameManager.Instance?.World;
            if(world==null)return false;
            try
            {
                string directory=RebirthNpcSaveScope.CurrentDirectory();if(string.IsNullOrEmpty(directory))return false;
                bool mainThread=ThreadManager.IsMainThread();
                if(mainThread)
                    lock(Sync)
                        if(ReferenceEquals(absentRecoveryWorld,world)&&
                            string.Equals(absentRecoveryDirectory,directory,StringComparison.Ordinal))return false;
                var group=RebirthNpcLiveRecoveryGroup.Read(directory);
                if(group==null)
                {
                    if(mainThread)
                        lock(Sync)
                        {absentRecoveryWorld=world;absentRecoveryDirectory=directory;}
                    return false;
                }
                if(group.Phase!=2)return true;
                foreach(var member in group.Members)if(!RebirthNpcRecoveryPublications.IsInstalled(Path.Combine(directory,member.File)))return true;
                return false;
            }
            catch{return true;}
        }
    }
    internal sealed class RecoveryLease:IDisposable
    {
        private readonly World world;private readonly string directory,fingerprint;private readonly RecoveryFlags flags;private bool disposed;
        internal RecoveryLease(World world,string directory,string fingerprint){this.world=world;this.directory=directory;this.fingerprint=fingerprint;flags=Flags(world,directory,true);flags.Owner=this;}
        internal bool IsCurrent
        {get{lock(Sync){if(disposed||!flags.Active||!ReferenceEquals(flags.Owner,this)||checkpointWrite||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||!RebirthNpcSaveQuarantineScope.SameDirectory(GameIO.GetSaveGameDir(),directory))return false;var scope=RebirthNpcSaveScope.ObserveCurrent();return scope!=null&&scope.Fingerprint==fingerprint&&scope.SaveDirectory==directory;}}}
        public void Dispose(){lock(Sync){if(disposed)return;disposed=true;if(ReferenceEquals(flags.Owner,this)){flags.Active=false;flags.Owner=null;}InvalidateRecoveryAbsence();}}
    }
    internal static bool TryAcquireRecovery(out RecoveryLease lease,out string error)
    {
        lease=null;error=string.Empty;
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true){error="recovery requires the authoritative game thread";return false;}
        if(RebirthNpcLiveRecoveryRuntime.HasUnretiredForeignScope||RebirthNpcRuntimeRegistry.HasForeignDomain){error="original NPC world requires typed retirement before recovery lease acquisition";return false;}
        lock(Sync)
        {
            if(checkpointWrite||recoveryActive){error="coordinated writer/recovery already active";return false;}
            var scope=RebirthNpcSaveScope.ObserveCurrent();if(scope==null||string.IsNullOrEmpty(scope.SaveDirectory)){error="recovery save scope unavailable";return false;}
            RebirthNpcRecoveryGeneration.InvalidateObservedGeneration();InvalidateRecoveryAbsence();CurrentFlags(true).Active=true;lease=new RecoveryLease(GameManager.Instance.World,scope.SaveDirectory,scope.Fingerprint);return true;
        }
    }
    internal static void SetRecoveryUnresolved(bool value,World world,string directory){lock(Sync){var flags=Flags(world,directory,value);if(flags!=null)flags.Unresolved=value;InvalidateRecoveryAbsence();}}
    internal static void SetRecoveryProjectionPending(bool value,World world,string directory){lock(Sync){var flags=Flags(world,directory,value);if(flags!=null)flags.ProjectionPending=value;}}
    public static bool LastSaveSucceeded => lastSaveSucceeded;
    private static long savePasses;
    private static long resetPasses;
    private static string lastError = string.Empty;
    private static bool lastSaveSucceeded = true;
#if REBIRTH_DEBUG
    internal static Action<string> BeforeCommitScenario;
#endif
    private static World lastSuccessfulSaveWorld;
    private static string lastSuccessfulSaveDirectory,lastSuccessfulSaveFingerprint;
    private static long failedSavePasses;
    private static readonly List<RebirthNpcPersistenceStoreCommitResult> LastCommitResults =
        new List<RebirthNpcPersistenceStoreCommitResult>();

    private sealed class PreSaveSnapshot
    {
        public string DirectoryPath;
        public string SaveDirectory;
        public World NativeWorld;
        public string[] Members;
        public readonly HashSet<string> PrimaryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> BackupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null || GameManager.Instance.World.IsRemote()) return;
        if(!RebirthNpcLiveRecoveryRuntime.BeforeOrdinaryLoad())throw new InvalidOperationException("NPC recovery local transaction must converge before ordinary loading.");
        RebirthNpcPersistenceTimelineService.EnsureAligned();
        RebirthNpcAggregatePersistenceStore.EnsureLoaded();
        RebirthNpcStableIdentityStore.EnsureLoaded();
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        RebirthNpcEquipmentPersistenceStore.EnsureLoaded();
        RebirthNpcExecutionPersistenceStore.EnsureLoaded();
        RebirthNpcSettlementPersistenceStore.EnsureLoaded();
        RebirthNpcWorkCheckpointPersistenceStore.EnsureLoaded();
        RebirthNpcWorkOutcomePersistenceStore.EnsureLoaded();
        RebirthNpcSocialPersistenceStore.EnsureLoaded();
        RebirthNpcCombatPersistenceStore.EnsureLoaded();
        RebirthNpcProgressionPersistenceStore.EnsureLoaded();
        RebirthNpcAdvancedProgressionPersistenceStore.EnsureLoaded();
        RebirthNpcLifecycleGameplayService.Load();
        RebirthNpcWorldIntegrationService.EnsurePersistenceLoaded();
        RebirthNpcEconomyPersistenceStore.Load();
    }

    // Force only the installed transaction's typed serializers. Unrelated failed
    // stores cannot deadlock recovery of this allow-listed member. Each write
    // advances its own durable same-generation publication receipt; partial
    // positive writes are retained, never rolled back to the initial backup.
    internal static bool TryFlushRecoveryMembers(Guid operation,out string error)
    {
        error=string.Empty;
        if(!ThreadManager.IsMainThread()||checkpointWrite||recoveryActive||recoveryUnresolved||GameManager.Instance?.World?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)
        {error="Recovery member checkpoint authority unavailable";return false;}
        lock(Sync)try
        {
            var group=RebirthNpcLiveRecoveryGroup.Read(GameIO.GetSaveGameDir());
            if(group==null||group.Operation!=operation||group.Phase!=2)throw new InvalidOperationException("Installed recovery group required");
            foreach(var member in group.Members)if(!RebirthNpcRecoveryPublications.IsInstalled(Path.Combine(group.Directory,member.File)))throw new InvalidOperationException("Member runtime generation is not installed");
            checkpointWrite=true;
            // World name projection precedes aggregate capture of those names.
            foreach(var member in group.Members)if(member.File=="RebirthNpcWorldIntegration.xml")RebirthNpcWorldIntegrationService.SaveIfDirty();
            foreach(var member in group.Members)
            {
                switch(member.File)
                {
                    case "RebirthNpcStableIdentities.xml":RebirthNpcStableIdentityStore.Save();break;
                    case "rebirth_npc_inventory.xml":RebirthNpcInventoryPersistenceStore.Save();break;
                    case "rebirth_npc_equipment.xml":RebirthNpcEquipmentPersistenceStore.Save();break;
                    case "RebirthNpcExecutionState.xml":RebirthNpcExecutionPersistenceStore.Save();break;
                    case "rebirth_npc_settlement.xml":RebirthNpcSettlementPersistenceStore.Save();break;
                    case "RebirthNpcWorkCheckpoints.xml":RebirthNpcWorkCheckpointPersistenceStore.Save();break;
                    case "RebirthNpcWorkOutcomes.xml":RebirthNpcWorkOutcomePersistenceStore.Save();break;
                    case "RebirthNpcSocialState.xml":RebirthNpcSocialPersistenceStore.Save();break;
                    case "RebirthNpcCombatState.xml":RebirthNpcCombatPersistenceStore.Save();break;
                    case "RebirthNpcPersistentRecords.xml":RebirthNpcAggregatePersistenceStore.Save();break;
                    case "RebirthNpcWorldIntegration.xml":break;
                    default:throw new InvalidDataException("Unknown recovery serializer");
                }
            }
            return true;
        }
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;return false;}
        finally{checkpointWrite=false;}
    }
    public static bool SaveAll(out string error)
    {
        if (!ThreadManager.IsMainThread() || RebirthNpcPersistenceFile.HasQuarantineWriterFence || checkpointWrite || recoveryActive || recoveryUnresolved || GameManager.Instance?.World == null || GameManager.Instance.World.IsRemote() ||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer != true)
        { error = "NPC coordinated save requires a non-reentrant authoritative world."; return false; }
        lock (Sync)
        {
            PreSaveSnapshot preSave = null;
            lastSaveSucceeded=false;lastSuccessfulSaveWorld=null;lastSuccessfulSaveDirectory=lastSuccessfulSaveFingerprint=null;
            try
            {
                // Capture the currently published sidecar set before any store writes.
                // If any later store or checkpoint publication fails, restore this set and
                // retain dirty in-memory state for a deterministic retry.
                LastCommitResults.Clear();
                EnsureLoaded(); // align/load this world's generation before taking its rollback copy
                preSave = CapturePreSaveSnapshot();
                // A checkpoint is a complete generation, unlike a periodic dirty-store tick.
                // Force every serializer on every attempt: file rollback cannot restore its
                // already-cleared in-memory dirty/version flag after a failed prior attempt.
                checkpointWrite = true;
                RebirthNpcItemIntegritySnapshot integrity = RebirthNpcItemIntegrityService.Audit();
                if (!integrity.IsValid)
                    Log.Warning("[REBIRTH NPC] Persistence save proceeding with item integrity violations: " + RebirthNpcItemIntegrityService.GetReport());
                RebirthNpcFarmingLogisticsQualificationSnapshot workIntegrity =
                    RebirthNpcFarmingLogisticsQualificationService.Audit();
                RebirthNpcProductionResourceQualificationSnapshot productionIntegrity =
                    RebirthNpcProductionResourceQualificationService.Audit();
                RebirthNpcDutyMedicalQualificationSnapshot dutyMedicalIntegrity =
                    RebirthNpcDutyMedicalQualificationService.Audit();
                if (!workIntegrity.IsValid)
                    Log.Warning("[REBIRTH NPC] Persistence save proceeding with farming/logistics qualification violations: " +
                        RebirthNpcFarmingLogisticsQualificationService.GetReport());
                if (!productionIntegrity.IsValid)
                    Log.Warning("[REBIRTH NPC] Persistence save proceeding with production/resource qualification violations: " +
                        RebirthNpcProductionResourceQualificationService.GetReport());
                if (!dutyMedicalIntegrity.IsValid)
                    Log.Warning("[REBIRTH NPC] Persistence save proceeding with duty/medical qualification violations: " +
                        RebirthNpcDutyMedicalQualificationService.GetReport());
                // Aggregate capture inventories the complete NPC-level envelope before domain writes.
                RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes();
                // Identity precedes records that reference stable ids; outcomes/checkpoints
                // follow execution and settlement state so no completed mutation is omitted.
                LastCommitResults.Clear();
                CommitStore("stable-identity", RebirthNpcStableIdentityStore.Save);
                CommitStore("inventory", RebirthNpcInventoryPersistenceStore.Save);
                CommitStore("equipment", RebirthNpcEquipmentPersistenceStore.Save);
                CommitStore("execution", RebirthNpcExecutionPersistenceStore.Save);
                CommitStore("settlement", RebirthNpcSettlementPersistenceStore.Save);
                CommitStore("work-checkpoint", RebirthNpcWorkCheckpointPersistenceStore.Save);
                CommitStore("work-outcome", RebirthNpcWorkOutcomePersistenceStore.Save);
                CommitStore("social", RebirthNpcSocialPersistenceStore.Save);
                CommitStore("combat", RebirthNpcCombatPersistenceStore.Save);
                CommitStore("progression", RebirthNpcProgressionPersistenceStore.Save);
                CommitStore("advanced-progression", RebirthNpcAdvancedProgressionPersistenceStore.Save);
                CommitStore("identity-audit", RebirthNpcIdentityAuditLedger.SaveCheckpoint);
                CommitStore("lifecycle-gameplay", RebirthNpcLifecycleGameplayService.Save);
                CommitStore("world-integration", RebirthNpcWorldIntegrationService.SaveIfDirty);
                CommitStore("economy-offline", RebirthNpcEconomyPersistenceStore.SaveIfDirty);
                CommitStore("external-transactions", RebirthNpcExternalInventoryTransferCoordinator.SaveCheckpoint);
                // Aggregate domain hashes must describe this final sidecar generation.
                CommitStore("aggregate", RebirthNpcAggregatePersistenceStore.Save);
                CommitStore("timeline-checkpoint", RebirthNpcPersistenceTimelineService.CheckpointAll);
                var completedScope=RebirthNpcSaveScope.ObserveCurrent();
                if(completedScope==null||!ReferenceEquals(GameManager.Instance?.World,preSave.NativeWorld)||
                    Path.GetFullPath(GameIO.GetSaveGameDir())!=Path.GetFullPath(preSave.SaveDirectory)||
                    completedScope.Fingerprint!=RebirthNpcSaveScope.ComputeFingerprint(preSave.SaveDirectory))throw new InvalidOperationException("NPC native save scope changed during coordinated publication.");
                savePasses++;
                lastSaveSucceeded = true;
                lastSuccessfulSaveWorld=preSave.NativeWorld;lastSuccessfulSaveDirectory=Path.GetFullPath(preSave.SaveDirectory);
                lastSuccessfulSaveFingerprint=completedScope.Fingerprint;
                lastError = string.Empty;
                error = string.Empty;
                CleanupPreSaveSnapshot(preSave);
                preSave = null;
                // Observation failure does not undo a positively committed save.
                // It refuses later quarantine admission rather than guessing old
                // custody from the newly selected world's paths.
                if(!RebirthNpcSaveQuarantineCapture.RememberCurrent(out var transitionError))
                    Log.Warning("[REBIRTH NPC] Original save transition capture retained unavailable: "+transitionError);
                return true;
            }
            catch (Exception ex)
            {
                lastSaveSucceeded = false;
                failedSavePasses++;
                string rollbackError = string.Empty;
                bool restored = preSave == null || TryRestorePreSaveSnapshot(preSave, out rollbackError);
                if (restored) CleanupPreSaveSnapshot(preSave);
                else rollbackError = "; file-generation rollback incomplete: " + rollbackError +
                    "; recovery snapshot retained at " + preSave.DirectoryPath;
                lastError = ex.GetType().Name + ": " + ex.Message + rollbackError;
                error = lastError;
                Log.Warning("[REBIRTH NPC] Persistence save pass failed; in-memory store state remains retained for retry: " + lastError);
                return false;
            }
            finally { checkpointWrite = false; }
        }
    }

    public static RebirthNpcPersistenceStoreCommitResult[] GetLastCommitResults()
    {
        lock (Sync)
        {
            RebirthNpcPersistenceStoreCommitResult[] copy = new RebirthNpcPersistenceStoreCommitResult[LastCommitResults.Count];
            for (int i = 0; i < LastCommitResults.Count; i++)
            {
                RebirthNpcPersistenceStoreCommitResult source = LastCommitResults[i];
                copy[i] = new RebirthNpcPersistenceStoreCommitResult
                {
                    StoreName = source.StoreName,
                    Succeeded = source.Succeeded,
                    Detail = source.Detail
                };
            }
            return copy;
        }
    }

    private static PreSaveSnapshot CapturePreSaveSnapshot()
    {
        string saveDir = GameIO.GetSaveGameDir();
        if (string.IsNullOrEmpty(saveDir))
            throw new InvalidOperationException("NPC persistence save path is unavailable.");
        string snapshotDir = Path.Combine(saveDir, ".rebirth-npc-presave-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(snapshotDir);
        PreSaveSnapshot snapshot = new PreSaveSnapshot { DirectoryPath = snapshotDir,SaveDirectory=saveDir,NativeWorld=GameManager.Instance?.World };
        try
        {
            var members=new List<string>(RebirthNpcPersistenceTimelineService.GetSidecarNames());
            foreach(string receipt in RebirthNpcRecoveryPublications.CheckpointReceiptNames())if(!members.Contains(receipt))members.Add(receipt);
            string[] names = snapshot.Members=members.ToArray();
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                string primary = Path.Combine(saveDir, name);
                string backup = primary + ".bak";
                if (File.Exists(primary))
                {
                    snapshot.PrimaryNames.Add(name);
                    File.Copy(primary, Path.Combine(snapshotDir, name), false);
                }
                if (File.Exists(backup))
                {
                    snapshot.BackupNames.Add(name);
                    File.Copy(backup, Path.Combine(snapshotDir, name + ".bak"), false);
                }
            }
            return snapshot;
        }
        catch
        {
            CleanupPreSaveSnapshot(snapshot);
            throw;
        }
    }

    private static bool TryRestorePreSaveSnapshot(PreSaveSnapshot snapshot, out string error)
    {
        error = string.Empty;
        if (snapshot == null || string.IsNullOrEmpty(snapshot.DirectoryPath)) return true;
        string saveDir = GameIO.GetSaveGameDir();
        if (string.IsNullOrEmpty(saveDir))
        {
            error = "save path unavailable during rollback";
            return false;
        }
        if(!ReferenceEquals(GameManager.Instance?.World,snapshot.NativeWorld)||!string.Equals(saveDir,snapshot.SaveDirectory,StringComparison.OrdinalIgnoreCase))
        {error="original pre-save world/directory changed; rollback evidence retained";return false;}
        List<string> failures = new List<string>();
        string[] names = snapshot.Members??RebirthNpcPersistenceTimelineService.GetSidecarNames();
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            RestoreSnapshotMember(snapshot, name, false, Path.Combine(saveDir, name), failures);
            RestoreSnapshotMember(snapshot, name, true, Path.Combine(saveDir, name + ".bak"), failures);
        }
        if (failures.Count == 0) return true;
        error = string.Join(" | ", failures.ToArray());
        return false;
    }

    private static void RestoreSnapshotMember(PreSaveSnapshot snapshot, string name, bool backup,
        string destination, List<string> failures)
    {
        try
        {
            bool existed = backup ? snapshot.BackupNames.Contains(name) : snapshot.PrimaryNames.Contains(name);
            string source = Path.Combine(snapshot.DirectoryPath, name + (backup ? ".bak" : string.Empty));
            if (!existed)
            {
                if (File.Exists(destination)) File.Delete(destination);
                return;
            }
            if (!File.Exists(source)) throw new FileNotFoundException("pre-save snapshot member missing", source);
            string temp = destination + ".presave-rollback."+Guid.NewGuid().ToString("N")+".tmp";
            using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {input.CopyTo(output,65536);output.Flush(true);}
            if(File.Exists(destination))File.Replace(temp,destination,null,true);else File.Move(temp,destination);
        }
        catch (Exception ex)
        {
            failures.Add(Path.GetFileName(destination) + "=" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private static void CleanupPreSaveSnapshot(PreSaveSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrEmpty(snapshot.DirectoryPath)) return;
        try { if (Directory.Exists(snapshot.DirectoryPath)) Directory.Delete(snapshot.DirectoryPath, true); }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH NPC] Failed to remove pre-save rollback snapshot: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void CommitStore(string name, Action save)
    {
        try
        {
#if REBIRTH_DEBUG
            if(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")BeforeCommitScenario?.Invoke(name);
#endif
            save();
            LastCommitResults.Add(new RebirthNpcPersistenceStoreCommitResult
            {
                StoreName = name ?? string.Empty,
                Succeeded = true,
                Detail = "save call completed without an error"
            });
        }
        catch (Exception ex)
        {
            LastCommitResults.Add(new RebirthNpcPersistenceStoreCommitResult
            {
                StoreName = name ?? string.Empty,
                Succeeded = false,
                Detail = ex.GetType().Name + ": " + ex.Message
            });
            throw;
        }
    }

    public static bool ResetAfterSave()
        =>TryResetAfterSaveFor(lastSuccessfulSaveWorld,lastSuccessfulSaveDirectory);
    public static bool TryResetAfterSaveFor(World original,string saveDirectory)
    {
        lock (Sync)
        {
            if(!ThreadManager.IsMainThread()||original==null||string.IsNullOrEmpty(saveDirectory)||
                !ReferenceEquals(original,lastSuccessfulSaveWorld)||!ReferenceEquals(GameManager.Instance?.World,original)||
                Path.GetFullPath(saveDirectory)!=lastSuccessfulSaveDirectory||Path.GetFullPath(GameIO.GetSaveGameDir())!=lastSuccessfulSaveDirectory)
            {lastError="NPC reset requires the exact original coordinated save scope.";return false;}
            var scope=RebirthNpcSaveScope.ObserveCurrent();
            if(scope==null||scope.Fingerprint!=lastSuccessfulSaveFingerprint)
            {lastError="NPC reset save identity changed after coordinated publication.";return false;}
            if(recoveryActive||recoveryUnresolved||recoveryProjectionPending){lastError="NPC recovery convergence is pending; retirement refused.";return false;}
            if (!lastSaveSucceeded)
            {
                Log.Warning("[REBIRTH NPC] Refusing ResetAfterSave because the last coordinated save failed; retryable in-memory state is retained.");
                return false;
            }
            if(!PrepareBackingStoreRetirement())return false;
            RebirthNpcRecoveryGeneration.InvalidateObservedGeneration();
            RebirthNpcExternalInventoryTransferCoordinator.Reset();
            RebirthNpcInventoryPersistenceStore.Reset(false);
            RebirthNpcEquipmentPersistenceStore.Reset(false);
            RebirthNpcExecutionPersistenceStore.Reset(false);
            RebirthNpcSettlementPersistenceStore.Reset(false);
            RebirthNpcStableIdentityStore.Reset(false);
            RebirthNpcWorkCheckpointPersistenceStore.Reset(false);
            RebirthNpcWorkOutcomePersistenceStore.Reset(false);
            if(!RebirthNpcSocialPersistenceStore.TryReset(false))throw new InvalidOperationException("Social retirement changed after preflight.");
            RebirthNpcCombatPersistenceStore.Reset(false);
            if(!RebirthNpcProgressionPersistenceStore.TryReset(false))throw new InvalidOperationException("Progression retirement changed after preflight.");
            if(!RebirthNpcAdvancedProgressionPersistenceStore.TryReset(false))throw new InvalidOperationException("Advanced progression retirement changed after preflight.");
            if(!RebirthNpcEconomyPersistenceStore.TryReset(false))throw new InvalidOperationException("Economy retirement changed after preflight.");
            RebirthNpcAggregatePersistenceStore.Reset(false);
            if(!RebirthNpcLifecycleGameplayService.TryResetLifecycle(false))throw new InvalidOperationException("Lifecycle original retirement changed after preflight.");
            if(!RebirthNpcWorldIntegrationService.TryResetWorldBacking(false))throw new InvalidOperationException("World identity original retirement changed after preflight.");
            if(!RebirthNpcIdentityAuditLedger.TryResetAuditBacking(false))throw new InvalidOperationException("Identity audit original retirement changed after preflight.");
            if(!RebirthNpcHeldDutyMedicalRetirement.TryRetireEmpty()||!RebirthNpcEquipmentProjectionReceipts.TryRetireEmptyReceiptObservation()||!RebirthNpcWorkAssignmentService.TryRetireEmptyAssignmentObservation()||!RebirthNpcNavigationRuntimeAdapter.TryRetireEmptyNavigationObservation()||!RebirthNpcWorkExecutorRegistry.TryRetireEmptyResolutionObservation()||!RebirthNpcWorkTargetRegistry.TryRetireEmptyTargetObservation()||!RebirthNpcWorkExecutorHealthRegistry.TryRetireEmptyHealthObservation()||!RebirthNpcWorkRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcPatrolRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcStationaryActivityRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcMissionRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcFollowRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcTravelRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcGuardNavigationRuntime.TryRetireEmptyControllerObservation())throw new InvalidOperationException("Original native controller observation changed after preflight.");
            if(!RebirthNpcActivityRegistry.TryRetireEmptyActivityObservation())throw new InvalidOperationException("Activity observation changed after retirement preflight.");
            if(!RebirthNpcLiveRecoveryRuntime.TryRetireEmptyDriver())throw new InvalidOperationException("Recovery driver original retirement changed after preflight.");
            resetPasses++;
            lastSuccessfulSaveWorld=null;lastSuccessfulSaveDirectory=lastSuccessfulSaveFingerprint=null;
            return true;
        }
    }

    /// <summary>
    /// Retire only positively matched original backing caches. If a different
    /// native World/save is already active, typed original leases may flush only
    /// their captured old path; unknown custody refuses before any cache clears.
    /// </summary>
    public static void ResetForUnexpectedWorldBoundary()
    {
        lock (Sync)
        {
            if(recoveryActive||recoveryUnresolved||recoveryProjectionPending)throw new InvalidOperationException("NPC recovery convergence is pending; original-world state retained.");
            // Each store flushes its original bound path, never the new GameIO path.
            // Refusal must stop the caller before it clears any backing service.
            if(!PrepareBackingStoreRetirement())throw new InvalidOperationException("NPC original-world stores remain unsettled; new-world admission refused.");
            RebirthNpcRecoveryGeneration.InvalidateObservedGeneration();
            RebirthNpcExternalInventoryTransferCoordinator.Reset();
            RebirthNpcInventoryPersistenceStore.Reset(false);
            RebirthNpcEquipmentPersistenceStore.Reset(false);
            RebirthNpcExecutionPersistenceStore.Reset(false);
            RebirthNpcSettlementPersistenceStore.Reset(false);
            RebirthNpcStableIdentityStore.Reset(false);
            RebirthNpcWorkCheckpointPersistenceStore.Reset(false);
            RebirthNpcWorkOutcomePersistenceStore.Reset(false);
            if(!RebirthNpcSocialPersistenceStore.TryReset(false))throw new InvalidOperationException("Social retirement changed after preflight.");
            RebirthNpcCombatPersistenceStore.Reset(false);
            if(!RebirthNpcProgressionPersistenceStore.TryReset(false))throw new InvalidOperationException("Progression retirement changed after preflight.");
            if(!RebirthNpcAdvancedProgressionPersistenceStore.TryReset(false))throw new InvalidOperationException("Advanced progression retirement changed after preflight.");
            if(!RebirthNpcEconomyPersistenceStore.TryReset(false))throw new InvalidOperationException("Economy retirement changed after preflight.");
            RebirthNpcAggregatePersistenceStore.Reset(false);
            if(!RebirthNpcLifecycleGameplayService.TryResetLifecycle(false))throw new InvalidOperationException("Lifecycle original retirement changed after preflight.");
            if(!RebirthNpcWorldIntegrationService.TryResetWorldBacking(false))throw new InvalidOperationException("World identity original retirement changed after preflight.");
            if(!RebirthNpcIdentityAuditLedger.TryResetAuditBacking(false))throw new InvalidOperationException("Identity audit original retirement changed after preflight.");
            if(!RebirthNpcHeldDutyMedicalRetirement.TryRetireEmpty()||!RebirthNpcEquipmentProjectionReceipts.TryRetireEmptyReceiptObservation()||!RebirthNpcWorkAssignmentService.TryRetireEmptyAssignmentObservation()||!RebirthNpcNavigationRuntimeAdapter.TryRetireEmptyNavigationObservation()||!RebirthNpcWorkExecutorRegistry.TryRetireEmptyResolutionObservation()||!RebirthNpcWorkTargetRegistry.TryRetireEmptyTargetObservation()||!RebirthNpcWorkExecutorHealthRegistry.TryRetireEmptyHealthObservation()||!RebirthNpcWorkRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcPatrolRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcStationaryActivityRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcMissionRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcFollowRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcTravelRuntime.TryRetireEmptyControllerObservation()||!RebirthNpcGuardNavigationRuntime.TryRetireEmptyControllerObservation())throw new InvalidOperationException("Original native controller observation changed after preflight.");
            if(!RebirthNpcActivityRegistry.TryRetireEmptyActivityObservation())throw new InvalidOperationException("Activity observation changed after retirement preflight.");
            if(!RebirthNpcLiveRecoveryRuntime.TryRetireEmptyDriver())throw new InvalidOperationException("Recovery driver original retirement changed after preflight.");
            lastSaveSucceeded = true;
            lastError = string.Empty;
            resetPasses++;
        }
    }

    private static bool PrepareBackingStoreRetirement()
    {
        // Evaluate every flush even when a previous domain failed, then preflight
        // all resets before the first destructive cache operation.
        bool social=TryRetirementFlush(RebirthNpcSocialPersistenceStore.TryFlush);
        bool progression=TryRetirementFlush(RebirthNpcProgressionPersistenceStore.TryFlush);
        bool advanced=TryRetirementFlush(RebirthNpcAdvancedProgressionPersistenceStore.TryFlush);
        bool economy=TryRetirementFlush(RebirthNpcEconomyPersistenceStore.TryFlush);
        bool inventory=TryRetirementFlush(RebirthNpcInventoryPersistenceStore.TryFlushOriginalBacking);
        bool equipment=TryRetirementFlush(RebirthNpcEquipmentPersistenceStore.TryFlushOriginalBacking);
        bool combat=TryRetirementFlush(RebirthNpcCombatPersistenceStore.TryFlushOriginalBacking);
        bool settlement=TryRetirementFlush(RebirthNpcSettlementPersistenceStore.TryFlushOriginalBacking);
        bool lifecycle=TryRetirementFlush(RebirthNpcLifecycleGameplayService.TryFlushOriginalLifecycle);
        bool worldIdentity=TryRetirementFlush(RebirthNpcWorldIntegrationService.TryFlushOriginalWorldBacking);
        bool identityAudit=TryRetirementFlush(RebirthNpcIdentityAuditLedger.TryFlushAuditBacking);
        bool projections=RebirthNpcProgressionClientCache.CanRetireEmptyProgression&&RebirthNpcProgressionProjectionRevisionService.CanRetireEmptyRevisions&&RebirthNpcLiveRecoveryRuntime.CanRetireEmptyDriver&&RebirthNpcNativeEquipmentBridge.CanRetireEmptyProjection&&RebirthNpcRecoveryProjectionNetwork.CanRetireEmptyMailbox&&RebirthNpcPendingRuntimeStates.CanRetireEmptyProjection&&RebirthNpcPendingEquipmentSnapshots.CanRetireEmptyProjection&&RebirthNpcPendingAppearanceSnapshots.CanRetireEmptyProjection&&RebirthNpcInteractionNetworkClient.CanRetireEmptyCallbacks;
        bool ready=RebirthNpcGuardDutyWorkPackageService.CanRetireEmptyGuardPackage&&RebirthNpcMedicalTreatmentWorkPackageService.CanRetireEmptyMedicalPackage&&RebirthNpcGuardPostWorkAdapter.CanRetireEmptyNative&&RebirthNpcMedicalTreatmentAdapter.CanRetireEmptyNative&&RebirthNpcWorkAssignmentService.CanRetireEmptyAssignmentRecords&&RebirthNpcNetworkRetirementPreflight.CanReset&&RebirthNpcWorkReservationService.CanRetireEmptyReservations&&RebirthNpcWorkAssignmentCoordinator.CanRetireEmptyAssignmentCoordinator&&RebirthNpcWorkstationExecutionService.CanRetireEmptyWorkstations&&RebirthNpcScaleHardeningService.CanRetireScaleDomains&&RebirthNpcNavigationRuntimeAdapter.CanRetireEmptyNavigationRuntime&&RebirthNpcWorkRuntime.CanRetireEmptyController&&RebirthNpcWorkExecutorRegistry.CanRetireEmptyResolution&&RebirthNpcWorkTargetRegistry.CanRetireEmptyTargets&&RebirthNpcWorkExecutorHealthRegistry.CanRetireEmptyHealth&&RebirthNpcPatrolRuntime.CanRetireEmptyController&&RebirthNpcStationaryActivityRuntime.CanRetireEmptyController&&RebirthNpcMissionRuntime.CanRetireEmptyController&&RebirthNpcFollowRuntime.CanRetireEmptyController&&RebirthNpcTravelRuntime.CanRetireEmptyController&&RebirthNpcGuardNavigationRuntime.CanRetireEmptyController&&RebirthDogNavigationMarkerService.CanRetireEmptyMarkers&&RebirthCompanionSnapshotService.CanRetireEmptySnapshots&&RebirthCompanionBehaviorService.CanRetireEmptyBehavior&&RebirthCompanionInventoryLockService.CanRetireEmptyItemLocks&&RebirthDogAutoLootBridge.CanRetireEmptyAutoLoot&&RebirthDogCapacitySyncService.CanRetireEmptyCapacity&&RebirthDogLifecycleService.CanRetireEmptyGhostRetries&&RebirthDogRuntimeService.CanRetireEmptyDogRuntime&&RebirthDogChunkObserverService.CanRetireEmptyObservers&&RebirthDogStorageService.CanRetireEmptyStorage&&RebirthNpcTradeService.CanRetireEmptyTrade&&RebirthNpcDialogueService.CanRetireEmptyDialogue&&RebirthNpcWorkNavigationService.CanRetireEmptyNavigation&&RebirthNpcDecisionEngine.CanRetireEmptyDecision&&RebirthNpcDecisionSignalStore.CanRetireEmptySignals&&RebirthNpcActivityRegistry.CanRetireEmptyActivities&&RebirthNpcRemoteCommandService.CanRetireEmptyRemoteCommands&&RebirthNpcActivityRecoveryRegistry.CanRetireEmptyActivityRecovery&&RebirthNpcCommandGateway.CanRetireEmptyCommands&&RebirthNpcPersistenceTimelineService.CanRetireTimeline&&projections&&identityAudit&&RebirthNpcIdentityAuditLedger.CanRetireAuditBacking&&worldIdentity&&RebirthNpcWorldIntegrationService.CanRetireWorldBacking&&lifecycle&&RebirthNpcLifecycleGameplayService.CanRetireLifecycleBacking&&social&&progression&&advanced&&economy&&inventory&&equipment&&combat&&settlement&&RebirthNpcWorkCheckpointPersistenceStore.CanRetireBacking&&RebirthNpcExecutionPersistenceStore.CanRetireBacking&&RebirthNpcStableIdentityStore.CanRetireBacking&&RebirthNpcAggregatePersistenceStore.CanRetireBacking&&RebirthNpcWorkOutcomePersistenceStore.CanRetireBacking&&RebirthNpcSettlementPersistenceStore.CanRetireBacking&&RebirthNpcCombatPersistenceStore.CanRetireBacking&&RebirthNpcSocialPersistenceStore.CanReset&&
            RebirthNpcProgressionPersistenceStore.CanReset&&RebirthNpcAdvancedProgressionPersistenceStore.CanReset&&RebirthNpcEconomyPersistenceStore.CanReset&&
            RebirthNpcInventoryPersistenceStore.CanRetireBacking&&RebirthNpcEquipmentPersistenceStore.CanRetireBacking;
        if(!ready){lastSaveSucceeded=false;lastError="Backing store retirement refused; original state retained.";}
        return ready;
    }

    private static bool TryRetirementFlush(Func<bool> flush)
    {
        try{return flush();}
        catch(Exception error){Log.Warning("[REBIRTH NPC] Original-world flush refused: "+error.GetType().Name);return false;}
    }

    public static RebirthNpcPersistenceFileSnapshot[] CaptureFileSnapshots()
    {
        string dir = GameIO.GetSaveGameDir();
        if (string.IsNullOrEmpty(dir)) return new RebirthNpcPersistenceFileSnapshot[0];
        string[] names = RebirthNpcPersistenceTimelineService.GetSidecarNames();
        List<RebirthNpcPersistenceFileSnapshot> result = new List<RebirthNpcPersistenceFileSnapshot>();
        for (int i = 0; i < names.Length; i++)
        {
            string path = Path.Combine(dir, names[i]);
            string backup = path + ".bak";
            result.Add(new RebirthNpcPersistenceFileSnapshot {
                FileName = names[i], Exists = File.Exists(path), Length = GetLength(path),
                Sha256 = File.Exists(path) ? Hash(path) : string.Empty,
                BackupExists = File.Exists(backup), BackupLength = GetLength(backup)
            });
        }
        return result.ToArray();
    }

    public static string GetReport()
    {
        StringBuilder b = new StringBuilder();
        b.Append("[REBIRTH NPC Persistence Coordinator] savePasses=").Append(savePasses)
         .Append(" resetPasses=").Append(resetPasses)
         .Append(" failedSavePasses=").Append(failedSavePasses)
         .Append(" retryStateRetained=").Append(!lastSaveSucceeded)
         .Append(" recoveryInstalling=").Append(recoveryActive||recoveryUnresolved)
         .Append(" recoveryProjectionPending=").Append(recoveryProjectionPending)
         .Append(" lastError=").Append(string.IsNullOrEmpty(lastError) ? "none" : lastError).AppendLine();
        RebirthNpcPersistenceFileSnapshot[] files = CaptureFileSnapshots();
        long total = 0;
        for (int i = 0; i < files.Length; i++)
        {
            total += files[i].Length;
            b.Append("  ").Append(files[i].FileName).Append(" exists=").Append(files[i].Exists)
             .Append(" bytes=").Append(files[i].Length.ToString(CultureInfo.InvariantCulture))
             .Append(" backup=").Append(files[i].BackupExists)
             .Append(" backupBytes=").Append(files[i].BackupLength.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(files[i].Sha256)) b.Append(" sha256=").Append(files[i].Sha256);
            b.AppendLine();
        }
        b.Append("  totalPrimaryBytes=").Append(total.ToString(CultureInfo.InvariantCulture)).AppendLine();
        b.Append(RebirthNpcPersistenceTimelineService.GetReport());
        return b.ToString();
    }

    private static long GetLength(string path) { try { return File.Exists(path) ? new FileInfo(path).Length : 0L; } catch { return 0L; } }
    private static string Hash(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder b = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) b.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return b.ToString();
        }
    }
}
