using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

// Separate schema/identity: the existing AssemblySnapshot wire layout is inert.
[Preserve]
public sealed class NetPackageRebirthVehicleCustodySnapshot:NetPackage
{
    private Guid id;private RebirthVehicleAssemblyCarrier carrier;private Vector3i position;private int entity;
    private string game,creation,payload,message,pickup;private RebirthVehicleRequestOutcome outcome;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthVehicleCustodySnapshot Setup(Guid request,RebirthVehicleAssemblyCarrier kind,Vector3i point,int target,RebirthVehicleAssembly snapshot,string text,RebirthVehicleRequestOutcome result,string nativePickup,string character=null)
    {id=request;carrier=kind;position=point;entity=target;game=GameManager.Instance?.World?.worldState?.Guid??"";creation=character??"";payload=snapshot==null?"":RebirthVehicleAssemblySerializer.ToBase64(snapshot);message=text??"";outcome=result;pickup=nativePickup??"";return this;}
    public override void read(PooledBinaryReader r)
    {
        if(r.ReadByte()!=1)throw new InvalidDataException("Unsupported vehicle custody snapshot.");
        id=new Guid(r.ReadBytes(16));carrier=(RebirthVehicleAssemblyCarrier)r.ReadByte();position=new Vector3i(r.ReadInt32(),r.ReadInt32(),r.ReadInt32());entity=r.ReadInt32();
        game=RebirthSurvivorNetworkCodec.ReadBoundedString(r,128);creation=RebirthSurvivorNetworkCodec.ReadBoundedString(r,71);
        payload=RebirthSurvivorNetworkCodec.ReadBoundedString(r,349528);message=RebirthSurvivorNetworkCodec.ReadBoundedString(r,4096);outcome=(RebirthVehicleRequestOutcome)r.ReadByte();pickup=RebirthSurvivorNetworkCodec.ReadBoundedString(r,262144);
    }
    public override void write(PooledBinaryWriter w)
    {
        base.write(w);w.Write((byte)1);w.Write(id.ToByteArray());w.Write((byte)carrier);w.Write(position.x);w.Write(position.y);w.Write(position.z);w.Write(entity);
        RebirthSurvivorNetworkCodec.WriteString(w,game,128);RebirthSurvivorNetworkCodec.WriteString(w,creation,71);RebirthSurvivorNetworkCodec.WriteString(w,payload,349528);RebirthSurvivorNetworkCodec.WriteString(w,message,4096);w.Write((byte)outcome);RebirthSurvivorNetworkCodec.WriteString(w,pickup,262144);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||connection==null||connection.IsServer||world.worldState?.Guid!=game)return;
        var owner=world.GetPrimaryPlayer();if(owner==null||!XUiC_RebirthVehicleRestoration.TryGetRequestCharacter(owner,out var current)||current!=creation)return;
        XUiC_RebirthVehicleRestoration.Receive(id,carrier,position,entity,RebirthVehicleAssemblySerializer.FromBase64(payload),message,outcome,pickup);
    }
    public int GetLength()=>37+RebirthSurvivorNetworkCodec.EstimateString(game,128)+RebirthSurvivorNetworkCodec.EstimateString(creation,71)+RebirthSurvivorNetworkCodec.EstimateString(payload,349528)+RebirthSurvivorNetworkCodec.EstimateString(message,4096)+RebirthSurvivorNetworkCodec.EstimateString(pickup,262144);
}
