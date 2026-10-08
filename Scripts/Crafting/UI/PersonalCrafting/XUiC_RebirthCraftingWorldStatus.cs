using System;
using System.Globalization;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Compact V3.2 Personal Crafting world-status projection. The strip deliberately mirrors the
/// proven Rebirth compass language: day icon + day, clock + time, temperature icon + value.
/// Biome text is intentionally omitted so the three useful values can remain grouped at right.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingWorldStatus : XUiController
{
    private const float RefreshSeconds = 0.20f;
    private XUiC_RebirthPersonalCrafting personalOwner;

    private XUiV_Label dayLabel;
    private XUiV_Label timeLabel;
    private XUiV_Label temperatureLabel;
    private XUiController sunIcon;
    private XUiController moonIcon;
    private XUiController clockIcon;
    private XUiController temperatureIcon;
    private float refreshTimer;

    public override void Init()
    {
        base.Init();
        personalOwner = GetParentByType<XUiC_RebirthPersonalCrafting>();
        dayLabel = Label("rebirthCraftingStatusDay");
        timeLabel = Label("rebirthCraftingStatusTime");
        temperatureLabel = Label("rebirthCraftingStatusTemperature");
        sunIcon = GetChildById("rebirthCraftingStatusSun");
        moonIcon = GetChildById("rebirthCraftingStatusMoon");
        clockIcon = GetChildById("rebirthCraftingStatusClock");
        temperatureIcon = GetChildById("rebirthCraftingStatusTemperatureIcon");
        RefreshNow();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        refreshTimer = 0f;
        RefreshNow();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (personalOwner != null && !personalOwner.State.IsOpen) return;
        refreshTimer -= Math.Max(0f, dt);
        if (refreshTimer > 0f)
            return;
        refreshTimer = RefreshSeconds;
        RefreshNow();
    }

    private void RefreshNow()
    {
        EntityPlayerLocal player = xui != null && xui.playerUI != null
            ? xui.playerUI.entityPlayer
            : null;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;

        if (!XUi.IsGameRunning() || world == null)
        {
            Set(dayLabel, string.Empty);
            Set(timeLabel, string.Empty);
            Set(temperatureLabel, string.Empty);
            SetVisible(sunIcon, false);
            SetVisible(moonIcon, false);
            SetVisible(clockIcon, false);
            SetVisible(temperatureIcon, false);
            return;
        }

        ulong worldTime = world.worldTime;
        bool showClock = IsTimeDisplayVisible(player);
        int day = GameUtils.WorldTimeToDays(worldTime);
        (int _, int hour, int minute) = GameUtils.WorldTimeToElements(worldTime);

        Set(dayLabel, showClock
            ? (Localization.Get("xuiDay") + " " + day.ToString(CultureInfo.InvariantCulture)).ToUpperInvariant()
            : string.Empty);
        Set(timeLabel, showClock
            ? string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", hour, minute)
            : string.Empty);

        bool isDay = world.IsDaytime();
        SetVisible(sunIcon, showClock && isDay);
        SetVisible(moonIcon, showClock && !isDay);
        SetVisible(clockIcon, showClock);
        SetVisible(temperatureIcon, true);
        Set(temperatureLabel, player != null ? XUiM_Player.GetOutsideTemp((EntityPlayer)player) : string.Empty);
    }

    private static bool IsTimeDisplayVisible(EntityPlayerLocal player)
    {
        if (player == null)
            return true;
        return Math.Abs(EffectManager.GetValue(
            PassiveEffects.NoTimeDisplay,
            _entity: (EntityAlive)player)) < 0.0001f;
    }

    private XUiV_Label Label(string id)
    {
        XUiController controller = GetChildById(id);
        return controller != null ? controller.ViewComponent as XUiV_Label : null;
    }

    private static void Set(XUiV_Label label, string value)
    {
        if (label != null)
            label.Text = value ?? string.Empty;
    }

    private static void SetVisible(XUiController controller, bool visible)
    {
        if (controller != null && controller.ViewComponent != null)
            controller.ViewComponent.IsVisible = visible;
    }
}
