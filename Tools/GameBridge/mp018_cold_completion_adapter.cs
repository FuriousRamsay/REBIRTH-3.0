// AUTHORED_NOT_RUN. Source After witness and wrapper path/hash are adapters.
// Actual cold producer/owner proof/receipt parser/immutable WAL are embedded.
internal static class RebirthMetabolismStateRepository
{
    internal static bool SourceSaved=true;internal static Action DuringColdRead;
    internal static bool HasSavedColdLocalHydrationAfter(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {DuringColdRead?.Invoke();return SourceSaved;}
}
internal static partial class RebirthHydrationTransferJournal
{
    internal static bool HasApplied(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {var journal=Open(original,false);var record=journal?.Read(original.Operation);return Matches(record,original)&&record.State==RebirthCamperTransferJournal.Phase.Applied;}
}
