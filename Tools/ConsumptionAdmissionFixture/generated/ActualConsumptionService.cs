using System;
using System.Globalization;
using UnityEngine;
public static partial class RebirthMetabolismService {
    public static bool IsServerAuthority
    {
        get
        {
            ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
            return c == null || c.IsServer;
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
            RebirthStatisticsService.RecordConsumption(player,def.ItemName,true,consumed);
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
            RebirthStatisticsService.RecordConsumption(player,def.ItemName,false,0f);
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
            RebirthStatisticsService.RecordMetabolismTreatment(player,def.ItemName);
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
        => RebirthPreservedConsumableEffects.Apply(player,itemValue,IsServerAuthority,DebugEnabled);

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

    internal static bool TryFindMatchingStack(EntityPlayer player, int itemType, ushort seed, out RebirthMetabolismSourceKind kind, out int slot, out ItemStack stack)
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
        return RebirthConsumptionSourceIdentity.Matches(stack, itemType, seed);
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


    private static RebirthConsumeResult Fail(RebirthMetabolismConsumeFailure failure, string message)
    {
        return new RebirthConsumeResult { Success = false, Failure = failure, Message = message ?? string.Empty };
    }


}