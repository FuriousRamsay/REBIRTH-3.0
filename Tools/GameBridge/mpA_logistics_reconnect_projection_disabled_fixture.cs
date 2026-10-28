using System;

// PREPARED NOT RUN. Uses real received packets/retained originals only.
// No projection API or synthetic authority; production projection stays disabled.
internal static class mpA_logistics_reconnect_projection_disabled_fixture
{
    internal static void RequireOldReferenceRetained(NetPackageRequestToSpawnEntity actual, World world,
        RebirthLogisticsTransferJournal.Record original)
    {
        if (actual?.ecd == null || actual.Sender == null || original == null)
            throw new Exception("Actual authenticated old-reference request required.");
        string before = Convert.ToBase64String(RebirthLogisticsNativeInvocationJournal.Encode(actual.ecd));
        int beforeId = actual.ecd.id;
        if (!RebirthLogisticsNativeInvocationJournal.MustRefuseRetainedOldReference(actual, world, original))
            throw new Exception("Exact retained old-reference original was not refused.");
        if (RebirthLogisticsNativeInvocationJournal.IsExecutingPrepared(actual, world))
            throw new Exception("Passive reconnect classification created execution permission.");
        if (beforeId != actual.ecd.id || before != Convert.ToBase64String(RebirthLogisticsNativeInvocationJournal.Encode(actual.ecd)))
            throw new Exception("Disabled projection changed actual native ECD.");
    }
    internal static void RequireUnrelatedUntouched(NetPackageRequestToSpawnEntity actual, World world,
        RebirthLogisticsTransferJournal.Record original)
    {
        if (actual?.ecd == null) throw new Exception("Actual unrelated native request required.");
        string before = Convert.ToBase64String(RebirthLogisticsNativeInvocationJournal.Encode(actual.ecd));
        if (RebirthLogisticsNativeInvocationJournal.MustRefuseRetainedOldReference(actual, world, original))
            throw new Exception("Unrelated canonical request classified as retained original.");
        if (before != Convert.ToBase64String(RebirthLogisticsNativeInvocationJournal.Encode(actual.ecd)))
            throw new Exception("Unrelated native request mutated.");
    }
}
