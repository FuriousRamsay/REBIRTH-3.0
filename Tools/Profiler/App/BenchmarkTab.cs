using System.Data;
using System.Drawing.Drawing2D;

namespace RebirthProfiler;

/// <summary>Themed line chart: several series over time, auto-scaled, soft grid, accent-coloured lines with a light area fill.</summary>
sealed class SeriesChart : Control
{
    public sealed record Line(string Name, Color Color, double[] X, double[] Y);
    List<Line> lines = new();
    string title = "", unit = "";

    public SeriesChart()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        MinimumSize = new Size(200, 120);
        Theme.Changed += Invalidate;
    }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= Invalidate; base.Dispose(d); }
    public void Set(string title, string unit, List<Line> lines) { this.title = title; this.unit = unit; this.lines = lines; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = Theme.Scale(this);
        var area = new RectangleF(58 * k, 38 * k, Math.Max(10, Width - 72 * k), Math.Max(10, Height - 64 * k));

        TextRenderer.DrawText(g, title, Theme.Title, new Point((int)(4 * k), (int)(2 * k)), Theme.Text);
        if (unit.Length > 0)
        {
            var us = TextRenderer.MeasureText(unit, Theme.Small);
            TextRenderer.DrawText(g, unit, Theme.Small, new Point(Width - us.Width - (int)(8 * k), (int)(6 * k)), Theme.Muted);
        }
        if (lines.Count == 0 || lines.All(l => l.X.Length == 0))
        {
            TextRenderer.DrawText(g, "No data yet", Theme.Body, new Point((int)(area.X + 8 * k), (int)(area.Y + 8 * k)), Theme.Muted);
            return;
        }

        double xmax = Math.Max(1, lines.Max(l => l.X.Length == 0 ? 0 : l.X.Max()));
        double ymax = lines.Max(l => l.Y.Length == 0 ? 0 : l.Y.Max()); if (ymax <= 0) ymax = 1;
        ymax *= 1.1;
        using (var grid = new Pen(Color.FromArgb(90, Theme.Border)))
            for (int i = 0; i <= 4; i++)
            {
                float y = area.Bottom - area.Height * i / 4f;
                g.DrawLine(grid, area.Left, y, area.Right, y);
                string label = (ymax * i / 4).ToString(ymax >= 100 ? "F0" : "F1");
                var ls = TextRenderer.MeasureText(label, Theme.Small);
                TextRenderer.DrawText(g, label, Theme.Small, new Point((int)(area.Left - ls.Width - 8 * k), (int)(y - ls.Height / 2f)), Theme.Muted);
            }
        TextRenderer.DrawText(g, "0", Theme.Small, new Point((int)area.Left, (int)(area.Bottom + 6 * k)), Theme.Muted);
        string xl = $"{xmax:F0} s";
        TextRenderer.DrawText(g, xl, Theme.Small, new Point((int)(area.Right - TextRenderer.MeasureText(xl, Theme.Small).Width), (int)(area.Bottom + 6 * k)), Theme.Muted);

        foreach (var l in lines)
        {
            if (l.X.Length < 2) continue;
            var pts = l.X.Select((x, i) => new PointF((float)(area.Left + area.Width * x / xmax), (float)(area.Bottom - area.Height * Math.Min(l.Y[i], ymax) / ymax))).ToArray();
            if (lines.Count <= 2 || l.Name != "baseline")
            {
                var fill = pts.Concat(new[] { new PointF(pts[^1].X, area.Bottom), new PointF(pts[0].X, area.Bottom) }).ToArray();
                using var fb = new LinearGradientBrush(new RectangleF(area.X, area.Y, area.Width, area.Height), Color.FromArgb(70, l.Color), Color.FromArgb(0, l.Color), LinearGradientMode.Vertical);
                g.FillPolygon(fb, fill);
            }
            using var pen = new Pen(l.Color, 2f * k) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(pen, pts);
        }

        float lx = area.Left + 4 * k + TextRenderer.MeasureText(title, Theme.Title).Width;
        lx = Math.Max(lx, area.Left + 140 * k);
        foreach (var l in lines.GroupBy(l => l.Name).Select(x => x.First()))
        {
            using (var b = new SolidBrush(l.Color)) g.FillEllipse(b, lx, 10 * k, 8 * k, 8 * k);
            TextRenderer.DrawText(g, l.Name, Theme.Small, new Point((int)(lx + 13 * k), (int)(6 * k)), Theme.Muted);
            lx += 34 * k + TextRenderer.MeasureText(l.Name, Theme.Small).Width;
        }
    }
}

sealed class BenchmarkTab : PagePanel
{
    readonly List<BenchResult> baseline = new(), current = new();
    readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, BorderStyle = BorderStyle.None,
    };
    readonly SeriesChart chart = new() { Dock = DockStyle.Fill };
    readonly ComboBox chartMetric = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Margin = new Padding(0, 4, 0, 0) };
    readonly RoundedTextBox label = new() { Text = "baseline", Width = 170 };
    readonly NumericUpDown warmup = new() { Minimum = 0, Maximum = 600, Value = 30, Width = 64, Margin = new Padding(0, 6, 16, 0) };
    readonly NumericUpDown duration = new() { Minimum = 10, Maximum = 1800, Value = 120, Width = 72, Margin = new Padding(0, 6, 16, 0) };
    readonly Toggle closeGame = new() { Text = "Close game when done", Checked = true, AutoSize = true, Margin = new Padding(0, 3, 18, 0) };
    readonly AppButton run = new() { Text = "▶   Run benchmark", Kind = AppButton.K.Primary, AutoSize = true };
    readonly AppButton cancel = new() { Text = "Cancel", AutoSize = true, Enabled = false };
    readonly Label status = new() { AutoSize = false, Dock = DockStyle.Fill, Tag = "muted", Text = "Runs the same scenario every time: load the test save, clear the area, warm up, then measure. Nothing is loaded into the game; frame times come from Windows." };
    readonly Func<string> gameArgs;
    readonly Func<bool> freshSave;
    CancellationTokenSource cts;
    // ---- live readout ----------------------------------------------------------------------------------------------
    readonly List<LiveSample> liveSeries = new();
    readonly AppButton monitor = new() { Text = "●   Monitor running game", AutoSize = true };
    readonly System.Windows.Forms.Timer monitorTimer = new() { Interval = 1000 };
    LiveMonitor liveMon;
    bool liveActive;
    /// <summary>Receives every one-second reading so the always-visible strip in the main window can show it.</summary>
    public Action<LiveSample> LiveSink;
    /// <summary>true while this tab needs its own monitor (a benchmark or a recorded session), so the main window releases the shared one.</summary>
    public Action<bool> ActiveChanged;
    static readonly Func<LiveSample, double>[] LiveGet =
    {
        s => s.Fps, s => s.WorstMs, s => s.Os.MainThreadPct, s => s.Os.ProcCores, s => s.Os.Gpu3dPct, s => s.Os.GpuVramMB, s => s.Os.WsMB,
    };


    public bool ExitWhenDone;

    static readonly (string Name, string Unit, Func<BenchResult, (double[] x, double[] y)> Get)[] ChartMetrics =
    {
        ("FPS (per second)", "fps", r => (r.Game.Series.Select(s => (double)s.T).ToArray(), r.Game.Series.Select(s => (double)s.F).ToArray())),
        ("Worst frame in each second", "ms", r => (r.Game.Series.Select(s => (double)s.T).ToArray(), r.Game.Series.Select(s => s.Max).ToArray())),
        ("Main thread CPU", "% of a core", r => (r.Os.Select(s => s.T).ToArray(), r.Os.Select(s => s.MainThreadPct).ToArray())),
        ("Process CPU", "cores", r => (r.Os.Select(s => s.T).ToArray(), r.Os.Select(s => s.ProcCores).ToArray())),
        ("GPU 3D utilisation", "%", r => (r.Os.Select(s => s.T).ToArray(), r.Os.Select(s => s.Gpu3dPct).ToArray())),
        ("GPU memory (VRAM)", "MB", r => (r.Os.Select(s => s.T).ToArray(), r.Os.Select(s => s.GpuVramMB).ToArray())),
        ("Working set", "MB", r => (r.Os.Select(s => s.T).ToArray(), r.Os.Select(s => s.WsMB).ToArray())),
    };

    public BenchmarkTab(Func<string> gameArgs, Func<bool> freshSave) : base("Benchmark")
    {
        this.gameArgs = gameArgs; this.freshSave = freshSave;
        float k = Theme.Scale(this);
        int S(int px) => (int)(px * k);
        Padding = new Padding(0, S(12), 0, 0);
        Label Caption(string t) => new Label { Text = t, Tag = "muted", AutoSize = true, Margin = new Padding(0, 9, 8, 0) };
        AppButton Tool(string text, Action click, AppButton.K kind = AppButton.K.Secondary)
        {
            var b = new AppButton { Text = text, Kind = kind, AutoSize = true };
            b.Click += (_, _) => click();
            return b;
        }

        var runRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = S(42), WrapContents = false };
        runRow.Controls.AddRange(new Control[] { Caption("Label"), label, Caption("Warm-up (s)"), warmup, Caption("Measure (s)"), duration, closeGame, run, cancel });
        var statusHost = new Panel { Dock = DockStyle.Top, Height = S(28) };
        statusHost.Controls.Add(status);
        var toolsRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = S(42), WrapContents = false };
        toolsRow.Controls.AddRange(new Control[]
        {
            monitor, Tool("Load baseline run(s)…", () => LoadInto(baseline)), Tool("Load new run(s)…", () => LoadInto(current)),
            Tool("Use new runs as baseline", () => { baseline.Clear(); baseline.AddRange(current); current.Clear(); Refresh(); }),
            Tool("Clear", () => { baseline.Clear(); current.Clear(); Refresh(); }, AppButton.K.Ghost),
            new Label { Text = "Chart", Tag = "muted", AutoSize = true, Margin = new Padding(18, 9, 8, 0) }, chartMetric,
        });
        foreach (var m in ChartMetrics) chartMetric.Items.Add(m.Name);
        chartMetric.SelectedIndex = 0;
        chartMetric.SelectedIndexChanged += (_, _) => RefreshChart();
        run.Click += async (_, _) => await RunBenchmark();
        monitor.Click += (_, _) => ToggleMonitor();
        monitorTimer.Tick += (_, _) => { if (liveMon == null) return; if (liveMon.HasExited) { StopMonitor(true); return; } ShowLive(liveMon.Sample()); };
        cancel.Click += (_, _) => cts?.Cancel();

        var runCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(14), S(10), S(14), S(6)) };
        runCard.Controls.Add(toolsRow);
        runCard.Controls.Add(statusHost);
        runCard.Controls.Add(runRow);
        var runHost = new Panel { Dock = DockStyle.Top, Height = S(42 + 28 + 42 + 16 + 12), Padding = new Padding(0, 0, 0, S(12)) };
        runHost.Controls.Add(runCard);

        var gridCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
        gridCard.Controls.Add(Ui.WithSlimScroll(grid));
        var chartCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(14), S(10), S(14), S(10)) };
        chartCard.Controls.Add(chart);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = S(300), SplitterWidth = 12 };
        split.Panel1.Controls.Add(gridCard);
        split.Panel2.Controls.Add(chartCard);
        Controls.Add(split);
        Controls.Add(runHost);
        Theme.Changed += RefreshChart;
        grid.DataError += (_, e) => e.ThrowException = false;
        Refresh();
    }

    void ShowLive(LiveSample s)
    {
        liveSeries.Add(s);
        if (liveSeries.Count > 3600) liveSeries.RemoveAt(0);
        LiveSink?.Invoke(s);
        if (liveActive) RefreshChart();
    }

    void ToggleMonitor()
    {
        if (liveMon != null) { StopMonitor(saveResult: true); return; }
        ActiveChanged?.Invoke(true);
        var game = System.Diagnostics.Process.GetProcessesByName("7DaysToDie").FirstOrDefault();
        if (game == null) { status.Text = "7 Days To Die is not running. Start it, then press Monitor."; ActiveChanged?.Invoke(false); return; }
        uint tid = 0;
        try { tid = (uint)game.Threads.Cast<System.Diagnostics.ProcessThread>().OrderBy(t => t.StartTime).First().Id; } catch { }
        try { liveMon = new LiveMonitor(game.Id, tid); }
        catch (Exception ex) { status.Text = "Could not attach: " + ex.Message; ActiveChanged?.Invoke(false); return; }
        if (!liveMon.FpsAvailable) status.Text = "Frame capture unavailable: " + liveMon.FpsError + " (CPU, memory and GPU readings still work)";
        else status.Text = "Monitoring the running game. Press again to stop and save the session as a result.";
        liveSeries.Clear(); liveActive = true; monitor.Text = "■ Stop monitoring"; run.Enabled = false;
        monitorTimer.Start();
    }

    void StopMonitor(bool saveResult)
    {
        monitorTimer.Stop();
        var mon = liveMon; liveMon = null; liveActive = false; monitor.Text = "● Monitor running game"; run.Enabled = true;
        if (mon == null) return;
        try
        {
            if (saveResult && liveSeries.Count >= 20 && mon.FpsAvailable)
            {
                var presents = mon.PresentTicks();
                var r = new BenchResult
                {
                    Label = (label.Text.Trim().Length == 0 ? "monitor" : label.Text.Trim()), Created = DateTime.Now.ToString("s"), DurationSec = liveSeries.Count,
                    Os = liveSeries.Select(s => s.Os).ToList(), Machine = Environment.MachineName, Notes = "live monitoring session",
                    Game = EtwFrames.ToGameBench(presents),
                };
                string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "out", "bench"));
                Directory.CreateDirectory(dir);
                r.Save(Path.Combine(dir, $"bench_{r.Label}_{DateTime.Now:yyyyMMdd_HHmmss}.json"));
                current.Add(r); RegisterSession(r); Refresh();
                status.Text = "Session saved: " + Path.GetFileName(r.Path);
            }
            else status.Text = "Monitoring stopped.";
        }
        finally { mon.Dispose(); ActiveChanged?.Invoke(false); }
    }

    public Action SessionSaved;

    public void LoadResult(string path)
    {
        try { current.Add(BenchResult.Load(path)); Refresh(); }
        catch (Exception ex) { status.Text = "Could not open the result: " + ex.Message; }
    }

    void RegisterSession(BenchResult r)
    {
        try
        {
            var s = r.Summary;
            SessionStore.Save(r.Path, "benchmark", r.Label, "", $"{s.GetValueOrDefault("avgFps"):F0} fps avg, 1% low {s.GetValueOrDefault("low1"):F0}, {r.DurationSec}s", r.Machine);
            SessionSaved?.Invoke();
        }
        catch (Exception ex) { status.Text = "Result saved, but not added to Sessions: " + ex.Message; }
    }

    public void Configure(string labelText, int warm, int measure, bool close)
    {
        label.Text = labelText; warmup.Value = warm; duration.Value = measure; closeGame.Checked = close;
    }

    void LoadInto(List<BenchResult> target)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Benchmark result (*.json)|*.json", Multiselect = true, Title = "Choose benchmark result file(s)",
            InitialDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "out", "bench")),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { foreach (var f in dlg.FileNames) target.Add(BenchResult.Load(f)); Refresh(); }
        catch (Exception ex) { MessageBox.Show(this, "Could not read the result:\n" + ex.Message, "Rebirth Profiler"); }
    }

    public async Task RunBenchmark()
    {
        if (cts != null) return;
        string appDir = AppContext.BaseDirectory;
        string exe = GameLauncher.FindGameExe(appDir);
        string module = GameLauncher.FindModule(appDir);
        if (exe == null) { status.Text = "Could not find 7DaysToDie.exe."; if (ExitWhenDone) { Environment.ExitCode = 2; FindForm()?.Close(); } return; }
        cts = new CancellationTokenSource();
        run.Enabled = false; cancel.Enabled = true; monitor.Enabled = false; liveSeries.Clear(); liveActive = true; ActiveChanged?.Invoke(true);
        var progress = new Progress<string>(m => status.Text = m);
        try
        {
            var r = await BenchmarkRunner.Run(new BenchOptions
            {
                Label = label.Text.Trim().Length == 0 ? "run" : label.Text.Trim(), WarmupSec = (int)warmup.Value, DurationSec = (int)duration.Value,
                CloseGame = closeGame.Checked, GameArgs = gameArgs(), FreshSave = freshSave(),
            }, exe, progress, cts.Token, new Progress<LiveSample>(ShowLive));
            current.Add(r);
            RegisterSession(r);
            Refresh();
            status.Text = $"Done: {Path.GetFileName(r.Path)}  ({r.Summary["avgFps"]:F1} fps average, 1% low {r.Summary["low1"]:F1})";
        }
        catch (OperationCanceledException) { status.Text = "Cancelled."; if (ExitWhenDone) Environment.ExitCode = 3; }
        catch (Exception ex)
        {
            status.Text = "Benchmark failed: " + ex.Message;
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "..", "out", "app_errors.log"), ex + Environment.NewLine); } catch { }
            if (ExitWhenDone) Environment.ExitCode = 1;
        }
        finally
        {
            cts.Dispose(); cts = null; run.Enabled = true; cancel.Enabled = false; monitor.Enabled = true; liveActive = false; RefreshChart(); ActiveChanged?.Invoke(false);
            if (ExitWhenDone) FindForm()?.Close();
        }
    }

    static string Fmt(double v, string unit) => unit is "fps" or "%" or "cores" or "% of a core" ? v.ToString("F1") : v.ToString(v >= 100 ? "F0" : "F2");

    public new void Refresh()
    {
        var t = new DataTable();
        foreach (var c in new[] { "Area", "Metric", "Baseline", "New", "Change", "Verdict" }) t.Columns.Add(c, typeof(string));
        var rows = BenchCompare.Build(baseline, current);
        foreach (var r in rows) t.Rows.Add(r.Area, r.Metric, r.Baseline, r.New, r.Change, r.Verdict);
        grid.DataSource = null;
        grid.DataSource = t;
        for (int i = 0; i < grid.Rows.Count && i < rows.Count; i++)
        {
            var c = grid.Rows[i].Cells["Verdict"];
            c.Style.ForeColor = rows[i].State switch { 1 => Theme.Good, -1 => Theme.Bad, _ => Theme.Muted };
            c.Style.Font = new Font(grid.Font, rows[i].State is 1 or -1 ? FontStyle.Bold : FontStyle.Regular);
        }
        RefreshChart();
    }

    void RefreshChart()
    {
        var m = ChartMetrics[Math.Max(chartMetric.SelectedIndex, 0)];
        if (liveActive && liveSeries.Count > 0)
        {
            var get = LiveGet[Math.Max(chartMetric.SelectedIndex, 0)];
            // only the last two minutes, so the loading screen (hundreds of FPS) does not squash the scale
            var tail = liveSeries.Skip(Math.Max(0, liveSeries.Count - 120)).ToList();
            double t0 = tail[0].T;
            chart.Set(m.Name + "  (live, last 2 min)", m.Unit, new List<SeriesChart.Line> { new("live", Theme.Accent, tail.Select(s => s.T - t0).ToArray(), tail.Select(get).ToArray()) });
            return;
        }
        var lines = new List<SeriesChart.Line>();
        foreach (var r in baseline) { var (x, y) = m.Get(r); lines.Add(new SeriesChart.Line("baseline", Color.FromArgb(0x8A, 0x9B, 0xB8), x, y)); }
        foreach (var r in current) { var (x, y) = m.Get(r); lines.Add(new SeriesChart.Line("new", Theme.Accent, x, y)); }
        chart.Set(m.Name, m.Unit, lines);
    }
}
