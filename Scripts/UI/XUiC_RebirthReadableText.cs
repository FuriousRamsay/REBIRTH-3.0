using UnityEngine.Scripting;

/// <summary>Native scrolling prose, with a fresh reading position when its subject changes.</summary>
[Preserve]
public sealed class XUiC_RebirthReadableText : XUiController
{
    private XUiV_Label text;
    private XUiV_ScrollView viewport;
    private string previous;
    private int previousTextWidth = -1, previousViewportWidth = -1, previousViewportHeight = -1;
    private int resetFrames;
    private bool resetOnTextChange = true;
    private bool resetPosition;

    public override bool ParseAttribute(string name, string value)
    {
        if (name == "reset_on_text_change") { resetOnTextChange = value != "false"; return true; }
        return base.ParseAttribute(name, value);
    }

    public void ResetReadingPosition()
    {
        resetPosition = true;
        resetFrames = 2;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ResetReadingPosition();
    }

    public override void Init()
    {
        base.Init();
        var body = GetChildById("readableTextViewport");
        viewport = body?.ViewComponent as XUiV_ScrollView;
        if (body != null)
            foreach (var child in body.Children)
                if (child.ViewComponent is XUiV_Label label) { text = label; break; }
    }

    public void SetBounds(int width,int height)
    {
        if(ViewComponent.Size.x==width&&ViewComponent.Size.y==height)return;
        ViewComponent.Size=new Vector2i(width,height);
        if(viewport!=null)viewport.Size=new Vector2i(System.Math.Max(1,width-22),height);
        if(text!=null)text.Size=new Vector2i(System.Math.Max(1,width-30),height);
        foreach(var child in Children)if(child.ViewComponent is XUiV_ScrollBar bar)RebirthScrollbarPresentation.SizeNativeHost(bar,height);
        ResetReadingPosition();
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (text == null || viewport == null) return;
        if (previous != text.Text)
        {
            previous = text.Text;
            // Allow the native label to measure its new wrapped height first.
            resetFrames = 2;
            if (resetOnTextChange) resetPosition = true;
        }
        if (previousTextWidth != text.Size.x || previousViewportWidth != viewport.Size.x ||
            previousViewportHeight != viewport.Size.y)
        {
            previousTextWidth = text.Size.x;
            previousViewportWidth = viewport.Size.x;
            previousViewportHeight = viewport.Size.y;
            // Reflow can change content bounds without changing the actual prose.
            // Preserve the reading position; only a new subject explicitly resets it.
            resetFrames = 2;
        }
        // Do not consume the pending reset while the native scroll view is still being created.
        if (resetFrames > 0 && viewport.scrollView != null && --resetFrames == 0)
        {
            viewport.scrollView.InvalidateBounds();
            if (resetPosition) viewport.scrollView.ResetPosition();
            resetPosition = false;
        }
    }
}
