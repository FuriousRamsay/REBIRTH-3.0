using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

#nullable disable

public enum RebirthTraderQuestGraceReason
{
    None = 0,
    LeftArea = 1,
    Death = 2,
    Disconnect = 3
}

public sealed class RebirthTraderOfflineGraceRecord
{
    public string PlayerId;
    public int QuestCode;
    public string QuestId;
    public string PoiReservationKey;
    public long DeadlineUtcTicks;
    public bool PartySiteCompleted;
}

/// <summary>
/// Five-minute recovery window for activated normal trader jobs.
///
/// The client/local Quest DataVariables hold the live grace timer so it survives normal
/// player-data saves. Dedicated-server disconnect records are also persisted separately,
/// because the disconnected player entity/quest is no longer available while party
/// members can continue the shared job.
/// </summary>
public static class RebirthTraderQuestGraceManager
{
    public static bool Enabled
    {
        get
        {
            return RebirthSandboxOptionManager.Current != null &&
                   RebirthSandboxOptionManager.Current.TraderJobRecoveryGrace;
        }
    }

    public static int GraceMinutes
    {
        get
        {
            if (RebirthSandboxOptionManager.Current == null)
                return 5;

            switch (RebirthSandboxOptionManager.Current.TraderJobRecoveryGraceDuration)
            {
                case RebirthTraderJobRecoveryGraceDuration.TenMinutes:
                    return 10;
                case RebirthTraderJobRecoveryGraceDuration.FifteenMinutes:
                    return 15;
                default:
                    return 5;
            }
        }
    }

    public static int GraceSeconds
    {
        get { return GraceMinutes * 60; }
    }

    public const string DeadlineKey = "RebirthTraderQuestGraceDeadlineUtc";
    public const string ReasonKey = "RebirthTraderQuestGraceReason";

    private const int OfflineFormatVersion = 1;
    private const string OfflineFileName = "RebirthTraderQuestGrace.xml";

    private static readonly object Sync = new object();
    private static readonly List<RebirthTraderOfflineGraceRecord> Offline =
        new List<RebirthTraderOfflineGraceRecord>();

    private static bool offlineLoaded;
    private static bool offlineDirty;
    private static readonly HashSet<string> PartyCompletionSignals =
        new HashSet<string>(StringComparer.Ordinal);

    // Death is armed independently of per-quest objective state. 3.1 marks the player
    // alive again before its later FailAllActivatedQuests call, so relying only on
    // EntityPlayerLocal.IsDead() or a quest flag is not sufficient during respawn.
    private static readonly Dictionary<int, long> ArmedDeathDeadlineByPlayer =
        new Dictionary<int, long>();

    private sealed class GraceProjection
    {
        public object World, QuestList, Owner;
        public long Revision, ScannedRevision = -1L;
        public int Count = -1;
        public bool Enabled, DisabledCleaned;
        public readonly List<Quest> GraceQuests = new List<Quest>();
        public bool Maintaining;
        public Quest NearestQuest;
        public long NearestDeadline = long.MaxValue;
        public long TextDeadline, TextSecond = long.MinValue;
        public string Text = string.Empty, TextTemplate;
        public GraceProjection() { }
    }
    private sealed class QuestProjectionSignature
    {
        public QuestJournal Journal;
        public Quest.QuestState State;
        public int Phase;
        public bool Rally, Initialized;
        public string Deadline, Reason;
        public QuestProjectionSignature() { }
    }
    private static ConditionalWeakTable<QuestJournal, GraceProjection> GraceByJournal = new ConditionalWeakTable<QuestJournal, GraceProjection>();
    private static ConditionalWeakTable<Quest, QuestProjectionSignature> QuestSignatures = new ConditionalWeakTable<Quest, QuestProjectionSignature>();

    public static void InvalidateJournal(QuestJournal journal)
    {
        if (journal == null) return;
        GraceProjection projection = GraceByJournal.GetOrCreateValue(journal);
        unchecked { projection.Revision++; }
        projection.DisabledCleaned = false;
    }

    // RefreshQuestCompletion is also called without a phase change. Such calls must not
    // invalidate the journal every frame; compare only the state relevant to grace.
    public static void ObserveQuestMutation(Quest quest)
    {
        if (quest == null) return;
        QuestProjectionSignature signature = QuestSignatures.GetOrCreateValue(quest);
        string deadline = null, reason = null;
        if (quest.DataVariables != null)
        { quest.DataVariables.TryGetValue(DeadlineKey, out deadline); quest.DataVariables.TryGetValue(ReasonKey, out reason); }
        if (signature.Initialized && ReferenceEquals(signature.Journal, quest.OwnerJournal)
            && signature.State == quest.CurrentState && signature.Phase == (int)quest.CurrentPhase
            && signature.Rally == quest.RallyMarkerActivated && string.Equals(signature.Deadline, deadline, StringComparison.Ordinal)
            && string.Equals(signature.Reason, reason, StringComparison.Ordinal)) return;
        if (signature.Journal != null && !ReferenceEquals(signature.Journal, quest.OwnerJournal)) InvalidateJournal(signature.Journal);
        signature.Initialized = true; signature.Journal = quest.OwnerJournal; signature.State = quest.CurrentState;
        signature.Phase = (int)quest.CurrentPhase; signature.Rally = quest.RallyMarkerActivated;
        signature.Deadline = deadline; signature.Reason = reason;
        InvalidateJournal(quest.OwnerJournal);
    }

    private static GraceProjection GetProjection(QuestJournal journal)
    {
        if (journal == null || journal.quests == null) return null;
        GraceProjection projection = GraceByJournal.GetOrCreateValue(journal);
        object world = GameManager.Instance != null ? GameManager.Instance.World : null;
        bool enabled = Enabled;
        if (!ReferenceEquals(projection.World, world) || !ReferenceEquals(projection.QuestList, journal.quests)
            || !ReferenceEquals(projection.Owner, journal.OwnerPlayer) || projection.Count != journal.quests.Count
            || projection.Enabled != enabled)
        {
            projection.World = world; projection.QuestList = journal.quests; projection.Owner = journal.OwnerPlayer;
            projection.Count = journal.quests.Count; projection.Enabled = enabled;
            unchecked { projection.Revision++; }
            projection.DisabledCleaned = false;
        }
        if (!enabled)
        { projection.NearestQuest = null; projection.NearestDeadline = long.MaxValue; return projection; }
        if (!projection.Maintaining && projection.ScannedRevision != projection.Revision)
        {
            projection.GraceQuests.Clear();
            projection.NearestQuest = null; projection.NearestDeadline = long.MaxValue;
            for (int i = 0; i < journal.quests.Count; i++)
            {
                Quest quest = journal.quests[i]; long deadline;
                if (!TryGetDeadline(quest, out deadline)) continue;
                projection.GraceQuests.Add(quest);
                if (IsSiteActive(quest) && deadline < projection.NearestDeadline)
                { projection.NearestQuest = quest; projection.NearestDeadline = deadline; }
            }
            projection.ScannedRevision = projection.Revision;
        }
        return projection;
    }

    public static long ArmDeathForPlayer(EntityPlayerLocal player)
    {
        if (!Enabled || player == null)
            return 0L;

        long deadline =
            DateTime.UtcNow.AddSeconds(GraceSeconds).Ticks;

        lock (Sync)
        {
            ArmedDeathDeadlineByPlayer[player.entityId] = deadline;
        }

        { if (RebirthTraderDebug.Enabled) RebirthTraderDebug.Trace(
            "DEATH_GRACE_ARM player=" + player.entityId +
            " deadlineUtc=" + deadline); }

        return deadline;
    }

    public static bool TryGetArmedDeathDeadline(
        EntityPlayerLocal player,
        out long deadline)
    {
        deadline = 0L;
        if (!Enabled || player == null)
            return false;

        lock (Sync)
        {
            if (!ArmedDeathDeadlineByPlayer.TryGetValue(
                    player.entityId,
                    out deadline))
                return false;
        }

        return deadline > DateTime.UtcNow.Ticks;
    }

    public static void ClearArmedDeath(EntityPlayerLocal player)
    {
        if (player == null)
            return;

        lock (Sync)
        {
            ArmedDeathDeadlineByPlayer.Remove(player.entityId);
        }

        { if (RebirthTraderDebug.Enabled) RebirthTraderDebug.Trace(
            "DEATH_GRACE_DISARM player=" + player.entityId); }
    }

    public static bool IsEligibleQuest(Quest quest)
    {
        if (quest == null || quest.QuestClass == null)
            return false;
        if (!string.IsNullOrEmpty(quest.QuestClass.QuestType))
            return false;
        return RebirthTraderJobCompletionStats.IsRecognizedNormalTraderJob(quest);
    }

    public static bool IsSiteActive(Quest quest)
    {
        return IsEligibleQuest(quest) &&
               quest.RallyMarkerActivated &&
               quest.CurrentState == Quest.QuestState.InProgress &&
               (int)quest.CurrentPhase < (int)quest.QuestClass.HighestPhase;
    }

    public static bool BeginGrace(
        Quest quest,
        RebirthTraderQuestGraceReason reason,
        long authoritativeDeadlineUtcTicks = 0L,
        bool syncToServer = true)
    {
        if (!Enabled || !IsSiteActive(quest))
            return false;

        long existing;
        bool hadExisting = TryGetDeadline(quest, out existing);
        long now = DateTime.UtcNow.Ticks;
        long proposed = authoritativeDeadlineUtcTicks > 0L
            ? authoritativeDeadlineUtcTicks
            : now + TimeSpan.FromSeconds(GraceSeconds).Ticks;

        // Death while already outside, or disconnect while already on a timer, must not
        // reset the clock. An authoritative server reconnect deadline can only shorten it.
        long deadline;
        if (hadExisting)
        {
            deadline = authoritativeDeadlineUtcTicks > 0L
                ? Math.Min(existing, proposed)
                : existing;
        }
        else
        {
            deadline = proposed;
        }

        quest.DataVariables[DeadlineKey] =
            deadline.ToString(CultureInfo.InvariantCulture);
        quest.DataVariables[ReasonKey] =
            ((int)reason).ToString(CultureInfo.InvariantCulture);

        ObserveQuestMutation(quest);
        QuestJournal journal = quest.OwnerJournal;
        if (journal != null && journal.ActiveQuest == quest)
        {
            // A grace job is no longer occupying the single "active POI" slot. This is
            // what allows the player to deliberately start another job; the RallyPoint
            // patch below will fail this old grace job at that exact moment.
            journal.ActiveQuest = null;
            journal.RefreshRallyMarkerPositions();
        }

        if (!hadExisting)
        {
            EntityPlayerLocal owner = journal != null ? journal.OwnerPlayer : null;
            if (owner != null)
            {
                GameManager.ShowTooltip(
                    owner,
                    string.Format(
                        Localization.Get("xuiRebirthQuestGraceStarted"),
                        FormatRemaining(deadline)),
                    string.Empty,
                    "ui_denied");
            }
        }

        if (syncToServer && (!hadExisting || existing != deadline))
            SendGraceUpdateToServer(quest, deadline, reason, false);

        return true;
    }

    public static void ClearGrace(Quest quest, bool restoreActiveQuest = true, bool syncToServer = true)
    {
        if (quest == null)
            return;

        bool had = quest.DataVariables.ContainsKey(DeadlineKey);
        quest.DataVariables.Remove(DeadlineKey);
        quest.DataVariables.Remove(ReasonKey);
        ObserveQuestMutation(quest);

        QuestJournal journal = quest.OwnerJournal;
        if (restoreActiveQuest &&
            had &&
            journal != null &&
            journal.ActiveQuest == null &&
            IsSiteActive(quest))
        {
            journal.ActiveQuest = quest;
            journal.RefreshRallyMarkerPositions();
        }

        if (had && syncToServer)
            SendGraceUpdateToServer(
                quest,
                0L,
                RebirthTraderQuestGraceReason.None,
                true);
    }

    public static bool IsGraceActive(Quest quest)
    {
        if (!Enabled)
            return false;

        long deadline;
        return TryGetDeadline(quest, out deadline) &&
               deadline > DateTime.UtcNow.Ticks &&
               IsSiteActive(quest);
    }

    public static float GetRemainingSeconds(Quest quest)
    {
        long deadline;
        if (!TryGetDeadline(quest, out deadline))
            return 0f;

        long remaining = deadline - DateTime.UtcNow.Ticks;
        return remaining > 0L
            ? (float)TimeSpan.FromTicks(remaining).TotalSeconds
            : 0f;
    }

    public static Quest FindGraceQuest(QuestJournal journal)
    {
        GraceProjection projection = GetProjection(journal);
        return projection != null && projection.Enabled ? projection.NearestQuest : null;
    }

    public static string GetHudText(QuestJournal journal)
    {
        GraceProjection projection = GetProjection(journal);
        if (projection == null || !projection.Enabled || projection.NearestQuest == null) return string.Empty;
        long remaining = projection.NearestDeadline - DateTime.UtcNow.Ticks;
        long seconds = remaining > 0L ? (long)Math.Ceiling(remaining / (double)TimeSpan.TicksPerSecond) : 0L;
        string template = Localization.Get("xuiRebirthQuestGraceHud");
        if (projection.TextDeadline != projection.NearestDeadline || projection.TextSecond != seconds
            || !string.Equals(projection.TextTemplate, template, StringComparison.Ordinal))
        {
            projection.TextDeadline = projection.NearestDeadline; projection.TextSecond = seconds; projection.TextTemplate = template;
            projection.Text = string.Format(template, FormatRemaining(projection.NearestDeadline));
        }
        return projection.Text;
    }

    public static bool HasHud(QuestJournal journal) { return FindGraceQuest(journal) != null; }

    public static void TickLocal(EntityPlayerLocal player)
    {
        QuestJournal journal = player != null ? player.QuestJournal : null;
        GraceProjection projection = GetProjection(journal);
        if (projection == null) return;
        if (!projection.Enabled)
        {
            // First observation, on->off, journal mutations and load/rebind clean once.
            // Stable off does not scan, clear, send or prepare disarm diagnostics again.
            if (projection.DisabledCleaned) return;
            for (int i = journal.quests.Count - 1; i >= 0; i--)
            {
                Quest stale = journal.quests[i];
                if (stale != null && stale.DataVariables != null && stale.DataVariables.ContainsKey(DeadlineKey)) ClearGrace(stale, false);
            }
            ClearArmedDeath(player);
            projection.GraceQuests.Clear(); projection.NearestQuest = null; projection.NearestDeadline = long.MaxValue;
            projection.DisabledCleaned = true; projection.ScannedRevision = projection.Revision;
            return;
        }
        if (projection.Maintaining) return;
        projection.Maintaining = true;
        try
        {
        long now = DateTime.UtcNow.Ticks;
        projection.NearestQuest = null; projection.NearestDeadline = long.MaxValue;
        // Only quests that actually have grace markers are revisited. Expiry and current site
        // state remain live on every native compass update, not an arbitrary delayed poll.
        for (int i = projection.GraceQuests.Count - 1; i >= 0; i--)
        {
            Quest quest = projection.GraceQuests[i]; long deadline;
            if (!TryGetDeadline(quest, out deadline)) { projection.GraceQuests.RemoveAt(i); continue; }
            if (!IsSiteActive(quest))
            { ClearGrace(quest, false); projection.GraceQuests.RemoveAt(i); continue; }
            if (deadline <= now)
            { FailGraceQuest(quest, Localization.Get("xuiRebirthQuestGraceExpired")); projection.GraceQuests.RemoveAt(i); continue; }
            // Iterate reverse as maintenance did, but preserve the original forward journal
            // tie-breaking for identical deadlines by allowing the earlier entry to replace.
            if (deadline <= projection.NearestDeadline)
            { projection.NearestQuest = quest; projection.NearestDeadline = deadline; }
        }
        }
        finally { projection.Maintaining = false; }
    }

    public static void FailOtherGraceJobs(QuestJournal journal, Quest newlyStarting)
    {
        if (journal == null || journal.quests == null)
            return;

        for (int i = journal.quests.Count - 1; i >= 0; i--)
        {
            Quest q = journal.quests[i];
            if (q == null || q == newlyStarting || !IsGraceActive(q))
                continue;

            FailGraceQuest(
                q,
                Localization.Get("xuiRebirthQuestGraceNewJob"));
        }
    }

    public static void FailGraceQuest(Quest quest, string message)
    {
        if (quest == null ||
            quest.CurrentState != Quest.QuestState.InProgress)
            return;

        QuestJournal journal = quest.OwnerJournal;
        EntityPlayerLocal player =
            journal != null ? journal.OwnerPlayer : null;

        ClearGrace(quest, false);

        // A shared quest that this player does not own is always removed silently,
        // matching the 2.6 shared-quest notification suppressor.
        if (quest.SharedOwnerID != -1 && journal != null)
        {
            journal.ForceRemoveQuest(quest);
            return;
        }

        quest.CloseQuest(Quest.QuestState.Failed);

        if (player != null && !string.IsNullOrEmpty(message))
            GameManager.ShowTooltip(
                player,
                message,
                string.Empty,
                "quest_failed");
    }

    /// <summary>
    /// Server records the exact disconnect deadline before base PlayerDisconnected
    /// removes the persistent entity id.
    /// </summary>
    public static void CaptureDisconnect(ClientInfo cInfo)
    {
        if (!Enabled)
            return;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (cInfo == null || connection == null || !connection.IsServer ||
            cInfo.entityId == -1 || GameManager.Instance == null ||
            GameManager.Instance.World == null)
            return;

        EntityPlayer player =
            GameManager.Instance.World.GetEntity(cInfo.entityId) as EntityPlayer;
        if (player == null || player.QuestJournal == null)
            return;

        string playerId =
            cInfo.InternalId != null ? cInfo.InternalId.CombinedString : string.Empty;
        if (string.IsNullOrEmpty(playerId))
            return;

        EnsureOfflineLoaded();

        bool changed = false;
        lock (Sync)
        {
            PruneOfflineLocked();

            for (int i = 0; i < player.QuestJournal.quests.Count; i++)
            {
                Quest q = player.QuestJournal.quests[i];
                if (!IsSiteActive(q))
                    continue;

                long deadline;
                if (!TryGetDeadline(q, out deadline))
                    deadline = DateTime.UtcNow.AddSeconds(GraceSeconds).Ticks;

                string poiReservationKey =
                    GetPoiReservationKey(q);

                RebirthTraderOfflineGraceRecord record =
                    FindOfflineLocked(playerId, q.QuestCode, q.ID);

                if (record == null)
                {
                    record = new RebirthTraderOfflineGraceRecord
                    {
                        PlayerId = playerId,
                        QuestCode = q.QuestCode,
                        QuestId = q.ID ?? string.Empty,
                        PoiReservationKey = poiReservationKey,
                        DeadlineUtcTicks = deadline,
                        PartySiteCompleted = PartyCompletionSignals.Contains(q.QuestCode.ToString(CultureInfo.InvariantCulture) + "|" + (q.ID ?? string.Empty))
                    };
                    Offline.Add(record);
                    changed = true;
                }
                else
                {
                    long replacement = Math.Min(record.DeadlineUtcTicks, deadline);
                    if (record.DeadlineUtcTicks != replacement)
                    {
                        record.DeadlineUtcTicks = replacement;
                        changed = true;
                    }

                    if (!string.IsNullOrEmpty(poiReservationKey) &&
                        !string.Equals(
                            record.PoiReservationKey,
                            poiReservationKey,
                            StringComparison.Ordinal))
                    {
                        record.PoiReservationKey = poiReservationKey;
                        changed = true;
                    }
                }
                SetQuestGraceData(q, record.DeadlineUtcTicks, RebirthTraderQuestGraceReason.Disconnect);
            }

            if (changed)
            {
                offlineDirty = true;
                SaveOfflineLocked();
            }
        }
    }

    /// <summary>
    /// When any live copy of a shared job reaches its return-to-trader phase, remember
    /// that fact for disconnected holders of the same shared QuestCode/QuestId.
    /// </summary>
    public static void NotifyPartySiteCompleted(Quest quest)
    {
        if (!IsEligibleQuest(quest) ||
            quest.QuestClass == null ||
            (int)quest.CurrentPhase < (int)quest.QuestClass.HighestPhase)
            return;

        string signalKey =
            quest.QuestCode.ToString(CultureInfo.InvariantCulture) + "|" +
            (quest.ID ?? string.Empty);

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null) return;

        if (connection.IsServer)
        {
            lock (Sync)
            {
                if (!PartyCompletionSignals.Add(signalKey)) return;
            }
            MarkOfflinePartySiteCompleted(quest.QuestCode, quest.ID ?? string.Empty);
        }
        else
        {
            // Do not mark a client-side delivery as complete before the server handles it.
            // Repeated phase observations are safe because the server mutation is idempotent.
            connection.SendToServer(
                NetPackageManager.GetPackage<NetPackageRebirthQuestGracePartyComplete>()
                    .Setup(quest.QuestCode, quest.ID ?? string.Empty));
        }
    }

    public static bool IsAuthorizedPartyCompletion(EntityPlayer player, int questCode, string questId)
    {
        Quest quest = FindQuestByCode(player != null ? player.QuestJournal : null, questCode);
        return quest != null && IsEligibleQuest(quest) && quest.QuestClass != null &&
            string.Equals(quest.ID ?? string.Empty, questId ?? string.Empty, StringComparison.Ordinal) &&
            (int)quest.CurrentPhase >= (int)quest.QuestClass.HighestPhase;
    }

    public static void MarkOfflinePartySiteCompleted(
        int questCode,
        string questId)
    {
        ConnectionManager connection =
            SingletonMonoBehaviour<ConnectionManager>.Instance;

        if (connection == null || !connection.IsServer)
            return;

        EnsureOfflineLoaded();

        bool changed = false;
        lock (Sync)
        {
            // Remember validated completion even when no holder has disconnected yet.
            PartyCompletionSignals.Add(questCode.ToString(CultureInfo.InvariantCulture) + "|" + (questId ?? string.Empty));
            PruneOfflineLocked();

            for (int i = 0; i < Offline.Count; i++)
            {
                RebirthTraderOfflineGraceRecord record = Offline[i];
                if (record.QuestCode != questCode ||
                    !string.Equals(
                        record.QuestId,
                        questId ?? string.Empty,
                        StringComparison.Ordinal))
                    continue;

                if (!record.PartySiteCompleted)
                {
                    record.PartySiteCompleted = true;
                    changed = true;
                }
            }

            if (changed)
            {
                offlineDirty = true;
                SaveOfflineLocked();
            }
        }
    }

    public static void SyncReconnect(ClientInfo cInfo, int entityId)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || cInfo == null ||
            entityId == -1 || cInfo.InternalId == null)
            return;

        string playerId = cInfo.InternalId.CombinedString;
        if (string.IsNullOrEmpty(playerId))
            return;

        EnsureOfflineLoaded();

        if (!Enabled)
        {
            lock (Sync)
            {
                for (int i = Offline.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(
                            Offline[i].PlayerId,
                            playerId,
                            StringComparison.Ordinal))
                    {
                        Offline.RemoveAt(i);
                        offlineDirty = true;
                    }
                }

                SaveOfflineLocked();
            }

            return;
        }

        List<RebirthTraderOfflineGraceRecord> send =
            new List<RebirthTraderOfflineGraceRecord>();

        lock (Sync)
        {
            // Do not prune first: an expired record still has to reach the reconnecting
            // client once so the saved quest is failed rather than receiving a fresh timer.
            for (int i = 0; i < Offline.Count; i++)
            {
                RebirthTraderOfflineGraceRecord record = Offline[i];
                if (string.Equals(record.PlayerId, playerId, StringComparison.Ordinal))
                {
                    send.Add(new RebirthTraderOfflineGraceRecord
                    {
                        PlayerId = record.PlayerId,
                        QuestCode = record.QuestCode,
                        QuestId = record.QuestId,
                        PoiReservationKey = record.PoiReservationKey,
                        DeadlineUtcTicks = record.DeadlineUtcTicks,
                        PartySiteCompleted = record.PartySiteCompleted
                    });
                }
            }

            // Keep active records after reconnect. They also serve as the
            // server-authoritative POI reservation until the client returns to the
            // site, explicitly clears grace, or the deadline expires.
            PruneOfflineLocked();
            SaveOfflineLocked();
        }

        for (int i = 0; i < send.Count; i++)
        {
            RebirthTraderOfflineGraceRecord record = send[i];

            connection.SendPackage(
                NetPackageManager.GetPackage<NetPackageRebirthQuestGraceSync>()
                    .Setup(
                        record.QuestCode,
                        record.QuestId,
                        record.DeadlineUtcTicks,
                        record.PartySiteCompleted),
                _attachedToEntityId: entityId);
        }
    }

    public static void ApplyServerGraceUpdate(
        ClientInfo sender,
        EntityPlayer player,
        int questCode,
        string questId,
        string poiReservationKey,
        long deadlineUtcTicks,
        RebirthTraderQuestGraceReason reason,
        bool clear)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (connection == null || !connection.IsServer || world == null || world.IsRemote() ||
            sender == null || (player != null &&
            !ReferenceEquals(world.GetEntity(sender.entityId), player))) return;
        string playerId = sender.InternalId != null ? sender.InternalId.CombinedString : string.Empty;
        if (string.IsNullOrEmpty(playerId)) return;

        if (!Enabled) clear = true;
        if (!clear && reason != RebirthTraderQuestGraceReason.LeftArea &&
            reason != RebirthTraderQuestGraceReason.Death && reason != RebirthTraderQuestGraceReason.Disconnect) return;
        Quest quest = FindQuestByCode(player != null ? player.QuestJournal : null, questCode);
        bool liveAuthorized = quest != null && IsEligibleQuest(quest) &&
            string.Equals(quest.ID ?? string.Empty, questId ?? string.Empty, StringComparison.Ordinal);
        if (!liveAuthorized) quest = null;
        EnsureOfflineLoaded();

        lock (Sync)
        {
            var record = FindOfflineLocked(playerId, questCode, questId ?? string.Empty);
            if (!liveAuthorized && record == null) return;
            if (clear)
            {
                if (quest != null)
                {
                    quest.DataVariables.Remove(DeadlineKey);
                    quest.DataVariables.Remove(ReasonKey);
                    ObserveQuestMutation(quest);
                }
                if (record != null)
                {
                    Offline.Remove(record);
                    offlineDirty = true;
                    SaveOfflineLocked();
                }
                return;
            }

            // One authoritative deadline for both live and saved representations.
            // Client ticks and repeated notifications cannot extend an existing grace period.
            deadlineUtcTicks = DateTime.UtcNow.AddSeconds(GraceSeconds).Ticks;
            long liveDeadline;
            if (quest != null && TryGetDeadline(quest, out liveDeadline))
                deadlineUtcTicks = Math.Min(deadlineUtcTicks, liveDeadline);
            if (record != null)
                deadlineUtcTicks = Math.Min(deadlineUtcTicks, record.DeadlineUtcTicks);

            // Ignore the client reservation string. Missing server position data must
            // not replace a previously known reservation with an unverified location.
            poiReservationKey = quest != null ? GetPoiReservationKey(quest) : string.Empty;
            if (string.IsNullOrEmpty(poiReservationKey))
                poiReservationKey = record != null ? record.PoiReservationKey : string.Empty;
            if (quest != null) SetQuestGraceData(quest, deadlineUtcTicks, reason);

            bool changed = record == null;
            if (record == null)
            {
                record = new RebirthTraderOfflineGraceRecord
                {
                    PlayerId = playerId, QuestCode = questCode,
                    QuestId = questId ?? string.Empty,
                    PartySiteCompleted = PartyCompletionSignals.Contains(questCode.ToString(CultureInfo.InvariantCulture) + "|" + (questId ?? string.Empty))
                };
                Offline.Add(record);
            }
            if (record.DeadlineUtcTicks != deadlineUtcTicks ||
                !string.Equals(record.PoiReservationKey, poiReservationKey, StringComparison.Ordinal))
            {
                record.DeadlineUtcTicks = deadlineUtcTicks;
                record.PoiReservationKey = poiReservationKey;
                changed = true;
            }
            if (changed)
            {
                offlineDirty = true;
                SaveOfflineLocked();
            }
        }
    }

    public static void ApplyReconnectSync(
        int questCode,
        string questId,
        long deadlineUtcTicks,
        bool partySiteCompleted)
    {
        if (!Enabled)
            return;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        QuestJournal journal = player != null ? player.QuestJournal : null;
        Quest quest = FindQuestByCode(journal, questCode);

        if (quest == null ||
            (!string.IsNullOrEmpty(questId) &&
             !string.Equals(quest.ID, questId, StringComparison.Ordinal)))
            return;

        if (deadlineUtcTicks <= DateTime.UtcNow.Ticks)
        {
            FailGraceQuest(
                quest,
                Localization.Get("xuiRebirthQuestGraceExpired"));
            return;
        }

        SetQuestGraceData(
            quest,
            deadlineUtcTicks,
            RebirthTraderQuestGraceReason.Disconnect);

        if (partySiteCompleted)
        {
            CompleteSiteForOfflineParty(quest);
            ClearGrace(quest, false);
            GameManager.ShowTooltip(
                player,
                Localization.Get("xuiRebirthQuestGracePartyCompleted"),
                string.Empty,
                "quest_subtask_complete");
        }
    }

    public static void PreserveActivatedTraderQuestsOnLogin(QuestJournal journal)
    {
        if (journal == null || journal.quests == null)
            return;

        for (int i = 0; i < journal.quests.Count; i++)
        {
            Quest q = journal.quests[i];
            if (!IsSiteActive(q))
                continue;

            long deadline;
            if (!TryGetDeadline(q, out deadline))
            {
                BeginGrace(
                    q,
                    RebirthTraderQuestGraceReason.Disconnect,
                    0L,
                    false);
            }

            // Reattach objective hooks at the saved site phase. Do NOT call
            // ResetToRallyPointObjective and do NOT fail a shared quest.
            q.StartQuest(Quest.QuestSource.Load, false, false);
        }
    }

    public static void CompleteSiteForOfflineParty(Quest quest)
    {
        if (!IsSiteActive(quest) || quest.QuestClass == null)
            return;

        byte highest = quest.QuestClass.HighestPhase;

        // Activated normal trader jobs are at their site phase. Mark only objectives
        // before the final return-to-trader phase complete, then let Quest's own phase
        // transition execute its normal phase actions/hooks.
        for (int i = 0; i < quest.Objectives.Count; i++)
        {
            BaseObjective objective = quest.Objectives[i];
            if (objective == null)
                continue;

            if (objective.Phase == 0 || objective.Phase < highest)
                objective.Complete = true;
        }

        if ((int)quest.CurrentPhase < (int)highest)
            quest.RefreshQuestCompletion(playObjectiveComplete: false);
    }

    public static Quest FindQuestByCode(QuestJournal journal, int questCode)
    {
        if (journal == null || journal.quests == null)
            return null;

        for (int i = 0; i < journal.quests.Count; i++)
            if (journal.quests[i] != null && journal.quests[i].QuestCode == questCode)
                return journal.quests[i];

        return null;
    }

    public static void ResetOffline(bool save)
    {
        lock (Sync)
        {
            if (save && offlineLoaded)
                SaveOfflineLocked();

            Offline.Clear();
            PartyCompletionSignals.Clear();
            ArmedDeathDeadlineByPlayer.Clear();
            offlineLoaded = false;
            offlineDirty = false;
            GraceByJournal = new ConditionalWeakTable<QuestJournal, GraceProjection>();
            QuestSignatures = new ConditionalWeakTable<Quest, QuestProjectionSignature>();
        }
    }

    public static string GetPoiReservationKey(Quest quest)
    {
        if (quest == null)
            return string.Empty;

        Vector3 poi;
        if (!quest.GetPositionData(
                out poi,
                Quest.PositionDataTypes.POIPosition))
            return string.Empty;

        return BuildPoiReservationKey(poi);
    }

    public static string BuildPoiReservationKey(Vector3 poiPosition)
    {
        return Mathf.RoundToInt(poiPosition.x).ToString(CultureInfo.InvariantCulture) +
               "," +
               Mathf.RoundToInt(poiPosition.z).ToString(CultureInfo.InvariantCulture);
    }

    private static string GetStablePlayerIdForEntity(int entityId)
    {
        if (GameManager.Instance == null)
            return string.Empty;

        PersistentPlayerData persistent =
            GameManager.Instance.GetPersistentPlayerList() != null
                ? GameManager.Instance.GetPersistentPlayerList()
                    .GetPlayerDataFromEntityID(entityId)
                : null;

        return persistent != null && persistent.PrimaryId != null
            ? persistent.PrimaryId.CombinedString
            : string.Empty;
    }

    public static bool TryGetBlockingPoiReservation(
        int requesterEntityId,
        Vector3 prefabPosition,
        out int remainingSeconds)
    {
        remainingSeconds = 0;

        if (!Enabled ||
            GameManager.Instance == null ||
            GameManager.Instance.World == null)
            return false;

        string requestedKey =
            BuildPoiReservationKey(prefabPosition);

        if (string.IsNullOrEmpty(requestedKey))
            return false;

        long now = DateTime.UtcNow.Ticks;
        World world = GameManager.Instance.World;
        List<EntityPlayer> players = world.GetPlayers();

        if (players != null)
        {
            for (int playerIndex = 0;
                 playerIndex < players.Count;
                 playerIndex++)
            {
                EntityPlayer player = players[playerIndex];

                if (player == null ||
                    player.entityId == requesterEntityId ||
                    player.QuestJournal == null ||
                    player.QuestJournal.quests == null)
                    continue;

                for (int questIndex = 0;
                     questIndex < player.QuestJournal.quests.Count;
                     questIndex++)
                {
                    Quest q = player.QuestJournal.quests[questIndex];

                    long deadline;
                    if (!TryGetDeadline(q, out deadline) ||
                        deadline <= now ||
                        !IsSiteActive(q) ||
                        !string.Equals(
                            GetPoiReservationKey(q),
                            requestedKey,
                            StringComparison.Ordinal))
                        continue;

                    remainingSeconds =
                        Math.Max(
                            1,
                            (int)Math.Ceiling(
                                TimeSpan.FromTicks(deadline - now)
                                    .TotalSeconds));

                    return true;
                }
            }
        }

        string requesterPlayerId =
            GetStablePlayerIdForEntity(requesterEntityId);

        EnsureOfflineLoaded();

        lock (Sync)
        {
            for (int i = 0; i < Offline.Count; i++)
            {
                RebirthTraderOfflineGraceRecord record = Offline[i];

                if (record == null ||
                    record.DeadlineUtcTicks <= now ||
                    string.IsNullOrEmpty(record.PoiReservationKey) ||
                    !string.Equals(
                        record.PoiReservationKey,
                        requestedKey,
                        StringComparison.Ordinal))
                    continue;

                if (!string.IsNullOrEmpty(requesterPlayerId) &&
                    string.Equals(
                        record.PlayerId,
                        requesterPlayerId,
                        StringComparison.Ordinal))
                    continue;

                remainingSeconds =
                    Math.Max(
                        1,
                        (int)Math.Ceiling(
                            TimeSpan.FromTicks(
                                record.DeadlineUtcTicks - now)
                                .TotalSeconds));

                return true;
            }
        }

        return false;
    }

    public static string GetPoiReservationMessage(int totalSeconds)
    {
        string format =
            Localization.Get("xuiRebirthQuestGracePoiReserved");

        if (string.IsNullOrEmpty(format) ||
            format == "xuiRebirthQuestGracePoiReserved")
        {
            format =
                "This job site is reserved by another player's recovery timer for {0}.";
        }

        return string.Format(
            format,
            FormatSeconds(totalSeconds));
    }

    public static string FormatSeconds(int totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return minutes.ToString("0", CultureInfo.InvariantCulture) +
               ":" +
               seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    private static bool TryGetDeadline(Quest quest, out long deadline)
    {
        deadline = 0L;
        if (quest == null || quest.DataVariables == null)
            return false;

        string raw;
        return quest.DataVariables.TryGetValue(DeadlineKey, out raw) &&
               long.TryParse(
                   raw,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out deadline);
    }

    private static void SetQuestGraceData(
        Quest quest,
        long deadlineUtcTicks,
        RebirthTraderQuestGraceReason reason)
    {
        if (quest == null)
            return;

        quest.DataVariables[DeadlineKey] =
            deadlineUtcTicks.ToString(CultureInfo.InvariantCulture);
        quest.DataVariables[ReasonKey] =
            ((int)reason).ToString(CultureInfo.InvariantCulture);
        ObserveQuestMutation(quest);
    }

    private static string FormatRemaining(long deadline)
    {
        long ticks = deadline - DateTime.UtcNow.Ticks;
        int totalSeconds = ticks > 0L
            ? Math.Max(0, (int)Math.Ceiling(TimeSpan.FromTicks(ticks).TotalSeconds))
            : 0;

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return minutes.ToString("0", CultureInfo.InvariantCulture) +
               ":" +
               seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    private static void SendGraceUpdateToServer(
        Quest quest,
        long deadline,
        RebirthTraderQuestGraceReason reason,
        bool clear)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || connection.IsServer || quest == null)
            return;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal owner = quest.OwnerJournal != null ? quest.OwnerJournal.OwnerPlayer : null;
        if (world == null || !world.IsRemote() || owner == null ||
            !ReferenceEquals(owner.world, world) || !ReferenceEquals(world.GetPrimaryPlayer(), owner) ||
            !ReferenceEquals(owner.QuestJournal, quest.OwnerJournal)) return;

        var update = NetPackageManager.GetPackage<NetPackageRebirthQuestGraceUpdate>()
            .Setup(quest.QuestCode, quest.ID ?? string.Empty, GetPoiReservationKey(quest), deadline, reason, clear);
        if (!LogisticsTransferService.ClientChannelReady(connection, update)) return;

        // Both packets use native reliable channel 0. Install the current journal before
        // authorizing the transition; periodic player saves can be up to 30 seconds behind.
        // This occurs only on a grace transition, never on objective refresh or UI ticks.
        var snapshot = NetPackageManager.GetPackage<NetPackagePlayerData>();
        if (!LogisticsTransferService.ClientChannelReady(connection, snapshot)) return;
        connection.SendToServer(snapshot.Setup(owner));
        connection.SendToServer(update);
    }

    private static RebirthTraderOfflineGraceRecord FindOfflineLocked(
        string playerId,
        int questCode,
        string questId)
    {
        for (int i = 0; i < Offline.Count; i++)
        {
            RebirthTraderOfflineGraceRecord record = Offline[i];
            if (record.QuestCode == questCode &&
                string.Equals(record.PlayerId, playerId, StringComparison.Ordinal) &&
                string.Equals(record.QuestId, questId ?? string.Empty, StringComparison.Ordinal))
                return record;
        }

        return null;
    }

    private static void EnsureOfflineLoaded()
    {
        lock (Sync)
        {
            if (offlineLoaded)
                return;

            offlineLoaded = true;
            string path = GetOfflinePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlElement root = doc.DocumentElement;
                int format;

                if (root == null ||
                    root.Name != "rebirthTraderQuestGrace" ||
                    !int.TryParse(root.GetAttribute("format"), out format) ||
                    format != OfflineFormatVersion)
                    return;

                foreach (XmlNode node in root.SelectNodes("record"))
                {
                    XmlElement element = node as XmlElement;
                    if (element == null)
                        continue;

                    int questCode;
                    long deadline;
                    bool party;

                    if (!int.TryParse(
                            element.GetAttribute("questCode"),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out questCode) ||
                        !long.TryParse(
                            element.GetAttribute("deadline"),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out deadline) ||
                        !bool.TryParse(element.GetAttribute("partyCompleted"), out party))
                        continue;

                    Offline.Add(new RebirthTraderOfflineGraceRecord
                    {
                        PlayerId = element.GetAttribute("player"),
                        QuestCode = questCode,
                        QuestId = element.GetAttribute("questId"),
                        PoiReservationKey = element.GetAttribute("poiKey"),
                        DeadlineUtcTicks = deadline,
                        PartySiteCompleted = party
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning(
                    "[RebirthQuestGrace] Failed to load offline grace: " +
                    ex.GetType().Name + ": " + ex.Message);
                Offline.Clear();
            }
        }
    }

    private static void PruneOfflineLocked()
    {
        long cutoff = DateTime.UtcNow.AddDays(-30).Ticks;
        for (int i = Offline.Count - 1; i >= 0; i--)
        {
            // Keep expired records for a long time so reconnecting players are told
            // that the original grace expired instead of receiving a fresh timer.
            if (Offline[i].DeadlineUtcTicks < cutoff)
            {
                Offline.RemoveAt(i);
                offlineDirty = true;
            }
        }
    }

    private static void SaveOfflineLocked()
    {
        if (!offlineDirty)
            return;

        string path = GetOfflinePath();
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("rebirthTraderQuestGrace");
            root.SetAttribute(
                "format",
                OfflineFormatVersion.ToString(CultureInfo.InvariantCulture));
            doc.AppendChild(root);

            for (int i = 0; i < Offline.Count; i++)
            {
                RebirthTraderOfflineGraceRecord record = Offline[i];
                XmlElement element = doc.CreateElement("record");
                element.SetAttribute("player", record.PlayerId ?? string.Empty);
                element.SetAttribute(
                    "questCode",
                    record.QuestCode.ToString(CultureInfo.InvariantCulture));
                element.SetAttribute("questId", record.QuestId ?? string.Empty);
                element.SetAttribute("poiKey", record.PoiReservationKey ?? string.Empty);
                element.SetAttribute(
                    "deadline",
                    record.DeadlineUtcTicks.ToString(CultureInfo.InvariantCulture));
                element.SetAttribute(
                    "partyCompleted",
                    record.PartySiteCompleted.ToString());
                root.AppendChild(element);
            }

            string tmp = path + ".tmp";
            doc.Save(tmp);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
            offlineDirty = false;
        }
        catch (Exception ex)
        {
            Log.Warning(
                "[RebirthQuestGrace] Failed to save offline grace: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static string GetOfflinePath()
    {
        string directory = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(directory)
            ? string.Empty
            : Path.Combine(directory, OfflineFileName);
    }
}

/// <summary>
/// Start the five-minute recovery clock at the ACTUAL death event. Base 3.1 does not
/// call FailAllActivatedQuests until the respawn sequence, which was making v92/v93
/// incorrectly grant a fresh five minutes after the death screen.
/// </summary>
[HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.OnEntityDeath))]
public static class RebirthTraderDeathStartsGraceImmediatelyPatch
{
    [HarmonyPrefix]
    public static void Prefix(EntityPlayerLocal __instance)
    {
        if (!RebirthTraderQuestGraceManager.Enabled ||
            __instance == null ||
            __instance.QuestJournal == null ||
            __instance.QuestJournal.quests == null)
            return;

        // Arm death before ANY base death/respawn work. This timestamp is authoritative
        // even if an objective tries to CloseQuest(Failed) after SetAlive() but before
        // FailAllActivatedQuests() runs.
        long deadline =
            RebirthTraderQuestGraceManager.ArmDeathForPlayer(
                __instance);

        for (int i = 0;
             i < __instance.QuestJournal.quests.Count;
             i++)
        {
            Quest q = __instance.QuestJournal.quests[i];

            bool eligible =
                RebirthTraderQuestGraceManager.IsSiteActive(q);

            { if (RebirthTraderDebug.Enabled) RebirthTraderDebug.Trace(
                "DEATH_GRACE_SCAN quest='" +
                (q != null ? q.ID : "<null>") +
                "' code=" +
                (q != null ? q.QuestCode.ToString() : "-") +
                " eligibleSite=" + eligible +
                " rally=" +
                (q != null && q.RallyMarkerActivated) +
                " state=" +
                (q != null ? q.CurrentState.ToString() : "<null>") +
                " phase=" +
                (q != null ? q.CurrentPhase.ToString() : "-") +
                "/" +
                (q != null && q.QuestClass != null
                    ? q.QuestClass.HighestPhase.ToString()
                    : "-")); }

            if (!eligible)
                continue;

            RebirthTraderQuestGraceManager.BeginGrace(
                q,
                RebirthTraderQuestGraceReason.Death,
                deadline);
        }
    }
}

/// <summary>
/// Respawn-time FailAllActivatedQuests no longer STARTS the timer. It only preserves
/// eligible trader jobs whose death/disconnect timer was already created earlier.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.FailAllActivatedQuests))]
public static class RebirthTraderQuestDeathGracePatch
{
    [HarmonyPrefix]
    public static bool Prefix(QuestJournal __instance)
    {
        if (!RebirthTraderQuestGraceManager.Enabled ||
            __instance == null ||
            __instance.quests == null)
            return true;

        EntityPlayerLocal owner = __instance.OwnerPlayer;
        long armedDeadline;
        bool deathArmed =
            RebirthTraderQuestGraceManager.TryGetArmedDeathDeadline(
                owner,
                out armedDeadline);

        for (int i = 0; i < __instance.quests.Count; i++)
        {
            Quest q = __instance.quests[i];
            if (q == null ||
                !q.RallyMarkerActivated ||
                q.CurrentState != Quest.QuestState.InProgress ||
                q.QuestClass == null ||
                (int)q.CurrentPhase >=
                    (int)q.QuestClass.HighestPhase)
                continue;

            if (RebirthTraderQuestGraceManager.IsEligibleQuest(q))
            {
                if (!RebirthTraderQuestGraceManager.IsGraceActive(q) &&
                    deathArmed)
                {
                    { if (RebirthTraderDebug.Enabled) RebirthTraderDebug.Trace(
                        "RESPAWN_GRACE_RECOVER quest='" + q.ID +
                        "' code=" + q.QuestCode +
                        " deadlineUtc=" + armedDeadline); }

                    RebirthTraderQuestGraceManager.BeginGrace(
                        q,
                        RebirthTraderQuestGraceReason.Death,
                        armedDeadline);
                }

                if (RebirthTraderQuestGraceManager.IsGraceActive(q))
                {
                    { if (RebirthTraderDebug.Enabled) RebirthTraderDebug.Trace(
                        "RESPAWN_GRACE_PRESERVE quest='" + q.ID +
                        "' code=" + q.QuestCode); }
                    continue;
                }
            }

            { if (RebirthTraderDebug.Enabled) RebirthTraderDebug.Trace(
                "RESPAWN_GRACE_FAIL quest='" +
                (q.ID ?? "") +
                "' code=" + q.QuestCode +
                " eligible=" +
                RebirthTraderQuestGraceManager.IsEligibleQuest(q) +
                " deathArmed=" + deathArmed); }

            q.CloseQuest(Quest.QuestState.Failed);
        }

        RebirthTraderQuestGraceManager.ClearArmedDeath(owner);
        return false;
    }
}

/// <summary>
/// Final safety net: any 3.1 path that attempts CloseQuest(Failed) while a normal
/// activated trader job is protected by death/disconnect grace is suppressed.
///
/// This specifically covers the respawn ordering where 3.1 calls SetAlive() BEFORE
/// FailAllActivatedQuests(). An objective can update at the respawn location in that
/// window; the armed death timestamp still proves that this failure came from death,
/// not from the player simply walking out alive.
/// </summary>
[HarmonyPatch(typeof(Quest), nameof(Quest.CloseQuest))]
public static class RebirthTraderDeathDisconnectFailureGuardPatch
{
    [HarmonyPrefix]
    public static bool Prefix(
        Quest __instance,
        Quest.QuestState finalState)
    {
        if (!RebirthTraderQuestGraceManager.Enabled ||
            __instance == null ||
            finalState != Quest.QuestState.Failed ||
            !RebirthTraderQuestGraceManager.IsEligibleQuest(__instance) ||
            !__instance.RallyMarkerActivated ||
            __instance.CurrentState != Quest.QuestState.InProgress ||
            __instance.QuestClass == null ||
            (int)__instance.CurrentPhase >=
                (int)__instance.QuestClass.HighestPhase)
            return true;

        QuestJournal journal = __instance.OwnerJournal;
        EntityPlayerLocal owner =
            journal != null ? journal.OwnerPlayer : null;

        long armedDeadline;
        bool deathArmed =
            RebirthTraderQuestGraceManager.TryGetArmedDeathDeadline(
                owner,
                out armedDeadline);

        if (!RebirthTraderQuestGraceManager.IsGraceActive(__instance) &&
            deathArmed)
        {
            { if (RebirthTraderDebug.Enabled) RebirthTraderDebug.Trace(
                "QUEST_FAIL_GUARD recover-death quest='" +
                (__instance.ID ?? "") +
                "' code=" + __instance.QuestCode +
                " deadlineUtc=" + armedDeadline); }

            RebirthTraderQuestGraceManager.BeginGrace(
                __instance,
                RebirthTraderQuestGraceReason.Death,
                armedDeadline);
        }

        if (RebirthTraderQuestGraceManager.IsGraceActive(__instance))
        {
            { if (RebirthTraderDebug.Enabled) RebirthTraderDebug.Trace(
                "QUEST_FAIL_GUARD suppressed quest='" +
                (__instance.ID ?? "") +
                "' code=" + __instance.QuestCode +
                " playerDead=" +
                (owner != null && owner.IsDead()) +
                " deathArmed=" + deathArmed +
                " remaining=" +
                RebirthTraderQuestGraceManager
                    .GetRemainingSeconds(__instance)
                    .ToString("0.0")); }

            return false;
        }

        // No death/disconnect grace exists: ordinary alive boundary failure remains
        // untouched and proceeds through vanilla CloseQuest(Failed).
        return true;
    }
}

/// <summary>
/// Boundary code NEVER creates grace. It only preserves a timer that already exists
/// because of death/disconnect. Walking out alive with no active timer remains vanilla
/// immediate failure.
/// </summary>
[HarmonyPatch(typeof(ObjectivePOIStayWithin), nameof(ObjectivePOIStayWithin.UpdateState_Update))]
public static class RebirthTraderExistingGracePoiBoundaryPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ObjectivePOIStayWithin __instance)
    {
        if (__instance == null ||
            !RebirthTraderQuestGraceManager.IsGraceActive(
                __instance.OwnerQuest) ||
            !__instance.positionSet)
            return true;

        Quest quest = __instance.OwnerQuest;
        QuestJournal journal = quest.OwnerJournal;
        EntityPlayerLocal owner =
            journal != null ? journal.OwnerPlayer : null;

        if (owner == null)
            return true;

        Vector3 p = owner.position;
        p.y = p.z;

        if (__instance.outerRect.Contains(p))
        {
            RebirthTraderQuestGraceManager.ClearGrace(quest);
            return true;
        }

        __instance.ObjectiveState =
            BaseObjective.ObjectiveStates.InProgress;

        if (RebirthTraderQuestGraceManager
                .GetRemainingSeconds(quest) <= 0f)
        {
            RebirthTraderQuestGraceManager.FailGraceQuest(
                quest,
                Localization.Get(
                    "xuiRebirthQuestGraceExpired"));
        }

        return false;
    }
}

[HarmonyPatch(typeof(ObjectiveStayWithin), nameof(ObjectiveStayWithin.Update))]
public static class RebirthTraderExistingGraceStayWithinPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ObjectiveStayWithin __instance)
    {
        if (__instance == null ||
            !RebirthTraderQuestGraceManager.IsGraceActive(
                __instance.OwnerQuest))
            return true;

        Quest quest = __instance.OwnerQuest;
        QuestJournal journal = quest.OwnerJournal;
        EntityPlayerLocal owner =
            journal != null ? journal.OwnerPlayer : null;

        if (owner == null)
            return true;

        Vector3 center = quest.Position;

        if (!__instance.positionSetup)
        {
            if (quest.GetPositionData(
                    out center,
                    Quest.PositionDataTypes.Location) ||
                quest.GetPositionData(
                    out center,
                    Quest.PositionDataTypes.POIPosition))
            {
                quest.Position = center;
                __instance.positionSetup = true;
            }
            else
            {
                return true;
            }
        }

        Vector3 playerPos = owner.position;
        playerPos.y = 0f;
        center.y = 0f;

        __instance.currentDistance =
            (playerPos - center).magnitude;

        if (__instance.currentDistance <=
            __instance.maxDistance)
        {
            RebirthTraderQuestGraceManager.ClearGrace(quest);
            return true;
        }

        __instance.ObjectiveState =
            BaseObjective.ObjectiveStates.InProgress;

        if (RebirthTraderQuestGraceManager
                .GetRemainingSeconds(quest) <= 0f)
        {
            RebirthTraderQuestGraceManager.FailGraceQuest(
                quest,
                Localization.Get(
                    "xuiRebirthQuestGraceExpired"));
        }

        return false;
    }
}

[HarmonyPatch(typeof(ObjectiveStayWithin), "get_StatusText")]
public static class RebirthTraderExistingGraceStayWithinStatusPatch
{
    [HarmonyPrefix]
    public static bool Prefix(
        ObjectiveStayWithin __instance,
        ref string __result)
    {
        if (__instance == null ||
            !RebirthTraderQuestGraceManager.IsGraceActive(
                __instance.OwnerQuest))
            return true;

        __result =
            Localization.Get(
                "xuiRebirthQuestGraceObjective");

        return false;
    }
}

/// <summary>
/// Starting another activated POI job intentionally forfeits any previous grace job.
/// </summary>
[HarmonyPatch(typeof(ObjectiveRallyPoint), nameof(ObjectiveRallyPoint.RallyPointActivate))]
public static class RebirthTraderRallyStartsNewJobGracePatch
{
    [HarmonyPrefix]
    public static bool Prefix(
        ObjectiveRallyPoint __instance,
        Vector3 prefabPos,
        bool activate)
    {
        if (!activate ||
            __instance == null ||
            !RebirthTraderQuestGraceManager.IsEligibleQuest(
                __instance.OwnerQuest))
            return true;

        Quest quest = __instance.OwnerQuest;
        QuestJournal journal =
            quest != null ? quest.OwnerJournal : null;

        EntityPlayer owner =
            journal != null ? journal.OwnerPlayer : null;

        ConnectionManager connection =
            SingletonMonoBehaviour<ConnectionManager>.Instance;

        // Host/SP reaches RallyPointActivate directly. Remote clients are validated
        // earlier on the dedicated/host server in NetPackageQuestEvent.TryRallyMarker.
        if (RebirthTraderQuestGraceManager.Enabled &&
            connection != null &&
            connection.IsServer &&
            owner != null)
        {
            int remaining;
            if (RebirthTraderQuestGraceManager.TryGetBlockingPoiReservation(
                    owner.entityId,
                    prefabPos,
                    out remaining))
            {
                EntityPlayerLocal local =
                    owner as EntityPlayerLocal;

                if (local != null)
                {
                    GameManager.ShowTooltip(
                        local,
                        RebirthTraderQuestGraceManager
                            .GetPoiReservationMessage(remaining),
                        string.Empty);
                }

                { if (RebirthTraderDebug.Enabled) RebirthTraderDebug.Trace(
                    "GRACE_POI_LOCK deny-local player=" +
                    owner.entityId +
                    " quest='" + (quest.ID ?? "") +
                    "' poi=" +
                    RebirthTraderQuestGraceManager
                        .BuildPoiReservationKey(prefabPos) +
                    " remaining=" + remaining); }

                return false;
            }
        }

        return true;
    }

    [HarmonyPostfix]
    public static void Postfix(ObjectiveRallyPoint __instance, bool activate)
    {
        if (!activate || __instance == null) return;
        Quest quest = __instance.OwnerQuest;
        if (!RebirthTraderQuestGraceManager.IsEligibleQuest(quest) || !quest.RallyMarkerActivated) return;
        // Forfeit older grace jobs only after native rally activation actually succeeded.
        RebirthTraderQuestGraceManager.FailOtherGraceJobs(quest.OwnerJournal, quest);
    }
}

/// <summary>
/// Remote-client rally attempts are authoritative on the server. Block the TryRallyMarker
/// request before vanilla can lock/reset the POI when another player's death/disconnect
/// recovery reservation is active for the same physical POI.
/// </summary>
[HarmonyPatch(typeof(NetPackageQuestEvent), nameof(NetPackageQuestEvent.ProcessPackage))]
public static class RebirthTraderGracePoiReservationServerPatch
{
    [HarmonyPrefix]
    public static bool Prefix(NetPackageQuestEvent __instance)
    {
        ConnectionManager connection =
            SingletonMonoBehaviour<ConnectionManager>.Instance;

        if (!RebirthTraderQuestGraceManager.Enabled ||
            __instance == null ||
            connection == null ||
            !connection.IsServer ||
            __instance.eventType !=
                NetPackageQuestEvent.QuestEventTypes.TryRallyMarker)
            return true;

        int remaining;
        if (!RebirthTraderQuestGraceManager.TryGetBlockingPoiReservation(
                __instance.entityID,
                __instance.prefabPos,
                out remaining))
            return true;

        string message =
            RebirthTraderQuestGraceManager
                .GetPoiReservationMessage(remaining);

        ClientInfo client =
            connection.Clients.ForEntityId(
                __instance.entityID);

        if (client != null)
        {
            connection.SendPackage(
                NetPackageManager
                    .GetPackage<NetPackageShowToolbeltMessage>()
                    .Setup(message, string.Empty),
                _attachedToEntityId: __instance.entityID);
        }

        { if (RebirthTraderDebug.Enabled) RebirthTraderDebug.Trace(
            "GRACE_POI_LOCK deny-remote player=" +
            __instance.entityID +
            " questCode=" + __instance.questCode +
            " poi=" +
            RebirthTraderQuestGraceManager
                .BuildPoiReservationKey(__instance.prefabPos) +
            " remaining=" + remaining); }

        return false;
    }
}

/// <summary>
/// Login normally deletes active shared quests. Preserve activated normal trader jobs;
/// non-trader/shared behavior remains vanilla-equivalent.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.RemoveAllSharedQuests))]
public static class RebirthTraderReconnectPreserveSharedPatch
{
    [HarmonyPrefix]
    public static bool Prefix(QuestJournal __instance)
    {
        if (__instance == null || __instance.quests == null)
            return false;

        ConnectionManager connection =
            SingletonMonoBehaviour<ConnectionManager>.Instance;

        for (int i = __instance.quests.Count - 1;
             i >= 0;
             i--)
        {
            Quest q = __instance.quests[i];

            if (q == null ||
                q.SharedOwnerID == -1 ||
                q.CurrentState != Quest.QuestState.InProgress ||
                (int)q.CurrentPhase >=
                    (int)q.QuestClass.HighestPhase)
                continue;

            // Activated normal trader jobs that were interrupted by disconnect are
            // preserved for reconnect grace. Every other shared quest is silently
            // removed exactly like the 2.6 notification suppressor.
            if (RebirthTraderQuestGraceManager.Enabled &&
                RebirthTraderQuestGraceManager.IsSiteActive(q))
                continue;

            if (connection != null)
            {
                NetPackageSharedQuest package =
                    NetPackageManager.GetPackage<NetPackageSharedQuest>()
                        .Setup(
                            q.QuestUniqueId,
                            q.QuestCode,
                            q.SharedOwnerID,
                            __instance.OwnerPlayer.entityId,
                            false);

                if (connection.IsServer)
                    connection.SendPackage(
                        package,
                        _attachedToEntityId: q.SharedOwnerID);
                else
                    connection.SendToServer(package);
            }

            __instance.ForceRemoveQuest(q);
        }

        for (int i = 0;
             i < __instance.sharedQuestEntries.Count;
             i++)
        {
            __instance.sharedQuestEntries[i]
                .Quest.RemoveMapObject();
        }

        __instance.sharedQuestEntries.Clear();
        __instance.OwnerPlayer
            .TriggerSharedQuestRemovedEvent(null);

        return false;
    }
}

/// <summary>
/// Login normally resets activated personal quests to RallyPoint and fails activated
/// shared quests. REBIRTH keeps the saved site phase and provisionally starts grace;
/// the server reconnect sync then supplies the original disconnect deadline.
/// </summary>
[HarmonyPatch(typeof(QuestJournal), nameof(QuestJournal.StartQuests))]
public static class RebirthTraderReconnectStartQuestPatch
{
    [HarmonyPrefix]
    public static bool Prefix(QuestJournal __instance)
    {
        if (__instance == null || __instance.quests == null)
            return false;

        if (!GameManager.Instance.World.IsEditor() &&
            !GameUtils.IsWorldEditor() &&
            !GameUtils.IsPlaytesting())
        {
            __instance.OwnerPlayer.challengeJournal
                .StartChallenges(__instance.OwnerPlayer);
        }

        for (int i = 0;
             i < __instance.quests.Count;
             i++)
        {
            Quest q = __instance.quests[i];

            if (q == null)
                continue;

            if (RebirthTraderQuestGraceManager.Enabled &&
                RebirthTraderQuestGraceManager.IsSiteActive(q))
            {
                // Remote dedicated clients receive the authoritative disconnect
                // deadline moments later. Host/SP saves already contain it.
                if (!RebirthTraderQuestGraceManager.IsGraceActive(q))
                {
                    RebirthTraderQuestGraceManager.BeginGrace(
                        q,
                        RebirthTraderQuestGraceReason.Disconnect,
                        0L,
                        false);
                }

                q.StartQuest(Quest.QuestSource.Load, false, false);
                continue;
            }

            if (q.RallyMarkerActivated)
            {
                if (q.SharedOwnerID != -1 &&
                    (int)q.CurrentPhase <
                        (int)q.QuestClass.HighestPhase &&
                    q.CurrentState ==
                        Quest.QuestState.InProgress)
                {
                    // Exact 2.6 presentation behavior: remove silently instead of
                    // CloseQuest(Failed), which emits a failure toast and sound.
                    __instance.ForceRemoveQuest(q);
                    i--;
                    continue;
                }

                q.ResetToRallyPointObjective();
                q.StartQuest(Quest.QuestSource.Load, false);
            }
            else
            {
                q.StartQuest(Quest.QuestSource.Load, false);
            }
        }

        return false;
    }
}

/// <summary>
/// Hosted multiplayer and single-player do not necessarily run PlayerDisconnected for
/// the local host before their player data is saved. Start disconnect grace at world
/// cleanup so the UTC deadline is serialized into the local QuestJournal.
/// </summary>
[HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveAndCleanupWorld))]
public static class RebirthTraderLocalWorldExitGracePatch
{
    [HarmonyPrefix]
    public static void Prefix()
    {
        if (!RebirthTraderQuestGraceManager.Enabled)
            return;

        World world =
            GameManager.Instance != null
                ? GameManager.Instance.World
                : null;

        EntityPlayerLocal player =
            world != null
                ? world.GetPrimaryPlayer()
                : null;

        QuestJournal journal =
            player != null
                ? player.QuestJournal
                : null;

        if (journal == null || journal.quests == null)
            return;

        for (int i = 0;
             i < journal.quests.Count;
             i++)
        {
            Quest q = journal.quests[i];

            if (!RebirthTraderQuestGraceManager.IsSiteActive(q))
                continue;

            RebirthTraderQuestGraceManager.BeginGrace(
                q,
                RebirthTraderQuestGraceReason.Disconnect,
                0L,
                false);
        }
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerDisconnected))]
public static class RebirthTraderDisconnectCapturePatch
{
    [HarmonyPrefix]
    public static void Prefix(ClientInfo _cInfo)
    {
        RebirthTraderQuestGraceManager.CaptureDisconnect(_cInfo);
    }
}

/// <summary>
/// Completion packets can arrive before the native 30-second player journal snapshot.
/// Reconcile after the native save has installed the sender's updated journal.
/// </summary>
[HarmonyPatch(typeof(GameManager), nameof(GameManager.SavePlayerData))]
public static class RebirthTraderSavedJournalCompletionPatch
{
    [HarmonyPostfix]
    public static void Postfix(GameManager __instance, ClientInfo _cInfo, PlayerDataFile _playerDataFile)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = __instance != null ? __instance.World : null;
        if (connection == null || !connection.IsServer || world == null || world.IsRemote() ||
            !RebirthTraderQuestGraceManager.Enabled || _cInfo == null || _playerDataFile == null ||
            _cInfo.entityId != _playerDataFile.id)
            return;

        EntityPlayer player = world.GetEntity(_cInfo.entityId) as EntityPlayer;
        QuestJournal journal = player != null ? player.QuestJournal : null;
        // Only examine the journal that native SavePlayerData actually accepted.
        if (journal == null || !ReferenceEquals(journal, _playerDataFile.questJournal) || journal.quests == null)
            return;

        for (int i = 0; i < journal.quests.Count; i++)
            RebirthTraderQuestGraceManager.NotifyPartySiteCompleted(journal.quests[i]);
    }
}
[HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerSpawnedInWorld))]
public static class RebirthTraderReconnectSyncPatch
{
    [HarmonyPostfix]
    public static void Postfix(
        ClientInfo _cInfo,
        RespawnType _respawnReason,
        Vector3i _pos,
        int _entityId)
    {
        if (_respawnReason == RespawnType.EnterMultiplayer ||
            _respawnReason == RespawnType.JoinMultiplayer ||
            _respawnReason == RespawnType.LoadedGame)
        {
            RebirthTraderQuestGraceManager.SyncReconnect(_cInfo, _entityId);
        }
    }
}

/// <summary>
/// Any live shared copy reaching the final return phase marks disconnected copies of
/// that same shared QuestCode/QuestId as site-complete.
/// </summary>
[HarmonyPatch(typeof(Quest), nameof(Quest.RefreshQuestCompletion))]
public static class RebirthTraderOfflinePartyCompletionPatch
{
    [HarmonyPostfix]
    public static void Postfix(Quest __instance)
    {
        RebirthTraderQuestGraceManager.NotifyPartySiteCompleted(__instance);
    }
}

/// <summary>
/// Runs the timer and exposes bindings for the small warning directly under the compass.
/// </summary>
[HarmonyPatch(typeof(XUiC_CompassWindow), nameof(XUiC_CompassWindow.Update))]
public static class RebirthTraderQuestGraceCompassUpdatePatch
{
    [HarmonyPostfix]
    public static void Postfix(XUiC_CompassWindow __instance)
    {
        if (__instance == null ||
            __instance.xui == null ||
            __instance.xui.playerUI == null)
            return;

        RebirthTraderQuestGraceManager.TickLocal(
            __instance.xui.playerUI.entityPlayer);
    }
}

[HarmonyPatch(typeof(XUiC_CompassWindow), "GetBindingValueInternal")]
public static class RebirthTraderQuestGraceCompassBindingPatch
{
    [HarmonyPostfix]
    public static void Postfix(
        XUiC_CompassWindow __instance,
        ref bool __result,
        ref string value,
        string bindingName)
    {
        if (__result || __instance == null ||
            __instance.xui == null ||
            __instance.xui.playerUI == null)
            return;

        EntityPlayerLocal player = __instance.xui.playerUI.entityPlayer;
        QuestJournal journal = player != null ? player.QuestJournal : null;

        if (bindingName == "rebirthquestgracevisible")
        {
            value = RebirthTraderQuestGraceManager.HasHud(journal).ToString();
            __result = true;
        }
        else if (bindingName == "rebirthquestgracetext")
        {
            value = RebirthTraderQuestGraceManager.GetHudText(journal);
            __result = true;
        }
    }
}


// All names below already have native call/patch precedents in the supplied source. Reflection
// selects every matching overload without inventing a native parameter signature.
[HarmonyPatch]
public static class RebirthTraderGraceJournalRevisionPatch
{
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(QuestJournal)))
            if (method.Name == nameof(QuestJournal.AddQuest) || method.Name == nameof(QuestJournal.RemoveQuest)
                || method.Name == nameof(QuestJournal.ForceRemoveQuest) || method.Name == nameof(QuestJournal.StartQuests)
                || method.Name == nameof(QuestJournal.RemoveAllSharedQuests)) yield return method;
    }
    [HarmonyPostfix]
    public static void Postfix(QuestJournal __instance) { RebirthTraderQuestGraceManager.InvalidateJournal(__instance); }
}

[HarmonyPatch]
public static class RebirthTraderGraceQuestRevisionPatch
{
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(Quest)))
            if (method.Name == nameof(Quest.CloseQuest) || method.Name == nameof(Quest.StartQuest)
                || method.Name == nameof(Quest.RefreshQuestCompletion)) yield return method;
    }
    [HarmonyPostfix]
    public static void Postfix(Quest __instance) { RebirthTraderQuestGraceManager.ObserveQuestMutation(__instance); }
}
