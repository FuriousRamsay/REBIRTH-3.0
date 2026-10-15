using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Retained server operation. Remote server EntityPlayer.bag never plans credit.
internal sealed class RebirthCamperStationRemoteRefundDelivery
{
    private readonly RebirthCamperStationCancellationCheckpoint checkpoint;
    private readonly RebirthCamperStationCancellationRemoval removal;
    private readonly RebirthCamperStationCancellationRecord cancellation;
    private readonly RebirthStationRefundBackpackPlan plan;
    private readonly object lease;
    private RebirthCamperStationRefundDeliveryRecord record;
    private bool prepareSent,prepareDone,prepareAck,applySent,applyDone,applyAck,uncertain,confirmed;
    private RebirthCamperStationSaveEvidence.Request retirement;
    private RebirthCamperStationSaveEvidence.Publication retirementProof;
    private RebirthStationRefundArchive terminal;
    private RebirthCamperStationRemoteRefundDelivery(RebirthCamperStationCancellationCheckpoint cp,
        RebirthCamperStationCancellationRemoval removal,RebirthCamperStationCancellationRecord cancelled,
        RebirthStationRefundBackpackPlan plan,RebirthCamperStationRefundDeliveryRecord record,object lease)
    {checkpoint=cp;this.removal=removal;cancellation=cancelled.Clone();this.plan=plan;this.record=record.Clone();this.lease=lease;}
    internal static bool HasRoom(RebirthCamperStationCancellationCheckpoint checkpoint)
    {
        try
        {
            var b=checkpoint?.Binding;
            return b!=null&&!(b.Player is EntityPlayerLocal)&&b.IsOwnerCurrent()&&
                RebirthStationRefundPlayerFileWitness.TryCopySavedBag(b.Identity,out var bag)&&
                RebirthStationRefundBackpackPlan.TryCreate(bag.ItemGrid.items,bag.ItemGrid.items.Length,
                    slot=>bag.LockedSlots!=null&&slot<bag.LockedSlots.Length&&bag.LockedSlots[slot],checkpoint.CopyRefund(),out _)&&b.IsOwnerCurrent();
        }
        catch{return false;}
    }
    internal static bool TryBegin(RebirthCamperStationCancellationCheckpoint checkpoint,RebirthCamperStationCancellationRemoval removal,
        out RebirthCamperStationRemoteRefundDelivery operation)
    {
        operation=null;
        try
        {
            var b=checkpoint?.Binding;
            if(!ThreadManager.IsMainThread()||b==null||b.Player is EntityPlayerLocal||removal==null||
                !RebirthCamperRemoteOwnerApplication.IsRegistered||
                !RebirthCamperRemoteStationTransport.TryResolve(b.Player,b.Address,b.Admission.CreationId,out var station,out var lease)||
                !ReferenceEquals(station,b.Station)||!removal.TryGetCancellationRecord(out var cancelled)||
                !removal.TryPrepareRefund(out var plan)||b.Progression.CamperStationRefundDeliveries.ContainsKey(b.Admission.JobId)||
                b.Progression.CamperStationRefundDeliveries.Count>=64)return false;
            var delivery=Guid.NewGuid();
            if(!RebirthCamperStationRemoteRefundPayload.TryPrepareOperation(delivery,out var prepare)||
                b.Progression.CamperStationRefundDeliveries.Values.Any(v=>v==null||v.DeliveryId==delivery||v.DeliveryId==prepare||
                    RebirthCamperStationRemoteRefundPayload.TryPrepareOperation(v.DeliveryId,out var priorPrepare)&&(priorPrepare==delivery||priorPrepare==prepare))||
                b.Progression.StationRefundArchives.Values.Any(v=>v==null||v.DeliveryId==delivery||v.DeliveryId==prepare||
                    v.IsMobile&&!v.IsCompletion&&RebirthCamperStationRemoteRefundPayload.TryPrepareOperation(v.DeliveryId,out var priorPrepare)&&
                    (priorPrepare==delivery||priorPrepare==prepare))||
                !RebirthCamperStationRefundDeliveryRecord.TryCreate(delivery,b.Identity.StorageKey,b.Admission,b.Publication,
                    checkpoint.CopyIntent(),checkpoint.CopyRefund(),checkpoint.CopyAttempt(),cancelled,plan,out var record))return false;
            var candidate=new RebirthCamperStationRemoteRefundDelivery(checkpoint,removal,cancelled,plan,record,lease);
            if(!candidate.Current(false))return false;
            operation=candidate;b.Progression.CamperStationRefundDeliveries.Add(record.JobId,record.Clone());
            return candidate.Persist();
        }
        catch{return false;}
    }
    private bool Scope()
    {
        var b=checkpoint.Binding;var player=b.Player;
        return ThreadManager.IsMainThread()&&!(player is EntityPlayerLocal)&&player.world!=null&&!player.world.IsRemote()&&
            ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
            !player.IsDead()&&!RebirthCharacterCreationHoldService.IsHeld(player)&&
            RebirthWorldCharacterRepository.IsCurrentCachedRecord(b.Owner)&&
            RebirthWorldCharacterService.TryGetIdentity(player,out var identity)&&identity!=null&&
            identity.CanonicalId==b.Identity.CanonicalId&&identity.StorageKey==b.Identity.StorageKey&&
            RebirthCamperRemoteStationTransport.IsCurrent(player,b.Address,b.Admission.CreationId,lease)&&
            RebirthStationLiveAccess.TryResolveMobile(player,b.Address,b.Admission.CreationId,out var station,out var owner)&&
            ReferenceEquals(station,b.Station)&&ReferenceEquals(owner,b.Owner);
    }
    private bool Current(bool requireRecord=true)
    {
        try
        {
            var b=checkpoint.Binding;
            return Scope()&&checkpoint.MatchesStagedOwner()&&removal.MatchesRefundCustody(checkpoint,cancellation)&&
                (!requireRecord||b.Progression.CamperStationRefundDeliveries.TryGetValue(record.JobId,out var retained)&&
                    retained!=null&&XNode.DeepEquals(retained.Write(),record.Write()));
        }
        catch{return false;}
    }
    private bool HasSaved()
    {
        var b=checkpoint.Binding;
        return RebirthWorldCharacterRepository.HasSavedCamperStationRefundDelivery(b.Identity,b.Admission,b.Publication,
            checkpoint.CopyIntent(),checkpoint.CopyRefund(),checkpoint.CopyAttempt(),cancellation,record);
    }
    private bool Persist()
    {
        if(!Current())return false;
        var b=checkpoint.Binding;RebirthWorldCharacterService.MarkDirty(b.Owner,"camper-remote-refund");
        return RebirthWorldCharacterService.FlushPlayer(b.Player,"camper-remote-refund")&&Current()&&HasSaved()&&Current();
    }
    internal bool TryApply()
    {
        try
        {
            var b=checkpoint.Binding;
            if(uncertain||confirmed||record.IsSaved||!Current())return false;
            if(!record.IsAttempted)
            {
                if(!HasSaved()&&!Persist())return false;
                if(!prepareSent)
                {
                    if(!RebirthStationRefundPlayerFileWitness.HasSavedMissingBefore(b.Identity,record.DeliveryId,plan)&&
                        !RebirthStationRefundPlayerFileWitness.HasSavedBefore(b.Identity,record.DeliveryId,plan)||
                        !RebirthCamperStationRemoteRefundPayload.TryPrepareOperation(record.DeliveryId,out var operation)||
                        !RebirthCamperStationRemoteRefundPayload.TryCreate(checkpoint,cancellation,record,true,out var payload))return false;
                    prepareSent=true;
                    if(!RebirthCamperRemoteOwnerApplication.SendOwnerApplication(b.Player,operation,RebirthCamperRemoteOwnerEffectKind.Refund,payload,
                        ack=>{if(prepareDone)return;prepareDone=true;prepareAck=ack&&Current();if(!prepareAck)uncertain=true;}))uncertain=true;
                }
                if(!prepareAck||!Current()||!RebirthStationRefundPlayerFileWitness.HasSavedBefore(b.Identity,record.DeliveryId,plan)||
                    !record.TryMarkAttempted(out var attempted))return false;
                record=attempted;b.Progression.CamperStationRefundDeliveries[record.JobId]=attempted.Clone();
            }
            if(!HasSaved()&&!Persist())return false;
            if(!applySent)
            {
                if(!Current()||!HasSaved()||!RebirthStationRefundPlayerFileWitness.HasSavedBefore(b.Identity,record.DeliveryId,plan)||
                    !RebirthCamperStationRemoteRefundPayload.TryCreate(checkpoint,cancellation,record,false,out var payload))return false;
                applySent=true;
                if(!RebirthCamperRemoteOwnerApplication.SendOwnerApplication(b.Player,record.DeliveryId,RebirthCamperRemoteOwnerEffectKind.Refund,payload,
                    ack=>{if(applyDone)return;applyDone=true;applyAck=ack&&Current();if(!applyAck)uncertain=true;}))uncertain=true;
            }
            return !uncertain&&applyAck&&Current()&&RebirthStationRefundPlayerFileWitness.HasSavedReceipt(b.Identity,record.DeliveryId);
        }
        catch{return false;}
    }
    internal bool TryConfirmSaved()
    {
        try
        {
            if(uncertain||!applyAck||!Current()||!RebirthStationRefundPlayerFileWitness.HasSavedReceipt(checkpoint.Binding.Identity,record.DeliveryId)||
                !RebirthCamperStationPartialCancellationRewards.TryTrain(checkpoint.Binding.Player,checkpoint.Binding.Admission,checkpoint.CopyIntent(),record.DeliveryId))return false;
            if(!record.IsSaved)
            {
                if(!record.TryMarkSaved(out var saved))return false;
                record=saved;checkpoint.Binding.Progression.CamperStationRefundDeliveries[record.JobId]=saved.Clone();
            }
            return confirmed=(HasSaved()||Persist())&&Current();
        }
        catch{return false;}
    }
    internal bool TryGetSaved(out RebirthCamperStationRefundDeliveryRecord saved)
    {saved=null;if(!TryConfirmSaved())return false;saved=record.Clone();return true;}
    internal bool IsConfirmedFor(RebirthCamperStationCancellationCheckpoint cp,RebirthCamperStationCancellationRemoval removed)
        =>ReferenceEquals(cp,checkpoint)&&ReferenceEquals(removed,removal)&&TryConfirmSaved();
    private bool TerminalCurrent()
    {
        if(!confirmed||!Scope()||!RebirthStationRefundPlayerFileWitness.HasSavedReceipt(checkpoint.Binding.Identity,record.DeliveryId)||
            checkpoint.Binding.Station.Queue==null||!RebirthStationCompletionReceipt.HasNoJobReceipt(checkpoint.Binding.Station.CraftCompleteList,record.JobId))return false;
        foreach(var row in checkpoint.Binding.Station.Queue)
            if(row==null||RebirthStationGridQueue.IsMarked(row.Recipe)&&(!RebirthStationGridQueue.TryGetJobId(row.Recipe,out var job)||job==record.JobId))return false;
        return true;
    }
    private bool MatchesActiveParents()
    {
        var b=checkpoint.Binding;var s=b.Progression;string job=record.JobId;
        return checkpoint.MatchesStagedOwner()&&
            s.StationPreparations.TryGetValue(job,out var a)&&a!=null&&XNode.DeepEquals(a.Write(),b.Admission.Write())&&
            s.CamperStationPublications.TryGetValue(job,out var q)&&q!=null&&XNode.DeepEquals(q.Write(),b.Publication.Write())&&
            s.StationTerminalIntents.TryGetValue(job,out var i)&&i!=null&&XNode.DeepEquals(i.Write(),checkpoint.CopyIntent().Write())&&
            s.StationCancellationRefunds.TryGetValue(job,out var r)&&r!=null&&XNode.DeepEquals(r.Write(),checkpoint.CopyRefund().Write())&&
            s.StationCancellationAttempts.TryGetValue(job,out var t)&&t!=null&&XNode.DeepEquals(t.Write(),checkpoint.CopyAttempt().Write())&&
            s.CamperStationCancellations.TryGetValue(job,out var c)&&c!=null&&XNode.DeepEquals(c.Write(),cancellation.Write())&&
            s.CamperStationRefundDeliveries.TryGetValue(job,out var d)&&d!=null&&XNode.DeepEquals(d.Write(),record.Write());
    }
    internal bool TryArchive()
    {
        try
        {
            var b=checkpoint.Binding;var state=b.Progression;
            if(!TerminalCurrent())return false;
            if(retirementProof==null)
            {
                if(retirement==null)retirement=RebirthCamperStationSaveEvidence.BeginRetirement(b.Station,b.Address,TerminalCurrent);
                if(!RebirthCamperStationSaveEvidence.TryObserve(retirement,out var observed))return false;
                retirementProof=observed;
            }
            if(!cancellation.MatchesCurrentCancellation(retirementProof,b.Publication)||!TerminalCurrent())return false;
            if(terminal==null)
            {
                if(!MatchesActiveParents()||!HasSaved()||
                    !RebirthWorldCharacterRepository.HasSavedCamperStationCancellation(b.Identity,b.Admission,b.Publication,
                        checkpoint.CopyIntent(),checkpoint.CopyRefund(),checkpoint.CopyAttempt(),cancellation)||
                    !RebirthStationRefundArchive.TryCreate(b.Identity.StorageKey,b.Admission,b.Publication,checkpoint.CopyIntent(),
                        checkpoint.CopyRefund(),checkpoint.CopyAttempt(),cancellation,record,out var archive))return false;
                var proposed=new Dictionary<string,RebirthStationRefundArchive>(state.StationRefundArchives,StringComparer.Ordinal);
                proposed.Add(record.JobId,archive);RebirthStationRefundArchive.WriteAll(proposed,b.Identity.StorageKey);
                if(!TerminalCurrent()||!MatchesActiveParents()||!HasSaved())return false;
                terminal=archive;state.StationRefundArchives.Add(record.JobId,archive.Clone());
                state.StationPreparations.Remove(record.JobId);state.CamperStationPublications.Remove(record.JobId);
                state.StationTerminalIntents.Remove(record.JobId);state.StationCancellationRefunds.Remove(record.JobId);
                state.StationCancellationAttempts.Remove(record.JobId);state.CamperStationCancellations.Remove(record.JobId);
                state.CamperStationRefundDeliveries.Remove(record.JobId);
            }
            Func<bool> current=()=>TerminalCurrent()&&state.StationRefundArchives.TryGetValue(record.JobId,out var retained)&&
                retained!=null&&XNode.DeepEquals(retained.Write(),terminal.Write());
            if(!current())return false;
            RebirthWorldCharacterService.MarkDirty(b.Owner,"camper-remote-refund-archive");
            if(!RebirthWorldCharacterService.FlushPlayer(b.Player,"camper-remote-refund-archive")||!current()||
                !RebirthWorldCharacterRepository.HasSavedStationRefundArchive(b.Identity,terminal))return false;
            Func<bool> acknowledged=()=>current()&&RebirthWorldCharacterRepository.HasSavedStationRefundArchive(b.Identity,terminal);
            return RebirthStationPreparationReservation.TryReleaseSettled(b.World,b.Owner,b.Admission,acknowledged)&&
                RebirthCamperStationQueuedAuthority.TryReleaseArchived(b.Station,b.Owner,b.Identity,terminal,acknowledged);
        }
        catch{return false;}
    }
}
