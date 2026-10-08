using System.IO;

#nullable disable

/// <summary>
/// Server -> reconnecting client authoritative disconnect deadline and offline party result.
/// </summary>
public sealed class NetPackageRebirthQuestGraceSync : NetPackage
{
    private int questCode;
    private string questId;
    private long deadlineUtcTicks;
    private bool partySiteCompleted;

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.ToClient; }
    }

    public NetPackageRebirthQuestGraceSync Setup(
        int code,
        string id,
        long deadline,
        bool partyCompleted)
    {
        questCode = code;
        questId = id ?? string.Empty;
        deadlineUtcTicks = deadline;
        partySiteCompleted = partyCompleted;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader br = (BinaryReader)reader;
        questCode = br.ReadInt32();
        questId = br.ReadString();
        deadlineUtcTicks = br.ReadInt64();
        partySiteCompleted = br.ReadBoolean();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter bw = (BinaryWriter)writer;
        bw.Write(questCode);
        bw.Write(questId ?? string.Empty);
        bw.Write(deadlineUtcTicks);
        bw.Write(partySiteCompleted);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthTraderQuestGraceManager.ApplyReconnectSync(
            questCode,
            questId,
            deadlineUtcTicks,
            partySiteCompleted);
    }

    public int GetLength()
    {
        return 20 + (questId != null ? questId.Length : 0);
    }
}
