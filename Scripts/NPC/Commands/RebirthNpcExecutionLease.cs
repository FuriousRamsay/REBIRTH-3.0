using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

public enum RebirthNpcExecutionLeaseStatus : byte
{
    Active = 0,
    Completed = 1,
    Interrupted = 2,
    Released = 3,
    Failed = 4
}

public sealed class RebirthNpcExecutionLease
{
    public ulong LeaseId { get; internal set; }
    public ulong CommandId { get; internal set; }
    public int TargetEntityId { get; internal set; }
    public int SubjectEntityId { get; internal set; }
    public Vector3 TargetPosition { get; internal set; }
    public bool HasTargetPosition { get; internal set; }
    public string IssuerId { get; internal set; }
    public RebirthNpcCommandKind CommandKind { get; internal set; }
    public RebirthNpcOrderState Order { get; internal set; }
    public uint AcceptedRevision { get; internal set; }
    public RebirthNpcExecutionLeaseStatus Status { get; internal set; }
    public string Detail { get; internal set; }
    public long StartedUtcTicks { get; internal set; }
    public long UpdatedUtcTicks { get; internal set; }

    internal RebirthNpcExecutionLease Clone()
    {
        return (RebirthNpcExecutionLease)MemberwiseClone();
    }
}

public static class RebirthNpcExecutionLeaseRegistry
{
    private sealed class NativeWorkOwner
    {
        internal readonly RebirthNpcExecutionWorkSaveScope Scope;
        internal readonly EntityRebirthNPC Actor;
        internal readonly RebirthNpcRuntimeState Runtime;
        internal readonly RebirthNpcStableId StableOwner;
        internal readonly string ProfileId;
        internal readonly RebirthNpcExecutionLease OriginalLease;
        internal readonly Vector3 GuardPosition;
        internal readonly bool HasGuardPosition;
        internal NativeWorkOwner(RebirthNpcExecutionWorkSaveScope scope, EntityRebirthNPC actor,
            RebirthNpcRuntimeState runtime, RebirthNpcExecutionLease lease)
        {
            Scope=scope; Actor=actor; Runtime=runtime; StableOwner=runtime.StableId;
            ProfileId=runtime.ProfileId; OriginalLease=lease==null?null:lease.Clone();
            GuardPosition=runtime.GuardPosition; HasGuardPosition=runtime.HasGuardPosition;
        }
    }
    private static readonly Dictionary<ulong, NativeWorkOwner> NativeWorkOwners =
        new Dictionary<ulong, NativeWorkOwner>();
    private static readonly HashSet<ulong> UnknownWorkCustody = new HashSet<ulong>();

    private static bool TryCaptureOriginalNativeWorkOwner(EntityRebirthNPC npc, out NativeWorkOwner owner)
    {
        owner=null;
        RebirthNpcExecutionWorkSaveScope scope;
        if(npc==null || !RebirthNpcExecutionPersistenceStore.TryGetCurrentWorkSaveScope(out scope))return false;
        var runtime=npc.RebirthRuntimeState;
        if(runtime==null || runtime.StableId.IsEmpty)return false;
        var world=GameManager.Instance==null?null:GameManager.Instance.World;
        if(world==null || !ReferenceEquals(world,scope.NativeWorld) ||
            !ReferenceEquals(world.GetEntity(npc.entityId),npc) ||
            !ReferenceEquals(npc.RebirthRuntimeState,runtime) ||
            !RebirthNpcExecutionPersistenceStore.IsCurrentWorkSaveScope(scope))return false;
        owner=new NativeWorkOwner(scope,npc,runtime,null);
        return true;
    }

    private static bool NativeWorkOwnerIsCurrent(NativeWorkOwner owner, int entityId)
    {
        if(owner==null || owner.OriginalLease==null ||
            !RebirthNpcExecutionPersistenceStore.IsCurrentWorkSaveScope(owner.Scope))return false;
        var world=GameManager.Instance==null?null:GameManager.Instance.World;
        if(world==null || !ReferenceEquals(world,owner.Scope.NativeWorld))return false;
        var current=world.GetEntity(entityId);
        var runtime=owner.Actor.RebirthRuntimeState;
        return ReferenceEquals(current,owner.Actor) && ReferenceEquals(runtime,owner.Runtime) &&
            runtime!=null && runtime.StableId.Equals(owner.StableOwner) &&
            string.Equals(runtime.ProfileId,owner.ProfileId,StringComparison.Ordinal) &&
            owner.OriginalLease.TargetEntityId==entityId &&
            ReferenceEquals(GameManager.Instance==null?null:GameManager.Instance.World,world) &&
            RebirthNpcExecutionPersistenceStore.IsCurrentWorkSaveScope(owner.Scope);
    }

    public static bool IsCurrentWorldWorkLeaseOwner(int entityId, ulong leaseId)
    {
        NativeWorkOwner owner; RebirthNpcExecutionLease original;
        lock(Sync)
        {
            if(!ByLeaseId.TryGetValue(leaseId,out original) ||
                original.Order!=RebirthNpcOrderState.Work ||
                !NativeWorkOwners.TryGetValue(leaseId,out owner))return false;
        }
        if(!NativeWorkOwnerIsCurrent(owner,entityId))return false;
        lock(Sync)
        {
            RebirthNpcExecutionLease active;
            return ByLeaseId.TryGetValue(leaseId,out var retained) && ReferenceEquals(retained,original) &&
                ActiveByEntity.TryGetValue(entityId,out active) && ReferenceEquals(active,original);
        }
    }

    private static void QuarantineNonCurrentWorkIndex(int entityId)
    {
        RebirthNpcExecutionLease original;
        lock(Sync)
        {
            if(!ActiveByEntity.TryGetValue(entityId,out original) ||
                original.Order!=RebirthNpcOrderState.Work)return;
        }
        if(IsCurrentWorldWorkLeaseOwner(entityId,original.LeaseId))return;
        lock(Sync)
        {
            RebirthNpcExecutionLease current;
            if(!ActiveByEntity.TryGetValue(entityId,out current) || !ReferenceEquals(current,original))return;
            // Current lookup detachment only: original custody/status/detail never terminalized.
            ActiveByEntity.Remove(entityId);
            if(!NativeWorkOwners.ContainsKey(original.LeaseId))UnknownWorkCustody.Add(original.LeaseId);
            revision++;
        }
    }

    public static bool IsCurrentWorldHeldWorkEntity(int entityId)
    {
        QuarantineNonCurrentWorkIndex(entityId);
        RebirthNpcExecutionLease current;
        lock(Sync)
            if(!ActiveByEntity.TryGetValue(entityId,out current) ||
                current.Order!=RebirthNpcOrderState.Work)return false;
        return IsCurrentWorldWorkLeaseOwner(entityId,current.LeaseId);
    }

    public static bool IsHeldWorkLease(ulong leaseId)
    {
        lock(Sync)
        {
            RebirthNpcExecutionLease original;
            return UnknownWorkCustody.Contains(leaseId) || NativeWorkOwners.ContainsKey(leaseId) ||
                (ByLeaseId.TryGetValue(leaseId,out original) && original.Order==RebirthNpcOrderState.Work);
        }
    }

    public static bool TryGetWorkCustody(ulong leaseId, out RebirthNpcWorkCustodyView view)
    {
        view=null; NativeWorkOwner owner; RebirthNpcExecutionLease original;
        lock(Sync)
        {
            if(!ByLeaseId.TryGetValue(leaseId,out original) || !IsHeldWorkLease(leaseId))return false;
            NativeWorkOwners.TryGetValue(leaseId,out owner);
        }
        if(owner==null)
        {
            view=new RebirthNpcWorkCustodyView(leaseId,null,default(RebirthNpcStableId),false,
                RebirthNpcWorkCustodyDisposition.QuarantinedUnknownOrigin);
            return true;
        }
        bool current=IsCurrentWorldWorkLeaseOwner(original.TargetEntityId,leaseId);
        view=new RebirthNpcWorkCustodyView(leaseId,owner.Scope,owner.StableOwner,true,current?
            RebirthNpcWorkCustodyDisposition.HeldOriginalCurrentOwner:
            RebirthNpcWorkCustodyDisposition.QuarantinedForeignScope);
        return true;
    }

    internal static bool TryExportOriginalWorkForScope(ulong leaseId,
        RebirthNpcExecutionWorkSaveScope scope, out RebirthNpcPersistedExecution record)
    {
        record=null; NativeWorkOwner owner;
        lock(Sync)
            if(!NativeWorkOwners.TryGetValue(leaseId,out owner))return false;
        if(!ReferenceEquals(scope,owner.Scope) ||
            !RebirthNpcExecutionPersistenceStore.IsCurrentWorkSaveScope(scope) ||
            owner.StableOwner.IsEmpty || owner.OriginalLease==null)return false;
        var original=owner.OriginalLease;
        record=new RebirthNpcPersistedExecution {
            EntityId=original.TargetEntityId, StableId=owner.StableOwner, LeaseId=original.LeaseId,
            CommandId=original.CommandId, SubjectEntityId=original.SubjectEntityId,
            IssuerId=original.IssuerId, CommandKind=original.CommandKind, Order=original.Order,
            TargetPosition=original.TargetPosition, HasTargetPosition=original.HasTargetPosition,
            GuardPosition=owner.GuardPosition, HasGuardPosition=owner.HasGuardPosition,
            StartedUtcTicks=original.StartedUtcTicks
        };
        return true;
    }

    private const int MaxHistoryRecords = 256;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthNpcExecutionLease> ActiveByEntity =
        new Dictionary<int, RebirthNpcExecutionLease>();
    private static readonly Dictionary<ulong, RebirthNpcExecutionLease> ByLeaseId =
        new Dictionary<ulong, RebirthNpcExecutionLease>();
    private static readonly Queue<ulong> HistoryOrder = new Queue<ulong>();
    private static ulong nextLeaseId = 1UL;
    private static long revision;
    public static long Revision { get { lock (Sync) return revision; } }

    public static RebirthNpcExecutionLease BeginOrReplace(EntityRebirthNPC npc,
        RebirthNpcCommandRecord command, uint acceptedRevision)
    {
        // Release hold: preserve original work custody; no native effects.
        if(command!=null && (RebirthNpcWorkReleaseGate.Hold(command.Request.Kind)||RebirthNpcWorkReleaseGate.HoldEntity(npc==null?0:npc.entityId)))return null;

        if (npc == null) throw new ArgumentNullException(nameof(npc));
        if (command == null) throw new ArgumentNullException(nameof(command));

        RebirthNpcOrderState order = NormalizeOrder(command.Request.Kind);
        NativeWorkOwner originalWorkOwner=null;
        if(order==RebirthNpcOrderState.Work && !TryCaptureOriginalNativeWorkOwner(npc,out originalWorkOwner))return null;
        lock (Sync)
        {
            InterruptActiveLocked(npc.entityId,
                "Interrupted by accepted command " + command.Request.CommandId +
                " (" + command.Request.Kind + ").");

            if (order == RebirthNpcOrderState.None)
                return null;

            ulong leaseId = nextLeaseId++;
            if (leaseId == 0UL) leaseId = nextLeaseId++;
            long now = DateTime.UtcNow.Ticks;
            RebirthNpcExecutionLease lease = new RebirthNpcExecutionLease
            {
                LeaseId = leaseId,
                CommandId = command.Request.CommandId,
                TargetEntityId = npc.entityId,
                SubjectEntityId = command.Request.SubjectEntityId,
                TargetPosition = command.Request.TargetPosition,
                HasTargetPosition = command.Request.HasTargetPosition,
                IssuerId = command.Request.IssuerId ?? string.Empty,
                CommandKind = command.Request.Kind,
                Order = order,
                AcceptedRevision = acceptedRevision,
                Status = RebirthNpcExecutionLeaseStatus.Active,
                Detail = "Order accepted; awaiting execution-controller completion or interruption.",
                StartedUtcTicks = now,
                UpdatedUtcTicks = now
            };
            ActiveByEntity[npc.entityId] = lease;
            ByLeaseId[leaseId] = lease;
            if(originalWorkOwner!=null)NativeWorkOwners[leaseId]=new NativeWorkOwner(originalWorkOwner.Scope,npc,originalWorkOwner.Runtime,lease);
            RebirthNpcExecutionLeaseDiagnostics.RecordStarted();
            revision++;
            return lease.Clone();
        }
    }

    public static bool TryRestore(EntityRebirthNPC npc, ulong persistedLeaseId, ulong commandId,
        int subjectEntityId, Vector3 targetPosition, bool hasTargetPosition, string issuerId,
        RebirthNpcCommandKind commandKind, RebirthNpcOrderState order, uint acceptedRevision,
        long startedUtcTicks, string detail, out RebirthNpcExecutionLease restored)
    {
        restored = null;
        if (npc == null || order == RebirthNpcOrderState.None) return false;
        QuarantineNonCurrentWorkIndex(npc.entityId);
        if(!RebirthNpcWorkReleaseGate.Enabled && order==RebirthNpcOrderState.Work)return false; // No new native Work activation or DTO-origin receipt.
        lock (Sync)
        {
            RebirthNpcExecutionLease existing;
            if (ActiveByEntity.TryGetValue(npc.entityId, out existing))
            {
                restored = existing.Clone();
                return existing.Order == order;
            }

            ulong leaseId = persistedLeaseId;
            if (leaseId == 0UL || ByLeaseId.ContainsKey(leaseId))
            {
                leaseId = nextLeaseId++;
                if (leaseId == 0UL) leaseId = nextLeaseId++;
            }
            else if (leaseId >= nextLeaseId)
            {
                nextLeaseId = leaseId + 1UL;
                if (nextLeaseId == 0UL) nextLeaseId = 1UL;
            }

            long now = DateTime.UtcNow.Ticks;
            RebirthNpcExecutionLease lease = new RebirthNpcExecutionLease
            {
                LeaseId = leaseId,
                CommandId = commandId,
                TargetEntityId = npc.entityId,
                SubjectEntityId = subjectEntityId,
                TargetPosition = targetPosition,
                HasTargetPosition = hasTargetPosition,
                IssuerId = issuerId ?? string.Empty,
                CommandKind = commandKind,
                Order = order,
                AcceptedRevision = acceptedRevision,
                Status = RebirthNpcExecutionLeaseStatus.Active,
                Detail = detail ?? "Restored from authoritative NPC persistence.",
                StartedUtcTicks = startedUtcTicks > 0L ? startedUtcTicks : now,
                UpdatedUtcTicks = now
            };
            ActiveByEntity[npc.entityId] = lease;
            ByLeaseId[leaseId] = lease;
            RebirthNpcExecutionLeaseDiagnostics.RecordStarted();
            revision++;
            restored = lease.Clone();
            return true;
        }
    }

    public static bool TryComplete(int entityId, ulong leaseId, uint observedRevision, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return false;

        lock (Sync)
        {
            RebirthNpcExecutionLease lease;
            if (!ActiveByEntity.TryGetValue(entityId, out lease) || lease.LeaseId != leaseId)
                return false;
            if (observedRevision != 0U && observedRevision < lease.AcceptedRevision)
                return false;
            TerminalizeLocked(lease, RebirthNpcExecutionLeaseStatus.Completed,
                detail ?? "Execution controller reported completion.");
            RebirthNpcExecutionLeaseDiagnostics.RecordCompleted();
            return true;
        }
    }

    public static bool TryRelease(int entityId, ulong leaseId, uint observedRevision, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return false;

        lock (Sync)
        {
            RebirthNpcExecutionLease lease;
            if (!ActiveByEntity.TryGetValue(entityId, out lease) || lease.LeaseId != leaseId)
                return false;
            if (observedRevision != 0U && observedRevision < lease.AcceptedRevision)
                return false;
            TerminalizeLocked(lease, RebirthNpcExecutionLeaseStatus.Released,
                detail ?? "Execution controller abandoned activity.");
            RebirthNpcExecutionLeaseDiagnostics.RecordReleased();
            return true;
        }
    }

    public static bool TryFail(int entityId, ulong leaseId, uint observedRevision, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return false;

        lock (Sync)
        {
            RebirthNpcExecutionLease lease;
            if (!ActiveByEntity.TryGetValue(entityId, out lease) || lease.LeaseId != leaseId)
                return false;
            if (observedRevision != 0U && observedRevision < lease.AcceptedRevision)
                return false;
            TerminalizeLocked(lease, RebirthNpcExecutionLeaseStatus.Failed,
                detail ?? "Execution controller reported failure.");
            RebirthNpcExecutionLeaseDiagnostics.RecordFailed();
            return true;
        }
    }

    public static bool TryGetActive(int entityId, out RebirthNpcExecutionLease lease)
    {
        QuarantineNonCurrentWorkIndex(entityId);
        lock(Sync)
        {
            RebirthNpcExecutionLease current;
            if(!ActiveByEntity.TryGetValue(entityId,out current)){lease=null;return false;}
            lease=current.Clone();return true;
        }
    }

    public static void OnAuthoritativeOrderChanged(int entityId, RebirthNpcOrderState order,
        uint revision, ulong sourceCommandId)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return;

        lock (Sync)
        {
            RebirthNpcExecutionLease active;
            if (!ActiveByEntity.TryGetValue(entityId, out active)) return;
            if (sourceCommandId != 0UL && active.CommandId == sourceCommandId) return;
            if (revision <= active.AcceptedRevision && order == active.Order) return;
            TerminalizeLocked(active, RebirthNpcExecutionLeaseStatus.Interrupted,
                "Authoritative order changed to " + order + " at revision " + revision + ".");
            RebirthNpcExecutionLeaseDiagnostics.RecordInterrupted();
        }
    }

    public static void ReleaseForEntity(int entityId, string reason)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return;

        lock (Sync)
        {
            RebirthNpcExecutionLease active;
            if (!ActiveByEntity.TryGetValue(entityId, out active)) return;
            TerminalizeLocked(active, RebirthNpcExecutionLeaseStatus.Released,
                reason ?? "Target left the active runtime.");
            RebirthNpcExecutionLeaseDiagnostics.RecordReleased();
        }
    }

    public static RebirthNpcExecutionLease[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcExecutionLease[] result = new RebirthNpcExecutionLease[ByLeaseId.Count];
            int index = 0;
            foreach (RebirthNpcExecutionLease lease in ByLeaseId.Values)
                result[index++] = lease.Clone();
            Array.Sort(result, (left, right) => left.LeaseId.CompareTo(right.LeaseId));
            return result;
        }
    }

    // Dispatch needs active custody only; the public snapshot retains terminal history.
    internal static RebirthNpcExecutionLease[] GetActiveDispatchSnapshot()
    {
        lock (Sync)
        {
            int count = 0;
            foreach (RebirthNpcExecutionLease lease in ByLeaseId.Values)
                if (lease.Status == RebirthNpcExecutionLeaseStatus.Active) count++;
            if (count == 0) return Array.Empty<RebirthNpcExecutionLease>();
            RebirthNpcExecutionLease[] result = new RebirthNpcExecutionLease[count];
            int index = 0;
            foreach (RebirthNpcExecutionLease lease in ByLeaseId.Values)
                if (lease.Status == RebirthNpcExecutionLeaseStatus.Active)
                    result[index++] = lease.Clone();
            Array.Sort(result, (left, right) => left.LeaseId.CompareTo(right.LeaseId));
            return result;
        }
    }

    private static void InterruptActiveLocked(int entityId, string detail)
    {
        RebirthNpcExecutionLease active;
        if (!ActiveByEntity.TryGetValue(entityId, out active)) return;
        TerminalizeLocked(active, RebirthNpcExecutionLeaseStatus.Interrupted, detail);
        RebirthNpcExecutionLeaseDiagnostics.RecordInterrupted();
    }

    private static void TerminalizeLocked(RebirthNpcExecutionLease lease,
        RebirthNpcExecutionLeaseStatus status, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.Hold(lease.Order))return;

        lease.Status = status;
        lease.Detail = detail ?? string.Empty;
        lease.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
        revision++;
        ActiveByEntity.Remove(lease.TargetEntityId);
        HistoryOrder.Enqueue(lease.LeaseId);
        TrimHistoryLocked();
    }

    private static void TrimHistoryLocked()
    {
        while (HistoryOrder.Count > MaxHistoryRecords)
        {
            ulong oldest = HistoryOrder.Dequeue();
            RebirthNpcExecutionLease lease;
            if (ByLeaseId.TryGetValue(oldest, out lease) &&
                lease.Status != RebirthNpcExecutionLeaseStatus.Active)
                ByLeaseId.Remove(oldest);
        }
    }

    private static RebirthNpcOrderState NormalizeOrder(RebirthNpcCommandKind kind)
    {
        switch (kind)
        {
            case RebirthNpcCommandKind.Follow: return RebirthNpcOrderState.Follow;
            case RebirthNpcCommandKind.Stay: return RebirthNpcOrderState.Stay;
            case RebirthNpcCommandKind.Guard: return RebirthNpcOrderState.Guard;
            case RebirthNpcCommandKind.Patrol: return RebirthNpcOrderState.Patrol;
            case RebirthNpcCommandKind.Work: return RebirthNpcOrderState.Work;
            case RebirthNpcCommandKind.Travel: return RebirthNpcOrderState.Travel;
            case RebirthNpcCommandKind.Mission: return RebirthNpcOrderState.Mission;
            default: return RebirthNpcOrderState.None;
        }
    }
}

public static class RebirthNpcExecutionLeaseDiagnostics
{
    private static long started, completed, interrupted, released, failed;
    internal static void RecordStarted() { started++; }
    internal static void RecordCompleted() { completed++; }
    internal static void RecordInterrupted() { interrupted++; }
    internal static void RecordReleased() { released++; }
    internal static void RecordFailed() { failed++; }

    public static string GetReport()
    {
        RebirthNpcExecutionLease[] leases = RebirthNpcExecutionLeaseRegistry.GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC] execution leases tracked=").Append(leases.Length)
            .Append(" started=").Append(started)
            .Append(" completed=").Append(completed)
            .Append(" interrupted=").Append(interrupted)
            .Append(" released=").Append(released)
            .Append(" failed=").Append(failed).AppendLine();
        for (int i = Math.Max(0, leases.Length - 25); i < leases.Length; i++)
        {
            RebirthNpcExecutionLease lease = leases[i];
            builder.Append("  lease=").Append(lease.LeaseId)
                .Append(" command=").Append(lease.CommandId)
                .Append(" entity=").Append(lease.TargetEntityId)
                .Append(" issuer=").Append(lease.IssuerId)
                .Append(" order=").Append(lease.Order)
                .Append(" revision=").Append(lease.AcceptedRevision)
                .Append(" status=").Append(lease.Status)
                .Append(" detail=").Append(lease.Detail).AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}
