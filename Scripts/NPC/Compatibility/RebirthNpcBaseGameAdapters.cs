using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public interface IRebirthNpcBaseGameAdapter
{
    string AdapterId { get; }
    string Capability { get; }
    bool IsAvailable { get; }
    string UnavailableReason { get; }
}

public sealed class RebirthNpcBaseGameAdapterDescriptor : IRebirthNpcBaseGameAdapter
{
    public string AdapterId { get; internal set; }
    public string Capability { get; internal set; }
    public bool IsAvailable { get; internal set; }
    public string UnavailableReason { get; internal set; }
}

/// <summary>Stable adapter registry separating NPC domain code from the 3.1 APIs.</summary>
public static class RebirthNpcBaseGameAdapterRegistry
{
    private static readonly object Sync = new object();
    private static readonly SortedDictionary<string, RebirthNpcBaseGameAdapterDescriptor> Adapters = new SortedDictionary<string, RebirthNpcBaseGameAdapterDescriptor>(StringComparer.Ordinal);
    private static bool initialized;

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (initialized) return;
            initialized = true;
            RegisterNoLock("entity-position", "entity.position");
            RegisterNoLock("entity-lookup", "entity.lookup");
            RegisterNoLock("combat-damage", "combat.damage");
            RegisterNoLock("combat-death-events", "combat.death-events");
            RegisterNoLock("combat-status-effects", "combat.status-effects");
            RegisterNoLock("native-pathing", "navigation.native-pathing");
            RegisterNoLock("native-inventory-transfer", "inventory.native-transfer");
        }
        Refresh();
    }

    public static void Refresh()
    {
        RebirthNpcCompatibilityQualificationService.Audit(false);
        lock (Sync)
        {
            foreach (KeyValuePair<string, RebirthNpcBaseGameAdapterDescriptor> pair in Adapters)
            {
                bool available = RebirthNpcCompatibilityQualificationService.IsCapabilityAvailable(pair.Value.Capability);
                pair.Value.IsAvailable = available;
                pair.Value.UnavailableReason = available ? null : "3.1-contract-unavailable";
            }
        }
    }

    public static bool TryGet(string adapterId, out IRebirthNpcBaseGameAdapter adapter)
    {
        EnsureInitialized();
        lock (Sync)
        {
            RebirthNpcBaseGameAdapterDescriptor value;
            if (Adapters.TryGetValue(adapterId, out value)) { adapter = value; return true; }
            adapter = null; return false;
        }
    }

    public static string GetReport()
    {
        EnsureInitialized();
        lock (Sync)
        {
            StringBuilder b = new StringBuilder("[REBIRTH NPC Base Game Adapters] count=").Append(Adapters.Count);
            foreach (KeyValuePair<string, RebirthNpcBaseGameAdapterDescriptor> pair in Adapters)
                b.AppendLine().Append("  ").Append(pair.Key).Append(" capability=").Append(pair.Value.Capability)
                    .Append(" available=").Append(pair.Value.IsAvailable)
                    .Append(" reason=").Append(pair.Value.UnavailableReason ?? "-");
            return b.ToString();
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { initialized = false; Adapters.Clear(); }
    }

    private static void RegisterNoLock(string id, string capability)
    {
        Adapters[id] = new RebirthNpcBaseGameAdapterDescriptor { AdapterId = id, Capability = capability };
    }
}
