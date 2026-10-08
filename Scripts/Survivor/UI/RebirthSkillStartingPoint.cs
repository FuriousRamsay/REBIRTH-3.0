using System;
using System.Globalization;
using System.Text;

/// <summary>Read-only presentation of creation contributions, using the creation validator
/// for the final bounded start. Historical definitions must match before inferring a start.</summary>
public static class RebirthSkillStartingPoint
{
    public static string Describe(RebirthSurvivorOwnerStateSnapshot owner, RebirthSkillDefinition skill,
        float current, RebirthSurvivorCreationResult start)
    {
        if (start == null)
            return "Starting breakdown unavailable for this character's definition version.\nCurrent levels are still shown accurately.";
        float startingValue;
        if (!start.StartingSkills.TryGetValue(skill.Id, out startingValue)) return "Starting level unavailable.";

        var progression = RebirthSurvivorDefinitionRegistry.Bundle.Progression;
        float traceValue = 0f;
        var trace = new StringBuilder();
        var learning = new StringBuilder();
        RebirthBackgroundDefinition background;
        string backgroundName = owner.BackgroundId;
        if (RebirthSurvivorDefinitionRegistry.TryGetBackground(owner.BackgroundId, out background))
        {
            backgroundName = RebirthSurvivorUiText.L(background.NameKey, background.Id);
            foreach (var bias in background.StartingSkills)
            {
                if (!string.Equals(bias.SkillId, skill.Id, StringComparison.OrdinalIgnoreCase)) continue;
                int tier;
                float value = bias.HasExplicitValue ? bias.Value :
                    progression.SkillBiasTiers.TryGetValue(bias.TierId, out tier) ? tier : 0;
                traceValue += value;
                AppendTrace(trace, backgroundName, "add", value, traceValue);
            }
        }

        foreach (string id in owner.TraitIds)
        {
            RebirthTraitDefinition trait;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out trait)) continue;
            string traitName=RebirthSurvivorUiText.L(trait.NameKey, trait.Id);
            float gain = RebirthTraitGameplayModifierService.GetSkillGainMultiplier(new[] { id }, skill.Id, current);
            if (Math.Abs(gain - 1f) > 0.0001f)
            {
                if (learning.Length > 0) learning.Append(", ");
                learning.Append(traitName).Append(" ").Append(Signed((gain - 1f) * 100)).Append("%");
            }

            string aptitudeSkill; int aptitudeTier, bonus, cost;
            if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait))
            {
                if (RebirthSkillAptitudeTraitFactory.TryParse(id, out aptitudeSkill, out aptitudeTier, out bonus, out cost) &&
                    string.Equals(aptitudeSkill, skill.Id, StringComparison.OrdinalIgnoreCase))
                {
                    traceValue += bonus;
                    AppendTrace(trace, traitName, "add", bonus, traceValue);
                }
                continue;
            }

            RebirthConditionModifierProfileDefinition profile;
            if (!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId, out profile)) continue;
            foreach (var component in profile.Components)
            {
                if (!string.Equals(component.Phase, "creation", StringComparison.OrdinalIgnoreCase)) continue;
                float operand; int tier; string op=component.Operation;
                if (string.Equals(component.Target, "skill.start_bias", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(component.Value, skill.Id, StringComparison.OrdinalIgnoreCase) &&
                    progression.SkillBiasTiers.TryGetValue(component.Note, out tier))
                {
                    operand=tier; op="add";
                }
                else if (string.Equals(component.Target, "skill.start." + skill.Id, StringComparison.OrdinalIgnoreCase))
                {
                    if (!float.TryParse(component.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out operand)) continue;
                }
                else continue;
                bool recognized; float next=RebirthSurvivorCreationValidator.ApplyStartingSkillOperation(traceValue,op,operand,out recognized);
                if (!recognized) continue;
                traceValue=next;
                AppendTrace(trace,traitName,op,operand,traceValue);
            }
        }

        float creationMin=Math.Max(skill.Min,progression.CreationSkillMin);
        float creationMax=Math.Min(skill.Max,progression.CreationSkillMax);
        float clamped=Math.Max(creationMin,Math.Min(creationMax,traceValue));
        if (Math.Abs(clamped-traceValue)>0.0001f)
            AppendTrace(trace,"Starting limit","clamp",clamped,clamped);

        float knowledge = 0; start.StartingSkillKnowledge.TryGetValue(skill.Id, out knowledge);
        string reconciliation=Math.Abs(clamped-startingValue)>0.001f
            ? "\n[CC6B64]Trace differs from stored authoritative start; definition-version reconstruction is incomplete.[-]"
            : string.Empty;
        return "Background · " + backgroundName
            + "\nOrdered creation trace: " + (trace.Length==0 ? "No starting level adjustment" : trace.ToString())
            + "\nStarting level: " + Number(startingValue)
            + "   ·   Change since creation: " + Signed(current-startingValue)
            + "\nStarting theory: " + Number(knowledge) + " / " + Number(progression.SkillKnowledgeMax)
            + "\nLearning from traits: " + (learning.Length == 0 ? "Normal rate at this level" : learning.ToString())
            + reconciliation;
    }

    private static void AppendTrace(StringBuilder trace,string source,string operation,float operand,float result)
    {
        if(trace.Length>0)trace.Append(" → ");
        trace.Append(source).Append(" [").Append(string.IsNullOrEmpty(operation)?"add":operation).Append(" ")
            .Append(Signed(operand)).Append(" ⇒ ").Append(Number(result)).Append("]");
    }
    private static string Number(float value) { return value.ToString("0.#", CultureInfo.InvariantCulture); }
    private static string Signed(float value) { return value.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture); }
}
