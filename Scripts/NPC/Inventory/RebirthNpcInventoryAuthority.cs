using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

#nullable disable

[Flags]
public enum RebirthNpcInventoryAuthorityOperations : byte
{
    None = 0,
    Mutate = 1 << 0,
    Transfer = 1 << 1,
    Reserve = 1 << 2,
    All = Mutate | Transfer | Reserve
}

public sealed class RebirthNpcInventoryAuthorityLease : IDisposable
{
    public string AuthorityKey { get; }
    private int disposed;

    internal RebirthNpcInventoryAuthorityLease(string authorityKey)
    {
        AuthorityKey = authorityKey;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            RebirthNpcInventoryAuthorityService.Revoke(AuthorityKey);
    }
}

public static class RebirthNpcInventoryAuthorityService
{
    private sealed class AuthorityRecord
    {
        public string Subsystem;
        public RebirthNpcInventoryAuthorityOperations Operations;
        public HashSet<RebirthNpcStableId> AllowedNpcIds;
        public long ExpiresClockTicks;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<string, AuthorityRecord> Records =
        new Dictionary<string, AuthorityRecord>(StringComparer.Ordinal);
    private static long issued;
    private static long accepted;
    private static long rejected;
    private static long expired;
    private static long revoked;

    public static RebirthNpcInventoryAuthorityLease Issue(string subsystem,
        RebirthNpcInventoryAuthorityOperations operations, TimeSpan lifetime,
        params RebirthNpcStableId[] allowedNpcIds)
    {
        subsystem = (subsystem ?? string.Empty).Trim();
        if (subsystem.Length == 0) throw new ArgumentException("Subsystem is required.", nameof(subsystem));
        if (operations == RebirthNpcInventoryAuthorityOperations.None)
            throw new ArgumentOutOfRangeException(nameof(operations));
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(lifetime));

        string key = "rbnpc-inventory:" + Guid.NewGuid().ToString("N");
        HashSet<RebirthNpcStableId> scope = null;
        if (allowedNpcIds != null && allowedNpcIds.Length > 0)
        {
            scope = new HashSet<RebirthNpcStableId>();
            for (int i = 0; i < allowedNpcIds.Length; i++)
            {
                if (allowedNpcIds[i].IsEmpty)
                    throw new ArgumentException("Authority scope contains an empty NPC identity.", nameof(allowedNpcIds));
                scope.Add(allowedNpcIds[i]);
            }
        }

        lock (Sync)
        {
            PruneExpiredLocked();
            Records.Add(key, new AuthorityRecord
            {
                Subsystem = subsystem,
                Operations = operations,
                AllowedNpcIds = scope,
                ExpiresClockTicks = NowTicks() + lifetime.Ticks
            });
        }
        Interlocked.Increment(ref issued);
        return new RebirthNpcInventoryAuthorityLease(key);
    }

    public static bool Validate(string authorityKey, RebirthNpcInventoryAuthorityOperations operation,
        RebirthNpcStableId firstNpcId, RebirthNpcStableId secondNpcId = default(RebirthNpcStableId))
    {
        if (string.IsNullOrWhiteSpace(authorityKey) || operation == RebirthNpcInventoryAuthorityOperations.None ||
            firstNpcId.IsEmpty)
        {
            Interlocked.Increment(ref rejected);
            return false;
        }

        lock (Sync)
        {
            AuthorityRecord record;
            if (!Records.TryGetValue(authorityKey, out record))
            {
                Interlocked.Increment(ref rejected);
                return false;
            }
            if (record.ExpiresClockTicks <= NowTicks())
            {
                Records.Remove(authorityKey);
                Interlocked.Increment(ref expired);
                Interlocked.Increment(ref rejected);
                return false;
            }
            if ((record.Operations & operation) != operation ||
                !Allows(record, firstNpcId) || (!secondNpcId.IsEmpty && !Allows(record, secondNpcId)))
            {
                Interlocked.Increment(ref rejected);
                return false;
            }
        }

        Interlocked.Increment(ref accepted);
        return true;
    }

    public static void Revoke(string authorityKey)
    {
        if (string.IsNullOrEmpty(authorityKey)) return;
        lock (Sync)
        {
            if (Records.Remove(authorityKey)) Interlocked.Increment(ref revoked);
        }
    }

    public static void Reset()
    {
        lock (Sync) Records.Clear();
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            PruneExpiredLocked();
            return "[REBIRTH NPC Inventory Authority] active=" + Records.Count +
                " issued=" + Interlocked.Read(ref issued) +
                " accepted=" + Interlocked.Read(ref accepted) +
                " rejected=" + Interlocked.Read(ref rejected) +
                " expired=" + Interlocked.Read(ref expired) +
                " revoked=" + Interlocked.Read(ref revoked);
        }
    }

    private static bool Allows(AuthorityRecord record, RebirthNpcStableId npcId)
    {
        return record.AllowedNpcIds == null || record.AllowedNpcIds.Contains(npcId);
    }

    private static void PruneExpiredLocked()
    {
        if (Records.Count == 0) return;
        long now = NowTicks();
        List<string> remove = null;
        foreach (KeyValuePair<string, AuthorityRecord> pair in Records)
        {
            if (pair.Value.ExpiresClockTicks > now) continue;
            if (remove == null) remove = new List<string>();
            remove.Add(pair.Key);
        }
        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++) Records.Remove(remove[i]);
        Interlocked.Add(ref expired, remove.Count);
    }

    private static long NowTicks()
    {
        return (long)(Stopwatch.GetTimestamp() * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
    }
}
