using System;

#nullable disable

/// <summary>
/// Single interaction-state owner for the Rebirth Personal Crafting screen.
/// Native controllers continue to own recipes/items/queue transactions; this coordinator owns only
/// which recipe/item/filter/batch state the custom surface is presenting and ensures those states do
/// not compete for geometry.
/// </summary>
public sealed class RebirthPersonalCraftingCoordinator
{
    private readonly XUiC_RebirthPersonalCrafting owner;
    private readonly RebirthPersonalCraftingState state;
    private Recipe selectedRecipe;
    private XUiC_ItemStack selectedInventorySlot;

    public RebirthPersonalCraftingCoordinator(XUiC_RebirthPersonalCrafting owner, RebirthPersonalCraftingState state)
    {
        this.owner = owner;
        this.state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public Recipe SelectedRecipe => selectedRecipe;
    public XUiC_ItemStack SelectedInventorySlot => selectedInventorySlot;

    public void Open()
    {
        bool returningFromKnowledge = state.KnowledgeExplorerActive;
        state.IsOpen = true;
        state.SearchText = string.Empty;
        state.SearchActive = false;
        state.FavoritesOnly = false;
        state.SelectedInventorySlot = -1;
        state.ItemContextOpen = false;
        selectedInventorySlot = null;
        state.MaterialsStateKnown = false;
        state.MaterialsSufficient = false;
        if (returningFromKnowledge)
        {
            state.KnowledgeExplorerActive = false;
            state.KnowledgeReturnCount++;
        }
        RecomputeMode();
        Touch();
    }

    public void Close()
    {
        state.IsOpen = false;
        state.SearchText = string.Empty;
        state.SearchActive = false;
        state.SelectedInventorySlot = -1;
        state.ItemContextOpen = false;
        selectedInventorySlot = null;
        state.MaterialsStateKnown = false;
        state.MaterialsSufficient = false;
        RecomputeMode();
        Touch();
    }

    public void SelectRecipe(Recipe recipe, bool explicitInteraction = false)
    {
        if (selectedRecipe == recipe)
        {
            RecordRecipeName(recipe);
            if (explicitInteraction && state.ItemContextOpen)
            {
                XUiC_RebirthCraftingItemContext sameRecipeContext = owner != null
                    ? owner.GetChildByType<XUiC_RebirthCraftingItemContext>()
                    : null;
                sameRecipeContext?.ClearSelectionFromCoordinator();
                selectedInventorySlot = null;
                state.SelectedInventorySlot = -1;
                state.ItemContextOpen = false;
                Touch();
            }
            RecomputeMode();
            return;
        }

        selectedRecipe = recipe;
        RecordRecipeName(recipe);

        // Recipe interaction supersedes a transient item popover, but never changes Backpack data.
        // The coordinator—not the recipe row—owns that cross-region transition.
        XUiC_RebirthCraftingItemContext context = owner != null
            ? owner.GetChildByType<XUiC_RebirthCraftingItemContext>()
            : null;
        if (context != null && context.IsContextVisible)
            context.ClearSelectionFromCoordinator();
        selectedInventorySlot = null;
        state.SelectedInventorySlot = -1;
        state.ItemContextOpen = false;
        state.MaterialsStateKnown = false;
        state.MaterialsSufficient = false;
        RecomputeMode();
        Touch();
    }

    public void SelectInventoryItem(XUiC_ItemStack slot, bool contextOpen)
    {
        bool keepExistingContext = slot != null && selectedInventorySlot == slot && state.ItemContextOpen;
        int slotNumber = slot != null ? slot.SlotNumber : -1;
        bool nextContextOpen = slot != null && (contextOpen || keepExistingContext);
        if (selectedInventorySlot == slot && state.SelectedInventorySlot == slotNumber && state.ItemContextOpen == nextContextOpen)
            return;
        selectedInventorySlot = slot;
        state.SelectedInventorySlot = slotNumber;
        state.ItemContextOpen = nextContextOpen;
        RecomputeMode();
        Touch();
    }

    public void ClearInventoryItem()
    {
        if (selectedInventorySlot == null && state.SelectedInventorySlot < 0 && !state.ItemContextOpen)
            return;
        selectedInventorySlot = null;
        state.SelectedInventorySlot = -1;
        state.ItemContextOpen = false;
        RecomputeMode();
        Touch();
    }

    public void RecordSearch(string text)
    {
        text = text ?? string.Empty;
        bool active = text.Length > 0;
        if (state.SearchText == text && state.SearchActive == active)
            return;
        state.SearchText = text;
        state.SearchActive = active;
        Touch();
    }

    public void RecordCategory(string category)
    {
        category = category ?? string.Empty;
        if (state.SelectedCategory == category && !state.FavoritesOnly)
            return;
        state.SelectedCategory = category;
        state.FavoritesOnly = false;
        Touch();
    }

    public void RecordFavorites(bool favoritesOnly)
    {
        if (state.FavoritesOnly == favoritesOnly)
            return;
        state.FavoritesOnly = favoritesOnly;
        Touch();
    }

    public void RecordBatch(int count)
    {
        count = Math.Max(1, count);
        if (state.BatchCount == count)
            return;
        state.BatchCount = count;
        Touch();
    }

    public void RecordMaterials(bool known, bool sufficient)
    {
        if (state.MaterialsStateKnown == known && state.MaterialsSufficient == sufficient)
            return;
        state.MaterialsStateKnown = known;
        state.MaterialsSufficient = sufficient;
        Touch();
    }

    public void RecordQueue(int activeCount, int capacity)
    {
        activeCount = Math.Max(0, activeCount);
        capacity = Math.Max(0, capacity);
        if (state.QueueActiveCount == activeCount && state.QueueCapacity == capacity)
            return;
        state.QueueActiveCount = activeCount;
        state.QueueCapacity = capacity;
        state.QueueRevision++;
        Touch();
    }

    public void RecordBackpack(int physical, int unencumbered, int used)
    {
        physical = Math.Max(0, physical);
        unencumbered = Math.Max(0, Math.Min(physical, unencumbered));
        used = Math.Max(0, Math.Min(physical, used));
        if (state.BackpackPhysicalSlots == physical && state.BackpackUnencumberedSlots == unencumbered &&
            state.BackpackUsedSlots == used)
            return;
        state.BackpackPhysicalSlots = physical;
        state.BackpackUnencumberedSlots = unencumbered;
        state.BackpackUsedSlots = used;
        state.BackpackRevision++;
        Touch();
    }

    public void BeginKnowledgeExplorer()
    {
        if (state.KnowledgeExplorerActive)
            return;
        state.KnowledgeExplorerActive = true;
        Touch();
    }

    public void CancelKnowledgeExplorerTransition()
    {
        if (!state.KnowledgeExplorerActive)
            return;
        state.KnowledgeExplorerActive = false;
        Touch();
    }

    private void RecordRecipeName(Recipe recipe)
    {
        string name = string.Empty;
        if (recipe != null)
        {
            try { name = recipe.GetName() ?? string.Empty; }
            catch { name = string.Empty; }
        }
        state.SelectedRecipeName = name;
    }

    private void RecomputeMode()
    {
        bool hasRecipe = selectedRecipe != null;
        bool hasItem = selectedInventorySlot != null || state.SelectedInventorySlot >= 0;
        if (hasRecipe && state.ItemContextOpen && hasItem)
            state.Mode = RebirthPersonalCraftingState.VisualMode.RecipeWithItemContext;
        else if (hasRecipe)
            state.Mode = RebirthPersonalCraftingState.VisualMode.Recipe;
        else if (hasItem)
            state.Mode = RebirthPersonalCraftingState.VisualMode.InventoryItem;
        else
            state.Mode = RebirthPersonalCraftingState.VisualMode.Empty;
    }

    private void Touch()
    {
        state.InteractionRevision++;
        owner?.RequestLayoutAudit();
    }
}
