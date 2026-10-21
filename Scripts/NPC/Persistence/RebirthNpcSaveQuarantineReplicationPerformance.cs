using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcReplicationPerformanceService
{
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {lock(Sync){if(RelationshipOrder.Count!=0||relationshipFlushActive||dispatchCallbacks!=0||Adapter is not RebirthNpcNullReplicationDispatchAdapter)throw new InvalidOperationException("Opaque replication original requires qualified continuation");foreach(var id in Visuals.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);foreach(string source in RelationshipSequences.Keys){if(!RebirthNpcStableId.TryParse(source,out var id))throw new InvalidOperationException("Original relationship sequence identity unknown");RebirthNpcSaveQuarantineIds.Add(ids,id);}}}
    private static RebirthNpcAncillaryOriginalScope projectionScope;
    private static int dispatchCallbacks;
    private static bool EmptyProjection=>RemainingBudget.Count==0&&Visuals.Count==0&&RelationshipSequences.Count==0&&RelationshipOrder.Count==0&&RelationshipReplayIds.Count==0&&RelationshipReplayOrder.Count==0&&Adapter is RebirthNpcNullReplicationDispatchAdapter&&dispatchCallbacks==0&&!relationshipFlushActive;
    private static bool UseProjectionScope(bool observedPolicy=false){if(dispatchCallbacks>=64)return false;lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref projectionScope,EmptyProjection,observedPolicy);}
    private static bool ProjectionScopeCurrent()
    {return projectionScope?.Current()==true;}
    internal static bool CanRetireEmptyProjection{get{lock(Sync)return ThreadManager.IsMainThread()&&EmptyProjection;}}
    private static long[] ProjectionCounts()=>new[]{interestEvaluations,outOfInterestSuppressed,budgetAccepted,budgetDeferred,batchedMessages,explicitExemptions,staleDropped,baselineRecoveries,interpolatedFrames,predictedFrames,corrections,authoritativeMutationAttemptsBlocked,relationshipQueued,relationshipApplied,relationshipReplays,relationshipDeferred,budgetEpochs,budgetComplianceFailures};
    private static void SetProjectionCounts(long[] v)
    {interestEvaluations=v[0];outOfInterestSuppressed=v[1];budgetAccepted=v[2];budgetDeferred=v[3];batchedMessages=v[4];explicitExemptions=v[5];staleDropped=v[6];baselineRecoveries=v[7];interpolatedFrames=v[8];predictedFrames=v[9];corrections=v[10];authoritativeMutationAttemptsBlocked=v[11];relationshipQueued=v[12];relationshipApplied=v[13];relationshipReplays=v[14];relationshipDeferred=v[15];budgetEpochs=v[16];budgetComplianceFailures=v[17];}
    private static void ResetProjectionForWorldChange()
    {
        lock(Sync)
        {
            if(RebirthNpcPersistenceCoordinator.HasActiveRecoveryWriterLease||!CanRetireEmptyProjection||!RebirthNpcNetworkBatcher.CanRetireEmptyBatches||!RebirthNpcRelationshipBatcher.CanRetireEmptyRelationships)throw new InvalidOperationException("Original replication queues/projection remain retained");
            projectionScope=null;Epoch=-1;SetProjectionCounts(new long[18]);
            RebirthNpcNetworkBatcher.ResetForWorldChange();RebirthNpcRelationshipBatcher.ResetForWorldChange();
        }
    }
    private static XmlDocument ProjectionImage()
    {
        if(RemainingBudget.Count>8192||Visuals.Count>8192||RelationshipSequences.Count>8192||RelationshipReplayIds.Count>4096||RelationshipReplayOrder.Count>4096)throw new InvalidOperationException("Original replication primitive evidence exceeds bound");
        var doc=RebirthNpcAncillaryOriginalScope.Image("replicationPerformance",projectionScope,ProjectionCounts());var root=doc.DocumentElement;root.SetAttribute("epoch",Epoch.ToString(CultureInfo.InvariantCulture));long text=0;
        var budgets=new List<string>(RemainingBudget.Keys);budgets.Sort(StringComparer.Ordinal);foreach(string key in budgets){Text(key);var row=doc.CreateElement("budget");row.SetAttribute("key",key);row.SetAttribute("remaining",RemainingBudget[key].ToString(CultureInfo.InvariantCulture));root.AppendChild(row);}
        var visuals=new List<RebirthNpcStableId>(Visuals.Keys);visuals.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));foreach(var id in visuals){var track=Visuals[id];if(id.IsEmpty||track==null)throw new InvalidOperationException("Original visual track invalid");var row=doc.CreateElement("visual");row.SetAttribute("id",id.ToString());Sample(row,"previous",track.Previous,id);Sample(row,"current",track.Current,id);root.AppendChild(row);}
        var sequences=new List<string>(RelationshipSequences.Keys);sequences.Sort(StringComparer.Ordinal);foreach(string key in sequences){Text(key);var row=doc.CreateElement("sequence");row.SetAttribute("source",key);row.SetAttribute("value",RelationshipSequences[key].ToString(CultureInfo.InvariantCulture));root.AppendChild(row);}
        var seen=new HashSet<Guid>();foreach(Guid id in RelationshipReplayOrder){if(id==Guid.Empty||!seen.Add(id)||!RelationshipReplayIds.Contains(id))throw new InvalidOperationException("Original relationship replay order conflict");var row=doc.CreateElement("replay");row.SetAttribute("id",id.ToString("N"));root.AppendChild(row);}if(seen.Count!=RelationshipReplayIds.Count)throw new InvalidOperationException("Original relationship replay membership incomplete");return doc;
        void Text(string value){if(value==null||value.Length>65536||(text+=value.Length)>4L*1024*1024)throw new InvalidOperationException("Original replication text evidence exceeds bound");}
        void Sample(XmlElement parent,string name,RebirthNpcVisualSample value,RebirthNpcStableId id)
        {
            var row=doc.CreateElement(name);parent.AppendChild(row);if(value==null){row.SetAttribute("null","1");return;}if(value.NpcId!=id||!Finite(value.X)||!Finite(value.Y)||!Finite(value.Z)||!Finite(value.Yaw))throw new InvalidOperationException("Original visual sample invalid");row.SetAttribute("revision",value.Revision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("ticks",value.ServerTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("x",value.X.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("y",value.Y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("z",value.Z.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("yaw",value.Yaw.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("correction",value.IsCorrection?"1":"0");
        }
        bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
    internal static RebirthNpcSaveQuarantineDomain PreparePerformanceQuarantine(RebirthNpcSaveQuarantineScope scope,RebirthNpcRecoveryPrimitiveCaches.Context transition=null)
    {
        lock(Sync)
        {
            if(transition==null)RebirthNpcAncillaryOriginalScope.RequireScope(scope,projectionScope);else transition.RequireOriginal(scope,projectionScope);if((projectionScope==null&&!EmptyProjection)||relationshipFlushActive||dispatchCallbacks!=0||RelationshipOrder.Count!=0||Adapter is not RebirthNpcNullReplicationDispatchAdapter)throw new InvalidOperationException("Opaque original dispatch/relationship continuation remains retained");
            var budget=RemainingBudget;var visuals=Visuals;var sequences=RelationshipSequences;var order=RelationshipOrder;var replay=RelationshipReplayIds;var replayOrder=RelationshipReplayOrder;var adapter=Adapter;var original=projectionScope;long epoch=Epoch;var counts=ProjectionCounts();var doc=ProjectionImage();
            var nextBudget=new Dictionary<string,int>(StringComparer.Ordinal);var nextVisuals=new Dictionary<RebirthNpcStableId,VisualTrack>();var nextSequences=new Dictionary<string,long>(StringComparer.Ordinal);var nextOrder=new Queue<RelationshipEnvelope>();var nextReplay=new HashSet<Guid>();var nextReplayOrder=new Queue<Guid>();var nextAdapter=new RebirthNpcNullReplicationDispatchAdapter();
            return new RebirthNpcSaveQuarantineDomain("replication-performance",doc,
                ()=>{lock(Sync)return !relationshipFlushActive&&dispatchCallbacks==0&&RelationshipOrder.Count==0&&ReferenceEquals(RemainingBudget,budget)&&ReferenceEquals(Visuals,visuals)&&ReferenceEquals(RelationshipSequences,sequences)&&ReferenceEquals(RelationshipOrder,order)&&ReferenceEquals(RelationshipReplayIds,replay)&&ReferenceEquals(RelationshipReplayOrder,replayOrder)&&ReferenceEquals(Adapter,adapter)&&ReferenceEquals(projectionScope,original)&&ProjectionImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){RemainingBudget=nextBudget;Visuals=nextVisuals;RelationshipSequences=nextSequences;RelationshipOrder=nextOrder;RelationshipReplayIds=nextReplay;RelationshipReplayOrder=nextReplayOrder;Adapter=nextAdapter;projectionScope=null;Epoch=-1;SetProjectionCounts(new long[18]);}},
                ()=>{lock(Sync){RemainingBudget=budget;Visuals=visuals;RelationshipSequences=sequences;RelationshipOrder=order;RelationshipReplayIds=replay;RelationshipReplayOrder=replayOrder;Adapter=adapter;projectionScope=original;Epoch=epoch;SetProjectionCounts(counts);}});
        }
    }
}
