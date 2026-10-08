using System;
using System.Collections.Generic;

public static class RebirthCookingLocalIngredients
{
    private sealed class Demand { public ItemValue Item; public long Count; }
    private sealed class Removal { public int Source, Index, Count; public ItemStack Before; }

    public static int Count(EntityPlayer player,ItemValue item)
    {
        if(player==null||item==null||item.IsEmpty()||RebirthBackpackLibraryReservation.BlocksResourceUse(player))return 0;
        long count=0;
        foreach(var stack in player.bag.ItemGrid.items)if(Matches(stack,item))count+=stack.count;
        var belt=player.inventory.ItemGrid.items;
        for(int i=0;i<RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);i++)
            if(Matches(belt[i],item))count+=belt[i].count;
        return count>int.MaxValue?int.MaxValue:(int)count;
    }
    private static bool Matches(ItemStack stack,ItemValue item)=>stack!=null&&!stack.IsEmpty()&&item!=null&&!item.IsEmpty()&&stack.itemValue.type==item.type&&RebirthCookingItemStats.Compatible(stack.itemValue,item)&&RebirthCookingItemStats.FullIngredient(stack.itemValue);

    public static bool Take(EntityPlayer player,List<ItemStack> needs,List<ItemStack> removed, IList<RemoteResourceTransactions.ReturnReceipt> receipts = null)
    {
        if(player==null||needs==null||removed==null||RebirthBackpackLibraryReservation.BlocksResourceUse(player))return false;
        var demands=new List<Demand>();
        foreach(var need in needs)
        {
            if(need==null||need.IsEmpty()||need.itemValue==null||need.itemValue.IsEmpty()||need.count<=0)return false;
            Demand demand=null;
            for(int i=0;i<demands.Count;i++)if(MatchesIdentity(demands[i].Item,need.itemValue)){demand=demands[i];break;}
            if(demand==null){demand=new Demand{Item=need.itemValue.Clone()};demands.Add(demand);}
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
            receipts?.Add(new RemoteResourceTransactions.ReturnReceipt {Toolbelt=step.Source==1,Slot=step.Index,Item=new ItemStack(step.Before.itemValue.Clone(),step.Count),Remaining=step.Count});
            if(step.Source==0)player.bag.SetSlot(step.Index,after);else player.inventory.SetItem(step.Index,after);
        }
        return true;
    }

    private static bool MatchesIdentity(ItemValue a,ItemValue b)=>a!=null&&b!=null&&a.type==b.type&&RebirthCookingItemStats.Compatible(a,b);
    private static bool SameStackIdentity(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&MatchesIdentity(a.itemValue,b.itemValue);
}
