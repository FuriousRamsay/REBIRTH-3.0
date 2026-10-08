using System;
using System.Collections.Generic;
using HarmonyLib;

#nullable disable

public enum RebirthRepeatPoiPolicy
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Unlimited = 4
}

public static class RebirthTraderJobPolicy
{
    public static readonly int[] AllowedMaxJobs = { 5, 11, 17 };
    public static readonly int[] AllowedJobsToNextTier = { 6, 8, 10, 12, 14, 16, 18, 20 };
    public const int DefaultJobsToNextTier = 10;
    private const int BaselineJobs = 11;
    private const string DailyAcceptedDayKey = "RebirthTraderAcceptedDay";
    private static int maxJobs = BaselineJobs;
    private static int jobsToNextTier = DefaultJobsToNextTier;
    private static RebirthRepeatPoiPolicy repeatPoiPolicy = RebirthRepeatPoiPolicy.Medium;

    public static int MaxJobs { get { return maxJobs; } }
    public static int JobsToNextTier { get { return jobsToNextTier; } }
    public static RebirthRepeatPoiPolicy RepeatPoiPolicy { get { return repeatPoiPolicy; } }
    // Exact 2.6 listing-composition rules.
    // Fetch: up to three newly generated satchel jobs, but none when the player
    // already owns an open personal Fetch job.
    // Buried Supplies: maxJobs / 5 (5=>1, 11=>2, 17=>3).
    // Restore Power: fixed maximum of four.
    public static int FetchOfferCap { get { return 3; } }
    public static int BuriedSuppliesOfferCap
    {
        get { return Math.Max(1, maxJobs / 5); }
    }
    public static int RestorePowerOfferCap { get { return 4; } }

    public static void SetMaxJobs(int value) { maxJobs = NormalizeMaxJobs(value); }
    public static void SetJobsToNextTier(int value)
    {
        jobsToNextTier = NormalizeJobsToNextTier(value);
        RebirthVariables.customJobsToNextTier = jobsToNextTier;
    }
    public static void SetRepeatPoiPolicy(RebirthRepeatPoiPolicy value)
    {
        repeatPoiPolicy = value < RebirthRepeatPoiPolicy.None || value > RebirthRepeatPoiPolicy.Unlimited
            ? RebirthRepeatPoiPolicy.Medium : value;
    }

    public static int NormalizeJobsToNextTier(int value)
    {
        int best = AllowedJobsToNextTier[0];
        int distance = Math.Abs(value - best);
        for (int i = 1; i < AllowedJobsToNextTier.Length; i++)
        {
            int candidateDistance = Math.Abs(value - AllowedJobsToNextTier[i]);
            if (candidateDistance < distance)
            {
                best = AllowedJobsToNextTier[i];
                distance = candidateDistance;
            }
        }
        return best;
    }

    public static int JobsToNextTierToIndex(int value)
    {
        value = NormalizeJobsToNextTier(value);
        for (int i = 0; i < AllowedJobsToNextTier.Length; i++)
            if (AllowedJobsToNextTier[i] == value) return i;
        return 2;
    }

    public static bool TryJobsToNextTierFromIndex(int index, out int value)
    {
        if (index < 0 || index >= AllowedJobsToNextTier.Length)
        {
            value = DefaultJobsToNextTier;
            return false;
        }
        value = AllowedJobsToNextTier[index];
        return true;
    }

    public static int GetTierForFactionPoints(int questFactionPoints, int offset = 0, bool allowExtraTierOverMax = false)
    {
        long remaining = Math.Max(0L, (long)questFactionPoints + offset);
        int perTier = Math.Max(1, jobsToNextTier);
        int maximumTier = Math.Max(1, Quest.MaxQuestTier + (allowExtraTierOverMax ? 1 : 0));
        for (int tier = 1; tier <= maximumTier; tier++)
        {
            long threshold = (long)tier * perTier;
            if (remaining < threshold) return tier;
            remaining -= threshold;
        }
        return maximumTier;
    }

    public static int NormalizeMaxJobs(int value)
    {
        int best = AllowedMaxJobs[0];
        int distance = Math.Abs(value - best);
        for (int i = 1; i < AllowedMaxJobs.Length; i++)
        {
            int candidateDistance = Math.Abs(value - AllowedMaxJobs[i]);
            if (candidateDistance < distance) { best = AllowedMaxJobs[i]; distance = candidateDistance; }
        }
        return best;
    }

    public static int MaxJobsToIndex(int value)
    {
        value = NormalizeMaxJobs(value);
        for (int i = 0; i < AllowedMaxJobs.Length; i++) if (AllowedMaxJobs[i] == value) return i;
        return 1;
    }

    // RebirthSandboxCode historically stores MaxJobs as four wire slots. v76 keeps the
    // same slot numbers but retunes the visible capacities to the fixed-card layout:
    // 0=legacy disabled, 1=5, 2=11, 3=17. This preserves saved-world/index compatibility.
    public static int MaxJobsToPersistedIndex(int value)
    {
        value = NormalizeMaxJobs(value);
        if (value == 5) return 1;
        if (value == 17) return 3;
        return 2;
    }

    public static bool TryMaxJobsFromPersistedIndex(int index, out int value)
    {
        switch (index)
        {
            case 0: // Legacy custom Disabled/0 now maps to the lowest supported custom listing.
            case 1:
                value = 5;
                return true;
            case 2:
                value = 11;
                return true;
            case 3:
                value = 17;
                return true;
            default:
                value = BaselineJobs;
                return false;
        }
    }


    public static bool HasOpenPersonalFetchJob(QuestJournal journal)
    {
        if (journal == null || journal.quests == null)
            return false;

        for (int i = 0; i < journal.quests.Count; i++)
        {
            Quest q = journal.quests[i];
            if (q == null ||
                q.SharedOwnerID != -1 ||
                !RebirthTraderJobCompletionStats.IsRecognizedNormalTraderJob(q))
                continue;

            if (q.CurrentState != Quest.QuestState.NotStarted &&
                q.CurrentState != Quest.QuestState.InProgress &&
                q.CurrentState != Quest.QuestState.ReadyForTurnIn)
                continue;

            // 2.6's rule was specifically the satchel Fetch type, not treasure/buried.
            if (q.Objectives == null)
                continue;

            for (int objectiveIndex = 0;
                 objectiveIndex < q.Objectives.Count;
                 objectiveIndex++)
            {
                ObjectiveFetchFromContainer fetch =
                    q.Objectives[objectiveIndex] as ObjectiveFetchFromContainer;

                if (fetch != null)
                    return true;
            }
        }

        return false;
    }

    public static int GetAcceptedJobLimit(int jobTier, bool multiplayerClient)
    {
        // Preserve the 2.6 concurrent-job curve, but key it to the tier of the
        // job being accepted. In 3.1 the base QuestsPerTier option can unlock
        // every trader tier at once; using highest unlocked tier would then
        // collapse every job list to the Tier VI limit of one quest.
        jobTier = Math.Max(1, Math.Min(6, jobTier));
        if (multiplayerClient)
        {
            if (jobTier <= 2) return 3;
            if (jobTier <= 4) return 2;
            return 1;
        }
        return Math.Max(1, 7 - jobTier);
    }

    public static bool IsMultiplayerClient()
    {
        ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return cm != null && cm.IsClient;
    }

    public static int GetDailyQuestLimit()
    {
        return Quest.QuestsPerDay;
    }

    public static int GetEffectiveAcceptedJobLimit(int jobTier)
    {
        int tierLimit = GetAcceptedJobLimit(jobTier, IsMultiplayerClient());
        int dailyLimit = GetDailyQuestLimit();

        return dailyLimit == -1
            ? tierLimit
            : Math.Min(Math.Max(0, dailyLimit), tierLimit);
    }

    public static int CountDailyReservedTraderJobs(QuestJournal journal)
    {
        if (journal == null || journal.quests == null ||
            GameManager.Instance == null || GameManager.Instance.World == null)
            return 0;

        int worldDay = GameManager.Instance.World.WorldDay;
        int count = 0;

        for (int i = 0; i < journal.quests.Count; i++)
        {
            Quest quest = journal.quests[i];
            if (quest == null)
                continue;

            // Match the base progression accounting first: anything already assigned
            // to the current day consumes one daily progression slot.
            if (quest.QuestProgressDay == worldDay)
            {
                count++;
                continue;
            }

            // Reserve a slot for normal personal trader jobs ACCEPTED today. The day is
            // persisted on the Quest itself so yesterday's unfinished jobs do not keep
            // consuming tomorrow's fresh daily allowance.
            if (!IsOpenPersonalTraderJob(quest) || quest.QuestProgressDay > 0)
                continue;

            int acceptedDay;
            if (!TryGetAcceptedDay(quest, out acceptedDay))
            {
                // v84-and-earlier migration: active jobs have no acceptance-day stamp.
                // Treat them as today's reservations once, persist that day, and they
                // naturally stop consuming a slot after the next day rollover.
                acceptedDay = worldDay;
                SetAcceptedDay(quest, acceptedDay);
            }

            if (acceptedDay == worldDay)
                count++;
        }

        return count;
    }

    public static void StampAcceptedDay(Quest quest)
    {
        if (!IsOpenPersonalTraderJob(quest) ||
            GameManager.Instance == null || GameManager.Instance.World == null)
            return;

        SetAcceptedDay(quest, GameManager.Instance.World.WorldDay);
    }

    private static bool TryGetAcceptedDay(Quest quest, out int day)
    {
        day = int.MinValue;
        if (quest == null || quest.DataVariables == null)
            return false;

        string raw;
        return quest.DataVariables.TryGetValue(DailyAcceptedDayKey, out raw) &&
               int.TryParse(raw, out day);
    }

    private static void SetAcceptedDay(Quest quest, int day)
    {
        if (quest == null)
            return;
        if (quest.DataVariables == null)
            quest.DataVariables = new Dictionary<string, string>();

        quest.DataVariables[DailyAcceptedDayKey] = day.ToString();
    }

    public static bool CanUseDailyQuestSlot(QuestJournal journal, int additionalReservedJobs = 0)
    {
        if (journal == null)
            return false;

        int limit = GetDailyQuestLimit();
        if (limit == -1)
            return true;

        return CountDailyReservedTraderJobs(journal) + Math.Max(0, additionalReservedJobs) < Math.Max(0, limit);
    }

    public static bool CanAcceptAnotherJobAtTier(QuestJournal journal, int jobTier, int additionalOpenJobs = 0)
    {
        if (journal == null) return false;
        if (!CanUseDailyQuestSlot(journal, additionalOpenJobs)) return false;

        int limit = GetAcceptedJobLimit(jobTier, IsMultiplayerClient());
        return CountOpenPersonalTraderJobs(journal) + Math.Max(0, additionalOpenJobs) < limit;
    }

    public static int GetHighestUnlockedJobTier(EntityPlayer player, EntityTrader trader)
    {
        if (player == null || trader == null || trader.NPCInfo == null ||
            player.QuestJournal == null)
            return 0;

        return Math.Max(
            1,
            Math.Min(
                6,
                player.QuestJournal.GetCurrentFactionTier(
                    trader.NPCInfo.QuestFaction)));
    }

    public static int GetHighestTierWithAcceptanceRoom(
        EntityPlayer player,
        EntityTrader trader)
    {
        if (player == null || trader == null)
            return 0;

        QuestJournal journal = player.QuestJournal;
        if (journal == null || !CanUseDailyQuestSlot(journal))
            return 0;

        int highest = GetHighestUnlockedJobTier(player, trader);
        for (int tier = highest; tier >= 1; tier--)
            if (CanAcceptAnotherJobAtTier(journal, tier))
                return tier;

        return 0;
    }

    public static bool CanAcceptAnyListedJob(EntityPlayer player, EntityTrader trader)
    {
        // Never derive Jobs visibility from trader.activeQuests. In Random mode the
        // cache is transient and accepted cards are removed from it. Capacity depends
        // only on unlocked tiers, the daily allowance, and the player's open jobs.
        return GetHighestTierWithAcceptanceRoom(player, trader) > 0;
    }

    private static bool IsOpenPersonalTraderJob(Quest quest)
    {
        if (quest == null || quest.SharedOwnerID != -1)
            return false;

        if (!RebirthTraderJobCompletionStats.IsRecognizedNormalTraderJob(quest))
            return false;

        return quest.CurrentState == Quest.QuestState.NotStarted ||
               quest.CurrentState == Quest.QuestState.InProgress ||
               quest.CurrentState == Quest.QuestState.ReadyForTurnIn;
    }

    public static int CountOpenPersonalTraderJobs(QuestJournal journal)
    {
        if (journal == null || journal.quests == null) return 0;
        int count = 0;
        for (int i = 0; i < journal.quests.Count; i++)
        {
            if (IsOpenPersonalTraderJob(journal.quests[i]))
                count++;
        }
        return count;
    }
}

public struct RebirthTraderPoiIdentity
{
    public string PrefabName;
    public string PhysicalPoiKey;
    public bool HasPoi { get { return !string.IsNullOrEmpty(PrefabName); } }
    public static RebirthTraderPoiIdentity FromQuest(Quest quest)
    {
        if (quest == null) return default(RebirthTraderPoiIdentity);
        string raw = quest.QuestPrefab != null ? quest.QuestPrefab.name : quest.GetPOIName();
        string normalized = Normalize(raw);
        return new RebirthTraderPoiIdentity { PrefabName = normalized, PhysicalPoiKey = normalized.Length == 0 ? string.Empty : normalized + "@" + quest.Position.x + "," + quest.Position.y + "," + quest.Position.z };
    }
    public static string Normalize(string value) { return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant(); }
}

public sealed class RebirthTraderPoiHistorySnapshot
{
    private readonly HashSet<string> activePhysical;
    private readonly Dictionary<string, int> prefabAge;
    private readonly Dictionary<string, int> physicalAge;

    public RebirthTraderPoiHistorySnapshot(HashSet<string> active, Dictionary<string, int> prefab, Dictionary<string, int> physical)
    {
        activePhysical = active ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        prefabAge = prefab ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        physicalAge = physical ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsActivePhysical(string key) { return !string.IsNullOrEmpty(key) && activePhysical.Contains(key); }
    public int GetPrefabAge(string name) { int age; return !string.IsNullOrEmpty(name) && prefabAge.TryGetValue(name, out age) ? age : int.MaxValue; }
    public int GetPhysicalAge(string key) { int age; return !string.IsNullOrEmpty(key) && physicalAge.TryGetValue(key, out age) ? age : int.MaxValue; }
}

public static class RebirthTraderPoiHistory
{
    private static readonly int[] LowWindows = { 0, 16, 14, 12, 10, 8, 6 };
    private static readonly int[] MediumWindows = { 0, 8, 6, 5, 4, 3, 2 };
    private static readonly int[] HighWindows = { 0, 3, 3, 2, 2, 1, 1 };

    public static int GetWindow(RebirthRepeatPoiPolicy policy, int tier)
    {
        tier = Math.Max(1, Math.Min(6, tier));
        if (policy == RebirthRepeatPoiPolicy.Unlimited) return 0;
        if (policy == RebirthRepeatPoiPolicy.None) return 64;
        if (policy == RebirthRepeatPoiPolicy.Low) return LowWindows[tier];
        if (policy == RebirthRepeatPoiPolicy.High) return HighWindows[tier];
        return MediumWindows[tier];
    }

    public static RebirthTraderPoiHistorySnapshot Build(QuestJournal journal, int candidateTier)
    {
        HashSet<string> active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> prefab = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> physical = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (journal != null && journal.quests != null)
        {
            for (int i = journal.quests.Count - 1; i >= 0; i--)
            {
                Quest quest = journal.quests[i];
                if (!QualifiesAsPersonalTraderPoi(quest)) continue;
                RebirthTraderPoiIdentity identity = RebirthTraderPoiIdentity.FromQuest(quest);
                if (!identity.HasPoi) continue;
                if (quest.CurrentState == Quest.QuestState.NotStarted || quest.CurrentState == Quest.QuestState.InProgress || quest.CurrentState == Quest.QuestState.ReadyForTurnIn)
                    active.Add(identity.PhysicalPoiKey);
                // Completed history is ingested by completion/spawn lifecycle hooks.
                // Build is intentionally observational so diagnostics/list reads cannot rewrite persistence.
            }
        }
        int window = GetWindow(RebirthTraderJobPolicy.RepeatPoiPolicy, candidateTier);
        if (window > 0) RebirthTraderPoiPersistentHistory.AddRecentHistory(journal, candidateTier, window, prefab, physical);
        return new RebirthTraderPoiHistorySnapshot(active, prefab, physical);
    }

    public static bool QualifiesAsPersonalTraderPoi(Quest quest)
    {
        if (quest == null || quest.SharedOwnerID != -1)
            return false;

        // Do not depend on QuestGiverID resolving to a currently loaded trader entity.
        // Completed/active trader jobs remain recognizable from their persisted quest
        // identity and TraderPosition data.
        if (!RebirthTraderJobCompletionStats.IsRecognizedNormalTraderJob(quest))
            return false;

        if (quest.QuestTags.Test_AnySet(QuestEventManager.treasureTag) &&
            !string.IsNullOrEmpty(quest.ID) &&
            quest.ID.IndexOf(
                "buried_supplies",
                StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        return true;
    }
    public static int GetTier(Quest quest) { return quest == null || quest.QuestClass == null ? 1 : Math.Max(1, Math.Min(6, (int)quest.QuestClass.DifficultyTier)); }
}

public sealed class RebirthTraderCandidate
{
    public Quest Quest;
    public RebirthTraderPoiIdentity Identity;
    public int SourceIndex;
    public int Pass;
    public int Age;
}

/// <summary>
/// Restores the REBIRTH 2.6 trader-tier threshold calculation while keeping the
/// current 3.0 quest-faction point storage. Each tier requires the configured
/// number of same-tier jobs because a Tier N completion contributes N faction points.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.GetCurrentFactionTier), new Type[] { typeof(byte), typeof(int), typeof(bool) })]
public static class RebirthTraderJobsToNextTierPatch
{
    [HarmonyPrefix]
    public static bool Prefix(
        QuestJournal __instance,
        ref int __result,
        byte id,
        int offset = 0,
        bool allowExtraTierOverMax = false)
    {
        if (__instance == null)
        {
            __result = 1;
            return false;
        }

        __result = RebirthTraderJobPolicy.GetTierForFactionPoints(
            __instance.GetQuestFactionPoints(id),
            offset,
            allowExtraTierOverMax);
        return false;
    }
}

[HarmonyPatch(typeof(EntityTrader), nameof(EntityTrader.PopulateActiveQuests))]
public static class RebirthEntityTraderJobListingPatch
{
    public static void Postfix(EntityTrader __instance, EntityPlayer player, int currentTier, ref List<Quest> __result)
    {
        if (__result == null) return;

        QuestJournal journal = player != null ? player.QuestJournal : null;
        int max = RebirthSandboxOptionManager.Current.MaxJobs;
        Dictionary<int, RebirthTraderPoiHistorySnapshot> historyByTier = new Dictionary<int, RebirthTraderPoiHistorySnapshot>();
        List<Quest> specials = new List<Quest>();
        List<RebirthTraderCandidate> ranked = new List<RebirthTraderCandidate>(__result.Count);

        for (int i = 0; i < __result.Count; i++)
        {
            Quest quest = __result[i];
            if (quest == null || quest.QuestClass == null) continue;

            // Progression/special jobs are deliberately outside the normal MaxJobs cap.
            if (string.Equals(quest.QuestClass.QuestType, "special", StringComparison.OrdinalIgnoreCase))
            {
                specials.Add(quest);
                continue;
            }

            RebirthTraderPoiIdentity id = RebirthTraderPoiIdentity.FromQuest(quest);
            int candidateTier = RebirthTraderPoiHistory.GetTier(quest);
            RebirthTraderPoiHistorySnapshot history;
            if (!historyByTier.TryGetValue(candidateTier, out history))
            {
                history = RebirthTraderPoiHistory.Build(journal, candidateTier);
                historyByTier.Add(candidateTier, history);

                RebirthTraderDebug.Trace(
                    "REPEAT_POLICY tier=" + candidateTier +
                    " policy=" + RebirthTraderJobPolicy.RepeatPoiPolicy +
                    " completedWindow=" +
                    RebirthTraderPoiHistory.GetWindow(
                        RebirthTraderJobPolicy.RepeatPoiPolicy,
                        candidateTier));
            }
            if (id.HasPoi && history.IsActivePhysical(id.PhysicalPoiKey)) continue;

            int prefabAge = history.GetPrefabAge(id.PrefabName);
            int physicalAge = history.GetPhysicalAge(id.PhysicalPoiKey);
            int pass = !id.HasPoi || prefabAge == int.MaxValue ? 1 : physicalAge == int.MaxValue ? 2 : 3;
            ranked.Add(new RebirthTraderCandidate
            {
                Quest = quest,
                Identity = id,
                SourceIndex = i,
                Pass = pass,
                Age = pass == 2 ? prefabAge : physicalAge
            });
        }
        ranked.Sort(CompareCandidates);

        int fetch = 0, buried = 0, restore = 0;

        // Exact 2.6 Fetch rule: if the player already owns an open personal Fetch job,
        // newly generated lists contain zero additional satchel Fetch offers.
        int fetchMax =
            RebirthTraderJobPolicy.HasOpenPersonalFetchJob(journal)
                ? 0
                : RebirthTraderJobPolicy.FetchOfferCap;

        int buriedMax = RebirthTraderJobPolicy.BuriedSuppliesOfferCap;
        int restoreMax = RebirthTraderJobPolicy.RestorePowerOfferCap;

        List<Quest> filtered =
            new List<Quest>(ranked.Count + specials.Count);

        Dictionary<int, int> acceptedByTier =
            new Dictionary<int, int>();

        Dictionary<int, int> infestedAcceptedByTier =
            new Dictionary<int, int>();

        Dictionary<int, int> infestedCandidatesByTier =
            new Dictionary<int, int>();

        Dictionary<int, int> candidatesByTier =
            new Dictionary<int, int>();

        HashSet<string> offeredPhysical =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        HashSet<string> offeredKeys =
            new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < ranked.Count; i++)
        {
            int candidateTier =
                RebirthTraderPoiHistory.GetTier(ranked[i].Quest);

            int candidateCount;
            candidatesByTier.TryGetValue(
                candidateTier,
                out candidateCount);

            candidatesByTier[candidateTier] =
                candidateCount + 1;

            if (RebirthInfestedJobsRuntimePolicy.IsInfestedId(
                    ranked[i].Quest.QuestClass.ID))
            {
                int infestedCount;
                infestedCandidatesByTier.TryGetValue(
                    candidateTier,
                    out infestedCount);

                infestedCandidatesByTier[candidateTier] =
                    infestedCount + 1;
            }
        }

        if (max > 0)
        {
            for (int i = 0; i < ranked.Count; i++)
            {
                RebirthTraderCandidate candidate = ranked[i];
                Quest quest = candidate.Quest;
                int tier = RebirthTraderPoiHistory.GetTier(quest);

                int tierCount;
                acceptedByTier.TryGetValue(tier, out tierCount);

                if (tierCount >= max)
                    continue;

                string physical =
                    candidate.Identity.PhysicalPoiKey;

                string offerKey =
                    RebirthTraderOfferSnapshotService.GetOfferKey(quest);

                if (!string.IsNullOrEmpty(physical) &&
                    offeredPhysical.Contains(physical))
                    continue;

                if (offeredKeys.Contains(offerKey))
                    continue;

                bool isFetch =
                    quest.QuestTags.Test_AnySet(
                        QuestEventManager.fetchTag);

                bool isTreasure =
                    quest.QuestTags.Test_AnySet(
                        QuestEventManager.treasureTag);

                bool isBuried =
                    isTreasure &&
                    !string.IsNullOrEmpty(quest.ID) &&
                    quest.ID.IndexOf(
                        "buried_supplies",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                bool isRestore =
                    quest.QuestTags.Test_AnySet(
                        QuestEventManager.restorePowerTag);

                // HARD 2.6 caps. Do not relax these to fill the board.
                if (isBuried)
                {
                    if (buried >= buriedMax)
                        continue;
                }
                else if (isRestore)
                {
                    if (restore >= restoreMax)
                        continue;
                }
                else if (isFetch)
                {
                    if (fetch >= fetchMax)
                        continue;
                }

                if (!string.IsNullOrEmpty(physical))
                    offeredPhysical.Add(physical);

                offeredKeys.Add(offerKey);

                if (isBuried)
                    buried++;
                else if (isRestore)
                    restore++;
                else if (isFetch)
                    fetch++;

                filtered.Add(quest);
                acceptedByTier[tier] =
                    tierCount + 1;

                if (RebirthInfestedJobsRuntimePolicy.IsInfestedId(
                        quest.QuestClass.ID))
                {
                    int infestedAccepted;
                    infestedAcceptedByTier.TryGetValue(
                        tier,
                        out infestedAccepted);

                    infestedAcceptedByTier[tier] =
                        infestedAccepted + 1;
                }
            }
        }

        if (RebirthTraderDebug.Enabled)
        {
            foreach (KeyValuePair<int, int> pair in candidatesByTier)
            {
                int finalCount;
                acceptedByTier.TryGetValue(
                    pair.Key,
                    out finalCount);

                int infestedCandidates = 0;
                int infestedFinal = 0;

                infestedCandidatesByTier.TryGetValue(
                    pair.Key,
                    out infestedCandidates);

                infestedAcceptedByTier.TryGetValue(
                    pair.Key,
                    out infestedFinal);

                RebirthTraderDebug.Trace(
                    "LIST_FILTER tier=" + pair.Key +
                    " candidates=" + pair.Value +
                    " requested=" + max +
                    " final=" + finalCount +
                    " infestedCandidates=" + infestedCandidates +
                    " infestedFinal=" + infestedFinal +
                    " fetch=" + fetch + "/" + fetchMax +
                    " buried=" + buried + "/" + buriedMax +
                    " restore=" + restore + "/" + restoreMax);
            }
        }

        // 2.6 parity: random/type-valid selection happens first, then only the
        // accepted normal offers are sorted by distance for presentation.
        filtered.Sort(
            (a, b) =>
                (a.Position - player.position).sqrMagnitude.CompareTo(
                    (b.Position - player.position).sqrMagnitude));

        // Special progression jobs remain available independently of MaxJobs.
        for (int i = 0; i < specials.Count; i++)
        {
            Quest special = specials[i];
            if (special != null && offeredKeys.Add(RebirthTraderOfferSnapshotService.GetOfferKey(special)))
                filtered.Add(special);
        }

        __result = filtered;
        RebirthTraderOfferSnapshotService.Issue(
            journal,
            __instance != null ? __instance.entityId : 0,
            __result);
    }

    private static int CompareCandidates(RebirthTraderCandidate a, RebirthTraderCandidate b)
    {
        int result = a.Pass.CompareTo(b.Pass);
        if (result != 0) return result;
        if (a.Pass > 1)
        {
            result = b.Age.CompareTo(a.Age);
            if (result != 0) return result;
        }
        return a.SourceIndex.CompareTo(b.SourceIndex);
    }
}

[HarmonyPatch(typeof(XUiC_QuestOfferWindow), "btnAccept_OnPress")]
public static class RebirthTraderQuestOfferAcceptPreflightPatch
{
    public static bool Prefix(XUiC_QuestOfferWindow __instance)
    {
        if (__instance == null || __instance.OfferType != XUiC_QuestOfferWindow.OfferTypes.Dialog)
            return true;

        Quest q = __instance.Quest;
        if (q == null) return true;

        // Special dialog quests use native confirmation and progression.
        // Normal job snapshot and capacity restrictions do not apply to them.
        if (q.QuestClass != null && !string.IsNullOrEmpty(q.QuestClass.QuestType))
            return true;

        // Base 3.1 assigns the giver again immediately after this prefix. Mirror that
        // value here so validation is performed against the exact trader being used.
        if (__instance.QuestGiverID != -1)
            q.QuestGiverID = __instance.QuestGiverID;

        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer()
            : null;
        QuestJournal journal = player != null ? player.QuestJournal : null;
        if (journal == null || !RebirthUtilities.IsVanillaTrader(q.QuestGiverID) || q.SharedOwnerID != -1)
            return true;

        RebirthTraderOfferValidation validation = RebirthTraderOfferSnapshotService.Validate(journal, q);
        if (validation != RebirthTraderOfferValidation.Allowed)
        {
            GameManager.ShowTooltip(player, Localization.Get("xuiRebirthTraderOfferStale"), string.Empty, "ui_denied");
            return false;
        }

        int tier = RebirthTraderPoiHistory.GetTier(q);
        RebirthTraderPoiIdentity identity = RebirthTraderPoiIdentity.FromQuest(q);
        if (identity.HasPoi && RebirthTraderPoiHistory.Build(journal, tier).IsActivePhysical(identity.PhysicalPoiKey))
        {
            GameManager.ShowTooltip(player, Localization.Get("xuiRebirthRepeatPoiJobRejected"), string.Empty, "ui_denied");
            return false;
        }

        if (!RebirthTraderJobPolicy.CanUseDailyQuestSlot(journal))
        {
            RebirthTraderDebug.Trace(
                "QUEST_OFFER preflight daily limit -> silent reject");
            return false;
        }

        int limit = RebirthTraderJobPolicy.GetAcceptedJobLimit(
            tier,
            RebirthTraderJobPolicy.IsMultiplayerClient());

        if (RebirthTraderJobPolicy.CountOpenPersonalTraderJobs(journal) >= limit)
        {
            RebirthTraderDebug.Trace(
                "QUEST_OFFER preflight tier limit=" + limit +
                " -> silent reject");
            return false;
        }

        // All REBIRTH checks have passed while the offer window is still open.
        // Vanilla may now close the window, remove the offer, clone/reset the quest
        // as appropriate and commit it through QuestJournal.AddQuest().
        return true;
    }
}

[HarmonyPatch(typeof(XUiC_QuestOfferWindow), nameof(XUiC_QuestOfferWindow.OnClose))]
public static class RebirthTraderQuestOfferReturnToListPatch
{
    public struct ReturnState
    {
        public Action<EntityNPC> DeniedCallback;
        public bool ForceCloseDialog;
    }

    public static void Prefix(XUiC_QuestOfferWindow __instance, ref ReturnState __state)
    {
        if (__instance == null || __instance.OfferType != XUiC_QuestOfferWindow.OfferTypes.Dialog)
            return;

        Quest q = __instance.Quest;
        if (q == null || q.QuestClass == null || !string.IsNullOrEmpty(q.QuestClass.QuestType))
            return; // special/non-standard dialog quests keep vanilla flow

        int giverId = __instance.QuestGiverID != -1 ? __instance.QuestGiverID : q.QuestGiverID;
        if (!RebirthUtilities.IsVanillaTrader(giverId))
            return;

        EntityPlayerLocal player = __instance.xui != null && __instance.xui.playerUI != null
            ? __instance.xui.playerUI.entityPlayer
            : null;
        QuestJournal journal = player != null ? player.QuestJournal : null;
        if (journal == null)
            return;

        int tier = RebirthTraderPoiHistory.GetTier(q);
        int projectedAdditional = __instance.questAccepted ? 1 : 0;
        bool dailyRoomAfterClose = RebirthTraderJobPolicy.CanUseDailyQuestSlot(journal, projectedAdditional);
        bool mayTakeAnother = dailyRoomAfterClose &&
            RebirthTraderJobPolicy.CanAcceptAnotherJobAtTier(journal, tier, projectedAdditional);

        // Only return to the same tier while another job of that tier is actually
        // allowed AND the daily quest budget still has a free slot.
        if (mayTakeAnother)
        {
            __instance.xui.Dialog.ReturnStatement = "currentjobs" + tier;
            return;
        }

        // Once this acceptance/close exhausts the daily limit, leave the trader dialog
        // entirely. Base currentjobsN statements normally point back to "start", so an
        // empty ReturnStatement alone is not enough; Postfix explicitly closes dialog.
        __state.ForceCloseDialog = !dailyRoomAfterClose;

        // On decline, vanilla's OnDenied callback would otherwise force the same job
        // statement back open even when no further job can be accepted.
        if (!__instance.questAccepted)
        {
            __state.DeniedCallback = __instance.OnDenied;
            __instance.OnDenied = null;
        }
        __instance.xui.Dialog.ReturnStatement = string.Empty;
    }

    public static void Postfix(XUiC_QuestOfferWindow __instance, ReturnState __state)
    {
        if (__instance == null)
            return;

        if (__state.DeniedCallback != null)
            __instance.OnDenied = __state.DeniedCallback;

        if (__state.ForceCloseDialog &&
            __instance.xui != null &&
            __instance.xui.playerUI != null &&
            __instance.xui.playerUI.windowManager != null)
        {
            __instance.xui.playerUI.windowManager.Close("dialog");
        }
    }
}

/// <summary>
/// Separate the daily acceptance budget from progression credit.
///
/// Base 3.1 writes QuestProgressDay=-1 after the daily progression limit has been
/// reached. QuestJournal.CompleteQuest then checks Quest.AddsProgression and silently
/// skips the trader-tier point. REBIRTH uses the daily limit only to control whether
/// another normal trader job may be accepted; a quest that actually reaches the
/// progression stage always receives a positive progression day.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.HandleQuestCompleteToday))]
public static class RebirthTraderProgressionDayCreditPatch
{
    public static bool Prefix(QuestJournal __instance, Quest q)
    {
        if (__instance == null || q == null ||
            GameManager.Instance == null || GameManager.Instance.World == null)
            return true;

        int worldDay = GameManager.Instance.World.WorldDay;
        q.QuestProgressDay = worldDay;
        __instance.ResetAddToProgression();

        // REBIRTH controls job availability in the trader dialogue. Do not add the
        // vanilla persistent toolbelt "Quest Limit" buff/notification.
        if (__instance.OwnerPlayer != null)
            __instance.OwnerPlayer.Buffs.RemoveBuff("buffShowQuestLimitReached");

        return false;
    }
}

/// <summary>
/// Suppress the vanilla persistent toolbelt Quest Limit buff. The actual acceptance
/// limit is represented by the trader Jobs entry/card status instead.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.Update))]
public static class RebirthTraderQuestLimitBuffSuppressPatch
{
    [HarmonyPrefix]
    public static bool Prefix(
        QuestJournal __instance,
        int worldDay)
    {
        if (__instance == null)
            return false;

        if (__instance.previousDay == worldDay)
        {
            if (__instance.OwnerPlayer != null)
                __instance.OwnerPlayer.Buffs
                    .RemoveBuff("buffShowQuestLimitReached");

            return false;
        }

        __instance.previousDay = worldDay;
        __instance.ResetAddToProgression();

        if (__instance.OwnerPlayer != null)
            __instance.OwnerPlayer.Buffs
                .RemoveBuff("buffShowQuestLimitReached");

        return false;
    }
}

/// <summary>
/// Compatibility/safety net for quests that were already marked QuestProgressDay=-1
/// before this fix (for example a ReadyForTurnIn quest carried across an upgrade).
/// Set a valid day immediately before vanilla CompleteQuest checks AddsProgression.
/// Vanilla still performs the actual AddQuestFactionPoint exactly once.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.CompleteQuest))]
public static class RebirthTraderCompletionCreditSafetyPatch
{
    public static void Prefix(Quest q)
    {
        if (q == null || q.QuestClass == null || !q.QuestClass.AddsToTierComplete ||
            q.QuestProgressDay > 0 ||
            GameManager.Instance == null || GameManager.Instance.World == null)
            return;

        q.QuestProgressDay = GameManager.Instance.World.WorldDay;
    }
}

[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.AddQuest))]
public static class RebirthQuestJournalAcceptedTraderJobLimitPatch
{
    // Acceptance rejection no longer belongs here. Base 3.1 closes the quest-offer
    // window and removes the trader offer BEFORE calling QuestJournal.AddQuest().
    // Returning false from an AddQuest prefix therefore loses the offer without
    // giving the quest. The preflight patch above performs every rejection before
    // vanilla is allowed to close the offer window.
    public static void Postfix(QuestJournal __instance, Quest q)
    {
        if (__instance == null || q == null || __instance.OwnerPlayer == null) return;
        if (!RebirthUtilities.IsVanillaTrader(q.QuestGiverID) || q.SharedOwnerID != -1) return;

        // Persist the acceptance day on the quest. This makes the configured daily
        // limit an actual per-day acceptance budget instead of a permanent penalty
        // for unfinished jobs carried across midnight.
        RebirthTraderJobPolicy.StampAcceptedDay(q);

        if (__instance.FindActiveQuest(q.QuestCode) != null)
            RebirthTraderOfferSnapshotService.Consume(__instance, q);
    }
}

public static class RebirthFetchQuestIdentity
{
    public const string MetadataKey = "RebirthFetchQuestCode";

    public static void Bind(ObjectiveBaseFetchContainer objective)
    {
        if (objective == null || objective.OwnerQuest == null || objective.expectedItem == null) return;
        int code = objective.OwnerQuest.QuestCode;
        objective.expectedItem.Meta = code;
        objective.expectedItem.SetMetadata(MetadataKey, code);
    }

    public static bool Matches(ItemValue value, ObjectiveBaseFetchContainer objective)
    {
        if (value == null || objective == null || objective.expectedItem == null || objective.OwnerQuest == null) return false;
        if (value.type != objective.expectedItem.type || value.Seed != objective.expectedItem.Seed) return false;
        int code = objective.OwnerQuest.QuestCode;
        if (value.Meta == code) return true;
        int stored;
        return value.TryGetMetadata(MetadataKey, out stored) && stored == code;
    }

    public static int Count(ItemStack[] slots, ObjectiveBaseFetchContainer objective)
    {
        if (slots == null) return 0;
        int count = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) continue;
            if (Matches(stack.itemValue, objective)) count += stack.count;
        }
        return count;
    }

    public static bool TryFindActiveFetchForPosition(
        QuestJournal journal,
        Vector3i containerPosition,
        out Quest matchedQuest,
        out ObjectiveFetchFromContainer matchedObjective)
    {
        matchedQuest = null;
        matchedObjective = null;
        if (journal == null || journal.quests == null)
            return false;

        // First prefer an objective that already knows this exact fetch-container position.
        for (int i = 0; i < journal.quests.Count; i++)
        {
            Quest quest = journal.quests[i];
            if (!IsCandidateQuest(quest))
                continue;

            ObjectiveFetchFromContainer objective = FindFetchObjective(quest);
            if (objective == null)
                continue;

            Vector3 existing;
            Quest.PositionDataTypes dataType =
                objective.FetchMode == ObjectiveFetchFromContainer.FetchModeTypes.Standard
                    ? Quest.PositionDataTypes.FetchContainer
                    : Quest.PositionDataTypes.HiddenCache;

            if (quest.GetPositionData(out existing, dataType) &&
                new Vector3i(existing) == containerPosition)
            {
                matchedQuest = quest;
                matchedObjective = objective;
                return true;
            }
        }

        // A remote 3.1 client receives only the selected container position. Base
        // QuestJournal.SetActivePositionData sends that position to every active,
        // rally-activated quest. Route it instead to the one active fetch quest whose
        // assigned POI contains the selected container.
        Quest bestQuest = null;
        ObjectiveFetchFromContainer bestObjective = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < journal.quests.Count; i++)
        {
            Quest quest = journal.quests[i];
            if (!IsCandidateQuest(quest))
                continue;

            ObjectiveFetchFromContainer objective = FindFetchObjective(quest);
            if (objective == null)
                continue;

            UnityEngine.Rect rect = quest.GetLocationRect();
            if (rect.width <= 0f || rect.height <= 0f ||
                !rect.Contains(new UnityEngine.Vector2(containerPosition.x, containerPosition.z)))
                continue;

            UnityEngine.Vector3 location = quest.GetLocation();
            float dx = containerPosition.x - location.x;
            float dz = containerPosition.z - location.z;
            float distance = dx * dx + dz * dz;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestQuest = quest;
                bestObjective = objective;
            }
        }

        if (bestQuest == null || bestObjective == null)
            return false;

        matchedQuest = bestQuest;
        matchedObjective = bestObjective;
        return true;
    }

    private static bool IsCandidateQuest(Quest quest)
    {
        return quest != null &&
               quest.Active &&
               quest.RallyMarkerActivated &&
               quest.Objectives != null;
    }

    private static ObjectiveFetchFromContainer FindFetchObjective(Quest quest)
    {
        if (quest == null || quest.Objectives == null)
            return null;

        for (int i = 0; i < quest.Objectives.Count; i++)
        {
            ObjectiveFetchFromContainer objective = quest.Objectives[i] as ObjectiveFetchFromContainer;
            if (objective != null &&
                (objective.Phase == 0 || objective.Phase == quest.CurrentPhase) &&
                !objective.Complete)
                return objective;
        }
        return null;
    }

    public static bool StorageContainsBoundItem(TEFeatureStorage storage, ObjectiveBaseFetchContainer objective)
    {
        if (storage == null || storage.ItemGrid.items == null || objective == null)
            return false;

        for (int i = 0; i < storage.ItemGrid.items.Length; i++)
        {
            ItemStack stack = storage.ItemGrid.items[i];
            if (stack == null || stack.IsEmpty())
                continue;
            if (Matches(stack.itemValue, objective))
                return true;
        }
        return false;
    }
}

/// <summary>
/// 3.1 remote-client fetch routing fix.
///
/// NetPackageQuestEvent.SetupFetch calls QuestJournal.SetActivePositionData without a
/// QuestCode. Vanilla then applies the selected container position to every active,
/// rally-activated quest. With multiple simultaneous Fetch jobs that can point several
/// objectives at the same satchel. REBIRTH resolves the owning quest by the assigned POI
/// rectangle and applies the position only to that quest.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.SetActivePositionData))]
public static class RebirthFetchPositionRoutingPatch
{
    public static bool Prefix(
        QuestJournal __instance,
        Quest.PositionDataTypes dataType,
        Vector3i position)
    {
        if (dataType != Quest.PositionDataTypes.FetchContainer &&
            dataType != Quest.PositionDataTypes.HiddenCache)
            return true;

        Quest quest;
        ObjectiveFetchFromContainer objective;
        if (!RebirthFetchQuestIdentity.TryFindActiveFetchForPosition(
                __instance, position, out quest, out objective))
        {
            // Do not fall back to vanilla's broadcast-to-all behavior. If routing cannot
            // be resolved, leaving the objectives untouched is safer than cross-linking
            // multiple active Fetch quests to one container.
            Log.Warning("[REBIRTH] Could not route fetch-container position to a unique active Fetch quest at " + position + ".");
            return false;
        }

        quest.SetObjectivePosition(dataType, position);
        return false;
    }
}

/// <summary>
/// Dedicated-server half of the Fetch identity fix.
///
/// The quest satchel loot container is server-owned. Stamp/add the quest item on the
/// authoritative TE using the active quest whose POI contains this container, before the
/// client starts moving items. This preserves QuestCode identity on dedicated servers and
/// prevents a satchel from one Fetch job from satisfying another.
/// </summary>
[HarmonyPatch(typeof(TEFeatureStorage), nameof(TEFeatureStorage.OnLockResponseServer), new Type[] { typeof(int), typeof(PooledBinaryWriter), typeof(ushort) })]
public static class RebirthFetchServerStorageIdentityPatch
{
    public static void Prefix(TEFeatureStorage __instance, int _lockingPlayerID)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (__instance == null || connection == null || !connection.IsServer ||
            _lockingPlayerID < 0 || GameManager.Instance == null ||
            GameManager.Instance.World == null)
            return;

        // Only quest fetch satchels need this identity injection.
        BlockValue blockValue = GameManager.Instance.World.GetBlock(__instance.ToWorldPos());
        Block block = blockValue.Block;
        if (block == null ||
            (!string.Equals(block.GetBlockName(), "cntFetchQuestSatchel", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(block.IndexName, "FetchContainer", StringComparison.OrdinalIgnoreCase)))
            return;

        EntityPlayer player = GameManager.Instance.World.GetEntity(_lockingPlayerID) as EntityPlayer;
        QuestJournal journal = player != null ? player.QuestJournal : null;
        if (journal == null)
            return;

        Quest quest;
        ObjectiveFetchFromContainer objective;
        if (!RebirthFetchQuestIdentity.TryFindActiveFetchForPosition(
                journal, __instance.ToWorldPos(), out quest, out objective))
            return;

        RebirthFetchQuestIdentity.Bind(objective);
        if (RebirthFetchQuestIdentity.StorageContainsBoundItem(__instance, objective))
            return;

        ItemValue bound = objective.expectedItem.Clone();
        bound.Meta = quest.QuestCode;
        bound.SetMetadata(RebirthFetchQuestIdentity.MetadataKey, quest.QuestCode);
        __instance.AddItem(new ItemStack(bound, 1));
        __instance.SetModified();
    }
}

[HarmonyPatch(typeof(ObjectiveBaseFetchContainer), "SetupExpectedItem")]
public static class RebirthFetchSetupIdentityPatch
{
    public static void Postfix(ObjectiveBaseFetchContainer __instance) { RebirthFetchQuestIdentity.Bind(__instance); }
}

[HarmonyPatch(typeof(ObjectiveBaseFetchContainer), "GetItemCount")]
public static class RebirthFetchCountIdentityPatch
{
    public static bool Prefix(ObjectiveBaseFetchContainer __instance, ref int __result)
    {
        EntityPlayerLocal player = __instance.OwnerQuest != null && __instance.OwnerQuest.OwnerJournal != null
            ? __instance.OwnerQuest.OwnerJournal.OwnerPlayer : null;
        if (player == null) return true;
        RebirthFetchQuestIdentity.Bind(__instance);
        __result = RebirthFetchQuestIdentity.Count(player.bag != null ? player.bag.ItemGrid.items : null, __instance)
            + RebirthFetchQuestIdentity.Count(player.inventory != null ? player.inventory.ItemGrid.items : null, __instance);
        return false;
    }
}

[HarmonyPatch(typeof(ObjectiveFetchFromContainer), "Current_ContainerOpened")]
public static class RebirthFetchContainerItemIdentityPatch
{
    public static bool Prefix(ObjectiveFetchFromContainer __instance, Vector3i containerLocation, TEFeatureStorage lootTE)
    {
        if (!(containerLocation == __instance.lootContainerPos) || lootTE == null) return false;
        RebirthFetchQuestIdentity.Bind(__instance);
        if (__instance.GetItemCount() >= 1 || lootTE.HasItem(__instance.expectedItem)) return false;
        ItemValue bound = __instance.expectedItem.Clone();
        bound.Meta = __instance.OwnerQuest.QuestCode;
        bound.SetMetadata(RebirthFetchQuestIdentity.MetadataKey, __instance.OwnerQuest.QuestCode);
        lootTE.AddItem(new ItemStack(bound, 1));
        return false;
    }
}

