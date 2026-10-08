using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public interface IRebirthNpcWorkExecutor
{
    string ExecutorId { get; }
    int Priority { get; }
    bool CanExecute(EntityRebirthNPC npc, RebirthNpcExecutionLease lease);
    void Begin(RebirthNpcWorkContext context);
    void Tick(RebirthNpcWorkContext context);
    void End(RebirthNpcWorkContext context, string reason);
}

public readonly struct RebirthNpcWorkContext
{
    public readonly EntityRebirthNPC Npc;
    public readonly RebirthNpcExecutionLease Lease;
    public readonly ulong ActivityId;
    public readonly Vector3 WorkPosition;

    public RebirthNpcWorkContext(EntityRebirthNPC npc, RebirthNpcExecutionLease lease,
        ulong activityId, Vector3 workPosition)
    {
        Npc = npc;
        Lease = lease;
        ActivityId = activityId;
        WorkPosition = workPosition;
    }
}

public readonly struct RebirthNpcWorkExecutorBinding
{
    public readonly IRebirthNpcWorkExecutor Executor;
    public readonly long Generation;

    public RebirthNpcWorkExecutorBinding(IRebirthNpcWorkExecutor executor, long generation)
    {
        Executor = executor;
        Generation = generation;
    }

    public bool IsValid => Executor != null && Generation > 0;
}

public static class RebirthNpcWorkExecutorRegistry
{
    private sealed class Registration
    {
        public IRebirthNpcWorkExecutor Executor;
        public long Generation;
        public long RegisteredClockTicks;
    }

    private sealed class ResolutionCacheEntry
    {
        public int EntityId;
        public ulong LeaseId;
        public uint AcceptedRevision;
        public RebirthNpcWorkExecutorBinding Binding;
        public long ExpiresClockTicks;
    }

    private const int ResolutionCacheMilliseconds = 1500;
    private static readonly object Sync = new object();
    private static readonly List<Registration> Executors = new List<Registration>();
    private static readonly Dictionary<int, ResolutionCacheEntry> ResolutionCache =
        new Dictionary<int, ResolutionCacheEntry>();
    private static long nextGeneration;
    private static long registrations, replacements, unregisters, drainedSessions, staleBindingRejects;
    private static long resolutionCacheHits, resolutionCacheMisses, resolutionCacheInvalidations,
        resolutionCacheHealthInvalidations, resolutionCacheExpiredPrunes, resolutionRaces;
    private static long nextResolutionCachePruneClockTicks;

    public static bool Register(IRebirthNpcWorkExecutor executor)
    {
        if (executor == null || string.IsNullOrWhiteSpace(executor.ExecutorId)) return false;
        long replacedGeneration = 0;
        lock (Sync)
        {
            for (int i = 0; i < Executors.Count; i++)
            {
                if (!string.Equals(Executors[i].Executor.ExecutorId, executor.ExecutorId,
                    StringComparison.Ordinal)) continue;
                if (ReferenceEquals(Executors[i].Executor, executor)) return true;

                replacedGeneration = Executors[i].Generation;
                InvalidateCachedGenerationLocked(replacedGeneration);
                Executors[i] = new Registration
                {
                    Executor = executor,
                    Generation = ++nextGeneration,
                    RegisteredClockTicks = RebirthNpcWorkClock.NowTicks
                };
                replacements++;
                SortExecutorsLocked();
                break;
            }

            if (replacedGeneration <= 0)
            {
                Executors.Add(new Registration
                {
                    Executor = executor,
                    Generation = ++nextGeneration,
                    RegisteredClockTicks = RebirthNpcWorkClock.NowTicks
                });
                registrations++;
                SortExecutorsLocked();
            }
        }

        if (replacedGeneration > 0)
        {
            int drained = RebirthNpcWorkRuntime.DrainExecutor(executor.ExecutorId, replacedGeneration,
                "Work executor registration was replaced by a newer generation.");
            RebirthNpcWorkExecutorHealthRegistry.Remove(executor.ExecutorId, replacedGeneration);
            lock (Sync) drainedSessions += drained;
        }
        return true;
    }

    private static void SortExecutorsLocked()
    {
        Executors.Sort((left, right) =>
        {
            int priority = right.Executor.Priority.CompareTo(left.Executor.Priority);
            return priority != 0 ? priority : string.Compare(left.Executor.ExecutorId,
                right.Executor.ExecutorId, StringComparison.Ordinal);
        });
    }

    public static bool Unregister(string executorId)
    {
        if (string.IsNullOrWhiteSpace(executorId)) return false;
        long removedGeneration = 0;
        lock (Sync)
        {
            for (int i = 0; i < Executors.Count; i++)
                if (string.Equals(Executors[i].Executor.ExecutorId, executorId, StringComparison.Ordinal))
                {
                    removedGeneration = Executors[i].Generation;
                    Executors.RemoveAt(i);
                    InvalidateCachedGenerationLocked(removedGeneration);
                    unregisters++;
                    break;
                }
        }
        if (removedGeneration <= 0) return false;

        int drained = RebirthNpcWorkRuntime.DrainExecutor(executorId, removedGeneration,
            "Work executor registration was removed.");
        RebirthNpcWorkExecutorHealthRegistry.Remove(executorId, removedGeneration);
        lock (Sync) drainedSessions += drained;
        return true;
    }

    public static IRebirthNpcWorkExecutor[] GetSnapshot()
    {
        lock (Sync)
        {
            IRebirthNpcWorkExecutor[] result = new IRebirthNpcWorkExecutor[Executors.Count];
            for (int i = 0; i < Executors.Count; i++) result[i] = Executors[i].Executor;
            return result;
        }
    }

    public static RebirthNpcWorkExecutorBinding ResolveBinding(EntityRebirthNPC npc,
        RebirthNpcExecutionLease lease)
    {
        if (npc == null || lease == null) return default(RebirthNpcWorkExecutorBinding);
        long now = RebirthNpcWorkClock.NowTicks;
        Registration[] snapshot;
        lock (Sync)
        {
            PruneExpiredResolutionCacheLocked(now);
            ResolutionCacheEntry cached;
            if (ResolutionCache.TryGetValue(npc.entityId, out cached))
            {
                bool registrationCurrent = cached.Binding.Executor != null &&
                    IsCurrentRegistrationLocked(cached.Binding.Executor.ExecutorId,
                        cached.Binding.Generation);
                string availabilityDetail;
                bool executorAvailable = registrationCurrent &&
                    RebirthNpcWorkExecutorHealthRegistry.IsAvailable(
                        cached.Binding.Executor.ExecutorId, cached.Binding.Generation, out availabilityDetail);
                if (cached.LeaseId == lease.LeaseId &&
                    cached.AcceptedRevision == lease.AcceptedRevision &&
                    cached.ExpiresClockTicks > now && registrationCurrent && executorAvailable)
                {
                    resolutionCacheHits++;
                    return cached.Binding;
                }
                ResolutionCache.Remove(npc.entityId);
                resolutionCacheInvalidations++;
                if (registrationCurrent && !executorAvailable)
                    resolutionCacheHealthInvalidations++;
            }
            resolutionCacheMisses++;
            snapshot = Executors.ToArray();
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            Registration registration = snapshot[i];
            string availabilityDetail;
            if (!RebirthNpcWorkExecutorHealthRegistry.IsAvailable(
                registration.Executor.ExecutorId, registration.Generation, out availabilityDetail))
                continue;
            bool canExecute;
            try
            {
                canExecute = registration.Executor.CanExecute(npc, lease);
            }
            catch (Exception ex)
            {
                RebirthNpcWorkExecutionDiagnostics.RecordFault();
                RebirthNpcWorkExecutorHealthRegistry.RecordFault(
                    registration.Executor.ExecutorId, registration.Generation, "CanExecute", ex.Message);
                Log.Warning("[REBIRTH NPC] Work executor " +
                    registration.Executor.ExecutorId + " CanExecute failed: " + ex.Message);
                continue;
            }
            if (!canExecute) continue;

            RebirthNpcWorkExecutorBinding binding = new RebirthNpcWorkExecutorBinding(
                registration.Executor, registration.Generation);
            lock (Sync)
            {
                if (!IsCurrentRegistrationLocked(registration.Executor.ExecutorId,
                    registration.Generation))
                {
                    resolutionRaces++;
                    continue;
                }
                ResolutionCache[npc.entityId] = new ResolutionCacheEntry
                {
                    EntityId = npc.entityId,
                    LeaseId = lease.LeaseId,
                    AcceptedRevision = lease.AcceptedRevision,
                    Binding = binding,
                    ExpiresClockTicks = now + TimeSpan.FromMilliseconds(
                        ResolutionCacheMilliseconds).Ticks
                };
                return binding;
            }
        }

        lock (Sync) ResolutionCache.Remove(npc.entityId);
        return default(RebirthNpcWorkExecutorBinding);
    }

    public static IRebirthNpcWorkExecutor Resolve(EntityRebirthNPC npc, RebirthNpcExecutionLease lease) =>
        ResolveBinding(npc, lease).Executor;

    public static bool IsCurrentRegistration(string executorId, long generation)
    {
        if (string.IsNullOrWhiteSpace(executorId) || generation <= 0) return false;
        lock (Sync)
        {
            if (IsCurrentRegistrationLocked(executorId, generation)) return true;
            staleBindingRejects++;
            return false;
        }
    }

    private static bool IsCurrentRegistrationLocked(string executorId, long generation)
    {
        for (int i = 0; i < Executors.Count; i++)
            if (Executors[i].Generation == generation &&
                string.Equals(Executors[i].Executor.ExecutorId, executorId,
                    StringComparison.Ordinal))
                return true;
        return false;
    }

    private static void PruneExpiredResolutionCacheLocked(long now)
    {
        if (now < nextResolutionCachePruneClockTicks) return;
        nextResolutionCachePruneClockTicks = now + TimeSpan.FromSeconds(5).Ticks;
        if (ResolutionCache.Count == 0) return;

        List<int> remove = null;
        foreach (KeyValuePair<int, ResolutionCacheEntry> pair in ResolutionCache)
        {
            if (pair.Value.ExpiresClockTicks > now) continue;
            if (remove == null) remove = new List<int>();
            remove.Add(pair.Key);
        }
        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++) ResolutionCache.Remove(remove[i]);
        resolutionCacheExpiredPrunes += remove.Count;
    }

    private static void InvalidateCachedGenerationLocked(long generation)
    {
        if (generation <= 0 || ResolutionCache.Count == 0) return;
        List<int> remove = null;
        foreach (KeyValuePair<int, ResolutionCacheEntry> pair in ResolutionCache)
        {
            if (pair.Value.Binding.Generation != generation) continue;
            if (remove == null) remove = new List<int>();
            remove.Add(pair.Key);
        }
        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++) ResolutionCache.Remove(remove[i]);
        resolutionCacheInvalidations += remove.Count;
    }

    public static void ResetResolutionCacheForWorldChange()
    {
        lock (Sync)
        {
            ResolutionCache.Clear();
            staleBindingRejects = 0;
            resolutionCacheHits = resolutionCacheMisses = resolutionCacheInvalidations = 0;
            resolutionCacheHealthInvalidations = resolutionCacheExpiredPrunes = 0;
            resolutionRaces = 0;
            nextResolutionCachePruneClockTicks = 0;
        }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder b = new StringBuilder();
            b.Append("[REBIRTH NPC] work executors registered=").Append(Executors.Count)
                .Append(" registrations=").Append(registrations)
                .Append(" replacements=").Append(replacements)
                .Append(" unregisters=").Append(unregisters)
                .Append(" drainedSessions=").Append(drainedSessions)
                .Append(" staleBindingRejects=").Append(staleBindingRejects)
                .Append(" resolutionCache=").Append(ResolutionCache.Count)
                .Append(" resolutionCacheHits=").Append(resolutionCacheHits)
                .Append(" resolutionCacheMisses=").Append(resolutionCacheMisses)
                .Append(" resolutionCacheInvalidations=").Append(resolutionCacheInvalidations)
                .Append(" resolutionCacheHealthInvalidations=").Append(resolutionCacheHealthInvalidations)
                .Append(" resolutionCacheExpiredPrunes=").Append(resolutionCacheExpiredPrunes)
                .Append(" resolutionRaces=").Append(resolutionRaces).AppendLine();
            long now = RebirthNpcWorkClock.NowTicks;
            for (int i = 0; i < Executors.Count; i++)
                b.Append("  id=").Append(Executors[i].Executor.ExecutorId)
                    .Append(" generation=").Append(Executors[i].Generation)
                    .Append(" priority=").Append(Executors[i].Executor.Priority)
                    .Append(" ageSeconds=").Append(Math.Max(0L,
                        (now - Executors[i].RegisteredClockTicks) / TimeSpan.TicksPerSecond))
                    .Append(" type=").Append(Executors[i].Executor.GetType().FullName).AppendLine();
            return b.ToString().TrimEnd();
        }
    }
}

public sealed class RebirthNpcLocationPresenceWorkExecutor : IRebirthNpcWorkExecutor
{
    public string ExecutorId => "rebirth.work.location-presence";
    public int Priority => -1000;
    public bool CanExecute(EntityRebirthNPC npc, RebirthNpcExecutionLease lease) =>
        npc != null && lease != null && lease.Order == RebirthNpcOrderState.Work;
    public void Begin(RebirthNpcWorkContext context) { }
    public void Tick(RebirthNpcWorkContext context) { }
    public void End(RebirthNpcWorkContext context, string reason) { }
}

public sealed class RebirthNpcWorkNavigationController : IRebirthNpcActivityController
{
    public string ControllerId => "rebirth.navigation.work";
    public int Priority => 165;
    public bool CanHandle(EntityRebirthNPC npc, RebirthNpcExecutionLease lease) =>
        npc != null && lease != null && lease.Order == RebirthNpcOrderState.Work &&
        lease.HasTargetPosition &&
        (npc.position - lease.TargetPosition).sqrMagnitude > RebirthNpcWorkRuntime.WorkRadiusSquared;
    public int GetHeartbeatTimeoutSeconds(RebirthNpcExecutionLease lease) => 6;
    public void Start(RebirthNpcControllerContext context) => RebirthNpcWorkRuntime.AttachNavigation(context);
    public void Recover(RebirthNpcControllerContext context) => RebirthNpcWorkRuntime.AttachNavigation(context);
}

public sealed class RebirthNpcWorkExecutionController : IRebirthNpcActivityController
{
    public string ControllerId => "rebirth.execution.work";
    public int Priority => 160;
    public bool CanHandle(EntityRebirthNPC npc, RebirthNpcExecutionLease lease) =>
        npc != null && lease != null && lease.Order == RebirthNpcOrderState.Work &&
        lease.HasTargetPosition &&
        (npc.position - lease.TargetPosition).sqrMagnitude <= RebirthNpcWorkRuntime.WorkRadiusSquared &&
        RebirthNpcWorkExecutorRegistry.Resolve(npc, lease) != null;
    public int GetHeartbeatTimeoutSeconds(RebirthNpcExecutionLease lease) => 6;
    public void Start(RebirthNpcControllerContext context) => RebirthNpcWorkRuntime.AttachExecution(context);
    public void Recover(RebirthNpcControllerContext context) => RebirthNpcWorkRuntime.AttachExecution(context);
}

public static class RebirthNpcWorkRuntime
{
    public const float WorkRadius = 2.5f;
    public const float WorkRadiusSquared = WorkRadius * WorkRadius;
    private const int HeartbeatTimeoutSeconds = 6;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthNpcWorkSession> Sessions =
        new Dictionary<int, RebirthNpcWorkSession>();
    private static readonly object TickSweepSync = new object();
    private static long nextTickClockTicks;

    public static void Register()
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        RebirthNpcWorkExecutorRegistry.Register(new RebirthNpcLocationPresenceWorkExecutor());
        RebirthNpcActivityControllerRegistry.Register(new RebirthNpcWorkNavigationController());
        RebirthNpcActivityControllerRegistry.Register(new RebirthNpcWorkExecutionController());
    }

    public static void AttachNavigation(RebirthNpcControllerContext context)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        bool enteredImmediately = Monitor.TryEnter(TickSweepSync);
        if (!enteredImmediately)
        {
            RebirthNpcWorkDiagnostics.RecordAttachmentWaitedForLifecycle();
            Monitor.Enter(TickSweepSync);
        }
        try
        {
            AttachNavigationCore(context);
        }
        finally
        {
            Monitor.Exit(TickSweepSync);
        }
    }

    private static void AttachNavigationCore(RebirthNpcControllerContext context)
    {
        if (!ValidateTarget(context)) return;
        Store(new RebirthNpcWorkSession
        {
            EntityId = context.Npc.entityId,
            Npc = context.Npc,
            LeaseId = context.Lease.LeaseId,
            ActivityId = context.ActivityId,
            AcceptedRevision = context.Lease.AcceptedRevision,
            WorkPosition = context.Lease.TargetPosition,
            Mode = RebirthNpcWorkSessionMode.Navigating,
            Executor = null,
            Lease = context.Lease,
            CallbackSync = new object(),
            StartedClockTicks = RebirthNpcWorkClock.NowTicks
        }, context.Npc, context.Lease, "Work navigation session was replaced by a newer activity.");
        RebirthNpcWorkDiagnostics.RecordAttached(true, context.IsRecovery);
    }

    public static void AttachExecution(RebirthNpcControllerContext context)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        bool enteredImmediately = Monitor.TryEnter(TickSweepSync);
        if (!enteredImmediately)
        {
            RebirthNpcWorkDiagnostics.RecordAttachmentWaitedForLifecycle();
            Monitor.Enter(TickSweepSync);
        }
        try
        {
            AttachExecutionCore(context);
        }
        finally
        {
            Monitor.Exit(TickSweepSync);
        }
    }

    private static void AttachExecutionCore(RebirthNpcControllerContext context)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        if (!ValidateTarget(context)) return;
        RebirthNpcWorkExecutorBinding binding = RebirthNpcWorkExecutorRegistry.ResolveBinding(
            context.Npc, context.Lease);
        IRebirthNpcWorkExecutor executor = binding.Executor;
        if (executor == null)
        {
            RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                context.Lease.LeaseId, context.Lease.AcceptedRevision,
                RebirthNpcActivityOutcome.TransientBlocked, "No compatible Work executor is registered.");
            return;
        }
        RebirthNpcWorkSession session = new RebirthNpcWorkSession
        {
            EntityId = context.Npc.entityId,
            Npc = context.Npc,
            LeaseId = context.Lease.LeaseId,
            ActivityId = context.ActivityId,
            AcceptedRevision = context.Lease.AcceptedRevision,
            WorkPosition = context.Lease.TargetPosition,
            Mode = RebirthNpcWorkSessionMode.Executing,
            Executor = executor,
            ExecutorId = executor.ExecutorId,
            ExecutorGeneration = binding.Generation,
            Lease = context.Lease,
            CallbackSync = new object(),
            StartedClockTicks = RebirthNpcWorkClock.NowTicks
        };
        RebirthNpcWorkContext workContext = new RebirthNpcWorkContext(context.Npc, context.Lease, context.ActivityId,
            context.Lease.TargetPosition);
        IRebirthNpcValidatedWorkExecutor validated = executor as IRebirthNpcValidatedWorkExecutor;
        if (validated != null)
        {
            RebirthNpcWorkValidationResult validation;
            try { validation = validated.Validate(workContext); }
            catch (Exception ex)
            {
                RebirthNpcWorkExecutionDiagnostics.RecordValidation(false);
                RebirthNpcWorkExecutionDiagnostics.RecordFault();
                RebirthNpcWorkExecutorHealthRegistry.RecordFault(executor.ExecutorId, binding.Generation, "Validate", ex.Message);
                RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                    context.Lease.LeaseId, context.Lease.AcceptedRevision,
                    RebirthNpcActivityOutcome.ControllerFault,
                    "Work executor " + executor.ExecutorId + " validation failed: " + ex.Message);
                return;
            }
            RebirthNpcWorkExecutionDiagnostics.RecordValidation(validation.IsValid);
            if (validation.IsValid) RebirthNpcWorkExecutorHealthRegistry.RecordSuccess(executor.ExecutorId, binding.Generation);
            if (!validation.IsValid)
            {
                RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                    context.Lease.LeaseId, context.Lease.AcceptedRevision,
                    validation.Code == RebirthNpcWorkValidationCode.TargetBusy ?
                        RebirthNpcActivityOutcome.TransientBlocked : RebirthNpcActivityOutcome.PermanentInvalid,
                    validation.Detail);
                return;
            }
        }
        string ownershipDetail;
        if (!RebirthNpcWorkTargetRegistry.TryAcquire(context.Lease.TargetPosition, context.Npc.entityId,
            context.ActivityId, context.Lease.LeaseId, context.Lease.AcceptedRevision, out ownershipDetail))
        {
            RebirthNpcWorkExecutionDiagnostics.RecordValidation(false);
            RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                context.Lease.LeaseId, context.Lease.AcceptedRevision,
                RebirthNpcActivityOutcome.TransientBlocked, ownershipDetail);
            return;
        }
        lock (session.CallbackSync)
        {
            Store(session, context.Npc, context.Lease,
                "Work execution session was replaced by a newer activity.");
            try
            {
                IRebirthNpcRecoverableWorkExecutor recoverable = executor as IRebirthNpcRecoverableWorkExecutor;
                if (context.IsRecovery && recoverable != null) recoverable.Recover(workContext);
                else executor.Begin(workContext);
                RebirthNpcWorkExecutionDiagnostics.RecordBegin(context.IsRecovery);
                RebirthNpcWorkExecutorHealthRegistry.RecordSuccess(executor.ExecutorId, binding.Generation);
            }
            catch (Exception ex)
            {
                RebirthNpcWorkExecutionDiagnostics.RecordFault();
                RebirthNpcWorkExecutorHealthRegistry.RecordFault(executor.ExecutorId, binding.Generation,
                    context.IsRecovery ? "Recover" : "Begin", ex.Message);
                RebirthNpcWorkDiagnostics.RecordStartupRollback();
                Report(session, RebirthNpcActivityOutcome.ControllerFault,
                    "Work executor " + executor.ExecutorId + " failed to begin: " + ex.Message,
                    context.Npc, context.Lease);
                return;
            }
            RebirthNpcWorkDiagnostics.RecordAttached(false, context.IsRecovery);
        }
    }

    private static bool ValidateTarget(RebirthNpcControllerContext context)
    {
        if (context.Lease.HasTargetPosition) return true;
        RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
            context.Lease.LeaseId, context.Lease.AcceptedRevision,
            RebirthNpcActivityOutcome.PermanentInvalid, "Work execution lease has no target position.");
        return false;
    }

    private static void Store(RebirthNpcWorkSession session, EntityRebirthNPC npc,
        RebirthNpcExecutionLease lease, string replacementReason)
    {
        RebirthNpcWorkSession displaced = default(RebirthNpcWorkSession);
        bool hasDisplaced = false;
        lock (Sync)
        {
            RebirthNpcWorkSession current;
            if (Sessions.TryGetValue(session.EntityId, out current))
            {
                displaced = current;
                hasDisplaced = true;
            }
            Sessions[session.EntityId] = session;
        }
        if (!hasDisplaced) return;

        RebirthNpcWorkDiagnostics.RecordSessionReplacement();
        SafeCleanupDetached(displaced, string.IsNullOrEmpty(replacementReason) ?
            "Work session was replaced." : replacementReason, npc, displaced.Lease ?? lease,
            "session replacement");
    }

    public static void Tick()
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        if (!Monitor.TryEnter(TickSweepSync))
        {
            RebirthNpcWorkDiagnostics.RecordOverlappingTickSweepRejected();
            return;
        }

        try
        {
            RebirthNpcWorkSession[] snapshot;
            lock (Sync)
            {
                long now = RebirthNpcWorkClock.NowTicks;
                if (now < nextTickClockTicks) return;
                nextTickClockTicks = now + TimeSpan.FromSeconds(1).Ticks;
                snapshot = new RebirthNpcWorkSession[Sessions.Count];
                Sessions.Values.CopyTo(snapshot, 0);
            }

            RebirthNpcWorkDiagnostics.RecordTickSweep();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    TickSession(snapshot[i]);
                }
                catch (Exception ex)
                {
                    RebirthNpcWorkDiagnostics.RecordSessionTickFaultContained();
                    Log.Warning("[REBIRTH NPC] Work session tick fault was contained for entity " +
                        snapshot[i].EntityId + " activity " + snapshot[i].ActivityId + ": " + ex.Message);
                    try
                    {
                        Report(snapshot[i], RebirthNpcActivityOutcome.ControllerFault,
                            "Work runtime failed while processing the session: " + ex.Message,
                            ResolveNpc(snapshot[i].EntityId), snapshot[i].Lease);
                    }
                    catch (Exception cleanupEx)
                    {
                        RebirthNpcWorkDiagnostics.RecordEmergencyCleanupFault();
                        Log.Warning("[REBIRTH NPC] Work session emergency cleanup also failed for entity " +
                            snapshot[i].EntityId + ": " + cleanupEx.Message);
                        ForceDetach(snapshot[i], "Emergency cleanup after an unhandled Work runtime fault.");
                    }
                }
            }
            RebirthNpcWorkTargetRegistry.Reconcile(GetSnapshot());
        }
        finally
        {
            Monitor.Exit(TickSweepSync);
        }
    }

    private static void TickSession(RebirthNpcWorkSession session)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        object callbackSync = session.CallbackSync ?? session;
        lock (callbackSync)
        {
            if (!IsCurrentSession(session))
            {
                RebirthNpcWorkDiagnostics.RecordStaleTickRejected();
                return;
            }
            long now = RebirthNpcWorkClock.NowTicks;
            EntityRebirthNPC npc = ResolveNpc(session.EntityId);
            if (npc == null) { Remove(session, "NPC unavailable."); return; }
            RebirthNpcExecutionLease lease;
            if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(session.EntityId, out lease) ||
                lease.LeaseId != session.LeaseId || lease.Order != RebirthNpcOrderState.Work)
            { Remove(session, "Work lease ended.", npc, lease); return; }
            if (session.Executor != null && !RebirthNpcWorkExecutorRegistry.IsCurrentRegistration(
                session.ExecutorId, session.ExecutorGeneration))
            {
                Report(session, RebirthNpcActivityOutcome.TransientBlocked,
                    "Work executor registration changed while the session was active.", npc, lease);
                return;
            }
    
            float distanceSquared = (npc.position - session.WorkPosition).sqrMagnitude;
            if (session.Mode == RebirthNpcWorkSessionMode.Navigating)
            {
                if (distanceSquared <= WorkRadiusSquared)
                {
                    Stop(npc, "Work position reached.");
                    RebirthNpcActivityRegistry.TryYield(session.EntityId, session.ActivityId,
                        session.LeaseId, "Work position reached; yielding to the Work executor.");
                    RebirthNpcWorkDiagnostics.RecordHandoff();
                    Remove(session, null);
                    return;
                }
                IRebirthNpcMovementAdapter adapter = RebirthNpcMovementAdapterRegistry.Current;
                if (adapter == null)
                { Report(session, RebirthNpcActivityOutcome.TransientBlocked, "No movement adapter is registered for Work navigation."); return; }
                string detail;
                RebirthNpcMovementRequestResult result;
                try
                {
                    result = adapter.RequestMove(npc, session.WorkPosition, WorkRadius, out detail);
                }
                catch (Exception ex)
                {
                    RebirthNpcWorkDiagnostics.RecordMovementAdapterFault();
                    Report(session, RebirthNpcActivityOutcome.ControllerFault,
                        "Work movement adapter failed: " + ex.Message, npc, lease);
                    return;
                }
                if (result == RebirthNpcMovementRequestResult.Unsupported || result == RebirthNpcMovementRequestResult.Failed)
                { Report(session, RebirthNpcActivityOutcome.PermanentInvalid, detail); return; }
                if (result == RebirthNpcMovementRequestResult.TemporarilyUnavailable)
                { Report(session, RebirthNpcActivityOutcome.TransientBlocked, detail); return; }
                RebirthNpcWorkDiagnostics.RecordMoveRequest(result);
            }
            else
            {
                string ownershipDetail;
                if (!RebirthNpcWorkTargetRegistry.TryRenew(session.WorkPosition, session.EntityId,
                    session.ActivityId, session.LeaseId, session.AcceptedRevision, out ownershipDetail))
                {
                    Report(session, RebirthNpcActivityOutcome.TransientBlocked, ownershipDetail);
                    return;
                }
                if (distanceSquared > WorkRadiusSquared)
                {
                    RebirthNpcActivityRegistry.TryYield(session.EntityId, session.ActivityId,
                        session.LeaseId, "NPC left the Work radius; yielding to Work navigation.");
                    RebirthNpcWorkDiagnostics.RecordLeftRadius();
                    Remove(session, "NPC left the Work radius.", npc, lease);
                    return;
                }
                IRebirthNpcWorkExecutionPolicy policy = session.Executor as IRebirthNpcWorkExecutionPolicy;
                if (policy != null && policy.MaximumExecutionSeconds > 0 &&
                    now - session.StartedClockTicks > TimeSpan.FromSeconds(policy.MaximumExecutionSeconds).Ticks)
                {
                    RebirthNpcWorkExecutionDiagnostics.RecordTimeout();
                    Report(session, RebirthNpcActivityOutcome.TransientBlocked,
                        "Work executor " + session.Executor.ExecutorId + " exceeded its maximum execution duration.",
                        npc, lease);
                    return;
                }
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    session.Executor.Tick(new RebirthNpcWorkContext(npc, lease, session.ActivityId,
                        session.WorkPosition));
                    RebirthNpcWorkDiagnostics.RecordExecutorTick();
                    RebirthNpcWorkExecutorHealthRegistry.RecordSuccess(session.Executor.ExecutorId, session.ExecutorGeneration);
                }
                catch (Exception ex)
                {
                    RebirthNpcWorkExecutionDiagnostics.RecordFault();
                    RebirthNpcWorkExecutorHealthRegistry.RecordFault(session.Executor.ExecutorId, session.ExecutorGeneration, "Tick", ex.Message);
                    Report(session, RebirthNpcActivityOutcome.ControllerFault,
                        "Work executor " + session.Executor.ExecutorId + " failed: " + ex.Message,
                        npc, lease);
                    return;
                }
                finally
                {
                    stopwatch.Stop();
                    if (policy != null && policy.SlowTickWarningMilliseconds > 0 &&
                        stopwatch.ElapsedMilliseconds >= policy.SlowTickWarningMilliseconds)
                        RebirthNpcWorkExecutionDiagnostics.RecordSlowTick();
                }
            }
    
            bool heartbeatAccepted;
            try
            {
                heartbeatAccepted = RebirthNpcActivityRegistry.TryHeartbeat(session.EntityId, session.ActivityId,
                    session.LeaseId, HeartbeatTimeoutSeconds,
                    session.Mode == RebirthNpcWorkSessionMode.Navigating ?
                        "Navigating to Work position." : "Executing Work at assigned position.");
            }
            catch (Exception ex)
            {
                RebirthNpcWorkDiagnostics.RecordHeartbeatFault();
                Report(session, RebirthNpcActivityOutcome.ControllerFault,
                    "Work heartbeat failed: " + ex.Message, npc, lease);
                return;
            }
            if (!heartbeatAccepted)
                Remove(session, "Work heartbeat rejected.", npc, lease);
        }
    }
    private static bool IsCurrentSession(RebirthNpcWorkSession session)
    {
        lock (Sync)
        {
            RebirthNpcWorkSession current;
            return Sessions.TryGetValue(session.EntityId, out current) &&
                current.ActivityId == session.ActivityId &&
                current.LeaseId == session.LeaseId &&
                current.AcceptedRevision == session.AcceptedRevision &&
                ReferenceEquals(current.CallbackSync, session.CallbackSync);
        }
    }

    private static void Report(RebirthNpcWorkSession session, RebirthNpcActivityOutcome outcome, string detail,
        EntityRebirthNPC npc = null, RebirthNpcExecutionLease lease = null)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        try
        {
            RebirthNpcActivityRegistry.TryReportResult(session.EntityId, session.ActivityId,
                session.LeaseId, session.AcceptedRevision, outcome, detail);
        }
        catch (Exception ex)
        {
            RebirthNpcWorkDiagnostics.RecordResultReportFault();
            Log.Warning("[REBIRTH NPC] Work result reporting failed for entity " + session.EntityId +
                " activity " + session.ActivityId + ": " + ex.Message);
        }
        finally
        {
            Remove(session, detail, npc, lease);
        }
    }

    private static EntityRebirthNPC ResolveNpc(int entityId)
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.World == null) return null;
        return manager.World.GetEntity(entityId) as EntityRebirthNPC;
    }

    private static void Stop(EntityRebirthNPC npc, string reason)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        IRebirthNpcMovementAdapter adapter = RebirthNpcMovementAdapterRegistry.Current;
        if (adapter == null || npc == null) return;
        try
        {
            adapter.Stop(npc, reason);
        }
        catch (Exception ex)
        {
            RebirthNpcWorkDiagnostics.RecordMovementAdapterFault();
            Log.Warning("[REBIRTH NPC] Work movement adapter Stop failed for entity " + npc.entityId +
                ": " + ex.Message);
        }
    }

    private static bool Remove(RebirthNpcWorkSession session, string reason,
        EntityRebirthNPC npc = null, RebirthNpcExecutionLease lease = null)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return false;

        RebirthNpcWorkSession detached;
        lock (Sync)
        {
            RebirthNpcWorkSession current;
            if (!Sessions.TryGetValue(session.EntityId, out current) ||
                current.ActivityId != session.ActivityId ||
                current.LeaseId != session.LeaseId ||
                current.AcceptedRevision != session.AcceptedRevision ||
                !ReferenceEquals(current.CallbackSync, session.CallbackSync))
                return false;
            detached = current;
            Sessions.Remove(session.EntityId);
        }
        SafeCleanupDetached(detached, reason, npc, lease, "session removal");
        return true;
    }

    private static void ForceDetach(RebirthNpcWorkSession session, string reason)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        RebirthNpcWorkSession detached;
        lock (Sync)
        {
            RebirthNpcWorkSession current;
            if (!Sessions.TryGetValue(session.EntityId, out current) ||
                current.ActivityId != session.ActivityId ||
                current.LeaseId != session.LeaseId ||
                current.AcceptedRevision != session.AcceptedRevision ||
                !ReferenceEquals(current.CallbackSync, session.CallbackSync))
                return;
            detached = current;
            Sessions.Remove(session.EntityId);
        }
        RebirthNpcWorkDiagnostics.RecordForcedSessionDetach();
        SafeCleanupDetached(detached, reason, ResolveNpc(detached.EntityId), detached.Lease,
            "forced session detachment");
    }

    private static void SafeCleanupDetached(RebirthNpcWorkSession session, string reason,
        EntityRebirthNPC npc, RebirthNpcExecutionLease lease, string lifecycleStage)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        try
        {
            CleanupDetached(session, reason, npc, lease);
        }
        catch (Exception ex)
        {
            RebirthNpcWorkDiagnostics.RecordLifecycleCleanupFault();
            try
            {
                RebirthNpcWorkTargetRegistry.Release(session.WorkPosition, session.EntityId,
                    session.ActivityId, session.LeaseId, session.AcceptedRevision);
            }
            catch (Exception releaseEx)
            {
                RebirthNpcWorkDiagnostics.RecordEmergencyCleanupFault();
                Log.Warning("[REBIRTH NPC] Work target release also failed during contained cleanup for entity " +
                    session.EntityId + ": " + releaseEx.Message);
            }
            Log.Warning("[REBIRTH NPC] Work cleanup fault was contained during " +
                (string.IsNullOrEmpty(lifecycleStage) ? "an unspecified lifecycle stage" : lifecycleStage) +
                " for entity " + session.EntityId + " activity " + session.ActivityId + ": " + ex.Message);
        }
    }

    private static void CleanupDetached(RebirthNpcWorkSession session, string reason,
        EntityRebirthNPC npc = null, RebirthNpcExecutionLease lease = null)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        object callbackSync = session.CallbackSync ?? session;
        lock (callbackSync)
        {
            RebirthNpcWorkTargetRegistry.Release(session.WorkPosition, session.EntityId, session.ActivityId,
                session.LeaseId, session.AcceptedRevision);
            if (session.Executor == null) return;
    
            RebirthNpcWorkExecutionDiagnostics.RecordEnd();
            bool usedRetainedContext = false;
            if (npc == null && GameManager.Instance != null && GameManager.Instance.World != null)
                npc = GameManager.Instance.World.GetEntity(session.EntityId) as EntityRebirthNPC;
            if (npc == null && session.Npc != null)
            {
                npc = session.Npc;
                usedRetainedContext = true;
            }
            if (lease == null && !RebirthNpcExecutionLeaseRegistry.TryGetActive(session.EntityId, out lease))
            {
                lease = session.Lease;
                if (lease != null) usedRetainedContext = true;
            }
            if (usedRetainedContext) RebirthNpcWorkDiagnostics.RecordRetainedCleanupContext();
            if (npc == null || lease == null)
            {
                RebirthNpcWorkDiagnostics.RecordCleanupContextUnavailable();
                Log.Warning("[REBIRTH NPC] Work executor End skipped because retained cleanup context was unavailable for entity " +
                    session.EntityId + " activity " + session.ActivityId + ".");
                return;
            }
    
            try
            {
                session.Executor.End(new RebirthNpcWorkContext(npc, lease, session.ActivityId,
                    session.WorkPosition), string.IsNullOrEmpty(reason) ? "Work execution session ended." : reason);
            }
            catch (Exception ex)
            {
                RebirthNpcWorkExecutionDiagnostics.RecordEndFault();
                RebirthNpcWorkExecutorHealthRegistry.RecordFault(session.Executor.ExecutorId, session.ExecutorGeneration, "End", ex.Message);
                Log.Warning("[REBIRTH NPC] Work executor End failed: " + ex.Message);
            }
        
        }
    }
    public static int DrainExecutor(string executorId, long generation, string reason)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return 0;

        if (string.IsNullOrWhiteSpace(executorId) || generation <= 0) return 0;
        bool enteredImmediately = Monitor.TryEnter(TickSweepSync);
        if (!enteredImmediately)
        {
            RebirthNpcWorkDiagnostics.RecordExecutorDrainWaitedForLifecycle();
            Monitor.Enter(TickSweepSync);
        }
        try
        {
            RebirthNpcWorkSession[] snapshot = GetSnapshot();
            int drained = 0;
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i].Executor == null || snapshot[i].ExecutorGeneration != generation ||
                    !string.Equals(snapshot[i].ExecutorId, executorId, StringComparison.Ordinal))
                    continue;
                EntityRebirthNPC npc = ResolveNpc(snapshot[i].EntityId);
                try
                {
                    if (Remove(snapshot[i], string.IsNullOrEmpty(reason) ?
                        "Work executor was unregistered." : reason, npc, snapshot[i].Lease))
                        drained++;
                }
                catch (Exception ex)
                {
                    RebirthNpcWorkDiagnostics.RecordLifecycleCleanupFault();
                    Log.Warning("[REBIRTH NPC] Work executor drain fault was contained for entity " +
                        snapshot[i].EntityId + ": " + ex.Message);
                    ForceDetach(snapshot[i], "Emergency detachment during executor unregistration.");
                }
            }
            return drained;
        }
        finally
        {
            Monitor.Exit(TickSweepSync);
        }
    }

    public static void ResetForWorldChange(string reason)
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled) return;

        bool enteredImmediately = Monitor.TryEnter(TickSweepSync);
        if (!enteredImmediately) Monitor.Enter(TickSweepSync);

        try
        {
            RebirthNpcWorkDiagnostics.ResetForWorldChange();
            RebirthNpcWorkSession[] snapshot = GetSnapshot();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    EntityRebirthNPC npc = ResolveNpc(snapshot[i].EntityId);
                    if (!Remove(snapshot[i], string.IsNullOrEmpty(reason) ?
                        "World lifecycle reset." : reason, npc, snapshot[i].Lease))
                        RebirthNpcWorkDiagnostics.RecordResetSessionAlreadyDetached();
                }
                catch (Exception ex)
                {
                    RebirthNpcWorkDiagnostics.RecordLifecycleCleanupFault();
                    Log.Warning("[REBIRTH NPC] Work world-reset cleanup fault was contained for entity " +
                        snapshot[i].EntityId + ": " + ex.Message);
                    ForceDetach(snapshot[i], "Emergency detachment during world lifecycle reset.");
                }
            }

            int discarded;
            lock (Sync)
            {
                discarded = Sessions.Count;
                Sessions.Clear();
                nextTickClockTicks = 0;
            }
            if (discarded > 0) RebirthNpcWorkDiagnostics.RecordResetSessionsDiscarded(discarded);

            TryResetComponent("executor resolution cache",
                RebirthNpcWorkExecutorRegistry.ResetResolutionCacheForWorldChange);
            TryResetComponent("work target registry", RebirthNpcWorkTargetRegistry.ResetForWorldChange);
            TryResetComponent("executor health registry", RebirthNpcWorkExecutorHealthRegistry.ResetForWorldChange);
            TryResetComponent("execution diagnostics", RebirthNpcWorkExecutionDiagnostics.ResetForWorldChange);

            bool waited = !enteredImmediately;
            RebirthNpcWorkDiagnostics.RecordWorldReset();
            if (waited) RebirthNpcWorkDiagnostics.RecordWorldResetWaitedForTickSweep();
        }
        finally
        {
            Monitor.Exit(TickSweepSync);
        }
    }

    private static void TryResetComponent(string componentName, Action reset)
    {
        try
        {
            reset();
        }
        catch (Exception ex)
        {
            RebirthNpcWorkDiagnostics.RecordResetComponentFault();
            Log.Warning("[REBIRTH NPC] Work world-reset component fault was contained for " +
                componentName + ": " + ex.Message);
        }
    }

    public static RebirthNpcWorkSession[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcWorkSession[] result = new RebirthNpcWorkSession[Sessions.Count];
            Sessions.Values.CopyTo(result, 0);
            Array.Sort(result, (left, right) => left.EntityId.CompareTo(right.EntityId));
            return result;
        }
    }
}

public enum RebirthNpcWorkSessionMode : byte { Navigating = 0, Executing = 1 }

public struct RebirthNpcWorkSession
{
    public int EntityId;
    public EntityRebirthNPC Npc;
    public ulong LeaseId;
    public ulong ActivityId;
    public uint AcceptedRevision;
    public Vector3 WorkPosition;
    public RebirthNpcWorkSessionMode Mode;
    public IRebirthNpcWorkExecutor Executor;
    public string ExecutorId;
    public long ExecutorGeneration;
    public RebirthNpcExecutionLease Lease;
    public object CallbackSync;
    public long StartedClockTicks;
}

public static class RebirthNpcWorkDiagnostics
{
    private static long navigationAttached, executionAttached, recoveries, moveRequests,
        alreadySatisfied, handoffs, leftRadius, executorTicks, sessionReplacements,
        startupRollbacks, retainedCleanupContexts, cleanupContextUnavailable,
        staleTicksRejected, tickSweeps, overlappingTickSweepsRejected,
        worldResets, worldResetsWaitedForTickSweep, attachmentWaitsForLifecycle,
        executorDrainsWaitedForLifecycle, sessionTickFaultsContained,
        emergencyCleanupFaults, movementAdapterFaults, heartbeatFaults,
        resultReportFaults, lifecycleCleanupFaults, forcedSessionDetaches,
        resetComponentFaults, resetSessionsDiscarded, resetSessionsAlreadyDetached;
    internal static void RecordAttached(bool navigation, bool recovery)
    { if (navigation) navigationAttached++; else executionAttached++; if (recovery) recoveries++; }
    internal static void RecordMoveRequest(RebirthNpcMovementRequestResult result)
    { if (result == RebirthNpcMovementRequestResult.Accepted) moveRequests++; else if (result == RebirthNpcMovementRequestResult.AlreadySatisfied) alreadySatisfied++; }
    internal static void RecordHandoff() { handoffs++; }
    internal static void RecordLeftRadius() { leftRadius++; }
    internal static void RecordExecutorTick() { executorTicks++; }
    internal static void RecordSessionReplacement() { sessionReplacements++; }
    internal static void RecordStartupRollback() { startupRollbacks++; }
    internal static void RecordRetainedCleanupContext() { retainedCleanupContexts++; }
    internal static void RecordCleanupContextUnavailable() { cleanupContextUnavailable++; }
    internal static void RecordStaleTickRejected() { staleTicksRejected++; }
    internal static void RecordTickSweep() { tickSweeps++; }
    internal static void RecordOverlappingTickSweepRejected() { overlappingTickSweepsRejected++; }
    internal static void RecordWorldReset() { worldResets++; }
    internal static void RecordWorldResetWaitedForTickSweep() { worldResetsWaitedForTickSweep++; }
    internal static void RecordAttachmentWaitedForLifecycle() { attachmentWaitsForLifecycle++; }
    internal static void RecordExecutorDrainWaitedForLifecycle() { executorDrainsWaitedForLifecycle++; }
    internal static void RecordSessionTickFaultContained() { sessionTickFaultsContained++; }
    internal static void RecordEmergencyCleanupFault() { emergencyCleanupFaults++; }
    internal static void RecordMovementAdapterFault() { movementAdapterFaults++; }
    internal static void RecordHeartbeatFault() { heartbeatFaults++; }
    internal static void RecordResultReportFault() { resultReportFaults++; }
    internal static void RecordLifecycleCleanupFault() { lifecycleCleanupFaults++; }
    internal static void RecordForcedSessionDetach() { forcedSessionDetaches++; }
    internal static void RecordResetComponentFault() { resetComponentFaults++; }
    internal static void RecordResetSessionsDiscarded(int count) { if (count > 0) resetSessionsDiscarded += count; }
    internal static void RecordResetSessionAlreadyDetached() { resetSessionsAlreadyDetached++; }
    public static void ResetForWorldChange()
    {
        navigationAttached = executionAttached = recoveries = moveRequests = 0;
        alreadySatisfied = handoffs = leftRadius = executorTicks = 0;
        sessionReplacements = startupRollbacks = 0;
        retainedCleanupContexts = cleanupContextUnavailable = staleTicksRejected = 0;
        tickSweeps = overlappingTickSweepsRejected = 0;
        worldResets = worldResetsWaitedForTickSweep = 0;
        attachmentWaitsForLifecycle = executorDrainsWaitedForLifecycle = 0;
        sessionTickFaultsContained = emergencyCleanupFaults = movementAdapterFaults = 0;
        heartbeatFaults = resultReportFaults = 0;
        lifecycleCleanupFaults = forcedSessionDetaches = resetComponentFaults = 0;
        resetSessionsDiscarded = resetSessionsAlreadyDetached = 0;
    }

    public static string GetReport()
    {
        RebirthNpcWorkSession[] sessions = RebirthNpcWorkRuntime.GetSnapshot();
        StringBuilder b = new StringBuilder();
        b.Append(RebirthNpcWorkExecutorRegistry.GetReport()).AppendLine()
            .Append(RebirthNpcWorkTargetRegistry.GetReport()).AppendLine()
            .Append(RebirthNpcWorkExecutionDiagnostics.GetReport()).AppendLine()
            .Append(RebirthNpcWorkExecutorHealthRegistry.GetReport()).AppendLine()
            .Append("[REBIRTH NPC] work active=").Append(sessions.Length)
            .Append(" navigationAttached=").Append(navigationAttached)
            .Append(" executionAttached=").Append(executionAttached)
            .Append(" recoveries=").Append(recoveries)
            .Append(" moveRequests=").Append(moveRequests)
            .Append(" alreadySatisfied=").Append(alreadySatisfied)
            .Append(" handoffs=").Append(handoffs)
            .Append(" leftRadius=").Append(leftRadius)
            .Append(" executorTicks=").Append(executorTicks)
            .Append(" sessionReplacements=").Append(sessionReplacements)
            .Append(" startupRollbacks=").Append(startupRollbacks)
            .Append(" retainedCleanupContexts=").Append(retainedCleanupContexts)
            .Append(" cleanupContextUnavailable=").Append(cleanupContextUnavailable)
            .Append(" staleTicksRejected=").Append(staleTicksRejected)
            .Append(" tickSweeps=").Append(tickSweeps)
            .Append(" overlappingTickSweepsRejected=").Append(overlappingTickSweepsRejected)
            .Append(" worldResets=").Append(worldResets)
            .Append(" worldResetsWaitedForTickSweep=").Append(worldResetsWaitedForTickSweep)
            .Append(" attachmentWaitsForLifecycle=").Append(attachmentWaitsForLifecycle)
            .Append(" executorDrainsWaitedForLifecycle=").Append(executorDrainsWaitedForLifecycle)
            .Append(" sessionTickFaultsContained=").Append(sessionTickFaultsContained)
            .Append(" emergencyCleanupFaults=").Append(emergencyCleanupFaults)
            .Append(" movementAdapterFaults=").Append(movementAdapterFaults)
            .Append(" heartbeatFaults=").Append(heartbeatFaults)
            .Append(" resultReportFaults=").Append(resultReportFaults)
            .Append(" lifecycleCleanupFaults=").Append(lifecycleCleanupFaults)
            .Append(" forcedSessionDetaches=").Append(forcedSessionDetaches)
            .Append(" resetComponentFaults=").Append(resetComponentFaults)
            .Append(" resetSessionsDiscarded=").Append(resetSessionsDiscarded)
            .Append(" resetSessionsAlreadyDetached=").Append(resetSessionsAlreadyDetached).AppendLine();
        for (int i = 0; i < sessions.Length; i++)
            b.Append("  entity=").Append(sessions[i].EntityId)
                .Append(" activity=").Append(sessions[i].ActivityId)
                .Append(" lease=").Append(sessions[i].LeaseId)
                .Append(" mode=").Append(sessions[i].Mode)
                .Append(" position=").Append(sessions[i].WorkPosition)
                .Append(" executor=").Append(sessions[i].Executor == null ? "-" : sessions[i].Executor.ExecutorId)
                .Append(" executorGeneration=").Append(sessions[i].ExecutorGeneration)
                .Append(" ageSeconds=").Append(Math.Max(0L, (RebirthNpcWorkClock.NowTicks - sessions[i].StartedClockTicks) / TimeSpan.TicksPerSecond))
                .AppendLine();
        return b.ToString().TrimEnd();
    }
}
