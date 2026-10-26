using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

#nullable disable

public static partial class RebirthNpcWorkOutcomePersistenceStore
{
    private const int FormatVersion = 2;
    private const string StoreId = "npc-work-outcomes";
    private const string FileName = "RebirthNpcWorkOutcomes.xml";
    private static readonly object Sync = new object();
    private static World boundWorld;
    private static string boundPath;
    private static bool BoundCompatible=>boundWorld==null||(ReferenceEquals(boundWorld,GameManager.Instance?.World)&&boundPath==GetPath());
    private static void BindCurrent()
    {
        if(!BoundCompatible||GameManager.Instance?.World==null)throw new InvalidOperationException("NPC cache belongs to another retained original world/save");
        boundWorld=GameManager.Instance.World;boundPath=GetPath();
    }
    private static bool loaded;
    private static long nextSaveUtcTicks;
    private static int lastSavedVersion;
    private static long loads, saves, recovered, rejected;
    internal static bool IsReadyForNativeAdmission
    {
        get
        {
            lock(Sync)return loaded&&ThreadManager.IsMainThread()&&GameManager.Instance?.World?.IsRemote()==false&&
                ReferenceEquals(boundWorld,GameManager.Instance.World)&&BoundCompatible&&!string.IsNullOrEmpty(boundPath);
        }
    }

    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        lock (Sync)
        {
            if(!BoundCompatible)throw new InvalidOperationException("Retained cache scope differs; original retirement required");
            if (loaded) return;
            BindCurrent();
        }
        string path = GetPath();
        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Outcome save path is unavailable");
        XmlDocument document;
        string source, error;
        if (!File.Exists(path) && !File.Exists(path + ".bak"))
        {
            if(!RebirthNpcPersistenceFile.CanInitializeEmpty(path)||!RebirthNpcWorkOutcomeService.CanRetireEmptyBacking)return;
            RebirthNpcPersistenceSchemaTelemetry.RecordLoad(StoreId, path, FormatVersion, FormatVersion, "new");
            lock(Sync){loaded=true;lastSavedVersion=RebirthNpcWorkOutcomeService.PersistenceVersion;}
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
        var current=RebirthNpcWorkOutcomeService.GetOutcomes();
        if(current.Length!=0&&RecoveryRecordsImage(current)!=RecoveryRecordsImage(records.ToArray()))
            throw new InvalidDataException("Outcome load would replace retained original runtime records");
        RebirthNpcWorkOutcomeService.ImportPersisted(records.ToArray());
        loads++;
        if (string.Equals(source, "backup", StringComparison.Ordinal)) recovered++;
        RebirthNpcPersistenceSchemaTelemetry.RecordLoad(StoreId, path, format, FormatVersion, source);
        lastSavedVersion = RebirthNpcWorkOutcomeService.PersistenceVersion;
        lock(Sync)loaded=true;
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
        lock(Sync)if(!loaded)throw new InvalidOperationException("Outcome persistence load remains unresolved");
        string path = GetPath(); RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded, path);
        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Outcome save path is unavailable");
        using var recoveryWriter = RebirthNpcPersistenceFile.BeginWrite(path);
        int capturedVersion=RebirthNpcWorkOutcomeService.PersistenceVersion;
        RebirthNpcWorkOutcomeRecord[] records = RebirthNpcWorkOutcomeService.GetOutcomes();
        XmlDocument document = new XmlDocument();
        XmlElement root = document.CreateElement("rebirthNpcWorkOutcomes");
        root.SetAttribute("format", FormatVersion.ToString(CultureInfo.InvariantCulture));
        root.SetAttribute("savedUtcTicks", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        document.AppendChild(root);
        for (int i = 0; i < records.Length; i++) root.AppendChild(WriteRecord(document, records[i]));
        if(capturedVersion!=RebirthNpcWorkOutcomeService.PersistenceVersion)throw new InvalidOperationException("Outcome snapshot changed before publication");
        RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot(StoreId, path, FormatVersion);
        RebirthNpcPersistenceFile.SaveAtomic(path, document);
        if(capturedVersion!=RebirthNpcWorkOutcomeService.PersistenceVersion)throw new InvalidOperationException("Outcome runtime changed during publication; latest version remains unsaved");
        lastSavedVersion = capturedVersion;
        saves++;
    }

    internal static void SaveAndVerifyCommitted(RebirthNpcWorkOutcomeRecord expected)
    {
        if(expected==null||expected.OutcomeId==Guid.Empty||!expected.DurableCompletion)
            throw new InvalidOperationException("Committed outcome witness is unavailable");
        Save();
        if(!IsCurrentPrimaryCommitted(expected))
            throw new InvalidDataException("Committed outcome is absent or mismatched in current primary");
    }

    internal static bool IsCurrentPrimaryCommitted(RebirthNpcWorkOutcomeRecord expected)
    {
        if(expected==null||expected.OutcomeId==Guid.Empty||!expected.DurableCompletion)return false;
        try
        {
        string path=GetPath();
        if(string.IsNullOrEmpty(path)||!BoundCompatible||!File.Exists(path))return false;
        // Read the exact current primary, never a backup or the in-memory row.
        XmlDocument document=RebirthNpcRecoveryCurrentBacking.Read(path);
        if(!Validate(document))return false;
        foreach(var saved in ReadRecords(document))
        {
            if(saved.OutcomeId!=expected.OutcomeId)continue;
            return RebirthNpcWorkOutcomeService.SameCompletionWitness(saved,expected);
        }
        return false;
        }
        catch{return false;}
    }

    public static void Reset(bool save)
    {
        if(!TryReset(save))throw new InvalidOperationException("Outcome original state retained; typed per-save custody retirement required");
    }
    internal static bool CanRetireBacking
    {
        get{lock(Sync)try
        {
            if(!RebirthNpcWorkOutcomeService.CanRetireEmptyBacking)return false;
            if(boundWorld==null)return !loaded;
            if(!loaded||string.IsNullOrEmpty(boundPath)||RebirthNpcPersistenceFile.IsRecoveryFenced(boundPath))return false;
            if(!File.Exists(boundPath))return RebirthNpcPersistenceFile.CanInitializeEmpty(boundPath);
            var primary=RebirthNpcRecoveryCurrentBacking.Read(boundPath);
            return Validate(primary)&&primary.DocumentElement.SelectNodes("outcome").Count==0&&RebirthNpcWorkOutcomeService.CanRetireEmptyBacking;
        }catch{return false;}}
    }
    public static bool TryReset(bool save)
    {
        if(save){if(!BoundCompatible)return false;try{Save();}catch{return false;}}
        lock (Sync)
        {
            if(!CanRetireBacking)return false;
            loaded = false;boundWorld=null;boundPath=null;
            nextSaveUtcTicks = 0;
            lastSavedVersion = 0;
            return true;
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
        if(r.DurableCompletion)e.SetAttribute("durable", "1");
        if(r.DurableCompletion)e.SetAttribute("witness", r.DurableWitness ?? string.Empty);
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
                DurableCompletion = e.GetAttribute("durable") == "1",
                DurableWitness = e.GetAttribute("witness"),
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

    internal static string RecoveryRecordsImage(RebirthNpcWorkOutcomeRecord[] records)
    {var document=new XmlDocument();var root=document.CreateElement("rebirthNpcWorkOutcomes");root.SetAttribute("format",FormatVersion.ToString(CultureInfo.InvariantCulture));document.AppendChild(root);foreach(var row in records)root.AppendChild(WriteRecord(document,row));return document.OuterXml;}
    internal static bool TryPrepareRecovery(XmlDocument document,out RebirthNpcPreparedRecovery candidate,out RebirthNpcWorkOutcomeRecord[] records,out string error)
    {
        candidate=null;records=null;error=string.Empty;if(!BoundCompatible){error="recovery cannot replace another original world cache";return false;}if(!Validate(document)||document.DocumentElement.SelectNodes("outcome").Count>512){error="outcome semantic/lossless-bound validation failed";return false;}
        records=ReadRecords(document).ToArray();if(!RebirthNpcWorkOutcomeService.TryPrepareRecovery(records,out var prepared,out error))return false;
        candidate=new RebirthNpcPreparedRecovery(FileName,()=>BoundCompatible&&prepared.IsCurrent,()=>{lock(Sync){if(!BoundCompatible||!prepared.Commit())return false;BindCurrent();loaded=true;lastSavedVersion=RebirthNpcWorkOutcomeService.PersistenceVersion;nextSaveUtcTicks=0;return true;}});return true;
    }
    internal static bool ValidateRecoveryDocument(XmlDocument document)=>Validate(document);
    private static bool Validate(XmlDocument document)
    {
        if (document == null || document.DocumentElement == null ||
            document.DocumentElement.Name != "rebirthNpcWorkOutcomes") return false;
        int format = ParseInt(document.DocumentElement.GetAttribute("format"), 0);
        if(format<1||format>FormatVersion)return false;
        var outcomes=new HashSet<Guid>();
        foreach(XmlNode node in document.DocumentElement.ChildNodes)
        {
            var row=node as XmlElement;if(row==null)continue;
            Guid id;ulong assignment;RebirthNpcStableId npc;int state,failure,retries;long started,completed;
            if(row.Name!="outcome"||!Guid.TryParse(row.GetAttribute("id"),out id)||id==Guid.Empty||!outcomes.Add(id)||
                (row.HasAttribute("durable")&&(row.GetAttribute("durable")!="1"||
                    string.IsNullOrWhiteSpace(row.GetAttribute("witness"))))||
                !ulong.TryParse(row.GetAttribute("assignment"),NumberStyles.Integer,CultureInfo.InvariantCulture,out assignment)||assignment==0||
                !RebirthNpcStableId.TryParse(row.GetAttribute("npc"),out npc)||npc.IsEmpty||
                !int.TryParse(row.GetAttribute("state"),NumberStyles.Integer,CultureInfo.InvariantCulture,out state)||state<0||state>255||!Enum.IsDefined(typeof(RebirthNpcWorkOutcomeState),(byte)state)||
                !int.TryParse(row.GetAttribute("failure"),NumberStyles.Integer,CultureInfo.InvariantCulture,out failure)||failure<0||failure>255||!Enum.IsDefined(typeof(RebirthNpcWorkFailureCategory),(byte)failure)||
                !int.TryParse(row.GetAttribute("retries"),NumberStyles.Integer,CultureInfo.InvariantCulture,out retries)||retries<0||
                !long.TryParse(row.GetAttribute("started"),NumberStyles.Integer,CultureInfo.InvariantCulture,out started)||started<0||
                !long.TryParse(row.GetAttribute("completed"),NumberStyles.Integer,CultureInfo.InvariantCulture,out completed)||completed<=0||completed<started||completed>DateTime.MaxValue.Ticks)return false;
            var groups=new HashSet<string>(StringComparer.Ordinal);
            foreach(XmlNode child in row.ChildNodes)
            {
                var group=child as XmlElement;if(group==null)continue;
                if((group.Name!="produced"&&group.Name!="consumed")||!groups.Add(group.Name))return false;
                var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(XmlNode entry in group.ChildNodes)
                {
                    var item=entry as XmlElement;if(item==null)continue;int count;string key=item.GetAttribute("key");
                    if(item.Name!="item"||item.HasChildNodes||string.IsNullOrWhiteSpace(key)||!keys.Add(key)||
                        !int.TryParse(item.GetAttribute("count"),NumberStyles.Integer,CultureInfo.InvariantCulture,out count)||count==0)return false;
                }
            }
        }
        return true;
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
