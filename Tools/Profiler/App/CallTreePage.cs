namespace RebirthProfiler;

/// <summary>
/// Top-down call tree built from the sampled stacks: the outermost frames at the top, each level showing how much main-thread time
/// (milliseconds per second) passes through that call. Children load when a node is opened, so a big profile stays quick.
/// Mod code is tinted with the accent colour.
/// </summary>
sealed class CallTreePage : PagePanel
{
    sealed class Node
    {
        public int Method = -1;
        public double Ms;                 // thread time per second passing through this call (main thread and workers weighted on their own scales)
        public Dictionary<int, Node> Kids = new();
    }

    readonly TreeView tree = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, ShowLines = false, HideSelection = false, FullRowSelect = true, ItemHeight = 24, ShowPlusMinus = true };
    readonly Toggle mainOnly = new() { Text = "Main thread only", AutoSize = true, Checked = true };
    readonly RoundedTextBox search = new() { Width = 260, PlaceholderText = "Find a method (press Enter)" };
    readonly AppButton hot = new() { Text = "Follow the hot path", AutoSize = true };
    readonly AppButton collapse = new() { Text = "Collapse all", Kind = AppButton.K.Ghost, AutoSize = true };
    readonly Label info = new() { Dock = DockStyle.Fill, Tag = "muted", Text = "Open or capture a profile to see where the time goes, call by call." };
    Profile p;
    bool[] mask;
    Node root = new();
    Color modColor = Color.Empty;
    string lastSearch = "";
    int searchSkip;

    public CallTreePage() : base("Call tree")
    {
        float k = Theme.Scale(this);
        int S(int px) => (int)(px * k);
        Padding = new Padding(0, S(8), 0, 0);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = S(42), WrapContents = false };
        bar.Controls.AddRange(new Control[] { mainOnly, search, hot, collapse });
        var infoHost = new Panel { Dock = DockStyle.Top, Height = S(26) };
        infoHost.Controls.Add(info);
        var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(8)) };
        card.Controls.Add(tree);
        Controls.Add(card);
        Controls.Add(infoHost);
        Controls.Add(bar);
        mainOnly.CheckedChanged += (_, _) => Rebuild();
        hot.Click += (_, _) => FollowHotPath();
        collapse.Click += (_, _) => { tree.CollapseAll(); };
        search.Inner.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Find(search.Text.Trim()); } };
        tree.BeforeExpand += (_, e) => Populate(e.Node);
        tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
        tree.DrawNode += DrawNode;
        Theme.Changed += Restyle;
        Restyle();
    }

    void Restyle()
    {
        tree.BackColor = Theme.Surface;
        tree.ForeColor = Theme.Text;
        tree.Font = Theme.Body;
        modColor = Theme.Accent;
        tree.Invalidate();
    }

    DateTime lastBuild = DateTime.MinValue;
    string lastPath;

    public void SetProfile(Profile profile, bool[] modMask)
    {
        // A live profile is reloaded every few seconds; do not collapse what the user has opened while browsing.
        bool samePath = profile != null && profile.Path == lastPath;
        double age = (DateTime.Now - lastBuild).TotalSeconds;
        if (samePath && p != null && ((tree.SelectedNode != null && age < 180) || age < 15)) return;
        p = profile; mask = modMask; lastPath = profile?.Path; lastBuild = DateTime.Now;
        Rebuild();
    }

    void Rebuild()
    {
        tree.BeginUpdate();
        tree.Nodes.Clear();
        root = new Node();
        if (p == null) { tree.EndUpdate(); return; }
        double total = 0;
        foreach (var s in p.Stacks)
        {
            if (mainOnly.Checked && !s.Main) continue;
            double w = s.Main ? p.MainMsPerSec(s.Count) : p.OtherMsPerSec(s.Count);
            total += w;
            var node = root;
            // frames are stored innermost first; the tree reads from the outermost call down
            for (int i = s.F.Length - 1; i >= 0; i--)
            {
                int m = s.F[i];
                if (p.Methods[m].Full.StartsWith("(wrapper")) continue;
                if (!node.Kids.TryGetValue(m, out var child)) node.Kids[m] = child = new Node { Method = m };
                child.Ms += w;
                node = child;
            }
        }
        root.Ms = total;
        info.Text = $"{total:F0} ms of thread time per second ({(mainOnly.Checked ? "main thread" : "main thread and worker threads; workers need the launch option Sample worker threads")}). Only the heaviest {p.Stacks.Count:N0} distinct stacks were recorded, so small branches are approximate.";
        foreach (var kid in TopKids(root, 200)) tree.Nodes.Add(MakeNode(kid, root.Ms));
        tree.EndUpdate();
        if (tree.Nodes.Count > 0) tree.Nodes[0].EnsureVisible();
    }

    static IEnumerable<Node> TopKids(Node n, int limit) => n.Kids.Values.OrderByDescending(x => x.Ms).Take(limit);

    TreeNode MakeNode(Node n, double total)
    {
        var m = p.Methods[n.Method];
        double ms = n.Ms;
        double pct = total <= 0 ? 0 : 100.0 * n.Ms / total;
        var tn = new TreeNode($"{ms,8:F1} ms/s  {pct,4:F0}%   {m.Short}") { Tag = n };
        if (n.Kids.Count > 0) tn.Nodes.Add(new TreeNode("…") { Tag = null });   // placeholder so the node can be opened
        return tn;
    }

    void Populate(TreeNode tn)
    {
        if (tn.Nodes.Count != 1 || tn.Nodes[0].Tag != null) return;
        var n = (Node)tn.Tag;
        tn.Nodes.Clear();
        double floor = n.Ms / 200;            // hide branches below half a percent of their parent
        foreach (var kid in TopKids(n, 60).Where(k => k.Ms >= floor)) tn.Nodes.Add(MakeNode(kid, root.Ms));
    }

    void DrawNode(object sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node == null || e.Bounds.IsEmpty) return;
        bool selected = (e.State & TreeNodeStates.Selected) != 0;
        using (var back = new SolidBrush(selected ? Theme.SelectionBg : Theme.Surface)) e.Graphics.FillRectangle(back, new Rectangle(e.Bounds.X - 2, e.Bounds.Y, tree.ClientSize.Width, e.Bounds.Height));
        Color c = Theme.Text;
        if (e.Node.Tag is Node n && mask != null && n.Method >= 0 && n.Method < mask.Length && mask[n.Method]) c = modColor;
        else if (e.Node.Tag == null) c = Theme.Muted;
        TextRenderer.DrawText(e.Graphics, e.Node.Text, tree.Font, new Point(e.Bounds.X, e.Bounds.Y + 3), c, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    void FollowHotPath()
    {
        var tn = tree.SelectedNode ?? (tree.Nodes.Count > 0 ? tree.Nodes[0] : null);
        while (tn != null)
        {
            tn.Expand();
            if (tn.Nodes.Count == 0) break;
            var next = tn.Nodes.Cast<TreeNode>().Where(x => x.Tag is Node).OrderByDescending(x => ((Node)x.Tag).Ms).FirstOrDefault();
            if (next == null || ((Node)next.Tag).Ms < ((Node)tn.Tag).Ms * 0.3) break;   // stop where the time spreads out
            tn = next;
        }
        if (tn != null) { tree.SelectedNode = tn; tn.EnsureVisible(); }
    }

    /// <summary>Finds the next node whose method name contains the text, opening the path to it.</summary>
    void Find(string text)
    {
        if (p == null || text.Length == 0) return;
        if (text != lastSearch) { lastSearch = text; searchSkip = 0; }
        var hits = new List<List<int>>();
        void Walk(Node n, List<int> path)
        {
            if (hits.Count > searchSkip + 1) return;
            foreach (var kid in n.Kids.Values.OrderByDescending(x => x.Ms))
            {
                path.Add(kid.Method);
                if (p.Methods[kid.Method].Short.Contains(text, StringComparison.OrdinalIgnoreCase)) hits.Add(new List<int>(path));
                if (path.Count < 40) Walk(kid, path);
                path.RemoveAt(path.Count - 1);
                if (hits.Count > searchSkip + 1) return;
            }
        }
        Walk(root, new List<int>());
        if (hits.Count == 0) { info.Text = $"No call path contains \"{text}\"."; return; }
        var chosen = hits[Math.Min(searchSkip, hits.Count - 1)];
        searchSkip = searchSkip + 1 >= hits.Count ? 0 : searchSkip + 1;
        TreeNodeCollection level = tree.Nodes; TreeNode found = null;
        foreach (int m in chosen)
        {
            found = level.Cast<TreeNode>().FirstOrDefault(x => x.Tag is Node n && n.Method == m);
            if (found == null) break;
            Populate(found);
            found.Expand();
            level = found.Nodes;
        }
        if (found != null) { tree.SelectedNode = found; found.EnsureVisible(); tree.Focus(); }
    }
}
