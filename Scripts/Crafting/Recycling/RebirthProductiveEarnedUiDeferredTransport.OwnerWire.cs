using System;
using System.IO;
using System.Threading;
using UnityEngine.Scripting;

// Purpose messages carry observations/status. The actual native owner upload,
// native Check/GiveExp and native station read remain separate mandatory events.
[Preserve]
public sealed class NetPackageRebirthUiDeferredOwnerV1:NetPackage,IMemoryPoolableObject
{
    internal const int Chunk=9000,Maximum=16*1024*1024;
    private static int readBytes;
    internal Guid Group;internal byte Action;internal int Player,Total,Index;internal string Digest;internal byte[] Part;
    private int charged;private IDisposable retention;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthUiDeferredOwnerV1 Setup(Guid group,byte action,int player,string digest,int total,int index,byte[] part,IDisposable lease)
    {Reset();Group=group;Action=action;Player=player;Digest=digest;Total=total;Index=index;Part=part;retention=lease;return this;}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(Group.ToByteArray());writer.Write(Action);writer.Write(Player);writer.Write(Digest);writer.Write(Total);writer.Write(Index);writer.Write(Part?.Length??0);if(Part!=null)writer.Write(Part);}
    public override void read(PooledBinaryReader reader)
    {
        Reset();
        try
        {
            if(reader.ReadByte()!=1)throw new InvalidDataException();var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();
            Group=new Guid(bytes);Action=reader.ReadByte();Player=reader.ReadInt32();Digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
            Total=reader.ReadInt32();Index=reader.ReadInt32();int length=reader.ReadInt32();
            if(Group==Guid.Empty||Action>8||Digest.Length!=64||!Hex(Digest))throw new InvalidDataException();
            if(Action==5&&(Total<1||Total>4096||Index!=0||length!=Total))throw new InvalidDataException();
            if((Action==7||Action==8)&&(Total!=32||Index!=0||length!=32))throw new InvalidDataException();
            bool frame=Action==0||Action==2||Action==5||Action==7||Action==8;
            if(frame?(Total<1||Total>Maximum||Index<0||Index>=(Total+Chunk-1)/Chunk||length!=Math.Min(Chunk,Total-Index*Chunk)):
                (Total!=0||Index!=0||length!=0))throw new InvalidDataException();
            if(length==0){Part=Array.Empty<byte>();return;}
            // Bounded worker queue input; retained assembled frames use the
            // original scope's combined measured account on the main thread.
            if(Interlocked.Add(ref readBytes,length)>8*1024*1024){Interlocked.Add(ref readBytes,-length);throw new InvalidDataException();}
            charged=length;Part=reader.ReadBytes(length);if(Part.Length!=length)throw new EndOfStreamException();
        }
        catch{Reset();throw;}
    }
    private static bool Hex(string value){foreach(char c in value)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return false;return true;}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world?.IsRemote()==false&&Sender!=null&&ValidEntityIdForSender(Player))RebirthProductiveEarnedUiDeferredTransport.ReceiveOwnerRequest(this,world);}
    public void Reset(){Group=Guid.Empty;Action=0;Player=Total=Index=0;Digest=null;Part=null;retention?.Dispose();retention=null;int bytes=Interlocked.Exchange(ref charged,0);if(bytes!=0)Interlocked.Add(ref readBytes,-bytes);}
    public void Cleanup()=>Reset();public static int GetPoolSize()=>16;
}

[Preserve]
public sealed class NetPackageRebirthUiDeferredOwnerReplyV1:NetPackage,IMemoryPoolableObject
{
    internal Guid Group;internal byte Status;internal string Digest;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthUiDeferredOwnerReplyV1 Setup(Guid group,byte status,string digest)
    {Reset();Group=group;Status=status;Digest=digest;return this;}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(Group.ToByteArray());writer.Write(Status);writer.Write(Digest);}
    public override void read(PooledBinaryReader reader)
    {
        Reset();if(reader.ReadByte()!=1)throw new InvalidDataException();var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();
        Group=new Guid(bytes);Status=reader.ReadByte();Digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
        if(Group==Guid.Empty||Status>9||Digest.Length!=64)throw new InvalidDataException();
        foreach(char c in Digest)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))throw new InvalidDataException();
    }
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthProductiveEarnedUiDeferredTransport.ReceiveOwnerReply(this,world);
    public void Reset(){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);Group=Guid.Empty;Status=0;Digest=null;}
    public void Cleanup()=>Reset();public static int GetPoolSize()=>16;
}
