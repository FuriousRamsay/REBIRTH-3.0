using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationAcknowledgment : NetPackage
{
    private int playerId;private Guid operation;private string digest;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthHydrationAcknowledgment Setup(EntityPlayerLocal player,Guid id,string hash)
    {
        if(!RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,id,hash,out _))
            throw new InvalidOperationException("Current released hydration terminal required");
        playerId=player.entityId;operation=id;digest=hash;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        operation=Guid.Empty;digest=null;playerId=reader.ReadInt32();var bytes=reader.ReadBytes(RebirthHydrationOperationFrame.Size);
        if(playerId<=0||!RebirthHydrationOperationFrame.TryDecode(bytes,out var id,out var hash))throw new InvalidDataException("Invalid hydration acknowledgment");
        operation=id;digest=hash;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0)throw new InvalidOperationException("Missing hydration acknowledgment");
        byte[] bytes=RebirthHydrationOperationFrame.Encode(operation,digest);
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write(bytes);
    }
    public int GetLength()=>6+RebirthHydrationOperationFrame.Size;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(Sender==null||!ValidEntityIdForSender(playerId))return;
        RebirthHydrationRemoteCompletionServer.Acknowledge(world,playerId,Sender,operation,digest);
        RebirthHydrationClosedServer.TrySend(world,playerId,Sender,operation,digest);
    }
}
