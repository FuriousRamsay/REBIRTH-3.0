using System;
using System.IO;

// Detached framing only. Envelope identities and hashes confer no native transport authority.
internal static class RebirthPurgeProjectionFragmentCodec
{
    public const int PayloadLimit=16384, HeaderBytes=125, MessageLimit=HeaderBytes+PayloadLimit;
    public const int AggregateLimit=67108864, BlobLimit=16777216, ManifestLimit=8388608, BlobCountLimit=4097;
    internal enum Kind : byte { Begin=1, Fragment=2, Fence=3, Invalidate=4 }
    internal sealed class Frame
    {
        public readonly Kind Type;
        public readonly Guid World,Snapshot;
        public readonly long Epoch,Generation,Sequence,Revision;
        public readonly int Blob,Blobs,Aggregate,BlobLength,Offset;
        private readonly byte[] hash,payload;
        public byte[] Hash { get { return (byte[])hash.Clone(); } }
        public byte[] Payload { get { return (byte[])payload.Clone(); } }
        public int PayloadLength { get { return payload.Length; } }
        public Frame(Kind type,Guid world,Guid snapshot,long epoch,long generation,long sequence,long revision,int blob,int blobs,int aggregate,int blobLength,int offset,byte[] hash,byte[] payload)
        {
            if(hash==null || hash.Length!=32 || payload==null || payload.Length>PayloadLimit) throw new ArgumentException("Invalid projection payload.");
            Type=type;World=world;Snapshot=snapshot;Epoch=epoch;Generation=generation;Sequence=sequence;Revision=revision;
            Blob=blob;Blobs=blobs;Aggregate=aggregate;BlobLength=blobLength;Offset=offset;
            this.hash=hash==null?null:(byte[])hash.Clone();this.payload=payload==null?null:(byte[])payload.Clone();
            if(!Valid(this)) throw new ArgumentException("Invalid projection frame.");
        }
    }
    private static bool Valid(Frame f)
    {
        if(f==null || f.World==Guid.Empty || f.Snapshot==Guid.Empty || f.Epoch<0 || f.Generation<0 || f.Sequence<1 || f.Revision<0 ||
            f.Blobs<1 || f.Blobs>BlobCountLimit || f.Aggregate<1 || f.Aggregate>AggregateLimit || f.Hash==null || f.Hash.Length!=32 || f.Payload==null || f.Payload.Length>PayloadLimit) return false;
        if(f.Type==Kind.Fragment) return f.Blob>=0 && f.Blob<f.Blobs && f.BlobLength>0 && f.BlobLength<=(f.Blob==0?ManifestLimit:BlobLimit) && f.Offset>=0 &&
            f.Offset%PayloadLimit==0 && f.Offset<f.BlobLength && f.Payload.Length==Math.Min(PayloadLimit,f.BlobLength-f.Offset);
        return (f.Type==Kind.Begin || f.Type==Kind.Fence || f.Type==Kind.Invalidate) && f.Blob==0 && f.BlobLength==0 && f.Offset==0 && f.Payload.Length==0;
    }
    public static byte[] Encode(Frame f)
    {
        if(!Valid(f)) throw new ArgumentException("Invalid projection frame.");
        using(var s=new MemoryStream()) using(var w=new BinaryWriter(s))
        {
            w.Write(0x32525052);w.Write((byte)f.Type);w.Write(f.World.ToByteArray());w.Write(f.Snapshot.ToByteArray());
            w.Write(f.Epoch);w.Write(f.Generation);w.Write(f.Sequence);w.Write(f.Revision);
            w.Write(f.Blob);w.Write(f.Blobs);w.Write(f.Aggregate);w.Write(f.BlobLength);w.Write(f.Offset);w.Write(f.Payload.Length);w.Write(f.Hash);w.Write(f.Payload);
            return s.ToArray();
        }
    }
    public static bool TryDecode(byte[] bytes,out Frame frame)
    {
        frame=null;
        if(bytes==null || bytes.Length<HeaderBytes || bytes.Length>MessageLimit) return false;
        try
        {
            using(var s=new MemoryStream(bytes,false)) using(var r=new BinaryReader(s))
            {
                if(r.ReadInt32()!=0x32525052) return false;
                var kind=(Kind)r.ReadByte();var world=new Guid(r.ReadBytes(16));var snapshot=new Guid(r.ReadBytes(16));
                long epoch=r.ReadInt64(),generation=r.ReadInt64(),sequence=r.ReadInt64(),revision=r.ReadInt64();
                int blob=r.ReadInt32(),blobs=r.ReadInt32(),aggregate=r.ReadInt32(),length=r.ReadInt32(),offset=r.ReadInt32(),count=r.ReadInt32();
                if(count<0 || count>PayloadLimit || count!=bytes.Length-HeaderBytes) return false;
                frame=new Frame(kind,world,snapshot,epoch,generation,sequence,revision,blob,blobs,aggregate,length,offset,r.ReadBytes(32),r.ReadBytes(count));
                return s.Position==s.Length;
            }
        }
        catch(ArgumentException) { return false; } catch(IOException) { return false; }
    }
}


