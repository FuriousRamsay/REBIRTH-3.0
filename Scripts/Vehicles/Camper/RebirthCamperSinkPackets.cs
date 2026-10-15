using System;
using System.IO;
using System.Text;
using UnityEngine.Scripting;

internal static class RebirthCamperSinkWire
{
    internal static string Text(PooledBinaryReader reader,int maximum)
    {
        int count=reader.ReadUInt16();if(count>maximum)throw new InvalidDataException("Sink text too large.");
        var bytes=reader.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);
    }
    internal static void Text(PooledBinaryWriter writer,string value,int maximum)
    {var bytes=Encoding.UTF8.GetBytes(value??string.Empty);if(bytes.Length>maximum)throw new InvalidDataException();writer.Write((ushort)bytes.Length);writer.Write(bytes);}
    internal static byte[] Bytes(PooledBinaryReader reader)
    {int count=reader.ReadInt32();if(count<0||count>1024*1024)throw new InvalidDataException("Sink frame too large.");var bytes=reader.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();return bytes;}
    internal static void Bytes(PooledBinaryWriter writer,byte[] bytes)
    {bytes=bytes??Array.Empty<byte>();if(bytes.Length>1024*1024)throw new InvalidDataException();writer.Write(bytes.Length);writer.Write(bytes);}
}
[Preserve]
public sealed class NetPackageRebirthCamperSinkRequestV1:NetPackage
{
    private int actor,entity,slot;private Guid world,operation;private string creation;private byte[] offered;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperSinkRequestV1 Setup(int a,int e,Guid w,string c,Guid o,int s,byte[] image)
    {actor=a;entity=e;world=w;creation=c;operation=o;slot=s;offered=image;return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=1)throw new InvalidDataException();actor=reader.ReadInt32();entity=reader.ReadInt32();world=new Guid(reader.ReadBytes(16));
        creation=RebirthCamperSinkWire.Text(reader,4096);operation=new Guid(reader.ReadBytes(16));slot=reader.ReadInt32();offered=RebirthCamperSinkWire.Bytes(reader);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write((byte)1);writer.Write(actor);writer.Write(entity);writer.Write(world.ToByteArray());
        RebirthCamperSinkWire.Text(writer,creation,4096);writer.Write(operation.ToByteArray());writer.Write(slot);RebirthCamperSinkWire.Bytes(writer,offered);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(ValidEntityIdForSender(actor))RebirthCamperSink.Receive(world,Sender,actor,entity,this.world,creation,operation,slot,offered);}
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthCamperSinkReplyV1:NetPackage
{
    private int entity,ml;private Guid world,identity,operation;private long revision;private bool done;private string error;private byte[] apply;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperSinkReplyV1 Setup(int e,Guid w,Guid id,long r,int amount,Guid o,bool d,string status,byte[] frame)
    {entity=e;world=w;identity=id;revision=r;ml=amount;operation=o;done=d;error=status;apply=frame;return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=1)throw new InvalidDataException();entity=reader.ReadInt32();world=new Guid(reader.ReadBytes(16));identity=new Guid(reader.ReadBytes(16));
        revision=reader.ReadInt64();ml=reader.ReadInt32();operation=new Guid(reader.ReadBytes(16));done=reader.ReadBoolean();
        error=RebirthCamperSinkWire.Text(reader,256);apply=RebirthCamperSinkWire.Bytes(reader);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write((byte)1);writer.Write(entity);writer.Write(world.ToByteArray());writer.Write(identity.ToByteArray());writer.Write(revision);writer.Write(ml);
        writer.Write(operation.ToByteArray());writer.Write(done);RebirthCamperSinkWire.Text(writer,error,256);RebirthCamperSinkWire.Bytes(writer,apply);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world?.IsRemote()==true)RebirthCamperSink.ReceiveReply(world,entity,this.world,identity,revision,ml,operation,done,error,apply);}
    public int GetLength()=>0;
}
