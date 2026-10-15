using System;

// Authoritative local window binding. Remote clients require the server session
// transport; never deserialize a client-provided station manifest here.
public static class RebirthCamperStationAccess
{
    public const string PickerWindow = "rebirthCamperUseStation";

    public static void OpenPicker(EntityVehicle vehicle, EntityPlayerLocal player, Func<bool> authenticatedCharacter)
    {
        if (!ThreadManager.IsMainThread() || authenticatedCharacter == null || !authenticatedCharacter() || !CanAccess(vehicle, player))
            throw new InvalidOperationException("Authoritative camper access required.");
        var runtime = RebirthCamperModuleRuntime.For(vehicle);
        var nativeCurrent = RebirthCamperModuleInteraction.CaptureCurrent(vehicle, player);
        var nativeRelease = RebirthCamperModuleInteraction.CaptureRelease(vehicle, player);
        if (runtime.TransferPending)
            throw new InvalidOperationException("Resolve the pending camper transfer before using crafting stations.");
        var model = runtime.Stations;
        long revision = model.Revision;
        var ui = LocalPlayerUI.GetUIForPlayer(player);
        GUIWindow window;
        if (ui == null || ui.windowManager.IsWindowOpen(PickerWindow)
            || !ui.windowManager.TryGetWindow(PickerWindow, out window)
            || !(window is XUiWindowGroup))
            throw new InvalidOperationException("Station picker is unavailable.");
        var controller = ((XUiWindowGroup)window).Controller.GetChildByType<XUiC_RebirthCamperStationPicker>();
        if (controller == null) throw new InvalidOperationException("Station picker controller is missing.");
        var identities = new Guid[RebirthCamperCraftingStation.ModuleSlots];
        var names = new string[identities.Length];
        for (int slot = 0; slot < identities.Length; slot++)
        {
            var installation = model.GetSlot(slot);
            if (installation == null) continue;
            identities[slot] = installation.Id;
            names[slot] = installation.InstalledItem.itemValue.ItemClass.GetLocalizedItemName()
                + " (" + (slot + 1) + ")";
        }
        Func<bool> current = () => nativeCurrent() && authenticatedCharacter() && CanAccess(vehicle, player)
            && !runtime.TransferPending && ReferenceEquals(runtime.Stations, model) && model.Revision == revision;
        controller.Bind(identities, names, current, id =>
        {
            if (!current()) throw new InvalidOperationException("Station selection expired.");
            var installation = model.Find(id);
            if (installation == null) throw new InvalidOperationException("Selected station is no longer installed.");
            // Closing the picker clears its callbacks, not the captured session.
            controller.RetainLeaseForSelection();
            RebirthCamperStationPointerTransition.Begin(player,vehicle,id);
            try
            {
                RebirthCamperStationPointerTransition.Trace("overview.source.close.before","showing="+window.isShowing);
                ui.windowManager.Close(window);
                RebirthCamperStationPointerTransition.Trace("overview.source.close.after","showing="+window.isShowing);
                RebirthCamperStationWindowSession.Open(vehicle, player, installation,
                    () => current() && ReferenceEquals(model.Find(id), installation),
                    () => { runtime.Persist(true); nativeRelease(); });
            }
            catch
            {
                RebirthCamperStationPointerTransition.Cancel(player,vehicle);
                // Preflight failures occur before a station session can own
                // its close callback. Release only our native vehicle lease.
                if (!RebirthCamperStationWindowSession.IsInUse(installation.State))
                    nativeRelease();
                throw;
            }
        }, () => { RebirthCamperStationPointerTransition.Cancel(player,vehicle);nativeRelease(); }, inspection: id => model.Find(id));
        try
        {
            RebirthCamperStationPointerTransition.BeforeOpen(player,vehicle,Guid.Empty);
            ui.windowManager.Open(window, true, false);
            RebirthCamperStationPointerTransition.Complete(player,vehicle,Guid.Empty,window,current);
            controller.SelectInstallation(RebirthCamperStationSelectionIntent.Take(player,vehicle));
        }
        catch{RebirthCamperStationPointerTransition.Cancel(player,vehicle);throw;}
    }

    private static bool CanAccess(EntityVehicle vehicle, EntityPlayerLocal player)
        => vehicle != null && vehicle.vehicle != null && vehicle.world != null && !vehicle.world.IsRemote()
            && ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId), vehicle)
            && player != null && !player.IsDead() && ReferenceEquals(vehicle.world, player.world)
            && RebirthCamperModuleInteraction.Current(vehicle, player)
            && (vehicle.position - player.position).sqrMagnitude <= 64f
            && vehicle.AllowActivationCommand("service", player);
}
