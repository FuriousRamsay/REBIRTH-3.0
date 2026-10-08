using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

#nullable disable

public static class RebirthSurvivorDefinitionVersion
{
    // XML authoring schema remains v3. The semantic hash format below is V5, but that is
    // intentionally independent from the on-disk Survivor definition schema.
    public const int SchemaVersion = 3;

    public static string ComputeSemanticHash(RebirthSurvivorDefinitionBundle bundle)
    {
        if (bundle == null || bundle.Progression == null) return string.Empty;
        StringBuilder b = new StringBuilder(65536);
        b.Append("SURVIVOR-DEFINITION-HASH-V5\n");
        AppendProgression(b, bundle.Progression);

        List<RebirthBackgroundDefinition> backgrounds = new List<RebirthBackgroundDefinition>(bundle.Backgrounds); backgrounds.Sort((a,z)=>StringComparer.Ordinal.Compare(a.Id,z.Id));
        foreach (RebirthBackgroundDefinition d in backgrounds)
        {
            b.Append("BG|").Append(d.Id).Append('|').Append(d.CreationPointModifier).Append('|');
            AppendSorted(b,d.RestrictedTraitIds); AppendSorted(b,d.FavoredTraitIds); AppendSorted(b,d.BlockedTraitIds);
            List<string> skills=new List<string>(); foreach (RebirthStartingSkillBiasDefinition s in d.StartingSkills) skills.Add(s.SkillId+"="+(s.HasExplicitValue?("value:"+F(s.Value)):("tier:"+s.TierId))); AppendSorted(b,skills);
            List<string> skillKnowledge=new List<string>(); foreach (RebirthStartingSkillKnowledgeDefinition k in d.StartingSkillKnowledge) skillKnowledge.Add(k.SkillId+"="+F(k.Value)); AppendSorted(b,skillKnowledge);
            // Preserve existing V5 hashes when the optional extension is absent.
            if(d.StartingAttributes.Count>0)
            {
                b.Append("attribute-starts:");
                List<string> starts=new List<string>();
                foreach(RebirthStartingAttributeDefinition a in d.StartingAttributes) starts.Add(a.AttributeId+"="+F(a.Current));
                AppendSorted(b,starts);
            }
            AppendSorted(b,d.StartingKnowledgeIds);
            List<string> starterItems=new List<string>(); foreach(RebirthStartingItemDefinition item in d.StartingItems) starterItems.Add(item.ItemId+"="+item.Count+(item.HasQuality?("@q"+item.Quality):string.Empty)+(item.IsBlock?"@block":string.Empty)); AppendSorted(b,starterItems); b.Append('\n');
        }

        List<RebirthTraitDefinition> traits = new List<RebirthTraitDefinition>(bundle.Traits); traits.Sort((a,z)=>StringComparer.Ordinal.Compare(a.Id,z.Id));
        foreach (RebirthTraitDefinition d in traits)
        {
            b.Append("TR|").Append(d.Id).Append('|').Append((int)d.Polarity).Append('|').Append(d.Points).Append('|').Append((int)d.Availability).Append('|').Append(d.ModifierId).Append('|');
            AppendSorted(b,d.AllowedBackgroundIds); AppendSorted(b,d.ConflictTraitIds); AppendSorted(b,d.ConflictDietIds); b.Append('\n');
        }

        List<RebirthDietDefinition> diets=new List<RebirthDietDefinition>(bundle.Diets); diets.Sort((a,z)=>StringComparer.Ordinal.Compare(a.Id,z.Id));
        foreach (RebirthDietDefinition d in diets) b.Append("DI|").Append(d.Id).Append('|').Append(d.Points).Append('|').Append(d.CompositionRule).Append('|').Append(d.RuleSummary).Append('\n');

        List<RebirthConditionModifierProfileDefinition> mods=new List<RebirthConditionModifierProfileDefinition>(bundle.ModifierProfiles); mods.Sort((a,z)=>StringComparer.Ordinal.Compare(a.Id,z.Id));
        foreach (RebirthConditionModifierProfileDefinition m in mods)
        {
            b.Append("MO|").Append(m.Id).Append('|').Append(m.OwnerTraitId).Append('|').Append(m.CapGroup).Append('|').Append(m.EffectSummary).Append('|').Append(m.ImplementationSurface).Append('|');
            b.Append("components=").Append(m.Components.Count).Append('|');
            foreach (RebirthConditionModifierComponent c in m.Components)
                b.Append(c.Target).Append('~').Append(c.Operation).Append('~').Append(c.Value).Append('~').Append(c.Unit).Append('~').Append(c.Phase).Append('~').Append(c.Note).Append('|');
            b.Append('\n');
        }

        List<RebirthTraitSupportProfileDefinition> supports=new List<RebirthTraitSupportProfileDefinition>(bundle.SupportProfiles); supports.Sort((a,z)=>StringComparer.Ordinal.Compare(a.Id,z.Id));
        foreach (RebirthTraitSupportProfileDefinition s in supports)
        {
            b.Append("SU|").Append(s.Id).Append('|').Append(s.Kind).Append('|').Append(s.EffectSummary).Append('|').Append(s.TimingState).Append('|').Append(s.RepeatMode).Append('|')
                .Append(s.HabitTraitId).Append('|').Append(s.GlobalCapGroup).Append('|').Append(s.EquipmentCVar).Append('|').Append(s.GearSlotId).Append('|').Append(s.GearSlotNameKey).Append('|').Append(s.GearItemId).Append('|').Append(s.GearBagSlotBonus).Append('|').Append(s.GearToolbeltSlotBonus).Append('|').Append(F(s.GearMinStrength)).Append('|').Append(F(s.GearMinConstitution)).Append('|').Append(F(s.GraceSeconds)).Append('|').Append(F(s.ManagedSeconds)).Append('|').Append(F(s.PositiveSeconds)).Append('|').Append(F(s.CooldownSeconds)).Append('|');
            AppendSorted(b,s.SupportedTraitIds); AppendSorted(b,s.ItemBindings);
            b.Append("effects=").Append(s.Effects.Count).Append('|'); foreach(RebirthTraitSupportEffectDefinition e in s.Effects) b.Append(e.Target).Append('~').Append(e.Operation).Append('~').Append(F(e.Value)).Append('~').Append(e.Scope).Append('~').Append(e.State).Append('~').Append(e.Note).Append('|'); b.Append('\n');
        }

        using (SHA256 sha=SHA256.Create())
        {
            byte[] hash=sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()));
            StringBuilder hex=new StringBuilder(hash.Length*2); for(int i=0;i<hash.Length;i++) hex.Append(hash[i].ToString("x2",CultureInfo.InvariantCulture)); return hex.ToString();
        }
    }

    public static string BuildVersionLabel(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return "survivor-v"+SchemaVersion+"-unavailable";
        return "survivor-v"+SchemaVersion+"-"+hash.Substring(0,Math.Min(12,hash.Length));
    }

    private static void AppendProgression(StringBuilder b, RebirthProgressionDefinition p)
    {
        b.Append("PR|").Append(p.BaseCreationPoints).Append('|').Append(p.CleanSlateBonus).Append('|').Append(F(p.CreationSkillMin)).Append('|').Append(F(p.CreationSkillMax)).Append('|').Append(F(p.SkillKnowledgeMin)).Append('|').Append(F(p.SkillKnowledgeMax)).Append('|').Append(F(p.BaseHealthPotential)).Append('|').Append(F(p.MinHealthPotential)).Append('|').Append(F(p.MaxHealthPotential)).Append('\n');
        List<RebirthAttributeDefinition> attrs=new List<RebirthAttributeDefinition>(p.Attributes); attrs.Sort((a,z)=>StringComparer.Ordinal.Compare(a.Id,z.Id));
        foreach(RebirthAttributeDefinition a in attrs) b.Append("AT|").Append(a.Id).Append('|').Append(F(a.BaseCurrent)).Append('|').Append(F(a.BasePotential)).Append('|').Append(F(a.Min)).Append('|').Append(F(a.Max)).Append('\n');
        List<string> tiers=new List<string>(); foreach(KeyValuePair<string,int> kv in p.SkillBiasTiers) tiers.Add(kv.Key+"="+kv.Value); AppendSorted(b,tiers); b.Append('\n');
        List<RebirthSkillDefinition> skills=new List<RebirthSkillDefinition>(p.Skills); skills.Sort((a,z)=>StringComparer.Ordinal.Compare(a.Id,z.Id)); foreach(RebirthSkillDefinition s in skills) b.Append("SK|").Append(s.Id).Append('|').Append(s.LearnByDoingSource).Append('|').Append(F(s.Min)).Append('|').Append(F(s.Max)).Append('|').Append(s.PrimaryAttributeId).Append('|').Append(s.Advanced).Append('\n');
        List<string> knowledge=new List<string>(); foreach(RebirthKnowledgeDefinition k in p.Knowledge) knowledge.Add(k.Id); AppendSorted(b,knowledge); b.Append('\n');
    }
    private static string F(float v) { return v.ToString("R",CultureInfo.InvariantCulture); }
    private static void AppendSorted(StringBuilder b, IEnumerable<string> source) { List<string> list=new List<string>(); if(source!=null) foreach(string s in source) list.Add(s??string.Empty); list.Sort(StringComparer.Ordinal); for(int i=0;i<list.Count;i++) { if(i>0)b.Append(','); b.Append(list[i]); } b.Append('|'); }
}
