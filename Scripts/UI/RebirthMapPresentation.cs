using HarmonyLib;
using UnityEngine;

// One interaction gate shared by native hover, native context actions and the
// Purge postfix. Frame settling handles event/update ordering without a timer.
internal sealed class RebirthMapInteraction
{
    private int zoomFrame=-3,panFrame=-3;
    private bool dragging;
    internal void Drag(bool active){dragging=active;Pan();}
    internal void Zoom(){zoomFrame=Time.frameCount;}
    internal void Pan(){panFrame=Time.frameCount;}
    internal void Reset(){zoomFrame=panFrame=-3;dragging=false;}
    internal bool Suppress(XUiC_RebirthMapArea map)
    {
        if(map.ZoomSlider?.BlocksMapInput==true)return true;
        if(dragging){if(Input.GetMouseButton(0))return true;dragging=false;}
        return IsMoving(map.zoomScale,map.targetZoomScale,Time.frameCount,zoomFrame,panFrame);
    }
    internal static bool IsMoving(float current,float target,int frame,int zoom,int pan)
        =>current!=target||frame<=zoom+1||frame<=pan+1;
}

[HarmonyPatch(typeof(XUiC_MapArea),"UpdateWaypointSelection")]
internal static class RebirthMapHoverSelection
{
    private static bool Prefix(XUiC_MapArea __instance)
    {
        var map=__instance as XUiC_RebirthMapArea;
        if(map==null||!map.SuppressMapHover)return true;
        map.ClearMapHover();return false;
    }
}
[HarmonyPatch(typeof(XUiC_MapArea),"updateMapObjectList")]
internal static class RebirthMapObjectHover
{
    private static void Prefix(XUiC_MapArea __instance,ref bool _bConsiderInOnMouseOverCursor)
    {if(__instance is XUiC_RebirthMapArea map&&map.SuppressMapHover)_bConsiderInOnMouseOverCursor=false;}
}
[HarmonyPatch]
internal static class RebirthMapPressGuard
{
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(XUiC_MapArea),"onMapPressed");
        yield return AccessTools.Method(typeof(XUiC_MapArea),"OpenWaypointPopup");
    }
    private static bool Prefix(XUiC_MapArea __instance)
        =>!(__instance is XUiC_RebirthMapArea map)||!map.SuppressMapHover;
}

// Borderless native UI textures cover only the projected outside area. Interior fog,
// native terrain shader and marker positions are untouched. No terrain reads.
internal sealed class RebirthMapBoundary
{
    private World world;
    private bool ready;
    private Vector3 minimum,maximum;
    private readonly XUiView[] masks=new XUiView[8];
    private readonly RectInt[] geometry=new RectInt[8];
    private static readonly string[] ids={"worldOutsideLeft","worldOutsideRight","worldOutsideTop","worldOutsideBottom","worldBorderLeft","worldBorderRight","worldBorderTop","worldBorderBottom"};
    internal void Reset(){world=null;ready=false;for(int i=0;i<masks.Length;i++)masks[i]=null;}
    private bool Acquire(XUiC_RebirthMapArea map)
    {
        var current=map.localPlayer?.world;
        if(!ReferenceEquals(world,current)){world=current;ready=false;}
        if(!ready&&world!=null)
        {
            Vector3i low,high;
            if(world.GetWorldExtent(out low,out high))
            {
                minimum=world.ClampToValidWorldPos(new Vector3(-100000000f,180,-100000000f));
                maximum=world.ClampToValidWorldPos(new Vector3(100000000f,180,100000000f));
                ready=maximum.x>minimum.x&&maximum.z>minimum.z;
            }
        }
        return ready&&PrefabEditModeManager.Instance?.IsActive()!=true;
    }
    internal float MaximumZoom(XUiC_RebirthMapArea map,float atlasMaximum)
        =>Acquire(map)?FitMaximum(maximum.z-minimum.z,map.ActiveCanvasHeight,atlasMaximum):atlasMaximum;
    internal static float FitMaximum(float worldHeight,int canvasHeight,float atlasMaximum)
        =>Mathf.Max(.7f,Mathf.Min(atlasMaximum,worldHeight/(Mathf.Max(1,canvasHeight)*RebirthMapOverview.MetersPerUnit)));
    internal bool Fit(XUiC_RebirthMapArea map)
    {
        if(!map.bMapInitialized||!Acquire(map))return false;
        float limit=RebirthMapOverview.Limit(map);
        map.targetZoomScale=Mathf.Clamp(map.targetZoomScale,.7f,limit);
        map.zoomScale=Mathf.Clamp(map.zoomScale,.7f,limit);
        float z=FitCenter(map.mapMiddlePosPixel.y,minimum.z,maximum.z,map.ActiveCanvasHeight,map.cTexMiddle.y,map.zoomScale);
        if(z==map.mapMiddlePosPixel.y)return false;
        map.mapMiddlePosPixel=new Vector2(map.mapMiddlePosPixel.x,z);return true;
    }
    internal static float FitCenter(float center,float min,float max,int height,int middle,float zoom)
    {
        float meters=RebirthMapOverview.MetersPerUnit*zoom;
        return Mathf.Clamp(center,min+(height-middle)*meters,max-middle*meters);
    }
    internal void Render(XUiC_RebirthMapArea map)
    {
        bool available=Acquire(map);        for(int i=0;i<masks.Length;i++)if(masks[i]==null)masks[i]=map.GetChildById(ids[i])?.ViewComponent;
        if(!available||!map.bMapInitialized||!map.isOpen)
        {foreach(var view in masks)if(view!=null)view.IsVisible=false;return;}
        float meters=RebirthMapOverview.MetersPerUnit*Mathf.Max(.7f,map.zoomScale);
        float left=map.cTexMiddle.x+(minimum.x-map.mapMiddlePosPixel.x)/meters;
        float right=map.cTexMiddle.x+(maximum.x-map.mapMiddlePosPixel.x)/meters;
        float top=map.cTexMiddle.y-(maximum.z-map.mapMiddlePosPixel.y)/meters;
        float bottom=map.cTexMiddle.y-(minimum.z-map.mapMiddlePosPixel.y)/meters;
        Project(geometry,map.ActiveCanvasWidth,map.ActiveCanvasHeight,left,right,top,bottom);
        for(int i=0;i<masks.Length;i++)Apply(masks[i],geometry[i]);
    }
    // Integers represent UI raster edges. Border strokes stay inside the safe
    // rectangle; black masks partition its complement without overlaps/gaps.
    internal static void Project(RectInt[] output,int width,int height,float left,float right,float top,float bottom)
    {
        int l=Mathf.Clamp(Mathf.RoundToInt(left),0,width),r=Mathf.Clamp(Mathf.RoundToInt(right),0,width);
        int t=Mathf.Clamp(Mathf.RoundToInt(top),0,height),b=Mathf.Clamp(Mathf.RoundToInt(bottom),0,height);
        output[0]=new RectInt(0,0,l,height);output[1]=new RectInt(r,0,width-r,height);
        output[2]=new RectInt(l,0,r-l,t);output[3]=new RectInt(l,b,r-l,height-b);
        output[4]=new RectInt(l,t,left>=0&&left<width?Mathf.Min(2,r-l):0,b-t);
        output[5]=new RectInt(Mathf.Max(l,r-2),t,right>0&&right<=width?Mathf.Min(2,r-l):0,b-t);
        output[6]=new RectInt(l,t,r-l,top>=0&&top<height?Mathf.Min(2,b-t):0);
        output[7]=new RectInt(l,Mathf.Max(t,b-2),r-l,bottom>0&&bottom<=height?Mathf.Min(2,b-t):0);
    }
    private static void Apply(XUiView view,RectInt rect)
    {
        if(view==null)return;
        bool changed=false;
        var texture=view as XUiV_Texture;
        if(texture!=null&&texture.Texture!=Texture2D.whiteTexture){texture.AutoUnload=false;texture.Texture=Texture2D.whiteTexture;changed=true;}
        bool visible=rect.width>0&&rect.height>0;
        if(view.IsVisible!=visible)view.IsVisible=visible;if(!visible){if(changed)view.updateData();return;}
        var position=new Vector2i(rect.x,-rect.y);var size=new Vector2i(rect.width,rect.height);
        if(view.Position.x!=position.x||view.Position.y!=position.y){view.Position=position;changed=true;}
        if(view.Size.x!=size.x||view.Size.y!=size.y){view.Size=size;changed=true;}
        // Apply only native view data before render, without input/controller updates.
        if(changed)view.updateData();
    }
}


[HarmonyPatch(typeof(XUiC_MapArea),"onMapDragged")]
internal static class RebirthMapDragGesture
{
    private static bool Prefix(XUiC_MapArea __instance,EDragType __1)
    {
        var map=__instance as XUiC_RebirthMapArea;
        if(map==null)return true;
        if(map.ZoomSlider?.BlocksMapInput==true)return false;
        map.Interaction.Drag(__1!=EDragType.DragEnd&&Input.GetMouseButton(0));
        return true;
    }
}

[HarmonyPatch(typeof(XUiC_MapArea),"positionMap")]
internal static class RebirthMapPlayableView
{
    private static void Prefix(XUiC_MapArea __instance)
    {if(__instance is XUiC_RebirthMapArea map)map.FitPlayableView();}
}

