using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Coordinates the context-sensitive item-info tabs added by Rebirth.
///
/// Native 3.1 has two layouts:
/// - items with stats: Stats + Description header, 228px content below it;
/// - items without stats: description-only content begins at the top with no header.
///
/// Rebirth keeps the native two-tab header when stats exist and appends Quick Stack
/// Category as the third tab.  When stats do not exist, Rebirth supplies a matching
/// two-tab header containing Description + Quick Stack Category and moves the native
/// description body below that header.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthItemInfoQuickStackTabs : XUiController
{
    private XUiC_ItemInfoWindow itemInfo;
    private XUiController statButton;
    private XUiController descriptionButton;
    private XUiController descriptionOnlyButton;
    private XUiController categoryTabbedButton;
    private XUiController categoryDescriptionOnlyButton;
    private XUiC_RebirthItemQuickStackCategoryPage categoryPage;
    private XUiController descriptionOnlyContent;
    private XUiController statsDescriptionContent;

    private bool wired;
    private int lastItemType = int.MinValue;
    private bool lastHasStats;

    public override void Init()
    {
        base.Init();
        itemInfo = GetParentByType<XUiC_ItemInfoWindow>();
        ResolveChildren();
        WireButtons();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ResolveChildren();
        WireButtons();

        if (categoryPage != null)
            categoryPage.Hide();

        RestoreNativeContentForCurrentItem();
        lastItemType = int.MinValue;
        SyncVisualState(true);
    }

    public override void Update(float dt)
    {
        base.Update(dt);

        if (!wired)
        {
            ResolveChildren();
            WireButtons();
        }

        SyncVisualState(false);

        // Native visibility bindings are evaluated every frame.  While the custom
        // category page is selected, force both native description/stat bodies off
        // after that evaluation so their labels cannot render through the category
        // page.  When Category is not selected, mirror the native parent-layout
        // visibility so changing tabs/items cannot leave either body stuck hidden.
        if (categoryPage != null && categoryPage.IsShowing)
            SetNativeContentVisible(false);
        else
            RestoreNativeContentForCurrentItem();
    }

    private void ResolveChildren()
    {
        if (itemInfo == null)
            itemInfo = GetParentByType<XUiC_ItemInfoWindow>();

        statButton = statButton ?? GetChildById("statButton");
        descriptionButton = descriptionButton ?? GetChildById("descriptionButton");
        descriptionOnlyButton = descriptionOnlyButton ?? GetChildById("rebirthDescriptionOnlyButton");
        categoryTabbedButton = categoryTabbedButton ?? GetChildById("rebirthQuickStackCategoryButtonTabbed");
        categoryDescriptionOnlyButton = categoryDescriptionOnlyButton ?? GetChildById("rebirthQuickStackCategoryButtonDescriptionOnly");
        categoryPage = categoryPage ?? GetChildByType<XUiC_RebirthItemQuickStackCategoryPage>();

        // There are two native rects with id="description": the first is the
        // description-only body, the second is the Stats/Description body.  Keep
        // direct references so Category can make the content area exclusive instead
        // of merely drawing another panel over native text.
        if (descriptionOnlyContent == null || statsDescriptionContent == null)
        {
            int found = 0;
            for (int i = 0; i < Children.Count; i++)
            {
                XUiController child = Children[i];
                if (child == null || child.ViewComponent == null || child.ViewComponent.ID != "description")
                    continue;
                if (found == 0) descriptionOnlyContent = child;
                else if (found == 1) { statsDescriptionContent = child; break; }
                found++;
            }
        }
    }

    private void WireButtons()
    {
        if (wired)
            return;

        if (statButton == null || descriptionButton == null || descriptionOnlyButton == null ||
            categoryTabbedButton == null || categoryDescriptionOnlyButton == null || categoryPage == null)
            return;

        // Native ItemInfoWindow also listens to statButton/descriptionButton.  Rebirth's
        // handler only closes the custom overlay; native selection/showStats behavior is
        // intentionally left to the base-game handlers.
        statButton.OnPress += NativeTab_OnPressed;
        descriptionButton.OnPress += NativeTab_OnPressed;
        descriptionOnlyButton.OnPress += DescriptionOnly_OnPressed;
        categoryTabbedButton.OnPress += Category_OnPressed;
        categoryDescriptionOnlyButton.OnPress += Category_OnPressed;
        wired = true;
    }

    private void NativeTab_OnPressed(XUiController sender, int mouseButton)
    {
        if (categoryPage != null)
            categoryPage.Hide();

        RestoreNativeContentForCurrentItem();
        SetSelected(categoryTabbedButton, false);
        SetSelected(categoryDescriptionOnlyButton, false);
    }

    private void DescriptionOnly_OnPressed(XUiController sender, int mouseButton)
    {
        if (categoryPage != null)
            categoryPage.Hide();

        RestoreNativeContentForCurrentItem();
        SetSelected(descriptionOnlyButton, true);
        SetSelected(categoryTabbedButton, false);
        SetSelected(categoryDescriptionOnlyButton, false);
    }

    private void Category_OnPressed(XUiController sender, int mouseButton)
    {
        if (itemInfo == null || categoryPage == null)
            return;

        // Both category buttons track the same logical tab so changing from a stat item
        // to a description-only item while inspected does not lose the selected state.
        SetSelected(statButton, false);
        SetSelected(descriptionButton, false);
        SetSelected(descriptionOnlyButton, false);
        SetSelected(categoryTabbedButton, true);
        SetSelected(categoryDescriptionOnlyButton, true);

        SetNativeContentVisible(false);
        categoryPage.Show(itemInfo);
    }

    private void SyncVisualState(bool force)
    {
        if (itemInfo == null)
            return;

        ItemStack stack = itemInfo.itemStack;
        int itemType = stack != null && !stack.IsEmpty() ? stack.itemValue.type : -1;
        bool hasStats = stack != null && !stack.IsEmpty() && XUiM_ItemStack.HasItemStats(stack);

        if (!force && itemType == lastItemType && hasStats == lastHasStats)
            return;

        lastItemType = itemType;
        lastHasStats = hasStats;

        bool showingCategory = categoryPage != null && categoryPage.IsShowing;
        if (showingCategory)
        {
            SetSelected(statButton, false);
            SetSelected(descriptionButton, false);
            SetSelected(descriptionOnlyButton, false);
            SetSelected(categoryTabbedButton, true);
            SetSelected(categoryDescriptionOnlyButton, true);
            return;
        }

        SetSelected(categoryTabbedButton, false);
        SetSelected(categoryDescriptionOnlyButton, false);

        if (hasStats)
        {
            SetSelected(descriptionOnlyButton, false);
            // Mirror the native showStats state so the native two-tab header remains the
            // source of truth for Stats versus Description.
            SetSelected(statButton, itemInfo.showStats);
            SetSelected(descriptionButton, !itemInfo.showStats);
        }
        else
        {
            // Description-only items always default to their native description content.
            SetSelected(statButton, false);
            SetSelected(descriptionButton, false);
            SetSelected(descriptionOnlyButton, true);
        }
    }

    private void SetNativeContentVisible(bool visible)
    {
        if (descriptionOnlyContent != null && descriptionOnlyContent.ViewComponent != null)
            descriptionOnlyContent.ViewComponent.IsVisible = visible;
        if (statsDescriptionContent != null && statsDescriptionContent.ViewComponent != null)
            statsDescriptionContent.ViewComponent.IsVisible = visible;
    }

    private void RestoreNativeContentForCurrentItem()
    {
        if (itemInfo == null)
            return;

        ItemStack stack = itemInfo.itemStack;
        bool hasStats = stack != null && !stack.IsEmpty() && XUiM_ItemStack.HasItemStats(stack);
        if (descriptionOnlyContent != null && descriptionOnlyContent.ViewComponent != null)
            descriptionOnlyContent.ViewComponent.IsVisible = !hasStats;
        if (statsDescriptionContent != null && statsDescriptionContent.ViewComponent != null)
            statsDescriptionContent.ViewComponent.IsVisible = hasStats;
    }

    private static void SetSelected(XUiController controller, bool value)
    {
        if (controller != null && controller.ViewComponent is XUiV_Button)
            ((XUiV_Button)controller.ViewComponent).Selected = value;
    }
}

/// <summary>
/// Quick Stack classification page shown inside the normal item-info content area.
/// The selected item's name is already present in the window header, so this page only
/// displays the category and the routing explanation.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthItemQuickStackCategoryPage : XUiController
{
    private XUiV_Label categoryLabel;
    private XUiV_Label diagnosticLabel;
    private XUiC_ItemInfoWindow itemInfo;
    private int lastItemType = -1;

    public bool IsShowing => viewComponent != null && viewComponent.IsVisible;

    public override void Init()
    {
        base.Init();
        itemInfo = GetParentByType<XUiC_ItemInfoWindow>();
        categoryLabel = GetLabel("rebirthQuickStackCategoryName");
        diagnosticLabel = GetLabel("rebirthQuickStackCategoryDiagnostic");
        if (viewComponent != null)
            viewComponent.IsVisible = false;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (!IsShowing)
            return;
        Refresh(false);
    }

    public void Show(XUiC_ItemInfoWindow info)
    {
        itemInfo = info ?? itemInfo;
        lastItemType = -1;
        if (viewComponent != null)
            viewComponent.IsVisible = true;
        Refresh(true);
    }

    public void Hide()
    {
        if (viewComponent != null)
            viewComponent.IsVisible = false;
        lastItemType = -1;
    }

    private void Refresh(bool force)
    {
        ItemStack stack = itemInfo != null ? itemInfo.itemStack : null;
        int itemType = stack != null && !stack.IsEmpty() ? stack.itemValue.type : -1;
        if (!force && itemType == lastItemType)
            return;
        lastItemType = itemType;

        if (itemType < 0)
        {
            SetLabel(categoryLabel, Localization.Get("xuiRebirthQuickStackCategoryUnassigned"));
            SetLabel(diagnosticLabel, Localization.Get("xuiRebirthQuickStackCategoryNoneDesc"));
            return;
        }

        QuickStackCategory category = QuickStackCategoryRegistry.GetFresh(itemType);
        SetLabel(categoryLabel, QuickStackCategoryRegistry.GetDisplayName(category));
        SetLabel(diagnosticLabel,
            Localization.Get(category == QuickStackCategory.None
                ? "xuiRebirthQuickStackCategoryNoneDesc"
                : "xuiRebirthQuickStackCategoryAssignedDesc"));
    }

    private XUiV_Label GetLabel(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Label : null;
    }

    private static void SetLabel(XUiV_Label label, string text)
    {
        if (label != null)
            label.Text = text ?? string.Empty;
    }
}

/// <summary>
/// Read-only combat summary embedded beneath the native ItemInfo action buttons.
/// It projects the same live sustained-DPS profile used by combat training so the
/// displayed numbers and the server-authoritative skill award model cannot drift.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthWeaponTrainingPanel : XUiController
{
    private XUiC_ItemInfoWindow itemInfo;
    private XUiController meleeRows;
    private XUiController rangedRows;
    private XUiV_Label skillValue;
    private XUiV_Label normalDpsValue;
    private XUiV_Label powerDpsValue;
    private XUiV_Label normalEfficiencyValue;
    private XUiV_Label powerEfficiencyValue;
    private XUiV_Label rangedDpsValue;
    private XUiV_Label capacityValue;
    private XUiV_Label reloadTimeValue;
    private XUiV_Label reloadsPerMinuteValue;
    private XUiV_Label skillGainValue;
    private int lastFingerprint = int.MinValue;
    private float nextRefresh;

    public override void Init()
    {
        base.Init();
        itemInfo = GetParentByType<XUiC_ItemInfoWindow>();
        meleeRows = GetChildById("rebirthCombatMeleeRows");
        rangedRows = GetChildById("rebirthCombatRangedRows");
        skillValue = Label("rebirthCombatSkillValue");
        normalDpsValue = Label("rebirthCombatNormalDpsValue");
        powerDpsValue = Label("rebirthCombatPowerDpsValue");
        normalEfficiencyValue = Label("rebirthCombatNormalEfficiencyValue");
        powerEfficiencyValue = Label("rebirthCombatPowerEfficiencyValue");
        rangedDpsValue = Label("rebirthCombatRangedDpsValue");
        capacityValue = Label("rebirthCombatCapacityValue");
        reloadTimeValue = Label("rebirthCombatReloadTimeValue");
        reloadsPerMinuteValue = Label("rebirthCombatReloadsPerMinuteValue");
        skillGainValue = Label("rebirthCombatSkillGainValue");
        SetVisible(false);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        lastFingerprint = int.MinValue;
        nextRefresh = 0f;
        Refresh(true);
    }

    public override void OnClose()
    {
        SetVisible(false);
        base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (Time.realtimeSinceStartup < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartup + 0.15f;
        Refresh(false);
    }

    private void Refresh(bool force)
    {
        if (itemInfo == null) itemInfo = GetParentByType<XUiC_ItemInfoWindow>();
        ItemStack stack = itemInfo != null ? itemInfo.itemStack : null;
        EntityPlayer player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        RebirthWeaponSustainedDpsService.Profile profile;
        if (stack == null || stack.IsEmpty() || stack.itemValue == null || player == null ||
            !RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player, stack.itemValue, out profile) ||
            profile == null || !profile.Valid)
        {
            lastFingerprint = int.MinValue;
            SetVisible(false);
            return;
        }

        float skillValueRaw = 0f, skillProgress = 0f;
        RebirthServiceCraftSkillService.TryGetPracticalSkillProgress(player, profile.SkillId, out skillValueRaw, out skillProgress);
        float currentSkill = skillValueRaw + skillProgress;
        int fingerprint = RebirthWeaponSustainedDpsService.GetDisplayFingerprint(player, stack.itemValue);
        unchecked
        {
            fingerprint = fingerprint * 31 + Mathf.RoundToInt(currentSkill * 1000f);
            fingerprint = fingerprint * 31 + (profile.Melee ? 1 : 0);
            fingerprint = fingerprint * 31 + (profile.Ranged ? 1 : 0);
        }
        if (!force && fingerprint == lastFingerprint && ViewComponent != null && ViewComponent.IsVisible) return;
        lastFingerprint = fingerprint;

        SetVisible(true);
        SetSectionVisible(meleeRows, profile.Melee);
        SetSectionVisible(rangedRows, profile.Ranged);

        Set(skillValue, RebirthSurvivorUiText.ResolveDefinitionName(profile.SkillId));
        if (profile.Melee)
        {
            Set(normalDpsValue, FormatNumber(profile.NormalAttackDps));
            Set(powerDpsValue, FormatNumber(profile.PowerAttackDps));
            Set(normalEfficiencyValue, FormatEfficiency(profile.NormalDamagePerStamina));
            Set(powerEfficiencyValue, FormatEfficiency(profile.PowerDamagePerStamina));
        }
        else
        {
            Set(rangedDpsValue, FormatNumber(profile.SustainedDps));
            Set(capacityValue, profile.MagazineSize.ToString(CultureInfo.InvariantCulture));
            Set(reloadTimeValue, profile.ReloadSeconds > 0f
                ? profile.ReloadSeconds.ToString("0.##", CultureInfo.InvariantCulture) + " s"
                : "—");
            Set(reloadsPerMinuteValue, profile.ReloadsPerMinute.ToString("0.#", CultureInfo.InvariantCulture));
        }

        float gain = RebirthWeaponSustainedDpsService.CalculateCombatProgress(100f, profile.SustainedDps, currentSkill);
        Set(skillGainValue, FormatGain(gain));
    }

    private XUiV_Label Label(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Label : null;
    }

    private void SetVisible(bool visible)
    {
        if (ViewComponent != null) ViewComponent.IsVisible = visible;
    }

    private static void SetSectionVisible(XUiController controller, bool visible)
    {
        if (controller != null && controller.ViewComponent != null) controller.ViewComponent.IsVisible = visible;
    }

    private static void Set(XUiV_Label label, string value)
    {
        if (label != null) label.SetTextImmediately(value ?? string.Empty);
    }

    private static string FormatNumber(float value)
    {
        return value > 0f ? value.ToString("0.#", CultureInfo.InvariantCulture) : "—";
    }

    private static string FormatEfficiency(float value)
    {
        return value > 0f ? value.ToString("0.##", CultureInfo.InvariantCulture) + " dmg / stamina" : "—";
    }

    private static string FormatGain(float value)
    {
        if (value <= 0f) return "—";
        string format = value < 0.01f ? "0.0000" : "0.000";
        return "+" + value.ToString(format, CultureInfo.InvariantCulture);
    }
}

