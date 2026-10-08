using System.Text.Json;
using System.Text.RegularExpressions;

namespace RebirthProfiler;

sealed class Method
{
    public string Full, Cls, Name, Sig;
    public long Self, Incl;
    public string Short => Cls + ":" + Name;
}

/// <summary>One distinct call stack (innermost frame first) with how often it was sampled.</summary>
sealed class StackSample
{
    public int[] F;
    public long Count, Bytes;
    public bool Main;
    public string Type;
}

sealed class Profile
{
    public long Total, NoManaged, MainTotal, MainNoManaged, WallMs, AllocSeen;
    public int Hz = 500;
    public bool TrackAlloc;
    public List<Method> Methods = new();
    public List<StackSample> Stacks = new(), Allocs = new();
    public string Path;
    public long OtherSamples, OtherNative;      // worker-thread samples (only when the game was started with worker-thread sampling)
    public int OtherHz = 100;
    /// <summary>Milliseconds of a worker thread time per second of wall time that <paramref name="count"/> samples stand for.</summary>
    public double OtherMsPerSec(long count) => count * (1000.0 / Math.Max(1, OtherHz)) / WallSec;
    public long GcCount, GcTotalUs, GcMaxUs;
    public long HeapCollections = -1, HeapBytes = -1, HeapUsedBytes = -1;   // Mono's own counters: every collection, whatever triggered it                       // garbage collections since the counters were reset
    public List<(long Start, long Us, int Gen)> GcEvents = new();  // latest ones: FILETIME start, pause in microseconds, generation

    public double WallSec => Math.Max(WallMs / 1000.0, 0.001);
    public double Pct(long n) => Total == 0 ? 0 : 100.0 * n / Total;
    /// <summary>Milliseconds per second of wall time the main thread spent in <paramref name="count"/> samples.</summary>
    public double MainMsPerSec(long count) => MainTotal == 0 ? 0 : 1000.0 * count / MainTotal;

    public static Profile Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var r = doc.RootElement;
        var p = new Profile { Path = path, Total = r.GetProperty("total").GetInt64() };
        long L(string k) => r.TryGetProperty(k, out var v) ? v.GetInt64() : 0;
        p.NoManaged = L("noManaged"); p.MainTotal = L("mainTotal"); p.MainNoManaged = L("mainNoManaged");
        p.WallMs = L("wallMs"); p.AllocSeen = L("allocSeen");
        if (r.TryGetProperty("hz", out var hz)) p.Hz = hz.GetInt32();
        p.TrackAlloc = L("trackAlloc") == 1;
        foreach (var m in r.GetProperty("methods").EnumerateArray())
        {
            var full = m.GetProperty("n").GetString() ?? "?";
            Split(full, out var cls, out var name, out var sig);
            p.Methods.Add(new Method { Full = full, Cls = cls, Name = name, Sig = sig, Self = m.GetProperty("self").GetInt64(), Incl = m.GetProperty("incl").GetInt64() });
        }
        p.Stacks = ReadStacks(r, "stacks");
        p.Allocs = ReadStacks(r, "allocs");
        if (r.TryGetProperty("other", out var ot)) { p.OtherHz = ot.GetProperty("hz").GetInt32(); p.OtherSamples = ot.GetProperty("samples").GetInt64(); p.OtherNative = ot.GetProperty("native").GetInt64(); }
        if (r.TryGetProperty("heap", out var hp)) { p.HeapCollections = hp.GetProperty("collections").GetInt64(); p.HeapBytes = hp.GetProperty("heapBytes").GetInt64(); p.HeapUsedBytes = hp.GetProperty("usedBytes").GetInt64(); }
        if (r.TryGetProperty("gc", out var gc))
        {
            p.GcCount = gc.GetProperty("count").GetInt64(); p.GcTotalUs = gc.GetProperty("totalUs").GetInt64(); p.GcMaxUs = gc.GetProperty("maxUs").GetInt64();
            foreach (var e in gc.GetProperty("events").EnumerateArray()) { var a = e.EnumerateArray().ToArray(); p.GcEvents.Add(((long)a[0].GetUInt64(), a[1].GetInt64(), a[2].GetInt32())); }
        }
        return p;
    }

    static List<StackSample> ReadStacks(JsonElement r, string key)
    {
        var list = new List<StackSample>();
        if (!r.TryGetProperty(key, out var arr)) return list;
        foreach (var s in arr.EnumerateArray())
        {
            var f = s.GetProperty("f").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            list.Add(new StackSample
            {
                F = f, Count = s.GetProperty("c").GetInt64(), Main = s.GetProperty("m").GetInt32() != 0,
                Bytes = s.TryGetProperty("b", out var b) ? b.GetInt64() : 0,
                Type = s.TryGetProperty("t", out var t) ? t.GetString() : null,
            });
        }
        return list;
    }

    // mono_method_full_name: "Namespace.Class:Method (params)", possibly preceded by a return type
    static void Split(string full, out string cls, out string name, out string sig)
    {
        var s = full;
        int sp = s.IndexOf(' ');
        int colon0 = s.IndexOf(':');
        if (sp > 0 && colon0 > sp && s.IndexOf('(') > colon0) s = s[(sp + 1)..];
        int paren = s.IndexOf(" (", StringComparison.Ordinal);
        sig = paren >= 0 ? s[paren..] : "";
        var head = paren >= 0 ? s[..paren] : s;
        int colon = head.LastIndexOf(':');
        cls = colon >= 0 ? head[..colon] : "(unknown)";
        name = colon >= 0 ? head[(colon + 1)..] : head;
    }
}

sealed class Finding
{
    public string Severity, Category, Code, Entry, Trigger, Advice;
    public double MsPerSec, OtherPct, AllocKBps;
    public double Score;
}

sealed class CostRow
{
    public string Entry, Callees;
    public double MainMs, OtherPct;
    public string Key;
}

sealed class AllocRow
{
    public string Code, Types, Entry;
    public double KBps;
}

static class Analyzer
{
    // Known-expensive patterns. Each is matched against the methods a mod method calls (between it and the leaf).
    static readonly (string Category, Regex Rx, string Advice)[] Rules =
    {
        ("Component lookup", R(@"GameObject:GetComponent|Component:GetComponent|GetComponentsInternal|GetComponentInChildren|GetComponentInParent|GetComponents"),
            "Looks components up repeatedly. Cache the reference once (on creation or first use) instead of every call."),
        ("Scene search", R(@"Object:FindObjectOfType|Object:FindObjectsOfType|Object:FindObjectsByType|Object:FindAnyObjectByType|GameObject:Find|GameObject:FindWithTag|GameObject:FindGameObjectsWithTag|Transform:Find"),
            "Searches the scene by name/type. Never do this per frame: find once and keep the reference."),
        ("Camera.main", R(@"Camera:get_main"),
            "Camera.main does a tag search each time. Cache the camera."),
        ("Reflection", R(@"MethodBase:Invoke|RuntimeMethodInfo:Invoke|Type:GetMethod|Type:GetField|Type:GetProperty|Activator:CreateInstance|FieldInfo:GetValue|FieldInfo:SetValue|RuntimeFieldInfo:|Type:GetType|AccessTools|Traverse:"),
            "Reflection on a hot path. Resolve once and cache a delegate, or access the member directly."),
        ("LINQ / sorting", R(@"System\.Linq\.|Enumerable:|Array:Sort|List`1<[^>]*>:Sort|Enumerable`"),
            "LINQ and sorting allocate and are slow in per-frame code. Use plain loops, reuse lists, and sort only when the data changes."),
        ("String building", R(@"string:Concat|String:Concat|string:Format|String:Format|string:Join|string:Split|string:Replace|string:ToLower|string:ToUpper|string:Substring|StringBuilder:"),
            "Builds strings every call. Only rebuild text when its inputs change and cache the result."),
        ("Physics query", R(@"Physics:Raycast|Physics:SphereCast|Physics:OverlapSphere|Physics:OverlapBox|Physics:CapsuleCast|Physics:CheckSphere|Physics:BoxCast|PhysicsScene:Query|PhysicsScene:Raycast"),
            "Physics queries are expensive. Throttle them (not every frame), use layer masks, and prefer the NonAlloc variants."),
        ("World/entity scan", R(@"GetEntitiesInBounds|GetEntitiesAround|World:GetClosestPlayer|World:GetPlayers|GetPrimaryPlayer|EntityFactory:|FindNearest|World:GetEntity"),
            "Scans entities or the world. Throttle to every N ticks and cache the result."),
        ("UI binding refresh", R(@"BindingInfo:RefreshValue|GetBindingValueInternal|XUiController:RefreshBindings"),
            "XUi bindings are re-evaluated frequently. Keep GetBindingValue cheap: return cached values and avoid formatting or lookups inside it."),
        ("File / XML / IO", R(@"System\.IO\.File|XmlDocument|XmlReader|XDocument|StreamReader|StreamWriter|JsonUtility|File:Read|File:Write"),
            "Disk or XML work during gameplay. Load once at startup and cache."),
        ("Regex", R(@"System\.Text\.RegularExpressions"),
            "Regex in a hot path. Use a static precompiled Regex, or plain string checks."),
        ("Instantiate / Destroy", R(@"Object:Instantiate|Object:Destroy|Object:DestroyImmediate"),
            "Creates/destroys objects at runtime. Pool and reuse them."),
        ("Renderer material", R(@"Renderer:get_material |Renderer:get_materials|Renderer:get_material\b"),
            "Renderer.material clones the material each call. Use sharedMaterial or a MaterialPropertyBlock."),
        ("List lookup", R(@"List`1<[^>]*>:Contains|List`1<[^>]*>:IndexOf|List`1<[^>]*>:Remove |Array:IndexOf"),
            "Linear search in a list. Use a HashSet or Dictionary for repeated lookups."),
        ("Logging", R(@"UnityEngine\.Debug:Log|^Log:Out|^Log:Warning|^Log:Error|Logger:Log"),
            "Logging in a gameplay path. Remove it or guard it behind a debug flag."),
        ("GC / unload", R(@"GC:Collect|Resources:UnloadUnusedAssets"),
            "Forces a garbage collection or asset unload, which stalls the frame. Avoid outside of loading screens."),
    };

    static Regex R(string p) => new(p, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool[] ModMask(Profile p, string pattern)
    {
        var mask = new bool[p.Methods.Count];
        if (string.IsNullOrWhiteSpace(pattern)) return mask;
        // The Game Bridge is test tooling, not mod behaviour: keep it out of the results.
        for (int i = 0; i < mask.Length; i++)
            mask[i] = p.Methods[i].Full.Contains(pattern.Trim(), StringComparison.OrdinalIgnoreCase) && !p.Methods[i].Full.Contains("GameBridge", StringComparison.OrdinalIgnoreCase);
        return mask;
    }

    static string Sev(double msPerSec) => msPerSec >= 10 ? "High" : msPerSec >= 2 ? "Medium" : "Low";

    static string TopName(Profile p, Dictionary<int, long> d) => d.Count == 0 ? "" : p.Methods[d.OrderByDescending(x => x.Value).First().Key].Short;

    static int Innermost(int[] f, bool[] mod) { for (int i = 0; i < f.Length; i++) if (mod[f[i]]) return i; return -1; }
    static int Outermost(int[] f, bool[] mod) { for (int i = f.Length - 1; i >= 0; i--) if (mod[f[i]]) return i; return -1; }

    /// <summary>For every point where the game enters mod code: how much time everything below it costs.</summary>
    public static List<CostRow> EntryCosts(Profile p, bool[] mod)
    {
        var agg = new Dictionary<int, (long main, long other, Dictionary<int, long> leafMain, Dictionary<int, long> leafOther)>();
        foreach (var s in p.Stacks)
        {
            int k = Outermost(s.F, mod);
            if (k < 0) continue;
            int e = s.F[k];
            if (!agg.TryGetValue(e, out var a)) a = (0, 0, new(), new());
            var leafMap = s.Main ? a.leafMain : a.leafOther;
            leafMap[s.F[0]] = leafMap.GetValueOrDefault(s.F[0]) + s.Count;
            agg[e] = s.Main ? (a.main + s.Count, a.other, a.leafMain, a.leafOther) : (a.main, a.other + s.Count, a.leafMain, a.leafOther);
        }
        var rows = new List<CostRow>();
        foreach (var (e, a) in agg)
        {
            var leaves = a.main > 0 ? a.leafMain : a.leafOther;
            var top = leaves.OrderByDescending(x => x.Value).Take(3)
                .Select(x => $"{p.Methods[x.Key].Short} ({(a.main > 0 ? p.MainMsPerSec(x.Value).ToString("F2") + " ms/s" : p.Pct(x.Value).ToString("F2") + "%")})");
            rows.Add(new CostRow
            {
                Entry = p.Methods[e].Short, Key = p.Methods[e].Full, MainMs = p.MainMsPerSec(a.main), OtherPct = p.Pct(a.other),
                Callees = string.Join("; ", top),
            });
        }
        return rows.OrderByDescending(r => r.MainMs).ThenByDescending(r => r.OtherPct).ToList();
    }

    /// <summary>Concrete, ranked observations about mod code: expensive calls it makes, its biggest cost centres and its garbage.</summary>
    public static List<Finding> Findings(Profile p, bool[] mod)
    {
        var list = new List<Finding>();

        // 1. Known-expensive calls made (directly or indirectly) by a mod method.
        var hits = new Dictionary<(int mod, int rule), (long main, long other, Dictionary<int, long> trig)>();
        var hitEntries = new Dictionary<(int mod, int rule), Dictionary<int, long>>();
        var directEntries = new Dictionary<int, Dictionary<int, long>>();
        // 2. Direct cost per mod method (its own code + everything it calls that is not mod code).
        var direct = new Dictionary<int, (long main, long other, long self)>();
        foreach (var s in p.Stacks)
        {
            int k = Innermost(s.F, mod);
            if (k < 0) continue;
            int m = s.F[k];
            int outer = s.F[Outermost(s.F, mod)];
            if (!directEntries.TryGetValue(m, out var de)) directEntries[m] = de = new Dictionary<int, long>();
            de[outer] = de.GetValueOrDefault(outer) + s.Count;
            var d = direct.GetValueOrDefault(m);
            direct[m] = (d.main + (s.Main ? s.Count : 0), d.other + (s.Main ? 0 : s.Count), d.self + (k == 0 ? s.Count : 0));
            if (k == 0) continue;
            var matched = new HashSet<int>();
            for (int i = 0; i < k; i++)
            {
                var name = p.Methods[s.F[i]].Full;
                for (int r = 0; r < Rules.Length; r++)
                {
                    if (matched.Contains(r) || !Rules[r].Rx.IsMatch(name)) continue;
                    matched.Add(r);
                    var key = (m, r);
                    if (!hitEntries.TryGetValue(key, out var he)) hitEntries[key] = he = new Dictionary<int, long>();
                    he[outer] = he.GetValueOrDefault(outer) + s.Count;
                    if (!hits.TryGetValue(key, out var h)) h = (0, 0, new Dictionary<int, long>());
                    h.trig[s.F[i]] = h.trig.GetValueOrDefault(s.F[i]) + s.Count;
                    hits[key] = (h.main + (s.Main ? s.Count : 0), h.other + (s.Main ? 0 : s.Count), h.trig);
                }
            }
        }
        foreach (var (key, h) in hits)
        {
            double ms = p.MainMsPerSec(h.main);
            list.Add(new Finding
            {
                Severity = Sev(ms), Category = Rules[key.rule].Category, Code = p.Methods[key.mod].Short, Entry = TopName(p, hitEntries[key]),
                Trigger = p.Methods[h.trig.OrderByDescending(x => x.Value).First().Key].Short,
                Advice = Rules[key.rule].Advice, MsPerSec = ms, OtherPct = p.Pct(h.other), Score = ms * 10 + p.Pct(h.other),
            });
        }
        foreach (var (m, d) in direct.OrderByDescending(x => x.Value.main).ThenByDescending(x => x.Value.other).Take(15))
        {
            double ms = p.MainMsPerSec(d.main);
            if (ms <= 0 && d.other == 0) continue;
            double selfMs = p.MainMsPerSec(d.self);
            list.Add(new Finding
            {
                Severity = Sev(ms), Category = "Cost centre", Code = p.Methods[m].Short, Entry = TopName(p, directEntries[m]),
                Trigger = $"self {selfMs:F2} ms/s, calls {Math.Max(ms - selfMs, 0):F2} ms/s",
                Advice = "One of the most expensive pieces of Rebirth code. Check how often it runs (every frame vs only on change) and whether its work can be cached, throttled or skipped when nothing changed.",
                MsPerSec = ms, OtherPct = p.Pct(d.other), Score = ms * 10 + p.Pct(d.other) - 0.001,
            });
        }

        // 3. Garbage produced by mod code.
        foreach (var row in AllocByCode(p, mod).Where(r => !r.Code.StartsWith("(") && r.KBps >= 5))
        {
            list.Add(new Finding
            {
                Severity = row.KBps >= 100 ? "High" : row.KBps >= 20 ? "Medium" : "Low", Category = "Allocation", Code = row.Code, Entry = row.Entry,
                Trigger = row.Types,
                Advice = "Allocates managed memory continuously; this feeds the garbage collector and causes periodic hitches. Reuse buffers/lists, pool objects, avoid LINQ, string building and boxing in this path.",
                AllocKBps = row.KBps, Score = row.KBps / 10,
            });
        }
        return list.OrderByDescending(f => f.Score).ToList();
    }

    public static List<AllocRow> AllocByCode(Profile p, bool[] mod)
    {
        var agg = new Dictionary<string, (long bytes, Dictionary<string, long> types, Dictionary<int, long> entries)>();
        foreach (var s in p.Allocs)
        {
            int k = Innermost(s.F, mod);
            string code = k >= 0 ? p.Methods[s.F[k]].Short : s.F.Length > 0 ? "(other) " + p.Methods[s.F[0]].Short : "(unknown)";
            if (!agg.TryGetValue(code, out var a)) a = (0, new Dictionary<string, long>(), new Dictionary<int, long>());
            string t = string.IsNullOrEmpty(s.Type) ? "?" : s.Type;
            a.types[t] = a.types.GetValueOrDefault(t) + s.Bytes;
            if (k >= 0) { int o = s.F[Outermost(s.F, mod)]; a.entries[o] = a.entries.GetValueOrDefault(o) + s.Bytes; }
            agg[code] = (a.bytes + s.Bytes, a.types, a.entries);
        }
        return agg.Select(x => new AllocRow
        {
            Code = x.Key, KBps = x.Value.bytes / 1024.0 / p.WallSec, Entry = TopName(p, x.Value.entries),
            Types = string.Join(", ", x.Value.types.OrderByDescending(t => t.Value).Take(3).Select(t => t.Key)),
        }).OrderByDescending(r => r.KBps).ToList();
    }

    public static List<AllocRow> AllocByType(Profile p)
    {
        return p.Allocs.GroupBy(s => string.IsNullOrEmpty(s.Type) ? "?" : s.Type)
            .Select(g => new AllocRow { Code = g.Key, KBps = g.Sum(s => s.Bytes) / 1024.0 / p.WallSec, Types = "" })
            .OrderByDescending(r => r.KBps).ToList();
    }
}

/// <summary>Plain-text version of the Findings / Rebirth cost / Allocations tabs (for before/after comparisons and CI).</summary>
static class ReportText
{
    public static string Build(Profile p, string modPattern)
    {
        var mod = Analyzer.ModMask(p, modPattern);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{Path.GetFileName(p.Path)}   {p.WallSec:F0} s measured, {p.Total:N0} samples ({p.MainTotal:N0} on the main thread), allocation tracking {(p.TrackAlloc ? "on" : "off")}");
        if (p.HeapCollections >= 0)
            sb.AppendLine($"Mono heap: {p.HeapCollections} collections in {p.WallSec:F0} s ({p.HeapCollections / p.WallSec * 60:F1} per minute), heap {p.HeapBytes / 1048576.0:F0} MB, in use {p.HeapUsedBytes / 1048576.0:F0} MB");
        if (p.GcCount > 0 || p.GcEvents.Count > 0)
            sb.AppendLine($"Garbage collection: {p.GcCount} collections, {p.GcTotalUs / 1000.0:F0} ms paused in total ({p.GcTotalUs / 1000.0 / p.WallSec:F1} ms/s), worst pause {p.GcMaxUs / 1000.0:F0} ms");
        sb.AppendLine();
        sb.AppendLine("== ALLOCATIONS by Rebirth code (KB/s) ==");
        foreach (var r in Analyzer.AllocByCode(p, mod).Where(r => !r.Code.StartsWith("(")).Take(25))
            sb.AppendLine($"  {r.KBps,8:F1}  {r.Code,-70} <- {r.Entry}   [{r.Types}]");
        sb.AppendLine($"  total allocation rate, all code: {Analyzer.AllocByCode(p, mod).Sum(r => r.KBps):F0} KB/s");
        sb.AppendLine();
        sb.AppendLine("== REBIRTH COST (main-thread ms per second, entry points) ==");
        foreach (var r in Analyzer.EntryCosts(p, mod).Take(20))
            sb.AppendLine($"  {r.MainMs,7:F2}  {r.Entry}");
        sb.AppendLine($"  total: {Analyzer.EntryCosts(p, mod).Sum(r => r.MainMs):F2} ms/s");
        sb.AppendLine();
        sb.AppendLine("== FINDINGS ==");
        foreach (var f in Analyzer.Findings(p, mod).Take(30))
            sb.AppendLine($"  [{f.Severity,-6}] {f.Category,-18} {f.Code,-60} {f.MsPerSec,6:F2} ms/s {f.AllocKBps,8:F1} KB/s  <- {f.Entry}");
        return sb.ToString();
    }
}
