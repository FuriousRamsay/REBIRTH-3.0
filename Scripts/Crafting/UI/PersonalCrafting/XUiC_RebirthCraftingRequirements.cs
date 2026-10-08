using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Rebirth-owned Requirements region. Six cards are visible in a 2x3 viewport; recipes with
/// more than six ingredients retain access through the stock draggable scrollbar.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingRequirements : XUiController
{
    private const int Columns = 2;
    private const int VisibleRows = 3;
    private const int VisibleCards = Columns * VisibleRows;
    private const int ScrollbarWidth = 18;

    private XUiC_RebirthPersonalCrafting owner;
    private XUiC_RebirthCraftingRecipeCatalogue catalogue;
    private XUiC_RebirthCraftingRecipeDetails details;
    private XUiC_RecipeCraftCount craftCount;
    private XUiC_RebirthCraftingInventory inventoryPresenter;
    private XUiC_RebirthCraftingRequirementEntry[] entries = new XUiC_RebirthCraftingRequirementEntry[VisibleCards];
    private XUiV_Label countLabel;
    private XUiController scrollHost;
    private XUiController scrollView;
    private XUiController pagingRegisteredHost;
    private UIScrollView pagingRegisteredView;
    private UIProgressBar pagingRegisteredBar;
    private void RefreshPagingRegistration()
    {
        UIScrollView view = (scrollView?.ViewComponent as XUiV_ScrollView)?.scrollView;
        UIProgressBar bar = view != null ? view.verticalScrollBar : null;
        if (ReferenceEquals(pagingRegisteredHost, scrollHost) && ReferenceEquals(pagingRegisteredView, view)
            && ReferenceEquals(pagingRegisteredBar, bar)) return;
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(pagingRegisteredHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(scrollHost);
        pagingRegisteredHost = scrollHost; pagingRegisteredView = view; pagingRegisteredBar = bar;
    }
    private XUiController scrollProxy;
    private List<RebirthCraftingRequirementProjectionService.Requirement> requirements = new List<RebirthCraftingRequirementProjectionService.Requirement>();
    private Recipe lastRecipe;
    private int lastBatch = -1;
    private int lastTier = -1;
    private int lastFingerprint;
    private long lastInventoryRevision = long.MinValue;
    private long lastRemoteRevision = long.MinValue;
    private int firstVisibleRow;
    private float lastScrollbarValue = -1f;
    private float nextRefresh;
    private Vector2i lastSize = new Vector2i(-1, -1);
    private long projectionRevision;

    public long ProjectionRevision => projectionRevision;
    public IList<RebirthCraftingRequirementProjectionService.Requirement> CurrentRequirements => requirements;
    public bool AllMaterialsAvailable
    {
        get
        {
            for (int i = 0; i < requirements.Count; i++) if (!requirements[i].HasEnough) return false;
            return requirements.Count > 0;
        }
    }

    public override void Init()
    {
        base.Init();
        owner = GetParentByType<XUiC_RebirthPersonalCrafting>();
        catalogue = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingRecipeCatalogue>() : null;
        details = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingRecipeDetails>() : null;
        craftCount = owner != null ? owner.GetChildByType<XUiC_RecipeCraftCount>() : null;
        inventoryPresenter = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingInventory>() : null;
        countLabel = GetView<XUiV_Label>("rebirthCraftingRequirementCount");
        scrollHost = GetChildById("rebirthCraftingRequirementScrollHost");
        scrollView = GetChildById("rebirthCraftingRequirementScrollView");
        scrollProxy = GetChildById("rebirthCraftingRequirementScrollProxy");
        for (int i = 0; i < VisibleCards; i++)
        {
            entries[i] = GetChildById("rebirthCraftingRequirementCard" + i) as XUiC_RebirthCraftingRequirementEntry;
            if (entries[i] != null) entries[i].OnScroll += HandleScroll;
        }

        if (craftCount != null) craftCount.OnCountChanged += CraftCount_OnCountChanged;
        OnScroll += HandleScroll;
        if (scrollHost != null) scrollHost.OnScroll += HandleScroll;
        if (scrollView != null) scrollView.OnScroll += HandleScroll;
        ApplyGeometry(true);
        RefreshNow();
    }

        public override void OnClose()
    {
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(pagingRegisteredHost);
        pagingRegisteredHost = null; pagingRegisteredView = null; pagingRegisteredBar = null;
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(scrollHost);
        base.OnClose();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        firstVisibleRow = 0;
        lastScrollbarValue = -1f;
        nextRefresh = 0f;
        RefreshNow();
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(scrollHost);
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (owner == null || !owner.State.IsOpen) return;
        ApplyGeometry(false);
        RefreshPagingRegistration();
        int beforePagingRow = firstVisibleRow;
        ClampOffset();
        if (beforePagingRow != firstVisibleRow) { ApplyEntries(); SyncScrollbarToOffset(); }
        PollScrollbar();
        if (Time.realtimeSinceStartup >= nextRefresh)
        {
            nextRefresh = Time.realtimeSinceStartup + 0.20f;
            RebuildIfChanged(false);
        }
    }

    public void RefreshNow()
    {
        nextRefresh = 0f;
        RebuildIfChanged(true);
    }

    private void CraftCount_OnCountChanged(XUiController sender, OnCountChangedEventArgs args)
    {
        RefreshNow();
    }

    private void RebuildIfChanged(bool force)
    {
        Recipe recipe = owner?.Coordinator?.SelectedRecipe ?? (catalogue != null ? catalogue.CurrentRecipe : null);
        int batch = craftCount != null ? Math.Max(1, craftCount.Count) : 1;
        int tier = details != null ? Math.Max(1, details.SelectedCraftingTier) : 1;
        long inventoryRevision = inventoryPresenter != null ? inventoryPresenter.ProjectionRevision : 0L;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        long remoteRevision = connection != null && connection.IsServer
            ? RemoteResourceSnapshotCache.ProjectionRevision
            : RemoteResourceClientAvailability.ProjectionRevision;
        if (!force && recipe == lastRecipe && batch == lastBatch && tier == lastTier &&
            inventoryRevision == lastInventoryRevision && remoteRevision == lastRemoteRevision)
            return;

        List<RebirthCraftingRequirementProjectionService.Requirement> next =
            RebirthCraftingRequirementProjectionService.Build(xui, recipe, batch, tier);
        int fingerprint = BuildFingerprint(next);

        if (!force && recipe == lastRecipe && batch == lastBatch && tier == lastTier && fingerprint == lastFingerprint)
        {
            lastInventoryRevision = inventoryRevision;
            lastRemoteRevision = remoteRevision;
            return;
        }

        bool recipeChanged = recipe != lastRecipe;
        lastRecipe = recipe;
        lastBatch = batch;
        lastTier = tier;
        lastFingerprint = fingerprint;
        lastInventoryRevision = inventoryRevision;
        lastRemoteRevision = remoteRevision;
        requirements = next;
        unchecked { projectionRevision++; }
        owner?.Coordinator?.RecordBatch(batch);
        bool materialsSufficient = recipe != null && (requirements.Count == 0 || AllMaterialsAvailable);
        owner?.Coordinator?.RecordMaterials(recipe != null, materialsSufficient);
        if (recipeChanged) firstVisibleRow = 0;
        ClampOffset();
        // Scrollbar presence changes usable card width. Reflow the fixed 2x3 viewport when
        // crossing the six-requirement boundary without moving the outer Requirements region.
        ApplyGeometry(true);
        ApplyEntries();
        UpdateScrollbarGeometry();
        SyncScrollbarToOffset();
    }

    private void ApplyEntries()
    {
        int first = firstVisibleRow * Columns;
        for (int i = 0; i < entries.Length; i++)
        {
            int index = first + i;
            if (entries[i] != null)
                entries[i].Bind(index >= 0 && index < requirements.Count ? requirements[index] : null);
        }

        if (countLabel != null)
        {
            if (requirements.Count <= VisibleCards)
                countLabel.Text = requirements.Count + " / " + VisibleCards;
            else
                countLabel.Text = Math.Min(VisibleCards, Math.Max(0, requirements.Count - first)) + " / " + requirements.Count;
        }
    }

    private void HandleScroll(XUiController sender, float delta)
    {
        int max = MaxFirstRow();
        if (max <= 0 || Math.Abs(delta) < 0.001f) return;
        int direction = delta > 0f ? -1 : 1;
        int next = RebirthScrollbarPagingPolicy.Enabled ? (int)RebirthScrollbarPagingPolicy.Step(firstVisibleRow, max, VisibleRows, direction) : Mathf.Clamp(firstVisibleRow + direction, 0, max);
        if (next == firstVisibleRow) return;
        firstVisibleRow = next;
        ApplyEntries();
        SyncScrollbarToOffset();
    }

    private void PollScrollbar()
    {
        int max = MaxFirstRow();
        if (max <= 0 || scrollView == null) return;
        float value;
        if (!RebirthNativeScrollbarUtil.TryGetValue(scrollView, out value)) return;
        if (lastScrollbarValue >= 0f && Math.Abs(value - lastScrollbarValue) < 0.001f) return;
        lastScrollbarValue = value;
        int row = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(value * max), 0, max), max, VisibleRows, RebirthScrollbarPagingPolicy.Enabled);
        if (row == firstVisibleRow) return;
        firstVisibleRow = row;
        ApplyEntries();
    }

    private void UpdateScrollbarGeometry()
    {
        int max = MaxFirstRow();
        if (scrollHost?.ViewComponent != null) scrollHost.ViewComponent.IsVisible = max > 0;
        if (scrollProxy?.ViewComponent != null)
        {
            int viewport = Math.Max(1, scrollView?.ViewComponent != null ? scrollView.ViewComponent.Size.y : 100);
            int totalRows = Math.Max(VisibleRows, (requirements.Count + Columns - 1) / Columns);
            scrollProxy.ViewComponent.Size = new Vector2i(1, Math.Max(viewport, viewport * totalRows / VisibleRows));
        }
        if (scrollView != null)
        {
            RebirthNativeScrollbarUtil.Refresh(scrollView);
            lastScrollbarValue = -1f;
        }
    }

    private void SyncScrollbarToOffset()
    {
        int max = MaxFirstRow();
        float value = max > 0 ? (float)firstVisibleRow / max : 0f;
        RebirthNativeScrollbarUtil.TrySetValue(scrollView, value);
        RebirthNativeScrollbarUtil.Refresh(scrollView);
        lastScrollbarValue = value;
    }

    private int MaxFirstRow()
    {
        int totalRows = (requirements.Count + Columns - 1) / Columns;
        return Math.Max(0, totalRows - VisibleRows);
    }

    private void ClampOffset()
    {
        firstVisibleRow = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(firstVisibleRow, 0, MaxFirstRow()), MaxFirstRow(), VisibleRows, RebirthScrollbarPagingPolicy.Enabled);
    }

    private void ApplyGeometry(bool force)
    {
        if (ViewComponent == null) return;
        Vector2i size = ViewComponent.Size;
        if (!force && size.x == lastSize.x && size.y == lastSize.y) return;
        lastSize = size;

        int width = Math.Max(420, size.x);
        int height = Math.Max(130, size.y);
        int top = 46;
        int rightPad = 12;
        int leftPad = 12;
        int gapX = 8;
        int gapY = 6;
        int scrollbarSpace = MaxFirstRow() > 0 ? ScrollbarWidth + 6 : 0;
        int usableWidth = Math.Max(200, width - leftPad - rightPad - scrollbarSpace);
        int cardWidth = Math.Max(140, (usableWidth - gapX) / Columns);
        int usableHeight = Math.Max(72, height - top - 8);
        int cardHeight = Math.Max(24, (usableHeight - gapY * (VisibleRows - 1)) / VisibleRows);

        for (int row = 0; row < VisibleRows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                int i = row * Columns + col;
                SetRect(entries[i], leftPad + col * (cardWidth + gapX), -(top + row * (cardHeight + gapY)), cardWidth, cardHeight);
                if (entries[i] != null) ApplyCardGeometry(entries[i], cardWidth, cardHeight);
            }
        }
        SetRect(GetChildById("rebirthCraftingRequirementCount"), width - 116, -8, 100, 26);
        SetRect(scrollHost, width - rightPad - ScrollbarWidth, -top, ScrollbarWidth, usableHeight);
        SetRect(scrollView, 0, 0, 1, usableHeight);
        UpdateScrollbarGeometry();
    }

    private static void ApplyCardGeometry(XUiC_RebirthCraftingRequirementEntry entry, int width, int height)
    {
        int iconSize = Math.Max(24, Math.Min(36, height - 6));
        int textX = iconSize + 12;
        const int rightPad = 8;
        const int valueWidth = 64;
        const int labelWidth = 58;
        const int labelValueGap = 6;
        int valueX = Math.Max(textX + 92, width - rightPad - valueWidth);
        int labelX = Math.Max(textX + 28, valueX - labelValueGap - labelWidth);
        int nameWidth = Math.Max(70, labelX - textX - 10);
        int half = Math.Max(18, height / 2);

        SetRect(entry.GetChildById("rebirthCraftingRequirementBackground"), 0, 0, width, height);
        SetRect(entry.GetChildById("rebirthCraftingRequirementFrame"), 0, 0, width, height);
        SetRect(entry.GetChildById("rebirthCraftingRequirementIcon"), 6, -3, iconSize, iconSize);
        SetRect(entry.GetChildById("rebirthCraftingRequirementName"), textX, -3, nameWidth, height - 6);

        // HAVE / NEED are four independent labels. Keep both captions and both values pinned
        // to the right edge of the live card rather than to the original 338px XML template.
        SetRect(entry.GetChildById("rebirthCraftingRequirementHaveLabel"), labelX, -2, labelWidth, half);
        SetRect(entry.GetChildById("rebirthCraftingRequirementHaveValue"), valueX, -2, valueWidth, half);
        SetRect(entry.GetChildById("rebirthCraftingRequirementNeedLabel"), labelX, -half, labelWidth, half);
        SetRect(entry.GetChildById("rebirthCraftingRequirementNeedValue"), valueX, -half, valueWidth, half);
    }

    private static int BuildFingerprint(List<RebirthCraftingRequirementProjectionService.Requirement> values)
    {
        unchecked
        {
            int hash = values != null ? values.Count : 0;
            for (int i = 0; values != null && i < values.Count; i++) hash = hash * 31 + values[i].Fingerprint;
            return hash;
        }
    }

    private T GetView<T>(string id) where T : XUiView
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as T : null;
    }

    private static void SetRect(XUiController controller, int x, int y, int width, int height)
    {
        if (controller?.ViewComponent == null) return;
        controller.ViewComponent.Position = new Vector2i(x, y);
        controller.ViewComponent.Size = new Vector2i(Math.Max(1, width), Math.Max(1, height));
    }
}
