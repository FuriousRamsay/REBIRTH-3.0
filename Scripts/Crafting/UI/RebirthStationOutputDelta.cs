using System;
using System.Collections.Generic;
using System.Linq;

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
            if(!RebirthRecipeOutputIntegration.ReadReceipt(receipt,out var extras))return false;
            physical.Metadata.Remove(RebirthStationCompletionReceipt.Prefix+RebirthRecipeOutputIntegration.ReceiptField);
            if(extras.Count>0)
            {
                var cells=new ItemStack[before.Count];for(int i=0;i<cells.Length;i++)cells[i]=before[i]?.Clone();
                if(cells.Any(cell=>cell==null)||!RebirthRecipeOutputIntegration.Predict(cells,new ItemStack(physical,identity.Count),-1,extras,out var expected,out _))return false;
                for(int i=0;i<expected.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(expected[i],after[i]))return false;
                return true;
            }
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
