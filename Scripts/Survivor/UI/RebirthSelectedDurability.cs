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
            root.ViewComponent.IsVisible = false; if(owner.GetChildById(id+"Count")?.ViewComponent is XUiV_Label stale)stale.IsVisible=false;
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
        var actualFill=NativeCache.GetValue(slot,c=>new NativeViews(c,false)).Fill;
        if(actualFill!=null)
        {
            fill=actualFill.Fill.ToString(CultureInfo.InvariantCulture);
            Color32 tint=actualFill.Color;color=tint.r+","+tint.g+","+tint.b+","+tint.a;
        }
        if (drink) { visible="true"; fill=RebirthLiquidContainerService.GetFill01(value,definition).ToString(CultureInfo.InvariantCulture); color="66,139,190,255"; permanent="false"; }
        bool show = string.Equals(visible, "true", StringComparison.OrdinalIgnoreCase);
        root.ViewComponent.IsVisible = show;
        RenderPlainCount(owner,id,slot,sourceIcon,icon,show);
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

        var stack = (slot as XUiC_ItemStack)?.ItemStack;
        ApplyCount(number, slot.GetChildById("stackValue")?.ViewComponent as XUiV_Label,
            sourceIcon, icon, value, stack?.count ?? 1, root.ViewComponent.Position, true,sourceRail);
    }
    private static void RenderPlainCount(XUiController owner,string id,XUiController slot,XUiV_Sprite fromIcon,XUiV_Sprite toIcon,bool railVisible)
    {
        string legacy=id=="sellSelectedDurability"?"sellSelectedCount":id=="theorySelectedDurability"?"theorySelectedCount":id=="contextSelectedDurability"?"contextSelectedCount":null;
        if(legacy!=null&&owner.GetChildById(legacy)?.ViewComponent is XUiV_Label old)old.IsVisible=false;
        var target=owner.GetChildById(id+"Count")?.ViewComponent as XUiV_Label;
        if(target==null)return;
        target.IsVisible=!railVisible;
        if(railVisible)return;
        var stack=(slot as XUiC_ItemStack)?.ItemStack;
        ApplyCount(target,slot?.GetChildById("stackValue")?.ViewComponent as XUiV_Label,
            fromIcon,toIcon,stack?.itemValue??(slot as XUiC_EquipmentStack)?.ItemValue,stack?.count??1,Vector2i.zero,false);
    }
    // Native preview widgets share the real slot's geometry rather than using another
    // cell_size template whose constant rail height/font do not scale with its icon.
    public static void CopyNativePresentation(XUiController target, XUiController source, bool infoWindow = false, ItemStack referenceStack = null)
    {
        if (target == null || source == null || ReferenceEquals(target,source)) return; if(referenceStack!=null&&referenceStack.IsEmpty())return;
        var sourceIcon=source.GetChildById("itemIcon")?.ViewComponent as XUiV_Sprite;
        var targetIcon=target.GetChildById(infoWindow?"itemPreview":"itemIcon")?.ViewComponent as XUiV_Sprite;
        if(sourceIcon==null||targetIcon==null)return;
        var sourceViews=NativeCache.GetValue(source,c=>new NativeViews(c,false));
        var targetViews=NativeCache.GetValue(target,c=>new NativeViews(c,infoWindow));
        float ratio=targetIcon.Size.x/(float)Math.Max(1,sourceIcon.Size.x);
        Vector2 from=TopLeft(sourceIcon),to=TopLeft(targetIcon);
        CopySprite(targetViews.Track,sourceViews.Track,from,to,ratio,referenceStack==null);
        CopySprite(targetViews.Fill,sourceViews.Fill,from,to,ratio,referenceStack==null);
        CopySprite(targetViews.Damage,sourceViews.Damage,from,to,ratio,referenceStack==null);
        var inspected=referenceStack??(source as XUiC_ItemStack)?.ItemStack;
        var value=inspected?.itemValue??(source as XUiC_EquipmentStack)?.ItemValue;
        if(RebirthConsumableResolver.TryResolve(value,out var definition)&&definition.IsDrink)
        {
            if(targetViews.Fill!=null){targetViews.Fill.IsVisible=true;targetViews.Fill.Fill=RebirthLiquidContainerService.GetFill01(value,definition);targetViews.Fill.SetColorImmediately(new Color32(66,139,190,255));}
            if(targetViews.Damage!=null)targetViews.Damage.IsVisible=false;
        }
        ApplyCount(targetViews.Count,sourceViews.Count,sourceIcon,targetIcon,value,inspected?.count??1,Vector2i.zero,false,sourceViews.Track??sourceViews.Fill);
    }

    // One label path for every manual detail panel, native detail panel and Modify preview.
    // Keep the native slot's top-left count rectangle: its text sits slightly above the rail.
    // Runtime Pivot changes alone do not update UIWidget.pivot, so commit both layers.
    private static void ApplyCount(XUiV_Label target,XUiV_Label source,XUiV_Sprite fromIcon,
        XUiV_Sprite toIcon,ItemValue value,int count,Vector2i railOrigin,bool nested,XUiV_Sprite sourceRail=null)
    {
        if(target==null)return;
        target.IsVisible=value!=null&&!value.IsEmpty()&&count>0;
        if(!target.IsVisible||source==null)return;
        bool drink=RebirthConsumableResolver.TryResolve(value,out var definition)&&definition.IsDrink;
        bool volume=drink&&count==1;
        bool quality=!drink&&value.ItemClass.HasQuality&&value.Quality>0;
        float ratio=toIcon.Size.x/(float)Math.Max(1,fromIcon.Size.x);
        Vector2 from=TopLeft(fromIcon),to=TopLeft(toIcon),label=TopLeft(source);
        var layout=RebirthItemPreviewLayout.Place(from.x,from.y,fromIcon.Size.x,to.x,to.y,toIcon.Size.x,
            label.x,label.y,source.FontSize,quality,volume,nested,railOrigin.x,railOrigin.y,
            sourceRail==null?float.NaN:TopLeft(sourceRail).x,sourceRail?.Size.x??0,source.Size.x);

        target.Pivot=UIWidget.Pivot.TopLeft;
        if(target.widget!=null)target.widget.pivot=UIWidget.Pivot.TopLeft;
        target.Position=new Vector2i(Mathf.RoundToInt(layout.X),Mathf.RoundToInt(layout.Y));
        target.Size=source.Size;
        target.Alignment=volume||quality?NGUIText.Alignment.Center:NGUIText.Alignment.Right;
        target.FontSize=layout.FontSize;
        target.SetTextImmediately(volume?RebirthLiquidContainerService.FormatVolume(RebirthLiquidContainerService.GetRemainingMl(value,definition)):
            quality?value.Quality.ToString(CultureInfo.InvariantCulture):count.ToString(CultureInfo.InvariantCulture));
        if(target.widget!=null)target.widget.pivot=UIWidget.Pivot.TopLeft;
        target.Update(0f);
        target.TryUpdatePosition();
        if(target.UiTransform!=null)target.UiTransform.localScale=new Vector3(layout.Scale,layout.Scale,1f);
    }
    private sealed class NativeViews
    {
        internal XUiV_Sprite Track,Fill,Damage;internal XUiV_Label Count;
        internal NativeViews(XUiController root,bool info)
        {
            Count=root.GetChildById(info?"durabilityValue":"stackValue")?.ViewComponent as XUiV_Label;
            if(info){Track=root.GetChildById("removedurabilityBar")?.ViewComponent as XUiV_Sprite;Fill=root.GetChildById("durabilityBar")?.ViewComponent as XUiV_Sprite;Damage=root.GetChildById("durabilityBackground")?.ViewComponent as XUiV_Sprite;}
            else if(root.Children!=null)foreach(var child in root.Children)
            {
                if(child.ViewComponent?.ID=="durability")
                {if(child.ViewComponent is XUiV_FilledSprite fill)Fill=fill;else Track=child.ViewComponent as XUiV_Sprite;}
                if(child.ViewComponent?.ID=="durabilityBackground")Damage=child.ViewComponent as XUiV_Sprite;
            }
        }
    }
    private static readonly ConditionalWeakTable<XUiController,NativeViews> NativeCache=new ConditionalWeakTable<XUiController,NativeViews>();
    private static void CopySprite(XUiV_Sprite target,XUiV_Sprite source,Vector2 from,Vector2 to,float ratio,bool copyState)
    {
        if(target==null||source==null)return;
        Vector2 position=TopLeft(source);
        target.Pivot=UIWidget.Pivot.TopLeft;
        if(target.widget!=null)target.widget.pivot=UIWidget.Pivot.TopLeft;
        target.Position=new Vector2i(Mathf.RoundToInt(to.x+(position.x-from.x)*ratio),Mathf.RoundToInt(to.y+(position.y-from.y)*ratio));
        target.Size=source.Size;target.SpriteName=source.SpriteName;
        if(copyState){target.Fill=source.Fill;target.IsVisible=source.IsVisible;target.SetColorImmediately(source.Color);}
        target.TryUpdatePosition();
        if(target.UiTransform!=null)target.UiTransform.localScale=new Vector3(ratio,ratio,1f);
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
