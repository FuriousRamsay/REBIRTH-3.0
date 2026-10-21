using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationGeneration : NetPackage
{
    private static long sequence;private RebirthHydrationGenerationFrame frame;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal bool CurrentFor(EntityPlayer player)=>frame!=null&&player!=null&&frame.PlayerId==player.entityId&&frame.Deaths==player.Died&&
        Guid.TryParse(player.world?.Guid,out var world)&&world==frame.World&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&owner.StorageKey==frame.Owner&&
        RebirthMetabolismStateRepository.TryEnsureHydrationGeneration(player,out var generation)&&generation==frame.Generation;
    internal NetPackageRebirthHydrationGeneration Setup(EntityPlayer player)
    {
        if(!ThreadManager.IsMainThread()||sequence==long.MaxValue||!RebirthMetabolismStateRepository.TryEnsureHydrationGeneration(player,out var generation)||
            !Guid.TryParse(player.world.Guid,out var world)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||
            !RebirthHydrationGenerationFrame.TryCreate(player.entityId,world,generation,owner.StorageKey,player.Died,++sequence,out var value))throw new InvalidOperationException("Saved native hydration generation required");
        frame=value;return this;
    }
    public override void read(PooledBinaryReader reader)
    {frame=null;if(!RebirthHydrationGenerationFrame.TryDecode(reader.ReadBytes(RebirthHydrationGenerationFrame.Size),out var value))throw new InvalidDataException("Invalid native hydration generation");frame=value;}
    public override void write(PooledBinaryWriter writer)
    {if(frame==null)throw new InvalidOperationException("Missing hydration generation");base.write(writer);((BinaryWriter)writer).Write(frame.Encode());}
    public int GetLength()=>2+RebirthHydrationGenerationFrame.Size;
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthHydrationGenerationClient.Receive(world,frame);
}
