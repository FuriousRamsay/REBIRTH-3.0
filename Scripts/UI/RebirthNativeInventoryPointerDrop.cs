using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// The same release gate used by cooking/service windows, shared by the other
// native inventory surfaces. Native ItemStack still owns swap/split/admission.
internal static class RebirthNativeInventoryPointerDrop
{
    private sealed class Gesture
    {
        internal int Frame=-1,HeldFrame=-1,Release=-1,Count;
        internal bool Shift,Claimed;
        internal EntityPlayerLocal Player;
        internal World World;
        internal GameObject Source;
        internal XUiC_DragAndDropWindow Cursor;
        internal string Item;
    }
    private static readonly ConditionalWeakTable<XUi,Gesture> states=new ConditionalWeakTable<XUi,Gesture>();
    internal static void Arm(XUiC_ItemStack slot)
    {
        if(slot?.xui==null)return;
        var pointer=slot.xui.playerUI?.CursorController;
        if(pointer==null||(!pointer.GetMouseButton(UICamera.MouseButton.LeftButton)
            &&!pointer.GetMouseButtonDown(UICamera.MouseButton.LeftButton)
            &&!pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton)))return;
        if(slot.IsDragAndDrop||slot is XUiC_Creative2Stack||slot is XUiC_RebirthReadonlyItemSlot||slot is XUiC_RebirthEditorPreviewSlot
            ||slot.windowGroup?.Controller is XUiC_RebirthCookingStation||slot.GetParentByType<XUiC_RebirthVehicleFrame>()!=null)return;
        var ui=slot.xui;var player=ui.playerUI?.entityPlayer as EntityPlayerLocal;
        if(player==null||pointer==null||player.IsDead()||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return;
        var state=states.GetValue(ui,_=>new Gesture());var held=ui.DragAndDropWindow?.CurrentStack;
        if(state.Frame!=Time.frameCount)
        {
            state.Frame=Time.frameCount;
            if(pointer.GetMouseButtonDown(UICamera.MouseButton.LeftButton))
            {state.Source=null;state.HeldFrame=-1;state.Release=-1;state.Shift=false;state.Claimed=false;
                RebirthCreativeTransferTrace.Write(ui,"relay.down");}
            state.Shift|=InputUtils.ShiftKeyPressed;
            var mouse=UICamera.GetMouse?.Invoke(0);
            if(pointer.GetMouseButton(UICamera.MouseButton.LeftButton)&&!state.Shift&&mouse?.dragStarted==true&&mouse.pressed
                &&held?.IsEmpty()==false)
            {
                if(RebirthLogSettings.UiRouteLoggingEnabled&&state.HeldFrame<0)RebirthCreativeTransferTrace.Write(ui,"relay.capture",null,"source="+mouse.pressed.name);
                state.Player=player;state.World=player.world;state.Source=mouse.pressed;state.Cursor=ui.DragAndDropWindow;
                state.Count=held.count;state.Item=RebirthNativeItemCodec.Encode(held.itemValue);state.HeldFrame=Time.frameCount;
            }
            if(pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton))
            {
                state.Claimed=false;
                state.Release=!state.Shift&&state.HeldFrame>=Time.frameCount-2&&state.HeldFrame<Time.frameCount&&Matches(ui,state,player,held)?Time.frameCount:-1;
                if(RebirthLogSettings.UiRouteLoggingEnabled)
                    RebirthCreativeTransferTrace.Write(ui,"relay.release",null,
                    "captured="+(state.HeldFrame>=0)+" captureFrame="+state.HeldFrame+" release="+state.Release+
                    " matches="+Matches(ui,state,player,held)+" shiftLatched="+state.Shift);
                state.HeldFrame=-1;
            }
            else if(!pointer.GetMouseButton(UICamera.MouseButton.LeftButton)){state.Release=-1;state.Source=null;}
        }
        if(RebirthLogSettings.UiRouteLoggingEnabled&&RebirthCreativeTransferTrace.Active(ui)&&pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton)&&
            (slot.StackLocation==XUiC_ItemStack.StackLocationTypes.Backpack||slot.StackLocation==XUiC_ItemStack.StackLocationTypes.ToolBelt))
        {
            var target=slot.ViewComponent;var targetCollider=target?.UiTransform?.GetComponent<BoxCollider>();
            var targetCamera=ui.playerUI.camera;
            bool hit=targetCollider&&targetCamera&&targetCollider.Raycast(targetCamera.ScreenPointToRay(pointer.GetScreenPosition()),out _,10000f);
            if(hit)RebirthCreativeTransferTrace.Write(ui,"relay.target",slot,
                "release="+state.Release+" claimed="+state.Claimed+" matches="+Matches(ui,state,player,held)+
                " visible="+target.IsVisible+" enabled="+target.Enabled+" press="+target.EventOnPress+
                " colliderEnabled="+targetCollider.enabled+" active="+targetCollider.gameObject.activeInHierarchy+
                " locked="+slot.IsLocked+" stackLock="+slot.StackLock+" userLock="+slot.UserLockedSlot+
                " simple="+slot.SimpleClick);
        }
        if(state.Release!=Time.frameCount||state.Claimed||!Matches(ui,state,player,held))return;
        var view=slot.ViewComponent;var collider=view?.UiTransform?.GetComponent<BoxCollider>();
        // NGUI retains hover on the pressed source for a real drag. The collider
        // raycast below identifies the destination even when native hover is stale.
        if(view?.IsVisible!=true||!view.Enabled||!view.EventOnPress
            ||!collider||!collider.enabled||!collider.gameObject.activeInHierarchy||slot.IsLocked||slot.StackLock||slot.UserLockedSlot||slot.SimpleClick)return;
        var camera=ui.playerUI.camera;if(!camera)return;
        var ray=camera.ScreenPointToRay(pointer.GetScreenPosition());
        if(!collider.Raycast(ray,out _,10000f))return;
        if(!slot.CanSwap(held))
        {RebirthCreativeTransferTrace.Write(ui,"relay.cannotSwap",slot);return;}
        state.Claimed=true;
        // The native Update path requires both isOver and UiTransformIsHovered,
        // while NGUI keeps those on the pressed source until release. Reuse the
        // native swap entry point once the real target collider owns the ray.
        slot.HandleStackSwap();
        RebirthCreativeTransferTrace.Write(ui,"relay.swap",slot);
        RebirthInventoryGestureTrace.Write(slot,"drag.release.armed");
    }
    private static bool Matches(XUi ui,Gesture state,EntityPlayerLocal player,ItemStack held)
        =>ReferenceEquals(state.Player,player)&&ReferenceEquals(state.World,player.world)&&ReferenceEquals(GameManager.Instance?.World,state.World)
            &&ReferenceEquals(state.World?.GetEntity(player.entityId),player)&&ReferenceEquals(state.Cursor,ui.DragAndDropWindow)
            &&state.Source&&state.Source.activeInHierarchy&&held?.IsEmpty()==false&&held.count==state.Count
            &&RebirthNativeItemCodec.Encode(held.itemValue)==state.Item;
}
[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.Update))]
internal static class RebirthNativeInventoryPointerDropPatch
{
    private static void Prefix(XUiC_ItemStack __instance)
    {
        if(__instance is XUiC_RebirthToolbeltItemSlot)
            RebirthVehicleServicePointerDrop.ToolbeltReleaseBeforeNative(XUiC_RebirthVehicleRestoration.ActiveForPointerDrop);
        RebirthNativeInventoryPointerDrop.Arm(__instance);
    }
}
