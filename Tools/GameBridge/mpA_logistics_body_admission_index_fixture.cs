using System;

// PREPARED NOT RUN. Harness supplies exact real retained originals/header/file
// states; this fixture does not fabricate native Before/fatal/publication proof.
internal static class mpA_logistics_body_admission_index_fixture
{
    internal static void RequireReadyMissingIndexUnknown(RebirthLogisticsNativeBodyIntent.Command command,string acceptedHeader)
    {
        if(RebirthLogisticsBodyAdmissionIndex.Ready(command,acceptedHeader))throw new Exception("Missing/corrupt Ready index became explicit None.");
    }
    internal static void RequirePreparedOriginal(RebirthLogisticsTransferJournal.Record original,string header,string exactNonce,string canonical)
    {
        if(!RebirthLogisticsBodyAdmissionIndex.TryPrepared(original,header,out var nonce,out var native)||nonce!=exactNonce||native!=canonical)
            throw new Exception("Positive Prepared lost its immutable original body.");
    }
    internal static void RequireAttemptedCannotPrepare(RebirthLogisticsTransferJournal.Record original,string header,string nonce,string canonical)
    {
        if(RebirthLogisticsBodyAdmissionIndex.Prepare(original,header,nonce,canonical)||RebirthLogisticsBodyAdmissionIndex.TryPrepared(original,header,out _,out _))
            throw new Exception("Attempted index replayed as Prepared.");
    }
    internal static void RequireWrongHeaderRejected(RebirthLogisticsTransferJournal.Record original,string wrongHeader,string nonce,string canonical)
    {
        if(RebirthLogisticsBodyAdmissionIndex.Prepare(original,wrongHeader,nonce,canonical)||RebirthLogisticsBodyAdmissionIndex.Attempt(original,wrongHeader,nonce,canonical))
            throw new Exception("A later unrelated native header rebound old body authority.");
    }
    internal static void RequireInitializingCannotAdmit(RebirthLogisticsTransferJournal.Record original)
    {
        if(RebirthLogisticsHeaderAdmission.TryBodyIndexHeader(original,out _,out _))throw new Exception("Initializing header armed body authority.");
    }
    internal static void RequireHistoricalPreparedRecovery(RebirthLogisticsTransferJournal.Record original,
        RebirthLogisticsNativeDeathReceipt.Record fatal,EntityPlayer current,NetPackageRequestToSpawnEntity exactHistoricalPacket,string header,string nonce,string canonical)
    {
        if(!RebirthLogisticsNativeInvocationJournal.TryPrepareHistorical(original,fatal,current,exactHistoricalPacket)||
            !RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var actualNonce,out var native,out int state)||state!=0||actualNonce!=nonce||native!=canonical)
            throw new Exception("Positive original index Prepared did not recover same historical generic Prepared.");
        RequirePreparedOriginal(original,header,nonce,canonical);
    }
    internal static void RequireColdAttemptStillReadyButNeverPrepared(RebirthLogisticsTransferJournal.Record original,string header,string nonce,string canonical)
    {
        if(!RebirthLogisticsBodyAdmissionIndex.Ready(RebirthLogisticsNativeBodyIntent.Command.From(original),header))throw new Exception("Exact flushed Attempted temp was not preserved as monotone original authority.");
        RequireAttemptedCannotPrepare(original,header,nonce,canonical);
    }
    internal static void RequireMonotoneFirstAttempt(RebirthLogisticsTransferJournal.Record original,string header,string nonce,string canonical)
    {
        RequirePreparedOriginal(original,header,nonce,canonical);
        if(!RebirthLogisticsBodyAdmissionIndex.Attempt(original,header,nonce,canonical)||RebirthLogisticsBodyAdmissionIndex.Attempt(original,header,nonce,canonical))
            throw new Exception("Original native invocation did not consume one monotone attempt.");
        RequireAttemptedCannotPrepare(original,header,nonce,canonical);
    }
    // Deferred harness matrix: Header initial temp/Initializing->Ready split;
    // None->Prepared temp resumes same nonce; Prepared->Attempted temp refuses;
    // Ready index deleted, legacy absent/Prepared/Attempted, source root lost
    // with positive index Prepared vs Attempted, fatal-before/after fragments,
    // reconnect ref collision, ACK loss/cancel before hold, A/B purpose collision.
    // Native same-snapshot body/marker is mandatory for repair4->5; no fixture
    // treats Attempted before-pointer observation as historical no-effect.
}
