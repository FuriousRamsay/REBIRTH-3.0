using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthPurgeStartQueryV1:NetPackage
{
    private int actor;private Guid worldId;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthPurgeStartQueryV1 Setup(int player,Guid world){actor=player;worldId=world;return this;}
    public override void read(PooledBinaryReader r)
    {if(r.ReadByte()!=1)throw new InvalidDataException("Purge start query version.");actor=r.ReadInt32();var bytes=r.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();worldId=new Guid(bytes);}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write((byte)1);w.Write(actor);w.Write(worldId.ToByteArray());}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(ValidEntityIdForSender(actor))RebirthPurgeTruckStart.Query(world,Sender,actor,worldId);}
    public int GetLength()=>21;
}

[Preserve]
public sealed class NetPackageRebirthPurgeStartStatusV1:NetPackage
{
    private int actor,vehicle;private Guid worldId;private byte state;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeStartStatusV1 Setup(int player,Guid world,byte value,int truck)
    {actor=player;worldId=world;state=value;vehicle=truck;return this;}
    public override void read(PooledBinaryReader r)
    {
        if(r.ReadByte()!=1)throw new InvalidDataException("Purge start status version.");actor=r.ReadInt32();var bytes=r.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();worldId=new Guid(bytes);
        state=r.ReadByte();vehicle=r.ReadInt32();if(state<1||state>4||state>=3&&vehicle<=0)throw new InvalidDataException("Purge start status value.");
    }
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write((byte)1);w.Write(actor);w.Write(worldId.ToByteArray());w.Write(state);w.Write(vehicle);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world?.IsRemote()==true)RebirthPurgeTruckStart.Status(world,actor,worldId,state,vehicle);}
    public int GetLength()=>26;
}
