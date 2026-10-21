using System;
using System.IO;
using System.Threading;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthCompletionNativeV1:NetPackage,IMemoryPoolableObject
{
    internal const int Maximum=5*1024*1024,Chunk=9000;
    internal Guid Dispatch;internal int Total,Count,Index;internal string Digest;internal byte[] Part;internal object Original;
    private IDisposable lease;private int input;private static int allInput;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthCompletionNativeV1 Setup(Guid dispatch,int total,int count,int index,string digest,byte[] part,object original,IDisposable retention)
    {Reset();Dispatch=dispatch;Total=total;Count=count;Index=index;Digest=digest;Part=part;Original=original;lease=retention;return this;}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(Dispatch.ToByteArray());writer.Write(Total);writer.Write(Count);writer.Write(Index);writer.Write(Digest);writer.Write(Part.Length);writer.Write(Part);}
    public override void read(PooledBinaryReader reader)
    {
        Reset();
        try
        {
            if(reader.ReadByte()!=1)throw new InvalidDataException();var id=reader.ReadBytes(16);if(id.Length!=16)throw new EndOfStreamException();Dispatch=new Guid(id);
            Total=reader.ReadInt32();Count=reader.ReadInt32();Index=reader.ReadInt32();Digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);int length=reader.ReadInt32();
            if(Dispatch==Guid.Empty||Total<1||Total>Maximum||Count!=(Total+Chunk-1)/Chunk||Index<0||Index>=Count||Digest.Length!=64||
                length!=Math.Min(Chunk,Total-Index*Chunk))throw new InvalidDataException();
            foreach(char c in Digest)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))throw new InvalidDataException();
            if(Interlocked.Add(ref allInput,length)>24*1024*1024){Interlocked.Add(ref allInput,-length);throw new InvalidDataException();}
            input=length;Part=reader.ReadBytes(length);if(Part.Length!=length)throw new EndOfStreamException();
        }
        catch{Reset();throw;}
    }
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthWorkstationCompletionPipeline.ReceiveRemoteNativeUpload(this,world,callbacks);
    public void Reset(){Dispatch=Guid.Empty;Total=Count=Index=0;Digest=null;Part=null;Original=null;lease?.Dispose();lease=null;
        int bytes=Interlocked.Exchange(ref input,0);if(bytes!=0)Interlocked.Add(ref allInput,-bytes);}
    public void Cleanup()=>Reset();public static int GetPoolSize()=>16;
}
