using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#nullable disable

public static class RebirthSurvivorNetworkProtocol
{
    public const int Version = 12; // Owner snapshots now carry authoritative gear revision.
    public const int MaxTraitIds = 64;
    public const int MaxCreationChoices = 32;
    public const int MaxAttributes = 8;
    public const int MaxSkills = 64;
    public const int MaxSkillKnowledge = 64;
    public const int MaxKnowledgeIds = 1024;
    public const int MaxDisciplineIds = 16;
    public const int MaxSupportEntries = 32;
    public const int MaxGearSlots = 16;
    public const int MaxIdLength = 128;
    public const int MaxHashLength = 128;
    public const int MaxVersionLength = 96;
    public const int MaxProfileNameLength = 96;
    public const int MaxChoiceKeyLength = 96;
    public const int MaxChoiceValueLength = 256;
    public const int MaxErrorEntries = 64;
    public const int MaxCachedCreationResultsPerPlayer = 32;
}

public enum RebirthSurvivorCreationNetworkOperation : byte
{
    Validate = 0,
    Commit = 1
}

public enum RebirthSurvivorCreationNetworkStatus : byte
{
    None = 0,
    Validated = 1,
    Created = 2,
    AlreadyCreated = 3,
    RebirthModeDisabled = 4,
    ProtocolMismatch = 5,
    DefinitionMismatch = 6,
    MalformedRequest = 7,
    ValidationFailed = 8,
    Busy = 9,
    CommitServiceUnavailable = 10,
    CommitRejected = 11,
    CommitFailed = 12,
    InternalError = 13
}

public enum RebirthSurvivorCommitStatus : byte
{
    Created = 0,
    AlreadyCreated = 1,
    Rejected = 2,
    Failed = 3
}

public enum RebirthSurvivorOwnerCreationState : byte
{
    NotApplicable = 0,
    CreationRequired = 1,
    Ready = 2,
    RecoveryRequired = 3
}

public sealed class RebirthSurvivorCreationNetworkRequest
{
    public int ProtocolVersion = RebirthSurvivorNetworkProtocol.Version;
    public int PlayerEntityId;
    public ulong RequestId;
    public RebirthSurvivorCreationNetworkOperation Operation;
    public string ClientDefinitionHash = string.Empty;
    public string ClientDefinitionVersion = string.Empty;
    public string SourceProfileId = string.Empty;
    public string SourceProfileName = string.Empty;
    public string BackgroundId = string.Empty;
    public string DietId = string.Empty;
    public readonly List<string> TraitIds = new List<string>();
    public readonly Dictionary<string, string> CreationChoices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public bool Malformed;
    public string MalformedReason = string.Empty;

    public RebirthSurvivorCreationSelection ToSelection()
    {
        return new RebirthSurvivorCreationSelection(BackgroundId, DietId, TraitIds, ClientDefinitionHash);
    }

    public RebirthSurvivorCreationNetworkRequest Clone()
    {
        RebirthSurvivorCreationNetworkRequest copy = new RebirthSurvivorCreationNetworkRequest();
        copy.ProtocolVersion = ProtocolVersion;
        copy.PlayerEntityId = PlayerEntityId;
        copy.RequestId = RequestId;
        copy.Operation = Operation;
        copy.ClientDefinitionHash = ClientDefinitionHash ?? string.Empty;
        copy.ClientDefinitionVersion = ClientDefinitionVersion ?? string.Empty;
        copy.SourceProfileId = SourceProfileId ?? string.Empty;
        copy.SourceProfileName = SourceProfileName ?? string.Empty;
        copy.BackgroundId = BackgroundId ?? string.Empty;
        copy.DietId = DietId ?? string.Empty;
        copy.TraitIds.AddRange(TraitIds);
        foreach (KeyValuePair<string, string> pair in CreationChoices)
            copy.CreationChoices[pair.Key] = pair.Value;
        copy.Malformed = Malformed;
        copy.MalformedReason = MalformedReason ?? string.Empty;
        return copy;
    }
}


public static class RebirthSurvivorCreationRequestFingerprint
{
    public static string Compute(RebirthSurvivorCreationNetworkRequest request)
    {
        if (request == null) return string.Empty;
        StringBuilder b = new StringBuilder(2048);
        Add(b, "creation-request-fingerprint-v2");
        Add(b, request.ProtocolVersion.ToString());
        Add(b, ((byte)request.Operation).ToString());
        Add(b, request.ClientDefinitionHash);
        Add(b, request.ClientDefinitionVersion);
        Add(b, request.SourceProfileId);
        Add(b, request.SourceProfileName);
        Add(b, request.BackgroundId);
        Add(b, request.DietId);
        Add(b, request.Malformed ? "1" : "0");
        Add(b, request.MalformedReason);
        List<string> traits = new List<string>(request.TraitIds);
        traits.Sort(StringComparer.Ordinal);
        Add(b, "TRAITS"); Add(b, traits.Count.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < traits.Count; i++) Add(b, traits[i]);
        List<string> keys = new List<string>(request.CreationChoices.Keys);
        keys.Sort(StringComparer.Ordinal);
        Add(b, "CHOICES"); Add(b, keys.Count.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < keys.Count; i++)
        {
            Add(b, keys[i]);
            Add(b, request.CreationChoices[keys[i]]);
        }
        return RebirthStablePlayerIdentity.ComputeStorageKey(b.ToString());
    }

    private static void Add(StringBuilder b, string value)
    {
        string s = value ?? string.Empty;
        b.Append(s.Length).Append(':').Append(s).Append('|');
    }
}

public sealed class RebirthSurvivorCommitExecutionResult
{
    public RebirthSurvivorCommitStatus Status;
    public string MessageCode = string.Empty;
    public long CharacterRevision;

    public static RebirthSurvivorCommitExecutionResult Created(long revision)
    {
        return new RebirthSurvivorCommitExecutionResult { Status = RebirthSurvivorCommitStatus.Created, CharacterRevision = Math.Max(0L, revision), MessageCode = "created" };
    }

    public static RebirthSurvivorCommitExecutionResult AlreadyCreated(long revision)
    {
        return new RebirthSurvivorCommitExecutionResult { Status = RebirthSurvivorCommitStatus.AlreadyCreated, CharacterRevision = Math.Max(0L, revision), MessageCode = "already-created" };
    }

    public static RebirthSurvivorCommitExecutionResult Rejected(string code)
    {
        return new RebirthSurvivorCommitExecutionResult { Status = RebirthSurvivorCommitStatus.Rejected, MessageCode = code ?? "rejected" };
    }

    public static RebirthSurvivorCommitExecutionResult Failed(string code)
    {
        return new RebirthSurvivorCommitExecutionResult { Status = RebirthSurvivorCommitStatus.Failed, MessageCode = code ?? "failed" };
    }
}

public delegate RebirthSurvivorCommitExecutionResult RebirthSurvivorCreationCommitDelegate(
    EntityPlayer player,
    RebirthStablePlayerIdentity identity,
    RebirthSurvivorCreationNetworkRequest request,
    RebirthSurvivorCreationResult authoritativeValidation);

public sealed class RebirthSurvivorCreationNetworkResponse
{
    public int ProtocolVersion = RebirthSurvivorNetworkProtocol.Version;
    public ulong RequestId;
    public RebirthSurvivorCreationNetworkOperation Operation;
    public RebirthSurvivorCreationNetworkStatus Status;
    public bool WasReplay;
    public string ServerDefinitionHash = string.Empty;
    public string ServerDefinitionVersion = string.Empty;
    public long CharacterRevision;
    public string MessageCode = string.Empty;
    public RebirthSurvivorCreationResult ValidationResult;

    public bool IsSuccessful
    {
        get
        {
            return Status == RebirthSurvivorCreationNetworkStatus.Validated ||
                Status == RebirthSurvivorCreationNetworkStatus.Created ||
                Status == RebirthSurvivorCreationNetworkStatus.AlreadyCreated;
        }
    }

    public RebirthSurvivorCreationNetworkResponse Clone()
    {
        return new RebirthSurvivorCreationNetworkResponse
        {
            ProtocolVersion = ProtocolVersion,
            RequestId = RequestId,
            Operation = Operation,
            Status = Status,
            WasReplay = WasReplay,
            ServerDefinitionHash = ServerDefinitionHash ?? string.Empty,
            ServerDefinitionVersion = ServerDefinitionVersion ?? string.Empty,
            CharacterRevision = CharacterRevision,
            MessageCode = MessageCode ?? string.Empty,
            ValidationResult = RebirthSurvivorNetworkClone.CloneCreationResult(ValidationResult)
        };
    }
}

public sealed class RebirthSurvivorOwnerAttributeSnapshot
{
    public string Id = string.Empty;
    public float Current;
    public float Potential;
    public RebirthSurvivorOwnerAttributeSnapshot Clone() { return new RebirthSurvivorOwnerAttributeSnapshot { Id = Id ?? string.Empty, Current = Current, Potential = Potential }; }
}

public sealed class RebirthSurvivorOwnerSkillSnapshot
{
    public string Id = string.Empty;
    public float Value;
    public float Progress;
    public RebirthSurvivorOwnerSkillSnapshot Clone() { return new RebirthSurvivorOwnerSkillSnapshot { Id = Id ?? string.Empty, Value = Value, Progress = Progress }; }
}

public sealed class RebirthSurvivorOwnerSkillKnowledgeSnapshot
{
    public string Id = string.Empty;
    public float Value;
    public RebirthSurvivorOwnerSkillKnowledgeSnapshot Clone() { return new RebirthSurvivorOwnerSkillKnowledgeSnapshot { Id = Id ?? string.Empty, Value = Value }; }
}

public sealed class RebirthSurvivorOwnerSupportSnapshot
{
    public string ProfileId = string.Empty;
    public float GraceRemainingActiveSeconds;
    public float ManagedRemainingActiveSeconds;
    public float PositiveRemainingActiveSeconds;
    public float CooldownRemainingActiveSeconds;
    public int Stacks;
    public RebirthSurvivorOwnerSupportSnapshot Clone()
    {
        return new RebirthSurvivorOwnerSupportSnapshot
        {
            ProfileId = ProfileId ?? string.Empty,
            GraceRemainingActiveSeconds = GraceRemainingActiveSeconds,
            ManagedRemainingActiveSeconds = ManagedRemainingActiveSeconds,
            PositiveRemainingActiveSeconds = PositiveRemainingActiveSeconds,
            CooldownRemainingActiveSeconds = CooldownRemainingActiveSeconds,
            Stacks = Stacks
        };
    }
}


public sealed class RebirthSurvivorOwnerGearSnapshot
{
    public string SlotId = string.Empty;
    public string ItemId = string.Empty;
    public RebirthSurvivorOwnerGearSnapshot Clone() { return new RebirthSurvivorOwnerGearSnapshot { SlotId = SlotId ?? string.Empty, ItemId = ItemId ?? string.Empty }; }
}

/// <summary>
/// Compact owning-client Survivor projection. Deliberately excludes meal history, persistence
/// paths, stable player identity, and metabolism internals (which have their own snapshot).
/// </summary>
public sealed class RebirthSurvivorOwnerStateSnapshot
{
    public int ProtocolVersion = RebirthSurvivorNetworkProtocol.Version;
    public bool RebirthModeEnabled;
    public bool HasCharacter;
    public RebirthSurvivorOwnerCreationState CreationState;
    public long CharacterRevision;
    public long GearRevision = -1; // Unknown until an authoritative v12 owner producer supplies it.
    public string CreationId = string.Empty;
    public string ServerDefinitionHash = string.Empty;
    public string ServerDefinitionVersion = string.Empty;
    public string OriginDefinitionHash = string.Empty;
    public string OriginDefinitionVersion = string.Empty;
    public string SourceProfileName = string.Empty;
    public string BackgroundId = string.Empty;
    public string DietId = string.Empty;
    public readonly List<string> TraitIds = new List<string>();
    public float HealthPotential;
    public float MoodCurrent;
    public float MoodTarget;
    public float DietSatisfaction;
    public float HealthCapacity;
    public int RecentMealCount;
    public int RecentVarietyCount;
    public bool LastMealCompatible;
    public string MoodPositiveCauseId = string.Empty;
    public float MoodPositiveCauseDelta;
    public string MoodNegativeCauseId = string.Empty;
    public float MoodNegativeCauseDelta;
    public readonly List<RebirthSurvivorOwnerAttributeSnapshot> Attributes = new List<RebirthSurvivorOwnerAttributeSnapshot>();
    public readonly List<RebirthSurvivorOwnerSkillSnapshot> Skills = new List<RebirthSurvivorOwnerSkillSnapshot>();
    public readonly List<RebirthSurvivorOwnerSkillKnowledgeSnapshot> SkillKnowledge = new List<RebirthSurvivorOwnerSkillKnowledgeSnapshot>();
    public readonly Dictionary<string,float> ImprovisationProgress = new Dictionary<string,float>(StringComparer.Ordinal);
    public readonly List<string> KnowledgeIds = new List<string>();
    public readonly List<string> AcquiredDisciplineIds = new List<string>();
    public readonly List<string> AccomplishmentIds = new List<string>();
    public readonly List<string> CompletedTrialIds = new List<string>();
    public readonly List<RebirthSurvivorOwnerSupportSnapshot> SupportEntries = new List<RebirthSurvivorOwnerSupportSnapshot>();
    public readonly List<RebirthSurvivorOwnerGearSnapshot> GearSlots = new List<RebirthSurvivorOwnerGearSnapshot>();
    public int PhysicalBagSlots;

    public bool DefinitionsCompatible
    {
        get
        {
            return string.Equals(ServerDefinitionHash ?? string.Empty,
                RebirthSurvivorDefinitionRegistry.SemanticHash ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public RebirthSurvivorOwnerStateSnapshot Clone()
    {
        RebirthSurvivorOwnerStateSnapshot copy = new RebirthSurvivorOwnerStateSnapshot();
        copy.ProtocolVersion = ProtocolVersion;
        copy.RebirthModeEnabled = RebirthModeEnabled;
        copy.HasCharacter = HasCharacter;
        copy.CreationState = CreationState;
        copy.CharacterRevision = CharacterRevision;
        copy.GearRevision = GearRevision;
        copy.CreationId = CreationId ?? string.Empty;
        copy.ServerDefinitionHash = ServerDefinitionHash ?? string.Empty;
        copy.ServerDefinitionVersion = ServerDefinitionVersion ?? string.Empty;
        copy.OriginDefinitionHash = OriginDefinitionHash ?? string.Empty;
        copy.OriginDefinitionVersion = OriginDefinitionVersion ?? string.Empty;
        copy.SourceProfileName = SourceProfileName ?? string.Empty;
        copy.BackgroundId = BackgroundId ?? string.Empty;
        copy.DietId = DietId ?? string.Empty;
        copy.TraitIds.AddRange(TraitIds);
        copy.HealthPotential = HealthPotential;
        copy.MoodCurrent = MoodCurrent;
        copy.MoodTarget = MoodTarget;
        copy.DietSatisfaction = DietSatisfaction;
        copy.HealthCapacity = HealthCapacity;
        copy.RecentMealCount = RecentMealCount;
        copy.RecentVarietyCount = RecentVarietyCount;
        copy.LastMealCompatible = LastMealCompatible;
        copy.MoodPositiveCauseId = MoodPositiveCauseId ?? string.Empty;
        copy.MoodPositiveCauseDelta = MoodPositiveCauseDelta;
        copy.MoodNegativeCauseId = MoodNegativeCauseId ?? string.Empty;
        copy.MoodNegativeCauseDelta = MoodNegativeCauseDelta;
        for (int i = 0; i < Attributes.Count; i++) if (Attributes[i] != null) copy.Attributes.Add(Attributes[i].Clone());
        for (int i = 0; i < Skills.Count; i++) if (Skills[i] != null) copy.Skills.Add(Skills[i].Clone());
        for (int i = 0; i < SkillKnowledge.Count; i++) if (SkillKnowledge[i] != null) copy.SkillKnowledge.Add(SkillKnowledge[i].Clone());
        foreach(var pair in ImprovisationProgress) copy.ImprovisationProgress[pair.Key]=pair.Value;
        copy.KnowledgeIds.AddRange(KnowledgeIds);
        copy.AcquiredDisciplineIds.AddRange(AcquiredDisciplineIds);
        copy.AccomplishmentIds.AddRange(AccomplishmentIds);
        copy.CompletedTrialIds.AddRange(CompletedTrialIds);
        for (int i = 0; i < SupportEntries.Count; i++) if (SupportEntries[i] != null) copy.SupportEntries.Add(SupportEntries[i].Clone());
        for (int i = 0; i < GearSlots.Count; i++) if (GearSlots[i] != null) copy.GearSlots.Add(GearSlots[i].Clone());
        copy.PhysicalBagSlots = PhysicalBagSlots;
        return copy;
    }
}

public static class RebirthSurvivorNetworkClone
{
    public static RebirthSurvivorCreationResult CloneCreationResult(RebirthSurvivorCreationResult source)
    {
        if (source == null) return null;
        List<string> traits = new List<string>(source.TraitIds);
        List<RebirthResolvedAttributeStart> attrs = new List<RebirthResolvedAttributeStart>();
        for (int i = 0; i < source.Attributes.Count; i++)
        {
            RebirthResolvedAttributeStart a = source.Attributes[i];
            attrs.Add(new RebirthResolvedAttributeStart(a.AttributeId, a.Current, a.Potential));
        }
        Dictionary<string, float> skills = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, float> pair in source.StartingSkills) skills[pair.Key] = pair.Value;
        Dictionary<string, float> skillKnowledge = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, float> pair in source.StartingSkillKnowledge) skillKnowledge[pair.Key] = pair.Value;
        List<string> knowledge = new List<string>(source.StartingKnowledgeIds);
        List<RebirthSurvivorCreationError> errors = new List<RebirthSurvivorCreationError>();
        for (int i = 0; i < source.Errors.Count; i++)
        {
            RebirthSurvivorCreationError e = source.Errors[i];
            errors.Add(new RebirthSurvivorCreationError(e.Code, e.SubjectId, e.RelatedId));
        }
        return new RebirthSurvivorCreationResult(source.DefinitionHash, source.DefinitionVersion, source.BackgroundId,
            source.DietId, traits, source.RemainingCreationPoints, attrs, source.HealthPotential,
            source.UnencumberedSlotDelta, skills, skillKnowledge, knowledge, errors);
    }
}
