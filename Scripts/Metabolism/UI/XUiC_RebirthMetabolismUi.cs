using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Minimal play-HUD metabolism companion.
///
/// Stomach and intestine remain fully tracked and available from Character ->
/// Metabolism, but they are intentionally absent from the in-world HUD. Energy is
/// presented in the compass/header as a compact Concept-E readout showing current/max,
/// live net change per real minute, and the current recovery/exertion state.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthMetabolismHud : XUiController
{
    private int lastRevision = -1;
    private float nextRefresh;
    private RebirthMetabolismSnapshot snapshot;
    private bool hasSnapshot;
    private int lastColorRevision = -1;
    private bool energyButtonHooked;
    private XUiController energyButton;
    private XUiV_Sprite energyIcon;
    private XUiV_Sprite energyAccent;
    private XUiV_Label energyRateLabel;
    private XUiV_Label energyStateLabel;

    public override void Init()
    {
        base.Init();
        ResolveEnergyViews();
        HookEnergyButton();
        ApplyEnergyAccent(true);
        RefreshSnapshot(true);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ResolveEnergyViews();
        HookEnergyButton();
        ApplyEnergyAccent(true);
        RefreshSnapshot(true);
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (Time.realtimeSinceStartup < nextRefresh)
            return;

        nextRefresh = Time.realtimeSinceStartup + 0.25f;
        RebirthJournalGuideService.Poll(xui?.playerUI?.entityPlayer);
        ApplyEnergyAccent(false);
        RefreshSnapshot(false);
    }

    private void RefreshSnapshot(bool force)
    {
        RebirthMetabolismSnapshot next;
        bool found = RebirthMetabolismClientState.TryGet(out next);
        if (!found && xui != null && xui.playerUI != null &&
            xui.playerUI.entityPlayer != null && RebirthMetabolismService.IsServerAuthority)
        {
            next = RebirthMetabolismService.BuildSnapshot(xui.playerUI.entityPlayer);
            found = true;
        }

        if (!found)
            return;
        if (!force && hasSnapshot && next.Revision == lastRevision)
            return;

        snapshot = next;
        hasSnapshot = true;
        lastRevision = next.Revision;
        IsDirty = true;
        RefreshBindings();
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        switch (bindingName)
        {
            case "rbmet_hud_energy_current":
                value = Math.Round(snapshot.Energy).ToString("0");
                return true;
            case "rbmet_hud_energy_max":
                value = Math.Round(snapshot.EnergyMax).ToString("0");
                return true;
            case "rbmet_hud_energy_current_with_max":
                value = Math.Round(snapshot.Energy).ToString("0") + "/" + Math.Round(snapshot.EnergyMax).ToString("0");
                return true;
            case "rbmet_hud_energy_net":
            {
                float net = EnergyNet(snapshot);
                value = (net >= 0f ? "+" : string.Empty) + net.ToString("0.00") + "/m";
                return true;
            }
            case "rbmet_hud_energy_state":
                value = BuildEnergyState(snapshot);
                return true;
            case "rbmet_hud_energy_fill":
                value = Fill(snapshot.Energy, snapshot.EnergyMax);
                return true;
            case "rbmet_hud_energy_capacity_fill":
                value = Fill(EnergyCeiling(snapshot), snapshot.EnergyMax);
                return true;
            case "rbmet_hud_energy_tooltip":
                value = BuildEnergyTooltip(snapshot);
                return true;
            default:
                return base.GetBindingValueInternal(ref value, bindingName);
        }
    }


    private void ResolveEnergyViews()
    {
        energyButton = GetChildById("btnRebirthMetabolismEnergyInfo");
        energyIcon = GetSpriteView("rebirthMetabolismEnergyIcon");
        energyAccent = GetSpriteView("rebirthMetabolismEnergyAccent");
        energyRateLabel = GetLabelView("rebirthMetabolismEnergyRate");
        energyStateLabel = GetLabelView("rebirthMetabolismEnergyState");
    }

    private void HookEnergyButton()
    {
        if (energyButtonHooked || energyButton == null)
            return;

        energyButton.OnPress += new XUiEvent_OnPressEventHandler(EnergyButton_OnPress);
        energyButtonHooked = true;
    }

    private void EnergyButton_OnPress(XUiController _sender, int _mouseButton)
    {
        XUiC_RebirthVitalColorPicker.Open(xui, RebirthVitalHudKind.Energy);
    }

    private void ApplyEnergyAccent(bool force)
    {
        int revision = RebirthVitalHudColors.Revision;
        if (!force && revision == lastColorRevision)
            return;

        lastColorRevision = revision;
        Color32 color = RebirthVitalHudColors.Get(RebirthVitalHudKind.Energy);
        if (energyIcon != null) energyIcon.Color = color;
        if (energyAccent != null) energyAccent.Color = color;
        if (energyRateLabel != null) energyRateLabel.Color = color;
        if (energyStateLabel != null) energyStateLabel.Color = color;
    }

    private XUiV_Sprite GetSpriteView(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Sprite : null;
    }

    private XUiV_Label GetLabelView(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Label : null;
    }

    private static float EnergyNet(RebirthMetabolismSnapshot s)
    {
        return s.EnergyRecoveryPerRealMinute + s.BeverageEnergyGainPerRealMinute - s.EnergyUsePerRealMinute;
    }

    private static string BuildEnergyState(RebirthMetabolismSnapshot s)
    {
        int staminaPercent = Mathf.Clamp(Mathf.RoundToInt(s.StaminaRecoveryMultiplier * 100f), 0, 100);
        if (s.Energy <= RebirthMetabolismConfig.EnergyCriticalThreshold)
            return Localization.Get("xuiRebirthEnergyCritical") + " • " + staminaPercent.ToString("0") + "% " + Localization.Get("xuiRebirthEnergyStaminaRecovery");
        if (s.Energy < RebirthMetabolismConfig.EnergyLowThreshold)
            return Localization.Get("xuiRebirthEnergyFatigue") + " • " + staminaPercent.ToString("0") + "% " + Localization.Get("xuiRebirthEnergyStaminaRecovery");

        float net = EnergyNet(s);
        if (net > 0.05f && s.EnergyUsePerRealMinute <= 0.05f)
            return Localization.Get("xuiRebirthEnergyIdleRecovery");
        if (net > 0.05f)
            return Localization.Get("xuiRebirthEnergyRecovering");
        if (net < -0.05f)
            return Localization.Get("xuiRebirthEnergyExertion");
        return Localization.Get("xuiRebirthEnergyStable");
    }

    private static float EnergyCeiling(RebirthMetabolismSnapshot s)
    {
        float hydration = s.HydrationMax > 0.001f ? Clamp01(s.Hydration / s.HydrationMax) : 0f;
        float nutrition = s.FoodMax > 0.001f ? Clamp01(s.Food / s.FoodMax) : 0f;
        return RebirthMetabolismConfig.GetEnergyReserveCeiling(hydration, nutrition);
    }

    private static string BuildEnergyTooltip(RebirthMetabolismSnapshot s)
    {
        float net = s.EnergyRecoveryPerRealMinute + s.BeverageEnergyGainPerRealMinute - s.EnergyUsePerRealMinute;
        string rate = (net >= 0f ? "+" : string.Empty) + net.ToString("0.00");
        float recoveryNutritionPerMinute = s.EnergyRecoveryNutritionUsePerRealMinute;
        float ceiling = EnergyCeiling(s);

        return "Energy is the reserve that powers sustained physical work and controls Stamina recovery."
            + "\nSustainable cap: " + ceiling.ToString("0.0") + " / " + s.EnergyMax.ToString("0") + ". The black part of the bar is currently unavailable Energy capacity."
            + "\nGray is recoverable missing Energy; the colored portion is current Energy."
            + "\nNutrition supplies Energy; Hydration changes how efficiently that Nutrition can support it."
            + "\nCurrent activity use: -" + s.EnergyUsePerRealMinute.ToString("0.00") + " / real min"
            + "\nRest recovery: +" + s.EnergyRecoveryPerRealMinute.ToString("0.00") + " / real min"
            + (s.BeverageEnergyGainPerRealMinute > 0.005f ? "\nBeverage Energy absorbed now: +" + s.BeverageEnergyGainPerRealMinute.ToString("0.00") + " / real min" : string.Empty)
            + "\nNet: " + rate + " / real min"
            + "\nCurrent recovery Nutrition cost: -" + recoveryNutritionPerMinute.ToString("0.00") + " / real min"
            + "\nThe sustainable cap never falls below " + RebirthMetabolismConfig.EnergyMinimumSustainableCap.ToString("0") + ", but activity can still spend current Energy to 0."
            + "\nCurrent Hydration Energy efficiency: " + Math.Round(s.EnergyRecoveryHydrationMultiplier * 100f).ToString("0") + "%"
            + "\nRebuilding 1 Energy costs " + RebirthMetabolismConfig.FoodUnitsPerEnergyRecovered.ToString("0.##") + " Nutrition."
            + (s.PendingEnergy > 0.05f ? "\nBeverage Energy incoming: +" + s.PendingEnergy.ToString("0.0") + " after intestinal absorption." : string.Empty)
            + "\nStamina recovery begins to weaken below " + RebirthMetabolismConfig.EnergyLowThreshold.ToString("0") + " Energy.";
    }

    private static string Fill(float value, float max)
    {
        return (max > 0f ? Clamp01(value / max) : 0f)
            .ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static float Clamp01(float value)
    {
        return Math.Max(0f, Math.Min(1f, value));
    }
}

/// <summary>
/// Detailed metabolism status and hydration-equipment window.
/// All mutations are routed through the server-authoritative service/network packages.
/// </summary>
[Preserve]
public class XUiC_RebirthMetabolismWindow : XUiController
{
    public const string WindowGroupName = "rebirthMetabolism";

    private int lastRevision = -1;
    private float nextRefresh;
    private RebirthMetabolismSnapshot snapshot;
    private bool hasSnapshot;

    public override void Init()
    {
        base.Init();
        Hook("btnRebirthMetabolismClose", Close_OnPress);
        Hook("btnRebirthMetabolismEquipHeld", EquipHeld_OnPress);
        Hook("btnRebirthMetabolismUnequip", Unequip_OnPress);
        Hook("btnRebirthMetabolismSip", Sip_OnPress);
        Hook("btnRebirthMetabolismAutoSip", AutoSip_OnPress);
        Hook("btnRebirthMetabolismInformation", Information_OnPress);
    }

    private void Hook(string name, XUiEvent_OnPressEventHandler handler)
    {
        XUiController control = GetChildById(name);
        if (control != null)
            control.OnPress += handler;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (GetParentByType<XUiC_RebirthSurvivorCharacter>() == null && windowGroup != null)
            windowGroup.isEscClosable = true;
        RefreshSnapshot(true);
    }

    public override void Update(float dt)
    {
        var character = GetParentByType<XUiC_RebirthSurvivorCharacter>();
        if (character != null && (!character.IsCharacterWindowOpen || ViewComponent?.UiTransform == null || !ViewComponent.UiTransform.gameObject.activeInHierarchy)) return;
        base.Update(dt);
        if (Time.realtimeSinceStartup >= nextRefresh)
        {
            nextRefresh = Time.realtimeSinceStartup + 0.20f;
            RefreshSnapshot(false);
            string message = RebirthMetabolismUiFeedback.CurrentMessage;
            if (!string.IsNullOrEmpty(message))
            {
                IsDirty = true;
                RefreshBindings();
            }
        }

        if (GetParentByType<XUiC_RebirthSurvivorCharacter>() == null && XUiUtils.HotkeysAllowedFor(viewComponent) &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased || xui.playerUI.playerInput.GUIActions.Cancel.WasReleased))
            CloseWindow();
    }

    public static void Open(XUi targetXui)
    {
        if (targetXui == null || targetXui.playerUI == null)
            return;
        XUiController group = targetXui.FindWindowGroupByName(WindowGroupName);
        XUiC_RebirthMetabolismWindow controller = group != null ? group.GetChildByType<XUiC_RebirthMetabolismWindow>() : null;
        if (controller == null)
        {
            Log.Error("[REBIRTH Metabolism] XUi controller not found.");
            return;
        }
        targetXui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, false);
    }

    private void Close_OnPress(XUiController sender, int mouseButton) { CloseWindow(); }
    private void EquipHeld_OnPress(XUiController sender, int mouseButton) { RequestSlot(RebirthHydrationSlotOperation.EquipHeld); }
    private void Unequip_OnPress(XUiController sender, int mouseButton) { RequestSlot(RebirthHydrationSlotOperation.Unequip); }
    private void Sip_OnPress(XUiController sender, int mouseButton) { RequestSlot(RebirthHydrationSlotOperation.ManualSip); }
    private void AutoSip_OnPress(XUiController sender, int mouseButton) { RequestSlot(RebirthHydrationSlotOperation.ToggleAutoSip); }
    private void Information_OnPress(XUiController sender, int mouseButton) { XUiC_RebirthMetabolismInformationWindow.Open(xui); }

    private void CloseWindow()
    {
        if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null && windowGroup != null)
            xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }

    private void RequestSlot(RebirthHydrationSlotOperation operation)
    {
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (player == null)
            return;

        if (RebirthMetabolismService.IsServerAuthority)
        {
            bool success = false;
            string message = string.Empty;
            switch (operation)
            {
                case RebirthHydrationSlotOperation.EquipHeld:
                    success = RebirthMetabolismService.EquipHeldHydrationContainer(player, out message);
                    break;
                case RebirthHydrationSlotOperation.Unequip:
                    success = RebirthMetabolismService.UnequipHydrationContainer(player, out message);
                    break;
                case RebirthHydrationSlotOperation.ToggleAutoSip:
                    RebirthMetabolismService.ToggleAutoSip(player);
                    success = true;
                    message = "Auto-sip setting changed.";
                    break;
                case RebirthHydrationSlotOperation.ManualSip:
                    RebirthConsumeResult consume = RebirthMetabolismService.ConsumeHydrationSlot(player, false);
                    success = consume.Success;
                    message = consume.Message;
                    break;
            }
            RebirthMetabolismUiFeedback.Receive(new RebirthConsumeResult
            {
                Success = success,
                Failure = success ? RebirthMetabolismConsumeFailure.None : RebirthMetabolismConsumeFailure.SlotMismatch,
                Message = message
            });
            RebirthMetabolismService.SendSnapshotToOwner(player, true);
            RefreshSnapshot(true);
            return;
        }

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection != null)
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthHydrationSlotRequest>().Setup(player.entityId, operation));
    }

    private void RefreshSnapshot(bool force)
    {
        RebirthMetabolismSnapshot next;
        bool found = RebirthMetabolismClientState.TryGet(out next);
        if (!found && xui != null && xui.playerUI != null && xui.playerUI.entityPlayer != null && RebirthMetabolismService.IsServerAuthority)
        {
            next = RebirthMetabolismService.BuildSnapshot(xui.playerUI.entityPlayer);
            found = true;
        }
        if (!found)
            return;
        if (!force && hasSnapshot && next.Revision == lastRevision)
            return;
        snapshot = next;
        hasSnapshot = true;
        lastRevision = next.Revision;
        IsDirty = true;
        RefreshBindings();
    }

    private static string Pct(float value) { return Math.Round(value * 100f).ToString("0") + "%"; }
    private static string Num(float value) { return Math.Round(value).ToString("0"); }
    private static string One(float value) { return value.ToString("0.0"); }
    private static string Signed(float value, string format) { return (value >= 0f ? "+" : string.Empty) + value.ToString(format); }
    private static string FormatLongDuration(float seconds)
    {
        int total = Math.Max(0, (int)Math.Ceiling(seconds));
        return (total / 60).ToString("0") + ":" + (total % 60).ToString("00");
    }

    private static float EnergyCeiling(RebirthMetabolismSnapshot s)
    {
        float hydration = s.HydrationMax > 0.001f ? Math.Max(0f, Math.Min(1f, s.Hydration / s.HydrationMax)) : 0f;
        float nutrition = s.FoodMax > 0.001f ? Math.Max(0f, Math.Min(1f, s.Food / s.FoodMax)) : 0f;
        return RebirthMetabolismConfig.GetEnergyReserveCeiling(hydration, nutrition);
    }

    private static string EnergyRestrictionFill(RebirthMetabolismSnapshot s)
    {
        if (s.EnergyMax <= 0.001f) return "0";
        float restricted = Math.Max(0f, Math.Min(1f, 1f - EnergyCeiling(s) / s.EnergyMax));
        return restricted.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        switch (bindingName)
        {
            case "rbmet_hydration": value = One(snapshot.Hydration) + " / " + Num(snapshot.HydrationMax); return true;
            case "rbmet_hydration_fill": value = (snapshot.HydrationMax > 0f ? Math.Max(0f, Math.Min(1f, snapshot.Hydration / snapshot.HydrationMax)) : 0f).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture); return true;
            case "rbmet_hydration_pending": value = "+" + One(snapshot.PendingHydration); return true;
            case "rbmet_hydration_projected": value = One(snapshot.ProjectedHydration) + " / " + Num(snapshot.HydrationMax); return true;
            case "rbmet_hydration_net": value = Signed(snapshot.HydrationNetPointsPerRealMinute, "0.00") + " / real min"; return true;
            case "rbmet_hydration_loss": value = One(snapshot.HydrationLossMlPerReal60Minutes) + " mL / 60 real min"; return true;
            case "rbmet_fluid_absorption": value = One(snapshot.FluidAbsorptionMlPerRealMinute) + " mL / real min"; return true;
            case "rbmet_hydration_requirement": value = Pct(snapshot.HydrationRequirementMultiplier); return true;
            case "rbmet_fluid_absorption_mult": value = Pct(snapshot.FluidAbsorptionMultiplier); return true;
            case "rbmet_fluid_utilization": value = Pct(snapshot.FluidUtilizationMultiplier); return true;

            case "rbmet_food": value = One(snapshot.Food) + " / " + Num(snapshot.FoodMax); return true;
            case "rbmet_food_fill": value = (snapshot.FoodMax > 0f ? Math.Max(0f, Math.Min(1f, snapshot.Food / snapshot.FoodMax)) : 0f).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture); return true;
            case "rbmet_food_pending": value = "+" + One(snapshot.PendingFood); return true;
            case "rbmet_food_projected": value = One(snapshot.ProjectedFood) + " / " + Num(snapshot.FoodMax); return true;
            case "rbmet_nutrition_net": value = Signed(snapshot.NutritionNetPointsPerRealMinute, "0.00") + " / real min"; return true;
            case "rbmet_food_use": value = One(snapshot.FoodUsePerReal60Minutes) + " / 60 real min"; return true;
            case "rbmet_energy": value = Num(snapshot.Energy) + " / " + Num(EnergyCeiling(snapshot)) + " sustainable cap"; return true;
            case "rbmet_energy_fill": value = (snapshot.EnergyMax > 0f ? Math.Max(0f, Math.Min(1f, snapshot.Energy / snapshot.EnergyMax)) : 0f).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture); return true;
            case "rbmet_energy_capacity_fill": value = (snapshot.EnergyMax > 0f ? Math.Max(0f, Math.Min(1f, EnergyCeiling(snapshot) / snapshot.EnergyMax)) : 0f).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture); return true;
            case "rbmet_energy_restriction_fill": value = EnergyRestrictionFill(snapshot); return true;
            case "rbmet_energy_color":
                if (snapshot.Energy <= RebirthMetabolismConfig.EnergyCriticalThreshold) value = "205,75,65,255";
                else if (snapshot.Energy <= RebirthMetabolismConfig.EnergyLowThreshold) value = "220,170,65,255";
                else value = "210,190,75,255";
                return true;
            case "rbmet_energy_bar_text": value = "ENERGY  " + Num(snapshot.Energy) + " / " + Num(EnergyCeiling(snapshot)) + (snapshot.PendingEnergy >= 0.5f ? "  (+" + One(snapshot.PendingEnergy) + ")" : string.Empty) + "   •   STAMINA REGEN " + Pct(snapshot.StaminaRecoveryMultiplier); return true;
            case "rbmet_energy_use": value = One(snapshot.EnergyUsePerRealMinute) + " / real min"; return true;
            case "rbmet_energy_recovery": value = One(snapshot.EnergyRecoveryPerRealMinute) + " / real min"; return true;
            case "rbmet_energy_net":
                float netEnergy = snapshot.EnergyRecoveryPerRealMinute + snapshot.BeverageEnergyGainPerRealMinute - snapshot.EnergyUsePerRealMinute;
                float recoveryNutrition = snapshot.EnergyRecoveryNutritionUsePerRealMinute;
                value = "Energy -" + One(snapshot.EnergyUsePerRealMinute) + " + " + One(snapshot.EnergyRecoveryPerRealMinute) + " = " + (netEnergy >= 0f ? "+" : string.Empty) + One(netEnergy) + "/min   •   Nutrition -" + One(recoveryNutrition) + "/min";
                return true;
            case "rbmet_energy_eta":
                float net = snapshot.EnergyRecoveryPerRealMinute + snapshot.BeverageEnergyGainPerRealMinute - snapshot.EnergyUsePerRealMinute;
                float cap = EnergyCeiling(snapshot);
                if (net < -0.01f) value = One(snapshot.Energy / -net) + " real min to empty at this rate";
                else if (net > 0.01f && snapshot.Energy < cap - 0.1f) value = One((cap - snapshot.Energy) / net) + " real min to sustainable cap";
                else value = snapshot.Energy >= cap - 0.1f ? "At sustainable Energy cap" : "Energy stable at this activity";
                return true;
            case "rbmet_energy_hydration_eff": value = Pct(snapshot.EnergyRecoveryHydrationMultiplier); return true;
            case "rbmet_stamina_recovery": value = Pct(snapshot.StaminaRecoveryMultiplier); return true;
            case "rbmet_food_requirement": value = Pct(snapshot.FoodRequirementMultiplier); return true;
            case "rbmet_nutrient_utilization": value = Pct(snapshot.NutrientUtilizationMultiplier); return true;

            case "rbmet_fullness": value = Num(snapshot.FullnessMl) + " / " + Num(snapshot.StomachCapacityMl) + " mL (" + Pct(snapshot.FullnessPercent) + ")"; return true;
            case "rbmet_stomach_fill": value = Math.Max(0f, Math.Min(1f, snapshot.FullnessPercent)).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture); return true;
            case "rbmet_stomach_bar_text": value = "STOMACH  " + Num(snapshot.FullnessMl) + " / " + Num(snapshot.StomachCapacityMl) + " mL   •   LIQUID " + Num(snapshot.StomachFluidMl) + "   •   SOLID " + Num(snapshot.StomachSolidMl); return true;
            case "rbmet_intestine_fill": value = Math.Max(0f, Math.Min(1f, snapshot.IntestinalContentPercent)).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture); return true;
            case "rbmet_intestine_bar_text": value = "INTESTINE  " + Num(snapshot.IntestinalFluidMl + snapshot.IntestinalSolidMl) + " / " + Num(snapshot.IntestinalCapacityMl) + " mL   •   LIQUID " + Num(snapshot.IntestinalFluidMl) + "   •   SOLID " + Num(snapshot.IntestinalSolidMl); return true;
            case "rbmet_stomach_fluid": value = Num(snapshot.StomachFluidMl) + " mL"; return true;
            case "rbmet_stomach_food": value = Num(snapshot.StomachSolidMl) + " mL"; return true;
            case "rbmet_gastric_fluid_transfer": value = One(snapshot.GastricFluidTransferMlPerRealMinute) + " mL / real min"; return true;
            case "rbmet_gastric_solid_transfer": value = One(snapshot.GastricSolidTransferMlPerRealMinute) + " mL / real min"; return true;
            case "rbmet_digestive_health": value = Num(snapshot.DigestiveHealth) + " / 100"; return true;
            case "rbmet_digestive_health_fill": value = Math.Max(0f, Math.Min(1f, snapshot.DigestiveHealth / 100f)).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture); return true;
            case "rbmet_digestion_rate": value = One(snapshot.SolidGastricEmptyingMlPerRealMinute) + " mL / real min"; return true;
            case "rbmet_gastric_phase": value = snapshot.GastricHoldSecondsRemaining > 0.5f ? "Holding " + FormatLongDuration(snapshot.GastricHoldSecondsRemaining) : (snapshot.FullnessMl > 0.5f ? "Emptying into intestine" : "Stomach empty"); return true;
            case "rbmet_fluid_gastric_phase": value = snapshot.StomachFluidMl <= 0.5f ? "Empty" : (snapshot.FluidGastricHoldSecondsRemaining > 0.5f ? "Holding " + FormatLongDuration(snapshot.FluidGastricHoldSecondsRemaining) : "Emptying into intestine"); return true;
            case "rbmet_food_gastric_phase": value = snapshot.StomachSolidMl <= 0.5f ? "Empty" : (snapshot.SolidGastricHoldSecondsRemaining > 0.5f ? "Holding " + FormatLongDuration(snapshot.SolidGastricHoldSecondsRemaining) : "Emptying into intestine"); return true;
            case "rbmet_gastric_emptying": value = One(snapshot.GastricEmptyingMlPerRealMinute) + " mL / real min"; return true;
            case "rbmet_intestinal_content": value = Num(snapshot.IntestinalFluidMl + snapshot.IntestinalSolidMl) + " / " + Num(snapshot.IntestinalCapacityMl) + " mL"; return true;
            case "rbmet_intestinal_fluid": value = One(snapshot.IntestinalFluidMl) + " mL"; return true;
            case "rbmet_intestinal_solid": value = One(snapshot.IntestinalSolidMl) + " mL"; return true;
            case "rbmet_intestinal_fluid_phase": value = snapshot.IntestinalFluidMl <= 0.05f ? "Empty" : (snapshot.IntestinalFluidResidenceSecondsRemaining > 0.5f ? "Residing " + FormatLongDuration(snapshot.IntestinalFluidResidenceSecondsRemaining) : "Absorbing"); return true;
            case "rbmet_intestinal_nutrition_phase": value = snapshot.IntestinalSolidMl <= 0.05f && snapshot.IntestinalNutritionUnits <= 0.01f ? "Empty" : (snapshot.IntestinalNutritionResidenceSecondsRemaining > 0.5f ? "Residing " + FormatLongDuration(snapshot.IntestinalNutritionResidenceSecondsRemaining) : "Absorbing"); return true;
            case "rbmet_fluid_arriving": value = One(snapshot.GastricFluidTransferMlPerRealMinute) + " mL / real min"; return true;
            case "rbmet_fluid_absorbed_now": value = One(snapshot.FluidAbsorbedMlPerRealMinute) + " mL / real min"; return true;
            case "rbmet_hydration_gain_now": value = "+" + snapshot.HydrationGainPointsPerRealMinute.ToString("0.00") + " points / real min"; return true;
            case "rbmet_intestinal_nutrition": value = One(snapshot.IntestinalNutritionUnits) + " units"; return true;
            case "rbmet_nutrition_absorbed_now": value = One(snapshot.NutritionAbsorbedUnitsPerRealMinute) + " units / real min"; return true;
            case "rbmet_nutrition_gain_now": value = "+" + One(snapshot.NutritionGainPointsPerRealMinute) + " reserve / real min"; return true;
            case "rbmet_intestinal_energy": value = snapshot.IntestinalEnergyUnits > 0.05f ? "+" + One(snapshot.IntestinalEnergyUnits) : "0"; return true;
            case "rbmet_energy_pending": value = snapshot.PendingEnergy > 0.05f ? "+" + One(snapshot.PendingEnergy) : "0"; return true;
            case "rbmet_nutrient_absorption": value = One(snapshot.NutrientAbsorptionUnitsPerRealMinute) + " units / real min"; return true;
            case "rbmet_digestion_speed": value = Pct(snapshot.DigestionSpeedMultiplier); return true;
            case "rbmet_gut_resilience": value = Pct(snapshot.GutResilienceMultiplier); return true;
            case "rbmet_process_status":
                if (snapshot.FullnessPercent >= 1f) value = Localization.Get("xuiRebirthMetabolismStatusStomachFull");
                else if (snapshot.FluidGastricHoldSecondsRemaining > 0.5f || snapshot.SolidGastricHoldSecondsRemaining > 0.5f) value = Localization.Get("xuiRebirthMetabolismStatusStomachHold");
                else if (snapshot.GastricFluidTransferMlPerRealMinute > 0.05f || snapshot.GastricSolidTransferMlPerRealMinute > 0.05f) value = Localization.Get("xuiRebirthMetabolismStatusGastricTransfer");
                else if (snapshot.IntestinalFluidResidenceSecondsRemaining > 0.5f || snapshot.IntestinalNutritionResidenceSecondsRemaining > 0.5f) value = Localization.Get("xuiRebirthMetabolismStatusIntestinalResidence");
                else if (snapshot.FluidAbsorbedMlPerRealMinute > 0.05f || snapshot.NutritionAbsorbedUnitsPerRealMinute > 0.005f) value = Localization.Get("xuiRebirthMetabolismStatusAbsorbing");
                else if (snapshot.FullnessMl > 0.5f || snapshot.IntestinalFluidMl + snapshot.IntestinalSolidMl > 0.05f) value = Localization.Get("xuiRebirthMetabolismStatusProcessing");
                else value = Localization.Get("xuiRebirthMetabolismStatusEmpty");
                return true;

            // Compact Character-tab bindings. These deliberately avoid exposing implementation
            // bookkeeping; they describe the visible stomach -> intestine -> body process.
            case "rbmet_intake_name":
                value = string.IsNullOrEmpty(snapshot.ActiveIntakeItemName)
                    ? "No active intake"
                    : Localization.Get(snapshot.ActiveIntakeItemName);
                return true;
            case "rbmet_intake_icon":
                value = string.IsNullOrEmpty(snapshot.ActiveIntakeItemName) ? "ui_game_symbol_fork" : snapshot.ActiveIntakeItemName;
                return true;
            case "rbmet_intake_item_visible":
                value = string.IsNullOrEmpty(snapshot.ActiveIntakeItemName) ? "false" : "true";
                return true;
            case "rbmet_intake_empty_visible":
                value = string.IsNullOrEmpty(snapshot.ActiveIntakeItemName) ? "true" : "false";
                return true;
            case "rbmet_intake_amount":
                value = snapshot.ActiveIntakeRemainingMl > 0.05f ? One(snapshot.ActiveIntakeRemainingMl) + " mL still processing" : "--";
                return true;
            case "rbmet_intake_summary":
                if (snapshot.PendingHydration > 0.05f && snapshot.PendingFood > 0.05f)
                    value = "H +" + One(snapshot.PendingHydration) + "  •  N +" + One(snapshot.PendingFood);
                else if (snapshot.PendingHydration > 0.05f)
                    value = "Hydration +" + One(snapshot.PendingHydration);
                else if (snapshot.PendingFood > 0.05f)
                    value = "Nutrition +" + One(snapshot.PendingFood);
                else
                    value = "No reserve pending";
                return true;
            case "rbmet_intake_pending":
                value = "Stomach " + Num(snapshot.FullnessMl) + " mL  •  Gut " + Num(snapshot.IntestinalFluidMl + snapshot.IntestinalSolidMl) + " mL";
                return true;
            case "rbmet_stomach_liquid_state":
                string stomachLiquidStatus;
                string stomachLiquidTime = string.Empty;
                if (snapshot.StomachFluidMl <= 0.5f)
                    stomachLiquidStatus = Localization.Get("xuiRebirthMetabolismEmpty");
                else if (snapshot.FluidGastricHoldSecondsRemaining > 0.5f)
                {
                    stomachLiquidStatus = Localization.Get("xuiRebirthMetabolismHolding");
                    stomachLiquidTime = " " + FormatLongDuration(snapshot.FluidGastricHoldSecondsRemaining);
                }
                else
                    stomachLiquidStatus = Localization.Get("xuiRebirthMetabolismMoving");
                value = "[87CDF5]" + Localization.Get("xuiRebirthMetabolismLiquid") + ":[-] "
                    + Num(snapshot.StomachFluidMl) + " mL  ([E8C75A]" + stomachLiquidStatus + "[-]" + stomachLiquidTime;
                if (snapshot.StomachFluidMl > 0.5f && snapshot.GastricFluidTransferMlPerRealMinute > 0.05f)
                    value += "  •  " + One(snapshot.GastricFluidTransferMlPerRealMinute) + " mL/min";
                value += ")";
                return true;
            case "rbmet_stomach_liquid_rate":
                value = "→ " + Localization.Get("xuiRebirthMetabolismIntestine") + ": " + One(snapshot.GastricFluidTransferMlPerRealMinute) + " mL/min";
                return true;
            case "rbmet_stomach_solid_state":
                string stomachSolidStatus;
                string stomachSolidTime = string.Empty;
                if (snapshot.StomachSolidMl <= 0.5f)
                    stomachSolidStatus = Localization.Get("xuiRebirthMetabolismEmpty");
                else if (snapshot.SolidGastricHoldSecondsRemaining > 0.5f)
                {
                    stomachSolidStatus = Localization.Get("xuiRebirthMetabolismHolding");
                    stomachSolidTime = " " + FormatLongDuration(snapshot.SolidGastricHoldSecondsRemaining);
                }
                else
                    stomachSolidStatus = Localization.Get("xuiRebirthMetabolismMoving");
                value = "[E1B955]" + Localization.Get("xuiRebirthMetabolismSolid") + ":[-] "
                    + Num(snapshot.StomachSolidMl) + " mL  ([E8C75A]" + stomachSolidStatus + "[-]" + stomachSolidTime;
                if (snapshot.StomachSolidMl > 0.5f && snapshot.GastricSolidTransferMlPerRealMinute > 0.05f)
                    value += "  •  " + One(snapshot.GastricSolidTransferMlPerRealMinute) + " mL/min";
                value += ")";
                return true;
            case "rbmet_stomach_solid_rate":
                value = "→ " + Localization.Get("xuiRebirthMetabolismIntestine") + ": " + One(snapshot.GastricSolidTransferMlPerRealMinute) + " mL/min";
                return true;
            case "rbmet_intestine_liquid_state":
                string intestineLiquidStatus;
                string intestineLiquidTime = string.Empty;
                if (snapshot.IntestinalFluidMl <= 0.05f)
                    intestineLiquidStatus = Localization.Get("xuiRebirthMetabolismEmpty");
                else if (snapshot.IntestinalFluidResidenceSecondsRemaining > 0.5f)
                {
                    intestineLiquidStatus = Localization.Get("xuiRebirthMetabolismResiding");
                    intestineLiquidTime = " " + FormatLongDuration(snapshot.IntestinalFluidResidenceSecondsRemaining);
                }
                else
                    intestineLiquidStatus = Localization.Get("xuiRebirthMetabolismAbsorbing");
                value = "[87CDF5]" + Localization.Get("xuiRebirthMetabolismLiquid") + ":[-] "
                    + One(snapshot.IntestinalFluidMl) + " mL  ([E8C75A]" + intestineLiquidStatus + "[-]" + intestineLiquidTime;
                if (snapshot.IntestinalFluidMl > 0.05f && snapshot.HydrationGainPointsPerRealMinute > 0.005f)
                    value += "  •  +" + One(snapshot.HydrationGainPointsPerRealMinute) + "/min";
                value += ")";
                return true;
            case "rbmet_intestine_liquid_rate":
                value = "→ " + Localization.Get("xuiRebirthMetabolismHydration") + ": +" + One(snapshot.HydrationGainPointsPerRealMinute) + "/min";
                return true;
            case "rbmet_intestine_solid_state":
                string intestineSolidStatus;
                string intestineSolidTime = string.Empty;
                if (snapshot.IntestinalSolidMl <= 0.05f && snapshot.IntestinalNutritionUnits <= 0.01f)
                    intestineSolidStatus = Localization.Get("xuiRebirthMetabolismEmpty");
                else if (snapshot.IntestinalNutritionResidenceSecondsRemaining > 0.5f)
                {
                    intestineSolidStatus = Localization.Get("xuiRebirthMetabolismResiding");
                    intestineSolidTime = " " + FormatLongDuration(snapshot.IntestinalNutritionResidenceSecondsRemaining);
                }
                else
                    intestineSolidStatus = Localization.Get("xuiRebirthMetabolismAbsorbing");
                value = "[E1B955]" + Localization.Get("xuiRebirthMetabolismSolid") + ":[-] "
                    + One(snapshot.IntestinalSolidMl) + " mL  ([E8C75A]" + intestineSolidStatus + "[-]" + intestineSolidTime;
                if ((snapshot.IntestinalSolidMl > 0.05f || snapshot.IntestinalNutritionUnits > 0.01f) && snapshot.NutritionGainPointsPerRealMinute > 0.005f)
                    value += "  •  +" + One(snapshot.NutritionGainPointsPerRealMinute) + "/min";
                value += ")";
                return true;
            case "rbmet_intestine_solid_rate":
                value = "→ " + Localization.Get("xuiRebirthMetabolismNutrition") + ": +" + One(snapshot.NutritionGainPointsPerRealMinute) + "/min";
                return true;
            case "rbmet_compact_gastric_state":
                value = string.Empty;
                return true;
            case "rbmet_compact_gastric_transfer":
                value = string.Empty;
                return true;
            case "rbmet_compact_intestinal_water":
                value = string.Empty;
                return true;
            case "rbmet_compact_intestinal_food":
                value = string.Empty;
                return true;
            case "rbmet_compact_absorption":
                value = Localization.Get("xuiRebirthMetabolismHydration") + " +" + One(snapshot.HydrationGainPointsPerRealMinute)
                    + "/min  •  " + Localization.Get("xuiRebirthMetabolismNutrition") + " +" + One(snapshot.NutritionGainPointsPerRealMinute) + "/min";
                return true;
            case "rbmet_absorption_water_compact":
                value = Localization.Get("xuiRebirthMetabolismHydration") + "  " + Signed(snapshot.HydrationGainPointsPerRealMinute, "0.0") + " / min";
                return true;
            case "rbmet_absorption_nutrition_compact":
                value = Localization.Get("xuiRebirthMetabolismNutrition") + "  " + Signed(snapshot.NutritionGainPointsPerRealMinute, "0.0") + " / min";
                return true;
            case "rbmet_hydration_net_compact":
                value = Signed(snapshot.HydrationNetPointsPerRealMinute, "0.00") + " / min";
                return true;
            case "rbmet_nutrition_net_compact":
                value = Signed(snapshot.NutritionNetPointsPerRealMinute, "0.00") + " / min";
                return true;
            case "rbmet_digestive_health_summary":
                value = Localization.Get("xuiRebirthMetabolismDigestionSpeed") + " " + Pct(snapshot.DigestionSpeedMultiplier)
                    + "  •  " + Localization.Get("xuiRebirthMetabolismGutResilience") + " " + Pct(snapshot.GutResilienceMultiplier);
                return true;
            case "rbmet_energy_compact":
                value = Num(snapshot.Energy) + "/" + Num(EnergyCeiling(snapshot)) + " " + Localization.Get("xuiRebirthMetabolismCap")
                    + "  •  " + Num(snapshot.EnergyMax) + " " + Localization.Get("xuiRebirthMetabolismMax")
                    + "  •  " + Localization.Get("xuiRebirthEnergyStaminaRecovery") + " " + Pct(snapshot.StaminaRecoveryMultiplier);
                return true;
            case "rbmet_energy_rates_compact":
                value = Localization.Get("xuiRebirthMetabolismUse") + " " + One(snapshot.EnergyUsePerRealMinute) + "/min  •  "
                    + Localization.Get("xuiRebirthMetabolismRecovery") + " +" + One(snapshot.EnergyRecoveryPerRealMinute + snapshot.BeverageEnergyGainPerRealMinute) + "/min";
                return true;
            case "rbmet_energy_use_compact":
                value = Localization.Get("xuiRebirthMetabolismUse") + " -" + One(snapshot.EnergyUsePerRealMinute) + "/min";
                return true;
            case "rbmet_energy_recovery_compact":
                value = Localization.Get("xuiRebirthMetabolismRecovery") + " +" + One(snapshot.EnergyRecoveryPerRealMinute + snapshot.BeverageEnergyGainPerRealMinute) + "/min";
                return true;

            case "rbmet_slot_name": value = string.IsNullOrEmpty(snapshot.HydrationSlotItemName) ? Localization.Get("xuiRebirthMetabolismSlotEmpty") : Localization.Get(snapshot.HydrationSlotItemName); return true;
            case "rbmet_slot_volume": value = snapshot.HydrationSlotCapacityMl > 0f ? RebirthLiquidContainerService.FormatVolume(snapshot.HydrationSlotVolumeMl) + " / " + RebirthLiquidContainerService.FormatVolume(snapshot.HydrationSlotCapacityMl) : "--"; return true;
            case "rbmet_slot_fill": value = snapshot.HydrationSlotCapacityMl > 0f ? Math.Max(0f, Math.Min(1f, snapshot.HydrationSlotVolumeMl / snapshot.HydrationSlotCapacityMl)).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) : "0"; return true;
            case "rbmet_slot_liquid": value = string.IsNullOrEmpty(snapshot.HydrationSlotLiquidProfile) ? "--" : snapshot.HydrationSlotLiquidProfile; return true;
            case "rbmet_autosip": value = snapshot.AutoSipEnabled ? Localization.Get("xuiRebirthMetabolismAutoSipOn") : Localization.Get("xuiRebirthMetabolismAutoSipOff"); return true;
            case "rbmet_feedback": value = RebirthMetabolismUiFeedback.CurrentMessage; return true;
            default: return base.GetBindingValueInternal(ref value, bindingName);
        }
    }
}


/// <summary>
/// Static player-facing explanation of the metabolism pipeline.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthMetabolismInformationWindow : XUiController
{
    public const string WindowGroupName = "rebirthMetabolismInformation";

    public override void Init()
    {
        base.Init();
        XUiController close = GetChildById("btnRebirthMetabolismInformationClose");
        if (close != null)
            close.OnPress += Close_OnPress;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (GetParentByType<XUiC_RebirthSurvivorCharacter>() == null && windowGroup != null)
            windowGroup.isEscClosable = true;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (GetParentByType<XUiC_RebirthSurvivorCharacter>() == null && XUiUtils.HotkeysAllowedFor(viewComponent) &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased || xui.playerUI.playerInput.GUIActions.Cancel.WasReleased))
            CloseWindow();
    }

    public static void Open(XUi targetXui)
    {
        if (targetXui == null || targetXui.playerUI == null)
            return;

        XUiController group = targetXui.FindWindowGroupByName(WindowGroupName);
        XUiC_RebirthMetabolismInformationWindow controller = group != null ? group.GetChildByType<XUiC_RebirthMetabolismInformationWindow>() : null;
        if (controller == null)
        {
            Log.Error("[REBIRTH Metabolism] information window controller not found.");
            return;
        }

        targetXui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, false);
    }

    private void Close_OnPress(XUiController sender, int mouseButton)
    {
        CloseWindow();
    }

    private void CloseWindow()
    {
        if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null && windowGroup != null)
            xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }
}


/// <summary>
/// Read-only Character-window presentation of the metabolism snapshot.
/// The Character tab is the primary detailed presentation; there is intentionally
/// no secondary DETAILS popup at this time.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthMetabolismCharacterPanel : XUiC_RebirthMetabolismWindow
{
}
