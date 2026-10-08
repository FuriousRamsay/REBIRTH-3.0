using System;
// Read-only native destination selection followed by detached strict output prediction. No native insertion simulation.
internal static class RebirthStationPaidOutputPreflight
{
    internal static bool TryConform(ItemStack[] cells,ItemStack incoming,int maxItemCount,CraftCompleteData receipt)
    {
        try
        {
            if(cells==null||cells.Length<1||cells.Length>255||incoming?.itemValue==null||incoming.count<1||receipt==null)return false;
            var before=new ItemStack[cells.Length];var after=new ItemStack[cells.Length];
            for(int i=0;i<cells.Length;i++)
            {
                var cell=cells[i];if(cell?.itemValue==null||cell.count<0)return false;
                before[i]=cell.Clone();after[i]=cell.Clone();
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(cell,before[i])||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(cell,after[i]))return false;
            }
            int destination=-1;
            for(int i=0;i<cells.Length;i++)if(cells[i].CanStackWith(incoming)){destination=i;break;}
            if(destination>=0)
            {
                long count=(long)cells[destination].count+incoming.count;if(count>int.MaxValue)return false;
                after[destination]=new ItemStack(cells[destination].itemValue.Clone(),(int)count);
            }
            else
            {
                for(int i=0;i<cells.Length&&(maxItemCount==-1||i!=maxItemCount);i++)
                    if(cells[i].IsEmpty()){destination=i;break;}
                if(destination<0)return true; // genuine full output: original returns -1, no completion
                after[destination]=new ItemStack(incoming.itemValue.Clone(),incoming.count);
            }
            return RebirthStationOutputDelta.Matches(before,after,receipt);
        }
        catch{return false;}
    }
}