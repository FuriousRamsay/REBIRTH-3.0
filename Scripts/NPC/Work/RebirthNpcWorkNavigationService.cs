using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcWorkNavigationDisposition : byte
{
    InRange = 0, Moving = 1, TemporarilyUnavailable = 2, PermanentlyUnavailable = 3
}

public static partial class RebirthNpcWorkNavigationService
{
    private sealed class State
    {
        public ulong AssignmentId;
        public long LastRequestUtcTicks;
        public int ConsecutiveFailures;
        public string LastDetail;
        public RebirthNpcEmbodiedWorkIntent Intent;
        public EntityRebirthNPC Actor;
        public IRebirthNpcMovementAdapter Adapter;
    }

    private const int MaximumStates = 512;
    private static readonly object Sync = new object();
    private static Dictionary<ulong, State> States = new Dictionary<ulong, State>();
    private static RebirthNpcFairRoundRobin<ulong> Order = new RebirthNpcFairRoundRobin<ulong>();
    private static long requests, accepted, alreadySatisfied, temporaryFailures, permanentFailures;

    public static RebirthNpcWorkNavigationDisposition Request(RebirthNpcConcreteWorkContext context,
        out string detail)
    {
        detail="Literature movement original is unavailable.";
        var intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        Interlocked.Increment(ref requests);
        var assignment=context?.Assignment;
        if(!RebirthNpcQualifiedWorkGate.Allows(assignment)||intent==null||
            !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!RebirthNpcQualifiedWorkGate.Allows(intent)||
            intent.PrimaryId!=assignment.AssignmentId||intent.Actor is not EntityRebirthHumanoidNPC npc||
            context.Target==null||context.Target.StableTargetId!=RebirthNpcQualifiedWorkGate.TargetId(assignment)||
            (!MultiLeg(assignment)&&!context.Target.Position.Equals(assignment.TargetPosition))||
            RebirthNpcRetainedControllerCustody.BlocksOwner(assignment.NpcId)||
            RebirthNpcRetainedWorkPackages.BlocksOwner(assignment.NpcId)||
            RebirthNpcRetainedWorkCustody.BlocksOwner(assignment.NpcId)||
            RebirthNpcExecutionLeaseRegistry.TryGetActive(npc.entityId,out _)||
            !RebirthHumanAiPolicy.Ready(npc))return RebirthNpcWorkNavigationDisposition.TemporarilyUnavailable;
        var adapter=RebirthNpcMovementAdapterRegistry.Current;
        if(adapter==null){detail="Shared native movement adapter is unavailable.";return RebirthNpcWorkNavigationDisposition.TemporarilyUnavailable;}
        lock(Sync)
        {
            if(!States.ContainsKey(assignment.AssignmentId)&&States.Count>=MaximumStates)
            {detail="Literature route custody capacity is reached.";return RebirthNpcWorkNavigationDisposition.TemporarilyUnavailable;}
            if(States.TryGetValue(assignment.AssignmentId,out var prior)&&
                (!ReferenceEquals(prior.Intent,intent)||!ReferenceEquals(prior.Actor,npc)||
                 !ReferenceEquals(prior.Adapter,adapter)))return RebirthNpcWorkNavigationDisposition.TemporarilyUnavailable;
        }
        // Multi-leg jobs (P8 water refill) move the goal inside one assignment; the Begin-time
        // target snapshot is stale, so ask the target adapter for the current leg every request.
        var goal=assignment.TargetPosition;
        if(MultiLeg(assignment))
        {
            if(context.Definition==null||!RebirthNpcWorkTargetAdapterRegistry.TryResolve(assignment,context.Definition,
                out _,out var leg,out detail)||leg==null)return RebirthNpcWorkNavigationDisposition.TemporarilyUnavailable;
            goal=leg.Position;
        }
        RebirthNpcMovementRequestResult result;
        try{result=adapter.RequestMove(npc,goal,RebirthNpcWorkRuntime.WorkRadius,out detail);}
        catch(Exception ex){detail="Shared native movement request failed: "+ex.Message;return RebirthNpcWorkNavigationDisposition.TemporarilyUnavailable;}
        if(!RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!RebirthNpcQualifiedWorkGate.Allows(intent))
        {detail="Literature movement original changed after request.";return RebirthNpcWorkNavigationDisposition.TemporarilyUnavailable;}
        if(result==RebirthNpcMovementRequestResult.AlreadySatisfied){Interlocked.Increment(ref alreadySatisfied);return RebirthNpcWorkNavigationDisposition.InRange;}
        if(result==RebirthNpcMovementRequestResult.Accepted)
        {
            var state=GetOrCreate(assignment.AssignmentId);
            state.Intent=intent;state.Actor=npc;state.Adapter=adapter;state.LastRequestUtcTicks=DateTime.UtcNow.Ticks;
            Interlocked.Increment(ref accepted);return RebirthNpcWorkNavigationDisposition.Moving;
        }
        if(result==RebirthNpcMovementRequestResult.Unsupported||result==RebirthNpcMovementRequestResult.Failed)
        {Interlocked.Increment(ref permanentFailures);return RebirthNpcWorkNavigationDisposition.PermanentlyUnavailable;}
        Interlocked.Increment(ref temporaryFailures);
        return RebirthNpcWorkNavigationDisposition.TemporarilyUnavailable;
    }

    public static bool TryResolveNpc(RebirthNpcStableId stableId, out EntityRebirthNPC npc)
    {
        npc = null;
        int entityId;
        if (stableId.IsEmpty || !RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out entityId)) return false;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        npc = world != null ? world.GetEntity(entityId) as EntityRebirthNPC : null;
        return npc != null;
    }

    public static void Complete(ulong assignmentId, string reason)
    {
        // Neither an exact token nor void Stop proves typed original movement cancellation.
        // Retain navigation state; do not stop a possibly combat-owned route.
    }
    internal static bool CompleteLiterature(RebirthNpcConcreteWorkContext context)
        =>StopLiterature(context,true);
    internal static bool CancelLiterature(RebirthNpcConcreteWorkContext context)
        =>StopLiterature(context,false);
    // EAI Reset/End runs outside the owned Tick callback. Stop only the route
    // retained for this exact saved assignment and original actor; a combat or
    // replacement route is never cleared by a stale literature interruption.
    internal static bool TryStopInterruptedLiterature(RebirthNpcEmbodiedWorkIntent intent)
    {
        if(!RebirthNpcQualifiedWorkGate.Allows(intent)||
            intent.Actor is not EntityRebirthHumanoidNPC npc||
            !RebirthNpcWorkAssignmentService.TryGet(intent.PrimaryId,out var assignment)||
            !RebirthNpcQualifiedWorkGate.Allows(assignment)||
            assignment.NpcId!=intent.StableId||
            assignment.AssignmentId!=intent.PrimaryId)return false;
        State state;
        lock(Sync)if(!States.TryGetValue(intent.PrimaryId,out state))return true;
        if(!ReferenceEquals(state.Intent,intent)||!ReferenceEquals(state.Actor,npc)||
            !ReferenceEquals(state.Adapter,RebirthNpcMovementAdapterRegistry.Current)||
            state.Adapter is not RebirthNpcNavigationRuntimeAdapter adapter||
            RebirthNpcExecutionLeaseRegistry.TryGetActive(npc.entityId,out _))return false;
        try{if(!adapter.TryStopOwnedLiteratureRoute(npc,
            "Original literature route interrupted."))return false;}
        catch{return false;}
        lock(Sync)if(States.TryGetValue(intent.PrimaryId,out var live)&&ReferenceEquals(live,state))
        {States.Remove(intent.PrimaryId);Order.Remove(intent.PrimaryId);}
        return true;
    }
    // True for jobs whose travel goal moves inside one assignment (their target adapter owns the leg). The
    // single-entry cache is one immutable object, so a concurrent reader never sees a torn id/revision/value.
    private sealed class MultiLegEntry{internal ulong Id;internal uint Revision;internal bool Value;}
    private static MultiLegEntry multiLegCache;
    private static bool MultiLeg(RebirthNpcWorkAssignment a)
    {
        if(a==null)return false;
        // Parse re-resolves endpoints; the answer is fixed for a given assignment revision.
        var cached=multiLegCache;
        if(cached!=null&&cached.Id==a.AssignmentId&&cached.Revision==a.Revision)return cached.Value;
        bool value=RebirthP8NpcWaterRefillWork.Allows(a)||RebirthNpcB14MiningWork.Allows(a)||
            RebirthNpcStorageOrganizationWork.Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out var storage)&&
            RebirthNpcStorageOrganizationWork.IsFarMode(storage.Mode);
        multiLegCache=new MultiLegEntry{Id=a.AssignmentId,Revision=a.Revision,Value=value};return value;
    }
    // Current leg for multi-leg jobs (fresh from the target adapter), else the saved target.
    private static UnityEngine.Vector3 ArrivalGoal(RebirthNpcConcreteWorkContext context,RebirthNpcWorkAssignment assignment)
    {
        if(MultiLeg(assignment)&&context.Definition!=null&&
            RebirthNpcWorkTargetAdapterRegistry.TryResolve(assignment,context.Definition,out _,out var leg,out _)&&leg!=null)
            return leg.Position;
        return assignment.TargetPosition;
    }
    private static bool StopLiterature(RebirthNpcConcreteWorkContext context,bool requireArrival)
    {
        var assignment=context?.Assignment;var intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        if(!RebirthNpcQualifiedWorkGate.Allows(assignment)||intent==null||
            !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!RebirthNpcQualifiedWorkGate.Allows(intent)||
            intent.PrimaryId!=assignment.AssignmentId||intent.Actor is not EntityRebirthHumanoidNPC npc)return false;
        State state;lock(Sync)if(!States.TryGetValue(assignment.AssignmentId,out state))return true;
        if(!ReferenceEquals(state.Intent,intent)||!ReferenceEquals(state.Actor,npc)||
            !ReferenceEquals(state.Adapter,RebirthNpcMovementAdapterRegistry.Current)||
            RebirthNpcExecutionLeaseRegistry.TryGetActive(npc.entityId,out _)||
            !RebirthHumanAiPolicy.Ready(npc)||
            (requireArrival&&(npc.position-ArrivalGoal(context,assignment)).sqrMagnitude>
                RebirthNpcWorkRuntime.WorkRadiusSquared))return false;
        if(state.Adapter is not RebirthNpcNavigationRuntimeAdapter qualified)return false;
        try{if(!qualified.TryStopOwnedLiteratureRoute(npc,requireArrival?
            "Literature source reached.":"Original literature route cancelled."))return false;}
        catch{return false;}
        lock(Sync)if(States.TryGetValue(assignment.AssignmentId,out var live)&&ReferenceEquals(live,state))
        {States.Remove(assignment.AssignmentId);Order.Remove(assignment.AssignmentId);}
        return true;
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder b = new StringBuilder("[REBIRTH NPC Work Navigation] active=").Append(States.Count)
                .Append(" requests=").Append(requests).Append(" accepted=").Append(accepted)
                .Append(" satisfied=").Append(alreadySatisfied).Append(" temporaryFailures=").Append(temporaryFailures)
                .Append(" permanentFailures=").Append(permanentFailures);
            foreach (KeyValuePair<ulong, State> pair in States)
                b.AppendLine().Append("  assignment=").Append(pair.Key).Append(" failures=")
                    .Append(pair.Value.ConsecutiveFailures).Append(" detail=").Append(pair.Value.LastDetail);
            return b.ToString();
        }
    }

    private static State GetOrCreate(ulong assignmentId)
    {
        lock (Sync)
        {
            State state;
            if (States.TryGetValue(assignmentId, out state)) return state;
            state = new State { AssignmentId = assignmentId };
            States.Add(assignmentId, state);
            Order.Add(assignmentId);
            while (States.Count > MaximumStates && Order.Count > 0)
            {
                ulong oldest;
                if(!Order.TryTake(out oldest))break;
                States.Remove(oldest);
            }
            return state;
        }
    }

    public static void ResetForWorldChange()
    {
        lock(Sync)if(!CanRetireEmptyNavigation)throw new InvalidOperationException("Original navigation actor/adapter custody remains unqualified; retained");
    }

    private static void Record(ulong assignmentId, string detail, bool failure)
    {
        State state = GetOrCreate(assignmentId);
        state.LastDetail = detail ?? string.Empty;
        if (failure) state.ConsecutiveFailures++;
    }
}
