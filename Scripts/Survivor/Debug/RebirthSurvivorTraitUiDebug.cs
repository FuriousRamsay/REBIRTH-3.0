using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#nullable disable

/// <summary>
/// Debug-only instrumentation for the Survivor Creator Traits screen.
/// It records what was actually selected and the exact trait-point accounting before/after
/// every toggle so selection failures and Trait-point accounting can be distinguished.
/// </summary>
public static class RebirthSurvivorTraitUiDebug
{
    public sealed class Snapshot
    {
        public int SelectedCount;
        public int PositiveCount;
        public int NegativeCount;
        public int MixedCount;
        public int AptitudeCount;
        public int BasePoints;
        public int BackgroundPoints;
        public int DietPoints;
        public int NegativeRequested;
        public int NegativeApplied;
        public int NegativeCap;
        public int PositiveSpent;
        public int AptitudeSpent;
        public int CalculatedRemaining;
        public int ValidatorRemaining;
        public bool ValidatorAvailable;
        public bool ValidatorValid;
        public string ValidationErrors = string.Empty;
        public string SelectedIds = string.Empty;
    }

    public static Snapshot Capture(RebirthSurvivorCreatorViewModel model)
    {
        Snapshot s = new Snapshot();
        if (model == null) return s;

        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle != null && bundle.Progression != null)
        {
            s.BasePoints = bundle.Progression.BaseCreationPoints;
            s.NegativeCap = bundle.Progression.MaxNegativeTraitRefund;
        }

        RebirthBackgroundDefinition background = model.GetSelectedBackground();
        if (background != null)
        {
            s.BackgroundPoints = background.CreationPointModifier;
            if (bundle != null && bundle.Progression != null &&
                string.Equals(background.Id, RebirthSurvivorIds.BackgroundCleanSlate, StringComparison.OrdinalIgnoreCase))
                s.BackgroundPoints += bundle.Progression.CleanSlateBonus;
        }

        RebirthDietDefinition diet = model.GetSelectedDiet();
        s.DietPoints = diet != null ? diet.Points : 0;

        List<string> ids = new List<string>(model.SelectedTraitIds);
        s.SelectedCount = ids.Count;
        s.SelectedIds = ids.Count > 0 ? string.Join(",", ids.ToArray()) : "<none>";
        for (int i = 0; i < ids.Count; i++)
        {
            RebirthTraitDefinition trait = model.GetTrait(ids[i]);
            if (trait == null) continue;
            if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait))
            {
                s.AptitudeCount++;
                s.AptitudeSpent += trait.Points;
            }
            else if (trait.Polarity == RebirthTraitPolarity.Positive)
            {
                s.PositiveCount++;
                s.PositiveSpent += trait.Points;
            }
            else if (trait.Polarity == RebirthTraitPolarity.Negative)
            {
                s.NegativeCount++;
                s.NegativeRequested += trait.Points;
            }
            else
            {
                s.MixedCount++;
            }
        }

        s.NegativeApplied = RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund(s.NegativeCap, s.NegativeRequested);
        s.CalculatedRemaining = s.BasePoints + s.BackgroundPoints + s.DietPoints + s.NegativeApplied - s.PositiveSpent - s.AptitudeSpent;

        RebirthSurvivorCreationResult result = model.Validation;
        if (result != null)
        {
            s.ValidatorAvailable = true;
            s.ValidatorRemaining = result.RemainingCreationPoints;
            s.ValidatorValid = result.IsValid;
            if (result.Errors != null && result.Errors.Count > 0)
            {
                string[] errors = new string[result.Errors.Count];
                for (int i = 0; i < result.Errors.Count; i++)
                    errors[i] = result.Errors[i] != null ? result.Errors[i].ToString() : "<null>";
                s.ValidationErrors = string.Join("; ", errors);
            }
        }
        return s;
    }

    public static string BuildReport(RebirthSurvivorCreatorViewModel model)
    {
        if (model == null) return "[REBIRTH Survivor][TraitUI] no active Survivor Creator model";
        Snapshot s = Capture(model);
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Survivor][TraitUI] active creator trait accounting");
        b.AppendLine("  step=" + model.Step + " background=" + Safe(model.BackgroundId) + " diet=" + Safe(model.DietId));
        b.AppendLine("  categoryFilter=" + model.TraitCategoryFilter +
            " positiveOffset=" + model.PositiveTraitOffset + "/" + model.GetPositiveTraitCount() +
            " negativeOffset=" + model.NegativeTraitOffset + "/" + model.GetNegativeTraitCount());
        AppendSnapshot(b, "current", s);
        return b.ToString().TrimEnd();
    }

    public static void LogToggle(RebirthSurvivorCreatorViewModel model, RebirthTraitDefinition trait,
        string action, bool success, string reason, Snapshot before)
    {
        if (!RebirthSurvivorDebug.TraitUiLoggingEnabled) return;
        Snapshot after = Capture(model);
        string id = trait != null ? trait.Id : "<unknown>";
        string name = trait != null ? RebirthSurvivorUiText.TraitDisplayName(trait) : id;
        string polarity = trait != null ? trait.Polarity.ToString() : "<unknown>";
        int points = trait != null ? trait.Points : 0;

        Log.Out("[REBIRTH Survivor][TraitUI] toggle action=" + Safe(action) +
            " success=" + success +
            " trait=" + Safe(id) +
            " name='" + Safe(name) + "'" +
            " polarity=" + polarity +
            " points=" + points.ToString(CultureInfo.InvariantCulture) +
            " selected=" + before.SelectedCount.ToString(CultureInfo.InvariantCulture) + "->" + after.SelectedCount.ToString(CultureInfo.InvariantCulture) +
            " remaining=" + before.CalculatedRemaining.ToString(CultureInfo.InvariantCulture) + "->" + after.CalculatedRemaining.ToString(CultureInfo.InvariantCulture) +
            (string.IsNullOrEmpty(reason) ? string.Empty : " reason='" + Safe(reason) + "'"));

        Log.Out("[REBIRTH Survivor][TraitUI] before " + CompactBudget(before));
        Log.Out("[REBIRTH Survivor][TraitUI] after  " + CompactBudget(after));

        if (success)
        {
            int expectedCountDelta = string.Equals(action, "add", StringComparison.OrdinalIgnoreCase) ? 1 :
                (string.Equals(action, "remove", StringComparison.OrdinalIgnoreCase) ? -1 : 0);
            int actualCountDelta = after.SelectedCount - before.SelectedCount;
            if (expectedCountDelta != 0 && actualCountDelta != expectedCountDelta)
                Log.Warning("[REBIRTH Survivor][TraitUI] SELECTION-COUNT-MISMATCH action=" + Safe(action) +
                    " expectedDelta=" + expectedCountDelta + " actualDelta=" + actualCountDelta +
                    " before=" + before.SelectedCount + " after=" + after.SelectedCount + " trait=" + Safe(id));
        }

        if (trait != null && trait.Polarity == RebirthTraitPolarity.Negative && success)
        {
            int rawDelta = after.NegativeRequested - before.NegativeRequested;
            int appliedDelta = after.NegativeApplied - before.NegativeApplied;
            if (RebirthSurvivorTraitPointEconomy.HasRefundCap(after.NegativeCap) && rawDelta != 0 && appliedDelta == 0 && after.NegativeRequested > after.NegativeCap)
            {
                Log.Out("[REBIRTH Survivor][TraitUI] REFUND-CAP trait selection DID change selectedTraits, but available points did not change: " +
                    "negativeRequested=" + after.NegativeRequested.ToString(CultureInfo.InvariantCulture) +
                    " negativeApplied=" + after.NegativeApplied.ToString(CultureInfo.InvariantCulture) +
                    " cap=" + after.NegativeCap.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        if (after.ValidatorAvailable && after.ValidatorRemaining != after.CalculatedRemaining)
            Log.Warning("[REBIRTH Survivor][TraitUI] ACCOUNTING-MISMATCH calculated=" + after.CalculatedRemaining + " validator=" + after.ValidatorRemaining);
        if (after.ValidatorAvailable && !after.ValidatorValid && !string.IsNullOrEmpty(after.ValidationErrors))
            Log.Out("[REBIRTH Survivor][TraitUI] validatorErrors=" + after.ValidationErrors);
        Log.Out("[REBIRTH Survivor][TraitUI] selectedIds=" + after.SelectedIds);
    }

    public static void LogRejected(RebirthSurvivorCreatorViewModel model, string traitId, string reason, Snapshot before)
    {
        if (!RebirthSurvivorDebug.TraitUiLoggingEnabled) return;
        Snapshot after = Capture(model);
        Log.Out("[REBIRTH Survivor][TraitUI] toggle action=reject success=False trait=" + Safe(traitId) +
            " selected=" + before.SelectedCount + "->" + after.SelectedCount +
            " remaining=" + before.CalculatedRemaining + "->" + after.CalculatedRemaining +
            " reason='" + Safe(reason) + "'");
        Log.Out("[REBIRTH Survivor][TraitUI] after  " + CompactBudget(after));
    }

    private static string CompactBudget(Snapshot s)
    {
        return "base=" + Signed(s.BasePoints) +
            " experience=" + Signed(s.BackgroundPoints) +
            " diet=" + Signed(s.DietPoints) +
            " negRequested=+" + s.NegativeRequested.ToString(CultureInfo.InvariantCulture) +
            " negApplied=+" + s.NegativeApplied.ToString(CultureInfo.InvariantCulture) + "/" + (RebirthSurvivorTraitPointEconomy.HasRefundCap(s.NegativeCap) ? s.NegativeCap.ToString(CultureInfo.InvariantCulture) : "unlimited") +
            " positiveSpend=" + s.PositiveSpent.ToString(CultureInfo.InvariantCulture) +
            " aptitudeSpend=" + s.AptitudeSpent.ToString(CultureInfo.InvariantCulture) +
            " remaining=" + s.CalculatedRemaining.ToString(CultureInfo.InvariantCulture) +
            " validator=" + (s.ValidatorAvailable ? s.ValidatorRemaining.ToString(CultureInfo.InvariantCulture) : "<none>") +
            " counts[pos=" + s.PositiveCount + ",neg=" + s.NegativeCount + ",mixed=" + s.MixedCount + ",apt=" + s.AptitudeCount + "]";
    }

    private static void AppendSnapshot(StringBuilder b, string label, Snapshot s)
    {
        b.AppendLine("  " + label + " selected=" + s.SelectedCount +
            " pos=" + s.PositiveCount + " neg=" + s.NegativeCount + " mixed=" + s.MixedCount + " aptitudes=" + s.AptitudeCount);
        b.AppendLine("  budget " + CompactBudget(s));
        if (RebirthSurvivorTraitPointEconomy.HasRefundCap(s.NegativeCap) && s.NegativeRequested > s.NegativeApplied)
            b.AppendLine("  refundCapReached=True excessNegativeRefund=" + (s.NegativeRequested - s.NegativeApplied));
        b.AppendLine("  validator valid=" + s.ValidatorValid +
            (string.IsNullOrEmpty(s.ValidationErrors) ? string.Empty : " errors=" + s.ValidationErrors));
        b.AppendLine("  selectedIds=" + s.SelectedIds);
    }

    private static string Signed(int value)
    {
        return value >= 0 ? "+" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Safe(string value)
    {
        return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("'", "");
    }
}
