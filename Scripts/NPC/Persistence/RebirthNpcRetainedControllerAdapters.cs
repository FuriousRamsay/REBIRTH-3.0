using System;
using System.Collections.Generic;

public static partial class RebirthNpcFollowRuntime
{
    internal static RebirthNpcRetainedControllerCustody.Member CaptureControllerCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTick;
            var next=new Dictionary<int,RebirthNpcFollowSession>();
            return RebirthNpcRetainedControllerCustody.Capture("follow-controller-empty",scope,world,generation,timer,records,
                s=>s.Original,s=>s.LeaseId,s=>s.ActivityId,s=>RebirthNpcRetainedControllerCustody.Fields(s.EntityId,s.TargetEntityId,s.LeaseId,s.ActivityId,s.AcceptedRevision,s.LastDestination),
                s=>new object[]{s.Original},
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTick==timer;},
                ()=>{lock(Sync)return !ReferenceEquals(Sessions,records)&&(world==null||!ReferenceEquals(controllerWorld,world));},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTick=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTick=timer;}});
        }
    }
}

public static partial class RebirthNpcTravelRuntime
{
    internal static RebirthNpcRetainedControllerCustody.Member CaptureControllerCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickUtcTicks;
            var next=new Dictionary<int,RebirthNpcTravelSession>();
            return RebirthNpcRetainedControllerCustody.Capture("travel-controller-empty",scope,world,generation,timer,records,
                s=>s.Original,s=>s.LeaseId,s=>s.ActivityId,s=>RebirthNpcRetainedControllerCustody.Fields(s.EntityId,s.LeaseId,s.ActivityId,s.AcceptedRevision,s.Destination,s.MoveRequested),
                s=>new object[]{s.Original},
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickUtcTicks==timer;},
                ()=>{lock(Sync)return !ReferenceEquals(Sessions,records)&&(world==null||!ReferenceEquals(controllerWorld,world));},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickUtcTicks=timer;}});
        }
    }
}

public static partial class RebirthNpcGuardNavigationRuntime
{
    internal static RebirthNpcRetainedControllerCustody.Member CaptureControllerCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickUtcTicks;
            var next=new Dictionary<int,RebirthNpcGuardNavigationSession>();
            return RebirthNpcRetainedControllerCustody.Capture("guard-controller-empty",scope,world,generation,timer,records,
                s=>s.Original,s=>s.LeaseId,s=>s.ActivityId,s=>RebirthNpcRetainedControllerCustody.Fields(s.EntityId,s.LeaseId,s.ActivityId,s.AcceptedRevision,s.GuardPosition,s.LastRequestedDestination),
                s=>new object[]{s.Original},
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickUtcTicks==timer;},
                ()=>{lock(Sync)return !ReferenceEquals(Sessions,records)&&(world==null||!ReferenceEquals(controllerWorld,world));},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickUtcTicks=timer;}});
        }
    }
}

public static partial class RebirthNpcPatrolRuntime
{
    internal static RebirthNpcRetainedControllerCustody.Member CaptureControllerCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickUtcTicks;
            var next=new Dictionary<int,RebirthNpcPatrolSession>();
            return RebirthNpcRetainedControllerCustody.Capture("patrol-controller-empty",scope,world,generation,timer,records,
                s=>s.Original,s=>s.LeaseId,s=>s.ActivityId,s=>RebirthNpcRetainedControllerCustody.Fields(s.EntityId,s.LeaseId,s.ActivityId,s.AcceptedRevision,s.PointA,s.PointB,s.CurrentPointIndex,s.LastRequestedPointIndex,s.CompletedLegs),
                s=>new object[]{s.Original},
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickUtcTicks==timer;},
                ()=>{lock(Sync)return !ReferenceEquals(Sessions,records)&&(world==null||!ReferenceEquals(controllerWorld,world));},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickUtcTicks=timer;}});
        }
    }
}

public static partial class RebirthNpcStationaryActivityRuntime
{
    internal static RebirthNpcRetainedControllerCustody.Member CaptureControllerCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickUtcTicks;
            var next=new Dictionary<int,RebirthNpcStationaryActivitySession>();
            return RebirthNpcRetainedControllerCustody.Capture("stationary-controller-empty",scope,world,generation,timer,records,
                s=>s.Original,s=>s.LeaseId,s=>s.ActivityId,s=>RebirthNpcRetainedControllerCustody.Fields(s.EntityId,s.LeaseId,s.ActivityId,s.AcceptedRevision,s.Order,s.Anchor,s.AllowedRadiusSquared,s.Detail),
                s=>new object[]{s.Original},
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickUtcTicks==timer;},
                ()=>{lock(Sync)return !ReferenceEquals(Sessions,records)&&(world==null||!ReferenceEquals(controllerWorld,world));},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickUtcTicks=timer;}});
        }
    }
}

public static partial class RebirthNpcWorkRuntime
{
    internal static RebirthNpcRetainedControllerCustody.Member CaptureControllerCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickClockTicks;
            var next=new Dictionary<int,RebirthNpcWorkSession>();
            return RebirthNpcRetainedControllerCustody.Capture("work-controller-empty",scope,world,generation,timer,records,
                s=>s.Original,s=>s.LeaseId,s=>s.ActivityId,s=>RebirthNpcRetainedControllerCustody.Fields(s.EntityId,s.LeaseId,s.ActivityId,s.AcceptedRevision,s.WorkPosition,s.Mode,s.ExecutorId,s.ExecutorGeneration,s.StartedClockTicks,RebirthNpcExecutionLeaseRegistry.RetainedControllerLeaseImage(s.Lease)),
                s=>new object[]{s.Original,s.Npc,s.Executor,s.Lease,s.CallbackSync},
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickClockTicks==timer;},
                ()=>{lock(Sync)return !ReferenceEquals(Sessions,records)&&(world==null||!ReferenceEquals(controllerWorld,world));},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickClockTicks=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickClockTicks=timer;}});
        }
    }
}

public static partial class RebirthNpcExecutionLeaseRegistry
{
    internal static string RetainedControllerLeaseImage(RebirthNpcExecutionLease lease)
    {return lease==null?null:QuarantineLeaseRecord(new System.Xml.XmlDocument(),lease).OuterXml;}
}

