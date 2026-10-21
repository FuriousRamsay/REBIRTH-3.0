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

public static partial class RebirthNpcIdentityAuditLedger
{
    private const int FormatVersion = 1;
    private const string FileName = "RebirthNpcIdentityAudit.xml";
    private const int MaximumEntries = 256;
    private const int DefaultReportEntries = 20;
    private const int MaximumReportEntries = 100;

    private static readonly object Sync = new object();
    private static List<RebirthNpcIdentityAuditEntry> Entries = new List<RebirthNpcIdentityAuditEntry>();
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
            if(nextSequence>=long.MaxValue)throw new InvalidOperationException("NPC audit sequence exhausted; original history retained");
            auditDirty=true;auditEpoch=new object();auditBinding?.ObserveMutation(auditEpoch);
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

    public static void ResetForWorldChange(){if(!TryResetAuditBacking(true))throw new InvalidOperationException("Original NPC audit records retained" );}
    private static void EnsureLoaded()=>LoadAuditBound();
    private static bool ValidateDocument(XmlDocument document){ReadAuditDocument(document);return true;}
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

    private static void SaveLocked()=>SaveAuditBound();
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
