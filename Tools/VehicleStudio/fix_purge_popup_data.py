from pathlib import Path
import xml.etree.ElementTree as E
p=Path('Config/_Purge/map_popup.xml');t=E.parse(p);card=t.find(".//panel[@name='rebirthPurgePoiPopup']")
for n in card:
 if n.get('name')=='poiPopupPicture':n.set('autounload','false')
 if n.tag=='sprite' and n.get('pos')=='14,-232':n.set('name','poiPopupDivider')
E.indent(t,space='  ');t.write(p,encoding='unicode')
p=Path('Scripts/UI/XUiC_RebirthPurgePoiPopup.cs');s=p.read_text();s=s.replace('private XUiV_Texture picture;','private XUiV_Texture picture;\n    private Texture2D ownedPicture;')
s=s.replace('if(!visible){shown=null;if(picture!=null)picture.Path=string.Empty;return;}','if(!visible){shown=null;ClearPicture();return;}')
s=s.replace('if(status!=null)status.Color=color;', 'if(status!=null)status.Color=new Color32(255,255,255,255);')
s=s.replace('int height=246+discoveryHeight+purgeHeight;', '''int footerTop=purged?184:242;
            foreach(string metric in new[]{"Areas","Entities"})
                foreach(string part in new[]{"Background","Icon","Caption","Value"})
                {var view=GetChildById("poiPopup"+metric+part)?.ViewComponent;if(view!=null)view.IsVisible=!purged;}
            var divider=Sprite("poiPopupDivider");if(divider!=null)divider.Position=new Vector2i(14,-footerTop+10);
            int height=footerTop+4+discoveryHeight+purgeHeight;''')
s=s.replace('discoveryHeight,-242,discoveryKnown','discoveryHeight,-footerTop,discoveryKnown').replace('purgeHeight,-242-discoveryHeight,purged','purgeHeight,-footerTop-discoveryHeight,purged').replace('record.DiscoveredAt,-257,discoveryKnown','record.DiscoveredAt,-footerTop-15,discoveryKnown').replace('record.PurgedAt,-257-discoveryHeight,purged','record.PurgedAt,-footerTop-15-discoveryHeight,purged')
s=s.replace('picture.Path=path!=null&&SdFile.Exists(path)?"@"+new Uri(path).AbsoluteUri:string.Empty;', 'LoadPicture(path);')
s=s.replace('        var transform=map?.ViewComponent?.UiTransform;', '''        // The HUD scanner carries the freshest authenticated native observation.
        // Use it only for this exact POI and epoch, never another location's counts.
        var scan=RebirthPurgeScanner.Observe(xui?.playerUI?.entityPlayer);
        int areas=record.AreasLeft,entities=record.EstimatedEntities;bool approximate=record.Approximate;
        if(scan!=null&&scan.PoiKey==record.Identity.Key&&scan.Epoch==record.Epoch)
        {areas=Math.Max(0,scan.Rooms-scan.Cleared);entities=scan.Enemies;approximate=scan.Approximate;}
        Set(Label("poiPopupAreasValue"),areas<0?"—":areas.ToString(CultureInfo.InvariantCulture));
        Set(Label("poiPopupEntitiesValue"),entities<0?"—":entities.ToString(CultureInfo.InvariantCulture)+(approximate?"+":""));
        var transform=map?.ViewComponent?.UiTransform;''')
pos=s.index('    private static string[] Names(')
s=s[:pos]+'''    private void ClearPicture()
    {
        if(picture!=null){picture.AutoUnload=false;picture.Texture=null;}
        if(ownedPicture!=null){UnityEngine.Object.Destroy(ownedPicture);ownedPicture=null;}
    }
    private void LoadPicture(string path)
    {
        ClearPicture();if(picture==null||path==null||!SdFile.Exists(path))return;
        try
        {
            var info=new System.IO.FileInfo(path);if(info.Length>4*1024*1024)return;
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(texture,System.IO.File.ReadAllBytes(path),true))
            {UnityEngine.Object.Destroy(texture);return;}
            ownedPicture=texture;picture.Texture=texture;
        }
        catch(Exception error){Log.Warning("[Purge popup] Prefab photo unavailable: "+error.Message);}
    }
    public override void Cleanup(){ClearPicture();base.Cleanup();}
''' +s[pos:];p.write_text(s)
p=Path('Scripts/Purge/Evidence/RebirthPoiNativeEvidence.cs');s=p.read_text();needle='        if(c.PendingRepopulation!=null && c.Epoch!=null'
s=s.replace(needle,'''        // Earned map reveals do not establish a discoverer. Capture the first
        // authenticated on-foot native visit even when a marker already exists.
        if(c.Touched!=null && RebirthPurgeKillContributor.Identify(c.Touched,c.Manifest.World)!=null
            && c.Touched.IsSpawned()&&!c.Touched.IsDead()&&!c.Touched.IsSpectator
            && c.Touched.AttachedToEntity==null&&!c.Touched.IsFlyMode.Value)
            RebirthPurgePoiHistory.Discover(c.Manifest.Identity,RebirthPurgeDiscoveryNotification.DisplayName(c.Touched));
''' +needle);p.write_text(s)
