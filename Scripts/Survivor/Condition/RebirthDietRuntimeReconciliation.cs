using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#nullable disable

/// <summary>
/// Read-only Diet diagnostics for the post-Revision-2 reconciliation pass.
/// Gameplay and creator presentation intentionally share RebirthDietCompatibility and the same
/// loaded ItemClass authoring rather than maintaining a second compatibility list in code.
/// </summary>
public static class RebirthDietRuntimeReconciliation
{
    public static string BuildSummary()
    {
        StringBuilder sb = new StringBuilder();
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null) return "[REBIRTH Survivor Diet] definitions unavailable";
        RebirthDietFoodEntry[] foods = RebirthDietFoodCatalogue.BuildCurrent();
        sb.Append("[REBIRTH Survivor Diet] catalogue=").Append(foods.Length)
          .Append(" contract=DietSatisfaction/Mood-only physicalNutrition=metabolism-owned meaningfulMeal=first-nutrient-absorption");
        for (int i = 0; i < bundle.Diets.Count; i++)
        {
            RebirthDietDefinition diet = bundle.Diets[i];
            int compatible = 0, offDiet = 0, unknown = 0;
            for (int f = 0; f < foods.Length; f++)
            {
                RebirthDietCompatibilityResult r = RebirthDietCompatibility.Evaluate(diet.CompositionRule, foods[f].DietTags);
                if (r.State == RebirthDietCompatibilityState.Compatible) compatible++;
                else if (r.State == RebirthDietCompatibilityState.OffDiet) offDiet++;
                else unknown++;
            }
            sb.Append("\n  ").Append(diet.Id)
              .Append(" points=+").Append(diet.Points)
              .Append(" rule=").Append(diet.CompositionRule)
              .Append(" compatible=").Append(compatible)
              .Append(" offDiet=").Append(offDiet)
              .Append(" unknown=").Append(unknown);
        }
        return sb.ToString();
    }

    public static string BuildDietReport(string idOrName)
    {
        RebirthDietDefinition diet = ResolveDiet(idOrName);
        if (diet == null) return "[REBIRTH Survivor Diet] not found: " + (idOrName ?? string.Empty);
        RebirthDietFoodEntry[] foods = RebirthDietFoodCatalogue.BuildCurrent();
        int compatible = 0, offDiet = 0, unknownComposition = 0, unknownRule = 0;
        List<string> examples = new List<string>();
        for (int i = 0; i < foods.Length; i++)
        {
            RebirthDietFoodEntry food = foods[i];
            RebirthDietCompatibilityResult r = RebirthDietCompatibility.Evaluate(diet.CompositionRule, food.DietTags);
            if (r.State == RebirthDietCompatibilityState.Compatible) compatible++;
            else if (r.State == RebirthDietCompatibilityState.OffDiet)
            {
                offDiet++;
                if (examples.Count < 12) examples.Add(food.ItemId + "(" + r.Reason + ")");
            }
            else if (r.State == RebirthDietCompatibilityState.UnknownComposition) unknownComposition++;
            else unknownRule++;
        }
        return "[REBIRTH Survivor Diet] id=" + diet.Id
            + " points=+" + diet.Points.ToString(CultureInfo.InvariantCulture)
            + " rule=" + diet.CompositionRule
            + " catalogue=" + foods.Length.ToString(CultureInfo.InvariantCulture)
            + " compatible=" + compatible.ToString(CultureInfo.InvariantCulture)
            + " offDiet=" + offDiet.ToString(CultureInfo.InvariantCulture)
            + " unknownComposition=" + unknownComposition.ToString(CultureInfo.InvariantCulture)
            + " unknownRule=" + unknownRule.ToString(CultureInfo.InvariantCulture)
            + "\n  ruleSummary=" + diet.RuleSummary
            + "\n  offDietExamples=" + (examples.Count == 0 ? "<none>" : string.Join(",", examples.ToArray()))
            + "\n  behavior=incompatible food remains edible/nutritive; positive enjoyment suppressed; violation modifier affects Diet Satisfaction/Mood only";
    }

    public static string BuildFoodReport(string itemIdOrName)
    {
        ItemClass item = ResolveFood(itemIdOrName);
        if (item == null) return "[REBIRTH Survivor Diet] food not found in loaded authored meal catalogue: " + (itemIdOrName ?? string.Empty);
        RebirthFoodMoodDefinition food;
        if (!RebirthFoodMoodResolver.TryResolve(item, out food) || food == null)
            return "[REBIRTH Survivor Diet] item is not authored as a meaningful Diet/Mood meal: " + (item.GetItemName() ?? string.Empty);

        StringBuilder sb = new StringBuilder();
        List<string> tags = new List<string>(food.DietTags); tags.Sort(StringComparer.OrdinalIgnoreCase);
        sb.Append("[REBIRTH Survivor Diet] food=").Append(food.SourceItemId)
          .Append(" profile=").Append(food.MoodProfileId)
          .Append(" baseMood=").Append(food.BaseMoodInfluence.ToString("0.##", CultureInfo.InvariantCulture))
          .Append(" family=").Append(food.VarietyFamilyId)
          .Append(" tags=").Append(tags.Count == 0 ? "<none>" : string.Join(",", tags.ToArray()));
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle != null)
        {
            for (int i = 0; i < bundle.Diets.Count; i++)
            {
                RebirthDietDefinition diet = bundle.Diets[i];
                RebirthDietCompatibilityResult r = RebirthDietCompatibility.Evaluate(diet.CompositionRule, food.DietTags);
                sb.Append("\n  ").Append(diet.Id).Append(" state=").Append(r.State)
                  .Append(" modifier=").Append(r.DietModifier.ToString("0.##", CultureInfo.InvariantCulture))
                  .Append(" reason=").Append(r.Reason);
            }
        }
        return sb.ToString();
    }

    private static RebirthDietDefinition ResolveDiet(string idOrName)
    {
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null) return null;
        string q = (idOrName ?? string.Empty).Trim();
        if (q.Length == 0) return null;
        for (int i = 0; i < bundle.Diets.Count; i++)
        {
            RebirthDietDefinition d = bundle.Diets[i];
            if (string.Equals(d.Id, q, StringComparison.OrdinalIgnoreCase)) return d;
            string shortId = d.Id.StartsWith("diet.", StringComparison.OrdinalIgnoreCase) ? d.Id.Substring(5) : d.Id;
            if (string.Equals(shortId, q, StringComparison.OrdinalIgnoreCase)) return d;
            string localized = string.IsNullOrEmpty(d.NameKey) ? string.Empty : Localization.Get(d.NameKey);
            if (!string.IsNullOrEmpty(localized) && string.Equals(localized, q, StringComparison.OrdinalIgnoreCase)) return d;
        }
        return null;
    }

    private static ItemClass ResolveFood(string itemIdOrName)
    {
        if (ItemClass.list == null) return null;
        string q = (itemIdOrName ?? string.Empty).Trim();
        if (q.Length == 0) return null;
        for (int i = 0; i < ItemClass.list.Length; i++)
        {
            ItemClass item = ItemClass.list[i];
            if (item == null) continue;
            string id = item.GetItemName() ?? string.Empty;
            if (string.Equals(id, q, StringComparison.OrdinalIgnoreCase)) return item;
            string localized = id.Length == 0 ? string.Empty : Localization.Get(id);
            if (!string.IsNullOrEmpty(localized) && string.Equals(localized, q, StringComparison.OrdinalIgnoreCase)) return item;
        }
        return null;
    }
}
