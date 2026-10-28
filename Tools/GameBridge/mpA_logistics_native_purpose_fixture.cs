using System;

// PREPARED, NOT RUN. Harness pauses only at real original prepared invocation;
// does not create permits, mutate journals, or replay a native ProcessPackage.
internal static class mpA_logistics_native_purpose_fixture
{
    internal static void RequireExactActivePacket(NetPackageRequestToSpawnEntity actualActive,NetPackageRequestToSpawnEntity actualOther,World world)
    {
        if(actualActive==null||actualOther==null||ReferenceEquals(actualActive,actualOther))throw new Exception("Two distinct actual packet objects required.");
        if(!RebirthLogisticsNativeInvocationJournal.IsExecutingPrepared(actualActive,world)||RebirthLogisticsNativeInvocationJournal.IsExecutingPrepared(actualOther,world))throw new Exception("Original purpose leaked beyond exact authenticated packet reference.");
    }
    internal static void RequireOneDomainPurpose(NetPackageRequestToSpawnEntity actual,World world,bool logistics)
    {
        bool a=RebirthLogisticsNativeInvocationJournal.IsExecutingPrepared(actual,world),b=RebirthPlacedOriginalDeathPartition.IsExecutingPreparedBody(actual,world);
        if(a!=logistics||b==logistics)throw new Exception("Actual exact invoking original did not select exactly its own domain.");
    }
    internal static void RequireExactOldReferenceRefusal(NetPackageRequestToSpawnEntity actual,World world,RebirthLogisticsTransferJournal.Record actualHeldOriginal,bool expectedRefusal)
    {
        if(actual?.ecd==null)throw new Exception("Actual received native body required.");
        var bytes=RebirthLogisticsNativeInvocationJournal.Encode(actual.ecd);int id=actual.ecd.id;
        if(RebirthLogisticsNativeInvocationJournal.MustRefuseRetainedOldReference(actual,world,actualHeldOriginal)!=expectedRefusal||actual.ecd.id!=id||Convert.ToBase64String(RebirthLogisticsNativeInvocationJournal.Encode(actual.ecd))!=Convert.ToBase64String(bytes))throw new Exception("Old reference classification changed native payload or mismatched retained original.");
    }
    internal static void RequireScopeDisposed(NetPackageRequestToSpawnEntity actual,World world)
    {
        if(RebirthLogisticsNativeInvocationJournal.IsExecutingPrepared(actual,world))throw new Exception("Disposed original invoke scope remained active.");
    }
    // Same canonical ECD for A and B must select only positively named actual
    // invoking domain. Ordinary unscoped native packet must not bypass either.
    // Failed/duplicate attempted original never gains a new permit.
}
