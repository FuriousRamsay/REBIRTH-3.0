using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable
public static class RebirthCookingHudService
{
    // Observational: progression and observer lifetime belong to the authority GameUpdate owner.
    public static string Snapshot(EntityPlayer player, RebirthWorldCharacterRecord record)
    {
        return RebirthCookingJobRuntime.GetSnapshot(player, record);
    }
}

[Preserve]
public sealed class XUiC_RebirthCookingHud : XUiController
{
    private sealed class Card
    {
        public XUiController Root;
        public XUiV_Label Name, Status, Time;
        public XUiV_Sprite Icon, Background, Line;
    }
    private sealed class Job
    {
        public string Name, Status, Time, Icon, Position;
        public Job(XElement value)
        {
            Name = (string)value.Attribute("name") ?? string.Empty;
            Status = (string)value.Attribute("status") ?? string.Empty;
            Time = (string)value.Attribute("time") ?? string.Empty;
            Icon = (string)value.Attribute("icon") ?? string.Empty;
            Position = (string)value.Attribute("position") ?? string.Empty;
        }
    }
    private readonly Card[] cards = new Card[64];
    private readonly List<Job> jobs = new List<Job>();
    private World world;
    private EntityPlayerLocal owner;
    private XUiView cachedRoot;
    private XUiController ammoHud, pickupHud;
    private bool active, inFlight, dirty = true;
    private string lastXml, pendingXml;
    private int generation, displayed, lastRows = -1, lastColumns = -1;
    private bool lastVisible;
    private float nextRefresh, nextDiscovery;

    public override void Init()
    {
        base.Init();
        active = true; cachedRoot = null; dirty = true;
        generation++; inFlight = false; nextRefresh = nextDiscovery = 0f;
    }
    public override void OnOpen()
    {
        base.OnOpen(); active = true; dirty = true; nextRefresh = 0f;
    }
    public override void OnClose()
    {
        active = false; ResetPresentation(); base.OnClose();
    }
    public override void Cleanup()
    {
        active = false; ResetPresentation(); world = null; owner = null;
        cachedRoot = null; Array.Clear(cards, 0, cards.Length); base.Cleanup();
    }
    private void ResetPresentation()
    {
        generation++; inFlight = false; pendingXml = lastXml = null;
        jobs.Clear(); HideCards(); dirty = true; lastVisible = false;
        nextRefresh = nextDiscovery = 0f; lastRows = lastColumns = -1;
    }
    private void HideCards()
    {
        var overflow = GetChildById("cookingHudOverflow")?.ViewComponent;
        if(overflow != null) overflow.IsVisible = false;
        for (int i = 0; i < displayed; i++)
            if (cards[i]?.Root?.ViewComponent != null && cards[i].Root.ViewComponent.IsVisible)
                cards[i].Root.ViewComponent.IsVisible = false;
        displayed = 0;
    }
    private void Discover(float now)
    {
        bool rebound = !ReferenceEquals(cachedRoot, ViewComponent);
        if (!rebound && now < nextDiscovery) return;
        nextDiscovery = now + 1f;
        if (rebound) { HideCards(); cachedRoot = ViewComponent; Array.Clear(cards, 0, cards.Length); dirty = true; }
        // Positive references are reused; only missing entries retry. A recreated root invalidates
        // the whole pool. This fixed-pool lookup is not performed in an ordinary frame.
        for (int i = 0; i < cards.Length; i++)
        {
            Card card = cards[i];
            if (card?.Root?.ViewComponent != null && card.Name != null && card.Status != null && card.Time != null && card.Icon != null) continue;
            XUiController root = GetChildById("cookingHud" + i);
            if (root == null) continue;
            cards[i] = new Card { Root = root, Name = root.GetChildById("name")?.ViewComponent as XUiV_Label,
                Status = root.GetChildById("status")?.ViewComponent as XUiV_Label, Time = root.GetChildById("time")?.ViewComponent as XUiV_Label,
                Icon = root.GetChildById("icon")?.ViewComponent as XUiV_Sprite, Background = root.GetChildById("background")?.ViewComponent as XUiV_Sprite, Line = root.GetChildById("line")?.ViewComponent as XUiV_Sprite };
            if (root.ViewComponent != null && i >= displayed) root.ViewComponent.IsVisible = false;
            dirty = true;
        }
    }
    public override void Update(float dt)
    {
        base.Update(dt); // Preserve native traversal even while our presentation is dormant.
        EntityPlayerLocal player = xui?.playerUI?.entityPlayer;
        if (!ReferenceEquals(world, player?.world) || !ReferenceEquals(owner, player))
        {
            ResetPresentation(); world = player?.world; owner = player; cachedRoot = null;
        }
        if (!active || player == null) return;
        bool visible = RebirthSurvivorMode.IsEnabledForCurrentWorld() && !player.IsDead()
            && xui.playerUI.windowManager != null && !xui.playerUI.windowManager.IsModalWindowOpen();
        if (!visible)
        {
            if (lastVisible || displayed != 0) { HideCards(); dirty = true; }
            lastVisible = false; return; // No jobs requests or XML projection while dead/modal/off.
        }
        float now = UnityEngine.Time.realtimeSinceStartup;
        Discover(now);
        if (!lastVisible) { dirty = true; nextRefresh = 0f; }
        lastVisible = true;
        if (!inFlight && now >= nextRefresh)
        {
            int requestedGeneration = generation;
            World requestedWorld = world;
            EntityPlayerLocal requestedOwner = owner;
            inFlight = true; nextRefresh = now + 2f;
            RebirthCookingSessionService.Request(player, "jobs", reply: response =>
            {
                if (generation != requestedGeneration || !active || !ReferenceEquals(world, requestedWorld) || !ReferenceEquals(owner, requestedOwner)) return;
                inFlight = false;
                if (!string.IsNullOrEmpty(response) && response.StartsWith("<jobs", StringComparison.Ordinal)) pendingXml = response;
            });
        }
        if (pendingXml != null)
        {
            string value = pendingXml; pendingXml = null;
            if (!string.Equals(lastXml, value, StringComparison.Ordinal))
            {
                try
                {
                    XElement root = XElement.Parse(value);
                    var next = new List<Job>();
                    foreach (XElement item in root.Elements("job")) { if (next.Count == cards.Length) break; next.Add(new Job(item)); }
                    jobs.Clear(); jobs.AddRange(next); lastXml = value; dirty = true;
                }
                catch (System.Xml.XmlException) { /* Preserve the last valid presentation. */ }
            }
        }
        // Count-based, row-major cards immediately above the minimap.
        Vector2i screen = xui.GetXUiScreenSize();
        int columns = jobs.Count <= 1 ? 1 : jobs.Count <= 4 ? 2 : jobs.Count <= 6 ? 3 : 4;
        int rows = Math.Max(1, (screen.y - 70 - 247) / 64 + 1);
        if (lastRows != rows || lastColumns != columns) { lastRows = rows; lastColumns = columns; dirty = true; }
        PositionHud(columns);
        if (!dirty) return;
        dirty = false;
        int count = Math.Min(Math.Min(cards.Length, rows * columns), jobs.Count), end = Math.Max(displayed, count);
        for (int i = 0; i < end; i++)
        {
            Card card = cards[i];
            if (card?.Root?.ViewComponent == null) continue;
            bool show = i < count;
            if (card.Root.ViewComponent.IsVisible != show) card.Root.ViewComponent.IsVisible = show;
            if (!show) continue;
            int left = (i % columns) * 245 / columns, right = ((i % columns) + 1) * 245 / columns;
            int width = right - left - (i % columns == columns - 1 ? 0 : 4);
            SetPosition(card.Root.ViewComponent, new Vector2i(-255 + left, 247 + (i / columns) * 64));
            Layout(card, width, columns > 1);
            Job job = jobs[i];
            string status = job.Status.StartsWith("OVERCOOKING", StringComparison.Ordinal) ? "Burning" : job.Status.StartsWith("READY", StringComparison.Ordinal) ? "Ready" : job.Status == "BURNT" ? "Burnt" : job.Status.StartsWith("PAUSED", StringComparison.Ordinal) ? "Paused" : "Cooking";
            string timer = job.Time; int suffix = timer.IndexOf(" to ", StringComparison.Ordinal); if (suffix >= 0) timer = timer.Substring(0, suffix);
            SetText(card.Name, job.Name); SetText(card.Status, status.ToUpperInvariant()); SetText(card.Time, timer.TrimStart('0').TrimStart(':'));
            Color tint = status == "Ready" ? new Color32(32,76,40,240) : status == "Burning" ? new Color32(115,48,30,240) : new Color32(16,16,20,235);
            if (card.Background != null) card.Background.SetColorImmediately(tint);
            if (card.Icon != null && card.Icon.SpriteName != job.Icon) card.Icon.SpriteName = job.Icon;
            if (card.Root.ViewComponent.ToolTip != job.Position) card.Root.ViewComponent.ToolTip = job.Position;
        }
        displayed = count;
        var overflow = GetChildById("cookingHudOverflow")?.ViewComponent as XUiV_Label;
        if (overflow != null)
        {
            overflow.IsVisible = jobs.Count > count;
            if (overflow.IsVisible) SetText(overflow, string.Format(Localization.Get("rebirthCookingHudOverflow"), jobs.Count - count));
        }
    }
    private void PositionHud(int columns)
    {
        if (ammoHud == null || pickupHud == null)
        {
            var hud = xui.FindWindowGroupByName("toolbelt")?.GetChildById("HUDRightStatBars");
            if (hud?.Children != null) foreach (var child in hud.Children)
            {
                if (child.GetType().Name == "XUiC_CollectedItemList") pickupHud = child;
                else if (child.ViewComponent?.GetType().Name == "XUiV_Grid") ammoHud = child;
            }
        }
        int visibleCount = Math.Min(jobs.Count, Math.Min(cards.Length, lastRows * columns));
        int top = visibleCount > 0 ? 247 + ((visibleCount - 1) / columns) * 64 : 228;
        int ammoY = visibleCount > 0 ? top + 54 : 228;
        if (ammoHud?.ViewComponent != null) SetPosition(ammoHud.ViewComponent, new Vector2i(-194, ammoY));
        bool ammoVisible = ammoHud?.Children != null && ammoHud.Children.Count > 0 && ammoHud.Children[0].ViewComponent?.IsVisible == true;
        int pickupY = visibleCount > 0 ? top + 24 + (ammoVisible ? 14 : 0) : ammoVisible ? ammoY + 14 : 223;
        if (pickupHud?.ViewComponent != null) SetPosition(pickupHud.ViewComponent, new Vector2i(-90, pickupY));
    }
    private static void Layout(Card card, int width, bool compact)
    {
        card.Root.ViewComponent.Size = new Vector2i(width, 60);
        if (card.Background != null) card.Background.Size = new Vector2i(width, 60);
        if (card.Line != null) { card.Line.Size = new Vector2i(width, 2); card.Line.SetColorImmediately(new Color32(181,140,255,255)); }
        int iconSize = compact ? Math.Min(42, width / 2) : 42;
        if (card.Icon != null) { card.Icon.Size = new Vector2i(iconSize, iconSize); SetPosition(card.Icon, new Vector2i(4, compact ? -2 : -(60-iconSize)/2)); }
        if (card.Name != null) { card.Name.IsVisible = !compact; card.Name.Size = new Vector2i(Math.Max(1,width-54),20); card.Name.Alignment=NGUIText.Alignment.Right; card.Name.FontSize=17; }
        int textX = compact ? iconSize + 7 : 51;
        int textWidth = Math.Max(1,width-textX-3);
        if (card.Time != null) { SetPosition(card.Time,new Vector2i(textX,compact ? -10 : -41)); card.Time.Size=new Vector2i(textWidth,20); card.Time.Alignment=NGUIText.Alignment.Right; card.Time.FontSize=compact ? 17 : 15; }
        if (card.Status != null) { SetPosition(card.Status,new Vector2i(compact ? 4 : textX,compact ? -41 : -23)); card.Status.Size=new Vector2i(compact ? width-8 : textWidth,compact ? 17 : 20); card.Status.Alignment=compact ? NGUIText.Alignment.Left : NGUIText.Alignment.Right; card.Status.FontSize=compact && card.Time != null ? card.Time.FontSize : 15; }
    }
    private static void SetText(XUiV_Label label, string value) { if (label != null && label.Text != value) label.Text = value; }
    private static void SetPosition(XUiView view, Vector2i value)
    { if (view.Position.x != value.x || view.Position.y != value.y) view.Position = value; }
}
