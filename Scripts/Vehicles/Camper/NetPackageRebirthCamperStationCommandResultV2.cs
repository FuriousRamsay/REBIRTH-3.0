using System;
using System.IO;
using UnityEngine.Scripting;

// Separate wire identity. Legacy session-result framing stays unchanged.
[Preserve]
public sealed class NetPackageRebirthCamperStationCommandResultV2:NetPackage,IMemoryPoolableObject
{
    private int vehicle;private Guid token,identity,operation;private byte action;private bool accepted;private byte[] snapshot;
    internal Guid OriginalOperation=>operation;
    internal Guid SessionToken=>token;
    internal byte ReplyPurpose=>action;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthCamperStationCommandResultV2 Setup(int v,Guid t,Guid i,Guid op,bool ok,byte[] state,byte purpose=RebirthCamperRemoteStationTransport.StationCommand)
    {vehicle=v;token=t;identity=i;operation=op;accepted=ok;snapshot=state;action=purpose;return this;}
    public override void read(PooledBinaryReader reader)
        =>ReadBody(reader);
    internal void ReadBody(PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=2)throw new InvalidDataException("Unsupported station command result version.");
        vehicle=reader.ReadInt32();token=new Guid(reader.ReadBytes(16));identity=new Guid(reader.ReadBytes(16));operation=new Guid(reader.ReadBytes(16));
        if(token==Guid.Empty||operation==Guid.Empty)throw new InvalidDataException("Station command correlation required.");
        action=reader.ReadByte();if(action!=RebirthCamperRemoteStationTransport.StationCommand&&action!=RebirthCamperRemoteStationTransport.FuelSwitch)throw new InvalidDataException("Invalid station result purpose.");
        accepted=reader.ReadBoolean();snapshot=CamperFrame.Read(reader);
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);WriteBody(writer);}
    internal void WriteBody(PooledBinaryWriter writer)
    {if(token==Guid.Empty||operation==Guid.Empty)throw new InvalidDataException("Station command correlation required.");writer.Write((byte)2);writer.Write(vehicle);writer.Write(token.ToByteArray());writer.Write(identity.ToByteArray());writer.Write(operation.ToByteArray());writer.Write(action);writer.Write(accepted);CamperFrame.Write(writer,snapshot);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||operation==Guid.Empty)return;
        var live=world.GetEntity(vehicle) as EntityVehicle;if(live==null)return;
        var state=identity==Guid.Empty?null:RebirthCamperTransferSnapshot.Restore(identity,snapshot);
        RebirthCamperRemoteStationTransport.ReceiveCommandSnapshot?.Invoke(live,token,state,action,operation,accepted);
    }
    public int GetLength()=>0;
    public void Reset(){snapshot=null;vehicle=0;token=identity=operation=Guid.Empty;action=0;accepted=false;Sender=null;}
    public void Cleanup()=>Reset();
}
