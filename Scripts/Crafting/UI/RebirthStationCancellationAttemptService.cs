using System;
using System.Xml.Linq;

// Persists an attempt checkpoint only. Returned records never authorize native effects.
internal static class RebirthStationCancellationAttemptService
{
    internal static bool TryBegin(EntityPlayer player,Vector3i position,string creation,Guid job,
        out RebirthStationCancellationAttempt attempt)
        =>TryBeginCore(player,position,creation,job,false,out attempt,out _);

    private static bool TryBeginCore(EntityPlayer player,Vector3i position,string creation,Guid job,bool mintPermit,
        out RebirthStationCancellationAttempt attempt,out RemovalPermit permit)
    {
        attempt=null;permit=null;
        if(!RebirthStationTerminalIntentService.TrySave(player,position,creation,job,false,out var intent)||
            !RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
        var key=job.ToString("N");
        if(owner.Progression.StationCancellationAttempts.ContainsKey(key)||owner.Progression.StationCancellationAttempts.Count>=64||
            !owner.Progression.StationPreparations.TryGetValue(key,out var admission)||admission==null||
            !owner.Progression.StationTerminalIntents.TryGetValue(key,out var retained)||retained==null||
            !XNode.DeepEquals(retained.Write(),intent.Write())||
            !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var refund)||refund==null)return false;
        try
        {
            var admissionImage=admission.Write();var intentImage=retained.Write();var refundImage=refund.Write();
            if(!RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var before)||
                !RebirthStationTerminalSnapshot.TryCapture(station,out var terminal)||
                !RebirthStationCompletionReceipt.HasNoJobReceipt(station.CraftCompleteList,key)||
                !RebirthStationNativeQueueEntry.TryBuildCancellation(station.Queue,admission,retained,player.entityId,out var replacement)||
                !RebirthStationNativeQueueSnapshot.TryCapture(replacement,out var after)||
                !RebirthStationCancellationAttempt.TryCreate(admission,retained,refund,before,after,terminal,out var requested))return false;
            // Recheck both custody and exact cached obligations around every storage boundary.
            Func<bool> unchanged=()=>
                RebirthStationLiveAccess.TryResolve(player,position,creation,out var current,out var currentOwner)&&
                ReferenceEquals(current,station)&&ReferenceEquals(currentOwner,owner)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)&&
                (string)admissionImage.Attribute("block")==current.block?.GetBlockName()&&
                before.Matches(current.Queue)&&terminal.Matches(current)&&
                RebirthStationCompletionReceipt.HasNoJobReceipt(current.CraftCompleteList,key)&&
                owner.Progression.StationPreparations.TryGetValue(key,out var currentAdmission)&&currentAdmission!=null&&
                XNode.DeepEquals(currentAdmission.Write(),admissionImage)&&
                owner.Progression.StationTerminalIntents.TryGetValue(key,out var currentIntent)&&currentIntent!=null&&
                XNode.DeepEquals(currentIntent.Write(),intentImage)&&
                owner.Progression.StationCancellationRefunds.TryGetValue(key,out var currentRefund)&&currentRefund!=null&&
                XNode.DeepEquals(currentRefund.Write(),refundImage)&&
                RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission);
            if(!unchanged()||owner.Progression.StationCancellationAttempts.ContainsKey(key)||
                !RebirthWorldCharacterRepository.HasSavedStationCancellationRefund(identity,admission,retained,refund))return false;
            owner.Progression.StationCancellationAttempts.Add(key,requested);
            RebirthWorldCharacterService.MarkDirty(owner,"station-cancellation-attempt");
            if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"station-cancellation-attempt"))return false;
            if(!unchanged()||!owner.Progression.StationCancellationAttempts.TryGetValue(key,out var saved)||saved==null||
                !XNode.DeepEquals(saved.Write(),requested.Write())||
                !RebirthWorldCharacterRepository.HasSavedStationCancellationAttempt(identity,admission,retained,refund,requested))
            {RebirthWorldCharacterService.MarkDirty(owner,"station-cancellation-attempt-unconfirmed");return false;}
            attempt=requested.Clone();if(mintPermit)permit=new RemovalPermit(player,station,owner,position,creation,requested);return true;
        }
        catch{RebirthWorldCharacterService.MarkDirty(owner,"station-cancellation-attempt-uncertain");return false;}
    }
    // Confirms only the character checkpoint; native outcome must be classified separately.
    internal static bool TryConfirmCheckpoint(EntityPlayer player,Vector3i position,string creation,Guid job,
        out RebirthStationCancellationAttempt attempt)
    {
        attempt=null;
        if(job==Guid.Empty||!RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
        var key=job.ToString("N");
        if(!owner.Progression.StationPreparations.TryGetValue(key,out var admission)||admission==null||
            !owner.Progression.StationTerminalIntents.TryGetValue(key,out var intent)||intent==null||
            !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var refund)||refund==null||
            !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var retained)||retained==null)return false;
        try
        {
            var admissionImage=admission.Write();var intentImage=intent.Write();var refundImage=refund.Write();var attemptImage=retained.Write();
            if((int)admissionImage.Attribute("x")!=position.x||(int)admissionImage.Attribute("y")!=position.y||
                (int)admissionImage.Attribute("z")!=position.z||(string)admissionImage.Attribute("block")!=station.block?.GetBlockName()||
                !RebirthStationCancellationAttempt.TryRead(attemptImage,admission,intent,refund,out var validated)||
                !RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission))return false;
            RebirthWorldCharacterService.MarkDirty(owner,"station-cancellation-checkpoint");
            if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"station-cancellation-checkpoint"))return false;
            if(!RebirthStationLiveAccess.TryResolve(player,position,creation,out var current,out var currentOwner)||
                !ReferenceEquals(current,station)||!ReferenceEquals(currentOwner,owner)||
                !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                (string)admissionImage.Attribute("block")!=current.block?.GetBlockName()||
                !owner.Progression.StationPreparations.TryGetValue(key,out var a)||a==null||!XNode.DeepEquals(a.Write(),admissionImage)||
                !owner.Progression.StationTerminalIntents.TryGetValue(key,out var i)||i==null||!XNode.DeepEquals(i.Write(),intentImage)||
                !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var r)||r==null||!XNode.DeepEquals(r.Write(),refundImage)||
                !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var t)||t==null||!XNode.DeepEquals(t.Write(),attemptImage)||
                !RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission)||
                !RebirthWorldCharacterRepository.HasSavedStationCancellationAttempt(identity,admission,intent,refund,validated))
            {RebirthWorldCharacterService.MarkDirty(owner,"station-cancellation-checkpoint-unconfirmed");return false;}
            attempt=validated.Clone();return true;
        }
        catch{RebirthWorldCharacterService.MarkDirty(owner,"station-cancellation-checkpoint-uncertain");return false;}
    }
    // Save-only retained evidence recovery; never remints native removal or refund permission.
    internal static bool TryConfirmPublication(EntityPlayer player,Vector3i position,string creation,Guid job,string saveRoot,
        out RebirthStationCancellationPublication publication)
    {
        publication=null;
        if(job==Guid.Empty||!RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
        var key=job.ToString("N");
        if(!owner.Progression.StationPreparations.TryGetValue(key,out var admission)||admission==null||
            !owner.Progression.StationTerminalIntents.TryGetValue(key,out var intent)||intent==null||
            !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var refund)||refund==null||
            !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var retained)||retained==null||
            !owner.Progression.StationCancellationPublications.TryGetValue(key,out var pending)||pending==null)return false;
        try
        {
            var admissionImage=admission.Write();var intentImage=intent.Write();var refundImage=refund.Write();var attemptImage=retained.Write();var publicationImage=pending.Write();
            if((int)admissionImage.Attribute("x")!=position.x||(int)admissionImage.Attribute("y")!=position.y||
                (int)admissionImage.Attribute("z")!=position.z||(string)admissionImage.Attribute("block")!=station.block?.GetBlockName()||
                !RebirthStationCancellationAttempt.TryRead(attemptImage,admission,intent,refund,out var validated)||
                !RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission)||
                !RebirthStationCancellationPublication.TryRead(publicationImage,admission,intent,refund,retained,out _)||
                !pending.Revalidate(saveRoot,admission,intent,refund,retained))return false;
            RebirthWorldCharacterService.MarkDirty(owner,"station-cancelled-publication-recovery");
            if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"station-cancelled-publication-recovery"))return false;
            if(!RebirthStationLiveAccess.TryResolve(player,position,creation,out var current,out var currentOwner)||
                !ReferenceEquals(current,station)||!ReferenceEquals(currentOwner,owner)||
                !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                (string)admissionImage.Attribute("block")!=current.block?.GetBlockName()||
                !owner.Progression.StationPreparations.TryGetValue(key,out var a)||a==null||!XNode.DeepEquals(a.Write(),admissionImage)||
                !owner.Progression.StationTerminalIntents.TryGetValue(key,out var i)||i==null||!XNode.DeepEquals(i.Write(),intentImage)||
                !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var r)||r==null||!XNode.DeepEquals(r.Write(),refundImage)||
                !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var t)||t==null||!XNode.DeepEquals(t.Write(),attemptImage)||
                !RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission)||
                !owner.Progression.StationCancellationPublications.TryGetValue(key,out var p)||p==null||!XNode.DeepEquals(p.Write(),publicationImage)||
                !RebirthWorldCharacterRepository.HasSavedStationCancellationPublication(identity,admission,intent,refund,validated,pending)||
                !pending.Revalidate(saveRoot,admission,intent,refund,validated))
            {RebirthWorldCharacterService.MarkDirty(owner,"station-cancelled-publication-recovery-unconfirmed");return false;}
            publication=pending.Clone();return true;
        }
        catch{RebirthWorldCharacterService.MarkDirty(owner,"station-cancelled-publication-recovery-uncertain");return false;}
    }
    internal sealed class RemovalPermit
    {
        private readonly EntityPlayer player;
        private readonly object world;
        private readonly TileEntityWorkstation station;
        private readonly RebirthWorldCharacterRecord owner;
        private readonly Vector3i position;
        private readonly string creation;
        private readonly RebirthStationCancellationAttempt attempt;
        private readonly int thread,actor;
        private bool consumed;
        internal RemovalPermit(EntityPlayer p,TileEntityWorkstation s,RebirthWorldCharacterRecord o,Vector3i pos,string c,RebirthStationCancellationAttempt a)
        {player=p;world=p.world;station=s;owner=o;position=pos;creation=c;attempt=a.Clone();thread=System.Threading.Thread.CurrentThread.ManagedThreadId;actor=p.entityId;}
        // Reports live mutation only. A serialized native outcome is still required before refund.
        internal bool TryApply(out RebirthStationCancellationObservation observation)
        {
            observation=null;
            if(!TryConsume(out var replacement)||station.bDisableModifiedCheck)return false;
            bool applied=false;
            try
            {
                if(station.bDisableModifiedCheck)return false;
                station.SetDisableModifiedCheck(true);
                station.Queue=replacement;
                applied=RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var after)&&
                    after.Digest()==(string)attempt.Write().Attribute("after")&&
                    RebirthStationTerminalSnapshot.TryCapture(station,out var terminal)&&
                    terminal.Digest()==(string)attempt.Write().Attribute("terminal")&&
                    RebirthStationCancellationObservation.TryWatch(player,position,creation,Guid.ParseExact(attempt.JobId,"N"),out observation);
            }
            catch{applied=false;} // Keep attempt and any partial native effect; never replay or refund here.
            finally
            {
                try{station.SetDisableModifiedCheck(false);station.SetModified();}
                catch{applied=false;}
            }
            if(!applied){observation?.Dispose();observation=null;}
            return applied;
        }
        // Consumed even on failure; never retry a potentially ambiguous native operation.
        internal bool TryConsume(out RecipeQueueItem[] replacement)
        {
            replacement=null;
            if(consumed||thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)return false;
            consumed=true;
            try
            {
                var key=attempt.JobId;var image=attempt.Write();
                if(player.entityId!=actor||!ReferenceEquals(player.world,world)||
                    !RebirthStationLiveAccess.TryResolve(player,position,creation,out var current,out var currentOwner)||
                    !ReferenceEquals(current,station)||!ReferenceEquals(currentOwner,owner)||
                    !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                    !owner.Progression.StationPreparations.TryGetValue(key,out var admission)||admission==null||
                    (string)admission.Write().Attribute("block")!=current.block?.GetBlockName()||
                    !owner.Progression.StationTerminalIntents.TryGetValue(key,out var intent)||intent==null||
                    !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var refund)||refund==null||
                    !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var retained)||retained==null||
                    !XNode.DeepEquals(retained.Write(),image)||
                    !RebirthStationCancellationAttempt.TryRead(image,admission,intent,refund,out _)||
                    !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||
                    !RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission)||
                    !RebirthWorldCharacterRepository.HasSavedStationCancellationAttempt(identity,admission,intent,refund,attempt)||
                    !RebirthStationCompletionReceipt.HasNoJobReceipt(current.CraftCompleteList,key)||
                    !RebirthStationNativeQueueSnapshot.TryCapture(current.Queue,out var before)||before.Digest()!=(string)image.Attribute("before")||
                    !RebirthStationTerminalSnapshot.TryCapture(current,out var terminal)||terminal.Digest()!=(string)image.Attribute("terminal")||
                    !RebirthStationNativeQueueEntry.TryBuildCancellation(current.Queue,admission,intent,player.entityId,out var staged)||
                    !RebirthStationNativeQueueSnapshot.TryCapture(staged,out var after)||after.Digest()!=(string)image.Attribute("after"))return false;
                replacement=staged;return true;
            }
            catch{return false;}
        }
    }
    // Only an original successful begin may mint this session permission. Recovery never calls it.
    internal static bool TryBeginRemoval(EntityPlayer player,Vector3i position,string creation,Guid job,out RemovalPermit permit)
    {
        return TryBeginCore(player,position,creation,job,true,out _,out permit);
    }
}