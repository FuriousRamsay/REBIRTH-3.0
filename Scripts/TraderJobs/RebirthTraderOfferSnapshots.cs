using System;
using System.Collections.Generic;

#nullable disable

public enum RebirthTraderOfferValidation
{
    Allowed,
    RejectedStaleSnapshot,
    RejectedChangedPoi
}

public sealed class RebirthTraderOfferEntry
{
    public int QuestCode;
    public string QuestId;
    public string PrefabName;
    public string PhysicalPoiKey;
}

public sealed class RebirthTraderOfferSnapshot
{
    public long Revision;
    public int OptionRevision;
    public int TraderId;
    public string PlayerId;
    public readonly Dictionary<string, RebirthTraderOfferEntry> Entries = new Dictionary<string, RebirthTraderOfferEntry>(StringComparer.Ordinal);
}

public static class RebirthTraderOfferSnapshotService
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthTraderOfferSnapshot> Snapshots =
        new Dictionary<string, RebirthTraderOfferSnapshot>(StringComparer.Ordinal);
    private static long nextRevision = 1;

    public static void Issue(QuestJournal journal, int traderId, List<Quest> offers)
    {
        string playerId = GetSnapshotPlayerId(journal);
        if (string.IsNullOrEmpty(playerId)) return;
        RebirthTraderOfferSnapshot snapshot = new RebirthTraderOfferSnapshot
        {
            Revision = nextRevision++,
            OptionRevision = RebirthSandboxOptionManager.Current.Revision,
            TraderId = traderId,
            PlayerId = playerId
        };
        if (offers != null)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                Quest quest = offers[i];
                if (quest == null) continue;
                snapshot.Entries[GetOfferKey(quest)] = new RebirthTraderOfferEntry
                {
                    QuestCode = quest.QuestCode,
                    QuestId = quest.ID ?? string.Empty,
                    PrefabName = GetStablePoiName(quest),
                    PhysicalPoiKey = GetStablePositionKey(quest)
                };
            }
        }
        lock (Sync) Snapshots[Key(playerId, traderId)] = snapshot;
    }

    public static RebirthTraderOfferValidation Validate(QuestJournal journal, Quest candidate)
    {
        if (journal == null || candidate == null) return RebirthTraderOfferValidation.RejectedStaleSnapshot;
        string playerId = GetSnapshotPlayerId(journal);
        if (string.IsNullOrEmpty(playerId)) return RebirthTraderOfferValidation.RejectedStaleSnapshot;
        RebirthTraderOfferSnapshot snapshot;
        lock (Sync)
        {
            if (!Snapshots.TryGetValue(Key(playerId, candidate.QuestGiverID), out snapshot))
                return RebirthTraderOfferValidation.RejectedStaleSnapshot;
        }
        if (snapshot.OptionRevision != RebirthSandboxOptionManager.Current.Revision)
            return RebirthTraderOfferValidation.RejectedStaleSnapshot;
        RebirthTraderOfferEntry entry;
        if (!snapshot.Entries.TryGetValue(GetOfferKey(candidate), out entry))
            return RebirthTraderOfferValidation.RejectedStaleSnapshot;
        string currentPoiName = GetStablePoiName(candidate);
        string currentPositionKey = GetStablePositionKey(candidate);
        if (!string.Equals(entry.QuestId, candidate.ID ?? string.Empty, StringComparison.Ordinal) ||
            !string.Equals(entry.PhysicalPoiKey, currentPositionKey, StringComparison.Ordinal))
            return RebirthTraderOfferValidation.RejectedChangedPoi;

        // Quest.Clone() in base 3.1 intentionally does not copy QuestPrefab. The
        // localized POIName DataVariable is copied, however, so use it as the stable
        // name across offered, cloned and network-reconstructed quest instances.
        if (!string.IsNullOrEmpty(entry.PrefabName) && !string.IsNullOrEmpty(currentPoiName) &&
            !string.Equals(entry.PrefabName, currentPoiName, StringComparison.OrdinalIgnoreCase))
            return RebirthTraderOfferValidation.RejectedChangedPoi;

        return RebirthTraderOfferValidation.Allowed;
    }

    public static void Consume(QuestJournal journal, Quest candidate)
    {
        string playerId = GetSnapshotPlayerId(journal);
        if (string.IsNullOrEmpty(playerId) || candidate == null) return;
        lock (Sync)
        {
            RebirthTraderOfferSnapshot snapshot;
            if (Snapshots.TryGetValue(Key(playerId, candidate.QuestGiverID), out snapshot))
                snapshot.Entries.Remove(GetOfferKey(candidate));
        }
    }

    private static string GetSnapshotPlayerId(QuestJournal journal)
    {
        string stable = RebirthTraderPoiPersistentHistory.GetStablePlayerId(journal);
        if (!string.IsNullOrEmpty(stable)) return stable;
        EntityPlayer owner = journal != null ? journal.OwnerPlayer : null;
        return owner != null ? "entity:" + owner.entityId : string.Empty;
    }

    public static string GetOfferKey(Quest quest)
    {
        if (quest == null) return string.Empty;

        // Do not include QuestCode or QuestPrefab here. QuestCode is assigned when
        // the quest starts, and base 3.1 Quest.Clone() does not copy QuestPrefab.
        // Quest ID + assigned world position is stable through the complete trader
        // offer -> clone -> QuestJournal.AddQuest transaction.
        return (quest.ID ?? string.Empty) + "|" + GetStablePositionKey(quest);
    }

    private static string GetStablePoiName(Quest quest)
    {
        if (quest == null) return string.Empty;

        string poiName = string.Empty;
        try { poiName = quest.GetPOIName(); } catch { }
        if (!string.IsNullOrWhiteSpace(poiName))
            return RebirthTraderPoiIdentity.Normalize(poiName);

        try
        {
            if (quest.QuestPrefab != null)
                return RebirthTraderPoiIdentity.Normalize(quest.QuestPrefab.name);
        }
        catch { }

        return string.Empty;
    }

    private static string GetStablePositionKey(Quest quest)
    {
        if (quest == null) return string.Empty;

        // Base 3.1 Quest.Clone() copies PositionData but not Quest.position. GetLocation()
        // resolves POIPosition / TreasurePoint / Location from PositionData, so this key
        // survives the host clone performed by XUiC_QuestOfferWindow before AddQuest.
        UnityEngine.Vector3 location = quest.GetLocation();
        return location.x + "," + location.y + "," + location.z;
    }

    public static void Reset()
    {
        lock (Sync)
        {
            Snapshots.Clear();
            nextRevision = 1;
        }
    }

    private static string Key(string playerId, int traderId) { return playerId + "|" + traderId; }
}

[HarmonyLib.HarmonyPatch(typeof(EntityTrader), nameof(EntityTrader.SetActiveQuests))]
public static class RebirthTraderClientOfferSnapshotPatch
{
    public static void Postfix(EntityTrader __instance, EntityPlayer player)
    {
        if (__instance == null || player == null)
            return;

        RebirthTraderOfferSnapshotService.Issue(player.QuestJournal, __instance.entityId, __instance.activeQuests);
    }
}
