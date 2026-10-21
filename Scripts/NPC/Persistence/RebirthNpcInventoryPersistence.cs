using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine.Scripting;

#nullable disable

public static partial class RebirthNpcInventoryPersistenceStore
{
    private const int FormatVersion = 1;
    private const string FileName = "rebirth_npc_inventory.xml";
    private static readonly object Sync = new object();
    private static bool loaded;
    private static World boundWorld;private static string boundPath;
    private static RebirthNpcOriginalSaveBinding originalBinding;
    private static bool BoundCurrent=>boundWorld!=null&&ReferenceEquals(boundWorld,GameManager.Instance?.World)&&boundPath==GetPath();
    private static bool BoundCompatible=>boundWorld==null||BoundCurrent;
    private static void BindCurrent(){if(!BoundCompatible)throw new InvalidOperationException("Inventory cache belongs to another original world/save.");boundWorld=GameManager.Instance.World;boundPath=GetPath();originalBinding=RebirthNpcOriginalSaveBinding.Capture(boundWorld,boundPath,RebirthNpcInventoryTransactionService.RecoveryEpoch);}
    private static volatile bool dirty;
    private static long nextSaveUtcTicks;
    private static long saves;
    private static long restored;
    private static long rejected;

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
            if(!BoundCompatible)throw new InvalidOperationException("Retained inventory cache must retire in its original scope before another world loads.");
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
                    BindCurrent();loaded = true;
                    return;
                }
                RebirthNpcPersistenceSchemaTelemetry.RecordLoad("inventory", path, FormatVersion, FormatVersion, source);
                var records=new List<RebirthNpcInventoryPersistentRecord>();
                foreach(XmlNode node in document.DocumentElement.SelectNodes("inventory"))records.Add(Read(node as XmlElement));
                if(!RebirthNpcInventoryTransactionService.TryReplacePersistentRecords(records,out var recordError))
                {rejected++;throw new InvalidDataException("Invalid inventory generation: "+recordError);}
                restored+=records.Count;
                dirty = false;
                BindCurrent();loaded = true;
            }
            catch (Exception ex)
            {
                // Detached generation rejection leaves the prior live state intact.
                RebirthNpcPersistenceSchemaTelemetry.RecordRejected("inventory", path, FormatVersion,
                    ex.GetType().Name + ": " + ex.Message);
                Log.Warning("[REBIRTH NPC] Failed to load inventory state: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    public static void MarkDirty()
    {
        if (IsServer()) { if(!ThreadManager.IsMainThread()){dirty=true;originalBinding?.InvalidateMutation();return;} lock(Sync){dirty=true;originalBinding?.ObserveMutation(RebirthNpcInventoryTransactionService.RecoveryEpoch);} }
    }

    // Positive observation of the final canonical inventory file only. No load,
    // backup fallback, runtime mutation, or forced write is evidence of a commit.
    internal static bool ObserveSavedTransferSnapshot(RebirthNpcStableId id,Func<RebirthNpcInventoryPersistentRecord,bool> positiveObservation)
        =>positiveObservation!=null&&RebirthNpcSavedXmlObservation.Observe(GetPath(),()=>TryReadSavedTransferSnapshot(id,out var saved)&&positiveObservation(saved));

    internal static bool TryReadSavedTransferSnapshot(RebirthNpcStableId id,out RebirthNpcInventoryPersistentRecord record)
    {
        record=null;
        if(id.IsEmpty||!ThreadManager.IsMainThread()||!IsServer()||GameManager.Instance?.World?.IsRemote()!=false)return false;
        var world=GameManager.Instance.World;var scope=RebirthNpcSaveScope.ObserveCurrent();string directory=GameIO.GetSaveGameDir(),path=GetPath();
        if(scope==null||string.IsNullOrEmpty(directory)||string.IsNullOrEmpty(path)||!string.Equals(scope.SaveDirectory,directory,StringComparison.OrdinalIgnoreCase))return false;
        try
        {
            var info=new FileInfo(path);if(!info.Exists||info.Length<1||info.Length>64L*1024*1024)return false;
            var document=new XmlDocument{XmlResolver=null};
            using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64L*1024*1024}))document.Load(reader);
            if(!ValidateDocument(document))return false;
            RebirthNpcInventoryPersistentRecord found=null;
            foreach(XmlNode node in document.DocumentElement.ChildNodes)
            {
                var candidate=Read(node as XmlElement);if(candidate==null||candidate.NpcId!=id)continue;if(found!=null)return false;found=candidate;
            }
            var after=RebirthNpcSaveScope.ObserveCurrent();
            if(found==null||after==null||!ReferenceEquals(GameManager.Instance?.World,world)||GameIO.GetSaveGameDir()!=directory||GetPath()!=path||
                !string.Equals(after.SaveDirectory,scope.SaveDirectory,StringComparison.OrdinalIgnoreCase)||after.Fingerprint!=scope.Fingerprint)return false;
            record=found;return true;
        }
        catch{return false;}
    }
    // Administrative backup recovery cannot roll physical custody or original
    // effect/replay receipts backwards. Terminal transfer receipts authorize only
    // their own effect, never restoration of a historical whole inventory.
    // This initial-admission check is deliberately separate from cold adoption
    // of a positively installed recovery-generation publication manifest.
    internal static bool MatchesCurrentRecoveryCustody(RebirthNpcInventoryPersistentRecord[] proposed,out string error)
    {
        error=string.Empty;
        if(proposed==null||!ThreadManager.IsMainThread()||!IsServer()||GameManager.Instance?.World?.IsRemote()!=false)
        {error="authoritative inventory custody observation unavailable";return false;}
        lock(Sync)try
        {
            var world=GameManager.Instance.World;string path=GetPath(),directory=GameIO.GetSaveGameDir();
            if(!BoundCompatible||string.IsNullOrEmpty(path))throw new InvalidDataException("inventory cache scope differs");
            var epoch=RebirthNpcInventoryTransactionService.RecoveryEpoch;
            var runtime=RebirthNpcInventoryTransactionService.CapturePersistentRecords();
            string expectedCustody=RecoveryCustodyImage(proposed);
            // Do not load/fallback/save here: only the existing final primary can
            // corroborate current custody. Missing/corrupt evidence is unknown.
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(input.Length<1||input.Length>67108864)throw new InvalidDataException("inventory primary bound");
                var doc=new XmlDocument{XmlResolver=null};
                using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=67108864}))doc.Load(reader);
                if(!ValidateDocument(doc))throw new InvalidDataException("inventory primary semantic validation");
                var saved=new List<RebirthNpcInventoryPersistentRecord>();
                foreach(XmlElement row in doc.DocumentElement.SelectNodes("inventory"))saved.Add(Read(row));
                var current=saved.ToArray();
                if(RecoveryBackingImage(current)!=RecoveryBackingImage(runtime)||expectedCustody!=RecoveryCustodyImage(current))
                {error="backup inventory changes current physical/replay custody or current primary/runtime full image is not identical";return false;}
            }
            if(!ReferenceEquals(world,GameManager.Instance?.World)||directory!=GameIO.GetSaveGameDir()||path!=GetPath()||
                !ReferenceEquals(epoch,RebirthNpcInventoryTransactionService.RecoveryEpoch))
            {error="inventory custody changed during recovery observation";return false;}
            return true;
        }
        catch(Exception ex){error="current inventory custody is unproved: "+ex.GetType().Name;return false;}
    }
    // Cold interrupted INITIAL install only: durable staged before/after must
    // prove no historical physical inventory or original receipts were restored.
    internal static bool ProvesStagedRecoveryCustody(XmlDocument before,XmlDocument after,out string error)
    {
        error=string.Empty;
        try
        {
            if(!ValidateDocument(before)||!ValidateDocument(after))throw new InvalidDataException("Staged custody schema invalid");
            var prior=new List<RebirthNpcInventoryPersistentRecord>();var next=new List<RebirthNpcInventoryPersistentRecord>();
            foreach(XmlElement row in before.DocumentElement.SelectNodes("inventory"))prior.Add(Read(row));
            foreach(XmlElement row in after.DocumentElement.SelectNodes("inventory"))next.Add(Read(row));
            var nextRows=next.ToArray();var priorRows=prior.ToArray();string expected=RecoveryCustodyImage(nextRows);
            if(expected!=RecoveryCustodyImage(priorRows))throw new InvalidDataException("Staged backup changes physical custody or original receipts");
            var runtime=RebirthNpcInventoryTransactionService.CapturePersistentRecords();
            if(runtime.Length!=0&&RecoveryBackingImage(runtime)!=RecoveryBackingImage(priorRows)&&RecoveryBackingImage(runtime)!=RecoveryBackingImage(nextRows))throw new InvalidDataException("Current runtime differs from both exact staged full images");
            return true;
        }catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;return false;}
    }
    internal static bool IsTransferLoaded
    {get{if(!ThreadManager.IsMainThread()||!IsServer()||GameManager.Instance?.World?.IsRemote()!=false)return false;lock(Sync)return loaded&&BoundCurrent;}}
    internal static bool TryFlushOriginalBacking()
    {
        if(!ThreadManager.IsMainThread()||!IsServer())return false;
        lock(Sync)try
        {
            if(BoundCurrent&&dirty){Save();return CanRetireBacking;}
            if(!dirty)return CanRetireBacking;
            if(!loaded||originalBinding==null)return false;
            var epoch=RebirthNpcInventoryTransactionService.RecoveryEpoch;
            
            if(!ReferenceEquals(epoch,RebirthNpcInventoryTransactionService.RecoveryEpoch))return false;
            if(!originalBinding.TryPublish(()=>{var doc=new XmlDocument{XmlResolver=null};doc.LoadXml(RecoveryBackingImage(RebirthNpcInventoryTransactionService.CapturePersistentRecords()));return doc;},ValidateDocument,()=>RebirthNpcInventoryTransactionService.RecoveryEpoch,out var error))
            {if(!string.IsNullOrEmpty(error))Log.Warning("[REBIRTH NPC] Original inventory flush retained: "+error);return false;}
            dirty=false;saves++;return CanRetireBacking;
        }catch(Exception error){Log.Warning("[REBIRTH NPC] Original inventory flush retained: "+error.GetType().Name);return false;}
    }
    internal static bool CanRetireBacking
    {
        get
        {
            lock(Sync)try
            {
                var epoch=RebirthNpcInventoryTransactionService.RecoveryEpoch;
                var runtime=RebirthNpcInventoryTransactionService.CapturePersistentRecords();
                if(dirty)return false;
                if(boundWorld==null)return !loaded&&!dirty&&ReferenceEquals(epoch,RebirthNpcInventoryTransactionService.RecoveryEpoch)&&runtime.Length==0;
                if(string.IsNullOrEmpty(boundPath)||RebirthNpcPersistenceFile.IsRecoveryFenced(boundPath))return false;
                if(!File.Exists(boundPath))return !dirty&&ReferenceEquals(epoch,RebirthNpcInventoryTransactionService.RecoveryEpoch)&&runtime.Length==0&&!File.Exists(boundPath+".bak")&&!RebirthNpcPersistenceFile.IsRecoveryFenced(boundPath);
                using(var input=new FileStream(boundPath,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(input.Length<1||input.Length>67108864)return false;
                    var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=67108864}))doc.Load(reader);
                    if(!ValidateDocument(doc))return false;
                    var saved=new List<RebirthNpcInventoryPersistentRecord>();foreach(XmlElement row in doc.DocumentElement.SelectNodes("inventory"))saved.Add(Read(row));
                    return !dirty&&ReferenceEquals(epoch,RebirthNpcInventoryTransactionService.RecoveryEpoch)&&RecoveryBackingImage(runtime)==RecoveryBackingImage(saved.ToArray());
                }
            }
            catch{return false;}
        }
    }
    private static string RecoveryBackingImage(RebirthNpcInventoryPersistentRecord[] records)
    {
        var rows=new List<RebirthNpcInventoryPersistentRecord>(records);rows.Sort((a,b)=>string.CompareOrdinal(a.NpcId.ToString(),b.NpcId.ToString()));
        var doc=new XmlDocument();var root=doc.CreateElement("rebirthNpcInventoryState");root.SetAttribute("format",FormatVersion.ToString(CultureInfo.InvariantCulture));doc.AppendChild(root);
        foreach(var row in rows)root.AppendChild(Write(doc,row));return root.OuterXml;
    }
    internal static string RecoveryCustodyImage(RebirthNpcInventoryPersistentRecord[] records)
    {
        var document=new XmlDocument{XmlResolver=null};document.LoadXml(RecoveryBackingImage(records));
        // ONLY the top-level transport revision is replaced by independent G.
        // Every native slot, quantity, reservation, replay/effect byte and ID
        // remains part of the exact current-custody equality.
        foreach(XmlElement row in document.DocumentElement.SelectNodes("inventory"))row.RemoveAttribute("revision");
        return document.OuterXml;
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
                using var recoveryWriter = RebirthNpcPersistenceFile.BeginWrite(path);
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
                originalBinding=RebirthNpcOriginalSaveBinding.Capture(boundWorld,boundPath,RebirthNpcInventoryTransactionService.RecoveryEpoch);
                saves++;
            }
            catch (Exception ex)
            {
                Log.Warning("[REBIRTH NPC] Failed to save inventory state: " + ex.GetType().Name + ": " + ex.Message);
                throw;
            }
        }
    }

    internal static bool TryPrepareRecovery(XmlDocument document,out RebirthNpcPreparedRecovery candidate,out RebirthNpcInventoryPersistentRecord[] records,out string error)
    {
        candidate=null;records=null;error=string.Empty;
        var recoveryWorld=GameManager.Instance?.World;string recoveryPath=GetPath();
        if(!ThreadManager.IsMainThread()||recoveryWorld?.IsRemote()!=false||string.IsNullOrEmpty(recoveryPath)){error="authoritative recovery store scope unavailable";return false;}
        lock(Sync)if(!BoundCompatible){error="inventory recovery would replace another original world cache";return false;}
        if(!ValidateDocument(document)){error="inventory semantic validation failed";return false;}
        var detached=new List<RebirthNpcInventoryPersistentRecord>();
        foreach(XmlNode node in document.DocumentElement.SelectNodes("inventory"))detached.Add(Read(node as XmlElement));
        if(!RebirthNpcInventoryTransactionService.TryPreparePersistentRecords(detached,out var generation,out error))return false;
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
    {
        if(!TryReset(save))throw new InvalidOperationException("Inventory retirement refused; original custody remains retained");
    }
    public static bool TryReset(bool save)
    {
        if(save)try{Save();}catch{return false;}
        lock (Sync)
        {
            if(!CanRetireBacking)return false;
            RebirthNpcInventoryTransactionService.ClearRuntimeState();
            RebirthNpcInventoryAuthorityService.Reset();
            loaded = false;boundWorld=null;boundPath=null;originalBinding=null;
            dirty = false;
            nextSaveUtcTicks = 0L;
            return true;
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
        if(record.LogisticsEffects?.Count>0)
        {
            if(!RebirthLogisticsNpcEffectReceipt.Validate(record.NpcId,record.Revision,record.LogisticsEffects))throw new InvalidDataException("Invalid saved original inventory effects.");
            var effects=document.CreateElement("logisticsEffects");effects.SetAttribute("version","1");
            var ids=new List<Guid>(record.LogisticsEffects.Keys);ids.Sort();
            foreach(var id in ids)
            {
                var effect=document.CreateElement("effect");effect.SetAttribute("id",id.ToString("N"));
                effect.InnerText=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(record.LogisticsEffects[id]));effects.AppendChild(effect);
            }
            inventory.AppendChild(effects);
        }
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
        Dictionary<Guid,string> effects=null;
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
            else if(element.Name=="logisticsEffects")
            {
                if(effects!=null||element.NamespaceURI.Length!=0||element.Attributes.Count!=1||element.GetAttribute("version")!="1")return null;
                effects=new Dictionary<Guid,string>();
                foreach(XmlNode child in element.ChildNodes)
                {
                    if(child is XmlWhitespace)continue;
                    if(!(child is XmlElement effect)||effect.Name!="effect"||effect.NamespaceURI.Length!=0||effect.Attributes.Count!=1||effect.ChildNodes.Count!=1||!(effect.FirstChild is XmlText)||
                        effects.Count>=RebirthLogisticsNpcEffectReceipt.MaximumReceipts||effect.InnerText.Length>RebirthLogisticsNpcEffectReceipt.MaximumText*6||
                        !Guid.TryParseExact(effect.GetAttribute("id"),"N",out var id)||id==Guid.Empty||id.ToString("N")!=effect.GetAttribute("id")||effects.ContainsKey(id))return null;
                    string text;
                    try{var bytes=Convert.FromBase64String(effect.InnerText);if(Convert.ToBase64String(bytes)!=effect.InnerText)return null;text=new System.Text.UTF8Encoding(false,true).GetString(bytes);}catch{return null;}
                    if(!RebirthLogisticsNpcEffectReceipt.TryRead(text,out var value)||value.Transaction!=id)return null;
                    effects.Add(id,text);
                }
            }
            else return null;
        }
        return new RebirthNpcInventoryPersistentRecord
        {
            NativeStacks = nativeStacks,
            LogisticsEffects = effects??new Dictionary<Guid,string>(),
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

    // Detached validation only; administrative recovery must use the same
    // record invariants as normal loading before publishing a candidate.
    internal static bool ValidateRecoveryDocument(XmlDocument document)=>ValidateDocument(document);

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
        // RebirthNpcLifecycle owns the coordinated save/reset. An independent
        // callback must not clear this store after another store failed.
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) =>
        RebirthNpcLifecycle.RequestCoordinatedShutdown(false);
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) =>
        RebirthNpcLifecycle.RequestCoordinatedShutdown(true);
}
