using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthMapArea : XUiC_MapArea
{
    public const int CanvasWidth=1412, CanvasHeight=712;
    private static bool installed;
    private readonly RebirthMapOverview overview=new RebirthMapOverview();
    internal RebirthMapOverview Overview=>overview;
    internal readonly RebirthMapInteraction Interaction=new RebirthMapInteraction();
    private readonly RebirthMapBoundary boundary=new RebirthMapBoundary();
    private XUiC_RebirthMapZoomSlider zoomSlider;
    private bool interactionWasSuppressed;
    internal bool RefreshingFittedCenter {get;private set;}
    internal float WorldFitMaximum(float maximum)=>boundary.MaximumZoom(this,maximum);
    internal void FitPlayableView()
    {
        ResizeTexture();
        if(RefreshingFittedCenter||!boundary.Fit(this))return;
        // Reuse native circular terrain-buffer scrolling for the constrained
        // center, before native positionMap and marker transforms consume it.
        RefreshingFittedCenter=true;
        try{DragMap(Vector2.zero);}finally{RefreshingFittedCenter=false;}
    }
    internal XUiC_RebirthMapZoomSlider ZoomSlider=>zoomSlider??(zoomSlider=GetChildByType<XUiC_RebirthMapZoomSlider>());
    internal bool SuppressMapHover=>Interaction.Suppress(this);
    internal void ClearMapHover()
    {
        closestMouseOverNavObject=null;
        if(selectMapSprite!=null){selectMapSprite.transform.localScale=Vector3.one;selectMapSprite=null;}
        SetMapCursor(false);
        if(xui?.ToolTipWindow!=null)xui.ToolTipWindow.ToolTip=string.Empty;
    }
    // The authored controller tree is stable for an open map. Read live views
    // and geometry each frame; retain only successful structural lookups.
    private XUi cachedUi;
    private XUiC_RebirthMapChrome cachedChrome;
    private XUiController cachedTopZone,cachedMapView;
    private void ResetLayoutReferences()
    {cachedUi=xui;cachedChrome=null;cachedTopZone=null;cachedMapView=null;}
    private void CheckLayoutReferenceOwner()
    {if(!ReferenceEquals(cachedUi,xui))ResetLayoutReferences();}
    private XUiController MapView
    {
        get
        {
            CheckLayoutReferenceOwner();
            return cachedMapView??(cachedMapView=GetChildById("mapView"));
        }
    }
    private XUiController NavigationTopZone
    {
        get
        {
            CheckLayoutReferenceOwner();
            if(cachedChrome==null)cachedChrome=xui.GetChildByType<XUiC_RebirthMapChrome>();
            return cachedTopZone??(cachedTopZone=cachedChrome?.GetChildById("rebirthCraftingTopZone"));
        }
    }
    public int ActiveCanvasWidth => MapView?.ViewComponent?.Size.x ?? CanvasWidth;
    public int ActiveCanvasHeight => MapView?.ViewComponent?.Size.y ?? CanvasHeight;
    public override void Init(){ResetLayoutReferences();if(!installed){installed=true;var h=new Harmony("rebirth.map.rectangular");RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWideMapRender));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWideMapPointer));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWideMapDrag));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWideMapLocalPointer));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWideMapHover));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapOverviewZoom));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapResponsiveWheel));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapTeleportBounds));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapHoverSelection));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapObjectHover));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapPressGuard));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapDragGesture));RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapPlayableView));}base.Init();}
    public override void OnOpen(){ResetLayoutReferences();Interaction.Reset();boundary.Reset();zoomSlider=null;interactionWasSuppressed=false;base.OnOpen();ResizeTexture();}
    public override void OnClose(){overview.Close(this);Interaction.Reset();boundary.Reset();zoomSlider=null;interactionWasSuppressed=false;base.OnClose();}
    public override void Update(float dt){FitToNavigation();ResizeTexture();targetZoomScale=Mathf.Min(targetZoomScale,RebirthMapOverview.Limit(this));if(!RebirthMapOverview.Supported(this))zoomScale=Mathf.Min(zoomScale,RebirthMapOverview.NativeMaximum(ActiveCanvasWidth,ActiveCanvasHeight));base.Update(dt);ResizeTexture();overview.Update(this);boundary.Render(this);bool suppressed=SuppressMapHover;if(suppressed){if(!interactionWasSuppressed)closeAllPopups();ClearMapHover();}interactionWasSuppressed=suppressed;}
    private void ResizeTexture(){int width=ActiveCanvasWidth,height=ActiveCanvasHeight;cTexMiddle=new Vector2i(width/2,height/2);if(xuiTexture!=null&&(xuiTexture.Size.x!=width||xuiTexture.Size.y!=height)){xuiTexture.Size=new Vector2i(width,height);if(bMapInitialized)xuiTexture.updateData();}}
    private void FitToNavigation()
    {
        var navigation=NavigationTopZone?.ViewComponent;
        var transform=ViewComponent?.UiTransform;
        var map=MapView;
        if(navigation?.UiTransform==null||transform==null||map?.ViewComponent==null)return;
        // Compare rendered edges in the map's own coordinates: the shared chrome
        // and native windows can have different effective scales at small resolutions.
        int width=Mathf.RoundToInt(transform.InverseTransformPoint(navigation.UiTransform.TransformPoint(new Vector3(navigation.Size.x,0,0))).x);
        if(width<720)return;
        // The map begins six units inside the window; its right edge should
        // meet the window's right edge at every aspect ratio.
        int canvas=width-6;
        if(ViewComponent.Size.x==width&&map.ViewComponent.Size.x==canvas)return;
        SetWidth(this,width);
        var header=GetChildById("header");SetWidth(header,width-3);
        SetWidth(header?.GetChildById("backgroundMain"),width-3);
        var headerBackground=header?.GetChildById("boxbackground");SetWidth(headerBackground,width-3);
        SetWidth(headerBackground?.GetChildById("sprBorder"),width-3);
        SetWidth(headerBackground?.GetChildById("sprBackground"),width-3);
        var content=GetChildById("content");SetWidth(content,width);
        SetWidth(content?.GetChildById("backgroundMain"),width-3);
        var toolbar=GetChildById("toolbar");SetWidth(toolbar,canvas);
        if(toolbar!=null)foreach(var child in toolbar.Children)
            if(child.ViewComponent is XUiV_Sprite&&child.ViewComponent.Size.x>100)SetWidth(child,canvas);
        SetWidth(toolbar?.GetChildById("backgroundMain"),width-3);
        SetWidth(map,canvas);SetWidth(map.GetChildById("mapViewTexture"),canvas);SetWidth(map.GetChildById("backgroundMain"),canvas);
        var clip=map.GetChildById("clippingPanel")?.ViewComponent as XUiV_Panel;
        if(clip!=null){clip.Size=new Vector2i(canvas,clip.Size.y);clip.ClippingSize=new Vector2(canvas,clip.ClippingSize.y);clip.ClippingCenter=new Vector2(canvas/2f,clip.ClippingCenter.y);}
        var crosshair=map.GetChildById("crosshair")?.ViewComponent;
        if(crosshair!=null)crosshair.Position=new Vector2i(canvas/2,crosshair.Position.y);
        var toggle=toolbar?.GetChildById("switchStaticMap")?.ViewComponent;
        if(toggle!=null)toggle.Position=new Vector2i(canvas-28,toggle.Position.y);
        var selector=toolbar?.GetChildById("cbxStaticMapType")?.ViewComponent;
        if(selector!=null)selector.Position=new Vector2i(canvas-206,selector.Position.y);
    }
    private static void SetWidth(XUiController controller,int width)
    {var view=controller?.ViewComponent;if(view!=null)view.Size=new Vector2i(width,view.Size.y);}
    public Vector3 LocalPointer(Vector3 screen){var t=xuiTexture.UiTransform;var ray=xui.playerUI.camera.ScreenPointToRay(screen);float d;return new Plane(t.forward,t.position).Raycast(ray,out d)?t.InverseTransformPoint(ray.GetPoint(d)):Vector3.zero;}
}
[HarmonyPatch(typeof(XUiC_MapArea),"OnPreRender")]
internal static class RebirthWideMapRender
{
    static void Postfix(XUiC_MapArea __instance){var map=__instance as XUiC_RebirthMapArea;if(map==null)return;float sx=__instance.mapScale*map.ActiveCanvasWidth/712f, sy=__instance.mapScale*map.ActiveCanvasHeight/712f;float dx=(sx-__instance.mapScale)/2,dy=(sy-__instance.mapScale)/2;Vector4 terrain;Shader.SetGlobalVector("_MainMapPosAndScale",map.Overview.TryGetUV(map,out terrain)?terrain:new Vector4(__instance.mapPos.x-dx,__instance.mapPos.y-dy,sx,sy));Shader.SetGlobalVector("_MainMapBGPosAndScale",new Vector4(__instance.mapBGPos.x-dx,__instance.mapBGPos.y-dy,sx,sy));}
}
[HarmonyPatch(typeof(XUiC_MapArea),"screenPosToWorldPos")]
internal static class RebirthWideMapPointer
{
    static bool Prefix(XUiC_MapArea __instance,Vector3 _mousePos,bool needY,ref Vector3 __result){var map=__instance as XUiC_RebirthMapArea;if(map==null)return true;var p=map.LocalPointer(_mousePos);float x=(p.x-map.cTexMiddle.x)*(.471910119f*map.zoomScale)+map.mapMiddlePosPixel.x,z=(p.y+map.cTexMiddle.y)*(.471910119f*map.zoomScale)+map.mapMiddlePosPixel.y;__result=new Vector3(x,needY?GameManager.Instance.World.GetHeightAt(x,z):0,z);return false;}
}
[HarmonyPatch(typeof(XUiC_MapArea),"DragMap")]
internal static class RebirthWideMapDrag
{
    static bool Prefix(XUiC_MapArea __instance,ref Vector2 delta)
    {
        var map=__instance as XUiC_RebirthMapArea;
        if(map==null)return true;
        if(!map.RefreshingFittedCenter&&map.ZoomSlider?.BlocksMapInput==true)return false;
        if(!map.RefreshingFittedCenter)map.Interaction.Pan();
        if(map.ViewComponent?.UiTransform!=null)delta/=Mathf.Max(.01f,map.ViewComponent.UiTransform.localScale.x);
        return true;
    }
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
        var map=__instance as XUiC_RebirthMapArea;if(map==null||!_bConsiderInOnMouseOverCursor||map.SuppressMapHover)return;
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





