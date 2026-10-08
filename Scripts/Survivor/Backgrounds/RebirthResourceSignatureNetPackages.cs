using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthTreeWoodCreditRequest : NetPackage
{
    private int playerId,blockType,toolType,count;private Vector3i pos;private string blockName=string.Empty;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthTreeWoodCreditRequest Setup(int p,Vector3i v,string n,int bt,int tt,int c){playerId=p;pos=v;blockName=n??string.Empty;blockType=bt;toolType=tt;count=c;return this;}
    public override void read(PooledBinaryReader r){BinaryReader b=(BinaryReader)r;playerId=b.ReadInt32();pos=new Vector3i(b.ReadInt32(),b.ReadInt32(),b.ReadInt32());blockName=RebirthSurvivorNetworkCodec.ReadString(b,192);blockType=b.ReadInt32();toolType=b.ReadInt32();count=b.ReadInt32();}
    public override void write(PooledBinaryWriter w){base.write(w);BinaryWriter b=(BinaryWriter)w;b.Write(playerId);b.Write(pos.x);b.Write(pos.y);b.Write(pos.z);RebirthSurvivorNetworkCodec.WriteString(b,blockName,192);b.Write(blockType);b.Write(toolType);b.Write(count);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||!ValidEntityIdForSender(playerId))return;EntityPlayer p=world.GetEntity(playerId) as EntityPlayer;if(p!=null)RebirthResourceSignatureService.SubmitTreeWoodCredit(p,pos,blockName,blockType,toolType,count);}
    public int GetLength(){return 48+RebirthSurvivorNetworkCodec.EstimateString(blockName,192);}
}

[Preserve]
public sealed class NetPackageRebirthTreeWoodGrant : NetPackage
{
    private int playerId,itemType,count,toolType;private string treeBlockName=string.Empty;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthTreeWoodGrant Setup(int p,int item,int c,int tool,string tree){playerId=p;itemType=item;count=c;toolType=tool;treeBlockName=tree??string.Empty;return this;}
    public override void read(PooledBinaryReader r){BinaryReader b=(BinaryReader)r;playerId=b.ReadInt32();itemType=b.ReadInt32();count=b.ReadInt32();toolType=b.ReadInt32();treeBlockName=RebirthSurvivorNetworkCodec.ReadString(b,192);}
    public override void write(PooledBinaryWriter w){base.write(w);BinaryWriter b=(BinaryWriter)w;b.Write(playerId);b.Write(itemType);b.Write(count);b.Write(toolType);RebirthSurvivorNetworkCodec.WriteString(b,treeBlockName,192);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthResourceSignatureService.GrantTreeWood(playerId,itemType,count,toolType,treeBlockName);}
    public int GetLength(){return 40+RebirthSurvivorNetworkCodec.EstimateString(treeBlockName,192);}
}
