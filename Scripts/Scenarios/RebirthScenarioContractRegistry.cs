using System;
using System.Text;

#nullable disable

public enum RebirthScenarioContractKind
{
    Unknown,
    NoneScenario,
    Common,
    Purge,
    Hive,
    SurvivorScenario,
    FutureCustom
}

public enum RebirthScenarioActivationMode
{
    Unknown,
    AlwaysAvailable,
    CustomGameOption,
    WorldOption,
    ServerAuthoritative,
    XmlOnly,
    FuturePlugin
}

public enum RebirthScenarioCombinationPolicy
{
    Unknown,
    Exclusive,
    CanCombine,
    CommonOnly,
    RequiresManualReview
}

public enum RebirthScenarioRuntimePolicyMode
{
    Unknown,
    Disabled,
    CachedSnapshot,
    EventDriven,
    SchedulerDriven,
    XmlLoadTimeOnly,
    ManualReviewRequired
}

public sealed class RebirthScenarioContractDecl
{
    public string ScenarioId;
    public string DisplayName;
    public RebirthScenarioContractKind Kind;
    public RebirthScenarioActivationMode ActivationMode;
    public RebirthScenarioCombinationPolicy CombinationPolicy;
    public RebirthScenarioRuntimePolicyMode RuntimePolicyMode;
    public string Current2_6Surface;
    public string PrimaryOwnerModule;
    public bool HasServerAuthorityConcern;
    public bool HasClientDisplayConcern;
    public bool HasXmlOwnershipConcern;
    public bool HasHotPathConcern;
    public string Notes;
}

/// <summary>
/// First-class scenario contract registry.
/// This is read-only and does not activate/deactivate scenarios.
/// It is intentionally built after the 2.6 surface ledger.
/// </summary>
public static class RebirthScenarioContractRegistry
{
    private static readonly RebirthScenarioContractDecl[] s_contracts = new[]
    {
        new RebirthScenarioContractDecl
        {
            ScenarioId = "none",
            DisplayName = "No Scenario / Baseline",
            Kind = RebirthScenarioContractKind.NoneScenario,
            ActivationMode = RebirthScenarioActivationMode.AlwaysAvailable,
            CombinationPolicy = RebirthScenarioCombinationPolicy.Exclusive,
            RuntimePolicyMode = RebirthScenarioRuntimePolicyMode.CachedSnapshot,
            Current2_6Surface = "Baseline/no custom scenario paths still share common systems.",
            PrimaryOwnerModule = "scenario.runtime",
            HasServerAuthorityConcern = false,
            HasClientDisplayConcern = false,
            HasXmlOwnershipConcern = true,
            HasHotPathConcern = true,
            Notes = "Baseline must remain measurable against scenario-enabled configurations."
        },
        new RebirthScenarioContractDecl
        {
            ScenarioId = "common",
            DisplayName = "Common Scenario Infrastructure",
            Kind = RebirthScenarioContractKind.Common,
            ActivationMode = RebirthScenarioActivationMode.AlwaysAvailable,
            CombinationPolicy = RebirthScenarioCombinationPolicy.CommonOnly,
            RuntimePolicyMode = RebirthScenarioRuntimePolicyMode.CachedSnapshot,
            Current2_6Surface = "Shared scenario helpers, options, XML cleanup, managers, and caches.",
            PrimaryOwnerModule = "scenario.common",
            HasServerAuthorityConcern = true,
            HasClientDisplayConcern = true,
            HasXmlOwnershipConcern = true,
            HasHotPathConcern = true,
            Notes = "Common owns infrastructure only; it must not become a dumping ground for scenario-specific behavior."
        },
        new RebirthScenarioContractDecl
        {
            ScenarioId = "purge",
            DisplayName = "Purge",
            Kind = RebirthScenarioContractKind.Purge,
            ActivationMode = RebirthScenarioActivationMode.CustomGameOption,
            CombinationPolicy = RebirthScenarioCombinationPolicy.CanCombine,
            RuntimePolicyMode = RebirthScenarioRuntimePolicyMode.EventDriven,
            Current2_6Surface = "Purge progress, POI discovery, notifications, skull/map markers, supply drops, sleeper/prefab/spawn logic, network packages.",
            PrimaryOwnerModule = "scenario.purge",
            HasServerAuthorityConcern = true,
            HasClientDisplayConcern = true,
            HasXmlOwnershipConcern = true,
            HasHotPathConcern = true,
            Notes = "Server owns progress/truth; client receives cached display state. Map/compass updates should be event-driven."
        },
        new RebirthScenarioContractDecl
        {
            ScenarioId = "hive",
            DisplayName = "Hive / The Descent",
            Kind = RebirthScenarioContractKind.Hive,
            ActivationMode = RebirthScenarioActivationMode.CustomGameOption,
            CombinationPolicy = RebirthScenarioCombinationPolicy.CanCombine,
            RuntimePolicyMode = RebirthScenarioRuntimePolicyMode.CachedSnapshot,
            Current2_6Surface = "Cave tunnels, underground/cave POI distinction, worldgen/prefab/spawn restrictions, blood moon spawn rules.",
            PrimaryOwnerModule = "scenario.hive",
            HasServerAuthorityConcern = true,
            HasClientDisplayConcern = false,
            HasXmlOwnershipConcern = true,
            HasHotPathConcern = true,
            Notes = "Cave tunnel helper and cave POI tag checks must remain separate concepts."
        },
        new RebirthScenarioContractDecl
        {
            ScenarioId = "survivor",
            DisplayName = "Survivor Scenario",
            Kind = RebirthScenarioContractKind.SurvivorScenario,
            ActivationMode = RebirthScenarioActivationMode.CustomGameOption,
            CombinationPolicy = RebirthScenarioCombinationPolicy.RequiresManualReview,
            RuntimePolicyMode = RebirthScenarioRuntimePolicyMode.ManualReviewRequired,
            Current2_6Surface = "The survivor token is heavily used by NPC/entity role systems and cannot be blindly treated as scenario logic.",
            PrimaryOwnerModule = "scenario.survivor",
            HasServerAuthorityConcern = true,
            HasClientDisplayConcern = true,
            HasXmlOwnershipConcern = true,
            HasHotPathConcern = true,
            Notes = "Must disambiguate ScenarioId.Survivor from EntityRole.Survivor and EntityTag.survivor before migration."
        },
        new RebirthScenarioContractDecl
        {
            ScenarioId = "future.custom",
            DisplayName = "Future / Custom Scenario",
            Kind = RebirthScenarioContractKind.FutureCustom,
            ActivationMode = RebirthScenarioActivationMode.FuturePlugin,
            CombinationPolicy = RebirthScenarioCombinationPolicy.RequiresManualReview,
            RuntimePolicyMode = RebirthScenarioRuntimePolicyMode.ManualReviewRequired,
            Current2_6Surface = "Not present as a fixed 2.6 scenario, but architecture must allow additional scenarios over time.",
            PrimaryOwnerModule = "scenario.custom",
            HasServerAuthorityConcern = true,
            HasClientDisplayConcern = true,
            HasXmlOwnershipConcern = true,
            HasHotPathConcern = true,
            Notes = "New scenarios must declare contracts, XML ownership, module bindings, performance budget, and authority before behavior."
        }
    };

    public static RebirthScenarioContractDecl[] GetSnapshot()
    {
        RebirthScenarioContractDecl[] copy = new RebirthScenarioContractDecl[s_contracts.Length];
        Array.Copy(s_contracts, copy, copy.Length);
        return copy;
    }

    public static string GetSummaryReport()
    {
        int server = 0;
        int client = 0;
        int xml = 0;
        int hot = 0;
        int manual = 0;

        for (int i = 0; i < s_contracts.Length; i++)
        {
            RebirthScenarioContractDecl c = s_contracts[i];
            if (c.HasServerAuthorityConcern)
                server++;
            if (c.HasClientDisplayConcern)
                client++;
            if (c.HasXmlOwnershipConcern)
                xml++;
            if (c.HasHotPathConcern)
                hot++;
            if (c.CombinationPolicy == RebirthScenarioCombinationPolicy.RequiresManualReview
                || c.RuntimePolicyMode == RebirthScenarioRuntimePolicyMode.ManualReviewRequired)
                manual++;
        }

        return "[RebirthScenarioContracts] Contracts: " + s_contracts.Length
            + "; server-authority concerns: " + server
            + "; client-display concerns: " + client
            + "; XML ownership concerns: " + xml
            + "; hot-path concerns: " + hot
            + "; manual-review required: " + manual
            + ". Contracts are read-only.";
    }

    public static string GetContractReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthScenarioContracts] first-class scenario contracts.");
        sb.AppendLine("  Read-only. These contracts do not activate scenarios or connect gameplay behavior.");
        sb.AppendLine("scenario | display | activation | combination | runtime | owner | server | client | xml | hot | 2.6 surface | notes");

        bool any = false;
        for (int i = 0; i < s_contracts.Length; i++)
        {
            RebirthScenarioContractDecl c = s_contracts[i];
            if (!Matches(c, f))
                continue;

            any = true;
            sb.Append(c.ScenarioId).Append(" | ")
              .Append(c.DisplayName).Append(" | ")
              .Append(c.ActivationMode).Append(" | ")
              .Append(c.CombinationPolicy).Append(" | ")
              .Append(c.RuntimePolicyMode).Append(" | ")
              .Append(c.PrimaryOwnerModule).Append(" | ")
              .Append(c.HasServerAuthorityConcern).Append(" | ")
              .Append(c.HasClientDisplayConcern).Append(" | ")
              .Append(c.HasXmlOwnershipConcern).Append(" | ")
              .Append(c.HasHotPathConcern).Append(" | ")
              .Append(c.Current2_6Surface).Append(" | ")
              .AppendLine(c.Notes);
        }

        if (!any)
            sb.AppendLine("No scenario contract matched the filter.");

        return sb.ToString();
    }

    public static string GetDetailReport(string scenarioId)
    {
        string f = (scenarioId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(f))
            return "[RebirthScenarioContracts] Missing scenario id.";

        for (int i = 0; i < s_contracts.Length; i++)
        {
            RebirthScenarioContractDecl c = s_contracts[i];
            if (!Matches(c, f))
                continue;

            StringBuilder sb = new StringBuilder(4096);
            sb.Append("[RebirthScenarioContracts] detail for ").AppendLine(c.ScenarioId);
            sb.Append("  Display: ").AppendLine(c.DisplayName);
            sb.Append("  Kind: ").AppendLine(c.Kind.ToString());
            sb.Append("  Activation: ").AppendLine(c.ActivationMode.ToString());
            sb.Append("  Combination: ").AppendLine(c.CombinationPolicy.ToString());
            sb.Append("  Runtime policy: ").AppendLine(c.RuntimePolicyMode.ToString());
            sb.Append("  Owner: ").AppendLine(c.PrimaryOwnerModule);
            sb.Append("  Server authority concern: ").AppendLine(c.HasServerAuthorityConcern.ToString());
            sb.Append("  Client display concern: ").AppendLine(c.HasClientDisplayConcern.ToString());
            sb.Append("  XML ownership concern: ").AppendLine(c.HasXmlOwnershipConcern.ToString());
            sb.Append("  Hot-path concern: ").AppendLine(c.HasHotPathConcern.ToString());
            sb.Append("  2.6 surface: ").AppendLine(c.Current2_6Surface);
            sb.Append("  Notes: ").AppendLine(c.Notes);
            return sb.ToString();
        }

        return "[RebirthScenarioContracts] No scenario matched: " + f;
    }

    public static string GetCombinationReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioContracts] scenario combination rules.");
        sb.AppendLine("scenario | policy | notes");

        for (int i = 0; i < s_contracts.Length; i++)
        {
            RebirthScenarioContractDecl c = s_contracts[i];
            sb.Append(c.ScenarioId).Append(" | ")
              .Append(c.CombinationPolicy).Append(" | ")
              .AppendLine(c.Notes);
        }

        sb.AppendLine("Future scenario combinations must be evaluated as explicit scenario sets, not scattered option checks.");
        return sb.ToString();
    }

    public static string GetAuthorityReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioContracts] authority/display concerns.");
        sb.AppendLine("scenario | server authority | client display | runtime | notes");

        for (int i = 0; i < s_contracts.Length; i++)
        {
            RebirthScenarioContractDecl c = s_contracts[i];
            sb.Append(c.ScenarioId).Append(" | ")
              .Append(c.HasServerAuthorityConcern).Append(" | ")
              .Append(c.HasClientDisplayConcern).Append(" | ")
              .Append(c.RuntimePolicyMode).Append(" | ")
              .AppendLine(c.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthScenarioContracts] safety rules:");
        sb.AppendLine("  1. Contracts are read-only.");
        sb.AppendLine("  2. Contracts do not activate/deactivate scenarios.");
        sb.AppendLine("  3. Contracts do not parse XML at runtime.");
        sb.AppendLine("  4. Contracts do not install/uninstall Harmony patches.");
        sb.AppendLine("  5. Scenarios must not directly own hot-method Harmony patches.");
        sb.AppendLine("  6. Scenarios must register policies/data into central module owners.");
        sb.AppendLine("  7. Survivor scenario must remain distinct from survivor NPC/entity role concepts.");
        sb.AppendLine("  8. Future/custom scenarios must declare contracts before behavior.");
        return sb.ToString();
    }

    private static bool Matches(RebirthScenarioContractDecl c, string filter)
    {
        if (c == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (c.ScenarioId != null && c.ScenarioId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.DisplayName != null && c.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.PrimaryOwnerModule != null && c.PrimaryOwnerModule.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.Current2_6Surface != null && c.Current2_6Surface.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.Kind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
