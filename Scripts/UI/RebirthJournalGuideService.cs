using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Private per-world/per-player reading preferences; gameplay evidence remains authoritative.
public static class RebirthJournalGuideService
{
    private sealed class Definition { public RebirthJournalEntry Entry; public string Trigger; public string[] Challenges; }
    private static readonly List<Definition> Definitions = new List<Definition>();
    private static readonly Dictionary<string, string> Unlocked = new Dictionary<string, string>();
    private static readonly Dictionary<string, bool> Read = new Dictionary<string, bool>();
    private static string path;
    private static float nextPoll;
    private static bool writable, dirty;
    public static int Revision { get; private set; }
    public static string Error { get; private set; }
    public static string Root => Path.Combine(Path.GetDirectoryName(typeof(RebirthJournalGuideService).Assembly.Location), "Resources", "Journal");
    public static int UnreadCount
    {
        get
        {
            int count = 0;
            foreach (var definition in Definitions)
                if (Unlocked.ContainsKey(definition.Entry.Id) && !IsRead(definition.Entry)) count++;
            return count;
        }
    }
    public static bool IsRead(RebirthJournalEntry entry) => Read.TryGetValue(entry.Id, out var read) ? read : !entry.Authored;
    public static List<RebirthJournalEntry> Entries => Definitions.Where(d => Unlocked.ContainsKey(d.Entry.Id)).Select(d => {
        d.Entry.Created = Unlocked[d.Entry.Id]; return d.Entry;
    }).ToList();

    public static void Reset()
    {
        path = null; nextPoll = 0f; writable = false; dirty = false; Error = null;
        Definitions.Clear(); Unlocked.Clear(); Read.Clear(); Revision++;
    }

    public static void Poll(EntityPlayer player,bool force=false)
    {
        if (player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld() || !force && Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + 2f;
        bool loading = false;
        try
        {
            string target = RebirthJournalStore.ResolvePath() + ".guides.xml";
            if (target != path)
            {
                loading = true;
                path = target; writable = false; dirty = false; Definitions.Clear(); Unlocked.Clear(); Read.Clear();
                var doc = XDocument.Load(Path.Combine(Root, "entries.xml"));
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in doc.Root.Elements("entry")) {
                    string id = (string)e.Attribute("id");
                    if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new IOException("Invalid journal guide definition identity");
                    Definitions.Add(new Definition {
                    Entry = new RebirthJournalEntry { Id=(string)e.Attribute("id"), Title=Localization.Get((string)e.Element("title")),
                        Body=Localization.Get((string)e.Element("body")), Type="Lore", Authored=true, Image=(string)e.Element("image") },
                    Trigger=(string)e.Attribute("trigger"), Challenges=((string)e.Attribute("challenges")??"").Split(',') });
                }
                if (File.Exists(path) || File.Exists(path+".bak"))
                {
                    XDocument saved; bool recovered; string error;
                    if (!RebirthAtomicXmlFile.TryLoadFinalThenBackup(path,out saved,out recovered,out error) || saved.Root?.Name != "guideState")
                        throw new IOException(error ?? "Invalid journal guide state");
                    foreach (var e in saved.Root.Elements("entry"))
                    {
                        string id=(string)e.Attribute("id");
                        if (string.IsNullOrEmpty(id) || Read.ContainsKey(id)) throw new IOException("Invalid journal guide identity");
                        Read[id]=(bool?)e.Attribute("read")??false;
                        string when=(string)e.Attribute("unlocked"); if (!string.IsNullOrEmpty(when)) Unlocked[id]=when;
                    }
                }
                writable=true; Error=null; loading=false; Revision++;
            }
            if (!writable) return;
            var achieved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (player.challengeJournal != null)
                foreach (var c in player.challengeJournal.Challenges)
                    if (c?.ChallengeClass != null && c.ObjectiveList != null) {
                        if(c.ObjectiveList.Any(o => o.Current > 0)) achieved.Add(c.ChallengeClass.Name);
                        if(c.ObjectiveList.Count > 0 && c.ObjectiveList.All(o => o.Complete)) completed.Add(c.ChallengeClass.Name);
                    }
            RebirthMetabolismSnapshot met; bool hasMet=RebirthMetabolismClientState.TryGet(out met);
            bool changed=false;
            foreach (var d in Definitions)
            {
                if (Unlocked.ContainsKey(d.Entry.Id)) continue;
                bool ready=d.Trigger=="intro";
                var evidence=d.Trigger=="completed"?completed:achieved;
                foreach (string challenge in d.Challenges)
                    if (evidence.Contains(challenge)) { ready=true; break; }
                if (hasMet && d.Trigger=="ingestion") ready |= met.StomachFluidMl+met.StomachSolidMl+met.IntestinalFluidMl+met.IntestinalSolidMl>1f;
                if (!ready) continue;
                Unlocked[d.Entry.Id]=DateTime.UtcNow.ToString("o"); Read[d.Entry.Id]=false; changed=true;
            }
            if(changed){Revision++;dirty=true;}
            if(dirty)dirty=!Save();
        }
        catch(Exception ex)
        {
            writable=false;
            if (loading)
            {
                // Do not expose partially loaded preferences or overwrite the failed file.
                // Retry transient read failures at a bounded rate instead of staying disabled.
                path=null; Definitions.Clear(); Unlocked.Clear(); Read.Clear(); Revision++;
                nextPoll=Time.realtimeSinceStartup+30f;
            }
            if (Error!=ex.Message) Log.Warning("[REBIRTH Journal Guides] "+ex.Message);
            Error=ex.Message;
        }
    }
    public static bool Mark(RebirthJournalEntry entry, bool read)
    {
        if (!writable || entry==null) return false;
        if(IsRead(entry)==read)return true;
        bool had=Read.TryGetValue(entry.Id,out var old); Read[entry.Id]=read;
        if(!Save()){if(had)Read[entry.Id]=old;else Read.Remove(entry.Id);return false;}
        Revision++; return true;
    }
    public static bool MarkAll(IEnumerable<RebirthJournalEntry> entries)
    {
        if(!writable)return false;
        var before=new Dictionary<string,bool>(Read);
        bool changed=false;
        foreach(var e in entries)
        {
            if(e==null || IsRead(e))continue;
            Read[e.Id]=true; changed=true;
        }
        if(!changed)return true;
        if(!Save()){Read.Clear();foreach(var kv in before)Read[kv.Key]=kv.Value;return false;}
        Revision++;return true;
    }
    private static bool Save()
    {
        var root=new XElement("guideState",new XAttribute("version",1));
        foreach(var kv in Read)root.Add(new XElement("entry",new XAttribute("id",kv.Key),new XAttribute("read",kv.Value),
            new XAttribute("unlocked",Unlocked.TryGetValue(kv.Key,out var when)?when:"")));
        string error;bool ok=RebirthAtomicXmlFile.TryWrite(path,new XDocument(root),out error);
        Error=ok?null:error;return ok;
    }
}
