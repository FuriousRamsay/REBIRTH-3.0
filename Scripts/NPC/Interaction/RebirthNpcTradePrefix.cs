using System;

// Detached original-prefix comparison. No native setter, load, permission or
// receipt producer. Odd entered steps deliberately cannot become replayable.
internal static class RebirthNpcTradePrefix
{
    internal static int Changes(RebirthNpcTradeOriginal original)
    {
        int count=0;
        foreach(bool bag in new[]{true,false})
        {
            var before=original.CopySlots(bag,false);var after=original.CopySlots(bag,true);
            for(int slot=0;slot<before.Length;slot++)if(!Same(before[slot],after[slot]))count++;
        }
        return count;
    }
    internal static bool Matches(RebirthNpcTradeOriginal original,ItemStack[] bag,ItemStack[] belt,int step)
    {
        if(original==null||step<0||(step&1)!=0)return false;
        int progress=step/2,changed=0;
        foreach(bool backpack in new[]{true,false})
        {
            var before=original.CopySlots(backpack,false);var after=original.CopySlots(backpack,true);var actual=backpack?bag:belt;
            if(actual==null||actual.Length!=before.Length)return false;
            for(int slot=0;slot<before.Length;slot++)
            {
                bool differs=!Same(before[slot],after[slot]);
                var expected=differs&&changed<progress?after[slot]:before[slot];
                if(!Same(actual[slot],expected))return false;
                if(differs)changed++;
            }
        }
        return progress<=changed;
    }
    private static bool Same(ItemStack left,ItemStack right)=>RebirthStationGridIngredients.IsSameStackSnapshot(left,right);
}
