using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthCamperFuelSwitchV2:NetPackage,IMemoryPoolableObject
{
    private int player,vehicle;private Guid token,identity,installation,operation;private bool burning;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthCamperFuelSwitchV2 Setup(int p,int v,Guid t,Guid id,Guid station,Guid op,bool value)
    {player=p;vehicle=v;token=t;identity=id;installation=station;operation=op;burning=value;return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=2)throw new InvalidDataException("Unsupported station fuel request.");
        player=reader.ReadInt32();vehicle=reader.ReadInt32();token=new Guid(reader.ReadBytes(16));identity=new Guid(reader.ReadBytes(16));installation=new Guid(reader.ReadBytes(16));operation=new Guid(reader.ReadBytes(16));burning=reader.ReadBoolean();
        if(token==Guid.Empty||operation==Guid.Empty||identity==Guid.Empty||installation==Guid.Empty)throw new InvalidDataException("Fuel original identity required.");
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)2);writer.Write(player);writer.Write(vehicle);writer.Write(token.ToByteArray());writer.Write(identity.ToByteArray());writer.Write(installation.ToByteArray());writer.Write(operation.ToByteArray());writer.Write(burning);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!ThreadManager.IsMainThread()||operation==Guid.Empty||!ReferenceEquals(callbacks,GameManager.Instance)
            ||!ReferenceEquals(world,callbacks?.World)||!ValidEntityIdForSender(player))return;
        RebirthCamperRemoteStationTransport.HandleSession(world,Sender,player,vehicle,token,identity,installation,RebirthCamperRemoteStationTransport.FuelSwitch,null,burning,operation);
    }
    public int GetLength()=>0;
    public void Reset(){player=vehicle=0;token=identity=installation=operation=Guid.Empty;burning=false;Sender=null;}
    public void Cleanup()=>Reset();
}
