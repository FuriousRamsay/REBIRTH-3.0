using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthCamperCargoCategoryFilter : XUiController
{
    private XUiC_BagContainer owner;
    private Bag boundBag;
    private bool all=true,dirty;
    private XUiC_RebirthContextGrid cargoView;
    private QuickStackCategory category;
    private int openedFrame=-1;
    private XUiView choices;
    private XUiV_Label caption;
    private XUiView storageTitle;
    private int authoredTitleWidth;
    // The dropdown lists "All categories" plus only the categories present in the cargo (owner request
    // 2026-11-07). shown[i] is the category on row i (null = all); unused rows are hidden.
    private QuickStackCategory[] values;
    private readonly System.Collections.Generic.List<QuickStackCategory?> shown=new System.Collections.Generic.List<QuickStackCategory?>();
    private const int RowPitch=32, MaxVisibleRows=10;
    private bool Current => RebirthContextNavigationService.Session?.Suspended==false&&owner?.Bag!=null&&RebirthCamperRadial.IsCamper(xui?.Vehicle?.CurrentVehicle)
        &&ReferenceEquals(owner.Bag,xui.Vehicle.CurrentVehicle.bag);

    public override void Init()
    {
        base.Init();cargoView=GetParentByType<XUiC_RebirthContainerWorkspace>()?.GetChildById("contextContainerGrid") as XUiC_RebirthContextGrid;
        choices=GetChildById("camperCargoCategoryChoices")?.ViewComponent;
        caption=GetChildById("camperCargoCategoryCaption")?.ViewComponent as XUiV_Label;
        storageTitle=GetParentByType<XUiC_RebirthContainerWorkspace>()?.GetChildById("contextContainerName")?.ViewComponent;
        authoredTitleWidth=storageTitle?.Size.x??0;
        GetChildById("camperCargoCategoryToggle").OnPress+=(sender,button)=>
        {
            if((button!=0&&button!=-1)||!Current)return;
            if(!choices.IsVisible)RebuildChoices();
            choices.IsVisible=!choices.IsVisible;openedFrame=Time.frameCount;
        };
        values=((QuickStackCategory[])Enum.GetValues(typeof(QuickStackCategory)))
            .OrderBy(value=>value==QuickStackCategory.None?0:1)
            .ThenBy(value=>QuickStackCategoryRegistry.GetDisplayName(value),StringComparer.CurrentCultureIgnoreCase).ToArray();
        for(int i=0;i<=values.Length;i++)
        {
            int index=i;
            var row=GetChildById("camperCargoCategoryChoice"+i);
            if(row==null)throw new InvalidOperationException("Cargo category dropdown does not match QuickStack categories.");
            var label=GetChildById("camperCargoCategoryChoiceLabel"+i)?.ViewComponent as XUiV_Label;
            if(label!=null)label.Text=i==0?Localization.Get("xuiRebirthCamperCargoAllCategories"):QuickStackCategoryRegistry.GetDisplayName(values[i-1]);
            row.OnPress+=(sender,button)=>
            {
                if((button!=0&&button!=-1)||!Current||index>=shown.Count)return;
                var picked=shown[index];all=!picked.HasValue;if(!all)category=picked.Value;
                choices.IsVisible=false;dirty=true;RefreshCaption();Apply();
            };
        }
        GetChildByType<XUiC_RebirthCharacterOverviewList>()?.BindOwner(()=>Current);
        ViewComponent.IsVisible=false;
    }
    public override void OnOpen()
    {
        base.OnOpen();owner=RebirthContextNavigationService.Session?.BagContainer;all=true;choices.IsVisible=false;RefreshCaption();BindBag();
        RebuildChoices();
    }
    public override void Update(float dt)
    {
        base.Update(dt);owner=RebirthContextNavigationService.Session?.BagContainer;ViewComponent.IsVisible=Current;
        ReserveTitleWidth(Current);
        if(!Current)return;
        if(!ReferenceEquals(boundBag,owner.Bag))BindBag();
        if(dirty)Apply();
        if(choices.IsVisible&&Time.frameCount!=openedFrame&&xui.playerUI.playerInput.GUIActions.LeftClick.WasPressed)
        {
            var hovered=UICamera.hoveredObject;
            if(hovered==null||!hovered.transform.IsChildOf(ViewComponent.UiTransform))choices.IsVisible=false;
        }
    }
    private void RefreshCaption(){if(caption!=null)caption.Text=all?Localization.Get("xuiRebirthCamperCargoAllCategories"):QuickStackCategoryRegistry.GetDisplayName(category);}
    private void ReserveTitleWidth(bool visible)
    {
        if(storageTitle==null||authoredTitleWidth<=0)return;
        // Both views share contextContainerPanel coordinates. Reserve the title
        // only to this actual dropdown's left edge; native shrink overflow keeps
        // long storage names inside that rectangle without changing the layout.
        int width=visible?Math.Max(1,Math.Min(authoredTitleWidth,ViewComponent.Position.x-storageTitle.Position.x)):authoredTitleWidth;
        if(storageTitle.Size.x!=width)storageTitle.Size=new Vector2i(width,storageTitle.Size.y);
    }
    private void Changed(){dirty=true;if(choices!=null&&choices.IsVisible)RebuildChoices();}
    private void RebuildChoices()
    {
        var present=new System.Collections.Generic.HashSet<QuickStackCategory>();
        var slots=owner?.Bag?.ItemGrid?.items;
        if(slots!=null)foreach(var stack in slots)if(stack!=null&&!stack.IsEmpty())present.Add(QuickStackCategoryRegistry.Get(stack.itemValue.type));
        shown.Clear();shown.Add(null);
        foreach(var value in values)if(present.Contains(value))shown.Add(value);
        for(int i=0;i<=values.Length;i++)
        {
            var row=GetChildById("camperCargoCategoryRow"+i)?.ViewComponent;
            if(row!=null&&row.IsVisible!=(i<shown.Count))row.IsVisible=i<shown.Count;
            if(i>=shown.Count)continue;
            var label=GetChildById("camperCargoCategoryChoiceLabel"+i)?.ViewComponent as XUiV_Label;
            if(label!=null)label.Text=shown[i].HasValue?QuickStackCategoryRegistry.GetDisplayName(shown[i].Value):Localization.Get("xuiRebirthCamperCargoAllCategories");
        }
        // A picked category that is no longer in the cargo falls back to all categories.
        if(!all&&!present.Contains(category)){all=true;dirty=true;RefreshCaption();}
        int height=Math.Min(shown.Count,MaxVisibleRows)*RowPitch;
        Resize(choices,height);Resize(GetChildById("camperCargoCategoryScroll")?.ViewComponent,height);Resize(GetChildById("listTrack")?.ViewComponent,height);
        var viewport=GetChildById("listViewport")?.ViewComponent;Resize(viewport,height);
        if(viewport is XUiV_Panel panel){panel.ClippingSize=new Vector2(panel.ClippingSize.x,height);panel.ClippingCenter=new Vector2(panel.ClippingCenter.x,-height/2f);}
        GetChildByType<XUiC_RebirthCharacterOverviewList>()?.SetItemCount(shown.Count,string.Empty);
    }
    private static void Resize(XUiView view,int height){if(view!=null&&view.Size.y!=height)view.Size=new Vector2i(view.Size.x,height);}
    private void BindBag()
    {
        Unbind();if(!Current)return;
        boundBag=owner.Bag;boundBag.OnBackpackItemsChangedInternal+=Changed;
        dirty=true;Apply();
    }
    private void Apply()
    {
        if(!Current)return;
        cargoView?.SetCategoryFilter(all?(QuickStackCategory?)null:category);dirty=false;
    }
    private void Unbind()
    {
        if(boundBag==null)return;
        boundBag.OnBackpackItemsChangedInternal-=Changed;
        cargoView?.SetCategoryFilter(null);
        boundBag=null;
    }
    public override void OnClose(){Unbind();all=true;choices.IsVisible=false;ViewComponent.IsVisible=false;ReserveTitleWidth(false);base.OnClose();}
    public override void Cleanup(){Unbind();ReserveTitleWidth(false);base.Cleanup();}
}
