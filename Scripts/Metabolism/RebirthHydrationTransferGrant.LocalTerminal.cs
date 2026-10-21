internal sealed partial class RebirthHydrationTransferGrant
{
    internal bool TryCompleteLocal()
    {
        return RebirthHydrationTransferJournal.LocalTerminal.TryPublish(this,out var terminal)&&
            RebirthHydrationOwnerHold.TryReleaseLocal(terminal);
    }
    internal bool TryRetireLocal(RebirthHydrationTransferJournal.LocalTerminal terminal)
    {
        if(terminal==null||!terminal.CurrentFor(this)||!issued.TryGetValue(Original.Operation,out var current)||
            !ReferenceEquals(current,this))return false;
        return issued.Remove(Original.Operation);
    }
}
