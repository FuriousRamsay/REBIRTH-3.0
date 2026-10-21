using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    // Called only after the native coordinator published its actual returned
    // output/queue/completion terminal. This closes remote metadata; it never
    // prepares, inserts, rewards, clears a marker or creates another original.
    internal static bool ObserveNativeInheritedCompletion(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact,
        byte[] body,int offset,RebirthProductiveEarnedObservationBudget memory=null)
    {
        if(File.Exists(artifact.OriginalPath+".remote-paid-parent.tmp"))return false;
        if(!File.Exists(artifact.OriginalPath+".remote-paid-parent"))return true;
        IDisposable workspace=null;
        try
        {
            using(var callbacks=memory?.TryEnterCallbacks()??RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())
            {
                if(callbacks==null||artifact.Current()!=true||!artifact.IsTarget(station)||
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,body,offset)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*(artifact.ImageLength+body.Length)+4L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes+65536),out workspace))return false;
                byte[] image=artifact.CopyImage();var frame=RebirthProductiveEarnedRowProof.Decode(image);
                if(!TryReadRemotePaidParent(artifact.OriginalPath,image,frame,station,out _,out var child))return false;
                if(!TryNativeInheritedCustody(child))return true; // This native original never transferred its remote hold.
                if(TryClosedEarly(child,out var completed)&&completed.Kind==1&&completed.OriginalDigest==Digest(image))return true;
                string header=child.Path.Substring(0,child.Path.Length-5)+".header";
                byte[] terminal=ReadNativeOutputMetadata(artifact.OriginalPath+".native-terminal",2*RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                using(var stream=new MemoryStream(terminal,false))using(var reader=new BinaryReader(stream))
                {
                    if(reader.ReadUInt32()!=0x31544552u||new Guid(reader.ReadBytes(16))!=frame.Birth||new Guid(reader.ReadBytes(16))!=frame.Operation||
                        !ReadInheritedTerminalBytes(reader,RebirthProductiveEarnedRowProof.MaximumArtifactBytes).SequenceEqual(image))return false;
                    int savedOffset=reader.ReadInt32();byte[] savedBody=ReadInheritedTerminalBytes(reader,RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                    if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(savedBody,savedOffset,body,offset))return false;
                    string path=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,1024),payload=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                    reader.ReadInt32();reader.ReadInt32();long savedAt=reader.ReadInt64();int length=reader.ReadInt32();string digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                    if(stream.Position!=stream.Length||!Safe(path)||!HashText(payload)||!HashText(digest)||savedAt<0||length<1)return false;
                }
                // This distinct small receipt is metadata-only. It deliberately
                // is not the NOS1 native output-plan financial qualifier.
                byte[] saved=Encoding.ASCII.GetBytes("nativeStationCompleted:"+Digest(terminal));
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(header+".nativeOutputSaved",saved))return false;
                var closed=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=frame.Operation,Birth=frame.Birth,Nonce=child.Nonce,
                    World=child.World,Owner=child.Owner,Creation=child.Creation,Deaths=child.Deaths,Position=child.Position,Kind=1,OriginalDigest=Digest(image),
                    HeaderDigest=Digest(Read(header)),HoldDigest=Digest(child.Bytes),TerminalDigest=Digest(saved)};
                byte[] bytes=RebirthProductiveEarnedRemoteClosedRecord.Encode(closed);
                return RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(header+".closed",bytes)&&
                    RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(header+".nativeReleased",Encoding.ASCII.GetBytes(Digest(bytes)));
            }
        }
        catch{return false;}
        finally{workspace?.Dispose();}
    }
    private static byte[] ReadInheritedTerminalBytes(BinaryReader reader,int maximum)
    {
        int length=reader.ReadInt32();
        if(length<1||length>maximum||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();
        return reader.ReadBytes(length);
    }
    internal static bool ObserveLoadedNativeInheritedCompletion(TileEntityWorkstation station,RebirthProductiveEarnedSavedNativeSource.Evidence source)
    {
        string original=Path.Combine(source.Directory,"RebirthProductiveEarned",source.Frame.Birth.ToString("N"),source.Frame.Operation.ToString("N")+".rew");
        if(!File.Exists(original+".remote-paid-parent")&&!File.Exists(original+".remote-paid-parent.tmp"))return true;
        if(!RebirthProductiveEarnedArtifact.TryReadRemoteObservedOriginal(station,source.Frame.Birth,source.Frame.Operation,out var artifact,out _))return false;
        return ObserveNativeInheritedCompletion(station,artifact,source.Native,source.NativeOffset);
    }
}
