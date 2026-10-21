using System;
using System.Collections.Generic;
using System.Linq;

// Pure native XUiC_WorkstationInputGrid.DecItem allocation. Unlike native
// PlayerInventory payment, the native station grid does not skip modified
// items. This planner grants no source payment or tile mutation authority.
internal static class RebirthRemoteResourcePlacedStationInputPlan
{
    internal static bool TryGetShortfall(ItemStack[] input,IReadOnlyList<ItemStack> totalRequirements,out List<ItemStack> missing)
    {
        missing=null;
        try
        {
            if(input==null||input.Length>byte.MaxValue||totalRequirements==null||totalRequirements.Count<1||totalRequirements.Count>4096||
                input.Any(item=>item?.itemValue==null||item.count<0||item.count>ushort.MaxValue)||
                totalRequirements.Any(item=>item==null||item.IsEmpty()||item.count<1))return false;
            var result=new List<ItemStack>();
            foreach(var group in totalRequirements.GroupBy(item=>item.itemValue.type))
            {
                long need=group.Sum(item=>(long)item.count),available=input.Where(item=>item.itemValue.type==group.Key).Sum(item=>(long)item.count);
                long remaining=Math.Max(0L,need-available);if(remaining>int.MaxValue)return false;
                if(remaining>0)result.Add(new ItemStack(group.First().itemValue.Clone(),(int)remaining));
            }
            missing=result;return true;
        }
        catch{return false;}
    }
    internal sealed class Plan
    {
        private readonly ItemStack[] before,after,paid;
        internal Plan(ItemStack[] old,ItemStack[] next,List<ItemStack> fragments)
        {before=ItemStack.Clone(old);after=ItemStack.Clone(next);paid=ItemStack.Clone(fragments.ToArray());}
        internal ItemStack[] CopyBefore()=>ItemStack.Clone(before);
        internal ItemStack[] CopyAfter()=>ItemStack.Clone(after);
        internal ItemStack[] CopyPaid()=>ItemStack.Clone(paid);
    }
    internal static bool TryCreate(ItemStack[] original,IReadOnlyList<ItemStack> totalRequirements,IReadOnlyList<ItemStack> originalRemotePaid,out Plan plan)
    {
        plan=null;
        try
        {
            if(original==null||original.Length>byte.MaxValue||totalRequirements==null||totalRequirements.Count<1||totalRequirements.Count>4096||
                originalRemotePaid==null||originalRemotePaid.Count<1||originalRemotePaid.Count>short.MaxValue||
                original.Any(item=>item?.itemValue==null||item.count<0||item.count>ushort.MaxValue||item.count>0&&item.itemValue.IsEmpty())||
                totalRequirements.Any(item=>item==null||item.IsEmpty()||item.count<1)||originalRemotePaid.Any(item=>item==null||item.IsEmpty()||item.count<1))return false;
            var needs=new Dictionary<int,int>();var remote=new Dictionary<int,int>();var order=new List<int>();
            foreach(var item in totalRequirements)
            {int type=item.itemValue.type;if(!needs.TryGetValue(type,out int count))order.Add(type);needs[type]=checked(count+item.count);}
            foreach(var item in originalRemotePaid)
            {int type=item.itemValue.type;remote.TryGetValue(type,out int count);remote[type]=checked(count+item.count);}
            if(remote.Any(pair=>!needs.TryGetValue(pair.Key,out int count)||pair.Value>count))return false;
            if(!TryGetShortfall(original,totalRequirements,out var exact)||exact.Count!=remote.Count||
                exact.Any(item=>!remote.TryGetValue(item.itemValue.type,out int count)||count!=item.count))return false;
            var after=ItemStack.Clone(original);var fragments=new List<ItemStack>();
            foreach(int type in order)
            {
                remote.TryGetValue(type,out int fetched);int remaining=needs[type]-fetched;
                // Exact native order: source ingredient order, ascending grid
                // slot order, stack decrement or entire nonstackable clear.
                for(int i=0;i<after.Length&&remaining>0;i++)
                {
                    var item=after[i];if(item.itemValue.type!=type)continue;
                    if(item.itemValue.ItemClass==null)return false;
                    if(item.itemValue.ItemClass.CanStack())
                    {
                        int take=Math.Min(item.count,remaining);
                        if(take>0)fragments.Add(new ItemStack(item.itemValue.Clone(),take));
                        remaining-=take;item.count-=take;
                        if(item.count==0)after[i]=ItemStack.Empty.Clone();
                    }
                    else
                    {
                        // Native would clear a malformed count0/>1 slot but
                        // account one unit. Do not admit a nonconserving plan.
                        if(item.count!=1)return false;
                        fragments.Add(item.Clone());after[i]=ItemStack.Empty.Clone();remaining--;
                    }
                }
                if(remaining!=0)return false;
            }
            fragments.AddRange(originalRemotePaid.Select(item=>item.Clone()));
            plan=new Plan(original,after,fragments);return true;
        }
        catch{plan=null;return false;}
    }
}
