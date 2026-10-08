using System;
using System.Xml.Linq;

// Authority-local owner preparation only. Remote owners require authenticated native inventory snapshots.
internal static class RebirthStationRefundPreparationService
{
    internal static bool TryPrepare(EntityPlayerLocal player,Vector3i position,string creation,Guid job,Guid deliveryId,
        string saveRoot,out RebirthStationRefundDelivery delivery)
    {
        delivery=null;
        if(player==null||job==Guid.Empty||deliveryId==Guid.Empty||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player)||
            !RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
        var key=job.ToString("N");
        foreach(var pair in owner.Progression.StationRefundDeliveries)
            if(pair.Key!=key&&pair.Value!=null&&(string)pair.Value.Write().Attribute("delivery")==deliveryId.ToString("N"))return false;
        if(owner.Progression.StationRefundDeliveries.TryGetValue(key,out var prior)&&(prior==null||prior.IsAttempted||
            (string)prior.Write().Attribute("delivery")!=deliveryId.ToString("N")))return false;
        if(!RebirthStationCancellationAttemptService.TryConfirmPublication(player,position,creation,job,saveRoot,out var publication)||
            !RebirthStationLiveAccess.TryResolve(player,position,creation,out var current,out var currentOwner)||
            !ReferenceEquals(current,station)||!ReferenceEquals(currentOwner,owner)||
            !owner.Progression.StationPreparations.TryGetValue(key,out var admission)||admission==null||
            !owner.Progression.StationTerminalIntents.TryGetValue(key,out var intent)||intent==null||
            !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var refund)||refund==null||
            !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var attempt)||attempt==null)return false;
        try
        {
            var a=admission.Write();var i=intent.Write();var r=refund.Write();var t=attempt.Write();var p=publication.Write();
            Func<int,bool> locked=slot=>player.bag?.LockedSlots!=null&&slot<player.bag.LockedSlots.Length&&player.bag.LockedSlots[slot];
            var slots=player.bag?.ItemGrid?.items;if(slots==null)return false;
            RebirthStationRefundBackpackPlan plan;
            if(owner.Progression.StationRefundDeliveries.TryGetValue(key,out var retained))
            {
                if(retained==null||retained.IsAttempted||(string)retained.Write().Attribute("delivery")!=deliveryId.ToString("N")||
                    !RebirthStationRefundDelivery.TryRead(retained.Write(),identity.StorageKey,admission,intent,refund,attempt,publication,out _)||
                    !retained.TryCopyPlan(refund,out plan))return false;
            }
            else
            {
                if(owner.Progression.StationRefundDeliveries.Count>=64||
                    !RebirthStationRefundBackpackPlan.TryCreate(slots,slots.Length,locked,refund,out plan)||
                    !RebirthStationRefundDelivery.TryCreate(deliveryId,identity.StorageKey,admission,intent,refund,attempt,publication,plan,out retained))return false;
            }
            Func<bool> unchanged=()=>ReferenceEquals(player.world?.GetPrimaryPlayer(),player)&&
                RebirthStationLiveAccess.TryResolve(player,position,creation,out var live,out var record)&&
                ReferenceEquals(live,station)&&ReferenceEquals(record,owner)&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)&&
                (int)a.Attribute("x")==position.x&&(int)a.Attribute("y")==position.y&&(int)a.Attribute("z")==position.z&&
                (string)a.Attribute("block")==live.block?.GetBlockName()&&
                owner.Progression.StationPreparations.TryGetValue(key,out var ca)&&ca!=null&&XNode.DeepEquals(ca.Write(),a)&&
                owner.Progression.StationTerminalIntents.TryGetValue(key,out var ci)&&ci!=null&&XNode.DeepEquals(ci.Write(),i)&&
                owner.Progression.StationCancellationRefunds.TryGetValue(key,out var cr)&&cr!=null&&XNode.DeepEquals(cr.Write(),r)&&
                owner.Progression.StationCancellationAttempts.TryGetValue(key,out var ct)&&ct!=null&&XNode.DeepEquals(ct.Write(),t)&&
                owner.Progression.StationCancellationPublications.TryGetValue(key,out var cp)&&cp!=null&&XNode.DeepEquals(cp.Write(),p)&&
                player.bag?.ItemGrid?.items!=null&&plan.MatchesBefore(player.bag.ItemGrid.items,player.bag.ItemGrid.items.Length,locked)&&
                RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission);
            if(!unchanged()||!publication.Revalidate(saveRoot,admission,intent,refund,attempt))return false;
            if(!owner.Progression.StationRefundDeliveries.ContainsKey(key))owner.Progression.StationRefundDeliveries.Add(key,retained);
            var image=retained.Write();RebirthWorldCharacterService.MarkDirty(owner,"station-refund-preparation");
            if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"station-refund-preparation"))return false;
            if(!unchanged()||!owner.Progression.StationRefundDeliveries.TryGetValue(key,out var saved)||saved==null||
                !XNode.DeepEquals(saved.Write(),image)||
                !RebirthWorldCharacterRepository.HasSavedStationRefundDelivery(identity,admission,intent,refund,attempt,publication,retained)||
                !publication.Revalidate(saveRoot,admission,intent,refund,attempt))
            {RebirthWorldCharacterService.MarkDirty(owner,"station-refund-preparation-unconfirmed");return false;}
            delivery=retained.Clone();return true;
        }
        catch{RebirthWorldCharacterService.MarkDirty(owner,"station-refund-preparation-uncertain");return false;}
    }
    // Save-only attempted phase. Returned record grants no inventory mutation or replay authority.
    internal static bool TryBeginDelivery(EntityPlayerLocal player,Vector3i position,string creation,Guid job,Guid deliveryId,
        string saveRoot,out RebirthStationRefundDelivery delivery)
        =>TryBeginDeliveryCore(player,position,creation,job,deliveryId,saveRoot,false,out delivery,out _);
    private static bool TryBeginDeliveryCore(EntityPlayerLocal player,Vector3i position,string creation,Guid job,Guid deliveryId,
        string saveRoot,bool mintPermit,out RebirthStationRefundDelivery delivery,out ApplicationPermit permit)
    {
        delivery=null;permit=null;
        if(!TryPrepare(player,position,creation,job,deliveryId,saveRoot,out var prepared)||
            !RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
        var key=job.ToString("N");
        if(!owner.Progression.StationRefundDeliveries.TryGetValue(key,out var retained)||retained==null||retained.IsAttempted||
            !XNode.DeepEquals(retained.Write(),prepared.Write())||
            !owner.Progression.StationPreparations.TryGetValue(key,out var admission)||admission==null||
            !owner.Progression.StationTerminalIntents.TryGetValue(key,out var intent)||intent==null||
            !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var refund)||refund==null||
            !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var attempt)||attempt==null||
            !owner.Progression.StationCancellationPublications.TryGetValue(key,out var publication)||publication==null)return false;
        try
        {
            var a=admission.Write();var i=intent.Write();var r=refund.Write();var t=attempt.Write();var p=publication.Write();
            if(!retained.TryCopyPlan(refund,out var plan)||!retained.TryMarkAttempted(out var attempted)||
                !RebirthStationRefundDelivery.TryRead(attempted.Write(),identity.StorageKey,admission,intent,refund,attempt,publication,out _))return false;
            Func<int,bool> locked=slot=>player.bag?.LockedSlots!=null&&slot<player.bag.LockedSlots.Length&&player.bag.LockedSlots[slot];
            Func<bool,bool> validateContext=afterApplication=>ReferenceEquals(player.world?.GetPrimaryPlayer(),player)&&
                RebirthStationLiveAccess.TryResolve(player,position,creation,out var live,out var record)&&
                ReferenceEquals(live,station)&&ReferenceEquals(record,owner)&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)&&
                (int)a.Attribute("x")==position.x&&(int)a.Attribute("y")==position.y&&(int)a.Attribute("z")==position.z&&
                (string)a.Attribute("block")==live.block?.GetBlockName()&&
                owner.Progression.StationPreparations.TryGetValue(key,out var ca)&&ca!=null&&XNode.DeepEquals(ca.Write(),a)&&
                owner.Progression.StationTerminalIntents.TryGetValue(key,out var ci)&&ci!=null&&XNode.DeepEquals(ci.Write(),i)&&
                owner.Progression.StationCancellationRefunds.TryGetValue(key,out var cr)&&cr!=null&&XNode.DeepEquals(cr.Write(),r)&&
                owner.Progression.StationCancellationAttempts.TryGetValue(key,out var ct)&&ct!=null&&XNode.DeepEquals(ct.Write(),t)&&
                owner.Progression.StationCancellationPublications.TryGetValue(key,out var cp)&&cp!=null&&XNode.DeepEquals(cp.Write(),p)&&
                player.bag?.ItemGrid?.items!=null&&(afterApplication?plan.MatchesAfter(player.bag.ItemGrid.items):plan.MatchesBefore(player.bag.ItemGrid.items,player.bag.ItemGrid.items.Length,locked))&&
                RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission);
            if(!validateContext(false)||!RebirthWorldCharacterRepository.HasSavedStationRefundDelivery(identity,admission,intent,refund,attempt,publication,retained)||
                !publication.Revalidate(saveRoot,admission,intent,refund,attempt))return false;
            owner.Progression.StationRefundDeliveries[key]=attempted;
            RebirthWorldCharacterService.MarkDirty(owner,"station-refund-delivery-attempt");
            if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"station-refund-delivery-attempt"))return false;
            if(!validateContext(false)||!owner.Progression.StationRefundDeliveries.TryGetValue(key,out var current)||current==null||
                !XNode.DeepEquals(current.Write(),attempted.Write())||
                !RebirthWorldCharacterRepository.HasSavedStationRefundDelivery(identity,admission,intent,refund,attempt,publication,attempted)||
                !publication.Revalidate(saveRoot,admission,intent,refund,attempt))
            {RebirthWorldCharacterService.MarkDirty(owner,"station-refund-delivery-attempt-unconfirmed");return false;}
            delivery=attempted.Clone();
            Func<RebirthStationRefundDelivery,bool> sameDelivery=checkpoint=>
            {
                if(checkpoint==null)return false;
                if(XNode.DeepEquals(checkpoint.Write(),attempted.Write()))return true;
                if(!checkpoint.IsSaved||!RebirthStationRefundDelivery.TryRead(checkpoint.Write(),identity.StorageKey,admission,intent,refund,attempt,publication,out _))return false;
                var normalized=checkpoint.Write();normalized.SetAttributeValue("phase","deliveryAttempted");
                return XNode.DeepEquals(normalized,attempted.Write());
            };
            if(mintPermit)permit=new ApplicationPermit(player,plan,"rbStationRefund_"+deliveryId.ToString("N"),afterApplication=>validateContext(afterApplication)&&
                owner.Progression.StationRefundDeliveries.TryGetValue(key,out var checkpoint)&&sameDelivery(checkpoint)&&(!checkpoint.IsSaved||afterApplication)&&
                (RebirthWorldCharacterRepository.HasSavedStationRefundDelivery(identity,admission,intent,refund,attempt,publication,attempted)||afterApplication&&RebirthWorldCharacterRepository.HasSavedStationRefundDelivery(identity,admission,intent,refund,attempt,publication,checkpoint))&&
                publication.Revalidate(saveRoot,admission,intent,refund,attempt),()=>
                {
                    var game=GameManager.Instance;
                    if(game==null||!ReferenceEquals(game.World,player.world)||
                        game.getPersistentPlayerID(null)?.CombinedString!=identity.CanonicalId)return false;
                    game.SaveLocalPlayerData();
                    return ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(game.World,player.world)&&
                        game.getPersistentPlayerID(null)?.CombinedString==identity.CanonicalId&&
                        RebirthStationRefundPlayerFileWitness.HasSaved(identity,deliveryId,plan);
                },()=>
                {
                    // Called only after native save proof and fresh applied context validation.
                    if(!attempted.TryMarkSaved(out var savedDelivery))return false;
                    owner.Progression.StationRefundDeliveries[key]=savedDelivery.Clone();
                    RebirthWorldCharacterService.MarkDirty(owner,"station-refund-delivery-saved");
                    return RebirthWorldCharacterRepository.SaveIfDirty(identity,"station-refund-delivery-saved")&&
                        owner.Progression.StationRefundDeliveries.TryGetValue(key,out var retainedSaved)&&retainedSaved!=null&&
                        XNode.DeepEquals(retainedSaved.Write(),savedDelivery.Write())&&
                        RebirthWorldCharacterRepository.HasSavedStationRefundDelivery(identity,admission,intent,refund,attempt,publication,savedDelivery);
                });
            return true;
        }
        catch{RebirthWorldCharacterService.MarkDirty(owner,"station-refund-delivery-attempt-uncertain");return false;}
    }
    // Confirms only the retained character checkpoint. Bag contents are not evidence of delivery.
    internal static bool TryConfirmDeliveryCheckpoint(EntityPlayerLocal player,Vector3i position,string creation,Guid job,Guid deliveryId,
        out RebirthStationRefundDelivery delivery)
    {
        delivery=null;
        if(player==null||job==Guid.Empty||deliveryId==Guid.Empty||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player)||
            !RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
        var key=job.ToString("N");
        if(!owner.Progression.StationRefundDeliveries.TryGetValue(key,out var retained)||retained==null||!retained.IsAttempted||
            (string)retained.Write().Attribute("delivery")!=deliveryId.ToString("N")||
            !owner.Progression.StationPreparations.TryGetValue(key,out var admission)||admission==null||
            !owner.Progression.StationTerminalIntents.TryGetValue(key,out var intent)||intent==null||
            !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var refund)||refund==null||
            !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var attempt)||attempt==null||
            !owner.Progression.StationCancellationPublications.TryGetValue(key,out var publication)||publication==null)return false;
        try
        {
            var a=admission.Write();var i=intent.Write();var r=refund.Write();var t=attempt.Write();var p=publication.Write();var d=retained.Write();
            if((int)a.Attribute("x")!=position.x||(int)a.Attribute("y")!=position.y||(int)a.Attribute("z")!=position.z||
                (string)a.Attribute("block")!=station.block?.GetBlockName()||
                !RebirthStationRefundDelivery.TryRead(d,identity.StorageKey,admission,intent,refund,attempt,publication,out _)||
                !RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission))return false;
            RebirthWorldCharacterService.MarkDirty(owner,"station-refund-delivery-checkpoint");
            if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"station-refund-delivery-checkpoint"))return false;
            if(!ReferenceEquals(player.world?.GetPrimaryPlayer(),player)||
                !RebirthStationLiveAccess.TryResolve(player,position,creation,out var live,out var record)||
                !ReferenceEquals(live,station)||!ReferenceEquals(record,owner)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                (string)a.Attribute("block")!=live.block?.GetBlockName()||
                !owner.Progression.StationPreparations.TryGetValue(key,out var ca)||ca==null||!XNode.DeepEquals(ca.Write(),a)||
                !owner.Progression.StationTerminalIntents.TryGetValue(key,out var ci)||ci==null||!XNode.DeepEquals(ci.Write(),i)||
                !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var cr)||cr==null||!XNode.DeepEquals(cr.Write(),r)||
                !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var ct)||ct==null||!XNode.DeepEquals(ct.Write(),t)||
                !owner.Progression.StationCancellationPublications.TryGetValue(key,out var cp)||cp==null||!XNode.DeepEquals(cp.Write(),p)||
                !owner.Progression.StationRefundDeliveries.TryGetValue(key,out var cd)||cd==null||!XNode.DeepEquals(cd.Write(),d)||
                !RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission)||
                !RebirthWorldCharacterRepository.HasSavedStationRefundDelivery(identity,admission,intent,refund,attempt,publication,retained))
            {RebirthWorldCharacterService.MarkDirty(owner,"station-refund-delivery-checkpoint-unconfirmed");return false;}
            delivery=retained.Clone();return true;
        }
        catch{RebirthWorldCharacterService.MarkDirty(owner,"station-refund-delivery-checkpoint-uncertain");return false;}
    }
    // Only original successful attempt save can create this session permit; confirmation cannot.
    internal static bool TryBeginApplication(EntityPlayerLocal player,Vector3i position,string creation,Guid job,Guid deliveryId,
        string saveRoot,out ApplicationPermit permit)
        =>TryBeginDeliveryCore(player,position,creation,job,deliveryId,saveRoot,true,out _,out permit);
    // Recovery verifies retained native delivery; it cannot mint a permit or mutate inventory.
    internal static bool TryRecoverSavedDelivery(EntityPlayerLocal player,Vector3i position,string creation,Guid job,Guid deliveryId,
        out RebirthStationRefundDelivery delivery)
    {
        delivery=null;
        if(!TryConfirmDeliveryCheckpoint(player,position,creation,job,deliveryId,out var checkpoint)||
            !RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
        try
        {
            var key=job.ToString("N");var world=player.world;var bag=player.bag;int actor=player.entityId;
            if(!owner.Progression.StationCancellationRefunds.TryGetValue(key,out var refund)||refund==null||
                !checkpoint.TryCopyPlan(refund,out var plan))return false;
            string receipt="rbStationRefund_"+deliveryId.ToString("N");
            Func<bool> nativeProof=()=>player.entityId==actor&&ReferenceEquals(player.world,world)&&ReferenceEquals(player.bag,bag)&&
                ReferenceEquals(world?.GetPrimaryPlayer(),player)&&player.Buffs?.GetCustomVar(receipt)==1f&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)&&
                RebirthStationLiveAccess.TryResolve(player,position,creation,out var live,out var record)&&
                ReferenceEquals(live,station)&&ReferenceEquals(record,owner)&&plan.MatchesAfter(bag?.ItemGrid?.items)&&
                GameManager.Instance!=null&&ReferenceEquals(GameManager.Instance.World,world)&&
                GameManager.Instance.getPersistentPlayerID(null)?.CombinedString==identity.CanonicalId&&
                RebirthStationRefundPlayerFileWitness.HasSaved(identity,deliveryId,plan);
            if(!nativeProof()||!owner.Progression.StationRefundDeliveries.TryGetValue(key,out var retained)||retained==null||
                !XNode.DeepEquals(retained.Write(),checkpoint.Write()))return false;
            RebirthStationRefundDelivery saved;
            if(checkpoint.IsSaved)saved=checkpoint.Clone();
            else if(!checkpoint.TryMarkSaved(out saved))return false;
            owner.Progression.StationRefundDeliveries[key]=saved;
            RebirthWorldCharacterService.MarkDirty(owner,"station-refund-saved-recovery");
            if(!TryConfirmDeliveryCheckpoint(player,position,creation,job,deliveryId,out var confirmed)||!confirmed.IsSaved||!nativeProof())return false;
            delivery=confirmed.Clone();return true;
        }
        catch{return false;}
    }
    internal sealed class ApplicationPermit
    {
        private readonly EntityPlayerLocal player;
        private readonly object world,bag;
        private readonly string receipt;
        private readonly RebirthStationRefundBackpackPlan plan;
        private readonly Func<bool,bool> validate;
        private readonly Func<bool> saveProof,retainSaved;
        private readonly int actor,thread;
        private bool consumed,appliedObserved;
        internal ApplicationPermit(EntityPlayerLocal p,RebirthStationRefundBackpackPlan destination,string receiptKey,Func<bool,bool> check,Func<bool> saveWitness,Func<bool> retainWitness)
        {player=p;world=p.world;bag=p.bag;plan=destination;receipt=receiptKey;validate=check;saveProof=saveWitness;retainSaved=retainWitness;actor=p.entityId;thread=System.Threading.Thread.CurrentThread.ManagedThreadId;}
        // Save confirmation may retry; it grants neither another application nor settlement authority.
        internal bool TryConfirmSaved()
        {
            if(!appliedObserved||thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)return false;
            try
            {
                Func<bool> current=()=>player.entityId==actor&&ReferenceEquals(player.world,world)&&
                    ReferenceEquals(player.bag,bag)&&player.Buffs?.GetCustomVar(receipt)==1f&&validate(true);
                return current()&&saveProof()&&current()&&retainSaved()&&current();
            }
            catch{return false;}
        }
        // Live application observation only; durable owner inventory receipt still required.
        internal bool TryApply()
        {
            if(consumed||thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)return false;
            consumed=true;
            try
            {
                if(player.entityId!=actor||!ReferenceEquals(player.world,world)||!ReferenceEquals(player.bag,bag)||!validate(false))return false;
                if(player.Buffs==null||player.Buffs.GetCustomVar(receipt)!=0f)return false;
                var after=plan.CopyAfter();
                if(!validate(false))return false;
                player.Buffs.SetCustomVar(receipt,2f,true);
                if(player.Buffs.GetCustomVar(receipt)!=2f||!validate(false))return false;
                player.bag.SetSlots(after);
                if(player.entityId!=actor||!ReferenceEquals(player.world,world)||!ReferenceEquals(player.bag,bag)||
                    player.Buffs.GetCustomVar(receipt)!=2f||!validate(true))return false;
                player.Buffs.SetCustomVar(receipt,1f,true);
                return appliedObserved=player.Buffs.GetCustomVar(receipt)==1f&&player.entityId==actor&&ReferenceEquals(player.world,world)&&
                    ReferenceEquals(player.bag,bag)&&validate(true);
            }
            catch{return false;} // Any partial effect belongs to retained attempted phase, never replay.
        }
    }
}