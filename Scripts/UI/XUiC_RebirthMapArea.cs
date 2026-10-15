using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthMapArea : XUiC_MapArea
{
    public const int CanvasWidth=1412, CanvasHeight=712;
    private static bool installed;
    public int ActiveCanvasWidth => GetChildById("mapView")?.ViewComponent?.Size.x ?? CanvasWidth;
    public int ActiveCanvasHeight => GetChildById("mapView")?.ViewComponent?.Size.y ?? CanvasHeight;
    public override void Init(){if(!installed){installed=true;var h=new Harmony("rebirth.map.rectangular");RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWideMapRender));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWideMapPointer));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWideMapDrag));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWideMapLocalPointer));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWideMapHover));}base.Init();}
    public override void OnOpen(){base.OnOpen();ResizeTexture();}
    public override void Update(float dt){FitToNavigation();targetZoomScale=Mathf.Min(targetZoomScale,2048f/336f*ActiveCanvasHeight/ActiveCanvasWidth);base.Update(dt);ResizeTexture();}
    private void ResizeTexture(){int width=ActiveCanvasWidth,height=ActiveCanvasHeight;cTexMiddle=new Vector2i(width/2,height/2);if(xuiTexture!=null)xuiTexture.Size=new Vector2i(width,height);}
    private void FitToNavigation()
    {
        var navigation=xui.GetChildByType<XUiC_RebirthMapChrome>()?.GetChildById("rebirthCraftingTopZone")?.ViewComponent;
        var transform=ViewComponent?.UiTransform;
        var map=GetChildById("mapView");
        if(navigation?.UiTransform==null||transform==null||map?.ViewComponent==null)return;
        // Compare rendered edges in the map's own coordinates: the shared chrome
        // and native windows can have different effective scales at small resolutions.
        int width=Mathf.RoundToInt(transform.InverseTransformPoint(navigation.UiTransform.TransformPoint(new Vector3(navigation.Size.x,0,0))).x);
        if(width<720)return;
        int canvas=width-12;
        if(ViewComponent.Size.x==width&&map.ViewComponent.Size.x==canvas)return;
        SetWidth(this,width);
        var header=GetChildById("header");SetWidth(header,width-3);
        SetWidth(header?.GetChildById("backgroundMain"),width-3);
        var headerBackground=header?.GetChildById("boxbackground");SetWidth(headerBackground,width-3);
        SetWidth(headerBackground?.GetChildById("sprBorder"),width-3);
        SetWidth(headerBackground?.GetChildById("sprBackground"),width-3);
        var content=GetChildById("content");SetWidth(content,width);
        SetWidth(content?.GetChildById("backgroundMain"),width-6);
        var toolbar=GetChildById("toolbar");SetWidth(toolbar,canvas);
        if(toolbar!=null)foreach(var child in toolbar.Children)
            if(child.ViewComponent is XUiV_Sprite&&child.ViewComponent.Size.x>100)SetWidth(child,canvas);
        SetWidth(map,canvas);SetWidth(map.GetChildById("mapViewTexture"),canvas);SetWidth(map.GetChildById("backgroundMain"),canvas);
        var clip=map.GetChildById("clippingPanel")?.ViewComponent as XUiV_Panel;
        if(clip!=null){clip.Size=new Vector2i(canvas,clip.Size.y);clip.ClippingSize=new Vector2(canvas,clip.ClippingSize.y);clip.ClippingCenter=new Vector2(canvas/2f,clip.ClippingCenter.y);}
        var crosshair=map.GetChildById("crosshair")?.ViewComponent;
        if(crosshair!=null)crosshair.Position=new Vector2i(canvas/2,crosshair.Position.y);
        var toggle=header?.GetChildById("switchStaticMap")?.ViewComponent;
        if(toggle!=null)toggle.Position=new Vector2i(width-28,toggle.Position.y);
        var selector=header?.GetChildById("cbxStaticMapType")?.ViewComponent;
        if(selector!=null)selector.Position=new Vector2i(width-268,selector.Position.y);
    }
    private static void SetWidth(XUiController controller,int width)
    {var view=controller?.ViewComponent;if(view!=null)view.Size=new Vector2i(width,view.Size.y);}
    public Vector3 LocalPointer(Vector3 screen){var t=xuiTexture.UiTransform;var ray=xui.playerUI.camera.ScreenPointToRay(screen);float d;return new Plane(t.forward,t.position).Raycast(ray,out d)?t.InverseTransformPoint(ray.GetPoint(d)):Vector3.zero;}
}
[HarmonyPatch(typeof(XUiC_MapArea),"OnPreRender")]
internal static class RebirthWideMapRender
{
    static void Postfix(XUiC_MapArea __instance){var map=__instance as XUiC_RebirthMapArea;if(map==null)return;float sx=__instance.mapScale*map.ActiveCanvasWidth/712f, sy=__instance.mapScale*map.ActiveCanvasHeight/712f;float dx=(sx-__instance.mapScale)/2,dy=(sy-__instance.mapScale)/2;Shader.SetGlobalVector("_MainMapPosAndScale",new Vector4(__instance.mapPos.x-dx,__instance.mapPos.y-dy,sx,sy));Shader.SetGlobalVector("_MainMapBGPosAndScale",new Vector4(__instance.mapBGPos.x-dx,__instance.mapBGPos.y-dy,sx,sy));}
}
[HarmonyPatch(typeof(XUiC_MapArea),"screenPosToWorldPos")]
internal static class RebirthWideMapPointer
{
    static bool Prefix(XUiC_MapArea __instance,Vector3 _mousePos,bool needY,ref Vector3 __result){var map=__instance as XUiC_RebirthMapArea;if(map==null)return true;var p=map.LocalPointer(_mousePos);float x=(p.x-map.cTexMiddle.x)*(.471910119f*map.zoomScale)+map.mapMiddlePosPixel.x,z=(p.y+map.cTexMiddle.y)*(.471910119f*map.zoomScale)+map.mapMiddlePosPixel.y;__result=new Vector3(x,needY?GameManager.Instance.World.GetHeightAt(x,z):0,z);return false;}
}
[HarmonyPatch(typeof(XUiC_MapArea),"DragMap")]
internal static class RebirthWideMapDrag
{
    static void Prefix(XUiC_MapArea __instance,ref Vector2 delta){if(__instance is XUiC_RebirthMapArea && __instance.ViewComponent?.UiTransform!=null)delta/=Mathf.Max(.01f,__instance.ViewComponent.UiTransform.localScale.x);}
}
[Preserve]
public sealed class XUiC_RebirthMapChrome : XUiC_RebirthScreenChrome
{
    protected override RebirthCraftingNavigationService.Destination Destination => RebirthCraftingNavigationService.Destination.Map;
}


[HarmonyPatch(typeof(XUiC_MapArea),"mousePosToWindowPos")]
internal static class RebirthWideMapLocalPointer
{
    static bool Prefix(XUiC_MapArea __instance,Vector3 _mousePos,ref Vector3 __result){var map=__instance as XUiC_RebirthMapArea;if(map==null)return true;var p=map.LocalPointer(_mousePos);__result=new Vector3(p.x,-p.y,0);return false;}
}
[HarmonyPatch(typeof(XUiC_MapArea),"updateMapObjectList")]
internal static class RebirthWideMapHover
{
    static void Postfix(XUiC_MapArea __instance,System.Collections.Generic.List<MapObject> _mapObjectList,bool _bConsiderInOnMouseOverCursor)
    {
        var map=__instance as XUiC_RebirthMapArea;if(map==null||!_bConsiderInOnMouseOverCursor)return;
        var pointer=map.LocalPointer((Vector3)map.xui.playerUI.CursorController.GetScreenPosition());
        if(pointer.x<=712 || pointer.x>=map.ActiveCanvasWidth || pointer.y>=0 || pointer.y<=-map.ActiveCanvasHeight)return;
        foreach(var item in _mapObjectList){if(!item.IsMapIconEnabled())continue;var pos=map.worldPosToScreenPos(item.GetPosition());if(Vector3.Distance(pos,pointer)>=30)continue;map.SetMapCursor(true);map.xui.ToolTipWindow.ToolTip=item.GetName();if(item is MapObjectWaypoint waypoint)map.selectWaypoint(waypoint.waypoint);break;}
    }
}
[Preserve]
public sealed class XUiC_RebirthMapWaypointEntry : XUiC_MapWaypointListEntry
{
    public override void Update(float dt){base.Update(dt);if(Tracking!=null)Tracking.Color=new Color32(143,209,143,255);RebirthMapRowStyle.Apply(Background);}
}
[Preserve]
public sealed class XUiC_RebirthMapInviteEntry : XUiC_MapInvitesListEntry
{
    private XUiV_Sprite trackingIndicator;
    public override void Init(){base.Init();trackingIndicator=GetChildById("Tracking")?.ViewComponent as XUiV_Sprite;}
    public override void Update(float dt){base.Update(dt);if(trackingIndicator!=null)trackingIndicator.Color=new Color32(143,209,143,255);RebirthMapRowStyle.Apply(Background);}
}
internal static class RebirthMapRowStyle
{
    public static void Apply(XUiV_Sprite background)
    {
        if(background==null||background.SpriteName=="ui_game_select_row")return;
        var color=(Color32)background.Color;
        if(color.r==96)background.Color=new Color32(48,48,56,255);
        else if(color.r==64)background.Color=new Color32(24,24,29,250);
    }
}
