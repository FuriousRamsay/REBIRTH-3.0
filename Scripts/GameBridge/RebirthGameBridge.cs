using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// REBIRTH Game Bridge: a localhost-only HTTP endpoint that lets external tooling (Codex, scripts)
/// drive and inspect a running game for automated testing.
///
/// Hard-gated: the listener only starts when the game is launched with "-rebirthbridge" or
/// "-rebirthbridge=PORT". Without that argument this class does nothing at all.
///
/// Threading: HTTP requests arrive on listener/thread-pool threads and are queued to the Unity main
/// thread (RebirthGameBridgePump.Update). Only /ping is answered off-thread, from state cached each frame.
/// </summary>
[Preserve]
public sealed class RebirthGameBridgeModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        RebirthGameBridge.TryStart(modInstance);
    }
}

public static class RebirthGameBridge
{
    public const string Version = "1.0.0";
    public const int DefaultPort = 8765;
    private const string LaunchArg = "-rebirthbridge";
    private const string TokenHeader = "X-Bridge-Token";

    internal static readonly ConcurrentQueue<BridgeRequest> Pending = new ConcurrentQueue<BridgeRequest>();
    internal static string OutputDir;
    internal static string SessionFile;

    // Cached by the pump every frame so /ping can answer while the main thread is busy loading.
    internal static volatile string CachedState = "starting";
    internal static long LastFrameTicks = DateTime.UtcNow.Ticks;
    internal static float LastRequestAt = -100f;
    /// <summary>Seconds since the agent last sent a (non-ping) request; used to tell "idle" from "being driven".</summary>
    public static float LastRequestAge { get { return Time.realtimeSinceStartup - LastRequestAt; } }

    private static bool started;
    private static HttpListener listener;
    private static string token;
    private static int port;

    public static bool IsRunning { get { return listener != null && listener.IsListening; } }

    public static void TryStart(Mod mod)
    {
        if (started) return;
        started = true;

        int? requestedPort = ReadPortFromCommandLine();
        if (!requestedPort.HasValue) return; // not enabled: zero footprint

        // Tests that top up food, water and health themselves start with the eating/drinking/crafting chores OFF, so nothing is wasted at
        // every launch (set REBIRTH_BRIDGE_NOCHORES=1 in the launching environment; the game inherits it).
        // Human-play recording: no automation of any kind (no guard instincts, no reflexes, no chores); only the probe watches.
        if (Environment.GetEnvironmentVariable("REBIRTH_BRIDGE_HUMAN") == "1")
        {
            RebirthGameBridgeMeleeProbe.Human = true;
            RebirthGameBridgeCombat.GuardEnabled = false;
            RebirthGameBridgeCombat.BleedReflexEnabled = false;
            RebirthGameBridgeNeeds.Enabled = false;
            RebirthGameBridgeAgenda.Enabled = false;
        }
        if (Environment.GetEnvironmentVariable("REBIRTH_BRIDGE_NOCHORES") == "1")
        {
            RebirthGameBridgeNeeds.Enabled = false;
            RebirthGameBridgeAgenda.Enabled = false;
        }

        try
        {
            port = requestedPort.Value;
            string modPath = mod != null ? mod.Path : Path.GetDirectoryName(typeof(RebirthGameBridge).Assembly.Location);
            OutputDir = Path.Combine(Path.Combine(modPath, "Tools"), Path.Combine("GameBridge", "out"));
            Directory.CreateDirectory(OutputDir);
            SessionFile = Path.Combine(OutputDir, "session.json");

            RebirthGameBridgeLog.Install();
            RebirthGameBridgePump.Create();
            RebirthGameBridgeInput.Install(new Harmony("rebirth.gamebridge"));
            RebirthGameBridgeMeleeProbe.Install(new Harmony("rebirth.gamebridge.meleeprobe"));

            token = Guid.NewGuid().ToString("N");
            // Loopback only. Never bind to a wildcard/external address. If the port is taken (a previous game instance still holds it) or reserved by
            // Windows, try the next ones: the session file tells clients the port actually used.
            int firstPort = port; Exception lastEx = null; bool bound = false;
            for (int attempt = 0; attempt < 12 && !bound; attempt++)
            {
                port = firstPort + attempt;
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                    listener.Start();
                    bound = true;
                }
                catch (Exception bindEx) { lastEx = bindEx; try { listener.Close(); } catch { } Log.Warning("[REBIRTH GameBridge] port " + port + " not available: " + bindEx.Message); }
            }
            if (!bound) throw lastEx ?? new Exception("no free port");

            var thread = new Thread(ListenLoop) { IsBackground = true, Name = "RebirthGameBridge" };
            thread.Start();

            WriteSessionFile();
            Application.quitting += Stop;
            Log.Out("[REBIRTH GameBridge] v" + Version + " listening on http://127.0.0.1:" + port + "/ (session: " + SessionFile + ")");
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH GameBridge] Failed to start on port " + port + ": " + ex.Message);
            Stop();
        }
    }

    public static void Stop()
    {
        try { if (listener != null) { listener.Close(); } } catch { }
        listener = null;
        // Only delete our own session file (a newer game may already have written its own).
        try
        {
            if (SessionFile != null && File.Exists(SessionFile)
                && File.ReadAllText(SessionFile).Contains("\"pid\": " + System.Diagnostics.Process.GetCurrentProcess().Id + ","))
                File.Delete(SessionFile);
        }
        catch { }
    }

    private static int? ReadPortFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i] ?? string.Empty;
            if (a.Equals(LaunchArg, StringComparison.OrdinalIgnoreCase)) return DefaultPort;
            if (a.StartsWith(LaunchArg + "=", StringComparison.OrdinalIgnoreCase))
            {
                int p;
                return int.TryParse(a.Substring(LaunchArg.Length + 1), out p) && p > 0 && p < 65536 ? p : DefaultPort;
            }
        }
        return null;
    }

    private static void WriteSessionFile()
    {
        var o = new JObject
        {
            ["version"] = Version,
            ["port"] = port,
            ["token"] = token,
            ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id,
            ["startedUtc"] = DateTime.UtcNow.ToString("o"),
            ["outputDir"] = OutputDir,
            ["gameLog"] = Application.consoleLogPath
        };
        File.WriteAllText(SessionFile, o.ToString(Formatting.Indented));
    }

    private static void ListenLoop()
    {
        while (listener != null && listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = listener.GetContext(); }
            catch { break; }
            ThreadPool.QueueUserWorkItem(_ => HandleContext(ctx));
        }
    }

    private static void HandleContext(HttpListenerContext ctx)
    {
        int status = 200;
        string body;
        try
        {
            if (ctx.Request.Headers[TokenHeader] != token)
            {
                status = 401;
                body = Error("missing or invalid " + TokenHeader + " header (read it from " + SessionFile + ")");
            }
            else
            {
                var req = BridgeRequest.From(ctx.Request);
                if (req.Path == "/ping")
                {
                    body = Ping().ToString(Formatting.None);
                }
                else
                {
                    Pending.Enqueue(req);
                    int timeoutMs = req.QueryInt("timeout", 60) * 1000;
                    if (!req.Done.Wait(timeoutMs))
                    {
                        req.Abandon();
                        status = 504;
                        body = Error("main thread did not complete the request within " + (timeoutMs / 1000) + "s (state=" + CachedState + ")");
                    }
                    else
                    {
                        status = req.Status;
                        body = req.ResponseBody;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            status = 500;
            body = Error(ex.ToString());
        }

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body ?? "{}");
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }
        catch { }
    }

    internal static JObject Ping()
    {
        double stallMs = (DateTime.UtcNow.Ticks - Interlocked.Read(ref LastFrameTicks)) / (double)TimeSpan.TicksPerMillisecond;
        return new JObject
        {
            ["ok"] = true,
            ["version"] = Version,
            ["state"] = CachedState,
            ["mainThreadStallMs"] = Math.Round(stallMs),
            ["lastLogSeq"] = RebirthGameBridgeLog.LastSeq,
            ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id
        };
    }

    internal static string Error(string message)
    {
        return new JObject { ["ok"] = false, ["error"] = message }.ToString(Formatting.None);
    }
}

/// <summary>One queued HTTP request. Handlers call Complete (possibly on a later frame).</summary>
public sealed class BridgeRequest
{
    public string Method;
    public string Path;
    public Dictionary<string, string> Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string Body;
    public int Status = 200;
    public string ResponseBody;
    public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
    private int completed;
    private bool counted;
    private static int open;

    /// <summary>Agent requests still running (e.g. a raid): instincts must not treat the player as idle.</summary>
    public static int OpenCount { get { return Volatile.Read(ref open); } }

    public bool IsAbandoned { get { return Volatile.Read(ref completed) == 2; } }

    public static BridgeRequest From(HttpListenerRequest r)
    {
        var req = new BridgeRequest
        {
            Method = r.HttpMethod.ToUpperInvariant(),
            Path = (r.Url.AbsolutePath ?? "/").TrimEnd('/').ToLowerInvariant()
        };
        if (req.Path.Length == 0) req.Path = "/";
        req.counted = true;
        Interlocked.Increment(ref open);
        foreach (string k in r.QueryString.AllKeys)
            if (k != null) req.Query[k] = r.QueryString[k];
        if (r.HasEntityBody)
            using (var sr = new StreamReader(r.InputStream, Encoding.UTF8))
                req.Body = sr.ReadToEnd();
        return req;
    }

    public string QueryString(string key, string fallback = null)
    {
        string v;
        return Query.TryGetValue(key, out v) && !string.IsNullOrEmpty(v) ? v : fallback;
    }

    public int QueryInt(string key, int fallback)
    {
        int v;
        return int.TryParse(QueryString(key), out v) ? v : fallback;
    }

    public float QueryFloat(string key, float fallback)
    {
        float v;
        return float.TryParse(QueryString(key), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out v) ? v : fallback;
    }

    public bool QueryBool(string key, bool fallback)
    {
        string v = QueryString(key);
        if (v == null) return fallback;
        return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public void Complete(JObject json, int status = 200)
    {
        if (Interlocked.CompareExchange(ref completed, 1, 0) != 0) return;
        if (counted) Interlocked.Decrement(ref open);
        if (json["ok"] == null) json.AddFirst(new JProperty("ok", status < 400));
        Status = status;
        ResponseBody = json.ToString(Formatting.None);
        Done.Set();
    }

    public void Fail(string message, int status = 400)
    {
        Complete(new JObject { ["ok"] = false, ["error"] = message }, status);
    }

    internal void Abandon()
    {
        if (Interlocked.CompareExchange(ref completed, 2, 0) == 0 && counted) Interlocked.Decrement(ref open);
    }
}

/// <summary>Main-thread pump. Drains queued bridge requests and hosts coroutines (screenshots).</summary>
public sealed class RebirthGameBridgePump : MonoBehaviour
{
    internal static RebirthGameBridgePump Instance;
    private const int MaxRequestsPerFrame = 16;

    internal static void Create()
    {
        if (Instance != null) return;
        var go = new GameObject("RebirthGameBridge");
        UnityEngine.Object.DontDestroyOnLoad(go);
        Instance = go.AddComponent<RebirthGameBridgePump>();
    }

    private void Update()
    {
        Interlocked.Exchange(ref RebirthGameBridge.LastFrameTicks, DateTime.UtcNow.Ticks);
        RebirthGameBridge.CachedState = RebirthGameBridgeHandlers.DescribeGameState();
        if (!RebirthGameBridgeMeleeProbe.Human) RebirthGameBridgeUi.AutoRespawnTick();
        if (RebirthGameBridge.CachedState == "ingame") { RebirthGameBridgeCombat.BleedReflexTick(); RebirthGameBridgeCombat.GuardTick(); if (RebirthGameBridgeMeleeProbe.Human) RebirthGameBridgeMeleeProbe.HumanTick(GameManager.Instance.World.GetPrimaryPlayer()); }

        BridgeRequest req;
        int n = 0;
        while (n++ < MaxRequestsPerFrame && RebirthGameBridge.Pending.TryDequeue(out req))
        {
            if (req.IsAbandoned) continue;
            if (req.Path != "/guard" && req.Path != "/log" && req.Path != "/state") RebirthGameBridge.LastRequestAt = Time.realtimeSinceStartup;
            try { RebirthGameBridgeHandlers.Dispatch(req); }
            catch (Exception ex) { req.Fail(ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace, 500); }
        }
    }

    internal void Run(IEnumerator routine)
    {
        StartCoroutine(routine);
    }
}
