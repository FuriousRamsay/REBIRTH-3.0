using HarmonyLib;
using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Routes only Rebirth-owned radial sprites that are not present in the base UIAtlas
/// to RebirthUiIcons. This does not replace or recolor any base-game radial artwork.
/// The Quick Stack and Drone Lock-to-Player PNGs are the exact user-supplied legacy
/// assets; Quick Stack Categories is the accepted checklist icon with black pixels
/// converted to transparency.
/// </summary>
internal static class RebirthRadialIconAtlasPatch
{
    public const string LegacyQuickStack = "ui_game_symbol_quickstack";
    public const string LegacyQuickStackMine = "ui_game_symbol_quickstack_mine";
    public const string LegacyDroneLockToPlayer = "ui_game_symbol_dronelocktoplayer";
    public const string QuickStackCategories = "ui_game_symbol_quickstack_categories";
}

[HarmonyPatch(typeof(XUiC_Radial), nameof(XUiC_Radial.CreateRadialEntry),
    new Type[] { typeof(int), typeof(string), typeof(Color), typeof(string), typeof(string), typeof(string), typeof(bool) })]
internal static class RebirthQuickStackCategoriesRadialAtlasPatch
{
    private static void Prefix(string _icon, ref string _atlas)
    {
        if (string.Equals(_icon, RebirthRadialIconAtlasPatch.QuickStackCategories, StringComparison.Ordinal))
            _atlas = "RebirthUiIcons";
    }
}
