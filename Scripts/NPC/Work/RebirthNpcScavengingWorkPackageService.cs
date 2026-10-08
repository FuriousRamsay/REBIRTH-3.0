using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcScavengingNode
{
    public Vector3i Position { get; set; }
    public string SourceToken { get; set; }
    public string ItemKey { get; set; }
    public int Quantity { get; set; }
    public int WorkTicks { get; set; }
    public int Priority { get; set; }
    public bool AllowPartial { get; set; }
}

public sealed class RebirthNpcScavengingPackageRequest
{
    public string SessionKey { get; set; }
    public string SettlementId { get; set; }
    public IList<RebirthNpcScavengingNode> Nodes { get; set; }
    public int MaxAssignments { get; set; }
    public bool StartImmediately { get; set; }
    public bool StopOnFailure { get; set; }
}

public sealed class RebirthNpcScavengingPackageResult
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

public sealed class RebirthNpcScavengingPackageSnapshot
{
    public Guid PackageId { get; internal set; }
    public int AssignmentCount { get; internal set; }
    public int CurrentIndex { get; internal set; }
    public bool StopOnFailure { get; internal set; }
    public ulong[] AssignmentIds { get; internal set; }
    public long UpdatedUtcTicks { get; internal set; }
    public string Detail { get; internal set; }
}

public static class RebirthNpcScavengingWorkPackageService
{
    public const string ScavengingDefinitionId = "rebirth.scavenging.extract";

    private sealed class ActivePackage
    {
        public Guid Id;
        public readonly List<ulong> Assignments = new List<ulong>();
        public int CurrentIndex;
        public bool StopOnFailure;
        public bool StartImmediately;
        public string Detail;
        public long UpdatedUtcTicks;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<Guid, ActivePackage> Active = new Dictionary<Guid, ActivePackage>();
    private static readonly RebirthNpcFairRoundRobin<Guid> Schedule = new RebirthNpcFairRoundRobin<Guid>();
    private static long plans, packages, validatedNodes, created, started, conflicts, rejected,
        reconciliations, completedPackages, stoppedPackages, failedNodes;

    public static void RegisterDefinitions()
    {
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            ScavengingDefinitionId, RebirthNpcWorkKind.Scavenging, RebirthNpcWorkCapability.Scavenging,
            275, 1, TimeSpan.FromSeconds(60), false), true);
    }

    public static RebirthNpcScavengingNode[] Plan(RebirthNpcScavengingPackageRequest request, out string detail)
    {
        detail = string.Empty;
        Interlocked.Increment(ref plans);
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (request == null || request.Nodes == null || request.Nodes.Count == 0)
            return RejectPlan("At least one scavenging node is required.", out detail);
        if (world == null || world.IsRemote())
            return RejectPlan("Authoritative world is unavailable.", out detail);
        if (string.IsNullOrWhiteSpace(request.SettlementId))
            return RejectPlan("Settlement id is required for scavenged-resource authority.", out detail);

        int limit = Math.Max(1, Math.Min(512, request.MaxAssignments <= 0 ? 128 : request.MaxAssignments));
        List<RebirthNpcScavengingNode> valid = new List<RebirthNpcScavengingNode>();
        HashSet<string> identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int traversalBudget = Math.Min(request.Nodes.Count, 4096);
        for (int i = 0; i < traversalBudget; i++)
        {
            RebirthNpcScavengingNode source = request.Nodes[i];
            if (source == null) continue;
            string token = (source.SourceToken ?? string.Empty).Trim();
            string item = (source.ItemKey ?? string.Empty).Trim();
            string endpointError;
            RebirthNpcPhysicalResourceEndpoint endpoint = RebirthNpcPhysicalResourceEndpointRegistry.Ensure(token, out endpointError);
            if (endpoint == null || endpoint.Kind != RemoteResourceSourceKind.StaticContainer || !endpoint.Position.Equals(source.Position)) continue;
            if (item.Length == 0 || ItemClass.GetItem(item, false).IsEmpty() || source.Quantity <= 0) continue;
            int available = endpoint.GetAvailableDebitQuantity(item);
            int quantity = source.AllowPartial ? Math.Min(source.Quantity, available) : source.Quantity;
            if (quantity <= 0 || (!source.AllowPartial && available < source.Quantity)) continue;
            string identity = token + "|" + item;
            if (!identities.Add(identity)) continue;
            valid.Add(new RebirthNpcScavengingNode
            {
                Position = source.Position,
                SourceToken = token,
                ItemKey = item,
                Quantity = Math.Min(100000, quantity),
                WorkTicks = Math.Max(1, Math.Min(600, source.WorkTicks <= 0 ? 8 : source.WorkTicks)),
                Priority = source.Priority == 0 ? 275 : source.Priority,
                AllowPartial = source.AllowPartial
            });
        }
        valid.Sort(CompareNodes);
        if (valid.Count > limit) valid.RemoveRange(limit, valid.Count - limit);
        Interlocked.Add(ref validatedNodes, valid.Count);
        detail = "Planned " + valid.Count.ToString(CultureInfo.InvariantCulture) +
            " validated scavenging nodes in deterministic priority and route order.";
        return valid.ToArray();
    }

    public static RebirthNpcScavengingPackageResult CreateAndStart(RebirthNpcScavengingPackageRequest request)
    {
        string planningDetail;
        RebirthNpcScavengingNode[] plan = Plan(request, out planningDetail);
        RebirthNpcScavengingPackageResult result = new RebirthNpcScavengingPackageResult
        {
            PackageId = Guid.NewGuid(), Planned = plan.Length, Detail = planningDetail,
            AssignmentIds = new ulong[0]
        };
        if (request == null || plan.Length == 0 || string.IsNullOrWhiteSpace(request.SessionKey))
        {
            result.Rejected = plan.Length == 0 ? 1 : plan.Length;
            Interlocked.Add(ref rejected, result.Rejected);
            return result;
        }

        ActivePackage package = new ActivePackage
        {
            Id = result.PackageId,
            StopOnFailure = request.StopOnFailure,
            StartImmediately = request.StartImmediately,
            UpdatedUtcTicks = DateTime.UtcNow.Ticks,
            Detail = "Scavenging package created."
        };
        string settlement = request.SettlementId.Trim();
        for (int i = 0; i < plan.Length; i++)
        {
            RebirthNpcScavengingNode node = plan[i];
            RebirthNpcWorkAssignment assignment;
            string error;
            RebirthNpcWorkAssignmentResult createResult = RebirthNpcWorkAssignmentService.Create(
                new RebirthNpcWorkAssignmentRequest
                {
                    SessionKey = request.SessionKey,
                    DefinitionId = ScavengingDefinitionId,
                    TargetKey = BuildTargetKey(settlement, node),
                    TargetPosition = new Vector3(node.Position.x + 0.5f, node.Position.y + 0.5f, node.Position.z + 0.5f),
                    HasTargetPosition = true,
                    Priority = node.Priority
                }, out assignment, out error);
            if (createResult != RebirthNpcWorkAssignmentResult.Succeeded || assignment == null)
            {
                if (createResult == RebirthNpcWorkAssignmentResult.Conflict) result.Conflicts++;
                else result.Rejected++;
                continue;
            }
            package.Assignments.Add(assignment.AssignmentId);
            result.Created++;
        }
        result.AssignmentIds = package.Assignments.ToArray();
        if (package.Assignments.Count > 0)
        {
            lock (Sync) { Active[package.Id] = package; Schedule.Add(package.Id); }
            if (request.StartImmediately && TryStartCurrent(package)) result.Started = 1;
        }
        Interlocked.Increment(ref packages);
        Interlocked.Add(ref created, result.Created);
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
            if (assignment.Status == RebirthNpcWorkAssignmentStatus.Failed ||
                assignment.Status == RebirthNpcWorkAssignmentStatus.Cancelled)
            {
                Interlocked.Increment(ref failedNodes);
                if (package.StopOnFailure)
                {
                    package.Detail = "Package stopped after terminal scavenging node " + assignment.AssignmentId + ".";
                    CancelRemaining(package);
                    lock (Sync) RemovePackageLocked(package.Id);
                    Interlocked.Increment(ref stoppedPackages);
                    return;
                }
                package.CurrentIndex++;
                continue;
            }
            if (package.StartImmediately && assignment.Status == RebirthNpcWorkAssignmentStatus.Ready)
                TryStartCurrent(package);
            package.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
            package.Detail = "Waiting for scavenging assignment " + assignment.AssignmentId + " state=" + assignment.Status + ".";
            return;
        }
        package.Detail = "All scavenging nodes reached terminal state.";
        lock (Sync) RemovePackageLocked(package.Id);
        Interlocked.Increment(ref completedPackages);
    }

    private static bool TryStartCurrent(ActivePackage package)
    {
        if (package.CurrentIndex < 0 || package.CurrentIndex >= package.Assignments.Count) return false;
        string detail;
        bool ok = RebirthNpcWorkAssignmentCoordinator.Start(package.Assignments[package.CurrentIndex], out detail);
        package.Detail = detail;
        package.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
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
                RebirthNpcWorkAssignmentStatus.Cancelled,
                "Scavenging package stopped after an earlier failure.", out ignored);
        }
    }

    private static int CompareNodes(RebirthNpcScavengingNode a, RebirthNpcScavengingNode b)
    {
        int p = b.Priority.CompareTo(a.Priority);
        if (p != 0) return p;
        int x = a.Position.x.CompareTo(b.Position.x); if (x != 0) return x;
        int z = a.Position.z.CompareTo(b.Position.z); if (z != 0) return z;
        int y = a.Position.y.CompareTo(b.Position.y); if (y != 0) return y;
        int t = string.Compare(a.SourceToken, b.SourceToken, StringComparison.OrdinalIgnoreCase);
        return t != 0 ? t : string.Compare(a.ItemKey, b.ItemKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTargetKey(string settlement, RebirthNpcScavengingNode node)
    {
        return "scavenge:extract:" + settlement + ":" + node.SourceToken + ":" + node.ItemKey + ":" +
            node.Quantity.ToString(CultureInfo.InvariantCulture) + ":" +
            node.WorkTicks.ToString(CultureInfo.InvariantCulture) + ":" +
            (node.AllowPartial ? "partial" : "exact");
    }

    private static RebirthNpcScavengingNode[] RejectPlan(string message, out string detail)
    {
        detail = message;
        Interlocked.Increment(ref rejected);
        return new RebirthNpcScavengingNode[0];
    }

    public static RebirthNpcScavengingPackageSnapshot[] GetSnapshots()
    {
        lock (Sync)
        {
            RebirthNpcScavengingPackageSnapshot[] result = new RebirthNpcScavengingPackageSnapshot[Active.Count];
            int i = 0;
            foreach (ActivePackage p in Active.Values)
                result[i++] = new RebirthNpcScavengingPackageSnapshot
                {
                    PackageId = p.Id, AssignmentCount = p.Assignments.Count,
                    CurrentIndex = p.CurrentIndex, StopOnFailure = p.StopOnFailure,
                    AssignmentIds = p.Assignments.ToArray(), UpdatedUtcTicks = p.UpdatedUtcTicks,
                    Detail = p.Detail
                };
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
        int active;
        lock (Sync) active = Active.Count;
        return new StringBuilder("[REBIRTH NPC Scavenging Packages] active=").Append(active)
            .Append(" plans=").Append(Interlocked.Read(ref plans))
            .Append(" packages=").Append(Interlocked.Read(ref packages))
            .Append(" validatedNodes=").Append(Interlocked.Read(ref validatedNodes))
            .Append(" created=").Append(Interlocked.Read(ref created))
            .Append(" started=").Append(Interlocked.Read(ref started))
            .Append(" conflicts=").Append(Interlocked.Read(ref conflicts))
            .Append(" rejected=").Append(Interlocked.Read(ref rejected))
            .Append(" reconciliations=").Append(Interlocked.Read(ref reconciliations))
            .Append(" completedPackages=").Append(Interlocked.Read(ref completedPackages))
            .Append(" stoppedPackages=").Append(Interlocked.Read(ref stoppedPackages))
            .Append(" failedNodes=").Append(Interlocked.Read(ref failedNodes)).ToString();
    }
}

public sealed class RebirthNpcContainerScavengingAdapter : IRebirthNpcScavengingWorkAdapter
{
    private sealed class State
    {
        public RebirthNpcPhysicalResourceEndpoint Source;
        public RebirthNpcSettlementInventoryEndpoint Destination;
        public IRebirthNpcExternalInventoryReservation Debit;
        public IRebirthNpcExternalInventoryReservation Credit;
        public string ItemKey;
        public int Quantity;
        public int WorkTicks;
        public int Progress;
        public bool Committed;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<ulong, State> States = new Dictionary<ulong, State>();
    private static long begins, productiveTicks, commits, partials, targetFailures,
        destinationFailures, restorations, restorationFailures, cancellations;

    public string AdapterId { get { return "rebirth.scavenging.static-container"; } }
    public int Priority { get { return 700; } }

    public bool CanHandle(RebirthNpcConcreteWorkContext context)
    {
        return context != null && context.Assignment != null &&
            (context.Assignment.TargetKey ?? string.Empty).StartsWith("scavenge:extract:", StringComparison.OrdinalIgnoreCase);
    }

    public RebirthNpcWorkMutationResult BeginScavenging(RebirthNpcConcreteWorkContext context)
    {
        Interlocked.Increment(ref begins);
        string key = context.Assignment.TargetKey ?? string.Empty;
        const string root = "scavenge:extract:";
        if (!key.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, "Scavenging target contract is malformed.");
        string remainder = key.Substring(root.Length);
        string[] q = remainder.Split(':');
        int quantity, workTicks;
        if (q.Length != 6 || !int.TryParse(q[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) ||
            !int.TryParse(q[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out workTicks))
            return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, "Scavenging target contract is malformed.");
        string settlement = q[0].Trim();
        string token = q[1].Trim();
        string item = q[2].Trim();
        bool partial = string.Equals(q[5], "partial", StringComparison.OrdinalIgnoreCase);
        if (!token.StartsWith("S@", StringComparison.OrdinalIgnoreCase))
            return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, "Only static-container scavenging tokens are supported.");
        string endpointError;
        RebirthNpcPhysicalResourceEndpoint source = RebirthNpcPhysicalResourceEndpointRegistry.Ensure(token, out endpointError);
        RebirthNpcSettlementInventoryEndpoint destination = RebirthNpcSettlementInventoryEndpointRegistry.Ensure(settlement);
        if (source == null || source.Kind != RemoteResourceSourceKind.StaticContainer || destination == null)
            return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, endpointError.Length == 0 ? "Scavenging endpoints are unavailable." : endpointError);
        int available = source.GetAvailableDebitQuantity(item);
        int actual = partial ? Math.Min(quantity, available) : quantity;
        if (actual <= 0 || (!partial && available < quantity))
            return Failed(RebirthNpcWorkFailureCategory.MissingInput, "Scavenging source no longer contains the requested quantity.");
        if (partial && actual < quantity) Interlocked.Increment(ref partials);
        Guid debitId = context.OperationId;
        Guid creditId = Derive(context.OperationId, 1);
        IRebirthNpcExternalInventoryReservation debit, credit;
        string error;
        if (!source.TryReserveDebit(debitId, item, actual, out debit, out error))
            return Failed(RebirthNpcWorkFailureCategory.MissingInput, error);
        if (!destination.TryReserveCredit(creditId, item, actual, out credit, out error))
        { string ignored; debit.Rollback(out ignored); return Failed(RebirthNpcWorkFailureCategory.OutputBlocked, error); }
        State state = new State { Source = source, Destination = destination, Debit = debit, Credit = credit,
            ItemKey = item, Quantity = actual, WorkTicks = Math.Max(1, Math.Min(600, workTicks)) };
        lock (Sync) States[context.Assignment.AssignmentId] = state;
        return RebirthNpcWorkMutationResult.Continue(0f, "Scavenging reservations acquired.");
    }

    public RebirthNpcWorkMutationResult TickScavenging(RebirthNpcConcreteWorkContext context)
    {
        State state;
        lock (Sync) if (!States.TryGetValue(context.Assignment.AssignmentId, out state))
            return Failed(RebirthNpcWorkFailureCategory.ExecutorFault, "Scavenging adapter state disappeared.");
        if (state.Committed)
            return Completed(state, "Scavenging transaction was already committed.");
        state.Progress++;
        Interlocked.Increment(ref productiveTicks);
        if (state.Progress < state.WorkTicks)
            return RebirthNpcWorkMutationResult.Continue(1f / state.WorkTicks, "Scavenging source contents.");
        string error;
        if (!state.Debit.Commit(out error))
        { RollbackRemaining(state); Interlocked.Increment(ref targetFailures); return Failed(RebirthNpcWorkFailureCategory.TargetChanged, error); }
        if (!state.Credit.Commit(out error))
        {
            Interlocked.Increment(ref destinationFailures);
            string restoreError;
            IRebirthNpcExternalInventoryReservation compensation;
            Guid compensationId = Derive(context.OperationId, 2);
            bool restored = state.Source.TryReserveCredit(compensationId, state.ItemKey, state.Quantity,
                out compensation, out restoreError) && compensation.Commit(out restoreError);
            if (restored) Interlocked.Increment(ref restorations);
            else Interlocked.Increment(ref restorationFailures);
            return Failed(RebirthNpcWorkFailureCategory.OutputBlocked,
                "Settlement credit failed. Compensation=" + (restored ? "restored" : restoreError) + ". " + error);
        }
        state.Committed = true;
        Interlocked.Increment(ref commits);
        return Completed(state, "Scavenged resources committed to settlement inventory.");
    }

    public void CancelScavenging(RebirthNpcConcreteWorkContext context, string reason)
    {
        State state;
        lock (Sync) { if (!States.TryGetValue(context.Assignment.AssignmentId, out state)) return; States.Remove(context.Assignment.AssignmentId); }
        if (!state.Committed)
        {
            RollbackRemaining(state);
            Interlocked.Increment(ref cancellations);
        }
    }

    private static void RollbackRemaining(State state)
    {
        string ignored;
        if (state.Debit != null) state.Debit.Rollback(out ignored);
        if (state.Credit != null) state.Credit.Rollback(out ignored);
    }

    private static RebirthNpcWorkMutationResult Completed(State state, string detail)
    {
        return new RebirthNpcWorkMutationResult
        {
            Disposition = RebirthNpcWorkExecutionDisposition.Completed,
            Detail = detail,
            ProgressDelta = 1f,
            MutationCommitted = true,
            Produced = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { state.ItemKey, state.Quantity } }
        };
    }

    private static RebirthNpcWorkMutationResult Failed(RebirthNpcWorkFailureCategory category, string detail)
    {
        return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Failed,
            FailureCategory = category, Detail = detail ?? string.Empty };
    }

    private static Guid Derive(Guid source, byte discriminator)
    {
        byte[] bytes = source.ToByteArray();
        bytes[15] ^= discriminator;
        return new Guid(bytes);
    }

    public static string GetReport()
    {
        int active;
        lock (Sync) active = States.Count;
        return new StringBuilder("[REBIRTH NPC Container Scavenging Adapter] active=").Append(active)
            .Append(" begins=").Append(Interlocked.Read(ref begins))
            .Append(" productiveTicks=").Append(Interlocked.Read(ref productiveTicks))
            .Append(" commits=").Append(Interlocked.Read(ref commits))
            .Append(" partials=").Append(Interlocked.Read(ref partials))
            .Append(" targetFailures=").Append(Interlocked.Read(ref targetFailures))
            .Append(" destinationFailures=").Append(Interlocked.Read(ref destinationFailures))
            .Append(" restorations=").Append(Interlocked.Read(ref restorations))
            .Append(" restorationFailures=").Append(Interlocked.Read(ref restorationFailures))
            .Append(" cancellations=").Append(Interlocked.Read(ref cancellations)).ToString();
    }
}
