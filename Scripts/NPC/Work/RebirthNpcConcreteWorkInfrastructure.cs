using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public enum RebirthNpcWorkFailureCategory : byte
{
    None = 0, InvalidTarget = 1, TargetUnavailable = 2, TargetChanged = 3,
    AuthorizationDenied = 4, OutOfRange = 5, MissingInput = 6,
    OutputBlocked = 7, InventoryUnavailable = 8, WorkstationUnavailable = 9,
    PathingFailure = 10, Interrupted = 11, Cancelled = 12, Superseded = 13,
    ExecutorFault = 14, RetryExhausted = 15, PersistenceRecovery = 16
}

public enum RebirthNpcWorkExecutionDisposition : byte
{
    Continue = 0, Completed = 1, Retry = 2, Suspend = 3, Failed = 4
}

public enum RebirthNpcWorkExecutorPhase : byte
{
    ResolvingTarget = 0, ValidatingTarget = 1, AcquiringInputs = 2,
    MovingToTarget = 3, Mutating = 4, CollectingOutput = 5,
    DeliveringOutput = 6, Finalizing = 7, Suspended = 8, Terminal = 9
}

public sealed class RebirthNpcWorkTargetSnapshot
{
    public string AdapterId { get; internal set; }
    public string StableTargetId { get; internal set; }
    public string TargetRevision { get; internal set; }
    public Vector3 Position { get; internal set; }
    public bool IsLoaded { get; internal set; }
    public bool IsAvailable { get; internal set; }
    public float InteractionRange { get; internal set; }
    public string Detail { get; internal set; }
}

public sealed class RebirthNpcWorkMutationResult
{
    public RebirthNpcWorkExecutionDisposition Disposition { get; set; }
    public RebirthNpcWorkFailureCategory FailureCategory { get; set; }
    public string Detail { get; set; }
    public float ProgressDelta { get; set; }
    public bool MutationCommitted { get; set; }
    public Dictionary<string, int> Produced { get; set; }
    public Dictionary<string, int> Consumed { get; set; }

    public static RebirthNpcWorkMutationResult Continue(float progress, string detail)
    {
        return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Continue,
            ProgressDelta = Math.Max(0f, progress), Detail = detail ?? string.Empty };
    }
}

public sealed class RebirthNpcConcreteWorkContext
{
    public RebirthNpcWorkAssignment Assignment { get; internal set; }
    public RebirthNpcWorkDefinition Definition { get; internal set; }
    public RebirthNpcWorkReservation Reservation { get; internal set; }
    public RebirthNpcWorkTargetSnapshot Target { get; internal set; }
    public Guid OperationId { get; internal set; }
    public int Attempt { get; internal set; }
    public float Progress { get; internal set; }
    public string AuthorityKey { get; internal set; }
}

public interface IRebirthNpcWorkTargetAdapter
{
    string AdapterId { get; }
    int Priority { get; }
    bool CanResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition);
    bool TryResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition,
        out RebirthNpcWorkTargetSnapshot target, out string detail);
    bool Validate(RebirthNpcConcreteWorkContext context, out RebirthNpcWorkFailureCategory failure,
        out string detail);
    bool IsNpcInRange(RebirthNpcConcreteWorkContext context, out string detail);
}

public static class RebirthNpcWorkTargetAdapterRegistry
{
    private static readonly object Sync = new object();
    private static readonly List<IRebirthNpcWorkTargetAdapter> Adapters = new List<IRebirthNpcWorkTargetAdapter>();

    public static void Register(IRebirthNpcWorkTargetAdapter adapter)
    {
        if (adapter == null || string.IsNullOrWhiteSpace(adapter.AdapterId))
            throw new ArgumentException("A valid target adapter is required.", nameof(adapter));
        lock (Sync)
        {
            for (int i = Adapters.Count - 1; i >= 0; i--)
                if (string.Equals(Adapters[i].AdapterId, adapter.AdapterId, StringComparison.OrdinalIgnoreCase))
                    Adapters.RemoveAt(i);
            Adapters.Add(adapter);
            Adapters.Sort(Compare);
        }
    }

    public static bool TryResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition,
        out IRebirthNpcWorkTargetAdapter adapter, out RebirthNpcWorkTargetSnapshot target, out string detail)
    {
        adapter = null; target = null; detail = string.Empty;
        IRebirthNpcWorkTargetAdapter[] snapshot;
        lock (Sync) snapshot = Adapters.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            try
            {
                if (!snapshot[i].CanResolve(assignment, definition)) continue;
                RebirthNpcWorkTargetSnapshot candidate;
                if (snapshot[i].TryResolve(assignment, definition, out candidate, out detail) && candidate != null)
                { adapter = snapshot[i]; target = candidate; return true; }
            }
            catch (Exception ex) { detail = "Target adapter failed: " + ex.Message; }
        }
        if (detail.Length == 0) detail = "No registered target adapter resolved the assignment target.";
        return false;
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder b = new StringBuilder("[REBIRTH NPC Work Target Adapters] registered=").Append(Adapters.Count);
            for (int i = 0; i < Adapters.Count; i++) b.AppendLine().Append("  ").Append(Adapters[i].AdapterId)
                .Append(" priority=").Append(Adapters[i].Priority);
            return b.ToString();
        }
    }

    private static int Compare(IRebirthNpcWorkTargetAdapter a, IRebirthNpcWorkTargetAdapter b)
    {
        int p = b.Priority.CompareTo(a.Priority);
        return p != 0 ? p : string.Compare(a.AdapterId, b.AdapterId, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class RebirthNpcWorkExecutionSnapshot
{
    public ulong AssignmentId; public string ExecutorId; public RebirthNpcWorkExecutorPhase Phase;
    public string StableTargetId; public float Progress; public int RetryCount;
    public string SuspensionReason; public string SourceEndpoint; public string DestinationEndpoint;
    public long UpdatedUtcTicks;
}

public static class RebirthNpcWorkExecutionTelemetry
{
    private const int MaxSnapshots = 512;
    private static readonly object Sync = new object();
    private static readonly Dictionary<ulong, RebirthNpcWorkExecutionSnapshot> Active =
        new Dictionary<ulong, RebirthNpcWorkExecutionSnapshot>();
    private static readonly RebirthNpcFairRoundRobin<ulong> Order =
        new RebirthNpcFairRoundRobin<ulong>();
    private static long revision;
    public static long Revision { get { lock (Sync) return revision; } }

    public static void Upsert(RebirthNpcWorkExecutionSnapshot snapshot)
    {
        if (snapshot == null || snapshot.AssignmentId == 0) return;
        lock (Sync)
        {
            if (!Active.ContainsKey(snapshot.AssignmentId)) Order.Add(snapshot.AssignmentId);
            snapshot.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
            Active[snapshot.AssignmentId] = snapshot;
            revision++;
            while (Active.Count > MaxSnapshots && Order.Count > 0)
            {
                ulong oldest;
                if(!Order.TryTake(out oldest))break;
                Active.Remove(oldest);
            }
        }
    }

    public static void Remove(ulong assignmentId)
    {
        lock (Sync)
        {
            Order.Remove(assignmentId);
            if (Active.Remove(assignmentId)) revision++;
        }
    }
    public static RebirthNpcWorkExecutionSnapshot[] Snapshot()
    {
        lock (Sync) { var r = new RebirthNpcWorkExecutionSnapshot[Active.Count]; Active.Values.CopyTo(r, 0);
            Array.Sort(r, delegate(RebirthNpcWorkExecutionSnapshot a, RebirthNpcWorkExecutionSnapshot b)
            { return a.AssignmentId.CompareTo(b.AssignmentId); }); return r; }
    }
    public static string GetReport()
    {
        RebirthNpcWorkExecutionSnapshot[] s = Snapshot();
        StringBuilder b = new StringBuilder("[REBIRTH NPC Concrete Work] active=").Append(s.Length);
        for (int i=0;i<s.Length;i++) b.AppendLine().Append("  assignment=").Append(s[i].AssignmentId)
            .Append(" executor=").Append(s[i].ExecutorId).Append(" phase=").Append(s[i].Phase)
            .Append(" target=").Append(s[i].StableTargetId).Append(" progress=").Append(s[i].Progress.ToString("0.000"))
            .Append(" retries=").Append(s[i].RetryCount).Append(" source=").Append(s[i].SourceEndpoint)
            .Append(" destination=").Append(s[i].DestinationEndpoint).Append(" suspension=").Append(s[i].SuspensionReason);
        return b.ToString();
    }
}

public static class RebirthNpcConcreteWorkRuntime
{
    private static int registered;
    private static int updateCounter;

    public static void Register()
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        if (Interlocked.Exchange(ref registered, 1) != 0) return;
        RebirthNpcFarmingWorkPackageService.RegisterDefinitions();
        RebirthNpcSettlementLogisticsPackageService.RegisterDefinitions();
        RebirthNpcConstructionWorkPackageService.RegisterDefinitions();
        RebirthNpcMiningWorkPackageService.RegisterDefinitions();
        RebirthNpcScavengingWorkPackageService.RegisterDefinitions();
        RebirthNpcGuardDutyWorkPackageService.RegisterDefinitions();
        RebirthNpcMedicalTreatmentWorkPackageService.RegisterDefinitions();
        RebirthNpcWorkTargetAdapterRegistry.Register(new RebirthNpcMedicalPatientTargetAdapter());
        RebirthNpcWorkTargetAdapterRegistry.Register(new RebirthNpcStableEntityWorkTargetAdapter());
        RebirthNpcWorkTargetAdapterRegistry.Register(new RebirthNpcPositionWorkTargetAdapter());
        RebirthNpcWorkOutcomePersistenceStore.EnsureLoaded();
        RebirthNpcWorkCheckpointPersistenceStore.EnsureLoaded();
        RebirthNpcHaulingPlanRegistry.Register(new RebirthNpcMixedEndpointHaulingPlanProvider());
        RebirthNpcHaulingPlanRegistry.Register(new RebirthNpcPhysicalResourceHaulingPlanProvider());
        RebirthNpcHaulingPlanRegistry.Register(new RebirthNpcSettlementHaulingPlanProvider());
        RebirthNpcConcreteWorkAdapterRegistry.Register(new RebirthNpcAdvancedFarmingPlantingWateringAdapter());
        RebirthNpcConcreteWorkAdapterRegistry.Register(new RebirthNpcAdvancedFarmingHarvestAdapter());
        RebirthNpcConcreteWorkAdapterRegistry.Register(new RebirthNpcBlockConstructionAdapter());
        RebirthNpcConcreteWorkAdapterRegistry.Register(new RebirthNpcBlockMiningAdapter());
        RebirthNpcConcreteWorkAdapterRegistry.Register(new RebirthNpcContainerScavengingAdapter());
        RebirthNpcConcreteWorkAdapterRegistry.Register(new RebirthNpcGuardPostWorkAdapter());
        RebirthNpcConcreteWorkAdapterRegistry.Register(new RebirthNpcMedicalTreatmentAdapter());
        RebirthNpcWorkAssignmentCoordinator.Register(new RebirthNpcFarmingWorkExecutor());
        RebirthNpcWorkAssignmentCoordinator.Register(new RebirthNpcHaulingWorkExecutor());
        RebirthNpcWorkAssignmentCoordinator.Register(new RebirthNpcMiningWorkExecutor());
        RebirthNpcWorkAssignmentCoordinator.Register(new RebirthNpcScavengingWorkExecutor());
        RebirthNpcWorkAssignmentCoordinator.Register(new RebirthNpcGuardDutyWorkExecutor());
        RebirthNpcWorkAssignmentCoordinator.Register(new RebirthNpcMedicalWorkExecutor());
        RebirthNpcWorkAssignmentCoordinator.Register(new RebirthNpcRepairWorkExecutor());
        RebirthNpcWorkAssignmentCoordinator.Register(new RebirthNpcCraftingWorkExecutor());
    }

    public static void Tick()
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        if (Interlocked.CompareExchange(ref registered, 0, 0) == 0) return;
        if ((++updateCounter % 10) != 0) return;
        RebirthNpcWorkAssignmentCoordinator.Tick(32);
        RebirthNpcFarmingWorkPackageService.Tick(8);
        RebirthNpcSettlementLogisticsPackageService.Tick(8);
        RebirthNpcWorkstationExecutionService.Tick(8);
        RebirthNpcConstructionWorkPackageService.Tick(8);
        RebirthNpcMiningWorkPackageService.Tick(8);
        RebirthNpcScavengingWorkPackageService.Tick(8);
        RebirthNpcGuardDutyWorkPackageService.Tick(8);
        RebirthNpcMedicalTreatmentWorkPackageService.Tick(8);
        RebirthNpcWorkOutcomeService.TickPendingDeliveries(16);
        RebirthNpcWorkOutcomePersistenceStore.Tick();
        RebirthNpcWorkCheckpointPersistenceStore.Tick();
    }
}
