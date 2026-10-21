using System;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthCamperStationPicker : XUiController
{
    private readonly XUiController[] buttons = new XUiController[RebirthCamperCraftingStation.ModuleSlots];
    private readonly XUiController[] choices = new XUiController[RebirthCamperCraftingStation.ModuleSlots];
    private readonly XUiV_Label[] names = new XUiV_Label[RebirthCamperCraftingStation.ModuleSlots];
    private readonly Guid[] identities = new Guid[RebirthCamperCraftingStation.ModuleSlots];
    private Func<bool> current;
    private Action<Guid> select;
    private Action release;
    private bool retainingLease;
    private bool opened;
    private XUiC_RebirthCharacterOverviewList scroll;
    private Vector2i lastSize = new Vector2i(-1,-1);
    private Func<Guid,RebirthCamperStationInstallations.Installation> inspect;
    private float nextRefresh;
    internal bool IsOverviewCurrent => opened && current?.Invoke()==true;
    internal string NavigationCaption(Guid id)
    {for(int i=0;i<identities.Length;i++)if(identities[i]==id)return names[i].Text;return string.Empty;}
    internal System.Collections.Generic.List<RebirthCamperStationInstallations.Installation> NavigationInstallations()
    {
        var result=new System.Collections.Generic.List<RebirthCamperStationInstallations.Installation>();
        foreach(var id in identities){var item=id==Guid.Empty?null:inspect?.Invoke(id);if(item!=null)result.Add(item);}
        return result;
    }

    public override void Init()
    {
        base.Init();
        scroll = GetChildByType<XUiC_RebirthCharacterOverviewList>();
        scroll?.BindOwner(() => opened && windowGroup.isShowing);
        for (int index = 0; index < buttons.Length; index++)
        {
            int slot = index;
            choices[index] = GetChildById("camperStationChoice" + index);
            buttons[index] = choices[index]?.GetChildById("clickable");
            names[index] = GetChildById("camperStationName" + index)?.ViewComponent as XUiV_Label;
            if (buttons[index] == null || names[index] == null)
                throw new InvalidOperationException("Station picker requires twenty labelled choices.");
            buttons[index].OnPress += (sender, button) =>
            {
                if (!opened || current == null || !current() || identities[slot] == Guid.Empty
                    || (button != -1 && button != 0)) return;
                select(identities[slot]);
            };
        }
    }

    public void Bind(Guid[] installedIds, string[] installedNames, Func<bool> isCurrent, Action<Guid> choose, Action closeSession = null, bool waiting = false,
        Func<Guid,RebirthCamperStationInstallations.Installation> inspection = null)
    {
        if (installedIds == null || installedNames == null || installedIds.Length != RebirthCamperCraftingStation.ModuleSlots || installedNames.Length != RebirthCamperCraftingStation.ModuleSlots
            || isCurrent == null || choose == null) throw new ArgumentException("Complete twenty-position station selection required.");
        var seen = new System.Collections.Generic.HashSet<Guid>();
        for (int index = 0; index < RebirthCamperCraftingStation.ModuleSlots; index++)
            if (installedIds[index] != Guid.Empty && (!seen.Add(installedIds[index]) || string.IsNullOrEmpty(installedNames[index])))
                throw new ArgumentException("Station choices must have unique identities and display names.");
        current = isCurrent; select = choose; release = closeSession; retainingLease = false;
        inspect = inspection; nextRefresh = 0;
        int installed = 0;
        for (int index = 0; index < RebirthCamperCraftingStation.ModuleSlots; index++)
        {
            if (installedIds[index] == Guid.Empty) continue;
            identities[installed] = installedIds[index];
            names[installed].Text = installedNames[index];
            choices[installed].ViewComponent.IsVisible = true;
            installed++;
        }
        for (int index = installed; index < identities.Length; index++)
        { identities[index] = Guid.Empty; names[index].Text = string.Empty; choices[index].ViewComponent.IsVisible = false; }
        scroll?.SetItemCount((installed + 2) / 3, string.Empty);
        GetChildByType<XUiC_RebirthCamperStationSwitch>()?.RefreshTabs();
        var empty = GetChildById("camperStationPickerEmpty")?.ViewComponent as XUiV_Label;
        if (empty != null) { empty.IsVisible = installed == 0; empty.Text = Localization.Get(waiting ? "xuiRebirthCamperStationsLoading" : "xuiRebirthCamperNoStations"); }
    }
    public override void OnOpen() { opened = true; base.OnOpen(); Fit(); scroll?.SetItemCount((CountInstalled() + 2) / 3, string.Empty); }
    private int CountInstalled() { int count=0; foreach(var id in identities) if(id!=Guid.Empty) count++; return count; }
    private void Fit()
    {
        Vector2i size=xui.GetXUiScreenSize();
        if(size==lastSize)return; lastSize=size;
        RebirthCamperStationScreenFrame.Apply(this);
    }
    public override void Update(float dt)
    {
        if (opened && (current == null || !current()))
        {
            opened = false;
            xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
            return;
        }
        base.Update(dt);
        if(opened)Fit();
        if(opened&&xui.playerUI.entityPlayer.world.IsRemote())
            RebirthCamperRemoteStationClient.RefreshOverview(xui.playerUI.entityPlayer,this);
        if(opened && UnityEngine.Time.realtimeSinceStartup >= nextRefresh)
        { nextRefresh=UnityEngine.Time.realtimeSinceStartup+.5f; RefreshCards(); }
    }
    private void RefreshCards()
    {
        int installedCount=0,workingCount=0,idleCount=0,fuelCount=0;
        for(int index=0;index<identities.Length;index++)
        {
            if(identities[index]==Guid.Empty)continue;
            var installed=inspect?.Invoke(identities[index]);
            if(installed==null)continue;
            var state=installed.State;
            installedCount++;
            var icon=GetChildById("stationIcon"+index)?.ViewComponent as XUiV_Sprite;
            if(icon!=null){icon.SpriteName=installed.InstalledItem.itemValue.ItemClass.GetIconName();icon.IsVisible=true;}
            RecipeQueueItem active=null; int queued=0;
            foreach(var job in state.Queue)
                if(job?.Recipe!=null){queued++;if(active==null||job.IsCrafting)active=job;}
            bool cooking=active?.Recipe!=null&&RebirthCookingHeat.Managed(active.Recipe);
            bool ready=cooking&&RebirthCookingHeat.Ready(active.Recipe);
            bool burnt=cooking&&RebirthCookingHeat.Burnt(active.Recipe);
            var cardColor=cooking
                ? burnt?new UnityEngine.Color32(220,90,80,255):ready?new UnityEngine.Color32(235,210,118,255):new UnityEngine.Color32(117,205,143,255)
                : new UnityEngine.Color32(117,205,143,255);
            bool missing=active!=null&&!RebirthStationToolAvailability.HasRequirement(state.Tools,active.Recipe);
            bool fuelMissing=state.isModuleUsed[3]&&active!=null&&state.BurnTotalTimeLeft<=0;
            if(fuelMissing)fuelCount++;else if(active?.IsCrafting==true)workingCount++;else idleCount++;
            var accent=GetChildById("stationFuelAccent"+index)?.ViewComponent;
            if(accent!=null)accent.IsVisible=fuelMissing;
            void CardText(string id,string value){if(GetChildById(id+index)?.ViewComponent is XUiV_Label label)label.Text=value;}
            CardText("stationCategory",installed.InstalledItem.itemValue.ItemClass.GetLocalizedItemName());
            CardText("stationRecipeName",active==null?Localization.Get("xuiCamperWorkshopNoRecipe"):cooking?RebirthCookingHeat.Name(active.Recipe):Localization.Get(active.Recipe.GetName()));
            CardText("stationRecipeTime",active==null?string.Empty:cooking?RebirthCookingHeat.Timer(active.Recipe):RebirthCookingHeat.FormatDuration(active.CraftingTimeLeft));
            if(GetChildById("stationRecipeIcon"+index)?.ViewComponent is XUiV_Sprite recipeIcon)
            {var output=active?.Recipe?.GetOutputItemClass();recipeIcon.IsVisible=output!=null;if(output!=null){recipeIcon.SpriteName=output.GetIconName();recipeIcon.Color=output.GetIconTint();}}
            CardText("stationFuel",Localization.Get("xuiCamperWorkshopFuel")+" "+(state.isModuleUsed[3]?RebirthCookingHeat.FormatDuration(state.BurnTotalTimeLeft):"—"));
            CardText("stationQueue",Localization.Get("xuiCamperWorkshopQueue")+" "+queued+" / "+state.Queue.Length);
            int outputs=0;foreach(var stack in state.Output)if(stack!=null&&!stack.IsEmpty())outputs++;
            CardText("stationOutput",Localization.Get("xuiCamperWorkshopOutput")+" "+outputs+" / "+state.Output.Length);
            var progress=GetChildById("stationProgress"+index)?.ViewComponent as XUiV_Sprite;
            if(progress!=null)
            {
                progress.Fill=cooking?UnityEngine.Mathf.Clamp01(RebirthCookingHeat.Number(active.Recipe,"elapsed")/Math.Max(.001f,RebirthCookingHeat.Number(active.Recipe,"duration"))):active!=null&&active.OneItemCraftTime>0f
                    ? UnityEngine.Mathf.Clamp01(1f-active.CraftingTimeLeft/active.OneItemCraftTime):0f;
                progress.Color=cooking?cardColor:missing||fuelMissing?new UnityEngine.Color32(235,210,118,255):cardColor;
            }
            string key=missing?"xuiRebirthCamperCardMissingTools":fuelMissing?"xuiRebirthCamperCardNeedsFuel":
                active==null?"xuiRebirthCamperCardIdle":active.IsCrafting?"xuiRebirthCamperCardCrafting":"xuiRebirthCamperCardQueued";
            var status=GetChildById("stationStatus"+index)?.ViewComponent as XUiV_Label;
            if(status!=null){status.Text=fuelMissing?Localization.Get("xuiRebirthCamperCardNeedsFuel"):cooking?RebirthCookingHeat.Status(active.Recipe,state.IsBurning):Localization.Get(key);status.Color=cooking?cardColor:missing||fuelMissing?new UnityEngine.Color32(235,210,118,255):active==null?new UnityEngine.Color32(255,255,255,255):cardColor;}
            var statusIcon=GetChildById("stationStateIcon"+index)?.ViewComponent as XUiV_Sprite;
            if(statusIcon!=null)
            {
                statusIcon.SpriteName=cooking?"ui_game_symbol_clock":missing?"ui_game_symbol_wrench":fuelMissing?"ui_game_symbol_gas":"ui_game_symbol_clock";
                statusIcon.Color=status?.Color??new UnityEngine.Color32(255,255,255,255);
            }
            var details=GetChildById("stationDetails"+index)?.ViewComponent as XUiV_Label;
            if(details!=null)
            {
                string tool=missing?new ItemValue(active.Recipe.craftingToolType,false).ItemClass?.GetLocalizedItemName():null;
                details.Text=(active==null?string.Empty:(cooking?RebirthCookingHeat.Name(active.Recipe):Localization.Get(active.Recipe.GetName()))+" · "+(cooking?RebirthCookingHeat.Timer(active.Recipe):RebirthCookingHeat.FormatDuration(active.CraftingTimeLeft))+"\n")+
                    (ready||string.IsNullOrEmpty(tool)?string.Empty:"\n"+string.Format(Localization.Get("xuiRebirthCamperCardTool"),tool));
            }
        }
        void Summary(string id,string key,int count){if(GetChildById(id)?.ViewComponent is XUiV_Label label)label.Text=Localization.Get(key)+"  "+count;}
        Summary("workshopInstalled","xuiCamperWorkshopInstalled",installedCount);
        Summary("workshopWorking","xuiCamperWorkshopWorking",workingCount);
        Summary("workshopIdle","xuiCamperWorkshopIdle",idleCount);
        Summary("workshopNeedsFuel","xuiRebirthCamperCardNeedsFuel",fuelCount);
    }
    public override void OnClose()
    {
        var close = retainingLease ? null : release;
        release = null; retainingLease = false;
        opened = false; current = null; select = null; Array.Clear(identities, 0, identities.Length);
        inspect = null;
        base.OnClose();
        close?.Invoke();
    }
    internal void RetainLeaseForSelection() { retainingLease = true; }
    internal void SelectInstallation(Guid identity)
    {
        if(!opened||identity==Guid.Empty||current==null||!current())return;
        foreach(var id in identities)if(id==identity){select?.Invoke(identity);return;}
    }
}
