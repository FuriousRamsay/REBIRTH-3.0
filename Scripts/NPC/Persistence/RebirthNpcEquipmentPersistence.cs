using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthNpcEquipmentPersistenceStore
{
    private const int FormatVersion = 1;
    private const string FileName = "rebirth_npc_equipment.xml";
    private static readonly object Sync = new object();
    private static bool loaded; private static volatile bool dirty; private static long nextSaveUtcTicks;
    private static long saves, restored, rejected;

    public static void Tick()
    {
        if (!IsServer()) return; EnsureLoaded(); long now = DateTime.UtcNow.Ticks;
        if (now < nextSaveUtcTicks) return; nextSaveUtcTicks = now + TimeSpan.FromSeconds(30).Ticks; Save();
    }
    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        if (!IsServer()) return;
        lock (Sync)
        {
            if (loaded) return; string path = GetPath(); if (string.IsNullOrEmpty(path)) return;
            try
            {
                XmlDocument doc; string source, error;
                if (!RebirthNpcPersistenceFile.TryLoad(path, Validate, out doc, out source, out error))
                { if (!RebirthNpcPersistenceFile.CanInitializeEmpty(path)) { Log.Warning("[REBIRTH NPC] Failed to load equipment state: " + error); return; } loaded = true; return; }
                RebirthNpcPersistenceSchemaTelemetry.RecordLoad("equipment", path, FormatVersion, FormatVersion, source);
                foreach (XmlNode node in doc.DocumentElement.SelectNodes("loadout"))
                {
                    var record = Read(node as XmlElement); string recordError = "Record could not be parsed.";
                    if (record == null || !RebirthNpcEquipmentService.TryRestorePersistentRecord(record, out recordError))
                    { rejected++; throw new InvalidDataException("Invalid equipment record: " + recordError); }
                    restored++;
                }
                dirty = false;
                loaded = true;
            }
            catch (Exception ex)
            {
                RebirthNpcEquipmentService.ClearRuntimeState();
                RebirthNpcPersistenceSchemaTelemetry.RecordRejected("equipment", path, FormatVersion, ex.GetType().Name + ": " + ex.Message);
                Log.Warning("[REBIRTH NPC] Failed to load equipment state: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
    public static void MarkDirty() { if (IsServer()) dirty = true; }
    public static void Save()
    {
        if (!IsServer()) return; EnsureLoaded(); lock (Sync)
        {
            if (!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite) return; string path = GetPath(); RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded, path); if (string.IsNullOrEmpty(path)) return;
            try
            {
                var doc = new XmlDocument(); var root = doc.CreateElement("rebirthNpcEquipmentState");
                root.SetAttribute("format", FormatVersion.ToString(CultureInfo.InvariantCulture)); doc.AppendChild(root);
                foreach (var record in RebirthNpcEquipmentService.CapturePersistentRecords()) root.AppendChild(Write(doc, record));
                RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot("equipment", path, FormatVersion);
                RebirthNpcPersistenceFile.SaveAtomic(path, doc); dirty = false; saves++;
            }
            catch (Exception ex) { Log.Warning("[REBIRTH NPC] Failed to save equipment state: " + ex.GetType().Name + ": " + ex.Message); throw; }
        }
    }
    public static void Reset(bool save)
    { if (save) Save(); lock (Sync) { RebirthNpcEquipmentService.ClearRuntimeState(); loaded = false; dirty = false; nextSaveUtcTicks = 0; } }
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
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) => RebirthNpcEquipmentPersistenceStore.Reset(true);
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) => RebirthNpcEquipmentPersistenceStore.Reset(true);
}
