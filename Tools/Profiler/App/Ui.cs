using System.Data;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RebirthProfiler;

/// <summary>
/// Look and feel. Dark by default (near-black neutrals, soft borders, muted secondary text, one warm orange accent),
/// with a light variant. Everything custom-drawn reads its colours from here, so switching theme repaints the whole app.
/// </summary>
static class Theme
{
    public static bool Dark { get; private set; } = true;
    public static event Action Changed;

    public static Color Bg, Surface, Surface2, Border, Text, Muted, Accent, AccentHover, AccentPressed, OnAccent, Good, Bad, Warn, SelectionBg;
    public static Font Body, Small, SmallBold, Metric, Title, Brand;

    static string SettingsPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "ui-settings.json"));

    static Theme()
    {
        try
        {
            if (File.Exists(SettingsPath))
                using (var d = JsonDocument.Parse(File.ReadAllText(SettingsPath)))
                    if (d.RootElement.TryGetProperty("dark", out var v)) Dark = v.GetBoolean();
        }
        catch { }
        Palette();
        MakeFonts();
    }

    static void Palette()
    {
        if (Dark)
        {
            Bg = C(0x17, 0x17, 0x16); Surface = C(0x20, 0x1F, 0x1E); Surface2 = C(0x2A, 0x29, 0x27); Border = C(0x3A, 0x39, 0x36);
            Text = C(0xEC, 0xEB, 0xE4); Muted = C(0x8F, 0x8D, 0x86);
            Accent = C(0xB8, 0xA0, 0xF4); AccentHover = C(0xC9, 0xB6, 0xF8); AccentPressed = C(0xA4, 0x88, 0xEE); OnAccent = C(0x1B, 0x14, 0x33);
            Good = C(0x5F, 0xC0, 0x74); Bad = C(0xEF, 0x62, 0x5C); Warn = C(0xE6, 0xB4, 0x5E); SelectionBg = C(0x2E, 0x2A, 0x3F);
        }
        else
        {
            Bg = C(0xF5, 0xF4, 0xEE); Surface = C(0xFF, 0xFF, 0xFF); Surface2 = C(0xEE, 0xEC, 0xE4); Border = C(0xE0, 0xDD, 0xD0);
            Text = C(0x1F, 0x1E, 0x1D); Muted = C(0x77, 0x75, 0x6D);
            Accent = C(0x7B, 0x5C, 0xD6); AccentHover = C(0x8F, 0x72, 0xE6); AccentPressed = C(0x68, 0x48, 0xBF); OnAccent = C(0xFF, 0xFF, 0xFF);
            Good = C(0x2E, 0x7D, 0x4F); Bad = C(0xC0, 0x39, 0x2B); Warn = C(0xB7, 0x79, 0x1F); SelectionBg = C(0xE9, 0xE3, 0xF8);
        }
    }

    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    static void MakeFonts()
    {
        string family = FontFamily.Families.Any(f => f.Name == "Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
        Body = new Font(family, 9.5f); Small = new Font(family, 8.5f); SmallBold = new Font(family, 8.5f, FontStyle.Bold);
        Metric = new Font(family, 15f, FontStyle.Bold); Title = new Font(family, 11.5f, FontStyle.Bold); Brand = new Font(family, 13f, FontStyle.Bold);
    }

    public static void SetDark(bool dark)
    {
        Dark = dark;
        Palette();
        try { File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { dark })); } catch { }
        Changed?.Invoke();
    }

    public static float Scale(Control c) => c.DeviceDpi / 96f;

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Max(1f, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Color Mix(Color a, Color b, float t) =>
        Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    // ---- native helpers: dark title bar and dark scroll bars ---------------------------------------------------------
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idlist);

    public static void ApplyWindowChrome(Form f)
    {
        if (!f.IsHandleCreated) return;
        int on = Dark ? 1 : 0;
        DwmSetWindowAttribute(f.Handle, 20, ref on, sizeof(int));
        DwmSetWindowAttribute(f.Handle, 19, ref on, sizeof(int));
    }

    public static void ApplyScrollTheme(Control c)
    {
        if (!c.IsHandleCreated) return;
        SetWindowTheme(c.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null);
    }

    /// <summary>Restyles standard controls under <paramref name="root"/>. Custom controls repaint themselves from the palette.</summary>
    public static void Style(Control root)
    {
        switch (root)
        {
            case Form: root.BackColor = Bg; root.ForeColor = Text; root.Font = Body; break;
            case Card: break;
            case DataGridView g: StyleGrid(g); return;
            case Label l:
                l.ForeColor = (l.Tag as string) switch { "muted" => Muted, "warn" => Warn, "bad" => Bad, "good" => Good, "accent" => Accent, _ => Text };
                if (l.Tag as string == "small") l.ForeColor = Muted;
                l.BackColor = Color.Transparent;
                break;
            case TextBox t: t.BackColor = Bg; t.ForeColor = Text; break;
            case NumericUpDown n: n.BackColor = Bg; n.ForeColor = Text; n.BorderStyle = BorderStyle.FixedSingle; break;
            case ComboBox cb: cb.FlatStyle = FlatStyle.Flat; cb.BackColor = Bg; cb.ForeColor = Text; break;
            case SplitContainer s: s.BackColor = s.Panel1.BackColor = s.Panel2.BackColor = ParentSurface(s); break;
            case PagePanel tp: tp.BackColor = ParentSurface(tp); tp.ForeColor = Text; break;
            case Panel or FlowLayoutPanel or TableLayoutPanel:
                if (root is not FlatTabs) root.BackColor = root.Tag as string == "transparent" ? ParentSurface(root) : ParentSurface(root);
                root.ForeColor = Text;
                break;
        }
        foreach (Control c in root.Controls) Style(c);
    }

    static Color ParentSurface(Control c)
    {
        for (var p = c.Parent; p != null; p = p.Parent)
            if (p is Card) return Surface;
        return Bg;
    }

    public static void StyleGrid(DataGridView g)
    {
        g.EnableHeadersVisualStyles = false;
        g.BackgroundColor = Surface;
        g.GridColor = Border;
        g.BorderStyle = BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.RowHeadersVisible = false;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = (int)(34 * Scale(g));
        g.RowTemplate.Height = (int)(30 * Scale(g));
        g.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;   // wrapped text + auto-sized columns/rows never finished laying out
        var head = g.ColumnHeadersDefaultCellStyle;
        head.BackColor = Surface; head.ForeColor = Muted; head.Font = SmallBold; head.SelectionBackColor = Surface; head.SelectionForeColor = Muted;
        head.Padding = new Padding(8, 0, 8, 0); head.Alignment = DataGridViewContentAlignment.MiddleLeft;
        var cell = g.DefaultCellStyle;
        cell.BackColor = Surface; cell.ForeColor = Text; cell.Font = Body; cell.SelectionBackColor = SelectionBg; cell.SelectionForeColor = Text;
        cell.Padding = new Padding(8, 0, 8, 0);
        g.AlternatingRowsDefaultCellStyle.BackColor = Surface;
        g.ForeColor = Text;
        ApplyScrollTheme(g);
        if (g.Tag as string != "styled")
        {
            g.Tag = "styled";
            g.HandleCreated += (_, _) => ApplyScrollTheme(g);
            g.DataBindingComplete += (_, _) =>
            {
                foreach (DataGridViewColumn col in g.Columns)
                {
                    bool num = col.ValueType == typeof(double) || col.ValueType == typeof(int) || col.ValueType == typeof(long) || col.ValueType == typeof(float);
                    col.DefaultCellStyle.Alignment = num ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft;
                    col.HeaderCell.Style.Alignment = num ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft;
                    col.SortMode = DataGridViewColumnSortMode.Automatic;
                }
            };
        }
    }
}

// ---- controls --------------------------------------------------------------------------------------------------------

/// <summary>A rounded panel with a soft border: the basic surface everything sits on.</summary>
class Card : Panel
{
    public float Radius = 12;
    public Card()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Padding = new Padding(12);
        Theme.Changed += Invalidate;
    }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= Invalidate; base.Dispose(d); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.RoundRect(r, Radius * Theme.Scale(this));
        using var fill = new SolidBrush(Theme.Surface);
        using var pen = new Pen(Theme.Border);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
    }
}

sealed class AppButton : Control
{
    public enum K { Primary, Secondary, Ghost }
    public K Kind { get; set; } = K.Secondary;
    bool hover, down;

    public AppButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        TabStop = true; Cursor = Cursors.Hand; Font = Theme.Body; Margin = new Padding(0, 0, 8, 0);
        Theme.Changed += Invalidate;
    }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= Invalidate; base.Dispose(d); }
    public override Size GetPreferredSize(Size proposed)
    {
        var s = TextRenderer.MeasureText(Text, Font);
        float k = Theme.Scale(this);
        return new Size(s.Width + (int)(30 * k), (int)(34 * k));
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (AutoSize) { Size = GetPreferredSize(Size.Empty); } Invalidate(); }
    protected override bool IsInputKey(Keys k) => k == Keys.Enter || k == Keys.Space || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode is Keys.Enter or Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill, fore, border = Color.Transparent;
        if (!Enabled) { fill = Kind == K.Ghost ? Color.Transparent : Theme.Surface2; fore = Theme.Muted; if (Kind == K.Primary) fill = Theme.Mix(Theme.Surface2, Theme.Accent, 0.25f); }
        else switch (Kind)
        {
            case K.Primary: fill = down ? Theme.AccentPressed : hover ? Theme.AccentHover : Theme.Accent; fore = Theme.OnAccent; break;
            case K.Secondary: fill = down ? Theme.Border : hover ? Theme.Mix(Theme.Surface2, Theme.Text, 0.06f) : Theme.Surface2; fore = Theme.Text; border = Theme.Border; break;
            default: fill = down ? Theme.Border : hover ? Theme.Surface2 : Color.Transparent; fore = hover ? Theme.Text : Theme.Muted; break;
        }
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.RoundRect(r, 9 * Theme.Scale(this));
        if (fill.A > 0) { using var b = new SolidBrush(fill); g.FillPath(b, path); }
        if (border.A > 0) { using var p = new Pen(border); g.DrawPath(p, path); }
        if (Focused && ShowFocusCues) { using var p = new Pen(Theme.Mix(Theme.Accent, Theme.Bg, 0.3f)) { DashStyle = DashStyle.Dot }; g.DrawPath(p, path); }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

/// <summary>An on/off switch with a label; same API as a CheckBox for the parts the app uses.</summary>
sealed class Toggle : Control
{
    bool on;
    public bool Checked { get => on; set { if (on == value) return; on = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler CheckedChanged;

    public Toggle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        TabStop = true; Cursor = Cursors.Hand; Font = Theme.Body; Margin = new Padding(0, 0, 18, 0);
        Theme.Changed += Invalidate;
    }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= Invalidate; base.Dispose(d); }
    public override Size GetPreferredSize(Size proposed)
    {
        float k = Theme.Scale(this);
        return new Size((int)(44 * k) + TextRenderer.MeasureText(Text, Font).Width + (int)(4 * k), (int)(28 * k));
    }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (AutoSize) Size = GetPreferredSize(Size.Empty); Invalidate(); }
    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
    protected override bool IsInputKey(Keys k) => k == Keys.Space || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; } base.OnKeyDown(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = Theme.Scale(this), tw = 34 * k, th = 18 * k, y = (Height - th) / 2f;
        using (var path = Theme.RoundRect(new RectangleF(1, y, tw, th), th / 2))
        {
            using var b = new SolidBrush(on ? Theme.Accent : Theme.Border);
            g.FillPath(b, path);
        }
        float knob = th - 4 * k, kx = on ? 1 + tw - knob - 2 * k : 1 + 2 * k;
        using (var b = new SolidBrush(on ? Theme.OnAccent : Theme.Muted)) g.FillEllipse(b, kx, y + 2 * k, knob, knob);
        if (Focused && ShowFocusCues) { using var p = new Pen(Theme.Accent) { DashStyle = DashStyle.Dot }; g.DrawRectangle(p, 0, 0, Width - 1, Height - 1); }
        var textRect = new Rectangle((int)(tw + 10 * k), 0, Width - (int)(tw + 10 * k), Height);
        TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? Theme.Text : Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

/// <summary>A text input with a rounded, recessed field look. Text and TextChanged behave like a normal text box.</summary>
sealed class RoundedTextBox : Control
{
    readonly TextBox tb = new() { BorderStyle = BorderStyle.None };
    bool focused;
    public string PlaceholderText { get => tb.PlaceholderText; set => tb.PlaceholderText = value; }
    public override string Text { get => tb.Text; set => tb.Text = value; }
    public TextBox Inner => tb;

    public RoundedTextBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.ContainerControl, true);
        Height = 34; Font = Theme.Body; Margin = new Padding(0, 0, 8, 0);
        tb.Font = Theme.Body;
        tb.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);
        tb.GotFocus += (_, _) => { focused = true; Invalidate(); };
        tb.LostFocus += (_, _) => { focused = false; Invalidate(); };
        Controls.Add(tb);
        Theme.Changed += Recolor;
        Recolor();
    }
    void Recolor() { tb.BackColor = Theme.Bg; tb.ForeColor = Theme.Text; Invalidate(); }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= Recolor; base.Dispose(d); }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        float k = Theme.Scale(this);
        int pad = (int)(11 * k);
        tb.Left = pad; tb.Width = Math.Max(10, Width - pad * 2);
        if (tb.Multiline) { tb.Top = pad; tb.Height = Math.Max(10, Height - pad * 2); }
        else tb.Top = Math.Max(0, (Height - tb.Height) / 2);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 9 * Theme.Scale(this));
        using var fill = new SolidBrush(Theme.Bg);
        using var pen = new Pen(focused ? Theme.Accent : Theme.Border);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
    }
    protected override void OnGotFocus(EventArgs e) { tb.Focus(); base.OnGotFocus(e); }
}

/// <summary>Tab strip drawn as plain text with an accent underline; hosts ordinary <see cref="PagePanel"/> objects.</summary>
sealed class FlatTabs : Control
{
    public sealed class PageList
    {
        readonly FlatTabs owner;
        internal PageList(FlatTabs o) { owner = o; }
        public int Count => owner.pages.Count;
        public PagePanel this[int i] => owner.pages[i];
        public void Add(PagePanel p) { owner.pages.Add(p); p.Dock = DockStyle.None; p.SetBounds(0, 0, Math.Max(owner.host.Width, 800), Math.Max(owner.host.Height, 500)); p.Visible = false; owner.host.Controls.Add(p); owner.Select(owner.selected < 0 ? 0 : owner.selected, true); }
        public void AddRange(PagePanel[] ps) { foreach (var p in ps) Add(p); }
        public void Clear() { foreach (var p in owner.pages) { owner.host.Controls.Remove(p); p.Dispose(); } owner.pages.Clear(); owner.selected = -1; owner.Invalidate(); }
    }

    readonly List<PagePanel> pages = new();
    readonly Panel host = new();   // positioned in OnLayout, below the header
    int selected = -1, hoverIndex = -1;
    readonly List<Rectangle> rects = new();
    public PageList TabPages { get; }
    public event EventHandler SelectedIndexChanged;
    int HeaderHeight => (int)(44 * Theme.Scale(this));

    public FlatTabs()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabPages = new PageList(this);
        Controls.Add(host);
        Theme.Changed += OnTheme;
    }
    void OnTheme() { host.BackColor = Theme.Bg; Invalidate(); }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= OnTheme; base.Dispose(d); }

    public int SelectedIndex { get => selected; set => Select(value, false); }
    public PagePanel SelectedTab { get => selected >= 0 && selected < pages.Count ? pages[selected] : null; set { int i = pages.IndexOf(value); if (i >= 0) Select(i, false); } }

    void Select(int i, bool quiet)
    {
        if (pages.Count == 0) { selected = -1; return; }
        i = Math.Clamp(i, 0, pages.Count - 1);
        for (int p = 0; p < pages.Count; p++) pages[p].Visible = p == i;
        bool changed = selected != i;
        selected = i;
        pages[i].BringToFront();
        Invalidate();
        if (changed && !quiet) SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        host.SetBounds(0, HeaderHeight, Width, Math.Max(0, Height - HeaderHeight));
        // every page keeps the full size even while hidden: tables lay out their rows against their real width
        foreach (var pg in pages) pg.SetBounds(0, 0, host.Width, host.Height);
        base.OnLayout(e);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = rects.FindIndex(r => r.Contains(e.Location));
        if (h != hoverIndex) { hoverIndex = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        int i = rects.FindIndex(r => r.Contains(e.Location));
        if (i >= 0) Select(i, false);
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Bg);
        float k = Theme.Scale(this);
        int h = HeaderHeight, x = 0;
        rects.Clear();
        using var line = new Pen(Theme.Border);
        g.DrawLine(line, 0, h - 1, Width, h - 1);
        var boldFont = new Font(Theme.Body, FontStyle.Bold);
        for (int i = 0; i < pages.Count; i++)
        {
            string text = pages[i].Text;
            var size = TextRenderer.MeasureText(text, boldFont);
            var rect = new Rectangle(x, 0, size.Width + (int)(28 * k), h);
            rects.Add(rect);
            bool sel = i == selected, hov = i == hoverIndex;
            TextRenderer.DrawText(g, text, sel ? boldFont : Theme.Body, rect, sel ? Theme.Text : hov ? Theme.Text : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (sel)
            {
                using var b = new SolidBrush(Theme.Accent);
                g.FillRectangle(b, rect.X + (int)(14 * k), h - (int)(3 * k), rect.Width - (int)(28 * k), (int)(3 * k));
            }
            x += rect.Width;
        }
        boldFont.Dispose();
    }
}

/// <summary>A small caption above a big number, on its own rounded tile.</summary>
sealed class MetricTile : Control
{
    string caption = "", value = "–";
    Color? valueColor;
    public string Caption { get => caption; set { caption = value; Invalidate(); } }

    public MetricTile()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Margin = new Padding(0, 0, 8, 0);
        Theme.Changed += Invalidate;
    }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= Invalidate; base.Dispose(d); }
    public void Set(string v, Color? color = null) { if (value == v && valueColor == color) return; value = v; valueColor = color; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = Theme.Scale(this);
        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 11 * k);
        using (var fill = new SolidBrush(Theme.Surface)) g.FillPath(fill, path);
        using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
        int pad = (int)(12 * k);
        TextRenderer.DrawText(g, caption, Theme.Small, new Rectangle(pad, (int)(8 * k), Width - pad, (int)(18 * k)), Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, value, Theme.Metric, new Rectangle(pad, (int)(26 * k), Width - pad, Height - (int)(28 * k)), valueColor ?? Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

/// <summary>A small pill with a coloured dot: the game / capture state.</summary>
sealed class Chip : Control
{
    public enum S { Idle, Running, Busy, Error }
    string text = ""; S state = S.Idle;

    public Chip()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Font = Theme.Small;
        Theme.Changed += Invalidate;
    }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= Invalidate; base.Dispose(d); }
    public void Set(string t, S s)
    {
        if (text == t && state == s) return;
        text = t; state = s;
        Size = GetPreferredSize(Size.Empty);
        Parent?.PerformLayout();
        Invalidate();
    }
    public override Size GetPreferredSize(Size proposed)
    {
        float k = Theme.Scale(this);
        return new Size(TextRenderer.MeasureText(text, Theme.Small).Width + (int)(38 * k), (int)(28 * k));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = Theme.Scale(this);
        Color dot = state switch { S.Running => Theme.Good, S.Busy => Theme.Accent, S.Error => Theme.Bad, _ => Theme.Muted };
        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), (Height - 1) / 2f);
        using (var fill = new SolidBrush(Theme.Mix(Theme.Bg, dot, 0.12f))) g.FillPath(fill, path);
        using (var pen = new Pen(Theme.Mix(Theme.Border, dot, 0.35f))) g.DrawPath(pen, path);
        using (var b = new SolidBrush(dot)) g.FillEllipse(b, 12 * k, Height / 2f - 4 * k, 8 * k, 8 * k);
        TextRenderer.DrawText(g, text, Theme.Small, new Rectangle((int)(26 * k), 0, Width - (int)(28 * k), Height), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

/// <summary>A soft, tinted notice. Setting Text to empty hides it.</summary>
sealed class Banner : Control
{
    public Banner()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Visible = false;
        Theme.Changed += Invalidate;
    }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= Invalidate; base.Dispose(d); }
    public override string Text
    {
        get => base.Text;
        set { base.Text = value ?? ""; Visible = base.Text.Length > 0; Invalidate(); }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = Theme.Scale(this);
        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 10 * k);
        using (var fill = new SolidBrush(Theme.Mix(Theme.Bg, Theme.Warn, 0.10f))) g.FillPath(fill, path);
        using (var pen = new Pen(Theme.Mix(Theme.Border, Theme.Warn, 0.45f))) g.DrawPath(pen, path);
        using (var b = new SolidBrush(Theme.Warn)) g.FillEllipse(b, 14 * k, Height / 2f - 4 * k, 8 * k, 8 * k);
        TextRenderer.DrawText(g, Text, Theme.Body, new Rectangle((int)(32 * k), 0, Width - (int)(40 * k), Height), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

/// <summary>A page hosted by <see cref="FlatTabs"/>; its Text is the tab title.</summary>
class PagePanel : Panel
{
    public PagePanel() { }
    public PagePanel(string title) { Text = title; }
}

/// <summary>
/// A narrow, rounded scrollbar whose thumb is the accent colour. It replaces the native vertical scrollbar of a
/// <see cref="DataGridView"/> (rows are scrolled through FirstDisplayedScrollingRowIndex).
/// </summary>
sealed class SlimScrollBar : Control
{
    DataGridView grid;
    RectangleF thumb;
    bool hover, dragging;
    float dragOffset;

    public SlimScrollBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = false;
        Width = (int)(12 * Theme.Scale(this));
        Theme.Changed += Invalidate;
    }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= Invalidate; base.Dispose(d); }

    public void Attach(DataGridView g)
    {
        grid = g;
        g.ScrollBars = ScrollBars.Horizontal;             // the native vertical bar is replaced by this one
        void Refresh(object s, EventArgs e) => Recalc();
        g.Scroll += (_, _) => Recalc();
        g.RowsAdded += (_, _) => Recalc();
        g.RowsRemoved += (_, _) => Recalc();
        g.SizeChanged += Refresh;
        g.DataBindingComplete += (_, _) => Recalc();
        g.DataSourceChanged += Refresh;
        g.MouseWheel += (_, _) => Recalc();
        g.VisibleChanged += Refresh;
        Recalc();
    }

    float Pad => 4 * Theme.Scale(this);
    int Total => grid?.RowCount ?? 0;
    int Visible => Math.Max(1, grid?.DisplayedRowCount(false) ?? 1);
    int First => Math.Max(0, grid?.FirstDisplayedScrollingRowIndex ?? 0);
    bool Needed => grid != null && Total > Visible;

    void Recalc()
    {
        if (grid == null) return;
        float track = Height - 2 * Pad, k = Theme.Scale(this);
        if (!Needed || track <= 0) { thumb = RectangleF.Empty; Invalidate(); return; }
        float len = Math.Max(30 * k, track * Visible / Total);
        int maxFirst = Math.Max(1, Total - Visible);
        float y = Pad + (track - len) * Math.Min(First, maxFirst) / maxFirst;
        float w = (hover || dragging ? 8 : 6) * k;
        thumb = new RectangleF((Width - w) / 2f, y, w, len);
        Invalidate();
    }

    void ScrollToRow(int row)
    {
        if (grid == null || Total == 0) return;
        row = Math.Clamp(row, 0, Math.Max(0, Total - Visible));
        try { grid.FirstDisplayedScrollingRowIndex = row; } catch (InvalidOperationException) { }
        Recalc();
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Recalc(); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Recalc(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Recalc(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!Needed || e.Button != MouseButtons.Left) return;
        if (thumb.Contains(e.Location)) { dragging = true; dragOffset = e.Y - thumb.Y; Capture = true; Recalc(); }
        else ScrollToRow(First + (e.Y < thumb.Y ? -Visible : Visible));
        base.OnMouseDown(e);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging && Needed)
        {
            float track = Height - 2 * Pad - thumb.Height;
            float ratio = track <= 0 ? 0 : Math.Clamp((e.Y - dragOffset - Pad) / track, 0f, 1f);
            ScrollToRow((int)Math.Round(ratio * Math.Max(1, Total - Visible)));
        }
        base.OnMouseMove(e);
    }
    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Capture = false; Recalc(); base.OnMouseUp(e); }
    protected override void OnMouseWheel(MouseEventArgs e) { ScrollToRow(First - Math.Sign(e.Delta) * 3); base.OnMouseWheel(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Surface);
        if (!Needed) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = Theme.Scale(this);
        float tw = 2 * k;
        using (var trackPath = Theme.RoundRect(new RectangleF((Width - tw) / 2f, Pad, tw, Height - 2 * Pad), tw / 2))
        using (var trackBrush = new SolidBrush(Color.FromArgb(120, Theme.Border))) g.FillPath(trackBrush, trackPath);
        if (thumb.IsEmpty) return;
        using var path = Theme.RoundRect(thumb, thumb.Width / 2);
        Color top = hover || dragging ? Theme.AccentHover : Theme.Accent, bottom = hover || dragging ? Theme.Accent : Theme.AccentPressed;
        using var brush = new LinearGradientBrush(thumb, top, bottom, LinearGradientMode.Vertical);
        g.FillPath(brush, path);
    }
}

/// <summary>A divider line and three text areas: what is selected in the table above, in full.</summary>
sealed class DetailPanel : Panel
{
    readonly Label title = new() { AutoSize = false, Dock = DockStyle.Top, Font = Theme.Title, AutoEllipsis = true };
    readonly Label meta = new() { AutoSize = false, Dock = DockStyle.Top, Tag = "muted", AutoEllipsis = true };
    readonly Label body = new() { AutoSize = false, Dock = DockStyle.Fill };

    public DetailPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        float k = Theme.Scale(this);
        Padding = new Padding((int)(14 * k), (int)(12 * k), (int)(14 * k), (int)(8 * k));
        title.Height = (int)(26 * k); meta.Height = (int)(22 * k);
        Controls.Add(body); Controls.Add(meta); Controls.Add(title);
        Theme.Changed += Invalidate;
        ShowDetail("Select a row", "", "");
    }
    protected override void Dispose(bool d) { if (d) Theme.Changed -= Invalidate; base.Dispose(d); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Surface);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawLine(pen, 0, 0, Width, 0);
    }
    public void ShowDetail(string heading, string subtitle, string text)
    {
        title.Text = heading; meta.Text = subtitle; body.Text = text;
    }
}

static class Ui
{
    /// <summary>Puts a table on a panel together with the slim accent scrollbar.</summary>
    public static Control WithSlimScroll(DataGridView g)
    {
        var host = new Panel { Dock = DockStyle.Fill };
        var bar = new SlimScrollBar { Dock = DockStyle.Right };
        g.Dock = DockStyle.Fill;
        host.Controls.Add(g);
        host.Controls.Add(bar);
        bar.Attach(g);
        return host;
    }
}
