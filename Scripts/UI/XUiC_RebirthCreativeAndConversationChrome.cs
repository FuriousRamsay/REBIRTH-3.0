using UnityEngine;
using UnityEngine.Scripting;

// Instantiated only by the Rebirth-progression XML branch.
[Preserve]
public sealed class XUiC_RebirthConversationChrome : XUiController
{
    public override void Update(float dt)
    {
        base.Update(dt);
        var owner = Parent as XUiC_RebirthDialogResponseList;
        if (owner == null) return;
        bool show = !owner.IsJobCardMode;
        foreach (string id in new[] { "conversationFrame", "conversationRule", "conversationIcon", "conversationHint" })
            GetChildById(id).ViewComponent.IsVisible = show;
        if (!show) return;
        var size = owner.ViewComponent.Size;
        var background = owner.GetChildById("background")?.ViewComponent as XUiV_Sprite;
        if (background != null)
        {
            background.SpriteName = "menu_empty";
            background.Color = new Color32(24, 24, 29, 250);
        }
        GetChildById("conversationFrame").ViewComponent.Size = size;
        GetChildById("conversationRule").ViewComponent.Size = new Vector2i(size.x, 2);
        var hint = GetChildById("conversationHint").ViewComponent;
        hint.Position = new Vector2i(size.x - 248, -14);
        var name = owner.GetChildById("lblName")?.ViewComponent as XUiV_Label;
        if (name != null)
        {
            name.Position = new Vector2i(52, -10);
            name.Size = new Vector2i(Mathf.Max(100, size.x - 316), 32);
            name.Color = new Color32(181, 140, 255, 255);
        }
    }
}

[Preserve]
public sealed class XUiC_RebirthCreativeChrome : XUiController
{
    private XUiController nativeHeader;
    private bool wasVisible;
    public override void OnOpen()
    {
        base.OnOpen();
        GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(RebirthCraftingNavigationService.Destination.Inventory);
        nativeHeader = xui.FindWindowGroupByName("windowpaging")?.GetChildById("windowPagingHeader");
        if (nativeHeader?.ViewComponent != null)
        {
            wasVisible = nativeHeader.ViewComponent.IsVisible;
            nativeHeader.ViewComponent.IsVisible = false;
        }
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (nativeHeader?.ViewComponent != null) nativeHeader.ViewComponent.IsVisible = false;
    }
    public override void OnClose()
    {
        if (nativeHeader?.ViewComponent != null) nativeHeader.ViewComponent.IsVisible = wasVisible;
        base.OnClose();
    }
}

[Preserve]
public sealed class XUiC_RebirthConversationTopics : XUiController
{
    public override void Update(float dt)
    {
        base.Update(dt);
        bool any = false;
        for (int i = 1; i <= 6; i++)
        {
            var label = GetChildById("topicLabel" + i)?.ViewComponent as XUiV_Label;
            var button = GetChildById("topic" + i)?.ViewComponent;
            bool visible = !string.IsNullOrWhiteSpace(label?.Text);
            if (button != null) button.IsVisible = visible;
            any |= visible;
        }
        var heading = GetChildById("topicsHeading")?.ViewComponent;
        if (heading != null) heading.IsVisible = any;
    }
}
