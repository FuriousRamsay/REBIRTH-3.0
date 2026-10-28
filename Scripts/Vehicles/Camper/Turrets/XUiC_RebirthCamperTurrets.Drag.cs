using UnityEngine;

public sealed partial class XUiC_RebirthCamperTurrets
{
    private bool draggingHeld;
    private bool queuedDrop;
    private Ray queuedDropRay;
    private bool outgoingDrag,queuedGroundDrop;
    private ItemStack outgoingExpected,queuedExpected;
    private XUiC_ItemStack queuedNativeSlot;
    private int dropFrame=-1;
    private sealed class DragOrigin
    {internal string Id;internal RebirthCamperTurretAction Action;internal int Corner,Option;}
    private DragOrigin armedOrigin;
    private bool armedDragRequested;
    private ItemStack armedExpected;
    private int dragTraceBudget;
    private const bool DebugDragLog=true;
    internal void TraceDrag(string stage,string detail)
    {
#if DEBUG
        if(!DebugDragLog||dragTraceBudget<=0)return;
        dragTraceBudget--;
        try{Log.Out("[REBIRTH TurretDrag] frame="+Time.frameCount+" stage="+stage+" "+(detail??"").Replace('\r',' ').Replace('\n',' ')+
            " waiting="+waiting+" outgoing="+outgoingDrag+" queued="+queuedDrop+
            " cursor="+RebirthCamperEnergyGestureTrace.Stack(xui.DragAndDropWindow?.CurrentStack));}catch{}
#endif
    }
    private void ResetHeldDrag()
    {
        outgoingDrag=draggingHeld=queuedDrop=queuedGroundDrop=false;outgoingExpected=queuedExpected=null;queuedNativeSlot=null;dropFrame=-1;
        armedOrigin=null;armedExpected=null;armedDragRequested=false;dragTraceBudget=80;
    }
    private ItemStack SourceStack(DragOrigin source)
    {
        var slot=state?.Slots[source.Corner];if(slot?.HasTurret!=true)return null;
        return source.Action==RebirthCamperTurretAction.TakeAmmoHeld?slot.Ammo[source.Option]:
            source.Action==RebirthCamperTurretAction.TakeSilencerHeld?slot.Silencer:slot.Scopes[source.Option];
    }
    private void ArmOrigin(DragOrigin source)
    {
        var view=GetChildById(source.Id)?.ViewComponent;
        if(!opened||current?.Invoke()!=true||view?.IsVisible!=true||!view.Enabled||
            view.UiTransform?.gameObject.activeInHierarchy!=true||Held()||outgoingDrag||queuedDrop||cameraCorner>=0||
            InputUtils.ShiftKeyPressed||!xui.playerUI.CursorController.GetMouseButton(UICamera.MouseButton.LeftButton)||
            armedOrigin!=null)return;
        var stack=SourceStack(source);if(stack==null||stack.IsEmpty())return;
        armedOrigin=source;armedExpected=stack.Clone();armedDragRequested=false;
        TraceDrag("source.down","id="+source.Id+" expected="+RebirthCamperEnergyGestureTrace.Stack(stack));
    }
    private void BeginOutgoing(DragOrigin source,ItemStack expected)
    {
        if(armedOrigin!=source||!armedDragRequested||!opened||current?.Invoke()!=true||waiting||Held()||outgoingDrag||queuedDrop||cameraCorner>=0||
            !xui.playerUI.CursorController.GetMouseButton(UICamera.MouseButton.LeftButton)||InputUtils.ShiftKeyPressed)return;
        var view=GetChildById(source.Id)?.ViewComponent;
        if(view?.IsVisible!=true||!view.Enabled||view.UiTransform?.gameObject.activeInHierarchy!=true)return;
        var stack=SourceStack(source);
        if(stack==null||stack.IsEmpty()||expected!=null&&!RebirthCamperEnergyItems.Same(stack,expected))return;
        outgoingExpected=stack.Clone();outgoingDrag=true;draggingHeld=true;
        armedOrigin=null;armedExpected=null;
        TraceDrag("source.take","id="+source.Id+" action="+source.Action+" expected="+RebirthCamperEnergyGestureTrace.Stack(stack));
        try{Send(source.Action,source.Corner,source.Option);}
        catch(System.Exception e)
        {
            // Capture can refuse before the access layer enters its pending
            // transaction. Release only our gesture state, never cursor/items.
            outgoingDrag=draggingHeld=false;outgoingExpected=null;
            TraceDrag("source.command-refused",e.GetType().Name+": "+e.Message);
            Status("xuiCamperTurretChanged");
        }
    }
    private void DragSource(string id,RebirthCamperTurretAction action,int corner,int option)
    {
        var source=new DragOrigin{Id=id,Action=action,Corner=corner,Option=option};
        var node=GetChildById(id);node.ViewComponent.EventOnDrag=true;
        node.OnMouseUpDown+=(sender,down)=>
        {
            if(down)
            {
                TraceDrag("source.press","id="+id+" button="+UICamera.currentTouchID+" enabled="+node.ViewComponent.Enabled);
                if(UICamera.currentTouchID==-1)ArmOrigin(source);
            }
            else if(armedOrigin==source){armedOrigin=null;armedExpected=null;armedDragRequested=false;}
        };
        node.OnDrag+=(sender,type,delta)=>
        {
            if(type==EDragType.DragStart||type==EDragType.Dragging)
            {
                if(!outgoingDrag&&!armedDragRequested)TraceDrag("source.drag","id="+id+" type="+type+" wasDragging="+node.ViewComponent.wasDragging+" armed="+(armedOrigin?.Id??"none")+" shift="+InputUtils.ShiftKeyPressed);
                if(armedOrigin==source)armedDragRequested=true;
                BeginOutgoing(source,armedExpected);
            }
            else if(type==EDragType.DragEnd&&outgoingDrag)DropHeldAtPointer();
        };
    }
    private void UpdateHeldDrag()
    {
        var pointer=xui.playerUI.CursorController;
        // A native drag intent can arrive during a read-only query. Retry only
        // that pressed source while the same physical hold remains active.
        if(armedOrigin!=null)
        {
            if(!pointer.GetMouseButton(UICamera.MouseButton.LeftButton)||InputUtils.ShiftKeyPressed)
            {TraceDrag("source.disarm","id="+armedOrigin.Id);armedOrigin=null;armedExpected=null;armedDragRequested=false;}
            else if(armedDragRequested)BeginOutgoing(armedOrigin,armedExpected);
        }
        // DragEnd can run before OR after native item-slot Update in its release
        // frame. Let that complete before deciding whether a fallback is needed.
        if(queuedDrop&&!waiting&&Time.frameCount>dropFrame&&
            !pointer.GetMouseButton(UICamera.MouseButton.LeftButton)&&!pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton))
        {
            queuedDrop=false;dropFrame=Time.frameCount;draggingHeld=false;
            var destination=queuedNativeSlot;queuedNativeSlot=null;
            var expected=queuedExpected;queuedExpected=null;
            if(cameraCorner<0&&Held()&&expected!=null&&
                RebirthCamperEnergyItems.Same(xui.DragAndDropWindow.CurrentStack,expected))
            {
                TraceDrag("release.fallback","destination="+(destination?.ViewComponent?.ID??"none"));
                DropHeld(queuedDropRay,queuedGroundDrop,destination);
            }
            else outgoingDrag=false;
            outgoingExpected=null;
        }
        if(Held()&&pointer.GetMouseButton(UICamera.MouseButton.LeftButton))draggingHeld=true;
        if(pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton))
        {if(draggingHeld&&!InputUtils.ShiftKeyPressed)DropHeldAtPointer();draggingHeld=false;}
    }
    internal void DropHeldAtPointer()
    {
        if(dropFrame==Time.frameCount||queuedDrop||InputUtils.ShiftKeyPressed)return;
        if(!opened||(!Held()&&!outgoingDrag)||cameraCorner>=0||current?.Invoke()!=true)return;
        var camera=xui.playerUI.camera;if(!camera)return;
        queuedDropRay=camera.ScreenPointToRay((Vector3)xui.playerUI.CursorController.GetScreenPosition());
        // Bind the exact released physical cell, not a later pointer position or
        // a reflowed filtered inventory after the authoritative pickup reply.
        queuedNativeSlot=FindNativeDrop(Backpack?.GetItemStackControllers(),queuedDropRay)??
            FindNativeDrop(xui.GetChildByType<XUiC_Toolbelt>()?.GetItemStackControllers(),queuedDropRay);
        queuedGroundDrop=UICamera.hoveredObject==xui.gameObject;
        queuedExpected=outgoingDrag?outgoingExpected?.Clone():xui.DragAndDropWindow.CurrentStack?.Clone();
        dropFrame=Time.frameCount;draggingHeld=false;queuedDrop=true;
        TraceDrag("release.queued","destination="+(queuedNativeSlot?.ViewComponent?.ID??"none")+" ground="+queuedGroundDrop);
    }
    private void DropHeld(Ray ray,bool ground,XUiC_ItemStack nativeSlot)
    {
        if(outgoingDrag)
        {
            outgoingDrag=false;
            // Native slots own capacity, locks, stacking and exact cursor custody.
            if(TryNativeDrop(nativeSlot))return;
            if(ground&&Held()&&xui.DragAndDropWindow.CurrentStack.itemValue.ItemClassOrMissing.CanDrop(null))
            {xui.DragAndDropWindow.DropCurrentItem();return;}
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
    private static XUiC_ItemStack FindNativeDrop(XUiC_ItemStack[] slots,Ray ray)
    {
        if(slots==null)return null;
        foreach(var slot in slots)
        {
            var view=slot?.ViewComponent;var collider=view?.UiTransform?.GetComponent<BoxCollider>();
            if(view?.IsVisible==true&&view.Enabled&&collider&&collider.enabled&&
                collider.gameObject.activeInHierarchy&&collider.Raycast(ray,out _,10000))return slot;
        }
        return null;
    }
    private bool TryNativeDrop(XUiC_ItemStack slot)
    {
        if(slot==null)return false;
        // Consume even a refused/hidden destination; never reinterpret it as a
        // ground drop or another target after acknowledgement/layout changes.
        var view=slot.ViewComponent;
        if(view?.IsVisible==true&&view.Enabled&&view.UiTransform?.gameObject.activeInHierarchy==true&&
            !slot.IsLocked&&!slot.StackLock&&!slot.UserLockedSlot&&slot.AllowDropping&&Held())
        {TraceDrag("destination.before","id="+view.ID+" slot="+slot.SlotNumber);slot.HandleStackSwap();TraceDrag("destination.after","id="+view.ID+" slot="+slot.SlotNumber);}
        else TraceDrag("destination.refused","id="+(view?.ID??"none"));
        return true;
    }
    private bool Hit(string id,Ray ray)
    {
        var view=GetChildById(id)?.ViewComponent;var collider=view?.UiTransform?.GetComponent<BoxCollider>();
        return view?.IsVisible==true&&collider&&collider.enabled&&collider.gameObject.activeInHierarchy&&collider.Raycast(ray,out _,10000);
    }
}
