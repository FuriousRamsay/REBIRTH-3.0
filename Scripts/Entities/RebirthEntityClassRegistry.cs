using System;
using System.Text;

#nullable disable

public enum RebirthEntityFamily
{
    Unknown,
    Player,
    Zombie,
    Animal,
    NPC,
    Bandit,
    Vehicle,
    Drone,
    LootContainer,
    Trader,
    Projectile,
    EffectOnly
}

public enum RebirthEntityRuntimePolicy
{
    Unknown,
    VanillaOnly,
    RebirthManaged,
    ExternalManaged,
    CompatibilityOnly,
    ManualReviewRequired
}

public sealed class RebirthEntityClassDecl
{
    public string ClassId;
    public string NamePattern;
    public RebirthEntityFamily Family;
    public RebirthEntityRuntimePolicy RuntimePolicy;
    public string OwnerModuleId;
    public bool HotPathRelevant;
    public string PrimaryIndex;
    public string Evidence;
    public string Notes;
}

/// <summary>
/// Read-only entity classification seed registry.
/// This does not inspect EntityClass XML or mutate entity behavior.
/// </summary>
public static class RebirthEntityClassRegistry
{
    private static readonly RebirthEntityClassDecl[] s_classes = new[]
    {
        new RebirthEntityClassDecl
        {
            ClassId = "entity.zombie.base",
            NamePattern = "zombie* / EntityZombieSDX",
            Family = RebirthEntityFamily.Zombie,
            RuntimePolicy = RebirthEntityRuntimePolicy.RebirthManaged,
            OwnerModuleId = "zombies.resident",
            HotPathRelevant = true,
            PrimaryIndex = "future RebirthZombieClassIndex",
            Evidence = "P1/P2/Blueprint v2",
            Notes = "Zombie resident systems need active sets; do not scan all entities each frame."
        },
        new RebirthEntityClassDecl
        {
            ClassId = "entity.zombie.ai",
            NamePattern = "zombie* AI packages / EAI*",
            Family = RebirthEntityFamily.Zombie,
            RuntimePolicy = RebirthEntityRuntimePolicy.RebirthManaged,
            OwnerModuleId = "zombie.ai",
            HotPathRelevant = true,
            PrimaryIndex = "future RebirthZombieAiIndex",
            Evidence = "Blueprint v2/P2/P2.5",
            Notes = "ZombieAI depends on Pathing. Pathing must not depend on ZombieAI."
        },
        new RebirthEntityClassDecl
        {
            ClassId = "entity.npc.rebirth",
            NamePattern = "EntityNPCRebirth / companions / survivors / bandits",
            Family = RebirthEntityFamily.NPC,
            RuntimePolicy = RebirthEntityRuntimePolicy.RebirthManaged,
            OwnerModuleId = "npc.companions",
            HotPathRelevant = true,
            PrimaryIndex = "future RebirthNpcClassIndex",
            Evidence = "Blueprint v2/prior NPC fixes",
            Notes = "NPC systems need server-authoritative target/follow/inventory policies."
        },
        new RebirthEntityClassDecl
        {
            ClassId = "entity.animal",
            NamePattern = "animal* / animalZombie*",
            Family = RebirthEntityFamily.Animal,
            RuntimePolicy = RebirthEntityRuntimePolicy.RebirthManaged,
            OwnerModuleId = "animals",
            HotPathRelevant = true,
            PrimaryIndex = "future RebirthAnimalClassIndex",
            Evidence = "Blueprint v2/prior random-name and pathing issues",
            Notes = "Animal zombie dog/bear random names and pathing must be handled through indexed class traits."
        },
        new RebirthEntityClassDecl
        {
            ClassId = "entity.vehicle",
            NamePattern = "EntityVehicle / vehicle entities",
            Family = RebirthEntityFamily.Vehicle,
            RuntimePolicy = RebirthEntityRuntimePolicy.ManualReviewRequired,
            OwnerModuleId = "vehicles",
            HotPathRelevant = true,
            PrimaryIndex = "future RebirthVehicleEntityIndex",
            Evidence = "Blueprint v2/prior vehicle/camera/VML findings",
            Notes = "Vehicle camera/mounted state must fast-exit player crawl/movement logic."
        },
        new RebirthEntityClassDecl
        {
            ClassId = "entity.drone",
            NamePattern = "EntityDrone / drone deployables",
            Family = RebirthEntityFamily.Drone,
            RuntimePolicy = RebirthEntityRuntimePolicy.RebirthManaged,
            OwnerModuleId = "deployables.drones",
            HotPathRelevant = true,
            PrimaryIndex = "future RebirthDroneIndex",
            Evidence = "P4/V8",
            Notes = "Drone follow-lock must use drone-scoped hooks or active sets; never Entity.OnUpdatePosition globally."
        },
        new RebirthEntityClassDecl
        {
            ClassId = "entity.trader",
            NamePattern = "npcTrader* / protected traders",
            Family = RebirthEntityFamily.Trader,
            RuntimePolicy = RebirthEntityRuntimePolicy.RebirthManaged,
            OwnerModuleId = "traders",
            HotPathRelevant = true,
            PrimaryIndex = "future RebirthTraderEntityIndex",
            Evidence = "Blueprint v2/prior trader attack/death issues",
            Notes = "Trader protection/death/spawn behavior must be explicit and not depend on broad killall/entity rules."
        },
        new RebirthEntityClassDecl
        {
            ClassId = "entity.loot.reserved",
            NamePattern = "reserved loot / boss loot / supply crates",
            Family = RebirthEntityFamily.LootContainer,
            RuntimePolicy = RebirthEntityRuntimePolicy.RebirthManaged,
            OwnerModuleId = "loot.reservation",
            HotPathRelevant = false,
            PrimaryIndex = "future RebirthReservedLootIndex",
            Evidence = "Blueprint v2/prior marker/reservation fixes",
            Notes = "Reserved loot and nav markers should be event-driven and persisted by owner/position."
        }
    };

    public static RebirthEntityClassDecl[] GetSnapshot()
    {
        RebirthEntityClassDecl[] copy = new RebirthEntityClassDecl[s_classes.Length];
        Array.Copy(s_classes, copy, copy.Length);
        return copy;
    }

    public static string GetReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthEntities] read-only entity classification seed ledger.");
        sb.AppendLine("class | pattern | family | policy | owner | hot | index | evidence | notes");

        for (int i = 0; i < s_classes.Length; i++)
        {
            RebirthEntityClassDecl e = s_classes[i];
            if (!string.IsNullOrEmpty(f)
                && (e.ClassId == null || e.ClassId.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                && (e.NamePattern == null || e.NamePattern.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                && (e.OwnerModuleId == null || e.OwnerModuleId.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0))
                continue;

            sb.Append(e.ClassId).Append(" | ")
              .Append(e.NamePattern).Append(" | ")
              .Append(e.Family).Append(" | ")
              .Append(e.RuntimePolicy).Append(" | ")
              .Append(e.OwnerModuleId).Append(" | ")
              .Append(e.HotPathRelevant).Append(" | ")
              .Append(e.PrimaryIndex).Append(" | ")
              .Append(e.Evidence).Append(" | ")
              .AppendLine(e.Notes);
        }

        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthEntities] Seed entity classification entries: " + s_classes.Length
            + ". No XML parsing and no entity behavior changes occur in this phase.";
    }
}
