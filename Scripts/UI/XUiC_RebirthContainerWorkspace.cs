using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Equal-width container/inspector/backpack presentation. The backpack uses the same
/// thirteen-column physical ordering as personal crafting.
/// This controller is a view, not a second inventory. Commands and item-action targets use
/// the original native controllers. A physical slot index never changes when scrolling.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthContainerWorkspace : XUiController
{
    private RebirthContainerSession session;
    private XUiC_RebirthContextGrid left, right;
    private XUiController body, instructions, selection;
    private XUiV_Label title, currency, capacity, inspectTitle, selectedName, selectedCount, summary, description;
    private readonly XUiV_Label[] statNames = new XUiV_Label[7], statValues = new XUiV_Label[7];
    private ItemStack hoveredComparison = ItemStack.Empty;
    private XUiV_Sprite selectedIcon;
    private XUiC_ItemActionList actions;
    private XUiController wifiSource, categorySource;
    private XUiController wifiButton, categoryButton, lockLeft, lockRight;
    private XUiC_ItemStack selectedNative;
    private XUiC_RebirthContextSlot selectedMirror;
    private ItemStack renderedSelection;
    private int renderedLiquidFingerprint;
    private bool opened, presented, activateReleased, activatePressed;
    private bool lastLeftLock, lastRightLock, forceSelection;
    private int lastCurrency = int.MinValue;
    private float nextSync;
    private Vector2i lastPosition = new Vector2i(int.MinValue, int.MinValue);
    private Vector2i lastSize = new Vector2i(-1, -1);
    public XUiController SelectedItem => selectedNative;
    internal XUiController TooltipSelection => (XUiController)selectedMirror ?? selectedNative;
    internal bool CanBind => left != null && right != null && body?.ViewComponent != null &&
        instructions != null && selection != null && actions != null &&
        left.GetItemStackControllers()?.Length == XUiC_RebirthContextGrid.ContainerPool &&
        right.GetItemStackControllers()?.Length == XUiC_RebirthContextGrid.BackpackPool;

    public override void Init()
    {
        base.Init();
        body = GetChildById("contextBody");
        left = GetChildById("contextContainerGrid") as XUiC_RebirthContextGrid;
        right = GetChildById("contextBackpackGrid") as XUiC_RebirthContextGrid;
        instructions = GetChildById("contextInspectInstructions");
        selection = GetChildById("contextInspectSelection");
        title = Label("contextContainerName"); currency = Label("contextCurrency");
        capacity = Label("contextBackpackCapacity"); selectedName = Label("contextSelectedName");
        summary = Label("contextSelectedSummary"); description = Label("contextSelectedDescription");
        selectedIcon = GetChildById("contextSelectedIcon")?.ViewComponent as XUiV_Sprite;
        selectedCount = Label("contextSelectedCount");
        inspectTitle = Label("contextInspectTitle");
        for (int i = 0; i < statNames.Length; ++i)
        {
            statNames[i] = Label("contextStatName" + i);
            statValues[i] = Label("contextStatValue" + i);
        }
        actions = GetChildByType<XUiC_ItemActionList>();
        wifiButton = GetChildById("contextWifi"); categoryButton = GetChildById("contextCategory");
        lockLeft = GetChildById("contextContainerLock"); lockRight = GetChildById("contextBackpackLock");
        Wire("contextContainerSort", false, 0); Wire("contextContainerStack", false, 1);
        Wire("contextContainerTake", false, 2); Wire("contextContainerLock", false, 3);
        Wire("contextBackpackSort", true, 0); Wire("contextBackpackStack", true, 1);
        Wire("contextBackpackStash", true, 2); Wire("contextBackpackLock", true, 3);
        if (wifiButton != null) wifiButton.OnPress += (c, b) => Forward(wifiSource, b);
        if (categoryButton != null) categoryButton.OnPress += (c, b) => Forward(categorySource, b);
        var clearSelection = GetChildById("contextClearSelection");
        if (clearSelection != null) clearSelection.OnPress += (c, b) => { if (CanAct(b)) Select(null); };
    }
    private XUiV_Label Label(string id) => GetChildById(id)?.ViewComponent as XUiV_Label;
    private void Wire(string id, bool backpack, int command)
    {
        XUiController c = GetChildById(id);
        if (c != null) c.OnPress += (sender, button) => { if (CanAct(button)) Command(backpack, command); };
    }
    private bool CanAct(int button) => opened && presented && session != null && !session.Suspended &&
        (button == -1 || button == 0) && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput() &&
        xui.DragAndDropWindow.IsEmpty() && !xui.IsUsingItemActionEntryUse;
    private void Forward(XUiController target, int button)
    {
        if (!CanAct(button) || target == null) return;
        target.Pressed(-1); // native OnPress, including its ownership/permission checks
        nextSync = 0f;
    }
    internal void Command(bool backpack, int command)
    {
        if (session == null || session.Suspended || xui.IsUsingItemActionEntryUse) return;
        session.FlushLocks();
        var controls = session.Controls(backpack);
        switch (command)
        {
            case 0: Select(null); controls.Sort(); break;
            case 1: Select(null); controls.MoveFillAndSmart(); break;
            case 2: Select(null); controls.MoveAll(); break;
            case 3: controls.ToggleLockMode(); break;
        }
        nextSync = 0f;
        left?.Invalidate(); right?.Invalidate();
    }
    public override void OnOpen()
    {
        session = RebirthContextNavigationService.Session;
        opened = session != null;
        if (!opened) return;
        // Group controller Init ran before native open; bind each full-index view to its source.
        left.Bind(session, this, false); right.Bind(session, this, true);
        base.OnOpen();
        selectedNative = null; selectedMirror = null; renderedSelection = null; forceSelection = true;
        hoveredComparison = ItemStack.Empty;
        activateReleased = false; activatePressed = false;
        lastCurrency = int.MinValue; nextSync = 0f; lastSize = new Vector2i(-1, -1);
        wifiSource = session.Owner.GetChildById("btnRebirthRemoteResources");
        categorySource = session.Owner.GetChildById("btnRebirthQuickStackCategories");
        // Category control names vary between authored storage windows. Forward only a real
        // existing button; no dead overflow icon or fabricated permissions/action.
        SetActive(wifiButton, wifiSource != null);
        SetActive(categoryButton, categorySource != null);
        SetPresented(true);
        RefreshHeader(); RefreshSelection();
    }
    public override void OnClose()
    {
        opened = false; presented = false;
        actions?.SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.None, null);
        selectedMirror?.SetContextSelected(false);
        selectedNative = null; selectedMirror = null; renderedSelection = null;
        base.OnClose();
        session = null;
        // Unexpected overlay closure must not leave an invisible locked interaction behind.
        if (RebirthContextNavigationService.IsActiveFor(xui)) RebirthContextNavigationService.CloseInteraction();
    }
    internal void SetPresented(bool show)
    {
        presented = show;
        SetActive(this, show);
        if (!show)
        {
            RebirthCharacterItemStatsTooltip.Hovered = null;
            actions?.SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.None, null);
            forceSelection = true;
            return;
        }
        // Match Crafting's menu ownership. Native Character/windowpaging OnClose can reset
        // InMenu during a return route; re-acquire it only for this visible workspace.
        if (xui.DragAndDropWindow != null) xui.DragAndDropWindow.InMenu = true;
        hoveredComparison = ItemStack.Empty;
        nextSync = 0f;
        left?.Invalidate(); right?.Invalidate();
        ApplyLayout();
    }
    public override void Update(float dt)
    {
        if (!opened || !presented || session == null || session.Suspended) return;
        ApplyLayout();
        if (Time.realtimeSinceStartup >= nextSync)
        {
            nextSync = Time.realtimeSinceStartup + .1f;
            RefreshHeader(); RefreshSelection();
        }
        base.Update(dt);
        RefreshActionVisibility();
        if (RebirthContextNavigationService.Session != session || session == null) return;
        UpdateInput();
    }
    private void UpdateInput()
    {
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return;
        var input = xui.playerUI.playerInput;
        if (input == null) return;
        if (!input.PermanentActions.Activate.IsPressed) activateReleased = true;
        if (activateReleased)
        {
            if (input.PermanentActions.Activate.IsPressed) activatePressed = true;
            if (activatePressed && input.PermanentActions.Activate.WasReleased && !xui.playerUI.windowManager.IsInputActive())
            { RebirthContextNavigationService.CloseInteraction(); return; }
        }
        if (input.GUIActions.Cancel.WasPressed || input.PermanentActions.Cancel.WasPressed)
        {
            if (session.LockMode(false)) session.LeftControls.ToggleLockMode();
            if (session.LockMode(true)) session.RightControls.ToggleLockMode();
            nextSync = 0f;
        }
        if (!xui.playerUI.windowManager.IsInputActive() && xui.DragAndDropWindow.IsEmpty() &&
            (input.GUIActions.LeftStick.WasPressed || input.PermanentActions.Reload.WasPressed)) Command(false, 2);
    }
    private void RefreshHeader()
    {
        if (session == null) return;
        SetText(title, session.Binding(false, "lootcontainer_name", RebirthContextNavigationService.PrimaryLabel));
        int amount = xui.PlayerInventory.CurrencyAmount;
        if (amount != lastCurrency) { lastCurrency = amount; SetText(currency, amount.ToString("N0") + " $"); }
        ItemStack[] bag = session.GetSlots(true);
        if (bag != null) SetText(capacity, RebirthCraftingInventoryBridge.GetUsedSlotCount(xui) + "/" + bag.Length +
            "  •  " + RebirthCraftingInventoryBridge.GetEncumberedUsedSlotCount(xui) + " ENCUMBERED");
        bool leftMode = session.LockMode(false), rightMode = session.LockMode(true);
        if (leftMode != lastLeftLock) { lastLeftLock = leftMode; left.Invalidate(); }
        if (rightMode != lastRightLock) { lastRightLock = rightMode; right.Invalidate(); }
        SetLockTint(lockLeft, leftMode); SetLockTint(lockRight, rightMode);
        // ESC exits either lock mode before closing the interaction. Native modes each set this
        // flag independently, so combine them while both inventories share one visible workspace.
        session.Window.isEscClosable = !(leftMode || rightMode);
        SetActive(lockLeft, session.LeftControls.LockedSlots != null);
        if (wifiSource?.ViewComponent != null && wifiButton?.ViewComponent != null)
        {
            wifiSource.Update(0f); // retain the native throttled permission/state refresh
            SetActive(wifiButton, wifiSource.ViewComponent.IsVisible);
            wifiButton.ViewComponent.ToolTip = wifiSource.ViewComponent.ToolTip;
            if (wifiSource.ViewComponent is XUiV_Button native && wifiButton.ViewComponent is XUiV_Button visible)
            { visible.Selected = native.Selected; visible.Enabled = native.Enabled; }
        }
    }
    private static void SetLockTint(XUiController controller, bool active)
    {
        var sprite = controller?.ViewComponent as XUiV_Sprite;
        if (sprite == null) return;
        Color32 color = active ? new Color32(220, 195, 120, 255) : new Color32(255, 255, 255, 255);
        if (!sprite.Color.Equals((Color)color)) { sprite.Color = color; sprite.SetColorImmediately(color); }
    }
    internal void Select(XUiC_RebirthContextSlot mirror) => SelectItem(mirror);

    // The same explicit click-completion route as RebirthCraftingItemContext. Toolbelt slots
    // enter here too; selection is not delegated to the deliberately hidden native Inspect view.
    internal void SelectItem(XUiC_ItemStack item)
    {
        if (!opened || !presented || session == null || session.Suspended) return;
        XUiC_RebirthContextSlot mirror = item as XUiC_RebirthContextSlot;
        XUiC_ItemStack native = null;
        if (item != null && item.xui == xui && item.ItemStack != null && !item.ItemStack.IsEmpty())
        {
            if (mirror != null && mirror.IsPresented)
            {
                ItemStack[] slots = session.GetSlots(mirror.IsBackpack);
                if (slots != null && mirror.SlotNumber >= 0 && mirror.SlotNumber < slots.Length)
                    native = session.NativeSlot(mirror.IsBackpack, mirror.SlotNumber);
            }
            else if (item.StackLocation == XUiC_ItemStack.StackLocationTypes.ToolBelt &&
                item.SlotNumber >= 0 && item.SlotNumber < RebirthToolbeltCapacity.GetOwnedSlotCount(xui.playerUI.entityPlayer, xui.playerUI.entityPlayer.inventory.Length))
                native = item;
        }
        if (native == null) mirror = null;
        if (selectedMirror != mirror) selectedMirror?.SetContextSelected(false);
        selectedMirror = mirror;
        selectedMirror?.SetContextSelected(true);
        selectedNative = native;
        if (selectedNative != null) selectedNative.InfoWindow = session.InfoWindow;
        renderedSelection = null; forceSelection = true;
        RefreshSelection();
    }
    internal void SourceChanged()
    {
        // Native source callbacks run synchronously after a slot write. Poll that authoritative
        // source on the next update; never change selection during a passive mirror assignment.
        nextSync = 0f;
        left?.Invalidate(); right?.Invalidate();
    }
    internal void CompareStats(ItemStack stack)
    {
        hoveredComparison = stack ?? ItemStack.Empty;
        if (opened && presented && session?.Suspended == false) RenderItemStats();
    }
    private void RenderItemStats()
    {
        ItemStack stack = selectedNative?.ItemStack;
        // Hovering the selected mirror itself is not a comparison with a different item.
        ItemStack comparison = RebirthCharacterItemStatsTooltip.Hovered == TooltipSelection ? null : hoveredComparison;
        System.Collections.Generic.List<RebirthConsumableItemPresentation.Row> consumableRows;
        bool consumable = RebirthConsumableItemPresentation.TryBuild(stack, comparison,
            xui, out consumableRows, true);
        var display = !consumable && stack != null && !stack.IsEmpty()
            ? UIDisplayInfoManager.Current.GetDisplayStatsForTag(stack.itemValue.ItemClass.DisplayType) : null;
        int nativeIndex = 0;
        bool nativeCompare = display != null && !hoveredComparison.IsEmpty() &&
            XUiM_ItemStack.CanCompare(stack.itemValue.ItemClass, hoveredComparison.itemValue.ItemClass);
        for (int i = 0; i < statNames.Length; ++i)
        {
            string titleText = "", valueText = "";
            if (consumable)
            {
                if (i < consumableRows.Count)
                { titleText = consumableRows[i].Title; valueText = consumableRows[i].Value; }
            }
            else if (display != null)
            {
                // Use the popup's policy and pack kept rows into the existing fixed column.
                while (nativeIndex < display.DisplayStats.Count)
                {
                    var stat = display.DisplayStats[nativeIndex++];
                    if (!RebirthConsumableItemPresentation.ShouldShowNativeStat(stat, stack.itemValue,
                        nativeCompare ? hoveredComparison.itemValue : null, xui)) continue;
                    titleText = stat.TitleOverride ?? UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType);
                    valueText = nativeCompare
                        ? XUiM_ItemStack.GetStatItemValueTextWithCompareInfo(stack.itemValue, hoveredComparison.itemValue, xui.playerUI.entityPlayer, stat)
                        : XUiM_ItemStack.GetStatItemValueTextWithModInfo(stack, xui.playerUI.entityPlayer, stat);
                    valueText = RebirthItemStatColors.Format(valueText);
                    break;
                }
            }
            if(RebirthWeaponDetailRows.TryGet(xui,stack,nativeCompare?hoveredComparison:null,i,out var weaponTitle,out var weaponText)){titleText=weaponTitle;valueText=weaponText;}
            SetText(statNames[i], titleText);
            SetText(statValues[i], valueText);
        }
    }

    private void RefreshSelection()
    {
        ItemStack current = selectedNative?.ItemStack;
        bool has = current != null && !current.IsEmpty();
        int liquidFingerprint = RebirthConsumableItemPresentation.LiquidFingerprint(current);
        if (!forceSelection && liquidFingerprint == renderedLiquidFingerprint && ((!has && renderedSelection == null) ||
            (has && renderedSelection != null && renderedSelection.Equals(current)))) return;
        // Like Crafting, content-only changes do not rebuild the native action models every tick.
        bool refreshActions = forceSelection || !has || renderedSelection == null ||
            renderedSelection.itemValue.type != current.itemValue.type;
        forceSelection = false;
        renderedSelection = has ? current.Clone() : null;
        renderedLiquidFingerprint = liquidFingerprint;
        if (!has)
        {
            selectedMirror?.SetContextSelected(false);
            selectedMirror = null; selectedNative = null;
        }
        SetActive(instructions, !has); SetActive(selection, has);
        SetText(inspectTitle, has ? "SELECTED ITEM" : "Inspect");
        SetText(selectedName, has ? selectedNative.ItemNameText : "");
        SetText(summary, has ? XUiC_RebirthCraftingItemContext.BuildSummary(current) : "");
        SetText(description, has ? XUiC_RebirthCraftingItemContext.ResolveDescription(current, xui) : "");
        SetText(selectedCount, has && current.count > 1 ? current.count.ToString() : "");
        if (selectedIcon != null && has)
        {
            ItemValue value = current.itemValue;
            ItemClass itemClass = value.ItemClassOrMissing;
            selectedIcon.SpriteName = value.GetPropertyOverride(ItemClass.PropCustomIcon, itemClass.GetIconName());
            selectedIcon.Color = itemClass.GetIconTint(value);
        }
        RebirthSelectedDurability.Render(this, "contextSelectedDurability", (XUiController)selectedMirror ?? selectedNative);
        RenderItemStats();
        if (!refreshActions) return;
        // Keep real native source slots as action targets, not the mirror or a recycled page cell.
        SetActive(actions, false);
        actions.SetCraftingActionList(has ? XUiC_ItemActionList.ItemActionListTypes.Item : XUiC_ItemActionList.ItemActionListTypes.None, has ? selectedNative : null);
        RebirthCharacterGearEquipEntry.Adapt(actions, has ? selectedNative : null);
        if (has) RebirthEditorActions.Adapt(actions, selectedNative);
        actions.Update(0f);
        RefreshActionVisibility();
        SetActive(actions, has);
    }
    private void RefreshActionVisibility()
    {
        // Native ItemActionList clears unused labels, not the custom entry's outer frame.
        // Hide the entire unassigned entry (including its colliders), as Crafting does;
        // retain the pool so an item with more real actions can still use every entry.
        if (actions == null) return;
        foreach (var entry in actions.entryList)
            if (entry != null) SetActive(entry, entry.ItemActionEntry != null);
    }
    private void ApplyLayout()
    {
        Vector2i position, size;
        RebirthScreenLayout.GetScreenBounds(xui, out position, out size);
        if (lastPosition.x == position.x && lastPosition.y == position.y && lastSize.x == size.x && lastSize.y == size.y) return;
        lastPosition = position; lastSize = size;
        float scale = Math.Min(Math.Max(1, size.x - 16) / 1856f, Math.Max(1, size.y - 78) / 813f);
        ViewComponent.Position = new Vector2i(position.x + 8, position.y - 70);
        ViewComponent.Size = new Vector2i(Mathf.RoundToInt(1856 * scale), Mathf.RoundToInt(813 * scale));
        ViewComponent.TryUpdatePosition();
        if (body?.ViewComponent?.UiTransform != null) body.ViewComponent.UiTransform.localScale = new Vector3(scale, scale, 1f);
    }
    internal static void SetActive(XUiController controller, bool visible, bool allowInput = true)
    {
        var view = controller?.ViewComponent;
        if (view == null) return;
        if (view.IsVisible != visible) view.IsVisible = visible;
        bool enabled = visible && allowInput;
        if (view.Enabled != enabled) view.Enabled = enabled;
        if (view.UiTransform != null && view.UiTransform.gameObject.activeSelf != visible) view.UiTransform.gameObject.SetActive(visible);
    }
    private static void SetText(XUiV_Label label, string value)
    {
        if (label != null && !string.Equals(label.Text, value, StringComparison.Ordinal)) label.SetTextImmediately(value);
    }
}

/// <summary>
/// Full-index view with a bounded visible row range. Scroll changes presentation only; slots
/// keep their original physical indices, and single-slot writes go to the native backend.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthContextGrid : XUiC_ItemStackGrid
{
    public const int ContainerPool = 198, BackpackPool = 176;
    private int Columns => backpack ? 13 : 11;
    private int CellPitch => backpack ? 67 : 80;
    private RebirthContainerSession session;
    private XUiC_RebirthContainerWorkspace workspace;
    private bool backpack, opened, binding, dirty = true;
    private int visibleRows = 9, physical, wheelFrame = -1;
    private float nextSync, pixelOffset, targetPixelOffset;
    private XUiController scrollHost, scrollTrack, scrollThumb, slotsContent;
    private XUiV_Panel slotsViewport;
    private UIPanel nativeClip;
    private bool dragging;
    private bool pagingMode;
    private float dragStartY, dragStartOffset;
    internal bool IsSynchronizing => binding;
    internal bool IntersectsViewport(int slot)
    {
        float top = slot / Columns * CellPitch - pixelOffset;
        return slot >= 0 && slot < physical && top < visibleRows * CellPitch && top + CellPitch > 0f;
    }
    internal bool HasVisibleCenter(int slot)
    {
        float center = slot / Columns * CellPitch - pixelOffset + CellPitch * .5f;
        return slot >= 0 && slot < physical && center > 0f && center < visibleRows * CellPitch;
    }
    internal void Bind(RebirthContainerSession source, XUiC_RebirthContainerWorkspace owner, bool isBackpack)
    {
        session = source; workspace = owner; backpack = isBackpack;
        visibleRows = backpack ? 4 : 9;
    }
    public override void Init()
    {
        base.Init();
        // Crafting's viewport deliberately has no full-area collider over its ItemStacks.
        // Wheel events come from the slot roots and the two scrollbar hit surfaces instead.
        foreach (XUiC_ItemStack slot in itemControllers)
        {
            slot.OnScroll += Scroll;
            var mirror = slot as XUiC_RebirthContextSlot;
            if (mirror != null) mirror.Grid = this;
        }
        slotsContent = GetChildById("contextSlotsContent");
        slotsViewport = GetChildById("contextSlotsViewport")?.ViewComponent as XUiV_Panel;
        scrollHost = GetChildById("contextScrollHost");
        scrollTrack = GetChildById("contextScrollTrack");
        scrollThumb = GetChildById("contextScrollThumb");
        if (scrollTrack != null)
        {
            scrollTrack.OnScroll += Scroll;
            scrollTrack.OnPress += TrackPressed;
        }
        if (scrollThumb != null)
        {
            scrollThumb.OnScroll += Scroll;
            scrollThumb.OnDrag += ThumbDragged;
        }
    }
    public override void OnOpen()
    {
        opened = true; pixelOffset = targetPixelOffset = 0f; nextSync = 0f; dirty = true;
        dragging = false;
        base.OnOpen();
        for (int i = 0; i < itemControllers.Length; ++i)
        {
            itemControllers[i].SlotNumber = i;
            itemControllers[i].SlotChangedEvent -= handleSlotChangedDelegate;
            itemControllers[i].SlotChangedEvent += handleSlotChangedDelegate;
            itemControllers[i].InfoWindow = session?.InfoWindow;
            itemControllers[i].StackLocation = backpack ? XUiC_ItemStack.StackLocationTypes.Backpack : XUiC_ItemStack.StackLocationTypes.LootContainer;
            var mirror = itemControllers[i] as XUiC_RebirthContextSlot;
            mirror?.Configure(backpack);
            mirror?.BindNativeSource(session?.NativeSlot(backpack, i));
        }
        ConfigureViewport();
        Sync();
        ApplyScrollPosition();
    }
    public override void OnClose()
    {
        opened = false;
        foreach (var slot in itemControllers) slot.SlotChangedEvent -= handleSlotChangedDelegate;
        base.OnClose();
        session = null; workspace = null;
    }
    public override ItemStack[] GetSlots() => session?.GetSlots(backpack) ?? new ItemStack[0];
    public override void HandleSlotChangedEvent(int slotNumber, ItemStack stack)
    {
        if (binding || !opened || session == null) return;
        session.WriteSlot(backpack, slotNumber, stack);
        dirty = true;
        workspace?.SourceChanged();
    }
    internal void ChangeLock(XUiC_RebirthContextSlot slot)
    {
        if (!opened || session == null || session.Suspended) return;
        session.SetLock(backpack, slot.SlotNumber, slot.UserLockedSlot);
        dirty = true;
    }
    internal void Inspect(XUiC_RebirthContextSlot slot)
    {
        if (!binding && opened && session?.Suspended == false) workspace?.Select(slot);
    }
    internal void Compare(ItemStack stack) => workspace?.CompareStats(stack);
    internal bool IsLockMode => session != null && session.LockMode(backpack);
    internal void Invalidate() { dirty = true; nextSync = 0f; }
    private int Maximum => Math.Max(0, (physical + Columns - 1) / Columns - visibleRows);
    private float MaxPixelOffset => Maximum * CellPitch;
    private void Scroll(XUiController sender, float amount)
    {
        if (amount == 0f || wheelFrame == Time.frameCount || !CanScroll) return;
        wheelFrame = Time.frameCount;
        SetScrollOffset(RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(targetPixelOffset, MaxPixelOffset, visibleRows * CellPitch, amount > 0 ? -1 : 1) : targetPixelOffset + (amount > 0 ? -CellPitch : CellPitch), false);
    }
    private void SetScrollOffset(float offset, bool immediate)
    {
        if (!CanScroll) return;
        targetPixelOffset = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.SnapAbsolute(offset, MaxPixelOffset, visibleRows * CellPitch, true) : Mathf.Clamp(offset, 0f, MaxPixelOffset);
        if (!immediate && !RebirthScrollbarPagingPolicy.Enabled) return;
        pixelOffset = targetPixelOffset;
        ApplyScrollPosition(); UpdateScrollbar();
    }
    private void AnimateScroll(float dt)
    {
        // Like Crafting, move one content transform inside a native clipped panel.
        // Do NOT enable/disable, recolor, reposition or rebind individual slots on a scroll.
        if (!CanScroll) { targetPixelOffset = pixelOffset; return; }
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (pagingMode != pagingNow) { pagingMode = pagingNow; dragging = false; }
        if (RebirthScrollbarPagingPolicy.Enabled) targetPixelOffset = pixelOffset = RebirthScrollbarPagingPolicy.SnapAbsolute(targetPixelOffset, MaxPixelOffset, visibleRows * CellPitch, true);
        if (Mathf.Abs(pixelOffset - targetPixelOffset) < .05f) pixelOffset = targetPixelOffset;
        else pixelOffset = Mathf.Lerp(pixelOffset, targetPixelOffset, Mathf.Clamp01(dt * 16f));
        ApplyScrollPosition(); UpdateScrollbar();
    }
    private void ConfigureViewport()
    {
        if (slotsViewport == null || slotsContent?.ViewComponent == null) return;
        int width = Columns * CellPitch - 2, height = visibleRows * CellPitch;
        slotsViewport.Size = new Vector2i(width, height);
        slotsViewport.ClippingSize = new Vector2(width, height);
        slotsViewport.ClippingCenter = new Vector2(width * .5f, -height * .5f);
        if (nativeClip == null && slotsViewport.UiTransform != null)
            nativeClip = slotsViewport.UiTransform.GetComponent<UIPanel>();
        if (nativeClip == null) return;
        nativeClip.clipping = UIDrawCall.Clipping.SoftClip;
        nativeClip.clipSoftness = Vector2.zero;
        nativeClip.baseClipRegion = new Vector4(width * .5f, -height * .5f, width, height);
    }
    private void ApplyScrollPosition()
    {
        var view = slotsContent?.ViewComponent;
        if (view == null) return;
        int y = Mathf.RoundToInt(pixelOffset);
        if (view.Position.y != y) { view.Position = new Vector2i(0, y); view.TryUpdatePosition(); }
    }
    public override void Update(float dt)
    {
        if (!opened || session == null || session.Suspended) return;
        if (dirty || Time.realtimeSinceStartup >= nextSync) Sync();
        if (!opened || session == null || session.Suspended) return;
        if (nativeClip == null) ConfigureViewport();
        AnimateScroll(dt);
        base.Update(dt); // Stable physical slots; UIPanel performs clipping, including hit tests.
    }
    private void Sync()
    {
        if (session == null) return;
        ItemStack[] stacks = session.GetSlots(backpack);
        if (stacks == null) return;
        if (stacks.Length > itemControllers.Length)
        {
            // Never silently truncate a changed capacity. Close through the native owner;
            // contents remain untouched and the next open can fall back to native presentation.
            RebirthContextNavigationService.CloseInteraction(); return;
        }
        physical = stacks.Length;
        pixelOffset = Mathf.Clamp(pixelOffset, 0f, MaxPixelOffset);
        targetPixelOffset = Mathf.Clamp(targetPixelOffset, 0f, MaxPixelOffset);
        bool lockMode = session.LockMode(backpack);
        int unencumbered = backpack ? RebirthCraftingInventoryBridge.GetUnencumberedSlotCount(xui) : 0;
        var nativeSlots = session.NativeGrid(backpack).GetItemStackControllers();
        binding = true;
        try
        {
            for (int i = 0; i < itemControllers.Length; ++i)
            {
                var slot = itemControllers[i] as XUiC_RebirthContextSlot;
                if (slot == null) continue;
                bool show = i < physical;
                slot.Present(show, lockMode, i % Columns * CellPitch, -(i / Columns) * CellPitch, CellPitch - 2);
                // Keep the selected physical source stable even when it is scrolled off-screen.
                if (!show) continue;
                ItemStack stack = stacks[i] ?? ItemStack.Empty;
                if (!slot.ItemStack.Equals(stack)) slot.ItemStack = stack;
                if (i < nativeSlots.Length)
                {
                    bool attributeLocked = backpack
                        ? i >= unencumbered
                        : nativeSlots[i].AttributeLock;
                    if (slot.AttributeLock != attributeLocked) slot.AttributeLock = attributeLocked;
                    slot.UserLockedSlot = nativeSlots[i].UserLockedSlot;
                }
            }
        }
        finally { binding = false; }
        UpdateScrollbar();
        dirty = false; IsDirty = false; nextSync = Time.realtimeSinceStartup + .1f;
    }
    private bool CanScroll => opened && session != null && !session.Suspended &&
        !RebirthConsoleInputGuardRuntime.BlocksGameplayInput() && xui.DragAndDropWindow.IsEmpty();

    private void UpdateScrollbar()
    {
        if (scrollHost == null || scrollTrack?.ViewComponent == null || scrollThumb?.ViewComponent == null) return;
        int trackHeight = visibleRows * CellPitch;
        int thumbHeight = RebirthContextScrollGeometry.ThumbHeight(physical, Columns, visibleRows, trackHeight);
        int top = MaxPixelOffset > 0f
            ? Mathf.RoundToInt((trackHeight - thumbHeight) * (pixelOffset / MaxPixelOffset)) : 0;
        XUiC_RebirthContainerWorkspace.SetActive(scrollHost, Maximum > 0);
        if (Maximum == 0) return;
        // One owner of all dimensions, as on Crafting. Do not combine a fixed 100px XUi thumb
        // with UIScrollBar's foreground drawRegion/barSize, which has a different travel range.
        CommitScrollGeometry(scrollTrack, 0, 0, 14, trackHeight);
        CommitScrollGeometry(scrollThumb, 2, -top, 10, thumbHeight);
    }
    private static void CommitScrollGeometry(XUiController controller, int x, int y, int width, int height)
    {
        XUiView view = controller.ViewComponent;
        Vector2i p = view.Position, size = view.Size;
        if (p.x != x || p.y != y) view.Position = new Vector2i(x, y);
        if (size.x != width || size.y != height) view.Size = new Vector2i(width, height);
        if (view.UiTransform == null) return;
        view.TryUpdatePosition();
        // Commit widget AND collider dimensions in the same pass, exactly as Crafting does.
        UIWidget widget = view.UiTransform.GetComponent<UIWidget>();
        if (widget != null)
        {
            if (widget.width != width) widget.width = width;
            if (widget.height != height) widget.height = height;
        }
        BoxCollider collider = view.UiTransform.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.center = new Vector3(width * .5f, -height * .5f, collider.center.z);
            collider.size = new Vector3(width, height, collider.size.z);
        }
    }
    private bool TryPointerY(out float localY)
    {
        localY = 0f;
        Transform host = scrollHost?.ViewComponent?.UiTransform;
        Camera camera = UICamera.currentCamera;
        if (host == null || camera == null) return false;
        Vector2 mouse = UICamera.currentTouch != null ? UICamera.currentTouch.pos : (Vector2)Input.mousePosition;
        Ray ray = camera.ScreenPointToRay(mouse);
        Plane plane = new Plane(host.forward, host.position);
        float distance;
        if (!plane.Raycast(ray, out distance)) return false;
        // Local coordinates include the shared body/HUD scale. Screen-pixel delta would make
        // dragging speed and endpoint accuracy change at different resolutions.
        localY = -host.InverseTransformPoint(ray.GetPoint(distance)).y;
        return true;
    }
    private void TrackPressed(XUiController sender, int button)
    {
        if ((button != -1 && button != 0) || !CanScroll || Maximum == 0) return;
        float y;
        if (!TryPointerY(out y)) return;
        int height = visibleRows * CellPitch;
        int thumb = RebirthContextScrollGeometry.ThumbHeight(physical, Columns, visibleRows, height);
        SetScrollOffset((y - thumb * .5f) * MaxPixelOffset / Math.Max(1, height - thumb), true);
    }
    private void ThumbDragged(XUiController sender, EDragType dragType, Vector2 delta)
    {
        if (!CanScroll || Maximum == 0) { dragging = false; return; }
        float y;
        if (!TryPointerY(out y)) { if (dragType == EDragType.DragEnd) dragging = false; return; }
        if (dragType == EDragType.DragStart)
        {
            dragging = true; dragStartY = y; dragStartOffset = pixelOffset;
        }
        if (!dragging) return;
        int height = visibleRows * CellPitch;
        int thumb = RebirthContextScrollGeometry.ThumbHeight(physical, Columns, visibleRows, height);
        int travel = Math.Max(1, height - thumb);
        SetScrollOffset(dragStartOffset + (y - dragStartY) * MaxPixelOffset / travel, true);
        if (dragType == EDragType.DragEnd) dragging = false;
    }

}

[Preserve]
public sealed class XUiC_RebirthContextSlot : XUiC_ItemStack
{
    internal XUiC_RebirthContextGrid Grid;
    internal bool IsBackpack { get; private set; }
    private bool presented, contextSelected;
    internal bool IsPresented => presented;
    private XUiV_Sprite selectionBorder;
    private XUiController lockOverlay;
    private RebirthSlotPalette palette;
    private RebirthSlotPresentationGate presentation;
    private XUiC_ItemStack nativeSource;
    internal void BindNativeSource(XUiC_ItemStack source) { nativeSource = source; }
    internal void Configure(bool backpack)
    {
        IsBackpack = backpack;
        // The working Crafting inventory explicitly enables native mouse input on every slot.
        // Template instantiation alone is not sufficient ("hover" is not "press").
        ViewComponent.EventOnPress = true;
        ViewComponent.EventOnHover = true;
        ViewComponent.EventOnDrag = true;
        ViewComponent.EventOnScroll = true;
        SetContextSelected(false);
    }
    public override void Init()
    {
        base.Init();
        palette = new RebirthSlotPalette(this);
        Configure(false);
        selectionBorder = GetChildById("background")?.ViewComponent as XUiV_Sprite;
        lockOverlay = GetChildById("rectSlotLock");
        if (lockOverlay != null) lockOverlay.OnPress += (c, button) => Grid?.ChangeLock(this);
    }
    internal void Present(bool show, bool lockMode, int x, int y, int cellSize)
    {
        if (presented && !show) Hovered(false);
        presented = show;
        XUiC_RebirthContainerWorkspace.SetActive(this, show, !lockMode);
        if (show)
        {
            Vector2i p = ViewComponent.Position;
            if (p.x != x || p.y != y) { ViewComponent.Position = new Vector2i(x, y); ViewComponent.TryUpdatePosition(); }
            if (ViewComponent.UiTransform != null)
            {
                float scale = cellSize / 75f;
                Vector3 size = new Vector3(scale, scale, 1f);
                if (ViewComponent.UiTransform.localScale != size) ViewComponent.UiTransform.localScale = size;
            }
            palette?.Apply(AttributeLock);
            // The native lock overlay owns clicks only while editing slot locks.
            XUiC_RebirthContainerWorkspace.SetActive(lockOverlay, lockMode);
        }
    }
    public override bool GetBindingValueInternal(ref string value, string name)
    {
        if (name == "userlockmode") { value = Grid?.IsLockMode == true ? "true" : "false"; return true; }
        if (name == "backgroundcolor") { value = AttributeLock ? RebirthSlotPalette.Locked : RebirthSlotPalette.Normal; return true; }
        if (name == "tooltip")
        {
            string text = RebirthCharacterItemStatsTooltip.Build(this, ItemStack);
            if (text != null) { value = text; return true; }
        }
        return base.GetBindingValueInternal(ref value, name);
    }
    public override void updateItemInfoWindow(XUiC_ItemStack stack)
    {
        // Match Crafting: passive ItemStack/binding refreshes do not choose a new item.
        // HandleClickComplete is the explicit native inspection/transfer completion hook.
    }
    public override void HandleClickComplete()
    {
        base.HandleClickComplete();
        if (presented && Grid?.IsSynchronizing == false) Grid.Inspect(this);
    }
    internal void SetContextSelected(bool selected)
    {
        contextSelected = selected;
        if (!selected && IsSelected) IsSelected = false;
        Selected(selected);
        if (selectionBorder != null)
            selectionBorder.SetColorImmediately(selected ? Color.white : (Color)backgroundColor);
    }
    public override void OnHovered(bool over)
    {
        RebirthCharacterItemStatsTooltip.Hover(this, over);
        base.OnHovered(over);
        Grid?.Compare(over ? ItemStack : ItemStack.Empty);
    }
    public override void Update(float dt)
    {
        if (!presented || !RebirthContextNavigationService.IsContextVisible(xui)) return;
        bool pinned = isOver || IsSelected || IsHolding || IsLocked || IsDragAndDrop || contextSelected;
        if (Grid != null && !Grid.IntersectsViewport(SlotNumber) && !pinned)
        {
            presentation = presentation ?? new RebirthSlotPresentationGate(this);
            if (presentation.Hide()) return;
        }
        bool waking = presentation?.Hidden == true;
        if (waking) IsDirty = true;
        // ItemActionEntryUse reserves the real source slot until its animation returns.
        // Mirror that lock BEFORE native input polling, not only in the 100ms data sync.
        if (nativeSource != null && stackLockType != nativeSource.stackLockType)
        {
            stackLockType = nativeSource.stackLockType;
            IsDirty = true;
        }
        // Process native mouse/controller input every frame for visible slots.
        // Offscreen slots remain clipped by the existing presentation gate.
        base.Update(dt);
        if (waking) RebirthSlotPresentationGate.FlushViews(this);
        palette?.Apply(AttributeLock);
        if (contextSelected && selectionBorder != null && selectionBorder.Color != Color.white)
            selectionBorder.SetColorImmediately(Color.white);
        if (waking) presentation.Show();
    }
    public override void OnClose() { presentation?.Show(); base.OnClose(); }
}

/// <summary>Pure row/thumb arithmetic; the real capacity, never the presentation pool.</summary>
internal static class RebirthContextScrollGeometry
{
    internal static int ThumbHeight(int count, int columns, int visibleRows, int trackHeight)
    {
        int rows = Math.Max(1, (Math.Max(0, count) + columns - 1) / columns);
        if (rows <= visibleRows) return trackHeight;
        return Mathf.Clamp(Mathf.RoundToInt(trackHeight * (visibleRows / (float)rows)), 24, trackHeight);
    }
    internal static int ThumbTop(int firstRow, int maximum, int trackHeight, int thumbHeight)
    {
        if (maximum <= 0) return 0;
        return Mathf.RoundToInt(Math.Max(0, trackHeight - thumbHeight) *
            (Mathf.Clamp(firstRow, 0, maximum) / (float)maximum));
    }
    internal static int RowAt(float thumbTop, int maximum, int trackHeight, int thumbHeight)
    {
        if (maximum <= 0 || trackHeight <= thumbHeight) return 0;
        return Mathf.Clamp(Mathf.RoundToInt(thumbTop * maximum / (trackHeight - thumbHeight)), 0, maximum);
    }
}
