using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#nullable disable

[Flags]
public enum RebirthNpcWorkCapability : ulong
{
    None = 0,
    Farming = 1UL << 0,
    Mining = 1UL << 1,
    Hauling = 1UL << 2,
    Repair = 1UL << 3,
    Crafting = 1UL << 4,
    Workstation = 1UL << 5,
    Harvesting = 1UL << 6,
    Construction = 1UL << 7,
    GuardDuty = 1UL << 8,
    Medical = 1UL << 9,
    Scavenging = 1UL << 10
}

public enum RebirthNpcWorkKind : byte
{
    Farming = 0,
    Mining = 1,
    Hauling = 2,
    Repair = 3,
    Crafting = 4,
    Workstation = 5,
    Harvesting = 6,
    Construction = 7,
    GuardDuty = 8,
    Medical = 9,
    Scavenging = 10
}

public enum RebirthNpcWorkAssignmentStatus : byte
{
    Pending = 0,
    Scheduled = 1,
    Ready = 2,
    Reserved = 3,
    Executing = 4,
    Suspended = 5,
    Completed = 6,
    Cancelled = 7,
    Failed = 8
}

public enum RebirthNpcWorkAssignmentResult : byte
{
    Succeeded = 0,
    InvalidRequest = 1,
    NpcUnavailable = 2,
    PermissionDenied = 3,
    CapabilityDenied = 4,
    ScheduleDenied = 5,
    Conflict = 6,
    NotFound = 7,
    RevisionConflict = 8,
    ReservationDenied = 9
}

public sealed class RebirthNpcWorkDefinition
{
    public string DefinitionId { get; private set; }
    public RebirthNpcWorkKind Kind { get; private set; }
    public RebirthNpcWorkCapability RequiredCapability { get; private set; }
    public int Priority { get; private set; }
    public int MaxWorkersPerTarget { get; private set; }
    public TimeSpan ReservationLifetime { get; private set; }
    public bool AllowsPreemption { get; private set; }

    public RebirthNpcWorkDefinition(string definitionId, RebirthNpcWorkKind kind,
        RebirthNpcWorkCapability requiredCapability, int priority, int maxWorkersPerTarget,
        TimeSpan reservationLifetime, bool allowsPreemption)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
            throw new ArgumentException("Work definition id is required.", nameof(definitionId));
        if (requiredCapability == RebirthNpcWorkCapability.None)
            throw new ArgumentException("A work capability is required.", nameof(requiredCapability));
        DefinitionId = definitionId.Trim();
        Kind = kind;
        RequiredCapability = requiredCapability;
        Priority = priority;
        MaxWorkersPerTarget = Math.Max(1, maxWorkersPerTarget);
        ReservationLifetime = reservationLifetime <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(30) : reservationLifetime;
        AllowsPreemption = allowsPreemption;
    }
}

public static class RebirthNpcWorkDefinitionRegistry
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthNpcWorkDefinition> Definitions =
        new Dictionary<string, RebirthNpcWorkDefinition>(StringComparer.OrdinalIgnoreCase);

    public static void Register(RebirthNpcWorkDefinition definition, bool replace)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        lock (Sync)
        {
            if (!replace && Definitions.ContainsKey(definition.DefinitionId))
                throw new InvalidOperationException("Work definition already registered: " +
                    definition.DefinitionId);
            Definitions[definition.DefinitionId] = definition;
        }
    }

    public static bool TryGet(string definitionId, out RebirthNpcWorkDefinition definition)
    {
        lock (Sync) return Definitions.TryGetValue(definitionId ?? string.Empty, out definition);
    }

    public static RebirthNpcWorkDefinition[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcWorkDefinition[] result = new RebirthNpcWorkDefinition[Definitions.Count];
            Definitions.Values.CopyTo(result, 0);
            Array.Sort(result, (a, b) => string.Compare(a.DefinitionId, b.DefinitionId,
                StringComparison.OrdinalIgnoreCase));
            return result;
        }
    }

    public static void Reset() { lock (Sync) Definitions.Clear(); }
    public static int Count { get { lock (Sync) return Definitions.Count; } }
}

public sealed class RebirthNpcWorkCapabilityContext
{
    public RebirthNpcStableId NpcId { get; set; }
    public string DefinitionId { get; set; }
    public RebirthNpcWorkCapability Required { get; set; }
    public string ActorId { get; set; }
}

public interface IRebirthNpcWorkCapabilityProvider
{
    int Priority { get; }
    RebirthNpcWorkCapability Resolve(RebirthNpcStableId npcId);
    bool CanPerform(RebirthNpcWorkCapabilityContext context, out string denialReason);
}

public static class RebirthNpcWorkCapabilityService
{
    private static readonly object Sync = new object();
    private static readonly List<IRebirthNpcWorkCapabilityProvider> Providers =
        new List<IRebirthNpcWorkCapabilityProvider>();
    private static long checks, denied, providerFaults;

    public static void Register(IRebirthNpcWorkCapabilityProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        lock (Sync)
        {
            if (!Providers.Contains(provider)) Providers.Add(provider);
            Providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    public static void Unregister(IRebirthNpcWorkCapabilityProvider provider)
    {
        if (provider == null) return;
        lock (Sync) Providers.Remove(provider);
    }

    public static bool CanPerform(RebirthNpcStableId npcId, RebirthNpcWorkDefinition definition,
        string actorId, out RebirthNpcWorkCapability available, out string denialReason)
    {
        available = RebirthNpcWorkCapability.None;
        denialReason = string.Empty;
        Interlocked.Increment(ref checks);
        IRebirthNpcWorkCapabilityProvider[] snapshot;
        lock (Sync) snapshot = Providers.ToArray();
        RebirthNpcWorkCapabilityContext context = new RebirthNpcWorkCapabilityContext
        {
            NpcId = npcId,
            DefinitionId = definition.DefinitionId,
            Required = definition.RequiredCapability,
            ActorId = actorId ?? string.Empty
        };
        for (int i = 0; i < snapshot.Length; i++)
        {
            try
            {
                available |= snapshot[i].Resolve(npcId);
                string reason;
                if (!snapshot[i].CanPerform(context, out reason))
                {
                    denialReason = string.IsNullOrEmpty(reason)
                        ? "A capability provider denied this work assignment." : reason;
                    Interlocked.Increment(ref denied);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref providerFaults);
                Log.Warning("[REBIRTH NPC Work] Capability provider failed: " + ex.Message);
            }
        }
        if ((available & definition.RequiredCapability) != definition.RequiredCapability)
        {
            denialReason = "NPC lacks required capability " + definition.RequiredCapability + ".";
            Interlocked.Increment(ref denied);
            return false;
        }
        return true;
    }

    public static string GetReport()
    {
        lock (Sync) return "[REBIRTH NPC Work Capabilities] providers=" + Providers.Count +
            " checks=" + Interlocked.Read(ref checks) + " denied=" +
            Interlocked.Read(ref denied) + " faults=" + Interlocked.Read(ref providerFaults);
    }
}

public sealed class RebirthNpcWorkSchedule
{
    public int StartMinuteInclusive { get; private set; }
    public int EndMinuteExclusive { get; private set; }
    public byte DayMask { get; private set; }

    public RebirthNpcWorkSchedule(int startMinuteInclusive, int endMinuteExclusive, byte dayMask)
    {
        StartMinuteInclusive = NormalizeMinute(startMinuteInclusive);
        EndMinuteExclusive = NormalizeMinute(endMinuteExclusive);
        DayMask = dayMask == 0 ? (byte)0x7F : dayMask;
    }

    public bool IsActive(int dayIndex, int minuteOfDay)
    {
        int day = ((dayIndex % 7) + 7) % 7;
        if ((DayMask & (1 << day)) == 0) return false;
        int minute = NormalizeMinute(minuteOfDay);
        if (StartMinuteInclusive == EndMinuteExclusive) return true;
        if (StartMinuteInclusive < EndMinuteExclusive)
            return minute >= StartMinuteInclusive && minute < EndMinuteExclusive;
        return minute >= StartMinuteInclusive || minute < EndMinuteExclusive;
    }

    private static int NormalizeMinute(int value)
    {
        value %= 1440;
        return value < 0 ? value + 1440 : value;
    }
}

public sealed class RebirthNpcWorkAssignment
{
    public ulong AssignmentId { get; internal set; }
    public RebirthNpcStableId NpcId { get; internal set; }
    public string DefinitionId { get; internal set; }
    public string ActorId { get; internal set; }
    public string TargetKey { get; internal set; }
    public Vector3 TargetPosition { get; internal set; }
    public bool HasTargetPosition { get; internal set; }
    public RebirthNpcWorkSchedule Schedule { get; internal set; }
    public RebirthNpcWorkAssignmentStatus Status { get; internal set; }
    public uint Revision { get; internal set; }
    public int Priority { get; internal set; }
    public string Detail { get; internal set; }
    public long CreatedUtcTicks { get; internal set; }
    public long UpdatedUtcTicks { get; internal set; }

    internal RebirthNpcWorkAssignment Clone()
    {
        return (RebirthNpcWorkAssignment)MemberwiseClone();
    }
}
