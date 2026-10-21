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
        internal string Directory; internal int X,Z; internal IDisposable Allocation;
    }
    public sealed class WriteScope {internal Scope Scope;internal RegionFileV2 Region;internal byte[] Payload;internal bool Locked;internal IDisposable Allocation;}
    [ThreadStatic] private static Scope active;
    public static Scope Begin(object snapshot,string dir,int x,int z)
    {
        IDisposable allocation=null;
        try
        {
            if(!RebirthStationSnapshotEvidence.TryGetPublication(snapshot,out var request,out var bytes,out allocation)||request.X!=x||request.Z!=z||
                !ReferenceEquals(request.World,GameManager.Instance?.World))return null;
            request.Diagnostic="writer-entered";
            var scope=new Scope{Previous=active,Snapshot=snapshot,Request=request,Generation=request.Generation,Serialized=bytes,Directory=dir,X=x,Z=z,Allocation=allocation};
            active=scope;allocation=null;return scope;
        }
        finally{allocation?.Dispose();}
    }
    public static void End(Scope scope){if(scope!=null){active=scope.Previous;scope.Serialized=null;var allocation=scope.Allocation;scope.Allocation=null;allocation?.Dispose();}}
    public static WriteScope Before(RegionFileV2 region,int x,int z,int length,byte[] data,bool saveHeader)
    {
        var scope=active;
        if(scope!=null)scope.Request.Diagnostic="writer-arguments";
        if(scope==null||region==null||!saveHeader||scope.X!=x||scope.Z!=z||data==null||length<=8||length>data.Length||
            length>RebirthStationRegionPayload.MaximumPayloadBytes)return null;
        string expected=RegionFile.ConstructFullFilePath(scope.Directory,x>>5,z>>5,"7rg");
        if(!string.Equals(Path.GetFullPath(expected),Path.GetFullPath(region.fullFilePath),StringComparison.OrdinalIgnoreCase))return null;
        scope.Request.Diagnostic="writer-path-matched";
        IDisposable allocation=null;
        try
        {
        if(scope.Request.Budget!=null)
        {
            long expectedLength=scope.Serialized.Length-8L;
            // Readback parses an independently bounded region entry. Charge
            // its maximum payload even if a changed header names another size.
            long cost=length+RebirthStationRegionPayload.MaximumPayloadBytes+24L+Math.Max(256L,2*expectedLength)+expectedLength+4096+128*1024;
            if(scope.Serialized.Length>scope.Request.Budget.MaximumSnapshotBytes||!scope.Request.Budget.TryReserveAllocation(cost,out allocation))return null;
        }
        scope.Request.Diagnostic="writer-decoding";
        var payload=new byte[length];Buffer.BlockCopy(data,0,payload,0,length);
        int decodeLimit=scope.Request.Budget==null?RebirthStationSnapshotEvidence.MaximumSnapshotBytes:scope.Serialized.Length-8;
        if(!RebirthStationChunkInflate.TryDecode(payload,decodeLimit,out var decoded)||
            decoded.Length!=scope.Serialized.Length-8)return null;
        scope.Request.Diagnostic="writer-image-comparison";
        for(int i=0;i<decoded.Length;i++)if(decoded[i]!=scope.Serialized[i+8])return null;
        // Reentrant native lock spans original WriteData and readback, preventing another
        // writer/optimizer replacing the location header between publication and witness.
        var result=new WriteScope{Scope=scope,Region=region,Payload=payload};
        Monitor.Enter(region);result.Locked=true;result.Allocation=allocation;allocation=null;return result;
        }
        finally{allocation?.Dispose();}
    }
    public static void After(WriteScope write,Exception error)
    {
        if(write==null)return;
        try{
            write.Scope.Request.Diagnostic="writer-readback";
            if(error!=null)return;
            using(var file=SdFile.Open(write.Region.fullFilePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                if(RebirthStationRegionPayload.MatchesWrittenPayload(file,write.Scope.X&31,write.Scope.Z&31,write.Payload))
                    RebirthStationSnapshotEvidence.MarkPublished(write.Scope.Request,write.Scope.Snapshot,write.Scope.Generation,
                        write.Region.fullFilePath,RebirthStationPublicationRecord.Digest(write.Payload));
        }catch{ /* Missing or changed evidence leaves the preparation pending. */ }
        finally{try{if(write.Locked)Monitor.Exit(write.Region);}finally{write.Payload=null;var allocation=write.Allocation;write.Allocation=null;allocation?.Dispose();}}
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
    internal static void ValidateNativeWrite(int length,byte[] data)
    {
        // Native RegionFileV2 computes ceil((length+16)/4096) into a byte.
        // A wrap can write across other chunk sectors before its padding fails.
        // Reject before native header, allocation map or file mutation, even
        // when no REBIRTH publication observer is currently active.
        if(length<0||length>RebirthStationRegionPayload.MaximumPayloadBytes||data==null||length>data.Length)
            throw new InvalidDataException("Region payload cannot fit the native region sector format.");
    }
    [HarmonyPrefix] public static void Prefix(RegionFileV2 __instance,int _cX,int _cZ,int _dataLength,byte[] _data,bool _saveHeaderToFile,out RebirthStationPublicationEvidence.WriteScope __state)
    {__state=null;ValidateNativeWrite(_dataLength,_data);try{__state=RebirthStationPublicationEvidence.Before(__instance,_cX,_cZ,_dataLength,_data,_saveHeaderToFile);}catch{}}
    [HarmonyFinalizer] public static Exception Finalizer(Exception __exception,RebirthStationPublicationEvidence.WriteScope __state)
    {RebirthStationPublicationEvidence.After(__state,__exception);return __exception;}
}
