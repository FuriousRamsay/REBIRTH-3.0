using System;
using System.Collections.Generic;

public sealed partial class RebirthNpcNavigationRuntimeAdapter
{
    private static World navigationWorld;
    private static ulong navigationGeneration;
    private static int callbacks;
    private static bool TryEnterOriginal(EntityRebirthNPC actor)
    {
        var world=GameManager.Instance?.World;
        if(actor?.RebirthRuntimeState!=null&&RebirthNpcRetainedControllerCustody.BlocksOwner(actor.RebirthRuntimeState.StableId))return false;
        if(actor?.RebirthRuntimeState!=null&&RebirthNpcRetainedWorkPackages.BlocksOwner(actor.RebirthRuntimeState.StableId))return false;
        if(!RebirthNpcControllerOriginalScope.CanTick(world)||actor==null||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||!actor.NativeIdentityReadable||actor.RebirthRuntimeState==null||actor.RebirthRuntimeState.StableId.IsEmpty||callbacks>=64)return false;
        lock(Sync)
        {
            if(navigationWorld!=null&&!ReferenceEquals(navigationWorld,world))return false;
            if(navigationWorld==null&&States.Count!=0)return false;
            if(States.TryGetValue(actor.entityId,out var old)&&(!ReferenceEquals(old.Actor,actor)||!ReferenceEquals(old.Runtime,actor.RebirthRuntimeState)||old.StableId!=actor.RebirthRuntimeState.StableId))return false;
            if(!States.ContainsKey(actor.entityId)&&States.Count>=MaximumTrackedActors)return false;
            ulong generation=RebirthNpcRecoveryGeneration.RequireCurrent();
            if(navigationWorld!=null&&navigationGeneration!=generation&&(States.Count!=0||callbacks!=0))return false;
            navigationWorld=world;navigationGeneration=generation;callbacks++;return true;
        }
    }
    private static bool OriginalCurrent(EntityRebirthNPC actor)
    {
        if(!RebirthNpcControllerOriginalScope.CanTick(navigationWorld)||actor==null||!ReferenceEquals(actor.world,navigationWorld)||!ReferenceEquals(navigationWorld.GetEntity(actor.entityId),actor)||!actor.NativeIdentityReadable||actor.RebirthRuntimeState==null)return false;
        var embedded=actor.ObservedNativeStableIdentity;if(!embedded.IsEmpty&&embedded!=actor.RebirthRuntimeState.StableId)return false;
        lock(Sync){if(States.TryGetValue(actor.entityId,out var state)&&(!ReferenceEquals(state.Actor,actor)||!ReferenceEquals(state.Runtime,actor.RebirthRuntimeState)||state.StableId!=actor.RebirthRuntimeState.StableId))return false;}
        return RebirthNpcRecoveryGeneration.TryReadObservedGeneration(navigationWorld,out ulong generation)&&generation==navigationGeneration;
    }
    internal static bool CanRetireEmptyNavigationRuntime{get{lock(Sync)return ThreadManager.IsMainThread()&&callbacks==0&&States.Count==0&&Order.Count==0;}}
    internal static bool TryRetireEmptyNavigationObservation(){lock(Sync){if(!CanRetireEmptyNavigationRuntime)return false;ResetForWorldChange();return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyNavigationRuntimeQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!CanRetireEmptyNavigationRuntime)return CaptureControllerCustody(scope).Domain;
            var records=States;var order=Order;var world=navigationWorld;ulong generation=navigationGeneration;
            long[] counts={requests,accepted,satisfied,rateLimited,progressResets,stuckDetections,recoveryAttempts,terminalFailures,stops,evictions,budgetDeferred};
            var next=new Dictionary<int,State>();var nextOrder=new RebirthNpcFairRoundRobin<int>();
            return RebirthNpcEmptyControllerQuarantine.Create("navigation-runtime-empty",scope,world,generation,0,
                ()=>{lock(Sync)return CanRetireEmptyNavigationRuntime&&ReferenceEquals(States,records)&&ReferenceEquals(Order,order)&&ReferenceEquals(navigationWorld,world)&&navigationGeneration==generation&&SameCounts(counts);},
                ()=>{lock(Sync){States=next;Order=nextOrder;navigationWorld=null;navigationGeneration=0;SetCounts(new long[11]);}},
                ()=>{lock(Sync){States=records;Order=order;navigationWorld=world;navigationGeneration=generation;SetCounts(counts);}});
        }
    }
    private static bool SameCounts(long[] values)
    {long[] current={requests,accepted,satisfied,rateLimited,progressResets,stuckDetections,recoveryAttempts,terminalFailures,stops,evictions,budgetDeferred};for(int i=0;i<current.Length;i++)if(values[i]!=current[i])return false;return true;}
    private static void SetCounts(long[] v)
    {requests=v[0];accepted=v[1];satisfied=v[2];rateLimited=v[3];progressResets=v[4];stuckDetections=v[5];recoveryAttempts=v[6];terminalFailures=v[7];stops=v[8];evictions=v[9];budgetDeferred=v[10];}
}
