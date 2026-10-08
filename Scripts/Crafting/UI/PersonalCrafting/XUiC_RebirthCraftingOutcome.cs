using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>Renders the source-backed Expected Outcome panel without unsupported probability rows.</summary>
[Preserve]
public sealed class XUiC_RebirthCraftingOutcome : XUiController
{
    private XUiC_RebirthPersonalCrafting owner;
    private XUiC_RebirthCraftingRecipeCatalogue catalogue;
    private XUiC_RebirthCraftingRecipeDetails details;
    private XUiC_RebirthCraftingRequirements requirements;
    private XUiC_RecipeCraftCount craftCount;
    private XUiV_Label statusTitle;
    private XUiV_Label statusValue;
    private XUiV_Label skillValue;
    private XUiV_Sprite skillIcon;
    private readonly RebirthCraftSkillPreview skillPreview = new RebirthCraftSkillPreview();
    private float nextSkillRefresh;
    private XUiV_Label xpTitle;
    private XUiV_Label xpValue;
    private XUiV_Label resultsTitle;
    private XUiV_Label resultsValue;
    private XUiV_Sprite resultIcon;
    private XUiV_Label explanation;
    private XUiV_Label emptyLabel;
    private Recipe lastRecipe;
    private int lastBatch = -1;
    private long lastRequirementRevision = long.MinValue;
    private Vector2i lastSize = new Vector2i(-1, -1);

    public override void Init()
    {
        base.Init();
        owner = GetParentByType<XUiC_RebirthPersonalCrafting>();
        catalogue = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingRecipeCatalogue>() : null;
        details = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingRecipeDetails>() : null;
        requirements = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingRequirements>() : null;
        craftCount = owner != null ? owner.GetChildByType<XUiC_RecipeCraftCount>() : null;
        statusTitle = GetView<XUiV_Label>("rebirthCraftingOutcomeStatusTitle");
        statusValue = GetView<XUiV_Label>("rebirthCraftingOutcomeStatusValue");
        skillValue = GetView<XUiV_Label>("rebirthCraftingOutcomeSkillValue");
        skillIcon = GetView<XUiV_Sprite>("rebirthCraftingOutcomeSkillIcon");
        xpTitle = GetView<XUiV_Label>("rebirthCraftingOutcomeXpTitle");
        xpValue = GetView<XUiV_Label>("rebirthCraftingOutcomeXpValue");
        resultsTitle = GetView<XUiV_Label>("rebirthCraftingOutcomeResultsTitle");
        resultsValue = GetView<XUiV_Label>("rebirthCraftingOutcomeResultsValue");
        resultIcon = GetView<XUiV_Sprite>("rebirthCraftingOutcomeResultIcon");
        explanation = GetView<XUiV_Label>("rebirthCraftingOutcomeExplanation");
        emptyLabel = GetView<XUiV_Label>("rebirthCraftingOutcomeEmpty");
        if (craftCount != null) craftCount.OnCountChanged += CraftCount_OnCountChanged;
        ApplyGeometry(true);
        RefreshNow();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        RefreshNow();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (owner != null && !owner.State.IsOpen) return;
        ApplyGeometry(false);
        long requirementRevision = requirements != null ? requirements.ProjectionRevision : 0L;
        Recipe recipe = owner?.Coordinator?.SelectedRecipe ?? (catalogue != null ? catalogue.CurrentRecipe : null);
        int batch = craftCount != null ? Math.Max(1, craftCount.Count) : 1;
        if (recipe != lastRecipe || batch != lastBatch || requirementRevision != lastRequirementRevision ||
            Time.realtimeSinceStartup >= nextSkillRefresh)
        {
            nextSkillRefresh = Time.realtimeSinceStartup + 0.5f;
            RefreshNow();
        }
    }

    public void RefreshNow()
    {
        Recipe recipe = owner?.Coordinator?.SelectedRecipe ?? (catalogue != null ? catalogue.CurrentRecipe : null);
        int batch = craftCount != null ? Math.Max(1, craftCount.Count) : 1;
        // Requirements owns its own projection cadence. Force it only when the selected recipe or
        // requested batch actually changes; otherwise reuse its current snapshot rather than
        // repeating the same local/remote material scan a second time every 0.20 seconds.
        bool selectionChanged = recipe != lastRecipe || batch != lastBatch;
        if (selectionChanged)
            requirements?.RefreshNow();
        RebirthCraftingOutcomeViewModel vm = RebirthCraftingOutcomeViewModel.Build(
            xui, owner, details, recipe, batch, requirements?.CurrentRequirements, skillPreview);
        Render(vm);
        if (resultIcon != null) resultIcon.IsVisible = false; // matches the cooking outcome card
        lastRecipe = recipe;
        lastBatch = batch;
        lastRequirementRevision = requirements != null ? requirements.ProjectionRevision : 0L;
    }

    public override void OnClose()
    {
        skillPreview.Reset();
        base.OnClose();
    }

    private void CraftCount_OnCountChanged(XUiController sender, OnCountChangedEventArgs args)
    {
        RefreshNow();
    }

    private void Render(RebirthCraftingOutcomeViewModel vm)
    {
        bool visible = vm != null && vm.HasRecipe;
        SetVisible(statusTitle, visible); SetVisible(statusValue, visible);
        SetVisible(xpTitle, visible && vm.HasSkill); SetVisible(xpValue, visible && vm.HasSkill);
        SetVisible(resultsTitle, false); SetVisible(resultsValue, visible);
        SetVisible(skillValue, visible);
        if (skillIcon != null) skillIcon.IsVisible = visible && vm.HasSkill;
        if (resultIcon != null) resultIcon.IsVisible = visible;
        // No decorative outline around the result icon.
        SetControllerVisible(GetChildById("rebirthCraftingOutcomeResultIconFrame"), false);
        SetVisible(explanation, visible && ViewComponent != null && ViewComponent.Size.y >= 238 && !string.IsNullOrEmpty(vm.Explanation));
        if (emptyLabel != null)
        {
            emptyLabel.IsVisible = !visible;
            emptyLabel.Text = Localize("xuiRebirthSelectRecipeOutcomePrompt", "Select a recipe to preview its deterministic outcome.");
        }
        if (!visible) return;

        statusTitle.Text = vm.StatusTitle; statusValue.Text = vm.StatusValue;
        if (skillValue != null) skillValue.Text = vm.SkillValue;
        if (skillIcon != null && vm.HasSkill) skillIcon.SpriteName = vm.SkillIcon;
        xpTitle.Text = vm.XpTitle; xpValue.Text = vm.XpValue;
        if (resultsTitle != null) resultsTitle.Text = vm.ResultsTitle;
        resultsValue.Text = vm.ResultsValue;
        explanation.Text = vm.Explanation;
        try { statusValue.Color = StringParsers.ParseColor32(vm.StatusColor); }
        catch { statusValue.Color = new Color32(210, 210, 210, 255); }
    }

    private void ApplyGeometry(bool force)
    {
        if (ViewComponent == null) return;
        Vector2i size = ViewComponent.Size;
        if (!force && size.x == lastSize.x && size.y == lastSize.y) return;
        lastSize = size;

        ApplyOutcomeLayout(this, size.x, size.y);
    }

    public static void ApplyOutcomeLayout(XUiController root, int width, int height)
    {
        if (root == null) return;
        width = Math.Max(240, width); height = Math.Max(180, height);
        int pad = 14;
        int split = Math.Max(130, (int)Math.Round(width * 0.59f));
        int footerY = height < 210 ? height - 28 : height - 48;
        int footerIconSize = height < 210 ? 22 : 30;
        SetRect(root.GetChildById("rebirthCraftingOutcomeStatusTitle"), pad, -50, split - pad - 10, 24);
        SetRect(root.GetChildById("rebirthCraftingOutcomeStatusValue"), pad, -78, split - pad - 10, 28);
        SetRect(root.GetChildById("rebirthCraftingOutcomeXpTitle"), split, -50, width - split - 12, 24);
        SetRect(root.GetChildById("rebirthCraftingOutcomeXpValue"), split, -78, width - split - 12, 28);
        SetRect(root.GetChildById("rebirthCraftingOutcomeResultRule"), 12, -114, width - 24, 1);
        // Keep a distinct result row and a bottom-anchored skill footer on compact screens too.
        int resultY = height < 230 ? 118 : 134;
        SetRect(root.GetChildById("rebirthCraftingOutcomeResultsValue"), pad, -resultY, width - 28, 28);
        int hintY = resultY + 32;
        SetRect(root.GetChildById("rebirthCraftingOutcomeExplanation"), pad, -hintY,
            width - 28, Math.Max(1, footerY - hintY - 6));
        var hint = root.GetChildById("rebirthCraftingOutcomeExplanation")?.ViewComponent;
        if (hint != null) hint.IsVisible = footerY - hintY - 6 >= 20;
        SetRect(root.GetChildById("rebirthCraftingOutcomeSkillIcon"), pad, -footerY, footerIconSize, footerIconSize);
        SetRect(root.GetChildById("rebirthCraftingOutcomeSkillValue"), 54, -(footerY + (height < 210 ? 0 : 4)), width - 68, height < 210 ? 22 : 26);
        SetRect(root.GetChildById("rebirthCraftingOutcomeEmpty"), pad, -76, width - 28, 70);
    }

    private T GetView<T>(string id) where T : XUiView
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as T : null;
    }

    private static void SetVisible(XUiV_Label label, bool visible) { if (label != null) label.IsVisible = visible; }
    private static void SetControllerVisible(XUiController controller, bool visible)
    {
        if (controller != null && controller.ViewComponent != null) controller.ViewComponent.IsVisible = visible;
    }
    private static void SetRect(XUiController controller, int x, int y, int width, int height)
    {
        if (controller?.ViewComponent == null) return;
        controller.ViewComponent.Position = new Vector2i(x, y);
        controller.ViewComponent.Size = new Vector2i(Math.Max(1, width), Math.Max(1, height));
    }
    private static string Localize(string key, string fallback)
    {
        string value = Localization.Get(key ?? string.Empty);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
