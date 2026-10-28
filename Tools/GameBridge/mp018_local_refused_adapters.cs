// AUTHORED_NOT_RUN. Source Before and grant registry are controlled adapters.
// Native checkpoint/refusal/parser/private rejection/WAL/hold release are actual.
internal static class RebirthMetabolismStateRepository
{
    internal static bool BeforeSaved=true;
    internal static Action DuringBeforeRead;
    internal static bool HasCurrentLocalHydrationBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>BeforeSaved;
    internal static bool HasSavedLocalHydrationBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original){DuringBeforeRead?.Invoke();return BeforeSaved;}
}
internal static class RebirthHydrationTransferGrant
{
    internal static bool Issued;
    internal static bool HasIssued(Guid operation)=>Issued;
}
