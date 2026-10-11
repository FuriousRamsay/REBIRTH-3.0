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
    private static readonly HashSet<string> DismissedPurge = new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string> AchievedEvidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> CompletedEvidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
        Definitions.Clear(); Unlocked.Clear(); Read.Clear(); DismissedPurge.Clear(); AchievedEvidence.Clear(); CompletedEvidence.Clear(); Revision++;
    }

    public static void ObserveBackpackEquip(EntityPlayer player)
    {
        if (player == null || !ReferenceEquals(player.world?.GetPrimaryPlayer(), player)) return;
        if (Unlocked.ContainsKey("BackpackReadingMaterials") && Unlocked.ContainsKey("BackpackForSale")) return;
        if (HasBackpack(player)) Poll(player, true);
    }
    public static void ObservePurgeSupply(EntityPlayer player) { ObservePurgeGuide(player,"PurgeSupplies"); }
    internal static RebirthJournalEntry ObservePurgeGuide(EntityPlayer player,string id)
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||
           player==null||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player))return null;
        Poll(player,true);
        if(!writable)return null;
        var definition=Definitions.FirstOrDefault(d=>d.Entry.Id==id && d.Trigger.StartsWith("purge-",StringComparison.Ordinal));
        if(definition==null)return null;
        if(!Unlocked.ContainsKey(id))
        {
            Unlocked[id]=DateTime.UtcNow.ToString("o");Read[id]=false;
            Revision++;dirty=!Save();
        }
        return definition.Entry;
    }
    internal static bool TryGetUnlockedPurgeGuides(EntityPlayerLocal player,out List<RebirthJournalEntry> entries)
    {
        entries=null;
        if(player==null || !ReferenceEquals(player.world?.GetPrimaryPlayer(),player))return false;
        Poll(player,true);
        if(!writable)return false;
        entries=new List<RebirthJournalEntry>();
        foreach(var definition in Definitions)
            if(definition.Trigger.StartsWith("purge-",StringComparison.Ordinal) &&
                Unlocked.ContainsKey(definition.Entry.Id) && !DismissedPurge.Contains(definition.Entry.Id))
                entries.Add(definition.Entry);
        return true;
    }
    private static bool HasBackpack(EntityPlayer player)
    {
        if (player?.world == null) return false;
        string item;
        if (player.world.IsRemote())
            item = RebirthSurvivorClientState.GetProjectedGearItem(player, RebirthSurvivorGearService.BackpackSlotId);
        else
        {
            RebirthWorldCharacterRecord record;
            if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete
                || record.Support == null || !record.Support.EquippedGearBySlot.TryGetValue(RebirthSurvivorGearService.BackpackSlotId, out item)) return false;
        }
        return RebirthBackpackLibraryPolicy.CapacityForBackpack(item) > 0;
    }
    public static void Poll(EntityPlayer player,bool force=false)
    {
        if (player == null || (!RebirthSurvivorMode.IsEnabledForCurrentWorld() && !(RebirthPurgeReleasePolicy.Enabled && RebirthSandboxOptionManager.Current.IsPurge)) || !force && Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + 2f;
        bool loading = false;
        try
        {
            string target = RebirthJournalStore.ResolvePath() + ".guides.xml";
            if (target != path)
            {
                loading = true;
                path = target; writable = false; dirty = false; Definitions.Clear(); Unlocked.Clear(); Read.Clear(); DismissedPurge.Clear();
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
                        if ((bool?)e.Attribute("purgeDismissed") ?? Read[id]) DismissedPurge.Add(id);
                        string when=(string)e.Attribute("unlocked"); if (!string.IsNullOrEmpty(when)) Unlocked[id]=when;
                    }
                }
                writable=true; Error=null; loading=false; Revision++;
            }
            if (!writable) return;
            bool needsChallenges=false, needsMetabolism=false, needsBackpack=false;
            foreach (var definition in Definitions)
            {
                if (Unlocked.ContainsKey(definition.Entry.Id)) continue;
                foreach (var challenge in definition.Challenges)
                    if (!string.IsNullOrEmpty(challenge)) { needsChallenges=true; break; }
                needsMetabolism |= definition.Trigger=="ingestion";
                needsBackpack |= definition.Trigger=="backpack-equipped";
            }
            AchievedEvidence.Clear(); CompletedEvidence.Clear();
            if (needsChallenges && player.challengeJournal != null)
                foreach (var c in player.challengeJournal.Challenges)
                    if (c?.ChallengeClass != null && c.ObjectiveList != null) {
                        bool any=false, all=c.ObjectiveList.Count>0;
                        foreach (var objective in c.ObjectiveList) {
                            any |= objective.Current>0;
                            all &= objective.Complete;
                        }
                        if(any) AchievedEvidence.Add(c.ChallengeClass.Name);
                        if(all) CompletedEvidence.Add(c.ChallengeClass.Name);
                    }
            RebirthMetabolismSnapshot met=default(RebirthMetabolismSnapshot);
            bool hasMet=needsMetabolism && RebirthMetabolismClientState.TryGet(out met);
            bool backpackEquipped=needsBackpack && HasBackpack(player);
            bool changed=false;
            foreach (var d in Definitions)
            {
                if (Unlocked.ContainsKey(d.Entry.Id)) continue;
                bool ready=d.Trigger=="intro" || d.Trigger=="backpack-equipped" && backpackEquipped;
                var evidence=d.Trigger=="completed"?CompletedEvidence:AchievedEvidence;
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
                path=null; Definitions.Clear(); Unlocked.Clear(); Read.Clear(); DismissedPurge.Clear(); AchievedEvidence.Clear(); CompletedEvidence.Clear(); Revision++;
                nextPoll=Time.realtimeSinceStartup+30f;
            }
            if (Error!=ex.Message) Log.Warning("[REBIRTH Journal Guides] "+ex.Message);
            Error=ex.Message;
        }
    }
    internal static bool IsPurgeDismissed(RebirthJournalEntry entry) => entry != null && DismissedPurge.Contains(entry.Id);
    internal static void DismissPurge(RebirthJournalEntry entry)
    {
        if (!writable || entry == null || !entry.Authored || !Unlocked.ContainsKey(entry.Id)) return;
        if (!DismissedPurge.Add(entry.Id)) return;
        // Popup acknowledgement is separate from reading the retained Journal entry.
        dirty = !Save();
        Revision++;
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
        foreach(var kv in Read)root.Add(new XElement("entry",new XAttribute("id",kv.Key),new XAttribute("read",kv.Value),new XAttribute("purgeDismissed",DismissedPurge.Contains(kv.Key)),
            new XAttribute("unlocked",Unlocked.TryGetValue(kv.Key,out var when)?when:"")));
        string error;bool ok=RebirthAtomicXmlFile.TryWrite(path,new XDocument(root),out error);
        Error=ok?null:error;return ok;
    }
}
