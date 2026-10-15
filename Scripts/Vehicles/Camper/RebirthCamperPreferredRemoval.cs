internal static class RebirthCamperPreferredRemoval
{
    internal static void Confirm(EntityPlayerLocal player,EntityVehicle vehicle,RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after)
    {
        var ui=LocalPlayerUI.GetUIForPlayer(player)?.xui;
        if(ui==null)return;
        RebirthCamperStationContentsDelivery.Confirm(player,vehicle,ui,before,after);
        foreach(var name in new[]{"rebirthCamperStorageManagement","rebirthCamperStationManagement"})
        {
            var view=ui.FindWindowGroupByName(name)?.GetChildByType<XUiC_RebirthCamperModules>();
            if(view?.IsOpen==true&&ReferenceEquals(view.BoundVehicle,vehicle))view.CompletePreferredRemoval(before,after);
        }
    }
}
