using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthGearAppliedRequest : NetPackage
{
    private int playerId;
    private string creation;
    private Guid transaction;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthGearAppliedRequest Setup(int id,string character,Guid transfer)
    {
        if(transfer==Guid.Empty||!RebirthSurvivorRequestScope.TryNormalize(character,out creation))throw new ArgumentException("Invalid gear applied identity.");
        playerId=id;transaction=transfer;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        creation=null;transaction=Guid.Empty;playerId=reader.ReadInt32();
        string character=RebirthBackpackLibraryCreationWire.Read(reader);
        var bytes=reader.ReadBytes(16);
        if(bytes.Length!=16)throw new InvalidDataException("Truncated gear applied request.");
        var transfer=new Guid(bytes);
        if(transfer==Guid.Empty||!RebirthSurvivorRequestScope.TryNormalize(character,out creation))throw new InvalidDataException("Invalid gear applied request.");
        transaction=transfer;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(creation==null||transaction==Guid.Empty)throw new InvalidOperationException("Missing gear applied identity.");
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
            if(!RebirthGearOfferServer.TryAdvanceApplied(player,Sender,creation,transaction))return;
            if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!manager.IsServer||
                !ReferenceEquals(GameManager.Instance?.World,world)||!RebirthGearOfferServer.TryAdvanceRecovery(player,Sender,creation,transaction))return;
            if(!RebirthRemoteGearAppliedConfirmation.TryGetSettlement(player,Sender,creation,transfer,out terminal))return;
        }
        if(!terminal.Applied||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!manager.IsServer)return;
        manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthGearSettled>().Setup(playerId,terminal),_attachedToEntityId:playerId);
    }
}