using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

public static class RebirthCamperStationWindowSession
{
    private sealed class Session
    {
        internal RebirthCamperStationState State;
        internal Func<bool> Current;
        internal Action FlushAndRelease;
        internal EntityVehicle Vehicle;
        internal EntityPlayerLocal Player;
        internal bool Closing;
        internal bool Remote;
        internal bool SwitchRequested;
        internal bool SwitchCloseRegistered;
        internal bool OriginalProjectionPending;
        internal Guid SwitchTarget;
        internal RebirthCamperStationAddress Address;
    }
    private static readonly ConditionalWeakTable<XUiC_WorkstationWindowGroup, Session> sessions
        = new ConditionalWeakTable<XUiC_WorkstationWindowGroup, Session>();
    private static readonly ConditionalWeakTable<EntityVehicle, Dictionary<Guid, XUiC_WorkstationWindowGroup>> leases
        = new ConditionalWeakTable<EntityVehicle, Dictionary<Guid, XUiC_WorkstationWindowGroup>>();
    private static readonly ConditionalWeakTable<EntityPlayerLocal, XUiC_WorkstationWindowGroup> playerWindows
        = new ConditionalWeakTable<EntityPlayerLocal, XUiC_WorkstationWindowGroup>();
    internal static Newtonsoft.Json.Linq.JObject CaptureCommandAudit(EntityPlayerLocal player)
    {
        var result=new Newtonsoft.Json.Linq.JObject{["associated"]=false};
        if(!ThreadManager.IsMainThread()||player==null||!playerWindows.TryGetValue(player,out var window)||!sessions.TryGetValue(window,out var session)||!ReferenceEquals(session.Player,player))return result;
        result["associated"]=true;result["closing"]=session.Closing;result["remote"]=session.Remote;result["switchRequested"]=session.SwitchRequested;
        result["switchCloseRegistered"]=session.SwitchCloseRegistered;result["projectionPending"]=session.OriginalProjectionPending;
        result["switchTarget"]=session.SwitchTarget.ToString("N");result["address"]=session.Address.ToString();result["showing"]=window.windowGroup?.isShowing==true;
        result["current"]=IsCurrent(session);return result;
    }

    public static void Open(EntityVehicle vehicle, EntityPlayerLocal player,
        RebirthCamperStationInstallations.Installation station, Func<bool> current, Action flushAndRelease)
        =>OpenCore(vehicle,player,station,current,flushAndRelease,false,default);

    internal static void OpenRemote(EntityVehicle vehicle,EntityPlayerLocal player,
        RebirthCamperStationInstallations.Installation station,RebirthCamperStationAddress address,
        Func<bool> current,Action release)
        =>OpenCore(vehicle,player,station,current,release,true,address);

    private static void OpenCore(EntityVehicle vehicle,EntityPlayerLocal player,
        RebirthCamperStationInstallations.Installation station,Func<bool> current,Action flushAndRelease,
        bool remote,RebirthCamperStationAddress address)
    {
        if (!ThreadManager.IsMainThread() || vehicle == null || player == null || station == null
            || current == null || flushAndRelease == null || !current()
            || player.IsDead() || (vehicle.position - player.position).sqrMagnitude > 64f
            || !ReferenceEquals(vehicle.world, player.world) || !vehicle.AllowActivationCommand("service", player))
            throw new InvalidOperationException("Authenticated station session required.");
        if(remote && (!vehicle.world.IsRemote() || !address.IsValid || address.Installation!=station.Id))
            throw new InvalidOperationException("Server admitted remote installation required.");
        var runtime = remote?null:RebirthCamperModuleRuntime.For(vehicle);
        if (playerWindows.TryGetValue(player, out _))
            throw new InvalidOperationException("Close the active camper crafting station before opening another.");
        if (!remote && (runtime.TransferPending || !ReferenceEquals(runtime.Stations.Find(station.Id), station)))
            throw new InvalidOperationException("An installed station with no pending transfer is required.");
        RebirthCamperCraftingStation.Presentation presentation;
        if (!RebirthCamperCraftingStation.TryGetPresentation(station.InstalledItem, out presentation))
            throw new InvalidOperationException("Station presentation is missing.");
        var ui = LocalPlayerUI.GetUIForPlayer(player);
        GUIWindow window;
        if (ui == null || ui.windowManager.IsWindowOpen(presentation.WindowName)
            || !ui.windowManager.TryGetWindow(presentation.WindowName, out window)
            || !(window is XUiWindowGroup)) throw new InvalidOperationException("Native station window is missing.");
        var group = (XUiWindowGroup)window;
        var controller = group.Controller as XUiC_WorkstationWindowGroup;
        if (controller == null || sessions.TryGetValue(controller, out _))
            throw new InvalidOperationException("Native station window is unavailable.");
        var vehicleLeases = leases.GetValue(vehicle, _ => new Dictionary<Guid, XUiC_WorkstationWindowGroup>());
        if (vehicleLeases.ContainsKey(station.Id))
            throw new InvalidOperationException("Installed station already has an active session.");
        station.State.BindVehicle(vehicle);
        sessions.Add(controller, new Session { State = station.State, Current = current, FlushAndRelease = flushAndRelease,
            Vehicle = vehicle, Player = player,Remote=remote,
            Address=remote?address:new RebirthCamperStationAddress(runtime.Identity,station.Id) });
        vehicleLeases.Add(station.Id, controller);
        playerWindows.Add(player, controller);
        try
        {
            controller.SetTileEntity(station.State);
            controller.Workstation = presentation.RecipeWorkstationName;
            RebirthCamperStationPointerTransition.BeforeOpen(player,vehicle,station.Id);
            ui.windowManager.Open(window, true, false);
            RebirthCamperStationPointerTransition.Complete(player,vehicle,station.Id,window,current);
            if(!remote)
            {
                RebirthCamperStationNativeActorPayment.RestorePending(vehicle);
                RebirthCamperStationNativeActorPayment.TryResume(player,station.State,
                    new RebirthCamperStationAddress(runtime.Identity,station.Id),RebirthCamperCharacterAccess.CurrentCreation(player));
            }
        }
        catch
        {
            RebirthCamperStationPointerTransition.Cancel(player,vehicle);
            // Opening can run native callbacks before throwing; caller reconciles
            // any resulting inventory state rather than retrying the open blindly.
            // A failed publication retains both locks. Removing them in finally
            // would permit a second editor to overwrite unreconciled changes.
            flushAndRelease();
            Release(controller);
            throw;
        }
    }

    internal static bool Admitted(XUiC_WorkstationWindowGroup window, RebirthCamperStationState state)
        => sessions.TryGetValue(window, out var session) && ReferenceEquals(session.State, state) && IsCurrent(session);

    // A placed workstation has no camper session and never exposes this action.
    public static bool CanSwitch(XUiC_WorkstationWindowGroup window)
        => ThreadManager.IsMainThread() && window != null && sessions.TryGetValue(window, out var session)
            && !session.SwitchRequested && IsCurrent(session) && RebirthCamperRadial.IsCamper(session.Vehicle);

    internal static RebirthCamperStationInstallations InstalledForNavigation(XUiC_WorkstationWindowGroup window)
    {
        if(window==null || !sessions.TryGetValue(window,out var session) || !IsCurrent(session))return null;
        return session.Remote ? RebirthCamperRemoteStationClient.InstalledForNavigation(session.Player)
            : RebirthCamperModuleRuntime.For(session.Vehicle).Stations;
    }

    public static bool Switch(XUiC_WorkstationWindowGroup window, Guid target = default)
    {
        if (!CanSwitch(window)) return false;
        if(target!=Guid.Empty && InstalledForNavigation(window)?.Find(target)==null)return false;
        sessions.TryGetValue(window, out var session);
        session.SwitchTarget=target;
        session.SwitchRequested = true;
        GameManager.Instance.StartCoroutine(SwitchAfterPublication(window, session));
        return true;
    }

    private static IEnumerator SwitchAfterPublication(XUiC_WorkstationWindowGroup window, Session session)
    {
        // Keep the actual window/lease current while prepared cursor operations
        // and native save witnesses finish. Do not cancel or replay those writes.
        while (IsCurrent(session) && (RebirthCamperStationGridCustody.IsHeld(session.State)
            || !RebirthCamperStationOpenQueueProcessing.CanCommit(session.State)
            || session.Remote && RebirthCamperRemoteStationClient.IsPending(session.Player, session.State)))
            yield return null;
        if (!IsCurrent(session)) { session.SwitchRequested = false; yield break; }
        RebirthCamperStationPointerTransition.Begin(session.Player,session.Vehicle,session.SwitchTarget);
        session.Closing = true;
        bool failed = false;
        RebirthCamperStationPointerTransition.Trace("station.source.close.before","showing="+window.windowGroup.isShowing);
        try { window.xui.playerUI.windowManager.Close((GUIWindow)window.windowGroup); }
        catch (Exception error)
        {
            failed = true;
            Log.Warning("[CamperStation] Switch retained the current station: " + error.Message);
        }
        RebirthCamperStationPointerTransition.Trace("station.source.close.after","showing="+window.windowGroup.isShowing+" failed="+failed);
        if (failed)
        {
            // A native callback may throw AFTER presentation became hidden.
            // Continue only this retained remote close identity, never replay
            // native OnClose or any paid station command to infer its outcome.
            if(!session.Remote || window.windowGroup.isShowing || !sessions.TryGetValue(window,out var retained) || !ReferenceEquals(retained,session))
            { RebirthCamperStationPointerTransition.Cancel(session.Player,session.Vehicle);session.SwitchRequested=false;yield break; }
        }
        // Remote Unlock retains the exact window association until the server
        // acknowledges close; local persistence completes synchronously there.
        while (sessions.TryGetValue(window, out var held) && ReferenceEquals(held, session))
        {
            if (!session.Remote || !RebirthCamperModuleInteraction.Current(session.Vehicle, session.Player))
            {
                RebirthCamperStationPointerTransition.Cancel(session.Player,session.Vehicle);
                yield break;
            }
            try
            {
                RegisterSwitchClose(window,session);
                RebirthCamperRemoteStationClient.RetrySwitchClose(session.Player,session.State);
            }
            catch(Exception error){Log.Warning("[CamperStation] Retaining exact hidden station close: "+error.Message);}
            yield return null;
        }
        // Finish every native OnClose callback before requesting the next grant.
        yield return null;
        if (RebirthCamperRadial.Allowed(session.Vehicle, session.Player))
        {
            RebirthCamperStationSelectionIntent.Set(session.Player,session.Vehicle,session.SwitchTarget);
            try {RebirthCamperModuleInteraction.Request(session.Vehicle, session.Player, RebirthCamperRadial.Craft);}
            catch {RebirthCamperStationPointerTransition.Cancel(session.Player,session.Vehicle);throw;}
        }
        else RebirthCamperStationPointerTransition.Cancel(session.Player,session.Vehicle);
    }

    private static bool IsCurrent(Session session)
        => !session.Closing && session.Vehicle != null && session.Player != null
            && session.Vehicle.world != null
            && session.Vehicle.world.IsRemote()==session.Remote
            && ReferenceEquals(session.Vehicle.world.GetEntity(session.Vehicle.entityId), session.Vehicle)
            && (session.Remote || !RebirthCamperModuleRuntime.For(session.Vehicle).TransferPending
                && ReferenceEquals(RebirthCamperModuleRuntime.For(session.Vehicle).Stations.Find(session.State.InstallationId)?.State, session.State))
            && !session.Player.IsDead() && ReferenceEquals(session.Vehicle.world, session.Player.world)
            && UnityEngine.Vector3.SqrMagnitude(session.Vehicle.position - session.Player.position) <= 64f
            && session.Vehicle.AllowActivationCommand("service", session.Player) && session.Current();

    public static bool IsInUse(RebirthCamperStationState station)
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Game thread required.");
        if(RebirthCamperRemoteStationTransport.HasServerLease(station))return true;
        return station != null && station.BoundVehicle != null
            && leases.TryGetValue(station.BoundVehicle, out var vehicleLeases)
            && vehicleLeases.ContainsKey(station.InstallationId);
    }

    // Exact session lookup for mobile crafting authorization. Never resolve a
    // station by the terrain tile beneath the truck or by a client-supplied id.
    public static bool TryGetCurrent(EntityPlayer player, out RebirthCamperStationState state)
    {
        state = null;
        if (!ThreadManager.IsMainThread() || !(player is EntityPlayerLocal local)
            || !playerWindows.TryGetValue(local, out var window)
            || !sessions.TryGetValue(window, out var session)
            || !ReferenceEquals(session.Player, local) || !IsCurrent(session)) return false;
        state = session.State;
        return true;
    }

    public static bool TryGetCurrent(EntityPlayer player, out RebirthCamperStationState state,
        out RebirthCamperStationAddress address)
    {
        address = default(RebirthCamperStationAddress);
        if (!TryGetCurrent(player, out state)) return false;
        var local=(EntityPlayerLocal)player;
        if(!playerWindows.TryGetValue(local,out var window)||!sessions.TryGetValue(window,out var session))return false;
        address=session.Address;
        return true;
    }

    internal static XUiC_WorkstationWindowGroup RemoteWindow(EntityPlayerLocal player,RebirthCamperStationState state)
        =>player!=null && playerWindows.TryGetValue(player,out var window) && sessions.TryGetValue(window,out var session)
            && session.Remote && ReferenceEquals(session.State,state) && IsCurrent(session)?window:null;
    internal static void MarkOriginalProjection(RebirthCamperStationState state)
    {
        if(state?.BoundVehicle==null||!leases.TryGetValue(state.BoundVehicle,out var views)||!views.TryGetValue(state.InstallationId,out var window)||!sessions.TryGetValue(window,out var session)||
            session.Remote||!ReferenceEquals(session.State,state))return;
        session.OriginalProjectionPending=true;
    }
    internal static bool HasOriginalProjection(RebirthCamperStationState state)
    {
        return state?.BoundVehicle!=null&&leases.TryGetValue(state.BoundVehicle,out var views)&&views.TryGetValue(state.InstallationId,out var window)&&sessions.TryGetValue(window,out var session)&&
            !session.Remote&&ReferenceEquals(session.State,state)&&IsCurrent(session)&&session.OriginalProjectionPending;
    }

    internal static bool CloseRemoteForRecovery(EntityPlayerLocal player,RebirthCamperStationState state)
    {
        if(!ThreadManager.IsMainThread() || player==null || state==null
            || !playerWindows.TryGetValue(player,out var window) || !sessions.TryGetValue(window,out var session)
            || !session.Remote || !ReferenceEquals(session.Player,player) || !ReferenceEquals(session.State,state)
            || !ReferenceEquals(window.WorkstationData?.TileEntity,state) || !IsCurrent(session))return false;
        // This is a presentation abort, not a settled switch. Normal native
        // OnClose invokes the exact session's release and preserves its items;
        // remote syncTEfromUI is already suppressed by the existing guard.
        session.SwitchRequested=false;
        session.Closing=true;
        player.PlayerUI.windowManager.Close((GUIWindow)window.windowGroup);
        return true;
    }

    internal static bool TryGetLeaseIdentity(EntityPlayer player,out object identity)
    {
        identity=null;
        if(!TryGetCurrent(player,out _)||!(player is EntityPlayerLocal local)||
            !playerWindows.TryGetValue(local,out var window)||!sessions.TryGetValue(window,out var session))return false;
        identity=session;return true;
    }

    internal static bool BeforeUpdate(XUiC_WorkstationWindowGroup window)
    {
        if (!sessions.TryGetValue(window, out var session)) return true;
        if (IsCurrent(session))
        {
            if(session.OriginalProjectionPending&&!RebirthCamperStationFuelOriginal.IsHeld(session.State))
            {
                session.OriginalProjectionPending=false;
                try{RebirthCamperStationQueueProjection.Refresh(window,session.State);}
                catch(Exception error){Log.Warning("[CamperStation] Native queue display refresh failed: "+error.GetType().Name);}
            }
            return true;
        }
        session.OriginalProjectionPending=false; // Ended presentation never retains a settled native obligation.
        if (!session.Closing)
        {
            session.Closing = true;
            // Native close publishes the window's inventories before releasing
            // this session; do not abandon those items on access revocation.
            window.xui.playerUI.windowManager.Close((GUIWindow)window.windowGroup);
        }
        return false;
    }

    public static void Unlock(LockManager nativeLocks, XUiC_WorkstationWindowGroup window)
    {
        if (!(window.WorkstationData?.TileEntity is RebirthCamperStationState))
        { nativeLocks.UnlockRequestLocal(); return; }
        Session session;
        if (!sessions.TryGetValue(window, out session))
            throw new InvalidOperationException("Mobile station close has no session; retain state for reconciliation.");
        // Native OnClose already synchronized inventories and unsubscribed events.
        // Keep the session if publication fails so it cannot be silently reopened.
        if (session.Remote && session.SwitchRequested)
        {
            RegisterSwitchClose(window,session);
            return;
        }
        session.FlushAndRelease();
        Release(window);
    }

    private static void RegisterSwitchClose(XUiC_WorkstationWindowGroup window,Session session)
    {
        if(session.SwitchCloseRegistered)return;
        if(!session.Remote||!session.SwitchRequested||!sessions.TryGetValue(window,out var held)||!ReferenceEquals(held,session))
            throw new InvalidOperationException("Exact retained remote switch session required.");
        // Client installs completion before sending and catches synchronous send
        // failure without clearing it. Register once; Retry uses that original.
        RebirthCamperRemoteStationClient.CloseForSwitch(session.Player,session.State,()=>Release(window));
        session.SwitchCloseRegistered=true;
    }

    private static void Release(XUiC_WorkstationWindowGroup window)
    {
        if (!sessions.TryGetValue(window, out var session)) return;
        if (!leases.TryGetValue(session.Vehicle, out var vehicleLeases)
            || !vehicleLeases.TryGetValue(session.State.InstallationId, out var holder)
            || !ReferenceEquals(holder, window))
            throw new InvalidOperationException("Station lease changed; retain session for reconciliation.");
        if (!playerWindows.TryGetValue(session.Player, out var playerWindow) || !ReferenceEquals(playerWindow, window))
            throw new InvalidOperationException("Player station session changed; retain lease for reconciliation.");
        try
        {
            if(!session.SwitchRequested)RebirthCamperStationPointerTransition.Cancel(session.Player,session.Vehicle);
        }
        finally
        {
            // Suspension retains financial originals. Pointer failure cannot
            // strand presentation, and reentry cannot erase a replacement.
            if(sessions.TryGetValue(window,out var paymentSession)&&ReferenceEquals(paymentSession,session)&&
                vehicleLeases.TryGetValue(session.State.InstallationId,out var paymentHolder)&&ReferenceEquals(paymentHolder,window)&&
                playerWindows.TryGetValue(session.Player,out var paymentWindow)&&ReferenceEquals(paymentWindow,window))
                RebirthCamperStationNativeActorPayment.Suspend(session.State);
            if(vehicleLeases.TryGetValue(session.State.InstallationId,out var liveHolder)&&ReferenceEquals(liveHolder,window))vehicleLeases.Remove(session.State.InstallationId);
            if(playerWindows.TryGetValue(session.Player,out var liveWindow)&&ReferenceEquals(liveWindow,window))playerWindows.Remove(session.Player);
            if(sessions.TryGetValue(window,out var liveSession)&&ReferenceEquals(liveSession,session))sessions.Remove(window);
        }
    }
}

[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup), nameof(XUiC_WorkstationWindowGroup.Update))]
public static class RebirthCamperStationWindowValidity
{
    private static bool Prefix(XUiC_WorkstationWindowGroup __instance)
        => RebirthCamperStationWindowSession.BeforeUpdate(__instance);
}

[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup), nameof(XUiC_WorkstationWindowGroup.OnClose))]
public static class RebirthCamperStationWindowClose
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(LockManager), nameof(LockManager.UnlockRequestLocal));
        var replacement = AccessTools.Method(typeof(RebirthCamperStationWindowSession), nameof(RebirthCamperStationWindowSession.Unlock));
        var result = new List<CodeInstruction>();
        int count = 0;
        foreach (var source in instructions)
        {
            var code = new CodeInstruction(source);
            if (Equals(code.operand, original))
            {
                var window = new CodeInstruction(OpCodes.Ldarg_0);
                window.labels.AddRange(code.labels); window.blocks.AddRange(code.blocks);
                code.labels.Clear(); code.blocks.Clear();
                result.Add(window); code.opcode = OpCodes.Call; code.operand = replacement; count++;
            }
            result.Add(code);
        }
        if (count != 1) throw new InvalidOperationException("Native station close requires exactly one unlock call.");
        return result;
    }
}
