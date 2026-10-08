using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// Same screen bounds, top reserve, toolbelt reserve and navigation layout as Character.
public sealed class RebirthScreenLayout
{
    public static int ActiveCount { get; private set; }
    private readonly XUiController owner;
    private readonly RebirthWindowHudScope hud = new RebirthWindowHudScope();
    private readonly Dictionary<XUiController, Vector2i> positions = new Dictionary<XUiController, Vector2i>();
    private bool open;
    private Vector2i lastScreen = new Vector2i(-1,-1);
    private int lastWidth = -1, lastHeight = -1;
    private float nextHudMaintain;
    private sealed class HudBounds
    {
        public XUiController Toolbelt, Vitals;
    }
    private static readonly ConditionalWeakTable<XUi, HudBounds> hudBounds = new ConditionalWeakTable<XUi, HudBounds>();
    public RebirthScreenLayout(XUiController owner) { this.owner = owner; }
    public void Open() { if (!open) { open = true; ActiveCount++; } RebirthPersonalCraftingHudSuppressionInstaller.EnsureInstalled(); nextHudMaintain=0f; lastScreen=new Vector2i(-1,-1); Apply(); }
    public void Close() { if (open) { open = false; ActiveCount = Math.Max(0, ActiveCount - 1); } hud.Restore(); }
    public static void GetScreenBounds(XUi ui, out Vector2i position, out Vector2i size)
    {
        Vector2i screen = ui.GetXUiScreenSize();
        int top = Mathf.Clamp((int)Math.Round(screen.y * .012f), 8, 20), reserve = 132;
        var cached = hudBounds.GetValue(ui, key => new HudBounds());
        var toolbelt = ui.FindWindowGroupByName("toolbelt");
        if (!ReferenceEquals(cached.Toolbelt, toolbelt) || cached.Vitals == null)
        {
            cached.Toolbelt = toolbelt;
            cached.Vitals = toolbelt?.GetChildById("rebirthVitalsLayout");
        }
        var vitals = cached.Vitals;
        if (vitals?.ViewComponent != null) reserve = Math.Max(reserve, vitals.ViewComponent.Size.y + 30);
        int width = Math.Min(1872, Math.Max(1, screen.x - 4));
        int height = Math.Min(935, Math.Max(1, screen.y - top - reserve));
        position = new Vector2i(Math.Max(2, (screen.x - width) / 2) - screen.x / 2, screen.y / 2 - top);
        size = new Vector2i(width, height);
    }
    public void Apply()
    {
        if (!open) return;
        if (Time.realtimeSinceStartup >= nextHudMaintain)
        {
            nextHudMaintain = Time.realtimeSinceStartup + 0.25f;
            hud.Maintain(owner.xui);
        }
        var screen = owner.xui.GetXUiScreenSize();
        Vector2i boundsPosition, boundsSize;
        GetScreenBounds(owner.xui, out boundsPosition, out boundsSize);
        int width = boundsSize.x, height = boundsSize.y, x = boundsPosition.x, y = boundsPosition.y;
        bool ownerStable=owner.ViewComponent.Position.x==x&&owner.ViewComponent.Position.y==y&&owner.ViewComponent.Size.x==width&&owner.ViewComponent.Size.y==height;
        if (ownerStable&&screen.x==lastScreen.x && screen.y==lastScreen.y && width==lastWidth && height==lastHeight) return;
        lastScreen=screen; lastWidth=width; lastHeight=height;
        owner.ViewComponent.Position = new Vector2i(x,y); owner.ViewComponent.Size = new Vector2i(width,height); owner.ViewComponent.TryUpdatePosition();
        int navWidth = width - 16;
        Set(owner.GetChildById("rebirthCraftingTopZone"),new Vector2i(8,-8),new Vector2i(navWidth,52));
        Set(owner.GetChildById("rebirthCraftingTopBackground"),Vector2i.zero,new Vector2i(navWidth,52));
        Set(owner.GetChildById("rebirthCraftingTopFrame"),Vector2i.zero,new Vector2i(navWidth,52));
        RebirthPersonalCraftingLayoutService.ApplySharedTopLayout(owner,navWidth);
        float scale = Math.Min(navWidth/1856f, Math.Max(1,height-78)/813f);
        bool creative = owner is XUiC_RebirthCreativeWorkspace;
        bool journal = owner is XUiC_RebirthJournal || creative;
        if(creative)scale=Math.Min(navWidth/1856f,Math.Max(1,height-16)/889f);
        var children = journal ? owner.Children : owner.Parent.Children;
        foreach (var child in children)
        {
            if (child == owner || child.ViewComponent == null) continue;
            if (journal ? (child.ViewComponent.ID != "journalIndex" && child.ViewComponent.ID != "journalContent" && child.ViewComponent.ID != "journalImageOverlay" && child.ViewComponent.ID != "creativeCatalogue" && child.ViewComponent.ID != "creativeInventory") : !(child.ViewComponent is XUiV_Window)) continue;
            if (!positions.ContainsKey(child)) positions.Add(child,child.ViewComponent.Position);
            var p=positions[child];
            child.ViewComponent.Position = journal ? new Vector2i(8+Mathf.RoundToInt((p.x-8)*scale),(creative?-8:-70)+Mathf.RoundToInt((p.y+70)*scale)) : new Vector2i(x+8+Mathf.RoundToInt((p.x+928)*scale),y-70+Mathf.RoundToInt((p.y-435)*scale));
            child.ViewComponent.TryUpdatePosition();
            if(child.ViewComponent.UiTransform!=null)child.ViewComponent.UiTransform.localScale=new Vector3(scale,scale,1);
        }
    }
    private static void Set(XUiController c,Vector2i p,Vector2i s){if(c?.ViewComponent==null)return;c.ViewComponent.Position=p;c.ViewComponent.Size=s;}
}
public abstract class XUiC_RebirthScreenChrome : XUiController
{
    private RebirthScreenLayout layout;
    private XUiController header;
    private bool headerVisible;
    protected abstract RebirthCraftingNavigationService.Destination Destination { get; }
    public override void OnOpen(){base.OnOpen();layout=layout??new RebirthScreenLayout(this);layout.Open();GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(Destination);header=xui.FindWindowGroupByName("windowpaging")?.GetChildById("windowPagingHeader");if(header?.ViewComponent!=null){headerVisible=header.ViewComponent.IsVisible;header.ViewComponent.IsVisible=false;}}
    public override void Update(float dt){base.Update(dt);layout?.Apply();if(header?.ViewComponent!=null)header.ViewComponent.IsVisible=false;}
    public override void OnClose(){layout?.Close();if(header?.ViewComponent!=null)header.ViewComponent.IsVisible=headerVisible;base.OnClose();}
}
