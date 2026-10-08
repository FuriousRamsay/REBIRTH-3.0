using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthChefCraftSignatureRequest : NetPackage
{
    private int playerId, itemType;
    private ushort seed, quality;
    private string recipeName = string.Empty;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthChefCraftSignatureRequest Setup(int p, int t, ushort s, ushort q, string r) { playerId=p; itemType=t; seed=s; quality=q; recipeName=r??string.Empty; return this; }
    public override void read(PooledBinaryReader r) { PooledBinaryReader b=r; playerId=b.ReadInt32(); itemType=b.ReadInt32(); seed=b.ReadUInt16(); quality=b.ReadUInt16(); recipeName=RebirthSurvivorNetworkCodec.ReadString(b,256); }
    public override void write(PooledBinaryWriter w) { base.write(w); PooledBinaryWriter b=w; b.Write(playerId); b.Write(itemType); b.Write(seed); b.Write(quality); RebirthSurvivorNetworkCodec.WriteString(b,recipeName,256); }
    public override void ProcessPackage(World world, GameManager callbacks) { if(world==null||!ValidEntityIdForSender(playerId))return; EntityPlayer p=world.GetEntity(playerId) as EntityPlayer; if(p!=null)RebirthFoodFarmingButcherySignatureService.RegisterChefCraftRequest(p,itemType,seed,quality,recipeName); }
    public int GetLength() { return 24 + RebirthSurvivorNetworkCodec.EstimateString(recipeName,256); }
}

[Preserve]
public sealed class NetPackageRebirthChefCraftCorrection : NetPackage
{
    private int playerId;
    private ItemValue corrected = ItemValue.None.Clone();
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthChefCraftCorrection Setup(int p, ItemValue v) { playerId=p; corrected=v!=null?v.Clone():ItemValue.None.Clone(); return this; }
    public override void read(PooledBinaryReader r) { PooledBinaryReader b=r; playerId=b.ReadInt32(); corrected=new ItemValue(); corrected.Read(b); }
    public override void write(PooledBinaryWriter w) { base.write(w); PooledBinaryWriter b=w; b.Write(playerId); corrected.Write(b); }
    public override void ProcessPackage(World world, GameManager callbacks) { RebirthFoodFarmingButcherySignatureService.ApplyClientChefCorrection(playerId,corrected); }
    public int GetLength() { return 2048; }
}
