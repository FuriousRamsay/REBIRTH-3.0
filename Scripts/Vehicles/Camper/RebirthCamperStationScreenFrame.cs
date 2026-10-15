using System;
using UnityEngine;

// One calculated native Center-anchor frame for cooking and its camper overview.
// Dimensions and margins are the established cooking workspace layout contract.
internal static class RebirthCamperStationScreenFrame
{
    internal const int Width=1872,Height=900;
    private sealed class PriorScale { internal Vector3 Value; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XUiView,PriorScale> priorScales=
        new System.Runtime.CompilerServices.ConditionalWeakTable<XUiView,PriorScale>();
    internal static void Calculate(Vector2i screen,out Vector2i position,out float scale)
    {
        scale=Math.Min(1f,Math.Min((screen.x-4)/(float)Width,(screen.y-144)/(float)Height));
        position=new Vector2i((int)(-Width*scale/2),(int)(screen.y/2-12));
    }
    internal static void RestoreScale(XUiController root)
    {
        var view=root?.ViewComponent;
        if(view!=null&&view.UiTransform!=null&&priorScales.TryGetValue(view,out var prior))
        {view.UiTransform.localScale=prior.Value;priorScales.Remove(view);}
    }
    internal static void Apply(XUiController root)
    {
        if(root?.xui==null||root.ViewComponent==null)return;
        Vector2i screen=root.xui.GetXUiScreenSize();
        Calculate(screen,out var position,out float scale);
        var view=root.ViewComponent;
        if(view.UiTransform!=null&&!priorScales.TryGetValue(view,out _))
            priorScales.Add(view,new PriorScale{Value=view.UiTransform.localScale});
        if(view.UiTransform!=null&&view.UiTransform.localScale!=Vector3.one*scale)
            view.UiTransform.localScale=Vector3.one*scale;
        if(view.Position!=position){view.Position=position;view.TryUpdatePosition();}
    }
}
