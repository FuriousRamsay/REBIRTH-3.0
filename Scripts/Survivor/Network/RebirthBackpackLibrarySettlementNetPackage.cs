using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthBackpackLibrarySettleRequest : NetPackage
{
    private int playerId;
    private string creation;
    private Guid transaction;
    private bool applied;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthBackpackLibrarySettleRequest Setup(int id,Guid character,Guid transfer,bool wasApplied) =>Setup(id,character.ToString("N"),transfer,wasApplied);
    public NetPackageRebirthBackpackLibrarySettleRequest Setup(int id,string character,Guid transfer,bool wasApplied)
    {playerId=id;RebirthSurvivorRequestScope.TryNormalize(character,out creation);transaction=transfer;applied=wasApplied;return this;}
    public override void read(PooledBinaryReader reader)
    {
        creation=null;transaction=Guid.Empty;applied=false;playerId=reader.ReadInt32();
        var c=RebirthBackpackLibraryCreationWire.Read(reader);var t=reader.ReadBytes(16);byte outcome=reader.ReadByte();
        if(t.Length!=16||outcome>1)throw new InvalidDataException("Invalid library settlement request.");
        creation=c;transaction=new Guid(t);applied=outcome==1;
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);((BinaryWriter)writer).Write(playerId);RebirthBackpackLibraryCreationWire.Write((BinaryWriter)writer,creation);((BinaryWriter)writer).Write(transaction.ToByteArray());((BinaryWriter)writer).Write((byte)(applied?1:0));}
    public int GetLength()=>23+RebirthBackpackLibraryCreationWire.Length(creation);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(world==null||world.IsRemote()||Sender==null||creation==null||transaction==Guid.Empty||
            !ValidEntityIdForSender(playerId)||manager==null||!manager.IsServer)return;
        // Mapping failure is detected before touching durable custody state.
        NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibrarySettled));
        var player=world.GetEntity(playerId) as EntityPlayer;string character=creation,transfer=transaction.ToString("N");
        if(!RebirthBackpackLibraryServer.TryGetSettlement(player,Sender,character,transfer,out var settled))
        {
            // The claim alone proves nothing. Advance inspects freshly loaded native owner data.
            if(!RebirthBackpackLibraryServer.Advance(player,Sender,character,transfer,applied)||
                !RebirthBackpackLibraryServer.TryGetSettlement(player,Sender,character,transfer,out settled))return;
        }
        if(settled.Applied!=applied)return;
        manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthBackpackLibrarySettled>().Setup(playerId,settled),_attachedToEntityId:playerId);
    }
}

[Preserve]
public sealed class NetPackageRebirthBackpackLibrarySettled : NetPackage
{
    private int playerId;
    private string creation;
    private Guid transaction;
    private long revision=-1;
    private bool applied;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthBackpackLibrarySettled Setup(int id,RebirthBackpackLibrarySettlement value)
    {
        if(value==null)throw new ArgumentNullException(nameof(value));
        playerId=id;creation=value.CreationId;transaction=Guid.Parse(value.TransactionId);revision=value.GearRevision;applied=value.Applied;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        creation=null;transaction=Guid.Empty;revision=-1;applied=false;playerId=reader.ReadInt32();
        var c=RebirthBackpackLibraryCreationWire.Read(reader);var t=reader.ReadBytes(16);long r=reader.ReadInt64();byte outcome=reader.ReadByte();
        if(t.Length!=16||r<=0||outcome>1)throw new InvalidDataException("Invalid library settled outcome.");
        var character=c;var transfer=new Guid(t);
        if(character==null||transfer==Guid.Empty)throw new InvalidDataException("Missing library settled identity.");
        creation=character;transaction=transfer;revision=r;applied=outcome==1;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(creation==null||transaction==Guid.Empty||revision<=0)throw new InvalidOperationException("Missing library settlement.");
        base.write(writer);((BinaryWriter)writer).Write(playerId);RebirthBackpackLibraryCreationWire.Write((BinaryWriter)writer,creation);((BinaryWriter)writer).Write(transaction.ToByteArray());((BinaryWriter)writer).Write(revision);((BinaryWriter)writer).Write((byte)(applied?1:0));
    }
    public int GetLength()=>31+RebirthBackpackLibraryCreationWire.Length(creation);
    public override void ProcessPackage(World world,GameManager callbacks)
    {RebirthBackpackLibraryClientOffers.ReceiveSettlement(world,playerId,creation,transaction,revision,applied);}
}