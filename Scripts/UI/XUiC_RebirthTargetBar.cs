using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// REBIRTH presentation layer for the native 3.1 target bar.
///
/// Target acquisition, fade behaviour, boss support and authoritative health bindings
/// stay in XUiC_TargetBar. This controller only adds an existing target icon (when the
/// entity already supplies one) plus a compact, centered row of HUD-visible target buffs.
/// It deliberately does not synthesize portraits or substitute generic entity icons.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthTargetBar : XUiC_TargetBar
{
    private const int MaxBuffCards = 9;
    private const int TargetVisualCenterX = 125;
    private const int BuffCardWidth = 40;
    private const int BuffCardGap = 4;
    private const int BuffCardY = -58;
    private const float BuffRefreshInterval = 0.20f;

    private readonly XUiView[] buffCardViews = new XUiView[MaxBuffCards];
    private readonly string[] buffIcons = new string[MaxBuffCards];
    private readonly string[] buffTexts = new string[MaxBuffCards];
    private readonly string[] buffColors = new string[MaxBuffCards];
    private readonly List<BuffValue> visibleBuffs = new List<BuffValue>(MaxBuffCards);

    private EntityAlive lastPresentedTarget;
    private float nextBuffRefreshTime;
    private int visibleBuffCount;
    private int lastLayoutCount = -1;
    private bool targetHasIcon;
    private string targetIconAtlas = string.Empty;
    private string targetIcon = string.Empty;

    public override void Init()
    {
        base.Init();

        for (int i = 0; i < MaxBuffCards; i++)
        {
            XUiController card = GetChildById("rebirthTargetBuff" + i);
            buffCardViews[i] = card != null ? card.ViewComponent : null;
            if (buffCardViews[i] != null)
                buffCardViews[i].IsVisible = false;

            buffIcons[i] = string.Empty;
            buffTexts[i] = string.Empty;
            buffColors[i] = "255,255,255,255";
        }

        lastLayoutCount = -1;
        nextBuffRefreshTime = 0f;
        visibleBuffCount = 0;
        targetHasIcon = false;
    }

    public override void Update(float _dt)
    {
        // Keep the base-game target selection/fade/boss implementation authoritative.
        base.Update(_dt);

        EntityAlive target = Target;
        bool targetChanged = !ReferenceEquals(target, lastPresentedTarget);
        float now = Time.realtimeSinceStartup;

        if (!targetChanged && now < nextBuffRefreshTime)
            return;

        lastPresentedTarget = target;
        nextBuffRefreshTime = now + BuffRefreshInterval;

        targetHasIcon = ResolveTargetIcon(target, out targetIconAtlas, out targetIcon);
        RefreshBuffSnapshot(target);
        LayoutBuffCards();

        // Our icon/buff bindings are not known to the native target-bar dirtiness
        // calculation, so refresh them at a modest 5 Hz while a target is displayed.
        RefreshBindings();
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        switch (bindingName)
        {
            case "rebirthtargetitemiconvisible":
                value = targetHasIcon && string.Equals(targetIconAtlas, "ItemIconAtlas", StringComparison.Ordinal) ? "true" : "false";
                return true;

            case "rebirthtargetuiiconvisible":
                value = targetHasIcon && string.Equals(targetIconAtlas, "UIAtlas", StringComparison.Ordinal) ? "true" : "false";
                return true;

            case "rebirthtargetitemicon":
                value = targetHasIcon && string.Equals(targetIconAtlas, "ItemIconAtlas", StringComparison.Ordinal) ? targetIcon : string.Empty;
                return true;

            case "rebirthtargetuiicon":
                value = targetHasIcon && string.Equals(targetIconAtlas, "UIAtlas", StringComparison.Ordinal) ? targetIcon : string.Empty;
                return true;

            case "rebirthtargetnamecolor":
            {
                RebirthSandboxOptionManager manager = RebirthSandboxOptionManager.Current;
                value = manager.TargetNameColorR + "," + manager.TargetNameColorG + "," + manager.TargetNameColorB + ",255";
                return true;
            }

            case "name":
            {
                // Native 3.1 localizes an entity-class name for non-players. REBIRTH
                // companions have persistent custom names, so show those names directly.
                EntityAlive target = Target;
                if ((UnityEngine.Object)target != (UnityEngine.Object)null)
                {
                    EntityDrone drone = target as EntityDrone;
                    if (drone != null)
                    {
                        value = RebirthDroneRenameService.GetDisplayName(drone);
                        return true;
                    }

                    if (target is EntityRebirthDogCompanion || target is EntityRebirthNPC)
                    {
                        string customName = target.EntityName;
                        if (!string.IsNullOrWhiteSpace(customName))
                        {
                            value = customName.Trim();
                            return true;
                        }
                    }
                }

                return base.GetBindingValueInternal(ref value, bindingName);
            }
        }

        if (TryParseBuffBinding(bindingName, out int index, out string field))
        {
            if (index < 0 || index >= MaxBuffCards)
                return false;

            switch (field)
            {
                case "icon":
                    value = buffIcons[index] ?? string.Empty;
                    return true;
                case "text":
                    value = buffTexts[index] ?? string.Empty;
                    return true;
                case "color":
                    value = buffColors[index] ?? "255,255,255,255";
                    return true;
                case "visible":
                    value = index < visibleBuffCount ? "true" : "false";
                    return true;
            }
        }

        return base.GetBindingValueInternal(ref value, bindingName);
    }

    private void RefreshBuffSnapshot(EntityAlive target)
    {
        visibleBuffs.Clear();

        for (int i = 0; i < MaxBuffCards; i++)
        {
            buffIcons[i] = string.Empty;
            buffTexts[i] = string.Empty;
            buffColors[i] = "255,255,255,255";
        }

        if ((UnityEngine.Object)target == (UnityEngine.Object)null || target.Buffs == null || target.Buffs.ActiveBuffs == null)
        {
            visibleBuffCount = 0;
            return;
        }

        List<BuffValue> active = target.Buffs.ActiveBuffs;
        for (int i = 0; i < active.Count && visibleBuffs.Count < MaxBuffCards; i++)
        {
            BuffValue buff = active[i];
            if (buff == null || buff.Remove || buff.Finished || buff.Invalid || buff.Paused)
                continue;

            BuffClass buffClass = buff.BuffClass;
            if (buffClass == null || buffClass.Hidden || !buffClass.ShowOnHUD || string.IsNullOrEmpty(buffClass.Icon))
                continue;

            visibleBuffs.Add(buff);
        }

        visibleBuffCount = visibleBuffs.Count;
        for (int i = 0; i < visibleBuffCount; i++)
        {
            BuffValue buff = visibleBuffs[i];
            BuffClass buffClass = buff.BuffClass;

            buffIcons[i] = buffClass.Icon ?? string.Empty;
            buffTexts[i] = GetBuffCardText(target, buff);
            buffColors[i] = ToXuiColor(buffClass.IconColor);
        }
    }

    private static string GetBuffCardText(EntityAlive target, BuffValue buff)
    {
        if (buff == null || buff.BuffClass == null)
            return string.Empty;

        // Normal finite-duration buffs use the same compact time-left formatter as
        // the native HUD (for example 8S, 2M, 1H).
        string timeText = XUiM_PlayerBuffs.GetBuffTimeLeftString(buff);
        if (!string.IsNullOrEmpty(timeText))
            return timeText;

        // Some important buffs are intentionally infinite and drive their visible
        // countdown from a CVar instead. Vanilla bleeding is one of these:
        // duration=0 + display_value=$bleedDuration + display_value_format=time.
        // Respect that authored display contract so the target card still shows its
        // countdown even though BuffClass.DurationMax is zero.
        BuffClass buffClass = buff.BuffClass;
        if ((UnityEngine.Object)target != (UnityEngine.Object)null &&
            target.Buffs != null &&
            !string.IsNullOrEmpty(buffClass.DisplayValueCVar))
        {
            float displayValue = target.Buffs.GetCustomVar(buffClass.DisplayValueCVar);
            if (buffClass.DisplayValueFormat == BuffClass.CVarDisplayFormat.Time)
            {
                string cvarTime = XUiM_PlayerBuffs.GetCVarValueAsTimeString(displayValue);
                if (!string.IsNullOrEmpty(cvarTime))
                    return cvarTime;
            }
            else if (displayValue != 0f)
            {
                switch (buffClass.DisplayValueFormat)
                {
                    case BuffClass.CVarDisplayFormat.Float:
                        return displayValue.ToString("0.##");
                    case BuffClass.CVarDisplayFormat.FlooredToInt:
                        return Mathf.FloorToInt(displayValue).ToString();
                    case BuffClass.CVarDisplayFormat.RoundedToInt:
                        return Mathf.RoundToInt(displayValue).ToString();
                    case BuffClass.CVarDisplayFormat.CeiledToInt:
                        return Mathf.CeilToInt(displayValue).ToString();
                    case BuffClass.CVarDisplayFormat.Percentage:
                        return Mathf.RoundToInt(displayValue * 100f) + "%";
                    default:
                        return Mathf.RoundToInt(displayValue).ToString();
                }
            }
        }

        if (buff.StackEffectMultiplier > 1)
            return "x" + buff.StackEffectMultiplier;

        // Permanent/passive target buffs can legitimately have no duration/value.
        // Leave their footer clean rather than inventing a number that is not part
        // of the authoritative buff data.
        return string.Empty;
    }

    private void LayoutBuffCards()
    {
        int count = visibleBuffCount;
        for (int i = 0; i < MaxBuffCards; i++)
        {
            if (buffCardViews[i]?.UiTransform != null) continue;
            XUiView resolved = GetChildById("rebirthTargetBuff" + i)?.ViewComponent;
            if (!ReferenceEquals(resolved, buffCardViews[i])) { buffCardViews[i] = resolved; lastLayoutCount = -1; }
        }
        if (count == lastLayoutCount) return;
        lastLayoutCount = count;
        int totalWidth = count > 0 ? count * BuffCardWidth + (count - 1) * BuffCardGap : 0;
        int startX = TargetVisualCenterX - totalWidth / 2;

        for (int i = 0; i < MaxBuffCards; i++)
        {
            XUiView view = buffCardViews[i];
            if (view == null)
                continue;

            bool visible = i < count;
            if (view.IsVisible != visible) view.IsVisible = visible;
            if (visible)
                view.Position = new Vector2i(startX + i * (BuffCardWidth + BuffCardGap), BuffCardY);
        }
    }

    /// <summary>
    /// Uses only icons the target already owns. No generic substitute and no generated
    /// portrait is supplied. An entity without an authored map/item icon simply has no
    /// target-bar icon, which keeps custom zombies from displaying misleading artwork.
    /// </summary>
    private static bool ResolveTargetIcon(EntityAlive target, out string atlas, out string icon)
    {
        atlas = string.Empty;
        icon = string.Empty;

        if ((UnityEngine.Object)target == (UnityEngine.Object)null)
            return false;

        EntityDrone drone = target as EntityDrone;
        if (drone != null)
        {
            ItemClass droneItem = ItemClass.GetItemClass("gunBotT3JunkDrone", false);
            string droneIcon = droneItem != null ? droneItem.GetIconName() : string.Empty;
            if (!string.IsNullOrEmpty(droneIcon))
            {
                atlas = "ItemIconAtlas";
                icon = droneIcon;
                return true;
            }
        }

        EntityRebirthDogCompanion dog = target as EntityRebirthDogCompanion;
        if (dog != null)
        {
            RebirthNpcRuntimeState state = dog.RebirthRuntimeState;
            RebirthDogBreedDefinition breed;
            if (state != null && !state.StableId.IsEmpty &&
                RebirthDogStateService.TryGetBreed(state.StableId, out breed) &&
                breed != null && !string.IsNullOrEmpty(breed.IconName))
            {
                atlas = "ItemIconAtlas";
                icon = breed.IconName;
                return true;
            }
        }

        string mapIcon = target.GetMapIcon();
        if (!string.IsNullOrEmpty(mapIcon))
        {
            atlas = "UIAtlas";
            icon = mapIcon;
            return true;
        }

        return false;
    }

    private static bool TryParseBuffBinding(string bindingName, out int index, out string field)
    {
        index = -1;
        field = null;

        const string prefix = "rebirthbuff";
        if (string.IsNullOrEmpty(bindingName) || !bindingName.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        int digitIndex = prefix.Length;
        if (digitIndex >= bindingName.Length || bindingName[digitIndex] < '0' || bindingName[digitIndex] > '9')
            return false;

        index = bindingName[digitIndex] - '0';
        field = bindingName.Substring(digitIndex + 1);
        return true;
    }

    private static string ToXuiColor(Color color)
    {
        Color32 c = (Color32)color;
        if (c.a == 0)
            c = new Color32(255, 255, 255, 255);
        return c.r + "," + c.g + "," + c.b + "," + c.a;
    }
}
