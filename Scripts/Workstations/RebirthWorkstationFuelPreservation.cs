using System;

#nullable disable

/// <summary>
/// Server-authoritative workstation fuel preservation. The fuel hooks use one
/// productive-work classification so zero-time, blocked-smelting and catch-up
/// boundaries cannot disagree about whether fuel should continue to burn.
/// </summary>
public static class RebirthWorkstationFuelPreservation
{
    public const string HarmonyId = "rebirth.workstation.fuelpreservation.3.1";

    public enum ProductiveWorkState : byte
    {
        Complete = 0,
        Pending = 1,
        Active = 2,
        Blocked = 3
    }

    public struct ProductiveWorkSnapshot
    {
        public ProductiveWorkState State;
        public float RemainingSeconds;
        public bool HasQueuedRecipe;
        public bool HasSmeltableInput;
    }

    public struct UpdateState
    {
        public bool Authoritative;
        public bool WasBurning;
        public ProductiveWorkSnapshot Before;
    }

    public static bool IsAuthoritative(World world)
    {
        if (world == null || world.IsRemote())
            return false;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return connection == null || connection.IsServer;
    }

    /// <summary>
    /// General-purpose predicate used outside the fuel hooks, where native private
    /// smelting arrays are not available. Queue state is classified exactly; a
    /// smeltable input is conservatively treated as pending rather than complete.
    /// </summary>
    public static bool HasProductiveWork(TileEntityWorkstation workstation)
    {
        if (workstation == null)
            return false;

        ProductiveWorkSnapshot queue = EvaluateQueue(workstation.Queue);
        if (queue.State == ProductiveWorkState.Active || queue.State == ProductiveWorkState.Pending)
            return true;
        if (queue.State == ProductiveWorkState.Blocked)
            return false;
        return HasSmeltableInput(workstation);
    }

    public static ProductiveWorkSnapshot GetProductiveWorkState(
        TileEntityWorkstation workstation,
        float[] currentMeltTimesLeft,
        bool[] isModuleUsed)
    {
        ProductiveWorkSnapshot result = new ProductiveWorkSnapshot
        {
            State = ProductiveWorkState.Complete
        };
        if (workstation == null)
            return result;

        ProductiveWorkSnapshot queue = EvaluateQueue(workstation.Queue);
        if (queue.HasQueuedRecipe)
            return queue;

        bool smeltable = HasSmeltableInput(workstation);
        result.HasSmeltableInput = smeltable;
        if (!smeltable)
            return result;

        // Input is not productive when the native smelting module is unavailable.
        // Treat it as blocked rather than simultaneously "work exists" and zero time.
        if (currentMeltTimesLeft == null || isModuleUsed == null
            || isModuleUsed.Length <= 4 || !isModuleUsed[4])
        {
            result.State = ProductiveWorkState.Blocked;
            return result;
        }

        float remaining = GetSmeltingTimeRemaining(
            workstation,
            currentMeltTimesLeft,
            isModuleUsed);
        if (remaining > 0f)
        {
            result.State = ProductiveWorkState.Active;
            result.RemainingSeconds = remaining;
        }
        else
        {
            // Smeltable input with an enabled module but no initialized timer is a
            // pending native transition, not proof that work is complete.
            result.State = ProductiveWorkState.Pending;
        }
        return result;
    }

    private static ProductiveWorkSnapshot EvaluateQueue(RecipeQueueItem[] queue)
    {
        ProductiveWorkSnapshot result = new ProductiveWorkSnapshot
        {
            State = ProductiveWorkState.Complete
        };
        if (queue == null)
            return result;

        int first = -1;
        int validCount = 0;
        float remaining = 0f;
        for (int i = 0; i < queue.Length; i++)
        {
            RecipeQueueItem entry = queue[i];
            if (entry == null || entry.Recipe == null || entry.Multiplier <= 0)
                continue;

            if (first < 0) first = i;
            validCount++;
            if (entry.CraftingTimeLeft > 0f)
                remaining += entry.CraftingTimeLeft;
            if (entry.Multiplier > 1 && entry.OneItemCraftTime > 0f)
                remaining += entry.OneItemCraftTime * (entry.Multiplier - 1f);
        }

        if (first < 0)
            return result;

        result.HasQueuedRecipe = true;
        result.RemainingSeconds = remaining;
        RecipeQueueItem current = queue[first];

        if (current.CraftingTimeLeft > 0f)
        {
            result.State = ProductiveWorkState.Active;
            return result;
        }

        if (current.IsCrafting)
        {
            result.State = remaining > 0f
                ? ProductiveWorkState.Active
                : ProductiveWorkState.Pending;
            return result;
        }

        if (validCount > 1)
        {
            // The native queue has a future item but has not advanced it into the
            // active slot yet. Give UpdateTick a bounded chance to perform that move.
            result.State = ProductiveWorkState.Pending;
            return result;
        }

        if (current.OneItemCraftTime <= 0f)
        {
            // A newly enqueued/uninitialized recipe can legitimately have zero
            // timing fields for one native update.
            result.State = ProductiveWorkState.Pending;
            return result;
        }

        // An initialized single entry with zero time and no active crafting flag is
        // at the exact-complete or native-blocked boundary (for example saturated
        // output). It must not authorize further catch-up fuel consumption.
        result.State = ProductiveWorkState.Blocked;
        return result;
    }

    public static bool HasOutstandingRecipe(RecipeQueueItem[] queue)
    {
        ProductiveWorkSnapshot state = EvaluateQueue(queue);
        return state.State == ProductiveWorkState.Active
            || state.State == ProductiveWorkState.Pending;
    }

    public static bool HasSmeltableInput(TileEntityWorkstation workstation)
    {
        if (workstation == null || workstation.Input == null || workstation.MaterialNames == null)
            return false;

        int count = Math.Min(workstation.InputSlotCount, workstation.Input.Length);
        for (int i = 0; i < count; i++)
        {
            ItemStack stack = workstation.Input[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0)
                continue;

            ItemClass item = ItemClass.GetForId(stack.itemValue.type);
            if (CanSmeltItem(workstation, item))
                return true;
        }

        return false;
    }

    private static bool CanSmeltItem(TileEntityWorkstation workstation, ItemClass item)
    {
        if (workstation == null || item == null || item.MadeOfMaterial == null || workstation.MaterialNames == null)
            return false;

        string category = item.MadeOfMaterial.ForgeCategory;
        if (string.IsNullOrEmpty(category))
            return false;

        for (int i = 0; i < workstation.MaterialNames.Length; i++)
        {
            string materialName = workstation.MaterialNames[i];
            if (!string.IsNullOrEmpty(materialName) && category.EqualsCaseInsensitive(materialName))
                return true;
        }

        return false;
    }

    private static float GetSmeltingTimeRemaining(
        TileEntityWorkstation workstation,
        float[] currentMeltTimesLeft,
        bool[] isModuleUsed)
    {
        if (workstation.Input == null || currentMeltTimesLeft == null || isModuleUsed == null
            || isModuleUsed.Length <= 4 || !isModuleUsed[4])
            return 0f;

        int count = Math.Min(workstation.InputSlotCount, Math.Min(workstation.Input.Length, currentMeltTimesLeft.Length));
        float maximum = 0f;

        for (int i = 0; i < count; i++)
        {
            ItemStack stack = workstation.Input[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0)
                continue;

            ItemClass item = ItemClass.GetForId(stack.itemValue.type);
            if (!CanSmeltItem(workstation, item))
                continue;

            float oneUnitTime = item.GetWeight() * (item.MeltTimePerUnit > 0f ? item.MeltTimePerUnit : 1f);
            if (isModuleUsed.Length > 0 && isModuleUsed[0] && workstation.Tools != null)
            {
                for (int toolIndex = 0; toolIndex < workstation.Tools.Length; toolIndex++)
                {
                    ItemStack tool = workstation.Tools[toolIndex];
                    if (tool == null || tool.IsEmpty())
                        continue;

                    float modifier = 1f;
                    tool.itemValue.ModifyValue(
                        null,
                        null,
                        PassiveEffects.CraftingSmeltTime,
                        ref oneUnitTime,
                        ref modifier,
                        FastTags<TagGroup.Global>.Parse(item.Name));
                    oneUnitTime *= modifier;
                }
            }

            float slotTime = currentMeltTimesLeft[i];
            if (slotTime == int.MinValue || slotTime <= 0f)
                slotTime = oneUnitTime;

            if (stack.count > 1)
                slotTime += oneUnitTime * (stack.count - 1f);

            maximum = Math.Max(maximum, slotTime);
        }

        return maximum;
    }

    public static void PrefixUpdateTick(
        TileEntityWorkstation __instance,
        World world,
        float[] currentMeltTimesLeft,
        bool[] isModuleUsed,
        out UpdateState __state)
    {
        __state = new UpdateState
        {
            Authoritative = IsAuthoritative(world),
            WasBurning = __instance != null && __instance.IsBurning,
            Before = default(ProductiveWorkSnapshot)
        };

        if (__state.Authoritative && __state.WasBurning)
            __state.Before = GetProductiveWorkState(__instance, currentMeltTimesLeft, isModuleUsed);
    }

    public static void PostfixUpdateTick(
        TileEntityWorkstation __instance,
        World world,
        float[] currentMeltTimesLeft,
        bool[] isModuleUsed,
        UpdateState __state)
    {
        if (!__state.Authoritative || !__state.WasBurning || __instance == null || !__instance.IsBurning)
            return;

        if (__state.Before.State != ProductiveWorkState.Active
            && __state.Before.State != ProductiveWorkState.Pending)
            return;

        ProductiveWorkSnapshot after = GetProductiveWorkState(
            __instance,
            currentMeltTimesLeft,
            isModuleUsed);
        if (after.State == ProductiveWorkState.Active || after.State == ProductiveWorkState.Pending)
            return;

        __instance.IsBurning = false;
        __instance.setModified();
    }

    public static void PrefixHandleFuel(
        TileEntityWorkstation __instance,
        World world,
        float[] currentMeltTimesLeft,
        bool[] isModuleUsed,
        ref float timePassed)
    {
        if (__instance == null || timePassed <= 0f)
            return;
        if (!IsAuthoritative(world) || !__instance.IsBurning)
            return;
        if (timePassed < 10f)
            return;

        ProductiveWorkSnapshot state = GetProductiveWorkState(
            __instance,
            currentMeltTimesLeft,
            isModuleUsed);
        switch (state.State)
        {
            case ProductiveWorkState.Active:
                if (state.RemainingSeconds > 0f && timePassed > state.RemainingSeconds)
                    timePassed = state.RemainingSeconds;
                break;

            case ProductiveWorkState.Pending:
                // Allow one bounded native step to initialize/advance the queue,
                // but never feed an arbitrary offline catch-up interval into fuel.
                if (timePassed > 1f)
                    timePassed = 1f;
                break;

            case ProductiveWorkState.Blocked:
            case ProductiveWorkState.Complete:
                timePassed = 0f;
                break;
        }
    }
}
