#if DEBUG
using System.Collections.Generic;
using UnityEngine;

#nullable disable

// Always-compiled (NOT #if DEBUG) rate-limited tracing for Protect Supply
// Crates defender targeting and reload restoration.
//
// This exists specifically because the existing ConsoleCmdRebirthProtectCrateDebug
// command is compiled out of Release builds, which the handoff document
// identifies as a blocker to diagnosing the issue on a dedicated server
// running a Release build. This tracer has no such gate: it compiles into
// every build configuration and is controlled entirely by a runtime flag
// that defaults OFF, so it has zero cost and zero log noise unless a
// developer explicitly turns it on via `rbcratetrace on`.
//
// Every line is prefixed with the stable token requested in the handoff:
// "[REBIRTH ProtectCrate Trace]" so it can be grepped out of a combined
// server/client log pair.
//
// Method names deliberately avoid "Log" (singular) to avoid any ambiguity
// with the game's own static Log.Out/Log.Warning class inside this file.
public static class RebirthProtectCrateTrace
{
    private const string Prefix = "[REBIRTH ProtectCrate Trace] ";

    private static bool enabled;
    private static readonly Dictionary<string, float> nextAllowedByKey = new Dictionary<string, float>();

    public static bool Enabled
    {
        get { return enabled; }
    }

    public static void SetEnabled(bool value)
    {
        enabled = value;
        if (value)
        {
            nextAllowedByKey.Clear();
            Emit("tracing enabled role=" + DescribeRole());
        }
    }

    // Unconditional, unthrottled line. Use only for genuinely rare events
    // (encounter trigger or reload restoration) - never
    // for anything that can fire every tick/frame.
    public static void Emit(string message)
    {
        if (!enabled) return;
        Log.Out(Prefix + message);
    }

    // Throttled line safe to call from a per-tick/per-frame path
    // (MaintainDefenderObjective and target-policy rejection paths). `key` groups
    // related call sites so one noisy site cannot starve another; each key
    // is independently limited to at most one line per `minIntervalSeconds`.
    public static void EmitThrottled(string key, string message, float minIntervalSeconds)
    {
        if (!enabled) return;

        float now = Time.realtimeSinceStartup;
        float nextAllowed;
        if (nextAllowedByKey.TryGetValue(key, out nextAllowed) && now < nextAllowed) return;

        nextAllowedByKey[key] = now + Mathf.Max(0.05f, minIntervalSeconds);
        Log.Out(Prefix + message);
    }

    public static string DescribeRole()
    {
        bool dedicated = GameManager.IsDedicatedServer;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        bool remote = world != null && world.IsRemote();
        string worldTag = world != null ? world.GetHashCode().ToString() : "none";
        return "dedicated=" + dedicated + " remoteWorld=" + remote + " worldInstance=" + worldTag;
    }
}
#endif
