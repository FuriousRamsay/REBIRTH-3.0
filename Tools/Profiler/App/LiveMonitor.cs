using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RebirthProfiler;

/// <summary>One second of live readings.</summary>
sealed class LiveSample
{
    public double T;
    public double Fps, FrameMs, P99Ms, Low1Fps, WorstMs;      // frame statistics over the last few seconds
    public bool HasFps;
    public OsSample Os = new();
}

/// <summary>
/// Reads everything about a running game once a second, without touching the game: frame times (Windows graphics events),
/// process and main-thread CPU, memory, GPU memory and utilisation.
/// </summary>
sealed class LiveMonitor : IDisposable
{
    [DllImport("kernel32.dll")] static extern IntPtr OpenThread(uint access, bool inherit, uint tid);
    [DllImport("kernel32.dll")] static extern bool GetThreadTimes(IntPtr t, out long create, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    readonly Process proc;
    readonly Pdh pdh;
    readonly EtwLive etw;
    IntPtr mainThread;
    readonly Stopwatch clock = Stopwatch.StartNew();
    TimeSpan lastCpu;
    long lastMain;
    double lastT;

    public string FpsError { get; }
    public bool FpsAvailable => etw != null && FpsError == null;
    public bool GpuAvailable => pdh.Available;
    public int Pid { get; }

    public LiveMonitor(int pid, uint mainThreadId)
    {
        Pid = pid;
        proc = Process.GetProcessById(pid);
        pdh = new Pdh(pid);
        if (mainThreadId != 0) mainThread = OpenThread(0x0800, false, mainThreadId);
        proc.Refresh();
        lastCpu = proc.TotalProcessorTime;
        if (mainThread != IntPtr.Zero && GetThreadTimes(mainThread, out _, out _, out long k, out long u)) lastMain = k + u;
        var e = new EtwLive(pid);
        if (e.Start()) etw = e; else { FpsError = e.Error; e.Dispose(); }
    }

    public bool HasExited { get { try { return proc.HasExited; } catch { return true; } } }

    /// <param name="window">seconds of recent frames used for the FPS / percentile figures</param>
    readonly object gate = new();
    public LiveSample Sample(double window = 5) { lock (gate) return SampleLocked(window); }

    LiveSample SampleLocked(double window)
    {
        proc.Refresh();
        double t = clock.Elapsed.TotalSeconds, dt = Math.Max(t - lastT, 0.001);
        lastT = t;
        var cpu = proc.TotalProcessorTime;
        double mainPct = 0;
        if (mainThread != IntPtr.Zero && GetThreadTimes(mainThread, out _, out _, out long k, out long u))
        { mainPct = 100.0 * ((k + u) - lastMain) / 1e7 / dt; lastMain = k + u; }
        var (vram, g3d, gany) = pdh.Sample();
        var s = new LiveSample
        {
            T = t,
            Os = new OsSample
            {
                T = t, ProcCores = (cpu - lastCpu).TotalSeconds / dt, MainThreadPct = mainPct, WsMB = proc.WorkingSet64 / 1048576.0,
                PrivMB = proc.PrivateMemorySize64 / 1048576.0, GpuVramMB = vram, Gpu3dPct = g3d, GpuAnyPct = gany, Threads = proc.Threads.Count,
            },
        };
        lastCpu = cpu;
        if (etw != null)
        {
            var r = etw.Recent(window);
            if (r.frames > 0) { s.HasFps = true; s.Fps = r.fps; s.FrameMs = r.avgMs; s.P99Ms = r.p99Ms; s.Low1Fps = r.low1Fps; s.WorstMs = r.worstMs; }
            // frames per second right now (last second) is the number people expect to see move
            var now = etw.Recent(1.0);
            if (now.frames > 0) s.Fps = now.fps;
        }
        return s;
    }

    /// <summary>All Present timestamps seen since the monitor started (FILETIME ticks).</summary>
    public List<long> PresentTicks() => etw?.Snapshot() ?? new List<long>();
    public long EventsSeen => etw?.EventsSeen ?? 0;

    public void Dispose()
    {
        etw?.Dispose();
        pdh.Dispose();
        if (mainThread != IntPtr.Zero) { CloseHandle(mainThread); mainThread = IntPtr.Zero; }
    }
}
