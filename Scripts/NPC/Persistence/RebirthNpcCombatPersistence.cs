using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

#nullable disable

public static partial class RebirthNpcCombatPersistenceStore
{
    private const int FormatVersion=1;
    private const string FileName="RebirthNpcCombatState.xml";
    private static readonly object Sync=new object();
    private static World boundWorld;
    private static string boundPath;
    private static RebirthNpcOriginalSaveBinding originalBinding;
    private static bool BoundCompatible=>boundWorld==null||(ReferenceEquals(boundWorld,GameManager.Instance?.World)&&boundPath==GetPath());
    private static void BindCurrent()
    {
        if(!BoundCompatible||GameManager.Instance?.World==null)throw new InvalidOperationException("NPC cache belongs to another retained original world/save");
        boundWorld=GameManager.Instance.World;boundPath=GetPath();originalBinding=RebirthNpcOriginalSaveBinding.Capture(boundWorld,boundPath,RebirthNpcCombatEmergencyService.RecoveryEpoch);
    }
    private static bool loaded;
    private static volatile bool dirty;
    private static long saves,restored,rejected;

    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        if(!IsServer())return;
        lock(Sync)
        {
            if(!BoundCompatible)throw new InvalidOperationException("Retained cache scope differs; original retirement required");
            if(loaded)return;string path=GetPath();if(string.IsNullOrEmpty(path))return;
            try
            {
                XmlDocument document;string source,error;
                if(!RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out document,out source,out error))
                {if(!RebirthNpcPersistenceFile.CanInitializeEmpty(path)){Log.Warning("[REBIRTH NPC] Failed to load combat state: "+error);return;}BindCurrent();loaded=true;return;}
                List<RebirthNpcCombatStateRecord> states=new List<RebirthNpcCombatStateRecord>();List<string> transactions=new List<string>();
                foreach(XmlNode node in document.DocumentElement.ChildNodes)
                {
                    XmlElement e=node as XmlElement;if(e==null)continue;
                    if(e.Name=="state")states.Add(ReadState(e));
                    else if(e.Name=="deathTransaction"){string id=e.GetAttribute("id");if(string.IsNullOrWhiteSpace(id))throw new InvalidDataException("Empty death transaction id.");transactions.Add(id);}
                    else throw new InvalidDataException("Unknown combat persistence element: "+e.Name);
                }
                if(!RebirthNpcCombatEmergencyService.TryPrepareRecovery(states.ToArray(),transactions.ToArray(),out var prepared,out var loadError)||!prepared.Commit())throw new InvalidDataException("Combat generation refused: "+loadError);
                restored=states.Count+transactions.Count;
                RebirthNpcPersistenceSchemaTelemetry.RecordLoad("combat",path,FormatVersion,FormatVersion,source);BindCurrent();loaded=true;
            }
            catch(Exception ex){rejected++;RebirthNpcPersistenceFile.BlockWrite(path,ex.GetType().Name+": "+ex.Message);RebirthNpcPersistenceSchemaTelemetry.RecordRejected("combat",path,FormatVersion,ex.GetType().Name+": "+ex.Message);Log.Warning("[REBIRTH NPC] Failed to load combat state: "+ex.GetType().Name+": "+ex.Message);}
        }
    }

    public static void MarkDirty(){if(IsServer()){dirty=true;if(!ThreadManager.IsMainThread()){originalBinding?.InvalidateMutation();return;}lock(Sync)originalBinding?.ObserveMutation(RebirthNpcCombatEmergencyService.RecoveryEpoch);}}
    public static void Save()
    {
        if(!IsServer())return;EnsureLoaded();lock(Sync){if(!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite)return;string path=GetPath();RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded,path);if(string.IsNullOrEmpty(path))return;
        using var recoveryWriter=RebirthNpcPersistenceFile.BeginWrite(path);
        RebirthNpcCombatEmergencyService.CaptureRetirement(out var states,out var transactions,out var epoch,out _);var d=SnapshotDocument(states,transactions);
        if(!ReferenceEquals(epoch,RebirthNpcCombatEmergencyService.RecoveryEpoch))throw new InvalidOperationException("Combat snapshot changed");
        RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot("combat",path,FormatVersion);RebirthNpcPersistenceFile.SaveAtomic(path,d);dirty=false;saves++;originalBinding=RebirthNpcOriginalSaveBinding.Capture(boundWorld,boundPath,epoch);}
    }

    internal static bool CanRetireBacking
    {
        get{lock(Sync)try
        {
            if(dirty)return false;
            RebirthNpcCombatEmergencyService.CaptureRetirement(out var states,out var transactions,out var epoch,out var orders);if(orders)return false;
            if(boundWorld==null)return !loaded&&states.Length==0&&transactions.Length==0;
            if(string.IsNullOrEmpty(boundPath)||!loaded||RebirthNpcPersistenceFile.IsRecoveryFenced(boundPath))return false;
            if(!File.Exists(boundPath))return states.Length==0&&transactions.Length==0&&RebirthNpcPersistenceFile.CanInitializeEmpty(boundPath);
            var primary=RebirthNpcRecoveryCurrentBacking.Read(boundPath);if(!ValidateDocument(primary))return false;
            var savedStates=new List<RebirthNpcCombatStateRecord>();var savedTransactions=new List<string>();
            foreach(XmlElement row in primary.DocumentElement.ChildNodes){if(row.Name=="state")savedStates.Add(ReadState(row));else savedTransactions.Add(row.GetAttribute("id"));}
            return SnapshotDocument(savedStates.ToArray(),savedTransactions.ToArray()).OuterXml==SnapshotDocument(states,transactions).OuterXml&&ReferenceEquals(epoch,RebirthNpcCombatEmergencyService.RecoveryEpoch);
        }catch{return false;}}
    }
    internal static bool TryFlushOriginalBacking()
    {
        lock(Sync)try
        {
            if(BoundCompatible&&boundWorld!=null){Save();return CanRetireBacking;}
            if(!dirty)return CanRetireBacking;
            if(!loaded||originalBinding==null)return false;
            if(!originalBinding.TryPublish(()=>{RebirthNpcCombatEmergencyService.CaptureRetirement(out var rows,out var transactions,out _,out _);return SnapshotDocument(rows,transactions);},ValidateDocument,()=>RebirthNpcCombatEmergencyService.RecoveryEpoch,out var error))return false;
            dirty=false;saves++;return CanRetireBacking;
        }catch(Exception error){Log.Warning("[REBIRTH NPC] Original combat flush retained: "+error.GetType().Name);return false;}
    }
    private static XmlDocument SnapshotDocument(RebirthNpcCombatStateRecord[] states,string[] transactions)
    {
        var rows=(RebirthNpcCombatStateRecord[])states.Clone();Array.Sort(rows,(a,b)=>string.CompareOrdinal(a.NpcId.ToString(),b.NpcId.ToString()));
        var ids=(string[])transactions.Clone();Array.Sort(ids,StringComparer.Ordinal);
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("rebirthNpcCombatState");root.SetAttribute("format",FormatVersion.ToString(CultureInfo.InvariantCulture));doc.AppendChild(root);
        foreach(var state in rows)root.AppendChild(WriteState(doc,state));foreach(string id in ids){var element=doc.CreateElement("deathTransaction");element.SetAttribute("id",id);root.AppendChild(element);}return doc;
    }
    public static void Reset(bool save){if(!TryReset(save))throw new InvalidOperationException("Combat original state retained; retirement proof unavailable");}
    public static bool TryReset(bool save){if(save&&!TryFlushOriginalBacking())return false;lock(Sync){if(!CanRetireBacking)return false;RebirthNpcCombatEmergencyService.ResetForWorldChange();loaded=false;boundWorld=null;boundPath=null;originalBinding=null;dirty=false;return true;}}
    public static string GetReport(){EnsureLoaded();return "[REBIRTH NPC Combat Persistence] loaded="+loaded+" dirty="+dirty+" restored="+restored+" rejected="+rejected+" saves="+saves+" file="+GetPath();}

    private static XmlElement WriteState(XmlDocument d,RebirthNpcCombatStateRecord s){XmlElement e=d.CreateElement("state");e.SetAttribute("npc",s.NpcId.ToString());e.SetAttribute("life",((int)s.LifeState).ToString(CultureInfo.InvariantCulture));e.SetAttribute("severity",((int)s.InjurySeverity).ToString(CultureInfo.InvariantCulture));e.SetAttribute("injury",s.InjuryPoints.ToString(CultureInfo.InvariantCulture));e.SetAttribute("bleed",s.BleedPoints.ToString(CultureInfo.InvariantCulture));e.SetAttribute("incapacitated",s.IncapacitatedUtcTicks.ToString(CultureInfo.InvariantCulture));e.SetAttribute("death",s.DeathFinalizedUtcTicks.ToString(CultureInfo.InvariantCulture));e.SetAttribute("settlement",s.SettlementId??string.Empty);e.SetAttribute("cause",s.LastCause??string.Empty);e.SetAttribute("revision",s.Revision.ToString(CultureInfo.InvariantCulture));return e;}
    private static RebirthNpcCombatStateRecord ReadState(XmlElement e){RebirthNpcStableId id;int life,severity,injury,bleed;long incapacitated,death;uint revision;if(!RebirthNpcStableId.TryParse(e.GetAttribute("npc"),out id)||!int.TryParse(e.GetAttribute("life"),out life)||!int.TryParse(e.GetAttribute("severity"),out severity)||!int.TryParse(e.GetAttribute("injury"),out injury)||!int.TryParse(e.GetAttribute("bleed"),out bleed)||!long.TryParse(e.GetAttribute("incapacitated"),out incapacitated)||!long.TryParse(e.GetAttribute("death"),out death)||!uint.TryParse(e.GetAttribute("revision"),out revision))throw new InvalidDataException("Invalid combat state record.");if(life<0||life>4||severity<0||severity>4||injury<0||bleed<0)throw new InvalidDataException("Combat state values are out of range.");return new RebirthNpcCombatStateRecord{NpcId=id,LifeState=(RebirthNpcCombatLifeState)life,InjurySeverity=(RebirthNpcInjurySeverity)severity,InjuryPoints=injury,BleedPoints=bleed,IncapacitatedUtcTicks=incapacitated,DeathFinalizedUtcTicks=death,SettlementId=e.GetAttribute("settlement"),LastCause=e.GetAttribute("cause"),Revision=revision};}
    internal static bool TryPrepareRecovery(XmlDocument document,out RebirthNpcPreparedRecovery candidate,out RebirthNpcStableId[] identities,out string error)
    {
        candidate=null;identities=null;error=string.Empty;if(!BoundCompatible){error="recovery cannot replace another original world cache";return false;}if(!ValidateDocument(document)){error="combat semantic validation failed";return false;}
        var records=new List<RebirthNpcCombatStateRecord>();var transactions=new List<string>();
        foreach(XmlNode node in document.DocumentElement.ChildNodes){var row=node as XmlElement;if(row==null)continue;if(row.Name=="state")records.Add(ReadState(row));else transactions.Add(row.GetAttribute("id"));}
        if(!RebirthNpcCombatEmergencyService.TryPrepareRecovery(records.ToArray(),transactions.ToArray(),out var prepared,out error))return false;
        var ids=new List<RebirthNpcStableId>();foreach(var row in records)ids.Add(row.NpcId);identities=ids.ToArray();
        candidate=new RebirthNpcPreparedRecovery(FileName,()=>BoundCompatible&&prepared.IsCurrent,()=>{lock(Sync){if(!BoundCompatible||!prepared.Commit())return false;BindCurrent();loaded=true;dirty=false;restored=records.Count+transactions.Count;return true;}});return true;
    }    internal static bool ValidateRecoveryDocument(XmlDocument document)=>ValidateDocument(document);
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
