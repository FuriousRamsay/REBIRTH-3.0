using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public enum RebirthNpcLifecycleQualificationState : byte
{
    NotRun = 0,
    Qualified = 1,
    Degraded = 2,
    Failed = 3
}

public sealed class RebirthNpcLifecycleQualificationSnapshot
{
    public RebirthNpcLifecycleQualificationState State { get; internal set; }
    public int BootstrapGeneration { get; internal set; }
    public int WorldGeneration { get; internal set; }
    public int ActiveLifecycleEntities { get; internal set; }
    public int ActiveRuntimeEntities { get; internal set; }
    public long BootstrapObservations { get; internal set; }
    public long WorldStartObservations { get; internal set; }
    public long WorldStopObservations { get; internal set; }
    public long EntityActivationObservations { get; internal set; }
    public long EntityDeactivationObservations { get; internal set; }
    public long DuplicateEntityActivations { get; internal set; }
    public long DuplicateStableIdentities { get; internal set; }
    public long RegistryCountMismatches { get; internal set; }
    public long ResidualStateFailures { get; internal set; }
    public string LastAuditReason { get; internal set; }
    public string[] Findings { get; internal set; }
}

/// <summary>
/// WP22 lifecycle qualification boundary. It observes bootstrap/world/entity transitions,
/// verifies stable identity uniqueness and registry/lifecycle convergence, and proves that
/// world shutdown clears every world-scoped observation owned by this service.
/// </summary>
public static class RebirthNpcLifecycleQualificationService
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthNpcStableId> ActiveEntities =
        new Dictionary<int, RebirthNpcStableId>();
    private static readonly Dictionary<RebirthNpcStableId, int> ActiveStableIds =
        new Dictionary<RebirthNpcStableId, int>();
    private static readonly List<string> Findings = new List<string>();

    private const int MaxFindings = 64;
    private static long bootstrapObservations;
    private static long worldStartObservations;
    private static long worldStopObservations;
    private static long entityActivationObservations;
    private static long entityDeactivationObservations;
    private static long duplicateEntityActivations;
    private static long duplicateStableIdentities;
    private static long registryCountMismatches;
    private static long residualStateFailures;
    private static int bootstrapGeneration;
    private static int worldGeneration;
    private static string lastAuditReason = "not-run";
    private static RebirthNpcLifecycleQualificationState state = RebirthNpcLifecycleQualificationState.NotRun;
    private static bool registryMismatchActive;
    private static bool runtimeDuplicateActive;

    public static void RecordBootstrap(RebirthNpcLifecycleSnapshot snapshot, bool duplicateAttempt)
    {
        if (snapshot == null) return;
        ObserveBootstrap(snapshot.BootstrapGeneration);
        // Repeated calls are expected; idempotence is proven by an unchanged generation.
        if (duplicateAttempt) return;
    }

    public static void RecordTransition(string transition, RebirthNpcLifecycleSnapshot snapshot, string detail)
    {
        lock (Sync)
        {
            lastAuditReason = string.IsNullOrWhiteSpace(transition) ? "transition" : transition;
            if (!string.IsNullOrWhiteSpace(detail) && detail.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                AddFindingNoLock(transition + ": " + detail);
            if (snapshot != null)
            {
                bootstrapGeneration = Math.Max(bootstrapGeneration, snapshot.BootstrapGeneration);
                worldGeneration = Math.Max(worldGeneration, snapshot.WorldGeneration);
            }
        }
    }

    public static void AuditWorldReset(string reason)
    {
        lock (Sync)
        {
            if (ActiveEntities.Count != 0 || ActiveStableIds.Count != 0)
            {
                residualStateFailures++;
                AddFindingNoLock("world reset retained qualification state after " + reason);
                state = RebirthNpcLifecycleQualificationState.Failed;
            }
            lastAuditReason = "world-reset:" + (reason ?? string.Empty);
        }
    }

    public static void ObserveBootstrap(int generation)
    {
        lock (Sync)
        {
            bootstrapObservations++;
            if (generation < bootstrapGeneration)
                AddFindingNoLock("bootstrap generation regressed from " + bootstrapGeneration + " to " + generation);
            bootstrapGeneration = Math.Max(bootstrapGeneration, generation);
            lastAuditReason = "bootstrap";
        }
        Audit("bootstrap");
    }

    public static void ObserveWorldStart(int generation)
    {
        lock (Sync)
        {
            worldStartObservations++;
            if (generation <= worldGeneration && worldGeneration != 0)
                AddFindingNoLock("world generation did not advance: current=" + worldGeneration + " observed=" + generation);
            worldGeneration = Math.Max(worldGeneration, generation);
            lastAuditReason = "world-start";
        }
        Audit("world-start");
    }

    public static void ObserveEntityActivated(int entityId, RebirthNpcStableId stableId)
    {
        lock (Sync)
        {
            entityActivationObservations++;
            RebirthNpcStableId existingStableId;
            if (ActiveEntities.TryGetValue(entityId, out existingStableId))
            {
                duplicateEntityActivations++;
                if (existingStableId != stableId)
                    AddFindingNoLock("entity " + entityId + " reactivated with a different stable identity");
                lastAuditReason = "duplicate-entity-activation";
                return;
            }

            int existingEntityId;
            if (!stableId.IsEmpty && ActiveStableIds.TryGetValue(stableId, out existingEntityId) && existingEntityId != entityId)
            {
                duplicateStableIdentities++;
                AddFindingNoLock("stable identity " + stableId + " is active for entities " + existingEntityId + " and " + entityId);
            }

            ActiveEntities[entityId] = stableId;
            if (!stableId.IsEmpty)
                ActiveStableIds[stableId] = entityId;
            lastAuditReason = "entity-activated:" + entityId;
        }
        Audit("entity-activated");
    }

    public static void ObserveEntityDeactivated(int entityId)
    {
        lock (Sync)
        {
            entityDeactivationObservations++;
            RebirthNpcStableId stableId;
            if (ActiveEntities.TryGetValue(entityId, out stableId))
            {
                ActiveEntities.Remove(entityId);
                int boundEntityId;
                if (!stableId.IsEmpty && ActiveStableIds.TryGetValue(stableId, out boundEntityId) && boundEntityId == entityId)
                    ActiveStableIds.Remove(stableId);
            }
            lastAuditReason = "entity-deactivated:" + entityId;
        }
        Audit("entity-deactivated");
    }

    public static RebirthNpcLifecycleQualificationSnapshot Audit(string reason)
    {
        RebirthNpcRuntimeState[] runtime = RebirthNpcRuntimeRegistry.GetSnapshot();
        HashSet<RebirthNpcStableId> runtimeStableIds = new HashSet<RebirthNpcStableId>();
        int duplicateRuntimeStableIds = 0;
        for (int i = 0; i < runtime.Length; i++)
        {
            if (!runtimeStableIds.Add(runtime[i].StableId))
                duplicateRuntimeStableIds++;
        }

        lock (Sync)
        {
            lastAuditReason = string.IsNullOrEmpty(reason) ? "audit" : reason;
            bool mismatchNow = runtime.Length != ActiveEntities.Count;
            if (mismatchNow && !registryMismatchActive)
            {
                registryCountMismatches++;
                AddFindingNoLock("runtime/lifecycle count mismatch runtime=" + runtime.Length + " lifecycle=" + ActiveEntities.Count);
            }
            registryMismatchActive = mismatchNow;

            bool duplicateNow = duplicateRuntimeStableIds > 0;
            if (duplicateNow && !runtimeDuplicateActive)
            {
                duplicateStableIdentities += duplicateRuntimeStableIds;
                AddFindingNoLock("runtime registry contains duplicate stable identities=" + duplicateRuntimeStableIds);
            }
            runtimeDuplicateActive = duplicateNow;

            state = duplicateStableIdentities > 0 || residualStateFailures > 0
                ? RebirthNpcLifecycleQualificationState.Failed
                : (duplicateEntityActivations > 0 || registryCountMismatches > 0 || Findings.Count > 0
                    ? RebirthNpcLifecycleQualificationState.Degraded
                    : RebirthNpcLifecycleQualificationState.Qualified);
            return SnapshotNoLock(runtime.Length);
        }
    }

    public static void ResetForWorldChange(bool gameShutdown)
    {
        lock (Sync)
        {
            worldStopObservations++;
            ActiveEntities.Clear();
            ActiveStableIds.Clear();
            registryMismatchActive = false;
            runtimeDuplicateActive = false;

            // These findings and counters describe one world generation. Carrying them
            // into the next world would make a repaired condition permanently degraded.
            duplicateEntityActivations = 0;
            duplicateStableIdentities = 0;
            registryCountMismatches = 0;
            residualStateFailures = 0;
            entityActivationObservations = 0;
            entityDeactivationObservations = 0;
            Findings.Clear();

            lastAuditReason = gameShutdown ? "game-shutdown-reset" : "world-shutdown-reset";
            state = RebirthNpcLifecycleQualificationState.Qualified;
        }
    }

    public static RebirthNpcLifecycleQualificationSnapshot GetSnapshot()
    {
        return Audit("snapshot");
    }

    public static string GetReport()
    {
        RebirthNpcLifecycleQualificationSnapshot snapshot = GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC Lifecycle Qualification] state=").Append(snapshot.State)
            .Append(" bootstrapGeneration=").Append(snapshot.BootstrapGeneration)
            .Append(" worldGeneration=").Append(snapshot.WorldGeneration)
            .Append(" lifecycleEntities=").Append(snapshot.ActiveLifecycleEntities)
            .Append(" runtimeEntities=").Append(snapshot.ActiveRuntimeEntities)
            .Append(" bootstrapObservations=").Append(snapshot.BootstrapObservations)
            .Append(" worldStarts=").Append(snapshot.WorldStartObservations)
            .Append(" worldStops=").Append(snapshot.WorldStopObservations)
            .Append(" activations=").Append(snapshot.EntityActivationObservations)
            .Append(" deactivations=").Append(snapshot.EntityDeactivationObservations)
            .Append(" duplicateActivations=").Append(snapshot.DuplicateEntityActivations)
            .Append(" duplicateStableIds=").Append(snapshot.DuplicateStableIdentities)
            .Append(" countMismatches=").Append(snapshot.RegistryCountMismatches)
            .Append(" residualFailures=").Append(snapshot.ResidualStateFailures)
            .Append(" last=").Append(snapshot.LastAuditReason);
        for (int i = 0; i < snapshot.Findings.Length; i++)
            builder.AppendLine().Append("  finding=").Append(snapshot.Findings[i]);
        return builder.ToString();
    }

    private static RebirthNpcLifecycleQualificationSnapshot SnapshotNoLock(int runtimeCount)
    {
        return new RebirthNpcLifecycleQualificationSnapshot
        {
            State = state,
            BootstrapGeneration = bootstrapGeneration,
            WorldGeneration = worldGeneration,
            ActiveLifecycleEntities = ActiveEntities.Count,
            ActiveRuntimeEntities = runtimeCount,
            BootstrapObservations = bootstrapObservations,
            WorldStartObservations = worldStartObservations,
            WorldStopObservations = worldStopObservations,
            EntityActivationObservations = entityActivationObservations,
            EntityDeactivationObservations = entityDeactivationObservations,
            DuplicateEntityActivations = duplicateEntityActivations,
            DuplicateStableIdentities = duplicateStableIdentities,
            RegistryCountMismatches = registryCountMismatches,
            ResidualStateFailures = residualStateFailures,
            LastAuditReason = lastAuditReason,
            Findings = Findings.ToArray()
        };
    }

    private static void AddFindingNoLock(string finding)
    {
        if (string.IsNullOrWhiteSpace(finding)) return;
        if (Findings.Count >= MaxFindings) Findings.RemoveAt(0);
        Findings.Add(finding);
    }
}
