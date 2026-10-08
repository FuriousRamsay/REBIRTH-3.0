using System;
using System.Collections.Generic;

#nullable disable

public enum RebirthDietCompatibilityState
{
    Compatible = 0,
    OffDiet = 1,
    UnknownComposition = 2,
    UnknownRule = 3
}

public sealed class RebirthDietCompatibilityResult
{
    public RebirthDietCompatibilityState State = RebirthDietCompatibilityState.UnknownRule;
    public string Rule = string.Empty;
    public string Reason = string.Empty;
    public float DietModifier;
    public bool Compatible { get { return State == RebirthDietCompatibilityState.Compatible; } }
}

/// <summary>
/// Single authoritative interpretation of Survivor Diet composition rules.
/// Food authoring owns composition through RebirthDietTags; this class never switches on item names.
/// Restrictive diets fail closed for missing composition evidence without inventing a Mood penalty.
/// </summary>
public static class RebirthDietCompatibility
{
    private static readonly HashSet<string> CompositionTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Plant", "Meat", "Fish", "Egg", "Dairy", "Honey", "AnimalFat", "AnimalProduct"
    };

    private static readonly HashSet<string> AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Plant", "Meat", "Fish", "Egg", "Dairy", "Honey", "AnimalFat", "AnimalProduct", "Sweet"
    };

    public static bool IsKnownTag(string tag)
    {
        return !string.IsNullOrWhiteSpace(tag) && AllowedTags.Contains(tag.Trim());
    }

    public static bool HasCompositionEvidence(IEnumerable<string> tags)
    {
        if (tags == null) return false;
        foreach (string raw in tags)
        {
            string tag = (raw ?? string.Empty).Trim();
            if (tag.Length > 0 && CompositionTags.Contains(tag)) return true;
        }
        return false;
    }

    public static RebirthDietCompatibilityResult Evaluate(string compositionRule, IEnumerable<string> tags)
    {
        RebirthDietCompatibilityResult result = new RebirthDietCompatibilityResult();
        string rule = (compositionRule ?? string.Empty).Trim().ToLowerInvariant();
        result.Rule = rule;

        HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tags != null)
        {
            foreach (string raw in tags)
            {
                string tag = (raw ?? string.Empty).Trim();
                if (tag.Length > 0) set.Add(tag);
            }
        }

        if (rule == "unrestricted")
        {
            result.State = RebirthDietCompatibilityState.Compatible;
            result.Reason = "unrestricted";
            return result;
        }

        if (rule != "vegetarian" && rule != "pescatarian" && rule != "vegan" && rule != "carnivore")
        {
            result.State = RebirthDietCompatibilityState.UnknownRule;
            result.Reason = "unknown-diet-rule";
            return result;
        }

        if (!HasCompositionEvidence(set))
        {
            result.State = RebirthDietCompatibilityState.UnknownComposition;
            result.Reason = "missing-composition-evidence";
            return result;
        }


        if (rule == "pescatarian")
        {
            if (set.Contains("Meat") || set.Contains("AnimalFat"))
            {
                result.State = RebirthDietCompatibilityState.OffDiet;
                // Preserve the existing tuning field while the live display/rule moves from Vegetarian to Pescatarian.
                result.DietModifier = RebirthConditionRuntimeConfig.VegetarianViolation;
                result.Reason = "contains-meat-or-rendered-animal-fat";
                return result;
            }
            result.State = RebirthDietCompatibilityState.Compatible;
            result.Reason = "pescatarian-compatible";
            return result;
        }

        if (rule == "vegetarian")
        {
            if (set.Contains("Meat") || set.Contains("Fish") || set.Contains("AnimalFat"))
            {
                result.State = RebirthDietCompatibilityState.OffDiet;
                result.DietModifier = RebirthConditionRuntimeConfig.VegetarianViolation;
                result.Reason = "contains-meat-fish-or-rendered-animal-fat";
                return result;
            }
            result.State = RebirthDietCompatibilityState.Compatible;
            result.Reason = "vegetarian-compatible";
            return result;
        }

        if (rule == "vegan")
        {
            if (set.Contains("Meat") || set.Contains("Fish") || set.Contains("Egg") || set.Contains("Dairy") ||
                set.Contains("Honey") || set.Contains("AnimalFat") || set.Contains("AnimalProduct"))
            {
                result.State = RebirthDietCompatibilityState.OffDiet;
                result.DietModifier = RebirthConditionRuntimeConfig.VeganViolation;
                result.Reason = "contains-animal-derived-ingredient";
                return result;
            }
            result.State = RebirthDietCompatibilityState.Compatible;
            result.Reason = "vegan-compatible";
            return result;
        }

        bool animal = set.Contains("Meat") || set.Contains("Fish") || set.Contains("Egg") || set.Contains("Dairy") ||
            set.Contains("Honey") || set.Contains("AnimalFat") || set.Contains("AnimalProduct");
        if (set.Contains("Plant") || !animal)
        {
            result.State = RebirthDietCompatibilityState.OffDiet;
            result.DietModifier = RebirthConditionRuntimeConfig.CarnivoreViolation;
            result.Reason = set.Contains("Plant") ? "contains-plant-ingredient" : "contains-no-animal-origin-ingredient";
            return result;
        }

        result.State = RebirthDietCompatibilityState.Compatible;
        result.Reason = "carnivore-compatible";
        return result;
    }
}
