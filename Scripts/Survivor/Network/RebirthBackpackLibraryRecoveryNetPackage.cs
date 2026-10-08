using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthBackpackLibraryRecoveryRequest : NetPackage
{
    private int playerId;
    private string creation;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthBackpackLibraryRecoveryRequest Setup(int entityId,string creationId)
    {playerId=entityId;RebirthSurvivorRequestScope.TryNormalize(creationId,out creation);return this;}
    public override void read(PooledBinaryReader reader)
    {creation=null;playerId=reader.ReadInt32();creation=RebirthBackpackLibraryCreationWire.Read(reader);}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);((BinaryWriter)writer).Write(playerId);RebirthBackpackLibraryCreationWire.Write((BinaryWriter)writer,creation);}
    public int GetLength()=>6+RebirthBackpackLibraryCreationWire.Length(creation);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||Sender==null||creation==null||!ValidEntityIdForSender(playerId))return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        RebirthBackpackLibraryRecoveryDelivery.TrySend(player,Sender,creation);
    }
}

public static class RebirthBackpackLibraryRecoveryDelivery
{
    public static bool TrySend(EntityPlayer player,ClientInfo sender,string creation)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager==null||!manager.IsServer)return false;
        try
        {
            // Mapping failure must be detected before saving or enqueueing a partial offer.
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibraryOfferChunk));
            if(!RebirthBackpackLibraryServer.GetRecoveryOffer(player,sender,creation,out var offer)||
                !RebirthBackpackLibraryWireCodec.TryEncode(offer,out var bytes))return false;
            int size=RebirthBackpackLibraryOfferAssembly.ChunkBytes;
            for(int index=0,offset=0;offset<bytes.Length;index++,offset+=size)
            {
                var payload=new byte[Math.Min(size,bytes.Length-offset)];Buffer.BlockCopy(bytes,offset,payload,0,payload.Length);
                if(!RebirthBackpackLibraryOfferChunk.TryCreate(offer.CreationId,Guid.Parse(offer.TransactionId),
                    bytes.Length,index,payload,out var chunk))return false;
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthBackpackLibraryOfferChunk>().Setup(player.entityId,chunk),
                    _attachedToEntityId:player.entityId);
            }
            return true;
        }
        catch(Exception error)
        {
            // A partial queue is resumable using the same immutable journal intent.
            Log.Warning("[REBIRTH Library] Recovery offer delivery deferred: "+error.GetType().Name);
            return false;
        }
    }
}