using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthScavengerSalvageRequest : NetPackage
{
    private int playerId,itemType,baseCount,damageBefore;private uint blockRawData;private Vector3i pos;private string blockName=string.Empty,profileId=string.Empty;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthScavengerSalvageRequest Setup(int p,Vector3i v,string n,string profile,uint raw,int damage,int t,int c){playerId=p;pos=v;blockName=n??string.Empty;profileId=profile??string.Empty;blockRawData=raw;damageBefore=damage;itemType=t;baseCount=c;return this;}
    public override void read(PooledBinaryReader r){BinaryReader b=(BinaryReader)r;playerId=b.ReadInt32();pos=new Vector3i(b.ReadInt32(),b.ReadInt32(),b.ReadInt32());blockName=RebirthSurvivorNetworkCodec.ReadString(b,192);profileId=RebirthSurvivorNetworkCodec.ReadString(b,64);blockRawData=b.ReadUInt32();damageBefore=b.ReadInt32();itemType=b.ReadInt32();baseCount=b.ReadInt32();}
    public override void write(PooledBinaryWriter w){base.write(w);BinaryWriter b=(BinaryWriter)w;b.Write(playerId);b.Write(pos.x);b.Write(pos.y);b.Write(pos.z);RebirthSurvivorNetworkCodec.WriteString(b,blockName,192);RebirthSurvivorNetworkCodec.WriteString(b,profileId,64);b.Write(blockRawData);b.Write(damageBefore);b.Write(itemType);b.Write(baseCount);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||!ValidEntityIdForSender(playerId))return;EntityPlayer p=world.GetEntity(playerId) as EntityPlayer;if(p!=null)RebirthScavengerSalvageProfileService.ProcessSalvageRequest(p,pos,blockName,profileId,blockRawData,damageBefore,itemType,baseCount);}
    public int GetLength(){return 56+RebirthSurvivorNetworkCodec.EstimateString(blockName,192)+RebirthSurvivorNetworkCodec.EstimateString(profileId,64);}
}

[Preserve]
public sealed class NetPackageRebirthScavengerSalvageGrant : NetPackage
{
    private int playerId,itemType,count;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthScavengerSalvageGrant Setup(int p,int t,int c){playerId=p;itemType=t;count=c;return this;}
    public override void read(PooledBinaryReader r){BinaryReader b=(BinaryReader)r;playerId=b.ReadInt32();itemType=b.ReadInt32();count=b.ReadInt32();}
    public override void write(PooledBinaryWriter w){base.write(w);BinaryWriter b=(BinaryWriter)w;b.Write(playerId);b.Write(itemType);b.Write(count);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthScavengerSalvageProfileService.GrantExtra(playerId,itemType,count);}
    public int GetLength(){return 28;}
}
