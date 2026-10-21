using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

public static partial class RebirthNpcAggregatePersistenceStore
{
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            bool hasState=loaded||dirty||Records.Count!=0||QuarantinedRecords.Count!=0||ReadViews.Count!=0||orderedReadViews!=null;
            if(!scope.MatchesBinding(loadedNativeWorld,string.IsNullOrEmpty(loadedSaveDirectory)?null:Path.Combine(loadedSaveDirectory,FileName),hasState)||(!string.IsNullOrEmpty(loadedSaveScope)&&loadedSaveScope!=scope.Fingerprint))return false;
            var records=Records;var quarantine=QuarantinedRecords;var views=ReadViews;var ordered=orderedReadViews;var nativeWorld=loadedNativeWorld;
            bool priorLoaded=loaded,priorDirty=dirty;long retry=retryLoadAfterUtcTicks,generation=storeEpoch;var epoch=recoveryMutationEpoch;string directory=loadedSaveDirectory,fingerprint=loadedSaveScope,provenance=provenanceState;
            var nextRecords=new Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord>();var nextQuarantine=new Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord>();var nextViews=new Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecordView>();var nextEpoch=new object();
            var doc=QuarantineAggregateImage();
            domains.Add(new RebirthNpcSaveQuarantineDomain("aggregate",doc,
                ()=>{lock(Sync)return ReferenceEquals(Records,records)&&ReferenceEquals(QuarantinedRecords,quarantine)&&ReferenceEquals(ReadViews,views)&&ReferenceEquals(orderedReadViews,ordered)&&ReferenceEquals(loadedNativeWorld,nativeWorld)&&loaded==priorLoaded&&dirty==priorDirty&&retryLoadAfterUtcTicks==retry&&storeEpoch==generation&&ReferenceEquals(recoveryMutationEpoch,epoch)&&loadedSaveDirectory==directory&&loadedSaveScope==fingerprint&&provenanceState==provenance&&QuarantineAggregateImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Records=nextRecords;QuarantinedRecords=nextQuarantine;ReadViews=nextViews;orderedReadViews=null;loaded=false;dirty=false;loadedNativeWorld=null;loadedSaveDirectory=string.Empty;loadedSaveScope=string.Empty;provenanceState="unloaded";retryLoadAfterUtcTicks=0;unchecked{storeEpoch++;}recoveryMutationEpoch=nextEpoch;}},
                ()=>{lock(Sync){Records=records;QuarantinedRecords=quarantine;ReadViews=views;orderedReadViews=ordered;loaded=priorLoaded;dirty=priorDirty;loadedNativeWorld=nativeWorld;loadedSaveDirectory=directory;loadedSaveScope=fingerprint;provenanceState=provenance;retryLoadAfterUtcTicks=retry;storeEpoch=generation;recoveryMutationEpoch=epoch;}}));
            return true;
        }
    }
    private static XmlDocument QuarantineAggregateImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("aggregateQuarantine");doc.AppendChild(root);
        root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("dirty",dirty?"1":"0");root.SetAttribute("scope",loadedSaveScope??string.Empty);root.SetAttribute("provenance",provenanceState??string.Empty);root.SetAttribute("epoch",storeEpoch.ToString(CultureInfo.InvariantCulture));
        var active=new XmlDocument{XmlResolver=null};active.LoadXml(RetirementImage(Records.Values));var activeNode=doc.CreateElement("active");root.AppendChild(activeNode);activeNode.AppendChild(doc.ImportNode(active.DocumentElement,true));
        var held=new XmlDocument{XmlResolver=null};held.LoadXml(RetirementImage(QuarantinedRecords.Values));var heldNode=doc.CreateElement("held");root.AppendChild(heldNode);heldNode.AppendChild(doc.ImportNode(held.DocumentElement,true));
        // ReadViews are immutable projections of these exact retained records;
        // keep their actual typed objects for rollback, never reconstruct in B.
        return doc;
    }
}
