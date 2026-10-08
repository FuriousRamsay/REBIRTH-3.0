using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Server-authoritative, POI-local sleeper activation pressure.
/// It never forces untouched SleeperVolumes to populate. It only wakes sleepers
/// already instantiated by native SleeperVolume processing and applies rate/cap gates.
/// Discovery itself is sliced so the wake cap is not preceded by an unbounded scan.
/// </summary>
public static class RebirthPoiSenseRuntimePolicy
{
    private enum WorkPhase
    {
        Idle = 0,
        CountActive = 1,
        ScoreSleeping = 2
    }

    private sealed class PlayerObservation
    {
        public int EntityId;
        public EntityPlayer Identity;
        public Vector3 Position;
        public float LastSeen;
    }

    private sealed class PoiState
    {
        public float LastEvaluation;
        public float NextEvaluation;
        public float WakeTokens;
        public float LastSeen;
        public long EvaluationCalls;
        public int LastCandidateCount;
        public int LastWakeAttempts;
        public int LastWakeSuccesses;
        public int TotalWakeAttempts;
        public int TotalWakeSuccesses;
        public string LastReason = "not evaluated";
        public string LastWakeError = string.Empty;

        public readonly List<PlayerObservation> Players = new List<PlayerObservation>(4);
        public readonly List<Vector3> ActivePositions = new List<Vector3>(16);
        public readonly List<Candidate> BestCandidates = new List<Candidate>(3);
        public WorkPhase Phase;
        public Profile WorkProfile;
        public int AvailableWakes;
        public int ActiveCount;
        public int VolumeIndex;
        public IEnumerator<KeyValuePair<int, SleeperVolume.RespawnData>> VolumeEnumerator;
        public ulong LastWorkTick = ulong.MaxValue;
        public int PlayerTieOffset;
    }

    private sealed class PlayerPoiLookup
    {
        public int EntityId;
        public EntityPlayer Identity;
        public World World;
        public Vector3 Position;
        public PrefabInstance Prefab;
        public float ExpiresAt;
        public float LastUsed;
        public ulong LastLookupTick = ulong.MaxValue;
    }

    private sealed class StateCleanupToken
    {
        public PrefabInstance Prefab;
        public PoiState State;
    }

    private sealed class LookupCleanupToken
    {
        public int EntityId;
        public PlayerPoiLookup Lookup;
    }

    private struct Profile
    {
        public float SoftRadius;
        public float HardRadius;
        public float WakeRate;
        public int ActiveCap;
        public int BurstCap;
    }

    private struct Candidate
    {
        public EntityAlive Entity;
        public EntityPlayer Target;
        public float Score;
        public int PlayerTie;
    }

    private static readonly Dictionary<PrefabInstance, PoiState> States = new Dictionary<PrefabInstance, PoiState>();
    private static readonly Dictionary<int, PlayerPoiLookup> PlayerLookups = new Dictionary<int, PlayerPoiLookup>();
    private static readonly Queue<StateCleanupToken> StateCleanup = new Queue<StateCleanupToken>();
    private static readonly Queue<LookupCleanupToken> LookupCleanup = new Queue<LookupCleanupToken>();

    private static World stateWorld;
    private static ulong lastCleanupTick = ulong.MaxValue;
    private static RebirthPoiSenseSchedule schedule = RebirthPoiSenseSchedule.Never;
    private static RebirthPoiSenseIntensity intensity = RebirthPoiSenseIntensity.Medium;

    private const float EvaluationInterval = 0.25f;
    private const float LookupCacheSeconds = 0.20f;
    private const float LookupMovementDistance = 1.50f;
    private const float PlayerObservationSeconds = 0.75f;
    private const float LookupExpirySeconds = 2f;
    private const float StateExpirySeconds = 30f;
    private const int MaxPlayersPerPoi = 16;
    private const int PreparationOperationsPerSlice = 64;
    private const int ScoringOperationsPerSlice = 64;
    private const int StateCleanupPerTick = 8;
    private const int LookupCleanupPerTick = 16;

    public static RebirthPoiSenseSchedule Schedule { get { return schedule; } }
    public static RebirthPoiSenseIntensity Intensity { get { return intensity; } }

    public static void SetOptions(RebirthPoiSenseSchedule newSchedule, RebirthPoiSenseIntensity newIntensity)
    {
        newSchedule = newSchedule < RebirthPoiSenseSchedule.Never || newSchedule > RebirthPoiSenseSchedule.Always
            ? RebirthPoiSenseSchedule.Never : newSchedule;
        newIntensity = newIntensity < RebirthPoiSenseIntensity.Low || newIntensity > RebirthPoiSenseIntensity.High
            ? RebirthPoiSenseIntensity.Medium : newIntensity;

        if (schedule != newSchedule || intensity != newIntensity)
            ClearRuntimeState();

        schedule = newSchedule;
        intensity = newIntensity;

        // Keep POI Sense independent from the rest of the bootstrap chain. If an unrelated
        // installer fails before RebirthFreshInit reaches this feature, applying the option
        // state still guarantees that the authoritative player-update hook is installed.
        if (!RebirthPoiSenseRuntimeIntegration.IsInstalled)
            RebirthPoiSenseRuntimeIntegration.Install();
    }

    public static bool IsScheduleActive(World world)
    {
        if (world == null || world.IsRemote()) return false;
        if (schedule == RebirthPoiSenseSchedule.Never) return false;
        if (schedule == RebirthPoiSenseSchedule.Always) return true;
        bool day = world.IsDaytime();
        return schedule == RebirthPoiSenseSchedule.DayOnly ? day : !day;
    }

    public static void Evaluate(World world, EntityPlayer player)
    {
        if (world == null || player == null) return;
        if (world.IsRemote()) return;
        if (player.IsSpectator || !player.IsAlive()) return;

        // Preserve the opt-in contract: Never returns before any POI lookup, cache,
        // scanner or cleanup preparation is touched.
        if (!IsScheduleActive(world)) return;

        EnsureWorldIdentity(world);
        float now = Time.realtimeSinceStartup;
        ulong tick = CurrentTick();
        RunBoundedCleanup(now, tick);

        PrefabInstance prefab = ResolvePoi(world, player, now, tick);
        if (prefab == null || prefab.sleeperVolumes == null || prefab.sleeperVolumes.Count == 0) return;

        PoiState state;
        if (!States.TryGetValue(prefab, out state))
        {
            state = new PoiState
            {
                LastEvaluation = now,
                NextEvaluation = now + EvaluationInterval,
                WakeTokens = 1f,
                LastSeen = now,
                LastReason = "collecting player observations"
            };
            States[prefab] = state;
            StateCleanup.Enqueue(new StateCleanupToken { Prefab = prefab, State = state });
        }

        state.EvaluationCalls++;
        state.LastSeen = now;
        ObservePlayer(state, player, now);

        if (state.Phase == WorkPhase.Idle)
        {
            if (now < state.NextEvaluation)
            {
                state.LastReason = "waiting for interval/player aggregation";
                return;
            }

            StartEvaluation(state, now);
        }

        ProcessOneWorkSlice(world, prefab, state, now, tick);
    }

    public static void ReportPatchException(Exception ex)
    {
        if (ex == null) return;
        RebirthPoiSenseRuntimeIntegration.RecordRuntimeError(ex.GetType().Name + ": " + ex.Message);
    }

    private static void StartEvaluation(PoiState state, float now)
    {
        RemoveStalePlayers(state, now);
        if (state.Players.Count == 0)
        {
            FinishEvaluation(state, now, "no recent players");
            return;
        }

        float elapsed = state.LastEvaluation > 0f ? now - state.LastEvaluation : EvaluationInterval;
        if (elapsed < EvaluationInterval) elapsed = EvaluationInterval;
        state.LastEvaluation = now;
        state.NextEvaluation = now + EvaluationInterval;
        state.LastCandidateCount = 0;
        state.LastWakeAttempts = 0;
        state.LastWakeSuccesses = 0;
        state.LastWakeError = string.Empty;
        state.ActiveCount = 0;
        state.ActivePositions.Clear();
        state.BestCandidates.Clear();
        state.WorkProfile = GetProfile(intensity);
        state.WakeTokens = Mathf.Min(
            state.WorkProfile.BurstCap,
            state.WakeTokens + elapsed * state.WorkProfile.WakeRate);
        state.AvailableWakes = Mathf.Min(
            state.WorkProfile.BurstCap,
            Mathf.FloorToInt(state.WakeTokens));

        if (state.AvailableWakes <= 0)
        {
            FinishEvaluation(state, now, "waiting for wake token");
            return;
        }

        state.PlayerTieOffset = state.Players.Count > 0
            ? (state.PlayerTieOffset + 1) % state.Players.Count
            : 0;
        ResetScanner(state);
        state.Phase = WorkPhase.CountActive;
        state.LastReason = "preparing active sleeper count";
    }

    private static void ProcessOneWorkSlice(World world, PrefabInstance prefab, PoiState state, float now, ulong tick)
    {
        if (state == null || state.Phase == WorkPhase.Idle) return;
        if (state.LastWorkTick == tick) return;
        state.LastWorkTick = tick;

        if (state.Phase == WorkPhase.CountActive)
        {
            bool complete = ProcessActiveSlice(world, prefab, state);
            if (!complete) return;

            int capRemaining = state.WorkProfile.ActiveCap - state.ActiveCount;
            if (capRemaining <= 0)
            {
                FinishEvaluation(state, now, "active cap reached");
                return;
            }

            state.AvailableWakes = Mathf.Min(state.AvailableWakes, capRemaining);
            if (state.AvailableWakes <= 0)
            {
                FinishEvaluation(state, now, "no wake capacity");
                return;
            }

            ResetScanner(state);
            state.Phase = WorkPhase.ScoreSleeping;
            state.LastReason = "scoring sleeping candidates";
            return;
        }

        if (state.Phase == WorkPhase.ScoreSleeping)
        {
            bool complete = ProcessCandidateSlice(world, prefab, state);
            if (!complete) return;

            WakeBestCandidates(world, state);
            FinishEvaluation(
                state,
                now,
                state.LastWakeSuccesses > 0
                    ? "wake succeeded"
                    : (state.LastWakeAttempts > 0 ? "wake attempted but failed" : "no sleeping candidates in radius"));
        }
    }

    private static bool ProcessActiveSlice(World world, PrefabInstance prefab, PoiState state)
    {
        List<SleeperVolume> volumes = prefab != null ? prefab.sleeperVolumes : null;
        if (volumes == null) return true;

        for (int work = 0; work < PreparationOperationsPerSlice; work++)
        {
            int entityId;
            bool hasEntity;
            if (!AdvanceScanner(state, volumes, out entityId, out hasEntity))
                return true;
            if (!hasEntity) continue;

            EntityAlive entity = world.GetEntity(entityId) as EntityAlive;
            if (entity == null || !entity.IsAlive() || entity.IsSleeping) continue;

            state.ActiveCount++;
            if (state.ActivePositions.Count < state.WorkProfile.ActiveCap)
                state.ActivePositions.Add(entity.position);

            // Once the native-active cap is already met, no candidate can wake
            // this interval; do not scan the rest of the prefab merely for diagnostics.
            if (state.ActiveCount >= state.WorkProfile.ActiveCap)
                return true;
        }

        return false;
    }

    private static bool ProcessCandidateSlice(World world, PrefabInstance prefab, PoiState state)
    {
        List<SleeperVolume> volumes = prefab != null ? prefab.sleeperVolumes : null;
        if (volumes == null) return true;

        for (int work = 0; work < ScoringOperationsPerSlice; work++)
        {
            int entityId;
            bool hasEntity;
            if (!AdvanceScanner(state, volumes, out entityId, out hasEntity))
                return true;
            if (!hasEntity) continue;

            EntityAlive entity = world.GetEntity(entityId) as EntityAlive;
            if (entity == null || !entity.IsAlive() || !entity.IsSleeping) continue;

            Candidate candidate;
            if (!TryScoreCandidate(entity, state, out candidate)) continue;
            state.LastCandidateCount++;
            InsertBestCandidate(state.BestCandidates, candidate, state.AvailableWakes);
        }

        return false;
    }

    private static bool AdvanceScanner(
        PoiState state,
        List<SleeperVolume> volumes,
        out int entityId,
        out bool hasEntity)
    {
        entityId = -1;
        hasEntity = false;

        if (state.VolumeEnumerator != null)
        {
            try
            {
                if (state.VolumeEnumerator.MoveNext())
                {
                    entityId = state.VolumeEnumerator.Current.Key;
                    hasEntity = true;
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // Native respawn maps may change while a sliced scan is in flight.
                // Abandon only this volume; the next evaluation starts from fresh maps.
                state.LastWakeError = "sleeper map changed during bounded scan";
            }

            DisposeEnumerator(state);
            state.VolumeIndex++;
            return true;
        }

        if (state.VolumeIndex >= volumes.Count)
            return false;

        SleeperVolume volume = volumes[state.VolumeIndex];
        if (volume == null || volume.respawnMap == null)
        {
            state.VolumeIndex++;
            return true;
        }

        state.VolumeEnumerator = volume.respawnMap.GetEnumerator();
        return true;
    }

    private static bool TryScoreCandidate(EntityAlive entity, PoiState state, out Candidate candidate)
    {
        candidate = default(Candidate);
        if (entity == null || state == null || state.Players.Count == 0) return false;

        float hardSq = state.WorkProfile.HardRadius * state.WorkProfile.HardRadius;
        float softSq = state.WorkProfile.SoftRadius * state.WorkProfile.SoftRadius;
        bool propagated = IsNearActiveSleeper(
            state.ActivePositions,
            entity.position,
            intensity == RebirthPoiSenseIntensity.High ? 16f : 12f);

        bool found = false;
        float bestScore = float.MaxValue;
        int bestTie = int.MaxValue;
        EntityPlayer bestPlayer = null;
        int count = state.Players.Count;

        for (int step = 0; step < count; step++)
        {
            int index = (state.PlayerTieOffset + step) % count;
            PlayerObservation observation = state.Players[index];
            EntityPlayer player = observation != null ? observation.Identity : null;
            if (player == null || !player.IsAlive() || player.IsSpectator) continue;

            Vector3 playerPos = observation.Position;
            float distSq = (entity.position - playerPos).sqrMagnitude;
            if (distSq > hardSq) continue;

            bool nativeDetect = false;
            try { nativeDetect = player.Stealth.CanSleeperAttackDetect(entity); }
            catch { nativeDetect = false; }

            float score = distSq;
            if (nativeDetect) score -= 100000f;
            if (distSq <= softSq) score -= 50000f;
            if (propagated) score -= 25000f;

            if (!found || score < bestScore || (Mathf.Approximately(score, bestScore) && step < bestTie))
            {
                found = true;
                bestScore = score;
                bestTie = step;
                bestPlayer = player;
            }
        }

        if (!found || bestPlayer == null) return false;
        candidate = new Candidate
        {
            Entity = entity,
            Target = bestPlayer,
            Score = bestScore,
            PlayerTie = bestTie
        };
        return true;
    }

    private static void InsertBestCandidate(List<Candidate> best, Candidate candidate, int capacity)
    {
        if (capacity <= 0) return;
        int insertAt = best.Count;
        for (int i = 0; i < best.Count; i++)
        {
            Candidate existing = best[i];
            if (candidate.Score < existing.Score
                || (Mathf.Approximately(candidate.Score, existing.Score) && candidate.PlayerTie < existing.PlayerTie)
                || (Mathf.Approximately(candidate.Score, existing.Score)
                    && candidate.PlayerTie == existing.PlayerTie
                    && candidate.Entity != null
                    && existing.Entity != null
                    && candidate.Entity.entityId < existing.Entity.entityId))
            {
                insertAt = i;
                break;
            }
        }

        if (insertAt >= capacity) return;
        best.Insert(insertAt, candidate);
        if (best.Count > capacity) best.RemoveAt(best.Count - 1);
    }

    private static void WakeBestCandidates(World world, PoiState state)
    {
        for (int i = 0; i < state.BestCandidates.Count && i < state.AvailableWakes; i++)
        {
            Candidate candidate = state.BestCandidates[i];
            EntityAlive entity = candidate.Entity;
            EntityPlayer player = candidate.Target;
            if (entity == null || !entity.IsAlive() || !entity.IsSleeping) continue;
            if (player == null || !player.IsAlive() || player.IsSpectator) continue;

            state.LastWakeAttempts++;
            state.TotalWakeAttempts++;
            state.WakeTokens = Mathf.Max(0f, state.WakeTokens - 1f);

            string error;
            if (TryWakeSleeper(world, entity, player, out error))
            {
                state.LastWakeSuccesses++;
                state.TotalWakeSuccesses++;
            }
            else if (!string.IsNullOrEmpty(error))
            {
                state.LastWakeError = error;
            }
        }
    }

    private static void FinishEvaluation(PoiState state, float now, string reason)
    {
        if (state == null) return;
        DisposeEnumerator(state);
        state.VolumeIndex = 0;
        state.Phase = WorkPhase.Idle;
        state.LastReason = reason ?? string.Empty;
        state.LastSeen = now;
        state.ActivePositions.Clear();
        state.BestCandidates.Clear();
    }

    private static void ResetScanner(PoiState state)
    {
        DisposeEnumerator(state);
        state.VolumeIndex = 0;
    }

    private static void DisposeEnumerator(PoiState state)
    {
        if (state == null || state.VolumeEnumerator == null) return;
        try { state.VolumeEnumerator.Dispose(); }
        catch { }
        state.VolumeEnumerator = null;
    }

    private static void ObservePlayer(PoiState state, EntityPlayer player, float now)
    {
        for (int i = 0; i < state.Players.Count; i++)
        {
            PlayerObservation observation = state.Players[i];
            if (observation != null && observation.EntityId == player.entityId)
            {
                observation.Identity = player;
                observation.Position = player.position;
                observation.LastSeen = now;
                return;
            }
        }

        if (state.Players.Count >= MaxPlayersPerPoi)
        {
            int oldest = 0;
            for (int i = 1; i < state.Players.Count; i++)
                if (state.Players[i].LastSeen < state.Players[oldest].LastSeen) oldest = i;
            state.Players.RemoveAt(oldest);
        }

        state.Players.Add(new PlayerObservation
        {
            EntityId = player.entityId,
            Identity = player,
            Position = player.position,
            LastSeen = now
        });
    }

    private static void RemoveStalePlayers(PoiState state, float now)
    {
        for (int i = state.Players.Count - 1; i >= 0; i--)
        {
            PlayerObservation observation = state.Players[i];
            EntityPlayer player = observation != null ? observation.Identity : null;
            if (observation == null
                || player == null
                || !player.IsAlive()
                || player.IsSpectator
                || now - observation.LastSeen > PlayerObservationSeconds)
            {
                state.Players.RemoveAt(i);
            }
        }
    }

    private static PrefabInstance ResolvePoi(World world, EntityPlayer player, float now, ulong tick)
    {
        PlayerPoiLookup lookup;
        if (!PlayerLookups.TryGetValue(player.entityId, out lookup)
            || lookup == null
            || !ReferenceEquals(lookup.Identity, player)
            || !ReferenceEquals(lookup.World, world))
        {
            lookup = new PlayerPoiLookup
            {
                EntityId = player.entityId,
                Identity = player,
                World = world,
                Position = player.position,
                LastUsed = now
            };
            PlayerLookups[player.entityId] = lookup;
            LookupCleanup.Enqueue(new LookupCleanupToken { EntityId = player.entityId, Lookup = lookup });
        }

        lookup.LastUsed = now;
        Vector3 delta = player.position - lookup.Position;
        delta.y = 0f;
        bool movementInvalidated = delta.sqrMagnitude > LookupMovementDistance * LookupMovementDistance;
        bool cacheValid = lookup.LastLookupTick == tick
            || (now < lookup.ExpiresAt && !movementInvalidated);
        if (cacheValid) return lookup.Prefab;

        PrefabInstance prefab = null;
        try { prefab = world.GetPOIAtPosition(player.position); }
        catch { prefab = null; }

        lookup.Position = player.position;
        lookup.Prefab = prefab;
        lookup.ExpiresAt = now + LookupCacheSeconds;
        lookup.LastLookupTick = tick;
        return prefab;
    }

    private static void EnsureWorldIdentity(World world)
    {
        if (ReferenceEquals(stateWorld, world)) return;
        ClearRuntimeState();
        stateWorld = world;
    }

    private static void ClearRuntimeState()
    {
        foreach (KeyValuePair<PrefabInstance, PoiState> pair in States)
            DisposeEnumerator(pair.Value);
        States.Clear();
        PlayerLookups.Clear();
        StateCleanup.Clear();
        LookupCleanup.Clear();
        stateWorld = null;
        lastCleanupTick = ulong.MaxValue;
    }

    private static void RunBoundedCleanup(float now, ulong tick)
    {
        if (lastCleanupTick == tick) return;
        lastCleanupTick = tick;

        for (int i = 0; i < StateCleanupPerTick && StateCleanup.Count > 0; i++)
        {
            StateCleanupToken token = StateCleanup.Dequeue();
            PoiState current;
            if (token == null || token.Prefab == null
                || !States.TryGetValue(token.Prefab, out current)
                || !ReferenceEquals(current, token.State))
            {
                continue;
            }

            if (now - current.LastSeen > StateExpirySeconds)
            {
                DisposeEnumerator(current);
                States.Remove(token.Prefab);
                continue;
            }

            StateCleanup.Enqueue(token);
        }

        for (int i = 0; i < LookupCleanupPerTick && LookupCleanup.Count > 0; i++)
        {
            LookupCleanupToken token = LookupCleanup.Dequeue();
            PlayerPoiLookup current;
            if (token == null
                || !PlayerLookups.TryGetValue(token.EntityId, out current)
                || !ReferenceEquals(current, token.Lookup))
            {
                continue;
            }

            if (current.Identity == null
                || !ReferenceEquals(current.World, stateWorld)
                || now - current.LastUsed > LookupExpirySeconds)
            {
                PlayerLookups.Remove(token.EntityId);
                continue;
            }

            LookupCleanup.Enqueue(token);
        }
    }

    private static ulong CurrentTick()
    {
        return GameTimer.Instance != null ? GameTimer.Instance.ticks : 0UL;
    }

    private static bool TryWakeSleeper(World world, EntityAlive entity, EntityPlayer player, out string error)
    {
        error = string.Empty;
        if (world == null || entity == null || player == null) return false;

        try
        {
            entity.ConditionalTriggerSleeperWakeUp();
            if (!entity.IsSleeping)
            {
                entity.SetAttackTarget(player, 400);
                return true;
            }
            error = "native wake returned while IsSleeping remained true";
        }
        catch (Exception ex)
        {
            error = "native wake " + ex.GetType().Name + ": " + ex.Message;
        }

        // Defensive 3.1 fallback. The native call is always attempted first. This branch
        // completes the same authoritative state transition and client notification if a
        // partially initialized sleeper model causes the native animation call to fail.
        try
        {
            entity.IsSleeping = false;
            entity.IsSleeperPassive = false;
            if (entity.aiManager != null)
                entity.aiManager.SleeperWokeUp();
            entity.SetAttackTarget(player, 400);

            if (!world.IsRemote())
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                    (NetPackage)NetPackageManager.GetPackage<NetPackageSleeperWakeup>().Setup(entity.entityId));
            }

            return !entity.IsSleeping;
        }
        catch (Exception fallbackEx)
        {
            error = error + " | fallback " + fallbackEx.GetType().Name + ": " + fallbackEx.Message;
            return false;
        }
    }

    public static string GetDiagnosticReport(World world, EntityPlayer player)
    {
        if (world == null) return "[RebirthPOISense] No active world.";
        if (player == null) return "[RebirthPOISense] No player.";

        bool scheduleWindowActive = schedule == RebirthPoiSenseSchedule.Always
            || (schedule == RebirthPoiSenseSchedule.DayOnly && world.IsDaytime())
            || (schedule == RebirthPoiSenseSchedule.NightOnly && !world.IsDaytime());

        PrefabInstance prefab = null;
        try { prefab = world.GetPOIAtPosition(player.position); }
        catch { prefab = null; }

        if (prefab == null || prefab.sleeperVolumes == null)
        {
            return "[RebirthPOISense] outside POI"
                + " authority=" + (!world.IsRemote())
                + " runtimeInstalled=" + RebirthPoiSenseRuntimeIntegration.IsInstalled
                + " hook=EntityPlayer.CheckSleeperTriggers"
                + " schedule=" + schedule
                + " intensity=" + intensity
                + " scheduleActive=" + scheduleWindowActive
                + FormatRuntimeError();
        }

        Profile profile = GetProfile(intensity);
        int active = 0;
        int sleeping = 0;
        int inRadius = 0;
        float hardSq = profile.HardRadius * profile.HardRadius;
        List<SleeperVolume> volumes = prefab.sleeperVolumes;
        for (int i = 0; i < volumes.Count; i++)
        {
            SleeperVolume volume = volumes[i];
            if (volume == null || volume.respawnMap == null) continue;
            foreach (KeyValuePair<int, SleeperVolume.RespawnData> entry in volume.respawnMap)
            {
                EntityAlive entity = world.GetEntity(entry.Key) as EntityAlive;
                if (entity == null || !entity.IsAlive()) continue;
                if (entity.IsSleeping)
                {
                    sleeping++;
                    if ((entity.position - player.position).sqrMagnitude <= hardSq) inRadius++;
                }
                else
                {
                    active++;
                }
            }
        }

        PoiState state;
        bool hasState = States.TryGetValue(prefab, out state);
        float now = Time.realtimeSinceStartup;
        string stateText = hasState
            ? " evalCalls=" + state.EvaluationCalls
                + " lastEvalAge=" + Mathf.Max(0f, now - state.LastEvaluation).ToString("0.00")
                + " tokens=" + state.WakeTokens.ToString("0.00")
                + " players=" + state.Players.Count
                + " phase=" + state.Phase
                + " candidates=" + state.LastCandidateCount
                + " lastAttempts=" + state.LastWakeAttempts
                + " lastSuccess=" + state.LastWakeSuccesses
                + " totalAttempts=" + state.TotalWakeAttempts
                + " totalSuccess=" + state.TotalWakeSuccesses
                + " lastReason=" + state.LastReason
                + (string.IsNullOrEmpty(state.LastWakeError) ? string.Empty : " wakeError=" + state.LastWakeError)
            : " evalCalls=0 lastReason=no runtime state for this POI";

        return "[RebirthPOISense] prefab=" + prefab.name
            + " authority=" + (!world.IsRemote())
            + " runtimeInstalled=" + RebirthPoiSenseRuntimeIntegration.IsInstalled
            + " hook=EntityPlayer.CheckSleeperTriggers"
            + " schedule=" + schedule
            + " intensity=" + intensity
            + " scheduleActive=" + scheduleWindowActive
            + " hardRadius=" + profile.HardRadius
            + " wakeRate=" + profile.WakeRate
            + " active=" + active + "/" + profile.ActiveCap
            + " sleepingSpawned=" + sleeping
            + " sleepingInRadius=" + inRadius
            + stateText
            + FormatRuntimeError();
    }

    private static string FormatRuntimeError()
    {
        return string.IsNullOrEmpty(RebirthPoiSenseRuntimeIntegration.LastRuntimeError)
            ? string.Empty
            : " runtimeError=" + RebirthPoiSenseRuntimeIntegration.LastRuntimeError;
    }

    private static bool IsNearActiveSleeper(List<Vector3> activePositions, Vector3 position, float radius)
    {
        float radiusSq = radius * radius;
        for (int i = 0; i < activePositions.Count; i++)
            if ((activePositions[i] - position).sqrMagnitude <= radiusSq) return true;
        return false;
    }

    private static Profile GetProfile(RebirthPoiSenseIntensity value)
    {
        if (value == RebirthPoiSenseIntensity.Low)
            return new Profile { SoftRadius = 18f, HardRadius = 28f, WakeRate = 2f, ActiveCap = 8, BurstCap = 2 };
        if (value == RebirthPoiSenseIntensity.High)
            return new Profile { SoftRadius = 35f, HardRadius = 55f, WakeRate = 6f, ActiveCap = 16, BurstCap = 3 };
        return new Profile { SoftRadius = 25f, HardRadius = 40f, WakeRate = 4f, ActiveCap = 12, BurstCap = 2 };
    }
}

public static class RebirthPoiSenseRuntimeIntegration
{
    private const string HarmonyId = "rebirth.poisense.runtime.3.1";
    private static bool installed;
    private static string lastRuntimeError = string.Empty;

    public static bool IsInstalled { get { return installed; } }
    public static string LastRuntimeError { get { return lastRuntimeError; } }

    public static string Install()
    {
        if (installed) return "[RebirthPoiSense] Runtime integration already installed";
        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(new Harmony(HarmonyId), typeof(RebirthPoiSensePlayerSleeperTriggerPatch));
            installed = true;
            lastRuntimeError = string.Empty;
            string result = "[RebirthPoiSense] Installed authoritative EntityPlayer.CheckSleeperTriggers activation";
            { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(result); }
            return result;
        }
        catch (Exception ex)
        {
            installed = false;
            lastRuntimeError = "install " + ex.GetType().Name + ": " + ex.Message;
            string result = "[RebirthPoiSense] Failed to install: " + lastRuntimeError;
            Log.Error(result);
            return result;
        }
    }

    public static void RecordRuntimeError(string error)
    {
        lastRuntimeError = error ?? string.Empty;
    }
}

[HarmonyPatch(typeof(EntityPlayer), nameof(EntityPlayer.CheckSleeperTriggers))]
public static class RebirthPoiSensePlayerSleeperTriggerPatch
{
    public static void Postfix(EntityPlayer __instance)
    {
        try
        {
            if (__instance == null) return;
            World world = __instance.world as World;
            RebirthPoiSenseRuntimePolicy.Evaluate(world, __instance);
        }
        catch (Exception ex)
        {
            RebirthPoiSenseRuntimePolicy.ReportPatchException(ex);
        }
    }
}
