using System;

// Observe native presentation and authoritative queue separately. No inventory
// writes, clock changes or relaxation of paid-queue admission happen here.
internal static class RebirthCamperCookingTrace
{
    internal static void Observe(XUiC_WorkstationWindowGroup window,string stage,Recipe offered)
    {
        if(!RebirthCamperTrace.Enabled || !(window?.WorkstationData?.TileEntity is RebirthCamperStationState station))return;
        try
        {
            int visible=0,authoritative=0;
            bool shown=false,stored=false;
            var cards=window.craftingQueue?.GetRecipesToCraft();
            if(cards!=null)foreach(var card in cards)
                if(card?.recipe!=null){visible++;if(ReferenceEquals(card.recipe,offered))shown=true;}
            if(station.Queue!=null)foreach(var row in station.Queue)
                if(row?.Recipe!=null){authoritative++;if(ReferenceEquals(row.Recipe,offered))stored=true;}
            RebirthCamperTrace.Write(stage,station.BoundVehicle,
                "station="+station.InstallationId.ToString("N")+" offered="+offered?.GetName()
                +" uiJobs="+visible+" savedModelJobs="+authoritative+" offeredInUI="+shown+" offeredInModel="+stored
                +" openQueueOwner="+RebirthCamperStationOpenQueueProcessing.OwnsQueue(station));
        }
        catch(Exception error){RebirthCamperTrace.Write(stage+".observation-failed",station.BoundVehicle,error.GetType().Name);}
    }
}
