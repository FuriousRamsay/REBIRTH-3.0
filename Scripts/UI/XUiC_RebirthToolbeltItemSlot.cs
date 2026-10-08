using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthToolbeltItemSlot : XUiC_ItemStack
{
    // The slot number binding is read for every slot on every refresh; avoid a new string each time.
    public override void Update(float dt) { using var timing = RebirthInventoryTiming.Measure(6); base.Update(dt); }
    private static readonly string[] SlotNumberText = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" };

    public override bool GetBindingValueInternal(ref string value, string name) {
        if(name=="buckleSelected"){value=(IsHolding||IsSelected)?"true":"false";return true;}
        if(name=="buckleSlotNumber"){int n=SlotNumber+1;value=n>=0&&n<SlotNumberText.Length?SlotNumberText[n]:n.ToString();return true;}
        if (name == "tooltip") {
            var stats = RebirthCharacterItemStatsTooltip.Build(this, ItemStack);
            if (stats != null) { value = stats; return true; }
        }
        return base.GetBindingValueInternal(ref value, name);
    }

    private XUiC_RebirthItemEditorHeader Editor {
        get { var owner = XUiC_RebirthItemEditorHeader.ActiveInstance;
            return owner != null && owner.xui == xui && owner.IsEditorOpen ? owner : null; }
    }
    private XUiC_RebirthPersonalCrafting contextOwner;
    private XUiC_RebirthCraftingItemContext cachedContext;
    private XUiC_RebirthItemEditorHeader editorOwner;
    private XUiC_RebirthEditorBackpack cachedEditorBag;
    private XUiC_RebirthAssembleItem cachedAssembly;
    private XUiC_RebirthSurvivorCharacter characterOwner;
    private XUiC_RebirthCharacterBackpack cachedCharacterBag;
    private XUiC_RebirthCraftingItemContext Context {
        get { var owner = XUiC_RebirthPersonalCrafting.ActiveInstance;
            if (owner == null || owner.xui != xui || !owner.State.IsOpen) return null;
            if (!ReferenceEquals(contextOwner, owner)) { contextOwner=owner; cachedContext=owner.GetChildByType<XUiC_RebirthCraftingItemContext>(); }
            return cachedContext; }
    }
    private bool RouteSelection(bool refresh) {
        var rewards = XUiC_RebirthQuestTurnInWorkspace.ActiveInstance;
        if (rewards != null && rewards.xui == xui && rewards.windowGroup.isShowing && rewards.Details != null)
        {
            rewards.InspectToolbelt(this, refresh);
            return true;
        }
        var editor = Editor;
        if (!ReferenceEquals(editorOwner,editor)) { editorOwner=editor; cachedEditorBag=editor?.GetChildByType<XUiC_RebirthEditorBackpack>(); cachedAssembly=editor?.GetChildByType<XUiC_RebirthAssembleItem>(); }
        var editorBag = cachedEditorBag;
        if (editorBag != null) {
            if (refresh || editorBag.SelectedItem != this) editorBag.Inspect(this);
            return true;
        }
        var character = XUiC_RebirthSurvivorCharacter.ActiveInstance;
        if (character != null && character.xui == xui && character.IsCharacterWindowOpen) {
            if (!ReferenceEquals(characterOwner,character)) { characterOwner=character; cachedCharacterBag=character.GetChildByType<XUiC_RebirthCharacterBackpack>(); }
            var bag = cachedCharacterBag;
            if (bag != null) {
                if (refresh || bag.SelectedItem != this) bag.SelectItem(this);
                return true;
            }
        }
        if (RebirthContextNavigationService.IsContextVisible(xui))
        {
            var workspace = RebirthContextNavigationService.Session?.Workspace;
            if (workspace != null)
            {
                if (refresh || workspace.SelectedItem != this) workspace.SelectItem(this);
                return true;
            }
        }
        var context = Context;
        if (context == null) return false;
        if (refresh || context.SelectedSlot != this) context.SelectSlot(this);
        return true;
    }
    public override void HandleClickComplete() {
        using var inventoryTiming = RebirthInventoryTiming.Measure(8); base.HandleClickComplete(); RouteSelection(true); }
    public override void updateItemInfoWindow(XUiC_ItemStack stack) {
        if (!RouteSelection(false)) base.updateItemInfoWindow(stack);
    }
    public override void OnHovered(bool over) {
        using var inventoryTiming = RebirthInventoryTiming.Measure(7);
        RebirthCharacterItemStatsTooltip.Hover(this, over);

        base.OnHovered(over);
        var comparison = over ? ItemStack : ItemStack.Empty;
        Context?.CompareStats(comparison);
        if (RebirthContextNavigationService.IsContextVisible(xui))
            RebirthContextNavigationService.Session?.Workspace?.CompareStats(comparison);
        if (!ReferenceEquals(editorOwner,Editor)) { editorOwner=Editor; cachedEditorBag=editorOwner?.GetChildByType<XUiC_RebirthEditorBackpack>(); cachedAssembly=editorOwner?.GetChildByType<XUiC_RebirthAssembleItem>(); }
        cachedAssembly?.Compare(comparison);
    }
}
