using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public static class RebirthNpcSettlementOperations
{
    private static int initialized;
    private static long workGoals, workCommands, workStarts, workCompletions, workFailures, workHeartbeats, workRecoveries, workInterruptions;
    private static long needGoals, abstractHungerRelief, abstractThirstRelief, abstractFatigueRelief;

    public static void Register()
    {
        if (Interlocked.Exchange(ref initialized, 1) != 0) return;
        if(RebirthNpcWorkReleaseGate.Enabled) RebirthNpcDecisionEngine.Register(new ClaimedWorkGoal());
        RebirthNpcDecisionEngine.Register(new NeedGoal("need.drink", RebirthNpcNeedKind.Thirst, 0.72f, 0.75f));
        RebirthNpcDecisionEngine.Register(new NeedGoal("need.eat", RebirthNpcNeedKind.Hunger, 0.68f, 0.65f));
        RebirthNpcDecisionEngine.Register(new NeedGoal("need.rest", RebirthNpcNeedKind.Fatigue, 0.78f, 0.70f));
        if(RebirthNpcWorkReleaseGate.Enabled) RebirthNpcWorkExecutorRegistry.Register(new SettlementWorkExecutor());
    }

    private sealed class ClaimedWorkGoal : IRebirthNpcGoalProvider
    {
        public string GoalId => "settlement.claimed-work";
        public float Score(RebirthNpcDecisionContext context)
        {
            RebirthNpcWorkOrder work;
            if (!RebirthNpcSettlementSimulation.TryGetClaimedWork(context.NpcId, out work)) return 0f;
            Interlocked.Increment(ref workGoals);
            return 1f + Math.Max(-100, Math.Min(100, work.Priority)) * 0.005f;
        }

        public IRebirthNpcTask CreateTask(RebirthNpcDecisionContext context)
        {
            RebirthNpcWorkOrder work;
            if (!RebirthNpcSettlementSimulation.TryGetClaimedWork(context.NpcId, out work)) return null;
            return new RebirthNpcDelegateTask("work:" + work.WorkId.ToString("N"),
                RebirthNpcInterruptClass.Threat, delegate(RebirthNpcDecisionContext c)
                {
                    int entityId;
                    if (!RebirthNpcRuntimeRegistry.TryGetEntityId(c.NpcId, out entityId)) return false;
                    EntityRebirthNPC npc = GameManager.Instance.World.GetEntity(entityId) as EntityRebirthNPC;
                    if (npc == null || npc.RebirthRuntimeState == null) return false;
                    RebirthNpcCommandSubmissionResult result = RebirthNpcCommandGateway.Submit(npc,
                        "settlement:" + work.SettlementId, RebirthNpcCommandKind.Work,
                        work.TargetPosition, true, npc.RebirthRuntimeState.Revision);
                    if (!result.Accepted) { Interlocked.Increment(ref workFailures); return false; }
                    Interlocked.Increment(ref workCommands);
                    return true;
                });
        }
    }

    private sealed class NeedGoal : IRebirthNpcGoalProvider
    {
        private readonly string id;
        private readonly RebirthNpcNeedKind kind;
        private readonly float threshold;
        private readonly float reduction;
        public NeedGoal(string id, RebirthNpcNeedKind kind, float threshold, float reduction)
        { this.id=id; this.kind=kind; this.threshold=threshold; this.reduction=reduction; }
        public string GoalId => id;
        public float Score(RebirthNpcDecisionContext context)
        {
            float value=RebirthNpcSettlementSimulation.GetNeed(context.NpcId,kind);
            return value<threshold?0f:1.5f+value;
        }
        public IRebirthNpcTask CreateTask(RebirthNpcDecisionContext context)
        {
            return new RebirthNpcDelegateTask(id, RebirthNpcInterruptClass.Threat,
                delegate(RebirthNpcDecisionContext c)
                {
                    Interlocked.Increment(ref needGoals);
                    RebirthNpcSettlementSimulation.SatisfyNeed(c.NpcId,kind,reduction);
                    if(kind==RebirthNpcNeedKind.Hunger)Interlocked.Increment(ref abstractHungerRelief);
                    else if(kind==RebirthNpcNeedKind.Thirst)Interlocked.Increment(ref abstractThirstRelief);
                    else if(kind==RebirthNpcNeedKind.Fatigue)Interlocked.Increment(ref abstractFatigueRelief);
                    return true;
                });
        }
    }

    private sealed class SettlementWorkExecutor : IRebirthNpcWorkExecutor, IRebirthNpcValidatedWorkExecutor, IRebirthNpcRecoverableWorkExecutor
    {
        private sealed class Session { public Guid WorkId; public RebirthNpcStableId NpcId; public long StartedAt; public long BaseElapsed; public long CompleteAt; public long LastHeartbeatAt; }
        private static readonly object Sync=new object();
        private static readonly Dictionary<int,Session> Sessions=new Dictionary<int,Session>();
        public string ExecutorId => "rebirth.work.settlement-order";
        public int Priority => 1000;
        public RebirthNpcWorkValidationResult Validate(RebirthNpcWorkContext context)
        {
            if (context.Npc == null || context.Npc.RebirthRuntimeState == null)
                return RebirthNpcWorkValidationResult.Reject(RebirthNpcWorkValidationCode.InvalidNpc, "Settlement executor requires a persistent NPC runtime state.");
            if (context.Lease == null || context.Lease.Order != RebirthNpcOrderState.Work)
                return RebirthNpcWorkValidationResult.Reject(RebirthNpcWorkValidationCode.InvalidLease, "Settlement executor received an invalid Work lease.");
            if (!context.Lease.HasTargetPosition)
                return RebirthNpcWorkValidationResult.Reject(RebirthNpcWorkValidationCode.MissingTarget, "Settlement work has no world target.");
            RebirthNpcWorkOrder work;
            if (!RebirthNpcSettlementSimulation.TryGetClaimedWork(context.Npc.RebirthRuntimeState.StableId, out work))
                return RebirthNpcWorkValidationResult.Reject(RebirthNpcWorkValidationCode.ExecutorRejected, "No claimed settlement work belongs to this NPC.");
            return RebirthNpcWorkValidationResult.Valid("Settlement work ownership validated.");
        }
        public void Recover(RebirthNpcWorkContext context) { Begin(context); }
        public bool CanExecute(EntityRebirthNPC npc, RebirthNpcExecutionLease lease)
        {
            if(npc==null || npc.RebirthRuntimeState==null || lease==null || lease.Order!=RebirthNpcOrderState.Work)return false;
            RebirthNpcWorkOrder work;
            return RebirthNpcSettlementSimulation.TryGetClaimedWork(npc.RebirthRuntimeState.StableId,out work);
        }
        public void Begin(RebirthNpcWorkContext context)
        {
            RebirthNpcStableId id=context.Npc.RebirthRuntimeState.StableId;
            RebirthNpcWorkOrder work;
            if(!RebirthNpcSettlementSimulation.TryGetClaimedWork(id,out work) ||
                !RebirthNpcSettlementSimulation.TryMarkRunning(work.WorkId,id))return;
            long remaining; RebirthNpcSettlementSimulation.TryGetExecutionRemaining(work.WorkId,id,out remaining);
            long now=DateTime.UtcNow.Ticks;
            lock(Sync)Sessions[context.Npc.entityId]=new Session{WorkId=work.WorkId,NpcId=id,StartedAt=now,
                BaseElapsed=Math.Max(0,work.ExecutionElapsedTicks),CompleteAt=now+Math.Max(0,remaining),LastHeartbeatAt=now};
            if(work.ExecutionElapsedTicks>0)Interlocked.Increment(ref workRecoveries);
            Interlocked.Increment(ref workStarts);
        }
        public void Tick(RebirthNpcWorkContext context)
        {
            Session s; lock(Sync)if(!Sessions.TryGetValue(context.Npc.entityId,out s))return;
            long now=DateTime.UtcNow.Ticks;
            if(now-s.LastHeartbeatAt>=TimeSpan.FromSeconds(2).Ticks)
            {
                long elapsed=s.BaseElapsed+Math.Max(0,now-s.StartedAt);
                if(!RebirthNpcSettlementSimulation.TryHeartbeatWork(s.WorkId,s.NpcId,elapsed,"Executor heartbeat."))
                {
                    lock(Sync)Sessions.Remove(context.Npc.entityId);
                    Interlocked.Increment(ref workFailures);
                    RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId,context.ActivityId,context.Lease.LeaseId,context.Lease.AcceptedRevision,RebirthNpcActivityOutcome.ControllerFault,"Settlement work heartbeat was rejected.");
                    return;
                }
                s.LastHeartbeatAt=now; Interlocked.Increment(ref workHeartbeats);
            }
            if(now<s.CompleteAt)return;
            bool ok=RebirthNpcSettlementSimulation.CompleteWork(s.WorkId,s.NpcId,true);
            lock(Sync)Sessions.Remove(context.Npc.entityId);
            if(ok)Interlocked.Increment(ref workCompletions);else Interlocked.Increment(ref workFailures);
            RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId,context.ActivityId,
                context.Lease.LeaseId,context.Lease.AcceptedRevision,
                ok?RebirthNpcActivityOutcome.Succeeded:RebirthNpcActivityOutcome.ControllerFault,
                ok?"Settlement work order completed.":"Settlement work order completion was rejected.");
        }
        public void End(RebirthNpcWorkContext context,string reason)
        {
            Session s; lock(Sync){if(!Sessions.TryGetValue(context.Npc.entityId,out s))return;Sessions.Remove(context.Npc.entityId);}
            long elapsed=s.BaseElapsed+Math.Max(0,DateTime.UtcNow.Ticks-s.StartedAt);
            RebirthNpcSettlementSimulation.TryHeartbeatWork(s.WorkId,s.NpcId,elapsed,reason??"Executor interrupted.");
            Interlocked.Increment(ref workInterruptions);
        }
    }

    public static string GetReport()
    {
        return new StringBuilder("[REBIRTH NPC Settlement Operations]\n")
            .Append("workGoals=").Append(workGoals).Append(" commands=").Append(workCommands)
            .Append(" starts=").Append(workStarts).Append(" completions=").Append(workCompletions)
            .Append(" heartbeats=").Append(workHeartbeats).Append(" recoveries=").Append(workRecoveries)
            .Append(" interruptions=").Append(workInterruptions).Append(" workFailures=").Append(workFailures).Append(" needGoals=").Append(needGoals)
            .Append(" abstractHungerRelief=").Append(abstractHungerRelief).Append(" abstractThirstRelief=").Append(abstractThirstRelief).Append(" abstractFatigueRelief=").Append(abstractFatigueRelief).ToString();
    }
}
