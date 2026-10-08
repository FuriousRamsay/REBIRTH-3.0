using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#nullable disable

/// <summary>
/// The vanilla SteelSeries GameSense integration (only active when SteelSeries Engine is installed) posts an HTTP message per event. Some events are
/// raised every frame with no change check: <c>UpdateEventHealth</c> always sends the health percentage, and <c>UpdateEventAmmo</c> /
/// <c>UpdateEventDurability</c> send 0 every frame when nothing suitable is held. That is roughly three web requests per frame, a worker thread busy
/// with HTTP, and about 6-7 MB/s of allocation (profiled: ~82% of all garbage in idle play, one garbage collection per minute).
///
/// This drops a message whose value is identical to the last one sent for the same event, and re-sends it after a few seconds anyway so the
/// engine's state can never stay stale. Lighting behaviour is unchanged: the engine only ever reacts to values that changed.
/// Launching with <c>-nogamesense</c> still switches the integration off completely.
/// </summary>
public static class RebirthGameSenseDedupePatch
{
    private const float RefreshSeconds = 5f;
    private struct SentEvent
    {
        public readonly string Value;
        public readonly float Time;
        public SentEvent(string value, float time) { Value = value; Time = time; }
    }
    private static readonly Dictionary<string, SentEvent> LastSent = new Dictionary<string, SentEvent>();
    private static readonly object Sync = new object();
    private static long sent, skipped;

    public static long SentMessages { get { return sent; } }
    public static long SkippedMessages { get { return skipped; } }

    /// <summary>Patches GSClient.SendEvent(string, int). Returns a one-line report; does nothing (and never throws) when the game has no such type.</summary>
    public static string Install(Harmony harmony)
    {
        try
        {
            Type client = AccessTools.TypeByName("SteelSeries.GameSense.GSClient");
            if (client == null)
                return "[REBIRTH Perf] GameSense dedupe: GSClient type not found, skipped";
            int patched = 0;
            var seen = new List<string>();
            foreach (MethodInfo m in client.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (m.Name != "SendEvent")
                    continue;
                ParameterInfo[] ps = m.GetParameters();
                var sig = new List<string>();
                foreach (ParameterInfo pi in ps) sig.Add(pi.ParameterType.Name);
                seen.Add("(" + string.Join(", ", sig.ToArray()) + ")");
                // An event name and one simple value, optionally with a context frame (only plain calls are ever dropped); anything else is left alone.
                if (ps.Length < 2 || ps.Length > 3 || ps[0].ParameterType != typeof(string) || !(ps[1].ParameterType.IsPrimitive || ps[1].ParameterType == typeof(string)))
                    continue;
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(RebirthGameSenseDedupePatch).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.Public)));
                patched++;
            }
            return "[REBIRTH Perf] GameSense dedupe: patched " + patched + " of the SendEvent overloads " + string.Join(" ", seen.ToArray());
        }
        catch (Exception ex)
        {
            return "[REBIRTH Perf] GameSense dedupe failed: " + ex.Message;
        }
    }

    public static bool Prefix(object[] __args)
    {
        if (__args == null || __args.Length < 2)
            return true;
        string name = __args[0] as string;
        if (name == null || __args[1] == null)
            return true;
        if (__args.Length > 2 && __args[2] != null)
            return true;   // a message that carries a context frame always goes out
        string value = __args[1].ToString();
        float now = Time.unscaledTime;
        lock (Sync)
        {
            SentEvent last;
            if (LastSent.TryGetValue(name, out last) && last.Value == value && now - last.Time < RefreshSeconds)
            {
                skipped++;
                return false;
            }
            LastSent[name] = new SentEvent(value, now);
            sent++;
        }
        return true;
    }
}
