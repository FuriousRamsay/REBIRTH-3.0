// AUTHORED_NOT_RUN. Financial wrapper scope/path remain adapters; this phase
// observation reads actual immutable fixture WAL rather than a success flag.
internal static partial class RebirthHydrationTransferJournal
{
    internal static bool HasAttempted(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {var journal=Open(original,false);var record=journal?.Read(original.Operation);return Matches(record,original)&&
        (record.State==RebirthCamperTransferJournal.Phase.Uncertain||record.State==RebirthCamperTransferJournal.Phase.Applied);}
}
