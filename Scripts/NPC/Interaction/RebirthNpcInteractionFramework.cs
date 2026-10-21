using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

#nullable disable

[Flags]
public enum RebirthNpcInteractionPermissions : ushort
{
    None = 0,
    Inspect = 1 << 0,
    Dialogue = 1 << 1,
    Trade = 1 << 2,
    Inventory = 1 << 3,
    Equipment = 1 << 4,
    Command = 1 << 5,
    Hire = 1 << 6,
    Dismiss = 1 << 7,
    AssignWork = 1 << 8,
    Teach = 1 << 9,
    Admin = 1 << 15
}

public enum RebirthNpcInteractionAction : byte
{
    Inspect = 0,
    Dialogue = 1,
    Trade = 2,
    Inventory = 3,
    Equipment = 4,
    Command = 5,
    Hire = 6,
    Dismiss = 7,
    AssignWork = 8,
    Teach = 9
}

public enum RebirthNpcInteractionResult : byte
{
    Allowed = 0,
    InvalidRequest = 1,
    TargetUnavailable = 2,
    CapabilityDenied = 3,
    PermissionDenied = 4,
    SessionExpired = 5,
    RevisionConflict = 6,
    AlreadyHired = 7,
    NotHired = 8,
    OwnershipConflict = 9
}

public sealed class RebirthNpcInteractionContext
{
    public string ActorId { get; }
    public RebirthNpcStableId NpcId { get; }
    public RebirthNpcInteractionPermissions Permissions { get; }
    public uint RuntimeRevision { get; }
    public string SessionKey { get; }
    public long ExpiresClockTicks { get; }

    internal RebirthNpcInteractionContext(string actorId, RebirthNpcStableId npcId,
        RebirthNpcInteractionPermissions permissions, uint runtimeRevision,
        string sessionKey, long expiresClockTicks)
    {
        ActorId = actorId;
        NpcId = npcId;
        Permissions = permissions;
        RuntimeRevision = runtimeRevision;
        SessionKey = sessionKey;
        ExpiresClockTicks = expiresClockTicks;
    }
}

public interface IRebirthNpcInteractionPermissionProvider
{
    RebirthNpcInteractionPermissions Resolve(string actorId, RebirthNpcStableId npcId,
        RebirthNpcRuntimeState runtime);
}

public static class RebirthNpcInteractionPermissionService
{
    private static readonly object Sync = new object();
    private static readonly List<IRebirthNpcInteractionPermissionProvider> Providers =
        new List<IRebirthNpcInteractionPermissionProvider>();

    public static void Register(IRebirthNpcInteractionPermissionProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        lock (Sync) if (!Providers.Contains(provider)) Providers.Add(provider);
    }

    public static void Unregister(IRebirthNpcInteractionPermissionProvider provider)
    {
        if (provider == null) return;
        lock (Sync) Providers.Remove(provider);
    }

    public static RebirthNpcInteractionPermissions Resolve(string actorId,
        RebirthNpcStableId npcId, RebirthNpcRuntimeState runtime)
    {
        RebirthNpcInteractionPermissions result = RebirthNpcInteractionPermissions.None;
        lock (Sync)
        {
            for (int i = 0; i < Providers.Count; i++)
            {
                try { result |= Providers[i].Resolve(actorId, npcId, runtime); }
                catch (Exception ex)
                {
                    Log.Warning("[REBIRTH NPC Interaction] Permission provider failed: " +
                        ex.GetType().Name + ": " + ex.Message);
                }
            }
        }
        return result;
    }

    public static int ProviderCount { get { lock (Sync) return Providers.Count; } }
}

public static class RebirthNpcInteractionSessionService
{
    private sealed class SessionRecord
    {
        public RebirthNpcInteractionContext Context;
        public bool InventoryAccessing;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<string, SessionRecord> Sessions =
        new Dictionary<string, SessionRecord>(StringComparer.Ordinal);
    private static long opened;
    private static long accepted;
    private static long rejected;
    private static long expired;

    public static RebirthNpcInteractionResult Open(string actorId, RebirthNpcStableId npcId,
        TimeSpan lifetime, out RebirthNpcInteractionContext context)
    {
        context = null;
        if(RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return Reject(RebirthNpcInteractionResult.TargetUnavailable);
        actorId = (actorId ?? string.Empty).Trim();
        if (actorId.Length == 0 || npcId.IsEmpty || lifetime <= TimeSpan.Zero ||
            lifetime > TimeSpan.FromMinutes(2))
            return Reject(RebirthNpcInteractionResult.InvalidRequest);

        int entityId;
        RebirthNpcRuntimeState runtime;
        if (!RebirthNpcRuntimeRegistry.TryGetEntityId(npcId, out entityId) ||
            !RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime))
            return Reject(RebirthNpcInteractionResult.TargetUnavailable);
        if(GameManager.Instance?.World?.GetEntity(entityId) is EntityRebirthNPC recovering&&RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(recovering))return Reject(RebirthNpcInteractionResult.TargetUnavailable);

        RebirthNpcInteractionPermissions permissions =
            RebirthNpcInteractionPermissionService.Resolve(actorId, npcId, runtime);
        string key = "rbnpc-interaction:" + Guid.NewGuid().ToString("N");
        context = new RebirthNpcInteractionContext(actorId, npcId, permissions,
            runtime.Revision, key, NowTicks() + lifetime.Ticks);
        lock (Sync) Sessions[key] = new SessionRecord { Context = context };
        Interlocked.Increment(ref opened);
        return RebirthNpcInteractionResult.Allowed;
    }

    public static RebirthNpcInteractionResult Authorize(string sessionKey,
        RebirthNpcInteractionAction action, out RebirthNpcInteractionContext context)
    {
        return Authorize(sessionKey, null, action, out context);
    }

    public static RebirthNpcInteractionResult Authorize(string sessionKey, string authenticatedActorId,
        RebirthNpcInteractionAction action, out RebirthNpcInteractionContext context)
    {
        context = null;
        sessionKey = (sessionKey ?? string.Empty).Trim();
        if (sessionKey.Length == 0) return Reject(RebirthNpcInteractionResult.InvalidRequest);
        bool inventoryOpened = false;
        RebirthNpcStableId changedNpc = default(RebirthNpcStableId);
        lock (Sync)
        {
            SessionRecord record;
            if (!Sessions.TryGetValue(sessionKey, out record))
                return Reject(RebirthNpcInteractionResult.PermissionDenied);
            if (record.Context.ExpiresClockTicks <= NowTicks())
            {
                Sessions.Remove(sessionKey);
                Interlocked.Increment(ref expired);
                return Reject(RebirthNpcInteractionResult.SessionExpired);
            }
            if (!string.IsNullOrWhiteSpace(authenticatedActorId) &&
                !string.Equals(record.Context.ActorId, authenticatedActorId.Trim(), StringComparison.Ordinal))
                return Reject(RebirthNpcInteractionResult.PermissionDenied);

            int entityId;
            RebirthNpcRuntimeState runtime;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(record.Context.NpcId, out entityId) ||
                !RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime))
                return Reject(RebirthNpcInteractionResult.TargetUnavailable);
            RebirthNpcInteractionPermissions currentPermissions =
                RebirthNpcInteractionPermissionService.Resolve(record.Context.ActorId, record.Context.NpcId, runtime);
            RebirthNpcInteractionPermissions required = RequiredPermission(action);
            if ((currentPermissions & required) != required &&
                (currentPermissions & RebirthNpcInteractionPermissions.Admin) == 0)
                return Reject(RebirthNpcInteractionResult.PermissionDenied);
            context = new RebirthNpcInteractionContext(record.Context.ActorId, record.Context.NpcId,
                currentPermissions, runtime.Revision, record.Context.SessionKey, record.Context.ExpiresClockTicks);
            record.Context = context;
            if (action == RebirthNpcInteractionAction.Inventory && !record.InventoryAccessing)
            {
                record.InventoryAccessing = true;
                inventoryOpened = true;
                changedNpc = record.Context.NpcId;
            }
        }
        if (inventoryOpened) NotifyInventoryAccessChanged(changedNpc, "companion inventory access opened");
        Interlocked.Increment(ref accepted);
        return RebirthNpcInteractionResult.Allowed;
    }

    public static bool IsInventoryAccessing(RebirthNpcStableId npcId)
    {
        if (npcId.IsEmpty) return false;
        lock (Sync)
        {
            PruneExpiredLocked();
            foreach (SessionRecord record in Sessions.Values)
                if (record.InventoryAccessing && record.Context != null && record.Context.NpcId == npcId)
                    return true;
        }
        return false;
    }

    internal static bool CanRecoverNpcStores(IEnumerable<RebirthNpcStableId> affected,out string error)
    {
        error=string.Empty;var ids=new HashSet<RebirthNpcStableId>(affected??new RebirthNpcStableId[0]);
        lock(Sync)
        {
            foreach(var session in Sessions.Values)
                if(session.Context!=null&&ids.Contains(session.Context.NpcId))
                {error="NPC interaction session still references the current generation";return false;}
        }
        // No session close/prune/inventory notification is an admission shortcut.
        return true;
    }

    public static void Close(string sessionKey) { Close(sessionKey, null); }

    public static void Close(string sessionKey, string authenticatedActorId)
    {
        if (string.IsNullOrEmpty(sessionKey)) return;
        RebirthNpcStableId changedNpc = default(RebirthNpcStableId);
        bool notify = false;
        lock (Sync)
        {
            SessionRecord record;
            if (Sessions.TryGetValue(sessionKey, out record))
            {
                if (!string.IsNullOrWhiteSpace(authenticatedActorId) &&
                    (record.Context == null || !string.Equals(record.Context.ActorId, authenticatedActorId.Trim(), StringComparison.Ordinal)))
                    return;
                notify = record.InventoryAccessing;
                if (record.Context != null) changedNpc = record.Context.NpcId;
                Sessions.Remove(sessionKey);
            }
        }
        if (notify) NotifyInventoryAccessChanged(changedNpc, "companion inventory access closed");
    }

    public static void Reset()
    {
        lock (Sync) Sessions.Clear();
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            PruneExpiredLocked();
            return "[REBIRTH NPC Interaction] sessions=" + Sessions.Count +
                " providers=" + RebirthNpcInteractionPermissionService.ProviderCount +
                " opened=" + Interlocked.Read(ref opened) +
                " accepted=" + Interlocked.Read(ref accepted) +
                " rejected=" + Interlocked.Read(ref rejected) +
                " expired=" + Interlocked.Read(ref expired);
        }
    }

    private static void NotifyInventoryAccessChanged(RebirthNpcStableId npcId, string reason)
    {
        int entityId;
        RebirthNpcRuntimeState runtime;
        if (npcId.IsEmpty || !RebirthNpcRuntimeRegistry.TryGetEntityId(npcId, out entityId) ||
            !RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime)) return;
        Entity entity = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetEntity(entityId) : null;
        Vector3 position = entity != null ? entity.position : Vector3.zero;
        RemoteResourceLiveSync.NotifySourceChanged("N:" + npcId.ToString(), position, reason);
    }

    private static RebirthNpcInteractionPermissions RequiredPermission(RebirthNpcInteractionAction action)
    {
        switch (action)
        {
            case RebirthNpcInteractionAction.Inspect: return RebirthNpcInteractionPermissions.Inspect;
            case RebirthNpcInteractionAction.Dialogue: return RebirthNpcInteractionPermissions.Dialogue;
            case RebirthNpcInteractionAction.Trade: return RebirthNpcInteractionPermissions.Trade;
            case RebirthNpcInteractionAction.Inventory: return RebirthNpcInteractionPermissions.Inventory;
            case RebirthNpcInteractionAction.Equipment: return RebirthNpcInteractionPermissions.Equipment;
            case RebirthNpcInteractionAction.Command: return RebirthNpcInteractionPermissions.Command;
            case RebirthNpcInteractionAction.Hire: return RebirthNpcInteractionPermissions.Hire;
            case RebirthNpcInteractionAction.Dismiss: return RebirthNpcInteractionPermissions.Dismiss;
            case RebirthNpcInteractionAction.AssignWork: return RebirthNpcInteractionPermissions.AssignWork;
            case RebirthNpcInteractionAction.Teach: return RebirthNpcInteractionPermissions.Teach;
            default: return RebirthNpcInteractionPermissions.None;
        }
    }

    private static void PruneExpiredLocked()
    {
        long now = NowTicks();
        List<string> remove = null;
        foreach (KeyValuePair<string, SessionRecord> pair in Sessions)
        {
            if (pair.Value.Context.ExpiresClockTicks > now) continue;
            if (remove == null) remove = new List<string>();
            remove.Add(pair.Key);
        }
        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++) Sessions.Remove(remove[i]);
        Interlocked.Add(ref expired, remove.Count);
    }

    private static long NowTicks()
    {
        return (long)(Stopwatch.GetTimestamp() *
            ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
    }

    private static RebirthNpcInteractionResult Reject(RebirthNpcInteractionResult result)
    {
        Interlocked.Increment(ref rejected);
        return result;
    }
}

public sealed class RebirthNpcHireRecord
{
    public RebirthNpcStableId NpcId { get; internal set; }
    public string OwnerActorId { get; internal set; }
    public uint Revision { get; internal set; }
    public long HiredUtcTicks { get; internal set; }
}

public static class RebirthNpcHireService
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcStableId, RebirthNpcHireRecord> Hires =
        new Dictionary<RebirthNpcStableId, RebirthNpcHireRecord>();
    private static long hired;
    private static long dismissed;
    private static long rejected;

    public static RebirthNpcInteractionResult Hire(string sessionKey, uint expectedRevision,
        out RebirthNpcHireRecord record)
    {
        // Free ownership mutation is no longer a hiring entry point. Native paid
        // settlement below is invoked only after the saved original is verified.
        record = null;
        return RebirthNpcInteractionResult.InvalidRequest;
    }

    internal static RebirthNpcInteractionResult HirePaid(string sessionKey, uint expectedRevision,
        out RebirthNpcHireRecord record)
    {
        record = null;
        RebirthNpcInteractionContext context;
        RebirthNpcInteractionResult authorization =
            RebirthNpcInteractionSessionService.Authorize(sessionKey,
                RebirthNpcInteractionAction.Hire, out context);
        if (authorization != RebirthNpcInteractionResult.Allowed) return authorization;
        lock (Sync)
        {
            RebirthNpcHireRecord existing;
            if (TryGet(context.NpcId, out existing))
            {
                record = Clone(existing);
                if (string.Equals(existing.OwnerActorId, context.ActorId,
                    StringComparison.OrdinalIgnoreCase))
                    return RebirthNpcInteractionResult.AlreadyHired;
                return Reject(RebirthNpcInteractionResult.OwnershipConflict);
            }
            if (expectedRevision != context.RuntimeRevision)
                return Reject(RebirthNpcInteractionResult.RevisionConflict);
            int entityId;
            EntityRebirthNPC entity = RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId, out entityId) &&
                GameManager.Instance != null && GameManager.Instance.World != null
                ? GameManager.Instance.World.GetEntity(entityId) as EntityRebirthNPC : null;
            if (entity == null || !RebirthSurvivorHirePayment.Eligible(entity) || !entity.PromoteSurvivorForHire() ||
                !entity.SetRebirthOwner(RebirthNpcOwnershipKind.Player, context.ActorId).Succeeded)
                return Reject(RebirthNpcInteractionResult.OwnershipConflict);
            EntityPlayer actor = RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId);
            var followOrder = entity.SetRebirthOrder(RebirthNpcOrderState.Follow, UnityEngine.Vector3.zero, false);
            var follow = actor != null && followOrder.Succeeded ? RebirthNpcCommandGateway.SubmitAuthenticated(entity, context.ActorId,
                actor.entityId, RebirthNpcCommandKind.Follow, UnityEngine.Vector3.zero, false, entity.RebirthRuntimeState.Revision)
                : RebirthNpcCommandSubmissionResult.Failure("Hiring player unavailable.");
            if (!follow.Accepted)
            {
                entity.SetRebirthOrder(RebirthNpcOrderState.None, UnityEngine.Vector3.zero, false);
                entity.SetRebirthOwner(RebirthNpcOwnershipKind.None, string.Empty);
                return Reject(RebirthNpcInteractionResult.TargetUnavailable);
            }
            RebirthNpcAggregatePersistenceStore.Mutate(context.NpcId, delegate(RebirthNpcPersistentRecord r)
            {
                if (r.Ownership == null) r.Ownership = new RebirthNpcOwnershipRecord();
                r.Ownership.OwnershipState = RebirthNpcOwnershipKind.Player.ToString();
                r.Ownership.OwnerPlatformIdOrPersistentPlayerId = context.ActorId;
                r.Ownership.HiredWorldTime = DateTime.UtcNow.Ticks;
                r.Ownership.DismissedWorldTime = null;
                r.Ownership.PermissionPolicyId = "rebirth.default";
                r.Ownership.PartyAccessMode = "owner-and-party";
                unchecked { r.Ownership.OwnerRevision++; }
            });
            RebirthNpcHireRecord created = new RebirthNpcHireRecord
            {
                NpcId = context.NpcId,
                OwnerActorId = context.ActorId,
                Revision = 1,
                HiredUtcTicks = DateTime.UtcNow.Ticks
            };
            Hires[context.NpcId] = created;
            record = Clone(created);
        }
        Interlocked.Increment(ref hired);
        return RebirthNpcInteractionResult.Allowed;
    }

    public static RebirthNpcInteractionResult Dismiss(string sessionKey,
        out RebirthNpcHireRecord removed)
    {
        removed = null;
        RebirthNpcInteractionContext context;
        RebirthNpcInteractionResult authorization =
            RebirthNpcInteractionSessionService.Authorize(sessionKey,
                RebirthNpcInteractionAction.Dismiss, out context);
        if (authorization != RebirthNpcInteractionResult.Allowed) return authorization;
        lock (Sync)
        {
            RebirthNpcHireRecord existing;
            if (!TryGet(context.NpcId, out existing))
                return Reject(RebirthNpcInteractionResult.NotHired);
            if (!string.Equals(existing.OwnerActorId, context.ActorId,
                StringComparison.OrdinalIgnoreCase) &&
                (context.Permissions & RebirthNpcInteractionPermissions.Admin) == 0)
                return Reject(RebirthNpcInteractionResult.OwnershipConflict);
            if(RebirthNpcInventoryTransactionService.GetSnapshot(context.NpcId).Quantities.Count>0)
                return Reject(RebirthNpcInteractionResult.InvalidRequest);
            int entityId;
            EntityRebirthNPC entity = RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId, out entityId) &&
                GameManager.Instance != null && GameManager.Instance.World != null
                ? GameManager.Instance.World.GetEntity(entityId) as EntityRebirthNPC : null;
            if (entity == null || !entity.SetRebirthOrder(RebirthNpcOrderState.None, UnityEngine.Vector3.zero, false).Succeeded ||
                !entity.SetRebirthOwner(RebirthNpcOwnershipKind.None, string.Empty).Succeeded)
                return Reject(RebirthNpcInteractionResult.OwnershipConflict);
            RebirthNpcMovementAdapterRegistry.Current?.Stop(entity, "Companion dismissed.");
            entity.SetAttackTarget(null, 0);
            entity.SetRevengeTarget(null);
            RebirthNpcLifecycleGameplayService.ReleaseCompanionOwnership(context.NpcId,existing.OwnerActorId);
            RebirthNpcAggregatePersistenceStore.Mutate(context.NpcId, delegate(RebirthNpcPersistentRecord r)
            {
                r.Ownership = null;
                r.Order = null;
                r.Contract = null;
                r.Work = null;
                r.Mission = null;
            });
            Hires.Remove(context.NpcId);
            removed = Clone(existing);
            RebirthNpcAggregatePersistenceStore.Save();
        }
        Interlocked.Increment(ref dismissed);
        return RebirthNpcInteractionResult.Allowed;
    }

    public static bool TryGet(RebirthNpcStableId npcId, out RebirthNpcHireRecord record)
    {
        lock (Sync)
        {
            // Ownership belongs to the live/save domains. This dictionary is only a
            // projection; a reset or unload must never erase a companion's contract.
            int entityId;
            RebirthNpcRuntimeState runtime;
            RebirthNpcPersistentRecord saved;
            if (RebirthNpcRuntimeRegistry.TryGetEntityId(npcId, out entityId) &&
                RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime))
            {
                if (runtime.OwnershipKind != RebirthNpcOwnershipKind.Player || string.IsNullOrEmpty(runtime.OwnerId))
                { Hires.Remove(npcId); record = null; return false; }
                record = new RebirthNpcHireRecord { NpcId=npcId, OwnerActorId=runtime.OwnerId, Revision=runtime.Revision };
                if (RebirthNpcAggregatePersistenceStore.TryGet(npcId, out saved) && saved.Ownership != null)
                    record.HiredUtcTicks = saved.Ownership.HiredWorldTime;
            }
            else if (RebirthNpcAggregatePersistenceStore.TryGet(npcId, out saved) && saved.Ownership != null &&
                saved.Ownership.OwnershipState == RebirthNpcOwnershipKind.Player.ToString() &&
                !string.IsNullOrEmpty(saved.Ownership.OwnerPlatformIdOrPersistentPlayerId) && saved.Lifecycle?.TombstoneState != true)
                record = new RebirthNpcHireRecord { NpcId=npcId, OwnerActorId=saved.Ownership.OwnerPlatformIdOrPersistentPlayerId,
                    Revision=saved.Ownership.OwnerRevision, HiredUtcTicks=saved.Ownership.HiredWorldTime };
            else { Hires.Remove(npcId); record = null; return false; }
            Hires[npcId] = Clone(record);
            return true;
        }
    }

    public static bool IsOwner(string actorId, RebirthNpcStableId npcId)
    {
        lock (Sync)
        {
            RebirthNpcHireRecord existing;
            return TryGet(npcId, out existing) &&
                string.Equals(existing.OwnerActorId, actorId,
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void Reset()
    {
        lock (Sync) Hires.Clear();
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            return "[REBIRTH NPC Hire] active=" + Hires.Count +
                " hired=" + Interlocked.Read(ref hired) +
                " dismissed=" + Interlocked.Read(ref dismissed) +
                " rejected=" + Interlocked.Read(ref rejected);
        }
    }

    private static RebirthNpcHireRecord Clone(RebirthNpcHireRecord value)
    {
        return new RebirthNpcHireRecord
        {
            NpcId = value.NpcId,
            OwnerActorId = value.OwnerActorId,
            Revision = value.Revision,
            HiredUtcTicks = value.HiredUtcTicks
        };
    }

    private static RebirthNpcInteractionResult Reject(RebirthNpcInteractionResult result)
    {
        Interlocked.Increment(ref rejected);
        return result;
    }
}
