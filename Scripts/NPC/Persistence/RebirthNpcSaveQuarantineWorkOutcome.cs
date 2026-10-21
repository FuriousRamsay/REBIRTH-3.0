using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcWorkOutcomeService
{
    internal static RebirthNpcSaveQuarantineDomain PrepareWorkOutcomeQuarantine()
    {
        lock(Sync)
        {
            // An in-flight sink may hold a PendingDelivery outside Sync. Until a
            // typed sink ownership protocol exists, its original remains held.
            if(PendingDeliveries.Count!=0)throw new InvalidOperationException("Work sink delivery original cannot detach");
            var outcomes=Outcomes;var events=Events;var emittedSet=Emitted;var emittedOrder=EmittedOrder;var epoch=recoveryEpoch;int version=persistenceVersion;
            var nextOutcomes=new Queue<RebirthNpcWorkOutcomeRecord>();var nextEvents=new Queue<RebirthNpcProgressionEvent>();var nextSet=new HashSet<Guid>();var nextOrder=new Queue<Guid>();var nextEpoch=new object();
            var doc=QuarantineWorkOutcomeImage();
            return new RebirthNpcSaveQuarantineDomain("work-outcome-service",doc,
                ()=>{lock(Sync)return PendingDeliveries.Count==0&&ReferenceEquals(Outcomes,outcomes)&&ReferenceEquals(Events,events)&&ReferenceEquals(Emitted,emittedSet)&&ReferenceEquals(EmittedOrder,emittedOrder)&&ReferenceEquals(recoveryEpoch,epoch)&&persistenceVersion==version&&QuarantineWorkOutcomeImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Outcomes=nextOutcomes;Events=nextEvents;Emitted=nextSet;EmittedOrder=nextOrder;recoveryEpoch=nextEpoch;persistenceVersion=0;}},
                ()=>{lock(Sync){Outcomes=outcomes;Events=events;Emitted=emittedSet;EmittedOrder=emittedOrder;recoveryEpoch=epoch;persistenceVersion=version;}});
        }
    }
    private static XmlDocument QuarantineWorkOutcomeImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("workOutcomeQuarantine");doc.AppendChild(root);
        var outcomes=new XmlDocument{XmlResolver=null};outcomes.LoadXml(RebirthNpcWorkOutcomePersistenceStore.RecoveryRecordsImage(Outcomes.ToArray()));root.AppendChild(doc.ImportNode(outcomes.DocumentElement,true));
        root.SetAttribute("version",persistenceVersion.ToString(CultureInfo.InvariantCulture));var rows=doc.CreateElement("events");root.AppendChild(rows);
        foreach(var e in Events)
        {
            if(e==null||e.EventId==Guid.Empty||e.OutcomeId==Guid.Empty||e.NpcId.IsEmpty||!Enum.IsDefined(typeof(RebirthNpcProgressionEventKind),e.Kind))throw new InvalidOperationException("Invalid original progression event");
            var row=doc.CreateElement("event");rows.AppendChild(row);row.SetAttribute("id",e.EventId.ToString("N"));row.SetAttribute("outcome",e.OutcomeId.ToString("N"));row.SetAttribute("npc",e.NpcId.ToString());row.SetAttribute("kind",((byte)e.Kind).ToString(CultureInfo.InvariantCulture));row.SetAttribute("assignment",e.AssignmentId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("definition",e.DefinitionId??string.Empty);row.SetAttribute("resource",e.ResourceKey??string.Empty);row.SetAttribute("quantity",e.Quantity.ToString(CultureInfo.InvariantCulture));row.SetAttribute("settlement",e.SettlementId??string.Empty);row.SetAttribute("created",e.CreatedUtcTicks.ToString(CultureInfo.InvariantCulture));
        }
        var seen=new HashSet<Guid>();rows=doc.CreateElement("emittedOrder");root.AppendChild(rows);
        foreach(var id in EmittedOrder){if(id==Guid.Empty||!seen.Add(id)||!Emitted.Contains(id))throw new InvalidOperationException("Original emitted replay mismatch");var row=doc.CreateElement("id");row.SetAttribute("value",id.ToString("N"));rows.AppendChild(row);}
        if(seen.Count!=Emitted.Count)throw new InvalidOperationException("Original emitted replay set mismatch");return doc;
    }
}
public static partial class RebirthNpcWorkOutcomePersistenceStore
{
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            if(!scope.MatchesBinding(boundWorld,boundPath,loaded||!RebirthNpcWorkOutcomeService.CanRetireEmptyBacking))return false;
            var world=boundWorld;string path=boundPath;bool priorLoaded=loaded;long saveAt=nextSaveUtcTicks;int version=lastSavedVersion;
            var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("workOutcomeBinding");doc.AppendChild(root);root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("path",path??string.Empty);root.SetAttribute("saveAt",saveAt.ToString(CultureInfo.InvariantCulture));root.SetAttribute("savedVersion",version.ToString(CultureInfo.InvariantCulture));
            var service=RebirthNpcWorkOutcomeService.PrepareWorkOutcomeQuarantine();
            var store=new RebirthNpcSaveQuarantineDomain("work-outcome-binding",doc,
                ()=>{lock(Sync)return loaded==priorLoaded&&ReferenceEquals(boundWorld,world)&&boundPath==path&&nextSaveUtcTicks==saveAt&&lastSavedVersion==version;},
                ()=>{lock(Sync){loaded=false;boundWorld=null;boundPath=null;nextSaveUtcTicks=0;lastSavedVersion=0;}},
                ()=>{lock(Sync){loaded=priorLoaded;boundWorld=world;boundPath=path;nextSaveUtcTicks=saveAt;lastSavedVersion=version;}});
            domains.Add(service);domains.Add(store);return true;
        }
    }
}
