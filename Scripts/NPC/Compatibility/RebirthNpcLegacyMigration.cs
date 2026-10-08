using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public interface IRebirthNpcLegacyMigration
{
    string MigrationId { get; }
    int SourceMajorVersion { get; }
    bool CanMigrate(string legacyTypeId);
    bool TryTranslate(IDictionary<string, string> legacyValues, IDictionary<string, string> targetValues, out string error);
}

public sealed class RebirthNpcLegacyAliasMigration : IRebirthNpcLegacyMigration
{
    private readonly Dictionary<string, string> keyMap;
    public string MigrationId { get; private set; }
    public int SourceMajorVersion { get { return 2; } }

    public RebirthNpcLegacyAliasMigration(string id, Dictionary<string, string> map) { MigrationId = id; keyMap = map; }
    public bool CanMigrate(string legacyTypeId) { return !string.IsNullOrEmpty(legacyTypeId) && legacyTypeId.StartsWith("rebirth.npc.", StringComparison.OrdinalIgnoreCase); }

    public bool TryTranslate(IDictionary<string, string> legacyValues, IDictionary<string, string> targetValues, out string error)
    {
        error = null;
        if (legacyValues == null || targetValues == null) { error = "source-or-target-null"; return false; }
        foreach (KeyValuePair<string, string> pair in legacyValues)
        {
            string targetKey;
            if (!keyMap.TryGetValue(pair.Key, out targetKey)) targetKey = pair.Key;
            if (!targetValues.ContainsKey(targetKey)) targetValues[targetKey] = pair.Value;
        }
        targetValues["migration.source"] = "REBIRTH-2.6";
        targetValues["migration.id"] = MigrationId;
        return true;
    }
}

/// <summary>Explicit, allow-listed 2.6 behavioral migration registry.</summary>
public static class RebirthNpcLegacyMigrationRegistry
{
    private static readonly object Sync = new object();
    private static readonly List<IRebirthNpcLegacyMigration> Migrations = new List<IRebirthNpcLegacyMigration>();
    private static long attempted, completed, rejected;
    private static bool initialized;

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (initialized) return;
            initialized = true;
            Migrations.Add(new RebirthNpcLegacyAliasMigration("rebirth-2.6-npc-core-aliases-v1", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ownerId", "ownership.playerPersistentId" },
                { "entityName", "identity.displayName" },
                { "profession", "profession.definitionId" },
                { "currentOrder", "command.activeOrder" },
                { "homePosition", "settlement.homePosition" },
                { "faction", "organization.factionId" }
            }));
        }
    }

    public static bool TryMigrate(string legacyTypeId, IDictionary<string, string> source, IDictionary<string, string> target, out string error)
    {
        EnsureInitialized();
        lock (Sync)
        {
            attempted++;
            for (int i = 0; i < Migrations.Count; i++)
            {
                if (!Migrations[i].CanMigrate(legacyTypeId)) continue;
                bool ok = Migrations[i].TryTranslate(source, target, out error);
                if (ok) completed++; else rejected++;
                return ok;
            }
            rejected++; error = "unsupported-legacy-record-type"; return false;
        }
    }

    public static string GetReport()
    {
        EnsureInitialized();
        lock (Sync) return new StringBuilder("[REBIRTH NPC Legacy Migration] registered=").Append(Migrations.Count).Append(" attempted=").Append(attempted).Append(" completed=").Append(completed).Append(" rejected=").Append(rejected).ToString();
    }

    public static void ResetForWorldChange() { lock (Sync) { attempted = completed = rejected = 0; } }
}
