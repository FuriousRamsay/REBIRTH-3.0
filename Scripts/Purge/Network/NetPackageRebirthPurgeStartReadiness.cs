using System;
using System.IO;
using UnityEngine.Scripting;

// Native package-name mapping rejects peers on the older V1 wire before login.
internal static class RebirthPurgeStartWire
{
    internal static bool AcceptStatus(Guid expected,Guid request,byte current,int vehicle,byte next,int nextVehicle)
        =>expected!=Guid.Empty&&request==expected&&current!=1&&current!=5&&
            (next==1||next>=current&&(vehicle==0||nextVehicle==vehicle));
}

[Preserve]
public sealed class NetPackageRebirthPurgeStartQueryV2:NetPackage
{
    private int actor;private Guid worldId,request;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthPurgeStartQueryV2 Setup(int player,Guid world,Guid requestId){actor=player;worldId=world;request=requestId;return this;}
    public override void read(PooledBinaryReader r)
    {byte version=r.ReadByte();if(version!=2)throw new InvalidDataException("Purge start query version.");actor=r.ReadInt32();worldId=ReadGuid(r);request=ReadGuid(r);}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write((byte)2);w.Write(actor);w.Write(worldId.ToByteArray());w.Write(request.ToByteArray());}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(request!=Guid.Empty&&ValidEntityIdForSender(actor))RebirthPurgeTruckStart.Query(world,Sender,actor,worldId,request);}
    internal static Guid ReadGuid(PooledBinaryReader r)
    {var bytes=r.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();return new Guid(bytes);}
    public int GetLength()=>37;
}

[Preserve]
public sealed class NetPackageRebirthPurgeStartStatusV2:NetPackage
{
    private int actor,vehicle;private Guid worldId,request;private byte state;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeStartStatusV2 Setup(int player,Guid world,Guid requestId,byte value,int truck)
    {actor=player;worldId=world;request=requestId;state=value;vehicle=truck;return this;}
    public override void read(PooledBinaryReader r)
    {
        byte version=r.ReadByte();if(version!=2)throw new InvalidDataException("Purge start status version.");actor=r.ReadInt32();worldId=NetPackageRebirthPurgeStartQueryV2.ReadGuid(r);request=NetPackageRebirthPurgeStartQueryV2.ReadGuid(r);
        state=r.ReadByte();vehicle=r.ReadInt32();if(state<1||state>4||state>=3&&vehicle<=0)throw new InvalidDataException("Purge start status value.");
    }
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write((byte)2);w.Write(actor);w.Write(worldId.ToByteArray());w.Write(request.ToByteArray());w.Write(state);w.Write(vehicle);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world?.IsRemote()==true)RebirthPurgeTruckStart.Status(world,actor,worldId,request,state,vehicle);}
    public int GetLength()=>42;
}

// Relocation and readiness are scoped to the same client start request.
[Preserve]
public sealed class NetPackageRebirthPurgeStartRelocateV2:NetPackage
{
    private int actor,revision;private Guid worldId,request;private UnityEngine.Vector3 position;private float heading;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeStartRelocateV2 Setup(int player,Guid world,Guid requestId,int site,SpawnPosition anchor)
    {actor=player;worldId=world;request=requestId;revision=site;position=anchor.position;heading=anchor.heading;return this;}
    public override void read(PooledBinaryReader r)
    {byte version=r.ReadByte();if(version!=2)throw new InvalidDataException("Purge relocation version.");actor=r.ReadInt32();worldId=NetPackageRebirthPurgeStartQueryV2.ReadGuid(r);request=NetPackageRebirthPurgeStartQueryV2.ReadGuid(r);revision=r.ReadInt32();position=new UnityEngine.Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());heading=r.ReadSingle();if(revision<=0)throw new InvalidDataException("Purge relocation revision.");}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write((byte)2);w.Write(actor);w.Write(worldId.ToByteArray());w.Write(request.ToByteArray());w.Write(revision);w.Write(position.x);w.Write(position.y);w.Write(position.z);w.Write(heading);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world?.IsRemote()==true)RebirthPurgeTruckStart.RelocateClient(world,actor,worldId,request,revision,position,heading);}
    public int GetLength()=>57;
}
