using System;
using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthBackpackSectionNavigationChrome : XUiController
{
    private Vector2i lastPosition = new Vector2i(int.MinValue, int.MinValue);
    private int lastWidth = -1;
    public override void OnOpen()
    {
        base.OnOpen(); lastWidth = -1; ApplyLayout();
        GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(RebirthCraftingNavigationService.Destination.Crafting);
    }
    public override void Update(float dt)
    {

        ApplyLayout(); base.Update(dt);
    }
    private void ApplyLayout()
    {
        if (xui == null || ViewComponent == null) return;
        Vector2i position, size;
        RebirthScreenLayout.GetScreenBounds(xui, out position, out size);
        if (lastWidth == size.x && lastPosition.x == position.x && lastPosition.y == position.y) return;
        lastPosition = position; lastWidth = size.x;
        ViewComponent.Position = position;
        ViewComponent.Size = new Vector2i(size.x, 60);
        int width = Math.Max(1, size.x - 16);
        Set("rebirthCraftingTopZone", new Vector2i(8, -8), new Vector2i(width, 52));
        Set("rebirthCraftingTopBackground", Vector2i.zero, new Vector2i(width, 52));
        Set("rebirthCraftingTopFrame", Vector2i.zero, new Vector2i(width, 52));
        RebirthPersonalCraftingLayoutService.ApplySharedTopLayout(this, width, false);
        ViewComponent.TryUpdatePosition();
    }
    private void Set(string id, Vector2i position, Vector2i size)
    {
        XUiView view = GetChildById(id)?.ViewComponent;
        if (view == null) return;
        view.Position = position; view.Size = size;
    }
}

