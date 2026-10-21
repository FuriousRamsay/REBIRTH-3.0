using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

// Saved native pending-work identity for a different tool payer. This carries
// the old earned original unchanged; it grants neither another output nor XP.
internal static class RebirthProductiveEarnedViewerToolSource
{
    internal static byte[] Capture(RebirthProductiveEarnedCoordinator.SavedPendingToolsObservation source)
    {
        if(source?.CurrentContext()!=true)throw new InvalidDataException();
        byte[] image=source.CopyOriginalImage(),native=source.CopyNativeBefore();
        byte[] terminal=Read(source.OriginalPath+".native-terminal",2*RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(0x31535456u);writer.Write(Hash(image));writer.Write(Hash(terminal));
            writer.Write(source.Offset);writer.Write(native.Length);writer.Write(native);
            writer.Write(source.Index);writer.Write(source.BeforeTimer);writer.Write(source.TargetTimer);writer.Write(source.Binding);writer.Write(source.Extras);
            writer.Flush();if(stream.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)throw new InvalidDataException();return stream.ToArray();
        }
    }
    internal static bool TryRead(string original,byte[] image,byte[] record,Guid nonce,out RebirthProductiveEarnedNativeOutputPlan.Description paid,bool nativeHistory=true)
    {
        paid=null;
        try
        {
            if(record==null||record.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||nonce==Guid.Empty)return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(image);
            using(var stream=new MemoryStream(record,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadUInt32()!=0x31535456u||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Hash(image))return false;
                byte[] terminal=nativeHistory?Read(original+".native-terminal",2*RebirthProductiveEarnedRowProof.MaximumArtifactBytes):null;
                string terminalDigest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                if(terminalDigest.Length!=64||nativeHistory&&terminalDigest!=Hash(terminal))return false;
                int offset=reader.ReadInt32();byte[] native=Blob(reader,RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                int index=reader.ReadInt32();float before=reader.ReadSingle(),target=reader.ReadSingle();
                string binding=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes),
                    extras=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
                if(stream.Position!=stream.Length||!RebirthProductiveEarnedNativeBody.TryRead(native,offset,out var body,out _)||index!=body.Queue.Length-1||index<0||
                    !Finite(before)||!Finite(target)||target>=before||body.Queue[index]?.Recipe==null||body.Queue[index].Multiplier<=0||body.Queue[index].CraftingTimeLeft!=before||
                    RebirthProductiveEarnedUiQueueOwner.Binding(body.Queue[index])!=binding||RebirthRecipeOutputs.DefinitionIdentity(body.Queue[index].Recipe)!=extras)return false;
                if(nativeHistory)using(var savedStream=new MemoryStream(terminal,false))using(var saved=new BinaryReader(savedStream))
                {
                    if(saved.ReadUInt32()!=0x31544552u||new Guid(saved.ReadBytes(16))!=frame.Birth||new Guid(saved.ReadBytes(16))!=frame.Operation||
                        !Blob(saved,RebirthProductiveEarnedRowProof.MaximumArtifactBytes).SequenceEqual(image))return false;
                    int savedOffset=saved.ReadInt32();byte[] savedNative=Blob(saved,RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                    if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(savedNative,savedOffset,native,offset))return false;
                    string path=RebirthSurvivorNetworkCodec.ReadBoundedString(saved,1024),payload=RebirthSurvivorNetworkCodec.ReadBoundedString(saved,64);
                    saved.ReadInt32();saved.ReadInt32();long tileOffset=saved.ReadInt64();int length=saved.ReadInt32();string digest=RebirthSurvivorNetworkCodec.ReadBoundedString(saved,64);
                    if(savedStream.Position!=savedStream.Length||!RebirthProductiveEarnedArtifact.SafePath(path)||payload.Length!=64||digest.Length!=64||tileOffset<0||length<1)return false;
                }
                paid=new RebirthProductiveEarnedNativeOutputPlan.Description{Operation=frame.Operation,Birth=frame.Birth,Nonce=nonce,OriginalDigest=Hash(image),Native=native,Offset=offset,
                    PendingIndex=index,PendingBefore=before,PendingTarget=target,Binding=binding,Extras=extras};return true;
            }
        }
        catch{return false;}
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    private static byte[] Blob(BinaryReader reader,int maximum)
    {int length=reader.ReadInt32();if(length<1||length>maximum||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();return reader.ReadBytes(length);}
    private static byte[] Read(string path,int maximum)
    {
        if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))throw new InvalidDataException();
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(file.Length<1||file.Length>maximum)throw new InvalidDataException();var result=new byte[(int)file.Length];int at=0;while(at<result.Length){int n=file.Read(result,at,result.Length-at);if(n<1)throw new EndOfStreamException();at+=n;}return result;}
    }
    private static string Hash(byte[] image){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(image)).Replace("-","").ToLowerInvariant();}
}
