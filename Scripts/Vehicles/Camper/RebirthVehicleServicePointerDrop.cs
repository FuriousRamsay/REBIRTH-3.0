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
    private static readonly ConditionalWeakTable<XUiC_RebirthVehicleFrame,Gesture> States=new ConditionalWeakTable<XUiC_RebirthVehicleFrame,Gesture>();
    private static readonly Queue<JObject> Releases=new Queue<JObject>();
    private static bool tracing;private static long sequence;
    internal static void ConfigureTrace(bool enabled,bool clear){tracing=enabled;if(clear)Releases.Clear();}
    internal static JObject TraceSnapshot()
    {var rows=new JArray();foreach(var row in Releases)rows.Add(row.DeepClone());return new JObject{["enabled"]=tracing,["events"]=rows};}
    internal static void Reset(XUiC_RebirthVehicleFrame owner){if(owner!=null)States.Remove(owner);}
    internal static void Initialize(XUiController slot)
    {slot.ViewComponent.EventOnPress=true;slot.ViewComponent.EventOnHover=true;slot.ViewComponent.EventOnDrag=true;}
    private static object Session(ConnectionManager manager)
        =>manager?.connectionToServer!=null&&manager.connectionToServer.Length>0?(object)manager.connectionToServer[0]:null;
    private static bool OwnsSource(XUiC_RebirthVehicleFrame owner,GameObject source)
    {
        if(source==null)return false;
        foreach(var root in owner.Children)
        {
            var transform=root.ViewComponent?.UiTransform;
            if(transform!=null&&(source.transform==transform||source.transform.IsChildOf(transform)))return true;
        }
        return false;
    }
    private static bool CurrentScope(XUiC_RebirthVehicleFrame owner,Gesture state)
    {
        var player=owner.xui?.playerUI?.entityPlayer as EntityPlayerLocal;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        return owner.windowGroup?.isShowing==true&&States.TryGetValue(owner,out var retained)&&ReferenceEquals(retained,state)&&
            CursorMatches(owner,state,owner.xui.DragAndDropWindow?.CurrentStack)&&
            player!=null&&ReferenceEquals(player,state.Player)&&ReferenceEquals(player.world,state.World)&&
            ReferenceEquals(GameManager.Instance?.World,state.World)&&ReferenceEquals(state.World?.GetPrimaryPlayer(),player)&&
            ReferenceEquals(state.World?.GetEntity(player.entityId),player)&&player.IsSpawned()&&!player.IsDead()&&
            ReferenceEquals(manager,state.Manager)&&ReferenceEquals(Session(manager),state.Session)&&OwnsSource(owner,state.Source)&&
            state.Vehicle!=null&&ReferenceEquals(owner.Vehicle,state.Vehicle)&&ReferenceEquals(state.World.GetEntity(state.Vehicle.entityId),state.Vehicle);
    }
    private static bool CursorMatches(XUiC_RebirthVehicleFrame owner,Gesture state,ItemStack cursor)
    {
        if(!ReferenceEquals(owner.xui.DragAndDropWindow,state.CursorWindow)||cursor==null||cursor.IsEmpty()||
            cursor.count!=state.Count||string.IsNullOrEmpty(state.CursorImage))return false;
        try{return RebirthNativeItemCodec.Encode(cursor.itemValue)==state.CursorImage;}
        catch{return false;}
    }
    internal static void Tick(XUiC_RebirthVehicleFrame owner)
    {
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
        if(held&&!state.ShiftBlocked&&mouse?.dragStarted==true&&OwnsSource(owner,mouse.pressed)&&cursor!=null&&!cursor.IsEmpty())
        {
            state.Player=player;state.World=player.world;state.Manager=manager;state.Session=Session(manager);
            state.Vehicle=owner.Vehicle;
            state.Source=mouse.pressed;state.Cursor=cursor;state.Count=cursor.count;state.HeldFrame=Time.frameCount;
            state.CursorWindow=owner.xui.DragAndDropWindow;
            try{state.CursorImage=RebirthNativeItemCodec.Encode(cursor.itemValue);}
            catch{state.CursorImage=null;state.HeldFrame=-1;}
        }
        if(up)
        {
            bool recentHeld=state.HeldFrame>=0&&state.HeldFrame<Time.frameCount&&state.HeldFrame>=Time.frameCount-2;
            bool sourceCurrent=state.Source!=null&&OwnsSource(owner,state.Source);
            bool playerCurrent=ReferenceEquals(state.Player,player),playerWorldCurrent=ReferenceEquals(state.World,player.world);
            bool gameWorldCurrent=ReferenceEquals(GameManager.Instance?.World,state.World);
            bool primaryCurrent=ReferenceEquals(state.World?.GetPrimaryPlayer(),player),entityCurrent=ReferenceEquals(state.World?.GetEntity(player.entityId),player);
            bool spawned=player.IsSpawned(),alive=!player.IsDead();
            bool managerCurrent=ReferenceEquals(state.Manager,manager),sessionCurrent=ReferenceEquals(state.Session,Session(manager));
            bool cursorCurrent=CursorMatches(owner,state,cursor);
            bool vehicleCurrent=state.Vehicle!=null&&ReferenceEquals(owner.Vehicle,state.Vehicle)&&ReferenceEquals(player.world?.GetEntity(state.Vehicle.entityId),state.Vehicle);
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
    internal static void Arm(XUiC_ItemStack slot)
    {
        var owner=slot?.GetParentByType<XUiC_RebirthVehicleFrame>();
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
            collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy||!hitTarget||!hover||slot.IsDragAndDrop||slot.IsLocked||slot.StackLock||
            slot.UserLockedSlot||slot.AssembleLock||slot.SimpleClick||InputUtils.ShiftKeyPressed||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()||
            !CursorMatches(owner,state,owner.xui.DragAndDropWindow?.CurrentStack)||
            !CurrentScope(owner,state)||!slot.CanSwap(state.Cursor)||!CurrentScope(owner,state))return;
        // A prior native drag with an occupied held cursor is mandatory. A normal
        // click that fills the cursor on this same mouse-up cannot acquire this gate.
        state.Claimed=true;slot.lastClicked=true;
        if(tracing&&state.Trace!=null){state.Trace["armed"]=true;state.Trace["target"]=view.ID;}
    }
    // Native part/cosmetic controllers derive BasePartStack -> SelectableEntry,
    // not ItemStack. They have their own release gate and CanSwap policy; they
    // do not expose ItemStack's SimpleClick/StackLock/UserLockedSlot fields.
    internal static void Arm(XUiC_BasePartStack slot)
    {
        var owner=slot?.GetParentByType<XUiC_RebirthVehicleFrame>();
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
            collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy||!hitTarget||!hover||
            InputUtils.ShiftKeyPressed||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()||
            !CursorMatches(owner,state,owner.xui.DragAndDropWindow?.CurrentStack)||
            !CurrentScope(owner,state)||!slot.CanSwap(state.Cursor)||!CurrentScope(owner,state))return;
        state.Claimed=true;slot.lastClicked=true;
        if(tracing&&state.Trace!=null){state.Trace["armed"]=true;state.Trace["target"]=view.ID;}
        // BasePartStack.Update performs the real HandleStackSwap, CanSwap and
        // SwapItem calls. Native CanRemove still controls pickup from this slot.
    }
}



