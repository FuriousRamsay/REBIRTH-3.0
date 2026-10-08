using System;
using System.IO;
using System.Text;

// Packet-body codec only. Does not authenticate a server or grant owner custody.
public sealed class RebirthGearOfferFragment
{
    public string CreationId {get;private set;}
    public Guid TransactionId {get;private set;}
    public long ExpectedRevision {get;private set;}
    public int TotalBytes {get;private set;}
    public string Digest {get;private set;}
    public int Index {get;private set;}
    private byte[] payload;
    public byte[] CopyPayload() => (byte[])payload.Clone();
    public const int MaxFrameBytes=RebirthGearOfferAssembly.ChunkBytes+192;
    public static bool TryCreate(string creation,Guid transaction,long revision,int total,string digest,int index,byte[] bytes,out RebirthGearOfferFragment fragment)
    {
        fragment=null;
        if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||transaction==Guid.Empty||
            revision<0||revision==long.MaxValue||total<=0||total>RebirthGearOfferWireCodec.MaxBytes||
            digest==null||digest.Length!=64||index<0||index>=(total+16383)/16384||bytes==null||
            bytes.Length!=Math.Min(16384,total-index*16384))return false;
        foreach(char c in digest)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return false;
        fragment=new RebirthGearOfferFragment{CreationId=normalized,TransactionId=transaction,ExpectedRevision=revision,
            TotalBytes=total,Digest=digest,Index=index,payload=(byte[])bytes.Clone()};return true;
    }
    public byte[] Encode()
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8))
        {
            writer.Write((byte)1);writer.Write((byte)CreationId.Length);writer.Write(Encoding.ASCII.GetBytes(CreationId));
            writer.Write(TransactionId.ToByteArray());writer.Write(ExpectedRevision);writer.Write(TotalBytes);
            writer.Write(Encoding.ASCII.GetBytes(Digest));writer.Write((ushort)Index);writer.Write((ushort)payload.Length);writer.Write(payload);
            writer.Flush();return stream.ToArray();
        }
    }
    public static bool TryDecode(byte[] frame,out RebirthGearOfferFragment fragment)
    {
        fragment=null;if(frame==null||frame.Length==0||frame.Length>MaxFrameBytes)return false;
        try
        {
            using(var stream=new MemoryStream(frame,false))using(var reader=new BinaryReader(stream,Encoding.UTF8))
            {
                if(reader.ReadByte()!=1)return false;
                int size=reader.ReadByte();if(size!=32&&size!=71)return false;
                string creation=Encoding.ASCII.GetString(reader.ReadBytes(size));var idBytes=reader.ReadBytes(16);if(idBytes.Length!=16)return false;
                var transaction=new Guid(idBytes);long revision=reader.ReadInt64();int total=reader.ReadInt32();
                string digest=Encoding.ASCII.GetString(reader.ReadBytes(64));int index=reader.ReadUInt16(),length=reader.ReadUInt16();
                if(length<1||length>16384||stream.Length-stream.Position!=length)return false;
                var payload=reader.ReadBytes(length);
                return stream.Position==stream.Length&&TryCreate(creation,transaction,revision,total,digest,index,payload,out fragment);
            }
        }
        catch{return false;}
    }
}