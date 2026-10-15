using System;

// Server policy comes from the installed module's actual native window selection
// and a loaded server template. Missing/ambiguous templates fail closed.
internal static class RebirthCamperRemoteStationPlayerSource
{
    internal static bool TryResolve(EntityPlayer player,RebirthCamperStationState station,out bool actorSource)
    {
        actorSource=false;
        if(!ThreadManager.IsMainThread()||player==null||station==null||player.world==null||player.world.IsRemote()||
            !RebirthCamperRemoteStationTransport.TryGetCurrent(player,out var current,out var address,out var creation,out var lease)||
            !ReferenceEquals(current,station)||!RebirthCamperRemoteStationTransport.IsCurrent(player,address,creation,lease))return false;
        var vehicle=station.BoundVehicle;if(vehicle==null)return false;
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        var installed=runtime?.Stations.Find(address.Installation);
        if(installed==null||!ReferenceEquals(installed.State,station)||
            !RebirthCamperCraftingStation.TryGetPresentation(installed.InstalledItem,out var presentation))return false;
        // A remote client's UI is never used as server evidence. Dedicated
        // servers lacking a loaded native template cannot admit this request.
        var primary=player.world.GetPrimaryPlayer();
        var ui=primary==null?null:LocalPlayerUI.GetUIForPlayer(primary);
        GUIWindow window;
        if(ui!=null&&ui.windowManager.TryGetWindow(presentation.WindowName,out window)&&window is XUiWindowGroup group&&
            group.Controller is XUiC_WorkstationWindowGroup controller)
            actorSource=controller.GetChildByType<XUiC_WorkstationInputGrid>()==null;
        else if(!RebirthCamperStationTemplateSourcePolicy.TryGet(presentation.WindowName,out actorSource))return false;
        return RebirthCamperRemoteStationTransport.IsCurrent(player,address,creation,lease)&&
            ReferenceEquals(runtime.Stations.Find(address.Installation),installed);
    }
    internal static bool IsCurrent(EntityPlayer player,RebirthCamperStationState station)
        =>TryResolve(player,station,out var actorSource)&&actorSource;
}
