using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

/// <summary>
/// Runtime owner for Lockpicking, Bartering/Trading, Athletics, Stealth and Armor Proficiency.
/// Gameplay modifiers reuse native 7DTD passive effects. Progression is server-authoritative and
/// only accepts qualified action/movement evidence; no generic cross-skill synergy is applied.
/// </summary>
public static class RebirthSkillWaveAService
{
    public const bool StealthMovementTrainingEnabled = false;
    public const string PassiveBuff = "RebirthSurvivorSkillWaveAPassives";
    public const string LockpickTimeCVar = "$rbSurvivorSkillLockpickTime";
    public const string LockpickBreakCVar = "$rbSurvivorSkillLockpickBreak";
    public const string BarterBuyingCVar = "$rbSurvivorSkillBarterBuying";
    public const string BarterSellingCVar = "$rbSurvivorSkillBarterSelling";
    public const string TradingStashCVar = "$rbSurvivorSkillTradingStashLevel";
    public const string TradingRewardOptionCVar = "$rbSurvivorTradingRewardOptionBonus";
    public const string SalespersonRewardOptionCVar = "$rbSurvivorSalespersonRewardOptionBonus";
    public const string AthleticsJumpCVar = "$rbSurvivorSkillAthleticsJump";
    public const string AthleticsStaminaCVar = "$rbSurvivorSkillAthleticsStamina";
    public const string AthleticsFallCVar = "$rbSurvivorSkillAthleticsFall";
    public const string StealthNoiseCVar = "$rbSurvivorSkillStealthNoise";
    public const string StealthLightCVar = "$rbSurvivorSkillStealthLight";
    public const string ArmorMobilityCVar = "$rbSurvivorSkillArmorMobility";
    public const string ArmorStaminaWalkCVar = "$rbSurvivorSkillArmorStaminaWalk";
    public const string ArmorStaminaRunCVar = "$rbSurvivorSkillArmorStaminaRun";
    public const string ArmorNoiseCVar = "$rbSurvivorSkillArmorNoise";

    private sealed class MovementState
    {
        public Vector3 LastPosition;
        public bool HasPosition;
        public float AthleticsDistance;
        public float ArmorDistance;
    }

    private sealed class LockpickAttempt
    {
        public int PlayerEntityId;
        public Vector3i Position;
        public float Started;
        public float NativeBaseTime;
        public float EarliestSuccess;
        public float Expires;
        public int BlockType;
        public int UnlockedBlockType;
        public byte BlockMeta;
        public byte BlockMeta2;
        public byte BlockRotation;
    }

    private sealed class ServerInventoryWitness
    {
        public float CapturedAt;
        public int Currency;
        public readonly Dictionary<int,int> Counts = new Dictionary<int,int>();
    }

    private static readonly Dictionary<int, MovementState> MovementByEntity = new Dictionary<int, MovementState>();
    private static readonly Dictionary<string, LockpickAttempt> LockpickAttempts = new Dictionary<string, LockpickAttempt>(StringComparer.Ordinal);
    private static readonly Dictionary<int, Queue<ServerInventoryWitness>> TradeWitnessByEntity = new Dictionary<int, Queue<ServerInventoryWitness>>();
    private static bool installed;
    private static float nextPassiveSync;
    private static float nextSample;

    public static string Install()
    {
        if (installed) return "[REBIRTH Survivor Skill Wave A] already installed";
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed = true;
        return "[REBIRTH Survivor Skill Wave A] installed";
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
        float lockpicking, bartering, trading, athletics, stealth, armor; bool salesperson;
        bool hasCharacter = TryGetSixSkillValues(player, out lockpicking, out bartering, out trading, out athletics, out stealth, out armor, out salesperson);
        if (!hasCharacter)
        {
            SetAllZero(player);
            if (player.Buffs.HasBuff(PassiveBuff)) player.Buffs.RemoveBuff(PassiveBuff);
            return;
        }

        SetCVar(player, LockpickTimeCVar, SignedEndpoint(lockpicking, RebirthProgressionRuntimeConfig.LockpickNegativeTime, RebirthProgressionRuntimeConfig.LockpickPositiveTime));
        SetCVar(player, LockpickBreakCVar, SignedEndpoint(lockpicking, RebirthProgressionRuntimeConfig.LockpickNegativeBreak, RebirthProgressionRuntimeConfig.LockpickPositiveBreak));
        float legacyBarter = SignedEndpoint(bartering, RebirthProgressionRuntimeConfig.BarterNegative, RebirthProgressionRuntimeConfig.BarterLegacyPositiveCap);
        float tradingPrice = Mathf.Clamp01(trading/100f) * Mathf.Max(0f,RebirthProgressionRuntimeConfig.TradingPositive);
        float barter = Mathf.Clamp(legacyBarter + tradingPrice, RebirthProgressionRuntimeConfig.BarterNegative, RebirthProgressionRuntimeConfig.BarterPositive);
        SetCVar(player, BarterBuyingCVar, barter);
        SetCVar(player, BarterSellingCVar, barter);
        SetCVar(player, TradingStashCVar, Mathf.Clamp(trading,0f,100f));
        SetCVar(player, TradingRewardOptionCVar, trading + 0.0001f >= RebirthProgressionRuntimeConfig.TradingRewardOptionThreshold ? 1f : 0f);
        SetCVar(player, SalespersonRewardOptionCVar, salesperson ? 1f : 0f);
        SetCVar(player, AthleticsJumpCVar, SignedEndpoint(athletics, RebirthProgressionRuntimeConfig.AthleticsNegativeJump, RebirthProgressionRuntimeConfig.AthleticsPositiveJump));
        SetCVar(player, AthleticsStaminaCVar, SignedEndpoint(athletics, RebirthProgressionRuntimeConfig.AthleticsNegativeStamina, RebirthProgressionRuntimeConfig.AthleticsPositiveStamina));
        SetCVar(player, AthleticsFallCVar, SignedEndpoint(athletics, RebirthProgressionRuntimeConfig.AthleticsNegativeFall, RebirthProgressionRuntimeConfig.AthleticsPositiveFall));
        SetCVar(player, StealthNoiseCVar, SignedEndpoint(stealth, RebirthProgressionRuntimeConfig.StealthNegativeNoise, RebirthProgressionRuntimeConfig.StealthPositiveNoise));
        SetCVar(player, StealthLightCVar, SignedEndpoint(stealth, RebirthProgressionRuntimeConfig.StealthNegativeLight, RebirthProgressionRuntimeConfig.StealthPositiveLight));

        ArmorBurden burden = GetArmorBurden(player);
        float burdenFactor = SignedEndpoint(armor, RebirthProgressionRuntimeConfig.ArmorNegativeBurden, RebirthProgressionRuntimeConfig.ArmorPositiveRecovery);
        // Positive proficiency restores a bounded portion of existing negative burden directly
        // from Armor Proficiency Skill. No burden means Armor Proficiency has no passive effect.
        SetCVar(player, ArmorMobilityCVar, burden.Mobility * burdenFactor);
        SetCVar(player, ArmorStaminaWalkCVar, burden.StaminaWalk * burdenFactor);
        SetCVar(player, ArmorStaminaRunCVar, burden.StaminaRun * burdenFactor);
        SetCVar(player, ArmorNoiseCVar, -burden.Noise * burdenFactor);
        if (!player.Buffs.HasBuff(PassiveBuff)) player.Buffs.AddBuff(PassiveBuff);
    }

    public static void RegisterServerLockpickAttempt(EntityPlayer player, Vector3i position, float nativeBaseTime, float nativeEffectiveTime, float remainingPickTime, int unlockedBlockType = 0)
    {
        if (player == null || player.world == null || player.world.IsRemote()) return;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player, out identity, out record)) return;
        float now = Time.realtimeSinceStartup;
        float expected = remainingPickTime >= 0f ? Mathf.Min(Mathf.Max(0.25f, nativeEffectiveTime), Mathf.Max(0.25f, remainingPickTime)) : Mathf.Max(0.25f, nativeEffectiveTime);
        BlockValue target = player.world.GetBlock(position);
        LockpickAttempts[LockKey(player.entityId, position)] = new LockpickAttempt
        {
            PlayerEntityId = player.entityId,
            Position = position,
            Started = now,
            NativeBaseTime = Mathf.Max(0.25f,nativeBaseTime),
            EarliestSuccess = now + Mathf.Max(0.25f, expected - 0.75f),
            Expires = now + Mathf.Max(20f, expected + 30f),
            BlockType = target.type,
            UnlockedBlockType = unlockedBlockType,
            BlockMeta = target.meta,
            BlockMeta2 = target.meta2,
            BlockRotation = target.rotation
        };
    }

    public static void ReportClientLockpickSuccess(EntityPlayerLocal player, Vector3i position)
    {
        if (player == null) return;
        SendOrHandleClientAction(player, RebirthSkillWaveAClientAction.LockpickSuccess, position.x, position.y, position.z, 0, 0, 0);
    }

    public static void ReportClientBarter(EntityPlayerLocal player, bool buy, int itemType, int count, int value)
    {
        if (player == null || itemType <= 0 || count <= 0 || value <= 0) return;
        SendOrHandleClientAction(player, buy ? RebirthSkillWaveAClientAction.BarterBuy : RebirthSkillWaveAClientAction.BarterSell, 0, 0, 0, itemType, count, value);
    }

    public static void HandleServerClientAction(EntityPlayer player, RebirthSkillWaveAClientAction action, int x, int y, int z, int itemType, int count, int value)
    {
        if (player == null || player.world == null || player.world.IsRemote()) return;
        if (action == RebirthSkillWaveAClientAction.LockpickSuccess)
        {
            HandleLockpickSuccess(player, new Vector3i(x, y, z));
            return;
        }
        if (action == RebirthSkillWaveAClientAction.BarterBuy || action == RebirthSkillWaveAClientAction.BarterSell)
            HandleBarterCompleted(player, action == RebirthSkillWaveAClientAction.BarterBuy, itemType, count, value);
    }

    public static void OnArmoredCombat(EntityPlayer player)
    {
        // Phase 10: combat no longer grants a flat Armor Proficiency award. Armor training is
        // burden-weighted genuine exertion so repeated hits cannot become an independent XP farm.
    }

    public static string BuildDebugReport(EntityPlayer player)
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Skill Wave A] status");
        b.AppendLine("  Lockpicking [NATIVE-7DTD][NEW SKILL HOOK] LockPickTime + LockPickBreakChance; LBD=validated lock success");
        b.AppendLine("  Bartering/Trading [NATIVE-7DTD][NEW SKILL HOOK] purchase->Bartering, sale->Trading; neutral economic value + server inventory/currency witness + anti-loop/value/variety guards");
        b.AppendLine("  Athletics [NATIVE-7DTD][NEW SKILL HOOK] JumpStrength + StaminaLoss + FallDamageReduction; LBD=server walking/running-tagged locomotion normalized to neutral Walk/Run/Crouch x Mobility speed");
        b.AppendLine("  Stealth [NATIVE-7DTD][NEW SKILL HOOK] NoiseMultiplier + LightMultiplier; LBD=pre-hit-undetected actual weapon damage normalized by live weapon DPS");
        b.AppendLine("  Armor Proficiency [NATIVE-7DTD BURDEN][NEW SKILL HOOK] restores part of Mobility/Stamina/Noise burden; LBD=the same qualified locomotion multiplied only by incremental armor burden");
        if (player == null) return b.ToString().TrimEnd();
        float l,bar,trading,a,sneak,armor; bool salesperson;
        bool has = TryGetSixSkillValues(player, out l, out bar, out trading, out a, out sneak, out armor, out salesperson);
        ArmorBurden burden = GetArmorBurden(player);
        b.Append("  entity=").Append(player.entityId).Append(" hasCharacter=").Append(has)
            .Append(" skills lock=").Append(l.ToString("0.##")).Append(" legacyBarter=").Append(bar.ToString("0.##")).Append(" trading=").Append(trading.ToString("0.##"))
            .Append(" salesperson=").Append(salesperson).Append(" rewardOptionBonus=").Append(player.Buffs!=null?player.Buffs.GetCustomVar(TradingRewardOptionCVar)+player.Buffs.GetCustomVar(SalespersonRewardOptionCVar):0f)
            .Append(" athletics=").Append(a.ToString("0.##")).Append(" stealth=").Append(sneak.ToString("0.##"))
            .Append(" armor=").Append(armor.ToString("0.##")).AppendLine();
        b.Append("  armorBurden pieces=").Append(burden.Pieces).Append(" medium=").Append(burden.MediumPieces).Append(" heavy=").Append(burden.HeavyPieces)
            .Append(" mobility=").Append(burden.Mobility.ToString("0.###")).Append(" walk=").Append(burden.StaminaWalk.ToString("0.###"))
            .Append(" run=").Append(burden.StaminaRun.ToString("0.###")).Append(" noise=").Append(burden.Noise.ToString("0.###"));
        return b.ToString();
    }

    public struct ArmorBurden
    {
        public int Pieces, MediumPieces, HeavyPieces;
        public float Mobility, StaminaWalk, StaminaRun, Noise;
        public bool HasBurden { get { return MediumPieces + HeavyPieces > 0; } }
    }

    public static ArmorBurden GetArmorBurden(EntityPlayer player)
    {
        ArmorBurden b = new ArmorBurden();
        if (player == null || player.equipment == null) return b;
        EquipmentSlots[] slots = { EquipmentSlots.Head, EquipmentSlots.Chest, EquipmentSlots.Hands, EquipmentSlots.Feet };
        for (int i = 0; i < slots.Length; i++)
        {
            ItemValue item = player.equipment.GetSlotItem((int)slots[i]);
            if (item == null || item.IsEmpty() || item.ItemClass == null) continue;
            string tags = item.ItemClass.ItemTags.ToString() ?? string.Empty;
            if (tags.IndexOf("heavyArmorPenalty", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                b.Pieces++; b.HeavyPieces++; b.Mobility += 0.075f; b.StaminaWalk += 0.045f; b.StaminaRun += 0.09f; b.Noise += 0.20f;
            }
            else if (tags.IndexOf("mediumArmorPenalty", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                b.Pieces++; b.MediumPieces++; b.Mobility += 0.05f; b.StaminaWalk += 0.0281f; b.StaminaRun += 0.0562f; b.Noise += 0.10f;
            }
        }
        return b;
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data) { ResetRuntime(); }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { ResetRuntime(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { ResetRuntime(); }

    private static void ResetRuntime()
    {
        MovementByEntity.Clear(); LockpickAttempts.Clear(); TradeWitnessByEntity.Clear(); nextPassiveSync = 0f; nextSample = 0f;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if (GameManager.Instance == null || GameManager.Instance.World == null) return;
        World world = GameManager.Instance.World;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        float now = Time.realtimeSinceStartup;

        // Client needs the native passives for local UI/timers/prices/movement. Server also mirrors
        // authoritative values for server-side effect queries. Do not rescan equipment/rewrite CVars
        // every frame; the bridge is intentionally throttled and remains independent from LBD sampling.
        if (world.IsRemote())
        {
            if (now >= nextPassiveSync)
            {
                nextPassiveSync = now + Mathf.Max(0.25f, RebirthProgressionRuntimeConfig.WaveAPassiveSyncSeconds);
                EntityPlayerLocal local = world.GetPrimaryPlayer();
                if (local != null) SyncNativePassiveEffects(local);
            }
            return;
        }
        if (connection == null || !connection.IsServer || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        if (world.Players == null || world.Players.list == null) return;
        List<EntityPlayer> players = world.Players.list;
        if (now >= nextPassiveSync)
        {
            nextPassiveSync = now + Mathf.Max(0.25f, RebirthProgressionRuntimeConfig.WaveAPassiveSyncSeconds);
            for (int i = 0; i < players.Count; i++) SyncNativePassiveEffects(players[i]);
        }
        if (now < nextSample) return;
        nextSample = now + Mathf.Max(0.25f, RebirthProgressionRuntimeConfig.WaveASampleSeconds);
        for (int i = 0; i < players.Count; i++)
        {
            CaptureServerInventoryWitness(players[i], now);
            SampleServerMovement(players[i]);
        }
        PruneExpired(now);
    }

    private static void SampleServerMovement(EntityPlayer player)
    {
        if (player == null) return;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player, out identity, out record)) { MovementByEntity.Remove(player.entityId); return; }
        MovementState state;
        if (!MovementByEntity.TryGetValue(player.entityId, out state) || state == null)
        {
            state = new MovementState(); MovementByEntity[player.entityId] = state;
        }
        Vector3 pos = player.position;
        if (!state.HasPosition) { state.LastPosition = pos; state.HasPosition = true; return; }
        float dx = pos.x - state.LastPosition.x, dz = pos.z - state.LastPosition.z;
        state.LastPosition = pos;
        float distance = Mathf.Sqrt(dx * dx + dz * dz);
        if (distance < 0.10f || distance > RebirthProgressionRuntimeConfig.WaveAMaxSampleDistance || player.AttachedToEntity != null) return;
        string movementTags;
        if (!TryGetQualifiedLocomotionTags(player, out movementTags)) return;
        float exertionDistance = NormalizeLocomotionDistance(player, distance, movementTags);
        if (exertionDistance <= 0f) return;

        // Phase 10 exertion witness: displacement alone is not enough. The server must also report
        // one of the native locomotion movement tags (walking/running). Observed distance is then
        // converted back to a neutral Walk/Run/Crouch x Mobility distance equivalent so movement
        // speed bonuses or penalties do not directly multiply Skill. Idle jitter, knockback/slide,
        // vehicle motion and jump-only spam award zero. The locked reference remains .08 / 25m.
        state.AthleticsDistance += exertionDistance;
        while (state.AthleticsDistance >= RebirthProgressionRuntimeConfig.AthleticsDistance)
        {
            state.AthleticsDistance -= RebirthProgressionRuntimeConfig.AthleticsDistance;
            AwardWaveADiscrete(player,"skill.athletics",RebirthProgressionRuntimeConfig.AthleticsAward,
                "wavea:athletics:exertion","25m-equivalent genuine locomotion");
        }

        // Armor Proficiency trains only the incremental armor burden layered on top of the same
        // genuine locomotion. Use the exact authored walk/run stamina burden for the current mode;
        // this avoids double-counting base Athletics work and avoids valuing walking as running.
        ArmorBurden trainingBurden=GetArmorBurden(player);
        if (trainingBurden.HasBurden)
        {
            const float fullMediumWalkBurden=4f*0.0281f;
            const float fullMediumRunBurden=4f*0.0562f;
            bool runningExertion=HasMovementTagToken(movementTags,"running");
            float burden=runningExertion?trainingBurden.StaminaRun:trainingBurden.StaminaWalk;
            float reference=runningExertion?fullMediumRunBurden:fullMediumWalkBurden;
            float normalizedBurden=reference>0f?Mathf.Max(0f,burden/reference):0f;
            state.ArmorDistance += exertionDistance*normalizedBurden;
            while (state.ArmorDistance >= RebirthProgressionRuntimeConfig.ArmorDistance)
            {
                state.ArmorDistance -= RebirthProgressionRuntimeConfig.ArmorDistance;
                AwardWaveADiscrete(player,"skill.armor_proficiency",RebirthProgressionRuntimeConfig.ArmorAward,
                    "wavea:armor:burden-exertion","30m-equivalent incremental armor burden");
            }
        }
    }


    // Exact neutral player movement bases from the matching current entityclasses.xml extraction.
    // These are reference values, not new tuning coefficients.
    internal const float NeutralWalkSpeed = 1.53f;
    internal const float NeutralRunSpeed = 1.10f;
    internal const float NeutralCrouchSpeed = 1.04f;
    internal const float NeutralMobility = 1.00f;

    internal static bool HasMovementTagToken(string movementTags, string wanted)
    {
        if (string.IsNullOrWhiteSpace(movementTags) || string.IsNullOrWhiteSpace(wanted)) return false;
        string[] tokens = movementTags.Split(new [] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
            if (string.Equals(tokens[i].Trim(), wanted, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static bool IsQualifiedLocomotionTagText(string movementTags)
    {
        return HasMovementTagToken(movementTags, "walking") || HasMovementTagToken(movementTags, "running");
    }

    internal static float NormalizeLocomotionDistance(float actualDistance, float liveModeSpeed, float neutralModeSpeed, float liveMobility, float maxEquivalentDistance)
    {
        if (!Finite(actualDistance) || !Finite(liveModeSpeed) || !Finite(neutralModeSpeed) || !Finite(liveMobility) ||
            actualDistance <= 0f || liveModeSpeed <= 0f || neutralModeSpeed <= 0f || liveMobility <= 0f || maxEquivalentDistance <= 0f)
            return 0f;
        float speedScale = (liveModeSpeed / neutralModeSpeed) * (liveMobility / NeutralMobility);
        if (!Finite(speedScale) || speedScale <= 0f) return 0f;
        float equivalent = actualDistance / speedScale;
        if (!Finite(equivalent) || equivalent <= 0f) return 0f;
        return Mathf.Min(maxEquivalentDistance, equivalent);
    }

    private static bool TryGetQualifiedLocomotionTags(EntityPlayer player, out string movementTags)
    {
        movementTags = string.Empty;
        if (player == null) return false;
        try
        {
            movementTags = player.CurrentMovementTag.ToString();
            return IsQualifiedLocomotionTagText(movementTags);
        }
        catch { movementTags = string.Empty; return false; }
    }

    private static float NormalizeLocomotionDistance(EntityPlayer player, float actualDistance, string movementTags)
    {
        if (player == null) return 0f;
        bool running = HasMovementTagToken(movementTags, "running");
        bool crouching = !running && HasMovementTagToken(movementTags, "crouching");
        PassiveEffects modeEffect = running ? PassiveEffects.RunSpeed : (crouching ? PassiveEffects.CrouchSpeed : PassiveEffects.WalkSpeed);
        float neutralModeSpeed = running ? NeutralRunSpeed : (crouching ? NeutralCrouchSpeed : NeutralWalkSpeed);
        try
        {
            FastTags<TagGroup.Global> tags = player.CurrentMovementTag;
            float liveModeSpeed = EffectManager.GetValue(modeEffect, _originalValue: neutralModeSpeed, _entity: player, tags: tags);
            float liveMobility = EffectManager.GetValue(PassiveEffects.Mobility, _originalValue: NeutralMobility, _entity: player, tags: tags);
            return NormalizeLocomotionDistance(actualDistance, liveModeSpeed, neutralModeSpeed, liveMobility, RebirthProgressionRuntimeConfig.WaveAMaxSampleDistance);
        }
        catch { return 0f; }
    }

    private static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool HasNearbyZombieThreat(EntityPlayer player, float radius)
    {
        if (player == null || player.world == null || player.world.Entities == null || player.world.Entities.list == null) return false;
        float r2 = radius * radius;
        List<Entity> entities = player.world.Entities.list;
        for (int i = 0; i < entities.Count; i++)
        {
            EntityZombie zombie = entities[i] as EntityZombie;
            if (zombie == null || zombie.IsDead()) continue;
            Vector3 d = zombie.position - player.position;
            if (d.sqrMagnitude <= r2) return true;
        }
        return false;
    }

    private static void HandleLockpickSuccess(EntityPlayer player, Vector3i position)
    {
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player, out identity, out record)) return;
        LockpickAttempt attempt;
        string key = LockKey(player.entityId, position);
        if (!LockpickAttempts.TryGetValue(key, out attempt) || attempt == null) return;
        float now = Time.realtimeSinceStartup;
        if (now < attempt.EarliestSuccess || now > attempt.Expires) { if (now > attempt.Expires) LockpickAttempts.Remove(key); return; }
        Vector3 d = player.position - position.ToVector3();
        if (d.sqrMagnitude > 100f) return;
        BlockValue current = player.world.GetBlock(position);
        int expectedType = attempt.UnlockedBlockType > 0 ? attempt.UnlockedBlockType : attempt.BlockType;
        if (current.type != expectedType || current.meta != attempt.BlockMeta ||
            current.meta2 != attempt.BlockMeta2 || current.rotation != attempt.BlockRotation)
        {
            LockpickAttempts.Remove(key);
            return;
        }
        bool unlocked;
        if (attempt.UnlockedBlockType <= 0 && (!TryReadAuthoritativeUnlocked(player.world as World, position, out unlocked) || !unlocked))
            return;
        LockpickAttempts.Remove(key); // one server-registered target incarnation can pay once.
        RebirthPhase9TechnicalTrainingService.AwardLockpickSuccess(player,attempt.NativeBaseTime,position.x+":"+position.y+":"+position.z);
        RebirthTheoryProgressionService.TryAwardInsight(player, "insight.lockpicking.successful_lockpick",
            "server-verified-lockpick", out var theoryApplied, out var insightAlreadyEarned);
    }

    private static bool TryReadAuthoritativeUnlocked(World world, Vector3i position, out bool unlocked)
    {
        unlocked = false;
        if (world == null || world.IsRemote()) return false;
        object tile = null;
        try { tile = world.GetTileEntity(position); } catch { }
        if (tile == null) return false;
        Type type = tile.GetType();
        string[] memberNames = new [] { "IsLocked", "isLocked", "bLocked", "locked" };
        for (int i = 0; i < memberNames.Length; i++)
        {
            try
            {
                System.Reflection.PropertyInfo property = type.GetProperty(memberNames[i],
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                if (property != null && property.CanRead && property.PropertyType == typeof(bool))
                {
                    unlocked = !(bool)property.GetValue(tile, null);
                    return true;
                }
                System.Reflection.FieldInfo field = type.GetField(memberNames[i],
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(bool))
                {
                    unlocked = !(bool)field.GetValue(tile);
                    return true;
                }
                System.Reflection.MethodInfo method = type.GetMethod(memberNames[i],
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (method != null && method.ReturnType == typeof(bool))
                {
                    unlocked = !(bool)method.Invoke(tile, null);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    // Internal server-only entry: caller has already verified the saved payout and
    // committed the exact stash debit. Native bag-item delta cannot witness this sale.
    internal static void ReportCommittedStashSale(EntityPlayer player, ItemStack sold, int value)
    {
        if(player?.world==null||player.world.IsRemote()||sold==null||sold.IsEmpty())return;
        HandleBarterCompleted(player,false,sold.itemValue.type,sold.count,value,true);
    }
    private static void HandleBarterCompleted(EntityPlayer player, bool buy, int itemType, int count, int value, bool committedStash = false)
    {
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player, out identity, out record)) return;
        if (itemType <= 0 || count <= 0 || count > 10000 || value <= 0 || value > 100000000 || !HasNearbyTrader(player, 10f)) return;
        if (!committedStash && !ConsumeAuthoritativeTradeWitness(player,buy,itemType,count,value)) return;
        int trustedValue; if(!TryValidateTradingValue(itemType,count,value,out trustedValue)) return;
        RebirthStatisticsService.RecordTraderVisited(player);
        RebirthCommerceTrainingRuntimeState commerce=record.Progression.CommerceTraining ?? (record.Progression.CommerceTraining=new RebirthCommerceTrainingRuntimeState());
        string itemKey=itemType.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RebirthCommerceItemTrainingRuntimeState h;
        if(!commerce.Items.TryGetValue(itemKey,out h)||h==null)
        {
            h=new RebirthCommerceItemTrainingRuntimeState();
            commerce.Items[itemKey]=h;
        }
        double nowActive=record.Condition!=null?Math.Max(0d,record.Condition.ActivePlaySeconds):0d;
        if(commerce.LastGlobalAwardActiveSeconds>=0d && nowActive-commerce.LastGlobalAwardActiveSeconds<RebirthProgressionRuntimeConfig.BarterGlobalSeconds) return;
        double same=buy?h.LastBuyActiveSeconds:h.LastSellActiveSeconds;
        double opposite=buy?h.LastSellActiveSeconds:h.LastBuyActiveSeconds;
        if(same>=0d && nowActive-same<RebirthProgressionRuntimeConfig.BarterRepeatSeconds) return;
        if(opposite>=0d && nowActive-opposite<RebirthProgressionRuntimeConfig.BarterLoopSeconds) return;
        if(h.LastAwardActiveSeconds>=0d && nowActive-h.LastAwardActiveSeconds<RebirthProgressionRuntimeConfig.TradingVarietyWindowSeconds) h.RepeatChain=Math.Min(8,h.RepeatChain+1); else h.RepeatChain=0;
        if(buy)h.LastBuyActiveSeconds=nowActive;else h.LastSellActiveSeconds=nowActive;
        h.LastAwardActiveSeconds=nowActive;
        commerce.LastGlobalAwardActiveSeconds=nowActive;
        PruneCommerceHistory(commerce,nowActive);
        record.Touch("commerce-training:"+(buy?"buy":"sell")+":"+itemType);

        float scale=Mathf.Max(0.0001f,RebirthProgressionRuntimeConfig.BarterValueScale);
        float scaled=trustedValue/(trustedValue+scale);
        float award=RebirthProgressionRuntimeConfig.BarterBaseAward+scaled*RebirthProgressionRuntimeConfig.BarterValueAwardCap;
        if(h.RepeatChain>0)award*=Mathf.Max(0.25f,1f/(1f+h.RepeatChain));
        string skillId=buy?RebirthSurvivorIds.SkillBartering:RebirthSurvivorIds.SkillTrading;
        bool awarded=AwardWaveADiscrete(player,skillId,award,"wavea:"+(buy?"bartering:buy":"trading:sell")+":"+itemType,
            "committed neutral-reference NPC transaction value="+trustedValue);
        // A committed transaction consumes anti-loop state even if the Skill is capped or the
        // projected award is otherwise zero. If the award path did not save the record, persist
        // that transaction history explicitly so reconnect/restart cannot reset it.
        if(!awarded||record.Dirty)RebirthWorldCharacterRepository.SaveIfDirty(identity,"commerce-training:"+(buy?"buy":"sell"));
    }

    private static void PruneCommerceHistory(RebirthCommerceTrainingRuntimeState commerce,double nowActive)
    {
        if(commerce==null||commerce.Items.Count<=192)return;
        List<string> stale=new List<string>();
        foreach(KeyValuePair<string,RebirthCommerceItemTrainingRuntimeState> pair in commerce.Items)
        {
            RebirthCommerceItemTrainingRuntimeState h=pair.Value;
            if(h==null||h.LastAwardActiveSeconds<0d||nowActive-h.LastAwardActiveSeconds>Math.Max(300d,RebirthProgressionRuntimeConfig.TradingVarietyWindowSeconds*4d)) stale.Add(pair.Key);
        }
        for(int i=0;i<stale.Count;i++)commerce.Items.Remove(stale[i]);
        while(commerce.Items.Count>256)
        {
            string oldestKey=null;double oldest=double.MaxValue;
            foreach(KeyValuePair<string,RebirthCommerceItemTrainingRuntimeState> pair in commerce.Items)
            {
                double t=pair.Value!=null?pair.Value.LastAwardActiveSeconds:-1d;
                if(t<oldest){oldest=t;oldestKey=pair.Key;}
            }
            if(string.IsNullOrEmpty(oldestKey))break;
            commerce.Items.Remove(oldestKey);
        }
    }

    private static void CaptureServerInventoryWitness(EntityPlayer player,float now)
    {
        if(player==null)return;
        ServerInventoryWitness witness=new ServerInventoryWitness{CapturedAt=now};
        AccumulateInventoryCounts(player.bag!=null?player.bag.ItemGrid.items:null,witness.Counts);
        AccumulateInventoryCounts(player.inventory!=null?player.inventory.ItemGrid.items:null,witness.Counts);
        ItemValue currency=ItemClass.GetItem(TraderInfo.CurrencyItem);
        int currencyType=currency!=null&&!currency.IsEmpty()?currency.type:0;
        if(currencyType>0)witness.Counts.TryGetValue(currencyType,out witness.Currency);
        Queue<ServerInventoryWitness> history;
        if(!TradeWitnessByEntity.TryGetValue(player.entityId,out history)||history==null)
        {history=new Queue<ServerInventoryWitness>();TradeWitnessByEntity[player.entityId]=history;}
        history.Enqueue(witness);
        while(history.Count>0&&(history.Count>16||now-history.Peek().CapturedAt>3f))history.Dequeue();
    }

    private static void AccumulateInventoryCounts(ItemStack[] slots,Dictionary<int,int> counts)
    {
        if(slots==null||counts==null)return;
        for(int i=0;i<slots.Length;i++)
        {
            ItemStack stack=slots[i];if(stack==null||stack.IsEmpty()||stack.itemValue==null||stack.count<=0)continue;
            int current;counts.TryGetValue(stack.itemValue.type,out current);
            long next=(long)current+stack.count;counts[stack.itemValue.type]=(int)Math.Min(int.MaxValue,next);
        }
    }

    private static bool ConsumeAuthoritativeTradeWitness(EntityPlayer player,bool buy,int itemType,int count,int value)
    {
        if(player==null)return false;
        Queue<ServerInventoryWitness> history;
        if(!TradeWitnessByEntity.TryGetValue(player.entityId,out history)||history==null||history.Count==0)return false;
        float now=Time.realtimeSinceStartup;
        ServerInventoryWitness after=new ServerInventoryWitness{CapturedAt=now};
        AccumulateInventoryCounts(player.bag!=null?player.bag.ItemGrid.items:null,after.Counts);
        AccumulateInventoryCounts(player.inventory!=null?player.inventory.ItemGrid.items:null,after.Counts);
        ItemValue currency=ItemClass.GetItem(TraderInfo.CurrencyItem);
        int currencyType=currency!=null&&!currency.IsEmpty()?currency.type:0;
        if(currencyType>0)after.Counts.TryGetValue(currencyType,out after.Currency);

        bool witnessed=false;
        foreach(ServerInventoryWitness before in history)
        {
            if(before==null||now-before.CapturedAt>3f)continue;
            int beforeItem=0,afterItem=0;before.Counts.TryGetValue(itemType,out beforeItem);after.Counts.TryGetValue(itemType,out afterItem);
            witnessed=buy
                ? before.Currency-after.Currency>=value && afterItem-beforeItem>=count
                : after.Currency-before.Currency>=value && beforeItem-afterItem>=count;
            if(witnessed)break;
        }

        // Consume all prior candidates regardless of success so one observed transition can
        // never be replayed/probed with multiple reported values. Start a fresh baseline now.
        history.Clear();history.Enqueue(after);
        return witnessed;
    }

    private static bool TryValidateTradingValue(int itemType,int count,int reportedValue,out int trustedValue)
    {
        trustedValue=0; if(itemType<=0||count<=0||reportedValue<=0)return false;
        ItemClass item=ItemClass.GetForId(itemType); if(item==null)return false;
        float baseEconomic=Mathf.Max(0f,item.EconomicValue); if(baseEconomic<=0f)return false;
        float neutralValue=baseEconomic*Math.Max(1,count);
        float ceiling=neutralValue*Mathf.Max(1f,RebirthProgressionRuntimeConfig.TradingValueSpoofMultiplier);
        if(reportedValue>ceiling+1f)return false;
        // Progression uses neutral/pre-modifier economic value. Discounts, bonuses and the player's
        // own Bartering/Trading effects can prove a transaction but cannot inflate its training value.
        if(neutralValue+0.0001f<RebirthProgressionRuntimeConfig.TradingMinimumValue)return false;
        if(baseEconomic+0.0001f<RebirthProgressionRuntimeConfig.TradingMinimumUnitValue)return false;
        trustedValue=Math.Max(1,Mathf.FloorToInt(neutralValue));return true;
    }

    private static bool HasNearbyTrader(EntityPlayer player, float radius)
    {
        if (player == null || player.world == null || player.world.Entities == null || player.world.Entities.list == null) return false;
        float r2 = radius * radius;
        List<Entity> list = player.world.Entities.list;
        for (int i = 0; i < list.Count; i++)
        {
            EntityTrader trader = list[i] as EntityTrader;
            if (trader == null || trader.IsDead()) continue;
            if ((trader.position - player.position).sqrMagnitude <= r2) return true;
        }
        return false;
    }

    private static void SendOrHandleClientAction(EntityPlayerLocal player, RebirthSkillWaveAClientAction action, int x, int y, int z, int itemType, int count, int value)
    {
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c == null) return;
        if (c.IsServer) HandleServerClientAction(player, action, x, y, z, itemType, count, value);
        else c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthSkillWaveAActionRequest>().Setup(player.entityId, action, x, y, z, itemType, count, value));
    }

    private static bool AwardWaveADiscrete(EntityPlayer player,string skillId,float raw,string source,string description)
    {
        if(player==null||raw<=0f)return false;
        RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence{SkillId=skillId,SourceKey=source??string.Empty,ReferenceDescription=description??string.Empty,
            AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=1f,DiscreteRawAward=raw};
        RebirthSkillTrainingComputation computation;float appliedSkill,appliedAttribute;
        return RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(player,evidence,out computation,out appliedSkill,out appliedAttribute);
    }

    private static bool TryGetSixSkillValues(EntityPlayer player, out float lockpicking, out float bartering, out float trading, out float athletics, out float stealth, out float armor, out bool salesperson)
    {
        lockpicking = bartering = trading = athletics = stealth = armor = 0f; salesperson=false;
        if (player == null) return false;
        if (player.world != null && player.world.IsRemote())
        {
            RebirthSurvivorOwnerScalars state;
            if (!RebirthSurvivorClientState.TryGetOwnerScalars(player, out state)) return false;
            lockpicking = SnapshotSkill(state, RebirthSurvivorIds.SkillLockpicking); bartering = SnapshotSkill(state, RebirthSurvivorIds.SkillBartering); trading=SnapshotSkill(state,RebirthSurvivorIds.SkillTrading); athletics = SnapshotSkill(state, RebirthSurvivorIds.SkillAthletics); stealth = SnapshotSkill(state, RebirthSurvivorIds.SkillStealth); armor = SnapshotSkill(state, RebirthSurvivorIds.SkillArmorProficiency); salesperson=string.Equals(state.BackgroundId,"background.salesperson",StringComparison.OrdinalIgnoreCase);
            return true;
        }
        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete || record.Progression == null) return false;
        lockpicking = RecordSkill(record, RebirthSurvivorIds.SkillLockpicking); bartering = RecordSkill(record, RebirthSurvivorIds.SkillBartering); trading=RecordSkill(record,RebirthSurvivorIds.SkillTrading); athletics = RecordSkill(record, RebirthSurvivorIds.SkillAthletics); stealth = RecordSkill(record, RebirthSurvivorIds.SkillStealth); armor = RecordSkill(record, RebirthSurvivorIds.SkillArmorProficiency); salesperson=record.Origin!=null&&string.Equals(record.Origin.BackgroundId,"background.salesperson",StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static float SnapshotSkill(RebirthSurvivorOwnerScalars s, string id)
    {
        if (s == null) return 0f;
        float practical, attribute = RebirthProgressionRuntimeConfig.AttributeOutcomeCenter;
        if (!s.TryGetSkill(id, out practical)) return 0f;
        string attributeId = RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(id);
        float current;
        if (!string.IsNullOrEmpty(attributeId) && s.TryGetAttribute(attributeId, out current)) attribute = current;
        return string.IsNullOrEmpty(attributeId) ? practical : RebirthSkillOutcomeValueService.ApplyAttributeContribution(id, practical, attribute);
    }
    private static float RecordSkill(RebirthWorldCharacterRecord r, string id)
    {
        if(r==null||r.Progression==null)return 0f;
        RebirthSkillRuntimeState state;if(!r.Progression.Skills.TryGetValue(id,out state)||state==null)return 0f;
        string attributeId=RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(id);
        float attribute=RebirthProgressionRuntimeConfig.AttributeOutcomeCenter;
        RebirthAttributeRuntimeState a;
        if(!string.IsNullOrEmpty(attributeId)&&r.Progression.Attributes.TryGetValue(attributeId,out a)&&a!=null)attribute=a.Current;
        return string.IsNullOrEmpty(attributeId)?state.Value:RebirthSkillOutcomeValueService.ApplyAttributeContribution(id,state.Value,attribute);
    }

    private static void SetAllZero(EntityPlayer player)
    {
        SetCVar(player, LockpickTimeCVar, 0f); SetCVar(player, LockpickBreakCVar, 0f); SetCVar(player, BarterBuyingCVar, 0f); SetCVar(player, BarterSellingCVar, 0f); SetCVar(player,TradingStashCVar,0f); SetCVar(player,TradingRewardOptionCVar,0f); SetCVar(player,SalespersonRewardOptionCVar,0f);
        SetCVar(player, AthleticsJumpCVar, 0f); SetCVar(player, AthleticsStaminaCVar, 0f); SetCVar(player, AthleticsFallCVar, 0f); SetCVar(player, StealthNoiseCVar, 0f); SetCVar(player, StealthLightCVar, 0f);
        SetCVar(player, ArmorMobilityCVar, 0f); SetCVar(player, ArmorStaminaWalkCVar, 0f); SetCVar(player, ArmorStaminaRunCVar, 0f); SetCVar(player, ArmorNoiseCVar, 0f);
    }
    private static void SetCVar(EntityPlayer player, string name, float value)
    { if (Math.Abs(player.Buffs.GetCustomVar(name) - value) > 0.0005f) player.Buffs.SetCustomVar(name, value); }
    private static string LockKey(int entityId, Vector3i p) { return entityId + "|" + p.x + "," + p.y + "," + p.z; }
    private static void PruneExpired(float now)
    {
        if (LockpickAttempts.Count > 0)
        {
            List<string> remove = null;
            foreach (KeyValuePair<string, LockpickAttempt> kv in LockpickAttempts) if (kv.Value == null || now > kv.Value.Expires) { if (remove == null) remove = new List<string>(); remove.Add(kv.Key); }
            if (remove != null) for (int i = 0; i < remove.Count; i++) LockpickAttempts.Remove(remove[i]);
        }
    }
}
