using System;

internal static partial class RebirthNpcStationQueueOriginal
{
    private sealed class SlotPermit
    {
        internal ItemStackGrid Grid;internal int Slot;internal bool Used,Returned,Failed;
        internal Func<bool> Current;
        // null = the debit permit (only an empty stack may be written); otherwise the one exact credit image.
        internal ItemStack Desired;
    }
    [ThreadStatic]private static SlotPermit slotPermit;
    internal static bool BlocksNativeInventory(object target)
    {
        var grid=target as ItemStackGrid??(target as Bag)?.ItemGrid;
        return grid?.holder is EntityRebirthHumanoidNPC npc&&
            ReferenceEquals(grid,npc.bag?.ItemGrid)&&HasPending(npc.RebirthRuntimeState.StableId);
    }
    internal static bool ConsumeGrid(ItemStackGrid grid,int index,ItemStack desired)
    {
        var p=slotPermit;
        if(p==null||p.Used||!ReferenceEquals(grid,p.Grid)||index!=p.Slot||desired==null||
            (p.Desired==null?!desired.IsEmpty():!RebirthStationGridIngredients.IsSameStackSnapshot(p.Desired,desired))||p.Current?.Invoke()!=true)return false;
        p.Used=true;return true;
    }
    internal static void ObserveGrid(ItemStackGrid grid,int index,bool ran,Exception error)
    {
        var p=slotPermit;if(p==null||!p.Used||!ReferenceEquals(grid,p.Grid)||index!=p.Slot)return;
        if(error!=null)p.Failed=true;else if(ran)p.Returned=true;
    }
    private static bool DebitSlot(EntityRebirthHumanoidNPC npc,int index,Func<bool> current)
    {
        if(slotPermit!=null||!current())return false;
        var p=new SlotPermit{Grid=npc.bag.ItemGrid,Slot=index,Current=current};slotPermit=p;
        try{npc.bag.SetSlot(index,ItemStack.Empty.Clone());return p.Used&&p.Returned&&!p.Failed;}
        finally{slotPermit=null;}
    }
    private static bool CreditSlot(EntityRebirthHumanoidNPC npc,int index,ItemStack stack,Func<bool> current)
    {
        if(slotPermit!=null||stack==null||stack.IsEmpty()||!current())return false;
        var p=new SlotPermit{Grid=npc.bag.ItemGrid,Slot=index,Current=current,Desired=stack};slotPermit=p;
        try{npc.bag.SetSlot(index,stack);return p.Used&&p.Returned&&!p.Failed;}
        finally{slotPermit=null;}
    }
}
