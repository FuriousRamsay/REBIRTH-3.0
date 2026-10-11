using System;
using System.IO;
using UnityEngine.Scripting;
[Preserve]
public sealed class NetPackageRebirthBackpackBatchSaleRequest:NetPackage
{
    private int owner,trader;private string creation;private long revision;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthBackpackBatchSaleRequest Setup(int id,int vendor,string character,long version){owner=id;trader=vendor;creation=character;revision=version;return this;}
    public override void read(PooledBinaryReader r){owner=r.ReadInt32();trader=r.ReadInt32();creation=RebirthBackpackLibraryCreationWire.Read(r);revision=r.ReadInt64();if(revision<0)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter w){base.write(w);var b=(BinaryWriter)w;b.Write(owner);b.Write(trader);RebirthBackpackLibraryCreationWire.Write(b,creation);b.Write(revision);}
    public int GetLength()=>16+RebirthBackpackLibraryCreationWire.Length(creation);
    public override void ProcessPackage(World world,GameManager game)
    {
        if(world==null||world.IsRemote()||Sender==null||!ValidEntityIdForSender(owner))return;
        var player=world.GetEntity(owner) as EntityPlayer;
        if(RebirthBackpackLibraryServer.PrepareBatchSale(player,Sender,creation,revision,trader,out _))RebirthBackpackLibraryRecoveryDelivery.TrySend(player,Sender,creation);
    }
}
