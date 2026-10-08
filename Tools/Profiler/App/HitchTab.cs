using System.Data;
using System.Drawing.Drawing2D;

namespace RebirthProfiler;

/// <summary>Opens each Rebirth window several times in the running game and shows the frame-time dip and what caused it.</summary>
sealed class HitchTab : PagePanel
{
    readonly Func<LiveMonitor> monitor;
    readonly Func<string> livePath;
    readonly Func<string> modPattern;
    readonly RoundedTextBox trialsBox = new() { Width = 70, Text = "4" };
    readonly AppButton run = new() { Text = "Run hitch test", Kind = AppButton.K.Primary, AutoSize = true };
    readonly AppButton cancel = new() { Text = "Cancel", Kind = AppButton.K.Ghost, AutoSize = true, Enabled = false };
    readonly AppButton load = new() { Text = "Load results…", AutoSize = true };
    readonly Label status = new() { Dock = DockStyle.Fill, Tag = "muted", Text = "Start the game with the profiler, wait until you are in the save, then run the test. It opens each window several times." };
    readonly DataGridView grid = new()
    {
        ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BorderStyle = BorderStyle.None,
    };
    readonly DataGridView culprits = new()
    {
        ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BorderStyle = BorderStyle.None,
    };
    readonly SeriesChart chart = new() { Dock = DockStyle.Fill };
    HitchResult result = new();
    CancellationTokenSource cts;

    public HitchTab(Func<LiveMonitor> monitor, Func<string> livePath, Func<string> modPattern) : base("Hitches")
    {
        this.monitor = monitor; this.livePath = livePath; this.modPattern = modPattern;
        float k = Theme.Scale(this);
        int S(int px) => (int)(px * k);
        Padding = new Padding(0, S(12), 0, 0);
        Label Caption(string t) => new Label { Text = t, Tag = "muted", AutoSize = true, Margin = new Padding(0, 9, 8, 0) };

        var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = S(42), WrapContents = false };
        row.Controls.AddRange(new Control[] { Caption("Opens per window"), trialsBox, run, cancel, load });
        var statusHost = new Panel { Dock = DockStyle.Top, Height = S(46) };
        statusHost.Controls.Add(status);
        var runCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(14), S(10), S(14), S(6)) };
        runCard.Controls.Add(statusHost);
        runCard.Controls.Add(row);
        var runHost = new Panel { Dock = DockStyle.Top, Height = S(42 + 46 + 16 + 12), Padding = new Padding(0, 0, 0, S(12)) };
        runHost.Controls.Add(runCard);

        var gridCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
        gridCard.Controls.Add(Ui.WithSlimScroll(grid));
        var chartCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(14), S(10), S(14), S(10)) };
        chartCard.Controls.Add(chart);
        var culpritCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
        culpritCard.Controls.Add(Ui.WithSlimScroll(culprits));

        var bottom = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = S(760), SplitterWidth = 12 };
        bottom.Panel1.Controls.Add(chartCard);
        bottom.Panel2.Controls.Add(culpritCard);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = S(300), SplitterWidth = 12 };
        split.Panel1.Controls.Add(gridCard);
        split.Panel2.Controls.Add(bottom);
        Controls.Add(split);
        Controls.Add(runHost);

        run.Click += async (_, _) => await RunTest();
        cancel.Click += (_, _) => cts?.Cancel();
        load.Click += (_, _) => LoadFile();
        grid.SelectionChanged += (_, _) => ShowSelected();
        grid.DataError += (_, e) => e.ThrowException = false;
        culprits.DataError += (_, e) => e.ThrowException = false;
        Theme.Changed += ShowSelected;
        Fill();
    }

    async Task RunTest()
    {
        if (!int.TryParse(trialsBox.Text.Trim(), out int trials) || trials < 1 || trials > 12) { status.Text = "Enter 1 to 12 opens per window."; return; }
        cts = new CancellationTokenSource();
        run.Enabled = false; cancel.Enabled = true;
        string progressPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "out", "hitch_status.txt"));
        var log = new Progress<string>(s => { status.Text = s; File.WriteAllText(progressPath, s); });
        try
        {
            result = await HitchRunner.Run(monitor, livePath(), HitchRunner.Steps, trials, log, cts.Token);
            int bad = result.Trials.Count(t => !t.Opened);
            status.Text = $"Done: {result.Trials.Count} opens measured." + (bad > 0 ? $" {bad} did not open the window and are marked in the table." : "");
        }
        catch (OperationCanceledException) { status.Text = "Cancelled."; }
        catch (Exception ex) { status.Text = "Could not run: " + ex.Message; }
        finally { File.WriteAllText(progressPath, status.Text); run.Enabled = true; cancel.Enabled = false; cts.Dispose(); cts = null; Fill(); }
    }

    /// <summary>Waits (up to 10 minutes) until the game is in the save with the profiler attached, then runs the test.</summary>
    public async Task RunWhenReady(int trials)
    {
        trialsBox.Text = trials.ToString();
        await WaitUntilInGame();
        await RunTest();
    }

    /// <summary>Waits (up to 10 minutes) until the game is in the save with the profiler attached, then lets the world settle.</summary>
    public async Task WaitUntilInGame()
    {
        var until = DateTime.UtcNow.AddMinutes(10);
        while (DateTime.UtcNow < until)
        {
            var m = monitor();
            string lp = livePath();
            if (m != null && m.FpsAvailable && lp != null && File.Exists(lp))
            {
                try
                {
                    var sf = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "GameBridge", "out", "session.json"));
                    if (File.Exists(sf))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(sf));
                        var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                        http.DefaultRequestHeaders.Add("X-Bridge-Token", doc.RootElement.GetProperty("token").GetString());
                        if ((await http.GetStringAsync($"http://127.0.0.1:{doc.RootElement.GetProperty("port").GetInt32()}/ping")).Contains("\"state\":\"ingame\"")) break;
                    }
                }
                catch { }
            }
            status.Text = "Waiting for the game to load the save…";
            await Task.Delay(3000);
        }
        await Task.Delay(20000);      // let the world settle after loading
    }

    void LoadFile()
    {
        using var dlg = new OpenFileDialog { Filter = "Hitch results (hitch_*.json)|hitch_*.json|JSON (*.json)|*.json", InitialDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "out")) };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { result = HitchResult.Load(dlg.FileName); status.Text = "Loaded " + Path.GetFileName(dlg.FileName); Fill(); }
        catch (Exception ex) { status.Text = "Could not load: " + ex.Message; }
    }

    static double Median(IEnumerable<double> v)
    {
        var a = v.OrderBy(x => x).ToList();
        return a.Count == 0 ? 0 : a.Count % 2 == 1 ? a[a.Count / 2] : (a[a.Count / 2 - 1] + a[a.Count / 2]) / 2;
    }

    void Fill()
    {
        var t = new DataTable();
        t.Columns.Add("Window", typeof(string));
        t.Columns.Add("Opens", typeof(string));
        t.Columns.Add("Worst frame (ms)", typeof(double));
        t.Columns.Add("Lowest FPS", typeof(double));
        t.Columns.Add("Time lost (ms)", typeof(double));
        t.Columns.Add("Recovers in (s)", typeof(double));
        t.Columns.Add("First open: worst (ms)", typeof(double));
        t.Columns.Add("Later opens: worst (ms)", typeof(double));
        t.Columns.Add("Verdict", typeof(string));
        foreach (var g in result.Trials.GroupBy(x => x.Step))
        {
            var ok = g.Where(x => x.Opened).OrderBy(x => x.Index).ToList();
            if (ok.Count == 0) { t.Rows.Add(g.Key, $"0 of {g.Count()}", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, "did not open"); continue; }
            double worst = Median(ok.Select(x => x.WorstMs)), low = Median(ok.Select(x => x.LowFps)), lost = Median(ok.Select(x => x.StallMs)), rec = Median(ok.Select(x => x.RecoverySec));
            double first = ok[0].WorstMs;
            double later = ok.Count > 1 ? Median(ok.Skip(1).Select(x => x.WorstMs)) : double.NaN;
            string verdict = worst >= 100 || lost >= 200 ? "Visible hitch" : worst >= 50 || lost >= 80 ? "Noticeable" : worst >= 33 ? "Minor" : "Smooth";
            if (ok.Count > 1 && first > later * 1.6 && first >= 50) verdict += " (worse the first time)";
            t.Rows.Add(g.Key, $"{ok.Count} of {g.Count()}", worst, low, lost, rec, first, later, verdict);
        }
        grid.DataSource = null;
        grid.DataSource = t;
        Theme.StyleGrid(grid);
        foreach (DataGridViewColumn c in grid.Columns) if (c.ValueType == typeof(double)) c.DefaultCellStyle.Format = "F0";
        if (grid.Columns.Contains("Lowest FPS")) grid.Columns["Lowest FPS"].DefaultCellStyle.Format = "F0";
        if (grid.Columns.Contains("Recovers in (s)")) grid.Columns["Recovers in (s)"].DefaultCellStyle.Format = "F1";
        if (grid.Columns.Contains("Window")) grid.Columns["Window"].FillWeight = 160;
        foreach (DataGridViewRow r in grid.Rows)
        {
            string v = r.Cells["Verdict"].Value as string ?? "";
            r.Cells["Verdict"].Style.ForeColor = v.StartsWith("Visible") ? Theme.Bad : v.StartsWith("Noticeable") || v.StartsWith("did not") ? Theme.Warn : v.StartsWith("Minor") ? Theme.Muted : Theme.Good;
        }
        ShowSelected();
    }

    void ShowSelected()
    {
        string step = grid.CurrentRow?.Cells["Window"].Value as string;
        if (step == null) { chart.Set("Frame time around the click", "ms per frame", new()); culprits.DataSource = null; return; }
        var trials = result.Trials.Where(x => x.Step == step && x.Opened).OrderBy(x => x.Index).ToList();
        var lines = new List<SeriesChart.Line>();
        for (int i = 0; i < trials.Count; i++)
        {
            var tr = trials[i];
            var c = i == 0 ? Theme.Bad : Color.FromArgb(i == 1 ? 255 : 200, Theme.Accent);
            // The chart's x axis starts at 0, so shift by the seconds before the click.
            lines.Add(new SeriesChart.Line(i == 0 ? "first open" : $"open {i + 1}", c, tr.T.Select(x => x + HitchAnalysis.BeforeSec).ToArray(), tr.Ms.ToArray()));
        }
        chart.Set($"{step}: frame time (click at {HitchAnalysis.BeforeSec:F0} s)", "ms per frame", lines);

        var d = new DataTable();
        d.Columns.Add("Extra time in the 4 s after the click", typeof(string));
        d.Columns.Add("ms (average per open)", typeof(double));
        string mod = modPattern();
        foreach (var c in HitchAnalysis.Culprits(trials, mod).Take(40))
            d.Rows.Add((c.Rebirth ? "● " : "  ") + c.Method, c.ExcessMs);
        culprits.DataSource = d;
        Theme.StyleGrid(culprits);
        if (culprits.Columns.Count > 1) culprits.Columns[1].DefaultCellStyle.Format = "F0";
        foreach (DataGridViewRow r in culprits.Rows)
            if ((r.Cells[0].Value as string ?? "").StartsWith("●")) r.Cells[0].Style.ForeColor = Theme.Accent;
    }
}


