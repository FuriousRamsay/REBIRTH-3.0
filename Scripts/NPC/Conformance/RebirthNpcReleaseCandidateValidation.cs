using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcReleaseGateState : byte
{
    NotRun = 0,
    Passed = 1,
    Failed = 2,
    Blocked = 3,
    RequiresRuntimeEvidence = 4
}

public sealed class RebirthNpcReleaseGateResult
{
    public string Id;
    public string Category;
    public string Description;
    public RebirthNpcReleaseGateState State;
    public string Detail;
    public long EvaluatedUtcTicks;
}

public sealed class RebirthNpcReleaseEvidenceRecord
{
    public long Sequence;
    public long UtcTicks;
    public string GateId;
    public string Environment;
    public string Result;
    public string EvidenceReference;
    public string RecordedBy;
    public string BuildIdentity;
}

/// <summary>
/// WP20 release-candidate evidence ledger. Runtime, multiplayer, soak and external
/// build evidence is recorded explicitly instead of being inferred from source checks.
/// </summary>
public static class RebirthNpcReleaseEvidenceLedger
{
    private const int Capacity = 1024;
    private static readonly object Sync = new object();
    private static readonly Queue<RebirthNpcReleaseEvidenceRecord> Records = new Queue<RebirthNpcReleaseEvidenceRecord>();
    private static long sequence;

    public static bool Record(string gateId, string environment, string result, string evidenceReference, string actor, out string error)
    {
        gateId = Normalize(gateId);
        environment = Normalize(environment);
        result = Normalize(result).ToUpperInvariant();
        evidenceReference = Normalize(evidenceReference);
        actor = Normalize(actor);
        if (gateId.Length == 0) { error = "Gate ID is required."; return false; }
        if (environment.Length == 0) { error = "Environment is required."; return false; }
        if (result != "PASS" && result != "FAIL" && result != "BLOCKED") { error = "Result must be PASS, FAIL, or BLOCKED."; return false; }
        if (evidenceReference.Length == 0) { error = "Evidence reference is required."; return false; }
        lock (Sync)
        {
            while (Records.Count >= Capacity) Records.Dequeue();
            Records.Enqueue(new RebirthNpcReleaseEvidenceRecord {
                Sequence = ++sequence, UtcTicks = DateTime.UtcNow.Ticks, GateId = gateId,
                Environment = environment, Result = result, EvidenceReference = evidenceReference, RecordedBy = actor,
                BuildIdentity = CurrentBuildIdentity()
            });
        }
        error = null;
        return true;
    }

    public static RebirthNpcReleaseEvidenceRecord[] Snapshot()
    {
        lock (Sync) return Records.ToArray();
    }

    public static bool HasPassingEvidence(string gateId)
    {
        RebirthNpcReleaseEvidenceRecord[] records = Snapshot();
        for (int i = records.Length - 1; i >= 0; i--)
            if (string.Equals(records[i].GateId, gateId, StringComparison.OrdinalIgnoreCase))
                return string.Equals(records[i].Result, "PASS", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(records[i].BuildIdentity, CurrentBuildIdentity(), StringComparison.Ordinal);
        return false;
    }

    public static string GetReport(int maximum)
    {
        RebirthNpcReleaseEvidenceRecord[] all = Snapshot();
        if (maximum <= 0) maximum = 50;
        int start = Math.Max(0, all.Length - maximum);
        StringBuilder b = new StringBuilder();
        b.Append("[REBIRTH NPC RC Evidence] retained=").Append(all.Length).Append('/').Append(Capacity).AppendLine();
        for (int i = start; i < all.Length; i++)
        {
            RebirthNpcReleaseEvidenceRecord r = all[i];
            b.Append("  #").Append(r.Sequence).Append(' ')
             .Append(new DateTime(r.UtcTicks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture))
             .Append(" gate=").Append(r.GateId).Append(" environment=").Append(r.Environment)
             .Append(" result=").Append(r.Result).Append(" evidence=").Append(r.EvidenceReference)
             .Append(" actor=").Append(r.RecordedBy).Append(" build=").Append(r.BuildIdentity).AppendLine();
        }
        return b.ToString().TrimEnd();
    }

    public static void ResetForWorldChange()
    {
        lock (Sync)
        {
            Records.Clear();
            sequence = 0;
        }
    }
    private static string CurrentBuildIdentity()
    {
        Mod mod = ModManager.GetMod("zzz_REBIRTH__3_0");
        return mod != null && !string.IsNullOrEmpty(mod.VersionString) ? mod.VersionString : "unknown-build";
    }
    private static string Normalize(string value) { return (value ?? string.Empty).Trim(); }
}

/// <summary>
/// WP20 release-candidate validation orchestrator. It separates executable static
/// qualification from evidence that can only be produced in the exact b259 runtime.
/// </summary>
public static class RebirthNpcReleaseCandidateValidationService
{
    private static readonly object Sync = new object();
    private static RebirthNpcReleaseGateResult[] lastResults = new RebirthNpcReleaseGateResult[0];
    private static long runs;

    public static RebirthNpcReleaseGateResult[] Run()
    {
        List<RebirthNpcReleaseGateResult> results = new List<RebirthNpcReleaseGateResult>();
        long now = DateTime.UtcNow.Ticks;
        RebirthNpcLifecycleSnapshot lifecycle = RebirthNpcLifecycle.GetSnapshot();

        Add(results, "RC-STATIC-LIFECYCLE", "regression", "Lifecycle bootstrap is registered and service catalog is populated.",
            lifecycle.State != RebirthNpcLifecycleState.Cold && lifecycle.RegisteredServices != null && lifecycle.RegisteredServices.Length > 0,
            "state=" + lifecycle.State + " services=" + (lifecycle.RegisteredServices == null ? 0 : lifecycle.RegisteredServices.Length), now);
        Add(results, "RC-STATIC-COMPAT", "compatibility", "Required b259 compatibility contracts qualify without a blocking failure.",
            !Contains(RebirthNpcCompatibilityQualificationService.GetReport(), "Blocking"), RebirthNpcCompatibilityQualificationService.GetReport(), now);
        string persistenceDetail;
        bool persistenceValid = ValidatePersistence(out persistenceDetail);
        Add(results, "RC-STATIC-PERSISTENCE", "persistence", "Persistence validation reports a readable save set.",
            persistenceValid, persistenceDetail, now);
        Add(results, "RC-STATIC-SCALE", "performance", "Scale-hardening services and bounded policies are registered.",
            Contains(lifecycle.RegisteredServices, "RebirthNpcScaleHardeningService") && Contains(lifecycle.RegisteredServices, "RebirthNpcPathBudgetCoordinator") && Contains(lifecycle.RegisteredServices, "RebirthNpcRetentionCoordinator"),
            RebirthNpcScaleHardeningService.GetReport(), now);
        Add(results, "RC-STATIC-ADMIN", "support", "Administration, validation, export, recovery and audit services are registered.",
            Contains(lifecycle.RegisteredServices, "RebirthNpcAdministrationSupportService") && Contains(lifecycle.RegisteredServices, "RebirthNpcAdminAuditLedger"),
            RebirthNpcAdministrationSupportService.GetReport(), now);
        Add(results, "RC-STATIC-DEATH-IDEMPOTENCY", "security", "Combat death finalization diagnostics are available for duplicate-finalization review.",
            Contains(lifecycle.RegisteredServices, "RebirthNpcCombatEmergencyService"), RebirthNpcCombatEmergencyService.GetReport(), now);

        AddEvidenceGate(results, "RC-BUILD-B259", "build", "Clean compilation against exact 7 Days to Die 3.0 b259 references.", now);
        AddEvidenceGate(results, "RC-SP", "runtime", "Single-player regression and save/reload pass.", now);
        AddEvidenceGate(results, "RC-P2P", "multiplayer", "Peer-to-peer host plus remote-client parity pass.", now);
        AddEvidenceGate(results, "RC-DEDICATED", "multiplayer", "Dedicated-server authority, late join, restart and recovery pass.", now);
        AddEvidenceGate(results, "RC-MIGRATION", "migration", "Supported 2.6 fixture migration and unsupported-record rejection pass.", now);
        AddEvidenceGate(results, "RC-STRESS", "performance", "Accepted stress thresholds pass at target NPC population.", now);
        AddEvidenceGate(results, "RC-SOAK", "performance", "Long-duration soak shows bounded memory and stable queues.", now);
        AddEvidenceGate(results, "RC-PACKAGING", "packaging", "Clean install, changed-files package, cumulative archive and manifest QA pass.", now);
        AddEvidenceGate(results, "RC-SECURITY", "security", "Unauthorized mutations, malformed network requests and admin permission checks pass.", now);
        AddEvidenceGate(results, "RC-ROLLBACK", "recovery", "Rollback from RC to prior cumulative build succeeds using documented procedure.", now);

        lock (Sync) lastResults = results.ToArray();
        Interlocked.Increment(ref runs);
        return results.ToArray();
    }

    public static string GetReport()
    {
        RebirthNpcReleaseGateResult[] results;
        lock (Sync) results = lastResults.Length == 0 ? Run() : (RebirthNpcReleaseGateResult[])lastResults.Clone();
        int pass = 0, fail = 0, blocked = 0, runtime = 0;
        StringBuilder b = new StringBuilder();
        b.Append("[REBIRTH NPC WP20 Release Candidate] runs=").Append(Interlocked.Read(ref runs)).AppendLine();
        for (int i = 0; i < results.Length; i++)
        {
            RebirthNpcReleaseGateResult r = results[i];
            if (r.State == RebirthNpcReleaseGateState.Passed) pass++;
            else if (r.State == RebirthNpcReleaseGateState.Failed) fail++;
            else if (r.State == RebirthNpcReleaseGateState.Blocked) blocked++;
            else if (r.State == RebirthNpcReleaseGateState.RequiresRuntimeEvidence) runtime++;
            b.Append("  ").Append(r.Id).Append('=').Append(r.State).Append(" category=").Append(r.Category)
             .Append(" detail=").Append(Compact(r.Detail, 240)).AppendLine();
        }
        b.Append("summary pass=").Append(pass).Append(" fail=").Append(fail).Append(" blocked=").Append(blocked)
         .Append(" requiresRuntimeEvidence=").Append(runtime).AppendLine();
        b.Append("releaseDecision=").Append(fail == 0 && blocked == 0 && runtime == 0 ? "PASS" : "NOT QUALIFIED")
         .Append("; no release claim is valid until every runtime evidence gate has passing evidence.");
        return b.ToString();
    }

    public static bool Export(out string path, out string error)
    {
        path = null; error = null;
        try
        {
            string root = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(root)) { error = "Save directory is unavailable."; return false; }
            string directory = Path.Combine(root, "RebirthNpcSupport", "ReleaseCandidate");
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "REBIRTH_NPC_RC_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
            StringBuilder b = new StringBuilder();
            b.AppendLine("REBIRTH 3.0 NPC Framework - WP20 Release Candidate Evidence Export");
            b.AppendLine("GeneratedUtc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            b.AppendLine(GetReport());
            b.AppendLine();
            b.AppendLine(RebirthNpcReleaseEvidenceLedger.GetReport(1024));
            b.AppendLine();
            b.AppendLine(RebirthNpcAdministrationSupportService.GetConsolidatedReport());
            File.WriteAllText(path, b.ToString(), new UTF8Encoding(false));
            return true;
        }
        catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; return false; }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) lastResults = new RebirthNpcReleaseGateResult[0];
    }

    private static void Add(List<RebirthNpcReleaseGateResult> results, string id, string category, string description, bool pass, string detail, long now)
    {
        results.Add(new RebirthNpcReleaseGateResult { Id=id, Category=category, Description=description,
            State=pass?RebirthNpcReleaseGateState.Passed:RebirthNpcReleaseGateState.Failed, Detail=detail, EvaluatedUtcTicks=now });
    }
    private static void AddEvidenceGate(List<RebirthNpcReleaseGateResult> results, string id, string category, string description, long now)
    {
        bool pass = RebirthNpcReleaseEvidenceLedger.HasPassingEvidence(id);
        results.Add(new RebirthNpcReleaseGateResult { Id=id, Category=category, Description=description,
            State=pass?RebirthNpcReleaseGateState.Passed:RebirthNpcReleaseGateState.RequiresRuntimeEvidence,
            Detail=pass?"Passing evidence is recorded.":"No passing evidence has been recorded.", EvaluatedUtcTicks=now });
    }
    private static bool ValidatePersistence(out string detail)
    {
        try { return RebirthNpcAdministrationSupportService.ValidateSave(out detail); }
        catch (Exception ex) { detail=ex.GetType().Name+": "+ex.Message; return false; }
    }
    private static bool Contains(string value, string text) { return value != null && value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0; }
    private static bool Contains(string[] values, string text) { if(values==null)return false; for(int i=0;i<values.Length;i++)if(Contains(values[i],text))return true; return false; }
    private static string Compact(string value, int max) { value=(value??string.Empty).Replace('\r',' ').Replace('\n',' '); return value.Length<=max?value:value.Substring(0,max)+"..."; }
}

#if DEBUG
public sealed class ConsoleCmdRebirthNpcReleaseCandidate : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return false; } }
    public override string[] getCommands() { return new[] { "rbnpcrc", "rbnpcrelease" }; }
    public override string getDescription() { return "REBIRTH NPC WP20 release-candidate qualification and evidence management."; }
    public override string getHelp()
    {
        return "Usage:\n"+
            "  rbnpcrc run\n"+
            "  rbnpcrc status\n"+
            "  rbnpcrc evidence [count]\n"+
            "  rbnpcrc record <gateId> <environment> <PASS|FAIL|BLOCKED> <evidence-reference>\n"+
            "  rbnpcrc export\n"+
            "Record and export require the local server console.";
    }
    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string operation=parameters==null||parameters.Count==0?"status":(parameters[0]??string.Empty).Trim().ToLowerInvariant();
        string actor, denial;
        RebirthNpcAdminPermission permission=(operation=="record"||operation=="export")?RebirthNpcAdminPermission.Export:RebirthNpcAdminPermission.Inspect;
        if(!RebirthNpcAdminPermissionPolicy.Authorize(senderInfo,permission,out actor,out denial))
        { RebirthNpcAdminAuditLedger.Append(actor,"rc-"+operation,string.Empty,false,denial); Log.Out("[REBIRTH NPC RC] denied: "+denial); return; }
        if(operation=="run") { RebirthNpcReleaseCandidateValidationService.Run(); Log.Out(RebirthNpcReleaseCandidateValidationService.GetReport()); return; }
        if(operation=="status") { Log.Out(RebirthNpcReleaseCandidateValidationService.GetReport()); return; }
        if(operation=="evidence") { int count=50;if(parameters.Count>1)int.TryParse(parameters[1],out count);Log.Out(RebirthNpcReleaseEvidenceLedger.GetReport(count));return; }
        if(operation=="record")
        {
            if(parameters.Count<5){Log.Out("Usage: rbnpcrc record <gateId> <environment> <PASS|FAIL|BLOCKED> <evidence-reference>");return;}
            string error;bool ok=RebirthNpcReleaseEvidenceLedger.Record(parameters[1],parameters[2],parameters[3],parameters[4],actor,out error);
            RebirthNpcAdminAuditLedger.Append(actor,"rc-record",parameters[1],ok,ok?parameters[3]+":"+parameters[4]:error);
            Log.Out(ok?"[REBIRTH NPC RC] evidence recorded.":"[REBIRTH NPC RC] rejected: "+error);return;
        }
        if(operation=="export")
        {
            string path,error;bool ok=RebirthNpcReleaseCandidateValidationService.Export(out path,out error);
            RebirthNpcAdminAuditLedger.Append(actor,"rc-export",path,ok,ok?"Export completed.":error);
            Log.Out(ok?"[REBIRTH NPC RC] exported path="+path:"[REBIRTH NPC RC] export failed: "+error);return;
        }
        Log.Out(getHelp());
    }
}
#endif
