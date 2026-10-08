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
public static class RebirthNpcPersistenceTimelineService
{
    private const string TimelineDirectoryName = "RebirthNpcTimeline";
    private const int MaxCheckpoints = 16;
    private static readonly object TimelineSync = new object();
    private static bool alignmentAttempted;
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

    public static void EnsureAligned()
    {
        lock (TimelineSync)
        {
            if (alignmentAttempted) return;

            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            string saveDir = GameIO.GetSaveGameDir();
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
                lastDetail = "rollback detected but no checkpoint exists at/before restored world time; sidecars left untouched for manual recovery";
                Log.Warning("[REBIRTH NPC Timeline] " + lastDetail
                    + " worldTime=" + currentWorldTime.ToString(CultureInfo.InvariantCulture)
                    + " newestCheckpoint=" + newestCheckpoint.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (candidate == 0UL && currentWorldTime == 0UL)
            {
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
                lastDetail = checkpointForeign
                    ? "rollback checkpoint belongs to another save scope; restore refused"
                    : "unscoped legacy rollback checkpoint refused because save provenance is unknown";
                Log.Warning("[REBIRTH NPC Timeline] " + lastDetail
                    + " worldTime=" + currentWorldTime.ToString(CultureInfo.InvariantCulture)
                    + " checkpoint=" + candidate.ToString(CultureInfo.InvariantCulture));
                return;
            }

            RestoreCheckpoint(saveDir, root, candidate);
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
                + " restoredCheckpoint=" + restoredCheckpoint.ToString(CultureInfo.InvariantCulture)
                + " detail=" + lastDetail;
        }
    }

    public static void ResetForWorld()
    {
        lock (TimelineSync)
        {
            alignmentAttempted = false;
            rollbackDetected = false;
            currentWorldTime = 0UL;
            newestCheckpoint = 0UL;
            restoredCheckpoint = 0UL;
            lastDetail = "not checked";
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
        try
        {
            for (int i = 0; i < SidecarNames.Length; i++)
            {
                string name = SidecarNames[i];
                string src = Path.Combine(checkpoint, name);
                string staged = Path.Combine(staging, name);
                if (!File.Exists(src)) throw new InvalidDataException("restore source missing: " + name);
                File.Copy(src, staged, false);
                if (!string.Equals(HashFile(src), HashFile(staged), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("restore staging hash mismatch: " + name);

                string dst = Path.Combine(saveDir, name);
                string bak = dst + ".bak";
                if (File.Exists(dst))
                {
                    priorPrimary.Add(name);
                    File.Copy(dst, Path.Combine(rollback, name), false);
                }
                if (File.Exists(bak))
                {
                    priorBackup.Add(name);
                    File.Copy(bak, Path.Combine(rollback, name + ".bak"), false);
                }
            }

            // Publish all primaries first, then advance backups to the same generation.
            // Any exception rolls the complete set back to its exact pre-restore membership.
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
        }
        catch (Exception restoreFailure)
        {
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
                throw new IOException("NPC checkpoint restore failed (" + restoreFailure.GetType().Name +
                    ") and rollback was incomplete: " + string.Join(" | ", rollbackErrors.ToArray()), restoreFailure);
            throw;
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
            try { if (Directory.Exists(rollback)) Directory.Delete(rollback, true); } catch { }
        }
    }

    private static void PublishRestoredFile(string source, string destination)
    {
        string temp = destination + ".restore.tmp";
        if (File.Exists(temp)) File.Delete(temp);
        File.Copy(source, temp, false);
        if (!string.Equals(HashFile(source), HashFile(temp), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("restore publication hash mismatch: " + Path.GetFileName(destination));
        if (File.Exists(destination)) File.Delete(destination);
        File.Move(temp, destination);
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
    [ThreadStatic] private static bool checkpointWrite;
    internal static bool IsCheckpointWrite => checkpointWrite;
    public static bool LastSaveSucceeded => lastSaveSucceeded;
    private static long savePasses;
    private static long resetPasses;
    private static string lastError = string.Empty;
    private static bool lastSaveSucceeded = true;
    private static long failedSavePasses;
    private static readonly List<RebirthNpcPersistenceStoreCommitResult> LastCommitResults =
        new List<RebirthNpcPersistenceStoreCommitResult>();

    private sealed class PreSaveSnapshot
    {
        public string DirectoryPath;
        public readonly HashSet<string> PrimaryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> BackupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null || GameManager.Instance.World.IsRemote()) return;
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

    public static bool SaveAll(out string error)
    {
        if (checkpointWrite || GameManager.Instance?.World == null || GameManager.Instance.World.IsRemote() ||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer != true)
        { error = "NPC coordinated save requires a non-reentrant authoritative world."; return false; }
        lock (Sync)
        {
            PreSaveSnapshot preSave = null;
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
                savePasses++;
                lastSaveSucceeded = true;
                lastError = string.Empty;
                error = string.Empty;
                CleanupPreSaveSnapshot(preSave);
                preSave = null;
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
        PreSaveSnapshot snapshot = new PreSaveSnapshot { DirectoryPath = snapshotDir };
        try
        {
            string[] names = RebirthNpcPersistenceTimelineService.GetSidecarNames();
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
        List<string> failures = new List<string>();
        string[] names = RebirthNpcPersistenceTimelineService.GetSidecarNames();
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
            string temp = destination + ".presave-rollback.tmp";
            if (File.Exists(temp)) File.Delete(temp);
            File.Copy(source, temp, false);
            if (File.Exists(destination)) File.Delete(destination);
            File.Move(temp, destination);
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
    {
        lock (Sync)
        {
            if (!lastSaveSucceeded)
            {
                Log.Warning("[REBIRTH NPC] Refusing ResetAfterSave because the last coordinated save failed; retryable in-memory state is retained.");
                return false;
            }
            RebirthNpcExternalInventoryTransferCoordinator.Reset();
            RebirthNpcInventoryPersistenceStore.Reset(false);
            RebirthNpcEquipmentPersistenceStore.Reset(false);
            RebirthNpcExecutionPersistenceStore.Reset(false);
            RebirthNpcSettlementPersistenceStore.Reset(false);
            RebirthNpcStableIdentityStore.Reset(false);
            RebirthNpcWorkCheckpointPersistenceStore.Reset(false);
            RebirthNpcWorkOutcomePersistenceStore.Reset(false);
            RebirthNpcSocialPersistenceStore.Reset(true);
            RebirthNpcCombatPersistenceStore.Reset(false);
            RebirthNpcProgressionPersistenceStore.Reset(false);
            RebirthNpcAdvancedProgressionPersistenceStore.Reset(false);
            RebirthNpcAggregatePersistenceStore.Reset(false);
            resetPasses++;
            return true;
        }
    }

    /// <summary>
    /// Hard-discard every save-scoped persistence cache without writing anything.
    /// This is only for the exceptional case where a different World/save is already
    /// active and the previous world's shutdown callback did not finish. At that point
    /// GameIO may already target the new save, so even a normally-safe Reset(true) would
    /// risk writing stale records into the wrong save.
    /// </summary>
    public static void ResetForUnexpectedWorldBoundary()
    {
        lock (Sync)
        {
            RebirthNpcExternalInventoryTransferCoordinator.Reset();
            RebirthNpcInventoryPersistenceStore.Reset(false);
            RebirthNpcEquipmentPersistenceStore.Reset(false);
            RebirthNpcExecutionPersistenceStore.Reset(false);
            RebirthNpcSettlementPersistenceStore.Reset(false);
            RebirthNpcStableIdentityStore.Reset(false);
            RebirthNpcWorkCheckpointPersistenceStore.Reset(false);
            RebirthNpcWorkOutcomePersistenceStore.Reset(false);
            RebirthNpcSocialPersistenceStore.Reset(true);
            RebirthNpcCombatPersistenceStore.Reset(false);
            RebirthNpcProgressionPersistenceStore.Reset(false);
            RebirthNpcAdvancedProgressionPersistenceStore.Reset(false);
            RebirthNpcAggregatePersistenceStore.Reset(false);
            lastSaveSucceeded = true;
            lastError = string.Empty;
            resetPasses++;
        }
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
