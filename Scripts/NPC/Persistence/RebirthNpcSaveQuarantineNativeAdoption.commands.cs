using System;
using System.Collections.Generic;

public static partial class RebirthNpcCommandGateway
{
    private sealed class CommandNativeCustody
    {
        internal readonly World World;internal readonly Dictionary<ulong,RebirthNpcCommandRecord> Originals,Frozen;
        internal readonly Queue<ulong>[] Queues;internal readonly SortedDictionary<long,Queue<ulong>> Retries;internal readonly Queue<ulong> History;
        internal readonly RebirthNpcNativeActorReentryMap Actors=new RebirthNpcNativeActorReentryMap();
        internal readonly HashSet<ulong> Installed=new HashSet<ulong>();internal World Incoming;internal RebirthNpcNativeTransientAdoption Receipt;
        internal CommandNativeCustody(World world,Dictionary<ulong,RebirthNpcCommandRecord> records,Queue<ulong>[] queues,SortedDictionary<long,Queue<ulong>> retries,Queue<ulong> history)
        {
            World=world;Originals=records;Queues=queues;Retries=retries;History=history;Frozen=new Dictionary<ulong,RebirthNpcCommandRecord>();
            foreach(var pair in records)
            {
                Frozen.Add(pair.Key,CopyNativeCommand(pair.Value,pair.Value.Request));
                if(!IsTerminal(pair.Value.Status))
                {
                    Actors.Add(world,pair.Value.Request.TargetEntityId);
                    if(pair.Value.Request.SubjectEntityId>0&&world.GetEntity(pair.Value.Request.SubjectEntityId) is EntityRebirthNPC)Actors.Add(world,pair.Value.Request.SubjectEntityId);
                }
            }
        }
        internal bool HasPending {get{foreach(var pair in Originals)if(!Installed.Contains(pair.Key)&&!IsTerminal(pair.Value.Status))return true;return false;}}
        internal bool Holds(RebirthNpcStableId id)
        {foreach(var pair in Originals)if(!Installed.Contains(pair.Key)&&!IsTerminal(pair.Value.Status)&&
            (Actors.IsOriginalActor(pair.Value.Request.TargetEntityId,id)||Actors.IsOriginalActor(pair.Value.Request.SubjectEntityId,id)))return true;return false;}
        internal bool Exact()
        {if(Originals.Count!=Frozen.Count)return false;foreach(var pair in Originals)if(!Frozen.TryGetValue(pair.Key,out var copy)||!SameNativeCommand(pair.Value,copy))return false;return true;}
        internal bool Stage(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>StageScope(source.World);
        internal bool StageOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)=>source?.Current()==true&&Originals.Count==0&&StageScope(source.World);
        private bool StageScope(World incoming)
        {
            lock(Sync)
            {
                if(!Exact()||Incoming!=null&&!ReferenceEquals(Incoming,incoming)||commandWorld!=null&&!ReferenceEquals(commandWorld,incoming))return false;
                if(Incoming==null)
                {
                    if(!CanRetireEmptyCommands)return false;Incoming=incoming;commandWorld=incoming;commandNativeCustodies.Add(this);
                    foreach(ulong id in History)if(Originals.TryGetValue(id,out var record)&&IsTerminal(record.Status))
                    {Records.Add(id,CopyNativeCommand(record,record.Request));HistoryOrder.Enqueue(id);Installed.Add(id);}
                }
                var newlyInstalled=new HashSet<ulong>();
                foreach(var pair in Originals)
                {
                    var old=pair.Value;var request=old.Request;
                    if(Installed.Contains(pair.Key)||IsTerminal(old.Status)||old.Status==RebirthNpcCommandStatus.Executing||
                        old.Status!=RebirthNpcCommandStatus.Queued&&old.Status!=RebirthNpcCommandStatus.RetryScheduled||
                        !Actors.TryResolve(request.TargetEntityId,out var target))continue;
                    int subject=request.SubjectEntityId;
                    // Subject-bearing commands require their own actual NPC
                    // reentry. Player/connection authority stays original held;
                    // a numeric player id or a new login cannot replace it.
                    if(subject>0)
                    {if(!Actors.TryResolve(subject,out var actor))continue;subject=actor.entityId;}
                    var remapped=new RebirthNpcCommandRequest(request.CommandId,target.entityId,subject,request.IssuerId,request.Kind,request.TargetPosition,
                        request.HasTargetPosition,request.ExpectedRevision,request.Origin,request.Priority,request.ExpiresUtcTicks);
                    if(!string.IsNullOrEmpty(Validate(target,remapped))||Records.ContainsKey(pair.Key))continue;
                    Records.Add(pair.Key,CopyNativeCommand(old,remapped));Installed.Add(pair.Key);newlyInstalled.Add(pair.Key);pendingCount++;
                }
                // Preserve original token ordering and due times exactly once.
                foreach(var queue in Queues)foreach(ulong id in queue)if(newlyInstalled.Contains(id)&&Records.TryGetValue(id,out var record))PendingByPriority[(int)record.Request.Priority].Enqueue(id);
                foreach(var bucket in Retries)foreach(ulong id in bucket.Value)if(newlyInstalled.Contains(id)&&Records.TryGetValue(id,out var record)&&
                    record.Status==RebirthNpcCommandStatus.RetryScheduled)
                {if(!RetryByDue.TryGetValue(bucket.Key,out var queue))RetryByDue.Add(bucket.Key,queue=new Queue<ulong>());queue.Enqueue(id);}
                return true;
            }
        }
    }
    private static readonly List<CommandNativeCustody> commandNativeCustodies=new List<CommandNativeCustody>();
    internal static bool HasNativeCommandCustody(EntityRebirthNPC npc)
    {
        if(npc?.RebirthRuntimeState==null)return false;
        foreach(var custody in commandNativeCustodies)if(!ReferenceEquals(custody.World,npc.world)&&custody.Holds(npc.RebirthRuntimeState.StableId))return true;
        return false;
    }
    internal static RebirthNpcNativeTransientAdoption CaptureNativeCommandAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||commandWorld!=null&&!ReferenceEquals(commandWorld,scope.World))return null;
            var custody=new CommandNativeCustody(scope.World,Records,PendingByPriority,RetryByDue,HistoryOrder);
            string image=CommandQuarantineImage().OuterXml;var records=Records;var queues=PendingByPriority;var retries=RetryByDue;var history=HistoryOrder;int count=pendingCount;ulong allocator=nextCommandId;
            bool Original(){lock(Sync)return ReferenceEquals(Records,records)&&ReferenceEquals(PendingByPriority,queues)&&ReferenceEquals(RetryByDue,retries)&&
                ReferenceEquals(HistoryOrder,history)&&pendingCount==count&&nextCommandId==allocator&&custody.Actors.Current()&&CommandQuarantineImage().OuterXml==image;}
            bool Available(){lock(Sync)return custody.Exact()&&(custody.Incoming==null?CanRetireEmptyCommands&&commandWorld==null:ReferenceEquals(commandWorld,custody.Incoming));}
            var receipt=new RebirthNpcNativeTransientAdoption("command-gateway",scope,Original,Available,()=>custody.HasPending,custody.Actors.Observe,custody.Stage,image.Length,()=>commandNativeCustodies.Remove(custody),custody.Holds,custody.StageOwnerScope);
            custody.Receipt=receipt;return receipt;
        }
    }
    private static RebirthNpcCommandRecord CopyNativeCommand(RebirthNpcCommandRecord source,RebirthNpcCommandRequest request)=>new RebirthNpcCommandRecord(request)
    {Status=source.Status,Detail=source.Detail,SubmittedUtcTicks=source.SubmittedUtcTicks,UpdatedUtcTicks=source.UpdatedUtcTicks,
        AttemptCount=source.AttemptCount,NextAttemptUtcTicks=source.NextAttemptUtcTicks,LastFailureKind=source.LastFailureKind};
    private static bool SameNativeCommand(RebirthNpcCommandRecord a,RebirthNpcCommandRecord b)=>a!=null&&b!=null&&a.Request.Equals(b.Request)&&
        a.Status==b.Status&&a.Detail==b.Detail&&a.SubmittedUtcTicks==b.SubmittedUtcTicks&&a.UpdatedUtcTicks==b.UpdatedUtcTicks&&
        a.AttemptCount==b.AttemptCount&&a.NextAttemptUtcTicks==b.NextAttemptUtcTicks&&a.LastFailureKind==b.LastFailureKind;
}
