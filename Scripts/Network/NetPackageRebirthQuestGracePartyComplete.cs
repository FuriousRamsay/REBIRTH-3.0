using System.IO;

#nullable disable

/// <summary>
/// Online shared-quest holder -> server notification that the shared job site has
/// reached its final return-to-trader phase. The server uses this to reconcile
/// disconnected holders of the same shared QuestCode/QuestId.
/// </summary>
public sealed class NetPackageRebirthQuestGracePartyComplete : NetPackage
{
    private int questCode;
    private string questId;

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.ToServer; }
    }

    public NetPackageRebirthQuestGracePartyComplete Setup(
        int code,
        string id)
    {
        questCode = code;
        questId = id ?? string.Empty;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader br = (BinaryReader)reader;
        questCode = br.ReadInt32();
        questId = br.ReadString();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter bw = (BinaryWriter)writer;
        bw.Write(questCode);
        bw.Write(questId ?? string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (Sender == null || world == null || world.IsRemote()) return;
        EntityPlayer player = world.GetEntity(Sender.entityId) as EntityPlayer;
        if (!RebirthTraderQuestGraceManager.IsAuthorizedPartyCompletion(player, questCode, questId)) return;
        RebirthTraderQuestGraceManager.MarkOfflinePartySiteCompleted(questCode, questId);
    }

    public int GetLength()
    {
        return 12 + (questId != null ? questId.Length : 0);
    }
}
