using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    private static bool NativeFinalCurrent(Context source)
    {
        if(source?.NativeOutput==null)return false;
        if(source.NativeOutput.PendingIndex<0)return source.NativeOutputSaved&&NativeOutputCurrent(source);
        return source.NativePendingSaved&&NativePendingCurrent(source)&&(source.NativeChild==null||source.NativeChild.Accepted&&source.NativeChild.AfterPublished);
    }
    private static bool TryNativeFinalDigest(Context source,out byte kind,out string digest)
    {
        kind=0;digest=null;if(!NativeFinalCurrent(source))return false;
        kind=(byte)(source.NativePending==null?1:2);string path=source.HeaderPath+(kind==1?".nativeOutputSaved":".nativePendingSaved");
        if(!Safe(path)||!File.Exists(path)||File.Exists(path+".tmp"))return false;
        digest=Digest(ReadNativeOutputMetadata(path,8192));return true;
    }
    private static bool ReleaseNativeFinal(Context source)
    {
        if(source==null)return false;
        string closed=source.HeaderPath+".closed",released=source.HeaderPath+".nativeReleased";
        if(!Safe(closed)||File.Exists(closed+".tmp")||!File.Exists(closed))return false;
        byte[] proof=System.Text.Encoding.ASCII.GetBytes(Digest(ReadNativeOutputMetadata(closed,4096)));
        bool already=File.Exists(released);
        if(already&&!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(released,proof))return false;
        if(already&&HasNativeStationAdoption(source.HeaderPath,Digest(source.Image),Digest(source.Header)))
        {if(source.NativeChild!=null){DisposeAdoptedRemoteRoot(source.NativeChild);source.NativeChild=null;}DisposeAdoptedRemoteRoot(source);return true;}
        if(!already)
        {
        if(source.NativeChild!=null)
        {
            var child=source.NativeChild;
            if(!source.NativePendingSaved||!child.Accepted||!child.AfterPublished||!NativePendingCurrent(source))return false;
            if(pending.TryGetValue(child.Operation,out var existing)&&!ReferenceEquals(existing,child))return false;
            pending[child.Operation]=child;
        }
        else if(!source.NativeMarkerReleased)
        {
            bool resumed=RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeReleaseInvoking",proof)&&
                source.Current()&&!RebirthProductiveEarnedCoordinator.TryNativeMarker(source.Station,out _)&&NativeFinalBodyAndSavedMetadata(source);
            if(!resumed&&(!NativeFinalCurrent(source)||!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.HeaderPath+".nativeReleaseInvoking",proof)||
                !RebirthProductiveEarnedCoordinator.ClearRemoteNativeFinal(source.Station,source.Artifact)))return false;
            source.NativeMarkerReleased=true;
        }
        if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(released,proof))return false;
        }
        source.NativeChild=null;
        RebirthProductiveEarnedSavedNativeSource.ReleaseRegisteredTerminal(source.Artifact.Birth,source.Artifact.Operation);
        RebirthProductiveEarnedSavedNativeSource.Release(source.Artifact.LoadedSource);
        source.Tools?.Dispose();source.Tools=null;
        source.Observation?.Dispose();source.Observation=null;source.BeforeWatch?.Dispose();source.BeforeWatch=null;source.BeforeAllocation?.Dispose();source.BeforeAllocation=null;
        source.NativeOutputWatch?.Dispose();source.NativeOutputWatch=null;source.NativePendingWatch?.Dispose();source.NativePendingWatch=null;
        source.NativeOutputRetention?.Dispose();source.NativeOutputRetention=null;source.NativePendingRetention?.Dispose();source.NativePendingRetention=null;
        source.Memory?.Dispose();source.Memory=null;return true;
    }
    private static bool NativeFinalBodyAndSavedMetadata(Context source)
    {
        using(var callbacks=source.Memory?.TryEnterCallbacks())
        {
            if(callbacks==null||source.NativeOutput==null)return false;
            if(source.NativeOutput.PendingIndex<0)return source.NativeOutputSaved&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.NativeOutput.Native,source.NativeOutput.Offset);
            return source.NativePending?.Child==null&&source.NativePending!=null&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativePendingSaved",System.Text.Encoding.ASCII.GetBytes(Digest(source.NativePendingRecord)))&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.NativePending.After,source.NativePending.AfterOffset);
        }
    }
}
