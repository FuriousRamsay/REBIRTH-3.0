using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

public static partial class RebirthNpcStableIdentityStore
{
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            if(!scope.MatchesBinding(loadedNativeWorld,string.IsNullOrEmpty(loadedSaveDirectory)?null:Path.Combine(loadedSaveDirectory,FileName),loaded||Pending.Count!=0||UnresolvedByEntity.Count!=0))return false;
            var pending=Pending;var unresolved=UnresolvedByEntity;var epoch=recoveryEpoch;var world=loadedNativeWorld;bool priorLoaded=loaded;string directory=loadedSaveDirectory;
            var nextPending=new Dictionary<RebirthNpcStableId,RebirthNpcStableIdentityRecord>();var nextUnresolved=new Dictionary<int,string>();var nextEpoch=new object();var doc=QuarantineIdentityImage();
            domains.Add(new RebirthNpcSaveQuarantineDomain("stable-identity",doc,
                ()=>{lock(Sync)return ReferenceEquals(Pending,pending)&&ReferenceEquals(UnresolvedByEntity,unresolved)&&ReferenceEquals(recoveryEpoch,epoch)&&ReferenceEquals(loadedNativeWorld,world)&&loaded==priorLoaded&&loadedSaveDirectory==directory&&QuarantineIdentityImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Pending=nextPending;UnresolvedByEntity=nextUnresolved;recoveryEpoch=nextEpoch;loaded=false;loadedNativeWorld=null;loadedSaveDirectory=string.Empty;}},
                ()=>{lock(Sync){Pending=pending;UnresolvedByEntity=unresolved;recoveryEpoch=epoch;loaded=priorLoaded;loadedNativeWorld=world;loadedSaveDirectory=directory;}}));
            return true;
        }
    }
    private static XmlDocument QuarantineIdentityImage()
    {
        // No current GameIO/native registry scan: Pending and unresolved retain
        // their exact original meaning even after the native current world moves.
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("stableIdentityQuarantine");doc.AppendChild(root);root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("directory",loadedSaveDirectory??string.Empty);
        var ids=new List<RebirthNpcStableId>(Pending.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));
        foreach(var id in ids)
        {
            var record=Pending[id];if(record==null||record.StableId!=id||id.IsEmpty||!QuarantineFinite(record.LastPosition.x)||!QuarantineFinite(record.LastPosition.y)||!QuarantineFinite(record.LastPosition.z))throw new InvalidOperationException("Unresolved stable identity image");
            var row=doc.CreateElement("npc");root.AppendChild(row);row.SetAttribute("id",id.ToString());row.SetAttribute("entity",record.LastEntityId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("class",record.EntityClassId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("profile",record.ProfileId??string.Empty);row.SetAttribute("x",record.LastPosition.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("y",record.LastPosition.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("z",record.LastPosition.z.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("attempts",record.MatchAttempts.ToString(CultureInfo.InvariantCulture));row.SetAttribute("issue",record.LastMatchIssue??string.Empty);row.SetAttribute("lastMatchEntity",record.LastMatchEntityId.ToString(CultureInfo.InvariantCulture));
        }
        var entities=new List<int>(UnresolvedByEntity.Keys);entities.Sort();foreach(int id in entities){var row=doc.CreateElement("unresolved");root.AppendChild(row);row.SetAttribute("entity",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("issue",UnresolvedByEntity[id]??string.Empty);}return doc;
    }
    private static bool QuarantineFinite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}
