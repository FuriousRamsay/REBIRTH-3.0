using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class RebirthCookingGridCustody
{
    // No old UI delegate is resumed. This is a fresh current-life native credit
    // against the exact still-owed source, using the existing grid owner permit.
    internal static bool ReturnHistoricalFetched(EntityPlayerLocal player,long request,IList<ItemStack> fetched,int sourceDeaths)
    {
        try
        {
            if(!CurrentOwner(player)||!RebirthCookingLegacyPull.ValidateHistoricalReturnOriginal(player,request,fetched,sourceDeaths)||
                IsHeld(player))return false;
            if(HasFetchClaim(player,request,out bool complete))return complete;
            var before=RebirthCamperEnergyActorImage.Capture(player);var after=before.Copy();
            if(!RebirthRecyclerOwnerDelivery.Plan(before.Bag,before.Belt,before.BagLocks,RebirthCamperEnergyItems.Locks(player.inventory.ItemGrid.SlotLocks),player.inventory.Length,
                fetched,out after.Bag,out after.Belt))return false;
            var empty=Enumerable.Range(0,12).Select(_=>ItemStack.Empty.Clone()).ToArray();
            return Begin(null,player,"fetched-return",before,after,empty,ItemStack.Clone(empty),request,fetched,sourceDeaths:sourceDeaths);
        }
        catch{return false;}
    }
}
