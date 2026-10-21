internal static partial class RebirthHydrationOwnerHold
{
    internal static bool TryReleaseColdRecovery(RebirthHydrationTransferJournal.ColdRecoveryTerminal terminal)
    {
        var lease=terminal?.Lease;
        if(lease==null||!terminal.CurrentFor(lease)||!entries.TryGetValue(lease.Player,out var entry)||
            entry.Original.Digest!=lease.Original.Digest||!Current(entry))return false;
        if(!lease.TryRetire(terminal)||!entries.TryGetValue(lease.Player,out var current)||!ReferenceEquals(current,entry))return false;
        return entries.Remove(lease.Player);
    }
}
