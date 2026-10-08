using UnityEngine;
using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthDebugMenu : XUiC_InGameDebugMenu
{
    public override void OnOpen() { base.OnOpen(); Fit(); }
    public override void Update(float dt) { base.Update(dt); Fit(); }
    private void Fit()
    {
        // The table hides debug-only rows dynamically. Measure its final button rather
        // than retaining empty space for every possible row.
        var rows = GetChildById("rebirthDebugRows");
        float height = 300;
        if (rows != null && ViewComponent.UiTransform != null)
        {
            foreach (var row in rows.Children)
            {
                var view = row.ViewComponent;
                if (view == null || !view.IsVisible || view.UiTransform == null) continue;
                var local = ViewComponent.UiTransform.InverseTransformPoint(view.UiTransform.position);
                height = Mathf.Max(height, -local.y + view.Size.y + 12);
            }
        }
        ViewComponent.Size = new Vector2i(300, Mathf.CeilToInt(height));
        var content = GetChildById("content");
        if (content != null)
        {
            int bodyHeight = Mathf.CeilToInt(height) - 42;
            content.ViewComponent.Height = bodyHeight;
            // Template dimensions are fixed at XML expansion, so resize its fill too.
            var body = content.GetChildById("boxbackground");
            if (body != null)
            {
                body.ViewComponent.Height = bodyHeight;
                foreach (var child in body.Children) if (child.ViewComponent != null) child.ViewComponent.Height = bodyHeight;
            }
        }
        float scale = Mathf.Min(1, (xui.GetXUiScreenSize().y-100)/height);
        ViewComponent.Position = new Vector2i(-Mathf.RoundToInt(324*scale),-80);
        ViewComponent.TryUpdatePosition();
        if(ViewComponent.UiTransform != null) ViewComponent.UiTransform.localScale=new Vector3(scale,scale,1);
    }
}
