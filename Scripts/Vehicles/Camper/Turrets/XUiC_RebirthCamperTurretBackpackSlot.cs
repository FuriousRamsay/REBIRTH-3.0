using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthCamperTurretBackpackSlot:XUiC_ItemStack
{
    private RebirthSlotPalette palette;
#if DEBUG
    internal int ObservedUpdateFrame=-1,ObservedNativeUpdateFrame=-1,ObservedClickFrame=-1;
    internal bool ObservedPending,ObservedActive,ObservedLeftHeld;
    internal UnityEngine.Vector2 ObservedPointer;
#endif
    public override void Init()
    {
        base.Init();palette=new RebirthSlotPalette(this);
        ViewComponent.EventOnPress=true;ViewComponent.EventOnHover=true;ViewComponent.EventOnDrag=true;ViewComponent.EventOnScroll=true;
        OnDrag+=(s,type,delta)=>{if(type==EDragType.DragEnd)RebirthCamperTurretAccess.ViewFor(xui)?.DropHeldAtPointer();};
    }
    public override void updateItemInfoWindow(XUiC_ItemStack item)
        =>RebirthCamperTurretAccess.ViewFor(xui)?.Inspect(item?.ItemStack);
    public override void HandleClickComplete()
    {
#if DEBUG
        ObservedClickFrame=UnityEngine.Time.frameCount;
#endif
        base.HandleClickComplete();RebirthCamperTurretAccess.ViewFor(xui)?.Inspect(ItemStack);
    }
    public override void Update(float dt)
    {
#if DEBUG
        ObservedUpdateFrame=UnityEngine.Time.frameCount;
        ObservedPending=RebirthCamperTurretAccess.Pending;
        ObservedActive=ViewComponent?.UiTransform?.gameObject.activeInHierarchy==true;
        ObservedLeftHeld=xui.playerUI.CursorController.GetMouseButton(UICamera.MouseButton.LeftButton);
        ObservedPointer=xui.playerUI.CursorController.GetScreenPosition();
#endif
        if(RebirthCamperTurretAccess.PendingMutation||ViewComponent?.UiTransform?.gameObject.activeInHierarchy!=true)return;
#if DEBUG
        ObservedNativeUpdateFrame=UnityEngine.Time.frameCount;
#endif
        // Same native release preparation used by the working camper module
        // backpack (XUiC_RebirthBackpackSectionNativeSlot.Update).
        if(isOver&&ViewComponent.UiTransformIsHovered&&!IsLocked&&!StackLock&&xui.DragAndDropWindow?.IsEmpty()==false
            &&xui.playerUI.CursorController.GetMouseButton(UICamera.MouseButton.LeftButton))lastClicked=true;
        base.Update(dt);palette?.Apply(AttributeLock);
    }
}

[HarmonyPatch]
internal static class RebirthCamperTurretActorMutationGuard
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        string[] names={"SetSlot","SetSlots","SetItem","SetItems","DecItem","AddItem","AddItemAt","ChangeSlotCount","clearSlotByIndex","ChangeCount","ClearItems","Compact","Sort","CompactSlots","Reset","Clear","CopyFrom","SetSlotLocks","SetLocked"};
        foreach(var type in new[]{typeof(Bag),typeof(Inventory),typeof(ItemStackGrid)})
            foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if(method.DeclaringType==type&&names.Contains(method.Name))yield return method;
        yield return AccessTools.DeclaredMethod(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.SetCurrentStack));
        yield return AccessTools.DeclaredMethod(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.PlaceItemBackInInventory));
        yield return AccessTools.DeclaredMethod(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.OnClose));
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object __instance)=>!RebirthCamperTurretAccess.BlocksActorMutation(__instance);
}

// Keep native bag gestures, but route Shift-click into the selected camper card.
// Block repeat native mutations while the acknowledged actor transaction is pending.
[HarmonyPatch]
internal static class RebirthCamperTurretBackpackGestures
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
        {
            var method=AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes);
            if(method==null)throw new MissingMethodException(typeof(XUiC_ItemStack).FullName,name);yield return method;
        }
    }
    private static bool Prefix(XUiC_ItemStack __instance,MethodBase __originalMethod)
    {
        var owner=RebirthCamperTurretAccess.ViewFor(__instance.xui);
        if(owner==null||!(__instance is XUiC_RebirthCamperTurretBackpackSlot)&&__instance.StackLocation!=XUiC_ItemStack.StackLocationTypes.ToolBelt)return true;
        if(RebirthCamperTurretAccess.PendingMutation)return false;
        if(__originalMethod.Name!="HandleMoveToPreferredLocation")return true;
        owner?.QuickSource(__instance);return false;
    }
}

