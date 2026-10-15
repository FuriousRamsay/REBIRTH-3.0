using System;
using Platform;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthPurgeStatisticsPanel:XUiController
{
    private const int PoiRows=16,BiomeRows=64;
    private static readonly Func<string,string> BiomeLabel=BiomeName,PoiLabel=L;
    private readonly XUiV_Label[] cards=new XUiV_Label[6];
    private readonly XUiController[] biomeRows=new XUiController[BiomeRows],poiRows=new XUiController[PoiRows];
    private readonly XUiV_Label[] biomeNames=new XUiV_Label[BiomeRows],biomeValues=new XUiV_Label[BiomeRows],poiNames=new XUiV_Label[PoiRows],poiValues=new XUiV_Label[PoiRows];
    private readonly XUiV_Sprite[] biomeFills=new XUiV_Sprite[BiomeRows];
    private readonly XUiV_Sprite[] poiTierIcons=new XUiV_Sprite[PoiRows];
    private readonly XUiV_Label[] poiTierNames=new XUiV_Label[PoiRows];
    private readonly XUiV_Label[] poiStates=new XUiV_Label[PoiRows];
    private readonly XUiV_Texture[] poiPictures=new XUiV_Texture[PoiRows];
    private readonly Texture2D[] ownedPictures=new Texture2D[PoiRows];
    private readonly string[] picturePaths=new string[PoiRows];
    private readonly Dictionary<string,string> previewUris=new Dictionary<string,string>(StringComparer.Ordinal);
    private int locationFilter;
    private XUiV_Label target,pageLabel,pending;
    private XUiController previous,next;
    private XUiC_RebirthCharacterOverviewList poiScroll;
    private object objectiveSource,mapSource;
    private long generation=long.MinValue,textRevision=long.MinValue,mapRevision=long.MinValue;
    private int page,renderedMode=-1,renderedTarget=-1;
    private bool wasVisible,dirty;
    private float poll;
    private RebirthPoiMapRecord[] records=Array.Empty<RebirthPoiMapRecord>();
    private readonly RebirthPurgeStatisticsPoiIndex poiIndex=new RebirthPurgeStatisticsPoiIndex();
    private long indexGeneration=long.MinValue;
    public override void Init()
    {
        base.Init();
        poiScroll=GetChildById("purgeStatsPoiScroll") as XUiC_RebirthCharacterOverviewList;
        if(poiScroll!=null)
        {
            poiScroll.BindOwner(()=>windowGroup?.isShowing==true&&ViewComponent?.IsVisible==true);
            poiScroll.DataRangeChanged+=()=>{dirty=true;};
        }
        for(int i=0;i<cards.Length;i++)cards[i]=Label("purgeStatsValue"+i);
        for(int i=0;i<BiomeRows;i++){biomeRows[i]=GetChildById("purgeStatsBiomeRow"+i);biomeNames[i]=Label("purgeStatsBiomeName"+i);biomeValues[i]=Label("purgeStatsBiomeValue"+i);biomeFills[i]=GetChildById("purgeStatsBiomeFill"+i)?.ViewComponent as XUiV_Sprite;}
        for(int i=0;i<PoiRows;i++){poiRows[i]=GetChildById("purgeStatsPoiRow"+i);poiNames[i]=Label("purgeStatsPoiName"+i);poiValues[i]=Label("purgeStatsPoiValue"+i);}
        for(int i=0;i<PoiRows;i++)
        {
            int row=i;poiTierIcons[i]=GetChildById("purgeStatsPoiTierIcon"+i)?.ViewComponent as XUiV_Sprite;
            poiTierNames[i]=Label("purgeStatsPoiTierName"+i);poiStates[i]=Label("purgeStatsPoiState"+i);poiPictures[i]=GetChildById("purgeStatsPoiPicture"+i)?.ViewComponent as XUiV_Texture;
            var waypoint=GetChildById("purgeStatsPoiWaypoint"+i);
            if(waypoint!=null)waypoint.OnPress+=(sender,button)=>{if(button==0||button==-1)AddWaypoint(row);};
        }
        for(int i=0;i<3;i++)
        {
            int filter=i;var control=GetChildById("purgeStatsFilter"+i);
            if(control!=null)control.OnPress+=(sender,button)=>{if(button==0||button==-1){locationFilter=filter;FilterRecords();poiScroll?.ResetPosition();dirty=true;}};
        }
        target=Label("purgeStatsTarget");pageLabel=Label("purgeStatsPage");pending=Label("purgeStatsPending");
        previous=GetChildById("purgeStatsPrevious");next=GetChildById("purgeStatsNext");
        if(previous!=null)previous.OnPress+=(sender,button)=>{if((button==0||button==-1)&&page>0){page--;dirty=true;}};
        if(next!=null)next.OnPress+=(sender,button)=>{if((button==0||button==-1)&&(long)(page+1)*PoiRows<records.Length){page++;dirty=true;}};
    }
    public override void OnClose(){wasVisible=false;poiIndex.Reset();records=Array.Empty<RebirthPoiMapRecord>();previewUris.Clear();for(int i=0;i<PoiRows;i++)ClearPicture(i);mapSource=objectiveSource=null;base.OnClose();}
    public override void Update(float dt)
    {
        base.Update(dt);
        bool visible=ViewComponent!=null&&ViewComponent.IsVisible&&ViewComponent.UiTransform!=null&&ViewComponent.UiTransform.gameObject.activeInHierarchy;
        if(!visible){wasVisible=false;return;}
        // Keep the reusable list's extent authoritative even when this panel's
        // expensive text/card rendering is unchanged or throttled.
        poiScroll?.SetItemCount(poiIndex.Known?records.Length:0,string.Empty);
        if(!wasVisible){wasVisible=true;dirty=true;poll=0;var player=xui?.playerUI?.entityPlayer as EntityPlayerLocal;if(player!=null)RebirthStatisticsService.RequestSnapshot(player,true);}
        poiIndex.Pulse(RebirthPoiMapSync.LocalMap,RebirthPoiMapSync.LocalWorld,RebirthPoiMapSync.LocalSession,RebirthPoiMapSync.LocalRevision,RebirthUiProjectionTextCache.Revision,BiomeLabel,PoiLabel);
        if(indexGeneration!=poiIndex.Generation)
        {indexGeneration=poiIndex.Generation;FilterRecords();page=Math.Min(page,Math.Max(0,(records.Length-1)/PoiRows));dirty=true;}
        if(!dirty&&Time.realtimeSinceStartup<poll)return;poll=Time.realtimeSinceStartup+1;
        var world=GameManager.Instance?.World;
        object source=null;RebirthPurgeObjectiveFrame frame=null;
        RebirthPoiWorldStore store;
        if(world?.IsRemote()==true){frame=RebirthPoiMapSync.LocalObjectives;if(frame?.Known==true&&frame.World==RebirthPoiMapSync.LocalWorld&&frame.Session==RebirthPoiMapSync.LocalSession)source=frame;}
        else
        {
            var progress=RebirthPurgeObjectiveProgress.Instance;
            if(RebirthPoiWorldLifecycle.Instance.TryGetStore(out store)&&store.Published!=null&&progress.Published!=null&&progress.WorldId==store.Published.WorldId&&progress.Revision==store.Published.Revision)source=progress.Published;
        }
        var map=RebirthPoiMapSync.LocalMap;long currentMap=RebirthPoiMapSync.LocalRevision,words=RebirthUiProjectionTextCache.Revision,currentGeneration=RebirthStatisticsClientState.Generation;
        int mode=frame?.ProgressionMode??RebirthPurgeObjectivePolicy.ProgressionMode;
        int selectedTarget=frame?.TargetPercentage??RebirthPurgeObjectivePolicy.TargetPercentage;
        if(!dirty&&renderedMode==mode&&renderedTarget==selectedTarget&&ReferenceEquals(source,objectiveSource)&&ReferenceEquals(map,mapSource)&&mapRevision==currentMap&&generation==currentGeneration&&textRevision==words)return;
        objectiveSource=source;mapSource=map;mapRevision=currentMap;generation=currentGeneration;textRevision=words;dirty=false;
        RebirthPurgeObjectiveFrame.Biome[] biomes=null;int percent=75;
        if(source!=null)
        {
            if(frame!=null){biomes=frame.Biomes.Values.ToArray();percent=frame.TargetPercentage;}
            else{var progress=RebirthPurgeObjectiveProgress.Instance;percent=progress.TargetPercentage;biomes=progress.Published.Values.Select(b=>new RebirthPurgeObjectiveFrame.Biome(b.Biome,b.Eligible,b.Discovered,b.Cleared,new Dictionary<int,int>(b.EligibleByTier),new Dictionary<int,int>(b.ClearedByTier))).ToArray();}
        }
        renderedMode=mode;renderedTarget=percent;Render(biomes,percent,poiIndex.Known,mode);
    }
    private void Render(RebirthPurgeObjectiveFrame.Biome[] biomes,int percent,bool mapKnown,int mode)
    {
        poiScroll?.SetItemCount(mapKnown?records.Length:0,string.Empty);
        string unknown=L("xuiRebirthCharacterDataPending");var snapshot=RebirthStatisticsClientState.Get();
        int eligible=biomes?.Sum(x=>x.Eligible)??0,discovered=biomes?.Sum(x=>x.Discovered)??0,cleared=biomes?.Sum(x=>x.Cleared)??0;
        Set(cards[0],biomes==null?unknown:discovered.ToString("N0",CultureInfo.InvariantCulture));Set(cards[1],biomes==null?unknown:cleared.ToString("N0",CultureInfo.InvariantCulture));
        int eligibleUnits=biomes?.Sum(x=>RebirthPurgeProgression.Units(x.Eligible,x.EligibleByTier,mode))??0,clearedUnits=biomes?.Sum(x=>RebirthPurgeProgression.Units(x.Cleared,x.ClearedByTier,mode))??0;
        Set(cards[2],biomes==null?unknown:eligible==0?L("xuiRebirthPurgeStatsNotApplicable"):((double)clearedUnits/eligibleUnits).ToString("P1",CultureInfo.InvariantCulture));
        Set(cards[3],snapshot?.PurgeKillsKnown==true?snapshot.PurgeKills.ToString("N0",CultureInfo.InvariantCulture):unknown);
        Set(cards[4],snapshot?.PurgeSuppliesKnown==true?snapshot.PurgeDropsEarned.ToString("N0",CultureInfo.InvariantCulture):unknown);
        Set(cards[5],snapshot?.PurgeSuppliesKnown==true?snapshot.PurgeDropsLaunched.ToString("N0",CultureInfo.InvariantCulture):unknown);
        Set(target,biomes==null?unknown:string.Format(CultureInfo.InvariantCulture,L(mode==1?"xuiRebirthPurgeStatsTargetTier":"xuiRebirthPurgeStatsTarget"),percent,eligible));
        var ordered=biomes?.OrderBy(x=>BiomeName(x.Name),StringComparer.CurrentCultureIgnoreCase).ToArray();
        for(int i=0;i<BiomeRows;i++)
        {
            bool present=ordered!=null&&i<ordered.Length;if(biomeRows[i]?.ViewComponent!=null)biomeRows[i].ViewComponent.IsVisible=present;if(!present)continue;
            var biome=ordered[i];int done=RebirthPurgeProgression.Units(biome.Cleared,biome.ClearedByTier,mode);int required=RebirthPurgeProgression.Required(RebirthPurgeProgression.Units(biome.Eligible,biome.EligibleByTier,mode),percent*75);
            Set(biomeNames[i],BiomeName(biome.Name));Set(biomeValues[i],string.Format(CultureInfo.InvariantCulture,L(mode==1?"xuiRebirthPurgeStatsBiomeTier":"xuiRebirthPurgeStatsBiome"),RebirthPurgeProgression.Text(done,mode),RebirthPurgeProgression.Text(required,mode),biome.Discovered,biome.Eligible));
            if(biomeNames[i]!=null)biomeNames[i].Color=BiomeColor(biome.Name);
            if(biomeFills[i]!=null){biomeFills[i].Fill=required>0?Mathf.Clamp01((float)done/required):0;biomeFills[i].Color=BiomeColor(biome.Name);}
        }
        for(int i=0;i<PoiRows;i++)
        {
            int index=(poiScroll?.FirstDataIndex??0)+i;bool present=mapKnown&&index<records.Length;if(poiRows[i]?.ViewComponent!=null)poiRows[i].ViewComponent.IsVisible=present;if(!present){ClearPicture(i);continue;}
            var record=records[index];Set(poiNames[i],L(record.Identity.Prefab));
            if(poiNames[i]!=null)poiNames[i].Color=new Color32(112,214,205,255);
            LoadPicture(i,PreviewUri(record.Identity.Prefab));
            if(poiTierIcons[i]!=null){poiTierIcons[i].SpriteName="ui_game_symbol_Skull"+Math.Min(5,Math.Max(0,record.Tier));poiTierIcons[i].Color=Color.white;poiTierIcons[i].GlobalOpacityModifier=0;}
            Set(poiTierNames[i],string.Format(CultureInfo.InvariantCulture,L("xuiRebirthPurgeLocationTier"),record.Tier>=0?record.Tier.ToString(CultureInfo.InvariantCulture):"?"));
            string state=L(record.State==RebirthPoiClearanceState.Cleared?"xuiRebirthPurgeStatsCleared":record.State==RebirthPoiClearanceState.ResetPending?"xuiRebirthPurgeStatsResetPending":"xuiRebirthPurgeStatsDiscovered");
            Set(poiStates[i],state);
            if(poiStates[i]!=null)poiStates[i].Color=record.State==RebirthPoiClearanceState.Cleared?new Color32(133,217,151,255):record.State==RebirthPoiClearanceState.ResetPending?new Color32(239,136,152,255):new Color32(236,210,118,255);
            var stateBackground=GetChildById("purgeStatsPoiStateBg"+i)?.ViewComponent as XUiV_Sprite;
            if(stateBackground!=null)stateBackground.Color=record.State==RebirthPoiClearanceState.Cleared?new Color32(33,89,60,255):record.State==RebirthPoiClearanceState.ResetPending?new Color32(105,43,55,255):new Color32(107,82,27,255);
            var stateIcon=GetChildById("purgeStatsPoiStateIcon"+i)?.ViewComponent as XUiV_Sprite;
            if(stateIcon!=null){stateIcon.SpriteName=record.State==RebirthPoiClearanceState.Cleared?"ui_game_symbol_check":"ui_game_symbol_stealth";stateIcon.Color=poiStates[i]?.Color??Color.white;}
            Set(poiValues[i],string.Format(CultureInfo.InvariantCulture,L("xuiRebirthPurgeLocationDetails"),BiomeName(record.Identity.Biome),record.Identity.X,record.Identity.Z));
        }
        RebirthStatisticsScrollLayout.Refresh(GetChildById("purgeStatsBiomeScroll"),GetChildById("purgeStatsBiomeScrollContent"),480,72*(ordered?.Length??0));
        Set(pending,!mapKnown?unknown:records.Length==0?L("xuiRebirthPurgeStatsNoPois"):string.Empty);
        Set(pageLabel,string.Format(CultureInfo.InvariantCulture,L("xuiRebirthHordeHistoryPage"),records.Length==0?0:page+1,records.Length==0?0:(records.Length-1)/PoiRows+1));
        if(previous?.ViewComponent!=null)previous.ViewComponent.Enabled=page>0;if(next?.ViewComponent!=null)next.ViewComponent.Enabled=(long)(page+1)*PoiRows<records.Length;
        for(int i=0;i<3;i++){var fill=GetChildById("purgeStatsFilterBg"+i)?.ViewComponent as XUiV_Sprite;if(fill!=null){fill.GlobalOpacityModifier=0;fill.Color=i==locationFilter?new Color32(126,103,47,255):i==2?new Color32(33,65,48,255):new Color32(43,48,56,255);}}
    }
    private void FilterRecords()
    {records=(poiIndex.Published??Array.Empty<RebirthPoiMapRecord>()).Where(r=>locationFilter==0||locationFilter==1&&r.State==RebirthPoiClearanceState.Discovered||locationFilter==2&&r.State==RebirthPoiClearanceState.Cleared).ToArray();}
    private string PreviewUri(string prefab)
    {
        string uri;if(previewUris.TryGetValue(prefab,out uri))return uri;
        var location=PathAbstractions.PrefabsSearchPaths.GetLocation(prefab);
        string path=location.Type==PathAbstractions.EAbstractedLocationType.None?null:location.FullPathNoExtension+".jpg";
        uri=path!=null&&SdFile.Exists(path)?path:string.Empty;previewUris[prefab]=uri;return uri;
    }
    private void ClearPicture(int row)
    {
        picturePaths[row]=null;
        if(poiPictures[row]!=null){poiPictures[row].AutoUnload=false;poiPictures[row].Texture=null;}
        if(ownedPictures[row]!=null){UnityEngine.Object.Destroy(ownedPictures[row]);ownedPictures[row]=null;}
    }
    private void LoadPicture(int row,string path)
    {
        if(string.Equals(picturePaths[row],path,StringComparison.Ordinal))return;
        ClearPicture(row);picturePaths[row]=path;
        if(poiPictures[row]==null||string.IsNullOrEmpty(path)||!SdFile.Exists(path))return;
        Texture2D texture=null;
        try
        {
            if(new System.IO.FileInfo(path).Length>4*1024*1024)return;
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(texture,System.IO.File.ReadAllBytes(path),true))return;
            poiPictures[row].Texture=texture;ownedPictures[row]=texture;
        }
        catch(Exception error){Log.Warning("[Purge statistics] Prefab photo unavailable: "+error.Message);}
        finally{if(texture!=null&&texture!=ownedPictures[row])UnityEngine.Object.Destroy(texture);}
    }
    public override void Cleanup(){for(int i=0;i<PoiRows;i++)ClearPicture(i);base.Cleanup();}
    private void AddWaypoint(int row)
    {
        int index=(poiScroll?.FirstDataIndex??0)+row;var player=xui?.playerUI?.entityPlayer;if(player==null||index<0||index>=records.Length)return;
        var id=records[index].Identity;var waypoint=new Waypoint{pos=new Vector3i(id.X+id.SizeX/2,id.Y,id.Z+id.SizeZ/2),icon="ui_game_symbol_map_waypoint_set",bIsAutoWaypoint=false,bUsingLocalizationId=false};
        waypoint.name.Update(L(id.Prefab),PlatformManager.MultiPlatform.User.PlatformUserId);
        if(player.Waypoints.ContainsWaypoint(waypoint)||player.Waypoints.Collection.list.Any(w=>w!=null&&w.pos==waypoint.pos&&w.name.Text==waypoint.name.Text)){GameManager.ShowTooltip(player,L("ttWaypointAlreadyExists"));return;}
        player.Waypoints.Collection.Add(waypoint);waypoint.navObject=NavObjectManager.Instance.RegisterNavObject("waypoint",waypoint.pos.ToVector3(),waypoint.icon);
        if(waypoint.navObject!=null){waypoint.navObject.IsActive=true;waypoint.navObject.name=waypoint.name.Text;waypoint.navObject.usingLocalizationId=false;}
        GameManager.ShowTooltip(player,L("xuiRebirthPurgeWaypointAdded"));
    }
    private static Color BiomeColor(string name)
    {switch(name){case "pine_forest":case "forest":return new Color32(88,105,70,255);case "desert":return new Color32(185,149,91,255);case "snow":return new Color32(110,156,194,255);case "wasteland":return new Color32(150,110,155,255);case "burnt_forest":return new Color32(130,130,130,255);default:return new Color32(102,183,169,255);}}
    private static string BiomeName(string name){string key="biome_"+(name=="forest"?"pine_forest":name),text=L(key);return text==key?name.Replace('_',' '):text;}
    private XUiV_Label Label(string id)=>GetChildById(id)?.ViewComponent as XUiV_Label;
    private static void Set(XUiV_Label label,string text){if(label!=null)label.Text=text??string.Empty;}
    private static string L(string key)=>RebirthUiProjectionTextCache.L(key,key);
}

