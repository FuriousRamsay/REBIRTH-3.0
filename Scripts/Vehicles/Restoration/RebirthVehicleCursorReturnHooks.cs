using System;
using HarmonyLib;

// Observes the game's actual automatic close/open/out-of-menu return. No copied
// AddItem, fallback gesture, native item setter, or world-drop is introduced.
internal static class RebirthVehicleCursorReturnHooks
{
    internal static void Install(Harmony harmony)
    {
        var place=AccessTools.DeclaredMethod(typeof(XUiC_DragAndDropWindow),"PlaceItemBackInInventory",Type.EmptyTypes);
        var drop=AccessTools.DeclaredMethod(typeof(XUiM_PlayerInventory),"DropItem",new[]{typeof(ItemStack)});
        var set=AccessTools.DeclaredMethod(typeof(XUiC_DragAndDropWindow),"SetCurrentStack",new[]{typeof(ItemStack),typeof(bool)});
        var close=AccessTools.DeclaredMethod(typeof(GUIWindowManager),"Close",new[]{typeof(GUIWindow),typeof(bool)});
        if(place==null||drop==null||set==null||close==null)throw new MissingMethodException("Native cursor automatic return anchors unavailable.");
        harmony.Patch(place,prefix:new HarmonyMethod(typeof(RebirthVehicleCursorReturnHooks),nameof(Before)),finalizer:new HarmonyMethod(typeof(RebirthVehicleCursorReturnHooks),nameof(After)));
        harmony.Patch(drop,prefix:new HarmonyMethod(typeof(RebirthVehicleCursorReturnHooks),nameof(Drop)));
        harmony.Patch(set,prefix:new HarmonyMethod(typeof(RebirthVehicleCursorReturnHooks),nameof(Set)));
        harmony.Patch(close,prefix:new HarmonyMethod(typeof(RebirthVehicleCursorReturnHooks),nameof(Close)));
    }
    private static bool Before(XUiC_DragAndDropWindow __instance,out Guid __state)
    {
        __state=Guid.Empty;
        try{return RebirthVehicleClientCustody.BeforeNativeReturn(__instance,out __state);}catch(Exception error){RebirthVehicleClientCustody.Warn(error);return false;}
    }
    private static Exception After(XUiC_DragAndDropWindow __instance,Guid __state,Exception __exception)
    {try{RebirthVehicleClientCustody.AfterNativeReturn(__instance?.xui?.playerUI?.entityPlayer,__state);}catch(Exception error){RebirthVehicleClientCustody.Warn(error);}return __exception;}
    private static bool Drop(XUiM_PlayerInventory __instance,ItemStack _stack)
        =>RebirthVehicleClientCustody.AllowReturnDrop(__instance,_stack);
    private static bool Set(XUiC_DragAndDropWindow __instance,ItemStack stack)
        =>RebirthVehicleClientCustody.AllowReturnCursorSet(__instance,stack);
    private static bool Close(GUIWindowManager __instance,GUIWindow _w,bool _fromEsc)
    {try{return RebirthVehicleClientCustody.AllowWindowClose(__instance,_w,_fromEsc);}catch(Exception error){RebirthVehicleClientCustody.Warn(error);return false;}}
}
