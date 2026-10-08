using System;
using System.Xml.Linq;

// Bound paid discovery jobs ONLY. false means use the existing normal recipe gate, not permission.
// No UI lock, knowledge grant, queue mutation, or client authority substitution.
internal static class RebirthStationQueuedDiscoveryAuthorization
{
    internal static bool TryEvaluate(TileEntityWorkstation station,RecipeQueueItem queued,EntityPlayer player,
        out bool allowed,out string reason)
    {
        allowed=false;reason=null;
        if(queued?.Recipe==null||!RebirthStationGridQueue.IsMarked(queued.Recipe)||RebirthCookingBatch.IsBatch(queued.Recipe))return false;
        try
        {
            if(player==null||!RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null||
                !RebirthStationGridQueue.TryGetJobId(queued.Recipe,out var job)||
                !owner.Progression.StationDiscoveryAdmissions.TryGetValue(job,out var binding))return false;
            // Presence of original discovery custody makes every validation failure a hold, never a normal-gate fallback.
            reason="station-discovery-queued-authority";
            if(binding==null||!RebirthStationCompletionOriginalScope.TryCapture(station,queued,out var original)||
                !ReferenceEquals(original.Player,player)||!ReferenceEquals(original.Owner,owner)||
                !RebirthStationDiscoveryPreparationPair.Matches(binding,original.Admission))return true;
            var bindingImage=binding.Write();
            if(!RebirthStationGridAdmission.TryReadStored(bindingImage.Element("stationAdmission"),out var frozen)||
                !RebirthWorldCharacterRepository.HasSavedStationDiscoveryAdmission(original.Identity,original.Admission,binding)||!original.IsCurrent())return true;
            string policy=RebirthCraftingProgressionRegistry.SemanticHash;
            var definitions=XUiM_Recipes.GetRecipes();
            if(!original.IsCurrent()||!RebirthStationDiscoveryCanonicalRecipe.TryResolve(queued.Recipe,definitions,out var resolved)||
                resolved.JobId!=job||resolved.DefinitionId!=original.Admission.DefinitionId||
                !resolved.Matches(binding.CanonicalRecipe,frozen.DefinitionId)||
                !RebirthStationGridQueue.TryGetAdmittedSource(queued.Recipe,definitions,out var source)||
                !Guid.TryParse(original.WorldGuid,out var worldId)||worldId==Guid.Empty)return true;
            string root=RebirthStationDiscoveryAuthorityScope.Root();
            if(root==null||!RebirthStationDiscoveryWitness.TryRead(bindingImage.Element("stationDiscoveryWitness"),frozen,
                original.Identity.StorageKey,RebirthStationDiscoveryAuthorityScope.Digest(root,worldId),policy,resolved.KnowledgeId,out var witness)||
                !binding.TryValidateAuthority(resolved.CanonicalRecipe,witness,frozen,resolved.Matches)||
                !RebirthStationDiscoveryScope.IsUnlockedForDiscovery(source,player)||
                !RebirthRecipeDiscoveryRules.Allows(player,resolved.CanonicalRecipe)||!original.IsCurrent())return true;
            var evaluation=RebirthCapabilityService.EvaluateRecipeForDiscovery(player,resolved.CanonicalRecipe);
            if(evaluation?.IsAllowed!=true){reason="station-discovery-current-capability:"+(evaluation?.FirstMissingReason??"unavailable");return true;}
            // Policy/readback/native evaluation can invoke code: revalidate original owners and exact saved pair after it.
            if(!original.IsCurrent()||RebirthCraftingProgressionRegistry.SemanticHash!=policy||
                !owner.Progression.StationDiscoveryAdmissions.TryGetValue(job,out var retained)||retained==null||
                !XNode.DeepEquals(retained.Write(),bindingImage)||
                !RebirthWorldCharacterRepository.HasSavedStationDiscoveryAdmission(original.Identity,original.Admission,retained)||
                !original.IsCurrent()||!RebirthStationDiscoveryCanonicalRecipe.TryResolve(queued.Recipe,definitions,out var final)||
                final.JobId!=resolved.JobId||final.KnowledgeId!=resolved.KnowledgeId||!final.Matches(resolved.CanonicalRecipe,resolved.DefinitionId)||
                RebirthCraftingProgressionRegistry.SemanticHash!=policy||!original.IsCurrent())return true;
            allowed=true;reason=null;return true;
        }
        catch{reason="station-discovery-queued-validation-failed";return true;}
    }
}
