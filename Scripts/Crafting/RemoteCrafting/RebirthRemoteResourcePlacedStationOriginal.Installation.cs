using System;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static readonly object protocolInstallGate=new object();
    private static bool reviewedSource4Producer;
    // This policy belongs only to the ordinary placed original. Mobile,
    // managed/grid admissions and other Survivor producers retain their own
    // protocols; it is not a global remote-resource mode switch.
    internal static bool AllowsReviewedPlacedMode=>!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
        protocolActive&&reviewedSource4Producer&&RebirthProductiveEarnedUiQueueOwner.AcceptsSavedSource4;

    // Successful flags are set only after the owning installers return. Each
    // Harmony installer uses PatchClassOnce, so a failed startup step can retry
    // without multiplying already installed native observers or update handlers.
    internal static bool ProtocolInfrastructureInstalled
    {
        get
        {
            lock(protocolInstallGate)return protocolInfrastructure&&uploadReaderInstalled&&
                cancellationInstalled&&cancellationLoadInstalled&&cancellationNativeLifecycleQualified&&CancellationHostCustodyHooksInstalled&&
                RebirthCookingOutputDelivery.IsInstalled&&
                RebirthRemoteResourcePlacedStationGuards.IsInstalled&&
                RebirthRemoteResourcePlacedStationClientCustody.IsInstalled&&
                RebirthRemoteResourcePlacedStationEligibility.IsInstalled;
        }
    }

    // Reviewed production startup calls this only after all native owner/source
    // dependencies and fetched station installation have succeeded.
    // Enabling after a world exists could miss RegionFileManager construction
    // and initial owner hydration; those events cannot be synthesized later.
    internal static void EnableAfterReviewedSource()
    {
        lock(protocolInstallGate)
        {
            if(protocolActive)return;
            if(!ProtocolInfrastructureInstalled)
                throw new InvalidOperationException("Placed protocol infrastructure is incomplete.");
            if(GameManager.Instance?.World!=null)
                throw new InvalidOperationException("Placed protocol must be enabled before native world/account lifecycle begins.");
            if(!RebirthProductiveEarnedUiQueueOwner.AcceptsSavedSource4)
                throw new InvalidOperationException("Placed saved queue-owner identity observers are incomplete.");
            reviewedSource4Producer=true;
            protocolActive=true;
        }
    }
}
