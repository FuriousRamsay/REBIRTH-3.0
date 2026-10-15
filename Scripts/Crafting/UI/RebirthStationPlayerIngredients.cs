using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// Detached native PlayerInventory payment allocation. No setters, reservations,
// queue publication or persistence: caller owns durable coupled commit authority.
internal static class RebirthStationPlayerIngredients
{
    internal static bool UsesPlayerPayment(EntityPlayer player,RebirthCamperStationState station)
    {
        if(!ThreadManager.IsMainThread()||!(player is EntityPlayerLocal local)||station==null||
            !RebirthCamperStationWindowSession.TryGetCurrent(player,out var current)||!ReferenceEquals(current,station))return false;
        var groups=LocalPlayerUI.GetUIForPlayer(local)?.xui?.WindowGroups;
        if(groups==null)return false;
        XUiC_WorkstationWindowGroup selected=null;
        foreach(var group in groups)
            if(group?.isShowing==true&&group.Controller is XUiC_WorkstationWindowGroup window&&
                ReferenceEquals(window.WorkstationData?.TileEntity,station))
            {if(selected!=null)return false;selected=window;}
        // Actual native inputWindow is the source discriminator used by native
        // crafting, not the installed block's name or the hidden Input array.
        return selected!=null&&selected.inputWindow==null;
    }
    internal sealed class Allocation
    {
        internal bool IsBag { get; }
        internal int Slot { get; }
        internal int Count { get; }
        private readonly ItemStack fragment;
        internal Allocation(bool bag,int slot,ItemStack source,int count)
        {IsBag=bag;Slot=slot;Count=count;fragment=source.Clone();fragment.count=count;}
        internal ItemStack Snapshot()=>fragment.Clone();
    }
    internal sealed class Plan
    {
        private readonly ItemStack[] bagBefore,bagAfter,beltBefore,beltAfter;
        private readonly bool[] bagLocks,beltLocks;
        internal int OwnedBeltSlots {get;}
        internal ReadOnlyCollection<Allocation> Inputs {get;}
        internal Plan(ItemStack[] oldBag,ItemStack[] newBag,ItemStack[] oldBelt,ItemStack[] newBelt,
            bool[] locksBag,bool[] locksBelt,int owned,List<Allocation> inputs)
        {bagBefore=Copy(oldBag);bagAfter=Copy(newBag);beltBefore=Copy(oldBelt);beltAfter=Copy(newBelt);
            bagLocks=locksBag==null?null:(bool[])locksBag.Clone();beltLocks=locksBelt==null?null:(bool[])locksBelt.Clone();
            OwnedBeltSlots=owned;Inputs=new List<Allocation>(inputs).AsReadOnly();}
        internal bool TryGetImages(out ItemStack[] oldBag,out ItemStack[] newBag,out ItemStack[] oldBelt,out ItemStack[] newBelt)
        {oldBag=Copy(bagBefore);newBag=Copy(bagAfter);oldBelt=Copy(beltBefore);newBelt=Copy(beltAfter);return true;}
        internal bool Matches(ItemStack[] bag,ItemStack[] belt,bool[] locksBag,bool[] locksBelt,int owned)
            =>owned==OwnedBeltSlots&&Same(bagBefore,bag)&&Same(beltBefore,belt)&&SameLocks(bagLocks,locksBag)&&SameLocks(beltLocks,locksBelt);
        internal List<ItemStack> PaidSnapshots()
        {var result=new List<ItemStack>();foreach(var input in Inputs)result.Add(input.Snapshot());return result;}
    }
    private static ItemStack[] Copy(ItemStack[] source)
    {var result=new ItemStack[source.Length];for(int i=0;i<result.Length;i++)result[i]=source[i].Clone();return result;}
    private static bool Same(ItemStack[] a,ItemStack[] b)
    {if(a==null||b==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(a[i],b[i]))return false;return true;}
    private static bool SameLocks(bool[] a,bool[] b)
    {if(a==null||b==null)return a==b;if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    private static bool Valid(ItemStack[] cells,int maximum)
    {if(cells==null||cells.Length>maximum)return false;foreach(var s in cells)if(s==null||s.itemValue==null||s.count<0||s.count>ushort.MaxValue||s.count>0&&!s.itemValue.IsEmpty()&&s.itemValue.ItemClass==null)return false;return true;}
    internal static bool TryPlan(EntityPlayer player,Recipe recipe,int batches,int tier,
        ItemStack[] bag,ItemStack[] belt,bool[] bagLocks,bool[] beltLocks,int ownedBeltSlots,out Plan plan)
    {
        plan=null;
        try
        {
            if(player==null||recipe?.ingredients==null||recipe.ingredients.Count==0||recipe.materialBasedRecipe||
                batches<1||batches>9999||tier<0||tier>6||!Valid(bag,169)||!Valid(belt,20)||
                ownedBeltSlots<0||ownedBeltSlots>belt.Length||bagLocks!=null&&bagLocks.Length!=bag.Length||
                beltLocks!=null&&beltLocks.Length!=belt.Length)return false;
            var nextBag=Copy(bag);var nextBelt=Copy(belt);var allocations=new List<Allocation>();
            // Native RemoveItems preserves ingredient order, bag first then belt,
            // physical ascending order, ignoring modified candidates. Slot locks
            // are captured as current-scope evidence, not invented payment rules.
            foreach(var ingredient in recipe.ingredients)
            {
                if(ingredient?.itemValue==null||ingredient.itemValue.IsEmpty()||
                    !RebirthCraftingIngredientQuantity.TryResolveTotal(player,recipe,ingredient,tier,batches,out int remaining)||remaining<1)return false;
                for(int area=0;area<2&&remaining>0;area++)
                {
                    var cells=area==0?nextBag:nextBelt;int limit=area==0?cells.Length:ownedBeltSlots;
                    for(int slot=0;slot<limit&&remaining>0;slot++)
                    {
                        var item=cells[slot];var value=item.itemValue;
                        if(item.IsEmpty()||value.type!=ingredient.itemValue.type||value.HasModSlots&&value.HasMods())continue;
                        // Native nonstackable DecItem clears its entire slot.
                        // Refuse malformed multi-count nonstackables rather than
                        // guessing partial debit or silently losing extra items.
                        if(!value.ItemClass.CanStack()&&item.count!=1)return false;
                        int take=Math.Min(remaining,item.count);
                        allocations.Add(new Allocation(area==0,slot,item,take));
                        item.count-=take;remaining-=take;if(item.count==0)cells[slot]=ItemStack.Empty.Clone();
                    }
                }
                if(remaining!=0)return false;
            }
            if(allocations.Count==0)return false;
            plan=new Plan(bag,nextBag,belt,nextBelt,bagLocks,beltLocks,ownedBeltSlots,allocations);return true;
        }
        catch{return false;}
    }
}
