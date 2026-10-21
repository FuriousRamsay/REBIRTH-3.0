internal sealed partial class RebirthHydrationRemoteClientGrant
{
    internal static bool RetireHistorical(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        if(!RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,original.Operation,original.Digest,out _)||
            !RebirthHydrationTerminalSeenCheckpoint.HasSavedHistorical(player,original))return false;
        if(!issued.TryGetValue(original.Operation,out var grant))return true;
        return grant.Original.Digest==original.Digest&&!grant.Current()&&issued.Remove(original.Operation);
    }
    internal bool TryRetire(RebirthHydrationRemoteClientTerminal terminal)
    {
        if(terminal==null||!terminal.CurrentFor(this)||!issued.TryGetValue(Original.Operation,out var grant)||!ReferenceEquals(grant,this))return false;
        return issued.Remove(Original.Operation);
    }
}
