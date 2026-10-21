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
    public override void ProcessPackage(World world,GameManager callbacks) { /* Legacy primitive identity has no original paid-action proof. */ }
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

internal static class RebirthRepairOriginalWire
{
    private const int Maximum=2*1024*1024;
    internal static string Read(PooledBinaryReader input){int length=input.ReadInt32();if(length<0||length>Maximum)throw new InvalidDataException();var bytes=input.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return new System.Text.UTF8Encoding(false,true).GetString(bytes);}
    internal static void Write(PooledBinaryWriter output,string image){var bytes=new System.Text.UTF8Encoding(false,true).GetBytes(image);if(bytes.Length>Maximum)throw new InvalidDataException();output.Write(bytes.Length);output.Write(bytes);}
}
[Preserve]
public sealed class NetPackageRebirthRepairOriginalRequest:NetPackage
{
    private int playerId;private string image=string.Empty;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthRepairOriginalRequest Setup(int id,string value){playerId=id;image=value;return this;}
    public override void read(PooledBinaryReader input){playerId=input.ReadInt32();image=RebirthRepairOriginalWire.Read(input);}
    public override void write(PooledBinaryWriter output){base.write(output);output.Write(playerId);RebirthRepairOriginalWire.Write(output,image);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()!=false||!ValidEntityIdForSender(playerId))return;RebirthRepairOriginalCorrection.ReceiveServer(world,playerId,image);}
    public int GetLength()=>8+System.Text.Encoding.UTF8.GetByteCount(image);
}
[Preserve]
public sealed class NetPackageRebirthRepairOriginalReply:NetPackage
{
    private int playerId;private string image=string.Empty;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthRepairOriginalReply Setup(int id,string value){playerId=id;image=value;return this;}
    public override void read(PooledBinaryReader input){playerId=input.ReadInt32();image=RebirthRepairOriginalWire.Read(input);}
    public override void write(PooledBinaryWriter output){base.write(output);output.Write(playerId);RebirthRepairOriginalWire.Write(output,image);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()!=true)return;RebirthRepairOriginalCorrection.ReceiveClient(world,playerId,image);}
    public int GetLength()=>8+System.Text.Encoding.UTF8.GetByteCount(image);
}
