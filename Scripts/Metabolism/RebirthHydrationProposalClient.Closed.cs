using System;

internal static partial class RebirthHydrationProposalClient
{
    internal static bool TryRetireClosed(EntityPlayerLocal player,Guid operation,string digest)
    {
        if(!RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,operation,digest,out _)||RebirthHydrationOwnerHold.IsHeld(player))return false;
        if(!intents.TryGetValue(player,out var intent))return true;
        if(!intent.Current()||intent.Operation!=operation||intent.Original?.Digest!=digest)return false;
        return RebirthHydrationClientOriginals.Close(player,intent.Original)&&intents.Remove(player);
    }
}
