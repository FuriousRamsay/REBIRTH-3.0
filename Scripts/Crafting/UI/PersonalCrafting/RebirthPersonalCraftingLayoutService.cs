using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Calculates and applies the Rebirth personal Crafting safe rectangle in XUi logical units.
/// It intentionally uses XUi.GetXUiScreenSize() rather than Unity physical pixels so UI scale,
/// screen-bounds and split/local-player camera scaling are respected.
/// </summary>
public sealed class RebirthPersonalCraftingLayoutService
{
    private const int HorizontalMarginMin = 10;
    private const int HorizontalMarginMax = 28;
    private const int TopMarginMin = 8;
    private const int TopMarginMax = 20;
    private const int BottomHudReserveFallback = 132;
    private const int BottomHudGap = 18;
    private const int RootMinWidth = 1180;
    private const int RootMinHeight = 610;
    private const int FixedRootWidth = 1872;
    private const int FixedRootHeight = 935;
    // PC126: Inventory changes only the content panel. The outer/navigation shell never changes
    // size when switching between Inventory and Crafting. This prevents the top navigation from
    // shrinking/reflowing and preserves the approved full Crafting composition byte-for-byte in
    // its outer geometry. The Inventory panel is independently centered beneath the fixed nav.
    private const int InventoryPanelWidth = 1000;
    private const int InventoryPanelHeight = 572;
    private const int InventoryPanelMinWidth = 820;
    private const int InventoryPanelMinHeight = 470;
    private const int OffscreenZoneX = 10000;
    private const int ZoneGap = 10;
    private const int InnerMargin = 8;
    private const int TopZoneHeight = 52;
    // Keep the world-status block compact enough that eight navigation tabs retain essentially
    // the same visual width as the seven-tab PC124 bar. At the approved 1872-wide shell this
    // yields ~194px per tab instead of the squeezed ~174px PC125 presentation.
    private const int TopStatusMinWidth = 254;
    private const int TopStatusMaxWidth = 286;
    private const int TopTabGap = 3;

    private readonly XUiC_RebirthPersonalCrafting owner;
    private string lastLayoutDiagnosticKey = string.Empty;

    public RebirthPersonalCraftingLayoutService(XUiC_RebirthPersonalCrafting owner)
    {
        this.owner = owner;
    }

    public bool Apply(bool force)
    {
        if (owner == null || owner.xui == null)
            return false;

        XUiController root = owner.GetChildById("rebirthPersonalCraftingRoot");
        if (root == null || root.ViewComponent == null)
            return false;

        Vector2i screen = owner.xui.GetXUiScreenSize();
        if (screen.x <= 0 || screen.y <= 0)
            return false;

        // PC130: apply the session setting on initialization/open/explicit surface changes.
        // The command updates an already-open window directly. Ordinary layout checks do
        // not poll opacity or traverse the UI; unchanged colors are not rewritten.
        if (force)
            RebirthPersonalCraftingPanelOpacity.ApplyTo(owner);

        int horizontalMargin = Clamp((int)Math.Round(screen.x * 0.0125f), HorizontalMarginMin, HorizontalMarginMax);
        int topMargin = Clamp((int)Math.Round(screen.y * 0.0120f), TopMarginMin, TopMarginMax);
        int bottomReserve = ResolveBottomHudReserve();

        // The approved 1920x1080 presentation is now the maximum Crafting surface. Wider
        // resolutions center this same fixed-size surface instead of stretching the three
        // columns and changing visual density. Smaller screens may still shrink to fit safely.
        bool inventoryOnly = owner.IsInventoryOnlyMode;
        int maxSafeHeight = Math.Max(1, screen.y - topMargin - bottomReserve);

        // PC126: the navigation shell is fixed and independent from the selected content surface.
        // Inventory must never resize the root/top bar; only its centered content panel changes.
        int rootWidth = Math.Min(FixedRootWidth, Math.Max(1, screen.x - 4));
        int rootHeight = Math.Min(FixedRootHeight, maxSafeHeight);
        rootWidth = Math.Max(Math.Min(RootMinWidth, Math.Max(1, screen.x - 4)), rootWidth);
        rootHeight = Math.Max(Math.Min(RootMinHeight, maxSafeHeight), rootHeight);

        // rebirthPersonalCraftingRoot is anchored to XUi Center. XUi window positions are the
        // top-left corner relative to that anchor (for example a 1680-wide centered window uses
        // x=-840). Convert the desired screen-space safe rectangle into Center-anchor coordinates
        // instead of treating (0,0) as the screen's upper-left.
        int rootLeft = Math.Max(2, (screen.x - rootWidth) / 2);
        int rootTop = topMargin;
        int rootPosX = rootLeft - screen.x / 2;
        int rootPosY = screen.y / 2 - rootTop;
        Vector2i rootPosition = new Vector2i(rootPosX, rootPosY);
        Vector2i rootSize = new Vector2i(rootWidth, rootHeight);

        if (!force && owner.State.ScreenSize.x == screen.x && owner.State.ScreenSize.y == screen.y &&
            owner.State.RootSize.x == rootSize.x && owner.State.RootSize.y == rootSize.y &&
            owner.State.RootPosition.x == rootPosition.x && owner.State.RootPosition.y == rootPosition.y)
        {
            ApplyDebugVisibility();
            LogLayoutDiagnostic(screen, rootPosition, rootSize, bottomReserve);
            return false;
        }

        SetRect(root, rootPosition.x, rootPosition.y, rootWidth, rootHeight);
        SetRect(owner.GetChildById("rebirthCraftingRootBackground"), 0, 0, rootWidth, rootHeight);
        SetRect(owner.GetChildById("rebirthCraftingRootFrame"), 0, 0, rootWidth, rootHeight);

        // PC129: retain the full Crafting outline, but its root fill is transparent in XML.
        // Each column owns one translucent background; nested region fills are transparent
        // so they cannot compound into an opaque blanket. Compact Inventory still hides
        // the full Crafting frame. Navigation and all controls retain their own presentation.
        SetControllerActive(owner.GetChildById("rebirthCraftingRootBackground"), !inventoryOnly);
        SetControllerActive(owner.GetChildById("rebirthCraftingRootFrame"), !inventoryOnly);

        int topWidth = Math.Max(1, rootWidth - InnerMargin * 2);
        SetRect(owner.GetChildById("rebirthCraftingTopZone"), InnerMargin, -InnerMargin, topWidth, TopZoneHeight);
        SetRect(owner.GetChildById("rebirthCraftingTopBackground"), 0, 0, topWidth, TopZoneHeight);
        SetRect(owner.GetChildById("rebirthCraftingTopFrame"), 0, 0, topWidth, TopZoneHeight);

        // Navigation geometry/status visibility is surface-invariant.
        ApplyTopLayout(topWidth);

        int bodyY = -(InnerMargin + TopZoneHeight + ZoneGap);
        int bodyHeight = Math.Max(1, rootHeight - InnerMargin * 2 - TopZoneHeight - ZoneGap);
        int bodyWidth = topWidth;
        SetRect(owner.GetChildById("rebirthCraftingBodyZone"), InnerMargin, bodyY, bodyWidth, bodyHeight);

        int leftWidth;
        int centerWidth;
        int rightWidth;

        if (inventoryOnly)
        {
            // Compact Inventory is a separate centered content panel beneath the fixed full-width
            // navigation bar. It must not inherit the full Crafting center-column geometry and it
            // must not resize the outer/root/top navigation shell.
            leftWidth = 1;
            XUiController existingRight = owner.GetChildById("rebirthCraftingRightZone");
            rightWidth = existingRight != null && existingRight.ViewComponent != null
                ? Math.Max(300, existingRight.ViewComponent.Size.x)
                : 496;

            centerWidth = Math.Min(InventoryPanelWidth, bodyWidth);
            centerWidth = Math.Max(Math.Min(InventoryPanelMinWidth, bodyWidth), centerWidth);
            int centerHeight = Math.Min(InventoryPanelHeight, bodyHeight);
            centerHeight = Math.Max(Math.Min(InventoryPanelMinHeight, bodyHeight), centerHeight);
            int centerX = Math.Max(0, (bodyWidth - centerWidth) / 2);

            // PC129: align the compact panel with the full Crafting body bottom.
            // Move the whole center zone, not its children or the fixed navigation.
            // Derive the offset from the safe body height so UI scale/resolution changes
            // keep the existing toolbelt clearance without resizing either surface.
            int centerY = -Math.Max(0, bodyHeight - centerHeight);

            SetControllerActive(owner.GetChildById("rebirthCraftingLeftZone"), false);
            SetControllerActive(owner.GetChildById("rebirthCraftingCenterZone"), true);
            SetZone("rebirthCraftingCenterZone", "rebirthCraftingCenterBackground", "rebirthCraftingCenterFrame", centerX, centerY, centerWidth, centerHeight);
            SetZone("rebirthCraftingRightZone", "rebirthCraftingRightBackground", "rebirthCraftingRightFrame", OffscreenZoneX, 0, rightWidth, bodyHeight);
            ApplyCenterInventoryLayout(centerWidth, centerHeight);
            ApplyRightInternalLayout(rightWidth, bodyHeight);
            ApplyDebugInventoryGeometry(bodyWidth, bodyHeight, centerX, centerY, centerWidth, centerHeight);
        }
        else
        {
            SetControllerActive(owner.GetChildById("rebirthCraftingLeftZone"), true);
            SetControllerActive(owner.GetChildById("rebirthCraftingCenterZone"), true);
            SetControllerActive(owner.GetChildById("rebirthCraftingRightZone"), true);

            int availableWidth = Math.Max(1, bodyWidth - ZoneGap * 2);
            leftWidth = Math.Max(300, (int)Math.Round(availableWidth * 0.25f));
            rightWidth = Math.Max(300, (int)Math.Round(availableWidth * 0.27f));
            centerWidth = Math.Max(420, availableWidth - leftWidth - rightWidth);

            // If minimum widths overrun a small viewport, proportionally reclaim from side columns.
            int overrun = leftWidth + centerWidth + rightWidth - availableWidth;
            if (overrun > 0)
            {
                int fromSides = Math.Min(overrun, Math.Max(0, leftWidth - 260) + Math.Max(0, rightWidth - 260));
                int leftTake = Math.Min(Math.Max(0, leftWidth - 260), (fromSides + 1) / 2);
                leftWidth -= leftTake;
                int rightTake = Math.Min(Math.Max(0, rightWidth - 260), fromSides - leftTake);
                rightWidth -= rightTake;
                overrun -= leftTake + rightTake;
                if (overrun > 0)
                    centerWidth = Math.Max(320, centerWidth - overrun);
            }

            int used = leftWidth + centerWidth + rightWidth;
            if (used < availableWidth)
                centerWidth += availableWidth - used;

            SetZone("rebirthCraftingLeftZone", "rebirthCraftingLeftBackground", "rebirthCraftingLeftFrame", 0, 0, leftWidth, bodyHeight);
            SetZone("rebirthCraftingCenterZone", "rebirthCraftingCenterBackground", "rebirthCraftingCenterFrame", leftWidth + ZoneGap, 0, centerWidth, bodyHeight);
            SetZone("rebirthCraftingRightZone", "rebirthCraftingRightBackground", "rebirthCraftingRightFrame", leftWidth + ZoneGap + centerWidth + ZoneGap, 0, rightWidth, bodyHeight);

            ApplyLeftInternalLayout(leftWidth, bodyHeight);
            ApplyCenterInternalLayout(centerWidth, bodyHeight);
            ApplyRightInternalLayout(rightWidth, bodyHeight);
            ApplyDebugGeometry(bodyWidth, bodyHeight, leftWidth, centerWidth, rightWidth);
        }
        ApplyDebugVisibility();

        owner.State.RecordLayout(screen, rootSize, rootPosition);
        LogLayoutDiagnostic(screen, rootPosition, rootSize, bottomReserve);
        return true;
    }

    private void LogLayoutDiagnostic(Vector2i screen, Vector2i rootPosition, Vector2i rootSize, int bottomReserve)
    {
        if (!RebirthPersonalCraftingState.LayoutDebugEnabled)
            return;

        int left = screen.x / 2 + rootPosition.x;
        int top = screen.y / 2 - rootPosition.y;
        int right = left + rootSize.x;
        int bottom = top + rootSize.y;
        string key = screen.x + "x" + screen.y + ":" + rootPosition.x + "," + rootPosition.y
            + ":" + rootSize.x + "x" + rootSize.y + ":" + bottomReserve;
        if (key == lastLayoutDiagnosticKey)
            return;

        lastLayoutDiagnosticKey = key;
        Log.Out("[REBIRTH Crafting Layout] anchor=Center screen=" + screen.x + "x" + screen.y
            + " rootPos=" + rootPosition.x + "," + rootPosition.y
            + " rootSize=" + rootSize.x + "x" + rootSize.y
            + " edges=" + left + "," + top + ".." + right + "," + bottom
            + " bottomReserve=" + bottomReserve);
    }

    private int ResolveBottomHudReserve()
    {
        int reserve = BottomHudReserveFallback;
        try
        {
            XUiController toolbelt = owner.xui.FindWindowGroupByName("toolbelt");
            XUiController vitals = toolbelt != null
                ? toolbelt.GetChildById("rebirthVitalsLayout")
                : null;
            if (vitals != null && vitals.ViewComponent != null && vitals.ViewComponent.Size.y > 0)
                reserve = Math.Max(reserve, vitals.ViewComponent.Size.y + BottomHudGap + 12);
        }
        catch
        {
            // Fallback is deliberately safe; this is presentation geometry only.
        }
        return reserve;
    }

    private void ApplyTopLayout(int width)
    {
        ApplySharedTopLayout(owner, width);
    }

    // PC133: one navigation sizing implementation shared by Character and Personal Crafting.
    public static void ApplySharedTopLayout(XUiController owner, int width, bool showWorldStatus = true)
    {
        // PC126: navigation is independent from Inventory/Crafting content. The status module is
        // always present, and a narrower status reservation gives the eight tabs nearly the same
        // width/visual density as the approved seven-tab PC124 bar.
        int statusWidth = Clamp((int)Math.Round(width * 0.145f), TopStatusMinWidth, TopStatusMaxWidth);
        int tabsWidth = showWorldStatus ? Math.Max(1, width - statusWidth - ZoneGap) : Math.Max(1, width);

        SetRect(owner.GetChildById("rebirthCraftingTopTabs"), 0, 0, tabsWidth, TopZoneHeight);
        SetControllerActive(owner.GetChildById("rebirthCraftingWorldStatus"), showWorldStatus);
        SetRect(owner.GetChildById("rebirthCraftingWorldStatus"), tabsWidth + ZoneGap, 0, statusWidth, TopZoneHeight);
        SetRect(owner.GetChildById("rebirthCraftingWorldStatusBg"), 0, 0, statusWidth, TopZoneHeight);
        SetRect(owner.GetChildById("rebirthCraftingWorldStatusFrame"), 0, 0, statusWidth, TopZoneHeight);
        ApplyWorldStatusLayout(owner, statusWidth);

        ApplyTopTabsLayout(owner, tabsWidth);
    }

    private static void ApplyTopTabsLayout(XUiController owner, int width)
    {
        string[] tabIds =
        {
            "Crafting", "Character", "Map", "Quests", "Challenges", "Players", "Journal"
        };

        int gaps = TopTabGap * (tabIds.Length - 1);
        int available = Math.Max(tabIds.Length, width - gaps);
        int baseWidth = available / tabIds.Length;
        int remainder = available - baseWidth * tabIds.Length;
        int x = 0;

        for (int i = 0; i < tabIds.Length; i++)
        {
            int tabWidth = baseWidth + (i < remainder ? 1 : 0);
            string suffix = tabIds[i];
            XUiController tab = owner.GetChildById("rebirthCraftingTab" + suffix);
            SetRect(tab, x, 0, tabWidth, TopZoneHeight);
            SetRect(owner.GetChildById("rebirthCraftingTab" + suffix + "Bg"), 0, 0, tabWidth, TopZoneHeight);
            SetRect(owner.GetChildById("rebirthCraftingTab" + suffix + "Frame"), 0, 0, tabWidth, TopZoneHeight);
            SetRect(owner.GetChildById("btnRebirthCraftingTab" + suffix), 0, 0, tabWidth, TopZoneHeight);
            SetRect(owner.GetChildById("rebirthCraftingTab" + suffix + "Icon"), 10, -12, 28, 28);
            SetRect(owner.GetChildById("rebirthCraftingTab" + suffix + "Label"), 43, -18, Math.Max(1, tabWidth - 49), 30);
            if (suffix == "Crafting")
                SetRect(owner.GetChildById("rebirthCraftingTabCraftingActive"), 0, -49, tabWidth, 3);
            x += tabWidth + TopTabGap;
        }
    }

    private static void ApplyWorldStatusLayout(XUiController owner, int width)
    {
        const int rightPad = 6;
        const int dayWidth = 82;
        const int timeWidth = 78;
        const int tempWidth = 94;
        int total = dayWidth + timeWidth + tempWidth;
        int x = Math.Max(4, width - rightPad - total);

        // Keep the useful world status clustered at the right edge. Child icon/label coordinates
        // are local to each module and are authored in XML; no biome module is present.
        SetRect(owner.GetChildById("rebirthCraftingStatusDayModule"), x, 0, dayWidth, TopZoneHeight);
        x += dayWidth;
        SetRect(owner.GetChildById("rebirthCraftingStatusTimeModule"), x, 0, timeWidth, TopZoneHeight);
        x += timeWidth;
        SetRect(owner.GetChildById("rebirthCraftingStatusTemperatureModule"), x, 0, tempWidth, TopZoneHeight);
    }

    private void ApplyLeftInternalLayout(int width, int height)
    {
        const int headerHeight = 38;
        int outcomeHeight = Clamp((int)Math.Round(height * 0.33f), 180, 285);
        int recipeHeight = Math.Max(1, height - outcomeHeight - ZoneGap);

        SetRect(owner.GetChildById("rebirthCraftingRecipesRegion"), 0, 0, width, recipeHeight);
        SetRect(owner.GetChildById("rebirthCraftingRecipesRegionBg"), 0, 0, width, recipeHeight);
        SetRect(owner.GetChildById("rebirthCraftingRecipesHeaderRule"), 0, -headerHeight, width, 2);

        SetRect(owner.GetChildById("rebirthCraftingOutcomeRegion"), 0, -(recipeHeight + ZoneGap), width, outcomeHeight);
        SetRect(owner.GetChildById("rebirthCraftingOutcomeRegionBg"), 0, 0, width, outcomeHeight);
        SetRect(owner.GetChildById("rebirthCraftingOutcomeHeaderRule"), 0, -headerHeight, width, 2);
        ApplyOutcomeInternalLayout(width, outcomeHeight);
    }

    private void ApplyOutcomeInternalLayout(int width, int height)
    {
        XUiC_RebirthCraftingOutcome.ApplyOutcomeLayout(owner, width, height);
    }

    private void ApplyCenterInternalLayout(int width, int height)
    {
        const int headerHeight = 38;
        SetControllerActive(owner.GetChildById("rebirthCraftingRequirementsRegion"), true);
        // Chunk J: outer center geometry is state-invariant. Reserve enough height for the
        // Crafting Backpack header plus four complete minimum-size rows before distributing
        // the remaining space to Details and Requirements. The backpack uses a wide 13-column
        // proportional viewport with four complete rows. The entire stock ItemStack control is scaled uniformly.
        const int inventoryMinHeight = 310;
        int detailsHeight = Clamp((int)Math.Round(height * 0.34f), 244, 300);
        int requirementsHeight = Clamp((int)Math.Round(height * 0.24f), 120, 220);
        int inventoryHeight = height - detailsHeight - requirementsHeight - ZoneGap * 2;
        if (inventoryHeight < inventoryMinHeight)
        {
            int deficit = inventoryMinHeight - inventoryHeight;
            int reclaimDetails = Math.Min(deficit, Math.Max(0, detailsHeight - 244));
            detailsHeight -= reclaimDetails;
            deficit -= reclaimDetails;
            int reclaimRequirements = Math.Min(deficit, Math.Max(0, requirementsHeight - 110));
            requirementsHeight -= reclaimRequirements;
            deficit -= reclaimRequirements;
            inventoryHeight = Math.Max(1, height - detailsHeight - requirementsHeight - ZoneGap * 2);
        }

        SetRect(owner.GetChildById("rebirthCraftingDetailsRegion"), 0, 0, width, detailsHeight);
        SetRect(owner.GetChildById("rebirthCraftingDetailsRegionBg"), 0, 0, width, detailsHeight);
        SetRect(owner.GetChildById("rebirthCraftingDetailsHeaderRule"), 0, -headerHeight, width, 2);

        // The action strip must always be owned by the current center surface width. PC125 left it
        // at a stale surface width during Inventory/Crafting switches, allowing Selected Item
        // actions to extend into the queue. Recipe and Item actions both read this same strip.
        // RecipeDetails alone owns the action-row geometry; it must remain below the metadata.
        owner.GetChildByType<XUiC_RebirthCraftingRecipeDetails>()?.ApplyGeometry(true);

        // Selected Item is an alternate context for exactly the same top surface. It is a
        // sibling of Selected Recipe, never an inventory overlay. The item-context controller
        // toggles which of these two surfaces is visible.
        int itemContextHeight = detailsHeight + ZoneGap + requirementsHeight;
        SetRect(owner.GetChildById("rebirthCraftingItemContext"), 0, 0, width, itemContextHeight);
        SetRect(owner.GetChildById("rebirthCraftingItemContextBg"), 0, 0, width, itemContextHeight);
        SetRect(owner.GetChildById("rebirthCraftingItemContextFrame"), 0, 0, width, itemContextHeight);

        int requirementsY = -(detailsHeight + ZoneGap);
        SetRect(owner.GetChildById("rebirthCraftingRequirementsRegion"), 0, requirementsY, width, requirementsHeight);
        SetRect(owner.GetChildById("rebirthCraftingRequirementsRegionBg"), 0, 0, width, requirementsHeight);
        SetRect(owner.GetChildById("rebirthCraftingRequirementsHeaderRule"), 0, -headerHeight, width, 2);

        int inventoryY = -(detailsHeight + ZoneGap + requirementsHeight + ZoneGap);
        SetRect(owner.GetChildById("rebirthCraftingInventoryRegion"), 0, inventoryY, width, inventoryHeight);
        SetRect(owner.GetChildById("rebirthCraftingInventoryRegionBg"), 0, 0, width, inventoryHeight);
        SetRect(owner.GetChildById("rebirthCraftingInventoryHeaderRule"), 0, -headerHeight, width, 2);
        ApplyInventoryInternalLayout(width, inventoryHeight);
        owner.GetChildByType<XUiC_RebirthCraftingItemContext>()?.ApplySurfaceMode();
    }

    private void ApplyCenterInventoryLayout(int width, int height)
    {
        const int headerHeight = 38;
        const int detailsPreferred = 232;
        int detailsHeight = Clamp(detailsPreferred, 200, Math.Max(200, height - 300));
        int inventoryHeight = Math.Max(1, height - detailsHeight - ZoneGap);

        SetRect(owner.GetChildById("rebirthCraftingDetailsRegion"), 0, 0, width, detailsHeight);
        SetRect(owner.GetChildById("rebirthCraftingDetailsRegionBg"), 0, 0, width, detailsHeight);
        SetRect(owner.GetChildById("rebirthCraftingDetailsHeaderRule"), 0, -headerHeight, width, 2);
        owner.GetChildByType<XUiC_RebirthCraftingRecipeDetails>()?.ApplyGeometry(true);

        // Requirements are a Crafting-only surface. In compact Inventory the Selected Item context
        // occupies exactly the top details rectangle and Inventory begins immediately below it.
        SetControllerActive(owner.GetChildById("rebirthCraftingRequirementsRegion"), false);
        SetRect(owner.GetChildById("rebirthCraftingItemContext"), 0, 0, width, detailsHeight);
        SetRect(owner.GetChildById("rebirthCraftingItemContextBg"), 0, 0, width, detailsHeight);
        SetRect(owner.GetChildById("rebirthCraftingItemContextFrame"), 0, 0, width, detailsHeight);

        int inventoryY = -(detailsHeight + ZoneGap);
        SetRect(owner.GetChildById("rebirthCraftingInventoryRegion"), 0, inventoryY, width, inventoryHeight);
        SetRect(owner.GetChildById("rebirthCraftingInventoryRegionBg"), 0, 0, width, inventoryHeight);
        SetRect(owner.GetChildById("rebirthCraftingInventoryHeaderRule"), 0, -headerHeight, width, 2);
        ApplyInventoryInternalLayout(width, inventoryHeight);

        owner.GetChildByType<XUiC_RebirthCraftingItemContext>()?.ApplySurfaceMode();
    }

    private void ApplyInventoryInternalLayout(int width, int height)
    {
        const int pad = 12;
        const int headerHeight = 38;
        const int headerButton = 28;

        // Align the four header buttons to the actual 13-column viewport rather than the outer
        // inventory region. The Companions button ends exactly where the last slot ends, immediately
        // before the scrollbar; Sort/Lock/Quick Stack remain a left-aligned group leading into it.
        int scrollWidthForSlots = Math.Max(1, width - pad * 2);
        int scrollHeightForSlots = Math.Max(1, height - headerHeight - 8);
        int availableSlotWidth = Math.Max(13 * 36, scrollWidthForSlots - 28);
        int availableSlotHeight = Math.Max(4 * 36, scrollHeightForSlots);
        int pitch = Math.Max(40, availableSlotWidth / 13);
        if (pitch * 4 > availableSlotHeight)
            pitch = Math.Max(40, availableSlotHeight / 4);
        int slotViewportRight = pad + pitch * 13;
        int companionsX = Math.Max(pad + 152, slotViewportRight - headerButton / 2);
        int quickStackX = companionsX - 38;
        int lockX = quickStackX - 38;
        int sortX = lockX - 38;
        int libraryX = sortX - 38;
        int capacityX = 154;
        int capacityWidth = Math.Max(120, libraryX - capacityX - 18);

        SetRect(owner.GetChildById("rebirthCraftingInventoryTitle"), 14, -19, 126, 28);
        SetRect(owner.GetChildById("rebirthCraftingInventoryCapacity"), capacityX, -19, capacityWidth, 28);
        SetRect(owner.GetChildById("btnRebirthCraftingInventoryLibrary"), libraryX, -20, headerButton, headerButton);
        SetRect(owner.GetChildById("btnRebirthCraftingInventorySort"), sortX, -20, headerButton, headerButton);
        SetRect(owner.GetChildById("btnRebirthCraftingInventoryLock"), lockX, -20, headerButton, headerButton);
        SetRect(owner.GetChildById("rebirthCraftingInventoryLockActive"), lockX - 15, -36, 30, 3);
        SetRect(owner.GetChildById("btnRebirthCraftingInventoryQuickStack"), quickStackX, -20, headerButton, headerButton);
        SetRect(owner.GetChildById("btnRebirthCraftingInventoryCompanions"), companionsX, -20, headerButton, headerButton);

        int scrollWidth = Math.Max(1, width - pad * 2);
        int scrollHeight = Math.Max(1, height - headerHeight - 8);
        SetRect(owner.GetChildById("rebirthCraftingInventoryScroll"), pad, -(headerHeight + 8), scrollWidth, scrollHeight);

        // PC131: complete the shared backpack geometry in this same layout transaction.
        // The next scroll must only move rows, not finally apply the previous tab switch.
        owner.GetChildByType<XUiC_RebirthCraftingInventoryScroll>()?.ApplyLayoutGeometry();
    }

    private void ApplyRightInternalLayout(int width, int height)
    {
        SetRect(owner.GetChildById("rebirthCraftingQueueRegion"), 0, 0, width, height);
        SetRect(owner.GetChildById("rebirthCraftingQueueRegionBg"), 0, 0, width, height);
        SetRect(owner.GetChildById("rebirthCraftingQueueHeaderRule"), 0, -38, width, 2);
        SetRect(owner.GetChildById("rebirthCraftingQueueController"), 0, 0, width, height);
    }

    private void ApplyDebugInventoryGeometry(int bodyWidth, int bodyHeight, int centerX, int centerY, int centerWidth, int centerHeight)
    {
        XUiController debug = owner.GetChildById("rebirthCraftingLayoutDebug");
        if (debug == null)
            return;

        SetRect(debug, InnerMargin, -(InnerMargin + TopZoneHeight + ZoneGap), bodyWidth, bodyHeight);
        SetRect(owner.GetChildById("rebirthCraftingDebugLeft"), 0, 0, 1, 1);
        SetRect(owner.GetChildById("rebirthCraftingDebugCenter"), centerX, centerY, centerWidth, centerHeight);
        SetRect(owner.GetChildById("rebirthCraftingDebugRight"), OffscreenZoneX, 0, 1, 1);
    }

    private void ApplyDebugGeometry(int bodyWidth, int bodyHeight, int leftWidth, int centerWidth, int rightWidth)
    {
        XUiController debug = owner.GetChildById("rebirthCraftingLayoutDebug");
        if (debug == null)
            return;

        SetRect(debug, InnerMargin, -(InnerMargin + TopZoneHeight + ZoneGap), bodyWidth, bodyHeight);
        SetRect(owner.GetChildById("rebirthCraftingDebugLeft"), 0, 0, leftWidth, bodyHeight);
        SetRect(owner.GetChildById("rebirthCraftingDebugCenter"), leftWidth + ZoneGap, 0, centerWidth, bodyHeight);
        SetRect(owner.GetChildById("rebirthCraftingDebugRight"), leftWidth + ZoneGap + centerWidth + ZoneGap, 0, rightWidth, bodyHeight);
    }

    private void ApplyDebugVisibility()
    {
        XUiController debug = owner.GetChildById("rebirthCraftingLayoutDebug");
        if (debug != null && debug.ViewComponent != null)
            debug.ViewComponent.IsVisible = RebirthPersonalCraftingState.LayoutDebugEnabled;
    }

    private void SetZone(string zoneId, string bgId, string frameId, int x, int y, int width, int height)
    {
        SetRect(owner.GetChildById(zoneId), x, y, width, height);
        SetRect(owner.GetChildById(bgId), 0, 0, width, height);
        SetRect(owner.GetChildById(frameId), 0, 0, width, height);
    }

    private static void SetControllerActive(XUiController controller, bool active)
    {
        if (controller == null || controller.ViewComponent == null)
            return;
        if (controller.ViewComponent.IsVisible != active)
            controller.ViewComponent.IsVisible = active;
        if (controller.ViewComponent.Enabled != active)
            controller.ViewComponent.Enabled = active;
        GameObject go = controller.ViewComponent.UiTransform != null ? controller.ViewComponent.UiTransform.gameObject : null;
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }

    private static void SetRect(XUiController controller, int x, int y, int width, int height)
    {
        if (controller == null || controller.ViewComponent == null)
            return;

        controller.ViewComponent.Position = new Vector2i(x, y);
        controller.ViewComponent.Size = new Vector2i(Math.Max(1, width), Math.Max(1, height));
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
