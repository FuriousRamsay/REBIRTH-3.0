using System;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using Platform;

// A logical native cursor window only during one authenticated camper switch.
// No OS cursor API, persistent override or frame-by-frame position writes.
internal static class RebirthCamperStationPointerTransition
{
    private const bool DEBUGLOG=true;
    private static bool debugStarted,stackLogged;
    private static int debugRemaining,generation;
    private static float debugUntil;
    private static CursorControllerAbs debugCursor;
    private static string lastCursorDecision;
    private static string lastSoftCursorDecision;
    private static int visibilityDiagnosticRemaining;
    private static readonly System.Collections.Generic.HashSet<string> visibilityDiagnosticKeys=new System.Collections.Generic.HashSet<string>();
    // Separate budget from the existing 16 event trace: g2 must be observable
    // even when source-close/position events consumed that earlier budget.
    internal static void VisibilityPredicates(SoftCursor cursor,bool requested,string phase)
    {
        if(!DEBUGLOG||requested||!debugStarted||visibilityDiagnosticRemaining<=0||Time.realtimeSinceStartup>debugUntil||
            !ThreadManager.IsMainThread()||!ReferenceEquals(cursor,debugCursor))return;
        string key=generation+"/"+phase;if(!visibilityDiagnosticKeys.Add(key))return;
        visibilityDiagnosticRemaining--;
        try
        {
            var t=pending;var player=t?.Player;var vehicle=t?.Vehicle;var world=t?.World;
            var ui=player==null?null:LocalPlayerUI.GetUIForPlayer(player);
            var input=PlatformManager.NativePlatform?.Input;var local=ui?.playerInput;
            bool back=input!=null&&input.PrimaryPlayer.GUIActions.BackButton.WasPressed;
            bool pressed=local!=null&&local.PermanentActions.Cancel.WasPressed;
            bool released=local!=null&&local.PermanentActions.Cancel.WasReleased;
            bool guiReleased=local!=null&&local.GUIActions.Cancel.WasReleased;
            string line="[CamperPointer VISIBILITY] g="+generation+" phase="+phase+" requested=False"+
                " pending="+(t!=null)+" current="+Current(t)+" hidden="+cursor.hidden+" holds="+HoldsSoftCursor(cursor)+
                " main="+ThreadManager.IsMainThread()+" notExpired="+(t!=null&&Time.realtimeSinceStartup<t.Expires)+
                " currentWorld="+(world!=null&&ReferenceEquals(GameManager.Instance?.World,world))+
                " playerWorld="+(player!=null&&ReferenceEquals(player.world,world))+
                " vehicleWorld="+(vehicle!=null&&ReferenceEquals(vehicle.world,world))+
                " primary="+(world!=null&&ReferenceEquals(world.GetPrimaryPlayer(),player))+
                " vehicleEntity="+(world!=null&&vehicle!=null&&ReferenceEquals(world.GetEntity(vehicle.entityId),vehicle))+
                " alive="+(player!=null&&!player.IsDead())+
                " near="+(player!=null&&vehicle!=null&&(vehicle.position-player.position).sqrMagnitude<=64f)+
                " uiCursor="+(ui!=null&&ReferenceEquals(ui.CursorController,t?.Cursor))+
                " input="+(input!=null)+" localInput="+(local!=null)+
                " keyboard="+(input!=null&&input.CurrentInputStyle==PlayerInputManager.InputStyle.Keyboard)+
                " backPressed="+back+" cancelPressed="+pressed+" cancelReleased="+released+" guiCancelReleased="+guiReleased+
                " capturedCursor="+(t!=null&&ReferenceEquals(cursor,t.Cursor))+
                " windowManager="+(ui!=null&&ReferenceEquals(cursor.windowManager,ui.windowManager))+
                " guiActions="+(cursor.guiActions!=null)+" guiEnabled="+(cursor.guiActions!=null&&cursor.guiActions.Enabled)+
                " nativeLocked="+cursor.Locked;
            Log.Out(line);
        }
        catch(Exception error){Log.Out("[CamperPointer VISIBILITY] g="+generation+" phase="+phase+" observer="+error.GetType().Name);}
    }
    internal static void SoftCursorDecision(SoftCursor cursor)
    {
        string key=generation+"/"+cursor.guiActions.Enabled;
        if(key==lastSoftCursorDecision)return;
        lastSoftCursorDecision=key;
        Native(cursor,"native.softcursor.logical-hold","guiEnabled="+cursor.guiActions.Enabled);
    }
    internal static void CursorDecision(bool native,bool effective,bool explicitUI)
    {
        if(!debugStarted||debugRemaining<=0||Time.realtimeSinceStartup>debugUntil)return;
        string key=native+"/"+effective+"/"+explicitUI;
        if(key==lastCursorDecision)return;lastCursorDecision=key;
        Trace("native.cursorwindow","native="+native+" effective="+effective+" explicitUI="+explicitUI);
    }
    internal static void Navigation(XUiController source,int button,Guid target)
    {
        if(!DEBUGLOG||debugStarted||!ThreadManager.IsMainThread())return;
        debugStarted=true;debugRemaining=16;debugUntil=Time.realtimeSinceStartup+30f;
        visibilityDiagnosticRemaining=8;visibilityDiagnosticKeys.Clear();
        lastCursorDecision=null;lastSoftCursorDecision=null;
        debugCursor=source?.xui?.playerUI?.CursorController;
        Trace("nav.press","window="+source?.windowGroup?.Id+" button="+button+" target="+target.ToString("N"));
    }
    internal static void Trace(string stage,string detail="",bool stack=false)
    {
        if(!DEBUGLOG||!debugStarted||debugRemaining<=0||Time.realtimeSinceStartup>debugUntil||!ThreadManager.IsMainThread())return;
        debugRemaining--;
        try
        {
            string native=debugCursor==null?"none":debugCursor.GetScreenPosition().ToString();
            string style=PlatformManager.NativePlatform?.Input==null?"none":PlatformManager.NativePlatform.Input.CurrentInputStyle.ToString();
            string line="[CamperPointer DEBUGLOG] g="+generation+" stage="+stage+" native="+native+
                " mouse="+Input.mousePosition+" cursor="+debugCursor?.GetType().Name+" style="+style+
                " enabled="+GameManager.Instance?.bCursorVisible+" lock="+Cursor.lockState+" pending="+(pending!=null)+" "+detail;
            line+=" override="+GameManager.Instance?.bCursorVisibleOverride+" overrideState="+GameManager.Instance?.bCursorVisibleOverrideState;
            line+=" touch="+(UICamera.currentTouch==null?"none":UICamera.currentTouch.pos.ToString());
            if(stack&&!stackLogged)
            {stackLogged=true;string trace=new System.Diagnostics.StackTrace(1,false).ToString();line+=" caller="+trace.Substring(0,Math.Min(1200,trace.Length)).Replace('\n',' ').Replace('\r',' ');}
            Log.Out(line);
        }
        catch(Exception error){Log.Out("[CamperPointer DEBUGLOG] stage="+stage+" observer="+error.GetType().Name);}
    }
    internal static void Native(CursorControllerAbs cursor,string stage,string detail,bool stack=false)
    {if(ReferenceEquals(cursor,debugCursor))Trace(stage,detail,stack);}
    private sealed class Transition
    {
        internal EntityPlayerLocal Player;internal EntityVehicle Vehicle;internal World World;
        internal CursorControllerAbs Cursor;internal Vector2 Point;internal Guid Target;
        internal float Expires;internal bool Completing;
    }
    private static Transition pending;
    private static bool Current(Transition t)
        =>ThreadManager.IsMainThread()&&t!=null&&ReferenceEquals(pending,t)&&Time.realtimeSinceStartup<t.Expires&&
            ReferenceEquals(GameManager.Instance?.World,t.World)&&ReferenceEquals(t.Player.world,t.World)&&
            ReferenceEquals(t.Vehicle.world,t.World)&&
            ReferenceEquals(t.World.GetPrimaryPlayer(),t.Player)&&ReferenceEquals(t.World.GetEntity(t.Vehicle.entityId),t.Vehicle)&&
            !t.Player.IsDead()&&(t.Vehicle.position-t.Player.position).sqrMagnitude<=64f&&
            ReferenceEquals(LocalPlayerUI.GetUIForPlayer(t.Player)?.CursorController,t.Cursor)&&
            PlatformManager.NativePlatform?.Input!=null&&LocalPlayerUI.GetUIForPlayer(t.Player)?.playerInput!=null&&
            PlatformManager.NativePlatform.Input.CurrentInputStyle==PlayerInputManager.InputStyle.Keyboard&&
            !PlatformManager.NativePlatform.Input.PrimaryPlayer.GUIActions.BackButton.WasPressed&&
            !LocalPlayerUI.GetUIForPlayer(t.Player).playerInput.PermanentActions.Cancel.WasPressed&&
            !LocalPlayerUI.GetUIForPlayer(t.Player).playerInput.PermanentActions.Cancel.WasReleased&&
            !LocalPlayerUI.GetUIForPlayer(t.Player).playerInput.GUIActions.Cancel.WasReleased;
    internal static void Begin(EntityPlayerLocal player,EntityVehicle vehicle,Guid target)
    {
        if(!ThreadManager.IsMainThread()||player==null||vehicle==null||PlatformManager.NativePlatform?.Input==null)return;
        Trace("begin.enter","target="+target.ToString("N"));
        if(Current(pending)&&ReferenceEquals(pending.Player,player)&&ReferenceEquals(pending.Vehicle,vehicle)&&pending.Target==target)return;
        pending=null;
        var cursor=LocalPlayerUI.GetUIForPlayer(player)?.CursorController;
        if(!ThreadManager.IsMainThread()||player==null||vehicle==null||cursor==null||
            PlatformManager.NativePlatform.Input.CurrentInputStyle!=PlayerInputManager.InputStyle.Keyboard||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player))
        {Trace("begin.refused","cursor="+(cursor!=null)+" primary="+ReferenceEquals(player.world?.GetPrimaryPlayer(),player));return;}
        var t=new Transition{Player=player,Vehicle=vehicle,World=player.world,Cursor=cursor,
            Point=cursor.GetScreenPosition(),Target=target,Expires=Time.realtimeSinceStartup+30f};
        pending=t;generation++;Trace("begin.accepted","point="+t.Point+" target="+target.ToString("N"));GameManager.Instance.StartCoroutine(Expire(t));
    }
    internal static void Cancel(EntityPlayerLocal player,EntityVehicle vehicle)
    {if(pending!=null&&ReferenceEquals(pending.Player,player)&&ReferenceEquals(pending.Vehicle,vehicle)){Trace("cancel");pending=null;}}
    internal static bool HoldsCursor(LocalPlayerUI ui)
    {
        var t=pending;if(!Current(t)){if(ReferenceEquals(pending,t)&&t!=null){Trace("hold.refused","primary="+ReferenceEquals(t.World.GetPrimaryPlayer(),t.Player)+" world="+ReferenceEquals(GameManager.Instance?.World,t.World));pending=null;}return false;}
        return ui==null||ReferenceEquals(ui,LocalPlayerUI.GetUIForPlayer(t.Player));
    }
    internal static bool HoldsSoftCursor(SoftCursor cursor)
    {
        var t=pending;
        if(!Current(t)||cursor==null||!ReferenceEquals(cursor,t.Cursor))return false;
        var ui=LocalPlayerUI.GetUIForPlayer(t.Player);
        // Native menu soft cursor is created with the menu manager and resolved
        // by LocalPlayerUI through its cached/camera cursor. Its construction
        // manager need not equal the current player's gameplay window manager.
        // Ownership is the exact captured/current player UI cursor, already
        // authenticated by Current; GUI actions may be disabled during the gap.
        return ui!=null&&ReferenceEquals(ui.CursorController,cursor)&&cursor.guiActions!=null;
    }
    internal static void BeforeOpen(EntityPlayerLocal player,EntityVehicle vehicle,Guid target)
    {
        var t=pending;
        if(Current(t)&&!t.Completing&&ReferenceEquals(t.Player,player)&&ReferenceEquals(t.Vehicle,vehicle)&&t.Target==target)
        {
            // Keep the original pre-close pointer. The native gap may already
            // have locked/centered the mouse before this deferred open arrives.
            Trace("target.before.open","point="+t.Point+" current="+t.Cursor.GetScreenPosition()+" target="+target.ToString("N"));
        }
        else Trace("target.before.open.unmatched","target="+target.ToString("N"));
    }
    internal static void Complete(EntityPlayerLocal player,EntityVehicle vehicle,Guid target,GUIWindow window,Func<bool> current)
    {
        var t=pending;
        Trace("target.after.open","showing="+window?.isShowing+" target="+target.ToString("N"));
        if(!Current(t)||t.Completing||!ReferenceEquals(t.Player,player)||!ReferenceEquals(t.Vehicle,vehicle)||t.Target!=target)return;
        t.Completing=true;
        // Synchronous opens are restored before this opening callback returns.
        // Native deferred opens wait for this exact window once, not a frame loop
        // that repeatedly moves the cursor or touches a replacement session.
        try
        {
            if(current?.Invoke()!=true){if(ReferenceEquals(pending,t))pending=null;return;}
            if(window?.isShowing==true){Restore(t);return;}
            GameManager.Instance.StartCoroutine(CompleteLater(t,window,current));
        }
        catch{if(ReferenceEquals(pending,t))pending=null;throw;}
    }
    private static void Restore(Transition t)
    {if(!Current(t)){Trace("restore.refused");return;}Trace("restore.before","point="+t.Point);pending=null;t.Cursor.SetScreenPosition(t.Point);Trace("restore.after");}
    private static IEnumerator CompleteLater(Transition t,GUIWindow window,Func<bool> current)
    {
        while(Current(t))
        {
            bool valid=false;
            try{valid=current?.Invoke()==true;}
            catch(Exception error){Log.Warning("[CamperStation] Pointer transition retired: "+error.Message);}
            if(!valid)break;
            if(window?.isShowing==true){Restore(t);yield break;}
            yield return null;
        }
        if(ReferenceEquals(pending,t)){Trace("deferred.retired");pending=null;}
    }
    private static IEnumerator Expire(Transition t)
    {
        while(Current(t))yield return null;
        if(ReferenceEquals(pending,t)){Trace("scope.expired.or.invalid");pending=null;}
    }
}

[HarmonyPatch(typeof(GameManager),nameof(GameManager.isAnyCursorWindowOpen))]
internal static class RebirthCamperStationTransitionCursorWindow
{
    private static void Postfix(GameManager __instance,LocalPlayerUI _ui,ref bool __result)
    {bool native=__result;if(!__result&&ReferenceEquals(__instance,GameManager.Instance)&&RebirthCamperStationPointerTransition.HoldsCursor(_ui))__result=true;
        RebirthCamperStationPointerTransition.CursorDecision(native,__result,_ui!=null);}
}

// SoftCursor.Update has its own modal/GUI-action decision, independent of
// GameManager.isAnyCursorWindowOpen. Preserve only this exact active transition's
// logical cursor allowance; native hidden/locked/input/cancel behavior stays native.
[HarmonyPatch(typeof(SoftCursor),nameof(SoftCursor.SoftcursorAllowed),MethodType.Getter)]
internal static class RebirthCamperStationTransitionSoftCursorAllowed
{
    private static void Postfix(SoftCursor __instance,ref bool __result)
    {
        if(__result||!RebirthCamperStationPointerTransition.HoldsSoftCursor(__instance))return;
        __result=true;
        RebirthCamperStationPointerTransition.SoftCursorDecision(__instance);
    }
}

// The confirmed native Update -> SoftcursorEnabled setter -> SetVisible(false)
// path can still reach this final visibility decision during the empty-modal gap.
// Intercept the actual executed boundary rather than rely solely on a getter
// detour (native/JIT callers may have already inlined the tiny allowance getter).
[HarmonyPatch(typeof(SoftCursor),nameof(SoftCursor.SetVisible))]
internal static class RebirthCamperStationTransitionSoftCursorVisible
{
    private static void Prefix(SoftCursor __instance,ref bool _visible)
    {
        RebirthCamperStationPointerTransition.VisibilityPredicates(__instance,_visible,"guard.entry");
        if(_visible||__instance.hidden||!RebirthCamperStationPointerTransition.HoldsSoftCursor(__instance))return;
        _visible=true;
        RebirthCamperStationPointerTransition.SoftCursorDecision(__instance);
        RebirthCamperStationPointerTransition.Native(__instance,"native.visible.transition-hold","effective=True nativeHidden=False");
    }
}

[HarmonyPatch(typeof(GameManager),nameof(GameManager.setCursorEnabled))]
internal static class RebirthCamperStationCursorEnableTrace
{
    private static void Prefix(bool _e)=>RebirthCamperStationPointerTransition.Trace("native.enabled.before","requested="+_e,!_e);
    private static void Postfix(bool _e)=>RebirthCamperStationPointerTransition.Trace("native.enabled.after","requested="+_e);
}
[HarmonyPatch(typeof(FlexibleCursor),nameof(FlexibleCursor.SetScreenPosition),new Type[]{typeof(Vector2)})]
internal static class RebirthCamperStationCursorPositionTrace
{
    private static void Prefix(FlexibleCursor __instance,Vector2 _newPosition)=>RebirthCamperStationPointerTransition.Native(__instance,"native.position.before","requested="+_newPosition,true);
    private static void Postfix(FlexibleCursor __instance,Vector2 _newPosition)=>RebirthCamperStationPointerTransition.Native(__instance,"native.position.after","requested="+_newPosition);
}
[HarmonyPatch(typeof(SoftCursor),nameof(SoftCursor.SetVisible))]
internal static class RebirthCamperStationCursorVisibleTrace
{
    private static void Prefix(SoftCursor __instance,bool _visible)
    {RebirthCamperStationPointerTransition.VisibilityPredicates(__instance,_visible,"visibility.boundary");
        RebirthCamperStationPointerTransition.Native(__instance,"native.visible.before","requested="+_visible,!_visible);}
    private static void Postfix(SoftCursor __instance,bool _visible)=>RebirthCamperStationPointerTransition.Native(__instance,"native.visible.after","requested="+_visible);
}
