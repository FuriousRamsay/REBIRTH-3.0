using System;
using System.Collections.Generic;
using System.Linq;

// Canonical WHAT and exact current paid-definition binding only; not payment/completion/grant authority.
internal static class RebirthStationDiscoveryCanonicalRecipe
{
    internal sealed class Resolution
    {
        internal readonly string CanonicalRecipe,DefinitionId,JobId,KnowledgeId;
        internal readonly int NativeOutputType;
        internal Resolution(string recipe,string definition,string job,string knowledge,int output)
        {CanonicalRecipe=recipe;DefinitionId=definition;JobId=job;KnowledgeId=knowledge;NativeOutputType=output;}
        internal bool Matches(string canonical,string definition)
            =>string.Equals(canonical,CanonicalRecipe,StringComparison.Ordinal)&&string.Equals(definition,DefinitionId,StringComparison.Ordinal);
    }
    internal static bool TryResolve(Recipe queued,IList<Recipe> currentNativeDefinitions,out Resolution resolution)
    {
        resolution=null;
        try
        {
            if(!RebirthCraftingProgressionRegistry.IsReady||
                !RebirthStationGridQueue.TryGetJobId(queued,out var job)||
                !RebirthStationGridQueue.TryGetAdmittedSource(queued,currentNativeDefinitions,out var source)||source==null||
                !RebirthStationGridQueue.TryGetDefinitionBinding(queued,currentNativeDefinitions,out var binding)||binding==null)return false;
            var native=ItemClass.GetForId(source.itemValueType);
            string canonical=native?.GetItemName();
            if(string.IsNullOrEmpty(canonical)||canonical.Length>1024||canonical.Any(c=>char.IsWhiteSpace(c)||char.IsControl(c))||
                !string.Equals(source.GetName(),canonical,StringComparison.Ordinal)||
                !string.Equals(binding.RecipeName,canonical,StringComparison.Ordinal)||
                !RebirthCraftingProgressionRegistry.TryGetRecipe(canonical,out var policy)||policy==null||!policy.IsGated||!policy.HasLiveCapability||
                !string.Equals(policy.RecipeId,canonical,StringComparison.Ordinal)||
                !RebirthProgressionRuntimeConfig.TryGetRecipeRule(canonical,out var rule)||rule==null||
                !string.Equals(rule.RecipeName,canonical,StringComparison.OrdinalIgnoreCase)||string.IsNullOrEmpty(rule.KnowledgeId)||
                policy.KnowledgeIds==null||!policy.KnowledgeIds.Contains(rule.KnowledgeId,StringComparer.Ordinal))return false;
            if(RebirthRecipeDiscoveryRules.TryGetRequiredReading(canonical,out var reading))
            {if(!string.Equals(reading,rule.KnowledgeId,StringComparison.Ordinal))return false;}
            else if(!RebirthRecipeDiscoveryRules.Allows(null,canonical))return false; // unavailable policy must not look ungated
            // Recheck exact catalogue resolution after policy lookups; these never authorize owner/custody.
            if(!RebirthStationGridQueue.TryGetAdmittedSource(queued,currentNativeDefinitions,out var finalSource)||
                !ReferenceEquals(source,finalSource)||
                !RebirthStationGridQueue.TryGetDefinitionBinding(queued,currentNativeDefinitions,out var finalBinding)||
                finalBinding==null||finalBinding.DefinitionId!=binding.DefinitionId||finalBinding.RecipeName!=canonical||
                !RebirthStationGridQueue.TryGetJobId(queued,out var finalJob)||finalJob!=job)return false;
            resolution=new Resolution(canonical,binding.DefinitionId,job,rule.KnowledgeId,source.itemValueType);return true;
        }
        catch{return false;}
    }
}