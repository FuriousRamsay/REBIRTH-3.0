#if PURGE_QUALIFICATION
using System;
using Newtonsoft.Json.Linq;

// Bounded observation of completed native feedback calls in disposable Bridge saves.
// Does not dispatch feedback, inspect scene objects, or retain a World/player strongly.
internal static class RebirthPurgeFeedbackTrace
{
    private static WeakReference<World> owner;
    private static readonly JArray events = new JArray();
    private static long sequence;
    internal static bool Allowed => RebirthGameBridge.IsRunning &&
        ((GamePrefs.GetString(EnumGamePrefs.GameName) ?? "").StartsWith("CodexTest", StringComparison.Ordinal)||
         GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexPurgeRemote20261102");

    internal static void Record(string kind, string prefab, string biome, string value)
    {
        if (!Allowed) return;
        try
        {
            var world = GameManager.Instance?.World;
            if (world == null) return;
            if (owner == null || !owner.TryGetTarget(out var prior) || !ReferenceEquals(prior, world))
            { owner = new WeakReference<World>(world); events.Clear(); sequence = 0; }
            if (events.Count == 128) events.RemoveAt(0);
            events.Add(new JObject {
                ["sequence"] = ++sequence, ["time"] = UnityEngine.Time.realtimeSinceStartup,
                ["kind"] = kind, ["prefab"] = prefab, ["biome"] = biome,
                ["value"] = value == null || value.Length <= 4096 ? value : value.Substring(0, 4096),
                ["valueTruncated"] = value != null && value.Length > 4096
            });
        }
        catch { /* Diagnostics must never interrupt native feedback delivery. */ }
    }

    internal static JObject Observe()
    {
        var world = GameManager.Instance?.World;
        bool current = world != null && owner != null && owner.TryGetTarget(out var prior) && ReferenceEquals(prior, world);
        return new JObject {
            ["observationOnly"] = true, ["enabled"] = Allowed, ["capacity"] = 128,
            ["total"] = current ? sequence : 0, ["events"] = current ? events.DeepClone() : new JArray(),
            ["scope"] = "Completed tooltip/audio dispatch calls, not human listening or visible duration."
        };
    }
}
#endif
