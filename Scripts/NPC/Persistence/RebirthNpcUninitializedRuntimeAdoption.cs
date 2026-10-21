using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

// These captures prove absence of outgoing live obligations privately. They
// still pin/validate existing PRIMARY and use only ordinary incoming loaders.
internal static class RebirthNpcEmptyRuntimeDocument
{
    internal static XmlDocument Create(string name,int format)
    {var d=new XmlDocument{XmlResolver=null};var r=d.CreateElement(name);r.SetAttribute("format",format.ToString(CultureInfo.InvariantCulture));d.AppendChild(r);return d;}
}

public static partial class RebirthNpcAdvancedProgressionPersistenceStore
{
    private static RebirthNpcRuntimeContinuationAdoption CaptureUninitializedNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        if(scope==null||dirty||flushing||loadFailed||!RebirthNpcAdvancedProgressionService.NativeAdoptionEmpty())return null;
        bool wasLoaded=loaded;var world=boundWorld;string oldPath=boundPath;var binding=originalBinding;
        string path=Path.Combine(scope.Directory,"RebirthNpcAdvancedProgression.xml");
        if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(world as World,oldPath,true)))return null;
        if(!wasLoaded&&(world!=null||oldPath!=null||binding!=null))return null;
        var epoch=RebirthNpcAdvancedProgressionService.RecoveryEpoch;
        bool Original(){lock(Sync)return loaded==wasLoaded&&!dirty&&!flushing&&!loadFailed&&ReferenceEquals(boundWorld,world)&&boundPath==oldPath&&ReferenceEquals(originalBinding,binding)&&ReferenceEquals(epoch,RebirthNpcAdvancedProgressionService.RecoveryEpoch)&&RebirthNpcAdvancedProgressionService.NativeAdoptionEmpty();}
        bool Detached(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&originalBinding==null&&!dirty&&!flushing&&!loadFailed&&!ReferenceEquals(epoch,RebirthNpcAdvancedProgressionService.RecoveryEpoch)&&RebirthNpcAdvancedProgressionService.NativeAdoptionEmpty();}
        XmlDocument Snapshot(){RebirthNpcAdvancedProgressionService.CaptureRetirement(out var r,out var s,out _);return SnapshotDocument(r,s);}
        bool Valid(XmlDocument d)
        {
            try
            {
                ReadDocument(d);
                return true;
            }
            catch{return false;}
        }
        var store=RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("advanced",scope,path,Original,Detached,
            w=>loaded&&!dirty&&!loadFailed&&ReferenceEquals(boundWorld,w)&&boundPath==path,Valid,Snapshot,EnsureLoaded);
        string qualifiedImage=null;object qualifiedEpoch=null;
        bool Qualify(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
        {
            lock(Sync)
            {
                // Pinned persistent equality has already succeeded. Suspend
                // historical presence synchronously before returning to a tick.
                RebirthNpcAdvancedProgressionService.SuspendUninitializedNativePresence();
                qualifiedImage=QuarantineImage().OuterXml;qualifiedEpoch=RebirthNpcAdvancedProgressionService.RecoveryEpoch;return true;
            }
        }
        bool Qualified(World incoming){lock(Sync)return loaded&&!loadFailed&&ReferenceEquals(boundWorld,incoming)&&boundPath==path&&
            ReferenceEquals(qualifiedEpoch,RebirthNpcAdvancedProgressionService.RecoveryEpoch)&&QuarantineImage().OuterXml==qualifiedImage;}
        var actual=new Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry>();
        var observedSessions=new HashSet<Guid>();
        void Observe(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
        {
            lock(Sync)
            {
                if(!Qualified(source.World)||!source.Current())return;
                actual[source.StableId]=source;
                RebirthNpcAdvancedProgressionService.CaptureRetirement(out _,out var sessions,out _);
                foreach(var s in sessions)if(s.Active&&!observedSessions.Contains(s.SessionId)&&
                    actual.TryGetValue(s.MentorId,out var mentor)&&actual.TryGetValue(s.TraineeId,out var trainee)&&
                    mentor.Current()&&trainee.Current()&&ReferenceEquals(mentor.World,source.World)&&ReferenceEquals(trainee.World,source.World))
                {
                    RebirthNpcAdvancedProgressionService.UpdateMentorshipPresence(s.SessionId,true,true,
                        UnityEngine.Vector3.Distance(mentor.Actor.position,trainee.Actor.position),DateTime.UtcNow.Ticks);
                    observedSessions.Add(s.SessionId);
                }
                qualifiedImage=QuarantineImage().OuterXml;qualifiedEpoch=RebirthNpcAdvancedProgressionService.RecoveryEpoch;
            }
        }
        return RebirthNpcRuntimeContinuationAdoption.FromEmptySavedStore(scope,store,null,Qualify,Qualified,Observe);
    }
}

public static partial class RebirthNpcAdvancedProgressionService
{
    internal static void SuspendUninitializedNativePresence()
    {
        lock(Sync)foreach(var s in Mentorships.Values)
        {s.NativePresenceSuspended=true;s.MentorPresent=false;s.TraineePresent=false;s.CurrentRange=-1f;s.LastValidatedUtcTicks=0;}
    }
}

public static partial class RebirthNpcWorkCheckpointPersistenceStore
{
    private static RebirthNpcRuntimeContinuationAdoption CaptureUninitializedNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        if(scope==null||Loaded.Count!=0||RebirthNpcWorkExecutionTelemetry.Snapshot().Length!=0)return null;
        bool wasLoaded=loaded,wasReconciled=reconciled;var world=boundWorld;string oldPath=boundPath;var rows=Loaded;
        long revision=RebirthNpcWorkExecutionTelemetry.Revision;string path=Path.Combine(scope.Directory,FileName);
        if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(world,oldPath,true)))return null;
        if(!wasLoaded&&(world!=null||oldPath!=null))return null;
        bool Original(){lock(Sync)return loaded==wasLoaded&&reconciled==wasReconciled&&ReferenceEquals(boundWorld,world)&&boundPath==oldPath&&ReferenceEquals(Loaded,rows)&&rows.Count==0&&RebirthNpcWorkExecutionTelemetry.Revision==revision&&RebirthNpcWorkExecutionTelemetry.Snapshot().Length==0;}
        bool Detached(){lock(Sync)return !loaded&&!reconciled&&boundWorld==null&&boundPath==null&&!ReferenceEquals(Loaded,rows)&&Loaded.Count==0&&RebirthNpcWorkExecutionTelemetry.Snapshot().Length==0;}
        XmlDocument template=null;string expected=null;
        List<RebirthNpcWorkRecoveryCheckpoint> ReadRows(XmlDocument d)
        {
            var result=new List<RebirthNpcWorkRecoveryCheckpoint>();
            foreach(XmlElement e in d.DocumentElement.SelectNodes("checkpoint"))result.Add(new RebirthNpcWorkRecoveryCheckpoint{
                AssignmentId=ulong.Parse(e.GetAttribute("assignment"),CultureInfo.InvariantCulture),ExecutorId=e.GetAttribute("executor"),StableTargetId=e.GetAttribute("target"),
                Phase=(RebirthNpcWorkExecutorPhase)int.Parse(e.GetAttribute("phase"),CultureInfo.InvariantCulture),Progress=float.Parse(e.GetAttribute("progress"),CultureInfo.InvariantCulture),
                RetryCount=int.Parse(e.GetAttribute("retries"),CultureInfo.InvariantCulture),UpdatedUtcTicks=long.Parse(e.GetAttribute("updated"),CultureInfo.InvariantCulture)});
            return result;
        }
        bool Valid(XmlDocument d){if(!Validate(d)||d.DocumentElement.SelectNodes("checkpoint").Count>512)return false;try{var image=RecoveryRowsImage(ReadRows(d));if(template==null){template=d;expected=image;}return true;}catch{return false;}}
        XmlDocument Snapshot()=>template==null?RebirthNpcEmptyRuntimeDocument.Create("rebirthNpcWorkCheckpoints",FormatVersion):
            RecoveryRowsImage(Loaded)==expected?template:RebirthNpcEmptyRuntimeDocument.Create("unmatchedRuntimeState",0);
        var store=RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("work-checkpoint",scope,path,Original,Detached,
            w=>loaded&&!reconciled&&ReferenceEquals(boundWorld,w)&&boundPath==path&&RebirthNpcWorkExecutionTelemetry.Snapshot().Length==0,Valid,Snapshot,EnsureLoaded);
        return RebirthNpcRuntimeContinuationAdoption.FromEmptySavedStore(scope,store);
    }
}

public static partial class RebirthNpcWorkOutcomePersistenceStore
{
    private static RebirthNpcRuntimeContinuationAdoption CaptureUninitializedNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        if(scope==null||!RebirthNpcWorkOutcomeService.CanRetireEmptyBacking)return null;
        bool wasLoaded=loaded;var world=boundWorld;string oldPath=boundPath;int version=RebirthNpcWorkOutcomeService.PersistenceVersion;
        string path=Path.Combine(scope.Directory,FileName);
        if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(world,oldPath,true)))return null;
        if(!wasLoaded&&(world!=null||oldPath!=null))return null;
        if(!RebirthNpcWorkOutcomeService.CaptureNativeReplayAdoption(out var current,out var empty,out var install))return null;
        bool Original(){lock(Sync)return loaded==wasLoaded&&ReferenceEquals(boundWorld,world)&&boundPath==oldPath&&RebirthNpcWorkOutcomeService.PersistenceVersion==version&&RebirthNpcWorkOutcomeService.CanRetireEmptyBacking&&current();}
        bool Detached(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&empty();}
        XmlDocument template=null;string expected=null;
        bool Valid(XmlDocument d){if(!Validate(d)||d.DocumentElement.SelectNodes("outcome").Count>512)return false;var image=RecoveryRecordsImage(ReadRecords(d).ToArray());if(template==null){template=d;expected=image;}return true;}
        XmlDocument Snapshot()=>template==null?RebirthNpcEmptyRuntimeDocument.Create("rebirthNpcWorkOutcomes",FormatVersion):
            RecoveryRecordsImage(RebirthNpcWorkOutcomeService.GetOutcomes())==expected?template:RebirthNpcEmptyRuntimeDocument.Create("unmatchedRuntimeState",0);
        var store=RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("work-outcome",scope,path,Original,Detached,
            w=>loaded&&ReferenceEquals(boundWorld,w)&&boundPath==path,Valid,Snapshot,EnsureLoaded);
        return RebirthNpcRuntimeContinuationAdoption.FromEmptySavedStore(scope,store);
    }
}

public static partial class RebirthNpcExecutionPersistenceStore
{
    private static RebirthNpcRuntimeContinuationAdoption CaptureUninitializedNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        if(scope==null||PendingByStableId.Count!=0||LegacyPendingByEntity.Count!=0||WorkCustody.Count!=0)return null;
        bool wasLoaded=loaded;var oldScope=loadedWorkScope;var pending=PendingByStableId;var legacy=LegacyPendingByEntity;var custody=WorkCustody;
        string path=Path.Combine(scope.Directory,FileName);
        if(wasLoaded&&(File.Exists(path)||oldScope==null||!scope.MatchesBinding(oldScope.NativeWorld as World,oldScope.SaveFilePath,true)))return null;
        if(!wasLoaded&&oldScope!=null)return null;
        bool Original(){lock(Sync)return loaded==wasLoaded&&ReferenceEquals(loadedWorkScope,oldScope)&&ReferenceEquals(PendingByStableId,pending)&&ReferenceEquals(LegacyPendingByEntity,legacy)&&ReferenceEquals(WorkCustody,custody)&&pending.Count==0&&legacy.Count==0&&custody.Count==0;}
        bool Detached(){lock(Sync)return !loaded&&loadedWorkScope==null&&!ReferenceEquals(PendingByStableId,pending)&&!ReferenceEquals(LegacyPendingByEntity,legacy)&&!ReferenceEquals(WorkCustody,custody)&&PendingByStableId.Count==0&&LegacyPendingByEntity.Count==0&&WorkCustody.Count==0;}
        string Image(IEnumerable<RebirthNpcPersistedExecution> records){var images=new List<string>();foreach(var r in records){var d=new XmlDocument();images.Add(QuarantineExecutionRecord(d,r).OuterXml);}images.Sort(StringComparer.Ordinal);return string.Join("\n",images);}
        XmlDocument template=null;string expected=null;
        bool Valid(XmlDocument d){if(!ValidateDocument(d))return false;var image=Image(ReadRecoveryRecords(d));if(template==null){template=d;expected=image;}return true;}
        XmlDocument Snapshot()=>template==null?RebirthNpcEmptyRuntimeDocument.Create("rebirthNpcExecutionState",FormatVersion):
            Image(PendingByStableId.Values.Concat(LegacyPendingByEntity.Values))==expected?template:RebirthNpcEmptyRuntimeDocument.Create("unmatchedRuntimeState",0);
        var store=RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("execution",scope,path,Original,Detached,
            w=>loaded&&loadedWorkScope!=null&&IsCurrentWorkSaveScope(loadedWorkScope)&&ReferenceEquals(loadedWorkScope.NativeWorld,w)&&loadedWorkScope.SaveFilePath==path,
            Valid,Snapshot,EnsureLoaded);
        return RebirthNpcRuntimeContinuationAdoption.FromEmptySavedStore(scope,store);
    }
}
