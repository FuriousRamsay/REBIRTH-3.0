using System.Collections.Generic;
using Newtonsoft.Json.Linq;

#nullable disable

/// <summary>
/// In-memory ring buffer of game log lines with monotonically increasing sequence numbers, so bridge
/// clients can ask "what was logged since X" (e.g. errors caused by a console command or a test step).
/// Only installed when the bridge is enabled.
/// </summary>
public static class RebirthGameBridgeLog
{
    private const int Capacity = 20000;

    public sealed class Entry
    {
        public long Seq;
        public DateTime Time;
        public LogType Type;
        public string Message;
        public string Trace;
    }

    private static readonly object gate = new object();
    private static readonly Entry[] ring = new Entry[Capacity];
    private static long lastSeq;
    private static bool installed;

    public static long LastSeq { get { lock (gate) return lastSeq; } }

    public static void Install()
    {
        if (installed) return;
        installed = true;
        Log.LogCallbacks += OnLog;
    }

    private static void OnLog(string msg, string trace, LogType type)
    {
        lock (gate)
        {
            lastSeq++;
            ring[lastSeq % Capacity] = new Entry
            {
                Seq = lastSeq,
                Time = DateTime.Now,
                Type = type,
                Message = msg,
                Trace = (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) ? trace : null
            };
        }
    }

    /// <summary>Severity rank: log=0, warning=1, error/assert/exception=2.</summary>
    public static int Rank(LogType t)
    {
        switch (t)
        {
            case LogType.Warning: return 1;
            case LogType.Error:
            case LogType.Assert:
            case LogType.Exception: return 2;
            default: return 0;
        }
    }

    public static int ParseMinRank(string level)
    {
        if (string.IsNullOrEmpty(level)) return 0;
        switch (level.ToLowerInvariant())
        {
            case "warn":
            case "warning": return 1;
            case "err":
            case "error":
            case "exception": return 2;
            default: return 0;
        }
    }

    /// <summary>Entries with Seq &gt; sinceSeq and at least minRank, newest last, capped to the last `limit`.</summary>
    public static List<Entry> Since(long sinceSeq, int minRank, int limit, string contains = null)
    {
        var result = new List<Entry>();
        lock (gate)
        {
            long first = Math.Max(sinceSeq + 1, lastSeq - Capacity + 1);
            for (long s = Math.Max(first, 1); s <= lastSeq; s++)
            {
                Entry e = ring[s % Capacity];
                if (e == null || e.Seq != s) continue;
                if (Rank(e.Type) < minRank) continue;
                if (contains != null && (e.Message == null || e.Message.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                result.Add(e);
            }
        }
        if (limit > 0 && result.Count > limit) result.RemoveRange(0, result.Count - limit);
        return result;
    }

    public static JArray ToJson(List<Entry> entries)
    {
        var arr = new JArray();
        foreach (Entry e in entries)
        {
            var o = new JObject
            {
                ["seq"] = e.Seq,
                ["time"] = e.Time.ToString("HH:mm:ss.fff"),
                ["level"] = e.Type.ToString().ToLowerInvariant(),
                ["msg"] = e.Message
            };
            if (!string.IsNullOrEmpty(e.Trace)) o["trace"] = e.Trace;
            arr.Add(o);
        }
        return arr;
    }
}
