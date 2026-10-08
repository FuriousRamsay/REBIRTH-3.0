using System.Data;
using System.Diagnostics;

namespace RebirthProfiler;

/// <summary>Records dips while you play normally and lists each one with the code that used the extra time.</summary>
sealed class PlayTab : PagePanel
{
    readonly Func<LiveMonitor> monitor;
    readonly Func<string> livePath;
    readonly Func<string> modPattern;
    readonly AppButton start = new() { Text = "Start recording", Kind = AppButton.K.Primary, AutoSize = true };
    readonly AppButton stop = new() { Text = "Stop and save", AutoSize = true, Enabled = false };
    readonly AppButton folder = new() { Text = "Open results folder", Kind = AppButton.K.Ghost, AutoSize = true };
    readonly AppButton load = new() { Text = "Load recording…", Kind = AppButton.K.Ghost, AutoSize = true };
    readonly Label status = new() { Dock = DockStyle.Fill, Tag = "muted", Text = "Play normally. Every dip in frame rate is listed here with the code that used the extra time. Start it after the save has loaded (the first minute is ignored)." };
    readonly DataGridView grid = new()
    {
        ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BorderStyle = BorderStyle.None,
    };
    readonly DataGridView causes = new()
    {
        ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BorderStyle = BorderStyle.None,
    };
    readonly DataGridView summary = new()
    {
        ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BorderStyle = BorderStyle.None, ColumnHeadersVisible = false,
    };
    readonly SeriesChart chart = new() { Dock = DockStyle.Fill };
    DateTime lastSummary = DateTime.MinValue;
    PlayRecorder recorder;
    PlayResult shown = new();
    string resultDir;

    public PlayTab(Func<LiveMonitor> monitor, Func<string> livePath, Func<string> modPattern) : base("Play recording")
    {
        this.monitor = monitor; this.livePath = livePath; this.modPattern = modPattern;
        float k = Theme.Scale(this);
        int S(int px) => (int)(px * k);
        Padding = new Padding(0, S(12), 0, 0);
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = S(42), WrapContents = false };
        row.Controls.AddRange(new Control[] { start, stop, folder, load });
        var statusHost = new Panel { Dock = DockStyle.Top, Height = S(46) };
        statusHost.Controls.Add(status);
        var top = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(14), S(10), S(14), S(6)) };
        top.Controls.Add(statusHost);
        top.Controls.Add(row);
        var topHost = new Panel { Dock = DockStyle.Top, Height = S(42 + 46 + 16 + 12), Padding = new Padding(0, 0, 0, S(12)) };
        topHost.Controls.Add(top);
        var gridCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
        gridCard.Controls.Add(Ui.WithSlimScroll(grid));
        var causeCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
        causeCard.Controls.Add(Ui.WithSlimScroll(causes));
        var summaryCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
        summaryCard.Controls.Add(Ui.WithSlimScroll(summary));
        var lower = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = S(900), SplitterWidth = 12 };
        lower.Panel1.Controls.Add(causeCard);
        var chartCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(14), S(10), S(14), S(10)) };
        chartCard.Controls.Add(chart);
        var right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = S(190), SplitterWidth = 12 };
        right.Panel1.Controls.Add(chartCard);
        right.Panel2.Controls.Add(summaryCard);
        lower.Panel2.Controls.Add(right);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = S(320), SplitterWidth = 12 };
        split.Panel1.Controls.Add(gridCard);
        split.Panel2.Controls.Add(lower);
        summary.DataError += (_, e) => e.ThrowException = false;
        Controls.Add(split);
        Controls.Add(topHost);
        start.Click += (_, _) => Begin(60);
        stop.Click += async (_, _) => await End();
        folder.Click += (_, _) => { try { Process.Start("explorer.exe", Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "out"))); } catch { } };
        load.Click += (_, _) => LoadFile();
        grid.SelectionChanged += (_, _) => ShowCauses();
        grid.DataError += (_, e) => e.ThrowException = false;
        causes.DataError += (_, e) => e.ThrowException = false;
        Refill();
    }

    /// <summary>Starts recording the running game. Returns false (with a message) when there is nothing to record.</summary>
    public bool Begin(double warmupSeconds)
    {
        if (recorder?.Running == true) return true;
        var m = monitor(); string lp = livePath();
        if (m == null || !m.FpsAvailable || lp == null || !File.Exists(lp)) { status.Text = "Nothing to record yet: start the game with the profiler (Launch game with profiler) first."; return false; }
        recorder = new PlayRecorder(monitor, lp, modPattern());
        shown = recorder.Result;
        UpdateChart();
        recorder.Status += s => BeginInvoke(() =>
        {
            status.Text = s + $"  ·  results: {Path.GetFileName(recorder?.ResultPath ?? "")}";
            if ((DateTime.UtcNow - lastSummary).TotalSeconds >= 4 && recorder != null) { lastSummary = DateTime.UtcNow; FillSummary(recorder.SummaryRows()); UpdateChart(); }
        });
        recorder.Dip += _ => BeginInvoke(() => { Refill(); });
        recorder.Start(warmupSeconds);
        start.Enabled = false; stop.Enabled = true;
        status.Text = warmupSeconds > 0 ? $"Recording (ignoring the first {warmupSeconds:F0} s while the world loads)…" : "Recording…";
        return true;
    }

    /// <summary>Makes sure a recording is running (no warm-up). Used by the scenario runner.</summary>
    public bool EnsureRecording() => Begin(0);

    /// <summary>Stops the recording and returns the saved file.</summary>
    public async Task<string> StopAndSave()
    {
        string path = recorder?.ResultPath;
        await End();
        return path;
    }

    public async Task End()
    {
        if (recorder == null) return;
        stop.Enabled = false;
        await recorder.Stop();
        status.Text = $"Saved {recorder.Result.Hitches.Count} dips to {recorder.ResultPath}";
        start.Enabled = true;
        recorder = null;
    }

    /// <summary>Frame rate of every 2-second slice, plus a marker series where a dip was found.</summary>
    void UpdateChart()
    {
        List<SliceStat> s; List<PlayHitch> dips;
        lock (shown) { s = shown.SliceStats.ToList(); dips = shown.Hitches.ToList(); }
        if (s.Count < 2) { chart.Set("Frame rate", "fps per 2 s slice", new()); return; }
        var xs = Enumerable.Range(0, s.Count).Select(i => i * 2.0).ToArray();
        var fps = s.Select(x => Math.Min(240, 1000.0 / Math.Max(1, x.AvgMs))).ToArray();
        var lines = new List<SeriesChart.Line> { new("average fps", Theme.Accent, xs, fps) };
        if (dips.Count > 0)
        {
            var lowest = s.Select(x => Math.Min(240, 1000.0 / Math.Max(1, x.WorstMs))).ToArray();
            lines.Add(new SeriesChart.Line("worst frame in slice (as fps)", Theme.Bad, xs, lowest));
        }
        chart.Set("Frame rate over the recording", "fps", lines);
    }

    void FillSummary(List<(string Item, string Value)> rows)
    {
        var t = new DataTable();
        t.Columns.Add("Item", typeof(string));
        t.Columns.Add("Value", typeof(string));
        foreach (var r in rows) t.Rows.Add(r.Item, r.Value);
        summary.DataSource = null;
        summary.DataSource = t;
        Theme.StyleGrid(summary);
        if (summary.Columns.Count > 1) { summary.Columns[0].FillWeight = 220; summary.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; }
    }

    void LoadFile()
    {
        using var dlg = new OpenFileDialog { Filter = "Play recordings (play_*.json)|play_*.json", InitialDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "out")) };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { shown = PlayResult.Load(dlg.FileName); status.Text = "Loaded " + Path.GetFileName(dlg.FileName); Refill(); UpdateChart(); }
        catch (Exception ex) { status.Text = "Could not load: " + ex.Message; }
    }

    void Refill()
    {
        int keep = grid.CurrentRow?.Index ?? -1;
        var t = new DataTable();
        t.Columns.Add("Time", typeof(string));
        t.Columns.Add("Worst frame (ms)", typeof(double));
        t.Columns.Add("Lowest FPS", typeof(double));
        t.Columns.Add("Time lost (ms)", typeof(double));
        t.Columns.Add("GC pause (ms)", typeof(double));
        t.Columns.Add("Window activity", typeof(string));
        t.Columns.Add("Biggest cause", typeof(string));
        List<PlayHitch> list;
        lock (shown) list = shown.Hitches.ToList();
        foreach (var h in list.OrderByDescending(x => x.LostMs))
            t.Rows.Add(h.Time, h.WorstMs, h.LowFps, h.LostMs, h.GcMs, h.Hint, h.Causes.Count > 0 ? h.Causes[0].Method : "");
        grid.DataSource = null;
        grid.DataSource = t;
        Theme.StyleGrid(grid);
        foreach (DataGridViewColumn c in grid.Columns) if (c.ValueType == typeof(double)) c.DefaultCellStyle.Format = "F0";
        if (grid.Columns.Contains("Biggest cause")) grid.Columns["Biggest cause"].FillWeight = 200;
        if (grid.Columns.Contains("Window activity")) grid.Columns["Window activity"].FillWeight = 160;
        ShowCauses();
    }

    void ShowCauses()
    {
        var d = new DataTable();
        d.Columns.Add("Extra time in this dip, compared with normal play", typeof(string));
        d.Columns.Add("ms", typeof(double));
        string time = grid.CurrentRow?.Cells["Time"].Value as string;
        PlayHitch h; lock (shown) h = shown.Hitches.FirstOrDefault(x => x.Time == time);
        if (h != null) foreach (var c in h.Causes) d.Rows.Add((c.Rebirth ? "● " : "  ") + c.Method, c.ExcessMs);
        causes.DataSource = d;
        Theme.StyleGrid(causes);
        if (causes.Columns.Count > 1) causes.Columns[1].DefaultCellStyle.Format = "F0";
        foreach (DataGridViewRow r in causes.Rows)
            if ((r.Cells[0].Value as string ?? "").StartsWith("●")) r.Cells[0].Style.ForeColor = Theme.Accent;
    }
}
