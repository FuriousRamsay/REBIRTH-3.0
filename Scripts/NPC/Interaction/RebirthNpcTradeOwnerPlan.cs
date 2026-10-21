using System;

// Pure owner-native exchange plan. The caller must authenticate the server quote,
// retain this complete original and qualify native saved owner publication before
// settling NPC legs. This planner never reads a server EntityPlayer replica,
// invokes setters, pays a quote or manufactures payment/receipt authority.
internal sealed class RebirthNpcTradeOwnerPlan
{
    internal readonly ItemStack[] BeforeBag,BeforeBelt,AfterBag,AfterBelt;
    internal readonly bool[] BagLocks,BeltLocks;
    internal readonly int OwnedBeltSlots;
    private RebirthNpcTradeOwnerPlan(ItemStack[] bag,ItemStack[] belt,ItemStack[] afterBag,ItemStack[] afterBelt,int owned,bool[] bagLocks,bool[] beltLocks)
    {BeforeBag=bag;BeforeBelt=belt;AfterBag=afterBag;AfterBelt=afterBelt;OwnedBeltSlots=owned;BagLocks=bagLocks;BeltLocks=beltLocks;}

    internal static bool TryCreate(RebirthNpcTradeQuote quote,ItemStack[] bag,ItemStack[] belt,int owned,
        Func<bool,int,bool> locked,out RebirthNpcTradeOwnerPlan plan)
    {
        plan=null;
        try
        {
            if(quote==null||quote.QuoteId==Guid.Empty||bag==null||belt==null||owned<0||owned>belt.Length||locked==null||
                quote.Quantity<=0||quote.TotalPrice<=0||quote.UnitPrice<=0||(long)quote.Quantity*quote.UnitPrice!=quote.TotalPrice||
                quote.Direction!=RebirthNpcTradeDirection.BuyFromNpc&&quote.Direction!=RebirthNpcTradeDirection.SellToNpc)return false;
            var goods=ItemClass.GetItem(quote.ItemKey,false);var currency=ItemClass.GetItem(quote.CurrencyItemKey,false);
            if(goods==null||currency==null||goods.IsEmpty()||currency.IsEmpty()||goods.type==currency.type||
                !RebirthNpcPlayerInventoryEndpoint.IsFungible(goods)||!RebirthNpcPlayerInventoryEndpoint.IsFungible(currency))return false;
            var bagLocks=new bool[bag.Length];var beltLocks=new bool[belt.Length];
            for(int i=0;i<bag.Length;i++)bagLocks[i]=locked(true,i);
            for(int i=0;i<belt.Length;i++)beltLocks[i]=i>=owned||locked(false,i);
            bool IsLocked(bool backpack,int slot)=>backpack?bagLocks[slot]:beltLocks[slot];
            var beforeBag=Copy(bag);var beforeBelt=Copy(belt);var afterBag=Copy(beforeBag);var afterBelt=Copy(beforeBelt);
            bool buy=quote.Direction==RebirthNpcTradeDirection.BuyFromNpc;
            int debitType=buy?currency.type:goods.type,remaining=buy?quote.TotalPrice:quote.Quantity;
            Debit(afterBag,true,afterBag.Length,debitType,IsLocked,ref remaining);
            Debit(afterBelt,false,owned,debitType,IsLocked,ref remaining);
            if(remaining!=0)return false;
            var credit=new ItemStack((buy?goods:currency).Clone(),buy?quote.Quantity:quote.TotalPrice);
            // Preserve the native endpoint's single-stack bag-then-belt insertion
            // contract. Capacity is assessed after the exact owner debit.
            if(!Credit(afterBag,true,afterBag.Length,credit,IsLocked)&&!Credit(afterBelt,false,owned,credit,IsLocked))return false;
            plan=new RebirthNpcTradeOwnerPlan(beforeBag,beforeBelt,afterBag,afterBelt,owned,bagLocks,beltLocks);return true;
        }
        catch{return false;}
    }
    private static ItemStack[] Copy(ItemStack[] slots)
    {
        var result=new ItemStack[slots.Length];
        for(int i=0;i<slots.Length;i++)
        {
            var cell=slots[i];
            if(cell==null||cell.count<0||cell.itemValue==null)throw new InvalidOperationException("Invalid native trade cell");
            result[i]=cell.Clone(); // Preserve native type-zero/count-one empties too.
        }
        return result;
    }
    private static void Debit(ItemStack[] slots,bool bag,int owned,int type,Func<bool,int,bool> locked,ref int remaining)
    {
        for(int i=0;i<owned&&remaining>0;i++)
        {
            var cell=slots[i];if(cell.IsEmpty()||cell.itemValue.type!=type||locked(bag,i)||!RebirthNpcPlayerInventoryEndpoint.IsFungible(cell.itemValue))continue;
            int take=Math.Min(remaining,cell.count);remaining-=take;cell.count-=take;
            if(cell.count==0)slots[i]=ItemStack.Empty.Clone();
        }
    }
    private static bool Credit(ItemStack[] slots,bool bag,int owned,ItemStack credit,Func<bool,int,bool> locked)
    {
        if(!credit.CanMoveTo(bag?XUiC_ItemStack.StackLocationTypes.Backpack:XUiC_ItemStack.StackLocationTypes.ToolBelt))return false;
        var item=ItemClass.GetForId(credit.itemValue.type);if(item==null||item.MaxCount<credit.count)return false;
        // Native AddItem selects an existing compatible whole-stack destination
        // before an empty slot. Do not change a rich compatible stack's identity.
        for(int pass=0;pass<2;pass++)for(int i=0;i<owned;i++)
        {
            if(locked(bag,i))continue;var cell=slots[i];
            if(pass==0)
            {
                if(cell.IsEmpty()||!cell.CanStackPartlyWith(credit,out int capacity)||capacity<credit.count)continue;
                cell.count=checked(cell.count+credit.count);return true;
            }
            if(!cell.IsEmpty())continue;slots[i]=credit.Clone();return true;
        }
        return false;
    }
}
