using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Empty policy preserves existing requirements. Entries refer to actual reading
// knowledge IDs, never to possessing a recipe item or experiment-created markers.
public sealed class RebirthRecipeDiscoveryPolicy
{
    private readonly Dictionary<string,string> requiredRead = new Dictionary<string,string>(StringComparer.Ordinal);
    private readonly HashSet<string> readingKnowledge = new HashSet<string>(StringComparer.Ordinal);
    public bool RequiresReading(string knowledgeId) => knowledgeId != null && readingKnowledge.Contains(knowledgeId);
    // Exact authored recipe requirement. Ungated/unknown recipes do not supply a requirement.
    public bool TryGetRequiredReading(string recipe,out string knowledgeId)
    {
        knowledgeId=string.Empty;
        if(string.IsNullOrEmpty(recipe)||!requiredRead.TryGetValue(recipe,out var required))return false;
        knowledgeId=required;return true;
    }
    public bool Allows(string recipe, Func<string,bool> hasReadKnowledge)
    {
        if (string.IsNullOrEmpty(recipe)) return false;
        string knowledge;
        return !requiredRead.TryGetValue(recipe, out knowledge) || (hasReadKnowledge != null && hasReadKnowledge(knowledge));
    }
    public static RebirthRecipeDiscoveryPolicy Read(XElement root)
    {
        if (root == null || root.Name != "recipeDiscoveryPolicy" || (string)root.Attribute("version") != "1"
            || root.Attributes().Count() != 1) throw new InvalidDataException("Invalid recipe discovery policy.");
        var policy = new RebirthRecipeDiscoveryPolicy();
        foreach (var entry in root.Elements())
        {
            string recipe = (string)entry.Attribute("name"), knowledge = (string)entry.Attribute("requires_read");
            if (entry.Name != "recipe" || entry.HasElements || entry.Attributes().Count() != 2
                || string.IsNullOrWhiteSpace(recipe) || recipe.Length > 256 || string.IsNullOrWhiteSpace(knowledge)
                || knowledge.Length > 256 || policy.requiredRead.ContainsKey(recipe) || policy.requiredRead.Count >= 8192)
                throw new InvalidDataException("Invalid or repeated recipe reading requirement.");
            policy.requiredRead.Add(recipe,knowledge);
            policy.readingKnowledge.Add(knowledge);
        }
        return policy;
    }
}