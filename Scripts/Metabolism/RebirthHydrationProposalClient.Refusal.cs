using System;

internal static partial class RebirthHydrationProposalClient
{
    // Called only for the matching authenticated server Rejected outcome.
    // With no native hold/grant there is no client financial custody to undo.
    internal static bool TryRefuseUnheld(EntityPlayerLocal player,Guid operation,string digest)
    {
        if(!ThreadManager.IsMainThread()||player==null||!intents.TryGetValue(player,out var intent)||!intent.Current()||
            intent.Operation!=operation||intent.Original?.Digest!=digest||RebirthHydrationOwnerHold.IsHeld(player)||
            RebirthHydrationRemoteClientGrant.HasIssued(operation))return false;
        return RebirthHydrationClientOriginals.Close(player,intent.Original)&&intents.Remove(player);
    }
}
