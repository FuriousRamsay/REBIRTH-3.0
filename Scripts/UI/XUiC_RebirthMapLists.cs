using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// Keep the native waypoint actions and selection model; replace only list population.
[Preserve]
public sealed class XUiC_RebirthWaypointList : XUiC_MapWaypointList
{
    private XUiC_RebirthCharacterOverviewList scroll;
    private float refresh;
    public override void Init()
    {
        RebirthMapListInit.Children(this);
        scroll=GetParentByType<XUiC_RebirthCharacterOverviewList>();
        var root=xui.GetWindow("mapTracking").Controller;
        
        trackBtn=root.GetChildById("trackBtn");trackBtn.OnPress+=onTrackWaypointPressed;
        showOnMapBtn=root.GetChildById("showOnMapBtn");showOnMapBtn.OnPress+=onShowOnMapPressed;
        waypointRemoveBtn=root.GetChildById("waypointRemoveBtn");waypointRemoveBtn.OnPress+=onWaypointRemovePressed;
        inviteBtn=root.GetChildById("inviteBtn");inviteBtn.OnPress+=onInvitePressed;
        txtInputFilter=(XUiC_TextInput)root.GetChildById("searchInput");
        txtInputFilter.OnChangeHandler+=(s,t,c)=>{scroll.ResetPosition();Populate();};
        xui.GetWindow("mapTrackingPopup").Controller.GetChildById("inviteFriends").OnPress+=onInviteFriendsPressed;
        xui.GetWindow("mapTrackingPopup").Controller.GetChildById("inviteEveryone").OnPress+=onInviteEveryonePressed;
        scroll.DataRangeChanged+=()=>Populate();
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.map.scroll"),typeof(RebirthWaypointPopulate));
    }
    public override void Update(float dt){base.Update(dt);if(!xui.playerUI.windowManager.IsWindowOpen("map")){refresh=0;return;}refresh-=dt;if(refresh<=0){refresh=.3f;Populate();}}
    public void Populate(Waypoint selected=null)
    {
        if(scroll==null||xui.playerUI.entityPlayer==null)return;
        if(selected!=null)SelectedWaypoint=selected;
        var player=xui.playerUI.entityPlayer;
        var items=player.Waypoints.Collection.list.Where(w=>!w.HiddenOnMap && (string.IsNullOrEmpty(txtInputFilter.Text)||(w.bIsAutoWaypoint||w.bUsingLocalizationId?Localization.Get(w.name.Text):w.name.Text).IndexOf(txtInputFilter.Text,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
        items.Sort(new WaypointSorter(player));
        scroll.SetItemCount(items.Count,"");
        if(selected!=null)
        {
            int index=items.IndexOf(selected);
            if(index>=0&&(index*36<scroll.ScrollOffset||(index+1)*36>scroll.ScrollOffset+scroll.ViewComponent.Size.y))scroll.RestoreScrollOffset(index*36);
        }
        SelectedWaypointEntry=null;
        for(int i=0;i<Children.Count;i++)
        {
            var row=(XUiC_MapWaypointListEntry)Children[i];int n=scroll.FirstDataIndex+i;
            var w=n<items.Count?items[n]:null;row.ViewComponent.IsVisible=w!=null;
            bool changed=row.Waypoint!=w;if(changed)row.Selected=false;if(changed)row.Waypoint=w;row.Index=n;bool isSelected=w!=null&&w==SelectedWaypoint;if(row.Selected!=isSelected)row.Selected=isSelected;
            if(w==null)continue;
            row.Background.Enabled=true;row.Sprite.SpriteName=w.icon;row.Tracking.IsVisible=w.bTracked;
            RebirthMapListInit.Name(w,row.Name,()=>row.Waypoint==w);
            row.Distance.Text=ValueDisplayFormatters.Distance(new Vector2(w.pos.x-player.position.x,w.pos.z-player.position.z).magnitude);
            if(row.Selected)SelectedWaypointEntry=row;
        }
        // A pooled row scrolling offscreen must not clear the underlying selection. Only clear
        // it when the selected waypoint is no longer in the filtered/source snapshot.
        if(SelectedWaypoint!=null&&!items.Contains(SelectedWaypoint))SelectedWaypoint=null;
    }
}
[HarmonyPatch(typeof(XUiC_MapWaypointList),nameof(XUiC_MapWaypointList.UpdateWaypointsList))]
internal static class RebirthWaypointPopulate
{
    static bool Prefix(XUiC_MapWaypointList __instance,Waypoint _selectThisWaypoint){var list=__instance as XUiC_RebirthWaypointList;if(list==null)return true;list.Populate(_selectThisWaypoint);return false;}
}
[Preserve]
public sealed class XUiC_RebirthInviteList : XUiC_MapInvitesList
{
    private XUiC_RebirthCharacterOverviewList scroll;
    private float refresh;
    public override void Init()
    {
        RebirthMapListInit.Children(this);scroll=GetParentByType<XUiC_RebirthCharacterOverviewList>();
        var root=xui.GetWindow("mapInvites").Controller;
        waypointSetBtn=root.GetChildById("waypointSetBtn");waypointSetBtn.OnPress+=onInviteAddToWaypoints;
        waypointShowOnMapBtn=root.GetChildById("showOnMapBtn");waypointShowOnMapBtn.OnPress+=onInviteShowOnMapPressed;
        waypointRemoveBtn=root.GetChildById("waypointRemoveBtn");waypointRemoveBtn.OnPress+=onInviteRemovePressed;
        waypointReportBtn=root.GetChildById("waypointReportBtn");waypointReportBtn.OnPress+=onReportWaypointPressed;
        scroll.DataRangeChanged+=Populate;
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.map.invites.scroll"),typeof(RebirthInvitePopulate));
    }
    public override void Update(float dt){base.Update(dt);if(!xui.playerUI.windowManager.IsWindowOpen("map")){refresh=0;return;}refresh-=dt;if(refresh<=0){refresh=.3f;Populate();}}
    public void Populate()
    {
        if(scroll==null||xui.playerUI.entityPlayer==null)return;
        var player=xui.playerUI.entityPlayer;var items=player.WaypointInvites;scroll.SetItemCount(items.Count,"");SelectedInviteEntry=null;
        for(int i=0;i<Children.Count;i++)
        {
            var row=(XUiC_MapInvitesListEntry)Children[i];int n=scroll.FirstDataIndex+i;var w=n<items.Count?items[n]:null;
            row.ViewComponent.IsVisible=w!=null;bool changed=row.Waypoint!=w;if(changed)row.Selected=false;if(changed)row.Waypoint=w;row.Index=n;bool isSelected=w!=null&&w==SelectedInvite;if(row.Selected!=isSelected)row.Selected=isSelected;
            if(w==null)continue;row.Sprite.SpriteName=w.icon;row.Background.SoundPlayOnClick=true;
            RebirthMapListInit.Name(w,row.Name,()=>row.Waypoint==w);
            row.Distance.Text=ValueDisplayFormatters.Distance(new Vector2(w.pos.x-player.position.x,w.pos.z-player.position.z).magnitude);
            if(row.Selected)SelectedInviteEntry=row;
        }
        if(SelectedInvite!=null&&!items.Contains(SelectedInvite))SelectedInvite=null;
    }
}
[HarmonyPatch(typeof(XUiC_MapInvitesList),nameof(XUiC_MapInvitesList.UpdateInvitesList))]
internal static class RebirthInvitePopulate
{
    static bool Prefix(XUiC_MapInvitesList __instance){var list=__instance as XUiC_RebirthInviteList;if(list==null)return true;list.Populate();return false;}
}
internal static class RebirthMapListInit
{
    // Native list Init assumes the old un-clipped hierarchy. Initialize views identically,
    // then let the subclasses bind actions at the window root.
    public static void Children(XUiController c){c.ViewComponent?.InitView();foreach(var child in c.Children){child.AutoBindComponents();child.Init();child.AutoBindEvents();}}
    private sealed class NameState
    {
        internal Waypoint Waypoint;
        internal string Source;
        internal bool Localized;
        internal int Revision;
    }
    private static readonly ConditionalWeakTable<XUiV_Label, NameState> Names = new ConditionalWeakTable<XUiV_Label, NameState>();

    private static NameState CreateNameState(XUiV_Label label) { return new NameState(); }

    public static void Name(Waypoint waypoint, XUiV_Label label, Func<bool> current)
    {
        if (label == null || waypoint == null) return;
        bool localized = waypoint.bIsAutoWaypoint || waypoint.bUsingLocalizationId;
        string source = localized ? Localization.Get(waypoint.name.Text) : waypoint.name.Text;
        NameState state = Names.GetValue(label, CreateNameState);
        if (ReferenceEquals(state.Waypoint, waypoint) && state.Source == source && state.Localized == localized) return;
        state.Waypoint = waypoint;
        state.Source = source;
        state.Localized = localized;
        int revision = ++state.Revision;
        if (localized)
        {
            if (label.Text != source) label.Text = source;
            return;
        }
        // Clear the pooled row while filtering its replacement; never show the old name.
        label.Text = string.Empty;
        GeneratedTextManager.GetDisplayText(waypoint.name, text =>
        {
            if (state.Revision != revision) return;
            if (!current() || waypoint.name.Text != source
                || waypoint.bIsAutoWaypoint || waypoint.bUsingLocalizationId)
            {
                // This result never reached the label. Returning to the same waypoint
                // must request a fresh result, rather than treating the blank row as cached.
                state.Waypoint = null;
                return;
            }
            if (label.Text != text) label.Text = text;
        }, true, false, GeneratedTextManager.TextFilteringMode.FilterWithSafeString);
    }
}

[Preserve]
public sealed class XUiC_RebirthChallengeGroupList : XUiC_ChallengeGroupList
{
    private static readonly string[] ChapterBackgrounds={"backgroundMain","background"};
    private string category;
    private float nextPresentation;
    // The native update refreshes every challenge entry's bindings each frame (colour parsing + boxing, ~900 KB/s
    // with the list fully expanded). Progress text does not need more than 4 Hz.
    private const float NativeUpdateInterval=0.25f;
    private float pendingDt=NativeUpdateInterval;
    public override void OnOpen(){base.OnOpen();pendingDt=NativeUpdateInterval;}
    public override void Update(float dt)
    {
        pendingDt+=dt;
        string current=CategoryList?.CurrentCategory?.CategoryName;
        bool categoryChanged=!string.Equals(current,category,StringComparison.Ordinal);
        if(!categoryChanged&&pendingDt<NativeUpdateInterval)return;
        if(challengeGroupList!=null && challengeGroupList.Any(g=>string.Equals(g.ChallengeGroup.Name,"rebirthLearningLegacy",StringComparison.OrdinalIgnoreCase)))
            challengeGroupList=challengeGroupList.Where(g=>!string.Equals(g.ChallengeGroup.Name,"rebirthLearningLegacy",StringComparison.OrdinalIgnoreCase)).ToList();
        base.Update(pendingDt);
        pendingDt=0f;
        if(!categoryChanged&&Time.realtimeSinceStartup<nextPresentation)return;
        nextPresentation=Time.realtimeSinceStartup+0.10f;

        int count=0;
        int contentHeight=0;
        foreach(var entry in entryList)
        {
            bool visible=entry.Entry!=null;
            if(entry.ViewComponent!=null&&entry.ViewComponent.IsVisible!=visible)entry.ViewComponent.IsVisible=visible;
            if(visible)
            {
                count++;
                if(DisplayKey!="weather")
                {
                    int lessons=entry.ChallengeList?.challengeList?.Count??0;
                    int height=48+Math.Max(1,(lessons+7)/8)*94;
                    if(entry.ViewComponent.Position.y!=-contentHeight)
                        entry.ViewComponent.Position=new Vector2i(0,-contentHeight);
                    if(entry.ViewComponent.Size.y!=height)
                        entry.ViewComponent.Size=new Vector2i(764,height);
                    foreach(string id in ChapterBackgrounds)
                    {
                        var background=entry.GetChildById(id)?.ViewComponent;
                        if(background!=null && background.Size.y!=height)
                            background.Size=new Vector2i(background.Size.x,height);
                    }
                    contentHeight+=height;
                }
            }
            if(entry.ChallengeList!=null)foreach(var challenge in entry.ChallengeList.entryList)
            {
                bool challengeVisible=challenge.Entry!=null;
                if(challenge.ViewComponent!=null&&challenge.ViewComponent.IsVisible!=challengeVisible)challenge.ViewComponent.IsVisible=challengeVisible;
            }
        }
        int wantedHeight=Math.Max(1,DisplayKey=="weather"?count*166:contentHeight);
        if(ViewComponent.Size.y!=wantedHeight)ViewComponent.Size=new Vector2i(ViewComponent.Size.x,wantedHeight);
        if(categoryChanged){category=current;var view=Parent.ViewComponent as XUiV_ScrollView;view?.scrollView?.ResetPosition();}
    }
}


