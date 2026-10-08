using System.Data;

namespace RebirthProfiler;

/// <summary>Runs the stress scenarios on the running game and compares two recordings segment by segment (before / after a change).</summary>
sealed class ScenariosTab : PagePanel
{
    readonly Func<string> livePath;
    readonly Func<bool> startRecording;            // makes sure the Play recording is running
    readonly Func<Task<string>> stopRecording;     // stops it and returns the saved file
    readonly AppButton run = new() { Text = "Run scenarios on the running game", Kind = AppButton.K.Primary, AutoSize = true };
    readonly AppButton cancel = new() { Text = "Cancel", Kind = AppButton.K.Ghost, AutoSize = true, Enabled = false };
    readonly AppButton loadA = new() { Text = "Load recording A (before)…", AutoSize = true };
    readonly AppButton loadB = new() { Text = "Load recording B (after)…", AutoSize = true };
    readonly Label status = new() { Dock = DockStyle.Fill, Tag = "muted", Text = "Launch the game with the profiler (test mode, not \"Let me play\"), wait for the save, then run the scenarios: open field, motorcycle in open country, city, motorcycle into the city, and a 100-zombie horde. Load two recordings to compare them." };
    readonly DataGridView grid = new() { ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BorderStyle = BorderStyle.None };
    readonly DataGridView detail = new() { ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BorderStyle = BorderStyle.None };
    public Action SessionSaved;
    readonly AppButton save = new() { Text = "Save B to Sessionsâ€¦", Kind = AppButton.K.Ghost, AutoSize = true };
    string pathA, pathB;
    PlayResult a, b;
    string nameA = "", nameB = "";
    CancellationTokenSource cts;

    public ScenariosTab(Func<string> livePath, Func<bool> startRecording, Func<Task<string>> stopRecording) : base("Scenarios")
    {
        this.livePath = livePath; this.startRecording = startRecording; this.stopRecording = stopRecording;
        float k = Theme.Scale(this);
        int S(int px) => (int)(px * k);
        Padding = new Padding(0, S(12), 0, 0);
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = S(42), WrapContents = false };
        row.Controls.AddRange(new Control[] { run, cancel, loadA, loadB, save });
        var statusHost = new Panel { Dock = DockStyle.Top, Height = S(46) };
        statusHost.Controls.Add(status);
        var top = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(14), S(10), S(14), S(6)) };
        top.Controls.Add(statusHost);
        top.Controls.Add(row);
        var topHost = new Panel { Dock = DockStyle.Top, Height = S(42 + 46 + 16 + 12), Padding = new Padding(0, 0, 0, S(12)) };
        topHost.Controls.Add(top);
        var gridCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
        gridCard.Controls.Add(Ui.WithSlimScroll(grid));
        var detailCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
        detailCard.Controls.Add(Ui.WithSlimScroll(detail));
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = S(260), SplitterWidth = 12 };
        split.Panel1.Controls.Add(gridCard);
        split.Panel2.Controls.Add(detailCard);
        Controls.Add(split);
        Controls.Add(topHost);
        run.Click += async (_, _) => await RunAll();
        cancel.Click += (_, _) => cts?.Cancel();
        save.Click += (_, _) => SaveB();
        loadA.Click += (_, _) => Load(true);
        loadB.Click += (_, _) => Load(false);
        grid.SelectionChanged += (_, _) => ShowDetail();
        grid.DataError += (_, e) => e.ThrowException = false;
        detail.DataError += (_, e) => e.ThrowException = false;
        Fill();
    }

    /// <summary>Runs every scenario (used by the button and by --scenarios). Returns the saved recording.</summary>
    public async Task<string> RunAll()
    {
        cts = new CancellationTokenSource();
        run.Enabled = false; cancel.Enabled = true;
        string saved = null;
        try
        {
            if (!startRecording()) { status.Text = "Start the game with the profiler first."; return null; }
            await Task.Delay(3000);
            var log = new Progress<string>(s => status.Text = s);
            string notes = await ScenarioRunner.Run(livePath(), log, cts.Token);
            saved = await stopRecording();
            status.Text = "Done. Recording: " + saved + "   notes: " + notes;
            if (saved != null && File.Exists(saved)) { b = PlayResult.Load(saved); nameB = Path.GetFileName(saved); pathB = saved; Fill(); }
        }
        catch (OperationCanceledException) { status.Text = "Cancelled."; saved = await stopRecording(); }
        catch (Exception ex) { status.Text = "Could not run: " + ex.Message; }
        finally { run.Enabled = true; cancel.Enabled = false; cts?.Dispose(); cts = null; }
        return saved;
    }

    void Load(bool asA)
    {
        using var dlg = new OpenFileDialog { Filter = "Play recordings (play_*.json)|play_*.json", InitialDirectory = ScenarioRunner.OutDir };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var r = PlayResult.Load(dlg.FileName);
            if (asA) { a = r; nameA = Path.GetFileName(dlg.FileName); pathA = dlg.FileName; } else { b = r; nameB = Path.GetFileName(dlg.FileName); pathB = dlg.FileName; }
            Fill();
        }
        catch (Exception ex) { status.Text = "Could not load: " + ex.Message; }
    }

    /// <summary>Loads recordings from the Sessions library (either may be null to keep the current one).</summary>
    public void Show(string pathForA, string pathForB)
    {
        try
        {
            if (pathForA != null) { a = PlayResult.Load(pathForA); pathA = pathForA; nameA = Path.GetFileName(pathForA); }
            if (pathForB != null) { b = PlayResult.Load(pathForB); pathB = pathForB; nameB = Path.GetFileName(pathForB); }
            Fill();
        }
        catch (Exception ex) { status.Text = "Could not open: " + ex.Message; }
    }

    void SaveB()
    {
        if (pathB == null || !File.Exists(pathB)) { status.Text = "Run the scenarios or load a recording as B first."; return; }
        if (!PromptDialog.Ask(this, "Save recording", Path.GetFileNameWithoutExtension(pathB), out string label, out string notes)) return;
        try
        {
            int dips = b?.Hitches.Count ?? 0;
            var s = SessionStore.Save(pathB, "play", label, notes, $"{b?.Segments.Count(x => !x.Label.EndsWith("(running)")) ?? 0} segments, {dips} dips, {b?.CollectionsPerMinute:F1} collections/min");
            status.Text = $"Saved session \"{s.Label}\" (code {s.CodeVersion}). Find it in the Sessions tab.";
            SessionSaved?.Invoke();
        }
        catch (Exception ex) { status.Text = "Could not save: " + ex.Message; }
    }

    static string Fps(SegmentStat s) => s == null ? "" : s.AvgFps.ToString("F0");

    void Fill()
    {
        var t = new DataTable();
        t.Columns.Add("Scenario", typeof(string));
        t.Columns.Add("A: FPS", typeof(string));
        t.Columns.Add("B: FPS", typeof(string));
        t.Columns.Add("Change", typeof(string));
        t.Columns.Add("A: frame ms", typeof(string));
        t.Columns.Add("B: frame ms", typeof(string));
        t.Columns.Add("A: 99th ms", typeof(string));
        t.Columns.Add("B: 99th ms", typeof(string));
        t.Columns.Add("A: slow slices", typeof(string));
        t.Columns.Add("B: slow slices", typeof(string));
        var labels = new List<string>();
        foreach (var r in new[] { a, b }) if (r != null) foreach (var s in r.Segments.Where(x => !x.Label.EndsWith("(running)") && !x.Label.StartsWith("end"))) if (!labels.Contains(s.Label)) labels.Add(s.Label);
        foreach (var l in labels)
        {
            var sa = a?.Segments.FirstOrDefault(x => x.Label == l); var sb = b?.Segments.FirstOrDefault(x => x.Label == l);
            string change = sa != null && sb != null && sa.AvgFps > 0 ? $"{(sb.AvgFps - sa.AvgFps) / sa.AvgFps * 100:+0;-0}%" : "";
            t.Rows.Add(l, Fps(sa), Fps(sb), change, sa?.MedianMs.ToString("F1") ?? "", sb?.MedianMs.ToString("F1") ?? "", sa?.P99Ms.ToString("F0") ?? "", sb?.P99Ms.ToString("F0") ?? "",
                sa != null ? $"{sa.SlicesUnder30Fps} of {sa.Slices}" : "", sb != null ? $"{sb.SlicesUnder30Fps} of {sb.Slices}" : "");
        }
        grid.DataSource = null;
        grid.DataSource = t;
        Theme.StyleGrid(grid);
        if (grid.Columns.Contains("Scenario")) grid.Columns["Scenario"].FillWeight = 190;
        foreach (DataGridViewRow r in grid.Rows)
        {
            string c = r.Cells["Change"].Value as string ?? "";
            r.Cells["Change"].Style.ForeColor = c.StartsWith("+") ? Theme.Good : c.StartsWith("-") ? Theme.Bad : Theme.Muted;
        }
        if (a != null || b != null) status.Text = $"A: {(nameA.Length > 0 ? nameA : "(none)")}    B: {(nameB.Length > 0 ? nameB : "(none)")}" + (a != null && b != null ? "    Select a scenario for the method-level difference." : "");
        ShowDetail();
    }

    void ShowDetail()
    {
        var d = new DataTable();
        d.Columns.Add("Main-thread time (ms per second), biggest differences first", typeof(string));
        d.Columns.Add("A", typeof(double));
        d.Columns.Add("B", typeof(double));
        d.Columns.Add("B - A", typeof(double));
        string label = grid.CurrentRow?.Cells["Scenario"].Value as string;
        if (label != null)
        {
            var sa = a?.Segments.FirstOrDefault(x => x.Label == label); var sb = b?.Segments.FirstOrDefault(x => x.Label == label);
            var da = sa?.Inclusive.ToDictionary(x => x.Key, x => x.Value) ?? new(); var db = sb?.Inclusive.ToDictionary(x => x.Key, x => x.Value) ?? new();
            var keys = da.Keys.Union(db.Keys).Where(k => !k.StartsWith("(wrapper"));
            var rows = keys.Select(k => (k, av: da.GetValueOrDefault(k), bv: db.GetValueOrDefault(k))).OrderByDescending(x => sa != null && sb != null ? Math.Abs(x.bv - x.av) : Math.Max(x.av, x.bv)).Take(40);
            foreach (var r in rows) d.Rows.Add(r.k, r.av, r.bv, r.bv - r.av);
        }
        detail.DataSource = null;
        detail.DataSource = d;
        Theme.StyleGrid(detail);
        foreach (DataGridViewColumn c in detail.Columns) if (c.ValueType == typeof(double)) c.DefaultCellStyle.Format = "F1";
        if (detail.Columns.Count > 0) detail.Columns[0].FillWeight = 260;
    }
}
