using System;
using InControl;
using Platform;
using UnityEngine;

#nullable disable

/// <summary>
/// Rebirth-owned horizontal Selected Recipe action strip. Native crafting/favorite/track commands
/// remain authoritative. Presentation is stable and uses the exact native ItemActionEntry hover
/// treatment (ui_game_select_row + white). The fourth action opens the selected recipe directly
/// in the read-only Progression Explorer.
/// </summary>
[UnityEngine.Scripting.Preserve]
public sealed class XUiC_RebirthCraftingActions : XUiController
{
    private XUiC_RebirthCraftingRecipeDetails details;
    private RebirthCraftingPresentation personalOwner;
    private XUiController craftButton;
    private XUiController favoriteButton;
    private XUiController trackButton;
    private XUiController explorerButton;
    private XUiV_Sprite favoriteState;
    private XUiV_Sprite trackState;
    private XUiV_Label craftCaption;
    private XUiV_Label favoriteCaption;
    private XUiV_Label trackCaption;
    private XUiV_Label explorerCaption;
    private XUiV_Label craftShortcut;
    private XUiV_Label favoriteShortcut;
    private XUiV_Label trackShortcut;
    private XUiV_Label explorerShortcut;
    private int lastWidth = -1;
    private PlayerInputManager.InputStyle rebirthLastInputStyle = PlayerInputManager.InputStyle.Count;

    public override void Init()
    {
        base.Init();
        personalOwner = RebirthCraftingPresentation.Resolve(this);
        details = GetParentByType<XUiC_RebirthCraftingRecipeDetails>();
        craftButton = GetChildById("btnRebirthCraftingCraft");
        favoriteButton = GetChildById("btnRebirthCraftingFavorite");
        trackButton = GetChildById("btnRebirthCraftingTrack");
        explorerButton = GetChildById("btnRebirthCraftingExplorer");
        favoriteState = GetView<XUiV_Sprite>("rebirthCraftingFavoriteState");
        trackState = GetView<XUiV_Sprite>("rebirthCraftingTrackState");
        craftCaption = GetView<XUiV_Label>("rebirthCraftingCraftCaption");
        favoriteCaption = GetView<XUiV_Label>("rebirthCraftingFavoriteCaption");
        trackCaption = GetView<XUiV_Label>("rebirthCraftingTrackCaption");
        explorerCaption = GetView<XUiV_Label>("rebirthCraftingExplorerCaption");
        craftShortcut = GetView<XUiV_Label>("rebirthCraftingCraftShortcut");
        favoriteShortcut = GetView<XUiV_Label>("rebirthCraftingFavoriteShortcut");
        trackShortcut = GetView<XUiV_Label>("rebirthCraftingTrackShortcut");
        explorerShortcut = GetView<XUiV_Label>("rebirthCraftingExplorerShortcut");

        WireActionButton(craftButton, Craft_OnPress);
        WireActionButton(favoriteButton, Favorite_OnPress);
        WireActionButton(trackButton, Track_OnPress);
        WireActionButton(explorerButton, Explorer_OnPress);

        rebirthLastInputStyle = PlatformManager.NativePlatform.Input.CurrentInputStyle;
        RefreshShortcutLabels();
        ApplyGeometry(true);
        RefreshState();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (personalOwner != null && !personalOwner.State.IsOpen) return;

        // The V3.2 mod compile surface does not expose XUiController.InputStyleChanged as an
        // overridable member. Only touch shortcut presentation when the actual native style changes.
        PlayerInputManager.InputStyle currentInputStyle = PlatformManager.NativePlatform.Input.CurrentInputStyle;
        if (currentInputStyle != rebirthLastInputStyle)
        {
            rebirthLastInputStyle = currentInputStyle;
            RefreshShortcutLabels();
        }

        // State is pushed by RecipeDetails.RefreshNow/Render. Geometry is change-gated.
        ApplyGeometry(false);
        HandleShortcuts();
    }

    private void HandleShortcuts()
    {
        if (ViewComponent?.IsActiveInHierarchy != true || xui?.playerUI?.playerInput == null ||
            RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return;
        bool keyboard = IsKeyboardStyle();
        if (keyboard && xui.playerUI.windowManager.IsInputActive()) return;
        PlayerActionsGUI actions = xui.playerUI.playerInput.GUIActions;
        if (!keyboard && !actions.Inspect.IsPressed) return;
        if (actions.DPad_Up.WasPressed && CanActivate(craftButton)) Craft_OnPress(craftButton, -1);
        else if (actions.DPad_Right.WasPressed && CanActivate(favoriteButton)) Favorite_OnPress(favoriteButton, -1);
        else if (actions.DPad_Left.WasPressed && CanActivate(trackButton)) Track_OnPress(trackButton, -1);
        else if (actions.DPad_Down.WasPressed && xui.DragAndDropWindow?.IsEmpty() == false) xui.DragAndDropWindow.DropCurrentItem();
    }

    private static bool CanActivate(XUiController button)
    {
        return button?.ViewComponent?.IsActiveInHierarchy == true && button.ViewComponent.Enabled;
    }

    public void RefreshState()
    {
        Recipe recipe = details != null ? details.SelectedRecipe : null;
        RebirthCraftingCommandBridge bridge = details != null ? details.CommandBridge : null;
        int tier = details != null ? details.SelectedCraftingTier : 1;

        bool hasRecipe = recipe != null && bridge != null;
        string explorerFocus;
        bool canExplore = TryGetRecipeExplorerFocus(recipe, out explorerFocus);
        bool canCraft = hasRecipe && bridge.CanCraft(recipe, tier);
        bool canFavorite = hasRecipe && bridge.CanFavorite(recipe);
        bool canTrack = hasRecipe && bridge.CanTrack(recipe);

        SetEnabled(craftButton, canCraft);
        SetEnabled(favoriteButton, canFavorite);
        SetEnabled(trackButton, canTrack);
        SetEnabled(explorerButton, canExplore);
        SetVisible(trackButton, canTrack);
        SetVisible(GetChildById("rebirthCraftingTrackOpaqueFill"), canTrack);
        SetVisible(GetChildById("rebirthCraftingTrackIcon"), canTrack);
        SetVisible(trackCaption?.Controller, canTrack);
        SetVisible(trackShortcut?.Controller, canTrack && IsKeyboardStyle());

        if (favoriteState != null)
        {
            bool visible = hasRecipe && bridge.IsFavorite(recipe);
            if (favoriteState.IsVisible != visible)
                favoriteState.IsVisible = visible;
            Color desired = new Color32(204, 167, 56, 255);
            if (favoriteState.Color != desired)
                favoriteState.Color = desired;
        }
        if (trackState != null)
        {
            bool visible = canTrack && bridge.IsTracked(recipe);
            if (trackState.IsVisible != visible)
                trackState.IsVisible = visible;
            Color desired = new Color32(181, 140, 255, 255);
            if (trackState.Color != desired)
                trackState.Color = desired;
        }

        SetActionContentColor("rebirthCraftingCraftIcon", craftCaption, craftShortcut, canCraft);
        SetActionContentColor("rebirthCraftingFavoriteIcon", favoriteCaption, favoriteShortcut, canFavorite);
        SetActionContentColor("rebirthCraftingTrackIcon", trackCaption, trackShortcut, canTrack);
        SetActionContentColor("rebirthCraftingExplorerIcon", explorerCaption, explorerShortcut, canExplore);
    }

    public void ApplyGeometry(bool force)
    {
        if (ViewComponent == null) return;
        int width = Math.Max(320, ViewComponent.Size.x);
        if (!force && width == lastWidth) return;
        lastWidth = width;

        // Use nearly the entire Selected Recipe width.  The visible hit/highlight sprite is the
        // button itself, so controller width and rendered width are now identical.
        int gap = 6;
        int buttonWidth = Math.Max(110, (width - gap * 3) / 4);

        int craftX = 0;
        SetActionGeometry(craftButton, "rebirthCraftingCraftIcon", "rebirthCraftingCraftOpaqueFill", craftCaption, craftShortcut, craftX, buttonWidth);

        int favoriteX = buttonWidth + gap;
        SetActionGeometry(favoriteButton, "rebirthCraftingFavoriteIcon", "rebirthCraftingFavoriteOpaqueFill", favoriteCaption, favoriteShortcut, favoriteX, buttonWidth);
        SetRect(GetChildById("rebirthCraftingFavoriteState"), favoriteX + buttonWidth - 62, -8, 18, 18);

        int trackX = (buttonWidth + gap) * 2;
        SetActionGeometry(trackButton, "rebirthCraftingTrackIcon", "rebirthCraftingTrackOpaqueFill", trackCaption, trackShortcut, trackX, buttonWidth);
        SetRect(GetChildById("rebirthCraftingTrackState"), trackX + buttonWidth - 62, -8, 18, 18);

        int explorerX = (buttonWidth + gap) * 3;
        int explorerWidth = Math.Max(92, width - explorerX);
        SetActionGeometry(explorerButton, "rebirthCraftingExplorerIcon", "rebirthCraftingExplorerOpaqueFill", explorerCaption, explorerShortcut, explorerX, explorerWidth);
    }

    private void SetActionGeometry(XUiController button, string iconId, string opaqueFillId, XUiV_Label caption, XUiV_Label shortcut, int x, int width)
    {
        // PC129: only panel backgrounds are translucent. Keep this non-interactive fill
        // exactly beneath the existing button; geometry is still updated only on resize.
        SetRect(GetChildById(opaqueFillId), x, 0, width, 34);
        SetRect(button, x, 0, width, 34);
        SetRect(GetChildById(iconId), x + 10, -7, 20, 20);

        // Reserve a fixed right-hand pocket INSIDE the button for the keyboard binding.
        // Caption gets the space between the left icon and that pocket.
        const int shortcutWidth = 26;
        const int shortcutRightMargin = 10;
        int shortcutX = x + width - shortcutRightMargin - shortcutWidth;
        SetRect(shortcut?.Controller, shortcutX, -10, shortcutWidth, 24);

        int captionX = x + 34;
        int captionWidth = Math.Max(34, shortcutX - captionX - 4);
        SetRect(caption?.Controller, captionX, -10, captionWidth, 24);
    }

    private void Craft_OnPress(XUiController sender, int mouseButton)
    {
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return;
        if (details?.CommandBridge == null || details.SelectedRecipe == null) return;
        details.CommandBridge.ExecuteCraft(details.SelectedRecipe, details.SelectedCraftingTier);
        details.RefreshNow();
    }

    private void Favorite_OnPress(XUiController sender, int mouseButton)
    {
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return;
        if (details?.CommandBridge == null || details.SelectedRecipe == null) return;
        details.CommandBridge.ExecuteFavorite(details.SelectedRecipe);
        details.RefreshNow();
    }

    private void Track_OnPress(XUiController sender, int mouseButton)
    {
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return;
        if (details?.CommandBridge == null || details.SelectedRecipe == null) return;
        details.CommandBridge.ExecuteTrack(details.SelectedRecipe, details.SelectedCraftingTier);
        details.RefreshNow();
    }

    private void Explorer_OnPress(XUiController sender, int mouseButton)
    {
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return;
        Recipe recipe = details != null ? details.SelectedRecipe : null;
        string focusId;
        if (!TryGetRecipeExplorerFocus(recipe, out focusId) || xui == null)
            return;

        string returnLabelKey = "xuiRebirthProgressionExplorerReturnCrafting";
        string returnLabel = Localization.Get(returnLabelKey);
        if (string.IsNullOrEmpty(returnLabel) || string.Equals(returnLabel, returnLabelKey, StringComparison.OrdinalIgnoreCase))
            returnLabel = "Return to Crafting";

        RebirthProgressionExplorerReturnContext returnContext = new RebirthProgressionExplorerReturnContext(
            "crafting", string.Empty, returnLabel);
        RebirthProgressionExplorerLaunchRequest request = new RebirthProgressionExplorerLaunchRequest(
            focusId, RebirthProgressionExplorerMode.LiveCharacter, "personal-crafting-recipe", returnContext);

        string error;
        if (!RebirthProgressionExplorerUiService.Open(xui, request, out error) && !string.IsNullOrEmpty(error))
            Log.Error("[REBIRTH Crafting] Progression Explorer open failed for " + focusId + ": " + error);
    }

    private static bool TryGetRecipeExplorerFocus(Recipe recipe, out string focusId)
    {
        focusId = string.Empty;
        if (recipe == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return false;

        string recipeName;
        try { recipeName = recipe.GetName() ?? string.Empty; }
        catch { return false; }
        if (recipeName.Length == 0)
            return false;

        focusId = RebirthProgressionGraphRegistry.RecipeNodeId(recipeName);
        RebirthProgressionGraphNode node;
        return RebirthProgressionGraphRegistry.TryGetNode(focusId, out node) && node != null;
    }

    private void RefreshShortcutLabels()
    {
        if (xui?.playerUI?.playerInput == null)
            return;

        PlayerActionsGUI actions = xui.playerUI.playerInput.GUIActions;
        SetShortcut(craftShortcut, BindingText(actions.DPad_Up));
        SetShortcut(favoriteShortcut, BindingText(actions.DPad_Right));
        SetShortcut(trackShortcut, BindingText(actions.DPad_Left));
        SetShortcut(explorerShortcut, string.Empty);

        bool keyboard = IsKeyboardStyle();
        SetVisible(craftShortcut?.Controller, keyboard);
        SetVisible(favoriteShortcut?.Controller, keyboard);
        bool trackVisible = trackButton?.ViewComponent == null || trackButton.ViewComponent.IsVisible;
        SetVisible(trackShortcut?.Controller, keyboard && trackVisible);
        SetVisible(explorerShortcut?.Controller, false);
    }

    private bool IsKeyboardStyle()
    {
        PlayerInputManager.InputStyle style = PlatformManager.NativePlatform.Input.CurrentInputStyle;
        return style == PlayerInputManager.InputStyle.Keyboard ||
               style == PlayerInputManager.InputStyle.Count ||
               style == PlayerInputManager.InputStyle.Undefined;
    }

    private static string BindingText(PlayerAction action)
    {
        if (action == null)
            return string.Empty;
        string value = action.GetBindingString(
            false,
            _emptyStyle: XUiUtils.EmptyBindingStyle.EmptyString,
            _displayStyle: XUiUtils.DisplayStyle.KeyboardWithAngleBrackets);
        value = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (value.Length >= 2 && value[0] == '<' && value[value.Length - 1] == '>')
            value = value.Substring(1, value.Length - 2);
        return value;
    }

    private static void SetShortcut(XUiV_Label label, string text)
    {
        if (label == null)
            return;
        text = text ?? string.Empty;
        if (!string.Equals(label.Text, text, StringComparison.Ordinal))
            label.SetTextImmediately(text);
        if (label.FontSize != 17)
            label.FontSize = 17;
    }


    private void SetActionContentColor(string iconId, XUiV_Label caption, XUiV_Label shortcut, bool enabled)
    {
        Color desired = enabled ? new Color32(240, 240, 244, 255) : new Color32(120, 120, 126, 255);
        XUiV_Sprite icon = GetView<XUiV_Sprite>(iconId);
        if (icon != null && icon.Color != desired)
            icon.Color = desired;
        if (caption != null && caption.Color != desired)
            caption.Color = desired;
        SetShortcutColor(shortcut, enabled);
    }

    private static void SetShortcutColor(XUiV_Label label, bool enabled)
    {
        if (label == null)
            return;
        Color desired = enabled ? new Color32(240, 240, 244, 255) : new Color32(120, 120, 126, 255);
        if (label.Color != desired)
            label.Color = desired;
    }

    private void WireActionButton(XUiController controller, XUiEvent_OnPressEventHandler pressHandler)
    {
        if (controller == null)
            return;
        controller.OnPress += pressHandler;
        controller.OnHover += Action_OnHover;
        ApplyActionNormalVisual(controller);
    }

    private void Action_OnHover(XUiController sender, bool isOver)
    {
        XUiV_Sprite background = sender?.ViewComponent as XUiV_Sprite;
        if (background == null)
            return;

        // This is intentionally the same transition used by native XUiC_ItemActionEntry.OnHover:
        // white ui_game_select_row on hover, normal menu_empty presentation on exit.
        if (isOver && sender.ViewComponent.Enabled)
        {
            background.Color = Color.white;
            background.SpriteName = "ui_game_select_row";
        }
        else
        {
            ApplyActionNormalVisual(sender);
        }
    }

    private static void ApplyActionNormalVisual(XUiController controller)
    {
        XUiV_Sprite background = controller?.ViewComponent as XUiV_Sprite;
        if (background == null)
            return;
        background.Color = new Color32(240, 240, 244, 255);
        background.SpriteName = "menu_empty2px";
    }

    private static void SetEnabled(XUiController controller, bool enabled)
    {
        if (controller?.ViewComponent == null) return;
        if (controller.ViewComponent.Enabled != enabled)
            controller.ViewComponent.Enabled = enabled;
        if (controller.ViewComponent.IsNavigatable != enabled)
            controller.ViewComponent.IsNavigatable = enabled;
        if (controller.ViewComponent.IsSnappable != enabled)
            controller.ViewComponent.IsSnappable = enabled;
        if (!enabled)
            ApplyActionNormalVisual(controller);
    }

    private static void SetVisible(XUiController controller, bool visible)
    {
        if (controller?.ViewComponent != null && controller.ViewComponent.IsVisible != visible)
            controller.ViewComponent.IsVisible = visible;
    }

    private T GetView<T>(string id) where T : XUiView
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as T : null;
    }

    private static void SetRect(XUiController controller, int x, int y, int width, int height)
    {
        if (controller?.ViewComponent == null) return;
        Vector2i position = new Vector2i(x, y);
        Vector2i size = new Vector2i(Math.Max(1, width), Math.Max(1, height));
        if (controller.ViewComponent.Position.x != position.x || controller.ViewComponent.Position.y != position.y)
            controller.ViewComponent.Position = position;
        if (controller.ViewComponent.Size.x != size.x || controller.ViewComponent.Size.y != size.y)
            controller.ViewComponent.Size = size;
    }
}
