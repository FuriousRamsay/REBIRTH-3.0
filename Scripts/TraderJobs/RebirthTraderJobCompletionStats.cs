using System;

#nullable disable

public enum RebirthTraderJobCompletionKind
{
    Unknown = 0,
    Clear = 1,
    Fetch = 2,
    FetchClear = 3,
    BuriedSupplies = 4,
    RestorePower = 5
}

/// <summary>
/// Reads long-term trader-job completion history directly from QuestJournal.
///
/// Base 3.1 retains Completed quests in QuestJournal.quests and serializes the full list,
/// so no second REBIRTH completion database is necessary. Counts are differentiated by:
///   tier + job type + prefab + infested/non-infested.
/// Buried Supplies uses tier + type + infested because it has no prefab.
/// </summary>
public static class RebirthTraderJobCompletionStats
{
    private const string BuriedKey = "__buried_supplies__";

    public static RebirthTraderJobCompletionKind GetKind(Quest quest)
    {
        if (quest == null || quest.QuestClass == null)
            return RebirthTraderJobCompletionKind.Unknown;

        string id = quest.QuestClass.ID ?? quest.ID ?? string.Empty;
        string lower = id.ToLowerInvariant();

        if (lower.Contains("buried_supplies"))
            return RebirthTraderJobCompletionKind.BuriedSupplies;
        if (lower.Contains("fetch_clear"))
            return RebirthTraderJobCompletionKind.FetchClear;
        if (lower.Contains("restore_power"))
            return RebirthTraderJobCompletionKind.RestorePower;
        if (lower.Contains("fetch"))
            return RebirthTraderJobCompletionKind.Fetch;
        if (lower.Contains("clear"))
            return RebirthTraderJobCompletionKind.Clear;

        // Fallback for custom normal trader quest IDs that use the same objective types.
        bool fetch = false;
        bool clear = false;
        bool buried = false;
        bool power = false;

        if (quest.Objectives != null)
        {
            for (int i = 0; i < quest.Objectives.Count; i++)
            {
                BaseObjective objective = quest.Objectives[i];

                if (objective is ObjectiveFetchFromTreasure ||
                    objective is ObjectiveTreasureChest)
                    buried = true;
                else if (objective is ObjectiveFetchFromContainer)
                    fetch = true;
                else if (objective is ObjectiveClearSleepers)
                    clear = true;
                else if (objective is ObjectivePOIBlockActivate)
                    power = true;
            }
        }

        if (buried)
            return RebirthTraderJobCompletionKind.BuriedSupplies;
        if (fetch && clear)
            return RebirthTraderJobCompletionKind.FetchClear;
        if (power)
            return RebirthTraderJobCompletionKind.RestorePower;
        if (fetch)
            return RebirthTraderJobCompletionKind.Fetch;
        if (clear)
            return RebirthTraderJobCompletionKind.Clear;

        return RebirthTraderJobCompletionKind.Unknown;
    }

    public static bool IsRecognizedNormalTraderJob(Quest quest)
    {
        if (quest == null || quest.QuestClass == null)
            return false;

        if (!string.IsNullOrEmpty(quest.QuestClass.QuestType))
            return false;

        if (GetKind(quest) == RebirthTraderJobCompletionKind.Unknown)
            return false;

        string id = quest.QuestClass.ID ?? quest.ID ?? string.Empty;

        // Canonical 3.1 trader jobs are tierN_* quests. This survives trader chunk unloads
        // and old completed history where QuestGiverID no longer resolves to a live entity.
        if (id.StartsWith("tier", StringComparison.OrdinalIgnoreCase))
            return true;

        // Custom normal trader jobs retain the trader-position payload after acceptance.
        if (quest.PositionData != null &&
            quest.PositionData.ContainsKey(Quest.PositionDataTypes.TraderPosition))
            return true;

        // Last live-world fallback for custom trader quests.
        return RebirthUtilities.IsVanillaTrader(quest.QuestGiverID);
    }

    public static bool IsInfested(Quest quest)
    {
        return quest != null &&
               quest.QuestClass != null &&
               RebirthInfestedJobsRuntimePolicy.IsInfestedId(
                   quest.QuestClass.ID);
    }

    public static int GetCompletionCount(
        QuestJournal journal,
        Quest offeredQuest)
    {
        if (journal == null ||
            journal.quests == null ||
            offeredQuest == null)
            return 0;

        RebirthTraderJobCompletionKind offeredKind =
            GetKind(offeredQuest);

        if (offeredKind == RebirthTraderJobCompletionKind.Unknown)
            return 0;

        int offeredTier =
            RebirthTraderPoiHistory.GetTier(offeredQuest);
        string offeredPrefab =
            GetPrefabKey(offeredQuest, offeredKind);
        bool offeredInfested =
            IsInfested(offeredQuest);

        int count = 0;

        for (int i = 0; i < journal.quests.Count; i++)
        {
            Quest completed = journal.quests[i];

            if (!QualifiesCompleted(completed))
                continue;

            RebirthTraderJobCompletionKind completedKind =
                GetKind(completed);

            if (completedKind != offeredKind ||
                RebirthTraderPoiHistory.GetTier(completed) != offeredTier)
                continue;

            if (!string.Equals(
                    GetPrefabKey(completed, completedKind),
                    offeredPrefab,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            // Hide/Surprise must not leak that an offer is infested. In those modes the
            // card shows the combined history for normal + infested versions of the same
            // tier/type/prefab. The stored QuestJournal history remains distinguishable.
            if (!RebirthInfestedJobsRuntimePolicy.HideIdentity &&
                IsInfested(completed) != offeredInfested)
                continue;

            count++;
        }

        return count;
    }

    private static bool QualifiesCompleted(Quest quest)
    {
        if (quest == null ||
            quest.QuestClass == null ||
            quest.CurrentState != Quest.QuestState.Completed)
            return false;

        if (!string.IsNullOrEmpty(quest.QuestClass.QuestType))
            return false;

        return IsRecognizedNormalTraderJob(quest);
    }

    private static string GetPrefabKey(
        Quest quest,
        RebirthTraderJobCompletionKind kind)
    {
        if (kind == RebirthTraderJobCompletionKind.BuriedSupplies)
            return BuriedKey;

        string prefab = string.Empty;

        try
        {
            prefab =
                quest != null
                    ? quest.GetPOIName() ?? string.Empty
                    : string.Empty;
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(prefab) && quest != null)
        {
            RebirthTraderPoiIdentity identity =
                RebirthTraderPoiIdentity.FromQuest(quest);

            prefab = identity.PrefabName ?? string.Empty;
        }

        return RebirthTraderPoiIdentity.Normalize(prefab);
    }
}
