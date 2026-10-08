using System;

// Evidence for a synchronous native cursor -> backpack deposit. No item writes.
internal static class RebirthGearCursorDepositWitness
{
    internal static bool TryLocate(RebirthGearInventorySnapshot before,RebirthGearInventorySnapshot after,
        string selectedData,int deposited,out int index)
    {
        index=-1;
        if(before==null||after==null||string.IsNullOrEmpty(selectedData)||deposited<=0||
            before.Bag==null||after.Bag==null||before.Belt==null||after.Belt==null||
            before.Bag.Length!=after.Bag.Length||before.Belt.Length!=after.Belt.Length||
            before.OwnedBeltSlots!=after.OwnedBeltSlots)return false;
        for(int i=0;i<before.Belt.Length;i++)if(!Same(before.Belt[i],after.Belt[i]))return false;
        long added=0;int found=-1;
        for(int i=0;i<before.Bag.Length;i++)
        {
            var old=before.Bag[i];var current=after.Bag[i];
            if(old==null||current==null||old.Count<0||current.Count<0)return false;
            if(Same(old,current))continue;
            if(current.ItemData!=selectedData||current.Count<=old.Count||
                (old.Count>0&&old.ItemData!=selectedData))return false;
            added+=(long)current.Count-old.Count;
            if(found<0)found=i;
        }
        if(added!=deposited||found<0)return false;
        index=found;return true;
    }
    private static bool Same(RebirthGearInventoryPlan.Stack a,RebirthGearInventoryPlan.Stack b)
        =>a!=null&&b!=null&&a.Count==b.Count&&(a.Count==0||a.ItemData==b.ItemData);
}