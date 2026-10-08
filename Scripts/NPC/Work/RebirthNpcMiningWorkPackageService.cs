using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcMiningNode
{
    public Vector3i Position { get; set; }
    public string ExpectedBlockName { get; set; }
    public string OutputItemKey { get; set; }
    public int OutputQuantity { get; set; }
    public int WorkTicks { get; set; }
    public int Priority { get; set; }
}

public sealed class RebirthNpcMiningPackageRequest
{
    public string SessionKey { get; set; }
    public string SettlementId { get; set; }
    public IList<RebirthNpcMiningNode> Nodes { get; set; }
    public int MaxAssignments { get; set; }
    public bool StartImmediately { get; set; }
    public bool StopOnFailure { get; set; }
}

public sealed class RebirthNpcMiningPackageResult
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

public sealed class RebirthNpcMiningPackageSnapshot
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
/// Server-authoritative mining package planner. Each node is an ordinary coordinate-aware work
/// assignment. Packages serialize extraction for one NPC, order nearby nodes deterministically,
/// and expose explicit expected-block and output contracts rather than deriving yields client-side.
/// </summary>
public static class RebirthNpcMiningWorkPackageService
{
    public const string MiningDefinitionId = "rebirth.mining.extract";

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
    private static long plans, packages, nodes, created, started, conflicts, rejected,
        reconciliations, completedPackages, stoppedPackages, failedNodes;

    public static void RegisterDefinitions()
    {
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            MiningDefinitionId, RebirthNpcWorkKind.Mining, RebirthNpcWorkCapability.Mining,
            300, 1, TimeSpan.FromSeconds(60), false), true);
    }

    public static RebirthNpcMiningNode[] Plan(RebirthNpcMiningPackageRequest request, out string detail)
    {
        detail = string.Empty;
        Interlocked.Increment(ref plans);
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (request == null || request.Nodes == null || request.Nodes.Count == 0)
            return RejectPlan("At least one mining node is required.", out detail);
        if (world == null || world.IsRemote())
            return RejectPlan("Authoritative world is unavailable.", out detail);
        if (string.IsNullOrWhiteSpace(request.SettlementId))
            return RejectPlan("Settlement id is required for mined-resource authority.", out detail);

        int limit = Math.Max(1, Math.Min(512, request.MaxAssignments <= 0 ? 128 : request.MaxAssignments));
        List<RebirthNpcMiningNode> valid = new List<RebirthNpcMiningNode>();
        HashSet<Vector3i> positions = new HashSet<Vector3i>();
        int traversalBudget = Math.Min(request.Nodes.Count, 4096);
        for (int i = 0; i < traversalBudget; i++)
        {
            RebirthNpcMiningNode source = request.Nodes[i];
            if (source == null || !positions.Add(source.Position)) continue;
            string blockName = (source.ExpectedBlockName ?? string.Empty).Trim();
            string output = (source.OutputItemKey ?? string.Empty).Trim();
            BlockValue expected = Block.GetBlockValue(blockName);
            BlockValue current = world.GetBlock(source.Position);
            if (expected.isair || current.isair || current.type != expected.type) continue;
            if (output.Length == 0 || ItemClass.GetItem(output, false).IsEmpty()) continue;
            if (source.OutputQuantity <= 0) continue;
            valid.Add(new RebirthNpcMiningNode
            {
                Position = source.Position,
                ExpectedBlockName = blockName,
                OutputItemKey = output,
                OutputQuantity = Math.Min(100000, source.OutputQuantity),
                WorkTicks = Math.Max(1, Math.Min(600, source.WorkTicks <= 0 ? 10 : source.WorkTicks)),
                Priority = source.Priority == 0 ? 300 : source.Priority
            });
        }
        valid.Sort(CompareNodes);
        if (valid.Count > limit) valid.RemoveRange(limit, valid.Count - limit);
        Interlocked.Add(ref nodes, valid.Count);
        detail = "Planned " + valid.Count.ToString(CultureInfo.InvariantCulture) +
            " validated mining nodes in deterministic priority and exposure order.";
        return valid.ToArray();
    }

    public static RebirthNpcMiningPackageResult CreateAndStart(RebirthNpcMiningPackageRequest request)
    {
        string planningDetail;
        RebirthNpcMiningNode[] plan = Plan(request, out planningDetail);
        RebirthNpcMiningPackageResult result = new RebirthNpcMiningPackageResult
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
            Detail = "Mining package created."
        };
        string settlement = request.SettlementId.Trim();
        for (int i = 0; i < plan.Length; i++)
        {
            RebirthNpcMiningNode node = plan[i];
            RebirthNpcWorkAssignment assignment;
            string error;
            RebirthNpcWorkAssignmentResult createResult = RebirthNpcWorkAssignmentService.Create(
                new RebirthNpcWorkAssignmentRequest
                {
                    SessionKey = request.SessionKey,
                    DefinitionId = MiningDefinitionId,
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
                    package.Detail = "Package stopped after terminal mining node " + assignment.AssignmentId + ".";
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
            package.Detail = "Waiting for mining assignment " + assignment.AssignmentId + " state=" + assignment.Status + ".";
            return;
        }
        package.Detail = "All mining nodes reached terminal state.";
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
                "Mining package stopped after an earlier failure.", out ignored);
        }
    }

    public static RebirthNpcMiningPackageSnapshot[] GetSnapshots()
    {
        lock (Sync)
        {
            RebirthNpcMiningPackageSnapshot[] result = new RebirthNpcMiningPackageSnapshot[Active.Count];
            int i = 0;
            foreach (ActivePackage p in Active.Values)
                result[i++] = new RebirthNpcMiningPackageSnapshot
                {
                    PackageId = p.Id, AssignmentCount = p.Assignments.Count,
                    CurrentIndex = p.CurrentIndex, StopOnFailure = p.StopOnFailure,
                    AssignmentIds = p.Assignments.ToArray(), UpdatedUtcTicks = p.UpdatedUtcTicks, Detail = p.Detail
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
        RebirthNpcMiningPackageSnapshot[] snapshot = GetSnapshots();
        StringBuilder b = new StringBuilder("[REBIRTH NPC Mining Packages] active=").Append(snapshot.Length)
            .Append(" plans=").Append(Interlocked.Read(ref plans)).Append(" packages=").Append(Interlocked.Read(ref packages))
            .Append(" nodes=").Append(Interlocked.Read(ref nodes)).Append(" created=").Append(Interlocked.Read(ref created))
            .Append(" started=").Append(Interlocked.Read(ref started)).Append(" conflicts=").Append(Interlocked.Read(ref conflicts))
            .Append(" rejected=").Append(Interlocked.Read(ref rejected)).Append(" reconciliations=").Append(Interlocked.Read(ref reconciliations))
            .Append(" completedPackages=").Append(Interlocked.Read(ref completedPackages)).Append(" stoppedPackages=").Append(Interlocked.Read(ref stoppedPackages))
            .Append(" failedNodes=").Append(Interlocked.Read(ref failedNodes));
        for (int i = 0; i < snapshot.Length; i++)
            b.AppendLine().Append("  package=").Append(snapshot[i].PackageId)
                .Append(" index=").Append(snapshot[i].CurrentIndex).Append('/').Append(snapshot[i].AssignmentCount)
                .Append(" stopOnFailure=").Append(snapshot[i].StopOnFailure).Append(" detail=").Append(snapshot[i].Detail);
        return b.ToString();
    }

    private static RebirthNpcMiningNode[] RejectPlan(string message, out string detail)
    { detail = message; Interlocked.Increment(ref rejected); return new RebirthNpcMiningNode[0]; }

    private static int CompareNodes(RebirthNpcMiningNode a, RebirthNpcMiningNode b)
    {
        int priority = b.Priority.CompareTo(a.Priority); if (priority != 0) return priority;
        int y = b.Position.y.CompareTo(a.Position.y); if (y != 0) return y;
        int x = a.Position.x.CompareTo(b.Position.x); return x != 0 ? x : a.Position.z.CompareTo(b.Position.z);
    }

    private static string BuildTargetKey(string settlement, RebirthNpcMiningNode node)
    {
        return "mining:extract:" + Encode(settlement) + ":" + Encode(node.ExpectedBlockName) + ":" +
            Encode(node.OutputItemKey) + ":" + node.OutputQuantity.ToString(CultureInfo.InvariantCulture) + ":" +
            node.WorkTicks.ToString(CultureInfo.InvariantCulture);
    }

    private static string Encode(string value) { return (value ?? string.Empty).Replace(":", "%3A"); }
}

/// <summary>
/// Concrete block extraction adapter. It validates the exact expected block, advances bounded
/// productive-work ticks, removes the block through the authoritative RPC path, and only then
/// credits the declared output to settlement inventory. A failed credit restores the source block.
/// </summary>
public sealed class RebirthNpcBlockMiningAdapter : IRebirthNpcMiningWorkAdapter
{
    private sealed class Operation
    {
        public string SettlementId;
        public string ExpectedBlockName;
        public string OutputItemKey;
        public int OutputQuantity;
        public int RequiredTicks;
        public int CompletedTicks;
        public Vector3i Position;
        public BlockValue Original;
        public bool Committed;
    }

    private readonly object sync = new object();
    private readonly Dictionary<ulong, Operation> operations = new Dictionary<ulong, Operation>();
    private static long begins, workTicks, commits, targetFailures, creditFailures, restorations, cancellations;

    public string AdapterId { get { return "rebirth.mining.block-extraction"; } }
    public int Priority { get { return 700; } }

    public bool CanHandle(RebirthNpcConcreteWorkContext context)
    {
        return context != null && context.Assignment != null &&
            (context.Assignment.TargetKey ?? string.Empty).StartsWith("mining:extract:", StringComparison.OrdinalIgnoreCase);
    }

    public RebirthNpcWorkMutationResult BeginMining(RebirthNpcConcreteWorkContext context)
    {
        Operation operation;
        string detail;
        if (!TryParse(context, out operation, out detail)) return Failed(RebirthNpcWorkFailureCategory.InvalidTarget, detail);
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote()) return Failed(RebirthNpcWorkFailureCategory.TargetUnavailable, "Authoritative world is unavailable.");
        if (!ValidateTarget(world, operation, out detail))
        { Interlocked.Increment(ref targetFailures); return Failed(RebirthNpcWorkFailureCategory.TargetChanged, detail); }
        operation.Original = world.GetBlock(operation.Position);
        lock (sync) operations[context.Assignment.AssignmentId] = operation;
        Interlocked.Increment(ref begins);
        return Continue(0f, "Mining target validated; productive extraction started.");
    }

    public RebirthNpcWorkMutationResult TickMining(RebirthNpcConcreteWorkContext context)
    {
        Operation operation;
        lock (sync)
            if (!operations.TryGetValue(context.Assignment.AssignmentId, out operation))
                return Failed(RebirthNpcWorkFailureCategory.ExecutorFault, "Mining operation state disappeared.");
        if (operation.Committed) return Completed(operation, "Mining mutation already committed.");
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote()) return Retry("Authoritative world is temporarily unavailable.");
        string detail;
        if (!ValidateTarget(world, operation, out detail))
        { Interlocked.Increment(ref targetFailures); return Failed(RebirthNpcWorkFailureCategory.TargetChanged, detail); }

        operation.CompletedTicks++;
        Interlocked.Increment(ref workTicks);
        if (operation.CompletedTicks < operation.RequiredTicks)
            return Continue(1f / operation.RequiredTicks,
                "Mining progress " + operation.CompletedTicks + "/" + operation.RequiredTicks + ".");

        try
        {
            world.SetBlockRPC(new BlockChangeInfo((BlockValueRef)operation.Position, BlockValue.Air, true, false));
        }
        catch (Exception ex)
        {
            return Failed(RebirthNpcWorkFailureCategory.ExecutorFault, "Authoritative mining mutation failed: " + ex.Message);
        }

        BlockValue removed = world.GetBlock(operation.Position);
        if (!removed.isair)
            return Failed(RebirthNpcWorkFailureCategory.TargetChanged,
                "Mining block removal was not retained; output was not credited.");

        if (!RebirthNpcSettlementSimulation.DepositResource(operation.SettlementId,
            operation.OutputItemKey, operation.OutputQuantity))
        {
            Interlocked.Increment(ref creditFailures);
            BlockValue current = world.GetBlock(operation.Position);
            if (!current.isair)
                return Failed(RebirthNpcWorkFailureCategory.ExecutorFault,
                    "Mining output credit failed after the mined position changed again; source restoration is indeterminate and was not forced.");
            try
            {
                world.SetBlockRPC(new BlockChangeInfo((BlockValueRef)operation.Position, operation.Original, true, false));
                BlockValue restoredBlock = world.GetBlock(operation.Position);
                if (restoredBlock.type != operation.Original.type || restoredBlock.rotation != operation.Original.rotation)
                    return Failed(RebirthNpcWorkFailureCategory.ExecutorFault,
                        "Mining output credit failed and source restoration was not retained.");
                Interlocked.Increment(ref restorations);
            }
            catch (Exception restoreEx)
            {
                return Failed(RebirthNpcWorkFailureCategory.ExecutorFault,
                    "Mining output credit failed and source restoration also failed: " + restoreEx.Message);
            }
            return Failed(RebirthNpcWorkFailureCategory.OutputBlocked,
                "Mining output credit failed; source block was restored.");
        }

        operation.Committed = true;
        Interlocked.Increment(ref commits);
        return Completed(operation, "Mined block and credited settlement output.");
    }

    public void CancelMining(RebirthNpcConcreteWorkContext context, string reason)
    {
        if (context == null || context.Assignment == null) return;
        lock (sync) operations.Remove(context.Assignment.AssignmentId);
        Interlocked.Increment(ref cancellations);
    }

    public static string GetReport()
    {
        return "[REBIRTH NPC Block Mining Adapter] begins=" + Interlocked.Read(ref begins) +
            " workTicks=" + Interlocked.Read(ref workTicks) + " commits=" + Interlocked.Read(ref commits) +
            " targetFailures=" + Interlocked.Read(ref targetFailures) + " creditFailures=" + Interlocked.Read(ref creditFailures) +
            " restorations=" + Interlocked.Read(ref restorations) + " cancellations=" + Interlocked.Read(ref cancellations);
    }

    private static bool TryParse(RebirthNpcConcreteWorkContext context, out Operation operation, out string detail)
    {
        operation = null;
        detail = string.Empty;
        string key = context != null && context.Assignment != null ? context.Assignment.TargetKey ?? string.Empty : string.Empty;
        string[] p = key.Split(':');
        if (p.Length < 7) { detail = "Mining target key is incomplete."; return false; }
        int quantity, ticks;
        if (!int.TryParse(p[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) || quantity <= 0 ||
            !int.TryParse(p[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) || ticks <= 0)
        { detail = "Mining output quantity or work ticks are invalid."; return false; }
        Vector3 position = context.Assignment.TargetPosition;
        operation = new Operation
        {
            SettlementId = Decode(p[2]), ExpectedBlockName = Decode(p[3]), OutputItemKey = Decode(p[4]),
            OutputQuantity = quantity, RequiredTicks = ticks,
            Position = new Vector3i(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z))
        };
        if (operation.SettlementId.Length == 0 || operation.ExpectedBlockName.Length == 0 || operation.OutputItemKey.Length == 0)
        { detail = "Mining settlement, expected block, or output item is missing."; return false; }
        return true;
    }

    private static bool ValidateTarget(World world, Operation operation, out string detail)
    {
        BlockValue expected = Block.GetBlockValue(operation.ExpectedBlockName);
        BlockValue current = world.GetBlock(operation.Position);
        if (expected.isair) { detail = "Expected mining block does not resolve."; return false; }
        if (current.isair) { detail = "Mining target is no longer present."; return false; }
        if (current.type != expected.type) { detail = "Mining target changed before extraction completed."; return false; }
        detail = "Mining target remains valid.";
        return true;
    }

    private static RebirthNpcWorkMutationResult Continue(float progress, string detail)
    {
        return new RebirthNpcWorkMutationResult
        {
            Disposition = RebirthNpcWorkExecutionDisposition.Continue,
            FailureCategory = RebirthNpcWorkFailureCategory.None,
            Detail = detail, ProgressDelta = progress
        };
    }

    private static RebirthNpcWorkMutationResult Retry(string detail)
    {
        return new RebirthNpcWorkMutationResult
        {
            Disposition = RebirthNpcWorkExecutionDisposition.Retry,
            FailureCategory = RebirthNpcWorkFailureCategory.TargetUnavailable,
            Detail = detail
        };
    }

    private static RebirthNpcWorkMutationResult Completed(Operation operation, string detail)
    {
        Dictionary<string, int> produced = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        produced[operation.OutputItemKey] = operation.OutputQuantity;
        return new RebirthNpcWorkMutationResult
        {
            Disposition = RebirthNpcWorkExecutionDisposition.Completed,
            FailureCategory = RebirthNpcWorkFailureCategory.None,
            Detail = detail, ProgressDelta = 1f, MutationCommitted = true, Produced = produced
        };
    }

    private static RebirthNpcWorkMutationResult Failed(RebirthNpcWorkFailureCategory category, string detail)
    {
        return new RebirthNpcWorkMutationResult
        {
            Disposition = RebirthNpcWorkExecutionDisposition.Failed,
            FailureCategory = category, Detail = detail
        };
    }

    private static string Decode(string value) { return (value ?? string.Empty).Replace("%3A", ":"); }
}
