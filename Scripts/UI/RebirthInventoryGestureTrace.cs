using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using UnityEngine;

// Temporary bounded diagnostics requested by the owner. Never alters admission,
// native cursor state, inventory contents or transaction reservations.
internal static class RebirthInventoryGestureTrace
{
    private static int emitted;
    private static float window;
    private static int inWindow;
    internal static void Install()
    {
        var h=new Harmony("rebirth.inventory.gesture.trace");
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthInventoryGestureBoundaryTrace));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthInventoryPointerTrace));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthNativeInventoryPointerDropPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingItemResultSelection));
    }
    internal static void Write(XUiC_ItemStack slot,string phase,bool ran=true)
    {
        if(slot?.xui?.playerUI?.entityPlayer==null||emitted>=2000)return;
        float now=Time.realtimeSinceStartup;
        if(now-window>=1){window=now;inWindow=0;}
        if(inWindow++>=24)return;
        emitted++;
        var player=slot.xui.playerUI.entityPlayer;
        var held=slot.xui.DragAndDropWindow?.CurrentStack;
        var item=slot.ItemStack;
        Log.Out("[InventoryGesture] frame="+Time.frameCount+" phase="+phase+" nativeRan="+ran+
            " controller="+slot.GetType().Name+" slot="+slot.SlotNumber+" over="+slot.isOver+
            " visible="+slot.ViewComponent?.IsVisible+" press="+slot.ViewComponent?.EventOnPress+
            " item="+(item?.IsEmpty()==false?item.itemValue.ItemClass.GetItemName()+":"+item.count:"empty")+
            " cursor="+(held?.IsEmpty()==false?held.itemValue.ItemClass.GetItemName()+":"+held.count:"empty")+
            " shift="+InputUtils.ShiftKeyPressed+" sinkHold="+RebirthCamperSink.IsHeld(player)+
            " libraryHold="+RebirthBackpackLibraryReservation.IsHeld(player)+
            " gearHold="+RebirthGearOwnerReservation.IsHeld(player)+
            " resourceHold="+RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)+
            " energyHold="+RebirthCamperEnergyOwnerHold.Blocks(player.inventory)+
            " paymentHold="+RebirthCamperStationNativeActorPayment.Blocks(player.inventory));
    }
}
[HarmonyPatch]
internal static class RebirthInventoryGestureBoundaryTrace
{
    private static readonly HashSet<MethodBase> described=new HashSet<MethodBase>();
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
            yield return AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes);
    }
    [HarmonyPriority(Priority.First)]
    private static void Prefix(XUiC_ItemStack __instance,MethodBase __originalMethod)
        =>RebirthInventoryGestureTrace.Write(__instance,__originalMethod.Name+".before");
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(XUiC_ItemStack __instance,MethodBase __originalMethod,bool __runOriginal)
    {
        RebirthInventoryGestureTrace.Write(__instance,__originalMethod.Name+".after",__runOriginal);
        if(!__runOriginal&&described.Add(__originalMethod))
        {
            var patches=Harmony.GetPatchInfo(__originalMethod);
            if(patches!=null)Log.Out("[InventoryGesture] suppressed="+__originalMethod.Name+" candidatePrefixes="+
                string.Join(";",patches.Prefixes.Select(p=>p.owner+":"+p.PatchMethod.DeclaringType?.Name)));
        }
    }
}
[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.Update))]
internal static class RebirthInventoryPointerTrace
{
    private static void Postfix(XUiC_ItemStack __instance)
    {
        var cursor=__instance.xui?.playerUI?.CursorController;
        if(!__instance.isOver||cursor==null)return;
        if(cursor.GetMouseButtonUp(UICamera.MouseButton.LeftButton))
            RebirthInventoryGestureTrace.Write(__instance,"pointer.release");
    }
}
