using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthProtectCrateRuntimePolicy
{
    private static bool enabled = true;
    public static bool Enabled { get { return enabled; } }
    public static void SetEnabled(bool value)
    {
        if (enabled == value) return;
        enabled = value;
        RebirthProtectCrateService.OnPolicyChanged(value);
    }
}

internal sealed class RebirthProtectedCrateState
{
    public int CrateEntityId;
    public bool Triggered;
    public int TriggerPlayerEntityId = -1;
    public int TriggerGameStage;
    public int CrateMaxHealth;
    public bool RuntimeMarkersSynchronized;
    public int PlacementFailureCount;
    public float NextPlacementAttemptRealtime;
    public readonly List<int> ZombieEntityIds = new List<int>();
}

internal sealed class RebirthProtectedCrateDefenderState
{
    public int ZombieEntityId;
    public int CrateEntityId;
    public EntityAlive Entity;
    public bool RestoreObjectiveOnNextUpdate;
    public bool RuntimeMarkerSynchronized;
}

internal sealed class RebirthPendingProtectedCrateDefender
{
    public EntityAlive Entity;
    public RebirthSpawnCategory Category;
    public string EntityName;
}

public static class RebirthProtectCrateService
{
    private const float TriggerRadius = 10f;
    private const float SpawnRadius = 15f;
    private const float UpdateInterval = 1f;
    private const int CrateTargetTicks = 1200;
    private const int BaseCrateHealth = 5000;
    private const int BaselineAttackerCount = 8;
    private const string TriggeredCVar = "$RebirthProtectCrateTriggered";
    private const string SpawnsGeneratedCVar = "$RebirthProtectCrateSpawnsGenerated";
    private const string ZombieCrateCVar = "$RebirthProtectCrateTarget";
    internal const string HealthMaxCVar = "$RebirthProtectCrateHealthMax";

    private static readonly Dictionary<int, RebirthProtectedCrateState> crates = new Dictionary<int, RebirthProtectedCrateState>();
    private static readonly Dictionary<int, RebirthProtectedCrateDefenderState> defenders = new Dictionary<int, RebirthProtectedCrateDefenderState>();
    private static World activeWorld;
    private static float nextUpdate;
    private static float nextRecoveryScan;
    private static int rapidRecoveryScansRemaining;
    private static int recoveryScanCursor;
    private const int RecoveryEntitiesPerUpdate = 128;

    [ThreadStatic]
    private static bool assigningProtectedTarget;

    public static void Register(EntitySupplyCrate crate)
    {
        if (crate == null || crate.entityId < 0) return;

        RebirthProtectedCrateState state;
        if (!crates.TryGetValue(crate.entityId, out state))
        {
            state = new RebirthProtectedCrateState { CrateEntityId = crate.entityId };
            crates[crate.entityId] = state;
        }

        bool runtimeGenerated = false;
        try
        {
            runtimeGenerated = crate.Buffs.GetCustomVar(TriggeredCVar) > 0f
                || crate.Buffs.GetCustomVar(SpawnsGeneratedCVar) > 0f;
        }
        catch { }

        bool persistedGenerated = RebirthProtectCratePersistence.TryRestoreCrateState(crate.entityId, state);
        state.Triggered = state.Triggered || runtimeGenerated || persistedGenerated;
        int persistedMaxHealth = state.CrateMaxHealth > 0
            ? state.CrateMaxHealth
            : GetPersistedCrateMaxHealth(crate);
        if (persistedMaxHealth > 0)
        {
            state.CrateMaxHealth = persistedMaxHealth;
            ApplyCrateMaxHealth(crate, persistedMaxHealth, false, !state.RuntimeMarkersSynchronized);
        }

        if (state.Triggered && crate.world != null && !crate.world.IsRemote())
        {
            RebirthProtectCratePersistence.RecordGenerated(state);
        }

        if (state.Triggered && !state.RuntimeMarkersSynchronized)
        {
            SetCrateGeneratedMarkers(crate, true, true);
            state.RuntimeMarkersSynchronized = true;
        }
    }

    public static void UnregisterCrate(int entityId)
    {
        crates.Remove(entityId);
    }

    public static void RegisterLoadedDefender(EntityAlive zombie)
    {
        if (zombie == null
            || zombie.entityId < 0
            || zombie.world == null
            || zombie.world.IsRemote()
            || !RebirthProtectCrateRuntimePolicy.Enabled)
        {
            return;
        }

        int crateEntityId = GetPersistedCrateEntityId(zombie);
        if (crateEntityId <= 0) return;

        RebirthProtectCrateTrace.Emit(
            "RegisterLoadedDefender zombie=" + zombie.entityId
            + " persistedCrate=" + crateEntityId
            + " " + RebirthProtectCrateTrace.DescribeRole());

        RegisterDefender(zombie, crateEntityId, true);
    }

    public static void UnregisterDefender(int entityId)
    {
        defenders.Remove(entityId);
    }

    public static void Reset()
    {
        crates.Clear();
        defenders.Clear();
        activeWorld = null;
        nextUpdate = 0f;
        nextRecoveryScan = 0f;
        rapidRecoveryScansRemaining = 0;
        recoveryScanCursor = 0;
        assigningProtectedTarget = false;
    }

    public static void OnPolicyChanged(bool isEnabled)
    {
        if (isEnabled)
        {
            // Persistent defender identity survives the disabled interval. Rebuild
            // only transient runtime ownership from live entities on re-enable.
            nextRecoveryScan = 0f;
            rapidRecoveryScansRemaining = 20;
            recoveryScanCursor = 0;
            nextUpdate = 0f;
            return;
        }

        List<RebirthProtectedCrateDefenderState> live =
            new List<RebirthProtectedCrateDefenderState>(defenders.Values);
        for (int i = 0; i < live.Count; i++)
        {
            EntityAlive zombie = live[i] != null ? live[i].Entity : null;
            if (zombie == null) continue;
            try
            {
                if (zombie.GetRevengeTarget() != null) zombie.SetRevengeTarget(null);
                if (zombie.HasInvestigatePosition) zombie.ClearInvestigatePosition();
                ClearAttackTarget(zombie);
            }
            catch { }
        }

        defenders.Clear();
        assigningProtectedTarget = false;
    }

    public static void Update(World world)
    {
        if (world == null || world.IsRemote() || !RebirthProtectCrateRuntimePolicy.Enabled) return;

        float now = Time.realtimeSinceStartup;
        if (!ReferenceEquals(activeWorld, world))
        {
            activeWorld = world;
            crates.Clear();
            defenders.Clear();
            nextUpdate = 0f;
            nextRecoveryScan = 0f;
            rapidRecoveryScansRemaining = 20;
            recoveryScanCursor = 0;
        }

        if (now >= nextRecoveryScan)
        {
            bool forceObjectiveRestore = rapidRecoveryScansRemaining > 0;
            bool completedRecoveryCycle = ScanLoadedEntities(
                world,
                forceObjectiveRestore,
                RecoveryEntitiesPerUpdate);
            if (!completedRecoveryCycle)
            {
                // Continue on the next game update, but never inspect more than
                // RecoveryEntitiesPerUpdate entities in one callback.
                nextRecoveryScan = now;
            }
            else if (rapidRecoveryScansRemaining > 0)
            {
                rapidRecoveryScansRemaining--;
                nextRecoveryScan = now + 0.5f;
            }
            else
            {
                nextRecoveryScan = now + 10f;
            }
        }

        RebirthProtectCratePersistence.SaveIfDue(world);

        if (now < nextUpdate) return;
        nextUpdate = now + UpdateInterval;

        List<int> ids = new List<int>(crates.Keys);
        List<EntityPlayer> players = world.GetPlayers();
        for (int i = 0; i < ids.Count; i++)
        {
            EntitySupplyCrate crate = world.GetEntity(ids[i]) as EntitySupplyCrate;
            if (crate == null || crate.IsDead())
            {
                crates.Remove(ids[i]);
                continue;
            }

            RebirthProtectedCrateState state = crates[ids[i]];
            if (state.Triggered)
            {
                MaintainTrackedDefenders(world, state);
                continue;
            }

            if (!crate.onGround) continue;
            
            if (crate.bag != null && crate.bag.Touched) continue;

            EntityPlayer trigger = FindTriggerPlayer(players, crate.position);
            if (trigger == null) continue;

            Trigger(world, crate, state, trigger);
        }
    }

    public static void MaintainDefenderObjective(EntityAlive zombie)
    {
        if (zombie == null || zombie.world == null || !RebirthProtectCrateRuntimePolicy.Enabled) return;
        if (zombie.world.IsRemote())
        {
            MaintainRemoteDefenderObjective(zombie);
            return;
        }

        RebirthProtectedCrateDefenderState markedState;
        if (!TryGetMarkedDefender(zombie, out markedState)) return;

        // A protected defender never retaliates against a player, adopts an
        // investigate position, or accepts any objective other than its crate.
        if (zombie.GetRevengeTarget() != null)
        {
            zombie.SetRevengeTarget(null);
        }
        if (zombie.HasInvestigatePosition)
        {
            zombie.ClearInvestigatePosition();
        }

        RebirthProtectedCrateDefenderState activeState;
        EntitySupplyCrate crate;
        if (!TryGetActiveDefender(zombie, out activeState, out crate))
        {
            ClearAttackTarget(zombie);
            return;
        }

        EnsureTarget(zombie, crate, CrateTargetTicks);
    }

    public static void OnDefenderDamaged(EntityAlive zombie, DamageResponse damageResponse)
    {
        RebirthProtectedCrateDefenderState state;
        if (!TryGetMarkedDefender(zombie, out state)) return;

        // EntityAlive.ProcessDamageResponseLocal assigns the attacker to
        // revengeEntity in 3.1. Remove that assignment in the same damage pass.
        zombie.SetRevengeTarget(null);
        MaintainDefenderObjective(zombie);
    }

    public static bool AllowAttackTargetChange(EntityAlive zombie, EntityAlive requestedTarget)
    {
        if (assigningProtectedTarget) return true;
        if (zombie == null || zombie.world == null || zombie.world.IsRemote()) return true;
        if (!RebirthProtectCrateRuntimePolicy.Enabled) return true;

        RebirthProtectedCrateDefenderState state;
        if (!TryGetMarkedDefender(zombie, out state)) return true;

        if (zombie.GetRevengeTarget() != null)
        {
            zombie.SetRevengeTarget(null);
        }
        if (zombie.HasInvestigatePosition)
        {
            zombie.ClearInvestigatePosition();
        }

        EntitySupplyCrate crate = zombie.world.GetEntity(state.CrateEntityId) as EntitySupplyCrate;
        if (crate != null && !crate.IsDead())
        {
            if (requestedTarget == crate) return true;

            // Reject players, corpses, investigate targets, and every other
            // selector that eventually calls SetAttackTarget.
            EnsureTarget(zombie, crate, CrateTargetTicks);
        }
        else
        {
            state.RestoreObjectiveOnNextUpdate = true;
            ClearAttackTarget(zombie);
        }

        return false;
    }

    public static bool TryGetCrateObjective(EntityAlive zombie, out EntitySupplyCrate crate)
    {
        RebirthProtectedCrateDefenderState state;
        return TryGetActiveDefender(zombie, out state, out crate);
    }

    public static bool IsProtectedDefender(EntityAlive zombie)
    {
        RebirthProtectedCrateDefenderState state;
        return TryGetMarkedDefender(zombie, out state);
    }

    public static bool HasDefenderMarker(EntityAlive zombie)
    {
        return GetPersistedCrateEntityId(zombie) > 0;
    }

    public static void ConstrainClientAttackTarget(EntityAlive zombie, ref EntityAlive requestedTarget)
    {
        if (zombie == null || zombie.world == null || !zombie.world.IsRemote()) return;
        if (!RebirthProtectCrateRuntimePolicy.Enabled) return;

        int crateEntityId = GetPersistedCrateEntityId(zombie);
        if (crateEntityId <= 0) return;

        EntitySupplyCrate crate = zombie.world.GetEntity(crateEntityId) as EntitySupplyCrate;
        requestedTarget = crate != null && !crate.IsDead() ? crate : null;
    }

    private static void MaintainRemoteDefenderObjective(EntityAlive zombie)
    {
        int crateEntityId = GetPersistedCrateEntityId(zombie);
        if (crateEntityId <= 0) return;

        EntitySupplyCrate crate = zombie.world.GetEntity(crateEntityId) as EntitySupplyCrate;
        EntityAlive requestedTarget = crate != null && !crate.IsDead() ? crate : null;
        if (zombie.GetAttackTargetLocal() != requestedTarget)
        {
            zombie.SetAttackTargetClient(requestedTarget);
        }
    }

    private static EntityPlayer FindTriggerPlayer(List<EntityPlayer> players, Vector3 position)
    {
        EntityPlayer best = null;
        float bestSq = TriggerRadius * TriggerRadius;
        if (players == null) return null;

        for (int i = 0; i < players.Count; i++)
        {
            EntityPlayer player = players[i];
            if (player == null || player.IsDead() || !player.IsSpawned() || player.AttachedToEntity != null) continue;

            float sq = (player.position - position).sqrMagnitude;
            if (sq <= bestSq)
            {
                bestSq = sq;
                best = player;
            }
        }

        return best;
    }

    private static void Trigger(World world, EntitySupplyCrate crate, RebirthProtectedCrateState state, EntityPlayer player)
    {
        float now = Time.realtimeSinceStartup;
        if (now < state.NextPlacementAttemptRealtime)
            return;

        string persistenceReason;
        if (!RebirthProtectCratePersistence.CanGenerate(out persistenceReason))
        {
            state.NextPlacementAttemptRealtime = now + 10f;
            Log.Warning("[REBIRTH ProtectCrate] generation blocked crate=" + crate.entityId + " reason=" + persistenceReason);
            return;
        }

        int triggerGameStage = Math.Max(1, player.gameStage);
        int requestedCount = ResolveCount(triggerGameStage);
        System.Random rng = new System.Random(unchecked(world.Seed * 397 ^ crate.entityId * 31 ^ triggerGameStage));
        List<Vector3> positions = new List<Vector3>();
        if (!TryBuildPlacement(world, crate.position, requestedCount, rng, positions))
        {
            RecordPlacementFailure(state, now);
            Log.Warning("[REBIRTH ProtectCrate] Could not place defence group for crate " + crate.entityId +
                " retryIn=" + Math.Max(0f, state.NextPlacementAttemptRealtime - now).ToString("0.0", CultureInfo.InvariantCulture) + "s");
            return;
        }
        ClearPlacementFailure(state);

        string biome = RebirthBossEventIdentity.GetBiome(world, crate.position);
        RebirthSpawnProgressionMode progressionMode = RebirthSpawnCompositionRuntimeIntegration.SelectedProgressionMode();
        List<RebirthPendingProtectedCrateDefender> pending = new List<RebirthPendingProtectedCrateDefender>(requestedCount);

        for (int i = 0; i < requestedCount; i++)
        {
            RebirthPendingProtectedCrateDefender selected = null;
            for (int selectionAttempt = 0; selectionAttempt < 10 && selected == null; selectionAttempt++)
            {
                RebirthSpawnTrace trace;
                RebirthSpawnContext context = new RebirthSpawnContext { Surface = RebirthSpawnSurface.Biome, ProgressionMode = progressionMode, GameStage = triggerGameStage, Biome = biome, RequestedGroup = "ProtectCrateZombies", HistoryKey = "protectcrate:" + crate.entityId };

                if (!RebirthSpawnCompositionService.TrySelect(context, rng.NextDouble, out trace) || trace == null) continue;
                if (EntityClass.GetEntityClass(trace.EntityClassId) == null) continue;
                if (!IsZombieName(trace.EntityName)) continue;

                EntityAlive zombie = EntityFactory.CreateEntity(trace.EntityClassId, positions[i]) as EntityAlive;
                if (zombie == null) continue;

                selected = new RebirthPendingProtectedCrateDefender
                {
                    Entity = zombie,
                    Category = trace.Category,
                    EntityName = trace.EntityName
                };
            }

            if (selected != null) pending.Add(selected);
        }

        if (pending.Count == 0)
        {
            Log.Warning("[REBIRTH ProtectCrate] No eligible defenders selected for crate " + crate.entityId + " mode=" + progressionMode + " biome=" + biome);
            return;
        }

        float progressionFactor = progressionMode == RebirthSpawnProgressionMode.Biome
            ? GetBiomeThreatFactor(biome)
            : GetAverageSelectedThreatFactor(pending);
        int crateMaxHealth = CalculateCrateMaxHealth(progressionFactor, pending.Count);

        string reservationFailure;
        if (!RebirthProtectCratePersistence.TryReserveGenerated(
                crate.entityId,
                player.entityId,
                triggerGameStage,
                crateMaxHealth,
                out reservationFailure))
        {
            state.NextPlacementAttemptRealtime = Time.realtimeSinceStartup + 5f;
            Log.Warning("[REBIRTH ProtectCrate] generation reservation failed crate=" +
                crate.entityId + " reason=" + reservationFailure);
            return;
        }

        // Only an acknowledged durable reservation is allowed to publish native
        // crate markers/health or spawn the defender group.
        state.Triggered = true;
        state.TriggerPlayerEntityId = player.entityId;
        state.TriggerGameStage = triggerGameStage;
        state.CrateMaxHealth = crateMaxHealth;
        SetCrateGeneratedMarkers(crate, true, true);
        state.RuntimeMarkersSynchronized = true;
        ApplyCrateMaxHealth(crate, crateMaxHealth, true, true);

        for (int i = 0; i < pending.Count; i++)
        {
            EntityAlive zombie = pending[i].Entity;
            try
            {
                zombie.Buffs.SetCustomVar(
                    ZombieCrateCVar,
                    crate.entityId,
                    _netSync: false,
                    _operation: CVarOperation.set,
                    _forceSendToClients: false);
            }
            catch { }
            world.SpawnEntityInWorld(zombie);
            RegisterDefender(zombie, crate.entityId, false);
            EnsureTarget(zombie, crate, CrateTargetTicks);
        }

        RebirthProtectCratePersistence.RecordGenerated(state);
        RebirthProtectCratePersistence.TrySaveNow();

        Log.Out("[REBIRTH ProtectCrate] crate=" + crate.entityId
            + " player=" + player.EntityName
            + " progression=" + progressionMode
            + " biome=" + biome
            + " gamestage=" + triggerGameStage
            + " zombies=" + state.ZombieEntityIds.Count
            + " threatFactor=" + progressionFactor.ToString("0.00", CultureInfo.InvariantCulture)
            + " crateHealth=" + crateMaxHealth);
    }

    private static void RecordPlacementFailure(RebirthProtectedCrateState state, float now)
    {
        if (state == null) return;
        state.PlacementFailureCount = Math.Min(8, state.PlacementFailureCount + 1);
        float delay = Mathf.Min(30f, Mathf.Pow(2f, state.PlacementFailureCount - 1));
        state.NextPlacementAttemptRealtime = now + delay;
    }

    private static void ClearPlacementFailure(RebirthProtectedCrateState state)
    {
        if (state == null) return;
        state.PlacementFailureCount = 0;
        state.NextPlacementAttemptRealtime = 0f;
    }

    private static int ResolveCount(int gameStage)
    {
        return Mathf.Clamp(4 + gameStage / 35, 4, 18);
    }

    private static int CalculateCrateMaxHealth(float progressionFactor, int actualAttackerCount)
    {
        float countFactor = Mathf.Max(1f, actualAttackerCount / (float)BaselineAttackerCount);
        float raw = BaseCrateHealth * Mathf.Max(1f, progressionFactor) * countFactor;
        return Mathf.Max(BaseCrateHealth, Mathf.RoundToInt(raw / 500f) * 500);
    }

    private static float GetBiomeThreatFactor(string biome)
    {
        string normalized = NormalizeBiomeName(biome);
        if (normalized.IndexOf("burnt", StringComparison.Ordinal) >= 0) return 4.0f;
        if (normalized.IndexOf("wasteland", StringComparison.Ordinal) >= 0) return 2.8f;
        if (normalized.IndexOf("snow", StringComparison.Ordinal) >= 0) return 1.8f;
        if (normalized.IndexOf("desert", StringComparison.Ordinal) >= 0) return 1.3f;
        return 1.0f;
    }

    private static string NormalizeBiomeName(string biome)
    {
        return (biome ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }

    private static float GetAverageSelectedThreatFactor(List<RebirthPendingProtectedCrateDefender> selected)
    {
        if (selected == null || selected.Count == 0) return 1f;

        float total = 0f;
        for (int i = 0; i < selected.Count; i++)
        {
            total += GetSelectedThreatFactor(selected[i].Category, selected[i].EntityName);
        }

        return Mathf.Max(1f, total / selected.Count);
    }

    private static float GetSelectedThreatFactor(RebirthSpawnCategory category, string entityName)
    {
        switch (category)
        {
            case RebirthSpawnCategory.RegularLow: return 1.0f;
            case RebirthSpawnCategory.RegularMedium: return 1.2f;
            case RebirthSpawnCategory.RegularHigh: return 1.4f;
            case RebirthSpawnCategory.FeralLow: return 1.7f;
            case RebirthSpawnCategory.FeralMedium: return 2.0f;
            case RebirthSpawnCategory.FeralHigh: return 2.3f;
            case RebirthSpawnCategory.RadiatedLow: return 2.8f;
            case RebirthSpawnCategory.RadiatedMedium: return 3.2f;
            case RebirthSpawnCategory.RadiatedHigh: return 3.6f;
            case RebirthSpawnCategory.Charged: return 4.0f;
            case RebirthSpawnCategory.Infernal: return 4.5f;
            case RebirthSpawnCategory.Special: return GetSpecialEntityThreatFactor(entityName);
            default: return 1.0f;
        }
    }

    private static float GetSpecialEntityThreatFactor(string entityName)
    {
        string name = (entityName ?? string.Empty).ToLowerInvariant();
        if (name.IndexOf("demolition", StringComparison.Ordinal) >= 0 || name.IndexOf("demolisher", StringComparison.Ordinal) >= 0) return 4.0f;
        if (name.IndexOf("radiated", StringComparison.Ordinal) >= 0) return 3.2f;
        if (name.IndexOf("mutated", StringComparison.Ordinal) >= 0) return 2.8f;
        if (name.IndexOf("wight", StringComparison.Ordinal) >= 0) return 2.5f;
        if (name.IndexOf("plague", StringComparison.Ordinal) >= 0 || name.IndexOf("frost", StringComparison.Ordinal) >= 0) return 2.4f;
        if (name.IndexOf("feral", StringComparison.Ordinal) >= 0) return 2.1f;
        if (name.IndexOf("spider", StringComparison.Ordinal) >= 0) return 1.5f;
        if (name.IndexOf("screamer", StringComparison.Ordinal) >= 0) return 1.4f;
        if (name.IndexOf("crawler", StringComparison.Ordinal) >= 0) return 1.0f;
        return 2.0f;
    }

    private static int GetPersistedCrateMaxHealth(EntitySupplyCrate crate)
    {
        if (crate == null || crate.Buffs == null) return 0;
        try { return Mathf.RoundToInt(crate.Buffs.GetCustomVar(HealthMaxCVar)); }
        catch { return 0; }
    }

    private static void ApplyCrateMaxHealth(EntitySupplyCrate crate, int maxHealth, bool refill, bool forceCVarSync)
    {
        if (crate == null || crate.Stats == null || crate.Stats.Health == null || maxHealth <= 0) return;

        SetSyncedCVar(crate, HealthMaxCVar, maxHealth, forceCVarSync);

        crate.Stats.Health.BaseMax = maxHealth;
        if (refill)
        {
            crate.Health = maxHealth;
        }
        else if (crate.Health > maxHealth)
        {
            crate.Health = maxHealth;
        }
    }

    private static bool ScanLoadedEntities(
        World world,
        bool forceObjectiveRestore,
        int maximumEntities)
    {
        if (world == null || world.Entities == null || world.Entities.list == null)
        {
            recoveryScanCursor = 0;
            return true;
        }

        List<Entity> loaded = world.Entities.list;
        if (loaded.Count == 0)
        {
            recoveryScanCursor = 0;
            return true;
        }
        if (recoveryScanCursor < 0 || recoveryScanCursor >= loaded.Count)
            recoveryScanCursor = 0;

        int processed = 0;
        while (recoveryScanCursor < loaded.Count && processed < maximumEntities)
        {
            Entity entity = loaded[recoveryScanCursor++];
            processed++;

            EntitySupplyCrate crate = entity as EntitySupplyCrate;
            if (crate != null && !crate.IsDead())
                Register(crate);

            EntityAlive zombie = entity as EntityAlive;
            if (zombie == null || zombie is EntityPlayer || zombie.IsDead())
                continue;

            int crateEntityId = GetPersistedCrateEntityId(zombie);
            if (crateEntityId <= 0)
                continue;

            RebirthProtectedCrateDefenderState state = RegisterDefender(
                zombie,
                crateEntityId,
                forceObjectiveRestore);
            if (state != null && forceObjectiveRestore)
                MaintainDefenderObjective(zombie);
        }

        if (recoveryScanCursor >= loaded.Count)
        {
            recoveryScanCursor = 0;
            return true;
        }
        return false;
    }

    private static bool IsZombieName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        return name.IndexOf("zombie", StringComparison.OrdinalIgnoreCase) >= 0
            && name.IndexOf("animal", StringComparison.OrdinalIgnoreCase) < 0
            && name.IndexOf("bear", StringComparison.OrdinalIgnoreCase) < 0
            && name.IndexOf("dog", StringComparison.OrdinalIgnoreCase) < 0
            && name.IndexOf("vulture", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static void MaintainTrackedDefenders(World world, RebirthProtectedCrateState state)
    {
        for (int i = state.ZombieEntityIds.Count - 1; i >= 0; i--)
        {
            EntityAlive zombie = world.GetEntity(state.ZombieEntityIds[i]) as EntityAlive;
            if (zombie == null || zombie.IsDead())
            {
                defenders.Remove(state.ZombieEntityIds[i]);
                state.ZombieEntityIds.RemoveAt(i);
                continue;
            }

            MaintainDefenderObjective(zombie);
        }
    }

    private static RebirthProtectedCrateDefenderState RegisterDefender(EntityAlive zombie, int crateEntityId, bool restoreObjective)
    {
        if (zombie == null || zombie.entityId < 0 || crateEntityId <= 0) return null;

        RebirthProtectedCrateDefenderState state;
        if (!defenders.TryGetValue(zombie.entityId, out state) ||
            state == null ||
            !ReferenceEquals(state.Entity, zombie))
        {
            state = new RebirthProtectedCrateDefenderState
            {
                ZombieEntityId = zombie.entityId,
                Entity = zombie
            };
            defenders[zombie.entityId] = state;
        }

        state.CrateEntityId = crateEntityId;
        if (restoreObjective)
        {
            state.RestoreObjectiveOnNextUpdate = true;
        }

        bool forceMarkerSend = restoreObjective || !state.RuntimeMarkerSynchronized;
        if (!RebirthProtectCratePersistence.RecordDefender(crateEntityId, zombie.entityId))
        {
            defenders.Remove(zombie.entityId);
            return null;
        }

        SetSyncedCVar(zombie, ZombieCrateCVar, crateEntityId, forceMarkerSend);
        state.RuntimeMarkerSynchronized = true;

        RebirthProtectedCrateState crateState;
        if (crates.TryGetValue(crateEntityId, out crateState) && !crateState.ZombieEntityIds.Contains(zombie.entityId))
        {
            crateState.ZombieEntityIds.Add(zombie.entityId);
        }

        return state;
    }

    private static bool TryGetMarkedDefender(EntityAlive zombie, out RebirthProtectedCrateDefenderState state)
    {
        state = null;
        if (zombie == null || zombie.IsDead() || zombie.world == null || zombie.world.IsRemote()) return false;
        if (!RebirthProtectCrateRuntimePolicy.Enabled) return false;

        if (defenders.TryGetValue(zombie.entityId, out state) &&
            state != null &&
            ReferenceEquals(state.Entity, zombie) &&
            state.CrateEntityId > 0)
        {
            return true;
        }

        int persistedCrateEntityId = GetPersistedCrateEntityId(zombie);
        if (persistedCrateEntityId <= 0)
        {
            defenders.Remove(zombie.entityId);
            return false;
        }

        state = RegisterDefender(zombie, persistedCrateEntityId, true);
        return state != null;
    }

    private static bool TryGetActiveDefender(EntityAlive zombie, out RebirthProtectedCrateDefenderState state, out EntitySupplyCrate crate)
    {
        crate = null;
        if (!TryGetMarkedDefender(zombie, out state)) return false;

        crate = zombie.world.GetEntity(state.CrateEntityId) as EntitySupplyCrate;
        if (crate == null)
        {
            state.RestoreObjectiveOnNextUpdate = true;
            return false;
        }

        if (crate.IsDead())
        {
            ReleaseDefender(zombie);
            crate = null;
            state = null;
            return false;
        }

        Register(crate);
        RebirthProtectedCrateState activeCrateState;
        if (crates.TryGetValue(crate.entityId, out activeCrateState)
            && !activeCrateState.ZombieEntityIds.Contains(zombie.entityId))
        {
            activeCrateState.ZombieEntityIds.Add(zombie.entityId);
        }

        if (state.RestoreObjectiveOnNextUpdate)
        {
            if (zombie.GetRevengeTarget() != null)
            {
                zombie.SetRevengeTarget(null);
            }

            ClearAttackTarget(zombie);
            EnsureTarget(zombie, crate, CrateTargetTicks);
            state.RestoreObjectiveOnNextUpdate = false;
        }

        return true;
    }

    private static int GetPersistedCrateEntityId(EntityAlive zombie)
    {
        if (zombie == null) return -1;

        if (zombie.world != null && !zombie.world.IsRemote())
        {
            int ledgerCrateId = RebirthProtectCratePersistence.GetCrateForDefender(zombie.entityId);
            if (ledgerCrateId > 0) return ledgerCrateId;
        }

        try
        {
            int cvarId = Mathf.RoundToInt(zombie.Buffs.GetCustomVar(ZombieCrateCVar));
            if (cvarId > 0) return cvarId;
        }
        catch { }

        return -1;
    }

    private static void ReleaseDefender(EntityAlive zombie)
    {
        if (zombie == null) return;

        defenders.Remove(zombie.entityId);
        SetSyncedCVar(zombie, ZombieCrateCVar, 0f, true);
        RebirthProtectCratePersistence.RemoveDefender(zombie.entityId);

        if (zombie.GetRevengeTarget() != null) zombie.SetRevengeTarget(null);
        ClearAttackTarget(zombie);
    }


    private static void SetCrateGeneratedMarkers(EntitySupplyCrate crate, bool generated, bool forceSendToClients)
    {
        float value = generated ? 1f : 0f;
        SetSyncedCVar(crate, TriggeredCVar, value, forceSendToClients);
        SetSyncedCVar(crate, SpawnsGeneratedCVar, value, forceSendToClients);
    }

    private static void SetSyncedCVar(EntityAlive entity, string name, float value, bool forceSendToClients)
    {
        if (entity == null || entity.Buffs == null) return;

        bool isServerEntity = entity.world != null && !entity.world.IsRemote();
        try
        {
            entity.Buffs.SetCustomVar(
                name,
                value,
                _netSync: isServerEntity,
                _operation: CVarOperation.set,
                _forceSendToClients: isServerEntity && forceSendToClients);
        }
        catch { }
    }

    private static void ClearAttackTarget(EntityAlive zombie)
    {
        if (zombie == null || zombie.GetAttackTarget() == null) return;

        try
        {
            assigningProtectedTarget = true;
            zombie.SetAttackTarget(null, 0);
        }
        finally
        {
            assigningProtectedTarget = false;
        }
    }

    private static void EnsureTarget(EntityAlive zombie, EntityAlive target, int targetTicks)
    {
        if (zombie == null || target == null) return;

        try
        {
            assigningProtectedTarget = true;
            zombie.SetAttackTarget(target, targetTicks);
        }
        finally
        {
            assigningProtectedTarget = false;
        }
    }

    private static bool TryBuildPlacement(World world, Vector3 origin, int count, System.Random rng, List<Vector3> output)
    {
        const int MaximumBlockQueries = 2048;
        int remainingQueries = MaximumBlockQueries;
        output.Clear();
        for (int i = 0; i < count; i++)
        {
            bool found = false;
            for (int attempt = 0; attempt < 24 && !found; attempt++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                float radius = SpawnRadius;
                int x = Mathf.FloorToInt(origin.x + Mathf.Cos(angle) * radius);
                int z = Mathf.FloorToInt(origin.z + Mathf.Sin(angle) * radius);

                for (int y = Math.Min(252, Mathf.RoundToInt(origin.y) + 20); y >= Math.Max(2, Mathf.RoundToInt(origin.y) - 30); y--)
                {
                    if (remainingQueries < 3)
                    {
                        output.Clear();
                        return false;
                    }
                    remainingQueries -= 3;

                    Vector3i feet = new Vector3i(x, y, z);
                    BlockValue floor = world.GetBlock(feet - Vector3i.up);
                    BlockValue feetBlock = world.GetBlock(feet);
                    BlockValue headBlock = world.GetBlock(feet + Vector3i.up);
                    if (floor.isair || floor.Block.blockMaterial.IsLiquid || !feetBlock.isair || !headBlock.isair)
                        continue;

                    output.Add(new Vector3(x + 0.5f, y + 0.05f, z + 0.5f));
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                output.Clear();
                return false;
            }
        }

        return true;
    }

#if DEBUG
    public static EntitySupplyCrate DebugFindNearestLoadedCrate(World world, Vector3 playerPosition, float maximumDistance)
    {
        if (world == null) return null;

        EntitySupplyCrate best = null;
        float bestSq = maximumDistance * maximumDistance;
        List<Entity> loaded = world.Entities != null ? world.Entities.list : null;
        for (int i = 0; loaded != null && i < loaded.Count; i++)
        {
            EntitySupplyCrate candidate = loaded[i] as EntitySupplyCrate;
            if (candidate == null || candidate.IsDead()) continue;

            float sq = (candidate.position - playerPosition).sqrMagnitude;
            if (sq > bestSq) continue;

            bestSq = sq;
            best = candidate;
        }

        return best;
    }

    public static string DebugDescribe(World world, EntityPlayer player)
    {
        StringBuilder sb = new StringBuilder(1024);
        sb.AppendLine("[REBIRTH ProtectCrate Debug] service");
        sb.Append("  optionEnabled=").Append(RebirthProtectCrateRuntimePolicy.Enabled)
            .Append(" world=").Append(world != null)
            .Append(" remote=").Append(world != null && world.IsRemote())
            .Append(" registeredCrates=").Append(crates.Count)
            .Append(" registeredDefenders=").Append(defenders.Count)
            .AppendLine();

        if (world == null)
        {
            sb.AppendLine("  no active world");
            return sb.ToString();
        }

        EntitySupplyCrate nearest = DebugFindNearestLoadedCrate(
            world,
            player != null ? player.position : Vector3.zero,
            player != null ? 160f : float.MaxValue);
        if (nearest == null)
        {
            sb.AppendLine("  nearestSupplyCrate=<none loaded>");
            return sb.ToString();
        }

        RebirthProtectedCrateState crateState;
        bool tracked = crates.TryGetValue(nearest.entityId, out crateState);
        int persistedMax = GetPersistedCrateMaxHealth(nearest);
        float triggered = 0f;
        float spawnsGenerated = 0f;
        try
        {
            triggered = nearest.Buffs.GetCustomVar(TriggeredCVar);
            spawnsGenerated = nearest.Buffs.GetCustomVar(SpawnsGeneratedCVar);
        }
        catch { }

        sb.Append("  nearestSupplyCrate id=").Append(nearest.entityId)
            .Append(" distance=").Append(player != null ? Vector3.Distance(player.position, nearest.position).ToString("0.0", CultureInfo.InvariantCulture) : "n/a")
            .Append(" onGround=").Append(nearest.onGround)
            .Append(" dead=").Append(nearest.IsDead())
            .Append(" touched=").Append(nearest.bag != null && nearest.bag.Touched)
            .Append(" health=").Append(nearest.Health.ToString("0", CultureInfo.InvariantCulture))
            .Append(" max=").Append(nearest.GetMaxHealth())
            .Append(" cvarTriggered=").Append(triggered.ToString("0", CultureInfo.InvariantCulture))
            .Append(" cvarSpawnsGenerated=").Append(spawnsGenerated.ToString("0", CultureInfo.InvariantCulture))
            .Append(" cvarHealthMax=").Append(persistedMax)
            .Append(" tracked=").Append(tracked)
            .Append(" trackedTriggered=").Append(tracked && crateState != null && crateState.Triggered)
            .AppendLine();

        return sb.ToString();
    }

    public static string DebugDescribeTargets(World world)
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[REBIRTH ProtectCrate Debug] defender targets");
        if (world == null)
        {
            sb.AppendLine("  no active world");
            return sb.ToString();
        }

        int marked = 0;
        List<Entity> loaded = world.Entities != null ? world.Entities.list : null;
        for (int i = 0; loaded != null && i < loaded.Count; i++)
        {
            EntityAlive zombie = loaded[i] as EntityAlive;
            if (zombie == null || zombie is EntityPlayer) continue;

            int persistedCrateId = GetPersistedCrateEntityId(zombie);
            if (persistedCrateId <= 0) continue;
            marked++;

            RebirthProtectedCrateDefenderState state;
            bool registered = defenders.TryGetValue(zombie.entityId, out state);
            EntityAlive attackTarget = zombie.GetAttackTarget();
            EntityAlive revengeTarget = zombie.GetRevengeTarget();
            EntitySupplyCrate crate = world.GetEntity(persistedCrateId) as EntitySupplyCrate;

            sb.Append("  zombie=").Append(zombie.entityId)
                .Append(" class=").Append(zombie.EntityClass != null ? zombie.EntityClass.entityClassName : "<unknown>")
                .Append(" persistedCrate=").Append(persistedCrateId)
                .Append(" crateLoaded=").Append(crate != null)
                .Append(" registered=").Append(registered)
                .Append(" pendingRestore=").Append(registered && state != null && state.RestoreObjectiveOnNextUpdate)
                .Append(" attackTarget=").Append(attackTarget != null ? attackTarget.entityId.ToString(CultureInfo.InvariantCulture) : "none")
                .Append(" attackType=").Append(attackTarget != null ? attackTarget.GetType().Name : "none")
                .Append(" revengeTarget=").Append(revengeTarget != null ? revengeTarget.entityId.ToString(CultureInfo.InvariantCulture) : "none")
                .Append(" revengeTimer=").Append(zombie.revengeTimer)
                .AppendLine();
        }

        sb.Append("  markedLoadedDefenders=").Append(marked)
            .Append(" registeredDefenders=").Append(defenders.Count)
            .AppendLine();
        return sb.ToString();
    }

    public static string DebugRescanLoadedDefenders(World world)
    {
        if (world == null) return "[REBIRTH ProtectCrate Debug] rescan failed: no active world";
        if (world.IsRemote()) return "[REBIRTH ProtectCrate Debug] rescan is server-authoritative; run it on the host/server";

        int marked = 0;
        int restored = 0;
        List<Entity> loaded = world.Entities != null ? world.Entities.list : null;
        for (int i = 0; loaded != null && i < loaded.Count; i++)
        {
            EntityAlive zombie = loaded[i] as EntityAlive;
            if (zombie == null || zombie is EntityPlayer) continue;

            int crateEntityId = GetPersistedCrateEntityId(zombie);
            if (crateEntityId <= 0) continue;
            marked++;

            RegisterDefender(zombie, crateEntityId, true);
            MaintainDefenderObjective(zombie);
            restored++;
        }

        return "[REBIRTH ProtectCrate Debug] rescan marked=" + marked.ToString(CultureInfo.InvariantCulture)
            + " restored=" + restored.ToString(CultureInfo.InvariantCulture)
            + " registered=" + defenders.Count.ToString(CultureInfo.InvariantCulture);
    }
#endif
}

[HarmonyPatch(typeof(Stat), nameof(Stat.Tick))]
internal static class RebirthProtectCrateHealthStatPatch
{
    private static bool Prefix(Stat __instance)
    {
        if (__instance == null || __instance.StatType != Stat.StatTypes.Health) return true;
        EntitySupplyCrate crate = __instance.Entity as EntitySupplyCrate;
        if (crate == null || crate.Buffs == null) return true;

        float protectedMax;
        try { protectedMax = crate.Buffs.GetCustomVar(RebirthProtectCrateService.HealthMaxCVar); }
        catch { return true; }
        if (protectedMax <= 0f) return true;

        __instance.BaseMax = protectedMax;
        if (__instance.Value > protectedMax) __instance.Value = protectedMax;
        return false;
    }
}

public static class RebirthProtectCrateNativeTargetBar
{
    private sealed class TargetState
    {
        internal bool ForcedPreviousFrame;
        internal World World;
        internal Transform Hit, Parent;
        internal EntitySupplyCrate Crate;
    }
    private static readonly ConditionalWeakTable<XUiC_TargetBar, TargetState> targetStates =
        new ConditionalWeakTable<XUiC_TargetBar, TargetState>();

    public static bool ShouldForceNativeTargetBar(XUiC_TargetBar targetBar)
    {
        if (!RebirthProtectCrateRuntimePolicy.Enabled) return false;

        GameManager gameManager = GameManager.Instance;
        World world = gameManager != null ? gameManager.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        WorldRayHitInfo hitInfo = player != null ? player.HitInfo : null;
        if (hitInfo == null || !hitInfo.bHitValid || hitInfo.transform == null) return false;

        EntitySupplyCrate crate;
        if (targetBar == null)
            crate = hitInfo.transform.GetComponentInParent<EntitySupplyCrate>();
        else
        {
            TargetState state = targetStates.GetValue(targetBar, _ => new TargetState());
            Transform hit = hitInfo.transform;
            if (!ReferenceEquals(state.World, world) || state.Hit != hit || state.Parent != hit.parent ||
                state.Crate == null || !hit.IsChildOf(state.Crate.transform))
            {
                state.World = world;
                state.Hit = hit;
                state.Parent = hit.parent;
                state.Crate = hit.GetComponentInParent<EntitySupplyCrate>();
            }
            crate = state.Crate;
        }
        // Only ancestry is cached. Dead/ground/touched state is authoritative every call.
        if (crate == null || crate.IsDead() || !crate.onGround) return false;
        return crate.bag != null && !crate.bag.Touched;
    }

    public static void Prefix(XUiC_TargetBar targetBar)
    {
        if (targetBar == null) return;

        // Undo only the exception applied by the preceding frame. Setting the
        // timer to zero makes the base controller reload the real sandbox option
        // in this frame before it can display any non-crate target.
        TargetState state = targetStates.GetValue(targetBar, _ => new TargetState());
        if (state.ForcedPreviousFrame)
        {
            targetBar.checkSandboxOption = 0f;
            state.ForcedPreviousFrame = false;
        }

        if (!ShouldForceNativeTargetBar(targetBar)) return;

        targetBar.sandboxForceShow = true;
        targetBar.checkSandboxOption = 3f;
        state.ForcedPreviousFrame = true;
    }

#if DEBUG
    public static string DebugDescribe()
    {
        return "[REBIRTH ProtectCrate Debug] nativeTargetBarForce="
            + ShouldForceNativeTargetBar(null);
    }
#endif
}

[HarmonyPatch(typeof(XUiC_TargetBar), nameof(XUiC_TargetBar.Update))]
internal static class RebirthProtectCrateTargetBarPatch
{
    private static void Prefix(XUiC_TargetBar __instance)
    {
        RebirthProtectCrateNativeTargetBar.Prefix(__instance);
    }
}

[HarmonyPatch(typeof(EntitySupplyCrate), nameof(EntitySupplyCrate.PostInit))]
internal static class RebirthProtectCratePostInitPatch
{
    private static void Postfix(EntitySupplyCrate __instance)
    {
        RebirthProtectCrateService.Register(__instance);
    }
}

[HarmonyPatch(typeof(EntitySupplyCrate), nameof(EntitySupplyCrate.OnEntityUnload))]
internal static class RebirthProtectCrateUnloadPatch
{
    private static void Prefix(EntitySupplyCrate __instance)
    {
        if (__instance != null) RebirthProtectCrateService.UnregisterCrate(__instance.entityId);
    }
}

[HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.Read))]
internal static class RebirthProtectCrateDefenderBuffReadPatch
{
    private static void Postfix(EntityBuffs __instance)
    {
        if (__instance != null)
        {
            RebirthProtectCrateService.RegisterLoadedDefender(__instance.parent);
        }
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.PostInit))]
internal static class RebirthProtectCrateDefenderPostInitPatch
{
    private static void Postfix(EntityAlive __instance)
    {
        RebirthProtectCrateService.RegisterLoadedDefender(__instance);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnAddedToWorld))]
internal static class RebirthProtectCrateDefenderAddedToWorldPatch
{
    private static void Postfix(EntityAlive __instance)
    {
        RebirthProtectCrateService.RegisterLoadedDefender(__instance);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnEntityUnload))]
internal static class RebirthProtectCrateDefenderUnloadPatch
{
    private static void Prefix(EntityAlive __instance)
    {
        if (__instance != null) RebirthProtectCrateService.UnregisterDefender(__instance.entityId);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnUpdateLive))]
internal static class RebirthProtectCrateDefenderLiveUpdatePatch
{
    private static void Postfix(EntityAlive __instance)
    {
        RebirthProtectCrateService.MaintainDefenderObjective(__instance);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.ProcessDamageResponseLocal))]
internal static class RebirthProtectCrateDefenderDamagePatch
{
    private static void Postfix(EntityAlive __instance, DamageResponse _dmResponse)
    {
        if (__instance == null || __instance.world == null || __instance.world.IsRemote()) return;
        RebirthProtectCrateService.OnDefenderDamaged(__instance, _dmResponse);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetAttackTarget))]
internal static class RebirthProtectCrateAttackTargetPolicyPatch
{
    private static bool Prefix(EntityAlive __instance, EntityAlive _attackTarget)
    {
        return RebirthProtectCrateService.AllowAttackTargetChange(__instance, _attackTarget);
    }
}


[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetAttackTargetClient))]
internal static class RebirthProtectCrateClientAttackTargetPolicyPatch
{
    private static void Prefix(EntityAlive __instance, ref EntityAlive _attackTarget)
    {
        RebirthProtectCrateService.ConstrainClientAttackTarget(__instance, ref _attackTarget);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetRevengeTarget))]
internal static class RebirthProtectCrateRevengeTargetPolicyPatch
{
    private static void Prefix(EntityAlive __instance, ref EntityAlive _other)
    {
        if (_other != null && RebirthProtectCrateService.HasDefenderMarker(__instance))
        {
            _other = null;
        }
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetInvestigatePosition))]
internal static class RebirthProtectCrateInvestigatePositionPolicyPatch
{
    private static bool Prefix(EntityAlive __instance)
    {
        return !RebirthProtectCrateService.HasDefenderMarker(__instance);
    }
}

[HarmonyPatch(typeof(EAIApproachAndAttackTarget), nameof(EAIApproachAndAttackTarget.CanExecute))]
internal static class RebirthProtectCrateApproachTaskPatch
{
    private static bool Prefix(EAIApproachAndAttackTarget __instance, ref bool __result)
    {
        if (__instance == null || __instance.theEntity == null) return true;

        EntitySupplyCrate crate;
        if (!RebirthProtectCrateService.TryGetCrateObjective(__instance.theEntity, out crate)) return true;
        if (__instance.theEntity.GetAttackTarget() != crate) return true;

        if (__instance.theEntity.sleepingOrWakingUp
            || __instance.theEntity.bodyDamage.CurrentStun != EnumEntityStunType.None
            || (__instance.theEntity.Jumping && !__instance.theEntity.isSwimming))
        {
            __result = false;
            return false;
        }

        __instance.entityTarget = crate;
        __instance.chaseTimeMax = 0f;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(EAISetAsTargetIfHurt), nameof(EAISetAsTargetIfHurt.CanExecute))]
internal static class RebirthProtectCrateHurtTargetPatch
{
    private static bool Prefix(EAISetAsTargetIfHurt __instance, ref bool __result)
    {
        if (__instance == null || __instance.theEntity == null) return true;
        if (!RebirthProtectCrateService.IsProtectedDefender(__instance.theEntity)) return true;

        __instance.theEntity.SetRevengeTarget(null);
        RebirthProtectCrateService.MaintainDefenderObjective(__instance.theEntity);
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(EAISetNearestEntityAsTarget), nameof(EAISetNearestEntityAsTarget.CanExecute))]
internal static class RebirthProtectCrateNearestTargetCanExecutePatch
{
    private static bool Prefix(EAISetNearestEntityAsTarget __instance, ref bool __result)
    {
        if (__instance == null || !RebirthProtectCrateService.IsProtectedDefender(__instance.theEntity)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(EAISetNearestEntityAsTarget), nameof(EAISetNearestEntityAsTarget.Continue))]
internal static class RebirthProtectCrateNearestTargetContinuePatch
{
    private static bool Prefix(EAISetNearestEntityAsTarget __instance, ref bool __result)
    {
        if (__instance == null || !RebirthProtectCrateService.IsProtectedDefender(__instance.theEntity)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_BagStorageWindowGroup), nameof(XUiC_BagStorageWindowGroup.Open))]
internal static class RebirthProtectCrateLootOpenPatch
{
    private struct LootOpenOverrideState
    {
        public bool Applied;
        public float OriginalOpenTime;
    }

    private static void Prefix(
        Entity _entity,
        LootContainer _lootContainer,
        bool _runOpenTimer,
        ref LootOpenOverrideState __state)
    {
        __state = default(LootOpenOverrideState);
        if (!RebirthProtectCrateRuntimePolicy.Enabled || !_runOpenTimer || _lootContainer == null) return;

        EntitySupplyCrate crate = _entity as EntitySupplyCrate;
        if (crate == null || !crate.onGround || (crate.bag != null && crate.bag.Touched)) return;

        __state.Applied = true;
        __state.OriginalOpenTime = _lootContainer.openTime;
        _lootContainer.openTime = 20f;
    }

    private static void Postfix(LootContainer _lootContainer, ref LootOpenOverrideState __state)
    {
        Restore(_lootContainer, ref __state);
    }

    private static Exception Finalizer(
        LootContainer _lootContainer,
        Exception __exception,
        ref LootOpenOverrideState __state)
    {
        Restore(_lootContainer, ref __state);
        return __exception;
    }

    private static void Restore(LootContainer lootContainer, ref LootOpenOverrideState state)
    {
        if (!state.Applied || lootContainer == null) return;
        lootContainer.openTime = state.OriginalOpenTime;
        state.Applied = false;
    }
}

[Preserve]
public sealed class RebirthProtectCrateModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("rebirth.protectcrate");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateHealthStatPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateTargetBarPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCratePostInitPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateUnloadPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateDefenderBuffReadPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateDefenderPostInitPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateDefenderAddedToWorldPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateDefenderUnloadPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateDefenderLiveUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateDefenderDamagePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateAttackTargetPolicyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateClientAttackTargetPolicyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateRevengeTargetPolicyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateInvestigatePositionPolicyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateApproachTaskPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateHurtTargetPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateNearestTargetCanExecutePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateNearestTargetContinuePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthProtectCrateLootOpenPatch));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnStart));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShutdown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnShutdown));
    }

    private static void OnStart(ref ModEvents.SGameStartingData data)
    {
        if (data.AsServer) RebirthProtectCratePersistence.Load();
    }

    private static void OnUpdate(ref ModEvents.SGameUpdateData data)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        RebirthProtectCrateService.Update(world);
    }

    private static void OnWorldShutdown(ref ModEvents.SWorldShuttingDownData data)
    {
        RebirthProtectCratePersistence.TrySaveNow();
    }

    private static void OnShutdown(ref ModEvents.SGameShutdownData data)
    {
        RebirthProtectCrateService.Reset();
        RebirthProtectCratePersistence.Reset();
    }
}
