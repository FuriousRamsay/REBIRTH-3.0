using System;
using System.Collections.Generic;

// Detached preparation only; the transfer coordinator owns delivery and overflow.
internal sealed class RebirthCamperStationRemovalContents
{
    internal const string ContentsKey = "RebirthCamperRemovalContents";
    internal readonly ItemStack Carrier;
    internal readonly ItemStack[] Contents;
    private RebirthCamperStationRemovalContents(ItemStack carrier,ItemStack[] contents)
    { Carrier=carrier; Contents=contents; }

    internal static RebirthCamperStationRemovalContents Prepare(Guid vehicle,
        RebirthCamperStationInstallations.Installation installation)
    {
        if(vehicle==Guid.Empty||installation==null)throw new ArgumentException("Installed station required.");
        var original=installation.State;
        var detached=RebirthCamperStationSnapshot.Restore(installation.Id,installation.InstalledItem,
            RebirthCamperStationSnapshot.Capture(installation.Id,original));
        var contents=new List<ItemStack>();
        foreach(var grid in new[]{detached.Output,detached.Fuel,detached.Tools})
            foreach(var item in grid)
                if(item!=null&&!item.IsEmpty())contents.Add(item.Clone());
        detached.Output=ItemStack.CreateArray(detached.Output.Length);
        detached.Fuel=ItemStack.CreateArray(detached.Fuel.Length);
        detached.Tools=ItemStack.CreateArray(detached.Tools.Length);
        detached.isBurning=false;
        detached.currentBurnTimeLeft=0f;
        detached.lastTickTime=0;
        var carrier=RebirthCamperPortableStation.Capture(installation.InstalledItem,detached,
            new RebirthCamperStationAddress(vehicle,installation.Id));
        if(contents.Count>0)
        {
            if(carrier.itemValue.Metadata!=null&&carrier.itemValue.Metadata.Count>=byte.MaxValue)
                throw new InvalidOperationException("No room for native removal-content metadata.");
            string frame=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(contents.ToArray()));
            if(frame.Length>12*1024*1024)throw new InvalidOperationException("Removal contents exceed frame limit.");
            carrier.itemValue.SetMetadata(ContentsKey,frame);
        }
        return new RebirthCamperStationRemovalContents(carrier,contents.ToArray());
    }
    internal static bool TryRead(ItemStack carrier,out ItemStack[] contents)
    {
        contents=null;
        if(carrier?.itemValue==null||!carrier.itemValue.TryGetMetadata(ContentsKey,out string frame))return false;
        if(string.IsNullOrEmpty(frame)||frame.Length>12*1024*1024)
            throw new InvalidOperationException("Invalid removal-content frame.");
        contents=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(frame));
        return true;
    }
    internal static ItemStack WithoutContents(ItemStack carrier)
    {
        var copy=carrier.Clone();
        copy.itemValue.Metadata?.Remove(ContentsKey);
        return copy;
    }
}
