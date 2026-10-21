using System.IO;
using UnityEngine.Scripting;

// Explicit version2 wrapper. Legacy response bytes remain unchanged.
[Preserve]
public sealed class NetPackageRebirthNpcInteractionResponseV2:NetPackage
{
    private ulong generation,request;
    private uint worldEpoch,connection;
    private RebirthNpcInteractionProjection projection;
    private RebirthNpcInteractionCommandResponse response;
    private string error;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthNpcInteractionResponseV2 Setup(ulong g,uint world,uint session,ulong id,RebirthNpcInteractionProjection p,RebirthNpcInteractionCommandResponse r,string detail)
    {generation=g;worldEpoch=world;connection=session;request=id;projection=p;response=r;error=RebirthNpcNetworkProtocol.BoundDetail(detail);return this;}
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);WriteBody(writer);
    }
    internal ulong RecoveryGeneration=>generation;
    internal uint RuntimeRevision=>projection?.RuntimeRevision??0;
    internal void WriteBody(PooledBinaryWriter writer)
    {
        writer.Write((ushort)2);writer.Write(generation);writer.Write(worldEpoch);writer.Write(connection);writer.Write(request);
        RebirthNpcNetworkFraming.WriteString(writer,error,RebirthNpcNetworkProtocol.MaxDetailLength);
        NetPackageRebirthNpcInteractionResponse.WriteProjection(writer,projection);NetPackageRebirthNpcInteractionResponse.WriteResponse(writer,response);
    }
    public override void read(PooledBinaryReader reader)
    {ReadBody(reader);}
    internal void ReadBody(PooledBinaryReader reader)
    {
        if(reader.ReadUInt16()!=2)throw new InvalidDataException("Unsupported NPC interaction response codec");
        generation=reader.ReadUInt64();worldEpoch=reader.ReadUInt32();connection=reader.ReadUInt32();request=reader.ReadUInt64();
        error=RebirthNpcNetworkFraming.ReadString(reader,RebirthNpcNetworkProtocol.MaxDetailLength);
        projection=NetPackageRebirthNpcInteractionResponse.ReadProjection(reader);response=NetPackageRebirthNpcInteractionResponse.ReadResponse(reader);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=false||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||
            !RebirthNpcNetworkEpoch.AcceptClientEpoch(worldEpoch)||connection!=RebirthNpcNetworkEpoch.ClientConnectionEpoch||!RebirthNpcRecoveryProjectionScope.Observe(generation))return;
        RebirthNpcInteractionNetworkClient.Receive(connection,request,projection,response,error,generation);
    }
    public int GetLength()=>0;
}
