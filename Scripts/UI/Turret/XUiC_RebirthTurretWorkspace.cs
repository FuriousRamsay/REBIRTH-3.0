using System;
using UnityEngine;
using UnityEngine.Scripting;

// The native group still owns tile-entity binding, target masks, ammunition,
// lock/channel lifecycle, destruction and maximized camera transitions.
[Preserve]
public sealed class XUiC_RebirthTurretWindowGroup:XUiC_PowerRangedTrapWindowGroup
{
    public override void Init()
    {
        base.Init();
        // The native preview definition is shared with sensors/spotlights. Bind
        // this group's private presentation without changing those other UIs.
        cameraWindowPreview=GetChildById("rebirthTurretCameraPreview") as XUiC_CameraWindow;
        if(cameraWindowPreview!=null)cameraWindowPreview.Owner=this;
    }
}

[Preserve]
public sealed class XUiC_RebirthTurretWorkspace:XUiController
{
    public static XUiC_RebirthTurretWorkspace ActiveInstance;
    public bool IsWorkspaceOpen=>opened;
    private readonly RebirthWindowHudScope hud=new RebirthWindowHudScope();
    private XUiController options,ammo,camera,backpack,info,header;
    private bool opened,headerWasVisible;
    private float next;
    private Vector2i lastPosition=new Vector2i(int.MinValue,int.MinValue),lastSize=new Vector2i(-1,-1);
    public override void Init()
    {
        base.Init();Resolve();
        var close=info?.GetChildById("rebirthTurretCloseInfo");
        if(close!=null)close.OnPress+=(sender,button)=>{if(info?.ViewComponent!=null)info.ViewComponent.IsVisible=false;};
        Apply(true);
    }
    private void Resolve()
    {
        var group=windowGroup?.Controller;
        options=group?.GetChildById("windowPowerRangedTrapOptions");ammo=group?.GetChildById("windowPowerTrapSlots");
        camera=group?.GetChildById("rebirthTurretCameraPreview");backpack=group?.GetChildById("rebirthTurretBackpack");
        info=group?.GetChildById("rebirthTurretItemInfo");
    }
    public override void OnOpen()
    {
        base.OnOpen();opened=true;ActiveInstance=this;Resolve();
        GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(RebirthCraftingNavigationService.Destination.Crafting);
        header=xui.FindWindowGroupByName("windowpaging")?.GetChildById("windowPagingHeader");
        if(header?.ViewComponent!=null){headerWasVisible=header.ViewComponent.IsVisible;header.ViewComponent.IsVisible=false;}
        RebirthPersonalCraftingHudSuppressionInstaller.EnsureInstalled();
        xui.playerUI.windowManager.Open("toolbelt",false);xui.playerUI.windowManager.Open("dragAndDrop",false);
        // Native CameraWindow sizes its RenderTexture from the authored camera
        // texture during open; authored 16:9 dimensions are never resized here.
        Apply(true);hud.Maintain(xui);
        if(info?.ViewComponent!=null)info.ViewComponent.IsVisible=false;
    }
    public override void Update(float dt)
    {
        base.Update(dt);if(!opened||Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.2f;
        Apply(false);hud.Maintain(xui);
        if(header?.ViewComponent!=null)header.ViewComponent.IsVisible=false;
    }
    public override void OnClose()
    {
        opened=false;if(ActiveInstance==this)ActiveInstance=null;hud.Restore();
        if(header?.ViewComponent!=null)header.ViewComponent.IsVisible=headerWasVisible;
        base.OnClose();
    }
    private void Apply(bool force)
    {
        if(xui==null||ViewComponent==null)return;
        RebirthScreenLayout.GetScreenBounds(xui,out var position,out var size);
        if(!force&&lastPosition==position&&lastSize==size&&ViewComponent.Position==position)return;
        lastPosition=position;lastSize=size;ViewComponent.Position=position;ViewComponent.Size=size;ViewComponent.TryUpdatePosition();
        Set("rebirthTurretWorkspaceBackground",0,0,size.x,size.y);
        int width=Math.Max(1,size.x-16);float scale=Math.Min(width/1856f,Math.Max(1,size.y-78)/813f);
        Set("rebirthCraftingTopZone",8,-8,width,52);Set("rebirthCraftingTopBackground",0,0,width,52);Set("rebirthCraftingTopFrame",0,0,width,52);
        RebirthPersonalCraftingLayoutService.ApplySharedTopLayout(this,width);
        Place(options,position,0,0,scale);Place(ammo,position,466,0,scale);
        Place(camera,position,0,-226,scale);Place(backpack,position,916,0,scale);
        Place(info,position,1083,-180,scale);
        var title=camera?.GetChildById("rebirthTurretDeviceName")?.ViewComponent as XUiV_Label;
        var group=windowGroup?.Controller as XUiC_PowerRangedTrapWindowGroup;
        if(title!=null&&group?.TileEntity!=null)
        {
            var tile=group.TileEntity;
            string name=tile.GetChunk().GetBlock(tile.localChunkPos).Block.GetLocalizedBlockName()+" · "+Localization.Get("xuiCameraPreview");
            if(title.Text!=name)title.Text=name;
        }
    }
    private void Set(string id,int x,int y,int width,int height)
    {var view=GetChildById(id)?.ViewComponent;if(view==null)return;view.Position=new Vector2i(x,y);view.Size=new Vector2i(width,height);view.TryUpdatePosition();}
    private static void Place(XUiController controller,Vector2i origin,int x,int y,float scale)
    {
        var view=controller?.ViewComponent;if(view==null)return;
        view.Position=new Vector2i(origin.x+8+Mathf.RoundToInt(x*scale),origin.y-70+Mathf.RoundToInt(y*scale));view.TryUpdatePosition();
        if(view.UiTransform!=null)view.UiTransform.localScale=new Vector3(scale,scale,1);
    }
}

// Same native Backpack/ItemStack transactions as Character/Crafting. The fixed
// presentation pool is never serialized back as a larger physical inventory.
[Preserve]
public sealed class XUiC_RebirthTurretBackpack:XUiC_Backpack
{
    public override void SetStacks(ItemStack[] stacks)
    {
        if(stacks==null)return;
        if(itemControllers==null||itemControllers.Length==0)itemControllers=GetChildrenByType<XUiC_ItemStack>();
        base.SetStacks(stacks);maxSlotCount=xui.playerUI.entityPlayer.CarryCapacity;
        int physical=Math.Min(stacks.Length,itemControllers.Length);
        int unencumbered=RebirthCraftingInventoryBridge.GetUnencumberedSlotCount(xui);
        var inspector=windowGroup.Controller.GetChildById("rebirthTurretItemInfo") as XUiC_ItemInfoWindow;
        for(int i=0;i<itemControllers.Length;i++)
        {
            var slot=itemControllers[i];if(slot?.ViewComponent==null)continue;
            bool show=i<physical;slot.ViewComponent.IsVisible=show;slot.ViewComponent.Enabled=show;
            if(slot.ViewComponent.UiTransform!=null)
            {slot.ViewComponent.UiTransform.gameObject.SetActive(show);slot.ViewComponent.UiTransform.localScale=Vector3.one*(56f/75f);}
            slot.ViewComponent.Position=new Vector2i(i%13*58,-(i/13)*58);
            slot.SlotNumber=i;slot.InfoWindow=inspector;
            slot.AttributeLock=i>=unencumbered;
        }
    }
    public override void HandleSlotChangedEvent(int slotNumber,ItemStack stack)
    {
        var bag=RebirthCraftingInventoryBridge.GetBag(xui);
        if(bag==null||slotNumber<0||slotNumber>=bag.ItemGrid.items.Length)return;
        bag.SetSlot(slotNumber,stack?.Clone()??ItemStack.Empty.Clone());
    }
    public override ItemStack[] getUISlots()
    {
        var actual=GetSlots();var result=ItemStack.CreateArray(actual?.Length??0);
        for(int i=0;i<result.Length;i++)result[i]=i<itemControllers.Length?itemControllers[i].ItemStack.Clone():actual[i].Clone();
        return result;
    }
}

[Preserve]
public sealed class XUiC_RebirthTurretBackpackSlot:XUiC_ItemStack
{
    private RebirthSlotPalette palette;
    public override void Init(){base.Init();palette=new RebirthSlotPalette(this);}
    public override void Update(float dt)
    {if(ViewComponent?.UiTransform?.gameObject.activeInHierarchy!=true)return;base.Update(dt);palette?.Apply(AttributeLock);}
}

[Preserve]
public sealed class XUiC_RebirthTurretAmmoSlots:XUiC_PowerRangedAmmoSlots
{
    public override void SetSlots(ItemStack[] stacks)
    {
        base.SetSlots(stacks);
        var inspector=windowGroup?.Controller?.GetChildById("rebirthTurretItemInfo") as XUiC_ItemInfoWindow;
        var slots=GetItemStackControllers();
        if(slots!=null)foreach(var slot in slots)if(slot!=null)slot.InfoWindow=inspector;
    }
}

[Preserve]
public sealed class XUiC_RebirthTurretItemInfo:XUiC_ItemInfoWindow
{
    public override void Update(float dt)
    {
        // This group's inspector overlays its own backpack. Empty selection
        // returns to that backpack rather than revealing the hidden external
        // backpack group's emptyInfoPanel over the turret presentation.
        if((selectedItemStack==null||selectedItemStack.ItemStack.IsEmpty())&&ViewComponent!=null)ViewComponent.IsVisible=false;
        base.Update(dt);
    }
}
