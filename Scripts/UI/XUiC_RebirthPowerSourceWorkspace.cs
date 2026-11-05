using System;
using UnityEngine;
using UnityEngine.Scripting;

// Presentation only: the native PowerSourceWindowGroup retains tile binding,
// slot transactions, refuelling, switching, destruction and close ownership.
[Preserve]
public sealed class XUiC_RebirthPowerSourceWorkspace : XUiController
{
    private readonly RebirthWindowHudScope hud = new RebirthWindowHudScope();
    public static XUiC_RebirthPowerSourceWorkspace ActiveInstance { get; private set; }
    public bool IsWorkspaceOpen => opened;
    public XUiC_RebirthEditorBackpack Backpack => windowGroup?.Controller?.GetChildByType<XUiC_RebirthEditorBackpack>();
    private bool opened;
    private Vector2i lastPosition, lastSize;
    private float next;
    public override void OnOpen()
    {
        base.OnOpen();opened=true;ActiveInstance=this;
        xui.playerUI.windowManager.Open("toolbelt",false);
        xui.playerUI.windowManager.Open("dragAndDrop",false);
        Apply(true);hud.Maintain(xui);
        var group=windowGroup.Controller;
        var native=group as XUiC_PowerSourceWindowGroup;
        var title=GetChildById("rebirthBankTitle")?.ViewComponent as XUiV_Label;
        if(title!=null&&native?.TileEntity!=null)
        {
            var tile=native.TileEntity;
            title.SetTextImmediately(tile.GetChunk().GetBlock(tile.localChunkPos).Block.GetLocalizedBlockName());
        }
        var info=group.GetChildById("rebirthBankItemInfo");
        if(info?.ViewComponent!=null)info.ViewComponent.IsVisible=false;
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if(!opened)return;
        // Native windows return to scale 1 after they open. Positions below are scaled, so an
        // unscaled panel would overlap its neighbour on any screen where the layout has to shrink.
        if(scaled==null)
        {
            var group=windowGroup.Controller;
            scaled=new[]{group.GetChildById("windowPowerSourceStats"),group.GetChildById("windowPowerSourceSlots"),
                group.GetChildById("rebirthBankBackpack"),group.GetChildById("rebirthBankItemInfo")};
        }
        for(int i=0;i<scaled.Length;i++)KeepScale(scaled[i]);
        if(Time.realtimeSinceStartup<next)return;
        next=Time.realtimeSinceStartup+.2f;Apply(false);hud.Maintain(xui);
    }
    public override void OnClose()
    {
        opened=false;if(ActiveInstance==this)ActiveInstance=null;hud.Restore();
        if(xui?.DragAndDropWindow!=null)xui.DragAndDropWindow.InMenu=xui.playerUI.windowManager.IsWindowOpen("windowpaging");
        base.OnClose();
    }
    private void Apply(bool force)
    {
        if(xui==null||ViewComponent==null)return;
        RebirthScreenLayout.GetScreenBounds(xui,out var position,out var size);
        if(!force&&lastPosition==position&&lastSize==size)return;
        lastPosition=position;lastSize=size;
        ViewComponent.Position=position;ViewComponent.Size=size;ViewComponent.TryUpdatePosition();
        float scale=Math.Min(Math.Max(1,size.x-32)/1840f,Math.Max(1,size.y-32)/883f);
        layoutScale=new Vector3(scale,scale,1);
        var origin=new Vector2i(position.x+Mathf.RoundToInt((size.x-1840*scale)/2),
            position.y-Mathf.RoundToInt((size.y-883*scale)/2));
        var header=GetChildById("rebirthBankHeader")?.ViewComponent;
        if(header!=null)
        {
            header.Position=new Vector2i(origin.x-position.x,origin.y-position.y);
            header.TryUpdatePosition();
            if(header.UiTransform!=null)header.UiTransform.localScale=new Vector3(scale,scale,1);
        }
        var group=windowGroup.Controller;
        Place(group.GetChildById("windowPowerSourceStats"),origin,8,-62,scale);
        Place(group.GetChildById("windowPowerSourceSlots"),origin,8,-332,scale);
        Place(group.GetChildById("rebirthBankBackpack"),origin,991,-62,scale);
        Place(group.GetChildById("rebirthBankItemInfo"),origin,1158,-98,scale);
    }
    private XUiController[] scaled;
    private Vector3 layoutScale=Vector3.one;
    private void KeepScale(XUiController controller)
    {
        var transform=controller?.ViewComponent?.UiTransform;
        if(transform!=null&&transform.localScale!=layoutScale)transform.localScale=layoutScale;
    }
    private static void Place(XUiController controller,Vector2i origin,int x,int y,float scale)
    {
        var view=controller?.ViewComponent;if(view==null)return;
        view.Position=new Vector2i(origin.x+Mathf.RoundToInt(x*scale),origin.y+Mathf.RoundToInt(y*scale));
        view.TryUpdatePosition();
        if(view.UiTransform!=null)view.UiTransform.localScale=new Vector3(scale,scale,1);
    }
}

[Preserve]
public sealed class XUiC_RebirthPowerSourceSlots : XUiC_PowerSourceSlots
{
    private bool infoBound;
    public override void SetSlots(ItemStack[] stacks)
    {
        base.SetSlots(stacks);
        // Native calls this every update. The slots and info window never change, so bind them once.
        if(infoBound)return;
        infoBound=true;
        var info=windowGroup?.Controller?.GetChildById("rebirthBankItemInfo") as XUiC_ItemInfoWindow;
        foreach(var slot in GetChildrenByType<XUiC_ItemStack>())slot.InfoWindow=info;
    }
}

// The native remembered selection is shared by all BackpackWindow instances.
// A control from another screen cannot be restored inside this embedded bank bag.
[Preserve]
public sealed class XUiC_RebirthBankBackpackWindow : XUiC_BackpackWindow
{
    public override void OnOpen()
    {
        if(!string.IsNullOrEmpty(defaultSelectedElement)&&GetChildById(defaultSelectedElement)==null)
            defaultSelectedElement=string.Empty;
        base.OnOpen();
    }
}

[Preserve]
public sealed class XUiC_RebirthBankPowerSlot : XUiC_RequiredItemStack
{
    private XUiC_ItemStack geometry;
    public override void Init()
    {
        base.Init();
        // Same native input flags as the working backpack slots; without drag a stack cannot leave this slot.
        ViewComponent.EventOnPress=true;ViewComponent.EventOnDrag=true;ViewComponent.EventOnHover=true;
        // The native lock scales with cell_size, which is too large at 128.
        var lockIcon=GetChildById("assembleIcon")?.ViewComponent;
        if(lockIcon!=null){lockIcon.Size=new Vector2i(64,64);lockIcon.Position=new Vector2i(32,-32);}
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if(geometry==null)
        {
            var slots=windowGroup?.Controller?.GetChildByType<XUiC_RebirthEditorBackpack>()?.GetItemStackControllers();
            if(slots!=null&&slots.Length>0)geometry=slots[0];
        }
        if(geometry!=null)RebirthSelectedDurability.CopyNativePresentation(this,geometry,false,ItemStack,fitRailToSlot:true);
    }

    public override void OnHovered(bool over)
    {RebirthCharacterItemStatsTooltip.Hover(this,over);base.OnHovered(over);}
    public override bool GetBindingValueInternal(ref string value,string name)
    {
        if(name=="tooltip"&&RebirthCharacterItemStatsTooltip.Build(this,ItemStack)!=null)
        {value=string.Empty;return true;}
        return base.GetBindingValueInternal(ref value,name);
    }
    public override void HandleClickComplete()
    {base.HandleClickComplete();windowGroup?.Controller?.GetChildByType<XUiC_RebirthEditorBackpack>()?.Inspect(this);}
    public override void updateItemInfoWindow(XUiC_ItemStack slot)
    {windowGroup?.Controller?.GetChildByType<XUiC_RebirthEditorBackpack>()?.Inspect(this);}
}
