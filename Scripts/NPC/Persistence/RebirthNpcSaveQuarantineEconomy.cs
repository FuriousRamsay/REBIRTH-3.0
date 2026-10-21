using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcEconomyLogisticsService
{
    internal static RebirthNpcSaveQuarantineDomain PrepareEconomyQuarantine()
    {
        lock(Sync)
        {
            // The public endpoint interface cannot enumerate outstanding native
            // reservations. An opaque endpoint is not a detachable empty domain.
            if(Endpoints.Count!=0)throw new InvalidOperationException("Opaque economy endpoint custody retained");
            var checkpoints=Checkpoints;var replay=Replay;var order=ReplayOrder;var epoch=recoveryEpoch;
            var nextCheckpoints=new Dictionary<string,OfflineCheckpoint>(StringComparer.OrdinalIgnoreCase);var nextReplay=new HashSet<string>(StringComparer.Ordinal);var nextOrder=new Queue<string>();var nextEpoch=new object();
            var doc=QuarantineEconomyImage();
            return new RebirthNpcSaveQuarantineDomain("economy-service",doc,
                ()=>{lock(Sync)return Endpoints.Count==0&&ReferenceEquals(Checkpoints,checkpoints)&&ReferenceEquals(Replay,replay)&&ReferenceEquals(ReplayOrder,order)&&ReferenceEquals(recoveryEpoch,epoch)&&QuarantineEconomyImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Checkpoints=nextCheckpoints;Replay=nextReplay;ReplayOrder=nextOrder;recoveryEpoch=nextEpoch;}},
                ()=>{lock(Sync){Checkpoints=checkpoints;Replay=replay;ReplayOrder=order;recoveryEpoch=epoch;}});
        }
    }
    private static XmlDocument QuarantineEconomyImage()
    {
        var persisted=RebirthNpcEconomyPersistenceStore.QuarantineImage();var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("economyQuarantine");doc.AppendChild(root);root.AppendChild(doc.ImportNode(persisted.DocumentElement,true));
        var rows=doc.CreateElement("replayOrder");root.AppendChild(rows);var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(string id in ReplayOrder)
        {
            if(string.IsNullOrWhiteSpace(id)||!seen.Add(id)||!Replay.Contains(id))throw new InvalidOperationException("Economy replay queue/set mismatch");
            var row=doc.CreateElement("original");row.SetAttribute("id",id);rows.AppendChild(row);
        }
        if(seen.Count!=Replay.Count)throw new InvalidOperationException("Economy replay set has unowned entries");
        foreach(var pair in Checkpoints)if(pair.Value==null||!string.Equals(pair.Key,pair.Value.SettlementId,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Economy checkpoint identity mismatch");
        return doc;
    }
}
public static partial class RebirthNpcEconomyPersistenceStore
{
    internal static XmlDocument QuarantineImage()
    {
        RebirthNpcEconomyLogisticsService.CaptureRetirement(out var rows,out var pending,out _,out _);
        var doc=SnapshotDocument(rows,pending);ReadDocument(doc);return doc;
    }
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            RebirthNpcEconomyLogisticsService.CaptureRetirement(out var rows,out _,out _,out var opaque);
            if(flushing||!scope.MatchesBinding(boundWorld as World,boundPath,loaded||dirty||loadFailed||rows.Count!=0||opaque))return false;
            bool priorLoaded=loaded,priorDirty=dirty,failed=loadFailed;long version=changeVersion;var world=boundWorld;string path=boundPath;var binding=originalBinding;
            var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("economyBinding");doc.AppendChild(root);root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("dirty",dirty?"1":"0");root.SetAttribute("loadFailed",failed?"1":"0");root.SetAttribute("path",path??string.Empty);root.SetAttribute("version",version.ToString(CultureInfo.InvariantCulture));
            var service=RebirthNpcEconomyLogisticsService.PrepareEconomyQuarantine();
            var store=new RebirthNpcSaveQuarantineDomain("economy-binding",doc,
                ()=>{lock(Sync)return !flushing&&loaded==priorLoaded&&dirty==priorDirty&&loadFailed==failed&&changeVersion==version&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding);},
                ()=>{lock(Sync){loaded=false;dirty=false;loadFailed=false;changeVersion=0;boundWorld=null;boundPath=null;originalBinding=null;}},
                ()=>{lock(Sync){loaded=priorLoaded;dirty=priorDirty;loadFailed=failed;changeVersion=version;boundWorld=world;boundPath=path;originalBinding=binding;}});
            domains.Add(service);domains.Add(store);return true;
        }
    }
}
