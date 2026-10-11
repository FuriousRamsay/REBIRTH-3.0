using System;
using UnityEngine.Scripting;

// Native item-stack presentation/input; only section custody is delegated to its journal.
[Preserve]
public sealed class XUiC_RebirthBackpackSectionNativeSlot : XUiC_ItemStack
{
    private RebirthSlotPalette palette;
    private XUiC_RebirthCharacterOverviewList scroll;
    private RebirthSlotPresentationGate presentation;
    private bool pointerOver;
    private bool storage; private XUiC_RebirthBackpackSectionBackpack backpack;
    public bool IsStorage => storage;
    private XUiC_RebirthBackpackLibrary Library;
    private XUiC_RebirthBackpackSellStash Sale;
    public override void Init()
    {
        base.Init(); palette=new RebirthSlotPalette(this);scroll=GetParentByType<XUiC_RebirthCharacterOverviewList>();
        Library=GetParentByType<XUiC_RebirthBackpackLibrary>();Sale=GetParentByType<XUiC_RebirthBackpackSellStash>();
        backpack=GetParentByType<XUiC_RebirthBackpackSectionBackpack>();storage=backpack==null;
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
    public override void OnClose(){presentation?.Show();pointerOver=false;base.OnClose();}
    public override void updateItemInfoWindow(XUiC_ItemStack item) { }
    public override void HandleClickComplete() { base.HandleClickComplete();Inspect(); }
    public override void OnHovered(bool over) { pointerOver=over;RebirthCharacterItemStatsTooltip.Hover(this,over);base.OnHovered(over); }
    public override bool GetBindingValueInternal(ref string value,string name)
    {
        if(name=="userlockmode"&&backpack!=null){value=backpack.LockMode.ToString();return true;}
        if(name=="tooltip"&&RebirthCharacterItemStatsTooltip.Build(this,ItemStack)!=null){value="";return true;}
        return base.GetBindingValueInternal(ref value,name);
    }
    public override void Update(float dt)
    {
        if(!(Library?.IsOpen==true||Sale?.IsOpen==true)||!ViewComponent.IsVisible)return;
        bool pinned=pointerOver||IsSelected||IsHolding||IsDragAndDrop;
        if(!storage&&scroll!=null&&!scroll.IntersectsDataRow(SlotNumber/13)&&!pinned){presentation=presentation??new RebirthSlotPresentationGate(this);if(presentation.Hide())return;}
        bool waking=presentation?.Hidden==true;if(waking)IsDirty=true;
        base.Update(dt);if(waking)RebirthSlotPresentationGate.FlushViews(this);palette?.Apply(AttributeLock);
        if(waking)presentation.Show();
    }
}
[Preserve]
public sealed class XUiC_RebirthBackpackSectionBackpack : XUiC_Backpack
{
    public bool LockMode {get;private set;}
    private void PersistLocks()=>RebirthCraftingInventoryBridge.PersistLockedSlots(xui,itemControllers,RebirthCraftingInventoryBridge.GetPhysicalSlotCount(xui));
    public void ToggleLocks(){PersistLocks();LockMode=!LockMode;foreach(var slot in itemControllers){slot.RefreshBindings();slot.SetAllChildrenDirty();}}
    public void SortItems(){PersistLocks();xui.PlayerInventory.SortStacks();SetStacks(xui.PlayerInventory.Backpack.ItemGrid.items);}
    public override void Update(float dt){if(LockMode)PersistLocks();base.Update(dt);}
    public override void OnClose(){PersistLocks();LockMode=false;base.OnClose();}
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
