using System;
using System.Linq;
using System.Xml.Linq;

// Replace only a proved owner admission/publication pair. This does not move
// inventory, repay ingredients, register a queue or release the source claim.
internal static class RebirthCamperStationRelocationService
{
    // Retry an exact retained destination pair after reconnect. The fresh native
    // proof corroborates current contents; it never replaces the staged receipt.
    internal static bool TryFlushRecorded(EntityPlayer player,RebirthStationGridAdmission moved,
        RebirthCamperStationPublicationRecord receipt,RebirthCamperStationSaveEvidence.Publication fresh)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||moved==null||receipt==null||fresh==null||
                (string)moved.Write().Attribute("version")!="7"||!moved.TryGetMobileAddress(out var address)||
                !RebirthCamperStationPublicationRecord.TryRead(receipt.Write(),moved,out _)||
                !receipt.MatchesSaveScope(GameIO.GetSaveGameDir())||
                !RebirthStationLiveAccess.TryResolveMobile(player,address,moved.CreationId,out var tile,out var owner)||
                !(tile is RebirthCamperStationState station)||
                !RebirthStationLiveAccess.TryGetMobileLease(player,address,moved.CreationId,out var original,out var lease)||
                !ReferenceEquals(original,station)||lease==null||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null)return false;
            var wrapper=moved.Write();
            if(!RebirthStationGridAdmission.TryReadStored(wrapper.Element("parentAdmission")?.Elements().SingleOrDefault(),out var parent)||
                !RebirthCamperStationPublicationRecord.TryRead(wrapper.Element("parentPublication")?.Elements().SingleOrDefault(),parent,out var parentReceipt))return false;
            var progression=owner.Progression;int queuedOwner=(int)receipt.Write().Attribute("queuedOwner");
            bool Current()=>progression!=null&&ReferenceEquals(owner.Progression,progression)&&
                RebirthStationLiveAccess.TryResolveMobile(player,address,moved.CreationId,out var live,out var liveOwner)&&
                ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&
                RebirthStationLiveAccess.TryGetMobileLease(player,address,moved.CreationId,out var current,out var currentLease)&&
                ReferenceEquals(current,station)&&ReferenceEquals(currentLease,lease)&&
                RebirthWorldCharacterService.TryGetIdentity(player,out var liveIdentity)&&liveIdentity!=null&&
                liveIdentity.StorageKey==identity.StorageKey&&liveIdentity.CanonicalId==identity.CanonicalId&&
                progression.StationPreparations.TryGetValue(moved.JobId,out var a)&&a!=null&&XNode.DeepEquals(a.Write(),moved.Write())&&
                progression.CamperStationPublications.TryGetValue(moved.JobId,out var p)&&p!=null&&XNode.DeepEquals(p.Write(),receipt.Write())&&
                !progression.StationPublications.ContainsKey(moved.JobId)&&!progression.StationTerminalIntents.ContainsKey(moved.JobId)&&
                !progression.StationCompletionPublications.ContainsKey(moved.JobId)&&!progression.StationCancellationAttempts.ContainsKey(moved.JobId)&&
                !progression.StationCancellationPublications.ContainsKey(moved.JobId)&&!progression.CamperStationCompletions.ContainsKey(moved.JobId)&&
                !progression.CamperStationTrainingPlans.ContainsKey(moved.JobId)&&!progression.CamperStationCancellations.ContainsKey(moved.JobId)&&
                !progression.CamperStationRefundDeliveries.ContainsKey(moved.JobId)&&!progression.StationCancellationRefunds.ContainsKey(moved.JobId)&&
                !progression.StationRefundDeliveries.ContainsKey(moved.JobId)&&!progression.StationRefundArchives.ContainsKey(moved.JobId)&&
                RebirthCamperQueuedPublicationMatch.MatchesRestored(fresh,moved,queuedOwner)&&
                RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(station,address,moved,queuedOwner);
            if(!Current()||(!RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,parent,parentReceipt)&&
                !RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,moved,receipt)))return false;
            RebirthWorldCharacterService.MarkDirty(owner,"camper-station-relocation-retry");
            return Current()&&RebirthWorldCharacterService.FlushPlayer(player,"camper-station-relocation-retry")&&Current()&&
                RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,moved,receipt)&&Current();
        }
        catch{return false;}
    }
    internal static bool TryRecord(EntityPlayer player, RebirthCamperStationRelocationCorrelation correlation)
    {
        if(!ThreadManager.IsMainThread()||player==null||correlation==null
            ||!correlation.OriginalAdmission.TryRelocate(correlation,out var moved)
            ||!RebirthCamperStationPublicationRecord.TryCreateRelocated(correlation,moved,out var receipt)
            ||!RebirthStationLiveAccess.TryResolveMobile(player,correlation.Destination,moved.CreationId,out var tile,out var owner)
            ||!(tile is RebirthCamperStationState station)
            ||!RebirthStationLiveAccess.TryGetMobileLease(player,correlation.Destination,moved.CreationId,out var leased,out var lease)
            ||lease==null||!ReferenceEquals(leased,station)
            ||!RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null)return false;
        var progression=owner.Progression;
        if(progression==null)return false;
        bool Pair(RebirthStationGridAdmission admission,RebirthCamperStationPublicationRecord publication)
            =>progression.StationPreparations.TryGetValue(moved.JobId,out var prepared)&&prepared!=null
                &&progression.CamperStationPublications.TryGetValue(moved.JobId,out var published)&&published!=null
                &&XNode.DeepEquals(prepared.Write(),admission.Write())&&XNode.DeepEquals(published.Write(),publication.Write());
        bool Current()
            =>RebirthStationLiveAccess.TryResolveMobile(player,correlation.Destination,moved.CreationId,out var live,out var liveOwner)
                &&ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&ReferenceEquals(owner.Progression,progression)
                &&RebirthStationLiveAccess.TryGetMobileLease(player,correlation.Destination,moved.CreationId,out var currentStation,out var currentLease)
                &&ReferenceEquals(currentStation,station)&&ReferenceEquals(currentLease,lease)
                &&RebirthWorldCharacterService.TryGetIdentity(player,out var currentIdentity)&&currentIdentity!=null
                &&currentIdentity.StorageKey==identity.StorageKey&&currentIdentity.CanonicalId==identity.CanonicalId
                &&!progression.StationPublications.ContainsKey(moved.JobId)
                &&!progression.StationTerminalIntents.ContainsKey(moved.JobId)
                &&!progression.StationCompletionPublications.ContainsKey(moved.JobId)
                &&!progression.StationCancellationAttempts.ContainsKey(moved.JobId)
                &&!progression.StationCancellationPublications.ContainsKey(moved.JobId)
                &&!progression.CamperStationCompletions.ContainsKey(moved.JobId)
                &&!progression.CamperStationTrainingPlans.ContainsKey(moved.JobId)
                &&!progression.CamperStationCancellations.ContainsKey(moved.JobId)
                &&!progression.CamperStationRefundDeliveries.ContainsKey(moved.JobId)
                &&!progression.StationCancellationRefunds.ContainsKey(moved.JobId)
                &&!progression.StationRefundDeliveries.ContainsKey(moved.JobId)
                &&!progression.StationRefundArchives.ContainsKey(moved.JobId)
                &&RebirthCamperQueuedPublicationMatch.MatchesRestoredStation(station,correlation.Destination,moved,
                    (int)receipt.Write().Attribute("queuedOwner"));
        try
        {
            if(!Current()||(!Pair(correlation.OriginalAdmission,correlation.OriginalPublication)&&!Pair(moved,receipt)))return false;
            bool savedParent=RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,
                correlation.OriginalAdmission,correlation.OriginalPublication);
            bool savedDestination=RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,moved,receipt);
            if(!savedParent&&!savedDestination)return false;
            if(Pair(correlation.OriginalAdmission,correlation.OriginalPublication))
            {
                // No external callback occurs between these two dictionary writes.
                progression.StationPreparations[moved.JobId]=moved.Clone();
                progression.CamperStationPublications[moved.JobId]=receipt.Clone();
            }
            RebirthWorldCharacterService.MarkDirty(owner,"camper-station-relocation");
            return Current()&&Pair(moved,receipt)
                &&RebirthWorldCharacterService.FlushPlayer(player,"camper-station-relocation")
                &&Current()&&Pair(moved,receipt)
                &&RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,moved,receipt)&&Current();
        }
        catch{return false;}
    }
}
