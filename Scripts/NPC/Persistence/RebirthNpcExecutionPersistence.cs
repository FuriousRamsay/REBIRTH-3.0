using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public sealed class RebirthNpcPersistedExecution
{
    public int EntityId;
    public RebirthNpcStableId StableId;
    public ulong LeaseId;
    public ulong CommandId;
    public int SubjectEntityId;
    public string IssuerId;
    public RebirthNpcCommandKind CommandKind;
    public RebirthNpcOrderState Order;
    public Vector3 TargetPosition;
    public bool HasTargetPosition;
    public Vector3 GuardPosition;
    public bool HasGuardPosition;
    public long StartedUtcTicks;
}

public static partial class RebirthNpcExecutionPersistenceStore
{
    private sealed class SavedWorkCustody
    {
        internal readonly RebirthNpcExecutionWorkSaveScope Scope;
        internal readonly RebirthNpcPersistedExecution OriginalRecord;
        internal readonly RebirthNpcPersistedExecution Snapshot;
        internal readonly XmlElement OriginalElement;
        internal SavedWorkCustody(RebirthNpcExecutionWorkSaveScope scope,
            RebirthNpcPersistedExecution original, XmlElement element)
        {
            Scope=scope; OriginalRecord=original;
            Snapshot=RebirthNpcExecutionWorkRecordCopy.Copy(original);
            OriginalElement=element==null?null:(XmlElement)element.CloneNode(true);
        }
    }
    private static RebirthNpcExecutionWorkSaveScope loadedWorkScope;
    private static long workLoadGeneration;
    private static Dictionary<RebirthNpcPersistedExecution,SavedWorkCustody> WorkCustody =
        new Dictionary<RebirthNpcPersistedExecution,SavedWorkCustody>();

    public static bool TryGetCurrentWorkSaveScope(out RebirthNpcExecutionWorkSaveScope scope)
    {
        scope=System.Threading.Volatile.Read(ref loadedWorkScope);
        if(!IsCurrentWorkSaveScope(scope)){scope=null;return false;}
        return true;
    }

    public static bool IsCurrentWorkSaveScope(RebirthNpcExecutionWorkSaveScope scope)
    {
        if(scope==null || scope.LoadGeneration<=0 ||
            !ReferenceEquals(System.Threading.Volatile.Read(ref loadedWorkScope),scope))return false;
        try
        {
            return ReferenceEquals(GameManager.Instance==null?null:GameManager.Instance.World,scope.NativeWorld) &&
                string.Equals(GetNormalizedPath(),scope.SaveFilePath,StringComparison.Ordinal) &&
                ReferenceEquals(System.Threading.Volatile.Read(ref loadedWorkScope),scope);
        }
        catch { return false; }
    }

    private static void ArchiveUnknownWorkBeforeRetireNoLock()
    {
        foreach(var record in PendingByStableId.Values)
            if(record.Order==RebirthNpcOrderState.Work && !WorkCustody.ContainsKey(record))
                WorkCustody.Add(record,new SavedWorkCustody(null,record,null));
        foreach(var record in LegacyPendingByEntity.Values)
            if(record.Order==RebirthNpcOrderState.Work && !WorkCustody.ContainsKey(record))
                WorkCustody.Add(record,new SavedWorkCustody(null,record,null));
        // Retire reference/generation only. Original records/elements remain in WorkCustody.
        System.Threading.Volatile.Write(ref loadedWorkScope,null);
    }

    public static RebirthNpcWorkCustodyView[] GetHeldSavedWorkSnapshot()
    {
        lock(Sync)
        {
            var result=new List<RebirthNpcWorkCustodyView>();
            foreach(var custody in WorkCustody.Values)
            {
                bool current=IsCurrentWorkSaveScope(custody.Scope);
                result.Add(new RebirthNpcWorkCustodyView(custody.Snapshot.LeaseId,custody.Scope,
                    custody.Snapshot.StableId,false,custody.Scope==null || custody.Snapshot.StableId.IsEmpty?
                    RebirthNpcWorkCustodyDisposition.QuarantinedUnknownOrigin:
                    (current?RebirthNpcWorkCustodyDisposition.PendingSavedOriginalScope:
                        RebirthNpcWorkCustodyDisposition.QuarantinedForeignScope)));
            }
            return result.ToArray();
        }
    }

    private static XmlElement WriteOriginalHeldWork(XmlDocument document, RebirthNpcPersistedExecution record,
        RebirthNpcExecutionWorkSaveScope scope)
    {
        lock(Sync)
        {
            SavedWorkCustody custody;
            if(!WorkCustody.TryGetValue(record,out custody) ||
                !ReferenceEquals(custody.Scope,scope) || !IsCurrentWorkSaveScope(scope))return null;
            // Exact saved element preserves legacy owner/unknown fields without normalization/rebinding.
            return custody.OriginalElement==null?null:(XmlElement)document.ImportNode(custody.OriginalElement,true);
        }
    }

    // Refusal metadata only: no native execution/actuator ownership is issued by a saved record.
    public static bool IsCurrentWorldHeldSavedWorkOwner(int entityId)
    {
        try
        {
            var world=GameManager.Instance==null?null:GameManager.Instance.World;
            var npc=world==null?null:world.GetEntity(entityId) as EntityRebirthNPC;
            var runtime=npc==null?null:npc.RebirthRuntimeState;
            if(runtime==null || runtime.StableId.IsEmpty)return false;
            var stableOwner=runtime.StableId;
            lock(Sync)
            {
                RebirthNpcPersistedExecution record;SavedWorkCustody custody;
                var scope=System.Threading.Volatile.Read(ref loadedWorkScope);
                if(!IsCurrentWorkSaveScope(scope) ||
                    !PendingByStableId.TryGetValue(stableOwner,out record) ||
                    record.Order!=RebirthNpcOrderState.Work ||
                    !WorkCustody.TryGetValue(record,out custody) ||
                    !ReferenceEquals(custody.Scope,scope) ||
                    !custody.Snapshot.StableId.Equals(stableOwner))return false;
                return ReferenceEquals(GameManager.Instance==null?null:GameManager.Instance.World,world) &&
                    ReferenceEquals(world.GetEntity(entityId),npc) &&
                    ReferenceEquals(npc.RebirthRuntimeState,runtime) &&
                    runtime.StableId.Equals(stableOwner) && IsCurrentWorkSaveScope(scope);
            }
        }
        catch { return false; }
    }
    private const int FormatVersion = 2;
    private const string FileName = "RebirthNpcExecutionState.xml";
    private static readonly object Sync = new object();
    private static Dictionary<RebirthNpcStableId, RebirthNpcPersistedExecution> PendingByStableId =
        new Dictionary<RebirthNpcStableId, RebirthNpcPersistedExecution>();
    private static Dictionary<int, RebirthNpcPersistedExecution> LegacyPendingByEntity =
        new Dictionary<int, RebirthNpcPersistedExecution>();
    private static bool loaded;
    private static long nextSaveUtcTicks;
    private static long lastSavedLeaseRevision = -1L;
    private static long pendingRevision;
    private static long lastSavedPendingRevision = -1L;
    private static long saves;
    private static long restored;
    private static long rejected;
    internal static bool IsRecoveryBackingLoaded=>loaded&&IsCurrentWorkSaveScope(loadedWorkScope);
    internal static bool HasCurrentTravelOriginal(RebirthNpcStableId id)
    {
        lock(Sync)return loaded&&IsCurrentWorkSaveScope(loadedWorkScope)&&PendingByStableId.TryGetValue(id,out var row)&&
            row.Order==RebirthNpcOrderState.Travel&&row.HasTargetPosition&&row.LeaseId!=0&&string.IsNullOrEmpty(Validate(row));
    }
    internal static bool CanPrimeRecoveryBacking {get{lock(Sync)return !loaded&&PendingByStableId.Count==0&&LegacyPendingByEntity.Count==0&&WorkCustody.Count==0;}}
    internal static bool HasUnrestoredOrdinary(RebirthNpcStableId id,int entity)
    {
        lock(Sync)
        {
            if(!loaded||!IsCurrentWorkSaveScope(loadedWorkScope))return true;
            RebirthNpcPersistedExecution row;
            if(!PendingByStableId.TryGetValue(id,out row)&&!LegacyPendingByEntity.TryGetValue(entity,out row))return false;
            return row.Order!=RebirthNpcOrderState.Work||RebirthNpcWorkReleaseGate.Enabled;
        }
    }
    internal static bool CanRecoverQuiescent(bool replacingExecution,out string error)
    {
        error=string.Empty;
        lock(Sync)
        {
            if(PendingByStableId.Count!=0||LegacyPendingByEntity.Count!=0||WorkCustody.Count!=0)
            {error="Saved pending execution/Work originals must settle before recovery";return false;}
            if(replacingExecution)return true;
            if(!loaded||!IsCurrentWorkSaveScope(loadedWorkScope)){error="Current execution backing is not qualified";return false;}
            string path=loadedWorkScope.SaveFilePath;
            if(!File.Exists(path))
            {
                // The ordinary initializer intentionally refuses our own
                // writer fence. This is observational only, after a qualified
                // empty current scope was installed; any disk evidence holds.
                string directory=Path.GetDirectoryName(path);
                if(Directory.Exists(directory))foreach(var retained in Directory.EnumerateFiles(directory,Path.GetFileName(path)+".*"))
                {error="Missing current execution primary has retained evidence";return false;}
                return true;
            }
            try
            {
                var document=RebirthNpcRecoveryCurrentBacking.Read(path);
                if(!ValidateDocument(document)||ReadRecoveryRecords(document).Length!=0)
                {error="Current execution primary still contains prior originals; wait for its ordinary quiescent save";return false;}
                return true;
            }
            catch(Exception ex){error="Current execution primary cannot prove quiescence: "+ex.GetType().Name;return false;}
        }
    }

    public static void Tick()
    {
        if (!IsServer()) return;
        EnsureLoaded();
        long now = DateTime.UtcNow.Ticks;
        if (now < nextSaveUtcTicks) return;
        nextSaveUtcTicks = now + TimeSpan.FromSeconds(30).Ticks;
        if (RebirthNpcExecutionLeaseRegistry.Revision == lastSavedLeaseRevision && pendingRevision == lastSavedPendingRevision) return;
        Save();
    }

    public static void TryRestore(EntityRebirthNPC npc)
    {
        if (!IsServer() || npc == null || npc.RebirthRuntimeState == null) return;
        EnsureLoaded();
        RebirthNpcPersistedExecution record;
        lock (Sync)
        {
            if (!PendingByStableId.TryGetValue(npc.RebirthRuntimeState.StableId, out record))
            {
                if (!LegacyPendingByEntity.TryGetValue(npc.entityId, out record)) return;
                if(!RebirthNpcWorkReleaseGate.Enabled && record.Order==RebirthNpcOrderState.Work)return; // Original pending payload not consumed.
            }
            else { if(!RebirthNpcWorkReleaseGate.Enabled && record.Order==RebirthNpcOrderState.Work)return; }
        }

        string error = Validate(record);
        if (!string.IsNullOrEmpty(error))
        {
            rejected++;
            Log.Warning("[REBIRTH NPC] Ignored persisted execution for entity=" + npc.entityId + ": " + error);
            return;
        }

        bool recovery=RebirthNpcRecoveryGeneration.RequireCurrent()>0;
        if(recovery&&record.Order==RebirthNpcOrderState.None)
        {
            if(npc.RebirthRuntimeState.Order!=RebirthNpcOrderState.None||RebirthNpcExecutionLeaseRegistry.TryGetActive(npc.entityId,out _))return;
            // Explicit saved no-order is not a native lease invocation. Retire
            // only this exact passive row after positive current readback.
            lock(Sync)
            {
                if(PendingByStableId.TryGetValue(npc.RebirthRuntimeState.StableId,out var passive)&&ReferenceEquals(passive,record))PendingByStableId.Remove(npc.RebirthRuntimeState.StableId);
                if(LegacyPendingByEntity.TryGetValue(npc.entityId,out passive)&&ReferenceEquals(passive,record))LegacyPendingByEntity.Remove(npc.entityId);
                pendingRevision++;
            }
            return;
        }
        uint accepted=npc.RebirthRuntimeState.Revision;
        if(recovery)
        {
            // Recovered projection already owns its exact semantic revision.
            // A later range entry may not mint a command/revision or reinterpret
            // a pending original after a legitimate same-generation action.
            if(!RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(npc)&&(npc.RebirthRuntimeState.Order!=record.Order||npc.RebirthRuntimeState.HasGuardPosition!=record.HasGuardPosition||
                !npc.RebirthRuntimeState.GuardPosition.Equals(record.GuardPosition)))return;
        }
        else
        {
            var orderResult=npc.SetRebirthOrder(record.Order,record.GuardPosition,record.HasGuardPosition);
            if(!orderResult.Succeeded){rejected++;return;}accepted=orderResult.Revision;
        }

        RebirthNpcExecutionLease lease;
        if (!RebirthNpcExecutionLeaseRegistry.TryRestore(npc, record.LeaseId, record.CommandId,
            record.SubjectEntityId, record.TargetPosition, record.HasTargetPosition,
            record.IssuerId, record.CommandKind, record.Order, accepted,
            record.StartedUtcTicks, "Restored after world load; awaiting controller redispatch.", out lease))
        {
            rejected++;
            return;
        }
        lock(Sync)
        {
            if(PendingByStableId.TryGetValue(npc.RebirthRuntimeState.StableId,out var current)&&ReferenceEquals(current,record))PendingByStableId.Remove(npc.RebirthRuntimeState.StableId);
            if(LegacyPendingByEntity.TryGetValue(npc.entityId,out current)&&ReferenceEquals(current,record))LegacyPendingByEntity.Remove(npc.entityId);
            pendingRevision++;
        }
        restored++;
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH NPC] Restored execution lease=" + lease.LeaseId +
            " entity=" + npc.entityId + " order=" + lease.Order + "."); }
    }

    public static void Save()
    {
        if (GameManager.Instance?.World == null || GameManager.Instance.World.IsRemote() || !IsServer()) return;
        EnsureLoaded();
        RebirthNpcExecutionWorkSaveScope saveScope;
        if(!TryGetCurrentWorkSaveScope(out saveScope))throw new InvalidOperationException("Execution save has no original loaded world/save receipt.");
        string path = saveScope.SaveFilePath; RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded, path);
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            using var recoveryWriter = RebirthNpcPersistenceFile.BeginWrite(path);
            long capturedLeaseRevision = RebirthNpcExecutionLeaseRegistry.Revision;
            long capturedPendingRevision = pendingRevision;
            RebirthNpcExecutionLease[] leases = RebirthNpcExecutionLeaseRegistry.GetSnapshot();
            var nativeWorkExports=new HashSet<RebirthNpcPersistedExecution>();
            Dictionary<RebirthNpcStableId, RebirthNpcPersistedExecution> records =
                new Dictionary<RebirthNpcStableId, RebirthNpcPersistedExecution>();
            lock (Sync)
            {
                foreach (KeyValuePair<RebirthNpcStableId, RebirthNpcPersistedExecution> pair in PendingByStableId)
                    records[pair.Key] = pair.Value;
            }
            for (int i = 0; i < leases.Length; i++)
            {
                RebirthNpcExecutionLease lease = leases[i];
                if (lease.Status != RebirthNpcExecutionLeaseStatus.Active) continue;
                if(lease.Order==RebirthNpcOrderState.Work || RebirthNpcExecutionLeaseRegistry.IsHeldWorkLease(lease.LeaseId))
                {
                    RebirthNpcPersistedExecution originalWork;
                    if(RebirthNpcExecutionLeaseRegistry.TryExportOriginalWorkForScope(lease.LeaseId,saveScope,out originalWork))
                    { records[originalWork.StableId]=originalWork;nativeWorkExports.Add(originalWork); }
                    continue; // Never associate Work with current runtime by numeric entity ID.
                }
                RebirthNpcRuntimeState state;
                if (!RebirthNpcRuntimeRegistry.TryGet(lease.TargetEntityId, out state)) continue;
                RebirthNpcPersistedExecution prior;
                if(records.TryGetValue(state.StableId,out prior) && prior.Order==RebirthNpcOrderState.Work)continue; // Preserve original held Work under the one-record-per-owner format.
                records[state.StableId] = new RebirthNpcPersistedExecution
                {
                    EntityId = lease.TargetEntityId,
                    StableId = state.StableId,
                    LeaseId = lease.LeaseId,
                    CommandId = lease.CommandId,
                    SubjectEntityId = lease.SubjectEntityId,
                    IssuerId = lease.IssuerId,
                    CommandKind = lease.CommandKind,
                    Order = lease.Order,
                    TargetPosition = lease.TargetPosition,
                    HasTargetPosition = lease.HasTargetPosition,
                    GuardPosition = state.GuardPosition,
                    HasGuardPosition = state.HasGuardPosition,
                    StartedUtcTicks = lease.StartedUtcTicks
                };
            }

            XmlDocument document = new XmlDocument();
            XmlElement root = document.CreateElement("rebirthNpcExecutionState");
            root.SetAttribute("format", FormatVersion.ToString(CultureInfo.InvariantCulture));
            document.AppendChild(root);
            List<RebirthNpcStableId> stableIds = new List<RebirthNpcStableId>(records.Keys);
            stableIds.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
            for (int i = 0; i < stableIds.Count; i++)
            {
                var original=records[stableIds[i]];
                var saved=original.Order==RebirthNpcOrderState.Work?WriteOriginalHeldWork(document,original,saveScope):null;
                if(original.Order==RebirthNpcOrderState.Work && saved==null && !nativeWorkExports.Contains(original))
                    throw new InvalidOperationException("Held Work has no original saved/native export receipt.");
                root.AppendChild(saved??Write(document,original));
            }
            lock (Sync)
            {
                List<int> legacyEntityIds = new List<int>(LegacyPendingByEntity.Keys);
                legacyEntityIds.Sort();
                for (int i = 0; i < legacyEntityIds.Count; i++)
                {
                    var original=LegacyPendingByEntity[legacyEntityIds[i]];
                    var saved=original.Order==RebirthNpcOrderState.Work?WriteOriginalHeldWork(document,original,saveScope):null;
                    if(original.Order==RebirthNpcOrderState.Work && saved==null)
                        throw new InvalidOperationException("Legacy Work has no original saved element receipt.");
                    root.AppendChild(saved??Write(document,original));
                }
            }

            RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot("execution", path, FormatVersion);
            if(!IsCurrentWorkSaveScope(saveScope))throw new InvalidOperationException("World/save receipt changed before execution checkpoint write.");
            RebirthNpcPersistenceFile.SaveAtomic(path, document);
            if(!IsCurrentWorkSaveScope(saveScope))throw new InvalidOperationException("World/save receipt changed during execution checkpoint write; original file result cannot acknowledge another generation.");
            saves++;
            lastSavedLeaseRevision = capturedLeaseRevision;
            lastSavedPendingRevision = capturedPendingRevision;
            // Stable identity state is committed by identity-changing operations and the coordinated
            // world-save/shutdown pass. Do not force its loaded-NPC scan/XML rewrite from this
            // independent 30-second execution checkpoint.
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH NPC] Failed to save execution state: " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static XmlElement Write(XmlDocument document, RebirthNpcPersistedExecution record)
    {
        XmlElement element = document.CreateElement("execution");
        element.SetAttribute("stableId", record.StableId.ToString());
        Set(element, "entity", record.EntityId);
        Set(element, "lease", record.LeaseId);
        Set(element, "command", record.CommandId);
        Set(element, "subject", record.SubjectEntityId);
        element.SetAttribute("issuer", record.IssuerId ?? string.Empty);
        element.SetAttribute("commandKind", record.CommandKind.ToString());
        element.SetAttribute("order", record.Order.ToString());
        Set(element, "started", record.StartedUtcTicks);
        element.SetAttribute("hasTarget", record.HasTargetPosition ? "true" : "false");
        if (record.HasTargetPosition) SetVector(element, "target", record.TargetPosition);
        element.SetAttribute("hasGuard", record.HasGuardPosition ? "true" : "false");
        if (record.HasGuardPosition) SetVector(element, "guard", record.GuardPosition);
        return element;
    }

    public static void Reset(bool save)
    {
        if(!TryReset(save))throw new InvalidOperationException("Execution original custody retained; typed retirement required");
    }
    internal static bool CanRetireBacking
    {
        get{lock(Sync)try
        {
            if(PendingByStableId.Count!=0||LegacyPendingByEntity.Count!=0||WorkCustody.Count!=0||RebirthNpcExecutionLeaseRegistry.GetSnapshot().Length!=0)return false;
            if(loadedWorkScope==null)return !loaded;
            if(!loaded)return false;
            string path=loadedWorkScope.SaveFilePath;
            if(RebirthNpcPersistenceFile.IsRecoveryFenced(path))return false;
            if(!File.Exists(path))return RebirthNpcPersistenceFile.CanInitializeEmpty(path);
            var primary=RebirthNpcRecoveryCurrentBacking.Read(path);
            return ValidateDocument(primary)&&ReadRecoveryRecords(primary).Length==0;
        }catch{return false;}}
    }
    public static bool TryReset(bool save)
    {
        if(save)try{Save();}catch{return false;}
        lock (Sync)
        {
            if(!CanRetireBacking)return false;
            System.Threading.Volatile.Write(ref loadedWorkScope,null);
            PendingByStableId.Clear();
            LegacyPendingByEntity.Clear();
            loaded = false;
            nextSaveUtcTicks = 0L;
            lastSavedLeaseRevision = -1L;
            lastSavedPendingRevision = -1L;
            pendingRevision = 0L;
            return true;
        }
    }

    public static string GetReport()
    {
        EnsureLoaded();
        lock (Sync)
            return "[REBIRTH NPC] persistence pending=" + (PendingByStableId.Count + LegacyPendingByEntity.Count) +
                " restored=" + restored + " rejected=" + rejected + " saves=" + saves +
                " file=" + GetPath();
    }

    public static void EnsureLoaded()
    {
        if(GameManager.Instance?.World==null || GameManager.Instance.World.IsRemote() || !IsServer())return;
        lock(Sync)
        {
            if(loaded && IsCurrentWorkSaveScope(loadedWorkScope))return;
            if(loaded || PendingByStableId.Count!=0 || LegacyPendingByEntity.Count!=0)
                throw new InvalidOperationException("Retained execution originals must retire in their original world/save before another scope loads");
            string path=GetPath();
            if(string.IsNullOrEmpty(path))return;
            path=Path.GetFullPath(path);
            object originalWorld=GameManager.Instance.World;
            try
            {
                XmlDocument document;string source,loadError;
                if(!RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out document,out source,out loadError))
                {
                    if(!RebirthNpcPersistenceFile.CanInitializeEmpty(path))
                    {Log.Warning("[REBIRTH NPC] Failed to load execution state: "+loadError);return;}
                    if(!ReferenceEquals(GameManager.Instance==null?null:GameManager.Instance.World,originalWorld) ||
                        !string.Equals(GetNormalizedPath(),path,StringComparison.Ordinal))return;
                    var emptyScope=new RebirthNpcExecutionWorkSaveScope(path,originalWorld,
                        checked(++workLoadGeneration),"validated-empty");
                    loaded=true;System.Threading.Volatile.Write(ref loadedWorkScope,emptyScope);
                    return;
                }
                if(!ValidateDocument(document))throw new InvalidDataException("Invalid execution persistence document.");
                XmlElement root=document.DocumentElement;
                int format=int.Parse(root.GetAttribute("format"),CultureInfo.InvariantCulture);
                var parsed=new List<KeyValuePair<RebirthNpcPersistedExecution,XmlElement>>();
                foreach(XmlNode node in root.SelectNodes("execution"))
                {
                    var element=node as XmlElement;
                    var record=Read(element);
                    if(record==null)throw new InvalidDataException("Invalid execution record.");
                    parsed.Add(new KeyValuePair<RebirthNpcPersistedExecution,XmlElement>(record,element));
                }
                if(!ReferenceEquals(GameManager.Instance==null?null:GameManager.Instance.World,originalWorld) ||
                    !string.Equals(GetNormalizedPath(),path,StringComparison.Ordinal))
                    throw new InvalidDataException("World/save changed while execution records were loading.");
                var issued=new RebirthNpcExecutionWorkSaveScope(path,originalWorld,checked(++workLoadGeneration),source);
                if(RebirthNpcRecoveryGeneration.RequireCurrent()>0)
                {
                    if(source!="primary")throw new InvalidDataException("Recovery execution continuation requires current primary, not fallback history");
                    var rows=new List<RebirthNpcPersistedExecution>();foreach(var pair in parsed)rows.Add(pair.Key);
                    RebirthNpcExecutionLeaseRegistry.ReserveCurrentRecoveryExecution(rows.ToArray());
                }
                RebirthNpcPersistenceSchemaTelemetry.RecordLoad("execution",path,format,FormatVersion,source);
                foreach(var pair in parsed)
                {
                    var record=pair.Key;
                    if(!record.StableId.IsEmpty)PendingByStableId[record.StableId]=record;
                    else LegacyPendingByEntity[record.EntityId]=record;
                    if(record.Order==RebirthNpcOrderState.Work)
                        WorkCustody.Add(record,new SavedWorkCustody(issued,record,pair.Value));
                }
                loaded=true;System.Threading.Volatile.Write(ref loadedWorkScope,issued);
            }
            catch(Exception ex)
            {
                RebirthNpcPersistenceSchemaTelemetry.RecordRejected("execution",path,FormatVersion,ex.GetType().Name+": "+ex.Message);
                ArchiveUnknownWorkBeforeRetireNoLock();
                PendingByStableId.Clear();LegacyPendingByEntity.Clear();loaded=false;
                Log.Warning("[REBIRTH NPC] Failed to load execution state: "+ex.GetType().Name+": "+ex.Message);
            }
        }
    }


    internal static bool TryPrepareRecovery(XmlDocument document,out RebirthNpcPreparedRecovery candidate,out RebirthNpcStableId[] identities,out string error)
    {
        candidate=null;identities=null;error=string.Empty;
        if(!ValidateDocument(document)){error="execution semantic validation failed";return false;}
        var world=GameManager.Instance?.World;string path=GetNormalizedPath();long pending,leaseRevision,generation;RebirthNpcExecutionWorkSaveScope priorScope;
        lock(Sync)
        {
            if(loaded&&!IsCurrentWorkSaveScope(loadedWorkScope)){error="execution cache belongs to another retained original scope";return false;}
            if(WorkCustody.Count!=0){error="existing saved Work custody must settle before execution recovery";return false;}
            pending=pendingRevision;leaseRevision=RebirthNpcExecutionLeaseRegistry.Revision;generation=workLoadGeneration;priorScope=loadedWorkScope;
        }
        var issued=new RebirthNpcExecutionWorkSaveScope(path,world,checked(generation+1),"administrative-validated-backup");
        var stable=new Dictionary<RebirthNpcStableId,RebirthNpcPersistedExecution>();
        var custody=new Dictionary<RebirthNpcPersistedExecution,SavedWorkCustody>();
        var originalLeases=new HashSet<ulong>();
        foreach(XmlNode node in document.DocumentElement.SelectNodes("execution"))
        {
            var element=(XmlElement)node;var row=Read(element);
            if(row.StableId.IsEmpty){error="legacy entity-only execution has no proven stable owner for live recovery";return false;}
            if((row.LeaseId==0&&row.Order!=RebirthNpcOrderState.Work)||(row.LeaseId!=0&&!originalLeases.Add(row.LeaseId)))
            {error="recovered execution lacks a unique original lease identity";return false;}
            stable.Add(row.StableId,row);if(row.Order==RebirthNpcOrderState.Work)custody.Add(row,new SavedWorkCustody(issued,row,element));
        }
        identities=new List<RebirthNpcStableId>(stable.Keys).ToArray();
        Func<bool> current=()=>{lock(Sync)return ReferenceEquals(GameManager.Instance?.World,world)&&GetNormalizedPath()==path&&pendingRevision==pending&&workLoadGeneration==generation&&ReferenceEquals(loadedWorkScope,priorScope)&&WorkCustody.Count==0&&RebirthNpcExecutionLeaseRegistry.Revision==leaseRevision;};
        candidate=new RebirthNpcPreparedRecovery(FileName,current,()=>
        {
            lock(Sync)
            {
                if(!current())return false;
                PendingByStableId=stable;LegacyPendingByEntity=new Dictionary<int,RebirthNpcPersistedExecution>();WorkCustody=custody;
                workLoadGeneration=checked(generation+1);pendingRevision=checked(pending+1);lastSavedPendingRevision=pendingRevision;lastSavedLeaseRevision=leaseRevision;
                System.Threading.Volatile.Write(ref loadedWorkScope,issued);loaded=true;nextSaveUtcTicks=0;return true;
            }
        });return true;
    }
    internal static bool ValidateRecoveryDocument(XmlDocument document)=>ValidateDocument(document);
    internal static RebirthNpcPersistedExecution[] ReadRecoveryRecords(XmlDocument document)
    {
        if(!ValidateDocument(document))throw new InvalidDataException("Execution recovery document invalid.");
        var records=new List<RebirthNpcPersistedExecution>();foreach(XmlElement row in document.DocumentElement.SelectNodes("execution"))records.Add(Read(row));return records.ToArray();
    }
    private static bool ValidateDocument(XmlDocument document)
    {
        XmlElement root = document != null ? document.DocumentElement : null;
        int format;
        if(root==null||root.Name!="rebirthNpcExecutionState"
            ||!int.TryParse(root.GetAttribute("format"),NumberStyles.Integer,CultureInfo.InvariantCulture,out format)
            ||(format!=1&&format!=FormatVersion))return false;
        var stableIds=new HashSet<RebirthNpcStableId>();var legacyIds=new HashSet<int>();
        foreach(XmlNode node in root.ChildNodes)
        {
            var element=node as XmlElement;if(element==null)continue;
            if(element.Name!="execution")return false;
            var record=Read(element);
            if(record==null||!string.IsNullOrEmpty(Validate(record)))return false;
            if(!record.StableId.IsEmpty){if(!stableIds.Add(record.StableId))return false;}
            else if(!legacyIds.Add(record.EntityId))return false;
        }
        return true;
    }

    private static RebirthNpcPersistedExecution Read(XmlElement element)
    {
        if (element == null) return null;
        int entity, subject; ulong lease, command; long started;
        RebirthNpcCommandKind commandKind; RebirthNpcOrderState order;
        if (!Try(element, "entity", out entity) || !Try(element, "subject", out subject) ||
            !Try(element, "lease", out lease) || !Try(element, "command", out command) ||
            !Try(element, "started", out started) ||
            !Enum.TryParse(element.GetAttribute("commandKind"), true, out commandKind) ||
            !Enum.TryParse(element.GetAttribute("order"), true, out order)) return null;
        if(!Enum.IsDefined(typeof(RebirthNpcCommandKind),commandKind)||!Enum.IsDefined(typeof(RebirthNpcOrderState),order))return null;
        bool hasTarget, hasGuard;
        bool.TryParse(element.GetAttribute("hasTarget"), out hasTarget);
        bool.TryParse(element.GetAttribute("hasGuard"), out hasGuard);
        RebirthNpcStableId stableId;
        string stableText=element.GetAttribute("stableId");
        if(!RebirthNpcStableId.TryParse(stableText,out stableId)&&!string.IsNullOrEmpty(stableText)&&stableText!=default(RebirthNpcStableId).ToString())return null;
        return new RebirthNpcPersistedExecution
        {
            EntityId = entity, StableId = stableId, LeaseId = lease, CommandId = command, SubjectEntityId = subject,
            IssuerId = element.GetAttribute("issuer"), CommandKind = commandKind, Order = order,
            StartedUtcTicks = started,
            HasTargetPosition = hasTarget, TargetPosition = hasTarget ? ReadVector(element, "target") : Vector3.zero,
            HasGuardPosition = hasGuard, GuardPosition = hasGuard ? ReadVector(element, "guard") : Vector3.zero
        };
    }

    private static string Validate(RebirthNpcPersistedExecution record)
    {
        if(record==null||!Enum.IsDefined(typeof(RebirthNpcOrderState),record.Order)||!Enum.IsDefined(typeof(RebirthNpcCommandKind),record.CommandKind))return "Execution enum is invalid.";
        if(record.StartedUtcTicks<0||!Finite(record.TargetPosition)||!Finite(record.GuardPosition))return "Execution time/position is invalid.";
        if (record.Order == RebirthNpcOrderState.None) return "Order is None.";
        if (record.Order == RebirthNpcOrderState.Follow && record.SubjectEntityId < 0)
            return "Follow subject is invalid.";
        if ((record.Order == RebirthNpcOrderState.Guard || record.Order == RebirthNpcOrderState.Patrol ||
             record.Order == RebirthNpcOrderState.Work || record.Order == RebirthNpcOrderState.Travel ||
             record.Order == RebirthNpcOrderState.Mission) && !record.HasTargetPosition)
            return "Position-bound order has no target.";
        if (record.Order == RebirthNpcOrderState.Guard && !record.HasGuardPosition)
            return "Guard order has no guard position.";
        return string.Empty;
    }
    private static bool Finite(Vector3 position)=>!float.IsNaN(position.x)&&!float.IsInfinity(position.x)&&!float.IsNaN(position.y)&&!float.IsInfinity(position.y)&&!float.IsNaN(position.z)&&!float.IsInfinity(position.z);

    private static bool IsServer() => SingletonMonoBehaviour<ConnectionManager>.Instance != null &&
        SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;
    // Recheck the live directory on every admission; normalize only when it changes.
    private static readonly object PathSync = new object();
    private static string cachedDirectory, cachedNormalizedPath;
    private static string GetNormalizedPath()
    {
        string directory = GameIO.GetSaveGameDir();
        if (string.IsNullOrEmpty(directory)) return string.Empty;
        if (!Path.IsPathRooted(directory)) return Path.GetFullPath(Path.Combine(directory, FileName));
        lock (PathSync)
        {
            if (!string.Equals(directory, cachedDirectory, StringComparison.Ordinal))
            {
                string normalized = Path.GetFullPath(Path.Combine(directory, FileName));
                cachedNormalizedPath = normalized;
                cachedDirectory = directory;
            }
            return cachedNormalizedPath;
        }
    }
    private static string GetPath()
    {
        string directory = GameIO.GetSaveGameDir();
        return string.IsNullOrEmpty(directory) ? string.Empty : Path.Combine(directory, FileName);
    }
    private static void Set(XmlElement e, string name, object value) =>
        e.SetAttribute(name, Convert.ToString(value, CultureInfo.InvariantCulture));
    private static void SetVector(XmlElement e, string prefix, Vector3 v)
    {
        Set(e, prefix + "X", v.x); Set(e, prefix + "Y", v.y); Set(e, prefix + "Z", v.z);
    }
    private static Vector3 ReadVector(XmlElement e, string prefix)
    {
        float x, y, z;
        if(!float.TryParse(e.GetAttribute(prefix+"X"),NumberStyles.Float,CultureInfo.InvariantCulture,out x)
            ||!float.TryParse(e.GetAttribute(prefix+"Y"),NumberStyles.Float,CultureInfo.InvariantCulture,out y)
            ||!float.TryParse(e.GetAttribute(prefix+"Z"),NumberStyles.Float,CultureInfo.InvariantCulture,out z)
            ||float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||float.IsNaN(z)||float.IsInfinity(z))
            throw new InvalidDataException("Invalid execution position.");
        return new Vector3(x, y, z);
    }
    private static bool Try(XmlElement e, string n, out int v) => int.TryParse(e.GetAttribute(n), NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
    private static bool Try(XmlElement e, string n, out ulong v) => ulong.TryParse(e.GetAttribute(n), NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
    private static bool Try(XmlElement e, string n, out long v) => long.TryParse(e.GetAttribute(n), NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
}

[Preserve]
public sealed class RebirthNpcExecutionPersistenceModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        // Coordinated save/reset is owned by RebirthNpcLifecycle.
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        RebirthNpcLifecycle.RequestCoordinatedShutdown(false);
    }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        RebirthNpcLifecycle.RequestCoordinatedShutdown(true);
    }
}
