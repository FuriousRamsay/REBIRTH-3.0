using System.IO;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthSkillWaveAClientAction : byte
{
    LockpickSuccess = 0,
    BarterBuy = 1,
    BarterSell = 2
}

/// <summary>Small client evidence package. Server validates sender and action context before any LBD award.</summary>
[Preserve]
public sealed class NetPackageRebirthSkillWaveAActionRequest : NetPackage
{
    private int playerId, x, y, z, itemType, count, value;
    private RebirthSkillWaveAClientAction action;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthSkillWaveAActionRequest Setup(int entityId, RebirthSkillWaveAClientAction a, int px, int py, int pz, int type, int itemCount, int transactionValue)
    { playerId=entityId; action=a; x=px; y=py; z=pz; itemType=type; count=itemCount; value=transactionValue; return this; }

    public override void read(PooledBinaryReader reader)
    { BinaryReader b=(BinaryReader)reader; playerId=b.ReadInt32(); action=(RebirthSkillWaveAClientAction)b.ReadByte(); x=b.ReadInt32(); y=b.ReadInt32(); z=b.ReadInt32(); itemType=b.ReadInt32(); count=b.ReadInt32(); value=b.ReadInt32(); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); BinaryWriter b=(BinaryWriter)writer; b.Write(playerId); b.Write((byte)action); b.Write(x); b.Write(y); b.Write(z); b.Write(itemType); b.Write(count); b.Write(value); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if(world==null || world.IsRemote() || !ValidEntityIdForSender(playerId)) return;
        if(action<RebirthSkillWaveAClientAction.LockpickSuccess || action>RebirthSkillWaveAClientAction.BarterSell) return;
        EntityPlayer player=world.GetEntity(playerId) as EntityPlayer; if(player==null)return;
        RebirthSkillWaveAService.HandleServerClientAction(player,action,x,y,z,itemType,count,value);
    }
    public int GetLength(){return 41;}
}
