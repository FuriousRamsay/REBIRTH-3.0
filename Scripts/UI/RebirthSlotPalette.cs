using UnityEngine;

/// <summary>One persistent palette for Creative, Character backpack and the container workspace.</summary>
public sealed class RebirthSlotPalette
{
    public const string Normal = "58,58,65,255";
    public const string Locked = "31,31,36,255";
    private readonly XUiV_Sprite background, fill;
    public RebirthSlotPalette(XUiC_ItemStack slot)
    {
        // Update the controller's real color inputs AND view properties. A one-time write to
        // UISprite.color alone is overwritten by {backgroundcolor} on the next binding pass.
        slot.ParseAttribute("background_color", Normal);
        slot.ParseAttribute("attribute_lock_color", Locked);
        background = slot.GetChildById("backgroundMain")?.ViewComponent as XUiV_Sprite;
        fill = slot.GetChildById("highlightOverlay")?.ViewComponent as XUiV_Sprite;
        Apply(slot.AttributeLock);
    }
    public void Apply(bool locked)
    {
        // Match Crafting's post-native palette commit. A slot's lock bit can be unchanged
        // while native hover/binding/OnEnable code changes its live sprite color. Cache the
        // views, not that bit; update only divergent properties and commit the sprite now.
        Commit(background, new Color32(30, 30, 36, 255));
        Commit(fill, locked ? new Color32(31, 31, 36, 255) : new Color32(58, 58, 65, 255));
    }
    private static void Commit(XUiV_Sprite view, Color color)
    {
        if (view == null) return;
        if (view.Color != color) view.Color = color;
        if (view.Sprite != null && view.Sprite.color != color) view.SetColorImmediately(color);
    }
    private sealed class LockViews
    {
        internal XUiView Icon;
        internal LockViews(XUiC_ItemStack slot){Icon=slot.GetChildById("iconSlotLock")?.ViewComponent;}
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_ItemStack,LockViews> Locks=new System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_ItemStack,LockViews>();
    public static void ApplyLockIcon(XUiC_ItemStack slot)
    {
        if(slot==null)return;
        var icon=Locks.GetValue(slot,s=>new LockViews(s)).Icon;
        if(icon==null)return;
        bool show=slot.UserLockedSlot;
        if(icon.IsVisible!=show){icon.IsVisible=show;icon.Update(0f);}
    }
    private sealed class ScrollbarViews
    {
        internal readonly XUiV_Sprite[] Sprites;
        internal ScrollbarViews(XUiV_ScrollBar bar){Sprites=new[]{bar.Controller.GetChildById("scrollbarthumb")?.ViewComponent as XUiV_Sprite,bar.Controller.GetChildById("scrollbarbackground")?.ViewComponent as XUiV_Sprite,bar.Controller.GetChildById("scrollbarborder")?.ViewComponent as XUiV_Sprite};}
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XUiV_ScrollBar,ScrollbarViews> Bars=new System.Runtime.CompilerServices.ConditionalWeakTable<XUiV_ScrollBar,ScrollbarViews>();
    public static void ShowScrollbar(XUiV_ScrollBar bar, bool show)
    {
        if(bar?.ScrollBar==null)return;
        foreach(var sprite in Bars.GetValue(bar,b=>new ScrollbarViews(b)).Sprites)
        {
            if(sprite==null)continue;
            Color color=sprite.Color;float alpha=show?1f:0f;
            if(!Mathf.Approximately(color.a,alpha)){color.a=alpha;sprite.Color=color;}
        }        if (!Mathf.Approximately(bar.ScrollBar.alpha, show ? 1f : 0f)) bar.ScrollBar.alpha = show ? 1f : 0f;
    }
}
