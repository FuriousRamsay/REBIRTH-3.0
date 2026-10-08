using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public enum RebirthNpcConstructionAction : byte
{
    Place = 0,
    Repair = 1,
    Upgrade = 2
}

public sealed class RebirthNpcConstructionStep
{
    public RebirthNpcConstructionAction Action { get; set; }
    public Vector3i Position { get; set; }
    public string TargetBlockName { get; set; }
    public string MaterialItemKey { get; set; }
    public int MaterialQuantity { get; set; }
    public byte Rotation { get; set; }
    public bool PreserveRotation { get; set; }
    public int Priority { get; set; }
}

public sealed class RebirthNpcConstructionPackageRequest
{
    public string SessionKey { get; set; }
    public string SettlementId { get; set; }
    public IList<RebirthNpcConstructionStep> Steps { get; set; }
    public int MaxAssignments { get; set; }
    public bool StartImmediately { get; set; }
    public bool StopOnFailure { get; set; }
}

public sealed class RebirthNpcConstructionPackageResult
{
    public Guid PackageId { get; internal set; }
    public int Planned { get; internal set; }
    public int Created { get; internal set; }
    public int Started { get; internal set; }
    public int Conflicts { get; internal set; }
    public int Rejected { get; internal set; }
    public ulong[] AssignmentIds { get; internal set; }
    public string Detail { get; internal set; }
}

public sealed class RebirthNpcConstructionPackageSnapshot
{
    public Guid PackageId { get; internal set; }
    public int AssignmentCount { get; internal set; }
    public int CurrentIndex { get; internal set; }
    public bool StopOnFailure { get; internal set; }
    public ulong[] AssignmentIds { get; internal set; }
    public long UpdatedUtcTicks { get; internal set; }
    public string Detail { get; internal set; }
}

/// <summary>
/// Server-authoritative construction package planner. Ordinary work assignments remain the execution,
/// reservation, navigation, persistence and outcome boundary. Packages add deterministic step order,
/// one-at-a-time activation, stop-on-failure behavior and package-level diagnostics.
/// </summary>
public static class RebirthNpcConstructionWorkPackageService
{
    public const string PlaceDefinitionId = "rebirth.construction.place";
    public const string RepairDefinitionId = "rebirth.construction.repair";
    public const string UpgradeDefinitionId = "rebirth.construction.upgrade";

    private sealed class ActivePackage
    {
        public Guid Id;
        public readonly List<ulong> Assignments = new List<ulong>();
        public int CurrentIndex;
        public bool StopOnFailure;
        public bool StartImmediately;
        public long UpdatedUtcTicks;
        public string Detail;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<Guid, ActivePackage> Active = new Dictionary<Guid, ActivePackage>();
    private static readonly RebirthNpcFairRoundRobin<Guid> Schedule = new RebirthNpcFairRoundRobin<Guid>();
    private static long plans, packages, steps, created, started, conflicts, rejected,
        reconciliations, completedPackages, stoppedPackages, failedSteps;

    public static void RegisterDefinitions()
    {
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            PlaceDefinitionId, RebirthNpcWorkKind.Construction,
            RebirthNpcWorkCapability.Construction, 310, 1, TimeSpan.FromSeconds(60), false), true);
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            RepairDefinitionId, RebirthNpcWorkKind.Repair,
            RebirthNpcWorkCapability.Repair, 330, 1, TimeSpan.FromSeconds(45), false), true);
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            UpgradeDefinitionId, RebirthNpcWorkKind.Construction,
            RebirthNpcWorkCapability.Construction | RebirthNpcWorkCapability.Repair,
            320, 1, TimeSpan.FromSeconds(60), false), true);
    }

    public static RebirthNpcConstructionStep[] Plan(RebirthNpcConstructionPackageRequest request, out string detail)
    {
        detail = string.Empty;
        Interlocked.Increment(ref plans);
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (request == null || request.Steps == null || request.Steps.Count == 0)
            return RejectPlan("At least one construction step is required.", out detail);
        if (world == null || world.IsRemote())
            return RejectPlan("Authoritative world is unavailable.", out detail);
        if (string.IsNullOrWhiteSpace(request.SettlementId))
            return RejectPlan("Settlement id is required for construction material authority.", out detail);

        int limit = Math.Max(1, Math.Min(512, request.MaxAssignments <= 0 ? 128 : request.MaxAssignments));
        List<RebirthNpcConstructionStep> result = new List<RebirthNpcConstructionStep>();
        HashSet<Vector3i> positions = new HashSet<Vector3i>();
        int traversalBudget = Math.Min(request.Steps.Count, 4096);
        for (int i = 0; i < traversalBudget; i++)
        {
            RebirthNpcConstructionStep source = request.Steps[i];
            if (source == null || !positions.Add(source.Position)) continue;
            string material = (source.MaterialItemKey ?? string.Empty).Trim();
            if (material.Length == 0 || source.MaterialQuantity <= 0 || ItemClass.GetItem(material, false).IsEmpty()) continue;
            BlockValue current = world.GetBlock(source.Position);
            string blockName = (source.TargetBlockName ?? string.Empty).Trim();
            if (source.Action == RebirthNpcConstructionAction.Place)
            {
                BlockValue target = Block.GetBlockValue(blockName);
                if (!current.isair || target.isair) continue;
            }
            else if (source.Action == RebirthNpcConstructionAction.Repair)
            {
                if (current.isair || current.damage <= 0) continue;
            }
            else
            {
                BlockValue target = Block.GetBlockValue(blockName);
                if (current.isair || target.isair || current.type == target.type) continue;
            }
            result.Add(new RebirthNpcConstructionStep
            {
                Action = source.Action, Position = source.Position, TargetBlockName = blockName,
                MaterialItemKey = material, MaterialQuantity = Math.Min(100000, source.MaterialQuantity),
                Rotation = source.Rotation, PreserveRotation = source.PreserveRotation,
                Priority = source.Priority == 0 ? DefaultPriority(source.Action) : source.Priority
            });
        }
        result.Sort(CompareSteps);
        if (result.Count > limit) result.RemoveRange(limit, result.Count - limit);
        Interlocked.Add(ref steps, result.Count);
        detail = "Planned " + result.Count.ToString(CultureInfo.InvariantCulture) +
            " validated construction steps in support-sensitive order.";
        return result.ToArray();
    }

    public static RebirthNpcConstructionPackageResult CreateAndStart(RebirthNpcConstructionPackageRequest request)
    {
        string planningDetail;
        RebirthNpcConstructionStep[] plan = Plan(request, out planningDetail);
        RebirthNpcConstructionPackageResult result = new RebirthNpcConstructionPackageResult
        { PackageId = Guid.NewGuid(), Planned = plan.Length, Detail = planningDetail, AssignmentIds = new ulong[0] };
        if (request == null || plan.Length == 0 || string.IsNullOrWhiteSpace(request.SessionKey))
        { result.Rejected = plan.Length == 0 ? 1 : plan.Length; Interlocked.Add(ref rejected, result.Rejected); return result; }

        ActivePackage package = new ActivePackage
        {
            Id = result.PackageId, StopOnFailure = request.StopOnFailure,
            StartImmediately = request.StartImmediately, UpdatedUtcTicks = DateTime.UtcNow.Ticks,
            Detail = "Construction package created."
        };
        string settlement = request.SettlementId.Trim();
        for (int i = 0; i < plan.Length; i++)
        {
            RebirthNpcConstructionStep step = plan[i];
            RebirthNpcWorkAssignment assignment;
            string error = string.Empty;
            RebirthNpcWorkAssignmentResult create = RebirthNpcWorkAssignmentService.Create(
                new RebirthNpcWorkAssignmentRequest
                {
                    SessionKey = request.SessionKey,
                    DefinitionId = Definition(step.Action),
                    TargetKey = BuildTargetKey(settlement, step),
                    TargetPosition = new Vector3(step.Position.x + 0.5f, step.Position.y, step.Position.z + 0.5f),
                    HasTargetPosition = true,
                    Priority = step.Priority
                }, out assignment, out error);
            if (create != RebirthNpcWorkAssignmentResult.Succeeded || assignment == null)
            { if (create == RebirthNpcWorkAssignmentResult.Conflict) result.Conflicts++; else result.Rejected++; continue; }
            package.Assignments.Add(assignment.AssignmentId);
            result.Created++;
        }
        result.AssignmentIds = package.Assignments.ToArray();
        if (package.Assignments.Count > 0)
        {
            lock (Sync) { Active[package.Id] = package; Schedule.Add(package.Id); }
            if (request.StartImmediately && TryStartCurrent(package)) result.Started = 1;
        }
        Interlocked.Increment(ref packages); Interlocked.Add(ref created, result.Created);
        Interlocked.Add(ref conflicts, result.Conflicts);
        Interlocked.Add(ref rejected, result.Rejected);
        result.Detail = planningDetail + " Created=" + result.Created + " started=" + result.Started +
            " conflicts=" + result.Conflicts + " rejected=" + result.Rejected + ".";
        return result;
    }

    public static void Tick(int budget)
    {
        if (budget <= 0) return;
        for (int processed=0; processed<budget; processed++)
        {
            Guid packageId; ActivePackage package;
            lock (Sync)
            {
                if(!Schedule.TryTake(out packageId))break;
                if(!Active.TryGetValue(packageId,out package))continue;
            }
            Reconcile(package);
            Interlocked.Increment(ref reconciliations);
            lock (Sync) if(Active.ContainsKey(packageId))Schedule.Return(packageId);
        }
    }


    private static void Reconcile(ActivePackage package)
    {
        while (package.CurrentIndex < package.Assignments.Count)
        {
            RebirthNpcWorkAssignment assignment;
            if (!RebirthNpcWorkAssignmentService.TryGet(package.Assignments[package.CurrentIndex], out assignment))
            { package.CurrentIndex++; continue; }
            if (assignment.Status == RebirthNpcWorkAssignmentStatus.Completed)
            { package.CurrentIndex++; continue; }
            if (assignment.Status == RebirthNpcWorkAssignmentStatus.Failed || assignment.Status == RebirthNpcWorkAssignmentStatus.Cancelled)
            {
                Interlocked.Increment(ref failedSteps);
                if (package.StopOnFailure)
                {
                    package.Detail = "Package stopped after terminal construction step " + assignment.AssignmentId + ".";
                    CancelRemaining(package); lock (Sync) RemovePackageLocked(package.Id); Interlocked.Increment(ref stoppedPackages); return;
                }
                package.CurrentIndex++; continue;
            }
            if (package.StartImmediately && assignment.Status == RebirthNpcWorkAssignmentStatus.Ready) TryStartCurrent(package);
            package.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
            package.Detail = "Waiting for construction assignment " + assignment.AssignmentId + " state=" + assignment.Status + ".";
            return;
        }
        package.Detail = "All construction steps reached terminal state.";
        lock (Sync) RemovePackageLocked(package.Id); Interlocked.Increment(ref completedPackages);
    }

    private static bool TryStartCurrent(ActivePackage package)
    {
        if (package.CurrentIndex < 0 || package.CurrentIndex >= package.Assignments.Count) return false;
        string detail;
        bool ok = RebirthNpcWorkAssignmentCoordinator.Start(package.Assignments[package.CurrentIndex], out detail);
        package.Detail = detail; package.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
        if (ok) Interlocked.Increment(ref started);
        return ok;
    }

    private static void CancelRemaining(ActivePackage package)
    {
        for (int i = package.CurrentIndex + 1; i < package.Assignments.Count; i++)
        {
            RebirthNpcWorkAssignment assignment;
            if (!RebirthNpcWorkAssignmentService.TryGet(package.Assignments[i], out assignment)) continue;
            RebirthNpcWorkAssignment ignored;
            RebirthNpcWorkAssignmentService.TryTransition(assignment.AssignmentId, assignment.Revision,
                RebirthNpcWorkAssignmentStatus.Cancelled, "Construction package stopped after an earlier failure.", out ignored);
        }
    }

    public static RebirthNpcConstructionPackageSnapshot[] GetSnapshots()
    {
        lock (Sync)
        {
            RebirthNpcConstructionPackageSnapshot[] result = new RebirthNpcConstructionPackageSnapshot[Active.Count]; int i = 0;
            foreach (ActivePackage p in Active.Values) result[i++] = new RebirthNpcConstructionPackageSnapshot
            { PackageId = p.Id, AssignmentCount = p.Assignments.Count, CurrentIndex = p.CurrentIndex,
              StopOnFailure = p.StopOnFailure, AssignmentIds = p.Assignments.ToArray(),
              UpdatedUtcTicks = p.UpdatedUtcTicks, Detail = p.Detail };
            return result;
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
    }

    public static string GetReport()
    {
        RebirthNpcConstructionPackageSnapshot[] snapshot = GetSnapshots();
        StringBuilder b = new StringBuilder("[REBIRTH NPC Construction Packages] active=").Append(snapshot.Length)
            .Append(" plans=").Append(Interlocked.Read(ref plans)).Append(" packages=").Append(Interlocked.Read(ref packages))
            .Append(" steps=").Append(Interlocked.Read(ref steps)).Append(" created=").Append(Interlocked.Read(ref created))
            .Append(" started=").Append(Interlocked.Read(ref started)).Append(" conflicts=").Append(Interlocked.Read(ref conflicts))
            .Append(" rejected=").Append(Interlocked.Read(ref rejected)).Append(" reconciliations=").Append(Interlocked.Read(ref reconciliations))
            .Append(" completedPackages=").Append(Interlocked.Read(ref completedPackages)).Append(" stoppedPackages=").Append(Interlocked.Read(ref stoppedPackages))
            .Append(" failedSteps=").Append(Interlocked.Read(ref failedSteps));
        for (int i = 0; i < snapshot.Length; i++) b.AppendLine().Append("  package=").Append(snapshot[i].PackageId)
            .Append(" index=").Append(snapshot[i].CurrentIndex).Append('/').Append(snapshot[i].AssignmentCount)
            .Append(" stopOnFailure=").Append(snapshot[i].StopOnFailure).Append(" detail=").Append(snapshot[i].Detail);
        return b.ToString();
    }

    private static RebirthNpcConstructionStep[] RejectPlan(string message, out string detail)
    { detail = message; Interlocked.Increment(ref rejected); return new RebirthNpcConstructionStep[0]; }
    private static int DefaultPriority(RebirthNpcConstructionAction action)
    { return action == RebirthNpcConstructionAction.Repair ? 330 : action == RebirthNpcConstructionAction.Upgrade ? 320 : 310; }
    private static string Definition(RebirthNpcConstructionAction action)
    { return action == RebirthNpcConstructionAction.Repair ? RepairDefinitionId : action == RebirthNpcConstructionAction.Upgrade ? UpgradeDefinitionId : PlaceDefinitionId; }
    private static int CompareSteps(RebirthNpcConstructionStep a, RebirthNpcConstructionStep b)
    {
        int y = a.Position.y.CompareTo(b.Position.y); if (y != 0) return y;
        int action = a.Action.CompareTo(b.Action); if (action != 0) return action;
        int x = a.Position.x.CompareTo(b.Position.x); return x != 0 ? x : a.Position.z.CompareTo(b.Position.z);
    }
    private static string BuildTargetKey(string settlement, RebirthNpcConstructionStep step)
    {
        return "construction:" + step.Action.ToString().ToLowerInvariant() + ":" + settlement + ":" +
            Encode(step.TargetBlockName) + ":" + Encode(step.MaterialItemKey) + ":" +
            step.MaterialQuantity.ToString(CultureInfo.InvariantCulture) + ":" +
            step.Rotation.ToString(CultureInfo.InvariantCulture) + ":" + (step.PreserveRotation ? "preserve" : "explicit");
    }
    private static string Encode(string value) { return (value ?? string.Empty).Replace(":", "%3A"); }
}

/// <summary>Concrete repair/construction adapter using settlement inventory authority and native world mutation.</summary>
public sealed class RebirthNpcBlockConstructionAdapter : IRebirthNpcRepairWorkAdapter
{
    private sealed class Operation
    {
        public RebirthNpcConstructionAction Action; public string SettlementId; public string BlockName;
        public string Material; public int Quantity; public byte Rotation; public bool PreserveRotation;
        public Vector3i Position; public bool Committed;
    }
    private readonly object sync = new object();
    private readonly Dictionary<ulong, Operation> operations = new Dictionary<ulong, Operation>();
    private static long begins, commits, materialFailures, targetFailures, cancellations;
    public string AdapterId { get { return "rebirth.construction.block"; } }
    public int Priority { get { return 700; } }

    public bool CanHandle(RebirthNpcConcreteWorkContext context)
    { return context != null && context.Assignment != null && (context.Assignment.TargetKey ?? string.Empty).StartsWith("construction:", StringComparison.OrdinalIgnoreCase); }

    public RebirthNpcWorkMutationResult BeginRepair(RebirthNpcConcreteWorkContext context)
    {
        Operation operation; string detail;
        if (!TryParse(context, out operation, out detail)) return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, detail);
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote()) return Failed(RebirthNpcWorkFailureCategory.TargetUnavailable, "Authoritative world is unavailable.");
        if (!ValidateTarget(world, operation, out detail)) { Interlocked.Increment(ref targetFailures); return Failed(RebirthNpcWorkFailureCategory.TargetChanged, detail); }
        RebirthNpcSettlementInventoryEndpoint endpoint = RebirthNpcSettlementInventoryEndpointRegistry.Ensure(operation.SettlementId);
        if (endpoint == null || endpoint.GetAvailableDebitQuantity(operation.Material) < operation.Quantity)
        { Interlocked.Increment(ref materialFailures); return Failed(RebirthNpcWorkFailureCategory.MissingInput, "Settlement lacks unreserved construction material " + operation.Material + "."); }
        lock (sync) operations[context.Assignment.AssignmentId] = operation;
        Interlocked.Increment(ref begins);
        return RebirthNpcWorkMutationResult.Continue(0.15f, "Construction target and settlement materials validated.");
    }

    public RebirthNpcWorkMutationResult TickRepair(RebirthNpcConcreteWorkContext context)
    {
        Operation operation; lock (sync) if (!operations.TryGetValue(context.Assignment.AssignmentId, out operation))
            return Failed(RebirthNpcWorkFailureCategory.ExecutorFault, "Construction operation state disappeared.");
        if (operation.Committed) return Completed("Construction mutation already committed.", null);
        World world = GameManager.Instance != null ? GameManager.Instance.World : null; string detail;
        if (world == null || world.IsRemote()) return Retry("Authoritative world is temporarily unavailable.");
        if (!ValidateTarget(world, operation, out detail)) return Failed(RebirthNpcWorkFailureCategory.TargetChanged, detail);
        RebirthNpcSettlementInventoryEndpoint endpoint = RebirthNpcSettlementInventoryEndpointRegistry.Ensure(operation.SettlementId);
        Guid transactionId = Guid.NewGuid(); IRebirthNpcExternalInventoryReservation reservation; string error = string.Empty;
        if (endpoint == null || !endpoint.TryReserveDebit(transactionId, operation.Material, operation.Quantity, out reservation, out error) || reservation == null)
        { Interlocked.Increment(ref materialFailures); return Retry("Construction material reservation failed: " + error); }
        try
        {
            if (!reservation.Commit(out error)) return Retry("Construction material commit failed: " + error);
            BlockValue current = world.GetBlock(operation.Position); BlockValue original = current; BlockValue target;
            if (operation.Action == RebirthNpcConstructionAction.Repair)
            { target = current; target.damage = 0; }
            else
            {
                target = Block.GetBlockValue(operation.BlockName);
                target.rotation = operation.PreserveRotation ? current.rotation : operation.Rotation;
            }
            try
            {
                world.SetBlockRPC(new BlockChangeInfo((BlockValueRef)operation.Position, target, true, false));
            }
            catch (Exception ex)
            {
                RebirthNpcSettlementSimulation.DepositResource(operation.SettlementId, operation.Material, operation.Quantity);
                return Failed(RebirthNpcWorkFailureCategory.ExecutorFault, "World mutation failed and construction material was refunded: " + ex.Message);
            }
            BlockValue committed = world.GetBlock(operation.Position);
            bool retained = operation.Action == RebirthNpcConstructionAction.Repair
                ? (!committed.isair && committed.type == target.type && committed.damage == 0)
                : (!committed.isair && committed.type == target.type && committed.rotation == target.rotation);
            if (!retained)
            {
                // Do not overwrite the world: another mutation may already own the target.
                bool refunded = RebirthNpcSettlementSimulation.DepositResource(operation.SettlementId, operation.Material, operation.Quantity);
                return Failed(RebirthNpcWorkFailureCategory.TargetChanged,
                    "Construction mutation was not retained; material refund=" + refunded + ". Original block type=" + original.type + ".");
            }
            operation.Committed = true; Interlocked.Increment(ref commits);
            Dictionary<string, int> consumed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            consumed[operation.Material] = operation.Quantity;
            return Completed(operation.Action + " committed at " + operation.Position + ".", consumed);
        }
        finally { reservation.Dispose(); }
    }

    public void CancelRepair(RebirthNpcConcreteWorkContext context, string reason)
    { if (context == null || context.Assignment == null) return; lock (sync) operations.Remove(context.Assignment.AssignmentId); Interlocked.Increment(ref cancellations); }

    public static string GetReport()
    { return "[REBIRTH NPC Block Construction Adapter] begins=" + Interlocked.Read(ref begins) + " commits=" + Interlocked.Read(ref commits) +
        " materialFailures=" + Interlocked.Read(ref materialFailures) + " targetFailures=" + Interlocked.Read(ref targetFailures) +
        " cancellations=" + Interlocked.Read(ref cancellations); }

    private static bool TryParse(RebirthNpcConcreteWorkContext context, out Operation operation, out string detail)
    {
        operation = null; detail = string.Empty; string key = context?.Assignment?.TargetKey ?? string.Empty; string[] p = key.Split(':');
        if (p.Length < 8) { detail = "Construction target key is incomplete."; return false; }
        RebirthNpcConstructionAction action;
        if (!Enum.TryParse(p[1], true, out action)) { detail = "Construction action is invalid."; return false; }
        int quantity; byte rotation;
        if (!int.TryParse(p[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) || quantity <= 0 ||
            !byte.TryParse(p[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out rotation))
        { detail = "Construction material quantity or rotation is invalid."; return false; }
        Vector3 position = context.Assignment.TargetPosition;
        operation = new Operation { Action = action, SettlementId = Decode(p[2]), BlockName = Decode(p[3]), Material = Decode(p[4]),
            Quantity = quantity, Rotation = rotation, PreserveRotation = string.Equals(p[7], "preserve", StringComparison.OrdinalIgnoreCase),
            Position = new Vector3i(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z)) };
        if (operation.SettlementId.Length == 0 || operation.Material.Length == 0) { detail = "Construction settlement or material is missing."; return false; }
        return true;
    }

    private static bool ValidateTarget(World world, Operation operation, out string detail)
    {
        BlockValue current = world.GetBlock(operation.Position); detail = string.Empty;
        if (operation.Action == RebirthNpcConstructionAction.Place)
        { if (!current.isair) { detail = "Placement position is no longer empty."; return false; } if (Block.GetBlockValue(operation.BlockName).isair) { detail = "Placement block is unavailable."; return false; } }
        else if (operation.Action == RebirthNpcConstructionAction.Repair)
        { if (current.isair || current.damage <= 0) { detail = "Repair target is absent or no longer damaged."; return false; } }
        else
        { BlockValue target = Block.GetBlockValue(operation.BlockName); if (current.isair || target.isair || current.type == target.type) { detail = "Upgrade target is absent or already upgraded."; return false; } }
        detail = "Construction target remains valid."; return true;
    }

    private static RebirthNpcWorkMutationResult Completed(string detail, Dictionary<string, int> consumed)
    { return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Completed, FailureCategory = RebirthNpcWorkFailureCategory.None,
        Detail = detail, ProgressDelta = 1f, MutationCommitted = true, Consumed = consumed }; }
    private static RebirthNpcWorkMutationResult Retry(string detail)
    { return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Retry, FailureCategory = RebirthNpcWorkFailureCategory.MissingInput, Detail = detail }; }
    private static RebirthNpcWorkMutationResult Failed(RebirthNpcWorkFailureCategory category, string detail)
    { return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Failed, FailureCategory = category, Detail = detail }; }
    private static string Decode(string value) { return (value ?? string.Empty).Replace("%3A", ":"); }
}
