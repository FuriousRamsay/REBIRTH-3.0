using System;
using System.IO;
using System.Threading;
using UnityEngine.Scripting;

// Small lineage metadata only; the full Add/native images remain with their
// actual producers. No wire object is itself owner/custody/mutation authority.
internal static class RebirthUiQueueInsertionWireBudget
{
    private static int bytes;
    internal static void Reserve(ref int charged,int amount)
    {if(Interlocked.Add(ref bytes,amount)>8*1024*1024){Interlocked.Add(ref bytes,-amount);throw new InvalidDataException();}charged=amount;}
    internal static void Release(ref int charged){int amount=Interlocked.Exchange(ref charged,0);if(amount!=0)Interlocked.Add(ref bytes,-amount);}
}
[Preserve]
public sealed class NetPackageRebirthUiQueueInsertionRequestV1:NetPackage,IMemoryPoolableObject
{
    internal string Text;internal byte Action;internal bool Query=>Action==1;internal bool Ack=>Action==2;private int charged;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthUiQueueInsertionRequestV1 Setup(string text,bool query){Reset();Text=text;Action=(byte)(query?1:0);return this;}
    internal NetPackageRebirthUiQueueInsertionRequestV1 SetupAcknowledged(string text){Reset();Text=text;Action=2;return this;}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write((byte)1);writer.Write(Action);writer.Write(Text);}
    public override void read(PooledBinaryReader reader)
    {Reset();try{if(reader.ReadByte()!=1)throw new InvalidDataException();RebirthUiQueueInsertionWireBudget.Reserve(ref charged,4*65536);Action=reader.ReadByte();if(Action>2)throw new InvalidDataException();Text=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,65536);}catch{Reset();throw;}}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthProductiveEarnedUiQueueOwner.ReceiveRemoteInsertionRequest(this,world);
    public void Reset(){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);Text=null;Action=0;RebirthUiQueueInsertionWireBudget.Release(ref charged);}
    public void Cleanup()=>Reset();public static int GetPoolSize()=>16;
}
[Preserve]
public sealed class NetPackageRebirthUiQueueInsertionReplyV1:NetPackage,IMemoryPoolableObject
{
    internal string Text;private int charged;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthUiQueueInsertionReplyV1 Setup(string text){Reset();Text=text;return this;}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write((byte)1);writer.Write(Text);}
    public override void read(PooledBinaryReader reader)
    {Reset();try{if(reader.ReadByte()!=1)throw new InvalidDataException();RebirthUiQueueInsertionWireBudget.Reserve(ref charged,4*8192);Text=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,8192);}catch{Reset();throw;}}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthProductiveEarnedUiQueueOwner.ReceiveRemoteInsertionReply(this,world);
    public void Reset(){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);Text=null;RebirthUiQueueInsertionWireBudget.Release(ref charged);}
    public void Cleanup()=>Reset();public static int GetPoolSize()=>16;
}
