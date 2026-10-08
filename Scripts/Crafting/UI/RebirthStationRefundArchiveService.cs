using System;
using System.Xml.Linq;

// Authority-local terminal transition; does not apply inventory or infer completion from queue absence.
internal static class RebirthStationRefundArchiveService
{
    internal static bool TryArchive(EntityPlayerLocal player,Vector3i position,string creation,Guid job,Guid deliveryId)
    {
        if(job==Guid.Empty||deliveryId==Guid.Empty||!RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||!RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
        try
        {
            var world=player.world;int actor=player.entityId;string key=job.ToString("N");
            RebirthStationRefundArchive archive;
            if(!owner.Progression.StationRefundArchives.TryGetValue(key,out archive))
            {
                if(!RebirthStationRefundPreparationService.TryRecoverSavedDelivery(player,position,creation,job,deliveryId,out var delivered)||!delivered.IsSaved||
                    !owner.Progression.StationPreparations.TryGetValue(key,out var admission)||
                    !owner.Progression.StationPublications.TryGetValue(key,out var publication)||
                    !owner.Progression.StationTerminalIntents.TryGetValue(key,out var intent)||
                    !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var refund)||
                    !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var attempt)||
                    !owner.Progression.StationCancellationPublications.TryGetValue(key,out var cancelled)||
                    !RebirthStationRefundArchive.TryCreate(identity.StorageKey,admission,publication,intent,refund,attempt,cancelled,delivered,out archive))return false;
                var candidate=new System.Collections.Generic.Dictionary<string,RebirthStationRefundArchive>(owner.Progression.StationRefundArchives,StringComparer.Ordinal);
                candidate.Add(key,archive);RebirthStationRefundArchive.WriteAll(candidate,identity.StorageKey); // Capacity preflight before mutation.
                if(player.entityId!=actor||!ReferenceEquals(player.world,world)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                    !RebirthStationLiveAccess.TryResolve(player,position,creation,out var current,out var currentOwner)||
                    !ReferenceEquals(current,station)||!ReferenceEquals(currentOwner,owner)||
                    !RebirthWorldCharacterRepository.HasSavedStationRefundDelivery(identity,admission,intent,refund,attempt,cancelled,delivered))return false;
                if(!owner.Progression.StationPreparations.TryGetValue(key,out var currentAdmission)||
                    !owner.Progression.StationPublications.TryGetValue(key,out var currentPublication)||
                    !owner.Progression.StationTerminalIntents.TryGetValue(key,out var currentIntent)||
                    !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var currentRefund)||
                    !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var currentAttempt)||
                    !owner.Progression.StationCancellationPublications.TryGetValue(key,out var currentCancelled)||
                    !owner.Progression.StationRefundDeliveries.TryGetValue(key,out var currentDelivery)||
                    !RebirthStationRefundArchive.TryCreate(identity.StorageKey,currentAdmission,currentPublication,currentIntent,currentRefund,
                        currentAttempt,currentCancelled,currentDelivery,out var currentArchive)||!XNode.DeepEquals(currentArchive.Write(),archive.Write())||
                    !delivered.TryCopyPlan(refund,out var destination)||player.Buffs?.GetCustomVar("rbStationRefund_"+deliveryId.ToString("N"))!=1f||
                    !destination.MatchesAfter(player.bag?.ItemGrid?.items)||!RebirthStationRefundPlayerFileWitness.HasSaved(identity,deliveryId,destination))return false;
                owner.Progression.StationRefundArchives.Add(key,archive.Clone());
                owner.Progression.StationPreparations.Remove(key);owner.Progression.StationPublications.Remove(key);
                owner.Progression.StationTerminalIntents.Remove(key);owner.Progression.StationCancellationRefunds.Remove(key);
                owner.Progression.StationCancellationAttempts.Remove(key);owner.Progression.StationCancellationPublications.Remove(key);
                owner.Progression.StationRefundDeliveries.Remove(key);
            }
            if(archive==null||!RebirthStationRefundArchive.TryRead(archive.Write(),identity.StorageKey,out var valid)||
                (string)valid.Write().Element("stationRefundDelivery").Attribute("delivery")!=deliveryId.ToString("N")||
                !RebirthStationGridAdmission.TryReadStored(valid.Write().Element("stationAdmission"),out var original))return false;
            var image=original.Write();
            Func<bool> currentContext=()=>player.entityId==actor&&ReferenceEquals(player.world,world)&&ReferenceEquals(world?.GetPrimaryPlayer(),player)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)&&
                RebirthStationLiveAccess.TryResolve(player,position,creation,out var live,out var record)&&ReferenceEquals(live,station)&&ReferenceEquals(record,owner)&&
                (int)image.Attribute("x")==position.x&&(int)image.Attribute("y")==position.y&&(int)image.Attribute("z")==position.z&&
                (string)image.Attribute("block")==live.block?.GetBlockName()&&owner.Progression.StationRefundArchives.TryGetValue(key,out var retained)&&
                retained!=null&&XNode.DeepEquals(retained.Write(),valid.Write());
            RebirthWorldCharacterService.MarkDirty(owner,"station-refund-archive");
            if(!currentContext()||!RebirthWorldCharacterRepository.SaveIfDirty(identity,"station-refund-archive")||!currentContext()||
                !RebirthWorldCharacterRepository.HasSavedStationRefundArchive(identity,valid))return false;
            return RebirthStationPreparationReservation.TryReleaseSettled(world,owner,original,
                ()=>currentContext()&&RebirthWorldCharacterRepository.HasSavedStationRefundArchive(identity,valid));
        }
        catch{return false;} // Retain archive/claim on uncertainty, never reapply refund.
    }
}