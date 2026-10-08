using System;
using System.Collections.Generic;
using System.Globalization;

#nullable disable

public interface IRebirthHudTrackProvider
{
    RebirthHudTrackType Type { get; }
    bool CanTrack(string stableId);
    IList<RebirthHudTrackingId> EnumerateTrackableIds();
    bool TryResolveDisplay(string stableId, RebirthSurvivorOwnerStateSnapshot owner, out RebirthHudTrackDisplay display);
}

public static class RebirthHudTrackingRegistry
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthHudTrackType, IRebirthHudTrackProvider> Providers = new Dictionary<RebirthHudTrackType, IRebirthHudTrackProvider>();
    private static bool defaultsInstalled;
    private static long revision;
    public static long Revision { get { EnsureDefaults(); lock (Sync) return revision; } }

    public static void EnsureDefaults()
    {
        lock (Sync)
        {
            if (defaultsInstalled) return;
            RegisterInternal(new RebirthHudSkillTrackProvider());
            RegisterInternal(new RebirthHudAttributeTrackProvider());
            RegisterInternal(new RebirthHudKnowledgeTrackProvider());
            RegisterInternal(new RebirthHudSummaryTrackProvider());
            defaultsInstalled = true;
        }
    }

    public static void Register(IRebirthHudTrackProvider provider)
    {
        if (provider == null) throw new ArgumentNullException("provider");
        lock (Sync)
        {
            EnsureDefaults();
            Providers[provider.Type] = provider;
            unchecked { revision++; }
        }
    }

    private static void RegisterInternal(IRebirthHudTrackProvider provider)
    {
        Providers[provider.Type] = provider;
            unchecked { revision++; }
    }

    public static bool TryGetProvider(RebirthHudTrackType type, out IRebirthHudTrackProvider provider)
    {
        EnsureDefaults();
        lock (Sync) return Providers.TryGetValue(type, out provider) && provider != null;
    }

    public static bool CanTrack(RebirthHudTrackType type, string stableId)
    {
        IRebirthHudTrackProvider provider;
        return TryGetProvider(type, out provider) && provider.CanTrack(stableId);
    }

    public static IList<RebirthHudTrackingId> EnumerateTrackableIds(RebirthHudTrackType type)
    {
        IRebirthHudTrackProvider provider;
        return TryGetProvider(type, out provider) ? provider.EnumerateTrackableIds() : new List<RebirthHudTrackingId>();
    }

    public static bool TryResolveDisplay(RebirthHudTrackingEntryPreference entry, RebirthSurvivorOwnerStateSnapshot owner, out RebirthHudTrackDisplay display)
    {
        display = null;
        if (entry == null || !entry.Enabled) return false;
        IRebirthHudTrackProvider provider;
        return TryGetProvider(entry.Type, out provider) && provider.TryResolveDisplay(entry.StableId, owner, out display);
    }
}

internal abstract class RebirthHudTrackProviderBase : IRebirthHudTrackProvider
{
    public abstract RebirthHudTrackType Type { get; }
    public abstract bool CanTrack(string stableId);
    public abstract IList<RebirthHudTrackingId> EnumerateTrackableIds();
    public abstract bool TryResolveDisplay(string stableId, RebirthSurvivorOwnerStateSnapshot owner, out RebirthHudTrackDisplay display);

    protected static string L(string key, string fallback)
    {
        return RebirthUiProjectionTextCache.L(key ?? string.Empty, fallback ?? string.Empty);
    }
}

internal sealed class RebirthHudSkillTrackProvider : RebirthHudTrackProviderBase
{
    public override RebirthHudTrackType Type { get { return RebirthHudTrackType.Skill; } }

    public override bool CanTrack(string stableId)
    {
        RebirthSkillDefinition def;
        return RebirthSurvivorDefinitionRegistry.TryGetSkill(stableId, out def) && def != null;
    }

    public override IList<RebirthHudTrackingId> EnumerateTrackableIds()
    {
        List<RebirthHudTrackingId> result = new List<RebirthHudTrackingId>();
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null) return result;
        for (int i = 0; i < bundle.Progression.Skills.Count; i++)
        {
            RebirthSkillDefinition def = bundle.Progression.Skills[i];
            if (def != null && !string.IsNullOrEmpty(def.Id)) result.Add(new RebirthHudTrackingId(Type, def.Id));
        }
        return result;
    }

    public override bool TryResolveDisplay(string stableId, RebirthSurvivorOwnerStateSnapshot owner, out RebirthHudTrackDisplay display)
    {
        display = null;
        if (owner == null || !owner.RebirthModeEnabled || !owner.HasCharacter) return false;
        RebirthSkillDefinition def;
        if (!RebirthSurvivorDefinitionRegistry.TryGetSkill(stableId, out def) || def == null) return false;
        RebirthSurvivorOwnerSkillSnapshot state = null;
        for (int i = 0; i < owner.Skills.Count; i++)
        {
            RebirthSurvivorOwnerSkillSnapshot row = owner.Skills[i];
            if (row != null && string.Equals(row.Id, def.Id, StringComparison.OrdinalIgnoreCase)) { state = row; break; }
        }
        float value = state != null ? state.Value : def.Min;
        float progress = state != null ? Math.Max(0f, Math.Min(1f, state.Progress)) : 0f;
        display = new RebirthHudTrackDisplay
        {
            Id = new RebirthHudTrackingId(Type, def.Id),
            Atlas = "RebirthSurvivorIcons",
            Icon = RebirthSkillAptitudeTraitFactory.SkillIconKey(def.Id),
            Name = L(def.NameKey, def.Id),
            ValueText = value.ToString("0.##", CultureInfo.InvariantCulture),
            HasProgress = true,
            Progress01 = progress,
            Complete = value >= def.Max,
            SourceRevision = owner.CharacterRevision
        };
        return true;
    }
}

internal sealed class RebirthHudAttributeTrackProvider : RebirthHudTrackProviderBase
{
    public override RebirthHudTrackType Type { get { return RebirthHudTrackType.Attribute; } }

    private static RebirthAttributeDefinition Find(string stableId)
    {
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null) return null;
        for (int i = 0; i < bundle.Progression.Attributes.Count; i++)
        {
            RebirthAttributeDefinition def = bundle.Progression.Attributes[i];
            if (def != null && string.Equals(def.Id, stableId, StringComparison.OrdinalIgnoreCase)) return def;
        }
        return null;
    }

    public override bool CanTrack(string stableId) { return Find(stableId) != null; }

    public override IList<RebirthHudTrackingId> EnumerateTrackableIds()
    {
        List<RebirthHudTrackingId> result = new List<RebirthHudTrackingId>();
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null) return result;
        for (int i = 0; i < bundle.Progression.Attributes.Count; i++)
        {
            RebirthAttributeDefinition def = bundle.Progression.Attributes[i];
            if (def != null && !string.IsNullOrEmpty(def.Id)) result.Add(new RebirthHudTrackingId(Type, def.Id));
        }
        return result;
    }

    public override bool TryResolveDisplay(string stableId, RebirthSurvivorOwnerStateSnapshot owner, out RebirthHudTrackDisplay display)
    {
        display = null;
        if (owner == null || !owner.RebirthModeEnabled || !owner.HasCharacter) return false;
        RebirthAttributeDefinition def = Find(stableId);
        if (def == null) return false;
        RebirthSurvivorOwnerAttributeSnapshot state = null;
        for (int i = 0; i < owner.Attributes.Count; i++)
        {
            RebirthSurvivorOwnerAttributeSnapshot row = owner.Attributes[i];
            if (row != null && string.Equals(row.Id, def.Id, StringComparison.OrdinalIgnoreCase)) { state = row; break; }
        }
        float current = state != null ? state.Current : def.BaseCurrent;
        float potential = state != null ? state.Potential : def.BasePotential;
        string icon = string.Empty;
        switch ((def.Id ?? string.Empty).ToLowerInvariant())
        {
            case "strength": icon = "ui_game_symbol_muscle"; break;
            case "dexterity": icon = "ui_game_symbol_agility"; break;
            case "constitution": icon = "ui_game_symbol_fortitude_mastery"; break;
        }
        display = new RebirthHudTrackDisplay
        {
            Id = new RebirthHudTrackingId(Type, def.Id),
            Atlas = "UIAtlas",
            Icon = icon,
            Name = L(def.NameKey, def.Id),
            ValueText = current.ToString("0.##", CultureInfo.InvariantCulture) + "/" + potential.ToString("0.##", CultureInfo.InvariantCulture),
            HasProgress = potential > 0f,
            Progress01 = potential > 0f ? Math.Max(0f, Math.Min(1f, current / potential)) : 0f,
            Complete = current >= potential,
            SourceRevision = owner.CharacterRevision
        };
        return true;
    }
}

internal sealed class RebirthHudKnowledgeTrackProvider : RebirthHudTrackProviderBase
{
    public override RebirthHudTrackType Type { get { return RebirthHudTrackType.Knowledge; } }

    public override bool CanTrack(string stableId)
    {
        if ((stableId ?? "").StartsWith("skillknowledge:", StringComparison.OrdinalIgnoreCase))
        {
            RebirthSkillDefinition skill;
            return RebirthSurvivorDefinitionRegistry.TryGetSkill(stableId.Substring(15), out skill) && skill != null;
        }
        RebirthKnowledgeDefinition def;
        return !RebirthLiteratureService.IsInternalReadMarker(stableId) &&
            RebirthSurvivorDefinitionRegistry.TryGetKnowledge(stableId, out def) && def != null;
    }

    public override IList<RebirthHudTrackingId> EnumerateTrackableIds()
    {
        List<RebirthHudTrackingId> result = new List<RebirthHudTrackingId>();
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null) return result;
        foreach (var skill in bundle.Progression.Skills)
            result.Add(new RebirthHudTrackingId(Type, "skillknowledge:" + skill.Id));
        for (int i = 0; i < bundle.Progression.Knowledge.Count; i++)
        {
            RebirthKnowledgeDefinition def = bundle.Progression.Knowledge[i];
            if (def != null && !string.IsNullOrEmpty(def.Id) && !RebirthLiteratureService.IsInternalReadMarker(def.Id))
                result.Add(new RebirthHudTrackingId(Type, def.Id));
        }
        return result;
    }

    public override bool TryResolveDisplay(string stableId, RebirthSurvivorOwnerStateSnapshot owner, out RebirthHudTrackDisplay display)
    {
        display = null;
        if (owner == null || !owner.RebirthModeEnabled || !owner.HasCharacter) return false;
        if ((stableId ?? "").StartsWith("skillknowledge:", StringComparison.OrdinalIgnoreCase))
        {
            RebirthSkillDefinition skill;
            if (!RebirthSurvivorDefinitionRegistry.TryGetSkill(stableId.Substring(15), out skill) || skill == null) return false;
            var progression = RebirthSurvivorDefinitionRegistry.Bundle.Progression;
            float value = progression.SkillKnowledgeMin;
            foreach (var row in owner.SkillKnowledge)
                if (row != null && string.Equals(row.Id, skill.Id, StringComparison.OrdinalIgnoreCase)) { value = row.Value; break; }
            float max = progression.SkillKnowledgeMax;
            display = new RebirthHudTrackDisplay {
                Id = new RebirthHudTrackingId(Type, stableId), Atlas = "RebirthSurvivorIcons",
                Icon = RebirthSkillAptitudeTraitFactory.SkillIconKey(skill.Id),
                Name = L(skill.NameKey, skill.Id) + " | " + L("xuiRebirthHudTrackingKnowledge", "Knowledge"),
                ValueText = value.ToString("0.#", CultureInfo.InvariantCulture) + "/" + max.ToString("0.#", CultureInfo.InvariantCulture),
                HasProgress = max > 0f, Progress01 = max > 0f ? Math.Max(0f, Math.Min(1f,value/max)) : 0f,
                Complete = value >= max, SourceRevision = owner.CharacterRevision
            };
            return true;
        }
        RebirthKnowledgeDefinition def;
        if (RebirthLiteratureService.IsInternalReadMarker(stableId) ||
            !RebirthSurvivorDefinitionRegistry.TryGetKnowledge(stableId, out def) || def == null) return false;
        bool known = false;
        for (int i = 0; i < owner.KnowledgeIds.Count; i++)
            if (string.Equals(owner.KnowledgeIds[i], def.Id, StringComparison.OrdinalIgnoreCase)) { known = true; break; }
        display = new RebirthHudTrackDisplay
        {
            Id = new RebirthHudTrackingId(Type, def.Id),
            Atlas = "RebirthSurvivorIcons",
            Icon = def.AssociatedSkillIds.Count > 0 ? RebirthSkillAptitudeTraitFactory.SkillIconKey(def.AssociatedSkillIds[0]) : "rb_ui_knowledge",
            Name = L(def.NameKey, def.Id),
            ValueText = known ? "LEARNED" : "UNLEARNED",
            HasProgress = false,
            Complete = known,
            SourceRevision = owner.CharacterRevision
        };
        return true;
    }
}

internal sealed class RebirthHudSummaryTrackProvider : RebirthHudTrackProviderBase
{
    public const string KnowledgeDiscovered = "summary.knowledge_discovered";
    public override RebirthHudTrackType Type { get { return RebirthHudTrackType.Summary; } }

    public override bool CanTrack(string stableId)
    {
        return string.Equals(stableId, KnowledgeDiscovered, StringComparison.OrdinalIgnoreCase);
    }

    public override IList<RebirthHudTrackingId> EnumerateTrackableIds()
    {
        return new List<RebirthHudTrackingId> { new RebirthHudTrackingId(Type, KnowledgeDiscovered) };
    }

    public override bool TryResolveDisplay(string stableId, RebirthSurvivorOwnerStateSnapshot owner, out RebirthHudTrackDisplay display)
    {
        display = null;
        if (!CanTrack(stableId) || owner == null || !owner.RebirthModeEnabled || !owner.HasCharacter) return false;
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        int total = 0;
        if (bundle != null && bundle.Progression != null)
            foreach (RebirthKnowledgeDefinition definition in bundle.Progression.Knowledge)
                if (definition != null && !RebirthLiteratureService.IsInternalReadMarker(definition.Id)) total++;
        int current = owner.KnowledgeIds.Count;
        display = new RebirthHudTrackDisplay
        {
            Id = new RebirthHudTrackingId(Type, KnowledgeDiscovered),
            Atlas = "RebirthSurvivorIcons",
            Icon = "rb_ui_knowledge",
            Name = L("xuiRebirthHudSummaryKnowledgeDiscovered", "Recipes & Techniques Learned"),
            ValueText = current.ToString(CultureInfo.InvariantCulture) + " / " + total.ToString(CultureInfo.InvariantCulture),
            HasProgress = total > 0,
            Progress01 = total > 0 ? Math.Max(0f, Math.Min(1f, (float)current / total)) : 0f,
            Complete = total > 0 && current >= total,
            SourceRevision = owner.CharacterRevision
        };
        return true;
    }
}
