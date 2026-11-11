using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using HarmonyLib;

internal static class RebirthRemoteResourceLiveCraft
{
    internal static bool Applies(ItemActionEntryCraft entry)
    {
        var recipe = (entry?.ItemController as XUiC_RecipeEntry)?.Recipe;
        var station = entry?.craftCountControl?.WindowGroup?.Controller as XUiC_WorkstationWindowGroup;
        return RemoteResourcesRuntimePolicy.Enabled && !RemoteResourceClientGrantContext.Active &&
            IsOrdinary(recipe) && station?.WorkstationData?.TileEntity is not RebirthCamperStationState;
    }

    private static bool IsOrdinary(Recipe recipe) => recipe?.ingredients != null && !recipe.IsScrap &&
        !RebirthCookingHeat.Managed(recipe) && !RebirthRecyclerRuntime.IsMarked(recipe) && !RebirthStationGridQueue.IsMarked(recipe);

    private static IEnumerable<StaticContainerResourceSource> NearbyCrates(EntityPlayerLocal player)
    {
        float radius = RemoteResourcesRuntimePolicy.Radius;
        if (player.world.IsRemote())
            return RemoteResourceClientAvailability.GetSources(player)
                .Where(source => source.Kind == RemoteResourceSourceKind.StaticContainer &&
                    (source.Position - player.position).sqrMagnitude <= radius * radius)
                .Select(source => new StaticContainerResourceSource(player.world,
                    new Vector3i((int)source.Position.x, (int)source.Position.y, (int)source.Position.z)))
                .Where(source => source.IsLoaded && !source.IsBusy);

        return RemoteResourceSourceDiscovery.ResolveNearby(player.world, player, radius)
            .OfType<StaticContainerResourceSource>()
            .Where(source => !RebirthRemoteResourceSourceCustody.BlocksSource(source) &&
                RemoteResourceEligibility.CanUse(source, player, true, false, out _));
    }

    private static bool CanTake(ItemStack stack, ItemValue item, PackedBoolArray locks, int slot) =>
        (locks == null || slot >= locks.Length || !locks[slot]) &&
        RemoteResourceTransactions.MatchesConsumable(stack, item);

    private static int Take(ItemValue item, int remaining, ItemStack[] slots, PackedBoolArray locks,
        Action<int, ItemStack> setSlot, List<Action> undo, List<ItemStack> paid)
    {
        for (int slot = 0; slots != null && slot < slots.Length && remaining > 0; slot++)
        {
            var before = slots[slot];
            if (!CanTake(before, item, locks, slot)) continue;
            int count = Math.Min(before.count, remaining);
            var after = count == before.count ? ItemStack.Empty.Clone() :
                new ItemStack(before.itemValue, before.count - count);
            int index = slot;
            undo.Add(() => setSlot(index, before));
            setSlot(index, after);
            var actual = slots[index];
            if (actual == null || actual.count != after.count || actual.itemValue.type != after.itemValue.type) continue;

            paid?.Add(new ItemStack(before.itemValue.Clone(), count));
            remaining -= count;
        }
        return remaining;
    }

    // b10 normally inserts before calling RemoveItems. Pay at that insertion
    // call so native cannot publish a partially paid row.
    private static bool AddPaidRecipe(XUiC_CraftingWindowGroup group, Recipe recipe, ItemActionEntryCraft entry)
    {
        if (!Applies(entry) || recipe.ingredients.Count == 0) return group.AddItemToQueue(recipe);

        var player = entry.ItemController.xui.playerUI.entityPlayer;
        var input = entry.craftCountControl.WindowGroup.Controller.GetChildByType<XUiC_WorkstationInputGrid>();
        var inputSlots = input?.GetSlots()?.ToArray();
        var bag = player.bag.ItemGrid;
        var belt = player.inventory.ItemGrid;
        bool quality = ((XUiC_RecipeEntry)entry.ItemController).Recipe.ingredients.Any(stack => stack.itemValue.HasQuality);
        var paid = quality ? new List<ItemStack>() : null;
        var undo = new List<Action>();
        var changedCrates = new HashSet<StaticContainerResourceSource>();
        StaticContainerResourceSource[] crates = null;
        int batches = entry.craftCountControl.Count;
        int local = 0;
        int remote = 0;
        int shortfall = 0;
        bool inputWritten = false;
        bool queued = false;
        try
        {
            foreach (var required in recipe.ingredients)
            {
                if (required.IsEmpty()) continue;
                var item = required.itemValue;
                int due = checked(required.count * batches);
                int remaining = due;
                if (input != null)
                    remaining = Take(item, remaining, inputSlots, null, (i, value) => inputSlots[i] = value, undo, paid);
                else
                {
                    remaining = Take(item, remaining, bag.items, bag.SlotLocks, player.bag.SetSlot, undo, paid);
                    remaining = Take(item, remaining, belt.items, belt.SlotLocks, player.inventory.SetItem, undo, paid);
                }
                local += due - remaining;
                if (remaining <= 0) continue;

                crates ??= NearbyCrates(player).ToArray();
                foreach (var crate in crates)
                {
                    int before = remaining;
                    remaining = Take(item, remaining, crate.Slots, crate.SlotLocks, crate.SetSlot, undo, paid);
                    if (remaining != before) changedCrates.Add(crate);
                    remote += before - remaining;
                    if (remaining == 0) break;
                }
                if (remaining == 0) continue;

                shortfall = remaining;
                return false;
            }

            if (input != null && local != 0)
            {
                inputWritten = true;
                input.SetSlots(inputSlots);
                input.UpdateBackend(inputSlots);
                var actual = (group as XUiC_WorkstationWindowGroup)?.WorkstationData?.TileEntity?.Input;
                if (actual == null || actual.Length != inputSlots.Length ||
                    inputSlots.Where((stack, index) => !RebirthStationGridIngredients.IsSameStackSnapshot(actual[index], stack)).Any())
                {
                    shortfall = local;
                    return false;
                }
            }
            if (quality)
            {
                recipe.ingredients.Clear();
                recipe.AddIngredients(paid);
            }
            queued = group.AddItemToQueue(recipe);
            return queued;
        }
        finally
        {
            if (!queued)
                for (int index = undo.Count - 1; index >= 0; index--)
                    undo[index]();
            if (inputWritten && !queued)
            {
                input.SetSlots(inputSlots);
                input.UpdateBackend(inputSlots);
            }
            if (queued)
                foreach (var crate in changedCrates)
                    crate.MarkModified();
            if (input == null && local != 0)
            {
                var inventory = entry.ItemController.xui.PlayerInventory;
                inventory.dispatchBackpackItemsChanged();
                inventory.dispatchToolbeltItemsChanged();
            }
            RebirthDebug.Log("crafting", $"[REBIRTH ContainerCraft] debit recipe={recipe.GetName()} batches={batches} local={local} remote={remote} shortfall={shortfall} pool={(input == null ? "inventory" : "station-input")} accepted={queued}");
        }
    }

    private static void RemoveInventory(XUiM_PlayerInventory inventory, IList<ItemStack> items, int count,
        IList<ItemStack> removed, ItemActionEntryCraft entry)
    {
        if (!Applies(entry)) inventory.RemoveItems(items, count, removed);
        else CopyQualityIngredients(items, removed, entry);
    }

    private static void RemoveStationInput(XUiC_WorkstationInputGrid input, IList<ItemStack> items, int count,
        IList<ItemStack> removed, ItemActionEntryCraft entry)
    {
        if (!Applies(entry)) input.RemoveItems(items, count, removed);
        else CopyQualityIngredients(items, removed, entry);
    }

    private static void CopyQualityIngredients(IList<ItemStack> items, IList<ItemStack> removed, ItemActionEntryCraft entry)
    {
        // Native copies exact removed fragments back into quality recipes.
        if (removed != null && ((XUiC_RecipeEntry)entry.ItemController).Recipe.ingredients.Any(stack => stack.itemValue.HasQuality))
            foreach (var item in items)
                removed.Add(item);
    }
    internal static void Install(Harmony harmony)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(NativeCraft));
#if REBIRTH_DEBUG
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(QueueTrace));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(CompletionTrace));
#endif
    }

    [HarmonyPatch(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.OnActivated))]
    private static class NativeCraft
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var signature = new[] { typeof(IList<ItemStack>), typeof(int), typeof(IList<ItemStack>) };
            var queue = AccessTools.Method(typeof(XUiC_CraftingWindowGroup), nameof(XUiC_CraftingWindowGroup.AddItemToQueue),
                new[] { typeof(Recipe) });
            var payment = new Dictionary<MethodInfo, MethodInfo>
            {
                [AccessTools.Method(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.RemoveItems), signature)] =
                    AccessTools.DeclaredMethod(typeof(RebirthRemoteResourceLiveCraft), nameof(RemoveInventory)),
                [queue] =
                    AccessTools.DeclaredMethod(typeof(RebirthRemoteResourceLiveCraft), nameof(AddPaidRecipe)),
                [AccessTools.Method(typeof(XUiC_WorkstationInputGrid), nameof(XUiC_WorkstationInputGrid.RemoveItems), signature)] =
                    AccessTools.DeclaredMethod(typeof(RebirthRemoteResourceLiveCraft), nameof(RemoveStationInput))
            };
            var code = instructions.ToList();
            if (payment.Keys.Any(method => code.Count(instruction => instruction.Calls(method)) != (method == queue ? 2 : 1)))
                throw new NotSupportedException("Expected both native queue and payment callsites.");

            foreach (var instruction in code)
            {
                if (instruction.operand is MethodInfo original && payment.TryGetValue(original, out var replacement))
                {
                    var entry = new CodeInstruction(OpCodes.Ldarg_0);
                    entry.labels.AddRange(instruction.labels);
                    entry.blocks.AddRange(instruction.blocks);
                    yield return entry;
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                    continue;
                }
                yield return instruction;
            }
        }
    }

#if REBIRTH_DEBUG
    [ThreadStatic] private static long started;

    internal static void TraceClick(ItemActionEntryCraft entry)
    {
        if (!RebirthDebug.IsOn("crafting")) return;
        started = Stopwatch.GetTimestamp();
        var recipe = ((XUiC_RecipeEntry)entry.ItemController).Recipe;
        RebirthDebug.Log("crafting", $"[REBIRTH ContainerCraft] click recipe={recipe.GetName()} area={recipe.craftingArea}");
    }

    internal static void EndTrace() => started = 0;

    [HarmonyPatch(typeof(XUiC_CraftingQueue), nameof(XUiC_CraftingQueue.AddRecipeToCraft))]
    private static class QueueTrace
    {
        private static void Postfix(Recipe _recipe, bool __result)
        {
            if (started != 0)
                RebirthDebug.Log("crafting", $"[REBIRTH ContainerCraft] queue recipe={_recipe.GetName()} accepted={__result} elapsedMs={(Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency:F2}");
        }
    }

    [HarmonyPatch(typeof(XUiC_RecipeStack), "outputStack")]
    private static class CompletionTrace
    {
        private static void Postfix(XUiC_RecipeStack __instance, bool __result)
        {
            if (!__result || !RebirthDebug.IsOn("crafting")) return;
            var recipe = __instance.GetRecipe();
            if (IsOrdinary(recipe) && __instance.originalItem?.IsEmpty() != false)
                RebirthDebug.Log("crafting", $"[REBIRTH ContainerCraft] completion recipe={recipe.GetName()} status=delivered");
        }
    }
#endif
}
