using System;
using System.Xml.Linq;

// Saves the matching publication only. Never debits inputs, republishes the
// native queue, refunds ingredients or awards completion/progression here.
public static class RebirthCamperStationPublicationService
{
    public static bool TryRecord(EntityPlayer player,RebirthStationGridAdmission admission,
        RebirthCamperStationSaveEvidence.Publication proof)
    {
        if(!ThreadManager.IsMainThread() || player==null || admission==null
            || !admission.TryGetMobileAddress(out var address)
            || !RebirthStationLiveAccess.TryResolveMobile(player,address,admission.CreationId,out var tile,out var owner)
            || !(tile is RebirthCamperStationState station)
            || !RebirthWorldCharacterService.TryGetIdentity(player,out var identity) || identity==null
            || !RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,admission)
            || !RebirthCamperStationPublicationRecord.TryCreate(admission,player.entityId,proof,out var receipt))return false;
        var progression=owner.Progression;
        bool staged=false;
        Func<bool> current=()=>RebirthStationLiveAccess.TryResolveMobile(player,address,admission.CreationId,out var live,out var liveOwner)
            && ReferenceEquals(live,station) && ReferenceEquals(liveOwner,owner) && ReferenceEquals(owner.Progression,progression)
            && RebirthWorldCharacterService.TryGetIdentity(player,out var liveIdentity) && liveIdentity!=null
            && liveIdentity.StorageKey==identity.StorageKey && liveIdentity.CanonicalId==identity.CanonicalId
            && progression.StationPreparations.TryGetValue(admission.JobId,out var retained) && retained!=null
            && XNode.DeepEquals(retained.Write(),admission.Write())
            && (progression.CamperStationPublications.TryGetValue(admission.JobId,out var liveReceipt)
                ? liveReceipt!=null&&XNode.DeepEquals(liveReceipt.Write(),receipt.Write()) : !staged)
            && !progression.StationPublications.ContainsKey(admission.JobId)
            && !progression.StationTerminalIntents.ContainsKey(admission.JobId)
            && !progression.StationCompletionPublications.ContainsKey(admission.JobId)
            && !progression.StationCancellationAttempts.ContainsKey(admission.JobId)
            && !progression.StationCancellationPublications.ContainsKey(admission.JobId)
            && RebirthCamperQueuedPublicationMatch.MatchesStation(station,address,admission,player.entityId);
        try
        {
            if(!current())return false;
            if(progression.CamperStationPublications.TryGetValue(admission.JobId,out var retainedReceipt))
            {
                if(retainedReceipt==null||!XNode.DeepEquals(retainedReceipt.Write(),receipt.Write()))return false;
            }
            else progression.CamperStationPublications.Add(admission.JobId,receipt.Clone());
            staged=true;
            // A failed flush retains the exact receipt for owner-save retry.
            // No native item mutation is repeated by this service.
            RebirthWorldCharacterService.MarkDirty(owner,"camper-station-publication");
            return current()&&RebirthWorldCharacterService.FlushPlayer(player,"camper-station-publication")&&current()
                &&RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,admission,receipt)&&current();
        }
        catch{return false;}
    }
}
