using System;
using System.Globalization;
using UnityEngine;

#nullable disable

/// <summary>
/// Session-only opacity override for Inventory/Crafting and PC133 Character Overview backgrounds.
/// Never fades a parent panel or edits slots, buttons, entries, navigation or global options.
/// No Update loop, event subscription, retained window instance, file IO or Harmony patch.
/// </summary>
public static class RebirthPersonalCraftingPanelOpacity
{
    public const float DefaultAlpha = 0.98f;
    public const double DefaultPercent = 98d;

    // Explicit whitelist: the PC129 root and nested fills remain clear. Broad matching on
    // "background" would accidentally change interactive controls and compound transparency.
    private static readonly string[] PanelBackgroundIds =
    {
        "rebirthCraftingLeftBackground",
        "rebirthCraftingCenterBackground",
        "rebirthCraftingRightBackground"
    };

    private static readonly string[] CharacterBackgroundIds =
    {
        "survivorCharacterSidebarBg", "survivorOverviewIdentityPanelBg", "survivorOverviewEquipmentPanelBg",
        "survivorOverviewAttributesPanelBg", "survivorOverviewVitalsPanelBg", "survivorOverviewConditionsPanelBg",
        "pc134ProgressionBg", "pc134ConditionBg", "pc134StatisticsBg", "pc134MetabolismBg"
    };
    public static int CharacterTargetCount { get { return CharacterBackgroundIds.Length; } }

    private static double requestedPercent = DefaultPercent;
    public static bool OverrideEnabled { get; private set; }
    public static double Percent { get { return OverrideEnabled ? requestedPercent : DefaultPercent; } }
    public static float Alpha { get { return OverrideEnabled ? (float)(requestedPercent / 100d) : DefaultAlpha; } }
    public static int TargetCount { get { return PanelBackgroundIds.Length; } }

    public static bool TrySetPercent(string text, out string error)
    {
        error = string.Empty;
        string value = (text ?? string.Empty).Trim();
        if (value.EndsWith("%", StringComparison.Ordinal))
            value = value.Substring(0, value.Length - 1).Trim();

        double parsed;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
            double.IsNaN(parsed) || double.IsInfinity(parsed) || parsed < 0d || parsed > 100d)
        {
            error = "Use a finite opacity percentage from 0 to 100 (for example 70 or 72.5). Setting unchanged.";
            return false;
        }

        requestedPercent = parsed;
        OverrideEnabled = true;
        return true;
    }

    public static void ResetToDefault()
    {
        requestedPercent = DefaultPercent;
        OverrideEnabled = false;
    }

    public static void ApplyTo(RebirthCraftingPresentation owner)
    {
        int matched;
        ApplyTo(owner, out matched);
    }

    /// <returns>Number of sprites whose alpha actually changed, not a tree-redraw count.</returns>
    public static int ApplyTo(RebirthCraftingPresentation owner, out int matched)
    {
        return ApplyBackgrounds(owner, PanelBackgroundIds, out matched);
    }

    public static int ApplyCharacter(XUiC_RebirthSurvivorCharacter owner, out int matched)
    {
        return ApplyBackgrounds(owner, CharacterBackgroundIds, out matched);
    }

    private static int ApplyBackgrounds(XUiController owner, string[] ids, out int matched)
    {
        matched = 0;
        if (owner == null || owner.xui == null)
            return 0;

        float alpha = Alpha;
        int changed = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            XUiController child = owner.GetChildById(ids[i]);
            XUiV_Sprite sprite = child != null ? child.ViewComponent as XUiV_Sprite : null;
            if (sprite == null)
                continue;

            matched++;
            Color color = sprite.Color;
            if (color.a == alpha)
                continue;

            // Keep the existing dark-grey RGB. The native Color setter marks only this view
            // dirty for its normal next update; no layout, binding or child-tree refresh.
            color.a = alpha;
            sprite.Color = color;
            changed++;
        }
        return changed;
    }

    public static string DescribeSetting()
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        return "opacity=" + Percent.ToString("0.###", invariant) + "%"
            + " transparency=" + (100d - Percent).ToString("0.###", invariant) + "%"
            + " alpha=" + Alpha.ToString("R", invariant)
            + " source=" + (OverrideEnabled ? "session-override" : "PC131-default-98")
            + " replay=\"rbuiopacity " + (OverrideEnabled ? requestedPercent.ToString("R", invariant) : "reset") + "\"";
    }
}
