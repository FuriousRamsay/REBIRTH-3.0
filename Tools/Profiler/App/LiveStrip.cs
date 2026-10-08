namespace RebirthProfiler;

/// <summary>
/// The always-visible row of live readings above the tabs: FPS, frame times, CPU, memory, VRAM and GPU, refreshed every second
/// while the game is running (however it was started).
/// </summary>
sealed class LiveStrip : TableLayoutPanel
{
    static readonly (string Key, string Title)[] Fields =
    {
        ("fps", "FPS"), ("avg", "Frame time"), ("low", "1% low"), ("p99", "99th percentile"), ("worst", "Worst frame"),
        ("main", "Main thread"), ("cores", "CPU"), ("ws", "Memory"), ("vram", "VRAM"), ("gpu", "GPU 3D"),
    };
    readonly Dictionary<string, MetricTile> tiles = new();

    /// <summary>Raised with a short description of what the strip is showing, for the status chip in the header.</summary>
    public event Action<string, Chip.S> State;

    public LiveStrip()
    {
        Dock = DockStyle.Top; ColumnCount = Fields.Length; RowCount = 1; Margin = Padding.Empty; Padding = Padding.Empty;
        Height = (int)(66 * Theme.Scale(this));
        for (int i = 0; i < Fields.Length; i++)
        {
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / Fields.Length));
            var t = new MetricTile { Caption = Fields[i].Title, Dock = DockStyle.Fill, Margin = new Padding(0, 0, i == Fields.Length - 1 ? 0 : 8, 0) };
            tiles[Fields[i].Key] = t;
            Controls.Add(t, i, 0);
        }
    }

    protected override void OnDpiChangedAfterParent(EventArgs e) { Height = (int)(66 * Theme.Scale(this)); base.OnDpiChangedAfterParent(e); }

    public void Clear(string message)
    {
        State?.Invoke(message, Chip.S.Idle);
        foreach (var t in tiles.Values) t.Set("–");
    }

    public void Show(LiveSample s, string source)
    {
        State?.Invoke(source, s.HasFps ? Chip.S.Running : Chip.S.Busy);
        static string F(double v, string f = "F0") => v <= 0 ? "–" : v.ToString(f);
        Color? Grade(double v, double good, double ok) => v <= 0 ? null : v >= good ? Theme.Good : v >= ok ? Theme.Warn : Theme.Bad;
        Color? GradeLow(double v, double good, double ok) => v <= 0 ? null : v <= good ? Theme.Good : v <= ok ? Theme.Warn : Theme.Bad;
        tiles["fps"].Set(s.HasFps ? F(s.Fps) : "–", s.HasFps ? Grade(s.Fps, 60, 30) : null);
        tiles["avg"].Set(s.HasFps ? F(s.FrameMs, "F1") + " ms" : "–");
        tiles["low"].Set(s.HasFps ? F(s.Low1Fps) : "–", s.HasFps ? Grade(s.Low1Fps, 45, 25) : null);
        tiles["p99"].Set(s.HasFps ? F(s.P99Ms, "F1") + " ms" : "–", s.HasFps ? GradeLow(s.P99Ms, 22, 40) : null);
        tiles["worst"].Set(s.HasFps ? F(s.WorstMs) + " ms" : "–", s.HasFps ? GradeLow(s.WorstMs, 40, 100) : null);
        tiles["main"].Set($"{s.Os.MainThreadPct:F0}%", s.Os.MainThreadPct >= 97 ? Theme.Warn : null);
        tiles["cores"].Set($"{s.Os.ProcCores:F1} cores");
        tiles["ws"].Set($"{s.Os.WsMB / 1024:F1} GB");
        tiles["vram"].Set(s.Os.GpuVramMB > 0 ? $"{s.Os.GpuVramMB / 1024:F1} GB" : "–");
        tiles["gpu"].Set($"{s.Os.Gpu3dPct:F0}%");
    }
}
