using System;
using System.IO;
using UnityEngine.Scripting;
[Preserve]
public sealed class NetPackageRebirthGearSettled : NetPackage
{
    private int playerId;
    private string creation;
    private Guid transaction;
    private long revision=-1;
    private bool applied;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthGearSettled Setup(int id,RebirthGearSettlement value)
    {
        if(value==null)throw new ArgumentNullException(nameof(value));
        playerId=id;creation=value.CreationId;transaction=Guid.Parse(value.TransactionId);revision=value.GearRevision;applied=value.Applied;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        creation=null;transaction=Guid.Empty;revision=-1;applied=false;playerId=reader.ReadInt32();
        var c=RebirthBackpackLibraryCreationWire.Read(reader);var t=reader.ReadBytes(16);long r=reader.ReadInt64();byte outcome=reader.ReadByte();
        if(t.Length!=16||r<=0||outcome>1)throw new InvalidDataException("Invalid gear settled outcome.");
        var character=c;var transfer=new Guid(t);
        if(character==null||transfer==Guid.Empty)throw new InvalidDataException("Missing gear settled identity.");
        creation=character;transaction=transfer;revision=r;applied=outcome==1;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(creation==null||transaction==Guid.Empty||revision<=0)throw new InvalidOperationException("Missing gear settlement.");
        base.write(writer);((BinaryWriter)writer).Write(playerId);RebirthBackpackLibraryCreationWire.Write((BinaryWriter)writer,creation);((BinaryWriter)writer).Write(transaction.ToByteArray());((BinaryWriter)writer).Write(revision);((BinaryWriter)writer).Write((byte)(applied?1:0));
    }
    public int GetLength()=>31+RebirthBackpackLibraryCreationWire.Length(creation);
    public override void ProcessPackage(World world,GameManager callbacks)
    {RebirthGearOfferClient.ReceiveSettlement(world,playerId,creation,transaction,revision,applied);}
}