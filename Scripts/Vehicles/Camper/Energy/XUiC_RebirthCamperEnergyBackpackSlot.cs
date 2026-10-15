using UnityEngine.Scripting;
using UnityEngine;

// Native backpack input/identity with the working storage palette commit.
[Preserve]
public sealed class XUiC_RebirthCamperEnergyBackpackSlot:XUiC_ItemStack
{
    private RebirthSlotPalette palette;
    public override void Init(){base.Init();palette=new RebirthSlotPalette(this);}
    public override void Update(float dt)
    {
        base.Update(dt);palette?.Apply(AttributeLock);
        // Match the working item presentation: global panel opacity may tint
        // backgrounds, but must not fade the actual native icon/tint.
        if(itemIconSprite==null)return;
        itemIconSprite.ForegroundLayer=true;itemIconSprite.GlobalOpacityModifier=0;
        var tint=ItemIconColor;if(itemIconSprite.Color!=tint)itemIconSprite.Color=tint;
        if(itemIconSprite.Sprite!=null&&itemIconSprite.Sprite.color!=tint)itemIconSprite.SetColorImmediately(tint);
    }
}
