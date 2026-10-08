using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RebirthProfiler;

// ---- data ------------------------------------------------------------------------------------------------------------

sealed class GameSecond { public int T, F; public double Sum, Max; }
sealed class GameBench
{
    public long Frames, Missed;
    public long[] Hist = Array.Empty<long>();          // 0.1 ms bins
    public List<GameSecond> Series = new();
    public List<(double t, double ms)> Worst = new();
}
sealed class OsSample { public double T, ProcCores, MainThreadPct, WsMB, PrivMB, GpuVramMB, Gpu3dPct, GpuAnyPct; public int Threads; }

sealed class BenchResult
{
    public string Label { get; set; } = "";
    public string Created { get; set; } = "";
    public string Machine { get; set; } = "";
    public string Notes { get; set; } = "";
    public int DurationSec { get; set; }
    public GameBench Game { get; set; } = new();
    public List<OsSample> Os { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore] public string Path { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public Dictionary<string, double> Summary => summary ??= BenchMath.Summarize(this);
    Dictionary<string, double> summary;

    static readonly JsonSerializerOptions Opt = new() { WriteIndented = false, IncludeFields = true };
    public void Save(string path) { File.WriteAllText(path, JsonSerializer.Serialize(this, Opt)); Path = path; }
    public static BenchResult Load(string path)
    {
        var r = JsonSerializer.Deserialize<BenchResult>(File.ReadAllText(path), Opt) ?? throw new InvalidDataException("empty file");
        r.Path = path; return r;
    }

}

// ---- statistics ------------------------------------------------------------------------------------------------------

static class BenchMath
{
    public sealed record Metric(string Key, string Name, string Unit, bool HigherIsBetter, string Group);

    public static readonly Metric[] Metrics =
    {
        new("avgFps", "Average FPS", "fps", true, "Frame rate"),
        new("low1", "1% low FPS", "fps", true, "Frame rate"),
        new("low01", "0.1% low FPS", "fps", true, "Frame rate"),
        new("p50", "Frame time, median", "ms", false, "Frame time"),
        new("p95", "Frame time, 95th percentile", "ms", false, "Frame time"),
        new("p99", "Frame time, 99th percentile", "ms", false, "Frame time"),
        new("p999", "Frame time, 99.9th percentile", "ms", false, "Frame time"),
        new("worst", "Worst single frame", "ms", false, "Frame time"),
        new("hitch33", "Frames slower than 33 ms (<30 fps), per minute", "/min", false, "Hitches"),
        new("hitch100", "Frames slower than 100 ms, per minute", "/min", false, "Hitches"),
        new("cpuCores", "Process CPU, average", "cores", false, "CPU"),
        new("mainPct", "Main thread CPU, average", "% of a core", false, "CPU"),
        new("mainPeak", "Main thread CPU, peak", "% of a core", false, "CPU"),
        new("wsAvg", "Working set, average", "MB", false, "System memory"),
        new("wsPeak", "Working set, peak", "MB", false, "System memory"),
        new("privAvg", "Private memory, average", "MB", false, "System memory"),
        new("vramAvg", "GPU memory (VRAM), average", "MB", false, "GPU"),
        new("vramPeak", "GPU memory (VRAM), peak", "MB", false, "GPU"),
        new("gpu3dAvg", "GPU 3D utilisation, average", "%", false, "GPU"),
        new("gpu3dP95", "GPU 3D utilisation, 95th percentile", "%", false, "GPU"),
    };

    static double BinMs(int bin) => (bin + 0.5) * 0.1;

    static double Percentile(long[] hist, long total, double p)
    {
        if (total == 0) return 0;
        long target = (long)Math.Ceiling(total * p), acc = 0;
        for (int i = 0; i < hist.Length; i++) { acc += hist[i]; if (acc >= target) return BinMs(i); }
        return BinMs(hist.Length - 1);
    }

    /// <summary>Average FPS of the slowest <paramref name="fraction"/> of frames (the usual "1% low" definition).</summary>
    static double LowFps(long[] hist, long total, double fraction)
    {
        if (total == 0) return 0;
        long want = Math.Max(1, (long)Math.Ceiling(total * fraction)), taken = 0; double sumMs = 0;
        for (int i = hist.Length - 1; i >= 0 && taken < want; i--)
        {
            long n = Math.Min(hist[i], want - taken);
            taken += n; sumMs += n * BinMs(i);
        }
        return taken == 0 || sumMs <= 0 ? 0 : 1000.0 * taken / sumMs;
    }

    static double P(IEnumerable<double> v, double p)
    {
        var a = v.OrderBy(x => x).ToArray();
        return a.Length == 0 ? 0 : a[Math.Min(a.Length - 1, (int)Math.Ceiling(a.Length * p) - 1 < 0 ? 0 : (int)Math.Ceiling(a.Length * p) - 1)];
    }

    public static Dictionary<string, double> Summarize(BenchResult r)
    {
        var d = new Dictionary<string, double>();
        var g = r.Game;
        long n = g.Hist.Sum();
        double sumMs = g.Series.Sum(s => s.Sum);
        long framesInSeries = g.Series.Sum(s => (long)s.F);
        double minutes = Math.Max(g.Series.Count, 1) / 60.0;
        d["avgFps"] = sumMs > 0 ? 1000.0 * framesInSeries / sumMs : 0;
        d["low1"] = LowFps(g.Hist, n, 0.01);
        d["low01"] = LowFps(g.Hist, n, 0.001);
        d["p50"] = Percentile(g.Hist, n, 0.50);
        d["p95"] = Percentile(g.Hist, n, 0.95);
        d["p99"] = Percentile(g.Hist, n, 0.99);
        d["p999"] = Percentile(g.Hist, n, 0.999);
        d["worst"] = g.Worst.Count > 0 ? g.Worst.Max(w => w.ms) : 0;
        long h33 = 0, h100 = 0;
        for (int i = 0; i < g.Hist.Length; i++) { if (i >= 333) h33 += g.Hist[i]; if (i >= 1000) h100 += g.Hist[i]; }
        d["hitch33"] = h33 / minutes; d["hitch100"] = h100 / minutes;
        var os = r.Os;
        if (os.Count > 0)
        {
            d["cpuCores"] = os.Average(s => s.ProcCores);
            d["mainPct"] = os.Average(s => s.MainThreadPct);
            d["mainPeak"] = os.Max(s => s.MainThreadPct);
            d["wsAvg"] = os.Average(s => s.WsMB); d["wsPeak"] = os.Max(s => s.WsMB);
            d["privAvg"] = os.Average(s => s.PrivMB);
            d["vramAvg"] = os.Average(s => s.GpuVramMB); d["vramPeak"] = os.Max(s => s.GpuVramMB);
            d["gpu3dAvg"] = os.Average(s => s.Gpu3dPct);
            d["gpu3dP95"] = P(os.Select(s => s.Gpu3dPct), 0.95);
        }
        return d;
    }
}

// ---- Windows performance counters (GPU) ----------------------------------------------------------------------------------

sealed class Pdh : IDisposable
{
    const uint PDH_FMT_DOUBLE = 0x200, PDH_MORE_DATA = 0x800007D2;
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhOpenQueryW(string src, IntPtr user, out IntPtr q);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhAddEnglishCounterW(IntPtr q, string path, IntPtr user, out IntPtr c);
    [DllImport("pdh.dll")] static extern uint PdhCollectQueryData(IntPtr q);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhGetFormattedCounterArrayW(IntPtr c, uint fmt, ref uint size, ref uint count, IntPtr buf);
    [DllImport("pdh.dll")] static extern uint PdhCloseQuery(IntPtr q);

    IntPtr query, engine, procMem;
    public bool Available { get; private set; }

    public Pdh(int pid)
    {
        try
        {
            if (PdhOpenQueryW(null, IntPtr.Zero, out query) != 0) return;
            bool a = PdhAddEnglishCounterW(query, $"\\GPU Engine(pid_{pid}_*)\\Utilization Percentage", IntPtr.Zero, out engine) == 0;
            bool b = PdhAddEnglishCounterW(query, $"\\GPU Process Memory(pid_{pid}_*)\\Dedicated Usage", IntPtr.Zero, out procMem) == 0;
            Available = a || b;
            PdhCollectQueryData(query);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    static List<(string name, double value)> Read(IntPtr counter)
    {
        var list = new List<(string, double)>();
        if (counter == IntPtr.Zero) return list;
        uint size = 0, count = 0;
        uint rc = PdhGetFormattedCounterArrayW(counter, PDH_FMT_DOUBLE, ref size, ref count, IntPtr.Zero);
        if (rc != PDH_MORE_DATA || size == 0) return list;
        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(counter, PDH_FMT_DOUBLE, ref size, ref count, buf) != 0) return list;
            // PDH_FMT_COUNTERVALUE_ITEM_W: LPWSTR name; DWORD status (+pad); double value  => 24 bytes on x64
            for (int i = 0; i < count; i++)
            {
                IntPtr item = buf + i * 24;
                string name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(item)) ?? "";
                uint status = (uint)Marshal.ReadInt32(item, 8);
                double v = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item, 16));
                if (status == 0 || status == 1) list.Add((name, v));
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
        return list;
    }

    /// <returns>VRAM used by the process (MB), 3D-engine utilisation and busiest-engine utilisation (%).</returns>
    public (double vramMB, double gpu3d, double gpuAny) Sample()
    {
        if (!Available) return (0, 0, 0);
        try
        {
            PdhCollectQueryData(query);
            double vram = Read(procMem).Sum(x => x.value) / 1048576.0;
            var engines = Read(engine);
            double e3d = engines.Where(x => x.name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)).Sum(x => x.value);
            double any = engines.GroupBy(x => { int i = x.name.IndexOf("engtype_", StringComparison.OrdinalIgnoreCase); return i >= 0 ? x.name[i..] : x.name; })
                                .Select(g => g.Sum(x => x.value)).DefaultIfEmpty(0).Max();
            return (vram, Math.Min(e3d, 100), Math.Min(any, 100));
        }
        catch { return (0, 0, 0); }
    }

    public void Dispose() { if (query != IntPtr.Zero) { PdhCloseQuery(query); query = IntPtr.Zero; } }
}

// ---- the repeatable benchmark run ----------------------------------------------------------------------------------------

sealed class BenchOptions
{
    public string Label = "run";
    public int WarmupSec = 30, DurationSec = 120;
    public bool CloseGame = true;
    public bool FreshSave = true;      // restore the test save from its baseline before launching, so every run starts from the same world
    public string GameArgs;
}

static class BenchmarkRunner
{
    [DllImport("kernel32.dll")] static extern IntPtr OpenThread(uint access, bool inherit, uint tid);
    [DllImport("kernel32.dll")] static extern bool GetThreadTimes(IntPtr t, out long create, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    static string OutDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "out"));
    static string SessionFile => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "GameBridge", "out", "session.json"));

    static async Task<(HttpClient http, int port)> WaitForBridge(int pid, IProgress<string> log, CancellationToken ct)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var deadline = DateTime.UtcNow.AddMinutes(8);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (!Process.GetProcesses().Any(p => p.Id == pid)) throw new InvalidOperationException("the game exited during loading");
            try
            {
                if (File.Exists(SessionFile))
                {
                    using var s = JsonDocument.Parse(File.ReadAllText(SessionFile));
                    int port = s.RootElement.GetProperty("port").GetInt32();
                    string token = s.RootElement.GetProperty("token").GetString();
                    http.DefaultRequestHeaders.Remove("X-Bridge-Token");
                    http.DefaultRequestHeaders.Add("X-Bridge-Token", token);
                    var txt = await http.GetStringAsync($"http://127.0.0.1:{port}/ping", ct);
                    if (txt.Contains("\"state\":\"ingame\"")) return (http, port);
                    log.Report("loading the save… (" + (txt.Contains("\"state\":\"") ? txt.Substring(txt.IndexOf("\"state\":\"") + 9).Split('"')[0] : "starting") + ")");
                }
                else log.Report("game starting…");
            }
            catch (Exception) when (!ct.IsCancellationRequested) { }
            await Task.Delay(3000, ct);
        }
        throw new TimeoutException("the game did not reach the save within 8 minutes");
    }

    /// <summary>Runs a console command in the game through the bridge (e.g. "killall", "settime 1 9 0").</summary>
    static async Task GameCommand(HttpClient http, int port, string command, CancellationToken ct)
    {
        try { await http.PostAsync($"http://127.0.0.1:{port}/console?cmd={Uri.EscapeDataString(command)}", new StringContent(""), ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { }
    }

    static async Task<bool> PlayerIsDead(HttpClient http, int port, CancellationToken ct)
    {
        try { return (await http.GetStringAsync($"http://127.0.0.1:{port}/state?sections=player", ct)).Contains("\"dead\":true"); }
        catch (Exception) when (!ct.IsCancellationRequested) { return false; }
    }

    public static async Task<BenchResult> Run(BenchOptions o, string gameExe, IProgress<string> log, CancellationToken ct, IProgress<LiveSample> live = null)
    {
        Directory.CreateDirectory(OutDir);
        Directory.CreateDirectory(Path.Combine(OutDir, "bench"));
        if (Process.GetProcessesByName("7DaysToDie").Length > 0) throw new InvalidOperationException("7 Days To Die is already running. Close it first.");
        try { if (File.Exists(SessionFile)) File.Delete(SessionFile); } catch { }

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        if (o.FreshSave && TestSave.IsConfigured()) log.Report(TestSave.Restore());
        log.Report("launching the game normally (nothing is loaded into it)…");
        int pid = GameLauncher.LaunchPlain(gameExe, o.GameArgs, out uint mainTid);
        var proc = Process.GetProcessById(pid);
        try
        {
            // Watch the whole start-up too, so the readings are live from the first second (they are only *recorded* for the window).
            using var mon = new LiveMonitor(pid, mainTid);
            if (!mon.FpsAvailable) throw new InvalidOperationException(mon.FpsError ?? "frame capture is not available");
            var startup = new System.Timers.Timer(1000) { AutoReset = true };
            startup.Elapsed += (_, _) => { try { live?.Report(mon.Sample()); } catch { } };
            startup.Start();

            (HttpClient http, int port) bridge;
            try { bridge = await WaitForBridge(pid, log, ct); }
            finally { startup.Stop(); startup.Dispose(); }
            var (http, port) = bridge;
            log.Report("save loaded; setting 9:00 and clearing zombies…");
            await GameCommand(http, port, "settime 1 9 0", ct);
            await GameCommand(http, port, "killall", ct);
            try { await http.PostAsync($"http://127.0.0.1:{port}/cleararea?radius=40", new StringContent(""), ct); } catch { }

            for (int s = o.WarmupSec; s > 0; s--)
            {
                ct.ThrowIfCancellationRequested();
                if (proc.HasExited) throw new InvalidOperationException("the game exited during warm-up");
                log.Report($"warming up… {s}s");
                live?.Report(mon.Sample());
                if (s % 30 == 0) await GameCommand(http, port, "killall", ct);
                await Task.Delay(1000, ct);
            }
            await GameCommand(http, port, "killall", ct);

            var os = new List<OsSample>();
            long windowStart = DateTime.UtcNow.ToFileTimeUtc();
            mon.Sample();                                                 // resets the CPU deltas to the start of the window
            var sw = Stopwatch.StartNew();
            for (int i = 1; i <= o.DurationSec; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (proc.HasExited) throw new InvalidOperationException("the game exited during the measurement");
                await Task.Delay(Math.Max(1, (int)(i * 1000 - sw.ElapsedMilliseconds)), ct);
                var s = mon.Sample();
                os.Add(s.Os);
                live?.Report(s);
                log.Report($"measuring… {i}/{o.DurationSec}s   {s.Fps:F0} fps");
                if (i % 30 == 0) await GameCommand(http, port, "killall", ct);     // keep the character alive for the whole window
            }
            long windowEnd = DateTime.UtcNow.ToFileTimeUtc();
            if (await PlayerIsDead(http, port, ct))
                throw new InvalidOperationException("the player died during the measurement, so the result was discarded (a dead player is not running the normal gameplay code)");

            var presents = mon.PresentTicks().Where(t => t >= windowStart && t <= windowEnd).ToList();
            if (presents.Count < 30) throw new InvalidOperationException($"no frame data was captured ({mon.EventsSeen} graphics events seen, {presents.Count} from the game)");

            var result = new BenchResult
            {
                Label = o.Label, Created = DateTime.Now.ToString("s"), DurationSec = o.DurationSec, Os = os,
                Machine = $"{Environment.MachineName}, {Environment.ProcessorCount} logical CPUs" + (mon.GpuAvailable ? "" : " (GPU counters unavailable)"),
                Notes = $"{mon.EventsSeen} graphics events, {presents.Count} Present calls in the window (real-time capture)",
                Game = EtwFrames.ToGameBench(presents),
            };
            string file = Path.Combine(OutDir, "bench", $"bench_{Sanitize(o.Label)}_{stamp}.json");
            result.Save(file);
            log.Report("saved " + file);
            return result;
        }
        finally
        {
            if (o.CloseGame)
            {
                try { if (!proc.HasExited) { proc.CloseMainWindow(); if (!proc.WaitForExit(45000)) proc.Kill(); } } catch { }
            }
        }
    }

    static string Sanitize(string s) => string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c));
}

// ---- before/after comparison (shared by the Benchmark tab and the command line) ---------------------------------------------

static class BenchCompare
{
    public sealed record Row(string Area, string Metric, string Baseline, string New, string Change, string Verdict, int State);

    static string Fmt(double v, string unit) => unit is "fps" or "%" or "cores" or "% of a core" ? v.ToString("F1") : v.ToString(v >= 100 ? "F0" : "F2");

    public static List<Row> Build(IReadOnlyList<BenchResult> baseline, IReadOnlyList<BenchResult> current)
    {
        var rows = new List<Row>();
        foreach (var m in BenchMath.Metrics)
        {
            double[] a = baseline.Where(r => r.Summary.ContainsKey(m.Key)).Select(r => r.Summary[m.Key]).ToArray();
            double[] b = current.Where(r => r.Summary.ContainsKey(m.Key)).Select(r => r.Summary[m.Key]).ToArray();
            if (a.Length == 0 && b.Length == 0) continue;
            string sa = a.Length == 0 ? "" : Fmt(a.Average(), m.Unit) + (a.Length > 1 ? $"  (±{(a.Max() - a.Min()) / 2:F1})" : "");
            string sb = b.Length == 0 ? "" : Fmt(b.Average(), m.Unit) + (b.Length > 1 ? $"  (±{(b.Max() - b.Min()) / 2:F1})" : "");
            string change = "", verdict = ""; int state = 2;
            if (a.Length > 0 && b.Length > 0)
            {
                double da = a.Average(), db = b.Average(), delta = db - da, pct = da == 0 ? 0 : 100 * delta / Math.Abs(da);
                change = $"{(delta >= 0 ? "+" : "")}{Fmt(delta, m.Unit)} {m.Unit}  ({(pct >= 0 ? "+" : "")}{pct:F1}%)";
                // a change only counts when it is larger than the run-to-run spread (or 3% when there is a single run per side)
                double noise = Math.Max(a.Length > 1 ? a.Max() - a.Min() : 0, b.Length > 1 ? b.Max() - b.Min() : 0);
                double threshold = Math.Max(noise, Math.Abs(da) * 0.03);
                if (Math.Abs(delta) <= threshold) { verdict = a.Length > 1 || b.Length > 1 ? "within noise" : "about the same"; state = 0; }
                else { bool better = m.HigherIsBetter ? delta > 0 : delta < 0; verdict = better ? "better" : "worse"; state = better ? 1 : -1; }
            }
            rows.Add(new Row(m.Group, m.Name + (m.Unit.Length > 0 ? $" ({m.Unit})" : ""), sa, sb, change, verdict, state));
        }
        return rows;
    }

    public static string ToText(IReadOnlyList<BenchResult> baseline, IReadOnlyList<BenchResult> current)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Baseline: {string.Join(", ", baseline.Select(r => r.Label + " @" + r.Created))}");
        sb.AppendLine($"New:      {string.Join(", ", current.Select(r => r.Label + " @" + r.Created))}");
        sb.AppendLine();
        string area = null;
        foreach (var r in Build(baseline, current))
        {
            if (r.Area != area) { area = r.Area; sb.AppendLine($"[{area}]"); }
            sb.AppendLine($"  {r.Metric,-58} {r.Baseline,-16} {r.New,-16} {r.Change,-30} {r.Verdict}");
        }
        return sb.ToString();
    }
}
