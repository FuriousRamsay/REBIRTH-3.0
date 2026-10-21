using System;

internal static partial class RebirthHydrationProposalServer
{
    internal static bool TryRetireAcknowledged(EntityPlayer player,Guid operation,string digest)
    {
        if(player==null||RebirthHydrationServerSourceHold.HoldsPlayer(player)||
            !RebirthHydrationTransferJournal.TryReadAcknowledged(player,operation,digest,out var saved))return false;
        if(!proposals.TryGetValue(operation,out var original))return true;
        if(original.Original.Digest!=saved.Digest)return false;
        return proposals.Remove(operation); // Description cache only, no financial effects.
    }
}
