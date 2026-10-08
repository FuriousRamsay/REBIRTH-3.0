using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthBackpackLibraryOfferChunk : NetPackage
{
    private int playerId;
    private RebirthBackpackLibraryOfferChunk chunk;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthBackpackLibraryOfferChunk Setup(int entityId,RebirthBackpackLibraryOfferChunk value)
    {playerId=entityId;chunk=value??throw new ArgumentNullException(nameof(value));return this;}
    public override void read(PooledBinaryReader reader)
    {
        chunk=null;playerId=reader.ReadInt32();
        if(!RebirthBackpackLibraryOfferChunk.TryRead(reader,out chunk))throw new InvalidDataException("Invalid library offer chunk.");
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(chunk==null)throw new InvalidOperationException("Missing library offer chunk.");
        base.write(writer);((BinaryWriter)writer).Write(playerId);chunk.Write((BinaryWriter)writer);
    }
    public int GetLength()=>6+(chunk?.BodyLength??0);
    public override void ProcessPackage(World world,GameManager callbacks)
    {RebirthBackpackLibraryClientOffers.Receive(world,playerId,chunk);}
}