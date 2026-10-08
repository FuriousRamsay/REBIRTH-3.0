using System;
using System.Text;

#nullable disable

public static class RebirthMetabolismAuthoringValidator
{
    public static string BuildReport()
    {
        int items = 0;
        int drinks = 0;
        int foods = 0;
        int supplements = 0;
        int errors = 0;
        int warnings = 0;
        StringBuilder detail = new StringBuilder();

        if (ItemClass.list == null)
            return "[REBIRTH Metabolism] validation unavailable: ItemClass.list is not loaded.";

        for (int i = 0; i < ItemClass.list.Length; i++)
        {
            ItemClass ic = ItemClass.list[i];
            if (ic == null) continue;
            if (!RebirthConsumableResolver.HasMetabolismAuthoring(ic)) continue;
            string authoringError;
            if (!RebirthConsumableResolver.TryValidateAuthoring(ic, out authoringError))
            {
                AddError(detail, ref errors, ic.GetItemName(), authoringError);
                continue;
            }
            RebirthConsumableDefinition d;
            if (!RebirthConsumableResolver.TryResolve(ic, out d) || d == null)
            {
                AddError(detail, ref errors, ic.GetItemName(), "validated metabolism item could not be resolved");
                continue;
            }
            items++;
            if (d.IsDrink) drinks++;
            if (d.IsFood) foods++;
            if (d.IsSupplement) supplements++;

            if (d.IsDrink)
            {
                if (d.ContainerCapacityMl <= 0f) AddError(detail, ref errors, d.ItemName, "drink capacity must be > 0");
                if (d.InitialVolumeMl < 0f || d.InitialVolumeMl > d.ContainerCapacityMl + .01f) AddError(detail, ref errors, d.ItemName, "initial volume is outside container capacity");
                if (d.ManualSipMl <= 0f) AddError(detail, ref errors, d.ItemName, "manual sip must be > 0");
                if (ic.Stacknumber.Value < 1 || ic.Stacknumber.Value > 10) AddError(detail, ref errors, d.ItemName, "stateful liquid Stacknumber must be between 1 and 10");
                if (d.HydrationEquippable && d.AutoSipMl <= 0f) AddError(detail, ref errors, d.ItemName, "hydration-slot drink requires AutoSipMl");
                if (!d.ReusableContainer && string.IsNullOrEmpty(d.EmptyItem)) AddWarning(detail, ref warnings, d.ItemName, "non-reusable drink has no empty-item transition");
            }
            if (d.IsFood)
            {
                if (d.StomachVolumeMl + d.FoodWaterMl <= 0f) AddError(detail, ref errors, d.ItemName, "food has no stomach volume");
                if (d.NutritionUnits <= 0f) AddWarning(detail, ref warnings, d.ItemName, "food provides no nutrition");
            }
        }

        StringBuilder result = new StringBuilder();
        result.Append("[REBIRTH Metabolism] validation items=").Append(items)
              .Append(" drinks=").Append(drinks)
              .Append(" foods=").Append(foods)
              .Append(" supplements=").Append(supplements)
              .Append(" errors=").Append(errors)
              .Append(" warnings=").Append(warnings);
        if (detail.Length > 0) result.AppendLine().Append(detail);
        return result.ToString();
    }

    private static void AddError(StringBuilder sb, ref int count, string item, string message)
    {
        count++;
        sb.Append("ERROR ").Append(item).Append(": ").Append(message).AppendLine();
    }

    private static void AddWarning(StringBuilder sb, ref int count, string item, string message)
    {
        count++;
        sb.Append("WARN ").Append(item).Append(": ").Append(message).AppendLine();
    }
}
