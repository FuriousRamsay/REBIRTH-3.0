using System;
using System.Linq;

// Live preparation producer; publication/payment remains a separate durable commit.
public static class RebirthStationPreparationService
{
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
    // Absence here is only a retry preflight, never proof for refund or queue replay.
    private static bool HasNoPublication(RebirthWorldCharacterRecord owner,TileEntityWorkstation station,Guid job)
    {
        string id=job.ToString("N");
        if(owner.Progression.StationPublications.ContainsKey(id)||station.Queue==null)return false;
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
    private static bool CanUse(TileEntityWorkstation station,Recipe recipe,EntityPlayer player)
    {
        // Reading policy is independent of native unlock state and applies again on retained retries.
        if (player == null || recipe == null || !RebirthRecipeDiscoveryRules.Allows(player,recipe.GetName())) return false;
        var block=station.block;if(block==null)return false;
        string areas=block.Properties.Contains("Workstation","CraftingAreaRecipes")
            ?block.Properties.GetString("Workstation","CraftingAreaRecipes"):block.GetBlockName();
        return !string.IsNullOrEmpty(areas)&&areas.Split(',').Any(a=>
            string.Equals(a.Trim()=="player"?string.Empty:a.Trim(),recipe.craftingArea??string.Empty,StringComparison.OrdinalIgnoreCase))&&
            (recipe.craftingToolType==0||station.Tools!=null&&station.Tools.Any(s=>
                s!=null&&!s.IsEmpty()&&s.itemValue.type==recipe.craftingToolType));
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