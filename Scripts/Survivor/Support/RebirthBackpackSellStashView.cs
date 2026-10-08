using System;

// Owner-only display projection. It grants no inventory custody or transfer authority.
public sealed class RebirthBackpackSellStashView
{
    public string CreationId {get;private set;}
    public long GearRevision {get;private set;}
    public string BackpackItemId {get;private set;}
    public bool TransferPending {get;private set;}
    public int Capacity=>slots.Length;
    public int OccupiedSlots {get;private set;}
    private readonly ItemStack[] slots;
    private RebirthBackpackSellStashView(string creation,long revision,string itemId,bool pending,ItemStack[] contents)
    {
        CreationId=creation;GearRevision=revision;BackpackItemId=itemId;TransferPending=pending;
        slots=new ItemStack[contents.Length];
        for(int i=0;i<slots.Length;i++){slots[i]=contents[i].Clone();if(!slots[i].IsEmpty())OccupiedSlots++;}
    }
    public bool TryGetSlot(int index,out ItemStack stack)
    {
        stack=null;if(index<0||index>=slots.Length)return false;
        stack=slots[index].Clone();return true;
    }
    // Scalar presentation fields avoid cloning full native ItemValues on every UI refresh.
    public bool TryGetDisplaySlot(int index,out string itemId,out int count)
    {
        itemId=string.Empty;count=0;if(index<0||index>=slots.Length)return false;
        var slot=slots[index];if(slot.IsEmpty())return true;
        itemId=slot.itemValue.ItemClass.GetItemName();count=slot.count;return true;
    }
    public static bool TryCreate(Guid creation,long revision,ItemValue backpack,bool pending,out RebirthBackpackSellStashView view)
        =>TryCreate(creation.ToString("N"),revision,backpack,pending,out view);
    public static bool TryCreate(string creation,long revision,ItemValue backpack,bool pending,out RebirthBackpackSellStashView view)
    {
        view=null;
        if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||revision<0||backpack?.ItemClass==null||
            !RebirthBackpackSellStashContents.TryRead(backpack,out var contents)||contents==null||
            contents.Length<=0||contents.Length>RebirthBackpackSellStashPolicy.MaxSlots)return false;
        return TryCreateDisplay(normalized,revision,backpack.ItemClass.GetItemName(),pending,contents,out view);
    }
    public static bool TryCreateDisplay(Guid creation,long revision,string itemId,bool pending,ItemStack[] contents,out RebirthBackpackSellStashView view)
        =>TryCreateDisplay(creation.ToString("N"),revision,itemId,pending,contents,out view);
    public static bool TryCreateDisplay(string creation,long revision,string itemId,bool pending,ItemStack[] contents,out RebirthBackpackSellStashView view)
    {
        view=null;
        if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||revision<0||contents==null||contents.Length<=0||contents.Length>RebirthBackpackSellStashPolicy.MaxSlots||
            RebirthBackpackSellStashPolicy.CapacityForBackpack(itemId)!=contents.Length)return false;
        foreach(var stack in contents)
            if(stack==null||stack.count<0||stack.count>ushort.MaxValue||stack.count>0&&
                (stack.IsEmpty()||stack.itemValue?.ItemClass==null||stack.count>stack.itemValue.ItemClass.MaxCount||
                 !RebirthBackpackSellStashPolicy.IsStorableItem(stack.itemValue)))return false;
        try{view=new RebirthBackpackSellStashView(normalized,revision,itemId,pending,contents);return true;}
        catch{return false;}
    }
}