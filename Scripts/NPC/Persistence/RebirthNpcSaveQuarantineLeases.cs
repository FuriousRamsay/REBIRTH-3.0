using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcExecutionLeaseRegistry
{
    internal static RebirthNpcSaveQuarantineDomain PrepareLeaseQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(UnknownWorkCustody.Count!=0)throw new InvalidOperationException("Unknown native work lease custody retained");
            if(recoveryReserved.Count!=0&&!ReferenceEquals(recoveryReservedWorld,scope.World))throw new InvalidOperationException("Reserved execution lease belongs to another native world");
            foreach(var pair in NativeWorkOwners)
            {
                var owner=pair.Value;
                if(owner==null||owner.Scope==null||!scope.MatchesBinding(owner.Scope.NativeWorld as World,owner.Scope.SaveFilePath,true)||owner.Actor==null||owner.Runtime==null||owner.OriginalLease==null||owner.OriginalLease.LeaseId!=pair.Key||!ReferenceEquals(owner.Actor.world,scope.World)||!ReferenceEquals(owner.Actor.RebirthRuntimeState,owner.Runtime)||owner.Runtime.StableId!=owner.StableOwner)throw new InvalidOperationException("Native work owner lineage unresolved");
            }
            foreach(var pair in ActiveByEntity)if(pair.Value==null||pair.Key!=pair.Value.TargetEntityId||!ByLeaseId.TryGetValue(pair.Value.LeaseId,out var retained)||!ReferenceEquals(retained,pair.Value))throw new InvalidOperationException("Active lease original index mismatch");
            var active=ActiveByEntity;var leases=ByLeaseId;var owners=NativeWorkOwners;var unknown=UnknownWorkCustody;var history=HistoryOrder;var reserved=recoveryReserved;var reservedWorld=recoveryReservedWorld;long version=revision;ulong allocator=nextLeaseId;
            var nextActive=new Dictionary<int,RebirthNpcExecutionLease>();var nextLeases=new Dictionary<ulong,RebirthNpcExecutionLease>();var nextOwners=new Dictionary<ulong,NativeWorkOwner>();var nextUnknown=new HashSet<ulong>();var nextHistory=new Queue<ulong>();var nextReserved=new Dictionary<ulong,RebirthNpcPersistedExecution>();var doc=QuarantineLeaseImage();
            return new RebirthNpcSaveQuarantineDomain("execution-leases",doc,
                ()=>{lock(Sync)return ReferenceEquals(ActiveByEntity,active)&&ReferenceEquals(ByLeaseId,leases)&&ReferenceEquals(NativeWorkOwners,owners)&&ReferenceEquals(UnknownWorkCustody,unknown)&&ReferenceEquals(HistoryOrder,history)&&ReferenceEquals(recoveryReserved,reserved)&&ReferenceEquals(recoveryReservedWorld,reservedWorld)&&revision==version&&nextLeaseId==allocator&&QuarantineLeaseImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){ActiveByEntity=nextActive;ByLeaseId=nextLeases;NativeWorkOwners=nextOwners;UnknownWorkCustody=nextUnknown;HistoryOrder=nextHistory;recoveryReserved=nextReserved;recoveryReservedWorld=null;revision=0;}},
                ()=>{lock(Sync){ActiveByEntity=active;ByLeaseId=leases;NativeWorkOwners=owners;UnknownWorkCustody=unknown;HistoryOrder=history;recoveryReserved=reserved;recoveryReservedWorld=reservedWorld;revision=version;nextLeaseId=allocator;}});
        }
    }
    private static XmlDocument QuarantineLeaseImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("leaseQuarantine");doc.AppendChild(root);root.SetAttribute("revision",revision.ToString(CultureInfo.InvariantCulture));root.SetAttribute("allocator",nextLeaseId.ToString(CultureInfo.InvariantCulture));
        var ids=new List<ulong>(ByLeaseId.Keys);ids.Sort();var rows=doc.CreateElement("leases");root.AppendChild(rows);foreach(var id in ids){var lease=ByLeaseId[id];if(lease==null||lease.LeaseId!=id)throw new InvalidOperationException("Lease original identity mismatch");rows.AppendChild(QuarantineLeaseRecord(doc,lease));}
        var entities=new List<int>(ActiveByEntity.Keys);entities.Sort();rows=doc.CreateElement("active");root.AppendChild(rows);foreach(int id in entities){var row=doc.CreateElement("entity");row.SetAttribute("id",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("lease",ActiveByEntity[id].LeaseId.ToString(CultureInfo.InvariantCulture));rows.AppendChild(row);}
        rows=doc.CreateElement("historyOrder");root.AppendChild(rows);foreach(var id in HistoryOrder){var row=doc.CreateElement("lease");row.SetAttribute("id",id.ToString(CultureInfo.InvariantCulture));rows.AppendChild(row);}
        ids=new List<ulong>(recoveryReserved.Keys);ids.Sort();rows=doc.CreateElement("reserved");root.AppendChild(rows);foreach(var id in ids){var original=recoveryReserved[id];if(original==null||original.LeaseId!=id)throw new InvalidOperationException("Reserved original identity mismatch");rows.AppendChild(RebirthNpcExecutionPersistenceStore.QuarantineExecutionRecord(doc,original));}
        ids=new List<ulong>(NativeWorkOwners.Keys);ids.Sort();rows=doc.CreateElement("nativeOwners");root.AppendChild(rows);foreach(var id in ids)
        {
            var owner=NativeWorkOwners[id];var row=doc.CreateElement("owner");rows.AppendChild(row);row.SetAttribute("lease",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("stable",owner.StableOwner.ToString());row.SetAttribute("profile",owner.ProfileId??string.Empty);row.SetAttribute("entity",owner.Actor.entityId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("class",owner.Actor.entityClass.ToString(CultureInfo.InvariantCulture));row.SetAttribute("path",owner.Scope.SaveFilePath??string.Empty);row.SetAttribute("generation",owner.Scope.LoadGeneration.ToString(CultureInfo.InvariantCulture));row.SetAttribute("hasGuard",owner.HasGuardPosition?"1":"0");row.SetAttribute("guardX",owner.GuardPosition.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("guardY",owner.GuardPosition.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("guardZ",owner.GuardPosition.z.ToString("R",CultureInfo.InvariantCulture));row.AppendChild(QuarantineLeaseRecord(doc,owner.OriginalLease));
        }
        return doc;
    }
    private static XmlElement QuarantineLeaseRecord(XmlDocument doc,RebirthNpcExecutionLease lease)
    {
        var row=doc.CreateElement("lease");row.SetAttribute("id",lease.LeaseId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("command",lease.CommandId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("entity",lease.TargetEntityId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("subject",lease.SubjectEntityId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("hasTarget",lease.HasTargetPosition?"1":"0");row.SetAttribute("x",lease.TargetPosition.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("y",lease.TargetPosition.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("z",lease.TargetPosition.z.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("issuer",lease.IssuerId??string.Empty);row.SetAttribute("kind",((int)lease.CommandKind).ToString(CultureInfo.InvariantCulture));row.SetAttribute("order",((int)lease.Order).ToString(CultureInfo.InvariantCulture));row.SetAttribute("acceptedRevision",lease.AcceptedRevision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("status",((int)lease.Status).ToString(CultureInfo.InvariantCulture));row.SetAttribute("detail",lease.Detail??string.Empty);row.SetAttribute("started",lease.StartedUtcTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("updated",lease.UpdatedUtcTicks.ToString(CultureInfo.InvariantCulture));return row;
    }
}
