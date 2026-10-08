using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Functional Character-window projection for Chunk 08. It reads only the server-issued
/// owner snapshot; final visual hierarchy/polish remains owned by Chunk 15.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthSurvivorProgressionPanel : XUiController
{
    private RebirthSurvivorOwnerStateSnapshot snapshot;
    private RebirthSurvivorOwnerHeader lastHeader;
    private long lastTextRevision = long.MinValue;
    private readonly RebirthUiSortedIdProjection skillOrder = new RebirthUiSortedIdProjection();
    private float refreshAccumulator;
    private string cachedAttributes = string.Empty;
    private string cachedSkills = string.Empty;
    private string cachedKnowledge = string.Empty;


    public override void Init()
    {
        base.Init();
        RefreshSnapshot(true);
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        refreshAccumulator += Math.Max(0f, _dt);
        if (refreshAccumulator < 0.25f) return;
        refreshAccumulator = 0f;
        RefreshSnapshot(false);
    }

    private void RefreshSnapshot(bool force)
    {
        RebirthSurvivorOwnerHeader header = RebirthSurvivorClientState.GetOwnerHeader();
        long textRevision = RebirthUiProjectionTextCache.Revision;
        bool changed = !header.Equals(lastHeader);
        if (!force && !changed && textRevision == lastTextRevision) return;
        if (force || changed) snapshot = RebirthSurvivorClientState.GetOwnerStateSnapshot(out lastHeader);
        lastTextRevision = textRevision;
        cachedAttributes = BuildAttributes();
        cachedSkills = BuildSkills();
        cachedKnowledge = BuildKnowledge();
        IsDirty = true; RefreshBindings();
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        switch (bindingName)
        {
            case "rbprog_visible":
                value = (snapshot != null && snapshot.RebirthModeEnabled && snapshot.HasCharacter).ToString();
                return true;
            case "rbprog_attributes":
                value = cachedAttributes;
                return true;
            case "rbprog_skills":
                value = cachedSkills;
                return true;
            case "rbprog_knowledge":
                value = cachedKnowledge;
                return true;
            default:
                return base.GetBindingValueInternal(ref value, bindingName);
        }
    }

    private string BuildAttributes()
    {
        if (snapshot == null || !snapshot.HasCharacter) return string.Empty;
        StringBuilder b = new StringBuilder();
        AppendAttribute(b, "strength", "xuiRebirthAttributeStrength");
        AppendAttribute(b, "dexterity", "xuiRebirthAttributeDexterity");
        AppendAttribute(b, "constitution", "xuiRebirthAttributeConstitution");
        AppendAttribute(b, "intelligence", "xuiRebirthAttributeIntelligence");
        AppendAttribute(b, "charisma", "xuiRebirthAttributeCharisma");
        return b.ToString();
    }

    private void AppendAttribute(StringBuilder b, string id, string nameKey)
    {
        for (int i = 0; i < snapshot.Attributes.Count; i++)
        {
            RebirthSurvivorOwnerAttributeSnapshot a = snapshot.Attributes[i];
            if (a == null || !string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase)) continue;
            if (b.Length > 0) b.Append("   •   ");
            b.Append(Localize(nameKey, id)).Append(' ')
                .Append(a.Current.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" / ").Append(a.Potential.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
    }

    private string BuildSkills()
    {
        if (snapshot == null || !snapshot.HasCharacter) return string.Empty;
        List<string> sourceIds = new List<string>(snapshot.Skills.Count);
        Dictionary<string, RebirthSurvivorOwnerSkillSnapshot> byId = new Dictionary<string, RebirthSurvivorOwnerSkillSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (RebirthSurvivorOwnerSkillSnapshot skill in snapshot.Skills)
            if (skill != null && !string.IsNullOrEmpty(skill.Id)) { sourceIds.Add(skill.Id); byId[skill.Id] = skill; }
        IList<string> ids = skillOrder.Get(sourceIds, GetSkillName);
        List<RebirthSurvivorOwnerSkillSnapshot> ordered = new List<RebirthSurvivorOwnerSkillSnapshot>(ids.Count);
        foreach (string id in ids) ordered.Add(byId[id]);
        StringBuilder bld = new StringBuilder();
        for (int i = 0; i < ordered.Count; i += 2)
        {
            if (i > 0) bld.Append('\n');
            bld.Append(FormatSkill(ordered[i]));
            if (i + 1 < ordered.Count) bld.Append("    |    ").Append(FormatSkill(ordered[i + 1]));
        }
        return bld.ToString();
    }

    private string BuildKnowledge()
    {
        if (snapshot == null || !snapshot.HasCharacter) return string.Empty;
        if (snapshot.KnowledgeIds.Count == 0) return Localization.Get("xuiRebirthProgressionNoKnowledge");
        List<string> names = new List<string>();
        for (int i = 0; i < snapshot.KnowledgeIds.Count; i++) names.Add(RebirthKnowledgeService.GetDisplayName(snapshot.KnowledgeIds[i]));
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join(" • ", names.ToArray());
    }

    private static string FormatSkill(RebirthSurvivorOwnerSkillSnapshot s)
    {
        if (s == null) return string.Empty;
        float displayed = s.Value + Math.Max(0f, Math.Min(0.999999f, s.Progress));
        return GetSkillName(s.Id) + " " + displayed.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GetSkillName(string id)
    {
        RebirthSkillDefinition def;
        if (RebirthSurvivorDefinitionRegistry.TryGetSkill(id, out def) && def != null)
            return Localize(def.NameKey, id);
        return id ?? string.Empty;
    }

    private static string Localize(string key, string fallback)
    {
        string value = RebirthUiProjectionTextCache.L(key, fallback);
        return string.IsNullOrEmpty(value) || string.Equals(value, key, StringComparison.Ordinal) ? (fallback ?? string.Empty) : value;
    }
}
