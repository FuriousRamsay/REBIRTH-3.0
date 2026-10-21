using System;
using System.Collections.Generic;

public static partial class RebirthNpcPatrolRuntime
{
    private static World controllerWorld;private static ulong controllerGeneration;
    
    internal static bool CanRetireEmptyController{get{lock(Sync)return ThreadManager.IsMainThread()&&Sessions.Count==0;}}
    internal static bool TryRetireEmptyControllerObservation(){lock(Sync){if(!CanRetireEmptyController)return false;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyControllerQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!CanRetireEmptyController)return CaptureControllerCustody(scope).Domain;
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickUtcTicks;var next=new Dictionary<int,RebirthNpcPatrolSession>();
            
            return RebirthNpcEmptyControllerQuarantine.Create("patrol-controller-empty",scope,world,generation,timer,
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&Sessions.Count==0&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickUtcTicks==timer;},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickUtcTicks=timer;}});
        }
    }
}

public static partial class RebirthNpcStationaryActivityRuntime
{
    private static World controllerWorld;private static ulong controllerGeneration;
    
    internal static bool CanRetireEmptyController{get{lock(Sync)return ThreadManager.IsMainThread()&&Sessions.Count==0;}}
    internal static bool TryRetireEmptyControllerObservation(){lock(Sync){if(!CanRetireEmptyController)return false;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyControllerQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!CanRetireEmptyController)return CaptureControllerCustody(scope).Domain;
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickUtcTicks;var next=new Dictionary<int,RebirthNpcStationaryActivitySession>();
            
            return RebirthNpcEmptyControllerQuarantine.Create("stationary-controller-empty",scope,world,generation,timer,
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&Sessions.Count==0&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickUtcTicks==timer;},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickUtcTicks=timer;}});
        }
    }
}

public static partial class RebirthNpcMissionRuntime
{
    private static World controllerWorld;private static ulong controllerGeneration;
    private static Dictionary<ulong,RebirthNpcMissionSession> UncertainEnds=new Dictionary<ulong,RebirthNpcMissionSession>();
    internal static int PendingEndCount{get{lock(Sync)return UncertainEnds.Count;}}
    internal static bool CanRetireEmptyController{get{lock(Sync)return ThreadManager.IsMainThread()&&Sessions.Count==0&&UncertainEnds.Count==0;}}
    internal static bool TryRetireEmptyControllerObservation(){lock(Sync){if(!CanRetireEmptyController)return false;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyControllerQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!CanRetireEmptyController)return CaptureControllerCustody(scope).Domain;
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickUtcTicks;var next=new Dictionary<int,RebirthNpcMissionSession>();
            var ends=UncertainEnds;var nextEnds=new Dictionary<ulong,RebirthNpcMissionSession>();
            return RebirthNpcEmptyControllerQuarantine.Create("mission-controller-empty",scope,world,generation,timer,
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&Sessions.Count==0&&UncertainEnds.Count==0&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickUtcTicks==timer&&ReferenceEquals(UncertainEnds,ends);},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;UncertainEnds=nextEnds;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickUtcTicks=timer;UncertainEnds=ends;}});
        }
    }
}

