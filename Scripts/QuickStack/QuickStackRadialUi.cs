using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum QuickStackRadialAction : byte
{
    Deposit = 0,
    DepositOwned = 1,
    Restock = 2,
    CollectWorkstationOutputs = 3,
    PushVehicle = 4,
    PullVehicle = 5,
    PushDrone = 6,
    PullDrone = 7,
    // Internal companion-window transfer; not a radial menu choice.
    CompanionInventoryPull = 8,
    // Credit-receipt variants preserve the wire meaning of legacy action IDs 5 and 7.
    PullVehicleCredits = 9,
    PullDroneCredits = 10
}

public static class RebirthFeatureAvailability
{
    public static bool Logistics(EntityPlayer player, QuickStackRadialAction action, out string reason)
    {
        reason = !QuickStackRuntimePolicy.Enabled ? "Quick Stack is disabled" : string.Empty;
        return player != null && QuickStackRuntimePolicy.Enabled && (byte)action <= (byte)QuickStackRadialAction.PullDrone;
    }
    public static bool Companion(EntityPlayer player, RebirthCompanionCommand command, out string reason) { reason = string.Empty; return player != null; }
}

public static class QuickStackRadialUiService
{
    public const string WindowGroupName = "rebirthLogistics";
    public static void Open(XUi xui, QuickStackRadialAction initial = QuickStackRadialAction.Deposit)
    {
        if (xui == null || !QuickStackRuntimePolicy.Enabled) return;
        XUiController group = xui.FindWindowGroupByName(WindowGroupName);
        XUiC_RebirthLogistics controller = group != null ? group.GetChildByType<XUiC_RebirthLogistics>() : null;
        if (controller == null) { Log.Error("[Logistics] controller not found"); return; }
        controller.Prepare(initial);
        // Use the same compile-safe modal GUIWindow overload already used by
        // the Rebirth category/companion windows. Opening the actual windowGroup
        // also avoids depending on the non-public close-all helper that is not
        // exposed by the 3.1 compile surface used by RebirthUtils.
        xui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, true, true);
    }
}

public static class QuickStackUiDiagnostics
{
    public static bool Enabled;
    public static void Write(string text) { if (Enabled) Log.Out("[QuickStackUI] " + text); }
}

[Preserve]
public sealed class XUiC_RebirthLogistics : XUiController
{
    private readonly XUiController[] actionButtons = new XUiController[8];
    private XUiV_Label title;
    private XUiV_Label description;
    private XUiV_Label itemHeader;
    private XUiV_Label itemEmpty;
    private XUiV_Label targetEmpty;
    private XUiV_Label targetHeader;
    private XUiV_Label targetHint;
    private XUiController itemPaneScrollCapture;
    private XUiController targetPaneScrollCapture;
    private XUiController itemScrollTrackInput;
    private XUiController itemScrollThumbControl;
    private XUiV_Button itemScrollThumb;
    private XUiController targetScrollTrackInput;
    private XUiController targetScrollThumbControl;
    private XUiV_Button targetScrollThumb;
    private QuickStackRadialAction action;
    private LogisticsPreviewData previewData = new LogisticsPreviewData();
    private string selectedTargetId = string.Empty;
    private bool suppressSelectionEvents;
    private readonly HashSet<string> excludedSelections = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> quantityOverrides =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private bool suppressQuantityEvents;
    private bool wasCursorHidden;
    private int itemScrollIndex;
    private int targetScrollIndex;
    private bool itemScrollDragging;
    private float itemScrollDragAccumulatedUiY;
    private int itemScrollDragStartOffset;
    private bool targetScrollDragging;
    private float targetScrollDragAccumulatedUiY;
    private int targetScrollDragStartOffset;
    private const int VisibleItemRows = 6;
    private const int VisibleTargetRows = 5;
    private const int ItemScrollTrackHeight = 282;
    private const int TargetScrollTrackHeight = 242;
    private readonly PreviewRowView[] itemRows = new PreviewRowView[VisibleItemRows];
    private readonly PreviewRowView[] targetRows = new PreviewRowView[VisibleTargetRows];

    private sealed class PreviewRowView
    {
        public XUiController Root;
        public XUiController Select;
        public XUiV_Sprite Selection;
        public XUiV_Sprite Icon;
        public XUiC_ToggleButton Include;
        public XUiV_Label Name;
        public XUiC_ComboBoxInt Count;
        public XUiV_Label CountDisplay;
        public XUiV_Label Secondary;
        public XUiV_Label Distance;
    }

    public override void Init()
    {
        base.Init();
        title = GetLabel("actionTitle");
        description = GetLabel("actionDescription");
        itemHeader = GetLabel("itemHeader");
        itemEmpty = GetLabel("itemEmpty");
        targetEmpty = GetLabel("targetEmpty");
        targetHeader = GetLabel("targetHeader");
        targetHint = GetLabel("targetHint");
        itemPaneScrollCapture = GetChildById("itemPaneScrollCapture");
        targetPaneScrollCapture = GetChildById("targetPaneScrollCapture");
        itemScrollTrackInput = GetChildById("itemScrollTrackInput");
        itemScrollThumbControl = GetChildById("itemScrollThumb");
        itemScrollThumb = itemScrollThumbControl != null ? itemScrollThumbControl.ViewComponent as XUiV_Button : null;
        targetScrollTrackInput = GetChildById("targetScrollTrackInput");
        targetScrollThumbControl = GetChildById("targetScrollThumb");
        targetScrollThumb = targetScrollThumbControl != null ? targetScrollThumbControl.ViewComponent as XUiV_Button : null;

        int foundToggles = 0;
        int foundClickables = 0;
        for (int i = 0; i < itemRows.Length; i++)
        {
            itemRows[i] = GetPreviewRow("itemRow" + i.ToString("00"));
            PreviewRowView rowView = itemRows[i];
            if (rowView == null) continue;

            int captured = i;
            if (rowView.Include != null)
            {
                foundToggles++;
                rowView.Include.OnValueChanged += delegate(XUiC_ToggleButton sender, bool value)
                {
                    ItemSelection_OnValueChanged(captured, value);
                };

                XUiController clickable = rowView.Include.GetChildById("clickable");
                if (clickable != null)
                {
                    foundClickables++;
                    clickable.OnPress += delegate(XUiController sender, int mouseButton)
                    {
                        QuickStackUiDiagnostics.Write("checkbox press slot=" + captured +
                            " dataIndex=" + GetItemDataIndex(captured) +
                            " mouseButton=" + mouseButton + " sender=" + DescribeController(sender));
                    };
                    clickable.OnHover += delegate(XUiController sender, bool isOver)
                    {
                        if (isOver) QuickStackUiDiagnostics.Write("checkbox hover slot=" + captured +
                            " dataIndex=" + GetItemDataIndex(captured) +
                            " sender=" + DescribeController(sender));
                    };
                    WireScroll(clickable, ItemList_OnScroll);
                }
            }

            // Make the entire visible row participate in logical list scrolling, not
            // only the text/icon children. This also covers the blank horizontal
            // space between the item name and quantity control.
            WireScroll(rowView.Root, ItemList_OnScroll);
            WireScroll(rowView.Icon != null ? rowView.Icon.Controller : null, ItemList_OnScroll);
            WireScroll(rowView.Name != null ? rowView.Name.Controller : null, ItemList_OnScroll);
            WireScroll(rowView.CountDisplay != null ? rowView.CountDisplay.Controller : null, ItemList_OnScroll);

            if (rowView.Count != null)
            {
                rowView.Count.OnValueChanged += delegate(XUiController sender, long oldValue, long newValue)
                {
                    ItemCount_OnValueChanged(captured, oldValue, newValue);
                };

                // Stock 3.1 ComboBoxBase consumes wheel input only while Shift is
                // held. Forward ordinary wheel input from every visible combo-box
                // surface to the list, while leaving Shift+wheel to the stock
                // quantity adjustment behavior.
                WireComboListScroll(rowView.Count.GetChildById("directvalue"));
                WireComboListScroll(rowView.Count.GetChildById("back"));
                WireComboListScroll(rowView.Count.GetChildById("forward"));
            }
        }
        QuickStackUiDiagnostics.Write("init itemSlots=" + itemRows.Length +
            " toggles=" + foundToggles + " clickables=" + foundClickables);

        for (int i = 0; i < targetRows.Length; i++)
        {
            targetRows[i] = GetPreviewRow("targetRow" + i.ToString("00"));
            PreviewRowView rowView = targetRows[i];
            if (rowView == null || rowView.Select == null) continue;
            int captured = i;
            rowView.Select.OnPress += delegate(XUiController sender, int mouseButton)
            {
                QuickStackUiDiagnostics.Write("target press slot=" + captured +
                    " dataIndex=" + GetTargetDataIndex(captured) +
                    " mouseButton=" + mouseButton +
                    " sender=" + DescribeController(sender));
                TargetRow_OnPressed(captured);
            };
            rowView.Select.OnHover += delegate(XUiController sender, bool isOver)
            {
                if (isOver) QuickStackUiDiagnostics.Write("target hover slot=" + captured +
                    " dataIndex=" + GetTargetDataIndex(captured) +
                    " sender=" + DescribeController(sender));
            };
            WireScroll(rowView.Root, TargetList_OnScroll);
            WireScroll(rowView.Select, TargetList_OnScroll);
            WireScroll(rowView.Icon != null ? rowView.Icon.Controller : null, TargetList_OnScroll);
            WireScroll(rowView.Name != null ? rowView.Name.Controller : null, TargetList_OnScroll);
            WireScroll(rowView.Distance != null ? rowView.Distance.Controller : null, TargetList_OnScroll);
        }

        // Rect containers are not hit-testable in 3.1. The transparent capture
        // buttons sit behind every row and receive wheel input only where no
        // higher-depth row/control widget is under the pointer. This makes the
        // entire visible pane scrollable, including blank space below rows.
        WireScroll(itemPaneScrollCapture, ItemList_OnScroll);
        WireScroll(targetPaneScrollCapture, TargetList_OnScroll);

        WireLogicalScrollBars();

        string[] ids =
        {
            "btnDeposit", "btnOwned", "btnRestock", "btnOutputs",
            "btnPushVehicle", "btnPullVehicle", "btnPushDrone", "btnPullDrone"
        };
        for (int i = 0; i < ids.Length; i++)
        {
            actionButtons[i] = GetChildById(ids[i]);
            int captured = i;
            if (actionButtons[i] != null)
                actionButtons[i].OnPress += delegate { Select((QuickStackRadialAction)captured); };
        }
        Wire("btnExecute", Execute);
        Wire("btnClose", Close);
    }

    private void Wire(string id, Action callback)
    {
        XUiController c = GetChildById(id);
        if (c != null) c.OnPress += delegate { callback(); };
    }

    private XUiV_Label GetLabel(string id)
    {
        XUiController c = GetChildById(id);
        return c != null ? c.ViewComponent as XUiV_Label : null;
    }

    private PreviewRowView GetPreviewRow(string id)
    {
        XUiController root = GetChildById(id);
        if (root == null) return null;
        return new PreviewRowView
        {
            Root = root,
            Select = root.GetChildById("select"),
            Selection = GetChildView<XUiV_Sprite>(root, "selection"),
            Icon = GetChildView<XUiV_Sprite>(root, "icon"),
            Include = GetToggle(root, "include"),
            Name = GetChildView<XUiV_Label>(root, "name"),
            Count = GetComboBoxInt(root, "count"),
            CountDisplay = GetChildView<XUiV_Label>(root, "countDisplay"),
            Secondary = GetChildView<XUiV_Label>(root, "secondary"),
            Distance = GetChildView<XUiV_Label>(root, "distance")
        };
    }

    private static T GetChildView<T>(XUiController root, string id) where T : XUiView
    {
        if (root == null) return null;
        XUiController child = root.GetChildById(id);
        return child != null ? child.ViewComponent as T : null;
    }

    private static XUiC_ToggleButton GetToggle(XUiController root, string id)
    {
        if (root == null) return null;
        XUiController child = root.GetChildById(id);
        return child != null ? child.GetChildByType<XUiC_ToggleButton>() : null;
    }


    private static XUiC_ComboBoxInt GetComboBoxInt(XUiController root, string id)
    {
        if (root == null) return null;
        XUiController child = root.GetChildById(id);
        if (child == null) return null;
        XUiC_ComboBoxInt direct = child as XUiC_ComboBoxInt;
        return direct ?? child.GetChildByType<XUiC_ComboBoxInt>();
    }

    public void Prepare(QuickStackRadialAction initial)
    {
        action = initial;
        selectedTargetId = string.Empty;
        excludedSelections.Clear();
        quantityOverrides.Clear();
        itemScrollIndex = 0;
        targetScrollIndex = 0;
        previewData = new LogisticsPreviewData();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        windowGroup.isEscClosable = false;

        CursorControllerAbs cursor = xui.playerUI.CursorController;
        wasCursorHidden = cursor.GetCursorHidden();
        // Quick Stack is a standalone modal. Never preserve a navigation lock from
        // the inventory/radial that launched it; that stale lock was what left the
        // next radial menu non-interactive until an extra Escape press.
        cursor.Locked = false;
        cursor.SetCursorHidden(false);
        cursor.ResetToCenter();

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null)
        {
            player.SetControllable(false);
            player.ClearMovementInputs();
        }

        Select(action, false);
    }

    public override void OnClose()
    {
        LogisticsPreviewService.Clear();
        CursorControllerAbs cursor = xui.playerUI.CursorController;

        // NGUI can retain the last focused/clicked button after this modal closes.
        // Base GUIWindowManager consumes the first ESC solely to RemoveFocus() when
        // UIInput.selection is still active, which is exactly the extra-ESC symptom.
        UIInput selectedInput = UIInput.selection;
        if (selectedInput != null)
        {
            // RemoveFocus alone can leave NGUI's static selection reference alive in
            // some paths. GUIWindowManager then treats input as active and consumes
            // the next Escape only to clear it. Keep a local reference so it is safe
            // even if RemoveFocus itself clears UIInput.selection.
            selectedInput.RemoveFocus();
            selectedInput.isSelected = false;
        }
        cursor.HoverTarget = null;
        cursor.SetNavigationTarget((XUiView)null);
        cursor.SetNavigationLockView((XUiView)null);
        cursor.SetCursorHidden(wasCursorHidden);
        // Return to an unlocked gameplay cursor state. A lock captured from the
        // launcher window is no longer valid once that modal has been closed.
        cursor.Locked = false;

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null) player.SetControllable(true);
        base.OnClose();

        // OnClose can itself move focus while the modal hierarchy is being removed.
        // Clear any leftover NGUI selection one final time after the base close.
        if (UIInput.selection != null) UIInput.selection.isSelected = false;
        cursor.HoverTarget = null;
        cursor.ResetNavigationTarget();

        // Rebuild the active input stack after the standalone modal closes. This
        // prevents a stale backpack/Quick Stack navigation/action state from
        // swallowing radial hover until the player presses Escape again.
        xui.playerUI.windowManager.ResetActionSets();
    }

    private bool pagingMode;
    public override void Update(float dt)
    {
        base.Update(dt);
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (pagingMode != pagingNow) { pagingMode = pagingNow; itemScrollDragging = targetScrollDragging = false; }
        if (pagingNow)
        {
            int oldItem = itemScrollIndex, oldTarget = targetScrollIndex;
            ClampScrollIndexes();
            if (oldItem != itemScrollIndex) RenderItemRows();
            if (oldTarget != targetScrollIndex) RenderTargetRows();
        }
        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null) player.SetControllable(false);

        if (QuickStackUiDiagnostics.Enabled && Input.GetMouseButtonDown(0))
        {
            CursorControllerAbs cursor = xui.playerUI.CursorController;
            QuickStackUiDiagnostics.Write("mouse down action=" + action +
                " selectedTarget='" + selectedTargetId + "' itemOffset=" + itemScrollIndex +
                " targetOffset=" + targetScrollIndex +
                " hover=" + DescribeView(cursor != null ? cursor.HoverTarget : null) +
                " current=" + DescribeView(cursor != null ? cursor.CurrentTarget : null) +
                " screen=" + (cursor != null ? cursor.GetScreenPosition().ToString() : "<no cursor>"));
        }

        if (XUiUtils.HotkeysAllowedFor(viewComponent) &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased ||
             xui.playerUI.playerInput.GUIActions.Cancel.WasReleased))
        {
            Close();
        }
    }

    private void Select(QuickStackRadialAction value)
    {
        Select(value, true);
    }

    private void Select(QuickStackRadialAction value, bool resetTargetWhenActionChanges)
    {
        if (resetTargetWhenActionChanges && value != action)
        {
            selectedTargetId = string.Empty;
            excludedSelections.Clear();
            quantityOverrides.Clear();
            itemScrollIndex = 0;
            targetScrollIndex = 0;
        }
        action = value;

        for (int i = 0; i < actionButtons.Length; i++)
        {
            XUiV_Button button = actionButtons[i] != null
                ? actionButtons[i].ViewComponent as XUiV_Button
                : null;
            if (button != null) button.Selected = i == (int)value;
        }

        Set(title, Localization.Get(TitleKey(value)));
        Set(description, Localization.Get(DescKey(value)));
        Set(itemHeader, Localization.Get(ItemHeaderKey(value)));
        Set(targetHeader, Localization.Get(TargetHeaderKey(value)));
        Set(targetHint, Localization.Get(TargetHintKey(value)));

        // On the first open there is no preview to preserve, so clear the dormant
        // slots. During an in-window action switch, keep the existing permanent
        // controls alive until the replacement preview arrives instead of cycling
        // every nested XUi control through root visibility.
        bool noPriorPreview = previewData == null ||
            ((previewData.ItemRows == null || previewData.ItemRows.Count == 0) &&
             (previewData.TargetRows == null || previewData.TargetRows.Count == 0));
        if (noPriorPreview)
        {
            HideRows(itemRows);
            HideRows(targetRows);
        }

        Set(itemEmpty, Localization.Get("xuiRebirthQuickStackPreviewLoading"));
        Set(targetEmpty, Localization.Get("xuiRebirthLogisticsCalculating"));
        SetVisible(itemEmpty, true);
        SetVisible(targetEmpty, true);
        LogisticsPreviewService.Request(value, selectedTargetId, Receive);
    }

    private void Receive(QuickStackRadialAction value, LogisticsPreviewData data)
    {
        if (value != action) return;
        previewData = data ?? new LogisticsPreviewData();
        selectedTargetId = previewData.SelectedTargetId ?? string.Empty;
        QuickStackUiDiagnostics.Write("receive action=" + value +
            " selectedTarget='" + selectedTargetId + "'" +
            " itemRows=" + (previewData.ItemRows != null ? previewData.ItemRows.Count : 0) +
            " targetRows=" + (previewData.TargetRows != null ? previewData.TargetRows.Count : 0));
        ClampScrollIndexes();
        Render();
    }

    private void Render()
    {
        RenderItemRows();
        RenderTargetRows();
    }

    private void RenderItemRows()
    {
        // These are permanent virtual row slots. Do not hide/reopen every slot when
        // the logical offset changes: nested ComboBoxInt/ToggleButton controls can
        // lose their child visibility state when their root is repeatedly toggled.
        List<LogisticsItemPreviewRow> rows = previewData != null ? previewData.ItemRows : null;
        int total = rows != null ? rows.Count : 0;
        itemScrollIndex = ClampScrollIndex(itemScrollIndex, total, itemRows.Length);
        int visibleCount = rows != null ? Math.Min(itemRows.Length, Math.Max(0, total - itemScrollIndex)) : 0;
        QuickStackUiDiagnostics.Write("render items total=" + total + " offset=" + itemScrollIndex +
            " visible=" + visibleCount + " excluded=" + excludedSelections.Count);
        suppressSelectionEvents = true;
        suppressQuantityEvents = true;
        try
        {
            for (int slot = 0; slot < visibleCount; slot++)
            {
                int dataIndex = itemScrollIndex + slot;
                PreviewRowView view = itemRows[slot];
                LogisticsItemPreviewRow row = rows[dataIndex];
                if (view == null || row == null) continue;
                ItemClass item = ItemClass.GetForId(row.ItemType);
                string itemName = item != null ? item.GetLocalizedItemName() : row.ItemType.ToString();
                string sourceName = (row.SourceName ?? string.Empty).Trim();
                if (sourceName.Length == 0)
                {
                    string secondary = (row.Secondary ?? string.Empty).Trim();
                    if (secondary.StartsWith("Source: ", StringComparison.OrdinalIgnoreCase))
                        sourceName = secondary.Substring(8).Trim();
                }
                Set(view.Name, sourceName.Length > 0 ? itemName + " (" + FormatItemSourceName(sourceName) + ")" : itemName);
                Set(view.Secondary, string.Empty);
                if (view.Secondary != null) view.Secondary.IsVisible = false;

                bool selectable = row.Selectable && !string.IsNullOrEmpty(row.SelectionKey);
                int displayCount = row.Count;
                if (selectable)
                {
                    int requested;
                    if (quantityOverrides.TryGetValue(row.SelectionKey, out requested))
                    {
                        displayCount = Mathf.Clamp(requested, 0, Math.Max(1, row.Count));
                        if (displayCount == row.Count) quantityOverrides.Remove(row.SelectionKey);
                        else quantityOverrides[row.SelectionKey] = displayCount;
                    }
                }
                SetCount(view.Count, view.CountDisplay, displayCount, selectable, row.Count);

                if (view.Include != null)
                {
                    if (view.Include.ViewComponent != null) view.Include.ViewComponent.IsVisible = selectable;
                    view.Include.Value = selectable && !excludedSelections.Contains(row.SelectionKey);
                }

                SetLocalX(view.Icon, selectable ? 60f : 24f);
                SetLocalX(view.Name, selectable ? 84f : 48f);

                if (view.Icon != null && item != null)
                {
                    view.Icon.UIAtlas = "ItemIconAtlas";
                    view.Icon.SetSpriteImmediately(item.GetIconName());
                    view.Icon.SetColorImmediately(item.GetIconTint());
                }
                view.Root.ViewComponent.IsVisible = true;

                QuickStackUiDiagnostics.Write("item slot=" + slot + " dataIndex=" + dataIndex +
                    " itemType=" + row.ItemType + " count=" + row.Count +
                    " selectable=" + selectable + " key='" + (row.SelectionKey ?? string.Empty) + "'");
                ValidateItemControlState(slot, dataIndex, row, view, selectable);
                TraceItemControlState("render", slot, dataIndex, row, view);
            }
        }
        finally
        {
            suppressQuantityEvents = false;
            suppressSelectionEvents = false;
        }

        for (int slot = visibleCount; slot < itemRows.Length; slot++)
            if (itemRows[slot] != null && itemRows[slot].Root != null && itemRows[slot].Root.ViewComponent != null)
                itemRows[slot].Root.ViewComponent.IsVisible = false;

        UpdateLogicalScrollBar(itemScrollTrackInput, itemScrollThumb,
            itemScrollIndex, total, itemRows.Length, ItemScrollTrackHeight);
        bool empty = total == 0;
        SetVisible(itemEmpty, empty);
        if (empty) Set(itemEmpty, EmptyItemText());
    }

    private void ItemSelection_OnValueChanged(int rowIndex, bool selected)
    {
        int dataIndex = GetItemDataIndex(rowIndex);
        if (suppressSelectionEvents || previewData == null || previewData.ItemRows == null ||
            dataIndex < 0 || dataIndex >= previewData.ItemRows.Count) return;
        LogisticsItemPreviewRow row = previewData.ItemRows[dataIndex];
        if (row == null || !row.Selectable || string.IsNullOrEmpty(row.SelectionKey)) return;

        if (selected)
            excludedSelections.Remove(row.SelectionKey);
        else
            excludedSelections.Add(row.SelectionKey);

        QuickStackUiDiagnostics.Write("checkbox changed slot=" + rowIndex + " dataIndex=" + dataIndex +
            " selected=" + selected + " key='" + row.SelectionKey +
            "' excludedCount=" + excludedSelections.Count);
    }

    private void ItemCount_OnValueChanged(int rowIndex, long oldValue, long newValue)
    {
        int dataIndex = GetItemDataIndex(rowIndex);
        if (suppressQuantityEvents || previewData == null || previewData.ItemRows == null ||
            dataIndex < 0 || dataIndex >= previewData.ItemRows.Count) return;

        LogisticsItemPreviewRow row = previewData.ItemRows[dataIndex];
        if (row == null || !row.Selectable || string.IsNullOrEmpty(row.SelectionKey) || row.Count <= 0) return;

        int value = Mathf.Clamp((int)newValue, 0, row.Count);
        if (value == row.Count)
            quantityOverrides.Remove(row.SelectionKey);
        else
            quantityOverrides[row.SelectionKey] = value;

        // ComboBoxInt owns its display and provides mouse arrows, wheel adjustment,
        // direct bar clicks and drag-to-value. Keep the plain xN label hidden while
        // the interactive quantity control is available.
        PreviewRowView view = rowIndex < itemRows.Length ? itemRows[rowIndex] : null;
        if (view != null && view.CountDisplay != null)
            view.CountDisplay.IsVisible = false;
    }

    private void CommitItemCount(int rowIndex, bool normalizeValue)
    {
        int dataIndex = GetItemDataIndex(rowIndex);
        if (previewData == null || previewData.ItemRows == null ||
            dataIndex < 0 || dataIndex >= previewData.ItemRows.Count) return;

        LogisticsItemPreviewRow row = previewData.ItemRows[dataIndex];
        PreviewRowView view = rowIndex >= 0 && rowIndex < itemRows.Length ? itemRows[rowIndex] : null;
        if (row == null || view == null || view.Count == null || !row.Selectable ||
            string.IsNullOrEmpty(row.SelectionKey) || row.Count <= 0) return;

        int value = Mathf.Clamp((int)view.Count.Value, 0, row.Count);
        if (value == row.Count)
            quantityOverrides.Remove(row.SelectionKey);
        else
            quantityOverrides[row.SelectionKey] = value;

        if (normalizeValue && view.Count.Value != value)
        {
            suppressQuantityEvents = true;
            try { view.Count.Value = value; }
            finally { suppressQuantityEvents = false; }
        }
    }

    private void CommitVisibleItemCounts()
    {
        if (previewData == null || previewData.ItemRows == null) return;
        int count = Math.Min(itemRows.Length, Math.Max(0, previewData.ItemRows.Count - itemScrollIndex));
        for (int i = 0; i < count; i++) CommitItemCount(i, true);
    }

    private int EffectiveTransferCount(LogisticsItemPreviewRow row)
    {
        if (row == null || row.Count <= 0) return 0;
        int value;
        if (!string.IsNullOrEmpty(row.SelectionKey) &&
            quantityOverrides.TryGetValue(row.SelectionKey, out value))
            return Mathf.Clamp(value, 0, row.Count);
        return row.Count;
    }

    private string SerializeSelectedSourceKeys()
    {
        if (previewData == null || previewData.ItemRows == null) return string.Empty;
        List<string> selected = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < previewData.ItemRows.Count; i++)
        {
            LogisticsItemPreviewRow row = previewData.ItemRows[i];
            if (row == null || !row.Selectable || string.IsNullOrEmpty(row.SelectionKey) ||
                excludedSelections.Contains(row.SelectionKey) || !seen.Add(row.SelectionKey)) continue;

            int count = EffectiveTransferCount(row);
            if (count <= 0) continue;
            selected.Add(row.SelectionKey + "\u001d" + count);
        }
        return string.Join("\u001f", selected.ToArray());
    }

    private void ClearExecutedQuantityOverrides()
    {
        if (previewData == null || previewData.ItemRows == null) return;
        for (int i = 0; i < previewData.ItemRows.Count; i++)
        {
            LogisticsItemPreviewRow row = previewData.ItemRows[i];
            if (row == null || string.IsNullOrEmpty(row.SelectionKey) ||
                excludedSelections.Contains(row.SelectionKey)) continue;
            quantityOverrides.Remove(row.SelectionKey);
        }
    }

    private static string DescribeController(XUiController controller)
    {
        if (controller == null) return "<null>";
        return controller.GetType().Name + "/" + DescribeView(controller.ViewComponent);
    }

    private static string DescribeView(XUiView view)
    {
        if (view == null) return "<null>";
        string objectName = "<no object>";
        try
        {
            if (view.UiTransform != null && view.UiTransform.gameObject != null)
                objectName = view.UiTransform.gameObject.name;
        }
        catch { }
        return view.GetType().Name + "/" + objectName;
    }

    public static void DumpDebugStateLocal()
    {
        try
        {
            EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
                ? GameManager.Instance.World.GetPrimaryPlayer()
                : null;
            if (player == null || player.PlayerUI == null || player.PlayerUI.xui == null)
            {
                Log.Out("[QuickStackUI] dump: no local player/XUi");
                return;
            }

            XUi xui = player.PlayerUI.xui;
            XUiController group = xui.FindWindowGroupByName(QuickStackRadialUiService.WindowGroupName);
            XUiC_RebirthLogistics controller = group != null ? group.GetChildByType<XUiC_RebirthLogistics>() : null;
            Log.Out("[QuickStackUI] dump windowOpen=" +
                player.PlayerUI.windowManager.IsWindowOpen(QuickStackRadialUiService.WindowGroupName) +
                " group=" + (group != null) + " controller=" + (controller != null));
            if (controller == null) return;

            CursorControllerAbs cursor = player.PlayerUI.CursorController;
            Log.Out("[QuickStackUI] dump action=" + controller.action +
                " selectedTarget='" + controller.selectedTargetId + "' itemOffset=" + controller.itemScrollIndex +
                " targetOffset=" + controller.targetScrollIndex +
                " itemTotal=" + (controller.previewData != null && controller.previewData.ItemRows != null ? controller.previewData.ItemRows.Count : 0) +
                " targetTotal=" + (controller.previewData != null && controller.previewData.TargetRows != null ? controller.previewData.TargetRows.Count : 0));
            Log.Out("[QuickStackUI] dump cursor hidden=" + (cursor != null && cursor.GetCursorHidden()) +
                " locked=" + (cursor != null && cursor.Locked) +
                " hover=" + DescribeView(cursor != null ? cursor.HoverTarget : null) +
                " current=" + DescribeView(cursor != null ? cursor.CurrentTarget : null));
            Log.Out("[QuickStackUI] dump scrollbars itemTrack=" +
                (controller.itemScrollTrackInput != null && controller.itemScrollTrackInput.ViewComponent != null && controller.itemScrollTrackInput.ViewComponent.IsVisible) +
                " itemThumb=" + (controller.itemScrollThumb != null && controller.itemScrollThumb.IsVisible) +
                " targetTrack=" + (controller.targetScrollTrackInput != null && controller.targetScrollTrackInput.ViewComponent != null && controller.targetScrollTrackInput.ViewComponent.IsVisible) +
                " targetThumb=" + (controller.targetScrollThumb != null && controller.targetScrollThumb.IsVisible));

            for (int slot = 0; slot < controller.itemRows.Length; slot++)
            {
                PreviewRowView row = controller.itemRows[slot];
                if (row == null) continue;
                int dataIndex = controller.GetItemDataIndex(slot);
                XUiController clickable = row.Include != null ? row.Include.GetChildById("clickable") : null;
                Log.Out("[QuickStackUI] item slot=" + slot + " dataIndex=" + dataIndex +
                    " rootVisible=" + (row.Root != null && row.Root.ViewComponent != null && row.Root.ViewComponent.IsVisible) +
                    " include=" + (row.Include != null) +
                    " includeVisible=" + (row.Include != null && row.Include.ViewComponent != null && row.Include.ViewComponent.IsVisible) +
                    " checkboxClickable=" + (clickable != null) +
                    " count=" + (row.Count != null) +
                    " countVisible=" + (row.Count != null && row.Count.ViewComponent != null && row.Count.ViewComponent.IsVisible));
                LogisticsItemPreviewRow dataRow = controller.previewData != null &&
                    controller.previewData.ItemRows != null &&
                    dataIndex >= 0 && dataIndex < controller.previewData.ItemRows.Count
                    ? controller.previewData.ItemRows[dataIndex] : null;
                TraceItemControlState("dump", slot, dataIndex, dataRow, row);
            }

            for (int slot = 0; slot < controller.targetRows.Length; slot++)
            {
                PreviewRowView row = controller.targetRows[slot];
                if (row == null) continue;
                int dataIndex = controller.GetTargetDataIndex(slot);
                XUiV_Button button = row.Select != null ? row.Select.ViewComponent as XUiV_Button : null;
                Log.Out("[QuickStackUI] target slot=" + slot + " dataIndex=" + dataIndex +
                    " rootVisible=" + (row.Root != null && row.Root.ViewComponent != null && row.Root.ViewComponent.IsVisible) +
                    " button=" + (button != null) +
                    " buttonVisible=" + (button != null && button.IsVisible) +
                    " buttonEnabled=" + (button != null && button.Enabled) +
                    " selectionVisible=" + (row.Selection != null && row.Selection.IsVisible) +
                    " name='" + (row.Name != null ? row.Name.Text : string.Empty) + "'");
            }
        }
        catch (Exception ex)
        {
            Log.Error("[QuickStackUI] dump failed: " + ex);
        }
    }

    private static void SetLocalX(XUiView view, float x)
    {
        if (view == null || view.UiTransform == null) return;
        Vector3 p = view.UiTransform.localPosition;
        p.x = x;
        view.UiTransform.localPosition = p;
    }

    private int GetItemDataIndex(int slot)
    {
        return slot >= 0 && slot < itemRows.Length ? itemScrollIndex + slot : -1;
    }

    private int GetTargetDataIndex(int slot)
    {
        return slot >= 0 && slot < targetRows.Length ? targetScrollIndex + slot : -1;
    }

    private static int ClampScrollIndex(int value, int total, int visible)
    {
        int max = Math.Max(0, total - visible);
        return (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(value, 0, max), max, visible, RebirthScrollbarPagingPolicy.Enabled);
    }

    private void ClampScrollIndexes()
    {
        int itemTotal = previewData != null && previewData.ItemRows != null ? previewData.ItemRows.Count : 0;
        int targetTotal = previewData != null && previewData.TargetRows != null ? previewData.TargetRows.Count : 0;
        itemScrollIndex = ClampScrollIndex(itemScrollIndex, itemTotal, itemRows.Length);
        targetScrollIndex = ClampScrollIndex(targetScrollIndex, targetTotal, targetRows.Length);
    }

    private static void WireScroll(XUiController controller, Action<float> callback)
    {
        if (controller == null || callback == null) return;
        if (controller.ViewComponent != null) controller.ViewComponent.EventOnScroll = true;
        controller.OnScroll += delegate(XUiController sender, float delta) { callback(delta); };
    }

    private void WireComboListScroll(XUiController controller)
    {
        if (controller == null) return;
        if (controller.ViewComponent != null) controller.ViewComponent.EventOnScroll = true;
        controller.OnScroll += delegate(XUiController sender, float delta)
        {
            // ComboBoxBase.ScrollEvent uses Shift+wheel for value changes. Normal
            // wheel input should therefore move the containing logical item list.
            if (!InputUtils.ShiftKeyPressed) ItemList_OnScroll(delta);
        };
    }

    private void WireLogicalScrollBars()
    {
        // The whole scrollbar track accepts mouse-wheel input, while the red thumb
        // remains draggable. No runtime-resized page buttons are used; those were
        // producing unstable NGUI geometry during rapid logical rebinding.
        WireScroll(itemScrollTrackInput, ItemList_OnScroll);
        WireScroll(itemScrollThumbControl, ItemList_OnScroll);
        if (itemScrollThumbControl != null)
        {
            if (itemScrollThumbControl.ViewComponent != null) itemScrollThumbControl.ViewComponent.EventOnDrag = true;
            itemScrollThumbControl.OnPress += delegate(XUiController sender, int mouseButton)
            {
                QuickStackUiDiagnostics.Write("item scrollbar thumb press mouseButton=" + mouseButton + " offset=" + itemScrollIndex);
            };
            itemScrollThumbControl.OnDrag += ItemScrollThumb_OnDrag;
        }

        WireScroll(targetScrollTrackInput, TargetList_OnScroll);
        WireScroll(targetScrollThumbControl, TargetList_OnScroll);
        if (targetScrollThumbControl != null)
        {
            if (targetScrollThumbControl.ViewComponent != null) targetScrollThumbControl.ViewComponent.EventOnDrag = true;
            targetScrollThumbControl.OnPress += delegate(XUiController sender, int mouseButton)
            {
                QuickStackUiDiagnostics.Write("target scrollbar thumb press mouseButton=" + mouseButton + " offset=" + targetScrollIndex);
            };
            targetScrollThumbControl.OnDrag += TargetScrollThumb_OnDrag;
        }
    }

    private void ItemList_OnScroll(float delta)
    {
        if (RebirthScrollbarPagingPolicy.Enabled) { if (delta != 0f) SetItemScrollIndex((int)RebirthScrollbarPagingPolicy.Step(itemScrollIndex, Math.Max(0, (previewData?.ItemRows?.Count ?? 0) - itemRows.Length), itemRows.Length, delta > 0f ? -1 : 1), "mouse wheel " + delta); }
        else if (delta > 0f) SetItemScrollIndex(itemScrollIndex - 1, "mouse wheel " + delta);
        else if (delta < 0f) SetItemScrollIndex(itemScrollIndex + 1, "mouse wheel " + delta);
    }

    private void TargetList_OnScroll(float delta)
    {
        if (RebirthScrollbarPagingPolicy.Enabled) { if (delta != 0f) SetTargetScrollIndex((int)RebirthScrollbarPagingPolicy.Step(targetScrollIndex, Math.Max(0, (previewData?.TargetRows?.Count ?? 0) - targetRows.Length), targetRows.Length, delta > 0f ? -1 : 1), "mouse wheel " + delta); }
        else if (delta > 0f) SetTargetScrollIndex(targetScrollIndex - 1, "mouse wheel " + delta);
        else if (delta < 0f) SetTargetScrollIndex(targetScrollIndex + 1, "mouse wheel " + delta);
    }

    private void SetItemScrollIndex(int requested, string source)
    {
        int total = previewData != null && previewData.ItemRows != null ? previewData.ItemRows.Count : 0;
        int old = itemScrollIndex;
        itemScrollIndex = ClampScrollIndex(requested, total, itemRows.Length);
        if (itemScrollIndex == old) return;
        QuickStackUiDiagnostics.Write("item scroll source='" + source + "' old=" + old + " new=" + itemScrollIndex + " total=" + total);
        RenderItemRows();
    }

    private void SetTargetScrollIndex(int requested, string source)
    {
        int total = previewData != null && previewData.TargetRows != null ? previewData.TargetRows.Count : 0;
        int old = targetScrollIndex;
        targetScrollIndex = ClampScrollIndex(requested, total, targetRows.Length);
        if (targetScrollIndex == old) return;
        QuickStackUiDiagnostics.Write("target scroll source='" + source + "' old=" + old + " new=" + targetScrollIndex + " total=" + total);
        RenderTargetRows();
    }

    private void ItemScrollThumb_OnDrag(XUiController sender, EDragType dragType, Vector2 mousePositionDelta)
    {
        int total = previewData != null && previewData.ItemRows != null ? previewData.ItemRows.Count : 0;
        if (total <= itemRows.Length) return;
        if (dragType == EDragType.DragStart)
        {
            itemScrollDragging = true;
            itemScrollDragAccumulatedUiY = 0f;
            itemScrollDragStartOffset = itemScrollIndex;
            QuickStackUiDiagnostics.Write("item scrollbar drag-start offset=" + itemScrollDragStartOffset);
        }
        if (!itemScrollDragging) return;

        int thumbHeight = GetScrollThumbHeight(total, itemRows.Length, ItemScrollTrackHeight);
        int travel = Math.Max(1, ItemScrollTrackHeight - thumbHeight);
        int maxOffset = Math.Max(0, total - itemRows.Length);
        if (dragType != EDragType.DragEnd)
            itemScrollDragAccumulatedUiY += -mousePositionDelta.y;
        int desired = itemScrollDragStartOffset + Mathf.RoundToInt(itemScrollDragAccumulatedUiY * (maxOffset / (float)travel));
        SetItemScrollIndex(desired, "scrollbar drag " + dragType);

        if (dragType == EDragType.DragEnd)
        {
            itemScrollDragging = false;
            QuickStackUiDiagnostics.Write("item scrollbar drag-end uiDelta=" + itemScrollDragAccumulatedUiY + " offset=" + itemScrollIndex);
        }
    }

    private void TargetScrollThumb_OnDrag(XUiController sender, EDragType dragType, Vector2 mousePositionDelta)
    {
        int total = previewData != null && previewData.TargetRows != null ? previewData.TargetRows.Count : 0;
        if (total <= targetRows.Length) return;
        if (dragType == EDragType.DragStart)
        {
            targetScrollDragging = true;
            targetScrollDragAccumulatedUiY = 0f;
            targetScrollDragStartOffset = targetScrollIndex;
            QuickStackUiDiagnostics.Write("target scrollbar drag-start offset=" + targetScrollDragStartOffset);
        }
        if (!targetScrollDragging) return;

        int thumbHeight = GetScrollThumbHeight(total, targetRows.Length, TargetScrollTrackHeight);
        int travel = Math.Max(1, TargetScrollTrackHeight - thumbHeight);
        int maxOffset = Math.Max(0, total - targetRows.Length);
        if (dragType != EDragType.DragEnd)
            targetScrollDragAccumulatedUiY += -mousePositionDelta.y;
        int desired = targetScrollDragStartOffset + Mathf.RoundToInt(targetScrollDragAccumulatedUiY * (maxOffset / (float)travel));
        SetTargetScrollIndex(desired, "scrollbar drag " + dragType);

        if (dragType == EDragType.DragEnd)
        {
            targetScrollDragging = false;
            QuickStackUiDiagnostics.Write("target scrollbar drag-end uiDelta=" + targetScrollDragAccumulatedUiY + " offset=" + targetScrollIndex);
        }
    }

    // XUi drag events already supply a stable per-frame NGUI delta. Using that
    // value directly avoids camera/world projection math, which can produce NaN
    // geometry when a UI transform is temporarily hidden or being rebound.

    private static int GetScrollThumbHeight(int total, int visible, int trackHeight)
    {
        if (total <= 0 || visible <= 0 || total <= visible) return trackHeight;
        return Mathf.Clamp(Mathf.RoundToInt(trackHeight * ((float)visible / total)), 28, trackHeight);
    }

    private static void SetViewBounds(XUiView view, int x, int y, int width, int height, bool visible)
    {
        if (view == null) return;
        view.IsVisible = visible;
        if (!visible) return;
        view.Size = new Vector2i(width, Math.Max(1, height));
        view.Position = new Vector2i(x, y);
        if (view.UiTransform != null)
        {
            Vector3 p = view.UiTransform.localPosition;
            p.x = x;
            p.y = y;
            view.UiTransform.localPosition = p;
        }
    }

    private static void UpdateLogicalScrollBar(XUiController trackInput, XUiV_Button thumb,
        int offset, int total, int visible, int trackHeight)
    {
        bool needed = total > visible && total > 0;
        if (trackInput != null && trackInput.ViewComponent != null)
            trackInput.ViewComponent.IsVisible = needed;
        if (thumb != null) thumb.IsVisible = needed;
        if (!needed) return;

        int thumbHeight = GetScrollThumbHeight(total, visible, trackHeight);
        int maxOffset = Math.Max(1, total - visible);
        int travel = Math.Max(0, trackHeight - thumbHeight);
        int y = Mathf.RoundToInt(travel * ((float)offset / maxOffset));

        // Keep the runtime geometry identical to the shared custom-window scrollbar:
        // 8 px track with a 6 px thumb inset by 1 px. Do not widen the thumb here;
        // XML sizing is otherwise silently defeated after every refresh.
        SetViewBounds(thumb, 1, -y, 6, thumbHeight, true);
    }

    private bool ActionUsesSelectableItems()
    {
        // Every Quick Stack action that presents item rows must allow the player to
        // include/exclude each row and cap its quantity, including all retrievals
        // (Restock, Workstation Outputs, Vehicle Pull and Companion Pull).
        return action == QuickStackRadialAction.Deposit ||
               action == QuickStackRadialAction.DepositOwned ||
               action == QuickStackRadialAction.Restock ||
               action == QuickStackRadialAction.CollectWorkstationOutputs ||
               action == QuickStackRadialAction.PushVehicle ||
               action == QuickStackRadialAction.PullVehicle ||
               action == QuickStackRadialAction.PushDrone ||
               action == QuickStackRadialAction.PullDrone;
    }

    private bool ActionUsesSelectableTarget()
    {
        return action == QuickStackRadialAction.CollectWorkstationOutputs ||
               action == QuickStackRadialAction.PushVehicle ||
               action == QuickStackRadialAction.PushDrone;
    }

    private void TargetRow_OnPressed(int rowSlot)
    {
        int dataIndex = GetTargetDataIndex(rowSlot);
        if (!ActionUsesSelectableTarget() || previewData == null || previewData.TargetRows == null ||
            dataIndex < 0 || dataIndex >= previewData.TargetRows.Count) return;

        LogisticsTargetPreviewRow row = previewData.TargetRows[dataIndex];
        if (row == null || string.IsNullOrEmpty(row.Id)) return;
        if (string.Equals(selectedTargetId, row.Id, StringComparison.Ordinal)) return;

        string old = selectedTargetId;
        selectedTargetId = row.Id;
        for (int i = 0; i < previewData.TargetRows.Count; i++)
        {
            LogisticsTargetPreviewRow current = previewData.TargetRows[i];
            if (current != null) current.Selected = string.Equals(current.Id, selectedTargetId, StringComparison.Ordinal);
        }

        QuickStackUiDiagnostics.Write("target selected slot=" + rowSlot + " dataIndex=" + dataIndex +
            " old='" + old + "' new='" + selectedTargetId + "' name='" + (row.Name ?? string.Empty) + "'");

        RenderTargetRows();
        itemScrollIndex = 0;
        // Keep the permanent item-row controls alive while the newly selected
        // workstation/vehicle/companion preview is requested. Hiding the roots here
        // forces nested ToggleButton/ComboBoxInt controls through a hide/reopen cycle
        // and was one of the paths associated with disappearing controls.
        Set(itemEmpty, Localization.Get("xuiRebirthQuickStackPreviewLoading"));
        SetVisible(itemEmpty, true);
        LogisticsPreviewService.Request(action, selectedTargetId, Receive);
    }

    private void RenderTargetRows()
    {
        // Target rows are also permanent virtual slots. Rebind them in place so
        // their hit surfaces are never destroyed/recreated by logical scrolling.
        List<LogisticsTargetPreviewRow> rows = previewData != null ? previewData.TargetRows : null;
        int total = rows != null ? rows.Count : 0;
        targetScrollIndex = ClampScrollIndex(targetScrollIndex, total, targetRows.Length);
        int visibleCount = rows != null ? Math.Min(targetRows.Length, Math.Max(0, total - targetScrollIndex)) : 0;
        bool canSelectTarget = ActionUsesSelectableTarget();

        for (int slot = 0; slot < visibleCount; slot++)
        {
            int dataIndex = targetScrollIndex + slot;
            PreviewRowView view = targetRows[slot];
            LogisticsTargetPreviewRow row = rows[dataIndex];
            if (view == null || row == null) continue;

            if (view.Select != null && view.Select.ViewComponent is XUiV_Button selectButton)
                selectButton.Enabled = canSelectTarget;
            bool selected = canSelectTarget && !string.IsNullOrEmpty(row.Id) &&
                            string.Equals(row.Id, selectedTargetId, StringComparison.Ordinal);
            if (view.Selection != null)
                view.Selection.IsVisible = selected;

            Set(view.Name, row.Name);
            Set(view.Distance, row.Distance);
            if (view.Icon != null)
            {
                string atlas = row.IconAtlas;
                string icon = row.IconName;
                if (string.IsNullOrEmpty(icon)) GetTargetFallbackIcon(out atlas, out icon);
                if (!string.IsNullOrEmpty(icon))
                {
                    view.Icon.IsVisible = true;
                    view.Icon.UIAtlas = string.IsNullOrEmpty(atlas) ? "UIAtlas" : atlas;
                    view.Icon.SetSpriteImmediately(icon);
                    view.Icon.SetColorImmediately(Color.white);
                }
                else view.Icon.IsVisible = false;
            }
            view.Root.ViewComponent.IsVisible = true;

            QuickStackUiDiagnostics.Write("target slot=" + slot + " dataIndex=" + dataIndex +
                " id='" + (row.Id ?? string.Empty) + "' selected=" + selected +
                " selectable=" + canSelectTarget + " name='" + (row.Name ?? string.Empty) + "'");
        }

        for (int slot = visibleCount; slot < targetRows.Length; slot++)
            if (targetRows[slot] != null && targetRows[slot].Root != null && targetRows[slot].Root.ViewComponent != null)
                targetRows[slot].Root.ViewComponent.IsVisible = false;

        UpdateLogicalScrollBar(targetScrollTrackInput, targetScrollThumb,
            targetScrollIndex, total, targetRows.Length, TargetScrollTrackHeight);
        bool empty = total == 0;
        SetVisible(targetEmpty, empty);
        if (empty) Set(targetEmpty, EmptyTargetText());
    }

    private void GetTargetFallbackIcon(out string atlas, out string icon)
    {
        atlas = "RebirthUiIcons";
        switch (action)
        {
            case QuickStackRadialAction.PushVehicle:
            case QuickStackRadialAction.PullVehicle:
                icon = "rb_quickstack_push_vehicle";
                return;
            case QuickStackRadialAction.PushDrone:
            case QuickStackRadialAction.PullDrone:
                icon = "rb_backpack_companions";
                return;
            case QuickStackRadialAction.CollectWorkstationOutputs:
                atlas = "UIAtlas";
                icon = "ui_game_symbol_wrench";
                return;
            default:
                // Static storage rows are expected to carry their actual block/item
                // icon from the server preview. Never replace it with a generic crate.
                atlas = string.Empty;
                icon = string.Empty;
                return;
        }
    }

    private string EmptyItemText()
    {
        switch (action)
        {
            case QuickStackRadialAction.CollectWorkstationOutputs:
                return Localization.Get("xuiRebirthNoWorkstationOutputItems");
            case QuickStackRadialAction.PushVehicle:
            case QuickStackRadialAction.PushDrone:
                return Localization.Get("xuiRebirthNoQualifiedSourceItems");
            case QuickStackRadialAction.PullVehicle:
                return Localization.Get("xuiRebirthNoVehicleItemsToRetrieve");
            case QuickStackRadialAction.PullDrone:
                return Localization.Get("xuiRebirthNoCompanionItemsToRetrieve");
            default:
                return Localization.Get("xuiRebirthQuickStackPreviewNone");
        }
    }

    private string EmptyTargetText()
    {
        switch (action)
        {
            case QuickStackRadialAction.CollectWorkstationOutputs:
                return Localization.Get("xuiRebirthNoNearbyWorkstationOutputs");
            case QuickStackRadialAction.PushVehicle:
            case QuickStackRadialAction.PullVehicle:
                return Localization.Get("xuiRebirthNoNearbyVehicles");
            case QuickStackRadialAction.PushDrone:
            case QuickStackRadialAction.PullDrone:
                return Localization.Get("xuiRebirthNoNearbyCompanions");
            default:
                return Localization.Get("xuiRebirthNoQualifiedNearbyStorage");
        }
    }

    private static void HideRows(PreviewRowView[] rows)
    {
        for (int i = 0; rows != null && i < rows.Length; i++)
            if (rows[i] != null && rows[i].Root != null && rows[i].Root.ViewComponent != null)
                rows[i].Root.ViewComponent.IsVisible = false;
    }

    private static void SetVisible(XUiV_Label label, bool visible)
    {
        if (label != null) label.IsVisible = visible;
    }

    private void Execute()
    {
        bool selectiveItems = ActionUsesSelectableItems();
        if (selectiveItems) CommitVisibleItemCounts();

        string selectedSourceKeys = selectiveItems
            ? SerializeSelectedSourceKeys()
            : string.Empty;

        // A remote client must wait for the authoritative server transfer result before
        // requesting the next preview. Otherwise the immediate preview carries the
        // client's pre-transfer backpack snapshot and can overwrite the server's newly
        // committed backpack state. Host/single-player completes this callback inline.
        LogisticsTransferService.Request(action, selectedTargetId, selectedSourceKeys, delegate
        {
            // A quantity override is a one-transfer cap. Once a selected row is executed,
            // refresh it from the live remaining inventory so the number shown is the new
            // movable amount. Unchecked rows keep both their checkbox state and any custom
            // quantity because they were not part of this transfer.
            if (selectiveItems) ClearExecutedQuantityOverrides();

            // Keep the user's unchecked rows excluded when the preview refreshes after
            // Execute Transfer. Selection state is intentionally scoped to the current
            // Quick Stack session/action and is cleared only when the action changes or
            // the window is prepared/opened again.
            LogisticsPreviewService.Request(action, selectedTargetId, Receive);
        });
    }

    private void Close()
    {
        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }

    private static void Set(XUiV_Label label, string text)
    {
        if (label == null) return;
        // XUiV_Label.Text marks the label dirty and schedules its text refresh.
        // Do not call UpdateData directly: that method is not exposed by the
        // publicized 3.1 assembly used by the RebirthUtils project.
        label.Text = text ?? string.Empty;
    }

    private static void SetCount(XUiC_ComboBoxInt input, XUiV_Label display, int count, bool editable, int maxCount)
    {
        int safeCount = Math.Max(0, count);
        int safeMax = Math.Max(1, maxCount);
        if (input != null)
        {
            // IMPORTANT: ComboBoxInt's stock 3.1 fill binding divides by
            // (Max - Min). The previous 1..1 range therefore generated NaN fill
            // geometry and NGUI "abnormal mesh bounds" warnings. Use 0..Count:
            // this is always a non-zero range for a real item row and also makes
            // zero a valid mouse-selectable "transfer none" amount.
            input.Min = 0L;
            input.Max = (long)safeMax;
            input.Enabled = editable;
            if (editable) input.Value = (long)Mathf.Clamp(safeCount, 0, safeMax);
            if (input.ViewComponent != null)
            {
                // IsVisible already dirties the XUi view/controller in 3.1.
                // Do not access XUiView.IsDirty directly: it is not exposed by
                // the assembly surface used by the RebirthUtils project.
                input.ViewComponent.IsVisible = editable;
            }
        }

        if (display != null)
        {
            // XUiV_Label.Text schedules its own text refresh and IsVisible
            // dirties the view/controller. No direct IsDirty access is needed.
            display.Text = "x" + safeCount;
            display.IsVisible = !editable;
        }
    }

    private static void ValidateItemControlState(
        int slot,
        int dataIndex,
        LogisticsItemPreviewRow data,
        PreviewRowView view,
        bool selectable)
    {
        if (view == null) return;

        bool bad = false;
        string reason = string.Empty;

        if (selectable)
        {
            if (view.Include == null || view.Include.ViewComponent == null || !view.Include.ViewComponent.IsVisible)
            {
                bad = true;
                reason += " include-not-visible;";
            }
            if (view.Count == null || view.Count.ViewComponent == null || !view.Count.ViewComponent.IsVisible)
            {
                bad = true;
                reason += " combo-not-visible;";
            }
        }

        if (view.Count != null && view.Count.Max <= view.Count.Min)
        {
            bad = true;
            reason += " invalid-combo-range=" + view.Count.Min + ".." + view.Count.Max + ";";
        }

        XUiView[] views =
        {
            view.Root != null ? view.Root.ViewComponent : null,
            view.Include != null ? view.Include.ViewComponent : null,
            view.Count != null ? view.Count.ViewComponent : null,
            view.CountDisplay
        };
        for (int i = 0; i < views.Length; i++)
        {
            XUiView current = views[i];
            if (current == null || current.UiTransform == null) continue;
            if (!IsFinite(current.UiTransform.localPosition) || !IsFinite(current.UiTransform.localScale))
            {
                bad = true;
                reason += " nonfinite-transform[" + i + "];";
            }
        }

        if (bad)
        {
            Log.Warning("[QuickStackUI][ANOMALY] slot=" + slot +
                " dataIndex=" + dataIndex +
                " itemType=" + (data != null ? data.ItemType : -1) +
                " sourceCount=" + (data != null ? data.Count : -1) +
                " selectable=" + selectable +
                " key='" + (data != null ? data.SelectionKey ?? string.Empty : string.Empty) + "'" +
                " reason=" + reason +
                " combo={" + DescribeComboState(view.Count) + "}" +
                " include={" + DescribeToggleState(view.Include) + "}" +
                " root={" + DescribeViewState(view.Root != null ? view.Root.ViewComponent : null) + "}");
        }
    }

    private static void TraceItemControlState(
        string phase,
        int slot,
        int dataIndex,
        LogisticsItemPreviewRow data,
        PreviewRowView view)
    {
        if (!QuickStackUiDiagnostics.Enabled || view == null) return;

        XUiController includeClickable = view.Include != null ? view.Include.GetChildById("clickable") : null;
        XUiController directValue = view.Count != null ? view.Count.GetChildById("directvalue") : null;
        XUiController back = view.Count != null ? view.Count.GetChildById("back") : null;
        XUiController forward = view.Count != null ? view.Count.GetChildById("forward") : null;
        XUiController fill = view.Count != null ? view.Count.GetChildById("fill") : null;

        QuickStackUiDiagnostics.Write("rowstate phase=" + phase +
            " slot=" + slot + " dataIndex=" + dataIndex +
            " sourceCount=" + (data != null ? data.Count : -1) +
            " selectable=" + (data != null && data.Selectable) +
            " key='" + (data != null ? data.SelectionKey ?? string.Empty : string.Empty) + "'" +
            " root={" + DescribeViewState(view.Root != null ? view.Root.ViewComponent : null) + "}" +
            " include={" + DescribeToggleState(view.Include) + "}" +
            " includeClick={" + DescribeViewState(includeClickable != null ? includeClickable.ViewComponent : null) + "}" +
            " combo={" + DescribeComboState(view.Count) + "}" +
            " direct={" + DescribeViewState(directValue != null ? directValue.ViewComponent : null) + "}" +
            " back={" + DescribeViewState(back != null ? back.ViewComponent : null) + "}" +
            " forward={" + DescribeViewState(forward != null ? forward.ViewComponent : null) + "}" +
            " fill={" + DescribeViewState(fill != null ? fill.ViewComponent : null) + "}" +
            " display={" + DescribeViewState(view.CountDisplay) + "}");
    }

    private static string DescribeToggleState(XUiC_ToggleButton toggle)
    {
        if (toggle == null) return "<null>";
        return "value=" + toggle.Value +
            ",view=" + DescribeViewState(toggle.ViewComponent);
    }

    private static string DescribeComboState(XUiC_ComboBoxInt combo)
    {
        if (combo == null) return "<null>";
        return "enabled=" + combo.Enabled +
            ",min=" + combo.Min +
            ",max=" + combo.Max +
            ",value=" + combo.Value +
            ",finiteRange=" + (combo.Max > combo.Min) +
            ",view=" + DescribeViewState(combo.ViewComponent);
    }

    private static string DescribeViewState(XUiView view)
    {
        if (view == null) return "<null>";
        Vector3 localPosition = Vector3.zero;
        Vector3 localScale = Vector3.one;
        bool hasTransform = view.UiTransform != null;
        if (hasTransform)
        {
            localPosition = view.UiTransform.localPosition;
            localScale = view.UiTransform.localScale;
        }

        return "type=" + view.GetType().Name +
            ",visible=" + view.IsVisible +
            ",pos=" + view.Position +
            ",size=" + view.Size +
            ",localPos=" + localPosition +
            ",localScale=" + localScale +
            ",finite=" + (!hasTransform || (IsFinite(localPosition) && IsFinite(localScale)));
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // Mobile target presentation uses "Vehicle ([green]Owner[-])". Inside an
    // item/source line we remove that extra level of parentheses so the result is
    // "Item (Vehicle - [green]Owner[-])". Backpack/NPC source names are untouched.
    private static string FormatItemSourceName(string sourceName)
    {
        if (string.IsNullOrEmpty(sourceName)) return string.Empty;
        const string ownerMarker = " ([6F9F67]";
        int marker = sourceName.LastIndexOf(ownerMarker, StringComparison.Ordinal);
        if (marker < 0 || !sourceName.EndsWith("[-])", StringComparison.Ordinal))
            return sourceName;

        string baseName = sourceName.Substring(0, marker);
        string coloredOwner = sourceName.Substring(marker + 2, sourceName.Length - (marker + 2) - 1);
        return baseName + " - " + coloredOwner;
    }

    private static string TitleKey(QuickStackRadialAction a)
    {
        switch (a)
        {
            case QuickStackRadialAction.DepositOwned: return "xuiRebirthQuickStackDepositOwnedTitle";
            case QuickStackRadialAction.Restock: return "xuiRebirthQuickStackRestockTitle";
            case QuickStackRadialAction.CollectWorkstationOutputs: return "xuiRebirthQuickStackCollectOutputsTitle";
            case QuickStackRadialAction.PushVehicle: return "xuiRebirthPushVehicle";
            case QuickStackRadialAction.PullVehicle: return "xuiRebirthPullVehicle";
            case QuickStackRadialAction.PushDrone: return "xuiRebirthPushDrone";
            case QuickStackRadialAction.PullDrone: return "xuiRebirthPullDrone";
            default: return "xuiRebirthQuickStackDepositTitle";
        }
    }

    private static string DescKey(QuickStackRadialAction a)
    {
        switch (a)
        {
            case QuickStackRadialAction.DepositOwned: return "xuiRebirthQuickStackDepositOwnedDesc";
            case QuickStackRadialAction.Restock: return "xuiRebirthQuickStackRestockDesc";
            case QuickStackRadialAction.CollectWorkstationOutputs: return "xuiRebirthQuickStackCollectOutputsDesc";
            case QuickStackRadialAction.PushVehicle: return "xuiRebirthPushVehicleDesc";
            case QuickStackRadialAction.PullVehicle: return "xuiRebirthPullVehicleDesc";
            case QuickStackRadialAction.PushDrone: return "xuiRebirthPushDroneDesc";
            case QuickStackRadialAction.PullDrone: return "xuiRebirthPullDroneDesc";
            default: return "xuiRebirthQuickStackDepositDesc";
        }
    }

    private static string ItemHeaderKey(QuickStackRadialAction a)
    {
        switch (a)
        {
            case QuickStackRadialAction.CollectWorkstationOutputs:
            case QuickStackRadialAction.PullVehicle:
            case QuickStackRadialAction.PullDrone:
                return "xuiRebirthItemsToRetrieve";
            case QuickStackRadialAction.PushVehicle:
            case QuickStackRadialAction.PushDrone:
                return "xuiRebirthQualifiedSources";
            default:
                return "xuiRebirthItemsToTransfer";
        }
    }

    private static string TargetHeaderKey(QuickStackRadialAction a)
    {
        switch (a)
        {
            case QuickStackRadialAction.CollectWorkstationOutputs:
                return "xuiRebirthAccessibleWorkstations";
            case QuickStackRadialAction.PushVehicle:
            case QuickStackRadialAction.PullVehicle:
                return "xuiRebirthQualifiedVehicles";
            case QuickStackRadialAction.PushDrone:
            case QuickStackRadialAction.PullDrone:
                return "xuiRebirthQualifiedCompanions";
            default:
                return "xuiRebirthQualifiedNearbyStorage";
        }
    }

    private static string TargetHintKey(QuickStackRadialAction a)
    {
        switch (a)
        {
            case QuickStackRadialAction.CollectWorkstationOutputs:
                return "xuiRebirthWorkstationOutputHint";
            case QuickStackRadialAction.PushVehicle:
            case QuickStackRadialAction.PushDrone:
                return "xuiRebirthSelectTransferDestinationHint";
            case QuickStackRadialAction.PullVehicle:
                return "xuiRebirthPullAllVehiclesHint";
            case QuickStackRadialAction.PullDrone:
                return "xuiRebirthPullAllCompanionsHint";
            default:
                return "xuiRebirthQualifiedStorageHint";
        }
    }
}
