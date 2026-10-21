using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcWorkRuntime
{
    private static World controllerWorld;private static ulong controllerGeneration;
    internal static bool CanRetireEmptyController{get{lock(Sync)return ThreadManager.IsMainThread()&&Sessions.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static bool TryRetireEmptyControllerObservation(){lock(Sync){if(!CanRetireEmptyController)return false;controllerWorld=null;controllerGeneration=0;nextTickClockTicks=0;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyControllerQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!CanRetireEmptyController)return CaptureControllerCustody(scope).Domain;
            var records=Sessions;var world=controllerWorld;ulong generation=controllerGeneration;long timer=nextTickClockTicks;var next=new Dictionary<int,RebirthNpcWorkSession>();
            return RebirthNpcEmptyControllerQuarantine.Create("work-controller-empty",scope,world,generation,timer,
                ()=>{lock(Sync)return ReferenceEquals(Sessions,records)&&CanRetireEmptyController&&ReferenceEquals(controllerWorld,world)&&controllerGeneration==generation&&nextTickClockTicks==timer;},
                ()=>{lock(Sync){Sessions=next;controllerWorld=null;controllerGeneration=0;nextTickClockTicks=0;}},
                ()=>{lock(Sync){Sessions=records;controllerWorld=world;controllerGeneration=generation;nextTickClockTicks=timer;}});
        }
    }
}
public static partial class RebirthNpcWorkExecutorRegistry
{
    private static World resolutionWorld;private static ulong resolutionGeneration;
    private static bool CurrentResolutionScope()
    {
        var world=GameManager.Instance?.World;
        if(!RebirthNpcControllerOriginalScope.CanTick(world))return false;
        lock(Sync)
        {
            if(resolutionWorld!=null&&!ReferenceEquals(resolutionWorld,world))return false;
            if(resolutionWorld==null&&ResolutionCache.Count!=0)return false;
            ulong generation=RebirthNpcRecoveryGeneration.RequireCurrent();
            if(resolutionWorld!=null&&resolutionGeneration!=generation&&ResolutionCache.Count!=0)return false;
            resolutionWorld=world;resolutionGeneration=generation;return true;
        }
    }
    private static bool CanMutateResolutionMetadata()
    {
        if(!ThreadManager.IsMainThread()||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        if(ResolutionCache.Count==0)return true;
        return RebirthNpcControllerOriginalScope.CanTick(resolutionWorld)&&RebirthNpcRecoveryGeneration.TryReadObservedGeneration(resolutionWorld,out ulong generation)&&generation==resolutionGeneration;
    }
    internal static bool CanRetireEmptyResolution{get{lock(Sync)return ThreadManager.IsMainThread()&&ResolutionCache.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static bool TryRetireEmptyResolutionObservation(){lock(Sync){if(!CanRetireEmptyResolution)return false;resolutionWorld=null;resolutionGeneration=0;nextResolutionCachePruneClockTicks=0;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyResolutionQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!CanRetireEmptyResolution)return CapturePackageCustody(scope).Domain;
            var records=ResolutionCache;var world=resolutionWorld;ulong generation=resolutionGeneration;long timer=nextResolutionCachePruneClockTicks;
            long[] counters={staleBindingRejects,resolutionCacheHits,resolutionCacheMisses,resolutionCacheInvalidations,resolutionCacheHealthInvalidations,resolutionCacheExpiredPrunes,resolutionRaces};var next=new Dictionary<int,ResolutionCacheEntry>();
            var doc=ResolutionEmptyImage();
            if(scope==null||(world!=null&&!ReferenceEquals(world,scope.World)))throw new InvalidOperationException("Original resolution binding unavailable");
            return new RebirthNpcSaveQuarantineDomain("work-resolution-empty",doc,
                ()=>{lock(Sync)return ReferenceEquals(ResolutionCache,records)&&CanRetireEmptyResolution&&ReferenceEquals(resolutionWorld,world)&&resolutionGeneration==generation&&ResolutionEmptyImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){ResolutionCache=next;resolutionWorld=null;resolutionGeneration=0;nextResolutionCachePruneClockTicks=0;staleBindingRejects=resolutionCacheHits=resolutionCacheMisses=resolutionCacheInvalidations=resolutionCacheHealthInvalidations=resolutionCacheExpiredPrunes=resolutionRaces=0;}},
                ()=>{lock(Sync){ResolutionCache=records;resolutionWorld=world;resolutionGeneration=generation;nextResolutionCachePruneClockTicks=timer;staleBindingRejects=counters[0];resolutionCacheHits=counters[1];resolutionCacheMisses=counters[2];resolutionCacheInvalidations=counters[3];resolutionCacheHealthInvalidations=counters[4];resolutionCacheExpiredPrunes=counters[5];resolutionRaces=counters[6];}});
        }
    }
    private static XmlDocument ResolutionEmptyImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("workResolution");doc.AppendChild(root);root.SetAttribute("emptyOnly","1");root.SetAttribute("generation",resolutionGeneration.ToString(CultureInfo.InvariantCulture));root.SetAttribute("nextPrune",nextResolutionCachePruneClockTicks.ToString(CultureInfo.InvariantCulture));long[] values={staleBindingRejects,resolutionCacheHits,resolutionCacheMisses,resolutionCacheInvalidations,resolutionCacheHealthInvalidations,resolutionCacheExpiredPrunes,resolutionRaces};for(int i=0;i<values.Length;i++)root.SetAttribute("counter"+i,values[i].ToString(CultureInfo.InvariantCulture));return doc;
    }
}
public static partial class RebirthNpcWorkTargetRegistry
{
    private static World targetWorld;private static ulong targetGeneration;
    private static bool CurrentTargetScope()
    {
        var world=GameManager.Instance?.World;if(!RebirthNpcControllerOriginalScope.CanTick(world))return false;
        lock(Sync)
        {
            if(targetWorld!=null&&!ReferenceEquals(targetWorld,world))return false;
            if(targetWorld==null&&Owners.Count!=0)return false;
            ulong generation=RebirthNpcRecoveryGeneration.RequireCurrent();
            if(targetWorld!=null&&targetGeneration!=generation&&Owners.Count!=0)return false;
            targetWorld=world;targetGeneration=generation;return true;
        }
    }
    internal static bool CanRetireEmptyTargets{get{lock(Sync)return ThreadManager.IsMainThread()&&Owners.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static bool TryRetireEmptyTargetObservation(){lock(Sync){if(!CanRetireEmptyTargets)return false;targetWorld=null;targetGeneration=0;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyTargetQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!CanRetireEmptyTargets)return CapturePackageCustody(scope).Domain;
            if(scope==null||(targetWorld!=null&&!ReferenceEquals(targetWorld,scope.World)))throw new InvalidOperationException("Original native Work target claims remain retained");
            var records=Owners;var world=targetWorld;ulong generation=targetGeneration;long[] counters={acquisitions,renewals,renewalFailures,conflicts,releases,releaseRejects,staleReclaims};var next=new Dictionary<RebirthNpcWorkTargetKey,Ownership>();var doc=TargetEmptyImage();
            return new RebirthNpcSaveQuarantineDomain("work-targets-empty",doc,
                ()=>{lock(Sync)return ReferenceEquals(Owners,records)&&CanRetireEmptyTargets&&ReferenceEquals(targetWorld,world)&&targetGeneration==generation&&TargetEmptyImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Owners=next;targetWorld=null;targetGeneration=0;acquisitions=renewals=renewalFailures=conflicts=releases=releaseRejects=staleReclaims=0;}},
                ()=>{lock(Sync){Owners=records;targetWorld=world;targetGeneration=generation;acquisitions=counters[0];renewals=counters[1];renewalFailures=counters[2];conflicts=counters[3];releases=counters[4];releaseRejects=counters[5];staleReclaims=counters[6];}});
        }
    }
    private static XmlDocument TargetEmptyImage()
    {var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("workTargets");doc.AppendChild(root);root.SetAttribute("emptyOnly","1");root.SetAttribute("generation",targetGeneration.ToString(CultureInfo.InvariantCulture));long[] values={acquisitions,renewals,renewalFailures,conflicts,releases,releaseRejects,staleReclaims};for(int i=0;i<values.Length;i++)root.SetAttribute("counter"+i,values[i].ToString(CultureInfo.InvariantCulture));return doc;}
}
public static partial class RebirthNpcWorkExecutorHealthRegistry
{
    private static World healthWorld;
    private static bool CurrentHealthScope()
    {
        var world=GameManager.Instance?.World;if(!RebirthNpcControllerOriginalScope.CanTick(world))return false;
        lock(Sync){if(healthWorld!=null&&!ReferenceEquals(healthWorld,world))return false;if(healthWorld==null){if(Entries.Count!=0)return false;healthWorld=world;}return true;}
    }
    internal static bool CanRetireEmptyHealth{get{lock(Sync)return ThreadManager.IsMainThread()&&Entries.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static bool TryRetireEmptyHealthObservation(){lock(Sync){if(!CanRetireEmptyHealth)return false;healthWorld=null;return true;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareHealthQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||!RebirthNpcWorkCallbackObservation.IsIdle||scope==null||(healthWorld!=null&&!ReferenceEquals(healthWorld,scope.World))||(healthWorld==null&&Entries.Count!=0))throw new InvalidOperationException("Original Work health scope unavailable");
            var records=Entries;var world=healthWorld;var next=new Dictionary<HealthKey,Health>();var doc=HealthImage();
            return new RebirthNpcSaveQuarantineDomain("work-executor-health",doc,
                ()=>{lock(Sync)return ReferenceEquals(Entries,records)&&ReferenceEquals(healthWorld,world)&&RebirthNpcWorkCallbackObservation.IsIdle&&HealthImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Entries=next;healthWorld=null;}},
                ()=>{lock(Sync){Entries=records;healthWorld=world;}});
        }
    }
    private static XmlDocument HealthImage()
    {
        if(Entries.Count>8192)throw new InvalidOperationException("Work health retention bound exceeded");var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("workHealth");doc.AppendChild(root);root.SetAttribute("generationMeaning","executor-registration");long characters=0;var keys=new List<HealthKey>(Entries.Keys);keys.Sort((a,b)=>{int id=string.CompareOrdinal(a.ExecutorId,b.ExecutorId);return id!=0?id:a.Generation.CompareTo(b.Generation);});
        foreach(var key in keys)
        {
            var value=Entries[key];if(value==null||string.IsNullOrWhiteSpace(key.ExecutorId)||key.Generation<=0)throw new InvalidOperationException("Work health original invalid");var row=doc.CreateElement("health");root.AppendChild(row);
            foreach(var text in new[]{key.ExecutorId,value.LastFaultStage,value.LastFaultDetail}){characters+=text?.Length??0;if((text?.Length??0)>65536||characters>4L*1024*1024)throw new InvalidOperationException("Work health text evidence exceeds bound");}
            row.SetAttribute("executor",key.ExecutorId);row.SetAttribute("registrationGeneration",key.Generation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("faults",value.FaultsInWindow.ToString(CultureInfo.InvariantCulture));row.SetAttribute("window",value.WindowStartedClockTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("until",value.QuarantineUntilClockTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("totalFaults",value.TotalFaults.ToString(CultureInfo.InvariantCulture));row.SetAttribute("successes",value.Successes.ToString(CultureInfo.InvariantCulture));row.SetAttribute("quarantines",value.Quarantines.ToString(CultureInfo.InvariantCulture));row.SetAttribute("recoveries",value.Recoveries.ToString(CultureInfo.InvariantCulture));row.SetAttribute("stage",value.LastFaultStage??string.Empty);row.SetAttribute("stageNull",value.LastFaultStage==null?"1":"0");row.SetAttribute("detail",value.LastFaultDetail??string.Empty);row.SetAttribute("detailNull",value.LastFaultDetail==null?"1":"0");
        }
        return doc;
    }
}
