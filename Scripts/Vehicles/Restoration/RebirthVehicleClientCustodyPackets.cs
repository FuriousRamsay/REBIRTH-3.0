using System;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthVehicleCustodyQuery:NetPackage
{
    private Guid id;private string game,creation,frame;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthVehicleCustodyQuery Setup(Guid request,string world,string character,string payload)
    {id=request;game=world;creation=character;frame=payload;return this;}
    public override void read(PooledBinaryReader r){if(r.ReadByte()!=1)throw new System.IO.InvalidDataException();id=new Guid(r.ReadBytes(16));game=RebirthSurvivorNetworkCodec.ReadBoundedString(r,128);creation=RebirthSurvivorNetworkCodec.ReadBoundedString(r,71);frame=RebirthSurvivorNetworkCodec.ReadBoundedString(r,1398104);}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write((byte)1);w.Write(id.ToByteArray());RebirthSurvivorNetworkCodec.WriteString(w,game,128);RebirthSurvivorNetworkCodec.WriteString(w,creation,71);RebirthSurvivorNetworkCodec.WriteString(w,frame,1398104);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(Sender==null)return;try{NetPackageRebirthVehicleAssemblyRequest.QueryCustody(world,Sender,id,game,creation,frame);}catch{ /* Unknown remains held client-side. */ }}
    public int GetLength()=>19+RebirthSurvivorNetworkCodec.EstimateString(game,128)+RebirthSurvivorNetworkCodec.EstimateString(creation,71)+RebirthSurvivorNetworkCodec.EstimateString(frame,1398104);
}
[Preserve]
public sealed class NetPackageRebirthVehicleCustodyReply:NetPackage
{
    private Guid id;private string game,creation,hash,pickup;private RebirthVehicleRequestOutcome outcome;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthVehicleCustodyReply Setup(Guid request,string world,string character,string fingerprint,RebirthVehicleRequestOutcome result,string nativePickup=null)
    {id=request;game=world;creation=character;hash=fingerprint;outcome=result;pickup=nativePickup??"";return this;}
    public override void read(PooledBinaryReader r){if(r.ReadByte()!=1)throw new System.IO.InvalidDataException();id=new Guid(r.ReadBytes(16));game=RebirthSurvivorNetworkCodec.ReadBoundedString(r,128);creation=RebirthSurvivorNetworkCodec.ReadBoundedString(r,71);hash=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);outcome=(RebirthVehicleRequestOutcome)r.ReadByte();pickup=RebirthSurvivorNetworkCodec.ReadBoundedString(r,262144);}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write((byte)1);w.Write(id.ToByteArray());RebirthSurvivorNetworkCodec.WriteString(w,game,128);RebirthSurvivorNetworkCodec.WriteString(w,creation,71);RebirthSurvivorNetworkCodec.WriteString(w,hash,64);w.Write((byte)outcome);RebirthSurvivorNetworkCodec.WriteString(w,pickup,262144);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {var c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(ThreadManager.IsMainThread()&&world?.IsRemote()==true&&ReferenceEquals(world,GameManager.Instance?.World)&&c!=null&&!c.IsServer)RebirthVehicleClientCustody.Observe(world,id,game,creation,hash,outcome,pickup);}
    public int GetLength()=>20+RebirthSurvivorNetworkCodec.EstimateString(game,128)+RebirthSurvivorNetworkCodec.EstimateString(creation,71)+RebirthSurvivorNetworkCodec.EstimateString(hash,64)+RebirthSurvivorNetworkCodec.EstimateString(pickup,262144);
}
