using System;
using UnityEngine.Scripting;

// Native item-stack presentation/input; only section custody is delegated to its journal.
[Preserve]
public sealed class XUiC_RebirthBackpackSectionNativeSlot : XUiC_ItemStack
{
    private RebirthSlotPalette palette;
    private bool storage;
    public bool IsStorage => storage;
    private XUiC_RebirthBackpackLibrary Library;
    private XUiC_RebirthBackpackSellStash Sale;
    public override void Init()
    {
        base.Init(); palette=new RebirthSlotPalette(this);
        Library=GetParentByType<XUiC_RebirthBackpackLibrary>();Sale=GetParentByType<XUiC_RebirthBackpackSellStash>();
        storage=GetParentByType<XUiC_RebirthBackpackSectionBackpack>()==null;
        ViewComponent.EventOnPress=true;ViewComponent.EventOnHover=true;ViewComponent.EventOnDrag=true;
        ViewComponent.EventOnScroll=true;
        if(IsStorage) StackLocation=StackLocationTypes.LootContainer;
    }
    public void Inspect()
    {
        Library?.NativeSelect(this, !IsStorage); Sale?.NativeSelect(this, !IsStorage);
    }
    public bool Dispatch(string method)
    {
        bool quick=method=="HandleMoveToPreferredLocation";
        if(!IsStorage&&!quick)return false;
        Library?.NativeTransfer(this,!IsStorage,method=="HandlePartialStackPickup",method=="HandleDropOne",quick);
        Sale?.NativeTransfer(this,!IsStorage,method=="HandlePartialStackPickup",method=="HandleDropOne",quick);
        return true;
    }
    public override void updateItemInfoWindow(XUiC_ItemStack item) { }
    public override void HandleClickComplete() { base.HandleClickComplete();Inspect(); }
    public override void OnHovered(bool over) { RebirthCharacterItemStatsTooltip.Hover(this,over);base.OnHovered(over); }
    public override void Update(float dt)
    {
        if(!(Library?.IsOpen==true||Sale?.IsOpen==true)||!ViewComponent.IsVisible)return;
        base.Update(dt);palette?.Apply(AttributeLock);
    }
}
[Preserve]
public sealed class XUiC_RebirthBackpackSectionBackpack : XUiC_Backpack
{
    public override void HandleSlotChangedEvent(int index,ItemStack stack)
    {
        var bag=RebirthCraftingInventoryBridge.GetBag(xui);
        if(bag!=null&&index>=0&&index<bag.ItemGrid.items.Length)bag.SetSlot(index,stack?.Clone()??ItemStack.Empty.Clone());
    }
    public override void SetStacks(ItemStack[] stacks)
    {
        if(stacks==null)return;
        base.SetStacks(stacks);
        for(int i=0;i<itemControllers.Length;i++)
            if(itemControllers[i]?.ViewComponent!=null)itemControllers[i].ViewComponent.IsVisible=i<stacks.Length;
        RebirthCraftingInventoryBridge.ApplyLockedSlots(xui,itemControllers,Math.Min(stacks.Length,itemControllers.Length));
    }
}
public sealed class RebirthBackpackSectionTakeEntry : BaseItemActionEntry
{
    public RebirthBackpackSectionTakeEntry(XUiC_RebirthBackpackSectionNativeSlot slot)
        :base(slot,"lblContextActionTake","ui_game_symbol_hand",GamepadShortCut.DPadUp) { }
    public override void RefreshEnabled()
    {
        var slot=ItemController as XUiC_RebirthBackpackSectionNativeSlot;
        Enabled=slot!=null&&!slot.ItemStack.IsEmpty()&&slot.xui.DragAndDropWindow.IsEmpty()&&
            !RebirthBackpackLibraryReservation.IsHeld(slot.xui.playerUI.entityPlayer);
    }
    public override void OnActivated(){RefreshEnabled();if(Enabled)((XUiC_RebirthBackpackSectionNativeSlot)ItemController).Dispatch("HandleStackSwap");}
}
public sealed class RebirthBackpackSectionReadEntry : BaseItemActionEntry
{
    private readonly Action read;
    public RebirthBackpackSectionReadEntry(XUiController slot,Action action)
        :base(slot,"lblContextActionUse","ui_game_symbol_book",GamepadShortCut.DPadRight){read=action;}
    public override void RefreshEnabled(){Enabled=!RebirthBackpackLibraryReservation.IsHeld(ItemController.xui.playerUI.entityPlayer)&&ItemController.xui.DragAndDropWindow.IsEmpty();}
    public override void OnActivated(){RefreshEnabled();if(Enabled)read();}
}