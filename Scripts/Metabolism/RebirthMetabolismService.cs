using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#nullable disable

public static class RebirthMetabolismService
{
    private static readonly FastTags<TagGroup.Global> TagWalking = FastTags<TagGroup.Global>.Parse("walking");
    private static readonly FastTags<TagGroup.Global> TagRunning = FastTags<TagGroup.Global>.Parse("running");
    private static readonly FastTags<TagGroup.Global> TagSwimming = FastTags<TagGroup.Global>.Parse("swimming");
    private static readonly FastTags<TagGroup.Global> TagSwimmingRun = FastTags<TagGroup.Global>.Parse("swimmingRun");
    private static readonly FastTags<TagGroup.Global> TagClimbing = FastTags<TagGroup.Global>.Parse("climbing");
    private static readonly FastTags<TagGroup.Global> TagJumping = FastTags<TagGroup.Global>.Parse("jumping");

    public static bool DebugEnabled;
    private static float nextPersistenceSaveRealTime;
    private static readonly Dictionary<int, float> debugTraceUntilRealTime = new Dictionary<int, float>();
    private static readonly Dictionary<int, long> snapshotSequences = new Dictionary<int, long>();

    public static bool IsServerAuthority
    {
        get
        {
            ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
            return c == null || c.IsServer;
        }
    }

    public static void OnPlayerLoaded(EntityPlayer player)
    {
        EnsurePlayerReady(player);
    }

    public static void EnsurePlayerReady(EntityPlayer player)
    {
        if (!IsServerAuthority || player == null || player.world == null || player.Stats == null)
            return;
        if (RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))
            return;

        RebirthMetabolismStateRepository.SetServerAuthority(true);
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null)
            return;
        if (!RebirthSurvivorMetabolismCreationGate.IsLinkedToCommittedOrigin(player, state))
        {
            Log.Error("[REBIRTH Metabolism] Survivor creation generation mismatch; metabolism initialization deferred for entity=" + player.entityId);
            return;
        }

        if (state.RuntimeEntityId != player.entityId)
        {
            state.RuntimeEntityId = player.entityId;
            RebirthMetabolismStateRepository.PrepareOnlineState(player);
            NormalizeLoadedLiquidStacks(player);
            RemoveLegacyFoodWaterStatusBuffs(player);
            MigratePlayerStatsOnce(player, state);
            SendSnapshotToOwner(player, true);
        }
        else if (!state.Initialized)
        {
            MigratePlayerStatsOnce(player, state);
        }
    }

    public static void Tick(EntityPlayer player)
    {
        if (!IsServerAuthority || player == null || player.world == null || player.Stats == null || player.IsDead())
            return;
        if (RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))
            return;

        EnsurePlayerReady(player);
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null)
            return;
        if (!RebirthSurvivorMetabolismCreationGate.IsLinkedToCommittedOrigin(player, state))
            return;

        // Native god mode continuously restores the normal player stats. Do the same for the
        // custom Energy reserve before the one-second metabolism gate so toggling god mode never
        // leaves a stale partially-depleted Energy value visible beneath a restored 100 cap.
        if (IsGodModeActive(player))
            state.Energy = RebirthMetabolismConfig.EnergyMax;

        float nowReal = Time.realtimeSinceStartup;
        if (state.NextRealTickTime > nowReal)
            return;
        state.NextRealTickTime = nowReal + RebirthMetabolismConfig.UpdateRealSeconds;

        // Every metabolism process advances from ACTIVE REAL PLAY TIME only. World/day length
        // is intentionally irrelevant: a 30-, 60-, or 180-minute 7DTD day produces the same
        // hunger, thirst, Energy, gastric transit and absorption over the same amount of play.
        float deltaRealSeconds = state.LastRealProcessTime > 0f ? nowReal - state.LastRealProcessTime : 0f;
        state.LastRealProcessTime = nowReal;
        deltaRealSeconds = Mathf.Clamp(deltaRealSeconds, 0f, Mathf.Max(2f, RebirthMetabolismConfig.UpdateRealSeconds * 2.5f));
        float deltaRealMinutes = deltaRealSeconds / 60f;

        ulong now = player.world.GetWorldTime();
        state.LastProcessedWorldTime = now; // retained only for diagnostics/migration, never for metabolism pacing.
        if (state.LastStamina < 0f)
            state.LastStamina = player.Stats.Stamina.Value;

        if (state.AutoSipCooldownRemainingRealSeconds > 0f)
            state.AutoSipCooldownRemainingRealSeconds = Mathf.Max(0f, state.AutoSipCooldownRemainingRealSeconds - deltaRealSeconds);
        AdvanceTimedEffects(state, deltaRealSeconds);

        RebirthMetabolismModifiers mods = RebirthMetabolismModifierResolver.Resolve(player, state);
        float capacity = Mathf.Max(100f, mods.StomachCapacity);
        bool traceActive = IsDebugTraceActive(player);
        bool captureTrace = DebugEnabled || traceActive;

        // Capture the pre-tick state only when diagnostics are active. Ordinary simulation
        // must not pay for trace-only compartment traversals.
        // Capture the pre-tick state so trace logging can show the actual path through stomach,
        // intestine and body reserves instead of only the final post-absorption snapshot.
        float traceHydrationBefore = captureTrace ? player.Stats.Water.Value : 0f;
        float traceNutritionBefore = captureTrace ? player.Stats.Food.Value : 0f;
        float traceEnergyBefore = captureTrace ? state.Energy : 0f;
        float traceStomachFluidBefore = captureTrace ? GetFluidMl(state) : 0f;
        float traceStomachSolidBefore = captureTrace ? GetSolidMl(state) : 0f;
        float traceIntestinalFluidBefore = captureTrace ? GetIntestinalFluidMl(state) : 0f;
        float traceIntestinalSolidBefore = captureTrace ? GetIntestinalSolidMl(state) : 0f;
        float traceIntestinalNutritionBefore = captureTrace ? GetIntestinalNutritionUnits(state) : 0f;

        float activityHydrationMlPerReal60Minutes;
        float energyUsePerRealMinute;
        ResolveActivity(player, state, deltaRealMinutes, out activityHydrationMlPerReal60Minutes, out energyUsePerRealMinute);

        float heatHydrationMlPerReal60Minutes;
        float coldFoodPerReal60Minutes;
        ResolveTemperature(player, mods, out heatHydrationMlPerReal60Minutes, out coldFoodPerReal60Minutes);

        // The vanilla dysentery attack buffs provide the episode cadence. A newly-started
        // attack is now a real compartment event: diarrhea expels intestine contents and the
        // next attack vomits stomach contents. This alternates for as long as dysentery lasts.
        ProcessDysenteryEpisode(player, state);

        float illnessHydrationMlPerReal60Minutes;
        float illnessFoodPerReal60Minutes;
        float digestiveDamagePerReal60Minutes;
        ResolveIllness(player, out illnessHydrationMlPerReal60Minutes, out illnessFoodPerReal60Minutes, out digestiveDamagePerReal60Minutes);

        float basalHydrationMlPerRealMinute = (RebirthMetabolismConfig.BaseFluidNeedMlPerReal60Minutes / 60f) * mods.HydrationRequirement;
        float hydrationLossMlPerRealMinute = Mathf.Max(0f, (basalHydrationMlPerRealMinute
            + activityHydrationMlPerReal60Minutes / 60f
            + heatHydrationMlPerReal60Minutes / 60f
            + illnessHydrationMlPerReal60Minutes / 60f) * mods.TotalHydrationDemand);
        float hydrationLossPoints = hydrationLossMlPerRealMinute * deltaRealMinutes / RebirthMetabolismConfig.MlPerHydrationPoint;
        if (hydrationLossPoints > 0f)
            player.Stats.Water.Value = player.Stats.Water.Value - hydrationLossPoints;

        if (player.Stats.Water.Value <= 0.01f && RebirthMetabolismConfig.CriticalDehydrationHealthLossPerRealMinute > 0f)
            player.Stats.Health.Value = player.Stats.Health.Value - RebirthMetabolismConfig.CriticalDehydrationHealthLossPerRealMinute * deltaRealMinutes;

        float basalFoodPerRealMinute = (RebirthMetabolismConfig.BaseFoodNeedUnitsPerReal60Minutes / 60f) * mods.FoodRequirement;
        // Resting metabolism is paid by this modest basal Nutrition use. As Nutrition/Hydration fall,
        // the sustainable Energy ceiling falls with them; there is no second arbitrary idle Energy burn.
        float foodUsePerRealMinute = Mathf.Max(0f, (basalFoodPerRealMinute
            + coldFoodPerReal60Minutes / 60f
            + illnessFoodPerReal60Minutes / 60f) * mods.TotalNutritionDemand);
        if (foodUsePerRealMinute > 0f)
            player.Stats.Food.Value = player.Stats.Food.Value - foodUsePerRealMinute * deltaRealMinutes;

        float energyRecoveryPerRealMinute = ProcessEnergy(player, state, mods, energyUsePerRealMinute, deltaRealMinutes);

        float intestinalFluidAbsorptionRate = RebirthMetabolismConfig.BaseIntestinalFluidAbsorptionMlPerRealMinute * mods.FluidAbsorption;
        float solidGastricRate = RebirthMetabolismConfig.BaseSolidGastricEmptyingMlPerRealMinute * mods.DigestionSpeed;
        float intestinalNutrientAbsorptionRate = RebirthMetabolismConfig.BaseIntestinalNutrientAbsorptionUnitsPerRealMinute * mods.DigestionSpeed;

        float gastricFluidMovedMl;
        float gastricSolidMovedMl;
        float gastricMovedMl = ProcessGastricTransit(state, mods, capacity, deltaRealSeconds, out gastricFluidMovedMl, out gastricSolidMovedMl);
        float traceIntestinalFluidAfterGastric = captureTrace ? GetIntestinalFluidMl(state) : 0f;
        float traceIntestinalSolidAfterGastric = captureTrace ? GetIntestinalSolidMl(state) : 0f;
        float traceIntestinalNutritionAfterGastric = captureTrace ? GetIntestinalNutritionUnits(state) : 0f;

        // Intestinal contents are a real persistent stage. Newly-arrived material must remain
        // visible for its residence period before any part of it can become a body reserve.
        AdvanceIntestinalResidence(state, deltaRealSeconds);

        float hydrationPointsAdded;
        float beverageEnergyAdded;
        float energyBeforeIntestinalAbsorption = state.Energy;
        float fluidNutritionAbsorbedUnits;
        float fluidNutritionPointsAdded;
        float fluidAbsorbedMl = ProcessIntestinalFluidAbsorption(player, state, mods,
            intestinalFluidAbsorptionRate * (deltaRealSeconds / 60f), out hydrationPointsAdded, out beverageEnergyAdded,
            out fluidNutritionAbsorbedUnits, out fluidNutritionPointsAdded);
        float solidNutritionPointsAdded;
        float solidNutritionAbsorbedUnits = ProcessIntestinalNutritionAbsorption(player, state, mods,
            intestinalNutrientAbsorptionRate * (deltaRealSeconds / 60f), out solidNutritionPointsAdded);
        float nutritionAbsorbedUnits = fluidNutritionAbsorbedUnits + solidNutritionAbsorbedUnits;
        float nutritionPointsAdded = fluidNutritionPointsAdded + solidNutritionPointsAdded;

        // Absorption can raise the sustainable ceiling, and some drinks carry immediate Energy.
        // Enforce the same reserve ceiling after the whole intestinal absorption stage so a drink
        // can never leave Energy above what the newly absorbed Hydration/Nutrition can support.
        ClampEnergyToReserveCeiling(player, state);
        beverageEnergyAdded = Mathf.Max(0f, state.Energy - energyBeforeIntestinalAbsorption);

        float perMinute = deltaRealMinutes > 0.00001f ? 1f / deltaRealMinutes : 0f;
        state.LastGastricFluidTransferMlPerRealMinute = gastricFluidMovedMl * perMinute;
        state.LastGastricSolidTransferMlPerRealMinute = gastricSolidMovedMl * perMinute;
        state.LastFluidAbsorbedMlPerRealMinute = fluidAbsorbedMl * perMinute;
        state.LastHydrationGainPointsPerRealMinute = hydrationPointsAdded * perMinute;
        state.LastNutritionAbsorbedUnitsPerRealMinute = nutritionAbsorbedUnits * perMinute;
        state.LastNutritionGainPointsPerRealMinute = nutritionPointsAdded * perMinute;
        state.LastBeverageEnergyGainPerRealMinute = beverageEnergyAdded * perMinute;
        float fluidActivity = intestinalFluidAbsorptionRate > 0.001f ? state.LastFluidAbsorbedMlPerRealMinute / intestinalFluidAbsorptionRate : 0f;
        float nutritionActivity = intestinalNutrientAbsorptionRate > 0.001f ? state.LastNutritionAbsorbedUnitsPerRealMinute / intestinalNutrientAbsorptionRate : 0f;
        state.LastIntestinalActivityPercent = Mathf.Clamp01(Mathf.Max(fluidActivity, nutritionActivity));

        ProcessDigestiveHealth(player, state, mods, digestiveDamagePerReal60Minutes, deltaRealMinutes);
        CleanupEntries(state);
        UpdateStatusBuffs(player, state, capacity);
        TryAutoSip(player, state, mods, capacity);

        float gastricRate = deltaRealSeconds > 0.001f ? gastricMovedMl / (deltaRealSeconds / 60f) : 0f;
        // The player-facing Food-use rate includes both baseline/cold/illness metabolism and
        // the Food currently being converted back into Energy. This keeps the UI honest: Food
        // should never appear to fall faster than the displayed rate simply because Energy is recovering.
        float energyRecoveryFoodPerRealMinute = state.LastEnergyRecoveryNutritionUsePerRealMinute;
        float totalFoodUsePerRealMinute = foodUsePerRealMinute + energyRecoveryFoodPerRealMinute;

        // This sequence-zero projection is only consumed by diagnostics.
        // Owner publication below builds and sequences its own authoritative snapshot.
        RebirthMetabolismSnapshot snapshot = default(RebirthMetabolismSnapshot);
        if (DebugEnabled || traceActive)
            snapshot = BuildSnapshot(player, state, mods,
            hydrationLossMlPerRealMinute * 60f, totalFoodUsePerRealMinute * 60f,
            energyUsePerRealMinute * Mathf.Max(0f, mods.EnergyUse), energyRecoveryPerRealMinute,
            intestinalFluidAbsorptionRate, solidGastricRate, intestinalNutrientAbsorptionRate, gastricRate);
        state.Touch();

        if (state.Revision != state.LastReplicatedRevision)
        {
            SendSnapshotToOwner(player, false);
            state.LastReplicatedRevision = state.Revision;
        }

        if (Time.realtimeSinceStartup >= nextPersistenceSaveRealTime)
        {
            nextPersistenceSaveRealTime = Time.realtimeSinceStartup + 30f;
            RebirthMetabolismStateRepository.SaveIfDirty("periodic");
        }

        if (DebugEnabled)
            Log.Out("[REBIRTH Metabolism] tick " + FormatSnapshot(snapshot));
        if (DebugEnabled || traceActive)
        {
            LogDetailedTickTrace(player, state, snapshot, deltaRealSeconds,
                traceHydrationBefore, traceNutritionBefore, traceEnergyBefore,
                traceStomachFluidBefore, traceStomachSolidBefore,
                traceIntestinalFluidBefore, traceIntestinalSolidBefore, traceIntestinalNutritionBefore,
                traceIntestinalFluidAfterGastric, traceIntestinalSolidAfterGastric, traceIntestinalNutritionAfterGastric,
                gastricFluidMovedMl, gastricSolidMovedMl, fluidAbsorbedMl, nutritionAbsorbedUnits,
                hydrationPointsAdded, nutritionPointsAdded, beverageEnergyAdded);
        }
    }

    private static void RemoveLegacyFoodWaterStatusBuffs(EntityPlayer player)
    {
        if (player == null || player.Buffs == null)
            return;
        string[] names =
        {
            "buffStatusHungry01", "buffStatusHungry02", "buffStatusHungry03",
            "buffStatusThirsty01", "buffStatusThirsty02", "buffStatusThirsty03"
        };
        for (int i = 0; i < names.Length; i++)
            if (player.Buffs.HasBuff(names[i]))
                player.Buffs.RemoveBuff(names[i]);
    }

    private static void NormalizeLoadedLiquidStacks(EntityPlayer player)
    {
        if (player == null || player.inventory == null || player.bag == null)
            return;

        ItemStack[] tool = player.inventory.ItemGrid.items;
        ItemStack[] bag = player.bag.ItemGrid.items;
        bool changed = false;

        for (int i = 0; i < tool.Length; i++)
        {
            changed |= SynchronizeLiquidVisualState(player, RebirthMetabolismSourceKind.HeldToolbelt, i, tool[i]);
            changed |= SplitOversizedLiquidStack(player, RebirthMetabolismSourceKind.HeldToolbelt, i, tool[i], tool, bag);
        }
        for (int i = 0; i < bag.Length; i++)
        {
            changed |= SynchronizeLiquidVisualState(player, RebirthMetabolismSourceKind.Backpack, i, bag[i]);
            changed |= SplitOversizedLiquidStack(player, RebirthMetabolismSourceKind.Backpack, i, bag[i], tool, bag);
        }

        if (changed && DebugEnabled)
            Log.Out("[REBIRTH Metabolism] liquid state normalized for entity=" + player.entityId);
    }

    private static bool SynchronizeLiquidVisualState(EntityPlayer player, RebirthMetabolismSourceKind sourceKind, int sourceSlot, ItemStack source)
    {
        if (source == null || source.IsEmpty() || source.itemValue == null)
            return false;

        RebirthConsumableDefinition def;
        if (!RebirthConsumableResolver.TryResolve(source.itemValue, out def) || def == null || !def.IsDrink)
            return false;

        float remaining;
        if (!RebirthLiquidContainerService.NormalizeState(source.itemValue, def, out remaining))
            return false;

        if (sourceKind == RebirthMetabolismSourceKind.Backpack)
            player.bag.SetSlot(sourceSlot, source);
        else
            player.inventory.SetItem(sourceSlot, source);
        return true;
    }

    private static bool SplitOversizedLiquidStack(EntityPlayer player, RebirthMetabolismSourceKind sourceKind, int sourceSlot,
        ItemStack source, ItemStack[] tool, ItemStack[] bag)
    {
        if (source == null || source.IsEmpty() || source.count <= 1 || source.itemValue == null)
            return false;

        RebirthConsumableDefinition def;
        if (!RebirthConsumableResolver.TryResolve(source.itemValue, out def) || def == null || !def.IsDrink)
            return false;

        // One ItemValue describes every jar in this homogeneous stack. Normalizing its
        // metadata does not require exploding it into single jars. Preserve legal stacks.
        RebirthLiquidStackCapacityPatch.Apply(source.itemValue.ItemClass);
        int limit = Math.Min(10, Math.Max(1, source.itemValue.ItemClass.Stacknumber.Value));
        if (source.count <= limit) return false;
        int extras = source.count - limit;
        source.count = limit;
        if (sourceKind == RebirthMetabolismSourceKind.Backpack)
            player.bag.SetSlot(sourceSlot, source);
        else
            player.inventory.SetItem(sourceSlot, source);

        while (extras > 0)
        {
            int amount = Math.Min(limit, extras);
            ItemStack individual = new ItemStack(source.itemValue.Clone(), amount);
            extras -= amount;
            bool placed = false;

            for (int i = 0; i < bag.Length && !placed; i++)
            {
                if (bag[i] == null || bag[i].IsEmpty())
                {
                    player.bag.SetSlot(i, individual);
                    // SetSlot updates the bound grid cell in place.
                    placed = true;
                }
            }

            // Never populate hidden or reserved animation toolbelt positions during migration.
            // The bag receives bounded stacks; a full bag uses the existing world-drop fallback.
            if (!placed && player.world != null && player.world.gameManager != null)
                player.world.gameManager.ItemDropServer(individual, player.GetPosition(), Vector3.zero);
        }
        return true;
    }

    private static void MigratePlayerStatsOnce(EntityPlayer player, RebirthMetabolismState state)
    {
        if (state.Initialized || player == null || player.Stats == null)
            return;

        // XML removes level-based Max passives. Preserve the loaded percentage if a legacy
        // save arrives with max values above the new explicitly-authored maximum.
        MigrateStatPercentage(player.Stats.Health);
        MigrateStatPercentage(player.Stats.Stamina);
        MigrateStatPercentage(player.Stats.Water);
        MigrateStatPercentage(player.Stats.Food);

        state.DigestiveHealth = Mathf.Clamp(state.DigestiveHealth <= 0f ? RebirthMetabolismConfig.BaseDigestiveHealth : state.DigestiveHealth, 0f, 100f);
        state.Initialized = true;
        state.Touch();
    }

    private static void MigrateStatPercentage(Stat stat)
    {
        if (stat == null)
            return;
        float oldMax = Mathf.Max(1f, stat.Max);
        float oldValue = stat.Value;
        // Force a passive recalculation on next native Tick; current XML determines the new Max.
        float expected = EffectManager.GetValue(stat.MaxPassive, _originalValue: 100f, _entity: stat.Entity);
        if (expected <= 0f) expected = 100f;
        if (Mathf.Abs(oldMax - expected) > 0.01f)
        {
            float percent = Mathf.Clamp01(oldValue / oldMax);
            stat.BaseMax = expected;
            stat.OriginalMax = 100f;
            stat.Value = percent * stat.ModifiedMax;
        }
    }

    public static RebirthConsumeResult ConsumeMatchingInventoryItem(EntityPlayer player, int itemType, ushort seed, bool autoSip)
    {
        if (!IsServerAuthority || player == null)
            return Fail(RebirthMetabolismConsumeFailure.NotAuthoritative, "Not authoritative.");
        if (RebirthCharacterCreationHoldService.IsHeld(player))
            return Fail(RebirthMetabolismConsumeFailure.NotAuthoritative, "Finish Survivor creation before using consumables.");

        RebirthMetabolismSourceKind kind;
        int slot;
        ItemStack stack;
        if (!TryFindMatchingStack(player, itemType, seed, out kind, out slot, out stack))
            return Fail(RebirthMetabolismConsumeFailure.ItemChanged, "Item is no longer available.");

        return ConsumeAt(player, kind, slot, stack, autoSip);
    }

    public static RebirthConsumeResult ConsumeHydrationSlot(EntityPlayer player, bool autoSip)
    {
        if (!IsServerAuthority || player == null)
            return Fail(RebirthMetabolismConsumeFailure.NotAuthoritative, "Not authoritative.");
        if (RebirthCharacterCreationHoldService.IsHeld(player))
            return Fail(RebirthMetabolismConsumeFailure.NotAuthoritative, "Finish Survivor creation before drinking.");
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null || state.HydrationSlotItem == null || state.HydrationSlotItem.IsEmpty())
            return Fail(RebirthMetabolismConsumeFailure.NoLiquid, "Hydration slot is empty.");
        return ConsumeAt(player, RebirthMetabolismSourceKind.HydrationSlot, -1, state.HydrationSlotItem, autoSip);
    }

    // Caller authenticates companion ownership/distance and supplies a revision-checked
    // source commit. The callback must place replacement AND siblings atomically.
    internal static RebirthConsumeResult ConsumeExternalSource(EntityPlayer player, ItemStack stack,
        Func<ItemStack, ItemStack, ItemStack, bool> commit)
    {
        if (!IsServerAuthority || player == null || commit == null)
            return Fail(RebirthMetabolismConsumeFailure.NotAuthoritative, "Not authoritative.");
        if (RebirthCharacterCreationHoldService.IsHeld(player))
            return Fail(RebirthMetabolismConsumeFailure.NotAuthoritative, "Finish Survivor creation before using consumables.");
        return ConsumeAt(player, RebirthMetabolismSourceKind.Backpack, -1, stack, false, commit);
    }

    private static RebirthConsumeResult ConsumeAt(EntityPlayer player, RebirthMetabolismSourceKind sourceKind, int slot, ItemStack stack, bool autoSip, Func<ItemStack, ItemStack, ItemStack, bool> externalCommit = null)
    {
        if (stack == null || stack.IsEmpty() || stack.itemValue == null)
            return Fail(RebirthMetabolismConsumeFailure.InvalidItem, "Invalid item.");

        ItemStack expectedSource = stack.Clone();
        stack = stack.Clone(); // Normalization and serving planning must not mutate live slots.

        RebirthConsumableDefinition def;
        if (!RebirthConsumableResolver.TryResolve(stack.itemValue, out def) || def == null)
            return Fail(RebirthMetabolismConsumeFailure.InvalidItem, "Item is not a metabolism consumable.");

        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null)
            return Fail(RebirthMetabolismConsumeFailure.InvalidPlayer, "No metabolism state.");

        if ((def.IsDrink || def.IsFood) && !TryReserveIngestionCapacity(state))
            return Fail(RebirthMetabolismConsumeFailure.IngestionQueueFull, "Digestion queue is full; nothing was consumed.");

        RebirthMetabolismModifiers mods = RebirthMetabolismModifierResolver.Resolve(player, state);
        float capacity = Mathf.Max(100f, mods.StomachCapacity);
        float fullness = GetFullnessMl(state);
        float freeCapacity = Mathf.Max(0f, capacity - fullness);

        if (def.IsDrink)
        {
            float remaining;
            bool normalizedContainer = RebirthLiquidContainerService.NormalizeState(stack.itemValue, def, out remaining);
            if (normalizedContainer && externalCommit == null)
            {
                if (!RebirthConsumptionSourceWriteback.TryCommit(player, sourceKind, slot, expectedSource, stack, state))
                    return Fail(RebirthMetabolismConsumeFailure.ItemChanged, "Item is no longer available.");
                expectedSource = stack.Clone();
            }
            if (remaining <= 0.01f)
                return Fail(RebirthMetabolismConsumeFailure.NoLiquid, "Container is empty.");

            RebirthMetabolismSnapshot before = BuildSnapshot(player, state, mods, 0f, 0f, 0f, 0f,
                RebirthMetabolismConfig.BaseIntestinalFluidAbsorptionMlPerRealMinute * mods.FluidAbsorption,
                RebirthMetabolismConfig.BaseSolidGastricEmptyingMlPerRealMinute * mods.DigestionSpeed,
                RebirthMetabolismConfig.BaseIntestinalNutrientAbsorptionUnitsPerRealMinute * mods.DigestionSpeed, 0f);

            float sipTarget = autoSip ? (def.AutoSipMl > 0f ? def.AutoSipMl : RebirthMetabolismConfig.AutoSipDefaultMl) : def.ManualSipMl;
            if (sipTarget <= 0f) sipTarget = autoSip ? RebirthMetabolismConfig.AutoSipDefaultMl : 125f;

            // Manual drinking is a physical action, not an automatic reserve optimizer.
            // A player who presses Use should drink the authored sip size whenever the container
            // has that much liquid and the stomach has room, even when current + pending Hydration
            // already projects near 100. Only auto-sip is allowed to trim a serving to the amount
            // needed to reach its projected-Hydration stop target.
            float usefulMl = float.MaxValue;
            if (autoSip && !def.AllowConsumeWhenHydrated)
            {
                float targetHydration = player.Stats.Water.ModifiedMax * RebirthMetabolismConfig.AutoSipStopProjectedPercent;
                float deficitPoints = Mathf.Max(0f, targetHydration - before.ProjectedHydration);
                usefulMl = deficitPoints * RebirthMetabolismConfig.MlPerHydrationPoint
                    / Mathf.Max(0.01f, mods.FluidUtilization * def.LiquidHydrationYield);

                if (usefulMl <= 0.5f)
                    return Fail(RebirthMetabolismConsumeFailure.AlreadyHydrated, "Auto-sip stopped: projected Hydration is already covered.");
            }

            RebirthDrinkServing serving = RebirthConsumptionServing.Drink(sipTarget, remaining, freeCapacity, usefulMl);
            if (!serving.Accepted)
                return Fail(RebirthMetabolismConsumeFailure.TooFull, "Stomach is full. No liquid was consumed.");
            float consumed = serving.ConsumedMl;
            bool stomachLimited = serving.StomachLimited;

            RebirthDrinkSourcePlan sourcePlan = RebirthDrinkSourcePlan.Create(stack, def, remaining - consumed);
            ItemStack untouchedSiblings = sourcePlan.UntouchedSiblings;
            stack = sourcePlan.ConsumedSource;

            RebirthIngestionEntry entry = new RebirthIngestionEntry
            {
                EntryId = state.AllocateEntryId(),
                Kind = RebirthIngestionKind.Fluid,
                SourceItemName = def.ItemName,
                LiquidProfileId = RebirthLiquidContainerService.GetLiquidProfile(stack.itemValue, def),
                DigestionProfileId = string.Empty,
                FoodSafetyProfileId = def.FoodSafetyProfile,
                IngestedWorldTime = player.world.GetWorldTime(),
                RemainingFluidVolumeMl = consumed,
                RemainingSolidVolumeMl = 0f,
                RemainingNutritionUnits = def.NutritionUnits > 0f && def.InitialVolumeMl > 0f ? def.NutritionUnits * (consumed / def.InitialVolumeMl) : 0f,
                RemainingEnergyUnits = def.EnergyUnits > 0f && def.InitialVolumeMl > 0f ? def.EnergyUnits * (consumed / def.InitialVolumeMl) : 0f,
                IntestinalFluidVolumeMl = 0f,
                IntestinalNutritionUnits = 0f,
                IntestinalEnergyUnits = 0f,
                FluidIsMealBound = def.NutritionUnits > 0.001f,
                FluidHoldSecondsRemaining = def.NutritionUnits > 0.001f
                    ? RebirthMetabolismConfig.MealFluidHoldRealSeconds
                    : RebirthMetabolismConfig.ClearLiquidHoldRealSeconds,
                SolidHoldSecondsRemaining = 0f,
                FluidGastricHalfTimeSecondsSnapshot = RebirthMetabolismConfig.GetFluidGastricHalfTimeRealSeconds(def.NutritionUnits > 0.001f),
                FluidYieldMultiplierSnapshot = def.LiquidHydrationYield,
                NutrientYieldMultiplierSnapshot = mods.NutrientUtilization,
                DigestionRateMultiplierSnapshot = 1f,
                PreparedDrinkSpecialEffectDurationSeconds = RebirthDrinkPreparationSignatureService.GetPreparedSpecialEffectDurationSeconds(stack.itemValue),
                PreparedDrinkSpecialEffectActivated = false
            };
            remaining = sourcePlan.RemainingMl;
            bool createdEmpty = sourcePlan.CreatedEmptyItem;
            bool committed = externalCommit != null
                ? externalCommit(expectedSource, sourcePlan.Replacement, untouchedSiblings)
                : RebirthConsumptionSourceWriteback.TryCommit(player, sourceKind, slot, expectedSource, sourcePlan.Replacement, state);
            if (!committed)
                return Fail(RebirthMetabolismConsumeFailure.ItemChanged, "Item is no longer available.");
            MergeOrAddEntry(state, entry);
            if(externalCommit == null && untouchedSiblings!=null&&!untouchedSiblings.IsEmpty())
            {
                ItemStack remainder=untouchedSiblings;
                bool accepted=(player.bag!=null&&player.bag.AddItem(remainder))||remainder.count<=0;
                if(!accepted&&player.inventory!=null)accepted=player.inventory.AddItem(remainder)||remainder.count<=0;
                if(!accepted&&remainder.count>0&&player.world!=null&&player.world.gameManager!=null)
                    player.world.gameManager.ItemDropServer(remainder,player.GetPosition(),Vector3.zero);
            }

            FirePreservedUseEffects(player, stack.itemValue, true);
            RebirthStressService.Drink(player,def.ItemName,consumed/Mathf.Max(1f,def.ContainerCapacityMl));
            RebirthTraitSupportService.TryApplyFromItem(player, def.ItemName, "trait-support-metabolism-drink");
            state.Touch();
            SendSnapshotToOwner(player, false);
            return new RebirthConsumeResult
            {
                Success = true,
                Failure = RebirthMetabolismConsumeFailure.None,
                ConsumedMl = consumed,
                RemainingMl = remaining,
                AddedEntryId = entry.EntryId,
                CreatedEmptyItem = createdEmpty,
                Message = stomachLimited
                    ? "Drank " + consumed.ToString("0", CultureInfo.InvariantCulture) + " mL. Stomach is full; the rest stayed in the container."
                    : "Drank " + consumed.ToString("0", CultureInfo.InvariantCulture) + " mL."
            };
        }

        if (def.IsFood)
        {
            RebirthFoodServing serving = RebirthConsumptionServing.Food(def.StomachVolumeMl,
                def.FoodWaterMl, freeCapacity, RebirthMetabolismConfig.FoodMinimumFitFraction);

            // Meals remain all-or-nothing items, but the stomach may stretch slightly to finish one.
            // If at least the configured fraction (80% by default) of the meal can physically fit,
            // consume the whole food item and let the stomach temporarily exceed nominal capacity.
            // If less than that can fit, consume nothing. This avoids both partial-food bookkeeping
            // and the frustrating case where a nearly fitting meal is rejected.
            if (!serving.Accepted)
                return Fail(RebirthMetabolismConsumeFailure.TooFull,
                    "Not enough room in the stomach for this meal. At least "
                    + (RebirthMetabolismConfig.FoodMinimumFitFraction * 100f).ToString("0", CultureInfo.InvariantCulture)
                    + "% must fit; the food was not consumed.");

            bool mealOverfillsStomach = serving.OverfillsStomach;

            RebirthIngestionEntry entry = new RebirthIngestionEntry
            {
                EntryId = state.AllocateEntryId(),
                Kind = RebirthIngestionKind.Food,
                SourceItemName = def.ItemName,
                LiquidProfileId = def.FoodWaterMl > 0f ? "foodWater" : string.Empty,
                DigestionProfileId = def.DigestionProfile,
                FoodSafetyProfileId = def.FoodSafetyProfile,
                IngestedWorldTime = player.world.GetWorldTime(),
                RemainingFluidVolumeMl = Mathf.Max(0f, def.FoodWaterMl),
                RemainingSolidVolumeMl = Mathf.Max(0f, def.StomachVolumeMl),
                RemainingNutritionUnits = Mathf.Max(0f, def.NutritionUnits),
                RemainingEnergyUnits = Mathf.Max(0f, def.EnergyUnits),
                IntestinalFluidVolumeMl = 0f,
                IntestinalNutritionUnits = 0f,
                IntestinalEnergyUnits = 0f,
                FluidIsMealBound = def.FoodWaterMl > 0.001f,
                FluidHoldSecondsRemaining = def.FoodWaterMl > 0.001f ? RebirthMetabolismConfig.MealFluidHoldRealSeconds : 0f,
                SolidHoldSecondsRemaining = def.StomachVolumeMl > 0.001f ? RebirthMetabolismConfig.GetFoodGastricLagRealSeconds(def.DigestionProfile) : 0f,
                FluidGastricHalfTimeSecondsSnapshot = def.FoodWaterMl > 0.001f ? RebirthMetabolismConfig.MealFluidGastricHalfTimeRealSeconds : 0f,
                FluidYieldMultiplierSnapshot = 1f,
                NutrientYieldMultiplierSnapshot = mods.NutrientUtilization,
                DigestionRateMultiplierSnapshot = RebirthMetabolismConfig.GetDigestionProfileMultiplier(def.DigestionProfile) * GetMealEnzymeSnapshot(state),
                PreparedMealEnergyEfficiencyMultiplier = RebirthFoodFarmingButcherySignatureService.GetPreparedMealEnergyEfficiency(stack.itemValue)
            };
            if (!DecrementSource(player, sourceKind, slot, expectedSource, state, externalCommit))
                return Fail(RebirthMetabolismConsumeFailure.ItemChanged, "Item is no longer available.");
            MergeOrAddEntry(state, entry);
            FirePreservedUseEffects(player, stack.itemValue, false);
            RebirthTraitSupportService.TryApplyFromItem(player, def.ItemName, "trait-support-metabolism-food");
            // Dry food can have a real hydration cost; zero food water alone is not thirst.
            float hydrationCostMl = Mathf.Max(0f, RebirthConsumableResolver.GetFloat(stack.itemValue.ItemClass, "RebirthHydrationCostMl", 0f));
            player.Stats.Water.Value = Mathf.Max(0f, player.Stats.Water.Value - hydrationCostMl / Mathf.Max(0.001f, RebirthMetabolismConfig.MlPerHydrationPoint));
            state.Touch();
            SendSnapshotToOwner(player, false);
            return new RebirthConsumeResult
            {
                Success = true,
                Failure = RebirthMetabolismConsumeFailure.None,
                AddedEntryId = entry.EntryId,
                Message = mealOverfillsStomach ? "Meal consumed. You are stuffed." : "Meal entered digestion."
            };
        }

        if (def.IsSupplement)
        {
            if (!DecrementSource(player, sourceKind, slot, expectedSource, state, externalCommit))
                return Fail(RebirthMetabolismConsumeFailure.ItemChanged, "Item is no longer available.");
            ApplySupplement(player, state, def.SupplementProfile);
            FirePreservedUseEffects(player, stack.itemValue, false);
            RebirthTraitSupportService.TryApplyFromItem(player, def.ItemName, "trait-support-metabolism-supplement");
            state.Touch();
            SendSnapshotToOwner(player, false);
            return new RebirthConsumeResult { Success = true, Message = "Supplement taken." };
        }

        return Fail(RebirthMetabolismConsumeFailure.InvalidItem, "Unsupported consumable.");
    }

    private static void FirePreservedUseEffects(EntityPlayer player, ItemValue itemValue, bool isDrink)
    {
        if (player == null || itemValue == null)
            return;

        // We still fire the native item-use event so healing, buffs, quests and other secondary
        // authored effects survive. Food/Water themselves are different: those reserves belong
        // exclusively to the REBIRTH digestion pipeline and may not jump on the use event.
        float nutritionBeforeEvent = player.Stats != null ? player.Stats.Food.Value : 0f;
        float hydrationBeforeEvent = player.Stats != null ? player.Stats.Water.Value : 0f;
        float nativeFoodPayloadBefore = player.Buffs != null ? player.Buffs.GetCustomVar("$foodAmountAdd") : 0f;
        float nativeWaterPayloadBefore = player.Buffs != null ? player.Buffs.GetCustomVar("$waterAmountAdd") : 0f;
        try
        {
            player.MinEventContext.ItemValue = itemValue;
            QuestEventManager.Current.UsedItem(itemValue);
            player.FireEvent(MinEvent.End[0]);

            EntityPlayerLocal local = player as EntityPlayerLocal;
            ItemClass itemClass = itemValue.ItemClass;
            if (local != null && itemClass != null && itemClass.Actions != null && itemClass.Actions.Length > 0)
            {
                ItemActionEat eat = itemClass.Actions[0] as ItemActionEat;
                if (eat != null && eat.smellUse > 0f)
                    local.Stealth.SetSmellEat(eat.smellUse);
            }
        }
        catch (Exception ex)
        {
            if (DebugEnabled)
                Log.Warning("[REBIRTH Metabolism] preserved consumable event failed: " + ex.Message);
        }
        finally
        {
            // Inherited vanilla consumables can recreate these payload CVars even when their
            // child item XML no longer contains the ModifyCVar node. Restore the pre-use values
            // so buffProcessConsumables has no native Food/Water payload left to process later.
            if (player.Buffs != null)
            {
                player.Buffs.SetCustomVar("$foodAmountAdd", nativeFoodPayloadBefore);
                player.Buffs.SetCustomVar("$waterAmountAdd", nativeWaterPayloadBefore);
            }

            if (player.Stats != null)
            {
                if (player.Stats.Food.Value > nutritionBeforeEvent)
                {
                    if (DebugEnabled)
                        Log.Out("[REBIRTH Metabolism] blocked immediate pre-digestion Nutrition gain="
                            + (player.Stats.Food.Value - nutritionBeforeEvent).ToString("0.000", CultureInfo.InvariantCulture));
                    player.Stats.Food.Value = nutritionBeforeEvent;
                }
                if (player.Stats.Water.Value > hydrationBeforeEvent)
                {
                    if (DebugEnabled)
                        Log.Out("[REBIRTH Metabolism] blocked immediate pre-absorption Hydration gain="
                            + (player.Stats.Water.Value - hydrationBeforeEvent).ToString("0.000", CultureInfo.InvariantCulture));
                    player.Stats.Water.Value = hydrationBeforeEvent;
                }
            }
        }
    }

    private static void ApplySupplement(EntityPlayer player, RebirthMetabolismState state, string profile)
    {
        string p = (profile ?? string.Empty).Trim();
        if (p.Equals("digestiveEnzymes", StringComparison.OrdinalIgnoreCase))
            AddTimedEffect(state, "digestiveEnzymes", RebirthMetabolismConfig.DigestiveEnzymesRealSeconds);
        else if (p.Equals("electrolytes", StringComparison.OrdinalIgnoreCase))
            AddTimedEffect(state, "electrolytes", RebirthMetabolismConfig.ElectrolytesRealSeconds);
        else if (p.Equals("probiotics", StringComparison.OrdinalIgnoreCase))
            AddTimedEffect(state, "probiotics", RebirthMetabolismConfig.ProbioticsRealSeconds);
        else if (p.Equals("prebiotic", StringComparison.OrdinalIgnoreCase) || p.Equals("fiber", StringComparison.OrdinalIgnoreCase))
            AddTimedEffect(state, "prebiotic", RebirthMetabolismConfig.PrebioticRealSeconds);
    }

    private static void AddTimedEffect(RebirthMetabolismState state, string profile, float durationRealSeconds)
    {
        durationRealSeconds = Mathf.Max(0f, durationRealSeconds);
        for (int i = 0; i < state.TimedEffects.Count; i++)
        {
            RebirthMetabolismTimedEffect e = state.TimedEffects[i];
            if (e != null && string.Equals(e.ProfileId, profile, StringComparison.OrdinalIgnoreCase))
            {
                e.RemainingRealSeconds = Mathf.Max(e.RemainingRealSeconds, durationRealSeconds);
                e.Stacks = Math.Min(3, e.Stacks + 1);
                return;
            }
        }
        state.TimedEffects.Add(new RebirthMetabolismTimedEffect { ProfileId = profile, RemainingRealSeconds = durationRealSeconds, Stacks = 1 });
    }

    private static float GetMealEnzymeSnapshot(RebirthMetabolismState state)
    {
        return HasTimedEffect(state, "digestiveEnzymes") ? 1.20f : 1f;
    }

    /// <summary>
    /// Energy is the immediate work reserve between the long-term body reserves and Stamina.
    /// Nutrition supplies that reserve; Hydration changes how efficiently the body can turn the
    /// available Nutrition into usable Energy. The sustainable ceiling has a small configurable
    /// floor, but activity can still spend current Energy all the way to zero.
    /// </summary>
    private static bool IsGodModeActive(EntityPlayer player)
    {
        return player != null && player.IsGodMode.Value;
    }

    public static float GetEnergyReserveCeiling(EntityPlayer player)
    {
        if (player == null || player.Stats == null || player.Stats.Water == null || player.Stats.Food == null)
            return RebirthMetabolismConfig.EnergyMax;

        // God mode is a native "fully restored" state. Keep the custom Energy reserve in the same
        // state instead of only allowing the sustainable cap to return to 100.
        if (IsGodModeActive(player))
            return RebirthMetabolismConfig.EnergyMax;

        float hydrationPercent = player.Stats.Water.ModifiedMax > 0.001f
            ? Mathf.Clamp01(player.Stats.Water.Value / player.Stats.Water.ModifiedMax)
            : 0f;
        float nutritionPercent = player.Stats.Food.ModifiedMax > 0.001f
            ? Mathf.Clamp01(player.Stats.Food.Value / player.Stats.Food.ModifiedMax)
            : 0f;

        return RebirthMetabolismConfig.GetEnergyReserveCeiling(hydrationPercent, nutritionPercent);
    }

    public static void ClampEnergyToReserveCeiling(EntityPlayer player, RebirthMetabolismState state)
    {
        if (state == null) return;
        float ceiling = GetEnergyReserveCeiling(player);
        state.Energy = Mathf.Clamp(state.Energy, 0f, Mathf.Max(0f, ceiling));
    }

    private static float ProcessEnergy(EntityPlayer player, RebirthMetabolismState state, RebirthMetabolismModifiers mods, float usePerRealMinute, float deltaRealMinutes)
    {
        if (state != null)
            state.LastEnergyRecoveryNutritionUsePerRealMinute = 0f;
        if (state == null || player == null || player.Stats == null || deltaRealMinutes <= 0f)
            return 0f;

        if (IsGodModeActive(player))
        {
            state.Energy = RebirthMetabolismConfig.EnergyMax;
            return 0f;
        }

        float ceilingBeforeWork = GetEnergyReserveCeiling(player);
        state.Energy = Mathf.Clamp(state.Energy, 0f, ceilingBeforeWork);
        state.Energy = Mathf.Clamp(state.Energy - Mathf.Max(0f, usePerRealMinute) * Mathf.Max(0f, mods.EnergyUse) * deltaRealMinutes,
            0f, ceilingBeforeWork);

        float activity = Mathf.Max(0f, state.SmoothedActivity);
        float recoveryFactor = GetEnergyRecoveryFactor(activity);
        float foodFloor = player.Stats.Food.ModifiedMax * RebirthMetabolismConfig.EnergyRecoveryFoodFloorPercent;
        if (recoveryFactor <= 0f || player.Stats.Food.Value <= foodFloor || state.Energy >= ceilingBeforeWork - 0.001f)
            return 0f;

        // Professional Cooking is digestion-time efficiency, never instant Energy/Nutrition. The
        // preparation snapshot only becomes active after that prepared meal has physically reached
        // the intestinal nutrition compartment. Multiple meals do not multiply; the best active
        // prepared-meal snapshot applies while its nutrients are present.
        float preparedEfficiency = GetPreparedMealEnergyEfficiencyMultiplier(state);
        float hydrationRecoveryMultiplier = GetEnergyRecoveryHydrationMultiplier(player);
        float requested = RebirthMetabolismConfig.EnergyRecoveryPerRealMinute * recoveryFactor * hydrationRecoveryMultiplier
            * Mathf.Max(0f, mods.EnergyRecovery) * preparedEfficiency * deltaRealMinutes;
        requested = Mathf.Min(requested, Mathf.Max(0f, ceilingBeforeWork - state.Energy));
        float foodCostPerEnergy = Mathf.Max(0f, RebirthMetabolismConfig.FoodUnitsPerEnergyRecovered) / Mathf.Max(1f, preparedEfficiency);
        if (foodCostPerEnergy > 0f)
        {
            float availableFood = Mathf.Max(0f, player.Stats.Food.Value - foodFloor);
            requested = Mathf.Min(requested, availableFood / foodCostPerEnergy);
        }

        float beforeRecovery = state.Energy;
        float foodSpent = 0f;
        if (requested > 0f)
        {
            state.Energy += requested;
            if (foodCostPerEnergy > 0f)
            {
                foodSpent = requested * foodCostPerEnergy;
                player.Stats.Food.Value -= foodSpent;
            }
            ClampEnergyToReserveCeiling(player, state);
        }

        state.LastEnergyRecoveryNutritionUsePerRealMinute = deltaRealMinutes > 0.00001f ? foodSpent / deltaRealMinutes : 0f;
        float actuallyRecovered = Mathf.Max(0f, state.Energy - beforeRecovery);
        return deltaRealMinutes > 0.00001f ? actuallyRecovered / deltaRealMinutes : 0f;
    }

    private static float GetPreparedMealEnergyEfficiencyMultiplier(RebirthMetabolismState state)
    {
        if (state == null || state.IngestionEntries == null) return 1f;
        float best = 1f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry entry = state.IngestionEntries[i];
            if (entry == null || entry.Kind != RebirthIngestionKind.Food || entry.IntestinalNutritionUnits <= 0.001f) continue;
            best = Mathf.Max(best, Mathf.Clamp(entry.PreparedMealEnergyEfficiencyMultiplier, 1f, 2f));
        }
        return best;
    }

    private static float GetEnergyRecoveryFactor(float activity)
    {
        activity = Mathf.Max(0f, activity);
        if (activity < 0.5f) return 1f;       // idle: full recovery
        if (activity < 1.5f) return 0.15f;    // walking/light work: only a little recovery
        if (activity < 2.5f) return 0.05f;    // moderate work: effectively no recovery
        return 0f;                            // running/extreme work: no recovery
    }

    private static float EstimateEnergyRecoveryPerRealMinute(EntityPlayer player, RebirthMetabolismState state, RebirthMetabolismModifiers mods)
    {
        if (player == null || player.Stats == null || state == null)
            return 0f;
        float ceiling = GetEnergyReserveCeiling(player);
        float foodFloor = player.Stats.Food.ModifiedMax * RebirthMetabolismConfig.EnergyRecoveryFoodFloorPercent;
        if (player.Stats.Food.Value <= foodFloor || state.Energy >= ceiling - 0.001f)
            return 0f;
        return RebirthMetabolismConfig.EnergyRecoveryPerRealMinute
            * GetPreparedMealEnergyEfficiencyMultiplier(state)
            * GetEnergyRecoveryFactor(state.SmoothedActivity)
            * GetEnergyRecoveryHydrationMultiplier(player)
            * Mathf.Max(0f, mods.EnergyRecovery);
    }

    public static float GetEnergyRecoveryHydrationMultiplier(EntityPlayer player)
    {
        if (player == null || player.Stats == null || player.Stats.Water == null)
            return 1f;

        float max = Mathf.Max(1f, player.Stats.Water.ModifiedMax);
        float hydrationPercent = Mathf.Clamp01(player.Stats.Water.Value / max);
        return RebirthMetabolismConfig.GetEnergyHydrationEfficiency(hydrationPercent);
    }

    public static float GetStaminaRecoveryMultiplier(EntityPlayer player)
    {
        if (player == null || !IsServerAuthority)
            return 1f;
        if (RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))
            return 1f;
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null)
            return 1f;
        if (!RebirthSurvivorMetabolismCreationGate.IsLinkedToCommittedOrigin(player, state))
            return 1f;
        float e = Mathf.Clamp(state.Energy, 0f, RebirthMetabolismConfig.EnergyMax);
        if (e >= RebirthMetabolismConfig.EnergyLowThreshold)
            return 1f;
        if (e >= RebirthMetabolismConfig.EnergyCriticalThreshold)
        {
            float t = Mathf.InverseLerp(RebirthMetabolismConfig.EnergyCriticalThreshold, RebirthMetabolismConfig.EnergyLowThreshold, e);
            return Mathf.Lerp(RebirthMetabolismConfig.StaminaRegenAtCriticalEnergy, RebirthMetabolismConfig.StaminaRegenAtLowEnergy, t);
        }
        float c = RebirthMetabolismConfig.EnergyCriticalThreshold > 0.001f ? e / RebirthMetabolismConfig.EnergyCriticalThreshold : 0f;
        return Mathf.Lerp(RebirthMetabolismConfig.StaminaRegenAtZeroEnergy, RebirthMetabolismConfig.StaminaRegenAtCriticalEnergy, c);
    }

    private static void ResolveActivity(EntityPlayer player, RebirthMetabolismState state, float deltaRealMinutes,
        out float hydrationMlPerReal60Minutes, out float energyUsePerRealMinute)
    {
        float target = 0f;
        FastTags<TagGroup.Global> tag = player.CurrentMovementTag;
        if (tag.Test_AnySet(TagSwimmingRun)) target = 4f;
        else if (tag.Test_AnySet(TagRunning)) target = 3f;
        else if (tag.Test_AnySet(TagSwimming) || tag.Test_AnySet(TagClimbing)) target = 2.5f;
        else if (tag.Test_AnySet(TagJumping)) target = 2f;
        else if (tag.Test_AnySet(TagWalking)) target = 1f;

        // Movement tags capture sustained exertion. Net Stamina expenditure captures short,
        // expensive actions (repeated attacks, mining, jumping, etc.) that can happen between
        // metabolism ticks. It both raises the activity tier immediately and carries a small
        // direct Energy cost so a player repeatedly emptying Stamina cannot remain near 100 Energy.
        float stamina = player.Stats.Stamina.Value;
        float staminaSpent = 0f;
        if (state.LastStamina >= 0f && stamina < state.LastStamina - 0.05f)
        {
            staminaSpent = Mathf.Max(0f, state.LastStamina - stamina);
            float staminaTarget = Mathf.Clamp(1f + staminaSpent * 0.60f, 1f, 4f);
            target = Mathf.Max(target, staminaTarget);
        }
        state.LastStamina = stamina;

        float smoothed = Mathf.Lerp(state.SmoothedActivity, target, 0.35f);
        // Do not smooth away a real Stamina-spend spike on the tick where it happened.
        state.SmoothedActivity = staminaSpent > 0.05f ? Mathf.Max(smoothed, target) : smoothed;

        float a = state.SmoothedActivity;
        if (a < 0.5f)
        {
            hydrationMlPerReal60Minutes = 0f;
            energyUsePerRealMinute = 0f;
        }
        else if (a < 1.5f)
        {
            hydrationMlPerReal60Minutes = 25f;
            energyUsePerRealMinute = RebirthMetabolismConfig.EnergyLightUsePerRealMinute;
        }
        else if (a < 2.5f)
        {
            hydrationMlPerReal60Minutes = 100f;
            energyUsePerRealMinute = RebirthMetabolismConfig.EnergyModerateUsePerRealMinute;
        }
        else if (a < 3.5f)
        {
            hydrationMlPerReal60Minutes = 250f;
            energyUsePerRealMinute = RebirthMetabolismConfig.EnergyHighUsePerRealMinute;
        }
        else
        {
            hydrationMlPerReal60Minutes = 400f;
            energyUsePerRealMinute = RebirthMetabolismConfig.EnergyExtremeUsePerRealMinute;
        }

        energyUsePerRealMinute = RebirthEfficientMovementService.Apply(player, energyUsePerRealMinute);

        if (staminaSpent > 0f && deltaRealMinutes > 0.00001f)
        {
            float directStaminaCost = staminaSpent * Mathf.Max(0f, RebirthMetabolismConfig.EnergyPerStaminaSpent);
            energyUsePerRealMinute += directStaminaCost / deltaRealMinutes;
        }
    }

    private static void ResolveTemperature(EntityPlayer player, RebirthMetabolismModifiers mods, out float heatHydrationMlPerReal60Minutes, out float coldFoodPerReal60Minutes)
    {
        float core = player.PlayerStats.CoreTemp;
        if (core >= 100f) heatHydrationMlPerReal60Minutes = 450f;
        else if (core >= 90f) heatHydrationMlPerReal60Minutes = 250f;
        else if (core >= 80f) heatHydrationMlPerReal60Minutes = 100f;
        else if (core >= 75f) heatHydrationMlPerReal60Minutes = 25f;
        else heatHydrationMlPerReal60Minutes = 0f;
        heatHydrationMlPerReal60Minutes *= Mathf.Max(0f, mods.HeatPenalty);

        if (core <= 35f) coldFoodPerReal60Minutes = 5f;
        else if (core <= 45f) coldFoodPerReal60Minutes = 3f;
        else if (core <= 55f) coldFoodPerReal60Minutes = 1.5f;
        else if (core <= 62f) coldFoodPerReal60Minutes = 0.5f;
        else coldFoodPerReal60Minutes = 0f;
        coldFoodPerReal60Minutes *= Mathf.Max(0f, mods.ColdPenalty);
    }

    private static void ProcessDysenteryEpisode(EntityPlayer player, RebirthMetabolismState state)
    {
        if (player == null || state == null || player.Buffs == null)
            return;

        bool untreatedAttack = player.Buffs.HasBuff("buffDysentery01UntreatedDiarrhea");
        bool recoveringAttack = player.Buffs.HasBuff("buffDysentery01GetBetterDiarrhea");
        bool attackActive = untreatedAttack || recoveringAttack;

        if (!attackActive)
        {
            state.DysenteryAttackWasActive = false;
            return;
        }
        if (state.DysenteryAttackWasActive)
            return;

        state.DysenteryAttackWasActive = true;
        bool vomiting = state.DysenteryNextAttackVomiting;
        state.DysenteryNextAttackVomiting = !state.DysenteryNextAttackVomiting;

        // Getting-better attacks remain unpleasant but are less destructive than untreated ones.
        float contentFraction = recoveringAttack ? 0.20f : 0.35f;
        if (vomiting)
        {
            float stomachFluidBefore = GetFluidMl(state);
            float stomachSolidBefore = GetSolidMl(state);
            ExpelStomachFraction(state, contentFraction);
            float directFluidLossMl = recoveringAttack ? 20f : 35f;
            ApplyDirectHydrationLoss(player, directFluidLossMl);
            state.DigestiveHealth = Mathf.Max(0f, state.DigestiveHealth - (recoveringAttack ? 1.5f : 3f));
            CleanupEntries(state);
            GameManager.ShowTooltipMP(player, Localization.Get("xuiRebirthDysenteryVomit"), "");
            if (!TryPlaySound(player, "FuriousRamsayVomit"))
                TryPlaySound(player, "player#vomit");
            { if ((RebirthLogSettings.AutomaticLoggingDefault || DebugEnabled)) Log.Out("[REBIRTH Metabolism] Dysentery episode=vomit stomachLost="
                + (stomachFluidBefore - GetFluidMl(state)).ToString("0.0", CultureInfo.InvariantCulture) + "mL-liquid/"
                + (stomachSolidBefore - GetSolidMl(state)).ToString("0.0", CultureInfo.InvariantCulture) + "mL-solids"
                + " directFluidLoss=" + directFluidLossMl.ToString("0", CultureInfo.InvariantCulture) + "mL"); }
        }
        else
        {
            float gutFluidBefore = GetIntestinalFluidMl(state);
            float gutSolidBefore = GetIntestinalSolidMl(state);
            float gutNutritionBefore = GetIntestinalNutritionUnits(state);
            ExpelIntestinalFraction(state, contentFraction);
            float directFluidLossMl = recoveringAttack ? 45f : 75f;
            ApplyDirectHydrationLoss(player, directFluidLossMl);
            state.DigestiveHealth = Mathf.Max(0f, state.DigestiveHealth - (recoveringAttack ? 1f : 2f));
            CleanupEntries(state);
            TryPlaySound(player, "FuriousRamsayDiarrhea");
            GameManager.ShowTooltipMP(player, Localization.Get("xuiRebirthDysenteryDiarrhea"), "");
            { if ((RebirthLogSettings.AutomaticLoggingDefault || DebugEnabled)) Log.Out("[REBIRTH Metabolism] Dysentery episode=diarrhea intestineLost="
                + (gutFluidBefore - GetIntestinalFluidMl(state)).ToString("0.0", CultureInfo.InvariantCulture) + "mL-liquid/"
                + (gutSolidBefore - GetIntestinalSolidMl(state)).ToString("0.0", CultureInfo.InvariantCulture) + "mL-chyme/"
                + (gutNutritionBefore - GetIntestinalNutritionUnits(state)).ToString("0.00", CultureInfo.InvariantCulture) + " nutrition"
                + " directFluidLoss=" + directFluidLossMl.ToString("0", CultureInfo.InvariantCulture) + "mL"); }
        }

        state.Touch();
    }

    private static bool TryPlaySound(EntityPlayer player, string soundName)
    {
        if (player == null || string.IsNullOrEmpty(soundName))
            return false;
        try
        {
            player.PlayOneShot(soundName, false);
            return true;
        }
        catch (Exception ex)
        {
            if (DebugEnabled)
                Log.Warning("[REBIRTH Metabolism] sound playback failed sound=" + soundName
                    + " error=" + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static void ExpelStomachFraction(RebirthMetabolismState state, float fraction)
    {
        if (state == null) return;
        float keep = 1f - Mathf.Clamp01(fraction);
        for (int i = state.IngestionEntries.Count - 1; i >= 0; i--)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e == null) continue;
            e.RemainingFluidVolumeMl *= keep;
            e.RemainingSolidVolumeMl *= keep;
            e.RemainingNutritionUnits *= keep;
            e.RemainingEnergyUnits *= keep;
        }
    }

    private static void ExpelIntestinalFraction(RebirthMetabolismState state, float fraction)
    {
        if (state == null) return;
        float keep = 1f - Mathf.Clamp01(fraction);
        for (int i = state.IngestionEntries.Count - 1; i >= 0; i--)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e == null) continue;
            e.IntestinalFluidVolumeMl *= keep;
            e.IntestinalSolidVolumeMl *= keep;
            e.IntestinalNutritionUnits *= keep;
            e.IntestinalEnergyUnits *= keep;
        }
    }

    private static void ApplyDirectHydrationLoss(EntityPlayer player, float fluidLossMl)
    {
        if (player == null || player.Stats == null || player.Stats.Water == null || fluidLossMl <= 0f)
            return;
        player.Stats.Water.Value -= fluidLossMl / Mathf.Max(0.001f, RebirthMetabolismConfig.MlPerHydrationPoint);
    }

    private static void ResolveIllness(EntityPlayer player, out float hydrationMlPerReal60Minutes, out float foodPerReal60Minutes, out float digestiveDamagePerReal60Minutes)
    {
        hydrationMlPerReal60Minutes = 0f;
        foodPerReal60Minutes = 0f;
        digestiveDamagePerReal60Minutes = 0f;
        if (player.Buffs == null)
            return;

        if (player.Buffs.HasBuff("buffDysenteryMain") || player.Buffs.HasBuff("buffDysentery01Untreated") || player.Buffs.HasBuff("buffDysentery01GetBetter"))
        {
            // Ongoing fever/malaise cost. Acute vomiting/diarrhea loss is handled by the
            // physical compartment episodes above instead of being double-charged here.
            hydrationMlPerReal60Minutes += 150f;
            foodPerReal60Minutes += 0.5f;
            digestiveDamagePerReal60Minutes += 2f;
        }
        if (player.Buffs.HasBuff("buffDysentery01UntreatedDiarrhea"))
            digestiveDamagePerReal60Minutes += 3f;
        else if (player.Buffs.HasBuff("buffDysentery01GetBetterDiarrhea"))
            digestiveDamagePerReal60Minutes += 1.5f;
    }

    private static float ProcessGastricTransit(RebirthMetabolismState state, RebirthMetabolismModifiers mods,
        float stomachCapacityMl, float deltaRealSeconds, out float fluidMovedMl, out float solidMovedMl)
    {
        fluidMovedMl = 0f;
        solidMovedMl = 0f;
        if (state == null || deltaRealSeconds <= 0f || state.IngestionEntries.Count == 0)
            return 0f;

        float totalMovedMl = 0f;
        float stomachSolidAtStart = GetSolidMl(state);
        float solidLoad = stomachCapacityMl > 0.001f
            ? Mathf.Clamp01(stomachSolidAtStart / Mathf.Max(1f, stomachCapacityMl * 0.70f))
            : 0f;

        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry entry = state.IngestionEntries[i];
            if (entry == null)
                continue;

            // Clear fluid does not disappear from the stomach on the ingestion tick. Once its
            // short residence period is over it empties exponentially. Food in the stomach slows
            // clear water, but does not lock it to the solid meal: this preserves gastric sieving.
            if (entry.RemainingFluidVolumeMl > 0.001f)
            {
                float activeSeconds = ConsumeGastricHold(ref entry.FluidHoldSecondsRemaining, deltaRealSeconds);
                if (activeSeconds > 0f)
                {
                    float halfTime = entry.FluidGastricHalfTimeSecondsSnapshot > 0f
                        ? entry.FluidGastricHalfTimeSecondsSnapshot
                        : RebirthMetabolismConfig.GetFluidGastricHalfTimeRealSeconds(entry.FluidIsMealBound);

                    if (!entry.FluidIsMealBound && solidLoad > 0f)
                        halfTime *= Mathf.Lerp(1f, RebirthMetabolismConfig.ClearLiquidWithFoodHalfTimeMultiplier, solidLoad);
                    else if (entry.FluidIsMealBound)
                        halfTime /= Mathf.Sqrt(Mathf.Max(0.25f, mods.DigestionSpeed));

                    halfTime = Mathf.Max(1f, halfTime);
                    float fraction = 1f - Mathf.Exp(-0.69314718056f * activeSeconds / halfTime);
                    float beforeFluid = entry.RemainingFluidVolumeMl;
                    float movedFluid = Mathf.Clamp(beforeFluid * fraction, 0f, beforeFluid);
                    if (movedFluid > 0.0001f)
                    {
                        entry.RemainingFluidVolumeMl -= movedFluid;
                        if (entry.IntestinalFluidVolumeMl <= 0.001f)
                            entry.IntestinalFluidResidenceSecondsRemaining = RebirthMetabolismConfig.IntestinalFluidResidenceRealSeconds;
                        entry.IntestinalFluidVolumeMl += movedFluid;
                        totalMovedMl += movedFluid;
                        fluidMovedMl += movedFluid;

                        // Nutrient-bearing drinks carry their nutrition with the liquid phase.
                        if (entry.Kind == RebirthIngestionKind.Fluid && entry.RemainingNutritionUnits > 0f && beforeFluid > 0f)
                        {
                            float movedNutrition = entry.RemainingNutritionUnits * (movedFluid / beforeFluid);
                            entry.RemainingNutritionUnits = Mathf.Max(0f, entry.RemainingNutritionUnits - movedNutrition);
                            if (entry.IntestinalNutritionUnits <= 0.001f)
                                entry.IntestinalNutritionResidenceSecondsRemaining = RebirthMetabolismConfig.IntestinalFluidResidenceRealSeconds;
                            entry.IntestinalNutritionUnits += movedNutrition;
                        }
                        if (entry.RemainingEnergyUnits > 0f && beforeFluid > 0f)
                        {
                            float movedEnergy = entry.RemainingEnergyUnits * (movedFluid / beforeFluid);
                            entry.RemainingEnergyUnits = Mathf.Max(0f, entry.RemainingEnergyUnits - movedEnergy);
                            entry.IntestinalEnergyUnits += movedEnergy;
                        }
                    }
                }
            }

            // Solid food has an actual gastric lag while it is being mixed/ground. After that it
            // empties approximately linearly into the intestine. Nutrition follows the solid chyme.
            if (entry.RemainingSolidVolumeMl > 0.001f)
            {
                float activeSeconds = ConsumeGastricHold(ref entry.SolidHoldSecondsRemaining, deltaRealSeconds);
                if (activeSeconds > 0f)
                {
                    float profile = Mathf.Max(0.05f, entry.DigestionRateMultiplierSnapshot);
                    float rateMlPerSecond = RebirthMetabolismConfig.BaseSolidGastricEmptyingMlPerRealMinute
                        * mods.DigestionSpeed * profile / 60f;
                    float beforeSolid = entry.RemainingSolidVolumeMl;
                    float movedSolid = Mathf.Min(beforeSolid, rateMlPerSecond * activeSeconds);
                    if (movedSolid > 0.0001f)
                    {
                        entry.RemainingSolidVolumeMl -= movedSolid;
                        if (entry.IntestinalSolidVolumeMl <= 0.001f && entry.IntestinalNutritionUnits <= 0.001f)
                            entry.IntestinalNutritionResidenceSecondsRemaining = RebirthMetabolismConfig.IntestinalNutritionResidenceRealSeconds;
                        entry.IntestinalSolidVolumeMl += movedSolid;
                        totalMovedMl += movedSolid;
                        solidMovedMl += movedSolid;

                        if (entry.RemainingNutritionUnits > 0f && beforeSolid > 0f)
                        {
                            float movedNutrition = entry.RemainingNutritionUnits * (movedSolid / beforeSolid);
                            entry.RemainingNutritionUnits = Mathf.Max(0f, entry.RemainingNutritionUnits - movedNutrition);
                            entry.IntestinalNutritionUnits += movedNutrition;
                        }
                        if (entry.RemainingEnergyUnits > 0f && beforeSolid > 0f)
                        {
                            float movedEnergy = entry.RemainingEnergyUnits * (movedSolid / beforeSolid);
                            entry.RemainingEnergyUnits = Mathf.Max(0f, entry.RemainingEnergyUnits - movedEnergy);
                            entry.IntestinalEnergyUnits += movedEnergy;
                        }
                    }
                }
            }
        }
        return totalMovedMl;
    }

    private static float ConsumeGastricHold(ref float remainingHoldSeconds, float deltaRealSeconds)
    {
        if (deltaRealSeconds <= 0f)
            return 0f;
        if (remainingHoldSeconds <= 0f)
            return deltaRealSeconds;

        float active = Mathf.Max(0f, deltaRealSeconds - remainingHoldSeconds);
        remainingHoldSeconds = Mathf.Max(0f, remainingHoldSeconds - deltaRealSeconds);
        return active;
    }

    private static void AdvanceIntestinalResidence(RebirthMetabolismState state, float deltaRealSeconds)
    {
        if (state == null || deltaRealSeconds <= 0f)
            return;

        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry entry = state.IngestionEntries[i];
            if (entry == null)
                continue;

            if (entry.IntestinalFluidVolumeMl > 0.001f)
                entry.IntestinalFluidResidenceSecondsRemaining = Mathf.Max(0f, entry.IntestinalFluidResidenceSecondsRemaining - deltaRealSeconds);
            else
                entry.IntestinalFluidResidenceSecondsRemaining = 0f;

            if (entry.IntestinalNutritionUnits > 0.001f || entry.IntestinalSolidVolumeMl > 0.001f)
                entry.IntestinalNutritionResidenceSecondsRemaining = Mathf.Max(0f, entry.IntestinalNutritionResidenceSecondsRemaining - deltaRealSeconds);
            else
                entry.IntestinalNutritionResidenceSecondsRemaining = 0f;
        }
    }

    private static float ProcessIntestinalFluidAbsorption(EntityPlayer player, RebirthMetabolismState state,
        RebirthMetabolismModifiers mods, float budgetMl, out float hydrationPointsAdded, out float beverageEnergyAdded,
        out float nutritionUnitsAbsorbed, out float nutritionPointsAdded)
    {
        hydrationPointsAdded = 0f;
        beverageEnergyAdded = 0f;
        nutritionUnitsAbsorbed = 0f;
        nutritionPointsAdded = 0f;
        if (budgetMl <= 0f || state == null || state.IngestionEntries.Count == 0)
            return 0f;

        float left = budgetMl;
        float totalAbsorbedMl = 0f;
        for (int i = 0; i < state.IngestionEntries.Count && left > 0.001f; i++)
        {
            RebirthIngestionEntry entry = state.IngestionEntries[i];
            if (entry == null || entry.IntestinalFluidVolumeMl <= 0f || entry.IntestinalFluidResidenceSecondsRemaining > 0.001f)
                continue;

            float beforeFluid = entry.IntestinalFluidVolumeMl;
            float absorbed = Mathf.Min(beforeFluid, left);

            // Chunk K: specially prepared drink effects do not begin on the drink action. The
            // craft-time item snapshot survives handoff/save/partial drinking and activates once
            // only when this exact sip first begins real intestinal absorption.
            if (absorbed > 0.001f && !entry.PreparedDrinkSpecialEffectActivated && entry.PreparedDrinkSpecialEffectDurationSeconds > 0f)
            {
                entry.PreparedDrinkSpecialEffectActivated = true;
                RebirthDrinkPreparationSignatureService.TryActivateSpecialEffect(player, entry.SourceItemName, entry.PreparedDrinkSpecialEffectDurationSeconds);
            }

            entry.IntestinalFluidVolumeMl -= absorbed;
            left -= absorbed;
            totalAbsorbedMl += absorbed;

            if (entry.IntestinalEnergyUnits > 0f && beforeFluid > 0.001f)
            {
                float absorbedEnergy = entry.IntestinalEnergyUnits * (absorbed / beforeFluid);
                entry.IntestinalEnergyUnits = Mathf.Max(0f, entry.IntestinalEnergyUnits - absorbedEnergy);
                float beforeEnergy = state.Energy;
                state.Energy = Mathf.Min(RebirthMetabolismConfig.EnergyMax, state.Energy + absorbedEnergy);
                beverageEnergyAdded += Mathf.Max(0f, state.Energy - beforeEnergy);
            }

            // Nutrition dissolved in a drink travels with that liquid. Keeping it tied to the
            // physical fluid avoids an invisible nutrient queue after the intestinal liquid is gone.
            if (entry.Kind == RebirthIngestionKind.Fluid && entry.IntestinalNutritionUnits > 0f && beforeFluid > 0.001f)
            {
                float absorbedNutrition = entry.IntestinalNutritionUnits * (absorbed / beforeFluid);
                entry.IntestinalNutritionUnits = Mathf.Max(0f, entry.IntestinalNutritionUnits - absorbedNutrition);
                nutritionUnitsAbsorbed += absorbedNutrition;

                float effectiveNutrition = absorbedNutrition * Mathf.Max(0f, entry.NutrientYieldMultiplierSnapshot);
                if (effectiveNutrition > 0f)
                {
                    float beforeNutritionReserve = player.Stats.Food.Value;
                    player.Stats.Food.Value = Mathf.Min(player.Stats.Food.ModifiedMax, player.Stats.Food.Value + effectiveNutrition);
                    nutritionPointsAdded += Mathf.Max(0f, player.Stats.Food.Value - beforeNutritionReserve);
                }
            }

            float effectiveMl = absorbed * Mathf.Max(0f, entry.FluidYieldMultiplierSnapshot) * mods.FluidUtilization;
            if (effectiveMl > 0f)
            {
                float points = effectiveMl / RebirthMetabolismConfig.MlPerHydrationPoint;
                float beforeHydration = player.Stats.Water.Value;
                player.Stats.Water.Value = Mathf.Min(player.Stats.Water.ModifiedMax, player.Stats.Water.Value + points);
                hydrationPointsAdded += Mathf.Max(0f, player.Stats.Water.Value - beforeHydration);
            }
        }
        return totalAbsorbedMl;
    }

    private static float ProcessIntestinalNutritionAbsorption(EntityPlayer player, RebirthMetabolismState state,
        RebirthMetabolismModifiers mods, float budgetUnits, out float nutritionPointsAdded)
    {
        nutritionPointsAdded = 0f;
        if (budgetUnits <= 0f || state == null || state.IngestionEntries.Count == 0)
            return 0f;

        float left = budgetUnits;
        float totalAbsorbedUnits = 0f;
        for (int i = 0; i < state.IngestionEntries.Count && left > 0.001f; i++)
        {
            RebirthIngestionEntry entry = state.IngestionEntries[i];
            if (entry == null || entry.Kind == RebirthIngestionKind.Fluid || entry.IntestinalNutritionUnits <= 0f ||
                entry.IntestinalNutritionResidenceSecondsRemaining > 0.001f)
                continue;

            float beforeNutrition = entry.IntestinalNutritionUnits;
            float absorbed = Mathf.Min(beforeNutrition, left);
            entry.IntestinalNutritionUnits -= absorbed;
            left -= absorbed;
            totalAbsorbedUnits += absorbed;

            // A food becomes a meaningful meal only when real nutrition reaches the body. This
            // prevents eating from immediately affecting Mood/Diet before digestion has occurred.
            if (absorbed > 0.001f && entry.Kind == RebirthIngestionKind.Food && !entry.MeaningfulMealCredited)
            {
                entry.MeaningfulMealCredited = true;
                ItemValue mealItem = string.IsNullOrEmpty(entry.SourceItemName) ? null : ItemClass.GetItem(entry.SourceItemName, false);
                RebirthMealEvaluationResult mealEvaluation;
                if (mealItem != null && !mealItem.IsEmpty())
                    RebirthDietSatisfactionService.TryRecordMeaningfulMeal(player, mealItem, out mealEvaluation);
            }

            // For solid food, the visible chyme volume is processed away with the nutrient
            // fraction being absorbed. Liquid-meal nutrition has no solid intestinal volume.
            if (entry.IntestinalSolidVolumeMl > 0f && beforeNutrition > 0.001f)
            {
                float processedSolid = entry.IntestinalSolidVolumeMl * (absorbed / beforeNutrition);
                entry.IntestinalSolidVolumeMl = Mathf.Max(0f, entry.IntestinalSolidVolumeMl - processedSolid);
            }

            if (entry.IntestinalEnergyUnits > 0f && beforeNutrition > 0.001f)
            {
                float absorbedEnergy = entry.IntestinalEnergyUnits * (absorbed / beforeNutrition);
                entry.IntestinalEnergyUnits = Mathf.Max(0f, entry.IntestinalEnergyUnits - absorbedEnergy);
                state.Energy = Mathf.Min(RebirthMetabolismConfig.EnergyMax,
                    state.Energy + absorbedEnergy * Mathf.Max(0f, entry.PreparedMealEnergyEfficiencyMultiplier));
            }

            float effective = absorbed * Mathf.Max(0f, entry.NutrientYieldMultiplierSnapshot);
            if (effective > 0f)
            {
                float beforeNutritionReserve = player.Stats.Food.Value;
                player.Stats.Food.Value = Mathf.Min(player.Stats.Food.ModifiedMax, player.Stats.Food.Value + effective);
                nutritionPointsAdded += Mathf.Max(0f, player.Stats.Food.Value - beforeNutritionReserve);
            }
        }

        // Rare nutrientless solid entries still have to leave the intestine. This is a fallback
        // transit path only; normal foods lose chyme volume proportionally as nutrients absorb.
        float fallbackSolidBudget = RebirthMetabolismConfig.BaseSolidGastricEmptyingMlPerRealMinute * (budgetUnits > 0f
            ? (budgetUnits / Mathf.Max(0.001f, RebirthMetabolismConfig.BaseIntestinalNutrientAbsorptionUnitsPerRealMinute))
            : 0f);
        if (fallbackSolidBudget > 0f)
        {
            for (int i = 0; i < state.IngestionEntries.Count && fallbackSolidBudget > 0.001f; i++)
            {
                RebirthIngestionEntry entry = state.IngestionEntries[i];
                if (entry == null || entry.IntestinalSolidVolumeMl <= 0f || entry.IntestinalNutritionUnits > 0.001f ||
                    entry.IntestinalNutritionResidenceSecondsRemaining > 0.001f)
                    continue;
                float beforeSolid = entry.IntestinalSolidVolumeMl;
                float moved = Mathf.Min(beforeSolid, fallbackSolidBudget);
                entry.IntestinalSolidVolumeMl -= moved;
                fallbackSolidBudget -= moved;
                if (entry.IntestinalEnergyUnits > 0f && beforeSolid > 0.001f)
                {
                    float absorbedEnergy = entry.IntestinalEnergyUnits * (moved / beforeSolid);
                    entry.IntestinalEnergyUnits = Mathf.Max(0f, entry.IntestinalEnergyUnits - absorbedEnergy);
                    state.Energy = Mathf.Min(RebirthMetabolismConfig.EnergyMax,
                        state.Energy + absorbedEnergy * Mathf.Max(0f, entry.PreparedMealEnergyEfficiencyMultiplier));
                }
            }
        }
        return totalAbsorbedUnits;
    }

    private static void ProcessDigestiveHealth(EntityPlayer player, RebirthMetabolismState state, RebirthMetabolismModifiers mods, float damagePerReal60Minutes, float deltaRealMinutes)
    {
        float target = Mathf.Clamp(RebirthMetabolismConfig.BaseDigestiveHealth + mods.DigestiveBaselineOffset, 0f, 100f);
        float delta = -Mathf.Max(0f, damagePerReal60Minutes) / 60f / Mathf.Max(0.5f, mods.GutResilience) * deltaRealMinutes;
        bool probiotic = HasTimedEffect(state, "probiotics");
        bool prebiotic = HasTimedEffect(state, "prebiotic");
        if (damagePerReal60Minutes <= 0f && state.DigestiveHealth < target)
            delta += (0.75f + (probiotic ? 1.25f : 0f) + (prebiotic ? 0.5f : 0f))
                * Mathf.Max(0f, mods.DigestiveRecovery) / 60f * deltaRealMinutes;
        if (Mathf.Abs(delta) > 0.0001f)
            state.DigestiveHealth = Mathf.Clamp(state.DigestiveHealth + delta, 0f, 100f);
    }

    private static void CleanupEntries(RebirthMetabolismState state)
    {
        for (int i = state.IngestionEntries.Count - 1; i >= 0; i--)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e == null || (e.RemainingFluidVolumeMl <= 0.01f && e.RemainingSolidVolumeMl <= 0.01f &&
                e.RemainingNutritionUnits <= 0.01f && e.RemainingEnergyUnits <= 0.01f &&
                e.IntestinalFluidVolumeMl <= 0.01f && e.IntestinalSolidVolumeMl <= 0.01f &&
                e.IntestinalNutritionUnits <= 0.01f && e.IntestinalEnergyUnits <= 0.01f))
                state.IngestionEntries.RemoveAt(i);
        }
    }

    private static void AdvanceTimedEffects(RebirthMetabolismState state, float deltaRealSeconds)
    {
        for (int i = state.TimedEffects.Count - 1; i >= 0; i--)
        {
            RebirthMetabolismTimedEffect e = state.TimedEffects[i];
            if (e == null)
            {
                state.TimedEffects.RemoveAt(i);
                continue;
            }
            e.RemainingRealSeconds = Mathf.Max(0f, e.RemainingRealSeconds - Mathf.Max(0f, deltaRealSeconds));
            if (e.RemainingRealSeconds <= 0.001f)
                state.TimedEffects.RemoveAt(i);
        }
    }

    private static bool HasTimedEffect(RebirthMetabolismState state, string profile)
    {
        for (int i = 0; i < state.TimedEffects.Count; i++)
        {
            RebirthMetabolismTimedEffect e = state.TimedEffects[i];
            if (e != null && e.RemainingRealSeconds > 0f && string.Equals(e.ProfileId, profile, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void TryAutoSip(EntityPlayer player, RebirthMetabolismState state, RebirthMetabolismModifiers mods, float capacity)
    {
        if (!state.AutoSipEnabled || state.HydrationSlotItem == null || state.HydrationSlotItem.IsEmpty() || state.AutoSipCooldownRemainingRealSeconds > 0f)
            return;
        RebirthConsumableDefinition def;
        if (!RebirthConsumableResolver.TryResolve(state.HydrationSlotItem.itemValue, out def) || def == null || !def.IsDrink || !def.AutoSipSafe)
            return;
        if (GetFullnessMl(state) / capacity >= RebirthMetabolismConfig.FullThreshold)
            return;
        RebirthMetabolismSnapshot snap = BuildSnapshot(player, state, mods, 0f, 0f, 0f, 0f,
            RebirthMetabolismConfig.BaseIntestinalFluidAbsorptionMlPerRealMinute * mods.FluidAbsorption,
            RebirthMetabolismConfig.BaseSolidGastricEmptyingMlPerRealMinute * mods.DigestionSpeed,
            RebirthMetabolismConfig.BaseIntestinalNutrientAbsorptionUnitsPerRealMinute * mods.DigestionSpeed,
            EstimateCurrentGastricEmptyingMlPerRealMinute(state, mods, capacity));
        float max = Mathf.Max(1f, player.Stats.Water.ModifiedMax);
        if (snap.ProjectedHydration / max >= RebirthMetabolismConfig.AutoSipStartProjectedPercent)
            return;
        RebirthConsumeResult result = ConsumeHydrationSlot(player, true);
        if (result.Success)
            state.AutoSipCooldownRemainingRealSeconds = RebirthMetabolismConfig.AutoSipCooldownRealSeconds;
    }

    public static RebirthMetabolismSnapshot BuildSnapshot(EntityPlayer player)
    {
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (IsServerAuthority && state != null && IsGodModeActive(player))
            state.Energy = RebirthMetabolismConfig.EnergyMax;
        RebirthMetabolismModifiers mods = RebirthMetabolismModifierResolver.Resolve(player, state);
        float aH, aF, hH, cF, iH, iF, d;
        ResolveActivityNoMutation(player, state, out aH, out aF);
        ResolveTemperature(player, mods, out hH, out cF);
        ResolveIllness(player, out iH, out iF, out d);
        float hyd = (RebirthMetabolismConfig.BaseFluidNeedMlPerReal60Minutes * mods.HydrationRequirement + aH + hH + iH)
            * mods.TotalHydrationDemand;
        float food = (RebirthMetabolismConfig.BaseFoodNeedUnitsPerReal60Minutes * mods.FoodRequirement + cF + iF)
            * mods.TotalNutritionDemand;
        float capacity = Mathf.Max(100f, mods.StomachCapacity);
        float energyUse = RebirthEfficientMovementService.Apply(player, aF) * mods.EnergyUse;
        float energyRecovery = EstimateEnergyRecoveryPerRealMinute(player, state, mods);
        return BuildSnapshot(player, state, mods, hyd, food, energyUse, energyRecovery,
            RebirthMetabolismConfig.BaseIntestinalFluidAbsorptionMlPerRealMinute * mods.FluidAbsorption,
            RebirthMetabolismConfig.BaseSolidGastricEmptyingMlPerRealMinute * mods.DigestionSpeed,
            RebirthMetabolismConfig.BaseIntestinalNutrientAbsorptionUnitsPerRealMinute * mods.DigestionSpeed,
            EstimateCurrentGastricEmptyingMlPerRealMinute(state, mods, capacity));
    }

    private static void ResolveActivityNoMutation(EntityPlayer player, RebirthMetabolismState state, out float hydration, out float energyUsePerRealMinute)
    {
        float a = state != null ? state.SmoothedActivity : 0f;
        if (a < 0.5f) { hydration = 0f; energyUsePerRealMinute = 0f; }
        else if (a < 1.5f) { hydration = 25f; energyUsePerRealMinute = RebirthMetabolismConfig.EnergyLightUsePerRealMinute; }
        else if (a < 2.5f) { hydration = 100f; energyUsePerRealMinute = RebirthMetabolismConfig.EnergyModerateUsePerRealMinute; }
        else if (a < 3.5f) { hydration = 250f; energyUsePerRealMinute = RebirthMetabolismConfig.EnergyHighUsePerRealMinute; }
        else { hydration = 400f; energyUsePerRealMinute = RebirthMetabolismConfig.EnergyExtremeUsePerRealMinute; }
    }

    private static RebirthMetabolismSnapshot BuildSnapshot(EntityPlayer player, RebirthMetabolismState state, RebirthMetabolismModifiers mods,
        float hydrationLossMlPerReal60Minutes, float foodUsePerReal60Minutes, float energyUsePerRealMinute, float energyRecoveryPerRealMinute,
        float absorptionRate, float solidGastricRate,
        float nutrientAbsorptionRate, float gastricEmptyingRate)
    {
        RebirthMetabolismSnapshot s = new RebirthMetabolismSnapshot();
        s.OwnerEntityId = player != null ? player.entityId : -1;
        RebirthWorldCharacterRecord ownerRecord;
        s.CreationId = RebirthWorldCharacterService.TryGet(player, out ownerRecord) && ownerRecord?.Origin != null
            ? ownerRecord.Origin.CreationId : string.Empty;
        s.Revision = state != null ? state.Revision : 0;
        s.Hydration = player != null && player.Stats != null ? player.Stats.Water.Value : 0f;
        s.HydrationMax = player != null && player.Stats != null ? player.Stats.Water.ModifiedMax : 100f;
        s.Food = player != null && player.Stats != null ? player.Stats.Food.Value : 0f;
        s.FoodMax = player != null && player.Stats != null ? player.Stats.Food.ModifiedMax : 100f;
        s.Energy = state != null ? Mathf.Clamp(state.Energy, 0f, RebirthMetabolismConfig.EnergyMax) : RebirthMetabolismConfig.EnergyMax;
        s.EnergyMax = RebirthMetabolismConfig.EnergyMax;
        s.StomachFluidMl = GetFluidMl(state);
        s.StomachSolidMl = GetSolidMl(state);
        s.FullnessMl = s.StomachFluidMl + s.StomachSolidMl;
        s.StomachCapacityMl = Mathf.Max(100f, mods.StomachCapacity);
        s.IntestinalFluidMl = GetIntestinalFluidMl(state);
        s.IntestinalSolidMl = GetIntestinalSolidMl(state);
        s.IntestinalCapacityMl = Mathf.Max(100f, RebirthMetabolismConfig.BaseIntestinalCapacityMl);
        s.IntestinalNutritionUnits = GetIntestinalNutritionUnits(state);
        s.IntestinalEnergyUnits = GetIntestinalEnergyUnits(state);
        s.IntestinalContentPercent = s.IntestinalCapacityMl > 0.001f
            ? Mathf.Clamp01((s.IntestinalFluidMl + s.IntestinalSolidMl) / s.IntestinalCapacityMl)
            : 0f;
        s.IntestinalFluidResidenceSecondsRemaining = GetIntestinalFluidResidenceSecondsRemaining(state);
        s.IntestinalNutritionResidenceSecondsRemaining = GetIntestinalNutritionResidenceSecondsRemaining(state);
        float intestinalPendingHydration = GetIntestinalPendingHydrationPoints(state, mods);
        float intestinalPendingFood = GetIntestinalPendingFoodPoints(state, mods);
        float totalPendingStage;
        // Pending values are assigned a few lines below; compute stage after they are available.
        s.IntestinalStagePercent = 0f;
        s.GastricHoldSecondsRemaining = GetGastricHoldSecondsRemaining(state);
        s.FluidGastricHoldSecondsRemaining = GetFluidGastricHoldSecondsRemaining(state);
        s.SolidGastricHoldSecondsRemaining = GetSolidGastricHoldSecondsRemaining(state);
        s.GastricEmptyingMlPerRealMinute = gastricEmptyingRate;
        s.DigestiveHealth = state != null ? state.DigestiveHealth : RebirthMetabolismConfig.BaseDigestiveHealth;
        s.PendingHydration = GetPendingHydrationPoints(state, mods);
        s.PendingFood = GetPendingFoodPoints(state, mods);
        s.PendingEnergy = GetPendingEnergyUnits(state);
        totalPendingStage = Mathf.Max(0f, s.PendingHydration) + Mathf.Max(0f, s.PendingFood) + Mathf.Max(0f, s.PendingEnergy);
        float intestinalPendingStage = Mathf.Max(0f, intestinalPendingHydration) + Mathf.Max(0f, intestinalPendingFood) + Mathf.Max(0f, s.IntestinalEnergyUnits);
        s.IntestinalStagePercent = totalPendingStage > 0.001f ? Mathf.Clamp01(intestinalPendingStage / totalPendingStage) : 0f;
        s.GastricFluidTransferMlPerRealMinute = state != null ? state.LastGastricFluidTransferMlPerRealMinute : 0f;
        s.GastricSolidTransferMlPerRealMinute = state != null ? state.LastGastricSolidTransferMlPerRealMinute : 0f;
        s.FluidAbsorbedMlPerRealMinute = state != null ? state.LastFluidAbsorbedMlPerRealMinute : 0f;
        s.HydrationGainPointsPerRealMinute = state != null ? state.LastHydrationGainPointsPerRealMinute : 0f;
        float hydrationLossPointsPerMinute = RebirthMetabolismConfig.MlPerHydrationPoint > 0.001f
            ? (hydrationLossMlPerReal60Minutes / 60f) / RebirthMetabolismConfig.MlPerHydrationPoint
            : 0f;
        s.HydrationNetPointsPerRealMinute = s.HydrationGainPointsPerRealMinute - hydrationLossPointsPerMinute;
        s.NutritionAbsorbedUnitsPerRealMinute = state != null ? state.LastNutritionAbsorbedUnitsPerRealMinute : 0f;
        s.NutritionGainPointsPerRealMinute = state != null ? state.LastNutritionGainPointsPerRealMinute : 0f;
        s.NutritionNetPointsPerRealMinute = s.NutritionGainPointsPerRealMinute - (foodUsePerReal60Minutes / 60f);
        s.BeverageEnergyGainPerRealMinute = state != null ? state.LastBeverageEnergyGainPerRealMinute : 0f;
        s.IntestinalActivityPercent = state != null ? state.LastIntestinalActivityPercent : 0f;
        s.ProjectedHydration = Mathf.Min(s.HydrationMax, s.Hydration + s.PendingHydration);
        s.ProjectedFood = Mathf.Min(s.FoodMax, s.Food + s.PendingFood);
        s.ProjectedEnergy = Mathf.Min(s.EnergyMax, s.Energy + s.PendingEnergy);
        s.HydrationLossMlPerReal60Minutes = hydrationLossMlPerReal60Minutes;
        s.FoodUsePerReal60Minutes = foodUsePerReal60Minutes;
        s.EnergyUsePerRealMinute = energyUsePerRealMinute;
        s.EnergyRecoveryPerRealMinute = energyRecoveryPerRealMinute;
        s.EnergyRecoveryNutritionUsePerRealMinute = state != null ? state.LastEnergyRecoveryNutritionUsePerRealMinute : 0f;
        s.EnergyRecoveryHydrationMultiplier = GetEnergyRecoveryHydrationMultiplier(player);
        s.StaminaRecoveryMultiplier = GetStaminaRecoveryMultiplier(player);
        s.FluidAbsorptionMlPerRealMinute = absorptionRate;
        s.SolidGastricEmptyingMlPerRealMinute = solidGastricRate;
        s.NutrientAbsorptionUnitsPerRealMinute = nutrientAbsorptionRate;
        s.HydrationRequirementMultiplier = mods.HydrationRequirement * mods.TotalHydrationDemand;
        s.FoodRequirementMultiplier = mods.FoodRequirement * mods.TotalNutritionDemand;
        s.FluidAbsorptionMultiplier = mods.FluidAbsorption;
        s.FluidUtilizationMultiplier = mods.FluidUtilization;
        s.NutrientUtilizationMultiplier = mods.NutrientUtilization;
        s.DigestionSpeedMultiplier = mods.DigestionSpeed;
        s.GutResilienceMultiplier = mods.GutResilience;
        s.AutoSipEnabled = state != null && state.AutoSipEnabled;
        GetActiveIntakeSummary(state, out s.ActiveIntakeItemName, out s.ActiveIntakeRemainingMl);
        if (state != null && state.HydrationSlotItem != null && !state.HydrationSlotItem.IsEmpty())
        {
            RebirthConsumableDefinition def;
            if (RebirthConsumableResolver.TryResolve(state.HydrationSlotItem.itemValue, out def))
            {
                s.HydrationSlotItemName = def.ItemName;
                s.HydrationSlotCapacityMl = def.ContainerCapacityMl;
                s.HydrationSlotVolumeMl = RebirthLiquidContainerService.GetRemainingMl(state.HydrationSlotItem.itemValue, def);
                s.HydrationSlotLiquidProfile = RebirthLiquidContainerService.GetLiquidProfile(state.HydrationSlotItem.itemValue, def);
            }
        }
        return s;
    }

    private static void GetActiveIntakeSummary(RebirthMetabolismState state, out string itemName, out float remainingMl)
    {
        itemName = string.Empty;
        remainingMl = 0f;
        if (state == null || state.IngestionEntries == null)
            return;

        // Newest still-active ingestion wins. This is intentionally a presentation summary only;
        // the authoritative digestion model continues to process every entry independently.
        int bestEntryId = int.MinValue;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e == null)
                continue;

            float content = Mathf.Max(0f, e.RemainingFluidVolumeMl)
                + Mathf.Max(0f, e.RemainingSolidVolumeMl)
                + Mathf.Max(0f, e.IntestinalFluidVolumeMl)
                + Mathf.Max(0f, e.IntestinalSolidVolumeMl);
            if (content <= 0.01f || e.EntryId < bestEntryId)
                continue;

            bestEntryId = e.EntryId;
            itemName = e.SourceItemName ?? string.Empty;
            remainingMl = content;
        }
    }

    public static float GetFullnessMl(RebirthMetabolismState state)
    {
        return GetFluidMl(state) + GetSolidMl(state);
    }

    public static float GetFluidMl(RebirthMetabolismState state)
    {
        if (state == null) return 0f;
        float result = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
            if (state.IngestionEntries[i] != null) result += Mathf.Max(0f, state.IngestionEntries[i].RemainingFluidVolumeMl);
        return result;
    }

    public static float GetSolidMl(RebirthMetabolismState state)
    {
        if (state == null) return 0f;
        float result = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
            if (state.IngestionEntries[i] != null) result += Mathf.Max(0f, state.IngestionEntries[i].RemainingSolidVolumeMl);
        return result;
    }

    public static float GetIntestinalFluidMl(RebirthMetabolismState state)
    {
        if (state == null) return 0f;
        float result = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
            if (state.IngestionEntries[i] != null) result += Mathf.Max(0f, state.IngestionEntries[i].IntestinalFluidVolumeMl);
        return result;
    }

    public static float GetIntestinalSolidMl(RebirthMetabolismState state)
    {
        if (state == null) return 0f;
        float result = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
            if (state.IngestionEntries[i] != null) result += Mathf.Max(0f, state.IngestionEntries[i].IntestinalSolidVolumeMl);
        return result;
    }

    private static float GetIntestinalFluidResidenceSecondsRemaining(RebirthMetabolismState state)
    {
        if (state == null) return 0f;
        float result = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e != null && e.IntestinalFluidVolumeMl > 0.001f)
                result = Mathf.Max(result, e.IntestinalFluidResidenceSecondsRemaining);
        }
        return result;
    }

    private static float GetIntestinalNutritionResidenceSecondsRemaining(RebirthMetabolismState state)
    {
        if (state == null) return 0f;
        float result = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e != null && (e.IntestinalNutritionUnits > 0.001f || e.IntestinalSolidVolumeMl > 0.001f))
                result = Mathf.Max(result, e.IntestinalNutritionResidenceSecondsRemaining);
        }
        return result;
    }

    public static float GetIntestinalNutritionUnits(RebirthMetabolismState state)
    {
        if (state == null) return 0f;
        float result = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
            if (state.IngestionEntries[i] != null) result += Mathf.Max(0f, state.IngestionEntries[i].IntestinalNutritionUnits);
        return result;
    }

    public static float GetIntestinalEnergyUnits(RebirthMetabolismState state)
    {
        if (state == null) return 0f;
        float result = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
            if (state.IngestionEntries[i] != null) result += Mathf.Max(0f, state.IngestionEntries[i].IntestinalEnergyUnits);
        return result;
    }

    private static float GetFluidGastricHoldSecondsRemaining(RebirthMetabolismState state)
    {
        float max = 0f;
        if (state == null) return max;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e != null && e.RemainingFluidVolumeMl > 0.01f) max = Mathf.Max(max, e.FluidHoldSecondsRemaining);
        }
        return max;
    }

    private static float GetSolidGastricHoldSecondsRemaining(RebirthMetabolismState state)
    {
        float max = 0f;
        if (state == null) return max;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e != null && e.RemainingSolidVolumeMl > 0.01f) max = Mathf.Max(max, e.SolidHoldSecondsRemaining);
        }
        return max;
    }

    public static float GetGastricHoldSecondsRemaining(RebirthMetabolismState state)
    {
        if (state == null) return 0f;
        float result = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e == null) continue;
            if (e.RemainingFluidVolumeMl > 0.01f) result = Mathf.Max(result, e.FluidHoldSecondsRemaining);
            if (e.RemainingSolidVolumeMl > 0.01f) result = Mathf.Max(result, e.SolidHoldSecondsRemaining);
        }
        return result;
    }

    private static float EstimateCurrentGastricEmptyingMlPerRealMinute(RebirthMetabolismState state,
        RebirthMetabolismModifiers mods, float stomachCapacityMl)
    {
        if (state == null) return 0f;
        float total = 0f;
        float solid = GetSolidMl(state);
        float solidLoad = stomachCapacityMl > 0.001f
            ? Mathf.Clamp01(solid / Mathf.Max(1f, stomachCapacityMl * 0.70f))
            : 0f;

        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e == null) continue;

            if (e.RemainingFluidVolumeMl > 0.01f && e.FluidHoldSecondsRemaining <= 0f)
            {
                float half = e.FluidGastricHalfTimeSecondsSnapshot > 0f
                    ? e.FluidGastricHalfTimeSecondsSnapshot
                    : RebirthMetabolismConfig.GetFluidGastricHalfTimeRealSeconds(e.FluidIsMealBound);
                if (!e.FluidIsMealBound && solidLoad > 0f)
                    half *= Mathf.Lerp(1f, RebirthMetabolismConfig.ClearLiquidWithFoodHalfTimeMultiplier, solidLoad);
                else if (e.FluidIsMealBound)
                    half /= Mathf.Sqrt(Mathf.Max(0.25f, mods.DigestionSpeed));
                half = Mathf.Max(1f, half);
                total += e.RemainingFluidVolumeMl * 0.69314718056f / half * 60f;
            }

            if (e.RemainingSolidVolumeMl > 0.01f && e.SolidHoldSecondsRemaining <= 0f)
                total += RebirthMetabolismConfig.BaseSolidGastricEmptyingMlPerRealMinute
                    * mods.DigestionSpeed * Mathf.Max(0.05f, e.DigestionRateMultiplierSnapshot);
        }
        return total;
    }

    public static float GetPendingHydrationPoints(RebirthMetabolismState state, RebirthMetabolismModifiers mods)
    {
        if (state == null) return 0f;
        float effectiveMl = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e != null)
            {
                float pendingFluid = Mathf.Max(0f, e.RemainingFluidVolumeMl) + Mathf.Max(0f, e.IntestinalFluidVolumeMl);
                if (pendingFluid > 0f)
                    effectiveMl += pendingFluid * Mathf.Max(0f, e.FluidYieldMultiplierSnapshot) * mods.FluidUtilization;
            }
        }
        return effectiveMl / RebirthMetabolismConfig.MlPerHydrationPoint;
    }

    public static float GetPendingFoodPoints(RebirthMetabolismState state, RebirthMetabolismModifiers mods)
    {
        if (state == null) return 0f;
        float value = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e != null)
            {
                float pendingNutrition = Mathf.Max(0f, e.RemainingNutritionUnits) + Mathf.Max(0f, e.IntestinalNutritionUnits);
                if (pendingNutrition > 0f)
                    value += pendingNutrition * Mathf.Max(0f, e.NutrientYieldMultiplierSnapshot);
            }
        }
        return value;
    }

    private static float GetIntestinalPendingHydrationPoints(RebirthMetabolismState state, RebirthMetabolismModifiers mods)
    {
        if (state == null) return 0f;
        float effectiveMl = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e != null && e.IntestinalFluidVolumeMl > 0f)
                effectiveMl += e.IntestinalFluidVolumeMl * Mathf.Max(0f, e.FluidYieldMultiplierSnapshot) * mods.FluidUtilization;
        }
        return effectiveMl / RebirthMetabolismConfig.MlPerHydrationPoint;
    }

    private static float GetIntestinalPendingFoodPoints(RebirthMetabolismState state, RebirthMetabolismModifiers mods)
    {
        if (state == null) return 0f;
        float value = 0f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e != null && e.IntestinalNutritionUnits > 0f)
                value += e.IntestinalNutritionUnits * Mathf.Max(0f, e.NutrientYieldMultiplierSnapshot);
        }
        return value;
    }

    private static bool TryReserveIngestionCapacity(RebirthMetabolismState state)
    {
        if (state == null) return false;
        CleanupEntries(state);
        return state.IngestionEntries.Count < RebirthMetabolismConfig.MaxIngestionEntries;
    }

    private static void MergeOrAddEntry(RebirthMetabolismState state, RebirthIngestionEntry entry)
    {
        // Capacity is reserved before any item debit. Never discard accepted digestive material.
        state.IngestionEntries.Add(entry);
    }

    private static bool TryFindMatchingStack(EntityPlayer player, int itemType, ushort seed, out RebirthMetabolismSourceKind kind, out int slot, out ItemStack stack)
    {
        kind = RebirthMetabolismSourceKind.HeldToolbelt;
        slot = -1;
        stack = null;
        if (player == null) return false;

        ItemStack[] tool = player.inventory.ItemGrid.items;
        int held = player.inventory.holdingItemIdx;
        if (held >= 0 && held < tool.Length && Matches(tool[held], itemType, seed))
        {
            kind = RebirthMetabolismSourceKind.HeldToolbelt; slot = held; stack = tool[held]; return true;
        }
        for (int i = 0; i < tool.Length; i++)
        {
            if (Matches(tool[i], itemType, seed)) { kind = RebirthMetabolismSourceKind.HeldToolbelt; slot = i; stack = tool[i]; return true; }
        }
        ItemStack[] bag = player.bag.ItemGrid.items;
        for (int i = 0; i < bag.Length; i++)
        {
            if (Matches(bag[i], itemType, seed)) { kind = RebirthMetabolismSourceKind.Backpack; slot = i; stack = bag[i]; return true; }
        }
        return false;
    }

    private static bool Matches(ItemStack stack, int itemType, ushort seed)
    {
        if (stack == null || stack.IsEmpty() || stack.itemValue == null || stack.itemValue.type != itemType)
            return false;
        return seed == 0 || stack.itemValue.Seed == seed || stack.count > 1;
    }

    private static bool DecrementSource(EntityPlayer player, RebirthMetabolismSourceKind kind, int slot, ItemStack expected, RebirthMetabolismState state, Func<ItemStack, ItemStack, ItemStack, bool> externalCommit)
    {
        if (expected == null || expected.IsEmpty()) return false;
        ItemStack remaining = expected.Clone();
        remaining.count--;
        if (remaining.count <= 0) remaining = ItemStack.Empty.Clone();
        return externalCommit != null ? externalCommit(expected, remaining, null)
            : RebirthConsumptionSourceWriteback.TryCommit(player, kind, slot, expected, remaining, state);
    }

    public static float GetPendingEnergyUnits(RebirthMetabolismState state)
    {
        float value = 0f;
        if (state == null) return value;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry e = state.IngestionEntries[i];
            if (e != null)
                value += Mathf.Max(0f, e.RemainingEnergyUnits) + Mathf.Max(0f, e.IntestinalEnergyUnits);
        }
        return value;
    }

    public static bool EquipHeldHydrationContainer(EntityPlayer player,out string message)
    {
        message=string.Empty;
        return player?.inventory!=null&&TryEquipHydrationAt(player,false,player.inventory.holdingItemIdx,out message);
    }

    public static bool EquipMatchingHydrationContainer(EntityPlayer player,int type,ushort seed,out string message)
    {
        message=string.Empty;
        if(!IsServerAuthority||player==null||player.world==null||player.world.IsRemote()||
            player.bag==null||player.inventory==null||RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))return false;
        var bag=player.bag.ItemGrid.items;
        for(int i=0;i<bag.Length;i++)
            if(MatchesHydrationItem(bag[i],type,seed))return TryEquipHydrationAt(player,true,i,out message);
        var belt=player.inventory.ItemGrid.items;
        // Only unlocked public slots are physical sources; retired backing/dummy slots are excluded.
        for(int i=0;i<RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);i++)
            if(MatchesHydrationItem(belt[i],type,seed))return TryEquipHydrationAt(player,false,i,out message);
        return false;
    }

    private static bool MatchesHydrationItem(ItemStack stack,int type,ushort seed)
        => stack!=null&&!stack.IsEmpty()&&stack.itemValue!=null&&stack.itemValue.type==type&&stack.itemValue.Seed==seed;

    private static bool TryEquipHydrationAt(EntityPlayer player,bool inBag,int slot,out string message)
    {
        message=string.Empty;
        if(!IsServerAuthority||player==null||player.inventory==null||player.world==null||player.world.IsRemote()||
            RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))return false;
        if (RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        { message = Localization.Get("xuiRebirthLibraryTransferPending"); return false; }
        var slots=inBag?player.bag?.ItemGrid.items:player.inventory.ItemGrid.items;
        if(slots==null||slot<0||slot>=slots.Length||
            (!inBag&&slot>=RebirthToolbeltCapacity.GetOwnedSlotCount(player,slots.Length)))return false;
        var source=slots[slot];
        RebirthConsumableDefinition def;
        if(source==null||source.IsEmpty()||!RebirthConsumableResolver.TryResolve(source.itemValue,out def)||
            def==null||!def.IsDrink||!def.HydrationEquippable)return false;
        var state=RebirthMetabolismStateRepository.GetOrCreate(player);
        if(state==null)return false;
        if(state.HydrationSlotItem!=null&&!state.HydrationSlotItem.IsEmpty())
        { message=Localization.Get("xuiRebirthWaterSupportOccupied");return false; }
        if(RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            RebirthWorldCharacterRecord record;string support;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null)return false;
            if(record.Support.EquippedGearBySlot.TryGetValue(RebirthSurvivorGearService.SupportSlotId,out support)&&!string.IsNullOrEmpty(support))
            { message=Localization.Get("xuiRebirthSupportSlotOccupied");return false; }
        }
        var equipped=source.Clone();equipped.count=1;
        float normalized;
        RebirthLiquidContainerService.NormalizeState(equipped.itemValue,def,out normalized);
        var remaining=source.Clone();remaining.count--;
        if(remaining.count<=0)remaining=ItemStack.Empty.Clone();
        if(inBag)player.bag.SetSlot(slot,remaining);else player.inventory.SetItem(slot,remaining);
        state.HydrationSlotItem=equipped;state.Touch();SendSnapshotToOwner(player,false);
        message=Localization.Get("xuiRebirthWaterSupportEquipped");return true;
    }
    public static bool UnequipHydrationContainer(EntityPlayer player, out string message)
    {
        message = string.Empty;
        if (!IsServerAuthority || player?.world == null || player.world.IsRemote() || player.bag == null
            || RebirthSurvivorMetabolismCreationGate.ShouldDefer(player)) return false;
        if (RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        { message = Localization.Get("xuiRebirthLibraryTransferPending"); return false; }
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null || state.HydrationSlotItem == null || state.HydrationSlotItem.IsEmpty())
        {
            message = Localization.Get("xuiRebirthWaterSupportEmpty");
            return false;
        }
        ItemStack moving = state.HydrationSlotItem.Clone();
        ItemStack[] bag = player.bag.ItemGrid.items;
        for (int i = 0; i < bag.Length; i++)
        {
            if (bag[i] == null || bag[i].IsEmpty())
            {
                player.bag.SetSlot(i, moving);
                state.HydrationSlotItem = ItemStack.Empty.Clone();
                state.Touch();
                SendSnapshotToOwner(player, false);
                message = Localization.Get("xuiRebirthWaterSupportReturned");
                return true;
            }
        }
        message = Localization.Get("xuiRebirthWaterSupportNoSpace");
        return false;
    }

    public static void HandlePlayerDeath(EntityPlayer player)
    {
        if (!IsServerAuthority || player == null)
            return;
        if (RebirthSurvivorMetabolismCreationGate.ShouldDefer(player))
            return;
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null)
            return;

        if (state.HydrationSlotItem != null && !state.HydrationSlotItem.IsEmpty())
        {
            ItemStack slot = state.HydrationSlotItem.Clone();
            AddToBackpackOrDrop(player, slot);
            state.HydrationSlotItem = ItemStack.Empty.Clone();
        }

        state.IngestionEntries.Clear();
        state.TimedEffects.Clear();
        state.AutoSipCooldownRemainingRealSeconds = 0f;
        state.LastProcessedWorldTime = player.world != null ? player.world.GetWorldTime() : 0UL;
        state.RuntimeEntityId = -1;
        state.Touch();
        RebirthMetabolismStateRepository.SaveIfDirty("player-death");
    }

    public static void ToggleAutoSip(EntityPlayer player)
    {
        if (!IsServerAuthority || player == null) return;
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null) return;
        state.AutoSipEnabled = !state.AutoSipEnabled;
        state.Touch();
        SendSnapshotToOwner(player, false);
    }

    public static void ApplyVomiting(EntityPlayer player, float stomachFraction, float directFluidLossMl)
    {
        if (!IsServerAuthority || player == null) return;
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null) return;
        ExpelStomachFraction(state, stomachFraction);
        ApplyDirectHydrationLoss(player, directFluidLossMl);
        state.DigestiveHealth = Mathf.Max(0f, state.DigestiveHealth - 4f);
        CleanupEntries(state);
        ClampEnergyToReserveCeiling(player, state);
        state.Touch();
        SendSnapshotToOwner(player, false);
    }

    public static void ApplyDiarrhea(EntityPlayer player, float intestinalFraction, float directFluidLossMl)
    {
        if (!IsServerAuthority || player == null) return;
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null) return;
        ExpelIntestinalFraction(state, intestinalFraction);
        ApplyDirectHydrationLoss(player, directFluidLossMl);
        state.DigestiveHealth = Mathf.Max(0f, state.DigestiveHealth - 3f);
        CleanupEntries(state);
        ClampEnergyToReserveCeiling(player, state);
        state.Touch();
        SendSnapshotToOwner(player, false);
    }

    private static void UpdateStatusBuffs(EntityPlayer player, RebirthMetabolismState state, float capacity)
    {
        float percent = player.Stats.Water.ModifiedMax > 0f ? player.Stats.Water.Value / player.Stats.Water.ModifiedMax : 0f;
        SetExclusiveHydrationBuff(player, percent);
        float fullness = capacity > 0f ? GetFullnessMl(state) / capacity : 0f;
        SetBuff(player, "buffRebirthFull", fullness >= RebirthMetabolismConfig.FullThreshold && fullness < 1f);
        SetBuff(player, "buffRebirthStuffed", fullness >= 1f);
        float foodPercent = player.Stats.Food.ModifiedMax > 0f ? player.Stats.Food.Value / player.Stats.Food.ModifiedMax : 0f;
        SetExclusiveFoodBuff(player, foodPercent);
    }

    private static void SetExclusiveHydrationBuff(EntityPlayer player, float p)
    {
        string wanted = p <= 0.01f ? "buffRebirthCriticalDehydration" :
                        p < 0.10f ? "buffRebirthCriticalDehydration" :
                        p < 0.25f ? "buffRebirthSevereDehydration" :
                        p < 0.40f ? "buffRebirthDehydrated" :
                        p < 0.55f ? "buffRebirthThirsty" : string.Empty;
        string[] all = { "buffRebirthThirsty", "buffRebirthDehydrated", "buffRebirthSevereDehydration", "buffRebirthCriticalDehydration" };
        for (int i = 0; i < all.Length; i++) SetBuff(player, all[i], all[i] == wanted);
    }

    private static void SetExclusiveFoodBuff(EntityPlayer player, float p)
    {
        string wanted = p < 0.05f ? "buffRebirthStarving" :
                        p < 0.20f ? "buffRebirthVeryHungry" :
                        p < 0.40f ? "buffRebirthHungry" : string.Empty;
        string[] all = { "buffRebirthHungry", "buffRebirthVeryHungry", "buffRebirthStarving" };
        for (int i = 0; i < all.Length; i++) SetBuff(player, all[i], all[i] == wanted);
    }

    private static void SetBuff(EntityPlayer player, string name, bool wanted)
    {
        if (player == null || player.Buffs == null) return;
        bool has = player.Buffs.HasBuff(name);
        if (wanted && !has) player.Buffs.AddBuff(name);
        else if (!wanted && has) player.Buffs.RemoveBuff(name);
    }

    private static bool AddToBackpackOrDrop(EntityPlayer player, ItemStack stack)
    {
        if (player == null || stack == null || stack.IsEmpty()) return true;
        ItemStack[] bag = player.bag.ItemGrid.items;
        for (int i = 0; i < bag.Length; i++)
        {
            if (bag[i] == null || bag[i].IsEmpty())
            {
                player.bag.SetSlot(i, stack);
                return true;
            }
        }
        if (player.world != null && player.world.gameManager != null)
            player.world.gameManager.ItemDropServer(stack, player.GetPosition(), Vector3.zero);
        return false;
    }

    private static RebirthConsumeResult Fail(RebirthMetabolismConsumeFailure failure, string message)
    {
        return new RebirthConsumeResult { Success = false, Failure = failure, Message = message ?? string.Empty };
    }

    public static void StartDebugTrace(EntityPlayer player, float seconds)
    {
        if (player == null)
            return;
        seconds = Mathf.Clamp(seconds, 10f, 600f);
        debugTraceUntilRealTime[player.entityId] = Time.realtimeSinceStartup + seconds;
        Log.Out("[REBIRTH Metabolism Trace] BEGIN entity=" + player.entityId
            + " duration=" + seconds.ToString("0", CultureInfo.InvariantCulture) + "s"
            + " update=" + RebirthMetabolismConfig.UpdateRealSeconds.ToString("0.##", CultureInfo.InvariantCulture) + "s"
            + " | Run normally, drink/eat, then send the output_log. Trace auto-stops.");
    }

    public static void StopDebugTrace(EntityPlayer player)
    {
        if (player == null)
            return;
        if (debugTraceUntilRealTime.Remove(player.entityId))
            Log.Out("[REBIRTH Metabolism Trace] END entity=" + player.entityId + " reason=manual");
        else
            Log.Out("[REBIRTH Metabolism Trace] entity=" + player.entityId + " was not tracing.");
    }

    private static bool IsDebugTraceActive(EntityPlayer player)
    {
        if (player == null)
            return false;
        float until;
        if (!debugTraceUntilRealTime.TryGetValue(player.entityId, out until))
            return false;
        if (Time.realtimeSinceStartup <= until)
            return true;
        debugTraceUntilRealTime.Remove(player.entityId);
        Log.Out("[REBIRTH Metabolism Trace] END entity=" + player.entityId + " reason=timeout");
        return false;
    }

    private static string DescribeMovement(EntityPlayer player)
    {
        if (player == null)
            return "none";
        FastTags<TagGroup.Global> tag = player.CurrentMovementTag;
        if (tag.Test_AnySet(TagSwimmingRun)) return "swimmingRun";
        if (tag.Test_AnySet(TagRunning)) return "running";
        if (tag.Test_AnySet(TagSwimming)) return "swimming";
        if (tag.Test_AnySet(TagClimbing)) return "climbing";
        if (tag.Test_AnySet(TagJumping)) return "jumping";
        if (tag.Test_AnySet(TagWalking)) return "walking";
        return "idle/other";
    }

    private static void LogDetailedTickTrace(EntityPlayer player, RebirthMetabolismState state, RebirthMetabolismSnapshot snapshot,
        float deltaRealSeconds, float hydrationBefore, float nutritionBefore, float energyBefore,
        float stomachFluidBefore, float stomachSolidBefore,
        float intestinalFluidBefore, float intestinalSolidBefore, float intestinalNutritionBefore,
        float intestinalFluidAfterGastric, float intestinalSolidAfterGastric, float intestinalNutritionAfterGastric,
        float gastricFluidMovedMl, float gastricSolidMovedMl, float fluidAbsorbedMl, float nutritionAbsorbedUnits,
        float hydrationPointsAdded, float nutritionPointsAdded, float beverageEnergyAdded)
    {
        if (player == null || state == null)
            return;

        float dtMinutes = deltaRealSeconds / 60f;
        float fluidInRate = dtMinutes > 0.00001f ? gastricFluidMovedMl / dtMinutes : 0f;
        float solidInRate = dtMinutes > 0.00001f ? gastricSolidMovedMl / dtMinutes : 0f;
        float fluidAbsRate = dtMinutes > 0.00001f ? fluidAbsorbedMl / dtMinutes : 0f;
        float nutritionAbsRate = dtMinutes > 0.00001f ? nutritionAbsorbedUnits / dtMinutes : 0f;
        float hydrationGainRate = dtMinutes > 0.00001f ? hydrationPointsAdded / dtMinutes : 0f;
        float nutritionGainRate = dtMinutes > 0.00001f ? nutritionPointsAdded / dtMinutes : 0f;
        float beverageEnergyRate = dtMinutes > 0.00001f ? beverageEnergyAdded / dtMinutes : 0f;
        float hydrationLossMlPerMinute = snapshot.HydrationLossMlPerReal60Minutes / 60f;
        float nutritionUsePerMinute = snapshot.FoodUsePerReal60Minutes / 60f;

        Log.Out("[REBIRTH Metabolism Trace] entity=" + player.entityId
            + " dt=" + deltaRealSeconds.ToString("0.00", CultureInfo.InvariantCulture) + "s"
            + " move=" + DescribeMovement(player)
            + " activity=" + state.SmoothedActivity.ToString("0.00", CultureInfo.InvariantCulture)
            + " | energy " + energyBefore.ToString("0.00", CultureInfo.InvariantCulture) + "->" + state.Energy.ToString("0.00", CultureInfo.InvariantCulture)
            + " ceiling=" + GetEnergyReserveCeiling(player).ToString("0.00", CultureInfo.InvariantCulture)
            + " use=" + snapshot.EnergyUsePerRealMinute.ToString("0.00", CultureInfo.InvariantCulture) + "/m"
            + " recover=" + snapshot.EnergyRecoveryPerRealMinute.ToString("0.00", CultureInfo.InvariantCulture) + "/m"
            + " beverage=" + beverageEnergyRate.ToString("0.00", CultureInfo.InvariantCulture) + "/m"
            + " | hydration " + hydrationBefore.ToString("0.00", CultureInfo.InvariantCulture) + "->" + player.Stats.Water.Value.ToString("0.00", CultureInfo.InvariantCulture)
            + " loss=" + hydrationLossMlPerMinute.ToString("0.0", CultureInfo.InvariantCulture) + "mL/m"
            + " gain=" + hydrationGainRate.ToString("0.00", CultureInfo.InvariantCulture) + "pt/m"
            + " | nutrition " + nutritionBefore.ToString("0.00", CultureInfo.InvariantCulture) + "->" + player.Stats.Food.Value.ToString("0.00", CultureInfo.InvariantCulture)
            + " use=" + nutritionUsePerMinute.ToString("0.00", CultureInfo.InvariantCulture) + "/m"
            + " gain=" + nutritionGainRate.ToString("0.00", CultureInfo.InvariantCulture) + "/m"
            + " | stomach water " + stomachFluidBefore.ToString("0.0", CultureInfo.InvariantCulture) + "->" + GetFluidMl(state).ToString("0.0", CultureInfo.InvariantCulture)
            + " out=" + fluidInRate.ToString("0.0", CultureInfo.InvariantCulture) + "mL/m"
            + " solids " + stomachSolidBefore.ToString("0.0", CultureInfo.InvariantCulture) + "->" + GetSolidMl(state).ToString("0.0", CultureInfo.InvariantCulture)
            + " out=" + solidInRate.ToString("0.0", CultureInfo.InvariantCulture) + "mL/m"
            + " | gut water " + intestinalFluidBefore.ToString("0.00", CultureInfo.InvariantCulture)
            + "+" + (intestinalFluidAfterGastric - intestinalFluidBefore).ToString("0.00", CultureInfo.InvariantCulture)
            + "-" + fluidAbsorbedMl.ToString("0.00", CultureInfo.InvariantCulture)
            + "=" + GetIntestinalFluidMl(state).ToString("0.00", CultureInfo.InvariantCulture)
            + " abs=" + fluidAbsRate.ToString("0.0", CultureInfo.InvariantCulture) + "mL/m"
            + " hold=" + snapshot.IntestinalFluidResidenceSecondsRemaining.ToString("0.0", CultureInfo.InvariantCulture) + "s"
            + " | gut chyme " + intestinalSolidBefore.ToString("0.00", CultureInfo.InvariantCulture)
            + "+" + (intestinalSolidAfterGastric - intestinalSolidBefore).ToString("0.00", CultureInfo.InvariantCulture)
            + "-" + Mathf.Max(0f, intestinalSolidAfterGastric - GetIntestinalSolidMl(state)).ToString("0.00", CultureInfo.InvariantCulture)
            + "=" + GetIntestinalSolidMl(state).ToString("0.00", CultureInfo.InvariantCulture)
            + "mL"
            + " | gut nutrition " + intestinalNutritionBefore.ToString("0.00", CultureInfo.InvariantCulture)
            + "+" + (intestinalNutritionAfterGastric - intestinalNutritionBefore).ToString("0.00", CultureInfo.InvariantCulture)
            + "-" + nutritionAbsorbedUnits.ToString("0.00", CultureInfo.InvariantCulture)
            + "=" + GetIntestinalNutritionUnits(state).ToString("0.00", CultureInfo.InvariantCulture)
            + " abs=" + nutritionAbsRate.ToString("0.00", CultureInfo.InvariantCulture) + "/m"
            + " hold=" + snapshot.IntestinalNutritionResidenceSecondsRemaining.ToString("0.0", CultureInfo.InvariantCulture) + "s"
            + " | incoming hyd=" + snapshot.PendingHydration.ToString("0.00", CultureInfo.InvariantCulture)
            + " nutr=" + snapshot.PendingFood.ToString("0.00", CultureInfo.InvariantCulture)
            + " energy=" + snapshot.PendingEnergy.ToString("0.00", CultureInfo.InvariantCulture));
    }

    public static string FormatSnapshot(RebirthMetabolismSnapshot s)
    {
        return "Hydration=" + s.Hydration.ToString("0.0", CultureInfo.InvariantCulture) + "/" + s.HydrationMax.ToString("0.0", CultureInfo.InvariantCulture)
            + " pending=" + s.PendingHydration.ToString("0.0", CultureInfo.InvariantCulture)
            + " Nutrition=" + s.Food.ToString("0.0", CultureInfo.InvariantCulture) + "/" + s.FoodMax.ToString("0.0", CultureInfo.InvariantCulture)
            + " pending=" + s.PendingFood.ToString("0.0", CultureInfo.InvariantCulture)
            + " Energy=" + s.Energy.ToString("0.0", CultureInfo.InvariantCulture) + "/" + (s.EnergyMax * Mathf.Min(s.HydrationMax > 0.001f ? Mathf.Clamp01(s.Hydration / s.HydrationMax) : 0f, s.FoodMax > 0.001f ? Mathf.Clamp01(s.Food / s.FoodMax) : 0f)).ToString("0.0", CultureInfo.InvariantCulture) + "cap"
            + " Stomach=" + s.FullnessMl.ToString("0", CultureInfo.InvariantCulture) + "/" + s.StomachCapacityMl.ToString("0", CultureInfo.InvariantCulture) + "ml"
            + " (water=" + s.StomachFluidMl.ToString("0", CultureInfo.InvariantCulture) + ", solids=" + s.StomachSolidMl.ToString("0", CultureInfo.InvariantCulture) + ")"
            + " gutWater=" + s.IntestinalFluidMl.ToString("0.0", CultureInfo.InvariantCulture) + "ml"
            + " gutChyme=" + s.IntestinalSolidMl.ToString("0.0", CultureInfo.InvariantCulture) + "ml"
            + " gutNutrition=" + s.IntestinalNutritionUnits.ToString("0.0", CultureInfo.InvariantCulture)
            + " waterOut=" + s.GastricFluidTransferMlPerRealMinute.ToString("0.0", CultureInfo.InvariantCulture) + "ml/min"
            + " waterAbs=" + s.FluidAbsorbedMlPerRealMinute.ToString("0.0", CultureInfo.InvariantCulture) + "ml/min"
            + " hydGain=" + s.HydrationGainPointsPerRealMinute.ToString("0.00", CultureInfo.InvariantCulture) + "/min"
            + " hold=" + s.GastricHoldSecondsRemaining.ToString("0", CultureInfo.InvariantCulture) + "s"
            + " HydLoss=" + s.HydrationLossMlPerReal60Minutes.ToString("0", CultureInfo.InvariantCulture) + "ml/60real";
    }

    public static void SendSnapshotToOwner(EntityPlayer player, bool force)
    {
        if (!IsServerAuthority || player == null) return;
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null) return;
        RebirthMetabolismSnapshot snapshot = BuildSnapshot(player);
        long sequence;
        if (!snapshotSequences.TryGetValue(player.entityId, out sequence)) sequence = 0L;
        snapshot.Sequence = sequence + 1L;
        snapshotSequences[player.entityId] = snapshot.Sequence;
        RebirthMetabolismClientState.Receive(snapshot);
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c != null && c.IsServer)
        {
            try
            {
                NetPackageManager.GetPackageId(typeof(NetPackageRebirthMetabolismScopedState));
                var package = NetPackageManager.GetPackage<NetPackageRebirthMetabolismScopedState>();
                if (package != null)
                    c.SendPackage(package.Setup(snapshot), _attachedToEntityId: player.entityId);
            }
            catch (Exception)
            {
                // Snapshot delivery is retried by later ticks; it must not interrupt
                // authoritative metabolism or the remaining players in the world loop.
            }
        }
    }
}

public static class RebirthMetabolismModifierResolver
{
    public static RebirthMetabolismModifiers Resolve(EntityPlayer player, RebirthMetabolismState state)
    {
        RebirthMetabolismModifiers m = RebirthMetabolismModifiers.Neutral(RebirthMetabolismConfig.BaseStomachCapacityMl);
        if (player == null || player.Buffs == null)
            return m;

        m.HydrationRequirement = RebirthMetabolismConfig.ClampRequirement(CVarMultiplier(player, "rbMetGenHydrationNeed"));
        m.FoodRequirement = RebirthMetabolismConfig.ClampFoodRequirement(CVarMultiplier(player, "rbMetGenFoodNeed"));
        m.FluidAbsorption = RebirthMetabolismConfig.ClampAbsorption(CVarMultiplier(player, "rbMetGenFluidAbsorption"));
        m.FluidUtilization = RebirthMetabolismConfig.ClampUtilization(CVarMultiplier(player, "rbMetGenFluidUtilization"));
        m.NutrientUtilization = RebirthMetabolismConfig.ClampUtilization(CVarMultiplier(player, "rbMetGenNutrientUtilization"));
        m.DigestionSpeed = RebirthMetabolismConfig.ClampDigestion(CVarMultiplier(player, "rbMetGenDigestionSpeed"));
        m.GutResilience = RebirthMetabolismConfig.ClampGutResilience(CVarMultiplier(player, "rbMetGenGutResilience"));
        m.StomachCapacity = RebirthMetabolismConfig.BaseStomachCapacityMl * Mathf.Clamp(CVarMultiplier(player, "rbMetGenStomachCapacity"), 0.75f, 1.35f);
        m.DigestiveBaselineOffset = player.Buffs.GetCustomVar("rbMetGenDigestiveBaseline");

        if (state != null)
        {
            if (Has(state, "electrolytes"))
            {
                m.FluidAbsorption = RebirthMetabolismConfig.ClampAbsorption(m.FluidAbsorption * 1.10f);
                m.FluidUtilization = RebirthMetabolismConfig.ClampUtilization(m.FluidUtilization * 1.10f);
            }
            if (state.DigestiveHealth < 70f)
            {
                float healthFactor = Mathf.Lerp(0.70f, 1f, Mathf.Clamp01(state.DigestiveHealth / 70f));
                m.DigestionSpeed = RebirthMetabolismConfig.ClampDigestion(m.DigestionSpeed * healthFactor);
                m.NutrientUtilization = RebirthMetabolismConfig.ClampUtilization(m.NutrientUtilization * healthFactor);
            }
            if (state.DigestiveHealth < 35f)
            {
                float fluidFactor = Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(state.DigestiveHealth / 35f));
                m.FluidAbsorption = RebirthMetabolismConfig.ClampAbsorption(m.FluidAbsorption * fluidFactor);
            }
        }

        // Survivor origins are authoritative only in Rebirth progression mode. WorldCharacterService
        // returns false in Base Game mode, leaving the existing metabolism completely unchanged.
        RebirthWorldCharacterRecord record;
        if (RebirthWorldCharacterService.TryGet(player, out record) && record != null && record.IsComplete)
        {
            RebirthConditionResolvedSnapshot condition = RebirthConditionResolver.Resolve(player, record, state);
            m.TotalHydrationDemand = Mathf.Clamp(m.TotalHydrationDemand * condition.HydrationDemandMultiplier, 0.65f, 1.50f);
            m.TotalNutritionDemand = Mathf.Clamp(m.TotalNutritionDemand * condition.NutritionUseMultiplier, 0.65f, 1.50f);
            m.EnergyUse = Mathf.Clamp(m.EnergyUse * condition.EnergyUseMultiplier, 0.65f, 1.50f);
            m.EnergyRecovery = Mathf.Clamp(m.EnergyRecovery * condition.EnergyRecoveryMultiplier, 0.60f, 1.50f);
            m.HeatPenalty = Mathf.Clamp(m.HeatPenalty * condition.HeatPenaltyMultiplier, 0.50f, 1.50f);
            m.ColdPenalty = Mathf.Clamp(m.ColdPenalty * condition.ColdPenaltyMultiplier, 0.50f, 1.50f);
            m.FluidAbsorption = RebirthMetabolismConfig.ClampAbsorption(m.FluidAbsorption * condition.FluidAbsorptionMultiplier);
            m.FluidUtilization = RebirthMetabolismConfig.ClampUtilization(m.FluidUtilization * condition.FluidUtilizationMultiplier);
            m.NutrientUtilization = RebirthMetabolismConfig.ClampUtilization(m.NutrientUtilization * condition.NutrientUtilizationMultiplier);
            m.DigestionSpeed = RebirthMetabolismConfig.ClampDigestion(m.DigestionSpeed * condition.DigestionSpeedMultiplier);
            m.GutResilience = RebirthMetabolismConfig.ClampGutResilience(m.GutResilience * condition.GutResilienceMultiplier);
            m.DigestiveBaselineOffset += condition.DigestiveBaselineOffset;
            m.DigestiveRecovery = Mathf.Clamp(m.DigestiveRecovery * condition.DigestiveRecoveryMultiplier, 0.60f, 1.50f);
        }
        return m;
    }

    private static float CVarMultiplier(EntityPlayer player, string name)
    {
        float value = player.Buffs.GetCustomVar(name);
        return value <= 0f ? 1f : value;
    }

    private static bool Has(RebirthMetabolismState state, string id)
    {
        for (int i = 0; i < state.TimedEffects.Count; i++)
        {
            RebirthMetabolismTimedEffect e = state.TimedEffects[i];
            if (e != null && e.RemainingRealSeconds > 0f && string.Equals(e.ProfileId, id, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

public static class RebirthMetabolismClientState
{
    private static RebirthMetabolismSnapshot snapshot;
    private static bool hasSnapshot;

    public static void Receive(RebirthMetabolismSnapshot value)
    {
        EntityPlayerLocal local = null;
        try
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            local = world != null ? world.GetPrimaryPlayer() : null;
        }
        catch { }

        // A listen server invokes SendSnapshotToOwner for every connected player. Never let
        // that server-local publication replace the local owner's projection. Remote clients
        // also get an identity check in addition to attached-entity network routing.
        if (local == null || value.OwnerEntityId != local.entityId || value.Sequence <= 0L ||
            float.IsNaN(value.Food) || float.IsInfinity(value.Food) ||
            float.IsNaN(value.Hydration) || float.IsInfinity(value.Hydration))
            return;
        if (!MatchesCurrentCharacter(local, value.CreationId)) return;
        if (hasSnapshot && snapshot.OwnerEntityId == value.OwnerEntityId &&
            string.Equals(snapshot.CreationId, value.CreationId, StringComparison.Ordinal) &&
            snapshot.Sequence >= value.Sequence)
            return;

        if(local!=null&&local.world!=null&&local.world.IsRemote()&&local.Stats!=null&&
            RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
            !float.IsNaN(value.Food)&&!float.IsInfinity(value.Food)&&
            !float.IsNaN(value.Hydration)&&!float.IsInfinity(value.Hydration))
        {
            local.Stats.Food.Value=Mathf.Clamp(value.Food,0f,local.Stats.Food.ModifiedMax);
            local.Stats.Water.Value=Mathf.Clamp(value.Hydration,0f,local.Stats.Water.ModifiedMax);
            local.Stats.Food.Changed=false;
            local.Stats.Water.Changed=false;
        }
        snapshot = value;
        hasSnapshot = true;
    }

    public static bool TryGet(out RebirthMetabolismSnapshot value)
    {
        value = default(RebirthMetabolismSnapshot);
        if (!hasSnapshot) return false;
        try
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityPlayerLocal local = world != null ? world.GetPrimaryPlayer() : null;
            // No owning player during disconnect/load means no usable projection.
            // Do not expose the previous owner's water equipment or reserves.
            if (local == null || snapshot.OwnerEntityId != local.entityId || !MatchesCurrentCharacter(local, snapshot.CreationId)) return false;
            value = snapshot;
            return true;
        }
        catch { return false; }
    }

    private static bool MatchesCurrentCharacter(EntityPlayer player, string creationId)
    {
        if (player?.world == null) return false;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) return true;
        string current;
        if (player.world.IsRemote()) current = RebirthSurvivorClientState.GetProjectedCreationId(player);
        else
        {
            RebirthWorldCharacterRecord record;
            if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete) return false;
            current = record.Origin?.CreationId;
        }
        return RebirthSurvivorRequestScope.Matches(creationId, current);
    }

    public static void Clear()
    {
        hasSnapshot = false;
        snapshot = new RebirthMetabolismSnapshot { OwnerEntityId = -1 };
    }
}
