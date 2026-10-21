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
        if(slot?.xui==null||slot.IsDragAndDrop||slot is XUiC_Creative2Stack||slot is XUiC_RebirthReadonlyItemSlot||slot is XUiC_RebirthEditorPreviewSlot
            ||slot.windowGroup?.Controller is XUiC_RebirthCookingStation||slot.GetParentByType<XUiC_RebirthVehicleFrame>()!=null)return;
        var ui=slot.xui;var player=ui.playerUI?.entityPlayer as EntityPlayerLocal;var pointer=ui.playerUI?.CursorController;
        if(player==null||pointer==null||player.IsDead()||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return;
        var state=states.GetValue(ui,_=>new Gesture());var held=ui.DragAndDropWindow?.CurrentStack;
        if(state.Frame!=Time.frameCount)
        {
            state.Frame=Time.frameCount;
            if(pointer.GetMouseButtonDown(UICamera.MouseButton.LeftButton))
            {state.Source=null;state.HeldFrame=-1;state.Release=-1;state.Shift=false;state.Claimed=false;}
            state.Shift|=InputUtils.ShiftKeyPressed;
            var mouse=UICamera.GetMouse?.Invoke(0);
            if(pointer.GetMouseButton(UICamera.MouseButton.LeftButton)&&!state.Shift&&mouse?.dragStarted==true&&mouse.pressed
                &&held?.IsEmpty()==false)
            {
                state.Player=player;state.World=player.world;state.Source=mouse.pressed;state.Cursor=ui.DragAndDropWindow;
                state.Count=held.count;state.Item=RebirthNativeItemCodec.Encode(held.itemValue);state.HeldFrame=Time.frameCount;
            }
            if(pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton))
            {
                state.Claimed=false;
                state.Release=!state.Shift&&state.HeldFrame>=Time.frameCount-2&&state.HeldFrame<Time.frameCount&&Matches(ui,state,player,held)?Time.frameCount:-1;
                state.HeldFrame=-1;
            }
            else if(!pointer.GetMouseButton(UICamera.MouseButton.LeftButton)){state.Release=-1;state.Source=null;}
        }
        if(state.Release!=Time.frameCount||state.Claimed||!Matches(ui,state,player,held))return;
        var view=slot.ViewComponent;var collider=view?.UiTransform?.GetComponent<BoxCollider>();
        if(view?.IsVisible!=true||!view.Enabled||!view.EventOnPress||!slot.isOver||!view.UiTransformIsHovered
            ||!collider||!collider.enabled||!collider.gameObject.activeInHierarchy||slot.IsLocked||slot.StackLock||slot.UserLockedSlot||slot.SimpleClick)return;
        var camera=ui.playerUI.camera;if(!camera)return;
        var ray=camera.ScreenPointToRay(pointer.GetScreenPosition());
        if(!collider.Raycast(ray,out _,10000f)||!slot.CanSwap(held))return;
        state.Claimed=true;slot.lastClicked=true;
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
    private static void Prefix(XUiC_ItemStack __instance)=>RebirthNativeInventoryPointerDrop.Arm(__instance);
}
