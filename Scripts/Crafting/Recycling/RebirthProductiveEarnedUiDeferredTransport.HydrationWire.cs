using System;
using System.IO;
using System.Threading;
using UnityEngine.Scripting;

// A selected native hydration is fragmented only for transport. Its actual
// unchanged native packet is processed ONCE after the authenticated group has
// been completely received on the same native connection.
[Preserve]
public sealed class NetPackageRebirthUiDeferredHydrationV1:NetPackage,IMemoryPoolableObject
{
    // Existing remote-resource transport ceiling, shared by the metadata and
    // native components of ONE selected group. Native envelope keeps its cap.
    internal const int Chunk=9000,Maximum=RebirthRemoteResourceFragments.MaximumBody,NativeEnvelopeMaximum=5*1024*1024;
    private static int readBytes;
    internal Guid Group;internal int Total,Count,Index;internal string Digest;internal byte[] Part;
    internal byte Component;
    internal object Original;
    private int chargedRead;private IDisposable retained;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthUiDeferredHydrationV1 Setup(Guid group,int total,int count,int index,string digest,byte[] part,object original,IDisposable lease)
    {Reset();Group=group;Total=total;Count=count;Index=index;Digest=digest;Part=part;Original=original;retained=lease;return this;}
    internal NetPackageRebirthUiDeferredHydrationV1 AsMetadata(){Component=1;return this;}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)2);writer.Write(Component);writer.Write(Group.ToByteArray());writer.Write(Total);writer.Write(Count);writer.Write(Index);writer.Write(Digest);writer.Write(Part.Length);writer.Write(Part);}
    public override void read(PooledBinaryReader reader)
    {
        Reset();RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);
        try
        {
            byte version=reader.ReadByte();if(version!=1&&version!=2)throw new InvalidDataException();Component=version==2?reader.ReadByte():(byte)0;
            if(Component>1)throw new InvalidDataException();var group=reader.ReadBytes(16);if(group.Length!=16)throw new EndOfStreamException();Group=new Guid(group);
            Total=reader.ReadInt32();Count=reader.ReadInt32();Index=reader.ReadInt32();Digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);int length=reader.ReadInt32();
            if(Group==Guid.Empty||Total<1||Total>(Component==0?NativeEnvelopeMaximum:Maximum)||Count!=(Total+Chunk-1)/Chunk||Index<0||Index>=Count||
                Digest.Length!=64||length!=Math.Min(Chunk,Total-Index*Chunk))throw new InvalidDataException();
            foreach(char c in Digest)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))throw new InvalidDataException();
            // Explicit process-wide worker input cap: at most twenty-four MiB of
            // detached purpose fragments, including packages awaiting dequeue.
            if(Interlocked.Add(ref readBytes,length)>24*1024*1024){Interlocked.Add(ref readBytes,-length);throw new InvalidDataException();}
            chargedRead=length;Part=reader.ReadBytes(length);if(Part.Length!=length)throw new EndOfStreamException();
        }
        catch{Reset();throw;}
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {RebirthProductiveEarnedUiDeferredTransport.ReceiveHydration(this,world,callbacks);}
    public void Reset()
    {
        RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);
        Group=Guid.Empty;Component=0;Total=Count=Index=0;Digest=null;Part=null;Original=null;
        retained?.Dispose();retained=null;int charge=Interlocked.Exchange(ref chargedRead,0);if(charge!=0)Interlocked.Add(ref readBytes,-charge);
    }
    public void Cleanup()=>Reset();
    public static int GetPoolSize()=>16;
}

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static bool IsHydrationEnvelope(NetPackage packet)=>packet is NetPackageRebirthUiDeferredHydrationV1||packet is NetPackageRebirthUiDeferredOwnerReplyV1;
}
