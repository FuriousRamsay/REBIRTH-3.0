using System;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthEditorBackpack : XUiC_Backpack
{
    private XUiC_RebirthCharacterOverviewList scroll;
    private bool open, filtered, lockMode;
    private XUiC_ItemStack selected;
    private float nextInspectionRefresh;
    private XUiV_Sprite inspectedIcon;
    private XUiV_Label inspectedName, inspectedDescription;
    private XUiC_SimpleButton modifyButton;
    private XUiView modifyIcon;
    public XUiC_ItemStack SelectedItem => selected;
    public override void Init() {
        base.Init(); scroll = GetChildByType<XUiC_RebirthCharacterOverviewList>();
        inspectedIcon = GetChildById("editorBagItemIcon")?.ViewComponent as XUiV_Sprite;
        inspectedName = GetChildById("editorBagItemName")?.ViewComponent as XUiV_Label;
        inspectedDescription = GetChildById("editorBagItemDescription")?.ViewComponent as XUiV_Label;
        modifyButton = GetChildById("editorBagModify") as XUiC_SimpleButton;
        modifyIcon = GetChildById("editorModifyIcon")?.ViewComponent;
        GetChildById("editorBagFilter").OnPress += (sender, button) => {
            if (!CanOperate(button)) return;
            filtered = !filtered; SetStacks(GetSlots()); scroll?.ResetPosition();
            (sender.ViewComponent as XUiV_Sprite)?.SetColorImmediately(filtered ? new UnityEngine.Color32(220,195,120,255) : new UnityEngine.Color32(255,255,255,255));
        };
        GetChildById("editorBagSort").OnPress += (sender, button) => {
            if (!CanOperate(button)) return;
            PersistLocks();
            var bag = RebirthCraftingInventoryBridge.GetBag(xui);
            var ignored = new PackedBoolArray(bag.ItemGrid.items.Length);
            for (int i = 0; i < bag.ItemGrid.items.Length; i++)
                ignored[i] = (bag.LockedSlots != null && i < bag.LockedSlots.Length && bag.LockedSlots[i]) || (i < itemControllers.Length && itemControllers[i] != null && itemControllers[i].AssembleLock);
            var originalLocks = bag.LockedSlots;
            try { bag.ItemGrid.SetSlotLocks(ignored); xui.PlayerInventory.SortStacks(); }
            finally { bag.ItemGrid.SetSlotLocks(originalLocks); }
            SetStacks(GetSlots());
        };
        GetChildById("editorBagLock").OnPress += (sender, button) => {
            if (!CanOperate(button)) return;
            PersistLocks(); lockMode = !lockMode; RefreshBindings();
            foreach (var slot in itemControllers) { slot.RefreshBindings(); slot.SetAllChildrenDirty(); }
            SetStacks(GetSlots());
            (sender.ViewComponent as XUiV_Sprite)?.SetColorImmediately(lockMode ? new UnityEngine.Color32(220,195,120,255) : new UnityEngine.Color32(255,255,255,255));
        };
        ((XUiC_SimpleButton)GetChildById("editorBagModify")).OnPressed += (sender, button) => {
            if (!CanModify()) return;
            selected.InfoWindow = RebirthInventoryInfoWindowLookup.Find(xui);
            var action = new ItemActionEntryAssemble(selected);
            action.RefreshEnabled();
            if (action.Enabled) { action.OnActivated(); SetStacks(RebirthCraftingInventoryBridge.GetBag(xui).ItemGrid.items); }
        };
    }
    private bool CanOperate(int button) => open && (button == 0 || button == -1)
        && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput() && xui.DragAndDropWindow.IsEmpty();

    private void PersistLocks() => RebirthCraftingInventoryBridge.PersistLockedSlots(xui, itemControllers,
        Math.Min(GetSlots()?.Length ?? 0, itemControllers?.Length ?? 0));
    public override bool GetBindingValueInternal(ref string value, string name) {
        if (name == "userlockmode") { value = lockMode.ToString(); return true; }
        return base.GetBindingValueInternal(ref value, name);
    }
    private bool CanModify() {
        var value = selected?.ItemStack?.itemValue;
        return selected != null && !selected.ItemStack.IsEmpty() && !selected.AssembleLock &&
            (value.ModificationCount != 0 || value.CosmeticModCount != 0);
    }
    public override void OnOpen() { open = true; nextInspectionRefresh = 0f; base.OnOpen(); }
    public override void OnClose() { PersistLocks(); lockMode = false; open = false; base.OnClose(); }
    public override void Update(float dt) {
        if (!open) return;
        base.Update(dt);
        if (lockMode) PersistLocks();
        if (selected != null && UnityEngine.Time.realtimeSinceStartup >= nextInspectionRefresh) Inspect(selected);
        bool canModify = CanModify();
        if (modifyButton != null) { modifyButton.ViewComponent.IsVisible = canModify; modifyButton.Enabled = canModify; }
        if (modifyIcon != null) modifyIcon.IsVisible = canModify;
        RebirthSelectedDurability.Render(this, "editorBagDurability", selected);
    }
    public override void SetStacks(ItemStack[] stacks)
    {
        if (stacks == null) return;
        nextInspectionRefresh = 0f;
        if (lockMode) PersistLocks();
        base.SetStacks(stacks);
        int visible = 0;
        var source = xui.AssembleItem.CurrentItemStackController;
        for (int i = 0; i < itemControllers.Length; i++)
        {
            itemControllers[i].ViewComponent.IsVisible = i < stacks.Length;
            bool show = i < stacks.Length && (!filtered || stacks[i]?.itemValue?.ItemClass is ItemClassModifier);
            var wrapper = GetChildById("editorCell" + i).ViewComponent;
            wrapper.IsVisible = show;
            if (wrapper.UiTransform != null) wrapper.UiTransform.gameObject.SetActive(show);
            if (show) {
                wrapper.Position = new Vector2i(visible % 10 * 78, (i / 10 - visible / 10) * 78);
                wrapper.TryUpdatePosition(); visible++;
            }
            var lockOverlay = itemControllers[i].GetChildById("rectSlotLock")?.ViewComponent;
            if (lockOverlay != null) lockOverlay.IsVisible = show && lockMode;
            // The source lives in the closed Character/Crafting surface. Mirror its native
            // assembly lock here so that editing cannot move the source out from under it.
            bool assemblyLocked = source != null &&
                source.StackLocation == XUiC_ItemStack.StackLocationTypes.Backpack && source.SlotNumber == i;
            if (assemblyLocked || itemControllers[i].AssembleLock)
                itemControllers[i].AssembleLock = assemblyLocked;
        }
        RebirthCraftingInventoryBridge.ApplyLockedSlots(xui, itemControllers, Math.Min(stacks.Length, itemControllers.Length));
        scroll?.SetItemCount((visible + 9) / 10, "");
        (GetChildById("editorBagCapacity").ViewComponent as XUiV_Label)?.SetTextImmediately(
            RebirthCraftingInventoryBridge.GetUsedSlotCount(xui) + "/" + stacks.Length + " | " +
            RebirthCraftingInventoryBridge.GetEncumberedUsedSlotCount(xui) + " ENCUMBERED");
    }
    public override void HandleSlotChangedEvent(int index, ItemStack stack)
    {
        var bag = RebirthCraftingInventoryBridge.GetBag(xui);
        if (bag != null && index >= 0 && index < bag.ItemGrid.items.Length)
            bag.SetSlot(index, stack?.Clone() ?? ItemStack.Empty.Clone());
    }
    public void Inspect(XUiC_ItemStack slot)
    {
        selected = slot;
        nextInspectionRefresh = UnityEngine.Time.realtimeSinceStartup + .25f;
        var stack = slot?.ItemStack;
        bool hasItem = stack != null && !stack.IsEmpty() && stack.itemValue?.ItemClass != null;
        if (inspectedIcon != null)
        {
            inspectedIcon.IsVisible = hasItem;
            if (hasItem)
            {
                string sprite = stack.itemValue.GetPropertyOverride("CustomIcon", stack.itemValue.ItemClass.GetIconName());
                if (inspectedIcon.SpriteName != sprite) inspectedIcon.SpriteName = sprite;
            }
        }
        SetInspectionText(inspectedName, hasItem ? stack.itemValue.ItemClass.GetLocalizedItemName() : "");
        SetInspectionText(inspectedDescription, XUiC_RebirthCraftingItemContext.ResolveDescription(stack, xui));
    }
    private static void SetInspectionText(XUiV_Label label, string text)
    {
        if (label != null && !string.Equals(label.Text, text, StringComparison.Ordinal))
            label.SetTextImmediately(text);
    }
}

[Preserve]
public sealed class XUiC_RebirthEditorBackpackSlot : XUiC_ItemStack
{
    private XUiV_Sprite slotBackground, slotHighlight;
    private static readonly UnityEngine.Color32 NormalColor = new UnityEngine.Color32(30,30,36,255);
    private static readonly UnityEngine.Color32 LockedColor = new UnityEngine.Color32(31,31,36,255);
    private static readonly UnityEngine.Color32 AvailableColor = new UnityEngine.Color32(58,58,65,255);
    public override void Init()
    {
        base.Init();
        slotBackground = GetChildById("backgroundMain")?.ViewComponent as XUiV_Sprite;
        slotHighlight = GetChildById("highlightOverlay")?.ViewComponent as XUiV_Sprite;
    }

    public override bool GetBindingValueInternal(ref string value, string name)
    {
        if (name == "tooltip" && RebirthCharacterItemStatsTooltip.Build(this,ItemStack) != null) { value=""; return true; }
        return base.GetBindingValueInternal(ref value,name);
    }
    public override void updateItemInfoWindow(XUiC_ItemStack stack) { }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (slotBackground != null && slotBackground.Color != (UnityEngine.Color)NormalColor)
            slotBackground.SetColorImmediately(NormalColor);
        UnityEngine.Color color = AttributeLock ? (UnityEngine.Color)LockedColor : (UnityEngine.Color)AvailableColor;
        if (slotHighlight != null && slotHighlight.Color != color)
            slotHighlight.SetColorImmediately(color);
    }
    public override void OnHovered(bool over)
    {
        RebirthCharacterItemStatsTooltip.Hover(this, over);
        base.OnHovered(over);
        var stats = GetParentByType<XUiC_RebirthItemEditorHeader>()?.GetChildByType<XUiC_RebirthAssembleItem>();
        stats?.Compare(over ? ItemStack : ItemStack.Empty);
    }
    public override void HandleClickComplete()
    {
        base.HandleClickComplete();
        GetParentByType<XUiC_RebirthEditorBackpack>()?.Inspect(this);
    }
}

[Preserve]
public sealed class XUiC_RebirthAssembleItem : XUiC_AssembleWindow
{
    private static string Number(float value)=>value.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture);
    private static string Pair(float normal,float power)=>Number(normal)+" ("+Number(power)+")";
    private ItemStack comparison = ItemStack.Empty;
    public void Compare(ItemStack other) { comparison = other ?? ItemStack.Empty; IsDirty = true; }

    public override bool GetBindingValueInternal(ref string value, string name)
    {
        bool titleBinding=name.StartsWith("itemstattitle",StringComparison.Ordinal);
        bool valueBinding=name.StartsWith("itemstat",StringComparison.Ordinal)&&!titleBinding;
        if((titleBinding||valueBinding)&&int.TryParse(name.Substring(titleBinding?13:8),out int index)&&
            RebirthWeaponDetailRows.TryGet(xui,ItemStack,comparison,index-1,out var title,out var text))
        {value=titleBinding?title:text;return true;}        if (name == "editorDescription") { value = XUiC_RebirthCraftingItemContext.ResolveDescription(ItemStack, xui); return true; }
        if (name == "editorHasQuality") { value = (ItemStack?.itemValue?.ItemClass?.ShowQualityBar == true).ToString(); return true; }
        if (name == "editorQualityFill")
        {
            var item = ItemStack?.itemValue;
            value = item == null ? "0" : item.MaxUseTimes == 0 ? "1" :
                ((item.MaxUseTimes - item.UseTimes) / item.MaxUseTimesUI).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        bool found = base.GetBindingValueInternal(ref value, name);
        if (found && name.StartsWith("itemstat") && int.TryParse(name.Substring(8), out int statIndex))
            value = RebirthItemStatColors.Format(value);
        return found;
    }
}

[Preserve]
public sealed class XUiC_RebirthEditorPartSlot : XUiC_ItemPartStack
{
    public override void Update(float dt)
    {
        base.Update(dt);
        (GetChildById("background")?.ViewComponent as XUiV_Sprite)?.SetColorImmediately(new UnityEngine.Color32(35,35,43,245));
    }
}

[Preserve]
public sealed class XUiC_RebirthEditorCosmeticSlot : XUiC_ItemCosmeticStack
{
    public override void Update(float dt)
    {
        base.Update(dt);
        (GetChildById("background")?.ViewComponent as XUiV_Sprite)?.SetColorImmediately(new UnityEngine.Color32(35,35,43,245));
    }
}
