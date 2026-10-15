using System;
using HarmonyLib;

// Narrow native equipment boundary. All item/cursor setters remain native.
internal static class RebirthVehicleCursorGearHooks
{
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(XUiC_EquipmentStack),"SwapItem"),
            prefix:new HarmonyMethod(typeof(RebirthVehicleCursorGearHooks),nameof(Before)),
            finalizer:new HarmonyMethod(typeof(RebirthVehicleCursorGearHooks),nameof(After)));
    }
    private sealed class State {internal EntityPlayerLocal Owner;internal Guid Operation;}
    private static bool Before(XUiC_EquipmentStack __instance,out State __state)
    {
        __state=null;
        // Unknown custom overrides have not been qualified against this native plan.
        if(__instance==null||__instance.GetType()!=typeof(XUiC_EquipmentStack))return true;
        try
        {
            var owner=__instance.xui?.playerUI?.entityPlayer;
            if(RebirthVehicleClientCustody.IsPendingCursorHandoff(owner))return false;
            // Ordinary equipment interactions must not serialize equipment or
            // load/scan custody journals. Full admission follows only a live
            // memory-owned cursor match; the custody service rechecks its scope.
            if(!RebirthVehicleClientCustody.HasLiveCursorCustody(owner))return true;
            var cursor=__instance.xui?.DragAndDropWindow?.CurrentStack;
            var stored=__instance.ItemStack;
            if(owner==null||cursor==null||cursor.IsEmpty()||cursor.count!=1||stored==null)return true;
            if(!__instance.AllowRemoveItem&&!stored.IsEmpty())
            {
                var armor=stored.itemValue.ItemClass as ItemClassArmor;
                if(armor?.ReplaceByTag!=null)
                {
                    var incoming=cursor.itemValue.ItemClass as ItemClassArmor;
                    if(incoming!=null&&!FastTags<TagGroup.Global>.Parse(armor.ReplaceByTag).Test_AnySet(incoming.ItemTags))return true;
                }
            }
            ItemStack afterTarget=cursor.Clone();
            ItemStack afterCursor=__instance.AllowRemoveItem?stored.Clone():ItemStack.Empty.Clone();
            if(!RebirthVehicleCursorGearWitness.TryCapture(__instance,afterTarget,out var witness))return true;
            if(!RebirthVehicleClientCustody.BeforeDestinationDrop(__instance,afterTarget,afterCursor,witness,out var operation))return false;
            if(operation!=Guid.Empty)__state=new State{Owner=owner,Operation=operation};
            return true;
        }
        catch(Exception error){RebirthVehicleClientCustody.Warn(error);return false;}
    }
    private static Exception After(Exception __exception,State __state)
    {
        if(__state!=null)
        {
            try{RebirthVehicleClientCustody.AfterDestinationDrop(__state.Owner,__state.Operation);}
            catch(Exception error){RebirthVehicleClientCustody.Warn(error);}
        }
        return __exception;
    }
}
