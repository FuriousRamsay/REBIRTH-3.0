// Pure locator for the sole direct native return in a validated unequip plan.
internal static class RebirthGearUnequipReturnSlot
{
    internal static bool TryFind(RebirthGearInventoryPlan plan,out int index,out string itemData)
    {
        index=-1;itemData=null;
        if(plan==null||!plan.IsConserved()||plan.GearBefore==null||plan.GearBefore.Count!=1||
            plan.GearAfter==null||plan.GearAfter.Count!=0)return false;
        int found=-1;
        foreach(var change in plan.Changes)
        {
            if(!change.IsBag||change.Index<0||change.Index>=plan.BagSlotsAfter||change.Before.Count!=0||
                change.After.Count!=1||change.After.ItemData!=plan.GearBefore.ItemData)continue;
            if(found>=0)return false;
            found=change.Index;
        }
        if(found<0)return false;
        index=found;itemData=plan.GearBefore.ItemData;return true;
    }
}