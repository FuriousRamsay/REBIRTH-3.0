using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthActionPaymentOwnerGrant:NetPackage
{
    private int actor,total,count,index;private Guid operation;private byte phase;private string hash,fragment;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthActionPaymentOwnerGrant Setup(int actorId,Guid original,byte action,RebirthRemoteResourceFragments.Body body,int part)
    {actor=actorId;operation=original;phase=action;hash=body.Hash;total=body.Length;count=body.Count;index=part;fragment=body.Part(part);return this;}
    public override void read(PooledBinaryReader reader)
    {
        RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);
        if(reader.ReadUInt32()!=1)throw new InvalidDataException("Electrical owner grant version differs");
        actor=reader.ReadInt32();byte[] nonce=reader.ReadBytes(16);if(nonce.Length!=16)throw new EndOfStreamException();operation=new Guid(nonce);phase=reader.ReadByte();
        hash=RebirthCookingGridCustody.ReadWireString(reader,64);total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();
        fragment=RebirthCookingGridCustody.ReadWireString(reader,RebirthRemoteResourceFragments.ChunkText);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write((uint)1);writer.Write(actor);writer.Write(operation.ToByteArray());writer.Write(phase);RebirthCookingGridCustody.WriteWireString(writer,hash,64);
        writer.Write(total);writer.Write(count);writer.Write(index);RebirthCookingGridCustody.WriteWireString(writer,fragment,RebirthRemoteResourceFragments.ChunkText);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        try{RemoteResourceTransactions.ReceiveActionRemoteGrant(this,world,actor,operation,phase,hash,total,count,index,fragment);}
        catch(Exception error){RemoteResourceDiagnostics.Write("Electrical owner original retained: "+error.Message);}
    }
    public int GetLength()=>128+(fragment==null?0:System.Text.Encoding.UTF8.GetByteCount(fragment));
}
