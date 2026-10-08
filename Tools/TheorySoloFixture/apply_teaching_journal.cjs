const fs=require('fs');const path='Scripts/Survivor/Progression/RebirthTeachingOutcomeStore.cs';let s=fs.readFileSync(path,'utf8');function replace(a,b){if(!s.includes(a))throw Error('missing '+a.slice(0,80));s=s.replace(a,b);}
replace('    public bool RewardApplied;','    public bool RewardApplied;\n    public long OriginalOrdinal;\n    public bool SoloEvidenceApplied;');replace('private const int SchemaVersion = 2;','private const int SchemaVersion = 3;\n    private static long issuedOrdinal;\n    private static bool journalDirty;');
const start=s.indexOf('    public static bool TryReserve('),end=s.indexOf('    public static RebirthTeachingDurableOutcome[] Snapshot()',start);
s=s.slice(0,start)+`    public static bool TryReserve(RebirthTeachingDurableOutcome outcome,out string error)
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

`+s.slice(end);
replace('if(loadFailed)return new RebirthTeachingDurableOutcome[0];','if(loadFailed)return new RebirthTeachingDurableOutcome[0];\n            if(journalDirty){string retry;if(!SaveLocked(out retry))return new RebirthTeachingDurableOutcome[0];}');
replace('    private static bool Acknowledge(string outcomeId, int stage)','    internal static bool AcknowledgeSolo(string outcomeId) { return Acknowledge(outcomeId,4); }\n\n    private static bool Acknowledge(string outcomeId, int stage)');
replace('            RebirthTeachingDurableOutcome before = current.Clone();','            if(journalDirty){string retry;if(!SaveLocked(out retry))return false;}');replace('            else current.RewardApplied = true;','            else if(stage==3) current.RewardApplied = true;\n            else current.SoloEvidenceApplied=true;');replace('current.StudentApplied && current.InstructorApplied && current.RewardApplied;','current.StudentApplied && current.InstructorApplied && current.RewardApplied && current.SoloEvidenceApplied;');replace('            if (remove) Pending.Remove(outcomeId);','            if (remove) Pending.Remove(outcomeId);\n            journalDirty=true;');replace('            Pending[outcomeId] = before;','            // Keep the original attempted stage under an uncertainty hold; do not guess rollback.');
replace('stage=="student"||stage=="instructor"||stage=="reward"','stage=="student"||stage=="instructor"||stage=="reward"||stage=="solo"');replace('Pending.Clear(); BackupOutcomeIds.Clear(); backupUnknown=false;','Pending.Clear(); issuedOrdinal=0; journalDirty=false; BackupOutcomeIds.Clear(); backupUnknown=false;');replace('Pending.Clear(); loadFailed = true; loaded = true; loadedPath = path;','Pending.Clear(); issuedOrdinal=0; journalDirty=false; loadFailed = true; loaded = true; loadedPath = path;');replace('(version != 1 && version != SchemaVersion)','(version != 1 && version != 2 && version != SchemaVersion)');
replace('        foreach (XElement x in doc.Root.Elements())','        long loadedIssued=0;\n        if(version==3&&(!long.TryParse(A(doc.Root,"issued"),NumberStyles.None,CultureInfo.InvariantCulture,out loadedIssued)||loadedIssued<0||!StrictRoot(doc.Root)))return;\n        var originalOrdinals=new HashSet<long>();\n        foreach (XElement x in doc.Root.Elements())');replace('if(!TryReadOutcome(x,version,out o)||Pending.ContainsKey(o.OutcomeId))','if(!TryReadOutcome(x,version,out o)||Pending.ContainsKey(o.OutcomeId)||(o.OriginalOrdinal>0&&(o.OriginalOrdinal>loadedIssued||!originalOrdinals.Add(o.OriginalOrdinal))))');replace('        }        loadFailed=false;','        }        issuedOrdinal=Math.Max(issuedOrdinal,loadedIssued);loadFailed=false;');replace('(version!=1&&version!=SchemaVersion)','(version!=1&&version!=2&&version!=SchemaVersion)');replace('            foreach(var node in doc.Root.Elements())','            long backupIssued=0;if(version==3&&(!long.TryParse(A(doc.Root,"issued"),NumberStyles.None,CultureInfo.InvariantCulture,out backupIssued)||backupIssued<0||!StrictRoot(doc.Root)))return;\n            var ordinals=new HashSet<long>();\n            foreach(var node in doc.Root.Elements())');replace('if(!TryReadOutcome(node,version,out outcome)||!BackupOutcomeIds.Add(outcome.OutcomeId))return;','if(!TryReadOutcome(node,version,out outcome)||!BackupOutcomeIds.Add(outcome.OutcomeId)||(outcome.OriginalOrdinal>0&&(outcome.OriginalOrdinal>backupIssued||!ordinals.Add(outcome.OriginalOrdinal))))return;');replace('            backupUnknown=false;','            issuedOrdinal=Math.Max(issuedOrdinal,backupIssued);backupUnknown=false;');
replace('        if(version==2)','        if(version>=2)');replace('        outcome=o;return true;','        if(version==3)\n        {\n            if(!long.TryParse(A(x,"originalOrdinal"),NumberStyles.None,CultureInfo.InvariantCulture,out o.OriginalOrdinal)||o.OriginalOrdinal<0||(A(x,"soloApplied")!="0"&&A(x,"soloApplied")!="1")||!StrictOutcome(x))return false;\n            o.SoloEvidenceApplied=A(x,"soloApplied")=="1";\n            if(o.OriginalOrdinal==0&&!o.SoloEvidenceApplied||o.OriginalOrdinal>0&&(!HasCharacterBinding(o)||o.StudentKnowledgeTarget>100f))return false;\n        }\n        else o.SoloEvidenceApplied=true;\n        outcome=o;return true;');
replace('        XElement root = new XElement("rebirthTeachingOutcomes", new XAttribute("version", SchemaVersion));','        bool written=RebirthAtomicXmlFile.TryWrite(path,BuildDocumentLocked(),out error);\n        bool exact=HasSavedSnapshotLocked();journalDirty=!exact;RefreshBackupProtectionLocked(path);\n        return exact;\n    }\n    private static XDocument BuildDocumentLocked()\n    {\n        XElement root = new XElement("rebirthTeachingOutcomes", new XAttribute("version", SchemaVersion),new XAttribute("issued",issuedOrdinal));');replace('new XAttribute("rewardApplied", o.RewardApplied ? "1" : "0")))','new XAttribute("rewardApplied", o.RewardApplied ? "1" : "0"),new XAttribute("originalOrdinal",o.OriginalOrdinal),new XAttribute("soloApplied",o.SoloEvidenceApplied?"1":"0")))');
replace('        bool saved=RebirthAtomicXmlFile.TryWrite(path, new XDocument(root), out error);\r\n        RefreshBackupProtectionLocked(path);\r\n        return saved;','        return new XDocument(root);');
const pos=s.indexOf('    private static string A(');s=s.slice(0,pos)+`    private static bool HasSavedSnapshotLocked()
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

`+s.slice(pos);s=s.replace('using System.IO;','using System.IO;\nusing System.Linq;');fs.writeFileSync(path,s);