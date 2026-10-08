using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthMetalCraftSignatureRequest : NetPackage
{
    private int playerId,itemType;private ushort seed,quality;private string recipeName=string.Empty;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthMetalCraftSignatureRequest Setup(int p,int t,ushort s,ushort q,string r){playerId=p;itemType=t;seed=s;quality=q;recipeName=r??string.Empty;return this;}
    public override void read(PooledBinaryReader r){PooledBinaryReader b=r;playerId=b.ReadInt32();itemType=b.ReadInt32();seed=b.ReadUInt16();quality=b.ReadUInt16();recipeName=RebirthSurvivorNetworkCodec.ReadString(b,256);}
    public override void write(PooledBinaryWriter w){base.write(w);PooledBinaryWriter b=w;b.Write(playerId);b.Write(itemType);b.Write(seed);b.Write(quality);RebirthSurvivorNetworkCodec.WriteString(b,recipeName,256);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||!ValidEntityIdForSender(playerId))return;EntityPlayer p=world.GetEntity(playerId) as EntityPlayer;if(p!=null)RebirthWorkmanshipSignatureService.RegisterMetalCraftRequest(p,itemType,seed,quality,recipeName);}
    public int GetLength(){return 24+RebirthSurvivorNetworkCodec.EstimateString(recipeName,256);}
}

[Preserve]
public sealed class NetPackageRebirthMetalCraftCorrection : NetPackage
{
    private int playerId;private ItemValue corrected=ItemValue.None.Clone();
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthMetalCraftCorrection Setup(int p,ItemValue v){playerId=p;corrected=v!=null?v.Clone():ItemValue.None.Clone();return this;}
    public override void read(PooledBinaryReader r){PooledBinaryReader b=r;playerId=b.ReadInt32();corrected=new ItemValue();corrected.Read(b);}
    public override void write(PooledBinaryWriter w){base.write(w);PooledBinaryWriter b=w;b.Write(playerId);corrected.Write(b);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthWorkmanshipSignatureService.ApplyClientMetalCorrection(playerId,corrected);}
    public int GetLength(){return 2048;}
}

[Preserve]
public sealed class NetPackageRebirthMechanicSalvageRequest : NetPackage
{
    private int playerId,itemType,baseCount,damageBefore;private uint blockRawData;private Vector3i pos;private string blockName=string.Empty;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthMechanicSalvageRequest Setup(int p,Vector3i v,string n,uint raw,int damage,int t,int c){playerId=p;pos=v;blockName=n??string.Empty;blockRawData=raw;damageBefore=damage;itemType=t;baseCount=c;return this;}
    public override void read(PooledBinaryReader r){PooledBinaryReader b=r;playerId=b.ReadInt32();pos=new Vector3i(b.ReadInt32(),b.ReadInt32(),b.ReadInt32());blockName=RebirthSurvivorNetworkCodec.ReadString(b,192);blockRawData=b.ReadUInt32();damageBefore=b.ReadInt32();itemType=b.ReadInt32();baseCount=b.ReadInt32();}
    public override void write(PooledBinaryWriter w){base.write(w);PooledBinaryWriter b=w;b.Write(playerId);b.Write(pos.x);b.Write(pos.y);b.Write(pos.z);RebirthSurvivorNetworkCodec.WriteString(b,blockName,192);b.Write(blockRawData);b.Write(damageBefore);b.Write(itemType);b.Write(baseCount);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||!ValidEntityIdForSender(playerId))return;EntityPlayer p=world.GetEntity(playerId) as EntityPlayer;if(p!=null)RebirthWorkmanshipSignatureService.ProcessMechanicSalvageRequest(p,pos,blockName,blockRawData,damageBefore,itemType,baseCount);}
    public int GetLength(){return 40+RebirthSurvivorNetworkCodec.EstimateString(blockName,192);}
}

[Preserve]
public sealed class NetPackageRebirthMechanicSalvageGrant : NetPackage
{
    private int playerId,itemType,count;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthMechanicSalvageGrant Setup(int p,int t,int c){playerId=p;itemType=t;count=c;return this;}
    public override void read(PooledBinaryReader r){PooledBinaryReader b=r;playerId=b.ReadInt32();itemType=b.ReadInt32();count=b.ReadInt32();}
    public override void write(PooledBinaryWriter w){base.write(w);PooledBinaryWriter b=w;b.Write(playerId);b.Write(itemType);b.Write(count);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthWorkmanshipSignatureService.GrantMechanicExtra(playerId,itemType,count);}
    public int GetLength(){return 28;}
}
