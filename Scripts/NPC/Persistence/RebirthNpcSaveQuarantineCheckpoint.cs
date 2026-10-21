using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcWorkExecutionTelemetry
{
    internal static RebirthNpcSaveQuarantineDomain PrepareTelemetryQuarantine()
    {
        lock(Sync)
        {
            var active=Active;var order=Order;long version=revision;
            var nextActive=new Dictionary<ulong,RebirthNpcWorkExecutionSnapshot>();var nextOrder=new RebirthNpcFairRoundRobin<ulong>();var doc=QuarantineTelemetryImage();
            return new RebirthNpcSaveQuarantineDomain("work-telemetry",doc,
                ()=>{lock(Sync)return ReferenceEquals(Active,active)&&ReferenceEquals(Order,order)&&revision==version&&QuarantineTelemetryImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Active=nextActive;Order=nextOrder;revision=0;}},
                ()=>{lock(Sync){Active=active;Order=order;revision=version;}});
        }
    }
    private static XmlDocument QuarantineTelemetryImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("workTelemetryQuarantine");doc.AppendChild(root);root.SetAttribute("revision",revision.ToString(CultureInfo.InvariantCulture));var ids=new List<ulong>(Active.Keys);ids.Sort();
        foreach(var id in ids)
        {
            var record=Active[id];if(record==null||record.AssignmentId!=id||id==0||float.IsNaN(record.Progress)||float.IsInfinity(record.Progress))throw new InvalidOperationException("Telemetry original image unresolved");
            var row=doc.CreateElement("snapshot");root.AppendChild(row);row.SetAttribute("assignment",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("executor",record.ExecutorId??string.Empty);row.SetAttribute("phase",((int)record.Phase).ToString(CultureInfo.InvariantCulture));row.SetAttribute("target",record.StableTargetId??string.Empty);row.SetAttribute("progress",record.Progress.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("retry",record.RetryCount.ToString(CultureInfo.InvariantCulture));row.SetAttribute("suspension",record.SuspensionReason??string.Empty);row.SetAttribute("source",record.SourceEndpoint??string.Empty);row.SetAttribute("destination",record.DestinationEndpoint??string.Empty);row.SetAttribute("updated",record.UpdatedUtcTicks.ToString(CultureInfo.InvariantCulture));
        }
        var queue=doc.CreateElement("order");root.AppendChild(queue);var seen=new HashSet<ulong>();foreach(var id in Order.CopyOrder()){if(!seen.Add(id)||!Active.ContainsKey(id))throw new InvalidOperationException("Telemetry scheduling ownership mismatch");var row=doc.CreateElement("assignment");row.SetAttribute("id",id.ToString(CultureInfo.InvariantCulture));queue.AppendChild(row);}if(seen.Count!=Active.Count)throw new InvalidOperationException("Telemetry scheduling record missing");return doc;
    }
}
public static partial class RebirthNpcWorkCheckpointPersistenceStore
{
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            if(!scope.MatchesBinding(boundWorld,boundPath,loaded||reconciled||Loaded.Count!=0||RebirthNpcWorkExecutionTelemetry.Snapshot().Length!=0))return false;
            var rows=Loaded;var world=boundWorld;string path=boundPath;bool priorLoaded=loaded,priorReconciled=reconciled;long saveAt=nextSaveUtcTicks,savedRevision=lastSavedTelemetryRevision;
            string image=RecoveryRowsImage(Loaded);var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("checkpointBinding");doc.AppendChild(root);root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("reconciled",reconciled?"1":"0");root.SetAttribute("path",path??string.Empty);root.SetAttribute("saveAt",saveAt.ToString(CultureInfo.InvariantCulture));root.SetAttribute("savedRevision",savedRevision.ToString(CultureInfo.InvariantCulture));var saved=doc.CreateElement("canonicalRows");saved.InnerText=image;root.AppendChild(saved);
            var nextRows=new List<RebirthNpcWorkRecoveryCheckpoint>();var telemetry=RebirthNpcWorkExecutionTelemetry.PrepareTelemetryQuarantine();
            var store=new RebirthNpcSaveQuarantineDomain("work-checkpoint-binding",doc,
                ()=>{lock(Sync)return ReferenceEquals(Loaded,rows)&&ReferenceEquals(boundWorld,world)&&boundPath==path&&loaded==priorLoaded&&reconciled==priorReconciled&&nextSaveUtcTicks==saveAt&&lastSavedTelemetryRevision==savedRevision&&RecoveryRowsImage(Loaded)==image;},
                ()=>{lock(Sync){Loaded=nextRows;boundWorld=null;boundPath=null;loaded=false;reconciled=false;nextSaveUtcTicks=0;lastSavedTelemetryRevision=-1;}},
                ()=>{lock(Sync){Loaded=rows;boundWorld=world;boundPath=path;loaded=priorLoaded;reconciled=priorReconciled;nextSaveUtcTicks=saveAt;lastSavedTelemetryRevision=savedRevision;}});
            domains.Add(telemetry);domains.Add(store);return true;
        }
    }
}
