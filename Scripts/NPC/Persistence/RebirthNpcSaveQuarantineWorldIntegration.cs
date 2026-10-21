using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

public static partial class RebirthNpcWorldIntegrationService
{
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            if(worldSnapshotPublicationUncertain||PendingSpawns.Count!=0||SpawnAttempts.HasClaims)return false;
            if(!scope.MatchesBinding(loadedNativeWorld,string.IsNullOrEmpty(loadedNativeDirectory)?null:Path.Combine(loadedNativeDirectory,FileName),loaded||dirty||ByStable.Count!=0||ByAmbient.Count!=0||Completions.Count!=0||Replay.Count!=0||Markers.Count!=0))return false;
            if(ByStable.Count!=ByAmbient.Count)return false;foreach(var pair in ByStable)if(pair.Value==null||pair.Value.StableId!=pair.Key||!ByAmbient.TryGetValue(pair.Value.AmbientEntityId,out var ambient)||!ReferenceEquals(pair.Value,ambient))return false;
            var stable=ByStable;var ambientMap=ByAmbient;var markers=Markers;var pending=PendingSpawns;var completions=Completions;var replay=Replay;var world=loadedNativeWorld;string directory=loadedNativeDirectory;bool priorLoaded=loaded,priorDirty=dirty;var originalEpoch=worldOriginalEpoch;var originalBinding=worldOriginalBinding;var nextEpoch=new object();
            var nextStable=new Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord>();var nextAmbient=new Dictionary<int,RebirthNpcWorldIdentityRecord>();var nextMarkers=new Dictionary<string,RebirthNpcMarkerProjection>(StringComparer.Ordinal);var nextPending=new Dictionary<string,RebirthNpcPendingSpawn>(StringComparer.Ordinal);var nextCompletions=new Dictionary<string,RebirthNpcSpawnCompletion>(StringComparer.Ordinal);var nextReplay=new HashSet<string>(StringComparer.Ordinal);var doc=QuarantineWorldIntegrationImage();
            domains.Add(new RebirthNpcSaveQuarantineDomain("world-integration",doc,
                ()=>{lock(Sync)return !worldSnapshotPublicationUncertain&&!SpawnAttempts.HasClaims&&PendingSpawns.Count==0&&ReferenceEquals(ByStable,stable)&&ReferenceEquals(ByAmbient,ambientMap)&&ReferenceEquals(Markers,markers)&&ReferenceEquals(PendingSpawns,pending)&&ReferenceEquals(Completions,completions)&&ReferenceEquals(Replay,replay)&&ReferenceEquals(loadedNativeWorld,world)&&loadedNativeDirectory==directory&&loaded==priorLoaded&&dirty==priorDirty&&ReferenceEquals(worldOriginalEpoch,originalEpoch)&&ReferenceEquals(worldOriginalBinding,originalBinding)&&QuarantineWorldIntegrationImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){ByStable=nextStable;ByAmbient=nextAmbient;Markers=nextMarkers;PendingSpawns=nextPending;Completions=nextCompletions;Replay=nextReplay;loadedNativeWorld=null;loadedNativeDirectory=string.Empty;loaded=false;dirty=false;worldOriginalEpoch=nextEpoch;worldOriginalBinding=null;}},
                ()=>{lock(Sync){ByStable=stable;ByAmbient=ambientMap;Markers=markers;PendingSpawns=pending;Completions=completions;Replay=replay;loadedNativeWorld=world;loadedNativeDirectory=directory;loaded=priorLoaded;dirty=priorDirty;worldOriginalEpoch=originalEpoch;worldOriginalBinding=originalBinding;}}));
            return true;
        }
    }
    private static XmlDocument QuarantineWorldIntegrationImage()
    {
        var saved=CaptureRecoveryDocumentNoLock();var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("worldIntegrationQuarantine");doc.AppendChild(root);root.AppendChild(doc.ImportNode(saved.DocumentElement,true));root.SetAttribute("directory",loadedNativeDirectory??string.Empty);
        var ids=new List<RebirthNpcStableId>(ByStable.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));var flags=doc.CreateElement("identityFlags");root.AppendChild(flags);foreach(var id in ids){var row=doc.CreateElement("identity");row.SetAttribute("stable",id.ToString());row.SetAttribute("persistent",ByStable[id].Persistent?"1":"0");flags.AppendChild(row);}
        var keys=new List<string>(Markers.Keys);keys.Sort(StringComparer.Ordinal);var markers=doc.CreateElement("markers");root.AppendChild(markers);foreach(string key in keys)
        {
            var marker=Markers[key];if(marker==null)throw new InvalidOperationException("Original marker missing");var row=doc.CreateElement("marker");markers.AppendChild(row);row.SetAttribute("key",key);row.SetAttribute("viewer",marker.ViewerId??string.Empty);row.SetAttribute("stable",marker.StableId.ToString());row.SetAttribute("name",marker.DisplayName??string.Empty);row.SetAttribute("x",marker.Position.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("y",marker.Position.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("z",marker.Position.z.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("compass",marker.Compass?"1":"0");row.SetAttribute("map",marker.Map?"1":"0");row.SetAttribute("revision",marker.Revision.ToString(CultureInfo.InvariantCulture));
        }
        return doc;
    }
}
