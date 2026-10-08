using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#nullable disable

public static class RebirthSurvivorUiText
{
    public static string L(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;
        string value = Localization.Get(key);
        return string.IsNullOrEmpty(value) || string.Equals(value, key, StringComparison.Ordinal) ? (fallback ?? key) : value;
    }

    public static bool TryGetSignatureBonus(string backgroundId, out RebirthBackgroundBonusDefinition definition)
    {
        definition = null;
        if (string.IsNullOrEmpty(backgroundId) || !RebirthBackgroundBonusRegistry.IsReady) return false;
        return RebirthBackgroundBonusRegistry.TryGetByBackground(backgroundId, out definition) && definition != null;
    }

    public static string SignatureBonusName(string backgroundId)
    {
        RebirthBackgroundBonusDefinition bonus;
        return TryGetSignatureBonus(backgroundId, out bonus)
            ? L(bonus.NameKey, RebirthBackgroundBonusTextFallback.Get(bonus.NameKey,"Background bonus"))
            : L("xuiRebirthSignatureBonusNone", "None");
    }

    public static string SignatureBonusDescription(string backgroundId)
    {
        RebirthBackgroundBonusDefinition bonus;
        return TryGetSignatureBonus(backgroundId, out bonus)
            ? L(bonus.DescriptionKey, RebirthBackgroundBonusTextFallback.Get(bonus.DescriptionKey,"A benefit from your pre-apocalypse experience."))
            : L("xuiRebirthSignatureBonusNoneDesc", "This Experience has no Signature Bonus.");
    }

    public static string SignatureBonusIcon(string backgroundId)
    {
        RebirthBackgroundBonusDefinition bonus;
        return TryGetSignatureBonus(backgroundId, out bonus) ? bonus.IconKey : string.Empty;
    }

    public static string BuildSignatureBonusSummary(string backgroundId, bool includeHeading)
    {
        StringBuilder b = new StringBuilder(256);
        if (includeHeading)
            b.Append("[D6C978]").Append(L("xuiRebirthSignatureBonus", "SIGNATURE BONUS")).Append("[-]\n");
        b.Append("[FFFFFF]").Append(SignatureBonusName(backgroundId)).Append("[-]\n")
            .Append(SignatureBonusDescription(backgroundId));
        return b.ToString();
    }

    public static string BuildSkillRelationshipHint(string skillId)
    {
        string backgroundId = string.Empty;
        string explanationKey = string.Empty;
        string explanationFallback = string.Empty;
        if (string.Equals(skillId, "skill.animal_handling", StringComparison.OrdinalIgnoreCase))
        {
            backgroundId = "background.park_ranger_outdoor_guide";
            explanationKey = "xuiRebirthSkillRelationAnimalHandling";
            explanationFallback = "Calming Presence complements Animal Handling, but Animal Handling remains available to every Rebirth survivor.";
        }
        else if (string.Equals(skillId, "skill.drink_preparation", StringComparison.OrdinalIgnoreCase))
        {
            backgroundId = "background.bartender";
            explanationKey = "xuiRebirthSkillRelationDrinkPreparation";
            explanationFallback = "Master Mixologist multiplies the final prepared-drink effect duration after Drink Preparation determines it; the Skill is not background-gated.";
        }
        else if (string.Equals(skillId, "skill.trading", StringComparison.OrdinalIgnoreCase))
        {
            backgroundId = "background.salesperson";
            explanationKey = "xuiRebirthSkillRelationTrading";
            explanationFallback = "More Options is independent of Trading and can add another reward option; Trading remains a normal Skill for every Rebirth survivor.";
        }
        else if (string.Equals(skillId, "skill.teaching", StringComparison.OrdinalIgnoreCase))
        {
            backgroundId = "background.teacher";
            explanationKey = "xuiRebirthSkillRelationTeaching";
            explanationFallback = "Scholar and Mentor augments study and lessons, but Teaching remains available to every Rebirth survivor.";
        }
        if (backgroundId.Length == 0) return string.Empty;
        return L("xuiRebirthProgressionRelatedSignatureBonus", "Related Signature Bonus") + ": " + SignatureBonusName(backgroundId)
            + " — " + L(explanationKey, explanationFallback);
    }

    public static string FormatCreationError(RebirthSurvivorCreationError error)
    {
        if (error == null) return L("xuiRebirthSurvivorValidationUnavailable", "Validation unavailable.");
        string text = L(error.MessageKey, error.Code.ToString());
        if (!string.IsNullOrEmpty(error.SubjectId))
            text += " [" + ResolveDefinitionName(error.SubjectId) + "]";
        if (!string.IsNullOrEmpty(error.RelatedId))
            text += " (" + ResolveDefinitionName(error.RelatedId) + ")";
        return text;
    }

    public static string BuildValidationSummary(RebirthSurvivorCreationResult result)
    {
        if (result == null) return L("xuiRebirthSurvivorValidationUnavailable", "Validation unavailable.");
        if (result.IsValid)
            return string.Format(CultureInfo.InvariantCulture,
                L("xuiRebirthSurvivorValidationReadyFormat", "Ready — {0} creation points remaining."), result.RemainingCreationPoints);

        StringBuilder text = new StringBuilder();
        int max = Math.Min(3, result.Errors.Count);
        for (int i = 0; i < max; i++)
        {
            if (i > 0) text.Append("\n");
            text.Append("• ").Append(FormatCreationError(result.Errors[i]));
        }
        if (result.Errors.Count > max)
            text.Append("\n+").Append(result.Errors.Count - max).Append(" ").Append(L("xuiRebirthSurvivorMoreIssues", "more issues"));
        return text.ToString();
    }

    public static string BuildValidationSummaryForStep(RebirthSurvivorCreationResult result, RebirthSurvivorCreatorStep step)
    {
        if (result == null) return L("xuiRebirthSurvivorValidationUnavailable", "Validation unavailable.");
        if (step == RebirthSurvivorCreatorStep.Profile) return string.Empty;
        if (step != RebirthSurvivorCreatorStep.Background)
            return BuildValidationSummary(result);

        // Background should not nag about the Diet step before the player reaches it.
        StringBuilder text = new StringBuilder();
        int shown = 0;
        int hidden = 0;
        for (int i = 0; i < result.Errors.Count; i++)
        {
            RebirthSurvivorCreationError error = result.Errors[i];
            if (error == null) continue;
            if (error.Code == RebirthSurvivorCreationErrorCode.MissingDiet)
            {
                hidden++;
                continue;
            }
            if (shown >= 3) continue;
            if (shown > 0) text.Append("\n");
            text.Append("• ").Append(FormatCreationError(error));
            shown++;
        }

        if (shown == 0) return string.Empty;
        int remainingVisible = Math.Max(0, result.Errors.Count - hidden - shown);
        if (remainingVisible > 0)
            text.Append("\n+").Append(remainingVisible).Append(" ").Append(L("xuiRebirthSurvivorMoreIssues", "more issues"));
        return text.ToString();
    }

    public static string BuildReview(RebirthSurvivorCreatorViewModel model)
    {
        if (model == null) return string.Empty;
        RebirthSurvivorCreationResult result = model.Validation;
        RebirthBackgroundDefinition bg = model.GetSelectedBackground();
        RebirthDietDefinition diet = model.GetSelectedDiet();
        StringBuilder text = new StringBuilder(1400);

        text.Append("[D6C978]").Append(L("xuiRebirthSurvivorReviewIdentity", "SURVIVOR IDENTITY")).Append("[-]\n");
        text.Append("[FFFFFF]").Append(L("xuiRebirthSurvivorPlayerProfileTitle", "PLAYER PROFILE")).Append("[-]: ")
            .Append(model.HasPlayerProfileSelection ? model.PlayerProfileName : L("xuiRebirthSurvivorNotSelected", "Not selected")).Append("\n");
        text.Append("[FFFFFF]").Append(L("xuiRebirthSurvivorReviewBackground", "EXPERIENCE")).Append("[-]: ")
            .Append(bg != null ? L(bg.NameKey, bg.Id) : L("xuiRebirthSurvivorNotSelected", "Not selected")).Append("\n");
        text.Append("[FFFFFF]").Append(L("xuiRebirthSurvivorReviewDiet", "DIET")).Append("[-]: ")
            .Append(diet != null ? L(diet.NameKey, diet.Id) : L("xuiRebirthSurvivorNotSelected", "Not selected"));
        if (diet != null) text.Append(" [D6C978](").Append(SignedPoints(diet.Points)).Append(" pt)[-]");
        if (diet != null) text.Append("\n[AAAAAA]").Append(BuildDietCompatibilityShort(diet)).Append("[-]");
        text.Append("\n\n").Append(BuildSignatureBonusSummary(bg != null ? bg.Id : string.Empty, true));
        text.Append("\n\n[D6C978]").Append(L("xuiRebirthSurvivorStartingItems", "STARTING ITEMS")).Append("[-]\n")
            .Append(StartingItemsBullets(bg)).Append("\n\n");

        List<RebirthTraitDefinition> positive = new List<RebirthTraitDefinition>();
        List<RebirthTraitDefinition> negative = new List<RebirthTraitDefinition>();
        List<RebirthTraitDefinition> mixed = new List<RebirthTraitDefinition>();
        List<RebirthTraitDefinition> aptitudes = new List<RebirthTraitDefinition>();
        foreach (string id in model.SelectedTraitIds)
        {
            RebirthTraitDefinition trait = model.GetTrait(id);
            if (trait == null) continue;
            if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait)) aptitudes.Add(trait);
            else if (trait.Polarity == RebirthTraitPolarity.Positive) positive.Add(trait);
            else if (trait.Polarity == RebirthTraitPolarity.Negative) negative.Add(trait);
            else mixed.Add(trait);
        }
        Comparison<RebirthTraitDefinition> byName = delegate(RebirthTraitDefinition a, RebirthTraitDefinition b)
        {
            return StringComparer.CurrentCultureIgnoreCase.Compare(TraitDisplayName(a), TraitDisplayName(b));
        };
        positive.Sort(byName); negative.Sort(byName); mixed.Sort(byName); aptitudes.Sort(byName);

        AppendReviewTraitGroup(text, L("xuiRebirthSurvivorReviewPositiveTraits", "POSITIVE TRAITS"), positive, "8FD18F");
        AppendReviewTraitGroup(text, L("xuiRebirthSurvivorReviewWeaknesses", "NEGATIVE TRAITS / WEAKNESSES"), negative, "CC6B64");
        AppendReviewTraitGroup(text, L("xuiRebirthSurvivorReviewMixedTraits", "MIXED / EXPERIENCE TRAITS"), mixed, "D6C978");

        text.Append("[B58CFF]").Append(L("xuiRebirthSurvivorReviewAptitudes", "SKILL APTITUDES")).Append("[-]\n");
        if (aptitudes.Count == 0)
            text.Append(L("xuiRebirthSurvivorNone", "None")).Append("\n");
        else
        {
            for (int i = 0; i < aptitudes.Count; i++)
            {
                string skillId; int tier; int bonus; int cost;
                RebirthTraitDefinition trait = aptitudes[i];
                if (RebirthSkillAptitudeTraitFactory.TryParse(trait.Id, out skillId, out tier, out bonus, out cost))
                    text.Append("• ").Append(ResolveDefinitionName(skillId)).Append(" ").Append(RomanTier(tier))
                        .Append("  [8FD18F]+").Append(bonus).Append(" Skill[-]  [D6C978]-").Append(cost).Append(" pt[-]\n");
                else
                    text.Append("• ").Append(TraitDisplayName(trait)).Append("\n");
            }
        }
        text.Append("\n").Append(BuildTraitBudgetSummary(model)).Append("\n\n");

        if (result != null)
        {
            text.Append("[FFFFFF]").Append(L("xuiRebirthSurvivorReviewAttributes", "ATTRIBUTES")).Append("[-]\n");
            foreach (RebirthResolvedAttributeStart attr in result.Attributes)
            {
                text.Append(ResolveDefinitionName(attr.AttributeId)).Append(": ")
                    .Append(attr.Current.ToString("0.#", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(attr.Potential.ToString("0.#", CultureInfo.InvariantCulture)).Append("\n");
            }
            text.Append(L("xuiRebirthSurvivorHealthPotential", "Health Potential")).Append(": ")
                .Append(result.HealthPotential.ToString("0.#", CultureInfo.InvariantCulture)).Append("\n\n");

            AppendSupportHints(text, model);
            text.Append(result.IsValid ? "[8FD18F]" : "[CC6B64]")
                .Append(BuildValidationSummary(result)).Append("[-]");
        }

        text.Append("\n\n[D6C978]");
        if (model.Purpose == RebirthSurvivorCreatorPurpose.FirstWorldCreate)
        {
            text.Append(L("xuiRebirthSurvivorWorldCreationWarning",
                "Confirming creates this world's permanent Survivor origin. If you also save a reusable profile, later edits or deletion of that local profile will never change this world character."));
        }
        else
        {
            text.Append(L("xuiRebirthSurvivorWorldIndependenceWarning",
                "When this template creates a world character, that character becomes independent. Editing or deleting this local Survivor Profile will not change existing world characters."));
        }
        text.Append("[-]");
        return text.ToString();
    }

    private static void AppendSupportHints(StringBuilder text, RebirthSurvivorCreatorViewModel model)
    {
        if (text == null || model == null || !RebirthSurvivorDefinitionRegistry.IsReady) return;
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.SupportProfiles == null || bundle.SupportProfiles.Count == 0) return;

        bool wroteHeader = false;
        foreach (string traitId in model.SelectedTraitIds)
        {
            RebirthTraitDefinition trait = model.GetTrait(traitId);
            if (trait == null || trait.Polarity != RebirthTraitPolarity.Negative) continue;

            List<string> supportNames = new List<string>();
            foreach (RebirthTraitSupportProfileDefinition support in bundle.SupportProfiles)
            {
                if (support == null || !ContainsId(support.SupportedTraitIds, trait.Id)) continue;
                supportNames.Add(L(support.NameKey, support.Id));
            }
            if (supportNames.Count == 0) continue;
            supportNames.Sort(StringComparer.OrdinalIgnoreCase);

            if (!wroteHeader)
            {
                text.Append("[FFFFFF]").Append(L("xuiRebirthSurvivorReviewSupport", "MANAGEMENT / SUPPORT")).Append("[-]\n");
                wroteHeader = true;
            }
            text.Append(L(trait.NameKey, trait.Id)).Append(": ");
            for (int i = 0; i < supportNames.Count; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(supportNames[i]);
            }
            text.Append("\n");
        }

        if (wroteHeader) text.Append("\n");
    }

    private static bool ContainsId(IEnumerable<string> values, string id)
    {
        if (values == null || string.IsNullOrEmpty(id)) return false;
        foreach (string value in values)
            if (string.Equals(value, id, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static string BuildTraitCaption(RebirthSurvivorTraitChoice choice)
    {
        if (choice == null || choice.Definition == null) return string.Empty;
        RebirthTraitDefinition trait = choice.Definition;
        bool aptitude = RebirthSkillAptitudeTraitFactory.IsAptitude(trait);
        string badge;
        if (aptitude) badge = "[B58CFF]" + L("xuiRebirthSurvivorTraitBadgeAptitude", "APTITUDE") + "[-]";
        else if (trait.Availability == RebirthDefinitionAvailability.Restricted) badge = "[E0B35C]" + L("xuiRebirthSurvivorTraitBadgeBackground", "EXPERIENCE") + "[-]";
        else if (trait.Polarity == RebirthTraitPolarity.Positive) badge = "[8FD18F]" + L("xuiRebirthSurvivorTraitBadgePositive", "POSITIVE") + "[-]";
        else if (trait.Polarity == RebirthTraitPolarity.Negative) badge = "[CC6B64]" + L("xuiRebirthSurvivorTraitBadgeNegative", "NEGATIVE") + "[-]";
        else badge = "[D6C978]" + L("xuiRebirthSurvivorTraitBadgeMixed", "MIXED") + "[-]";

        string state = choice.IsSelected ? "[8FD18F]✓[-]" : (choice.CanToggle ? "[B8B8B8]○[-]" : "[707070]×[-]");
        string issue = string.Empty;
        if (!choice.IsEligibleForBackground) issue = " [707070]" + L("xuiRebirthSurvivorTraitStateLocked", "LOCKED") + "[-]";
        else if (choice.HasDietConflict) issue = " [E7A15A]" + L("xuiRebirthSurvivorTraitStateDietConflict", "DIET CONFLICT") + "[-]";
        else if (choice.HasSelectedTraitConflict) issue = " [CC6B64]" + L("xuiRebirthSurvivorTraitStateConflict", "CONFLICT") + "[-]";
        string nameColor = choice.CanToggle || choice.IsSelected ? "FFFFFF" : "8A8A8A";
        return state + " " + badge + " [" + nameColor + "]" + TraitDisplayName(trait) + "[-]  [D6C978]" + PointText(trait) + " pt[-]" + issue;
    }

    public static string BuildTraitDetails(RebirthSurvivorTraitChoice choice, RebirthSurvivorCreatorViewModel model)
    {
        if (choice == null || choice.Definition == null) return string.Empty;
        RebirthTraitDefinition trait = choice.Definition;
        StringBuilder text = new StringBuilder();
        text.Append("[FFFFFF]").Append(TraitDisplayName(trait)).Append("[-]\n")
            .Append(TraitDescription(trait)).Append("\n\n")
            .Append(L("xuiRebirthSurvivorTraitCategory", "Category")).Append(": ").Append(trait.Category).Append("\n")
            .Append(L("xuiRebirthSurvivorTraitPoints", "Points")).Append(": ").Append(PointText(trait));
        if (!choice.IsEligibleForBackground)
            text.Append("\n[CC6B64]").Append(L("xuiRebirthSurvivorTraitUnavailableBackground", "Unavailable for this Experience.")).Append("[-]");
        if (choice.HasSelectedTraitConflict)
            text.Append("\n[CC6B64]").Append(L("xuiRebirthSurvivorTraitConflictWith", "Conflicts with")).Append(": ").Append(ResolveDefinitionName(choice.BlockingTraitId)).Append("[-]");
        if (choice.HasDietConflict)
            text.Append("\n[CC6B64]").Append(L("xuiRebirthSurvivorTraitDietConflict", "Conflicts with the selected Diet.")).Append("[-]");
        if (model != null)
            text.Append("\n\n").Append(BuildTraitBudgetSummary(model));
        return text.ToString();
    }

    public static string BuildBackgroundDetails(RebirthBackgroundDefinition bg)
    {
        if (bg == null) return L("xuiRebirthSurvivorChooseBackgroundPrompt", "Choose an Experience to see its details.");

        // Keep the information dense enough for the creator while giving each gameplay section
        // a clear visual hierarchy. 7DTD XUi labels understand the existing [RRGGBB] BBCode
        // convention used throughout REBIRTH, so no new assets or custom fonts are required.
        const string Heading = "E41215";
        const string Muted = "A8A8A8";
        StringBuilder text = new StringBuilder();

        text.Append("[FFFFFF]").Append(L(bg.NameKey, bg.Id)).Append("[-]\n")
            .Append("[").Append(Muted).Append("]")
            .Append(L(bg.DescriptionKey, bg.Identity)).Append("[-]");

        if (bg.StartingSkills.Count > 0)
        {
            text.Append("\n\n[").Append(Heading).Append("]")
                .Append(L("xuiRebirthSurvivorStartingSkillBiases", "Starting Skills"))
                .Append("[-]\n");
            foreach (RebirthStartingSkillBiasDefinition skill in bg.StartingSkills)
            {
                text.Append("[FFFFFF]• ").Append(ResolveDefinitionName(skill.SkillId)).Append("[-] ")
                    .Append("[").Append(Muted).Append("](").Append(skill.TierId).Append(")[-]\n");
            }
        }

        if (bg.StartingKnowledgeIds.Count > 0)
        {
            text.Append("\n[").Append(Heading).Append("]")
                .Append(L("xuiRebirthSurvivorStartingKnowledge", "Starting Recipes & Techniques"))
                .Append("[-]\n");
            foreach (string id in bg.StartingKnowledgeIds)
                text.Append("[FFFFFF]• ").Append(ResolveDefinitionName(id)).Append("[-]\n");
        }

        return text.ToString().TrimEnd();
    }

    public static string BuildDietDetails(RebirthDietDefinition diet)
    {
        if (diet == null) return L("xuiRebirthSurvivorChooseDietPrompt", "Choose a Diet to see its details.");
        StringBuilder text = new StringBuilder();
        text.Append("[FFFFFF]").Append(L(diet.NameKey, diet.Id)).Append("[-]   ")
            .Append("[D6C978]").Append(SignedPoints(diet.Points)).Append(" Trait Points[-]\n")
            .Append("[B8B8B8]").Append(L(diet.DescriptionKey, diet.RuleSummary)).Append("[-]")
            .Append("\n\n[FFFFFF]").Append(L("xuiRebirthSurvivorDietRule", "DIET RULE")).Append("[-]\n")
            .Append(diet.RuleSummary)
            .Append("\n\n[E7A15A]").Append(L("xuiRebirthSurvivorDietOffDietHeading", "OFF-DIET FOOD")).Append("[-]  ")
            .Append(L("xuiRebirthSurvivorDietOffDietCompact",
                "Restricted food is still edible and keeps its physical Nutrition/hydration, but positive enjoyment is suppressed and Diet Satisfaction/Mood is reduced when nutrients are absorbed."));
        return text.ToString();
    }

    public static string ResolveDefinitionName(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        switch (id.ToLowerInvariant())
        {
            case "constitution": return L("xuiRebirthAttributeConstitution", "Constitution");
            case "dexterity": return L("xuiRebirthAttributeDexterity", "Dexterity");
            case "strength": return L("xuiRebirthAttributeStrength", "Strength");
            case "intelligence": return L("xuiRebirthAttributeIntelligence", "Intelligence");
            case "charisma": return L("xuiRebirthAttributeCharisma", "Charisma");
        }
        RebirthBackgroundDefinition bg;
        if (RebirthSurvivorDefinitionRegistry.TryGetBackground(id, out bg) && bg != null) return L(bg.NameKey, id);
        RebirthTraitDefinition trait;
        if (RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out trait) && trait != null) return TraitDisplayName(trait);
        RebirthDietDefinition diet;
        if (RebirthSurvivorDefinitionRegistry.TryGetDiet(id, out diet) && diet != null) return L(diet.NameKey, id);
        RebirthSkillDefinition skill;
        if (RebirthSurvivorDefinitionRegistry.TryGetSkill(id, out skill) && skill != null) return RebirthSkillDisplayNames.Get(id);
        RebirthKnowledgeDefinition knowledge;
        if (RebirthSurvivorDefinitionRegistry.TryGetKnowledge(id, out knowledge) && knowledge != null) return L(knowledge.NameKey, id);
        return id;
    }

    public static string TraitDisplayName(RebirthTraitDefinition trait)
    {
        if (trait == null) return string.Empty;
        string skillId; int tier; int bonus; int cost;
        if (RebirthSkillAptitudeTraitFactory.TryParse(trait.Id, out skillId, out tier, out bonus, out cost))
        {
            string skillName = ResolveDefinitionName(skillId);
            return string.Format(CultureInfo.InvariantCulture,
                L("xuiRebirthSurvivorSkillAptitudeNameFormat", "Aptitude: {0} {1}"),
                skillName, RomanTier(tier));
        }
        return L(trait.NameKey, trait.Id);
    }

    public static string TraitDescription(RebirthTraitDefinition trait)
    {
        if (trait == null) return string.Empty;
        string skillId; int tier; int bonus; int cost;
        if (RebirthSkillAptitudeTraitFactory.TryParse(trait.Id, out skillId, out tier, out bonus, out cost))
        {
            return string.Format(CultureInfo.InvariantCulture,
                L("xuiRebirthSurvivorSkillAptitudeDescFormat",
                    "Natural aptitude starts {0} at +{1}. This changes only starting proficiency; the Skill still grows through use and has no special mastery ceiling."),
                ResolveDefinitionName(skillId), bonus);
        }
        return L(trait.DescriptionKey, trait.EffectSummary);
    }

    public static string BuildTraitBudgetSummary(RebirthSurvivorCreatorViewModel model)
    {
        TraitBudgetBreakdown b = GetTraitBudgetBreakdown(model);
        if (b == null) return string.Empty;
        StringBuilder text = new StringBuilder();
        text.Append("[D6C978]").Append(L("xuiRebirthSurvivorTraitBudget", "TRAIT-POINT BUDGET")).Append("[-]\n")
            .Append(L("xuiRebirthSurvivorTraitBudgetBase", "Base")).Append(": ").Append(SignedPoints(b.Base)).Append("\n")
            .Append("+ ").Append(L("xuiRebirthSurvivorTraitBudgetBackground", "Experience")).Append(": ").Append(SignedPoints(b.Background)).Append("\n")
            .Append("+ ").Append(L("xuiRebirthSurvivorTraitBudgetDiet", "Diet")).Append(": ").Append(SignedPoints(b.Diet)).Append("\n")
            .Append("+ ").Append(L("xuiRebirthSurvivorTraitBudgetNegatives", "Negative Trait refunds")).Append(": ").Append(SignedPoints(b.NegativeApplied));
        if (RebirthSurvivorTraitPointEconomy.HasRefundCap(b.NegativeCap) && b.NegativeRequested > b.NegativeApplied)
            text.Append(" [808080](+").Append(b.NegativeRequested).Append(" requested; +").Append(b.NegativeCap).Append(" cap)[-]");
        text.Append("\n- ").Append(L("xuiRebirthSurvivorTraitBudgetPositiveTraits", "Positive Traits")).Append(": ").Append(b.PositiveSpent)
            .Append("\n- ").Append(L("xuiRebirthSurvivorTraitBudgetAptitudes", "Skill Aptitudes")).Append(": ").Append(b.AptitudeSpent)
            .Append("\n[FFFFFF]= ").Append(L("xuiRebirthSurvivorTraitBudgetRemaining", "Remaining")).Append(": ").Append(b.Remaining).Append("[-]");
        return text.ToString();
    }

    public static string BuildTraitBudgetCompact(RebirthSurvivorCreatorViewModel model)
    {
        TraitBudgetBreakdown b = GetTraitBudgetBreakdown(model);
        if (b == null) return string.Empty;
        return "[D6C978]" + L("xuiRebirthSurvivorTraitBudget", "TRAIT-POINT BUDGET") + "[-]  " +
            L("xuiRebirthSurvivorTraitBudgetBase", "Base") + " " + SignedPoints(b.Base) + "  +  " +
            L("xuiRebirthSurvivorTraitBudgetBackground", "Experience") + " " + SignedPoints(b.Background) + "  +  " +
            L("xuiRebirthSurvivorTraitBudgetDiet", "Diet") + " " + SignedPoints(b.Diet) + "  +  " +
            L("xuiRebirthSurvivorTraitBudgetNegativesShort", "Neg") + " " + SignedPoints(b.NegativeApplied) + "\n" +
            "- " + L("xuiRebirthSurvivorTraitBudgetPositiveShort", "Positive") + " " + b.PositiveSpent.ToString(CultureInfo.InvariantCulture) +
            "  -  " + L("xuiRebirthSurvivorTraitBudgetAptitudesShort", "Aptitudes") + " " + b.AptitudeSpent.ToString(CultureInfo.InvariantCulture) +
            "  =  [FFFFFF]" + L("xuiRebirthSurvivorTraitBudgetRemaining", "Remaining") + " " + b.Remaining.ToString(CultureInfo.InvariantCulture) + "[-]";
    }

    public static int GetTraitPointsRemaining(RebirthSurvivorCreatorViewModel model)
    {
        TraitBudgetBreakdown b = GetTraitBudgetBreakdown(model);
        return b != null ? b.Remaining : 0;
    }

    public static string BuildSelectedTraitSummary(RebirthSurvivorCreatorViewModel model)
    {
        if (model == null || model.SelectedTraitIds.Count == 0)
            return "[808080]" + L("xuiRebirthSurvivorNoTraitsSelected", "No Traits selected yet.") + "[-]";

        List<RebirthTraitDefinition> positive = new List<RebirthTraitDefinition>();
        List<RebirthTraitDefinition> mixed = new List<RebirthTraitDefinition>();
        List<RebirthTraitDefinition> negative = new List<RebirthTraitDefinition>();
        List<RebirthTraitDefinition> aptitude = new List<RebirthTraitDefinition>();
        foreach (string id in model.SelectedTraitIds)
        {
            RebirthTraitDefinition trait = model.GetTrait(id);
            if (trait == null) continue;
            if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait)) aptitude.Add(trait);
            else if (trait.Polarity == RebirthTraitPolarity.Positive) positive.Add(trait);
            else if (trait.Polarity == RebirthTraitPolarity.Negative) negative.Add(trait);
            else mixed.Add(trait);
        }

        Comparison<RebirthTraitDefinition> byName = delegate(RebirthTraitDefinition a, RebirthTraitDefinition b)
        {
            return StringComparer.CurrentCultureIgnoreCase.Compare(TraitDisplayName(a), TraitDisplayName(b));
        };
        positive.Sort(byName); mixed.Sort(byName); negative.Sort(byName); aptitude.Sort(byName);

        StringBuilder text = new StringBuilder();
        AppendSelectedTraitRows(text, positive, "8FD18F", "+");
        AppendSelectedTraitRows(text, mixed, "D6C978", "•");
        AppendSelectedTraitRows(text, negative, "CC6B64", "−");
        AppendSelectedTraitRows(text, aptitude, "B58CFF", "◆");
        return text.ToString().TrimEnd();
    }

    private static void AppendSelectedTraitRows(StringBuilder text, IList<RebirthTraitDefinition> traits, string color, string marker)
    {
        if (text == null || traits == null) return;
        for (int i = 0; i < traits.Count; i++)
        {
            RebirthTraitDefinition trait = traits[i];
            text.Append("[").Append(color).Append("]").Append(marker).Append("[-] ")
                .Append(TraitDisplayName(trait)).Append("   [").Append(color).Append("]")
                .Append(PointText(trait)).Append("[-]\n");
        }
    }

    public static string BuildDietCompatibilityShort(RebirthDietDefinition diet)
    {
        if (diet == null) return string.Empty;
        string rule = (diet.CompositionRule ?? string.Empty).Trim().ToLowerInvariant();
        switch (rule)
        {
            case "unrestricted": return L("xuiRebirthSurvivorDietShortUnrestricted", "All authored foods are compatible.");
            case "pescatarian": return L("xuiRebirthSurvivorDietShortPescatarian", "Fish and seafood are allowed; avoids meat and rendered animal fat.");
            case "vegetarian": return L("xuiRebirthSurvivorDietShortVegetarian", "Avoids meat, fish, and rendered animal fat.");
            case "carnivore": return L("xuiRebirthSurvivorDietShortCarnivore", "Avoids foods containing plant ingredients.");
            case "vegan": return L("xuiRebirthSurvivorDietShortVegan", "Plant-only; avoids all animal-derived ingredients.");
            default: return diet.RuleSummary ?? string.Empty;
        }
    }

    private static void AppendReviewTraitGroup(StringBuilder text, string heading, IList<RebirthTraitDefinition> traits, string color)
    {
        if (text == null) return;
        text.Append("[").Append(color).Append("]").Append(heading).Append("[-]\n");
        if (traits == null || traits.Count == 0)
        {
            text.Append(L("xuiRebirthSurvivorNone", "None")).Append("\n\n");
            return;
        }
        for (int i = 0; i < traits.Count; i++)
        {
            RebirthTraitDefinition trait = traits[i];
            text.Append("• ").Append(TraitDisplayName(trait)).Append("  [D6C978]").Append(PointText(trait)).Append(" pt[-]");
            if (trait != null && trait.Availability == RebirthDefinitionAvailability.Restricted)
                text.Append(" [E0B35C]").Append(L("xuiRebirthSurvivorTraitBadgeBackground", "EXPERIENCE")).Append("[-]");
            text.Append("\n");
        }
        text.Append("\n");
    }

    private sealed class TraitBudgetBreakdown
    {
        public int Base;
        public int Background;
        public int Diet;
        public int NegativeRequested;
        public int NegativeApplied;
        public int NegativeCap;
        public int PositiveSpent;
        public int AptitudeSpent;
        public int Remaining;
    }

    private static TraitBudgetBreakdown GetTraitBudgetBreakdown(RebirthSurvivorCreatorViewModel model)
    {
        if (model == null) return null;
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null) return null;
        RebirthBackgroundDefinition bg = model.GetSelectedBackground();
        RebirthDietDefinition diet = model.GetSelectedDiet();
        TraitBudgetBreakdown b = new TraitBudgetBreakdown();
        b.Base = bundle.Progression.BaseCreationPoints;
        b.Background = bg != null ? bg.CreationPointModifier : 0;
        if (bg != null && string.Equals(bg.Id, RebirthSurvivorIds.BackgroundCleanSlate, StringComparison.OrdinalIgnoreCase))
            b.Background += bundle.Progression.CleanSlateBonus;
        b.Diet = diet != null ? diet.Points : 0;
        b.NegativeCap = bundle.Progression.MaxNegativeTraitRefund;
        foreach (string id in model.SelectedTraitIds)
        {
            RebirthTraitDefinition trait = model.GetTrait(id);
            if (trait == null) continue;
            if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait)) b.AptitudeSpent += trait.Points;
            else if (trait.Polarity == RebirthTraitPolarity.Positive) b.PositiveSpent += trait.Points;
            else if (trait.Polarity == RebirthTraitPolarity.Negative) b.NegativeRequested += trait.Points;
        }
        b.NegativeApplied = RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund(b.NegativeCap, b.NegativeRequested);
        b.Remaining = b.Base + b.Background + b.Diet + b.NegativeApplied - b.PositiveSpent - b.AptitudeSpent;
        return b;
    }

    public static string SignedPoints(int value)
    {
        return value > 0 ? "+" + value.ToString(CultureInfo.InvariantCulture) :
            value.ToString(CultureInfo.InvariantCulture);
    }

    private static string SignedNumber(float value)
    {
        return (value > 0f ? "+" : string.Empty) + value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string RomanTier(int tier)
    {
        switch (tier)
        {
            case 1: return "I";
            case 2: return "II";
            default: return "III";
        }
    }

    public static string PolarityMarker(RebirthTraitDefinition trait)
    {
        if (trait == null) return "[B0B0B0]•[-]";
        switch (trait.Polarity)
        {
            case RebirthTraitPolarity.Positive: return "[8FD18F]+[-]";
            case RebirthTraitPolarity.Negative: return "[CC6B64]−[-]";
            default: return "[D6C978]•[-]";
        }
    }

    public static string PointText(RebirthTraitDefinition trait)
    {
        if (trait == null || trait.Points == 0 || (trait.Polarity != RebirthTraitPolarity.Positive && trait.Polarity != RebirthTraitPolarity.Negative)) return "0";
        return trait.Polarity == RebirthTraitPolarity.Positive
            ? "-" + trait.Points.ToString(CultureInfo.InvariantCulture)
            : "+" + trait.Points.ToString(CultureInfo.InvariantCulture);
    }
    public static List<string> OrderedStartingKnowledgeIds(IList<string> knowledgeIds)
    {
        List<string> ordered = new List<string>();
        if (knowledgeIds != null)
        {
            for (int i = 0; i < knowledgeIds.Count; i++)
            {
                string id = knowledgeIds[i];
                if (!string.IsNullOrEmpty(id)) ordered.Add(id);
            }
        }

        ordered.Sort(delegate(string a, string b)
        {
            int groupA = StartingKnowledgeDisplayGroup(a);
            int groupB = StartingKnowledgeDisplayGroup(b);
            if (groupA != groupB) return groupA.CompareTo(groupB);

            string nameA = ResolveDefinitionName(a);
            string nameB = ResolveDefinitionName(b);
            int byName = StringComparer.CurrentCultureIgnoreCase.Compare(nameA, nameB);
            return byName != 0 ? byName : StringComparer.OrdinalIgnoreCase.Compare(a, b);
        });
        return ordered;
    }

    public static int StartingKnowledgeDisplayGroup(string knowledgeId)
    {
        if (string.IsNullOrEmpty(knowledgeId)) return 0;
        if (knowledgeId.StartsWith("recipebook.", StringComparison.OrdinalIgnoreCase)) return 1;
        if (knowledgeId.StartsWith("recipe.", StringComparison.OrdinalIgnoreCase)) return 2;

        // Procedures/techniques and other non-recipe Knowledge (for example schematics)
        // are deliberately shown before books and individual recipes.
        return 0;
    }

    public static string StartingKnowledgeIconKey(string knowledgeId)
    {
        if (string.IsNullOrEmpty(knowledgeId)) return "rb_ui_knowledge";
        if (knowledgeId.StartsWith("recipebook.", StringComparison.OrdinalIgnoreCase))
            return "rb_bonus_bookworm";
        if (knowledgeId.StartsWith("procedure.cooking.", StringComparison.OrdinalIgnoreCase))
            return "rb_skill_cooking";
        return "rb_ui_knowledge";
    }

    public static string StartingItemsInline(RebirthBackgroundDefinition background)
    {
        if (background == null || background.StartingItems == null || background.StartingItems.Count == 0)
            return L("xuiRebirthSurvivorNoStartingItems", "None");
        StringBuilder b = new StringBuilder(256);
        for (int i = 0; i < background.StartingItems.Count; i++)
        {
            RebirthStartingItemDefinition item = background.StartingItems[i];
            if (item == null) continue;
            if (b.Length > 0) b.Append("  •  ");
            b.Append(L(item.NameKey, item.ItemId));
            if (item.HasQuality) b.Append(" Q").Append(item.Quality.ToString(CultureInfo.InvariantCulture));
            if (item.Count > 1) b.Append(" ×").Append(item.Count.ToString(CultureInfo.InvariantCulture));
        }
        return b.ToString();
    }

    public static string StartingItemsBullets(RebirthBackgroundDefinition background)
    {
        if (background == null || background.StartingItems == null || background.StartingItems.Count == 0)
            return "• " + L("xuiRebirthSurvivorNoStartingItems", "None");
        StringBuilder b = new StringBuilder(384);
        for (int i = 0; i < background.StartingItems.Count; i++)
        {
            RebirthStartingItemDefinition item = background.StartingItems[i];
            if (item == null) continue;
            if (b.Length > 0) b.Append('\n');
            b.Append("• ").Append(L(item.NameKey, item.ItemId));
            if (item.HasQuality) b.Append(" Q").Append(item.Quality.ToString(CultureInfo.InvariantCulture));
            if (item.Count > 1) b.Append(" ×").Append(item.Count.ToString(CultureInfo.InvariantCulture));
        }
        return b.ToString();
    }

}
