using System;

// Cooking retains its existing registered native batch path. This exception
// does not admit grid-paid/native-job recipes or remote presentation writeback.
internal static class RebirthCamperCookingQueueAdmission
{
    internal static bool CanWrite(RebirthCamperStationState station)
    {
        var world=station?.BoundVehicle?.world;
        var player=world?.GetPrimaryPlayer();
        if(!ThreadManager.IsMainThread() || world==null || world.IsRemote() || player==null
            || !RebirthCamperStationWindowSession.TryGetCurrent(player,out var current,out _)
            || !ReferenceEquals(current,station))return false;
        return true; // The active native cooking window owns its ordinary queue setters.
    }
    internal static bool IsCurrentCard(XUiC_RecipeStack card,RebirthCamperStationState station)
        =>CanObserveCurrentCard(card,station);
    // A remote card is an authenticated projection, never permission to write
    // its queue back. Delivery additionally requires host ticket/file evidence.
    internal static bool CanObserveCurrentCard(XUiC_RecipeStack card,RebirthCamperStationState station)
    {
        if(!RebirthCookingHeat.Managed(card?.recipe)||station==null)return false;
        var player=card.xui?.playerUI?.entityPlayer;var world=station.BoundVehicle?.world;
        if(world?.IsRemote()==true)
        {
            var window=card.GetParentByType<XUiC_WorkstationWindowGroup>();
            if(player==null||window==null||!RebirthCamperStationWindowSession.Admitted(window,station)||
                !RebirthCamperStationWindowSession.TryGetCurrent(player,out var current,out var address)||!ReferenceEquals(current,station)||
                address.Installation!=station.InstallationId||string.IsNullOrEmpty(RebirthCamperCharacterAccess.CurrentCreation(player)))return false;
        }
        else
        {
            var localWindow=card.GetParentByType<XUiC_WorkstationWindowGroup>();
            if(world==null||player==null||!ReferenceEquals(player.world,world)||localWindow==null||
                !ReferenceEquals(localWindow.WorkstationData?.TileEntity,station)||localWindow.windowGroup?.isShowing!=true)return false;
        }
        int matches=0;
        foreach(var row in station.Queue)
            if(ReferenceEquals(row?.Recipe,card.recipe)&&row.Multiplier==card.recipeCount)matches++;
        return matches==1;
    }
    // Only the authenticated server observer retires a projected mobile row,
    // after final native owner phase3 proof. Incoming projection writes stay denied.
    internal static bool TryRetireObservedOutput(EntityPlayer player,RebirthCamperStationState station,Recipe recipe,Func<bool> saved)
    {
        if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=false||station==null||recipe==null||saved==null||
            !RebirthCamperRemoteStationTransport.TryGetCurrent(player,out var current,out _,out _,out _)||
            !ReferenceEquals(current,station))return false;
        int found=-1;for(int i=0;i<station.Queue.Length;i++)if(ReferenceEquals(station.Queue[i]?.Recipe,recipe)){if(found>=0)return false;found=i;}
        if(found<0||!saved())return false;
        var queue=(RecipeQueueItem[])station.Queue.Clone();queue[found]=new RecipeQueueItem{StartingEntityId=-1};
        station.Queue=queue;station.cycleRecipeQueue();station.ResetTickTime();station.setModified();
        RebirthCamperModuleRuntime.For(station.BoundVehicle).Persist(true);
        return true;
    }
}
