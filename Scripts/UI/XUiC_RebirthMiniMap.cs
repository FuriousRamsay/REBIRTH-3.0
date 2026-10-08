using UnityEngine;
using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthMiniMap : XUiC_MapArea
{
    public const float DefaultOpacity = 1f;
    public static float TerrainOpacity = 1f - Mathf.Clamp(SdPlayerPrefs.GetInt("RebirthMinimapTransparency",0),0,100)/100f;
    private const float DefaultZoom = 0.70f;
    private const float MaximumZoom = 2.0f;
    private float userZoom=DefaultZoom;
    private string zoomPreferenceKey;

    private void LoadSavedZoom()
    {
        RebirthHudTrackingPreferenceContext context;
        string error;
        zoomPreferenceKey = RebirthHudTrackingPreferenceStore.TryResolveCurrentContext(out context, out error)
            ? "RebirthMinimapZoom." + context.WorldKey + "." + context.PlayerStorageKey
            : null;
        // Store millionths to preserve small zoom steps using the integer preference API.
        int savedZoom = zoomPreferenceKey == null ? Mathf.RoundToInt(DefaultZoom * 1000000f)
            : SdPlayerPrefs.GetInt(zoomPreferenceKey, Mathf.RoundToInt(DefaultZoom * 1000000f));
        userZoom = Mathf.Clamp(savedZoom / 1000000f, 0.10f, MaximumZoom);
    }

    private void SaveZoom()
    {
        if (zoomPreferenceKey == null) return;
        SdPlayerPrefs.SetInt(zoomPreferenceKey, Mathf.RoundToInt(userZoom * 1000000f));
        SdPlayerPrefs.Save();
    }
    public static bool MinimapVisible { get; private set; } = true;
    public static bool IsDisplayed => ActiveInstance?.ViewComponent?.IsVisible == true && MinimapVisible;
    private static string visibilityPreferenceKey;
    private EntityPlayerLocal visibilityPlayer;

    public static void RefreshMinimapVisibility()
    {
        RebirthHudTrackingPreferenceContext context;
        string error;
        visibilityPreferenceKey = RebirthHudTrackingPreferenceStore.TryResolveCurrentContext(out context, out error)
            ? "RebirthMinimapVisible." + context.WorldKey + "." + context.PlayerStorageKey
            : null;
        // The old global setting must not hide the minimap in unrelated saves.
        MinimapVisible = visibilityPreferenceKey == null || SdPlayerPrefs.GetInt(visibilityPreferenceKey, 1) != 0;
    }

    public static void SetMinimapVisible(bool value)
    {
        RefreshMinimapVisibility();
        if (visibilityPreferenceKey == null)
        {
            Log.Warning("[REBIRTH Minimap] Cannot save visibility: no selected save/player context.");
            return;
        }
        MinimapVisible = value;
        SdPlayerPrefs.SetInt(visibilityPreferenceKey, value ? 1 : 0);
        SdPlayerPrefs.Save();
    }
    private UIPanel opacityPanel;
    private Material opacityMaterial;

    private Material mapSourceMaterial;
    private RenderTexture mapSurface;
    
    public static XUiC_RebirthMiniMap ActiveInstance;
    public static bool DiagnosticPending;
    public static void ReportDiagnostics(string phase)
    {
        var owner=ActiveInstance;
        string prefix="[REBIRTH Minimap Diagnostic PC226] ";
        System.Action<string> report=message=>{SdtdConsole.Instance.Output(prefix+message);Log.Out(prefix+message);};
        if(owner==null){report("No active minimap controller.");return;}
        report("phase="+phase+" ready="+owner.ready+" visible="+owner.ViewComponent.IsVisible+" subscribed="+owner.subscribed+" requestedOpacity="+TerrainOpacity);
        var widget=owner.xuiTexture?.UiTransform?.GetComponent<UITexture>();
        if(widget==null){report("No map UITexture.");return;}
        var mat=widget.material;
        report("widgetAlpha="+widget.alpha+" color="+widget.color+" texture="+(widget.mainTexture==null?"null":widget.mainTexture.name)
            +" material="+(mat==null?"null":mat.name)+" shader="+(mat?.shader==null?"null":mat.shader.name));
        if(mat==null||mat.shader==null)return;
        report("shaderSupported="+mat.shader.isSupported+" renderQueue="+mat.renderQueue+" passes="+mat.passCount+" keywords="+string.Join(",",mat.shaderKeywords));
        for(int i=0;i<mat.shader.GetPropertyCount();i++)
        {
            string name=mat.shader.GetPropertyName(i);
            var type=mat.shader.GetPropertyType(i);
            string value="";
            if(type==UnityEngine.Rendering.ShaderPropertyType.Color)value=mat.GetColor(name).ToString();
            else if(type==UnityEngine.Rendering.ShaderPropertyType.Vector)value=mat.GetVector(name).ToString();
            else if(type==UnityEngine.Rendering.ShaderPropertyType.Texture){var tex=mat.GetTexture(name);value=tex==null?"null":tex.name+" "+tex.width+"x"+tex.height;}
            else value=mat.GetFloat(name).ToString(System.Globalization.CultureInfo.InvariantCulture);
            report(name+" ("+type+")="+value);
        }
    }
    private bool ready;
    private bool subscribed;
    private LocalPlayerCamera subscribedCamera;
    private float refreshAt;
    private float markersAt;
    private Vector2 refreshedPosition;
    private const float DisplayScale=(288f*0.85f)/712f;
    public override void OnOpen()
    {
        ActiveInstance=this;
        visibilityPlayer=null;
        RefreshMinimapVisibility();
        // Preserve the window lifecycle (including its alpha) without opening map menus.
        foreach (var child in children) child.OnOpen();
        ViewComponent.OnOpen();
        RefreshBindings();
        ready=false;
    }
    public override void OnClose()
    {
        ReleaseCameraSubscription();
        ready=false;isOpen=false;
        foreach(var child in children)child.OnClose();
        ViewComponent.OnClose();
    }
    public override void Update(float dt)
    {
        var player=xui?.playerUI?.entityPlayer;
        if(player?.ChunkObserver?.mapDatabase==null)return;
        if (visibilityPlayer != player)
        {
            ReleaseCameraSubscription();
            ready = false;
            RefreshMinimapVisibility();
            LoadSavedZoom();
            visibilityPlayer = player;
        }
        var manager=xui.playerUI.windowManager;
        bool visible=manager.IsHUDEnabled()&&!manager.IsInputActive()&&!manager.IsModalWindowOpen()&&!player.IsDead();
        if(visible && RebirthNativeControls.Actions?.MinimapVisibility.WasPressed==true)
            SetMinimapVisible(!MinimapVisible);
        visible=visible && MinimapVisible;
        ViewComponent.IsVisible=visible;
        // This HUD specialization skips the full-screen MapArea.Update, but
        // must still tick the base view tree so textures and panels are rendered.
        if(!visible){ViewComponent.Update(dt);return;}

        if(!ready)
        {
            localPlayer=player;bFowMaskEnabled=true;showStaticData=false;isOpen=true;
            initMap();cTexMiddle=new Vector2i(356,240);zoomScale=targetZoomScale=userZoom;
            initExistingWaypoints(player.Waypoints);
            RefreshTerrain(player.GetPosition());
            xuiTexture.Size=new Vector2i(712,480);

            if(!subscribed)
            {
                subscribedCamera=xui.playerUI.GetComponentInParent<LocalPlayerCamera>();
                if(subscribedCamera!=null)
                {
                    subscribedCamera.PreRender+=RenderMiniMap;
                    subscribed=true;
                }
            }
            ready=subscribed;
        }
        var p=player.GetPosition();
        var target=new Vector2(p.x,p.z);
        // Follow every frame; terrain reads/uploads are separate from camera movement.
        mapMiddlePosPixel=target;
        if(Time.realtimeSinceStartup>=refreshAt)
        {
            refreshAt=Time.realtimeSinceStartup+0.5f;
            bool networkChanged=player.ChunkObserver.mapDatabase.IsNetworkDataAvail();
            if(networkChanged || (target-refreshedPosition).sqrMagnitude>=1f)
            {
                RefreshTerrain(p);
                if(networkChanged)player.ChunkObserver.mapDatabase.ResetNetworkDataAvail();
            }
        }
        var controls=RebirthNativeControls.Actions;
        if(controls!=null)
        {
            float previousZoom=userZoom;
            if(controls.MinimapZoomIn.WasPressed)userZoom=Mathf.Max(0.10f,userZoom/1.10f);
            else if(controls.MinimapZoomOut.WasPressed)userZoom=Mathf.Min(MaximumZoom,userZoom*1.10f);
            if(previousZoom!=userZoom)
            {
                SaveZoom();
                RefreshTerrain(p);
                { if (RebirthLogSettings.UiRouteLoggingEnabled) Log.Out("[REBIRTH Minimap Zoom] zoomScale="+userZoom.ToString("0.000000",System.Globalization.CultureInfo.InvariantCulture)); }
            }
            zoomScale=targetZoomScale=userZoom;
        }
        positionMap();
        if(Time.realtimeSinceStartup>=markersAt)
        {
            markersAt=Time.realtimeSinceStartup+0.05f;
            updateMapObjects();
            foreach(var entry in keyToNavSprite.Dict) ResizeMarkerLabel(entry.Value);
            foreach(var entry in keyToMapSprite.Dict) ResizeMarkerLabel(entry.Value);
        }
        ViewComponent.Update(dt);
        // Window layout resets scale when dirty; apply the HUD scale afterwards.
        ViewComponent.UiTransform.localScale=new Vector3(DisplayScale,DisplayScale,1);
        foreach(var child in children)child.Update(dt);
        var texture=xuiTexture.UiTransform.GetComponent<UITexture>();
        if(texture!=null && opacityMaterial==null && texture.material!=null)
        {
            var shader=Shader.Find("Unlit/Transparent Colored");
            if(shader!=null)
            {
                mapSourceMaterial=new Material(texture.material);
                opacityMaterial=new Material(shader);
                opacityMaterial.name="REBIRTH Minimap Composite";
                mapSurface=new RenderTexture(356,240,0,RenderTextureFormat.ARGB32);
                mapSurface.Create();
            }
        }
        if(mapSurface!=null && texture!=null)
        {
            if(texture.material!=opacityMaterial) texture.material=opacityMaterial;
            if(texture.mainTexture!=mapSurface) texture.mainTexture=mapSurface;
        }
        // A panel on this window scopes alpha to terrain, border, and native marker children.
        // Set after the XUi update, which may restore authored view opacity.
        if(opacityPanel==null)
        {
            var root=ViewComponent.UiTransform.gameObject;
            opacityPanel=root.GetComponent<UIPanel>();
            if(opacityPanel==null)opacityPanel=root.AddComponent<UIPanel>();
        }
        opacityPanel.alpha=TerrainOpacity;
        if(texture!=null&&texture.onPostFill==null){texture.onPostFill=RoundMap;texture.MarkAsChanged();}
        if(DiagnosticPending){DiagnosticPending=false;ReportDiagnostics("visible-frame");}
    }
    private static void ResizeMarkerLabel(GameObject marker)
    {
        // Keep native names legible after the map canvas is scaled down for the HUD.
        var label=marker.transform.Find("Name");
        if(label==null)return;
        label.localScale=new Vector3(2.4f,2.4f,1f);
    }
    private void RefreshTerrain(Vector3 position)
    {
        // Upload only the visible terrain region plus a movement margin, sized for the current zoom.
        // Recenter the cache together with its upload so scrolling stays continuous.
        mapMiddlePosPixel=new Vector2(position.x,position.z);
        mapMiddlePosChunks=new Vector2(Mathf.Floor(position.x/16f)*16f,Mathf.Floor(position.z/16f)*16f);
        mapScrollTextureOffset=Vector2.zero;
        mapScrollTextureChunksOffsetX=mapScrollTextureChunksOffsetZ=0;
        int cx=(int)mapMiddlePosChunks.x,cz=(int)mapMiddlePosChunks.y;
        int halfWidth=Mathf.Max(192,Mathf.CeilToInt((168f*userZoom+32f)/16f)*16);
        int halfHeight=Mathf.Max(144,Mathf.CeilToInt((168f*userZoom*480f/712f+32f)/16f)*16);
        updateMapSection(cx-halfWidth,cz-halfHeight,cx+halfWidth,cz+halfHeight,
            1024-halfWidth,1024-halfHeight,1024+halfWidth,1024+halfHeight);
        mapTexture.Apply(false);
        refreshedPosition=mapMiddlePosPixel;
        SendMapPositionToServer();
    }
    private void RoundMap(UIWidget widget,int offset,System.Collections.Generic.List<Vector3> verts,System.Collections.Generic.List<Vector2> uvs,System.Collections.Generic.List<Color> colors)
    {
        if(verts.Count-offset!=4)return;
        float left=verts[offset].x,right=left,bottom=verts[offset].y,top=bottom;
        float u0=uvs[offset].x,u1=u0,v0=uvs[offset].y,v1=v0;
        Color color=colors[offset];float z=verts[offset].z;
        for(int i=offset;i<offset+4;i++){left=Mathf.Min(left,verts[i].x);right=Mathf.Max(right,verts[i].x);bottom=Mathf.Min(bottom,verts[i].y);top=Mathf.Max(top,verts[i].y);u0=Mathf.Min(u0,uvs[i].x);u1=Mathf.Max(u1,uvs[i].x);v0=Mathf.Min(v0,uvs[i].y);v1=Mathf.Max(v1,uvs[i].y);}
        verts.RemoveRange(offset,4);uvs.RemoveRange(offset,4);colors.RemoveRange(offset,4);
        const float radius=24f;
        for(int row=0;row<48;row++)
        {
            float y0=Mathf.Lerp(bottom,top,row/48f),y1=Mathf.Lerp(bottom,top,(row+1)/48f);
            float d0=Mathf.Max(0,radius-Mathf.Min(y0-bottom,top-y0));
            float d1=Mathf.Max(0,radius-Mathf.Min(y1-bottom,top-y1));
            float inset0=radius-Mathf.Sqrt(radius*radius-d0*d0),inset1=radius-Mathf.Sqrt(radius*radius-d1*d1);
            AddRoundedVertex(verts,uvs,colors,new Vector3(left+inset0,y0,z),left,right,bottom,top,u0,u1,v0,v1,color);
            AddRoundedVertex(verts,uvs,colors,new Vector3(left+inset1,y1,z),left,right,bottom,top,u0,u1,v0,v1,color);
            AddRoundedVertex(verts,uvs,colors,new Vector3(right-inset1,y1,z),left,right,bottom,top,u0,u1,v0,v1,color);
            AddRoundedVertex(verts,uvs,colors,new Vector3(right-inset0,y0,z),left,right,bottom,top,u0,u1,v0,v1,color);
        }
    }

    private static void AddRoundedVertex(System.Collections.Generic.List<Vector3> verts,System.Collections.Generic.List<Vector2> uvs,System.Collections.Generic.List<Color> colors,Vector3 point,float left,float right,float bottom,float top,float u0,float u1,float v0,float v1,Color color)
    {
        verts.Add(point);
        uvs.Add(new Vector2(Mathf.Lerp(u0,u1,(point.x-left)/(right-left)),Mathf.Lerp(v0,v1,(point.y-bottom)/(top-bottom))));
        colors.Add(color);
    }

    private void ReleaseCameraSubscription()
    {
        if(subscribedCamera!=null) subscribedCamera.PreRender-=RenderMiniMap;
        subscribedCamera=null;
        subscribed=false;
    }
    private void RenderMiniMap(LocalPlayerCamera camera)
    {
        if(!ready||!ViewComponent.IsVisible)return;
        float sy=mapScale*480f/712f,offset=(sy-mapScale)/2;
        Shader.SetGlobalVector("_MainMapPosAndScale",new Vector4(mapPos.x,mapPos.y-offset,mapScale,sy));
        Shader.SetGlobalVector("_MainMapBGPosAndScale",new Vector4(mapBGPos.x,mapBGPos.y-offset,mapScale,sy));
        if(mapSurface!=null)
        {
            var previousTarget=RenderTexture.active;
            try
            {
                RenderTexture.active=mapSurface;
                // GameUI/MainMap uses ColorMask RGB. Seed alpha explicitly: otherwise
                // the resolved map has zero alpha and disappears in the transparent UI pass.
                GL.Clear(false,true,new Color(0,0,0,1));
                Graphics.Blit(mapTexture,mapSurface,mapSourceMaterial);
            }
            finally {RenderTexture.active=previousTarget;}
        }
    }
    public override void Cleanup(){if(ActiveInstance==this)ActiveInstance=null;OnClose();if(opacityMaterial!=null)UnityEngine.Object.Destroy(opacityMaterial);opacityMaterial=null;if(mapSourceMaterial!=null)UnityEngine.Object.Destroy(mapSourceMaterial);if(mapSurface!=null){mapSurface.Release();UnityEngine.Object.Destroy(mapSurface);}mapSourceMaterial=null;mapSurface=null;base.Cleanup();}
}
