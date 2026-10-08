using System;
using System.Text;

#nullable disable

public enum RebirthScenarioXmlLayerKind
{
    Unknown,
    EntityClasses,
    EntityGroups,
    Spawning,
    Buffs,
    Items,
    Loot,
    Quests,
    Progression,
    Localization,
    XUi,
    PrefabLists,
    Blocks,
    Biomes,
    Recipes,
    Cleanup,
    Compatibility,
    Tags
}

public enum RebirthScenarioXmlOwnershipKind
{
    Unknown,
    Owns,
    Extends,
    Consumes,
    CleanupReconciles,
    MustDisambiguate,
    SharedCommon,
    ExternalCompatibility
}

public enum RebirthScenarioXmlLoadPolicy
{
    Unknown,
    XmlLoadTimeOnly,
    GeneratedAtBuildTimeFuture,
    CleanupFinalLayer,
    CompatibilityLayer,
    ManualReviewRequired
}

public enum RebirthScenarioXmlRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    MustDisambiguate
}

public sealed class RebirthScenarioXmlOwnershipDecl
{
    public string OwnershipId;
    public string ScenarioId;
    public RebirthScenarioXmlLayerKind LayerKind;
    public RebirthScenarioXmlOwnershipKind OwnershipKind;
    public RebirthScenarioXmlLoadPolicy LoadPolicy;
    public RebirthScenarioXmlRisk Risk;
    public string Current2_6Surface;
    public string FutureOwnerModule;
    public string FutureContract;
    public string Notes;
}

/// <summary>
/// Read-only scenario XML ownership registry.
/// This records scenario XML ownership before physical XML reorganization.
/// It does not parse XML, patch XML, or alter runtime behavior.
/// </summary>
public static class RebirthScenarioXmlOwnershipRegistry
{
    private static readonly RebirthScenarioXmlOwnershipDecl[] s_xml = new[]
    {
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "common.cleanup",
            ScenarioId = "common",
            LayerKind = RebirthScenarioXmlLayerKind.Cleanup,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.CleanupReconciles,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.CleanupFinalLayer,
            Risk = RebirthScenarioXmlRisk.High,
            Current2_6Surface = "zzzzzzz_REBIRTH__Cleanup and broad final reconciliation XML.",
            FutureOwnerModule = "scenario.xml.cleanup",
            FutureContract = "Cleanup reconciles scenario layers but must not hide ownership.",
            Notes = "Cleanup remains final reconciliation, not the only place where ownership is understood."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "common.localization",
            ScenarioId = "common",
            LayerKind = RebirthScenarioXmlLayerKind.Localization,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.SharedCommon,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.Medium,
            Current2_6Surface = "Localization entries for scenario UI/options/notifications.",
            FutureOwnerModule = "scenario.xml.localization",
            FutureContract = "Scenario localization keys must be grouped by owner and stable ids.",
            Notes = "Avoid token ambiguity where survivor text may mean entity role or scenario."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "common.options",
            ScenarioId = "common",
            LayerKind = RebirthScenarioXmlLayerKind.XUi,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.SharedCommon,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.Medium,
            Current2_6Surface = "windows/options/XUi surfaces for custom scenario options.",
            FutureOwnerModule = "options.cache + scenario.runtime",
            FutureContract = "Scenario options resolve into cached scenario policy; UI owns rendering only.",
            Notes = "C# should not generate XUi XML."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "purge.prefabs",
            ScenarioId = "purge",
            LayerKind = RebirthScenarioXmlLayerKind.PrefabLists,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Owns,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.High,
            Current2_6Surface = "IgnorePrefabsScenario.xml, prefab include/exclude and POI tier/discovery filters.",
            FutureOwnerModule = "scenario.purge.discovery + scenario.xml.prefabs",
            FutureContract = "Purge owns POI discovery prefab lists and tier eligibility filters.",
            Notes = "Purge POI discovery must not be inferred from generic prefab cleanup alone."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "purge.entitygroups",
            ScenarioId = "purge",
            LayerKind = RebirthScenarioXmlLayerKind.EntityGroups,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Extends,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.High,
            Current2_6Surface = "entitygroups.xml scenario spawn group contributions.",
            FutureOwnerModule = "spawning.groups + scenario.purge",
            FutureContract = "Purge XML contributes named spawn groups; C# resolves to cached spawn policy.",
            Notes = "Avoid runtime group scans after init."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "purge.spawning",
            ScenarioId = "purge",
            LayerKind = RebirthScenarioXmlLayerKind.Spawning,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Extends,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.High,
            Current2_6Surface = "spawning.xml purge/scenario spawn rules.",
            FutureOwnerModule = "spawning.biomes + scenario.purge",
            FutureContract = "Purge spawning XML maps to server-authoritative spawn policy snapshots.",
            Notes = "Runtime spawning owns execution; scenario owns policy data."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "purge.quests.progression",
            ScenarioId = "purge",
            LayerKind = RebirthScenarioXmlLayerKind.Quests,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Extends,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.Medium,
            Current2_6Surface = "quests/progression entries related to purge scenario progression.",
            FutureOwnerModule = "scenario.purge.progress",
            FutureContract = "Purge quest/progression XML ownership must be explicit and tied to server progress authority.",
            Notes = "Client display state is not authoritative progress."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "purge.loot",
            ScenarioId = "purge",
            LayerKind = RebirthScenarioXmlLayerKind.Loot,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Extends,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.Medium,
            Current2_6Surface = "loot.xml entries for purge rewards/supply crates/scenario loot.",
            FutureOwnerModule = "scenario.purge.loot",
            FutureContract = "Purge loot XML must be tagged by scenario owner and event source.",
            Notes = "Needed for future supply crate/marker/reward auditing."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "purge.localization",
            ScenarioId = "purge",
            LayerKind = RebirthScenarioXmlLayerKind.Localization,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Owns,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.Low,
            Current2_6Surface = "Purge notifications, progress labels, POI tier/discovery text.",
            FutureOwnerModule = "scenario.purge.ui",
            FutureContract = "Purge localization keys should align with server-owned progress/display events.",
            Notes = "Keep UI text separate from progress calculation."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "hive.prefabs",
            ScenarioId = "hive",
            LayerKind = RebirthScenarioXmlLayerKind.PrefabLists,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Owns,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.ManualReviewRequired,
            Risk = RebirthScenarioXmlRisk.Critical,
            Current2_6Surface = "Cave POI/underground prefab tags and cave tunnel surfaces.",
            FutureOwnerModule = "world.caves + scenario.hive",
            FutureContract = "Hive prefab ownership must separate cave POIs from cave tunnel runtime checks.",
            Notes = "Do not merge tunnel-only helper with POI tag semantics."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "hive.tags",
            ScenarioId = "hive",
            LayerKind = RebirthScenarioXmlLayerKind.Tags,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Owns,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.Critical,
            Current2_6Surface = "TAG_POI_CAVE and TAG_POI_UNDERGROUND-related semantics.",
            FutureOwnerModule = "world.caves",
            FutureContract = "Hive owns tag interpretation policy but not generic tag storage.",
            Notes = "Cave POI checks must use tags; cave tunnel helper remains tunnel-only."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "hive.entitygroups",
            ScenarioId = "hive",
            LayerKind = RebirthScenarioXmlLayerKind.EntityGroups,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Extends,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.High,
            Current2_6Surface = "Hive/cave entity groups.",
            FutureOwnerModule = "spawning.groups + scenario.hive",
            FutureContract = "Hive contributes cave/tunnel spawn groups into central spawning policy.",
            Notes = "Spawn execution remains in spawning modules."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "hive.spawning",
            ScenarioId = "hive",
            LayerKind = RebirthScenarioXmlLayerKind.Spawning,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Extends,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.XmlLoadTimeOnly,
            Risk = RebirthScenarioXmlRisk.High,
            Current2_6Surface = "spawning.xml cave/tunnel/underground restrictions.",
            FutureOwnerModule = "spawning.biomes + world.caves",
            FutureContract = "Hive spawning XML must resolve to cached restrictions for cave/tunnel contexts.",
            Notes = "Blood moon cave spawn behavior requires explicit server policy."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "hive.worldgen",
            ScenarioId = "hive",
            LayerKind = RebirthScenarioXmlLayerKind.PrefabLists,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.MustDisambiguate,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.ManualReviewRequired,
            Risk = RebirthScenarioXmlRisk.Critical,
            Current2_6Surface = "DynamicPrefabDecorator and worldgen-adjacent cave behavior.",
            FutureOwnerModule = "world.prefabs",
            FutureContract = "Hive worldgen XML/prefab ownership requires manual migration review.",
            Notes = "No mechanical 2.6 to 3.0 port."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "survivor.entityclasses",
            ScenarioId = "survivor",
            LayerKind = RebirthScenarioXmlLayerKind.EntityClasses,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.MustDisambiguate,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.ManualReviewRequired,
            Risk = RebirthScenarioXmlRisk.MustDisambiguate,
            Current2_6Surface = "entityclasses.xml survivor token may mean NPC/entity role, not scenario.",
            FutureOwnerModule = "entities.roles + scenario.survivor",
            FutureContract = "Separate ScenarioId.Survivor from EntityRole.Survivor and EntityTag.survivor.",
            Notes = "Token hit alone is not scenario ownership."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "survivor.entitygroups",
            ScenarioId = "survivor",
            LayerKind = RebirthScenarioXmlLayerKind.EntityGroups,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.MustDisambiguate,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.ManualReviewRequired,
            Risk = RebirthScenarioXmlRisk.MustDisambiguate,
            Current2_6Surface = "entitygroups.xml survivor references may be NPC/entity groups.",
            FutureOwnerModule = "spawning.groups + entities.roles",
            FutureContract = "Scenario survivor groups must be explicitly separated from NPC survivor groups.",
            Notes = "Do not migrate by keyword."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "survivor.items.buffs",
            ScenarioId = "survivor",
            LayerKind = RebirthScenarioXmlLayerKind.Items,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.MustDisambiguate,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.ManualReviewRequired,
            Risk = RebirthScenarioXmlRisk.MustDisambiguate,
            Current2_6Surface = "items/buffs may use survivor-related naming for non-scenario reasons.",
            FutureOwnerModule = "items.stats + scenario.survivor",
            FutureContract = "Classify survivor item/buff XML before assigning scenario ownership.",
            Notes = "Needs manual review before migration."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "survivor.quests.progression",
            ScenarioId = "survivor",
            LayerKind = RebirthScenarioXmlLayerKind.Progression,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.MustDisambiguate,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.ManualReviewRequired,
            Risk = RebirthScenarioXmlRisk.MustDisambiguate,
            Current2_6Surface = "quests/progression may include survivor/class/NPC concepts.",
            FutureOwnerModule = "scenario.survivor + progression",
            FutureContract = "Only explicit survivor scenario progression belongs to scenario.survivor.",
            Notes = "Manual classification required."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "external.compat.scenario",
            ScenarioId = "common",
            LayerKind = RebirthScenarioXmlLayerKind.Compatibility,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.ExternalCompatibility,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.CompatibilityLayer,
            Risk = RebirthScenarioXmlRisk.High,
            Current2_6Surface = "IzayoGuns, SonjaArmor, cleanup/external compatibility scenario-adjacent patches.",
            FutureOwnerModule = "external.compat + scenario.xml",
            FutureContract = "External compatibility must remain separate from scenario ownership.",
            Notes = "Do not confuse external support with core scenario layer."
        },
        new RebirthScenarioXmlOwnershipDecl
        {
            OwnershipId = "future.custom.xml",
            ScenarioId = "future.custom",
            LayerKind = RebirthScenarioXmlLayerKind.Compatibility,
            OwnershipKind = RebirthScenarioXmlOwnershipKind.Owns,
            LoadPolicy = RebirthScenarioXmlLoadPolicy.GeneratedAtBuildTimeFuture,
            Risk = RebirthScenarioXmlRisk.High,
            Current2_6Surface = "No fixed 2.6 equivalent.",
            FutureOwnerModule = "scenario.custom + scenario.xml",
            FutureContract = "Future scenarios must declare XML ownership before behavior.",
            Notes = "A future XML generator/manifest can use this structure, but Phase 1Z is read-only."
        }
    };

    public static RebirthScenarioXmlOwnershipDecl[] GetSnapshot()
    {
        RebirthScenarioXmlOwnershipDecl[] copy = new RebirthScenarioXmlOwnershipDecl[s_xml.Length];
        for (int i=0;i<s_xml.Length;i++)
        {
            RebirthScenarioXmlOwnershipDecl source=s_xml[i];
            copy[i]=source==null?null:new RebirthScenarioXmlOwnershipDecl { OwnershipId = source.OwnershipId, ScenarioId = source.ScenarioId, LayerKind = source.LayerKind, OwnershipKind = source.OwnershipKind, LoadPolicy = source.LoadPolicy, Risk = source.Risk, Current2_6Surface = source.Current2_6Surface, FutureOwnerModule = source.FutureOwnerModule, FutureContract = source.FutureContract, Notes = source.Notes };
        }
        return copy;
    }

    public static string GetSummaryReport()
    {
        int owns = 0;
        int extends = 0;
        int cleanup = 0;
        int disambig = 0;
        int critical = 0;

        for (int i = 0; i < s_xml.Length; i++)
        {
            RebirthScenarioXmlOwnershipDecl x = s_xml[i];

            if (x.OwnershipKind == RebirthScenarioXmlOwnershipKind.Owns)
                owns++;
            if (x.OwnershipKind == RebirthScenarioXmlOwnershipKind.Extends)
                extends++;
            if (x.OwnershipKind == RebirthScenarioXmlOwnershipKind.CleanupReconciles)
                cleanup++;
            if (x.OwnershipKind == RebirthScenarioXmlOwnershipKind.MustDisambiguate || x.Risk == RebirthScenarioXmlRisk.MustDisambiguate)
                disambig++;
            if (x.Risk == RebirthScenarioXmlRisk.Critical)
                critical++;
        }

        return "[RebirthScenarioXml] Ownership entries: " + s_xml.Length
            + "; owns: " + owns
            + "; extends: " + extends
            + "; cleanup: " + cleanup
            + "; must-disambiguate: " + disambig
            + "; critical: " + critical
            + ". Registry is read-only.";
    }

    public static string GetOwnershipReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthScenarioXml] scenario XML ownership registry.");
        sb.AppendLine("  Read-only. This does not parse, patch, generate, or reorganize XML.");
        sb.AppendLine("id | scenario | layer | ownership | load policy | risk | owner | 2.6 surface | future contract | notes");

        bool any = false;
        for (int i = 0; i < s_xml.Length; i++)
        {
            RebirthScenarioXmlOwnershipDecl x = s_xml[i];
            if (!Matches(x, f))
                continue;

            any = true;
            sb.Append(x.OwnershipId).Append(" | ")
              .Append(x.ScenarioId).Append(" | ")
              .Append(x.LayerKind).Append(" | ")
              .Append(x.OwnershipKind).Append(" | ")
              .Append(x.LoadPolicy).Append(" | ")
              .Append(x.Risk).Append(" | ")
              .Append(x.FutureOwnerModule).Append(" | ")
              .Append(x.Current2_6Surface).Append(" | ")
              .Append(x.FutureContract).Append(" | ")
              .AppendLine(x.Notes);
        }

        if (!any)
            sb.AppendLine("No XML ownership entry matched the filter.");

        return sb.ToString();
    }

    public static string GetScenarioReport(string scenarioId)
    {
        string f = (scenarioId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(f))
            return "[RebirthScenarioXml] Missing scenario id.";

        return GetOwnershipReport("scenario:" + f);
    }

    public static string GetLayerReport(string layer)
    {
        string f = (layer ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(f))
            return "[RebirthScenarioXml] Missing layer.";

        return GetOwnershipReport("layer:" + f);
    }

    public static string GetDisambiguationReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioXml] XML disambiguation required.");
        sb.AppendLine("id | scenario | layer | risk | 2.6 surface | contract | notes");

        for (int i = 0; i < s_xml.Length; i++)
        {
            RebirthScenarioXmlOwnershipDecl x = s_xml[i];
            if (x.OwnershipKind != RebirthScenarioXmlOwnershipKind.MustDisambiguate && x.Risk != RebirthScenarioXmlRisk.MustDisambiguate)
                continue;

            sb.Append(x.OwnershipId).Append(" | ")
              .Append(x.ScenarioId).Append(" | ")
              .Append(x.LayerKind).Append(" | ")
              .Append(x.Risk).Append(" | ")
              .Append(x.Current2_6Surface).Append(" | ")
              .Append(x.FutureContract).Append(" | ")
              .AppendLine(x.Notes);
        }

        sb.AppendLine("Rule: XML token hits are not enough. Ownership requires explicit scenario classification.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthScenarioXml] safety rules:");
        sb.AppendLine("  1. XML ownership registry is read-only.");
        sb.AppendLine("  2. It does not parse XML at runtime.");
        sb.AppendLine("  3. It does not patch XML.");
        sb.AppendLine("  4. It does not generate XML.");
        sb.AppendLine("  5. It does not reorganize physical XML files.");
        sb.AppendLine("  6. Cleanup can reconcile layers but must not hide ownership.");
        sb.AppendLine("  7. Survivor XML references require manual scenario-vs-entity-role classification.");
        sb.AppendLine("  8. Future/custom scenarios must declare XML ownership before behavior.");
        return sb.ToString();
    }

    private static bool Matches(RebirthScenarioXmlOwnershipDecl x, string filter)
    {
        if (x == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        string f = filter;

        if (f.StartsWith("scenario:", StringComparison.OrdinalIgnoreCase))
        {
            string scenario = f.Substring("scenario:".Length);
            return x.ScenarioId != null && x.ScenarioId.IndexOf(scenario, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        if (f.StartsWith("layer:", StringComparison.OrdinalIgnoreCase))
        {
            string layer = f.Substring("layer:".Length);
            return x.LayerKind.ToString().IndexOf(layer, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        if (x.OwnershipId != null && x.OwnershipId.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (x.ScenarioId != null && x.ScenarioId.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (x.FutureOwnerModule != null && x.FutureOwnerModule.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (x.Current2_6Surface != null && x.Current2_6Surface.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (x.FutureContract != null && x.FutureContract.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (x.LayerKind.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (x.OwnershipKind.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (x.Risk.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
