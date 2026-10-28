using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthPurgePoiPopup:XUiController
{
    private RebirthPoiMapRecord shown;
    private bool awaitingPlacement;
    private int placementFrame;
    private Vector2i pendingPosition;
    private XUiC_MapArea map;
    private XUiV_Texture picture;
    private Texture2D ownedPicture;
    private string picturePath;
    private XUiV_Sprite background,border,tierIcon,statusBackground,statusIcon;
    private XUiV_Label title,tier,biome,coordinates,status,discoverer,contributors,discoveredTime,purgedTime;
    public override void Init()
    {
        base.Init();map=GetParentByType<XUiC_MapArea>();picture=GetChildById("poiPopupPicture")?.ViewComponent as XUiV_Texture;
        background=Sprite("poiPopupBackground");border=Sprite("poiPopupBorder");tierIcon=Sprite("poiPopupTierIcon");statusBackground=Sprite("poiPopupStatusBg");statusIcon=Sprite("poiPopupStatusIcon");
        title=Label("poiPopupTitle");tier=Label("poiPopupTier");biome=Label("poiPopupBiome");coordinates=Label("poiPopupCoordinates");status=Label("poiPopupStatus");
        discoverer=Label("poiPopupDiscoverer");contributors=Label("poiPopupContributors");discoveredTime=Label("poiPopupDiscoveredTime");purgedTime=Label("poiPopupPurgedTime");
        if(ViewComponent!=null)ViewComponent.IsVisible=false;
    }
    public override void OnClose(){Show(null);base.OnClose();}
    internal void Show(RebirthPoiMapRecord record)
    {
        if(ViewComponent==null)return;
        bool visible=record!=null&&
            (record.State==RebirthPoiClearanceState.Discovered||record.State==RebirthPoiClearanceState.Cleared)&&
            RebirthPurgeReleasePolicy.Enabled&&RebirthSandboxOptionManager.Current.IsPurge;
        if(!visible){ViewComponent.IsVisible=false;shown=null;awaitingPlacement=false;ClearPicture();return;}
        if(!ReferenceEquals(shown,record))
        {
            if(shown==null||shown.Identity.Key!=record.Identity.Key||shown.Epoch!=record.Epoch)
                BeginPlacement();
            shown=record;bool purged=record.State==RebirthPoiClearanceState.Cleared;
            Set(title,L(record.Identity.Prefab));Set(tier,string.Format(CultureInfo.InvariantCulture,L("xuiRebirthPurgeLocationTier"),record.Tier<0?"?":record.Tier.ToString(CultureInfo.InvariantCulture)));
            Set(biome,L("biome_"+record.Identity.Biome));
            Set(coordinates,string.Format(CultureInfo.InvariantCulture,L("xuiRebirthPurgePopupCoordinates"),Math.Abs(record.Identity.X),L(record.Identity.X<0?"xuiRebirthPurgeWest":"xuiRebirthPurgeEast"),Math.Abs(record.Identity.Z),L(record.Identity.Z<0?"xuiRebirthPurgeSouth":"xuiRebirthPurgeNorth")));
            if(tierIcon!=null)tierIcon.SpriteName="ui_game_symbol_Skull"+Math.Min(5,Math.Max(0,record.Tier));
            Set(status,L(purged?"xuiRebirthPurgeStatsCleared":"xuiRebirthPurgeStatsDiscovered"));
            Color color=purged?new Color32(133,217,151,255):new Color32(236,210,118,255);
            if(status!=null)status.Color=new Color32(255,255,255,255);if(statusIcon!=null){statusIcon.Color=color;statusIcon.SpriteName=purged?"ui_game_symbol_check":"ui_game_symbol_stealth";}
            var biomeIcon=Sprite("poiPopupBiomeIcon");
            if(biomeIcon!=null){biomeIcon.UIAtlas="RebirthTraderJobs";string region=record.Identity.Biome;biomeIcon.SpriteName="rb_biome_"+(region=="pine_forest"||region=="forest"?"forest":region=="burnt_forest"?"burnt":region=="snow"||region=="desert"||region=="wasteland"?region:"unknown");}
            if(statusBackground!=null)statusBackground.Color=purged?new Color32(33,65,48,255):new Color32(82,65,27,255);
            var discoveryNames=Names(record.Discoverer);
            var names=Names(record.Contributors);
            bool discoveryKnown=discoveryNames.Length>0||record.DiscoveredAt>0;
            Set(discoverer,string.Join("\n",discoveryNames.Select(n=>"[EBD276]"+n+"[-]")));
            Set(contributors,string.Join("\n",names.Length==0?new[]{L("xuiRebirthPurgeHistoryUnknown")}:names.Select(n=>"[EBD276]"+n+"[-]").ToArray()));
            Set(Label("poiPopupAreasValue"),record.AreasLeft<0?"—":record.AreasLeft.ToString(CultureInfo.InvariantCulture));
            Set(Label("poiPopupEntitiesValue"),record.EstimatedEntities<0?"—":(record.Approximate?"~":"")+record.EstimatedEntities.ToString(CultureInfo.InvariantCulture));
            Set(discoveredTime,TimeText(record.DiscoveredAt));Set(purgedTime,TimeText(record.PurgedAt));
            int discoveryHeight=discoveryKnown?Math.Max(30,30*discoveryNames.Length):0;
            int purgeHeight=purged?Math.Max(30,30*names.Length):0;
            int footerTop=purged?184:242;
            foreach(string metric in new[]{"Areas","Entities"})
                foreach(string part in new[]{"Background","Icon","Caption","Value"})
                {var view=GetChildById("poiPopup"+metric+part)?.ViewComponent;if(view!=null)view.IsVisible=!purged;}
            var divider=Sprite("poiPopupDivider");if(divider!=null)divider.Position=new Vector2i(14,-footerTop+10);
            int height=footerTop+4+discoveryHeight+purgeHeight;
            if(ViewComponent.Size.y!=height)BeginPlacement();
            ViewComponent.Size=new Vector2i(620,height);
            if(background!=null)background.Size=new Vector2i(620,height);
            if(border!=null)border.Size=new Vector2i(620,height);
            LayoutAttribution("Discoverer",discoverer,discoveryHeight,-footerTop,discoveryKnown);
            LayoutAttribution("Contributors",contributors,purgeHeight,-footerTop-discoveryHeight,purged);
            SetDateRow("Discovered",record.DiscoveredAt,-footerTop-15,discoveryKnown);
            SetDateRow("Purged",record.PurgedAt,-footerTop-15-discoveryHeight,purged);
            if(picture!=null)
            {
                var location=PathAbstractions.PrefabsSearchPaths.GetLocation(record.Identity.Prefab);
                string path=location.Type==PathAbstractions.EAbstractedLocationType.None?null:location.FullPathNoExtension+".jpg";
                LoadPicture(path);
            }
        }
        // The HUD scanner carries the freshest authenticated native observation.
        // Use it only for this exact POI and epoch, never another location's counts.
        var scan=RebirthPurgeScanner.Observe(xui?.playerUI?.entityPlayer);
        int areas=record.AreasLeft,entities=record.EstimatedEntities;bool approximate=record.Approximate;
        if(RebirthPoiMapSync.PresentationMatchesTransport&&record.State==RebirthPoiClearanceState.Discovered&&scan!=null&&scan.PoiKey==record.Identity.Key&&scan.Epoch==record.Epoch)
        {areas=Math.Max(0,scan.Rooms-scan.Cleared);entities=scan.Enemies;approximate=scan.Approximate;}
        Set(Label("poiPopupAreasValue"),areas<0?"—":areas.ToString(CultureInfo.InvariantCulture));
        Set(Label("poiPopupEntitiesValue"),entities<0?"—":entities.ToString(CultureInfo.InvariantCulture)+(approximate?"+":""));
        var transform=map?.ViewComponent?.UiTransform;var camera=xui?.playerUI?.camera;
        if(transform==null||camera==null){ViewComponent.IsVisible=false;awaitingPlacement=true;placementFrame=-1;return;}
        var ray=camera.ScreenPointToRay((Vector3)xui.playerUI.CursorController.GetScreenPosition());float distance;
        if(!new Plane(transform.forward,transform.position).Raycast(ray,out distance)){ViewComponent.IsVisible=false;awaitingPlacement=true;placementFrame=-1;return;}
        var point=transform.InverseTransformPoint(ray.GetPoint(distance));
        int width=Math.Max(620,map.ViewComponent.Size.x),heightLimit=Math.Max(ViewComponent.Size.y,map.ViewComponent.Size.y);
        int left=(int)point.x+24;if(left+620>width)left=(int)point.x-644;
        int top=(int)point.y+24;
        var position=new Vector2i(Math.Max(0,Math.Min(width-620,left)),Math.Max(-heightLimit+ViewComponent.Size.y,Math.Min(0,top)));
        ViewComponent.Position=position;
        // IsVisible activates the native GameObject immediately. Apply the
        // dirty position while hidden rather than assuming a frame delay has
        // updated an inactive view's transform.
        if(ViewComponent.UiTransform==null)return;
        ViewComponent.TryUpdatePosition();
        // XUi applies dirty geometry on its next view update. Keep a new card
        // hidden across that update so its first visible frame has final bounds.
        if(awaitingPlacement)
        {
            if(placementFrame<0||!pendingPosition.Equals(position))
            {pendingPosition=position;placementFrame=Time.frameCount;return;}
            if(Time.frameCount<=placementFrame)return;
            awaitingPlacement=false;
        }
        ViewComponent.IsVisible=true;
    }
    private void BeginPlacement()
    {
        ViewComponent.IsVisible=false;awaitingPlacement=true;placementFrame=-1;
    }
    private void ClearPicture()
    {
        picturePath=null;
        if(picture!=null){picture.AutoUnload=false;picture.Texture=null;}
        if(ownedPicture!=null){UnityEngine.Object.Destroy(ownedPicture);ownedPicture=null;}
    }
    private void LoadPicture(string path)
    {
        if(ownedPicture!=null&&string.Equals(picturePath,path,StringComparison.Ordinal))return;
        ClearPicture();if(picture==null||path==null||!SdFile.Exists(path))return;
        Texture2D texture=null;
        try
        {
            var info=new System.IO.FileInfo(path);if(info.Length>4*1024*1024)return;
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(texture,System.IO.File.ReadAllBytes(path),true))
                return;
            picture.Texture=texture;ownedPicture=texture;picturePath=path;
        }
        catch(Exception error){Log.Warning("[Purge popup] Prefab photo unavailable: "+error.Message);}
        finally
        {
            // Also release runtime textures when reading or decoding fails.
            if(texture!=null&&texture!=ownedPicture)UnityEngine.Object.Destroy(texture);
        }
    }
    public override void Cleanup(){ClearPicture();base.Cleanup();}
    private static string[] Names(string text)=> (text??"").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries)
        .Select(RebirthPurgeDiscoveryNotification.CleanDisplayName).Where(n=>n.Length>0).Distinct(StringComparer.Ordinal).ToArray();
    private void LayoutAttribution(string kind,XUiV_Label names,int height,int top,bool visible)
    {
        if(names!=null){names.Position=new Vector2i(164,top-height/2);names.Size=new Vector2i(272,height);names.IsVisible=visible;}
        var caption=Label("poiPopup"+kind+"Caption");
        if(caption!=null){caption.Position=new Vector2i(54,top-15);caption.IsVisible=visible;}
        var icon=Sprite(kind=="Discoverer"?"poiPopupDiscovererIcon":"poiPopupContributorsIcon");
        if(icon!=null){icon.Position=new Vector2i(20,top-3);icon.IsVisible=visible;}
    }
    private void SetDateRow(string kind,ulong time,int center,bool visible)
    {
        bool known=visible&&time>0;var value=GameUtils.WorldTimeToElements(time);
        var day=Label("poiPopup"+kind+"Day");if(day!=null){day.Text=known?string.Format(CultureInfo.InvariantCulture,L("xuiRebirthPurgePopupDay"),value.Item1):string.Empty;day.Position=new Vector2i(448,center);day.IsVisible=known;}
        var clock=Sprite(kind=="Discovered"?"poiPopupClockIcon":"poiPopupPurgedClockIcon");if(clock!=null){clock.Position=new Vector2i(526,center+10);clock.IsVisible=known;}
        var label=Label("poiPopup"+kind+"Time");if(label!=null){label.Position=new Vector2i(554,center);label.IsVisible=known;}
    }
    private static string TimeText(ulong time){if(time==0)return string.Empty;var value=GameUtils.WorldTimeToElements(time);return string.Format(CultureInfo.InvariantCulture,"{0:00}:{1:00}",value.Item2,value.Item3);}
    private XUiV_Label Label(string id)=>GetChildById(id)?.ViewComponent as XUiV_Label;
    private XUiV_Sprite Sprite(string id)=>GetChildById(id)?.ViewComponent as XUiV_Sprite;
    private static void Set(XUiV_Label label,string text){if(label!=null)label.Text=text;}
    private static string L(string key)=>RebirthUiProjectionTextCache.L(key,key);
}
