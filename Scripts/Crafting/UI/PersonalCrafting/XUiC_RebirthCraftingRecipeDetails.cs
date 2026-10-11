using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Permanent upper-center recipe region for Rebirth Personal Crafting.
/// Inventory selection is intentionally unable to replace this controller or its visual tree.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingRecipeDetails : XUiController
{
    private RebirthCraftingPresentation owner;
    private XUiC_RebirthCraftingRecipeCatalogue catalogue;
    private XUiC_RecipeCraftCount craftCount;
    private XUiC_RebirthCraftingActions actions;
    private XUiController knowledgeButton;

    private XUiV_Sprite outputIcon;
    private XUiV_Label nameLabel;
    private XUiV_Label typeLabel;
    private XUiV_Label descriptionLabel;
    private XUiV_Label knowledgeLabel;
    private XUiV_Label timeTitleLabel;
    private XUiV_Label timeValueLabel;
    private XUiV_Label emptyLabel;

    private Recipe selectedRecipe;
    private int selectedCraftingTier = 1;
    private int lastCount = -1;
    private Vector2i lastSize = new Vector2i(-1, -1);

    // Inventory events only invalidate the selected recipe's presentation. Coalesce the
    // actual refresh outside the native ingredient-removal/remote-grant transaction.
    private XUiM_PlayerInventory observedInventory;
    private bool availabilityDirty = true;
    private bool refreshingCraftCount;
    private long lastRemoteRevision = long.MinValue;
    private float nextAvailabilityCheck;
    private float nextAvailabilitySafetyRefresh;

    public Recipe SelectedRecipe => selectedRecipe;
    public int SelectedCraftingTier => selectedCraftingTier;
    public RebirthCraftingCommandBridge CommandBridge { get; private set; }

    public override void Init()
    {
        base.Init();
        owner = RebirthCraftingPresentation.Resolve(this);
        catalogue = owner != null ? owner.GetChildByType<XUiC_RebirthCraftingRecipeCatalogue>() : null;
        craftCount = owner != null ? owner.GetChildByType<XUiC_RecipeCraftCount>() : null;
        actions = GetChildByType<XUiC_RebirthCraftingActions>();
        knowledgeButton = GetChildById("btnRebirthCraftingViewKnowledge");

        outputIcon = GetView<XUiV_Sprite>("rebirthCraftingSelectedRecipeIcon");
        nameLabel = GetView<XUiV_Label>("rebirthCraftingSelectedRecipeName");
        typeLabel = GetView<XUiV_Label>("rebirthCraftingSelectedRecipeType");
        descriptionLabel = GetView<XUiV_Label>("rebirthCraftingSelectedRecipeDescription");
        knowledgeLabel = GetView<XUiV_Label>("rebirthCraftingSelectedRecipeKnowledge");
        timeTitleLabel = GetView<XUiV_Label>("rebirthCraftingSelectedRecipeTimeTitle");
        timeValueLabel = GetView<XUiV_Label>("rebirthCraftingSelectedRecipeTimeValue");
        emptyLabel = GetView<XUiV_Label>("rebirthCraftingSelectedRecipeEmpty");

        if (catalogue != null) catalogue.RebirthSelectionChanged += Catalogue_SelectionChanged;
        if (craftCount != null) craftCount.OnCountChanged += CraftCount_OnCountChanged;
        if (knowledgeButton != null)
        {
            knowledgeButton.OnPress += Knowledge_OnPress;
            knowledgeButton.OnHover += (sender, over) =>
            {
                var background = sender.ViewComponent as XUiV_Sprite;
                if(background != null) { background.SpriteName = over ? "ui_game_select_row" : "menu_empty2px"; background.Color = over ? Color.white : new Color32(240,240,244,255); }
            };
        }
        GetChildById("qualityDown").OnPress += (sender, button) => ChangeQuality(-1);
        GetChildById("qualityUp").OnPress += (sender, button) => ChangeQuality(1);

        CommandBridge = new RebirthCraftingCommandBridge(owner, catalogue, craftCount);
        ApplyGeometry(true);
        ClearVisuals();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ObserveInventory();
        availabilityDirty = true;
        lastRemoteRevision = long.MinValue;
        nextAvailabilityCheck = nextAvailabilitySafetyRefresh = 0f;
        ApplyRecipe(owner?.Coordinator?.SelectedRecipe ?? (catalogue != null ? catalogue.CurrentRecipe : null), true);
    }

    public override void OnClose()
    {
        UnsubscribeInventory();
        availabilityDirty = true;
        base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (owner == null || !owner.State.IsOpen) return;
        ObserveInventory();
        ApplyGeometry(false);

        Recipe current = owner?.Coordinator?.SelectedRecipe ?? (catalogue != null ? catalogue.CurrentRecipe : null);
        if (current != selectedRecipe)
            ApplyRecipe(current, true);

        // Preserve the zero state here: Math.Max(1, Count) hid the 1 -> 0 transition.
        int count = craftCount != null ? craftCount.Count : 0;
        if (count != lastCount)
        {
            RecordBatchCount(count);
            Render();
        }

        float now = Time.realtimeSinceStartup;
        if (now < nextAvailabilityCheck) return;
        nextAvailabilityCheck = now + 0.20f;
        RefreshToolRequirement();
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        long remoteRevision = connection != null && connection.IsServer
            ? RemoteResourceSnapshotCache.ProjectionRevision
            : RemoteResourceClientAvailability.ProjectionRevision;
        if (availabilityDirty || remoteRevision != lastRemoteRevision || now >= nextAvailabilitySafetyRefresh)
        {
            lastRemoteRevision = remoteRevision;
            RefreshAvailability();
            nextAvailabilitySafetyRefresh = now + 1f;
        }
    }

    public void RequestAvailabilityRefresh()
    {
        availabilityDirty = true;
    }

    private void ObserveInventory()
    {
        XUiM_PlayerInventory current = xui != null ? xui.PlayerInventory : null;
        if (ReferenceEquals(current, observedInventory)) return;
        UnsubscribeInventory();
        observedInventory = current;
        if (observedInventory == null) return;
        observedInventory.OnBackpackItemsChanged += RequestAvailabilityRefresh;
        observedInventory.OnToolbeltItemsChanged += RequestAvailabilityRefresh;
        availabilityDirty = true;
    }

    private void UnsubscribeInventory()
    {
        if (observedInventory == null) return;
        // Detach from the exact subscribed object, even if XUi has since changed inventories.
        observedInventory.OnBackpackItemsChanged -= RequestAvailabilityRefresh;
        observedInventory.OnToolbeltItemsChanged -= RequestAvailabilityRefresh;
        observedInventory = null;
    }

    private void RecordBatchCount(int count)
    {
        if (count != lastCount) owner?.AdvanceCraftIntentEpoch();
        lastCount = count;
        // Requirements/outcome may preview one craft while nothing can be submitted.
        // Transaction eligibility always uses the native, UNCLAMPED Count instead.
        owner?.Coordinator?.RecordBatch(Math.Max(1, count));
    }

    private void RecalculateCraftCount(bool resetCount = false)
    {
        if (refreshingCraftCount || craftCount == null || selectedRecipe == null) return;
        if (!resetCount && RemoteResourceClientTransactionCoordinator.HasPendingCraftForUi(xui))
        {
            // Reserved remote inputs are temporarily absent from availability snapshots.
            // Preserve the in-flight quantity until the grant commits or fails; explicit
            // selection/count changes still invalidate the intent through the native path.
            availabilityDirty = true;
            return;
        }
        int before = craftCount.Count;
        refreshingCraftCount = true;
        try
        {
            if (resetCount) craftCount.Count = 1;
            craftCount.CalculateMaxCount();
            // Native CalculateMaxCount can retain Count=0 after a previous material shortage.
            // Recover to one only after its current material calculation permits a real craft.
            if (craftCount.Count <= 0 && craftCount.MaxCount > 0)
                craftCount.Count = 1;
            if (craftCount.Count != before)
                craftCount.ForceTextRefresh();
        }
        finally
        {
            refreshingCraftCount = false;
        }
        RecordBatchCount(craftCount.Count);
    }

    private void RefreshAvailability()
    {
        availabilityDirty = false;
        int before = lastCount;
        RecalculateCraftCount();
        // Updating eligibility does not rebuild the catalogue, queue or complete item panel.
        if (before != lastCount) Render();
        else actions?.RefreshState();
    }

    public void RefreshNow()
    {
        availabilityDirty = false;
        RecalculateCraftCount();
        Render();
        owner?.GetChildByType<XUiC_RebirthCraftingRequirements>()?.RefreshNow();
        owner?.GetChildByType<XUiC_RebirthCraftingOutcome>()?.RefreshNow();
    }

    private void Catalogue_SelectionChanged(Recipe recipe)
    {
        if (recipe != selectedRecipe) owner?.AdvanceCraftIntentEpoch();
        ApplyRecipe(recipe, true);
    }

    private void CraftCount_OnCountChanged(XUiController sender, OnCountChangedEventArgs args)
    {
        // Count setters can raise this event during CalculateMaxCount. Publish the final
        // count/intent epoch only, not intermediate zero/clamp/restore steps.
        if (refreshingCraftCount) return;
        RecordBatchCount(args.Count);
        Render();
    }

    private void ApplyRecipe(Recipe recipe, bool resetCount)
    {
        bool changed = selectedRecipe != recipe;
        selectedRecipe = recipe;
        if (changed) selectedCraftingTier = ResolveCraftingTier(recipe);
        selectedCraftingTier = Math.Min(selectedCraftingTier, ResolveCraftingTier(recipe));

        if (craftCount != null)
        {
            craftCount.recipe = recipe;
            if (recipe != null)
            {
                RecalculateCraftCount(changed && resetCount);
                craftCount.ForceTextRefresh();
            }
            else
            {
                craftCount.Count = 1;
                craftCount.MaxCount = 1;
                craftCount.ForceTextRefresh();
            }
        }

        RecordBatchCount(craftCount != null ? craftCount.Count : 0);
        Render();
    }

    private void RefreshToolRequirement()
    {
        var tool=selectedRecipe!=null&&selectedRecipe.craftingToolType>0?ItemClass.GetForId(selectedRecipe.craftingToolType):null;
        var grid=owner?.GetChildByType<XUiC_WorkstationToolGrid>();
        var fuel=owner?.GetChildByType<XUiC_WorkstationFuelGrid>();
        bool missingTool=tool!=null&&!(grid?.HasRequirement(selectedRecipe)??false);
        bool missingFuel=selectedRecipe!=null&&fuel?.WorkstationData!=null&&!fuel.HasRequirement(selectedRecipe);
        var icon=GetChildById("recipeRequiredToolIcon")?.ViewComponent as XUiV_Sprite;
        var label=GetChildById("recipeRequiredToolText")?.ViewComponent as XUiV_Label;
        if(icon!=null)
        {
            icon.IsVisible=missingTool;
            if(missingTool)icon.SpriteName=tool.GetIconName();
        }
        if(label!=null)
        {
            label.IsVisible=missingTool;
            if(missingTool)label.Text=Localization.Get("xuiTools")+": "+tool.GetLocalizedItemName();
            label.Color=new Color32(255,120,120,255);
        }
        var fuelIcon=GetChildById("recipeRequiredFuelIcon")?.ViewComponent as XUiV_Sprite;
        var fuelLabel=GetChildById("recipeRequiredFuelText")?.ViewComponent as XUiV_Label;
        int fuelY=-244;
        // Reserve the warning rows below the scrollable unlock list, including when
        // tools and fuel are both missing. Neither warning may cover an unlock entry.
        var knowledge=GetChildById("rebirthCraftingSelectedRecipeKnowledge")?.ViewComponent;
        if(knowledge!=null)knowledge.Size=new Vector2i(knowledge.Size.x,missingTool&&missingFuel?140:168);
        if(icon!=null)icon.Position=new Vector2i(icon.Position.x,missingFuel?-216:-244);
        if(label!=null)label.Position=new Vector2i(label.Position.x,missingFuel?-216:-244);
        if(fuelIcon!=null){fuelIcon.IsVisible=missingFuel;fuelIcon.Position=new Vector2i(fuelIcon.Position.x,fuelY);}
        if(fuelLabel!=null){fuelLabel.IsVisible=missingFuel;fuelLabel.Position=new Vector2i(fuelLabel.Position.x,fuelY);}

    }

    private void Render()
    {
        if (selectedRecipe == null)
        {
            ClearVisuals();
            return;
        }

        ItemValue value = new ItemValue(selectedRecipe.itemValueType, selectedCraftingTier, selectedCraftingTier);
        RebirthBackpackSectionStats.Render(this, new ItemStack(value, selectedRecipe.count), "recipeResult");
        ItemClass itemClass = value.ItemClass;
        var quality = GetChildById("rebirthRecipeQuality");
        quality.ViewComponent.IsVisible = itemClass != null && itemClass.ShowQualityBar;
        (GetChildById("qualityLabel").ViewComponent as XUiV_Label).SetTextImmediately(selectedCraftingTier.ToString());
        (GetChildById("recipeQualityFill").ViewComponent as XUiV_Sprite).Color = QualityInfo.GetTierColor(selectedCraftingTier);
        GetChildById("qualityDown").ViewComponent.Enabled = selectedCraftingTier > 1;
        GetChildById("qualityUp").ViewComponent.Enabled = selectedCraftingTier < ResolveCraftingTier(selectedRecipe);
        if (outputIcon != null)
        {
            outputIcon.IsVisible = itemClass != null;
            if (itemClass != null)
            {
                outputIcon.SpriteName = value.GetPropertyOverride("CustomIcon", itemClass.GetIconName());
                outputIcon.Color = itemClass.GetIconTint(value);
            }
        }

        if (nameLabel != null) nameLabel.Text = SafeLocalized(selectedRecipe.GetName());
        if (typeLabel != null)
        {
            typeLabel.IsVisible = true;
            string display = catalogue != null ? catalogue.GetCategoryDisplayName(selectedRecipe) : string.Empty;
            typeLabel.Text = string.IsNullOrWhiteSpace(display) ? Localize("xuiRebirthPersonalCrafting", "Personal Crafting") : display;
        }
        if (descriptionLabel != null) descriptionLabel.Text = ResolveDescription(selectedRecipe);
        RefreshToolRequirement();

        EntityPlayer player = owner != null && owner.xui != null && owner.xui.playerUI != null
            ? owner.xui.playerUI.entityPlayer : null;
        int count = craftCount != null ? Math.Max(1, craftCount.Count) : 1;
        RebirthCraftOutcomeService.Snapshot snapshot = RebirthCraftOutcomeService.Build(player, selectedRecipe, count, selectedCraftingTier);
        if (knowledgeLabel != null)
        {
            // Skill requirements are rendered as separate interactive status rows.
            knowledgeLabel.Text = "";
            knowledgeLabel.Color = ParseColor(snapshot.KnowledgeColor, new Color32(210, 210, 210, 255));
        }
        if (timeTitleLabel != null) timeTitleLabel.Text = snapshot.TimeLabel;
        if (timeValueLabel != null)
        {
            float perCraft = XUiM_Recipes.GetRecipeCraftTime(owner.xui, selectedRecipe);
            timeValueLabel.Text = FormatTime(perCraft * count);
        }

        string focusId = GetKnowledgeFocusId(selectedRecipe);
        if (knowledgeButton?.ViewComponent != null)
        {
            bool enabled = !string.IsNullOrEmpty(focusId);
            knowledgeButton.ViewComponent.IsVisible = enabled;
            GetChildById("rebirthCraftingKnowledgeAction").ViewComponent.IsVisible = enabled;
            knowledgeButton.ViewComponent.Enabled = enabled;
            knowledgeButton.ViewComponent.IsNavigatable = enabled;
            knowledgeButton.ViewComponent.IsSnappable = enabled;
        }
        if (emptyLabel != null) emptyLabel.IsVisible = false;
        actions?.RefreshState();
    }

    private void ClearVisuals()
    {
        GetChildById("rebirthRecipeQuality").ViewComponent.IsVisible = false;
        if(GetChildById("recipeRequiredFuelIcon")?.ViewComponent is XUiView fi)fi.IsVisible=false;
        if(GetChildById("recipeRequiredFuelText")?.ViewComponent is XUiView ft)ft.IsVisible=false;
        if(GetChildById("recipeRequiredToolIcon")?.ViewComponent is XUiView ti)ti.IsVisible=false;
        if(GetChildById("recipeRequiredToolText")?.ViewComponent is XUiView tt)tt.IsVisible=false;
        if (outputIcon != null) outputIcon.IsVisible = false;
        if (nameLabel != null) nameLabel.Text = string.Empty;
        if (typeLabel != null) typeLabel.Text = string.Empty;
        if (descriptionLabel != null) descriptionLabel.Text = string.Empty;
        RebirthBackpackSectionStats.Render(this, ItemStack.Empty, "recipeResult");
        if (knowledgeLabel != null) knowledgeLabel.Text = string.Empty;
        if (timeTitleLabel != null) timeTitleLabel.Text = Localize("xuiRebirthCraftTime", "CRAFT TIME");
        if (timeValueLabel != null) timeValueLabel.Text = "â€”";
        if (knowledgeButton?.ViewComponent != null) knowledgeButton.ViewComponent.IsVisible = false;
        GetChildById("rebirthCraftingKnowledgeAction").ViewComponent.IsVisible = false;
        if (emptyLabel != null)
        {
            emptyLabel.Text = Localize("xuiRebirthSelectRecipePrompt", "Select a recipe to view its details.");
            emptyLabel.IsVisible = true;
        }
        actions?.RefreshState();
    }

    private void Knowledge_OnPress(XUiController sender, int mouseButton)
    {
        string focusId = GetKnowledgeFocusId(selectedRecipe);
        if (string.IsNullOrEmpty(focusId) || owner?.xui == null) return;

        RebirthProgressionExplorerReturnContext returnContext = new RebirthProgressionExplorerReturnContext(
            "crafting", string.Empty, Localize("xuiRebirthReturnToCrafting", "RETURN TO CRAFTING"));
        RebirthProgressionExplorerLaunchRequest request = new RebirthProgressionExplorerLaunchRequest(
            focusId, RebirthProgressionExplorerMode.LiveCharacter, "crafting-knowledge", returnContext);
        owner?.Coordinator?.BeginKnowledgeExplorer();
        string error;
        if (!RebirthProgressionExplorerUiService.Open(owner.xui, request, out error))
        {
            owner?.Coordinator?.CancelKnowledgeExplorerTransition();
            Log.Error("[REBIRTH Crafting UI] Could not open Knowledge in Progression Explorer. focus=" + focusId + " error=" + error);
        }
    }

    public void ApplyGeometry(bool force)
    {
        if (ViewComponent == null) return;
        Vector2i size = ViewComponent.Size;
        if (!force && size.x == lastSize.x && size.y == lastSize.y) return;
        lastSize = size;

        int width = Math.Max(480, size.x);
        int height = Math.Max(190, size.y);
        int contentTop = 46;
        int iconSize = Math.Max(72, Math.Min(112, height - 118));
        int metaWidth = Math.Max(285, Math.Min(350, (int)(width * 0.42f)));
        int leftWidth = Math.Max(210, width - metaWidth - 32);
        int iconX = 14;
        int textX = iconX + iconSize + 14;
        int textWidth = Math.Max(120, leftWidth - textX);

        SetRect(GetChildById("rebirthCraftingSelectedRecipeIconFrame"), iconX, -contentTop, iconSize, iconSize);
        SetRect(GetChildById("rebirthCraftingSelectedRecipeIcon"), iconX + 6, -(contentTop + 6), iconSize - 12, iconSize - 12);
        SetRect(GetChildById("rebirthCraftingSelectedRecipeName"), textX, -contentTop, textWidth, 30);
        SetRect(GetChildById("rebirthCraftingSelectedRecipeType"), textX, -(contentTop + 30), textWidth, 22);
        SetRect(GetChildById("rebirthCraftingSelectedRecipeDescription"), textX, -(contentTop + 56), textWidth, Math.Max(45, iconSize - 56));
        int barWidth = iconSize - 12;
        SetRect(GetChildById("rebirthRecipeQuality"), iconX + 6, -(contentTop + iconSize - 12), barWidth, 10);
        SetRect(GetChildById("recipeQualityTrack"), 0, 0, barWidth, 10);
        SetRect(GetChildById("recipeQualityFill"), 0, 0, barWidth, 10);
        SetRect(GetChildById("qualityLabel"), 0, 14, barWidth, 28);
        GetChildById("qualityDown").ViewComponent.Position = new Vector2i(10, -22);
        GetChildById("qualityUp").ViewComponent.Position = new Vector2i(barWidth - 10, -22);

        int metaX = width - metaWidth - 14;
        for (int i=0;i<7;i++)
        {
            SetRect(GetChildById("recipeResultStatName"+i), iconX, -(contentTop+140+i*20), leftWidth*3/5-14, 20);
            SetRect(GetChildById("recipeResultStatValue"+i), leftWidth*3/5, -(contentTop+140+i*20), leftWidth*2/5, 20);
        }
        SetRect(GetChildById("rebirthCraftingKnowledgeTitle"), metaX, -contentTop, metaWidth - 82, 20);
        SetRect(GetChildById("rebirthCraftingSelectedRecipeKnowledge"), metaX, -(contentTop + 24), metaWidth, 180);
        // The recipe's chevron replaces the separate View Recipe action.
        SetRect(GetChildById("rebirthCraftingKnowledgeAction"), width - 39, -(contentTop + 24), 25, 24);
        SetRect(knowledgeButton, 0, 0, 24, 25);
        SetRect(GetChildById("rebirthKnowledgeFill"), 0, 0, 24, 25);
        SetRect(GetChildById("rebirthKnowledgeActionIcon"), 6, -5, 12, 15);
        SetRect(GetChildById("recipeRequiredToolIcon"), metaX, -244, 24, 24);
        SetRect(GetChildById("recipeRequiredToolText"), metaX+30, -244, metaWidth-30, 24);
        SetRect(GetChildById("recipeRequiredFuelIcon"), metaX, -216, 24, 24);
        SetRect(GetChildById("recipeRequiredFuelText"), metaX+30, -216, metaWidth-30, 24);
        int footerY = -(height - 94);
        int footerRight = metaX + metaWidth / 2;
        SetRect(GetChildById("rebirthCraftingSelectedRecipeTimeTitle"), metaX, footerY, 140, 24);
        SetRect(GetChildById("rebirthCraftingSelectedRecipeTimeValue"), metaX, footerY - 22, 140, 22);
        SetRect(GetChildById("rebirthCraftingBatchTitle"), footerRight - 8, footerY, 105, 24);
        SetRect(GetChildById("rebirthCraftingRecipeCraftCount"), footerRight, footerY - 18, 150, 28);
        GetChildById("countMax").ViewComponent.IsVisible=true;
        int actionY = Math.Max(contentTop + 192, height - 34);
        XUiController actionStrip = GetChildById("rebirthCraftingActionsStrip");
        SetRect(actionStrip, 14, -actionY, Math.Max(260, width - 28), 34);
        actions?.ApplyGeometry(true);
        SetRect(GetChildById("rebirthCraftingSelectedRecipeEmpty"), 16, -74, Math.Max(200, width - 32), 80);
    }

    private int ResolveCraftingTier(Recipe recipe)
    {
        if (recipe == null || owner?.xui?.playerUI?.entityPlayer == null) return 1;
        try { return Math.Max(1, Math.Min(XUiM_Recipes.CraftingMaxTier, recipe.GetCraftingTier(owner.xui.playerUI.entityPlayer))); }
        catch { return Math.Max(1, recipe.craftingTier > 0 ? recipe.craftingTier : 1); }
    }

    private void ChangeQuality(int delta)
    {
        if (selectedRecipe == null || !selectedRecipe.GetOutputItemClass().ShowQualityBar) return;
        int nextTier = Math.Max(1, Math.Min(ResolveCraftingTier(selectedRecipe), selectedCraftingTier + delta));
        if (nextTier == selectedCraftingTier) return;
        selectedCraftingTier = nextTier;
        owner?.AdvanceCraftIntentEpoch();
        Log.Out("[REBIRTH Crafting Quality] recipe=" + selectedRecipe.GetName() + " selected=" + selectedCraftingTier);
        RefreshNow();
    }

    private static string ResolveDescription(Recipe recipe)
    {
        return RebirthRecipeDescriptionText.GetForRecipe(recipe);
    }

    private static string GetKnowledgeFocusId(Recipe recipe)
    {
        if (recipe == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return string.Empty;
        string name;
        try { name = recipe.GetName() ?? string.Empty; } catch { return string.Empty; }
        if (name.Length == 0) return string.Empty;
        RebirthCapabilityDefinition definition;
        if (!RebirthCapabilityRegistry.TryGetRecipe(name, out definition) || definition == null) return string.Empty;
        return FindFirstKnowledge(definition.Requirement);
    }

    private static string FindFirstKnowledge(RebirthCapabilityRequirement requirement)
    {
        if (requirement == null) return string.Empty;
        if (string.Equals(requirement.Kind, RebirthCapabilityKinds.Knowledge, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(requirement.Id)) return requirement.Id;
        if (requirement.Children == null) return string.Empty;
        for (int i = 0; i < requirement.Children.Count; i++)
        {
            string id = FindFirstKnowledge(requirement.Children[i]);
            if (!string.IsNullOrEmpty(id)) return id;
        }
        return string.Empty;
    }

    private T GetView<T>(string id) where T : XUiView
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as T : null;
    }

    private static void SetRect(XUiController controller, int x, int y, int width, int height)
    {
        if (controller?.ViewComponent == null) return;
        controller.ViewComponent.Position = new Vector2i(x, y);
        controller.ViewComponent.Size = new Vector2i(Math.Max(1, width), Math.Max(1, height));
    }

    private static string FormatTime(float seconds)
    {
        int total = Math.Max(0, (int)Math.Ceiling(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    private static string SafeLocalized(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        string value = Localization.Get(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private static string Localize(string key, string fallback)
    {
        string value = Localization.Get(key ?? string.Empty);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static Color32 ParseColor(string value, Color32 fallback)
    {
        try { return StringParsers.ParseColor32(value); }
        catch { return fallback; }
    }
}

