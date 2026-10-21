using System;
using System.IO;
using System.Threading;
using UnityEngine.Scripting;

internal static class RebirthRecyclerPlacedWireBudget
{
    private static int bytes;
    internal static void Reserve(ref int charged,int count)
    {if(Interlocked.Add(ref bytes,count)>32*1024*1024){Interlocked.Add(ref bytes,-count);throw new InvalidDataException();}charged=count;}
    internal static void Release(ref int charged){int count=Interlocked.Exchange(ref charged,0);if(count!=0)Interlocked.Add(ref bytes,-count);}
}
[Preserve]
public sealed class NetPackageRebirthRecyclerPlacedRequestV1:NetPackage,IMemoryPoolableObject
{
    internal Guid Group;internal int Total,Index,Count;internal byte[] Payload;private int charged;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthRecyclerPlacedRequestV1 Setup(Guid group,int total,int index,int count,byte[] payload)
    {
        Reset();
        if(group==Guid.Empty||total<1||total>25*1024*1024||count!=(total+8999)/9000||index<0||index>=count||payload?.Length!=Math.Min(9000,total-index*9000))throw new InvalidDataException();
        RebirthRecyclerPlacedWireBudget.Reserve(ref charged,payload.Length);
        Group=group;Total=total;Index=index;Count=count;Payload=payload;return this;
    }
    // Reserve before allocating an outbound fragment; the charge remains owned
    // by the actual pooled packet until native Reset/Cleanup after all queues.
    internal NetPackageRebirthRecyclerPlacedRequestV1 SetupSlice(Guid group,byte[] source,int index)
    {
        Reset();
        if(source==null||source.Length<1||source.Length>25*1024*1024||group==Guid.Empty||index<0||index>=(source.Length+8999)/9000)throw new InvalidDataException();
        int length=Math.Min(9000,source.Length-index*9000);
        RebirthRecyclerPlacedWireBudget.Reserve(ref charged,length);
        try
        {
            Payload=new byte[length];Buffer.BlockCopy(source,index*9000,Payload,0,length);
            Group=group;Total=source.Length;Index=index;Count=(source.Length+8999)/9000;return this;
        }
        catch{Reset();throw;}
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(Group.ToByteArray());writer.Write(Total);writer.Write(Index);writer.Write(Count);writer.Write((ushort)Payload.Length);writer.Write(Payload);}
    public override void read(PooledBinaryReader reader)
    {
        Reset();try
        {
            if(reader.ReadByte()!=1)throw new InvalidDataException();var group=reader.ReadBytes(16);if(group.Length!=16)throw new InvalidDataException();Group=new Guid(group);
            Total=reader.ReadInt32();Index=reader.ReadInt32();Count=reader.ReadInt32();int length=reader.ReadUInt16();
            if(Group==Guid.Empty||Total<1||Total>25*1024*1024||Count!=(Total+8999)/9000||Index<0||Index>=Count||length!=Math.Min(9000,Total-Index*9000))throw new InvalidDataException();
            RebirthRecyclerPlacedWireBudget.Reserve(ref charged,length);Payload=reader.ReadBytes(length);if(Payload.Length!=length)throw new InvalidDataException();
        }
        catch{Reset();throw;}
    }
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthRecyclerPlacedSettlement.ReceivePlacedRequest(this,world);
    public void Reset(){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);Group=Guid.Empty;Total=Index=Count=0;Payload=null;RebirthRecyclerPlacedWireBudget.Release(ref charged);}
    public void Cleanup()=>Reset();public static int GetPoolSize()=>32;
}
[Preserve]
public sealed class NetPackageRebirthRecyclerPlacedReplyV1:NetPackage,IMemoryPoolableObject
{
    internal string Text;private int charged;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthRecyclerPlacedReplyV1 Setup(string text)
    {Reset();if(text==null||text.Length>8192)throw new InvalidDataException();RebirthRecyclerPlacedWireBudget.Reserve(ref charged,checked(2*text.Length+64));Text=text;return this;}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write((byte)1);writer.Write(Text);}
    public override void read(PooledBinaryReader reader)
    {Reset();try{if(reader.ReadByte()!=1)throw new InvalidDataException();RebirthRecyclerPlacedWireBudget.Reserve(ref charged,4*8192);Text=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,8192);}catch{Reset();throw;}}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthRecyclerPlacedSettlement.ReceivePlacedReply(this,world);
    public void Reset(){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);Text=null;RebirthRecyclerPlacedWireBudget.Release(ref charged);}
    public void Cleanup()=>Reset();public static int GetPoolSize()=>16;
}
