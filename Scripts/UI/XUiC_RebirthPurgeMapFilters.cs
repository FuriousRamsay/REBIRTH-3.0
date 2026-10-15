using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthPurgeMapFilters : XUiController
{
    private readonly Dictionary<XUiView,Vector2i> sizes=new Dictionary<XUiView,Vector2i>();
    private XUiView mapView,crosshair;
    private XUiV_Panel clipping;
    private Vector2i mapPosition,crosshairPosition;
    private Vector2 clipSize,clipCenter;
    private bool applied;
    private float next;
    public override void Init()
    {
        base.Init();
        for(int i=0;i<7;i++)
        {
            int filter=i;var button=GetChildById("purgeFilter"+i);
            if(button!=null)button.OnPress+=(sender,mouse)=>{if(mouse==0||mouse==-1){RebirthPoiMapPresentation.Toggle(filter);RefreshBindings();}};
        }
    }
    public override void OnOpen(){base.OnOpen();next=0;Layout();RefreshBindings();}
    public override void OnClose(){Restore();base.OnClose();}
    public override void Update(float dt)
    {
        base.Update(dt);
        if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.5f;Layout();
        // Match the native map marker's fully opaque sprite presentation. Legend
        // icons are foreground symbols, not translucent window backgrounds.
        for(int i=0;i<7;i++)
        {
            var icon=GetChildById("purgeFilter"+i)?.GetChildById("icon")?.ViewComponent as XUiV_Sprite;
            if(icon==null)continue;
            var color=RebirthPoiMapPresentation.Dimmed(i)?new Color32(100,100,100,255):new Color32(255,255,255,255);
            icon.GlobalOpacityModifier=0;icon.ForegroundLayer=true;icon.Color=color;icon.SetColorImmediately(color);
        }
    }
    private void Layout()
    {
        bool show=RebirthPurgeReleasePolicy.Enabled&&RebirthSandboxOptionManager.Current.IsPurge;
        if(ViewComponent!=null)ViewComponent.IsVisible=show;
        if(!show){Restore();return;}
        var root=GetParentByType<XUiC_MapArea>();
        var map=root?.GetChildById("mapView");
        if(map?.ViewComponent==null)return;
        mapView=map.ViewComponent;
        if(applied&&ViewComponent.Size.x==mapView.Size.x)return;
        if(!applied)mapPosition=mapView.Position;
        // Purge is independent of character progression: fit both native and REBIRTH maps.
        int width=mapView.Size.x;
        // The legend is a window child; mapView can be nested inside content.
        // Translate its original position into window coordinates before reserving the strip.
        var legendPosition=mapPosition;
        for(var parent=map.Parent;parent!=null&&parent!=root;parent=parent.Parent)
            if(parent.ViewComponent!=null)
                legendPosition=new Vector2i(legendPosition.x+parent.ViewComponent.Position.x,legendPosition.y+parent.ViewComponent.Position.y);
        ViewComponent.Position=legendPosition;
        ViewComponent.Size=new Vector2i(width,42);
        var background=GetChildById("filterBackground")?.ViewComponent;
        if(background!=null)background.Size=new Vector2i(width,42);
        for(int i=0;i<7;i++)
        {
            var button=GetChildById("purgeFilter"+i);
            if(button?.ViewComponent==null)continue;
            int left=width*i/7,cellWidth=width*(i+1)/7-left;
            button.ViewComponent.Position=new Vector2i(left,0);
            button.ViewComponent.Size=new Vector2i(cellWidth,42);
            var title=button.GetChildById("title")?.ViewComponent;
            if(title!=null)title.Size=new Vector2i(Math.Max(1,cellWidth-48),26);
        }
        if(applied)
        {
            // Preserve unfiltered heights while the map follows resized chrome.
            foreach(var view in new List<XUiView>(sizes.Keys))sizes[view]=new Vector2i(width,sizes[view].y);
            clipSize.x=width;clipCenter.x=width/2f;
            crosshairPosition.x=width/2;
            return;
        }
        sizes.Clear();sizes[mapView]=mapView.Size;
        foreach(string name in new[]{"mapViewTexture","clippingPanel","backgroundMain"})
        {
            var view=map.GetChildById(name)?.ViewComponent;if(view!=null)sizes[view]=view.Size;
        }
        crosshair=map.GetChildById("crosshair")?.ViewComponent;
        if(crosshair!=null)crosshairPosition=crosshair.Position;
        clipping=map.GetChildById("clippingPanel")?.ViewComponent as XUiV_Panel;
        if(clipping!=null){clipSize=clipping.ClippingSize;clipCenter=clipping.ClippingCenter;}
        mapView.Position=new Vector2i(mapPosition.x,mapPosition.y-42);
        foreach(var pair in sizes)pair.Key.Size=new Vector2i(pair.Value.x,Math.Max(1,pair.Value.y-42));
        if(crosshair!=null)crosshair.Position=new Vector2i(crosshairPosition.x,crosshairPosition.y+21);
        if(clipping!=null){clipping.ClippingSize=new Vector2(clipSize.x,Math.Max(1,clipSize.y-42));clipping.ClippingCenter=new Vector2(clipCenter.x,clipCenter.y+21);}
        applied=true;
    }
    private void Restore()
    {
        if(!applied)return;
        foreach(var pair in sizes)pair.Key.Size=pair.Value;
        mapView.Position=mapPosition;if(crosshair!=null)crosshair.Position=crosshairPosition;
        if(clipping!=null){clipping.ClippingSize=clipSize;clipping.ClippingCenter=clipCenter;}
        sizes.Clear();applied=false;
    }
    public override bool GetBindingValueInternal(ref string value,string bindingName)
    {
        const string prefix="purgefiltercolor";
        int filter;
        if(bindingName.StartsWith(prefix,StringComparison.Ordinal)&&int.TryParse(bindingName.Substring(prefix.Length),out filter))
        {value=RebirthPoiMapPresentation.Dimmed(filter)?"100,100,100,255":"255,255,255,255";return true;}
        return base.GetBindingValueInternal(ref value,bindingName);
    }
}
