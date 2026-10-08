using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public sealed class RebirthWorldOriginAttribute
{
    public string AttributeId { get; private set; }
    public float Current { get; private set; }
    public float Potential { get; private set; }
    public RebirthWorldOriginAttribute(string id, float current, float potential)
    { AttributeId = id ?? string.Empty; Current = current; Potential = potential; }
    public RebirthWorldOriginAttribute Clone() { return new RebirthWorldOriginAttribute(AttributeId, Current, Potential); }
}

public sealed class RebirthWorldOriginSnapshot
{
    /// <summary>
    /// Server-generated technical creation identifier. It is not a user choice and has no gameplay
    /// meaning; it links the immutable world origin to the fresh metabolism generation created by
    /// the same authoritative commit.
    /// </summary>
    public string CreationId { get; private set; }
    public string DefinitionHash { get; private set; }
    public string DefinitionVersion { get; private set; }
    // Provenance only. Runtime gameplay never re-reads the reusable local profile after this
    // snapshot is committed; Background, Diet, Traits, resolved Skills/Attributes/Knowledge,
    // and creation choices below are the save-owned immutable origin.
    public string SourceProfileId { get; private set; }
    public string SourceProfileName { get; private set; }
    public string BackgroundId { get; private set; }
    public string DietId { get; private set; }
    public ReadOnlyCollection<string> TraitIds { get; private set; }
    public int RemainingCreationPoints { get; private set; }
    public ReadOnlyCollection<RebirthWorldOriginAttribute> Attributes { get; private set; }
    public float HealthPotential { get; private set; }
    public int UnencumberedSlotDelta { get; private set; }
    public ReadOnlyDictionary<string, float> StartingSkills { get; private set; }
    public ReadOnlyDictionary<string, float> StartingSkillKnowledge { get; private set; }
    public ReadOnlyCollection<string> StartingKnowledgeIds { get; private set; }
    public ReadOnlyDictionary<string, string> CreationChoices { get; private set; }
    public DateTime CommittedAtUtc { get; private set; }

    public RebirthWorldOriginSnapshot(
        string creationId,
        string definitionHash,
        string definitionVersion,
        string sourceProfileId,
        string sourceProfileName,
        string backgroundId,
        string dietId,
        IEnumerable<string> traitIds,
        int remainingCreationPoints,
        IEnumerable<RebirthWorldOriginAttribute> attributes,
        float healthPotential,
        int unencumberedSlotDelta,
        IDictionary<string, float> startingSkills,
        IDictionary<string, float> startingSkillKnowledge,
        IEnumerable<string> startingKnowledgeIds,
        IDictionary<string, string> creationChoices,
        DateTime committedAtUtc)
    {
        CreationId = creationId ?? string.Empty;
        DefinitionHash = definitionHash ?? string.Empty;
        DefinitionVersion = definitionVersion ?? string.Empty;
        SourceProfileId = sourceProfileId ?? string.Empty;
        SourceProfileName = sourceProfileName ?? string.Empty;
        BackgroundId = backgroundId ?? string.Empty;
        DietId = dietId ?? string.Empty;

        List<string> traits = new List<string>();
        if (traitIds != null) foreach (string id in traitIds) traits.Add(id ?? string.Empty);
        TraitIds = new ReadOnlyCollection<string>(traits);
        RemainingCreationPoints = remainingCreationPoints;

        List<RebirthWorldOriginAttribute> attr = new List<RebirthWorldOriginAttribute>();
        if (attributes != null) foreach (RebirthWorldOriginAttribute a in attributes) if (a != null) attr.Add(a.Clone());
        Attributes = new ReadOnlyCollection<RebirthWorldOriginAttribute>(attr);
        HealthPotential = healthPotential;
        UnencumberedSlotDelta = unencumberedSlotDelta;
        StartingSkills = new ReadOnlyDictionary<string, float>(new Dictionary<string, float>(startingSkills ?? new Dictionary<string, float>(), StringComparer.OrdinalIgnoreCase));
        StartingSkillKnowledge = new ReadOnlyDictionary<string, float>(new Dictionary<string, float>(startingSkillKnowledge ?? new Dictionary<string, float>(), StringComparer.OrdinalIgnoreCase));

        List<string> knowledge = new List<string>();
        if (startingKnowledgeIds != null) foreach (string id in startingKnowledgeIds) knowledge.Add(id ?? string.Empty);
        StartingKnowledgeIds = new ReadOnlyCollection<string>(knowledge);
        CreationChoices = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(creationChoices ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase));
        CommittedAtUtc = committedAtUtc.Kind == DateTimeKind.Utc ? committedAtUtc : committedAtUtc.ToUniversalTime();
    }

    public static RebirthWorldOriginSnapshot FromAuthoritativeCreationResult(
        string creationId,
        RebirthSurvivorCreationResult result,
        RebirthSurvivorCreationNetworkRequest request,
        DateTime committedAtUtc)
    {
        if (result == null || !result.IsValid || request == null || string.IsNullOrEmpty(creationId))
            return null;

        return Build(
            creationId,
            result,
            request.SourceProfileId,
            request.SourceProfileName,
            request.CreationChoices,
            committedAtUtc);
    }

    private static RebirthWorldOriginSnapshot Build(
        string creationId,
        RebirthSurvivorCreationResult result,
        string sourceProfileId,
        string sourceProfileName,
        IDictionary<string, string> creationChoices,
        DateTime committedAtUtc)
    {
        List<RebirthWorldOriginAttribute> attributes = new List<RebirthWorldOriginAttribute>();
        for (int i = 0; i < result.Attributes.Count; i++)
        {
            RebirthResolvedAttributeStart a = result.Attributes[i];
            if (a != null)
                attributes.Add(new RebirthWorldOriginAttribute(a.AttributeId, a.Current, a.Potential));
        }

        return new RebirthWorldOriginSnapshot(
            creationId,
            result.DefinitionHash,
            result.DefinitionVersion,
            sourceProfileId ?? string.Empty,
            sourceProfileName ?? string.Empty,
            result.BackgroundId,
            result.DietId,
            result.TraitIds,
            result.RemainingCreationPoints,
            attributes,
            result.HealthPotential,
            result.UnencumberedSlotDelta,
            new Dictionary<string, float>(result.StartingSkills, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, float>(result.StartingSkillKnowledge, StringComparer.OrdinalIgnoreCase),
            result.StartingKnowledgeIds,
            creationChoices != null
                ? new Dictionary<string, string>(creationChoices, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            committedAtUtc);
    }

    public RebirthWorldOriginSnapshot Clone()
    {
        return new RebirthWorldOriginSnapshot(CreationId, DefinitionHash, DefinitionVersion, SourceProfileId, SourceProfileName, BackgroundId, DietId,
            TraitIds, RemainingCreationPoints, Attributes, HealthPotential, UnencumberedSlotDelta, StartingSkills, StartingSkillKnowledge, StartingKnowledgeIds, CreationChoices, CommittedAtUtc);
    }
}

public sealed class RebirthAttributeRuntimeState
{
    public string AttributeId;
    public float Current;
    public float Potential;
    public RebirthAttributeRuntimeState Clone() { return new RebirthAttributeRuntimeState { AttributeId = AttributeId, Current = Current, Potential = Potential }; }
}

public sealed class RebirthSkillRuntimeState
{
    public string SkillId;
    public float Value;
    public float Progress;
    public RebirthSkillRuntimeState Clone() { return new RebirthSkillRuntimeState { SkillId = SkillId, Value = Value, Progress = Progress }; }
}

public sealed class RebirthSkillKnowledgeRuntimeState
{
    public string SkillId;
    public float Value;
    public RebirthSkillKnowledgeRuntimeState Clone() { return new RebirthSkillKnowledgeRuntimeState { SkillId = SkillId, Value = Value }; }
}

public sealed class RebirthWildTamingRuntimeState
{
    public string SessionId = string.Empty;
    public int TargetEntityId;
    public string SourceEntityClass = string.Empty;
    public string SpeciesCategory = string.Empty;
    public string Stage = "calm";
    public int SuccessfulInteractions;
    public Vector3 LastKnownPosition;
    public long StartedUtcTicks;
    public long LastInteractionUtcTicks;
    public bool InitiationTrial;

    public RebirthWildTamingRuntimeState Clone()
    {
        return new RebirthWildTamingRuntimeState
        {
            SessionId = SessionId ?? string.Empty, TargetEntityId = TargetEntityId, SourceEntityClass = SourceEntityClass ?? string.Empty,
            SpeciesCategory = SpeciesCategory ?? string.Empty, Stage = Stage ?? "calm", SuccessfulInteractions = SuccessfulInteractions,
            LastKnownPosition = LastKnownPosition, StartedUtcTicks = StartedUtcTicks, LastInteractionUtcTicks = LastInteractionUtcTicks, InitiationTrial = InitiationTrial
        };
    }
}


public sealed class RebirthTeachingLessonRuntimeState
{
    public string SkillId = string.Empty;
    public float RemainingActiveSeconds;
    public float GainMultiplier = 1f;
    public string InstructorStorageKey = string.Empty;
    public string OutcomeId = string.Empty;
    public RebirthTeachingLessonRuntimeState Clone() { return new RebirthTeachingLessonRuntimeState { SkillId=SkillId??string.Empty, RemainingActiveSeconds=RemainingActiveSeconds, GainMultiplier=GainMultiplier, InstructorStorageKey=InstructorStorageKey??string.Empty, OutcomeId=OutcomeId??string.Empty }; }
}

public sealed class RebirthTeachingHistoryRuntimeState
{
    public string Key = string.Empty;
    public string StudentStorageKey = string.Empty;
    public string SkillId = string.Empty;
    public long LastCompletedUtcTicks;
    public int CompletionCount;
    // Non-zero only for persistent virtual NPC/companion students. Player-to-player lessons
    // keep their Theory in the student's own world-character record.
    public float StudentTheoryValue;
    public RebirthTeachingHistoryRuntimeState Clone() { return new RebirthTeachingHistoryRuntimeState { Key=Key??string.Empty, StudentStorageKey=StudentStorageKey??string.Empty, SkillId=SkillId??string.Empty, LastCompletedUtcTicks=LastCompletedUtcTicks, CompletionCount=CompletionCount, StudentTheoryValue=StudentTheoryValue }; }
}

public sealed class RebirthConstitutionTrainingRuntimeState
{
    public double DirectWindowStartedActiveSeconds;
    public float HealingFractionInWindow;
    public float RawDirectAwardInWindow;
    public readonly Dictionary<string, double> ExposureFamilyReadyAtActiveSeconds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    public RebirthConstitutionTrainingRuntimeState Clone()
    {
        RebirthConstitutionTrainingRuntimeState copy = new RebirthConstitutionTrainingRuntimeState
        {
            DirectWindowStartedActiveSeconds = DirectWindowStartedActiveSeconds,
            HealingFractionInWindow = HealingFractionInWindow,
            RawDirectAwardInWindow = RawDirectAwardInWindow
        };
        foreach (KeyValuePair<string,double> pair in ExposureFamilyReadyAtActiveSeconds)
            copy.ExposureFamilyReadyAtActiveSeconds[pair.Key] = pair.Value;
        return copy;
    }
}

public sealed class RebirthSkillAntiRepeatRuntimeState
{
    // Canonical repeat-window state for Skill awards that specify a minimum interval.
    // Values use the owning character's persisted active-play clock, so reconnect/restart
    // cannot reset the window and offline time does not consume it.
    public readonly Dictionary<string, double> AwardReadyAtActiveSeconds = new Dictionary<string, double>(StringComparer.Ordinal);

    public RebirthSkillAntiRepeatRuntimeState Clone()
    {
        RebirthSkillAntiRepeatRuntimeState copy = new RebirthSkillAntiRepeatRuntimeState();
        foreach (KeyValuePair<string,double> pair in AwardReadyAtActiveSeconds)
            copy.AwardReadyAtActiveSeconds[pair.Key] = pair.Value;
        return copy;
    }
}

public sealed class RebirthCommerceItemTrainingRuntimeState
{
    public double LastBuyActiveSeconds = -1d;
    public double LastSellActiveSeconds = -1d;
    public double LastAwardActiveSeconds = -1d;
    public int RepeatChain;

    public RebirthCommerceItemTrainingRuntimeState Clone()
    {
        return new RebirthCommerceItemTrainingRuntimeState
        {
            LastBuyActiveSeconds = LastBuyActiveSeconds,
            LastSellActiveSeconds = LastSellActiveSeconds,
            LastAwardActiveSeconds = LastAwardActiveSeconds,
            RepeatChain = RepeatChain
        };
    }
}

public sealed class RebirthCommerceTrainingRuntimeState
{
    public double LastGlobalAwardActiveSeconds = -1d;
    public readonly Dictionary<string, RebirthCommerceItemTrainingRuntimeState> Items = new Dictionary<string, RebirthCommerceItemTrainingRuntimeState>(StringComparer.Ordinal);

    public RebirthCommerceTrainingRuntimeState Clone()
    {
        RebirthCommerceTrainingRuntimeState copy = new RebirthCommerceTrainingRuntimeState
        {
            LastGlobalAwardActiveSeconds = LastGlobalAwardActiveSeconds
        };
        foreach (KeyValuePair<string,RebirthCommerceItemTrainingRuntimeState> pair in Items)
            if (pair.Value != null) copy.Items[pair.Key] = pair.Value.Clone();
        return copy;
    }
}

public sealed class RebirthWorldProgressionState
{
    public readonly Dictionary<string,RebirthStationRecipeDiscoveryRecord> RecipeDiscoveries = new Dictionary<string,RebirthStationRecipeDiscoveryRecord>(StringComparer.Ordinal);
    public readonly Dictionary<string,RebirthStationDiscoveryAdmissionBinding> StationDiscoveryAdmissions = new Dictionary<string,RebirthStationDiscoveryAdmissionBinding>(StringComparer.Ordinal);
    public readonly Dictionary<string,RebirthStationGridAdmission> StationPreparations = new Dictionary<string,RebirthStationGridAdmission>(StringComparer.Ordinal);
    public RebirthTheoryStudyOutcome PendingTheoryStudy;
    public RebirthTheorySoloState SoloTheory;
    internal readonly Dictionary<string,RebirthStationCancellationAttempt> StationCancellationAttempts = new Dictionary<string,RebirthStationCancellationAttempt>(StringComparer.Ordinal);
    internal readonly Dictionary<string,RebirthStationCancellationPublication> StationCancellationPublications = new Dictionary<string,RebirthStationCancellationPublication>(StringComparer.Ordinal);
    internal readonly Dictionary<string,RebirthStationRefundDelivery> StationRefundDeliveries = new Dictionary<string,RebirthStationRefundDelivery>(StringComparer.Ordinal);
    internal readonly Dictionary<string,RebirthStationRefundArchive> StationRefundArchives = new Dictionary<string,RebirthStationRefundArchive>(StringComparer.Ordinal);
    internal readonly Dictionary<string,RebirthStationCancellationRefund> StationCancellationRefunds = new Dictionary<string,RebirthStationCancellationRefund>(StringComparer.Ordinal);
    public readonly Dictionary<string,RebirthStationTerminalIntent> StationTerminalIntents = new Dictionary<string,RebirthStationTerminalIntent>(StringComparer.Ordinal);
    internal readonly Dictionary<string,RebirthStationCompletionPublication> StationCompletionPublications = new Dictionary<string,RebirthStationCompletionPublication>(StringComparer.Ordinal);
    internal readonly Dictionary<string,RebirthStationCompletionExpectationProjection> StationCompletionExpectationProjections = new Dictionary<string,RebirthStationCompletionExpectationProjection>(StringComparer.Ordinal);
    public readonly Dictionary<string,RebirthStationPublicationRecord> StationPublications = new Dictionary<string,RebirthStationPublicationRecord>(StringComparer.Ordinal);
    public RebirthCookingMemory Cooking = new RebirthCookingMemory();
    public RebirthConstitutionTrainingRuntimeState ConstitutionTraining = new RebirthConstitutionTrainingRuntimeState();
    public RebirthSkillAntiRepeatRuntimeState SkillAntiRepeat = new RebirthSkillAntiRepeatRuntimeState();
    public RebirthCommerceTrainingRuntimeState CommerceTraining = new RebirthCommerceTrainingRuntimeState();
    public readonly Dictionary<string, RebirthAttributeRuntimeState> Attributes = new Dictionary<string, RebirthAttributeRuntimeState>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, RebirthSkillRuntimeState> Skills = new Dictionary<string, RebirthSkillRuntimeState>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, float> ImprovisationProgress = new Dictionary<string, float>(StringComparer.Ordinal);
    public readonly Dictionary<string, RebirthSkillKnowledgeRuntimeState> SkillKnowledge = new Dictionary<string, RebirthSkillKnowledgeRuntimeState>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> KnowledgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    // Normalized 0..1 physical-study progress. Completed titles are removed after the
    // corresponding Knowledge/read marker is committed.
    public readonly Dictionary<string, float> LiteratureStudyProgress = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> AcquiredDisciplineIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> AccomplishmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> CompletedTrialIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, RebirthWildTamingRuntimeState> WildTamingSessions = new Dictionary<string, RebirthWildTamingRuntimeState>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, RebirthTeachingLessonRuntimeState> TeachingLessons = new Dictionary<string, RebirthTeachingLessonRuntimeState>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, RebirthTeachingHistoryRuntimeState> TeachingHistory = new Dictionary<string, RebirthTeachingHistoryRuntimeState>(StringComparer.OrdinalIgnoreCase);
    // Bounded durable idempotency receipts for accepted progression events whose external
    // coordinator may retry after a crash (for example, a two-player teaching outcome).
    public readonly HashSet<string> SkillAwardReceipts = new HashSet<string>(StringComparer.Ordinal);
    public int WitchDoctorInitiationAttunements;
    public int BerserkerInitiationMeleeHits;
    public float HealthPotential;

    public RebirthWorldProgressionState Clone()
    {
        RebirthWorldProgressionState copy = new RebirthWorldProgressionState { HealthPotential = HealthPotential, WitchDoctorInitiationAttunements = WitchDoctorInitiationAttunements, BerserkerInitiationMeleeHits = BerserkerInitiationMeleeHits };
        foreach(var pair in RecipeDiscoveries) copy.RecipeDiscoveries.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationDiscoveryAdmissions) copy.StationDiscoveryAdmissions.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationPreparations) copy.StationPreparations.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationCancellationAttempts) copy.StationCancellationAttempts.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationCancellationPublications) copy.StationCancellationPublications.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationRefundDeliveries) copy.StationRefundDeliveries.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationRefundArchives) copy.StationRefundArchives.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationCancellationRefunds) copy.StationCancellationRefunds.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationTerminalIntents) copy.StationTerminalIntents.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationCompletionPublications) copy.StationCompletionPublications.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationCompletionExpectationProjections) copy.StationCompletionExpectationProjections.Add(pair.Key,pair.Value.Clone());
        foreach(var pair in StationPublications) copy.StationPublications.Add(pair.Key,pair.Value.Clone());
        copy.PendingTheoryStudy=PendingTheoryStudy?.Clone();
        copy.SoloTheory=SoloTheory?.Clone();
        copy.Cooking = Cooking.Clone();
        copy.ConstitutionTraining = ConstitutionTraining != null ? ConstitutionTraining.Clone() : new RebirthConstitutionTrainingRuntimeState();
        copy.SkillAntiRepeat = SkillAntiRepeat != null ? SkillAntiRepeat.Clone() : new RebirthSkillAntiRepeatRuntimeState();
        copy.CommerceTraining = CommerceTraining != null ? CommerceTraining.Clone() : new RebirthCommerceTrainingRuntimeState();
        foreach (KeyValuePair<string, RebirthAttributeRuntimeState> pair in Attributes) if (pair.Value != null) copy.Attributes[pair.Key] = pair.Value.Clone();
        foreach (KeyValuePair<string, RebirthSkillRuntimeState> pair in Skills) if (pair.Value != null) copy.Skills[pair.Key] = pair.Value.Clone();
        foreach (KeyValuePair<string, float> pair in ImprovisationProgress) copy.ImprovisationProgress[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, RebirthSkillKnowledgeRuntimeState> pair in SkillKnowledge) if (pair.Value != null) copy.SkillKnowledge[pair.Key] = pair.Value.Clone();
        foreach (string id in KnowledgeIds) copy.KnowledgeIds.Add(id);
        foreach (KeyValuePair<string,float> pair in LiteratureStudyProgress) copy.LiteratureStudyProgress[pair.Key]=pair.Value;
        foreach (string id in AcquiredDisciplineIds) copy.AcquiredDisciplineIds.Add(id);
        foreach (string id in AccomplishmentIds) copy.AccomplishmentIds.Add(id);
        foreach (string id in CompletedTrialIds) copy.CompletedTrialIds.Add(id);
        foreach (KeyValuePair<string,RebirthWildTamingRuntimeState> pair in WildTamingSessions) if(pair.Value!=null) copy.WildTamingSessions[pair.Key]=pair.Value.Clone();
        foreach (KeyValuePair<string,RebirthTeachingLessonRuntimeState> pair in TeachingLessons) if(pair.Value!=null) copy.TeachingLessons[pair.Key]=pair.Value.Clone();
        foreach (KeyValuePair<string,RebirthTeachingHistoryRuntimeState> pair in TeachingHistory) if(pair.Value!=null) copy.TeachingHistory[pair.Key]=pair.Value.Clone();
        foreach (string receipt in SkillAwardReceipts) copy.SkillAwardReceipts.Add(receipt);
        return copy;
    }
}

public sealed class RebirthRecentMealState
{
    public string SourceItemId;
    public string VarietyFamilyId;
    public float MoodQuality;
    public bool CompatibleWithDiet;
    public float AgeActiveSeconds;
    public RebirthRecentMealState Clone()
    {
        return new RebirthRecentMealState { SourceItemId = SourceItemId, VarietyFamilyId = VarietyFamilyId, MoodQuality = MoodQuality, CompatibleWithDiet = CompatibleWithDiet, AgeActiveSeconds = AgeActiveSeconds };
    }
}

public sealed class RebirthWorldConditionState
{
    public RebirthStressState Stress = new RebirthStressState();
    public float MoodCurrent = 50f;
    public float MoodTarget = 50f;
    public float DietSatisfaction = 50f;
    public float HealthCapacity;
    public float SevereDehydrationActiveSeconds;
    public float SevereMalnutritionActiveSeconds;
    public double ActivePlaySeconds;
    public ulong LastActiveWorldTime;
    public readonly List<RebirthRecentMealState> RecentMeals = new List<RebirthRecentMealState>();

    public RebirthWorldConditionState Clone()
    {
        RebirthWorldConditionState copy = new RebirthWorldConditionState
        {
            MoodCurrent = MoodCurrent,
            Stress = Stress.Clone(),
            MoodTarget = MoodTarget,
            DietSatisfaction = DietSatisfaction,
            HealthCapacity = HealthCapacity,
            SevereDehydrationActiveSeconds = SevereDehydrationActiveSeconds,
            SevereMalnutritionActiveSeconds = SevereMalnutritionActiveSeconds,
            ActivePlaySeconds = ActivePlaySeconds,
            LastActiveWorldTime = LastActiveWorldTime
        };
        for (int i = 0; i < RecentMeals.Count; i++) if (RecentMeals[i] != null) copy.RecentMeals.Add(RecentMeals[i].Clone());
        return copy;
    }
}

public sealed class RebirthTraitSupportRuntimeState
{
    public string SupportProfileId;
    public float GraceRemainingActiveSeconds;
    public float ManagedRemainingActiveSeconds;
    public float PositiveRemainingActiveSeconds;
    public float CooldownRemainingActiveSeconds;
    public int Stacks;
    public RebirthTraitSupportRuntimeState Clone()
    {
        return new RebirthTraitSupportRuntimeState
        {
            SupportProfileId = SupportProfileId,
            GraceRemainingActiveSeconds = GraceRemainingActiveSeconds,
            ManagedRemainingActiveSeconds = ManagedRemainingActiveSeconds,
            PositiveRemainingActiveSeconds = PositiveRemainingActiveSeconds,
            CooldownRemainingActiveSeconds = CooldownRemainingActiveSeconds,
            Stacks = Stacks
        };
    }
}

public sealed class RebirthWorldSupportState
{
    // Ordered cassette ownership is separate from the bounded equipment projection.
    public bool MusicShuffle = true;
    public long GearRevision;
    public RebirthGearTransferPhase GearTransferPhase;
    public RebirthGearTransferState PendingGearTransfer;
    public RebirthBackpackLibraryReceipt PendingLibraryTransfer;
    public RebirthBackpackLibraryPhase LibraryTransferPhase;
    public RebirthGearSettlement LastGearSettlement;
    public RebirthGearPreparationRefusal PendingGearPreparationRefusal;
    public RebirthGearPreparationRefusal LastGearPreparationRefusal;
    public RebirthGearTransferState LastGearSettlementOriginal;
    public RebirthBackpackLibrarySettlement LastLibrarySettlement;
    internal System.Xml.Linq.XElement RemoteResourceRefundJournalImage;
    public long MusicRevision;
    public RebirthMusicTransferState PendingMusicTransfer;
    public readonly List<RebirthMusicCassetteState> MusicCassettes = new List<RebirthMusicCassetteState>();
    public long AudiobookRevision;
    public readonly List<RebirthAudiobookCassetteState> AudiobookCassettes = new List<RebirthAudiobookCassetteState>();
    public readonly Dictionary<string, RebirthTraitSupportRuntimeState> Entries = new Dictionary<string, RebirthTraitSupportRuntimeState>(StringComparer.OrdinalIgnoreCase);
    // Lightweight REBIRTH-owned wearable/utility slots. Values are stable item IDs, not native armor/equipment objects.
    public readonly Dictionary<string, string> EquippedGearBySlot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    // Exact serialized ItemValue for equipped gear. ItemId remains the lightweight
    // projection used by rules/UI; this preserves quality, durability, seed, mods and metadata.
    public readonly Dictionary<string, string> EquippedGearItemDataBySlot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public RebirthWorldSupportState Clone()
    {
        RebirthWorldSupportState copy = new RebirthWorldSupportState();
        copy.MusicShuffle = MusicShuffle;
        copy.GearRevision = GearRevision;
        copy.GearTransferPhase = GearTransferPhase;
        copy.PendingGearTransfer = PendingGearTransfer; // Immutable payload, safe to share.
        copy.PendingGearPreparationRefusal = PendingGearPreparationRefusal; // Immutable unprepared original refusal.
        copy.LastGearPreparationRefusal = LastGearPreparationRefusal; // Immutable retired original acknowledgment.
        copy.LastGearSettlement = LastGearSettlement; // Immutable durable outcome.
        copy.LastGearSettlementOriginal = LastGearSettlementOriginal; // Immutable original, never permission to replay.
        copy.PendingLibraryTransfer=PendingLibraryTransfer; // Immutable receipt.
        copy.LibraryTransferPhase=LibraryTransferPhase;
        copy.LastLibrarySettlement=LastLibrarySettlement; // Immutable durable outcome.
        copy.RemoteResourceRefundJournalImage=RemoteResourceRefundJournalImage==null?null:new System.Xml.Linq.XElement(RemoteResourceRefundJournalImage);
        copy.MusicRevision = MusicRevision;
        copy.AudiobookRevision = AudiobookRevision;
        foreach(var cassette in AudiobookCassettes)
            if(cassette != null)copy.AudiobookCassettes.Add(cassette.Clone());
        copy.PendingMusicTransfer = PendingMusicTransfer?.Clone();
        foreach (var cassette in MusicCassettes)
            if (cassette != null) copy.MusicCassettes.Add(new RebirthMusicCassetteState { ItemId = cassette.ItemId, ItemData = cassette.ItemData });
        foreach (KeyValuePair<string, RebirthTraitSupportRuntimeState> pair in Entries) if (pair.Value != null) copy.Entries[pair.Key] = pair.Value.Clone();
        foreach (KeyValuePair<string, string> pair in EquippedGearBySlot) copy.EquippedGearBySlot[pair.Key] = pair.Value ?? string.Empty;
        foreach (KeyValuePair<string, string> pair in EquippedGearItemDataBySlot) copy.EquippedGearItemDataBySlot[pair.Key] = pair.Value ?? string.Empty;
        return copy;
    }
}

public sealed class RebirthMusicCassetteState
{
    public string ItemId = string.Empty;
    public string ItemData = string.Empty;
}

public sealed class RebirthWorldCharacterRecord
{
    public const int CurrentSchemaVersion = 19;

    public int SchemaVersion { get; private set; }
    public string StablePlayerId { get; private set; }
    public string StablePlayerKey { get; private set; }
    public long Revision { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ModifiedAtUtc { get; private set; }
    public RebirthWorldOriginSnapshot Origin { get; private set; }
    public RebirthWorldProgressionState Progression { get; private set; }
    public RebirthWorldConditionState Condition { get; private set; }
    public RebirthWorldSupportState Support { get; private set; }
    public int MigrationSourceSchema { get; private set; }
    public int MigrationTargetSchema { get; private set; }
    public string MigrationPolicyId { get; private set; }
    public bool MigrationApplied { get; private set; }
    public string MigrationOriginAudit { get; private set; }
    public string MigrationProgressionAudit { get; private set; }
    public bool Dirty { get; private set; }
    public string LastDirtyReason { get; private set; }

    public bool IsComplete
    {
        get
        {
            return Origin != null
                && !string.IsNullOrEmpty(Origin.CreationId)
                && !string.IsNullOrEmpty(Origin.DefinitionHash)
                && !string.IsNullOrEmpty(Origin.DefinitionVersion)
                && !string.IsNullOrEmpty(Origin.BackgroundId)
                && !string.IsNullOrEmpty(Origin.DietId);
        }
    }

    public RebirthWorldCharacterRecord(int schemaVersion, string stablePlayerId, string stablePlayerKey, long revision,
        DateTime createdAtUtc, DateTime modifiedAtUtc, RebirthWorldOriginSnapshot origin,
        RebirthWorldProgressionState progression, RebirthWorldConditionState condition, RebirthWorldSupportState support,
        int migrationSourceSchema = 0, int migrationTargetSchema = 0, string migrationPolicyId = "", bool migrationApplied = false,
        string migrationOriginAudit = "", string migrationProgressionAudit = "")
    {
        SchemaVersion = schemaVersion;
        StablePlayerId = stablePlayerId ?? string.Empty;
        StablePlayerKey = stablePlayerKey ?? string.Empty;
        Revision = Math.Max(0L, revision);
        CreatedAtUtc = NormalizeUtc(createdAtUtc);
        ModifiedAtUtc = NormalizeUtc(modifiedAtUtc);
        Origin = origin != null ? origin.Clone() : null;
        Progression = progression != null ? progression : new RebirthWorldProgressionState();
        Condition = condition != null ? condition : new RebirthWorldConditionState();
        Support = support != null ? support : new RebirthWorldSupportState();
        MigrationSourceSchema = migrationSourceSchema > 0 ? migrationSourceSchema : schemaVersion;
        MigrationTargetSchema = migrationTargetSchema > 0 ? migrationTargetSchema : schemaVersion;
        MigrationPolicyId = migrationPolicyId ?? string.Empty;
        MigrationApplied = migrationApplied;
        MigrationOriginAudit = migrationOriginAudit ?? string.Empty;
        MigrationProgressionAudit = migrationProgressionAudit ?? string.Empty;
    }

    public void Touch(string reason)
    {
        Revision = Revision == long.MaxValue ? long.MaxValue : Revision + 1L;
        ModifiedAtUtc = DateTime.UtcNow;
        Dirty = true;
        LastDirtyReason = reason ?? string.Empty;
    }

    public void MarkPersisted()
    {
        Dirty = false;
        LastDirtyReason = string.Empty;
    }

    public RebirthWorldCharacterRecord Clone()
    {
        RebirthWorldCharacterRecord copy = new RebirthWorldCharacterRecord(SchemaVersion, StablePlayerId, StablePlayerKey, Revision,
            CreatedAtUtc, ModifiedAtUtc, Origin, Progression != null ? Progression.Clone() : null,
            Condition != null ? Condition.Clone() : null, Support != null ? Support.Clone() : null,
            MigrationSourceSchema, MigrationTargetSchema, MigrationPolicyId, MigrationApplied, MigrationOriginAudit, MigrationProgressionAudit);
        copy.Dirty = Dirty;
        copy.LastDirtyReason = LastDirtyReason;
        return copy;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value == default(DateTime)) return DateTime.UtcNow;
        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
}

public sealed class RebirthWorldCharacterPersistenceIssue
{
    public string StablePlayerKey { get; private set; }
    public string Path { get; private set; }
    public string Reason { get; private set; }
    public bool RecoveredFromBackup { get; private set; }
    public RebirthWorldCharacterPersistenceIssue(string key, string path, string reason, bool recovered)
    { StablePlayerKey = key ?? string.Empty; Path = path ?? string.Empty; Reason = reason ?? string.Empty; RecoveredFromBackup = recovered; }
}

