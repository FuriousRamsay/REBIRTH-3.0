using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthMusicTransferOffer : NetPackage
{
    public const int MaxPayloadBytes = 262144;
    private int playerId;
    private byte[] payload;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthMusicTransferOffer Setup(int owner, RebirthMusicTransferState offer)
    {
        playerId=owner; payload=Encoding.UTF8.GetBytes(offer.ToXml().ToString(SaveOptions.DisableFormatting));
        if(payload.Length>MaxPayloadBytes)throw new InvalidDataException("Music transfer exceeds packet limit");
        return this;
    }
    public override void read(PooledBinaryReader r)
    {
        playerId=r.ReadInt32(); int length=r.ReadInt32();
        if(length<1 || length>MaxPayloadBytes)throw new InvalidDataException("Invalid music transfer length");
        payload=r.ReadBytes(length);
        if(payload.Length!=length)throw new EndOfStreamException();
    }
    public override void write(PooledBinaryWriter w)
    {base.write(w);((BinaryWriter)w).Write(playerId);((BinaryWriter)w).Write(payload.Length);((BinaryWriter)w).Write(payload);}
    public int GetLength()=>12+(payload?.Length??0);
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        var player=world?.GetPrimaryPlayer();
        if(world==null || !world.IsRemote() || player==null || player.entityId!=playerId)return;
        RebirthMusicTransferState offer;
        try
        {
            using(var stream=new MemoryStream(payload))
            using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
                if(!RebirthMusicTransferState.TryRead(XElement.Load(reader),out offer))return;
        }
        catch(XmlException){return;}
        RebirthMusicTransferClient.Receive(world,playerId,offer);
    }
}

[Preserve]
public sealed class NetPackageRebirthMusicTransferAck : NetPackage
{
    private int playerId;
    private string creationId,transactionId;
    private bool applied;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthMusicTransferAck Setup(int owner,RebirthMusicTransferState offer,bool success)
    {playerId=owner;creationId=offer.CreationId;transactionId=offer.TransactionId;applied=success;return this;}
    public override void read(PooledBinaryReader r)
    {playerId=r.ReadInt32();creationId=RebirthSurvivorNetworkCodec.ReadString(r,128);transactionId=RebirthSurvivorNetworkCodec.ReadString(r,128);applied=r.ReadBoolean();}
    public override void write(PooledBinaryWriter w)
    {base.write(w);((BinaryWriter)w).Write(playerId);RebirthSurvivorNetworkCodec.WriteString(w,creationId,128);RebirthSurvivorNetworkCodec.WriteString(w,transactionId,128);((BinaryWriter)w).Write(applied);}
    public int GetLength()=>9+RebirthSurvivorNetworkCodec.EstimateString(creationId,128)+RebirthSurvivorNetworkCodec.EstimateString(transactionId,128);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null || world.IsRemote() || !ValidEntityIdForSender(playerId))return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        RebirthMusicTransferServer.Acknowledge(player,Sender,creationId,transactionId,applied);
        RebirthWorldCharacterRecord record;
        if(player!=null && RebirthWorldCharacterService.TryGet(player,out record) && record?.Support!=null)
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMusicLibrarySnapshot>().Setup(playerId,record),_attachedToEntityId:playerId);
    }
}

public static class RebirthMusicTransferClient
{
    private static World offerWorld;
    private static int ownerId;
    private static RebirthMusicTransferState pending;
    private static float nextAttempt;
    public static void Reset(){offerWorld=null;pending=null;nextAttempt=0;}
    public static void Receive(World world,int owner,RebirthMusicTransferState offer)
    {
        if(world==null||!RebirthMusicLibraryClient.EnsureCurrent(world.GetPrimaryPlayer())||offer==null || offer.CreationId!=RebirthMusicLibraryClient.CreationId)return;
        offerWorld=world;ownerId=owner;pending=offer.Clone();nextAttempt=0;
    }
    public static void Tick()
    {
        if(Time.realtimeSinceStartup<nextAttempt)return;
        nextAttempt=Time.realtimeSinceStartup+2f;
        var world=GameManager.Instance?.World;
        var player=world?.GetPrimaryPlayer();
        if(world==null || player==null || !player.IsSpawned()
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        RebirthMusicLibraryClient.EnsureCurrent(player);
        if(!world.IsRemote())
        {
            if(RebirthMusicLibraryClient.Revision<0||RebirthMusicLibraryClient.PendingTransfer)
                RebirthMusicLibraryClient.Dispatch(player,0);
            return;
        }
        if(pending==null)
        {
            // Recover saved custody even if the player unequipped the device or
            // closed its window before disconnecting. Also retry a missed offer.
            if(RebirthMusicLibraryClient.Revision<0 || RebirthMusicLibraryClient.PendingTransfer)
                RebirthMusicLibraryClient.Dispatch(player,0);
            return;
        }
        if(!ReferenceEquals(world,offerWorld) || pending.CreationId!=RebirthMusicLibraryClient.CreationId){Reset();return;}
        if(player==null || player.entityId!=ownerId)return;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        NetPackagePlayerData checkpoint;
        NetPackageRebirthMusicTransferAck acknowledgement;
        try
        {
            if(connection==null || !connection.IsClient || connection.IsServer)return;
            checkpoint=NetPackageManager.GetPackage<NetPackagePlayerData>();
            acknowledgement=NetPackageManager.GetPackage<NetPackageRebirthMusicTransferAck>();
            if(!RebirthMusicLibraryClient.CanSend(connection,checkpoint)||
                !RebirthMusicLibraryClient.CanSend(connection,acknowledgement))return;
        }
        catch(Exception){return;}
        var result=RebirthMusicOwnerTransfer.Apply(player,RebirthMusicLibraryClient.CreationId,pending);
        if(result==RebirthMusicOwnerTransferResult.Pending)return;
        try
        {
            // Save inventory AND persistent receipt before acknowledging. If either
            // send throws, retain the offer; its owner receipt prevents repeating items.
            connection.SendToServer(checkpoint.Setup(player));
            connection.SendToServer(acknowledgement.Setup(ownerId,pending,result==RebirthMusicOwnerTransferResult.Applied));
        }
        catch(Exception){return;}
    }
}
