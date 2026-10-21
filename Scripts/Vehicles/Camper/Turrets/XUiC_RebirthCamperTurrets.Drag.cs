using UnityEngine;

public sealed partial class XUiC_RebirthCamperTurrets
{
    private bool draggingHeld;
    private bool queuedDrop;
    private Ray queuedDropRay;
    private bool outgoingDrag;
    private void ResetHeldDrag(){outgoingDrag=draggingHeld=queuedDrop=false;}
    private void DragSource(string id,RebirthCamperTurretAction action,int corner,int option)
    {
        var node=GetChildById(id);node.ViewComponent.EventOnDrag=true;
        node.OnDrag+=(sender,type,delta)=>
        {
            if(type==EDragType.DragStart&&!waiting&&!Held()&&cameraCorner<0)
            {outgoingDrag=true;draggingHeld=true;Send(action,corner,option);}
        };
    }
    private void UpdateHeldDrag()
    {
        var pointer=xui.playerUI.CursorController;
        if(queuedDrop&&!waiting){queuedDrop=false;if(Held())DropHeld(queuedDropRay);else outgoingDrag=false;}
        if(Held()&&pointer.GetMouseButton(UICamera.MouseButton.LeftButton))draggingHeld=true;
        if(pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton))
        {if(draggingHeld&&!InputUtils.ShiftKeyPressed)DropHeldAtPointer();draggingHeld=false;}
    }
    internal void DropHeldAtPointer()
    {
        if(!opened||(!Held()&&!outgoingDrag)||cameraCorner>=0||current?.Invoke()!=true)return;
        var camera=xui.playerUI.camera;if(!camera)return;
        var ray=camera.ScreenPointToRay((Vector3)xui.playerUI.CursorController.GetScreenPosition());
        if(waiting){queuedDrop=true;queuedDropRay=ray;return;}
        DropHeld(ray);
    }
    private void DropHeld(Ray ray)
    {
        if(outgoingDrag)
        {
            outgoingDrag=false;
            // Native item slots retain all stack, lock and capacity admission.
            var slots=Backpack?.GetChildrenByType<XUiC_ItemStack>();
            if(slots!=null)foreach(var slot in slots)
            {
                var view=slot.ViewComponent;var collider=view?.UiTransform?.GetComponent<BoxCollider>();
                if(view?.IsVisible==true&&view.Enabled&&!slot.IsLocked&&!slot.StackLock&&!slot.UserLockedSlot
                    &&collider&&collider.enabled&&collider.Raycast(ray,out _,10000)&&slot.CanSwap(xui.DragAndDropWindow.CurrentStack))
                {slot.HandleStackSwap();return;}
            }
        }
        for(int corner=0;corner<4;corner++)
        {
            if(Hit("turretInstall"+corner,ray)&&state?.Slots[corner].HasTurret==false)
            {Send(RebirthCamperTurretAction.InstallHeld,corner,0);return;}
            if(state?.Slots[corner].HasTurret!=true)continue;
            for(int a=0;a<3;a++)if(Hit("turretAmmo"+corner+"_"+a,ray))
            {Send(RebirthCamperTurretAction.LoadHeld,corner,a);return;}
            if(Hit("turretSilencer"+corner,ray)){Send(RebirthCamperTurretAction.SilencerHeld,corner,0);return;}
            for(int s=0;s<3;s++)if(Hit("turretScope"+corner+"_"+s,ray))
            {Send(RebirthCamperTurretAction.ScopeHeld,corner,s);return;}
        }
    }
    private bool Hit(string id,Ray ray)
    {
        var view=GetChildById(id)?.ViewComponent;var collider=view?.UiTransform?.GetComponent<BoxCollider>();
        return view?.IsVisible==true&&collider&&collider.enabled&&collider.gameObject.activeInHierarchy&&collider.Raycast(ray,out _,10000);
    }
}
