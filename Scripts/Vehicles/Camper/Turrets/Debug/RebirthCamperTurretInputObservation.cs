using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class XUiC_RebirthCamperTurrets
{
    // Explicit bridge observation only. Does not change hover, locks, native
    // input source, cursor custody or any inventory; excluded from Release.
    private JObject ObserveBackpackInput()
    {
#if DEBUG
        var pointer=xui.playerUI.CursorController;
        var slots=Backpack?.GetItemStackControllers()??Array.Empty<XUiC_ItemStack>();
        return new JObject
        {
            ["frame"]=Time.frameCount,
            ["nativeGuiInputType"]=xui.playerUI.playerInput.GUIActions.LastInputType.ToString(),
            ["leftHeld"]=pointer.GetMouseButton(UICamera.MouseButton.LeftButton),
            ["pending"]=RebirthCamperTurretAccess.Pending,
            ["slots"]=new JArray(slots.Where(s=>s!=null&&(!s.ItemStack.IsEmpty()||s.isOver)).Select(s=>new JObject
            {
                ["slot"]=s.SlotNumber,["item"]=s.ItemStack.IsEmpty()?null:s.ItemStack.itemValue.ItemClass.GetItemName(),
                ["count"]=s.ItemStack.count,["location"]=s.StackLocation.ToString(),
                ["over"]=s.isOver,["transformHovered"]=s.ViewComponent?.UiTransformIsHovered??false,
                ["eventOnPress"]=s.ViewComponent?.EventOnPress??false,
                ["isLocked"]=s.IsLocked,["stackLock"]=s.StackLock,["userLock"]=s.UserLockedSlot,
                ["simpleClick"]=s.SimpleClick,["allowDropping"]=s.AllowDropping,
                ["pickupSnapDistance"]=s.PickupSnapDistance,
                ["updateFrame"]=(s as XUiC_RebirthCamperTurretBackpackSlot)?.ObservedUpdateFrame??-1,
                ["nativeUpdateFrame"]=(s as XUiC_RebirthCamperTurretBackpackSlot)?.ObservedNativeUpdateFrame??-1,
                ["clickFrame"]=(s as XUiC_RebirthCamperTurretBackpackSlot)?.ObservedClickFrame??-1,
                ["updatePending"]=(s as XUiC_RebirthCamperTurretBackpackSlot)?.ObservedPending??false,
                ["updateActive"]=(s as XUiC_RebirthCamperTurretBackpackSlot)?.ObservedActive??false,
                ["updateLeftHeld"]=(s as XUiC_RebirthCamperTurretBackpackSlot)?.ObservedLeftHeld??false,
                ["pointerX"]=(s as XUiC_RebirthCamperTurretBackpackSlot)?.ObservedPointer.x??0,
                ["pointerY"]=(s as XUiC_RebirthCamperTurretBackpackSlot)?.ObservedPointer.y??0
            }))
        };
#else
        return null;
#endif
    }
}

