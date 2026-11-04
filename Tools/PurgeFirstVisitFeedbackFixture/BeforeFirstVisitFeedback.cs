// Exact old evidence-owner operation, before extracting the guarded branch.
internal static class RebirthPurgeFirstVisitFeedback
{
    internal static void Observe(RebirthPoiClearanceRecord record,EntityPlayer visitor)
        => RebirthPurgePoiHistory.Discover(record.Identity,RebirthPurgeDiscoveryNotification.DisplayName(visitor));
}
