using System;
using System.Xml.Linq;

// Detached original binding producer. Does not stage/save/debit/publish or grant knowledge.
internal static class RebirthStationDiscoveryFrozenBindingCreator
{
    internal static bool TryCreate(EntityPlayer player,Vector3i position,string creation,RebirthStationGridAdmission admission,
        out RebirthStationDiscoveryAdmissionBinding binding,out RebirthStationDiscoveryAuthorityScope scope)
    {
        binding=null;scope=null;
        try
        {
            if(admission==null||admission.IsPublicationAttempted||admission.CreationId!=creation||
                !RebirthStationDiscoveryAuthorityScope.TryCapture(player,position,creation,out var original))return false;
            var image=admission.Write();
            if((int)image.Attribute("x")!=position.x||(int)image.Attribute("y")!=position.y||(int)image.Attribute("z")!=position.z||
                (string)image.Attribute("block")!=original.Station.block?.GetBlockName())return false;
            var definitions=XUiM_Recipes.GetRecipes();
            if(!original.IsCurrent()||!admission.TryMaterialize(definitions,out var queued,out _,out _)||
                !RebirthStationDiscoveryCanonicalRecipe.TryResolve(queued,definitions,out var resolved)||
                resolved.JobId!=admission.JobId||resolved.DefinitionId!=admission.DefinitionId||
                !RebirthStationGridQueue.TryGetAdmittedSource(queued,definitions,out var source)||
                !RebirthStationDiscoveryScope.IsUnlockedForDiscovery(source,player)||
                !RebirthRecipeDiscoveryRules.Allows(player,resolved.CanonicalRecipe)||
                RebirthCapabilityService.EvaluateRecipeForDiscovery(player,resolved.CanonicalRecipe)?.IsAllowed!=true||
                !original.IsCurrent()||
                !RebirthStationDiscoveryWitness.TryCreate(admission,original.OwnerKey,original.SaveDigest,original.PolicyDigest,resolved.KnowledgeId,out var witness)||
                !RebirthStationDiscoveryAdmissionBinding.TryCreateDetached(resolved.CanonicalRecipe,witness,admission,out var candidate)||
                !candidate.TryValidateAuthority(resolved.CanonicalRecipe,witness,admission,resolved.Matches)||
                !original.IsCurrent())return false;
            binding=candidate;scope=original;return true;
        }
        catch{return false;}
    }
}