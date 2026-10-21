using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Read-only qualification of existing Survivor originals. No queue insertion,
// ingredient/refund setters, native save request or reward may be called here.
internal static class RebirthCamperRemoteStationSurvivorStatus
{
    private static string Digest(XElement node)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)))).Replace("-","").ToLowerInvariant();}
    private static bool Admission(RebirthStationGridAdmission source,RebirthCamperStationAddress address,string creation,RebirthCamperRemoteStationCommand command,RebirthStablePlayerIdentity identity,out RebirthStationGridAdmission admission)
    {
        admission=null;
        if(source==null||!RebirthStationGridAdmission.TryReadStored(source.Write(),out var original)||original.JobId!=command.Job.ToString("N")||original.CreationId!=creation||
            !original.TryGetMobileAddress(out var target)||!target.Equals(address)||!original.IsPublicationAttempted)return false;
        if(command.Kind!=RebirthCamperRemoteStationCommandKind.Cancel)
        {
            if(command.Operation!=command.Job||original.IsPlayerPayment!=(command.Kind==RebirthCamperRemoteStationCommandKind.CraftPlayer)||original.OriginalBatches!=command.Batches||
                !original.TryMaterialize(XUiM_Recipes.GetRecipes(),out var queued,out _,out _)||queued.GetName()!=command.RecipeName||!original.MatchesInitialQueuedQuality(queued,command.Batches,command.Tier))return false;
        }
        else if(command.RecipeName.Length!=0||command.Batches!=0||command.Tier!=0)return false;
        if(original.IsPlayerPayment&&(!original.TryGetPlayerPayment(out var payment)||payment.OwnerKey!=identity.StorageKey||payment.OwnerId!=identity.CanonicalId))return false;
        admission=original;return true;
    }
    internal static RebirthCamperRemoteStationCommandStatus.State Read(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,out string proof)
    {
        proof=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor==null||actor.IsDead()||actor.world?.IsRemote()!=false||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
                command==null||(command.Kind!=RebirthCamperRemoteStationCommandKind.Craft&&command.Kind!=RebirthCamperRemoteStationCommandKind.CraftPlayer&&command.Kind!=RebirthCamperRemoteStationCommandKind.Cancel)||
                !RebirthCamperRemoteStationTransport.IsCurrent(actor,address,creation,lease)||!RebirthWorldCharacterService.TryGetIdentity(actor,out var identity)||identity==null||
                !RebirthCamperStationNativeActorPayment.RemoteIdentity(actor,out var nativeOwner,out _)||nativeOwner==null||nativeOwner.StorageKey!=identity.StorageKey||nativeOwner.CanonicalId!=identity.CanonicalId||
                !RebirthWorldCharacterService.TryGet(actor,out var owner)||owner?.Progression==null||owner.Origin?.CreationId!=creation||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner))return RebirthCamperRemoteStationCommandStatus.State.Unknown;
            var world=actor.world;var state=owner.Progression;int deaths=actor.Died;
            bool Current()=>ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(actor.world,world)&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&actor.Died==deaths&&
                RebirthCamperRemoteStationTransport.IsCurrent(actor,address,creation,lease)&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)&&ReferenceEquals(owner.Progression,state)&&owner.Origin?.CreationId==creation;
            if(!Current())return RebirthCamperRemoteStationCommandStatus.State.Unknown;
            string job=command.Job.ToString("N");bool cancel=command.Kind==RebirthCamperRemoteStationCommandKind.Cancel;
            // A full saved terminal archive retains the original queued witness
            // after active preparations are retired. It never grants effects.
            if(state.StationRefundArchives.TryGetValue(job,out var historical))
            {
                if(historical==null||!RebirthStationRefundArchive.TryRead(historical.Write(),identity.StorageKey,out var archive)||!archive.IsMobile||cancel&&archive.IsCompletion||
                    !RebirthStationGridAdmission.TryReadStored(archive.Write().Element("stationAdmission"),out var savedAdmission)||!Admission(savedAdmission,address,creation,command,identity,out var original)||
                    !RebirthCamperStationPublicationRecord.TryRead(archive.Write().Element("camperStationPublication"),original,out var queued)||!queued.MatchesSaveScope(GameIO.GetSaveGameDir())||
                    !RebirthWorldCharacterRepository.HasSavedStationRefundArchive(identity,archive)||original.IsPlayerPayment&&(!original.IsPlayerPaymentConfirmed||!RebirthCamperStationPlayerPayment.HasSavedReceipt(actor,original))||
                    cancel&&!RebirthStationRefundPlayerFileWitness.HasSavedReceipt(identity,archive.DeliveryId)||!Current())return RebirthCamperRemoteStationCommandStatus.State.Unknown;
                proof="saved-survivor-archive:"+Digest(archive.Write());return RebirthCamperRemoteStationCommandStatus.State.Applied;
            }
            if(!state.StationPreparations.TryGetValue(job,out var offered)||!Admission(offered,address,creation,command,identity,out var admission)||
                !RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,admission)||!Current())return RebirthCamperRemoteStationCommandStatus.State.Unknown;
            if(!cancel)
            {
                proof="saved-survivor-admission:"+Digest(admission.Write());
                if(!state.CamperStationPublications.TryGetValue(job,out var candidate)||candidate==null)return RebirthCamperRemoteStationCommandStatus.State.Pending;
                if(!RebirthCamperStationPublicationRecord.TryRead(candidate.Write(),admission,out var publication)||!RebirthWorldCharacterRepository.HasSavedCamperStationPublication(identity,admission,publication)||
                    admission.IsPlayerPayment&&(!admission.IsPlayerPaymentConfirmed||!RebirthCamperStationPlayerPayment.HasSavedReceipt(actor,admission))||!Current())return RebirthCamperRemoteStationCommandStatus.State.Unknown;
                proof="saved-survivor-queued:"+Digest(publication.Write());return RebirthCamperRemoteStationCommandStatus.State.Applied;
            }
            if(!state.CamperStationPublications.TryGetValue(job,out var q)||q==null||!state.StationTerminalIntents.TryGetValue(job,out var intent)||intent==null||intent.IsCompletion||
                !state.StationCancellationRefunds.TryGetValue(job,out var refund)||refund==null||!state.StationCancellationAttempts.TryGetValue(job,out var attempt)||attempt==null||
                !state.CamperStationCancellations.TryGetValue(job,out var cancelled)||cancelled==null||!state.CamperStationRefundDeliveries.TryGetValue(job,out var delivery)||delivery==null||
                !RebirthCamperStationRefundDeliveryRecord.TryRead(delivery.Write(),identity.StorageKey,admission,q,intent,refund,attempt,cancelled,out var valid)||
                !RebirthWorldCharacterRepository.HasSavedCamperStationRefundDelivery(identity,admission,q,intent,refund,attempt,cancelled,valid)||!Current())return RebirthCamperRemoteStationCommandStatus.State.Unknown;
            proof="saved-survivor-cancel:"+Digest(valid.Write());
            bool ownerSaved=valid.IsSaved&&RebirthStationRefundPlayerFileWitness.HasSavedReceipt(identity,valid.DeliveryId);
            if(!Current()){proof=null;return RebirthCamperRemoteStationCommandStatus.State.Unknown;}
            return ownerSaved?RebirthCamperRemoteStationCommandStatus.State.Applied:RebirthCamperRemoteStationCommandStatus.State.Pending;
        }
        catch{proof=null;return RebirthCamperRemoteStationCommandStatus.State.Unknown;}
    }
}
