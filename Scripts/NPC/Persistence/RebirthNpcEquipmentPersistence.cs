using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine.Scripting;

#nullable disable

public static partial class RebirthNpcEquipmentPersistenceStore
{
    private const int FormatVersion = 1;
    private const string FileName = "rebirth_npc_equipment.xml";
    private static readonly object Sync = new object();
    private static bool loaded; private static volatile bool dirty; private static long nextSaveUtcTicks;
    private static World boundWorld;private static string boundPath;
    private static RebirthNpcOriginalSaveBinding originalBinding;
    private static bool BoundCurrent=>boundWorld!=null&&ReferenceEquals(boundWorld,GameManager.Instance?.World)&&boundPath==GetPath();
    private static bool BoundCompatible=>boundWorld==null||BoundCurrent;
    private static void BindCurrent(){if(!BoundCompatible)throw new InvalidOperationException("Equipment cache belongs to another original world/save.");boundWorld=GameManager.Instance.World;boundPath=GetPath();originalBinding=RebirthNpcOriginalSaveBinding.Capture(boundWorld,boundPath,RebirthNpcEquipmentService.RecoveryEpoch);}
    private static long saves, restored, rejected;

    public static void Tick()
    {
        if (!IsServer() || GameManager.Instance?.World == null) return;
        long now = DateTime.UtcNow.Ticks;
        if (now < nextSaveUtcTicks) return;
        if (string.IsNullOrEmpty(GetPath())) return;
        // Save ensures loading before its dirty check. Keep automatic failed-load
        // retries on the save cadence; explicit operations still load immediately.
        nextSaveUtcTicks = now + TimeSpan.FromSeconds(30).Ticks;
        Save();
    }
    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        if (!IsServer()) return;
        lock (Sync)
        {
            if(!BoundCompatible)throw new InvalidOperationException("Retained equipment cache must retire in its original scope before another world loads.");
            if (loaded) return; string path = GetPath(); if (string.IsNullOrEmpty(path)) return;
            try
            {
                XmlDocument doc; string source, error;
                if (!RebirthNpcPersistenceFile.TryLoad(path, Validate, out doc, out source, out error))
                { if (!RebirthNpcPersistenceFile.CanInitializeEmpty(path)) { Log.Warning("[REBIRTH NPC] Failed to load equipment state: " + error); return; } BindCurrent();loaded = true; return; }
                RebirthNpcPersistenceSchemaTelemetry.RecordLoad("equipment", path, FormatVersion, FormatVersion, source);
                var generation=new List<RebirthNpcEquipmentPersistentRecord>();
                foreach (XmlNode node in doc.DocumentElement.SelectNodes("loadout"))
                {
                    generation.Add(Read(node as XmlElement));
                }
                if(!RebirthNpcEquipmentService.TryReplacePersistentRecords(generation,out var recordError))
                {rejected++;throw new InvalidDataException("Invalid equipment generation: "+recordError);}
                restored+=generation.Count;
                dirty = false;
                BindCurrent();loaded = true;
            }
            catch (Exception ex)
            {
                RebirthNpcPersistenceSchemaTelemetry.RecordRejected("equipment", path, FormatVersion, ex.GetType().Name + ": " + ex.Message);
                Log.Warning("[REBIRTH NPC] Failed to load equipment state: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
    public static void MarkDirty() { if (IsServer()) { if(!ThreadManager.IsMainThread()){dirty=true;originalBinding?.InvalidateMutation();return;} lock(Sync){dirty=true;originalBinding?.ObserveMutation(RebirthNpcEquipmentService.RecoveryEpoch);} } }
    internal static bool IsRecoveryProjectionLoaded{get{lock(Sync)return loaded&&BoundCurrent;}}
    internal static bool TryFlushOriginalBacking()
    {
        if(!ThreadManager.IsMainThread()||!IsServer())return false;
        lock(Sync)try
        {
            if(BoundCurrent&&dirty){Save();return CanRetireBacking;}
            if(!dirty)return CanRetireBacking;
            if(!loaded||originalBinding==null)return false;
            var epoch=RebirthNpcEquipmentService.RecoveryEpoch;
            
            if(!ReferenceEquals(epoch,RebirthNpcEquipmentService.RecoveryEpoch))return false;
            if(!originalBinding.TryPublish(()=>{var doc=new XmlDocument{XmlResolver=null};doc.LoadXml(RecoveryBackingImage(RebirthNpcEquipmentService.CapturePersistentRecords()));return doc;},Validate,()=>RebirthNpcEquipmentService.RecoveryEpoch,out var error))
            {if(!string.IsNullOrEmpty(error))Log.Warning("[REBIRTH NPC] Original equipment flush retained: "+error);return false;}
            dirty=false;saves++;return CanRetireBacking;
        }catch(Exception error){Log.Warning("[REBIRTH NPC] Original equipment flush retained: "+error.GetType().Name);return false;}
    }
    internal static bool CanRetireBacking
    {
        get
        {
            lock(Sync)try
            {
                var epoch=RebirthNpcEquipmentService.RecoveryEpoch;
                var runtime=RebirthNpcEquipmentService.CapturePersistentRecords();if(dirty)return false;
                if(boundWorld==null)return !loaded&&!dirty&&ReferenceEquals(epoch,RebirthNpcEquipmentService.RecoveryEpoch)&&runtime.Length==0;
                if(string.IsNullOrEmpty(boundPath)||RebirthNpcPersistenceFile.IsRecoveryFenced(boundPath))return false;
                if(!File.Exists(boundPath))return !dirty&&ReferenceEquals(epoch,RebirthNpcEquipmentService.RecoveryEpoch)&&runtime.Length==0&&!File.Exists(boundPath+".bak")&&!RebirthNpcPersistenceFile.IsRecoveryFenced(boundPath);
                using(var input=new FileStream(boundPath,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(input.Length<1||input.Length>67108864)return false;
                    var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=67108864}))doc.Load(reader);
                    if(!Validate(doc))return false;var saved=new List<RebirthNpcEquipmentPersistentRecord>();foreach(XmlElement row in doc.DocumentElement.SelectNodes("loadout"))saved.Add(Read(row));
                    return !dirty&&ReferenceEquals(epoch,RebirthNpcEquipmentService.RecoveryEpoch)&&RecoveryBackingImage(runtime)==RecoveryBackingImage(saved.ToArray());
                }
            }
            catch{return false;}
        }
    }
    private static string RecoveryBackingImage(RebirthNpcEquipmentPersistentRecord[] records)
    {
        var rows=new List<RebirthNpcEquipmentPersistentRecord>(records);rows.Sort((a,b)=>string.CompareOrdinal(a.NpcId.ToString(),b.NpcId.ToString()));
        var doc=new XmlDocument();var root=doc.CreateElement("rebirthNpcEquipmentState");root.SetAttribute("format",FormatVersion.ToString(CultureInfo.InvariantCulture));doc.AppendChild(root);
        foreach(var row in rows)root.AppendChild(Write(doc,row));return root.OuterXml;
    }
    public static void Save()
    {
        if (!IsServer()) return; EnsureLoaded(); lock (Sync)
        {
            if (!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite) return; string path = GetPath(); RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded, path); if (string.IsNullOrEmpty(path)) return;
            try
            {
                using var recoveryWriter = RebirthNpcPersistenceFile.BeginWrite(path);
                var doc = new XmlDocument(); var root = doc.CreateElement("rebirthNpcEquipmentState");
                root.SetAttribute("format", FormatVersion.ToString(CultureInfo.InvariantCulture)); doc.AppendChild(root);
                foreach (var record in RebirthNpcEquipmentService.CapturePersistentRecords()) root.AppendChild(Write(doc, record));
                RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot("equipment", path, FormatVersion);
                RebirthNpcPersistenceFile.SaveAtomic(path, doc); dirty = false; originalBinding=RebirthNpcOriginalSaveBinding.Capture(boundWorld,boundPath,RebirthNpcEquipmentService.RecoveryEpoch); saves++;
            }
            catch (Exception ex) { Log.Warning("[REBIRTH NPC] Failed to save equipment state: " + ex.GetType().Name + ": " + ex.Message); throw; }
        }
    }
    internal static bool TryPrepareRecovery(XmlDocument document,out RebirthNpcPreparedRecovery candidate,out RebirthNpcEquipmentPersistentRecord[] records,out string error)
    {
        candidate=null;records=null;error=string.Empty;
        var recoveryWorld=GameManager.Instance?.World;string recoveryPath=GetPath();
        if(!ThreadManager.IsMainThread()||recoveryWorld?.IsRemote()!=false||string.IsNullOrEmpty(recoveryPath)){error="authoritative recovery store scope unavailable";return false;}
        lock(Sync)if(!BoundCompatible){error="equipment recovery would replace another original world cache";return false;}
        if(!Validate(document)){error="equipment semantic validation failed";return false;}
        var detached=new List<RebirthNpcEquipmentPersistentRecord>();
        foreach(XmlNode node in document.DocumentElement.SelectNodes("loadout"))detached.Add(Read(node as XmlElement));
        foreach(var row in detached)foreach(var slot in row.Slots)if(slot.Value.Length>RebirthNpcNetworkFraming.MaxId||slot.Value!=slot.Value.Trim()){error="equipment key exceeds lossless canonical projection codec";return false;}
        if(!RebirthNpcEquipmentService.TryPreparePersistentRecords(detached,out var generation,out error))return false;
        records=detached.ToArray();
        candidate=new RebirthNpcPreparedRecovery(FileName,()=>ReferenceEquals(GameManager.Instance?.World,recoveryWorld)&&GetPath()==recoveryPath&&generation.IsCurrent&&BoundCompatible,()=>
        {
            lock(Sync)
            {
                if(!ReferenceEquals(GameManager.Instance?.World,recoveryWorld)||GetPath()!=recoveryPath||!BoundCompatible||!generation.Commit())return false;
                BindCurrent();loaded=true;dirty=false;nextSaveUtcTicks=0;restored+=detached.Count;return true;
            }
        });return true;
    }
    public static void Reset(bool save)
    {if(!TryReset(save))throw new InvalidOperationException("Equipment retirement refused; original state retained");}
    public static bool TryReset(bool save)
    {
        if(save)try{Save();}catch{return false;}
        lock(Sync){if(!CanRetireBacking)return false;RebirthNpcEquipmentService.ClearRuntimeState();loaded=false;boundWorld=null;boundPath=null;originalBinding=null;dirty=false;nextSaveUtcTicks=0;return true;}
    }
    public static string GetReport()
    { EnsureLoaded(); lock (Sync) return "[REBIRTH NPC Equipment Persistence] loaded=" + loaded + " dirty=" + dirty + " restored=" + restored + " rejected=" + rejected + " saves=" + saves + " file=" + GetPath(); }

    private static XmlElement Write(XmlDocument doc, RebirthNpcEquipmentPersistentRecord record)
    {
        var e = doc.CreateElement("loadout"); e.SetAttribute("stableId", record.NpcId.ToString()); e.SetAttribute("revision", record.Revision.ToString(CultureInfo.InvariantCulture));
        var slots = new List<KeyValuePair<RebirthNpcEquipmentSlot,string>>(record.Slots); slots.Sort((a,b) => a.Key.CompareTo(b.Key));
        foreach (var pair in slots) { var s = doc.CreateElement("slot"); s.SetAttribute("id", ((byte)pair.Key).ToString(CultureInfo.InvariantCulture)); s.SetAttribute("item", pair.Value); e.AppendChild(s); }
        foreach (Guid id in record.ReplayJournal) { var r = doc.CreateElement("replay"); r.SetAttribute("id", id.ToString("N")); e.AppendChild(r); }
        return e;
    }
    private static RebirthNpcEquipmentPersistentRecord Read(XmlElement e)
    {
        if (e == null) return null; RebirthNpcStableId id; uint revision;
        if (!RebirthNpcStableId.TryParse(e.GetAttribute("stableId"), out id) || !uint.TryParse(e.GetAttribute("revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out revision)) return null;
        var slots = new Dictionary<RebirthNpcEquipmentSlot,string>(); var replay = new List<Guid>();
        foreach (XmlNode node in e.ChildNodes)
        {
            var x = node as XmlElement; if (x == null) continue;
            if (x.Name == "slot") { byte raw; string item = (x.GetAttribute("item") ?? string.Empty).Trim(); if (!byte.TryParse(x.GetAttribute("id"), out raw) || !Enum.IsDefined(typeof(RebirthNpcEquipmentSlot), raw) || item.Length == 0) return null; var slot=(RebirthNpcEquipmentSlot)raw; if (slots.ContainsKey(slot)) return null; slots.Add(slot,item); }
            else if (x.Name == "replay") { Guid g; if (!Guid.TryParseExact(x.GetAttribute("id"), "N", out g) || g == Guid.Empty) return null; replay.Add(g); }
            else return null;
        }
        return new RebirthNpcEquipmentPersistentRecord { NpcId=id, Revision=revision, Slots=slots, ReplayJournal=replay.ToArray() };
    }
    internal static bool ValidateRecoveryDocument(XmlDocument document)=>Validate(document);
    private static bool Validate(XmlDocument doc)
    {
        int format;var root=doc?.DocumentElement;
        if(root==null||root.Name!="rebirthNpcEquipmentState"||!int.TryParse(root.GetAttribute("format"),out format)||format!=FormatVersion)return false;
        var identities=new HashSet<RebirthNpcStableId>();
        foreach(XmlNode node in root.ChildNodes)
        {
            var element=node as XmlElement;if(element==null)continue;
            if(element.Name!="loadout")return false;
            var record=Read(element);string error;
            if(!RebirthNpcEquipmentService.ValidatePersistentRecord(record,out error)||!identities.Add(record.NpcId))return false;
        }
        return true;
    }
    private static string GetPath() { string d=GameIO.GetSaveGameDir(); return string.IsNullOrEmpty(d)?string.Empty:Path.Combine(d,FileName); }
    private static bool IsServer() { return SingletonMonoBehaviour<ConnectionManager>.Instance != null && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer; }
}

[Preserve]
public sealed class RebirthNpcEquipmentPersistenceModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        // Coordinated save/reset is owned by RebirthNpcLifecycle.
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) => RebirthNpcLifecycle.RequestCoordinatedShutdown(false);
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) => RebirthNpcLifecycle.RequestCoordinatedShutdown(true);
}
