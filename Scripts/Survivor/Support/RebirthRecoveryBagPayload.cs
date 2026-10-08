using System;
using System.Globalization;
using System.IO;
using System.Linq;

// Exact conserved stack byte witness; no native ItemValue parsing or entity callbacks.
public static class RebirthRecoveryBagPayload
{
    public static bool Matches(byte[] payload,RebirthRecoveryEntityRecord record,RebirthGearTransferState pending,Guid publicationId)
    {
        if(payload==null||record==null||pending==null||record.BagLength<=0||record.BagLength>4*1024*1024||record.BagOffset<0||record.BagOffset>payload.Length-record.BagLength||
            !pending.TryGetPlan(out var plan)||!pending.TryGetRecoveryManifest(out var manifest))return false;
        var publication=manifest.ToXml().Elements().SingleOrDefault(e=>(string)e.Attribute("id")==publicationId.ToString("N"));if(publication==null)return false;
        var entries=publication.Elements().Select(e=>int.Parse((string)e.Attribute("index"),CultureInfo.InvariantCulture)).ToArray();
        try{using(var stream=new MemoryStream(payload,record.BagOffset,record.BagLength,false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadByte()!=2||reader.ReadUInt16()!=2)return false;
            int x=reader.ReadInt32(),y=reader.ReadInt32(),slots=reader.ReadInt16();
            if(x<=0||y<=0||slots<=0||slots>4096||(long)x*y!=slots||slots<entries.Length||((string)publication.Attribute("kind")=="item"&&slots!=1))return false;
            for(int i=0;i<slots;i++)
            {
                int count=reader.ReadUInt16();if(i>=entries.Length){if(count!=0)return false;continue;}
                var expected=plan.Recovery[entries[i]].Item;if(count!=expected.Count)return false;
                var bytes=Convert.FromBase64String(expected.ItemData);if(stream.Position+bytes.Length>stream.Length)return false;
                int start=record.BagOffset+checked((int)stream.Position);for(int j=0;j<bytes.Length;j++)if(payload[start+j]!=bytes[j])return false;
                stream.Position+=bytes.Length;
            }
            if(Flag(reader)){int bits=Length(reader);if(bits!=slots)return false;Skip(stream,(bits+7)/8);}
            // Initial recovery bags have empty preference snapshots. Nonempty snapshots
            // require additional bounded native array framing before accepting a receipt.
            if(Flag(reader)){reader.ReadInt32();if(Flag(reader)||Flag(reader)||Flag(reader))return false;}
            reader.ReadUInt64();Flag(reader);return stream.Position==stream.Length;
        }}catch{return false;}
    }
    private static bool Flag(BinaryReader reader){byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value!=0;}
    private static int Length(BinaryReader reader){uint value=0;for(int i=0;i<5;i++){byte b=reader.ReadByte();if(i==4&&(b&240)!=0)throw new InvalidDataException();value|=(uint)(b&127)<<(7*i);if((b&128)==0)return checked((int)value);}throw new InvalidDataException();}
    private static void Skip(Stream stream,int count){if(count<0||stream.Position+count>stream.Length)throw new EndOfStreamException();stream.Position+=count;}
}