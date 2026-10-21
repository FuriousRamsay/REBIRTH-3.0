using System;
using System.Collections.Generic;

public static class RebirthCookingLocalIngredients
{
    private sealed class Demand { public ItemValue Item; public long Count; }
    private sealed class Removal { public int Source, Index, Count; public ItemStack Before; }

    public static int Count(EntityPlayer player,ItemValue item)
    {
        if(player==null||item==null||item.IsEmpty()||RebirthBackpackLibraryReservation.BlocksResourceUse(player))return 0;
        item=RequiredIdentity(item);
        long count=0;
        foreach(var stack in player.bag.ItemGrid.items)if(Matches(stack,item))count+=stack.count;
        var belt=player.inventory.ItemGrid.items;
        for(int i=0;i<RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);i++)
            if(Matches(belt[i],item))count+=belt[i].count;
        return count>int.MaxValue?int.MaxValue:(int)count;
    }
    private static bool Matches(ItemStack stack,ItemValue item)=>stack!=null&&!stack.IsEmpty()&&item!=null&&!item.IsEmpty()&&stack.itemValue.type==item.type&&RebirthCookingItemStats.Compatible(stack.itemValue,item)&&RebirthCookingItemStats.FullIngredient(stack.itemValue);
    // This marker requests a full ingredient; it is not physical item metadata.
    // Normalize the required clone only. The source and its provenance remain
    // untouched, and Matches still explicitly rejects partially used liquids.
    private static ItemValue RequiredIdentity(ItemValue required)
    {
        var identity=required.Clone();
        if(identity.TryGetMetadata("rebirth.cooking.fullIngredient",out int full)&&full==1)
            identity.Metadata.Remove("rebirth.cooking.fullIngredient");
        return identity;
    }

    public static bool Take(EntityPlayer player,List<ItemStack> needs,List<ItemStack> removed, IList<RemoteResourceTransactions.ReturnReceipt> receipts = null)
    {
        if(player==null||needs==null||removed==null||RebirthBackpackLibraryReservation.BlocksResourceUse(player))return false;
        var inventoryModel=(player as EntityPlayerLocal)?.PlayerUI?.xui?.PlayerInventory;
        if(player.world.IsRemote()&&inventoryModel==null)return false;
        var demands=new List<Demand>();
        foreach(var need in needs)
        {
            if(need==null||need.IsEmpty()||need.itemValue==null||need.itemValue.IsEmpty()||need.count<=0)return false;
            var required=RequiredIdentity(need.itemValue);
            Demand demand=null;
            for(int i=0;i<demands.Count;i++)if(MatchesIdentity(demands[i].Item,required)){demand=demands[i];break;}
            if(demand==null){demand=new Demand{Item=required};demands.Add(demand);}
            demand.Count+=need.count;
            if(demand.Count>int.MaxValue)return false;
        }

        var plan=new List<Removal>();
        foreach(var demand in demands)
        {
            long remaining=demand.Count;
            for(int source=0;source<2&&remaining>0;source++)
            {
                var slots=source==0?player.bag.ItemGrid.items:player.inventory.ItemGrid.items;
                int limit=source==0?slots.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,slots.Length);
                for(int i=0;i<limit&&remaining>0;i++)
                {
                    var stack=slots[i];if(!Matches(stack,demand.Item))continue;
                    int take=(int)Math.Min(remaining,stack.count);if(take<=0)continue;
                    plan.Add(new Removal{Source=source,Index=i,Count=take,Before=stack.Clone()});
                    remaining-=take;
                }
            }
            if(remaining!=0)return false; // no mutation occurs until every aggregated demand is satisfied
        }

        bool remote=player.world.IsRemote(),backpackChanged=false,toolbeltChanged=false;
        var remoteBackpack=remote?ItemStack.Clone(player.bag.ItemGrid.items):null;
        var remoteToolbelt=remote?ItemStack.Clone(player.inventory.ItemGrid.items):null;
        for(int p=0;p<plan.Count;p++)
        {
            var step=plan[p];
            var current=step.Source==0?player.bag.ItemGrid.items[step.Index]:player.inventory.ItemGrid.items[step.Index];
            if(current==null||current.IsEmpty()||!SameStackIdentity(current,step.Before)||current.count<step.Count)return false;
        }
        for(int p=0;p<plan.Count;p++)
        {
            var step=plan[p];
            var after=step.Before.Clone();after.count-=step.Count;if(after.count==0)after=ItemStack.Empty.Clone();
            removed.Add(new ItemStack(step.Before.itemValue.Clone(),step.Count));
            receipts?.Add(new RemoteResourceTransactions.ReturnReceipt(player) {Toolbelt=step.Source==1,Slot=step.Index,Item=new ItemStack(step.Before.itemValue.Clone(),step.Count),Remaining=step.Count});
            if(step.Source==0)
            {if(remote)remoteBackpack[step.Index]=after;else player.bag.SetSlot(step.Index,after);backpackChanged=true;}
            else
            {if(remote)remoteToolbelt[step.Index]=after;else player.inventory.SetItem(step.Index,after);toolbeltChanged=true;}
        }
        if(remote)
        {
            // Native inventory setters publish the changed grids to the remote host.
            // Bag.SetSlot alone only changes the local cell; UI dispatch is not a network publication.
            if(backpackChanged)inventoryModel.SetBackpackItemStacks(remoteBackpack);
            if(toolbeltChanged)inventoryModel.SetToolbeltItemStacks(remoteToolbelt);
        }
        return true;
    }

    private static bool MatchesIdentity(ItemValue a,ItemValue b)=>a!=null&&b!=null&&a.type==b.type&&RebirthCookingItemStats.Compatible(a,b);
    private static bool SameStackIdentity(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&MatchesIdentity(a.itemValue,b.itemValue);
}
