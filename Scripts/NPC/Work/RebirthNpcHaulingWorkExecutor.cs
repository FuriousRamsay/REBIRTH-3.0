using System;
using System.Collections.Generic;

#nullable disable

public sealed class RebirthNpcHaulingPlan
{
    public string SourceEndpointId { get; set; }
    public string DestinationEndpointId { get; set; }
    public string ItemKey { get; set; }
    public int Quantity { get; set; }
    public bool AllowPartial { get; set; }
}

public interface IRebirthNpcHaulingPlanProvider
{
    int Priority { get; }
    bool TryPlan(RebirthNpcConcreteWorkContext context, out RebirthNpcHaulingPlan plan, out string detail);
}

public static class RebirthNpcHaulingPlanRegistry
{
    private static readonly object Sync = new object();
    private static readonly List<IRebirthNpcHaulingPlanProvider> Providers = new List<IRebirthNpcHaulingPlanProvider>();
    public static void Register(IRebirthNpcHaulingPlanProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        lock (Sync) { if (!Providers.Contains(provider)) Providers.Add(provider);
            Providers.Sort(delegate(IRebirthNpcHaulingPlanProvider a, IRebirthNpcHaulingPlanProvider b) { return b.Priority.CompareTo(a.Priority); }); }
    }
    public static bool TryPlan(RebirthNpcConcreteWorkContext context, out RebirthNpcHaulingPlan plan, out string detail)
    {
        plan = null; detail = string.Empty; IRebirthNpcHaulingPlanProvider[] snapshot;
        lock (Sync) snapshot = Providers.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
            if (snapshot[i].TryPlan(context, out plan, out detail) && plan != null) return true;
        if (detail.Length == 0) detail = "No hauling plan provider produced a transfer plan.";
        return false;
    }
    public static string GetReport() { lock (Sync) return "[REBIRTH NPC Hauling Plans] providers=" + Providers.Count; }
}

public sealed class RebirthNpcHaulingWorkExecutor : RebirthNpcConcreteWorkExecutorBase
{
    private sealed class HaulState
    {
        public RebirthNpcHaulingPlan Plan;
        public bool Withdrawn;
        public uint NpcRevision;
        public int Quantity;
    }
    private readonly Dictionary<ulong, HaulState> hauling = new Dictionary<ulong, HaulState>();
    public override string ExecutorId { get { return "rebirth.work.hauling"; } }
    public override int Priority { get { return 290; } }
    protected override RebirthNpcWorkKind Kind { get { return RebirthNpcWorkKind.Hauling; } }

    protected override RebirthNpcWorkMutationResult BeginCore(State state, out string detail)
    {
        RebirthNpcHaulingPlan plan;
        if (!RebirthNpcHaulingPlanRegistry.TryPlan(state.Context, out plan, out detail) ||
            string.IsNullOrWhiteSpace(plan.SourceEndpointId) || string.IsNullOrWhiteSpace(plan.DestinationEndpointId) ||
            string.IsNullOrWhiteSpace(plan.ItemKey) || plan.Quantity <= 0)
            return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Failed,
                FailureCategory = RebirthNpcWorkFailureCategory.InvalidTarget, Detail = detail };
        int quantity = plan.Quantity;
        if (plan.AllowPartial)
        {
            quantity = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                plan.SourceEndpointId, plan.ItemKey, quantity, true);
            quantity = RebirthNpcExternalInventoryEndpointRegistry.GetNegotiatedQuantity(
                plan.DestinationEndpointId, plan.ItemKey, quantity, false);
            if (quantity <= 0)
            {
                detail = "No transferable quantity is currently available for the partial hauling plan.";
                return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Suspend,
                    FailureCategory = RebirthNpcWorkFailureCategory.InventoryUnavailable, Detail = detail };
            }
        }
        uint revision = RebirthNpcInventoryTransactionService.GetRevision(state.Context.Assignment.NpcId);
        hauling[state.Context.Assignment.AssignmentId] = new HaulState { Plan = plan, NpcRevision = revision, Quantity = quantity };
        detail = "Hauling plan reserved for " + plan.ItemKey + " x" + quantity +
            (quantity < plan.Quantity ? " (partial)." : ".");
        return RebirthNpcWorkMutationResult.Continue(0.05f, detail);
    }

    protected override RebirthNpcWorkMutationResult TickCore(State state, out string detail)
    {
        HaulState h;
        if (!hauling.TryGetValue(state.Context.Assignment.AssignmentId, out h)) { detail = "Hauling state disappeared."; return null; }
        if (!h.Withdrawn)
        {
            Guid tx = Derive(state.Context.OperationId, 1);
            uint revision;
            string error;
            RebirthNpcExternalTransferResult result = RebirthNpcExternalInventoryTransferCoordinator.Apply(
                new RebirthNpcExternalTransferRequest(tx, state.Context.Assignment.NpcId, h.NpcRevision,
                    h.Plan.SourceEndpointId, h.Plan.ItemKey, h.Quantity,
                    RebirthNpcExternalTransferDirection.ExternalToNpc, state.Context.AuthorityKey), out revision, out error);
            if (result != RebirthNpcExternalTransferResult.Applied && result != RebirthNpcExternalTransferResult.Replayed)
            {
                detail = "Source withdrawal failed: " + result + " " + error;
                return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Retry,
                    FailureCategory = RebirthNpcWorkFailureCategory.InventoryUnavailable, Detail = detail };
            }
            h.Withdrawn = true; h.NpcRevision = revision;
            detail = "Source withdrawal committed; resources are in authoritative NPC transit inventory.";
            return RebirthNpcWorkMutationResult.Continue(0.45f, detail);
        }
        else
        {
            Guid tx = Derive(state.Context.OperationId, 2);
            uint revision; string error;
            RebirthNpcExternalTransferResult result = RebirthNpcExternalInventoryTransferCoordinator.Apply(
                new RebirthNpcExternalTransferRequest(tx, state.Context.Assignment.NpcId, h.NpcRevision,
                    h.Plan.DestinationEndpointId, h.Plan.ItemKey, h.Quantity,
                    RebirthNpcExternalTransferDirection.NpcToExternal, state.Context.AuthorityKey), out revision, out error);
            if (result != RebirthNpcExternalTransferResult.Applied && result != RebirthNpcExternalTransferResult.Replayed)
            {
                detail = "Destination deposit failed; items remain in NPC transit inventory: " + result + " " + error;
                return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Retry,
                    FailureCategory = RebirthNpcWorkFailureCategory.OutputBlocked, Detail = detail };
            }
            h.NpcRevision = revision;
            detail = "Hauling transfer completed atomically through the external inventory coordinator.";
            return new RebirthNpcWorkMutationResult { Disposition = RebirthNpcWorkExecutionDisposition.Completed,
                MutationCommitted = true, ProgressDelta = 0.50f, Detail = detail,
                Produced = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { h.Plan.ItemKey, h.Quantity } } };
        }
    }

    protected override void EndCore(State state, string reason)
    {
        hauling.Remove(state.Context.Assignment.AssignmentId);
    }

    private static Guid Derive(Guid source, byte salt)
    {
        byte[] bytes = source.ToByteArray(); bytes[15] ^= salt; return new Guid(bytes);
    }
}
