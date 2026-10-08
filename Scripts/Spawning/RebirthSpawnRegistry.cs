using System;
using System.Text;

#nullable disable

public enum RebirthSpawnSurface
{
    Unknown,
    Biome,
    Sleeper,
    WanderingHorde,
    BloodMoon,
    EventSpawn,
    CaveTunnel,
    CavePOI,
    TraderArea,
    CommandSpawn
}

public enum RebirthSpawnPolicyStatus
{
    Unknown,
    VanillaOnly,
    RebirthOwned,
    ExternalOwned,
    CompatibilityOnly,
    ManualReviewRequired
}

public sealed class RebirthSpawnGroupDecl
{
    public string GroupId;
    public string SourceXmlFamily;
    public RebirthSpawnSurface Surface;
    public RebirthSpawnPolicyStatus PolicyStatus;
    public string OwnerModuleId;
    public bool HotPathRelevant;
    public string RuntimeIndex;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthSpawnRuleDecl
{
    public string RuleId;
    public string OwnerModuleId;
    public string Rule;
    public string Evidence;
    public string Notes;
}

/// <summary>
/// Spawn composition/policy registry describing the installed runtime adapters.
/// </summary>
public static class RebirthSpawnRegistry
{
    private static readonly RebirthSpawnGroupDecl[] s_groups = new[]
    {
        new RebirthSpawnGroupDecl
        {
            GroupId = "spawn.biome",
            SourceXmlFamily = "biomes.xml / spawnmanagerbiomes.xml / entitygroups.xml",
            Surface = RebirthSpawnSurface.Biome,
            PolicyStatus = RebirthSpawnPolicyStatus.RebirthOwned,
            OwnerModuleId = "spawning.biomes",
            HotPathRelevant = true,
            RuntimeIndex = "RebirthSpawnCompositionService + SpawnManagerBiomes adapter",
            Evidence = "Blueprint v2/P1/P3",
            Notes = "Biome spawn checks must use cached policy and separate cave tunnel vs cave POI logic."
        },
        new RebirthSpawnGroupDecl
        {
            GroupId = "spawn.sleeper",
            SourceXmlFamily = "entitygroups.xml / sleepers",
            Surface = RebirthSpawnSurface.Sleeper,
            PolicyStatus = RebirthSpawnPolicyStatus.RebirthOwned,
            OwnerModuleId = "spawning.sleepers",
            HotPathRelevant = false,
            RuntimeIndex = "RebirthSpawnCompositionService + SleeperVolume adapter",
            Evidence = "Blueprint v2",
            Notes = "Sleeper/infested multipliers and POI tier discovery must be indexed, not ad-hoc scanned."
        },
        new RebirthSpawnGroupDecl
        {
            GroupId = "spawn.wanderinghorde",
            SourceXmlFamily = "entitygroups.xml / wandering horde settings",
            Surface = RebirthSpawnSurface.WanderingHorde,
            PolicyStatus = RebirthSpawnPolicyStatus.RebirthOwned,
            OwnerModuleId = "spawning.wanderinghorde",
            HotPathRelevant = false,
            RuntimeIndex = "RebirthSpawnCompositionService + AIWanderingHordeSpawner adapter",
            Evidence = "Blueprint v2",
            Notes = "Horde size/frequency/despawn/herd restrictions should be explicit policies."
        },
        new RebirthSpawnGroupDecl
        {
            GroupId = "spawn.bloodmoon",
            SourceXmlFamily = "AIDirectorBloodMoonParty / entitygroups",
            Surface = RebirthSpawnSurface.BloodMoon,
            PolicyStatus = RebirthSpawnPolicyStatus.RebirthOwned,
            OwnerModuleId = "spawning.bloodmoon",
            HotPathRelevant = true,
            RuntimeIndex = "RebirthSpawnCompositionService + AIDirectorBloodMoonParty adapter",
            Evidence = "P6/P6.5",
            Notes = "HordeNight/BloodMoon terminology migration and Dayuppy/TheDescent patch collision must be resolved."
        },
        new RebirthSpawnGroupDecl
        {
            GroupId = "spawn.events",
            SourceXmlFamily = "entityplayer local event spawns / entitygroups",
            Surface = RebirthSpawnSurface.EventSpawn,
            PolicyStatus = RebirthSpawnPolicyStatus.RebirthOwned,
            OwnerModuleId = "spawning.events",
            HotPathRelevant = false,
            RuntimeIndex = "future RebirthEventSpawnPolicyIndex",
            Evidence = "Blueprint v2/prior cave event restrictions",
            Notes = "Event spawns must be blocked separately for cave tunnels and cave POIs."
        },
        new RebirthSpawnGroupDecl
        {
            GroupId = "spawn.cave.tunnel",
            SourceXmlFamily = "TheDescent cave runtime / biome spawn",
            Surface = RebirthSpawnSurface.CaveTunnel,
            PolicyStatus = RebirthSpawnPolicyStatus.ManualReviewRequired,
            OwnerModuleId = "world.caves",
            HotPathRelevant = true,
            RuntimeIndex = "future RebirthCaveTunnelSpawnPolicy",
            Evidence = "P3/prior cave helper warning",
            Notes = "Cave tunnel helper must remain tunnel-only, not cave POI."
        },
        new RebirthSpawnGroupDecl
        {
            GroupId = "spawn.cave.poi",
            SourceXmlFamily = "POI tags TAG_POI_CAVE / TAG_POI_UNDERGROUND",
            Surface = RebirthSpawnSurface.CavePOI,
            PolicyStatus = RebirthSpawnPolicyStatus.ManualReviewRequired,
            OwnerModuleId = "world.caves",
            HotPathRelevant = false,
            RuntimeIndex = "future RebirthCavePoiSpawnPolicy",
            Evidence = "prior cave POI directive",
            Notes = "Cave POI check must be separate from cave tunnel helper."
        }
    };

    private static readonly RebirthSpawnRuleDecl[] s_rules = new[]
    {
        new RebirthSpawnRuleDecl
        {
            RuleId = "cave-tunnel-vs-poi-separation",
            OwnerModuleId = "world.caves",
            Rule = "Do not use the cave tunnel helper for cave POI checks. Use TAG_POI_CAVE and TAG_POI_UNDERGROUND separately.",
            Evidence = "prior explicit user directive / Blueprint v2",
            Notes = "This prevents breaking systems that require cave tunnel only."
        },
        new RebirthSpawnRuleDecl
        {
            RuleId = "event-spawns-no-cave",
            OwnerModuleId = "spawning.events",
            Rule = "Player entity spawn events must not trigger in cave tunnels or cave POIs.",
            Evidence = "prior user directive / Blueprint v2",
            Notes = "Requires two separate checks: tunnel runtime and POI tag."
        },
        new RebirthSpawnRuleDecl
        {
            RuleId = "bloodmoon-terminology",
            OwnerModuleId = "spawning.bloodmoon",
            Rule = "CustomHordeNight* options must be semantically mapped to 3.0 BloodMoon* native options.",
            Evidence = "P6",
            Notes = "Do not rely on token matching."
        },
        new RebirthSpawnRuleDecl
        {
            RuleId = "spawn-resolution-cache",
            OwnerModuleId = "spawning.groups",
            Rule = "Entity group resolution should use cached group graphs and policy contexts.",
            Evidence = "Blueprint v2",
            Notes = "Avoid repeated entitygroup traversal in spawn hot paths."
        }
    };

    public static string GetGroupsReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthSpawn] spawn group/policy runtime ledger.");
        sb.AppendLine("group | surface | status | owner | hot | index | evidence | notes");

        for (int i = 0; i < s_groups.Length; i++)
        {
            RebirthSpawnGroupDecl g = s_groups[i];
            if (!string.IsNullOrEmpty(f)
                && (g.GroupId == null || g.GroupId.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                && (g.OwnerModuleId == null || g.OwnerModuleId.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                && g.Surface.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            sb.Append(g.GroupId).Append(" | ")
              .Append(g.Surface).Append(" | ")
              .Append(g.PolicyStatus).Append(" | ")
              .Append(g.OwnerModuleId).Append(" | ")
              .Append(g.HotPathRelevant).Append(" | ")
              .Append(g.RuntimeIndex).Append(" | ")
              .Append(g.Evidence).Append(" | ")
              .AppendLine(g.Notes);
        }

        return sb.ToString();
    }

    public static string GetRulesReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthSpawn] spawn policy rules.");
        sb.AppendLine("rule | owner | evidence | text | notes");

        for (int i = 0; i < s_rules.Length; i++)
        {
            RebirthSpawnRuleDecl r = s_rules[i];
            sb.Append(r.RuleId).Append(" | ")
              .Append(r.OwnerModuleId).Append(" | ")
              .Append(r.Evidence).Append(" | ")
              .Append(r.Rule).Append(" | ")
              .AppendLine(r.Notes);
        }

        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthSpawn] Spawn group entries: " + s_groups.Length
            + "; rules: " + s_rules.Length
            + ". Runtime adapters are installed explicitly during REBIRTH bootstrap.";
    }
}
