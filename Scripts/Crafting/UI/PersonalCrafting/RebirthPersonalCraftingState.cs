using System;

#nullable disable

/// <summary>
/// Read-only shared snapshot for the Rebirth personal Crafting surface.
/// Chunk J freezes the rule that all interaction/selection state is mutated only by
/// RebirthPersonalCraftingCoordinator. Child controllers may project native data, but they do not
/// independently own screen mode or cross-region selection.
/// </summary>
public sealed class RebirthPersonalCraftingState
{
    public enum VisualMode
    {
        Empty = 0,
        Recipe = 1,
        InventoryItem = 2,
        RecipeWithItemContext = 3
    }

    /// <summary>Developer-only layout guides/logging. Default false; no player-facing toggle.</summary>
    public static bool LayoutDebugEnabled { get; set; }

    public bool IsOpen { get; internal set; }
    public VisualMode Mode { get; internal set; } = VisualMode.Empty;

    public string SelectedRecipeName { get; internal set; } = string.Empty;
    public int SelectedInventorySlot { get; internal set; } = -1;
    public bool ItemContextOpen { get; internal set; }

    public string SearchText { get; internal set; } = string.Empty;
    public bool SearchActive { get; internal set; }
    public string SelectedCategory { get; internal set; } = string.Empty;
    public bool FavoritesOnly { get; internal set; }
    public int BatchCount { get; internal set; } = 1;
    public bool MaterialsSufficient { get; internal set; }
    public bool MaterialsStateKnown { get; internal set; }
    public bool KnowledgeExplorerActive { get; internal set; }
    public int KnowledgeReturnCount { get; internal set; }

    public int BackpackPhysicalSlots { get; internal set; }
    public int BackpackUnencumberedSlots { get; internal set; }
    public int BackpackUsedSlots { get; internal set; }

    public int QueueActiveCount { get; internal set; }
    public int QueueCapacity { get; internal set; }

    public int InteractionRevision { get; internal set; }
    public int LayoutRevision { get; private set; }
    public int QueueRevision { get; internal set; }
    public int BackpackRevision { get; internal set; }
    public int LayoutAuditRevision { get; internal set; }
    public int LayoutAuditFailureCount { get; internal set; }

    public Vector2i ScreenSize { get; private set; }
    public Vector2i RootSize { get; private set; }
    public Vector2i RootPosition { get; private set; }

    public bool LastLayoutAuditPassed { get; internal set; } = true;
    public string LastLayoutAuditReport { get; internal set; } = string.Empty;
    public int LastLayoutAuditFingerprint { get; internal set; }
    public Vector2i LastLayoutAuditScreen { get; internal set; }

    public string InteractionSummary
    {
        get
        {
            return "mode=" + Mode
                + " recipe=" + (string.IsNullOrEmpty(SelectedRecipeName) ? "<none>" : SelectedRecipeName)
                + " itemSlot=" + SelectedInventorySlot
                + " itemContext=" + ItemContextOpen
                + " search=" + SearchActive
                + " category=" + (string.IsNullOrEmpty(SelectedCategory) ? "<all>" : SelectedCategory)
                + " favorites=" + FavoritesOnly
                + " batch=" + BatchCount
                + " materials=" + (MaterialsStateKnown ? (MaterialsSufficient ? "enough" : "missing") : "unknown")
                + " queue=" + QueueActiveCount + "/" + QueueCapacity
                + " backpack=" + BackpackUsedSlots + "/" + BackpackPhysicalSlots
                + " knowledge=" + KnowledgeExplorerActive;
        }
    }

    public void RecordLayout(Vector2i screenSize, Vector2i rootSize, Vector2i rootPosition)
    {
        if (screenSize.x == ScreenSize.x && screenSize.y == ScreenSize.y &&
            rootSize.x == RootSize.x && rootSize.y == RootSize.y &&
            rootPosition.x == RootPosition.x && rootPosition.y == RootPosition.y)
            return;

        ScreenSize = screenSize;
        RootSize = rootSize;
        RootPosition = rootPosition;
        LayoutRevision++;
    }
}
