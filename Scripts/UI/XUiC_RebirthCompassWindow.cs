using System;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Compact adaptive REBIRTH compass/header.
///
/// Visual order:
///   player level + trader-job progression -> elevation -> compass -> Energy -> optional day -> optional time -> temperature
///
/// The native compass/nav-marker implementation is inherited unchanged. The root remains
/// exactly CompassWidth wide because native waypoint placement derives from ViewComponent.Size.x.
/// Presentation modules outside that native compass area are laid out independently and their
/// widths are calculated from the rendered/localized text.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCompassWindow : XUiC_CompassWindow
{
    private const int CompassWidth = 250;
    private const int SeparatorWidth = 1;
    private const int HeaderHeight = 38;
    private const int HeaderTopY = -10;

    private const int ShellOuterPadding = 4;

    private const int ModulePaddingLeft = 5;
    private const int ModulePaddingRight = 5;
    private const int IconTextGap = 3;

    // Progression module geometry is intentionally content-tight:
    // level ring -> divider -> authored satchel -> localized Tier text.
    private const int LevelDividerX = 39;
    private const int JobIconCenterX = 53;
    private const int JobLabelLeftX = 65;

    private const int JobIconSize = 18;
    private const int ElevationIconSize = 17;
    private const int EnergyIconSize = 18;
    private const int DayIconSize = 18;
    private const int TimeIconSize = 17;
    private const int TemperatureIconWidth = 17;
    private const int TemperatureIconHeight = 19;

    // English/default minima preserve the approved compact proportions. A translated or
    // otherwise longer value is allowed to grow beyond them instead of being clipped/shrunk.
    private const int ProgressionMinWidth = 108;
    private const int ElevationMinWidth = 64;
    private const int EnergyMinWidth = 70;
    private const int DayMinWidth = 70;
    private const int TimeMinWidth = 70;
    private const int TemperatureMinWidth = 72;

    private const int ProgressionMaxWidth = 340;
    private const int GeneralModuleMaxWidth = 260;
    private const float ResponsiveLayoutRefreshSeconds = 0.20f;

    private static readonly PropertyInfo PrintedSizeProperty = typeof(UILabel).GetProperty(
        "printedSize",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private XUiView shellView;

    private XUiView progressionView;
    private XUiView levelSeparatorView;
    private XUiView progressionSeparatorView;
    private XUiView elevationView;
    private XUiView elevationSeparatorView;
    private XUiView compassRightSeparatorView;

    private XUiView jobIconView;
    private XUiV_Label jobLabelView;
    private XUiV_Label elevationLabelView;
    private XUiView elevationIconView;

    private XUiView energyView;
    private XUiView energySeparatorView;
    private XUiView energyIconView;
    private XUiV_Label energyLabelView;

    private XUiView dayView;
    private XUiView daySeparatorView;
    private XUiView sunView;
    private XUiView moonView;
    private XUiV_Label dayLabelView;

    private XUiView timeView;
    private XUiView timeSeparatorView;
    private XUiView timeIconView;
    private XUiV_Label timeLabelView;

    private XUiView temperatureView;
    private XUiView temperatureIconView;
    private XUiV_Label temperatureLabelView;

    private XUiV_Sprite levelRingProgressView;

    private bool layoutInitialized;
    private bool lastShowDay;
    private bool lastShowTime;
    private int lastLayoutSignature = int.MinValue;
    private float responsiveLayoutTimer;
    private Transform responsiveRoot;
    private int viewGeneration;
    private struct MarkerTint { internal Color Before, Applied; }
    private readonly Dictionary<UISprite, MarkerTint> markerTints = new Dictionary<UISprite, MarkerTint>();

    public override void Init()
    {
        base.Init();
        responsiveRoot = null;
        lastLayoutSignature = int.MinValue;
        ResolveResponsiveViews();
        layoutInitialized = false;
        responsiveLayoutTimer = 0f;
    }

    public override void Update(float _dt)
    {
        RestoreCompassMarkers();
        base.Update(_dt);

        // The native controller has just assigned current nav/quest/companion colors. Tone only
        // the rendered compass sprites so authoritative NavObject colors remain untouched.
        DimCompassMarkers();
        UpdateDayNightIcon();

        EntityPlayerLocal player = xui != null && xui.playerUI != null
            ? xui.playerUI.entityPlayer
            : null;

        // Keep the level circle as native player-XP progression.
        ApplyXpFill(levelRingProgressView, GetXpFill(player), true);

        bool showDay = IsTimeDisplayVisible(player);
        bool showTime = showDay;

        responsiveLayoutTimer -= _dt;
        if (!layoutInitialized ||
            showDay != lastShowDay ||
            showTime != lastShowTime ||
            responsiveLayoutTimer <= 0f)
        {
            // Other config fragments (notably Metabolism) append children to this window. Resolve
            // again until those optional views are available, then use actual rendered text sizes.
            ResolveResponsiveViews();
            ApplyResponsiveLayout(showDay, showTime);

            lastShowDay = showDay;
            lastShowTime = showTime;
            layoutInitialized = true;
            responsiveLayoutTimer = ResponsiveLayoutRefreshSeconds;
        }
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        EntityPlayerLocal player = xui != null && xui.playerUI != null
            ? xui.playerUI.entityPlayer
            : null;

        switch (bindingName)
        {
            case "rebirthlevel":
                value = GetLevel(player).ToString(CultureInfo.InvariantCulture);
                return true;

            case "rebirthjobtier":
                value = GetJobTierText(player);
                return true;

            case "mapelevation":
                // REBIRTH 2.6 meaning: signed metres relative to sea level.
                if (player == null || !XUi.IsGameRunning())
                {
                    value = string.Empty;
                    return true;
                }

                int elevation = Mathf.RoundToInt(player.GetPosition().y - WeatherManager.SeaLevel());
                value = elevation.ToString("+0;-#;0", CultureInfo.InvariantCulture);
                return true;

            case "daycolor":
            {
                string nativeDayColor = string.Empty;
                base.GetBindingValueInternal(ref nativeDayColor, bindingName);
                value = string.Equals(nativeDayColor, "FF0000", StringComparison.OrdinalIgnoreCase)
                    ? "9A3838"
                    : "FFFFFF";
                return true;
            }

            default:
                // Preserve native compass/day/time/temperature bindings and existing Harmony
                // binding extensions such as rebirthquestgracevisible/text.
                return base.GetBindingValueInternal(ref value, bindingName);
        }
    }

    private void ResolveResponsiveViews()
    {
        Transform root = ViewComponent != null ? ViewComponent.UiTransform : null;
        bool reset = responsiveRoot != root;
        responsiveRoot = root;
        ResolveResponsiveView("rebirthCompassShell", ref shellView, reset);

        ResolveResponsiveView("rebirthCompassProgression", ref progressionView, reset);
        ResolveResponsiveView("rebirthCompassLevelSeparator", ref levelSeparatorView, reset);
        ResolveResponsiveView("rebirthCompassProgressionSeparator", ref progressionSeparatorView, reset);
        ResolveResponsiveView("rebirthCompassElevation", ref elevationView, reset);
        ResolveResponsiveView("rebirthCompassElevationSeparator", ref elevationSeparatorView, reset);
        ResolveResponsiveView("rebirthCompassRightSeparator", ref compassRightSeparatorView, reset);

        ResolveResponsiveView("rebirthJobTierIcon", ref jobIconView, reset);
        ResolveResponsiveView("rebirthJobTierText", ref jobLabelView, reset);
        ResolveResponsiveView("rebirthCompassElevationIcon", ref elevationIconView, reset);
        ResolveResponsiveView("rebirthCompassElevationText", ref elevationLabelView, reset);

        ResolveResponsiveView("rebirthMetabolismHud", ref energyView, reset);
        ResolveResponsiveView("rebirthCompassEnergySeparator", ref energySeparatorView, reset);
        ResolveResponsiveView("rebirthMetabolismEnergyLightningIcon", ref energyIconView, reset);
        ResolveResponsiveView("rebirthMetabolismEnergyText", ref energyLabelView, reset);

        ResolveResponsiveView("rebirthCompassDay", ref dayView, reset);
        ResolveResponsiveView("rebirthCompassDaySeparator", ref daySeparatorView, reset);
        ResolveResponsiveView("rebirthCompassSun", ref sunView, reset);
        ResolveResponsiveView("rebirthCompassMoon", ref moonView, reset);
        ResolveResponsiveView("rebirthCompassDayText", ref dayLabelView, reset);

        ResolveResponsiveView("rebirthCompassTime", ref timeView, reset);
        ResolveResponsiveView("rebirthCompassTimeSeparator", ref timeSeparatorView, reset);
        ResolveResponsiveView("rebirthCompassClockIcon", ref timeIconView, reset);
        ResolveResponsiveView("rebirthCompassTimeText", ref timeLabelView, reset);

        ResolveResponsiveView("rebirthCompassTemperature", ref temperatureView, reset);
        ResolveResponsiveView("rebirthCompassTempIcon", ref temperatureIconView, reset);
        ResolveResponsiveView("rebirthCompassTemperatureText", ref temperatureLabelView, reset);

        ResolveResponsiveView("rebirthLevelRingProgress", ref levelRingProgressView, reset);
    }

    private void ResolveResponsiveView<T>(string id, ref T view, bool reset) where T : XUiView
    {
        if (!reset && view != null && view.UiTransform != null &&
            (responsiveRoot == null || view.UiTransform.IsChildOf(responsiveRoot))) return;
        T resolved = GetChildById(id)?.ViewComponent as T;
        if (!ReferenceEquals(view, resolved))
        {
            view = resolved;
            viewGeneration++;
            lastLayoutSignature = int.MinValue;
        }
    }

    private XUiView FindView(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent : null;
    }

    private XUiV_Label FindLabelView(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Label : null;
    }

    private XUiV_Sprite FindSpriteView(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Sprite : null;
    }

    /// <summary>
    /// Reflows every text-bearing module using its current rendered/localized text. This handles
    /// language changes, larger day numbers, wider temperatures, Energy values, Roman job tiers,
    /// and future translated labels without hard-coded English widths.
    /// </summary>
    private void ApplyResponsiveLayout(bool showDay, bool showTime)
    {
        int jobTextWidth = MeasureLabelWidth(jobLabelView, EstimateTextWidth(jobLabelView?.Text, jobLabelView?.FontSize ?? 18)) + 6;
        int elevationTextWidth = MeasureLabelWidth(elevationLabelView, 31);
        int energyTextWidth = Mathf.Max(MeasureLabelWidth(energyLabelView, 70), EstimateTextWidth(energyLabelView?.Text, energyLabelView?.FontSize ?? 20) + 10);
        int dayTextWidth = MeasureLabelWidth(dayLabelView, 37);
        int timeTextWidth = MeasureLabelWidth(timeLabelView, 39);
        int temperatureTextWidth = MeasureTemperatureLabelWidth(temperatureLabelView, 34);

        int progressionWidth = Mathf.Clamp(
            JobLabelLeftX + jobTextWidth + ModulePaddingRight,
            ProgressionMinWidth,
            ProgressionMaxWidth);

        int elevationWidth = ComputeIconTextModuleWidth(
            elevationTextWidth,
            ElevationIconSize,
            ElevationMinWidth,
            GeneralModuleMaxWidth);

        int energyWidth = ComputeIconTextModuleWidth(
            energyTextWidth,
            EnergyIconSize,
            EnergyMinWidth,
            GeneralModuleMaxWidth);

        int dayWidth = ComputeIconTextModuleWidth(
            dayTextWidth,
            DayIconSize,
            DayMinWidth,
            GeneralModuleMaxWidth);

        int timeWidth = ComputeIconTextModuleWidth(
            timeTextWidth,
            TimeIconSize,
            TimeMinWidth,
            GeneralModuleMaxWidth);

        int temperatureWidth = ComputeIconTextModuleWidth(
            temperatureTextWidth,
            TemperatureIconWidth,
            TemperatureMinWidth,
            GeneralModuleMaxWidth);

        int signature = progressionWidth ^ viewGeneration;
        signature = signature * 397 ^ elevationWidth;
        signature = signature * 397 ^ energyWidth;
        signature = signature * 397 ^ dayWidth;
        signature = signature * 397 ^ timeWidth;
        signature = signature * 397 ^ temperatureWidth;
        signature = signature * 397 ^ (showDay ? 1 : 0);
        signature = signature * 397 ^ (showTime ? 1 : 0);

        if (signature == lastLayoutSignature)
        {
            // Visibility can still be dirtied by another controller/config fragment, so keep these
            // inexpensive states authoritative even when geometry has not changed.
            ApplyOptionalVisibility(showDay, showTime);
            return;
        }

        lastLayoutSignature = signature;

        // ----- Left side: progression -> elevation -> native compass -----
        int elevationX = -SeparatorWidth - elevationWidth;
        int progressionSeparatorX = elevationX - SeparatorWidth;
        int progressionX = progressionSeparatorX - progressionWidth;
        int shellLeft = progressionX - ShellOuterPadding;

        SetViewGeometry(progressionView, progressionX, 0, progressionWidth, HeaderHeight);
        SetViewPosition(progressionSeparatorView, progressionSeparatorX, -8);

        SetViewGeometry(elevationView, elevationX, 0, elevationWidth, HeaderHeight);
        SetViewPosition(elevationSeparatorView, -SeparatorWidth, -8);
        LayoutIconTextChildren(
            elevationIconView,
            elevationLabelView,
            elevationWidth,
            ElevationIconSize,
            elevationTextWidth,
            -19,
            -21);

        // Player XP and Job Tier are separate visual modules. Keep the XP ring fixed and concentric,
        // then place a divider, the 18px authored satchel, and exactly the measured Tier text.
        SetViewPosition(levelSeparatorView, LevelDividerX, -8);
        int jobLabelWidth = Mathf.Max(1, Math.Min(
            jobTextWidth + 2,
            progressionWidth - JobLabelLeftX - ModulePaddingRight));
        SetViewGeometry(jobIconView, JobIconCenterX, -19, JobIconSize, JobIconSize);
        SetViewGeometry(jobLabelView, JobLabelLeftX + jobLabelWidth / 2, -21, jobLabelWidth, 18);

        // ----- Right side: native compass -> Energy -> Day -> Time -> Temperature -----
        SetViewPosition(compassRightSeparatorView, CompassWidth, -8);

        int x = CompassWidth + SeparatorWidth;

        SetViewGeometry(energyView, x, 0, energyWidth, HeaderHeight);
        LayoutIconTextChildren(
            energyIconView,
            energyLabelView,
            energyWidth,
            EnergyIconSize,
            energyTextWidth,
            -19,
            -21);
        int energySeparatorX = x + energyWidth;
        SetViewPosition(energySeparatorView, energySeparatorX, -8);
        x = energySeparatorX + SeparatorWidth;

        if (showDay)
        {
            SetViewGeometry(dayView, x, 0, dayWidth, HeaderHeight);
            LayoutIconTextChildren(
                sunView,
                dayLabelView,
                dayWidth,
                DayIconSize,
                dayTextWidth,
                -19,
                -21);
            // Moon occupies the same location as the Sun.
            if (sunView != null && moonView != null)
                SetViewGeometry(moonView, sunView.Position.x, -19, DayIconSize, DayIconSize);

            int daySeparatorX = x + dayWidth;
            SetViewPosition(daySeparatorView, daySeparatorX, -8);
            x = daySeparatorX + SeparatorWidth;
        }

        if (showTime)
        {
            SetViewGeometry(timeView, x, 0, timeWidth, HeaderHeight);
            LayoutIconTextChildren(
                timeIconView,
                timeLabelView,
                timeWidth,
                TimeIconSize,
                timeTextWidth,
                -19,
                -21);

            int timeSeparatorX = x + timeWidth;
            SetViewPosition(timeSeparatorView, timeSeparatorX, -8);
            x = timeSeparatorX + SeparatorWidth;
        }

        SetViewGeometry(temperatureView, x, 0, temperatureWidth, HeaderHeight);
        LayoutIconTextChildren(
            temperatureIconView,
            temperatureLabelView,
            temperatureWidth,
            TemperatureIconWidth,
            TemperatureIconHeight,
            temperatureTextWidth,
            -19,
            -21);

        int visualRight = x + temperatureWidth;
        int shellRight = visualRight + ShellOuterPadding;
        LayoutShell(shellLeft, shellRight);

        ApplyOptionalVisibility(showDay, showTime);

        // Re-center the complete variable-width shell while leaving the native compass root width
        // untouched. Native waypoint/nav-object calculations therefore remain based on 250 px.
        if (ViewComponent != null)
        {
            float nativeRootCenter = CompassWidth * 0.5f;
            float visualCenter = (shellLeft + shellRight) * 0.5f;
            int headerOffsetX = Mathf.RoundToInt(nativeRootCenter - visualCenter);
            ViewComponent.Position = new Vector2i(headerOffsetX, HeaderTopY);
        }
    }

    private static int ComputeIconTextModuleWidth(int textWidth, int iconWidth, int minimum, int maximum)
    {
        int desired = ModulePaddingLeft + iconWidth + IconTextGap + textWidth + ModulePaddingRight;
        return Mathf.Clamp(desired, minimum, maximum);
    }

    private static void LayoutIconTextChildren(
        XUiView iconView,
        XUiV_Label labelView,
        int moduleWidth,
        int iconSize,
        int measuredTextWidth,
        int iconY,
        int labelY)
    {
        LayoutIconTextChildren(
            iconView,
            labelView,
            moduleWidth,
            iconSize,
            iconSize,
            measuredTextWidth,
            iconY,
            labelY);
    }

    private static void LayoutIconTextChildren(
        XUiView iconView,
        XUiV_Label labelView,
        int moduleWidth,
        int iconWidth,
        int iconHeight,
        int measuredTextWidth,
        int iconY,
        int labelY)
    {
        int iconCenterX = ModulePaddingLeft + iconWidth / 2;
        int labelLeftX = ModulePaddingLeft + iconWidth + IconTextGap;
        int availableLabelWidth = Math.Max(1, moduleWidth - labelLeftX - ModulePaddingRight);
        int labelWidth = Math.Max(1, Math.Min(availableLabelWidth, measuredTextWidth + 2));

        SetViewGeometry(iconView, iconCenterX, iconY, iconWidth, iconHeight);
        SetViewGeometry(labelView, labelLeftX + labelWidth / 2, labelY, labelWidth, 18);
    }

    private void LayoutShell(int shellLeft, int shellRight)
    {
        // A single 8px-sliced sprite owns the entire header background. This is intentionally
        // one renderable so the rounded ends cannot develop different alpha/gradient profiles
        // from the center when the localized header grows or shrinks.
        int totalWidth = Math.Max(17, shellRight - shellLeft);
        SetViewGeometry(shellView, shellLeft, 0, totalWidth, HeaderHeight);
    }

    private void ApplyOptionalVisibility(bool showDay, bool showTime)
    {
        if (dayView != null)
            dayView.IsVisible = showDay;
        if (daySeparatorView != null)
            daySeparatorView.IsVisible = showDay;

        if (timeView != null)
            timeView.IsVisible = showTime;
        if (timeSeparatorView != null)
            timeSeparatorView.IsVisible = showTime;
    }

    private void UpdateDayNightIcon()
    {
        bool isDaytime = true;

        if (GameManager.Instance != null && GameManager.Instance.World != null)
            isDaytime = GameManager.Instance.World.IsDaytime();

        if (sunView != null)
            sunView.IsVisible = isDaytime;
        if (moonView != null)
            moonView.IsVisible = !isDaytime;
    }

    private static bool IsDayVisible(EntityPlayerLocal player)
    {
        return IsTimeDisplayVisible(player);
    }

    private static bool IsTimeVisible(EntityPlayerLocal player)
    {
        return IsTimeDisplayVisible(player);
    }

    // Profiling showed this being re-evaluated from every Update/binding pass (~370 KB/s of garbage from the effect
    // lookup). The NoTimeDisplay effect only changes with buffs/equipment, so a quarter of a second of latency is invisible.
    private const float TimeDisplayCacheSeconds = 0.25f;
    private static float timeDisplayCheckedAt = -100f;
    private static int timeDisplayPlayerId = int.MinValue;
    private static bool timeDisplayVisible = true;

    private static bool IsTimeDisplayVisible(EntityPlayerLocal player)
    {
        if (player == null)
            return true;

        float now = Time.unscaledTime;
        if (timeDisplayPlayerId == player.entityId && now - timeDisplayCheckedAt < TimeDisplayCacheSeconds && now >= timeDisplayCheckedAt)
            return timeDisplayVisible;

        timeDisplayPlayerId = player.entityId;
        timeDisplayCheckedAt = now;
        timeDisplayVisible = Math.Abs(EffectManager.GetValue(
            PassiveEffects.NoTimeDisplay,
            _entity: (EntityAlive)player)) < 0.0001f;
        return timeDisplayVisible;
    }

    /// <summary>
    /// Temperature must never size itself from an already-clipped UILabel. PC059 could create a
    /// feedback loop where shrinkcontent reduced printedSize, the responsive pass then made the
    /// label even narrower, and the numeric temperature disappeared. Read the native binding
    /// directly and size from that un-clipped value instead.
    /// </summary>
    private int MeasureTemperatureLabelWidth(XUiV_Label labelView, int fallback)
    {
        string nativeText = string.Empty;
        try
        {
            base.GetBindingValueInternal(ref nativeText, "outsidetemp");
        }
        catch
        {
            nativeText = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(nativeText))
        {
            if (labelView != null && labelView.Text != nativeText)
                labelView.Text = nativeText;

            int fontSize = labelView != null ? labelView.FontSize : 15;
            return Mathf.Max(fallback, EstimateTextWidth(nativeText, fontSize) + 4);
        }

        return Mathf.Max(fallback, MeasureLabelWidth(labelView, fallback));
    }

    /// <summary>
    /// Gets the visible text width without assuming English. The live NGUI printedSize is used when
    /// available. We also calculate a conservative Unicode-aware width from the visible string so a
    /// label that was previously constrained by shrinkcontent can still grow after a language change.
    /// </summary>
    private static int MeasureLabelWidth(XUiV_Label labelView, int fallback)
    {
        if (labelView == null)
            return fallback;

        int measured = 0;
        UILabel label = null;
        Transform labelTransform = labelView.UiTransform;
        if ((UnityEngine.Object)labelTransform != (UnityEngine.Object)null)
            label = labelTransform.GetComponent<UILabel>();

        if ((UnityEngine.Object)label != (UnityEngine.Object)null)
        {
            if (PrintedSizeProperty != null)
            {
                try
                {
                    object raw = PrintedSizeProperty.GetValue(label, null);
                    if (raw is Vector2 printed && !float.IsNaN(printed.x) && !float.IsInfinity(printed.x))
                        measured = Mathf.Max(measured, Mathf.CeilToInt(printed.x));
                }
                catch
                {
                    // Runtime font measurement is an optimization. The string-based path below is
                    // deliberately sufficient on builds where NGUI hides/renames printedSize.
                }
            }

            // printedSize is authoritative when NGUI exposes it. Only estimate when the runtime
            // does not provide a usable printed width; taking the max of both made short labels
            // such as "TIER I 0/10" unnecessarily wide.
            if (measured <= 0)
                measured = EstimateTextWidth(label.text, labelView.FontSize);
        }
        else
        {
            measured = EstimateTextWidth(labelView.Text, labelView.FontSize);
        }

        // A fallback is only for the pre-binding/initialization frame. Once NGUI has text, use
        // the actual localized width rather than treating the English fallback as a permanent minimum.
        return measured > 0 ? measured : Mathf.Max(1, fallback);
    }

    private static int EstimateTextWidth(string rawText, int fontSize)
    {
        string text = StripBbCode(rawText);
        if (string.IsNullOrEmpty(text))
            return 0;

        float size = Math.Max(8, fontSize);
        float width = 0f;

        for (int i = 0; i < text.Length; ++i)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                width += size * 0.34f;
            }
            else if (c >= '\u2E80')
            {
                // CJK/full-width glyphs are normally close to an em square.
                width += size * 1.02f;
            }
            else if (char.IsDigit(c))
            {
                width += size * 0.57f;
            }
            else if (char.IsUpper(c))
            {
                width += size * 0.62f;
            }
            else if (char.IsLower(c))
            {
                width += size * 0.54f;
            }
            else if (c == '/' || c == ':' || c == '.' || c == ',' || c == '-' || c == '+' || c == '°')
            {
                width += size * 0.38f;
            }
            else
            {
                width += size * 0.56f;
            }
        }

        // XUi/NGUI labels commonly use one pixel of character spacing in this HUD font family.
        if (text.Length > 1)
            width += text.Length - 1;

        return Mathf.CeilToInt(width);
    }

    private static string StripBbCode(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf('[') < 0)
            return value ?? string.Empty;

        StringBuilder sb = new StringBuilder(value.Length);
        bool insideTag = false;

        for (int i = 0; i < value.Length; ++i)
        {
            char c = value[i];
            if (!insideTag && c == '[')
            {
                insideTag = true;
                continue;
            }

            if (insideTag)
            {
                if (c == ']')
                    insideTag = false;
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static void SetViewPosition(XUiView view, int x, int y)
    {
        if (view == null)
            return;

        Vector2i target = new Vector2i(x, y);
        if (view.Position != target)
            view.Position = target;
    }

    private static void SetViewGeometry(XUiView view, int x, int y, int width, int height)
    {
        if (view == null)
            return;

        Vector2i targetPosition = new Vector2i(x, y);
        Vector2i targetSize = new Vector2i(Math.Max(1, width), Math.Max(1, height));

        if (view.Position != targetPosition)
            view.Position = targetPosition;
        if (view.Size != targetSize)
            view.Size = targetSize;
    }

    private static void ApplyXpFill(XUiV_Sprite view, float fill, bool radial)
    {
        if (view == null)
            return;

        fill = Mathf.Clamp01(fill);

        view.Type = UIBasicSprite.Type.Filled;
        view.FillDirection = radial
            ? UIBasicSprite.FillDirection.Radial360
            : UIBasicSprite.FillDirection.Horizontal;
        view.FillInvert = false;
        view.Fill = fill;

        UISprite sprite = view.Sprite;
        if ((UnityEngine.Object)sprite == (UnityEngine.Object)null)
            return;

        sprite.type = UIBasicSprite.Type.Filled;
        sprite.fillDirection = radial
            ? UIBasicSprite.FillDirection.Radial360
            : UIBasicSprite.FillDirection.Horizontal;
        sprite.invert = false;
        sprite.fillAmount = fill;
    }

    public override void OnClose()
    {
        RestoreCompassMarkers();
        base.OnClose();
    }

    private void RestoreCompassMarkers()
    {
        foreach (KeyValuePair<UISprite, MarkerTint> entry in markerTints)
            if (entry.Key != null && entry.Key.color.Equals(entry.Value.Applied)) entry.Key.color = entry.Value.Before;
        markerTints.Clear();
    }

    private void DimCompassMarkers()
    {
        if (waypointSpriteList == null)
            return;

        const float rgbScale = 0.92f;
        const float alphaScale = 0.98f;

        for (int i = 0; i < waypointSpriteList.Count; i++)
        {
            UISprite sprite = waypointSpriteList[i];
            if ((UnityEngine.Object)sprite == (UnityEngine.Object)null)
                continue;

            Color c = sprite.color;
            if (c.a <= 0.001f)
                continue;

            Color applied = new Color(c.r * rgbScale, c.g * rgbScale, c.b * rgbScale, c.a * alphaScale);
            markerTints[sprite] = new MarkerTint { Before = c, Applied = applied };
            if (!sprite.color.Equals(applied)) sprite.color = applied;
        }
    }

    private static int GetLevel(EntityPlayerLocal player)
    {
        Progression progression = player != null ? player.Progression : null;
        return progression != null ? progression.Level : 0;
    }

    /// <summary>
    /// Compact global trader-job progression matching REBIRTH 2.6 tier math and presentation:
    /// Roman tier numerals and the completed-job value highlighted in REBIRTH UI purple (#B58CFF).
    /// </summary>
    private static string GetJobTierText(EntityPlayerLocal player)
    {
        if(RebirthPurgeReleasePolicy.Enabled&&RebirthSandboxOptionManager.Current.IsPurge)return RebirthPurgeHudProgress.Text(player);
        QuestJournal journal = player != null ? player.QuestJournal : null;

        int jobsToNextTier = RebirthTraderJobPolicy.JobsToNextTier;
        if (jobsToNextTier <= 0)
            jobsToNextTier = RebirthTraderJobPolicy.DefaultJobsToNextTier;

        if (journal == null)
            return FormatJobTierProgress(1, 0, jobsToNextTier);

        int questPoints = 0;
        for (byte factionId = 1; factionId <= 5; ++factionId)
            questPoints = Math.Max(questPoints, journal.GetQuestFactionPoints(factionId));

        questPoints = Math.Max(0, questPoints);
        int maxTier = Mathf.Clamp(Quest.MaxQuestTier, 1, 6);
        int questTier = 1;
        int remaining = questPoints;

        // Same cumulative weighted-tier calculation used by REBIRTH 2.6.
        for (int tier = 1; tier < 100; ++tier)
        {
            remaining -= tier * jobsToNextTier;
            if (remaining < 0)
            {
                questTier = Math.Min(tier, maxTier);
                break;
            }

            if (tier >= maxTier)
            {
                questTier = maxTier;
                break;
            }
        }

        if (questTier >= maxTier)
            return FormatJobTierMaximum(questTier);

        int tierPoints = 0;
        for (int tier = 1; tier <= questTier; ++tier)
            tierPoints += tier * jobsToNextTier;

        int pointsLeft = Math.Max(0, tierPoints - questPoints);
        int jobsLeft = pointsLeft / Math.Max(1, questTier);

        if (pointsLeft > 0 && pointsLeft < questTier)
            jobsLeft = 1;

        int completed = Mathf.Clamp(jobsToNextTier - jobsLeft, 0, jobsToNextTier);
        return FormatJobTierProgress(questTier, completed, jobsToNextTier);
    }

    private static string FormatJobTierProgress(int tier, int completed, int total)
    {
        string template = Localization.Get("xuiRebirthCompassJobTier");
        if (string.IsNullOrEmpty(template) || template == "xuiRebirthCompassJobTier")
            template = "TIER {0} [B58CFF]{1}[-]/{2}";

        return string.Format(
            CultureInfo.InvariantCulture,
            template,
            ValueDisplayFormatters.RomanNumber(Mathf.Clamp(tier, 1, 6)),
            completed.ToString(CultureInfo.InvariantCulture),
            total.ToString(CultureInfo.InvariantCulture));
    }

    private static string FormatJobTierMaximum(int tier)
    {
        string template = Localization.Get("xuiRebirthCompassJobTierMax");
        if (string.IsNullOrEmpty(template) || template == "xuiRebirthCompassJobTierMax")
            template = "TIER {0} MAX";

        return string.Format(
            CultureInfo.InvariantCulture,
            template,
            ValueDisplayFormatters.RomanNumber(Mathf.Clamp(tier, 1, 6)));
    }

    private static float GetXpFill(EntityPlayerLocal player)
    {
        Progression progression = player != null ? player.Progression : null;
        if (progression == null)
            return 0f;

        if (progression.Level >= Progression.MaxLevel)
            return 1f;

        return Mathf.Clamp01(progression.GetLevelProgressPercentage());
    }
}
