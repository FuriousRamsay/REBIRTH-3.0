using System;
using Audio;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;
using Random = System.Random;

#nullable disable

public enum RebirthBossEventRole { Regular = 0, Support = 1, Boss = 2 }
public enum RebirthBossEventState { Scheduled, EligibilityPending, CompositionPending, PlacementPending, Spawning, Active, BossDefeated, RewardPending, Completed, Failed, Cancelled, CleaningUp }
public enum RebirthBossEventFrequency { Rare, Low, Normal, High, VeryHigh }
public enum RebirthBossEventSize { Small, Normal, Large, Random }
public enum RebirthBossEventDifficulty { Low, Normal, High, Random }
public enum RebirthBossEventTime { DayOnly, DayAndNight, NightOnly }
public enum RebirthBossEventRestriction { Anywhere, OutdoorsOnly, OnlyWhileQuesting, NotWhileQuesting, OutdoorsAndNotQuesting }
public enum RebirthBossEventOwnerUpdateKind : byte { Started = 0, ActiveSync = 1, BossDefeated = 2, RewardAvailable = 3, RewardRemoved = 4, Cancelled = 5 }

public sealed class RebirthBossEventOptionSnapshot
{
    public bool Enabled = true;
    public int MinimumPlayerLevel = 15;
    public RebirthBossEventFrequency Frequency = RebirthBossEventFrequency.Normal;
    public int MaximumPerDay = 2; // int.MaxValue means Unlimited.
    public RebirthBossEventSize Size = RebirthBossEventSize.Normal;
    public RebirthBossEventDifficulty Difficulty = RebirthBossEventDifficulty.Normal;
    public RebirthBossEventTime Time = RebirthBossEventTime.DayAndNight;
    public bool BloodMoonDayEvents = true;
    public RebirthBossEventRestriction Restriction = RebirthBossEventRestriction.Anywhere;
    public bool Rewards = true;
    public bool Notifications = true;
    public RebirthBossEventOptionSnapshot Clone() { return (RebirthBossEventOptionSnapshot)MemberwiseClone(); }
}

public sealed class RebirthBossEventProgressionSnapshot
{
    public string OwnerStableId;
    public int OwnerEntityId;
    public string OwnerDisplayName;
    public int PlayerLevel;
    public int PlayerGameStage;
    public int EffectiveEventGameStage;
    public RebirthBossEventDifficulty Difficulty;
    public RebirthBossEventSize Size;
    public int RewardTier;
    public int WorldDay;
    public int EventSeed;
}

public sealed class RebirthBossEventEntityState
{
    public Guid EventId;
    public string OwnerStableId;
    public int OwnerEntityId;
    public RebirthBossEventRole Role;
    public float HealthMultiplier;
    public float DamageMultiplier;
    public float FixedSpeed;
    public float Scale;
    public bool RewardSource;
    public bool ScalingApplied;
    public int LastKnownHealth;
    public int LastKnownMaxHealth;
    public float LastKnownHealthFraction;
    public bool HasHealthSnapshot;

    // Runtime-only. Set whenever an already-registered event entity is streamed
    // into the world, and for every persisted entity state loaded from disk.
    // This prevents a freshly reconstructed full-health EntityAlive from
    // overwriting the authoritative Boss Events health snapshot before restore.
    public bool NeedsHealthRestore;

    // Runtime-only. Set only for genuinely new event spawns. Once the role
    // HealthMax passive has been folded into ModifiedMax, the spawn is filled
    // exactly once and this flag is cleared. Persisted/streamed entities never
    // receive this flag.
    public bool NeedsInitialHealthFill;
}

public sealed class RebirthBossEventInstance
{
    public Guid EventId;
    public RebirthBossEventState State;
    public RebirthBossEventProgressionSnapshot Progression;
    public RebirthBossEventOptionSnapshot Options;
    public ulong ScheduledWorldTime;
    public ulong StartWorldTime;
    public int BossEntityId = -1;
    public readonly List<int> SupportEntityIds = new List<int>(2);
    public readonly List<int> RegularEntityIds = new List<int>();
    public bool RewardIssued;
    public Vector3 LastBossPosition;
    public Vector3i RewardBlockPosition;
    public bool RewardBlockPlaced;
    public int RewardEntityId = -1;
    public ulong RewardExpiresWorldTime;
    public ulong LastObservedWorldTime;
    public ulong MissingBossSinceWorldTime;
    public string FailureReason;
}

public static class RebirthBossEventOptions
{
    private static RebirthBossEventOptionSnapshot current = new RebirthBossEventOptionSnapshot();
    public static RebirthBossEventOptionSnapshot Current { get { return current.Clone(); } }
    public static void Apply(RebirthBossEventOptionSnapshot snapshot) { current = snapshot != null ? snapshot.Clone() : new RebirthBossEventOptionSnapshot(); }
}

public static class RebirthBossEventRegistry
{
    private const int MaxArchivedEvents = 4096;
    private static readonly Dictionary<Guid, RebirthBossEventInstance> Events = new Dictionary<Guid, RebirthBossEventInstance>();
    private static readonly Dictionary<Guid, RebirthBossEventInstance> ArchivedEvents = new Dictionary<Guid, RebirthBossEventInstance>();
    private static readonly Queue<Guid> ArchiveOrder = new Queue<Guid>();
    private static readonly Dictionary<int, RebirthBossEventEntityState> Entities = new Dictionary<int, RebirthBossEventEntityState>();
    public static IEnumerable<RebirthBossEventInstance> ActiveEvents { get { return Events.Values; } }
    public static IEnumerable<RebirthBossEventEntityState> EntityStates { get { return Entities.Values; } }
    public static void Register(RebirthBossEventInstance e)
    {
        if (e == null) return;
        ArchivedEvents.Remove(e.EventId);
        Events[e.EventId] = e;
    }
    public static void RegisterArchived(RebirthBossEventInstance e)
    {
        if (e == null) return;
        Events.Remove(e.EventId);
        RebirthBossEventInstance snapshot = CloneEvent(e);
        if (!ArchivedEvents.ContainsKey(snapshot.EventId))
            ArchiveOrder.Enqueue(snapshot.EventId);
        ArchivedEvents[snapshot.EventId] = snapshot;
        TrimArchive();
    }
    public static void RegisterEntity(int id, RebirthBossEventEntityState state) { if (id >= 0 && state != null) Entities[id] = state; }
    public static bool TryGetEntity(int id, out RebirthBossEventEntityState state) { return Entities.TryGetValue(id, out state); }
    public static bool TryGet(Guid id, out RebirthBossEventInstance e) { return Events.TryGetValue(id, out e); }
    public static bool HasActiveOwner(string stableId)
    {
        if (string.IsNullOrEmpty(stableId)) return false;
        foreach (RebirthBossEventInstance e in Events.Values)
            if (e != null && e.Progression != null && string.Equals(e.Progression.OwnerStableId, stableId, StringComparison.Ordinal)
                && e.State != RebirthBossEventState.Completed && e.State != RebirthBossEventState.Cancelled && e.State != RebirthBossEventState.Failed)
                return true;
        return false;
    }
    public static void RemoveEntity(int id) { Entities.Remove(id); }
    public static void Remove(Guid id)
    {
        Events.Remove(id);
        ArchivedEvents.Remove(id);
    }
    public static List<RebirthBossEventInstance> SnapshotEvents() { return new List<RebirthBossEventInstance>(Events.Values); }
    public static List<RebirthBossEventInstance> SnapshotArchivedEvents() { return new List<RebirthBossEventInstance>(ArchivedEvents.Values); }
    public static List<KeyValuePair<int, RebirthBossEventEntityState>> SnapshotEntities() { return new List<KeyValuePair<int, RebirthBossEventEntityState>>(Entities); }

    public static bool TryArchiveSettled(Guid eventId)
    {
        RebirthBossEventInstance e;
        if (!Events.TryGetValue(eventId, out e) || !CanArchive(e))
            return false;
        RegisterArchived(e);
        return true;
    }

    public static int CompactTerminalEvents()
    {
        List<Guid> candidates = null;
        foreach (KeyValuePair<Guid, RebirthBossEventInstance> pair in Events)
        {
            if (!CanArchive(pair.Value)) continue;
            if (candidates == null) candidates = new List<Guid>();
            candidates.Add(pair.Key);
        }
        if (candidates == null) return 0;
        for (int i = 0; i < candidates.Count; i++)
            TryArchiveSettled(candidates[i]);
        return candidates.Count;
    }

    private static bool CanArchive(RebirthBossEventInstance e)
    {
        if (e == null) return false;
        if (e.State != RebirthBossEventState.Completed &&
            e.State != RebirthBossEventState.Cancelled &&
            e.State != RebirthBossEventState.Failed)
            return false;
        if (e.RewardBlockPlaced || e.RewardEntityId >= 0)
            return false;
        if (e.SupportEntityIds.Count != 0 || e.RegularEntityIds.Count != 0)
            return false;
        foreach (RebirthBossEventEntityState state in Entities.Values)
            if (state != null && state.EventId == e.EventId)
                return false;
        return true;
    }

    private static RebirthBossEventInstance CloneEvent(RebirthBossEventInstance e)
    {
        RebirthBossEventProgressionSnapshot progression = null;
        if (e.Progression != null)
        {
            progression = new RebirthBossEventProgressionSnapshot
            {
                OwnerStableId = e.Progression.OwnerStableId,
                OwnerEntityId = e.Progression.OwnerEntityId,
                OwnerDisplayName = e.Progression.OwnerDisplayName,
                PlayerLevel = e.Progression.PlayerLevel,
                PlayerGameStage = e.Progression.PlayerGameStage,
                EffectiveEventGameStage = e.Progression.EffectiveEventGameStage,
                Difficulty = e.Progression.Difficulty,
                Size = e.Progression.Size,
                RewardTier = e.Progression.RewardTier,
                WorldDay = e.Progression.WorldDay,
                EventSeed = e.Progression.EventSeed
            };
        }
        RebirthBossEventInstance copy = new RebirthBossEventInstance
        {
            EventId = e.EventId,
            State = e.State,
            Progression = progression,
            Options = e.Options != null ? e.Options.Clone() : null,
            ScheduledWorldTime = e.ScheduledWorldTime,
            StartWorldTime = e.StartWorldTime,
            BossEntityId = e.BossEntityId,
            RewardIssued = e.RewardIssued,
            LastBossPosition = e.LastBossPosition,
            RewardBlockPosition = e.RewardBlockPosition,
            RewardBlockPlaced = e.RewardBlockPlaced,
            RewardEntityId = e.RewardEntityId,
            RewardExpiresWorldTime = e.RewardExpiresWorldTime,
            LastObservedWorldTime = e.LastObservedWorldTime,
            MissingBossSinceWorldTime = e.MissingBossSinceWorldTime,
            FailureReason = e.FailureReason
        };
        copy.SupportEntityIds.AddRange(e.SupportEntityIds);
        copy.RegularEntityIds.AddRange(e.RegularEntityIds);
        return copy;
    }

    private static void TrimArchive()
    {
        while (ArchivedEvents.Count > MaxArchivedEvents && ArchiveOrder.Count > 0)
        {
            Guid oldest = ArchiveOrder.Dequeue();
            ArchivedEvents.Remove(oldest);
        }
    }

    public static void Reset()
    {
        Events.Clear();
        ArchivedEvents.Clear();
        ArchiveOrder.Clear();
        Entities.Clear();
    }
}

public static class RebirthBossEventDirector
{
    public sealed class OwnerSchedule
    {
        public int Day = -1;
        public int Started;
        public int RetryCount;
        public int ConfigurationSignature;
        public ulong LastNaturalStartWorldTime;
        public bool CooldownPending;
        public bool PartyBlockedPending;
        public ulong PendingSinceWorldTime;
        public int PendingPartyId = -1;
        public readonly Queue<ulong> Times = new Queue<ulong>();
    }
    private static readonly Dictionary<string, OwnerSchedule> Schedules = new Dictionary<string, OwnerSchedule>(StringComparer.Ordinal);
    private static float nextPump;
    private const float PumpIntervalSeconds = 1f;
    public const int DefaultMinimumPlayerLevel = 15;
    private const int PersonalCooldownMinutes = 20;
    private const int PartyCooldownMinutes = 15;
    private const int PostCooldownDelayMinGameMinutes = 5;
    private const int PostCooldownDelayMaxGameMinutes = 20;

    public static void Update(World world)
    {
        if (world == null || world.IsRemote() || Time.realtimeSinceStartup < nextPump) return;
        nextPump = Time.realtimeSinceStartup + PumpIntervalSeconds;
        RebirthBossEventOptionSnapshot options = RebirthBossEventOptions.Current;
        if (!options.Enabled) return;
        List<EntityPlayer> players = world.GetPlayers();
        if (players == null) return;
        for (int i = 0; i < players.Count; i++) EvaluatePlayer(world, players[i], options);
    }

    private static void EvaluatePlayer(World world, EntityPlayer owner, RebirthBossEventOptionSnapshot options)
    {
        if (owner == null || !owner.Spawned || owner.Progression == null || owner.Progression.Level < options.MinimumPlayerLevel) return;
        int day = GameUtils.WorldTimeToDays(world.worldTime);
        string stableId = RebirthBossEventIdentity.GetStableId(owner);
        OwnerSchedule schedule;
        if (!Schedules.TryGetValue(stableId, out schedule))
            Schedules[stableId] = schedule = new OwnerSchedule();

        if (schedule.PartyBlockedPending &&
            (owner.Party == null || owner.Party.PartyID != schedule.PendingPartyId))
        {
            schedule.PartyBlockedPending = false;
            schedule.PendingPartyId = -1;
            RebirthBossEventPersistence.MarkDirty();
        }

        int schedulingSignature =
            RebirthBossEventScheduler.GetSchedulingSignature(options);

        ulong now = world.worldTime;
        bool preserveHeldDueEvent =
            schedule.CooldownPending &&
            schedule.Times.Count > 0;

        if ((schedule.Day != day ||
             schedule.ConfigurationSignature != schedulingSignature) &&
            !preserveHeldDueEvent)
        {
            GenerateDailySchedule(
                world,
                owner,
                stableId,
                day,
                options,
                schedule);
        }

        if (schedule.Times.Count == 0) return;
        if (!schedule.CooldownPending && schedule.Started >= options.MaximumPerDay) return;
        ulong dueWorldTime = schedule.Times.Peek();
        if (now < dueWorldTime) return;

        ulong personalRemaining;
        if (IsPersonalCooldownActive(schedule, now, out personalRemaining))
        {
            MarkCooldownPending(
                world,
                stableId,
                options.Time,
                schedule,
                false,
                -1,
                now,
                dueWorldTime,
                "personal",
                personalRemaining);
            return;
        }

        if (owner.Party != null)
        {
            string oldestPendingStableId;
            if (TryGetOldestPartyPending(owner.Party, out oldestPendingStableId) &&
                !string.Equals(oldestPendingStableId, stableId, StringComparison.Ordinal))
            {
                MarkCooldownPending(
                    world,
                    stableId,
                    options.Time,
                    schedule,
                    true,
                    owner.Party.PartyID,
                    now,
                    dueWorldTime,
                    "party-fairness:" + oldestPendingStableId,
                    0UL);
                return;
            }

            ulong partyRemaining;
            string blockingStableId;
            if (IsPartyCooldownActive(
                    owner.Party,
                    now,
                    out partyRemaining,
                    out blockingStableId))
            {
                MarkCooldownPending(
                    world,
                    stableId,
                    options.Time,
                    schedule,
                    true,
                    owner.Party.PartyID,
                    now,
                    dueWorldTime,
                    "party:" + blockingStableId,
                    partyRemaining);
                return;
            }
        }

        schedule.Times.Dequeue();
        ClearCooldownPending(schedule);

        if (!RebirthBossEventEligibility.CanStart(world, owner, options, out string reason))
        {
            Requeue(schedule, now, "eligibility:" + reason);
            return;
        }

        int seed = RebirthBossEventSeed.Create(world.Seed, day, stableId, schedule.Started);
        if (TryStart(world, owner, options, seed, out string failure))
        {
            schedule.Started++;
            schedule.RetryCount = 0;
            schedule.LastNaturalStartWorldTime = now;
            RebirthBossEventDiagnostics.Write(
                "natural event cooldown committed owner=" + owner.EntityName +
                " stableId=" + stableId +
                " personalMinutes=" + PersonalCooldownMinutes +
                " partyMinutes=" + PartyCooldownMinutes +
                " startWorldTime=" + now);
            RebirthBossEventPersistence.MarkDirty();
        }
        else Requeue(schedule, now, "start:" + failure);
    }

    private static ulong GetCooldownWorldTime(int minutes)
    {
        int inc = GameStats.GetInt(EnumGameStats.TimeOfDayIncPerSec);
        if (inc <= 0) inc = 1;
        return (ulong)minutes * 60UL * (ulong)inc;
    }

    private static bool IsPersonalCooldownActive(
        OwnerSchedule schedule,
        ulong now,
        out ulong remaining)
    {
        remaining = 0UL;
        if (schedule == null || schedule.LastNaturalStartWorldTime == 0UL)
            return false;

        ulong until =
            schedule.LastNaturalStartWorldTime +
            GetCooldownWorldTime(PersonalCooldownMinutes);

        if (now >= until)
            return false;

        remaining = until - now;
        return true;
    }

    private static bool IsPartyCooldownActive(
        Party party,
        ulong now,
        out ulong remaining,
        out string blockingStableId)
    {
        remaining = 0UL;
        blockingStableId = string.Empty;
        if (party == null || party.MemberList == null || party.MemberList.Count <= 1)
            return false;

        ulong cooldown = GetCooldownWorldTime(PartyCooldownMinutes);
        for (int i = 0; i < party.MemberList.Count; i++)
        {
            EntityPlayer member = party.MemberList[i];
            if (member == null) continue;

            string memberStableId = RebirthBossEventIdentity.GetStableId(member);
            OwnerSchedule memberSchedule;
            if (string.IsNullOrEmpty(memberStableId) ||
                !Schedules.TryGetValue(memberStableId, out memberSchedule) ||
                memberSchedule == null ||
                memberSchedule.LastNaturalStartWorldTime == 0UL)
                continue;

            ulong until = memberSchedule.LastNaturalStartWorldTime + cooldown;
            if (now >= until) continue;

            ulong candidateRemaining = until - now;
            if (candidateRemaining > remaining)
            {
                remaining = candidateRemaining;
                blockingStableId = memberStableId;
            }
        }

        return remaining > 0UL;
    }

    private static bool TryGetOldestPartyPending(
        Party party,
        out string stableId)
    {
        stableId = null;
        if (party == null || party.MemberList == null || party.MemberList.Count <= 1)
            return false;

        ulong bestPendingSince = ulong.MaxValue;
        ulong bestDue = ulong.MaxValue;

        for (int i = 0; i < party.MemberList.Count; i++)
        {
            EntityPlayer member = party.MemberList[i];
            if (member == null) continue;

            string candidateStableId = RebirthBossEventIdentity.GetStableId(member);
            OwnerSchedule candidate;
            if (string.IsNullOrEmpty(candidateStableId) ||
                !Schedules.TryGetValue(candidateStableId, out candidate) ||
                candidate == null ||
                !candidate.PartyBlockedPending ||
                candidate.PendingPartyId != party.PartyID ||
                candidate.Times.Count == 0)
                continue;

            ulong due = candidate.Times.Peek();
            bool better =
                candidate.PendingSinceWorldTime < bestPendingSince ||
                (candidate.PendingSinceWorldTime == bestPendingSince && due < bestDue) ||
                (candidate.PendingSinceWorldTime == bestPendingSince &&
                 due == bestDue &&
                 (stableId == null || string.CompareOrdinal(candidateStableId, stableId) < 0));

            if (!better) continue;

            stableId = candidateStableId;
            bestPendingSince = candidate.PendingSinceWorldTime;
            bestDue = due;
        }

        return !string.IsNullOrEmpty(stableId);
    }

    private static void MarkCooldownPending(
        World world,
        string stableId,
        RebirthBossEventTime allowedTime,
        OwnerSchedule schedule,
        bool partyBlocked,
        int partyId,
        ulong now,
        ulong dueWorldTime,
        string reason,
        ulong remaining)
    {
        if (schedule == null) return;

        bool stateChanged =
            !schedule.CooldownPending ||
            schedule.PartyBlockedPending != partyBlocked ||
            (partyBlocked && schedule.PendingPartyId != partyId);

        schedule.CooldownPending = true;
        if (partyBlocked)
        {
            if (!schedule.PartyBlockedPending || schedule.PendingPartyId != partyId)
                schedule.PendingSinceWorldTime = now;
            schedule.PartyBlockedPending = true;
            schedule.PendingPartyId = partyId;
        }
        else
        {
            schedule.PartyBlockedPending = false;
            schedule.PendingPartyId = -1;
            if (schedule.PendingSinceWorldTime == 0UL)
                schedule.PendingSinceWorldTime = now;
        }

        ulong resumeWorldTime = dueWorldTime;
        int randomDelayGameMinutes = 0;
        bool deferred = false;

        // A real cooldown should be a minimum separation, not the cadence.
        // Move the held event to a fresh random point after the cooldown expires
        // instead of letting it fire on the exact cooldown boundary. Pure party
        // fairness holds (remaining == 0) stay due and retain FIFO reservation.
        // Do not rely on stateChanged here: a party-fairness hold can transition
        // into a real party cooldown without changing the pending-party flags.
        if (remaining > 0UL &&
            schedule.Times.Count > 0 &&
            schedule.Times.Peek() <= now)
        {
            randomDelayGameMinutes =
                RollPostCooldownDelayGameMinutes(
                    world,
                    stableId,
                    schedule,
                    dueWorldTime);

            resumeWorldTime =
                now +
                remaining +
                GetGameMinutesWorldTime(randomDelayGameMinutes);

            resumeWorldTime =
                RebirthBossEventScheduler.MoveToAllowedWindow(
                    resumeWorldTime,
                    allowedTime,
                    randomDelayGameMinutes);

            DeferHead(schedule, resumeWorldTime);
            deferred = true;
        }

        if (stateChanged || deferred)
        {
            RebirthBossEventDiagnostics.Write(
                "scheduled event held by cooldown reason=" + reason +
                " due=" + dueWorldTime +
                " now=" + now +
                " remainingWorldTime=" + remaining +
                " randomDelayGameMinutes=" + randomDelayGameMinutes +
                " resumeWorldTime=" + resumeWorldTime +
                " partyPending=" + schedule.PartyBlockedPending +
                " partyId=" + schedule.PendingPartyId);
            RebirthBossEventPersistence.MarkDirty();
        }
    }

    private static ulong GetGameMinutesWorldTime(int gameMinutes)
    {
        if (gameMinutes <= 0) return 0UL;
        return (ulong)Math.Max(
            1,
            (int)Math.Round(
                gameMinutes * (24000.0 / (24.0 * 60.0))));
    }

    private static int RollPostCooldownDelayGameMinutes(
        World world,
        string stableId,
        OwnerSchedule schedule,
        ulong dueWorldTime)
    {
        int worldSeed = world != null ? world.Seed : 0;
        int day =
            world != null
                ? GameUtils.WorldTimeToDays(world.worldTime)
                : 0;

        int sequence = schedule != null ? schedule.Started : 0;
        int seed =
            RebirthBossEventSeed.Create(
                worldSeed,
                day,
                stableId,
                sequence);

        unchecked
        {
            seed = seed * 397 ^ (int)dueWorldTime;
            seed = seed * 397 ^ (int)(dueWorldTime >> 32);
        }

        return new Random(seed).Next(
            PostCooldownDelayMinGameMinutes,
            PostCooldownDelayMaxGameMinutes + 1);
    }

    private static void DeferHead(
        OwnerSchedule schedule,
        ulong resumeWorldTime)
    {
        if (schedule == null || schedule.Times.Count == 0)
            return;

        schedule.Times.Dequeue();
        InsertDueOrdered(schedule.Times, resumeWorldTime);
    }

    private static void InsertDueOrdered(Queue<ulong> queue, ulong dueWorldTime)
    {
        if (queue == null)
            return;

        int count = queue.Count;
        bool inserted = false;
        for (int i = 0; i < count; i++)
        {
            ulong current = queue.Dequeue();
            if (!inserted && dueWorldTime < current)
            {
                queue.Enqueue(dueWorldTime);
                inserted = true;
            }
            queue.Enqueue(current);
        }

        if (!inserted)
            queue.Enqueue(dueWorldTime);
    }

    private static void SortDueQueue(Queue<ulong> queue)
    {
        if (queue == null || queue.Count <= 1)
            return;

        ulong[] ordered = queue.ToArray();
        Array.Sort(ordered);
        queue.Clear();
        for (int i = 0; i < ordered.Length; i++)
            queue.Enqueue(ordered[i]);
    }

    private static void ClearCooldownPending(OwnerSchedule schedule)
    {
        if (schedule == null) return;
        schedule.CooldownPending = false;
        schedule.PartyBlockedPending = false;
        schedule.PendingSinceWorldTime = 0UL;
        schedule.PendingPartyId = -1;
    }

    private static void GenerateDailySchedule(World world, EntityPlayer owner, string stableId, int day, RebirthBossEventOptionSnapshot options, OwnerSchedule schedule)
    {
        bool sameDay = schedule.Day == day;
        int alreadyStarted =
            sameDay
                ? Math.Max(0, schedule.Started)
                : 0;

        schedule.Day = day;
        schedule.Started = alreadyStarted;
        schedule.RetryCount = 0;
        schedule.ConfigurationSignature =
            RebirthBossEventScheduler.GetSchedulingSignature(options);
        ClearCooldownPending(schedule);
        schedule.Times.Clear();

        Random rng =
            new Random(
                RebirthBossEventSeed.Create(
                    world.Seed,
                    day,
                    stableId,
                    0));

        int rolledTotal =
            RebirthBossEventScheduler.RollDailyCount(
                options.Frequency,
                owner.Progression != null
                    ? owner.Progression.Level
                    : options.MinimumPlayerLevel,
                options.MinimumPlayerLevel,
                rng);

        int targetTotal =
            Math.Min(
                rolledTotal,
                options.MaximumPerDay);

        int pendingCount =
            RebirthBossEventScheduler.GetPendingDailyCount(
                rolledTotal,
                options.MaximumPerDay,
                alreadyStarted);

        List<ulong> times =
            RebirthBossEventScheduler.CreateWorldTimes(
                day,
                pendingCount,
                options.Time,
                rng);

        for (int i = 0; i < times.Count; i++)
            schedule.Times.Enqueue(times[i]);

        int playerLevel =
            owner.Progression != null
                ? owner.Progression.Level
                : options.MinimumPlayerLevel;
        int eventDayChanceBasisPoints =
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                options.Frequency,
                playerLevel,
                options.MinimumPlayerLevel);
        int effectiveCountCeiling =
            Math.Min(
                RebirthBossEventScheduler.GetFrequencyCountCeiling(
                    options.Frequency),
                RebirthBossEventScheduler.GetLevelCountCeiling(
                    playerLevel,
                    options.MinimumPlayerLevel));
        effectiveCountCeiling =
            Math.Min(
                effectiveCountCeiling,
                options.MaximumPerDay);

        RebirthBossEventDiagnostics.Write(
            "schedule owner=" + owner.EntityName +
            " day=" + day +
            " level=" + playerLevel +
            " frequency=" + options.Frequency +
            " eventDayChance=" +
            (eventDayChanceBasisPoints / 100.0).ToString(
                "0.00",
                CultureInfo.InvariantCulture) + "%" +
            " countCeiling=" + effectiveCountCeiling +
            " alreadyStarted=" + alreadyStarted +
            " targetTotal=" + targetTotal +
            " pending=" + pendingCount +
            " times=" +
            string.Join(",", times.ToArray()));

        RebirthBossEventPersistence.MarkDirty();
    }

    private static void Requeue(OwnerSchedule schedule, ulong now, string reason)
    {
        schedule.RetryCount++;
        if (schedule.RetryCount > 6)
        {
            RebirthBossEventDiagnostics.Write("scheduled event abandoned after retries reason=" + reason);
            schedule.RetryCount = 0;
        }
        else
        {
            ulong retryAt = now + 1000UL;
            InsertDueOrdered(schedule.Times, retryAt);
            RebirthBossEventDiagnostics.Write("scheduled event retry=" + schedule.RetryCount + " at=" + retryAt + " reason=" + reason);
        }
        RebirthBossEventPersistence.MarkDirty();
    }

    public static Dictionary<string, OwnerSchedule> SnapshotSchedules()
    {
        Dictionary<string, OwnerSchedule> copy = new Dictionary<string, OwnerSchedule>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, OwnerSchedule> pair in Schedules)
        {
            OwnerSchedule clone = new OwnerSchedule
            {
                Day = pair.Value.Day,
                Started = pair.Value.Started,
                RetryCount = pair.Value.RetryCount,
                ConfigurationSignature = pair.Value.ConfigurationSignature,
                LastNaturalStartWorldTime = pair.Value.LastNaturalStartWorldTime,
                CooldownPending = pair.Value.CooldownPending,
                PartyBlockedPending = pair.Value.PartyBlockedPending,
                PendingSinceWorldTime = pair.Value.PendingSinceWorldTime,
                PendingPartyId = pair.Value.PendingPartyId
            };
            foreach (ulong time in pair.Value.Times) clone.Times.Enqueue(time);
            copy[pair.Key] = clone;
        }
        return copy;
    }

    public static void RestoreSchedules(Dictionary<string, OwnerSchedule> schedules)
    {
        Schedules.Clear();
        if (schedules == null) return;
        foreach (KeyValuePair<string, OwnerSchedule> pair in schedules)
        {
            if (pair.Value == null)
                continue;
            SortDueQueue(pair.Value.Times);
            Schedules[pair.Key] = pair.Value;
        }
    }

    public static bool TryStart(World world, EntityPlayer owner, RebirthBossEventOptionSnapshot options, int seed, out string failure)
    {
        failure = null;
        Random rng = new Random(seed);
        RebirthBossEventDifficulty difficulty = options.Difficulty == RebirthBossEventDifficulty.Random ? (RebirthBossEventDifficulty)rng.Next(0, 3) : options.Difficulty;
        RebirthBossEventSize size = options.Size == RebirthBossEventSize.Random ? (RebirthBossEventSize)rng.Next(0, 3) : options.Size;
        int regularCount = RebirthBossEventCompositionResolver.RollRegularCount(size, rng);
        RebirthBossEventProgressionSnapshot progression = RebirthBossEventProgression.Create(world, owner, difficulty, size, seed);
        RebirthBossEventSpawnPlan plan;
        if (!RebirthBossEventCompositionResolver.TryBuild(world, owner, progression, regularCount, rng, out plan, out failure)) return false;
        RebirthBossEventInstance e = new RebirthBossEventInstance { EventId = Guid.NewGuid(), State = RebirthBossEventState.Spawning, Progression = progression, Options = options.Clone(), ScheduledWorldTime = world.worldTime, StartWorldTime = world.worldTime };
        RebirthBossEventRegistry.Register(e);
        if (!RebirthBossEventSpawner.TrySpawn(world, owner, e, plan, rng, out failure)) { e.State = RebirthBossEventState.Failed; e.FailureReason = failure; RebirthBossEventPersistence.MarkDirty(); return false; }
        e.State = RebirthBossEventState.Active; e.LastObservedWorldTime = world.worldTime; RebirthBossEventPersistence.MarkDirty();
        RebirthBossEventOwnerNetwork.Send(world, e, RebirthBossEventOwnerUpdateKind.Started);
        RebirthBossEventDiagnostics.Write("active event=" + e.EventId + " owner=" + progression.OwnerDisplayName + " boss=" + e.BossEntityId + " supports=" + string.Join(",", e.SupportEntityIds.ToArray()) + " regulars=" + e.RegularEntityIds.Count);
        return true;
    }

#if DEBUG
    public static string GetFrequencyTestGateReport(
        World world,
        EntityPlayer owner,
        RebirthBossEventOptionSnapshot options)
    {
        if (world == null)
            return "world=null";
        if (world.IsRemote())
            return "authoritative=False";
        if (options == null)
            return "options=null";
        if (!options.Enabled)
            return "enabled=False";
        if (owner == null)
            return "owner=null";
        if (!owner.Spawned)
            return "spawned=False";
        if (owner.Progression == null)
            return "progression=null";
        if (owner.Progression.Level < options.MinimumPlayerLevel)
            return
                "level=" + owner.Progression.Level +
                " belowMinimum=" + options.MinimumPlayerLevel;

        return
            "eligible=True" +
            " authoritative=True" +
            " enabled=True" +
            " spawned=True" +
            " level=" + owner.Progression.Level;
    }

    public static bool DebugPrepareScheduleForFrequencyTest(
        World world,
        EntityPlayer owner,
        out string report)
    {
        RebirthBossEventOptionSnapshot options =
            RebirthBossEventOptions.Current;
        string gate =
            GetFrequencyTestGateReport(
                world,
                owner,
                options);

        if (!gate.StartsWith(
                "eligible=True",
                StringComparison.Ordinal))
        {
            report = gate;
            return false;
        }

        string stableId =
            RebirthBossEventIdentity.GetStableId(owner);
        if (string.IsNullOrEmpty(stableId))
        {
            report = "stableId=empty";
            return false;
        }

        int day = GameUtils.WorldTimeToDays(world.worldTime);
        int signature =
            RebirthBossEventScheduler.GetSchedulingSignature(options);

        OwnerSchedule schedule;
        if (!Schedules.TryGetValue(stableId, out schedule) ||
            schedule == null)
        {
            Schedules[stableId] = schedule = new OwnerSchedule();
        }

        if (schedule.Day != day ||
            schedule.ConfigurationSignature != signature)
        {
            GenerateDailySchedule(
                world,
                owner,
                stableId,
                day,
                options,
                schedule);
        }

        report =
            gate +
            " scheduleDay=" + schedule.Day +
            " started=" + schedule.Started +
            " pending=" + schedule.Times.Count;
        return true;
    }
#endif

    public static void Reset()
    {
        Schedules.Clear();
        nextPump = 0f;
        RebirthBossEventRegistry.Reset();
#if DEBUG
        RebirthBossEventFrequencyTestProbe.Reset();
#endif
    }
}

public static class RebirthBossEventScheduler
{
    // Increment whenever the daily schedule algorithm changes so persisted
    // schedules from an older implementation are regenerated immediately.
    private const int SchedulingAlgorithmVersion = 5;

    public static int GetSchedulingSignature(
        RebirthBossEventOptionSnapshot options)
    {
        if (options == null)
            return 0;

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + SchedulingAlgorithmVersion;
            hash = hash * 31 + (options.Enabled ? 1 : 0);
            hash = hash * 31 + options.MinimumPlayerLevel;
            hash = hash * 31 + (int)options.Frequency;
            hash = hash * 31 + options.MaximumPerDay;
            hash = hash * 31 + (int)options.Time;
            return hash;
        }
    }

    public static bool IsWorldTickAllowed(
        int tickWithinDay,
        RebirthBossEventTime time)
    {
        tickWithinDay =
            ((tickWithinDay % 24000) + 24000) % 24000;

        if (time == RebirthBossEventTime.DayOnly)
            return tickWithinDay >= 6000 &&
                   tickWithinDay < 22000;

        if (time == RebirthBossEventTime.NightOnly)
            return tickWithinDay < 6000 ||
                   tickWithinDay >= 22000;

        return true;
    }

    public static int GetPendingDailyCount(
        int rolledTotal,
        int maximumPerDay,
        int alreadyStarted)
    {
        rolledTotal = Math.Max(0, rolledTotal);
        alreadyStarted = Math.Max(0, alreadyStarted);

        int targetTotal =
            Math.Min(
                rolledTotal,
                maximumPerDay);

        return Math.Max(
            0,
            targetTotal - alreadyStarted);
    }

    public static int RollDailyCount(
        RebirthBossEventFrequency frequency,
        int playerLevel,
        Random r)
    {
        return RollDailyCount(frequency, playerLevel, RebirthBossEventDirector.DefaultMinimumPlayerLevel, r);
    }

    public static int RollDailyCount(
        RebirthBossEventFrequency frequency,
        int playerLevel,
        int minimumPlayerLevel,
        Random r)
    {
        if (r == null || playerLevel < minimumPlayerLevel)
            return 0;

        int eventDayChanceBasisPoints =
            GetEventDayChanceBasisPoints(
                frequency,
                playerLevel,
                minimumPlayerLevel);

        // The frequency option is a ceiling, never a daily entitlement.
        // Even level 75+ Very High tops out at an 80% event-day chance.
        if (r.Next(10000) >= eventDayChanceBasisPoints)
            return 0;

        int maximumCount =
            Math.Min(
                GetFrequencyCountCeiling(frequency),
                GetLevelCountCeiling(playerLevel, minimumPlayerLevel));

        return RollEventDayCount(maximumCount, r);
    }

    public static int GetEventDayChanceBasisPoints(
        RebirthBossEventFrequency frequency,
        int playerLevel)
    {
        return GetEventDayChanceBasisPoints(frequency, playerLevel, RebirthBossEventDirector.DefaultMinimumPlayerLevel);
    }

    public static int GetEventDayChanceBasisPoints(
        RebirthBossEventFrequency frequency,
        int playerLevel,
        int minimumPlayerLevel)
    {
        if (playerLevel < minimumPlayerLevel)
            return 0;

        int fullProgressionChance;
        switch (frequency)
        {
            case RebirthBossEventFrequency.Rare:
                fullProgressionChance = 2000;
                break;
            case RebirthBossEventFrequency.Low:
                fullProgressionChance = 3500;
                break;
            case RebirthBossEventFrequency.Normal:
                fullProgressionChance = 5000;
                break;
            case RebirthBossEventFrequency.High:
                fullProgressionChance = 6500;
                break;
            default:
                fullProgressionChance = 8000;
                break;
        }

        int levelScale = GetLevelFrequencyScaleBasisPoints(playerLevel, minimumPlayerLevel);
        return fullProgressionChance * levelScale / 10000;
    }

    public static int GetLevelFrequencyScaleBasisPoints(int playerLevel)
    {
        return GetLevelFrequencyScaleBasisPoints(playerLevel, RebirthBossEventDirector.DefaultMinimumPlayerLevel);
    }

    public static int GetLevelFrequencyScaleBasisPoints(int playerLevel, int minimumPlayerLevel)
    {
        if (playerLevel < minimumPlayerLevel) return 0;
        if (playerLevel >= 75) return 10000;

        if (playerLevel < 25)
            return InterpolateBasisPoints(
                playerLevel,
                minimumPlayerLevel,
                3500,
                25,
                5500);

        if (playerLevel < 50)
            return InterpolateBasisPoints(
                playerLevel,
                25,
                5500,
                50,
                7500);

        return InterpolateBasisPoints(
            playerLevel,
            50,
            7500,
            75,
            10000);
    }

    private static int InterpolateBasisPoints(
        int level,
        int startLevel,
        int startValue,
        int endLevel,
        int endValue)
    {
        if (level <= startLevel) return startValue;
        if (level >= endLevel) return endValue;

        long numerator =
            (long)(level - startLevel) *
            (endValue - startValue);
        int denominator = endLevel - startLevel;

        return startValue +
               (int)(numerator / denominator);
    }

    public static int GetFrequencyCountCeiling(
        RebirthBossEventFrequency frequency)
    {
        switch (frequency)
        {
            case RebirthBossEventFrequency.Rare: return 1;
            case RebirthBossEventFrequency.Low: return 2;
            case RebirthBossEventFrequency.Normal: return 2;
            case RebirthBossEventFrequency.High: return 3;
            default: return 4;
        }
    }

    public static int GetLevelCountCeiling(int playerLevel)
    {
        return GetLevelCountCeiling(playerLevel, RebirthBossEventDirector.DefaultMinimumPlayerLevel);
    }

    public static int GetLevelCountCeiling(int playerLevel, int minimumPlayerLevel)
    {
        if (playerLevel < minimumPlayerLevel) return 0;
        if (playerLevel < 25) return 1;
        if (playerLevel < 50) return 2;
        if (playerLevel < 75) return 3;
        return 4;
    }

    private static int RollEventDayCount(
        int maximumCount,
        Random r)
    {
        if (maximumCount <= 1) return 1;

        int roll = r.Next(100);
        if (maximumCount == 2)
            return roll < 70 ? 1 : 2;

        if (maximumCount == 3)
            return roll < 55 ? 1 : (roll < 85 ? 2 : 3);

        return roll < 45
            ? 1
            : (roll < 75
                ? 2
                : (roll < 93 ? 3 : 4));
    }
    public static ulong MoveToAllowedWindow(
        ulong worldTime,
        RebirthBossEventTime time,
        int randomDelayGameMinutes)
    {
        int tickWithinDay = (int)(worldTime % 24000UL);
        if (IsWorldTickAllowed(tickWithinDay, time))
            return worldTime;

        ulong dayBase = worldTime - (ulong)tickWithinDay;
        ulong offset =
            (ulong)Math.Max(
                1,
                (int)Math.Round(
                    Math.Max(1, randomDelayGameMinutes) *
                    (24000.0 / (24.0 * 60.0))));

        if (time == RebirthBossEventTime.DayOnly)
        {
            if (tickWithinDay < 6000)
                return dayBase + 6000UL + offset;

            return dayBase + 24000UL + 6000UL + offset;
        }

        if (time == RebirthBossEventTime.NightOnly)
            return dayBase + 22000UL + offset;

        return worldTime;
    }

    public static List<ulong> CreateWorldTimes(int day, int count, RebirthBossEventTime time, Random r)
    {
        List<ulong> result = new List<ulong>(count);

        // GameUtils.WorldTimeToDays is 1-based: world time 0 is Day 1.
        // Convert that display day back to the zero-based world-time day base.
        // The previous day * 24000 calculation scheduled every event one full
        // day late, then the Director regenerated it again at the next day
        // boundary before it could ever become due.
        ulong dayBase =
            day > 0
                ? (ulong)(day - 1) * 24000UL
                : 0UL;

        for (int i = 0; i < count; i++)
        {
            int tick;
            if (time == RebirthBossEventTime.DayOnly) tick = r.Next(6000, 22000);
            else if (time == RebirthBossEventTime.NightOnly)
            {
                int nightTick = r.Next(0, 8000);
                tick = nightTick < 6000 ? nightTick : 22000 + (nightTick - 6000);
            }
            else tick = r.Next(0, 24000);
            result.Add(dayBase + (ulong)tick);
        }
        result.Sort(); return result;
    }
}

public static class RebirthBossEventEligibility
{
#if DEBUG
    public static bool CanStartNaturalTest(
        World world,
        EntityPlayer owner,
        RebirthBossEventOptionSnapshot options,
        out string reason)
    {
        reason = null;

        if (options == null || !options.Enabled)
        {
            reason = "events-disabled";
            return false;
        }

        if (owner == null || !owner.Spawned)
        {
            reason = "owner-unavailable";
            return false;
        }

        if (owner.Progression == null)
        {
            reason = "owner-progression-unavailable";
            return false;
        }

        if (owner.Progression.Level < options.MinimumPlayerLevel)
        {
            reason =
                "owner-level-below-minimum:" +
                owner.Progression.Level.ToString(CultureInfo.InvariantCulture) +
                "<" +
                options.MinimumPlayerLevel.ToString(CultureInfo.InvariantCulture);
            return false;
        }

        return CanStart(world, owner, options, out reason);
    }
#endif

    public static bool CanStart(World world, EntityPlayer owner, RebirthBossEventOptionSnapshot options, out string reason)
    {
        reason = null;
        if (world == null || world.IsRemote()) { reason = "not-authoritative"; return false; }
        if (owner == null || !owner.Spawned) { reason = "owner-unavailable"; return false; }
        int hour = GameUtils.WorldTimeToHours(world.worldTime);

        if (!IsTimeAllowed(hour, options.Time))
        {
            reason = options.Time == RebirthBossEventTime.DayOnly
                ? "outside-day-window"
                : "outside-night-window";
            return false;
        }

        if (!options.BloodMoonDayEvents &&
            IsBloodMoonDay(world))
        {
            reason = "blood-moon-day-disabled";
            return false;
        }

        if (RebirthBossEventRegistry.HasActiveOwner(
                RebirthBossEventIdentity.GetStableId(owner)))
        {
            reason = "owner-already-has-active-event";
            return false;
        }

        bool questing = IsQuesting(owner);
        bool outdoors =
            RebirthBossEventPlacement.IsOutdoors(
                world,
                owner.position);

        if (!IsRestrictionAllowed(
                options.Restriction,
                questing,
                outdoors,
                out reason))
            return false;

        return true;
    }

    public static bool IsTimeAllowed(
        int hour,
        RebirthBossEventTime time)
    {
        hour = ((hour % 24) + 24) % 24;

        if (time == RebirthBossEventTime.DayOnly)
            return hour >= 6 && hour < 22;

        if (time == RebirthBossEventTime.NightOnly)
            return hour < 6 || hour >= 22;

        return true;
    }

    public static bool IsBloodMoonDay(World world)
    {
        if (world == null)
            return false;

        int currentDay =
            GameUtils.WorldTimeToDays(world.worldTime);

        int bloodMoonDay =
            GameStats.GetInt(EnumGameStats.BloodMoonDay);

        return IsBloodMoonDayNumber(
            currentDay,
            bloodMoonDay);
    }

    public static bool IsBloodMoonDayNumber(
        int currentDay,
        int bloodMoonDay)
    {
        return bloodMoonDay > 0 &&
               currentDay == bloodMoonDay;
    }

    public static bool IsRestrictionAllowed(
        RebirthBossEventRestriction restriction,
        bool questing,
        bool outdoors,
        out string reason)
    {
        reason = null;

        switch (restriction)
        {
            case RebirthBossEventRestriction.OutdoorsOnly:
                if (!outdoors)
                {
                    reason = "owner-not-outdoors";
                    return false;
                }
                break;

            case RebirthBossEventRestriction.OnlyWhileQuesting:
                if (!questing)
                {
                    reason = "owner-not-questing";
                    return false;
                }
                break;

            case RebirthBossEventRestriction.NotWhileQuesting:
                if (questing)
                {
                    reason = "owner-is-questing";
                    return false;
                }
                break;

            case RebirthBossEventRestriction.OutdoorsAndNotQuesting:
                if (!outdoors)
                {
                    reason = "owner-not-outdoors";
                    return false;
                }
                if (questing)
                {
                    reason = "owner-is-questing";
                    return false;
                }
                break;
        }

        return true;
    }

    public static bool IsQuesting(EntityPlayer owner)
    {
        if (owner == null ||
            owner.QuestJournal == null)
            return false;

        QuestJournal journal = owner.QuestJournal;

        Quest active = journal.ActiveQuest;
        if (active != null &&
            active.CurrentState == Quest.QuestState.InProgress &&
            active.RallyMarkerActivated)
            return true;

        if (journal.quests == null)
            return false;

        // 2.6 parity for quest types (including Buried Supplies) that do not always
        // occupy QuestJournal.ActiveQuest: only an actually activated site counts.
        for (int i = 0; i < journal.quests.Count; i++)
        {
            Quest q = journal.quests[i];

            if (q == null ||
                q.CurrentState != Quest.QuestState.InProgress ||
                !q.RallyMarkerActivated)
                continue;

            return true;
        }

        return false;
    }
}

public static class RebirthBossEventProgression
{
    public static int GetEffectiveGameStage(
        int gameStage,
        RebirthBossEventDifficulty difficulty)
    {
        gameStage = Math.Max(1, gameStage);

        float factor =
            difficulty == RebirthBossEventDifficulty.Low
                ? 0.85f
                : difficulty == RebirthBossEventDifficulty.High
                    ? 1.20f
                    : 1f;

        return Math.Max(
            1,
            Mathf.RoundToInt(gameStage * factor));
    }

    public static RebirthBossEventProgressionSnapshot Create(World world, EntityPlayer owner, RebirthBossEventDifficulty difficulty, RebirthBossEventSize size, int seed)
    {
        int gs = Math.Max(1, owner.gameStage);
        int effective =
            GetEffectiveGameStage(gs, difficulty);
        return new RebirthBossEventProgressionSnapshot { OwnerStableId = RebirthBossEventIdentity.GetStableId(owner), OwnerEntityId = owner.entityId, OwnerDisplayName = owner.EntityName, PlayerLevel = owner.Progression != null ? owner.Progression.Level : 1, PlayerGameStage = gs, EffectiveEventGameStage = effective, Difficulty = difficulty, Size = size, RewardTier = Math.Max(1, Math.Min(5, 1 + effective / 60)), WorldDay = GameUtils.WorldTimeToDays(world.worldTime), EventSeed = seed };
    }
}

public sealed class RebirthBossEventSpawnPlan
{
    public int BossClass;
    public readonly List<int> Supports = new List<int>(2);
    public readonly List<int> Regulars = new List<int>();
    public readonly List<Vector3> Positions = new List<Vector3>();
    public Vector3 SpawnOrigin;
}

public static class RebirthBossEventCompositionResolver
{
    private const int MaximumSelectionAttemptsPerSlot = 32;

    public static int RollRegularCount(RebirthBossEventSize size, Random r) { if (size == RebirthBossEventSize.Small) return r.Next(4, 7); if (size == RebirthBossEventSize.Large) return r.Next(11, 16); return r.Next(7, 11); }
    public static bool TryBuild(World world, EntityPlayer owner, RebirthBossEventProgressionSnapshot p, int regularCount, Random rng, out RebirthBossEventSpawnPlan plan, out string failure)
    {
        plan = new RebirthBossEventSpawnPlan(); failure = null;
        List<int> selected = new List<int>();
        for (int i = 0; i < regularCount + 3; i++)
        {
            bool accepted = false;

            for (int attempt = 0;
                 attempt < MaximumSelectionAttemptsPerSlot;
                 attempt++)
            {
                RebirthSpawnTrace trace;

                RebirthSpawnContext context =
                    new RebirthSpawnContext
                    {
                        Surface = RebirthSpawnSurface.Biome,
                        ProgressionMode =
                            RebirthSpawnCompositionRuntimeIntegration.SelectedProgressionMode(),
                        GameStage =
                            p.EffectiveEventGameStage,
                        Biome =
                            RebirthBossEventIdentity.GetBiome(
                                world,
                                owner.position),
                        HistoryKey =
                            "bossevent:" + p.OwnerStableId
                    };

                if (!RebirthSpawnCompositionService.TrySelect(
                        context,
                        rng.NextDouble,
                        out trace) ||
                    trace == null)
                {
                    failure =
                        "composition-empty: " +
                        RebirthSpawnCompositionService.GetStatus();
                    return false;
                }

                // 3.1 EntityClass IDs are signed string hashes. Negative values are valid.
                // Validate against the live registry instead of testing id > 0.
                EntityClass selectedClass =
                    EntityClass.GetEntityClass(
                        trace.EntityClassId);

                if (selectedClass == null)
                {
                    failure =
                        "composition-invalid-entity:" +
                        (trace.EntityName ?? "<unknown>") +
                        " id=" + trace.EntityClassId;
                    return false;
                }

                // Boss Events never use screamers, crawlers or demolition zombies.
                if (RebirthSpawnCompositionService.IsForbiddenRestrictedSpawnEntity(selectedClass))
                {
                    RebirthBossEventDiagnostics.Write(
                        "event composition excluded entity="
                        + selectedClass.entityClassName
                        + " reason=forbidden-special"
                        + " slot=" + i
                        + " attempt=" + (attempt + 1));
                    continue;
                }

                int selectedId = trace.EntityClassId;

                // Role identity rules:
                //  - supports must differ from the boss and from each other;
                //  - regulars must differ from the boss and both supports;
                //  - regulars may repeat other regulars.
                bool duplicateRole =
                    (i == 1 && selected.Count >= 1 && selectedId == selected[0]) ||
                    (i == 2 && selected.Count >= 2 &&
                        (selectedId == selected[0] || selectedId == selected[1])) ||
                    (i >= 3 && selected.Count >= 3 &&
                        (selectedId == selected[0] ||
                         selectedId == selected[1] ||
                         selectedId == selected[2]));

                if (duplicateRole)
                {
                    RebirthBossEventDiagnostics.Write(
                        "event composition excluded entity="
                        + selectedClass.entityClassName
                        + " reason=role-duplicate"
                        + " slot=" + i
                        + " attempt=" + (attempt + 1));
                    continue;
                }

                selected.Add(selectedId);
                accepted = true;
                break;
            }

            if (!accepted)
            {
                failure =
                    "composition-event-exclusion-exhausted:slot=" + i +
                    " excluded=forbidden-special-or-role-duplicate";
                return false;
            }
        }
        plan.BossClass = selected[0]; plan.Supports.Add(selected[1]); plan.Supports.Add(selected[2]); for (int i = 3; i < selected.Count; i++) plan.Regulars.Add(selected[i]);
        int requiredPositions = 1 + plan.Supports.Count + plan.Regulars.Count;
        if (!RebirthBossEventPlacement.TryBuildAtomicPlacement(world, owner, requiredPositions, rng, plan.Positions, out plan.SpawnOrigin, out failure)) return false;
        return true;
    }
}

public static class RebirthBossEventPlacement
{
    // REBIRTH 2.6 event spawns were 40 m from the player. 3.1 needs a little
    // extra room for the atomic cluster, but 92 m made contact take too long.
    // Prefer 50 m, allow 45/40 m, and only expand to 55 m as a final distance
    // fallback. Never accept an origin closer than 40 m.
    private static readonly float[] OriginRadii = { 50f, 45f, 40f, 55f };
    private const float MinimumOriginRadius = 40f;
    private const float MaximumOriginRadius = 58f;
    private const int OriginDirectionAttempts = 36;
    private const int OriginValidationRadius = 3;
    private const int OriginPlayerClearance = 38;
    private const int ClusterMinimumRadius = 1;
    private const int ClusterMaximumRadius = 6;
    private const int ClusterPlayerClearance = 15;
    private const int AttemptsPerEntity = 64;
    private const int MaxNativePlacementProbes = 2048;

    public static bool TryBuildAtomicPlacement(
        World world,
        EntityPlayer owner,
        int count,
        Random rng,
        List<Vector3> result,
        out Vector3 spawnOrigin,
        out string failure)
    {
        failure = null;
        spawnOrigin = Vector3.zero;
        result.Clear();

        if (world == null || owner == null || count <= 0)
        {
            failure = "placement-invalid-input";
            return false;
        }

        int bestClusterCount = 0;
        int remainingNativeProbes = MaxNativePlacementProbes;

        // Preference order:
        //  1) road/street tile, outside the player's view cone
        //  2) open wilderness, outside the player's view cone
        //  3) road/street tile, visible only as a last resort
        //  4) open wilderness, visible only as a last resort
        for (int pass = 0; pass < 4; pass++)
        {
            bool requireRoad = pass == 0 || pass == 2;
            bool allowViewCone = pass >= 2;

            for (int radiusIndex = 0; radiusIndex < OriginRadii.Length; radiusIndex++)
            {
                float radius = OriginRadii[radiusIndex];

                for (int attempt = 0; attempt < OriginDirectionAttempts; attempt++)
                {
                    float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                    Vector3 rough = new Vector3(
                        owner.position.x + Mathf.Cos(angle) * radius,
                        owner.position.y,
                        owner.position.z + Mathf.Sin(angle) * radius);

                    int x = Mathf.FloorToInt(rough.x);
                    int z = Mathf.FloorToInt(rough.z);
                    if (!HasLoadedSpawnNeighborhood(world, x, z))
                        continue;

                    if (remainingNativeProbes-- <= 0)
                    {
                        failure = "placement-probe-budget-exhausted:" +
                            MaxNativePlacementProbes.ToString(CultureInfo.InvariantCulture);
                        return false;
                    }

                    Vector3 candidateOrigin;
                    if (!world.GetMobRandomSpawnPosWithWater(
                            rough,
                            0,
                            OriginValidationRadius,
                            OriginPlayerClearance,
                            true,
                            out candidateOrigin))
                    {
                        continue;
                    }

                    Vector3 ownerDelta = candidateOrigin - owner.position;
                    ownerDelta.y = 0f;
                    float ownerDistance = ownerDelta.magnitude;
                    if (ownerDistance < MinimumOriginRadius ||
                        ownerDistance > MaximumOriginRadius)
                        continue;

                    if (!IsValidOutdoorNonPoiPosition(world, candidateOrigin))
                        continue;

                    bool isRoad = IsStreetTile(world, candidateOrigin);
                    if (requireRoad != isRoad)
                        continue;

                    bool inViewCone = owner.IsInViewCone(candidateOrigin);
                    if (!allowViewCone && inViewCone)
                        continue;

                    List<Vector3> trial = new List<Vector3>(count);
                    int placed;
                    if (!TryBuildClusterAtOrigin(
                            world,
                            owner,
                            candidateOrigin,
                            count,
                            allowViewCone,
                            trial,
                            ref remainingNativeProbes,
                            out placed))
                    {
                        if (placed > bestClusterCount)
                            bestClusterCount = placed;
                        continue;
                    }

                    result.AddRange(trial);
                    spawnOrigin = candidateOrigin;

                    RebirthBossEventDiagnostics.Write(
                        "placement selected origin=" + candidateOrigin
                        + " distance=" + ownerDistance.ToString("F1", CultureInfo.InvariantCulture)
                        + "m road=" + isRoad
                        + " inViewCone=" + inViewCone
                        + " pass=" + pass
                        + " count=" + count);

                    return true;
                }
            }
        }

        failure =
            "placement-insufficient-cluster-sites:"
            + bestClusterCount + "/" + count;
        return false;
    }

    private static bool TryBuildClusterAtOrigin(
        World world,
        EntityPlayer owner,
        Vector3 origin,
        int count,
        bool allowViewCone,
        List<Vector3> result,
        ref int remainingNativeProbes,
        out int placed)
    {
        result.Clear();
        placed = 0;

        HashSet<Vector3i> usedCells = new HashSet<Vector3i>();

        for (int i = 0; i < count; i++)
        {
            bool found = false;

            for (int attempt = 0; attempt < AttemptsPerEntity; attempt++)
            {
                if (remainingNativeProbes-- <= 0)
                    return false;

                Vector3 candidate;
                if (!world.GetMobRandomSpawnPosWithWater(
                        origin,
                        ClusterMinimumRadius,
                        ClusterMaximumRadius,
                        ClusterPlayerClearance,
                        true,
                        out candidate))
                {
                    continue;
                }

                if (!IsValidOutdoorNonPoiPosition(world, candidate))
                    continue;

                if (!allowViewCone && owner.IsInViewCone(candidate))
                    continue;

                Vector3i cell = World.worldToBlockPos(candidate);
                if (usedCells.Contains(cell))
                    continue;

                usedCells.Add(cell);
                result.Add(candidate);
                placed++;
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private static bool IsValidOutdoorNonPoiPosition(
        World world,
        Vector3 point)
    {
        if (world == null)
            return false;

        // World.GetPOIAtPosition() excludes street tiles by default in 3.1,
        // which is exactly what the event needs: reject actual POI/building
        // prefab volumes while still permitting road/street-tile prefabs.
        if (world.GetPOIAtPosition(point) != null)
            return false;

        return IsOutdoors(world, point);
    }

    private static bool IsStreetTile(
        World world,
        Vector3 point)
    {
        if (world == null)
            return false;

        return world.GetPOIAtPosition(
                   point,
                   FastTags<TagGroup.Poi>.none,
                   DynamicPrefabDecorator.streetTileTag) != null;
    }

    private static bool HasLoadedSpawnNeighborhood(
        World world,
        int x,
        int z)
    {
        return world.GetChunkFromWorldPos(x, z) != null
            && world.GetChunkFromWorldPos(x - 16, z - 16) != null
            && world.GetChunkFromWorldPos(x + 16, z + 16) != null;
    }

    public static bool IsOutdoors(World world, Vector3 point)
    {
        if (world == null) return false;

        int x = Mathf.FloorToInt(point.x);
        int z = Mathf.FloorToInt(point.z);
        int startY = Mathf.Clamp(
            Mathf.CeilToInt(point.y + 1f),
            1,
            252);

        for (int y = startY; y <= 252; y++)
        {
            if (!world.GetBlock(new Vector3i(x, y, z)).isair)
                return false;
        }

        return true;
    }
}

public static class RebirthBossEventSpawner
{
    public static bool TrySpawn(World world, EntityPlayer owner, RebirthBossEventInstance e, RebirthBossEventSpawnPlan plan, Random rng, out string failure)
    {
        failure = null;
        List<EntityAlive> created = new List<EntityAlive>();
        List<int> supportIds = new List<int>();
        List<int> regularIds = new List<int>();
        int bossId = -1;
        int positionIndex = 0;

        if (world == null || owner == null || e == null || plan == null ||
            plan.Positions.Count != 1 + plan.Supports.Count + plan.Regulars.Count)
        {
            failure = "placement-plan-mismatch";
            return false;
        }

        RebirthBossEventDiagnostics.Write(
            "spawn cluster event=" + e.EventId +
            " origin=" + plan.SpawnOrigin +
            " owner=" + owner.EntityName +
            " ownerDistance=" + Vector3.Distance(owner.position, plan.SpawnOrigin).ToString("F1", CultureInfo.InvariantCulture) +
            "m count=" + plan.Positions.Count);

        try
        {
            EntityAlive boss = Spawn(
                world, owner, e, plan.BossClass,
                RebirthBossEventRole.Boss, 1.50f, 15.0f, 1.0f, 0f,
                plan.Positions[positionIndex++]);
            if (boss == null)
            {
                failure = "boss-spawn-failed";
                Cleanup(world, created);
                return false;
            }
            created.Add(boss);
            bossId = boss.entityId;

            for (int i = 0; i < plan.Supports.Count; i++)
            {
                EntityAlive support = Spawn(
                    world, owner, e, plan.Supports[i],
                    RebirthBossEventRole.Support, 1.25f, 7.50f, 1.0f, 0f,
                    plan.Positions[positionIndex++]);
                if (support == null)
                {
                    failure = "support-spawn-failed";
                    Cleanup(world, created);
                    return false;
                }
                created.Add(support);
                supportIds.Add(support.entityId);
            }

            for (int i = 0; i < plan.Regulars.Count; i++)
            {
                EntityAlive regular = Spawn(
                    world, owner, e, plan.Regulars[i],
                    RebirthBossEventRole.Regular, 1.10f, 1.25f, 1.0f, 0f,
                    plan.Positions[positionIndex++]);
                if (regular == null)
                {
                    failure = "regular-spawn-failed";
                    Cleanup(world, created);
                    return false;
                }
                created.Add(regular);
                regularIds.Add(regular.entityId);
            }

            // Publish encounter membership only after the complete native batch
            // exists. A partial failure therefore cannot leave an event pointing
            // at a compensated entity.
            e.BossEntityId = bossId;
            e.SupportEntityIds.Clear();
            e.SupportEntityIds.AddRange(supportIds);
            e.RegularEntityIds.Clear();
            e.RegularEntityIds.AddRange(regularIds);
            return true;
        }
        catch (Exception ex)
        {
            failure = "spawn-exception:" + ex.GetType().Name;
            Cleanup(world, created);
            e.BossEntityId = -1;
            e.SupportEntityIds.Clear();
            e.RegularEntityIds.Clear();
            RebirthBossEventDiagnostics.Write(
                "spawn batch compensated event=" + e.EventId +
                " failure=" + failure +
                " detail=" + ex.Message);
            return false;
        }
    }
    private static void Cleanup(World world, List<EntityAlive> created)
    {
        for (int i = 0; i < created.Count; i++)
        {
            EntityAlive entity = created[i];
            if (entity == null) continue;
            RebirthBossEventRegistry.RemoveEntity(entity.entityId);
            world.RemoveEntity(entity.entityId, EnumRemoveEntityReason.Killed);
        }
    }
    private static EntityAlive Spawn(World world, EntityPlayer owner, RebirthBossEventInstance e, int classId, RebirthBossEventRole role, float scale, float hp, float damage, float speed, Vector3 pos)
    {
        EntityAlive entity = null;
        try
        {
            entity = EntityFactory.CreateEntity(classId, pos) as EntityAlive;
            if (entity == null) return null;

            // Match the 2.6 event-spawn semantics before OnAddedToWorld runs.
            entity.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
            world.SpawnEntityInWorld(entity);

            ApplyEventTargeting(entity, owner);

            RebirthBossEventEntityState state = new RebirthBossEventEntityState
            {
                EventId = e.EventId,
                OwnerStableId = e.Progression.OwnerStableId,
                OwnerEntityId = owner.entityId,
                Role = role,
                Scale = scale,
                HealthMultiplier = hp,
                DamageMultiplier = damage,
                FixedSpeed = speed,
                RewardSource = role == RebirthBossEventRole.Boss,
                ScalingApplied = false
            };

            RebirthBossEventRegistry.RegisterEntity(entity.entityId, state);
#if DEBUG
            RebirthBossEventHealthTrace.ExtendWindow(
                "new-event-entity-spawn",
                10f);
#endif
            RebirthBossEventScaling.ApplyScaleAndHealth(entity, state);

            state.NeedsInitialHealthFill = true;
            entity.Health = entity.GetMaxHealth();
            RebirthBossEventHealthPersistence.Capture(
                entity,
                state,
                "spawn-pre-role-max",
                true);

            RebirthBossEventDiagnostics.Write(
                "spawned event entity=" + entity.entityId
                + " class=" + entity.EntityClass.entityClassName
                + " role=" + role
                + " distance=" + Vector3.Distance(owner.position, entity.position)
                    .ToString("F1", CultureInfo.InvariantCulture) + "m");

            return entity;
        }
        catch
        {
            if (entity != null)
            {
                RebirthBossEventRegistry.RemoveEntity(entity.entityId);
                try
                {
                    world.RemoveEntity(entity.entityId, EnumRemoveEntityReason.Despawned);
                }
                catch
                {
                    // Preserve the original spawn exception; the outer batch
                    // compensation will retry removal for entities it owns.
                }
            }
            throw;
        }
    }
    public static void ApplyEventPersistence(
        EntityAlive entity)
    {
        if (entity == null)
            return;

        // Buff/custom-var state is runtime state and must be rebuilt whenever an
        // event entity is streamed back in. Keep the event marker and lifetime
        // protection valid even while the owning player is temporarily offline.
        if (entity.Buffs != null)
            entity.Buffs.SetCustomVar("$eventSpawn", 1f);

        EntityHuman human = entity as EntityHuman;
        if (human != null)
            human.timeToDie = ulong.MaxValue;
    }

    public static void ApplyEventTargeting(
        EntityAlive entity,
        EntityPlayer owner)
    {
        if (entity == null)
            return;

        ApplyEventPersistence(entity);

        if (owner == null)
            return;

        // Exact 2.6 event targeting behavior: long-lived attack target,
        // extended sight and 360-degree acquisition.
        entity.SetAttackTarget(owner, 60000);
        entity.sightRangeBase = 200;
        entity.SetMaxViewAngle(360);
    }
}

public static class RebirthBossEventPresentation
{
    // These are the actual REBIRTH 2.6 Tier-1 event role buffs. In 3.1 they
    // again own health, damage, resistance and movement instead of replacing
    // EntityStats.Health.BaseMax from C#.
    public const string BossRoleBuff = "FuriousRamsayBossBuffTier1B";
    public const string SupportRoleBuff = "FuriousRamsaySupportBuffTier1";
    public const string MinionRoleBuff = "FuriousRamsayMinionBuffTier1";

    public static string GetRoleBuff(RebirthBossEventRole role)
    {
        if (role == RebirthBossEventRole.Boss) return BossRoleBuff;
        if (role == RebirthBossEventRole.Support) return SupportRoleBuff;
        return MinionRoleBuff;
    }

    public static float GetEffectiveHealthMultiplier(RebirthBossEventRole role)
    {
        // v110 event balance: boss 15x, support 7.5x, regular 1.25x.
        // XML HealthMax perc_add values are therefore +14, +6.5 and +0.25.
        if (role == RebirthBossEventRole.Boss) return 15f;
        if (role == RebirthBossEventRole.Support) return 7.5f;
        return 1.25f;
    }

    public static void EnsureRoleBuff(EntityAlive entity, RebirthBossEventEntityState state)
    {
        if (entity == null || entity.Buffs == null || state == null)
            return;

        if (state.Role == RebirthBossEventRole.Boss)
        {
            if (entity.Buffs.GetCustomVar("$varFuriousRamsayBoss") != 1f)
                entity.Buffs.SetCustomVar("$varFuriousRamsayBoss", 1f);
            if (entity.Buffs.GetCustomVar("$EventNavIcon") != 1f)
                entity.Buffs.SetCustomVar("$EventNavIcon", 1f);
        }
        else if (state.Role == RebirthBossEventRole.Support)
        {
            if (entity.Buffs.GetCustomVar("$varFuriousRamsaySupportMinion") != 1f)
                entity.Buffs.SetCustomVar("$varFuriousRamsaySupportMinion", 1f);
            if (entity.Buffs.GetCustomVar("$EventNavIcon") != 1f)
                entity.Buffs.SetCustomVar("$EventNavIcon", 1f);
        }

        string roleBuff = GetRoleBuff(state.Role);
        if (entity.Buffs.HasBuff(roleBuff))
            return;

#if DEBUG
        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "ROLE-BUFF-BEFORE-ADD",
            "buff=" + roleBuff);
#endif

        EntityBuffs.BuffStatus result =
            entity.Buffs.AddBuff(
                roleBuff,
                -1,
                true,
                false);

#if DEBUG
        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "ROLE-BUFF-AFTER-ADD",
            "buff=" + roleBuff +
            " result=" + result);
#endif
        RebirthBossEventDiagnostics.Write(
            "role buff entity=" + entity.entityId
            + " role=" + state.Role
            + " buff=" + roleBuff
            + " result=" + result
            + " maxHealth=" + entity.GetMaxHealth()
            + " health=" + entity.Health);
    }
}

public static class RebirthBossEventScaling
{
    private static bool IsLegacyV102HealthMultiplier(RebirthBossEventRole role, float value)
    {
        float expected = role == RebirthBossEventRole.Boss ? 2.5f
            : role == RebirthBossEventRole.Support ? 1.65f
            : 1.15f;
        return Mathf.Abs(value - expected) < 0.001f;
    }

    public static void ApplyScaleAndHealth(EntityAlive entity, RebirthBossEventEntityState state)
    {
        if (entity == null || state == null) return;

        if (entity.gameObject != null)
            entity.gameObject.transform.localScale = Vector3.one * state.Scale;

        string roleBuff = RebirthBossEventPresentation.GetRoleBuff(state.Role);

        // v102 directly overwrote BaseMax with 2.5x/1.65x/1.15x. If an event
        // survives an upgrade, normalize that one-time C# scaling before the
        // restored 2.6 XML role buff is applied, otherwise the multipliers stack.
        if (entity.Buffs != null
            && !entity.Buffs.HasBuff(roleBuff)
            && state.ScalingApplied
            && IsLegacyV102HealthMultiplier(state.Role, state.HealthMultiplier))
        {
            int normalizedBaseMax = Math.Max(1, Mathf.RoundToInt(entity.Stats.Health.BaseMax / state.HealthMultiplier));
            entity.Stats.Health.BaseMax = normalizedBaseMax;
            RebirthBossEventDiagnostics.Write(
                "migrated v102 health entity=" + entity.entityId
                + " role=" + state.Role
                + " normalizedBaseMax=" + normalizedBaseMax);
        }

        state.HealthMultiplier = RebirthBossEventPresentation.GetEffectiveHealthMultiplier(state.Role);
        // EntityDamage/BlockDamage now come from the restored XML role buff.
        // Keep the runtime damage multiplier neutral to prevent double scaling.
        state.DamageMultiplier = 1f;
        state.FixedSpeed = 0f;
        state.ScalingApplied = true;

        RebirthBossEventPresentation.EnsureRoleBuff(entity, state);
    }
}


public static class RebirthBossEventHealthPersistence
{
    public static void Capture(
        EntityAlive entity,
        RebirthBossEventEntityState state,
        string reason,
        bool markDirty)
    {
        if (entity == null || state == null || entity.IsDead())
            return;

        int maxHealth = Math.Max(1, entity.GetMaxHealth());
        int health = Math.Max(0, Math.Min(entity.Health, maxHealth));
        float fraction = Mathf.Clamp01((float)health / maxHealth);

        bool changed =
            !state.HasHealthSnapshot ||
            state.LastKnownHealth != health ||
            state.LastKnownMaxHealth != maxHealth ||
            Mathf.Abs(state.LastKnownHealthFraction - fraction) > 0.0001f;

        state.LastKnownHealth = health;
        state.LastKnownMaxHealth = maxHealth;
        state.LastKnownHealthFraction = fraction;
        state.HasHealthSnapshot = true;

        if (changed && markDirty)
            RebirthBossEventPersistence.MarkDirty();

        if (changed &&
            !string.Equals(reason, "lifecycle", StringComparison.Ordinal))
        {
            RebirthBossEventDiagnostics.Write(
                "event health snapshot reason=" + (reason ?? "unknown") +
                " entity=" + entity.entityId +
                " role=" + state.Role +
                " health=" + health +
                "/" + maxHealth +
                " fraction=" + fraction.ToString(
                    "0.0000", CultureInfo.InvariantCulture));
        }
    }

    public static int CaptureAllLoaded(
        World world,
        string reason)
    {
        if (world == null || world.IsRemote())
            return 0;

        List<KeyValuePair<int, RebirthBossEventEntityState>> states =
            RebirthBossEventRegistry.SnapshotEntities();

        int captured = 0;
        for (int i = 0; i < states.Count; i++)
        {
            RebirthBossEventEntityState state = states[i].Value;
            if (state == null)
                continue;

            EntityAlive alive =
                world.GetEntity(states[i].Key) as EntityAlive;

            if (alive == null || alive.IsDead())
                continue;

            Capture(
                alive,
                state,
                reason ?? "capture-all-loaded",
                false);
            captured++;
        }

        if (captured > 0)
        {
            RebirthBossEventDiagnostics.Write(
                "event health capture-all reason=" +
                (reason ?? "unknown") +
                " count=" + captured);
        }

        return captured;
    }

    public static bool Restore(
        EntityAlive entity,
        RebirthBossEventEntityState state,
        string reason)
    {
        if (entity == null || state == null || !state.HasHealthSnapshot)
            return false;

        int newMax = Math.Max(1, entity.GetMaxHealth());
        float fraction = Mathf.Clamp01(state.LastKnownHealthFraction);

        int restoredHealth;
        if (state.LastKnownMaxHealth == newMax)
        {
            restoredHealth = state.LastKnownHealth;
        }
        else
        {
            restoredHealth = Mathf.RoundToInt(newMax * fraction);
        }

        if (fraction > 0f && restoredHealth < 1)
            restoredHealth = 1;

        restoredHealth = Math.Max(0, Math.Min(restoredHealth, newMax));

#if DEBUG
        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "HEALTH-RESTORE-BEFORE-SET",
            "reason=" + (reason ?? "unknown") +
            " requested=" + restoredHealth);
#endif

        entity.Health = restoredHealth;

#if DEBUG
        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "HEALTH-RESTORE-AFTER-SET",
            "reason=" + (reason ?? "unknown") +
            " requested=" + restoredHealth);
#endif

        state.LastKnownHealth = restoredHealth;
        state.LastKnownMaxHealth = newMax;
        state.LastKnownHealthFraction =
            Mathf.Clamp01((float)restoredHealth / newMax);
        state.HasHealthSnapshot = true;
        RebirthBossEventPersistence.MarkDirty();

        RebirthBossEventDiagnostics.Write(
            "event health restored reason=" + (reason ?? "unknown") +
            " entity=" + entity.entityId +
            " role=" + state.Role +
            " health=" + restoredHealth +
            "/" + newMax +
            " fraction=" + state.LastKnownHealthFraction.ToString(
                "0.0000", CultureInfo.InvariantCulture));

        return true;
    }
}

public static class RebirthBossEventRuntimePolicy
{
    public static bool TryGetDamageMultiplier(int attackerEntityId, out float multiplier)
    {
        multiplier = 1f; RebirthBossEventEntityState state;
        if (!RebirthBossEventRegistry.TryGetEntity(attackerEntityId, out state) || state == null) return false;
        multiplier = state.DamageMultiplier; return true;
    }

}

public static class RebirthBossEventTargeting
{
    public static void Maintain(
        World world,
        RebirthBossEventInstance e)
    {
        if (world == null ||
            e == null ||
            e.Progression == null)
            return;

        EntityPlayer owner =
            RebirthBossEventIdentity.FindPlayer(
                world,
                e.Progression.OwnerStableId);

        if (owner == null ||
            !owner.Spawned ||
            owner.IsDead())
            return;

        RehydrateEventForOwner(
            world,
            e,
            owner,
            "lifecycle");
    }

    public static int RehydrateOwner(
        World world,
        EntityPlayer owner,
        string reason)
    {
        if (world == null || owner == null)
            return 0;

        string stableId =
            RebirthBossEventIdentity.GetStableId(owner);

        List<RebirthBossEventInstance> events =
            RebirthBossEventRegistry.SnapshotEvents();

        int loadedEntities = 0;
        int matchingEvents = 0;

        for (int i = 0; i < events.Count; i++)
        {
            RebirthBossEventInstance e = events[i];
            if (e == null ||
                e.Progression == null ||
                e.State == RebirthBossEventState.Cancelled ||
                e.State == RebirthBossEventState.Failed ||
                !string.Equals(
                    e.Progression.OwnerStableId,
                    stableId,
                    StringComparison.Ordinal))
                continue;

            matchingEvents++;
            loadedEntities +=
                RehydrateEventForOwner(
                    world,
                    e,
                    owner,
                    reason ?? "owner-rehydrate");

            RebirthBossEventOwnerNetwork.Send(
                world,
                e,
                RebirthBossEventOwnerUpdateKind.ActiveSync);
        }

        RebirthBossEventDiagnostics.Write(
            "owner event rehydrate reason=" + (reason ?? "unknown") +
            " owner=" + owner.EntityName +
            " stableId=" + stableId +
            " events=" + matchingEvents +
            " loadedEntities=" + loadedEntities);

        return loadedEntities;
    }

    public static bool RehydrateLoadedEntity(
        World world,
        EntityAlive entity,
        string reason)
    {
        if (world == null || entity == null || entity.IsDead())
            return false;

        RebirthBossEventEntityState state;
        if (!RebirthBossEventRegistry.TryGetEntity(
                entity.entityId,
                out state) ||
            state == null)
            return false;

        RebirthBossEventInstance e;
        if (!RebirthBossEventRegistry.TryGet(
                state.EventId,
                out e) ||
            e == null ||
            e.Progression == null ||
            e.State == RebirthBossEventState.Cancelled ||
            e.State == RebirthBossEventState.Failed)
            return false;

#if DEBUG
        RebirthBossEventHealthTrace.ExtendWindow(
            "rehydrate-loaded-entity",
            15f);
        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "REHYDRATE-ENTRY",
            "reason=" + (reason ?? "unknown"));
#endif

        // Restore everything that belongs to the event entity itself immediately.
        // v112 diagnostic note: authoritative 3.1 EntityCreationData DOES persist
        // EntityStats. The separate Boss Events snapshot is retained unchanged
        // in this diagnostic build while we trace the post-load health mutation.
        int healthBeforeRehydrate = entity.Health;
        int maxBeforeRehydrate = Math.Max(1, entity.GetMaxHealth());
        string roleBuff =
            RebirthBossEventPresentation.GetRoleBuff(state.Role);
        bool roleBuffWasPresent =
            entity.Buffs != null &&
            entity.Buffs.HasBuff(roleBuff);

        RebirthBossEventScaling.ApplyScaleAndHealth(
            entity,
            state);

#if DEBUG
        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "REHYDRATE-AFTER-SCALE-ROLEBUFF",
            "roleBuffWasPresent=" + roleBuffWasPresent);
#endif

        bool shouldRestoreSnapshot =
            state.HasHealthSnapshot &&
            (state.NeedsHealthRestore || !roleBuffWasPresent);

        if (shouldRestoreSnapshot)
        {
            RebirthBossEventHealthPersistence.Restore(
                entity,
                state,
                reason ?? "rehydrate");

#if DEBUG
            RebirthBossEventHealthTrace.TraceEntity(
                entity,
                state,
                "REHYDRATE-AFTER-HEALTH-RESTORE",
                "reason=" + (reason ?? "unknown"));
#endif

            state.NeedsHealthRestore = false;
        }
        else if (!roleBuffWasPresent && !state.HasHealthSnapshot)
        {
            // Backward compatibility for v1-v5 persistence files, which do not
            // contain a health snapshot. Do not invent a heal during role rebuild.
            int fallbackHealth =
                Math.Max(
                    1,
                    Math.Min(
                        healthBeforeRehydrate,
                        entity.GetMaxHealth()));
            entity.Health = fallbackHealth;
            state.NeedsHealthRestore = false;
            RebirthBossEventHealthPersistence.Capture(
                entity,
                state,
                "legacy-health-fallback",
                true);

            RebirthBossEventDiagnostics.Write(
                "event health legacy fallback entity="
                + entity.entityId
                + " role=" + state.Role
                + " pre=" + healthBeforeRehydrate
                + "/" + maxBeforeRehydrate
                + " restored=" + entity.Health
                + "/" + entity.GetMaxHealth());
        }
        else
        {
            // This is a continuously-loaded entity, not a reconstruction. Its
            // live HP is authoritative and may safely refresh the snapshot.
            state.NeedsHealthRestore = false;
            RebirthBossEventHealthPersistence.Capture(
                entity,
                state,
                reason ?? "rehydrate-live",
                true);
        }

        RebirthBossEventSpawner.ApplyEventPersistence(entity);

        EntityPlayer owner =
            RebirthBossEventIdentity.FindPlayer(
                world,
                state.OwnerStableId);

        if (owner != null && owner.Spawned && !owner.IsDead())
        {
            e.Progression.OwnerEntityId = owner.entityId;
            state.OwnerEntityId = owner.entityId;
            RebirthBossEventSpawner.ApplyEventTargeting(
                entity,
                owner);
        }

#if DEBUG
        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "REHYDRATE-EXIT",
            "ownerLoaded=" + (owner != null));
#endif

        RebirthBossEventDiagnostics.Write(
            "event entity rehydrated reason=" + (reason ?? "unknown") +
            " entity=" + entity.entityId +
            " class=" + entity.EntityClass.entityClassName +
            " role=" + state.Role +
            " owner=" +
            (owner != null ? owner.EntityName : "offline") +
            " target=" +
            (entity.GetAttackTarget() != null
                ? entity.GetAttackTarget().entityId.ToString(
                    CultureInfo.InvariantCulture)
                : "none") +
            " sight=" + entity.sightRangeBase.ToString(
                "0.##", CultureInfo.InvariantCulture) +
            " eventSpawn=" +
            (entity.Buffs != null
                ? entity.Buffs.GetCustomVar("$eventSpawn").ToString(
                    "0", CultureInfo.InvariantCulture)
                : "n/a") +
            " roleBuff=" +
            RebirthBossEventPresentation.GetRoleBuff(state.Role));

        return true;
    }

    private static int RehydrateEventForOwner(
        World world,
        RebirthBossEventInstance e,
        EntityPlayer owner,
        string reason)
    {
        if (world == null ||
            e == null ||
            e.Progression == null ||
            owner == null)
            return 0;

        e.Progression.OwnerEntityId = owner.entityId;

        int count = 0;
        if (MaintainEntity(
                world,
                e.BossEntityId,
                owner,
                reason))
            count++;

        for (int i = 0; i < e.SupportEntityIds.Count; i++)
            if (MaintainEntity(
                    world,
                    e.SupportEntityIds[i],
                    owner,
                    reason))
                count++;

        for (int i = 0; i < e.RegularEntityIds.Count; i++)
            if (MaintainEntity(
                    world,
                    e.RegularEntityIds[i],
                    owner,
                    reason))
                count++;

        return count;
    }

    private static bool MaintainEntity(
        World world,
        int entityId,
        EntityPlayer owner,
        string reason)
    {
        if (entityId < 0)
            return false;

        EntityAlive entity =
            world.GetEntity(entityId) as EntityAlive;

        if (entity == null || entity.IsDead())
            return false;

        RebirthBossEventEntityState state;
        if (!RebirthBossEventRegistry.TryGetEntity(
                entityId,
                out state) ||
            state == null)
            return false;

        state.OwnerEntityId = owner.entityId;

        string roleBuff =
            RebirthBossEventPresentation.GetRoleBuff(state.Role);
        bool roleBuffPresent =
            entity.Buffs != null &&
            entity.Buffs.HasBuff(roleBuff);

        // A loaded entity without its role buff has just streamed/reconstructed.
        // Use the rehydrate path so the saved health snapshot is restored BEFORE
        // ordinary lifecycle maintenance can observe/overwrite it.
        if (state.NeedsHealthRestore || !roleBuffPresent)
            return RehydrateLoadedEntity(
                world,
                entity,
                state.NeedsHealthRestore
                    ? (reason ?? "maintain-health-restore")
                    : (reason ?? "maintain-missing-role-buff"));

        RebirthBossEventScaling.ApplyScaleAndHealth(
            entity,
            state);

        // Brand-new event spawns need one full-health initialization only after
        // the passive HealthMax multiplier is reflected by GetMaxHealth().
        // Persisted/streamed entities never set NeedsInitialHealthFill.
        if (state.NeedsInitialHealthFill)
        {
            int expandedMax = Math.Max(1, entity.GetMaxHealth());
            if (!state.HasHealthSnapshot ||
                expandedMax > state.LastKnownMaxHealth)
            {
#if DEBUG
                RebirthBossEventHealthTrace.TraceEntity(
                    entity,
                    state,
                    "INITIAL-FILL-BEFORE",
                    "expandedMax=" + expandedMax);
#endif

                entity.Health = expandedMax;
                state.NeedsInitialHealthFill = false;

                RebirthBossEventHealthPersistence.Capture(
                    entity,
                    state,
                    "initial-role-health-fill",
                    true);

#if DEBUG
                RebirthBossEventHealthTrace.TraceEntity(
                    entity,
                    state,
                    "INITIAL-FILL-AFTER",
                    "expandedMax=" + expandedMax);
#endif

                RebirthBossEventDiagnostics.Write(
                    "initial event health fill entity=" + entity.entityId
                    + " role=" + state.Role
                    + " health=" + entity.Health
                    + "/" + entity.GetMaxHealth());
            }
        }

        // Reassert the complete 2.6 event-targeting package. This also keeps
        // 3.1 EntityHuman.timeToDie disabled after a chunk reload/reconnect.
        RebirthBossEventSpawner.ApplyEventTargeting(
            entity,
            owner);

        RebirthBossEventHealthPersistence.Capture(
            entity,
            state,
            reason ?? "lifecycle",
            true);

        if (!string.Equals(reason, "lifecycle", StringComparison.Ordinal))
        {
            RebirthBossEventDiagnostics.Write(
                "event target reasserted reason=" + (reason ?? "unknown") +
                " entity=" + entity.entityId +
                " role=" + state.Role +
                " owner=" + owner.EntityName +
                " target=" +
                (entity.GetAttackTarget() != null
                    ? entity.GetAttackTarget().entityId.ToString(
                        CultureInfo.InvariantCulture)
                    : "none"));
        }

        return true;
    }
}

public static class RebirthBossEventLifecycle
{
    private const ulong MissingBossGraceTicks = 300UL;
    private static float nextUpdate;

    public static void Update(World world)
    {
        if (world == null || world.IsRemote() || Time.realtimeSinceStartup < nextUpdate) return;
        nextUpdate = Time.realtimeSinceStartup + 1f;
        List<RebirthBossEventInstance> events = RebirthBossEventRegistry.SnapshotEvents();
        for (int i = 0; i < events.Count; i++)
        {
            RebirthBossEventInstance e = events[i];
            UpdateEvent(world, e);
            if (e != null && RebirthBossEventRegistry.TryArchiveSettled(e.EventId))
                RebirthBossEventPersistence.MarkDirty();
        }
        RebirthBossEventPersistence.SaveIfDue(world);
    }

    private static void UpdateEvent(
        World world,
        RebirthBossEventInstance e)
    {
        if (e == null ||
            e.State == RebirthBossEventState.Cancelled ||
            e.State == RebirthBossEventState.Failed)
            return;

        e.LastObservedWorldTime = world.worldTime;

        // Support/regular event entities remain part of the encounter after the
        // boss dies. Keep pruning and maintaining their 2.6 event targeting
        // independently of reward/completion state.
        PruneDead(world, e.SupportEntityIds);
        PruneDead(world, e.RegularEntityIds);
        RebirthBossEventTargeting.Maintain(world, e);

        if (e.State == RebirthBossEventState.RewardPending)
            RebirthBossEventRewardService.TryIssue(world, e);

        if (e.RewardIssued)
            RebirthBossEventRewardService.Update(world, e);

        // Once the boss has been defeated, reward state and surviving event
        // entities are maintained above. Do not run the boss-missing failure
        // path for a completed encounter.
        if (e.State == RebirthBossEventState.BossDefeated ||
            e.State == RebirthBossEventState.RewardPending ||
            e.State == RebirthBossEventState.Completed)
            return;

        EntityAlive boss =
            e.BossEntityId >= 0
                ? world.GetEntity(e.BossEntityId) as EntityAlive
                : null;

        if (boss != null)
        {
            e.MissingBossSinceWorldTime = 0UL;
            e.LastBossPosition = boss.position;

            RebirthBossEventEntityState state;
            if (RebirthBossEventRegistry.TryGetEntity(
                    boss.entityId,
                    out state))
            {
                RebirthBossEventScaling.ApplyScaleAndHealth(
                    boss,
                    state);
            }

            if (!boss.IsDead())
                return;

            // An actually dead boss is authoritative proof of completion. Do not
            // wait for the missing-entity timeout before issuing completion/reward.
            CompleteBossDefeat(world, e);
            return;
        }

        // A missing entity whose persisted event-role record still exists is
        // expected to be chunk-unloaded, not dead. Keep the encounter alive until
        // that exact persistent entity streams back in. Death/removal paths clear
        // the role record explicitly, so only a truly unaccounted-for boss reaches
        // the failure timeout below.
        RebirthBossEventEntityState expectedBossState;
        if (RebirthBossEventRegistry.TryGetEntity(
                e.BossEntityId,
                out expectedBossState) &&
            expectedBossState != null &&
            expectedBossState.EventId == e.EventId &&
            expectedBossState.Role == RebirthBossEventRole.Boss)
        {
            e.MissingBossSinceWorldTime = 0UL;
            return;
        }

        if (e.MissingBossSinceWorldTime == 0UL)
        {
            e.MissingBossSinceWorldTime =
                world.worldTime;

            RebirthBossEventPersistence.MarkDirty();
            return;
        }

        if (world.worldTime -
            e.MissingBossSinceWorldTime <
            MissingBossGraceTicks)
            return;

        e.State = RebirthBossEventState.Failed;
        e.FailureReason = "boss-missing-timeout";

        RebirthBossEventRegistry.RemoveEntity(
            e.BossEntityId);

        RebirthBossEventOwnerNetwork.Send(
            world,
            e,
            RebirthBossEventOwnerUpdateKind.Cancelled);

        RebirthBossEventDiagnostics.Write(
            "event failed event=" + e.EventId +
            " reason=" + e.FailureReason +
            " owner=" +
            (e.Progression != null
                ? e.Progression.OwnerDisplayName
                : "?"));

        RebirthBossEventPersistence.MarkDirty();
    }

    private static void CompleteBossDefeat(
        World world,
        RebirthBossEventInstance e)
    {
        if (e == null ||
            e.State == RebirthBossEventState.BossDefeated ||
            e.State == RebirthBossEventState.RewardPending ||
            e.State == RebirthBossEventState.Completed)
            return;

        e.State = RebirthBossEventState.BossDefeated;

        RebirthBossEventRegistry.RemoveEntity(
            e.BossEntityId);

        e.State =
            e.Options != null &&
            e.Options.Rewards
                ? RebirthBossEventState.RewardPending
                : RebirthBossEventState.Completed;

        if (e.State ==
            RebirthBossEventState.RewardPending)
        {
            RebirthBossEventRewardService.TryIssue(
                world,
                e);
        }

        RebirthBossEventOwnerNetwork.Send(
            world,
            e,
            RebirthBossEventOwnerUpdateKind.BossDefeated);

        RebirthBossEventDiagnostics.Write(
            "boss defeated event=" + e.EventId +
            " owner=" +
            (e.Progression != null
                ? e.Progression.OwnerDisplayName
                : "?"));

        RebirthBossEventPersistence.MarkDirty();
    }

    public static void NotifyTrackedEntityKilled(
        World world,
        EntityAlive killed,
        RebirthBossEventEntityState state)
    {
        if (world == null || killed == null || state == null)
            return;

        RebirthBossEventInstance e;
        if (!RebirthBossEventRegistry.TryGet(
                state.EventId,
                out e) ||
            e == null)
        {
            RebirthBossEventRegistry.RemoveEntity(killed.entityId);
            return;
        }

        if (state.Role == RebirthBossEventRole.Boss)
        {
            e.LastBossPosition = killed.position;
            CompleteBossDefeat(world, e);
            return;
        }

        List<int> ids =
            state.Role == RebirthBossEventRole.Support
                ? e.SupportEntityIds
                : e.RegularEntityIds;

        ids.Remove(killed.entityId);
        RebirthBossEventRegistry.RemoveEntity(killed.entityId);
        RebirthBossEventPersistence.MarkDirty();
    }

    public static void NotifyTrackedEntityRemoved(
        World world,
        Entity entity,
        RebirthBossEventEntityState state,
        EnumRemoveEntityReason reason)
    {
        if (entity == null || state == null)
            return;

        if (reason == EnumRemoveEntityReason.Unloaded)
            return;

        EntityAlive alive = entity as EntityAlive;
        if (reason == EnumRemoveEntityReason.Killed && alive != null)
        {
            NotifyTrackedEntityKilled(
                world,
                alive,
                state);
            return;
        }

        RebirthBossEventInstance e;
        if (RebirthBossEventRegistry.TryGet(
                state.EventId,
                out e) &&
            e != null)
        {
            if (state.Role == RebirthBossEventRole.Support)
                e.SupportEntityIds.Remove(entity.entityId);
            else if (state.Role == RebirthBossEventRole.Regular)
                e.RegularEntityIds.Remove(entity.entityId);
        }

        RebirthBossEventRegistry.RemoveEntity(entity.entityId);
        RebirthBossEventPersistence.MarkDirty();
    }

    private static void PruneDead(World world, List<int> ids)
    {
        for (int i = ids.Count - 1; i >= 0; i--)
        {
            int id = ids[i];
            EntityAlive entity =
                world.GetEntity(id) as EntityAlive;

            // Null means the persistent entity is currently chunk-unloaded. Do not
            // erase its event-role record; EntityLoaded rehydrates it later.
            if (entity == null)
                continue;

            if (!entity.IsDead())
                continue;

            ids.RemoveAt(i);
            RebirthBossEventRegistry.RemoveEntity(id);
        }
    }

    public static void Reset() { nextUpdate = 0f; }
}

public static class RebirthBossEventPersistence
{
    private const uint Magic = 0x52424245;
    private const ushort Version = 8;
    public static ushort FormatVersion { get { return Version; } }
    private const int DefaultPersistedMinimumPlayerLevel = 15;
    private const float SaveIntervalSeconds = 60f;
    private static bool dirty;
    private static float nextSave;
    private static string PathName { get { return Path.Combine(GameIO.GetSaveGameDir(), "RebirthBossEvents.dat"); } }

    public static void MarkDirty() { dirty = true; }
    public static void SaveIfDue(World world)
    {
        if (!dirty || world == null || world.IsRemote() || Time.realtimeSinceStartup < nextSave) return;
        // Failed writes must respect the same cadence; Save leaves dirty set on failure.
        // Explicit shutdown saves still call Save directly and bypass this delay.
        try { Save(); }
        finally { nextSave = Time.realtimeSinceStartup + SaveIntervalSeconds; }
    }
    public static void Save()
    {
        string path = PathName, temp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Dictionary<string, RebirthBossEventDirector.OwnerSchedule> schedules = RebirthBossEventDirector.SnapshotSchedules();
        List<RebirthBossEventInstance> events = RebirthBossEventRegistry.SnapshotEvents();
        List<RebirthBossEventInstance> archivedEvents = RebirthBossEventRegistry.SnapshotArchivedEvents();
        List<KeyValuePair<int, RebirthBossEventEntityState>> entities = RebirthBossEventRegistry.SnapshotEntities();
        if (events.Count > 4096 || archivedEvents.Count > 4096 || entities.Count > 65536)
            throw new InvalidDataException("Boss Events persistence bounds exceeded.");
        using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(Magic); writer.Write(Version);
            writer.Write(schedules.Count);
            foreach (KeyValuePair<string, RebirthBossEventDirector.OwnerSchedule> pair in schedules)
            {
                writer.Write(pair.Key ?? string.Empty);
                writer.Write(pair.Value.Day);
                writer.Write(pair.Value.Started);
                writer.Write(pair.Value.RetryCount);
                writer.Write(pair.Value.ConfigurationSignature);
                writer.Write(pair.Value.LastNaturalStartWorldTime);
                writer.Write(pair.Value.CooldownPending);
                writer.Write(pair.Value.PartyBlockedPending);
                writer.Write(pair.Value.PendingSinceWorldTime);
                writer.Write(pair.Value.PendingPartyId);
                writer.Write(pair.Value.Times.Count);
                foreach (ulong time in pair.Value.Times) writer.Write(time);
            }
            writer.Write(events.Count);
            for (int i = 0; i < events.Count; i++) WriteEvent(writer, events[i]);
            writer.Write(archivedEvents.Count);
            for (int i = 0; i < archivedEvents.Count; i++) WriteEvent(writer, archivedEvents[i]);
            writer.Write(entities.Count);
            for (int i = 0; i < entities.Count; i++)
            {
                RebirthBossEventEntityState state = entities[i].Value;
#if DEBUG
                if (RebirthBossEventHealthTrace.Active)
                {
                    Log.Out(
                    "[REBIRTH BossEvents:HealthTrace] PERSIST-WRITE" +
                    " entity=" + entities[i].Key +
                    " role=" + (state != null ? state.Role.ToString() : "null") +
                    " saved=" +
                    (state != null && state.HasHealthSnapshot
                        ? state.LastKnownHealth.ToString(CultureInfo.InvariantCulture) +
                          "/" +
                          state.LastKnownMaxHealth.ToString(CultureInfo.InvariantCulture) +
                          "@" +
                          state.LastKnownHealthFraction.ToString("0.0000", CultureInfo.InvariantCulture)
                        : "none") +
                    " needsRestore=" +
                    (state != null && state.NeedsHealthRestore));
                }
#endif
                WriteEntity(writer, entities[i].Key, state);
            }
            writer.Flush(); stream.Flush(true);
        }
        string publishError;
        if (!RebirthDurableFileCommit.TryPublish(temp, path, out publishError))
            throw new IOException("Boss Events durable publication failed: " + publishError);
        dirty = false;
    }
    public static void Load()
    {
        ResetRuntimeOnly(); string path = PathName, backup = path + ".bak";
        if (!File.Exists(path) && !File.Exists(backup)) return;
        Exception primaryFailure = null;
        if (File.Exists(path))
        {
            try { LoadFile(path); return; }
            catch (Exception ex) { primaryFailure = ex; ResetRuntimeOnly(); }
        }
        if (!File.Exists(backup))
        {
            RebirthBossEventDiagnostics.Write("load failed " + (primaryFailure != null ? primaryFailure.Message : "primary missing"));
            return;
        }
        try
        {
            LoadFile(backup);
            RebirthBossEventDiagnostics.Write("recovered from backup" + (primaryFailure != null ? " after " + primaryFailure.Message : " because primary was missing"));
        }
        catch (Exception ex) { ResetRuntimeOnly(); RebirthBossEventDiagnostics.Write("backup load failed " + ex.Message); }
    }
    private static void LoadFile(string path)
    {
        Dictionary<string, RebirthBossEventDirector.OwnerSchedule> schedules = new Dictionary<string, RebirthBossEventDirector.OwnerSchedule>(StringComparer.Ordinal);
        ushort fileVersion;
        using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
        {
            if (reader.ReadUInt32() != Magic) throw new InvalidDataException("Invalid Boss Events persistence file.");
            fileVersion = reader.ReadUInt16();
            if (fileVersion < 1 || fileVersion > Version) throw new InvalidDataException("Unsupported Boss Events persistence format version " + fileVersion + ".");
            int scheduleCount = ReadCount(reader, 65536);
            for (int i = 0; i < scheduleCount; i++)
            {
                string key = reader.ReadString();
                var schedule =
                    new RebirthBossEventDirector.OwnerSchedule
                    {
                        Day = reader.ReadInt32(),
                        Started = reader.ReadInt32(),
                        RetryCount = reader.ReadInt32(),
                        ConfigurationSignature =
                            fileVersion >= 3
                                ? reader.ReadInt32()
                                : 0
                    };

                if (fileVersion >= 5)
                {
                    schedule.LastNaturalStartWorldTime = reader.ReadUInt64();
                    schedule.CooldownPending = reader.ReadBoolean();
                    schedule.PartyBlockedPending = reader.ReadBoolean();
                    schedule.PendingSinceWorldTime = reader.ReadUInt64();
                    schedule.PendingPartyId = reader.ReadInt32();
                }

                int timeCount = ReadCount(reader, 64);
                for (int j = 0; j < timeCount; j++)
                    schedule.Times.Enqueue(reader.ReadUInt64());

                schedules[key] = schedule;
            }
            int eventCount = ReadCount(reader, fileVersion >= 8 ? 4096 : 65536);
            for (int i = 0; i < eventCount; i++)
                RebirthBossEventRegistry.Register(ReadEvent(reader, fileVersion));
            if (fileVersion >= 8)
            {
                int archivedCount = ReadCount(reader, 4096);
                for (int i = 0; i < archivedCount; i++)
                    RebirthBossEventRegistry.RegisterArchived(ReadEvent(reader, fileVersion));
            }
            int entityCount = ReadCount(reader, 65536);
            for (int i = 0; i < entityCount; i++)
            {
                int id;
                RebirthBossEventEntityState state =
                    ReadEntity(reader, fileVersion, out id);
                state.NeedsHealthRestore = state.HasHealthSnapshot;
#if DEBUG
                if (RebirthBossEventHealthTrace.Active)
                {
                    Log.Out(
                    "[REBIRTH BossEvents:HealthTrace] PERSIST-READ" +
                    " version=" + fileVersion +
                    " entity=" + id +
                    " role=" + state.Role +
                    " saved=" +
                    (state.HasHealthSnapshot
                        ? state.LastKnownHealth.ToString(CultureInfo.InvariantCulture) +
                          "/" +
                          state.LastKnownMaxHealth.ToString(CultureInfo.InvariantCulture) +
                          "@" +
                          state.LastKnownHealthFraction.ToString("0.0000", CultureInfo.InvariantCulture)
                        : "none") +
                    " needsRestore=" + state.NeedsHealthRestore);
                }
#endif
                RebirthBossEventRegistry.RegisterEntity(id, state);
            }
        }
        RebirthBossEventDirector.RestoreSchedules(schedules);
        if (fileVersion < 8)
            RebirthBossEventRegistry.CompactTerminalEvents();
        RebirthBossEventRewardRegistry.RebuildFromEvents();
        dirty = false;
        nextSave = Time.realtimeSinceStartup + SaveIntervalSeconds;
        RebirthBossEventDiagnostics.Write(
            "loaded schedules=" + schedules.Count +
            " liveEvents=" + RebirthBossEventRegistry.SnapshotEvents().Count +
            " archivedEvents=" + RebirthBossEventRegistry.SnapshotArchivedEvents().Count);
    }
    private static int ReadCount(BinaryReader reader, int maximum) { int value = reader.ReadInt32(); if (value < 0 || value > maximum) throw new InvalidDataException("Invalid Boss Events record count."); return value; }
    private static void ResetRuntimeOnly() { RebirthBossEventDirector.Reset(); RebirthBossEventLifecycle.Reset(); dirty = false; nextSave = 0f; }
    private static void WriteEvent(BinaryWriter w, RebirthBossEventInstance e)
    {
        w.Write(e.EventId.ToByteArray()); w.Write((byte)e.State); WriteProgression(w, e.Progression); WriteOptions(w, e.Options); w.Write(e.ScheduledWorldTime); w.Write(e.StartWorldTime); w.Write(e.BossEntityId);
        w.Write(e.SupportEntityIds.Count); for (int i = 0; i < e.SupportEntityIds.Count; i++) w.Write(e.SupportEntityIds[i]); w.Write(e.RegularEntityIds.Count); for (int i = 0; i < e.RegularEntityIds.Count; i++) w.Write(e.RegularEntityIds[i]);
        w.Write(e.RewardIssued); WriteVector3(w, e.LastBossPosition); WriteVector3i(w, e.RewardBlockPosition); w.Write(e.RewardBlockPlaced); w.Write(e.RewardExpiresWorldTime); w.Write(e.RewardEntityId); w.Write(e.LastObservedWorldTime); w.Write(e.MissingBossSinceWorldTime); w.Write(e.FailureReason ?? string.Empty);
    }
    private static RebirthBossEventInstance ReadEvent(BinaryReader r, ushort fileVersion)
    {
        var e = new RebirthBossEventInstance { EventId = new Guid(r.ReadBytes(16)), State = (RebirthBossEventState)r.ReadByte(), Progression = ReadProgression(r), Options = ReadOptions(r, fileVersion), ScheduledWorldTime = r.ReadUInt64(), StartWorldTime = r.ReadUInt64(), BossEntityId = r.ReadInt32() };
        int supports = ReadCount(r, 32); for (int i = 0; i < supports; i++) e.SupportEntityIds.Add(r.ReadInt32()); int regulars = ReadCount(r, 256); for (int i = 0; i < regulars; i++) e.RegularEntityIds.Add(r.ReadInt32());
        e.RewardIssued = r.ReadBoolean();
        if (fileVersion >= 2)
        {
            e.LastBossPosition = ReadVector3(r);
            e.RewardBlockPosition = ReadVector3i(r);
            e.RewardBlockPlaced = r.ReadBoolean();
            e.RewardExpiresWorldTime = r.ReadUInt64();
        }
        if (fileVersion >= 4)
            e.RewardEntityId = r.ReadInt32();
        e.LastObservedWorldTime = r.ReadUInt64(); e.MissingBossSinceWorldTime = r.ReadUInt64(); e.FailureReason = r.ReadString(); return e;
    }

    private static void WriteVector3(BinaryWriter w, Vector3 value) { w.Write(value.x); w.Write(value.y); w.Write(value.z); }
    private static Vector3 ReadVector3(BinaryReader r) { return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }
    private static void WriteVector3i(BinaryWriter w, Vector3i value) { w.Write(value.x); w.Write(value.y); w.Write(value.z); }
    private static Vector3i ReadVector3i(BinaryReader r) { return new Vector3i(r.ReadInt32(), r.ReadInt32(), r.ReadInt32()); }
    private static void WriteProgression(BinaryWriter w, RebirthBossEventProgressionSnapshot p)
    {
        w.Write(p != null); if (p == null) return; w.Write(p.OwnerStableId ?? string.Empty); w.Write(p.OwnerEntityId); w.Write(p.OwnerDisplayName ?? string.Empty); w.Write(p.PlayerLevel); w.Write(p.PlayerGameStage); w.Write(p.EffectiveEventGameStage); w.Write((byte)p.Difficulty); w.Write((byte)p.Size); w.Write(p.RewardTier); w.Write(p.WorldDay); w.Write(p.EventSeed);
    }
    private static RebirthBossEventProgressionSnapshot ReadProgression(BinaryReader r)
    {
        if (!r.ReadBoolean()) return null; return new RebirthBossEventProgressionSnapshot { OwnerStableId = r.ReadString(), OwnerEntityId = r.ReadInt32(), OwnerDisplayName = r.ReadString(), PlayerLevel = r.ReadInt32(), PlayerGameStage = r.ReadInt32(), EffectiveEventGameStage = r.ReadInt32(), Difficulty = (RebirthBossEventDifficulty)r.ReadByte(), Size = (RebirthBossEventSize)r.ReadByte(), RewardTier = r.ReadInt32(), WorldDay = r.ReadInt32(), EventSeed = r.ReadInt32() };
    }
    private static void WriteOptions(BinaryWriter w, RebirthBossEventOptionSnapshot o)
    {
        w.Write(o != null); if (o == null) return; w.Write(o.Enabled); w.Write(o.MinimumPlayerLevel); w.Write((byte)o.Frequency); w.Write(o.MaximumPerDay); w.Write((byte)o.Size); w.Write((byte)o.Difficulty); w.Write((byte)o.Time); w.Write(o.BloodMoonDayEvents); w.Write((byte)o.Restriction); w.Write(o.Rewards); w.Write(o.Notifications);
    }
    private static RebirthBossEventOptionSnapshot ReadOptions(BinaryReader r, ushort fileVersion)
    {
        if (!r.ReadBoolean()) return new RebirthBossEventOptionSnapshot();
        bool enabled = r.ReadBoolean();
        int minimumPlayerLevel = fileVersion >= 7 ? r.ReadInt32() : DefaultPersistedMinimumPlayerLevel;
        return new RebirthBossEventOptionSnapshot { Enabled = enabled, MinimumPlayerLevel = minimumPlayerLevel, Frequency = (RebirthBossEventFrequency)r.ReadByte(), MaximumPerDay = r.ReadInt32(), Size = (RebirthBossEventSize)r.ReadByte(), Difficulty = (RebirthBossEventDifficulty)r.ReadByte(), Time = (RebirthBossEventTime)r.ReadByte(), BloodMoonDayEvents = r.ReadBoolean(), Restriction = (RebirthBossEventRestriction)r.ReadByte(), Rewards = r.ReadBoolean(), Notifications = r.ReadBoolean() };
    }
    private static void WriteEntity(BinaryWriter w, int id, RebirthBossEventEntityState s)
    {
        w.Write(id);
        w.Write(s.EventId.ToByteArray());
        w.Write(s.OwnerStableId ?? string.Empty);
        w.Write(s.OwnerEntityId);
        w.Write((byte)s.Role);
        w.Write(s.HealthMultiplier);
        w.Write(s.DamageMultiplier);
        w.Write(s.FixedSpeed);
        w.Write(s.Scale);
        w.Write(s.RewardSource);
        w.Write(s.ScalingApplied);
        w.Write(s.LastKnownHealth);
        w.Write(s.LastKnownMaxHealth);
        w.Write(s.LastKnownHealthFraction);
        w.Write(s.HasHealthSnapshot);
    }

    private static RebirthBossEventEntityState ReadEntity(
        BinaryReader r,
        ushort fileVersion,
        out int id)
    {
        id = r.ReadInt32();

        RebirthBossEventEntityState state =
            new RebirthBossEventEntityState
            {
                EventId = new Guid(r.ReadBytes(16)),
                OwnerStableId = r.ReadString(),
                OwnerEntityId = r.ReadInt32(),
                Role = (RebirthBossEventRole)r.ReadByte(),
                HealthMultiplier = r.ReadSingle(),
                DamageMultiplier = r.ReadSingle(),
                FixedSpeed = r.ReadSingle(),
                Scale = r.ReadSingle(),
                RewardSource = r.ReadBoolean(),
                ScalingApplied = r.ReadBoolean()
            };

        if (fileVersion >= 6)
        {
            state.LastKnownHealth = r.ReadInt32();
            state.LastKnownMaxHealth = r.ReadInt32();
            state.LastKnownHealthFraction = r.ReadSingle();
            state.HasHealthSnapshot = r.ReadBoolean();
        }

        return state;
    }
}


public static class RebirthBossEventRewardService
{
    private const ulong RewardLifetimeTicks = 72000UL;
    private const int PlacementRadius = 5;
    private const string RewardCrateClassName = "RB_EventLootContainer";

    public static void TryIssue(
        World world,
        RebirthBossEventInstance e)
    {
        if (world == null ||
            world.IsRemote() ||
            e == null ||
            e.Progression == null ||
            e.RewardIssued)
            return;

        Vector3i position;
        if (!TryFindPosition(
                world,
                e.LastBossPosition,
                out position))
        {
            e.FailureReason =
                "reward-placement-unavailable";
            RebirthBossEventPersistence.MarkDirty();
            return;
        }

        EntityLootContainer crate;
        if (!TrySpawnEventRewardCrate(
                world,
                e,
                position,
                out crate))
        {
            e.FailureReason =
                "reward-event-crate-spawn-failed";
            RebirthBossEventPersistence.MarkDirty();
            return;
        }

        e.RewardBlockPosition =
            World.worldToBlockPos(crate.position);
        e.RewardBlockPlaced = true;
        e.RewardEntityId = crate.entityId;
        e.RewardIssued = true;
        e.RewardExpiresWorldTime =
            world.worldTime + RewardLifetimeTicks;
        e.State = RebirthBossEventState.Completed;

        RebirthBossEventOwnerNetwork.Send(
            world,
            e,
            RebirthBossEventOwnerUpdateKind.RewardAvailable);

        RebirthBossEventPersistence.MarkDirty();

        RebirthBossEventDiagnostics.Write(
            "reward 2.6-style event crate issued event="
            + e.EventId
            + " entity=" + crate.entityId
            + " class=" + RewardCrateClassName
            + " loot=airDrop"
            + " pos=" + crate.position
            + " owner="
            + e.Progression.OwnerDisplayName);
    }

    public static void Update(
        World world,
        RebirthBossEventInstance e)
    {
        if (world == null ||
            e == null ||
            !e.RewardIssued ||
            !e.RewardBlockPlaced)
            return;

        // v103 and earlier persisted a custom reward block. Convert it once to
        // the restored 2.6-style event loot entity.
        if (e.RewardEntityId < 0)
        {
            if (TryMigrateLegacyRewardBlock(world, e))
                return;

            // If the legacy block is not currently present, preserve the record
            // until its lifetime expires; the chunk may simply be unloaded.
            if (world.worldTime < e.RewardExpiresWorldTime)
                return;

            MarkRemoved(world, e);
            return;
        }

        Entity rewardEntity = world.GetEntity(e.RewardEntityId);
        EntityLootContainer crate = rewardEntity as EntityLootContainer;

        if (crate != null)
        {
            e.RewardBlockPosition =
                World.worldToBlockPos(crate.position);

            // EntityLootContainer.Start() intentionally marks the entity dead;
            // that is its normal stationary-loot state. Only bRemoved/unload or
            // our explicit event expiry means the reward is actually gone.
            if (crate.bRemoved || crate.IsMarkedForUnload())
            {
                MarkRemoved(world, e);
                return;
            }

            if (world.worldTime < e.RewardExpiresWorldTime)
                return;

            world.RemoveEntity(
                crate.entityId,
                EnumRemoveEntityReason.Despawned);
            MarkRemoved(world, e);
            return;
        }

        // v105 used sc_General, which is EntitySupplyCrate and therefore brings
        // the parachute/landing lifecycle. Replace any still-loaded v105 reward
        // once with the restored stationary 2.6-style event crate.
        EntitySupplyCrate oldSupplyCrate = rewardEntity as EntitySupplyCrate;
        if (oldSupplyCrate != null)
        {
            Vector3i position =
                World.worldToBlockPos(oldSupplyCrate.position);
            int oldEntityId = oldSupplyCrate.entityId;

            world.RemoveEntity(
                oldEntityId,
                EnumRemoveEntityReason.Despawned);

            EntityLootContainer migrated;
            if (TrySpawnEventRewardCrate(
                    world,
                    e,
                    position,
                    out migrated))
            {
                e.RewardEntityId = migrated.entityId;
                e.RewardBlockPosition =
                    World.worldToBlockPos(migrated.position);

                RebirthBossEventOwnerNetwork.Send(
                    world,
                    e,
                    RebirthBossEventOwnerUpdateKind.RewardAvailable);
                RebirthBossEventPersistence.MarkDirty();

                RebirthBossEventDiagnostics.Write(
                    "migrated v105 supply reward event="
                    + e.EventId
                    + " oldEntity=" + oldEntityId
                    + " newEntity=" + migrated.entityId
                    + " loot=airDrop");
                return;
            }

            e.FailureReason = "v105-reward-migration-failed";
            RebirthBossEventPersistence.MarkDirty();
            return;
        }

        // Reward entities can be temporarily absent while their chunk is not
        // loaded. Do not treat that as looted until the event reward expires.
        if (world.worldTime < e.RewardExpiresWorldTime)
            return;

        MarkRemoved(world, e);
    }

    public static void NotifyRewardEntityRemoved(
        World world,
        int entityId)
    {
        if (world == null ||
            entityId < 0)
            return;

        List<RebirthBossEventInstance> events =
            RebirthBossEventRegistry.SnapshotEvents();

        for (int i = 0; i < events.Count; i++)
        {
            RebirthBossEventInstance e = events[i];
            if (e == null ||
                e.RewardEntityId != entityId ||
                !e.RewardBlockPlaced)
                continue;

            MarkRemoved(world, e);
            break;
        }
    }

    private static bool TrySpawnEventRewardCrate(
        World world,
        RebirthBossEventInstance e,
        Vector3i position,
        out EntityLootContainer crate)
    {
        crate = null;

        int classId =
            EntityClass.FromString(
                RewardCrateClassName);

        if (EntityClass.GetEntityClass(classId) == null)
        {
            RebirthBossEventDiagnostics.Write(
                "reward missing entity class "
                + RewardCrateClassName);
            return false;
        }

        Vector3 spawnPosition =
            new Vector3(
                position.x + 0.5f,
                position.y + 0.25f,
                position.z + 0.5f);

        Vector3 rotation =
            new Vector3(
                0f,
                world.GetGameRandom().RandomFloat * 360f,
                0f);

        EntityLootContainer candidate = null;
        try
        {
            candidate =
                EntityFactory.CreateEntity(
                    classId,
                    spawnPosition,
                    rotation)
                as EntityLootContainer;

            if (candidate == null)
                return false;

            candidate.SetSpawnerSource(
                EnumSpawnerSource.Dynamic);

            candidate.spawnById =
                e.Progression.OwnerEntityId;
            candidate.spawnByName =
                e.Progression.OwnerDisplayName
                ?? string.Empty;
            candidate.spawnByAllowShare = false;
            candidate.OverrideLootList = "airDrop";
            candidate.OverrideName = "sc_General";

            world.SpawnEntityInWorld(candidate);
            crate = candidate;
            return true;
        }
        catch (Exception ex)
        {
            if (candidate != null)
            {
                try
                {
                    world.RemoveEntity(
                        candidate.entityId,
                        EnumRemoveEntityReason.Despawned);
                }
                catch
                {
                }
            }
            RebirthBossEventDiagnostics.Write(
                "reward crate publish failed event=" + e.EventId +
                " error=" + ex.GetType().Name);
            crate = null;
            return false;
        }
    }

    private static bool TryMigrateLegacyRewardBlock(
        World world,
        RebirthBossEventInstance e)
    {
        if (e == null ||
            e.Progression == null ||
            e.RewardEntityId >= 0 ||
            !e.RewardBlockPlaced)
            return false;

        Vector3i position = e.RewardBlockPosition;
        BlockValue existing = world.GetBlock(position);
        if (!IsOwnedLegacyRewardBlock(e, position, existing))
        {
            RebirthBossEventDiagnostics.Write(
                "legacy reward migration refused event=" + e.EventId +
                " pos=" + position +
                " block=" +
                (existing.Block != null ? existing.Block.GetBlockName() : "<none>"));
            return false;
        }

        // Publish the replacement first. If creation fails the legacy reward is
        // untouched and remains the single authoritative reward.
        EntityLootContainer crate;
        if (!TrySpawnEventRewardCrate(
                world,
                e,
                position,
                out crate))
        {
            e.FailureReason = "legacy-reward-migration-create-failed";
            RebirthBossEventPersistence.MarkDirty();
            return false;
        }

        // Revalidate the exact legacy block incarnation immediately before the
        // destructive step. A player/world mutation in between must not be
        // mistaken for our old reward.
        BlockValue beforeRemove = world.GetBlock(position);
        if (beforeRemove.rawData != existing.rawData ||
            beforeRemove.damage != existing.damage ||
            !IsOwnedLegacyRewardBlock(e, position, beforeRemove))
        {
            CompensateRewardEntity(world, crate);
            return false;
        }

        Exception removeFailure = null;
        try
        {
            world.SetBlockRPC(
                (BlockValueRef)position,
                BlockValue.Air);
        }
        catch (Exception ex)
        {
            removeFailure = ex;
        }

        if (!world.GetBlock(position).isair)
        {
            // Native block removal did not commit. Keep the legacy reward and
            // compensate the newly prepared entity.
            CompensateRewardEntity(world, crate);
            e.FailureReason =
                removeFailure != null
                    ? "legacy-reward-migration-remove-exception:" + removeFailure.GetType().Name
                    : "legacy-reward-migration-remove-not-committed";
            RebirthBossEventPersistence.MarkDirty();
            return false;
        }

        // The destructive mutation committed. Publish authoritative event state
        // before best-effort client notifications so a network exception cannot
        // create a zero-reward persistence state.
        e.RewardEntityId = crate.entityId;
        e.RewardBlockPosition = World.worldToBlockPos(crate.position);
        e.RewardBlockPlaced = true;
        e.FailureReason =
            removeFailure != null
                ? "legacy-reward-migration-remove-threw-after-commit"
                : null;
        RebirthBossEventPersistence.MarkDirty();

        RebirthBossEventRewardRegistry.Remove(position);
        try
        {
            RebirthBossEventRewardAccessNetwork.BroadcastRemove(world, position);
            RebirthBossEventOwnerNetwork.Send(
                world,
                e,
                RebirthBossEventOwnerUpdateKind.RewardAvailable);
        }
        catch (Exception ex)
        {
            RebirthBossEventDiagnostics.Write(
                "legacy reward migration notification failed event=" +
                e.EventId + " error=" + ex.GetType().Name);
        }

        RebirthBossEventDiagnostics.Write(
            "migrated legacy reward block event="
            + e.EventId
            + " entity=" + crate.entityId
            + " pos=" + crate.position);

        return true;
    }

    private static bool IsOwnedLegacyRewardBlock(
        RebirthBossEventInstance e,
        Vector3i position,
        BlockValue value)
    {
        if (e == null || e.Progression == null || value.isair || value.Block == null)
            return false;

        int tier = Math.Max(1, Math.Min(5, e.Progression.RewardTier));
        string expected = "rebirthBossRewardCrateT" + tier.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(
                value.Block.GetBlockName(),
                expected,
                StringComparison.OrdinalIgnoreCase))
            return false;

        RebirthBossEventRewardOwner owner;
        return RebirthBossEventRewardRegistry.TryGet(position, out owner) &&
            owner != null &&
            owner.EventId == e.EventId &&
            string.Equals(
                owner.StableId ?? string.Empty,
                e.Progression.OwnerStableId ?? string.Empty,
                StringComparison.Ordinal);
    }

    private static void CompensateRewardEntity(
        World world,
        EntityLootContainer crate)
    {
        if (world == null || crate == null)
            return;
        try
        {
            world.RemoveEntity(
                crate.entityId,
                EnumRemoveEntityReason.Despawned);
        }
        catch
        {
        }
    }

    private static void MarkRemoved(
        World world,
        RebirthBossEventInstance e)
    {
        if (e == null)
            return;

        if (e.RewardEntityId < 0)
        {
            RebirthBossEventRewardRegistry.Remove(
                e.RewardBlockPosition);

            RebirthBossEventRewardAccessNetwork
                .BroadcastRemove(
                    world,
                    e.RewardBlockPosition);
        }

        e.RewardEntityId = -1;
        e.RewardBlockPlaced = false;

        RebirthBossEventOwnerNetwork.Send(
            world,
            e,
            RebirthBossEventOwnerUpdateKind.RewardRemoved);

        RebirthBossEventPersistence.MarkDirty();
    }

    private static bool TryFindPosition(
        World world,
        Vector3 origin,
        out Vector3i result)
    {
        Vector3i center =
            new Vector3i(
                Mathf.FloorToInt(origin.x),
                Mathf.FloorToInt(origin.y),
                Mathf.FloorToInt(origin.z));

        for (int radius = 0;
             radius <= PlacementRadius;
             radius++)
        {
            for (int x = -radius;
                 x <= radius;
                 x++)
            {
                for (int z = -radius;
                     z <= radius;
                     z++)
                {
                    if (radius > 0 &&
                        Math.Abs(x) != radius &&
                        Math.Abs(z) != radius)
                        continue;

                    Vector3i candidate =
                        new Vector3i(
                            center.x + x,
                            center.y,
                            center.z + z);

                    for (int y = 4;
                         y >= -4;
                         y--)
                    {
                        Vector3i p =
                            new Vector3i(
                                candidate.x,
                                candidate.y + y,
                                candidate.z);

                        BlockValue floor =
                            world.GetBlock(
                                new Vector3i(
                                    p.x,
                                    p.y - 1,
                                    p.z));

                        if (floor.isair ||
                            floor.Block.blockMaterial.IsLiquid)
                            continue;

                        if (!world.GetBlock(p).isair ||
                            !world.GetBlock(
                                new Vector3i(
                                    p.x,
                                    p.y + 1,
                                    p.z)).isair)
                            continue;

                        result = p;
                        return true;
                    }
                }
            }
        }

        result = Vector3i.zero;
        return false;
    }
}

public static class RebirthBossEventDeathLootService
{
    // Support bag presentation scales from the event owner's level snapshot:
    // 15-49 = native yellow bag, 50-99 = native blue bag, 100+ = native red bag.
    // The event snapshot is captured at event creation and persisted, so the bag
    // tier cannot change if the owner levels during the encounter or reloads.
    private const string SupportLootEntityClassYellow =
        "EntityLootContainerRegular";
    private const string SupportLootEntityClassBlue =
        "EntityLootContainerStrong";
    private const string SupportLootEntityClassRed =
        "EntityLootContainerBoss";

    public static void OnEntityKilled(
        ref ModEvents.SEntityKilledData data)
    {
        World world =
            GameManager.Instance != null
                ? GameManager.Instance.World
                : null;

        if (world == null ||
            world.IsRemote() ||
            data.KilledEntitiy == null)
            return;

        // v105 rewards were native EntitySupplyCrate instances. Keep this
        // cleanup path only for a still-active v105 crate during migration.
        EntitySupplyCrate legacySupplyCrate =
            data.KilledEntitiy as EntitySupplyCrate;

        if (legacySupplyCrate != null)
        {
            RebirthBossEventRewardService
                .NotifyRewardEntityRemoved(
                    world,
                    legacySupplyCrate.entityId);
            return;
        }

        EntityAlive killed =
            data.KilledEntitiy as EntityAlive;

        if (killed == null)
            return;

        RebirthBossEventEntityState state;
        if (!RebirthBossEventRegistry.TryGetEntity(
                killed.entityId,
                out state) ||
            state == null)
            return;

        EntityHuman killedHuman =
            killed as EntityHuman;

        RebirthBossEventDiagnostics.Write(
            "event entity death entity="
            + killed.entityId
            + " role=" + state.Role
            + " class=" + killed.EntityClass.entityClassName
            + " killer="
            + (data.KillingEntity != null
                ? data.KillingEntity.entityId.ToString(
                    CultureInfo.InvariantCulture)
                : "none")
            + " pos=" + killed.position
            + " target="
            + (killed.GetAttackTarget() != null
                ? killed.GetAttackTarget().entityId.ToString(
                    CultureInfo.InvariantCulture)
                : "none")
            + (killedHuman != null
                ? " timeToDie="
                  + killedHuman.timeToDie.ToString(
                      CultureInfo.InvariantCulture)
                : string.Empty));

        if (state.Role ==
            RebirthBossEventRole.Support)
        {
            int eventOwnerLevel =
                RebirthBossEventDirector.DefaultMinimumPlayerLevel;
            RebirthBossEventInstance supportEvent;
            if (RebirthBossEventRegistry.TryGet(
                    state.EventId,
                    out supportEvent) &&
                supportEvent != null &&
                supportEvent.Progression != null)
            {
                eventOwnerLevel =
                    supportEvent.Progression.PlayerLevel;
            }

            string bagTier;
            string bagClass =
                GetSupportLootEntityClass(
                    eventOwnerLevel,
                    out bagTier);

            bool customBagPublished =
                SpawnSupportBag(
                    world,
                    killed,
                    bagClass,
                    bagTier);

            // EntityAlive.OnEntityDeath invokes ModEvents.EntityKilled before
            // dropItemOnDeath(). Suppress native loot only after the custom bag
            // is actually published; otherwise native loot remains the fallback.
            if (customBagPublished)
                killed.lootDropProb = 0f;

            RebirthBossEventDiagnostics.Write(
                "support loot drop entity="
                + killed.entityId
                + " eventOwnerLevel="
                + eventOwnerLevel.ToString(CultureInfo.InvariantCulture)
                + " tier=" + bagTier
                + " class=" + bagClass
                + " customPublished=" + customBagPublished
                + " fallbackNative=" + (!customBagPublished));

            RebirthBossEventLifecycle.NotifyTrackedEntityKilled(
                world,
                killed,
                state);
            return;
        }

        if (state.Role ==
            RebirthBossEventRole.Boss)
        {
            RebirthBossEventInstance e;
            if (RebirthBossEventRegistry.TryGet(
                    state.EventId,
                    out e) &&
                e != null &&
                e.Options != null &&
                e.Options.Rewards)
            {
                // The event reward is the restored stationary 2.6-style
                // event crate. Suppress an unrelated normal zombie loot bag
                // so the boss reward has one clear source.
                killed.lootDropProb = 0f;
            }
        }

        // Death is authoritative. Remove the tracked role immediately so a later
        // chunk unload cannot be confused with a still-live persistent event entity.
        RebirthBossEventLifecycle.NotifyTrackedEntityKilled(
            world,
            killed,
            state);
    }

    private static string GetSupportLootEntityClass(
        int eventOwnerLevel,
        out string tier)
    {
        if (eventOwnerLevel >= 100)
        {
            tier = "red";
            return SupportLootEntityClassRed;
        }

        if (eventOwnerLevel >= 50)
        {
            tier = "blue";
            return SupportLootEntityClassBlue;
        }

        tier = "yellow";
        return SupportLootEntityClassYellow;
    }

    private static bool SpawnSupportBag(
        World world,
        EntityAlive killed,
        string bagClass,
        string bagTier)
    {
        int classId =
            EntityClass.FromString(
                bagClass);

        if (EntityClass.GetEntityClass(classId) == null)
        {
            RebirthBossEventDiagnostics.Write(
                "support " + bagTier +
                " loot missing entity class " +
                bagClass);
            return false;
        }

        Vector3 position =
            killed.GetPosition();
        position.y += 0.9f;

        EntityLootContainer bag = null;
        try
        {
            bag =
                EntityFactory.CreateEntity(
                    classId,
                    position,
                    Vector3.zero)
                as EntityLootContainer;

            if (bag == null)
            {
                RebirthBossEventDiagnostics.Write(
                    "support " + bagTier +
                    " loot create failed entity=" +
                    killed.entityId);
                return false;
            }

            world.SpawnEntityInWorld(bag);
            bag.transform.localScale =
                new Vector3(1.25f, 1.25f, 1.25f);

            Manager.BroadcastPlay(
                position,
                "zpack_spawn");
            return true;
        }
        catch (Exception ex)
        {
            if (bag != null)
            {
                try
                {
                    world.RemoveEntity(
                        bag.entityId,
                        EnumRemoveEntityReason.Despawned);
                }
                catch
                {
                }
            }
            RebirthBossEventDiagnostics.Write(
                "support " + bagTier +
                " loot publish failed entity=" + killed.entityId +
                " error=" + ex.GetType().Name);
            return false;
        }
    }

}


public sealed class RebirthBossEventRewardOwner
{
    public Guid EventId;
    public string StableId;
    public int EntityId;
    public string DisplayName;
    public ulong ExpiresWorldTime;
}

public static class RebirthBossEventRewardRegistry
{
    private static readonly Dictionary<Vector3i, RebirthBossEventRewardOwner> Owners = new Dictionary<Vector3i, RebirthBossEventRewardOwner>();
    public static void Register(Vector3i pos, Guid eventId, string stableId, int entityId, string displayName, ulong expires)
    {
        Owners[pos] = new RebirthBossEventRewardOwner { EventId = eventId, StableId = stableId ?? string.Empty, EntityId = entityId, DisplayName = displayName ?? string.Empty, ExpiresWorldTime = expires };
    }
    public static bool TryGet(Vector3i pos, out RebirthBossEventRewardOwner owner) { return Owners.TryGetValue(pos, out owner); }
    public static void Remove(Vector3i pos) { Owners.Remove(pos); }
    public static void Reset() { Owners.Clear(); }

    public static void RebuildFromEvents()
    {
        Owners.Clear();
        foreach (RebirthBossEventInstance e in RebirthBossEventRegistry.ActiveEvents)
            if (e != null && e.RewardIssued && e.RewardBlockPlaced && e.RewardEntityId < 0 && e.Progression != null)
                Register(e.RewardBlockPosition, e.EventId, e.Progression.OwnerStableId, e.Progression.OwnerEntityId, e.Progression.OwnerDisplayName, e.RewardExpiresWorldTime);
    }
}

public static class RebirthBossEventRewardAccessNetwork
{
    public static void BroadcastRegister(
        World world,
        RebirthBossEventInstance e)
    {
        if (world == null ||
            world.IsRemote() ||
            e == null ||
            e.Progression == null ||
            !e.RewardBlockPlaced ||
            e.RewardEntityId >= 0)
            return;

        Broadcast(
            NetPackageManager
                .GetPackage<NetPackageRebirthBossRewardAccess>()
                .Setup(
                    false,
                    e.RewardBlockPosition,
                    e.EventId,
                    e.Progression.OwnerStableId,
                    e.Progression.OwnerEntityId,
                    e.Progression.OwnerDisplayName,
                    e.RewardExpiresWorldTime));
    }

    public static void BroadcastRemove(
        World world,
        Vector3i position)
    {
        if (world == null || world.IsRemote())
            return;

        Broadcast(
            NetPackageManager
                .GetPackage<NetPackageRebirthBossRewardAccess>()
                .Setup(
                    true,
                    position,
                    Guid.Empty,
                    string.Empty,
                    -1,
                    string.Empty,
                    0UL));
    }

    public static void BroadcastCurrent(World world)
    {
        if (world == null || world.IsRemote())
            return;

        foreach (RebirthBossEventInstance e
                 in RebirthBossEventRegistry.ActiveEvents)
        {
            if (e == null ||
                e.Progression == null ||
                !e.RewardIssued ||
                !e.RewardBlockPlaced ||
                e.RewardEntityId >= 0)
                continue;

            BroadcastRegister(world, e);
        }
    }

    private static void Broadcast(NetPackage package)
    {
        ConnectionManager connection =
            SingletonMonoBehaviour<ConnectionManager>.Instance;

        if (connection == null ||
            !connection.IsServer ||
            package == null)
            return;

        // Server-wide broadcast is intentional: every remote client must know
        // reward ownership locally before TEFeatureStorage.OnBlockActivated runs.
        connection.SendPackage(package);
    }
}

[Preserve]
public sealed class NetPackageRebirthBossRewardAccess : NetPackage
{
    private const int MaxOwnerStableIdLength = 256;
    private const int MaxOwnerDisplayNameLength = 256;
    private bool remove;
    private Vector3i position;
    private Guid eventId;
    private string ownerStableId;
    private int ownerEntityId;
    private string ownerDisplayName;
    private ulong expiresWorldTime;

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.ToClient; }
    }

    public NetPackageRebirthBossRewardAccess Setup(
        bool removeEntry,
        Vector3i blockPosition,
        Guid id,
        string stableId,
        int entityId,
        string displayName,
        ulong expiry)
    {
        remove = removeEntry;
        position = blockPosition;
        eventId = id;
        ownerStableId = stableId ?? string.Empty;
        ownerEntityId = entityId;
        ownerDisplayName = displayName ?? string.Empty;
        expiresWorldTime = expiry;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader r = (BinaryReader)reader;

        remove = r.ReadBoolean();
        position =
            new Vector3i(
                r.ReadInt32(),
                r.ReadInt32(),
                r.ReadInt32());
        eventId = new Guid(r.ReadBytes(16));
        ownerStableId = RebirthSurvivorNetworkCodec.ReadBoundedString(r, MaxOwnerStableIdLength);
        ownerEntityId = r.ReadInt32();
        ownerDisplayName = RebirthSurvivorNetworkCodec.ReadBoundedString(r, MaxOwnerDisplayNameLength);
        expiresWorldTime = r.ReadUInt64();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter w = (BinaryWriter)writer;

        w.Write(remove);
        w.Write(position.x);
        w.Write(position.y);
        w.Write(position.z);
        w.Write(eventId.ToByteArray());
        RebirthSurvivorNetworkCodec.WriteString(w, ownerStableId, MaxOwnerStableIdLength);
        w.Write(ownerEntityId);
        RebirthSurvivorNetworkCodec.WriteString(w, ownerDisplayName, MaxOwnerDisplayNameLength);
        w.Write(expiresWorldTime);
    }

    public override void ProcessPackage(
        World world,
        GameManager callbacks)
    {
        if (remove)
        {
            RebirthBossEventRewardRegistry.Remove(position);
            return;
        }

        RebirthBossEventRewardRegistry.Register(
            position,
            eventId,
            ownerStableId,
            ownerEntityId,
            ownerDisplayName,
            expiresWorldTime);
    }

    public int GetLength()
    {
        return 48 +
               RebirthSurvivorNetworkCodec.EstimateString(ownerStableId, MaxOwnerStableIdLength) +
               RebirthSurvivorNetworkCodec.EstimateString(ownerDisplayName, MaxOwnerDisplayNameLength);
    }
}

public static class RebirthBossEventRewardAccessInstaller
{
    private static bool installed;

    public static void Install()
    {
        if (installed) return;
        installed = true;
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.boss-events.reward-access.3.1"), typeof(RebirthBossEventRewardStorageActivationPatch));
    }
}

[HarmonyPatch(typeof(TEFeatureStorage), nameof(TEFeatureStorage.OnBlockActivated))]
internal static class RebirthBossEventRewardStorageActivationPatch
{
    private static bool Prefix(ref bool __result, Vector3i __2, EntityPlayerLocal __4)
    {
        RebirthBossEventRewardOwner owner;
        if (!RebirthBossEventRewardRegistry.TryGet(__2, out owner)) return true;
        if (__4 != null && string.Equals(RebirthBossEventIdentity.GetStableId(__4), owner.StableId, StringComparison.Ordinal)) return true;
        if (__4 != null) GameManager.ShowTooltip(__4, string.Format(Localization.Get("rebirthBossRewardDenied"), owner.DisplayName), string.Empty, "ui_denied", null);
        Manager.PlayInsidePlayerHead("ui_denied");
        __result = false;
        return false;
    }
}


public sealed class RebirthBossEventOwnerClientState
{
    public Guid EventId;
    public RebirthBossEventOwnerUpdateKind Kind;
    public RebirthBossEventState State;
    public int BossEntityId;
    public int[] SupportEntityIds = new int[0];
    public Vector3 BossPosition;
    public Vector3i RewardPosition;
    public bool RewardPlaced;
    public ulong RewardExpiresWorldTime;
    public bool ShowNotification;
}

public static class RebirthBossEventOwnerClientRegistry
{
    private static readonly Dictionary<Guid, RebirthBossEventOwnerClientState> States = new Dictionary<Guid, RebirthBossEventOwnerClientState>();
    public static IEnumerable<RebirthBossEventOwnerClientState> ActiveStates { get { return States.Values; } }
    public static void Apply(RebirthBossEventOwnerClientState state)
    {
        if (state == null) return;
        if (state.Kind == RebirthBossEventOwnerUpdateKind.Cancelled)
        {
            States.Remove(state.EventId);
        }
        else if (state.Kind == RebirthBossEventOwnerUpdateKind.RewardRemoved &&
                 (state.SupportEntityIds == null ||
                  state.SupportEntityIds.Length == 0))
        {
            States.Remove(state.EventId);
        }
        else
        {
            // Reward removal must not erase still-living support entities.
            // Their nav markers are owned by those entities and remain until
            // each support dies/despawns.
            States[state.EventId] = state;
        }
        if (state.ShowNotification) ShowNotification(state.Kind);
    }
    private static void ShowNotification(RebirthBossEventOwnerUpdateKind kind)
    {
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player == null) return;
        if (kind == RebirthBossEventOwnerUpdateKind.Started)
        {
            // REBIRTH 2.6 start cue: Death's Whisper sound + belt message +
            // one-second Drunk pulse. ShowNotification is already gated by the
            // current Boss Event Notifications option before this method runs.
            player.Buffs.AddBuff("FuriousRamsayEventTrigger", -1, false, false);
            return;
        }

        string key = null;
        switch (kind)
        {
            case RebirthBossEventOwnerUpdateKind.BossDefeated: key = "rebirthBossEventBossDefeated"; break;
            case RebirthBossEventOwnerUpdateKind.RewardAvailable: key = "rebirthBossEventRewardAvailable"; break;
            case RebirthBossEventOwnerUpdateKind.RewardRemoved: key = "rebirthBossEventRewardRemoved"; break;
        }
        if (!string.IsNullOrEmpty(key)) GameManager.ShowTooltip(player, Localization.Get(key), true, false, 5f);
    }
    public static void Reset() { States.Clear(); }
}



public sealed class RebirthBossEventHudMarkerHost : MonoBehaviour
{
    private static RebirthBossEventHudMarkerHost instance;
    private static Texture2D whiteTexture;
    private static GUIStyle markerStyle;
    private static GUIStyle distanceStyle;
    private const float EdgePadding = 34f;

    public static void Ensure()
    {
        if (instance != null) return;
        GameObject host = new GameObject("REBIRTH_BossEventHudMarkers");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<RebirthBossEventHudMarkerHost>();
    }

    public static void DestroyHost()
    {
        if (instance != null) Destroy(instance.gameObject);
        instance = null;
        if (whiteTexture != null) Destroy(whiteTexture);
        whiteTexture = null;
        markerStyle = null;
        distanceStyle = null;
        RebirthBossEventNativeMapMarkerService.Reset();
    }

    private static void EnsureStyles()
    {
        if (whiteTexture == null)
        {
            whiteTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply(false, true);
        }
        if (markerStyle == null)
        {
            markerStyle = new GUIStyle(GUI.skin.label);
            markerStyle.alignment = TextAnchor.MiddleCenter;
            markerStyle.fontSize = 20;
            markerStyle.fontStyle = FontStyle.Bold;
            markerStyle.normal.textColor = Color.white;
        }
        if (distanceStyle == null)
        {
            distanceStyle = new GUIStyle(GUI.skin.label);
            distanceStyle.alignment = TextAnchor.UpperCenter;
            distanceStyle.fontSize = 12;
            distanceStyle.normal.textColor = Color.white;
        }
    }

    private void Update()
    {
        RebirthBossEventNativeMapMarkerService.Update();
    }

    private void OnGUI()
    {
        // v106 has no hand-drawn Boss Event markers. Boss/support use the exact
        // 2.6 entity nav classes, and the reward crate carries the exact 2.6
        // FuriousRamsayDroppedLoot NavObject through its entity class.
    }

    private static void DrawMarker(Camera camera, Vector3 playerPosition, Vector3 worldPosition, Color color, string glyph, float size, bool showDistance)
    {
        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        bool behind = screen.z <= 0.01f;
        float x = screen.x;
        float y = Screen.height - screen.y;
        if (behind)
        {
            x = Screen.width - x;
            y = Screen.height - y;
        }
        x = Mathf.Clamp(x, EdgePadding, Screen.width - EdgePadding);
        y = Mathf.Clamp(y, EdgePadding, Screen.height - EdgePadding);
        Rect iconRect = new Rect(x - size, y - size, size * 2f, size * 2f);
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(iconRect, whiteTexture);
        GUI.color = Color.black;
        GUI.Label(iconRect, glyph, markerStyle);
        GUI.color = previous;
        if (showDistance)
        {
            float distance = Vector3.Distance(playerPosition, worldPosition);
            Rect distanceRect = new Rect(x - 48f, y + size + 1f, 96f, 20f);
            GUI.Label(distanceRect, Mathf.RoundToInt(distance).ToString(CultureInfo.InvariantCulture) + " m", distanceStyle);
        }
    }
}

public static class RebirthBossEventNativeMapMarkerService
{
    private sealed class MarkerSet
    {
        public NavObject Boss;
        public readonly Dictionary<int, NavObject> Supports = new Dictionary<int, NavObject>();
        public NavObject Reward;
    }

    private static readonly Dictionary<Guid, MarkerSet> Markers = new Dictionary<Guid, MarkerSet>();
    private static float nextReconcile;
    private const float ReconcileIntervalSeconds = 1f;
    // Use the exact REBIRTH 2.6 event nav classes. They are appended by
    // Config/nav_objects.xml and use the original Twitch boss/shield sprites,
    // sizes, ranges and head offsets.
    private const string BossClass = "boss_event";
    private const string SupportClass = "support_event";
    private const string RewardClass = "waypoint";
    private const string RewardSprite = "ui_game_symbol_loot";

    public static void Update()
    {
        if (!RebirthHotPathFeatureFlags.IsMapMarkersReservationsEnabled)
        {
            if (Markers.Count > 0)
                Reset();
            return;
        }
        if (Time.realtimeSinceStartup < nextReconcile) return;
        nextReconcile = Time.realtimeSinceStartup + ReconcileIntervalSeconds;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (world == null || player == null || GameManager.IsDedicatedServer)
        {
            Reset();
            return;
        }

        HashSet<Guid> live = new HashSet<Guid>();
        foreach (RebirthBossEventOwnerClientState state in RebirthBossEventOwnerClientRegistry.ActiveStates)
        {
            if (state == null) continue;
            live.Add(state.EventId);
            MarkerSet set;
            if (!Markers.TryGetValue(state.EventId, out set))
            {
                set = new MarkerSet();
                Markers.Add(state.EventId, set);
            }

            // Marker lifetime is entity-based, not boss-state-based. Killing
            // the boss must remove only the boss marker; every support marker
            // remains until that support entity actually dies/despawns.
            Entity boss =
                state.BossEntityId >= 0
                    ? world.GetEntity(state.BossEntityId)
                    : null;
            EntityAlive bossAlive = boss as EntityAlive;
            if (bossAlive != null && bossAlive.IsDead())
                boss = null;

            set.Boss = EnsureEntityMarker(
                set.Boss,
                boss,
                BossClass,
                Localization.Get("rebirthBossMapBoss"));
            ReconcileSupports(
                world,
                set,
                state.SupportEntityIds);

            // The restored 2.6 reward EntityLootContainer owns its own
            // FuriousRamsayDroppedLoot nav object. Do not add the v105 custom
            // position-based reward waypoint on top of it.
            Unregister(ref set.Reward);
        }

        if (Markers.Count != live.Count)
        {
            List<Guid> remove = null;
            foreach (KeyValuePair<Guid, MarkerSet> pair in Markers)
            {
                if (live.Contains(pair.Key)) continue;
                if (remove == null) remove = new List<Guid>();
                remove.Add(pair.Key);
            }
            if (remove != null)
            {
                for (int i = 0; i < remove.Count; i++)
                {
                    MarkerSet set = Markers[remove[i]];
                    DestroySet(set);
                    Markers.Remove(remove[i]);
                }
            }
        }
    }

    private static void ReconcileSupports(World world, MarkerSet set, int[] ids)
    {
        HashSet<int> live = new HashSet<int>();
        for (int i = 0; ids != null && i < ids.Length; i++)
        {
            int id = ids[i];
            Entity entity = world.GetEntity(id);
            EntityAlive alive = entity as EntityAlive;
            if (entity == null ||
                (alive != null && alive.IsDead()))
                continue;

            live.Add(id);
            NavObject marker;
            set.Supports.TryGetValue(id, out marker);
            marker = EnsureEntityMarker(marker, entity, SupportClass, Localization.Get("rebirthBossMapSupport"));
            if (marker != null)
                set.Supports[id] = marker;
            else
                set.Supports.Remove(id);
        }

        List<int> remove = null;
        foreach (KeyValuePair<int, NavObject> pair in set.Supports)
        {
            if (live.Contains(pair.Key)) continue;
            if (remove == null) remove = new List<int>();
            remove.Add(pair.Key);
        }
        if (remove == null) return;

        for (int i = 0; i < remove.Count; i++)
        {
            NavObject marker = set.Supports[remove[i]];
            Unregister(marker);
            set.Supports.Remove(remove[i]);
        }
    }

    private static NavObject EnsureEntityMarker(NavObject marker, Entity entity, string navClass, string label)
    {
        if (entity == null)
        {
            Unregister(ref marker);
            return null;
        }

        if (marker != null && !marker.IsTrackedEntity(entity))
            Unregister(ref marker);

        if (marker != null)
            return marker;

        NavObjectClass wanted =
            NavObjectClass.GetNavObjectClass(navClass);
        if (wanted == null)
        {
            RebirthBossEventDiagnostics.Write(
                "event nav FAILED missing class entity="
                + entity.entityId
                + " class=" + navClass);
            return null;
        }

        // Exact 2.6 attachment path: remove the sleeper marker and call the
        // entity's AddNavObject(...) method. Do not substitute a direct manager
        // registration; the old implementation attached the class through the
        // entity itself and that is what we want to preserve in 3.1.
        entity.RemoveNavObject("clear_sleeper");
        entity.AddNavObject(navClass, "", "");

        marker = FindEntityMarker(entity, navClass);

        if (marker != null)
        {
            RebirthBossEventDiagnostics.Write(
                "event nav added via Entity.AddNavObject entity="
                + entity.entityId
                + " class=" + navClass
                + " trackedEntity="
                + marker.IsTrackedEntity(entity));
        }
        else
        {
            RebirthBossEventDiagnostics.Write(
                "event nav FAILED after Entity.AddNavObject entity="
                + entity.entityId
                + " class=" + navClass
                + " label=" + label);
        }

        return marker;
    }

    private static NavObject FindEntityMarker(Entity entity, string navClass)
    {
        if (entity == null || NavObjectManager.Instance == null) return null;
        NavObjectClass wanted = NavObjectClass.GetNavObjectClass(navClass);
        List<NavObject> list = NavObjectManager.Instance.NavObjectList;
        for (int i = 0; i < list.Count; i++)
        {
            NavObject candidate = list[i];
            if (candidate == null || !candidate.IsTrackedEntity(entity)) continue;
            if (candidate.NavObjectClass != null
                && candidate.NavObjectClass.NavObjectClassName == navClass)
                return candidate;
            if (wanted != null && candidate.NavObjectClassList != null
                && candidate.NavObjectClassList.Contains(wanted))
                return candidate;
        }
        return null;
    }

    private static NavObject EnsureRewardMarker(NavObject marker, Vector3 position, string sprite, Color color, string label)
    {
        if (marker == null)
        {
            marker = NavObjectManager.Instance.RegisterNavObject(RewardClass, position, sprite);
            if (marker == null) return null;
            marker.IsActive = false;
            marker.OverrideSpriteName = sprite;
            marker.OverrideColor = color;
            marker.name = label;
        }
        marker.TrackedPosition = position;
        marker.trackedPosition = position;
        return marker;
    }

    private static void ClearSupports(MarkerSet set)
    {
        foreach (NavObject marker in set.Supports.Values) Unregister(marker);
        set.Supports.Clear();
    }

    private static void DestroySet(MarkerSet set)
    {
        if (set == null) return;
        Unregister(ref set.Boss);
        Unregister(ref set.Reward);
        ClearSupports(set);
    }

    private static void Unregister(ref NavObject marker)
    {
        if (marker == null) return;
        Unregister(marker);
        marker = null;
    }

    private static void Unregister(NavObject marker)
    {
        if (marker == null || NavObjectManager.Instance == null) return;
        NavObjectManager.Instance.UnRegisterNavObject(marker);
    }

    public static void Reset()
    {
        foreach (MarkerSet set in Markers.Values) DestroySet(set);
        Markers.Clear();
        nextReconcile = 0f;
    }
}

public static class RebirthBossEventOwnerNetwork
{
    private static float nextSync;
    private const float SyncIntervalSeconds = 10f;

    public static void Update(World world)
    {
        if (world == null || world.IsRemote() || Time.realtimeSinceStartup < nextSync) return;
        nextSync = Time.realtimeSinceStartup + SyncIntervalSeconds;
        List<RebirthBossEventInstance> events = RebirthBossEventRegistry.SnapshotEvents();
        for (int i = 0; i < events.Count; i++)
        {
            RebirthBossEventInstance e = events[i];

            if (e == null || e.Progression == null)
                continue;

            if (e.State == RebirthBossEventState.Active ||
                e.State == RebirthBossEventState.RewardPending ||
                e.RewardBlockPlaced ||
                e.SupportEntityIds.Count > 0)
            {
                Send(
                    world,
                    e,
                    RebirthBossEventOwnerUpdateKind.ActiveSync);
            }
        }

        // Access ownership must exist on non-owner remote clients too. Periodic
        // rebroadcast makes reconnecting/late-joining clients converge without
        // relying on the event owner being the one who joined.
        RebirthBossEventRewardAccessNetwork.BroadcastCurrent(world);
    }

    public static bool ShouldNotify(
        RebirthBossEventInstance e,
        RebirthBossEventOwnerUpdateKind kind)
    {
        if (e == null ||
            e.Options == null ||
            !e.Options.Notifications)
            return false;

        // The option promises event start and completion notifications. Reward
        // availability/removal is represented by its marker, not extra toasts.
        return kind == RebirthBossEventOwnerUpdateKind.Started ||
               kind == RebirthBossEventOwnerUpdateKind.BossDefeated;
    }

    public static void Send(
        World world,
        RebirthBossEventInstance e,
        RebirthBossEventOwnerUpdateKind kind)
    {
        if (world == null ||
            world.IsRemote() ||
            e == null ||
            e.Progression == null)
            return;

        EntityPlayer owner =
            RebirthBossEventIdentity.FindPlayer(
                world,
                e.Progression.OwnerStableId);

        if (owner == null)
            return;

        e.Progression.OwnerEntityId = owner.entityId;

        int[] supports =
            e.SupportEntityIds.ToArray();

        bool notify =
            kind != RebirthBossEventOwnerUpdateKind.ActiveSync &&
            ShouldNotify(e, kind);

        RebirthBossEventOwnerClientState clientState =
            new RebirthBossEventOwnerClientState
            {
                EventId = e.EventId,
                Kind = kind,
                State = e.State,
                BossEntityId = e.BossEntityId,
                SupportEntityIds = supports,
                BossPosition = e.LastBossPosition,
                RewardPosition = e.RewardBlockPosition,
                RewardPlaced = e.RewardBlockPlaced,
                RewardExpiresWorldTime =
                    e.RewardExpiresWorldTime,
                ShowNotification = notify
            };

        // SP and the local player on a player-host do not need a round-trip
        // through ConnectionManager. Apply the same state directly.
        if (owner is EntityPlayerLocal)
        {
            RebirthBossEventOwnerClientRegistry.Apply(
                clientState);
            return;
        }

        ConnectionManager connection =
            SingletonMonoBehaviour<ConnectionManager>.Instance;

        if (connection == null || !connection.IsServer)
            return;

        NetPackageRebirthBossEventOwnerUpdate package =
            NetPackageManager
                .GetPackage<NetPackageRebirthBossEventOwnerUpdate>()
                .Setup(
                    e.EventId,
                    kind,
                    e.State,
                    e.BossEntityId,
                    supports,
                    e.LastBossPosition,
                    e.RewardBlockPosition,
                    e.RewardBlockPlaced,
                    e.RewardExpiresWorldTime,
                    notify);

        connection.SendPackage(
            package,
            _attachedToEntityId: owner.entityId);
    }

    public static void Reset()
    {
        nextSync = 0f;
        RebirthBossEventOwnerClientRegistry.Reset();
        RebirthBossEventNativeMapMarkerService.Reset();
        RebirthBossEventRewardRegistry.Reset();
    }
}

[Preserve]
public sealed class NetPackageRebirthBossEventOwnerUpdate : NetPackage
{
    private Guid eventId;
    private RebirthBossEventOwnerUpdateKind kind;
    private RebirthBossEventState state;
    private int bossEntityId;
    private int[] supportEntityIds = new int[0];
    private Vector3 bossPosition;
    private Vector3i rewardPosition;
    private bool rewardPlaced;
    private ulong rewardExpiresWorldTime;
    private bool showNotification;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRebirthBossEventOwnerUpdate Setup(Guid id, RebirthBossEventOwnerUpdateKind updateKind, RebirthBossEventState eventState, int bossId, int[] supports, Vector3 bossPos, Vector3i rewardPos, bool hasReward, ulong rewardExpiry, bool notify)
    {
        eventId = id; kind = updateKind; state = eventState; bossEntityId = bossId; supportEntityIds = supports ?? new int[0]; bossPosition = bossPos; rewardPosition = rewardPos; rewardPlaced = hasReward; rewardExpiresWorldTime = rewardExpiry; showNotification = notify; return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader r = (BinaryReader)reader; eventId = new Guid(r.ReadBytes(16)); kind = (RebirthBossEventOwnerUpdateKind)r.ReadByte(); state = (RebirthBossEventState)r.ReadByte(); bossEntityId = r.ReadInt32();
        int count = r.ReadByte(); supportEntityIds = new int[count]; for (int i = 0; i < count; i++) supportEntityIds[i] = r.ReadInt32();
        bossPosition = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); rewardPosition = new Vector3i(r.ReadInt32(), r.ReadInt32(), r.ReadInt32()); rewardPlaced = r.ReadBoolean(); rewardExpiresWorldTime = r.ReadUInt64(); showNotification = r.ReadBoolean();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer); BinaryWriter w = (BinaryWriter)writer; w.Write(eventId.ToByteArray()); w.Write((byte)kind); w.Write((byte)state); w.Write(bossEntityId); int count = Math.Min(16, supportEntityIds != null ? supportEntityIds.Length : 0); w.Write((byte)count); for (int i = 0; i < count; i++) w.Write(supportEntityIds[i]);
        w.Write(bossPosition.x); w.Write(bossPosition.y); w.Write(bossPosition.z); w.Write(rewardPosition.x); w.Write(rewardPosition.y); w.Write(rewardPosition.z); w.Write(rewardPlaced); w.Write(rewardExpiresWorldTime); w.Write(showNotification);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthBossEventOwnerClientRegistry.Apply(new RebirthBossEventOwnerClientState { EventId = eventId, Kind = kind, State = state, BossEntityId = bossEntityId, SupportEntityIds = supportEntityIds, BossPosition = bossPosition, RewardPosition = rewardPosition, RewardPlaced = rewardPlaced, RewardExpiresWorldTime = rewardExpiresWorldTime, ShowNotification = showNotification });
    }

    public int GetLength() { return 128; }
}

public static class RebirthBossEventIdentity
{
    public static string GetStableId(EntityPlayer player)
    {
        if (player == null)
            return string.Empty;

        try
        {
            return player.PersistentPlayerData != null &&
                   player.PersistentPlayerData.PrimaryId != null
                ? player.PersistentPlayerData.PrimaryId.CombinedString
                : "entity:" + player.entityId;
        }
        catch
        {
            return "entity:" + player.entityId;
        }
    }

    public static EntityPlayer FindPlayer(
        World world,
        string stableId)
    {
        if (world == null ||
            string.IsNullOrEmpty(stableId))
            return null;

        List<EntityPlayer> players = world.GetPlayers();
        if (players == null)
            return null;

        for (int i = 0; i < players.Count; i++)
        {
            EntityPlayer player = players[i];

            if (player != null &&
                string.Equals(
                    GetStableId(player),
                    stableId,
                    StringComparison.Ordinal))
                return player;
        }

        return null;
    }

    public static string GetBiome(World world, Vector3 p)
    {
        try
        {
            BiomeDefinition b =
                world.GetBiome((int)p.x, (int)p.z);

            return b != null
                ? b.m_sBiomeName
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
public static class RebirthBossEventSeed { public static int Create(int worldSeed, int day, string owner, int sequence) { unchecked { int h = worldSeed; h = h * 397 ^ day; h = h * 397 ^ (owner ?? string.Empty).GetHashCode(); h = h * 397 ^ sequence; return h; } } }

public static class RebirthBossEventTestTools
{
    public static bool CleanupEvent(
        World world,
        RebirthBossEventInstance e,
        bool notifyOwner)
    {
        if (world == null || e == null)
            return false;

        if (notifyOwner &&
            e.Progression != null)
        {
            e.State = RebirthBossEventState.Cancelled;
            RebirthBossEventOwnerNetwork.Send(
                world,
                e,
                RebirthBossEventOwnerUpdateKind.Cancelled);
        }

        RemoveEntity(
            world,
            e.BossEntityId);

        for (int i = 0; i < e.SupportEntityIds.Count; i++)
            RemoveEntity(world, e.SupportEntityIds[i]);

        for (int i = 0; i < e.RegularEntityIds.Count; i++)
            RemoveEntity(world, e.RegularEntityIds[i]);

        if (e.RewardBlockPlaced)
        {
            if (e.RewardEntityId >= 0)
            {
                Entity rewardEntity =
                    world.GetEntity(e.RewardEntityId);

                if (rewardEntity != null)
                {
                    world.RemoveEntity(
                        rewardEntity.entityId,
                        EnumRemoveEntityReason.Despawned);
                }

                e.RewardEntityId = -1;
            }
            else
            {
                if (!world.GetBlock(
                        e.RewardBlockPosition).isair)
                {
                    world.SetBlockRPC(
                        (BlockValueRef)e.RewardBlockPosition,
                        BlockValue.Air);
                }

                RebirthBossEventRewardRegistry.Remove(
                    e.RewardBlockPosition);

                RebirthBossEventRewardAccessNetwork.BroadcastRemove(
                    world,
                    e.RewardBlockPosition);
            }

            e.RewardBlockPlaced = false;
        }

        e.State = RebirthBossEventState.Cancelled;
        RebirthBossEventRegistry.Remove(e.EventId);
        RebirthBossEventPersistence.MarkDirty();

        return true;
    }

    private static void RemoveEntity(
        World world,
        int entityId)
    {
        if (world == null || entityId < 0)
            return;

        RebirthBossEventRegistry.RemoveEntity(entityId);

        Entity entity = world.GetEntity(entityId);
        if (entity != null)
        {
            world.RemoveEntity(
                entityId,
                EnumRemoveEntityReason.Despawned);
        }
    }

    public static RebirthBossEventInstance FindOwnedActiveEvent(
        EntityPlayer owner)
    {
        if (owner == null)
            return null;

        string ownerId =
            RebirthBossEventIdentity.GetStableId(owner);

        List<RebirthBossEventInstance> events =
            RebirthBossEventRegistry.SnapshotEvents();

        RebirthBossEventInstance best = null;

        for (int i = 0; i < events.Count; i++)
        {
            RebirthBossEventInstance e = events[i];

            if (e == null ||
                e.Progression == null ||
                !string.Equals(
                    e.Progression.OwnerStableId,
                    ownerId,
                    StringComparison.Ordinal))
                continue;

            if (e.State != RebirthBossEventState.Active &&
                e.State != RebirthBossEventState.BossDefeated &&
                e.State != RebirthBossEventState.RewardPending)
                continue;

            if (best == null ||
                e.StartWorldTime > best.StartWorldTime)
                best = e;
        }

        return best;
    }
}

#if DEBUG
public static class RebirthBossEventFrequencyTestProbe
{
    private sealed class ProbeState
    {
        public string OwnerStableId;
        public ulong DueWorldTime;
        public int StartedBefore;
        public int OwnedEventsBefore;
        public int PendingBefore;
        public RebirthBossEventFrequency Frequency;
    }

    private static ProbeState probe;

    public static string Status(
        World world,
        EntityPlayer owner)
    {
        if (world == null)
            return "[REBIRTH BossEvents:FrequencyTest] world unavailable.";

        if (owner == null)
            return "[REBIRTH BossEvents:FrequencyTest] owner unavailable.";

        string stableId =
            RebirthBossEventIdentity.GetStableId(owner);

        Dictionary<string, RebirthBossEventDirector.OwnerSchedule> schedules =
            RebirthBossEventDirector.SnapshotSchedules();

        RebirthBossEventDirector.OwnerSchedule schedule;
        if (!schedules.TryGetValue(stableId, out schedule) ||
            schedule == null)
        {
            string gate =
                RebirthBossEventDirector.GetFrequencyTestGateReport(
                    world,
                    owner,
                    RebirthBossEventOptions.Current);
            return
                "[REBIRTH BossEvents:FrequencyTest] no natural schedule exists for owner=" +
                owner.EntityName +
                " directorGate={" + gate + "}. " +
                "Run rbevents frequencytest next; it will prepare the queue with the exact production scheduler if this gate is eligible.";
        }

        StringBuilder sb = new StringBuilder();
        RebirthBossEventOptionSnapshot options =
            RebirthBossEventOptions.Current;
        int playerLevel =
            owner.Progression != null
                ? owner.Progression.Level
                : 0;
        int eventDayChanceBasisPoints =
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                options.Frequency,
                playerLevel,
                options.MinimumPlayerLevel);
        int effectiveCountCeiling =
            Math.Min(
                RebirthBossEventScheduler.GetFrequencyCountCeiling(
                    options.Frequency),
                RebirthBossEventScheduler.GetLevelCountCeiling(
                    playerLevel,
                    options.MinimumPlayerLevel));
        effectiveCountCeiling =
            Math.Min(
                effectiveCountCeiling,
                options.MaximumPerDay);

        sb.Append("[REBIRTH BossEvents:FrequencyTest] owner=")
            .Append(owner.EntityName)
            .Append(" stableId=")
            .Append(stableId)
            .Append(" frequency=")
            .Append(options.Frequency)
            .Append(" level=")
            .Append(playerLevel)
            .Append(" eventDayChance=")
            .Append((eventDayChanceBasisPoints / 100.0).ToString(
                "0.00",
                CultureInfo.InvariantCulture))
            .Append("% countCeiling=")
            .Append(effectiveCountCeiling)
            .Append(" world=")
            .Append(FormatWorldTime(world.worldTime))
            .Append(" scheduleDay=")
            .Append(schedule.Day)
            .Append(" started=")
            .Append(schedule.Started)
            .Append(" pending=")
            .Append(schedule.Times.Count)
            .Append(" retries=")
            .Append(schedule.RetryCount)
            .Append(" cooldownPending=")
            .Append(schedule.CooldownPending)
            .Append(" partyPending=")
            .Append(schedule.PartyBlockedPending);

        ulong[] times = schedule.Times.ToArray();
        for (int i = 0; i < times.Length; i++)
        {
            ulong due = times[i];
            sb.Append("\n due[")
                .Append(i)
                .Append("]=")
                .Append(due)
                .Append(" (")
                .Append(FormatWorldTime(due))
                .Append(") settime=")
                .Append(BuildSetTimeCommand(due))
                .Append(" dueNow=")
                .Append(world.worldTime >= due);
        }

        if (times.Length == 0)
            sb.Append("\n no scheduled natural event remains for this owner/day.");

        return sb.ToString();
    }

    public static string JumpToNext(
        World world,
        EntityPlayer owner)
    {
        if (world == null || world.IsRemote())
            return "[REBIRTH BossEvents:FrequencyTest] authoritative world unavailable.";

        if (owner == null)
            return "[REBIRTH BossEvents:FrequencyTest] owner unavailable.";

        string stableId =
            RebirthBossEventIdentity.GetStableId(owner);

        Dictionary<string, RebirthBossEventDirector.OwnerSchedule> schedules =
            RebirthBossEventDirector.SnapshotSchedules();

        RebirthBossEventDirector.OwnerSchedule schedule;
        if (!schedules.TryGetValue(stableId, out schedule) ||
            schedule == null)
        {
            string prepareReport;
            if (!RebirthBossEventDirector.DebugPrepareScheduleForFrequencyTest(
                    world,
                    owner,
                    out prepareReport))
            {
                return
                    "[REBIRTH BossEvents:FrequencyTest] cannot prepare natural schedule. " +
                    "directorGate={" + prepareReport + "}.";
            }

            schedules =
                RebirthBossEventDirector.SnapshotSchedules();
            schedules.TryGetValue(stableId, out schedule);

            if (schedule == null)
            {
                return
                    "[REBIRTH BossEvents:FrequencyTest] production scheduler preparation returned success but no schedule exists. " +
                    "report={" + prepareReport + "}.";
            }
        }

        if (schedule.Times.Count == 0)
        {
            return
                "[REBIRTH BossEvents:FrequencyTest] this schedule has no pending natural event. " +
                "For a frequency that can roll zero, test another day.";
        }

        if (RebirthBossEventRegistry.HasActiveOwner(stableId))
        {
            return
                "[REBIRTH BossEvents:FrequencyTest] owner already has an active event. " +
                "Finish or cancel it before testing the next natural schedule entry.";
        }

        ulong due = schedule.Times.Peek();

        probe = new ProbeState
        {
            OwnerStableId = stableId,
            DueWorldTime = due,
            StartedBefore = schedule.Started,
            OwnedEventsBefore = CountOwnedEvents(stableId),
            PendingBefore = schedule.Times.Count,
            Frequency = RebirthBossEventOptions.Current.Frequency
        };

        if (due <= world.worldTime)
        {
            return
                "[REBIRTH BossEvents:FrequencyTest] ARMED already-due natural-spawn proof owner=" +
                owner.EntityName +
                " frequency=" + probe.Frequency +
                " due=" + due +
                " (" + FormatWorldTime(due) + ")" +
                " now=" + FormatWorldTime(world.worldTime) +
                " pendingBefore=" + probe.PendingBefore +
                " startedBefore=" + probe.StartedBefore +
                ". No time jump and no spawn/start API was called. " +
                "Wait about two seconds for the normal Director pump, then run: rbevents frequencytest check";
        }

        // This deliberately does NOT call TryStart, the spawner, or any event
        // creation API. It only performs the same world-time jump as the base
        // settime command. The normal RebirthBossEventDirector.Update pump must
        // observe the real scheduled entry becoming due and start the event.
        world.SetTimeJump(due);

        return
            "[REBIRTH BossEvents:FrequencyTest] ARMED natural-spawn proof owner=" +
            owner.EntityName +
            " frequency=" + probe.Frequency +
            " due=" + due +
            " (" + FormatWorldTime(due) + ")" +
            " pendingBefore=" + probe.PendingBefore +
            " startedBefore=" + probe.StartedBefore +
            ". World time was jumped only; no event spawn/start API was called. " +
            "Wait about two seconds. The normal Director must create the event, then run: rbevents frequencytest check";
    }

    public static string Check(
        World world,
        EntityPlayer owner)
    {
        if (probe == null)
            return "[REBIRTH BossEvents:FrequencyTest] no armed probe. Run rbevents frequencytest next first.";

        if (world == null || owner == null)
            return "[REBIRTH BossEvents:FrequencyTest] world/owner unavailable.";

        string stableId =
            RebirthBossEventIdentity.GetStableId(owner);

        if (!string.Equals(
                stableId,
                probe.OwnerStableId,
                StringComparison.Ordinal))
        {
            return
                "[REBIRTH BossEvents:FrequencyTest] armed probe belongs to a different owner.";
        }

        Dictionary<string, RebirthBossEventDirector.OwnerSchedule> schedules =
            RebirthBossEventDirector.SnapshotSchedules();

        RebirthBossEventDirector.OwnerSchedule schedule;
        schedules.TryGetValue(stableId, out schedule);

        int startedNow =
            schedule != null
                ? schedule.Started
                : -1;

        int pendingNow =
            schedule != null
                ? schedule.Times.Count
                : -1;

        int ownedEventsNow = CountOwnedEvents(stableId);
        RebirthBossEventInstance naturalEvent =
            FindOwnedEventStartedAtOrAfter(
                stableId,
                probe.DueWorldTime);

        bool startedIncremented =
            startedNow > probe.StartedBefore;

        bool eventCreated =
            naturalEvent != null ||
            ownedEventsNow > probe.OwnedEventsBefore;

        bool passed =
            startedIncremented && eventCreated;

        StringBuilder sb = new StringBuilder();
        sb.Append("[REBIRTH BossEvents:FrequencyTest] ")
            .Append(passed ? "PASS" : "NOT-YET/PENDING")
            .Append(" natural scheduler -> actual event spawn")
            .Append(" frequency=")
            .Append(probe.Frequency)
            .Append(" due=")
            .Append(FormatWorldTime(probe.DueWorldTime))
            .Append(" now=")
            .Append(FormatWorldTime(world.worldTime))
            .Append(" started=")
            .Append(probe.StartedBefore)
            .Append("->")
            .Append(startedNow)
            .Append(" pending=")
            .Append(probe.PendingBefore)
            .Append("->")
            .Append(pendingNow)
            .Append(" ownedEvents=")
            .Append(probe.OwnedEventsBefore)
            .Append("->")
            .Append(ownedEventsNow)
            .Append(" eventCreated=")
            .Append(eventCreated);

        if (naturalEvent != null)
        {
            sb.Append(" eventId=")
                .Append(naturalEvent.EventId)
                .Append(" eventStart=")
                .Append(FormatWorldTime(naturalEvent.StartWorldTime))
                .Append(" state=")
                .Append(naturalEvent.State)
                .Append(" bossEntity=")
                .Append(naturalEvent.BossEntityId);
        }

        if (!passed && schedule != null)
        {
            sb.Append(" cooldownPending=")
                .Append(schedule.CooldownPending)
                .Append(" partyPending=")
                .Append(schedule.PartyBlockedPending)
                .Append(" retries=")
                .Append(schedule.RetryCount);

            string reason;
            bool eligible =
                RebirthBossEventEligibility.CanStart(
                    world,
                    owner,
                    RebirthBossEventOptions.Current,
                    out reason);

            sb.Append(" eligibleNow=")
                .Append(eligible)
                .Append(" eligibilityReason=")
                .Append(reason ?? "none");

            if (schedule.Times.Count > 0)
            {
                sb.Append(" nextQueueTime=")
                    .Append(FormatWorldTime(schedule.Times.Peek()));
            }
        }

        if (passed)
            probe = null;

        return sb.ToString();
    }

    public static void Reset()
    {
        probe = null;
    }

    private static int CountOwnedEvents(string stableId)
    {
        int count = 0;
        List<RebirthBossEventInstance> events =
            RebirthBossEventRegistry.SnapshotEvents();

        for (int i = 0; i < events.Count; i++)
        {
            RebirthBossEventInstance e = events[i];
            if (e == null || e.Progression == null)
                continue;

            if (!string.Equals(
                    e.Progression.OwnerStableId,
                    stableId,
                    StringComparison.Ordinal))
                continue;

            if (e.State == RebirthBossEventState.Cancelled ||
                e.State == RebirthBossEventState.Failed)
                continue;

            count++;
        }

        return count;
    }

    private static RebirthBossEventInstance FindOwnedEventStartedAtOrAfter(
        string stableId,
        ulong dueWorldTime)
    {
        List<RebirthBossEventInstance> events =
            RebirthBossEventRegistry.SnapshotEvents();

        RebirthBossEventInstance best = null;
        for (int i = 0; i < events.Count; i++)
        {
            RebirthBossEventInstance e = events[i];
            if (e == null || e.Progression == null)
                continue;

            if (!string.Equals(
                    e.Progression.OwnerStableId,
                    stableId,
                    StringComparison.Ordinal))
                continue;

            if (e.StartWorldTime < dueWorldTime)
                continue;

            if (e.State == RebirthBossEventState.Cancelled ||
                e.State == RebirthBossEventState.Failed)
                continue;

            if (best == null || e.StartWorldTime < best.StartWorldTime)
                best = e;
        }

        return best;
    }

    private static string FormatWorldTime(ulong worldTime)
    {
        return
            "Day " + GameUtils.WorldTimeToDays(worldTime) +
            " " + GameUtils.WorldTimeToHours(worldTime).ToString("D2", CultureInfo.InvariantCulture) +
            ":" + GameUtils.WorldTimeToMinutes(worldTime).ToString("D2", CultureInfo.InvariantCulture);
    }

    private static string BuildSetTimeCommand(ulong worldTime)
    {
        return
            "settime " + GameUtils.WorldTimeToDays(worldTime) +
            " " + GameUtils.WorldTimeToHours(worldTime) +
            " " + GameUtils.WorldTimeToMinutes(worldTime);
    }
}

public static class RebirthBossEventTestSuite
{
    private sealed class Result
    {
        public int Passed;
        public int Failed;
        public int StructuralPassed;
        public int RuntimePassed;
        public int RuntimeNotRun;
        public readonly StringBuilder Text =
            new StringBuilder();
    }

    private enum EvidenceKind : byte
    {
        Structural = 0,
        Runtime = 1
    }

    public static string Run(
        World world,
        EntityPlayer owner)
    {
        Result result = new Result();

        result.Text.AppendLine(
            "[REBIRTH EventsTest] ===== EVENTS SUITE BEGIN =====");

        RebirthBossEventOptionSnapshot current =
            RebirthBossEventOptions.Current;

        result.Text.Append(
            "[REBIRTH EventsTest] Runtime options: enabled=")
            .Append(current.Enabled)
            .Append(" frequency=")
            .Append(current.Frequency)
            .Append(" maximum=")
            .Append(
                current.MaximumPerDay == int.MaxValue
                    ? "Unlimited"
                    : current.MaximumPerDay.ToString(
                        CultureInfo.InvariantCulture))
            .Append(" size=")
            .Append(current.Size)
            .Append(" difficulty=")
            .Append(current.Difficulty)
            .Append(" time=")
            .Append(current.Time)
            .Append(" bloodMoonDay=")
            .Append(current.BloodMoonDayEvents)
            .Append(" restriction=")
            .Append(current.Restriction)
            .Append(" rewards=")
            .Append(current.Rewards)
            .Append(" notifications=")
            .Append(current.Notifications)
            .AppendLine();

        Check(
            result,
            "01 master option snapshot valid",
            Enum.IsDefined(
                typeof(RebirthBossEventFrequency),
                current.Frequency) &&
            Enum.IsDefined(
                typeof(RebirthBossEventSize),
                current.Size) &&
            Enum.IsDefined(
                typeof(RebirthBossEventDifficulty),
                current.Difficulty) &&
            Enum.IsDefined(
                typeof(RebirthBossEventTime),
                current.Time) &&
            Enum.IsDefined(
                typeof(RebirthBossEventRestriction),
                current.Restriction));

        TestFrequency(result);
        TestMaximum(result);
        TestSize(result);
        TestDifficulty(result);
        TestTime(result);
        TestBloodMoon(result, world);
        TestRestrictions(result);
        TestRewards(result);
        TestNotifications(result);
        TestSchedulingSignature(result);
        TestAuthorityAndPersistence(result, world);
        TestSpawnComposition(result, world, owner);

        if (owner != null)
        {
            result.Text.Append(
                "[REBIRTH EventsTest] Owner context: name=")
                .Append(owner.EntityName)
                .Append(" level=")
                .Append(
                    owner.Progression != null
                        ? owner.Progression.Level
                        : 0)
                .Append(" gamestage=")
                .Append(owner.gameStage)
                .Append(" questing=")
                .Append(
                    RebirthBossEventEligibility.IsQuesting(owner))
                .Append(" outdoors=")
                .Append(
                    world != null &&
                    RebirthBossEventPlacement.IsOutdoors(
                        world,
                        owner.position))
                .AppendLine();
        }

        result.Text.Append(
            "[REBIRTH EventsTest] SUMMARY ")
            .Append(result.Passed)
            .Append("/")
            .Append(result.Passed + result.Failed)
            .Append(" passed; structuralPassed=")
            .Append(result.StructuralPassed)
            .Append(" runtimePassed=")
            .Append(result.RuntimePassed)
            .Append(" runtimeNotRun=")
            .Append(result.RuntimeNotRun);

        if (result.Failed > 0)
        {
            result.Text.Append(" -- FAILED=")
                .Append(result.Failed);
        }

        result.Text.AppendLine();
        result.Text.Append(
            "[REBIRTH EventsTest] ===== EVENTS SUITE END =====");

        return result.Text.ToString();
    }

    private static void TestFrequency(Result result)
    {
        RebirthBossEventFrequency[] values =
        {
            RebirthBossEventFrequency.Rare,
            RebirthBossEventFrequency.Low,
            RebirthBossEventFrequency.Normal,
            RebirthBossEventFrequency.High,
            RebirthBossEventFrequency.VeryHigh
        };

        int[] fullLevelMaximums = { 1, 2, 2, 3, 4 };
        double[] fullLevelMeans = new double[values.Length];
        bool boundsOk = true;
        bool quietDaysExist = true;

        for (int f = 0; f < values.Length; f++)
        {
            Random rng = new Random(8100 + f);
            long total = 0;
            int quietDays = 0;

            for (int i = 0; i < 20000; i++)
            {
                int count =
                    RebirthBossEventScheduler.RollDailyCount(
                        values[f],
                        75,
                        rng);

                if (count < 0 ||
                    count > fullLevelMaximums[f])
                    boundsOk = false;

                if (count == 0)
                    quietDays++;

                total += count;
            }

            quietDaysExist &= quietDays > 0;
            fullLevelMeans[f] = total / 20000.0;
        }

        Check(
            result,
            "02 frequency roll bounds + quiet days at full progression",
            boundsOk && quietDaysExist);

        bool frequencyMonotonic = true;
        for (int i = 1; i < fullLevelMeans.Length; i++)
        {
            if (fullLevelMeans[i] <= fullLevelMeans[i - 1])
                frequencyMonotonic = false;
        }

        Check(
            result,
            "03 configured frequency increases long-run intensity without guaranteeing a day",
            frequencyMonotonic,
            "means=" +
            string.Join(
                ",",
                Array.ConvertAll(
                    fullLevelMeans,
                    x => x.ToString(
                        "0.000",
                        CultureInfo.InvariantCulture))));

        int[] levels = { 15, 25, 50, 75 };
        int[] expectedVeryHighChanceBp = { 2800, 4400, 6000, 8000 };
        int[] expectedLevelCeilings = { 1, 2, 3, 4 };
        bool progressionModelOk = true;

        for (int i = 0; i < levels.Length; i++)
        {
            progressionModelOk &=
                RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                    RebirthBossEventFrequency.VeryHigh,
                    levels[i]) == expectedVeryHighChanceBp[i];
            progressionModelOk &=
                RebirthBossEventScheduler.GetLevelCountCeiling(
                    levels[i]) == expectedLevelCeilings[i];
        }

        progressionModelOk &=
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                RebirthBossEventFrequency.VeryHigh,
                100) == 8000;
        progressionModelOk &=
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                RebirthBossEventFrequency.Rare,
                100) == 2000;
        progressionModelOk &=
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                RebirthBossEventFrequency.VeryHigh,
                14) == 0;

        Check(
            result,
            "03a level progression ramps event-day chance and count ceiling",
            progressionModelOk);

        bool configurableStartLevelOk =
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                RebirthBossEventFrequency.VeryHigh, 9, 10) == 0 &&
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                RebirthBossEventFrequency.VeryHigh, 10, 10) > 0 &&
            RebirthBossEventScheduler.GetLevelCountCeiling(10, 10) == 1 &&
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                RebirthBossEventFrequency.VeryHigh, 19, 20) == 0 &&
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                RebirthBossEventFrequency.VeryHigh, 20, 20) > 0 &&
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                RebirthBossEventFrequency.VeryHigh, 29, 30) == 0 &&
            RebirthBossEventScheduler.GetEventDayChanceBasisPoints(
                RebirthBossEventFrequency.VeryHigh, 30, 30) > 0;

        Check(
            result,
            "03c configurable minimum player level gates natural events",
            configurableStartLevelOk);

        bool levelIntensityMonotonic = true;
        double previousMean = -1.0;
        for (int l = 0; l < levels.Length; l++)
        {
            Random rng = new Random(8190 + l);
            long total = 0;
            for (int i = 0; i < 20000; i++)
            {
                total +=
                    RebirthBossEventScheduler.RollDailyCount(
                        RebirthBossEventFrequency.VeryHigh,
                        levels[l],
                        rng);
            }

            double mean = total / 20000.0;
            if (mean <= previousMean)
                levelIntensityMonotonic = false;
            previousMean = mean;
        }

        Check(
            result,
            "03b VeryHigh intensity increases with player progression",
            levelIntensityMonotonic);
    }

    private static void TestMaximum(Result result)
    {
        bool ok =
            RebirthBossEventScheduler.GetPendingDailyCount(4, 1, 0) == 1 &&
            RebirthBossEventScheduler.GetPendingDailyCount(4, 2, 0) == 2 &&
            RebirthBossEventScheduler.GetPendingDailyCount(4, 3, 0) == 3 &&
            RebirthBossEventScheduler.GetPendingDailyCount(4, 4, 0) == 4 &&
            RebirthBossEventScheduler.GetPendingDailyCount(4, 5, 0) == 4 &&
            RebirthBossEventScheduler.GetPendingDailyCount(4, int.MaxValue, 0) == 4 &&
            RebirthBossEventScheduler.GetPendingDailyCount(4, 5, 2) == 2 &&
            RebirthBossEventScheduler.GetPendingDailyCount(2, 5, 2) == 0;

        Check(
            result,
            "04 maximum-per-day cap + mid-day rebuild preserves started count",
            ok);
    }

    private static void TestSize(Result result)
    {
        bool small = true;
        bool normal = true;
        bool large = true;

        Random a = new Random(8201);
        Random b = new Random(8202);
        Random c = new Random(8203);

        for (int i = 0; i < 1000; i++)
        {
            int s =
                RebirthBossEventCompositionResolver
                    .RollRegularCount(
                        RebirthBossEventSize.Small,
                        a);

            int n =
                RebirthBossEventCompositionResolver
                    .RollRegularCount(
                        RebirthBossEventSize.Normal,
                        b);

            int l =
                RebirthBossEventCompositionResolver
                    .RollRegularCount(
                        RebirthBossEventSize.Large,
                        c);

            small &= s >= 4 && s <= 6;
            normal &= n >= 7 && n <= 10;
            large &= l >= 11 && l <= 15;
        }

        Check(
            result,
            "05 size Small regular-count range 4-6",
            small);

        Check(
            result,
            "06 size Normal regular-count range 7-10",
            normal);

        Check(
            result,
            "07 size Large regular-count range 11-15",
            large);
    }

    private static void TestDifficulty(Result result)
    {
        int low =
            RebirthBossEventProgression.GetEffectiveGameStage(
                100,
                RebirthBossEventDifficulty.Low);

        int normal =
            RebirthBossEventProgression.GetEffectiveGameStage(
                100,
                RebirthBossEventDifficulty.Normal);

        int high =
            RebirthBossEventProgression.GetEffectiveGameStage(
                100,
                RebirthBossEventDifficulty.High);

        Check(
            result,
            "08 difficulty Low/Normal/High gamestage scaling",
            low == 85 &&
            normal == 100 &&
            high == 120,
            "GS100=" + low + "/" + normal + "/" + high);
    }

    private static void TestTime(Result result)
    {
        RebirthBossEventTime[] values =
        {
            RebirthBossEventTime.DayOnly,
            RebirthBossEventTime.DayAndNight,
            RebirthBossEventTime.NightOnly
        };

        bool ok = true;
        bool sameRequestedDay = true;

        for (int mode = 0; mode < values.Length; mode++)
        {
            Random rng = new Random(8300 + mode);

            List<ulong> times =
                RebirthBossEventScheduler.CreateWorldTimes(
                    5,
                    512,
                    values[mode],
                    rng);

            for (int i = 0; i < times.Count; i++)
            {
                int tick =
                    (int)(times[i] % 24000UL);

                if (GameUtils.WorldTimeToDays(times[i]) != 5)
                    sameRequestedDay = false;

                if (!RebirthBossEventScheduler
                        .IsWorldTickAllowed(
                            tick,
                            values[mode]))
                {
                    ok = false;
                    break;
                }
            }
        }

        Check(
            result,
            "09 Day/Night scheduler windows",
            ok);

        Check(
            result,
            "09b scheduled world times remain on requested display day",
            sameRequestedDay);
    }

    private static void TestBloodMoon(
        Result result,
        World world)
    {
        bool pure =
            RebirthBossEventEligibility
                .IsBloodMoonDayNumber(14, 14) &&
            !RebirthBossEventEligibility
                .IsBloodMoonDayNumber(13, 14) &&
            !RebirthBossEventEligibility
                .IsBloodMoonDayNumber(14, 0);

        Check(
            result,
            "10 Blood Moon option uses whole scheduled Blood Moon day",
            pure);

        if (world != null)
        {
            result.Text.Append(
                "[REBIRTH EventsTest] Blood Moon context: currentDay=")
                .Append(
                    GameUtils.WorldTimeToDays(
                        world.worldTime))
                .Append(" scheduledBloodMoonDay=")
                .Append(
                    GameStats.GetInt(
                        EnumGameStats.BloodMoonDay))
                .Append(" isBloodMoonDay=")
                .Append(
                    RebirthBossEventEligibility
                        .IsBloodMoonDay(world))
                .AppendLine();
        }
    }

    private static void TestRestrictions(Result result)
    {
        bool ok = true;
        string reason;

        for (int quest = 0; quest <= 1; quest++)
        {
            for (int outdoors = 0; outdoors <= 1; outdoors++)
            {
                bool q = quest == 1;
                bool o = outdoors == 1;

                ok &=
                    RebirthBossEventEligibility
                        .IsRestrictionAllowed(
                            RebirthBossEventRestriction.Anywhere,
                            q,
                            o,
                            out reason);

                ok &=
                    RebirthBossEventEligibility
                        .IsRestrictionAllowed(
                            RebirthBossEventRestriction.OutdoorsOnly,
                            q,
                            o,
                            out reason) == o;

                ok &=
                    RebirthBossEventEligibility
                        .IsRestrictionAllowed(
                            RebirthBossEventRestriction.OnlyWhileQuesting,
                            q,
                            o,
                            out reason) == q;

                ok &=
                    RebirthBossEventEligibility
                        .IsRestrictionAllowed(
                            RebirthBossEventRestriction.NotWhileQuesting,
                            q,
                            o,
                            out reason) == !q;

                ok &=
                    RebirthBossEventEligibility
                        .IsRestrictionAllowed(
                            RebirthBossEventRestriction.OutdoorsAndNotQuesting,
                            q,
                            o,
                            out reason) == (o && !q);
            }
        }

        Check(
            result,
            "11 restriction truth table",
            ok);
    }

    private static void TestRewards(Result result)
    {
        bool blocks = true;

        for (int tier = 1; tier <= 5; tier++)
        {
            Block block =
                Block.GetBlockByName(
                    "rebirthBossRewardCrateT" +
                    tier.ToString(
                        CultureInfo.InvariantCulture),
                    true);

            blocks &= block != null;
        }

        Check(
            result,
            "12 reward crate definitions T1-T5",
            blocks);
    }

    private static void TestNotifications(Result result)
    {
        RebirthBossEventInstance e =
            new RebirthBossEventInstance
            {
                Options =
                    new RebirthBossEventOptionSnapshot
                    {
                        Notifications = true
                    }
            };

        bool enabled =
            RebirthBossEventOwnerNetwork.ShouldNotify(
                e,
                RebirthBossEventOwnerUpdateKind.Started) &&
            RebirthBossEventOwnerNetwork.ShouldNotify(
                e,
                RebirthBossEventOwnerUpdateKind.BossDefeated) &&
            !RebirthBossEventOwnerNetwork.ShouldNotify(
                e,
                RebirthBossEventOwnerUpdateKind.RewardAvailable) &&
            !RebirthBossEventOwnerNetwork.ShouldNotify(
                e,
                RebirthBossEventOwnerUpdateKind.RewardRemoved) &&
            !RebirthBossEventOwnerNetwork.ShouldNotify(
                e,
                RebirthBossEventOwnerUpdateKind.ActiveSync);

        e.Options.Notifications = false;

        bool disabled =
            !RebirthBossEventOwnerNetwork.ShouldNotify(
                e,
                RebirthBossEventOwnerUpdateKind.Started) &&
            !RebirthBossEventOwnerNetwork.ShouldNotify(
                e,
                RebirthBossEventOwnerUpdateKind.BossDefeated);

        Check(
            result,
            "13 notifications = start/completion only; Off suppresses both",
            enabled && disabled);
    }

    private static void TestSchedulingSignature(Result result)
    {
        RebirthBossEventOptionSnapshot a =
            new RebirthBossEventOptionSnapshot();

        int baseSignature =
            RebirthBossEventScheduler
                .GetSchedulingSignature(a);

        RebirthBossEventOptionSnapshot b = a.Clone();
        b.Frequency = RebirthBossEventFrequency.High;

        RebirthBossEventOptionSnapshot c = a.Clone();
        c.MaximumPerDay = 5;

        RebirthBossEventOptionSnapshot d = a.Clone();
        d.Time = RebirthBossEventTime.NightOnly;

        RebirthBossEventOptionSnapshot e = a.Clone();
        e.MinimumPlayerLevel = 25;

        bool changed =
            baseSignature !=
                RebirthBossEventScheduler
                    .GetSchedulingSignature(b) &&
            baseSignature !=
                RebirthBossEventScheduler
                    .GetSchedulingSignature(c) &&
            baseSignature !=
                RebirthBossEventScheduler
                    .GetSchedulingSignature(d) &&
            baseSignature !=
                RebirthBossEventScheduler
                    .GetSchedulingSignature(e);

        Check(
            result,
            "14 schedule rebuild signature reacts to Frequency/Maximum/Time/StartLevel",
            changed);
    }

    private static void TestAuthorityAndPersistence(
        Result result,
        World world)
    {
        Check(
            result,
            "15 command suite executing on authoritative world",
            world != null && !world.IsRemote(),
            world == null ? "world unavailable" : null,
            EvidenceKind.Runtime);

        Check(
            result,
            "16 persistence format supports schedule configuration signature",
            RebirthBossEventPersistence.FormatVersion >= 3);
    }

    private static void TestSpawnComposition(
        Result result,
        World world,
        EntityPlayer owner)
    {
        string readiness;
        bool ready =
            RebirthSpawnCompositionService.IsRuntimeReady(
                out readiness);

        int gameStage =
            owner != null
                ? Math.Max(1, owner.gameStage)
                : 100;

        string biome =
            world != null && owner != null
                ? RebirthBossEventIdentity.GetBiome(
                    world,
                    owner.position)
                : "forest";

        RebirthSpawnTrace trace = null;
        bool selected = false;

        if (ready)
        {
            Random rng = new Random(8999);

            RebirthSpawnContext context =
                new RebirthSpawnContext
                {
                    Surface =
                        RebirthSpawnSurface.Biome,
                    ProgressionMode =
                        RebirthSpawnProgressionMode.Gamestage,
                    GameStage = gameStage,
                    Biome = biome,
                    HistoryKey = string.Empty
                };

            selected =
                RebirthSpawnCompositionService.TrySelect(
                    context,
                    rng.NextDouble,
                    out trace) &&
                trace != null &&
                EntityClass.GetEntityClass(
                    trace.EntityClassId) != null;
        }

        string detail =
            readiness +
            " probe=" +
            (trace != null
                ? (trace.EntityName ?? "<unnamed>") +
                  " id=" +
                  trace.EntityClassId
                : "<none>");

        Check(
            result,
            "17 runtime spawn composition loaded + selectable",
            ready && selected,
            detail,
            EvidenceKind.Runtime);
    }

    private static void Check(
        Result result,
        string name,
        bool passed,
        string detail = null,
        EvidenceKind evidence = EvidenceKind.Structural)
    {
        if (evidence == EvidenceKind.Runtime && detail == "world unavailable")
        {
            result.RuntimeNotRun++;
            result.Text.Append("[REBIRTH EventsTest] NOT_RUN[RUNTIME] - ").Append(name);
            if (!string.IsNullOrEmpty(detail)) result.Text.Append(" :: ").Append(detail);
            result.Text.AppendLine();
            return;
        }

        if (passed)
        {
            result.Passed++;
            if (evidence == EvidenceKind.Runtime) result.RuntimePassed++;
            else result.StructuralPassed++;
        }
        else
            result.Failed++;

        result.Text.Append("[REBIRTH EventsTest] ")
            .Append(passed ? "PASS[" : "FAIL[")
            .Append(evidence == EvidenceKind.Runtime ? "RUNTIME" : "STRUCTURAL")
            .Append("] - ")
            .Append(name);

        if (!string.IsNullOrEmpty(detail)) result.Text.Append(" :: ").Append(detail);
        result.Text.AppendLine();
    }
}
#endif


#if DEBUG
public sealed class RebirthBossEventHealthTraceSnapshot
{
    public int EntityId;
    public int Health;
    public int MaxHealth;
    public float StatValue;
    public float BaseMax;
    public float ModifiedMax;
    public float OriginalValue;
    public float OriginalMax;
    public float RegenerationAmount;
    public float RawValue;
    public float LastValue;
    public float RawBaseMax;
    public float RawOriginalBaseMax;
    public float MaxModifier;
    public bool RoleBuffPresent;
    public bool NeedsHealthRestore;
    public bool HasSavedSnapshot;
    public int SavedHealth;
    public int SavedMaxHealth;
    public float SavedFraction;

    public bool SameCore(RebirthBossEventHealthTraceSnapshot other)
    {
        if (other == null) return false;
        return EntityId == other.EntityId &&
               Health == other.Health &&
               MaxHealth == other.MaxHealth &&
               Mathf.Abs(StatValue - other.StatValue) < 0.001f &&
               Mathf.Abs(BaseMax - other.BaseMax) < 0.001f &&
               Mathf.Abs(ModifiedMax - other.ModifiedMax) < 0.001f &&
               Mathf.Abs(OriginalValue - other.OriginalValue) < 0.001f &&
               Mathf.Abs(OriginalMax - other.OriginalMax) < 0.001f &&
               Mathf.Abs(RegenerationAmount - other.RegenerationAmount) < 0.001f &&
               Mathf.Abs(RawValue - other.RawValue) < 0.001f &&
               Mathf.Abs(LastValue - other.LastValue) < 0.001f &&
               Mathf.Abs(RawBaseMax - other.RawBaseMax) < 0.001f &&
               Mathf.Abs(RawOriginalBaseMax - other.RawOriginalBaseMax) < 0.001f &&
               Mathf.Abs(MaxModifier - other.MaxModifier) < 0.001f &&
               RoleBuffPresent == other.RoleBuffPresent &&
               NeedsHealthRestore == other.NeedsHealthRestore &&
               HasSavedSnapshot == other.HasSavedSnapshot &&
               SavedHealth == other.SavedHealth &&
               SavedMaxHealth == other.SavedMaxHealth &&
               Mathf.Abs(SavedFraction - other.SavedFraction) < 0.0001f;
    }

    public string Compact()
    {
        return
            "hp=" + Health + "/" + MaxHealth +
            " statValue=" + StatValue.ToString("0.###", CultureInfo.InvariantCulture) +
            " raw=" + RawValue.ToString("0.###", CultureInfo.InvariantCulture) +
            " last=" + LastValue.ToString("0.###", CultureInfo.InvariantCulture) +
            " baseMax=" + BaseMax.ToString("0.###", CultureInfo.InvariantCulture) +
            " rawBaseMax=" + RawBaseMax.ToString("0.###", CultureInfo.InvariantCulture) +
            " modMax=" + ModifiedMax.ToString("0.###", CultureInfo.InvariantCulture) +
            " origValue=" + OriginalValue.ToString("0.###", CultureInfo.InvariantCulture) +
            " origMax=" + OriginalMax.ToString("0.###", CultureInfo.InvariantCulture) +
            " rawOrigMax=" + RawOriginalBaseMax.ToString("0.###", CultureInfo.InvariantCulture) +
            " maxMod=" + MaxModifier.ToString("0.###", CultureInfo.InvariantCulture) +
            " regen=" + RegenerationAmount.ToString("0.###", CultureInfo.InvariantCulture) +
            " roleBuff=" + RoleBuffPresent +
            " needsRestore=" + NeedsHealthRestore +
            " saved=" +
            (HasSavedSnapshot
                ? SavedHealth.ToString(CultureInfo.InvariantCulture) + "/" +
                  SavedMaxHealth.ToString(CultureInfo.InvariantCulture) +
                  "@" + SavedFraction.ToString("0.0000", CultureInfo.InvariantCulture)
                : "none");
    }
}

public static class RebirthBossEventHealthTrace
{
    private static readonly FieldInfo StatRawValueField =
        AccessTools.Field(typeof(Stat), "m_value");
    private static readonly FieldInfo StatLastValueField =
        AccessTools.Field(typeof(Stat), "m_lastValue");
    private static readonly FieldInfo StatRawBaseMaxField =
        AccessTools.Field(typeof(Stat), "m_baseMax");
    private static readonly FieldInfo StatRawOriginalBaseMaxField =
        AccessTools.Field(typeof(Stat), "m_originalBaseMax");
    private static readonly FieldInfo StatMaxModifierField =
        AccessTools.Field(typeof(Stat), "m_maxModifier");

    private static readonly Dictionary<int, RebirthBossEventHealthTraceSnapshot>
        LastObserved =
            new Dictionary<int, RebirthBossEventHealthTraceSnapshot>();

    private static readonly Dictionary<int, int> FullHealStackCounts =
        new Dictionary<int, int>();

    private static float verboseUntilRealtime;
    private static bool enabled;

    public static bool Enabled
    {
        get { return enabled; }
    }

    public static bool Active
    {
        get
        {
            return enabled &&
                   Time.realtimeSinceStartup <= verboseUntilRealtime;
        }
    }

    public static void Enable(string reason, float seconds)
    {
        enabled = true;
        LastObserved.Clear();
        FullHealStackCounts.Clear();
        verboseUntilRealtime =
            Time.realtimeSinceStartup + Mathf.Max(1f, seconds);

        Log.Out(
            "[REBIRTH BossEvents:HealthTrace] " + RebirthBossEventHealthTraceInstaller.Status + " ENABLED reason=" +
            (reason ?? "unknown") +
            " until=" +
            verboseUntilRealtime.ToString(
                "0.000", CultureInfo.InvariantCulture) +
            " frame=" + Time.frameCount);
    }

    public static void Disable()
    {
        enabled = false;
        verboseUntilRealtime = 0f;
        LastObserved.Clear();
        FullHealStackCounts.Clear();
    }

    public static void BeginWorldLoad(string reason)
    {
        if (!enabled)
            return;

        LastObserved.Clear();
        FullHealStackCounts.Clear();
        verboseUntilRealtime =
            Mathf.Max(verboseUntilRealtime, Time.realtimeSinceStartup + 20f);

        Log.Out(
            "[REBIRTH BossEvents:HealthTrace] BEGIN reason=" +
            (reason ?? "unknown") +
            " realtime=" +
            Time.realtimeSinceStartup.ToString(
                "0.000", CultureInfo.InvariantCulture) +
            " frame=" + Time.frameCount);
    }

    public static void ExtendWindow(string reason, float seconds)
    {
        if (!enabled)
            return;

        verboseUntilRealtime =
            Mathf.Max(
                verboseUntilRealtime,
                Time.realtimeSinceStartup + Mathf.Max(1f, seconds));

        if (Active)
        {
            Log.Out(
                "[REBIRTH BossEvents:HealthTrace] WINDOW reason=" +
                (reason ?? "unknown") +
                " until=" +
                verboseUntilRealtime.ToString(
                    "0.000", CultureInfo.InvariantCulture) +
                " frame=" + Time.frameCount);
        }
    }

    private static float ReadFloat(FieldInfo field, object instance)
    {
        if (field == null || instance == null)
            return float.NaN;

        try
        {
            object value = field.GetValue(instance);
            return value is float ? (float)value : float.NaN;
        }
        catch
        {
            return float.NaN;
        }
    }

    public static string DescribeStat(Stat stat)
    {
        if (stat == null)
            return "stat=null";

        return
            "value=" + stat.Value.ToString("0.###", CultureInfo.InvariantCulture) +
            " raw=" + ReadFloat(StatRawValueField, stat).ToString("0.###", CultureInfo.InvariantCulture) +
            " last=" + ReadFloat(StatLastValueField, stat).ToString("0.###", CultureInfo.InvariantCulture) +
            " baseMax=" + stat.BaseMax.ToString("0.###", CultureInfo.InvariantCulture) +
            " rawBaseMax=" + ReadFloat(StatRawBaseMaxField, stat).ToString("0.###", CultureInfo.InvariantCulture) +
            " modifiedMax=" + stat.ModifiedMax.ToString("0.###", CultureInfo.InvariantCulture) +
            " originalValue=" + stat.OriginalValue.ToString("0.###", CultureInfo.InvariantCulture) +
            " originalMax=" + stat.OriginalMax.ToString("0.###", CultureInfo.InvariantCulture) +
            " rawOriginalMax=" + ReadFloat(StatRawOriginalBaseMaxField, stat).ToString("0.###", CultureInfo.InvariantCulture) +
            " maxModifier=" + ReadFloat(StatMaxModifierField, stat).ToString("0.###", CultureInfo.InvariantCulture) +
            " regen=" + stat.RegenerationAmount.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static bool TryGetTracked(
        EntityAlive entity,
        out RebirthBossEventEntityState state)
    {
        state = null;
        return entity != null &&
               RebirthBossEventRegistry.TryGetEntity(
                   entity.entityId,
                   out state) &&
               state != null;
    }

    public static bool TryGetTracked(
        Stat stat,
        out EntityAlive entity,
        out RebirthBossEventEntityState state)
    {
        entity = null;
        state = null;

        if (stat == null ||
            stat.StatType != Stat.StatTypes.Health)
            return false;

        entity = stat.Entity;
        return TryGetTracked(entity, out state);
    }

    public static RebirthBossEventHealthTraceSnapshot Take(
        EntityAlive entity,
        RebirthBossEventEntityState state)
    {
        if (entity == null || state == null || entity.Stats == null ||
            entity.Stats.Health == null)
            return null;

        Stat stat = entity.Stats.Health;
        string roleBuff =
            RebirthBossEventPresentation.GetRoleBuff(state.Role);

        RebirthBossEventHealthTraceSnapshot snapshot =
            new RebirthBossEventHealthTraceSnapshot();

        snapshot.EntityId = entity.entityId;
        snapshot.Health = entity.Health;
        snapshot.MaxHealth = entity.GetMaxHealth();
        snapshot.StatValue = stat.Value;
        snapshot.BaseMax = stat.BaseMax;
        snapshot.ModifiedMax = stat.ModifiedMax;
        snapshot.OriginalValue = stat.OriginalValue;
        snapshot.OriginalMax = stat.OriginalMax;
        snapshot.RegenerationAmount = stat.RegenerationAmount;
        snapshot.RawValue = ReadFloat(StatRawValueField, stat);
        snapshot.LastValue = ReadFloat(StatLastValueField, stat);
        snapshot.RawBaseMax = ReadFloat(StatRawBaseMaxField, stat);
        snapshot.RawOriginalBaseMax =
            ReadFloat(StatRawOriginalBaseMaxField, stat);
        snapshot.MaxModifier = ReadFloat(StatMaxModifierField, stat);
        snapshot.RoleBuffPresent =
            entity.Buffs != null &&
            entity.Buffs.HasBuff(roleBuff);
        snapshot.NeedsHealthRestore = state.NeedsHealthRestore;
        snapshot.HasSavedSnapshot = state.HasHealthSnapshot;
        snapshot.SavedHealth = state.LastKnownHealth;
        snapshot.SavedMaxHealth = state.LastKnownMaxHealth;
        snapshot.SavedFraction = state.LastKnownHealthFraction;
        return snapshot;
    }

    public static void TraceEntity(
        EntityAlive entity,
        RebirthBossEventEntityState state,
        string stage,
        string extra)
    {
        if (!Active || entity == null || state == null)
            return;

        RebirthBossEventHealthTraceSnapshot snapshot =
            Take(entity, state);

        if (snapshot == null)
            return;

        Log.Out(
            "[REBIRTH BossEvents:HealthTrace] " +
            (stage ?? "TRACE") +
            " frame=" + Time.frameCount +
            " realtime=" +
            Time.realtimeSinceStartup.ToString(
                "0.000", CultureInfo.InvariantCulture) +
            " entity=" + entity.entityId +
            " class=" +
            EntityClass.list[entity.entityClass].entityClassName +
            " role=" + state.Role +
            " " + snapshot.Compact() +
            (string.IsNullOrEmpty(extra)
                ? string.Empty
                : " " + extra));

        LastObserved[entity.entityId] = snapshot;
    }

    public static void ObserveWorld(World world)
    {
        if (!Active || world == null || world.IsRemote())
            return;

        List<KeyValuePair<int, RebirthBossEventEntityState>> states =
            RebirthBossEventRegistry.SnapshotEntities();

        for (int i = 0; i < states.Count; i++)
        {
            RebirthBossEventEntityState state = states[i].Value;
            if (state == null)
                continue;

            EntityAlive entity =
                world.GetEntity(states[i].Key) as EntityAlive;

            if (entity == null || entity.IsDead())
                continue;

            RebirthBossEventHealthTraceSnapshot current =
                Take(entity, state);

            if (current == null)
                continue;

            RebirthBossEventHealthTraceSnapshot previous;
            if (!LastObserved.TryGetValue(
                    entity.entityId,
                    out previous))
            {
                Log.Out(
                    "[REBIRTH BossEvents:HealthTrace] OBS-BASELINE" +
                    " frame=" + Time.frameCount +
                    " realtime=" +
                    Time.realtimeSinceStartup.ToString(
                        "0.000", CultureInfo.InvariantCulture) +
                    " entity=" + entity.entityId +
                    " role=" + state.Role +
                    " " + current.Compact());

                LastObserved[entity.entityId] = current;
                continue;
            }

            if (!current.SameCore(previous))
            {
                Log.Out(
                    "[REBIRTH BossEvents:HealthTrace] OBS-CHANGE" +
                    " frame=" + Time.frameCount +
                    " realtime=" +
                    Time.realtimeSinceStartup.ToString(
                        "0.000", CultureInfo.InvariantCulture) +
                    " entity=" + entity.entityId +
                    " role=" + state.Role +
                    " before={" + previous.Compact() + "}" +
                    " after={" + current.Compact() + "}");

                LastObserved[entity.entityId] = current;
            }
        }
    }

    public static void TraceAllLoaded(
        World world,
        string stage)
    {
        if (!Active || world == null || world.IsRemote())
            return;

        List<KeyValuePair<int, RebirthBossEventEntityState>> states =
            RebirthBossEventRegistry.SnapshotEntities();

        for (int i = 0; i < states.Count; i++)
        {
            RebirthBossEventEntityState state = states[i].Value;
            if (state == null)
                continue;

            EntityAlive entity =
                world.GetEntity(states[i].Key) as EntityAlive;

            if (entity == null || entity.IsDead())
                continue;

            TraceEntity(
                entity,
                state,
                stage,
                string.Empty);
        }
    }

    public static string Dump(World world)
    {
        StringBuilder sb =
            new StringBuilder(
                "[REBIRTH BossEvents:HealthTrace] dump active=")
                .Append(Active)
                .Append(" frame=")
                .Append(Time.frameCount)
                .Append(" realtime=")
                .Append(
                    Time.realtimeSinceStartup.ToString(
                        "0.000", CultureInfo.InvariantCulture));

        List<KeyValuePair<int, RebirthBossEventEntityState>> states =
            RebirthBossEventRegistry.SnapshotEntities();

        for (int i = 0; i < states.Count; i++)
        {
            RebirthBossEventEntityState state = states[i].Value;
            if (state == null)
                continue;

            EntityAlive entity =
                world != null
                    ? world.GetEntity(states[i].Key) as EntityAlive
                    : null;

            sb.Append("\n  entity=")
              .Append(states[i].Key)
              .Append(" role=")
              .Append(state.Role)
              .Append(" loaded=")
              .Append(entity != null);

            if (entity != null)
            {
                RebirthBossEventHealthTraceSnapshot snapshot =
                    Take(entity, state);

                if (snapshot != null)
                    sb.Append(" ").Append(snapshot.Compact());
            }
            else
            {
                sb.Append(" saved=")
                  .Append(
                      state.HasHealthSnapshot
                          ? state.LastKnownHealth.ToString(
                                CultureInfo.InvariantCulture) +
                            "/" +
                            state.LastKnownMaxHealth.ToString(
                                CultureInfo.InvariantCulture) +
                            "@" +
                            state.LastKnownHealthFraction.ToString(
                                "0.0000",
                                CultureInfo.InvariantCulture)
                          : "none")
                  .Append(" needsRestore=")
                  .Append(state.NeedsHealthRestore);
            }
        }

        return sb.ToString();
    }

    public static void LogSetter(
        Stat stat,
        float requestedValue,
        string stage)
    {
        EntityAlive entity;
        RebirthBossEventEntityState state;
        if (!Active ||
            !TryGetTracked(stat, out entity, out state))
            return;

        float before = stat.Value;
        float max = stat.ModifiedMax;

        Log.Out(
            "[REBIRTH BossEvents:HealthTrace] " +
            (stage ?? "STAT-VALUE-SET") +
            " frame=" + Time.frameCount +
            " realtime=" +
            Time.realtimeSinceStartup.ToString(
                "0.000", CultureInfo.InvariantCulture) +
            " entity=" + entity.entityId +
            " role=" + state.Role +
            " before=" +
            before.ToString("0.###", CultureInfo.InvariantCulture) +
            " requested=" +
            requestedValue.ToString("0.###", CultureInfo.InvariantCulture) +
            " modifiedMax=" +
            max.ToString("0.###", CultureInfo.InvariantCulture) +
            " " + DescribeStat(stat));

        if (requestedValue > before + 0.5f &&
            requestedValue >= max - 0.5f &&
            state.Role != RebirthBossEventRole.Regular)
        {
            int count;
            FullHealStackCounts.TryGetValue(entity.entityId, out count);
            if (count < 3)
            {
                FullHealStackCounts[entity.entityId] = count + 1;
                Log.Out(
                    "[REBIRTH BossEvents:HealthTrace] HEAL-TO-FULL-SETTER-STACK" +
                    " entity=" + entity.entityId +
                    " role=" + state.Role +
                    "\n" + Environment.StackTrace);
            }
        }
    }

    public static void LogStatTickTransition(
        Stat stat,
        RebirthBossEventHealthTraceSnapshot before,
        float dt)
    {
        EntityAlive entity;
        RebirthBossEventEntityState state;
        if (!Active ||
            before == null ||
            !TryGetTracked(stat, out entity, out state))
            return;

        RebirthBossEventHealthTraceSnapshot after =
            Take(entity, state);

        if (after == null || after.SameCore(before))
            return;

        Log.Out(
            "[REBIRTH BossEvents:HealthTrace] STAT-TICK-CHANGE" +
            " frame=" + Time.frameCount +
            " realtime=" +
            Time.realtimeSinceStartup.ToString(
                "0.000", CultureInfo.InvariantCulture) +
            " dt=" + dt.ToString("0.###", CultureInfo.InvariantCulture) +
            " entity=" + entity.entityId +
            " role=" + state.Role +
            " before={" + before.Compact() + "}" +
            " after={" + after.Compact() + "}");

        if (after.Health > before.Health &&
            after.Health >= after.MaxHealth)
        {
            Log.Out(
                "[REBIRTH BossEvents:HealthTrace] STAT-TICK-HEALED-TO-FULL" +
                " entity=" + entity.entityId +
                " role=" + state.Role +
                " beforeHp=" + before.Health +
                " afterHp=" + after.Health +
                " beforeLast=" +
                before.LastValue.ToString(
                    "0.###", CultureInfo.InvariantCulture) +
                " afterLast=" +
                after.LastValue.ToString(
                    "0.###", CultureInfo.InvariantCulture) +
                " beforeRegen=" +
                before.RegenerationAmount.ToString(
                    "0.###", CultureInfo.InvariantCulture) +
                " afterRegen=" +
                after.RegenerationAmount.ToString(
                    "0.###", CultureInfo.InvariantCulture));
        }

        LastObserved[entity.entityId] = after;
    }
}

public static class RebirthBossEventHealthTraceInstaller
{
    private static bool installed;
    public static bool Installed { get { return installed; } }
    public static string Status
    {
        get { return "hooksInstalled=" + Installed + " enabled=" + RebirthBossEventHealthTrace.Enabled + " active=" + RebirthBossEventHealthTrace.Active; }
    }

    public static void Install()
    {
        if (installed)
            return;

        Harmony harmony =
            new Harmony("rebirth.boss-events.health-trace.3.1");

        RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthBossEventEntityCreationApplyTracePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthBossEventSetStatsTracePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthBossEventStatCopyTracePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthBossEventStatValueSetterTracePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthBossEventStatTickTracePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthBossEventSetAliveTracePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthBossEventOnAddedToWorldTracePatch));

        installed = true; // All registrations above completed; failed attempts remain retryable.

        if (RebirthBossEventHealthTrace.Active)
        {
            Log.Out(
                "[REBIRTH BossEvents:HealthTrace] diagnostic patches installed");
        }
    }
}

[HarmonyPatch(typeof(EntityCreationData), nameof(EntityCreationData.ApplyToEntity))]
internal static class RebirthBossEventEntityCreationApplyTracePatch
{
    private static void Prefix(
        EntityCreationData __instance,
        Entity _e)
    {
        EntityAlive entity = _e as EntityAlive;
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                entity,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "ECD-APPLY-PREFIX",
            "sourceStats={" +
            (__instance != null && __instance.stats != null
                ? RebirthBossEventHealthTrace.DescribeStat(
                    __instance.stats.Health)
                : "null") +
            "}");
    }

    private static void Postfix(
        EntityCreationData __instance,
        Entity _e)
    {
        EntityAlive entity = _e as EntityAlive;
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                entity,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "ECD-APPLY-POSTFIX",
            "sourceStats={" +
            (__instance != null && __instance.stats != null
                ? RebirthBossEventHealthTrace.DescribeStat(
                    __instance.stats.Health)
                : "null") +
            "}");
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetStats))]
internal static class RebirthBossEventSetStatsTracePatch
{
    private static void Prefix(
        EntityAlive __instance,
        EntityStats _stats)
    {
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                __instance,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            __instance,
            state,
            "SET-STATS-PREFIX",
            "sourceStats={" +
            (_stats != null
                ? RebirthBossEventHealthTrace.DescribeStat(
                    _stats.Health)
                : "null") +
            "}");
    }

    private static void Postfix(
        EntityAlive __instance,
        EntityStats _stats)
    {
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                __instance,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            __instance,
            state,
            "SET-STATS-POSTFIX",
            "sourceStats={" +
            (_stats != null
                ? RebirthBossEventHealthTrace.DescribeStat(
                    _stats.Health)
                : "null") +
            "}");
    }
}

[HarmonyPatch(typeof(Stat), nameof(Stat.CopyFrom))]
internal static class RebirthBossEventStatCopyTracePatch
{
    private static void Prefix(
        Stat __instance,
        Stat _stat)
    {
        EntityAlive entity;
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                __instance,
                out entity,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "STAT-COPYFROM-PREFIX",
            "source={" +
            RebirthBossEventHealthTrace.DescribeStat(_stat) +
            "}");
    }

    private static void Postfix(
        Stat __instance,
        Stat _stat)
    {
        EntityAlive entity;
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                __instance,
                out entity,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "STAT-COPYFROM-POSTFIX",
            "source={" +
            RebirthBossEventHealthTrace.DescribeStat(_stat) +
            "}");
    }
}

[HarmonyPatch(typeof(Stat), "set_Value")]
internal static class RebirthBossEventStatValueSetterTracePatch
{
    private static void Prefix(
        Stat __instance,
        float value)
    {
        RebirthBossEventHealthTrace.LogSetter(
            __instance,
            value,
            "STAT-VALUE-SET-PREFIX");
    }

    private static void Postfix(
        Stat __instance,
        float value)
    {
        EntityAlive entity;
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                __instance,
                out entity,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            entity,
            state,
            "STAT-VALUE-SET-POSTFIX",
            "requested=" +
            value.ToString(
                "0.###", CultureInfo.InvariantCulture));
    }
}

[HarmonyPatch(typeof(Stat), nameof(Stat.Tick))]
internal static class RebirthBossEventStatTickTracePatch
{
    private static void Prefix(
        Stat __instance,
        float dt,
        ref RebirthBossEventHealthTraceSnapshot __state)
    {
        EntityAlive entity;
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                __instance,
                out entity,
                out state))
            return;

        __state =
            RebirthBossEventHealthTrace.Take(
                entity,
                state);
    }

    private static void Postfix(
        Stat __instance,
        float dt,
        RebirthBossEventHealthTraceSnapshot __state)
    {
        RebirthBossEventHealthTrace.LogStatTickTransition(
            __instance,
            __state,
            dt);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetAlive))]
internal static class RebirthBossEventSetAliveTracePatch
{
    private static void Prefix(EntityAlive __instance)
    {
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                __instance,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            __instance,
            state,
            "SET-ALIVE-PREFIX",
            string.Empty);
    }

    private static void Postfix(EntityAlive __instance)
    {
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                __instance,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            __instance,
            state,
            "SET-ALIVE-POSTFIX",
            string.Empty);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnAddedToWorld))]
internal static class RebirthBossEventOnAddedToWorldTracePatch
{
    private static void Prefix(EntityAlive __instance)
    {
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                __instance,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            __instance,
            state,
            "ON-ADDED-TO-WORLD-PREFIX",
            string.Empty);
    }

    private static void Postfix(EntityAlive __instance)
    {
        RebirthBossEventEntityState state;
        if (!RebirthBossEventHealthTrace.Active ||
            !RebirthBossEventHealthTrace.TryGetTracked(
                __instance,
                out state))
            return;

        RebirthBossEventHealthTrace.TraceEntity(
            __instance,
            state,
            "ON-ADDED-TO-WORLD-POSTFIX",
            string.Empty);
    }
}

#endif

public static class RebirthBossEventDiagnostics
{
#if DEBUG
    public static bool Enabled;

    [System.Diagnostics.Conditional("DEBUG")]
    public static void Write(string text)
    {
        if (Enabled)
            Log.Out("[REBIRTH BossEvents] " + text);
    }
#else
    public const bool Enabled = false;

    [System.Diagnostics.Conditional("DEBUG")]
    public static void Write(string text) { }
#endif

    public static string Status()
    {
        StringBuilder sb =
            new StringBuilder(
                "[REBIRTH BossEvents] debug=")
                .Append(Enabled);

        RebirthBossEventOptionSnapshot options =
            RebirthBossEventOptions.Current;

        sb.Append("\nspawnComposition=")
            .Append(
                RebirthSpawnCompositionService
                    .GetStatus());

        sb.Append("\noptions enabled=")
            .Append(options.Enabled)
            .Append(" frequency=")
            .Append(options.Frequency)
            .Append(" maximum=")
            .Append(
                options.MaximumPerDay == int.MaxValue
                    ? "Unlimited"
                    : options.MaximumPerDay.ToString(
                        CultureInfo.InvariantCulture))
            .Append(" size=")
            .Append(options.Size)
            .Append(" difficulty=")
            .Append(options.Difficulty)
            .Append(" time=")
            .Append(options.Time)
            .Append(" bloodMoonDay=")
            .Append(options.BloodMoonDayEvents)
            .Append(" restriction=")
            .Append(options.Restriction)
            .Append(" rewards=")
            .Append(options.Rewards)
            .Append(" notifications=")
            .Append(options.Notifications);

        World world =
            GameManager.Instance != null
                ? GameManager.Instance.World
                : null;

        if (world != null)
        {
            sb.Append("\nworld authoritative=")
                .Append(!world.IsRemote())
                .Append(" day=")
                .Append(
                    GameUtils.WorldTimeToDays(
                        world.worldTime))
                .Append(" hour=")
                .Append(
                    GameUtils.WorldTimeToHours(
                        world.worldTime))
                .Append(" bloodMoonDay=")
                .Append(
                    GameStats.GetInt(
                        EnumGameStats.BloodMoonDay))
                .Append(" isBloodMoonDay=")
                .Append(
                    RebirthBossEventEligibility
                        .IsBloodMoonDay(world));
        }

        Dictionary<string, RebirthBossEventDirector.OwnerSchedule>
            schedules =
                RebirthBossEventDirector.SnapshotSchedules();

        sb.Append("\nschedules=")
            .Append(schedules.Count);

        foreach (KeyValuePair<
                     string,
                     RebirthBossEventDirector.OwnerSchedule>
                 pair in schedules)
        {
            RebirthBossEventDirector.OwnerSchedule schedule =
                pair.Value;

            sb.Append("\n schedule owner=")
                .Append(pair.Key)
                .Append(" day=")
                .Append(schedule.Day)
                .Append(" started=")
                .Append(schedule.Started)
                .Append(" pending=")
                .Append(schedule.Times.Count)
                .Append(" retries=")
                .Append(schedule.RetryCount)
                .Append(" signature=")
                .Append(schedule.ConfigurationSignature)
                .Append(" lastNaturalStart=")
                .Append(schedule.LastNaturalStartWorldTime)
                .Append(" cooldownPending=")
                .Append(schedule.CooldownPending)
                .Append(" partyPending=")
                .Append(schedule.PartyBlockedPending)
                .Append(" pendingSince=")
                .Append(schedule.PendingSinceWorldTime)
                .Append(" pendingPartyId=")
                .Append(schedule.PendingPartyId);
        }

        List<RebirthBossEventInstance> events =
            RebirthBossEventRegistry.SnapshotEvents();

        sb.Append("\nevents=")
            .Append(events.Count);

        for (int i = 0; i < events.Count; i++)
        {
            RebirthBossEventInstance e = events[i];

            if (e == null)
                continue;

            sb.Append("\n ")
                .Append(e.EventId)
                .Append(" state=")
                .Append(e.State)
                .Append(" owner=")
                .Append(
                    e.Progression != null
                        ? e.Progression.OwnerDisplayName
                        : "?")
                .Append(" boss=")
                .Append(e.BossEntityId)
                .Append(" supports=")
                .Append(e.SupportEntityIds.Count)
                .Append(" regulars=")
                .Append(e.RegularEntityIds.Count)
                .Append(" reward=")
                .Append(e.RewardIssued)
                .Append(" rewardPlaced=")
                .Append(e.RewardBlockPlaced);

            if (!string.IsNullOrEmpty(e.FailureReason))
            {
                sb.Append(" reason=")
                    .Append(e.FailureReason);
            }
        }

        return sb.ToString();
    }
}


public static class RebirthBossEventWorldHooks
{
    private sealed class PendingLoadedEntityProbe
    {
        public int EntityId;
        public int Generation;
        public int Attempts;
        public ulong DueWorldTime;
    }

    private sealed class PendingLoadedEntityToken
    {
        public int EntityId;
        public int Generation;
    }

    private static World hookedWorld;
    private static readonly Dictionary<int, PendingLoadedEntityProbe> PendingLoadedEntities =
        new Dictionary<int, PendingLoadedEntityProbe>();
    private static int nextPendingLoadedGeneration;
    private static readonly SortedDictionary<ulong, Queue<PendingLoadedEntityToken>> PendingLoadedDue =
        new SortedDictionary<ulong, Queue<PendingLoadedEntityToken>>();
    private const int MaxPendingLoadedProbesPerUpdate = 16;
    private const int MaxPendingLoadedAttempts = 8;

    public static void Ensure(World world)
    {
        if (world == null || world.IsRemote())
            return;

        if (object.ReferenceEquals(hookedWorld, world))
            return;

        Reset();
        hookedWorld = world;
        hookedWorld.EntityLoadedDelegates += OnEntityLoaded;
        hookedWorld.EntityUnloadedDelegates += OnEntityUnloaded;

        RebirthBossEventDiagnostics.Write(
            "world entity rehydrate hooks installed");
    }

    private static void OnEntityLoaded(Entity entity)
    {
        if (entity == null || hookedWorld == null)
            return;

        RebirthBossEventEntityState state;
        if (!RebirthBossEventRegistry.TryGetEntity(
                entity.entityId,
                out state) ||
            state == null)
            return;

        // World invokes EntityLoadedDelegates before the entity is added to
        // World.Entities and before OnAddedToWorld. Mark the persisted health
        // snapshot as requiring one authoritative restore, then queue that work
        // for the next GameUpdate after normal 3.1 entity-add initialization.
#if DEBUG
        RebirthBossEventHealthTrace.ExtendWindow(
            "tracked-entity-loaded",
            15f);
#endif

        EntityAlive loadedAlive = entity as EntityAlive;
#if DEBUG
        if (loadedAlive != null)
        {
            RebirthBossEventHealthTrace.TraceEntity(
                loadedAlive,
                state,
                "ENTITY-LOADED-DELEGATE-BEFORE-MARK",
                string.Empty);
        }
#endif

        state.NeedsHealthRestore = true;

#if DEBUG
        if (loadedAlive != null)
        {
            RebirthBossEventHealthTrace.TraceEntity(
                loadedAlive,
                state,
                "ENTITY-LOADED-DELEGATE-AFTER-MARK",
                string.Empty);
        }
#endif

        SchedulePendingLoadedEntity(entity.entityId, hookedWorld.worldTime, true);
    }

    private static void SchedulePendingLoadedEntity(
        int entityId,
        ulong dueWorldTime,
        bool newLoadGeneration)
    {
        if (entityId < 0)
            return;

        PendingLoadedEntityProbe probe;
        if (newLoadGeneration ||
            !PendingLoadedEntities.TryGetValue(entityId, out probe) ||
            probe == null)
        {
            nextPendingLoadedGeneration++;
            if (nextPendingLoadedGeneration <= 0)
                nextPendingLoadedGeneration = 1;
            probe = new PendingLoadedEntityProbe
            {
                EntityId = entityId,
                Generation = nextPendingLoadedGeneration,
                Attempts = 0,
                DueWorldTime = dueWorldTime
            };
            PendingLoadedEntities[entityId] = probe;
        }
        else
        {
            probe.DueWorldTime = dueWorldTime;
        }
        int generation = probe.Generation;

        Queue<PendingLoadedEntityToken> bucket;
        if (!PendingLoadedDue.TryGetValue(dueWorldTime, out bucket))
        {
            bucket = new Queue<PendingLoadedEntityToken>();
            PendingLoadedDue.Add(dueWorldTime, bucket);
        }
        bucket.Enqueue(new PendingLoadedEntityToken
        {
            EntityId = entityId,
            Generation = generation
        });
    }

    private static bool TryTakeDuePending(
        ulong now,
        out PendingLoadedEntityToken token)
    {
        token = null;
        ulong selectedDue = 0UL;
        Queue<PendingLoadedEntityToken> selected = null;
        foreach (KeyValuePair<ulong, Queue<PendingLoadedEntityToken>> pair in PendingLoadedDue)
        {
            if (pair.Key > now)
                break;
            selectedDue = pair.Key;
            selected = pair.Value;
            break;
        }

        if (selected == null || selected.Count == 0)
            return false;

        token = selected.Dequeue();
        if (selected.Count == 0)
            PendingLoadedDue.Remove(selectedDue);
        return token != null;
    }

    private static void CancelPendingLoadedEntity(int entityId)
    {
        PendingLoadedEntities.Remove(entityId);
    }

    public static void ProcessPending(World world)
    {
        if (world == null ||
            hookedWorld == null ||
            !object.ReferenceEquals(world, hookedWorld) ||
            PendingLoadedEntities.Count == 0)
            return;

        int processed = 0;
        while (processed < MaxPendingLoadedProbesPerUpdate)
        {
            PendingLoadedEntityToken token;
            if (!TryTakeDuePending(world.worldTime, out token))
                break;
            processed++;

            PendingLoadedEntityProbe probe;
            if (!PendingLoadedEntities.TryGetValue(token.EntityId, out probe) ||
                probe == null ||
                probe.Generation != token.Generation)
                continue;

            EntityAlive alive =
                world.GetEntity(token.EntityId) as EntityAlive;

            if (alive == null)
            {
                RebirthBossEventEntityState state;
                if (!RebirthBossEventRegistry.TryGetEntity(
                        token.EntityId,
                        out state) ||
                    state == null)
                {
                    CancelPendingLoadedEntity(token.EntityId);
                    continue;
                }

                probe.Attempts++;
                if (probe.Attempts >= MaxPendingLoadedAttempts)
                {
                    // Preserve NeedsHealthRestore on the persisted state. A later
                    // genuine EntityLoaded callback creates a fresh probe generation.
                    PendingLoadedEntities.Remove(token.EntityId);
                    RebirthBossEventDiagnostics.Write(
                        "event entity rehydrate probe expired entity=" +
                        token.EntityId + " attempts=" + probe.Attempts);
                    continue;
                }

                ulong delay = 30UL << Math.Min(6, probe.Attempts - 1);
                SchedulePendingLoadedEntity(
                    token.EntityId,
                    world.worldTime + delay,
                    false);
                continue;
            }

            RebirthBossEventEntityState pendingState;
            if (RebirthBossEventRegistry.TryGetEntity(
                    alive.entityId,
                    out pendingState) &&
                pendingState != null)
            {
#if DEBUG
                RebirthBossEventHealthTrace.TraceEntity(
                    alive,
                    pendingState,
                    "PROCESS-PENDING-BEFORE-REHYDRATE",
                    string.Empty);
#endif
            }

            PendingLoadedEntities.Remove(token.EntityId);
            RebirthBossEventTargeting.RehydrateLoadedEntity(
                world,
                alive,
                "entity-loaded");

#if DEBUG
            if (pendingState != null)
            {
                RebirthBossEventHealthTrace.TraceEntity(
                    alive,
                    pendingState,
                    "PROCESS-PENDING-AFTER-REHYDRATE",
                    string.Empty);
            }
#endif
        }
    }

    private static void OnEntityUnloaded(
        Entity entity,
        EnumRemoveEntityReason reason)
    {
        if (entity == null || hookedWorld == null)
            return;

        RebirthBossEventEntityState state;
        if (!RebirthBossEventRegistry.TryGetEntity(
                entity.entityId,
                out state) ||
            state == null)
            return;

        if (reason == EnumRemoveEntityReason.Unloaded)
        {
            CancelPendingLoadedEntity(entity.entityId);
            EntityAlive alive = entity as EntityAlive;
            if (alive != null && !alive.IsDead())
            {
#if DEBUG
                RebirthBossEventHealthTrace.TraceEntity(
                    alive,
                    state,
                    "ENTITY-UNLOADED-BEFORE-CAPTURE",
                    "removeReason=" + reason);
#endif

                RebirthBossEventHealthPersistence.Capture(
                    alive,
                    state,
                    "entity-unloaded",
                    true);

#if DEBUG
                RebirthBossEventHealthTrace.TraceEntity(
                    alive,
                    state,
                    "ENTITY-UNLOADED-AFTER-CAPTURE",
                    "removeReason=" + reason);
#endif
            }

            state.NeedsHealthRestore = true;

            RebirthBossEventDiagnostics.Write(
                "event entity unloaded retained entity="
                + entity.entityId
                + " role=" + state.Role
                + " event=" + state.EventId
                + " health="
                + (state.HasHealthSnapshot
                    ? state.LastKnownHealth.ToString(
                        CultureInfo.InvariantCulture)
                      + "/"
                      + state.LastKnownMaxHealth.ToString(
                        CultureInfo.InvariantCulture)
                    : "none"));
            return;
        }

        RebirthBossEventLifecycle.NotifyTrackedEntityRemoved(
            hookedWorld,
            entity,
            state,
            reason);
    }

    public static void Reset()
    {
        if (hookedWorld != null)
        {
            hookedWorld.EntityLoadedDelegates -= OnEntityLoaded;
            hookedWorld.EntityUnloadedDelegates -= OnEntityUnloaded;
        }

        hookedWorld = null;
        PendingLoadedEntities.Clear();
        PendingLoadedDue.Clear();
        nextPendingLoadedGeneration = 0;
    }
}


public static class RebirthBossEventLegacyHealthRefillGuard
{
    private const float LegacyFillAmount = 30000f;

    public static bool IsLegacyHealthFillAction(
        MinEventActionModifyStats action)
    {
        return action != null
            && string.Equals(
                action.statName,
                "health",
                StringComparison.OrdinalIgnoreCase)
            && action.operation ==
                MinEventActionModifyStats.OperationTypes.add
            && !action.cvarRef
            && Math.Abs(action.value - LegacyFillAmount) <= 0.001f
            && string.IsNullOrEmpty(action.valueType)
            && action.targetType ==
                MinEventActionTargetedBase.TargetTypes.self;
    }

#if DEBUG
    public static string GetBuffName(MinEventParams eventParams)
    {
        BuffValue activeBuff =
            eventParams != null ? eventParams.Buff : null;
        BuffClass buffClass =
            activeBuff != null ? activeBuff.BuffClass : null;
        return buffClass != null ? buffClass.Name : "null";
    }

#endif

    public static bool TryGetTrackedTarget(
        MinEventActionModifyStats action,
        MinEventParams eventParams,
        out EntityAlive target,
        out RebirthBossEventEntityState state,
        out string source)
    {
        target = null;
        state = null;
        source = "none";

        // First use the resolved action target list. CanExecute() populates this
        // immediately before Execute(), and it remains the most authoritative
        // statement of which entity MinEventActionModifyStats will mutate.
        if (action != null && action.targets != null)
        {
            for (int i = 0; i < action.targets.Count; i++)
            {
                EntityAlive candidate = action.targets[i];
                if (candidate == null)
                    continue;

                RebirthBossEventEntityState candidateState;
                if (RebirthBossEventRegistry.TryGetEntity(
                        candidate.entityId,
                        out candidateState) &&
                    candidateState != null)
                {
                    target = candidate;
                    state = candidateState;
                    source = "action.targets[" + i + "]";
                    return true;
                }
            }
        }

        // Fallback for unusual execution order / delayed actions where the
        // target list is unavailable but MinEventParams.Self is still valid.
        EntityAlive self = eventParams != null ? eventParams.Self : null;
        if (self != null)
        {
            RebirthBossEventEntityState selfState;
            if (RebirthBossEventRegistry.TryGetEntity(
                    self.entityId,
                    out selfState) &&
                selfState != null)
            {
                target = self;
                state = selfState;
                source = "params.Self";
                return true;
            }
        }

        return false;
    }

#if DEBUG
    public static void LogCandidate(
        string stage,
        MinEventActionModifyStats action,
        MinEventParams eventParams)
    {
        if (!RebirthBossEventHealthTrace.Active ||
            !IsLegacyHealthFillAction(action))
            return;

        EntityAlive target;
        RebirthBossEventEntityState state;
        string targetSource;
        bool tracked = TryGetTrackedTarget(
            action,
            eventParams,
            out target,
            out state,
            out targetSource);

        string selfId =
            eventParams != null && eventParams.Self != null
                ? eventParams.Self.entityId.ToString(
                    CultureInfo.InvariantCulture)
                : "null";

        StringBuilder targetsText = new StringBuilder();
        if (action.targets != null)
        {
            for (int i = 0; i < action.targets.Count; i++)
            {
                if (i > 0) targetsText.Append(',');
                EntityAlive item = action.targets[i];
                targetsText.Append(
                    item != null
                        ? item.entityId.ToString(CultureInfo.InvariantCulture)
                        : "null");
            }
        }

        Log.Out(
            "[REBIRTH BossEvents:HealthTrace] LEGACY-ACTION-" +
            (stage ?? "UNKNOWN") +
            " frame=" + Time.frameCount +
            " realtime=" +
            Time.realtimeSinceStartup.ToString(
                "0.000", CultureInfo.InvariantCulture) +
            " buff=" + GetBuffName(eventParams) +
            " self=" + selfId +
            " targets=" + targetsText +
            " tracked=" + tracked +
            " targetSource=" + targetSource +
            " target=" +
            (target != null
                ? target.entityId.ToString(CultureInfo.InvariantCulture)
                : "null") +
            " role=" + (state != null ? state.Role.ToString() : "null") +
            " value=" + action.value.ToString(
                "0.###", CultureInfo.InvariantCulture) +
            " valueType=" + (action.valueType ?? "null") +
            " cvarRef=" + action.cvarRef +
            " targetType=" + action.targetType +
            (target != null
                ? " health=" + target.Health + "/" + target.GetMaxHealth()
                : string.Empty));
    }

#endif

    public static bool ShouldBlock(
        MinEventActionModifyStats action,
        MinEventParams eventParams,
        out EntityAlive target,
        out RebirthBossEventEntityState state,
        out string targetSource)
    {
        target = null;
        state = null;
        targetSource = "none";

        if (!IsLegacyHealthFillAction(action))
            return false;

        // Do not depend on _params.Buff. The v112 trace proves the actual
        // mutation is exactly +30000 Health. The resolved action target is the
        // authoritative object that execute() is about to modify.
        return TryGetTrackedTarget(
            action,
            eventParams,
            out target,
            out state,
            out targetSource);
    }
}

// Outer Execute is diagnostic only. This proves whether the public action entry
// point is reached and what buff/target context exists before the inner method.
#if DEBUG
[HarmonyPatch(typeof(MinEventActionModifyStats), nameof(MinEventActionModifyStats.Execute))]
internal static class RebirthBossEventLegacyHealthRefillOuterTracePatch
{
    private static void Prefix(
        MinEventActionModifyStats __instance,
        MinEventParams _params)
    {
        RebirthBossEventLegacyHealthRefillGuard.LogCandidate(
            "OUTER-EXECUTE",
            __instance,
            _params);
    }
}
#endif

// The actual guard is on the exact inner method seen in the v112 stack trace.
[HarmonyPatch(typeof(MinEventActionModifyStats), "execute")]
internal static class RebirthBossEventLegacyHealthRefillPatch
{
    private static bool Prefix(
        MinEventActionModifyStats __instance,
        MinEventParams _params)
    {
#if DEBUG
        RebirthBossEventLegacyHealthRefillGuard.LogCandidate(
            "INNER-EXECUTE",
            __instance,
            _params);
#endif

        EntityAlive target;
        RebirthBossEventEntityState state;
        string targetSource;
        if (!RebirthBossEventLegacyHealthRefillGuard.ShouldBlock(
                __instance,
                _params,
                out target,
                out state,
                out targetSource))
        {
#if DEBUG
            if (RebirthBossEventHealthTrace.Active &&
                RebirthBossEventLegacyHealthRefillGuard
                    .IsLegacyHealthFillAction(__instance))
            {
                Log.Out(
                    "[REBIRTH BossEvents:HealthTrace] " +
                    "LEGACY-ACTION-ALLOWED reason=no-tracked-event-target" +
                    " buff=" +
                    RebirthBossEventLegacyHealthRefillGuard.GetBuffName(_params) +
                    " self=" +
                    (_params != null && _params.Self != null
                        ? _params.Self.entityId.ToString(
                            CultureInfo.InvariantCulture)
                        : "null"));
            }
#endif
            return true;
        }

#if DEBUG
        if (RebirthBossEventHealthTrace.Active)
        {
            Log.Out(
            "[REBIRTH BossEvents:HealthTrace] LEGACY-ACTION-BLOCKED" +
            " entity=" + target.entityId +
            " role=" + state.Role +
            " buff=" +
            RebirthBossEventLegacyHealthRefillGuard.GetBuffName(_params) +
            " source=" + targetSource +
            " requestedAdd=" + __instance.value.ToString(
                "0", CultureInfo.InvariantCulture) +
            " health=" + target.Health + "/" + target.GetMaxHealth() +
            " needsRestore=" + state.NeedsHealthRestore +
            " initialFillPending=" + state.NeedsInitialHealthFill);
        }
#endif

        return false;
    }
}

public static class RebirthBossEventLegacyHealthRefillInstaller
{
    private const string HarmonyId =
        "rebirth.boss-events.legacy-health-refill-guard.3.1";
    private static bool installed;

#if DEBUG
    private static string OwnersFor(MethodBase method)
    {
        if (method == null)
            return "target-null";

        Patches patches = Harmony.GetPatchInfo(method);
        if (patches == null)
            return "none";

        HashSet<string> owners = new HashSet<string>();
        foreach (Patch patch in patches.Prefixes)
            owners.Add(patch.owner);
        foreach (Patch patch in patches.Postfixes)
            owners.Add(patch.owner);
        foreach (Patch patch in patches.Transpilers)
            owners.Add(patch.owner);
        foreach (Patch patch in patches.Finalizers)
            owners.Add(patch.owner);

        return owners.Count > 0
            ? string.Join(",", new List<string>(owners).ToArray())
            : "none";
    }

#endif

    public static void Install()
    {
        if (installed)
            return;

        installed = true;
        Harmony harmony = new Harmony(HarmonyId);

        bool innerInstalled = RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthBossEventLegacyHealthRefillPatch));

#if DEBUG
        bool outerInstalled = RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthBossEventLegacyHealthRefillOuterTracePatch));

        MethodBase outer = AccessTools.Method(
            typeof(MinEventActionModifyStats),
            nameof(MinEventActionModifyStats.Execute),
            new Type[] { typeof(MinEventParams) });
        MethodBase inner = AccessTools.Method(
            typeof(MinEventActionModifyStats),
            "execute",
            new Type[] { typeof(MinEventParams) });

        if (RebirthBossEventHealthTrace.Active)
        {
            Log.Out(
                "[REBIRTH BossEvents:HealthTrace] LEGACY-GUARD-INSTALL" +
                " outerInstalled=" + outerInstalled +
                " innerInstalled=" + innerInstalled +
                " outerTarget=" + (outer != null) +
                " innerTarget=" + (inner != null) +
                " outerOwners=" + OwnersFor(outer) +
                " innerOwners=" + OwnersFor(inner));
        }
#endif
    }
}

public sealed class RebirthBossEventModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        RebirthDamageAdapterInstaller.Install();
        RebirthBossEventRewardAccessInstaller.Install();
#if DEBUG
        RebirthBossEventHealthTraceInstaller.Install();
#endif
        RebirthBossEventLegacyHealthRefillInstaller.Install();
        ModEvents.EntityKilled.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SEntityKilledData>(
                RebirthBossEventDeathLootService.OnEntityKilled));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnStart));
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(
                OnPlayerSpawnedInWorld));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShutdown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnShutdown));
    }
    private static void OnStart(ref ModEvents.SGameStartingData data)
    {
        if (!data.AsServer)
            return;

#if DEBUG
        RebirthBossEventHealthTrace.BeginWorldLoad(
            "game-starting-before-bossevents-load");
#endif
        RebirthBossEventPersistence.Load();
#if DEBUG
        RebirthBossEventHealthTrace.ExtendWindow(
            "game-starting-after-bossevents-load",
            15f);
#endif
        World world =
            GameManager.Instance != null
                ? GameManager.Instance.World
                : null;
        RebirthBossEventWorldHooks.Ensure(world);
    }

    private static void OnPlayerSpawnedInWorld(
        ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        ConnectionManager connection =
            SingletonMonoBehaviour<ConnectionManager>.Instance;

        if (connection != null && !connection.IsServer)
            return;

        World world =
            GameManager.Instance != null
                ? GameManager.Instance.World
                : null;

        if (world == null)
            return;

        RebirthBossEventWorldHooks.Ensure(world);
#if DEBUG
        RebirthBossEventHealthTrace.ExtendWindow(
            "player-spawned-in-world",
            15f);
        RebirthBossEventHealthTrace.TraceAllLoaded(
            world,
            "PLAYER-SPAWN-BEFORE-OWNER-REHYDRATE");
#endif

        EntityPlayer owner =
            world.GetEntity(data.EntityId) as EntityPlayer;

        if (owner == null)
            return;

        RebirthBossEventTargeting.RehydrateOwner(
            world,
            owner,
            "player-spawned-in-world");

#if DEBUG
        RebirthBossEventHealthTrace.TraceAllLoaded(
            world,
            "PLAYER-SPAWN-AFTER-OWNER-REHYDRATE");
#endif
    }

    private static void OnUpdate(ref ModEvents.SGameUpdateData data)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        RebirthBossEventWorldHooks.Ensure(world);
        RebirthBossEventWorldHooks.ProcessPending(world);
#if DEBUG
        RebirthBossEventHealthTrace.ObserveWorld(world);
#endif
        RebirthBossEventDirector.Update(world);
        RebirthBossEventLifecycle.Update(world);
        RebirthBossEventOwnerNetwork.Update(world);
        if (world != null && world.GetPrimaryPlayer() != null) RebirthBossEventHudMarkerHost.Ensure();
    }
    private static void OnWorldShutdown(ref ModEvents.SWorldShuttingDownData data)
    {
        try
        {
            World world =
                GameManager.Instance != null
                    ? GameManager.Instance.World
                    : null;

            // Base 3.1 invokes WorldShuttingDown before SaveWorld(). Capture the
            // exact live HP now so even an immediate quit after damaging a boss
            // cannot leave the Boss Events file with an older/full snapshot.
#if DEBUG
            RebirthBossEventHealthTrace.ExtendWindow(
                "world-shutting-down",
                10f);
            RebirthBossEventHealthTrace.TraceAllLoaded(
                world,
                "WORLD-SHUTTING-DOWN-BEFORE-CAPTURE");
#endif

            RebirthBossEventHealthPersistence.CaptureAllLoaded(
                world,
                "world-shutting-down");

#if DEBUG
            RebirthBossEventHealthTrace.TraceAllLoaded(
                world,
                "WORLD-SHUTTING-DOWN-AFTER-CAPTURE");
#endif

            RebirthBossEventPersistence.Save();
        }
        catch (Exception ex)
        {
            Log.Warning(
                "[REBIRTH BossEvents] save failed: " + ex.Message);
        }
    }
    private static void OnShutdown(ref ModEvents.SGameShutdownData data)
    {
        RebirthBossEventWorldHooks.Reset();
        RebirthBossEventDirector.Reset();
        RebirthBossEventLifecycle.Reset();
        RebirthBossEventOwnerNetwork.Reset();
        RebirthBossEventHudMarkerHost.DestroyHost();
    }
}

[Preserve]
public sealed class ConsoleCmdRebirthBossEvents : ConsoleCmdAbstract
{
    public override string[] getCommands()
    {
        return new[]
        {
            "rbevents",
            "rbbossevent"
        };
    }

    public override string getDescription()
    {
        return
            "Controls and reports authoritative REBIRTH Zombie Boss Events.";
    }

    public override string getHelp()
    {
        return
            "rbevents status\n" +
#if DEBUG
            "rbevents suite\n" +
#endif
            "rbevents composition\n" +
#if DEBUG
            "rbevents debug on|off\n" +
            "rbevents healthtrace [on|off|status]\n" +
            "rbevents frequencytest status|next|check\n" +
            "rbevents testspawn [small|normal|large|random] [low|normal|high|random] [rewards|norewards] [notify|silent]\n" +
#endif
            "rbevents spawn [small|normal|large|random] [low|normal|high|random] [rewards|norewards] [notify|silent]\n" +
            "rbevents spawn size=<small|normal|large|random> difficulty=<low|normal|high|random> [rewards|norewards] [notify|silent]\n" +
#if DEBUG
            "rbevents kill\n" +
#endif
            "rbevents sync\n" +
            "rbevents cancel [mine|all]\n" +
            "Alias: rbbossevent";
    }

    public override void Execute(
        List<string> args,
        CommandSenderInfo sender)
    {
        string cmd =
            args != null && args.Count > 0
                ? args[0].ToLowerInvariant()
                : "status";

        if (cmd == "status")
        {
            Log.Out(
                RebirthBossEventDiagnostics.Status());
            return;
        }

        if (cmd == "composition")
        {
            Log.Out(
                RebirthSpawnCompositionService
                    .GetValidationReport());

            return;
        }

#if DEBUG
        if (cmd == "debug")
        {
            string mode =
                args != null && args.Count > 1
                    ? args[1].ToLowerInvariant()
                    : string.Empty;

            if (mode == "on")
                RebirthBossEventDiagnostics.Enabled = true;
            else if (mode == "off")
                RebirthBossEventDiagnostics.Enabled = false;
            else
            {
                Log.Out(
                    "[REBIRTH BossEvents] debug=" +
                    RebirthBossEventDiagnostics.Enabled +
                    ". Use rbevents debug on|off.");
                return;
            }

            Log.Out(
                "[REBIRTH BossEvents] debug=" +
                RebirthBossEventDiagnostics.Enabled);

            return;
        }

#endif

        World world =
            GameManager.Instance != null
                ? GameManager.Instance.World
                : null;

        if (world == null)
        {
            Log.Out(
                "[REBIRTH BossEvents] world unavailable.");
            return;
        }

#if DEBUG
        if (cmd == "healthtrace" ||
            cmd == "health" ||
            cmd == "hp")
        {
            string mode =
                args != null && args.Count > 1
                    ? args[1].ToLowerInvariant()
                    : "on";

            if (mode == "off" || mode == "disable")
            {
                RebirthBossEventHealthTrace.Disable();
                Log.Out(
                    "[REBIRTH BossEvents:HealthTrace] disabled.");
                return;
            }

            if (mode == "status")
            {
                Log.Out(
                    "[REBIRTH BossEvents:HealthTrace] enabled=" +
                    RebirthBossEventHealthTrace.Enabled +
                    " active=" +
                    RebirthBossEventHealthTrace.Active);
                return;
            }

            RebirthBossEventHealthTrace.Enable(
                "manual-healthtrace-command",
                20f);
            Log.Out(
                RebirthBossEventHealthTrace.Dump(world));
            return;
        }

#endif

        EntityPlayer owner =
            ResolveOwner(world, sender);

#if DEBUG
        if (cmd == "frequencytest" ||
            cmd == "freqtest" ||
            cmd == "frequency")
        {
            if (owner == null)
            {
                Log.Out(
                    "[REBIRTH BossEvents:FrequencyTest] no player could be resolved for the command.");
                return;
            }

            string action =
                args != null && args.Count > 1
                    ? args[1].ToLowerInvariant()
                    : "status";

            if (action == "status" ||
                action == "show" ||
                action == "inspect")
            {
                Log.Out(
                    RebirthBossEventFrequencyTestProbe.Status(
                        world,
                        owner));
                return;
            }

            if (action == "next" ||
                action == "jump" ||
                action == "trigger")
            {
                Log.Out(
                    RebirthBossEventFrequencyTestProbe.JumpToNext(
                        world,
                        owner));
                return;
            }

            if (action == "check" ||
                action == "verify")
            {
                Log.Out(
                    RebirthBossEventFrequencyTestProbe.Check(
                        world,
                        owner));
                return;
            }

            Log.Out(
                "[REBIRTH BossEvents:FrequencyTest] usage: " +
                "rbevents frequencytest status|next|check");
            return;
        }

        if (cmd == "suite" || cmd == "test")
        {
            Log.Out(
                RebirthBossEventTestSuite.Run(
                    world,
                    owner));
            return;
        }

#endif

        if (world.IsRemote())
        {
            Log.Out(
                "[REBIRTH BossEvents] runtime event commands must execute on the authoritative server.");
            return;
        }

#if DEBUG
        if (cmd == "testspawn" ||
            cmd == "testevent" ||
            cmd == "eligibilitytest")
        {
            if (owner == null)
            {
                Log.Out(
                    "[REBIRTH BossEvents:TestSpawn] BLOCKED reason=no-player-resolved.");
                return;
            }

            RebirthBossEventOptionSnapshot options =
                RebirthBossEventOptions.Current.Clone();

            ApplySpawnOverrides(
                args,
                options);

            string eligibilityReason;
            if (!RebirthBossEventEligibility.CanStartNaturalTest(
                    world,
                    owner,
                    options,
                    out eligibilityReason))
            {
                int level =
                    owner.Progression != null
                        ? owner.Progression.Level
                        : -1;
                int hour =
                    GameUtils.WorldTimeToHours(
                        world.worldTime);
                bool outdoors =
                    RebirthBossEventPlacement.IsOutdoors(
                        world,
                        owner.position);
                bool questing =
                    RebirthBossEventEligibility.IsQuesting(
                        owner);

                Log.Out(
                    "[REBIRTH BossEvents:TestSpawn] BLOCKED reason=" +
                    eligibilityReason +
                    " owner=" + owner.EntityName +
                    " level=" +
                    level.ToString(CultureInfo.InvariantCulture) +
                    " minimumLevel=" +
                    options.MinimumPlayerLevel.ToString(CultureInfo.InvariantCulture) +
                    " hour=" +
                    hour.ToString(CultureInfo.InvariantCulture) +
                    " time=" + options.Time +
                    " restriction=" + options.Restriction +
                    " outdoors=" + outdoors +
                    " questing=" + questing +
                    " bloodMoonAllowed=" +
                    options.BloodMoonDayEvents +
                    ".");
                return;
            }

            int seed =
                RebirthBossEventSeed.Create(
                    world.Seed,
                    GameUtils.WorldTimeToDays(
                        world.worldTime),
                    RebirthBossEventIdentity.GetStableId(
                        owner),
                    unchecked((int)world.worldTime));

            string failure;
            bool started =
                RebirthBossEventDirector.TryStart(
                    world,
                    owner,
                    options,
                    seed,
                    out failure);

            if (started)
            {
                int level =
                    owner.Progression != null
                        ? owner.Progression.Level
                        : -1;
                string supportTier =
                    level >= 100
                        ? "red"
                        : (level >= 50
                            ? "blue"
                            : "yellow");

                Log.Out(
                    "[REBIRTH BossEvents:TestSpawn] STARTED owner=" +
                    owner.EntityName +
                    " level=" +
                    level.ToString(CultureInfo.InvariantCulture) +
                    " supportBag=" + supportTier +
                    " time=" + options.Time +
                    " restriction=" + options.Restriction +
                    ". Normal time/restriction/BloodMoon/active-event gates passed.");
            }
            else
            {
                Log.Out(
                    "[REBIRTH BossEvents:TestSpawn] FAILED after eligibility passed: " +
                    failure);
            }

            return;
        }
#endif

        if (cmd == "spawn")
        {
            if (owner == null)
            {
                Log.Out(
                    "[REBIRTH BossEvents] no player could be resolved for the command.");
                return;
            }

            RebirthBossEventOptionSnapshot options =
                RebirthBossEventOptions.Current.Clone();

            options.Enabled = true;

            ApplySpawnOverrides(
                args,
                options);

            int seed =
                RebirthBossEventSeed.Create(
                    world.Seed,
                    GameUtils.WorldTimeToDays(
                        world.worldTime),
                    RebirthBossEventIdentity.GetStableId(
                        owner),
                    unchecked((int)world.worldTime));

            string failure;
            bool started =
                RebirthBossEventDirector.TryStart(
                    world,
                    owner,
                    options,
                    seed,
                    out failure);

            if (started)
            {
                Log.Out(
                    "[REBIRTH BossEvents] forced event started owner=" +
                    owner.EntityName +
                    " size=" + options.Size +
                    " difficulty=" + options.Difficulty +
                    " rewards=" + options.Rewards +
                    " notifications=" +
                    options.Notifications + ".");
            }
            else
            {
                Log.Out(
                    "[REBIRTH BossEvents] forced event failed: " +
                    failure);
            }

            return;
        }

#if DEBUG
        if (cmd == "kill")
        {
            if (owner == null)
            {
                Log.Out(
                    "[REBIRTH BossEvents] no player could be resolved for the command.");
                return;
            }

            RebirthBossEventInstance e =
                RebirthBossEventTestTools
                    .FindOwnedActiveEvent(owner);

            if (e == null)
            {
                Log.Out(
                    "[REBIRTH BossEvents] no active owned boss event was found.");
                return;
            }

            EntityAlive boss =
                e.BossEntityId >= 0
                    ? world.GetEntity(
                        e.BossEntityId) as EntityAlive
                    : null;

            if (boss == null)
            {
                Log.Out(
                    "[REBIRTH BossEvents] event boss entity is not currently loaded; kill test aborted.");
                return;
            }

            boss.Kill(
                DamageResponse.New(true));

            // Force one immediate lifecycle observation so this command specifically
            // tests the "actual death completes immediately" path.
            RebirthBossEventLifecycle.Reset();
            RebirthBossEventLifecycle.Update(world);

            Log.Out(
                "[REBIRTH BossEvents] killed test boss event=" +
                e.EventId +
                " resultingState=" + e.State +
                " rewardIssued=" + e.RewardIssued +
                " rewardPlaced=" +
                e.RewardBlockPlaced + ".");

            return;
        }

#endif

        if (cmd == "sync")
        {
            List<RebirthBossEventInstance> events =
                RebirthBossEventRegistry
                    .SnapshotEvents();

            int sent = 0;
            string ownerId =
                owner != null
                    ? RebirthBossEventIdentity
                        .GetStableId(owner)
                    : null;

            for (int i = 0; i < events.Count; i++)
            {
                RebirthBossEventInstance e = events[i];

                if (e == null ||
                    e.Progression == null)
                    continue;

                if (ownerId != null &&
                    !string.Equals(
                        e.Progression.OwnerStableId,
                        ownerId,
                        StringComparison.Ordinal))
                    continue;

                RebirthBossEventOwnerNetwork.Send(
                    world,
                    e,
                    RebirthBossEventOwnerUpdateKind.ActiveSync);

                sent++;
            }

            RebirthBossEventRewardAccessNetwork
                .BroadcastCurrent(world);

            Log.Out(
                "[REBIRTH BossEvents] owner-state sync events=" +
                sent +
                "; reward-access ownership rebroadcast.");

            return;
        }

        if (cmd == "cancel")
        {
            bool all =
                args != null &&
                args.Count > 1 &&
                args[1].Equals(
                    "all",
                    StringComparison.OrdinalIgnoreCase);

            string ownerId =
                owner != null
                    ? RebirthBossEventIdentity
                        .GetStableId(owner)
                    : null;

            List<RebirthBossEventInstance> events =
                RebirthBossEventRegistry
                    .SnapshotEvents();

            int cancelled = 0;

            for (int i = 0; i < events.Count; i++)
            {
                RebirthBossEventInstance e = events[i];

                if (e == null ||
                    e.Progression == null)
                    continue;

                if (!all &&
                    !string.Equals(
                        e.Progression.OwnerStableId,
                        ownerId,
                        StringComparison.Ordinal))
                    continue;

                if (RebirthBossEventTestTools
                        .CleanupEvent(
                            world,
                            e,
                            true))
                {
                    cancelled++;
                }
            }

            Log.Out(
                "[REBIRTH BossEvents] cleanly cancelled events=" +
                cancelled +
                ".");

            return;
        }

        Log.Out(getHelp());
    }

    private static void ApplySpawnOverrides(
        List<string> args,
        RebirthBossEventOptionSnapshot options)
    {
        if (args == null ||
            options == null)
            return;

        for (int i = 1; i < args.Count; i++)
        {
            string raw =
                (args[i] ?? string.Empty)
                    .Trim()
                    .ToLowerInvariant();

            if (raw.Length == 0)
                continue;

            if (raw.StartsWith("size="))
            {
                RebirthBossEventSize parsed;
                if (TryParseSize(
                        raw.Substring(5),
                        out parsed))
                    options.Size = parsed;

                continue;
            }

            if (raw.StartsWith("difficulty="))
            {
                RebirthBossEventDifficulty parsed;
                if (TryParseDifficulty(
                        raw.Substring(11),
                        out parsed))
                    options.Difficulty = parsed;

                continue;
            }

            if (raw == "rewards")
            {
                options.Rewards = true;
                continue;
            }

            if (raw == "norewards")
            {
                options.Rewards = false;
                continue;
            }

            if (raw == "notify" ||
                raw == "notifications")
            {
                options.Notifications = true;
                continue;
            }

            if (raw == "silent" ||
                raw == "nonotify")
            {
                options.Notifications = false;
                continue;
            }

            RebirthBossEventSize size;
            if (TryParseSize(raw, out size) &&
                (raw == "small" ||
                 raw == "large" ||
                 i == 1))
            {
                options.Size = size;
                continue;
            }

            RebirthBossEventDifficulty difficulty;
            if (TryParseDifficulty(
                    raw,
                    out difficulty))
            {
                options.Difficulty = difficulty;
            }
        }
    }

    private static bool TryParseSize(
        string value,
        out RebirthBossEventSize result)
    {
        result = RebirthBossEventSize.Normal;

        if (value == "small")
            result = RebirthBossEventSize.Small;
        else if (value == "normal")
            result = RebirthBossEventSize.Normal;
        else if (value == "large")
            result = RebirthBossEventSize.Large;
        else if (value == "random")
            result = RebirthBossEventSize.Random;
        else
            return false;

        return true;
    }

    private static bool TryParseDifficulty(
        string value,
        out RebirthBossEventDifficulty result)
    {
        result = RebirthBossEventDifficulty.Normal;

        if (value == "low")
            result = RebirthBossEventDifficulty.Low;
        else if (value == "normal")
            result = RebirthBossEventDifficulty.Normal;
        else if (value == "high")
            result = RebirthBossEventDifficulty.High;
        else if (value == "random")
            result = RebirthBossEventDifficulty.Random;
        else
            return false;

        return true;
    }

    private static EntityPlayer ResolveOwner(
        World world,
        CommandSenderInfo sender)
    {
        EntityPlayer owner = null;

        if (sender.RemoteClientInfo != null)
        {
            owner =
                world.GetEntity(
                    sender.RemoteClientInfo.entityId)
                as EntityPlayer;
        }

        if (owner == null)
            owner = world.GetPrimaryPlayer();

        if (owner == null)
        {
            List<EntityPlayer> players =
                world.GetPlayers();

            if (players != null &&
                players.Count > 0)
                owner = players[0];
        }

        return owner;
    }
}

