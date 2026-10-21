internal static partial class RebirthHydrationServerSourceHold
{
    internal static bool TryReleasePreparedRejection(RebirthHydrationTransferJournal.PreparedRejection rejection)
    {
        var lease=rejection?.Lease;
        if(lease==null||!rejection.CurrentFor(lease)||!leases.TryGetValue(lease.Player,out var original)||!ReferenceEquals(original,lease))return false;
        if(!leases.Remove(lease.Player))return false;
        var remove=new System.Collections.Generic.List<EntityPlayer>();
        foreach(var pair in leases)if(pair.Value.Original.Digest==lease.Original.Digest&&!pair.Value.Current())remove.Add(pair.Key);
        foreach(var key in remove)leases.Remove(key);
        reenteredRemoteLeases.RemoveAll(value=>value.Original.Digest==lease.Original.Digest&&!value.Current());
        return true;
    }
}
