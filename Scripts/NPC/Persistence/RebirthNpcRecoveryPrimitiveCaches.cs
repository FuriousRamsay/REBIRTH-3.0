using System;
using System.Collections.Generic;
using System.IO;

// Five explicitly classified derived caches, NOT the world quarantine bundle.
// Opaque callbacks, leases, pending payloads and relationship replay originals
// remain refusals. Only a positively installed durable recovery may retire them.
internal sealed class RebirthNpcRecoveryPrimitiveCaches
{
    internal sealed class Context
    {
        internal readonly RebirthNpcSaveQuarantineScope Scope;
        internal readonly ulong Previous,Next;
        private readonly bool coldInstalled;
        private readonly int processGeneration;
        internal Context(RebirthNpcSaveQuarantineScope scope,ulong previous,ulong next,bool cold)
        {Scope=scope;Previous=previous;Next=next;coldInstalled=cold;processGeneration=RebirthNpcSaveScope.ObserveCurrent()?.ProcessSaveGeneration??throw new InvalidOperationException("Actual save observation required");}
        private void Match(RebirthNpcAncillaryOriginalScope original)
        {
            if(original!=null&&(!ReferenceEquals(original.World,Scope.World)||original.Directory!=Scope.Directory||original.Fingerprint!=Scope.Fingerprint||original.ProcessGeneration!=processGeneration))throw new InvalidOperationException("Derived cache belongs to another original save");
        }
        internal bool IsCurrent{get{var save=RebirthNpcSaveScope.ObserveCurrent();return save!=null&&save.SaveDirectory==Scope.Directory&&save.Fingerprint==Scope.Fingerprint&&save.ProcessSaveGeneration==processGeneration;}}
        internal void RequireOriginal(RebirthNpcSaveQuarantineScope scope,RebirthNpcAncillaryOriginalScope original)
        {
            if(!ReferenceEquals(scope,Scope))throw new InvalidOperationException("Cache recovery context changed");Match(original);
            if(original!=null&&original.Generation!=Previous)throw new InvalidOperationException("Cache is not the exact previous generation");
        }
        internal Step Capture(RebirthNpcAncillaryOriginalScope original,Func<RebirthNpcSaveQuarantineDomain> old,Func<RebirthNpcSaveQuarantineDomain> current)
        {
            Match(original);bool preserve=original!=null&&original.Generation==Next;
            if(preserve&&!coldInstalled)throw new InvalidOperationException("Unexpected installed-generation cache before publication");
            return new Step(preserve?current():old(),preserve);
        }
    }
    internal sealed class Step
    {
        private readonly RebirthNpcSaveQuarantineDomain domain;
        private readonly bool preserve;private bool retired;private Func<bool> fence;
        private int detachCalls,retirementCommits;
        internal void BindFence(Func<bool> proof){if(fence!=null||proof==null)throw new InvalidOperationException("Exact cache fence already bound or missing");fence=proof;}
        internal bool Retired=>retired;
        internal Step(RebirthNpcSaveQuarantineDomain value,bool preserveCurrent){domain=value;preserve=preserveCurrent;}
        internal bool IsCurrent=>(fence==null||fence())&&(retired||domain.IsCurrent);
        internal System.Xml.XmlElement CopyEvidence(System.Xml.XmlDocument owner)
        {var row=domain.CopyEvidence(owner);row.SetAttribute("cacheRetired",retired?"1":"0");row.SetAttribute("preserveInstalled",preserve?"1":"0");row.SetAttribute("detachCalls",detachCalls.ToString());row.SetAttribute("retirementCommits",retirementCommits.ToString());return row;}
        internal object CaptureStatus()=>new{retired,preserveInstalled=preserve,detachCalls,retirementCommits,fenceCurrent=fence!=null&&fence()};
        internal void Retire()
        {
            if(fence!=null&&!fence())throw new InvalidOperationException("Original cache writer fence no longer owned");
            if(retired)return;
            if(!domain.IsCurrent)throw new InvalidOperationException("Frozen derived cache changed");
            if(preserve){retired=true;retirementCommits++;return;}
            try{detachCalls++;domain.Detach();if(fence!=null&&!fence())throw new InvalidOperationException("Original cache writer fence changed during retirement");retired=true;retirementCommits++;}
            catch{domain.Rollback();throw;}
        }
    }
    private readonly Context context;
    private readonly Guid operation;
    private readonly RebirthNpcPersistenceCoordinator.RecoveryLease lease;
    private readonly Step[] steps;
    private bool completed;
    internal object CaptureStatus()
    {int done=0;var rows=new List<object>();foreach(var step in steps){if(step.Retired)done++;rows.Add(step.CaptureStatus());}return new{operation=operation.ToString("N"),previous=context.Previous,generation=context.Next,steps=steps.Length,retired=done,completed,fenceCurrent=FenceCurrent,stepStates=rows.ToArray()};}
    private bool FenceCurrent=>lease?.IsCurrent==true&&RebirthNpcLiveRecoverySession.OwnsAllWriterFences(context.Scope.Directory,operation);
    internal System.Xml.XmlElement CopyEvidence(System.Xml.XmlDocument owner)
    {var root=owner.CreateElement("primitiveCacheTransition");root.SetAttribute("operation",operation.ToString("N"));root.SetAttribute("previous",context.Previous.ToString(System.Globalization.CultureInfo.InvariantCulture));root.SetAttribute("generation",context.Next.ToString(System.Globalization.CultureInfo.InvariantCulture));root.SetAttribute("completed",completed?"1":"0");foreach(var step in steps)root.AppendChild(step.CopyEvidence(owner));return root;}
    private RebirthNpcRecoveryPrimitiveCaches(Context context,Guid operation,RebirthNpcPersistenceCoordinator.RecoveryLease lease)
    {
        this.context=context;this.operation=operation;this.lease=lease;
        if(!OpaqueIdle())throw new InvalidOperationException("Opaque original cache payload/lease remains held");
        using(var budget=new RebirthNpcSaveQuarantineDomain.EvidenceBudget(64L*1024*1024))
            steps=new[]{RebirthNpcSpatialIndex.PrepareRecoveryCache(context),RebirthNpcPathBudgetCoordinator.PrepareRecoveryCache(context),RebirthNpcSerializationBufferPool.PrepareRecoveryCache(context),RebirthNpcReplicationPerformanceService.PrepareRecoveryCache(context),RebirthNpcEquipmentProjectionReceipts.PrepareRecoveryCache(context)};
        if(lease!=null)foreach(var step in steps)step.BindFence(()=>FenceCurrent);
    }
    private static bool OpaqueIdle()=>RebirthNpcWorkCallbackObservation.IsIdle&&RebirthNpcNetworkBatcher.CanRetireEmptyBatches&&RebirthNpcRelationshipBatcher.CanRetireEmptyRelationships&&RebirthNpcRetentionCoordinator.CanRetireEmptyRetention&&RebirthNpcSerializationBufferPool.CanRetireIdlePool&&RebirthNpcReplicationPerformanceService.CanRetireDerivedProjection;
    internal static bool CanPrepareFresh(World world,out string error)
    {
        error="Derived cache recovery observation unavailable";
        try
        {
            var scope=RebirthNpcSaveQuarantineScope.CaptureCurrent();if(!ReferenceEquals(scope.World,world)||scope.Generation==ulong.MaxValue)return false;
            var plan=new RebirthNpcRecoveryPrimitiveCaches(new Context(scope,scope.Generation,scope.Generation+1,false),Guid.Empty,null);
            if(!plan.Frozen)return false;error=string.Empty;return true;
        }
        catch(Exception ex){error="Derived cache original retained: "+ex.GetType().Name;return false;}
    }
    internal static RebirthNpcRecoveryPrimitiveCaches Capture(RebirthNpcPersistenceCoordinator.RecoveryLease lease,Guid operation,ulong previous,ulong next,bool coldInstalled)
    {
        if(lease?.IsCurrent!=true||operation==Guid.Empty||previous==ulong.MaxValue||next!=previous+1)throw new InvalidOperationException("Exact recovery writer required before cache capture");
        var scope=RebirthNpcSaveQuarantineScope.CaptureCurrent();
        if(scope.Generation!=previous&&scope.Generation!=next)throw new InvalidOperationException("Actual cache generation is outside original recovery");
        return new RebirthNpcRecoveryPrimitiveCaches(new Context(scope,previous,next,coldInstalled),operation,lease);
    }
    internal bool Frozen
    {
        get
        {
            if(!ThreadManager.IsMainThread()||!ReferenceEquals(context.Scope.World,GameManager.Instance?.World)||!RebirthNpcSaveQuarantineScope.SameDirectory(context.Scope.Directory,GameIO.GetSaveGameDir())||!context.IsCurrent||!OpaqueIdle())return false;
            foreach(var step in steps)if(!step.IsCurrent)return false;return true;
        }
    }
    internal void RetireAfterInstalled(RebirthNpcLiveRecoveryGroup expected)
    {
        if(completed)return;
        RequireInstalledFence(expected);
        for(int i=0;i<steps.Length;i++){RequireInstalledFence(expected);steps[i].Retire();RequireInstalledFence(expected);RebirthNpcRecoveryFaults.Checkpoint(operation,expected.Target,"cache-retired-"+i);}
        // Real durable read seeds the hot projection floor; never implicit G0.
        RequireInstalledFence(expected);completed=true;
    }
    private void RequireInstalledFence(RebirthNpcLiveRecoveryGroup expected)
    {
        if(!FenceCurrent||!Frozen||expected==null||expected.Operation!=operation||expected.Generation!=context.Next||expected.PreviousGeneration!=context.Previous||expected.Scope!=context.Scope.Fingerprint||!string.Equals(expected.Directory,context.Scope.Directory,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Exact cache installation scope changed");
        var saved=RebirthNpcLiveRecoveryGroup.Read(expected.Directory);
        if(saved?.Phase!=2||saved.Operation!=operation||saved.Generation!=context.Next||saved.PreviousGeneration!=context.Previous||saved.Scope!=context.Scope.Fingerprint||saved.Target!=expected.Target||saved.Members.Length!=expected.Members.Length||RebirthNpcRecoveryGeneration.RequireCurrent()!=context.Next)throw new InvalidDataException("Durable installed recovery proof required before cache retirement");
        for(int i=0;i<saved.Members.Length;i++)if(saved.Members[i].File!=expected.Members[i].File||saved.Members[i].Hash!=expected.Members[i].Hash||saved.Members[i].BeforeHash!=expected.Members[i].BeforeHash)throw new InvalidDataException("Installed original member identity changed");
        foreach(var member in saved.Members)
        {
            string primary=Path.Combine(saved.Directory,member.File);
            if(!RebirthNpcRecoveryPublications.IsInstalled(primary))throw new InvalidDataException("Installed runtime publication registration unavailable");
            if(!RebirthNpcRecoveryPublications.TryResolveLatest(primary,operation,context.Next,out _,out _,out _,out var error))throw new InvalidDataException("Installed current publication unavailable: "+error);
        }
    }
}
public static partial class RebirthNpcSpatialIndex
{internal static RebirthNpcRecoveryPrimitiveCaches.Step PrepareRecoveryCache(RebirthNpcRecoveryPrimitiveCaches.Context c){lock(Sync)return c.Capture(spatialScope,()=>PrepareSpatialQuarantine(c.Scope,c),()=>PrepareSpatialQuarantine(c.Scope));}}
public static partial class RebirthNpcPathBudgetCoordinator
{internal static RebirthNpcRecoveryPrimitiveCaches.Step PrepareRecoveryCache(RebirthNpcRecoveryPrimitiveCaches.Context c){lock(Sync)return c.Capture(pathScope,()=>PreparePathQuarantine(c.Scope,c),()=>PreparePathQuarantine(c.Scope));}}
public static partial class RebirthNpcSerializationBufferPool
{internal static RebirthNpcRecoveryPrimitiveCaches.Step PrepareRecoveryCache(RebirthNpcRecoveryPrimitiveCaches.Context c){lock(Sync)return c.Capture(poolScope,()=>PreparePoolQuarantine(c.Scope,c),()=>PreparePoolQuarantine(c.Scope));}}
public static partial class RebirthNpcReplicationPerformanceService
{
    // Sequence/replay floors are original duplicate protection, not render cache.
    internal static bool CanRetireDerivedProjection{get{lock(Sync)return ThreadManager.IsMainThread()&&!relationshipFlushActive&&dispatchCallbacks==0&&RelationshipOrder.Count==0&&RelationshipSequences.Count==0&&RelationshipReplayIds.Count==0&&RelationshipReplayOrder.Count==0&&Adapter is RebirthNpcNullReplicationDispatchAdapter;}}
    internal static RebirthNpcRecoveryPrimitiveCaches.Step PrepareRecoveryCache(RebirthNpcRecoveryPrimitiveCaches.Context c)
    {lock(Sync){if(!CanRetireDerivedProjection)throw new InvalidOperationException("Relationship originals remain held");return c.Capture(projectionScope,()=>PreparePerformanceQuarantine(c.Scope,c),()=>PreparePerformanceQuarantine(c.Scope));}}
}
internal static partial class RebirthNpcEquipmentProjectionReceipts
{internal static RebirthNpcRecoveryPrimitiveCaches.Step PrepareRecoveryCache(RebirthNpcRecoveryPrimitiveCaches.Context c)=>c.Capture(receiptScope,()=>PrepareReceiptQuarantine(c.Scope,c),()=>PrepareReceiptQuarantine(c.Scope));}
