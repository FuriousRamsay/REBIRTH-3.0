using System;
using System.Linq;

// Read-only placed native custody. These predicates neither admit a fetched
// original nor grant payment, queue setters, an incarnation or save evidence.
internal static class RebirthRemoteResourcePlacedStationScope
{
    internal enum PaymentSource:byte{OwnerInventory=0,StationInput=1}
    internal static bool TryGetWindowName(World world,TileEntityWorkstation station,out string windowName)
    {
        windowName=null;
        try
        {
            if(world==null||station==null||station is RebirthCamperStationState||!ReferenceEquals(GameManager.Instance?.World,world)||
                !ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station))return false;
            string blockName=world.GetBlock(station.ToWorldPos()).Block.GetBlockName();
            var definition=CraftingManager.GetWorkstationData(blockName);if(definition==null)return false;
            windowName=string.IsNullOrEmpty(definition.WorkstationWindow)?"workstation_"+blockName:definition.WorkstationWindow;
            if(blockName=="forge"&&XUiM_Recipes.DisableSmelter)windowName+="_nosmelting";
            return !string.IsNullOrEmpty(windowName)&&windowName.Length<=512;
        }
        catch{return false;}
    }
    internal static bool TryResolveLayout(EntityPlayer actor,Vector3i position,string creation,out TileEntityWorkstation station,
        out string windowName,out PaymentSource payment,out bool turnsOn)
    {
        station=null;windowName=null;payment=default;turnsOn=false;
        try
        {
            if(!TryResolveNativeLease(actor,position,creation,out var actual))return false;
            if(!TryGetWindowName(actor.world,actual,out string window))return false;
            // Same installed merged native template capture used on dedicated
            // servers. Never infer actual controllers from Modules booleans.
            if(!RebirthCamperStationTemplateSourcePolicy.TryGetLayout(window,out bool actorSource,out bool fuel))return false;
            station=actual;windowName=window;payment=actorSource?PaymentSource.OwnerInventory:PaymentSource.StationInput;turnsOn=fuel;return true;
        }
        catch{return false;}
    }
    internal static bool TryObserveWindow(EntityPlayerLocal player,ItemActionEntryCraft entry,out TileEntityWorkstation station,out PaymentSource payment)
        =>TryObserveWindow(player,entry,out station,out payment,out _);
    internal static bool TryObserveWindow(EntityPlayerLocal player,ItemActionEntryCraft entry,out TileEntityWorkstation station,out PaymentSource payment,out bool turnsOn)
    {
        station=null;payment=default;turnsOn=false;
        try
        {
            if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=true||player.IsDead()||!player.IsSpawned()||
                !ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player)||!ReferenceEquals(entry?.ItemController?.xui?.playerUI?.entityPlayer,player))return false;
            var window=entry.ItemController.GetParentByType<XUiC_WorkstationWindowGroup>();
            var visible=entry.ItemController.xui.GetChildrenByType<XUiC_CraftingWindowGroup>()?.FirstOrDefault(value=>value?.WindowGroup?.isShowing==true);
            var candidate=window?.WorkstationData?.TileEntity;
            if(candidate==null||candidate is RebirthCamperStationState||!ReferenceEquals(visible,window)||
                !ReferenceEquals(entry.craftCountControl?.WindowGroup?.Controller,window)||
                !ReferenceEquals(player.world.GetTileEntity(candidate.ToWorldPos()),candidate)||LockManager.Instance?.IsLockedByLocalPlayer(candidate,0)!=true)return false;
            var actualGrid=entry.craftCountControl.WindowGroup.Controller.GetChildByType<XUiC_WorkstationInputGrid>();
            if(!ReferenceEquals(actualGrid,window.inputWindow))return false;
            // Native AddItemToQueue calls TurnOn whenever the actual window
            // has a fuel controller, including when its fuel is empty.
            turnsOn=window.fuelWindow!=null;
            station=candidate;payment=actualGrid==null?PaymentSource.OwnerInventory:PaymentSource.StationInput;return true;
        }
        catch{station=null;return false;}
    }
    // Initial callers may inspect an authenticated native lock before an
    // incarnation admission. This method never mints or adopts a token.
    internal static bool TryResolveNativeLease(EntityPlayer actor,Vector3i position,string creation,out TileEntityWorkstation station)
    {
        station=null;
        try
        {
            var world=actor?.world;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||
                !ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthOriginalActionScope.Matches(actor,owner.StorageKey,creation))return false;
            float distance=(actor.position-position.ToVector3()).sqrMagnitude;
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance>64f)return false;
            var candidate=world.GetTileEntity(position) as TileEntityWorkstation;var locks=LockManager.Instance;
            if(candidate==null||candidate is RebirthCamperStationState||locks==null||candidate.IsSharedLock(0)||
                !locks.singleLocks.TryGetByKey(actor.entityId,out var entries)||entries==null||
                !entries.Any(value=>ReferenceEquals(value.Target,candidate)&&value.Channel==0))return false;
            var persistent=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;
            if(persistent==null||!RebirthWorkstationSecurityService.CanAccessWorkstation(world,position,actor,persistent,false,RebirthSecureAccessPurpose.WorkstationAccess,out _))return false;
            station=candidate;return true;
        }
        catch{station=null;return false;}
    }
    internal static bool TryResolve(EntityPlayer actor,Vector3i position,string creation,string incarnation,out TileEntityWorkstation station)
    {
        station=null;
        if(!Guid.TryParseExact(incarnation,"N",out var token)||token==Guid.Empty||token.ToString("N")!=incarnation||
            !TryResolveNativeLease(actor,position,creation,out var actual)||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,actual,out string current)||current!=incarnation)return false;
        station=actual;return true;
    }
    internal static bool AllowsRecipe(EntityPlayer actor,TileEntityWorkstation station,Recipe recipe)
    {
        try
        {
            if(actor==null||station==null||station is RebirthCamperStationState||recipe==null||station.block==null||
                !recipe.IsUnlocked(actor)||!RebirthRecipeDiscoveryRules.Allows(actor,recipe.GetName())||
                !RebirthStationToolAvailability.HasRequirement(station.Tools,recipe))return false;
            string areas=station.block.Properties.Contains("Workstation","CraftingAreaRecipes")?
                station.block.Properties.GetString("Workstation","CraftingAreaRecipes"):station.block.GetBlockName();
            return !string.IsNullOrEmpty(areas)&&areas.Split(',').Any(value=>string.Equals(value.Trim()=="player"?"":value.Trim(),recipe.craftingArea??"",StringComparison.OrdinalIgnoreCase));
        }
        catch{return false;}
    }
}
