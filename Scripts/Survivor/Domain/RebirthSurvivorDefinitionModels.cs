using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public sealed class RebirthStartingSkillBiasDefinition
{
    public string SkillId { get; private set; }
    public string TierId { get; private set; }
    public float Value { get; private set; }
    public bool HasExplicitValue { get; private set; }

    // Tier-based authoring remains supported for existing Trait creation modifiers.
    public RebirthStartingSkillBiasDefinition(string skillId, string tierId)
        : this(skillId, tierId, 0f, false) { }

    public RebirthStartingSkillBiasDefinition(string skillId, string tierId, float value, bool hasExplicitValue)
    {
        SkillId = skillId ?? string.Empty;
        TierId = tierId ?? string.Empty;
        Value = value;
        HasExplicitValue = hasExplicitValue;
    }
}


public sealed class RebirthStartingSkillKnowledgeDefinition
{
    public string SkillId { get; private set; }
    public float Value { get; private set; }

    public RebirthStartingSkillKnowledgeDefinition(string skillId, float value)
    {
        SkillId = skillId ?? string.Empty;
        Value = value;
    }
}

public sealed class RebirthStartingItemDefinition
{
    public string ItemId { get; private set; }
    public string NameKey { get; private set; }
    public int Count { get; private set; }
    public int Quality { get; private set; }
    public bool HasQuality { get; private set; }
    public bool IsBlock { get; private set; }

    public RebirthStartingItemDefinition(string itemId, string nameKey, int count, int quality, bool hasQuality, bool isBlock)
    {
        ItemId = itemId ?? string.Empty;
        NameKey = nameKey ?? string.Empty;
        Count = Math.Max(1, count);
        Quality = Math.Max(1, quality);
        HasQuality = hasQuality;
        IsBlock = isBlock;
    }
}

public sealed class RebirthStartingAttributeDefinition
{
    public string AttributeId { get; private set; }
    public float Current { get; private set; }
    public RebirthStartingAttributeDefinition(string attributeId, float current)
    { AttributeId = attributeId ?? string.Empty; Current = current; }
}

public sealed class RebirthBackgroundDefinition
{
    public string Id { get; private set; }
    public string NameKey { get; private set; }
    public string DescriptionKey { get; private set; }
    public string BackgroundArtKey { get; private set; }
    public string BackgroundArtAltTextKey { get; private set; }
    public int CreationPointModifier { get; private set; }
    public ReadOnlyCollection<string> RestrictedTraitIds { get; private set; }
    public ReadOnlyCollection<string> FavoredTraitIds { get; private set; }
    public ReadOnlyCollection<string> BlockedTraitIds { get; private set; }
    public ReadOnlyCollection<RebirthStartingAttributeDefinition> StartingAttributes { get; private set; }
    public ReadOnlyCollection<RebirthStartingSkillBiasDefinition> StartingSkills { get; private set; }
    public ReadOnlyCollection<RebirthStartingSkillKnowledgeDefinition> StartingSkillKnowledge { get; private set; }
    public ReadOnlyCollection<string> StartingKnowledgeIds { get; private set; }
    public ReadOnlyCollection<RebirthStartingItemDefinition> StartingItems { get; private set; }
    public string Identity { get; private set; }
    public string StartingExperience { get; private set; }
    public string DesignNote { get; private set; }

    public RebirthBackgroundDefinition(string id, string nameKey, string descriptionKey, string artKey, string altKey, int pointModifier,
        IList<string> restricted, IList<string> favored, IList<string> blocked, IList<RebirthStartingSkillBiasDefinition> skills,
        IList<RebirthStartingSkillKnowledgeDefinition> skillKnowledge, IList<string> knowledge, IList<RebirthStartingItemDefinition> startingItems, string identity, string startingExperience, string designNote, IList<RebirthStartingAttributeDefinition> startingAttributes = null)
    {
        Id = id ?? string.Empty; NameKey = nameKey ?? string.Empty; DescriptionKey = descriptionKey ?? string.Empty;
        BackgroundArtKey = artKey ?? string.Empty; BackgroundArtAltTextKey = altKey ?? string.Empty; CreationPointModifier = pointModifier;
        RestrictedTraitIds = Freeze(restricted); FavoredTraitIds = Freeze(favored); BlockedTraitIds = Freeze(blocked);
        StartingAttributes = new ReadOnlyCollection<RebirthStartingAttributeDefinition>(new List<RebirthStartingAttributeDefinition>(startingAttributes ?? new List<RebirthStartingAttributeDefinition>()));
        StartingSkills = new ReadOnlyCollection<RebirthStartingSkillBiasDefinition>(new List<RebirthStartingSkillBiasDefinition>(skills ?? new List<RebirthStartingSkillBiasDefinition>()));
        StartingSkillKnowledge = new ReadOnlyCollection<RebirthStartingSkillKnowledgeDefinition>(new List<RebirthStartingSkillKnowledgeDefinition>(skillKnowledge ?? new List<RebirthStartingSkillKnowledgeDefinition>()));
        StartingKnowledgeIds = Freeze(knowledge); StartingItems = new ReadOnlyCollection<RebirthStartingItemDefinition>(new List<RebirthStartingItemDefinition>(startingItems ?? new List<RebirthStartingItemDefinition>())); Identity = identity ?? string.Empty; StartingExperience = startingExperience ?? string.Empty; DesignNote = designNote ?? string.Empty;
    }

    private static ReadOnlyCollection<string> Freeze(IList<string> source) { return new ReadOnlyCollection<string>(new List<string>(source ?? new List<string>())); }
}

public sealed class RebirthTraitDefinition
{
    public string Id { get; private set; }
    public string NameKey { get; private set; }
    public string DescriptionKey { get; private set; }
    public string Category { get; private set; }
    public RebirthTraitPolarity Polarity { get; private set; }
    public int Points { get; private set; }
    public RebirthDefinitionAvailability Availability { get; private set; }
    public string IconKey { get; private set; }
    public string ModifierId { get; private set; }
    public ReadOnlyCollection<string> AllowedBackgroundIds { get; private set; }
    public ReadOnlyCollection<string> ConflictTraitIds { get; private set; }
    public ReadOnlyCollection<string> ConflictDietIds { get; private set; }
    public string EffectSummary { get; private set; }
    public string ImplementationSurface { get; private set; }

    public int BudgetDelta { get { return Polarity == RebirthTraitPolarity.Positive ? -Points : Polarity == RebirthTraitPolarity.Negative ? Points : 0; } }

    public RebirthTraitDefinition(string id, string nameKey, string descriptionKey, string category, RebirthTraitPolarity polarity, int points,
        RebirthDefinitionAvailability availability, string iconKey, string modifierId, IList<string> allowedBackgrounds,
        IList<string> conflicts, IList<string> dietConflicts, string effectSummary, string implementationSurface)
    {
        Id=id??string.Empty; NameKey=nameKey??string.Empty; DescriptionKey=descriptionKey??string.Empty; Category=category??string.Empty;
        Polarity=polarity; Points=Math.Max(0, points); Availability=availability; IconKey=iconKey??string.Empty; ModifierId=modifierId??string.Empty;
        AllowedBackgroundIds=Freeze(allowedBackgrounds); ConflictTraitIds=Freeze(conflicts); ConflictDietIds=Freeze(dietConflicts);
        EffectSummary=effectSummary??string.Empty; ImplementationSurface=implementationSurface??string.Empty;
    }
    private static ReadOnlyCollection<string> Freeze(IList<string> source) { return new ReadOnlyCollection<string>(new List<string>(source ?? new List<string>())); }
}

public sealed class RebirthDietDefinition
{
    public string Id { get; private set; }
    public string NameKey { get; private set; }
    public string DescriptionKey { get; private set; }
    public int Points { get; private set; }
    public string IconKey { get; private set; }
    public string CompositionRule { get; private set; }
    public string RuleSummary { get; private set; }
    public RebirthDietDefinition(string id,string nameKey,string descriptionKey,int points,string iconKey,string compositionRule,string ruleSummary)
    { Id=id??string.Empty; NameKey=nameKey??string.Empty; DescriptionKey=descriptionKey??string.Empty; Points=points; IconKey=iconKey??string.Empty; CompositionRule=compositionRule??string.Empty; RuleSummary=ruleSummary??string.Empty; }
}

public sealed class RebirthAttributeDefinition
{
    public string Id { get; private set; }
    public string NameKey { get; private set; }
    public float BaseCurrent { get; private set; }
    public float BasePotential { get; private set; }
    public float Min { get; private set; }
    public float Max { get; private set; }
    public RebirthAttributeDefinition(string id,string nameKey,float baseCurrent,float basePotential,float min,float max) { Id=id??string.Empty;NameKey=nameKey??string.Empty;BaseCurrent=baseCurrent;BasePotential=basePotential;Min=min;Max=max; }
}

public sealed class RebirthSkillDefinition
{
    public string Id { get; private set; }
    public string NameKey { get; private set; }
    public string SourceKey { get; private set; }
    public string LearnByDoingSource { get; private set; }
    public string Feasibility { get; private set; }
    public float Min { get; private set; }
    public float Max { get; private set; }
    public bool Advanced { get; private set; }
    public string PrimaryAttributeId { get; private set; }
    public RebirthSkillDefinition(string id,string nameKey,string sourceKey,string lbd,string feasibility,float min,float max,string primaryAttributeId,bool advanced=false) { Id=id??string.Empty;NameKey=nameKey??string.Empty;SourceKey=sourceKey??string.Empty;LearnByDoingSource=lbd??string.Empty;Feasibility=feasibility??string.Empty;Min=min;Max=max;PrimaryAttributeId=primaryAttributeId??string.Empty;Advanced=advanced; }
}

public sealed class RebirthKnowledgeDefinition
{
    public string Id { get; private set; }
    public string NameKey { get; private set; }
    public ReadOnlyCollection<string> AssociatedSkillIds { get; private set; }
    public string ExplorerStatus { get; private set; }
    public RebirthKnowledgeDefinition(string id,string nameKey,IList<string> associatedSkillIds,string explorerStatus)
    {
        Id=id??string.Empty;NameKey=nameKey??string.Empty;
        AssociatedSkillIds=new ReadOnlyCollection<string>(new List<string>(associatedSkillIds??new List<string>()));
        ExplorerStatus=explorerStatus??string.Empty;
    }
}

public sealed class RebirthProgressionDefinition
{
    public int BaseCreationPoints { get; private set; }
    public int CleanSlateBonus { get; private set; }
    public int MaxNegativeTraitRefund { get; private set; }
    public float CreationSkillMin { get; private set; }
    public float CreationSkillMax { get; private set; }
    public float SkillKnowledgeMin { get; private set; }
    public float SkillKnowledgeMax { get; private set; }
    public float BaseHealthPotential { get; private set; }
    public float MinHealthPotential { get; private set; }
    public float MaxHealthPotential { get; private set; }
    public ReadOnlyCollection<RebirthAttributeDefinition> Attributes { get; private set; }
    public ReadOnlyDictionary<string, int> SkillBiasTiers { get; private set; }
    public ReadOnlyCollection<RebirthSkillDefinition> Skills { get; private set; }
    public ReadOnlyCollection<RebirthKnowledgeDefinition> Knowledge { get; private set; }

    public RebirthProgressionDefinition(int basePoints,int cleanSlateBonus,int maxNegativeTraitRefund,float creationSkillMin,float creationSkillMax,float skillKnowledgeMin,float skillKnowledgeMax,float baseHealth,float minHealth,float maxHealth,
        IList<RebirthAttributeDefinition> attributes, IDictionary<string,int> tiers, IList<RebirthSkillDefinition> skills, IList<RebirthKnowledgeDefinition> knowledge)
    {
        BaseCreationPoints=basePoints;CleanSlateBonus=cleanSlateBonus;MaxNegativeTraitRefund=Math.Max(-1,maxNegativeTraitRefund);CreationSkillMin=creationSkillMin;CreationSkillMax=creationSkillMax;SkillKnowledgeMin=skillKnowledgeMin;SkillKnowledgeMax=skillKnowledgeMax;BaseHealthPotential=baseHealth;MinHealthPotential=minHealth;MaxHealthPotential=maxHealth;
        Attributes=new ReadOnlyCollection<RebirthAttributeDefinition>(new List<RebirthAttributeDefinition>(attributes??new List<RebirthAttributeDefinition>()));
        SkillBiasTiers=new ReadOnlyDictionary<string,int>(new Dictionary<string,int>(tiers??new Dictionary<string,int>(),StringComparer.OrdinalIgnoreCase));
        Skills=new ReadOnlyCollection<RebirthSkillDefinition>(new List<RebirthSkillDefinition>(skills??new List<RebirthSkillDefinition>()));
        Knowledge=new ReadOnlyCollection<RebirthKnowledgeDefinition>(new List<RebirthKnowledgeDefinition>(knowledge??new List<RebirthKnowledgeDefinition>()));
    }
}

public sealed class RebirthConditionCapGroupDefinition
{
    public string Id { get; private set; } public string Description { get; private set; } public string TuningState { get; private set; }
    public RebirthConditionCapGroupDefinition(string id,string description,string tuningState) { Id=id??string.Empty;Description=description??string.Empty;TuningState=tuningState??string.Empty; }
}

public sealed class RebirthConditionModifierComponent
{
    public string Target { get; private set; } public string Operation { get; private set; } public string Value { get; private set; }
    public string Unit { get; private set; } public string Phase { get; private set; } public string Note { get; private set; }
    public RebirthConditionModifierComponent(string target,string operation,string value,string unit,string phase,string note) { Target=target??string.Empty;Operation=operation??string.Empty;Value=value??string.Empty;Unit=unit??string.Empty;Phase=phase??string.Empty;Note=note??string.Empty; }
}

public sealed class RebirthConditionModifierProfileDefinition
{
    public string Id { get; private set; } public string OwnerTraitId { get; private set; } public string EffectSummary { get; private set; }
    public string ImplementationSurface { get; private set; } public string ImplementationState { get; private set; } public string CapGroup { get; private set; }
    public ReadOnlyCollection<RebirthConditionModifierComponent> Components { get; private set; }
    public RebirthConditionModifierProfileDefinition(string id,string owner,string summary,string surface,string state,string cap,IList<RebirthConditionModifierComponent> components)
    { Id=id??string.Empty;OwnerTraitId=owner??string.Empty;EffectSummary=summary??string.Empty;ImplementationSurface=surface??string.Empty;ImplementationState=state??string.Empty;CapGroup=cap??string.Empty;Components=new ReadOnlyCollection<RebirthConditionModifierComponent>(new List<RebirthConditionModifierComponent>(components??new List<RebirthConditionModifierComponent>())); }
}

public sealed class RebirthTraitSupportEffectDefinition
{
    public string Target { get; private set; }
    public string Operation { get; private set; }
    public float Value { get; private set; }
    public string Scope { get; private set; }
    public string State { get; private set; }
    public string Note { get; private set; }
    internal string NormalizedTarget { get; private set; }
    internal string NormalizedState { get; private set; }
    public RebirthTraitSupportEffectDefinition(string target,string operation,float value,string scope,string state,string note)
    { Target=target??string.Empty;Operation=operation??string.Empty;Value=value;Scope=scope??string.Empty;State=state??string.Empty;Note=note??string.Empty;NormalizedTarget=Target.Trim().ToLowerInvariant();NormalizedState=State.Trim().ToLowerInvariant(); }
}

public sealed class RebirthTraitSupportProfileDefinition
{
    public string Id { get; private set; } public string NameKey { get; private set; } public string Kind { get; private set; }
    public string EffectSummary { get; private set; } public string TimingState { get; private set; } public string RepeatMode { get; private set; }
    public string HabitTraitId { get; private set; } public string GlobalCapGroup { get; private set; } public string EquipmentCVar { get; private set; }
    public string GearSlotId { get; private set; } public string GearSlotNameKey { get; private set; } public string GearItemId { get; private set; }
    public int GearToolbeltSlotBonus { get; private set; }
    public int GearBagSlotBonus { get; private set; } public float GearMinStrength { get; private set; } public float GearMinConstitution { get; private set; }
    public float GraceSeconds { get; private set; } public float ManagedSeconds { get; private set; } public float PositiveSeconds { get; private set; } public float CooldownSeconds { get; private set; }
    public ReadOnlyCollection<string> SupportedTraitIds { get; private set; }
    public ReadOnlyCollection<string> ItemBindings { get; private set; }
    public ReadOnlyCollection<RebirthTraitSupportEffectDefinition> Effects { get; private set; }
    public RebirthTraitSupportProfileDefinition(string id,string nameKey,string kind,string summary,string timing,string repeat,string habitTraitId,string capGroup,string equipmentCVar,
        string gearSlotId,string gearSlotNameKey,string gearItemId,int gearBagSlotBonus,float gearMinStrength,float gearMinConstitution,float graceSeconds,float managedSeconds,float positiveSeconds,float cooldownSeconds,IList<string> traits,IList<string> items,IList<RebirthTraitSupportEffectDefinition> effects, int gearToolbeltSlotBonus = 0)
    {
        Id=id??string.Empty;NameKey=nameKey??string.Empty;Kind=kind??string.Empty;EffectSummary=summary??string.Empty;TimingState=timing??string.Empty;RepeatMode=repeat??string.Empty;
        HabitTraitId=habitTraitId??string.Empty;GlobalCapGroup=capGroup??string.Empty;EquipmentCVar=equipmentCVar??string.Empty;GearSlotId=gearSlotId??string.Empty;GearSlotNameKey=gearSlotNameKey??string.Empty;GearItemId=gearItemId??string.Empty;
        GearToolbeltSlotBonus=Math.Max(0,gearToolbeltSlotBonus);GearBagSlotBonus=Math.Max(0,gearBagSlotBonus);GearMinStrength=Math.Max(0f,gearMinStrength);GearMinConstitution=Math.Max(0f,gearMinConstitution);
        GraceSeconds=Math.Max(0f,graceSeconds);ManagedSeconds=Math.Max(0f,managedSeconds);PositiveSeconds=Math.Max(0f,positiveSeconds);CooldownSeconds=Math.Max(0f,cooldownSeconds);
        SupportedTraitIds=new ReadOnlyCollection<string>(new List<string>(traits??new List<string>()));
        ItemBindings=new ReadOnlyCollection<string>(new List<string>(items??new List<string>()));
        Effects=new ReadOnlyCollection<RebirthTraitSupportEffectDefinition>(new List<RebirthTraitSupportEffectDefinition>(effects??new List<RebirthTraitSupportEffectDefinition>()));
    }
}

public sealed class RebirthSurvivorDefinitionBundle
{
    public string ConfigRoot { get; private set; }
    public RebirthProgressionDefinition Progression { get; private set; }
    public ReadOnlyCollection<RebirthBackgroundDefinition> Backgrounds { get; private set; }
    public ReadOnlyCollection<RebirthTraitDefinition> Traits { get; private set; }
    public ReadOnlyCollection<RebirthDietDefinition> Diets { get; private set; }
    public ReadOnlyCollection<RebirthConditionCapGroupDefinition> CapGroups { get; private set; }
    public ReadOnlyCollection<RebirthConditionModifierProfileDefinition> ModifierProfiles { get; private set; }
    public ReadOnlyCollection<RebirthTraitSupportProfileDefinition> SupportProfiles { get; private set; }
    public RebirthSurvivorDefinitionBundle(string root,RebirthProgressionDefinition progression,IList<RebirthBackgroundDefinition> backgrounds,IList<RebirthTraitDefinition> traits,IList<RebirthDietDefinition> diets,IList<RebirthConditionCapGroupDefinition> caps,IList<RebirthConditionModifierProfileDefinition> modifiers,IList<RebirthTraitSupportProfileDefinition> support)
    { ConfigRoot=root??string.Empty;Progression=progression;Backgrounds=Freeze(backgrounds);Traits=Freeze(traits);Diets=Freeze(diets);CapGroups=Freeze(caps);ModifierProfiles=Freeze(modifiers);SupportProfiles=Freeze(support); }
    private static ReadOnlyCollection<T> Freeze<T>(IList<T> source) { return new ReadOnlyCollection<T>(new List<T>(source??new List<T>())); }
}
