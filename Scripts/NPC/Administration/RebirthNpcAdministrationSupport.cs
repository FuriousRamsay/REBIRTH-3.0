using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

#nullable disable

public enum RebirthNpcAdminPermission : byte
{
    Inspect = 0,
    Export = 1,
    Repair = 2
}

public sealed class RebirthNpcAdminAuditEntry
{
    public long Sequence;
    public long UtcTicks;
    public string Actor;
    public string Operation;
    public string Target;
    public bool Succeeded;
    public string Detail;
}

/// <summary>Bounded process audit trail for WP19 administrative actions.</summary>
public static class RebirthNpcAdminAuditLedger
{
    private const int Capacity = 512;
    private static readonly object Sync = new object();
    private static readonly Queue<RebirthNpcAdminAuditEntry> Entries = new Queue<RebirthNpcAdminAuditEntry>();
    private static long sequence;

    public static void Append(string actor, string operation, string target, bool succeeded, string detail)
    {
        lock (Sync)
        {
            while (Entries.Count >= Capacity) Entries.Dequeue();
            Entries.Enqueue(new RebirthNpcAdminAuditEntry {
                Sequence = ++sequence, UtcTicks = DateTime.UtcNow.Ticks,
                Actor = Normalize(actor), Operation = Normalize(operation), Target = Normalize(target),
                Succeeded = succeeded, Detail = Normalize(detail)
            });
        }
    }

    public static RebirthNpcAdminAuditEntry[] GetSnapshot(int maximum)
    {
        if (maximum <= 0) maximum = 20;
        lock (Sync)
        {
            RebirthNpcAdminAuditEntry[] all = Entries.ToArray();
            int count = Math.Min(maximum, all.Length);
            RebirthNpcAdminAuditEntry[] result = new RebirthNpcAdminAuditEntry[count];
            Array.Copy(all, all.Length - count, result, 0, count);
            return result;
        }
    }

    public static string GetReport(int maximum)
    {
        RebirthNpcAdminAuditEntry[] entries = GetSnapshot(maximum);
        StringBuilder b = new StringBuilder();
        b.Append("[REBIRTH NPC Admin Audit] retained=").Append(entries.Length).AppendLine();
        for (int i = 0; i < entries.Length; i++)
        {
            RebirthNpcAdminAuditEntry e = entries[i];
            b.Append("  #").Append(e.Sequence).Append(' ')
             .Append(new DateTime(e.UtcTicks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture))
             .Append(" actor=").Append(e.Actor).Append(" operation=").Append(e.Operation)
             .Append(" target=").Append(e.Target).Append(" success=").Append(e.Succeeded)
             .Append(" detail=").Append(e.Detail).AppendLine();
        }
        return b.ToString().TrimEnd();
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) Entries.Clear();
    }

    private static string Normalize(string value) { return string.IsNullOrWhiteSpace(value) ? "none" : value.Trim(); }
}

/// <summary>
/// Explicit permission boundary. Remote callers are inspect-only; export and
/// repair remain server-console operations unless a future native permission
/// adapter proves a stronger authenticated role mapping.
/// </summary>
public static class RebirthNpcAdminPermissionPolicy
{
    public static bool Authorize(CommandSenderInfo sender, RebirthNpcAdminPermission permission, out string actor, out string reason)
    {
        actor = sender.RemoteClientInfo == null ? "server-console" : "remote-entity:" + sender.RemoteClientInfo.entityId;
        if (permission == RebirthNpcAdminPermission.Inspect)
        {
            reason = string.Empty;
            return true;
        }
        if (sender.RemoteClientInfo == null)
        {
            reason = string.Empty;
            return true;
        }
        reason = "Operation requires the local dedicated-server console.";
        return false;
    }
}

public static class RebirthNpcAdministrationSupportService
{
    private static readonly HashSet<string> RecoverableFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "RebirthNpcStableIdentities.xml", "rebirth_npc_inventory.xml", "rebirth_npc_equipment.xml",
        "RebirthNpcExecutionState.xml", "rebirth_npc_settlement.xml", "RebirthNpcWorkCheckpoints.xml",
        "RebirthNpcWorkOutcomes.xml", "RebirthNpcSocialState.xml", "RebirthNpcCombatState.xml",
        "RebirthNpcPersistentRecords.xml"
    };
    private static long reportsGenerated, exportsGenerated, recoveryAttempts, recoverySuccesses;
    private static string lastExport = string.Empty, lastRecovery = string.Empty;

    public static void EnsureInitialized() { }

    public static string GetConsolidatedReport()
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH NPC Administration Support]");
        b.AppendLine(RebirthNpcLifecycle.GetReport());
        b.AppendLine(RebirthNpcDiagnostics.GetStatus());
        b.AppendLine(RebirthNpcPersistenceCoordinator.GetReport());
        b.AppendLine(RebirthNpcAggregatePersistenceStore.GetReport());
        b.AppendLine(RebirthNpcLegacy26ImportService.GetReport());
        b.AppendLine(RebirthNpcScaleHardeningService.GetReport());
        b.AppendLine(RebirthNpcWorldIntegrationService.GetReport());
        b.AppendLine(RebirthNpcCompatibilityQualificationService.GetReport());
        b.AppendLine(GetCommandQueueSummary());
        b.AppendLine(RebirthNpcActivityDiagnostics.GetReport());
        b.AppendLine(RebirthNpcExecutionLeaseDiagnostics.GetReport());
        b.Append("  reportsGenerated=").Append(++reportsGenerated)
         .Append(" exportsGenerated=").Append(exportsGenerated)
         .Append(" recoveryAttempts=").Append(recoveryAttempts)
         .Append(" recoverySuccesses=").Append(recoverySuccesses)
         .Append(" lastExport=").Append(string.IsNullOrEmpty(lastExport) ? "none" : lastExport)
         .Append(" lastRecovery=").Append(string.IsNullOrEmpty(lastRecovery) ? "none" : lastRecovery);
        return b.ToString();
    }

    public static string Inspect(string selector)
    {
        RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
        int entityId;
        RebirthNpcStableId stableId;
        bool byEntity = int.TryParse(selector, NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId);
        bool byStable = RebirthNpcStableId.TryParse(selector, out stableId);
        for (int i = 0; i < states.Length; i++)
        {
            RebirthNpcRuntimeState s = states[i];
            int resolvedEntity;
            bool hasEntity = RebirthNpcRuntimeRegistry.TryGetEntityId(s.StableId, out resolvedEntity);
            if ((byEntity && hasEntity && resolvedEntity == entityId) || (byStable && s.StableId.Equals(stableId)))
                return FormatNpc(s, hasEntity ? resolvedEntity : 0);
        }
        return "[REBIRTH NPC Inspect] no active NPC matched selector=" + selector;
    }

    public static string GetQueueReport()
    {
        return "[REBIRTH NPC Queue Inspection]\n" +
            GetCommandQueueSummary() + "\n" +
            RebirthNpcActivityDiagnostics.GetReport() + "\n" +
            RebirthNpcActivityRecoveryDiagnostics.GetReport() + "\n" +
            RebirthNpcExecutionLeaseDiagnostics.GetReport() + "\n" +
            GetNetworkBatchSummary();
    }

    public static bool ValidateSave(out string report)
    {
        RebirthNpcPersistenceFileSnapshot[] files = RebirthNpcPersistenceCoordinator.CaptureFileSnapshots();
        StringBuilder b = new StringBuilder();
        bool valid = true;
        b.AppendLine("[REBIRTH NPC Save Validation]");
        for (int i = 0; i < files.Length; i++)
        {
            RebirthNpcPersistenceFileSnapshot f = files[i];
            bool fileValid = !f.Exists || (f.Length > 0 && !string.IsNullOrEmpty(f.Sha256));
            if (!fileValid) valid = false;
            b.Append("  ").Append(f.FileName).Append(" valid=").Append(fileValid)
             .Append(" primary=").Append(f.Exists).Append(" bytes=").Append(f.Length)
             .Append(" backup=").Append(f.BackupExists).Append(" backupBytes=").Append(f.BackupLength).AppendLine();
        }
        b.Append("  overall=").Append(valid ? "PASS" : "FAIL");
        report = b.ToString();
        return valid;
    }

    public static bool RecoverFromBackup(string fileName, out string detail)
    {
        recoveryAttempts++;
        if (string.IsNullOrWhiteSpace(fileName) || !RecoverableFiles.Contains(fileName))
        {
            detail = "File is not in the recoverable persistence allow-list.";
            lastRecovery = detail;
            return false;
        }
        try
        {
            string dir = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(dir)) { detail = "Save directory is unavailable."; lastRecovery = detail; return false; }
            string primary = Path.Combine(dir, fileName);
            string backup = primary + ".bak";
            if (!File.Exists(backup) || new FileInfo(backup).Length <= 0)
            { detail = "A non-empty backup file does not exist."; lastRecovery = detail; return false; }
            string quarantine = primary + ".recovery-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string staged = primary + ".recovery.tmp";
            File.Copy(backup, staged, true);
            string validationError;
            if (!ValidateRecoveryCandidate(fileName, staged, out validationError))
            {
                try { if (File.Exists(staged)) File.Delete(staged); } catch { }
                detail = "Backup rejected by recovery validation: " + validationError;
                lastRecovery = detail;
                return false;
            }
            if (File.Exists(primary)) File.Copy(primary, quarantine, false);
            string publishError;
            if (!RebirthDurableFileCommit.TryPublish(staged, primary, out publishError))
            {
                detail = "Validated backup could not be published: " + publishError;
                lastRecovery = detail;
                return false;
            }
            recoverySuccesses++;
            detail = "Validated backup restored; previous primary quarantined at " + quarantine;
            lastRecovery = detail;
            return true;
        }
        catch (Exception ex)
        {
            detail = ex.GetType().Name + ": " + ex.Message;
            lastRecovery = detail;
            return false;
        }
    }

    private static bool ValidateRecoveryCandidate(string fileName, string path, out string error)
    {
        error = string.Empty;
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length <= 0) { error = "candidate-empty"; return false; }
            XmlDocument document = new XmlDocument();
            document.Load(path);
            XmlElement root = document.DocumentElement;
            if (root == null) { error = "xml-root-missing"; return false; }
            if (string.Equals(fileName, RebirthNpcAggregatePersistenceStore.FileName, StringComparison.OrdinalIgnoreCase))
            {
                int format;
                if (!int.TryParse(root.GetAttribute("format"), NumberStyles.Integer, CultureInfo.InvariantCulture, out format) ||
                    format < 1 || format > RebirthNpcAggregatePersistenceStore.CurrentFormat)
                { error = "aggregate-format-unsupported"; return false; }
            }
            return true;
        }
        catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; return false; }
    }

    public static bool ExportSnapshot(out string path, out string detail)
    {
        try
        {
            string save = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(save)) { path = string.Empty; detail = "Save directory is unavailable."; return false; }
            string dir = Path.Combine(save, "RebirthNpcSupportExports");
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "npc-support-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");
            StringBuilder b = new StringBuilder();
            b.AppendLine(GetConsolidatedReport());
            b.AppendLine();
            b.AppendLine(GetQueueReport());
            b.AppendLine();
            b.AppendLine("[REBIRTH NPC Active NPC Export]");
            RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
            for (int i = 0; i < states.Length; i++)
            {
                int entityId;
                RebirthNpcRuntimeRegistry.TryGetEntityId(states[i].StableId, out entityId);
                b.AppendLine(FormatNpc(states[i], entityId));
            }
            b.AppendLine();
            b.AppendLine(RebirthNpcAdminAuditLedger.GetReport(512));
            File.WriteAllText(path, b.ToString(), new UTF8Encoding(false));
            exportsGenerated++;
            lastExport = path;
            detail = "Support export written.";
            return true;
        }
        catch (Exception ex)
        {
            path = string.Empty; detail = ex.GetType().Name + ": " + ex.Message; return false;
        }
    }

    public static string GetReport()
    {
        return "[REBIRTH NPC Administration] reports=" + reportsGenerated + " exports=" + exportsGenerated +
            " recoveryAttempts=" + recoveryAttempts + " recoverySuccesses=" + recoverySuccesses +
            " lastExport=" + (string.IsNullOrEmpty(lastExport) ? "none" : lastExport) +
            " lastRecovery=" + (string.IsNullOrEmpty(lastRecovery) ? "none" : lastRecovery);
    }

    public static void ResetForWorldChange()
    {
        RebirthNpcAdminAuditLedger.ResetForWorldChange();
        lastExport = string.Empty; lastRecovery = string.Empty;
    }

    private static string GetCommandQueueSummary()
    {
        RebirthNpcCommandRecord[] commands = RebirthNpcCommandGateway.GetSnapshot();
        return "[REBIRTH NPC Command Queue] records=" + commands.Length;
    }

    private static string GetNetworkBatchSummary()
    {
        RebirthNpcNetworkBatchSnapshot s = RebirthNpcNetworkBatcher.GetSnapshot();
        return "[REBIRTH NPC Network Queue] pendingBatches=" + s.PendingBatches +
            " pendingMessages=" + s.PendingMessages + " queued=" + s.MessagesQueued +
            " drained=" + s.BatchesDrained + " bytes=" + s.BytesQueued;
    }

    private static string FormatNpc(RebirthNpcRuntimeState s, int entityId)
    {
        return "[REBIRTH NPC Inspect] entityId=" + entityId + " stableId=" + s.StableId +
            " profile=" + s.ProfileId + " presence=" + s.Presence + " ownership=" + s.OwnershipKind +
            " owner=" + (string.IsNullOrEmpty(s.OwnerId) ? "none" : s.OwnerId) + " order=" + s.Order +
            " travel=" + s.Travel + " revision=" + s.Revision + " dirty=" + s.Dirty +
            " appearance=" + (s.HasHumanAppearance ? s.HumanAppearance.ToString() : "none");
    }
}

public sealed class ConsoleCmdRebirthNpcAdmin : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return false; } }
    public override string[] getCommands() { return new[] { "rbnpcadmin", "rbnpcsupport" }; }
    public override string getDescription() { return "REBIRTH NPC consolidated administration, inspection, export, validation and recovery tooling."; }
    public override string getHelp()
    {
        return "Usage:\n" +
            "  rbnpcadmin status\n" +
            "  rbnpcadmin inspect <entityId|stableId>\n" +
            "  rbnpcadmin queues\n" +
            "  rbnpcadmin validate\n" +
            "  rbnpcadmin export\n" +
            "  rbnpcadmin progression [stableId]\n" +
            "  rbnpcadmin professions [stableId]\n" +
            "  rbnpcadmin professionqualify\n" +
            "  rbnpcadmin progressionexport [fileName]\n" +
            "  rbnpcadmin progressionimport <fileName> [merge|replace]\n" +
            "  rbnpcadmin progressioninspect <stableId>\n" +
            "  rbnpcadmin progressiondiagnosticexport <stableId>\n" +
            "  rbnpcadmin progressionreset <stableId>\n" +
            "  rbnpcadmin combatpolicy\n" +
            "  rbnpcadmin combatqualify\n" +
            "  rbnpcadmin lifecycleplay\n" +
            "  rbnpcadmin lifecycleplayqualify\n" +
            "  rbnpcadmin worldintegration\n" +
            "  rbnpcadmin worldintegrationqualify\n" +
            "  rbnpcadmin replicationperformance\n" +
            "  rbnpcadmin replicationperformancequalify\n" +
            "  rbnpcadmin economylogistics\n" +
            "  rbnpcadmin economylogisticsqualify\n" +
            "  rbnpcadmin persistenceaggregate\n" +
            "  rbnpcadmin persistenceaggregatequalify\n" +
            "  rbnpcadmin migration26 <legacyXmlPath>\n" +
            "  rbnpcadmin recover <persistenceFileName>\n" +
            "  rbnpcadmin audit [count]\n" +
            "Export and recovery require the local dedicated-server console.";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string operation = parameters == null || parameters.Count == 0 ? "status" : (parameters[0] ?? string.Empty).Trim().ToLowerInvariant();
        RebirthNpcAdminPermission required = (operation == "recover" || operation == "progressionimport" || operation == "progressionreset" || operation == "migration26") ? RebirthNpcAdminPermission.Repair :
            (operation == "export" || operation == "progressionexport" || operation == "progressiondiagnosticexport") ? RebirthNpcAdminPermission.Export : RebirthNpcAdminPermission.Inspect;
        string actor, denial;
        if (!RebirthNpcAdminPermissionPolicy.Authorize(senderInfo, required, out actor, out denial))
        {
            RebirthNpcAdminAuditLedger.Append(actor, operation, string.Empty, false, denial);
            Log.Out("[REBIRTH NPC Admin] denied: " + denial);
            return;
        }

        if (operation == "status") { Log.Out(RebirthNpcAdministrationSupportService.GetConsolidatedReport()); return; }
        if (operation == "queues") { Log.Out(RebirthNpcAdministrationSupportService.GetQueueReport()); return; }
        if (operation == "audit") { int count = 20; if (parameters.Count > 1) int.TryParse(parameters[1], out count); Log.Out(RebirthNpcAdminAuditLedger.GetReport(count)); return; }
        if (operation == "inspect")
        {
            if (parameters.Count < 2) { Log.Out("Usage: rbnpcadmin inspect <entityId|stableId>"); return; }
            string report = RebirthNpcAdministrationSupportService.Inspect(parameters[1]);
            RebirthNpcAdminAuditLedger.Append(actor, operation, parameters[1], true, "Inspection completed."); Log.Out(report); return;
        }
        if (operation == "validate")
        {
            string report; bool success = RebirthNpcAdministrationSupportService.ValidateSave(out report);
            RebirthNpcAdminAuditLedger.Append(actor, operation, "save", success, success ? "Validation passed." : "Validation failed."); Log.Out(report); return;
        }
        if (operation == "export")
        {
            string path, detail; bool success = RebirthNpcAdministrationSupportService.ExportSnapshot(out path, out detail);
            RebirthNpcAdminAuditLedger.Append(actor, operation, path, success, detail); Log.Out("[REBIRTH NPC Admin] " + detail + (success ? " path=" + path : string.Empty)); return;
        }
        if (operation == "progression")
        {
            if (parameters.Count < 2) { Log.Out(RebirthNpcProgressionService.GetReport()+"\n"+RebirthNpcProgressionPersistenceStore.GetReport()); return; }
            RebirthNpcStableId id; RebirthNpcProgressionView view = null;
            bool success = RebirthNpcStableId.TryParse(parameters[1], out id) && RebirthNpcProgressionService.TryGetView(id, "profession.general", out view);
            string detail = success ? view.ToLine() : "No profession.general progression record found.";
            RebirthNpcAdminAuditLedger.Append(actor, operation, parameters[1], success, detail); Log.Out("[REBIRTH NPC Admin Progression] "+detail); return;
        }
        if (operation == "progressionmodifiers")
        {
            if (parameters.Count < 2) { Log.Out(RebirthNpcProgressionModifierService.GetReport()+"\n"+RebirthNpcWeaponProgressionService.GetReport()); return; }
            RebirthNpcStableId id; if(!RebirthNpcStableId.TryParse(parameters[1],out id)){Log.Out("Usage: rbnpcadmin progressionmodifiers <stableId> [specialty]");return;}
            RebirthNpcWeaponSpecialty specialty=RebirthNpcWeaponSpecialty.Unarmed;
            if(parameters.Count>2)Enum.TryParse(parameters[2],true,out specialty);
            RebirthNpcProgressionModifierSnapshot m=RebirthNpcProgressionModifierService.Evaluate(id,specialty);
            string detail="npc="+id+" constitution="+m.ConstitutionLevel+" strength="+m.StrengthLevel+" dexterity="+m.DexterityLevel+" weapon="+m.WeaponLevel+" health="+m.MaximumHealthMultiplier+" physical="+m.PhysicalDamageMultiplier+" move="+m.MovementSpeedMultiplier+" dodge="+m.DodgeChance+" accuracy="+m.AccuracyMultiplier+" reload="+m.ReloadSpeedMultiplier;
            RebirthNpcAdminAuditLedger.Append(actor,operation,parameters[1],true,detail);Log.Out("[REBIRTH NPC Admin ACIP-02] "+detail);return;
        }
        if (operation == "progressionqualify") { string detail=RebirthNpcProgressionAcip02Qualification.Run();RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,detail.Contains("PASS"),detail);Log.Out(detail);return; }
        if (operation == "professions")
        {
            if(parameters.Count<2){Log.Out(RebirthNpcProfessionProgressionService.GetReport());return;}
            RebirthNpcStableId id;if(!RebirthNpcStableId.TryParse(parameters[1],out id)){Log.Out("Usage: rbnpcadmin professions <stableId>");return;}
            StringBuilder report=new StringBuilder("[REBIRTH NPC Professions] npc=").Append(id);
            foreach(RebirthNpcProfession profession in Enum.GetValues(typeof(RebirthNpcProfession))){RebirthNpcProfessionModifierSnapshot m=RebirthNpcProfessionProgressionService.Evaluate(id,profession);report.AppendLine().Append("  ").Append(profession).Append(" level=").Append(m.Level).Append(" speed=").Append(m.SpeedMultiplier).Append(" yield=").Append(m.YieldMultiplier).Append(" quality=").Append(m.QualityMultiplier).Append(" efficiency=").Append(m.ResourceEfficiencyMultiplier).Append(" treatment=").Append(m.TreatmentMultiplier);}
            string detail=report.ToString();RebirthNpcAdminAuditLedger.Append(actor,operation,parameters[1],true,detail);Log.Out(detail);return;
        }
        if (operation == "professionqualify") { string detail=RebirthNpcProgressionAcip03Qualification.Run();RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,detail.Contains("Result: PASS"),detail);Log.Out(detail);return; }
        if (operation == "advancedprogression")
        {
            RebirthNpcStableId? id=null;RebirthNpcStableId parsed;if(parameters.Count>1&&RebirthNpcStableId.TryParse(parameters[1],out parsed))id=parsed;
            string detail=RebirthNpcAdvancedProgressionService.GetReport(id);RebirthNpcAdminAuditLedger.Append(actor,operation,parameters.Count>1?parameters[1]:string.Empty,true,detail);Log.Out(detail);return;
        }
        if (operation == "advancedprogressionqualify") { string detail=RebirthNpcAdvancedProgressionQualificationService.Run();bool success=!detail.Contains("=FAIL");RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,success,detail);Log.Out(detail);return; }
        if (operation == "combatpolicy") { string detail=RebirthNpcTargetingService.GetReport()+"\n"+RebirthNpcFriendlyFirePolicy.GetReport()+"\n"+RebirthNpcCombatExecutionService.GetReport();RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,true,detail);Log.Out(detail);return; }
        if (operation == "combatqualify") { string detail=RebirthNpcAcip06QualificationService.Run();bool success=detail.Contains("result=PASS");RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,success,detail);Log.Out(detail);return; }
        if (operation == "lifecycleplay") { string detail=RebirthNpcLifecycleGameplayService.GetReport();RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,true,detail);Log.Out(detail);return; }
        if (operation == "lifecycleplayqualify") { string detail=RebirthNpcLifecycleGameplayService.Qualify();bool success=detail.Contains("result=PASS");RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,success,detail);Log.Out(detail);return; }
        if (operation == "worldintegration") { string detail=RebirthNpcWorldIntegrationService.GetReport();RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,true,detail);Log.Out(detail);return; }
        if (operation == "worldintegrationqualify") { string detail=RebirthNpcWorldIntegrationService.Qualify();bool success=detail.Contains("result=PASS");RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,success,detail);Log.Out(detail);return; }
        if (operation == "economylogistics") { string detail=RebirthNpcEconomyLogisticsService.GetReport();RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,true,detail);Log.Out(detail);return; }
        if (operation == "economylogisticsqualify") { string detail=RebirthNpcEconomyLogisticsService.Qualify();bool success=detail.Contains("result=PASS");RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,success,detail);Log.Out(detail);return; }
        if (operation == "replicationperformance") { string detail=RebirthNpcReplicationPerformanceService.GetReport();RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,true,detail);Log.Out(detail);return; }
        if (operation == "replicationperformancequalify") { string detail=RebirthNpcReplicationPerformanceService.Qualify();bool success=detail.Contains("result=PASS");RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,success,detail);Log.Out(detail);return; }
        if (operation == "persistenceaggregate") { string detail=RebirthNpcAggregatePersistenceStore.GetReport()+"\n"+RebirthNpcLegacy26ImportService.GetReport();RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,true,detail);Log.Out(detail);return; }
        if (operation == "persistenceaggregatequalify") { RebirthNpcAggregateQualificationSnapshot q=RebirthNpcLegacy26ImportService.Qualify();string detail="[REBIRTH NPC ACIP-11 Qualification] result="+(q.IsValid?"PASS":"FAIL")+" records="+q.Records+" domains="+q.ManifestDomains+" mappings="+q.MigrationMappings+" consistencyFailures="+q.ConsistencyFailures+" unsupported="+q.UnsupportedFields+" quarantined="+q.Quarantined+" duplicates="+q.DuplicateImports+" detail="+q.Detail;RebirthNpcAdminAuditLedger.Append(actor,operation,string.Empty,q.IsValid,detail);Log.Out(detail);return; }
        if (operation == "migration26") { if(parameters.Count<2){Log.Out("Usage: rbnpcadmin migration26 <legacyXmlPath>");return;}bool success=RebirthNpcLegacy26ImportService.ImportFile(parameters[1]);string detail=RebirthNpcLegacy26ImportService.GetReport();RebirthNpcAdminAuditLedger.Append(actor,operation,parameters[1],success,detail);Log.Out(detail);return; }
        if (operation == "progressioninspect")
        {
            RebirthNpcStableId id;if(parameters.Count<2||!RebirthNpcStableId.TryParse(parameters[1],out id)){Log.Out("Usage: rbnpcadmin progressioninspect <stableId>");return;}
            string detail=RebirthNpcProgressionAdministration.Inspect(id);RebirthNpcAdminAuditLedger.Append(actor,operation,parameters[1],true,detail);Log.Out(detail);return;
        }
        if (operation == "progressiondiagnosticexport")
        {
            RebirthNpcStableId id;if(parameters.Count<2||!RebirthNpcStableId.TryParse(parameters[1],out id)){Log.Out("Usage: rbnpcadmin progressiondiagnosticexport <stableId>");return;}string path,detail;bool ok=RebirthNpcProgressionAdministration.Export(id,out path,out detail);RebirthNpcAdminAuditLedger.Append(actor,operation,parameters[1],ok,detail);Log.Out(detail+(ok?" path="+path:string.Empty));return;
        }
        if (operation == "progressionreset")
        {
            RebirthNpcStableId id;if(parameters.Count<2||!RebirthNpcStableId.TryParse(parameters[1],out id)){Log.Out("Usage: rbnpcadmin progressionreset <stableId>");return;}string detail;bool ok=RebirthNpcProgressionAdministration.RepairSafeReset(id,out detail);RebirthNpcAdminAuditLedger.Append(actor,operation,parameters[1],ok,detail);Log.Out(detail);return;
        }
        if (operation == "progressionexport")
        {
            string name = parameters.Count > 1 ? Path.GetFileName(parameters[1]) : "RebirthNpcProgressionExport.xml";
            if (string.IsNullOrWhiteSpace(name)) name = "RebirthNpcProgressionExport.xml";
            string path = Path.Combine(GameIO.GetSaveGameDir(), name); RebirthNpcProgressionPersistenceStore.ExportTo(path);
            RebirthNpcAdminAuditLedger.Append(actor, operation, path, true, "Progression export completed."); Log.Out("[REBIRTH NPC Admin Progression] exported path="+path); return;
        }
        if (operation == "progressionimport")
        {
            if (parameters.Count < 2) { Log.Out("Usage: rbnpcadmin progressionimport <fileName> [merge|replace]"); return; }
            string path = Path.Combine(GameIO.GetSaveGameDir(), Path.GetFileName(parameters[1])); bool replace = parameters.Count > 2 && string.Equals(parameters[2], "replace", StringComparison.OrdinalIgnoreCase);
            try { int count=RebirthNpcProgressionPersistenceStore.ImportFrom(path,replace); RebirthNpcAdminAuditLedger.Append(actor, operation, path, true, "Imported records="+count); Log.Out("[REBIRTH NPC Admin Progression] imported records="+count+" mode="+(replace?"replace":"merge")); }
            catch(Exception ex){RebirthNpcAdminAuditLedger.Append(actor, operation, path, false, ex.Message);Log.Out("[REBIRTH NPC Admin Progression] import rejected: "+ex.Message);} return;
        }
        if (operation == "recover")
        {
            if (parameters.Count < 2) { Log.Out("Usage: rbnpcadmin recover <persistenceFileName>"); return; }
            string detail; bool success = RebirthNpcAdministrationSupportService.RecoverFromBackup(parameters[1], out detail);
            RebirthNpcAdminAuditLedger.Append(actor, operation, parameters[1], success, detail); Log.Out("[REBIRTH NPC Admin] " + (success ? "recovery succeeded: " : "recovery rejected: ") + detail); return;
        }
        Log.Out(getHelp());
    }
}
