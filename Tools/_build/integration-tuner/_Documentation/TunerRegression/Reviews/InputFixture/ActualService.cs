#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using PlayerAction=InControl.PlayerAction;
using UnityEngine;
using UnityEngine.Scripting;

internal static class RebirthVehicleTuningUi
{
    internal enum State { Off,Edit,Driving,Hidden }
    internal static State Mode {get;private set;}
    internal static RebirthVehicleTuningSession Session {get;private set;}
    internal static bool Transitioning {get;private set;}
    internal static readonly ConditionalWeakTable<EntityVehicle,RebirthVehicleTuningSession> Sessions=new ConditionalWeakTable<EntityVehicle,RebirthVehicleTuningSession>();
    private static LocalPlayerUI ui;
    private static EntityPlayerLocal owner;
    private static bool installed,ending,pendingRestore,keysReleased;
    private static PlayerAction fps,screenshot;
    private static bool fpsEnabled,screenshotEnabled;
    private static int restoreAfterFrame;
    internal static bool Active=>Session!=null&&Mode!=State.Off;
    internal static void Begin(LocalPlayerUI playerUi,RebirthVehicleTuningSession session)
    {
        if(!installed){ModEvents.GameUpdate.RegisterHandler(Tick);ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);ModEvents.GameShutdown.RegisterHandler(GameShutdown);installed=true;}
        End();if(Session!=null||Mode!=State.Off)throw new InvalidOperationException(Localization.Get("xuiRbTuneCloseFailed"));
        ui=playerUi;owner=playerUi.entityPlayer;Session=session;
        try{EnsureHotkeysAvailable();SuppressTwoHotkeys();keysReleased=false;OpenEdit(true);}catch{End();throw;}
    }
    private static void EnsureHotkeysAvailable()
    {
        var local=ui.playerInput;
        var sets=new InControl.PlayerActionSet[]{local,local.GUIActions,local.VehicleActions,local.PermanentActions,RebirthNativeControls.Actions,PlayerActionsGlobal.Instance};
        foreach(var set in sets)if(set!=null)foreach(var action in set.Actions)
        {
            if(ReferenceEquals(action,PlayerActionsGlobal.Instance.ShowFPS)||ReferenceEquals(action,PlayerActionsGlobal.Instance.Screenshot))continue;
            foreach(var binding in action.Bindings)
            {
                string key=binding?.Name?.Replace(" ","");
                if(string.Equals(key,"F8",StringComparison.OrdinalIgnoreCase)||string.Equals(key,"F9",StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,Localization.Get("xuiRbTuneKeyConflict"),action.Name));
            }
        }
    }
    private static void SuppressTwoHotkeys()
    {
        // Preserve exact pre-session flags. Never disable the global action set.
        pendingRestore=false;
        if(fps==null)
        {
            fps=PlayerActionsGlobal.Instance.ShowFPS;screenshot=PlayerActionsGlobal.Instance.Screenshot;
            fpsEnabled=fps.Enabled;screenshotEnabled=screenshot.Enabled;
        }
        fps.Enabled=false;screenshot.Enabled=false;
    }
    private static void RestoreTwoHotkeys()
    {
        if(!pendingRestore||Time.frameCount<=restoreAfterFrame||Input.GetKey(KeyCode.F8)||Input.GetKey(KeyCode.F9))return;
        if(fps!=null)fps.Enabled=fpsEnabled;
        if(screenshot!=null)screenshot.Enabled=screenshotEnabled;
        fps=null;screenshot=null;pendingRestore=false;
    }
    private static bool CloseNative(string id)
    {
        if(ui==null||ui.windowManager==null)return true;
        GUIWindow window;
        if(!ui.windowManager.TryGetWindow(id,out window))return true;
        // A throwing OnClose can leave the action pop pending; a second native Close
        // skips the already-hidden callback and completes native action cleanup.
        for(int attempt=0;attempt<2;attempt++)
        {
            try
            {
                ui.windowManager.Close(window);
                if(!ui.windowManager.IsWindowOpen(window)&&!window.bActionSetEnabled)return true;
            }
            catch(Exception error){if(attempt==1)Log.Warning("[REBIRTH VehicleTuning] close "+id+": "+error.Message);}
        }
        return false;
    }
    private static void CloseForTransition()
    {
        bool edit=CloseNative("rebirthVehicleTuning"),preview=CloseNative("rebirthVehicleTuningPreview");
        if(!edit||!preview)throw new InvalidOperationException(Localization.Get("xuiRbTuneCloseFailed"));
    }
    internal static void End()
    {
        if(ending)return;ending=true;Transitioning=true;
        try
        {
            bool edit=CloseNative("rebirthVehicleTuning"),preview=CloseNative("rebirthVehicleTuningPreview");
            if(!edit||!preview)return;
            Mode=State.Off;Session=null;owner=null;ui=null;
            if(fps!=null){pendingRestore=true;restoreAfterFrame=Time.frameCount+1;}
        }
        finally{Transitioning=false;ending=false;}
    }
    internal static void EmergencyClose(LocalPlayerUI playerUi)
    {
        End();
        // Also close an orphaned group if a session was lost before recovery.
        playerUi?.windowManager?.Close("rebirthVehicleTuning");
        playerUi?.windowManager?.Close("rebirthVehicleTuningPreview");
    }
    internal static void OpenEdit(bool fromConsole=false)
    {
        if(Session==null||ui==null)return;
        var manager=ui.windowManager;
        var modal=manager.GetModalWindow();
        if(!fromConsole&&((modal!=null&&modal.Id!="rebirthVehicleTuning")||manager.IsInputActive()))return;
        GUIWindow window;
        if(!manager.TryGetWindow("rebirthVehicleTuning",out window)||!(window is XUiWindowGroup group))throw new InvalidOperationException(Localization.Get("xuiRbVehicleTune23"));
        var view=group.Controller.GetChildByType<XUiC_RebirthVehicleTuning>();
        if(view==null)throw new InvalidOperationException(Localization.Get("xuiRbVehicleTune23"));
        Transitioning=true;
        try
        {
            CloseForTransition();
            Mode=State.Edit;view.Bind(Session);window.alwaysUsesMouseCursor=true;
            manager.Open(window,true,false);
        }
        catch{End();throw;}
        finally{Transitioning=false;}
    }
    internal static void Drive()
    {
        if(Session==null||ui==null)return;
        GUIWindow window;
        if(!ui.windowManager.TryGetWindow("rebirthVehicleTuningPreview",out window)||!(window is XUiWindowGroup group)||group.hasActionSetFor!=XUiWindowGroup.EHasActionSetFor.None)
            throw new InvalidOperationException(Localization.Get("xuiRbTunePreviewUnavailable"));
        Transitioning=true;
        try
        {
            // Close normally: native OnClose and action-set Pop must run before the passive HUD.
            CloseForTransition();
            Mode=State.Driving;window.alwaysUsesMouseCursor=false;
            ui.windowManager.Open(window,false,false);
        }
        catch{End();throw;}
        finally{Transitioning=false;}
    }
    internal static void Hide()
    {
        if(Session==null)return;
        Transitioning=true;
        try{CloseForTransition();Mode=State.Hidden;}
        finally{Transitioning=false;}
    }
    internal static void NativeClosed(State state)
    {
        if(!Transitioning&&Mode==state)End();
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        RestoreTwoHotkeys();
        if(Session==null)return;
        if(!Session.Valid||ui==null||owner==null||owner.IsDead()||!ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer(),owner))
        {End();return;}
        if(!Input.GetKey(KeyCode.F8)&&!Input.GetKey(KeyCode.F9))keysReleased=true;
        if(!keysReleased||GUIWindowConsole.IsOpen()||Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)||
            Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt)||Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))return;
        var modal=ui.windowManager.GetModalWindow();
        if(modal!=null&&modal.Id!="rebirthVehicleTuning")return;
        try
        {
            if(Input.GetKeyDown(KeyCode.F8))
            {keysReleased=false;if(Mode==State.Edit)Drive();else OpenEdit();}
            else if(Input.GetKeyDown(KeyCode.F9))
            {keysReleased=false;if(Mode==State.Hidden)Drive();else Hide();}
        }
        catch(Exception error){Log.Warning("[REBIRTH VehicleTuning] "+error.Message);End();}
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data){End();}
    private static void GameShutdown(ref ModEvents.SGameShutdownData data)
    {
        End();
        // No later native global-input frame exists after final game shutdown.
        if(fps!=null)fps.Enabled=fpsEnabled;if(screenshot!=null)screenshot.Enabled=screenshotEnabled;
        fps=null;screenshot=null;pendingRestore=false;
    }
}


#endif
