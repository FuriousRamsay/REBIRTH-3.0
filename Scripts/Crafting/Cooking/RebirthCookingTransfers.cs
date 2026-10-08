using HarmonyLib;

// Retain native source-slot notifications; never let a cooking Shift-click fall through to output.
[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
public static class RebirthCookingTransfers
{
    public static bool Prefix(XUiC_ItemStack __instance)
    {
        var owner=XUiC_RebirthCookingWorkspace.ActiveInstance;
        if(owner?.IsCookingOpen!=true||owner.xui!=__instance.xui)return true;
        var location=__instance.StackLocation;
        if(location!=XUiC_ItemStack.StackLocationTypes.Backpack&&location!=XUiC_ItemStack.StackLocationTypes.ToolBelt)return true;
        if(__instance.ItemStack.IsEmpty()||__instance.StackLock)return false;
        
        var station=owner.windowGroup.Controller as XUiC_RebirthCookingStation;
        var remainder=__instance.ItemStack.Clone();int before=remainder.count;
        station.toolWindow?.TryAddTool(remainder.itemValue.ItemClass,remainder);
        if(remainder.count==before)
        {
            if(RebirthCookingCatalogue.IsIngredient(remainder))owner.MoveIngredient(remainder);
            else if(remainder.itemValue.ItemClass.FuelValue?.Value>0&&station.fuelWindow!=null)
            {
                // Native AddItem may retain the passed stack in an empty slot; do not clear it.
                var fuel=remainder.Clone();
                bool placed=station.fuelWindow.AddItem(fuel.itemValue.ItemClass,fuel);
                remainder.count=placed?0:fuel.count;
            }
        }
        if(remainder.count==before)return false;
        if(location==XUiC_ItemStack.StackLocationTypes.ToolBelt)__instance.ItemStack.Deactivate();
        __instance.ItemStack=remainder.count>0?remainder:ItemStack.Empty.Clone();
        __instance.HandleSlotChangeEvent();
        __instance.PlayPlaceSound();
        owner.RefreshStationState();
        return false;
    }
}
