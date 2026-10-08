using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Character body beneath the same fixed 1872/52 navigation shell as PC126 Personal Crafting.
/// Layout changes only on open, resize or an external position reset. It never follows list scrolling.
/// </summary>
public sealed class RebirthCharacterLayoutService
{
    private readonly XUiC_RebirthSurvivorCharacter owner;
    private Vector2i lastScreen;
    private int lastReserve;
    private bool applied;

    public RebirthCharacterLayoutService(XUiC_RebirthSurvivorCharacter owner) { this.owner = owner; }

    public void Apply(bool force)
    {
        if (owner?.xui == null || owner.ViewComponent == null) return;
        Vector2i screen = owner.xui.GetXUiScreenSize();
        if (screen.x <= 0 || screen.y <= 0) return;
        int top = Mathf.Clamp((int)Math.Round(screen.y * 0.0120f), 8, 20);
        int reserve = 132;
        XUiController vitals = owner.xui.FindWindowGroupByName("toolbelt")?.GetChildById("rebirthVitalsLayout");
        if (vitals?.ViewComponent != null && vitals.ViewComponent.Size.y > 0)
            reserve = Math.Max(reserve, vitals.ViewComponent.Size.y + 30);
        int width = Math.Min(1872, Math.Max(1, screen.x - 4));
        int height = Math.Min(935, Math.Max(1, screen.y - top - reserve));
        int x = Math.Max(2, (screen.x - width) / 2) - screen.x / 2;
        int y = screen.y / 2 - top;
        bool positionChanged = owner.ViewComponent.Position.x != x || owner.ViewComponent.Position.y != y
            || owner.ViewComponent.Size.x != width || owner.ViewComponent.Size.y != height;
        if (!force && applied && !positionChanged && lastScreen.x == screen.x && lastScreen.y == screen.y && lastReserve == reserve) return;
        SetRect(owner, x, y, width, height);
        int navWidth = Math.Max(1, width - 16);
        SetRect(owner.GetChildById("rebirthCraftingTopZone"), 8, -8, navWidth, 52);
        SetRect(owner.GetChildById("rebirthCraftingTopBackground"), 0, 0, navWidth, 52);
        SetRect(owner.GetChildById("rebirthCraftingTopFrame"), 0, 0, navWidth, 52);
        RebirthPersonalCraftingLayoutService.ApplySharedTopLayout(owner, navWidth);
        owner.GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(RebirthCraftingNavigationService.Destination.Character);

        // Design coordinates keep a uniform font/slot scale and leave the toolbelt untouched.
        float bodyScale = Math.Min(navWidth / 1856f, Math.Max(1, height - 78) / 813f);
        XUiController body = owner.GetChildById("survivorCharacterBody");
        SetRect(body, 8, -70, 1856, 813);
        SetScale(body, bodyScale);
        foreach (string id in new[] { "survivorProgressionPanel", "survivorConditionPanel" })
            SetScale(owner.GetChildById(id), Math.Min(1666f / 1744f, 813f / 851f));
        SetScale(owner.GetChildById("survivorStatisticsPanel"), 1666f / 1744f);
        SetScale(owner.GetChildById("survivorCharacterLegacyPages"), Math.Min(1666f / 1424f, 813f / 742f));
        ApplyBackgroundOpacity();
        lastScreen = screen; lastReserve = reserve; applied = true;
    }

    public int ApplyBackgroundOpacity()
    {
        int matched;
        return RebirthPersonalCraftingPanelOpacity.ApplyCharacter(owner, out matched);
    }

    private static void SetRect(XUiController c, int x, int y, int width, int height)
    {
        XUiView v = c?.ViewComponent;
        if (v == null) return;
        if (v.Position.x != x || v.Position.y != y) v.Position = new Vector2i(x, y);
        if (v.Size.x != width || v.Size.y != height) v.Size = new Vector2i(width, height);
        v.TryUpdatePosition();
    }
    private static void SetScale(XUiController c, float value)
    {
        Transform t = c?.ViewComponent?.UiTransform;
        if (t == null) return;
        Vector3 scale = new Vector3(value, value, 1f);
        if (t.localScale != scale) t.localScale = scale;
    }
}
