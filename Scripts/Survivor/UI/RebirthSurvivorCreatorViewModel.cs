using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

#nullable disable

public static class RebirthSurvivorCreationChoiceKeys
{
    // Native 7DTD Player Profile selected for this Rebirth Survivor.
    // The Player Profile owns appearance; Rebirth owns Background/Traits/Diet/progression.
    public const string PlayerProfileName = "base_player_profile";
}

public enum RebirthSurvivorCreatorPurpose
{
    CreateLocalProfile = 0,
    EditLocalProfile = 1,
    ReviewLocalProfile = 2,
    FirstWorldCreate = 3
}

public enum RebirthSurvivorCreatorStep
{
    Profile = 0,
    Background = 1,
    Diet = 2,
    Traits = 3,
    Review = 4
}

public enum RebirthSurvivorTraitFilter
{
    All = 0,
    Positive = 1,
    Negative = 2,
    Mixed = 3,
    BackgroundSpecific = 4,
    SkillAptitudes = 5
}

public enum RebirthSurvivorTraitCategoryFilter
{
    All = 0,
    Physical = 1,
    Mental = 2,
    Social = 3,
    Lifestyle = 4,
    Aptitudes = 5
}

public sealed class RebirthSurvivorTraitChoice
{
    public RebirthTraitDefinition Definition { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsEligibleForBackground { get; private set; }
    public bool HasSelectedTraitConflict { get; private set; }
    public bool HasDietConflict { get; private set; }
    public string BlockingTraitId { get; private set; }

    public bool CanToggle
    {
        get
        {
            return IsSelected || (IsEligibleForBackground && !HasSelectedTraitConflict && !HasDietConflict);
        }
    }

    public RebirthSurvivorTraitChoice(RebirthTraitDefinition definition, bool selected, bool eligible,
        bool traitConflict, bool dietConflict, string blockingTraitId)
    {
        Definition = definition;
        IsSelected = selected;
        IsEligibleForBackground = eligible;
        HasSelectedTraitConflict = traitConflict;
        HasDietConflict = dietConflict;
        BlockingTraitId = blockingTraitId ?? string.Empty;
    }
}

/// <summary>
/// UI-independent mutable editor for Survivor starting selections.
/// All gameplay validity and resolved starting numbers come from RebirthSurvivorCreationValidator.
/// This view model owns only editor state, paging/filter state, and local-profile persistence intent.
/// </summary>
public sealed class RebirthSurvivorCreatorViewModel
{
    private const int MaxProfileNameLength = 48;
    private const string DefaultBackgroundId = "background.clean_slate";
    private readonly HashSet<string> selectedTraits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> creationChoices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private string editingProfileId = string.Empty;
    private string sourceProfileId = string.Empty;
    private DateTime editingCreatedAtUtc;
    private string profileName = string.Empty;
    private string backgroundId = string.Empty;
    private string dietId = string.Empty;
    private RebirthSurvivorCreationResult validation;
    private bool saveAsReusableProfile;
    private RebirthSurvivorCreatorStep highestUnlockedStep;

    public RebirthSurvivorCreatorPurpose Purpose { get; private set; }
    public RebirthSurvivorCreatorStep Step { get; private set; }
    public RebirthSurvivorCreatorStep HighestUnlockedStep { get { return highestUnlockedStep; } }
    public RebirthSurvivorTraitFilter TraitFilter { get; set; }
    public RebirthSurvivorTraitCategoryFilter TraitCategoryFilter { get; private set; }
    public int BackgroundOffset { get; set; }
    public int TraitOffset { get; set; }
    public int PositiveTraitOffset { get; set; }
    public int NegativeTraitOffset { get; set; }
    public int DietOffset { get; set; }
    public int LastPrunedTraitCount { get; private set; }

    public string ProfileName { get { return profileName; } }
    public string PlayerProfileName
    {
        get
        {
            string value;
            return creationChoices.TryGetValue(RebirthSurvivorCreationChoiceKeys.PlayerProfileName, out value)
                ? (value ?? string.Empty)
                : string.Empty;
        }
    }
    public bool HasPlayerProfileSelection { get { return !string.IsNullOrEmpty(PlayerProfileName.Trim()); } }
    public string BackgroundId { get { return backgroundId; } }
    public string DietId { get { return dietId; } }
    public string EditingProfileId { get { return editingProfileId; } }
    public string SelectedSourceProfileId { get { return sourceProfileId; } }
    public bool IsUsingExistingProfile { get { return !string.IsNullOrEmpty(sourceProfileId); } }
    public RebirthSurvivorCreationResult Validation { get { return validation; } }
    public bool IsReadOnly { get { return Purpose == RebirthSurvivorCreatorPurpose.ReviewLocalProfile; } }
    public bool SaveAsReusableProfile { get { return saveAsReusableProfile; } }
    public bool IsLocalProfileFlow
    {
        get
        {
            return Purpose == RebirthSurvivorCreatorPurpose.CreateLocalProfile ||
                   Purpose == RebirthSurvivorCreatorPurpose.EditLocalProfile ||
                   Purpose == RebirthSurvivorCreatorPurpose.ReviewLocalProfile;
        }
    }

    public ReadOnlyCollection<string> SelectedTraitIds
    {
        get
        {
            List<string> list = new List<string>(selectedTraits);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return new ReadOnlyCollection<string>(list);
        }
    }

    public bool HasValidProfileName
    {
        get
        {
            string value = (profileName ?? string.Empty).Trim();
            return value.Length > 0 && value.Length <= MaxProfileNameLength && value.IndexOfAny(new char[] { '\r', '\n', '\t' }) < 0;
        }
    }

    public bool HasExactTraitPointBalance
    {
        get { return validation != null && validation.IsValid && validation.RemainingCreationPoints == 0; }
    }

    public bool CanPersistLocalProfile
    {
        get
        {
            return !IsReadOnly && IsLocalProfileFlow && HasPlayerProfileSelection && HasValidProfileName && HasExactTraitPointBalance;
        }
    }

    public void BeginCreateLocalProfile()
    {
        Reset(RebirthSurvivorCreatorPurpose.CreateLocalProfile);
    }

    public bool BeginEditLocalProfile(RebirthSurvivorProfile profile, out string error)
    {
        return BeginFromProfile(profile, RebirthSurvivorCreatorPurpose.EditLocalProfile, out error);
    }

    public bool BeginReviewLocalProfile(RebirthSurvivorProfile profile, out string error)
    {
        return BeginFromProfile(profile, RebirthSurvivorCreatorPurpose.ReviewLocalProfile, out error);
    }

    public void BeginFirstWorldCreate()
    {
        Reset(RebirthSurvivorCreatorPurpose.FirstWorldCreate);
        // The first step selects the native 7DTD Player Profile (appearance).
        // Saving a reusable Rebirth Survivor Profile is an explicit choice on Review.
        saveAsReusableProfile = false;
    }

    public void SelectNewFirstWorldProfile()
    {
        if (Purpose != RebirthSurvivorCreatorPurpose.FirstWorldCreate || IsReadOnly) return;
        sourceProfileId = string.Empty;
        profileName = string.Empty;
        backgroundId = DefaultBackgroundId;
        dietId = string.Empty;
        selectedTraits.Clear();
        creationChoices.Clear();
        saveAsReusableProfile = false;
        BackgroundOffset = 0;
        TraitOffset = 0;
        PositiveTraitOffset = 0;
        NegativeTraitOffset = 0;
        TraitCategoryFilter = RebirthSurvivorTraitCategoryFilter.All;
        DietOffset = 0;
        LastPrunedTraitCount = 0;
        highestUnlockedStep = RebirthSurvivorCreatorStep.Profile;
        Step = RebirthSurvivorCreatorStep.Profile;
        Revalidate();
    }

    public bool SelectExistingFirstWorldProfile(RebirthSurvivorProfile profile, out string error)
    {
        error = string.Empty;
        if (Purpose != RebirthSurvivorCreatorPurpose.FirstWorldCreate || IsReadOnly)
        {
            error = "creator is not in first-world profile selection mode";
            return false;
        }
        if (profile == null)
        {
            error = "profile is null";
            return false;
        }
        if (profile.SchemaVersion != RebirthSurvivorProfile.CurrentSchemaVersion)
        {
            error = "unsupported profile schema " + profile.SchemaVersion.ToString(CultureInfo.InvariantCulture);
            return false;
        }

        sourceProfileId = profile.ProfileId ?? string.Empty;
        profileName = profile.ProfileName ?? string.Empty;
        backgroundId = profile.BackgroundId ?? string.Empty;
        dietId = profile.DietId ?? string.Empty;
        selectedTraits.Clear();
        foreach (string id in profile.TraitIds)
            if (!string.IsNullOrEmpty(id)) selectedTraits.Add(id);
        creationChoices.Clear();
        foreach (KeyValuePair<string, string> pair in profile.CreationChoices)
            creationChoices[pair.Key] = pair.Value;
        saveAsReusableProfile = false;
        BackgroundOffset = 0;
        TraitOffset = 0;
        PositiveTraitOffset = 0;
        NegativeTraitOffset = 0;
        TraitCategoryFilter = RebirthSurvivorTraitCategoryFilter.All;
        DietOffset = 0;
        LastPrunedTraitCount = 0;
        highestUnlockedStep = RebirthSurvivorCreatorStep.Profile;
        Step = RebirthSurvivorCreatorStep.Profile;
        Revalidate();
        return true;
    }

    public void ToggleSaveAsReusableProfile()
    {
        if (Purpose != RebirthSurvivorCreatorPurpose.FirstWorldCreate || IsReadOnly) return;
        saveAsReusableProfile = !saveAsReusableProfile;
    }

    public Dictionary<string, string> GetCreationChoicesSnapshot()
    {
        return new Dictionary<string, string>(creationChoices, StringComparer.OrdinalIgnoreCase);
    }

    public void RefreshValidation()
    {
        Revalidate();
    }

    public bool CanNavigateToStep(RebirthSurvivorCreatorStep step)
    {
        if (IsReadOnly) return step == RebirthSurvivorCreatorStep.Review;
        if (step < RebirthSurvivorCreatorStep.Profile || step > highestUnlockedStep) return false;
        if (step == RebirthSurvivorCreatorStep.Profile) return true;
        if (!IsProfileStepComplete()) return false;
        if (step == RebirthSurvivorCreatorStep.Background) return true;
        if (GetSelectedBackground() == null) return false;
        if (step == RebirthSurvivorCreatorStep.Diet) return true;
        if (GetSelectedDiet() == null) return false;
        if (step == RebirthSurvivorCreatorStep.Traits) return true;
        Revalidate();
        return HasExactTraitPointBalance;
    }

    public bool TryNavigateToStep(RebirthSurvivorCreatorStep step, out string reason)
    {
        reason = string.Empty;
        if (!CanNavigateToStep(step))
        {
            reason = "Finish the previous Survivor creation step before continuing.";
            return false;
        }
        Step = step;
        return true;
    }

    public bool TryGoPrevious()
    {
        if (IsReadOnly || Step <= RebirthSurvivorCreatorStep.Profile) return false;
        Step--;
        return true;
    }

    public bool CanAdvanceCurrentStep(out string reason)
    {
        reason = string.Empty;
        if (IsReadOnly || Step >= RebirthSurvivorCreatorStep.Review) return false;
        if (!RebirthSurvivorDefinitionRegistry.IsReady)
        {
            reason = RebirthSurvivorInstaller.LastReport;
            return false;
        }

        if (Step == RebirthSurvivorCreatorStep.Profile)
        {
            if (!IsProfileStepComplete())
            {
                reason = RebirthSurvivorUiText.L("xuiRebirthSurvivorPlayerProfileRequired",
                    "Select a Player Profile before continuing.");
                return false;
            }
            return true;
        }

        if (Step == RebirthSurvivorCreatorStep.Background)
        {
            if (GetSelectedBackground() == null)
            {
                reason = "Choose an Experience before continuing.";
                return false;
            }
            return true;
        }

        if (Step == RebirthSurvivorCreatorStep.Diet)
        {
            if (GetSelectedDiet() == null)
            {
                reason = "Choose a Diet before continuing.";
                return false;
            }
            return true;
        }

        if (Step == RebirthSurvivorCreatorStep.Traits)
        {
            Revalidate();
            if (validation == null || !validation.IsValid)
            {
                reason = validation != null && validation.Errors.Count > 0
                    ? RebirthSurvivorUiText.FormatCreationError(validation.Errors[0])
                    : "Survivor selection is invalid.";
                return false;
            }
            if (validation.RemainingCreationPoints != 0)
            {
                reason = RebirthSurvivorUiText.L("xuiRebirthCreationErrorUnspentCreationPoints",
                    "Trait-point balance must be exactly zero before continuing.");
                return false;
            }
            return true;
        }

        return false;
    }

    public bool TryAdvance(out string reason)
    {
        if (!CanAdvanceCurrentStep(out reason)) return false;
        if (Step == RebirthSurvivorCreatorStep.Profile)
            UnlockAndMoveTo(RebirthSurvivorCreatorStep.Background);
        else if (Step == RebirthSurvivorCreatorStep.Background)
            UnlockAndMoveTo(RebirthSurvivorCreatorStep.Diet);
        else if (Step == RebirthSurvivorCreatorStep.Diet)
            UnlockAndMoveTo(RebirthSurvivorCreatorStep.Traits);
        else if (Step == RebirthSurvivorCreatorStep.Traits)
            UnlockAndMoveTo(RebirthSurvivorCreatorStep.Review);
        else
            return false;
        return true;
    }

    private void UnlockAndMoveTo(RebirthSurvivorCreatorStep step)
    {
        if (step > highestUnlockedStep) highestUnlockedStep = step;
        Step = step;
    }


    public bool IsProfileStepComplete()
    {
        if (IsReadOnly) return true;
        return HasPlayerProfileSelection;
    }

    public void SetPlayerProfileName(string value)
    {
        if (IsReadOnly) return;
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length > RebirthSurvivorNetworkProtocol.MaxChoiceValueLength)
            normalized = normalized.Substring(0, RebirthSurvivorNetworkProtocol.MaxChoiceValueLength);
        if (normalized.Length == 0)
            creationChoices.Remove(RebirthSurvivorCreationChoiceKeys.PlayerProfileName);
        else
            creationChoices[RebirthSurvivorCreationChoiceKeys.PlayerProfileName] = normalized;
    }

    public void SetProfileName(string value)
    {
        if (IsReadOnly) return;
        string normalized = value ?? string.Empty;
        if (normalized.Length > MaxProfileNameLength)
            normalized = normalized.Substring(0, MaxProfileNameLength);
        profileName = normalized;
    }

    public void SelectBackground(string id)
    {
        if (IsReadOnly) return;
        RebirthBackgroundDefinition definition;
        if (!RebirthSurvivorDefinitionRegistry.TryGetBackground(id, out definition) || definition == null)
            return;

        backgroundId = definition.Id;
        LastPrunedTraitCount = PruneTraitsNotAvailableToBackground(definition);
        TraitOffset = 0;
        PositiveTraitOffset = 0;
        NegativeTraitOffset = 0;
        Revalidate();
    }

    public void SelectDiet(string id)
    {
        if (IsReadOnly) return;
        RebirthDietDefinition definition;
        if (!RebirthSurvivorDefinitionRegistry.TryGetDiet(id, out definition) || definition == null)
            return;
        dietId = definition.Id;
        Revalidate();
    }

    public bool ToggleTrait(string id, out string reason)
    {
        reason = string.Empty;
#if REBIRTH_DEBUG
        RebirthSurvivorTraitUiDebug.Snapshot debugBefore = RebirthLogSettings.TraitUiLoggingEnabled ? RebirthSurvivorTraitUiDebug.Capture(this) : null;
#endif
        if (IsReadOnly)
        {
            reason = "profile is read-only";
#if REBIRTH_DEBUG
            RebirthSurvivorTraitUiDebug.LogRejected(this, id, reason, debugBefore);
#endif
            return false;
        }

        RebirthTraitDefinition definition;
        if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out definition) || definition == null)
        {
            reason = "trait definition is unavailable";
#if REBIRTH_DEBUG
            RebirthSurvivorTraitUiDebug.LogRejected(this, id, reason, debugBefore);
#endif
            return false;
        }

        if (selectedTraits.Contains(definition.Id))
        {
            selectedTraits.Remove(definition.Id);
            Revalidate();
#if REBIRTH_DEBUG
            RebirthSurvivorTraitUiDebug.LogToggle(this, definition, "remove", true, string.Empty, debugBefore);
#endif
            return true;
        }

        RebirthSurvivorTraitChoice choice = BuildTraitChoice(definition);
        if (!choice.CanToggle)
        {
            if (!choice.IsEligibleForBackground)
                reason = "trait is not available to the selected Experience";
            else if (choice.HasSelectedTraitConflict)
                reason = "trait conflicts with " + choice.BlockingTraitId;
            else if (choice.HasDietConflict)
                reason = "trait conflicts with the selected Diet";
            else
                reason = "trait cannot be selected";
#if REBIRTH_DEBUG
            RebirthSurvivorTraitUiDebug.LogToggle(this, definition, "add", false, reason, debugBefore);
#endif
            return false;
        }

        selectedTraits.Add(definition.Id);
        Revalidate();
#if REBIRTH_DEBUG
        RebirthSurvivorTraitUiDebug.LogToggle(this, definition, "add", true, string.Empty, debugBefore);
#endif
        return true;
    }

    public RebirthBackgroundDefinition[] GetBackgroundPage(int pageSize)
    {
        List<RebirthBackgroundDefinition> values = GetSortedBackgrounds();
        BackgroundOffset = ClampOffset(BackgroundOffset, values.Count, pageSize);
        return Slice(values, BackgroundOffset, pageSize).ToArray();
    }

    public int GetBackgroundCount()
    {
        return GetSortedBackgrounds().Count;
    }

    public RebirthSurvivorTraitChoice[] GetTraitPage(int pageSize)
    {
        List<RebirthTraitDefinition> values = GetVisibleTraits();
        TraitOffset = ClampOffset(TraitOffset, values.Count, pageSize);
        List<RebirthSurvivorTraitChoice> page = new List<RebirthSurvivorTraitChoice>();
        foreach (RebirthTraitDefinition definition in Slice(values, TraitOffset, pageSize))
            page.Add(BuildTraitChoice(definition));
        return page.ToArray();
    }

    public int GetTraitCount()
    {
        return GetVisibleTraits().Count;
    }

    public RebirthSurvivorTraitChoice[] GetPositiveTraitPage(int pageSize)
    {
        List<RebirthTraitDefinition> values = GetTraitSideList(false);
        PositiveTraitOffset = ClampOffset(PositiveTraitOffset, values.Count, pageSize);
        List<RebirthSurvivorTraitChoice> page = new List<RebirthSurvivorTraitChoice>();
        foreach (RebirthTraitDefinition definition in Slice(values, PositiveTraitOffset, pageSize))
            page.Add(BuildTraitChoice(definition));
        return page.ToArray();
    }

    public RebirthSurvivorTraitChoice[] GetNegativeTraitPage(int pageSize)
    {
        List<RebirthTraitDefinition> values = GetTraitSideList(true);
        NegativeTraitOffset = ClampOffset(NegativeTraitOffset, values.Count, pageSize);
        List<RebirthSurvivorTraitChoice> page = new List<RebirthSurvivorTraitChoice>();
        foreach (RebirthTraitDefinition definition in Slice(values, NegativeTraitOffset, pageSize))
            page.Add(BuildTraitChoice(definition));
        return page.ToArray();
    }

    public int GetPositiveTraitCount() { return GetTraitSideList(false).Count; }
    public int GetNegativeTraitCount() { return GetTraitSideList(true).Count; }

    public void SetTraitCategoryFilter(RebirthSurvivorTraitCategoryFilter filter)
    {
        TraitCategoryFilter = filter;
        PositiveTraitOffset = 0;
        NegativeTraitOffset = 0;
    }

    public void ClearTraits()
    {
        if (IsReadOnly) return;
        selectedTraits.Clear();
        PositiveTraitOffset = 0;
        NegativeTraitOffset = 0;
        Revalidate();
    }

    public RebirthDietDefinition[] GetDietPage(int pageSize)
    {
        List<RebirthDietDefinition> values = GetSortedDiets();
        DietOffset = ClampOffset(DietOffset, values.Count, pageSize);
        return Slice(values, DietOffset, pageSize).ToArray();
    }

    public int GetDietCount()
    {
        return GetSortedDiets().Count;
    }

    public RebirthBackgroundDefinition GetSelectedBackground()
    {
        RebirthBackgroundDefinition value;
        return RebirthSurvivorDefinitionRegistry.TryGetBackground(backgroundId, out value) ? value : null;
    }

    public RebirthDietDefinition GetSelectedDiet()
    {
        RebirthDietDefinition value;
        return RebirthSurvivorDefinitionRegistry.TryGetDiet(dietId, out value) ? value : null;
    }

    public RebirthTraitDefinition GetTrait(string id)
    {
        RebirthTraitDefinition value;
        return RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out value) ? value : null;
    }

    public RebirthSurvivorTraitChoice GetTraitChoice(string id)
    {
        RebirthTraitDefinition value = GetTrait(id);
        return value != null ? BuildTraitChoice(value) : null;
    }

    public ReadOnlyCollection<string> GetUnresolvedTraitIds()
    {
        List<string> unresolved = new List<string>();
        foreach (string id in selectedTraits)
        {
            RebirthTraitDefinition value;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out value) || value == null) unresolved.Add(id);
        }
        unresolved.Sort(StringComparer.OrdinalIgnoreCase);
        return new ReadOnlyCollection<string>(unresolved);
    }

    public int RemoveUnresolvedTraits()
    {
        if (IsReadOnly) return 0;
        ReadOnlyCollection<string> unresolved = GetUnresolvedTraitIds();
        for (int i = 0; i < unresolved.Count; i++) selectedTraits.Remove(unresolved[i]);
        if (unresolved.Count > 0) Revalidate();
        return unresolved.Count;
    }

    public string GetTraitFilterLabelKey()
    {
        switch (TraitFilter)
        {
            case RebirthSurvivorTraitFilter.Positive: return "xuiRebirthSurvivorFilterPositive";
            case RebirthSurvivorTraitFilter.Negative: return "xuiRebirthSurvivorFilterNegative";
            case RebirthSurvivorTraitFilter.Mixed: return "xuiRebirthSurvivorFilterMixed";
            case RebirthSurvivorTraitFilter.BackgroundSpecific: return "xuiRebirthSurvivorFilterBackground";
            case RebirthSurvivorTraitFilter.SkillAptitudes: return "xuiRebirthSurvivorFilterAptitudes";
            default: return "xuiRebirthSurvivorFilterAll";
        }
    }

    public void CycleTraitFilter(int direction)
    {
        int count = (int)RebirthSurvivorTraitFilter.SkillAptitudes + 1;
        int value = ((int)TraitFilter + (direction < 0 ? -1 : 1) + count) % count;
        TraitFilter = (RebirthSurvivorTraitFilter)value;
        TraitOffset = 0;
    }

    public bool SaveLocalProfile(out RebirthSurvivorProfile savedProfile, out string error)
    {
        savedProfile = null;
        error = string.Empty;
        if (!CanPersistLocalProfile)
        {
            error = BuildLocalSaveBlockingReason();
            return false;
        }

        RebirthSurvivorCreationSelection selection = BuildSelection();
        if (Purpose == RebirthSurvivorCreatorPurpose.CreateLocalProfile)
            return RebirthSurvivorProfileStore.TryCreate(profileName.Trim(), selection, creationChoices, out savedProfile, out error);

        if (Purpose != RebirthSurvivorCreatorPurpose.EditLocalProfile || string.IsNullOrEmpty(editingProfileId))
        {
            error = "creator is not in a local profile save mode";
            return false;
        }

        RebirthSurvivorProfile original;
        if (!RebirthSurvivorProfileStore.TryGet(editingProfileId, out original) || original == null)
        {
            error = "profile no longer exists";
            return false;
        }
        RebirthSurvivorProfile edited = original.WithSelections(profileName.Trim(), selection, creationChoices);
        if (!RebirthSurvivorProfileStore.TrySaveEdited(edited, out error))
            return false;
        savedProfile = edited.Clone();
        return true;
    }

    public RebirthSurvivorCreationSelection BuildSelection()
    {
        return new RebirthSurvivorCreationSelection(backgroundId, dietId, SelectedTraitIds,
            RebirthSurvivorDefinitionRegistry.SemanticHash);
    }

    public string BuildLocalSaveBlockingReason()
    {
        if (IsReadOnly) return Localization.Get("xuiRebirthSurvivorReviewReadOnly");
        if (!HasPlayerProfileSelection) return RebirthSurvivorUiText.L("xuiRebirthSurvivorPlayerProfileRequired", "Select a Player Profile before continuing.");
        if (!HasValidProfileName) return Localization.Get("xuiRebirthSurvivorProfileNameRequired");
        if (validation == null) return Localization.Get("xuiRebirthSurvivorValidationUnavailable");
        if (!validation.IsValid && validation.Errors.Count > 0)
            return RebirthSurvivorUiText.FormatCreationError(validation.Errors[0]);
        if (validation.RemainingCreationPoints != 0)
            return RebirthSurvivorUiText.L("xuiRebirthCreationErrorUnspentCreationPoints",
                "Trait-point balance must be exactly zero before this Survivor can be saved.");
        return RebirthSurvivorUiText.L("xuiRebirthSurvivorSelectionInvalid", "Survivor selection is not eligible to save.");
    }

    private void Reset(RebirthSurvivorCreatorPurpose purpose)
    {
        Purpose = purpose;
        Step = purpose == RebirthSurvivorCreatorPurpose.ReviewLocalProfile ? RebirthSurvivorCreatorStep.Review : RebirthSurvivorCreatorStep.Profile;
        highestUnlockedStep = purpose == RebirthSurvivorCreatorPurpose.ReviewLocalProfile ? RebirthSurvivorCreatorStep.Review : RebirthSurvivorCreatorStep.Profile;
        TraitFilter = RebirthSurvivorTraitFilter.All;
        TraitCategoryFilter = RebirthSurvivorTraitCategoryFilter.All;
        BackgroundOffset = 0;
        TraitOffset = 0;
        PositiveTraitOffset = 0;
        NegativeTraitOffset = 0;
        DietOffset = 0;
        LastPrunedTraitCount = 0;
        editingProfileId = string.Empty;
        sourceProfileId = string.Empty;
        editingCreatedAtUtc = default(DateTime);
        profileName = string.Empty;
        backgroundId = DefaultBackgroundId;
        dietId = string.Empty;
        selectedTraits.Clear();
        creationChoices.Clear();
        saveAsReusableProfile = false;
        Revalidate();
    }

    private bool BeginFromProfile(RebirthSurvivorProfile profile, RebirthSurvivorCreatorPurpose purpose, out string error)
    {
        error = string.Empty;
        if (profile == null)
        {
            error = "profile is null";
            return false;
        }
        if (profile.SchemaVersion != RebirthSurvivorProfile.CurrentSchemaVersion)
        {
            error = "unsupported profile schema " + profile.SchemaVersion.ToString(CultureInfo.InvariantCulture);
            return false;
        }

        Reset(purpose);
        editingProfileId = profile.ProfileId;
        editingCreatedAtUtc = profile.CreatedAtUtc;
        profileName = profile.ProfileName;
        backgroundId = profile.BackgroundId;
        dietId = profile.DietId;
        foreach (string id in profile.TraitIds)
            if (!string.IsNullOrEmpty(id)) selectedTraits.Add(id);
        foreach (KeyValuePair<string, string> pair in profile.CreationChoices)
            creationChoices[pair.Key] = pair.Value;
        Step = purpose == RebirthSurvivorCreatorPurpose.ReviewLocalProfile ? RebirthSurvivorCreatorStep.Review : RebirthSurvivorCreatorStep.Profile;
        highestUnlockedStep = RebirthSurvivorCreatorStep.Review;
        Revalidate();
        return true;
    }

    private void Revalidate()
    {
        validation = RebirthSurvivorCreationValidator.Validate(BuildSelection(), Purpose == RebirthSurvivorCreatorPurpose.FirstWorldCreate);
    }

    private int PruneTraitsNotAvailableToBackground(RebirthBackgroundDefinition background)
    {
        List<string> remove = new List<string>();
        foreach (string id in selectedTraits)
        {
            RebirthTraitDefinition trait;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out trait) || trait == null || !IsEligibleForBackground(trait, background))
                remove.Add(id);
        }
        for (int i = 0; i < remove.Count; i++) selectedTraits.Remove(remove[i]);
        return remove.Count;
    }

    private RebirthSurvivorTraitChoice BuildTraitChoice(RebirthTraitDefinition definition)
    {
        bool selected = definition != null && selectedTraits.Contains(definition.Id);
        RebirthBackgroundDefinition background = GetSelectedBackground();
        bool eligible = definition != null && IsEligibleForBackground(definition, background);
        bool conflict = false;
        string blocking = string.Empty;
        if (definition != null)
        {
            foreach (string selectedId in selectedTraits)
            {
                if (string.Equals(selectedId, definition.Id, StringComparison.OrdinalIgnoreCase)) continue;
                RebirthTraitDefinition other;
                if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(selectedId, out other) || other == null) continue;
                if (Contains(definition.ConflictTraitIds, other.Id) || Contains(other.ConflictTraitIds, definition.Id))
                {
                    conflict = true;
                    blocking = other.Id;
                    break;
                }
            }
        }
        bool dietConflict = definition != null && !string.IsNullOrEmpty(dietId) && Contains(definition.ConflictDietIds, dietId);
        return new RebirthSurvivorTraitChoice(definition, selected, eligible, conflict, dietConflict, blocking);
    }

    private bool IsEligibleForBackground(RebirthTraitDefinition trait, RebirthBackgroundDefinition background)
    {
        if (trait == null || trait.Availability == RebirthDefinitionAvailability.Deferred)
            return false;
        if (background == null)
            return trait.Availability != RebirthDefinitionAvailability.Restricted;
        if (Contains(background.BlockedTraitIds, trait.Id))
            return false;
        if (trait.Availability != RebirthDefinitionAvailability.Restricted)
            return true;
        return Contains(trait.AllowedBackgroundIds, background.Id) && Contains(background.RestrictedTraitIds, trait.Id);
    }

    private List<RebirthBackgroundDefinition> GetSortedBackgrounds()
    {
        List<RebirthBackgroundDefinition> list = new List<RebirthBackgroundDefinition>();
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle != null) list.AddRange(bundle.Backgrounds);
        list.Sort(delegate(RebirthBackgroundDefinition a, RebirthBackgroundDefinition b)
        {
            bool aCleanSlate = a != null && string.Equals(a.Id, "background.clean_slate", StringComparison.OrdinalIgnoreCase);
            bool bCleanSlate = b != null && string.Equals(b.Id, "background.clean_slate", StringComparison.OrdinalIgnoreCase);
            if (aCleanSlate != bCleanSlate) return aCleanSlate ? -1 : 1;
            return StringComparer.CurrentCultureIgnoreCase.Compare(RebirthSurvivorUiText.L(a.NameKey, a.Id), RebirthSurvivorUiText.L(b.NameKey, b.Id));
        });
        return list;
    }

    private List<RebirthDietDefinition> GetSortedDiets()
    {
        List<RebirthDietDefinition> list = new List<RebirthDietDefinition>();
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle != null) list.AddRange(bundle.Diets);
        list.Sort(delegate(RebirthDietDefinition a, RebirthDietDefinition b)
        {
            return StringComparer.CurrentCultureIgnoreCase.Compare(RebirthSurvivorUiText.L(a.NameKey, a.Id), RebirthSurvivorUiText.L(b.NameKey, b.Id));
        });
        return list;
    }

    private List<RebirthTraitDefinition> GetTraitSideList(bool negativeSide)
    {
        List<RebirthTraitDefinition> list = new List<RebirthTraitDefinition>();
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null) return list;
        RebirthBackgroundDefinition background = GetSelectedBackground();

        foreach (RebirthTraitDefinition trait in bundle.Traits)
        {
            if (trait == null || trait.Availability == RebirthDefinitionAvailability.Deferred) continue;
            if (negativeSide)
            {
                if (trait.Polarity != RebirthTraitPolarity.Negative) continue;
            }
            else
            {
                // The approved dual-pane design has no third neutral column. Mixed Traits remain
                // selectable here as zero-point choices and are explicitly badged MIXED in the row.
                if (trait.Polarity == RebirthTraitPolarity.Negative) continue;
            }
            if (!MatchesTraitCategory(trait)) continue;
            list.Add(trait);
        }

        list.Sort(delegate(RebirthTraitDefinition a, RebirthTraitDefinition b)
        {
            // Keep list positions stable while the player selects/removes Traits. Selection state is
            // already mirrored in the center summary; moving a clicked row to the top is disruptive.
            bool aEligible = IsEligibleForBackground(a, background);
            bool bEligible = IsEligibleForBackground(b, background);
            if (aEligible != bEligible) return aEligible ? -1 : 1;

            bool aAptitude = RebirthSkillAptitudeTraitFactory.IsAptitude(a);
            bool bAptitude = RebirthSkillAptitudeTraitFactory.IsAptitude(b);
            if (aAptitude != bAptitude) return aAptitude ? 1 : -1;

            if (!negativeSide)
            {
                bool aMixed = a.Polarity != RebirthTraitPolarity.Positive;
                bool bMixed = b.Polarity != RebirthTraitPolarity.Positive;
                if (aMixed != bMixed) return aMixed ? 1 : -1;
            }
            return StringComparer.CurrentCultureIgnoreCase.Compare(RebirthSurvivorUiText.TraitDisplayName(a), RebirthSurvivorUiText.TraitDisplayName(b));
        });
        return list;
    }

    private bool MatchesTraitCategory(RebirthTraitDefinition trait)
    {
        if (trait == null) return false;
        bool aptitude = RebirthSkillAptitudeTraitFactory.IsAptitude(trait);
        if (TraitCategoryFilter == RebirthSurvivorTraitCategoryFilter.All) return true;
        if (TraitCategoryFilter == RebirthSurvivorTraitCategoryFilter.Aptitudes)
            return aptitude || string.Equals(trait.Category, "Skill Weakness", StringComparison.OrdinalIgnoreCase);
        if (aptitude) return false;

        string category = (trait.Category ?? string.Empty).Trim();
        switch (TraitCategoryFilter)
        {
            case RebirthSurvivorTraitCategoryFilter.Physical:
                return category.Equals("Physical", StringComparison.OrdinalIgnoreCase) ||
                       category.Equals("Physiology", StringComparison.OrdinalIgnoreCase) ||
                       category.Equals("Environment", StringComparison.OrdinalIgnoreCase);
            case RebirthSurvivorTraitCategoryFilter.Mental:
                return category.Equals("Psychological", StringComparison.OrdinalIgnoreCase) ||
                       category.Equals("Development", StringComparison.OrdinalIgnoreCase);
            case RebirthSurvivorTraitCategoryFilter.Social:
                return IsSocialTrait(trait);
            case RebirthSurvivorTraitCategoryFilter.Lifestyle:
                return category.Equals("Metabolism", StringComparison.OrdinalIgnoreCase) ||
                       category.Equals("Food/Mood", StringComparison.OrdinalIgnoreCase) ||
                       category.Equals("Habit", StringComparison.OrdinalIgnoreCase) ||
                       (category.Equals("Background", StringComparison.OrdinalIgnoreCase) && !IsSocialTrait(trait));
            default:
                return true;
        }
    }

    private static bool IsSocialTrait(RebirthTraitDefinition trait)
    {
        if (trait == null) return false;
        string id = (trait.Id ?? string.Empty).ToLowerInvariant();
        return id.Contains("people_person") || id.Contains("social_host") || id.Contains("negotiator") ||
               id.Contains("customer_service") || id.Contains("loner") || id.Contains("compassion") ||
               id.Contains("desk_duty") || id.Contains("field_discipline");
    }

    private List<RebirthTraitDefinition> GetVisibleTraits()
    {
        List<RebirthTraitDefinition> list = new List<RebirthTraitDefinition>();
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null) return list;
        RebirthBackgroundDefinition background = GetSelectedBackground();

        foreach (RebirthTraitDefinition trait in bundle.Traits)
        {
            if (trait == null || trait.Availability == RebirthDefinitionAvailability.Deferred) continue;
            // Chunk 9 presentation rule: unavailable/background-locked choices remain visible so the
            // player can understand that they exist and why they cannot currently be selected.
            // Deferred Traits remain hidden because they are not launch choices at all.
            if (!MatchesFilter(trait)) continue;
            list.Add(trait);
        }
        list.Sort(delegate(RebirthTraitDefinition a, RebirthTraitDefinition b)
        {
            // Keep list positions stable while the player selects/removes Traits. Selection state is
            // already mirrored in the center summary; moving a clicked row to the top is disruptive.
            bool aEligible = IsEligibleForBackground(a, background);
            bool bEligible = IsEligibleForBackground(b, background);
            if (aEligible != bEligible) return aEligible ? -1 : 1;

            bool aAptitude = RebirthSkillAptitudeTraitFactory.IsAptitude(a);
            bool bAptitude = RebirthSkillAptitudeTraitFactory.IsAptitude(b);
            if (aAptitude != bAptitude) return aAptitude ? 1 : -1;

            int polarity = a.Polarity.CompareTo(b.Polarity);
            if (TraitFilter == RebirthSurvivorTraitFilter.All && polarity != 0) return polarity;
            return StringComparer.CurrentCultureIgnoreCase.Compare(RebirthSurvivorUiText.TraitDisplayName(a), RebirthSurvivorUiText.TraitDisplayName(b));
        });
        return list;
    }

    private bool MatchesFilter(RebirthTraitDefinition trait)
    {
        switch (TraitFilter)
        {
            case RebirthSurvivorTraitFilter.Positive:
                return trait.Polarity == RebirthTraitPolarity.Positive && !RebirthSkillAptitudeTraitFactory.IsAptitude(trait);
            case RebirthSurvivorTraitFilter.Negative: return trait.Polarity == RebirthTraitPolarity.Negative;
            case RebirthSurvivorTraitFilter.Mixed:
                return trait.Polarity != RebirthTraitPolarity.Positive && trait.Polarity != RebirthTraitPolarity.Negative;
            case RebirthSurvivorTraitFilter.BackgroundSpecific:
                return trait.Availability == RebirthDefinitionAvailability.Restricted;
            case RebirthSurvivorTraitFilter.SkillAptitudes:
                return RebirthSkillAptitudeTraitFactory.IsAptitude(trait);
            default: return true;
        }
    }

    private static int ClampOffset(int offset, int total, int pageSize)
    {
        if (pageSize <= 0 || total <= pageSize) return 0;
        int max = Math.Max(0, total - pageSize);
        if (offset < 0) return 0;
        if (offset > max) return max;
        return offset;
    }

    private static List<T> Slice<T>(List<T> source, int offset, int count)
    {
        List<T> result = new List<T>();
        if (source == null || count <= 0) return result;
        int end = Math.Min(source.Count, Math.Max(0, offset) + count);
        for (int i = Math.Max(0, offset); i < end; i++) result.Add(source[i]);
        return result;
    }

    private static bool Contains(IEnumerable<string> values, string wanted)
    {
        if (values == null || string.IsNullOrEmpty(wanted)) return false;
        foreach (string value in values)
            if (string.Equals(value, wanted, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
