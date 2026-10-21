using System;
using System.Reflection;
using HarmonyLib;

internal static class RebirthCamperStationQueueProjection
{
    private static readonly MethodInfo refreshGrids=AccessTools.Method(typeof(XUiC_WorkstationWindowGroup),"syncUIfromTE");
    internal static bool HasPaid(RecipeQueueItem[] queue)
    {
        if(queue==null)return false;
        foreach(var row in queue)if(RebirthStationGridQueue.IsMarked(row?.Recipe))return true;
        return false;
    }
    // Projection only: no ingredient debit, queue mutation, output or award.
    internal static void Refresh(XUiC_WorkstationWindowGroup window,RebirthCamperStationState station)
    {
        if(!ThreadManager.IsMainThread()||window==null||station==null||
            !RebirthCamperStationWindowSession.Admitted(window,station))throw new InvalidOperationException("Current mobile window required.");
        var cards=window.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
        var queue=station.Queue;
        if(refreshGrids==null||cards==null||queue==null||cards.Length!=queue.Length)
            throw new InvalidOperationException("Native mobile queue presentation is unavailable.");
        foreach(var row in queue)
            if(row==null||row.RepairItem!=null||row.AmountToRepair!=0)
                throw new InvalidOperationException("Preserve unsupported queue presentation.");
        refreshGrids.Invoke(window,null);
        for(int i=0;i<queue.Length;i++)
        {
            var row=queue[i];
            if(cards[i]==null||!cards[i].SetRecipe(row.Recipe,row.Multiplier,row.CraftingTimeLeft,true,
                row.StartingEntityId,row.Quality,row.OneItemCraftTime))throw new InvalidOperationException("Native mobile queue card refused projection.");
            cards[i].IsCrafting=false;
            cards[i].IsDirty=true;
        }
    }
}
[HarmonyPatch(typeof(XUiM_Workstation),nameof(XUiM_Workstation.SetRecipeQueueItems))]
internal static class RebirthCamperStationQueueWriteback
{
    private static bool Prefix(XUiM_Workstation __instance,RecipeQueueItem[] __0)
    {
        if(!(__instance?.TileEntity is RebirthCamperStationState station))return true;
        // Host-local cooking already uses the native workstation sync path.
        // A transient camper window session must not veto its queue setter.
        if(station.block is BlockCampfire&&station.BoundVehicle?.world?.IsRemote()==false)
            return true;
        if(RebirthCamperStationFuelOriginal.IsHeld(station)||RebirthCamperStationWindowSession.HasOriginalProjection(station))return false;
        if(RebirthCamperStationOpenQueueProcessing.OwnsQueue(station))
            return RebirthCamperCookingQueueAdmission.CanWrite(station);
        // Native UI serializes replacement row objects. It cannot replace an
        // authoritative paid queue or inject a paid row through ordinary sync.
        if(station.Queue!=null)foreach(var row in station.Queue)if(RebirthCamperStationNativeCrafting.TryJob(row?.Recipe,out _))return false;
        if(__0!=null)foreach(var row in __0)if(RebirthCamperStationNativeCrafting.TryJob(row?.Recipe,out _))return false;
        return !RebirthCamperStationQueueProjection.HasPaid(station.Queue)&&!RebirthCamperStationQueueProjection.HasPaid(__0);
    }
}
