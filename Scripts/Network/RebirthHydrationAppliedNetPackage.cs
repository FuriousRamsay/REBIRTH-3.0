using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationAppliedRequest : NetPackage
{
    private int playerId;private Guid operation;private string digest;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthHydrationAppliedRequest Setup(RebirthHydrationRemoteClientGrant grant)
    {
        if(grant==null||!grant.Current()||!RebirthHydrationOwnerCheckpoint.HasSaved(grant.Player,grant.Original,RebirthGearOwnerReceipt.Applied))
            throw new InvalidOperationException("Saved original hydration application required");
        playerId=grant.Player.entityId;operation=grant.Original.Operation;digest=grant.Original.Digest;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        operation=Guid.Empty;digest=null;playerId=reader.ReadInt32();var bytes=reader.ReadBytes(RebirthHydrationOperationFrame.Size);
        if(playerId<=0||!RebirthHydrationOperationFrame.TryDecode(bytes,out var id,out var hash))throw new InvalidDataException("Invalid hydration applied request");
        operation=id;digest=hash;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0)throw new InvalidOperationException("Missing hydration applied request");
        byte[] bytes=RebirthHydrationOperationFrame.Encode(operation,digest);
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write(bytes);
    }
    public int GetLength()=>6+RebirthHydrationOperationFrame.Size;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(Sender==null||!ValidEntityIdForSender(playerId))return;
        if(!RebirthHydrationRemoteCompletionServer.Receive(world,playerId,Sender,operation,digest))return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        if(RebirthHydrationRemoteGrant.TryGet(player,operation,digest,out var grant))RebirthHydrationTerminalServer.TrySend(grant);
    }
}
