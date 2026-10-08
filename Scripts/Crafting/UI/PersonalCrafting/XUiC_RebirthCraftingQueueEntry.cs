using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Stable presentation for one real native XUiC_RecipeStack queue slot.
/// The native RecipeStack remains authoritative for craft timing/output/cancel/refund, but all
/// visible card widgets are Rebirth-owned display children. Native SetRecipe/CopyTo/RefreshQueue
/// can therefore compact backing slots without clearing/rebuilding the widgets the player sees.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingQueueEntry : XUiC_RecipeStack
{
    private float nextCookingPresentation;
    private int displayIndex = -1;
    private int lastRecipeIdentity = int.MinValue;
    private int lastDisplayedCount = int.MinValue;
    private int lastDisplayedWholeSecond = int.MinValue;
    private bool lastCrafting;
    private int lastLayoutWidth = -1;
    private int lastLayoutHeight = -1;

    private XUiController nativeBackground;
    private XUiController nativeCancelSprite;
    private XUiController displayCancel;
    private XUiV_Sprite displayIcon;
    private XUiV_Sprite progressFill;
    private XUiV_Label nameLabel;
    private XUiV_Label statusLabel;
    private XUiV_Label countLabel;
    private XUiV_Label timerLabel;
    private XUiController cookingDetails, cookingExplanationReader, cookingExplanationViewport;
    private XUiV_Label cookingTitle, cookingExplanation, cookingQuality;
    private bool cancelInputAllowed = true;

    public bool HasRecipeForPresentation => GetRecipe() != null;

    public override void Init()
    {
        // Required: native RecipeStack owns queue transactions and refund behavior.
        base.Init();

        nativeBackground = GetChildById("background");
        nativeCancelSprite = GetChildById("cancel");
        displayCancel = GetChildById("rebirthQueueCancelButton");
        displayIcon = GetChildById("rebirthQueueDisplayIcon")?.ViewComponent as XUiV_Sprite;
        progressFill = GetChildById("rebirthQueueProgressFill")?.ViewComponent as XUiV_Sprite;
        nameLabel = GetChildById("rebirthQueueName")?.ViewComponent as XUiV_Label;
        statusLabel = GetChildById("rebirthQueueStatus")?.ViewComponent as XUiV_Label;
        countLabel = GetChildById("rebirthQueueDisplayCount")?.ViewComponent as XUiV_Label;
        timerLabel = GetChildById("rebirthQueueDisplayTimer")?.ViewComponent as XUiV_Label;

        cookingDetails = GetChildById("rebirthCookingDetails");
        cookingExplanationReader = cookingDetails?.GetChildById("detailConsequencesReader");
        cookingExplanationViewport = cookingExplanationReader?.GetChildById("readableTextViewport");
        cookingTitle = cookingDetails?.GetChildById("detailTitle")?.ViewComponent as XUiV_Label;
        cookingExplanation = cookingDetails?.GetChildById("detailConsequences")?.ViewComponent as XUiV_Label;
        cookingQuality = cookingDetails?.GetChildById("detailQuality")?.ViewComponent as XUiV_Label;

        if (displayCancel != null)
            displayCancel.OnPress += Cancel_OnPress;

        var take=GetChildById("rebirthCookingTake");
        if(take!=null)take.OnPress+=(sender,button)=>{
            if((button!=0&&button!=-1)||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return;
            if(RebirthCookingHeat.Ready(recipe))RebirthCookingHeat.Take(this);else RebirthCookingHeat.Cancel(this);
        };
        PrepareNativeContract();
        ApplyInternalLayoutIfNeeded();
        RefreshDisplayPresentation(true);
        ApplyCancelButtonState();
    }

    public void RefreshPresentationNow()
    {
        if (ViewComponent == null)
            return;
        ApplyInternalLayoutIfNeeded();
        RefreshDisplayPresentation(true);
        ApplyCancelButtonState();
    }

    public void SyncPresentationAfterNativeQueuePass()
    {
        if (ViewComponent == null || !ViewComponent.IsVisible)
            return;
        RefreshDisplayPresentation(false);
        ApplyCancelButtonState();
    }

    /// <summary>
    /// Disable the visible cancel collider while that X is clipped by a viewport edge.
    /// </summary>
    public void SetCancelHitEnabled(bool allowed)
    {
        if (cancelInputAllowed == allowed)
            return;
        cancelInputAllowed = allowed;
        ApplyCancelButtonState();
    }

    public int DisplayIndex
    {
        get => displayIndex;
        set
        {
            if (displayIndex == value)
                return;
            displayIndex = value;
            XUiV_Label indexLabel = GetChildById("rebirthQueueIndex")?.ViewComponent as XUiV_Label;
            string text = (displayIndex + 1).ToString();
            if (indexLabel != null && !string.Equals(indexLabel.Text, text, StringComparison.Ordinal))
                indexLabel.SetTextImmediately(text);
        }
    }

    public override void Update(float dt)
    {
        // The active native stack and a completed inventory-full retry need XUiC_RecipeStack.Update. Queued backing
        // slots remain native data containers and are advanced/shifted by XUiC_CraftingQueue.
        bool managed=RebirthCookingHeat.Managed(recipe);
        if(managed)
        {
            long profile=RebirthCookingDiagnostics.Begin();
            RebirthCookingHeat.TickUi(this,dt);
            RebirthCookingDiagnostics.Heat(profile);
        }
        bool nativeTick = HasRecipeForPresentation && (IsCrafting || isInventoryFull) && !managed;
        if (nativeTick)
            base.Update(dt);

        // Paid personal jobs still run native timing/output above while their window is closed.
        // IsVisible alone describes the authored card, not whether its window is showing.
        if (windowGroup?.isShowing != true || ViewComponent == null || !ViewComponent.IsVisible)
            return;

        ApplyInternalLayoutIfNeeded();
        RefreshDisplayPresentation(false);
        ApplyCancelButtonState();

        // Active base.Update already updates the view tree. Queued cards only need lightweight
        // hover/widget flushing; do not invoke native RecipeStack processing for them.
        if (!nativeTick && HasRecipeForPresentation)
            UpdatePresentationTree(dt);
    }

    private void Cancel_OnPress(XUiController sender, int mouseButton)
    {
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return;
        if ((mouseButton != 0 && mouseButton != -1) || !cancelInputAllowed || !HasRecipeForPresentation)
            return;

        if(RebirthCookingHeat.Managed(recipe)){RebirthCookingHeat.Cancel(this);return;}

        // This is the public native entry point into RecipeStack.HandleOnPress. It performs the
        // authoritative ingredient/original-item refund, overflow drop, ClearRecipe and Owner.RefreshQueue.
        // Native HandleOnPress also ends with a broad windowGroup.Controller.SetAllChildrenDirty();
        // scope only THAT redundant legacy invalidation away so cancellation does not redraw the
        // whole Personal Crafting tree.
        XUiC_RebirthPersonalCrafting personalCrafting = GetParentByType<XUiC_RebirthPersonalCrafting>();
        personalCrafting?.BeginIncrementalQueueMutation();
        try
        {
            ForceCancel();
        }
        finally
        {
            personalCrafting?.EndIncrementalQueueMutation();
        }

        // Owner.RefreshQueue is synchronous. Ask the Rebirth presenter for one immediate,
        // change-aware post-mutation pass so the same visible card positions are retained and only
        // the now-unused trailing card disappears.
        XUiC_RebirthCraftingQueue rebirthQueue = Owner as XUiC_RebirthCraftingQueue;
        if (rebirthQueue != null)
            rebirthQueue.SyncVisiblePresentationAfterNativeMutation();
        else
        {
            RefreshDisplayPresentation(true);
            ApplyCancelButtonState();
            if (!HasRecipeForPresentation && ViewComponent != null && ViewComponent.IsVisible)
                ViewComponent.IsVisible = false;
        }

        personalCrafting?.RefreshAfterIncrementalQueueMutation();
    }

    private void RefreshDisplayPresentation(bool force)
    {
        Recipe recipe = GetRecipe();
        int recipeIdentity = recipe != null ? recipe.GetHashCode() : 0;
        bool craftingNow = recipe != null && IsCrafting;
        bool identityChanged = force || recipeIdentity != lastRecipeIdentity;
        bool craftingChanged = force || craftingNow != lastCrafting;

        if (recipe == null)
        {
            lastRecipeIdentity = 0;
            lastCrafting = false;
            lastDisplayedCount = int.MinValue;
            lastDisplayedWholeSecond = int.MinValue;
            SetLabelText(nameLabel, string.Empty);
            SetLabelText(statusLabel, string.Empty);
            SetLabelText(countLabel, string.Empty);
            SetLabelText(timerLabel, string.Empty);
            SetVisible(displayIcon, false);
            SetProgress(0f, false);
            return;
        }

        if(RebirthCookingHeat.Managed(recipe))
        {
            if(!force&&Time.unscaledTime<nextCookingPresentation)return;
            nextCookingPresentation=Time.unscaledTime+.25f;
            SetLabelText(nameLabel,RebirthCookingHeat.Name(recipe));
            SetLabelText(statusLabel,RebirthCookingHeat.Status(recipe,(windowGroup.Controller as XUiC_RebirthCookingStation)?.WorkstationData.GetIsBurning()??false));
            SetLabelText(timerLabel,RebirthCookingHeat.Timer(recipe));
            SetLabelText(countLabel,(recipeCount*recipe.count).ToString());
            if(displayIcon!=null){displayIcon.SpriteName=RebirthCookingHeat.Icon(recipe);displayIcon.IsVisible=true;}
            SetRectIfChanged(GetChildById("rebirthQueueDisplayTimer"),111,-68,250,18);
            SetProgress(1-RebirthCookingHeat.Remaining(recipe)/Math.Max(1,RebirthCookingHeat.Number(recipe,"duration")),true);
            bool ready=RebirthCookingHeat.Ready(recipe);
            var action=GetChildById("rebirthCookingTake");
            SetRectIfChanged(action,ViewComponent.Size.x-146,-10,136,32);
            SetLabelText(action?.GetChildById("label")?.ViewComponent as XUiV_Label,Localization.Get(ready?"rbCookingTake":"rbCookingCancel"));
            SetLabelText(action?.GetChildById("shortcut")?.ViewComponent as XUiV_Label,ready?xui.playerUI.playerInput.PermanentActions.Reload.GetBindingString(false,_emptyStyle:XUiUtils.EmptyBindingStyle.EmptyString,_displayStyle:XUiUtils.DisplayStyle.KeyboardWithAngleBrackets).Trim('<','>'):"");
            if(action?.GetChildById("actionIcon")?.ViewComponent is XUiV_Sprite actionIcon)actionIcon.SpriteName=ready?"ui_game_symbol_store_all_up":"ui_game_symbol_x";
            SetRectIfChanged(GetChildById("rebirthQueueName"),69,-5,Math.Max(80,ViewComponent.Size.x-225),21);
            SetRectIfChanged(GetChildById("rebirthQueueStatus"),69,-25,Math.Max(80,ViewComponent.Size.x-225),18);
            GetChildById("rebirthQueueIndex").ViewComponent.IsVisible=false;
            GetChildById("rebirthQueueIndexBox").ViewComponent.IsVisible=false;
            RenderCookingDetails(recipe);
            return;
        }
        if (identityChanged)
        {
            SetLabelText(nameLabel, RebirthCraftingQueueBridge.GetLocalizedRecipeName(this));
            ItemClass itemClass = ItemClass.GetForId(recipe.itemValueType);
            if (displayIcon != null && itemClass != null)
            {
                string spriteName = itemClass.GetIconName();
                if (!string.Equals(displayIcon.SpriteName, spriteName, StringComparison.Ordinal))
                    displayIcon.SetSpriteImmediately(spriteName);
                Color tint = itemClass.GetIconTint();
                if (displayIcon.Color != tint)
                    displayIcon.Color = tint;
                SetVisible(displayIcon, true);
            }
        }

        int totalCount = GetRecipeCount() * Math.Max(1, recipe.count);
        if (force || totalCount != lastDisplayedCount)
        {
            lastDisplayedCount = totalCount;
            SetLabelText(countLabel, totalCount.ToString());
        }

        int wholeSecond = Math.Max(0, (int)(GetTotalRecipeCraftingTimeLeft() + 0.5f));
        if (force || wholeSecond != lastDisplayedWholeSecond)
        {
            lastDisplayedWholeSecond = wholeSecond;
            SetLabelText(timerLabel, FormatCraftTime(wholeSecond));
        }

        if (craftingChanged)
        {
            SetLabelText(statusLabel, craftingNow
                ? RebirthCraftingQueueBridge.Localize("xuiRebirthQueueCrafting", "Crafting")
                : RebirthCraftingQueueBridge.Localize("xuiRebirthQueueQueued", "Queued"));
            if (statusLabel != null)
            {
                Color desired = craftingNow
                    ? (Color)new Color32(232, 192, 100, 255)
                    : (Color)new Color32(181, 140, 255, 255);
                if (statusLabel.Color != desired)
                    statusLabel.Color = desired;
            }
        }

        SetProgress(craftingNow ? RebirthCraftingQueueBridge.GetCurrentItemProgress(this) : 0f, craftingNow);
        lastRecipeIdentity = recipeIdentity;
        lastCrafting = craftingNow;
    }

    private void SetProgress(float progress, bool crafting)
    {
        if (progressFill == null)
            return;

        float clamped = Mathf.Clamp01(progress);
        if (Mathf.Abs(progressFill.Fill - clamped) > 0.0005f)
            progressFill.Fill = clamped;

        Color desired = crafting
            ? (Color)new Color32(232, 192, 100, 255)
            : (Color)new Color32(110, 86, 155, 255);
        if (progressFill.Color != desired)
            progressFill.Color = desired;
    }

    private void RenderCookingDetails(Recipe batch)
    {
        var details=cookingDetails;
        if(details==null)return;
        details.ViewComponent.IsVisible=true;
        int width=ViewComponent.Size.x-28;
        SetRectIfChanged(details,14,-101,width,Math.Max(176,ViewComponent.Size.y-115));
        if(cookingTitle!=null && cookingTitle.Size.x!=width)cookingTitle.Size=new Vector2i(width,cookingTitle.Size.y);
        if(cookingQuality!=null && cookingQuality.Size.x!=width)cookingQuality.Size=new Vector2i(width,cookingQuality.Size.y);
        var explanationReader=cookingExplanationReader;
        SetRectIfChanged(explanationReader,0,-26,width,48);
        SetRectIfChanged(cookingExplanationViewport,0,0,Math.Max(1,width-22),48);
        var explanationLabel=cookingExplanation;
        if(explanationLabel!=null && explanationLabel.Size.x!=Math.Max(1,width-28))
            explanationLabel.Size=new Vector2i(Math.Max(1,width-28),explanationLabel.Size.y);
        bool ready=RebirthCookingHeat.Ready(batch),burnt=RebirthCookingHeat.Burnt(batch);
        bool cold=!RebirthCookingBatch.NeedsHeat(batch);
        bool over=ready&&!cold&&RebirthCookingHeat.Number(batch,"overdue")>=RebirthCookingHeatRules.Grace;
        string phase=burnt?"Burnt":!ready?"Cooking":cold?"Cold":over?"Overcooking":"Ready";
        SetLabelText(cookingTitle,Localization.Get("rbCookingDetail"+phase));
        string explanation=Localization.Get("rbCookingConsequence"+phase);
        var station=windowGroup.Controller as XUiC_RebirthCookingStation;
        if(ready&&!cold&&!burnt&&!(station?.WorkstationData.GetIsBurning()??false))explanation=Localization.Get("rbCookingDeteriorationPaused")+" "+explanation;
        SetLabelText(cookingExplanation,explanation);
        // The very same item calculation is used by Take(), including the burnt-item replacement.
        ItemValue output=RebirthCookingHeat.Output(batch);
        ItemValue baseline=RebirthCookingBatch.Preview(batch);
        int rowY=80;
        void Stat(string id,string key,string unit,bool visible=true)
        {
            var control=details.GetChildById(id);
            var icon=details.GetChildById(id+"Icon");
            control.ViewComponent.IsVisible=visible;
            if(icon!=null)icon.ViewComponent.IsVisible=visible;
            if(!visible)return;
            SetRectIfChanged(control,28,-rowY,width-28,24);
            SetRectIfChanged(icon,0,-rowY,20,20);
            float value=RebirthCookingItemStats.Value(output,key),original=RebirthCookingItemStats.Value(baseline,key);
            SetLabelText(control.ViewComponent as XUiV_Label,Localization.Get("rbCookingStat"+key)+"  "+RebirthCookingItemStats.Display(value,original,unit));
            rowY+=24;
        }
        Stat("detailNutrition","nutrition","");Stat("detailWater","water"," mL");
        Stat("detailComfort","comfort","");
        Stat("detailEnergy","energy","",RebirthCookingItemStats.Value(output,"energy")!=0||RebirthCookingItemStats.Value(baseline,"energy")!=0);
        if(cookingQuality!=null)cookingQuality.IsVisible=false;

    }

    private void UpdatePresentationTree(float dt)
    {
        if (ViewComponent != null)
            ViewComponent.Update(dt);
        if (Children == null)
            return;
        for (int i = 0; i < Children.Count; i++)
        {
            XUiController child = Children[i];
            if (child != null)
                child.Update(dt);
        }
    }

    private static string FormatCraftTime(int wholeSeconds)
    {
        int whole = Math.Max(0, wholeSeconds);
        return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
    }

    private void ApplyInternalLayoutIfNeeded()
    {
        if (ViewComponent == null)
            return;

        Vector2i size = ViewComponent.Size;
        if (size.x == lastLayoutWidth && size.y == lastLayoutHeight)
            return;
        lastLayoutWidth = size.x;
        lastLayoutHeight = size.y;

        int width = Math.Max(150, size.x);
        int height = Math.Max(84, size.y);
        const int pad = 7;
        const int iconSize = 54;
        const int indexSize = 22;
        const int cancelSize = 18;
        int iconX = pad;
        int contentX = iconX + iconSize + 8;
        int indexX = width - pad - indexSize;
        int contentRight = indexX - 6;
        int contentWidth = Math.Max(66, contentRight - contentX);
        const int progressY = -58;
        const int progressHeight = 8;

        SetRectIfChanged(GetChildById("rebirthQueueCardBg"), 0, 0, width, height);
        SetRectIfChanged(GetChildById("rebirthQueueCardFrame"), 0, 0, width, height);
        SetRectIfChanged(GetChildById("rebirthQueueIndexBox"), indexX, -7, indexSize, indexSize);
        SetRectIfChanged(GetChildById("rebirthQueueIndex"), indexX, -11, indexSize, 22);
        SetRectIfChanged(GetChildById("rebirthQueueDisplayIcon"), iconX, -7, iconSize, iconSize);
        SetRectIfChanged(GetChildById("rebirthQueueName"), contentX, -5, contentWidth, 21);
        SetRectIfChanged(GetChildById("rebirthQueueStatus"), contentX, -25, contentWidth, 18);
        SetRectIfChanged(GetChildById("rebirthQueueQtyCaption"), contentX, -40, 34, 18);
        SetRectIfChanged(GetChildById("rebirthQueueDisplayCount"), contentX + 36, -40, 40, 18);
        SetRectIfChanged(GetChildById("rebirthQueueProgressBg"), contentX, progressY, contentWidth, progressHeight);
        SetRectIfChanged(GetChildById("rebirthQueueProgressFill"), contentX, progressY, contentWidth, progressHeight);
        SetRectIfChanged(GetChildById("rebirthQueueTimeCaption"), contentX, -68, 40, 18);
        SetRectIfChanged(GetChildById("rebirthQueueDisplayTimer"), contentX + 42, -68, 58, 18);

        int cancelX = width - pad - cancelSize;
        SetRectIfChanged(displayCancel, cancelX, -36, cancelSize, cancelSize);

        SetLabelFont("rebirthQueueName", 16);
        SetLabelFont("rebirthQueueStatus", 15);
        SetLabelFont("rebirthQueueQtyCaption", 15);
        SetLabelFont("rebirthQueueDisplayCount", 15);
        SetLabelFont("rebirthQueueTimeCaption", 15);
        SetLabelFont("rebirthQueueDisplayTimer", 15);
    }

    private void PrepareNativeContract()
    {
        // Base RecipeStack requires these exact child IDs/types. They remain present but are never
        // the visible presentation, so native SetRecipe/updateRecipeData churn cannot flash onscreen.
        if (nativeBackground?.ViewComponent != null)
        {
            nativeBackground.ViewComponent.IsVisible = false;
            nativeBackground.ViewComponent.Enabled = false;
        }
        XUiV_Sprite native = nativeCancelSprite?.ViewComponent as XUiV_Sprite;
        if (native != null)
        {
            native.Color = new Color32(0, 0, 0, 0);
            native.IsVisible = false;
        }
    }

    private void ApplyCancelButtonState()
    {
        if (displayCancel?.ViewComponent == null)
            return;

        bool managed=RebirthCookingHeat.Managed(recipe);
        if(GetChildById("rebirthCookingDetails")?.ViewComponent is XUiView details)details.IsVisible=managed;
        if(GetChildById("rebirthCookingTake")?.ViewComponent is XUiView take)take.IsVisible=managed;
        bool hasRecipe = HasRecipeForPresentation && !managed;
        bool enabled = hasRecipe && cancelInputAllowed;
        if (displayCancel.ViewComponent.IsVisible != hasRecipe)
            displayCancel.ViewComponent.IsVisible = hasRecipe;
        if (displayCancel.ViewComponent.Enabled != enabled)
            displayCancel.ViewComponent.Enabled = enabled;
    }

    private void SetLabelFont(string id, int size)
    {
        XUiV_Label label = GetChildById(id)?.ViewComponent as XUiV_Label;
        if (label != null && label.FontSize != size)
            label.FontSize = size;
    }

    private static void SetLabelText(XUiV_Label label, string text)
    {
        if (label == null)
            return;
        text = text ?? string.Empty;
        if (!string.Equals(label.Text, text, StringComparison.Ordinal))
            label.SetTextImmediately(text);
    }

    private static void SetVisible(XUiView view, bool visible)
    {
        if (view != null && view.IsVisible != visible)
            view.IsVisible = visible;
    }

    private static void SetRectIfChanged(XUiController controller, int x, int y, int width, int height)
    {
        if (controller?.ViewComponent == null)
            return;
        Vector2i position = new Vector2i(x, y);
        Vector2i size = new Vector2i(Math.Max(1, width), Math.Max(1, height));
        if (controller.ViewComponent.Position.x != position.x || controller.ViewComponent.Position.y != position.y)
            controller.ViewComponent.Position = position;
        if (controller.ViewComponent.Size.x != size.x || controller.ViewComponent.Size.y != size.y)
            controller.ViewComponent.Size = size;
    }
}
