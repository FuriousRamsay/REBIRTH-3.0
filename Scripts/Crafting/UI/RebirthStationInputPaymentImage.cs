using System;
using System.Collections.Generic;

// Builds a detached native input image; caller must own station custody and publication.
public static class RebirthStationInputPaymentImage
{
    public static bool TryMerge(IList<ItemStack> current,int physicalSlots,int materialCount,
        IList<ItemStack> expected,IList<ItemStack> remainder,out ItemStack[] result)
    {
        result=null;
        if(materialCount>0&&expected!=null&&remainder!=null&&expected.Count==physicalSlots+materialCount&&
            remainder.Count==expected.Count&&current!=null&&current.Count==expected.Count)
        {
            if(!RebirthStationMaterialInputCodec.TryWrite(remainder,physicalSlots,out _))return false;
            var whole=new ItemStack[current.Count];
            for(int i=0;i<whole.Length;i++)
            {
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(current[i],expected[i])||
                    remainder[i]?.itemValue==null||current[i]?.itemValue==null||
                    remainder[i].count<0||remainder[i].count>current[i].count)return false;
                var comparable=remainder[i].Clone();comparable.count=current[i].count;
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(current[i],comparable)||
                    (i<physicalSlots&&!RebirthStationGridIngredients.IsSameStackSnapshot(current[i],remainder[i])))return false;
                whole[i]=remainder[i].Clone();
            }
            result=whole;return true;
        }
        if(!RebirthStationInputLayout.TryReadPhysical(current,physicalSlots,materialCount,out var physical)||
            expected==null||remainder==null||expected.Count!=physicalSlots||remainder.Count!=physicalSlots||
            !RebirthStationNativeInputCodec.IsRoundTrippable(remainder))return false;
        for(int i=0;i<physicalSlots;i++)
        {
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(physical[i],expected[i])||
                remainder[i]==null||remainder[i].itemValue==null||remainder[i].count<0||remainder[i].count>physical[i].count)return false;
            if(remainder[i].count>0)
            {
                var same=physical[i].Clone();same.count=remainder[i].count;
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(same,remainder[i]))return false;
            }
        }
        var merged=new ItemStack[current.Count];
        for(int i=0;i<merged.Length;i++)merged[i]=(i<physicalSlots?remainder[i]:current[i]).Clone();
        result=merged;return true;
    }
}
