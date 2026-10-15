using System;

// Detached prediction of native XUiM_PlayerInventory.AddItem(count1) only.
// No item setters, AddItem calls, receipts, save writes or drop fallback.
internal static class RebirthVehicleCursorReturnPlan
{
    internal static bool TryPlan(EntityPlayerLocal owner,ItemStack cursor,out ItemStack[] afterBag,out ItemStack[] afterBelt)
    {
        afterBag=afterBelt=null;
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||owner==null||world==null||!ReferenceEquals(owner.world,world)||
            !ReferenceEquals(world?.GetPrimaryPlayer(),owner)||!ReferenceEquals(world.GetEntity(owner.entityId),owner)||
            cursor?.itemValue==null||cursor.count!=1||cursor.IsEmpty()||cursor.itemValue.ItemClass==null||
            cursor.itemValue.ItemClass is ItemClassArmor||
            RebirthToolbeltCapacity.IsRejectedIncoming(cursor)||
            cursor.itemValue.ItemClass.GetItemName()=="resourceRockSmall"||
            !cursor.CanMoveTo(XUiC_ItemStack.StackLocationTypes.ToolBelt)||
            RebirthGearOwnerReservation.IsHeld(owner)||RebirthBackpackLibraryReservation.IsHeld(owner))return false;
        var bag=owner.bag;var belt=owner.inventory;
        var ui=owner.PlayerUI?.xui?.PlayerInventory;
        if(bag?.ItemGrid?.items==null||belt?.ItemGrid?.items==null||
            !ReferenceEquals(ui?.Backpack,bag)||!ReferenceEquals(ui.Toolbelt,belt))return false;
        int owned=RebirthToolbeltCapacity.GetOwnedSlotCount(owner,belt.Length);
        if(owned<=0||owned>belt.ItemGrid.items.Length)return false;
        try
        {
            var bags=Clone(bag.ItemGrid.items);var belts=Clone(belt.ItemGrid.items);
            var incoming=cursor.Clone();
            bool moved=false;
            // These predicates are detached native ItemStack predicates. Native
            // Bag CanStackNoEmpty/CanStack ignore sort locks; do not invent them.
            if(NoEmpty(bags,incoming))moved=Partial(bags,incoming,null);
            else if(NoEmpty(belts,incoming))moved=Partial(belts,incoming,i=>i<owned&&belt.CanMoveToSlot(incoming,i));
            if(!moved)
            {
                if(CanStack(bags,incoming,null))moved=Partial(bags,incoming,null);
                else if(CanStack(belts,incoming,i=>belt.CanMoveToSlot(incoming,i)))
                    moved=Partial(belts,incoming,i=>i<owned&&belt.CanMoveToSlot(incoming,i));
            }
            if(!moved)
            {
                // Native ReturnItem considers preferred belt slots before empty bag.
                for(int start=0;start<belt.Length;start++)
                {
                    int index=belt.FindPreferredSlot(incoming.itemValue.type,start);
                    if(index<0||index>=belt.Length)break;
                    start=index;
                    if(index>=owned)continue;
                    if(belts[index].itemValue.type==incoming.itemValue.type&&belts[index].CanStackWith(incoming))
                    {belts[index].count+=incoming.count;moved=true;break;}
                    if(belts[index].IsEmpty()&&belt.CanMoveToSlot(incoming,index))
                    {belts[index]=incoming.Clone();moved=true;break;}
                }
                if(!moved&&incoming.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack))
                    moved=Add(bags,incoming,bags.Length,false);
                // Inventory.AddItem checks the destination type but deliberately
                // does not call CanMoveToSlot, even for empty slots. Capacity's
                // AddItem prefix only constrains the range/rejected incoming item.
                if(!moved)moved=Add(belts,incoming,owned,true);
            }
            if(!moved)return false; // native would reach DropItem; never admit that path
            afterBag=bags;afterBelt=belts;return true;
        }
        catch{return false;}
    }
    private static ItemStack[] Clone(ItemStack[] source)
    {var copy=new ItemStack[source.Length];for(int i=0;i<copy.Length;i++){if(source[i]?.itemValue==null||source[i].count<0)throw new InvalidOperationException();copy[i]=source[i].Clone();}return copy;}
    private static bool NoEmpty(ItemStack[] slots,ItemStack incoming)
    {foreach(var slot in slots)if(slot.CanStackPartlyWith(incoming,out _))return true;return false;}
    private static bool CanStack(ItemStack[] slots,ItemStack incoming,Func<int,bool> allowed)
    {for(int i=0;i<slots.Length;i++)if((allowed==null||allowed(i))&&(slots[i].IsEmpty()||slots[i].CanStackWith(incoming)))return true;return false;}
    private static bool Partial(ItemStack[] slots,ItemStack incoming,Func<int,bool> allowed)
    {
        if(allowed==null&&!incoming.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack))return false;
        for(int i=0;i<slots.Length;i++)
        {
            if(allowed!=null&&!allowed(i))continue;
            int amount=incoming.count;
            if(slots[i].itemValue.type==incoming.itemValue.type&&(allowed==null||!slots[i].IsEmpty())&&
                slots[i].CanStackPartly(ref amount)&&amount==1)
            {slots[i].count++;incoming.count--;return true;}
        }
        return false;
    }
    private static bool Add(ItemStack[] slots,ItemStack incoming,int maximum,bool toolbelt)
    {
        for(int i=0;i<maximum;i++)if((!toolbelt||slots[i].itemValue.type==incoming.itemValue.type)&&slots[i].CanStackWith(incoming))
        {slots[i].count+=incoming.count;return true;}
        for(int i=0;i<maximum;i++)if(slots[i].IsEmpty())
        {slots[i]=incoming.Clone();return true;}
        return false;
    }
}
