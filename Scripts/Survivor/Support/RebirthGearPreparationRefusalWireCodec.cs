using System;
using System.IO;
using System.Text;

// Data transport only. Decoding cannot authorize retirement of an owner request.
public static class RebirthGearPreparationRefusalWireCodec
{
    public const int MaximumBytes=12+RebirthGearPreparationMarker.MaximumKeyLength;
    public static bool TryEncode(RebirthGearPreparationRefusal refusal,out byte[] bytes)
    {
        bytes=null;
        try
        {
            if(refusal==null||!RebirthGearPreparationRefusal.TryCreateStale(refusal.OriginalMarker,refusal.ObservedRevision,out var validated)||
                validated.RequestDigest!=refusal.RequestDigest)return false;
            string marker=refusal.OriginalMarker;
            for(int i=0;i<marker.Length;i++)if(marker[i]>127)return false;
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream,Encoding.ASCII,true))
            {
                writer.Write((byte)1);writer.Write((byte)1); // schema / stale revision reason
                writer.Write(refusal.ObservedRevision);writer.Write((ushort)marker.Length);
                writer.Write(Encoding.ASCII.GetBytes(marker));writer.Flush();
                if(stream.Length>MaximumBytes)return false;
                bytes=stream.ToArray();return true;
            }
        }
        catch{return false;}
    }
    public static bool TryDecode(byte[] bytes,out RebirthGearPreparationRefusal refusal)
    {
        refusal=null;
        try
        {
            if(bytes==null||bytes.Length<13||bytes.Length>MaximumBytes)return false;
            using(var stream=new MemoryStream(bytes,false))
            using(var reader=new BinaryReader(stream,Encoding.ASCII,true))
            {
                if(reader.ReadByte()!=1||reader.ReadByte()!=1)return false;
                long observed=reader.ReadInt64();int length=reader.ReadUInt16();
                if(length<1||length>RebirthGearPreparationMarker.MaximumKeyLength||stream.Length-stream.Position!=length)return false;
                var marker=reader.ReadBytes(length);
                for(int i=0;i<marker.Length;i++)if(marker[i]>127)return false;
                return RebirthGearPreparationRefusal.TryCreateStale(Encoding.ASCII.GetString(marker),observed,out refusal);
            }
        }
        catch{return false;}
    }
}