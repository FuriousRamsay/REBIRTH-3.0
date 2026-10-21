using System;
using System.Collections.Generic;
using System.IO;

public static partial class RebirthNpcWorkOutcomePersistenceStore
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var uninitialized=CaptureUninitializedNativeAdoption(scope);
            if(uninitialized!=null)return uninitialized;
            if(!loaded||!scope.MatchesBinding(boundWorld,boundPath,true))return null;
            var world=boundWorld;string path=boundPath;int version=RebirthNpcWorkOutcomeService.PersistenceVersion;
            string image=RecoveryRecordsImage(RebirthNpcWorkOutcomeService.GetOutcomes());
            if(!RebirthNpcWorkOutcomeService.CaptureNativeReplayAdoption(out var replayCurrent,out var replayEmpty,out var installReplay))return null;
            bool Primary()
            {
                try
                {
                    if(!File.Exists(path)||File.Exists(path+".tmp")||RebirthNpcPersistenceFile.IsRecoveryFenced(path))return false;
                    var doc=RebirthNpcRecoveryCurrentBacking.Read(path);
                    return Validate(doc)&&RecoveryRecordsImage(ReadRecords(doc).ToArray())==image;
                }
                catch{return false;}
            }
            bool Current(){lock(Sync)return loaded&&ReferenceEquals(boundWorld,world)&&boundPath==path&&lastSavedVersion==version&&
                RebirthNpcWorkOutcomeService.PersistenceVersion==version&&RecoveryRecordsImage(RebirthNpcWorkOutcomeService.GetOutcomes())==image&&replayCurrent()&&Primary();}
            if(!Current())return null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(!Primary())return false;
                    if(!loaded)
                    {
                        if(boundWorld!=null||boundPath!=null||!replayEmpty())return false;
                        using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))EnsureLoaded();
                    }
                    if(!loaded||!ReferenceEquals(boundWorld,incoming)||boundPath!=path||
                        RecoveryRecordsImage(RebirthNpcWorkOutcomeService.GetOutcomes())!=image||!Primary())return false;
                    // Retained event IDs remain denial-only replay custody;
                    // importing saved outcomes does not emit them again.
                    return installReplay(scope.World,scope.Directory,incoming);
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}

public static partial class RebirthNpcWorkOutcomeService
{
    private sealed class RetainedReplay
    {internal World Original,Incoming;internal string Directory;internal HashSet<Guid> Events;internal HashSet<ulong> Assignments;}
    private static readonly List<RetainedReplay> retainedReplay=new List<RetainedReplay>();
    private static bool BlocksRetainedEvent(Guid eventId,ulong assignment)
    {
        lock(Sync)
        {
            foreach(var replay in retainedReplay)
                if(!ReferenceEquals(replay.Original,GameManager.Instance?.World)&&RebirthNpcSaveQuarantineScope.SameDirectory(replay.Directory,GameIO.GetSaveGameDir())&&
                    (replay.Events.Contains(eventId)||replay.Assignments.Contains(assignment)))return true;
            return false;
        }
    }
    internal static bool CaptureNativeReplayAdoption(out Func<bool> current,out Func<bool> empty,out Func<World,string,World,bool> install)
    {
        lock(Sync)
        {
            current=empty=null;install=null;if(PendingDeliveries.Count!=0||retainedReplay.Count>=8)return false;
            var outcomes=Outcomes;var events=Events;var emitted=Emitted;var order=EmittedOrder;var epoch=recoveryEpoch;
            string image=QuarantineWorkOutcomeImage().OuterXml;
            var ids=new HashSet<Guid>(Emitted);foreach(var e in Events)ids.Add(e.EventId);
            var assignments=new HashSet<ulong>();foreach(var outcome in Outcomes)assignments.Add(outcome.AssignmentId);
            RetainedReplay retained=null;
            current=()=>{lock(Sync)return ReferenceEquals(Outcomes,outcomes)&&ReferenceEquals(Events,events)&&ReferenceEquals(Emitted,emitted)&&
                ReferenceEquals(EmittedOrder,order)&&ReferenceEquals(recoveryEpoch,epoch)&&QuarantineWorkOutcomeImage().OuterXml==image;};
            empty=()=>{lock(Sync)return !ReferenceEquals(Outcomes,outcomes)&&!ReferenceEquals(Events,events)&&!ReferenceEquals(Emitted,emitted)&&CanRetireEmptyBacking;};
            install=(original,directory,incoming)=>
            {
                lock(Sync)
                {
                    if(retained!=null)return ReferenceEquals(retained.Incoming,incoming);
                    if(ReferenceEquals(Outcomes,outcomes)||ReferenceEquals(Events,events)||ReferenceEquals(Emitted,emitted)||
                        PendingDeliveries.Count!=0||retainedReplay.Count>=8)return false;
                    retained=new RetainedReplay{Original=original,Incoming=incoming,Directory=directory,Events=ids,Assignments=assignments};
                    retainedReplay.Add(retained);return true;
                }
            };return true;
        }
    }
}
