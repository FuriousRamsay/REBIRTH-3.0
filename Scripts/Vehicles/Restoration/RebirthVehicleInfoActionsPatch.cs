using System;
using System.Reflection;
using HarmonyLib;

// The shared ItemInfoWindow (itemInfoPanel) builds Drop/Scrap/Take/Wear/Modify entries from the selected stack
// controller. In the vehicle restoration window the selected stack is either an editor-backpack cell or the hidden
// read-only proxy that stands in for an installed part, and every vehicle transfer goes through the server-
// authoritative custody protocol. The pane is therefore informational there: the action list is cleared right
// after the native build (also on each dirty rebuild, because ItemInfoWindow.Update re-calls SetItemStack).
// Same technique as RebirthHumanEquipmentInfoActionsPatch; it only fires for controllers under the vehicle window.
[HarmonyPatch]
internal static class RebirthVehicleInfoActionsPatch
{
    internal static bool Healthy{get;private set;}
    private static bool attempted;
    internal static void Install()
    {
        if(attempted)return;
        attempted=true;
        try
        {
            if(AccessTools.DeclaredMethod(typeof(XUiC_ItemActionList),nameof(XUiC_ItemActionList.SetCraftingActionList))==null)
            {Log.Error("[REBIRTH Vehicle UI] XUiC_ItemActionList.SetCraftingActionList not found; the shared item pane is disabled in the vehicle window.");return;}
            RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.vehicle.infoactions"),typeof(RebirthVehicleInfoActionsPatch));
            Healthy=true;
        }
        catch(Exception e){Log.Error("[REBIRTH Vehicle UI] info action patch failed ("+e.GetType().Name+"); the shared item pane is disabled in the vehicle window.");}
    }
    private static MethodBase TargetMethod()=>AccessTools.DeclaredMethod(typeof(XUiC_ItemActionList),nameof(XUiC_ItemActionList.SetCraftingActionList));
    private static void Postfix(XUiC_ItemActionList __instance,XUiC_ItemActionList.ItemActionListTypes _actionListType,XUiController itemController)
    {
        if(_actionListType!=XUiC_ItemActionList.ItemActionListTypes.Item)return;
        if(!(itemController is XUiC_RebirthEditorBackpackSlot)&&!(itemController is XUiC_RebirthReadonlyItemSlot))return;
        if(itemController.GetParentByType<XUiC_RebirthVehicleRestoration>()==null)return;
        __instance.SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.None,null);
    }
}
