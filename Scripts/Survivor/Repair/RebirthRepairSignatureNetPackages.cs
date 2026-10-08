using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthRepairSignatureBegin : NetPackage
{
    private int playerId, itemType; private ushort seed, quality;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRebirthRepairSignatureBegin Setup(int p, int t, ushort s, ushort q) { playerId=p; itemType=t; seed=s; quality=q; return this; }
    public override void read(PooledBinaryReader reader) { BinaryReader b=(BinaryReader)reader; playerId=b.ReadInt32(); itemType=b.ReadInt32(); seed=b.ReadUInt16(); quality=b.ReadUInt16(); }
    public override void write(PooledBinaryWriter writer) { base.write(writer); BinaryWriter b=(BinaryWriter)writer; b.Write(playerId); b.Write(itemType); b.Write(seed); b.Write(quality); }
    public override void ProcessPackage(World world, GameManager callbacks) { if(world==null||world.IsRemote()||!ValidEntityIdForSender(playerId))return; EntityPlayer p=world.GetEntity(playerId) as EntityPlayer; if(p!=null)RebirthRepairSignatureService.BeginServerRepair(p,itemType,seed,quality); }
    public int GetLength() { return 12; }
}

[Preserve]
public sealed class NetPackageRebirthRepairConditionCorrection : NetPackage
{
    private int playerId, itemType, targetMax; private ushort seed, quality; private float originalCap;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRebirthRepairConditionCorrection Setup(int p,int t,ushort s,ushort q,int m,float cap){playerId=p;itemType=t;seed=s;quality=q;targetMax=m;originalCap=cap;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;playerId=b.ReadInt32();itemType=b.ReadInt32();seed=b.ReadUInt16();quality=b.ReadUInt16();targetMax=b.ReadInt32();originalCap=b.ReadSingle();}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(playerId);b.Write(itemType);b.Write(seed);b.Write(quality);b.Write(targetMax);b.Write(originalCap);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthRepairSignatureService.ApplyClientCorrection(playerId,itemType,seed,quality,targetMax,originalCap);}
    public int GetLength(){return 20;}
}
