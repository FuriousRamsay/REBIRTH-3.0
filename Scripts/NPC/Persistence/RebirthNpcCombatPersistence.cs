using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

#nullable disable

public static class RebirthNpcCombatPersistenceStore
{
    private const int FormatVersion=1;
    private const string FileName="RebirthNpcCombatState.xml";
    private static readonly object Sync=new object();
    private static bool loaded;
    private static volatile bool dirty;
    private static long saves,restored,rejected;

    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        if(!IsServer())return;
        lock(Sync)
        {
            if(loaded)return;string path=GetPath();if(string.IsNullOrEmpty(path))return;
            try
            {
                XmlDocument document;string source,error;
                if(!RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out document,out source,out error))
                {if(!RebirthNpcPersistenceFile.CanInitializeEmpty(path)){Log.Warning("[REBIRTH NPC] Failed to load combat state: "+error);return;}loaded=true;return;}
                List<RebirthNpcCombatStateRecord> states=new List<RebirthNpcCombatStateRecord>();List<string> transactions=new List<string>();
                foreach(XmlNode node in document.DocumentElement.ChildNodes)
                {
                    XmlElement e=node as XmlElement;if(e==null)continue;
                    if(e.Name=="state")states.Add(ReadState(e));
                    else if(e.Name=="deathTransaction"){string id=e.GetAttribute("id");if(string.IsNullOrWhiteSpace(id))throw new InvalidDataException("Empty death transaction id.");transactions.Add(id);}
                    else throw new InvalidDataException("Unknown combat persistence element: "+e.Name);
                }
                RebirthNpcCombatEmergencyService.Restore(states.ToArray(),transactions.ToArray());restored=states.Count+transactions.Count;
                RebirthNpcPersistenceSchemaTelemetry.RecordLoad("combat",path,FormatVersion,FormatVersion,source);loaded=true;
            }
            catch(Exception ex){RebirthNpcCombatEmergencyService.ResetForWorldChange();rejected++;RebirthNpcPersistenceSchemaTelemetry.RecordRejected("combat",path,FormatVersion,ex.GetType().Name+": "+ex.Message);Log.Warning("[REBIRTH NPC] Failed to load combat state: "+ex.GetType().Name+": "+ex.Message);}
        }
    }

    public static void MarkDirty(){if(IsServer())dirty=true;}
    public static void Save()
    {
        if(!IsServer())return;EnsureLoaded();lock(Sync){if(!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite)return;string path=GetPath();RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded,path);if(string.IsNullOrEmpty(path))return;
        XmlDocument d=new XmlDocument();XmlElement root=d.CreateElement("rebirthNpcCombatState");root.SetAttribute("format",FormatVersion.ToString(CultureInfo.InvariantCulture));d.AppendChild(root);
        foreach(RebirthNpcCombatStateRecord s in RebirthNpcCombatEmergencyService.CaptureStates())root.AppendChild(WriteState(d,s));
        foreach(string transaction in RebirthNpcCombatEmergencyService.CaptureFinalizedTransactions()){XmlElement e=d.CreateElement("deathTransaction");e.SetAttribute("id",transaction);root.AppendChild(e);}
        RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot("combat",path,FormatVersion);RebirthNpcPersistenceFile.SaveAtomic(path,d);dirty=false;saves++;}
    }

    public static void Reset(bool save){if(save)Save();lock(Sync){RebirthNpcCombatEmergencyService.ResetForWorldChange();loaded=false;dirty=false;}}
    public static string GetReport(){EnsureLoaded();return "[REBIRTH NPC Combat Persistence] loaded="+loaded+" dirty="+dirty+" restored="+restored+" rejected="+rejected+" saves="+saves+" file="+GetPath();}

    private static XmlElement WriteState(XmlDocument d,RebirthNpcCombatStateRecord s){XmlElement e=d.CreateElement("state");e.SetAttribute("npc",s.NpcId.ToString());e.SetAttribute("life",((int)s.LifeState).ToString(CultureInfo.InvariantCulture));e.SetAttribute("severity",((int)s.InjurySeverity).ToString(CultureInfo.InvariantCulture));e.SetAttribute("injury",s.InjuryPoints.ToString(CultureInfo.InvariantCulture));e.SetAttribute("bleed",s.BleedPoints.ToString(CultureInfo.InvariantCulture));e.SetAttribute("incapacitated",s.IncapacitatedUtcTicks.ToString(CultureInfo.InvariantCulture));e.SetAttribute("death",s.DeathFinalizedUtcTicks.ToString(CultureInfo.InvariantCulture));e.SetAttribute("settlement",s.SettlementId??string.Empty);e.SetAttribute("cause",s.LastCause??string.Empty);e.SetAttribute("revision",s.Revision.ToString(CultureInfo.InvariantCulture));return e;}
    private static RebirthNpcCombatStateRecord ReadState(XmlElement e){RebirthNpcStableId id;int life,severity,injury,bleed;long incapacitated,death;uint revision;if(!RebirthNpcStableId.TryParse(e.GetAttribute("npc"),out id)||!int.TryParse(e.GetAttribute("life"),out life)||!int.TryParse(e.GetAttribute("severity"),out severity)||!int.TryParse(e.GetAttribute("injury"),out injury)||!int.TryParse(e.GetAttribute("bleed"),out bleed)||!long.TryParse(e.GetAttribute("incapacitated"),out incapacitated)||!long.TryParse(e.GetAttribute("death"),out death)||!uint.TryParse(e.GetAttribute("revision"),out revision))throw new InvalidDataException("Invalid combat state record.");if(life<0||life>4||severity<0||severity>4||injury<0||bleed<0)throw new InvalidDataException("Combat state values are out of range.");return new RebirthNpcCombatStateRecord{NpcId=id,LifeState=(RebirthNpcCombatLifeState)life,InjurySeverity=(RebirthNpcInjurySeverity)severity,InjuryPoints=injury,BleedPoints=bleed,IncapacitatedUtcTicks=incapacitated,DeathFinalizedUtcTicks=death,SettlementId=e.GetAttribute("settlement"),LastCause=e.GetAttribute("cause"),Revision=revision==0?1U:revision};}
    private static bool ValidateDocument(XmlDocument d)
    {
        XmlElement root=d?.DocumentElement;int format;
        if(root==null||root.Name!="rebirthNpcCombatState"||!int.TryParse(root.GetAttribute("format"),out format)||format!=FormatVersion)return false;
        var identities=new HashSet<RebirthNpcStableId>();
        var transactions=new HashSet<string>(StringComparer.Ordinal);
        foreach(XmlNode node in root.ChildNodes)
        {
            var element=node as XmlElement;if(element==null)continue;
            if(element.Name=="state")
            {
                // ReadState is side-effect free; invalid records must reject the primary before backup selection.
                var state=ReadState(element);
                if(state.NpcId.IsEmpty||!identities.Add(state.NpcId))return false;
            }
            else if(element.Name=="deathTransaction")
            {
                string id=element.GetAttribute("id");
                if(string.IsNullOrWhiteSpace(id)||!transactions.Add(id))return false;
            }
            else return false;
        }
        return true;
    }
    private static string GetPath(){string dir=GameIO.GetSaveGameDir();return string.IsNullOrEmpty(dir)?string.Empty:Path.Combine(dir,FileName);}
    private static bool IsServer(){return SingletonMonoBehaviour<ConnectionManager>.Instance!=null&&SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;}
}
