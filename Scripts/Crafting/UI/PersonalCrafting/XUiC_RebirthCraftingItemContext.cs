using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// In-place Selected Item context for Rebirth Personal Crafting.
/// It swaps with the Selected Recipe surface; it is never a floating Backpack popup. It never
/// owns item transactions: the visible action row is a real XUiC_ItemActionList populated from
/// the selected authoritative Backpack XUiC_ItemStack.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingItemContext : XUiController
{
    private const int MaxVisibleActions = 6;
    private const int ContextHeight = 232;
    private const int CombatPanelHeight = 206;
    private const int CombatPanelGap = 16;
    private int combatVisibleDataRows;

    private RebirthCraftingPresentation owner;
    private XUiC_RebirthCraftingInventory inventory;
    private XUiController recipeDetailsRegion;
    private XUiController requirementsRegion;
    private XUiC_RebirthCraftingInventoryScroll scroll;
    private XUiC_ItemActionList actionList;
    private readonly XUiController[] actionControllers = new XUiController[MaxVisibleActions];
    private readonly XUiV_Label[] actionNames = new XUiV_Label[MaxVisibleActions];
    private XUiController closeButton;
    private XUiV_Sprite itemIcon;
    private XUiV_Label itemName;
    private XUiV_Label itemSummary;
    private XUiV_Label itemDescription;
    private XUiV_Label actionCount;
    private XUiC_ItemStack selectedSlot;
    private int lastFingerprint;
    private int renderedConsumableRows;
    private int lastStatPitch = -1, lastStatWidth = -1;
    private int lastActionItemType = int.MinValue;
    private bool backpackEventWired;
    private Vector2i lastParentSize = new Vector2i(-1, -1);
    private bool lastSelectedSlotVisible = true;
    private int lastActionLayoutCount = -1;
    private int lastActionLayoutMask = -1;
    private Vector2i lastActionListSize = new Vector2i(-1, -1);
    private readonly List<string> fingerprintMetadataKeys = new List<string>();

    public XUiC_ItemStack SelectedSlot => selectedSlot;
    public bool IsContextVisible => ViewComponent != null && ViewComponent.IsVisible && selectedSlot != null;

    public override void Init()
    {
        base.Init();
        owner = RebirthCraftingPresentation.Resolve(this);
        inventory = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingInventory>() : null;
        recipeDetailsRegion = owner != null ? owner.GetChildById("rebirthCraftingDetailsRegion") : null;
        requirementsRegion = owner != null ? owner.GetChildById("rebirthCraftingRequirementsRegion") : null;
        scroll = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingInventoryScroll>() : null;
        actionList = GetChildById("rebirthCraftingItemActionList") as XUiC_ItemActionList;
        // Native action assignment reuses these authored controls; it changes their data.
        for (int i = 0; i < MaxVisibleActions; i++)
        {
            actionControllers[i] = GetChildById("rebirthCraftingItemAction" + i);
            actionNames[i] = actionControllers[i]?.GetChildById("name")?.ViewComponent as XUiV_Label;
        }
        closeButton = GetChildById("btnRebirthCraftingItemContextClose");
        itemIcon = GetView<XUiV_Sprite>("rebirthCraftingItemContextIcon");
        itemName = GetView<XUiV_Label>("rebirthCraftingItemContextName");
        itemSummary = GetView<XUiV_Label>("rebirthCraftingItemContextSummary");
        itemDescription = GetView<XUiV_Label>("rebirthCraftingItemContextDescription");
        actionCount = GetView<XUiV_Label>("rebirthCraftingItemContextActionCount");
        if (closeButton != null) closeButton.OnPress += Close_OnPress;
        HideContext(false, true);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        WireBackpackEvent();
        HideContext(false, true);
    }

    public override void OnClose()
    {
        HideContext(true, true);
        UnwireBackpackEvent();
        base.OnClose();
    }

    public override void Update(float dt)
    {
        // The native ItemActionList resolves its dirty action assignment inside base.Update().
        // Keep the action strip hidden while that one assignment pass runs, then reveal the
        // already-normalized/stable entries in this same outer update.
        base.Update(dt);
        if (!IsContextVisible)
            return;

        ApplyActionEntryGeometry(false);

        // Requirements belong to recipe context only. These helpers are change-aware.
        SetRequirementsVisible(false);

        if (selectedSlot.ItemStack == null || selectedSlot.ItemStack.IsEmpty() ||
            selectedSlot.SlotNumber < 0 || selectedSlot.SlotNumber >= SlotLimit(selectedSlot))
        {
            HideContext(true, true);
            return;
        }

        bool selectedVisible = selectedSlot.StackLocation == XUiC_ItemStack.StackLocationTypes.ToolBelt || scroll == null || scroll.IsSlotVisible(selectedSlot.SlotNumber);
        if (selectedVisible != lastSelectedSlotVisible)
        {
            lastSelectedSlotVisible = selectedVisible;
            if (RebirthLogSettings.CraftingUiLoggingEnabled)
                Log.Out("[REBIRTH Crafting ItemContext] selectedSlot=" + selectedSlot.SlotNumber
                    + " visibleInInventory=" + selectedVisible
                    + " contextPreserved=true");
        }

        int fingerprint = BuildFingerprint(selectedSlot.ItemStack);
        Vector2i parentSize = recipeDetailsRegion != null && recipeDetailsRegion.ViewComponent != null
            ? recipeDetailsRegion.ViewComponent.Size : new Vector2i(-1, -1);
        bool contentChanged = fingerprint != lastFingerprint;
        int currentItemType = selectedSlot.ItemStack?.itemValue != null ? selectedSlot.ItemStack.itemValue.type : 0;
        bool actionSetChanged = currentItemType != lastActionItemType;
        bool geometryChanged = parentSize.x != lastParentSize.x || parentSize.y != lastParentSize.y;
        if (Time.unscaledTime >= nextRepairMaterialsRefresh)
        {
            nextRepairMaterialsRefresh = Time.unscaledTime + 0.5f;
            RenderRepairMaterials(selectedSlot.ItemStack);
        }
        if (contentChanged || geometryChanged)
            Render(actionSetChanged);
    }

    private int SlotLimit(XUiC_ItemStack slot) => slot.StackLocation == XUiC_ItemStack.StackLocationTypes.ToolBelt
        ? RebirthToolbeltCapacity.GetOwnedSlotCount(xui.playerUI.entityPlayer, xui.playerUI.entityPlayer.inventory.Length) : RebirthCraftingInventoryBridge.GetPhysicalSlotCount(xui);

    public void SelectSlot(XUiC_ItemStack slot)
    {
        if (slot == null || slot.ItemStack == null || slot.ItemStack.IsEmpty())
        {
            HideContext(true, true);
            return;
        }

        int physical = SlotLimit(slot);
        if (slot.SlotNumber < 0 || slot.SlotNumber >= physical)
        {
            HideContext(true, true);
            return;
        }

        if (selectedSlot != null && selectedSlot != slot)
        {
            (selectedSlot as XUiC_RebirthCraftingInventorySlot)?.SetRebirthContextSelected(false);
            selectedSlot.Selected(false);
        }

        selectedSlot = slot;
        selectedSlot.Selected(true);
        (selectedSlot as XUiC_RebirthCraftingInventorySlot)?.SetRebirthContextSelected(true);
        selectedSlot.InfoWindow = RebirthInventoryInfoWindowLookup.Find(xui); // Native Modify requires this; the slot suppresses ordinary inspection.
        owner?.Coordinator?.SelectInventoryItem(slot, true);
        if (ViewComponent != null)
        {
            ViewComponent.IsVisible = true;
            ViewComponent.Enabled = true;
        }
        SetRecipeDetailsVisible(false);
        SetRequirementsVisible(false);
        SetControllerVisible(closeButton, true);

        lastFingerprint = 0;
        lastActionItemType = int.MinValue;
        lastSelectedSlotVisible = slot.StackLocation == XUiC_ItemStack.StackLocationTypes.ToolBelt || scroll == null || scroll.IsSlotVisible(slot.SlotNumber);
        Render(true);
    }

    public void ApplySurfaceMode()
    {
        if (owner != null && owner.IsInventoryOnlyMode)
        {
            SetRecipeDetailsVisible(false);
            SetRequirementsVisible(false);
            if (selectedSlot != null && selectedSlot.ItemStack != null && !selectedSlot.ItemStack.IsEmpty())
            {
                if (ViewComponent != null)
                {
                    ViewComponent.IsVisible = true;
                    ViewComponent.Enabled = true;
                }
                SetControllerVisible(closeButton, true);
                ApplyPopoverGeometry();
            }
            else
            {
                ShowInventoryIdleContext();
            }
            return;
        }

        if (selectedSlot != null && selectedSlot.ItemStack != null && !selectedSlot.ItemStack.IsEmpty())
        {
            if (ViewComponent != null)
            {
                ViewComponent.IsVisible = true;
                ViewComponent.Enabled = true;
            }
            SetControllerVisible(closeButton, true);
            SetRecipeDetailsVisible(false);
            SetRequirementsVisible(false);
            ApplyPopoverGeometry();
        }
        else
        {
            if (ViewComponent != null)
            {
                ViewComponent.IsVisible = false;
                ViewComponent.Enabled = false;
            }
            SetRecipeDetailsVisible(true);
            SetRequirementsVisible(true);
        }
        owner?.RequestLayoutAudit();
    }

    public void ClearSelection()
    {
        HideContext(true, true);
    }

    public void ClearSelectionFromCoordinator()
    {
        HideContext(true, false);
    }

    public void ClearIfSelected(XUiC_RebirthCraftingInventorySlot slot)
    {
        if (selectedSlot == slot)
            HideContext(true, true);
    }

    public string ContextTooltip
    {
        get
        {
            if (selectedSlot == null || selectedSlot.ItemStack == null || selectedSlot.ItemStack.IsEmpty())
                return string.Empty;
            string description = ResolveDescription(selectedSlot.ItemStack, xui);
            string summary = BuildSummary(selectedSlot.ItemStack);
            StringBuilder sb = new StringBuilder(selectedSlot.ItemNameText ?? string.Empty);
            if (!string.IsNullOrEmpty(summary)) sb.Append("\n").Append(summary);
            if (!string.IsNullOrEmpty(description)) sb.Append("\n\n").Append(description);
            return sb.ToString();
        }
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        if (bindingName == "rebirthitemcontexttooltip")
        {
            value = ContextTooltip;
            return true;
        }
        return base.GetBindingValueInternal(ref value, bindingName);
    }

    private ItemStack hoveredComparison = ItemStack.Empty;
    public void CompareStats(ItemStack stack)
    {
        hoveredComparison = stack ?? ItemStack.Empty;
        if (IsContextVisible) RenderItemStats();
    }
    private void RenderItemStats()
    {
        var stack = selectedSlot?.ItemStack;
        ItemStack comparison = RebirthCharacterItemStatsTooltip.Hovered == selectedSlot ? null : hoveredComparison;
        System.Collections.Generic.List<RebirthConsumableItemPresentation.Row> consumableRows;
        bool consumable = RebirthConsumableItemPresentation.TryBuild(stack, comparison,
            xui, out consumableRows, true);
        renderedConsumableRows = consumable ? consumableRows.Count : 0;
        var display = !consumable && stack != null && !stack.IsEmpty()
            ? UIDisplayInfoManager.Current.GetDisplayStatsForTag(stack.itemValue.ItemClass.DisplayType) : null;
        int nativeIndex = 0;
        bool nativeCompare = display != null && !hoveredComparison.IsEmpty() &&
            XUiM_ItemStack.CanCompare(stack.itemValue.ItemClass, hoveredComparison.itemValue.ItemClass);

        string[] titles = new string[7];
        string[] values = new string[7];
        for (int i = 0; i < 7; i++)
        {
            string title = "", value = "";
            if (consumable)
            {
                if (i < consumableRows.Count)
                { title = consumableRows[i].Title; value = consumableRows[i].Value; }
            }
            else if (display != null)
            {
                // Compact kept native rows; do not leave a blank row where Health 0 was.
                while (nativeIndex < display.DisplayStats.Count)
                {
                    var stat = display.DisplayStats[nativeIndex++];
                    if (!RebirthConsumableItemPresentation.ShouldShowNativeStat(stat, stack.itemValue,
                        nativeCompare ? hoveredComparison.itemValue : null, xui)) continue;
                    if (stat.StatType == PassiveEffects.RoundRayCount) continue;
                    string resolvedTitle = stat.TitleOverride ?? UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType);
                    string combatSkill = RebirthProgressionRuntimeConfig.ClassifyCombat(stack.itemValue);
                    if (RebirthWeaponFamilySkillService.IsRangedSkill(combatSkill) &&
                        !string.IsNullOrEmpty(resolvedTitle) && resolvedTitle.IndexOf("Stun Target", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    title = resolvedTitle;
                    value = nativeCompare ? XUiM_ItemStack.GetStatItemValueTextWithCompareInfo(stack.itemValue, hoveredComparison.itemValue, xui.playerUI.entityPlayer, stat)
                        : RebirthItemStatColors.NativeValue(stack, xui.playerUI.entityPlayer, stat);
                    value = RebirthItemStatColors.Format(value);
                    break;
                }
            }
            titles[i] = title ?? string.Empty;
            values[i] = value ?? string.Empty;
        }

        // Melee Selected Item presentation deliberately replaces the redundant raw Normal/Power
        // damage rows with the live derived values the player actually needs. Keep the familiar
        // native labels, but show Normal (Power) in one value column.
        if (!consumable && stack != null && !stack.IsEmpty() && stack.itemValue != null)
        {
            EntityPlayer player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
            RebirthWeaponSustainedDpsService.Profile profile;
            if (RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player, stack.itemValue, out profile) &&
                profile != null && profile.Valid)
            {
                if (profile.Melee)
                {
                    string[] sourceTitles = (string[])titles.Clone();
                    string[] sourceValues = (string[])values.Clone();
                    int meleeRow = FindStatRow(sourceTitles, "melee damage", 0);
                    int powerRow = FindStatRow(sourceTitles, "power attack damage", 1);
                    int targetArmorRow = FindStatRow(sourceTitles, "target armor", 2);
                    int blockRow = FindStatRow(sourceTitles, "block damage", 3);
                    int staminaRow = FindStatRow(sourceTitles, "stamina cost", 4);
                    int attacksRow = FindStatRow(sourceTitles, "attacks / minute", 5);
                    int durabilityRow = FindStatRow(sourceTitles, "durability", 6);

                    float powerDpsDisplay = profile.PowerAttackDps;
                    float powerBlockDisplay = profile.PowerBlockDamagePerAttack;

                    titles[0] = "Melee DPS";
                    values[0] = FormatPair(profile.NormalAttackDps, powerDpsDisplay, false);
                    titles[1] = StatTitleOr(sourceTitles, targetArmorRow, "Target Armor");
                    values[1] = StatValueOr(sourceValues, targetArmorRow);
                    titles[2] = StatTitleOr(sourceTitles, blockRow, "Block Damage");
                    values[2] = FormatPair(profile.NormalBlockDamagePerAttack, powerBlockDisplay, false);
                    titles[3] = StatTitleOr(sourceTitles, staminaRow, "Stamina Cost");
                    values[3] = FormatPair(profile.NormalStaminaCost, profile.PowerStaminaCost, false);
                    titles[4] = StatTitleOr(sourceTitles, attacksRow, "Attacks / Minute");
                    values[4] = StatValueOr(sourceValues, attacksRow);
                    titles[5] = StatTitleOr(sourceTitles, durabilityRow, "Max Durability");
                    values[5] = StatValueOr(sourceValues, durabilityRow);
                    titles[6] = string.Empty;
                    values[6] = string.Empty;
                }
                else if (profile.Ranged)
                {
                    // Shotguns are presented in total damage per trigger pull, not damage per pellet.
                    // Pellet count is intentionally hidden from this player-facing summary.
                    int damageRow = FindStatRow(titles, "damage / pellet", -1);
                    if (damageRow < 0) damageRow = FindStatRow(titles, "ranged damage", 0);
                    if (damageRow >= 0)
                    {
                        float totalShotDamage = profile.DamagePerAttack * Mathf.Max(1f, profile.ProjectilesPerAttack);
                        titles[damageRow] = "Ranged DPS";
                        values[damageRow] = profile.SustainedDps.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        for (int i = 0; i < 7; i++)
        {
            if(!consumable&&RebirthWeaponDetailRows.TryGet(xui,stack,null,i,out var weaponTitle,out var weaponValue)){titles[i]=weaponTitle;values[i]=weaponValue;}
            (GetChildById("craftingStatName" + i)?.ViewComponent as XUiV_Label)?.SetTextImmediately(titles[i]);
            (GetChildById("craftingStatValue" + i)?.ViewComponent as XUiV_Label)?.SetTextImmediately(values[i]);
        }

        // Seven liquid/effect rows must fit above the existing action strip, not cover it.
        // The outer panel, icon, description, inventory and action positions do not change.
        if (IsContextVisible) ApplyStatRowGeometry();
    }

    private static int FindStatRow(string[] titles, string needle, int fallback)
    {
        if (titles != null && !string.IsNullOrEmpty(needle))
            for (int i = 0; i < titles.Length; i++)
                if (!string.IsNullOrEmpty(titles[i]) && titles[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
        return titles != null && fallback >= 0 && fallback < titles.Length ? fallback : -1;
    }

    private static string StatTitleOr(string[] values, int index, string fallback)
    {
        return values != null && index >= 0 && index < values.Length && !string.IsNullOrWhiteSpace(values[index])
            ? values[index] : fallback;
    }

    private static string StatValueOr(string[] values, int index)
    {
        return values != null && index >= 0 && index < values.Length ? (values[index] ?? string.Empty) : string.Empty;
    }

    private bool repairMaterialsVisible;
    private float nextRepairMaterialsRefresh;
    private void RenderRepairMaterials(ItemStack stack)
    {
        ItemClass item = stack?.itemValue?.ItemClassOrMissing;
        repairMaterialsVisible = item?.RepairTools != null && item.RepairTools.Length > 0;
        SetControllerVisible(GetChildById("rebirthRepairMaterials"), repairMaterialsVisible);
        if (!repairMaterialsVisible) return;
        for (int i = 0; i < 8; i++)
        {
            ItemClass material = i < item.RepairTools.Length
                ? ItemClass.GetItemClass(item.RepairTools[i].Value) : null;
            bool show = material != null && material.RepairAmount.Value > 0;
            SetControllerVisible(GetChildById("rebirthRepairMaterialRow" + i), show);
            if (!show) continue;
            int count = Math.Max(0, Convert.ToInt32(Math.Ceiling(
                (double)Mathf.CeilToInt(stack.itemValue.UseTimes) / material.RepairAmount.Value)));
            var value = new ItemValue(material.Id);
            var icon = GetView<XUiV_Sprite>("rebirthRepairMaterialIcon" + i);
            if (icon != null) { icon.SpriteName = material.GetIconName(); icon.Color = material.GetIconTint(value); }
            SetLabelText("rebirthRepairMaterialName" + i, Localization.Get(material.GetItemName()));
            var quantity = GetView<XUiV_Label>("rebirthRepairMaterialQuantity" + i);
            if (quantity != null)
            {
                quantity.SetTextImmediately(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                // Uses the same broadcast-aware inventory count as native repair actions.
                int available = xui?.PlayerInventory != null ? xui.PlayerInventory.GetItemCount(value) : 0;
                quantity.Color = available > 0 ? new Color32(112, 207, 120, 255) : new Color32(238, 91, 91, 255);
            }
        }
    }
    private void RenderCombatData(ItemStack stack)
    {
        XUiController panel = GetChildById("rebirthCraftingCombatPanel");
        EntityPlayer player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        RebirthWeaponSustainedDpsService.Profile profile = null;
        bool show = stack != null && !stack.IsEmpty() && stack.itemValue != null &&
            RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player, stack.itemValue, out profile) &&
            profile != null && profile.Valid;

        SetControllerVisible(panel, show);
        if (!show)
        {
            combatVisibleDataRows = 0;
            SetCombatSkillDisplay(string.Empty, string.Empty, false);
            for (int i = 0; i < 4; i++) SetCombatRow(i, string.Empty, string.Empty, false);
            SetLabelText("rebirthCraftingCombatSkillGainValue", string.Empty);
            SetControllerVisible(GetChildById("rebirthCraftingCombatSkillGainRow"), false);
            return;
        }

        SetCombatSkillDisplay(profile.SkillId, RebirthSurvivorUiText.ResolveDefinitionName(profile.SkillId), true);

        if (profile.Melee)
        {
            combatVisibleDataRows = 1;
            SetCombatRow(0, "Damage / Stamina",
                FormatPair(profile.NormalDamagePerStamina, profile.PowerDamagePerStamina, true), true);
            SetCombatRow(1, string.Empty, string.Empty, false);
            SetCombatRow(2, string.Empty, string.Empty, false);
            SetCombatRow(3, string.Empty, string.Empty, false);
        }
        else if (profile.Ranged)
        {
            combatVisibleDataRows = 2;
            SetCombatRow(0, Localization.Get("xuiRebirthSustainedDps"), FormatNumber(profile.SustainedDps), true);
            SetCombatRow(1, Localization.Get("xuiRebirthReloadTime"), profile.ReloadSeconds > 0f ? profile.ReloadSeconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " s" : "—", true);
            SetCombatRow(2, string.Empty, string.Empty, false);
            SetCombatRow(3, string.Empty, string.Empty, false);
        }
        else if (profile.Explosive)
        {
            combatVisibleDataRows = 0;
            for (int i = 0; i < 4; i++) SetCombatRow(i, string.Empty, string.Empty, false);
        }
        else
        {
            combatVisibleDataRows = 0;
            for (int i = 0; i < 4; i++) SetCombatRow(i, string.Empty, string.Empty, false);
        }

        float practical = 0f, progress = 0f;
        float skillForRate = 0f;
        if (player != null && RebirthServiceCraftSkillService.TryGetPracticalSkillProgress(player, profile.SkillId, out practical, out progress))
            skillForRate = practical + progress;
        float gain;
        if (profile.Explosive)
        {
            // Explosives use the existing attributed damage award path rather than firearm/melee
            // sustained-DPS normalization. Show the exact 100-damage reference value that path uses.
            gain = Mathf.Clamp(RebirthProgressionRuntimeConfig.CombatPer100Damage,
                RebirthProgressionRuntimeConfig.CombatMin, RebirthProgressionRuntimeConfig.CombatMax);
        }
        else
        {
            gain = RebirthWeaponSustainedDpsService.CalculateCombatProgress(100f, profile.SustainedDps, skillForRate);
        }
        SetControllerVisible(GetChildById("rebirthCraftingCombatSkillGainRow"), true);
        SetLabelText("rebirthCraftingCombatSkillGainValue", "+" + gain.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void SetCombatSkillDisplay(string skillId, string skillName, bool visible)
    {
        SetControllerVisible(GetChildById("rebirthCraftingCombatSkillRow"), visible);
        SetLabelText("rebirthCraftingCombatSkillValue", visible ? (skillName ?? string.Empty) : string.Empty);
        XUiV_Sprite icon = GetChildById("rebirthCraftingCombatSkillIcon")?.ViewComponent as XUiV_Sprite;
        if (icon != null)
        {
            icon.IsVisible = visible && !string.IsNullOrEmpty(skillId);
            if (icon.IsVisible)
            {
                icon.UIAtlas = "RebirthSurvivorIcons";
                icon.SpriteName = RebirthSkillAptitudeTraitFactory.SkillIconKey(skillId);
                icon.Color = Color.white;
            }
        }
    }

    private void SetCombatRow(int index, string title, string value, bool visible)
    {
        SetControllerVisible(GetChildById("rebirthCraftingCombatDataRow" + index), visible);
        SetLabelText("rebirthCraftingCombatRowTitle" + index, visible ? (title ?? string.Empty) : string.Empty);
        SetLabelText("rebirthCraftingCombatRowValue" + index, visible ? (value ?? string.Empty) : string.Empty);
    }

    private static string FormatNumber(float value)
    {
        return value > 0f ? value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : "—";
    }

    private static string FormatEfficiency(float value)
    {
        return value > 0f ? value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "—";
    }

    private static string FormatPair(float normal, float power, bool twoDecimals)
    {
        string pattern = twoDecimals ? "0.00" : "0.#";
        string normalText = normal > 0f ? normal.ToString(pattern, System.Globalization.CultureInfo.InvariantCulture) : "—";
        if (power <= 0f) return normalText;
        return normalText + " (" + power.ToString(pattern, System.Globalization.CultureInfo.InvariantCulture) + ")";
    }

    private static bool TryParseFirstFloat(string text, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(text)) return false;
        System.Text.StringBuilder token = new System.Text.StringBuilder();
        bool started = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if ((c >= '0' && c <= '9') || c == '-' || c == '.' || c == ',')
            {
                token.Append(c == ',' ? '.' : c);
                started = true;
            }
            else if (started)
            {
                break;
            }
        }
        if (token.Length == 0) return false;
        return float.TryParse(token.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private void SetLabelText(string id, string text)
    {
        XUiV_Label label = GetChildById(id)?.ViewComponent as XUiV_Label;
        if (label != null) label.SetTextImmediately(text ?? string.Empty);
    }

    private void ApplyStatRowGeometry()
    {
        XUiController stats = GetChildById("craftingItemStats");
        if (stats?.ViewComponent == null) return;
        int statsWidth = stats.ViewComponent.Size.x;
        XUiController recipeStrip = owner != null ? owner.GetChildById("rebirthCraftingActionsStrip") : null;
        int actionsY = recipeStrip?.ViewComponent != null ? recipeStrip.ViewComponent.Position.y : -198;
        int rowPitch = 24;
        if (renderedConsumableRows > 6)
            rowPitch = Math.Min(24, Math.Max(18, (stats.ViewComponent.Position.y - actionsY - 8) / renderedConsumableRows));
        if (rowPitch == lastStatPitch && statsWidth == lastStatWidth) return;
        lastStatPitch = rowPitch; lastStatWidth = statsWidth;
        for (int i = 0; i < 7; ++i)
        {
            int valueWidth = statsWidth * 45 / 100;
            SetRect(GetChildById("craftingStatName" + i), 0, -i * rowPitch, statsWidth - valueWidth - 6, rowPitch);
            SetRect(GetChildById("craftingStatValue" + i), statsWidth - valueWidth, -i * rowPitch, valueWidth, rowPitch);
        }
    }

    private void Render(bool refreshActions)
    {
        if (selectedSlot == null || selectedSlot.ItemStack == null || selectedSlot.ItemStack.IsEmpty())
        {
            HideContext(true, true);
            return;
        }

        ItemStack stack = selectedSlot.ItemStack;
        ItemValue value = stack.itemValue;
        ItemClass itemClass = value != null ? value.ItemClassOrMissing : null;

        if (itemIcon != null)
        {
            itemIcon.IsVisible = itemClass != null;
            if (itemClass != null)
            {
                itemIcon.SpriteName = value.GetPropertyOverride(ItemClass.PropCustomIcon, itemClass.GetIconName());
                itemIcon.Color = itemClass.GetIconTint(value);
            }
        }

        if (itemName != null) itemName.Text = selectedSlot.ItemNameText ?? string.Empty;
        if (itemSummary != null) itemSummary.Text = BuildSummary(stack);
        if (itemDescription != null) itemDescription.Text = ResolveDescription(stack, xui);
        RenderItemStats();
        RenderCombatData(stack);
        RenderRepairMaterials(stack);

        if (refreshActions)
            RefreshNativeActions();

        if (actionCount != null && !string.IsNullOrEmpty(actionCount.Text))
            actionCount.Text = string.Empty;

        lastFingerprint = BuildFingerprint(stack);
        lastActionItemType = value != null ? value.type : 0;
        ApplyPopoverGeometry();
        owner?.RequestLayoutAudit();
        if (refreshActions)
            RefreshBindings();
    }

    private void RefreshNativeActions()
    {
        if (actionList == null || selectedSlot == null)
            return;

        // Resolve the native action-model rebuild atomically. ItemActionList normally defers the
        // visible entry assignment until its next Update, which exposed one intermediate frame
        // (old labels / <W>-style shortcut text). Run that one dirty pass immediately while the
        // strip is hidden, let RebirthCraftingItemActionEntry normalize the native shortcut text,
        // lay the final entries out, and reveal only the settled result.
        SetControllerVisible(actionList, false);
        lastActionLayoutCount = -1;
        actionList.SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.Item, selectedSlot);
        RebirthCharacterGearEquipEntry.Adapt(actionList, selectedSlot);
        RebirthEditorActions.Adapt(actionList, selectedSlot);
        actionList.Update(0f);
        ApplyActionEntryGeometry(true);
        SetControllerVisible(actionList, true);
    }

    private void BackpackItemsChanged()
    {
        if (selectedSlot == null)
            return;

        if (selectedSlot.ItemStack == null || selectedSlot.ItemStack.IsEmpty())
        {
            HideContext(true, true);
            return;
        }

        inventory?.RefreshAuthoritativePresentation(true);
        owner?.GetChildByType<XUiC_RebirthCraftingRequirements>()?.RefreshNow();
        owner?.GetChildByType<XUiC_RebirthCraftingRecipeDetails>()?.RefreshNow();
    }

    private void WireBackpackEvent()
    {
        if (backpackEventWired || xui == null || xui.PlayerInventory == null)
            return;
        xui.PlayerInventory.OnBackpackItemsChanged += BackpackItemsChanged;
        backpackEventWired = true;
    }

    private void UnwireBackpackEvent()
    {
        if (!backpackEventWired || xui == null || xui.PlayerInventory == null)
            return;
        xui.PlayerInventory.OnBackpackItemsChanged -= BackpackItemsChanged;
        backpackEventWired = false;
    }

    private void Close_OnPress(XUiController sender, int mouseButton)
    {
        if (mouseButton == 0 || mouseButton == -1)
            HideContext(true, true);
    }

    private void HideContext(bool clearActions, bool notifyCoordinator)
    {
        if (selectedSlot != null)
        {
            (selectedSlot as XUiC_RebirthCraftingInventorySlot)?.SetRebirthContextSelected(false);
            selectedSlot.Selected(false);
        }
        XUiC_ItemStack previous = selectedSlot;
        selectedSlot = null;
        lastSelectedSlotVisible = true;
        lastFingerprint = 0;
        lastActionItemType = int.MinValue;
        lastActionLayoutCount = -1;
        if (clearActions && actionList != null)
        {
            SetControllerVisible(actionList, false);
            actionList.SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.None, previous);
        }
        if (owner != null && owner.IsInventoryOnlyMode)
        {
            ShowInventoryIdleContext();
        }
        else
        {
            if (ViewComponent != null)
            {
                ViewComponent.IsVisible = false;
                ViewComponent.Enabled = false;
            }
            SetRecipeDetailsVisible(true);
            SetRequirementsVisible(true);
        }
        if (notifyCoordinator)
            owner?.Coordinator?.ClearInventoryItem();
        owner?.RequestLayoutAudit();
    }

    private void ShowInventoryIdleContext()
    {
        if (ViewComponent != null)
        {
            ViewComponent.IsVisible = true;
            ViewComponent.Enabled = true;
        }
        SetRecipeDetailsVisible(false);
        SetRequirementsVisible(false);
        SetControllerVisible(closeButton, false);
        SetControllerVisible(actionList, false);
        if (itemIcon != null) itemIcon.IsVisible = false;
        if (itemName != null) itemName.Text = string.Empty;
        if (itemSummary != null) itemSummary.Text = string.Empty;
        if (itemDescription != null) itemDescription.Text = string.Empty;
        if (actionCount != null) actionCount.Text = string.Empty;
        SetControllerVisible(GetChildById("rebirthCraftingCombatPanel"), false);
        ApplyPopoverGeometry();
        owner?.RequestLayoutAudit();
    }

    private void ApplyPopoverGeometry()
    {
        if (ViewComponent == null || recipeDetailsRegion == null || recipeDetailsRegion.ViewComponent == null)
            return;

        // This is a true context swap for the top detail surface, not a floating panel over the
        // backpack. It occupies the exact Selected Recipe rectangle while that rectangle is hidden.
        Vector2i parentSize = recipeDetailsRegion.ViewComponent.Size;
        lastParentSize = parentSize;
        int width = Math.Max(520, parentSize.x);
        int requirementsHeight = owner != null && owner.IsInventoryOnlyMode
            ? 0
            : (requirementsRegion != null && requirementsRegion.ViewComponent != null
                ? requirementsRegion.ViewComponent.Size.y
                : 0);
        int lowerSectionHeight = Math.Max(requirementsHeight, CombatPanelHeight);
        int height = Math.Max(ContextHeight, parentSize.y + (lowerSectionHeight > 0 ? CombatPanelGap + lowerSectionHeight : 0));

        SetRect(this, 0, 0, width, height);
        SetRect(GetChildById("rebirthCraftingItemContextBg"), 0, 0, width, height);
        SetRect(GetChildById("rebirthCraftingItemContextFrame"), 0, 0, width, height);
        SetRect(GetChildById("rebirthCraftingItemContextHeaderRule"), 0, -38, width, 2);
        int combatWidth = repairMaterialsVisible ? Math.Max(320, width - 300) : width;
        SetRect(GetChildById("rebirthRepairMaterials"), combatWidth + 12, -(parentSize.y + CombatPanelGap), width - combatWidth - 12, CombatPanelHeight);
        SetRect(GetChildById("rebirthCraftingCombatPanel"), 0, -(parentSize.y + CombatPanelGap), combatWidth, CombatPanelHeight);
        SetRect(GetChildById("rebirthCraftingCombatPanelBg"), 0, 0, combatWidth, CombatPanelHeight);
        SetRect(GetChildById("rebirthCraftingCombatHeaderRule"), 0, -38, combatWidth, 2);
        SetRect(GetChildById("rebirthCraftingCombatTitle"), 14, -8, Math.Max(180, combatWidth - 28), 28);
        ApplyCombatPanelGeometry(combatWidth);

        SetRect(GetChildById("rebirthCraftingItemContextTitle"), 14, -8, Math.Max(180, width - 64), 28);
        SetRect(GetChildById("btnRebirthCraftingItemContextClose"), width - 38, -7, 28, 28);

        SetRect(GetChildById("rebirthCraftingItemContextIconFrame"), 14, -46, 96, 96);
        SetRect(GetChildById("rebirthCraftingItemContextIcon"), 20, -52, 84, 84);
        int statsWidth = width / 3;
        int statsX = width - statsWidth - 14;
        int descriptionWidth = System.Math.Max(120, statsX - 140);
        SetRect(GetChildById("craftingItemStats"), statsX, -46, statsWidth, 180);
        ApplyStatRowGeometry();
        SetRect(GetChildById("rebirthCraftingItemContextName"), 124, -46, descriptionWidth, 30);
        SetRect(GetChildById("rebirthCraftingItemContextSummary"), 124, -76, descriptionWidth, 28);
        SetRect(GetChildById("rebirthCraftingItemContextDescription"), 124, -86, descriptionWidth, 128);

        // Item actions occupy the same baseline and width as the recipe Craft/Favorite/Track strip.
        // There is no separate action-count number; the actions themselves are the affordance.
        XUiController actionsTitle = GetChildById("rebirthCraftingItemContextActionsTitle");
        XUiController actionCountController = GetChildById("rebirthCraftingItemContextActionCount");
        if (actionsTitle?.ViewComponent != null) actionsTitle.ViewComponent.IsVisible = false;
        if (actionCountController?.ViewComponent != null) actionCountController.ViewComponent.IsVisible = false;

        XUiController list = actionList;
        XUiController recipeStrip = owner != null ? owner.GetChildById("rebirthCraftingActionsStrip") : null;
        Vector2i recipeActionPos = recipeStrip != null && recipeStrip.ViewComponent != null
            ? recipeStrip.ViewComponent.Position : new Vector2i(14, -198);
        Vector2i recipeActionSize = recipeStrip != null && recipeStrip.ViewComponent != null
            ? recipeStrip.ViewComponent.Size : new Vector2i(Math.Max(360, width - 28), 34);
        int listX = recipeActionPos.x;
        int listWidth = Math.Max(360, recipeActionSize.x);
        int listHeight = Math.Max(34, recipeActionSize.y);
        SetRect(list, listX, recipeActionPos.y, listWidth, listHeight);

        // ItemActionList owns action identity/input. Geometry is stable and only changes when
        // the number of resolved native actions or the strip size changes.
        ApplyActionEntryGeometry(false);
        RebirthSelectedDurability.Render(this, "rebirthCraftingItemDurability", selectedSlot);
        RenderSelectedQualityOverlay();

        SetRecipeDetailsVisible(false);
        SetRequirementsVisible(false);
    }

    private void RenderSelectedQualityOverlay()
    {
        // The shared renderer owns the real slot's number and its proportional placement.
        var overlay=GetChildById("rebirthCraftingItemQualityOverlay")?.ViewComponent as XUiV_Label;
        if(overlay!=null)overlay.IsVisible=false;
    }
    private void ApplyCombatPanelGeometry(int panelWidth)
    {
        int rowX = 14;
        int rowWidth = Math.Max(260, panelWidth - 28);
        int rightPad = 10;
        int valueWidth = Math.Min(200, Math.Max(140, rowWidth / 4));
        int valueX = rowWidth - valueWidth - rightPad;
        int titleWidth = Math.Max(140, valueX - 20);

        SetRect(GetChildById("rebirthCraftingCombatSkillRow"), rowX, -46, rowWidth, 28);
        SetRect(GetChildById("rebirthCraftingCombatDataRow0"), rowX, -78, rowWidth, 26);
        SetRect(GetChildById("rebirthCraftingCombatDataRow1"), rowX, -104, rowWidth, 24);
        SetRect(GetChildById("rebirthCraftingCombatDataRow2"), rowX, -130, rowWidth, 24);
        SetRect(GetChildById("rebirthCraftingCombatDataRow3"), rowX, -156, rowWidth, 24);
        int skillGainY = combatVisibleDataRows <= 0 ? -78 : (combatVisibleDataRows == 1 ? -108 : (combatVisibleDataRows == 2 ? -134 : (combatVisibleDataRows == 3 ? -160 : -186)));
        SetRect(GetChildById("rebirthCraftingCombatSkillGainRow"), rowX, skillGainY, rowWidth, 26);

        SetRect(GetChildById("rebirthCraftingCombatSkillRowBg"), 0, 0, rowWidth, 28);
        SetRect(GetChildById("rebirthCraftingCombatDataRowBg0"), 0, 0, rowWidth, 26);
        SetRect(GetChildById("rebirthCraftingCombatDataRowBg1"), 0, 0, rowWidth, 24);
        SetRect(GetChildById("rebirthCraftingCombatDataRowBg2"), 0, 0, rowWidth, 24);
        SetRect(GetChildById("rebirthCraftingCombatDataRowBg3"), 0, 0, rowWidth, 24);
        SetRect(GetChildById("rebirthCraftingCombatSkillGainBg"), 0, 0, rowWidth, 26);

        SetRect(GetChildById("rebirthCraftingCombatRowTitle0"), 10, -3, titleWidth, 20);
        SetRect(GetChildById("rebirthCraftingCombatRowValue0"), valueX, -3, valueWidth, 20);
        SetRect(GetChildById("rebirthCraftingCombatRowTitle1"), 10, -3, titleWidth, 18);
        SetRect(GetChildById("rebirthCraftingCombatRowValue1"), valueX, -3, valueWidth, 18);
        SetRect(GetChildById("rebirthCraftingCombatRowTitle2"), 10, -3, titleWidth, 18);
        SetRect(GetChildById("rebirthCraftingCombatRowValue2"), valueX, -3, valueWidth, 18);
        SetRect(GetChildById("rebirthCraftingCombatRowTitle3"), 10, -3, titleWidth, 18);
        SetRect(GetChildById("rebirthCraftingCombatRowValue3"), valueX, -3, valueWidth, 18);
        SetRect(GetChildById("rebirthCraftingCombatSkillGainValue"), valueX, -3, valueWidth, 20);

        int skillIconWidth = 22;
        int skillGap = 6;
        int skillValueWidth = Math.Min(120, valueWidth);
        int skillIconX = rowWidth - skillIconWidth - rightPad;
        int skillValueX = skillIconX - skillGap - skillValueWidth;
        SetRect(GetChildById("rebirthCraftingCombatSkillIcon"), skillIconX, -2, skillIconWidth, 22);
        SetRect(GetChildById("rebirthCraftingCombatSkillValue"), skillValueX, -4, skillValueWidth, 20);
    }

    private void ApplyActionEntryGeometry(bool force)
    {
        XUiController list = GetChildById("rebirthCraftingItemActionList");
        if (list?.ViewComponent == null)
            return;

        int activeCount = 0, activeMask = 0;
        for (int i = 0; i < MaxVisibleActions; i++)
        {
            XUiV_Label actionName = actionNames[i];
            if (actionName != null && !string.IsNullOrWhiteSpace(actionName.Text))
            {
                activeCount++;
                activeMask |= 1 << i;
            }
        }

        Vector2i listSize = list.ViewComponent.Size;
        if (!force && activeCount == lastActionLayoutCount && activeMask == lastActionLayoutMask &&
            listSize.x == lastActionListSize.x && listSize.y == lastActionListSize.y)
            return;

        lastActionLayoutCount = activeCount;
        lastActionLayoutMask = activeMask;
        lastActionListSize = listSize;

        int listWidth = Math.Max(1, listSize.x);
        int listHeight = Math.Max(1, listSize.y);
        int gap = 8;
        int cell = activeCount > 0
            ? Math.Max(96, (listWidth - gap * (activeCount - 1)) / activeCount)
            : Math.Max(96, listWidth);
        int labelY = -Math.Max(0, (listHeight - 24) / 2) - 5;
        int displayIndex = 0;

        for (int i = 0; i < MaxVisibleActions; i++)
        {
            XUiController entry = actionControllers[i];
            if (entry == null)
                continue;

            XUiV_Label actionName = actionNames[i];
            bool active = actionName != null && !string.IsNullOrWhiteSpace(actionName.Text);
            SetControllerVisible(entry, active);
            if (!active)
                continue;

            SetRect(entry, displayIndex * (cell + gap), 0, cell, listHeight);
            displayIndex++;
            // PC129: the plain backing follows this existing change-gated layout.
            // It has no input handlers and does not replace the native hover surface.
            SetRect(entry.GetChildById("rebirthActionOpaqueFill"), 0, 0, cell, listHeight);
            SetRect(entry.GetChildById("background"), 0, 0, cell, listHeight);
            SetRect(entry.GetChildById("icon"), 12, -7, 20, 20);

            XUiController nameController = entry.GetChildById("name");
            SetRect(nameController, 0, labelY, cell, 24);
            SetLabelFont(nameController, 17);

            XUiController keyboardController = entry.GetChildById("keyboardButton");
            SetRect(keyboardController, cell - 48, labelY, 38, 24);
            SetLabelFont(keyboardController, 17);
            SetRect(entry.GetChildById("gamepadIcon"), cell - 31, -7, 20, 20);
        }
    }

    private static void SetLabelFont(XUiController controller, int fontSize)
    {
        XUiV_Label label = controller?.ViewComponent as XUiV_Label;
        if (label != null && label.FontSize != fontSize)
            label.FontSize = fontSize;
    }

    private void SetRecipeDetailsVisible(bool visible)
    {
        SetControllerActive(recipeDetailsRegion, visible);
    }

    private void SetRequirementsVisible(bool visible)
    {
        SetControllerActive(requirementsRegion, visible);
    }

    private static void SetControllerActive(XUiController controller, bool visible)
    {
        if (controller == null || controller.ViewComponent == null)
            return;

        GameObject go = controller.ViewComponent.UiTransform != null
            ? controller.ViewComponent.UiTransform.gameObject : null;
        // Once a hidden region is actually inactive, ignore harmless controller-side writes to
        // IsVisible/Enabled instead of fighting them every Selected-Item frame. Re-enabling still
        // restores the complete view state below.
        if (!visible && go != null && !go.activeSelf)
            return;

        if (controller.ViewComponent.IsVisible != visible)
            controller.ViewComponent.IsVisible = visible;
        if (controller.ViewComponent.Enabled != visible)
            controller.ViewComponent.Enabled = visible;
        if (go != null && go.activeSelf != visible)
            go.SetActive(visible);
    }

    public static string BuildSummary(ItemStack stack) => string.Empty;

    private static void AppendPart(StringBuilder sb, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (sb.Length > 0) sb.Append("  •  ");
        sb.Append(value);
    }

    public static string ResolveDescription(ItemStack stack, XUi ui = null)
    {
        if (stack == null || stack.IsEmpty() || stack.itemValue == null)
            return string.Empty;
        string description = string.Empty;
        try
        {
            ItemClass item = stack.itemValue.ItemClassOrMissing;
            if (item == null) return string.Empty;
            string key = item.IsBlock() ? Block.list[stack.itemValue.type].DescriptionKey : item.GetItemDescriptionKey();
            description = !string.IsNullOrEmpty(key) && Localization.Exists(key) ? Localization.Get(key) : string.Empty;
            int estimate;
            if (RebirthItemSaleEstimate.TryGet(ui, stack, out estimate))
            {
                string price = string.Format(Localization.Get("xuiRebirthEstimatedStackSale"), estimate);
                return price + (description.Length > 0 ? "\n" + description : string.Empty);
            }
            return description;
        }
        catch { return description; }
    }

    private int BuildFingerprint(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty() || stack.itemValue == null)
            return 0;
        unchecked
        {
            ItemValue value = stack.itemValue;
            int hash = 17;
            hash = hash * 31 + value.type;
            hash = hash * 31 + value.Meta;
            hash = hash * 31 + value.Quality;
            hash = hash * 31 + value.UseTimes.GetHashCode();
            hash = hash * 31 + stack.count;
            hash = hash * 31 + RebirthConsumableItemPresentation.LiquidFingerprint(stack);
            if (value.Metadata != null && value.Metadata.Count > 0)
            {
                // This runs while an item is selected, including unchanged frames.
                // Reuse scratch storage while retaining the same ordered fingerprint.
                fingerprintMetadataKeys.Clear();
                fingerprintMetadataKeys.AddRange(value.Metadata.Keys);
                fingerprintMetadataKeys.Sort(StringComparer.Ordinal);
                for (int k = 0; k < fingerprintMetadataKeys.Count; k++)
                {
                    string key = fingerprintMetadataKeys[k] ?? string.Empty;
                    object metadata = value.Metadata[key];
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(key);
                    hash = hash * 31 + (metadata != null ? metadata.GetHashCode() : 0);
                }
                fingerprintMetadataKeys.Clear();
            }
            ItemValue[] mods = value.modifications;
            hash = hash * 31 + (mods != null ? mods.Length : 0);
            for (int i = 0; mods != null && i < mods.Length; i++)
                hash = hash * 31 + (mods[i] != null ? mods[i].type : 0);
            ItemValue[] cosmetics = value.cosmeticMods;
            hash = hash * 31 + (cosmetics != null ? cosmetics.Length : 0);
            for (int i = 0; cosmetics != null && i < cosmetics.Length; i++)
                hash = hash * 31 + (cosmetics[i] != null ? cosmetics[i].type : 0);

            EntityPlayer player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
            RebirthWeaponSustainedDpsService.Profile profile;
            bool hasProfile = RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player, value, out profile);
            hash = hash * 31 + (hasProfile ? RebirthWeaponSustainedDpsService.GetDisplayFingerprint(profile) : 0);
            float practical, progress;
            if (player != null && hasProfile && profile != null && profile.Valid &&
                RebirthServiceCraftSkillService.TryGetPracticalSkillProgress(player, profile.SkillId, out practical, out progress))
                hash = hash * 31 + Mathf.RoundToInt((practical + progress) * 1000f);
            return hash;
        }
    }

    private T GetView<T>(string id) where T : XUiView
    {
        XUiController c = GetChildById(id);
        return c != null ? c.ViewComponent as T : null;
    }

    private static void SetControllerVisible(XUiController controller, bool visible)
    {
        if (controller == null || controller.ViewComponent == null)
            return;
        if (controller.ViewComponent.IsVisible != visible)
            controller.ViewComponent.IsVisible = visible;
        if (controller.ViewComponent.Enabled != visible)
            controller.ViewComponent.Enabled = visible;
    }

    private static void SetRect(XUiController controller, int x, int y, int width, int height)
    {
        if (controller == null || controller.ViewComponent == null)
            return;
        Vector2i position = new Vector2i(x, y);
        Vector2i size = new Vector2i(Math.Max(1, width), Math.Max(1, height));
        if (controller.ViewComponent.Position.x != position.x || controller.ViewComponent.Position.y != position.y)
            controller.ViewComponent.Position = position;
        if (controller.ViewComponent.Size.x != size.x || controller.ViewComponent.Size.y != size.y)
            controller.ViewComponent.Size = size;
    }

    private static string Localize(string key, string fallback)
    {
        string value = Localization.Get(key ?? string.Empty);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
