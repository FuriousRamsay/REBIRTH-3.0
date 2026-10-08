using System;
using System.Xml.Linq;

internal static class RebirthStationDiscoveryPreparationPair
{
    internal static bool Matches(RebirthStationDiscoveryAdmissionBinding binding,RebirthStationGridAdmission current)
    {
        try
        {
            if(binding==null||current==null||binding.JobId!=current.JobId||
                !RebirthStationDiscoveryAdmissionBinding.TryReadStored(binding.Write(),out _)||
                !RebirthStationGridAdmission.TryReadStored(binding.Write().Element("stationAdmission"),out var original)||original.IsPublicationAttempted)return false;
            if(current.IsPublicationAttempted)
            {if(!original.TryMarkPublicationAttempted(out var attempted))return false;return XNode.DeepEquals(attempted.Write(),current.Write());}
            return XNode.DeepEquals(original.Write(),current.Write());
        }
        catch{return false;}
    }
    internal static Vector3i Position(RebirthStationGridAdmission current)
    {var node=current.Write();return new Vector3i((int)node.Attribute("x"),(int)node.Attribute("y"),(int)node.Attribute("z"));}
    internal static bool TryValidateLive(EntityPlayer player,RebirthStationGridAdmission current,
        RebirthStationDiscoveryAdmissionBinding binding,out RebirthStationDiscoveryAuthorityScope scope)
    {
        scope=null;
        try
        {
            if(!Matches(binding,current)||!RebirthStationDiscoveryAuthorityScope.TryCapture(player,Position(current),current.CreationId,out var observed)||
                !RebirthStationGridAdmission.TryReadStored(binding.Write().Element("stationAdmission"),out var original))return false;
            if((string)original.Write().Attribute("block")!=observed.Station.block?.GetBlockName())return false;
            var node=binding.Write();var definitions=XUiM_Recipes.GetRecipes();
            if(!original.TryMaterialize(definitions,out var queued,out _,out _)||
                !RebirthStationDiscoveryCanonicalRecipe.TryResolve(queued,definitions,out var resolved)||
                !resolved.Matches(binding.CanonicalRecipe,original.DefinitionId)||resolved.JobId!=original.JobId||
                !RebirthStationDiscoveryWitness.TryRead(node.Element("stationDiscoveryWitness"),original,observed.OwnerKey,
                    observed.SaveDigest,observed.PolicyDigest,resolved.KnowledgeId,out _)||
                !RebirthStationGridQueue.TryGetAdmittedSource(queued,definitions,out var source)||
                !RebirthStationDiscoveryScope.IsUnlockedForDiscovery(source,player)||
                !RebirthRecipeDiscoveryRules.Allows(player,binding.CanonicalRecipe)||
                RebirthCapabilityService.EvaluateRecipeForDiscovery(player,binding.CanonicalRecipe)?.IsAllowed!=true||!observed.IsCurrent())return false;
            scope=observed;return true;
        }
        catch{return false;}
    }
}