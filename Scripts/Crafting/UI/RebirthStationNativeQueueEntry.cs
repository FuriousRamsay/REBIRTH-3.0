using System;
using System.Collections.Generic;

// Detached native entry. This is not queue admission, payment or completion evidence.
public static class RebirthStationNativeQueueEntry
{
    // Capacity observation only; no slot is reserved or overwritten by this method.
    public static bool TryFindVacantSlot(IList<RecipeQueueItem> queue,Guid job,out int slot)
    {
        slot=-1;if(queue==null||queue.Count<1||queue.Count>byte.MaxValue||job==Guid.Empty)return false;
        string id=job.ToString("N");
        for(int i=0;i<queue.Count;i++)
        {
            var current=queue[i];
            if(current==null||current.Multiplier<0)return false;
            if(current.Recipe!=null&&RebirthStationGridQueue.IsMarked(current.Recipe)&&
                (!RebirthStationGridQueue.TryGetJobId(current.Recipe,out var existing)||existing==id))return false;
        }
        for(int i=queue.Count-1;i>=0;i--)
        {
            var current=queue[i];
            if(current.Recipe==null&&current.Multiplier==0&&!current.IsCrafting&&current.RepairItem==null&&current.AmountToRepair==0)
            {slot=i;return true;}
        }
        return false;
    }
    // Stages a replacement array; unrelated entries retain their exact native objects.
    // This is not an immutable snapshot: caller must separately recheck the whole live queue at commit.
    internal static bool TryBuildReplacement(IList<RecipeQueueItem> queue,Guid job,RecipeQueueItem entry,
        out RecipeQueueItem[] replacement)
    {
        replacement=null;
        if(entry?.Recipe==null||entry.Multiplier!=1||entry.RepairItem!=null||entry.AmountToRepair!=0||
            entry.StartingEntityId<=0||entry.IsCrafting||
            !RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var id)||id!=job.ToString("N")||
            !TryFindVacantSlot(queue,job,out var slot))return false;
        var staged=new RecipeQueueItem[queue.Count];
        for(int i=0;i<queue.Count;i++)staged[i]=queue[i];
        staged[slot]=new RecipeQueueItem{Recipe=entry.Recipe,Multiplier=entry.Multiplier,Quality=entry.Quality,
            CraftingTimeLeft=entry.CraftingTimeLeft,OneItemCraftTime=entry.OneItemCraftTime,
            StartingEntityId=entry.StartingEntityId,IsCrafting=slot==queue.Count-1};
        replacement=staged;return true;
    }
    // Detached cancellation image only; caller proves original queue, saved refund and native commit separately.
    internal static bool TryBuildCancellation(IList<RecipeQueueItem> queue,RebirthStationGridAdmission admission,
        RebirthStationTerminalIntent intent,int actor,out RecipeQueueItem[] replacement)
    {
        replacement=null;
        if(queue==null||queue.Count<1||queue.Count>255||admission==null||intent==null||intent.IsCompletion||actor<=0||
            !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _)||(int)intent.Write().Attribute("actor")!=actor)return false;
        int slot=-1;
        for(int i=0;i<queue.Count;i++)
        {
            var entry=queue[i];
            if(entry==null||entry.Multiplier<0||float.IsNaN(entry.CraftingTimeLeft)||float.IsInfinity(entry.CraftingTimeLeft)||
                float.IsNaN(entry.OneItemCraftTime)||float.IsInfinity(entry.OneItemCraftTime))return false;
            if(entry.Recipe==null||!RebirthStationGridQueue.IsMarked(entry.Recipe))continue;
            if(!RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var job))return false;
            if(job!=admission.JobId)continue;
            if(slot>=0||entry.StartingEntityId!=actor||entry.Multiplier!=1||entry.RepairItem!=null||entry.AmountToRepair!=0||
                !admission.MatchesQueuedQuality(entry.Recipe,entry.Multiplier,entry.Quality))return false;
            slot=i;
        }
        if(slot<0)return false;
        var staged=new RecipeQueueItem[queue.Count];for(int i=0;i<queue.Count;i++)staged[i]=queue[i];
        staged[slot]=new RecipeQueueItem{Recipe=null,Multiplier=0,CraftingTimeLeft=0,OneItemCraftTime=0,
            IsCrafting=false,Quality=0,StartingEntityId=-1};
        replacement=staged;return true;
    }
    public static bool TryCreate(Recipe recipe,IList<Recipe> definitions,int startingPlayerId,
        out RecipeQueueItem entry)
    {
        entry=null;
        if(recipe==null||startingPlayerId<=0||!RebirthStationGridQueue.IsMarked(recipe)||
            !RebirthStationGridQueue.TryGetJobId(recipe,out var job)||
            !RebirthStationGridQueue.TryGetDefinitionBinding(recipe,definitions,out var binding)||
            !RebirthStationGridQueue.HasResolvedDefinition(recipe)||recipe.count<1||recipe.count>32767||
            float.IsNaN(recipe.craftingTime)||float.IsInfinity(recipe.craftingTime)||recipe.craftingTime<0||
            binding.Tier<0||binding.Tier>6)return false;
        entry=new RecipeQueueItem{Recipe=recipe,Multiplier=1,Quality=(byte)binding.Tier,
            CraftingTimeLeft=recipe.craftingTime,OneItemCraftTime=recipe.craftingTime,
            StartingEntityId=startingPlayerId,IsCrafting=false};
        return true;
    }
}