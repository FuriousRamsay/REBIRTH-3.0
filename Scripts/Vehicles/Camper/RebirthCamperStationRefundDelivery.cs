using System;
using System.Linq;
using System.Xml.Linq;

// One original inventory application. Saved records cannot reconstruct this
// permit; uncertain application requires observation/recovery, never replay.
internal sealed class RebirthCamperStationRefundDelivery
{
    private readonly RebirthCamperStationCancellationCheckpoint checkpoint;
    private readonly RebirthCamperStationCancellationRemoval removal;
    private readonly RebirthCamperStationCancellationRecord cancellation;
    private readonly RebirthStationRefundBackpackPlan plan;
    private readonly EntityPlayerLocal player;
    private readonly object world,bag;
    private readonly int actor;
    private RebirthCamperStationRefundDeliveryRecord record;
    private RebirthStationRefundPreparationService.ApplicationPermit permit;
    private bool preparationSaved,applicationIssued,savedConfirmed;
    private RebirthCamperStationRefundDelivery(RebirthCamperStationCancellationCheckpoint checkpoint,
        RebirthCamperStationCancellationRemoval removal,RebirthCamperStationCancellationRecord cancellation,
        RebirthStationRefundBackpackPlan plan,RebirthCamperStationRefundDeliveryRecord record,EntityPlayerLocal player)
    {
        this.checkpoint=checkpoint;this.removal=removal;this.cancellation=cancellation.Clone();this.plan=plan;
        this.record=record.Clone();this.player=player;world=player.world;bag=player.bag;actor=player.entityId;
    }
    internal static bool TryBegin(RebirthCamperStationCancellationCheckpoint checkpoint,
        RebirthCamperStationCancellationRemoval removal,out RebirthCamperStationRefundDelivery operation)
    {
        operation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||checkpoint==null||removal==null||
                !(checkpoint.Binding.Player is EntityPlayerLocal player)||RebirthCharacterCreationHoldService.IsHeld(player)||
                !removal.TryGetCancellationRecord(out var cancelled)||!removal.TryPrepareRefund(out var plan))return false;
            var binding=checkpoint.Binding;var records=binding.Progression.CamperStationRefundDeliveries;
            if(records.ContainsKey(binding.Admission.JobId)||records.Count>=64)return false;
            var id=Guid.NewGuid();
            if(records.Values.Any(r=>r==null||r.DeliveryId==id)||
                !RebirthCamperStationRefundDeliveryRecord.TryCreate(id,binding.Identity.StorageKey,binding.Admission,binding.Publication,
                    checkpoint.CopyIntent(),checkpoint.CopyRefund(),checkpoint.CopyAttempt(),cancelled,plan,out var prepared))return false;
            var proposed=new RebirthCamperStationRefundDelivery(checkpoint,removal,cancelled,plan,prepared,player);
            if(!proposed.Current(false,false))return false;
            operation=proposed;records.Add(binding.Admission.JobId,prepared.Clone());
            return proposed.TrySavePreparation();
        }
        catch{return false;}
    }
    private bool Current(bool after,bool requireRecord=true)
    {
        try
        {
            var binding=checkpoint.Binding;
            if(!ThreadManager.IsMainThread()||player.entityId!=actor||!ReferenceEquals(player.world,world)||
                !ReferenceEquals(player.bag,bag)||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player)||
                RebirthCharacterCreationHoldService.IsHeld(player)||!removal.MatchesRefundCustody(checkpoint,cancellation))return false;
            if(requireRecord&&(!binding.Progression.CamperStationRefundDeliveries.TryGetValue(record.JobId,out var retained)||
                retained==null||!XNode.DeepEquals(retained.Write(),record.Write())))return false;
            var slots=player.bag?.ItemGrid?.items;
            Func<int,bool> locked=slot=>player.bag.LockedSlots!=null&&slot<player.bag.LockedSlots.Length&&player.bag.LockedSlots[slot];
            return slots!=null&&(after?plan.MatchesAfter(slots):plan.MatchesBefore(slots,slots.Length,locked));
        }
        catch{return false;}
    }
    private bool HasSavedRecord()
    {
        var b=checkpoint.Binding;
        return RebirthWorldCharacterRepository.HasSavedCamperStationRefundDelivery(b.Identity,b.Admission,b.Publication,
            checkpoint.CopyIntent(),checkpoint.CopyRefund(),checkpoint.CopyAttempt(),cancellation,record);
    }
    private bool Persist(bool after)
    {
        if(!Current(after))return false;
        var b=checkpoint.Binding;
        RebirthWorldCharacterService.MarkDirty(b.Owner,"camper-refund-delivery");
        return RebirthWorldCharacterService.FlushPlayer(player,"camper-refund-delivery")&&Current(after)&&HasSavedRecord()&&Current(after);
    }
    internal bool TrySavePreparation()
    {
        try
        {
            if(record.IsAttempted||!Current(false))return false;
            if(!preparationSaved&&!Persist(false))return false;
            preparationSaved=HasSavedRecord()&&Current(false);
            return preparationSaved&&TrySaveNativeBefore();
        }
        catch{return false;}
    }
    private bool TrySaveNativeBefore()
    {
        if(!Current(false)||!HasSavedRecord()||record.IsAttempted||player.Buffs?.CVars==null)return false;
        string key="rbStationRefund_"+record.DeliveryId.ToString("N");
        bool present=player.Buffs.CVars.TryGetValue(key,out var phase);
        if(present&&phase!=3f)return false;
        var identity=checkpoint.Binding.Identity;
        // Original live custody supplies the exact current beforebag. The prior
        // primary bag may legitimately predate ingredient withdrawal.
        if(!RebirthStationRefundPlayerFileWitness.TryReadSavedPhase(identity,record.DeliveryId,out var savedPresent,out var savedPhase)||
            savedPresent&&savedPhase!=3f)return false;
        if(!Current(false)||!HasSavedRecord())return false;
        if(!present)player.Buffs.SetCustomVar(key,3f,true);
        var game=GameManager.Instance;
        if(!player.Buffs.CVars.TryGetValue(key,out phase)||phase!=3f||!Current(false)||game==null||
            !ReferenceEquals(game.World,world)||game.getPersistentPlayerID(null)?.CombinedString!=identity.CanonicalId)return false;
        game.SaveLocalPlayerData();
        return Current(false)&&ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(game.World,world)&&
            game.getPersistentPlayerID(null)?.CombinedString==identity.CanonicalId&&
            player.Buffs.CVars.TryGetValue(key,out phase)&&phase==3f&&
            RebirthStationRefundPlayerFileWitness.HasSavedBefore(identity,record.DeliveryId,plan);
    }
    internal bool TryApply()
    {
        try
        {
            if(applicationIssued)return false;
            if(permit==null)
            {
                if(!record.IsAttempted)
                {
                    if(!TrySavePreparation()||!record.TryMarkAttempted(out var attempted))return false;
                    record=attempted;checkpoint.Binding.Progression.CamperStationRefundDeliveries[record.JobId]=attempted.Clone();
                }
                if(record.IsSaved||!Persist(false)||player.Buffs?.CVars==null||
                    !player.Buffs.CVars.TryGetValue("rbStationRefund_"+record.DeliveryId.ToString("N"),out var phase)||phase!=3f||
                    !RebirthStationRefundPlayerFileWitness.HasSavedBefore(checkpoint.Binding.Identity,record.DeliveryId,plan))return false;
                permit=new RebirthStationRefundPreparationService.ApplicationPermit(player,plan,"rbStationRefund_"+record.DeliveryId.ToString("N"),
                    after=>Current(after)&&record.IsAttempted&&(!record.IsSaved||after),SaveNativePlayer,RetainSaved,3f,
                    ()=>RebirthCamperStationPartialCancellationRewards.ApplyNative(player,checkpoint.Binding.Admission,checkpoint.CopyIntent(),
                        "rbStationRefund_"+record.DeliveryId.ToString("N")));
            }
            applicationIssued=true; // even partial/failed native application cannot repeat
            return permit.TryApply();
        }
        catch{return false;}
    }
    private bool SaveNativePlayer()
    {
        if(!Current(true))return false;
        var game=GameManager.Instance;
        if(game==null||!ReferenceEquals(game.World,world)||game.getPersistentPlayerID(null)?.CombinedString!=checkpoint.Binding.Identity.CanonicalId)return false;
        game.SaveLocalPlayerData();
        return Current(true)&&ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(game.World,world)&&
            game.getPersistentPlayerID(null)?.CombinedString==checkpoint.Binding.Identity.CanonicalId&&
            RebirthStationRefundPlayerFileWitness.HasSaved(checkpoint.Binding.Identity,record.DeliveryId,plan);
    }
    private bool RetainSaved()
    {
        if(!Current(true)||!RebirthStationRefundPlayerFileWitness.HasSavedReceipt(checkpoint.Binding.Identity,record.DeliveryId)||
            !RebirthCamperStationPartialCancellationRewards.TryTrain(player,checkpoint.Binding.Admission,checkpoint.CopyIntent(),record.DeliveryId))return false;
        if(!record.IsSaved)
        {
            if(!record.TryMarkSaved(out var saved))return false;
            record=saved;checkpoint.Binding.Progression.CamperStationRefundDeliveries[record.JobId]=saved.Clone();
        }
        return Persist(true);
    }
    internal bool TryConfirmSaved()
    {
        try
        {
            if(savedConfirmed)return Current(true)&&HasSavedRecord()&&
                RebirthStationRefundPlayerFileWitness.HasSaved(checkpoint.Binding.Identity,record.DeliveryId,plan);
            return savedConfirmed=applicationIssued&&permit!=null&&permit.TryConfirmSaved()&&record.IsSaved&&Current(true)&&HasSavedRecord();
        }
        catch{return false;}
    }
    internal bool TryGetSaved(out RebirthCamperStationRefundDeliveryRecord saved)
    {
        saved=null;
        if(!TryConfirmSaved())return false;
        saved=record.Clone();return true;
    }
    internal bool IsConfirmedFor(RebirthCamperStationCancellationCheckpoint expectedCheckpoint,
        RebirthCamperStationCancellationRemoval expectedRemoval)
        =>ReferenceEquals(checkpoint,expectedCheckpoint)&&ReferenceEquals(removal,expectedRemoval)&&TryConfirmSaved();
}
