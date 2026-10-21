using System;
using System.Collections.Generic;

// Detached prediction of the installed XUiM_PlayerInventory.AddItemNoPartial /
// AddItem ordering. This is capacity evidence only; it never invokes a setter,
// equipment event, collection callback or payment operation.
internal sealed class RebirthRecipeOutputInventoryPrediction
{
    internal readonly ItemStack[] Bag,Belt;
    private readonly ItemValue[] equipment;
    private readonly int[] beltPreferred,equipmentPreferred;
    private readonly int beltLength;

    private RebirthRecipeOutputInventoryPrediction(EntityPlayerLocal actor)
    {
        Bag=ItemStack.Clone(actor.bag.ItemGrid.items);Belt=ItemStack.Clone(actor.inventory.ItemGrid.items);
        beltLength=actor.inventory.Length;
        beltPreferred=(int[])actor.inventory.preferredItemTypes.Clone();
        equipmentPreferred=(int[])actor.equipment.preferredItemSlots.Clone();
        equipment=new ItemValue[actor.equipment.GetSlotCount()];
        for(int i=0;i<equipment.Length;i++)equipment[i]=actor.equipment.GetSlotItem(i).Clone();
    }
    private RebirthRecipeOutputInventoryPrediction(ItemStack[] bag,ItemStack[] belt,int beltLength,int[] beltPreferred,ItemValue[] equipment,int[] equipmentPreferred)
    {
        Bag=ItemStack.Clone(bag);Belt=ItemStack.Clone(belt);this.beltLength=beltLength;
        this.beltPreferred=(int[])beltPreferred.Clone();this.equipmentPreferred=(int[])equipmentPreferred.Clone();
        this.equipment=new ItemValue[equipment.Length];
        for(int i=0;i<equipment.Length;i++)this.equipment[i]=equipment[i].Clone();
    }
    // Detached native-owner witness input. No server live remote inventory is read.
    internal static bool TryPlan(ItemStack[] originalBag,ItemStack[] originalBelt,int beltLength,int[] beltPreferred,
        ItemValue[] originalEquipment,int[] equipmentPreferred,IReadOnlyList<ItemStack> outputs,
        out ItemStack[] bag,out ItemStack[] belt,out ItemValue[] equipment)
    {
        bag=null;belt=null;equipment=null;
        if(originalBag==null||originalBelt==null||originalEquipment==null||beltPreferred==null||equipmentPreferred==null||
            originalBag.Length>169||originalBelt.Length>20||originalEquipment.Length!=13||equipmentPreferred.Length!=13||
            beltPreferred.Length!=originalBelt.Length||outputs==null||outputs.Count<1||outputs.Count>65)return false;
        var plan=new RebirthRecipeOutputInventoryPrediction(originalBag,originalBelt,beltLength,beltPreferred,originalEquipment,equipmentPreferred);
        return FinishPlan(plan,outputs,out bag,out belt,out equipment);
    }

    internal static bool TryPlan(EntityPlayerLocal actor,IReadOnlyList<ItemStack> outputs,out ItemStack[] bag,out ItemStack[] belt)
        =>TryPlan(actor,outputs,out bag,out belt,out _);
    internal static bool TryPlan(EntityPlayerLocal actor,IReadOnlyList<ItemStack> outputs,out ItemStack[] bag,out ItemStack[] belt,out ItemValue[] equipment)
    {
        bag=null;belt=null;equipment=null;
        if(actor==null||outputs==null||outputs.Count<1||outputs.Count>65)return false;
        var plan=new RebirthRecipeOutputInventoryPrediction(actor);
        return FinishPlan(plan,outputs,out bag,out belt,out equipment);
    }
    private static bool FinishPlan(RebirthRecipeOutputInventoryPrediction plan,IReadOnlyList<ItemStack> outputs,out ItemStack[] bag,out ItemStack[] belt,out ItemValue[] equipment)
    {
        bag=null;belt=null;equipment=null;
        if(plan.beltLength<0||plan.beltLength>plan.Belt.Length||plan.beltLength>plan.beltPreferred.Length)return false;
        foreach(var output in outputs)if(!plan.AddNoPartial(output))return false;
        bag=plan.Bag;belt=plan.Belt;equipment=new ItemValue[plan.equipment.Length];
        for(int index=0;index<equipment.Length;index++)equipment[index]=plan.equipment[index].Clone();return true;
    }

    private bool CanStack(ItemStack[] cells,int length,ItemStack item,bool belt,bool partial)
    {
        for(int i=0;i<length;i++)
        {
            if(!partial&&belt&&!CanMove(item,i))continue;
            if(!partial&&cells[i].IsEmpty())return true;
            if(partial?cells[i].CanStackPartlyWith(item,out _):cells[i].CanStackWith(item))return true;
        }
        return false;
    }
    private static bool CanMove(ItemStack item,int slot)=>item.itemValue.ItemClass==null||
        item.itemValue.ItemClass.CanMoveToSlot(XUiC_ItemStack.StackLocationTypes.Backpack,slot);

    private bool Merge(ItemStack[] cells,int length,ItemStack item,bool belt)
    {
        if(!belt&&!item.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack))return false;
        var previous=RebirthCookingPartialMergeScope.Incoming;
        RebirthCookingPartialMergeScope.Incoming=item.itemValue;
        try
        {
            for(int i=0;i<length;i++)
            {
                if(belt&&!CanMove(item,i))continue;
                int count=item.count;
                if(cells[i].itemValue.type!=item.itemValue.type||
                    !RebirthCookingItemStats.Compatible(cells[i].itemValue,item.itemValue)||!cells[i].CanStackPartly(ref count))continue;
                cells[i].count+=count;item.count-=count;
                if(item.count==0)return true;
            }
            return false;
        }
        finally{RebirthCookingPartialMergeScope.Incoming=previous;}
    }

    private bool AddNoPartial(ItemStack original)
    {
        if(original?.itemValue==null||original.IsEmpty()||original.count<1||
            (!CanStack(Bag,Bag.Length,original,false,false)&&!CanStack(Belt,beltLength,original,true,false))||
            !original.CanMoveTo(XUiC_ItemStack.StackLocationTypes.ToolBelt))return false;
        var item=original.Clone();bool moved=false;
        if(item.itemValue.ItemClass is ItemClassArmor armor&&armor.AutoEquip)
        {
            int slot=(int)armor.EquipSlot;if(slot<0||slot>=equipment.Length)return false;
            var old=equipment[slot];SetEquipment(slot,item.itemValue);
            if(armor.AllowUnEquip)item=new ItemStack(old.Clone(),1);
            else{item.count=0;moved=true;}
        }
        if(!moved)
        {
            if(CanStack(Bag,Bag.Length,item,false,true))moved=Merge(Bag,Bag.Length,item,false);
            else if(CanStack(Belt,beltLength,item,true,true))moved=Merge(Belt,beltLength,item,true);
        }
        if(!moved)
        {
            if(CanStack(Bag,Bag.Length,item,false,false))moved=Merge(Bag,Bag.Length,item,false);
            else if(CanStack(Belt,beltLength,item,true,false))moved=Merge(Belt,beltLength,item,true);
        }
        if(moved)return true;
        int maximum=item.itemValue.ItemClass?.Stacknumber==null?1:item.itemValue.ItemClass.MaxCount;
        if(maximum<1)return false;
        // Native splits only the fallback insertion, after both partial-merge passes.
        for(int remaining=item.count;remaining>0;remaining-=maximum)
        {
            var part=item.Clone();part.count=Math.Min(remaining,maximum);
            if(!ReturnEquipment(part)&&!ReturnBelt(part)&&!AddGrid(Bag,Bag.Length,part,false)&&!AddGrid(Belt,beltLength,part,true))return false;
        }
        return true;
    }

    private void SetEquipment(int slot,ItemValue value)
    {equipment[slot]=value.Clone();if(slot<equipmentPreferred.Length)equipmentPreferred[slot]=value.type;}
    private bool ReturnEquipment(ItemStack item)
    {
        if(!(item.itemValue.ItemClass is ItemClassArmor armor))return false;
        int worn=(int)armor.EquipSlot;
        if(worn>=0&&worn<equipment.Length&&!equipment[worn].IsEmpty())return false;
        int preferred=Array.IndexOf(equipmentPreferred,item.itemValue.type);
        if(preferred<0||preferred>=equipment.Length||!equipment[preferred].IsEmpty())return false;
        SetEquipment(preferred,item.itemValue);return true;
    }
    private bool ReturnBelt(ItemStack item)
    {
        for(int i=0;i<beltLength;i++)
        {
            if(beltPreferred[i]!=item.itemValue.type)continue;
            if(!Belt[i].IsEmpty()&&Belt[i].itemValue.type==item.itemValue.type&&Belt[i].CanStackWith(item))
            {Belt[i].count+=item.count;return true;}
            if(Belt[i].IsEmpty()&&CanMove(item,i)){Belt[i]=item.Clone();beltPreferred[i]=item.itemValue.type;return true;}
        }
        return false;
    }
    private bool AddGrid(ItemStack[] cells,int length,ItemStack item,bool belt)
    {
        if(!item.CanMoveTo(belt?XUiC_ItemStack.StackLocationTypes.ToolBelt:XUiC_ItemStack.StackLocationTypes.Backpack))return false;
        for(int i=0;i<length;i++)if(cells[i].itemValue.type==item.itemValue.type&&cells[i].CanStackWith(item))
        {cells[i].count+=item.count;return true;}
        for(int i=0;i<length;i++)if(cells[i].IsEmpty())
        {cells[i]=item.Clone();if(belt)beltPreferred[i]=item.itemValue.type;return true;}
        return false;
    }
}
