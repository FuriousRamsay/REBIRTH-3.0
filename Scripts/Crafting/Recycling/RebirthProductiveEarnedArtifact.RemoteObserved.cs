using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static partial class RebirthProductiveEarnedArtifact
{
    // Binds immutable originals to an actually installed native saved/load
    // observation. Historical Birth is never substituted for current custody.
    // This method creates neither a native incarnation nor a load witness.
    internal static bool TryReadRemoteObservedOriginal(TileEntityWorkstation station,Guid birth,Guid operation,
        out Prepared prepared,out byte[] image)
    {
        prepared=null;image=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||birth==Guid.Empty||operation==Guid.Empty||
                !RebirthProductiveEarnedSavedNativeSource.TryObserved(station,out var source)||!source.PrimaryCurrent()||
                !source.CurrentIdentity(world,station,source.Frame.Birth,source.Frame.Operation)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset)||
                !RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var incarnation)||
                !Guid.TryParseExact(incarnation,"N",out var custody)||custody==Guid.Empty)return false;
            string save=Path.GetFullPath(GameIO.GetSaveGameDir());
            string path=Path.Combine(save,"RebirthProductiveEarned",birth.ToString("N"),operation.ToString("N")+".rew");
            if(!SafePath(path)||File.Exists(path+".tmp"))return false;
            byte[] original=ReadBounded(path);var frame=RebirthProductiveEarnedRowProof.Decode(original);
            if(frame.Birth!=birth||frame.Operation!=operation||frame.World!=WorldIdentity(world)||
                !RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out _,out _,out _,out _))return false;
            byte[] link=null;string linkPath=path+".earned-child";
            if(source.Frame.Birth==birth&&source.Frame.Operation==operation)
            {if(!source.Original.SequenceEqual(original))return false;}
            else
            {
                // REC1 is an immutable paid-parent lineage, not a new native
                // grant. Both the prepared and entered records must agree.
                if(!SafePath(linkPath)||File.Exists(linkPath+".tmp")||new FileInfo(linkPath).Length!=164)return false;
                link=File.ReadAllBytes(linkPath);
                using(var stream=new MemoryStream(link,false))using(var reader=new BinaryReader(stream))using(var sha=SHA256.Create())
                {
                    if(reader.ReadUInt32()!=0x31434552u||new Guid(reader.ReadBytes(16))!=birth||new Guid(reader.ReadBytes(16))!=operation||
                        new Guid(reader.ReadBytes(16))!=source.Frame.Birth||new Guid(reader.ReadBytes(16))!=source.Frame.Operation||
                        !reader.ReadBytes(32).SequenceEqual(sha.ComputeHash(original))||
                        !reader.ReadBytes(32).SequenceEqual(sha.ComputeHash(source.Original))||
                        !reader.ReadBytes(32).SequenceEqual(RebirthProductiveEarnedNativeBody.ComparisonDigest(source.Frame.BeforeNative,source.Frame.BeforeElapsedOffset))||
                        stream.Position!=stream.Length)return false;
                }
                if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(linkPath+".invoking",link))return false;
            }
            source.CustodyBirth=custody;
            byte[] immutableLink=link;
            var result=new Prepared(world,station,save,path,original,birth,operation,custody);
            result.observedRemoteIdentity=()=>source.PrimaryCurrent()&&
                source.CurrentIdentity(world,station,source.Frame.Birth,source.Frame.Operation)&&
                NativeCurrent(world,station,custody)&&
                (immutableLink==null||RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(linkPath,immutableLink)&&
                    RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(linkPath+".invoking",immutableLink));
            if(!result.Current())return false;
            prepared=result;image=original;return true;
        }
        catch{prepared=null;image=null;return false;}
    }
}
