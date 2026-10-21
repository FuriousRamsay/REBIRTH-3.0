using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationGrant : NetPackage
{
    private int playerId;private Guid operation;private string digest;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthHydrationGrant Setup(RebirthHydrationRemoteGrant grant)
    {
        if(grant==null||!grant.Current())throw new InvalidOperationException("Current hydration server grant required");
        playerId=grant.Lease.Player.entityId;operation=grant.Original.Operation;digest=grant.Original.Digest;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        operation=Guid.Empty;digest=null;playerId=reader.ReadInt32();var bytes=reader.ReadBytes(RebirthHydrationOperationFrame.Size);
        if(playerId<=0||!RebirthHydrationOperationFrame.TryDecode(bytes,out var id,out var hash))throw new InvalidDataException("Invalid hydration grant frame");
        operation=id;digest=hash;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0)throw new InvalidOperationException("Missing hydration grant");
        byte[] bytes=RebirthHydrationOperationFrame.Encode(operation,digest);
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write(bytes);
    }
    public int GetLength()=>6+RebirthHydrationOperationFrame.Size;
    public override void ProcessPackage(World world,GameManager callbacks)
        =>RebirthHydrationRemoteClientGrant.Receive(world,playerId,operation,digest);
}
