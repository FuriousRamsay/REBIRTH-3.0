using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationClosed : NetPackage
{
    private int playerId;private Guid operation;private string digest;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthHydrationClosed Setup(int id,RebirthHydrationTransferOriginal original)
    {
        if(id<=0||original==null)throw new InvalidOperationException("Acknowledged hydration original required");
        playerId=id;operation=original.Operation;digest=original.Digest;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        operation=Guid.Empty;digest=null;playerId=reader.ReadInt32();var bytes=reader.ReadBytes(RebirthHydrationOperationFrame.Size);
        if(playerId<=0||!RebirthHydrationOperationFrame.TryDecode(bytes,out var id,out var hash))throw new InvalidDataException("Invalid hydration closed receipt");operation=id;digest=hash;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0)throw new InvalidOperationException("Missing hydration closed receipt");byte[] bytes=RebirthHydrationOperationFrame.Encode(operation,digest);
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write(bytes);
    }
    public int GetLength()=>6+RebirthHydrationOperationFrame.Size;
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthHydrationRemoteClientTerminal.ReceiveClosed(world,playerId,operation,digest);
}
