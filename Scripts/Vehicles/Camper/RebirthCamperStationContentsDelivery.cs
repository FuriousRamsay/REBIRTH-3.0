using System;
using System.Collections.Generic;
using UnityEngine;

// Called only by the admitted, confirmed removal path, never by a slot preview.
internal static class RebirthCamperStationContentsDelivery
{
    internal static bool RecoverHeld(EntityPlayerLocal player,EntityVehicle vehicle,XUi ui,
        RebirthCamperTransferJournal journal,string ownerCreation)
    {
        var held=ui?.DragAndDropWindow?.CurrentStack;
        if(held?.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)!=true
            ||journal==null||!RebirthCamperPortableStation.TryGetSourceAddress(held,out var address))return false;
        foreach(var receipt in journal.FindApplied(address.Vehicle,ownerCreation))
        {
            var after=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.After);
            if(!RebirthCamperRemoteStationGridTransfer.Same(held,after.Cursor))continue;
            var before=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.Before);
            Confirm(player,vehicle,ui,before,after);
            return !RebirthCamperRemoteStationGridTransfer.Same(held,ui.DragAndDropWindow.CurrentStack);
        }
        return false;
    }
    internal static void Confirm(EntityPlayerLocal player,EntityVehicle vehicle,XUi ui,
        RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after)
    {
        if(player==null||vehicle==null||player.world!=vehicle.world||before==null||after==null
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
        Deliver(player,ui.PlayerInventory,contents);
        var carrier=RebirthCamperStationRemovalContents.WithoutContents(after.Cursor);
        if(after.BackpackSlot<0)cursor.SetCurrentStack(carrier,false);
        else player.bag.SetSlot(after.BackpackSlot,carrier);
    }
    internal static void Deliver(EntityPlayerLocal player,XUiM_PlayerInventory inventory,ItemStack[] contents)
    {
        if(!ThreadManager.IsMainThread()||player==null||player.world==null||player.IsDead()
            ||inventory==null||contents==null)throw new InvalidOperationException("Live removal recipient required.");
        // Resolve and size the native overflow destination before any grants.
        var backpack=EntityFactory.CreateEntity("Backpack".GetHashCode(),
            player.position+Vector3.up*2f) as EntityBackpack;
        if(backpack?.bag?.ItemGrid==null)throw new InvalidOperationException("Native backpack loot entity unavailable.");
        if(backpack.bag.ItemGrid.items.Length<contents.Length)
            backpack.bag.Resize(new Vector2i(8,(contents.Length+7)/8));
        var remaining=new List<ItemStack>();
        try
        {
            foreach(var source in contents)
            {
                if(source==null||source.IsEmpty())continue;
                var offered=source.Clone();
                bool added=inventory.AddItemToBackpack(offered);
                // Native stacking consumes the offered count even when its bool
                // is false. A successful slot add takes the complete remainder.
                if(!added&&offered.count>0)remaining.Add(offered);
            }
            var updated=inventory.Backpack.ItemGrid.items;
            var image=new ItemStack[updated.Length];
            for(int index=0;index<image.Length;index++)image[index]=updated[index].Clone();
            inventory.Backpack.SetSlots(image,false);
            if(remaining.Count==0)return;
            for(int index=0;index<remaining.Count;index++)backpack.bag.SetSlot(index,remaining[index].Clone());
            var creation=new EntityCreationData(backpack)
            {entityName=Localization.Get("xuiRebirthCamperOverflowBag"),id=-1};
            GameManager.Instance.RequestToSpawnEntityServer(creation);
        }
        finally { backpack.OnEntityUnload(); }
    }
}
