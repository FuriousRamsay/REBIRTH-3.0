using System.Collections.Generic;

// Prepare detached item images before touching inventory or publishing recovery loot.
public static class RebirthBackpackSectionTransfer
{
    public static bool TryPrepare(ItemValue outgoing,ItemValue incoming,out ItemValue emptied,out ItemValue filled,out List<ItemStack> overflow)
    {
        emptied=outgoing;filled=incoming;overflow=new List<ItemStack>();
        if(outgoing==null)return true;
        if(!RebirthBackpackLibraryContents.TryRead(outgoing,out var books)||!RebirthBackpackSellStashContents.TryRead(outgoing,out var sale))return false;
        ItemStack[] nextBooks=null,nextSale=null;
        if(incoming!=null&&(!RebirthBackpackLibraryContents.TryRead(incoming,out nextBooks)||!RebirthBackpackSellStashContents.TryRead(incoming,out nextSale)))return false;
        Transfer(books,nextBooks,overflow,true);Transfer(sale,nextSale,overflow,false);
        if(!RebirthBackpackLibraryContents.TryWrite(outgoing,ItemStack.CreateArray(books.Length),out var oldBooks)||
            !RebirthBackpackSellStashContents.TryWrite(oldBooks,ItemStack.CreateArray(sale.Length),out emptied))return false;
        if(incoming!=null&&(!RebirthBackpackLibraryContents.TryWrite(incoming,nextBooks,out var newBooks)||
            !RebirthBackpackSellStashContents.TryWrite(newBooks,nextSale,out filled)))return false;
        return true;
    }
    private static void Transfer(ItemStack[] source,ItemStack[] target,List<ItemStack> overflow,bool reading)
    {
        for(int i=0;i<source.Length;i++)
        {
            var item=source[i];if(item==null||item.IsEmpty())continue;
            var remaining=item.Clone();
            // Slots removed by a smaller pack go to recovery, preserving the capacity boundary.
            if(target!=null&&i<target.Length&&(!reading||RebirthBackpackLibraryPolicy.IsLearningMaterial(item.itemValue)))
            {
                for(int j=0;j<target.Length&&remaining.count>0;j++)
                {
                    int slot=(i+j)%target.Length;var current=target[slot];
                    if(current==null||current.IsEmpty()){target[slot]=remaining.Clone();remaining.count=0;break;}
                    if(!RebirthBackpackLibraryTransfer.CanMerge(current,remaining))continue;
                    int take=System.Math.Min(remaining.count,System.Math.Max(0,current.itemValue.ItemClass.MaxCount-current.count));
                    if(take<=0)continue;var merged=current.Clone();merged.count+=take;target[slot]=merged;remaining.count-=take;
                }
            }
            if(remaining.count>0)overflow.Add(remaining);
        }
    }
}
