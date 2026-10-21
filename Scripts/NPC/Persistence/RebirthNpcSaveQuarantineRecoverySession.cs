using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

internal sealed partial class RebirthNpcLiveRecoverySession
{
    internal XmlDocument CaptureQuarantineEvidence(World originalWorld,string originalDirectory)
    {
        if(disposed||!ReferenceEquals(world,originalWorld)||group==null||!string.Equals(group.Directory,originalDirectory,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Recovery session original scope unresolved");
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("recoverySessionQuarantine");doc.AppendChild(root);root.SetAttribute("operation",Operation.ToString("N"));root.SetAttribute("target",Target??string.Empty);root.SetAttribute("generation",Generation.ToString(CultureInfo.InvariantCulture));root.SetAttribute("scope",group.Scope??string.Empty);root.SetAttribute("directory",group.Directory??string.Empty);root.SetAttribute("phase",group.Phase.ToString(CultureInfo.InvariantCulture));root.SetAttribute("previousGeneration",group.PreviousGeneration.ToString(CultureInfo.InvariantCulture));root.SetAttribute("closingProjection",group.ClosingProjection??string.Empty);
        root.SetAttribute("mainCommitted",mainCommitted?"1":"0");root.SetAttribute("executionCommitted",executionCommitted?"1":"0");root.SetAttribute("runtimeCommitted",runtimeCommitted?"1":"0");root.SetAttribute("installed",installed?"1":"0");root.SetAttribute("coldInstalled",coldInstalled?"1":"0");root.SetAttribute("closing",closing?"1":"0");root.SetAttribute("completed",completed?"1":"0");root.SetAttribute("writerFences",fences.Count.ToString(CultureInfo.InvariantCulture));root.SetAttribute("coordinatorLease",coordinator!=null?"1":"0");
        if(primitiveCaches!=null)root.AppendChild(primitiveCaches.CopyEvidence(doc));
        var members=doc.CreateElement("members");root.AppendChild(members);foreach(var member in group.Members){var row=doc.CreateElement("member");members.AppendChild(row);row.SetAttribute("file",member.File??string.Empty);row.SetAttribute("before",member.BeforeHash??string.Empty);row.SetAttribute("candidate",member.Hash??string.Empty);row.SetAttribute("terminal",member.TerminalHash??string.Empty);row.SetAttribute("sequence",member.TerminalSequence.ToString(CultureInfo.InvariantCulture));}
        var originals=doc.CreateElement("intents");root.AppendChild(originals);foreach(var intent in intents)
        {if(intent==null||intent.Operation!=Operation||intent.Scope!=group.Scope||!string.Equals(intent.Directory,group.Directory,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Recovery member intent lineage changed");var row=doc.CreateElement("intent");originals.AppendChild(row);row.SetAttribute("file",intent.File??string.Empty);row.SetAttribute("before",intent.BeforeHash??string.Empty);row.SetAttribute("candidate",intent.CandidateHash??string.Empty);row.SetAttribute("terminal",intent.TerminalHash??string.Empty);row.SetAttribute("sequence",intent.TerminalSequence.ToString(CultureInfo.InvariantCulture));row.SetAttribute("phase",intent.Phase.ToString(CultureInfo.InvariantCulture));}
        var related=doc.CreateElement("relatedCommitted");root.AppendChild(related);var names=new List<string>(relatedCommitted);names.Sort(StringComparer.Ordinal);foreach(string name in names){var row=doc.CreateElement("file");row.SetAttribute("name",name);related.AppendChild(row);}
        if(candidate!=null)
        {
            var image=doc.CreateElement("preparedCandidate");root.AppendChild(image);if(candidate.Publication?.DocumentElement==null)throw new InvalidOperationException("Recovery candidate publication missing");image.AppendChild(doc.ImportNode(candidate.Publication.DocumentElement,true));
            foreach(var prepared in candidate.Related){var row=doc.CreateElement("related");image.AppendChild(row);row.SetAttribute("file",prepared.FileName);var publication=prepared.PublicationDocument;if(publication?.DocumentElement!=null)row.AppendChild(doc.ImportNode(publication.DocumentElement,true));}
        }
        return doc;
    }
}
internal static partial class RebirthNpcLiveRecoveryRuntime
{
    internal static RebirthNpcSaveQuarantineDomain PrepareRecoveryDriverQuarantine(World originalWorld,string originalDirectory)
    {
        if(!ThreadManager.IsMainThread()||pumping||originalWorld==null||(context!=null&&(!ReferenceEquals(context,originalWorld)||!string.Equals(directory,originalDirectory,StringComparison.OrdinalIgnoreCase))))throw new InvalidOperationException("Recovery driver original scope unavailable");
        var session=current;var priorContext=context;string save=directory,detail=lastDetail;var ready=readyWorld;double retryAt=retry;bool loaded=loadedAfterRecovery;Guid completed=lastCompleted;string target=lastCompletedTarget;ulong generation=lastCompletedGeneration;
        var beforeDeferred=deferred;var beforeProjected=deferredProjected;var nextDeferred=new Dictionary<EntityRebirthNPC,World>();var nextProjected=new HashSet<EntityRebirthNPC>();
        // Native B factory callbacks may already be waiting. They are not old A
        // custody and remain pending through the atomic pointer swap.
        foreach(var pair in beforeDeferred)if(!ReferenceEquals(pair.Value,originalWorld)){if(pair.Value==null||pair.Key==null||!ReferenceEquals(pair.Key.world,pair.Value)||!ReferenceEquals(pair.Value,GameManager.Instance?.World))throw new InvalidOperationException("Deferred actor belongs to an unknown third native scope");nextDeferred.Add(pair.Key,pair.Value);if(beforeProjected.Contains(pair.Key))nextProjected.Add(pair.Key);}
        var doc=QuarantineDriverImage(originalWorld,originalDirectory);
        return new RebirthNpcSaveQuarantineDomain("recovery-driver",doc,
            ()=>ThreadManager.IsMainThread()&&!pumping&&ReferenceEquals(current,session)&&ReferenceEquals(context,priorContext)&&directory==save&&ReferenceEquals(readyWorld,ready)&&retry==retryAt&&loadedAfterRecovery==loaded&&lastCompleted==completed&&lastCompletedTarget==target&&lastCompletedGeneration==generation&&ReferenceEquals(deferred,beforeDeferred)&&ReferenceEquals(deferredProjected,beforeProjected)&&QuarantineDriverImage(originalWorld,originalDirectory).OuterXml==doc.OuterXml,
            ()=>{current=null;context=null;directory=null;retry=0;loadedAfterRecovery=false;lastCompleted=Guid.Empty;lastCompletedTarget=null;lastCompletedGeneration=0;deferred=nextDeferred;deferredProjected=nextProjected;lastDetail="Original recovery driver retained in typed save quarantine";},
            ()=>{current=session;context=priorContext;directory=save;readyWorld=ready;retry=retryAt;loadedAfterRecovery=loaded;lastCompleted=completed;lastCompletedTarget=target;lastCompletedGeneration=generation;deferred=beforeDeferred;deferredProjected=beforeProjected;lastDetail=detail;});
    }
    private static XmlDocument QuarantineDriverImage(World originalWorld,string originalDirectory)
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("recoveryDriverQuarantine");doc.AppendChild(root);root.SetAttribute("directory",directory??string.Empty);root.SetAttribute("retry",retry.ToString("R",CultureInfo.InvariantCulture));root.SetAttribute("loadedAfterRecovery",loadedAfterRecovery?"1":"0");root.SetAttribute("lastCompleted",lastCompleted.ToString("N"));root.SetAttribute("lastTarget",lastCompletedTarget??string.Empty);root.SetAttribute("lastGeneration",lastCompletedGeneration.ToString(CultureInfo.InvariantCulture));
        if(current!=null)root.AppendChild(doc.ImportNode(current.CaptureQuarantineEvidence(originalWorld,originalDirectory).DocumentElement,true));
        var rows=new List<XmlElement>();foreach(var pair in deferred)
        {
            if(!ReferenceEquals(pair.Value,originalWorld))continue;var row=doc.CreateElement("deferred");row.SetAttribute("entity",pair.Key.entityId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("stable",pair.Key.RebirthRuntimeState?.StableId.ToString()??string.Empty);row.SetAttribute("projected",deferredProjected.Contains(pair.Key)?"1":"0");rows.Add(row);
        }
        rows.Sort((a,b)=>string.CompareOrdinal(a.OuterXml,b.OuterXml));foreach(var row in rows)root.AppendChild(row);return doc;
    }
}
