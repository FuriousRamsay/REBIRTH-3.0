using System;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthCamperStationSwitch : XUiController
{
    private XUiC_WorkstationWindowGroup stationWindow;
    private readonly Guid[] tabIds = new Guid[6];
    private readonly XUiController[] tabs = new XUiController[6];
    private readonly XUiView[] tabGroups = new XUiView[6];
    private readonly XUiV_Label[] captions = new XUiV_Label[6];
    private int page;
    private XUiView classic;
    private bool classicVisible;
    private XUiView classicTop;
    private bool classicTopVisible;
    private XUiC_RebirthCamperStationPicker overview;

    public override void Init()
    {
        base.Init();
        overview=GetParentByType<XUiC_RebirthCamperStationPicker>();
        stationWindow = GetParentByType<XUiC_WorkstationWindowGroup>()
            ?? windowGroup.Controller as XUiC_WorkstationWindowGroup;
        var button = GetChildById("camperSwitchStationButton");
        if (button == null) throw new InvalidOperationException("Camper station switch button is missing.");
        button.OnHover+=(sender,hovered)=>ApplyTab(-1,hovered);
        button.OnPress += (sender, mouseButton) =>
        {
            if (mouseButton != -1 && mouseButton != 0) return;
            if (!VisibleForCurrentStation()) return;
            if(overview!=null)return;
            RebirthCamperStationPointerTransition.Navigation(this,mouseButton,Guid.Empty);
            if (!RebirthCamperStationWindowSession.Switch(stationWindow))
                GameManager.ShowTooltip(xui.playerUI.entityPlayer, Localization.Get("xuiRebirthCamperStationSaving"));
        };
        ViewComponent.IsVisible = false;
        for(int i=0;i<tabs.Length;i++)
        {
            int slot=i; tabs[i]=GetChildById("camperStationTab"+i);
            tabGroups[i]=GetChildById("camperStationTabGroup"+i)?.ViewComponent;
            if(tabGroups[i]==null)throw new InvalidOperationException("Camper station tab group is missing: "+i);
            tabGroups[i].IsVisible=false;
            captions[i]=GetChildById("camperStationTabName"+i)?.ViewComponent as XUiV_Label;
            if(tabs[i]!=null)tabs[i].OnPress+=(sender,mouseButton)=>
            {
                if((mouseButton!=0&&mouseButton!=-1)||!VisibleForCurrentStation()||tabIds[slot]==Guid.Empty)return;
                if(overview!=null){RebirthCamperStationPointerTransition.Navigation(this,mouseButton,tabIds[slot]);overview.SelectInstallation(tabIds[slot]);return;}
                if(stationWindow.WorkstationData.TileEntity is RebirthCamperStationState active&&active.InstallationId!=tabIds[slot])
                {
                    RebirthCamperStationPointerTransition.Navigation(this,mouseButton,tabIds[slot]);
                    RebirthCamperStationWindowSession.Switch(stationWindow,tabIds[slot]);
                }
            };
            if(tabs[i]!=null)tabs[i].OnHover+=(sender,hovered)=>ApplyTab(slot,hovered);
        }
        var previous=GetChildById("camperStationPrevious");
        if(previous!=null)previous.OnPress+=(sender,b)=>{if((b==0||b==-1)&&page>0){page--;RefreshTabs();}};
        var next=GetChildById("camperStationNext");
        if(next!=null)next.OnPress+=(sender,b)=>{if((b==0||b==-1)&&page<3){page++;RefreshTabs();}};
    }

    private bool VisibleForCurrentStation()
        => overview!=null ? overview.IsOverviewCurrent : stationWindow != null && RebirthCamperStationWindowSession.TryGetCurrent(xui?.playerUI?.entityPlayer, out var state)
            && ReferenceEquals(stationWindow.WorkstationData?.TileEntity, state);

    public override void OnOpen()
    {
        base.OnOpen();
        ViewComponent.IsVisible = VisibleForCurrentStation();
        page=0;
        if(ViewComponent.IsVisible)
        {
            classic=overview==null?windowGroup.Controller.GetChildByType<XUiC_RebirthBackpackSectionNavigationChrome>()?.ViewComponent:null;
            if(classic!=null){classicVisible=classic.IsVisible;classic.IsVisible=false;}
            var cooking=GetParentByType<XUiC_RebirthCookingWorkspace>();
            var stationSurface=GetParentByType<XUiC_RebirthPersonalCraftingSurface>();
            classicTop=(cooking as XUiController??stationSurface)?.GetChildById("rebirthCraftingTopZone")?.ViewComponent;
            if(classicTop!=null){classicTopVisible=classicTop.IsVisible;classicTop.IsVisible=false;}
            var model=RebirthCamperStationWindowSession.InstalledForNavigation(stationWindow);
            int visibleIndex=0;
            if(model!=null)for(int i=0;i<RebirthCamperCraftingStation.ModuleSlots;i++)
            {var item=model.GetSlot(i);if(item==null)continue;if(ReferenceEquals(item.State,stationWindow.WorkstationData.TileEntity)){page=visibleIndex/6;break;}visibleIndex++;}
            RefreshTabs();
        }
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        ViewComponent.IsVisible = VisibleForCurrentStation();
        if(ViewComponent.IsVisible&&classic!=null)classic.IsVisible=false;
        if(ViewComponent.IsVisible&&classicTop!=null)classicTop.IsVisible=false;
    }
    internal void RefreshTabs()
    {
        var overviewIcon=GetChildById("camperOverviewTabIcon")?.ViewComponent as XUiV_Sprite;
        if(overviewIcon!=null)
        {overviewIcon.UIAtlas="UIAtlas";overviewIcon.SpriteName="ui_game_symbol_workbench";}
        ApplyTab(-1,false);
        var model=RebirthCamperStationWindowSession.InstalledForNavigation(stationWindow);
        var installed=overview?.NavigationInstallations()??new System.Collections.Generic.List<RebirthCamperStationInstallations.Installation>();
        if(model!=null)for(int i=0;i<RebirthCamperCraftingStation.ModuleSlots;i++)
        {var item=model.GetSlot(i);if(item!=null)installed.Add(item);}
        page=Math.Min(page,Math.Max(0,(installed.Count-1)/6));
        var ids=GetIds(model);
        for(int i=0;i<tabs.Length;i++)
        {
            int index=page*6+i;bool visible=index<installed.Count;
            // One native parent controls all chrome and hit targets together.
            // Child updates cannot reveal an unused tab under its inactive parent.
            tabGroups[i].IsVisible=visible;
            tabIds[i]=visible?installed[index].Id:Guid.Empty;
            if(tabs[i]!=null)tabs[i].ViewComponent.IsVisible=visible;
            var icon=GetChildById("camperStationTabIcon"+i)?.ViewComponent as XUiV_Sprite;
            if(icon!=null)
            {
                icon.IsVisible=visible;
                if(visible){icon.UIAtlas="UIAtlas";icon.SpriteName=NavigationIcon(installed[index]);}
            }
            var label=captions[i];
            if(label!=null)
            {
                label.IsVisible=visible;
                label.Text=!visible?string.Empty:overview!=null?overview.NavigationCaption(installed[index].Id)
                    :installed[index].InstalledItem.itemValue.ItemClass.GetLocalizedItemName()+" ("+(Array.IndexOf(ids,installed[index].Id)+1)+")";
            }
            ApplyTab(i,false);
            var bg=GetChildById("camperStationTabBg"+i)?.ViewComponent;
            var frame=GetChildById("camperStationTabFrame"+i)?.ViewComponent;
            if(bg!=null)bg.IsVisible=visible;
            if(frame!=null)frame.IsVisible=visible;
        }
        var previous=GetChildById("camperStationPrevious");if(previous!=null)previous.ViewComponent.IsVisible=installed.Count>6;
        var next=GetChildById("camperStationNext");if(next!=null)next.ViewComponent.IsVisible=installed.Count>6;
    }
    // Use the same authored station symbols as native WorkstationData, including
    // inherited/custom station definitions. Inventory CustomIcon is a separate bitmap.
    private static string NavigationIcon(RebirthCamperStationInstallations.Installation installation)
    {
        var block=installation?.State?.InstalledBlock.Block;
        var data=block==null?null:CraftingManager.GetWorkstationData(block.GetBlockName());
        return string.IsNullOrEmpty(data?.WorkstationIcon)?"ui_game_symbol_hammer":data.WorkstationIcon;
    }
    private void ApplyTab(int index,bool hovered)
    {
        bool first=index<0;
        var background=GetChildById(first?"camperOverviewTabBg":"camperStationTabBg"+index)?.ViewComponent as XUiV_Sprite;
        var icon=GetChildById(first?"camperOverviewTabIcon":"camperStationTabIcon"+index)?.ViewComponent as XUiV_Sprite;
        var caption=GetChildById(first?"camperOverviewTabCaption":"camperStationTabName"+index)?.ViewComponent as XUiV_Label;
        bool active=first?overview!=null:tabIds[index]!=Guid.Empty
            &&tabIds[index]==(stationWindow?.WorkstationData?.TileEntity as RebirthCamperStationState)?.InstallationId;
        XUiC_RebirthCraftingTopTabs.ApplyTabVisual(background,icon,caption,active,hovered);
    }
    private static Guid[] GetIds(RebirthCamperStationInstallations model)
    {var ids=new Guid[RebirthCamperCraftingStation.ModuleSlots];if(model!=null)for(int i=0;i<ids.Length;i++)ids[i]=model.GetSlot(i)?.Id??Guid.Empty;return ids;}

    public override void OnClose()
    {
        ViewComponent.IsVisible = false;
        if(classic!=null)classic.IsVisible=classicVisible;
        if(classicTop!=null)classicTop.IsVisible=classicTopVisible;
        classicTop=null;
        classic=null;Array.Clear(tabIds,0,tabIds.Length);
        foreach(var group in tabGroups)if(group!=null)group.IsVisible=false;
        base.OnClose();
    }
}
