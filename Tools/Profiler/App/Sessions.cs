using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RebirthProfiler;

/// <summary>Describes one saved session (a profile or a benchmark run) and the code it was taken with.</summary>
sealed class SessionMeta
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Created { get; set; } = "";
    public string Kind { get; set; } = "";              // "profile" | "benchmark" | "play" (a Play recording with labelled segments)
    public string DataFile { get; set; } = "";          // file name inside the sessions folder
    public string GitCommit { get; set; } = "";
    public string GitBranch { get; set; } = "";
    public int ModifiedFiles { get; set; }              // uncommitted changes under Scripts/ and Config/ when the session was taken
    public string ModDllHash { get; set; } = "";
    public string ModDllTime { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public string Machine { get; set; } = "";
    public string Summary { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public string Dir { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public string DataPath => Path.Combine(Dir, DataFile);
    public string CodeVersion => (GitCommit.Length > 0 ? GitCommit + (ModifiedFiles > 0 ? $"+{ModifiedFiles} changed" : "") : "?") + (ModDllHash.Length > 0 ? "  dll " + ModDllHash : "");
}

static class SessionStore
{
    public static string Dir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "sessions"));
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static List<SessionMeta> List()
    {
        var list = new List<SessionMeta>();
        if (!Directory.Exists(Dir)) return list;
        foreach (var f in Directory.GetFiles(Dir, "*.session.json"))
        {
            try
            {
                var m = JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(f), Opt);
                if (m == null) continue;
                m.Dir = Dir;
                if (File.Exists(m.DataPath)) list.Add(m);
            }
            catch { }
        }
        return list.OrderByDescending(m => m.Created).ToList();
    }

    static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, ".git")) || File.Exists(Path.Combine(d.FullName, ".git"))) return d.FullName;
        return null;
    }

    static string Git(string root, string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", $"-C \"{root}\" {args}") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var p = Process.Start(psi)!;
            string o = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } return ""; }
            return o.Trim();
        }
        catch { return ""; }
    }

    static string GameVersion()
    {
        try
        {
            string log = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? "", "AppData", "LocalLow", "The Fun Pimps", "7 Days To Die", "Player.log");
            using var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var r = new StreamReader(fs);
            for (int i = 0; i < 400 && !r.EndOfStream; i++)
            {
                string line = r.ReadLine() ?? "";
                int at = line.IndexOf("Version:", StringComparison.OrdinalIgnoreCase);
                if (at >= 0 && line.Contains("Compatibility", StringComparison.OrdinalIgnoreCase) == false && line.Length < 200) return line[(at + 8)..].Trim();
            }
        }
        catch { }
        return "";
    }

    /// <summary>Copies the data file into the library with an automatic description of the code that produced it.</summary>
    public static SessionMeta Save(string sourceFile, string kind, string label, string notes, string summary = "", string machine = "", bool captureCode = true)
    {
        Directory.CreateDirectory(Dir);
        string id = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + new string(label.Where(char.IsLetterOrDigit).Take(20).ToArray());
        while (File.Exists(Path.Combine(Dir, id + ".session.json"))) id += "x";   // never overwrite an existing session
        string dataName = id + (kind == "profile" ? ".profile.json" : kind == "play" ? ".play.json" : ".bench.json");
        File.Copy(sourceFile, Path.Combine(Dir, dataName), true);

        var m = new SessionMeta
        {
            Id = id, Label = label, Notes = notes ?? "", Created = DateTime.Now.ToString("s"), Kind = kind, DataFile = dataName,
            Summary = summary, Machine = machine.Length > 0 ? machine : Environment.MachineName, GameVersion = GameVersion(), Dir = Dir,
        };
        string root = captureCode ? FindRoot() : null;   // imports of old data must not be stamped with today's code
        if (root != null)
        {
            m.GitCommit = Git(root, "rev-parse --short HEAD");
            m.GitBranch = Git(root, "rev-parse --abbrev-ref HEAD");
            string status = Git(root, "status --porcelain -- Scripts Config");
            m.ModifiedFiles = status.Length == 0 ? 0 : status.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            string dll = Path.Combine(root, "RebirthUtils.dll");
            if (File.Exists(dll))
            {
                using var sha = SHA256.Create();
                using var fs = new FileStream(dll, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                m.ModDllHash = Convert.ToHexString(sha.ComputeHash(fs))[..10].ToLowerInvariant();
                m.ModDllTime = File.GetLastWriteTime(dll).ToString("s");
            }
        }
        File.WriteAllText(Path.Combine(Dir, id + ".session.json"), JsonSerializer.Serialize(m, Opt));
        return m;
    }

    public static void Delete(SessionMeta m)
    {
        try { File.Delete(m.DataPath); } catch { }
        try { File.Delete(Path.Combine(Dir, m.Id + ".session.json")); } catch { }
    }

    public static void UpdateNotes(SessionMeta m)
    {
        File.WriteAllText(Path.Combine(Dir, m.Id + ".session.json"), JsonSerializer.Serialize(m, Opt));
    }
}

// ---- comparison of two groups of sessions ------------------------------------------------------------------------------

static class SessionCompare
{
    public sealed record DiffRow(string Item, double A, double B, string Status);

    /// <summary>Compares named values between two groups of sessions (mean over each group; a value missing from a session counts as 0).</summary>
    public static List<DiffRow> Diff(List<Dictionary<string, double>> groupA, List<Dictionary<string, double>> groupB, double epsilon)
    {
        var keys = new HashSet<string>();
        foreach (var d in groupA) foreach (var k in d.Keys) keys.Add(k);
        foreach (var d in groupB) foreach (var k in d.Keys) keys.Add(k);
        var rows = new List<DiffRow>();
        foreach (var k in keys)
        {
            var av = groupA.Select(d => d.GetValueOrDefault(k)).ToArray();
            var bv = groupB.Select(d => d.GetValueOrDefault(k)).ToArray();
            double a = av.Length == 0 ? 0 : av.Average(), b = bv.Length == 0 ? 0 : bv.Average();
            if (Math.Abs(a) < epsilon && Math.Abs(b) < epsilon) continue;
            double noise = Math.Max(av.Length > 1 ? av.Max() - av.Min() : 0, bv.Length > 1 ? bv.Max() - bv.Min() : 0);
            string status;
            if (a < epsilon) status = "new";
            else if (b < epsilon) status = "removed";
            else if (Math.Abs(b - a) <= Math.Max(noise, Math.Abs(a) * 0.05)) status = (av.Length > 1 || bv.Length > 1) ? "within noise" : "same";
            else status = b < a ? "lower" : "higher";
            rows.Add(new DiffRow(k, a, b, status));
        }
        return rows.OrderByDescending(r => Math.Abs(r.B - r.A)).ToList();
    }

    public static DataTable ToTable(string itemHeader, string unit, List<DiffRow> rows, string filter, int limit = 500)
    {
        var t = new DataTable();
        t.Columns.Add(itemHeader, typeof(string));
        t.Columns.Add("A (" + unit + ")", typeof(double));
        t.Columns.Add("B (" + unit + ")", typeof(double));
        t.Columns.Add("Change (" + unit + ")", typeof(double));
        t.Columns.Add("Change %", typeof(double));
        t.Columns.Add("Status", typeof(string));
        foreach (var r in rows.Where(r => filter.Length == 0 || r.Item.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(limit))
            t.Rows.Add(r.Item, Math.Round(r.A, 3), Math.Round(r.B, 3), Math.Round(r.B - r.A, 3), r.A > 0 ? Math.Round(100 * (r.B - r.A) / r.A, 1) : 0.0, r.Status);
        return t;
    }

    public static string ToText(string title, string unit, List<DiffRow> rows, string filter, int limit = 60)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"== {title} ({unit}) ==");
        foreach (var r in rows.Where(r => filter.Length == 0 || r.Item.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(limit))
            sb.AppendLine($"  {r.A,10:F2} -> {r.B,10:F2}   {r.B - r.A,+10:F2}   {r.Status,-13} {r.Item}");
        sb.AppendLine();
        return sb.ToString();
    }


    /// <summary>Full text comparison of two groups of saved sessions (same content as the Sessions tab).</summary>
    public static string ReportText(List<SessionMeta> a, List<SessionMeta> b, string mod)
    {
        var sb = new StringBuilder();
        sb.AppendLine("A: " + (a.Count == 0 ? "(none)" : string.Join(" | ", a.Select(s => $"{s.Label} [{s.CodeVersion}]"))));
        sb.AppendLine("B: " + (b.Count == 0 ? "(none)" : string.Join(" | ", b.Select(s => $"{s.Label} [{s.CodeVersion}]"))));
        sb.AppendLine();
        if (a.Count == 0 || b.Count == 0) return sb.AppendLine("Choose at least one session per side.").ToString();
        if (a.Concat(b).Select(s => s.Kind).Distinct().Count() > 1) return sb.AppendLine("A and B must be the same kind.").ToString();
        if (a[0].Kind == "play")
        {
            var ra = PlayResult.Load(a[0].DataPath); var rb = PlayResult.Load(b[0].DataPath);
            sb.AppendLine($"{"scenario",-28} {"A fps",6} {"B fps",6} {"change",7} {"A ms",6} {"B ms",6} {"A p99",6} {"B p99",6}");
            foreach (var l in ra.Segments.Select(s => s.Label).Union(rb.Segments.Select(s => s.Label)).Where(l => !l.EndsWith("(running)") && !l.StartsWith("end")))
            {
                var sa = ra.Segments.FirstOrDefault(s => s.Label == l); var sbb = rb.Segments.FirstOrDefault(s => s.Label == l);
                string ch = sa != null && sbb != null && sa.AvgFps > 0 ? $"{(sbb.AvgFps - sa.AvgFps) / sa.AvgFps * 100:+0;-0}%" : "";
                sb.AppendLine($"{l,-28} {sa?.AvgFps.ToString("F0"),6} {sbb?.AvgFps.ToString("F0"),6} {ch,7} {sa?.MedianMs.ToString("F1"),6} {sbb?.MedianMs.ToString("F1"),6} {sa?.P99Ms.ToString("F0"),6} {sbb?.P99Ms.ToString("F0"),6}");
            }
            sb.AppendLine($"collections per minute: A {ra.CollectionsPerMinute:F1}, B {rb.CollectionsPerMinute:F1}; dips: A {ra.Hitches.Count}, B {rb.Hitches.Count}");
            return sb.ToString();
        }
        if (a[0].Kind == "benchmark")
            return sb.Append(BenchCompare.ToText(a.Select(s => BenchResult.Load(s.DataPath)).ToList(), b.Select(s => BenchResult.Load(s.DataPath)).ToList())).ToString();
        var pa = a.Select(s => Profile.Load(s.DataPath)).ToList();
        var pb = b.Select(s => Profile.Load(s.DataPath)).ToList();
        sb.Append(ToText("Rebirth cost by entry point", "ms/s", Diff(pa.Select(p => RebirthCost(p, mod)).ToList(), pb.Select(p => RebirthCost(p, mod)).ToList(), 0.005), "", 40));
        if (pa.Concat(pb).Any(p => p.Allocs.Count > 0))
            sb.Append(ToText("Garbage by Rebirth code", "KB/s", Diff(pa.Select(p => Allocations(p, mod)).ToList(), pb.Select(p => Allocations(p, mod)).ToList(), 0.5), "", 40));
        sb.Append(ToText("Methods: inclusive CPU share", "% of samples", Diff(pa.Select(MethodInclusive).ToList(), pb.Select(MethodInclusive).ToList(), 0.01), mod, 40));
        return sb.ToString();
    }
    // value extractors ------------------------------------------------------------------------------------------------------
    public static Dictionary<string, double> RebirthCost(Profile p, string mod)
    {
        var mask = Analyzer.ModMask(p, mod);
        return Analyzer.EntryCosts(p, mask).GroupBy(r => r.Entry).ToDictionary(g => g.Key, g => g.Sum(r => r.MainMs));
    }
    public static Dictionary<string, double> Allocations(Profile p, string mod)
    {
        var mask = Analyzer.ModMask(p, mod);
        return Analyzer.AllocByCode(p, mask).Where(r => !r.Code.StartsWith("(")).GroupBy(r => r.Code).ToDictionary(g => g.Key, g => g.Sum(r => r.KBps));
    }
    public static Dictionary<string, double> MethodInclusive(Profile p)
        => p.Methods.GroupBy(m => m.Short).ToDictionary(g => g.Key, g => g.Sum(m => p.Pct(m.Incl)));
    public static Dictionary<string, double> MethodSelf(Profile p)
        => p.Methods.GroupBy(m => m.Short).ToDictionary(g => g.Key, g => g.Sum(m => p.Pct(m.Self)));
}

