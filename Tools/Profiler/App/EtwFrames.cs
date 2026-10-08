using System.Diagnostics;
using System.Text;

namespace RebirthProfiler;

/// <summary>
/// Frame times measured from outside the game. Windows' graphics stack emits an event for every Present call
/// (Microsoft-Windows-DXGI), and the gap between consecutive Present calls of the game process is the frame time.
/// This uses only built-in tools (logman to record, tracerpt to read) and needs no admin rights when the user is in
/// the "Performance Log Users" group. Nothing is loaded into the game.
/// </summary>
static class EtwFrames
{
    const string Session = "RebirthBenchDxgi";
    const string DxgiProvider = "{CA11C036-0102-4A2D-A6AD-F03CFED5D3C9}";   // Microsoft-Windows-DXGI

    static (int code, string text) Run(string exe, string args, int timeoutMs = 120000)
    {
        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using var p = Process.Start(psi)!;
        var text = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) text.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) text.AppendLine(e.Data); };
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return (-1, "timed out"); }
        p.WaitForExit();
        return (p.ExitCode, text.ToString());
    }

    /// <summary>Starts recording. Throws with a readable message if Windows refuses (e.g. missing permission).</summary>
    public static void Start(string etlPath)
    {
        Run("logman", $"stop {Session} -ets");                       // a leftover session from an earlier crashed run
        var (code, text) = Run("logman", $"start {Session} -p {DxgiProvider} 0xFFFFFFFFFFFFFFFF 0xFF -o \"{etlPath}\" -ets -bs 1024 -nb 32 128");
        if (code != 0)
            throw new InvalidOperationException("Could not start the frame-time capture (Windows event tracing). " +
                "Your account must be an administrator or a member of the 'Performance Log Users' group.\n" + text.Trim());
    }

    public static void Stop() => Run("logman", $"stop {Session} -ets");

    /// <summary>Converts the recording and returns the Present timestamps (100 ns FILETIME ticks) of the given process.</summary>
    public static List<long> ReadPresentTimes(string etlPath, int pid, out string diagnostics)
    {
        string csv = Path.ChangeExtension(etlPath, ".csv");
        string summary = Path.ChangeExtension(etlPath, ".summary.txt");
        var (code, text) = Run("tracerpt", $"\"{etlPath}\" -o \"{csv}\" -of CSV -summary \"{summary}\" -y");
        if (!File.Exists(csv)) throw new InvalidOperationException("tracerpt produced no output: " + text.Trim());

        var times = new List<long>();
        int total = 0, forPid = 0;
        using var reader = new StreamReader(csv);
        string header = null;
        int iPid = -1, iTime = -1, iId = -1, iName = -1, cols = 0;
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (header == null)
            {
                var h = line.Split(',');
                for (int i = 0; i < h.Length; i++)
                {
                    var n = h[i].Trim().Trim('"');
                    if (n.Equals("PID", StringComparison.OrdinalIgnoreCase) || n.Equals("Process ID", StringComparison.OrdinalIgnoreCase)) iPid = i;
                    else if (n.StartsWith("Clock-Time", StringComparison.OrdinalIgnoreCase)) iTime = i;
                    else if (n.Equals("Event ID", StringComparison.OrdinalIgnoreCase)) iId = i;
                    else if (n.Equals("Event Name", StringComparison.OrdinalIgnoreCase)) iName = i;
                }
                if (iPid < 0 || iTime < 0) { diagnostics = "unexpected CSV header: " + line; return times; }
                header = line; cols = h.Length;
                continue;
            }
            var f = line.Split(',', cols);
            if (f.Length <= Math.Max(iPid, iTime)) continue;
            total++;
            if (!TryParseId(f[iPid], out int rowPid) || rowPid != pid) continue;
            forPid++;
            bool presentStart = iId >= 0 && f.Length > iId && f[iId].Trim().Trim('"') == "42";
            if (!presentStart && iName >= 0 && f.Length > iName)
            {
                var name = f[iName];
                presentStart = name.Contains("Present", StringComparison.OrdinalIgnoreCase) && name.Contains("Start", StringComparison.OrdinalIgnoreCase);
            }
            if (presentStart && long.TryParse(f[iTime].Trim().Trim('"'), out long t)) times.Add(t);
        }
        diagnostics = $"{total} events in the trace, {forPid} from the game, {times.Count} Present calls";
        return times;
    }

    static bool TryParseId(string s, out int v)
    {
        s = s.Trim().Trim('"');
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return int.TryParse(s.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out v);
        return int.TryParse(s, out v);
    }

    /// <summary>Turns Present timestamps into the frame-time histogram, per-second series and worst frames.</summary>
    public static GameBench ToGameBench(List<long> presentTicks)
    {
        var g = new GameBench { Hist = new long[2001] };
        if (presentTicks.Count < 2) return g;
        presentTicks.Sort();
        // one frame can present more than once (several swap chains / overlay planes): treat calls within 1 ms as one
        var merged = new List<long> { presentTicks[0] };
        foreach (var t in presentTicks) if (t - merged[^1] > 10000) merged.Add(t);
        presentTicks = merged;
        if (presentTicks.Count < 2) return g;
        long t0 = presentTicks[0];
        var perSecond = new Dictionary<int, GameSecond>();
        var worst = new List<(double t, double ms)>();
        for (int i = 1; i < presentTicks.Count; i++)
        {
            double ms = (presentTicks[i] - presentTicks[i - 1]) / 10000.0;
            double at = (presentTicks[i] - t0) / 1e7;
            int bin = Math.Clamp((int)(ms * 10), 0, 2000);
            g.Hist[bin]++;
            g.Frames++;
            int sec = (int)at;
            if (!perSecond.TryGetValue(sec, out var s)) perSecond[sec] = s = new GameSecond { T = sec };
            s.F++; s.Sum += ms; if (ms > s.Max) s.Max = ms;
            worst.Add((at, ms));
        }
        // the first and last buckets are partial seconds and would show as bogus FPS dips
        var ordered = perSecond.OrderBy(k => k.Key).Select(k => k.Value).ToList();
        if (ordered.Count > 3) { ordered.RemoveAt(ordered.Count - 1); ordered.RemoveAt(0); }
        g.Series = ordered;
        g.Worst = worst.OrderByDescending(w => w.ms).Take(20).ToList();
        return g;
    }
}
