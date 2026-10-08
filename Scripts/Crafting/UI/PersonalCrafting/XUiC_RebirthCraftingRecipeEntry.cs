using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Rebirth-owned virtualized recipe row. It inherits XUiC_RecipeEntry only to preserve the native
/// recipe-entry data contract; all visible layout, selection and interaction are owned here.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingRecipeEntry : XUiC_RecipeEntry
{
    private XUiController rowHit;
    private XUiController favoriteHit;
    private XUiV_Label categoryLabel;
    private XUiV_Label statusLabel;
    private XUiV_Sprite selectionFrame;
    private XUiV_Sprite favoriteStar;
    private XUiV_Sprite stateIcon;
    private bool rebirthSelected;

    public XUiC_RebirthCraftingRecipeCatalogue Catalogue { get; set; }
    public int RowIndex { get; private set; }
    public bool HasData => Recipe != null;

    public override void Init()
    {
        base.Init();

        RowIndex = ParseTrailingNumber(ViewComponent != null ? ViewComponent.ID : string.Empty);
        rowHit = GetChildById("rebirthCraftingRecipeRowHit" + RowIndex);
        favoriteHit = GetChildById("rebirthCraftingRecipeFavoriteHit" + RowIndex);
        categoryLabel = GetView<XUiV_Label>("rebirthCraftingRecipeCategory");
        statusLabel = GetView<XUiV_Label>("rebirthCraftingRecipeStatus");
        selectionFrame = GetView<XUiV_Sprite>("rebirthCraftingRecipeSelectionFrame");
        favoriteStar = GetView<XUiV_Sprite>("favorite");
        stateIcon = GetView<XUiV_Sprite>("unlocked");
        var stationHit = GetChildById("unlocked");
        if (stationHit != null)
        {
            stationHit.OnPress += HandleRowPress;
            stationHit.OnScroll += HandleScroll;
        }

        if (rowHit != null)
        {
            rowHit.OnPress += HandleRowPress;
            rowHit.OnHover += HandleRowHover;
            rowHit.OnScroll += HandleScroll;
        }

        if (favoriteHit != null)
        {
            favoriteHit.OnPress += HandleFavoritePress;
            favoriteHit.OnScroll += HandleScroll;
        }

        SetVisible(false);
    }

    public void SetData(XUiC_RecipeList.RecipeInfo info, string categoryDisplayName)
    {
        SetVisible(info.recipe != null);
        if (info.recipe == null)
        {
            SetRecipeAndHasIngredients(null, false);
            return;
        }

        SetRecipeAndHasIngredients(info.recipe, info.hasIngredients);

        if (lblName != null)
            lblName.Text = Localization.Get(info.recipe.GetName());

        if (icoRecipe != null)
        {
            icoRecipe.SpriteName = info.recipe.GetIcon();
            ItemClass item = ItemClass.GetForId(info.recipe.itemValueType);
            icoRecipe.Color = item != null ? item.GetIconTint() : Color.white;
        }

        if (categoryLabel != null)
            categoryLabel.Text = (categoryDisplayName ?? string.Empty).ToUpperInvariant();

        bool favorite = CraftingManager.RecipeIsFavorite(info.recipe);
        if (favoriteStar != null)
        {
            favoriteStar.IsVisible = favorite;
            favoriteStar.Color = new Color32(204, 167, 56, 255);
        }

        RefreshState(info);
        // SetData can run while this virtual row still represents the currently selected recipe.
        // Never reset its visual to unselected here: Selected(true) may correctly be a no-op when
        // the selection state did not change, leaving an incorrectly dark row for that frame.
        ApplySelectedVisual(rebirthSelected);
        SetInteractive(true);
    }

    public void ClearData()
    {
        SetRebirthSelected(false);
        // Do not touch the inherited/native Selected member here. In the V3.2 mod compile
        // surface, the unqualified name resolves to a Selected(...) method group rather than the
        // XUiC_SelectableEntry property exposed by the decompiled/publicized source. Rebirth owns
        // recipe-row selection through rebirthSelected, so clearing that state is sufficient and
        // avoids a compile-surface-dependent member access.
        SetRecipeAndHasIngredients(null, false);
        if (lblName != null) lblName.Text = string.Empty;
        if (categoryLabel != null) categoryLabel.Text = string.Empty;
        if (statusLabel != null) statusLabel.Text = string.Empty;
        if (favoriteStar != null) favoriteStar.IsVisible = false;
        if (stateIcon != null) stateIcon.IsVisible = false;
        SetInteractive(false);
        SetVisible(false);
    }

    public void ApplyResponsiveGeometry(int width, int height)
    {
        width = Math.Max(200, width);
        height = Math.Max(22, height);
        SetSize(ViewComponent, width, height);
        SetSize(background, width, height);
        SetSize(selectionFrame, width, height);
        SetSize(rowHit != null ? rowHit.ViewComponent : null, width, height);

        int iconSize = Mathf.Clamp(height - 12, 30, 38);
        if (icoRecipe != null)
        {
            icoRecipe.Position = new Vector2i(5, -Math.Max(2, (height - iconSize) / 2));
            icoRecipe.Size = new Vector2i(iconSize, iconSize);
        }

        int stationSize = 22;
        int stationX = width - 6 - stationSize;
        int textX = 12 + iconSize;
        int nameHeight = Math.Max(40, height - 24);
        int categoryHeight = 22;
        if (lblName != null)
        {
            lblName.Position = new Vector2i(textX, -1);
            lblName.Size = new Vector2i(Math.Max(80, width - 8 - textX), nameHeight);
            lblName.FontSize = 20;
            lblName.Overflow = UILabel.Overflow.ClampContent;
            lblName.OverflowEllipsis = true;
        }
        if (categoryLabel != null)
        {
            categoryLabel.Position = new Vector2i(textX, -nameHeight);
            categoryLabel.Size = new Vector2i(Math.Max(1, stationX - 108 - textX), categoryHeight);
            categoryLabel.FontSize = 18;
            categoryLabel.Overflow = UILabel.Overflow.ClampContent;
            categoryLabel.OverflowEllipsis = true;
        }
        int smallIcon = Mathf.Clamp(height - 20, 16, 18);
        if (statusLabel != null)
        {
            statusLabel.Position = new Vector2i(stationX - 104, -nameHeight);
            statusLabel.Size = new Vector2i(98, 22);
            statusLabel.FontSize = 18;
            statusLabel.Overflow = UILabel.Overflow.ClampContent;
        }
        if (stateIcon != null)
        {
            // Align station artwork to the right margin; favorites overlay the recipe icon.
            stateIcon.Position = new Vector2i(stationX, -nameHeight);
            stateIcon.Size = new Vector2i(stationSize, stationSize);
        }
        if (favoriteStar != null)
        {
            favoriteStar.Position = new Vector2i(3, -2);
            favoriteStar.Size = new Vector2i(smallIcon, smallIcon);
        }
        if (favoriteHit != null && favoriteHit.ViewComponent != null)
        {
            favoriteHit.ViewComponent.Position = new Vector2i(0, 0);
            favoriteHit.ViewComponent.Size = new Vector2i(27, height);
        }
    }

    public void SetRebirthSelected(bool selected)
    {
        if (rebirthSelected == selected)
        {
            // Virtual rows can be rebound without changing logical selection. Reasserting is
            // cheap and prevents a data refresh from restoring the neutral row sprite.
            ApplySelectedVisual(selected);
            return;
        }
        rebirthSelected = selected;
        ApplySelectedVisual(selected);
    }

    public override void SelectedChanged(bool isSelected)
    {
        // Recipe selection belongs to the catalogue, not XUi.currentSelectedEntry. Backpack/item
        // inspection legitimately changes the global selectable entry and must not erase the
        // selected recipe highlight. Native selection can still highlight a non-selected row.
        ApplySelectedVisual(rebirthSelected || isSelected);
    }

    private void ApplySelectedVisual(bool selected)
    {
        if (background != null)
        {
            // Exact native ItemActionEntry treatment used by Selected Item actions.
            background.SpriteName = selected ? "ui_game_select_row" : "menu_empty2px";
            background.Color = selected ? (Color) Color.white : new Color32(27, 27, 31, 248);
        }

        // The filled native row highlight is the selection affordance. Do not stack the old
        // amber outline on top of it.
        if (selectionFrame != null)
            selectionFrame.IsVisible = false;
    }

    private void RefreshState(XUiC_RecipeList.RecipeInfo info)
    {
        Recipe recipe = info.recipe;
        string text;
        Color32 color;
        string icon = string.Empty;

        if (recipe.isChallenge)
        {
            text = Localized("xuiRebirthCraftingStatusChallenge", "CHALLENGE");
            color = new Color32(201, 198, 105, 255);
            icon = "ui_game_symbol_challenge";
        }
        else if (recipe.isQuest)
        {
            text = Localized("xuiRebirthCraftingStatusQuest", "QUEST");
            color = new Color32(201, 198, 105, 255);
            icon = "ui_game_symbol_quest";
        }
        else if (recipe.IsTracked)
        {
            text = Localized("xuiRebirthCraftingStatusTracked", "TRACKED");
            color = new Color32(181, 140, 255, 255);
            icon = "ui_game_symbol_compass";
        }
        else if (!info.unlocked)
        {
            text = Localized("xuiRebirthCraftingStatusLocked", "LOCKED");
            color = new Color32(204, 107, 100, 255);
            icon = "ui_game_symbol_lock";
        }
        else if (info.hasIngredients)
        {
            text = Localized("xuiRebirthCraftingStatusReady", "READY");
            color = new Color32(143, 209, 143, 255);
        }
        else
        {
            text = Localized("xuiRebirthCraftingStatusMissing", "MISSING");
            color = new Color32(214, 201, 120, 255);
        }

        if (statusLabel != null)
        {
            statusLabel.Text = text;
            statusLabel.Color = color;
        }

        if (stateIcon != null)
        {
            // Station identity distinguishes alternate recipes even while they are locked.
            string area = recipe.craftingArea;
            bool byHand = string.IsNullOrEmpty(area);
            ItemValue station = byHand ? ItemValue.None : ItemClass.GetItem(area);
            bool hasStationIcon = !byHand && station != null && station.type != 0;
            stateIcon.UIAtlas = byHand || hasStationIcon ? "ItemIconAtlas" : "UIAtlas";
            stateIcon.SpriteName = byHand ? "rb_crafting_backpack" : hasStationIcon ? station.ItemClass.GetIconName() : "ui_game_symbol_hammer";
            stateIcon.IsVisible = true;
            stateIcon.Color = Color.white;
            string stationName = byHand ? "Backpack crafting" : Localization.Get(area);
            stateIcon.ToolTip = stationName;

        }
    }

    private void HandleRowPress(XUiController sender, int mouseButton)
    {
        if (Catalogue != null && Recipe != null)
            Catalogue.SelectEntry(this, InputUtils.ShiftKeyPressed);
    }

    private void HandleRowHover(XUiController sender, bool isOver)
    {
        // The row-hit button is intentionally transparent and sits above the content only for
        // input capture. Put the hover on the real row background so the exact opaque native
        // ui_game_select_row treatment remains BEHIND the recipe icon/text, like ItemActionEntry.
        if (background == null || Recipe == null || rebirthSelected)
            return;

        background.SpriteName = isOver ? "ui_game_select_row" : "menu_empty2px";
        background.Color = isOver ? (Color) Color.white : new Color32(27, 27, 31, 248);
    }

    private void HandleFavoritePress(XUiController sender, int mouseButton)
    {
        if (Catalogue == null || Recipe == null)
            return;

        Catalogue.ToggleFavorite(Recipe);
    }

    private void HandleScroll(XUiController sender, float delta)
    {
        Catalogue?.HandleWheel(delta);
    }

    private void SetInteractive(bool enabled)
    {
        // The final presentation row is a clipped smooth-scroll buffer, not a controller-nav stop.
        bool navigable = enabled && RowIndex < (Catalogue?.VisibleRows ?? XUiC_RebirthCraftingRecipeCatalogue.VisibleRowCount);
        if (ViewComponent != null)
        {
            ViewComponent.Enabled = enabled;
            ViewComponent.IsNavigatable = navigable;
            ViewComponent.IsSnappable = navigable;
        }

        if (rowHit != null && rowHit.ViewComponent != null)
        {
            rowHit.ViewComponent.Enabled = enabled;
            rowHit.ViewComponent.IsNavigatable = navigable;
            rowHit.ViewComponent.IsSnappable = navigable;
        }
        if (favoriteHit != null && favoriteHit.ViewComponent != null)
            favoriteHit.ViewComponent.Enabled = enabled;
    }

    private void SetVisible(bool visible)
    {
        if (ViewComponent != null)
            ViewComponent.IsVisible = visible;
    }

    private T GetView<T>(string id) where T : XUiView
    {
        XUiController controller = GetChildById(id);
        return controller != null ? controller.ViewComponent as T : null;
    }

    private static void SetSize(XUiView view, int width, int height)
    {
        if (view != null)
            view.Size = new Vector2i(width, height);
    }

    private static int ParseTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        int end = value.Length - 1;
        while (end >= 0 && char.IsDigit(value[end])) end--;
        string digits = value.Substring(end + 1);
        int parsed;
        return int.TryParse(digits, out parsed) ? parsed : 0;
    }

    private static string Localized(string key, string fallback)
    {
        string value = Localization.Get(key);
        return string.IsNullOrEmpty(value) || value.Equals(key, StringComparison.OrdinalIgnoreCase)
            ? fallback
            : value.ToUpperInvariant();
    }
}
