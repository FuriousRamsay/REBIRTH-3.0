using System;
using UnityEngine;

/// <summary>Shared scrollbar presentation. Owners supply bounds, content extent and offset;
/// they retain native population and inventory custody, never separate visual rules.</summary>
public static class RebirthScrollbarPresentation
{
    public const int TrackWidth = 16, ThumbWidth = 12, MinimumThumb = 30;
    public static float Fraction(float viewport,float content) => Mathf.Clamp01(viewport/Math.Max(1f,content));
    public static int ThumbHeight(int height, float viewport, float content)
        => Mathf.Clamp(Mathf.RoundToInt(height * Fraction(viewport,content)),
            Math.Min(MinimumThumb, height), height);

    public static float NativeFraction(int height,float viewport,float content)
        => ThumbHeight(Math.Max(1,height),viewport,content)/(float)Math.Max(1,height);
    public static void SizeNativeHost(XUiV_ScrollBar bar,int height)
    {
        if(bar==null)return;
        height=Math.Max(1,height);
        bar.Size=new Vector2i(TrackWidth,height);
        CommitGeometry(bar);
        foreach(string id in new[]{"scrollbarbackground","scrollbarborder"})
        {
            var view=bar.Controller?.GetChildById(id)?.ViewComponent;
            if(view==null)continue;
            view.Size=new Vector2i(TrackWidth,height);CommitGeometry(view);
        }
    }
    public static void Render(XUiController track, XUiController thumb, Vector2i origin,
        int height, float viewport, float content, float offset, int trackWidth=TrackWidth, int thumbWidth=ThumbWidth)
    {
        if(track?.ViewComponent == null || thumb?.ViewComponent == null) return;
        height = Math.Max(1, height);
        float maximum = Math.Max(0f, content - viewport);
        bool show = maximum > .5f;
        track.ViewComponent.IsVisible = thumb.ViewComponent.IsVisible = show;
        if(!show) return;
        int thumbHeight = ThumbHeight(height, viewport, content);
        int y = Mathf.RoundToInt((height - thumbHeight) * Mathf.Clamp01(offset / maximum));
        track.ViewComponent.Position = origin;
        track.ViewComponent.Size = new Vector2i(trackWidth, height);
        thumb.ViewComponent.Position = new Vector2i(origin.x + (trackWidth - thumbWidth)/2, origin.y - y);
        thumb.ViewComponent.Size = new Vector2i(thumbWidth, thumbHeight);
        CommitGeometry(track); CommitGeometry(thumb);
    }

    public static void RenderThumb(XUiView thumb,int height,float viewport,float content,float offset,
        int width,int left=1,int top=0)
    {
        if(thumb==null)return;
        bool show=content>viewport&&viewport>0;
        thumb.IsVisible=show;
        if(!show)return;
        int h=ThumbHeight(height,viewport,content);
        int y=Mathf.RoundToInt((height-h)*Mathf.Clamp01(offset/Math.Max(1f,content-viewport)));
        thumb.Size=new Vector2i(width,h);
        thumb.Position=new Vector2i(left,top-y);
        CommitGeometry(thumb);
    }
    public static void CommitGeometry(XUiController controller) => CommitGeometry(controller?.ViewComponent);
    public static void CommitGeometry(XUiView view)
    {

        if(view?.UiTransform == null) return;
        view.TryUpdatePosition();
        var widget = view.UiTransform.GetComponent<UIWidget>();
        if(widget != null) { widget.pivot = UIWidget.Pivot.TopLeft; widget.width = view.Size.x; widget.height = view.Size.y; }
        var collider = view.UiTransform.GetComponent<BoxCollider>();
        if(collider != null)
        {
            collider.center = new Vector3(view.Size.x*.5f,-view.Size.y*.5f,collider.center.z);
            collider.size = new Vector3(view.Size.x,view.Size.y,collider.size.z);
        }
    }
}