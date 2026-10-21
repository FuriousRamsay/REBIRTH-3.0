using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationTerminal : NetPackage
{
    private int playerId;private Guid operation;private string digest;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthHydrationTerminal Setup(RebirthHydrationTransferJournal.RemoteTerminal terminal)
    {
        if(terminal==null||!terminal.CurrentFor(terminal.Lease))throw new InvalidOperationException("Qualified hydration terminal required");
        playerId=terminal.Lease.Player.entityId;operation=terminal.Lease.Original.Operation;digest=terminal.Lease.Original.Digest;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        operation=Guid.Empty;digest=null;playerId=reader.ReadInt32();var bytes=reader.ReadBytes(RebirthHydrationOperationFrame.Size);
        if(playerId<=0||!RebirthHydrationOperationFrame.TryDecode(bytes,out var id,out var hash))throw new InvalidDataException("Invalid hydration terminal");
        operation=id;digest=hash;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0)throw new InvalidOperationException("Missing hydration terminal");
        byte[] bytes=RebirthHydrationOperationFrame.Encode(operation,digest);
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write(bytes);
    }
    public int GetLength()=>6+RebirthHydrationOperationFrame.Size;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(RebirthHydrationRemoteClientTerminal.Receive(world,playerId,operation,digest))
            RebirthHydrationAcknowledgmentClient.TrySend(world.GetPrimaryPlayer(),operation,digest);
    }
}
