using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcProgressionService
{
    internal static RebirthNpcSaveQuarantineDomain PrepareProgressionQuarantine()
    {
        lock(Sync)
        {
            var records=Records;var replay=ReplayIds;var order=ReplayOrder;var epoch=recoveryEpoch;
            var nextRecords=new Dictionary<RebirthNpcStableId,RebirthNpcProgressionRecord>();var nextReplay=new HashSet<Guid>();var nextOrder=new Queue<Guid>();var nextEpoch=new object();
            var doc=RebirthNpcProgressionPersistenceStore.QuarantineImage();
            return new RebirthNpcSaveQuarantineDomain("progression-service",doc,
                ()=>{lock(Sync)return ReferenceEquals(Records,records)&&ReferenceEquals(ReplayIds,replay)&&ReferenceEquals(ReplayOrder,order)&&ReferenceEquals(recoveryEpoch,epoch)&&RebirthNpcProgressionPersistenceStore.QuarantineImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Records=nextRecords;ReplayIds=nextReplay;ReplayOrder=nextOrder;recoveryEpoch=nextEpoch;}},
                ()=>{lock(Sync){Records=records;ReplayIds=replay;ReplayOrder=order;recoveryEpoch=epoch;}});
        }
    }
}
public static partial class RebirthNpcProgressionPersistenceStore
{
    internal static XmlDocument QuarantineImage()
    {RebirthNpcProgressionService.CaptureRetirement(out var records,out var replay,out _);var doc=SnapshotDocument(records,replay);ReadDocument(doc);return doc;}
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            var image=QuarantineImage();
            if(flushing||!scope.MatchesBinding(boundWorld as World,boundPath,loaded||dirty||loadFailed||image.DocumentElement.ChildNodes.Count!=0))return false;
            bool priorLoaded=loaded,priorDirty=dirty,failed=loadFailed;long version=changeVersion;var world=boundWorld;string path=boundPath,error=lastError;var binding=originalBinding;
            var doc=new XmlDocument();var root=doc.CreateElement("progressionBinding");doc.AppendChild(root);root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("dirty",dirty?"1":"0");root.SetAttribute("loadFailed",failed?"1":"0");root.SetAttribute("path",path??string.Empty);root.SetAttribute("version",version.ToString(CultureInfo.InvariantCulture));root.SetAttribute("error",error??string.Empty);
            var service=RebirthNpcProgressionService.PrepareProgressionQuarantine();
            var store=new RebirthNpcSaveQuarantineDomain("progression-binding",doc,
                ()=>{lock(Sync)return loaded==priorLoaded&&dirty==priorDirty&&loadFailed==failed&&!flushing&&changeVersion==version&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding);},
                ()=>{lock(Sync){loaded=false;dirty=false;loadFailed=false;changeVersion=0;boundWorld=null;boundPath=null;lastError=string.Empty;originalBinding=null;}},
                ()=>{lock(Sync){loaded=priorLoaded;dirty=priorDirty;loadFailed=failed;changeVersion=version;boundWorld=world;boundPath=path;lastError=error;originalBinding=binding;}});
            domains.Add(service);domains.Add(store);return true;
        }
    }
}
public static partial class RebirthNpcAdvancedProgressionService
{
    internal static RebirthNpcSaveQuarantineDomain PrepareAdvancedQuarantine()
    {
        lock(Sync)
        {
            var records=Records;var sessions=Mentorships;var epoch=recoveryEpoch;
            var nextRecords=new Dictionary<RebirthNpcStableId,RebirthNpcAdvancedProgressionRecord>();var nextSessions=new Dictionary<Guid,RebirthNpcMentorshipSession>();var nextEpoch=new object();
            var doc=RebirthNpcAdvancedProgressionPersistenceStore.QuarantineImage();
            return new RebirthNpcSaveQuarantineDomain("advanced-service",doc,
                ()=>{lock(Sync)return ReferenceEquals(Records,records)&&ReferenceEquals(Mentorships,sessions)&&ReferenceEquals(recoveryEpoch,epoch)&&RebirthNpcAdvancedProgressionPersistenceStore.QuarantineImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Records=nextRecords;Mentorships=nextSessions;recoveryEpoch=nextEpoch;}},
                ()=>{lock(Sync){Records=records;Mentorships=sessions;recoveryEpoch=epoch;}});
        }
    }
}
public static partial class RebirthNpcAdvancedProgressionPersistenceStore
{
    internal static XmlDocument QuarantineImage()
    {
        RebirthNpcAdvancedProgressionService.CaptureRetirement(out var records,out var sessions,out _);var persisted=SnapshotDocument(records,sessions);ReadDocument(persisted);
        var doc=new XmlDocument();var root=doc.CreateElement("advancedQuarantine");doc.AppendChild(root);root.AppendChild(doc.ImportNode(persisted.DocumentElement,true));
        var runtime=doc.CreateElement("runtimeSessions");root.AppendChild(runtime);
        foreach(var session in sessions)
        {
            if(session==null||float.IsNaN(session.CurrentRange)||float.IsInfinity(session.CurrentRange)||float.IsNaN(session.MaximumRange)||float.IsInfinity(session.MaximumRange))throw new InvalidOperationException("Nonfinite mentorship custody retained");
            runtime.SetAttribute("nativeSuspended_"+session.SessionId.ToString("N"),session.NativePresenceSuspended?"1":"0");
            var row=doc.CreateElement("session");row.SetAttribute("id",session.SessionId.ToString("N"));row.SetAttribute("validated",session.LastValidatedUtcTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("range",session.CurrentRange.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("mentorPresent",session.MentorPresent?"1":"0");row.SetAttribute("traineePresent",session.TraineePresent?"1":"0");row.SetAttribute("creditedOutcomes",session.CreditedOutcomes.ToString(CultureInfo.InvariantCulture));runtime.AppendChild(row);
        }
        return doc;
    }
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            RebirthNpcAdvancedProgressionService.CaptureRetirement(out var records,out var sessions,out _);
            if(flushing||!scope.MatchesBinding(boundWorld as World,boundPath,loaded||dirty||loadFailed||records.Length!=0||sessions.Length!=0))return false;
            bool priorLoaded=loaded,priorDirty=dirty,failed=loadFailed;long version=changeVersion;var world=boundWorld;string path=boundPath;var binding=originalBinding;
            var doc=new XmlDocument();var root=doc.CreateElement("advancedBinding");doc.AppendChild(root);root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("dirty",dirty?"1":"0");root.SetAttribute("loadFailed",failed?"1":"0");root.SetAttribute("path",path??string.Empty);root.SetAttribute("version",version.ToString(CultureInfo.InvariantCulture));
            var service=RebirthNpcAdvancedProgressionService.PrepareAdvancedQuarantine();
            var store=new RebirthNpcSaveQuarantineDomain("advanced-binding",doc,
                ()=>{lock(Sync)return loaded==priorLoaded&&dirty==priorDirty&&loadFailed==failed&&!flushing&&changeVersion==version&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding);},
                ()=>{lock(Sync){loaded=false;dirty=false;loadFailed=false;changeVersion=0;boundWorld=null;boundPath=null;originalBinding=null;}},
                ()=>{lock(Sync){loaded=priorLoaded;dirty=priorDirty;loadFailed=failed;changeVersion=version;boundWorld=world;boundPath=path;originalBinding=binding;}});
            domains.Add(service);domains.Add(store);return true;
        }
    }
}
