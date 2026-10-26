using UnityEngine;
using UnityEngine.Scripting;

/// <summary>Uses the personal Crafting action palette and native selection-row hover.</summary>
[Preserve]
public sealed class XUiC_RebirthCookingAction : XUiController
{
    private bool hover, armed;
    private XUiController fillController;
    private XUiV_Sprite fillSprite;
    private int pressedFrame = -1;
    internal bool PreserveIconColor;
    internal Color? StateFillColor;
    public override void Init()
    {
        base.Init();
        fillController=GetChildById("actionFill");
        fillSprite=fillController?.ViewComponent as XUiV_Sprite;
        OnHover+=(sender,over)=>hover=over;
        OnMouseUpDown+=(sender,down)=>
        {
            if(down)armed=UICamera.currentTouchID == -1 && hover&&ViewComponent.Enabled;
            else
            {
                bool activate=armed&&hover&&ViewComponent.Enabled;armed=false;
                if(activate)Pressed(-1);
            }
        };
    }
    public override void Pressed(int mouseButton)
    {
        if(!ViewComponent.Enabled || pressedFrame==Time.frameCount)return;
        pressedFrame=Time.frameCount;
        base.Pressed(mouseButton);
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        bool enabled=ViewComponent.Enabled;
        Color color=enabled?new Color32(240,240,244,255):new Color32(120,120,126,255);
        if(fillSprite is XUiV_Sprite fill)
        {
            if(fill.SpriteName!="menu_empty")fill.SpriteName="menu_empty";
            Color desired=StateFillColor.HasValue?StateFillColor.Value:enabled&&hover?new Color32(75,75,80,255):new Color32(17,17,21,255);
            if(StateFillColor.HasValue&&enabled&&hover)desired=Color.Lerp(desired,Color.white,.15f);
            if(fill.Color!=desired)fill.Color=desired;
        }
        foreach(var child in Children)
        {
            if(child==fillController)continue;
            if(child.ViewComponent is XUiV_Label label&&label.Color!=color)label.Color=color;
            if(child.ViewComponent is XUiV_Sprite icon&&!(PreserveIconColor&&child.ViewComponent.ID=="actionIcon")&&icon.Color!=color)icon.Color=color;
        }
    }
}

public static class RebirthCookingSlotStyle
{
    private sealed class Views
    {
        internal readonly RebirthSlotPalette Palette;
        internal readonly XUiV_Sprite[] Bars;
        internal Views(XUiC_ItemStack slot)
        {
            Palette = new RebirthSlotPalette(slot);
            var bars = new System.Collections.Generic.List<XUiV_Sprite>();
            foreach (var child in slot.Children)
                if ((child.ViewComponent?.ID == "durability" || child.ViewComponent?.ID == "durabilityBackground") && child.ViewComponent is XUiV_Sprite bar) bars.Add(bar);
            Bars = bars.ToArray();
        }
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_ItemStack,Views> Cache = new System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_ItemStack,Views>();
    public static void Apply(XUiC_ItemStack slot,bool ingredient=false)
    {
        var views = Cache.GetValue(slot, s => new Views(s));
        views.Palette.Apply(false);
        if(ingredient)
            foreach(var bar in views.Bars)
            {
                var pos=new Vector2i(2,-(slot.ViewComponent.Size.y-12));
                var size=new Vector2i(slot.ViewComponent.Size.x-4,10);
                if(bar.Position!=pos)bar.Position=pos;
                if(bar.Size!=size)bar.Size=size;
            }
        if(ingredient&&slot.itemIconSprite!=null)
        {
            if(slot.itemIconSprite.Size!=new Vector2i(64,64))slot.itemIconSprite.Size=new Vector2i(64,64);
            if(slot.itemIconSprite.Position!=new Vector2i(43,-49))slot.itemIconSprite.Position=new Vector2i(43,-49);
        }
    }
}
[Preserve]
public sealed class XUiC_RebirthCookingToolSlot : XUiC_RequiredItemStack
{
    public override void Init(){base.Init();RebirthCookingPointerDrop.Initialize(this);RebirthCookingSlotStyle.Apply(this);}
    public override void Update(float dt){long profile=RebirthCookingDiagnostics.Begin();RebirthCookingPointerDrop.Arm(this);base.Update(dt);RebirthCookingSlotStyle.Apply(this);RebirthCookingDiagnostics.Section("tool slots",profile);}
}
[Preserve]
public sealed class XUiC_RebirthCookingFuelSlot : XUiC_ItemStack
{
    public override void Init()
    {
        base.Init();RebirthCookingPointerDrop.Initialize(this);
        if(GetChildById("stackValue")?.ViewComponent is XUiV_Label count)count.FontSize=22;
    }
    public override void Update(float dt)
    {
        long profile=RebirthCookingDiagnostics.Begin();
        RebirthCookingPointerDrop.Arm(this);
        base.Update(dt);RebirthCookingSlotStyle.Apply(this);
        if(itemIconSprite!=null)
        {
            bool empty=ItemStack==null||ItemStack.IsEmpty();
            // The sibling SlotPreview renders below the native cell fill.
            // Put the placeholder in the cell's own icon layer instead.
            itemIconSprite.UIAtlas=empty?"ItemIconAtlasGreyscale":"ItemIconAtlas";
            if(empty)
            {
                itemIconSprite.SpriteName="resourceWood";
                itemIconSprite.Color=new Color32(155,155,164,150);
                itemIconSprite.IsVisible=true;
            }
        }
        RebirthCookingDiagnostics.Section("fuel slots",profile);
    }
}
