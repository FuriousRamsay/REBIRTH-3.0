using System;

#nullable disable

/// <summary>
/// 3.1 port of the REBIRTH 2.6 trader-job availability requirement.
/// Vanilla 3.1 QuestStatus/NotStarted hides Jobs as soon as the player has one
/// active quest from that trader. REBIRTH supports multiple concurrent trader jobs,
/// so expose Jobs while at least one UNLOCKED tier still has acceptance room.
/// </summary>
public sealed class DialogRequirementQuestStatusRebirth : BaseDialogRequirement
{
    public override BaseDialogRequirement.RequirementTypes RequirementType
    {
        get { return BaseDialogRequirement.RequirementTypes.QuestStatus; }
    }

    public override string GetRequiredDescription(EntityPlayer player)
    {
        return string.Empty;
    }

    public override bool CheckRequirement(EntityPlayer player, EntityNPC talkingTo)
    {
        // This custom requirement is installed only on the trader jobshave* responses,
        // where vanilla uses QuestStatus value=NotStarted.
        if (!string.Equals(Value, "NotStarted", StringComparison.OrdinalIgnoreCase))
            return true;

        EntityTrader trader = talkingTo as EntityTrader;
        if (trader == null || player == null)
            return false;

        bool allowed = RebirthTraderJobPolicy.CanAcceptAnyListedJob(player, trader);

        if (RebirthTraderDebug.Enabled)
        {
            QuestJournal journal = player.QuestJournal;
            int open = RebirthTraderJobPolicy.CountOpenPersonalTraderJobs(journal);
            int dailyUsed = RebirthTraderJobPolicy.CountDailyReservedTraderJobs(journal);
            int dailyLimit = RebirthTraderJobPolicy.GetDailyQuestLimit();

            RebirthTraderDebug.Trace(
                "JOBS_REQUIREMENT allowed=" + allowed +
                " open=" + open +
                " dailyUsed=" + dailyUsed +
                " dailyLimit=" + dailyLimit +
                " traderOffers=" +
                (trader.activeQuests != null ? trader.activeQuests.Count : -1));

            int highest =
                RebirthTraderJobPolicy.GetHighestUnlockedJobTier(
                    player,
                    trader);

            int target =
                RebirthTraderJobPolicy.GetHighestTierWithAcceptanceRoom(
                    player,
                    trader);

            RebirthTraderDebug.Trace(
                "JOBS_REQUIREMENT highestUnlocked=" + highest +
                " targetTier=" + target);

            for (int tier = 1; tier <= highest; tier++)
            {
                RebirthTraderDebug.Trace(
                    "JOBS_REQUIREMENT tier=" + tier +
                    " effectiveMax=" +
                    RebirthTraderJobPolicy.GetEffectiveAcceptedJobLimit(tier) +
                    " canAccept=" +
                    RebirthTraderJobPolicy.CanAcceptAnotherJobAtTier(
                        journal,
                        tier));
            }
        }

        return allowed;
    }
}
