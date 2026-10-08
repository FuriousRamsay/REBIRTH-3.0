using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// Renders the selected item's rail from its real inventory-slot presentation. The rail
/// width follows the icon; its thickness and bottom inset match the source slot in screen
/// space. The FilledSprite keeps the native border padding (no solid replacement rectangle).
/// Liquid volume is not item quality, even when ItemValue's default Quality is nonzero.
/// </summary>
public static class RebirthSelectedDurability
{
    private sealed class RailMetrics { public float VerticalScale = 1f; }
    private static readonly ConditionalWeakTable<XUiController, RailMetrics> Metrics =
        new ConditionalWeakTable<XUiController, RailMetrics>();
    public static void Render(XUiController owner, string id, XUiController slot)
    {
        var root = owner?.GetChildById(id);
        if (root?.ViewComponent == null) return;
        string iconId = id == "characterBagDurability" ? "characterBagSelectedIcon"
            : id == "editorBagDurability" ? "editorBagItemIcon"
            : id == "contextSelectedDurability" ? "contextSelectedIcon"
            : id == "sellSelectedDurability" ? "sellInspectIcon"
            : id == "theorySelectedDurability" ? "theoryInspectIcon"
            : "rebirthCraftingItemContextIcon";
        var icon = owner.GetChildById(iconId)?.ViewComponent as XUiV_Sprite;
        var sourceIcon = slot?.GetChildById("itemIcon")?.ViewComponent as XUiV_Sprite;
        var sourceRail = slot?.GetChildById("durability")?.ViewComponent as XUiV_Sprite;
        var value = slot is XUiC_ItemStack bag ? bag.ItemStack?.itemValue
            : (slot as XUiC_EquipmentStack)?.ItemValue;
        var number = root.GetChildById("qualityNumber")?.ViewComponent as XUiV_Label;
        if (value == null || value.IsEmpty() || icon == null || sourceIcon == null || sourceRail == null)
        {
            root.ViewComponent.IsVisible = false;
            number?.SetTextImmediately("");
            return;
        }

        bool drink = RebirthConsumableResolver.TryResolve(value, out var definition) && definition.IsDrink;
        string visible = "false", fill = "0", color = "255,255,255,255";
        string permanent = "false", remainingMaximum = "1";
        slot.GetBindingValue(ref visible, "hasdurability");
        slot.GetBindingValue(ref fill, "durabilityfill");
        slot.GetBindingValue(ref color, "durabilitycolor");
        slot.GetBindingValue(ref permanent, "haspermadurability");
        slot.GetBindingValue(ref remainingMaximum, "removeddurabilityfill");
        Color trackColor = sourceRail.Color;
        if (slot is XUiC_EquipmentStack equipment)
        {
            // EquipmentStack updates its sprite fields directly, not the ItemStack bindings.
            // Keep that existing native path instead of hiding all equipped-item rails.
            visible = equipment.durability != null && equipment.durability.IsVisible ? "true" : "false";
            if (equipment.durability != null)
            {
                fill = equipment.durability.Fill.ToString(CultureInfo.InvariantCulture);
                Color32 tint = equipment.durability.Color;
                color = tint.r + "," + tint.g + "," + tint.b + "," + tint.a;
            }
            if (equipment.durabilityRemoveBackground != null)
            {
                remainingMaximum = equipment.durabilityRemoveBackground.Fill.ToString(CultureInfo.InvariantCulture);
                trackColor = equipment.durabilityRemoveBackground.Color;
            }
            permanent = equipment.durabilityBackground != null && equipment.durabilityBackground.IsVisible ? "true" : "false";
        }
        if (drink) { visible="true"; fill=RebirthLiquidContainerService.GetFill01(value,definition).ToString(CultureInfo.InvariantCulture); color="66,139,190,255"; permanent="false"; }
        bool show = string.Equals(visible, "true", StringComparison.OrdinalIgnoreCase);
        root.ViewComponent.IsVisible = show;
        if (!show)
        {
            number?.SetTextImmediately("");
            return;
        }

        // Resolve pivots explicitly: native backpack icons are center-pivoted; these detail
        // icons are top-left-pivoted. Merely subtracting icon height placed the old rail wrong.
        Vector2 sourceTop = TopLeft(sourceIcon), railTop = TopLeft(sourceRail), destinationTop = TopLeft(icon);
        float scaleX = icon.Size.x / (float)Math.Max(1, sourceIcon.Size.x);
        // Preserve both dimensions and typography in the same proportion as the source icon.
        float scaleY = scaleX;
        float bottomInset = railTop.y - sourceRail.Size.y - (sourceTop.y - sourceIcon.Size.y);
        root.ViewComponent.Position = new Vector2i(Mathf.RoundToInt(destinationTop.x + (railTop.x - sourceTop.x) * scaleX),
            Mathf.RoundToInt(destinationTop.y - icon.Size.y + (bottomInset + sourceRail.Size.y) * scaleY));
        root.ViewComponent.Size = sourceRail.Size;
        root.ViewComponent.TryUpdatePosition();
        if (root.ViewComponent.UiTransform != null)
            root.ViewComponent.UiTransform.localScale = new Vector3(scaleX, scaleY, 1f);

        var track = root.GetChildById("track")?.ViewComponent as XUiV_Sprite;
        var target = root.GetChildById("fill")?.ViewComponent as XUiV_Sprite;
        var damage = root.GetChildById("permanent")?.ViewComponent as XUiV_Sprite;
        ConfigureRail(track, sourceRail.Size, sourceRail.SpriteName, ParseFraction(remainingMaximum, 1f), trackColor);
        ConfigureRail(target, sourceRail.Size, sourceRail.SpriteName, ParseFraction(fill, 0f), StringParsers.ParseColor32(color));
        if (damage != null)
        {
            var sourceDamage = slot.GetChildById("durabilityBackground")?.ViewComponent as XUiV_Sprite;
            bool showDamage = !drink && sourceDamage != null && string.Equals(permanent, "true", StringComparison.OrdinalIgnoreCase);
            damage.IsVisible = showDamage;
            if (showDamage) ConfigureRail(damage, sourceRail.Size, sourceDamage.SpriteName, 1f, sourceDamage.Color);
        }

        if (number != null)
        {
            var stack = (slot as XUiC_ItemStack)?.ItemStack;
            bool quality = !drink && value.ItemClass.HasQuality && value.Quality > 0;
            bool stacked = drink && stack != null && stack.count > 1;
            number.IsVisible = quality || drink;
            number.SetTextImmediately(stacked ? stack.count.ToString(CultureInfo.InvariantCulture) : drink
                ? RebirthLiquidContainerService.FormatVolume(RebirthLiquidContainerService.GetRemainingMl(value, definition))
                : quality ? value.Quality.ToString(CultureInfo.InvariantCulture) : "");
            var sourceCount = slot.GetChildById("stackValue")?.ViewComponent as XUiV_Label;
            if (sourceCount != null)
            {
                Vector2 countTop = TopLeft(sourceCount);
                number.Position = new Vector2i(Mathf.RoundToInt(countTop.x-railTop.x),Mathf.RoundToInt(countTop.y-railTop.y));
                number.Size=sourceCount.Size;
                number.Pivot=UIWidget.Pivot.TopLeft;
                number.Alignment=stacked ? NGUIText.Alignment.Right : NGUIText.Alignment.Center;
                number.FontSize=sourceCount.FontSize;
                number.TryUpdatePosition();
                if(number.UiTransform!=null)number.UiTransform.localScale=Vector3.one;
            }
        }
    }
    private static void ConfigureRail(XUiV_Sprite target, Vector2i size, string sprite, float fill, Color color)
    {
        if (target == null) return;
        target.Position = new Vector2i(0, 0);
        target.Size = size;
        target.Pivot = UIWidget.Pivot.TopLeft;
        target.SpriteName = sprite;
        target.Fill = fill;
        target.Color = color;
        // XUiV_FilledSprite owns its real width (including sprite-border clipping); do not
        // replace it with a plain sprite or force an independent UIWidget width afterwards.
        target.Update(0f);
    }
    private static float ParseFraction(string text, float fallback)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Mathf.Clamp01(value) : fallback;
    }
    private static Vector2 TopLeft(XUiV_Sprite view)
    {
        float x = view.Position.x, y = view.Position.y;
        switch (view.Pivot)
        {
            case UIWidget.Pivot.Top: case UIWidget.Pivot.Center: case UIWidget.Pivot.Bottom: x -= view.Size.x * .5f; break;
            case UIWidget.Pivot.TopRight: case UIWidget.Pivot.Right: case UIWidget.Pivot.BottomRight: x -= view.Size.x; break;
        }
        switch (view.Pivot)
        {
            case UIWidget.Pivot.Left: case UIWidget.Pivot.Center: case UIWidget.Pivot.Right: y += view.Size.y * .5f; break;
            case UIWidget.Pivot.BottomLeft: case UIWidget.Pivot.Bottom: case UIWidget.Pivot.BottomRight: y += view.Size.y; break;
        }
        return new Vector2(x, y);
    }
    private static Vector2 TopLeft(XUiV_Label view)
    {
        float x = view.Position.x, y = view.Position.y;
        switch (view.Pivot)
        {
            case UIWidget.Pivot.Top: case UIWidget.Pivot.Center: case UIWidget.Pivot.Bottom: x -= view.Size.x * .5f; break;
            case UIWidget.Pivot.TopRight: case UIWidget.Pivot.Right: case UIWidget.Pivot.BottomRight: x -= view.Size.x; break;
        }
        switch (view.Pivot)
        {
            case UIWidget.Pivot.Left: case UIWidget.Pivot.Center: case UIWidget.Pivot.Right: y += view.Size.y * .5f; break;
            case UIWidget.Pivot.BottomLeft: case UIWidget.Pivot.Bottom: case UIWidget.Pivot.BottomRight: y += view.Size.y; break;
        }
        return new Vector2(x, y);
    }
}
