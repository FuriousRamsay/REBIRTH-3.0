using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public static class RebirthCapabilityKinds
{
    public const string All = "all";
    public const string Any = "any";
    public const string Skill = "skill";
    public const string Knowledge = "knowledge";
    public const string Blueprint = "blueprint";
    public const string Discipline = "discipline";
}

public sealed class RebirthCapabilityRequirement
{
    public string Kind { get; private set; }
    public string Id { get; private set; }
    public float Minimum { get; private set; }
    public float Recommended { get; private set; }
    public bool HasMinimum { get; private set; }
    public bool HasRecommended { get; private set; }
    public ReadOnlyCollection<RebirthCapabilityRequirement> Children { get; private set; }

    public RebirthCapabilityRequirement(string kind, string id, float minimum, bool hasMinimum, float recommended, bool hasRecommended, IEnumerable<RebirthCapabilityRequirement> children)
    {
        Kind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        Id = (id ?? string.Empty).Trim();
        Minimum = minimum;
        Recommended = recommended;
        HasMinimum = hasMinimum;
        HasRecommended = hasRecommended;
        List<RebirthCapabilityRequirement> values = new List<RebirthCapabilityRequirement>();
        if (children != null) foreach (RebirthCapabilityRequirement child in children) if (child != null) values.Add(child);
        Children = new ReadOnlyCollection<RebirthCapabilityRequirement>(values);
    }
}

public sealed class RebirthCapabilityDefinition
{
    public string Id { get; private set; }
    public string Category { get; private set; }
    public string TargetType { get; private set; }
    public string TargetId { get; private set; }
    public string Visibility { get; private set; }
    public bool LegacyAdapted { get; private set; }
    public RebirthCapabilityRequirement Requirement { get; private set; }

    public RebirthCapabilityDefinition(string id, string category, string targetType, string targetId, string visibility, bool legacyAdapted, RebirthCapabilityRequirement requirement)
    {
        Id = (id ?? string.Empty).Trim();
        Category = (category ?? string.Empty).Trim();
        TargetType = (targetType ?? string.Empty).Trim().ToLowerInvariant();
        TargetId = (targetId ?? string.Empty).Trim();
        Visibility = string.IsNullOrWhiteSpace(visibility) ? "visible" : visibility.Trim().ToLowerInvariant();
        LegacyAdapted = legacyAdapted;
        Requirement = requirement;
    }
}

public sealed class RebirthBlueprintDefinition
{
    public string Id { get; private set; }
    public string NameKey { get; private set; }
    public string DescriptionKey { get; private set; }
    public string Category { get; private set; }

    public RebirthBlueprintDefinition(string id, string nameKey, string descriptionKey, string category)
    {
        Id = (id ?? string.Empty).Trim();
        NameKey = (nameKey ?? string.Empty).Trim();
        DescriptionKey = (descriptionKey ?? string.Empty).Trim();
        Category = (category ?? string.Empty).Trim();
    }
}

public sealed class RebirthProjectOperationDefinition
{
    public string Id { get; private set; }
    public string Type { get; private set; }
    public string NativeTarget { get; private set; }
    public string WorldAction { get; private set; }
    public ReadOnlyCollection<string> AfterOperationIds { get; private set; }
    public RebirthCapabilityRequirement Requirement { get; private set; }

    public RebirthProjectOperationDefinition(string id, string type, string nativeTarget, string worldAction, IEnumerable<string> afterOperationIds, RebirthCapabilityRequirement requirement)
    {
        Id = (id ?? string.Empty).Trim();
        Type = (type ?? string.Empty).Trim().ToLowerInvariant();
        NativeTarget = (nativeTarget ?? string.Empty).Trim();
        WorldAction = (worldAction ?? string.Empty).Trim();
        List<string> after = new List<string>();
        if (afterOperationIds != null) foreach (string value in afterOperationIds) if (!string.IsNullOrWhiteSpace(value)) after.Add(value.Trim());
        AfterOperationIds = new ReadOnlyCollection<string>(after);
        Requirement = requirement;
    }
}

public sealed class RebirthProjectDefinition
{
    public string Id { get; private set; }
    public string OutputId { get; private set; }
    public string Category { get; private set; }
    public bool Enabled { get; private set; }
    public ReadOnlyCollection<RebirthProjectOperationDefinition> Operations { get; private set; }

    public RebirthProjectDefinition(string id, string outputId, string category, bool enabled, IEnumerable<RebirthProjectOperationDefinition> operations)
    {
        Id = (id ?? string.Empty).Trim();
        OutputId = (outputId ?? string.Empty).Trim();
        Category = (category ?? string.Empty).Trim();
        Enabled = enabled;
        List<RebirthProjectOperationDefinition> values = new List<RebirthProjectOperationDefinition>();
        if (operations != null) foreach (RebirthProjectOperationDefinition operation in operations) if (operation != null) values.Add(operation);
        Operations = new ReadOnlyCollection<RebirthProjectOperationDefinition>(values);
    }
}
