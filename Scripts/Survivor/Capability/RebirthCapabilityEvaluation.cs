using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public sealed class RebirthCapabilityRequirementEvaluation
{
    public string Kind;
    public string Id;
    public bool Allowed;
    public bool WarningOnly;
    public float CurrentValue;
    public float RequiredValue;
    public float RecommendedValue;
    public string Message;
}

public sealed class RebirthCapabilityEvaluation
{
    public string CapabilityId { get; private set; }
    public string TargetType { get; private set; }
    public string TargetId { get; private set; }
    public bool IsAllowed { get; private set; }
    public string DisplayState { get; private set; }
    public ReadOnlyCollection<RebirthCapabilityRequirementEvaluation> Requirements { get; private set; }
    public ReadOnlyCollection<RebirthCapabilityRequirementEvaluation> MissingHardRequirements { get; private set; }
    public ReadOnlyCollection<RebirthCapabilityRequirementEvaluation> Warnings { get; private set; }
    public float ClosestSkillGap { get; private set; }

    public RebirthCapabilityEvaluation(string capabilityId, string targetType, string targetId, bool allowed, IEnumerable<RebirthCapabilityRequirementEvaluation> requirements)
    {
        CapabilityId = capabilityId ?? string.Empty;
        TargetType = targetType ?? string.Empty;
        TargetId = targetId ?? string.Empty;
        IsAllowed = allowed;
        List<RebirthCapabilityRequirementEvaluation> all = new List<RebirthCapabilityRequirementEvaluation>();
        List<RebirthCapabilityRequirementEvaluation> missing = new List<RebirthCapabilityRequirementEvaluation>();
        List<RebirthCapabilityRequirementEvaluation> warnings = new List<RebirthCapabilityRequirementEvaluation>();
        float closest = float.MaxValue;
        if (requirements != null)
        {
            foreach (RebirthCapabilityRequirementEvaluation value in requirements)
            {
                if (value == null) continue;
                all.Add(value);
                if (!value.Allowed && !value.WarningOnly)
                {
                    missing.Add(value);
                    if (string.Equals(value.Kind, RebirthCapabilityKinds.Skill, StringComparison.OrdinalIgnoreCase))
                    {
                        float gap = Math.Max(0f, value.RequiredValue - value.CurrentValue);
                        if (gap < closest) closest = gap;
                    }
                }
                if (value.WarningOnly) warnings.Add(value);
            }
        }
        Requirements = new ReadOnlyCollection<RebirthCapabilityRequirementEvaluation>(all);
        MissingHardRequirements = new ReadOnlyCollection<RebirthCapabilityRequirementEvaluation>(missing);
        Warnings = new ReadOnlyCollection<RebirthCapabilityRequirementEvaluation>(warnings);
        ClosestSkillGap = closest == float.MaxValue ? 0f : closest;
        DisplayState = IsAllowed ? (warnings.Count > 0 ? "available_with_recommendations" : "available") : "locked";
    }

    public string FirstMissingReason
    {
        get
        {
            if (MissingHardRequirements.Count == 0) return string.Empty;
            RebirthCapabilityRequirementEvaluation first = MissingHardRequirements[0];
            return first != null ? (first.Message ?? string.Empty) : string.Empty;
        }
    }
}
