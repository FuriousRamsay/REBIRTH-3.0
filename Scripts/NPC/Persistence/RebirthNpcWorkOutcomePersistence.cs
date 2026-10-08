using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

#nullable disable

public static class RebirthNpcWorkOutcomePersistenceStore
{
    private const int FormatVersion = 1;
    private const string StoreId = "npc-work-outcomes";
    private const string FileName = "RebirthNpcWorkOutcomes.xml";
    private static readonly object Sync = new object();
    private static bool loaded;
    private static long nextSaveUtcTicks;
    private static int lastSavedVersion;
    private static long loads, saves, recovered, rejected;

    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        lock (Sync)
        {
            if (loaded) return;
            loaded = true;
        }
        string path = GetPath();
        if (string.IsNullOrEmpty(path)) return;
        XmlDocument document;
        string source, error;
        if (!File.Exists(path) && !File.Exists(path + ".bak"))
        {
            RebirthNpcPersistenceSchemaTelemetry.RecordLoad(StoreId, path, FormatVersion, FormatVersion, "new");
            return;
        }
        if (!RebirthNpcPersistenceFile.TryLoad(path, Validate, out document, out source, out error))
        {
            rejected++;
            RebirthNpcPersistenceSchemaTelemetry.RecordRejected(StoreId, path, FormatVersion, error);
            Log.Warning("[REBIRTH NPC Work] Outcome persistence load rejected: " + error);
            return;
        }
        int format = ParseInt(document.DocumentElement.GetAttribute("format"), 1);
        List<RebirthNpcWorkOutcomeRecord> records = ReadRecords(document);
        RebirthNpcWorkOutcomeService.ImportPersisted(records.ToArray());
        loads++;
        if (string.Equals(source, "backup", StringComparison.Ordinal)) recovered++;
        RebirthNpcPersistenceSchemaTelemetry.RecordLoad(StoreId, path, format, FormatVersion, source);
        lastSavedVersion = RebirthNpcWorkOutcomeService.PersistenceVersion;
    }

    public static void Tick()
    {
        EnsureLoaded();
        int version = RebirthNpcWorkOutcomeService.PersistenceVersion;
        if (version == lastSavedVersion) return;
        long now = DateTime.UtcNow.Ticks;
        if (now < nextSaveUtcTicks) return;
        nextSaveUtcTicks = now + TimeSpan.FromSeconds(15).Ticks;
        Save();
    }

    public static void Save()
    {
        EnsureLoaded();
        string path = GetPath(); RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded, path);
        if (string.IsNullOrEmpty(path)) return;
        RebirthNpcWorkOutcomeRecord[] records = RebirthNpcWorkOutcomeService.GetOutcomes();
        XmlDocument document = new XmlDocument();
        XmlElement root = document.CreateElement("rebirthNpcWorkOutcomes");
        root.SetAttribute("format", FormatVersion.ToString(CultureInfo.InvariantCulture));
        root.SetAttribute("savedUtcTicks", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        document.AppendChild(root);
        for (int i = 0; i < records.Length; i++) root.AppendChild(WriteRecord(document, records[i]));
        RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot(StoreId, path, FormatVersion);
        RebirthNpcPersistenceFile.SaveAtomic(path, document);
        lastSavedVersion = RebirthNpcWorkOutcomeService.PersistenceVersion;
        saves++;
    }

    public static void Reset(bool save)
    {
        if (save) Save();
        lock (Sync)
        {
            loaded = false;
            nextSaveUtcTicks = 0;
            lastSavedVersion = 0;
        }
    }

    public static string GetReport()
    {
        return "[REBIRTH NPC Work Outcome Persistence] loaded=" + loaded + " loads=" + loads +
            " saves=" + saves + " recovered=" + recovered + " rejected=" + rejected +
            " version=" + RebirthNpcWorkOutcomeService.PersistenceVersion +
            " savedVersion=" + lastSavedVersion;
    }

    private static XmlElement WriteRecord(XmlDocument document, RebirthNpcWorkOutcomeRecord r)
    {
        XmlElement e = document.CreateElement("outcome");
        e.SetAttribute("id", r.OutcomeId.ToString("N"));
        e.SetAttribute("assignment", r.AssignmentId.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("npc", r.NpcId.ToString());
        e.SetAttribute("definition", r.DefinitionId ?? string.Empty);
        e.SetAttribute("target", r.TargetId ?? string.Empty);
        e.SetAttribute("started", r.StartedUtcTicks.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("completed", r.CompletedUtcTicks.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("state", ((byte)r.State).ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("failure", ((byte)r.FailureCategory).ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("retries", r.RetryCount.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("actor", r.CancellationActor ?? string.Empty);
        e.SetAttribute("settlement", r.SettlementId ?? string.Empty);
        e.SetAttribute("detail", r.Detail ?? string.Empty);
        WriteResources(document, e, "produced", r.Produced);
        WriteResources(document, e, "consumed", r.Consumed);
        return e;
    }

    private static void WriteResources(XmlDocument d, XmlElement parent, string name, Dictionary<string, int> values)
    {
        if (values == null || values.Count == 0) return;
        XmlElement group = d.CreateElement(name);
        foreach (KeyValuePair<string, int> pair in values)
        {
            if (string.IsNullOrEmpty(pair.Key) || pair.Value == 0) continue;
            XmlElement item = d.CreateElement("item");
            item.SetAttribute("key", pair.Key);
            item.SetAttribute("count", pair.Value.ToString(CultureInfo.InvariantCulture));
            group.AppendChild(item);
        }
        if (group.HasChildNodes) parent.AppendChild(group);
    }

    private static List<RebirthNpcWorkOutcomeRecord> ReadRecords(XmlDocument document)
    {
        List<RebirthNpcWorkOutcomeRecord> result = new List<RebirthNpcWorkOutcomeRecord>();
        XmlNodeList nodes = document.DocumentElement.SelectNodes("outcome");
        int start = Math.Max(0, nodes.Count - 512);
        for (int i = start; i < nodes.Count; i++)
        {
            XmlElement e = nodes[i] as XmlElement;
            if (e == null) continue;
            RebirthNpcStableId npc;
            ulong assignment;
            Guid outcome;
            if (!RebirthNpcStableId.TryParse(e.GetAttribute("npc"), out npc) ||
                !ulong.TryParse(e.GetAttribute("assignment"), NumberStyles.Integer, CultureInfo.InvariantCulture, out assignment) ||
                !Guid.TryParse(e.GetAttribute("id"), out outcome)) continue;
            result.Add(new RebirthNpcWorkOutcomeRecord
            {
                OutcomeId = outcome,
                AssignmentId = assignment,
                NpcId = npc,
                DefinitionId = e.GetAttribute("definition"),
                TargetId = e.GetAttribute("target"),
                StartedUtcTicks = ParseLong(e.GetAttribute("started")),
                CompletedUtcTicks = ParseLong(e.GetAttribute("completed")),
                State = (RebirthNpcWorkOutcomeState)ParseInt(e.GetAttribute("state"), 1),
                FailureCategory = (RebirthNpcWorkFailureCategory)ParseInt(e.GetAttribute("failure"), 0),
                RetryCount = ParseInt(e.GetAttribute("retries"), 0),
                CancellationActor = e.GetAttribute("actor"),
                SettlementId = e.GetAttribute("settlement"),
                Detail = e.GetAttribute("detail"),
                Produced = ReadResources(e["produced"]),
                Consumed = ReadResources(e["consumed"])
            });
        }
        return result;
    }

    private static Dictionary<string, int> ReadResources(XmlElement group)
    {
        if (group == null) return null;
        Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        XmlNodeList nodes = group.SelectNodes("item");
        for (int i = 0; i < nodes.Count; i++)
        {
            XmlElement e = nodes[i] as XmlElement;
            if (e == null || string.IsNullOrEmpty(e.GetAttribute("key"))) continue;
            int count = ParseInt(e.GetAttribute("count"), 0);
            if (count != 0) result[e.GetAttribute("key")] = count;
        }
        return result.Count == 0 ? null : result;
    }

    private static bool Validate(XmlDocument document)
    {
        if (document == null || document.DocumentElement == null ||
            document.DocumentElement.Name != "rebirthNpcWorkOutcomes") return false;
        int format = ParseInt(document.DocumentElement.GetAttribute("format"), 0);
        return format >= 1 && format <= FormatVersion;
    }

    private static string GetPath()
    {
        string directory = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(directory) ? string.Empty : Path.Combine(directory, FileName);
    }

    private static int ParseInt(string value, int fallback)
    {
        int result;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : fallback;
    }

    private static long ParseLong(string value)
    {
        long result;
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : 0L;
    }
}
