using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

#nullable disable

public sealed class RebirthNpcIdentityAuditEntry
{
    public long Sequence;
    public DateTime TimestampUtc;
    public string Actor;
    public string Operation;
    public int EntityId;
    public string StableId;
    public bool Succeeded;
    public string Message;
}

public static class RebirthNpcIdentityAuditLedger
{
    private const int FormatVersion = 1;
    private const string FileName = "RebirthNpcIdentityAudit.xml";
    private const int MaximumEntries = 256;
    private const int DefaultReportEntries = 20;
    private const int MaximumReportEntries = 100;

    private static readonly object Sync = new object();
    private static readonly List<RebirthNpcIdentityAuditEntry> Entries = new List<RebirthNpcIdentityAuditEntry>();
    private static bool loaded;
    private static long nextSequence = 1;
    private static long writes;

    public static void Append(string actor, string operation, int entityId, RebirthNpcStableId stableId,
        bool succeeded, string message)
    {
        if (!IsServer()) return;
        EnsureLoaded();
        lock (Sync)
        {
            Entries.Add(new RebirthNpcIdentityAuditEntry
            {
                Sequence = nextSequence++,
                TimestampUtc = DateTime.UtcNow,
                Actor = Normalize(actor, "unknown"),
                Operation = Normalize(operation, "unknown"),
                EntityId = entityId,
                StableId = stableId.ToString(),
                Succeeded = succeeded,
                Message = Normalize(message, string.Empty)
            });
            TrimLocked();
            SaveLocked();
        }
    }

    public static string GetReport(int requestedCount)
    {
        EnsureLoaded();
        int count = requestedCount <= 0 ? DefaultReportEntries : Math.Min(requestedCount, MaximumReportEntries);
        lock (Sync)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("[REBIRTH NPC] identity audit entries=").Append(Entries.Count)
                .Append(" showing=").Append(Math.Min(count, Entries.Count))
                .Append(" writes=").Append(writes)
                .Append(" file=").Append(GetPath());
            int start = Math.Max(0, Entries.Count - count);
            for (int i = Entries.Count - 1; i >= start; i--)
            {
                RebirthNpcIdentityAuditEntry entry = Entries[i];
                builder.Append("\n  #").Append(entry.Sequence)
                    .Append(" ").Append(entry.TimestampUtc.ToString("o", CultureInfo.InvariantCulture))
                    .Append(" actor=").Append(entry.Actor)
                    .Append(" op=").Append(entry.Operation)
                    .Append(" result=").Append(entry.Succeeded ? "accepted" : "rejected");
                if (entry.EntityId != 0) builder.Append(" entity=").Append(entry.EntityId);
                if (!string.IsNullOrEmpty(entry.StableId)) builder.Append(" stableId=").Append(entry.StableId);
                if (!string.IsNullOrEmpty(entry.Message)) builder.Append(" message=").Append(entry.Message);
            }
            if (Entries.Count == 0) builder.Append("\n  none");
            return builder.ToString();
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync)
        {
            Entries.Clear();
            loaded = false;
            nextSequence = 1;
        }
    }

    private static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        lock (Sync)
        {
            if (loaded) return;
            loaded = true;
            string path = GetPath();
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                XmlDocument document; string source, loadError;
                if (!RebirthNpcPersistenceFile.TryLoad(path, ValidateDocument, out document, out source, out loadError))
                {
                    if (File.Exists(path) || File.Exists(path + ".bak"))
                        Log.Warning("[REBIRTH NPC] Failed to load identity audit ledger: " + loadError);
                    return;
                }
                XmlElement root = document.DocumentElement;
                int format;
                if (root == null || root.Name != "rebirthNpcIdentityAudit"
                    || !int.TryParse(root.GetAttribute("format"), out format)
                    || format != FormatVersion)
                    throw new InvalidDataException("Unsupported identity audit format.");
                RebirthNpcPersistenceSchemaTelemetry.RecordLoad("identity-audit", path, format, FormatVersion, source);

                long maximumSequence = 0;
                foreach (XmlNode node in root.SelectNodes("entry"))
                {
                    XmlElement element = node as XmlElement;
                    long sequence;
                    DateTime timestamp;
                    bool succeeded;
                    int entityId;
                    if (element == null
                        || !long.TryParse(element.GetAttribute("sequence"), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out sequence)
                        || !DateTime.TryParse(element.GetAttribute("utc"), CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out timestamp)
                        || !bool.TryParse(element.GetAttribute("succeeded"), out succeeded)) continue;
                    int.TryParse(element.GetAttribute("entity"), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out entityId);
                    Entries.Add(new RebirthNpcIdentityAuditEntry
                    {
                        Sequence = sequence,
                        TimestampUtc = timestamp.ToUniversalTime(),
                        Actor = element.GetAttribute("actor"),
                        Operation = element.GetAttribute("operation"),
                        EntityId = entityId,
                        StableId = element.GetAttribute("stableId"),
                        Succeeded = succeeded,
                        Message = element.GetAttribute("message")
                    });
                    if (sequence > maximumSequence) maximumSequence = sequence;
                }
                Entries.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
                TrimLocked();
                nextSequence = Math.Max(1, maximumSequence + 1);
            }
            catch (Exception ex)
            {
                RebirthNpcPersistenceSchemaTelemetry.RecordRejected("identity-audit", path, FormatVersion, ex.GetType().Name + ": " + ex.Message);
                Entries.Clear();
                nextSequence = 1;
                Log.Warning("[REBIRTH NPC] Failed to load identity audit ledger: "
                    + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    private static bool ValidateDocument(XmlDocument document)
    {
        XmlElement root = document != null ? document.DocumentElement : null;
        int format;
        return root != null && root.Name == "rebirthNpcIdentityAudit"
            && int.TryParse(root.GetAttribute("format"), out format)
            && format == FormatVersion;
    }

    public static void SaveCheckpoint()
    {
        if (!IsServer()) return;
        EnsureLoaded();
        lock (Sync)
        {
            RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded, GetPath());
            SaveLocked();
        }
    }

    private static void SaveLocked()
    {
        string path = GetPath();
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            XmlDocument document = new XmlDocument();
            XmlElement root = document.CreateElement("rebirthNpcIdentityAudit");
            root.SetAttribute("format", FormatVersion.ToString(CultureInfo.InvariantCulture));
            document.AppendChild(root);
            for (int i = 0; i < Entries.Count; i++)
            {
                RebirthNpcIdentityAuditEntry entry = Entries[i];
                XmlElement element = document.CreateElement("entry");
                Set(element, "sequence", entry.Sequence);
                element.SetAttribute("utc", entry.TimestampUtc.ToString("o", CultureInfo.InvariantCulture));
                element.SetAttribute("actor", entry.Actor ?? string.Empty);
                element.SetAttribute("operation", entry.Operation ?? string.Empty);
                if (entry.EntityId != 0) Set(element, "entity", entry.EntityId);
                if (!string.IsNullOrEmpty(entry.StableId)) element.SetAttribute("stableId", entry.StableId);
                element.SetAttribute("succeeded", entry.Succeeded ? "true" : "false");
                if (!string.IsNullOrEmpty(entry.Message)) element.SetAttribute("message", entry.Message);
                root.AppendChild(element);
            }
            RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot("identity-audit", path, FormatVersion);
            RebirthNpcPersistenceFile.SaveAtomic(path, document);
            writes++;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH NPC] Failed to save identity audit ledger: "
                + ex.GetType().Name + ": " + ex.Message);
            if (RebirthNpcPersistenceCoordinator.IsCheckpointWrite) throw;
        }
    }

    private static void TrimLocked()
    {
        int remove = Entries.Count - MaximumEntries;
        if (remove > 0) Entries.RemoveRange(0, remove);
    }

    private static string Normalize(string value, string fallback)
    {
        string result = string.IsNullOrEmpty(value) ? fallback : value.Trim();
        return result.Length <= 512 ? result : result.Substring(0, 512);
    }

    private static bool IsServer()
    {
        return SingletonMonoBehaviour<ConnectionManager>.Instance != null
            && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;
    }

    private static string GetPath()
    {
        string directory = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(directory) ? string.Empty : Path.Combine(directory, FileName);
    }

    private static void Set(XmlElement element, string name, object value)
    {
        element.SetAttribute(name, Convert.ToString(value, CultureInfo.InvariantCulture));
    }
}
