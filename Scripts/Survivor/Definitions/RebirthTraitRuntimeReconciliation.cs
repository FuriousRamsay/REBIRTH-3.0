using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public enum RebirthTraitImplementationState
{
    Unknown = 0,
    Implemented = 1,
    CreationOnly = 2,
    Partial = 3,
    Deferred = 4
}

/// <summary>
/// Canonical post-Revision-2 Trait implementation-state resolver.
/// The authored modifier profile owns the state for normal Traits; generated Skill Aptitudes are
/// intentionally CreationOnly. This service is read-only and never changes immutable Survivor origin.
/// </summary>
public static class RebirthTraitRuntimeReconciliation
{
    public const string ImplementedToken = "IMPLEMENTED";
    public const string CreationOnlyToken = "CREATION_ONLY";
    public const string PartialToken = "PARTIAL";
    public const string DeferredToken = "DEFERRED";

    public static RebirthTraitImplementationState Resolve(RebirthTraitDefinition trait)
    {
        if (trait == null) return RebirthTraitImplementationState.Unknown;
        if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait)) return RebirthTraitImplementationState.CreationOnly;
        RebirthConditionModifierProfileDefinition profile;
        if (!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId, out profile) || profile == null)
            return RebirthTraitImplementationState.Unknown;
        return Parse(profile.ImplementationState);
    }

    public static RebirthTraitImplementationState Parse(string value)
    {
        string v=(value??string.Empty).Trim().Replace('-','_').ToUpperInvariant();
        if(v==ImplementedToken) return RebirthTraitImplementationState.Implemented;
        if(v==CreationOnlyToken) return RebirthTraitImplementationState.CreationOnly;
        if(v==PartialToken) return RebirthTraitImplementationState.Partial;
        if(v==DeferredToken) return RebirthTraitImplementationState.Deferred;
        return RebirthTraitImplementationState.Unknown;
    }

    public static string Token(RebirthTraitImplementationState state)
    {
        switch(state)
        {
            case RebirthTraitImplementationState.Implemented:return ImplementedToken;
            case RebirthTraitImplementationState.CreationOnly:return CreationOnlyToken;
            case RebirthTraitImplementationState.Partial:return PartialToken;
            case RebirthTraitImplementationState.Deferred:return DeferredToken;
            default:return "UNKNOWN";
        }
    }

    public static bool HasCreationComponent(RebirthTraitDefinition trait)
    {
        RebirthConditionModifierProfileDefinition profile;
        if(trait==null||!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId,out profile)||profile==null)return false;
        for(int i=0;i<profile.Components.Count;i++) if(profile.Components[i]!=null&&string.Equals(profile.Components[i].Phase,"creation",StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool HasRuntimeComponent(RebirthTraitDefinition trait)
    {
        RebirthConditionModifierProfileDefinition profile;
        if(trait==null||!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId,out profile)||profile==null)return false;
        for(int i=0;i<profile.Components.Count;i++) if(profile.Components[i]!=null&&string.Equals(profile.Components[i].Phase,"runtime",StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool HasSupportBinding(RebirthTraitDefinition trait)
    {
        if(trait==null||RebirthSurvivorDefinitionRegistry.Bundle==null)return false;
        foreach(RebirthTraitSupportProfileDefinition support in RebirthSurvivorDefinitionRegistry.Bundle.SupportProfiles)
        {
            if(support==null)continue;
            if(string.Equals(support.HabitTraitId,trait.Id,StringComparison.OrdinalIgnoreCase))return true;
            for(int i=0;i<support.SupportedTraitIds.Count;i++) if(string.Equals(support.SupportedTraitIds[i],trait.Id,StringComparison.OrdinalIgnoreCase))return true;
        }
        return false;
    }

    public static string BuildSummary()
    {
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(bundle==null)return "traitReconciliation=<definitions unavailable>";
        int implemented=0,creation=0,partial=0,deferred=0,unknown=0,selectable=0,aptitudes=0;
        for(int i=0;i<bundle.Traits.Count;i++)
        {
            RebirthTraitDefinition trait=bundle.Traits[i]; if(trait==null)continue;
            if(trait.Availability!=RebirthDefinitionAvailability.Deferred)selectable++;
            if(RebirthSkillAptitudeTraitFactory.IsAptitude(trait))aptitudes++;
            switch(Resolve(trait))
            {
                case RebirthTraitImplementationState.Implemented:implemented++;break;
                case RebirthTraitImplementationState.CreationOnly:creation++;break;
                case RebirthTraitImplementationState.Partial:partial++;break;
                case RebirthTraitImplementationState.Deferred:deferred++;break;
                default:unknown++;break;
            }
        }
        return "traitReconciliation total="+bundle.Traits.Count+" selectable="+selectable+" generatedAptitudes="+aptitudes+
            " implemented="+implemented+" creationOnly="+creation+" partial="+partial+" deferred="+deferred+" unknown="+unknown;
    }

    public static string BuildTraitReport(string idOrName)
    {
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(bundle==null)return "[REBIRTH Survivor] Trait reconciliation unavailable: definitions not loaded.";
        string q=(idOrName??string.Empty).Trim();
        StringBuilder b=new StringBuilder();
        int matches=0;
        for(int i=0;i<bundle.Traits.Count;i++)
        {
            RebirthTraitDefinition t=bundle.Traits[i]; if(t==null)continue;
            if(q.Length>0 && t.Id.IndexOf(q,StringComparison.OrdinalIgnoreCase)<0 && t.NameKey.IndexOf(q,StringComparison.OrdinalIgnoreCase)<0)continue;
            RebirthConditionModifierProfileDefinition p=null; RebirthSurvivorDefinitionRegistry.TryGetModifier(t.ModifierId,out p);
            if(matches++>0)b.AppendLine();
            b.Append(t.Id).Append(" state=").Append(Token(Resolve(t))).Append(" availability=").Append(t.Availability)
                .Append(" polarity=").Append(t.Polarity).Append(" points=").Append(t.Points).AppendLine();
            b.Append("  effect=").AppendLine(t.EffectSummary??string.Empty);
            b.Append("  surface=").AppendLine(p!=null?p.ImplementationSurface:(t.ImplementationSurface??string.Empty));
            b.Append("  path creation=").Append(HasCreationComponent(t)).Append(" runtime=").Append(HasRuntimeComponent(t)).Append(" support=").Append(HasSupportBinding(t)).AppendLine();
            if(t.ConflictTraitIds.Count>0)b.Append("  conflictsTraits=").AppendLine(Join(t.ConflictTraitIds));
            if(t.ConflictDietIds.Count>0)b.Append("  conflictsDiets=").AppendLine(Join(t.ConflictDietIds));
        }
        if(matches==0)return "[REBIRTH Survivor] Trait reconciliation: no Trait matched '"+q+"'.";
        return b.ToString().TrimEnd();
    }

    private static string Join(IEnumerable<string> values)
    {
        StringBuilder b=new StringBuilder(); foreach(string value in values){if(b.Length>0)b.Append(',');b.Append(value);}return b.ToString();
    }
}
