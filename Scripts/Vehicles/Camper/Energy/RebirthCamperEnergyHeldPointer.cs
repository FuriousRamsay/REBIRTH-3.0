using UnityEngine;

// Match management's native held-release/collider route; no inventory writes.
internal sealed class RebirthCamperEnergyHeldPointer
{
    private bool held;
    private int consumedFrame=-1;
    internal bool Consumed=>consumedFrame==Time.frameCount;
    internal void Reset(){held=false;consumedFrame=-1;}
    internal void Update(XUi xui,bool canInteract,XUiC_RebirthCamperEnergySlot[] sources,XUiC_RebirthCamperEnergySlot[] components)
    {
        var pointer=xui.playerUI.CursorController;
        if(pointer.GetMouseButton(UICamera.MouseButton.LeftButton)&&xui.DragAndDropWindow?.IsEmpty()==false&&!InputUtils.ShiftKeyPressed)held=true;
        if(!pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton))return;
        bool release=held;held=false;
        RebirthCamperEnergyGestureTrace.Write("pointer.owner-up","held="+release+" canInteract="+canInteract+" shift="+InputUtils.ShiftKeyPressed+
            " cursor "+RebirthCamperEnergyGestureTrace.Stack(xui.DragAndDropWindow?.CurrentStack));
        if(!release)return;
        Release(xui,canInteract,sources,components);
    }
    internal void Release(XUi xui,bool canInteract,XUiC_RebirthCamperEnergySlot[] sources,XUiC_RebirthCamperEnergySlot[] components)
    {
        held=false;
        var pointer=xui.playerUI.CursorController;
        if(Consumed||InputUtils.ShiftKeyPressed||!canInteract||xui.DragAndDropWindow?.IsEmpty()!=false||xui.playerUI.camera==null)return;
        var ray=xui.playerUI.camera.ScreenPointToRay((Vector3)pointer.GetScreenPosition());
        foreach(var slot in sources)if(TryDrop(slot,ray,xui))return;
        foreach(var slot in components)if(TryDrop(slot,ray,xui))return;
        var backpack=sources[0]?.GetParentByType<XUiC_RebirthCamperEnergy>()?.GetChildByType<XUiC_Backpack>();
        var cells=backpack?.GetItemStackControllers();
        if(cells==null)return;
        foreach(var cell in cells)
        {
            if(cell==null||cell.IsLocked||cell.StackLock||cell.UserLockedSlot||!Hit(cell,ray))continue;
            RebirthCamperEnergyGestureTrace.Write("pointer.release-backpack","slot="+cell.SlotNumber);
            // Native backpack admission, merge and exact cursor custody remain
            // authoritative, as in working management DropHeldAtPointer.
            consumedFrame=Time.frameCount;
            cell.HandleStackSwap();return;
        }
    }
    private bool TryDrop(XUiC_RebirthCamperEnergySlot slot,Ray ray,XUi xui)
    {
        if(!Hit(slot,ray))return false;
        bool admitted=slot.CanSwap(xui.DragAndDropWindow.CurrentStack);
        RebirthCamperEnergyGestureTrace.Write("pointer.release","slot="+slot.SlotNumber+" admitted="+admitted);
        // Consume the hit even on refusal: child native release must not retry it.
        if(admitted)slot.SwapItem();
        consumedFrame=Time.frameCount;return true;
    }
    private static bool Hit(XUiController slot,Ray ray)
    {
        var view=slot?.ViewComponent;
        var collider=view?.UiTransform?.GetComponent<BoxCollider>();RaycastHit hit;
        return view!=null&&view.IsVisible&&collider!=null&&collider.enabled&&collider.gameObject.activeInHierarchy&&collider.Raycast(ray,out hit,10000f);
    }
}
