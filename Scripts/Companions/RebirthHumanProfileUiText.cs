using System;
using System.Globalization;
using System.Linq;
using System.Text;

// Shared, read-only presentation for the same saved NPC projection in Hire and
// Companions. Names, descriptions, and signature text come from the player UI
// definition helpers rather than a second NPC localization table.
internal static class RebirthHumanProfileUiText
{
    internal static string BackgroundName(RebirthHumanProgressionProjection profile)
    {
        if (profile == null) return Unavailable;
        return RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out var definition) && definition != null
            ? RebirthSurvivorUiText.L(definition.NameKey, definition.Identity)
            : NoBackground;
    }

    internal static string BackgroundDescription(RebirthHumanProgressionProjection profile)
    {
        if (profile == null) return string.Empty;
        return RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out var definition) && definition != null
            ? RebirthSurvivorUiText.L(definition.DescriptionKey, definition.Identity)
            : RebirthSurvivorUiText.L("xuiRbNpcUiNoBackgroundDescription", "No background has been recorded for this survivor.");
    }

    internal static string SignatureDescription(RebirthHumanProgressionProjection profile)
    {
        if (profile == null) return string.Empty;
        if (!RebirthBackgroundBonusRegistry.TryGetByBackground(profile.BackgroundId, out var bonus) || bonus == null)
            return string.Empty;
        string description = RebirthSurvivorUiText.SignatureBonusDescription(profile.BackgroundId);
        bool supported = string.Equals(profile.BackgroundId, "background.firefighter", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(profile.BackgroundId, "background.soldier", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(profile.BackgroundId, "background.teacher", StringComparison.OrdinalIgnoreCase);
        return description + "\n\n" + (supported
            ? RebirthSurvivorUiText.L("xuiRbNpcUiSignatureSupported", "Applies to this survivor's available actions.")
            : RebirthSurvivorUiText.L("xuiRbNpcUiSignatureHeld", "This effect awaits a supported survivor action."));
    }

    internal static string Traits(RebirthHumanProgressionProjection profile)
    {
        if (profile == null) return string.Empty;
        var text = new StringBuilder();
        foreach (string id in profile.TraitIds ?? Array.Empty<string>())
            if (RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out var definition) && definition != null)
            {
                if (text.Length > 0) text.Append('\n');
                string color = definition.Polarity == RebirthTraitPolarity.Positive ? "8FD18F" :
                    definition.Polarity == RebirthTraitPolarity.Negative ? "CC6B64" : "B58CFF";
                text.Append('[').Append(color).Append(']')
                    .Append(RebirthSurvivorUiText.TraitDisplayName(definition)).Append("[-]");
            }
        return text.ToString();
    }

    internal static string Skills(RebirthHumanProgressionProjection profile)
    {
        if (profile == null) return Unavailable;
        var text = new StringBuilder();
        foreach (var pair in profile.Skills.OrderBy(p => RebirthSkillDisplayNames.Get(p.Key), StringComparer.CurrentCultureIgnoreCase))
            if (RebirthSurvivorDefinitionRegistry.TryGetSkill(pair.Key, out var definition) && definition != null)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(RebirthSkillDisplayNames.Get(pair.Key)).Append("  ")
                    .Append(pair.Value.ToString("0.#", CultureInfo.CurrentCulture));
                if (profile.Theory.TryGetValue(pair.Key, out float theory))
                    text.Append("  •  ").Append(RebirthSurvivorUiText.L("xuiRbNpcUiTheory", "Theory"))
                        .Append(' ').Append(theory.ToString("0.#", CultureInfo.CurrentCulture));
            }
        return text.ToString();
    }

    internal static string[] SkillIds(RebirthHumanProgressionProjection profile) => profile?.Skills == null
        ? Array.Empty<string>()
        : profile.Skills.Keys.Where(id => RebirthSurvivorDefinitionRegistry.TryGetSkill(id, out var definition) && definition != null)
            .OrderBy(id => RebirthSkillDisplayNames.Get(id), StringComparer.CurrentCultureIgnoreCase).ToArray();

    internal static string SkillRow(RebirthHumanProgressionProjection profile, string id)
    {
        if (profile == null || !profile.Skills.TryGetValue(id, out float practical)) return string.Empty;
        profile.Theory.TryGetValue(id, out float theory);
        return RebirthSkillDisplayNames.Get(id) + "     " + practical.ToString("0.#", CultureInfo.CurrentCulture) +
            "     " + RebirthSurvivorUiText.L("xuiRbNpcUiTheory", "Theory") + " " + theory.ToString("0.#", CultureInfo.CurrentCulture);
    }

    internal static string SkillDetails(RebirthHumanProgressionProjection profile, string id)
    {
        if (profile == null || !profile.Skills.TryGetValue(id, out float practical) ||
            !RebirthSurvivorDefinitionRegistry.TryGetSkill(id, out var definition) || definition == null) return Unavailable;
        profile.Theory.TryGetValue(id, out float theory);
        string attribute = RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(id);
        return RebirthSurvivorUiText.L("xuiRebirthProgressionPrimaryAttribute", "Primary Attribute") + ": " +
               RebirthSurvivorUiText.ResolveDefinitionName(attribute) + "\n\n" +
               RebirthSurvivorUiText.L("xuiRbNpcUiPractical", "Practical") + ": " + practical.ToString("0.#", CultureInfo.CurrentCulture) + "\n" +
               RebirthSurvivorUiText.L("xuiRbNpcUiTheory", "Theory") + ": " + theory.ToString("0.#", CultureInfo.CurrentCulture) + "\n\n" +
               RebirthSurvivorUiText.L("xuiRbNpcUiPracticalTraining", "Practical training") + ": " +
               RebirthCharacterUiSnapshotBuilder.SkillTrainingSource(definition) + "\n\n" +
               RebirthSurvivorUiText.L("xuiRbNpcUiTheorySources", "Theory sources") + ": " +
               RebirthCharacterUiSnapshotBuilder.TheorySource(id);
    }

    internal static string Attributes(RebirthHumanProgressionProjection profile)
    {
        if (profile?.Attributes == null || profile.AttributePotentials == null || profile.Attributes.Count == 0)
            return Unavailable;
        var text = new StringBuilder();
        foreach (string id in new[] { "strength", "dexterity", "constitution", "intelligence", "charisma" })
        {
            if (!profile.Attributes.TryGetValue(id, out float current) ||
                !profile.AttributePotentials.TryGetValue(id, out float potential)) return Unavailable;
            if (text.Length > 0) text.Append("     ");
            text.Append(RebirthSurvivorUiText.ResolveDefinitionName(id)).Append(' ')
                .Append(current.ToString("0.#", CultureInfo.CurrentCulture)).Append(" / ")
                .Append(potential.ToString("0.#", CultureInfo.CurrentCulture));
        }
        return text.ToString();
    }

    internal static string NoBackground => RebirthSurvivorUiText.L("xuiRbNpcUiNoBackground", "No background");

    internal static string Unavailable => RebirthSurvivorUiText.L("xuiRbNpcUiProfileUnavailable", "Saved survivor profile is unavailable.");
}
