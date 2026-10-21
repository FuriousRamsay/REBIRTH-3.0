using System;using System.IO;using System.Linq;using System.Text;

// Compact positive native-original closure metadata. This record grants no
// native work, output, owner transfer, source restoration or marker recreation.
internal static class RebirthProductiveEarnedRemoteClosedRecord
{
    internal sealed class Description
    {
        internal Guid Operation,Birth,Nonce;internal string World,Owner,Creation,OriginalDigest,HeaderDigest,HoldDigest,TerminalDigest;
        internal int Deaths;internal Vector3i Position;internal byte Kind;
    }
    private static bool Hash(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    internal static bool Valid(Description value)=>value!=null&&value.Operation!=Guid.Empty&&value.Birth!=Guid.Empty&&value.Nonce!=Guid.Empty&&
        value.World?.Length>0&&value.World.Length<=44&&value.Deaths>=0&&value.Kind>=1&&value.Kind<=2&&
        RebirthOriginalActionScope.Validate(value.Owner,value.Creation)&&Hash(value.Owner)&&value.Creation.Length<=512&&
        Hash(value.OriginalDigest)&&Hash(value.HeaderDigest)&&Hash(value.HoldDigest)&&Hash(value.TerminalDigest);
    internal static byte[] Encode(Description value)
    {
        if(!Valid(value))throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
        {
            writer.Write(0x31434E52u);writer.Write(value.Operation.ToByteArray());writer.Write(value.Birth.ToByteArray());writer.Write(value.Nonce.ToByteArray());
            writer.Write(value.World);writer.Write(value.Owner);writer.Write(value.Creation);writer.Write(value.Deaths);
            writer.Write(value.Position.x);writer.Write(value.Position.y);writer.Write(value.Position.z);writer.Write(value.Kind);
            writer.Write(value.OriginalDigest);writer.Write(value.HeaderDigest);writer.Write(value.HoldDigest);writer.Write(value.TerminalDigest);writer.Flush();
            if(stream.Length>4096-32)throw new InvalidDataException();
            using(var hash=System.Security.Cryptography.SHA256.Create())writer.Write(hash.ComputeHash(stream.ToArray()));writer.Flush();return stream.ToArray();
        }
    }
    internal static bool TryRead(byte[] bytes,out Description value)
    {
        value=null;
        try
        {
            if(bytes==null||bytes.Length<256||bytes.Length>4096)return false;
            using(var hash=System.Security.Cryptography.SHA256.Create())if(!hash.ComputeHash(bytes,0,bytes.Length-32).SequenceEqual(bytes.Skip(bytes.Length-32)))return false;
            using(var stream=new MemoryStream(bytes,0,bytes.Length-32,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadUInt32()!=0x31434E52u)return false;
                var item=new Description{Operation=new Guid(reader.ReadBytes(16)),Birth=new Guid(reader.ReadBytes(16)),Nonce=new Guid(reader.ReadBytes(16)),
                    World=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,44),Owner=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64),
                    Creation=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512),Deaths=reader.ReadInt32(),
                    Position=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32()),Kind=reader.ReadByte(),
                    OriginalDigest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64),HeaderDigest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64),
                    HoldDigest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64),TerminalDigest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)};
                if(stream.Position!=stream.Length||!Valid(item)||!Encode(item).SequenceEqual(bytes))return false;value=item;return true;
            }
        }
        catch{value=null;return false;}
    }
}
