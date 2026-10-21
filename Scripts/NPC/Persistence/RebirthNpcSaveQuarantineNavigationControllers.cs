using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

internal static class RebirthNpcEmptyControllerQuarantine
{
    internal static RebirthNpcSaveQuarantineDomain Create(string name,RebirthNpcSaveQuarantineScope scope,World originalWorld,ulong generation,long timer,Func<bool> current,Action detach,Action restore)
    {
        if(!ThreadManager.IsMainThread()||scope==null||(originalWorld!=null&&!ReferenceEquals(originalWorld,scope.World)))throw new InvalidOperationException("Original controller binding unavailable");
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("controllerEmpty");doc.AppendChild(root);root.SetAttribute("emptyOnly","1");root.SetAttribute("generation",generation.ToString(CultureInfo.InvariantCulture));root.SetAttribute("nextTick",timer.ToString(CultureInfo.InvariantCulture));
        return new RebirthNpcSaveQuarantineDomain(name,doc,current,detach,restore);
    }
}
public static partial class RebirthNpcFollowRuntime
{
    private static World controllerWorld;private static ulong controllerGeneration;
    internal static bool CanRetireEmptyController{get{lock(Sync)return ThreadManager.IsMainThread()&&Sessions.Count==0;}}
    internal static bool TryRetireEmptyControllerObservation(){lock(Sync){if(!CanRetireEmptyController)return false;controllerWorld=null;controllerGeneration=0;nextTick=0;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyControllerQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!CanRetireEmptyController)return CaptureControllerCustody(scope).Domain;
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTick;var next=new Dictionary<int,RebirthNpcFollowSession>();
            return RebirthNpcEmptyControllerQuarantine.Create("follow-controller-empty",scope,world,generation,timer,
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&Sessions.Count==0&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTick==timer;},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTick=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTick=timer;}});
        }
    }
}
public static partial class RebirthNpcTravelRuntime
{
    private static World controllerWorld;private static ulong controllerGeneration;
    internal static bool CanRetireEmptyController{get{lock(Sync)return ThreadManager.IsMainThread()&&Sessions.Count==0;}}
    internal static bool TryRetireEmptyControllerObservation(){lock(Sync){if(!CanRetireEmptyController)return false;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyControllerQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!CanRetireEmptyController)return CaptureControllerCustody(scope).Domain;
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickUtcTicks;var next=new Dictionary<int,RebirthNpcTravelSession>();
            return RebirthNpcEmptyControllerQuarantine.Create("travel-controller-empty",scope,world,generation,timer,
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&Sessions.Count==0&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickUtcTicks==timer;},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickUtcTicks=timer;}});
        }
    }
}
public static partial class RebirthNpcGuardNavigationRuntime
{
    private static World controllerWorld;private static ulong controllerGeneration;
    internal static bool CanRetireEmptyController{get{lock(Sync)return ThreadManager.IsMainThread()&&Sessions.Count==0;}}
    internal static bool TryRetireEmptyControllerObservation(){lock(Sync){if(!CanRetireEmptyController)return false;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyControllerQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!CanRetireEmptyController)return CaptureControllerCustody(scope).Domain;
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickUtcTicks;var next=new Dictionary<int,RebirthNpcGuardNavigationSession>();
            return RebirthNpcEmptyControllerQuarantine.Create("guard-controller-empty",scope,world,generation,timer,
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&Sessions.Count==0&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickUtcTicks==timer;},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickUtcTicks=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickUtcTicks=timer;}});
        }
    }
}
