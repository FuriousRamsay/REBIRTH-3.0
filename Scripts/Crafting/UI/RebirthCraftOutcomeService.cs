using System;
using System.Collections.Generic;
using System.Globalization;

#nullable disable

/// <summary>
/// Read-only presentation contract for the redesigned crafting screens.
/// Every displayed value comes from the selected Recipe, native crafting-tier logic,
/// or the existing REBIRTH Capability/Knowledge system. This service never rolls,
/// mutates or authorizes a craft result. Crafting XP is deliberately shown as the
/// recipe base XP per craft because the native runtime can apply repeat-craft diminishing XP.
/// </summary>
public static class RebirthCraftOutcomeService
{
    public sealed class Snapshot
    {
        public string SuccessChance = "—";
        public string QualityRange = "—";
        public string CraftingXp = "—";
        public string PossibleResults = "—";
        public string HighQuality = "N/A";
        public string NormalQuality = "N/A";
        public string LowQuality = "N/A";
        public string Knowledge = "—";
        public string KnowledgeColor = "210,210,210,255";
        public string TimeLabel = "CRAFT TIME";
    }

    public static Snapshot Build(EntityPlayer player, Recipe recipe, int craftCount, int selectedCraftingTier)
    {
        Snapshot result = new Snapshot();
        if (recipe == null) return result;

        int batches = Math.Max(1, craftCount);
        result.SuccessChance = "100%"; // Audited runtime has no approved random craft-failure roll.
        result.QualityRange = BuildQualityRange(recipe, selectedCraftingTier);
        result.CraftingXp = Math.Max(0, recipe.craftExpGain).ToString(CultureInfo.InvariantCulture) + " / craft";
        result.PossibleResults = BuildPossibleResults(recipe, batches);
        result.Knowledge = BuildKnowledgeSummary(player, recipe, out result.KnowledgeColor);
        result.TimeLabel = string.Equals(RebirthServiceCraftSkillService.ClassifyRecipe(recipe), "skill.cooking", StringComparison.OrdinalIgnoreCase)
            ? Localize("xuiRebirthCookTime", "COOK TIME")
            : Localize("xuiRebirthCraftTime", "CRAFT TIME");

        // High/Normal/Low are intentionally not fabricated. Native quality is deterministic
        // at the selected crafting tier and current REBIRTH prepared-dish outputs are distinct IDs.
        result.HighQuality = Localize("xuiRebirthNotApplicable", "N/A");
        result.NormalQuality = Localize("xuiRebirthNotApplicable", "N/A");
        result.LowQuality = Localize("xuiRebirthNotApplicable", "N/A");
        return result;
    }

    private static string BuildQualityRange(Recipe recipe, int selectedCraftingTier)
    {
        ItemClass output = null;
        try { output = recipe.GetOutputItemClass(); } catch { }
        if (output != null && output.ShowQualityBar)
            return Localize("xuiRebirthQualityTier", "Tier") + " " + Math.Max(1, selectedCraftingTier).ToString(CultureInfo.InvariantCulture);

        string name = SafeRecipeName(recipe);
        if (IsWellPreparedRecipe(name)) return Localize("xuiRebirthWellPrepared", "Well-Prepared");
        return Localize("xuiRebirthStandardQuality", "Standard");
    }

    private static string BuildPossibleResults(Recipe recipe, int craftCount)
    {
        string name = string.Empty;
        try
        {
            ItemClass output = recipe.GetOutputItemClass();
            if (output != null) name = output.GetLocalizedItemName();
        }
        catch { }
        if (string.IsNullOrWhiteSpace(name))
        {
            string key = SafeRecipeName(recipe);
            string localized = Localization.Get(key);
            name = string.IsNullOrWhiteSpace(localized) || string.Equals(localized, key, StringComparison.Ordinal) ? key : localized;
        }
        long count = (long)Math.Max(1, recipe.count) * Math.Max(1, craftCount);
        return string.IsNullOrWhiteSpace(name) ? "—" : name + "  x" + count.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildKnowledgeSummary(EntityPlayer player, Recipe recipe, out string color)
    {
        color = "210,210,210,255";
        if (player == null || recipe == null) return "—";
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return Localize("xuiRebirthNativeRecipeRules", "Native recipe rules");

        string recipeName = SafeRecipeName(recipe);

        RebirthCapabilityEvaluation evaluation = RebirthCapabilityService.EvaluateRecipe(player, recipeName);
        if (evaluation == null || (evaluation.IsAllowed && evaluation.Requirements.Count == 0))
        {
            color = "175,175,182,255";
            return Localize("xuiRebirthNoKnowledgeRequirement", "No REBIRTH Knowledge requirement");
        }

        List<string> parts = new List<string>();
        HashSet<string> shownSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < evaluation.Requirements.Count; i++)
        {
            RebirthCapabilityRequirementEvaluation requirement = evaluation.Requirements[i];
            if (requirement == null) continue;
            if (requirement.WarningOnly)
            {
                continue;
            }
            if (string.Equals(requirement.Kind, RebirthCapabilityKinds.Knowledge, StringComparison.OrdinalIgnoreCase))
            {
                string display = RebirthKnowledgeService.GetDisplayName(requirement.Id);
                parts.Add(RequirementMarker(requirement.Allowed) + "Recipe: " + display.Replace("Recipe: ", ""));
            }
            else if (requirement.Kind == RebirthCapabilityKinds.Discipline)
                parts.Add(RequirementMarker(requirement.Allowed) + requirement.Message);
            else if (string.Equals(requirement.Kind, RebirthCapabilityKinds.Skill, StringComparison.OrdinalIgnoreCase) && shownSkills.Add(requirement.Id ?? string.Empty))
            {
                parts.Add(RequirementMarker(requirement.Allowed) + SkillName(requirement.Id) + ": Requires "
                    + requirement.RequiredValue.ToString("0.##", CultureInfo.InvariantCulture)
                    + "  (You: " + requirement.CurrentValue.ToString("0.#", CultureInfo.InvariantCulture) + ")");
            }
        }

        if (parts.Count == 0)
            parts.Add(evaluation.IsAllowed ? Localize("xuiRebirthRequirementMet", "Requirement met") : evaluation.FirstMissingReason);
        color = "230,230,235,255";
        return string.Join("\n", parts.ToArray());
    }

    // Only the marker is colored; the requirement remains readable in neutral text.
    private static string RequirementMarker(bool met) => met ? "[8FD18F]\u2713[-] " : "[CC6B64]X[-] ";

    private static string SkillName(string id)
    {
        RebirthSkillDefinition def;
        if (RebirthSurvivorDefinitionRegistry.TryGetSkill(id, out def) && def != null)
        {
            string localized = Localization.Get(def.NameKey);
            if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, def.NameKey, StringComparison.Ordinal)) return localized;
        }
        return id ?? string.Empty;
    }

    public static bool IsWellPreparedRecipe(string recipeName)
    {
        return string.Equals(recipeName, "rebirthFoodMushroomSkilletPrepared", StringComparison.OrdinalIgnoreCase)
            || string.Equals(recipeName, "rebirthFoodPumpkinVegetableSoupPrepared", StringComparison.OrdinalIgnoreCase)
            || string.Equals(recipeName, "rebirthFoodBlueberryCrumblePrepared", StringComparison.OrdinalIgnoreCase)
            || string.Equals(recipeName, "rebirthFoodMushroomPotPiePrepared", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeRecipeName(Recipe recipe)
    {
        try { return recipe != null ? (recipe.GetName() ?? string.Empty) : string.Empty; }
        catch { return string.Empty; }
    }

    private static string Localize(string key, string fallback)
    {
        string value = Localization.Get(key ?? string.Empty);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
