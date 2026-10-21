using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal const int MaximumInitialNativeSuffix=RebirthProductiveEarnedNativeBody.MaximumBodyBytes+128;
    internal static bool TryReadInitialNativeSuffix(string originalPath,byte[] originalImage,out byte[] body,out int offset)
    {
        body=null;offset=-1;
        try
        {
            string path=originalPath+".initial-native-suffix";
            if(originalImage==null||!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return false;
            byte[] record;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<48||stream.Length>MaximumInitialNativeSuffix)return false;
                record=new byte[(int)stream.Length];int read=0;
                while(read<record.Length){int n=stream.Read(record,read,record.Length-read);if(n<1)return false;read+=n;}
                if(stream.ReadByte()!=-1)return false;
            }
            using(var stream=new MemoryStream(record,false))using(var reader=new BinaryReader(stream))using(var sha=SHA256.Create())
            {
                if(reader.ReadUInt32()!=0x31534552u||!reader.ReadBytes(32).SequenceEqual(sha.ComputeHash(originalImage)))return false;
                offset=reader.ReadInt32();int length=reader.ReadInt32();
                if(length<1||length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||length!=stream.Length-stream.Position)return false;
                body=reader.ReadBytes(length);
                if(!RebirthProductiveEarnedNativeBody.TryRead(body,offset,out _,out _)||
                    !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(path,record,MaximumInitialNativeSuffix))return false;
            }
            return true;
        }
        catch{body=null;offset=-1;return false;}
    }
    internal static bool EffectiveNativeAfter(Attempt original,out byte[] body,out int offset)
    {
        body=null;offset=-1;if(original?.Artifact==null||original.Frame==null)return false;
        string path=original.Artifact.OriginalPath+".initial-native-suffix";
        if(original.NativeSuffixRequired||File.Exists(path)||File.Exists(path+".tmp"))
            return TryReadInitialNativeSuffix(original.Artifact.OriginalPath,original.Artifact.CopyImage(),out body,out offset);
        body=original.Frame.AfterNative;offset=original.Frame.AfterElapsedOffset;return body!=null;
    }
    internal static bool IsInitialNativeSuffixPublication(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
    {
        var scope=nativeTerminalOuterTick;
        return scope!=null&&!scope.EarnedAmbiguous&&ReferenceEquals(scope.Station,station)&&ReferenceEquals(scope.World,GameManager.Instance?.World)&&
            scope.EarnedInvocation!=null&&ReferenceEquals(scope.Earned?.Artifact,artifact)&&scope.Earned.NativeSuffixRequired&&
            retained.TryGetValue(station,out var original)&&ReferenceEquals(original,scope.Earned)&&artifact.MatchesAfter(station);
    }
    internal static bool PublicationNativeAfter(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact,
        RebirthProductiveEarnedRowProof.Frame frame,out byte[] body,out int offset)
    {
        if(retained.TryGetValue(station,out var original)&&ReferenceEquals(original.Artifact,artifact))
            return EffectiveNativeAfter(original,out body,out offset);
        string path=artifact.OriginalPath+".initial-native-suffix";
        if(File.Exists(path)||File.Exists(path+".tmp"))return TryReadInitialNativeSuffix(artifact.OriginalPath,artifact.CopyImage(),out body,out offset);
        body=frame.AfterNative;offset=frame.AfterElapsedOffset;return body!=null;
    }
    private static void ObserveInitialNativeSuffixReturned(TileEntityWorkstation station,bool ran,object state)
    {
        if(!ran||state is not NativeTerminalOuterTick scope||!ReferenceEquals(nativeTerminalOuterTick,scope)||scope.EarnedAmbiguous||
            scope.EarnedInvocation==null||!ReferenceEquals(scope.Station,station)||!ReferenceEquals(scope.World,GameManager.Instance?.World)||
            !retained.TryGetValue(station,out var original)||!ReferenceEquals(original,scope.Earned)||original.OutputTimerObserved||
            original.Artifact?.MatchesAfter(station)!=true)return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out var body,out int offset))return;
            byte[] record;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(0x31534552u);writer.Write(original.Artifact.OriginalDigest());writer.Write(offset);writer.Write(body.Length);writer.Write(body);
                writer.Flush();record=stream.ToArray();
            }
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(original.Artifact.OriginalPath+".initial-native-suffix",record,MaximumInitialNativeSuffix)||
                !original.Artifact.MatchesAfter(station)||!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,body,offset))return;
            original.Observation?.Dispose();original.Observation=null;
            original.Observation=RebirthProductiveEarnedPublication.BeginAfterNativeWork(station,original.Artifact);
        }
    }
}
