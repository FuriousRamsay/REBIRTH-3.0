using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

#nullable disable

/// <summary>Atomic durable store for relationship, memory, replay and faction projections.</summary>
public static class RebirthNpcSocialPersistenceStore
{
    private const string FileName = "RebirthNpcSocialState.xml";
    private static readonly object Sync = new object();
    private static bool loaded;
    private static long loads, saves;
    private static string lastSource = "none", lastError = string.Empty;

    private static bool HasAuthoritativeWorld() => GameManager.Instance?.World != null
        && !GameManager.Instance.World.IsRemote()
        && SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer == true;

    private static string PathName { get { string d=GameIO.GetSaveGameDir(); return string.IsNullOrEmpty(d)?string.Empty:Path.Combine(d,FileName); } }

    public static void EnsureLoaded()
    {
        if (!HasAuthoritativeWorld()) return;
        lock(Sync)
        {
            if(loaded) return;
            string path=PathName; if(string.IsNullOrEmpty(path)) return;
            XmlDocument doc; string source,error;
            if(!RebirthNpcPersistenceFile.TryLoad(path, Validate, out doc, out source, out error))
            { lastError=error??string.Empty; if(RebirthNpcPersistenceFile.CanInitializeEmpty(path)) loaded=true; return; }
            try { Restore(doc); lastSource=source; lastError=error??string.Empty; loads++; loaded=true; }
            catch(Exception ex){ loaded=false; lastError=ex.GetType().Name+": "+ex.Message; Log.Warning("[REBIRTH NPC] Social persistence load failed: "+lastError); }
        }
    }

    public static void Save()
    {
        if (!HasAuthoritativeWorld()) return;
        EnsureLoaded();
        lock(Sync)
        {
            string path=PathName; RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded,path); if(string.IsNullOrEmpty(path)) return;
            XmlDocument doc=new XmlDocument(); XmlElement root=doc.CreateElement("rebirthNpcSocialState"); root.SetAttribute("version","2"); doc.AppendChild(root);
            XmlElement relationships=doc.CreateElement("relationships"); root.AppendChild(relationships);
            foreach(RebirthNpcRelationshipSnapshot r in RebirthNpcSocialService.GetRelationshipSnapshots())
            {
                XmlElement e=doc.CreateElement("relationship"); e.SetAttribute("key",r.Key??string.Empty);
                e.SetAttribute("familiarity",F(r.Familiarity)); e.SetAttribute("trust",F(r.Trust)); e.SetAttribute("respect",F(r.Respect));
                e.SetAttribute("fear",F(r.Fear)); e.SetAttribute("gratitude",F(r.Gratitude)); e.SetAttribute("suspicion",F(r.Suspicion));
                e.SetAttribute("loyalty",F(r.Loyalty)); e.SetAttribute("hostility",F(r.Hostility)); e.SetAttribute("revision",r.Revision.ToString(CultureInfo.InvariantCulture));
                e.SetAttribute("changed",r.LastChangedUtcTicks.ToString(CultureInfo.InvariantCulture)); relationships.AppendChild(e);
            }
            XmlElement memories=doc.CreateElement("memories"); root.AppendChild(memories);
            foreach(RebirthNpcSocialMemorySnapshot m in RebirthNpcSocialService.GetMemorySnapshots())
            {
                XmlElement e=doc.CreateElement("memory"); e.SetAttribute("event",m.EventId.ToString("N")); e.SetAttribute("subject",m.Subject.ToString());
                e.SetAttribute("counterparty",m.Counterparty??string.Empty); e.SetAttribute("kind",m.Kind.ToString()); e.SetAttribute("importance",F(m.Importance));
                e.SetAttribute("valence",F(m.Valence)); e.SetAttribute("confidence",F(m.Confidence)); e.SetAttribute("created",m.CreatedUtcTicks.ToString(CultureInfo.InvariantCulture));
                e.SetAttribute("reinforced",m.LastReinforcedUtcTicks.ToString(CultureInfo.InvariantCulture)); e.SetAttribute("decayed",m.LastDecayUtcTicks.ToString(CultureInfo.InvariantCulture)); memories.AppendChild(e);
            }
            XmlElement replay=doc.CreateElement("appliedEvents"); root.AppendChild(replay);
            foreach(Guid id in RebirthNpcSocialService.GetAppliedEventSnapshot()) { XmlElement e=doc.CreateElement("event"); e.SetAttribute("id",id.ToString("N")); replay.AppendChild(e); }
            XmlElement factions=doc.CreateElement("factions"); root.AppendChild(factions);
            foreach(RebirthNpcFactionStanding s in RebirthNpcFactionGameplayService.GetSnapshots())
            {
                XmlElement e=doc.CreateElement("standing"); e.SetAttribute("faction",s.FactionId??string.Empty); e.SetAttribute("counterparty",s.Counterparty??string.Empty);
                e.SetAttribute("value",F(s.Standing)); e.SetAttribute("revision",s.Revision.ToString(CultureInfo.InvariantCulture)); e.SetAttribute("changed",s.LastChangedUtcTicks.ToString(CultureInfo.InvariantCulture)); factions.AppendChild(e);
            }
            RebirthNpcPersistenceFile.SaveAtomic(path,doc); saves++; lastError=string.Empty;
        }
    }

    public static void Reset(bool clearLoaded){ lock(Sync){ if(clearLoaded) loaded=false; lastSource="none"; } }
    public static string GetReport(){lock(Sync)return "[REBIRTH NPC Social Persistence] loaded="+loaded+" loads="+loads+" saves="+saves+" source="+lastSource+" error="+(string.IsNullOrEmpty(lastError)?"none":lastError);}
    private static bool Validate(XmlDocument d)
    {
        if(d?.DocumentElement == null || d.DocumentElement.Name != "rebirthNpcSocialState") return false;
        string version = d.DocumentElement.GetAttribute("version");
        if(version != "1" && version != "2") return false;
        if(!ValidateIdentities(d.DocumentElement)) return false;
        // Validate numeric data before the loader chooses primary versus backup. Missing
        // legacy fields retain their established zero defaults; malformed values do not.
        foreach(XmlElement e in d.SelectNodes("/rebirthNpcSocialState/relationships/relationship"))
        {
            foreach(string name in new[]{"familiarity","trust","respect","fear","gratitude","suspicion","loyalty","hostility"}) PF(e,name);
            PU(e,"revision"); PL(e,"changed");
        }
        foreach(XmlElement e in d.SelectNodes("/rebirthNpcSocialState/memories/memory"))
        {
            PF(e,"importance"); PF(e,"valence"); PF(e,"confidence");
            PL(e,"created"); PL(e,"reinforced"); PL(e,"decayed");
        }
        foreach(XmlElement e in d.SelectNodes("/rebirthNpcSocialState/factions/standing"))
        { PF(e,"value"); PU(e,"revision"); PL(e,"changed"); }
        return true;
    }
    private static bool ValidateIdentities(XmlElement root)
    {
        var sections = new HashSet<string>(StringComparer.Ordinal);
        var relationships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var events = new HashSet<Guid>();
        var factions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(XmlNode node in root.ChildNodes)
        {
            var section = node as XmlElement;
            if(section == null) continue;
            if(!sections.Add(section.Name)) return false;
            string expected;
            switch(section.Name)
            {
                case "relationships": expected="relationship"; break;
                case "memories": expected="memory"; break;
                case "appliedEvents": expected="event"; break;
                case "factions": expected="standing"; break;
                default: return false;
            }
            foreach(XmlNode child in section.ChildNodes)
            {
                var e = child as XmlElement;
                if(e == null) continue;
                if(e.Name != expected) return false;
                Guid eventId; RebirthNpcStableId subject;
                switch(section.Name)
                {
                    case "relationships":
                        string key=e.GetAttribute("key");
                        if(string.IsNullOrWhiteSpace(key) || !relationships.Add(key)) return false;
                        break;
                    case "memories":
                        RebirthNpcSocialEventKind kind;
                        if(!Guid.TryParse(e.GetAttribute("event"),out eventId) || eventId==Guid.Empty
                            || !RebirthNpcStableId.TryParse(e.GetAttribute("subject"),out subject)
                            || !Enum.TryParse(e.GetAttribute("kind"),true,out kind)
                            || !Enum.IsDefined(typeof(RebirthNpcSocialEventKind),kind)) return false;
                        break;
                    case "appliedEvents":
                        if(!Guid.TryParse(e.GetAttribute("id"),out eventId) || eventId==Guid.Empty || !events.Add(eventId)) return false;
                        break;
                    case "factions":
                        string faction=e.GetAttribute("faction"), counterparty=e.GetAttribute("counterparty");
                        if(string.IsNullOrWhiteSpace(faction) || string.IsNullOrWhiteSpace(counterparty)
                            || !factions.Add(faction.Trim().ToLowerInvariant()+"|"+counterparty.Trim().ToLowerInvariant())) return false;
                        break;
                }
            }
        }
        return true;
    }
    private static string F(float v){return v.ToString("R",CultureInfo.InvariantCulture);}
    private static float PF(XmlElement e,string n)
    {
        if(!e.HasAttribute(n)) return 0f;
        float v;
        if(!float.TryParse(e.GetAttribute(n),NumberStyles.Float,CultureInfo.InvariantCulture,out v) || float.IsNaN(v) || float.IsInfinity(v))
            throw new InvalidDataException("Invalid social numeric field: " + n);
        return v;
    }
    private static long PL(XmlElement e,string n)
    {
        if(!e.HasAttribute(n)) return 0L;
        long v;
        if(!long.TryParse(e.GetAttribute(n),NumberStyles.Integer,CultureInfo.InvariantCulture,out v))
            throw new InvalidDataException("Invalid social integer field: " + n);
        return v;
    }
    private static uint PU(XmlElement e,string n)
    {
        if(!e.HasAttribute(n)) return 0U;
        uint v;
        if(!uint.TryParse(e.GetAttribute(n),NumberStyles.Integer,CultureInfo.InvariantCulture,out v))
            throw new InvalidDataException("Invalid social revision field: " + n);
        return v;
    }
    private static void Restore(XmlDocument doc)
    {
        List<RebirthNpcRelationshipSnapshot> rs=new List<RebirthNpcRelationshipSnapshot>();
        foreach(XmlElement e in doc.SelectNodes("/rebirthNpcSocialState/relationships/relationship")) rs.Add(new RebirthNpcRelationshipSnapshot{Key=e.GetAttribute("key"),Familiarity=PF(e,"familiarity"),Trust=PF(e,"trust"),Respect=PF(e,"respect"),Fear=PF(e,"fear"),Gratitude=PF(e,"gratitude"),Suspicion=PF(e,"suspicion"),Loyalty=PF(e,"loyalty"),Hostility=PF(e,"hostility"),Revision=PU(e,"revision"),LastChangedUtcTicks=PL(e,"changed")});
        List<RebirthNpcSocialMemorySnapshot> ms=new List<RebirthNpcSocialMemorySnapshot>();
        foreach(XmlElement e in doc.SelectNodes("/rebirthNpcSocialState/memories/memory")){Guid id;RebirthNpcStableId sid;RebirthNpcSocialEventKind kind;if(!Guid.TryParse(e.GetAttribute("event"),out id)||!RebirthNpcStableId.TryParse(e.GetAttribute("subject"),out sid)||!Enum.TryParse(e.GetAttribute("kind"),true,out kind))continue;ms.Add(new RebirthNpcSocialMemorySnapshot{EventId=id,Subject=sid,Counterparty=e.GetAttribute("counterparty"),Kind=kind,Importance=PF(e,"importance"),Valence=PF(e,"valence"),Confidence=PF(e,"confidence"),CreatedUtcTicks=PL(e,"created"),LastReinforcedUtcTicks=PL(e,"reinforced"),LastDecayUtcTicks=PL(e,"decayed")});}
        List<Guid> ids=new List<Guid>(); foreach(XmlElement e in doc.SelectNodes("/rebirthNpcSocialState/appliedEvents/event")){Guid id;if(Guid.TryParse(e.GetAttribute("id"),out id))ids.Add(id);}
        List<RebirthNpcFactionStanding> fs=new List<RebirthNpcFactionStanding>(); foreach(XmlElement e in doc.SelectNodes("/rebirthNpcSocialState/factions/standing"))fs.Add(new RebirthNpcFactionStanding{FactionId=e.GetAttribute("faction"),Counterparty=e.GetAttribute("counterparty"),Standing=PF(e,"value"),Revision=PU(e,"revision"),LastChangedUtcTicks=PL(e,"changed")});
        RebirthNpcSocialService.Restore(rs.ToArray(),ms.ToArray(),ids.ToArray()); RebirthNpcFactionGameplayService.Restore(fs.ToArray());
    }
}
