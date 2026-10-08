using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine.Scripting;

#nullable disable

public sealed class RebirthTraderPoiHistoryRecord
{
    public long Sequence;
    public string PlayerId;
    public int QuestCode;
    public string QuestId;
    public int Tier;
    public string PrefabName;
    public string PhysicalPoiKey;
    public int TraderId;
    public ulong CompletionWorldTime;
}

public static class RebirthTraderPoiPersistentHistory
{
    private const int FormatVersion = 2;
    private const int MaximumRecordsPerPlayer = 64;
    private const string FileName = "RebirthTraderPoiHistory.xml";
    private static readonly object Sync = new object();
    private sealed class CompletionWatermark
    {
        public ulong WorldTime;
        public readonly HashSet<string> KeysAtWorldTime = new HashSet<string>(StringComparer.Ordinal);
    }

    private static readonly Dictionary<string, List<RebirthTraderPoiHistoryRecord>> Records =
        new Dictionary<string, List<RebirthTraderPoiHistoryRecord>>(StringComparer.Ordinal);
    private static readonly Dictionary<string, CompletionWatermark> Watermarks =
        new Dictionary<string, CompletionWatermark>(StringComparer.Ordinal);
    private static bool loaded;
    private static bool dirty;
    private static long nextSequence = 1;

    public static string GetStablePlayerId(QuestJournal journal)
    {
        EntityPlayer player = journal != null ? journal.OwnerPlayer : null;
        if (player == null || GameManager.Instance == null) return string.Empty;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        return persistent != null && persistent.PrimaryId != null ? persistent.PrimaryId.CombinedString : string.Empty;
    }

    public static void AddRecentHistory(QuestJournal journal, int tier, int maximum, Dictionary<string, int> prefabAge, Dictionary<string, int> physicalAge)
    {
        if (maximum <= 0 || prefabAge == null || physicalAge == null) return;
        string playerId = GetStablePlayerId(journal);
        if (string.IsNullOrEmpty(playerId)) return;
        EnsureLoaded();
        lock (Sync)
        {
            List<RebirthTraderPoiHistoryRecord> records;
            if (!Records.TryGetValue(playerId, out records)) return;
            int age = 0;
            for (int i = records.Count - 1; i >= 0 && age < maximum; i--)
            {
                RebirthTraderPoiHistoryRecord record = records[i];
                if (record.Tier != tier || string.IsNullOrEmpty(record.PrefabName)) continue;
                age++;
                if (!prefabAge.ContainsKey(record.PrefabName)) prefabAge.Add(record.PrefabName, age);
                if (!string.IsNullOrEmpty(record.PhysicalPoiKey) && !physicalAge.ContainsKey(record.PhysicalPoiKey)) physicalAge.Add(record.PhysicalPoiKey, age);
            }
        }
    }

    public static void RecordCompleted(QuestJournal journal, Quest quest)
    {
        if (!CanRecord(journal, quest)) return;
        string playerId = GetStablePlayerId(journal);
        if (string.IsNullOrEmpty(playerId)) return;
        EnsureLoaded();
        lock (Sync)
        {
            if (RecordCompletedLocked(playerId, quest)) SaveLocked();
        }
    }

    /// <summary>
    /// One-time/migration ingestion invoked from player-spawn lifecycle, never from history reads.
    /// Uses the quest's authoritative FinishTime ordering and publishes at most once for the batch.
    /// </summary>
    public static void IngestJournalCompletions(QuestJournal journal)
    {
        if (journal == null || journal.quests == null) return;
        EntityPlayer owner = journal.OwnerPlayer;
        if (owner == null || owner.world == null || owner.world.IsRemote()) return;
        string playerId = GetStablePlayerId(journal);
        if (string.IsNullOrEmpty(playerId)) return;
        List<Quest> completed = new List<Quest>();
        for (int i = 0; i < journal.quests.Count; i++)
        {
            Quest quest = journal.quests[i];
            if (quest != null && quest.CurrentState == Quest.QuestState.Completed && CanRecord(journal, quest))
                completed.Add(quest);
        }
        completed.Sort(delegate(Quest a, Quest b)
        {
            int time = a.FinishTime.CompareTo(b.FinishTime);
            if (time != 0) return time;
            int code = a.QuestCode.CompareTo(b.QuestCode);
            if (code != 0) return code;
            return a.QuestGiverID.CompareTo(b.QuestGiverID);
        });
        EnsureLoaded();
        lock (Sync)
        {
            bool changed = false;
            // v1 had only a 64-row recency window and could have assigned sequence in
            // read order rather than completion order. With no v2 watermark, rebuild
            // this player's recency projection from the authoritative journal once.
            if (!Watermarks.ContainsKey(playerId) && completed.Count > 0)
            {
                Records[playerId] = new List<RebirthTraderPoiHistoryRecord>();
                dirty = true;
                changed = true;
            }
            for (int i = 0; i < completed.Count; i++) changed |= RecordCompletedLocked(playerId, completed[i]);
            if (changed) SaveLocked();
        }
    }

    private static bool CanRecord(QuestJournal journal, Quest quest)
    {
        return journal != null && quest != null && RebirthTraderPoiHistory.QualifiesAsPersonalTraderPoi(quest) &&
               RebirthTraderPoiIdentity.FromQuest(quest).HasPoi;
    }

    private static bool RecordCompletedLocked(string playerId, Quest quest)
    {
        RebirthTraderPoiIdentity identity = RebirthTraderPoiIdentity.FromQuest(quest);
        string key = CompletionKey(quest, identity);
        CompletionWatermark watermark;
        if (!Watermarks.TryGetValue(playerId, out watermark))
        {
            watermark = new CompletionWatermark();
            Watermarks.Add(playerId, watermark);
        }

        ulong completionTime = quest.FinishTime;
        if (completionTime < watermark.WorldTime) return false;
        if (completionTime == watermark.WorldTime && watermark.KeysAtWorldTime.Contains(key)) return false;

        bool changed = false;
        if (completionTime > watermark.WorldTime)
        {
            watermark.WorldTime = completionTime;
            watermark.KeysAtWorldTime.Clear();
            changed = true;
        }
        if (watermark.KeysAtWorldTime.Add(key)) changed = true;

        List<RebirthTraderPoiHistoryRecord> records;
        if (!Records.TryGetValue(playerId, out records))
        {
            records = new List<RebirthTraderPoiHistoryRecord>();
            Records.Add(playerId, records);
        }
        bool alreadyRecent = false;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            RebirthTraderPoiHistoryRecord existing = records[i];
            if (existing.QuestCode == quest.QuestCode && existing.TraderId == quest.QuestGiverID &&
                existing.CompletionWorldTime == completionTime &&
                string.Equals(existing.QuestId ?? string.Empty, quest.ID ?? string.Empty, StringComparison.Ordinal))
            {
                alreadyRecent = true;
                break;
            }
        }
        if (!alreadyRecent)
        {
            records.Add(new RebirthTraderPoiHistoryRecord
            {
                Sequence = nextSequence++, PlayerId = playerId, QuestCode = quest.QuestCode, QuestId = quest.ID ?? string.Empty,
                Tier = RebirthTraderPoiHistory.GetTier(quest), PrefabName = identity.PrefabName, PhysicalPoiKey = identity.PhysicalPoiKey,
                TraderId = quest.QuestGiverID, CompletionWorldTime = completionTime
            });
            if (records.Count > MaximumRecordsPerPlayer) records.RemoveRange(0, records.Count - MaximumRecordsPerPlayer);
            changed = true;
        }
        if (changed) dirty = true;
        return changed;
    }

    private static string CompletionKey(Quest quest, RebirthTraderPoiIdentity identity)
    {
        return quest.QuestCode.ToString(CultureInfo.InvariantCulture) + "|" +
               quest.QuestGiverID.ToString(CultureInfo.InvariantCulture) + "|" +
               (quest.ID ?? string.Empty) + "|" + (identity.PhysicalPoiKey ?? string.Empty);
    }

    public static void SaveIfDirty()
    {
        EnsureLoaded();
        lock (Sync) SaveLocked();
    }

    public static void Reset(bool save)
    {
        lock (Sync)
        {
            if (save && loaded) SaveLocked();
            Records.Clear();
            Watermarks.Clear();
            loaded = false;
            dirty = false;
            nextSequence = 1;
        }
    }

    private static void EnsureLoaded()
    {
        lock(Sync)
        {
            if(loaded)return;
            string path=GetPath();
            Dictionary<string,List<RebirthTraderPoiHistoryRecord>> staged;Dictionary<string,CompletionWatermark> stagedWatermarks;long stagedNext;string error;
            if(!TryLoadFile(path,out staged,out stagedWatermarks,out stagedNext,out error))
            {
                string backup=string.IsNullOrEmpty(path)?string.Empty:path+".bak";string backupError;
                if(!TryLoadFile(backup,out staged,out stagedWatermarks,out stagedNext,out backupError))
                {
                    if(!string.IsNullOrEmpty(error))Log.Warning("[RebirthTraderPoiHistory] Failed to load history: "+error);
                    loaded=true;return;
                }
                Log.Warning("[RebirthTraderPoiHistory] Recovered history from backup after primary load failure: "+(error??"missing primary"));
            }
            Records.Clear();foreach(var pair in staged)Records[pair.Key]=pair.Value;
            Watermarks.Clear();foreach(var pair in stagedWatermarks)Watermarks[pair.Key]=pair.Value;
            nextSequence=stagedNext;loaded=true;
        }
    }
    private static bool TryLoadFile(string path,out Dictionary<string,List<RebirthTraderPoiHistoryRecord>> staged,out Dictionary<string,CompletionWatermark> stagedWatermarks,out long stagedNext,out string error)
    {
        staged=new Dictionary<string,List<RebirthTraderPoiHistoryRecord>>(StringComparer.Ordinal);
        stagedWatermarks=new Dictionary<string,CompletionWatermark>(StringComparer.Ordinal);stagedNext=1;error=null;
        try
        {
            if(string.IsNullOrEmpty(path)||!File.Exists(path))return false;
            XmlDocument document=new XmlDocument();document.Load(path);XmlElement root=document.DocumentElement;int format;
            if(root==null||root.Name!="rebirthTraderPoiHistory"||!int.TryParse(root.GetAttribute("format"),out format)||format<1||format>FormatVersion){error="invalid root/format";return false;}
            foreach(XmlNode node in root.SelectNodes("record"))
            {
                XmlElement element=node as XmlElement;if(element==null)continue;RebirthTraderPoiHistoryRecord record=ReadRecord(element);
                if(record==null){error="invalid record";return false;}
                List<RebirthTraderPoiHistoryRecord> list;if(!staged.TryGetValue(record.PlayerId,out list)){list=new List<RebirthTraderPoiHistoryRecord>();staged.Add(record.PlayerId,list);}
                list.Add(record);stagedNext=Math.Max(stagedNext,record.Sequence+1);
            }
            foreach(List<RebirthTraderPoiHistoryRecord> list in staged.Values){list.Sort((a,b)=>a.Sequence.CompareTo(b.Sequence));if(list.Count>MaximumRecordsPerPlayer)list.RemoveRange(0,list.Count-MaximumRecordsPerPlayer);}
            if(format>=2)
            {
                foreach(XmlNode node in root.SelectNodes("watermark"))
                {
                    XmlElement element=node as XmlElement;if(element==null)continue;
                    string player=element.GetAttribute("player");ulong worldTime;
                    if(string.IsNullOrEmpty(player)||!ulong.TryParse(element.GetAttribute("worldTime"),NumberStyles.Integer,CultureInfo.InvariantCulture,out worldTime)){error="invalid watermark";return false;}
                    CompletionWatermark watermark=new CompletionWatermark{WorldTime=worldTime};
                    foreach(XmlNode keyNode in element.SelectNodes("key"))
                    {
                        XmlElement keyElement=keyNode as XmlElement;string value=keyElement!=null?keyElement.GetAttribute("value"):string.Empty;
                        if(!string.IsNullOrEmpty(value))watermark.KeysAtWorldTime.Add(value);
                    }
                    stagedWatermarks[player]=watermark;
                }
            }
            return true;
        }
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;return false;}
    }

    private static RebirthTraderPoiHistoryRecord ReadRecord(XmlElement element)
    {
        long sequence; int questCode; int tier; int traderId; ulong worldTime;
        string playerId = element.GetAttribute("player");
        string prefab = RebirthTraderPoiIdentity.Normalize(element.GetAttribute("prefab"));
        if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(prefab) ||
            !long.TryParse(element.GetAttribute("sequence"), NumberStyles.Integer, CultureInfo.InvariantCulture, out sequence) ||
            !int.TryParse(element.GetAttribute("questCode"), out questCode) ||
            !int.TryParse(element.GetAttribute("tier"), out tier) ||
            !int.TryParse(element.GetAttribute("trader"), out traderId)) return null;
        ulong.TryParse(element.GetAttribute("worldTime"), NumberStyles.Integer, CultureInfo.InvariantCulture, out worldTime);
        return new RebirthTraderPoiHistoryRecord
        {
            Sequence = sequence,
            PlayerId = playerId,
            QuestCode = questCode,
            QuestId = element.GetAttribute("questId"),
            Tier = Math.Max(1, Math.Min(6, tier)),
            PrefabName = prefab,
            PhysicalPoiKey = element.GetAttribute("physical"),
            TraderId = traderId,
            CompletionWorldTime = worldTime
        };
    }

    private static void SaveLocked()
    {
        if (!dirty) return;
        string path = GetPath();
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            XmlDocument document = new XmlDocument();
            XmlElement root = document.CreateElement("rebirthTraderPoiHistory");
            root.SetAttribute("format", FormatVersion.ToString(CultureInfo.InvariantCulture));
            document.AppendChild(root);
            foreach (KeyValuePair<string, CompletionWatermark> pair in Watermarks)
            {
                XmlElement watermark = document.CreateElement("watermark");
                watermark.SetAttribute("player", pair.Key);
                watermark.SetAttribute("worldTime", pair.Value.WorldTime.ToString(CultureInfo.InvariantCulture));
                List<string> keys = new List<string>(pair.Value.KeysAtWorldTime); keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    XmlElement key = document.CreateElement("key"); key.SetAttribute("value", keys[i]); watermark.AppendChild(key);
                }
                root.AppendChild(watermark);
            }
            foreach (KeyValuePair<string, List<RebirthTraderPoiHistoryRecord>> pair in Records)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    RebirthTraderPoiHistoryRecord record = pair.Value[i];
                    XmlElement element = document.CreateElement("record");
                    element.SetAttribute("sequence", record.Sequence.ToString(CultureInfo.InvariantCulture));
                    element.SetAttribute("player", record.PlayerId);
                    element.SetAttribute("questCode", record.QuestCode.ToString(CultureInfo.InvariantCulture));
                    element.SetAttribute("questId", record.QuestId ?? string.Empty);
                    element.SetAttribute("tier", record.Tier.ToString(CultureInfo.InvariantCulture));
                    element.SetAttribute("prefab", record.PrefabName ?? string.Empty);
                    element.SetAttribute("physical", record.PhysicalPoiKey ?? string.Empty);
                    element.SetAttribute("trader", record.TraderId.ToString(CultureInfo.InvariantCulture));
                    element.SetAttribute("worldTime", record.CompletionWorldTime.ToString(CultureInfo.InvariantCulture));
                    root.AppendChild(element);
                }
            }
            string temporary=path+".tmp",backup=path+".bak";
            document.Save(temporary);
            Dictionary<string,List<RebirthTraderPoiHistoryRecord>> verified;Dictionary<string,CompletionWatermark> verifiedWatermarks;long verifiedNext;string verifyError;
            if(!TryLoadFile(temporary,out verified,out verifiedWatermarks,out verifiedNext,out verifyError))throw new InvalidDataException("Staged trader history failed validation: "+verifyError);
            if(File.Exists(path))File.Copy(path,backup,true);
            File.Copy(temporary,path,true);File.Delete(temporary);
            dirty=false;
        }
        catch (Exception ex)
        {
            Log.Warning("[RebirthTraderPoiHistory] Failed to save history: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static string GetPath()
    {
        string directory = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(directory) ? string.Empty : Path.Combine(directory, FileName);
    }
}

[HarmonyLib.HarmonyPatch(typeof(QuestJournal), "CompleteQuest")]
public static class RebirthTraderPoiCompleteQuestPersistencePatch
{
    public static void Postfix(QuestJournal __instance, Quest q)
    {
        EntityPlayer owner = __instance != null ? __instance.OwnerPlayer : null;
        if (owner != null && owner.world != null && !owner.world.IsRemote() && q != null && q.CurrentState == Quest.QuestState.Completed)
            RebirthTraderPoiPersistentHistory.RecordCompleted(__instance, q);
    }
}

[Preserve]
public sealed class RebirthTraderPoiHistoryModApi : IModApi
{
    private static bool initialized;
    public void InitMod(Mod modInstance)
    {
        if (initialized) return;
        initialized = true;
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawned));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
    }
    private static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.IsRemote() || data.ClientInfo == null) return;
        EntityPlayer player = world.GetEntity(data.ClientInfo.entityId) as EntityPlayer;
        if (player != null) RebirthTraderPoiPersistentHistory.IngestJournalCompletions(player.QuestJournal);
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        RebirthTraderPoiPersistentHistory.Reset(true);
        RebirthTraderQuestGraceManager.ResetOffline(true);
        RebirthTraderOfferSnapshotService.Reset();
        RebirthTraderJobListRefresh.ResetPendingListRequest();
        RebirthTraderListRequestCorrelation.Reset();
    }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        RebirthTraderPoiPersistentHistory.Reset(true);
        RebirthTraderQuestGraceManager.ResetOffline(true);
        RebirthTraderOfferSnapshotService.Reset();
        RebirthTraderJobListRefresh.ResetPendingListRequest();
        RebirthTraderListRequestCorrelation.Reset();
    }
}
