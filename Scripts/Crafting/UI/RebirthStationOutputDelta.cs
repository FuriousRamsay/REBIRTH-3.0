using System;
using System.Collections.Generic;

// Detached observation filter only. Caller must separately prove native publication and custody.
internal static class RebirthStationOutputDelta
{
    internal static bool Matches(IList<ItemStack> before,IList<ItemStack> after,CraftCompleteData receipt)
    {
        try
        {
            if(before==null||after==null||before.Count<1||before.Count>255||before.Count!=after.Count||
                !RebirthStationCompletionReceipt.TryReadIdentity(receipt,out var identity))return false;
            var physical=receipt.CraftedItemStack.itemValue.Clone();
            foreach(var field in new[]{"version","job","creation","definition","tier","count","xp"})
                physical.Metadata.Remove(RebirthStationCompletionReceipt.Prefix+field);
            long added=0;
            for(int i=0;i<before.Count;i++)
            {
                var old=before[i];var next=after[i];
                if(old?.itemValue==null||next?.itemValue==null||old.count<0||next.count<0)return false;
                if(RebirthStationGridIngredients.IsSameStackSnapshot(old,next))continue;
                if(next.count<=old.count||!RebirthStationGridIngredients.IsSameStackSnapshot(next,new ItemStack(physical,next.count)))return false;
                if(old.count==0){if(old.itemValue.type!=0)return false;}
                else if(!RebirthStationGridIngredients.IsSameStackSnapshot(old,new ItemStack(physical,old.count)))return false;
                added+=(long)next.count-old.count;
                if(added>identity.Count)return false;
            }
            return added==identity.Count;
        }
        catch{return false;}
    }
}