using UnityEngine;
using UnityEngine.Scripting;

// Instantiated only by the Rebirth-progression XML branch.
[Preserve]
public sealed class XUiC_RebirthConversationChrome : XUiController
{
    private static readonly string[] ChromeIds = { "conversationFrame", "conversationRule", "conversationIcon", "conversationHint" };
    private readonly XUiView[] chrome = new XUiView[ChromeIds.Length];
    private XUiC_RebirthDialogResponseList owner;
    private XUiV_Sprite background;
    private XUiV_Label name;
    public override void Init()
    {
        base.Init();
        CacheViews();
    }
    public override void OnOpen()
    {
        base.OnOpen();
        CacheViews();
    }
    private void CacheViews()
    {
        owner = Parent as XUiC_RebirthDialogResponseList;
        for (int i=0;i<ChromeIds.Length;i++) chrome[i]=GetChildById(ChromeIds[i])?.ViewComponent;
        background=owner?.GetChildById("background")?.ViewComponent as XUiV_Sprite;
        name=owner?.GetChildById("lblName")?.ViewComponent as XUiV_Label;
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (owner == null || windowGroup?.isShowing != true) return;
        bool show = !owner.IsJobCardMode;
        foreach (var view in chrome)
            if(view!=null && view.IsVisible!=show)view.IsVisible=show;
        if (!show) return;
        var size = owner.ViewComponent.Size;
        if (background != null)
        {
            if(background.SpriteName!="menu_empty")background.SpriteName="menu_empty";
            var color=new Color32(24,24,29,250);
            if(background.Color != (Color)color)background.Color=color;
        }
        if(chrome[0]!=null && chrome[0].Size!=size)chrome[0].Size=size;
        var ruleSize=new Vector2i(size.x,2);
        if(chrome[1]!=null && chrome[1].Size!=ruleSize)chrome[1].Size=ruleSize;
        var hintPosition=new Vector2i(size.x-248,-14);
        if(chrome[3]!=null && chrome[3].Position!=hintPosition)chrome[3].Position=hintPosition;
        if(name!=null)
        {
            var position=new Vector2i(52,-10);
            var nameSize=new Vector2i(Mathf.Max(100,size.x-316),32);
            var color=new Color32(181,140,255,255);
            if(name.Position!=position)name.Position=position;
            if(name.Size!=nameSize)name.Size=nameSize;
            if(name.Color != (Color)color)name.Color=color;
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
        if (nativeHeader?.ViewComponent?.IsVisible == true) nativeHeader.ViewComponent.IsVisible = false;
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
    private readonly XUiV_Label[] labels = new XUiV_Label[6];
    private readonly XUiView[] buttons = new XUiView[6];
    private XUiView heading;
    public override void Update(float dt)
    {
        base.Update(dt);
        bool any = false;
        for (int i = 1; i <= 6; i++)
        {
            var label = labels[i-1] ?? (labels[i-1] = GetChildById("topicLabel" + i)?.ViewComponent as XUiV_Label);
            var button = buttons[i-1] ?? (buttons[i-1] = GetChildById("topic" + i)?.ViewComponent);
            bool visible = !string.IsNullOrWhiteSpace(label?.Text);
            if (button != null && button.IsVisible != visible) button.IsVisible = visible;
            any |= visible;
        }
        heading = heading ?? GetChildById("topicsHeading")?.ViewComponent;
        if (heading != null && heading.IsVisible != any) heading.IsVisible = any;
    }
}
