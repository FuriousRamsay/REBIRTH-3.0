using System;
using System.Text;

#nullable disable

public enum RebirthScenarioModuleBindingKind
{
    Unknown,
    Owns,
    Uses,
    DependsOn,
    RegistersPolicyInto,
    ProvidesDisplayStateTo,
    ProvidesXmlLayerTo,
    MustNotOwn
}

public enum RebirthScenarioModuleBindingRuntime
{
    Unknown,
    LoadTimeOnly,
    CachedSnapshot,
    EventDriven,
    SchedulerDriven,
    HotPathPolicy,
    ClientDisplayOnly,
    ServerAuthoritative,
    ManualReviewRequired
}

public enum RebirthScenarioModuleBindingRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    MustDisambiguate
}

public sealed class RebirthScenarioModuleBindingDecl
{
    public string BindingId;
    public string ScenarioId;
    public string ModuleId;
    public RebirthScenarioModuleBindingKind BindingKind;
    public RebirthScenarioModuleBindingRuntime RuntimeMode;
    public RebirthScenarioModuleBindingRisk Risk;
    public string Current2_6Surface;
    public string FutureContract;
    public string Notes;
}

/// <summary>
/// Read-only scenario-to-module binding registry.
/// Scenarios register policies/data into central owners; they do not own hot-method patches.
/// </summary>
public static class RebirthScenarioModuleBindingRegistry
{
    private static readonly RebirthScenarioModuleBindingDecl[] s_bindings = new[]
    {
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "common.scenario.runtime",
            ScenarioId = "common",
            ModuleId = "scenario.runtime",
            BindingKind = RebirthScenarioModuleBindingKind.Owns,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.CachedSnapshot,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "RebirthVariables/RebirthUtilities/RebirthManager shared scenario checks.",
            FutureContract = "Own cached scenario set and resolved scenario policy snapshots.",
            Notes = "Common owns infrastructure only, not scenario-specific behavior."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "common.options",
            ScenarioId = "common",
            ModuleId = "options.cache",
            BindingKind = RebirthScenarioModuleBindingKind.Uses,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.CachedSnapshot,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "customScenario and scenario-related custom game options are globally queried.",
            FutureContract = "Resolve options once into scenario policy; hot paths read cached booleans/enums.",
            Notes = "No repeated string/option lookup in hot loops."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "purge.progress",
            ScenarioId = "purge",
            ModuleId = "scenario.purge.progress",
            BindingKind = RebirthScenarioModuleBindingKind.Owns,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.ServerAuthoritative,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "NetPackageCheckPurgeProgress / SetPurgeProgress / UpdatePurgeDisplay.",
            FutureContract = "Server owns purge progress truth; clients receive display snapshots.",
            Notes = "Prevents client-side progress drift and keeps display updates event-driven."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "purge.poi.discovery",
            ScenarioId = "purge",
            ModuleId = "scenario.purge.discovery",
            BindingKind = RebirthScenarioModuleBindingKind.Owns,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.EventDriven,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "POI discovery/purge percentage/skull notifications.",
            FutureContract = "POI discovery updates only on relevant discovery/progress events.",
            Notes = "Avoid recomputing purge state from HUD/compass/player loops."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "purge.spawning",
            ScenarioId = "purge",
            ModuleId = "spawning.groups",
            BindingKind = RebirthScenarioModuleBindingKind.RegistersPolicyInto,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.CachedSnapshot,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "Harmony_EntityGroups / SleeperVolume / prefab filters.",
            FutureContract = "Purge contributes spawn/sleeper policies into central spawning owners.",
            Notes = "Purge does not own EntityGroups or SleeperVolume patches directly."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "purge.map.markers",
            ScenarioId = "purge",
            ModuleId = "map.markers",
            BindingKind = RebirthScenarioModuleBindingKind.RegistersPolicyInto,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.EventDriven,
            Risk = RebirthScenarioModuleBindingRisk.Medium,
            Current2_6Surface = "Purge display, skulls, supply crates, map/compass indicators.",
            FutureContract = "Markers update on spawn/destroy/discovery/login events, not every HUD tick.",
            Notes = "Client display uses cached marker snapshots."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "purge.ui",
            ScenarioId = "purge",
            ModuleId = "ui.hud",
            BindingKind = RebirthScenarioModuleBindingKind.ProvidesDisplayStateTo,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.ClientDisplayOnly,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "XUiC_ClassHUDRebirth / XUiC_CompassWindowRebirth purge display.",
            FutureContract = "HUD consumes precomputed purge display state.",
            Notes = "No purge scan or percentage calculation inside UI update loops."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "purge.xml",
            ScenarioId = "purge",
            ModuleId = "scenario.xml",
            BindingKind = RebirthScenarioModuleBindingKind.ProvidesXmlLayerTo,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.LoadTimeOnly,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "Prefab include/exclude, entitygroups, quests, buffs, loot, localization.",
            FutureContract = "Purge XML ownership must be tracked by layer and reconciled by cleanup.",
            Notes = "Physical XML organization can change later; ownership is explicit now."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "hive.caves",
            ScenarioId = "hive",
            ModuleId = "world.caves",
            BindingKind = RebirthScenarioModuleBindingKind.Owns,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.CachedSnapshot,
            Risk = RebirthScenarioModuleBindingRisk.Critical,
            Current2_6Surface = "TheDescent cave tunnels, cave POIs, underground tags, blood moon spawn filtering.",
            FutureContract = "Hive owns cave scenario policy but keeps cave tunnel helper separate from cave POI tag checks.",
            Notes = "Do not merge tunnel-only helper with cave POI semantics."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "hive.spawning",
            ScenarioId = "hive",
            ModuleId = "spawning.biomes",
            BindingKind = RebirthScenarioModuleBindingKind.RegistersPolicyInto,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.CachedSnapshot,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "spawnmanagerbiomes cave/tunnel logic and blood moon spawn restrictions.",
            FutureContract = "Hive contributes biome/tunnel spawn restrictions into central spawning module.",
            Notes = "Spawning owns spawn execution; hive provides policy."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "hive.worldgen",
            ScenarioId = "hive",
            ModuleId = "world.prefabs",
            BindingKind = RebirthScenarioModuleBindingKind.RegistersPolicyInto,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.LoadTimeOnly,
            Risk = RebirthScenarioModuleBindingRisk.Critical,
            Current2_6Surface = "DynamicPrefabDecorator/worldgen cave scenario surfaces.",
            FutureContract = "Hive worldgen/prefab rules require manual review before migration.",
            Notes = "Do not mechanically port 2.6 worldgen hooks."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "hive.xml",
            ScenarioId = "hive",
            ModuleId = "scenario.xml",
            BindingKind = RebirthScenarioModuleBindingKind.ProvidesXmlLayerTo,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.LoadTimeOnly,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "Cave/underground prefabs, entitygroups, spawning, tags, cleanup layers.",
            FutureContract = "Hive XML ownership must distinguish cave tunnel logic from cave POI tags.",
            Notes = "XML ownership ledger comes next."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "survivor.scenario",
            ScenarioId = "survivor",
            ModuleId = "scenario.survivor",
            BindingKind = RebirthScenarioModuleBindingKind.Owns,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.ManualReviewRequired,
            Risk = RebirthScenarioModuleBindingRisk.MustDisambiguate,
            Current2_6Surface = "Survivor term overlaps NPC/entity roles, tags, groups, and scenario concept.",
            FutureContract = "Only ScenarioId.Survivor belongs here.",
            Notes = "Must not absorb EntityRole.Survivor or NPC companion systems by token match."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "survivor.entity.role",
            ScenarioId = "survivor",
            ModuleId = "entities.roles",
            BindingKind = RebirthScenarioModuleBindingKind.MustNotOwn,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.ManualReviewRequired,
            Risk = RebirthScenarioModuleBindingRisk.MustDisambiguate,
            Current2_6Surface = "survivor NPC/entity role/token usage across C# and XML.",
            FutureContract = "EntityRole.Survivor remains in entity/NPC ownership, not scenario ownership.",
            Notes = "This binding exists as a guardrail."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "survivor.spawning",
            ScenarioId = "survivor",
            ModuleId = "spawning.groups",
            BindingKind = RebirthScenarioModuleBindingKind.RegistersPolicyInto,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.ManualReviewRequired,
            Risk = RebirthScenarioModuleBindingRisk.MustDisambiguate,
            Current2_6Surface = "Potential survivor scenario spawn policies mixed with survivor entity groups.",
            FutureContract = "Scenario-specific spawn policies must be separated from survivor NPC/entity group definitions.",
            Notes = "Do not migrate until disambiguation ledger is complete."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "survivor.xml",
            ScenarioId = "survivor",
            ModuleId = "scenario.xml",
            BindingKind = RebirthScenarioModuleBindingKind.ProvidesXmlLayerTo,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.ManualReviewRequired,
            Risk = RebirthScenarioModuleBindingRisk.MustDisambiguate,
            Current2_6Surface = "entityclasses/entitygroups/items/buffs/quests/progression may include survivor token for non-scenario reasons.",
            FutureContract = "Survivor XML ownership requires explicit surface classification.",
            Notes = "XML token hit alone is not sufficient proof of scenario ownership."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "future.custom.contracts",
            ScenarioId = "future.custom",
            ModuleId = "scenario.custom",
            BindingKind = RebirthScenarioModuleBindingKind.Owns,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.ManualReviewRequired,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "No fixed 2.6 equivalent.",
            FutureContract = "Custom scenarios must declare contracts, bindings, XML ownership, runtime policy, and perf budget before behavior.",
            Notes = "Prevents future scenarios from becoming intermingled by default."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "all.hot.damage.forbidden",
            ScenarioId = "common",
            ModuleId = "combat.damage",
            BindingKind = RebirthScenarioModuleBindingKind.MustNotOwn,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.HotPathPolicy,
            Risk = RebirthScenarioModuleBindingRisk.Critical,
            Current2_6Surface = "Harmony_EntityAlive scenario-specific behavior in hot entity/combat patch.",
            FutureContract = "Scenarios register damage policies into combat.damage; they do not own DamageEntity patches.",
            Notes = "One hot-method owner only."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "all.hot.player.forbidden",
            ScenarioId = "common",
            ModuleId = "player.movement",
            BindingKind = RebirthScenarioModuleBindingKind.MustNotOwn,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.HotPathPolicy,
            Risk = RebirthScenarioModuleBindingRisk.Critical,
            Current2_6Surface = "Harmony_EntityPlayerLocal scenario/player event checks.",
            FutureContract = "Scenarios register player-event policies into player/system owners; they do not own player update patches.",
            Notes = "One PlayerMoveController/EntityPlayerLocal adapter owner only."
        },
        new RebirthScenarioModuleBindingDecl
        {
            BindingId = "all.hot.ui.forbidden",
            ScenarioId = "common",
            ModuleId = "ui.hud",
            BindingKind = RebirthScenarioModuleBindingKind.MustNotOwn,
            RuntimeMode = RebirthScenarioModuleBindingRuntime.ClientDisplayOnly,
            Risk = RebirthScenarioModuleBindingRisk.High,
            Current2_6Surface = "XUi class HUD / compass scenario display logic.",
            FutureContract = "UI owns drawing/update; scenarios provide cached display state.",
            Notes = "No expensive scenario calculation from UI update loops."
        }
    };

    public static RebirthScenarioModuleBindingDecl[] GetSnapshot()
    {
        RebirthScenarioModuleBindingDecl[] copy = new RebirthScenarioModuleBindingDecl[s_bindings.Length];
        for (int i=0;i<s_bindings.Length;i++)
        {
            RebirthScenarioModuleBindingDecl source=s_bindings[i];
            copy[i]=source==null?null:new RebirthScenarioModuleBindingDecl { BindingId = source.BindingId, ScenarioId = source.ScenarioId, ModuleId = source.ModuleId, BindingKind = source.BindingKind, RuntimeMode = source.RuntimeMode, Risk = source.Risk, Current2_6Surface = source.Current2_6Surface, FutureContract = source.FutureContract, Notes = source.Notes };
        }
        return copy;
    }

    public static string GetSummaryReport()
    {
        int owns = 0;
        int registers = 0;
        int xml = 0;
        int mustNotOwn = 0;
        int critical = 0;
        int disambiguate = 0;

        for (int i = 0; i < s_bindings.Length; i++)
        {
            RebirthScenarioModuleBindingDecl b = s_bindings[i];

            if (b.BindingKind == RebirthScenarioModuleBindingKind.Owns)
                owns++;
            if (b.BindingKind == RebirthScenarioModuleBindingKind.RegistersPolicyInto)
                registers++;
            if (b.BindingKind == RebirthScenarioModuleBindingKind.ProvidesXmlLayerTo)
                xml++;
            if (b.BindingKind == RebirthScenarioModuleBindingKind.MustNotOwn)
                mustNotOwn++;
            if (b.Risk == RebirthScenarioModuleBindingRisk.Critical)
                critical++;
            if (b.Risk == RebirthScenarioModuleBindingRisk.MustDisambiguate)
                disambiguate++;
        }

        return "[RebirthScenarioBindings] Bindings: " + s_bindings.Length
            + "; owns: " + owns
            + "; registers policy into: " + registers
            + "; XML layer bindings: " + xml
            + "; must-not-own guardrails: " + mustNotOwn
            + "; critical risk: " + critical
            + "; must-disambiguate: " + disambiguate
            + ". Bindings are read-only.";
    }

    public static string GetBindingReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthScenarioBindings] scenario-to-module binding registry.");
        sb.AppendLine("  Read-only. Scenarios register policies/data into central module owners.");
        sb.AppendLine("binding | scenario | module | kind | runtime | risk | 2.6 surface | future contract | notes");

        bool any = false;
        for (int i = 0; i < s_bindings.Length; i++)
        {
            RebirthScenarioModuleBindingDecl b = s_bindings[i];
            if (!Matches(b, f))
                continue;

            any = true;
            sb.Append(b.BindingId).Append(" | ")
              .Append(b.ScenarioId).Append(" | ")
              .Append(b.ModuleId).Append(" | ")
              .Append(b.BindingKind).Append(" | ")
              .Append(b.RuntimeMode).Append(" | ")
              .Append(b.Risk).Append(" | ")
              .Append(b.Current2_6Surface).Append(" | ")
              .Append(b.FutureContract).Append(" | ")
              .AppendLine(b.Notes);
        }

        if (!any)
            sb.AppendLine("No scenario binding matched the filter.");

        return sb.ToString();
    }

    public static string GetScenarioReport(string scenarioId)
    {
        string f = (scenarioId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(f))
            return "[RebirthScenarioBindings] Missing scenario id.";

        return GetBindingReport("scenario:" + f);
    }

    public static string GetModuleReport(string moduleId)
    {
        string f = (moduleId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(f))
            return "[RebirthScenarioBindings] Missing module id.";

        return GetBindingReport("module:" + f);
    }

    public static string GetGuardrailReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioBindings] must-not-own guardrails.");
        sb.AppendLine("binding | scenario | module | risk | contract | notes");

        for (int i = 0; i < s_bindings.Length; i++)
        {
            RebirthScenarioModuleBindingDecl b = s_bindings[i];
            if (b.BindingKind != RebirthScenarioModuleBindingKind.MustNotOwn)
                continue;

            sb.Append(b.BindingId).Append(" | ")
              .Append(b.ScenarioId).Append(" | ")
              .Append(b.ModuleId).Append(" | ")
              .Append(b.Risk).Append(" | ")
              .Append(b.FutureContract).Append(" | ")
              .AppendLine(b.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthScenarioBindings] safety rules:");
        sb.AppendLine("  1. Bindings are read-only.");
        sb.AppendLine("  2. Bindings do not activate/deactivate scenarios.");
        sb.AppendLine("  3. Bindings do not connect gameplay behavior.");
        sb.AppendLine("  4. Bindings do not install/uninstall Harmony patches.");
        sb.AppendLine("  5. Scenarios must register policies/data into central module owners.");
        sb.AppendLine("  6. Scenario modules must not own hot-method patches directly.");
        sb.AppendLine("  7. Survivor scenario bindings require disambiguation before migration.");
        sb.AppendLine("  8. Future/custom scenarios must declare bindings before behavior.");
        return sb.ToString();
    }

    private static bool Matches(RebirthScenarioModuleBindingDecl b, string filter)
    {
        if (b == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        string f = filter;

        if (f.StartsWith("scenario:", StringComparison.OrdinalIgnoreCase))
        {
            string scenario = f.Substring("scenario:".Length);
            return b.ScenarioId != null && b.ScenarioId.IndexOf(scenario, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        if (f.StartsWith("module:", StringComparison.OrdinalIgnoreCase))
        {
            string module = f.Substring("module:".Length);
            return b.ModuleId != null && b.ModuleId.IndexOf(module, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        if (b.BindingId != null && b.BindingId.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.ScenarioId != null && b.ScenarioId.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.ModuleId != null && b.ModuleId.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.Current2_6Surface != null && b.Current2_6Surface.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.FutureContract != null && b.FutureContract.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.BindingKind.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.Risk.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
