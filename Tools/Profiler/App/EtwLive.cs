using System.Runtime.InteropServices;

namespace RebirthProfiler;

/// <summary>
/// Real-time consumer of Windows' DXGI Present events: every time the game presents a frame, Windows tells us
/// immediately, so FPS and frame times can be shown live. Nothing is loaded into the game. Needs no admin rights when the
/// account is in the "Performance Log Users" group.
/// </summary>
sealed class EtwLive : IDisposable
{
    const string SessionName = "RebirthBenchLive";
    static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    const int PresentStartEventId = 42;

    // ---- Win32 structures (only what ProcessTrace needs; layout mirrors evntrace.h / evntcons.h) -----------------------
    [StructLayout(LayoutKind.Sequential)]
    struct EVENT_TRACE_HEADER { public ushort Size, FieldTypeFlags; public uint Version, ThreadId, ProcessId; public long TimeStamp; public Guid Guid; public ulong ProcessorTime; }
    [StructLayout(LayoutKind.Sequential)]
    struct EVENT_TRACE { public EVENT_TRACE_HEADER Header; public uint InstanceId, ParentInstanceId; public Guid ParentGuid; public IntPtr MofData; public uint MofLength, ClientContext; }
    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEMTIME { public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct TIME_ZONE_INFORMATION
    {
        public int Bias;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string StandardName;
        public SYSTEMTIME StandardDate; public int StandardBias;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DaylightName;
        public SYSTEMTIME DaylightDate; public int DaylightBias;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct TRACE_LOGFILE_HEADER
    {
        public uint BufferSize, Version, ProviderVersion, NumberOfProcessors; public long EndTime;
        public uint TimerResolution, MaximumFileSize, LogFileMode, BuffersWritten;
        public uint StartBuffers, PointerSize, EventsLost, CpuSpeedInMHz;      // union with a GUID (16 bytes)
        public IntPtr LoggerName, LogFileName; public TIME_ZONE_INFORMATION TimeZone;
        public long BootTime, PerfFreq, StartTime; public uint ReservedFlags, BuffersLost;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct EVENT_TRACE_LOGFILE
    {
        public IntPtr LogFileName, LoggerName; public long CurrentTime; public uint BuffersRead, ProcessTraceMode;
        public EVENT_TRACE CurrentEvent; public TRACE_LOGFILE_HEADER LogfileHeader;
        public IntPtr BufferCallback; public uint BufferSize, Filled, EventsLost;
        public IntPtr EventRecordCallback; public uint IsKernelTrace; public IntPtr Context;
    }

    delegate void EventRecordCallback(IntPtr eventRecord);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern uint StartTraceW(out ulong handle, string name, IntPtr props);
    [DllImport("advapi32.dll")] static extern uint EnableTraceEx2(ulong handle, ref Guid provider, uint control, byte level, ulong matchAny, ulong matchAll, uint timeout, IntPtr parameters);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern ulong OpenTraceW(IntPtr logfile);
    [DllImport("advapi32.dll")] static extern uint ProcessTrace(ulong[] handles, uint count, IntPtr start, IntPtr end);
    [DllImport("advapi32.dll")] static extern uint CloseTrace(ulong handle);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern uint ControlTraceW(ulong handle, string name, IntPtr props, uint code);

    const int PropsSize = 1024, LoggerNameOffset = 120;
    const uint EVENT_TRACE_CONTROL_STOP = 1, WNODE_FLAG_TRACED_GUID = 0x00020000, EVENT_TRACE_REAL_TIME_MODE = 0x100;
    const uint PROCESS_TRACE_MODE_REAL_TIME = 0x100, PROCESS_TRACE_MODE_EVENT_RECORD = 0x10000000;
    const uint ERROR_ALREADY_EXISTS = 183, ERROR_ACCESS_DENIED = 5;

    readonly int pid;
    readonly List<long> ticks = new();                    // FILETIME ticks (100 ns) of the game's Present calls
    ulong session, trace;
    IntPtr logfilePtr;
    EventRecordCallback callback;                          // kept alive for the native side
    Thread worker;
    bool stopped;

    public string Error { get; private set; }
    public long EventsSeen;

    public EtwLive(int pid) { this.pid = pid; }

    static IntPtr BuildProps()
    {
        IntPtr p = Marshal.AllocHGlobal(PropsSize);
        for (int i = 0; i < PropsSize; i += 8) Marshal.WriteInt64(p, i, 0);
        Marshal.WriteInt32(p, 0, PropsSize);                       // Wnode.BufferSize
        Marshal.WriteInt32(p, 40, 2);                              // Wnode.ClientContext: system time (FILETIME) timestamps
        Marshal.WriteInt32(p, 44, (int)WNODE_FLAG_TRACED_GUID);    // Wnode.Flags
        Marshal.WriteInt32(p, 64, (int)EVENT_TRACE_REAL_TIME_MODE);// LogFileMode
        Marshal.WriteInt32(p, 116, LoggerNameOffset);              // LoggerNameOffset
        return p;
    }

    /// <returns>true when live capture is running; otherwise <see cref="Error"/> explains why.</returns>
    public bool Start()
    {
        IntPtr props = BuildProps();
        try
        {
            uint rc = StartTraceW(out session, SessionName, props);
            if (rc == ERROR_ALREADY_EXISTS)                        // left over from a crashed run
            {
                ControlTraceW(0, SessionName, props, EVENT_TRACE_CONTROL_STOP);
                Marshal.FreeHGlobal(props); props = BuildProps();
                rc = StartTraceW(out session, SessionName, props);
            }
            if (rc != 0)
            {
                Error = rc == ERROR_ACCESS_DENIED
                    ? "Windows refused to start the frame-time capture. Your account must be an administrator or a member of the 'Performance Log Users' group."
                    : $"StartTrace failed (error {rc}).";
                return false;
            }
            Guid g = DxgiProvider;
            rc = EnableTraceEx2(session, ref g, 1 /*ENABLE_PROVIDER*/, 5, ulong.MaxValue, 0, 0, IntPtr.Zero);
            if (rc != 0) { Error = $"EnableTraceEx2 failed (error {rc})."; Stop(); return false; }

            callback = OnEvent;
            var lf = new EVENT_TRACE_LOGFILE { LoggerName = Marshal.StringToHGlobalUni(SessionName), ProcessTraceMode = PROCESS_TRACE_MODE_REAL_TIME | PROCESS_TRACE_MODE_EVENT_RECORD };
            lf.EventRecordCallback = Marshal.GetFunctionPointerForDelegate(callback);
            lf.LogfileHeader.TimeZone = new TIME_ZONE_INFORMATION { StandardName = "", DaylightName = "" };
            logfilePtr = Marshal.AllocHGlobal(Marshal.SizeOf<EVENT_TRACE_LOGFILE>());
            Marshal.StructureToPtr(lf, logfilePtr, false);
            trace = OpenTraceW(logfilePtr);
            if (trace == ulong.MaxValue || trace == 0) { Error = "OpenTrace failed (error " + Marshal.GetLastWin32Error() + ")."; Stop(); return false; }

            worker = new Thread(() => { try { ProcessTrace(new[] { trace }, 1, IntPtr.Zero, IntPtr.Zero); } catch { } }) { IsBackground = true, Name = "etw-frames" };
            worker.Start();
            return true;
        }
        finally { Marshal.FreeHGlobal(props); }
    }

    void OnEvent(IntPtr rec)
    {
        // EVENT_RECORD.EventHeader: ProcessId @12, TimeStamp @16, EventDescriptor.Id @40
        Interlocked.Increment(ref EventsSeen);
        if (Marshal.ReadInt16(rec, 40) != PresentStartEventId) return;
        if (Marshal.ReadInt32(rec, 12) != pid) return;
        long ts = Marshal.ReadInt64(rec, 16);
        lock (ticks) ticks.Add(ts);
    }

    public int Count { get { lock (ticks) return ticks.Count; } }
    public List<long> Snapshot() { lock (ticks) return new List<long>(ticks); }

    /// <summary>Statistics over the last <paramref name="seconds"/> seconds of frames (one frame = calls &gt; 1 ms apart).</summary>
    public (double fps, double avgMs, double p99Ms, double low1Fps, double worstMs, int frames) Recent(double seconds)
    {
        long cutoff = DateTime.UtcNow.ToFileTimeUtc() - (long)(seconds * 1e7);
        var win = new List<long>();
        lock (ticks)
        {
            int i = ticks.Count - 1;
            while (i >= 0 && ticks[i] >= cutoff) { win.Add(ticks[i]); i--; }
            if (i >= 0) win.Add(ticks[i]);                      // one frame before the window, to get its first interval
        }
        win.Reverse();
        var deltas = new List<double>();
        long prev = 0;
        foreach (long t in win)
        {
            if (prev == 0) { prev = t; continue; }
            if (t - prev <= 10000) continue;                     // presents within 1 ms belong to the same frame
            deltas.Add((t - prev) / 10000.0);
            prev = t;
        }
        if (deltas.Count == 0) return (0, 0, 0, 0, 0, 0);
        deltas.Sort();
        double sum = deltas.Sum();
        double p99 = deltas[Math.Min(deltas.Count - 1, (int)Math.Ceiling(deltas.Count * 0.99) - 1)];
        int slow = Math.Max(1, (int)Math.Ceiling(deltas.Count * 0.01));
        double slowAvg = deltas.Skip(deltas.Count - slow).Average();
        return (1000.0 * deltas.Count / sum, sum / deltas.Count, p99, 1000.0 / slowAvg, deltas[^1], deltas.Count);
    }

    public void Stop()
    {
        if (stopped) return;
        stopped = true;
        IntPtr props = BuildProps();
        try
        {
            if (session != 0) { ControlTraceW(session, SessionName, props, EVENT_TRACE_CONTROL_STOP); session = 0; }
            else ControlTraceW(0, SessionName, props, EVENT_TRACE_CONTROL_STOP);
        }
        finally { Marshal.FreeHGlobal(props); }
        if (trace != 0 && trace != ulong.MaxValue) { CloseTrace(trace); trace = 0; }
        worker?.Join(3000);
        if (logfilePtr != IntPtr.Zero) { Marshal.FreeHGlobal(logfilePtr); logfilePtr = IntPtr.Zero; }
    }

    public void Dispose() => Stop();
}
