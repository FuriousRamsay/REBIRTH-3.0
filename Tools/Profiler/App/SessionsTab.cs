using System.Data;
using System.Text;

namespace RebirthProfiler;

static class PromptDialog
{
    /// <summary>Small themed modal asking for a label and optional notes. Returns false if cancelled.</summary>
    public static bool Ask(IWin32Window owner, string title, string defaultLabel, out string label, out string notes)
    {
        using var f = new Form
        {
            Text = title, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
        };
        float k = Theme.Scale(f);
        int S(int px) => (int)(px * k);
        f.ClientSize = new Size(S(520), S(300));
        f.Padding = new Padding(S(22), S(18), S(22), S(18));
        var heading = new Label { Text = "Save this session", Font = Theme.Title, AutoSize = true, Left = S(22), Top = S(18) };
        var l1 = new Label { Text = "Label", Tag = "muted", AutoSize = true, Left = S(22), Top = S(54) };
        var t1 = new RoundedTextBox { Left = S(22), Top = S(76), Width = S(476), Text = defaultLabel, PlaceholderText = "e.g. before the compass fix" };
        var l2 = new Label { Text = "Notes (what was different, what you expect to see)", Tag = "muted", AutoSize = true, Left = S(22), Top = S(122) };
        var t2 = new RoundedTextBox { Left = S(22), Top = S(144), Width = S(476), Height = S(80) };
        t2.Inner.Multiline = true;
        var ok = new AppButton { Text = "Save", Kind = AppButton.K.Primary, Left = S(392), Top = S(244), Width = S(106), Height = S(36) };
        var cancel = new AppButton { Text = "Cancel", Left = S(278), Top = S(244), Width = S(106), Height = S(36) };
        ok.Click += (_, _) => f.DialogResult = DialogResult.OK;
        cancel.Click += (_, _) => f.DialogResult = DialogResult.Cancel;
        f.Controls.AddRange(new Control[] { heading, l1, t1, l2, t2, ok, cancel });
        f.CancelButton = new Button();     // Esc closes the dialog
        ((Button)f.CancelButton).Click += (_, _) => f.DialogResult = DialogResult.Cancel;
        f.HandleCreated += (_, _) => Theme.ApplyWindowChrome(f);
        Theme.Style(f);
        bool result = f.ShowDialog(owner) == DialogResult.OK && t1.Text.Trim().Length > 0;
        label = t1.Text.Trim(); notes = t2.Text.Trim();
        return result;
    }
}

sealed class SessionsTab : PagePanel
{
    readonly DataGridView list = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, BorderStyle = BorderStyle.None,
    };
    readonly FlatTabs results = new() { Dock = DockStyle.Fill };
    readonly Label header = new() { Dock = DockStyle.Top, Height = 62, Tag = "muted", Text = "Select sessions and press \"Set as A\" / \"Set as B\", then Compare." };
    readonly RoundedTextBox filter = new() { Width = 170, Text = "Rebirth" };
    readonly Action<string> openProfile;
    readonly Action<string> openBenchmark;
    readonly Func<string> modPattern;
    readonly Action<string, string> comparePlay;   // A, B: open two Play recordings in the Scenarios tab
    List<SessionMeta> sessions = new();
    readonly HashSet<string> groupA = new(), groupB = new();
    List<(string title, string unit, List<SessionCompare.DiffRow> rows, bool modFilter)> lastDiffs = new();
    string lastHeader = "";

    public SessionsTab(Action<string> openProfile, Action<string> openBenchmark, Func<string> modPattern, Action<string, string> comparePlay = null) : base("Sessions")
    {
        this.openProfile = openProfile; this.openBenchmark = openBenchmark; this.modPattern = modPattern; this.comparePlay = comparePlay;
        float k = Theme.Scale(this);
        int S(int px) => (int)(px * k);
        Padding = new Padding(0, S(12), 0, 0);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        AppButton B(string text, Action a, AppButton.K kind = AppButton.K.Secondary)
        {
            var b = new AppButton { Text = text, Kind = kind, AutoSize = true };
            b.Click += (_, _) => a();
            bar.Controls.Add(b);
            return b;
        }
        B("Set selected as A (before)", () => Assign(groupA, groupB));
        B("Set selected as B (after)", () => Assign(groupB, groupA));
        B("Compare A ↔ B", Compare, AppButton.K.Primary);
        B("Open", OpenSelected);
        B("Edit notes…", EditNotes);
        B("Export comparison…", Export);
        B("Refresh", Reload, AppButton.K.Ghost);
        B("Delete", DeleteSelected, AppButton.K.Ghost);
        bar.Controls.Add(new Label { Text = "Filter items", Tag = "muted", AutoSize = true, Margin = new Padding(14, 9, 8, 0) });
        bar.Controls.Add(filter);
        filter.TextChanged += (_, _) => { if (lastDiffs.Count > 0) ShowDiffs(); };
        var barHost = new Panel { Dock = DockStyle.Top, Height = S(42 + 12), Padding = new Padding(0, 0, 0, S(12)) };
        barHost.Controls.Add(bar);

        var listCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
        listCard.Controls.Add(Ui.WithSlimScroll(list));
        var resultCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(14), S(10), S(14), S(8)) };
        resultCard.Controls.Add(results);
        var headerHost = new Panel { Dock = DockStyle.Top, Height = header.Height };
        header.Dock = DockStyle.Fill;
        headerHost.Controls.Add(header);
        resultCard.Controls.Add(headerHost);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = S(210), SplitterWidth = 12 };
        split.Panel1.Controls.Add(listCard);
        split.Panel2.Controls.Add(resultCard);
        Controls.Add(split);
        Controls.Add(barHost);
        list.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenSelected(); };
        list.DataError += (_, e) => e.ThrowException = false;
        list.DataBindingComplete += (_, _) => FixColumns();
        Reload();
    }

    IEnumerable<SessionMeta> Selected()
    {
        var ids = list.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Cells["Id"].Value as string).Where(s => s != null).ToHashSet();
        return sessions.Where(s => ids.Contains(s.Id));
    }

    public void Reload()
    {
        sessions = SessionStore.List();
        var t = new DataTable();
        foreach (var c in new[] { "Id", "Group", "Saved", "Kind", "Label", "Code version", "Game", "Summary", "Notes" }) t.Columns.Add(c, typeof(string));
        foreach (var s in sessions)
            t.Rows.Add(s.Id, groupA.Contains(s.Id) ? "A" : groupB.Contains(s.Id) ? "B" : "", s.Created.Replace('T', ' '), s.Kind, s.Label, s.CodeVersion, s.GameVersion, s.Summary, s.Notes);
        list.DataSource = null;
        list.DataSource = t;
        FixColumns();
    }

    void FixColumns()
    {
        if (list.Columns.Contains("Id")) list.Columns["Id"].Visible = false;
        if (list.Columns.Contains("Notes")) { list.Columns["Notes"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; list.Columns["Notes"].MinimumWidth = 300; }
    }

    void Assign(HashSet<string> target, HashSet<string> other)
    {
        foreach (var s in Selected()) { target.Add(s.Id); other.Remove(s.Id); }
        Reload();
    }

    void OpenSelected()
    {
        var s = Selected().FirstOrDefault();
        if (s == null) return;
        if (s.Kind == "profile") openProfile(s.DataPath);
        else if (s.Kind == "play") comparePlay?.Invoke(null, s.DataPath);
        else openBenchmark(s.DataPath);
    }

    void EditNotes()
    {
        var s = Selected().FirstOrDefault();
        if (s == null) return;
        if (PromptDialog.Ask(this, "Edit session", s.Label, out string label, out string notes)) { s.Label = label; s.Notes = notes; SessionStore.UpdateNotes(s); Reload(); }
    }

    void DeleteSelected()
    {
        var sel = Selected().ToList();
        if (sel.Count == 0) return;
        if (MessageBox.Show(this, $"Delete {sel.Count} session(s) permanently?", "Rebirth Profiler", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        foreach (var s in sel) { SessionStore.Delete(s); groupA.Remove(s.Id); groupB.Remove(s.Id); }
        Reload();
    }

    static string Describe(string name, List<SessionMeta> g)
    {
        if (g.Count == 0) return $"{name}: (none)";
        var versions = g.Select(s => s.CodeVersion).Distinct().ToList();
        return $"{name}: {g.Count} session(s) [{string.Join(", ", g.Select(s => s.Label))}]  code: {string.Join(" | ", versions)}";
    }

    void Compare()
    {
        var a = sessions.Where(s => groupA.Contains(s.Id)).ToList();
        var b = sessions.Where(s => groupB.Contains(s.Id)).ToList();
        if (a.Count == 0 || b.Count == 0) { header.Text = "Choose at least one session for A and one for B first."; return; }
        if (a.Concat(b).Select(s => s.Kind).Distinct().Count() > 1) { header.Text = "A and B must be the same kind (both profiles or both benchmarks)."; return; }

        string same = a.Concat(b).All(s => s.ModDllHash.Length > 0) && a.Select(s => s.ModDllHash).Concat(b.Select(s => s.ModDllHash)).Distinct().Count() == 1
            ? "\nNote: A and B were taken with the identical mod build, so any differences are noise or game state, not code changes." : "";
        lastHeader = Describe("A", a) + "\n" + Describe("B", b) + same;
        header.Text = lastHeader;
        lastDiffs.Clear();
        results.TabPages.Clear();
        try
        {
            if (a[0].Kind == "play")
            {
                comparePlay?.Invoke(a[0].DataPath, b[0].DataPath);
                header.Text = lastHeader + "\nOpened in the Scenarios tab.";
                return;
            }
            if (a[0].Kind == "benchmark")
            {
                var ra = a.Select(s => BenchResult.Load(s.DataPath)).ToList();
                var rb = b.Select(s => BenchResult.Load(s.DataPath)).ToList();
                var t = new DataTable();
                foreach (var c in new[] { "Area", "Metric", "A", "B", "Change", "Verdict" }) t.Columns.Add(c, typeof(string));
                foreach (var r in BenchCompare.Build(ra, rb)) t.Rows.Add(r.Area, r.Metric, r.Baseline, r.New, r.Change, r.Verdict);
                AddGrid("Benchmark summary", t);
                benchText = BenchCompare.ToText(ra, rb);
                return;
            }
            benchText = null;
            var pa = a.Select(s => Profile.Load(s.DataPath)).ToList();
            var pb = b.Select(s => Profile.Load(s.DataPath)).ToList();
            string mod = modPattern();
            double eps = 0.005;
            lastDiffs.Add(("Rebirth cost by entry point", "ms/s", SessionCompare.Diff(pa.Select(p => SessionCompare.RebirthCost(p, mod)).ToList(), pb.Select(p => SessionCompare.RebirthCost(p, mod)).ToList(), eps), false));
            if (pa.Concat(pb).Any(p => p.Allocs.Count > 0))
                lastDiffs.Add(("Garbage by Rebirth code", "KB/s", SessionCompare.Diff(pa.Select(p => SessionCompare.Allocations(p, mod)).ToList(), pb.Select(p => SessionCompare.Allocations(p, mod)).ToList(), 0.5), false));
            lastDiffs.Add(("Methods: inclusive CPU share", "% of samples", SessionCompare.Diff(pa.Select(SessionCompare.MethodInclusive).ToList(), pb.Select(SessionCompare.MethodInclusive).ToList(), 0.01), true));
            lastDiffs.Add(("Methods: self CPU share", "% of samples", SessionCompare.Diff(pa.Select(SessionCompare.MethodSelf).ToList(), pb.Select(SessionCompare.MethodSelf).ToList(), 0.01), true));
            ShowDiffs();
        }
        catch (Exception ex) { header.Text = lastHeader + "\nCould not compare: " + ex.Message; }
    }

    string benchText;

    void ShowDiffs()
    {
        var selected = results.SelectedIndex;
        results.TabPages.Clear();
        foreach (var d in lastDiffs)
        {
            var t = SessionCompare.ToTable(d.title.Contains("Methods") ? "Method" : "Item", d.unit, d.rows, d.modFilter ? filter.Text.Trim() : "");
            AddGrid(d.title, t, colorStatus: true);
        }
        if (selected >= 0 && selected < results.TabPages.Count) results.SelectedIndex = selected;
    }

    void AddGrid(string title, DataTable t, bool colorStatus = false)
    {
        var g = new DataGridView
        {
            ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        g.DataError += (_, e) => e.ThrowException = false;
        Theme.StyleGrid(g);
        g.DataSource = t;
        if (colorStatus && g.Columns.Contains("Status"))
            foreach (DataGridViewRow r in g.Rows)
            {
                string s = r.Cells["Status"].Value as string ?? "";
                r.Cells["Status"].Style.ForeColor = s switch { "lower" => Theme.Good, "removed" => Theme.Good, "higher" => Theme.Bad, "new" => Theme.Bad, _ => Theme.Muted };
            }
        var page = new PagePanel(title);
        page.Controls.Add(Ui.WithSlimScroll(g));
        results.TabPages.Add(page);
        Theme.Style(page);
    }

    void Export()
    {
        if (lastDiffs.Count == 0 && benchText == null) { header.Text = "Run a comparison first."; return; }
        using var dlg = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = "comparison.txt", InitialDirectory = SessionStore.Dir };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var sb = new StringBuilder();
        sb.AppendLine(lastHeader).AppendLine();
        if (benchText != null) sb.Append(benchText);
        foreach (var d in lastDiffs) sb.Append(SessionCompare.ToText(d.title, d.unit, d.rows, d.modFilter ? filter.Text.Trim() : "", 80));
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        header.Text = lastHeader + "\nExported to " + dlg.FileName;
    }
}
