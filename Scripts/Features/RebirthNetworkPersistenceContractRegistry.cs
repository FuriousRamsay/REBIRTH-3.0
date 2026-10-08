using System;
using System.Text;

#nullable disable

public enum RebirthAuthorityOwner
{
    Unknown,
    ClientOnly,
    ServerOnly,
    ServerAuthoritativeClientDisplay,
    SharedReadOnly,
    LocalPredictionServerCorrection,
    ManualReviewRequired
}

public enum RebirthPersistenceMode
{
    Unknown,
    None,
    RuntimeOnly,
    ChunkState,
    PlayerData,
    WorldState,
    EntityState,
    ItemValueData,
    ServerEventState,
    ManualReviewRequired
}

public enum RebirthStateSyncMode
{
    Unknown,
    None,
    SnapshotOnLogin,
    EventDrivenDelta,
    ServerCorrection,
    RequestResponse,
    WorldLoadOnly,
    ManualReviewRequired
}

public enum RebirthStateContractRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public sealed class RebirthNetworkPersistenceContractDecl
{
    public string ContractId;
    public string DomainId;
    public RebirthAuthorityOwner AuthorityOwner;
    public RebirthPersistenceMode PersistenceMode;
    public RebirthStateSyncMode SyncMode;
    public RebirthStateContractRisk Risk;
    public string ServerOwns;
    public string ClientOwns;
    public string SaveLoadRule;
    public string TestGate;
    public string Notes;
}

/// <summary>
/// Read-only network authority and persistence contract registry.
/// This does not send packets, save data, or connect gameplay.
/// </summary>
public static class RebirthNetworkPersistenceContractRegistry
{
    private static readonly RebirthNetworkPersistenceContractDecl[] s_contracts = new[]
    {
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.scenario.purge",
            DomainId = "scenario.core/purge",
            AuthorityOwner = RebirthAuthorityOwner.ServerAuthoritativeClientDisplay,
            PersistenceMode = RebirthPersistenceMode.WorldState,
            SyncMode = RebirthStateSyncMode.EventDrivenDelta,
            Risk = RebirthStateContractRisk.Critical,
            ServerOwns = "purge progress truth, POI discovery truth, scenario eligibility",
            ClientOwns = "display snapshot only",
            SaveLoadRule = "server persists progress/discovery; client reconstructs display from server snapshot on login",
            TestGate = "SP/dedi/P2P purge progress, relog, server restart, marker sync",
            Notes = "Client never owns purge progress."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.map.markers",
            DomainId = "map.markers.reservations",
            AuthorityOwner = RebirthAuthorityOwner.ServerAuthoritativeClientDisplay,
            PersistenceMode = RebirthPersistenceMode.WorldState,
            SyncMode = RebirthStateSyncMode.SnapshotOnLogin,
            Risk = RebirthStateContractRisk.Critical,
            ServerOwns = "marker source state, ownership, destroy/cleanup truth",
            ClientOwns = "visible nav/compass objects",
            SaveLoadRule = "server persists marker source state; clients rebuild markers from snapshot on login/chunk relevance",
            TestGate = "spawn/destroy/login/relog/server restart/no flicker",
            Notes = "Markers are event-based and derived from source state."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.loot.reservations",
            DomainId = "loot.persistence",
            AuthorityOwner = RebirthAuthorityOwner.ServerOnly,
            PersistenceMode = RebirthPersistenceMode.WorldState,
            SyncMode = RebirthStateSyncMode.RequestResponse,
            Risk = RebirthStateContractRisk.Critical,
            ServerOwns = "reserved loot owner, container existence, access permission, cleanup",
            ClientOwns = "request/open UI only",
            SaveLoadRule = "server persists reservation data and validates every access",
            TestGate = "owner/non-owner access, relog, restart, destroy cleanup",
            Notes = "Never trust client for ownership."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.npc.companions",
            DomainId = "npc.companions",
            AuthorityOwner = RebirthAuthorityOwner.ServerAuthoritativeClientDisplay,
            PersistenceMode = RebirthPersistenceMode.EntityState,
            SyncMode = RebirthStateSyncMode.ServerCorrection,
            Risk = RebirthStateContractRisk.Critical,
            ServerOwns = "owner, follow state, respawn eligibility, target selection, equipment truth",
            ClientOwns = "display/audio/local UI interactions",
            SaveLoadRule = "server persists companion identity/owner/equipment; clients display synced state",
            TestGate = "SP/dedi/P2P, chunk reload, relog, respawn, silencer persistence",
            Notes = "Survivor NPC/entity role remains separate from survivor scenario."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.item.rolls",
            DomainId = "items.stats.randomization",
            AuthorityOwner = RebirthAuthorityOwner.ServerAuthoritativeClientDisplay,
            PersistenceMode = RebirthPersistenceMode.ItemValueData,
            SyncMode = RebirthStateSyncMode.ServerCorrection,
            Risk = RebirthStateContractRisk.Critical,
            ServerOwns = "rolled values, quality bounds, authoritative stat calculation",
            ClientOwns = "tooltip/display using synced values",
            SaveLoadRule = "rolled values persist on ItemValue data and must survive container/player transfers",
            TestGate = "quality range, relog/restart, transfer, tooltip/server match",
            Notes = "No client-side rerolling."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.paint",
            DomainId = "paint.render",
            AuthorityOwner = RebirthAuthorityOwner.ServerAuthoritativeClientDisplay,
            PersistenceMode = RebirthPersistenceMode.ChunkState,
            SyncMode = RebirthStateSyncMode.ServerCorrection,
            Risk = RebirthStateContractRisk.High,
            ServerOwns = "paint/tint applied state and chunk persistence",
            ClientOwns = "rendering synced chunk state",
            SaveLoadRule = "paint state persists with chunk/block data; client render cache invalidates on authoritative change",
            TestGate = "paint, remote paint, chunk unload/reload, server restart",
            Notes = "Render path consumes state; it does not own truth."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.traders",
            DomainId = "traders.behavior",
            AuthorityOwner = RebirthAuthorityOwner.ServerOnly,
            PersistenceMode = RebirthPersistenceMode.WorldState,
            SyncMode = RebirthStateSyncMode.ServerCorrection,
            Risk = RebirthStateContractRisk.Critical,
            ServerOwns = "trader existence/death/protection/spawner state",
            ClientOwns = "display only",
            SaveLoadRule = "trader death/protection state must not poison new world or new spawned trader",
            TestGate = "killall, death, new trader, new world, POI change, static spawner",
            Notes = "Keep trader state isolated from generic entity death."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.spawning",
            DomainId = "spawning.events",
            AuthorityOwner = RebirthAuthorityOwner.ServerOnly,
            PersistenceMode = RebirthPersistenceMode.ServerEventState,
            SyncMode = RebirthStateSyncMode.EventDrivenDelta,
            Risk = RebirthStateContractRisk.Critical,
            ServerOwns = "spawn execution, sleeper/event spawn truth, forced target state",
            ClientOwns = "entity display and local effects",
            SaveLoadRule = "server event spawn state persists only when feature requires it; otherwise runtime-only cleanup",
            TestGate = "sleepers, event spawns, caves, blood moon, P2P/dedi",
            Notes = "Scenarios provide policy; spawning owns execution."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.player.crawl",
            DomainId = "player.crawl",
            AuthorityOwner = RebirthAuthorityOwner.LocalPredictionServerCorrection,
            PersistenceMode = RebirthPersistenceMode.PlayerData,
            SyncMode = RebirthStateSyncMode.ServerCorrection,
            Risk = RebirthStateContractRisk.High,
            ServerOwns = "valid player collision/position state",
            ClientOwns = "local camera/transition feel subject to server correction",
            SaveLoadRule = "if player reloads inside crawl space, restore to valid state or safe exit according to contract",
            TestGate = "relog while crawling, vehicle/water, third-person, dedi correction",
            Notes = "Camera feel and collision truth are separate."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.workstations",
            DomainId = "workstations.crafting",
            AuthorityOwner = RebirthAuthorityOwner.ServerAuthoritativeClientDisplay,
            PersistenceMode = RebirthPersistenceMode.ChunkState,
            SyncMode = RebirthStateSyncMode.EventDrivenDelta,
            Risk = RebirthStateContractRisk.High,
            ServerOwns = "crafting queues, fuel, inventory, remote container authority",
            ClientOwns = "UI interaction and display",
            SaveLoadRule = "station queues and remote container state persist with authoritative station/container data",
            TestGate = "queue empty, fuel, chunk reload, remote broadcast, server parity",
            Notes = "Avoid client-side automation truth."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.zombie.ai",
            DomainId = "zombie.ai.special",
            AuthorityOwner = RebirthAuthorityOwner.ServerOnly,
            PersistenceMode = RebirthPersistenceMode.RuntimeOnly,
            SyncMode = RebirthStateSyncMode.ServerCorrection,
            Risk = RebirthStateContractRisk.Critical,
            ServerOwns = "target selection, attack decision, blocked/repositioning decision, damage application",
            ClientOwns = "animation/display synced from server state",
            SaveLoadRule = "AI transient decisions are runtime-only unless explicitly declared",
            TestGate = "vehicle, decoy, spider, block jump, horde night, dedi",
            Notes = "Do not let client visual state decide damage/target truth."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.player.survivor",
            DomainId = "player.survivor.character",
            AuthorityOwner = RebirthAuthorityOwner.ServerAuthoritativeClientDisplay,
            PersistenceMode = RebirthPersistenceMode.PlayerData,
            SyncMode = RebirthStateSyncMode.RequestResponse,
            Risk = RebirthStateContractRisk.Critical,
            ServerOwns = "stable player identity, world origin, Attributes, Skills, Knowledge, condition/support state, creation transaction result",
            ClientOwns = "selection intent and cached UI projection only; no authoritative resolved values",
            SaveLoadRule = "one isolated server world-character record per stable identity; owner receives compact revisioned projection",
            TestGate = "creation replay, stale definition hash, reconnect, SP/P2P/dedicated authority parity",
            Notes = "Metabolism physical state remains in RebirthMetabolismStateRepository and is not duplicated here."
        },
        new RebirthNetworkPersistenceContractDecl
        {
            ContractId = "net.state.player.survivorProfiles",
            DomainId = "player.survivor.localProfiles",
            AuthorityOwner = RebirthAuthorityOwner.ClientOnly,
            PersistenceMode = RebirthPersistenceMode.PlayerData,
            SyncMode = RebirthStateSyncMode.None,
            Risk = RebirthStateContractRisk.Medium,
            ServerOwns = "nothing from the local profile file; server validates submitted IDs against its own definitions",
            ClientOwns = "reusable local template files, labels and selection convenience data",
            SaveLoadRule = "user-local one-file-per-profile store outside world saves; never copied into server authority wholesale",
            TestGate = "profile corruption isolation and server rejection of forged resolved values",
            Notes = "A local profile is convenience input, never a world-character authority source."
        }
    };

    public static string GetSummaryReport()
    {
        int critical = 0;
        int blocked = 0;
        int serverAuthority = 0;
        int persistence = 0;

        for (int i = 0; i < s_contracts.Length; i++)
        {
            RebirthNetworkPersistenceContractDecl c = s_contracts[i];

            if (c.Risk == RebirthStateContractRisk.Critical)
                critical++;
            if (c.Risk == RebirthStateContractRisk.Blocked)
                blocked++;
            if (c.AuthorityOwner == RebirthAuthorityOwner.ServerOnly
                || c.AuthorityOwner == RebirthAuthorityOwner.ServerAuthoritativeClientDisplay)
                serverAuthority++;
            if (c.PersistenceMode != RebirthPersistenceMode.None
                && c.PersistenceMode != RebirthPersistenceMode.RuntimeOnly
                && c.PersistenceMode != RebirthPersistenceMode.Unknown)
                persistence++;
        }

        return "[RebirthNetPersist] Contracts: " + s_contracts.Length
            + "; critical: " + critical
            + "; blocked: " + blocked
            + "; server-authoritative/server-only: " + serverAuthority
            + "; persistent-state contracts: " + persistence
            + ". Registry is read-only.";
    }

    public static string GetContractReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(32768);
        sb.AppendLine("[RebirthNetPersist] network authority and persistence contracts.");
        sb.AppendLine("  Read-only. This does not send packets, save data, or connect gameplay.");
        sb.AppendLine("id | domain | authority | persistence | sync | risk | server owns | client owns | save/load | test gate | notes");

        bool any = false;
        for (int i = 0; i < s_contracts.Length; i++)
        {
            RebirthNetworkPersistenceContractDecl c = s_contracts[i];
            if (!Matches(c, f))
                continue;

            any = true;
            sb.Append(c.ContractId).Append(" | ")
              .Append(c.DomainId).Append(" | ")
              .Append(c.AuthorityOwner).Append(" | ")
              .Append(c.PersistenceMode).Append(" | ")
              .Append(c.SyncMode).Append(" | ")
              .Append(c.Risk).Append(" | ")
              .Append(c.ServerOwns).Append(" | ")
              .Append(c.ClientOwns).Append(" | ")
              .Append(c.SaveLoadRule).Append(" | ")
              .Append(c.TestGate).Append(" | ")
              .AppendLine(c.Notes);
        }

        if (!any)
            sb.AppendLine("No network/persistence contract matched the filter.");

        return sb.ToString();
    }

    public static string GetAuthorityReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthNetPersist] authority contracts.");
        sb.AppendLine("domain | authority | server owns | client owns | notes");

        for (int i = 0; i < s_contracts.Length; i++)
        {
            RebirthNetworkPersistenceContractDecl c = s_contracts[i];

            sb.Append(c.DomainId).Append(" | ")
              .Append(c.AuthorityOwner).Append(" | ")
              .Append(c.ServerOwns).Append(" | ")
              .Append(c.ClientOwns).Append(" | ")
              .AppendLine(c.Notes);
        }

        return sb.ToString();
    }

    public static string GetPersistenceReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthNetPersist] persistence contracts.");
        sb.AppendLine("domain | persistence | sync | save/load rule | test gate");

        for (int i = 0; i < s_contracts.Length; i++)
        {
            RebirthNetworkPersistenceContractDecl c = s_contracts[i];
            if (c.PersistenceMode == RebirthPersistenceMode.None
                || c.PersistenceMode == RebirthPersistenceMode.RuntimeOnly)
                continue;

            sb.Append(c.DomainId).Append(" | ")
              .Append(c.PersistenceMode).Append(" | ")
              .Append(c.SyncMode).Append(" | ")
              .Append(c.SaveLoadRule).Append(" | ")
              .AppendLine(c.TestGate);
        }

        return sb.ToString();
    }

    public static string GetBlockedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthNetPersist] blocked contracts.");
        sb.AppendLine("id | domain | reason | notes");

        for (int i = 0; i < s_contracts.Length; i++)
        {
            RebirthNetworkPersistenceContractDecl c = s_contracts[i];
            if (c.Risk != RebirthStateContractRisk.Blocked)
                continue;

            sb.Append(c.ContractId).Append(" | ")
              .Append(c.DomainId).Append(" | ")
              .Append(c.SaveLoadRule).Append(" | ")
              .AppendLine(c.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthNetPersist] safety rules:");
        sb.AppendLine("  1. Registry is read-only.");
        sb.AppendLine("  2. No packets are sent.");
        sb.AppendLine("  3. No data is saved or loaded.");
        sb.AppendLine("  4. No gameplay behavior is connected.");
        sb.AppendLine("  5. No Harmony patch install/uninstall occurs.");
        sb.AppendLine("  6. Server truth must not be client-owned.");
        sb.AppendLine("  7. Client display state must be derived from authoritative snapshots/events.");
        sb.AppendLine("  8. Survivor scenario contracts remain blocked until disambiguation.");
        return sb.ToString();
    }

    private static bool Matches(RebirthNetworkPersistenceContractDecl c, string filter)
    {
        if (c == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (c.ContractId != null && c.ContractId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.DomainId != null && c.DomainId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.ServerOwns != null && c.ServerOwns.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.ClientOwns != null && c.ClientOwns.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.SaveLoadRule != null && c.SaveLoadRule.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.TestGate != null && c.TestGate.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.AuthorityOwner.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.PersistenceMode.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.SyncMode.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
