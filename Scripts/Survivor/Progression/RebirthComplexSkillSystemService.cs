using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// Chunk 10 owner for source-audited complex Skill adapters.
/// Enabled here: Explosives native blast performance + block LBD, Deployable-Turret owner LBD
/// through the existing combat router, and Drone Operations owner gun/shock behavior.
/// Construction native repair/upgrade completion and Electrical service lifecycle are now live;
/// Chemistry payload-potency metadata remains gated.
/// </summary>
public static class RebirthComplexSkillSystemService
{
    public const string PassiveBuff = "RebirthSurvivorComplexSkillPassives";
    public const string ExplosivesDamageCVar = "$rbSurvivorSkillExplosivesDamage";
    public const string TurretDamageCVar = "$rbSurvivorSkillTurretDamage";
    public const string DroneDamageCVar = "$rbSurvivorSkillDroneDamage";

    public const bool ConstructionWorkActionEnabled = true;
    public const bool ElectricalWorkmanshipEnabled = true;
    public const bool ChemistryPayloadPotencyEnabled = false;
    public const bool DeployableTurretPerformanceScalingEnabled = true;
    // Pass 5M safety contract: no native reload/feed/jam hook has been verified for EntityTurret.
    // Keep the authored procedure learnable, but never simulate hidden jam state or client-local reliability.
    public const bool DeployableTurretFeedReliabilityEnabled = false;

    private static readonly FieldInfo DroneEntityField = AccessTools.Field(typeof(DroneWeapons.Weapon), "entity");
    private static readonly FieldInfo DroneCooldownTimerField = AccessTools.Field(typeof(DroneWeapons.Weapon), "cooldownTimer");
    private static bool installed;
    private static float nextPassiveSync;

    public static string Install()
    {
        if (installed) return "[REBIRTH Survivor Complex Skills] already installed";
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed = true;
        return "[REBIRTH Survivor Complex Skills] installed live=explosives,turret-owner-lbd,turret-skill-scaling,drone,construction-commit,electrical-service blocked=chemistry-payload,turret-feed-reliability";
    }

    public static float SignedEndpoint(float skillValue, float negativeAtMinus50, float positiveAt100)
    {
        float v = Mathf.Clamp(skillValue, -50f, 100f);
        if (v < 0f) return negativeAtMinus50 * (-v / 50f);
        if (v > 0f) return positiveAt100 * (v / 100f);
        return 0f;
    }

    public static void SyncNativePassiveEffects(EntityPlayer player)
    {
        if (player == null || player.Buffs == null) return;
        float explosivesValue, turretValue, droneValue;
        bool hasExplosives = TryGetSkillValue(player, "skill.explosives", out explosivesValue);
        bool hasTurret = TryGetSkillValue(player, "skill.deployable_turrets", out turretValue);
        bool hasDrone = TryGetSkillValue(player, "skill.drone_operations", out droneValue);
        SetCVar(player, ExplosivesDamageCVar, hasExplosives ? SignedEndpoint(explosivesValue, RebirthProgressionRuntimeConfig.ExplosivesDamageNegative, RebirthProgressionRuntimeConfig.ExplosivesDamagePositive) : 0f);

        float turretDelta = hasTurret ? SignedEndpoint(turretValue, RebirthProgressionRuntimeConfig.TurretDamageNegative, RebirthProgressionRuntimeConfig.TurretDamagePositive) : 0f;
        // Deployable Turrets Skill directly governs bounded turret damage scaling. Calibration
        // knowledge is retained for compatibility but does not suppress the core Skill benefit.
        SetCVar(player, TurretDamageCVar, turretDelta);

        SetCVar(player, DroneDamageCVar, hasDrone ? SignedEndpoint(droneValue, RebirthProgressionRuntimeConfig.DroneDamageNegative, RebirthProgressionRuntimeConfig.DroneDamagePositive) : 0f);
        if (hasExplosives || hasTurret || hasDrone)
        {
            if (!player.Buffs.HasBuff(PassiveBuff)) player.Buffs.AddBuff(PassiveBuff);
        }
        else if (player.Buffs.HasBuff(PassiveBuff)) player.Buffs.RemoveBuff(PassiveBuff);
    }

    public static void OnExplosionBlocksCompleted(Explosion explosion, int entityThatCausedExplosion, ItemValue sourceItem)
    {
        if (!IsServerAuthority() || explosion == null || sourceItem == null || sourceItem.ItemClass == null) return;
        if (explosion.ChangedBlockPositions == null || explosion.ChangedBlockPositions.Count <= 0) return;
        if (!string.Equals(RebirthProgressionRuntimeConfig.ClassifyCombat(sourceItem), "skill.explosives", StringComparison.OrdinalIgnoreCase)) return;
        EntityPlayer player = GameManager.Instance != null && GameManager.Instance.World != null ? GameManager.Instance.World.GetEntity(entityThatCausedExplosion) as EntityPlayer : null;
        if (player == null) return;
        RebirthSkillAwardService.TryAward(player, "skill.explosives", RebirthProgressionRuntimeConfig.ExplosivesBlockAward, "explosion:block", 0.05f, out _, out _);
    }

    public static void OnDroneShockCompleted(DroneWeapons.StunBeamWeapon weapon, EntityAlive target)
    {
        if (!IsServerAuthority() || weapon == null || target == null || target.IsDead() || target.Buffs == null) return;
        EntityDrone drone = ResolveDrone(weapon);
        if (drone == null || drone.belongsPlayerId <= 0) return;
        EntityPlayer owner = GameManager.Instance != null && GameManager.Instance.World != null ? GameManager.Instance.World.GetEntity(drone.belongsPlayerId) as EntityPlayer : null;
        if (owner == null) return;
        float skillValue;
        if (!TryGetSkillValue(owner, "skill.drone_operations", out skillValue)) return;

        // StunBeamWeapon.Fire calls base.Fire first, which starts the native action+cooldown timer,
        // then applies buffShocked with belongsPlayerId as the instigator. Adjust only that live cycle.
        if (DroneCooldownTimerField != null)
        {
            object raw = DroneCooldownTimerField.GetValue(weapon);
            if (raw is float)
            {
                float current = (float)raw;
                float delta = SignedEndpoint(skillValue, RebirthProgressionRuntimeConfig.DroneStunCycleNegative, RebirthProgressionRuntimeConfig.DroneStunCyclePositive);
                DroneCooldownTimerField.SetValue(weapon, Mathf.Max(0.05f, current * (1f + delta)));
            }
        }

        if (!target.Buffs.HasBuff("buffShocked")) return;
        RebirthSkillAwardService.TryAward(owner, "skill.drone_operations", RebirthProgressionRuntimeConfig.DroneShockAward,
            "drone-shock:" + target.entityId, RebirthProgressionRuntimeConfig.DroneShockRepeatSeconds, out _, out _);
    }

    public static void OnMeaningfulStockDroneRecovery(EntityPlayer owner, EntityDrone drone, float preRecoveryDistance)
    {
        if (!IsServerAuthority() || owner == null || drone == null || drone.belongsPlayerId != owner.entityId) return;
        // Deploy/pickup and idle time award nothing. A stock recovery is meaningful only when an
        // already-deployed Follow drone is recalled from real distance; same-drone recovery is guarded.
        if (preRecoveryDistance < RebirthProgressionRuntimeConfig.DroneStockRecoveryMinDistance || drone.OrderState != EntityDrone.Orders.Follow) return;
        RebirthSkillAwardService.TryAward(owner, "skill.drone_operations", RebirthProgressionRuntimeConfig.DroneStockRecoveryAward,
            "drone-stock-recovery:" + drone.entityId, RebirthProgressionRuntimeConfig.DroneStockRecoveryRepeatSeconds, out _, out _);
    }

    /// <summary>
    /// Native turret/drone gun fire uses the owning player as DamageSource owner and the deployed
    /// entity as CreatorEntityId. This identifies remote-owned device damage without guessing from item names.
    /// </summary>
    public static bool IsRemoteOwnedDeviceSource(World world, DamageSource source)
    {
        if (world == null || source == null || source.CreatorEntityId <= 0) return false;
        Entity creator = world.GetEntity(source.CreatorEntityId);
        return creator is EntityTurret || creator is EntityDrone;
    }

    public static string BuildDebugReport(EntityPlayer player)
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Chunk 10 Complex Skills]");
        b.AppendLine("status=LIMITED_IMPLEMENTED_PREBOOT compileClaim=False sourceAuditGated=True");
        b.AppendLine("explosives=[NATIVE-7DTD][NEW REBIRTH SKILL HOOK] ExplosionEntityDamage+ExplosionBlockDamage; attributed entity LBD existing combat router; block LBD once per changed-block explosion");
        b.AppendLine("deployable_turrets=[NATIVE-7DTD][NEW REBIRTH SKILL HOOK] owner-attributed LBD + EntityDamage scaling directly from Skill; calibration knowledge does not gate core damage; feed/jam reliability failClosed=" + (!DeployableTurretFeedReliabilityEnabled) + " reason=no verified native EntityTurret feed/reload/jam hook");
        b.AppendLine("drone_operations=[NATIVE-7DTD][NEW REBIRTH SKILL HOOK] stock recovery + owner-attributed normalized gun damage + stun cycle + shock LBD; deploy/pickup/idle award zero; native buff duration unchanged");
        b.AppendLine("construction=[NATIVE-7DTD][NEW REBIRTH SKILL HOOK] craft-layer=implemented nativeRepairUpgradeCommitLbd=" + ConstructionWorkActionEnabled + " authority=server Block.DamageBlock post-commit comparison");
        b.AppendLine("electrical=[NEW-REBIRTH] craft-layer=implemented serviceLifecycleEnabled=" + ElectricalWorkmanshipEnabled + " authority=server service request + transactional parts + persisted condition/workmanship + power-use effect");
        b.AppendLine("chemistry_payload=[NEW-REBIRTH][DEFERRED_RUNTIME_GATE] enabled=" + ChemistryPayloadPotencyEnabled + " reason=ItemValue stack/equality/persistence risk");
        if (player == null) { b.AppendLine("player=<null>"); return b.ToString(); }
        float explosivesValue, droneValue, turretValue;
        bool e = TryGetSkillValue(player, "skill.explosives", out explosivesValue);
        bool d = TryGetSkillValue(player, "skill.drone_operations", out droneValue);
        bool t = TryGetSkillValue(player, "skill.deployable_turrets", out turretValue);
        b.AppendLine("player=" + player.entityId);
        b.AppendLine("skill.explosives=" + (e ? explosivesValue.ToString("0.###") : "<unavailable>") + " damageDelta=" + (e ? SignedEndpoint(explosivesValue, RebirthProgressionRuntimeConfig.ExplosivesDamageNegative, RebirthProgressionRuntimeConfig.ExplosivesDamagePositive).ToString("0.###") : "0"));
        float turretDelta = t ? SignedEndpoint(turretValue, RebirthProgressionRuntimeConfig.TurretDamageNegative, RebirthProgressionRuntimeConfig.TurretDamagePositive) : 0f;
        bool calibrated = player != null && RebirthKnowledgeService.HasKnowledge(player, "procedure.turrets.calibration");
        b.AppendLine("skill.deployable_turrets=" + (t ? turretValue.ToString("0.###") : "<unavailable>") + " damageDelta=" + turretDelta.ToString("0.###") + " calibrationKnown=" + calibrated + " calibrationGatesCoreScaling=false feedReliability=" + (DeployableTurretFeedReliabilityEnabled ? "ENABLED" : "BLOCKED_SOURCE_AUDIT"));
        b.AppendLine("skill.drone_operations=" + (d ? droneValue.ToString("0.###") : "<unavailable>") + " damageDelta=" + (d ? SignedEndpoint(droneValue, RebirthProgressionRuntimeConfig.DroneDamageNegative, RebirthProgressionRuntimeConfig.DroneDamagePositive).ToString("0.###") : "0") + " stunCycleDelta=" + (d ? SignedEndpoint(droneValue, RebirthProgressionRuntimeConfig.DroneStunCycleNegative, RebirthProgressionRuntimeConfig.DroneStunCyclePositive).ToString("0.###") : "0"));
        return b.ToString();
    }

    private static EntityDrone ResolveDrone(DroneWeapons.StunBeamWeapon weapon)
    {
        if (DroneEntityField == null || weapon == null) return null;
        return DroneEntityField.GetValue(weapon) as EntityDrone;
    }

    private static bool TryGetSkillValue(EntityPlayer player, string id, out float value)
    {
        return RebirthServiceCraftSkillService.TryGetSkillValue(player, id, out value);
    }

    private static bool IsServerAuthority()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return connection != null && connection.IsServer && RebirthWorldCharacterRepository.IsServerAuthority && RebirthSurvivorMode.IsEnabledForCurrentWorld();
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if (GameManager.Instance == null || GameManager.Instance.World == null) return;
        World world = GameManager.Instance.World;
        float now = Time.realtimeSinceStartup;
        if (now < nextPassiveSync) return;
        nextPassiveSync = now + Mathf.Max(0.10f, RebirthProgressionRuntimeConfig.ComplexPassiveSyncSeconds);
        if (world.IsRemote())
        {
            EntityPlayerLocal local = world.GetPrimaryPlayer();
            if (local != null) SyncNativePassiveEffects(local);
            return;
        }
        if (!IsServerAuthority() || world.Players == null || world.Players.list == null) return;
        RebirthDroneTravelTrainingService.Tick(world, now);
        List<EntityPlayer> players = world.Players.list;
        for (int i = 0; i < players.Count; i++) SyncNativePassiveEffects(players[i]);
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data) { ResetRuntime(); }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { ResetRuntime(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { ResetRuntime(); }
    private static void ResetRuntime() { nextPassiveSync = 0f; RebirthDroneTravelTrainingService.Reset(); }

    private static void SetCVar(EntityPlayer player, string name, float value)
    {
        if (player == null || player.Buffs == null) return;
        if (Math.Abs(player.Buffs.GetCustomVar(name) - value) > 0.0005f) player.Buffs.SetCustomVar(name, value);
    }
}
