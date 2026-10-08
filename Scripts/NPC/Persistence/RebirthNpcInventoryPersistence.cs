using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthNpcInventoryPersistenceStore
{
    private const int FormatVersion = 1;
    private const string FileName = "rebirth_npc_inventory.xml";
    private static readonly object Sync = new object();
    private static bool loaded;
    private static volatile bool dirty;
    private static long nextSaveUtcTicks;
    private static long saves;
    private static long restored;
    private static long rejected;

    public static void Tick()
    {
        if (!IsServer()) return;
        EnsureLoaded();
        long now = DateTime.UtcNow.Ticks;
        if (now < nextSaveUtcTicks) return;
        nextSaveUtcTicks = now + TimeSpan.FromSeconds(30).Ticks;
        Save();
    }

    public static void EnsureLoaded()
    {
        if (GameManager.Instance?.World == null) return;
        if (!IsServer()) return;
        lock (Sync)
        {
            if (loaded) return;
            string path = GetPath();
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                XmlDocument document; string source, loadError;
                if (!RebirthNpcPersistenceFile.TryLoad(path, ValidateDocument, out document, out source, out loadError))
                {
                    if (!RebirthNpcPersistenceFile.CanInitializeEmpty(path))
                    {
                        Log.Warning("[REBIRTH NPC] Failed to load inventory state: " + loadError);
                        return;
                    }
                    loaded = true;
                    return;
                }
                RebirthNpcPersistenceSchemaTelemetry.RecordLoad("inventory", path, FormatVersion, FormatVersion, source);
                XmlElement root = document.DocumentElement;
                foreach (XmlNode node in root.SelectNodes("inventory"))
                {
                    RebirthNpcInventoryPersistentRecord record = Read(node as XmlElement);
                    string error = "Record could not be parsed.";
                    if (record == null || !RebirthNpcInventoryTransactionService.TryRestorePersistentRecord(record, out error))
                    {
                        rejected++;
                        throw new InvalidDataException("Invalid inventory record: " + error);
                    }
                    restored++;
                }
                dirty = false;
                loaded = true;
            }
            catch (Exception ex)
            {
                RebirthNpcInventoryTransactionService.ClearRuntimeState();
                RebirthNpcPersistenceSchemaTelemetry.RecordRejected("inventory", path, FormatVersion,
                    ex.GetType().Name + ": " + ex.Message);
                Log.Warning("[REBIRTH NPC] Failed to load inventory state: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    public static void MarkDirty()
    {
        if (IsServer()) dirty = true;
    }

    public static void Save()
    {
        if (!IsServer()) return;
        EnsureLoaded();
        lock (Sync)
        {
            if (!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite) return;
            string path = GetPath(); RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded, path);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                RebirthNpcInventoryPersistentRecord[] records =
                    RebirthNpcInventoryTransactionService.CapturePersistentRecords();
                XmlDocument document = new XmlDocument();
                XmlElement root = document.CreateElement("rebirthNpcInventoryState");
                root.SetAttribute("format", FormatVersion.ToString(CultureInfo.InvariantCulture));
                document.AppendChild(root);
                for (int i = 0; i < records.Length; i++) root.AppendChild(Write(document, records[i]));
                RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot("inventory", path, FormatVersion);
                RebirthNpcPersistenceFile.SaveAtomic(path, document);
                dirty = false;
                saves++;
            }
            catch (Exception ex)
            {
                Log.Warning("[REBIRTH NPC] Failed to save inventory state: " + ex.GetType().Name + ": " + ex.Message);
                throw;
            }
        }
    }

    public static void Reset(bool save)
    {
        if (save) Save();
        lock (Sync)
        {
            RebirthNpcInventoryTransactionService.ClearRuntimeState();
            RebirthNpcInventoryAuthorityService.Reset();
            loaded = false;
            dirty = false;
            nextSaveUtcTicks = 0L;
        }
    }

    public static string GetReport()
    {
        EnsureLoaded();
        lock (Sync)
            return "[REBIRTH NPC Inventory Persistence] loaded=" + loaded + " dirty=" + dirty +
                " restored=" + restored + " rejected=" + rejected + " saves=" + saves + " file=" + GetPath();
    }

    private static XmlElement Write(XmlDocument document, RebirthNpcInventoryPersistentRecord record)
    {
        XmlElement inventory = document.CreateElement("inventory");
        inventory.SetAttribute("stableId", record.NpcId.ToString());
        inventory.SetAttribute("revision", record.Revision.ToString(CultureInfo.InvariantCulture));
        foreach (KeyValuePair<string, int> pair in Sorted(record.Quantities))
        {
            XmlElement item = document.CreateElement("item");
            item.SetAttribute("key", pair.Key);
            item.SetAttribute("quantity", pair.Value.ToString(CultureInfo.InvariantCulture));
            inventory.AppendChild(item);
        }
        foreach (KeyValuePair<string, int> pair in Sorted(record.Reservations))
        {
            XmlElement reservation = document.CreateElement("reservation");
            reservation.SetAttribute("key", pair.Key);
            reservation.SetAttribute("quantity", pair.Value.ToString(CultureInfo.InvariantCulture));
            inventory.AppendChild(reservation);
        }
        for (int i = 0; i < record.ReplayJournal.Length; i++)
        {
            XmlElement replay = document.CreateElement("replay");
            replay.SetAttribute("id", record.ReplayJournal[i].ToString("N"));
            inventory.AppendChild(replay);
        }
        if(record.NativeStacks!=null){using(var reader=record.NativeStacks.Write().CreateReader())inventory.AppendChild(document.ReadNode(reader));}
        return inventory;
    }

    private static RebirthNpcInventoryPersistentRecord Read(XmlElement inventory)
    {
        if (inventory == null) return null;
        RebirthNpcStableId npcId; uint revision;
        if (!RebirthNpcStableId.TryParse(inventory.GetAttribute("stableId"), out npcId) ||
            !uint.TryParse(inventory.GetAttribute("revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out revision))
            return null;
        Dictionary<string, int> quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> reservations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<Guid> replay = new List<Guid>();
        RebirthNpcNativeStackSet nativeStacks=null;
        foreach (XmlNode node in inventory.ChildNodes)
        {
            XmlElement element = node as XmlElement;
            if (element == null) continue;
            if (element.Name == "item" || element.Name == "reservation")
            {
                string key = (element.GetAttribute("key") ?? string.Empty).Trim(); int quantity;
                if (key.Length == 0 || !int.TryParse(element.GetAttribute("quantity"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out quantity) || quantity <= 0) return null;
                Dictionary<string, int> target = element.Name == "item" ? quantities : reservations;
                if (target.ContainsKey(key)) return null;
                target.Add(key, quantity);
            }
            else if(element.Name=="nativeStacks")
            {
                if(nativeStacks!=null||!RebirthNpcNativeStackSet.TryRead(System.Xml.Linq.XElement.Parse(element.OuterXml),out nativeStacks))return null;
            }
            else if (element.Name == "replay")
            {
                Guid id;
                if (!Guid.TryParseExact(element.GetAttribute("id"), "N", out id) || id == Guid.Empty) return null;
                replay.Add(id);
            }
            else return null;
        }
        return new RebirthNpcInventoryPersistentRecord
        {
            NativeStacks = nativeStacks,
            NpcId = npcId,
            Revision = revision,
            Quantities = quantities,
            Reservations = reservations,
            ReplayJournal = replay.ToArray()
        };
    }

    private static List<KeyValuePair<string, int>> Sorted(Dictionary<string, int> values)
    {
        List<KeyValuePair<string, int>> result = new List<KeyValuePair<string, int>>(values);
        result.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static bool ValidateDocument(XmlDocument document)
    {
        XmlElement root = document != null ? document.DocumentElement : null; int format;
        if (root == null || root.Name != "rebirthNpcInventoryState" ||
            !int.TryParse(root.GetAttribute("format"), NumberStyles.Integer, CultureInfo.InvariantCulture, out format) ||
            format != FormatVersion) return false;
        var identities = new HashSet<RebirthNpcStableId>();
        foreach (XmlNode node in root.ChildNodes)
        {
            var element = node as XmlElement;
            if (element == null) continue;
            if (element.Name != "inventory") return false;
            var record = Read(element);
            string error;
            if (!RebirthNpcInventoryTransactionService.ValidatePersistentRecord(record, out error)
                || !identities.Add(record.NpcId)) return false;
        }
        return true;
    }

    private static string GetPath()
    {
        string directory = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(directory) ? string.Empty : Path.Combine(directory, FileName);
    }

    private static bool IsServer()
    {
        return SingletonMonoBehaviour<ConnectionManager>.Instance != null &&
            SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;
    }
}

[Preserve]
public sealed class RebirthNpcInventoryPersistenceModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        ModEvents.WorldShuttingDown.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) =>
        RebirthNpcInventoryPersistenceStore.Reset(true);
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) =>
        RebirthNpcInventoryPersistenceStore.Reset(true);
}
