using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
#if REBIRTH_DEBUG
    private static readonly Dictionary<Guid,JObject> source4ObservedOwners=new Dictionary<Guid,JObject>();
    private static World source4AuditWorld;
#endif
    private static void ObserveSource4OwnerIdentity(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,string proof,string producer)
    {
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||root==null||artifact==null)return;
        if(!ReferenceEquals(source4AuditWorld,GameManager.Instance?.World)){source4ObservedOwners.Clear();source4AuditWorld=GameManager.Instance?.World;}
        if(source4ObservedOwners.Count>=128&&!source4ObservedOwners.ContainsKey(root.Operation))return;
        var plan=artifact.Copy().Element("plan");
        bool native=producer=="actual-native-load-query"||producer=="actual-native-load-owner-lookup";
        if(source4ObservedOwners.TryGetValue(root.Operation,out var previous)&&previous["nativeOwnerReentryObserved"]?.Value<bool>()==true)native=true;
        source4ObservedOwners[root.Operation]=new JObject{["sourceOperation"]=root.Operation.ToString("N"),["binding"]=root.Binding,["ownerKey"]=root.Owner,
            ["creationId"]=root.Creation,["deaths"]=artifact.Original.Deaths,["birth"]=artifact.Birth,["sourceWorld"]=root.World,
            ["proof"]=proof,["ownerIdentityPublicationObserved"]=true,["nativeOwnerReentryObserved"]=native,["producer"]=producer,["originalEntityId"]=(int)plan.Attribute("entity"),
            ["nativeAfterFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(plan.Element("afterTile").Value)};
#endif
    }
    // Bounded read-only projection of already captured native observations.
    // It does not open source histories, reconstruct capabilities or write files.
    internal static object CaptureSource4AuditState(EntityPlayer actor)
    {
        var result=new JObject{["schema"]=1,["available"]=false,["active"]=ProtocolActive,["records"]=new JArray(),["truncated"]=false};
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||actor?.world?.IsRemote()!=false||
            !ReferenceEquals(actor.world,GameManager.Instance?.World)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return result;
        result["available"]=true;result["identityObserversInstalled"]=RebirthProductiveEarnedUiQueueOwner.AcceptsSavedSource4;
        var records=(JArray)result["records"];
        var observed=ReferenceEquals(source4AuditWorld,actor.world)?source4ObservedOwners.Where(pair=>(string)pair.Value["ownerKey"]==owner.StorageKey).ToArray():Array.Empty<KeyValuePair<Guid,JObject>>();
        Source4NativeOriginal[] loads;lock(source4LoadGate)loads=source4NativeOriginals.Where(value=>ReferenceEquals(value.World,actor.world)&&value.Reference.Owner==owner.StorageKey).ToArray();
        foreach(var operation in observed.Select(pair=>pair.Key).Concat(loads.Select(value=>value.Reference.Source)).Distinct().Take(65))
        {
            if(records.Count>=64){result["truncated"]=true;break;}
            var prior=observed.SingleOrDefault(pair=>pair.Key==operation).Value;
            var row=prior==null?new JObject{["sourceOperation"]=operation.ToString("N"),["ownerIdentityPublicationObserved"]=false}:(JObject)prior.DeepClone();
            var matches=loads.Where(value=>value.Reference.Source==operation).ToArray();
            row["nativeLoadCandidates"]=matches.Length;
            if(matches.Length==1)
            {
                var source=matches[0];row["binding"]=source.Reference.Binding;row["ownerKey"]=source.Reference.Owner;row["creationId"]=source.Reference.Creation;
                row["birth"]=source.Reference.Birth;row["proof"]=source.Proof;row["savedCompletionDigest"]=source.Native.CompletionDigest;
                row["savedSnapshotDigest"]=source.Native.SnapshotDigest;row["savedSnapshotBytes"]=source.Native.SnapshotLength;row["nativeTileOffset"]=source.Native.Offset;
                row["nativeTileBytes"]=source.Native.TileLength;row["sourcePhaseAtDiscovery"]=source.Phase;row["actualNativeLoadObserved"]=source.Installed;
                row["actualNativeLoadCurrent"]=source.LoadedCurrent();row["actualOriginalRowCurrent"]=source.OriginalRowCurrent();row["nativeReadFence"]=source.Fence;
                row["originalCount"]=source.OriginalCount;row["originalEntityId"]=source.Row?.StartingEntityId??-1;
            }
            records.Add(row);
        }
#endif
        return result;
    }
}
