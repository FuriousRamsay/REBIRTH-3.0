internal static partial class RebirthHydrationServerSourceHold
{
    internal static bool RetireAcknowledgedHistory(EntityPlayer player,System.Guid operation,string digest)
    {
        if(!RebirthHydrationTransferJournal.TryReadAcknowledged(player,operation,digest,out var original)||
            !RebirthHydrationRemoteGrant.RetireAcknowledgedHistory(player,operation,digest))return false;
        var remove=new System.Collections.Generic.List<EntityPlayer>();
        foreach(var pair in leases)if(pair.Value.Original.Digest==original.Digest)remove.Add(pair.Key);
        if(!RebirthHydrationTransferJournal.TryReadAcknowledged(player,operation,digest,out _))return false;
        foreach(var key in remove)leases.Remove(key);
        reenteredRemoteLeases.RemoveAll(value=>value.Original.Digest==original.Digest);
        return true;
    }
    internal static bool TryReleaseAcknowledged(RebirthHydrationTransferJournal.RemoteTerminal terminal)
    {
        var lease=terminal?.Lease;
        if(lease==null||!terminal.CurrentForAcknowledgment(lease)||!leases.TryGetValue(lease.Player,out var current)||!ReferenceEquals(current,lease))return false;
        if(!RebirthHydrationRemoteGrant.TryRetireAcknowledged(terminal)||!leases.TryGetValue(lease.Player,out current)||!ReferenceEquals(current,lease))return false;
        if(!leases.Remove(lease.Player))return false;
        var remove=new System.Collections.Generic.List<EntityPlayer>();
        foreach(var pair in leases)if(pair.Value.Original.Digest==lease.Original.Digest&&!pair.Value.Current())remove.Add(pair.Key);
        foreach(var key in remove)leases.Remove(key);
        reenteredRemoteLeases.RemoveAll(value=>value.Original.Digest==lease.Original.Digest&&!value.Current());
        return true;
    }
    internal static bool TryReleaseRemote(RebirthHydrationTransferJournal.RemoteTerminal terminal)
    {
        var lease=terminal?.Lease;
        if(lease==null||!terminal.CurrentFor(lease)||!leases.TryGetValue(lease.Player,out var current)||!ReferenceEquals(current,lease))return false;
        // Exact privately retained source only; client owner custody has its
        // own authenticated saved terminal protocol and is not released here.
        if(!RebirthHydrationRemoteGrant.TryRetireIfIssued(terminal)||!leases.TryGetValue(lease.Player,out current)||!ReferenceEquals(current,lease))return false;
        return leases.Remove(lease.Player);
    }
}
