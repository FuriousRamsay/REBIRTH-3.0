using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

internal static class RebirthNpcWorkClock
{
    private static readonly long Frequency = Stopwatch.Frequency;

    public static long NowTicks
    {
        get
        {
            long timestamp = Stopwatch.GetTimestamp();
            long seconds = timestamp / Frequency;
            long remainder = timestamp % Frequency;
            return checked(seconds * TimeSpan.TicksPerSecond +
                remainder * TimeSpan.TicksPerSecond / Frequency);
        }
    }
}

public enum RebirthNpcWorkValidationCode : byte
{
    Valid = 0,
    InvalidNpc = 1,
    InvalidLease = 2,
    MissingTarget = 3,
    TargetBusy = 4,
    ExecutorRejected = 5
}

public struct RebirthNpcWorkValidationResult
{
    public RebirthNpcWorkValidationCode Code;
    public string Detail;
    public bool IsValid => Code == RebirthNpcWorkValidationCode.Valid;
    public static RebirthNpcWorkValidationResult Valid(string detail = "Validated.") =>
        new RebirthNpcWorkValidationResult { Code = RebirthNpcWorkValidationCode.Valid, Detail = detail };
    public static RebirthNpcWorkValidationResult Reject(RebirthNpcWorkValidationCode code, string detail) =>
        new RebirthNpcWorkValidationResult { Code = code, Detail = detail };
}

public interface IRebirthNpcValidatedWorkExecutor
{
    RebirthNpcWorkValidationResult Validate(RebirthNpcWorkContext context);
}

public interface IRebirthNpcRecoverableWorkExecutor
{
    void Recover(RebirthNpcWorkContext context);
}

public interface IRebirthNpcWorkExecutionPolicy
{
    int MaximumExecutionSeconds { get; }
    int SlowTickWarningMilliseconds { get; }
}

public readonly struct RebirthNpcWorkTargetKey : IEquatable<RebirthNpcWorkTargetKey>
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;
    public RebirthNpcWorkTargetKey(Vector3 position)
    {
        X = Mathf.RoundToInt(position.x * 2f);
        Y = Mathf.RoundToInt(position.y * 2f);
        Z = Mathf.RoundToInt(position.z * 2f);
    }
    public bool Equals(RebirthNpcWorkTargetKey other) => X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object obj) => obj is RebirthNpcWorkTargetKey && Equals((RebirthNpcWorkTargetKey)obj);
    public override int GetHashCode() { unchecked { return ((X * 397) ^ Y) * 397 ^ Z; } }
    public override string ToString() => X + ":" + Y + ":" + Z;
}

public static class RebirthNpcWorkTargetRegistry
{
    private static readonly long OwnershipTimeoutTicks = TimeSpan.FromSeconds(15).Ticks;
    private sealed class Ownership
    {
        public int EntityId;
        public ulong ActivityId;
        public ulong LeaseId;
        public uint AcceptedRevision;
        public long AcquiredClockTicks;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcWorkTargetKey, Ownership> Owners =
        new Dictionary<RebirthNpcWorkTargetKey, Ownership>();
    private static long acquisitions, renewals, renewalFailures, conflicts, releases, releaseRejects, staleReclaims;

    public static bool TryAcquire(Vector3 position, int entityId, ulong activityId, ulong leaseId, uint acceptedRevision, out string detail)
    {
        RebirthNpcWorkTargetKey key = new RebirthNpcWorkTargetKey(position);
        lock (Sync)
        {
            long now = RebirthNpcWorkClock.NowTicks;
            Ownership owner;
            if (Owners.TryGetValue(key, out owner))
            {
                if (now - owner.AcquiredClockTicks > OwnershipTimeoutTicks)
                {
                    Owners.Remove(key);
                    staleReclaims++;
                }
                else if (owner.EntityId == entityId && owner.ActivityId == activityId && owner.LeaseId == leaseId &&
                    owner.AcceptedRevision == acceptedRevision)
                {
                    owner.AcquiredClockTicks = now;
                    renewals++;
                    detail = "Work target ownership already held and renewed.";
                    return true;
                }
                else
                {
                    conflicts++;
                    detail = "Work target is owned by entity " + owner.EntityId + ".";
                    return false;
                }
            }
            Owners[key] = new Ownership { EntityId = entityId, ActivityId = activityId,
                LeaseId = leaseId, AcceptedRevision = acceptedRevision, AcquiredClockTicks = now };
            acquisitions++;
            detail = "Work target ownership acquired.";
            return true;
        }
    }


    public static bool TryRenew(Vector3 position, int entityId, ulong activityId, ulong leaseId, uint acceptedRevision, out string detail)
    {
        RebirthNpcWorkTargetKey key = new RebirthNpcWorkTargetKey(position);
        lock (Sync)
        {
            Ownership owner;
            if (!Owners.TryGetValue(key, out owner))
            {
                renewalFailures++;
                detail = "Work target ownership no longer exists.";
                return false;
            }
            long now = RebirthNpcWorkClock.NowTicks;
            if (now - owner.AcquiredClockTicks > OwnershipTimeoutTicks)
            {
                Owners.Remove(key);
                staleReclaims++;
                renewalFailures++;
                detail = "Work target ownership expired before renewal.";
                return false;
            }
            if (owner.EntityId != entityId || owner.ActivityId != activityId || owner.LeaseId != leaseId ||
                owner.AcceptedRevision != acceptedRevision)
            {
                renewalFailures++;
                detail = "Work target ownership belongs to another execution session.";
                return false;
            }
            owner.AcquiredClockTicks = now;
            renewals++;
            detail = "Work target ownership renewed.";
            return true;
        }
    }

    public static bool Release(Vector3 position, int entityId, ulong activityId, ulong leaseId, uint acceptedRevision)
    {
        RebirthNpcWorkTargetKey key = new RebirthNpcWorkTargetKey(position);
        lock (Sync)
        {
            Ownership owner;
            if (!Owners.TryGetValue(key, out owner) || owner.EntityId != entityId ||
                owner.ActivityId != activityId || owner.LeaseId != leaseId ||
                owner.AcceptedRevision != acceptedRevision)
            {
                releaseRejects++;
                return false;
            }
            Owners.Remove(key);
            releases++;
            return true;
        }
    }

    public static void Reconcile(RebirthNpcWorkSession[] activeSessions)
    {
        lock (Sync)
        {
            long now = RebirthNpcWorkClock.NowTicks;
            List<RebirthNpcWorkTargetKey> remove = null;
            foreach (KeyValuePair<RebirthNpcWorkTargetKey, Ownership> pair in Owners)
            {
                Ownership owner = pair.Value;
                bool active = false;
                if (now - owner.AcquiredClockTicks <= OwnershipTimeoutTicks && activeSessions != null)
                {
                    for (int i = 0; i < activeSessions.Length; i++)
                    {
                        RebirthNpcWorkSession session = activeSessions[i];
                        if (session.Mode != RebirthNpcWorkSessionMode.Executing ||
                            session.EntityId != owner.EntityId || session.ActivityId != owner.ActivityId ||
                            session.LeaseId != owner.LeaseId || session.AcceptedRevision != owner.AcceptedRevision ||
                            !new RebirthNpcWorkTargetKey(session.WorkPosition).Equals(pair.Key))
                            continue;
                        active = true;
                        break;
                    }
                }
                if (active) continue;
                if (remove == null) remove = new List<RebirthNpcWorkTargetKey>();
                remove.Add(pair.Key);
            }
            if (remove == null) return;
            for (int i = 0; i < remove.Count; i++) Owners.Remove(remove[i]);
            staleReclaims += remove.Count;
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync)
        {
            Owners.Clear();
            acquisitions = renewals = renewalFailures = conflicts = releases = releaseRejects = staleReclaims = 0;
        }
    }

    public static string GetReport()
    {
        lock (Sync)
            return "[REBIRTH NPC] work targets active=" + Owners.Count + " acquisitions=" + acquisitions +
                " renewals=" + renewals + " renewalFailures=" + renewalFailures +
                " conflicts=" + conflicts + " releases=" + releases + " releaseRejects=" + releaseRejects +
                " staleReclaims=" + staleReclaims;
    }
}


public static class RebirthNpcWorkExecutorHealthRegistry
{
    private const int FaultThreshold = 3;
    private static readonly long FaultWindowTicks = TimeSpan.FromSeconds(60).Ticks;
    private static readonly long QuarantineTicks = TimeSpan.FromSeconds(30).Ticks;

    private readonly struct HealthKey : IEquatable<HealthKey>
    {
        public readonly string ExecutorId;
        public readonly long Generation;

        public HealthKey(string executorId, long generation)
        {
            ExecutorId = executorId;
            Generation = generation;
        }

        public bool Equals(HealthKey other) => Generation == other.Generation &&
            string.Equals(ExecutorId, other.ExecutorId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is HealthKey && Equals((HealthKey)obj);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((ExecutorId == null ? 0 : StringComparer.Ordinal.GetHashCode(ExecutorId)) * 397) ^
                    Generation.GetHashCode();
            }
        }
    }

    private sealed class Health
    {
        public int FaultsInWindow;
        public long WindowStartedClockTicks;
        public long QuarantineUntilClockTicks;
        public long TotalFaults;
        public long Successes;
        public long Quarantines;
        public long Recoveries;
        public string LastFaultStage;
        public string LastFaultDetail;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<HealthKey, Health> Entries =
        new Dictionary<HealthKey, Health>();

    public static bool IsAvailable(string executorId, long generation, out string detail)
    {
        detail = "Executor is available.";
        if (string.IsNullOrWhiteSpace(executorId) || generation <= 0) return false;
        lock (Sync)
        {
            Health health;
            if (!Entries.TryGetValue(new HealthKey(executorId, generation), out health)) return true;
            long now = RebirthNpcWorkClock.NowTicks;
            if (health.QuarantineUntilClockTicks <= 0) return true;
            if (now < health.QuarantineUntilClockTicks)
            {
                long seconds = Math.Max(1L,
                    (health.QuarantineUntilClockTicks - now + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
                detail = "Executor generation is quarantined for approximately " + seconds + " more seconds.";
                return false;
            }
            health.QuarantineUntilClockTicks = 0;
            health.FaultsInWindow = 0;
            health.WindowStartedClockTicks = now;
            health.Recoveries++;
            detail = "Executor-generation quarantine expired; executor is eligible for a recovery attempt.";
            return true;
        }
    }

    public static void RecordSuccess(string executorId, long generation)
    {
        if (string.IsNullOrWhiteSpace(executorId) || generation <= 0) return;
        lock (Sync)
        {
            HealthKey key = new HealthKey(executorId, generation);
            Health health;
            if (!Entries.TryGetValue(key, out health))
            {
                health = new Health { WindowStartedClockTicks = RebirthNpcWorkClock.NowTicks };
                Entries.Add(key, health);
            }
            health.Successes++;
            if (health.FaultsInWindow > 0) health.FaultsInWindow--;
        }
    }

    public static void RecordFault(string executorId, long generation, string stage, string detail)
    {
        if (string.IsNullOrWhiteSpace(executorId) || generation <= 0) return;
        lock (Sync)
        {
            long now = RebirthNpcWorkClock.NowTicks;
            HealthKey key = new HealthKey(executorId, generation);
            Health health;
            if (!Entries.TryGetValue(key, out health))
            {
                health = new Health { WindowStartedClockTicks = now };
                Entries.Add(key, health);
            }
            if (health.WindowStartedClockTicks <= 0 || now - health.WindowStartedClockTicks > FaultWindowTicks)
            {
                health.WindowStartedClockTicks = now;
                health.FaultsInWindow = 0;
            }
            health.FaultsInWindow++;
            health.TotalFaults++;
            health.LastFaultStage = string.IsNullOrEmpty(stage) ? "unknown" : stage;
            health.LastFaultDetail = string.IsNullOrEmpty(detail) ? "No detail." : detail;
            if (health.FaultsInWindow >= FaultThreshold)
            {
                health.QuarantineUntilClockTicks = now + QuarantineTicks;
                health.Quarantines++;
            }
        }
    }

    public static void Remove(string executorId, long generation)
    {
        if (string.IsNullOrWhiteSpace(executorId) || generation <= 0) return;
        lock (Sync) Entries.Remove(new HealthKey(executorId, generation));
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) Entries.Clear();
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder b = new StringBuilder("[REBIRTH NPC] work executor health tracked=")
                .Append(Entries.Count).AppendLine();
            long now = RebirthNpcWorkClock.NowTicks;
            foreach (KeyValuePair<HealthKey, Health> pair in Entries)
            {
                long remaining = pair.Value.QuarantineUntilClockTicks > now ?
                    (pair.Value.QuarantineUntilClockTicks - now + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond : 0;
                b.Append("  id=").Append(pair.Key.ExecutorId)
                    .Append(" generation=").Append(pair.Key.Generation)
                    .Append(" faultsInWindow=").Append(pair.Value.FaultsInWindow)
                    .Append(" totalFaults=").Append(pair.Value.TotalFaults)
                    .Append(" successes=").Append(pair.Value.Successes)
                    .Append(" quarantines=").Append(pair.Value.Quarantines)
                    .Append(" recoveries=").Append(pair.Value.Recoveries)
                    .Append(" quarantineRemainingSeconds=").Append(remaining)
                    .Append(" lastStage=").Append(pair.Value.LastFaultStage ?? "-")
                    .Append(" lastDetail=").Append(pair.Value.LastFaultDetail ?? "-")
                    .AppendLine();
            }
            return b.ToString().TrimEnd();
        }
    }
}

public static class RebirthNpcWorkExecutionDiagnostics
{
    private static long validations, validationFailures, begins, recovers, ends, faults, endFaults, timeouts, slowTicks;
    internal static void RecordValidation(bool success) { Interlocked.Increment(ref validations); if (!success) Interlocked.Increment(ref validationFailures); }
    internal static void RecordBegin(bool recovery) { if (recovery) Interlocked.Increment(ref recovers); else Interlocked.Increment(ref begins); }
    internal static void RecordEnd() { Interlocked.Increment(ref ends); }
    internal static void RecordEndFault() { Interlocked.Increment(ref endFaults); }
    internal static void RecordTimeout() { Interlocked.Increment(ref timeouts); }
    internal static void RecordSlowTick() { Interlocked.Increment(ref slowTicks); }
    internal static void RecordFault() { Interlocked.Increment(ref faults); }
    public static void ResetForWorldChange()
    {
        Interlocked.Exchange(ref validations, 0);
        Interlocked.Exchange(ref validationFailures, 0);
        Interlocked.Exchange(ref begins, 0);
        Interlocked.Exchange(ref recovers, 0);
        Interlocked.Exchange(ref ends, 0);
        Interlocked.Exchange(ref faults, 0);
        Interlocked.Exchange(ref endFaults, 0);
        Interlocked.Exchange(ref timeouts, 0);
        Interlocked.Exchange(ref slowTicks, 0);
    }
    public static string GetReport() => new StringBuilder("[REBIRTH NPC] work execution lifecycle validations=")
        .Append(validations).Append(" validationFailures=").Append(validationFailures)
        .Append(" begins=").Append(begins).Append(" recovers=").Append(recovers)
        .Append(" ends=").Append(ends).Append(" endFaults=").Append(endFaults)
        .Append(" timeouts=").Append(timeouts).Append(" slowTicks=").Append(slowTicks)
        .Append(" faults=").Append(faults).ToString();
}
