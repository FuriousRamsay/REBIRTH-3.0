using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthGearRejectedRequest : NetPackage
{
    private int playerId;
    private string creation;
    private Guid transaction;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthGearRejectedRequest Setup(int id,string character,Guid transfer)
    {
        if(transfer==Guid.Empty||!RebirthSurvivorRequestScope.TryNormalize(character,out creation))throw new ArgumentException("Invalid gear rejected identity.");
        playerId=id;transaction=transfer;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        creation=null;transaction=Guid.Empty;playerId=reader.ReadInt32();
        string character=RebirthBackpackLibraryCreationWire.Read(reader);
        var bytes=reader.ReadBytes(16);
        if(bytes.Length!=16)throw new InvalidDataException("Truncated gear rejected request.");
        var transfer=new Guid(bytes);
        if(transfer==Guid.Empty||!RebirthSurvivorRequestScope.TryNormalize(character,out creation))throw new InvalidDataException("Invalid gear rejected request.");
        transaction=transfer;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(creation==null||transaction==Guid.Empty)throw new InvalidOperationException("Missing gear rejected identity.");
        base.write(writer);((BinaryWriter)writer).Write(playerId);RebirthBackpackLibraryCreationWire.Write((BinaryWriter)writer,creation);((BinaryWriter)writer).Write(transaction.ToByteArray());
    }
    public int GetLength()=>22+RebirthBackpackLibraryCreationWire.Length(creation);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||Sender==null||creation==null||
            transaction==Guid.Empty||!ValidEntityIdForSender(playerId))return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager==null||!manager.IsServer)return;
        NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearSettled));
        var player=world.GetEntity(playerId) as EntityPlayer;string transfer=transaction.ToString("N");
        if(!RebirthRemoteGearAppliedConfirmation.TryGetSettlement(player,Sender,creation,transfer,out var terminal))
        {
            if(!RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,Sender,creation,transfer))return;
            if(!RebirthRemoteGearAppliedConfirmation.TryGetSettlement(player,Sender,creation,transfer,out terminal))return;
        }
        if(terminal.Applied||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!manager.IsServer)return;
        manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthGearSettled>().Setup(playerId,terminal),_attachedToEntityId:playerId);
    }
}