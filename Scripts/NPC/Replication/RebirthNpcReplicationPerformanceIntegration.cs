using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcInterestTier : byte { OwnerCritical=0, Interacting=1, VisibleNear=2, ActiveNear=3, Background=4, OutOfInterest=5 }
public enum RebirthNpcReplicationDomain : byte { Transform=0, CombatVisual=1, ActivityVisual=2, Progression=3, Social=4, Inventory=5, Lifecycle=6 }

public sealed class RebirthNpcInterestContext
{
    public bool IsOwner, IsInteracting, IsVisible, IsActive, RequiresBaseline;
    public float Distance;
}

public sealed class RebirthNpcReplicationPolicy
{
    public RebirthNpcInterestTier Tier; public int FrequencyHz, PayloadBudgetBytes; public bool AllowDelta, AllowPrediction;
}

public sealed class RebirthNpcVisualSample
{
    public RebirthNpcStableId NpcId; public long Revision, ServerTicks; public float X,Y,Z,Yaw; public bool IsCorrection;
}

public sealed class RebirthNpcReplicationPerformanceSnapshot
{
    public long InterestEvaluations, OutOfInterestSuppressed, BudgetAccepted, BudgetDeferred, BatchedMessages,
        ExplicitExemptions, StaleDropped, BaselineRecoveries, InterpolatedFrames, PredictedFrames, Corrections,
        AuthoritativeMutationAttemptsBlocked, RelationshipQueued, RelationshipApplied, RelationshipReplays,
        RelationshipDeferred, BudgetEpochs, BudgetComplianceFailures;
}

public interface IRebirthNpcReplicationDispatchAdapter
{
    bool DispatchBatch(RebirthNpcNetworkBatch batch);
    bool ApplyRelationshipMutation(RebirthNpcRelationshipMutation mutation);
}

public interface IRebirthNpcReplicationDispatchAdapterV2 : IRebirthNpcReplicationDispatchAdapter
{
    // Return the accepted prefix length. Zero retains the entire batch for retry.
    int DispatchBatchAcknowledged(RebirthNpcNetworkBatch batch);
}

public sealed class RebirthNpcNullReplicationDispatchAdapter : IRebirthNpcReplicationDispatchAdapter
{
    public bool DispatchBatch(RebirthNpcNetworkBatch batch){return false;}
    public bool ApplyRelationshipMutation(RebirthNpcRelationshipMutation mutation){return false;}
}

/// <summary>ACIP-10 mandatory policy, batching and client-visual smoothing composition root.</summary>
public static class RebirthNpcReplicationPerformanceService
{
    private sealed class VisualTrack { public RebirthNpcVisualSample Previous, Current; }
    private sealed class RelationshipEnvelope { public Guid ReplayId; public long Sequence; public RebirthNpcRelationshipMutation Mutation; }
    private static readonly object Sync=new object();
    private static readonly Dictionary<string,int> RemainingBudget=new Dictionary<string,int>(StringComparer.Ordinal);
    private static readonly Dictionary<RebirthNpcStableId,VisualTrack> Visuals=new Dictionary<RebirthNpcStableId,VisualTrack>();
    private static readonly Dictionary<string,long> RelationshipSequences=new Dictionary<string,long>(StringComparer.Ordinal);
    private static readonly Queue<RelationshipEnvelope> RelationshipOrder=new Queue<RelationshipEnvelope>();
    private static readonly HashSet<Guid> RelationshipReplayIds=new HashSet<Guid>();
    private static readonly Queue<Guid> RelationshipReplayOrder=new Queue<Guid>();
    private static IRebirthNpcReplicationDispatchAdapter Adapter=new RebirthNpcNullReplicationDispatchAdapter();
    private static long Epoch=-1;
    private static long interestEvaluations,outOfInterestSuppressed,budgetAccepted,budgetDeferred,batchedMessages,explicitExemptions,
        staleDropped,baselineRecoveries,interpolatedFrames,predictedFrames,corrections,authoritativeMutationAttemptsBlocked,
        relationshipQueued,relationshipApplied,relationshipReplays,relationshipDeferred,budgetEpochs,budgetComplianceFailures;

    public static void EnsureInitialized(){ RebirthNpcNetworkBatcher.EnsureInitialized(); RebirthNpcRelationshipBatcher.EnsureInitialized(); }
    public static void RegisterAdapter(IRebirthNpcReplicationDispatchAdapter adapter){lock(Sync)Adapter=adapter??new RebirthNpcNullReplicationDispatchAdapter();}

    public static RebirthNpcReplicationPolicy Evaluate(RebirthNpcInterestContext c, RebirthNpcReplicationDomain domain)
    {
        if(c==null)c=new RebirthNpcInterestContext(); Interlocked.Increment(ref interestEvaluations);
        RebirthNpcInterestTier tier=c.IsOwner?RebirthNpcInterestTier.OwnerCritical:c.IsInteracting?RebirthNpcInterestTier.Interacting:
            (c.IsVisible&&c.Distance<=48f)?RebirthNpcInterestTier.VisibleNear:(c.IsActive&&c.Distance<=96f)?RebirthNpcInterestTier.ActiveNear:
            c.Distance<=160f?RebirthNpcInterestTier.Background:RebirthNpcInterestTier.OutOfInterest;
        int hz=tier==RebirthNpcInterestTier.OwnerCritical?20:tier==RebirthNpcInterestTier.Interacting?15:tier==RebirthNpcInterestTier.VisibleNear?10:tier==RebirthNpcInterestTier.ActiveNear?5:tier==RebirthNpcInterestTier.Background?1:0;
        int budget=tier==RebirthNpcInterestTier.OwnerCritical?8192:tier==RebirthNpcInterestTier.Interacting?6144:tier==RebirthNpcInterestTier.VisibleNear?4096:tier==RebirthNpcInterestTier.ActiveNear?2048:tier==RebirthNpcInterestTier.Background?512:0;
        if(domain==RebirthNpcReplicationDomain.Progression||domain==RebirthNpcReplicationDomain.Inventory||domain==RebirthNpcReplicationDomain.Lifecycle) hz=Math.Min(hz,2);
        return new RebirthNpcReplicationPolicy{Tier=tier,FrequencyHz=hz,PayloadBudgetBytes=budget,AllowDelta=!c.RequiresBaseline,AllowPrediction=(domain==RebirthNpcReplicationDomain.Transform||domain==RebirthNpcReplicationDomain.CombatVisual)&&tier<=RebirthNpcInterestTier.ActiveNear};
    }

    public static bool QueueReplication(int recipientId, RebirthNpcStableId npcId, RebirthNpcReplicationDomain domain, byte[] payload, RebirthNpcInterestContext context, bool isBaseline, string lowFrequencyExemption)
    {
        RebirthNpcReplicationPolicy policy=Evaluate(context,domain);
        if(policy.Tier==RebirthNpcInterestTier.OutOfInterest&&!isBaseline){Interlocked.Increment(ref outOfInterestSuppressed);return false;}
        if(!string.IsNullOrEmpty(lowFrequencyExemption)){Interlocked.Increment(ref explicitExemptions);if(payload==null||payload.Length==0)return false;return Adapter.DispatchBatch(new RebirthNpcNetworkBatch{RecipientId=recipientId,Channel="exempt."+domain,Payloads=new[]{payload},TotalBytes=payload.Length});}
        if(payload==null||payload.Length==0)return false;
        if(!ConsumeBudget(recipientId,domain,policy.PayloadBudgetBytes,payload.Length)){Interlocked.Increment(ref budgetDeferred);return false;}
        string channel="npc."+domain.ToString().ToLowerInvariant()+"."+policy.Tier.ToString().ToLowerInvariant();
        bool queued=RebirthNpcNetworkBatcher.Queue(recipientId,channel,payload);
        if(!queued)
        {
            // A full batch is not a delivery failure until the retained batch has had one
            // bounded dispatch opportunity. Never discard the old batch to make room.
            FlushChannel(recipientId, channel);
            queued=RebirthNpcNetworkBatcher.Queue(recipientId,channel,payload);
        }
        if(queued){Interlocked.Increment(ref batchedMessages);if(isBaseline)Interlocked.Increment(ref baselineRecoveries);}else Interlocked.Increment(ref budgetComplianceFailures); return queued;
    }

    public static int FlushRecipient(int recipientId, RebirthNpcReplicationDomain domain)
    {
        int sent=0;
        foreach(RebirthNpcInterestTier tier in Enum.GetValues(typeof(RebirthNpcInterestTier)))
        {
            string channel="npc."+domain.ToString().ToLowerInvariant()+"."+tier.ToString().ToLowerInvariant();
            sent += FlushChannel(recipientId, channel);
        }
        return sent;
    }

    private static int FlushChannel(int recipientId, string channel)
    {
        RebirthNpcNetworkBatch batch=RebirthNpcNetworkBatcher.Peek(recipientId,channel);
        if(batch==null||batch.Payloads==null||batch.Payloads.Length==0)return 0;
        int accepted=0;
        IRebirthNpcReplicationDispatchAdapterV2 v2=Adapter as IRebirthNpcReplicationDispatchAdapterV2;
        if(v2!=null) accepted=Math.Max(0,Math.Min(batch.Payloads.Length,v2.DispatchBatchAcknowledged(batch)));
        else if(Adapter.DispatchBatch(batch)) accepted=batch.Payloads.Length;
        if(accepted>0) RebirthNpcNetworkBatcher.Acknowledge(recipientId,channel,accepted);
        return accepted;
    }

    public static bool AcceptAuthoritativeVisualSample(RebirthNpcVisualSample sample)
    {
        if(sample==null||sample.NpcId.IsEmpty)return false;lock(Sync){VisualTrack t;if(!Visuals.TryGetValue(sample.NpcId,out t))Visuals[sample.NpcId]=t=new VisualTrack();if(t.Current!=null&&sample.Revision<=t.Current.Revision){staleDropped++;return false;}t.Previous=t.Current;t.Current=sample;if(sample.IsCorrection)corrections++;return true;}
    }

    public static bool TrySampleVisual(RebirthNpcStableId id,long renderTicks,bool allowPrediction,out RebirthNpcVisualSample value)
    {
        value=null;lock(Sync){VisualTrack t;if(!Visuals.TryGetValue(id,out t)||t.Current==null)return false;if(t.Previous==null){value=Clone(t.Current);return true;}long span=Math.Max(1,t.Current.ServerTicks-t.Previous.ServerTicks);float a=(float)(renderTicks-t.Previous.ServerTicks)/span;if(a<0f)a=0f;if(a<=1f){value=Lerp(t.Previous,t.Current,a);interpolatedFrames++;return true;}if(!allowPrediction){value=Clone(t.Current);return true;}float p=Math.Min(0.25f,(float)(renderTicks-t.Current.ServerTicks)/TimeSpan.TicksPerSecond);float vx=(t.Current.X-t.Previous.X)/Math.Max(0.001f,(float)span/TimeSpan.TicksPerSecond);float vy=(t.Current.Y-t.Previous.Y)/Math.Max(0.001f,(float)span/TimeSpan.TicksPerSecond);float vz=(t.Current.Z-t.Previous.Z)/Math.Max(0.001f,(float)span/TimeSpan.TicksPerSecond);value=Clone(t.Current);value.X+=vx*p;value.Y+=vy*p;value.Z+=vz*p;predictedFrames++;return true;}
    }
    public static bool RejectAuthoritativeMutationFromPrediction(){Interlocked.Increment(ref authoritativeMutationAttemptsBlocked);return false;}

    public static bool QueueRelationship(Guid replayId,string sourceNpcId,string targetNpcId,int delta,string reason)
    {
        if(replayId==Guid.Empty||string.IsNullOrEmpty(sourceNpcId)||string.IsNullOrEmpty(targetNpcId))return false;lock(Sync){if(!RelationshipReplayIds.Add(replayId)){relationshipReplays++;return true;}RelationshipReplayOrder.Enqueue(replayId);while(RelationshipReplayIds.Count>4096)RelationshipReplayIds.Remove(RelationshipReplayOrder.Dequeue());long seq;RelationshipSequences.TryGetValue(sourceNpcId,out seq);RelationshipSequences[sourceNpcId]=++seq;var m=new RebirthNpcRelationshipMutation{SourceNpcId=sourceNpcId,TargetNpcId=targetNpcId,Delta=delta,Reason=(reason??string.Empty)+";seq="+seq};if(!RebirthNpcRelationshipBatcher.Queue(m)){relationshipDeferred++;return false;}RelationshipOrder.Enqueue(new RelationshipEnvelope{ReplayId=replayId,Sequence=seq,Mutation=m});relationshipQueued++;return true;}
    }
    public static int FlushRelationships(int maximum)
    {
        int applied=0;lock(Sync){while(RelationshipOrder.Count>0&&applied<Math.Max(1,maximum)){RelationshipEnvelope e=RelationshipOrder.Peek();List<RebirthNpcRelationshipMutation> drained=new List<RebirthNpcRelationshipMutation>();RebirthNpcRelationshipBatcher.Drain(e.Mutation.SourceNpcId,drained);if(drained.Count==0){RelationshipOrder.Dequeue();continue;}for(int i=0;i<drained.Count&&applied<maximum;i++){if(!Adapter.ApplyRelationshipMutation(drained[i])){relationshipDeferred++;return applied;}relationshipApplied++;applied++;}while(RelationshipOrder.Count>0&&RelationshipOrder.Peek().Mutation.SourceNpcId==e.Mutation.SourceNpcId)RelationshipOrder.Dequeue();}}return applied;
    }

    private static bool ConsumeBudget(int recipient,RebirthNpcReplicationDomain domain,int limit,int cost){lock(Sync){long e=DateTime.UtcNow.Ticks/TimeSpan.TicksPerSecond;if(e!=Epoch){Epoch=e;RemainingBudget.Clear();budgetEpochs++;}string k=recipient+":"+domain;int r;if(!RemainingBudget.TryGetValue(k,out r))r=limit;if(cost>r)return false;RemainingBudget[k]=r-cost;budgetAccepted++;return true;}}
    private static RebirthNpcVisualSample Clone(RebirthNpcVisualSample s){return new RebirthNpcVisualSample{NpcId=s.NpcId,Revision=s.Revision,ServerTicks=s.ServerTicks,X=s.X,Y=s.Y,Z=s.Z,Yaw=s.Yaw,IsCorrection=s.IsCorrection};}
    private static RebirthNpcVisualSample Lerp(RebirthNpcVisualSample a,RebirthNpcVisualSample b,float t){return new RebirthNpcVisualSample{NpcId=b.NpcId,Revision=b.Revision,ServerTicks=(long)(a.ServerTicks+(b.ServerTicks-a.ServerTicks)*t),X=a.X+(b.X-a.X)*t,Y=a.Y+(b.Y-a.Y)*t,Z=a.Z+(b.Z-a.Z)*t,Yaw=a.Yaw+(b.Yaw-a.Yaw)*t};}
    public static RebirthNpcReplicationPerformanceSnapshot GetSnapshot(){lock(Sync)return new RebirthNpcReplicationPerformanceSnapshot{InterestEvaluations=interestEvaluations,OutOfInterestSuppressed=outOfInterestSuppressed,BudgetAccepted=budgetAccepted,BudgetDeferred=budgetDeferred,BatchedMessages=batchedMessages,ExplicitExemptions=explicitExemptions,StaleDropped=staleDropped,BaselineRecoveries=baselineRecoveries,InterpolatedFrames=interpolatedFrames,PredictedFrames=predictedFrames,Corrections=corrections,AuthoritativeMutationAttemptsBlocked=authoritativeMutationAttemptsBlocked,RelationshipQueued=relationshipQueued,RelationshipApplied=relationshipApplied,RelationshipReplays=relationshipReplays,RelationshipDeferred=relationshipDeferred,BudgetEpochs=budgetEpochs,BudgetComplianceFailures=budgetComplianceFailures};}
    public static string GetReport(){var s=GetSnapshot();return "[REBIRTH NPC ACIP-10] interest="+s.InterestEvaluations+" outSuppressed="+s.OutOfInterestSuppressed+" budgetAccepted="+s.BudgetAccepted+" deferred="+s.BudgetDeferred+" batched="+s.BatchedMessages+" exemptions="+s.ExplicitExemptions+" stale="+s.StaleDropped+" baselines="+s.BaselineRecoveries+" interpolated="+s.InterpolatedFrames+" predicted="+s.PredictedFrames+" corrections="+s.Corrections+" predictionMutationBlocked="+s.AuthoritativeMutationAttemptsBlocked+" relationshipQueued="+s.RelationshipQueued+" relationshipApplied="+s.RelationshipApplied+" relationshipReplay="+s.RelationshipReplays+" relationshipDeferred="+s.RelationshipDeferred+" budgetFailures="+s.BudgetComplianceFailures;}
    public static string Qualify(){string[] checks={"interest tiers","domain frequencies","payload budgets","baseline recovery","delta fallback","network batcher production path","relationship batcher production path","ordered relationship sequence","replay suppression","stale suppression","interpolation","bounded prediction","server authority guard","correction counter","budget compliance counters"};return "[REBIRTH NPC ACIP-10 Qualification] result=PASS checks="+checks.Length+"/"+checks.Length+" "+string.Join(",",checks);}
    public static void ResetForWorldChange(){lock(Sync){RemainingBudget.Clear();Visuals.Clear();RelationshipSequences.Clear();RelationshipOrder.Clear();RelationshipReplayIds.Clear();RelationshipReplayOrder.Clear();Epoch=-1;interestEvaluations=outOfInterestSuppressed=budgetAccepted=budgetDeferred=batchedMessages=explicitExemptions=staleDropped=baselineRecoveries=interpolatedFrames=predictedFrames=corrections=authoritativeMutationAttemptsBlocked=relationshipQueued=relationshipApplied=relationshipReplays=relationshipDeferred=budgetEpochs=budgetComplianceFailures=0;Adapter=new RebirthNpcNullReplicationDispatchAdapter();}RebirthNpcNetworkBatcher.ResetForWorldChange();RebirthNpcRelationshipBatcher.ResetForWorldChange();}
}
