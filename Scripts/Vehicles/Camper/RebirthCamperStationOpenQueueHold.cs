using HarmonyLib;
using System.Reflection;

// Native open-window output grants rewards before inserting the item and has
// a separate inventory-full retry. Neither route owns a mobile completion proof.
// Mobile cards present the vehicle queue and never run a second crafting clock.
internal static class RebirthCamperStationOpenQueueOwnership
{
    internal static bool IsMobile(XUiC_RecipeStack stack)
        =>stack?.windowGroup?.Controller is XUiC_WorkstationWindowGroup window&&
            window.WorkstationData?.TileEntity is RebirthCamperStationState;
    internal static bool IsPaidMobile(XUiC_RecipeStack stack)
        =>stack?.windowGroup?.Controller is XUiC_WorkstationWindowGroup window&&
            window.WorkstationData?.TileEntity is RebirthCamperStationState&&
            RebirthStationGridQueue.IsMarked(stack.recipe);
}
[HarmonyPatch(typeof(XUiC_RecipeStack),nameof(XUiC_RecipeStack.Update))]
internal static class RebirthCamperStationOpenQueueUpdateHold
{
    private static readonly MethodInfo refresh=AccessTools.Method(typeof(XUiC_RecipeStack),"updateRecipeData");
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_RecipeStack __instance)
    {
        if(!RebirthCamperStationOpenQueueOwnership.IsMobile(__instance))return true;
        // Native Update mixes binding refresh with rewards and time advancement.
        // Invoke only its binding refresh; projection supplies count and timer.
        refresh?.Invoke(__instance,null);
        __instance.IsDirty=true;
        return false;
    }
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"outputStack")]
internal static class RebirthCamperStationOpenQueueOutputHold
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_RecipeStack __instance,ref bool __result)
    {
        if(!RebirthCamperStationOpenQueueOwnership.IsMobile(__instance))return true;
        __result=false;return false;
    }
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"HandleOnPress")]
internal static class RebirthCamperStationOpenQueueCancellationHold
{
    // ForceCancel calls this same native boundary. Native refunds cannot settle
    // an immutable mobile paid job or authorize cancellation of its saved queue.
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_RecipeStack __instance)
    {
        if(!RebirthCamperStationOpenQueueOwnership.IsMobile(__instance))return true;
        if(!ThreadManager.IsMainThread()||
            !(__instance.windowGroup.Controller is XUiC_WorkstationWindowGroup window)||
            !(window.WorkstationData.TileEntity is RebirthCamperStationState station)||
            !RebirthCamperStationWindowSession.Admitted(window,station))return false;
        // Registered local cooking uses its existing Take/Cancel handler. A
        // stale card cannot grant output/refund against another live job.
        if(RebirthCookingHeat.Managed(__instance.recipe))
            return RebirthCamperCookingQueueAdmission.IsCurrentCard(__instance,station);
        var cards=window.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
        var rows=station.Queue;
        if(cards==null||rows==null||cards.Length!=rows.Length)return false;
        int selected=-1;
        for(int i=0;i<cards.Length;i++)if(ReferenceEquals(cards[i],__instance))
        {if(selected>=0)return false;selected=i;}
        if(selected>=0&&!RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
            RebirthCamperStationNativeCrafting.TryJob(__instance.recipe,out var shownNative)&&
            RebirthCamperStationNativeCrafting.TryJob(rows[selected]?.Recipe,out var actualNative)&&shownNative==actualNative)
        {
            var player=window.xui?.playerUI?.entityPlayer;
            if(station.BoundVehicle?.world?.IsRemote()==true)
                RebirthCamperRemoteStationClient.Cancel(player,station,actualNative.ToString("N"));
            else if(RebirthCamperStationWindowSession.TryGetCurrent(player,out var live,out var address)&&ReferenceEquals(live,station)&&
                RebirthCamperStationNativeCrafting.Cancel(player,address,RebirthCamperCharacterScope.ResolveLocal(player),actualNative))
                RebirthCamperStationQueueProjection.Refresh(window,station);
            return false;
        }
        if(selected>=0&&RebirthStationGridQueue.TryGetJobId(__instance.recipe,out var displayed)&&
            RebirthStationGridQueue.TryGetJobId(rows[selected]?.Recipe,out var actual)&&displayed==actual)
        {
            if(station.BoundVehicle?.world?.IsRemote()==true)
                RebirthCamperRemoteStationClient.Cancel(window.xui?.playerUI?.entityPlayer,station,actual);
            else if(RebirthCamperStationQueuedAuthority.TryGetCurrent(station,rows[selected],out var binding))
                RebirthCamperStationCancellationWatch.TryWatch(binding);
        }
        return false;
    }
}
