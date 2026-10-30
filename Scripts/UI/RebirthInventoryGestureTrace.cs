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
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCreativeNativeSwapTrace));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthInventoryPointerTrace));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthNativeInventoryPointerDropPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthCookingItemResultSelection));
    }
    internal static void Write(XUiC_ItemStack slot,string phase,bool ran=true)
    {
        if(!RebirthLogSettings.UiRouteLoggingEnabled)return;
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

[HarmonyPatch(typeof(XUiC_Creative2Stack),"SwapItem")]
internal static class RebirthCreativeNativeSwapTrace
{
    private static void Prefix(XUiC_Creative2Stack __instance)
        {if(RebirthLogSettings.UiRouteLoggingEnabled)RebirthCreativeTransferTrace.Write(__instance.xui,"creative.SwapItem.before",__instance);}
    private static void Postfix(XUiC_Creative2Stack __instance,bool __runOriginal)
        {if(RebirthLogSettings.UiRouteLoggingEnabled)RebirthCreativeTransferTrace.Write(__instance.xui,"creative.SwapItem.after",__instance,"nativeRan="+__runOriginal);}
}

// Owner-requested Creative transfer probe. Input-edge only and bounded, so the
// 182 pooled catalogue cells do not turn normal frame updates into log spam.
internal static class RebirthCreativeTransferTrace
{
    private static int emitted, inSecond;
    private static float second;
    internal static bool Active(XUi ui)
    {
        var workspace=XUiC_RebirthCreativeWorkspace.ActiveInstance;
        return workspace?.IsWorkspaceOpen==true&&ReferenceEquals(workspace.xui,ui);
    }
    internal static void Open(XUi ui)
    {if(!RebirthLogSettings.UiRouteLoggingEnabled)return;emitted=0;inSecond=0;second=Time.realtimeSinceStartup;Write(ui,"open",null,"root=rebirthCreativeRoot");}
    internal static void Write(XUi ui,string phase,XUiC_ItemStack slot=null,string detail=null)
    {
        if(!RebirthLogSettings.UiRouteLoggingEnabled)return;
        if(ui==null||!Active(ui)||emitted>=300)return;
        float now=Time.realtimeSinceStartup;
        if(now-second>=1f){second=now;inSecond=0;}
        if(inSecond++>=36)return;
        emitted++;
        var pointer=ui.playerUI?.CursorController;
        var held=ui.DragAndDropWindow?.CurrentStack;
        var item=slot?.ItemStack;
        var bag=ui.PlayerInventory?.GetBackpackItemStacks();
        string bagState=" bagUsed="+(bag==null?-1:RebirthCraftingInventoryBridge.GetUsedSlotCount(ui));
        var local=ui.playerUI?.entityPlayer as EntityPlayerLocal;
        if(local!=null)
            bagState+=" vehicleBagHold="+RebirthVehicleOwnerCustody.BlocksInventory(local.bag,true)+
                " vehicleBeltHold="+RebirthVehicleOwnerCustody.BlocksInventory(local.inventory,false)+
                " gearBagBlock="+RebirthGearOwnerReservation.BlocksInventory(local.bag,true)+
                " gearBeltBlock="+RebirthGearOwnerReservation.BlocksInventory(local.inventory,false)+
                " hydrationBagHold="+RebirthHydrationOwnerHold.BlocksInventory(local.bag,true)+
                " creativeBeltAllowed="+(item?.IsEmpty()==false&&slot.StackLocation==XUiC_ItemStack.StackLocationTypes.Creative?item.CanMoveTo(XUiC_ItemStack.StackLocationTypes.ToolBelt):true);
        if(slot?.StackLocation==XUiC_ItemStack.StackLocationTypes.Backpack && bag!=null && slot.SlotNumber>=0 && slot.SlotNumber<bag.Length)
            bagState+=" bagSlot="+(bag[slot.SlotNumber]?.IsEmpty()==false?bag[slot.SlotNumber].itemValue.ItemClass.GetItemName()+":"+bag[slot.SlotNumber].count:"empty");
        var mouse=UICamera.GetMouse?.Invoke(0);
        Log.Out("[REBIRTH CreativeTransfer] frame="+Time.frameCount+" phase="+phase+
            " source="+(slot==null?"none":slot.GetType().Name+":"+slot.StackLocation+":"+slot.SlotNumber)+
            " item="+(item?.IsEmpty()==false?item.itemValue?.ItemClass?.GetItemName()+":"+item.count:"empty")+
            " cursor="+(held?.IsEmpty()==false?held.itemValue?.ItemClass?.GetItemName()+":"+held.count:"empty")+
            " shift="+InputUtils.ShiftKeyPressed+" down="+(pointer?.GetMouseButtonDown(UICamera.MouseButton.LeftButton)??false)+
            " held="+(pointer?.GetMouseButton(UICamera.MouseButton.LeftButton)??false)+
            " up="+(pointer?.GetMouseButtonUp(UICamera.MouseButton.LeftButton)??false)+
            " dragStarted="+(mouse?.dragStarted??false)+" pressed="+(mouse?.pressed?.name??"none")+
            " hover="+(UICamera.hoveredObject?.name??"none")+
            " over="+(slot?.isOver??false)+" uiHover="+(slot?.ViewComponent?.UiTransformIsHovered??false)+
            bagState+(string.IsNullOrEmpty(detail)?"":" "+detail));
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
    {
        if(!RebirthLogSettings.UiRouteLoggingEnabled)return;
        RebirthInventoryGestureTrace.Write(__instance,__originalMethod.Name+".before");
        RebirthCreativeTransferTrace.Write(__instance.xui,__originalMethod.Name+".before",__instance);
    }
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(XUiC_ItemStack __instance,MethodBase __originalMethod,bool __runOriginal)
    {
        if(!RebirthLogSettings.UiRouteLoggingEnabled)return;
        RebirthInventoryGestureTrace.Write(__instance,__originalMethod.Name+".after",__runOriginal);
        RebirthCreativeTransferTrace.Write(__instance.xui,__originalMethod.Name+".after",__instance,"nativeRan="+__runOriginal);
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
        if(!RebirthLogSettings.UiRouteLoggingEnabled)return;
        var cursor=__instance.xui?.playerUI?.CursorController;
        if(!__instance.isOver||cursor==null)return;
        if(cursor.GetMouseButtonUp(UICamera.MouseButton.LeftButton))
            RebirthInventoryGestureTrace.Write(__instance,"pointer.release");
    }
}
