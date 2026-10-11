using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Native ItemStack behavior with a Crafting-owned type identity. Native mouse/controller,
/// drag/drop, split, swap, durability, quality and tooltip behavior remain authoritative.
/// Rebirth owns only the neutral fill palette plus one explicit visual guarantee: the item chosen
/// for Selected Item context keeps the same white native selection border used by the Toolbelt.
/// </summary>
[Preserve]
public class XUiC_RebirthCraftingInventorySlot : XUiC_ItemStack
{




    private XUiV_Sprite selectionBorder;
    private XUiC_RebirthCraftingItemContext itemContext;
    private bool rebirthContextSelected;
    private RebirthSlotPalette palette;
    private XUiC_RebirthCraftingInventoryScroll scroll;
    private RebirthSlotPresentationGate presentation;

    public override void Init()
    {
        base.Init();
        selectionBorder = GetChildById("background")?.ViewComponent as XUiV_Sprite;
        palette = new RebirthSlotPalette(this);

        scroll = GetParentByType<XUiC_RebirthCraftingInventoryScroll>();
        RebirthCraftingPresentation owner = RebirthCraftingPresentation.Resolve(this);
        itemContext = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingItemContext>() : null;
        ReapplyRebirthSlotPalette();
        ReapplySelectedItemBorder();
    }

    private bool pointerOver;

    public override void OnHovered(bool over)
    {
        using var inventoryTiming = RebirthInventoryTiming.Measure(1);
        pointerOver = over;
        RebirthCharacterItemStatsTooltip.Hover(this, over);
        base.OnHovered(over);
        updateBorderColor();
        if (selectionBorder != null) selectionBorder.SetColorImmediately(SelectionBorderColor);
        itemContext?.CompareStats(over ? ItemStack : ItemStack.Empty);
    }
    public override bool GetBindingValueInternal(ref string value, string name)
    {
        if (name == "backgroundcolor") { value = AttributeLock ? RebirthSlotPalette.Locked : RebirthSlotPalette.Normal; return true; }
        if (name == "tooltip" && RebirthCharacterItemStatsTooltip.Build(this,ItemStack) != null) { value=""; return true; }
        return base.GetBindingValueInternal(ref value,name);
    }

    public override void Update(float dt)
    {
        using var inventoryTiming = RebirthInventoryTiming.Measure(0);
        if (ViewComponent?.UiTransform == null || !ViewComponent.UiTransform.gameObject.activeInHierarchy) return;
        bool pinned = pointerOver || IsSelected || IsHolding || IsDragAndDrop || rebirthContextSelected;
        if (scroll != null && !scroll.IntersectsViewport(SlotNumber) && !pinned)
        {
            presentation = presentation ?? new RebirthSlotPresentationGate(this);
            if (presentation.Hide()) return;
        }
        bool waking = presentation?.Hidden == true;
        // Visible slots must process native input and hover changes every frame.
        if (waking) IsDirty = true;
        base.Update(dt);
        if (waking) RebirthSlotPresentationGate.FlushViews(this);
        // Native bindings can restore the stock pale-grey fill after any item/count/lock refresh.
        // Reassert in the same frame so crafting and scrolling cannot produce a light-grey flash.
        ReapplyRebirthSlotPalette();
        // Run after the native ItemStack border update. Only the one slot owned by Selected Item
        // context is overridden; every unselected/hover/holding border remains native-authoritative.
        ReapplySelectedItemBorder();
        if (waking) presentation.Show();
    }

    internal void FinishViewport()
    {
        if (presentation?.Hidden == true && scroll?.IntersectsViewport(SlotNumber) == true) Update(0f);
    }

    public override void OnClose() { presentation?.Show(); base.OnClose(); }

    internal bool ProjectedAttributeLock; public void ReapplyRebirthSlotPalette() { if (AttributeLock != ProjectedAttributeLock) AttributeLock = ProjectedAttributeLock; palette?.Apply(AttributeLock); }

    private void ReapplySelectedItemBorder()
    {
        // Rebirth owns one explicit Selected Item border. Do not infer it from XUi.currentSelectedEntry:
        // recipe rows and other selectable controls legitimately change that global selection.
        if (!rebirthContextSelected)
            return;
        if (selectionBorder == null)
            selectionBorder = GetChildById("background")?.ViewComponent as XUiV_Sprite;
        if (selectionBorder != null && selectionBorder.Color != Color.white)
            selectionBorder.SetColorImmediately(Color.white);
    }

    public void SetRebirthContextSelected(bool selected)
    {
        if (rebirthContextSelected == selected)
        {
            if (selected) ReapplySelectedItemBorder();
            return;
        }

        rebirthContextSelected = selected;
        if (selectionBorder == null)
            selectionBorder = GetChildById("background")?.ViewComponent as XUiV_Sprite;
        if (selectionBorder == null)
            return;

        if (selected)
        {
            selectionBorder.SetColorImmediately(Color.white);
        }
        else
        {
            // Clear the old white selection immediately. Native ItemStack hover/holding code is free
            // to replace this normal border on the following base.Update pass.
            selectionBorder.SetColorImmediately(backgroundColor);
        }
    }

    public override void updateItemInfoWindow(XUiC_ItemStack itemStack)
    {
        // Personal Crafting never delegates selected-slot refreshes to XUiC_ItemInfoWindow.
        // The sibling Rebirth item-context controller polls the authoritative slot instead.
    }

    public override void SwapItem()
    {
        base.SwapItem();
        var held=xui?.DragAndDropWindow;
        if(held!=null && !held.IsEmpty()) itemContext?.SelectSlot(held.ItemStackControl);
    }
    public override void HandleClickComplete()
    {
        using var inventoryTiming = RebirthInventoryTiming.Measure(2);
        base.HandleClickComplete();

        RebirthCraftingPresentation owner = RebirthCraftingPresentation.Resolve(this);
        XUiC_RebirthCraftingItemContext context = owner != null
            ? owner.GetChildByType<XUiC_RebirthCraftingItemContext>()
            : null;
        if (context == null)
            return;

        if (xui?.DragAndDropWindow?.IsEmpty() == false)
            context.SelectSlot(xui.DragAndDropWindow.ItemStackControl);
        else if (ItemStack != null && !ItemStack.IsEmpty())
            context.SelectSlot(this);
        else
            context.ClearSelection();
    }
}
