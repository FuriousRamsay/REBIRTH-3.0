using System;
using System.Reflection;
using HarmonyLib;

// The floating native item-info pane remains informational in vehicle restoration.
// Its action entries cannot operate on an installed-part proxy. The embedded
// backpack action list may operate only on a physical player-backpack slot;
// vehicle-part installation/removal still uses the authoritative transfer path.
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
        // The embedded backpack list acts on the player's real bag slot. Only a floating
        // item-info pane or read-only vehicle-part proxy must lose native item actions.
        if(itemController is XUiC_RebirthEditorBackpackSlot&&
            __instance.GetParentByType<XUiC_RebirthEditorBackpack>()!=null)return;
        __instance.SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.None,null);
    }
}
