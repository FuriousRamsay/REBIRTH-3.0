using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

// NGUI holds hoveredObject on the pressed source during drag and restores the
// target only on release. Retain a genuine prior-held drag witness, then open
// the native target's release gate. Never swap/write a stack here.
internal static class RebirthVehicleServicePointerDrop
{
    private sealed class Gesture
    {
        internal EntityPlayerLocal Player;internal World World;internal object Manager,Session;
        internal EntityVehicle Vehicle;
        internal GameObject Source;internal ItemStack Cursor;internal int Count,HeldFrame=-1,ReleaseFrame=-1,TickFrame=-1;
        internal XUiC_DragAndDropWindow CursorWindow;internal string CursorImage;
        internal bool ShiftBlocked,Claimed;internal JObject Trace;
    }
    private static readonly ConditionalWeakTable<XUiController,Gesture> States=new ConditionalWeakTable<XUiController,Gesture>();
    private static readonly Queue<JObject> Releases=new Queue<JObject>();
    private static bool tracing;private static long sequence;
    internal static void ConfigureTrace(bool enabled,bool clear){tracing=enabled;if(clear)Releases.Clear();}
    internal static JObject TraceSnapshot()
    {var rows=new JArray();foreach(var row in Releases)rows.Add(row.DeepClone());return new JObject{["enabled"]=tracing,["events"]=rows};}
    internal static void Reset(XUiController owner){if(owner!=null)States.Remove(owner);}
    internal static void Initialize(XUiController slot)
    {slot.ViewComponent.EventOnPress=true;slot.ViewComponent.EventOnHover=true;slot.ViewComponent.EventOnDrag=true;}
    private static EntityVehicle Vehicle(XUiController owner)
        =>owner is XUiC_RebirthVehicleFrame native?native.Vehicle:(owner as XUiC_RebirthVehicleRestoration)?.NativeVehicle;
    private static XUiController Owner(XUiController slot)
        =>(XUiController)slot?.GetParentByType<XUiC_RebirthVehicleFrame>()??slot?.GetParentByType<XUiC_RebirthVehicleRestoration>();
    private static bool TargetCurrent(XUiController owner,Gesture state)
        =>state.Vehicle!=null?ReferenceEquals(Vehicle(owner),state.Vehicle)&&ReferenceEquals(state.World?.GetEntity(state.Vehicle.entityId),state.Vehicle):
            owner is XUiC_RebirthVehicleRestoration restoration&&restoration.NativeVehicle==null&&restoration.NativeBlockPointerTarget;
    private static object Session(ConnectionManager manager)
        =>manager?.connectionToServer!=null&&manager.connectionToServer.Length>0?(object)manager.connectionToServer[0]:null;
    private static bool OwnsSource(XUiController owner,GameObject source)
    {
        if(source==null)return false;
        foreach(var root in owner.Children)
        {
            var transform=root.ViewComponent?.UiTransform;
            if(transform!=null&&(source.transform==transform||source.transform.IsChildOf(transform)))return true;
        }
        return false;
    }
    private static bool CurrentScope(XUiController owner,Gesture state)
    {
        var player=owner.xui?.playerUI?.entityPlayer as EntityPlayerLocal;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        return owner.windowGroup?.isShowing==true&&States.TryGetValue(owner,out var retained)&&ReferenceEquals(retained,state)&&
            CursorMatches(owner,state,owner.xui.DragAndDropWindow?.CurrentStack)&&
            player!=null&&ReferenceEquals(player,state.Player)&&ReferenceEquals(player.world,state.World)&&
            ReferenceEquals(GameManager.Instance?.World,state.World)&&ReferenceEquals(state.World?.GetPrimaryPlayer(),player)&&
            ReferenceEquals(state.World?.GetEntity(player.entityId),player)&&player.IsSpawned()&&!player.IsDead()&&
            ReferenceEquals(manager,state.Manager)&&ReferenceEquals(Session(manager),state.Session)&&
            state.Source!=null&&state.Source.activeInHierarchy&&
            TargetCurrent(owner,state);
    }
    private static bool CursorMatches(XUiController owner,Gesture state,ItemStack cursor)
    {
        if(!ReferenceEquals(owner.xui.DragAndDropWindow,state.CursorWindow)||cursor==null||cursor.IsEmpty()||
            cursor.count!=state.Count||string.IsNullOrEmpty(state.CursorImage))return false;
        try{return RebirthNativeItemCodec.Encode(cursor.itemValue)==state.CursorImage;}
        catch{return false;}
    }
    internal static void Tick(XUiController owner)
    {
        if(owner is XUiC_RebirthVehicleRestoration restoration)
        {
            TickRestoration(restoration);
            return;
        }
        if(owner?.windowGroup?.isShowing!=true){Reset(owner);return;}
        var state=States.GetValue(owner,_=>new Gesture());
        if(state.TickFrame==Time.frameCount)return;state.TickFrame=Time.frameCount;
        var pointer=owner.xui?.playerUI?.CursorController;var player=owner.xui?.playerUI?.entityPlayer as EntityPlayerLocal;
        if(pointer==null||player==null||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()){Reset(owner);return;}
        bool down=pointer.GetMouseButtonDown(UICamera.MouseButton.LeftButton);
        bool held=pointer.GetMouseButton(UICamera.MouseButton.LeftButton);
        bool up=pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton);
        if(down){state.Source=null;state.Cursor=null;state.CursorWindow=null;state.CursorImage=null;state.HeldFrame=-1;state.ReleaseFrame=-1;state.ShiftBlocked=false;state.Claimed=false;state.Trace=null;}
        if(InputUtils.ShiftKeyPressed)state.ShiftBlocked=true;
        var mouse=UICamera.GetMouse?.Invoke(0);var cursor=owner.xui.DragAndDropWindow?.CurrentStack;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        // Inventory/toolbelt drags can begin outside this window controller even
        // though they end on its part grid. Native ItemStack accepts those sources.
        if(held&&!state.ShiftBlocked&&mouse?.dragStarted==true&&mouse.pressed!=null&&mouse.pressed.activeInHierarchy&&cursor!=null&&!cursor.IsEmpty())
        {
            state.Player=player;state.World=player.world;state.Manager=manager;state.Session=Session(manager);
            state.Vehicle=Vehicle(owner);
            state.Source=mouse.pressed;state.Cursor=cursor;state.Count=cursor.count;state.HeldFrame=Time.frameCount;
            state.CursorWindow=owner.xui.DragAndDropWindow;
            try{state.CursorImage=RebirthNativeItemCodec.Encode(cursor.itemValue);}
            catch{state.CursorImage=null;state.HeldFrame=-1;}
        }
        if(up)
        {
            bool recentHeld=state.HeldFrame>=0&&state.HeldFrame<Time.frameCount&&state.HeldFrame>=Time.frameCount-2;
            bool sourceCurrent=state.Source!=null&&state.Source.activeInHierarchy;
            bool playerCurrent=ReferenceEquals(state.Player,player),playerWorldCurrent=ReferenceEquals(state.World,player.world);
            bool gameWorldCurrent=ReferenceEquals(GameManager.Instance?.World,state.World);
            bool primaryCurrent=ReferenceEquals(state.World?.GetPrimaryPlayer(),player),entityCurrent=ReferenceEquals(state.World?.GetEntity(player.entityId),player);
            bool spawned=player.IsSpawned(),alive=!player.IsDead();
            bool managerCurrent=ReferenceEquals(state.Manager,manager),sessionCurrent=ReferenceEquals(state.Session,Session(manager));
            bool cursorCurrent=CursorMatches(owner,state,cursor);
            bool vehicleCurrent=TargetCurrent(owner,state);
            bool current=!state.ShiftBlocked&&recentHeld&&sourceCurrent&&playerCurrent&&playerWorldCurrent&&gameWorldCurrent&&
                primaryCurrent&&entityCurrent&&spawned&&alive&&managerCurrent&&sessionCurrent&&cursorCurrent&&vehicleCurrent;
            state.ReleaseFrame=current?Time.frameCount:-1;state.Claimed=false;
            if(tracing)
            {
                state.Trace=new JObject{["sequence"]=++sequence,["frame"]=Time.frameCount,["context"]="vehicleService",
                    ["owner"]=player.entityId,["priorHeldFrame"]=state.HeldFrame,["shiftBlocked"]=state.ShiftBlocked,
                    ["source"]=state.Source?.name,["nativeHover"]=UICamera.hoveredObject?.name,
                    ["nativeCurrent"]=mouse?.current?.name,["nativeDragStartedAtRelease"]=mouse?.dragStarted??false,
                    ["cursorCount"]=cursor?.count??0,["releaseWitness"]=current,["armed"]=false};
                state.Trace["predicates"]=new JObject{["vehicleCurrent"]=vehicleCurrent,["recentHeld"]=recentHeld,["sourceCurrent"]=sourceCurrent,
                    ["playerCurrent"]=playerCurrent,["playerWorldCurrent"]=playerWorldCurrent,["gameWorldCurrent"]=gameWorldCurrent,
                    ["primaryCurrent"]=primaryCurrent,["entityCurrent"]=entityCurrent,["spawned"]=spawned,["alive"]=alive,
                    ["managerCurrent"]=managerCurrent,["sessionCurrent"]=sessionCurrent,
                    ["cursorOwnerCurrent"]=ReferenceEquals(owner.xui.DragAndDropWindow,state.CursorWindow),
                    ["cursorCountCurrent"]=cursor!=null&&cursor.count==state.Count,["cursorExactImageCurrent"]=cursorCurrent,
                    ["cursorGetterReferenceSame"]=ReferenceEquals(state.Cursor,cursor)};
                Releases.Enqueue(state.Trace);while(Releases.Count>32)Releases.Dequeue();
            }
            state.HeldFrame=-1;
        }
        else if(!held){state.ReleaseFrame=-1;state.Cursor=null;state.Source=null;}
    }
    // The working camper energy window resolves the held cursor against native
    // slot colliders on release. Use that same route for restoration instead of
    // requiring NGUI's pressed-source and hovered-object timing to coincide.
    private static void TickRestoration(XUiC_RebirthVehicleRestoration owner)
    {
        if(owner?.windowGroup?.isShowing!=true){Reset(owner);return;}
        var state=States.GetValue(owner,_=>new Gesture());
        var xui=owner.xui;
        var pointer=xui?.playerUI?.CursorController;
        if(pointer==null||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()){Reset(owner);return;}
        if(pointer.GetMouseButtonDown(UICamera.MouseButton.LeftButton)){state.HeldFrame=-1;state.Claimed=false;}
        if(pointer.GetMouseButton(UICamera.MouseButton.LeftButton)&&
            xui.DragAndDropWindow?.IsEmpty()==false&&!InputUtils.ShiftKeyPressed)
            state.HeldFrame=Time.frameCount;
        if(!pointer.GetMouseButtonUp(UICamera.MouseButton.LeftButton))return;
        bool held=state.HeldFrame>=0;state.HeldFrame=-1;
        if(held)ReleaseRestoration(owner,state);
    }
    internal static void DragEnded(XUiC_RebirthVehicleRestoration owner)
    {
        if(owner==null||owner.windowGroup?.isShowing!=true)return;
        var state=States.GetValue(owner,_=>new Gesture());
        state.HeldFrame=-1;
        ReleaseRestoration(owner,state);
    }
    // Toolbelt's native release can return the cursor to inventory before the
    // restoration controller updates. Resolve the already-held gesture first.
    internal static void ToolbeltReleaseBeforeNative(XUiC_RebirthVehicleRestoration owner)
    {
        if(owner?.windowGroup?.isShowing!=true||!States.TryGetValue(owner,out var state)||state.HeldFrame<0)return;
        var pointer=owner.xui?.playerUI?.CursorController;
        if(pointer?.GetMouseButtonUp(UICamera.MouseButton.LeftButton)!=true)return;
        state.HeldFrame=-1;
        ReleaseRestoration(owner,state);
    }
    private static void ReleaseRestoration(XUiC_RebirthVehicleRestoration owner,Gesture state)
    {
        var xui=owner.xui;
        var pointer=xui?.playerUI?.CursorController;
        if(state.Claimed||pointer==null||InputUtils.ShiftKeyPressed||!owner.CanInteract||
            xui.DragAndDropWindow?.IsEmpty()!=false||xui.playerUI.camera==null)return;
        var ray=xui.playerUI.camera.ScreenPointToRay((Vector3)pointer.GetScreenPosition());
        foreach(var slot in owner.GetChildrenByType<XUiC_RebirthVehicleRestorationSlot>())
        {
            if(!Hit(slot,ray))continue;
            state.Claimed=true;
            bool canSwap=slot.CanSwap(xui.DragAndDropWindow.CurrentStack);
            if(canSwap)slot.SwapItem();
            return;
        }
        // Match the working camper energy window: the pressed source still owns
        // NGUI's hover on release, so resolve the actual native destination cell.
        var backpack=owner.GetChildByType<XUiC_Backpack>()?.GetItemStackControllers();
        if(TryNativeInventoryDrop(backpack,ray,state))return;
        var toolbelt=xui.GetChildByType<XUiC_Toolbelt>()?.GetItemStackControllers();
        if(TryNativeInventoryDrop(toolbelt,ray,state))return;
    }
    private static bool TryNativeInventoryDrop(XUiC_ItemStack[] cells,Ray ray,Gesture state)
    {
        if(cells==null)return false;
        foreach(var cell in cells)
        {
            if(cell==null||cell.IsLocked||cell.StackLock||cell.UserLockedSlot||!Hit(cell,ray))continue;
            state.Claimed=true;
            cell.HandleStackSwap();
            return true;
        }
        return false;
    }
    private static bool Hit(XUiController slot,Ray ray)
    {
        var view=slot?.ViewComponent;
        var collider=view?.UiTransform?.GetComponent<BoxCollider>();
        RaycastHit hit;
        return view?.IsVisible==true&&view.Enabled&&collider!=null&&collider.enabled&&
            collider.gameObject.activeInHierarchy&&collider.Raycast(ray,out hit,10000f);
    }
    internal static void Arm(XUiC_ItemStack slot)
    {
        var owner=Owner(slot);
        if(owner==null||!States.TryGetValue(owner,out var state)||state.ReleaseFrame!=Time.frameCount||state.Claimed)return;
        var view=slot.ViewComponent;var collider=view?.UiTransform?.GetComponent<BoxCollider>();
        bool hover=slot.isOver&&view?.UiTransformIsHovered==true;
        bool hitTarget=false;
        if(collider!=null&&owner.xui.playerUI.camera!=null)
        {
            var ray=owner.xui.playerUI.camera.ScreenPointToRay(owner.xui.playerUI.CursorController.GetScreenPosition());
            RaycastHit hit;
            hitTarget=collider.Raycast(ray,out hit,10000f);
            if(hitTarget&&tracing&&state.Trace!=null)
            {state.Trace["target"]=view.ID;state.Trace["targetHover"]=hover;state.Trace["targetPress"]=view.EventOnPress;
                state.Trace["targetDrag"]=view.EventOnDrag;state.Trace["targetEnabled"]=view.Enabled;
                state.Trace["targetVisible"]=view.IsVisible;state.Trace["colliderEnabled"]=collider.enabled;state.Trace["targetActive"]=collider.gameObject.activeInHierarchy;state.Trace["isLocked"]=slot.IsLocked;
                state.Trace["stackLock"]=slot.StackLock;state.Trace["assembleLock"]=slot.AssembleLock;state.Trace["lastClickedBefore"]=slot.lastClicked;state.Trace["simpleClick"]=slot.SimpleClick;
                state.Trace["guiInputType"]=slot.xui.playerUI.playerInput.GUIActions.LastInputType.ToString();}
        }
        if(owner.windowGroup.isShowing!=true||view?.IsVisible!=true||!view.Enabled||!view.EventOnPress||
            collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy||!hitTarget||(!hover&&!(owner is XUiC_RebirthVehicleRestoration))||slot.IsDragAndDrop||slot.IsLocked||slot.StackLock||
            slot.UserLockedSlot||slot.AssembleLock||slot.SimpleClick||InputUtils.ShiftKeyPressed||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()||
            !CursorMatches(owner,state,owner.xui.DragAndDropWindow?.CurrentStack)||
            !CurrentScope(owner,state)||!slot.CanSwap(state.Cursor)||!CurrentScope(owner,state))return;
        // A prior native drag with an occupied held cursor is mandatory. A normal
        // click that fills the cursor on this same mouse-up cannot acquire this gate.
        state.Claimed=true;
        if(tracing&&state.Trace!=null){state.Trace["armed"]=true;state.Trace["target"]=view.ID;}
        // Restoration's native slot cannot see NGUI's hovered object on this
        // release because NGUI still owns the pressed source. The raycast above
        // identifies the destination; use the native backpack swap entry point.
        if(owner is XUiC_RebirthVehicleRestoration)slot.HandleStackSwap();
        else slot.lastClicked=true;
    }
    // Native part/cosmetic controllers derive BasePartStack -> SelectableEntry,
    // not ItemStack. They have their own release gate and CanSwap policy; they
    // do not expose ItemStack's SimpleClick/StackLock/UserLockedSlot fields.
    internal static void Arm(XUiC_BasePartStack slot)
    {
        var owner=Owner(slot);
        if(owner==null||!States.TryGetValue(owner,out var state)||state.ReleaseFrame!=Time.frameCount||state.Claimed)return;
        var view=slot.ViewComponent;var collider=view?.UiTransform?.GetComponent<BoxCollider>();
        bool hover=slot.isOver&&view?.UiTransformIsHovered==true;
        bool hitTarget=false;
        if(collider!=null&&owner.xui.playerUI.camera!=null)
        {
            var ray=owner.xui.playerUI.camera.ScreenPointToRay(owner.xui.playerUI.CursorController.GetScreenPosition());
            RaycastHit hit;hitTarget=collider.Raycast(ray,out hit,10000f);
            if(hitTarget&&tracing&&state.Trace!=null)
            {
                state.Trace["target"]=view.ID;state.Trace["targetHierarchy"]="BasePartStack";
                state.Trace["targetHover"]=hover;state.Trace["targetPress"]=view.EventOnPress;
                state.Trace["targetDrag"]=view.EventOnDrag;state.Trace["targetEnabled"]=view.Enabled;
                state.Trace["targetVisible"]=view.IsVisible;state.Trace["colliderEnabled"]=collider.enabled;
                state.Trace["targetActive"]=collider.gameObject.activeInHierarchy;state.Trace["lastClickedBefore"]=slot.lastClicked;
                state.Trace["guiInputType"]=slot.xui.playerUI.playerInput.GUIActions.LastInputType.ToString();
            }
        }
        if(owner.windowGroup.isShowing!=true||view?.IsVisible!=true||!view.Enabled||!view.EventOnPress||
            collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy||!hitTarget||(!hover&&!(owner is XUiC_RebirthVehicleRestoration))||
            InputUtils.ShiftKeyPressed||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()||
            !CursorMatches(owner,state,owner.xui.DragAndDropWindow?.CurrentStack)||
            !CurrentScope(owner,state)||!slot.CanSwap(state.Cursor)||!CurrentScope(owner,state))return;
        state.Claimed=true;
        if(tracing&&state.Trace!=null){state.Trace["armed"]=true;state.Trace["target"]=view.ID;}
        if(owner is XUiC_RebirthVehicleRestoration)slot.SwapItem();
        else slot.lastClicked=true;
    }
}



