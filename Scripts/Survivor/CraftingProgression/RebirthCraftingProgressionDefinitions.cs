using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public static class RebirthCraftingProgressionPolicies
{
    public const string Gated = "gated";
    public const string Universal = "universal";
    public const string Disabled = "disabled";

    public static bool IsKnown(string value)
    {
        return string.Equals(value,Gated,StringComparison.OrdinalIgnoreCase)
            || string.Equals(value,Universal,StringComparison.OrdinalIgnoreCase)
            || string.Equals(value,Disabled,StringComparison.OrdinalIgnoreCase);
    }

    public static bool RequiresServerAuthorization(string value)
    {
        return string.Equals(value,Gated,StringComparison.OrdinalIgnoreCase)
            || string.Equals(value,Disabled,StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class RebirthCraftingProgressionDefinition
{
    public string RecipeId { get; private set; }
    public string Policy { get; private set; }
    public string CapabilityId { get; private set; }
    public string PrimarySkillId { get; private set; }
    public string Family { get; private set; }
    public string ImprovisationCategory { get; private set; }
    public string Implementation { get; private set; }
    public string Source { get; private set; }
    public string SourceStatus { get; private set; }
    public string DecisionStatus { get; private set; }
    public ReadOnlyCollection<string> KnowledgeIds { get; private set; }

    public bool IsGated { get { return string.Equals(Policy,RebirthCraftingProgressionPolicies.Gated,StringComparison.OrdinalIgnoreCase); } }
    public bool IsUniversal { get { return string.Equals(Policy,RebirthCraftingProgressionPolicies.Universal,StringComparison.OrdinalIgnoreCase); } }
    public bool IsDisabled { get { return string.Equals(Policy,RebirthCraftingProgressionPolicies.Disabled,StringComparison.OrdinalIgnoreCase); } }
    public bool HasLiveCapability { get { return string.Equals(Implementation,"existing_capability",StringComparison.OrdinalIgnoreCase); } }
    public bool HasPlannedCapability { get { return string.Equals(Implementation,"planned_capability",StringComparison.OrdinalIgnoreCase); } }

    public RebirthCraftingProgressionDefinition(
        string recipeId,
        string policy,
        string capabilityId,
        string primarySkillId,
        string family,
        string implementation,
        string source,
        string sourceStatus,
        string decisionStatus,
        IEnumerable<string> knowledgeIds,
        string improvisationCategory = null)
    {
        RecipeId=(recipeId??string.Empty).Trim();
        Policy=(policy??string.Empty).Trim().ToLowerInvariant();
        CapabilityId=(capabilityId??string.Empty).Trim();
        PrimarySkillId=(primarySkillId??string.Empty).Trim();
        Family=(family??string.Empty).Trim();
        ImprovisationCategory=improvisationCategory??string.Empty;
        Implementation=(implementation??string.Empty).Trim().ToLowerInvariant();
        Source=(source??string.Empty).Trim();
        SourceStatus=(sourceStatus??string.Empty).Trim();
        DecisionStatus=(decisionStatus??string.Empty).Trim();
        List<string> values=new List<string>();
        if(knowledgeIds!=null)
            foreach(string value in knowledgeIds)
                if(!string.IsNullOrWhiteSpace(value)) values.Add(value.Trim());
        KnowledgeIds=new ReadOnlyCollection<string>(values);
    }
}
