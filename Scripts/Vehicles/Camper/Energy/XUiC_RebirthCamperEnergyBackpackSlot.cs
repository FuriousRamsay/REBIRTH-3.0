using UnityEngine.Scripting;
using UnityEngine;

// Native backpack input/identity with the working storage palette commit.
[Preserve]
public sealed class XUiC_RebirthCamperEnergyBackpackSlot:XUiC_ItemStack
{
    private RebirthSlotPalette palette;
    public override void Init()
    {
        base.Init();palette=new RebirthSlotPalette(this);
        ViewComponent.EventOnPress=true;ViewComponent.EventOnHover=true;
        ViewComponent.EventOnDrag=true;ViewComponent.EventOnScroll=true;
    }
    public override void OnHovered(bool over)
    {
        RebirthCharacterItemStatsTooltip.Hover(this,over);
        base.OnHovered(over);
    }
    public override void OnClose()
    {
        RebirthCharacterItemStatsTooltip.Hover(this,false);
        base.OnClose();
    }
    public override bool GetBindingValueInternal(ref string value,string name)
    {
        if(name=="tooltip")
        {
            var text=RebirthCharacterItemStatsTooltip.Build(this,ItemStack);
            if(text!=null){value=text;return true;}
        }
        return base.GetBindingValueInternal(ref value,name);
    }
    public override void Update(float dt)
    {
        // The window keeps capacity for 79 slots, but smaller backpacks hide
        // unused cells. They have no input or visible bindings to maintain.
        if(!ViewComponent.IsVisible)return;
        base.Update(dt);palette?.Apply(AttributeLock);
        // Match the working item presentation: global panel opacity may tint
        // backgrounds, but must not fade the actual native icon/tint.
        if(itemIconSprite==null)return;
        if(!itemIconSprite.ForegroundLayer)itemIconSprite.ForegroundLayer=true;
        if(!Mathf.Approximately(itemIconSprite.GlobalOpacityModifier,0f))itemIconSprite.GlobalOpacityModifier=0f;
        var tint=ItemIconColor;if(itemIconSprite.Color!=tint)itemIconSprite.Color=tint;
        if(itemIconSprite.Sprite!=null&&itemIconSprite.Sprite.color!=tint)itemIconSprite.SetColorImmediately(tint);
    }
}
