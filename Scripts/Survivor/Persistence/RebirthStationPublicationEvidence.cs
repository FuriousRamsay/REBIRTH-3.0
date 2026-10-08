using System;
using System.IO;
using System.Threading;
using HarmonyLib;

// Native publication/readback only, still not a station-content or completion receipt.
public static class RebirthStationPublicationEvidence
{
    public sealed class Scope
    {
        internal Scope Previous; internal object Snapshot; internal long Generation;
        internal RebirthStationSnapshotEvidence.Request Request; internal byte[] Serialized;
        internal string Directory; internal int X,Z;
    }
    public sealed class WriteScope {internal Scope Scope;internal RegionFileV2 Region;internal byte[] Payload;internal bool Locked;}
    [ThreadStatic] private static Scope active;
    public static Scope Begin(object snapshot,string dir,int x,int z)
    {
        if(!RebirthStationSnapshotEvidence.TryGetPublication(snapshot,out var request,out var bytes)||request.X!=x||request.Z!=z||
            !ReferenceEquals(request.World,GameManager.Instance?.World))return null;
        var scope=new Scope{Previous=active,Snapshot=snapshot,Request=request,Generation=request.Generation,Serialized=bytes,Directory=dir,X=x,Z=z};
        active=scope;return scope;
    }
    public static void End(Scope scope){if(scope!=null)active=scope.Previous;}
    public static WriteScope Before(RegionFileV2 region,int x,int z,int length,byte[] data,bool saveHeader)
    {
        var scope=active;
        if(scope==null||region==null||!saveHeader||scope.X!=x||scope.Z!=z||data==null||length<=8||length>data.Length||
            length>RebirthStationRegionPayload.MaximumPayloadBytes)return null;
        string expected=RegionFile.ConstructFullFilePath(scope.Directory,x>>5,z>>5,"7rg");
        if(!string.Equals(Path.GetFullPath(expected),Path.GetFullPath(region.fullFilePath),StringComparison.OrdinalIgnoreCase))return null;
        var payload=new byte[length];Buffer.BlockCopy(data,0,payload,0,length);
        if(!RebirthStationChunkInflate.TryDecode(payload,RebirthStationSnapshotEvidence.MaximumSnapshotBytes,out var decoded)||
            decoded.Length!=scope.Serialized.Length-8)return null;
        for(int i=0;i<decoded.Length;i++)if(decoded[i]!=scope.Serialized[i+8])return null;
        // Reentrant native lock spans original WriteData and readback, preventing another
        // writer/optimizer replacing the location header between publication and witness.
        var result=new WriteScope{Scope=scope,Region=region,Payload=payload};
        Monitor.Enter(region);result.Locked=true;return result;
    }
    public static void After(WriteScope write,Exception error)
    {
        if(write==null)return;
        try{
            if(error!=null)return;
            using(var file=SdFile.Open(write.Region.fullFilePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                if(RebirthStationRegionPayload.MatchesWrittenPayload(file,write.Scope.X&31,write.Scope.Z&31,write.Payload))
                    RebirthStationSnapshotEvidence.MarkPublished(write.Scope.Request,write.Scope.Snapshot,write.Scope.Generation,
                        write.Region.fullFilePath,RebirthStationPublicationRecord.Digest(write.Payload));
        }catch{ /* Missing or changed evidence leaves the preparation pending. */ }
        finally{if(write.Locked)Monitor.Exit(write.Region);}
    }
}
[HarmonyPatch(typeof(RegionFileChunkSnapshot),nameof(RegionFileChunkSnapshot.Write))]
public static class RebirthStationSnapshotWritePatch
{
    [HarmonyPrefix] public static void Prefix(RegionFileChunkSnapshot __instance,string dir,int chunkX,int chunkZ,out RebirthStationPublicationEvidence.Scope __state)
    {__state=null;try{__state=RebirthStationPublicationEvidence.Begin(__instance,dir,chunkX,chunkZ);}catch{}}
    [HarmonyFinalizer] public static Exception Finalizer(Exception __exception,RebirthStationPublicationEvidence.Scope __state)
    {RebirthStationPublicationEvidence.End(__state);return __exception;}
}
[HarmonyPatch(typeof(RegionFileV2),nameof(RegionFileV2.WriteData))]
public static class RebirthStationRegionPublishedPatch
{
    [HarmonyPrefix] public static void Prefix(RegionFileV2 __instance,int _cX,int _cZ,int _dataLength,byte[] _data,bool _saveHeaderToFile,out RebirthStationPublicationEvidence.WriteScope __state)
    {__state=null;try{__state=RebirthStationPublicationEvidence.Before(__instance,_cX,_cZ,_dataLength,_data,_saveHeaderToFile);}catch{}}
    [HarmonyFinalizer] public static Exception Finalizer(Exception __exception,RebirthStationPublicationEvidence.WriteScope __state)
    {RebirthStationPublicationEvidence.After(__state,__exception);return __exception;}
}