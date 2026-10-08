using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

#nullable disable

/// <summary>
/// Read-only aggregate used by the redesigned Character pages. It owns no persistence and
/// performs no gameplay mutation: native player state, Survivor owner state and Metabolism
/// remain authoritative in their existing systems.
/// </summary>
public sealed class RebirthCharacterUiSnapshot
{
    public long SurvivorRevision;
    public int MetabolismRevision;
    public int AppearanceRevision;
    public string PlayerName = string.Empty;
    public string SourceProfileName = string.Empty;
    public int PlayerLevel = 1;
    public string BackgroundId = string.Empty;
    public string BackgroundName = string.Empty;
    public string BackgroundIdentity = string.Empty;
    public string BackgroundStartingExperience = string.Empty;
    public string BackgroundDescription = string.Empty;
    public string BackgroundArtKey = string.Empty;
    public readonly List<RebirthCharacterUiTrait> Traits = new List<RebirthCharacterUiTrait>();
    public readonly List<RebirthCharacterUiAttribute> Attributes = new List<RebirthCharacterUiAttribute>();
    public readonly List<RebirthCharacterUiSkill> Skills = new List<RebirthCharacterUiSkill>();
    public readonly List<RebirthCharacterUiKnowledge> Knowledge = new List<RebirthCharacterUiKnowledge>();
    public readonly List<RebirthCharacterUiCondition> Conditions = new List<RebirthCharacterUiCondition>();
    public readonly List<RebirthCharacterUiGear> Gear = new List<RebirthCharacterUiGear>();
    public float Health;
    public float HealthMax;
    public float Stamina;
    public float StaminaMax;
    public float Energy;
    public float EnergyMax;
    public float Nutrition;
    public float NutritionMax;
    public float Hydration;
    public float HydrationMax;
}

public sealed class RebirthCharacterUiTrait
{
    public string Description = string.Empty;
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Icon = string.Empty;
    public RebirthTraitPolarity Polarity;
}

public sealed class RebirthCharacterUiAttribute
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Atlas = "UIAtlas";
    public string Icon = "ui_game_symbol_trophy";
    public float Current;
    public float Potential;
}


public sealed class RebirthCharacterUiSkill
{
    public string StartingPoint = string.Empty;
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Atlas = "RebirthSurvivorIcons";
    public string Icon = "rb_skill_unarmed";
    public float Practical;
    public float PracticalProgress;
    public float PracticalMin;
    public float PracticalMax;
    public float SkillKnowledge;
    public float SkillKnowledgeMin;
    public float SkillKnowledgeMax;
    public string PracticalStatus = string.Empty;
    public string TheoryStatus = string.Empty;
    public string TheorySource = string.Empty;
    public string TheoryReveal = string.Empty;
    public string PrimaryAttributeId = string.Empty;
    public string PrimaryAttributeName = string.Empty;
    public string TrainingSource = string.Empty;
}

public sealed class RebirthCharacterUiKnowledge
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public bool Known;
    public readonly List<string> AssociatedSkillIds = new List<string>();
}

public sealed class RebirthCharacterUiGear
{
    public string SlotId = string.Empty;
    public string ItemId = string.Empty;
}

public sealed class RebirthCharacterUiCondition
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Detail = string.Empty;
    public string Description = string.Empty;
    public string Category = string.Empty;
    public string Status = string.Empty;
    public string Effects = string.Empty;
    public string Guidance = string.Empty;
    public string SourceSystem = string.Empty;
    public string Duration = string.Empty;
    public string Atlas = "RebirthSurvivorIcons";
    public string Icon = string.Empty;
    public Color IconColor = Color.white;
    public bool Positive;
    public bool Negative;
}

public static class RebirthCharacterUiSnapshotBuilder
{
    public static RebirthCharacterUiSnapshot Build(EntityPlayer player, RebirthSurvivorOwnerStateSnapshot survivor,
        bool hasMetabolism, RebirthMetabolismSnapshot metabolism, bool includeProgression = true)
    {
        if (survivor == null || !survivor.RebirthModeEnabled || !survivor.HasCharacter)
            return null;

        RebirthCharacterUiSnapshot result = new RebirthCharacterUiSnapshot();
        result.SurvivorRevision = survivor.CharacterRevision;
        result.MetabolismRevision = hasMetabolism ? metabolism.Revision : int.MinValue;
        result.PlayerName = player != null ? (player.EntityName ?? string.Empty) : string.Empty;
        result.SourceProfileName = survivor.SourceProfileName ?? string.Empty;
        result.PlayerLevel = player != null && player.Progression != null ? Math.Max(1, player.Progression.Level) : 1;
        result.BackgroundId = survivor.BackgroundId ?? string.Empty;

        RebirthBackgroundDefinition background;
        if (RebirthSurvivorDefinitionRegistry.TryGetBackground(result.BackgroundId, out background) && background != null)
        {
            result.BackgroundName = RebirthUiProjectionTextCache.L(background.NameKey, background.Id);
            result.BackgroundIdentity = background.Identity ?? string.Empty;
            result.BackgroundStartingExperience = background.StartingExperience ?? string.Empty;
            result.BackgroundDescription = RebirthUiProjectionTextCache.L(background.DescriptionKey, background.Identity);
            result.BackgroundArtKey = background.BackgroundArtKey ?? string.Empty;
        }
        else
        {
            result.BackgroundName = result.BackgroundId;
        }

        BuildTraits(result, survivor);
        BuildAttributes(result, survivor);
        if (includeProgression) BuildProgression(result, survivor);
        BuildVitals(result, player, hasMetabolism, metabolism);
        BuildGear(result, survivor);
        BuildConditions(result, player, survivor, hasMetabolism, metabolism);
        return result;
    }

    private static void BuildTraits(RebirthCharacterUiSnapshot result, RebirthSurvivorOwnerStateSnapshot survivor)
    {
        List<RebirthCharacterUiTrait> positive = new List<RebirthCharacterUiTrait>();
        List<RebirthCharacterUiTrait> mixed = new List<RebirthCharacterUiTrait>();
        List<RebirthCharacterUiTrait> negative = new List<RebirthCharacterUiTrait>();
        for (int i = 0; i < survivor.TraitIds.Count; i++)
        {
            string id = survivor.TraitIds[i] ?? string.Empty;
            RebirthTraitDefinition def;
            RebirthCharacterUiTrait row = new RebirthCharacterUiTrait { Id = id, Name = id, Icon = "rb_trait_generalist" };
            if (RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out def) && def != null)
            {
                row.Name = RebirthUiProjectionTextCache.L(def.NameKey, def.Id);
                row.Icon = string.IsNullOrEmpty(def.IconKey) ? "rb_trait_generalist" : def.IconKey;
                row.Polarity = def.Polarity;
                row.Description = RebirthUiProjectionTextCache.L(def.DescriptionKey, def.EffectSummary);
                if (!string.IsNullOrEmpty(def.EffectSummary) && row.Description != def.EffectSummary) row.Description += "\n\n" + def.EffectSummary;
            }
            if (row.Polarity == RebirthTraitPolarity.Positive) positive.Add(row);
            else if (row.Polarity == RebirthTraitPolarity.Negative) negative.Add(row);
            else mixed.Add(row);
        }
        result.Traits.AddRange(positive);
        result.Traits.AddRange(mixed);
        result.Traits.AddRange(negative);
    }

    private static void BuildAttributes(RebirthCharacterUiSnapshot result, RebirthSurvivorOwnerStateSnapshot survivor)
    {
        // Character presentation is deliberately stable even if a network snapshot arrives in a different order.
        // This is also the release invariant used by host/P2P/dedicated screenshot comparisons.
        string[] order = { "strength", "dexterity", "constitution", "intelligence", "charisma" };
        Dictionary<string, RebirthSurvivorOwnerAttributeSnapshot> byId = new Dictionary<string, RebirthSurvivorOwnerAttributeSnapshot>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < survivor.Attributes.Count; i++)
        {
            RebirthSurvivorOwnerAttributeSnapshot source = survivor.Attributes[i];
            if (source != null && !string.IsNullOrEmpty(source.Id)) byId[source.Id] = source;
        }
        for (int i = 0; i < order.Length; i++)
        {
            RebirthSurvivorOwnerAttributeSnapshot source;
            if (!byId.TryGetValue(order[i], out source) || source == null) continue;
            RebirthCharacterUiAttribute row = new RebirthCharacterUiAttribute
            {
                Id = source.Id ?? string.Empty,
                Name = RebirthUiProjectionTextCache.DefinitionName(source.Id ?? string.Empty),
                Current = source.Current,
                Potential = source.Potential
            };
            switch ((source.Id ?? string.Empty).ToLowerInvariant())
            {
                case "strength": row.Icon = "ui_game_symbol_muscle"; break;
                case "dexterity": row.Icon = "ui_game_symbol_agility"; break;
                case "constitution": row.Icon = "ui_game_symbol_fortitude_mastery"; break;
                case "intelligence": row.Icon = "ui_game_symbol_intellect"; break;
                case "charisma": row.Icon = "ui_game_symbol_players"; break;
            }
            result.Attributes.Add(row);
        }
    }

    private static void BuildProgression(RebirthCharacterUiSnapshot result, RebirthSurvivorOwnerStateSnapshot survivor)
    {
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null) return;

        RebirthSurvivorCreationResult start = null;
        if (survivor.DefinitionsCompatible && !string.IsNullOrEmpty(survivor.OriginDefinitionHash)
            && string.Equals(survivor.OriginDefinitionHash, RebirthSurvivorDefinitionRegistry.SemanticHash, StringComparison.OrdinalIgnoreCase))
            start = RebirthSurvivorCreationValidator.Validate(new RebirthSurvivorCreationSelection(
                survivor.BackgroundId, survivor.DietId, survivor.TraitIds, survivor.OriginDefinitionHash), false);

        Dictionary<string, RebirthSurvivorOwnerSkillSnapshot> practical = new Dictionary<string, RebirthSurvivorOwnerSkillSnapshot>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < survivor.Skills.Count; i++)
        {
            RebirthSurvivorOwnerSkillSnapshot row = survivor.Skills[i];
            if (row != null && !string.IsNullOrEmpty(row.Id)) practical[row.Id] = row;
        }
        Dictionary<string, float> theory = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < survivor.SkillKnowledge.Count; i++)
        {
            RebirthSurvivorOwnerSkillKnowledgeSnapshot row = survivor.SkillKnowledge[i];
            if (row != null && !string.IsNullOrEmpty(row.Id)) theory[row.Id] = row.Value;
        }

        for (int i = 0; i < bundle.Progression.Skills.Count; i++)
        {
            RebirthSkillDefinition def = bundle.Progression.Skills[i];
            if (def == null) continue;
            RebirthSurvivorOwnerSkillSnapshot state;
            practical.TryGetValue(def.Id, out state);
            float knowledgeValue;
            if (!theory.TryGetValue(def.Id, out knowledgeValue)) knowledgeValue = bundle.Progression.SkillKnowledgeMin;
            string attributeId = RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(def.Id);
            string source = SkillTrainingSource(def);
            result.Skills.Add(new RebirthCharacterUiSkill
            {
                Id = def.Id,
                Name = RebirthUiProjectionTextCache.L(def.NameKey, def.Id),
                Icon = RebirthSkillAptitudeTraitFactory.SkillIconKey(def.Id),
                Practical = state != null ? state.Value : def.Min,
                StartingPoint = RebirthSkillStartingPoint.Describe(survivor, def, state != null ? state.Value : def.Min, start),
                PracticalProgress = state != null ? Mathf.Clamp01(state.Progress) : 0f,
                PracticalMin = def.Min,
                PracticalMax = def.Max,
                SkillKnowledge = knowledgeValue,
                SkillKnowledgeMin = bundle.Progression.SkillKnowledgeMin,
                SkillKnowledgeMax = bundle.Progression.SkillKnowledgeMax,
                PracticalStatus = PracticalStatus(state != null ? state.Value : def.Min),
                TheoryStatus = RebirthTheoryProgressionService.GetTheoryStatus(knowledgeValue),
                TheorySource = TheorySource(def.Id),
                TheoryReveal = RebirthTheoryRevealService.BuildReveal(def.Id, knowledgeValue),
                PrimaryAttributeId = attributeId,
                PrimaryAttributeName = RebirthUiProjectionTextCache.DefinitionName(attributeId),
                TrainingSource = source ?? string.Empty
            });
        }

        HashSet<string> known = new HashSet<string>(survivor.KnowledgeIds, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < bundle.Progression.Knowledge.Count; i++)
        {
            RebirthKnowledgeDefinition def = bundle.Progression.Knowledge[i];
            if (def == null || string.IsNullOrEmpty(def.Id) || RebirthLiteratureService.IsInternalReadMarker(def.Id)) continue;
            RebirthCharacterUiKnowledge row = new RebirthCharacterUiKnowledge
            {
                Id = def.Id,
                Name = RebirthUiProjectionTextCache.L(def.NameKey, def.Id),
                Known = known.Contains(def.Id)
            };
            for (int k = 0; k < def.AssociatedSkillIds.Count; k++)
                if (!string.IsNullOrEmpty(def.AssociatedSkillIds[k])) row.AssociatedSkillIds.Add(def.AssociatedSkillIds[k]);
            result.Knowledge.Add(row);
        }
    }

    public static string PracticalStatus(float value)
    {
        if (value <= -31f) return "Severely Unskilled";
        if (value <= -16f) return "Very Unskilled";
        if (value < 0f) return "Inexperienced";
        if (Math.Abs(value) < 0.0001f) return "No Practical Experience";
        if (value < 15f) return "Novice";
        if (value < 30f) return "Beginner";
        if (value < 45f) return "Competent";
        if (value < 60f) return "Experienced";
        if (value < 75f) return "Skilled";
        if (value < 90f) return "Highly Skilled";
        if (value < 100f) return "Expert";
        return "Master";
    }

    public static string TheorySource(string skillId)
    {
        if (string.Equals(skillId, "skill.cooking", StringComparison.OrdinalIgnoreCase))
            return "Study, instruction and finite Insights. Cooking cookbooks and magazines improve Preparation, not Cooking Theory.";
        return "Study, instruction and finite Insights; authored Fundamentals, Field Notes or training recordings contribute where available.";
    }

    public static string SkillTrainingSource(RebirthSkillDefinition def)
    {
        if(def==null)return string.Empty;
        string source=string.IsNullOrEmpty(def.SourceKey)?def.LearnByDoingSource:RebirthUiProjectionTextCache.L(def.SourceKey,def.LearnByDoingSource);
        return (source??string.Empty).Replace("successful entity damage using matching item tags","Land damaging hits with this weapon type.");
    }

    /// <summary>Shared concise Skill-information block used by Progression Explorer.</summary>
    public static string BuildExplorerSkillDetails(EntityPlayer player,string skillId,bool liveCharacter)
    {
        RebirthSkillDefinition def;
        if(string.IsNullOrEmpty(skillId)||!RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId,out def)||def==null)return string.Empty;
        string attributeId=RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(def.Id);
        string attributeName=RebirthUiProjectionTextCache.DefinitionName(attributeId);
        float theory=0f,practical=def.Min,practicalProgress=0f;
        if(liveCharacter)
        {
            RebirthSurvivorOwnerStateSnapshot owner=RebirthSurvivorClientState.GetOwnerStateSnapshot();
            if(owner!=null)
            {
                for(int i=0;i<owner.Skills.Count;i++)
                    if(owner.Skills[i]!=null&&string.Equals(owner.Skills[i].Id,def.Id,StringComparison.OrdinalIgnoreCase)){practical=owner.Skills[i].Value;practicalProgress=Mathf.Clamp01(owner.Skills[i].Progress);break;}
                for(int i=0;i<owner.SkillKnowledge.Count;i++)
                    if(owner.SkillKnowledge[i]!=null&&string.Equals(owner.SkillKnowledge[i].Id,def.Id,StringComparison.OrdinalIgnoreCase)){theory=owner.SkillKnowledge[i].Value;break;}
            }
        }
        string contribution=RebirthSkillTrainingUiProjectionService.GetOrRequest(liveCharacter?player:null,def.Id);
        string reveal=RebirthTheoryRevealService.BuildReveal(def.Id,theory);
        string text="Primary Attribute: "+attributeName;
        if(liveCharacter)text+="\nStatus: "+PracticalStatus(practical)+" ("+(practical+practicalProgress).ToString("0.###",CultureInfo.InvariantCulture)+") • "+RebirthTheoryProgressionService.GetTheoryStatus(theory)+" Theory";
        text+="\nPractical training: "+SkillTrainingSource(def);
        if(!string.IsNullOrEmpty(contribution))text+="\nTraining contribution: "+contribution;
        text+="\nTheory sources: "+TheorySource(def.Id);
        if(!string.IsNullOrEmpty(reveal))text+="\n"+reveal;
        return text;
    }

    public static void RefreshVitals(RebirthCharacterUiSnapshot result, EntityPlayer player, bool hasMetabolism, RebirthMetabolismSnapshot metabolism)
    {
        if(result==null)return;
        BuildVitals(result,player,hasMetabolism,metabolism);
    }

    private static void BuildVitals(RebirthCharacterUiSnapshot result, EntityPlayer player, bool hasMetabolism, RebirthMetabolismSnapshot metabolism)
    {
        if (player != null && player.Stats != null)
        {
            if (player.Stats.Health != null)
            {
                result.Health = player.Stats.Health.Value;
                result.HealthMax = Math.Max(1f, player.Stats.Health.Max);
            }
            if (player.Stats.Stamina != null)
            {
                result.Stamina = player.Stats.Stamina.Value;
                result.StaminaMax = Math.Max(1f, player.Stats.Stamina.Max);
            }
        }
        if (hasMetabolism)
        {
            result.Energy = metabolism.Energy;
            result.EnergyMax = Math.Max(1f, metabolism.EnergyMax);
            result.Nutrition = metabolism.Food;
            result.NutritionMax = Math.Max(1f, metabolism.FoodMax);
            result.Hydration = metabolism.Hydration;
            result.HydrationMax = Math.Max(1f, metabolism.HydrationMax);
        }
    }


    private static void BuildGear(RebirthCharacterUiSnapshot result, RebirthSurvivorOwnerStateSnapshot survivor)
    {
        for (int i = 0; i < survivor.GearSlots.Count; i++)
        {
            RebirthSurvivorOwnerGearSnapshot source = survivor.GearSlots[i];
            if (source == null) continue;
            result.Gear.Add(new RebirthCharacterUiGear
            {
                SlotId = source.SlotId ?? string.Empty,
                ItemId = source.ItemId ?? string.Empty
            });
        }
    }

    private static void BuildConditions(RebirthCharacterUiSnapshot result, EntityPlayer player, RebirthSurvivorOwnerStateSnapshot survivor,
        bool hasMetabolism, RebirthMetabolismSnapshot metabolism)
    {
        RebirthStressPresentation.AddConditions(result,player);
        string moodName;
        string moodIcon;
        if (survivor.MoodCurrent >= 80f) { moodName = L("xuiRebirthCharacterOverviewMoodExcellent", "Excellent Mood"); moodIcon = "rb_condition_mood_excellent"; }
        else if (survivor.MoodCurrent >= 55f) { moodName = L("xuiRebirthCharacterOverviewMoodGood", "Good Mood"); moodIcon = "rb_condition_mood_good"; }
        else if (survivor.MoodCurrent >= 30f) { moodName = L("xuiRebirthCharacterOverviewMoodLow", "Low Mood"); moodIcon = "rb_condition_mood_low"; }
        else { moodName = L("xuiRebirthCharacterOverviewMoodMiserable", "Miserable Mood"); moodIcon = "rb_condition_mood_miserable"; }
        float moodUse = RebirthConditionRuntimeConfig.GetMoodEnergyUseMultiplier(survivor.MoodCurrent);
        float moodRecovery = RebirthConditionRuntimeConfig.GetMoodEnergyRecoveryMultiplier(survivor.MoodCurrent);
        AddCondition(result, new RebirthCharacterUiCondition
        {
            Id = "rebirth:mood",
            Name = moodName,
            Detail = survivor.MoodCurrent.ToString("0", CultureInfo.InvariantCulture) + " / 100  •  target " + survivor.MoodTarget.ToString("0", CultureInfo.InvariantCulture),
            Description = L("xuiRebirthConditionMoodDescription", "Mood is a Survivor active-play state that moves toward a resolver-computed target."),
            Category = L("xuiRebirthConditionCategoryMood", "Mood"),
            Status = L("xuiRebirthConditionCurrent", "Current") + " " + survivor.MoodCurrent.ToString("0.0", CultureInfo.InvariantCulture) + " / 100\n" +
                L("xuiRebirthConditionTarget", "Target") + " " + survivor.MoodTarget.ToString("0.0", CultureInfo.InvariantCulture),
            Effects = L("xuiRebirthConditionEnergyUse", "Energy use") + " ×" + moodUse.ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                L("xuiRebirthConditionEnergyRecovery", "Energy recovery") + " ×" + moodRecovery.ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                L("xuiRebirthConditionPositiveContributor", "Positive contributor") + ": " + FormatCause(survivor.MoodPositiveCauseId, survivor.MoodPositiveCauseDelta) + "\n" +
                L("xuiRebirthConditionNegativeContributor", "Negative contributor") + ": " + FormatCause(survivor.MoodNegativeCauseId, survivor.MoodNegativeCauseDelta),
            Guidance = L("xuiRebirthConditionMoodGuidance", "Mood improves or worsens as its current resolver contributors change during active play."),
            SourceSystem = L("xuiRebirthConditionSourceSurvivor", "Survivor Condition"),
            Icon = moodIcon,
            Positive = survivor.MoodCurrent >= 55f,
            Negative = survivor.MoodCurrent < 55f
        });

        float dietDelta = ((Mathf.Clamp(survivor.DietSatisfaction, 0f, 100f) - 50f) / 50f) * RebirthConditionRuntimeConfig.DietSatisfactionMaxTargetDelta;
        bool dietPositive = dietDelta > 0.05f;
        bool dietNegative = dietDelta < -0.05f;
        string lastMeal = survivor.RecentMealCount <= 0
            ? L("xuiRebirthConditionNoMeals", "No meaningful meals recorded yet.")
            : (survivor.LastMealCompatible ? L("xuiRebirthConditionMealCompatible", "Last meal compatible") : L("xuiRebirthConditionMealIncompatible", "Last meal incompatible"));
        AddCondition(result, new RebirthCharacterUiCondition
        {
            Id = "rebirth:diet_satisfaction",
            Name = L("xuiRebirthCharacterOverviewDietSatisfaction", "Diet Satisfaction"),
            Detail = survivor.DietSatisfaction.ToString("0", CultureInfo.InvariantCulture) + " / 100  •  " + survivor.RecentVarietyCount.ToString(CultureInfo.InvariantCulture) + " " + L("xuiRebirthCharacterOverviewVarieties", "varieties"),
            Description = L("xuiRebirthConditionDietDescription", "Diet Satisfaction reflects recent compatible meals and food variety for the selected Survivor Diet."),
            Category = L("xuiRebirthConditionCategoryDiet", "Diet"),
            Status = survivor.DietSatisfaction.ToString("0.0", CultureInfo.InvariantCulture) + " / 100\n" +
                survivor.RecentVarietyCount.ToString(CultureInfo.InvariantCulture) + " " + L("xuiRebirthConditionVarieties", "compatible varieties") + " / " +
                survivor.RecentMealCount.ToString(CultureInfo.InvariantCulture) + " " + L("xuiRebirthConditionRecentMeals", "recent meals") + "\n" + lastMeal,
            Effects = L("xuiRebirthConditionMoodTargetImpact", "Mood target impact") + " " + Signed(dietDelta),
            Guidance = L("xuiRebirthConditionDietGuidance", "Compatible meals and greater recent variety improve Diet Satisfaction; repetition reduces its benefit."),
            SourceSystem = L("xuiRebirthConditionSourceDiet", "Survivor Diet"),
            Atlas = "UIAtlas",
            Icon = "ui_game_symbol_fork",
            Positive = dietPositive,
            Negative = dietNegative
        });

        // Resource meters are presented in Vitals and Metabolism, not active conditions.
        if (hasMetabolism)
        {
            if (metabolism.DigestiveHealth + 0.05f < RebirthMetabolismConfig.BaseDigestiveHealth)
            {
                bool severeDigestive = metabolism.DigestiveHealth < 35f;
                AddCondition(result, new RebirthCharacterUiCondition
                {
                    Id = "rebirth:digestive_health",
                    Name = L("xuiRebirthConditionDigestiveHealth", "Digestive Health"),
                    Detail = metabolism.DigestiveHealth.ToString("0", CultureInfo.InvariantCulture) + " / " + RebirthMetabolismConfig.BaseDigestiveHealth.ToString("0", CultureInfo.InvariantCulture),
                    Description = L("xuiRebirthConditionDigestiveDescription", "Digestive Health affects current nutrient and fluid utilization in the Metabolism digestion model."),
                    Category = L("xuiRebirthConditionCategoryMetabolism", "Metabolism"),
                    Status = L("xuiRebirthConditionCurrent", "Current") + " " + metabolism.DigestiveHealth.ToString("0.0", CultureInfo.InvariantCulture) + " / 100",
                    Effects = L("xuiRebirthConditionNutrientUtilization", "Nutrient utilization") + " ×" + metabolism.NutrientUtilizationMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                        L("xuiRebirthConditionFluidUtilization", "Fluid utilization") + " ×" + metabolism.FluidUtilizationMultiplier.ToString("0.00", CultureInfo.InvariantCulture),
                    Guidance = L("xuiRebirthConditionDigestiveGuidance", "Digestive Health recovers toward its configured baseline when current digestive damage is no longer being applied."),
                    SourceSystem = L("xuiRebirthConditionSourceMetabolism", "Metabolism"),
                    Icon = severeDigestive ? "rb_condition_nauseated" : "rb_condition_indigestion",
                    Negative = true
                });
            }
        }

        BuildLiveConditionSignals(result, player);

        for (int i = 0; i < survivor.SupportEntries.Count; i++)
        {
            RebirthSurvivorOwnerSupportSnapshot support = survivor.SupportEntries[i];
            if (support == null) continue;
            float active = Math.Max(support.ManagedRemainingActiveSeconds, support.PositiveRemainingActiveSeconds);
            float remaining = active > 0.01f ? active : support.GraceRemainingActiveSeconds;
            if (remaining <= 0.01f) continue;
            RebirthTraitSupportProfileDefinition def;
            bool hasDef = RebirthSurvivorDefinitionRegistry.TryGetSupport(support.ProfileId, out def) && def != null;
            string name = hasDef ? RebirthUiProjectionTextCache.L(def.NameKey, def.Id) : support.ProfileId;
            AddCondition(result, new RebirthCharacterUiCondition
            {
                Id = "support:" + (support.ProfileId ?? string.Empty),
                Name = name,
                Detail = FormatDuration(remaining) + " " + (active > 0.01f ? L("xuiRebirthCharacterOverviewActive", "active") : L("xuiRebirthCharacterOverviewGrace", "grace")),
                Description = hasDef ? (def.EffectSummary ?? string.Empty) : string.Empty,
                Category = L("xuiRebirthConditionCategorySupport", "Trait Support"),
                Status = (active > 0.01f ? L("xuiRebirthCharacterOverviewActive", "Active") : L("xuiRebirthCharacterOverviewGrace", "Grace")) + "\n" + FormatDuration(remaining) + " " + L("xuiRebirthConditionRemaining", "remaining"),
                Effects = hasDef ? (def.EffectSummary ?? string.Empty) : string.Empty,
                Guidance = string.Empty,
                SourceSystem = L("xuiRebirthConditionSourceSupport", "Trait Support"),
                Duration = FormatDuration(remaining),
                Icon = "rb_condition_trait_support_managed",
                Positive = true,
                Negative = false
            });
        }

        BuildNativeBuffConditions(result, player);
        StableConditionOrder(result.Conditions);
    }

    private static void BuildLiveConditionSignals(RebirthCharacterUiSnapshot result, EntityPlayer player)
    {
        if (player == null || player.Stats == null) return;

        float nativeHealthBlockage = Mathf.Max(0f, EffectManager.GetValue(PassiveEffects.HealthMaxBlockage, _entity: player));
        if (nativeHealthBlockage > 0.01f)
        {
            AddCondition(result, new RebirthCharacterUiCondition
            {
                Id = "native:health_blockage",
                Name = L("xuiRebirthConditionInjuryImpact", "Injury / Health Blockage"),
                Detail = nativeHealthBlockage.ToString("0.0", CultureInfo.InvariantCulture) + " " + L("xuiRebirthConditionHealthBlocked", "Health blocked"),
                Description = L("xuiRebirthConditionInjuryDescription", "The native HealthMaxBlockage effect reports current injury-related maximum-Health blockage."),
                Category = L("xuiRebirthConditionCategoryInjury", "Injury"),
                Status = L("xuiRebirthConditionHealthBlocked", "Health blocked") + " " + nativeHealthBlockage.ToString("0.0", CultureInfo.InvariantCulture),
                Effects = L("xuiRebirthConditionInjuryMoodSource", "This is also an authoritative injury-pain contributor to Survivor Mood."),
                SourceSystem = L("xuiRebirthConditionSourceNative", "Base Game"),
                Atlas = "UIAtlas",
                Icon = "ui_game_symbol_medical",
                Negative = true
            });
        }

        if (RebirthConditionTraitModifierService.IsSeriousIllness(player))
        {
            AddCondition(result, new RebirthCharacterUiCondition
            {
                Id = "native:serious_illness",
                Name = L("xuiRebirthConditionSeriousIllness", "Serious Illness"),
                Detail = L("xuiRebirthConditionActive", "Active"),
                Description = L("xuiRebirthConditionSeriousIllnessDescription", "The current native buff state satisfies the Survivor resolver's serious-illness test."),
                Category = L("xuiRebirthConditionCategoryIllness", "Illness"),
                Status = L("xuiRebirthConditionActive", "Active"),
                Effects = L("xuiRebirthConditionSeriousIllnessEffect", "Serious illness contributes negatively to the current Survivor Mood target."),
                SourceSystem = L("xuiRebirthConditionSourceNative", "Base Game"),
                Icon = "rb_condition_digestive_illness",
                Negative = true
            });
        }

        float core = player.PlayerStats.CoreTemp;
        if (core >= 75f)
        {
            float severity = Mathf.Clamp01((core - 75f) / 25f);
            AddCondition(result, new RebirthCharacterUiCondition
            {
                Id = "native:heat_exposure",
                Name = L("xuiRebirthConditionHeatExposure", "Heat Exposure"),
                Detail = L("xuiRebirthConditionCoreTemperature", "Core temperature") + " " + core.ToString("0.0", CultureInfo.InvariantCulture),
                Description = L("xuiRebirthConditionHeatDescription", "Core temperature is above the Survivor resolver's heat-exposure threshold."),
                Category = L("xuiRebirthConditionCategoryEnvironment", "Environment"),
                Status = L("xuiRebirthConditionSeverity", "Severity") + " " + (severity * 100f).ToString("0", CultureInfo.InvariantCulture) + "%",
                Effects = L("xuiRebirthConditionHeatMoodEffect", "Heat exposure is an authoritative negative Mood-target contributor while active."),
                SourceSystem = L("xuiRebirthConditionSourceEnvironment", "Environment"),
                Icon = "rb_condition_heat_stress",
                Negative = true
            });
        }
        else if (core <= 62f)
        {
            float severity = Mathf.Clamp01((62f - core) / 27f);
            AddCondition(result, new RebirthCharacterUiCondition
            {
                Id = "native:cold_exposure",
                Name = L("xuiRebirthConditionColdExposure", "Cold Exposure"),
                Detail = L("xuiRebirthConditionCoreTemperature", "Core temperature") + " " + core.ToString("0.0", CultureInfo.InvariantCulture),
                Description = L("xuiRebirthConditionColdDescription", "Core temperature is below the Survivor resolver's cold-exposure threshold."),
                Category = L("xuiRebirthConditionCategoryEnvironment", "Environment"),
                Status = L("xuiRebirthConditionSeverity", "Severity") + " " + (severity * 100f).ToString("0", CultureInfo.InvariantCulture) + "%",
                Effects = L("xuiRebirthConditionColdMoodEffect", "Cold exposure is an authoritative negative Mood-target contributor while active."),
                SourceSystem = L("xuiRebirthConditionSourceEnvironment", "Environment"),
                Icon = "rb_condition_cold_stress",
                Negative = true
            });
        }
    }

    private static void BuildNativeBuffConditions(RebirthCharacterUiSnapshot result, EntityPlayer player)
    {
        if (player == null || player.Buffs == null || player.Buffs.ActiveBuffs == null) return;
        List<BuffValue> active = player.Buffs.ActiveBuffs;
        for (int i = 0; i < active.Count; i++)
        {
            BuffValue buff = active[i];
            if (buff == null || buff.Remove || buff.Finished || buff.Invalid || buff.Paused) continue;
            BuffClass bc = buff.BuffClass;
            if (bc == null || bc.Hidden) continue;
            string id = bc.Name ?? string.Empty;
            if (string.IsNullOrEmpty(id)) continue;
            string name = NativeBuffName(bc);
            string description = NativeBuffDescription(bc);
            string time = NativeBuffDisplayInfo(player, buff);
            string duration = XUiM_PlayerBuffs.GetBuffTimeLeftString(buff);
            if (string.IsNullOrEmpty(duration) && bc.DisplayValueFormat == BuffClass.CVarDisplayFormat.Time && !string.IsNullOrEmpty(bc.DisplayValueCVar))
                duration = XUiM_PlayerBuffs.GetCVarValueAsTimeString(player.Buffs.GetCustomVar(bc.DisplayValueCVar));
            AddCondition(result, new RebirthCharacterUiCondition
            {
                Id = "buff:" + id,
                Name = name,
                Detail = string.IsNullOrEmpty(time) ? L("xuiRebirthConditionActive", "Active") : time,
                Description = description,
                Category = L("xuiRebirthConditionActiveEffect", "Active Effect"),
                Status = string.IsNullOrEmpty(time) ? L("xuiRebirthConditionActive", "Active") : time,
                Effects = description,
                Guidance = string.Empty,
                SourceSystem = L("xuiRebirthConditionSourceNative", "Base Game"),
                Duration = duration ?? string.Empty,
                Atlas = "UIAtlas",
                Icon = bc.Icon ?? string.Empty,
                IconColor = bc.IconColor.a <= 0f ? Color.white : bc.IconColor,
                Positive = false,
                // Native buffs have no polarity field; classify this known injury by ID,
                // never by icon tint (which is presentation rather than gameplay meaning).
                Negative = string.Equals(id, "buffLegBroken", StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    private static string NativeBuffName(BuffClass bc)
    {
        if (bc == null) return string.Empty;
        string key = bc.LocalizedName;
        string localized = LocalizeKey(key);
        if (!string.IsNullOrEmpty(localized)) return localized;
        localized = LocalizeKey((bc.Name ?? string.Empty) + "Name");
        if (!string.IsNullOrEmpty(localized)) return localized;
        return !string.IsNullOrEmpty(bc.LocalizedName) ? bc.LocalizedName : (bc.Name ?? string.Empty);
    }

    private static string NativeBuffDescription(BuffClass bc)
    {
        if (bc == null) return string.Empty;
        if ((bc.Name ?? string.Empty).StartsWith("buffDysentery", StringComparison.OrdinalIgnoreCase))
            return L("xuiRebirthDysenteryDescription", "Dysentery causes repeated diarrhea and occasional vomiting. Diarrhea expels part of your intestinal contents, losing unabsorbed nutrition and water. Vomiting expels part of your stomach contents. Both also reduce hydration. Goldenrod tea may help you recover.");
        string key = ReflectedString(bc, "DescriptionKey", "descriptionKey", "description_key");
        string localized = LocalizeKey(key);
        if (!string.IsNullOrEmpty(localized)) return localized;
        localized = LocalizeKey((bc.Name ?? string.Empty) + "Desc");
        if (!string.IsNullOrEmpty(localized)) return localized;
        string raw = ReflectedString(bc, "Description", "description");
        return raw ?? string.Empty;
    }

    private static string ReflectedString(object target, params string[] names)
    {
        if (target == null || names == null) return string.Empty;
        Type type = target.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrEmpty(name)) continue;
            try
            {
                PropertyInfo p = type.GetProperty(name, flags);
                if (p != null && p.PropertyType == typeof(string)) return p.GetValue(target, null) as string ?? string.Empty;
                FieldInfo f = type.GetField(name, flags);
                if (f != null && f.FieldType == typeof(string)) return f.GetValue(target) as string ?? string.Empty;
            }
            catch { }
        }
        return string.Empty;
    }

    private static string LocalizeKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        string value = Localization.Get(key);
        return string.IsNullOrEmpty(value) || string.Equals(value, key, StringComparison.Ordinal) ? string.Empty : value;
    }

    private static string NativeBuffDisplayInfo(EntityPlayer player, BuffValue buff)
    {
        if (buff == null || buff.BuffClass == null) return string.Empty;
        string time = XUiM_PlayerBuffs.GetBuffTimeLeftString(buff);
        if (!string.IsNullOrEmpty(time)) return time;
        BuffClass bc = buff.BuffClass;
        if (player != null && player.Buffs != null && !string.IsNullOrEmpty(bc.DisplayValueCVar))
        {
            float displayValue = player.Buffs.GetCustomVar(bc.DisplayValueCVar);
            if (bc.DisplayValueFormat == BuffClass.CVarDisplayFormat.Time)
                return XUiM_PlayerBuffs.GetCVarValueAsTimeString(displayValue) ?? string.Empty;
            if (Math.Abs(displayValue) > 0.0001f)
            {
                switch (bc.DisplayValueFormat)
                {
                    case BuffClass.CVarDisplayFormat.Float: return displayValue.ToString("0.##", CultureInfo.InvariantCulture);
                    case BuffClass.CVarDisplayFormat.FlooredToInt: return Mathf.FloorToInt(displayValue).ToString(CultureInfo.InvariantCulture);
                    case BuffClass.CVarDisplayFormat.RoundedToInt: return Mathf.RoundToInt(displayValue).ToString(CultureInfo.InvariantCulture);
                    case BuffClass.CVarDisplayFormat.CeiledToInt: return Mathf.CeilToInt(displayValue).ToString(CultureInfo.InvariantCulture);
                    case BuffClass.CVarDisplayFormat.Percentage: return Mathf.RoundToInt(displayValue * 100f).ToString(CultureInfo.InvariantCulture) + "%";
                }
            }
        }
        return buff.StackEffectMultiplier > 1 ? "x" + buff.StackEffectMultiplier.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static void AddCondition(RebirthCharacterUiSnapshot result, RebirthCharacterUiCondition condition)
    {
        if (result == null || condition == null || string.IsNullOrEmpty(condition.Id)) return;
        for (int i = 0; i < result.Conditions.Count; i++)
            if (string.Equals(result.Conditions[i].Id, condition.Id, StringComparison.OrdinalIgnoreCase)) return;
        result.Conditions.Add(condition);
    }

    private static void StableConditionOrder(List<RebirthCharacterUiCondition> list)
    {
        if (list == null || list.Count < 2) return;
        list.Sort(delegate(RebirthCharacterUiCondition a, RebirthCharacterUiCondition b)
        {
            bool aBuff = a != null && a.Id.StartsWith("buff:", StringComparison.Ordinal);
            bool bBuff = b != null && b.Id.StartsWith("buff:", StringComparison.Ordinal);
            if (aBuff != bBuff) return aBuff ? -1 : 1;
            int ap = a != null && a.Positive ? 0 : (a != null && a.Negative ? 1 : 2);
            int bp = b != null && b.Positive ? 0 : (b != null && b.Negative ? 1 : 2);
            int c = ap.CompareTo(bp);
            if (c != 0) return c;
            c = string.Compare(a != null ? a.Category : string.Empty, b != null ? b.Category : string.Empty, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            return string.Compare(a != null ? a.Name : string.Empty, b != null ? b.Name : string.Empty, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static float Percent(float value, float max) { return max > 0.001f ? Mathf.Clamp01(value / max) : 0f; }

    private static string FormatCause(string id, float delta)
    {
        if (string.IsNullOrEmpty(id) || Math.Abs(delta) < 0.001f) return L("xuiRebirthConditionNone", "None");
        string label = string.Equals(id, "diet_satisfaction", StringComparison.OrdinalIgnoreCase) ? L("xuiRebirthConditionDietCause", "Diet Satisfaction") : id.Replace('_', ' ');
        return label + " " + Signed(delta);
    }

    private static string Signed(float value)
    {
        return (value >= 0f ? "+" : string.Empty) + value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string L(string key, string fallback) { return RebirthUiProjectionTextCache.L(key, fallback); }

    private static string FormatDuration(float seconds)
    {
        int total = Mathf.CeilToInt(Math.Max(0f, seconds));
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        return hours > 0 ? hours.ToString(CultureInfo.InvariantCulture) + "h " + minutes.ToString(CultureInfo.InvariantCulture) + "m" : Math.Max(1, minutes).ToString(CultureInfo.InvariantCulture) + "m";
    }
}
