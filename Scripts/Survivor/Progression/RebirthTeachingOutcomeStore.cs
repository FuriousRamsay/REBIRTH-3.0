using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Small durable coordinator for cross-player teaching completion. The journal is written before
/// either participant is mutated; each participant snapshot also records its own outcome receipt,
/// so a crash between the player save and journal acknowledgement remains retry-safe.
/// </summary>
public sealed class RebirthTeachingDurableOutcome
{
    public string OutcomeId = string.Empty;
    public string InstructorStorageKey = string.Empty;
    public string StudentStorageKey = string.Empty;
    public string InstructorCreationId = string.Empty;
    public string StudentCreationId = string.Empty;
    public string SkillId = string.Empty;
    public float StudentKnowledgeTarget;
    public string HistoryKey = string.Empty;
    public int HistoryTargetCount;
    public long CompletedUtcTicks;
    public bool HasLastingLesson;
    public float LastingLessonSeconds;
    public float LastingLessonMultiplier = 1f;
    public float TeacherAward;
    public bool StudentApplied;
    public bool InstructorApplied;
    public bool RewardApplied;
    public long OriginalOrdinal;
    public bool SoloEvidenceApplied;

    public RebirthTeachingDurableOutcome Clone()
    {
        return (RebirthTeachingDurableOutcome)MemberwiseClone();
    }
}

public static class RebirthTeachingOutcomeStore
{
    private const int SchemaVersion = 3;
    private static long issuedOrdinal;
    private static bool journalDirty;
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, RebirthTeachingDurableOutcome> Pending =
        new Dictionary<string, RebirthTeachingDurableOutcome>(StringComparer.Ordinal);
    private static bool loaded;
    private static bool loadFailed;
    private static bool backupUnknown;
    private static readonly HashSet<string> BackupOutcomeIds = new HashSet<string>(StringComparer.Ordinal);
    private static string loadedPath = string.Empty;

    private static string CurrentPath
    {
        get
        {
            string save = string.Empty;
            try { save = GameIO.GetSaveGameDir() ?? string.Empty; } catch { }
            return string.IsNullOrEmpty(save) ? string.Empty : Path.Combine(save, "RebirthData", "Survivor", "TeachingOutcomes.xml");
        }
    }

    public static bool TryReserve(RebirthTeachingDurableOutcome outcome,out string error)
        =>TryReserveCore(outcome,false,0,out error);
    internal static bool TryReserveOriginal(RebirthTeachingDurableOutcome outcome,long minimumOrdinal,out string error)
        =>TryReserveCore(outcome,true,minimumOrdinal,out error);
    private static bool TryReserveCore(RebirthTeachingDurableOutcome outcome,bool original,long minimumOrdinal,out string error)
    {
        error=string.Empty;
        if(!ValidOutcome(outcome)||!HasCharacterBinding(outcome)||minimumOrdinal<0){error="teaching outcome is invalid";return false;}
        lock(Gate)
        {
            EnsureLoadedLocked();if(loadFailed){error="teaching outcome history is unavailable";return false;}
            RebirthTeachingDurableOutcome existing;
            if(Pending.TryGetValue(outcome.OutcomeId,out existing))
            {
                if(!SameIntent(existing,outcome)||(outcome.OriginalOrdinal!=0&&outcome.OriginalOrdinal!=existing.OriginalOrdinal)||original!=(existing.OriginalOrdinal>0))
                {error="teaching outcome id already belongs to a different lesson";return false;}
                outcome.OriginalOrdinal=existing.OriginalOrdinal;outcome.SoloEvidenceApplied=existing.SoloEvidenceApplied;
                return !journalDirty&&HasSavedSnapshotLocked()||SaveLocked(out error);
            }
            if(outcome.OriginalOrdinal!=0||outcome.StudentApplied||outcome.InstructorApplied||outcome.RewardApplied||outcome.SoloEvidenceApplied)
            {error="new teaching outcome contains acknowledged stages";return false;}
            if(original)
            {
                long floor=Math.Max(issuedOrdinal,minimumOrdinal);
                if(floor==long.MaxValue||outcome.StudentKnowledgeTarget>100f){error="teaching original ordinal or target is unavailable";return false;}
                issuedOrdinal=floor+1;outcome.OriginalOrdinal=issuedOrdinal;
            }
            else outcome.SoloEvidenceApplied=true; // Legacy/public reservations never acquire retroactive Solo evidence.
            Pending.Add(outcome.OutcomeId,outcome.Clone());journalDirty=true;
            return SaveLocked(out error); // Unknown writes retain the exact original request, never allocate a replacement ordinal.
        }
    }
    internal static bool TryGetOriginal(RebirthTeachingDurableOutcome outcome,out RebirthTeachingDurableOutcome original)
    {
        original=null;lock(Gate)
        {
            EnsureLoadedLocked();if(loadFailed||journalDirty||outcome==null||outcome.OriginalOrdinal<1||!HasSavedSnapshotLocked())return false;
            if(!Pending.TryGetValue(outcome.OutcomeId,out var current)||current.OriginalOrdinal!=outcome.OriginalOrdinal||!SameIntent(current,outcome))return false;
            original=current.Clone();return true;
        }
    }
    internal static long[] PendingOriginalOrdinals(string owner,string creation)
    {
        lock(Gate){EnsureLoadedLocked();var result=new List<long>();if(loadFailed||journalDirty)return result.ToArray();foreach(var o in Pending.Values)if(o.OriginalOrdinal>0&&!o.SoloEvidenceApplied&&o.InstructorStorageKey==owner&&o.InstructorCreationId==creation)result.Add(o.OriginalOrdinal);return result.ToArray();}
    }

    public static RebirthTeachingDurableOutcome[] Snapshot()
    {
        lock (Gate)
        {
            EnsureLoadedLocked();
            if(loadFailed)return new RebirthTeachingDurableOutcome[0];
            if(journalDirty){string retry;if(!SaveLocked(out retry))return new RebirthTeachingDurableOutcome[0];}
            RebirthTeachingDurableOutcome[] result = new RebirthTeachingDurableOutcome[Pending.Count];
            int i = 0; foreach (RebirthTeachingDurableOutcome o in Pending.Values) result[i++] = o.Clone();
            return result;
        }
    }

    public static bool AcknowledgeStudent(string outcomeId) { return Acknowledge(outcomeId, 1); }
    public static bool AcknowledgeInstructor(string outcomeId) { return Acknowledge(outcomeId, 2); }
    public static bool AcknowledgeReward(string outcomeId) { return Acknowledge(outcomeId, 3); }

    internal static bool AcknowledgeSolo(string outcomeId) { return Acknowledge(outcomeId,4); }

    private static bool Acknowledge(string outcomeId, int stage)
    {
        if (string.IsNullOrEmpty(outcomeId)) return false;
        lock (Gate)
        {
            EnsureLoadedLocked();
            if(loadFailed)return false;
            if(journalDirty){string retry;if(!SaveLocked(out retry))return false;}
            RebirthTeachingDurableOutcome current;
            if (!Pending.TryGetValue(outcomeId, out current) || current == null) return true;
            if (!HasCharacterBinding(current)) return false;
            if (stage == 1) current.StudentApplied = true;
            else if (stage == 2) current.InstructorApplied = true;
            else if(stage==3) current.RewardApplied = true;
            else current.SoloEvidenceApplied=true;
            bool remove = current.StudentApplied && current.InstructorApplied && current.RewardApplied && current.SoloEvidenceApplied;
            if (remove) Pending.Remove(outcomeId);
            journalDirty=true;
            string error;
            if (SaveLocked(out error)) return true;
            // Keep the original attempted stage under an uncertainty hold; do not guess rollback.
            Log.Warning("[REBIRTH Teaching] durable outcome acknowledgement failed stage=" + stage + " id=" + outcomeId + " error=" + error);
            return false;
        }
    }

    // Keep all receipts needed by pending lessons, plus a bounded ordinary history.
    // Unavailable journal state cannot prove that a teaching receipt is disposable.
    public static void PruneAwardReceipts(HashSet<string> receipts,string protectedReceipt=null)
    {
        if(receipts==null||receipts.Count<=256)return;
        lock(Gate)
        {
            EnsureLoadedLocked();
            var ordinary=new List<string>();
            foreach(string receipt in receipts)
            {
                bool protect=string.Equals(receipt,protectedReceipt,StringComparison.Ordinal);
                if(receipt!=null&&receipt.StartsWith("teaching:",StringComparison.Ordinal))
                {
                    int suffix=receipt.LastIndexOf(':');
                    if(suffix>9)
                    {
                        string stage=receipt.Substring(suffix+1);
                        if(stage=="student"||stage=="instructor"||stage=="reward"||stage=="solo")
                            protect=loadFailed||backupUnknown||Pending.ContainsKey(receipt.Substring(9,suffix-9))||BackupOutcomeIds.Contains(receipt.Substring(9,suffix-9));
                    }
                }
                if(!protect)ordinary.Add(receipt);
            }
            ordinary.Sort(StringComparer.Ordinal);
            for(int i=0;i<ordinary.Count-256;i++)receipts.Remove(ordinary[i]);
        }
    }
    public static void Reset()
    {
        lock (Gate)
        {
            Pending.Clear(); issuedOrdinal=0; journalDirty=false; BackupOutcomeIds.Clear(); backupUnknown=false; loadFailed = false; loaded = false; loadedPath = string.Empty;
        }
    }

    private static void EnsureLoadedLocked()
    {
        string path = CurrentPath;
        if (loaded && string.Equals(path, loadedPath, StringComparison.Ordinal)) return;
        Pending.Clear(); issuedOrdinal=0; journalDirty=false; loadFailed = true; loaded = true; loadedPath = path;
        RefreshBackupProtectionLocked(path);
        if (string.IsNullOrEmpty(path)) return;
        if(!File.Exists(path)&&!File.Exists(path+".bak")&&!File.Exists(path+".tmp"))
        {loadFailed=false;return;}
        XDocument doc; bool usedBackup; string error;
        if (!RebirthAtomicXmlFile.TryLoadFinalThenBackup(path, out doc, out usedBackup, out error)) return;
        if (doc == null || doc.Root == null || doc.Root.Name.LocalName != "rebirthTeachingOutcomes") return;
        int version; if (!int.TryParse((string)doc.Root.Attribute("version"), NumberStyles.Integer, CultureInfo.InvariantCulture, out version) || (version != 1 && version != 2 && version != SchemaVersion)) return;
        long loadedIssued=0;
        if(version==3&&(!long.TryParse(A(doc.Root,"issued"),NumberStyles.None,CultureInfo.InvariantCulture,out loadedIssued)||loadedIssued<0||!StrictRoot(doc.Root)))return;
        var originalOrdinals=new HashSet<long>();
        foreach (XElement x in doc.Root.Elements())
        {
            RebirthTeachingDurableOutcome o;
            if(!TryReadOutcome(x,version,out o)||Pending.ContainsKey(o.OutcomeId)||(o.OriginalOrdinal>0&&(o.OriginalOrdinal>loadedIssued||!originalOrdinals.Add(o.OriginalOrdinal))))
            {Pending.Clear();return;}
            Pending.Add(o.OutcomeId,o);
        }        issuedOrdinal=Math.Max(issuedOrdinal,loadedIssued);loadFailed=false;journalDirty=version<3||issuedOrdinal!=loadedIssued;
        if (usedBackup) { string rewrite; if(!SaveLocked(out rewrite))loadFailed=true; }
    }

    private static void RefreshBackupProtectionLocked(string path)
    {
        BackupOutcomeIds.Clear();backupUnknown=true;
        if(string.IsNullOrEmpty(path))return;
        try
        {
            if(!File.Exists(path+".bak")){backupUnknown=false;return;}
            var doc=XDocument.Load(path+".bak",LoadOptions.None);
            int version;
            if(doc.Root==null||doc.Root.Name!="rebirthTeachingOutcomes"||
                !int.TryParse(A(doc.Root,"version"),out version)||(version!=1&&version!=2&&version!=SchemaVersion))return;
            long backupIssued=0;if(version==3&&(!long.TryParse(A(doc.Root,"issued"),NumberStyles.None,CultureInfo.InvariantCulture,out backupIssued)||backupIssued<0||!StrictRoot(doc.Root)))return;
            var ordinals=new HashSet<long>();
            foreach(var node in doc.Root.Elements())
            {
                RebirthTeachingDurableOutcome outcome;
                if(!TryReadOutcome(node,version,out outcome)||!BackupOutcomeIds.Add(outcome.OutcomeId)||(outcome.OriginalOrdinal>0&&(outcome.OriginalOrdinal>backupIssued||!ordinals.Add(outcome.OriginalOrdinal))))return;
            }
            issuedOrdinal=Math.Max(issuedOrdinal,backupIssued);backupUnknown=false;
        }
        catch{backupUnknown=true;}
    }
    // Acknowledgement flags evolve separately; the reserved lesson itself is immutable.
    private static bool SameIntent(RebirthTeachingDurableOutcome a,RebirthTeachingDurableOutcome b)
        =>a!=null&&b!=null&&a.OutcomeId==b.OutcomeId&&a.InstructorStorageKey==b.InstructorStorageKey
            &&a.StudentStorageKey==b.StudentStorageKey&&a.SkillId==b.SkillId
            &&a.InstructorCreationId==b.InstructorCreationId&&a.StudentCreationId==b.StudentCreationId
            &&a.StudentKnowledgeTarget==b.StudentKnowledgeTarget&&a.HistoryKey==b.HistoryKey
            &&a.HistoryTargetCount==b.HistoryTargetCount&&a.CompletedUtcTicks==b.CompletedUtcTicks
            &&a.HasLastingLesson==b.HasLastingLesson&&a.LastingLessonSeconds==b.LastingLessonSeconds
            &&a.LastingLessonMultiplier==b.LastingLessonMultiplier&&a.TeacherAward==b.TeacherAward;
    private static bool FiniteNonnegative(float value)
        =>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=0f;
    private static bool ValidOutcome(RebirthTeachingDurableOutcome o)
        =>o!=null&&!string.IsNullOrWhiteSpace(o.OutcomeId)&&!string.IsNullOrWhiteSpace(o.InstructorStorageKey)
            &&!string.IsNullOrWhiteSpace(o.StudentStorageKey)&&!string.IsNullOrWhiteSpace(o.SkillId)
            &&!string.IsNullOrWhiteSpace(o.HistoryKey)&&o.HistoryTargetCount>=0
            &&o.CompletedUtcTicks>=0&&o.CompletedUtcTicks<=DateTime.MaxValue.Ticks
            &&FiniteNonnegative(o.StudentKnowledgeTarget)&&FiniteNonnegative(o.LastingLessonSeconds)
            &&FiniteNonnegative(o.LastingLessonMultiplier)&&FiniteNonnegative(o.TeacherAward);
    private static bool TryReadOutcome(XElement x,int version,out RebirthTeachingDurableOutcome outcome)
    {
        outcome=null;if(x.Name!="outcome"||x.HasElements||!string.IsNullOrWhiteSpace(x.Value))return false;
        var o=new RebirthTeachingDurableOutcome{OutcomeId=A(x,"id"),InstructorStorageKey=A(x,"instructor"),
            StudentStorageKey=A(x,"student"),SkillId=A(x,"skill"),HistoryKey=A(x,"historyKey")};
        if(!float.TryParse(A(x,"studentTarget"),NumberStyles.Float,CultureInfo.InvariantCulture,out o.StudentKnowledgeTarget)
            ||!int.TryParse(A(x,"historyTarget"),NumberStyles.Integer,CultureInfo.InvariantCulture,out o.HistoryTargetCount)
            ||!long.TryParse(A(x,"completedUtcTicks"),NumberStyles.Integer,CultureInfo.InvariantCulture,out o.CompletedUtcTicks)
            ||!float.TryParse(A(x,"lastingSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out o.LastingLessonSeconds)
            ||!float.TryParse(A(x,"lastingMultiplier"),NumberStyles.Float,CultureInfo.InvariantCulture,out o.LastingLessonMultiplier)
            ||!float.TryParse(A(x,"teacherAward"),NumberStyles.Float,CultureInfo.InvariantCulture,out o.TeacherAward)
            ||!ValidOutcome(o))return false;
        foreach(string name in new[]{"lasting","studentApplied","instructorApplied","rewardApplied"})
            if(A(x,name)!="0"&&A(x,name)!="1")return false;
        o.HasLastingLesson=A(x,"lasting")=="1";o.StudentApplied=A(x,"studentApplied")=="1";
        o.InstructorApplied=A(x,"instructorApplied")=="1";o.RewardApplied=A(x,"rewardApplied")=="1";
        if(version>=2)
        {
            o.InstructorCreationId=A(x,"instructorCreation");
            o.StudentCreationId=A(x,"studentCreation");
            bool unbound=A(x,"binding")=="legacy-unbound" &&
                o.InstructorCreationId.Length==0 && o.StudentCreationId.Length==0;
            if(!unbound && (A(x,"binding")!="character" || !HasCharacterBinding(o)))return false;
        }
        if(version==3)
        {
            if(!long.TryParse(A(x,"originalOrdinal"),NumberStyles.None,CultureInfo.InvariantCulture,out o.OriginalOrdinal)||o.OriginalOrdinal<0||(A(x,"soloApplied")!="0"&&A(x,"soloApplied")!="1")||!StrictOutcome(x))return false;
            o.SoloEvidenceApplied=A(x,"soloApplied")=="1";
            if(o.OriginalOrdinal==0&&!o.SoloEvidenceApplied||o.OriginalOrdinal>0&&(!HasCharacterBinding(o)||o.StudentKnowledgeTarget>100f))return false;
        }
        else o.SoloEvidenceApplied=true;
        outcome=o;return true;
    }
    public static bool HasCharacterBinding(RebirthTeachingDurableOutcome outcome)
    {
        string instructor,student;
        return outcome!=null &&
            RebirthSurvivorRequestScope.TryNormalize(outcome.InstructorCreationId,out instructor) &&
            RebirthSurvivorRequestScope.TryNormalize(outcome.StudentCreationId,out student);
    }
    private static bool SaveLocked(out string error)
    {
        error = string.Empty;
        string path = CurrentPath;
        if(loadFailed||!string.Equals(path,loadedPath,StringComparison.Ordinal)){error="teaching outcome history is unavailable or changed";return false;}
        if (string.IsNullOrEmpty(path)) { error = "save directory unavailable"; return false; }
        RefreshBackupProtectionLocked(path);
        RebirthAtomicXmlFile.TryWrite(path,BuildDocumentLocked(),out error);
        bool exact=HasSavedSnapshotLocked();journalDirty=!exact;RefreshBackupProtectionLocked(path);
        return exact;
    }
    private static XDocument BuildDocumentLocked()
    {
        XElement root = new XElement("rebirthTeachingOutcomes", new XAttribute("version", SchemaVersion),new XAttribute("issued",issuedOrdinal));
        List<string> ids = new List<string>(Pending.Keys); ids.Sort(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; ++i)
        {
            RebirthTeachingDurableOutcome o = Pending[ids[i]]; if (o == null) continue;
            root.Add(new XElement("outcome",
                new XAttribute("id", o.OutcomeId ?? string.Empty), new XAttribute("instructor", o.InstructorStorageKey ?? string.Empty), new XAttribute("student", o.StudentStorageKey ?? string.Empty),
                new XAttribute("binding", HasCharacterBinding(o) ? "character" : "legacy-unbound"),
                new XAttribute("instructorCreation", o.InstructorCreationId ?? string.Empty), new XAttribute("studentCreation", o.StudentCreationId ?? string.Empty),
                new XAttribute("skill", o.SkillId ?? string.Empty), new XAttribute("studentTarget", F(o.StudentKnowledgeTarget)), new XAttribute("historyKey", o.HistoryKey ?? string.Empty),
                new XAttribute("historyTarget", Math.Max(0, o.HistoryTargetCount)), new XAttribute("completedUtcTicks", Math.Max(0L, o.CompletedUtcTicks)),
                new XAttribute("lasting", o.HasLastingLesson ? "1" : "0"), new XAttribute("lastingSeconds", F(o.LastingLessonSeconds)), new XAttribute("lastingMultiplier", F(o.LastingLessonMultiplier)),
                new XAttribute("teacherAward", F(o.TeacherAward)), new XAttribute("studentApplied", o.StudentApplied ? "1" : "0"), new XAttribute("instructorApplied", o.InstructorApplied ? "1" : "0"), new XAttribute("rewardApplied", o.RewardApplied ? "1" : "0"),new XAttribute("originalOrdinal",o.OriginalOrdinal),new XAttribute("soloApplied",o.SoloEvidenceApplied?"1":"0")));
        }
        return new XDocument(root);
    }

    private static bool HasSavedSnapshotLocked()
    {
        try{string path=CurrentPath;if(loadFailed||string.IsNullOrEmpty(path)||path!=loadedPath||!File.Exists(path)||new FileInfo(path).Length>64L*1024*1024)return false;var saved=XDocument.Load(path,LoadOptions.None);return XNode.DeepEquals(saved.Root,BuildDocumentLocked().Root);}catch{return false;}
    }
    private static bool StrictRoot(XElement root)
    {return root.Attributes().Count()==2&&root.Attribute("version")!=null&&root.Attribute("issued")!=null&&!root.Nodes().Any(n=>!(n is XElement)&&!(n is XText t&&string.IsNullOrWhiteSpace(t.Value)));}
    private static bool StrictOutcome(XElement x)
    {
        var names=new HashSet<string>(new[]{"id","instructor","student","binding","instructorCreation","studentCreation","skill","studentTarget","historyKey","historyTarget","completedUtcTicks","lasting","lastingSeconds","lastingMultiplier","teacherAward","studentApplied","instructorApplied","rewardApplied","originalOrdinal","soloApplied"},StringComparer.Ordinal);
        return x.Attributes().Count()==names.Count&&x.Attributes().All(a=>a.Name.NamespaceName.Length==0&&names.Contains(a.Name.LocalName));
    }

    private static string A(XElement x, string name) { XAttribute a = x != null ? x.Attribute(name) : null; return a != null ? a.Value : string.Empty; }
    private static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
}
