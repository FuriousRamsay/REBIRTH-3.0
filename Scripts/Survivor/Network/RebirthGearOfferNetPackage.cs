using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthGearOfferFragment : NetPackage
{
    private int playerId;
    private byte[] body;
    private RebirthGearOfferFragment fragment;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthGearOfferFragment Setup(int entityId,RebirthGearOfferFragment value)
    {fragment=value??throw new ArgumentNullException(nameof(value));playerId=entityId;body=value.Encode();return this;}
    public override void read(PooledBinaryReader reader)
    {
        body=null;fragment=null;playerId=reader.ReadInt32();int length=reader.ReadUInt16();
        if(length<1||length>RebirthGearOfferFragment.MaxFrameBytes)throw new InvalidDataException("Invalid gear fragment length.");
        var bytes=reader.ReadBytes(length);
        if(bytes.Length!=length||!RebirthGearOfferFragment.TryDecode(bytes,out fragment))throw new InvalidDataException("Invalid gear fragment.");
        body=bytes;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(body==null||fragment==null)throw new InvalidOperationException("Missing gear fragment.");
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write((ushort)body.Length);((BinaryWriter)writer).Write(body);
    }
    public int GetLength()=>8+(body?.Length??0);
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthGearOfferClient.Receive(world,playerId,fragment);
}