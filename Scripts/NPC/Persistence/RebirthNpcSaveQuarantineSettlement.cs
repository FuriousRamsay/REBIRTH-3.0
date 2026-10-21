using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcSettlementSimulation
{
    internal static RebirthNpcSaveQuarantineDomain PrepareSettlementQuarantine()
    {
        lock(Sync)
        {
            var needs=Needs;var professions=Professions;var work=Work;var resources=Resources;var reservations=ResourceReservations;
            var available=AvailableByPriority;var queued=AvailableQueued;var claimed=ClaimedWorkByNpc;var busy=BusyNpcs;var expiry=ClaimExpiryByDue;var terminal=TerminalWorkOrderHistory;
            var epoch=recoveryEpoch;long needsAt=nextNeedsTick,assignAt=nextAssignmentTick;
            var nextNeeds=new Dictionary<RebirthNpcStableId,RebirthNpcNeedState>();var nextProfessions=new Dictionary<RebirthNpcStableId,RebirthNpcProfessionRecord>();var nextWork=new Dictionary<Guid,RebirthNpcWorkOrder>();
            var nextResources=new Dictionary<string,RebirthNpcSettlementResourcePersistentRecord>(StringComparer.OrdinalIgnoreCase);var nextReservations=new Dictionary<Guid,RebirthNpcResourceReservationPersistentRecord>();
            var nextAvailable=new SortedDictionary<int,Queue<Guid>>(available.Comparer);var nextQueued=new HashSet<Guid>();var nextClaimed=new Dictionary<RebirthNpcStableId,Guid>();var nextBusy=new HashSet<RebirthNpcStableId>();var nextExpiry=new SortedDictionary<long,Queue<Guid>>();var nextTerminal=new Queue<Guid>();var nextEpoch=new object();
            var doc=QuarantineSettlementImage();
            return new RebirthNpcSaveQuarantineDomain("settlement-service",doc,
                ()=>{lock(Sync)return ReferenceEquals(Needs,needs)&&ReferenceEquals(Professions,professions)&&ReferenceEquals(Work,work)&&ReferenceEquals(Resources,resources)&&ReferenceEquals(ResourceReservations,reservations)&&ReferenceEquals(AvailableByPriority,available)&&ReferenceEquals(AvailableQueued,queued)&&ReferenceEquals(ClaimedWorkByNpc,claimed)&&ReferenceEquals(BusyNpcs,busy)&&ReferenceEquals(ClaimExpiryByDue,expiry)&&ReferenceEquals(TerminalWorkOrderHistory,terminal)&&ReferenceEquals(recoveryEpoch,epoch)&&nextNeedsTick==needsAt&&nextAssignmentTick==assignAt&&QuarantineSettlementImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Needs=nextNeeds;Professions=nextProfessions;Work=nextWork;Resources=nextResources;ResourceReservations=nextReservations;AvailableByPriority=nextAvailable;AvailableQueued=nextQueued;ClaimedWorkByNpc=nextClaimed;BusyNpcs=nextBusy;ClaimExpiryByDue=nextExpiry;TerminalWorkOrderHistory=nextTerminal;recoveryEpoch=nextEpoch;nextNeedsTick=0;nextAssignmentTick=0;}},
                ()=>{lock(Sync){Needs=needs;Professions=professions;Work=work;Resources=resources;ResourceReservations=reservations;AvailableByPriority=available;AvailableQueued=queued;ClaimedWorkByNpc=claimed;BusyNpcs=busy;ClaimExpiryByDue=expiry;TerminalWorkOrderHistory=terminal;recoveryEpoch=epoch;nextNeedsTick=needsAt;nextAssignmentTick=assignAt;}});
        }
    }
    private static XmlDocument QuarantineSettlementImage()
    {
        var persisted=RebirthNpcSettlementPersistenceStore.QuarantineImage();var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("settlementQuarantine");doc.AppendChild(root);root.AppendChild(doc.ImportNode(persisted.DocumentElement,true));
        root.SetAttribute("needsAt",nextNeedsTick.ToString(CultureInfo.InvariantCulture));root.SetAttribute("assignmentAt",nextAssignmentTick.ToString(CultureInfo.InvariantCulture));
        var available=doc.CreateElement("available");root.AppendChild(available);
        foreach(var priority in AvailableByPriority){var row=doc.CreateElement("priority");row.SetAttribute("value",priority.Key.ToString(CultureInfo.InvariantCulture));available.AppendChild(row);foreach(var id in priority.Value)AppendQuarantineId(doc,row,id);}
        var queued=doc.CreateElement("queued");root.AppendChild(queued);var sortedIds=new List<Guid>(AvailableQueued);sortedIds.Sort();foreach(var id in sortedIds)AppendQuarantineId(doc,queued,id);
        var expiry=doc.CreateElement("expiry");root.AppendChild(expiry);foreach(var due in ClaimExpiryByDue){var row=doc.CreateElement("due");row.SetAttribute("value",due.Key.ToString(CultureInfo.InvariantCulture));expiry.AppendChild(row);foreach(var id in due.Value)AppendQuarantineId(doc,row,id);}
        var terminal=doc.CreateElement("terminal");root.AppendChild(terminal);foreach(var id in TerminalWorkOrderHistory)AppendQuarantineId(doc,terminal,id);
        var owners=new List<RebirthNpcStableId>(ClaimedWorkByNpc.Keys);owners.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));var claims=doc.CreateElement("claims");root.AppendChild(claims);
        foreach(var id in owners){var row=doc.CreateElement("claim");row.SetAttribute("npc",id.ToString());row.SetAttribute("work",ClaimedWorkByNpc[id].ToString("N"));claims.AppendChild(row);}
        owners=new List<RebirthNpcStableId>(BusyNpcs);owners.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));var busy=doc.CreateElement("busy");root.AppendChild(busy);foreach(var id in owners){var row=doc.CreateElement("npc");row.SetAttribute("id",id.ToString());busy.AppendChild(row);}
        return doc;
    }
    private static void AppendQuarantineId(XmlDocument doc,XmlElement parent,Guid id)
    {var row=doc.CreateElement("work");row.SetAttribute("id",id.ToString("N"));parent.AppendChild(row);}
}
public static partial class RebirthNpcSettlementPersistenceStore
{
    internal static XmlDocument QuarantineImage()
    {RebirthNpcSettlementSimulation.CaptureRetirement(out var state,out _,out _);var doc=Write(state);if(!ValidateDocument(doc))throw new InvalidOperationException("Settlement quarantine image invalid");return doc;}
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            RebirthNpcSettlementSimulation.CaptureRetirement(out var state,out _,out var custody);
            if(!scope.MatchesBinding(boundWorld,boundPath,loaded||dirty||custody||(state.Needs?.Length??0)!=0||(state.Professions?.Length??0)!=0||(state.WorkOrders?.Length??0)!=0||(state.Resources?.Length??0)!=0||(state.ResourceReservations?.Length??0)!=0))return false;
            bool priorLoaded=loaded,priorDirty=dirty;var world=boundWorld;string path=boundPath;var binding=originalBinding;long saveAt=nextSaveUtcTicks;
            var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("settlementBinding");doc.AppendChild(root);root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("dirty",dirty?"1":"0");root.SetAttribute("path",path??string.Empty);root.SetAttribute("saveAt",saveAt.ToString(CultureInfo.InvariantCulture));
            var service=RebirthNpcSettlementSimulation.PrepareSettlementQuarantine();
            var store=new RebirthNpcSaveQuarantineDomain("settlement-binding",doc,
                ()=>{lock(Sync)return loaded==priorLoaded&&dirty==priorDirty&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding)&&nextSaveUtcTicks==saveAt;},
                ()=>{lock(Sync){loaded=false;dirty=false;boundWorld=null;boundPath=null;originalBinding=null;nextSaveUtcTicks=0;}},
                ()=>{lock(Sync){loaded=priorLoaded;dirty=priorDirty;boundWorld=world;boundPath=path;originalBinding=binding;nextSaveUtcTicks=saveAt;}});
            domains.Add(service);domains.Add(store);return true;
        }
    }
}
