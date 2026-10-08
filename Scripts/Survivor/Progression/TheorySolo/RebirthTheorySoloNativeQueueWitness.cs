using System;

// Exact original enqueue/remaining-queue witness, never a completion witness.
internal static class RebirthTheorySoloNativeQueueWitness
{
    internal static bool TryReadOriginal(RebirthStablePlayerIdentity owner,RebirthTheorySoloOriginalTaskLedger.Task task,int expectedRemaining,out RecipeQueueItem witness)
    {
        witness=null;
        if(owner==null||task==null||task.Kind!="personal"||expectedRemaining<1||expectedRemaining>task.Portions||!RebirthGearNativePlayerFile.TryRead(owner,out var saved))return false;
        return TryMatch(saved,task,expectedRemaining,out witness);
    }
    internal static bool TryReadPaidOriginal(RebirthStablePlayerIdentity owner,RebirthTheorySoloOriginalTaskLedger.Task task,int expectedRemaining,out RecipeQueueItem witness)
    {
        witness=null;
        return owner!=null&&RebirthGearNativePlayerFile.TryRead(owner,out var saved)&&TryMatchPaidOriginal(saved,task,expectedRemaining,out witness);
    }
    internal static bool TryMatchPaidOriginal(PlayerDataFile saved,RebirthTheorySoloOriginalTaskLedger.Task task,int expectedRemaining,out RecipeQueueItem witness)
    {
        witness=null;
        try
        {
            if(!TryMatch(saved,task,expectedRemaining,out var queued)||!RebirthTheorySoloNativePaymentPlan.TryHash(RebirthPlayerDataInventory.ReadSlots(saved,true),RebirthPlayerDataInventory.ReadSlots(saved,false),out var inventory)||inventory!=task.AfterInventoryHash)return false;
            witness=queued;return true;
        }
        catch{return false;}
    }
    internal static bool TryMatch(PlayerDataFile saved,RebirthTheorySoloOriginalTaskLedger.Task task,int expectedRemaining,out RecipeQueueItem witness)
    {
        witness=null;var queue=saved?.craftingData?.RecipeQueueItems;
        if(task==null||task.Kind!="personal"||queue==null||queue.Length>128||expectedRemaining<1||expectedRemaining>task.Portions)return false;
        foreach(var row in queue)
        {
            if(row?.Recipe?.ingredients==null)continue;
            string marker=null;int markers=0;
            foreach(var ingredient in row.Recipe.ingredients)
            {
                if(ingredient?.itemValue?.Metadata==null||!ingredient.itemValue.Metadata.ContainsKey(RebirthTheorySoloTaskMarker.Key))continue;
                markers++;if(!ingredient.itemValue.TryGetMetadata(RebirthTheorySoloTaskMarker.Key,out marker))return false;
            }
            if(markers==0)continue;
            if(markers!=1||!RebirthTheorySoloTaskMarker.TryDecode(marker,out var ordinal,out var creation,out var lease))return false;
            if(ordinal!=task.Ordinal||creation!=task.Creation||lease!=task.Lease)continue;
            if(witness!=null||row.RepairItem!=null||row.AmountToRepair!=0||row.StartingEntityId!=task.OriginalActor||row.Multiplier!=expectedRemaining||Single.IsNaN(row.CraftingTimeLeft)||Single.IsInfinity(row.CraftingTimeLeft)||row.CraftingTimeLeft<0||Single.IsNaN(row.OneItemCraftTime)||Single.IsInfinity(row.OneItemCraftTime)||row.OneItemCraftTime<0||!RebirthTheorySoloTaskMarker.TryRecipeBinding(row.Recipe,out var binding)||binding!=task.RecipeBinding)return false;
            witness=row;
        }
        return witness!=null;
    }
}
