using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public enum RebirthSurvivorCreationErrorCode
{
    RebirthModeDisabled,
    DefinitionsUnavailable,
    DefinitionHashMismatch,
    MissingBackground,
    UnknownBackground,
    MissingDiet,
    UnknownDiet,
    DuplicateTrait,
    UnknownTrait,
    TraitNotAllowedForBackground,
    TraitBlockedByBackground,
    TraitConflict,
    TraitDietConflict,
    InsufficientCreationPoints,
    UnspentCreationPoints,
    AttributeOutOfRange,
    PotentialBelowCurrent,
    UnknownCreationModifier,
    InvalidSkillBiasTier
}

public sealed class RebirthSurvivorCreationError
{
    public RebirthSurvivorCreationErrorCode Code { get; private set; }
    public string SubjectId { get; private set; }
    public string RelatedId { get; private set; }
    public string MessageKey { get { return "xuiRebirthCreationError" + Code; } }
    public RebirthSurvivorCreationError(RebirthSurvivorCreationErrorCode code,string subjectId,string relatedId) { Code=code;SubjectId=subjectId??string.Empty;RelatedId=relatedId??string.Empty; }
    public override string ToString() { return Code+ (SubjectId.Length>0?" subject="+SubjectId:string.Empty) + (RelatedId.Length>0?" related="+RelatedId:string.Empty); }
}

public sealed class RebirthResolvedAttributeStart
{
    public string AttributeId { get; private set; }
    public float Current { get; private set; }
    public float Potential { get; private set; }
    public RebirthResolvedAttributeStart(string id,float current,float potential) { AttributeId=id??string.Empty;Current=current;Potential=potential; }
}

public sealed class RebirthSurvivorCreationResult
{
    public bool IsValid { get { return Errors.Count == 0; } }
    public string DefinitionHash { get; private set; }
    public string DefinitionVersion { get; private set; }
    public string BackgroundId { get; private set; }
    public string DietId { get; private set; }
    public ReadOnlyCollection<string> TraitIds { get; private set; }
    public int RemainingCreationPoints { get; private set; }
    public ReadOnlyCollection<RebirthResolvedAttributeStart> Attributes { get; private set; }
    public float HealthPotential { get; private set; }
    public int UnencumberedSlotDelta { get; private set; }
    public ReadOnlyDictionary<string,float> StartingSkills { get; private set; }
    public ReadOnlyDictionary<string,float> StartingSkillKnowledge { get; private set; }
    public ReadOnlyCollection<string> StartingKnowledgeIds { get; private set; }
    public ReadOnlyCollection<RebirthSurvivorCreationError> Errors { get; private set; }

    public RebirthSurvivorCreationResult(string hash,string version,string backgroundId,string dietId,IList<string> traitIds,int points,
        IList<RebirthResolvedAttributeStart> attributes,float healthPotential,int slotDelta,IDictionary<string,float> skills,IDictionary<string,float> skillKnowledge,IList<string> knowledge,IList<RebirthSurvivorCreationError> errors)
    {
        DefinitionHash=hash??string.Empty;DefinitionVersion=version??string.Empty;BackgroundId=backgroundId??string.Empty;DietId=dietId??string.Empty;
        TraitIds=new ReadOnlyCollection<string>(new List<string>(traitIds??new List<string>()));RemainingCreationPoints=points;
        Attributes=new ReadOnlyCollection<RebirthResolvedAttributeStart>(new List<RebirthResolvedAttributeStart>(attributes??new List<RebirthResolvedAttributeStart>()));HealthPotential=healthPotential;UnencumberedSlotDelta=slotDelta;
        StartingSkills=new ReadOnlyDictionary<string,float>(new Dictionary<string,float>(skills??new Dictionary<string,float>(),StringComparer.OrdinalIgnoreCase));
        StartingSkillKnowledge=new ReadOnlyDictionary<string,float>(new Dictionary<string,float>(skillKnowledge??new Dictionary<string,float>(),StringComparer.OrdinalIgnoreCase));
        StartingKnowledgeIds=new ReadOnlyCollection<string>(new List<string>(knowledge??new List<string>()));Errors=new ReadOnlyCollection<RebirthSurvivorCreationError>(new List<RebirthSurvivorCreationError>(errors??new List<RebirthSurvivorCreationError>()));
    }
}
