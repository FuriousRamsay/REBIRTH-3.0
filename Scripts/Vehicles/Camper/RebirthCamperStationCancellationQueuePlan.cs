using System.Collections.Generic;

// Detached removal planning only. A requester's temporary actor may differ
// from the immutable native recipe actor after reload. Neither this plan nor
// its synthetic comparison choice grants removal, refund or save authority.
internal static class RebirthCamperStationCancellationQueuePlan
{
    internal static bool TryBuild(IList<RecipeQueueItem> queue,RebirthStationGridAdmission admission,
        RebirthStationTerminalIntent requesterChoice,int queuedOwner,out RecipeQueueItem[] replacement)
    {
        replacement=null;
        if(!ThreadManager.IsMainThread()||admission==null||queuedOwner<=0||
            !admission.TryGetMobileAddress(out _)||requesterChoice==null||requesterChoice.IsCompletion||
            !RebirthStationTerminalIntent.TryRead(requesterChoice.Write(),admission,out var verified)||verified.IsCompletion||
            !RebirthStationTerminalIntent.TryCreate(admission,false,queuedOwner,verified.RemainingBatches,out var comparison))return false;
        // Reuse the exact paid-recipe/quality/countdown/unique-job checks. The
        // comparison record is never stored as the real requester's choice.
        return RebirthStationNativeQueueEntry.TryBuildCancellation(queue,admission,comparison,queuedOwner,out replacement);
    }
}
