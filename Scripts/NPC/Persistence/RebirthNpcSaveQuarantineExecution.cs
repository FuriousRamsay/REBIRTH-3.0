using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcExecutionPersistenceStore
{
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            var savedScope=loadedWorkScope;
            if(!scope.MatchesBinding(savedScope?.NativeWorld as World,savedScope?.SaveFilePath,loaded||PendingByStableId.Count!=0||LegacyPendingByEntity.Count!=0||WorkCustody.Count!=0))return false;
            foreach(var pair in WorkCustody)
                if(pair.Key==null||pair.Value==null||pair.Value.Scope==null||!ReferenceEquals(pair.Value.Scope,savedScope)||!ReferenceEquals(pair.Value.OriginalRecord,pair.Key)||pair.Value.OriginalElement==null)throw new InvalidOperationException("Unknown execution work custody retained");
            var pending=PendingByStableId;var legacy=LegacyPendingByEntity;var custody=WorkCustody;bool priorLoaded=loaded;long loadGeneration=workLoadGeneration,saveAt=nextSaveUtcTicks,leaseRevision=lastSavedLeaseRevision,pendingVersion=pendingRevision,savedPending=lastSavedPendingRevision;
            var nextPending=new Dictionary<RebirthNpcStableId,RebirthNpcPersistedExecution>();var nextLegacy=new Dictionary<int,RebirthNpcPersistedExecution>();var nextCustody=new Dictionary<RebirthNpcPersistedExecution,SavedWorkCustody>();var doc=QuarantineExecutionImage();
            domains.Add(new RebirthNpcSaveQuarantineDomain("execution-persistence",doc,
                ()=>{lock(Sync)return ReferenceEquals(PendingByStableId,pending)&&ReferenceEquals(LegacyPendingByEntity,legacy)&&ReferenceEquals(WorkCustody,custody)&&ReferenceEquals(loadedWorkScope,savedScope)&&loaded==priorLoaded&&workLoadGeneration==loadGeneration&&nextSaveUtcTicks==saveAt&&lastSavedLeaseRevision==leaseRevision&&pendingRevision==pendingVersion&&lastSavedPendingRevision==savedPending&&QuarantineExecutionImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){PendingByStableId=nextPending;LegacyPendingByEntity=nextLegacy;WorkCustody=nextCustody;loadedWorkScope=null;loaded=false;nextSaveUtcTicks=0;lastSavedLeaseRevision=-1;pendingRevision=0;lastSavedPendingRevision=-1;}},
                ()=>{lock(Sync){PendingByStableId=pending;LegacyPendingByEntity=legacy;WorkCustody=custody;loadedWorkScope=savedScope;loaded=priorLoaded;workLoadGeneration=loadGeneration;nextSaveUtcTicks=saveAt;lastSavedLeaseRevision=leaseRevision;pendingRevision=pendingVersion;lastSavedPendingRevision=savedPending;}}));
            return true;
        }
    }
    private static XmlDocument QuarantineExecutionImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("executionQuarantine");doc.AppendChild(root);root.SetAttribute("path",loadedWorkScope?.SaveFilePath??string.Empty);root.SetAttribute("loadSource",loadedWorkScope?.ValidatedLoadSource??string.Empty);root.SetAttribute("loadGeneration",(loadedWorkScope?.LoadGeneration??0).ToString(CultureInfo.InvariantCulture));
        var ids=new List<RebirthNpcStableId>(PendingByStableId.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));var rows=doc.CreateElement("pending");root.AppendChild(rows);foreach(var id in ids){var record=PendingByStableId[id];if(record==null||record.StableId!=id)throw new InvalidOperationException("Execution pending identity mismatch");rows.AppendChild(QuarantineExecutionRecord(doc,record));}
        var entities=new List<int>(LegacyPendingByEntity.Keys);entities.Sort();rows=doc.CreateElement("legacy");root.AppendChild(rows);foreach(int id in entities){var record=LegacyPendingByEntity[id];if(record==null||record.EntityId!=id)throw new InvalidOperationException("Legacy execution identity mismatch");rows.AppendChild(QuarantineExecutionRecord(doc,record));}
        var held=new List<string>();foreach(var pair in WorkCustody)
        {
            var item=pair.Value;if(item==null||item.Scope==null||item.Snapshot==null||item.OriginalRecord==null||item.OriginalElement==null)throw new InvalidOperationException("Execution custody unknown");
            var row=doc.CreateElement("held");row.SetAttribute("generation",item.Scope.LoadGeneration.ToString(CultureInfo.InvariantCulture));row.SetAttribute("path",item.Scope.SaveFilePath??string.Empty);row.AppendChild(QuarantineExecutionRecord(doc,item.Snapshot));var current=doc.CreateElement("originalRecord");row.AppendChild(current);current.AppendChild(QuarantineExecutionRecord(doc,item.OriginalRecord));var raw=doc.CreateElement("savedElement");row.AppendChild(raw);raw.AppendChild(doc.ImportNode(item.OriginalElement,true));held.Add(row.OuterXml);
        }
        held.Sort(StringComparer.Ordinal);rows=doc.CreateElement("custody");root.AppendChild(rows);foreach(string value in held){var raw=new XmlDocument{XmlResolver=null};raw.LoadXml(value);rows.AppendChild(doc.ImportNode(raw.DocumentElement,true));}return doc;
    }
    internal static XmlElement QuarantineExecutionRecord(XmlDocument doc,RebirthNpcPersistedExecution record)
    {
        if(record==null)throw new InvalidOperationException("Execution original missing");
        var row=Write(doc,record);
        // Preserve even inactive native coordinates; ordinary save omits these.
        SetVector(row,"target",record.TargetPosition);SetVector(row,"guard",record.GuardPosition);return row;
    }
}
