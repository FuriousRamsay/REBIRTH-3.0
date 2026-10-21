using System;
using System.Collections.Generic;

public static partial class RebirthNpcActivityRegistry
{
    private sealed class ActivityNativeCustody
    {
        internal World World,Incoming;internal Dictionary<int,RebirthNpcActivityRecord> Active;
        internal Dictionary<ulong,RebirthNpcActivityRecord> Records;internal Queue<ulong> History;
        internal readonly RebirthNpcNativeActorReentryMap Actors=new RebirthNpcNativeActorReentryMap();
        internal RebirthNpcNativeTransientAdoption Receipt;
        internal bool Pending=>Active.Count!=0;
        internal bool Holds(RebirthNpcStableId id){foreach(var row in Active.Values)if(Actors.IsOriginalActor(row.TargetEntityId,id))return true;return false;}
        internal bool Stage(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>StageScope(source.World);
        internal bool StageOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)=>source?.Current()==true&&Records.Count==0&&StageScope(source.World);
        private bool StageScope(World incoming)
        {
            lock(Sync)
            {
                if(Incoming!=null)return ReferenceEquals(Incoming,incoming)&&ReferenceEquals(activityWorld,incoming);
                if(!CanRetireEmptyActivities||activityWorld!=null)return false;
                activityWorld=Incoming=incoming;
                foreach(ulong id in History)if(Records.TryGetValue(id,out var record)&&!Active.ContainsKey(record.TargetEntityId))
                {ByActivityId.Add(id,record.Clone());HistoryOrder.Enqueue(id);}
                // Running/controller callbacks are original custody. Neither a
                // native NPC read nor a matching lease number recreates them.
                activityNativeCustodies.Add(this);return true;
            }
        }
    }
    private static readonly List<ActivityNativeCustody> activityNativeCustodies=new List<ActivityNativeCustody>();
    internal static bool HasNativeActivityCustody(int entity)
    {
        if(GameManager.Instance?.World?.GetEntity(entity) is not EntityRebirthNPC actor||actor.RebirthRuntimeState==null)return false;
        foreach(var custody in activityNativeCustodies)if(!ReferenceEquals(custody.World,actor.world)&&custody.Holds(actor.RebirthRuntimeState.StableId))return true;
        return false;
    }
    internal static RebirthNpcNativeTransientAdoption CaptureNativeActivityAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||activityWorld!=null&&!ReferenceEquals(activityWorld,scope.World))return null;
            var custody=new ActivityNativeCustody{World=scope.World,Active=ActiveByEntity,Records=ByActivityId,History=HistoryOrder};
            foreach(var activity in ActiveByEntity.Values)custody.Actors.Add(scope.World,activity.TargetEntityId);
            var active=ActiveByEntity;var records=ByActivityId;var history=HistoryOrder;ulong allocator=nextActivityId;long watchdog=nextWatchdogUtcTicks;
            string image=ActivityImage().OuterXml;
            bool Original(){lock(Sync)return ReferenceEquals(ActiveByEntity,active)&&ReferenceEquals(ByActivityId,records)&&ReferenceEquals(HistoryOrder,history)&&
                nextActivityId==allocator&&nextWatchdogUtcTicks==watchdog&&custody.Actors.Current()&&ActivityImage().OuterXml==image;}
            bool Available(){lock(Sync)return custody.Incoming==null?CanRetireEmptyActivities&&activityWorld==null:ReferenceEquals(activityWorld,custody.Incoming);}
            var receipt=new RebirthNpcNativeTransientAdoption("activity-runtime",scope,Original,Available,()=>custody.Pending,custody.Actors.Observe,custody.Stage,image.Length,()=>activityNativeCustodies.Remove(custody),custody.Holds,custody.StageOwnerScope);
            custody.Receipt=receipt;return receipt;
        }
    }
}

public static partial class RebirthNpcActivityRecoveryRegistry
{
    private sealed class RecoveryNativeCustody
    {
        internal World World,Incoming;internal Dictionary<int,RebirthNpcActivityRecoveryTicket> Pending;
        internal Dictionary<ulong,RebirthNpcActivityRecoveryTicket> Records;internal Queue<ulong> History;
        internal Dictionary<ulong,int> Attempts;internal LinkedList<ulong> Order;
        internal readonly HashSet<ulong> Installed=new HashSet<ulong>();internal readonly RebirthNpcNativeActorReentryMap Actors=new RebirthNpcNativeActorReentryMap();
        internal RebirthNpcNativeTransientAdoption Receipt;
        internal bool HasPending {get{foreach(var pair in Pending)if(!Installed.Contains(pair.Value.RecoveryId))return true;return false;}}
        internal bool Holds(RebirthNpcStableId id){foreach(var row in Pending.Values)if(!Installed.Contains(row.RecoveryId)&&Actors.IsOriginalActor(row.TargetEntityId,id))return true;return false;}
        internal bool Stage(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>StageScope(source.World);
        internal bool StageOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)=>source?.Current()==true&&Records.Count==0&&StageScope(source.World);
        private bool StageScope(World incoming)
        {
            lock(Sync)
            {
                if(Incoming!=null&&!ReferenceEquals(Incoming,incoming))return false;
                if(Incoming==null)
                {
                    if(!CanRetireEmptyActivityRecovery||activityRecoveryWorld!=null)return false;activityRecoveryWorld=Incoming=incoming;
                    foreach(ulong id in History)if(Records.TryGetValue(id,out var ticket)&&ticket.Status!=RebirthNpcActivityRecoveryStatus.Pending&&ticket.Status!=RebirthNpcActivityRecoveryStatus.Claimed)
                    {ByRecoveryId.Add(id,ticket.Clone());HistoryOrder.Enqueue(id);Installed.Add(id);}
                    foreach(var pair in Attempts)AttemptsByLease.Add(pair.Key,pair.Value);
                    recoveryNativeCustodies.Add(this);
                }
                foreach(ulong id in Order)
                {
                    if(Installed.Contains(id)||!Records.TryGetValue(id,out var ticket)||ticket.Status!=RebirthNpcActivityRecoveryStatus.Pending||
                        !Actors.TryResolve(ticket.TargetEntityId,out var actor)||RebirthNpcActivityRegistry.HasNativeActivityCustody(actor.entityId)||
                        !RebirthNpcExecutionLeaseRegistry.TryGetActive(actor.entityId,out var lease)||lease.LeaseId!=ticket.LeaseId||lease.Order!=ticket.Order||
                        lease.Status!=RebirthNpcExecutionLeaseStatus.Active||PendingByEntity.ContainsKey(actor.entityId))continue;
                    var copy=ticket.Clone();copy.TargetEntityId=actor.entityId;ByRecoveryId.Add(id,copy);PendingByEntity.Add(actor.entityId,copy);
                    var node=PendingOrder.AddLast(id);PendingNodes.Add(id,node);Installed.Add(id);
                }
                return ReferenceEquals(activityRecoveryWorld,Incoming);
            }
        }
    }
    private static readonly List<RecoveryNativeCustody> recoveryNativeCustodies=new List<RecoveryNativeCustody>();
    internal static bool HasNativeRecoveryCustody(int entity)
    {
        if(GameManager.Instance?.World?.GetEntity(entity) is not EntityRebirthNPC actor||actor.RebirthRuntimeState==null)return false;
        foreach(var custody in recoveryNativeCustodies)if(!ReferenceEquals(custody.World,actor.world)&&custody.Holds(actor.RebirthRuntimeState.StableId))return true;
        return false;
    }
    internal static RebirthNpcNativeTransientAdoption CaptureNativeRecoveryAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||activityRecoveryWorld!=null&&!ReferenceEquals(activityRecoveryWorld,scope.World))return null;
            var custody=new RecoveryNativeCustody{World=scope.World,Pending=PendingByEntity,Records=ByRecoveryId,History=HistoryOrder,Attempts=AttemptsByLease,Order=PendingOrder};
            foreach(var ticket in PendingByEntity.Values)custody.Actors.Add(scope.World,ticket.TargetEntityId);
            var pending=PendingByEntity;var records=ByRecoveryId;var history=HistoryOrder;var attempts=AttemptsByLease;var order=PendingOrder;var nodes=PendingNodes;var cursor=recoveryCursor;ulong allocator=nextRecoveryId;
            string image=ActivityRecoveryImage().OuterXml;
            bool Original(){lock(Sync)return ReferenceEquals(PendingByEntity,pending)&&ReferenceEquals(ByRecoveryId,records)&&ReferenceEquals(HistoryOrder,history)&&
                ReferenceEquals(AttemptsByLease,attempts)&&ReferenceEquals(PendingOrder,order)&&ReferenceEquals(PendingNodes,nodes)&&ReferenceEquals(recoveryCursor,cursor)&&
                nextRecoveryId==allocator&&custody.Actors.Current()&&ActivityRecoveryImage().OuterXml==image;}
            bool Available(){lock(Sync)return custody.Incoming==null?CanRetireEmptyActivityRecovery&&activityRecoveryWorld==null:ReferenceEquals(activityRecoveryWorld,custody.Incoming);}
            var receipt=new RebirthNpcNativeTransientAdoption("activity-recovery",scope,Original,Available,()=>custody.HasPending,custody.Actors.Observe,custody.Stage,image.Length,()=>recoveryNativeCustodies.Remove(custody),custody.Holds,custody.StageOwnerScope);
            custody.Receipt=receipt;return receipt;
        }
    }
}
