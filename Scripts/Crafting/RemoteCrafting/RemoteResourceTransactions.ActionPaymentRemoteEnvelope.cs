using System;
using System.IO;
using System.Text;

public static partial class RemoteResourceTransactions
{
    private const uint ActionOwnerEnvelopeVersion=0x314F4145; // EAO1, purpose only
    private static long ActionRemoteEnvelopeRetainedBytes(string text,byte[] native)
    {
        if(text==null||native==null)throw new InvalidDataException();
        long length=checked(12L+new UTF8Encoding(false,true).GetByteCount(text)+native.Length);
        if(length<14||length>24L*1024*1024+12)throw new InvalidDataException();
        // Body owns one exact byte[] clone, hash string and fixed envelope
        // objects. Sender closures alias this Body; they own no second image.
        return checked(length+4096);
    }
    private static byte[] EncodeActionRemoteEnvelope(string originalText,byte[] native)
    {
        byte[] original=new UTF8Encoding(false,true).GetBytes(originalText??throw new InvalidDataException());
        if(original.Length<1||original.Length>8*1024*1024||native==null||native.Length<1||native.Length>16*1024*1024)throw new InvalidDataException("Electrical envelope exceeds bound");
        using(var stream=new MemoryStream(checked(12+original.Length+native.Length)))
        {using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(ActionOwnerEnvelopeVersion);writer.Write(original.Length);writer.Write(original);writer.Write(native.Length);writer.Write(native);writer.Flush();}return stream.ToArray();}
    }
    private static bool ActionEnvelopeFileEquals(string path,byte[] expected)
    {
        ActionPath(path);if(expected==null||expected.Length>24*1024*1024+12||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(input.Length!=expected.Length)return false;byte[] buffer=new byte[65536];int offset=0;
            while(offset<expected.Length){int count=input.Read(buffer,0,Math.Min(buffer.Length,expected.Length-offset));if(count<1)return false;for(int i=0;i<count;i++)if(buffer[i]!=expected[offset+i])return false;offset+=count;}
            return input.ReadByte()==-1;
        }
    }
    // Binary framing avoids inflating the full native PDS into XML/Base64. The
    // shared bounded fragment transport confers no native acceptance or payment.
    private static RebirthRemoteResourceFragments.Body PrepareActionRemoteRequestBody(ActionRemoteClick click)
    {
        if(click==null||!click.CourierCurrent()||!click.BeforeObserved)return null;
        if(click.RequestBody!=null)return NarrowActionRemoteClickMemory(click)?click.RequestBody:null;
        if(click.NativeBefore==null)return null;
        if(!SaveActionClientOriginal(click))return null;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return null;
        using(scratch)
        {
        byte[] original=new UTF8Encoding(false,true).GetBytes(click.Original);
        if(original.Length<1||original.Length>8*1024*1024||click.NativeBefore.Length<1||click.NativeBefore.Length>16*1024*1024)throw new InvalidDataException("Electrical owner request exceeds bound");
        using(var stream=new MemoryStream(checked(12+original.Length+click.NativeBefore.Length)))
        {
            using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {writer.Write(ActionOwnerEnvelopeVersion);writer.Write(original.Length);writer.Write(original);writer.Write(click.NativeBefore.Length);writer.Write(click.NativeBefore);writer.Flush();}
            click.RequestBody=new RebirthRemoteResourceFragments.Body(stream.ToArray());
            // The complete immutable body now owns this native Before image.
            // Keep its actual-event classification, not a duplicate PDS graph.
            click.NativeBefore=null;
            if(!NarrowActionRemoteClickMemory(click))throw new InvalidDataException("Electrical original measured retention exceeds available allocation");
            return click.RequestBody;
        }
        }
    }
    private static void ReadActionRemoteEnvelope(byte[] body,out string original,out byte[] native,bool retainNative=true)
    {
        original=null;native=null;
        if(body==null||body.Length<14||body.Length>24*1024*1024+12)throw new InvalidDataException("Electrical owner envelope exceeds bound");
        using(var stream=new MemoryStream(body,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true),true))
        {
            if(reader.ReadUInt32()!=ActionOwnerEnvelopeVersion)throw new InvalidDataException("Electrical owner purpose differs");
            int length=reader.ReadInt32();if(length<1||length>8*1024*1024||length>stream.Length-stream.Position-4)throw new InvalidDataException("Electrical original length invalid");
            original=new UTF8Encoding(false,true).GetString(reader.ReadBytes(length));
            int size=reader.ReadInt32();if(size<1||size>16*1024*1024||size!=stream.Length-stream.Position)throw new InvalidDataException("Electrical native original length invalid");
            // A host proposal never supplies an accepted owner native image.
            // Validate its complete framing without allocating an unused copy.
            if(retainNative){native=reader.ReadBytes(size);if(native.Length!=size)throw new EndOfStreamException();}
            else stream.Position=checked(stream.Position+size);
            if(stream.Position!=stream.Length)throw new EndOfStreamException();
        }
    }
}
