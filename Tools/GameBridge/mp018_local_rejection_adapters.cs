// AUTHORED_NOT_RUN. Positive saved source/owner refusal observations are explicit
// adapters here. Actual private rejection/WAL/grant registry/hold release run.
public static partial class RebirthMetabolismStateRepository
{
    internal static bool BeforeSaved=true;
    internal static bool HasCurrentLocalHydrationBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>BeforeSaved;
    internal static bool TrySaveLocalHydrationBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>BeforeSaved;
    internal static bool HasSavedLocalHydrationBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>BeforeSaved;
}
internal static class RebirthHydrationLocalRefusedCheckpoint
{
    internal static bool Saved=true;
    internal static bool TryPersist(RebirthHydrationTransferJournal.LocalPreparedRejection rejection)=>Saved;
    internal static bool HasSaved(RebirthHydrationTransferJournal.LocalPreparedRejection rejection)=>Saved;
}
// This detached conductor/rejection fixture supplies no cold original.
internal sealed class RebirthHydrationColdRecoveryLease
{
    internal static bool TryGetRetained(EntityPlayerLocal player,out RebirthHydrationColdRecoveryLease lease){lease=null;return false;}
    internal static bool TryAcquire(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out RebirthHydrationColdRecoveryLease lease){lease=null;return false;}
    internal bool TryComplete()=>false;
}
