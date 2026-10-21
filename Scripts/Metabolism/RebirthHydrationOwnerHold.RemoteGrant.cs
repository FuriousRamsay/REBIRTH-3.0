internal static partial class RebirthHydrationOwnerHold
{
    internal static bool TryGetRemoteOriginal(EntityPlayerLocal player,out RebirthHydrationTransferOriginal original)
    {
        original=null;
        if(player==null||player.world?.IsRemote()!=true||!entries.TryGetValue(player,out var entry)||!Current(entry))return false;
        original=entry.Original;return true;
    }
}
