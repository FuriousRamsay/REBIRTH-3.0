using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>Character-local view of the real Bag; never serializes the authored slot pool.</summary>
[Preserve]
public sealed class XUiC_RebirthCharacterBackpack : XUiC_Backpack
{
    private XUiC_RebirthCharacterOverviewList scroll;
    private XUiV_Label selectedName;
    private bool open;
    private bool filtered, lockMode;
    private XUiC_BackpackWindow nativeBackpackWindow;
    private bool EffectiveLockMode => nativeBackpackWindow?.UserLockMode ?? lockMode;
    private XUiView[] cellViews, lockViews;
    private XUiC_ItemStack[] cachedControllers;
    private XUiV_Label capacityLabel;
    internal bool InventoryOpen => open;
    internal int FrameNumber { get; private set; } = -1;
    internal float FrameClock { get; private set; }
    internal bool FrameInteractionBusy { get; private set; }
    internal bool FrameEditorActive { get; private set; }
    private XUiC_ItemActionList actions;
    private XUiController selected;
    private int actionType = -1;
    private string actionSignature;
    private int lastActionRows = -1;
    private XUiController renderedSource;
    private ItemStack renderedStack;
    private int renderedLiquidFingerprint;
    private bool selectionRendered, renderedAssembling;
    private float nextSelectionCheck;
    private XUiV_Label selectedSummary, selectedDescription;
    private XUiV_Sprite selectedIcon;
    private XUiC_ItemActionEntry[] actionViews;
    public XUiController SelectedItem => selected;
    public float ScrollOffset => scroll?.ScrollOffset ?? 0f;
    public void RestoreItem(XUiController item, float offset) { SelectItem(item); scroll?.RestoreScrollOffset(offset); }

    public override bool GetBindingValueInternal(ref string value, string name)
    {
        if (name == "userlockmode") { value = EffectiveLockMode ? "true" : "false"; return true; }
        return base.GetBindingValueInternal(ref value, name);
    }

    public override void Init()
    {
        base.Init();
        nativeBackpackWindow = GetParentByType<XUiC_BackpackWindow>();
        scroll = GetChildByType<XUiC_RebirthCharacterOverviewList>();
        selectedName = GetChildById("characterBagSelected")?.ViewComponent as XUiV_Label;
        actions = GetChildByType<XUiC_ItemActionList>();
        selectedSummary = GetChildById("characterBagSummary")?.ViewComponent as XUiV_Label;
        selectedDescription = GetChildById("characterBagDescription")?.ViewComponent as XUiV_Label;
        selectedIcon = GetChildById("characterBagSelectedIcon")?.ViewComponent as XUiV_Sprite;
        actionViews = actions?.GetChildrenByType<XUiC_ItemActionEntry>() ?? new XUiC_ItemActionEntry[0];
        GetChildById("characterBagFilter").OnPress += Filter;
        GetChildById("characterBagSort").OnPress += Sort;
        GetChildById("characterBagLock").OnPress += Lock;
        capacityLabel = GetChildById("characterBagCapacity")?.ViewComponent as XUiV_Label;
        CacheSlotViews();
    }

    private void CacheSlotViews()
    {
        if (itemControllers == null || ReferenceEquals(cachedControllers, itemControllers)) return;
        cachedControllers = itemControllers;
        cellViews = new XUiView[itemControllers.Length];
        lockViews = new XUiView[itemControllers.Length];
        for (int i = 0; i < itemControllers.Length; ++i)
        {
            // Lookup once for this authored controller pool, not a full-tree name search
            // for every physical slot on every inventory notification.
            cellViews[i] = GetChildById("characterBagCell" + i)?.ViewComponent;
            lockViews[i] = itemControllers[i]?.GetChildById("rectSlotLock")?.ViewComponent;
        }
    }

    private void BeginInventoryFrame()
    {
        FrameNumber = Time.frameCount;
        FrameClock = Time.realtimeSinceStartup;
        var cursor = xui?.playerUI?.CursorController;
        FrameInteractionBusy = xui?.DragAndDropWindow == null || !xui.DragAndDropWindow.IsEmpty() ||
            (cursor != null && (cursor.GetMouseButton(UICamera.MouseButton.LeftButton) ||
                cursor.GetMouseButton(UICamera.MouseButton.RightButton)));
        var manager = xui?.playerUI?.windowManager;
        FrameEditorActive = manager != null && (manager.IsWindowOpen("assemble") || manager.IsWindowOpen("cosmetics"));
    }

    public override void OnOpen()
    {
        itemControllers = GetChildrenByType<XUiC_ItemStack>();
        nativeBackpackWindow = nativeBackpackWindow ?? GetParentByType<XUiC_BackpackWindow>();
        CacheSlotViews();
        FrameNumber = -1;
        open = true;
        selected = null;
        actionType = -1;
        selectionRendered = false; nextSelectionCheck = 0f;
        base.OnOpen();
        RefreshSelection();
    }

    public override void OnClose()
    {
        PersistLocks();
        lockMode = false;
        actions?.SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.None, selected);
        selected = null;
        open = false;
        FrameNumber = -1;
        base.OnClose();
    }

    public override void Update(float dt)
    {
        if (!open || ViewComponent?.UiTransform == null || !ViewComponent.UiTransform.gameObject.activeInHierarchy) return;
        BeginInventoryFrame();
        base.Update(dt);
        // OverviewList can commit its smooth scroll position after updating its children.
        // Reconcile once more before rendering, so a newly exposed row is ready this frame.
        for (int i = 0; itemControllers != null && i < itemControllers.Length; ++i)
            (itemControllers[i] as XUiC_RebirthCharacterBackpackSlot)?.FinishCreativeViewport();
        if (EffectiveLockMode) PersistLocks();
        if (Time.realtimeSinceStartup >= nextSelectionCheck)
        {
            nextSelectionCheck = Time.realtimeSinceStartup + .1f;
            RefreshSelection();
        }
    }

    public override void SetStacks(ItemStack[] stacks)
    {
        if (stacks == null) return;
        if (itemControllers == null || itemControllers.Length == 0)
            itemControllers = GetChildrenByType<XUiC_ItemStack>();
        CacheSlotViews();
        bool effectiveLockMode = EffectiveLockMode;
        if (effectiveLockMode) PersistLocks();
        // Keep the native inventory synchronization and callbacks on EVERY notification.
        // Real inventory dirtiness is never coalesced like read-only catalogue dirtiness.
        base.SetStacks(stacks);
        int physical = Math.Min(stacks?.Length ?? 0, itemControllers.Length);
        int visible = 0;
        for (int i = 0; i < itemControllers.Length; i++)
        {
            var slot = itemControllers[i];
            if (slot == null) continue;
            // Native Modify requires InfoWindow. The slot override suppresses only ordinary inspection.
            if (slot.ViewComponent.IsVisible != (i < physical)) slot.ViewComponent.IsVisible = i < physical;
            bool show = i < physical && (!filtered || IsWearable(stacks[i]));
            var wrapper = cellViews[i];
            if (wrapper != null)
            {
                if (wrapper.IsVisible != show) wrapper.IsVisible = show;
                if (wrapper.UiTransform != null && wrapper.UiTransform.gameObject.activeSelf != show)
                    wrapper.UiTransform.gameObject.SetActive(show);
                if (show)
                {
                    // Physical parent row is unchanged; filtering compacts only the visual positions.
                    var position = new Vector2i(visible % 5 * 76, (i / 5 - visible / 5) * 76);
                    if (wrapper.Position != position)
                    { wrapper.Position = position; wrapper.TryUpdatePosition(); }
                }
            }
            if (show) visible++;
            var overlay = lockViews[i];
            bool showLock = effectiveLockMode && show;
            if (overlay != null && overlay.IsVisible != showLock) overlay.IsVisible = showLock;
        }
        RebirthCraftingInventoryBridge.ApplyLockedSlots(xui, itemControllers, physical);
        // Retain the scroll owner's complete range/bar refresh contract on each sync.
        scroll?.SetItemCount((visible + 4) / 5, filtered ? "No wearable items" : "");
        if (capacityLabel != null)
        {
            string text = RebirthCraftingInventoryBridge.GetUsedSlotCount(xui) + "/" + physical
                + " • " + RebirthCraftingInventoryBridge.GetEncumberedUsedSlotCount(xui) + " ENCUMBERED";
            if (capacityLabel.Text != text) capacityLabel.Text = text;
        }
        RefreshSelection();
    }

    public override void HandleSlotChangedEvent(int slotNumber, ItemStack stack)
    {
        var bag = RebirthCraftingInventoryBridge.GetBag(xui);
        if (bag == null || slotNumber < 0 || slotNumber >= bag.ItemGrid.items.Length) return;
        // Native drag/split/swap emits one changed slot. Writing the entire 100-cell
        // presentation would incorrectly resize smaller physical backpacks.
        bag.SetSlot(slotNumber, stack?.Clone() ?? ItemStack.Empty.Clone());
    }

    public void SelectSlot(int index)
    {
        SelectItem(index >= 0 && index < itemControllers.Length ? itemControllers[index] : null);
        RefreshSelection();
    }

    public void SelectItem(XUiController slot)
    {
        if (selected != slot) (selected as XUiC_SelectableEntry)?.Selected(false);
        if (selected != slot) { actionType = -1; selectionRendered = false; }
        selected = slot;
        (selected as XUiC_SelectableEntry)?.Selected(true);
        RefreshSelection();
    }

    private static bool IsWearable(ItemStack stack) => stack != null && !stack.IsEmpty()
        && (stack.itemValue.ItemClass.CanEquip() || IsSurvivorGear(stack));

    private bool CanOperate(int button) => open && (button == 0 || button == -1)
        && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput() && xui.DragAndDropWindow.IsEmpty();

    private void Filter(XUiController sender, int button)
    {
        if (!CanOperate(button)) return;
        filtered = !filtered;
        sender.ViewComponent.ToolTip = filtered ? "Filter: wearables only" : "Filter: all items";
        SelectItem(null);
        SetStacks(GetSlots());
        scroll?.ResetPosition();
        (sender.ViewComponent as XUiV_Sprite)?.SetColorImmediately(filtered ? new Color32(220,195,120,255) : new Color32(255,255,255,255));
    }

    private void Sort(XUiController sender, int button)
    {
        if (!CanOperate(button)) return;
        PersistLocks();
        SelectItem(null);
        xui.PlayerInventory.SortStacks();
        SetStacks(GetSlots());
    }

    private void Lock(XUiController sender, int button)
    {
        if (!CanOperate(button)) return;
        PersistLocks();
        lockMode = !EffectiveLockMode;
        if (nativeBackpackWindow != null) nativeBackpackWindow.UserLockMode = lockMode;
        RefreshBindings();
        foreach (var item in itemControllers) { item.RefreshBindings(); item.SetAllChildrenDirty(); }
        sender.ViewComponent.ToolTip = lockMode ? "Lock: select slots to protect from sorting" : "Lock slots";
        SetStacks(GetSlots());
        (sender.ViewComponent as XUiV_Sprite)?.SetColorImmediately(lockMode ? new Color32(220,195,120,255) : new Color32(255,255,255,255));
    }

    private void PersistLocks() => RebirthCraftingInventoryBridge.PersistLockedSlots(xui, itemControllers,
        Math.Min(GetSlots()?.Length ?? 0, itemControllers?.Length ?? 0));

    private static bool IsSurvivorGear(ItemStack stack)
    {
        RebirthTraitSupportProfileDefinition profile;
        return stack?.itemValue?.ItemClass != null &&
            RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(stack.itemValue.ItemClass.GetItemName(), out profile) &&
            profile != null && string.Equals(profile.Kind, "survivor_gear", StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshSelection()
    {
        var stack = selected is XUiC_ItemStack bagSlot ? bagSlot.ItemStack
            : selected is XUiC_EquipmentStack equipment ? equipment.ItemStack : null;
        bool has = stack != null && !stack.IsEmpty();
        bool assembling = xui.AssembleItem.CurrentItem != null;
        int liquidFingerprint = RebirthConsumableItemPresentation.LiquidFingerprint(stack);
        if (liquidFingerprint == renderedLiquidFingerprint && selectionRendered && renderedSource == selected && renderedAssembling == assembling &&
            ((!has && renderedStack == null) || (has && renderedStack != null && renderedStack.Equals(stack)))) return;
        selectionRendered = true; renderedSource = selected; renderedAssembling = assembling;
        renderedStack = has ? stack.Clone() : null;
        renderedLiquidFingerprint = liquidFingerprint;
        if (selectedName != null) selectedName.SetTextImmediately(has ? (selected is XUiC_ItemStack namedSlot ? namedSlot.ItemNameText : stack.itemValue.ItemClass.GetLocalizedItemName()) : "");
        string liquidSummary = has ? RebirthConsumableItemPresentation.LiquidSummary(stack) : "";
        selectedSummary?.SetTextImmediately(has && liquidSummary.Length == 0
            ? XUiC_RebirthCraftingItemContext.BuildSummary(stack) : liquidSummary);
        selectedDescription?.SetTextImmediately(has ? XUiC_RebirthCraftingItemContext.ResolveDescription(stack, xui) : "");
        var icon = selectedIcon;
        if (icon != null)
        {
            icon.IsVisible = has;
            if (has) icon.SpriteName = stack.itemValue.ItemClass.GetIconName();
        }
        RebirthSelectedDurability.Render(this, "characterBagDurability", selected);
        if (actions != null)
        {
            // Wake before the native list is rebuilt. This does not replace its action
            // generation, enabled checks, shortcuts, subscriptions or activation handlers.
            (actions as XUiC_RebirthBackpackActionList)?.SetIdlePresentation(false);
            int type = has ? stack.itemValue.type : 0;
            string signature = has ? stack.itemValue.Quality + ":" + (stack.itemValue.UseTimes > 0)
                + ":" + stack.itemValue.ModificationCount + ":" + stack.itemValue.CosmeticModCount
                + ":" + (xui.AssembleItem.CurrentItem != null) : "";
            if (actionType != type || actionSignature != signature)
            {
                actionType = type;
                actionSignature = signature;
                actions.SetCraftingActionList(!has ? XUiC_ItemActionList.ItemActionListTypes.None
                    : selected is XUiC_EquipmentStack ? XUiC_ItemActionList.ItemActionListTypes.Equipment
                    : XUiC_ItemActionList.ItemActionListTypes.Item, selected);
                for (int i = actions.itemActionEntries.Count - 1; i >= 0; i--)
                    if (actions.itemActionEntries[i] is ItemActionEntryDrop)
                    {
                        actions.itemActionEntries[i].DisableEvents();
                        actions.itemActionEntries.RemoveAt(i);
                    }
                if (has && selected is XUiC_ItemStack && IsSurvivorGear(stack))
                {
                    // The native menu treats this custom ItemActionEat-derived equip action as Heal.
                    // Replace only that entry with the existing server-confirmed gear transaction.
                    for (int i = actions.itemActionEntries.Count - 1; i >= 0; i--)
                        if (actions.itemActionEntries[i] is ItemActionEntryUse)
                        {
                            actions.itemActionEntries[i].DisableEvents();
                            actions.itemActionEntries.RemoveAt(i);
                        }
                    actions.AddActionListEntry(new RebirthCharacterGearEquipEntry(selected));
                }
                RebirthEditorActions.Adapt(actions, selected);
                if(selected is XUiC_Creative2Stack)
                    for(int i=actions.itemActionEntries.Count-1;i>=0;i--)
                        if(actions.itemActionEntries[i] is ItemActionEntryUse||actions.itemActionEntries[i] is ItemActionEntryEquip
                            ||actions.itemActionEntries[i] is ItemActionEntryWear||actions.itemActionEntries[i] is RebirthCharacterGearEquipEntry
                            ||actions.itemActionEntries[i] is RebirthLiteratureUseEntry)
                        {actions.itemActionEntries[i].DisableEvents();actions.itemActionEntries.RemoveAt(i);}
                actions.Update(0f);
            }
            if (actions.ViewComponent.IsVisible != has) actions.ViewComponent.IsVisible = has;
            if (actions.ViewComponent.UiTransform != null && actions.ViewComponent.UiTransform.gameObject.activeSelf != has)
                actions.ViewComponent.UiTransform.gameObject.SetActive(has);
            (actions as XUiC_RebirthBackpackActionList)?.SetIdlePresentation(!has);
            int count = 0;
            foreach (var entry in actionViews)
            {
                entry.ViewComponent.IsVisible = has && entry.ItemActionEntry != null;
                if (has && entry.ItemActionEntry != null) count++;
            }
            LayoutActionRows((count + 1) / 2);
        }
    }

    private void LayoutActionRows(int rows)
    {
        rows = Math.Max(2, rows);
        if (rows == lastActionRows) return;
        int previousExtra = Math.Max(0, lastActionRows - 2) * 40;
        lastActionRows = rows;
        int extra = (rows - 2) * 40;
        foreach (string name in new[] { "rbBackpackTheorySection", "rbBackpackSellSection" })
        {
            var view = GetChildById(name)?.ViewComponent;
            if (view == null) continue;
            view.Position = new Vector2i(view.Position.x, view.Position.y + previousExtra - extra);
            view.TryUpdatePosition();
        }
        int bagHeight = GetParentByType<XUiC_RebirthCreativeWorkspace>() != null ? 456 : 380;
        foreach (string name in new[] { "characterBagInventoryHeading", "characterBagFilter", "characterBagSort", "characterBagLock", "characterBagCapacity", "characterBagContextRule" })
        {
            var view = GetChildById(name)?.ViewComponent;
            if (view == null) continue;
            int y = name == "characterBagInventoryHeading" ? 347 : name == "characterBagCapacity" ? 382 : name == "characterBagContextRule" ? 342 : 382;
            view.Position = new Vector2i(view.Position.x, -y - extra);
            view.TryUpdatePosition();
        }
        var background = GetChildById("characterBagContextBg")?.ViewComponent;
        if (background != null) background.Size = new Vector2i(420, 292 + extra);
        if (scroll == null) return;
        scroll.ViewComponent.Position = new Vector2i(14, -416-extra);
        scroll.ViewComponent.Size = new Vector2i(408,bagHeight-extra);
        scroll.ViewComponent.TryUpdatePosition();
        var clip = scroll.GetChildById("listViewport")?.ViewComponent as XUiV_Panel;
        if (clip != null)
        {
            clip.Size = new Vector2i(384,bagHeight-extra);
            clip.ClippingSize = new Vector2(384,bagHeight-extra);
            clip.ClippingCenter = new Vector2(192,-(bagHeight-extra)/2f);
            var panel = clip.UiTransform?.GetComponent<UIPanel>();
            if (panel != null) panel.baseClipRegion = new Vector4(192,-(bagHeight-extra)/2f,384,bagHeight-extra);
        }
        scroll.RestoreScrollOffset(scroll.ScrollOffset);
    }
}

public sealed class RebirthCharacterGearEquipEntry : BaseItemActionEntry
{
    public static void Adapt(XUiC_ItemActionList actions, XUiController controller)
    {
        var slot = controller as XUiC_ItemStack;
        RebirthTraitSupportProfileDefinition profile;
        if (actions == null || slot?.ItemStack?.itemValue?.ItemClass == null ||
            !RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(slot.ItemStack.itemValue.ItemClass.GetItemName(), out profile) ||
            profile == null || !string.Equals(profile.Kind, "survivor_gear", StringComparison.OrdinalIgnoreCase)) return;
        for (int i = actions.itemActionEntries.Count - 1; i >= 0; i--)
        {
            if (actions.itemActionEntries[i] is RebirthCharacterGearEquipEntry) return;
            if (!(actions.itemActionEntries[i] is ItemActionEntryUse)) continue;
            actions.itemActionEntries[i].DisableEvents();
            actions.itemActionEntries.RemoveAt(i);
        }
        actions.AddActionListEntry(new RebirthCharacterGearEquipEntry(controller));
    }
    public RebirthCharacterGearEquipEntry(XUiController controller)
        : base(controller, "lblContextActionEquip", "ui_game_symbol_shirt", GamepadShortCut.DPadUp) { }

    public override void RefreshEnabled()
    {
        var ui = ItemController.xui;
        Enabled = !RebirthConsoleInputGuardRuntime.BlocksGameplayInput()
            && ui.DragAndDropWindow.IsEmpty() && ui.AssembleItem.CurrentItem == null
            && ItemController is XUiC_ItemStack slot && !slot.ItemStack.IsEmpty();
    }

    public override void OnActivated()
    {
        RefreshEnabled();
        if (!Enabled) return;
        var slot = (XUiC_ItemStack)ItemController;
        new ItemActionEquipSurvivorGearRebirth().ExecuteInstantAction(
            ItemController.xui.playerUI.entityPlayer, slot.ItemStack, false, slot);
    }
}

[Preserve]
public sealed class XUiC_RebirthCharacterBackpackSlot : XUiC_ItemStack
{
    private RebirthSlotPalette palette;
    private XUiC_RebirthCharacterOverviewList scrollOwner;
    private XUiView viewport, cellView, rowView;
    private bool culled;
    private XUiC_RebirthCreativeWorkspace creativeOwner;
    private RebirthSlotPresentationGate presentation;
    private XUiView contentView;
    private bool presentationHovered;
    private float presentationWakeUntil;
    private XUiC_RebirthCharacterBackpack inventoryOwner;
    private XUiV_Sprite selectionBorder;
    private const float SafetyRefreshSeconds = .25f;
    private int childFlushPasses;
    private float nextNativeRefresh;
    public override void Init()
    {
        base.Init(); palette = new RebirthSlotPalette(this);
        inventoryOwner = GetParentByType<XUiC_RebirthCharacterBackpack>();
        selectionBorder = GetChildById("background")?.ViewComponent as XUiV_Sprite;
        scrollOwner = GetParentByType<XUiC_RebirthCharacterOverviewList>();
        viewport = scrollOwner?.GetChildById("listViewport")?.ViewComponent;
        // These are the explicitly authored row/cell wrappers, not a native repeat grid.
        if (Parent?.Parent?.Parent?.ViewComponent?.ID == "listContent")
        { cellView = Parent.ViewComponent; rowView = Parent.Parent.ViewComponent; contentView = Parent.Parent.Parent.ViewComponent; }
        creativeOwner = GetParentByType<XUiC_RebirthCreativeWorkspace>();
    }
    public override void OnOpen()
    {
        creativeOwner = creativeOwner ?? GetParentByType<XUiC_RebirthCreativeWorkspace>();
        inventoryOwner = inventoryOwner ?? GetParentByType<XUiC_RebirthCharacterBackpack>();
        presentation?.Show(); culled = false; presentationHovered = false; presentationWakeUntil = 0f;
        childFlushPasses = 0;
        nextNativeRefresh = 0f;
        base.OnOpen();
    }
    public override void OnClose()
    {
        presentation?.Show(); culled = false; childFlushPasses = 0;
        RebirthCharacterItemStatsTooltip.Hover(this, false);
        base.OnClose();
    }
    public override void OnHovered(bool over)
    {
        RebirthCharacterItemStatsTooltip.Hover(this, over);
        if (presentationHovered == over) return;
        presentationWakeUntil = Time.realtimeSinceStartup + .55f;
        presentationHovered = over;
        RefreshBindings(); base.OnHovered(over);
        if (!over && !IsSelected && !IsHolding && !IsDragAndDrop) CommitBorder(backgroundColor);
    }
    public override bool GetBindingValueInternal(ref string value, string name) {
        if (name == "backgroundcolor") { value = AttributeLock ? RebirthSlotPalette.Locked : RebirthSlotPalette.Normal; return true; }
        if (name == "tooltip") {
            var stats = RebirthCharacterItemStatsTooltip.Build(this, ItemStack);
            if (stats != null) { value = stats; return true; }
        }
        return base.GetBindingValueInternal(ref value, name);
    }

    private bool OutsideViewport(bool useRenderedOffset)
    {
        if (scrollOwner == null || viewport == null || cellView == null || rowView == null) return false;
        float offset = useRenderedOffset && contentView?.UiTransform != null
            ? contentView.UiTransform.localPosition.y : scrollOwner.ScrollOffset;
        float top = rowView.Position.y + cellView.Position.y + offset;
        if (!useRenderedOffset) return top - ViewComponent.Size.y >= 0f || top <= -viewport.Size.y;
        return !RebirthCreativeViewportMath.Intersects(top, ViewComponent.Size.y, viewport.Size.y);
    }
    private bool PinnedForNativeAction => IsSelected || IsHolding || IsLocked || IsDragAndDrop || presentationHovered ||
        Time.realtimeSinceStartup < presentationWakeUntil || creativeOwner?.BackpackView?.SelectedItem == this ||
        creativeOwner?.PresentationInteractionBusy == true;

    // Keep the native slot ROOT active: native empty-slot discovery and transaction sources
    // can consult activeInHierarchy. Only its visual descendants and root hit colliders sleep.
    private bool CullCreativePresentation()
    {
        if (creativeOwner?.IsWorkspaceOpen != true || !OutsideViewport(true) || PinnedForNativeAction)
            return false;
        if (!culled) Hovered(false);
        culled = true;
        presentation = presentation ?? new RebirthSlotPresentationGate(this);
        return presentation.Hide(); // If grouping cannot be initialized, retain the native path.
    }
    public override void Update(float dt)
    {
        if (ViewComponent?.UiTransform == null || !ViewComponent.UiTransform.gameObject.activeInHierarchy) return;
        if (creativeOwner?.IsWorkspaceOpen == true)
        {
            if (CullCreativePresentation())
            {
#if REBIRTH_UI_DIAGNOSTICS
                RebirthCreativePerformance.CountCulledBackpack();
#endif
                return;
            }
        }
        else if (OutsideViewport(true) && !PinnedForNativeAction)
        {
            if (!culled) Hovered(false);
            culled = true;
            presentation = presentation ?? new RebirthSlotPresentationGate(this);
            if (presentation.Hide()) return;
        }
        bool waking = culled || presentation?.Hidden == true;
        culled = false;
        if (waking) IsDirty = true;
        try
        {
            bool pointerHere = ViewComponent.UiTransformIsHovered;
            if (pointerHere != presentationHovered) OnHovered(pointerHere);
            bool frameReady = inventoryOwner?.InventoryOpen == true && inventoryOwner.FrameNumber == Time.frameCount;
            float now = frameReady ? inventoryOwner.FrameClock : Time.realtimeSinceStartup;
            bool dirty = IsDirty;
            bool interacting = presentationHovered || IsSelected || IsHolding || IsLocked || IsDragAndDrop ||
                now < presentationWakeUntil || inventoryOwner?.SelectedItem == this ||
                inventoryOwner?.FrameInteractionBusy == true || inventoryOwner?.FrameEditorActive == true;
            // Unlike Creative source cells, REAL inventory dirtiness always takes the native
            // path immediately. No ignored notification, custom inventory copy, or dirty reset.
            RebirthBackpackSlotUpdateMode mode = RebirthBackpackSlotUpdatePolicy.Choose(
                frameReady, dirty, waking, interacting, now >= nextNativeRefresh, childFlushPasses);
#if REBIRTH_UI_DIAGNOSTICS
            bool recording = creativeOwner?.IsWorkspaceOpen == true;
#endif
            if (mode == RebirthBackpackSlotUpdateMode.Native)
            {
#if REBIRTH_UI_DIAGNOSTICS
                long started = recording ? RebirthCreativePerformance.Begin() : 0;
                try { base.Update(dt); }
                finally { if (recording) RebirthCreativePerformance.EndBackpackSlot(started, dirty); }
#else
                base.Update(dt);
#endif
                childFlushPasses = 2;
                float phase = (SlotNumber % 31) * (SafetyRefreshSeconds / 31f);
                nextNativeRefresh = (Mathf.Floor((now - phase) / SafetyRefreshSeconds) + 1f) * SafetyRefreshSeconds + phase;
                if (nextNativeRefresh <= now) nextNativeRefresh = now + SafetyRefreshSeconds;
            }
            else if (mode == RebirthBackpackSlotUpdateMode.ViewsOnly)
            {
#if REBIRTH_UI_DIAGNOSTICS
                long started = recording ? RebirthCreativePerformance.Begin() : 0;
                try
                {
                    ViewComponent.Update(dt);
                    foreach (XUiController child in Children) child.Update(dt);
                }
                finally { if (recording) RebirthCreativePerformance.EndBackpackViews(started); }
#else
                ViewComponent.Update(dt);
                foreach (XUiController child in Children) child.Update(dt);
#endif
                --childFlushPasses;
            }
#if REBIRTH_UI_DIAGNOSTICS
            else if (recording) RebirthCreativePerformance.CountIdleBackpackSlot();
#endif
            if (waking) RebirthSlotPresentationGate.FlushViews(this);
            palette?.Apply(AttributeLock);
            CommitBorder(SelectionBorderColor);
        }
        finally { if (waking) presentation?.Show(); }
        if (waking) { palette?.Apply(AttributeLock); CommitBorder(SelectionBorderColor); }
    }
    private void CommitBorder(Color color)
    {
        if (selectionBorder == null) return;
        if (selectionBorder.Color != color) selectionBorder.Color = color;
        if (selectionBorder.Sprite != null && selectionBorder.Sprite.color != color)
            selectionBorder.SetColorImmediately(color);
    }
    internal void FinishCreativeViewport()
    {
        if (inventoryOwner?.InventoryOpen != true || ViewComponent?.UiTransform == null ||
            !ViewComponent.UiTransform.gameObject.activeInHierarchy) return;
        if (creativeOwner?.IsWorkspaceOpen == true && CullCreativePresentation()) return;
        // Only a previously sleeping source gets a zero-time native preparation pass.
        // Its root hit colliders remain disabled until its late binding writes are committed.
        if (presentation?.Hidden == true && !OutsideViewport(true)) Update(0f);
    }
    public override void updateItemInfoWindow(XUiC_ItemStack stack) { }
    public override void HandleClickComplete()
    {
        base.HandleClickComplete();
        GetParentByType<XUiC_RebirthCharacterBackpack>()?.SelectSlot(SlotNumber);
    }
}

// A mutable inventory has stricter invalidation than the read-only Creative catalogue.
internal enum RebirthBackpackSlotUpdateMode { Native, ViewsOnly, Idle }
internal static class RebirthBackpackSlotUpdatePolicy
{
    internal static RebirthBackpackSlotUpdateMode Choose(bool frameReady, bool dirty, bool waking,
        bool interacting, bool safetyDue, int pendingViews)
    {
        if (!frameReady || dirty || waking || interacting || safetyDue)
            return RebirthBackpackSlotUpdateMode.Native;
        return pendingViews > 0 ? RebirthBackpackSlotUpdateMode.ViewsOnly : RebirthBackpackSlotUpdateMode.Idle;
    }
}

[HarmonyLib.HarmonyPatch(typeof(XUiC_EquipmentStack), nameof(XUiC_EquipmentStack.HandleItemInspect))]
public static class RebirthCharacterEquipmentInspect
{
    public static bool Prefix(XUiC_EquipmentStack __instance)
    {
        var owner = __instance.GetParentByType<XUiC_RebirthSurvivorCharacter>();
        if (owner == null || !owner.IsCharacterWindowOpen) return true;
        var backpack = owner.GetChildByType<XUiC_RebirthCharacterBackpack>();
        if (backpack == null) return true;
        backpack.SelectItem(__instance);
        __instance.HandleClickComplete();
        return false;
    }
}

/// <summary>Registers the Overview armor/clothing/biome slots with native equipment transactions.</summary>
[Preserve]
public sealed class XUiC_RebirthCharacterEquipment : XUiC_EquipmentStackGrid
{
    private XUiC_EquipmentStack[] equipmentViews;
    public override void OnOpen()
    {
        base.OnOpen();
        equipmentViews = GetChildrenByType<XUiC_EquipmentStack>();
    }
    public override void Update(float dt)
    {
        if (ViewComponent?.UiTransform == null || !ViewComponent.UiTransform.gameObject.activeInHierarchy ||
            xui?.PlayerEquipment == null || GameManager.Instance?.World == null) return;
        base.Update(dt);
        if (equipmentViews == null) equipmentViews = GetChildrenByType<XUiC_EquipmentStack>();
        var pointer = UICamera.hoveredObject != null ? UICamera.hoveredObject.transform : null;
        foreach (var slot in equipmentViews) {
            var stats = RebirthCharacterItemStatsTooltip.Build(slot, slot.ItemStack);
            if (stats != null && slot.ViewComponent.ToolTip != stats) slot.ViewComponent.ToolTip = stats;
            if (pointer != null && slot.ViewComponent.UiTransform != null && pointer.IsChildOf(slot.ViewComponent.UiTransform))
                RebirthCharacterItemStatsTooltip.Hover(slot, true);
        }
    }
}

/// <summary>One identity transform per culled slot; no inventory or XUi-controller reparenting.</summary>
internal sealed class RebirthSlotPresentationGate
{
    private readonly XUiC_ItemStack owner;
    private GameObject group;
    private Collider[] hitTargets;
    private bool[] previousColliderStates;
    private bool failed;
    internal bool Hidden { get; private set; }
    internal RebirthSlotPresentationGate(XUiC_ItemStack owner) { this.owner = owner; }

    private bool EnsureGroup()
    {
        if (failed) return false;
        if (group != null) return true;
        Transform root = owner.ViewComponent?.UiTransform;
        if (root == null || root.childCount == 0) return false;
        Transform[] children = new Transform[root.childCount];
        for (int i = 0; i < children.Length; ++i) children[i] = root.GetChild(i);
        try
        {
            group = new GameObject("REBIRTH_Creative_SlotPresentation");
            group.layer = root.gameObject.layer;
            group.transform.SetParent(root, false);
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;
            // local TRS is identity. Every existing child's local TRS, widget depth, activeSelf,
            // bindings, controller Parent and native SlotNumber are left untouched.
            for (int i = 0; i < children.Length; ++i) children[i].SetParent(group.transform, false);
            hitTargets = root.GetComponents<Collider>();
            previousColliderStates = new bool[hitTargets.Length];
            return true;
        }
        catch (Exception error)
        {
            // A nonstandard view hierarchy falls back to the original presentation, not a
            // partially hidden inventory. This is a bounded construction-time path only.
            bool restored = true;
            for (int i = 0; i < children.Length; ++i)
            {
                try { if (children[i] != null) children[i].SetParent(root, false); }
                catch { restored = false; }
            }
            // Never destroy a grouping object that might still own an existing child.
            if (group != null && restored) UnityEngine.Object.Destroy(group);
            else if (group != null) group.SetActive(true);
            if (restored) group = null;
            failed = true;
            Log.Warning("[REBIRTH Creative29] Slot presentation gate unavailable: " + error.Message);
            return false;
        }
    }
    internal bool Hide()
    {
        if (Hidden) return true;
        if (!EnsureGroup()) return false;
        for (int i = 0; i < hitTargets.Length; ++i)
        {
            if (hitTargets[i] == null) continue;
            previousColliderStates[i] = hitTargets[i].enabled;
            hitTargets[i].enabled = false;
        }
        group.SetActive(false);
        Hidden = true;
        return true;
    }
    internal void Show()
    {
        if (!Hidden) return;
        // Child activeSelf values may legitimately change as native bindings synchronize a
        // sleeping slot. Enabling the ancestor preserves those current values, not a stale
        // snapshot of the old item's icon/favorite/quality visibility.
        if (group != null) group.SetActive(true);
        for (int i = 0; hitTargets != null && i < hitTargets.Length; ++i)
            if (hitTargets[i] != null) hitTargets[i].enabled = previousColliderStates[i];
        Hidden = false;
    }
    internal static void FlushViews(XUiController controller)
    {
        // Native ItemStack refreshes bindings AFTER its normal child update. Commit the
        // resulting view writes while the ancestor is still hidden; no second input walk.
        if (controller.ViewComponent?.UiTransform != null) controller.ViewComponent.updateData();
        foreach (XUiController child in controller.Children) FlushViews(child);
    }
}
internal static class RebirthCreativeViewportMath
{
    // Strict intersection: a cell merely touching the clip edge is not visible. Actual rendered
    // content offsets are used; partially intersecting rows are retained at fractional movement.
    internal static bool Intersects(float top, float cellHeight, float viewportHeight)
    {
        if (float.IsNaN(top) || float.IsInfinity(top) || cellHeight <= 0f || viewportHeight <= 0f)
            return true; // Fail open for geometry not initialized yet.
        return top - cellHeight < 0f && top > -viewportHeight;
    }
}


/// <summary>Only an explicitly unselected, empty action pane can sleep; real actions remain native.</summary>
[Preserve]
public sealed class XUiC_RebirthBackpackActionList : XUiC_ItemActionList
{
    private bool idlePresentation, cleared;
    internal void SetIdlePresentation(bool idle)
    {
        if (idlePresentation != idle) cleared = false;
        idlePresentation = idle;
    }
    public override void OnOpen()
    {
        idlePresentation = false; cleared = false;
        base.OnOpen();
    }
    public override void OnClose()
    {
        idlePresentation = false; cleared = false;
        base.OnClose();
    }
    public override void Update(float dt)
    {
        if (idlePresentation && itemActionEntries != null && itemActionEntries.Count == 0)
        {
            // Finish native clearing once. Native action-running bookkeeping has nothing
            // to enable here; a new selection explicitly wakes the list before generation.
            if (!cleared) { base.Update(dt); cleared = true; }
            var root = ViewComponent?.UiTransform;
            if (root != null && !ViewComponent.IsVisible && root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
            return;
        }
        cleared = false;
        var transform = ViewComponent?.UiTransform;
        if (transform != null && ViewComponent.IsVisible && !transform.gameObject.activeSelf)
            transform.gameObject.SetActive(true);
        base.Update(dt);
    }
}
