using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthVehicleOwnerFragment:NetPackage
{
    private int player,total,count,index;private Guid operation;private string digest,part;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthVehicleOwnerFragment Setup(int playerId,Guid id,RebirthRemoteResourceFragments.Body body,int fragment)
    {player=playerId;operation=id;digest=body.Hash;total=body.Length;count=body.Count;index=fragment;part=body.Part(fragment);return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=1)throw new InvalidDataException();player=reader.ReadInt32();operation=new Guid(reader.ReadBytes(16));
        digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();part=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthRemoteResourceFragments.ChunkText);
        if(player<1||operation==Guid.Empty||total<1||total>3*1024*1024||count!=(total+RebirthRemoteResourceFragments.ChunkBytes-1)/RebirthRemoteResourceFragments.ChunkBytes||index<0||index>=count)throw new InvalidDataException();
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(player);writer.Write(operation.ToByteArray());RebirthSurvivorNetworkCodec.WriteString(writer,digest,64);writer.Write(total);writer.Write(count);writer.Write(index);RebirthSurvivorNetworkCodec.WriteString(writer,part,RebirthRemoteResourceFragments.ChunkText);}
    public int GetLength()=>105+(part?.Length??0);
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthVehicleTransferCoordinator.ReceiveOwnerFragment(world,player,operation,digest,total,count,index,part);
}
