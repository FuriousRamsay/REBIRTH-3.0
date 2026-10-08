using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RebirthProfiler;

/// <summary>One thing to open in the game: some unmeasured setup clicks, then the measured action.</summary>
sealed record HitchStep(string Name, string[] Prepare, string Measure, string Expect);

/// <summary>One open of one window: what the frame times did around the click, and where the CPU time went.</summary>
sealed class HitchTrial
{
    public string Step { get; set; } = "";
    public int Index { get; set; }
    public bool Opened { get; set; }
    public double BaselineMs { get; set; }
    public double WorstMs { get; set; }
    public double LowFps { get; set; }          // worst 250 ms stretch after the click
    public double StallMs { get; set; }         // time lost in slow frames (sum of frame time above baseline)
    public double RecoverySec { get; set; }     // until a 250 ms stretch was back within 25% of the baseline
    public int SlowFrames { get; set; }
    public double[] T = Array.Empty<double>();   // frame end times, seconds relative to the click
    public double[] Ms = Array.Empty<double>();  // frame durations
    public string OpenProfile { get; set; } = "";
    public string BaseProfile { get; set; } = "";
    public string Windows { get; set; } = "";
}

sealed class HitchResult
{
    public string Created { get; set; } = DateTime.Now.ToString("s");
    public string Notes { get; set; } = "";
    public List<HitchTrial> Trials { get; set; } = new();
    static readonly JsonSerializerOptions Opt = new() { IncludeFields = true, WriteIndented = false };
    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Opt));
    public static HitchResult Load(string path) => JsonSerializer.Deserialize<HitchResult>(File.ReadAllText(path), Opt) ?? new HitchResult();
}

static class HitchAnalysis
{
    public const double BeforeSec = 3, AfterSec = 6;

    /// <summary>Turns present timestamps (FILETIME ticks) into frame durations relative to <paramref name="clickTicks"/>.</summary>
    public static void Fill(HitchTrial t, List<long> presents, long clickTicks)
    {
        var ts = new List<double>(); var ms = new List<double>();
        long prev = 0;
        foreach (long p in presents)
        {
            if (p < clickTicks - (long)((BeforeSec + 1) * 1e7)) continue;
            if (p > clickTicks + (long)((AfterSec + 0.5) * 1e7)) break;
            if (prev != 0 && p - prev > 10000)                       // presents within 1 ms belong to the same frame
            {
                ts.Add((p - clickTicks) / 1e7); ms.Add((p - prev) / 10000.0);
            }
            if (prev == 0 || p - prev > 10000) prev = p;
        }
        t.T = ts.ToArray(); t.Ms = ms.ToArray();
        var before = Enumerable.Range(0, ts.Count).Where(i => ts[i] < 0).Select(i => ms[i]).OrderBy(x => x).ToList();
        t.BaselineMs = before.Count > 0 ? before[before.Count / 2] : 0;
        double b = Math.Max(t.BaselineMs, 1);
        double slowLimit = Math.Max(b * 2, 25);
        for (int i = 0; i < ts.Count; i++)
        {
            if (ts[i] < 0 || ts[i] > AfterSec) continue;
            if (ms[i] > t.WorstMs) t.WorstMs = ms[i];
            if (ms[i] > slowLimit) { t.SlowFrames++; t.StallMs += ms[i] - b; }
        }
        // Rolling 250 ms average, stepped by 50 ms: catches a long dip made of many moderately slow frames.
        double worstAvg = 0, lastBad = 0;
        for (double w = 0; w <= AfterSec - 0.25; w += 0.05)
        {
            double sum = 0; int n = 0;
            for (int i = 0; i < ts.Count; i++)
                if (ts[i] > w && ts[i] <= w + 0.25) { sum += ms[i]; n++; }
            if (n == 0) { worstAvg = Math.Max(worstAvg, 250); lastBad = w + 0.25; continue; }   // no frame at all in 250 ms: a stall
            double avg = sum / n;
            if (avg > worstAvg) worstAvg = avg;
            if (avg > b * 1.25) lastBad = w + 0.25;
        }
        t.LowFps = worstAvg > 0 ? 1000.0 / worstAvg : 0;
        t.RecoverySec = lastBad;
    }

    public sealed record Culprit(string Method, double ExcessMs, bool Rebirth);

    /// <summary>Where the extra main-thread time went in the seconds after the click, compared with the seconds before it.</summary>
    public static List<Culprit> Culprits(IEnumerable<HitchTrial> trials, string mod)
    {
        var sum = new Dictionary<string, double>();
        int n = 0;
        foreach (var t in trials)
        {
            if (!File.Exists(t.OpenProfile) || !File.Exists(t.BaseProfile)) continue;
            Profile open, bas;
            try { open = Profile.Load(t.OpenProfile); bas = Profile.Load(t.BaseProfile); } catch { continue; }
            var o = Rates(open); var b = Rates(bas);
            foreach (var (k, v) in o)
            {
                double excess = (v - b.GetValueOrDefault(k)) * open.WallSec;
                if (excess > 0) sum[k] = sum.GetValueOrDefault(k) + excess;
            }
            n++;
        }
        if (n == 0) return new();
        return sum.Select(kv => new Culprit(kv.Key, kv.Value / n, mod.Length > 0 && kv.Key.Contains(mod, StringComparison.OrdinalIgnoreCase)))
                  .Where(c => c.ExcessMs >= 1).OrderByDescending(c => c.ExcessMs).Take(60).ToList();
    }

    static Dictionary<string, double> Rates(Profile p) =>
        p.Methods.GroupBy(m => m.Short).ToDictionary(g => g.Key, g => g.Sum(m => p.MainMsPerSec(m.Incl)));
}

static class HitchRunner
{
    public static readonly HitchStep[] Steps =
    {
        new("Inventory screen (Tab)",  new[] { "key:Escape" },                                         "key:Tab",                                  "windowpaging"),
        new("Crafting tab",            new[] { "btnRebirthCraftingTabQuests" },                        "btnRebirthCraftingTabCrafting",            "crafting"),
        new("Crafting: Explorer",      new[] { "btnRebirthCraftingTabCrafting" },                      "btnRebirthCraftingExplorer",               "rebirthProgressionExplorer"),
        new("Character: Overview",     new[] { "btnRebirthCraftingTabQuests" },                        "btnRebirthCraftingTabCharacter",           "rebirthSurvivorCharacter"),
        new("Character: Progression",  new[] { "btnRebirthCraftingTabCharacter", "btnSurvivorTabOverview" }, "btnSurvivorTabProgression",          "rebirthSurvivorCharacter"),
        new("Character: Condition",    new[] { "btnRebirthCraftingTabCharacter", "btnSurvivorTabOverview" }, "btnSurvivorTabCondition",            "rebirthSurvivorCharacter"),
        new("Character: Statistics",   new[] { "btnRebirthCraftingTabCharacter", "btnSurvivorTabOverview" }, "btnSurvivorTabStatistics",           "rebirthSurvivorCharacter"),
        new("Character: Metabolism",   new[] { "btnRebirthCraftingTabCharacter", "btnSurvivorTabOverview" }, "btnSurvivorTabMetabolism",           "rebirthSurvivorCharacter"),
        new("Quests tab",              new[] { "btnRebirthCraftingTabCrafting" },                      "btnRebirthCraftingTabQuests",              "quests"),
        new("Challenges tab",          new[] { "btnRebirthCraftingTabQuests" },                        "btnRebirthCraftingTabChallenges",          "challenges"),
        new("Players tab",             new[] { "btnRebirthCraftingTabQuests" },                        "btnRebirthCraftingTabPlayers",             "players"),
        new("Journal",                 new[] { "btnRebirthCraftingTabQuests" },                        "btnRebirthCraftingTabJournal",             "rebirthJournal"),
    };

    static string OutDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "out"));
    static string SessionFile => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "GameBridge", "out", "session.json"));

    /// <summary>A client for the running game's bridge, or null when the game was started without it.</summary>
    internal static BridgeClient ConnectBridge()
    {
        var s = ReadSession();
        return s == null ? null : new BridgeClient(s.Value.port, s.Value.token);
    }

    static (int port, string token)? ReadSession()
    {
        try
        {
            using var s = JsonDocument.Parse(File.ReadAllText(SessionFile));
            return (s.RootElement.GetProperty("port").GetInt32(), s.RootElement.GetProperty("token").GetString());
        }
        catch { return null; }
    }

    internal sealed class BridgeClient
    {
        readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
        readonly int port;
        public BridgeClient(int port, string token) { this.port = port; http.DefaultRequestHeaders.Add("X-Bridge-Token", token); }
        public async Task<string> Post(string path, CancellationToken ct)
        {
            try { return await (await http.PostAsync($"http://127.0.0.1:{port}{path}", new StringContent(""), ct)).Content.ReadAsStringAsync(ct); }
            catch (Exception) when (!ct.IsCancellationRequested) { return ""; }
        }
        public async Task<string> Get(string path, CancellationToken ct)
        {
            try { return await http.GetStringAsync($"http://127.0.0.1:{port}{path}", ct); }
            catch (Exception) when (!ct.IsCancellationRequested) { return ""; }
        }
        public Task<string> Do(string action, CancellationToken ct) =>
            action.StartsWith("key:") ? Post("/key?name=" + Uri.EscapeDataString(action[4..]), ct) : Post("/ui/click?id=" + Uri.EscapeDataString(action), ct);
        public Task<string> Console(string cmd, CancellationToken ct) => Post("/console?cmd=" + Uri.EscapeDataString(cmd), ct);
        public Task<string> OpenWindows(CancellationToken ct) => Get("/ui", ct);
    }

    /// <summary>Asks the sampler for a dump of everything since the last reset and copies the result.</summary>
    static async Task<string> DumpTo(string livePath, string dest, CancellationToken ct)
    {
        File.WriteAllText(livePath + ".dump", "");
        for (int i = 0; i < 40 && File.Exists(livePath + ".dump"); i++) await Task.Delay(250, ct);
        await Task.Delay(200, ct);
        File.Copy(livePath, dest, true);
        return dest;
    }

    static async Task PrepareGameplay(BridgeClient bridge, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            string windows = await bridge.OpenWindows(ct);
            if (!Regex.IsMatch(windows, "windowpaging|crafting|rebirthSurvivorCharacter|rebirthJournal|rebirthProgressionExplorer|ingameMenu|quests|challenges|players|\\\"map\\\"")) return;
            await bridge.Do("key:Escape", ct);
            await Task.Delay(350, ct);
        }
        throw new InvalidOperationException("Could not return to gameplay before a window measurement.");
    }

    public static async Task<HitchResult> Run(Func<LiveMonitor> monitor, string livePath, IReadOnlyList<HitchStep> steps, int trials,
                                              IProgress<string> log, CancellationToken ct)
    {
        var mon = monitor();
        if (mon == null || !mon.FpsAvailable) throw new InvalidOperationException("No frame capture: start the game with the profiler (Launch game with profiler) and wait until it is in the save.");
        if (livePath == null || !File.Exists(livePath)) throw new InvalidOperationException("The profiler is not attached to this game (no live profile file). Use \"Launch game with profiler\".");
        var ses = ReadSession() ?? throw new InvalidOperationException("The game bridge is not running (no session.json).");
        var br = new BridgeClient(ses.port, ses.token);
        if (!(await br.Get("/ping", ct)).Contains("\"state\":\"ingame\"")) throw new InvalidOperationException("The game is not in a save yet.");

        string dir = Path.Combine(OutDir, "hitch_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(dir);
        var result = new HitchResult();
        await br.Get("/surroundings", ct);
        await br.Post("/cleararea?radius=40", ct);
        await br.Post("/restore", ct);
        await br.Console("settime 1 9 0", ct);

        foreach (var step in steps)
        {
            await br.Post("/cleararea?radius=40", ct);
            for (int n = 1; n <= trials; n++)
            {
                ct.ThrowIfCancellationRequested();
                string tag = $"{Regex.Replace(step.Name, "[^A-Za-z0-9]+", "_")}_{n}";
                log.Report($"{step.Name}: trial {n} of {trials}");
                // Keep normal guard behavior enabled, but remove hunger/thirst as fixture noise.
                // /restore intentionally restores only health and stamina.
                await br.Console("rbmet set hydration 95", ct);
                await br.Console("rbmet set nutrition 95", ct);
                await br.Console("rbmet set energy 95", ct);
                // Begin every trial from a known state. An unconditional Escape from gameplay
                // opens Pause, making the subsequent Tab a no-op and producing a false hitch result.
                await PrepareGameplay(br, ct);
                if (step.Measure != "key:Tab")
                {
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        await Task.Delay(500, ct);
                        await br.Do("key:Tab", ct);
                        await Task.Delay(1200, ct);
                        if ((await br.OpenWindows(ct)).Contains("\"crafting\"")) break;
                        await PrepareGameplay(br, ct);
                    }
                    if (!(await br.OpenWindows(ct)).Contains("\"crafting\""))
                        throw new InvalidOperationException("Crafting did not open during unmeasured setup.");
                }
                foreach (var a in step.Prepare)
                {
                    if (step.Measure == "key:Tab" && a == "key:Escape") continue;
                    string response = await br.Do(a, ct);
                    using var parsed = JsonDocument.Parse(response);
                    if (!parsed.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
                        throw new InvalidOperationException("Window setup action failed: " + a);
                    await Task.Delay(1500, ct);
                }

                // Steady seconds before the click: frame baseline and the CPU baseline.
                File.WriteAllText(livePath + ".reset", "");
                await Task.Delay((int)(HitchAnalysis.BeforeSec * 1000), ct);
                string baseFile = await DumpTo(livePath, Path.Combine(dir, tag + "_before.json"), ct);

                File.WriteAllText(livePath + ".reset", "");
                long click = DateTime.UtcNow.ToFileTimeUtc();
                await br.Do(step.Measure, ct);
                await Task.Delay(4000, ct);
                string openFile = await DumpTo(livePath, Path.Combine(dir, tag + "_after.json"), ct);
                await Task.Delay(2500, ct);                         // let the frame tail arrive

                string windows = await br.OpenWindows(ct);
                var t = new HitchTrial { Step = step.Name, Index = n, OpenProfile = openFile, BaseProfile = baseFile, Windows = windows, Opened = windows.Contains(step.Expect) };
                HitchAnalysis.Fill(t, mon.PresentTicks(), click);
                result.Trials.Add(t);
                await br.Post("/cleararea?radius=40", ct);
            }
            await br.Do("key:Escape", ct);
            await Task.Delay(1500, ct);
        }
        result.Save(Path.Combine(dir, "result.json"));
        result.Save(Path.Combine(OutDir, "hitch_" + Path.GetFileName(dir).Substring(6) + ".json"));
        return result;
    }
}
