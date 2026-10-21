internal sealed partial class RebirthHydrationRemoteGrant
{
    internal static bool RetireAcknowledgedHistory(EntityPlayer player,System.Guid operation,string digest)
    {
        if(!RebirthHydrationTransferJournal.TryReadAcknowledged(player,operation,digest,out _))return false;
        if(!issued.TryGetValue(operation,out var grant))return true;
        return grant.Original.Digest==digest&&issued.Remove(operation);
    }
    internal static bool TryRetireAcknowledged(RebirthHydrationTransferJournal.RemoteTerminal terminal)
    {
        var lease=terminal?.Lease;
        if(lease==null||!terminal.CurrentForAcknowledgment(lease)||!issued.TryGetValue(lease.Original.Operation,out var grant)||
            !ReferenceEquals(grant.Lease,lease)||grant.Original.Digest!=lease.Original.Digest)return false;
        return issued.Remove(lease.Original.Operation);
    }
    internal static bool TryRetireIfIssued(RebirthHydrationTransferJournal.RemoteTerminal terminal)
    {
        var lease=terminal?.Lease;
        if(lease==null||!terminal.CurrentFor(lease))return false;
        if(!issued.TryGetValue(lease.Original.Operation,out var grant))return true;
        if(!ReferenceEquals(grant.Lease,lease)||grant.Original.Digest!=lease.Original.Digest)return false;
        return issued.Remove(lease.Original.Operation);
    }
}
