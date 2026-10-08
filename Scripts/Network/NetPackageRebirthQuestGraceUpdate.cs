using System.IO;

#nullable disable

/// <summary>
/// Client -> server mirror of a live quest-grace transition. This lets the dedicated
/// server preserve the original outside/death deadline if the player disconnects later.
/// </summary>
public sealed class NetPackageRebirthQuestGraceUpdate : NetPackage
{
    private int questCode;
    private string questId;
    private string poiReservationKey;
    private long deadlineUtcTicks;
    private byte reason;
    private bool clear;

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.ToServer; }
    }

    public NetPackageRebirthQuestGraceUpdate Setup(
        int code,
        string id,
        string poiKey,
        long deadline,
        RebirthTraderQuestGraceReason why,
        bool remove)
    {
        questCode = code;
        questId = id ?? string.Empty;
        poiReservationKey = poiKey ?? string.Empty;
        deadlineUtcTicks = deadline;
        reason = (byte)why;
        clear = remove;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader br = (BinaryReader)reader;
        questCode = br.ReadInt32();
        questId = br.ReadString();
        poiReservationKey = br.ReadString();
        deadlineUtcTicks = br.ReadInt64();
        reason = br.ReadByte();
        clear = br.ReadBoolean();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter bw = (BinaryWriter)writer;
        bw.Write(questCode);
        bw.Write(questId ?? string.Empty);
        bw.Write(poiReservationKey ?? string.Empty);
        bw.Write(deadlineUtcTicks);
        bw.Write(reason);
        bw.Write(clear);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (Sender == null || world == null)
            return;

        EntityPlayer player = world.GetEntity(Sender.entityId) as EntityPlayer;
        RebirthTraderQuestGraceManager.ApplyServerGraceUpdate(
            Sender,
            player,
            questCode,
            questId,
            poiReservationKey,
            deadlineUtcTicks,
            (RebirthTraderQuestGraceReason)reason,
            clear);
    }

    public int GetLength()
    {
        return 28 +
               (questId != null ? questId.Length : 0) +
               (poiReservationKey != null ? poiReservationKey.Length : 0);
    }
}
