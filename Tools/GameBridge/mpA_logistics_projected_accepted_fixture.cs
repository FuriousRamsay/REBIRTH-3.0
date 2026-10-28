using System;

// PREPARED NOT RUN. Harness supplies an actual native-published original/body.
// Never creates mapping, native save proof, invocation or additional backpack.
internal static class mpA_logistics_projected_accepted_fixture
{
    internal static void RequirePublishedMappedAdoption(RebirthLogisticsTransferJournal.Record original,int actualProjectedOwner)
    {
        if(!RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal))throw new Exception("Actual original fatal required.");
        string immutableFatal=fatal.Physical?.BodyEntityHash;
        if(!RebirthLogisticsAcceptedBodyJournal.HasPublished(original)||!RebirthLogisticsAcceptedBodyJournal.TryAdopt(original,out var accepted)||
            accepted.OriginalOwnerEntity!=actualProjectedOwner||accepted.Backpack.RefPlayerId!=actualProjectedOwner||
            !RebirthLogisticsNativeDeathReceipt.PhysicalPacketMatches(original,fatal,accepted.OriginalOwnerEntity,accepted.PacketBagHash,accepted.PacketEntityHash))
            throw new Exception("Actual projected body lacks original-bound native publication.");
        if(!RebirthLogisticsNativeDeathReceipt.TryRead(original,out var after)||after.Physical.BodyEntityHash!=immutableFatal)
            throw new Exception("Projected body overwrote immutable fatal ECD.");
    }
    internal static void RequireMissingMappingRetained(RebirthLogisticsTransferJournal.Record actualUnknown)
    {
        if(RebirthLogisticsAcceptedBodyJournal.TryAdopt(actualUnknown,out _))throw new Exception("Unknown mapping/native marker adopted.");
    }
    internal static void RequireSameTrackingEntry(EntityPlayer actor,ClientInfo sender,string owner,EntityBackpack actual,
        RebirthLogisticsBodyTracking.Entry retained)
    {
        if(!RebirthLogisticsBodyTracking.IsUnchanged(retained,actor,sender,actual)||!RebirthLogisticsBodyTracking.TryObserve(actor,sender,owner,actual,out var observed)||
            observed.Timestamp!=retained.Timestamp||!observed.Position.Equals(retained.Position)||observed.Entity!=retained.Entity)
            throw new Exception("Same-body reconciliation changed original native tracking entry.");
    }
    internal static void RequireHistoricalPacketOwnerPreserved(RebirthLogisticsTransferJournal.Record original,int historicalPacketOwner,int actualLiveOwner)
    {
        if(!RebirthLogisticsAcceptedBodyJournal.HasPublished(original)||!RebirthLogisticsDeathCustody.TryGet(original,out var custody)||
            custody.OriginalOwnerEntity!=historicalPacketOwner||custody.NativeOwnerEntity!=actualLiveOwner||custody.Backpack.RefPlayerId!=actualLiveOwner)
            throw new Exception("Same-body repair collapsed immutable packet owner into live reference.");
        if(!RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||
            !RebirthLogisticsNativeDeathReceipt.PhysicalPacketMatches(original,fatal,custody.OriginalOwnerEntity,custody.PacketBagHash,custody.PacketEntityHash))
            throw new Exception("Live reference repair changed original physical packet proof.");
    }
    internal static void RequireUnpublishedRepairStillHeld(RebirthLogisticsTransferJournal.Record original,EntityBackpack actual)
    {
        if(actual?.bag?.ItemGrid==null||RebirthLogisticsAcceptedBodyJournal.HasPublished(original)||!RebirthLogisticsDeathSettlement.Blocks(actual.bag.ItemGrid))
            throw new Exception("Unknown reference transition released original native custody.");
    }
    // Harness cases: State0 never adopts; State1 wrong GUID/ref/digest refuses;
    // old fatal hash untouched; legacy v1 unchanged; mapped Attempted ACK loss
    // never respawns; frozen intermediate Prepared rejects before replacement;
    // current native inverse-map collision and reused old ID retain original;
    // tracking missing/evicted/duplicate owner refuses, count3 does not evict.
}
