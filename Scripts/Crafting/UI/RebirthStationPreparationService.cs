using System;
using System.Linq;

// Live preparation producer; publication/payment remains a separate durable commit.
public static class RebirthStationPreparationService
{
    // Registered by authenticated remote station integration only. Absence
    // refuses actor-source remote preparation/publication, never uses replicas.
    internal delegate bool MobilePlayerPlanner(EntityPlayer player,RebirthCamperStationState station,Recipe recipe,int batches,int tier,
        out RebirthStationGridIngredients.Plan fragmentPlan,out ItemStack[] fragments,out RebirthCamperStationPlayerPaymentImage payment);
    internal static MobilePlayerPlanner RemotePlayerPlanner;
    internal static Func<EntityPlayer,RebirthCamperStationState,bool> RemoteUsesPlayerPayment;
    internal static Func<EntityPlayer,RebirthStationGridAdmission,Func<bool>,Action<bool>,bool> RemotePlayerPayment;
    internal static Action<EntityPlayer,RebirthStationGridAdmission,RebirthCamperStationSaveEvidence.Request> RemotePlayerPublished;
    // Retries the exact retained intent; changed buffs/tier must not rewrite its payment image.
    public static bool TryRetryUnpaid(EntityPlayer player,Vector3i position,string creationId,Guid job,
        out RebirthStationGridAdmission admission)
    {
        admission=null;
        if(job==Guid.Empty||!RebirthStationLiveAccess.TryResolve(player,position,creationId,out var station,out var owner)||
            !HasNoPublication(owner,station,job)||
            !owner.Progression.StationPreparations.TryGetValue(job.ToString("N"),out var pending)||pending==null||
            !TryPhysicalInput(station,out var physical)||!RebirthStationNativeInputCodec.IsRoundTrippable(physical)||
            !pending.MatchesUnpaidObservation(creationId,position.x,position.y,position.z,station.block?.GetBlockName(),physical))return false;
        var definitions=XUiM_Recipes.GetRecipes();
        if(!pending.TryMaterialize(definitions,out var queued,out _,out _)||
            !RebirthStationGridQueue.TryGetAdmittedSource(queued,definitions,out var source)||
            !RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,pending,source)||!CanUse(station,source,player))return false;
        // Re-resolve custody after recipe materialization before retrying the retained save.
        if(!RebirthStationLiveAccess.TryResolve(player,position,creationId,out var current,out var currentOwner)||
            !ReferenceEquals(current,station)||!ReferenceEquals(currentOwner,owner)||
            !HasNoPublication(currentOwner,current,job)||!TryPhysicalInput(current,out var currentInput)||
            !pending.MatchesUnpaidObservation(creationId,position.x,position.y,position.z,current.block?.GetBlockName(),currentInput))return false;
        if(!RebirthStationPreparationReservation.TryRegister(player.world,owner,pending,()=>Save(player,owner,pending)))return false;
        admission=pending.Clone();return true;
    }
    // Detached publication inputs only. No native debit, queue acceptance or completion occurs here.
    public static bool TryRetryMobileUnpaid(EntityPlayer player,RebirthCamperStationAddress address,string creationId,
        Guid job,out RebirthStationGridAdmission admission)
    {
        admission=null;
        if(job==Guid.Empty||!RebirthStationLiveAccess.TryResolveMobile(player,address,creationId,out var station,out var owner)||
            !HasNoPublication(owner,station,job)||
            !owner.Progression.StationPreparations.TryGetValue(job.ToString("N"),out var pending)||pending==null||
            pending.IsPublicationAttempted||owner.Progression.StationDiscoveryAdmissions.ContainsKey(pending.JobId)||
            !TryMobilePaymentInput(player,station,pending,out var input)||
            !pending.MatchesMobileUnpaidObservation(creationId,address,station.block?.GetBlockName(),input))return false;
        var definitions=XUiM_Recipes.GetRecipes();
        if(!pending.TryMaterialize(definitions,out var queued,out _,out _)||
            !RebirthStationGridQueue.TryGetAdmittedSource(queued,definitions,out var recipe)||
            !recipe.IsUnlocked(player)||!CanUse(station,recipe,player))return false;
        Func<bool> current=()=>RebirthStationLiveAccess.TryResolveMobile(player,address,creationId,out var live,out var liveOwner)&&
            ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&HasNoPublication(owner,live,job)&&
            owner.Progression.StationPreparations.TryGetValue(pending.JobId,out var retained)&&retained!=null&&
            System.Xml.Linq.XNode.DeepEquals(retained.Write(),pending.Write())&&
            !owner.Progression.StationDiscoveryAdmissions.ContainsKey(pending.JobId)&&recipe.IsUnlocked(player)&&CanUse(live,recipe,player)&&
            TryMobilePaymentInput(player,live,pending,out var physical)&&pending.MatchesMobileUnpaidObservation(creationId,address,live.block?.GetBlockName(),physical);
        if(!current()||!RebirthStationPreparationReservation.TryRegister(player.world,owner,pending,
            ()=>current()&&Save(player,owner,pending)&&current()))return false;
        admission=pending.Clone();return true;
    }
    // Detached publication inputs only. No native debit, queue acceptance or completion occurs here.
    internal static bool TryBuildPublicationInputs(EntityPlayer player,Vector3i position,string creationId,Guid job,
        out RebirthStationGridAdmission admission,out Recipe queued,out ItemStack[] nativeRemainder)
    {
        admission=null;queued=null;nativeRemainder=null;
        if(!TryRetryUnpaid(player,position,creationId,job,out var prepared)||
            !RebirthStationLiveAccess.TryResolve(player,position,creationId,out var station,out var owner)||
            !HasNoPublication(owner,station,job))return false;
        var definitions=XUiM_Recipes.GetRecipes();
        if(!prepared.TryMaterialize(definitions,out var recipe,out var before,out var after)||
            !RebirthStationGridQueue.TryGetAdmittedSource(recipe,definitions,out var source)||
            !RebirthStationDiscoveryScope.IsUnlockedForDiscovery(source,player)||!CanUse(station,source,player)||
            !RebirthStationInputPaymentImage.TryMerge(station.Input,station.InputSlotCount,station.MaterialNames.Length,
                before,after,out var merged)||
            !RebirthStationPreparationReservation.TryAcquire(player.world,owner,prepared))return false;
        admission=prepared.Clone();queued=recipe;nativeRemainder=merged;return true;
    }
    // Marks one durable native attempt; the result never itself mutates Input or Queue.
    internal static bool TryBeginPublicationAttempt(EntityPlayer player,Vector3i position,string creationId,Guid job,
        out RebirthStationGridAdmission attempted)
    {
        attempted=null;
        if(!TryRetryUnpaid(player,position,creationId,job,out var prepared)||
            !RebirthStationLiveAccess.TryResolve(player,position,creationId,out var station,out var owner)||
            !HasNoPublication(owner,station,job)||!TryPhysicalInput(station,out var physical)||
            !prepared.MatchesUnpaidObservation(creationId,position.x,position.y,position.z,station.block?.GetBlockName(),physical)||
            !RebirthStationNativeQueueEntry.TryFindVacantSlot(station.Queue,job,out _)||
            !RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var queueBefore))return false;
        var definitions=XUiM_Recipes.GetRecipes();
        if(!prepared.TryMaterialize(definitions,out var recipe,out var before,out var after)||
            !RebirthStationInputPaymentImage.TryMerge(station.Input,station.InputSlotCount,station.MaterialNames.Length,
                before,after,out _)||
            !RebirthStationNativeQueueEntry.TryCreate(recipe,definitions,player.entityId,out _))return false;
        if(!RebirthStationLiveAccess.TryResolve(player,position,creationId,out var current,out var currentOwner)||
            !ReferenceEquals(current,station)||!ReferenceEquals(currentOwner,owner)||
            !HasNoPublication(currentOwner,current,job)||
            !RebirthStationGridQueue.TryGetAdmittedSource(recipe,definitions,out var source)||
            !RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,currentOwner,prepared,source)||!CanUse(current,source,player)||current.MaterialNames==null||
            !RebirthStationInputPaymentImage.TryMerge(current.Input,current.InputSlotCount,current.MaterialNames.Length,
                before,after,out _)||!queueBefore.Matches(current.Queue)||
            !RebirthStationNativeQueueEntry.TryFindVacantSlot(current.Queue,job,out _))return false;
        if(!RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||
            !RebirthWorldCharacterRepository.TryGetSavedStationPreparationIntent(identity,prepared,out var savedIntent)||
            !savedIntent.MatchesCached(owner,prepared)||!RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,prepared)||
            !RebirthStationDiscoveryPreparationIntegration.HasSavedIntent(identity,owner,prepared)||
            !RebirthStationLiveAccess.TryResolve(player,position,creationId,out var finalStation,out var finalOwner)||
            !ReferenceEquals(finalStation,station)||!ReferenceEquals(finalOwner,owner)||!queueBefore.Matches(finalStation.Queue)||
            !RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,prepared,source)||!CanUse(finalStation,source,player))return false;
        return RebirthStationPreparationReservation.TryMarkPublicationAttempt(player.world,owner,prepared,
            next=>Save(player,owner,next),out attempted);
    }
    // Internal native publication only; success is live mutation, NOT durable output/publication proof.
    internal static bool TryPublishPrepared(EntityPlayer player,Vector3i position,string creationId,Guid job)
    {
        if(!TryBuildPublicationInputs(player,position,creationId,job,out _,out _,out _)||
            !RebirthStationLiveAccess.TryResolve(player,position,creationId,out var station,out var owner)||
            station.bDisableModifiedCheck||!RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var queueBefore)||
            !TryBeginPublicationAttempt(player,position,creationId,job,out var attempted))return false;
        if(!RebirthStationLiveAccess.TryResolve(player,position,creationId,out var current,out var currentOwner)||
            !ReferenceEquals(station,current)||!ReferenceEquals(owner,currentOwner)||current.bDisableModifiedCheck||
            !HasNoPublication(owner,current,job)||!queueBefore.Matches(current.Queue)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||
            !RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,attempted)||
            !RebirthWorldCharacterRepository.TryGetSavedStationPreparationIntent(identity,attempted,out var savedIntent)||
            !savedIntent.MatchesCached(owner,attempted)||!RebirthStationDiscoveryPreparationIntegration.HasSavedIntent(identity,owner,attempted))return false;
        var definitions=XUiM_Recipes.GetRecipes();
        if(!attempted.TryMaterialize(definitions,out var recipe,out var before,out var after)||
            !RebirthStationGridQueue.TryGetAdmittedSource(recipe,definitions,out var source)||
            !RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,currentOwner,attempted,source)||!CanUse(current,source,player)||current.MaterialNames==null||
            !RebirthStationInputPaymentImage.TryMerge(current.Input,current.InputSlotCount,current.MaterialNames.Length,
                before,after,out var merged)||
            !RebirthStationNativeQueueEntry.TryCreate(recipe,definitions,player.entityId,out var entry)||
            !RebirthStationNativeQueueEntry.TryBuildReplacement(current.Queue,job,entry,out var replacement)||
            !queueBefore.Matches(current.Queue)||
            !RebirthStationPreparationReservation.TryConsumeNativeAttempt(player.world,owner,attempted))return false;
        bool applied=false;
        // Both setters normally broadcast; suppress those intermediate notifications.
        if(!savedIntent.MatchesCached(owner,attempted)||
            !RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,attempted,source)||!CanUse(current,source,player))return false;
        current.SetDisableModifiedCheck(true);
        try
        {
            current.Input=merged;current.Queue=replacement;
            applied=RebirthStationObservationDispatcher.TryWatchQueued(player,attempted);
        }
        catch { applied=false; } // Retained attempted intent owns any uncertain partial effect.
        finally
        {
            current.SetDisableModifiedCheck(false);
            try{current.SetModified();}catch{applied=false;}
        }
        return applied;
    }
    // Save-only recovery of retained attempt intent. Success never authorizes another native attempt.
    internal static bool TryPublishMobilePrepared(EntityPlayer player,RebirthCamperStationAddress address,
        string creationId,Guid job,out RebirthStationGridAdmission attempted,
        out RebirthCamperStationSaveEvidence.Request saveRequest)
    {
        attempted=null;saveRequest=null;
        if(!TryRetryMobileUnpaid(player,address,creationId,job,out var prepared))return false;
        if(prepared.IsPlayerPayment)return TryPublishMobilePlayerPrepared(player,address,creationId,prepared,out attempted,out saveRequest);
        if(!RebirthStationLiveAccess.TryResolveMobile(player,address,creationId,out var station,out var owner)||
            station.bDisableModifiedCheck||station.MaterialNames==null||
            !RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var queueBefore)||
            !RebirthStationNativeQueueEntry.TryFindVacantSlot(station.Queue,job,out _))return false;
        var definitions=XUiM_Recipes.GetRecipes();
        if(!prepared.TryMaterialize(definitions,out var recipe,out var before,out var after)||
            !RebirthStationGridQueue.TryGetAdmittedSource(recipe,definitions,out var source)||
            !source.IsUnlocked(player)||!CanUse(station,source,player)||
            !RebirthStationInputPaymentImage.TryMerge(station.Input,station.InputSlotCount,station.MaterialNames.Length,
                before,after,out var merged)||
            !RebirthStationNativeQueueEntry.TryCreate(recipe,definitions,player.entityId,out var entry)||
            !RebirthStationNativeQueueEntry.TryBuildReplacement(station.Queue,job,entry,out var replacement)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||
            !RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,prepared))return false;
        Func<bool> unpaidCurrent=()=>RebirthStationLiveAccess.TryResolveMobile(player,address,creationId,out var live,out var liveOwner)&&
            ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&!live.bDisableModifiedCheck&&
            HasNoPublication(owner,live,job)&&queueBefore.Matches(live.Queue)&&source.IsUnlocked(player)&&CanUse(live,source,player)&&
            !owner.Progression.StationDiscoveryAdmissions.ContainsKey(prepared.JobId)&&TryPaymentInput(live,prepared.IsMaterialPayment,out var physical)&&
            prepared.MatchesMobileUnpaidObservation(creationId,address,live.block?.GetBlockName(),physical);
        if(!unpaidCurrent()||!RebirthStationPreparationReservation.TryMarkPublicationAttempt(player.world,owner,prepared,
            next=>unpaidCurrent()&&Save(player,owner,next)&&unpaidCurrent(),out var savedAttempt))return false;
        attempted=savedAttempt.Clone();
        if(!unpaidCurrent()||!RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,savedAttempt)||
            !RebirthStationInputPaymentImage.TryMerge(station.Input,station.InputSlotCount,station.MaterialNames.Length,
                before,after,out merged)||
            !RebirthStationPreparationReservation.TryConsumeNativeAttempt(player.world,owner,savedAttempt))return false;
        bool applied=false;
        station.SetDisableModifiedCheck(true);
        try
        {
            station.Input=merged;station.Queue=replacement;
            applied=RebirthCamperQueuedPublicationMatch.MatchesStation((RebirthCamperStationState)station,address,savedAttempt,player.entityId);
        }
        catch{applied=false;}
        finally
        {
            station.SetDisableModifiedCheck(false);
            try{station.SetModified();}catch{applied=false;}
        }
        // Any uncertain effect remains owned by the attempted record. Never
        // roll back payment or grant another attempt after this point.
        if(!applied)return false;
        Func<bool> queuedCurrent=()=>RebirthStationLiveAccess.TryResolveMobile(player,address,creationId,out var live,out var liveOwner)&&
            ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&
            owner.Progression.StationPreparations.TryGetValue(savedAttempt.JobId,out var retained)&&retained!=null&&
            System.Xml.Linq.XNode.DeepEquals(retained.Write(),savedAttempt.Write())&&
            !owner.Progression.StationTerminalIntents.ContainsKey(savedAttempt.JobId)&&
            !owner.Progression.StationCancellationAttempts.ContainsKey(savedAttempt.JobId)&&
            RebirthCamperQueuedPublicationMatch.MatchesStation((RebirthCamperStationState)live,address,savedAttempt,player.entityId);
        try{saveRequest=RebirthCamperStationSaveEvidence.Begin(player,queuedCurrent);return true;}
        catch{return false;}
    }
    // Save-only recovery of retained attempt intent. Success never authorizes another native attempt.
    internal static bool TryConfirmPublicationAttempt(EntityPlayer player,Vector3i position,string creationId,Guid job,
        out RebirthStationGridAdmission attempted)
    {
        attempted=null;
        if(job==Guid.Empty||!RebirthStationLiveAccess.TryResolve(player,position,creationId,out var station,out var owner)||
            owner.Progression.StationPublications.ContainsKey(job.ToString("N"))||
            !owner.Progression.StationPreparations.TryGetValue(job.ToString("N"),out var pending)||pending==null||
            !pending.IsPublicationAttempted||!RebirthSurvivorRequestScope.Matches(pending.CreationId,creationId))return false;
        var image=pending.Write();
        if((int)image.Attribute("x")!=position.x||(int)image.Attribute("y")!=position.y||
            (int)image.Attribute("z")!=position.z||
            !RebirthStationPreparationReservation.TryAcquire(player.world,owner,pending)||!Save(player,owner,pending))return false;
        attempted=pending.Clone();return true;
    }
    private static bool TryPublishMobilePlayerPrepared(EntityPlayer player,RebirthCamperStationAddress address,string creation,
        RebirthStationGridAdmission prepared,out RebirthStationGridAdmission attempted,out RebirthCamperStationSaveEvidence.Request request)
    {
        attempted=null;request=null;
        if(!(player is EntityPlayerLocal)&&(RemotePlayerPayment==null||RemotePlayerPublished==null))return false;
        if(!RebirthStationLiveAccess.TryResolveMobile(player,address,creation,out var station,out var owner)||station.bDisableModifiedCheck||
            !RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var queueBefore)||
            !prepared.TryMaterialize(XUiM_Recipes.GetRecipes(),out var recipe,out _,out _)||
            !RebirthStationGridQueue.TryGetAdmittedSource(recipe,XUiM_Recipes.GetRecipes(),out var source)||
            !prepared.TryGetPlayerPayment(out var payment)||!payment.MatchesPaid(recipe.ingredients)||
            !RebirthStationNativeQueueEntry.TryFindVacantSlot(station.Queue,Guid.ParseExact(prepared.JobId,"N"),out _))return false;
        Func<bool> unpaid=()=>RebirthStationLiveAccess.TryResolveMobile(player,address,creation,out var live,out var liveOwner)&&
            ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&!live.bDisableModifiedCheck&&
            HasNoPublication(owner,live,Guid.ParseExact(prepared.JobId,"N"))&&queueBefore.Matches(live.Queue)&&
            source.IsUnlocked(player)&&CanUse(live,source,player)&&
            TryMobilePaymentInput(player,live,prepared,out var input)&&prepared.MatchesMobileUnpaidObservation(creation,address,live.block?.GetBlockName(),input);
        if(!unpaid()||!RebirthStationPreparationReservation.TryMarkPublicationAttempt(player.world,owner,prepared,
            next=>unpaid()&&Save(player,owner,next)&&unpaid(),out var savedAttempt))return false;
        attempted=savedAttempt.Clone();
        Func<bool> current=()=>RebirthStationLiveAccess.TryResolveMobile(player,address,creation,out var live,out var liveOwner)&&
            ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&!live.bDisableModifiedCheck&&queueBefore.Matches(live.Queue)&&
            HasNoPublication(owner,live,Guid.ParseExact(savedAttempt.JobId,"N"))&&source.IsUnlocked(player)&&CanUse(live,source,player)&&
            payment.MatchesStation(live.Input)&&owner.Progression.StationPreparations.TryGetValue(savedAttempt.JobId,out var retained)&&
            retained!=null&&System.Xml.Linq.XNode.DeepEquals(retained.Write(),savedAttempt.Write())&&
            RebirthWorldCharacterService.TryGetIdentity(player,out var identity)&&
            RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,savedAttempt);
        if(!current()||!RebirthStationPreparationReservation.TryConsumeNativeAttempt(player.world,owner,savedAttempt))return false;
        bool queueIssued=false;
        Func<bool> claimQueue=()=>{if(queueIssued)return false;queueIssued=true;return true;};
        if(player is EntityPlayerLocal)
            return RebirthCamperStationPlayerPayment.TryApplyLocal(player,savedAttempt,current)&&
                TryPublishMobilePlayerPaid(player,savedAttempt,claimQueue,out attempted,out request);
        // Callback is the original owner delivery's saved-after witness; no
        // caller can use this branch to retry the debit or regenerate its plan.
        return RemotePlayerPayment(player,savedAttempt,current,paid=>
        {
            if(!paid||!current())return;
            if(TryPublishMobilePlayerPaid(player,savedAttempt,claimQueue,out var paidAdmission,out var nativeSave))
                RemotePlayerPublished(player,paidAdmission,nativeSave);
        });
    }
    private static bool TryPublishMobilePlayerPaid(EntityPlayer player,RebirthStationGridAdmission attempted,Func<bool> claimQueue,
        out RebirthStationGridAdmission confirmedAdmission,out RebirthCamperStationSaveEvidence.Request request)
    {
        confirmedAdmission=null;request=null;
        if(attempted==null||!attempted.IsPlayerPayment||!attempted.IsPublicationAttempted||
            !attempted.TryGetMobileAddress(out var address)||
            !RebirthStationLiveAccess.TryResolveMobile(player,address,attempted.CreationId,out var station,out var owner)||
            station.bDisableModifiedCheck||!RebirthCamperStationPlayerPayment.HasSaved(player,attempted)||
            !attempted.TryGetPlayerPayment(out var payment)||!payment.MatchesStation(station.Input)||
            !owner.Progression.StationPreparations.TryGetValue(attempted.JobId,out var retained)||retained==null||
            !System.Xml.Linq.XNode.DeepEquals(retained.Write(),attempted.Write())||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||
            !RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,attempted)||
            !HasNoPublication(owner,station,Guid.ParseExact(attempted.JobId,"N"))||
            !RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var queueBefore))return false;
        var definitions=XUiM_Recipes.GetRecipes();
        if(!attempted.TryMaterialize(definitions,out var recipe,out _,out _)||
            !RebirthStationGridQueue.TryGetAdmittedSource(recipe,definitions,out var source)||!source.IsUnlocked(player)||!CanUse(station,source,player)||
            !payment.MatchesPaid(recipe.ingredients)||!RebirthStationNativeQueueEntry.TryCreate(recipe,definitions,player.entityId,out var entry)||
            !RebirthStationNativeQueueEntry.TryBuildReplacement(station.Queue,Guid.ParseExact(attempted.JobId,"N"),entry,out var replacement))return false;
        Func<bool> current=()=>RebirthStationLiveAccess.TryResolveMobile(player,address,attempted.CreationId,out var live,out var liveOwner)&&
            ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&payment.MatchesStation(live.Input)&&
            source.IsUnlocked(player)&&CanUse(live,source,player)&&
            owner.Progression.StationPreparations.TryGetValue(attempted.JobId,out var saved)&&saved!=null&&
            System.Xml.Linq.XNode.DeepEquals(saved.Write(),attempted.Write())&&
            !owner.Progression.StationTerminalIntents.ContainsKey(attempted.JobId)&&
            !owner.Progression.StationCancellationAttempts.ContainsKey(attempted.JobId);
        if(!current()||!queueBefore.Matches(station.Queue)||
            !RebirthStationPreparationReservation.TryConfirmPlayerPayment(player,owner,attempted,next=>
                RebirthStationLiveAccess.TryResolveMobile(player,address,next.CreationId,out var live,out var liveOwner)&&
                ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&queueBefore.Matches(live.Queue)&&
                payment.MatchesStation(live.Input)&&source.IsUnlocked(player)&&CanUse(live,source,player)&&
                Save(player,owner,next)&&RebirthWorldCharacterRepository.HasSavedStationPreparation(identity,next),out var confirmed))return false;
        attempted=confirmed;confirmedAdmission=confirmed.Clone();
        if(!current()||!queueBefore.Matches(station.Queue)||!RebirthCamperStationPlayerPayment.HasSaved(player,attempted)||claimQueue==null||!claimQueue())return false;
        bool applied=false;station.SetDisableModifiedCheck(true);
        try
        {
            // Actor afterimage was saved before exposing this queue. Station
            // input remains its original independent custody witness.
            station.Queue=replacement;
            applied=RebirthCamperQueuedPublicationMatch.MatchesStation((RebirthCamperStationState)station,address,attempted,player.entityId);
        }
        catch{applied=false;}
        finally{station.SetDisableModifiedCheck(false);try{station.SetModified();}catch{applied=false;}}
        if(!applied)return false;
        try{request=RebirthCamperStationSaveEvidence.Begin(player,()=>current()&&
            RebirthCamperQueuedPublicationMatch.MatchesStation((RebirthCamperStationState)station,address,attempted,player.entityId));return true;}
        catch{return false;}
    }
    // Absence here is only a retry preflight, never proof for refund or queue replay.
    private static bool HasNoPublication(RebirthWorldCharacterRecord owner,TileEntityWorkstation station,Guid job)
    {
        string id=job.ToString("N");
        if(owner.Progression.StationPublications.ContainsKey(id)||owner.Progression.CamperStationPublications.ContainsKey(id)
            ||station.Queue==null)return false;
        foreach(var entry in station.Queue)
        {
            if(entry?.Recipe==null||!RebirthStationGridQueue.IsMarked(entry.Recipe))continue;
            if(!RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var queuedJob)||queuedJob==id)return false;
        }
        return true;
    }
    private static bool TryPhysicalInput(TileEntityWorkstation station,out ItemStack[] grid)
    {
        grid=null;
        return station?.MaterialNames!=null&&RebirthStationInputLayout.TryReadPhysical(
            station.Input,station.InputSlotCount,station.MaterialNames.Length,out grid);
    }
    private static bool TryPaymentInput(TileEntityWorkstation station,bool material,out ItemStack[] input)
    {
        input=null;
        if(!material)return TryPhysicalInput(station,out input)&&RebirthStationNativeInputCodec.IsRoundTrippable(input);
        if(station?.MaterialNames==null||station.Input==null||
            station.Input.Length!=station.InputSlotCount+station.MaterialNames.Length||
            !RebirthStationMaterialInputCodec.TryWrite(station.Input,station.InputSlotCount,out var image))return false;
        return RebirthStationMaterialInputCodec.TryRead(image,out input,out _);
    }
    private static bool UsesMobilePlayerPayment(EntityPlayer player,RebirthCamperStationState station)
        =>player is EntityPlayerLocal?RebirthStationPlayerIngredients.UsesPlayerPayment(player,station):
            RemoteUsesPlayerPayment!=null&&RemoteUsesPlayerPayment(player,station);
    private static bool TryMobilePlayerPlan(EntityPlayer player,RebirthCamperStationState station,Recipe recipe,int batches,int tier,
        out RebirthStationGridIngredients.Plan fragmentPlan,out ItemStack[] fragments,out RebirthCamperStationPlayerPaymentImage payment)
    {
        fragmentPlan=null;fragments=null;payment=null;
        if(!UsesMobilePlayerPayment(player,station))return false;
        return player is EntityPlayerLocal?RebirthCamperStationPlayerPayment.TryPlan(player,station,recipe,batches,tier,out fragmentPlan,out fragments,out payment):
            RemotePlayerPlanner!=null&&RemotePlayerPlanner(player,station,recipe,batches,tier,out fragmentPlan,out fragments,out payment);
    }
    private static bool TryMobilePaymentInput(EntityPlayer player,TileEntityWorkstation station,RebirthStationGridAdmission admission,out ItemStack[] input)
    {
        input=null;
        if(!admission.IsPlayerPayment)return TryPaymentInput(station,admission.IsMaterialPayment,out input);
        if(!(station is RebirthCamperStationState mobile)||!UsesMobilePlayerPayment(player,mobile)||
            !admission.TryGetPlayerPayment(out var payment)||!payment.MatchesStation(station.Input)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
            identity.CanonicalId!=payment.OwnerId||identity.StorageKey!=payment.OwnerKey)return false;
        if(player is EntityPlayerLocal&&!RebirthCamperStationPlayerPayment.MatchesBefore(player,admission))return false;
        // Remote unpaid images require the registered exact owner-file planner,
        // never EntityPlayer.bag/inventory replicas. Reproduce the retained plan.
        if(!(player is EntityPlayerLocal))
        {
            if(!admission.TryMaterialize(XUiM_Recipes.GetRecipes(),out var queued,out _,out _)||
                !RebirthStationGridQueue.TryGetAdmittedSource(queued,XUiM_Recipes.GetRecipes(),out var recipe)||
                !TryMobilePlayerPlan(player,mobile,recipe,admission.OriginalBatches,queued.craftingTier,out _,out _,out var current)||
                !System.Xml.Linq.XNode.DeepEquals(current.Write(),payment.Write()))return false;
        }
        input=payment.StationInputBefore();return true;
    }
    private static bool TryPaymentPlan(TileEntityWorkstation station,EntityPlayer player,Recipe recipe,
        ItemStack[] input,int batches,int tier,out RebirthStationGridIngredients.Plan plan)
        =>recipe.materialBasedRecipe?
            RebirthStationGridIngredients.TryPlanMaterial(player,recipe,input,station.InputSlotCount,station.MaterialNames,batches,tier,out plan):
            RebirthStationGridIngredients.TryPlan(player,recipe,input,batches,tier,out plan);
    private static bool CanUse(TileEntityWorkstation station,Recipe recipe,EntityPlayer player)
    {
        // Reading policy is independent of native unlock state and applies again on retained retries.
        if (player == null || recipe == null || !RebirthRecipeDiscoveryRules.Allows(player,recipe.GetName())) return false;
        var block=station.block;if(block==null)return false;
        string areas=block.Properties.Contains("Workstation","CraftingAreaRecipes")
            ?block.Properties.GetString("Workstation","CraftingAreaRecipes"):block.GetBlockName();
        return !string.IsNullOrEmpty(areas)&&areas.Split(',').Any(a=>
            string.Equals(a.Trim()=="player"?string.Empty:a.Trim(),recipe.craftingArea??string.Empty,StringComparison.OrdinalIgnoreCase))&&
            RebirthStationToolAvailability.HasRequirement(station.Tools,recipe);
    }
    private static bool Save(EntityPlayer player,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission prepared)
    {
        if(player?.world==null||owner==null||prepared==null||
            !RebirthWorldCharacterService.TryGet(player,out var currentOwner)||!ReferenceEquals(currentOwner,owner)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner))return false;
        RebirthStationDiscoveryAuthorityScope scope=null;
        bool discovery=owner.Progression.StationDiscoveryAdmissions.TryGetValue(prepared.JobId,out var frozen);
        if(discovery&&!RebirthStationDiscoveryPreparationPair.TryValidateLive(player,prepared,frozen,out scope))return false;
        Func<bool> sameIntent=()=>owner.Progression.StationDiscoveryAdmissions.TryGetValue(prepared.JobId,out var live)==discovery&&
            (!discovery||live!=null&&scope.IsCurrent()&&System.Xml.Linq.XNode.DeepEquals(live.Write(),frozen.Write()));
        if(!RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||
            !RebirthWorldCharacterRepository.TryGetSavedStationPreparationIntent(identity,prepared,out var originalIntent)||
            !originalIntent.MatchesCached(owner,prepared)||!sameIntent())return false;
        World originalWorld=player.world;
        // Reuse the authority-thread, identity, exclusive claim and final-file witness.
        // Registration is idempotent for the exact retained admission, including attempt phase.
        return sameIntent()&&RebirthStationObservationDispatcher.TrySavePreparation(player,prepared)&&sameIntent()&&originalIntent.MatchesCached(owner,prepared)&&
            ReferenceEquals(player.world,originalWorld)&&
            RebirthWorldCharacterService.TryGet(player,out currentOwner)&&ReferenceEquals(currentOwner,owner)&&
            RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner);
    }
    public static bool TryPrepare(EntityPlayer player,Vector3i position,string creationId,Guid job,
        Recipe recipe,int batches,out RebirthStationGridAdmission admission)
        =>TryPrepareCore(player,position,creationId,job,recipe,batches,false,out admission);
    // Mobile custody is resolved only from the authenticated vehicle window lease.
    // Coordinates are diagnostic; the durable reservation uses the full address.
    public static bool TryPrepareMobile(EntityPlayer player,RebirthCamperStationAddress address,string creationId,
        Guid job,Recipe recipe,int batches,out RebirthStationGridAdmission admission,int requestedTier=-1)
    {
        admission=null;
        if(job==Guid.Empty||recipe==null||RebirthStationGridQueue.IsMarked(recipe)||
            !RebirthStationLiveAccess.TryResolveMobile(player,address,creationId,out var station,out var owner)||
            !HasNoPublication(owner,station,job)||owner.Progression.StationDiscoveryAdmissions.ContainsKey(job.ToString("N")))return false;
        var definitions=XUiM_Recipes.GetRecipes();
        if(definitions==null||!definitions.Any(r=>ReferenceEquals(r,recipe))||!recipe.IsUnlocked(player)||
            !CanUse(station,recipe,player)||station.block==null)return false;
        if(!TryMobileTier(recipe,player,requestedTier,out var tier))return false;
        float seconds=EffectManager.GetValue(PassiveEffects.CraftingTime,_originalValue:recipe.craftingTime,
            _entity:player,_recipe:recipe,tags:recipe.tags)*XUiM_Recipes.CraftingTimeModifier;
        if(float.IsNaN(seconds)||float.IsInfinity(seconds))return false;
        var position=station.ToWorldPos();
        RebirthStationGridAdmission prepared;
        if(UsesMobilePlayerPayment(player,(RebirthCamperStationState)station)&&!recipe.materialBasedRecipe)
        {
            if(!TryMobilePlayerPlan(player,(RebirthCamperStationState)station,recipe,batches,tier,out var actorPlan,out var fragments,out var payment)||
                !RebirthStationGridAdmission.TryCreateMobilePlayer(job,creationId,address,position.x,position.y,position.z,
                    station.block.GetBlockName(),actorPlan,fragments,Math.Max(0,seconds),definitions,payment,out prepared))return false;
        }
        else if(!TryPaymentInput(station,recipe.materialBasedRecipe,out var input)||
            !TryPaymentPlan(station,player,recipe,input,batches,tier,out var plan)||
            !RebirthStationGridAdmission.TryCreateMobile(job,creationId,address,position.x,position.y,position.z,
                station.block.GetBlockName(),plan,input,Math.Max(0,seconds),definitions,out prepared))return false;
        Func<bool> current=()=>RebirthStationLiveAccess.TryResolveMobile(player,address,creationId,out var live,out var liveOwner)&&
            ReferenceEquals(live,station)&&ReferenceEquals(liveOwner,owner)&&HasNoPublication(owner,live,job)&&
            recipe.IsUnlocked(player)&&CanUse(live,recipe,player)&&TryMobilePaymentInput(player,live,prepared,out var physical)&&
            prepared.MatchesMobileUnpaidObservation(creationId,address,live.block?.GetBlockName(),physical)&&
            !owner.Progression.StationDiscoveryAdmissions.ContainsKey(prepared.JobId);
        if(!current()||!RebirthStationPreparationReservation.TryRegister(player.world,owner,prepared,
            ()=>current()&&Save(player,owner,prepared)&&current()))return false;
        admission=prepared.Clone();return true;
    }
    internal static bool CanPrepareMobile(EntityPlayer player,RebirthCamperStationAddress address,string creationId,
        Recipe recipe,int batches,int requestedTier)
    {
        if(recipe==null||RebirthStationGridQueue.IsMarked(recipe)||
            !RebirthStationLiveAccess.TryResolveMobile(player,address,creationId,out var station,out _)||
            !recipe.IsUnlocked(player)||!CanUse(station,recipe,player)||
            !TryMobileTier(recipe,player,requestedTier,out var tier))return false;
        var definitions=XUiM_Recipes.GetRecipes();
        if(definitions==null||!definitions.Any(r=>ReferenceEquals(r,recipe)))return false;
        if(UsesMobilePlayerPayment(player,(RebirthCamperStationState)station)&&!recipe.materialBasedRecipe)
            return TryMobilePlayerPlan(player,(RebirthCamperStationState)station,recipe,batches,tier,out _,out _,out _);
        return TryPaymentInput(station,recipe.materialBasedRecipe,out var input)&&TryPaymentPlan(station,player,recipe,input,batches,tier,out _);
    }
    internal static int GetMobileMaximum(EntityPlayer player,RebirthCamperStationAddress address,string creationId,
        Recipe recipe,int requestedTier)
    {
        if(!CanPrepareMobile(player,address,creationId,recipe,1,requestedTier)||
            !RebirthStationLiveAccess.TryResolveMobile(player,address,creationId,out var station,out _)||
            !TryMobileTier(recipe,player,requestedTier,out var tier))return 0;
        if(UsesMobilePlayerPayment(player,(RebirthCamperStationState)station)&&!recipe.materialBasedRecipe)
            return RebirthCamperStationBatchLimit.Find(batches=>TryMobilePlayerPlan(player,(RebirthCamperStationState)station,recipe,batches,tier,out _,out _,out _));
        if(!TryPaymentInput(station,recipe.materialBasedRecipe,out var input))return 0;
        return RebirthCamperStationBatchLimit.Find(batches=>TryPaymentPlan(station,player,recipe,input,batches,tier,out _));
    }
    private static bool TryMobileTier(Recipe recipe,EntityPlayer player,int requested,out int tier)
    {
        tier=0;
        var output=recipe?.GetOutputItemClass();
        if(output==null||player==null)return false;
        int allowed=recipe.GetCraftingTier(player);
        if(!output.HasQuality)return requested==-1||requested==0;
        allowed=Math.Min(XUiM_Recipes.CraftingMaxTier,allowed);
        tier=requested<0?allowed:requested;
        return requested>=-1&&tier>=1&&tier<=allowed&&tier<=6;
    }
    internal static bool TryPrepareDiscovery(EntityPlayer player,Vector3i position,string creationId,Guid job,
        Recipe recipe,int batches,out RebirthStationGridAdmission admission)
        =>TryPrepareCore(player,position,creationId,job,recipe,batches,true,out admission);
    private static bool TryPrepareCore(EntityPlayer player,Vector3i position,string creationId,Guid job,
        Recipe recipe,int batches,bool discoveryIntent,out RebirthStationGridAdmission admission)
    {
        admission=null;
        if(job==Guid.Empty||recipe==null||RebirthStationGridQueue.IsMarked(recipe)||
            !RebirthStationLiveAccess.TryResolve(player,position,creationId,out var station,out var owner)||
            !HasNoPublication(owner,station,job))return false;
        var definitions=XUiM_Recipes.GetRecipes();
        if(definitions==null||!definitions.Any(r=>ReferenceEquals(r,recipe))||!(discoveryIntent?RebirthStationDiscoveryScope.IsUnlockedForDiscovery(recipe,player):recipe.IsUnlocked(player)))return false;
        string key=job.ToString("N");
        var block=station.block;
        if(block==null)return false;
        if(!CanUse(station,recipe,player))return false;
        if(owner.Progression.StationPreparations.ContainsKey(key)&&discoveryIntent!=owner.Progression.StationDiscoveryAdmissions.ContainsKey(key))return false;
        int tier=Math.Max(1,Math.Min(XUiM_Recipes.CraftingMaxTier,recipe.GetCraftingTier(player)));
        float seconds=EffectManager.GetValue(PassiveEffects.CraftingTime,_originalValue:recipe.craftingTime,
            _entity:player,_recipe:recipe,tags:recipe.tags)*XUiM_Recipes.CraftingTimeModifier;
        if(float.IsNaN(seconds)||float.IsInfinity(seconds))return false;
        seconds=Math.Max(0,seconds);
        if(!TryPhysicalInput(station,out var input)||!RebirthStationGridIngredients.TryPlan(player,recipe,input,batches,tier,out var plan)||
            !RebirthStationGridAdmission.TryCreate(job,creationId,position.x,position.y,position.z,
                block.GetBlockName(),plan,input,seconds,definitions,out var prepared))return false;
        // Recheck live custody and exact input after allocation/serialization, before saving intent.
        if(!RebirthStationLiveAccess.TryResolve(player,position,creationId,out var current,out var currentOwner)||
            !ReferenceEquals(current,station)||!ReferenceEquals(currentOwner,owner)||
            !TryPhysicalInput(current,out var currentInput)||
            !RebirthStationGridIngredients.TryBuildDebitedGrid(plan,currentInput,out _))return false;
        if(discoveryIntent)
        {
            if(!RebirthStationDiscoveryFrozenBindingCreator.TryCreate(player,position,creationId,prepared,out var frozen,out var scope)||
                !scope.IsCurrent()||!ReferenceEquals(scope.Owner,owner)||
                !RebirthStationDiscoveryPreparationIntegration.RegisterOriginal(player,owner,prepared,frozen,scope,()=>Save(player,owner,prepared)))return false;
        }
        else if(!RebirthStationPreparationReservation.TryRegister(player.world,owner,prepared,()=>Save(player,owner,prepared)))return false;
        admission=prepared;return true;
    }
}
