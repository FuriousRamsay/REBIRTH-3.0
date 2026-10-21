internal static partial class RebirthHydrationOwnerHold
{
    internal static bool TryReleaseLocalPreparedRefusal(RebirthHydrationTransferJournal.LocalPreparedRejection rejection)
    {
        if(rejection==null||!rejection.Current()||!RebirthHydrationLocalRefusedCheckpoint.HasSaved(rejection)||
            !entries.TryGetValue(rejection.Player,out var retained)||retained.Original.Digest!=rejection.Original.Digest||
            !Current(retained)||RebirthHydrationTransferGrant.HasIssued(rejection.Original.Operation))return false;
        return entries.Remove(rejection.Player);
    }
}
