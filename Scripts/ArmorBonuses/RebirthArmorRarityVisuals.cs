using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 armor bonus slot visuals (RebirthUtilities.GetHighestRarity / RarityToColor, XUiC_ItemStack_Enchanting_Update,
/// owner 2026-11-08):
/// - an item with bonuses tints its slot with the colour of its highest bonus rarity, in every window;
/// - in an encumbered (attribute-locked) slot the slot keeps its locked colour and a white-outlined star in the rarity
///   colour is shown instead;
/// - equipment slots and quest reward entries take the same tint.
/// </summary>
public static class RebirthArmorRarityVisuals
{
    // 2.6 constants (all26.cs L63174-63180).
    private static readonly Color32[] Slot =
    {
        new Color32(90, 90, 95, 255),   // Common
        new Color32(45, 65, 120, 255),  // Uncommon
        new Color32(120, 80, 35, 255),  // Rare
        new Color32(90, 45, 130, 255),  // Legendary
    };

    public static bool TryColor(ItemValue item, out Color32 color)
    {
        color = default(Color32);
        var bonuses = item == null || item.IsEmpty() ? null : RebirthArmorBonusStorage.Read(item);
        if (bonuses == null || bonuses.Length == 0) return false;
        int highest = bonuses.Max(b => (int)b.Definition.Rarity);
        color = Slot[Mathf.Clamp(highest, 0, Slot.Length - 1)];
        return true;
    }

    private sealed class SlotState
    {
        internal bool Saved, OverlayResolved; internal Color Background; internal UISprite Star, Outline; internal XUiV_Sprite Overlay;
    }
    private static readonly ConditionalWeakTable<XUiController, SlotState> States = new ConditionalWeakTable<XUiController, SlotState>();

    /// <summary>Returns the fill colour to use for this slot (rarity tint) or null to keep the slot's own colour;
    /// also shows or hides the encumbered-slot star.</summary>
    public static Color32? Fill(XUiC_ItemStack slot)
    {
        if (slot == null) return null;
        // Cheap exit for the common case: no item, or an item without bonus metadata and no star to hide.
        var value = slot.ItemStack?.itemValue;
        if ((value == null || value.Metadata == null) && !(States.TryGetValue(slot, out var known) && known.Star != null && known.Star.enabled)) return null;
        bool has = TryColor(slot.ItemStack?.itemValue, out var color);
        bool locked = slot.AttributeLock;
        ShowStar(slot, has && locked, color);
        return has && !locked ? color : (Color32?)null;
    }

    /// <summary>Native slots (toolbelt, station grids, loot, trader player grid): 2.6 ApplySlotBackground.</summary>
    internal static void ApplyNative(XUiC_ItemStack slot)
    {
        var state = States.GetValue(slot, _ => new SlotState());
        var tint = Fill(slot);
        if (!state.OverlayResolved) { state.Overlay = slot.GetChildById("highlightOverlay")?.ViewComponent as XUiV_Sprite; state.OverlayResolved = true; }
        var overlay = state.Overlay;
        if (tint.HasValue)
        {
            if (!state.Saved) { state.Background = slot.backgroundColor; state.Saved = true; }
            if (slot.backgroundColor != (Color)tint.Value) slot.backgroundColor = tint.Value;
            if (overlay != null && overlay.Color != (Color)tint.Value) overlay.Color = tint.Value;
        }
        else if (state.Saved)
        {
            slot.backgroundColor = state.Background;
            state.Saved = false;
            slot.IsDirty = true;
        }
    }

    private static void ShowStar(XUiController slot, bool show, Color32 color)
    {
        var state = States.GetValue(slot, _ => new SlotState());
        if (!show)
        {
            if (state.Star != null && state.Star.enabled) { state.Star.enabled = false; state.Outline.enabled = false; }
            return;
        }
        if (state.Star == null && !CreateStar(slot, state)) return;
        state.Outline.enabled = true; state.Star.enabled = true;
        if (state.Star.color != (Color)color) state.Star.color = color;
    }

    private static bool CreateStar(XUiController slot, SlotState state)
    {
        var parent = slot.ViewComponent?.UiTransform;
        var reference = (slot.GetChildById("highlightOverlay")?.ViewComponent as XUiV_Sprite)?.Sprite
            ?? (slot.GetChildById("backgroundMain")?.ViewComponent as XUiV_Sprite)?.Sprite;
        if (parent == null || reference == null || reference.atlas == null) return false;
        state.Outline = MakeSprite(parent, reference, "rbRarityStarOutline", 22, reference.depth + 30, Color.white);
        state.Star = MakeSprite(parent, reference, "rbRarityStar", 16, reference.depth + 31, Color.white);
        return true;
    }

    private static UISprite MakeSprite(Transform parent, UISprite reference, string name, int size, int depth, Color color)
    {
        var go = new GameObject(name) { layer = parent.gameObject.layer };
        go.transform.SetParent(parent, false);
        // Top-left corner of the slot, like the 2.6 star.
        go.transform.localPosition = new Vector3(14f, -14f, 0f);
        var sprite = go.AddComponent<UISprite>();
        sprite.atlas = reference.atlas;
        sprite.spriteName = "ui_game_symbol_star";
        sprite.pivot = UIWidget.Pivot.Center;
        sprite.width = size; sprite.height = size;
        sprite.depth = depth;
        sprite.color = color;
        return sprite;
    }
}

[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.Update))]
internal static class RebirthArmorRaritySlotPatch
{
    private static void Postfix(XUiC_ItemStack __instance)
    {
        if (__instance?.ViewComponent == null || !__instance.ViewComponent.IsVisible) return;
        RebirthArmorRarityVisuals.ApplyNative(__instance);
    }
}

[HarmonyPatch(typeof(XUiC_EquipmentStack), nameof(XUiC_EquipmentStack.Update))]
internal static class RebirthArmorRarityEquipmentPatch
{
    // 2.6 XUiC_EquipmentStack_Enchanting_Update: only the highlight overlay is tinted on equipment slots.
    private static void Postfix(XUiC_EquipmentStack __instance)
    {
        var overlay = __instance?.GetChildById("highlightOverlay")?.ViewComponent as XUiV_Sprite;
        if (overlay == null) return;
        if (RebirthArmorRarityVisuals.TryColor(__instance.itemValue, out var color)) { if (overlay.Color != (Color)color) overlay.Color = color; }
    }
}

[HarmonyPatch]
internal static class RebirthArmorRarityQuestRewardPatch
{
    // 2.6 Patch_XUiC_QuestTurnInEntry_RarityBackground: unselected reward entries with bonuses get a rarity background.
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(XUiC_QuestTurnInEntry), nameof(XUiC_QuestTurnInEntry.RefreshBackground));
        yield return AccessTools.Method(typeof(XUiC_QuestTurnInEntry), nameof(XUiC_QuestTurnInEntry.OnHovered));
    }
    private static void Postfix(XUiC_QuestTurnInEntry __instance)
    {
        if (__instance?.background == null || __instance.IsSelected) return;
        if (!RebirthArmorRarityVisuals.TryColor(__instance.Item?.itemValue, out var color)) return;
        __instance.background.SpriteName = "menu_empty3px";
        __instance.background.Color = color;
    }
}

[Preserve]
public sealed class RebirthArmorRarityVisualsModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        var harmony = new Harmony("rebirth.armor-rarity-visuals");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthArmorRaritySlotPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthArmorRarityEquipmentPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthArmorRarityQuestRewardPatch));
    }
}
