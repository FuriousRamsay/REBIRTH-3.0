using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

public static partial class RebirthNpcStableIdentityStore
{
    internal static RebirthNpcSaveQuarantineNativeSavedStore CaptureNativeSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!loaded&&scope!=null)
            {
                var originalEpoch=recoveryEpoch;var originalPending=Pending;var originalUnresolved=UnresolvedByEntity;
                bool PrivateEmpty()=>!loaded&&loadedNativeWorld==null&&string.IsNullOrEmpty(loadedSaveDirectory)&&Pending.Count==0&&UnresolvedByEntity.Count==0;
                bool OriginalEmpty(){lock(Sync)return PrivateEmpty()&&ReferenceEquals(recoveryEpoch,originalEpoch)&&ReferenceEquals(Pending,originalPending)&&ReferenceEquals(UnresolvedByEntity,originalUnresolved);}
                bool DetachedEmpty(){lock(Sync)return PrivateEmpty()&&!ReferenceEquals(recoveryEpoch,originalEpoch)&&!ReferenceEquals(Pending,originalPending)&&!ReferenceEquals(UnresolvedByEntity,originalUnresolved);}
                bool IncomingOwner(World incoming){lock(Sync)return loaded&&ReferenceEquals(loadedNativeWorld,incoming)&&RebirthNpcSaveQuarantineScope.SameDirectory(loadedSaveDirectory,scope.Directory)&&UnresolvedByEntity.Count==0;}
                return RebirthNpcSaveQuarantineNativeSavedStore.CaptureUninitializedOwner("stable-identity",scope,Path.Combine(scope.Directory,FileName),
                    OriginalEmpty,DetachedEmpty,IncomingOwner,ValidateDocument,CaptureCurrentDocument,EnsureLoaded);
            }
            if(!loaded||loadedNativeWorld==null||UnresolvedByEntity.Count!=0||!CanRetireBacking||string.IsNullOrEmpty(loadedSaveDirectory))return null;
            var world=loadedNativeWorld;var epoch=recoveryEpoch;var pending=Pending;var unresolved=UnresolvedByEntity;
            string directory=loadedSaveDirectory,path=Path.Combine(directory,FileName),outgoing=QuarantineIdentityImage().OuterXml;
            var primary=CaptureCurrentDocument();string savedRows=NativeAdoptionSavedRows(primary);
            var declarations=new Dictionary<RebirthNpcStableId,XmlElement>();
            foreach(XmlElement row in primary.DocumentElement.SelectNodes("npc"))
            {RebirthNpcStableId.TryParse(row.GetAttribute("stableId"),out var id);declarations.Add(id,(XmlElement)row.CloneNode(true));}
            bool metadataOnly=Pending.Count==declarations.Count&&NativeAdoptionPendingRows()==savedRows;
            bool Original(){lock(Sync)return loaded&&UnresolvedByEntity.Count==0&&ReferenceEquals(loadedNativeWorld,world)&&
                loadedSaveDirectory==directory&&ReferenceEquals(recoveryEpoch,epoch)&&ReferenceEquals(Pending,pending)&&ReferenceEquals(UnresolvedByEntity,unresolved)&&
                QuarantineIdentityImage().OuterXml==outgoing;}
            bool Empty(){lock(Sync)return !loaded&&loadedNativeWorld==null&&string.IsNullOrEmpty(loadedSaveDirectory)&&
                Pending.Count==0&&UnresolvedByEntity.Count==0&&!ReferenceEquals(recoveryEpoch,epoch);}
            bool Incoming(World incoming){lock(Sync)return loaded&&ReferenceEquals(loadedNativeWorld,incoming)&&
                RebirthNpcSaveQuarantineScope.SameDirectory(directory,loadedSaveDirectory)&&UnresolvedByEntity.Count==0&&NativeAdoptionPendingRows()==savedRows;}
            bool Native(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
            {
                if(source?.Current()!=true||!declarations.TryGetValue(source.StableId,out var row))return false;
                return int.Parse(row.GetAttribute("class"),CultureInfo.InvariantCulture)==source.Actor.entityClass&&
                    row.GetAttribute("profile")==source.Actor.RebirthProfileId;
            }
            return RebirthNpcSaveQuarantineNativeSavedStore.Capture("stable-identity",scope,world,path,true,Original,Empty,Incoming,Native,
                document=>ValidateDocument(document)&&NativeAdoptionSavedRows(document)==savedRows,primary,EnsureLoaded,
                source=>metadataOnly&&source?.Current()==true);
        }
    }
    private static string NativeAdoptionSavedRows(XmlDocument document)
    {
        var rows=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(XmlElement row in document.DocumentElement.SelectNodes("npc"))
            rows.Add(row.GetAttribute("stableId"),NativeAdoptionIdentityRow(row.GetAttribute("stableId"),
                int.Parse(row.GetAttribute("entity"),CultureInfo.InvariantCulture),int.Parse(row.GetAttribute("class"),CultureInfo.InvariantCulture),row.GetAttribute("profile"),
                float.Parse(row.GetAttribute("x"),CultureInfo.InvariantCulture),float.Parse(row.GetAttribute("y"),CultureInfo.InvariantCulture),float.Parse(row.GetAttribute("z"),CultureInfo.InvariantCulture),
                row.HasAttribute("attempts")?int.Parse(row.GetAttribute("attempts"),CultureInfo.InvariantCulture):0,row.GetAttribute("lastMatchIssue"),
                row.HasAttribute("lastMatchEntity")?int.Parse(row.GetAttribute("lastMatchEntity"),CultureInfo.InvariantCulture):0));
        return string.Join("",rows.Values);
    }
    private static string NativeAdoptionPendingRows()
    {
        var rows=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(var record in Pending.Values)rows.Add(record.StableId.ToString(),NativeAdoptionIdentityRow(record.StableId.ToString(),record.LastEntityId,
            record.EntityClassId,record.ProfileId,record.LastPosition.x,record.LastPosition.y,record.LastPosition.z,record.MatchAttempts,record.LastMatchIssue,record.LastMatchEntityId));
        return string.Join("",rows.Values);
    }
    private static string NativeAdoptionIdentityRow(string id,int entity,int cls,string profile,float x,float y,float z,int attempts,string issue,int last)
    {
        var document=new XmlDocument();var row=document.CreateElement("npc");document.AppendChild(row);
        row.SetAttribute("id",id);row.SetAttribute("entity",entity.ToString(CultureInfo.InvariantCulture));row.SetAttribute("class",cls.ToString(CultureInfo.InvariantCulture));
        row.SetAttribute("profile",profile??string.Empty);row.SetAttribute("x",x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("y",y.ToString("R",CultureInfo.InvariantCulture));
        row.SetAttribute("z",z.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("attempts",attempts.ToString(CultureInfo.InvariantCulture));
        row.SetAttribute("issue",issue??string.Empty);row.SetAttribute("last",last.ToString(CultureInfo.InvariantCulture));return row.OuterXml;
    }
}
