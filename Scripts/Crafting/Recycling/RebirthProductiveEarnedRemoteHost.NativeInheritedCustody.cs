using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    private static bool PublishNativeInheritedCustody(Context parent,RebirthProductiveEarnedRowProof.Frame child,byte[] childImage,bool requireParentAdoption=true)
    {
        string header=Path.Combine(Path.GetDirectoryName(parent.HeaderPath),child.Operation.ToString("N")+".header");
        byte[] bytes=Read(header);var hold=ReadEarly(header.Substring(0,header.Length-7)+".hold");
        if(!TryReadHeader(bytes,parent.Owner,parent.Creation,parent.Deaths,parent.Station,child,Digest(childImage),out var nonce)||
            hold.Operation!=child.Operation||hold.Birth!=child.Birth||hold.Nonce!=nonce||hold.Owner!=parent.Owner||hold.Creation!=parent.Creation||
            hold.Deaths!=parent.Deaths||hold.World!=parent.WorldKey||!hold.Position.Equals(parent.Station.ToWorldPos()))return false;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(0x31494E52u);writer.Write(parent.HeaderPath);writer.Write(parent.Operation.ToByteArray());
            writer.Write(Digest(bytes));writer.Write(Digest(hold.Bytes));writer.Write(Digest(childImage));writer.Write(requireParentAdoption);writer.Flush();
            return RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(header+".nativeInheritedCustody",stream.ToArray());
        }
    }
    // Positive parent handoff retires the child's remote prepare hold only.
    // The child's native marker/original and native coordinator custody remain.
    private static bool TryNativeInheritedCustody(Early child)
    {
        try
        {
            string header=child.Path.Substring(0,child.Path.Length-5)+".header",proof=header+".nativeInheritedCustody";
            if(!File.Exists(proof))return false;
            byte[] bytes=ReadNativeOutputMetadata(proof,2048);
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadUInt32()!=0x31494E52u)return false;
                string parentHeader=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,1024);Guid operation=new Guid(reader.ReadBytes(16));
                if(operation==child.Operation||Path.GetDirectoryName(parentHeader)!=Path.GetDirectoryName(header)||
                    Path.GetFileName(parentHeader)!=operation.ToString("N")+".header"||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(Read(header))||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(child.Bytes))return false;
                string childDigest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                bool requireParentAdoption=reader.ReadBoolean();
                if(stream.Position!=stream.Length||!Manifest(child.Manifest,out _,out _,out _,out _,out var expected)||expected!=childDigest)return false;
                var parent=ReadEarly(parentHeader.Substring(0,parentHeader.Length-7)+".hold");
                if(parent.Operation!=operation||parent.Owner!=child.Owner||parent.Creation!=child.Creation||parent.Deaths!=child.Deaths||
                    parent.World!=child.World||!parent.Position.Equals(child.Position)||!TryClosedEarly(parent,out var closed)||closed.Kind!=2||
                    requireParentAdoption&&!HasNativeStationAdoption(parentHeader,closed.OriginalDigest,closed.HeaderDigest))return false;
                string path=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthProductiveEarned",parent.Birth.ToString("N"),operation.ToString("N")+".rew");
                byte[] link=ReadNativeOutputMetadata(path+".earned-child",164);
                if(link.Length!=164||!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(path+".earned-child.invoking",link))return false;
                using(var lineage=new BinaryReader(new MemoryStream(link,false)))
                    return lineage.ReadUInt32()==0x31434552u&&new Guid(lineage.ReadBytes(16))==parent.Birth&&new Guid(lineage.ReadBytes(16))==operation&&
                        new Guid(lineage.ReadBytes(16))==child.Birth&&new Guid(lineage.ReadBytes(16))==child.Operation&&
                        BitConverter.ToString(lineage.ReadBytes(32)).Replace("-","").ToLowerInvariant()==closed.OriginalDigest&&
                        BitConverter.ToString(lineage.ReadBytes(32)).Replace("-","").ToLowerInvariant()==childDigest;
            }
        }
        catch{return false;}
    }
}
