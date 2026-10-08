using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// 3.1-native replacement for EntityTrader.PopulateActiveQuests when REBIRTH asks
/// for more than vanilla's seven normal offers per tier. The vanilla method hard-caps
/// each tier through EntityTrader.distanceIndices.Length (7), so a Postfix cannot
/// increase the list. This Prefix preserves the 3.1 setup/requirement/special-quest
/// paths but raises the candidate count for REBIRTH's expanded fixed-card settings.
/// </summary>
[HarmonyPatch(typeof(EntityTrader), nameof(EntityTrader.PopulateActiveQuests))]
public static class RebirthEntityTraderExpandedJobGenerationPatch
{
    public static bool Prefix(
        EntityTrader __instance,
        EntityPlayer player,
        int currentTier,
        int questFactionPoints,
        ref List<Quest> __result)
    {
        if (__instance == null || player == null)
            return true;

        int requested = RebirthTraderJobPolicy.MaxJobs;
        if (requested <= 0)
            return true;

        // Always use the REBIRTH generator for all supported 5/11/17 board sizes.
        // Even when vanilla's seven slots would be numerically sufficient (MaxJobs=5),
        // vanilla still pre-filters QuestTraderData.CompletedPOIByTier and therefore
        // defeats Repeat POI Jobs = Unlimited before REBIRTH can apply its own policy.
        if (__instance.questDictionary.Count == 0)
        {
            __instance.PopulateQuestList();
            if (__instance.questDictionary.Count == 0)
            {
                __result = null;
                return false;
            }
        }

        bool enemySpawnMode = GameStats.GetBool(EnumGameStats.EnemySpawnMode);
        List<Quest> results = new List<Quest>(Math.Max(16, requested * Math.Max(1, currentTier)));
        __instance.uniqueKeysUsed.Clear();

        Vector2 traderPos = __instance.traderArea == null
            ? new Vector2(__instance.position.x, __instance.position.z)
            : new Vector2(__instance.traderArea.Position.x, __instance.traderArea.Position.z);

        if (currentTier == -1)
            currentTier = player.QuestJournal.GetCurrentFactionTier(__instance.NPCInfo.QuestFaction);
        if (questFactionPoints == -1)
            questFactionPoints = player.QuestJournal.GlobalFactionPoints;

        QuestTraderData traderData = player.QuestJournal.GetTraderData(traderPos);
        if (traderData != null)
            traderData.CheckReset(player);

        __instance.usedPOILocations.Clear();
        List<QuestEntry> eligible = new List<QuestEntry>();
        Dictionary<string, int> questClassCounts = new Dictionary<string, int>();

        // Build a deep candidate pool so post-generation repeat/type/duplicate
        // filtering cannot normally shrink an 11-card board to 9 or 10 cards.
        // REBIRTH 2.6 kept trying candidates until the tier itself was full.
        int candidateTargetPerTier =
            Math.Min(96, Math.Max(requested * 5, requested + 24));

        for (int tier = 1; tier <= currentTier; tier++)
        {
            List<QuestEntry> tierPool;
            if (!__instance.questDictionary.TryGetValue(tier, out tierPool) || tierPool == null)
                continue;

            questClassCounts.Clear();

            // IMPORTANT: REBIRTH owns completed-POI repeat policy.
            //
            // Base 3.1 GetUsedPOIs()/CompletedPOIByTier excludes completed locations
            // BEFORE Quest.SetupPosition returns a candidate. That made
            // Repeat POI Jobs = Unlimited ineffective because REBIRTH never got a
            // chance to see those previously completed POIs.
            //
            // REBIRTH 2.6 intentionally cleared usedPOILocations before SetupPosition
            // and applied its own repeat policy afterward. v95 restores that behavior.
            //
            // Keep traderData.CheckReset() above for vanilla bookkeeping side effects,
            // but do not feed completed POIs into SetupPosition.
            __instance.usedPOILocations.Clear();

            RebirthTraderDebug.Trace(
                "LIST_GENERATION_REPEAT tier=" + tier +
                " policy=" + RebirthTraderJobPolicy.RepeatPoiPolicy +
                " bypassVanillaCompletedPOIs=True");

            eligible.Clear();
            for (int i = 0; i < tierPool.Count; i++)
            {
                QuestEntry entry = tierPool[i];
                if ((entry.StartStage == -1 || entry.StartStage <= questFactionPoints)
                    && (entry.EndStage == -1 || entry.EndStage >= questFactionPoints)
                    && entry.CheckRequirement(player))
                    eligible.Add(entry);
            }

            int addedThisTier = 0;
            int infestedGeneratedThisTier = 0;

            for (int attempt = 0;
                 attempt < 2000 &&
                 eligible.Count != 0 &&
                 addedThisTier < candidateTargetPerTier;
                 attempt++)
            {
                int distancePatternIndex = addedThisTier % __instance.distanceIndices.Length;
                __instance.preferredDistanceIndex = __instance.distanceIndices[distancePatternIndex];

                int poolIndex = __instance.rand.RandomRange(eligible.Count);
                QuestEntry entry = eligible[poolIndex];
                QuestClass questClass = entry.QuestClass;
                int classCount = 0;
                questClassCounts.TryGetValue(questClass.Name, out classCount);

                // Do not consume MaxQuestCount until a quest actually obtains a valid
                // position. A failed SetupPosition/probability roll must not make the
                // expanded board progressively smaller.
                if (questClass.MaxQuestCount != 0 && classCount >= questClass.MaxQuestCount)
                {
                    RemoveQuestClassFromPool(eligible, questClass.Name);
                    continue;
                }

                if (__instance.rand.RandomFloat >= entry.Prob)
                    continue;

                Quest quest = questClass.CreateQuest();
                quest.QuestGiverID = __instance.entityId;
                quest.QuestFaction = __instance.NPCInfo.QuestFaction;
                quest.SetPositionData(Quest.PositionDataTypes.QuestGiver, __instance.position);
                quest.SetPositionData(
                    Quest.PositionDataTypes.TraderPosition,
                    __instance.traderArea != null ? (Vector3)__instance.traderArea.Position : __instance.position);
                quest.SetupTags();

                if (!enemySpawnMode && quest.QuestTags.Test_AnySet(QuestEventManager.clearTag))
                    continue;

                // 2.6 parity: completed vanilla trader POIs never constrain
                // SetupPosition. REBIRTH's listing Postfix applies active/recent
                // repeat restrictions after a valid candidate exists.
                __instance.usedPOILocations.Clear();

                if (quest.SetupPosition(
                        __instance,
                        player,
                        __instance.usedPOILocations,
                        player.entityId))
                {
                    __instance.preferredDistanceIndex = (__instance.preferredDistanceIndex + 1) % 3;
                    int newClassCount = classCount + 1;
                    results.Add(quest);
                    questClassCounts[questClass.Name] = newClassCount;
                    if (questClass.MaxQuestCount != 0 && newClassCount >= questClass.MaxQuestCount)
                        RemoveQuestClassFromPool(eligible, questClass.Name);
                    else if (questClass.SingleQuest)
                        eligible.RemoveAt(poolIndex);
                    addedThisTier++;

                    if (RebirthInfestedJobsRuntimePolicy.IsInfestedId(
                            questClass.ID))
                        infestedGeneratedThisTier++;
                }

                if (quest.QuestTags.Test_AnySet(QuestEventManager.treasureTag) && GameSparksCollector.CollectGamePlayData)
                {
                    GameSparksCollector.IncrementCounter(
                        GameSparksCollector.GSDataKey.QuestOfferedDistance,
                        ((int)Vector3.Distance(quest.Position, __instance.position) / 50 * 50).ToString(),
                        1);
                }
            }

            RebirthTraderDebug.Trace(
                "LIST_GENERATION tier=" + tier +
                " requested=" + requested +
                " candidateTarget=" + candidateTargetPerTier +
                " generated=" + addedThisTier +
                " infestedGenerated=" + infestedGeneratedThisTier +
                " infestedMode=" + RebirthInfestedJobsRuntimePolicy.Mode);
        }

        // Preserve the base 3.1 special/progression quest path. These are later kept
        // outside REBIRTH's MaxJobs normal-job cap.
        for (int i = 0; i < __instance.specialQuestList.Count; i++)
        {
            QuestEntry special = __instance.specialQuestList[i];
            if ((special.StartStage == -1 || special.StartStage <= questFactionPoints)
                && (special.EndStage == -1 || special.EndStage >= questFactionPoints)
                && special.CheckRequirement(player))
            {
                QuestClass questClass = special.QuestClass;
                if ((questClass.UniqueKey == "" || !__instance.uniqueKeysUsed.Contains(questClass.UniqueKey))
                    && (int)questClass.DifficultyTier - 1 <= currentTier
                    && !player.QuestJournal.FindCompletedQuest(
                        questClass.ID,
                        questClass.Repeatable ? (int)__instance.NPCInfo.QuestFaction : -1))
                {
                    for (int attempt = 0; attempt < 100; attempt++)
                    {
                        Quest quest = questClass.CreateQuest();
                        quest.QuestGiverID = __instance.entityId;
                        quest.QuestFaction = __instance.NPCInfo.QuestFaction;
                        quest.SetPositionData(Quest.PositionDataTypes.QuestGiver, __instance.position);
                        quest.SetPositionData(
                            Quest.PositionDataTypes.TraderPosition,
                            __instance.traderArea != null ? (Vector3)__instance.traderArea.Position : __instance.position);
                        quest.SetupTags();
                        if (!quest.NeedsNPCSetPosition || quest.SetupPosition(__instance, player, __instance.usedPOILocations, player.entityId))
                        {
                            results.Add(quest);
                            if (questClass.UniqueKey != "")
                                __instance.uniqueKeysUsed.Add(questClass.UniqueKey);
                            if (GameSparksCollector.CollectGamePlayData)
                            {
                                GameSparksCollector.IncrementCounter(
                                    GameSparksCollector.GSDataKey.QuestTraderToTraderDistance,
                                    ((int)Vector3.Distance(quest.Position, __instance.position) / 50 * 50).ToString(),
                                    1);
                            }
                            break;
                        }
                    }
                }
            }
        }

        // IMPORTANT: do NOT sort this oversized candidate pool here.
        // REBIRTH 2.6 randomly chose the accepted jobs first and only sorted the final
        // visible set by distance. Sorting 50+ candidates before filtering caused the
        // nearest normal variants to starve valid Infested candidates.
        RebirthTraderListRequestCorrelation.StampGeneratedOffers(__instance.entityId, player.entityId, results);
        __result = results;
        return false;
    }
    private static void RemoveQuestClassFromPool(List<QuestEntry> eligible, string className)
    {
        if (eligible == null || string.IsNullOrEmpty(className)) return;
        for (int i = eligible.Count - 1; i >= 0; i--)
        {
            QuestEntry entry = eligible[i];
            QuestClass candidate = entry != null ? entry.QuestClass : null;
            if (candidate != null && string.Equals(candidate.Name, className, StringComparison.Ordinal))
                eligible.RemoveAt(i);
        }
    }

}
