using System;

// PREPARED NOT RUN. Actual surviving body/settlement evidence is required.
internal static class mpA_logistics_body_prepare_guard_fixture
{
    internal static void RequireKnownBodyCannotReprepare(RebirthLogisticsTransferJournal.Record original,
        RebirthLogisticsNativeDeathReceipt.Record fatal,EntityPlayer actor,NetPackageRequestToSpawnEntity actual)
    {
        if(!RebirthLogisticsAcceptedBodyJournal.HasAnyCandidate(original)&&!RebirthLogisticsDeathJournal.HasAnyCandidate(original))
            throw new Exception("Actual retained accepted/settlement candidate required.");
        string before=Convert.ToBase64String(RebirthLogisticsNativeInvocationJournal.Encode(actual.ecd));
        if(RebirthLogisticsNativeInvocationJournal.TryPrepare(original,fatal,actor,actual))
            throw new Exception("Surviving body evidence recreated generic Prepared as unused.");
        if(before!=Convert.ToBase64String(RebirthLogisticsNativeInvocationJournal.Encode(actual.ecd)))
            throw new Exception("Refused body prepare changed actual native input.");
    }
    // Required future index cases are documented in
    // AGENT_A_BODY_ADMISSION_INDEX_PROPOSAL.md; no index authority exists yet.
}
