using System;
using HarmonyLib;
using Audio;

internal static class RebirthCamperStationCraftAction
{
    internal static bool IsMobile(ItemActionEntryCraft action,out EntityPlayerLocal player,
        out RebirthCamperStationAddress address,out string creation)
    {
        player=action?.ItemController?.xui?.playerUI?.entityPlayer;
        address=default;creation=null;
        var window=action?.ItemController?.GetParentByType<XUiC_WorkstationWindowGroup>();
        if(!(window?.WorkstationData?.TileEntity is RebirthCamperStationState station))return false;
        if(player!=null&&RebirthCamperStationWindowSession.TryGetCurrent(player,out var live,out var currentAddress)&&
            ReferenceEquals(live,station))
        {
            address=currentAddress;creation=RebirthCamperCharacterScope.ResolveLocal(player);
        }
        // A stale mobile window must be rejected, never sent through terrain crafting.
        return true;
    }
}

[HarmonyPatch(typeof(ItemActionEntryCraft),"hasItems")]
public static class RebirthCamperStationCraftIngredients
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemActionEntryCraft __instance,Recipe __1,ref bool __result)
    {
        if(!RebirthCamperStationCraftAction.IsMobile(__instance,out var player,out var address,out var creation))return true;
        if(player?.world?.IsRemote()==true)return address.IsValid;
        __result=address.IsValid&&(!RebirthSurvivorMode.IsEnabledForCurrentWorld()?
            RebirthCamperStationNativeCrafting.CanCraft(player,address,creation,__1,__instance.craftCountControl?.Count??0,__instance.craftingTier):
            RebirthStationPreparationService.CanPrepareMobile(player,address,creation,__1,__instance.craftCountControl?.Count??0,__instance.craftingTier));
        return false;
    }
}

[HarmonyPatch(typeof(ItemActionEntryCraft),nameof(ItemActionEntryCraft.OnActivated))]
public static class RebirthCamperStationCraftActivation
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemActionEntryCraft __instance)
    {
        if(!RebirthCamperStationCraftAction.IsMobile(__instance,out var player,out var address,out var creation))return true;
        var recipe=(__instance.ItemController as XUiC_RecipeEntry)?.Recipe;
        int count=__instance.craftCountControl?.Count??0;
        bool accepted=false;
        if(player?.world?.IsRemote()==true)
        {
            var window=__instance.ItemController.GetParentByType<XUiC_WorkstationWindowGroup>();
            try {accepted=RebirthCamperRemoteStationClient.Craft(player,window?.WorkstationData?.TileEntity as RebirthCamperStationState,recipe,count,__instance.craftingTier);}
            catch(Exception error){Log.Warning("[Camper] Remote craft request refused: "+error.Message);}
            if(!accepted)Manager.PlayInsidePlayerHead("ui_denied");
            __instance.ItemController.SetAllChildrenDirty();return false;
        }
        try
        {
            var job=Guid.NewGuid();
            accepted=!RebirthSurvivorMode.IsEnabledForCurrentWorld()?
                address.IsValid&&RebirthCamperStationNativeCrafting.Craft(player,address,creation,job,recipe,count,__instance.craftingTier):
                address.IsValid&&RebirthStationPreparationService.TryPrepareMobile(player,address,creation,job,
                recipe,count,out _,__instance.craftingTier)&&
                RebirthCamperStationPublicationWatch.TryPublishPrepared(player,address,creation,job);
        }
        catch(Exception error){Log.Warning("[Camper] Craft activation interrupted: "+error.GetType().Name);}
        if(accepted)
        {
            try
            {
                var window=__instance.ItemController.GetParentByType<XUiC_WorkstationWindowGroup>();
                RebirthCamperStationQueueProjection.Refresh(window,window?.WorkstationData?.TileEntity as RebirthCamperStationState);
            }
            catch(Exception error){Log.Warning("[Camper] Paid queue retained; presentation refresh failed: "+error.GetType().Name);}
        }
        if(!accepted&&player!=null)
        {
            Manager.PlayInsidePlayerHead("ui_denied");
            GameManager.ShowTooltip(player,Localization.Get("ttMissingCraftingResources"));
        }
        __instance.ItemController.SetAllChildrenDirty();
        return false;
    }
}

[HarmonyPatch(typeof(XUiC_RecipeCraftCount),"calcMaxCraftable")]
public static class RebirthCamperStationCraftCount
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_RecipeCraftCount __instance,ref int __result)
    {
        var window=__instance.GetParentByType<XUiC_WorkstationWindowGroup>();
        if(!(window?.WorkstationData?.TileEntity is RebirthCamperStationState station))return true;
        if(station.BoundVehicle?.world?.IsRemote()==true)return true;
        __result=0;
        var player=__instance.xui?.playerUI?.entityPlayer;
        if(player==null||!RebirthCamperStationWindowSession.TryGetCurrent(player,out var live,out var address)||
            !ReferenceEquals(live,station))return false;
        var recipe=__instance.recipe;
        if(recipe==null)return false;
        var details=window.GetChildByType<XUiC_RebirthCraftingRecipeDetails>();
        int tier=recipe?.GetOutputItemClass()?.HasQuality==false?0:
            details?.SelectedRecipe==recipe?details.SelectedCraftingTier:recipe?.craftingTier??-1;
        __result=!RebirthSurvivorMode.IsEnabledForCurrentWorld()?
            RebirthCamperStationNativeCrafting.Maximum(player,address,RebirthCamperCharacterScope.ResolveLocal(player),recipe,tier):
            RebirthStationPreparationService.GetMobileMaximum(player,address,RebirthCamperCharacterScope.ResolveLocal(player),recipe,tier);
        return false;
    }
}
