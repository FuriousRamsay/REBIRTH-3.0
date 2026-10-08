using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Server-authoritative active-play clock for Survivor condition state. Chunk 10 connects this
/// resolver to the existing metabolism Energy/demand paths and the single native Health Capacity
/// application surface while preserving active-play-only condition timing.
/// </summary>
public static class RebirthSurvivorConditionService
{
    private sealed class RuntimeClock
    {
        public float LastRealTime;
        public float UnsyncedSeconds;
    }

    private static readonly Dictionary<int, RuntimeClock> Clocks = new Dictionary<int, RuntimeClock>();
    private static readonly Dictionary<string, RebirthConditionResolvedSnapshot> ResolvedByStableKey = new Dictionary<string, RebirthConditionResolvedSnapshot>(StringComparer.Ordinal);
    private static bool installed;
    private static float nextGlobalTick;

    public static string Install()
    {
        if (installed) return "[REBIRTH Survivor Condition] already installed";
        string config = RebirthConditionRuntimeConfig.Load();
        RebirthStressService.Install();
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed = true;
        return "[REBIRTH Survivor Condition] installed " + config;
    }

    // Identity-only token: no mutable resolved state is exposed to projection consumers.
    internal static object GetResolvedProjectionToken(RebirthWorldCharacterRecord record)
    {
        RebirthConditionResolvedSnapshot resolved;
        return record != null && !string.IsNullOrEmpty(record.StablePlayerKey)
            && ResolvedByStableKey.TryGetValue(record.StablePlayerKey, out resolved) ? (object)resolved : null;
    }

    public static RebirthConditionStatusSnapshot BuildStatus(RebirthWorldCharacterRecord record)
    {
        RebirthConditionStatusSnapshot s = new RebirthConditionStatusSnapshot();
        if (record == null || record.Condition == null) return s;
        RebirthConditionResolvedSnapshot resolved;
        if (record != null && !string.IsNullOrEmpty(record.StablePlayerKey) && ResolvedByStableKey.TryGetValue(record.StablePlayerKey, out resolved) && resolved != null) { }
        else resolved = RebirthConditionResolver.Resolve(record);
        s.MoodCurrent = record.Condition.MoodCurrent;
        s.MoodTarget = record.Condition.MoodTarget;
        s.DietSatisfaction = record.Condition.DietSatisfaction;
        s.HealthCapacity = record.Condition.HealthCapacity;
        s.HealthPotential = RebirthHealthCapacityService.GetPotential(record);
        s.RecentMealCount = Math.Min(record.Condition.RecentMeals.Count, RebirthConditionRuntimeConfig.MealHistorySize);
        s.RecentVarietyCount = RebirthDietSatisfactionService.CountRecentCompatibleVarieties(record.Condition);
        if (record.Condition.RecentMeals.Count > 0)
        {
            RebirthRecentMealState last = record.Condition.RecentMeals[record.Condition.RecentMeals.Count - 1];
            s.HasRecentMeal = last != null;
            s.LastMealCompatible = last != null && last.CompatibleWithDiet;
        }
        s.DominantPositiveCauseId = resolved.DominantPositiveCauseId;
        s.DominantPositiveCauseDelta = resolved.DominantPositiveCauseDelta;
        s.DominantNegativeCauseId = resolved.DominantNegativeCauseId;
        s.DominantNegativeCauseDelta = resolved.DominantNegativeCauseDelta;
        s.EnergyUseMultiplier = resolved.EnergyUseMultiplier;
        s.EnergyRecoveryMultiplier = resolved.EnergyRecoveryMultiplier;
        s.HydrationDemandMultiplier = resolved.HydrationDemandMultiplier;
        s.NutritionUseMultiplier = resolved.NutritionUseMultiplier;
        s.HealthCapacityLossMultiplier = resolved.HealthCapacityLossMultiplier;
        s.HealthCapacityRecoveryMultiplier = resolved.HealthCapacityRecoveryMultiplier;
        s.SevereDehydrationActiveSeconds = record.Condition.SevereDehydrationActiveSeconds;
        s.SevereMalnutritionActiveSeconds = record.Condition.SevereMalnutritionActiveSeconds;
        return s;
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data) { ResetRuntime(); }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { ResetRuntime(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { ResetRuntime(); }

    private static void ResetRuntime()
    {
        Clocks.Clear();
        ResolvedByStableKey.Clear();
        nextGlobalTick = 0f;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        if (GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.World.Players == null || GameManager.Instance.World.Players.list == null) return;
        float now = Time.realtimeSinceStartup;
        if (now < nextGlobalTick) return;
        nextGlobalTick = now + RebirthConditionRuntimeConfig.TickSeconds;

        List<EntityPlayer> players = GameManager.Instance.World.Players.list;
        for (int i = 0; i < players.Count; i++) TickPlayer(players[i], now);
        PruneDeadClocks(players);
    }

    private static void TickPlayer(EntityPlayer player, float now)
    {
        if (player == null) return;
        // Native Trait passives are synchronized even for no-character/hold states so stale CVars cannot survive.
        RebirthTraitGameplayModifierService.SyncNativePassiveEffects(player);
        if (RebirthCharacterCreationHoldService.IsHeld(player)) { ResetClock(player, now); return; }
        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete || record.Condition == null)
        { ResetClock(player, now); return; }

        RuntimeClock clock;
        if (!Clocks.TryGetValue(player.entityId, out clock) || clock == null)
        {
            Clocks[player.entityId] = new RuntimeClock { LastRealTime = now };
            return;
        }
        float elapsed = Mathf.Clamp(now - clock.LastRealTime, 0f, Math.Max(2f, RebirthConditionRuntimeConfig.TickSeconds * 2f));
        clock.LastRealTime = now;
        if (elapsed <= 0f) return;

        record.Condition.ActivePlaySeconds += elapsed;
        if (player.world != null) record.Condition.LastActiveWorldTime = player.world.GetWorldTime();
        for (int i = 0; i < record.Condition.RecentMeals.Count; i++)
        {
            RebirthRecentMealState meal = record.Condition.RecentMeals[i];
            if (meal != null) meal.AgeActiveSeconds = Math.Max(0f, meal.AgeActiveSeconds + elapsed);
        }
        RebirthMetabolismState metabolism;
        RebirthWorldCharacterService.TryGetMetabolism(player, out metabolism);
        bool visibleChanged = RebirthTraitSupportService.Tick(player, record, elapsed);
        RebirthStressService.Tick(player,record,elapsed);
        visibleChanged |= RebirthMoodService.Tick(player, record, metabolism, elapsed);
        visibleChanged |= RebirthHealthCapacityService.Tick(player, record, elapsed);
        if (!string.IsNullOrEmpty(record.StablePlayerKey))
            ResolvedByStableKey[record.StablePlayerKey] = RebirthConditionResolver.Resolve(player, record, metabolism);
        clock.UnsyncedSeconds += elapsed;
        if (clock.UnsyncedSeconds < RebirthConditionRuntimeConfig.OwnerSyncSeconds && !visibleChanged) return;
        if (clock.UnsyncedSeconds < RebirthConditionRuntimeConfig.OwnerSyncSeconds) return;

        clock.UnsyncedSeconds = 0f;
        RebirthWorldCharacterService.MarkDirty(record, "condition-active-tick");
        RebirthSurvivorNetworkService.SendOwnerState(player, Math.Max(0L, record.Revision - 1L), false, "condition-active-tick");
    }

    private static void ResetClock(EntityPlayer player, float now)
    {
        if (player == null) return;
        RuntimeClock clock;
        if (Clocks.TryGetValue(player.entityId, out clock) && clock != null) clock.LastRealTime = now;
    }

    private static void PruneDeadClocks(List<EntityPlayer> players)
    {
        if (Clocks.Count == 0) return;
        HashSet<int> live = new HashSet<int>();
        for (int i = 0; i < players.Count; i++) if (players[i] != null) live.Add(players[i].entityId);
        List<int> remove = null;
        foreach (KeyValuePair<int, RuntimeClock> pair in Clocks)
            if (!live.Contains(pair.Key)) { if (remove == null) remove = new List<int>(); remove.Add(pair.Key); }
        if (remove != null) for (int i = 0; i < remove.Count; i++) Clocks.Remove(remove[i]);
    }
}
