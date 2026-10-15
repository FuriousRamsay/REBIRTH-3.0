using System;
using System.Linq;
using System.Xml.Linq;

// Cold application requires explicit primary/native before-image evidence and
// durable attempted custody. Applied receipts are observation-only.
internal sealed class RebirthCamperStationRefundRecovery:IDisposable
{
    private readonly EntityPlayer player;
    private EntityPlayerLocal Local=>player as EntityPlayerLocal;
    private readonly RebirthWorldCharacterRecord owner;
    private readonly RebirthStablePlayerIdentity identity;
    private readonly RebirthCamperStationState station;
    private readonly RebirthCamperStationAddress address;
    private readonly object lease,world,bag;
    private readonly int actor;
    private readonly ItemStack installed;
    private readonly RebirthStationGridAdmission admission;
    private readonly RebirthCamperStationPublicationRecord queued;
    private readonly RebirthStationTerminalIntent intent;
    private readonly RebirthStationCancellationRefund refund;
    private readonly RebirthStationCancellationAttempt attempt;
    private readonly RebirthCamperStationCancellationRecord cancellation;
    private readonly RebirthStationRefundBackpackPlan plan;
    private RebirthCamperStationRefundDeliveryRecord record;
    private RebirthCamperStationRefundDeliveryRecord primaryBefore;
    private RebirthStationRefundPreparationService.ApplicationPermit permit;
    private RebirthCamperStationSaveEvidence.Request request;
    private RebirthCamperStationSaveEvidence.Publication proof;
    private bool closed,savedPending,attemptPending,applicationIssued,confirmed,preparedBefore;
    private bool remotePrepareSent,remotePrepareDone,remotePrepareAck,remoteApplySent,remoteApplyDone,remoteApplyAck,remoteUncertain;
    private RebirthCamperStationRefundRecovery(EntityPlayer player,RebirthWorldCharacterRecord owner,
        RebirthStablePlayerIdentity identity,RebirthCamperStationState station,RebirthCamperStationAddress address,
        object lease,ItemStack installed,RebirthStationGridAdmission admission,RebirthCamperStationPublicationRecord queued,
        RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,
        RebirthCamperStationCancellationRecord cancellation,RebirthCamperStationRefundDeliveryRecord record,RebirthStationRefundBackpackPlan plan)
    {
        this.player=player;this.owner=owner;this.identity=identity;this.station=station;this.address=address;this.lease=lease;
        world=player.world;bag=(player as EntityPlayerLocal)?.bag;actor=player.entityId;this.installed=installed.Clone();
        this.admission=admission.Clone();this.queued=queued.Clone();this.intent=intent.Clone();this.refund=refund.Clone();
        this.attempt=attempt.Clone();this.cancellation=cancellation.Clone();this.record=record.Clone();primaryBefore=record.Clone();this.plan=plan;
    }
    internal static bool TryBegin(EntityPlayer player,string job,out RebirthCamperStationRefundRecovery recovery)
    {
        recovery=null;
        try
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
            var state=owner.Progression;
            if(!state.StationPreparations.TryGetValue(job,out var a)||a==null||
                !state.CamperStationPublications.TryGetValue(job,out var q)||q==null||
                !state.StationTerminalIntents.TryGetValue(job,out var i)||i==null||i.IsCompletion||
                !state.StationCancellationRefunds.TryGetValue(job,out var r)||r==null||
                !state.StationCancellationAttempts.TryGetValue(job,out var t)||t==null||
                !state.CamperStationCancellations.TryGetValue(job,out var c)||c==null||
                !state.CamperStationRefundDeliveries.TryGetValue(job,out var d)||d==null||
                !RebirthCamperStationRefundDeliveryRecord.TryRead(d.Write(),identity.StorageKey,a,q,i,r,t,c,out var valid)||
                !valid.TryCopyPlan(r,out var plan)||!a.TryGetMobileAddress(out var expected)||!address.Equals(expected))return false;
            var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(address.Installation)?.InstalledItem;
            if(installed==null)return false;
            var candidate=new RebirthCamperStationRefundRecovery(player,owner,identity,station,address,lease,installed,a,q,i,r,t,c,valid,plan);
            if(!candidate.Current())return false;
            recovery=candidate; // Retain before native observation requests can acquire custody.
            candidate.request=RebirthCamperStationSaveEvidence.BeginRecovery(player,station,address,candidate.Current);
            return true;
        }
        catch{return false;}
    }
    private bool HasSaved(RebirthCamperStationRefundDeliveryRecord delivery)
        =>RebirthWorldCharacterRepository.HasSavedCamperStationRefundDelivery(identity,admission,queued,intent,refund,attempt,cancellation,delivery);
    private bool Current()
    {
        try
        {
            if(closed||!ThreadManager.IsMainThread()||player.entityId!=actor||!ReferenceEquals(player.world,world)||
                (Local!=null? !ReferenceEquals(Local.bag,bag)||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player):
                    player.world==null||player.world.IsRemote()||!ReferenceEquals(player.world.GetEntity(actor),player))||player.IsDead()||
                RebirthCharacterCreationHoldService.IsHeld(player)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                !RebirthCamperVehicleIdentity.IsUniqueCurrent(station.BoundVehicle,address.Vehicle)||
                !RebirthStationLiveAccess.TryGetMobileLease(player,address,admission.CreationId,out var leaseStation,out var liveLease)||
                !ReferenceEquals(leaseStation,station)||!ReferenceEquals(liveLease,lease)||
                !RebirthStationLiveAccess.TryResolveMobile(player,address,admission.CreationId,out var live,out var liveOwner)||
                !ReferenceEquals(live,station)||!ReferenceEquals(liveOwner,owner)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var liveIdentity)||liveIdentity==null||
                liveIdentity.StorageKey!=identity.StorageKey||liveIdentity.CanonicalId!=identity.CanonicalId||
                !RebirthStationGridIngredients.IsSameStackSnapshot(installed,
                    RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(address.Installation)?.InstalledItem)||
                GameManager.Instance==null||!ReferenceEquals(GameManager.Instance.World,world)||
                Local!=null&&GameManager.Instance.getPersistentPlayerID(null)?.CombinedString!=identity.CanonicalId)return false;
            var state=owner.Progression;string job=admission.JobId;
            if(state==null||!state.StationPreparations.TryGetValue(job,out var a)||a==null||!XNode.DeepEquals(a.Write(),admission.Write())||
                !state.CamperStationPublications.TryGetValue(job,out var q)||q==null||!XNode.DeepEquals(q.Write(),queued.Write())||
                !state.StationTerminalIntents.TryGetValue(job,out var i)||i==null||!XNode.DeepEquals(i.Write(),intent.Write())||
                !state.StationCancellationRefunds.TryGetValue(job,out var r)||r==null||!XNode.DeepEquals(r.Write(),refund.Write())||
                !state.StationCancellationAttempts.TryGetValue(job,out var t)||t==null||!XNode.DeepEquals(t.Write(),attempt.Write())||
                !state.CamperStationCancellations.TryGetValue(job,out var c)||c==null||!XNode.DeepEquals(c.Write(),cancellation.Write())||
                !state.CamperStationRefundDeliveries.TryGetValue(job,out var d)||d==null||!XNode.DeepEquals(d.Write(),record.Write())||
                state.CamperStationRefundDeliveries.Values.Any(v=>v==null||v.JobId!=job&&v.DeliveryId==record.DeliveryId)||
                state.StationPublications.ContainsKey(job)||state.StationCancellationPublications.ContainsKey(job)||
                state.StationRefundDeliveries.ContainsKey(job)||state.CamperStationCompletions.ContainsKey(job)||
                state.CamperStationTrainingPlans.ContainsKey(job)||state.StationCompletionPublications.ContainsKey(job)||
                state.StationCompletionExpectationProjections.ContainsKey(job)||station.Queue==null||
                !RebirthWorldCharacterRepository.HasSavedCamperStationCancellation(identity,admission,queued,intent,refund,attempt,cancellation)||
                !(HasSaved(record)||(savedPending||attemptPending)&&HasSaved(primaryBefore))||
                !RebirthStationCompletionReceipt.HasNoJobReceipt(station.CraftCompleteList,job))return false;
            foreach(var row in station.Queue)
                if(row==null||RebirthStationGridQueue.IsMarked(row.Recipe)&&
                    (!RebirthStationGridQueue.TryGetJobId(row.Recipe,out var queuedJob)||queuedJob==job))return false;
            return true;
        }
        catch{return false;}
    }
    internal bool CanContinue=>Current()&&(proof!=null||request==null||!request.Closed);
    private bool ObserveVehicle()
    {
        if(!Current())return false;
        if(proof==null)
        {
            if(request==null)request=RebirthCamperStationSaveEvidence.BeginRecovery(player,station,address,Current);
            if(!RebirthCamperStationSaveEvidence.TryObserve(request,out var observed))return false;
            proof=observed;
        }
        return cancellation.MatchesCurrentCancellation(proof,queued)&&Current();
    }
    private bool Receipt(out float value)
    {
        value=0;
        // Missing is unknown, not an unattempted native receipt.
        if(Local==null)return RebirthStationRefundPlayerFileWitness.TryReadSavedPhase(identity,record.DeliveryId,out var present,out value)&&present;
        return Local.Buffs?.CVars!=null&&Local.Buffs.CVars.TryGetValue("rbStationRefund_"+record.DeliveryId.ToString("N"),out value);
    }
    private bool NativeSaved()
        =>Receipt(out var phase)&&phase==1f&&
            RebirthStationRefundPlayerFileWitness.HasSavedReceipt(identity,record.DeliveryId)&&Current();
    internal bool TryObservePreparedBeforeImage()
    {
        try
        {
            if(record.IsAttempted||!ObserveVehicle()||!Receipt(out var phase)||phase!=3f)return false;
            if(Local==null)return preparedBefore=RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan)&&Current();
            var slots=Local.bag?.ItemGrid?.items;
            Func<int,bool> locked=slot=>Local.bag.LockedSlots!=null&&slot<Local.bag.LockedSlots.Length&&Local.bag.LockedSlots[slot];
            return preparedBefore=slots!=null&&plan.MatchesBefore(slots,slots.Length,locked)&&Current();
        }
        catch{return false;}
    }
    // Compatibility observation only. TryApply separately requires primary
    // prepared3 evidence and a durably acknowledged attempted owner phase.
    internal bool NeedsOriginalRecoveryRoute=>preparedBefore&&!record.IsAttempted&&TryObservePreparedBeforeImage();
    private bool MatchesBag(bool after)
    {
        if(Local==null)return after?RebirthStationRefundPlayerFileWitness.HasSavedReceipt(identity,record.DeliveryId):
            RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan);
        var slots=Local.bag?.ItemGrid?.items;
        return slots!=null&&(after?plan.MatchesAfter(slots):plan.MatchesBefore(slots,slots.Length,
            slot=>Local.bag.LockedSlots!=null&&slot<Local.bag.LockedSlots.Length&&Local.bag.LockedSlots[slot]));
    }
    private bool SaveNative(bool after)
    {
        if(Local==null||!Current()||!MatchesBag(after))return false;
        var game=GameManager.Instance;
        if(game==null||!ReferenceEquals(game.World,world)||game.getPersistentPlayerID(null)?.CombinedString!=identity.CanonicalId)return false;
        game.SaveLocalPlayerData();
        return Current()&&MatchesBag(after)&&ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(game.World,world)&&
            game.getPersistentPlayerID(null)?.CombinedString==identity.CanonicalId&&
            (after?RebirthStationRefundPlayerFileWitness.HasSaved(identity,record.DeliveryId,plan):
                Receipt(out var phase)&&phase==3f&&RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan));
    }
    private bool PrepareNativeBefore()
    {
        if(Local==null||record.IsSaved||!ObserveVehicle()||!MatchesBag(false)||Local.Buffs?.CVars==null)return false;
        bool present=Receipt(out var phase);
        if(present&&phase!=3f)return false;
        if(RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan))
        {
            if(present)return Current();
            // Only durable prepared owner custody can initialize a missing live receipt.
            if(record.IsAttempted||!HasSaved(record))return false;
        }
        else if(record.IsAttempted||!HasSaved(record)||
            !RebirthStationRefundPlayerFileWitness.HasSavedMissingBefore(identity,record.DeliveryId,plan))return false;
        if(!Current()||!MatchesBag(false))return false;
        if(!present)Local.Buffs.SetCustomVar("rbStationRefund_"+record.DeliveryId.ToString("N"),3f,true);
        return Receipt(out phase)&&phase==3f&&SaveNative(false);
    }
    internal bool TryApply()
    {
        try
        {
            if(Local==null)return TryApplyRemote();
            if(applicationIssued||record.IsSaved||!PrepareNativeBefore())return false;
            if(!record.IsAttempted)
            {
                if(!record.TryMarkAttempted(out var attempted))return false;
                record=attempted;attemptPending=true;owner.Progression.CamperStationRefundDeliveries[record.JobId]=attempted.Clone();
            }
            if(!Current()||!MatchesBag(false)||!Receipt(out var phase)||phase!=3f||
                !RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan))return false;
            if(!HasSaved(record))
            {
                RebirthWorldCharacterService.MarkDirty(owner,"camper-refund-cold-attempt");
                if(!RebirthWorldCharacterService.FlushPlayer(player,"camper-refund-cold-attempt"))return false;
            }
            if(!ObserveVehicle()||!HasSaved(record)||!MatchesBag(false)||!Receipt(out phase)||phase!=3f||
                !RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan))return false;
            attemptPending=false;primaryBefore=record.Clone();
            permit=new RebirthStationRefundPreparationService.ApplicationPermit(Local,plan,"rbStationRefund_"+record.DeliveryId.ToString("N"),
                after=>Current()&&MatchesBag(after)&&record.IsAttempted&&(!record.IsSaved||after),()=>SaveNative(true),ObserveSaved,3f,
                ()=>RebirthCamperStationPartialCancellationRewards.ApplyNative(Local,admission,intent,"rbStationRefund_"+record.DeliveryId.ToString("N")));
            applicationIssued=true;
            return permit.TryApply();
        }
        catch{return false;}
    }
    internal bool TryConfirmSaved()
        =>Local!=null&&applicationIssued?permit!=null&&permit.TryConfirmSaved():ObserveSaved();
    private bool TryApplyRemote()
    {
        if(remoteUncertain||record.IsSaved||!ObserveVehicle()||!RebirthCamperRemoteOwnerApplication.IsRegistered)return false;
        if(!record.IsAttempted)
        {
            if(!HasSaved(record))return false;
            if(!RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan))
            {
                if(!RebirthStationRefundPlayerFileWitness.HasSavedMissingBefore(identity,record.DeliveryId,plan))return false;
                if(!remotePrepareSent)
                {
                    if(!RebirthCamperStationRemoteRefundPayload.TryPrepareOperation(record.DeliveryId,out var operation)||
                        !RebirthCamperStationRemoteRefundPayload.TryCreate(player,identity,admission,queued,intent,refund,attempt,cancellation,record,true,out var payload)||
                        !Current()||!HasSaved(record))return false;
                    remotePrepareSent=true;
                    if(!RebirthCamperRemoteOwnerApplication.SendOwnerApplication(player,operation,RebirthCamperRemoteOwnerEffectKind.Refund,payload,
                        ack=>{if(remotePrepareDone)return;remotePrepareDone=true;remotePrepareAck=ack&&Current();if(!remotePrepareAck)remoteUncertain=true;}))remoteUncertain=true;
                }
                if(!remotePrepareAck||!Current()||!RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan))return false;
            }
            if(!record.TryMarkAttempted(out var attempted))return false;
            record=attempted;attemptPending=true;owner.Progression.CamperStationRefundDeliveries[record.JobId]=attempted.Clone();
        }
        if(!Current()||!RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan))return false;
        if(!HasSaved(record))
        {
            RebirthWorldCharacterService.MarkDirty(owner,"camper-refund-remote-cold-attempt");
            if(!RebirthWorldCharacterService.FlushPlayer(player,"camper-refund-remote-cold-attempt"))return false;
        }
        if(!ObserveVehicle()||!HasSaved(record)||!RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan))return false;
        attemptPending=false;primaryBefore=record.Clone();
        if(!remoteApplySent)
        {
            if(!RebirthCamperStationRemoteRefundPayload.TryCreate(player,identity,admission,queued,intent,refund,attempt,cancellation,record,false,out var payload)||
                !Current()||!HasSaved(record))return false;
            remoteApplySent=true;
            if(!RebirthCamperRemoteOwnerApplication.SendOwnerApplication(player,record.DeliveryId,RebirthCamperRemoteOwnerEffectKind.Refund,payload,
                ack=>{if(remoteApplyDone)return;remoteApplyDone=true;remoteApplyAck=ack&&Current();if(!remoteApplyAck)remoteUncertain=true;}))remoteUncertain=true;
        }
        return !remoteUncertain&&remoteApplyAck&&NativeSaved();
    }
    private bool ObserveSaved()
    {
        try
        {
            if(!record.IsAttempted||!ObserveVehicle()||!NativeSaved()||
                !RebirthCamperStationPartialCancellationRewards.TryTrain(player,admission,intent,record.DeliveryId))return false;
            if(!record.IsSaved)
            {
                if(!record.TryMarkSaved(out var saved))return false;
                record=saved;savedPending=true;owner.Progression.CamperStationRefundDeliveries[record.JobId]=saved.Clone();
            }
            if(!Current()||!NativeSaved())return false;
            if(!HasSaved(record))
            {
                RebirthWorldCharacterService.MarkDirty(owner,"camper-refund-cold-saved");
                if(!Current()||!NativeSaved()||!RebirthWorldCharacterService.FlushPlayer(player,"camper-refund-cold-saved"))return false;
            }
            if(!Current()||!NativeSaved()||!HasSaved(record))return false;
            savedPending=false;return confirmed=true;
        }
        catch{return false;}
    }
    internal bool TryGetSaved(out RebirthCamperStationRefundDeliveryRecord saved)
    {
        saved=null;
        try
        {
            if(!confirmed||!record.IsSaved||!ObserveVehicle()||!NativeSaved()||!HasSaved(record))return false;
            saved=record.Clone();return true;
        }
        catch{return false;}
    }
    public void Dispose()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Release refund observation on game thread.");
        if(closed)return;request?.Dispose();closed=true;
        // Cancellation/owner custody is never retired here.
    }
}
