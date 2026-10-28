using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthMapZoomSlider : XUiController
{
    internal const int ControlWidth=348,ControlHeight=76,Inset=12;
    private XUiC_RebirthMapArea map;
    private XUiController viewport,track;
    private XUiV_Sprite fill,thumb;
    private XUiV_Label spanLabel;
    private bool held;
    private int releasedFrame=-2;
    internal bool BlocksMapInput=>held||Time.frameCount<=releasedFrame+1||ContainsPointer();
    private bool ContainsPointer()
    {
        if(map==null||!map.isOpen||ViewComponent?.IsVisible!=true)return false;
        var camera=map.xui?.playerUI?.camera;var transform=ViewComponent.UiTransform;
        if(camera==null||transform==null)return false;
        var ray=camera.ScreenPointToRay(map.xui.playerUI.CursorController.GetScreenPosition());
        if(!new Plane(transform.forward,transform.position).Raycast(ray,out float distance))return false;
        var point=transform.InverseTransformPoint(ray.GetPoint(distance));
        return point.x>=0&&point.x<=ControlWidth&&point.y<=0&&point.y>=-ControlHeight;
    }
    private void EndGesture(){held=false;releasedFrame=Time.frameCount;map?.Interaction.Zoom();}
    private float paintedZoom=float.NaN,paintedMaximum;
    private int paintedWidth;
    private bool CanInteract=>map!=null&&map.isOpen&&map.bMapInitialized&&track?.ViewComponent?.Enabled==true;

    public override void Init()
    {
        base.Init();track=GetChildById("zoomTrack");
        fill=GetChildById("zoomFill")?.ViewComponent as XUiV_Sprite;
        var handle=GetChildById("zoomThumb");thumb=handle?.ViewComponent as XUiV_Sprite;
        spanLabel=GetChildById("zoomSpan")?.ViewComponent as XUiV_Label;
        BindSurface(track);BindSurface(handle);
        var background=GetChildById("zoomBackground");if(background!=null)background.OnScroll+=Scroll;
    }
    private void BindSurface(XUiController surface)
    {
        if(surface!=null)
        {
            // Same press/drag lifecycle as the POI light-selection slider.
            surface.OnMouseUpDown+=(sender,pressed)=>
            {
                if(pressed){if(!CanInteract||!Input.GetMouseButton(0))return;held=true;map.Interaction.Zoom();PreviewPointer();}
                else if(held){PreviewPointer();EndGesture();}
            };
            surface.OnDrag+=(sender,type,delta)=>
            {
                if(!held)return;
                if(type==EDragType.Dragging)PreviewPointer();
                else if(type==EDragType.DragEnd){PreviewPointer();EndGesture();}
            };
            surface.OnScroll+=Scroll;
        }
    }
    public override void OnOpen()
    {
        base.OnOpen();held=false;releasedFrame=-2;paintedZoom=float.NaN;map=GetParentByType<XUiC_RebirthMapArea>();viewport=map?.GetChildById("mapView");
        bool ready=map!=null&&map.bMapInitialized&&Layout();
        if(ViewComponent!=null)ViewComponent.IsVisible=ready;if(ready)Paint();
    }
    public override void OnClose(){held=false;map=null;viewport=null;base.OnClose();}
    public override void Update(float dt)
    {
        base.Update(dt);
        if(map==null){map=GetParentByType<XUiC_RebirthMapArea>();viewport=map?.GetChildById("mapView");}
        bool visible=map!=null&&map.isOpen&&map.bMapInitialized&&Layout();
        if(ViewComponent!=null&&ViewComponent.IsVisible!=visible)ViewComponent.IsVisible=visible;
        if(!visible){held=false;return;}
        if(!CanInteract)held=false;
        else if(held){PreviewPointer();if(!Input.GetMouseButton(0))EndGesture();}
        Paint();
    }
    private void Scroll(XUiController sender,float delta)
    {if(CanInteract&&!held)map.onMapScrolled(sender,delta);}
    private bool Layout()
    {
        var view=viewport?.ViewComponent;var parent=Parent?.ViewComponent?.UiTransform;
        if(view?.UiTransform==null||parent==null||ViewComponent==null)return false;
        // The overlay is a WINDOW sibling of mapView, so its input never
        // bubbles into the viewport's pan/right-click-teleport handlers.
        var point=parent.InverseTransformPoint(view.UiTransform.TransformPoint(new Vector3(view.Size.x,-view.Size.y,0)));
        var top=parent.InverseTransformPoint(view.UiTransform.position);
        if(Mathf.Abs(point.x-top.x)<ControlWidth+Inset*2||Mathf.Abs(point.y-top.y)<ControlHeight+Inset*2)return false;
        var position=new Vector2i(Mathf.RoundToInt(point.x)-ControlWidth-Inset,Mathf.RoundToInt(point.y)+ControlHeight+Inset);
        if(ViewComponent.Position.x!=position.x||ViewComponent.Position.y!=position.y)ViewComponent.Position=position;
        if(track?.ViewComponent!=null)
        {
            bool enabled=map.bMapInitialized&&RebirthMapOverview.Limit(map)>.7f;
            if(track.ViewComponent.Enabled!=enabled)track.ViewComponent.Enabled=enabled;
            if(thumb!=null&&thumb.Enabled!=enabled)thumb.Enabled=enabled;
        }
        return true;
    }
    private void PreviewPointer()
    {
        if(!held||!CanInteract)return;
        var transform=track.ViewComponent.UiTransform;var camera=UICamera.currentCamera;
        if(camera==null)camera=map.xui.playerUI.camera;
        if(transform==null||camera==null||track.ViewComponent.Size.x<=0)return;
        Vector2 mouse=UICamera.currentTouch!=null?UICamera.currentTouch.pos:map.xui.playerUI.CursorController.GetScreenPosition();
        var ray=camera.ScreenPointToRay(mouse);
        if(!new Plane(transform.forward,transform.position).Raycast(ray,out float distance))return;
        float fraction=Mathf.Clamp01(transform.InverseTransformPoint(ray.GetPoint(distance)).x/track.ViewComponent.Size.x);
        // Direct manipulation follows the pointer immediately; release leaves
        // no queued easing or delayed jump. Wheel/trigger easing keeps the
        // installed responsive input behavior.
        map.Interaction.Zoom();map.zoomScale=map.targetZoomScale=ZoomAt(fraction,RebirthMapOverview.Limit(map));Paint();
    }
    internal static float ZoomAt(float fraction,float maximum)
    {
        maximum=Mathf.Max(.7f,maximum);fraction=Mathf.Clamp01(fraction);
        if(fraction<=0)return .7f;if(fraction>=1)return maximum;
        return Mathf.Exp(Mathf.Lerp(Mathf.Log(.7f),Mathf.Log(maximum),fraction));
    }
    internal static float FractionAt(float zoom,float maximum)
    {return maximum<=.7f?0:Mathf.Clamp01(Mathf.Log(Mathf.Clamp(zoom,.7f,maximum)/.7f)/Mathf.Log(maximum/.7f));}
    private void Paint()
    {
        if(map==null)return;
        float maximum=RebirthMapOverview.Limit(map);
        if(paintedZoom==map.zoomScale&&paintedMaximum==maximum&&paintedWidth==map.ActiveCanvasWidth)return;
        paintedZoom=map.zoomScale;paintedMaximum=maximum;paintedWidth=map.ActiveCanvasWidth;
        float fraction=FractionAt(map.zoomScale,maximum);
        if(fill!=null&&Mathf.Abs(fill.Fill-fraction)>.0001f)fill.Fill=fraction;
        if(thumb!=null&&track?.ViewComponent!=null)
        {
            var old=thumb.Position;int x=track.ViewComponent.Position.x+Mathf.RoundToInt(fraction*track.ViewComponent.Size.x)-thumb.Size.x/2;
            if(old.x!=x)thumb.Position=new Vector2i(x,old.y);
        }
        if(spanLabel!=null)
        {
            string text=string.Format(Localization.Get("xuiRebirthMapZoomSpan"),map.ActiveCanvasWidth*RebirthMapOverview.MetersPerUnit*map.zoomScale/1000f);
            if(spanLabel.Text!=text)spanLabel.Text=text;
        }
    }
}

