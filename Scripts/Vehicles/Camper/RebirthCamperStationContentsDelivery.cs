using System;

// Called only by the admitted, confirmed removal path, never by a slot preview.
internal static partial class RebirthCamperStationContentsDelivery
{
    internal static bool RecoverHeld(EntityPlayerLocal player,EntityVehicle vehicle,XUi ui,
        RebirthCamperTransferJournal journal,string ownerCreation)
    {
        var held=ui?.DragAndDropWindow?.CurrentStack;
        if(held?.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)!=true
            ||journal==null||!CurrentRecipient(player,vehicle,ui)||ownerCreation!=RebirthCamperCharacterAccess.CurrentCreation(player)||
            !RebirthCamperPortableStation.TryGetSourceAddress(held,out var address))return false;
        RebirthCamperTransferSnapshot selectedBefore=null,selectedAfter=null;
        foreach(var receipt in journal.FindApplied(address.Vehicle,ownerCreation))
        {
            var after=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.After);
            if(!RebirthCamperRemoteStationGridTransfer.Same(held,after.Cursor))continue;
            var before=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.Before);
            if(before.Stations.Find(address.Installation)==null||after.Stations.Find(address.Installation)!=null)continue;
            if(selectedBefore!=null)return false; // Never choose the first of ambiguous applied removals.
            selectedBefore=before;selectedAfter=after;
        }
        if(selectedBefore==null||!CurrentRecipient(player,vehicle,ui))return false;
        Confirm(player,vehicle,ui,selectedBefore,selectedAfter);
        return !RebirthCamperRemoteStationGridTransfer.Same(held,ui.DragAndDropWindow.CurrentStack);
    }
    internal static void Confirm(EntityPlayerLocal player,EntityVehicle vehicle,XUi ui,
        RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after)
    {
        if(!CurrentRecipient(player,vehicle,ui)||before==null||after==null
            ||before.Stations.VehicleIdentity!=after.Stations.VehicleIdentity||ui?.DragAndDropWindow==null)return;
        var cursor=ui.DragAndDropWindow;
        var held=after.BackpackSlot<0?cursor.CurrentStack:player.bag.ItemGrid.items[after.BackpackSlot];
        if(!RebirthCamperRemoteStationGridTransfer.Same(held,after.Cursor)
            ||!RebirthCamperStationRemovalContents.TryRead(after.Cursor,out var contents)
            ||!RebirthCamperPortableStation.TryGetSourceAddress(after.Cursor,out var address)
            ||address.Vehicle!=before.Stations.VehicleIdentity||after.Stations.Find(address.Installation)!=null)return;
        var original=before.Stations.Find(address.Installation);
        if(original==null)return;
        var expected=RebirthCamperStationRemovalContents.Prepare(address.Vehicle,original);
        if(!RebirthCamperRemoteStationGridTransfer.Same(expected.Carrier,after.Cursor))
            throw new InvalidOperationException("Removal contents differ from the confirmed station image.");
        // The immutable actual removal owns this continuation. Native owner
        // prefixes and any actual saved overflow precede envelope retirement.
        StartParcelOwner(player,before,after);
    }
    private static bool CurrentRecipient(EntityPlayerLocal player,EntityVehicle vehicle,XUi ui)
        =>ThreadManager.IsMainThread()&&player!=null&&vehicle!=null&&!player.IsDead()&&player.world!=null&&
            ReferenceEquals(player.world,GameManager.Instance?.World)&&ReferenceEquals(vehicle.world,player.world)&&
            ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
            ReferenceEquals(player.world.GetEntity(vehicle.entityId),vehicle)&&ReferenceEquals(ui?.playerUI?.entityPlayer,player)&&
            ReferenceEquals(player.PlayerUI?.xui,ui);
}
