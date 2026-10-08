using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RebirthProfiler;

/// <summary>One dip found while playing: when, how bad, and what used the extra time.</summary>
sealed class PlayHitch
{
    public string Time { get; set; } = "";              // local clock time of the slice end
    public double SessionSec { get; set; }
    public double BaselineMs { get; set; }
    public double WorstMs { get; set; }
    public double LowFps { get; set; }
    public double LostMs { get; set; }
    public int SlowFrames { get; set; }
    public double GcMs { get; set; }                    // garbage-collection pause time inside the slice
    public double GcWorstMs { get; set; }
    public int Collections { get; set; }                // every Mono collection in the slice (counter), not just the ones the event callback reports
    public int GcCount { get; set; }
    public string Hint { get; set; } = "";              // window open/close methods seen in the slice
    public List<HitchAnalysis.Culprit> Causes { get; set; } = new();
    public string SliceFile { get; set; } = "";
}

/// <summary>An error or exception line that keeps coming back in the game log while recording.</summary>
sealed class LogIssue
{
    public string Message { get; set; } = "";
    public int Count { get; set; }
    public string First { get; set; } = "";
    public string Last { get; set; } = "";
    public double PerMinute { get; set; }
}

sealed class SliceStat
{
    public string Time { get; set; } = "";
    public int Frames { get; set; }
    public double AvgMs { get; set; }
    public double MedianMs { get; set; }
    public double P99Ms { get; set; }
    public double WorstMs { get; set; }
    public int Collections { get; set; }
}

/// <summary>A labelled stretch of play (started by dropping a file named "<profile>.mark" containing the label): frame statistics and where the main thread spent its time.</summary>
sealed class SegmentStat
{
    public string Label { get; set; } = "";
    public string Start { get; set; } = "";
    public double Seconds { get; set; }
    public double AvgFps { get; set; }
    public double MedianMs { get; set; }
    public double P99Ms { get; set; }
    public double WorstMs { get; set; }
    public int SlicesUnder30Fps { get; set; }
    public int Slices { get; set; }
    public int Collections { get; set; }
    public List<KeyValuePair<string, double>> Inclusive { get; set; } = new();   // main-thread ms per second, whole call tree below the method
    public List<KeyValuePair<string, double>> Self { get; set; } = new();        // main-thread ms per second in the method itself
    public List<KeyValuePair<string, double>> Rebirth { get; set; } = new();     // same as Inclusive, mod code only
    public List<KeyValuePair<string, double>> Threads { get; set; } = new();     // worker threads: CPU time per thread (ms of thread time per second of wall time)
    public List<KeyValuePair<string, double>> WorkerInclusive { get; set; } = new();   // worker threads: methods, whole call tree
}

sealed class PlayResult
{
    public string Created { get; set; } = DateTime.Now.ToString("s");
    public double Seconds { get; set; }
    public int Slices { get; set; }
    public double MedianFrameMs { get; set; }
    public int Collections { get; set; }                // Mono garbage collections during the recording (after the warm-up)
    public double CollectionsPerMinute { get; set; }
    public double HeapMB { get; set; }                  // managed heap size at the last slice
    public double HeapUsedMB { get; set; }
    public List<string> CollectionTimes { get; set; } = new();
    public double AllocSeconds { get; set; }                       // recorded time with allocation tracking on
    public Dictionary<string, double> AllocByType { get; set; } = new();   // bytes over the whole recording
    public Dictionary<string, double> AllocBySite { get; set; } = new();   // bytes by the code that allocated (first Rebirth frame, else the calling chain)   // clock time of each slice that contained a collection
    public List<PlayHitch> Hitches { get; set; } = new();
    /// <summary>Frame statistics for every 2 s slice (including warm-up ones), so any stretch of play can be summarised afterwards by clock time.</summary>
    public List<SliceStat> SliceStats { get; set; } = new();
    public List<SegmentStat> Segments { get; set; } = new();
    /// <summary>Repeated ERR / EXC log lines seen during the recording (numbers in the text are folded together), worst first.</summary>
    public List<LogIssue> LogIssues { get; set; } = new();

    /// <summary>Average FPS, 1% low FPS and worst frame over the slices whose end falls between two clock times (HH:mm:ss).</summary>
    public string Summarise(string from, string to)
    {
        var s = SliceStats.Where(x => string.CompareOrdinal(x.Time, from) >= 0 && string.CompareOrdinal(x.Time, to) <= 0).ToList();
        if (s.Count == 0) return "no slices";
        double frames = s.Sum(x => x.Frames);
        double avgFps = frames / Math.Max(0.001, s.Sum(x => x.Frames * x.AvgMs) / 1000.0);
        var worst = s.OrderByDescending(x => x.WorstMs).First();
        var lows = s.Select(x => x.P99Ms).OrderBy(x => x).ToList();
        return $"{s.Count} slices: avg {avgFps:F0} fps, typical frame {s.Average(x => x.MedianMs):F1} ms, 99th percentile (median of slices) {lows[lows.Count / 2]:F0} ms, worst frame {worst.WorstMs:F0} ms at {worst.Time}, slices under 30 fps: {s.Count(x => 1000.0 / Math.Max(1, x.AvgMs) < 30)}, collections {s.Sum(x => x.Collections)}";
    }
    static readonly JsonSerializerOptions Opt = new() { IncludeFields = true, WriteIndented = true };
    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Opt));
    public static PlayResult Load(string path) => JsonSerializer.Deserialize<PlayResult>(File.ReadAllText(path), Opt) ?? new PlayResult();

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Play recording {Created}: {Seconds:F0} s, {Slices} slices of 2 s, typical frame {MedianFrameMs:F0} ms, {Hitches.Count} dips");
        sb.AppendLine($"Garbage collection: {Collections} collections ({CollectionsPerMinute:F1} per minute), managed heap {HeapMB:F0} MB ({HeapUsedMB:F0} MB in use)");
        if (AllocSeconds > 0)
        {
            double total = AllocByType.Values.Sum();
            sb.AppendLine($"Allocation (sampled): {total / AllocSeconds / 1024:F0} KB/s over {AllocSeconds:F0} s");
            sb.AppendLine("   by type:");
            foreach (var kv in AllocByType.OrderByDescending(x => x.Value).Take(12)) sb.AppendLine($"      {kv.Value / AllocSeconds / 1024,7:F0} KB/s  {kv.Key}");
            sb.AppendLine("   by code:");
            foreach (var kv in AllocBySite.OrderByDescending(x => x.Value).Take(25)) sb.AppendLine($"      {kv.Value / AllocSeconds / 1024,7:F0} KB/s  {kv.Key}");
        }
        if (CollectionTimes.Count > 0) sb.AppendLine("     at: " + string.Join(", ", CollectionTimes));
        if (LogIssues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Errors and exceptions in the game log (repeated lines first):");
            foreach (var li in LogIssues.Take(15)) sb.AppendLine($"   {li.Count,6}x  ({li.PerMinute:F1} per minute, {li.First} to {li.Last})  {li.Message}");
        }
        foreach (var g in Segments)
        {
            sb.AppendLine();
            sb.AppendLine($"== segment \"{g.Label}\" from {g.Start}, {g.Seconds:F0} s: {g.AvgFps:F0} fps average, typical frame {g.MedianMs:F1} ms, 99th percentile {g.P99Ms:F0} ms, worst {g.WorstMs:F0} ms, {g.SlicesUnder30Fps} of {g.Slices} slices under 30 fps, {g.Collections} collections");
            sb.AppendLine("   main thread, whole call tree (ms per second):");
            foreach (var kv in g.Inclusive.Take(25)) sb.AppendLine($"      {kv.Value,7:F1}  {kv.Key}");
            sb.AppendLine("   main thread, method itself (ms per second):");
            foreach (var kv in g.Self.Take(20)) sb.AppendLine($"      {kv.Value,7:F1}  {kv.Key}");
            if (g.Threads.Count > 0)
            {
                sb.AppendLine("   worker threads, CPU time per thread (ms of thread time per second):");
                foreach (var kv in g.Threads.Take(12)) sb.AppendLine($"      {kv.Value,7:F1}  {kv.Key}");
                sb.AppendLine("   worker threads, methods (whole call tree, ms per second):");
                foreach (var kv in g.WorkerInclusive.Take(25)) sb.AppendLine($"      {kv.Value,7:F1}  {kv.Key}");
            }
            sb.AppendLine("   mod code (ms per second):");
            foreach (var kv in g.Rebirth.Take(25)) sb.AppendLine($"      {kv.Value,7:F2}  {kv.Key}");
        }
        sb.AppendLine();
        foreach (var h in Hitches)
        {
            sb.AppendLine($"[{h.Time}] +{h.SessionSec:F0}s  worst {h.WorstMs:F0} ms (typical {h.BaselineMs:F0}), lowest {h.LowFps:F0} fps, time lost {h.LostMs:F0} ms, {h.SlowFrames} slow frames");
            if (h.Collections > 0) sb.AppendLine($"     Mono collections in this slice: {h.Collections}");
            if (h.GcCount > 0) sb.AppendLine($"     garbage collection: {h.GcCount} in this slice, {h.GcMs:F0} ms paused (worst {h.GcWorstMs:F0} ms)");
            if (h.Hint.Length > 0) sb.AppendLine("     window activity: " + h.Hint);
            foreach (var c in h.Causes.Take(10)) sb.AppendLine($"     {c.ExcessMs,6:F0} ms  {(c.Rebirth ? "*" : " ")} {c.Method}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

/// <summary>Watches a running game in 2-second slices: frame times from ETW, CPU from the sampler; keeps the slices that dipped.</summary>
sealed class PlayRecorder
{
    readonly Func<LiveMonitor> monitor;
    readonly string livePath;
    readonly string mod;
    readonly string dir;
    readonly string resultPath;
    readonly PlayResult result = new();
    readonly Dictionary<string, double> ema = new();      // typical ms/s per method in ordinary slices
    readonly List<double> medians = new();
    CancellationTokenSource cts;
    Task loop;
    public event Action<PlayHitch> Dip;
    public event Action<string> Status;
    public bool Running => loop != null && !loop.IsCompleted;
    public PlayResult Result => result;
    public string ResultPath => resultPath;

    const double SliceSec = 2, WarmupSec = 60;

    public PlayRecorder(Func<LiveMonitor> monitor, string livePath, string mod)
    {
        this.monitor = monitor; this.livePath = livePath; this.mod = mod;
        string outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "out"));
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        dir = Path.Combine(outDir, "play_" + stamp);
        resultPath = Path.Combine(outDir, "play_" + stamp + ".json");
    }

    public void Start(double warmupSeconds = WarmupSec)
    {
        Directory.CreateDirectory(dir);
        cts = new CancellationTokenSource();
        loop = Task.Run(() => Loop(warmupSeconds, cts.Token));
    }

    public async Task Stop()
    {
        cts?.Cancel();
        try { if (loop != null) await loop; } catch { }
        Save();
    }

    void Save()
    {
        CloseSegmentForSave();
        lock (result) result.Hitches.RemoveAll(h => h.SessionSec > result.Seconds - TailSlices * SliceSec);
        try { result.Save(resultPath); File.WriteAllText(Path.ChangeExtension(resultPath, ".txt"), result.ToText(), new UTF8Encoding(true)); } catch { }
    }

    async Task Loop(double warmup, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        warmupSecondsForRate = warmup;
        int n = 0;
        DateTime lastSave = DateTime.UtcNow;
        try { File.WriteAllText(livePath + ".reset", ""); } catch { }
        var sliceStart = DateTime.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay((int)(SliceSec * 1000), ct);
            var mon = monitor();
            if (mon == null || mon.HasExited) { Status?.Invoke("game closed"); break; }
            var sliceEnd = DateTime.UtcNow;
            try
            {
                File.WriteAllText(livePath + ".dump", "");
                for (int i = 0; i < 40 && File.Exists(livePath + ".dump"); i++) await Task.Delay(100, ct);
                await Task.Delay(100, ct);
                string snap = Path.Combine(dir, "last.json");
                File.Copy(livePath, snap, true);
                var prof = await Task.Run(() => Profile.Load(snap), ct);
                File.WriteAllText(livePath + ".reset", "");
                var frames = SliceFrames(mon.PresentTicks(), sliceStart.ToFileTimeUtc(), sliceEnd.ToFileTimeUtc());
                double session = (sliceEnd - started).TotalSeconds;
                CheckMark(mod);
                ScanLog();
                Analyse(prof, frames, session, sliceEnd, session < warmup, snap, sliceStart, ct);
                n++; result.Slices = n; result.Seconds = session;
                Status?.Invoke($"recording: {session:F0} s, {result.Hitches.Count} dips so far");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Status?.Invoke("recording problem: " + ex.Message); }
            sliceStart = sliceEnd;
            if ((DateTime.UtcNow - lastSave).TotalSeconds > 30) { Save(); lastSave = DateTime.UtcNow; }
        }
        CloseSegment();
        Save();
    }

    static List<double> SliceFrames(List<long> presents, long from, long to)
    {
        var d = new List<double>();
        long prev = 0;
        foreach (long p in presents)
        {
            if (p < from) { prev = p; continue; }
            if (p > to) break;
            if (prev != 0 && p - prev > 10000) d.Add((p - prev) / 10000.0);
            if (prev == 0 || p - prev > 10000) prev = p;
        }
        return d;
    }

    static readonly Regex LoadOnly = new(@"^(XUiFromXml:LoadXui|ChunkProviderGenerateWorldFromRaw|WorldDecoratorPOIFromImage|GameManager:StartGame|GameManager:loadPrefabs)", RegexOptions.Compiled);
    double lastLoadSec = -1000;

    // When the game closes, the engine tears down every UI controller (each unsubscribe allocates) and collects repeatedly.
    // Totals therefore lag by TailSlices slices and the unfinished tail is dropped, so shutdown never reaches the result.
    const int TailSlices = 8;
    sealed class SliceTotals
    {
        public int Collections; public string Time = ""; public double Seconds, Session; public bool HasAlloc;
        public Dictionary<string, double> ByType = new(), BySite = new();
    }
    readonly Queue<SliceTotals> pending = new();

    // ---- game log watcher -------------------------------------------------------------------------------------------
    static readonly string PlayerLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "The Fun Pimps", "7 Days To Die", "Player.log");
    long logOffset = -1;
    readonly Dictionary<string, LogIssue> logIssues = new();
    DateTime logStarted = DateTime.Now;
    static readonly Regex LogDigits = new(@"-?\d+(\.\d+)?", RegexOptions.Compiled);

    /// <summary>Reads the lines the game appended since the last call and counts error / exception messages. Repeats are the giveaway of a per-frame bug.</summary>
    void ScanLog()
    {
        try
        {
            if (!File.Exists(PlayerLog)) return;
            using var fs = new FileStream(PlayerLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (logOffset < 0 || fs.Length < logOffset) { logOffset = fs.Length; logStarted = DateTime.Now; return; }   // first look, or the game restarted the file
            fs.Seek(logOffset, SeekOrigin.Begin);
            using var rd = new StreamReader(fs, Encoding.UTF8);
            string line; string now = DateTime.Now.ToString("HH:mm:ss");
            while ((line = rd.ReadLine()) != null)
            {
                int k = line.IndexOf(" ERR ", StringComparison.Ordinal); if (k < 0) k = line.IndexOf(" EXC ", StringComparison.Ordinal);
                if (k < 0 || line.Contains("[Discord]")) continue;
                string msg = LogDigits.Replace(line.Substring(k + 1), "N");
                if (msg.Length > 150) msg = msg.Substring(0, 150);
                lock (result)
                {
                    if (!logIssues.TryGetValue(msg, out var li)) logIssues[msg] = li = new LogIssue { Message = msg, First = now };
                    li.Count++; li.Last = now;
                }
            }
            logOffset = fs.Position;
            lock (result)
            {
                double minutes = Math.Max(0.5, (DateTime.Now - logStarted).TotalMinutes);
                foreach (var li in logIssues.Values) li.PerMinute = li.Count / minutes;
                result.LogIssues = logIssues.Values.OrderByDescending(x => x.Count).Take(30).ToList();
            }
        }
        catch { }
    }

    void CloseSegmentForSave()
    {
        // keep the running segment visible in saved files without ending it
        lock (result) result.Segments.RemoveAll(x => x.Label.EndsWith(" (running)"));
        var g = segment;
        if (g == null) return;
        var copy = new SegmentAcc { Label = g.Label + " (running)", Start = g.Start, Seconds = g.Seconds, Collections = g.Collections, Slices = new List<SliceStat>(g.Slices), Incl = new Dictionary<string, double>(g.Incl), Self = new Dictionary<string, double>(g.Self), WorkerIncl = new Dictionary<string, double>(g.WorkerIncl), ThreadMs = new Dictionary<string, double>(g.ThreadMs) };
        var saved = segment; segment = copy; CloseSegment(); segment = saved;
    }

    // ---- labelled segments -------------------------------------------------------------------------------------------
    sealed class SegmentAcc
    {
        public string Label = "", Start = ""; public double Seconds;
        public Dictionary<string, double> Incl = new(), Self = new(), WorkerIncl = new(), ThreadMs = new();
        public List<SliceStat> Slices = new(); public int Collections;
    }
    SegmentAcc segment;

    void CheckMark(string modPattern)
    {
        string mark = livePath + ".mark";
        if (!File.Exists(mark)) return;
        string label;
        try { label = File.ReadAllText(mark).Trim(); File.Delete(mark); } catch { return; }
        CloseSegment();
        segment = new SegmentAcc { Label = label.Length > 0 ? label : "segment", Start = DateTime.Now.ToString("HH:mm:ss") };
    }

    void CloseSegment()
    {
        var g = segment; segment = null;
        if (g == null || g.Seconds < 1) return;
        var s = new SegmentStat { Label = g.Label, Start = g.Start, Seconds = g.Seconds, Slices = g.Slices.Count, Collections = g.Collections };
        double frames = g.Slices.Sum(x => x.Frames);
        s.AvgFps = frames / Math.Max(0.001, g.Slices.Sum(x => x.Frames * x.AvgMs) / 1000.0);
        s.MedianMs = g.Slices.Count > 0 ? g.Slices.Select(x => x.MedianMs).OrderBy(x => x).ElementAt(g.Slices.Count / 2) : 0;
        s.P99Ms = g.Slices.Count > 0 ? g.Slices.Select(x => x.P99Ms).OrderBy(x => x).ElementAt(g.Slices.Count / 2) : 0;
        s.WorstMs = g.Slices.Count > 0 ? g.Slices.Max(x => x.WorstMs) : 0;
        s.SlicesUnder30Fps = g.Slices.Count(x => 1000.0 / Math.Max(1, x.AvgMs) < 30);
        List<KeyValuePair<string, double>> Top(Dictionary<string, double> d, Func<string, bool> keep = null) =>
            d.Where(kv => keep == null || keep(kv.Key)).OrderByDescending(kv => kv.Value).Take(40).Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value / g.Seconds)).ToList();
        s.Inclusive = Top(g.Incl); s.Self = Top(g.Self);
        s.Threads = Top(g.ThreadMs); s.WorkerInclusive = Top(g.WorkerIncl);
        s.Rebirth = Top(g.Incl, k => mod.Length > 0 && k.Contains(mod, StringComparison.OrdinalIgnoreCase));
        lock (result) result.Segments.Add(s);
    }
    /// <summary>Rows for the live summary panel: garbage collection first, then the biggest allocators.</summary>
    public List<(string Item, string Value)> SummaryRows()
    {
        var rows = new List<(string, string)>();
        lock (result)
        {
            rows.Add(("Recorded", $"{result.Seconds:F0} s, typical frame {result.MedianFrameMs:F0} ms"));
            rows.Add(("Dips found", result.Hitches.Count.ToString()));
            foreach (var li in result.LogIssues.Where(x => x.Count >= 5).Take(4))
                rows.Add(("LOG " + (li.Message.Length > 90 ? li.Message.Substring(0, 90) : li.Message), $"{li.Count}x ({li.PerMinute:F0} per minute)"));
            rows.Add(("Garbage collections", $"{result.Collections} ({result.CollectionsPerMinute:F1} per minute)"));
            if (result.HeapMB > 0) rows.Add(("Managed heap", $"{result.HeapMB:F0} MB ({result.HeapUsedMB:F0} MB in use)"));
            if (result.AllocSeconds > 0)
            {
                double total = result.AllocByType.Values.Sum() / result.AllocSeconds / 1024;
                rows.Add(("Allocation", $"{total:F0} KB/s (sampled)"));
                foreach (var kv in result.AllocBySite.OrderByDescending(x => x.Value).Take(12))
                    rows.Add(("  " + (kv.Key.Length > 0 ? kv.Key : "(no readable stack)"), $"{kv.Value / result.AllocSeconds / 1024:F0} KB/s"));
            }
            else rows.Add(("Allocation", "turn on \"Track allocations\" before launching to see the allocators"));
        }
        return rows;
    }

    void Commit(SliceTotals s)
    {
        lock (result) CommitLocked(s);
    }

    void CommitLocked(SliceTotals s)
    {
        if (s.Collections > 0) { result.Collections += s.Collections; result.CollectionTimes.Add(s.Time + (s.Collections > 1 ? $" x{s.Collections}" : "")); }
        if (s.HasAlloc)
        {
            result.AllocSeconds += s.Seconds;
            foreach (var kv in s.ByType) result.AllocByType[kv.Key] = result.AllocByType.GetValueOrDefault(kv.Key) + kv.Value;
            foreach (var kv in s.BySite) result.AllocBySite[kv.Key] = result.AllocBySite.GetValueOrDefault(kv.Key) + kv.Value;
        }
        if (s.Session > warmupSecondsForRate) result.CollectionsPerMinute = result.Collections / Math.Max(1, (s.Session - warmupSecondsForRate) / 60.0);
    }
    double warmupSecondsForRate;

    static readonly Regex WindowMethod = new(@"(XUiC_[A-Za-z0-9_]+|Rebirth[A-Za-z0-9_]*Window[A-Za-z0-9_]*):(OnOpen|OnClose|Init)\b", RegexOptions.Compiled);

    void Analyse(Profile p, List<double> frames, double session, DateTime end, bool warmup, string snap, DateTime start, CancellationToken ct)
    {
        var slice = new SliceTotals { Collections = warmup ? 0 : (int)Math.Max(0, p.HeapCollections), Time = end.ToLocalTime().ToString("HH:mm:ss"), Seconds = p.WallSec, Session = session };
        if (p.HeapBytes > 0) { result.HeapMB = p.HeapBytes / 1048576.0; result.HeapUsedMB = p.HeapUsedBytes / 1048576.0; }
        if (!warmup && p.TrackAlloc && p.Allocs.Count > 0)
        {
            slice.HasAlloc = true;
            foreach (var a in p.Allocs)
            {
                if (a.Type != null) slice.ByType[a.Type] = slice.ByType.GetValueOrDefault(a.Type) + a.Bytes;
                string site = null;
                var names = new List<string>();
                foreach (int fi in a.F)
                {
                    string name = p.Methods[fi].Short;
                    if (name.StartsWith("(wrapper")) continue;
                    if (name.Contains("Rebirth")) { site = "* " + name; break; }
                    names.Add(name);
                }
                // Innermost frames say what allocated; the outermost say who started it. Keep both ends of long stacks.
                string chain = names.Count <= 9 ? string.Join(" <- ", names)
                    : string.Join(" <- ", names.Take(3)) + " <- ... <- " + string.Join(" <- ", names.Skip(names.Count - 5));                site ??= chain;
                slice.BySite[site] = slice.BySite.GetValueOrDefault(site) + a.Bytes;
            }
        }
        pending.Enqueue(slice);
        while (pending.Count > TailSlices) Commit(pending.Dequeue());
        var rates = p.Methods.GroupBy(m => m.Short).ToDictionary(g => g.Key, g => g.Sum(m => p.MainMsPerSec(m.Incl)));
        if (segment != null && !warmup)
        {
            segment.Seconds += p.WallSec;
            foreach (var kv in rates) segment.Incl[kv.Key] = segment.Incl.GetValueOrDefault(kv.Key) + kv.Value * p.WallSec;
            foreach (var g in p.Methods.GroupBy(m => m.Short)) segment.Self[g.Key] = segment.Self.GetValueOrDefault(g.Key) + g.Sum(m => p.MainMsPerSec(m.Self)) * p.WallSec;
            segment.Collections += (int)Math.Max(0, p.HeapCollections);
            foreach (var st in p.Stacks)
            {
                if (st.Main) continue;
                double ms = p.OtherMsPerSec(st.Count) * p.WallSec;
                var seen = new HashSet<int>();
                foreach (int fi in st.F)
                {
                    if (!seen.Add(fi)) continue;
                    string nm = p.Methods[fi].Short;
                    if (nm.StartsWith("(wrapper")) continue;
                    segment.WorkerIncl[nm] = segment.WorkerIncl.GetValueOrDefault(nm) + ms;
                    if (nm.StartsWith("[thread")) segment.ThreadMs[nm] = segment.ThreadMs.GetValueOrDefault(nm) + ms;
                }
            }
        }
        if (frames.Count < 3) return;
        var sorted = frames.OrderBy(x => x).ToList();
        double median = sorted[sorted.Count / 2], worst = sorted[^1];
        var thisSlice = new SliceStat { Time = end.ToLocalTime().ToString("HH:mm:ss"), Frames = frames.Count, AvgMs = frames.Average(), MedianMs = median, P99Ms = sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(sorted.Count * 0.99) - 1)], WorstMs = worst, Collections = (int)Math.Max(0, p.HeapCollections) };
        lock (result) result.SliceStats.Add(thisSlice);
        if (segment != null && !warmup) segment.Slices.Add(thisSlice);
        // World loading is not a hitch: skip slices that contain load-only code, and the 30 s after them.
        if (rates.Keys.Any(k => LoadOnly.IsMatch(k))) lastLoadSec = session;
        if (session - lastLoadSec < 30) return;
        double slowLimit = Math.Max(median * 2.5, 45);
        bool dip = worst >= slowLimit && !warmup;
        if (!dip)
        {
            foreach (var (k, v) in rates) ema[k] = ema.TryGetValue(k, out var e) ? e * 0.9 + v * 0.1 : v;
            foreach (var k in ema.Keys.ToList()) if (!rates.ContainsKey(k)) ema[k] *= 0.9;
            medians.Add(median);
            result.MedianFrameMs = medians.OrderBy(x => x).ElementAt(medians.Count / 2);
            return;
        }
        var h = new PlayHitch
        {
            Time = end.ToLocalTime().ToString("HH:mm:ss"), SessionSec = session, BaselineMs = median, WorstMs = worst,
            SlowFrames = frames.Count(x => x > slowLimit), LostMs = frames.Where(x => x > slowLimit).Sum(x => x - median),
        };
        // Lowest 250 ms stretch inside the slice.
        double acc = 0, win = 0, worstAvg = 0; var q = new Queue<double>();
        foreach (double f in frames) { q.Enqueue(f); acc += f; while (acc > 250 && q.Count > 1) acc -= q.Dequeue(); if (acc >= 200) worstAvg = Math.Max(worstAvg, acc / q.Count); win = acc; }
        h.LowFps = worstAvg > 0 ? 1000 / worstAvg : 0;
        var causes = new List<HitchAnalysis.Culprit>();
        foreach (var (k, v) in rates)
        {
            double excess = (v - ema.GetValueOrDefault(k)) * p.WallSec;
            if (excess >= 3) causes.Add(new HitchAnalysis.Culprit(k, excess, mod.Length > 0 && k.Contains(mod, StringComparison.OrdinalIgnoreCase)));
        }
        long fromFt = start.ToFileTimeUtc(), toFt = end.ToFileTimeUtc();
        foreach (var g in p.GcEvents.Where(e => e.Start >= fromFt && e.Start <= toFt))
        { h.GcCount++; h.GcMs += g.Us / 1000.0; h.GcWorstMs = Math.Max(h.GcWorstMs, g.Us / 1000.0); }
        h.Collections = (int)Math.Max(0, p.HeapCollections);
        if (h.Collections > 0 && h.GcMs < 3) causes.Add(new HitchAnalysis.Culprit($"Garbage collection ({h.Collections} in this slice, pause not measured)", 0, false));
        if (h.GcMs >= 3) causes.Add(new HitchAnalysis.Culprit($"Garbage collection ({h.GcCount} in this slice, worst {h.GcWorstMs:F0} ms)", h.GcMs, false));
        h.Causes = causes.OrderByDescending(c => c.ExcessMs).Take(25).ToList();
        h.Hint = string.Join(", ", rates.Keys.Select(k => WindowMethod.Match(k)).Where(m => m.Success).Select(m => m.Value).Distinct().Take(6));
        try
        {
            h.SliceFile = Path.Combine(dir, $"dip_{end.ToLocalTime():HHmmss}.json");
            File.Copy(snap, h.SliceFile, true);
        }
        catch { h.SliceFile = ""; }
        lock (result) result.Hitches.Add(h);
        Dip?.Invoke(h);
    }
}
