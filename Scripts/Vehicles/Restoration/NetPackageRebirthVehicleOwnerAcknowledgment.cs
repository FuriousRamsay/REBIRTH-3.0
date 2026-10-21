using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthVehicleOwnerAcknowledgment:NetPackage
{
    private Guid operation;private string hash;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthVehicleOwnerAcknowledgment Setup(Guid id,string digest){operation=id;hash=digest;return this;}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadByte()!=1)throw new InvalidDataException();operation=new Guid(reader.ReadBytes(16));hash=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);if(operation==Guid.Empty||hash.Length!=64)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(operation.ToByteArray());RebirthSurvivorNetworkCodec.WriteString(writer,hash,64);}
    public int GetLength()=>85;
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(Sender!=null)RebirthVehicleTransferCoordinator.ReceiveOwnerAcknowledgment(world,Sender,operation,hash);}
}
