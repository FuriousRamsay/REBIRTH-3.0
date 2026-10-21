internal sealed partial class RebirthHydrationColdRecoveryLease
{
    internal static bool TryGetRetained(EntityPlayerLocal player,out RebirthHydrationColdRecoveryLease lease)
    {
        lease=null;if(!ThreadManager.IsMainThread()||player==null)return false;
        foreach(var candidate in retained.Values)if(ReferenceEquals(candidate.Player,player)){lease=candidate;return true;}
        return false;
    }
    internal bool TryComplete()=>RebirthHydrationTransferJournal.ColdRecoveryTerminal.TryPublish(this,out var terminal)&&
        RebirthHydrationOwnerHold.TryReleaseColdRecovery(terminal);
    internal bool TryRetire(RebirthHydrationTransferJournal.ColdRecoveryTerminal terminal)
    {
        if(terminal==null||!terminal.CurrentFor(this)||!retained.TryGetValue(Original.Operation,out var current)||!ReferenceEquals(current,this))return false;
        return retained.Remove(Original.Operation);
    }
}
