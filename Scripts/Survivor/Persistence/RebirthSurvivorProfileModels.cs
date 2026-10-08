using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public enum RebirthSurvivorProfileCompatibilityKind
{
    Ready = 0,
    NeedsReview = 1,
    MissingDefinition = 2,
    InvalidCombination = 3,
    UnsupportedSchema = 4
}

public sealed class RebirthSurvivorProfileCompatibility
{
    public RebirthSurvivorProfileCompatibilityKind Kind { get; private set; }
    public string Message { get; private set; }
    public ReadOnlyCollection<RebirthSurvivorCreationError> ValidationErrors { get; private set; }

    public bool IsUsableWithoutReview { get { return Kind == RebirthSurvivorProfileCompatibilityKind.Ready; } }
    public bool CanBeReviewed { get { return Kind != RebirthSurvivorProfileCompatibilityKind.UnsupportedSchema; } }

    public RebirthSurvivorProfileCompatibility(RebirthSurvivorProfileCompatibilityKind kind, string message, IList<RebirthSurvivorCreationError> errors)
    {
        Kind = kind;
        Message = message ?? string.Empty;
        ValidationErrors = new ReadOnlyCollection<RebirthSurvivorCreationError>(new List<RebirthSurvivorCreationError>(errors ?? new List<RebirthSurvivorCreationError>()));
    }
}

public sealed class RebirthSurvivorProfileLoadIssue
{
    public string Path { get; private set; }
    public string ProfileId { get; private set; }
    public string Reason { get; private set; }
    public bool RecoveredFromBackup { get; private set; }

    public RebirthSurvivorProfileLoadIssue(string path, string profileId, string reason, bool recovered)
    {
        Path = path ?? string.Empty;
        ProfileId = profileId ?? string.Empty;
        Reason = reason ?? string.Empty;
        RecoveredFromBackup = recovered;
    }
}

/// <summary>
/// User-local reusable starting template. It deliberately contains no earned world progress.
/// </summary>
public sealed class RebirthSurvivorProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; private set; }
    public string ProfileId { get; private set; }
    public string ProfileName { get; private set; }
    public string AuthoredDefinitionHash { get; private set; }
    public string AuthoredDefinitionVersion { get; private set; }
    public string BackgroundId { get; private set; }
    public string DietId { get; private set; }
    public ReadOnlyCollection<string> TraitIds { get; private set; }
    public ReadOnlyDictionary<string, string> CreationChoices { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ModifiedAtUtc { get; private set; }

    public RebirthSurvivorProfile(
        int schemaVersion,
        string profileId,
        string profileName,
        string authoredDefinitionHash,
        string authoredDefinitionVersion,
        string backgroundId,
        string dietId,
        IEnumerable<string> traitIds,
        IDictionary<string, string> creationChoices,
        DateTime createdAtUtc,
        DateTime modifiedAtUtc)
    {
        SchemaVersion = schemaVersion;
        ProfileId = (profileId ?? string.Empty).Trim().ToLowerInvariant();
        ProfileName = (profileName ?? string.Empty).Trim();
        AuthoredDefinitionHash = (authoredDefinitionHash ?? string.Empty).Trim().ToLowerInvariant();
        AuthoredDefinitionVersion = (authoredDefinitionVersion ?? string.Empty).Trim();
        BackgroundId = (backgroundId ?? string.Empty).Trim();
        DietId = (dietId ?? string.Empty).Trim();

        List<string> traits = new List<string>();
        if (traitIds != null)
            foreach (string traitId in traitIds)
                traits.Add((traitId ?? string.Empty).Trim());
        TraitIds = new ReadOnlyCollection<string>(traits);

        Dictionary<string, string> choices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (creationChoices != null)
            foreach (KeyValuePair<string, string> pair in creationChoices)
                choices[(pair.Key ?? string.Empty).Trim()] = pair.Value ?? string.Empty;
        CreationChoices = new ReadOnlyDictionary<string, string>(choices);

        CreatedAtUtc = NormalizeUtc(createdAtUtc);
        ModifiedAtUtc = NormalizeUtc(modifiedAtUtc);
    }

    public static RebirthSurvivorProfile CreateNew(string profileName, RebirthSurvivorCreationSelection selection, IDictionary<string, string> creationChoices)
    {
        DateTime now = DateTime.UtcNow;
        return new RebirthSurvivorProfile(
            CurrentSchemaVersion,
            Guid.NewGuid().ToString("N").ToLowerInvariant(),
            profileName,
            RebirthSurvivorDefinitionRegistry.SemanticHash,
            RebirthSurvivorDefinitionRegistry.DefinitionVersion,
            selection != null ? selection.BackgroundId : string.Empty,
            selection != null ? selection.DietId : string.Empty,
            selection != null ? selection.TraitIds : null,
            creationChoices,
            now,
            now);
    }

    public RebirthSurvivorProfile WithSelections(string profileName, RebirthSurvivorCreationSelection selection, IDictionary<string, string> creationChoices)
    {
        return new RebirthSurvivorProfile(
            CurrentSchemaVersion,
            ProfileId,
            profileName,
            RebirthSurvivorDefinitionRegistry.SemanticHash,
            RebirthSurvivorDefinitionRegistry.DefinitionVersion,
            selection != null ? selection.BackgroundId : string.Empty,
            selection != null ? selection.DietId : string.Empty,
            selection != null ? selection.TraitIds : null,
            creationChoices,
            CreatedAtUtc,
            DateTime.UtcNow);
    }

    public RebirthSurvivorProfile Duplicate(string duplicateName)
    {
        DateTime now = DateTime.UtcNow;
        return new RebirthSurvivorProfile(
            CurrentSchemaVersion,
            Guid.NewGuid().ToString("N").ToLowerInvariant(),
            duplicateName,
            AuthoredDefinitionHash,
            AuthoredDefinitionVersion,
            BackgroundId,
            DietId,
            TraitIds,
            CreationChoices,
            now,
            now);
    }

    public RebirthSurvivorProfile Rename(string newName)
    {
        return new RebirthSurvivorProfile(
            SchemaVersion,
            ProfileId,
            newName,
            AuthoredDefinitionHash,
            AuthoredDefinitionVersion,
            BackgroundId,
            DietId,
            TraitIds,
            CreationChoices,
            CreatedAtUtc,
            DateTime.UtcNow);
    }

    public RebirthSurvivorProfile Clone()
    {
        return new RebirthSurvivorProfile(
            SchemaVersion,
            ProfileId,
            ProfileName,
            AuthoredDefinitionHash,
            AuthoredDefinitionVersion,
            BackgroundId,
            DietId,
            TraitIds,
            CreationChoices,
            CreatedAtUtc,
            ModifiedAtUtc);
    }

    public RebirthSurvivorCreationSelection ToSelection(string definitionHash)
    {
        return new RebirthSurvivorCreationSelection(BackgroundId, DietId, TraitIds, definitionHash ?? string.Empty);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value == default(DateTime))
            return DateTime.UtcNow;
        if (value.Kind == DateTimeKind.Utc)
            return value;
        return value.ToUniversalTime();
    }
}
