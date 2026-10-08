using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcMedicalTreatmentRequest
{
    public string SessionKey { get; set; }
    public string PatientStableId { get; set; }
    public string SettlementId { get; set; }
    public string MedicalItemKey { get; set; }
    public int MedicalItemQuantity { get; set; }
    public int HealAmount { get; set; }
    public int TreatmentTicks { get; set; }
    public int Priority { get; set; }
    public bool StartImmediately { get; set; }
}


public sealed class RebirthNpcMedicalTreatmentPackageSnapshot
{
    public Guid PackageId;
    public ulong AssignmentId;
    public long UpdatedUtcTicks;
    public string Detail;
}

public sealed class RebirthNpcMedicalTreatmentPackageResult
{
    public Guid PackageId { get; internal set; }
    public ulong AssignmentId { get; internal set; }
    public bool Created { get; internal set; }
    public bool Started { get; internal set; }
    public string Detail { get; internal set; }
}

/// <summary>
/// Creates explicit treatment assignments for stable REBIRTH NPC patients. Medical supplies remain
/// settlement-authoritative and are consumed only immediately before the authoritative health commit.
/// </summary>
public static class RebirthNpcMedicalTreatmentWorkPackageService
{
    public const string TreatmentDefinitionId = "rebirth.medical.treat";
    private sealed class ActivePackage { public Guid Id; public ulong AssignmentId; public long UpdatedUtcTicks; public string Detail; }
    private static readonly object Sync = new object();
    private static readonly Dictionary<Guid, ActivePackage> Active = new Dictionary<Guid, ActivePackage>();
    private static readonly RebirthNpcFairRoundRobin<Guid> Schedule = new RebirthNpcFairRoundRobin<Guid>();
    private static long requests, created, started, rejected, reconciliations, completed, failed;

    public static void RegisterDefinitions()
    {
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            TreatmentDefinitionId, RebirthNpcWorkKind.Medical, RebirthNpcWorkCapability.Medical,
            285, 1, TimeSpan.FromSeconds(60), true), true);
    }

    public static RebirthNpcMedicalTreatmentPackageResult CreateAndStart(RebirthNpcMedicalTreatmentRequest request)
    {
        Interlocked.Increment(ref requests);
        RebirthNpcMedicalTreatmentPackageResult result = new RebirthNpcMedicalTreatmentPackageResult { PackageId = Guid.NewGuid() };
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (request == null || string.IsNullOrWhiteSpace(request.SessionKey) || world == null || world.IsRemote())
            return Reject(result, "An authoritative world and NPC session are required.");
        RebirthNpcStableId patientId;
        if (!RebirthNpcStableId.TryParse(request.PatientStableId, out patientId))
            return Reject(result, "Patient stable identity is invalid.");
        if (string.IsNullOrWhiteSpace(request.SettlementId) || string.IsNullOrWhiteSpace(request.MedicalItemKey))
            return Reject(result, "Settlement and medical item keys are required.");
        int quantity = Math.Max(1, Math.Min(100, request.MedicalItemQuantity <= 0 ? 1 : request.MedicalItemQuantity));
        int healing = Math.Max(1, Math.Min(10000, request.HealAmount <= 0 ? 25 : request.HealAmount));
        int workTicks = Math.Max(1, Math.Min(3600, request.TreatmentTicks <= 0 ? 60 : request.TreatmentTicks));
        string target = "medical:treat:" + patientId + ":" + Encode(request.SettlementId) + ":" +
            Encode(request.MedicalItemKey) + ":" + quantity.ToString(CultureInfo.InvariantCulture) + ":" +
            healing.ToString(CultureInfo.InvariantCulture) + ":" + workTicks.ToString(CultureInfo.InvariantCulture);
        EntityRebirthNPC patient;
        Vector3 position = Vector3.zero;
        if (RebirthNpcWorkNavigationService.TryResolveNpc(patientId, out patient)) position = patient.position;
        RebirthNpcWorkAssignment assignment; string detail;
        RebirthNpcWorkAssignmentResult create = RebirthNpcWorkAssignmentService.Create(
            new RebirthNpcWorkAssignmentRequest {
                SessionKey = request.SessionKey, DefinitionId = TreatmentDefinitionId,
                TargetKey = target, TargetPosition = position, HasTargetPosition = true,
                Priority = request.Priority == 0 ? 285 : request.Priority
            }, out assignment, out detail);
        if (create != RebirthNpcWorkAssignmentResult.Succeeded || assignment == null)
            return Reject(result, string.IsNullOrEmpty(detail) ? "Medical assignment creation failed." : detail);
        ActivePackage package = new ActivePackage { Id = result.PackageId, AssignmentId = assignment.AssignmentId,
            UpdatedUtcTicks = DateTime.UtcNow.Ticks, Detail = "Medical treatment assignment created." };
        lock (Sync) { Active[package.Id] = package; Schedule.Add(package.Id); }
        result.AssignmentId = assignment.AssignmentId; result.Created = true; Interlocked.Increment(ref created);
        if (request.StartImmediately)
        {
            result.Started = RebirthNpcWorkAssignmentCoordinator.Start(assignment.AssignmentId, out detail);
            if (result.Started) Interlocked.Increment(ref started);
        }
        result.Detail = string.IsNullOrEmpty(detail) ? package.Detail : detail;
        return result;
    }

    public static void Tick(int budget)
    {
        if(budget<=0)return;
        for(int processed=0;processed<budget;processed++)
        {
            Guid packageId; ActivePackage package;
            lock(Sync)
            {
                if(!Schedule.TryTake(out packageId))break;
                if(!Active.TryGetValue(packageId,out package))continue;
            }
            RebirthNpcWorkAssignment assignment;
            if(!RebirthNpcWorkAssignmentService.TryGet(package.AssignmentId,out assignment))
            {
                lock(Sync)RemovePackageLocked(package.Id);
                Interlocked.Increment(ref failed);
                continue;
            }
            package.UpdatedUtcTicks=DateTime.UtcNow.Ticks;
            package.Detail="Medical assignment state="+assignment.Status+".";
            bool terminal=false;
            if(assignment.Status==RebirthNpcWorkAssignmentStatus.Completed)
            { terminal=true; Interlocked.Increment(ref completed); }
            else if(assignment.Status==RebirthNpcWorkAssignmentStatus.Failed||assignment.Status==RebirthNpcWorkAssignmentStatus.Cancelled)
            { terminal=true; Interlocked.Increment(ref failed); }
            lock(Sync)
            {
                if(terminal)RemovePackageLocked(package.Id);
                else if(Active.ContainsKey(package.Id))Schedule.Return(package.Id);
            }
            Interlocked.Increment(ref reconciliations);
        }
    }


    public static RebirthNpcMedicalTreatmentPackageSnapshot[] GetSnapshots()
    {
        lock (Sync)
        {
            RebirthNpcMedicalTreatmentPackageSnapshot[] snapshots = new RebirthNpcMedicalTreatmentPackageSnapshot[Active.Count];
            int index = 0;
            foreach (ActivePackage package in Active.Values)
                snapshots[index++] = new RebirthNpcMedicalTreatmentPackageSnapshot { PackageId = package.Id, AssignmentId = package.AssignmentId, UpdatedUtcTicks = package.UpdatedUtcTicks, Detail = package.Detail };
            Array.Sort(snapshots, delegate(RebirthNpcMedicalTreatmentPackageSnapshot a, RebirthNpcMedicalTreatmentPackageSnapshot b) { return a.PackageId.CompareTo(b.PackageId); });
            return snapshots;
        }
    }

    private static void RemovePackageLocked(Guid packageId)
    {
        Active.Remove(packageId);
        Schedule.Remove(packageId);
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { Active.Clear(); Schedule.Clear(); }
        RebirthNpcMedicalTreatmentAdapter.ResetForWorldChange();
    }

    private static RebirthNpcMedicalTreatmentPackageResult Reject(RebirthNpcMedicalTreatmentPackageResult result, string detail)
    { result.Detail = detail; Interlocked.Increment(ref rejected); return result; }
    private static string Encode(string value) { return (value ?? string.Empty).Replace(":", "%3A"); }
    public static string GetReport()
    {
        lock (Sync) return "[REBIRTH NPC Medical Packages] active=" + Active.Count +
            " requests=" + Interlocked.Read(ref requests) + " created=" + Interlocked.Read(ref created) +
            " started=" + Interlocked.Read(ref started) + " rejected=" + Interlocked.Read(ref rejected) +
            " reconciliations=" + Interlocked.Read(ref reconciliations) + " completed=" +
            Interlocked.Read(ref completed) + " failed=" + Interlocked.Read(ref failed);
    }
}

/// <summary>Resolves moving stable-NPC patients from the medical treatment target contract.</summary>
public sealed class RebirthNpcMedicalPatientTargetAdapter : IRebirthNpcWorkTargetAdapter
{
    public string AdapterId { get { return "rebirth.target.medical-patient"; } }
    public int Priority { get { return 350; } }
    public bool CanResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition)
    { return assignment != null && definition != null && definition.Kind == RebirthNpcWorkKind.Medical &&
        (assignment.TargetKey ?? string.Empty).StartsWith("medical:treat:", StringComparison.OrdinalIgnoreCase); }

    public bool TryResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition,
        out RebirthNpcWorkTargetSnapshot target, out string detail)
    {
        target = null; detail = string.Empty; RebirthNpcStableId id;
        if (!TryPatientId(assignment.TargetKey, out id)) { detail = "Medical patient identity is invalid."; return false; }
        EntityRebirthNPC patient; bool loaded = RebirthNpcWorkNavigationService.TryResolveNpc(id, out patient) && !patient.IsDead();
        target = new RebirthNpcWorkTargetSnapshot {
            AdapterId = AdapterId, StableTargetId = "medical-patient:" + id,
            TargetRevision = loaded && patient.RebirthRuntimeState != null ? patient.RebirthRuntimeState.Revision.ToString(CultureInfo.InvariantCulture) : string.Empty,
            Position = loaded ? patient.position : assignment.TargetPosition, IsLoaded = loaded, IsAvailable = loaded,
            InteractionRange = 3f, Detail = loaded ? "Medical patient resolved." : "Medical patient is currently unavailable."
        };
        detail = target.Detail; return true;
    }

    public bool Validate(RebirthNpcConcreteWorkContext context, out RebirthNpcWorkFailureCategory failure, out string detail)
    {
        failure = RebirthNpcWorkFailureCategory.None; detail = string.Empty; RebirthNpcStableId id;
        if (context == null || context.Assignment == null || !TryPatientId(context.Assignment.TargetKey, out id))
        { failure = RebirthNpcWorkFailureCategory.InvalidTarget; detail = "Medical patient identity is invalid."; return false; }
        EntityRebirthNPC patient;
        if (!RebirthNpcWorkNavigationService.TryResolveNpc(id, out patient) || patient.IsDead())
        { failure = RebirthNpcWorkFailureCategory.TargetUnavailable; detail = "Medical patient is unavailable."; return false; }
        context.Target.Position = patient.position; context.Target.IsLoaded = true; context.Target.IsAvailable = true;
        detail = "Medical patient remains available."; return true;
    }

    public bool IsNpcInRange(RebirthNpcConcreteWorkContext context, out string detail)
    {
        EntityRebirthNPC worker; RebirthNpcStableId id; EntityRebirthNPC patient;
        if (!RebirthNpcWorkNavigationService.TryResolveNpc(context.Assignment.NpcId, out worker) ||
            !TryPatientId(context.Assignment.TargetKey, out id) || !RebirthNpcWorkNavigationService.TryResolveNpc(id, out patient))
        { detail = "Worker or patient is not currently loaded."; return false; }
        context.Target.Position = patient.position;
        float range = Math.Max(0.5f, context.Target.InteractionRange);
        bool inside = (worker.position - patient.position).sqrMagnitude <= range * range;
        detail = inside ? "Medical worker is inside treatment range." : "Medical worker must navigate to the patient.";
        return inside;
    }

    internal static bool TryPatientId(string key, out RebirthNpcStableId id)
    {
        id = default(RebirthNpcStableId); if (string.IsNullOrEmpty(key)) return false;
        string[] p = key.Split(':'); return p.Length >= 8 && RebirthNpcStableId.TryParse(p[2], out id);
    }
}

/// <summary>Settlement-funded direct treatment adapter for REBIRTH NPC patients.</summary>
public sealed class RebirthNpcMedicalTreatmentAdapter : IRebirthNpcMedicalWorkAdapter
{
    private sealed class State
    {
        public RebirthNpcStableId PatientId; public string SettlementId; public string ItemKey;
        public int ItemQuantity; public int HealAmount; public int RemainingTicks; public int TotalTicks; public bool Committed;
    }
    private static readonly object sync = new object();
    private static readonly Dictionary<ulong, State> states = new Dictionary<ulong, State>();
    private static long begun, ticks, treatments, cancelled, overlapDenied, supplyFailures, patientFailures, refunds;
    public string AdapterId { get { return "rebirth.medical.treatment.native"; } }
    public int Priority { get { return 600; } }
    public bool CanHandle(RebirthNpcConcreteWorkContext context)
    { return context != null && context.Assignment != null && (context.Assignment.TargetKey ?? string.Empty).StartsWith("medical:treat:", StringComparison.OrdinalIgnoreCase); }

    public RebirthNpcWorkMutationResult BeginMedical(RebirthNpcConcreteWorkContext context)
    {
        State state; string detail;
        if (!TryParse(context.Assignment.TargetKey, out state, out detail)) return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, detail);
        EntityRebirthNPC patient;
        if (!RebirthNpcWorkNavigationService.TryResolveNpc(state.PatientId, out patient) || patient.IsDead())
        { Interlocked.Increment(ref patientFailures); return Retry("Medical patient is currently unavailable."); }
        int maxHealth = patient.GetMaxHealth();
        if (patient.Health >= maxHealth) return Completed("Patient no longer requires treatment.", null);
        RebirthHealingOverlapDecision overlap = RebirthHealingOverlapPolicy.Evaluate(patient, false);
        if (!overlap.Allowed) { Interlocked.Increment(ref overlapDenied); return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, "Patient already has sufficient pending medical healing."); }
        RebirthNpcSettlementInventoryEndpoint endpoint = RebirthNpcSettlementInventoryEndpointRegistry.Ensure(state.SettlementId);
        if (endpoint == null || endpoint.GetAvailableDebitQuantity(state.ItemKey) < state.ItemQuantity)
        { Interlocked.Increment(ref supplyFailures); return Failed(RebirthNpcWorkFailureCategory.MissingInput, "Settlement lacks unreserved medical supply " + state.ItemKey + "."); }
        lock (sync) states[context.Assignment.AssignmentId] = state;
        Interlocked.Increment(ref begun);
        return RebirthNpcWorkMutationResult.Continue(0.05f, "Patient and medical supplies validated.");
    }

    public RebirthNpcWorkMutationResult TickMedical(RebirthNpcConcreteWorkContext context)
    {
        State state; lock (sync) if (!states.TryGetValue(context.Assignment.AssignmentId, out state))
            return Failed(RebirthNpcWorkFailureCategory.ExecutorFault, "Medical treatment state disappeared.");
        if (state.Committed) return Completed("Medical treatment already committed.", null);
        EntityRebirthNPC patient;
        if (!RebirthNpcWorkNavigationService.TryResolveNpc(state.PatientId, out patient) || patient.IsDead())
        { Interlocked.Increment(ref patientFailures); return Retry("Medical patient is temporarily unavailable."); }
        int maxHealth = patient.GetMaxHealth();
        if (patient.Health >= maxHealth) return Completed("Patient recovered before supplies were consumed.", null);
        RebirthHealingOverlapDecision overlap = RebirthHealingOverlapPolicy.Evaluate(patient, false);
        if (!overlap.Allowed) { Interlocked.Increment(ref overlapDenied); return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, "Patient now has sufficient pending medical healing."); }
        state.RemainingTicks--; Interlocked.Increment(ref ticks);
        if (state.RemainingTicks > 0)
            return RebirthNpcWorkMutationResult.Continue(0.9f / Math.Max(1, state.TotalTicks), "Treating patient; remaining ticks=" + state.RemainingTicks + ".");
        RebirthNpcSettlementInventoryEndpoint endpoint = RebirthNpcSettlementInventoryEndpointRegistry.Ensure(state.SettlementId);
        IRebirthNpcExternalInventoryReservation reservation; string error = string.Empty; Guid transaction = Guid.NewGuid();
        if (endpoint == null || !endpoint.TryReserveDebit(transaction, state.ItemKey, state.ItemQuantity, out reservation, out error) || reservation == null)
        { Interlocked.Increment(ref supplyFailures); return Retry("Medical supply reservation failed: " + error); }
        try
        {
            if (!reservation.Commit(out error)) { Interlocked.Increment(ref supplyFailures); return Retry("Medical supply commit failed: " + error); }
            try
            {
                int before = patient.Health;
                patient.Health = Math.Min(patient.GetMaxHealth(), before + state.HealAmount);
                if (patient.Health <= before)
                {
                    RebirthNpcSettlementSimulation.DepositResource(state.SettlementId, state.ItemKey, state.ItemQuantity);
                    Interlocked.Increment(ref refunds);
                    return Failed(RebirthNpcWorkFailureCategory.TargetChanged, "Treatment produced no health change and the medical supply was refunded.");
                }
                state.Committed = true; Interlocked.Increment(ref treatments);
                RebirthNpcSocialGameplayEventProducers.PublishHealing(
                    state.PatientId, context.Assignment.ActorId,
                    Math.Max(0.25f, Math.Min(1f, (patient.Health - before) / (float)Math.Max(1, patient.GetMaxHealth()))));
                Dictionary<string, int> consumed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                consumed[state.ItemKey] = state.ItemQuantity;
                return Completed("Medical treatment restored " + (patient.Health - before) + " health.", consumed);
            }
            catch (Exception ex)
            {
                RebirthNpcSettlementSimulation.DepositResource(state.SettlementId, state.ItemKey, state.ItemQuantity);
                Interlocked.Increment(ref refunds);
                return Failed(RebirthNpcWorkFailureCategory.ExecutorFault, "Treatment failed and the medical supply was refunded: " + ex.Message);
            }
        }
        finally { reservation.Dispose(); }
    }

    public void CancelMedical(RebirthNpcConcreteWorkContext context, string reason)
    { if (context == null || context.Assignment == null) return; lock (sync) states.Remove(context.Assignment.AssignmentId); Interlocked.Increment(ref cancelled); }

    public static ulong[] GetActiveAssignmentIds()
    { lock (sync) { ulong[] ids = new ulong[states.Count]; states.Keys.CopyTo(ids, 0); Array.Sort(ids); return ids; } }
    public static void ResetForWorldChange() { lock (sync) states.Clear(); }

    public static string GetReport()
    { return "[REBIRTH NPC Medical Adapter] active=" + ActiveCount() + " begun=" + Interlocked.Read(ref begun) + " ticks=" + Interlocked.Read(ref ticks) +
        " treatments=" + Interlocked.Read(ref treatments) + " cancelled=" + Interlocked.Read(ref cancelled) +
        " overlapDenied=" + Interlocked.Read(ref overlapDenied) + " supplyFailures=" + Interlocked.Read(ref supplyFailures) +
        " patientFailures=" + Interlocked.Read(ref patientFailures) + " refunds=" + Interlocked.Read(ref refunds); }
    private static int ActiveCount() { lock (sync) return states.Count; }

    private static bool TryParse(string key, out State state, out string detail)
    {
        state = null; detail = string.Empty; string[] p = (key ?? string.Empty).Split(':');
        RebirthNpcStableId patient; int quantity, healing, workTicks;
        if (p.Length < 8 || !RebirthNpcStableId.TryParse(p[2], out patient) ||
            !int.TryParse(p[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) || quantity <= 0 ||
            !int.TryParse(p[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out healing) || healing <= 0 ||
            !int.TryParse(p[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out workTicks) || workTicks <= 0)
        { detail = "Medical treatment target key is invalid."; return false; }
        string settlement = Decode(p[3]); string item = Decode(p[4]);
        if (settlement.Length == 0 || item.Length == 0) { detail = "Medical settlement or supply key is missing."; return false; }
        state = new State { PatientId = patient, SettlementId = settlement, ItemKey = item,
            ItemQuantity = quantity, HealAmount = healing, RemainingTicks = workTicks, TotalTicks = workTicks };
        return true;
    }
    private static string Decode(string value) { return (value ?? string.Empty).Replace("%3A", ":"); }
    private static RebirthNpcWorkMutationResult Retry(string detail)
    { return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Retry, FailureCategory = RebirthNpcWorkFailureCategory.TargetUnavailable, Detail = detail }; }
    private static RebirthNpcWorkMutationResult Failed(RebirthNpcWorkFailureCategory category, string detail)
    { return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Failed, FailureCategory = category, Detail = detail }; }
    private static RebirthNpcWorkMutationResult Completed(string detail, Dictionary<string, int> consumed)
    { return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Completed,
        MutationCommitted = consumed != null, ProgressDelta = 1f, Detail = detail, Consumed = consumed }; }
}
