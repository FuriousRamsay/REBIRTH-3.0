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
    public static void ShowScrollbar(XUiV_ScrollBar bar, bool show)
    {
        if (bar?.ScrollBar == null) return;
        // b10's scrollbar OnOpen zeroes both sprite alpha properties. Restore the properties,
        // not just the live widget alpha, so a subsequent binding pass cannot erase the bar.
        foreach (string id in new[] { "scrollbarthumb", "scrollbarbackground", "scrollbarborder" })
        {
            var sprite = bar.Controller.GetChildById(id)?.ViewComponent as XUiV_Sprite;
            if (sprite == null) continue;
            Color color = sprite.Color;
            color.a = show ? 1f : 0f;
            sprite.Color = color;
        }
        if (!Mathf.Approximately(bar.ScrollBar.alpha, show ? 1f : 0f)) bar.ScrollBar.alpha = show ? 1f : 0f;
    }
}
