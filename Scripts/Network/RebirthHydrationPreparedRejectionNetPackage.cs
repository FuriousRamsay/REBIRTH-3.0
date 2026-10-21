using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationPreparedRejection : NetPackage
{
    private int playerId;private Guid operation;private string digest;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthHydrationPreparedRejection Setup(RebirthHydrationTransferJournal.SavedRejection rejection)
    {
        if(rejection==null||!rejection.Current())throw new InvalidOperationException("Saved hydration rejection required");
        playerId=rejection.Player.entityId;operation=rejection.Original.Operation;digest=rejection.Original.Digest;return this;
    }
    internal NetPackageRebirthHydrationPreparedRejection Setup(RebirthHydrationTransferJournal.PreparedRejection rejection)
    {
        if(rejection==null||!rejection.CurrentFor(rejection.Lease))throw new InvalidOperationException("Saved untouched rejection required");
        playerId=rejection.Lease.Player.entityId;operation=rejection.Lease.Original.Operation;digest=rejection.Lease.Original.Digest;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        operation=Guid.Empty;digest=null;playerId=reader.ReadInt32();var bytes=reader.ReadBytes(RebirthHydrationOperationFrame.Size);
        if(playerId<=0||!RebirthHydrationOperationFrame.TryDecode(bytes,out var id,out var hash))throw new InvalidDataException("Invalid hydration prepared rejection");operation=id;digest=hash;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0)throw new InvalidOperationException("Missing hydration prepared rejection");byte[] bytes=RebirthHydrationOperationFrame.Encode(operation,digest);
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write(bytes);
    }
    public int GetLength()=>6+RebirthHydrationOperationFrame.Size;
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthHydrationClientPreparedRejection.Receive(world,playerId,operation,digest);
}
