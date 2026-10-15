// Reject stale query replies without inventing local crafting progress.
internal static class RebirthCamperOverviewSnapshotPolicy
{
    internal static bool IsFresh(RebirthCamperTransferSnapshot current,RebirthCamperTransferSnapshot next)
    {
        if(next==null)return false;
        if(current==null)return true;
        if(current.Stations.VehicleIdentity!=next.Stations.VehicleIdentity
            ||next.Stations.Revision<current.Stations.Revision)return false;
        if(next.Stations.Revision>current.Stations.Revision)return true;
        for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
        {
            var previous=current.Stations.GetSlot(slot);
            var incoming=next.Stations.GetSlot(slot);
            if(previous==null&&incoming==null)continue;
            if(previous==null||incoming==null||previous.Id!=incoming.Id||incoming.State.Revision<previous.State.Revision)
                return false;
        }
        return true;
    }
}
