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
            choices.IsVisible=!choices.IsVisible;openedFrame=Time.frameCount;
        };
        var values=((QuickStackCategory[])Enum.GetValues(typeof(QuickStackCategory)))
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
                if((button!=0&&button!=-1)||!Current)return;
                all=index==0;if(!all)category=values[index-1];
                choices.IsVisible=false;dirty=true;RefreshCaption();Apply();
            };
        }
        GetChildByType<XUiC_RebirthCharacterOverviewList>()?.BindOwner(()=>Current);
        ViewComponent.IsVisible=false;
    }
    public override void OnOpen()
    {
        base.OnOpen();owner=RebirthContextNavigationService.Session?.BagContainer;all=true;choices.IsVisible=false;RefreshCaption();BindBag();
        GetChildByType<XUiC_RebirthCharacterOverviewList>()?.SetItemCount(Enum.GetValues(typeof(QuickStackCategory)).Length+1,string.Empty);
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
    private void Changed(){dirty=true;}
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
