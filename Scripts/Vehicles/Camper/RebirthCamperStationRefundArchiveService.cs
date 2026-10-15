using System;
using System.Collections.Generic;
using System.Xml.Linq;

// Retire already-applied mobile refunds into the existing completed history.
// This route never applies inventory and never uses queue absence as payment.
internal static class RebirthCamperStationRefundArchiveService
{
    internal static bool TryArchive(EntityPlayer player,string job,RebirthCamperStationRefundRecovery recovery=null)
    {
        if(!ThreadManager.IsMainThread()||player==null||string.IsNullOrEmpty(job)||
            !RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null)return false;
        RebirthCamperStationState station;RebirthCamperStationAddress address;object lease;
        if(player is EntityPlayerLocal)
        {
            if(!RebirthCamperStationWindowSession.TryGetCurrent(player,out station,out address)||
                !RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out lease))return false;
        }
        else if(!RebirthCamperRemoteStationTransport.TryGetCurrent(player,out station,out address,out _,out lease))return false;
        try
        {
            var world=player.world;int actor=player.entityId;var state=owner.Progression;
            RebirthStationRefundArchive archive;
            if(!state.StationRefundArchives.TryGetValue(job,out archive))
            {
                {
                    if(recovery==null||!recovery.TryConfirmSaved()||!recovery.TryGetSaved(out var delivered)||
                        !state.StationPreparations.TryGetValue(job,out var admission)||
                        !state.CamperStationPublications.TryGetValue(job,out var queued)||
                        !state.StationTerminalIntents.TryGetValue(job,out var intent)||
                        !state.StationCancellationRefunds.TryGetValue(job,out var refund)||
                        !state.StationCancellationAttempts.TryGetValue(job,out var attempt)||
                        !state.CamperStationCancellations.TryGetValue(job,out var cancelled)||
                        !RebirthStationRefundArchive.TryCreate(identity.StorageKey,admission,queued,intent,refund,attempt,cancelled,delivered,out archive))return false;
                    var proposed=new Dictionary<string,RebirthStationRefundArchive>(state.StationRefundArchives,StringComparer.Ordinal);
                    proposed.Add(job,archive);RebirthStationRefundArchive.WriteAll(proposed,identity.StorageKey);
                    if(!recovery.TryGetSaved(out var current)||!XNode.DeepEquals(current.Write(),delivered.Write()))return false;
                    state.StationRefundArchives.Add(job,archive.Clone());
                    state.StationPreparations.Remove(job);state.CamperStationPublications.Remove(job);
                    state.StationTerminalIntents.Remove(job);state.StationCancellationRefunds.Remove(job);
                    state.StationCancellationAttempts.Remove(job);state.CamperStationCancellations.Remove(job);
                    state.CamperStationRefundDeliveries.Remove(job);
                    RebirthWorldCharacterService.MarkDirty(owner,"camper-refund-archive");
                }
            }
            if(archive==null||!archive.IsMobile||archive.IsCompletion||!RebirthStationRefundArchive.TryRead(archive.Write(),identity.StorageKey,out var valid)||
                !RebirthStationGridAdmission.TryReadStored(valid.Write().Element("stationAdmission"),out var original)||
                !original.TryGetMobileAddress(out var expected)||!address.Equals(expected))return false;
            Func<bool> currentContext=()=>ThreadManager.IsMainThread()&&player.entityId==actor&&ReferenceEquals(player.world,world)&&
                (player is EntityPlayerLocal?ReferenceEquals(world?.GetPrimaryPlayer(),player):
                    world!=null&&!world.IsRemote()&&ReferenceEquals(world.GetEntity(actor),player))&&
                !player.IsDead()&&!RebirthCharacterCreationHoldService.IsHeld(player)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)&&
                RebirthStationLiveAccess.TryGetMobileLease(player,address,original.CreationId,out var leaseStation,out var currentLease)&&
                ReferenceEquals(leaseStation,station)&&ReferenceEquals(lease,currentLease)&&
                RebirthStationLiveAccess.TryResolveMobile(player,address,original.CreationId,out var live,out var liveOwner)&&
                ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&
                state.StationRefundArchives.TryGetValue(job,out var retained)&&retained!=null&&XNode.DeepEquals(retained.Write(),valid.Write())&&
                RebirthStationRefundPlayerFileWitness.HasSavedReceipt(identity,valid.DeliveryId);
            if(!currentContext())return false;
            RebirthWorldCharacterService.MarkDirty(owner,"camper-refund-archive");
            if(!RebirthWorldCharacterService.FlushPlayer(player,"camper-refund-archive")||!currentContext()||
                !RebirthWorldCharacterRepository.HasSavedStationRefundArchive(identity,valid))return false;
            Func<bool> savedTerminal=()=>currentContext()&&RebirthWorldCharacterRepository.HasSavedStationRefundArchive(identity,valid);
            return RebirthStationPreparationReservation.TryReleaseSettled(world,owner,original,savedTerminal)&&
                RebirthCamperStationQueuedAuthority.TryReleaseArchived(station,owner,identity,valid,savedTerminal);
        }
        catch{return false;} // Failed saves retain terminal history; never replay credit.
    }
}
