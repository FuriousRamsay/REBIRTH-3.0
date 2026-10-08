using System.Data;
using System.Diagnostics;
using System.Text.Json;

namespace RebirthProfiler;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // --compare "a1.json;a2.json" "b1.json;b2.json" [out.txt]: print a before/after report without opening a window
        int ci = Array.IndexOf(args, "--compare");
        if (ci >= 0 && args.Length > ci + 2)
        {
            var A = args[ci + 1].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(BenchResult.Load).ToList();
            var B = args[ci + 2].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(BenchResult.Load).ToList();
            string outFile = args.Length > ci + 3 ? args[ci + 3] : Path.Combine(AppContext.BaseDirectory, "..", "out", "bench", "comparison.txt");
            File.WriteAllText(outFile, BenchCompare.ToText(A, B), new System.Text.UTF8Encoding(true));
            return;
        }
        // --session profile|benchmark file label [notes]: import an existing result into the Sessions library
        int si = Array.IndexOf(args, "--session");
        if (si >= 0 && args.Length > si + 3)
        {
            var meta = SessionStore.Save(args[si + 2], args[si + 1], args[si + 3], args.Length > si + 4 ? args[si + 4] : "", captureCode: !args.Contains("--imported"));
            File.WriteAllText(Path.Combine(SessionStore.Dir, "last_import.txt"), meta.Id);
            return;
        }
        // --diff-sessions "labelA;labelB" "labelC" out.txt: compare saved sessions (matched by label text) without opening a window
        int di = Array.IndexOf(args, "--diff-sessions");
        if (di >= 0 && args.Length > di + 3)
        {
            var all = SessionStore.List();
            List<SessionMeta> Pick(string spec) => spec.Split(';', StringSplitOptions.RemoveEmptyEntries).SelectMany(t => all.Where(s => s.Label.Contains(t.Trim(), StringComparison.OrdinalIgnoreCase))).Distinct().ToList();
            File.WriteAllText(args[di + 3], SessionCompare.ReportText(Pick(args[di + 1]), Pick(args[di + 2]), args.Contains("--mod") ? args[Array.IndexOf(args, "--mod") + 1] : "Rebirth"), new System.Text.UTF8Encoding(true));
            return;
        }
        // --play-compare before.json after.json [out.txt]: scenario-by-scenario table with a verdict; exit code 2 when a scenario lost more than 10% FPS
        int pc = Array.IndexOf(args, "--play-compare");
        if (pc >= 0 && args.Length > pc + 2)
        {
            var before = PlayResult.Load(args[pc + 1]); var after = PlayResult.Load(args[pc + 2]);
            var sbc = new System.Text.StringBuilder();
            bool regression = false;
            sbc.AppendLine($"{"scenario",-28} {"before",7} {"after",7} {"change",8} {"frame ms",10} {"99th ms",10}  verdict");
            foreach (var l in before.Segments.Select(s => s.Label).Union(after.Segments.Select(s => s.Label)).Where(l => !l.EndsWith("(running)") && !l.StartsWith("end")))
            {
                var sa = before.Segments.FirstOrDefault(s => s.Label == l); var sb2 = after.Segments.FirstOrDefault(s => s.Label == l);
                if (sa == null || sb2 == null) { sbc.AppendLine($"{l,-28} (only in {(sa == null ? "after" : "before")})"); continue; }
                double ch = sa.AvgFps > 0 ? (sb2.AvgFps - sa.AvgFps) / sa.AvgFps * 100 : 0;
                string verdict = ch <= -10 ? "REGRESSION" : ch >= 10 ? "faster" : "same";
                if (ch <= -10) regression = true;
                sbc.AppendLine($"{l,-28} {sa.AvgFps,6:F0}  {sb2.AvgFps,6:F0}  {ch,7:+0;-0}%  {sa.MedianMs,4:F1}>{sb2.MedianMs,-4:F1}  {sa.P99Ms,4:F0}>{sb2.P99Ms,-4:F0}  {verdict}");
            }
            sbc.AppendLine($"garbage collections per minute: {before.CollectionsPerMinute:F1} > {after.CollectionsPerMinute:F1};  dips found: {before.Hitches.Count} > {after.Hitches.Count}");
            string text = sbc.ToString();
            if (args.Length > pc + 3 && !args[pc + 3].StartsWith("--")) File.WriteAllText(args[pc + 3], text, new System.Text.UTF8Encoding(true)); else Console.Write(text);
            Environment.Exit(regression ? 2 : 0);
            return;
        }
        // --play-window play_x.json HH:mm:ss HH:mm:ss: frame statistics of a recording between two clock times
        int pw = Array.IndexOf(args, "--play-window");
        if (pw >= 0 && args.Length > pw + 3)
        {
            var pr = PlayResult.Load(args[pw + 1]);
            File.WriteAllText(Path.ChangeExtension(args[pw + 1], null) + $"_{args[pw + 2].Replace(":", "")}.window.txt", pr.Summarise(args[pw + 2], args[pw + 3]) + Environment.NewLine);
            return;
        }
        // --hitch-report result.json [out.txt]: per-window frame-time table and the code that cost the extra time
        int hi = Array.IndexOf(args, "--hitch-report");
        if (hi >= 0 && args.Length > hi + 1)
        {
            var hr = HitchResult.Load(args[hi + 1]);
            string mod = args.Contains("--mod") ? args[Array.IndexOf(args, "--mod") + 1] : "Rebirth";
            var sbh = new System.Text.StringBuilder();
            foreach (var g in hr.Trials.GroupBy(x => x.Step))
            {
                var ok = g.Where(x => x.Opened).ToList();
                sbh.AppendLine($"== {g.Key}: opened {ok.Count} of {g.Count()}");
                foreach (var x in g) sbh.AppendLine($"   open {x.Index}: baseline {x.BaselineMs:F0} ms, worst {x.WorstMs:F0} ms, lowest {x.LowFps:F0} fps, time lost {x.StallMs:F0} ms, slow frames {x.SlowFrames}, recovered after {x.RecoverySec:F1} s{(x.Opened ? "" : "  [window did not open]")}");
                foreach (var c in HitchAnalysis.Culprits(ok, "").Take(12)) sbh.AppendLine($"      {c.ExcessMs,7:F0} ms  {(c.Method.Contains(mod, StringComparison.OrdinalIgnoreCase) ? "*" : " ")} {c.Method}");
                sbh.AppendLine();
            }
            File.WriteAllText(args.Length > hi + 2 && !args[hi + 2].StartsWith("--") ? args[hi + 2] : Path.ChangeExtension(args[hi + 1], ".txt"), sbh.ToString(), new System.Text.UTF8Encoding(true));
            return;
        }
        // --report profile.json [out.txt]: write the Findings / Rebirth cost / Allocations tables as text
        int ri = Array.IndexOf(args, "--report");
        if (ri >= 0 && args.Length > ri + 1)
        {
            var p = Profile.Load(args[ri + 1]);
            string outFile = args.Length > ri + 2 ? args[ri + 2] : Path.ChangeExtension(args[ri + 1], ".report.txt");
            File.WriteAllText(outFile, ReportText.Build(p, args.Contains("--mod") ? args[Array.IndexOf(args, "--mod") + 1] : "Rebirth"), new System.Text.UTF8Encoding(true));
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
        {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "..", "out", "app_errors.log"), e.Exception + Environment.NewLine); } catch { }
            MessageBox.Show(e.Exception.Message + Environment.NewLine + "(details in out\\app_errors.log)", "Rebirth Profiler");
        };
        var form = new MainForm(args.FirstOrDefault(a => !a.StartsWith("--")));
        var wa = args.FirstOrDefault(a => a.StartsWith("--watch="));
        if (wa != null) form.Watch(wa.Substring(8));
        var ga = args.FirstOrDefault(a => a.StartsWith("--gameargs="));
        if (ga != null) form.SetGameArgs(ga.Substring(11));
        if (args.Contains("--alloc")) form.SetTrackAllocations(true);
        var ta = args.FirstOrDefault(a => a.StartsWith("--tab="));
        if (ta != null) form.SelectTab(int.Parse(ta.Substring(6)));
        var ba = args.FirstOrDefault(a => a.StartsWith("--bench="));
        if (ba != null)
        {
            int Arg(string k, int d) { var v = args.FirstOrDefault(a => a.StartsWith(k + "=")); return v != null && int.TryParse(v.Substring(k.Length + 1), out var n) ? n : d; }
            form.Shown += (_, _) => form.BeginInvoke(() => form.StartBenchmarkFromCommandLine(ba.Substring(8), Arg("--warmup", 30), Arg("--duration", 120), !args.Contains("--keep-open")));
        }
        if (args.Contains("--launch")) form.Shown += (_, _) => form.BeginInvoke(form.LaunchGame);
        if (args.Contains("--playmode")) form.SetPlayMode(true);
        if (args.Contains("--nogamesense")) form.AppendGameArg("-nogamesense");
        if (args.Contains("--threads")) form.SetSampleThreads(true);
        var ra = args.FirstOrDefault(a => a.StartsWith("--record="));
        if (ra != null) form.Shown += (_, _) => form.BeginInvoke(() => form.StartPlayRecordingWhenReady(double.Parse(ra.Substring(9))));
        if (args.Contains("--scenarios")) form.Shown += (_, _) => form.BeginInvoke(() => form.StartScenariosFromCommandLine(args.Contains("--close-when-done")));
        var ha = args.FirstOrDefault(a => a.StartsWith("--hitch="));
        if (ha != null) form.Shown += (_, _) => form.BeginInvoke(() => form.StartHitchFromCommandLine(int.Parse(ha.Substring(8)), args.Contains("--close-when-done")));
        Application.Run(form);
    }
}

sealed class MainForm : Form
{
    Profile cur, baseline;
    bool[] mod;
    readonly DataGridView findingsGrid = MakeGrid(wrap: true), costGrid = MakeGrid(wrap: true), classGrid = MakeGrid(), methodGrid = MakeGrid(),
        allocCodeGrid = MakeGrid(wrap: true), allocTypeGrid = MakeGrid();
    readonly RoundedTextBox modPattern = new() { Text = "Rebirth", Width = 130 };
    readonly RoundedTextBox filter = new() { Width = 300, PlaceholderText = "Filter classes and methods" };
    readonly Toggle trackAlloc = new() { Text = "Track allocations (slower)", AutoSize = true };
    readonly Toggle freshSave = new() { Text = "Fresh test save each run", AutoSize = true, Checked = true };
    readonly Toggle threadsToggle = new() { Text = "Sample worker threads", AutoSize = true };
    readonly Toggle playMode = new() { Text = "Let me play (no test bridge)", AutoSize = true };
    readonly FlatTabs tabs = new() { Dock = DockStyle.Fill };
    readonly Label summary = new() { Dock = DockStyle.Fill, Tag = "muted", AutoEllipsis = true };
    readonly Banner warn = new() { Dock = DockStyle.Fill };
    readonly Label hint = new()
    {
        Dock = DockStyle.Bottom, Height = 30, Tag = "muted", Padding = new Padding(2, 8, 0, 0), Font = Theme.Small,
        Text = "ms/s = milliseconds per second of game time on the main thread (÷60 for ms per frame at 60 FPS).  Self = a method's own code, Inclusive = it plus what it calls."
    };
    readonly RoundedTextBox gameArgs = new() { Text = DefaultArgs(), PlaceholderText = "Launch arguments", Dock = DockStyle.Fill };
    readonly System.Windows.Forms.Timer liveTimer = new() { Interval = 2000 };
    DataTable classTable, methodTable;
    BenchmarkTab benchTab;
    readonly LiveStrip liveStrip = new();
    readonly System.Windows.Forms.Timer liveUiTimer = new() { Interval = 1000 };
    LiveMonitor autoMon;
    bool benchOwnsMonitor;
    SessionsTab sessionsTab;
    HitchTab hitchTab;
    PlayTab playTab;
    ScenariosTab scenariosTab;
    readonly CallTreePage callTree = new();
    bool rebuilding;
    string livePath; DateTime liveStamp; int gamePid;

    // Same arguments the Game Bridge uses, so the game reaches the test save on its own.
    static string DefaultArgs()
    {
        try
        {
            var cfg = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "GameBridge", "gamebridge.config.json"));
            using var d = JsonDocument.Parse(File.ReadAllText(cfg));
            var r = d.RootElement;
            var s = $"-noeac -rebirthbridge={r.GetProperty("port").GetInt32()} -LoadSaveGame=true \"-GameWorld={r.GetProperty("world").GetString()}\" \"-GameName={r.GetProperty("save").GetString()}\" -SkipNewsScreen=true -SkipSpawnButton=true -skipintro";
            if (r.GetProperty("windowed").GetBoolean()) s += $" -screen-fullscreen 0 -screen-width {r.GetProperty("width").GetInt32()} -screen-height {r.GetProperty("height").GetInt32()}";
            return s;
        }
        catch { return "-noeac"; }
    }

    public void SetGameArgs(string a) => gameArgs.Text = a;
    public void SetTrackAllocations(bool on) => trackAlloc.Checked = on;
    public void SetPlayMode(bool on) => playMode.Checked = on;
    public void SetSampleThreads(bool on) => threadsToggle.Checked = on;
    public void AppendGameArg(string a) { if (!gameArgs.Text.Contains(a)) gameArgs.Text = (gameArgs.Text + " " + a).Trim(); }
    public void SelectTab(int i) => tabs.SelectedIndex = i;
    /// <summary>Headless hitch test: waits for the save, runs the window-open test, saves the result, optionally closes game and app.</summary>
    /// <summary>Headless scenario run: waits for the save, records, runs the stress scenarios, saves, optionally closes game and app.</summary>
    public async void StartScenariosFromCommandLine(bool close)
    {
        tabs.SelectedTab = scenariosTab;
        await hitchTab.WaitUntilInGame();
        await scenariosTab.RunAll();
        if (!close) return;
        foreach (var g in Process.GetProcessesByName("7DaysToDie")) { try { g.CloseMainWindow(); } catch { } }
        await Task.Delay(12000);
        foreach (var g in Process.GetProcessesByName("7DaysToDie")) { try { g.Kill(); } catch { } }
        Close();
    }

    public async void StartHitchFromCommandLine(int trials, bool close)
    {
        tabs.SelectedTab = hitchTab;
        await hitchTab.RunWhenReady(trials);
        if (!close) return;
        foreach (var g in Process.GetProcessesByName("7DaysToDie")) { try { g.CloseMainWindow(); } catch { } }
        await Task.Delay(12000);
        foreach (var g in Process.GetProcessesByName("7DaysToDie")) { try { g.Kill(); } catch { } }
        Close();
    }

    public void StartBenchmarkFromCommandLine(string label, int warmup, int duration, bool close)
    {
        tabs.SelectedTab = benchTab;
        benchTab.Configure(label, warmup, duration, close);
        benchTab.ExitWhenDone = true;
        _ = benchTab.RunBenchmark();
    }
    public void Watch(string path) { livePath = path; liveStamp = DateTime.MinValue; liveTimer.Start(); }

    public MainForm(string initial)
    {
        Text = "Rebirth Profiler";
        Width = 1500; Height = 950; StartPosition = FormStartPosition.CenterScreen;   // size when restored; the window always starts maximized
        AllowDrop = true;
        DragEnter += (_, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) =>
        {
            var f = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (f.Length > 0) Open(f[0], ModifierKeys.HasFlag(Keys.Shift) && cur != null);
        };
        liveTimer.Tick += (_, _) => LiveTick();
        foreach (var g in new[] { findingsGrid, costGrid, classGrid, methodGrid, allocCodeGrid, allocTypeGrid }) g.DataError += (_, e) => e.ThrowException = false;

        float k = Theme.Scale(this);
        int S(int px) => (int)(px * k);
        HandleCreated += (_, _) => Theme.ApplyWindowChrome(this);

        // ---- header: brand, game state, main action -------------------------------------------------------------------
        var brandMark = new Label { Text = "✻", AutoSize = true, Font = new Font(Theme.Body.FontFamily, 18f, FontStyle.Bold), Tag = "accent", Margin = new Padding(0, 0, 8, 0) };
        var brandName = new Label { Text = "Rebirth Profiler", AutoSize = true, Font = Theme.Brand, Margin = new Padding(0, 6, 20, 0) };
        var stateChip = new Chip { Margin = new Padding(0, 7, 0, 0) };
        stateChip.Set("game not running", Chip.S.Idle);
        liveStrip.State += (text, s) => stateChip.Set(text, s);
        var left = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        left.Controls.AddRange(new Control[] { brandMark, brandName, stateChip });
        var launch = new AppButton { Text = "▶   Launch game with profiler", Kind = AppButton.K.Primary, AutoSize = true, Margin = Padding.Empty };
        launch.Click += (_, _) => LaunchGame();
        var themeBtn = new AppButton { Text = Theme.Dark ? "Light mode" : "Dark mode", Kind = AppButton.K.Ghost, AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        themeBtn.Click += (_, _) => { Theme.SetDark(!Theme.Dark); themeBtn.Text = Theme.Dark ? "Light mode" : "Dark mode"; };
        var right = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.RightToLeft };
        right.Controls.AddRange(new Control[] { launch, themeBtn });
        var header = new Panel { Dock = DockStyle.Top, Height = S(56) };
        header.Controls.Add(right);
        header.Controls.Add(left);

        // ---- options card: arguments, switches, the everyday actions ----------------------------------------------------
        AppButton Btn(string text, Action click, AppButton.K kind = AppButton.K.Secondary)
        {
            var b = new AppButton { Text = text, Kind = kind, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
            b.Click += (_, _) => click();
            return b;
        }
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.RightToLeft };
        actions.Controls.AddRange(new Control[]
        {
            Btn("Reset counters", ResetCounters), Btn("Save session…", SaveSession), Btn("Clear baseline", () => { baseline = null; Rebuild(); }, AppButton.K.Ghost),
            Btn("Compare with baseline…", () => Pick(true)), Btn("Open profile…", () => Pick(false)),
        });
        var switches = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        switches.Controls.Add(trackAlloc);
        switches.Controls.Add(freshSave);
        switches.Controls.Add(threadsToggle);
        switches.Controls.Add(playMode);
        string removedBridgeArg = "-rebirthbridge=8765";
        playMode.CheckedChanged += (_, _) =>
        {
            // Show exactly what will run: play mode has no test bridge, so no bot can act on the character.
            var m = System.Text.RegularExpressions.Regex.Match(gameArgs.Text, @"-rebirthbridge=\S+");
            if (playMode.Checked)
            {
                if (m.Success) { removedBridgeArg = m.Value; gameArgs.Text = System.Text.RegularExpressions.Regex.Replace(gameArgs.Text, @"\s*-rebirthbridge=\S+", ""); }
                summary.Text = "Play mode: the game starts without the test bridge, so nothing plays for you. The Play recording tab records every dip while you play. (Hitches and Benchmark need the bridge and will not run.)";
            }
            else if (!m.Success)
            {
                int i = gameArgs.Text.IndexOf("-noeac", StringComparison.Ordinal);
                gameArgs.Text = i >= 0 ? gameArgs.Text.Insert(i + 6, " " + removedBridgeArg) : (gameArgs.Text + " " + removedBridgeArg).Trim();
                summary.Text = "Test mode: the bridge is on again (needed for Hitches and Benchmark).";
            }
        };
        switches.Controls.Add(new Label { Text = "Mod code contains", Tag = "muted", AutoSize = true, Margin = new Padding(6, 7, 8, 0) });
        switches.Controls.Add(modPattern);
        var optionsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        optionsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        optionsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        optionsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, S(42)));
        optionsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, S(40)));
        gameArgs.Margin = new Padding(0, 0, 0, S(8));
        optionsGrid.Controls.Add(gameArgs, 0, 0);
        optionsGrid.SetColumnSpan(gameArgs, 2);
        optionsGrid.Controls.Add(switches, 0, 1);
        optionsGrid.Controls.Add(actions, 1, 1);
        var optionsCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(14), S(12), S(14), S(8)) };
        optionsCard.Controls.Add(optionsGrid);

        modPattern.TextChanged += (_, _) => Rebuild();
        filter.TextChanged += (_, _) => ApplyFilter();

        // ---- tab pages: each result table sits on a card ---------------------------------------------------------------
        PagePanel Page(string title, Control content)
        {
            var p = new PagePanel(title) { Padding = new Padding(0, S(12), 0, 0) };
            var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
            content.Dock = DockStyle.Fill;
            card.Controls.Add(content);
            p.Controls.Add(card);
            return p;
        }
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = S(260), SplitterWidth = 3 };
        split.Panel1.Controls.Add(Ui.WithSlimScroll(classGrid));
        split.Panel2.Controls.Add(Ui.WithSlimScroll(methodGrid));
        var methodsBody = new Panel { Dock = DockStyle.Fill };
        var filterRow = new Panel { Dock = DockStyle.Top, Height = S(46), Padding = new Padding(S(6), S(6), S(6), S(6)) };
        filter.Dock = DockStyle.Left;
        filterRow.Controls.Add(filter);
        methodsBody.Controls.Add(split);
        methodsBody.Controls.Add(filterRow);
        classGrid.SelectionChanged += (_, _) => ApplyFilter();
        var allocSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = S(300), SplitterWidth = 3 };
        allocSplit.Panel1.Controls.Add(Ui.WithSlimScroll(allocCodeGrid));
        allocSplit.Panel2.Controls.Add(Ui.WithSlimScroll(allocTypeGrid));

        // Findings and Rebirth cost: one line per row in the table, the full text of the selected row underneath.
        Control WithDetail(DataGridView grid, DetailPanel detail)
        {
            var body = new Panel { Dock = DockStyle.Fill };
            detail.Dock = DockStyle.Bottom;
            detail.Height = S(150);
            body.Controls.Add(Ui.WithSlimScroll(grid));
            body.Controls.Add(detail);
            return body;
        }
        string Cell(DataGridView grid, string column) =>
            grid.CurrentRow != null && grid.Columns.Contains(column) ? grid.CurrentRow.Cells[column].Value?.ToString() ?? "" : "";
        var findingsDetail = new DetailPanel();
        findingsGrid.SelectionChanged += (_, _) =>
        {
            if (findingsGrid.CurrentRow == null) return;
            string trigger = Cell(findingsGrid, "Trigger / detail");
            findingsDetail.ShowDetail(Cell(findingsGrid, "Rebirth code"),
                $"{Cell(findingsGrid, "Severity")}   ·   {Cell(findingsGrid, "Category")}   ·   driven by {Cell(findingsGrid, "Driven by (entry point)")}",
                (trigger.Length > 0 ? "Trigger: " + trigger + Environment.NewLine + Environment.NewLine : "") + Cell(findingsGrid, "What to do"));
        };
        var costDetail = new DetailPanel();
        costGrid.SelectionChanged += (_, _) =>
        {
            if (costGrid.CurrentRow == null) return;
            costDetail.ShowDetail(Cell(costGrid, "Rebirth entry point"),
                $"{Cell(costGrid, "Main thread ms/s")} ms/s on the main thread   ·   {Cell(costGrid, "Other threads %")}% of samples on other threads",
                "Where the time goes:" + Environment.NewLine + Cell(costGrid, "Where the time goes (innermost calls)").Replace("; ", Environment.NewLine));
        };

        benchTab = new BenchmarkTab(() => gameArgs.Text, () => freshSave.Checked);
        sessionsTab = new SessionsTab(path => { Open(path, false); tabs.SelectedIndex = 0; }, path => { benchTab.LoadResult(path); tabs.SelectedTab = benchTab; }, () => modPattern.Text,
            (pa, pb) => { scenariosTab.Show(pa, pb); tabs.SelectedTab = scenariosTab; });
        hitchTab = new HitchTab(() => autoMon, () => livePath, () => modPattern.Text);
        playTab = new PlayTab(() => autoMon, () => livePath, () => modPattern.Text);
        scenariosTab = new ScenariosTab(() => livePath, () => playTab.EnsureRecording(), () => playTab.StopAndSave());
        benchTab.SessionSaved = () => sessionsTab.Reload();
        scenariosTab.SessionSaved = () => sessionsTab.Reload();
        benchTab.LiveSink = s => liveStrip.Show(s, "recording");
        benchTab.ActiveChanged = active => { benchOwnsMonitor = active; if (active) DisposeAutoMonitor(); };
        liveUiTimer.Tick += (_, _) => AutoLiveTick();
        liveUiTimer.Start();
        FormClosing += (_, _) => DisposeAutoMonitor();
        tabs.TabPages.AddRange(new PagePanel[]
        {
            Page("Findings", WithDetail(findingsGrid, findingsDetail)), Page("Rebirth cost", WithDetail(costGrid, costDetail)),
            Page("Classes & methods", methodsBody), callTree, Page("Allocations", allocSplit),
            benchTab, hitchTab, playTab, scenariosTab, sessionsTab,
        });

        // ---- assemble ------------------------------------------------------------------------------------------------
        Panel Spaced(Control c, int height, int bottom)
        {
            var p = new Panel { Dock = DockStyle.Top, Height = S(height + bottom), Padding = new Padding(0, 0, 0, S(bottom)) };
            c.Dock = DockStyle.Fill;
            p.Controls.Add(c);
            return p;
        }
        var warnRow = Spaced(warn, 38, 10);
        warn.VisibleChanged += (_, _) => warnRow.Visible = warn.Visible;
        warnRow.Visible = false;
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(S(24), S(4), S(24), S(10)) };
        body.Controls.Add(tabs);
        body.Controls.Add(hint);
        body.Controls.Add(Spaced(liveStrip, 66, 12));
        body.Controls.Add(warnRow);
        body.Controls.Add(Spaced(summary, 26, 4));
        body.Controls.Add(Spaced(optionsCard, 116, 12));
        body.Controls.Add(header);
        Controls.Add(body);
        summary.Text = "Press Launch game with profiler, or open a profile.json (or drop one on this window).";
        Theme.Style(this);
        Theme.Changed += () => { Theme.Style(this); Theme.ApplyWindowChrome(this); Invalidate(true); };
        WindowState = FormWindowState.Maximized;
        if (initial != null && File.Exists(initial)) Open(initial, false);
    }

    static DataGridView MakeGrid(bool wrap = false)
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None,
        };
        return g;
    }

    // ---- launching and live updates ------------------------------------------------------------------------------

    public void LaunchGame()
    {
        string appDir = AppContext.BaseDirectory;
        string exe = GameLauncher.FindGameExe(appDir);
        if (exe == null)
        {
            using var dlg = new OpenFileDialog { Filter = "7DaysToDie.exe|7DaysToDie.exe", Title = "Locate 7DaysToDie.exe" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            exe = dlg.FileName;
        }
        string module = GameLauncher.FindModule(appDir);
        if (module == null) { MessageBox.Show(this, "mono-profiler-rebirthprof.dll was not found next to the app or in ..\\native. Run build.ps1.", "Rebirth Profiler"); return; }
        if (Process.GetProcessesByName("7DaysToDie").Length > 0)
        {
            MessageBox.Show(this, "7 Days To Die is already running. Close it first: the profiler has to be loaded when the game starts.", "Rebirth Profiler");
            return;
        }
        string outDir = Path.GetFullPath(Path.Combine(appDir, "..", "out"));
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, $"profile_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        try
        {
            if (freshSave.Checked && TestSave.IsConfigured()) summary.Text = TestSave.Restore();
            string launchArgs = playMode.Checked ? System.Text.RegularExpressions.Regex.Replace(gameArgs.Text, @"\s*-rebirthbridge=\S+", "") : gameArgs.Text;
            GameLauncher.SampleThreads = threadsToggle.Checked;
            gamePid = GameLauncher.Launch(exe, launchArgs, module, outPath, trackAlloc.Checked);
            if (playMode.Checked) StartPlayRecordingWhenReady();
            livePath = outPath; liveStamp = DateTime.MinValue; cur = null;
            summary.Text = $"Game started (pid {gamePid}) with the profiler attached. Play, then press Reset counters once the save has loaded. The report appears ~20 s after start and refreshes every few seconds.";
            liveTimer.Start();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not launch with profiler"); }
    }

    /// <summary>Play mode: once the game window is up and frames are being captured, start recording dips (the first 150 s are ignored while the world loads).</summary>
    public async void StartPlayRecordingWhenReady(double warmup = 150)
    {
        tabs.SelectedTab = playTab;
        for (int i = 0; i < 120; i++)
        {
            await Task.Delay(2000);
            if (autoMon != null && autoMon.FpsAvailable && livePath != null && File.Exists(livePath)) { playTab.Begin(warmup); return; }
        }
    }

    void SaveSession()
    {
        if (cur == null || !File.Exists(cur.Path)) { summary.Text = "Open or capture a profile first."; return; }
        if (!PromptDialog.Ask(this, "Save session", Path.GetFileNameWithoutExtension(cur.Path), out string label, out string notes)) return;
        try
        {
            var s = SessionStore.Save(cur.Path, "profile", label, notes, $"{cur.WallSec:F0} s, {cur.Total:N0} samples, allocations {(cur.TrackAlloc ? "on" : "off")}");
            sessionsTab.Reload();
            summary.Text = $"Saved session \"{s.Label}\" (code {s.CodeVersion}). Find it in the Sessions tab.";
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not save the session:" + Environment.NewLine + ex.Message, "Rebirth Profiler"); }
    }

    // ---- always-on live readout ------------------------------------------------------------------------------------
    void DisposeAutoMonitor()
    {
        var m = autoMon; autoMon = null;
        try { m?.Dispose(); } catch { }
    }

    /// <summary>Once a second: attach to the running game if there is one (however it was started) and update the strip.</summary>
    void AutoLiveTick()
    {
        if (benchOwnsMonitor) return;                       // a benchmark / recording has its own monitor and feeds the strip itself
        try
        {
            if (autoMon != null && autoMon.HasExited) { DisposeAutoMonitor(); liveStrip.Clear("game not running"); return; }
            if (autoMon == null)
            {
                var game = Process.GetProcessesByName("7DaysToDie").FirstOrDefault();
                if (game == null) { liveStrip.Clear("game not running"); return; }
                uint tid = 0;
                if (game.Id == gamePid && GameLauncher.LastMainThreadId != 0) tid = GameLauncher.LastMainThreadId;
                else { try { tid = (uint)game.Threads.Cast<ProcessThread>().OrderBy(t => t.StartTime).First().Id; } catch { } }
                autoMon = new LiveMonitor(game.Id, tid);
            }
            var s = autoMon.Sample();
            liveStrip.Show(s, autoMon.FpsAvailable ? $"game running (pid {autoMon.Pid})" : "game running, no frame capture");
        }
        catch (Exception ex)
        {
            DisposeAutoMonitor();
            liveStrip.Clear("live readout error: " + ex.Message);
        }
    }

    void ResetCounters()
    {
        if (livePath == null) return;
        try { File.WriteAllText(livePath + ".reset", ""); summary.Text = "Counters reset: measuring from now."; } catch { }
    }

    void LiveTick()
    {
        if (livePath == null) return;
        if (File.Exists(livePath))
        {
            var stamp = File.GetLastWriteTimeUtc(livePath);
            if (stamp != liveStamp)
            {
                liveStamp = stamp;
                try { cur = Profile.Load(livePath); Rebuild(); } catch (IOException) { liveStamp = DateTime.MinValue; } catch (JsonException) { liveStamp = DateTime.MinValue; }
            }
        }
        bool running = gamePid != 0 && Process.GetProcesses().Any(p => p.Id == gamePid);
        if (!running && gamePid != 0) { liveTimer.Stop(); Text = "Rebirth Profiler (game closed, final report shown)"; }
    }

    void Pick(bool asBaseline)
    {
        using var dlg = new OpenFileDialog { Filter = "Profile (*.json)|*.json", Title = asBaseline ? "Choose baseline profile" : "Choose profile" };
        if (dlg.ShowDialog(this) == DialogResult.OK) Open(dlg.FileName, asBaseline);
    }

    void Open(string path, bool asBaseline)
    {
        try
        {
            var p = Profile.Load(path);
            if (asBaseline) baseline = p; else cur = p;
            Rebuild();
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "..", "out", "app_errors.log"), ex + Environment.NewLine); } catch { }
            MessageBox.Show(this, "Could not read profile:" + Environment.NewLine + ex.Message, "Rebirth Profiler");
        }
    }

    // ---- building the views --------------------------------------------------------------------------------------

    static void Bind(DataGridView g, DataTable t, string sort)
    {
        g.DataSource = null;
        g.DataSource = new DataView(t) { Sort = sort };
    }

    void Rebuild()
    {
        if (cur == null) return;
        rebuilding = true;
        try
        {
            mod = Analyzer.ModMask(cur, modPattern.Text);
            BuildSummary();
            BuildFindings();
            BuildCost();
            BuildMethods();
            BuildAllocations();
            callTree.SetProfile(cur, mod);
        }
        finally { rebuilding = false; }
        ApplyFilter();
    }

    void BuildSummary()
    {
        double nativePct = cur.Pct(cur.NoManaged);
        double mainNative = cur.MainTotal == 0 ? 0 : 100.0 * cur.MainNoManaged / cur.MainTotal;
        summary.Text = $"{Path.GetFileName(cur.Path)}    {cur.WallSec:F0} s measured, {cur.Total:N0} samples ({cur.MainTotal:N0} on the main thread)    " +
                       $"main thread: {100 - mainNative:F0}% in managed code, {mainNative:F0}% native/engine/waiting    {cur.Methods.Count:N0} methods" +
                       (baseline != null ? $"\r\nComparing against baseline: {Path.GetFileName(baseline.Path)}  (positive Δ = costs more than baseline)" : "");
        string w = !cur.Stacks.Any() ? "This report has no stack data (older format): only the Classes & methods tab is available."
                 : cur.Total < 2000 ? $"Only {cur.Total} samples: results are noisy. Measure longer for reliable numbers."
                 : mainNative > 60 ? "Most main-thread time is outside managed code (rendering, physics, GC, waiting). Script code is not the main cost here."
                 : null;
        warn.Text = w ?? "";
    }

    void BuildFindings()
    {
        var t = new DataTable();
        t.Columns.Add("Severity", typeof(string));
        t.Columns.Add("Impact ms/s", typeof(double));
        t.Columns.Add("Other threads %", typeof(double));
        t.Columns.Add("Alloc KB/s", typeof(double));
        t.Columns.Add("Category", typeof(string));
        t.Columns.Add("Rebirth code", typeof(string));
        t.Columns.Add("Driven by (entry point)", typeof(string));
        t.Columns.Add("Trigger / detail", typeof(string));
        t.Columns.Add("What to do", typeof(string));
        t.Columns.Add("Score", typeof(double));
        foreach (var f in Analyzer.Findings(cur, mod).Take(200))
            t.Rows.Add(f.Severity, Math.Round(f.MsPerSec, 2), Math.Round(f.OtherPct, 2), Math.Round(f.AllocKBps, 1),
                f.Category, f.Code, f.Entry, f.Trigger, f.Advice, f.Score);
        Bind(findingsGrid, t, "[Score] DESC");
        if (findingsGrid.Columns.Contains("Score")) findingsGrid.Columns["Score"].Visible = false;
        SizeWide(findingsGrid, "Driven by (entry point)", 190);
        if (findingsGrid.Columns.Contains("What to do")) { var wc = findingsGrid.Columns["What to do"]; wc.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; wc.MinimumWidth = 300; }
        SizeWide(findingsGrid, "Trigger / detail", 170);
        SizeWide(findingsGrid, "Rebirth code", 210);
    }

    void BuildCost()
    {
        var baseCosts = baseline != null && baseline.Stacks.Any()
            ? Analyzer.EntryCosts(baseline, Analyzer.ModMask(baseline, modPattern.Text)).ToDictionary(r => r.Key, r => r.MainMs, StringComparer.Ordinal)
            : null;
        var t = new DataTable();
        t.Columns.Add("Rebirth entry point", typeof(string));
        t.Columns.Add("Main thread ms/s", typeof(double));
        t.Columns.Add("ms/frame @60", typeof(double));
        t.Columns.Add("Other threads %", typeof(double));
        if (baseCosts != null) t.Columns.Add("Δ ms/s vs baseline", typeof(double));
        t.Columns.Add("Where the time goes (innermost calls)", typeof(string));
        foreach (var r in Analyzer.EntryCosts(cur, mod).Take(200))
        {
            var row = new List<object> { r.Entry, Math.Round(r.MainMs, 2), Math.Round(r.MainMs / 60, 3), Math.Round(r.OtherPct, 2) };
            if (baseCosts != null) row.Add(Math.Round(r.MainMs - baseCosts.GetValueOrDefault(r.Key), 2));
            row.Add(r.Callees);
            t.Rows.Add(row.ToArray());
        }
        Bind(costGrid, t, "[Main thread ms/s] DESC");
        SizeWide(costGrid, "Rebirth entry point", 320);
        SizeWide(costGrid, "Where the time goes (innermost calls)", 420);
    }

    void BuildMethods()
    {
        var baseByName = baseline?.Methods.ToDictionary(m => m.Full, m => m);
        var baseClassSelf = baseline?.Methods.GroupBy(m => m.Cls).ToDictionary(g => g.Key, g => g.Sum(m => m.Self));

        methodTable = new DataTable();
        methodTable.Columns.Add("Class", typeof(string));
        methodTable.Columns.Add("Method", typeof(string));
        methodTable.Columns.Add("Signature", typeof(string));
        methodTable.Columns.Add("Inclusive %", typeof(double));
        methodTable.Columns.Add("Self %", typeof(double));
        if (baseline != null) methodTable.Columns.Add("Δ incl %", typeof(double));
        foreach (var m in cur.Methods)
        {
            var row = new List<object> { m.Cls, m.Name, m.Sig, Math.Round(cur.Pct(m.Incl), 2), Math.Round(cur.Pct(m.Self), 2) };
            if (baseline != null)
            {
                double b = baseByName.TryGetValue(m.Full, out var bm) ? baseline.Pct(bm.Incl) : 0;
                row.Add(Math.Round(cur.Pct(m.Incl) - b, 2));
            }
            methodTable.Rows.Add(row.ToArray());
        }

        classTable = new DataTable();
        classTable.Columns.Add("Class", typeof(string));
        classTable.Columns.Add("Methods", typeof(int));
        classTable.Columns.Add("Hottest inclusive %", typeof(double));
        classTable.Columns.Add("Self %", typeof(double));
        if (baseline != null) classTable.Columns.Add("Δ self %", typeof(double));
        foreach (var g in cur.Methods.GroupBy(m => m.Cls))
        {
            var self = cur.Pct(g.Sum(m => m.Self));
            var row = new List<object> { g.Key, g.Count(), Math.Round(cur.Pct(g.Max(m => m.Incl)), 2), Math.Round(self, 2) };
            if (baseline != null)
            {
                double b = baseClassSelf.TryGetValue(g.Key, out var bs) ? baseline.Pct(bs) : 0;
                row.Add(Math.Round(self - b, 2));
            }
            classTable.Rows.Add(row.ToArray());
        }

        string keepClass = classGrid.SelectedRows.Count > 0 ? classGrid.SelectedRows[0].Cells[0].Value as string : null;
        Bind(classGrid, classTable, "[Hottest inclusive %] DESC");
        Bind(methodGrid, methodTable, "[Inclusive %] DESC");
        classGrid.ClearSelection();
        if (keepClass != null)
            foreach (DataGridViewRow r in classGrid.Rows)
                if (r.Cells[0].Value as string == keepClass) { r.Selected = true; break; }
    }

    void BuildAllocations()
    {
        var byCode = new DataTable();
        byCode.Columns.Add("Allocating code (innermost Rebirth frame)", typeof(string));
        byCode.Columns.Add("KB/s", typeof(double));
        byCode.Columns.Add("Top types", typeof(string));
        byCode.Columns.Add("Driven by (entry point)", typeof(string));
        var byType = new DataTable();
        byType.Columns.Add("Allocated type", typeof(string));
        byType.Columns.Add("KB/s", typeof(double));
        if (cur.Allocs.Any())
        {
            foreach (var r in Analyzer.AllocByCode(cur, mod).Take(200)) byCode.Rows.Add(r.Code, Math.Round(r.KBps, 1), r.Types, r.Entry);
            foreach (var r in Analyzer.AllocByType(cur).Take(200)) byType.Rows.Add(r.Code, Math.Round(r.KBps, 1));
        }
        else byCode.Rows.Add(cur.TrackAlloc ? "(no allocations sampled yet)" : "Allocation tracking was off. Tick \"Track allocations\" and launch again.", 0.0, "", "");
        Bind(allocCodeGrid, byCode, "[KB/s] DESC");
        Bind(allocTypeGrid, byType, "[KB/s] DESC");
        SizeWide(allocCodeGrid, "Allocating code (innermost Rebirth frame)", 400);
    }

    static void SizeWide(DataGridView g, string col, int minWidth)
    {
        if (!g.Columns.Contains(col)) return;
        var c = g.Columns[col];
        c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        c.Width = minWidth;
    }

    static string Lit(string s) => s.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");

    void ApplyFilter()
    {
        if (rebuilding || classTable == null || classGrid.DataSource is not DataView cv || methodGrid.DataSource is not DataView mv) return;
        string q = Lit(filter.Text.Trim());
        cv.RowFilter = q.Length == 0 ? "" : $"[Class] LIKE '%{q}%'";

        string sel = classGrid.SelectedRows.Count > 0 ? classGrid.SelectedRows[0].Cells[0].Value as string : null;
        string mf = "";
        if (sel != null) mf = $"[Class] = '{sel.Replace("'", "''")}'";
        else if (q.Length > 0) mf = $"[Class] LIKE '%{q}%' OR [Method] LIKE '%{q}%'";
        mv.RowFilter = mf;
    }
}


