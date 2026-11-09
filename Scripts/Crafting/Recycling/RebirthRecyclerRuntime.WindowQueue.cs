using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthRecyclerRuntime
{
    // 2.6 (XUiC_ScrappingWorkstationInputGrid.QueueAggregateScrap): the item in the scrap input is taken at once and
    // one scrap row per output joins the station's crafting queue. In 3.0 the station owns those rows (the window only
    // shows them), so they go straight into the station's queue and the open window is refreshed from it.
    // Each row keeps the same marked token as TryQueue, so processing, cancel refunds and tool wear are unchanged.
    // Placed stations on the host or in single player; camper stations and remote clients keep their own paths.
    internal static bool TryQueueInWindow(EntityPlayerLocal actor, XUiC_WorkstationWindowGroup group, TileEntityWorkstation station,
        int inputSlot, int units, out ItemStack[] input, out string reason)
    {
        input = null;
        reason = "xuiCannotBeScrappedHere";
        if (actor == null || actor.world == null || actor.world.IsRemote() || station == null || station is RebirthCamperStationState) return false;
        if (!CanQueue(actor, station, inputSlot, units, out reason)) return false;
        PullWindowQueue(station);
        if (!ReferenceEquals(actor.world.GetTileEntity(station.ToWorldPos()), station) || LockManager.Instance?.IsLockedByLocalPlayer(station, 0) != true)
        { reason = "xuiRebirthRecyclingAccessExpired"; return false; }
        var source = station.Input[inputSlot].Clone();
        if (!RebirthRecyclerRules.TryResolve(source.itemValue.type, station.block.GetBlockName(), out var rule, out reason)) return false;
        string owner = Identity(actor), creation = RebirthCamperCharacterAccess.CurrentCreation(actor);
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(creation)) { reason = "xuiRebirthRecyclingAccessExpired"; return false; }

        var queue = (RecipeQueueItem[])station.Queue.Clone();
        var vacant = Enumerable.Range(0, queue.Length).Reverse().Where(i => queue[i] == null || queue[i].Recipe == null || queue[i].Multiplier <= 0).ToArray();
        if (vacant.Length < rule.Outputs.Count) { reason = "xuiRebirthRecyclingQueueFull"; return false; }

        var original = source.itemValue.Clone();
        var attachments = Detach(original);
        var operation = Guid.NewGuid();
        for (int outputIndex = 0; outputIndex < rule.Outputs.Count; outputIndex++)
        {
            var output = rule.Outputs[outputIndex];
            var stack = output.Stack;
            float time = output.TimeSeconds * units;
            var token = original.Clone();
            token.SetMetadata(Prefix + "group", operation.ToString("N")); token.SetMetadata(Prefix + "source", RebirthNativeItemCodec.Encode(original));
            token.SetMetadata(Prefix + "units", units); token.SetMetadata(Prefix + "outputs", rule.Outputs.Count); token.SetMetadata(Prefix + "index", outputIndex);
            token.SetMetadata(Prefix + "owner", owner); token.SetMetadata(Prefix + "creation", creation); token.SetMetadata(Prefix + "rule", rule.SourceId);
            var recipe = new Recipe { itemValueType = stack.itemValue.type, count = checked(stack.count * units), IsScrap = true, IsTrackable = false,
                craftingTime = time, craftExpGain = 0, UseIngredientModifier = false, ingredients = new List<ItemStack> { new ItemStack(token, 1) } };
            int slot = vacant[outputIndex];
            queue[slot] = new RecipeQueueItem { Recipe = recipe, Multiplier = 1, CraftingTimeLeft = time, OneItemCraftTime = time,
                StartingEntityId = actor.entityId, IsCrafting = slot == queue.Length - 1 };
        }

        input = ItemStack.Clone(station.Input);
        input[inputSlot].count -= units;
        if (input[inputSlot].count <= 0) input[inputSlot] = ItemStack.Empty.Clone();
        station.Input = input;
        station.Queue = queue;
        station.setModified();
        SyncGrids(group);
        ProjectWindowQueue(group, station);
        foreach (var attachment in attachments)
            if (!actor.bag.AddItem(attachment) && !actor.inventory.AddItem(attachment))
                GameManager.Instance.ItemDropServer(attachment, actor.GetPosition(), UnityEngine.Vector3.zero, actor.entityId);
        reason = null;
        return true;
    }

    // While a placed station's window is open on this machine, the window runs ordinary crafts and the station runs
    // scrap rows. These two keep one queue: the window's rows go into the station before a scrap step, and the
    // station's rows are drawn back into the window after it. Scrap rows always take the station's values.
    private static XUiC_WorkstationWindowGroup OpenPlacedWindow(TileEntityWorkstation station)
    {
        var world = GameManager.Instance?.World;
        if (station == null || station is RebirthCamperStationState || world == null || world.IsRemote() || !station.bUserAccessing) return null;
        var local = world.GetPrimaryPlayer() as EntityPlayerLocal;
        var ui = local != null ? LocalPlayerUI.GetUIForPlayer(local) : null;
        if (ui?.xui == null) return null;
        foreach (var window in RebirthWorkstationWindowIndex.For(ui.xui))
            if (window.windowGroup?.isShowing == true && ReferenceEquals(window.WorkstationData?.TileEntity, station)) return window;
        return null;
    }

    internal static void PullWindowQueue(TileEntityWorkstation station)
    {
        var window = OpenPlacedWindow(station);
        var cards = RebirthWorkstationQueueIndex.For(window)?.GetRecipesToCraft();
        var current = station?.Queue;
        if (cards == null || current == null || cards.Length != current.Length) return;
        var rows = new RecipeQueueItem[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            var recipe = card?.GetRecipe();
            if (recipe != null && IsMarked(recipe))
            {
                // The station runs this row: keep its own copy, or drop the card if the station already finished it.
                var own = current.FirstOrDefault(row => ReferenceEquals(row?.Recipe, recipe));
                rows[i] = own != null ? own : new RecipeQueueItem { StartingEntityId = -1 };
                continue;
            }
            rows[i] = recipe == null ? new RecipeQueueItem { StartingEntityId = -1 } : new RecipeQueueItem {
                Recipe = recipe, Multiplier = (short)card.GetRecipeCount(), CraftingTimeLeft = card.GetRecipeCraftingTimeLeft(),
                IsCrafting = card.IsCrafting, Quality = (byte)card.OutputQuality, StartingEntityId = card.StartingEntityId,
                OneItemCraftTime = card.GetOneItemCraftTime() };
        }
        // Scrap rows the window does not show yet stay on the station, in free places from the front.
        foreach (var own in current)
        {
            if (own?.Recipe == null || !IsMarked(own.Recipe) || rows.Any(row => ReferenceEquals(row?.Recipe, own.Recipe))) continue;
            // Behind the queued rows (the front is the last index), never at the back of the queue.
            int free = Array.FindLastIndex(rows, row => row?.Recipe == null);
            if (free < 0) return; // Never drop a paid row; leave the station as it is.
            rows[free] = own;
        }
        station.Queue = rows;
    }

    internal static void ProjectWindowQueue(XUiC_WorkstationWindowGroup window, TileEntityWorkstation station)
    {
        var cards = RebirthWorkstationQueueIndex.For(window)?.GetRecipesToCraft();
        var queue = station?.Queue;
        if (cards == null || queue == null || cards.Length != queue.Length || station is RebirthCamperStationState) return;
        for (int i = 0; i < queue.Length; i++)
        {
            var row = queue[i];
            if (cards[i] == null) continue;
            var card = cards[i];
            if (row?.Recipe == null)
            {
                if (card.recipe != null) { card.SetRecipe(null, 0, 0f, true); card.IsCrafting = false; card.IsDirty = true; }
                continue;
            }
            // The front card is always "crafting", as the native queue forces every frame; forcing scrap rows back
            // to queued made the card flip on each refresh. Scrap rows still only run in the station (marked rows need
            // a verified station admission before the window can complete them).
            bool crafting = i == queue.Length - 1;
            // Rebuild a card only when its row changed; rebuilding the active card on every refresh made it flicker.
            if (!ReferenceEquals(card.recipe, row.Recipe) || card.recipeCount != row.Multiplier)
            {
                card.SetRecipe(row.Recipe, row.Multiplier, row.CraftingTimeLeft, true, row.StartingEntityId, row.Quality, row.OneItemCraftTime);
                card.IsCrafting = crafting;
                card.IsDirty = true;
                continue;
            }
            // Same row: scrap rows are timed by the station, so only follow its time; the window times crafts itself.
            if (IsMarked(row.Recipe) && System.Math.Abs(card.craftingTimeLeft - row.CraftingTimeLeft) > 0.05f) card.craftingTimeLeft = row.CraftingTimeLeft;
            if (card.IsCrafting != crafting) card.IsCrafting = crafting;
        }
    }
}

public static partial class RebirthRecyclerRuntime
{
    /// <summary>
    /// The grid half of native syncUIfromTE (tools, input, output, fuel) without its SetAllChildrenDirty. Marking the
    /// whole window dirty after every scrap output redrew every queue card and recipe row, so scrapping armor made the
    /// window flicker (owner 2026-11-08). The queue is projected separately and only changed cards are rebuilt.
    /// </summary>
    internal static void SyncGrids(XUiC_WorkstationWindowGroup window)
    {
        var data = window?.WorkstationData;
        if (data?.TileEntity == null) return;
        // No UpdateTick here (native syncUIfromTE has one): ticking the station before the cards were projected made
        // the window/station merge treat freshly queued rows as unshown and move them to the back of the queue, which
        // then walked forward across the cards for seconds (verified 2026-11-08 with an order trace).
        if (window.toolWindow != null) { window.toolWindow.SetSlots(data.GetToolStacks()); window.toolWindow.IsDirty = true; }
        if (window.inputWindow != null) { window.inputWindow.SetSlots(data.GetInputStacks()); window.inputWindow.IsDirty = true; }
        if (window.outputWindow != null) { window.outputWindow.SetSlots(data.GetOutputStacks()); window.outputWindow.IsDirty = true; }
        if (window.fuelWindow != null) { window.fuelWindow.SetSlots(data.GetFuelStacks()); window.fuelWindow.IsDirty = true; }
    }
}

public static partial class RebirthRecyclerRuntime
{
    /// <summary>Scrap practice share of the matching craft award (provisional default, owner 2026-11-08).</summary>
    internal const float ScrapSkillShare = 0.5f;

    /// <summary>
    /// Scrapping armor and clothing trains Tailoring (owner 2026-11-08). When the last output of a scrap job is
    /// delivered, the owner earns half of what crafting that wearable once per scrapped unit would give. Other items
    /// give nothing here. Host/single player, placed stations; the deferred bonus-delivery paths do not award.
    /// </summary>
    private static void AwardScrapSkill(World world, Group group)
    {
        try
        {
            var item = group?.Original?.ItemClass;
            if (item == null) return;
            // 2.6 unlocks (cosmetic, recipe) reach the owner in every mode, online or later.
            RebirthScrapUnlockDelivery.Deliver(world, group.Owner,
                candidate => Identity(candidate) == group.Owner && RebirthCamperCharacterAccess.CurrentCreation(candidate) == group.Creation, item.GetItemName());
            var player = world.Players?.list?.FirstOrDefault(candidate => candidate != null && candidate.IsSpawned() && !candidate.IsDead()
                && Identity(candidate) == group.Owner && RebirthCamperCharacterAccess.CurrentCreation(candidate) == group.Creation);
            if (player == null) return;
            if (!RebirthServiceCraftSkillService.IsTailoringWearableName(item.GetItemName())) return;
            var recipe = CraftingManager.GetRecipe(item.GetItemName());
            if (recipe == null) return;
            var model = RebirthCraftTrainingRules.BuildModel(player, recipe, "skill.tailoring");
            RebirthSkillAwardService.TryAwardCraft(player, "skill.tailoring", model, Math.Max(1, Math.Min(9999, group.Units)), ScrapSkillShare,
                "scrap:skill.tailoring:" + item.GetItemName(), group.Id.ToString("N"));
        }
        catch (Exception e) { Log.Warning("[REBIRTH Recycling] scrap skill award skipped: " + e.Message); }
    }

    internal static string IdentityOf(EntityPlayer player) => Identity(player);
}

/// <summary>
/// One authority for scrap cards (owner 2026-11-08: cards jumped 4 to 5 and swapped places while scrapping). The native
/// queue copies every card one slot forward whenever the front card is empty, while the recycler places the station's
/// rows on the cards by position; the two undid each other. At a station with scrap rows, an empty front card is first
/// refilled from the station, so the native shift has nothing to do.
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(XUiC_CraftingQueue), nameof(XUiC_CraftingQueue.Update))]
internal static class RebirthRecyclerSingleQueueAuthority
{
    private static void Prefix(XUiC_CraftingQueue __instance)
    {
        var cards = __instance?.queueItems;
        if (cards == null || cards.Length == 0) return;
        var window = __instance.windowGroup?.Controller as XUiC_WorkstationWindowGroup;
        var station = window?.WorkstationData?.TileEntity;
        if (station == null || station is RebirthCamperStationState || !RebirthRecyclerRuntime.Supports(station)
            || !RebirthRecyclerRuntime.HasGroups(station.Queue)) return;
        // The station front slot can be empty right after scrap rows are queued (until its next tick moves them
        // forward). Pack them forward whenever that happens, whatever the cards show, then project, so the native
        // shift and the projection never fight (verified 2026-11-08: ~30 card rewrites per second for 2-3 s).
        var rows = station.Queue; var front = rows[rows.Length - 1];
        bool packed = false;
        if (front == null || front.Recipe == null || front.Multiplier <= 0)
        {
            // Empty slots must have a zero count, or the native pack treats the front as occupied and does nothing
            // (verified 2026-11-08: empty rows carried Multiplier 1).
            foreach (var row in rows) if (row != null && row.Recipe == null && row.Multiplier != 0) row.Multiplier = 0;
            station.cycleRecipeQueue(); station.setModified(); packed = true;
        }
        if (packed || !((XUiC_RecipeStack)cards[cards.Length - 1]).HasRecipe()) RebirthRecyclerRuntime.ProjectWindowQueue(window, station);
    }

}
