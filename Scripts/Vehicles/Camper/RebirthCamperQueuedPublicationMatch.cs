using System;
using System.IO;

// Correlates an opaque native-save observation with an existing paid job.
// Matching is not an owner-save commit, replay/refund permission or XP award.
public static class RebirthCamperQueuedPublicationMatch
{
    public static bool Matches(RebirthCamperStationSaveEvidence.Publication proof,
        RebirthStationGridAdmission admission,int queuedOwner)
        =>MatchesProof(proof,admission,queuedOwner,true);
    // Recovery callers must separately prove the original owner publication.
    // Later input edits do not change the ingredients already paid by this job.
    internal static bool MatchesRestored(RebirthCamperStationSaveEvidence.Publication proof,
        RebirthStationGridAdmission admission,int queuedOwner)
        =>MatchesProof(proof,admission,queuedOwner,false);
    private static bool MatchesProof(RebirthCamperStationSaveEvidence.Publication proof,
        RebirthStationGridAdmission admission,int queuedOwner,bool originalInput)
    {
        if(!ThreadManager.IsMainThread() || proof==null || admission==null || queuedOwner<=0
            || !admission.IsPublicationAttempted || !admission.TryGetMobileAddress(out var address)
            || !address.Equals(proof.Address))return false;
        try
        {
            var currentRoot=Path.GetFullPath(GameIO.GetSaveGameDir());
            var proofRoot=Path.GetFullPath(proof.SaveDirectory);
            var comparison=Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
            if(!string.Equals(currentRoot,proofRoot,comparison))return false;
            var saved=proof.RestoreStationSnapshot();
            return MatchesStationCore(saved,address,admission,queuedOwner,originalInput);
        }
        catch{return false;}
    }
    // Content correlation only. Caller must separately authenticate the live
    // vehicle/window lease; a matching detached station does not grant access.
    public static bool MatchesStation(RebirthCamperStationState saved,RebirthCamperStationAddress address,
        RebirthStationGridAdmission admission,int queuedOwner)
        =>MatchesStationCore(saved,address,admission,queuedOwner,true);
    internal static bool MatchesRestoredStation(RebirthCamperStationState saved,RebirthCamperStationAddress address,
        RebirthStationGridAdmission admission,int queuedOwner)
        =>MatchesStationCore(saved,address,admission,queuedOwner,false);
    private static bool MatchesStationCore(RebirthCamperStationState saved,RebirthCamperStationAddress address,
        RebirthStationGridAdmission admission,int queuedOwner,bool originalInput)
    {
        if(!ThreadManager.IsMainThread() || saved==null || admission==null || queuedOwner<=0
            || !admission.IsPublicationAttempted || !admission.TryGetMobileAddress(out var retained)
            || !retained.Equals(address))return false;
        try
        {
            if(saved.InstallationId!=address.Installation || saved.Queue==null || saved.Queue.Length<1
                || saved.Queue.Length>byte.MaxValue || saved.MaterialNames==null
                || !RebirthStationInputLayout.TryReadPhysical(saved.Input,saved.InputSlotCount,saved.MaterialNames.Length,out var input))return false;
            if(admission.IsMaterialPayment)
            {
                if(!RebirthStationMaterialInputCodec.TryWrite(saved.Input,saved.InputSlotCount,out var counterImage)||
                    !RebirthStationMaterialInputCodec.TryRead(counterImage,out input,out _))return false;
            }
            if(admission.IsPlayerPayment)
            {
                if(!admission.IsPlayerPaymentConfirmed||!admission.TryGetPlayerPayment(out var payment)||
                    (originalInput&&!payment.MatchesStation(saved.Input))||
                    !RebirthCamperStationPlayerPayment.HasHistoricalSavedReceipt(queuedOwner,admission))return false;
                // Player images never stand in for a saved vehicle input grid.
                input=payment.StationInputBefore();
            }
            RecipeQueueItem match=null;
            foreach(var entry in saved.Queue)
            {
                if(entry==null || entry.Multiplier<0)return false;
                if(entry.Recipe==null || !RebirthStationGridQueue.IsMarked(entry.Recipe))continue;
                if(!RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var job))return false;
                if(job!=admission.JobId)continue;
                if(match!=null || entry.StartingEntityId!=queuedOwner
                    || entry.RepairItem!=null || entry.AmountToRepair!=0
                    || float.IsNaN(entry.CraftingTimeLeft) || float.IsInfinity(entry.CraftingTimeLeft)
                    || float.IsNaN(entry.OneItemCraftTime) || float.IsInfinity(entry.OneItemCraftTime)
                    || !admission.MatchesQueuedQuality(entry.Recipe,entry.Multiplier,entry.Quality))return false;
                if(originalInput&&!admission.MatchesInitialQueuedQuality(entry.Recipe,entry.Multiplier,entry.Quality))return false;
                match=entry;
            }
            return match!=null && (originalInput
                ?admission.MatchesMobileQueuedObservation(admission.CreationId,address,
                    saved.InstalledBlock.Block.GetBlockName(),input,match.Recipe,match.Multiplier)
                :(string)admission.WritePaymentImage().Attribute("block")==saved.InstalledBlock.Block.GetBlockName()
                    &&admission.MatchesQueuedRecipe(match.Recipe,match.Multiplier));
        }
        catch{return false;}
    }
}
