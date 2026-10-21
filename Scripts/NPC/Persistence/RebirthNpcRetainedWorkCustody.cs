using System;
using System.Collections.Generic;

// Detached work remains owned by its original native World. Identity fences
// allow unrelated incoming NPCs to run without rebinding or replaying work.
internal sealed class RebirthNpcRetainedWorkCustody
{
    private static readonly List<RebirthNpcRetainedWorkCustody> held=new List<RebirthNpcRetainedWorkCustody>();
    private readonly World world;
    private readonly string directory;
    private readonly Func<bool> originalCurrent,detached;
    private readonly Dictionary<ulong,RebirthNpcExecutionLease> leases;
    private readonly Dictionary<ulong,RebirthNpcWorkExecutionSnapshot> telemetry;
    private readonly HashSet<RebirthNpcStableId> owners;
    private readonly HashSet<ulong> savedLeases;
    private readonly HashSet<ulong> savedAssignments;
    private bool registered;
    private RebirthNpcRuntimeNativeAdoption.NativeReentry incomingSource;
    private RebirthDogLifecycleService.NativeOwnerReentry incomingOwner;
    internal bool ObserveOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(!registered||HasUnresolved||owners.Count!=0||source?.Current()!=true||ReferenceEquals(source.World,world)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory))return false;
        if(incomingSource!=null&&!ReferenceEquals(incomingSource.World,source.World)||incomingOwner!=null&&!ReferenceEquals(incomingOwner.World,source.World))return false;
        incomingOwner=source;return true;
    }
    internal RebirthNpcRetainedWorkCustody(RebirthNpcSaveQuarantineScope scope,Func<bool> current,Func<bool> detached,
        Dictionary<ulong,RebirthNpcExecutionLease> leases,Dictionary<ulong,RebirthNpcWorkExecutionSnapshot> telemetry,
        HashSet<RebirthNpcStableId> owners,HashSet<ulong> savedLeases,HashSet<ulong> savedAssignments)
    {world=scope.World;directory=scope.Directory;originalCurrent=current;this.detached=detached;this.leases=leases;this.telemetry=telemetry;this.owners=owners;this.savedLeases=savedLeases;this.savedAssignments=savedAssignments;}
    internal bool IsOriginalCurrent=>ThreadManager.IsMainThread()&&!registered&&originalCurrent();
    internal bool CanRegister=>ThreadManager.IsMainThread()&&!registered&&held.Count<8&&originalCurrent();
    internal bool ObserveIncoming(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(!registered||source?.Current()!=true||ReferenceEquals(source.World,world)||!RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory))return false;
        if(incomingSource!=null&&!ReferenceEquals(incomingSource.World,source.World))return false;
        incomingSource=source;return true;
    }
    internal bool IncomingSafe(World incoming,string save)
    {
        bool native=incomingSource?.Current()==true&&ReferenceEquals(incomingSource.World,incoming)||
            !HasUnresolved&&owners.Count==0&&incomingOwner?.Current()==true&&ReferenceEquals(incomingOwner.World,incoming);
        return ThreadManager.IsMainThread()&&registered&&held.Contains(this)&&detached()&&native&&
            ReferenceEquals(GameManager.Instance?.World,incoming)&&incoming?.IsRemote()==false&&
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true&&RebirthNpcSaveQuarantineScope.SameDirectory(directory,save)&&
            RebirthNpcSaveQuarantineScope.SameDirectory(save,GameIO.GetSaveGameDir());
    }
    internal bool AbortAfterRollback()
    {
        if(!ThreadManager.IsMainThread()||!originalCurrent())return false;
        if(registered&&!held.Remove(this))return false;
        registered=false;incomingSource=null;incomingOwner=null;return true;
    }
    internal bool RegisterDetached()
    {
        if(!ThreadManager.IsMainThread())return false;
        if(registered)return held.Contains(this);
        if(held.Count>=8||!detached())return false;
        held.Add(this);registered=true;return true;
    }
    internal bool HasUnresolved=>leases.Count!=0||telemetry.Count!=0||savedLeases.Count!=0||savedAssignments.Count!=0;
    private bool Relevant=>registered&&ThreadManager.IsMainThread()&&!ReferenceEquals(world,GameManager.Instance?.World)&&
        RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir());
    internal static bool BlocksOwner(RebirthNpcStableId owner)
    {if(owner.IsEmpty)return false;foreach(var item in held)if(item.Relevant&&item.HasUnresolved&&item.owners.Contains(owner))return true;return false;}
    internal static bool BlocksEntity(int entity)
    {
        var npc=GameManager.Instance?.World?.GetEntity(entity) as EntityRebirthNPC;
        return npc?.RebirthRuntimeState!=null&&BlocksOwner(npc.RebirthRuntimeState.StableId);
    }
    internal static bool BlocksLease(ulong lease)
    {foreach(var item in held)if(item.Relevant&&(item.leases.ContainsKey(lease)||item.savedLeases.Contains(lease)))return true;return false;}
    internal static bool BlocksAssignment(ulong assignment)
    {foreach(var item in held)if(item.Relevant&&(item.telemetry.ContainsKey(assignment)||item.savedAssignments.Contains(assignment)))return true;return false;}
    internal static bool BlocksWrite(string path)
    {
        if(string.IsNullOrEmpty(path))return false;
        string full;
        try{full=System.IO.Path.GetFullPath(path);}catch{return true;}
        foreach(var item in held)
        {
            if(!item.registered||!item.HasUnresolved||ReferenceEquals(item.world,GameManager.Instance?.World))continue;
            if(!RebirthNpcSaveQuarantineScope.SameDirectory(System.IO.Path.GetDirectoryName(full),item.directory))continue;
            string name=System.IO.Path.GetFileName(full);
            var comparison=System.IO.Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
            bool Matches(string file)=>string.Equals(name,file,comparison)||string.Equals(name,file+".bak",comparison)||string.Equals(name,file+".tmp",comparison);
            bool execution=item.leases.Count!=0||item.savedLeases.Count!=0;
            bool checkpoint=item.telemetry.Count!=0||item.savedAssignments.Count!=0;
            if(execution&&Matches("RebirthNpcExecutionState.xml")||checkpoint&&Matches("RebirthNpcWorkCheckpoints.xml"))return true;
        }
        return false;
    }
    // Called only by the actual registry terminal publisher with its private
    // original object. A reused integer ID/new-world callback cannot settle it.
    internal static void ObserveLeaseTerminal(RebirthNpcExecutionLease original)
    {
        if(!ThreadManager.IsMainThread()||original==null||original.Status==RebirthNpcExecutionLeaseStatus.Active)return;
        foreach(var item in held)
            if(ReferenceEquals(item.world,GameManager.Instance?.World)&&RebirthNpcSaveQuarantineScope.SameDirectory(item.directory,GameIO.GetSaveGameDir())&&
                item.leases.TryGetValue(original.LeaseId,out var captured)&&ReferenceEquals(captured,original))item.leases.Remove(original.LeaseId);
    }
    internal static void ObserveTelemetryTerminal(RebirthNpcWorkExecutionSnapshot original)
    {
        if(!ThreadManager.IsMainThread()||original?.Phase!=RebirthNpcWorkExecutorPhase.Terminal)return;
        foreach(var item in held)
            if(ReferenceEquals(item.world,GameManager.Instance?.World)&&RebirthNpcSaveQuarantineScope.SameDirectory(item.directory,GameIO.GetSaveGameDir())&&
                item.telemetry.TryGetValue(original.AssignmentId,out var captured)&&ReferenceEquals(captured,original))item.telemetry.Remove(original.AssignmentId);
    }
    internal static bool TryCapture(RebirthNpcSaveQuarantineScope scope,out RebirthNpcRetainedWorkCustody receipt)
    {
        receipt=null;if(scope==null||!ThreadManager.IsMainThread())return false;
        return RebirthNpcExecutionPersistenceStore.TryCaptureRetainedWork(scope,out receipt);
    }
}

public static partial class RebirthNpcExecutionPersistenceStore
{
    internal static bool TryCaptureRetainedWork(RebirthNpcSaveQuarantineScope scope,out RebirthNpcRetainedWorkCustody receipt)
    {
        receipt=null;lock(Sync)
        {
            var workScope=loadedWorkScope;var pending=PendingByStableId;var legacy=LegacyPendingByEntity;var custody=WorkCustody;
            if(!scope.MatchesBinding(workScope?.NativeWorld as World,workScope?.SaveFilePath,loaded||pending.Count!=0||legacy.Count!=0||custody.Count!=0))return false;
            var owners=new HashSet<RebirthNpcStableId>();var saved=new HashSet<ulong>();
            foreach(var row in pending.Values)if(row.Order==RebirthNpcOrderState.Work){if(row.StableId.IsEmpty)return false;owners.Add(row.StableId);saved.Add(row.LeaseId);}
            foreach(var row in legacy.Values)if(row.Order==RebirthNpcOrderState.Work)return false; // No stable owner, preserve full boundary hold.
            if(!RebirthNpcExecutionLeaseRegistry.CaptureRetainedWorkLeases(scope,owners,out var leases,out var leaseCurrent,out var leaseDetached))return false;
            if(!RebirthNpcWorkExecutionTelemetry.CaptureRetainedTelemetry(out var telemetry,out var telemetryCurrent,out var telemetryDetached))return false;
            if(!RebirthNpcWorkCheckpointPersistenceStore.CaptureRetainedCheckpointIds(scope,owners,out var checkpoints,out var checkpointCurrent,out var checkpointDetached))return false;
            if(!RebirthNpcWorkAssignmentService.CaptureRetainedAssignments(scope,owners,checkpoints,out var assignmentCurrent,out var assignmentDetached))return false;
            foreach(var row in telemetry.Values)
            {
                if(!RebirthNpcWorkAssignmentService.TryCaptureRetainedAssignmentOwner(scope,row.AssignmentId,out var owner))return false;
                owners.Add(owner);
            }
            string image=QuarantineExecutionImage().OuterXml;
            bool Current(){lock(Sync)return ReferenceEquals(loadedWorkScope,workScope)&&ReferenceEquals(PendingByStableId,pending)&&ReferenceEquals(LegacyPendingByEntity,legacy)&&
                ReferenceEquals(WorkCustody,custody)&&QuarantineExecutionImage().OuterXml==image&&leaseCurrent()&&telemetryCurrent()&&checkpointCurrent()&&assignmentCurrent();}
            bool Detached(){lock(Sync)return !ReferenceEquals(PendingByStableId,pending)&&!ReferenceEquals(LegacyPendingByEntity,legacy)&&
                !ReferenceEquals(WorkCustody,custody)&&(workScope==null||!ReferenceEquals(loadedWorkScope,workScope))&&leaseDetached()&&telemetryDetached()&&checkpointDetached()&&assignmentDetached();}
            receipt=new RebirthNpcRetainedWorkCustody(scope,Current,Detached,leases,telemetry,owners,saved,checkpoints);return receipt.IsOriginalCurrent;
        }
    }
}

public static partial class RebirthNpcWorkCheckpointPersistenceStore
{
    internal static bool CaptureRetainedCheckpointIds(RebirthNpcSaveQuarantineScope scope,HashSet<RebirthNpcStableId> owners,
        out HashSet<ulong> ids,out Func<bool> current,out Func<bool> detached)
    {
        lock(Sync)
        {
            ids=new HashSet<ulong>();current=detached=null;
            if(!scope.MatchesBinding(boundWorld,boundPath,loaded||Loaded.Count!=0))return false;
            foreach(var row in CaptureCheckpointRows())if(row.Phase!=RebirthNpcWorkExecutorPhase.Terminal)
            {
                ids.Add(row.AssignmentId);
                if(RebirthNpcWorkAssignmentService.TryCaptureRetainedAssignmentOwner(scope,row.AssignmentId,out var owner))owners.Add(owner);
            }
            var original=Loaded;var world=boundWorld;string path=boundPath,image=RecoveryRowsImage(Loaded);
            current=()=>{lock(Sync)return ReferenceEquals(Loaded,original)&&ReferenceEquals(boundWorld,world)&&boundPath==path&&RecoveryRowsImage(Loaded)==image;};
            detached=()=>{lock(Sync)return !ReferenceEquals(Loaded,original)&&(world==null||!ReferenceEquals(boundWorld,world));};return true;
        }
    }
}

public static partial class RebirthNpcWorkAssignmentService
{
    internal static bool CaptureRetainedAssignments(RebirthNpcSaveQuarantineScope scope,HashSet<RebirthNpcStableId> owners,HashSet<ulong> ids,
        out Func<bool> current,out Func<bool> detached)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,assignmentRecordScope);
            var records=ById;var original=assignmentRecordScope;string image=AssignmentImage().OuterXml;
            foreach(var pair in records)if(!IsTerminal(pair.Value.Status)){owners.Add(pair.Value.NpcId);ids.Add(pair.Key);}
            current=()=>{lock(Sync)return ReferenceEquals(ById,records)&&ReferenceEquals(assignmentRecordScope,original)&&AssignmentImage().OuterXml==image;};
            detached=()=>{lock(Sync)return !ReferenceEquals(ById,records)&&(original==null||!ReferenceEquals(assignmentRecordScope,original));};return true;
        }
    }
    internal static bool TryCaptureRetainedAssignmentOwner(RebirthNpcSaveQuarantineScope scope,ulong assignment,out RebirthNpcStableId owner)
    {
        lock(Sync)
        {
            owner=default(RebirthNpcStableId);
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,assignmentRecordScope);
            if(!ById.TryGetValue(assignment,out var row)||row.NpcId.IsEmpty)return false;
            owner=row.NpcId;return true;
        }
    }
}

public static partial class RebirthNpcExecutionLeaseRegistry
{
    internal static bool CaptureRetainedWorkLeases(RebirthNpcSaveQuarantineScope scope,HashSet<RebirthNpcStableId> owners,
        out Dictionary<ulong,RebirthNpcExecutionLease> leases,out Func<bool> current,out Func<bool> detached)
    {
        lock(Sync)
        {
            leases=new Dictionary<ulong,RebirthNpcExecutionLease>();current=detached=null;
            if(UnknownWorkCustody.Count!=0)return false;
            foreach(var pair in ByLeaseId)if(pair.Value.Order==RebirthNpcOrderState.Work&&pair.Value.Status==RebirthNpcExecutionLeaseStatus.Active)
            {
                if(!NativeWorkOwners.TryGetValue(pair.Key,out var owner)||owner?.Scope==null||owner.StableOwner.IsEmpty||
                    !scope.MatchesBinding(owner.Scope.NativeWorld as World,owner.Scope.SaveFilePath,true)||!ReferenceEquals(owner.OriginalLease,pair.Value))return false;
                owners.Add(owner.StableOwner);leases.Add(pair.Key,pair.Value);
            }
            var original=ByLeaseId;var originalOwners=NativeWorkOwners;long version=revision;
            current=()=>{lock(Sync)return ReferenceEquals(ByLeaseId,original)&&ReferenceEquals(NativeWorkOwners,originalOwners)&&revision==version;};
            detached=()=>{lock(Sync)return !ReferenceEquals(ByLeaseId,original)&&!ReferenceEquals(NativeWorkOwners,originalOwners);};return true;
        }
    }
}

public static partial class RebirthNpcWorkExecutionTelemetry
{
    internal static bool CaptureRetainedTelemetry(out Dictionary<ulong,RebirthNpcWorkExecutionSnapshot> rows,out Func<bool> current,out Func<bool> detached)
    {
        lock(Sync)
        {
            rows=new Dictionary<ulong,RebirthNpcWorkExecutionSnapshot>();
            foreach(var pair in Active)if(pair.Value.Phase!=RebirthNpcWorkExecutorPhase.Terminal)rows.Add(pair.Key,pair.Value);
            var original=Active;long version=revision;string image=QuarantineTelemetryImage().OuterXml;
            current=()=>{lock(Sync)return ReferenceEquals(Active,original)&&revision==version&&QuarantineTelemetryImage().OuterXml==image;};
            detached=()=>{lock(Sync)return !ReferenceEquals(Active,original);};return true;
        }
    }
}
