// Release isolation only. No public enable switch and no saved schema changes.
internal static class RebirthNpcWorkReleaseGate
{
    internal static bool Enabled => false;
    internal const string Detail="NPC work is deferred for this release; existing work custody is retained.";
    internal static bool Hold(RebirthNpcCommandKind kind)=>!Enabled&&kind==RebirthNpcCommandKind.Work;
    internal static bool Hold(RebirthNpcOrderState order)=>!Enabled&&order==RebirthNpcOrderState.Work;
    internal static bool HoldEntity(int entityId)
    {return !Enabled&&(RebirthNpcExecutionLeaseRegistry.IsCurrentWorldHeldWorkEntity(entityId)||RebirthNpcExecutionPersistenceStore.IsCurrentWorldHeldSavedWorkOwner(entityId));}
    internal static bool HoldLease(ulong leaseId)
    {return !Enabled&&RebirthNpcExecutionLeaseRegistry.IsHeldWorkLease(leaseId);}
}