using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationPreparedRequest : NetPackage
{
    private int playerId;private Guid operation;private string digest;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthHydrationPreparedRequest Setup(RebirthHydrationProposalClient.Intent intent)
    {
        if(intent?.Original==null||!intent.Current()||!RebirthHydrationOwnerHold.Matches(intent.Player,intent.Original)||
            !RebirthHydrationOwnerCheckpoint.HasSavedBefore(intent.Player,intent.Original))throw new InvalidOperationException("Saved original hydration preimage required");
        playerId=intent.Player.entityId;operation=intent.Original.Operation;digest=intent.Original.Digest;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        operation=Guid.Empty;digest=null;playerId=reader.ReadInt32();var bytes=reader.ReadBytes(RebirthHydrationOperationFrame.Size);
        if(playerId<=0||!RebirthHydrationOperationFrame.TryDecode(bytes,out var id,out var hash))throw new InvalidDataException("Invalid hydration prepared request");
        operation=id;digest=hash;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0)throw new InvalidOperationException("Missing hydration prepared request");
        byte[] bytes=RebirthHydrationOperationFrame.Encode(operation,digest);
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write(bytes);
    }
    public int GetLength()=>6+RebirthHydrationOperationFrame.Size;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(Sender==null||!ValidEntityIdForSender(playerId))return;
        RebirthHydrationPreparedServer.Receive(world,playerId,Sender,operation,digest);
    }
}
