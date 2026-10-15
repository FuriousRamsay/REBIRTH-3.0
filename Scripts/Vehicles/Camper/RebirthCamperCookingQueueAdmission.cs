using System;

// Cooking retains its existing registered native batch path. This exception
// does not admit grid-paid/native-job recipes or remote presentation writeback.
internal static class RebirthCamperCookingQueueAdmission
{
    internal static bool CanWrite(RebirthCamperStationState station,RecipeQueueItem[] incoming)
    {
        var world=station?.BoundVehicle?.world;
        var player=world?.GetPrimaryPlayer();
        if(!ThreadManager.IsMainThread() || world==null || world.IsRemote() || player==null
            || !RebirthCamperStationWindowSession.TryGetCurrent(player,out var current,out var address)
            || !ReferenceEquals(current,station) || !RebirthWorldCharacterService.TryGet(player,out var owner))return false;
        return Matches(station.Queue,owner,address)&&Matches(incoming,owner,address);
    }
    private static bool Matches(RecipeQueueItem[] rows,RebirthWorldCharacterRecord owner,RebirthCamperStationAddress address)
    {
        if(rows==null || owner?.Progression?.Cooking==null)return false;
        int jobs=0;
        foreach(var row in rows)
        {
            if(row==null || row.RepairItem!=null || row.AmountToRepair!=0)return false;
            var recipe=row.Recipe;if(recipe==null)continue;
            if(++jobs>1 || !RebirthCookingHeat.Managed(recipe) || RebirthStationGridQueue.IsMarked(recipe)
                || RebirthCamperStationNativeCrafting.TryJob(recipe,out _))return false;
            var receipt=RebirthCookingBatch.Receipt(recipe);
            string token;
            if(receipt==null || !receipt.TryGetMetadata("rebirth.cooking.token",out token)
                || !Guid.TryParse(token,out _) || !owner.Progression.Cooking.Tickets.TryGetValue(token,out var ticket)
                || ticket==null || ticket.StationPosition!=address.ToString()
                || ticket.Recipe!=recipe.GetName() || ticket.Portions!=row.Multiplier
                || ticket.OutputCount!=recipe.count)return false;
        }
        return true;
    }
    internal static bool IsCurrentCard(XUiC_RecipeStack card,RebirthCamperStationState station)
    {
        if(!RebirthCookingHeat.Managed(card?.recipe)||!CanWrite(station,station.Queue))return false;
        int matches=0;
        foreach(var row in station.Queue)
            if(ReferenceEquals(row?.Recipe,card.recipe)&&row.Multiplier==card.recipeCount)matches++;
        return matches==1;
    }
}
