using System;partial class RebirthRecipeCapabilityIntegration{internal static bool AuthorizeActiveQueue(TileEntityWorkstation workstation)
    {
        if(workstation==null)return true;
        if(!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld()){HeldQueues.Remove(workstation);return true;}
        RecipeQueueItem[] queue=workstation.Queue;
        if(queue==null||queue.Length==0){HeldQueues.Remove(workstation);return true;}
        RecipeQueueItem item=queue[queue.Length-1];
        if(item==null||item.Recipe==null||item.Multiplier<=0){HeldQueues.Remove(workstation);return true;}
        if(!RebirthStationGridQueue.HasValidMultiplier(item.Recipe,item.Multiplier))
        {HoldActiveQueue(workstation,item,item.Recipe.GetName(),"station-batch-multiplier-invalid");return false;}
        if(!RebirthStationGridQueue.HasResolvedDefinition(item.Recipe))
        {HoldActiveQueue(workstation,item,item.Recipe.GetName(),"station-definition-unresolved");return false;}
        // Cooking cards are guidance. A concrete ingredient-grid batch does not require the card.
        if(RebirthCookingBatch.IsBatch(item.Recipe)){HeldQueues.Remove(workstation);return true;}
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        string recipeName=item.Recipe.GetName();
        EntityPlayer player=world!=null?world.GetEntity(item.StartingEntityId) as EntityPlayer:null;
        if(player==null){HoldActiveQueue(workstation,item,recipeName,"crafter-unavailable");return false;}
        if(RebirthStationQueuedDiscoveryAuthorization.TryEvaluate(workstation,item,player,out var discoveryAllowed,out var discoveryReason))
        {
            if(discoveryAllowed){HeldQueues.Remove(workstation);return true;}
            HoldActiveQueue(workstation,item,recipeName,discoveryReason??"station-discovery-queued-authority");return false;
        }
        RebirthCapabilityEvaluation evaluation=RebirthCapabilityService.EvaluateRecipe(player,recipeName);
        if(evaluation.IsAllowed){HeldQueues.Remove(workstation);return true;}
        HoldActiveQueue(workstation,item,recipeName,"crafting-policy:"+evaluation.FirstMissingReason);
        return false;
    }}