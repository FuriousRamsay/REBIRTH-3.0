using System;

internal static partial class RebirthHydrationProposalServer
{
    internal static bool TryRetireRejected(EntityPlayer player,Guid operation,string digest)
    {
        if(player==null||RebirthHydrationServerSourceHold.HoldsPlayer(player)||!RebirthHydrationTransferJournal.SavedRejection.TryRead(player,operation,digest,out _))return false;
        if(!proposals.TryGetValue(operation,out var original))return true;
        if(original.Original.Digest!=digest)return false;
        return proposals.Remove(operation);
    }
}
