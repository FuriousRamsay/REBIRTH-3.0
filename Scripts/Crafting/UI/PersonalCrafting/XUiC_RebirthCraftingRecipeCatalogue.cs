using Platform;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Rebirth-owned Personal Crafting recipe catalogue.
///
/// This is intentionally a behavioral subclass of XUiC_RecipeList so native cross-links such as
/// "Recipes" on an item, Challenge craft navigation and other GetChildByType<XUiC_RecipeList>()
/// callers continue to find a compatible target. It does not call the native RecipeList Init/
/// Update/OnOpen geometry path and has no pager. Readable rows and a clipped buffer
/// row permits smooth fractional scrolling. The catalogue owns its scrollbar directly so native
/// UIScrollView refreshes can never snap the recipe offset back to zero.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingRecipeCatalogue : XUiC_RecipeList
{
    private static XUiC_RebirthCraftingRecipeCatalogue activeInstance;
    public const int VisibleRowCount = 7;
    public const int PresentationRowCount = VisibleRowCount + 1;
    public int VisibleRows { get; private set; } = VisibleRowCount;
    private const int CategorySlotCount = 12;
    private const int RowGap = 2;
    private const int RowsStartY = -122;
    private const int ScrollbarWidth = 16;
    private const int MinScrollbarThumb = 30;
    private const float WheelStepRows = 0.62f;

    private int currentRowHeight = 36;
    private int currentRowStride = 38;
    private int currentScrollViewportHeight = 398;

    private static readonly PropertyInfo NativeCurrentRecipeProperty =
        typeof(XUiC_RecipeList).GetProperty("CurrentRecipe", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private readonly XUiController[] categoryButtons = new XUiController[CategorySlotCount];
    private readonly XUiV_Sprite[] categoryBackgrounds = new XUiV_Sprite[CategorySlotCount];
    private readonly XUiV_Sprite[] categoryFrames = new XUiV_Sprite[CategorySlotCount];
    private readonly XUiV_Sprite[] categoryIcons = new XUiV_Sprite[CategorySlotCount];

    private XUiC_RebirthCraftingRecipeEntry[] rows = Array.Empty<XUiC_RebirthCraftingRecipeEntry>();
    private XUiC_RebirthPersonalCrafting owner;
    private RebirthCraftingRecipeCatalogueService service;
    private XUiC_TextInput searchInput;
    private ItemStack pendingItemRecipes, ingredientFilter;
    private string defaultSearchPlaceholder = "Search recipes...";
    private string ingredientFilterName = string.Empty;
    private XUiController favoriteFilter;
    private XUiV_Sprite favoriteFilterBackground;
    private XUiV_Sprite favoriteFilterFrame;
    private XUiV_Sprite favoriteFilterStar;
    private XUiV_Label searchPlaceholder;
    private XUiV_Label emptyState;
    private XUiController scrollViewport;
    private XUiController scrollTrack;
    private XUiController scrollThumb;

    private int firstVisibleIndex;
    private int lastLayoutWidth = -1;
    private int lastRecipeCount = -1;
    private float scrollOffsetPixels;
    private float targetScrollOffsetPixels;
    // User-owned scroll target. Unlike the render offsets, this value is never clamped away
    // merely because V3.2 temporarily rebuilds the recipe dataset with a zero range.
    private float authoritativeScrollTargetPixels;
    private bool draggingScrollbar;
    private bool pagingMode;
    private float dragAccumulated;
    private float dragStartOffset;
    private bool resetScrollOnNextRebuild;
    private string pendingRebuildReason = "initial";
    private bool filterDirty = true;
    private bool availabilityDirty;
    private bool rowsDirty = true;
    private bool subscribed;
    private Action detachSubscriptions;
    private bool categoryOverflowLogged;
    private bool forceSelectionVisible = true;
    private Recipe lastPublishedRecipe;
    private float nextScrollTrace;
    private float lastTraceCurrent = float.MinValue;
    private float lastTraceTarget = float.MinValue;
    private int lastTraceFirst = -1;
    private int lastTraceCount = -1;
    private readonly HashSet<XUiController> scrollWiredControllers = new HashSet<XUiController>();
    private int lastEventScrollFrame = -1;
    private int lastHandledWheelFrame = -1;

    public event Action<Recipe> RebirthSelectionChanged;

    public string GetCategoryDisplayName(Recipe recipe)
    {
        return service != null ? service.ResolveCategoryDisplayName(recipe) : string.Empty;
    }

    /// <summary>
    /// Returns a real XUiC_RecipeEntry contract for native ItemActionEntry* commands.
    /// If the selected recipe was scrolled out of the eight-row viewport, it is brought back
    /// into view first; this never changes recipe capacity or inventory capacity.
    /// </summary>
    public XUiC_RebirthCraftingRecipeEntry GetBehaviorEntryForRecipe(Recipe recipe, bool ensureVisible)
    {
        if (recipe == null) return null;
        XUiC_RebirthCraftingRecipeEntry match = FindVisibleEntry(recipe);
        if (match != null || !ensureVisible) return match;

        int index = FindRecipeIndex(recipe);
        if (index < 0) return null;
        EnsureRecipeIndexVisible(index, true);
        ApplyRows();
        return FindVisibleEntry(recipe);
    }

    public void NotifyExternalFavoriteChanged(Recipe recipe)
    {
        if (showFavorites)
        {
            filterDirty = true;
            forceSelectionVisible = false;
            resetScrollOnNextRebuild = false;
            pendingRebuildReason = "favorites-external";
        }
        else rowsDirty = true;
    }

    public void NotifyExternalRecipeStateChanged()
    {
        rowsDirty = true;
    }

    public override void Init()
    {
        // V3.2 b10 exposes AlwaysUpdate as an inherited field, not a virtual method/property.
        AlwaysUpdate = true;

        // Reproduce XUiController.Init() without XUiC_RecipeList.Init(), which requires the native
        // CategoryList/Paging/InfoWindow hierarchy that Chunk B intentionally removed.
        if (viewComponent != null)
            viewComponent.InitView();
        for (int i = 0; i < children.Count; i++)
            children[i].Init();
        curInputStyle = PlatformManager.NativePlatform.Input.CurrentInputStyle;

        owner = GetParentByType<XUiC_RebirthPersonalCrafting>();
        service = new RebirthCraftingRecipeCatalogueService(xui, owner);

        workStation = string.Empty;
        craftingArea = new[] { string.Empty };
        category = "Basics";
        showFavorites = false;
        pager = null;
        page = 0;
        length = VisibleRows;
        CraftCount = null;
        InfoWindow = null;
        craftingWindow = owner;

        ResolveRows();
        ResolveFilters();
        ResolveScrollbar();
        WireCategoryButtons();
        WireFilterControls();
        WireScrollSurfaces();
        ApplyGeometry(true);

        IsDirty = false;
        resortRecipes = false;
        pageChanged = false;
    }

    public void RequestRecipesUsingItem(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty()) return;
        pendingItemRecipes = stack.Clone();
    }

    public void ApplyPendingItemRecipes()
    {
        if (pendingItemRecipes == null || service == null || owner?.State.IsOpen != true) return;
        ClaimScrollAuthority("item-recipes");
        ingredientFilter = pendingItemRecipes;
        pendingItemRecipes = null;
        ingredientFilterName = ingredientFilter.itemValue.ItemClassOrMissing.GetLocalizedItemName();
        // Search.Text raises the existing callback. Assign the ingredient filter first so
        // a deferred programmatic text callback cannot replace it with the default list.
        if (searchInput != null) searchInput.Text = string.Empty;
        category = string.Empty;
        showFavorites = false;
        SetNativeCurrentRecipe(null);
        resetScrollOnNextRebuild = true;
        service.RebuildFiltered(this, string.Empty, category, false, ingredientFilter);
        FinishRebuild(true, "item-recipes", true);
        ApplyRows();
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH Crafting] item-recipes ingredient=" + ingredientFilter.itemValue.type +
                " name=" + ingredientFilterName + " matches=" + recipeInfos.Count);
    }

    public override void OnOpen()
    {
        // Startup may open more than one XUi copy. Input under the mouse can reclaim authority
        // later; OnOpen only establishes the initial candidate.
        activeInstance = this;
        ingredientFilter = null;
        ingredientFilterName = string.Empty;
        // Do not clear pendingItemRecipes: an explicit cross-link queued it before Open.
        OpenControllerTree();
        Subscribe();

        service.RefreshCategories();
        EnsureCategoryIsValid();
        RefreshCategoryButtons();
        RefreshFilterVisuals();
        owner?.Coordinator?.RecordCategory(category);
        owner?.Coordinator?.RecordFavorites(showFavorites);
        owner?.Coordinator?.RecordSearch(searchInput != null ? searchInput.Text : string.Empty);

        filterDirty = true;
        availabilityDirty = false;
        rowsDirty = true;
        forceSelectionVisible = true;
        resetScrollOnNextRebuild = true;
        pendingRebuildReason = "open";
        firstVisibleIndex = 0;
        scrollOffsetPixels = 0f;
        targetScrollOffsetPixels = 0f;
        authoritativeScrollTargetPixels = 0f;
        lastEventScrollFrame = -1;
        lastHandledWheelFrame = -1;
        lastRecipeCount = -1;
        nextScrollTrace = 0f;
        ApplyGeometry(true);
        TraceScrollSnapshot("open", true);
    }

    public override void OnClose()
    {
        if (activeInstance == this)
            activeInstance = null;
        Unsubscribe();
        ingredientFilter = null;
        ingredientFilterName = string.Empty;
        pendingItemRecipes = null;
        CloseControllerTree();
    }

    public override void Update(float dt)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            UpdateControllerTree(dt);
            return;
        }

        ApplyPendingItemRecipes();
        if (activeInstance != null && activeInstance != this)
        {
            // Several XUi roots are instantiated by V3.2. Only the catalogue whose Crafting
            // group actually opened is allowed to own recipe scrolling. Inactive copies may
            // still receive late XUi updates, but they cannot reset the live list.
            UpdateControllerTree(dt);
            return;
        }

        ApplyGeometry(false);
        PollMouseWheelFallback();

        // Native cross-links call non-virtual SetRecipeDataByItem/ByItems/ByIngredient on the
        // XUiC_RecipeList contract. Those methods replace inherited 'recipes' and set
        // resortRecipes/pageChanged. When that happens without one of our own filter changes,
        // preserve their source list and only rebuild RecipeInfo projection.
        bool externalDatasetChanged = resortRecipes && pageChanged;
        if (externalDatasetChanged)
        {
            // A different native cross-link owns its replacement dataset. Ordinary item
            // availability refreshes use availabilityDirty and preserve ingredientFilter.
            ingredientFilter = null;
            ingredientFilterName = string.Empty;
            showFavorites = false;
            category = string.Empty;
            owner?.Coordinator?.RecordCategory(category);
            owner?.Coordinator?.RecordFavorites(false);
            RefreshCategoryButtons();
            RefreshFilterVisuals();
            service.BuildRecipeInfosFromCurrentSource(this);
            // Native RecipeList flags can be raised by background/cross-link refreshes while the
            // player is simply browsing. Do not force the currently selected recipe (often row 0)
            // back into view here; that was the remaining snap-to-top path reported in PC091.
            // Explicit cross-links that require visibility already call GetBehaviorEntryForRecipe().
            FinishRebuild(false, "external-dataset");
        }
        else if (filterDirty)
        {
            string search = searchInput != null ? searchInput.Text : string.Empty;
            service.RebuildFiltered(this, search, category, showFavorites, ingredientFilter);
            FinishRebuild(forceSelectionVisible, pendingRebuildReason);
        }
        else if (availabilityDirty)
        {
            service.BuildRecipeInfosFromCurrentSource(this);
            FinishRebuild(false, "availability");
        }

        UpdateSmoothScroll(dt);

        if (rowsDirty)
            ApplyRows();

        TraceScrollSnapshot("update", false);
        UpdateControllerTree(dt);
    }


    public void SelectEntry(XUiC_RebirthCraftingRecipeEntry entry, bool setMaxCraftCount)
    {
        if (entry == null || entry.Recipe == null)
            return;

        SelectRecipe(entry.Recipe, entry);

        // Craft-count ownership is introduced in Chunk E. Preserve the native Shift-select
        // behavior automatically once that authority is attached.
        if (setMaxCraftCount && CraftCount != null)
            CraftCount.SetToMaxCount();
    }

    public void ToggleFavorite(Recipe recipe)
    {
        if (recipe == null)
            return;

        Recipe selected = CurrentRecipe;
        CraftingManager.ToggleFavoriteRecipe(recipe);

        if (showFavorites)
        {
            filterDirty = true;
            forceSelectionVisible = false;
            resetScrollOnNextRebuild = false;
            pendingRebuildReason = "favorites-toggle";
        }
        else
        {
            rowsDirty = true;
        }

        if (selected != null)
            SetNativeCurrentRecipe(selected);
    }

    public void HandleWheel(float delta)
    {
        HandleWheel(delta, "direct");
    }

    public void HandleWheel(float delta, string source)
    {
        if (activeInstance != null && activeInstance != this)
        {
            activeInstance.HandleWheel(delta, (source ?? "unknown") + "->active");
            return;
        }

        // Recursive XUi wheel forwarding can deliver the same physical wheel sample through
        // more than one child controller in a single frame. Apply it once so one notch never
        // becomes several scroll steps.
        if (lastHandledWheelFrame == Time.frameCount)
            return;
        lastHandledWheelFrame = Time.frameCount;

        if (MaxPixelOffset <= 0.5f || Math.Abs(delta) < 0.0001f)
        {
            if (RebirthLogSettings.CraftingUiLoggingEnabled)
                Log.Out("[REBIRTH Crafting RecipeScroll] wheelIgnored source=" + (source ?? "unknown")
                    + " delta=" + delta.ToString("0.###")
                    + " count=" + (recipeInfos != null ? recipeInfos.Count : 0)
                    + " max=" + MaxPixelOffset.ToString("0.0"));
            return;
        }

        // One wheel notch moves only part of a row. The current offset then eases toward the
        // target every frame, giving continuous motion instead of page/row stepping.
        float before = targetScrollOffsetPixels;
        float direction = delta > 0f ? -1f : 1f;
        float magnitude = Math.Max(0.35f, Math.Min(1.5f, Math.Abs(delta)));
        float step = Math.Max(12f, currentRowStride * WheelStepRows * magnitude);
        SetScrollTarget(RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(targetScrollOffsetPixels, MaxPixelOffset, VisibleRows * currentRowStride, direction > 0 ? 1 : -1) : authoritativeScrollTargetPixels + direction * step, false, true);
        TraceScrollChange("wheel:" + (source ?? "unknown") + "(" + delta.ToString("0.###") + ")", before, targetScrollOffsetPixels);
        TraceScrollSnapshot("wheel", true);
    }

    private void ResolveRows()
    {
        rows = GetChildrenByType<XUiC_RebirthCraftingRecipeEntry>();
        Array.Sort(rows, (a, b) => a.RowIndex.CompareTo(b.RowIndex));

        recipeControls = new XUiC_RecipeEntry[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            XUiC_RebirthCraftingRecipeEntry row = rows[i];
            row.Catalogue = this;
            row.RecipeList = this;
            recipeControls[i] = row;
            WireScrollRecursive(row);
        }
    }

    private void ResolveFilters()
    {
        // V3.2 b10 ownership: the text field must not be a child of this RecipeList-derived
        // controller. The owning crafting window attaches the initialized sibling after the
        // full controller tree has completed Init().
        searchInput = null;
        txtInput = null;
        searchPlaceholder = null;
        emptyState = GetView<XUiV_Label>("rebirthCraftingRecipeEmptyState");

        favoriteFilter = GetChildById("btnRebirthCraftingFavoriteFilter");
        favoriteFilterBackground = GetView<XUiV_Sprite>("rebirthCraftingFavoriteFilterBg");
        favoriteFilterFrame = GetView<XUiV_Sprite>("rebirthCraftingFavoriteFilterFrame");
        favoriteFilterStar = GetView<XUiV_Sprite>("rebirthCraftingFavoriteFilterStar");
    }

    private void ResolveScrollbar()
    {
        scrollViewport = GetChildById("rebirthCraftingRecipeViewport");
        scrollTrack = GetChildById("rebirthCraftingRecipeScrollTrack");
        scrollThumb = GetChildById("rebirthCraftingRecipeScrollThumb");
    }

    private void WireCategoryButtons()
    {
        for (int i = 0; i < CategorySlotCount; i++)
        {
            int index = i;
            categoryButtons[i] = GetChildById("btnRebirthCraftingCategory" + i);
            categoryBackgrounds[i] = GetView<XUiV_Sprite>("rebirthCraftingCategoryBg" + i);
            categoryFrames[i] = GetView<XUiV_Sprite>("rebirthCraftingCategoryFrame" + i);
            categoryIcons[i] = GetView<XUiV_Sprite>("rebirthCraftingCategoryIcon" + i);

            XUiController button = categoryButtons[i];
            if (button == null)
                continue;

            button.OnPress += delegate(XUiController sender, int mouseButton)
            {
                SelectCategory(index);
            };
            button.OnScroll += HandleScrollEvent;
        }
    }

    private void WireFilterControls()
    {
        if (favoriteFilter != null)
        {
            favoriteFilter.OnPress += delegate(XUiController sender, int mouseButton)
            {
                ingredientFilter = null;
                ingredientFilterName = string.Empty;
                pendingItemRecipes = null;
                showFavorites = !showFavorites;
                if (searchInput != null)
                    searchInput.Text = string.Empty;
                owner?.Coordinator?.RecordSearch(string.Empty);
                owner?.Coordinator?.RecordFavorites(showFavorites);
                filterDirty = true;
                forceSelectionVisible = true;
                resetScrollOnNextRebuild = true;
                pendingRebuildReason = "favorites";
                RefreshFilterVisuals();
            };
            favoriteFilter.OnScroll += HandleScrollEvent;
            if (favoriteFilter.ViewComponent != null)
                favoriteFilter.ViewComponent.ToolTip = Localization.Get("xuiRebirthCraftingFavoritesTooltip");
        }

    }

    /// <summary>
    /// Attaches the V3.2-native sibling search field after its normal XUiController Init path
    /// has completed. This keeps XUiC_TextInput out of the catalogue's intentionally manual
    /// RecipeList initialization while preserving live search behavior.
    /// </summary>
    public void AttachSearchInput(XUiC_TextInput input, XUiV_Label placeholder)
    {
        if (ReferenceEquals(searchInput, input))
            return;

        if (searchInput != null)
        {
            searchInput.OnChangeHandler -= HandleSearchChanged;
            searchInput.OnSubmitHandler -= HandleSearchSubmitted;
        }

        searchInput = input;
        txtInput = input;
        searchPlaceholder = placeholder;
        if (placeholder != null && !string.IsNullOrEmpty(placeholder.Text))
            defaultSearchPlaceholder = placeholder.Text;

        if (searchInput != null)
        {
            searchInput.OnChangeHandler += HandleSearchChanged;
            searchInput.OnSubmitHandler += HandleSearchSubmitted;
        }

        filterDirty = true;
        forceSelectionVisible = false;
        resetScrollOnNextRebuild = false;
        pendingRebuildReason = "search-attach";
        RefreshFilterVisuals();
    }

    private void WireScrollSurfaces()
    {
        WireScrollOnce(this);
        WireScrollOnce(scrollViewport);
        WireScrollOnce(scrollTrack);
        WireScrollOnce(scrollThumb);
        for (int i = 0; i < rows.Length; i++)
            WireScrollRecursive(rows[i]);

        if (scrollTrack != null)
            scrollTrack.OnPress += ScrollTrack_OnPress;
        if (scrollThumb != null)
        {
            if (scrollThumb.ViewComponent != null)
                scrollThumb.ViewComponent.EventOnDrag = true;
            scrollThumb.OnDrag += ScrollThumb_OnDrag;
        }
    }

    private void WireScrollRecursive(XUiController controller)
    {
        if (controller == null)
            return;
        WireScrollOnce(controller);
        if (controller.Children == null)
            return;
        for (int i = 0; i < controller.Children.Count; i++)
            WireScrollRecursive(controller.Children[i]);
    }

    private void WireScrollOnce(XUiController controller)
    {
        if (controller == null || scrollWiredControllers.Contains(controller))
            return;
        scrollWiredControllers.Add(controller);
        if (controller.ViewComponent != null)
            controller.ViewComponent.EventOnScroll = true;
        controller.OnScroll += HandleScrollEvent;
    }

    private void PollMouseWheelFallback()
    {
        if (lastEventScrollFrame == Time.frameCount ||
            ViewComponent == null || !ViewComponent.IsVisible)
            return;

        float delta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(delta) < 0.0001f || !IsMouseOverRecipeArea())
            return;

        // More than one V3.2 XUi tree can exist. The catalogue physically under the cursor wins
        // scroll authority for this interaction rather than forwarding to whichever copy opened
        // most recently during startup.
        ClaimScrollAuthority("poll");
        lastEventScrollFrame = Time.frameCount;
        HandleWheel(delta, "poll");
    }

    private bool IsMouseOverRecipeArea()
    {
        GameObject hovered = UICamera.hoveredObject;
        Transform candidate = hovered != null ? hovered.transform : null;
        if (IsDescendant(candidate, this) || IsDescendant(candidate, scrollViewport) ||
            IsDescendant(candidate, scrollTrack) || IsDescendant(candidate, scrollThumb))
            return true;

        for (int i = 0; i < rows.Length; i++)
        {
            XUiC_RebirthCraftingRecipeEntry row = rows[i];
            if (row == null || row.ViewComponent == null || !row.ViewComponent.IsVisible)
                continue;
            if (IsDescendant(candidate, row) || IsMouseInsideColliderTree(row.ViewComponent.UiTransform))
                return true;
        }
        return false;
    }

    private static bool IsDescendant(Transform candidate, XUiController controller)
    {
        if (candidate == null || controller == null || controller.ViewComponent == null || controller.ViewComponent.UiTransform == null)
            return false;
        Transform root = controller.ViewComponent.UiTransform;
        while (candidate != null)
        {
            if (candidate == root)
                return true;
            candidate = candidate.parent;
        }
        return false;
    }

    private static bool IsMouseInsideColliderTree(Transform root)
    {
        if (root == null || UICamera.currentCamera == null)
            return false;
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        Vector2 mouse = (Vector2)Input.mousePosition;
        Camera camera = UICamera.currentCamera;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;
            Bounds bounds = collider.bounds;
            Vector3 a = camera.WorldToScreenPoint(bounds.min);
            Vector3 b = camera.WorldToScreenPoint(bounds.max);
            if (mouse.x >= Mathf.Min(a.x, b.x) && mouse.x <= Mathf.Max(a.x, b.x) &&
                mouse.y >= Mathf.Min(a.y, b.y) && mouse.y <= Mathf.Max(a.y, b.y))
                return true;
        }
        return false;
    }

    private void SelectCategory(int index)
    {
        if (service == null || index < 0 || index >= service.Categories.Count || index >= CategorySlotCount)
            return;

        ingredientFilter = null;
        ingredientFilterName = string.Empty;
        pendingItemRecipes = null;
        RebirthCraftingRecipeCatalogueService.Category selected = service.Categories[index];
        category = selected.Name ?? string.Empty;
        showFavorites = false;
        if (searchInput != null)
            searchInput.Text = string.Empty;
        owner?.Coordinator?.RecordSearch(string.Empty);
        owner?.Coordinator?.RecordCategory(category);

        filterDirty = true;
        forceSelectionVisible = true;
        resetScrollOnNextRebuild = true;
        pendingRebuildReason = "category";
        RefreshCategoryButtons();
        RefreshFilterVisuals();
    }

    private void HandleSearchChanged(XUiController sender, string text, bool changeFromCode)
    {
        owner?.Coordinator?.RecordSearch(text);
        filterDirty = true;
        // Native/text-field housekeeping can raise OnChangeHandler with changeFromCode=true.
        // That is not a player request to jump the recipe list back to row zero.
        bool userEdit = !changeFromCode;
        if (userEdit)
        {
            ingredientFilter = null;
            ingredientFilterName = string.Empty;
            pendingItemRecipes = null;
        }
        forceSelectionVisible = userEdit;
        resetScrollOnNextRebuild = userEdit;
        pendingRebuildReason = userEdit ? "search-change-user" : "search-change-code";
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH Crafting RecipeScroll] searchChanged fromCode=" + changeFromCode
                + " text='" + (text ?? string.Empty) + "' reset=" + resetScrollOnNextRebuild);
        RefreshFilterVisuals();
    }

    private void HandleSearchSubmitted(XUiController sender, string text)
    {
        ingredientFilter = null;
        ingredientFilterName = string.Empty;
        pendingItemRecipes = null;
        owner?.Coordinator?.RecordSearch(text);
        filterDirty = true;
        forceSelectionVisible = true;
        resetScrollOnNextRebuild = true;
        pendingRebuildReason = "search-submit";
    }

    private void HandleScrollEvent(XUiController sender, float delta)
    {
        ClaimScrollAuthority("event:" + ControllerId(sender));
        lastEventScrollFrame = Time.frameCount;
        HandleWheel(delta, ControllerId(sender));
    }

    private void ClaimScrollAuthority(string reason)
    {
        if (activeInstance == this)
            return;
        XUiC_RebirthCraftingRecipeCatalogue previous = activeInstance;
        activeInstance = this;
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH Crafting RecipeScroll] authorityClaim reason=" + (reason ?? "unknown")
                + " instance=" + GetHashCode()
                + " previous=" + (previous != null ? previous.GetHashCode().ToString() : "<none>"));
    }

    private void FinishRebuild(bool ensureSelectionVisible, string reason, bool explicitSelection = false)
    {
        float scrollBefore = scrollOffsetPixels;
        float targetBefore = targetScrollOffsetPixels;
        bool requestedReset = resetScrollOnNextRebuild;
        int countAfter = recipeInfos != null ? recipeInfos.Count : 0;
        filterDirty = false;
        availabilityDirty = false;
        resortRecipes = false;
        pageChanged = false;
        IsDirty = false;
        forceSelectionVisible = false;

        Recipe preferred = CurrentRecipe;
        int selectedIndex = FindRecipeIndex(preferred);
        if (selectedIndex < 0 && recipeInfos.Count > 0)
        {
            selectedIndex = 0;
            preferred = recipeInfos[0].recipe;
            SetNativeCurrentRecipe(preferred);
        }
        else if (recipeInfos.Count == 0)
        {
            preferred = null;
            SetNativeCurrentRecipe(null);
        }

        if (requestedReset)
        {
            SetScrollTarget(0f, true, true);
        }
        else
        {
            // Preserve the player's target across native/background list rebuilds. If native code
            // temporarily cleared recipeInfos, scrollOffsetPixels may already have been clamped,
            // but the private target still represents the player's last requested position.
            targetScrollOffsetPixels = Mathf.Clamp(authoritativeScrollTargetPixels, 0f, MaxPixelOffset);
            scrollOffsetPixels = Mathf.Clamp(scrollOffsetPixels, 0f, MaxPixelOffset);
            if (ensureSelectionVisible && selectedIndex >= 0)
                EnsureRecipeIndexVisible(selectedIndex, true);
        }
        resetScrollOnNextRebuild = false;
        pendingRebuildReason = "refresh";

        UpdateScrollbarGeometry();
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH Crafting RecipeScroll] rebuild reason=" + (reason ?? "refresh")
                + " reset=" + requestedReset
                + " ensureSelection=" + ensureSelectionVisible
                + " count=" + countAfter
                + " selectedIndex=" + selectedIndex
                + " before=" + scrollBefore.ToString("0.0")
                + " targetBefore=" + targetBefore.ToString("0.0")
                + " after=" + scrollOffsetPixels.ToString("0.0")
                + " targetAfter=" + targetScrollOffsetPixels.ToString("0.0")
                + " authority=" + authoritativeScrollTargetPixels.ToString("0.0")
                + " max=" + MaxPixelOffset.ToString("0.0")
                + " instance=" + GetHashCode()
                + " active=" + (activeInstance == this));
        TraceScrollChange(reason ?? "refresh", scrollBefore, scrollOffsetPixels);
        rowsDirty = true;
        RefreshFilterVisuals();

        owner?.Coordinator?.SelectRecipe(preferred, explicitSelection);
        owner?.Coordinator?.RecordCategory(category);
        owner?.Coordinator?.RecordFavorites(showFavorites);
        owner?.Coordinator?.RecordSearch(searchInput != null ? searchInput.Text : string.Empty);

        PublishSelectionIfChanged(preferred);
    }

    private void ApplyRows()
    {
        rowsDirty = false;
        UpdateFirstVisibleIndex();
        Recipe selectedRecipe = CurrentRecipe;
        selectedEntry = null;

        float remainder = currentRowStride > 0
            ? scrollOffsetPixels - firstVisibleIndex * currentRowStride
            : 0f;
        int yShift = Mathf.RoundToInt(remainder);

        for (int i = 0; i < rows.Length; i++)
        {
            int sourceIndex = firstVisibleIndex + i;
            XUiC_RebirthCraftingRecipeEntry row = rows[i];
            if (row.ViewComponent != null)
                row.ViewComponent.Position = new Vector2i(0, -i * currentRowStride + yShift);

            if (i <= VisibleRows && sourceIndex >= 0 && sourceIndex < recipeInfos.Count)
            {
                XUiC_RecipeList.RecipeInfo info = recipeInfos[sourceIndex];
                row.SetData(info, service.ResolveCategoryDisplayName(info.recipe));
                bool isSelected = selectedRecipe != null && info.recipe == selectedRecipe;
                row.SetRebirthSelected(isSelected);
                if (isSelected)
                    selectedEntry = row;
            }
            else
            {
                row.ClearData();
            }
        }

        if (emptyState != null)
            emptyState.IsVisible = recipeInfos.Count == 0;
        UpdateScrollbarGeometry();
    }

    private void SelectRecipe(Recipe recipe, XUiC_RebirthCraftingRecipeEntry sourceRow)
    {
        if (recipe == null)
            return;

        SetNativeCurrentRecipe(recipe);
        selectedEntry = sourceRow;
        for (int i = 0; i < rows.Length; i++)
            rows[i].SetRebirthSelected(rows[i] == sourceRow);

        owner?.Coordinator?.SelectRecipe(recipe, true);

        PublishSelectionIfChanged(recipe);
        rowsDirty = true;
    }

    private int FindRecipeIndex(Recipe recipe)
    {
        if (recipe == null)
            return -1;
        for (int i = 0; i < recipeInfos.Count; i++)
            if (recipeInfos[i].recipe == recipe)
                return i;
        return -1;
    }

    private void EnsureCategoryIsValid()
    {
        if (service == null || service.Categories.Count == 0)
        {
            category = string.Empty;
            return;
        }

        for (int i = 0; i < service.Categories.Count; i++)
        {
            if (string.Equals(service.Categories[i].Name, category, StringComparison.OrdinalIgnoreCase))
                return;
        }

        category = service.Categories[0].Name ?? string.Empty;
    }

    private void RefreshCategoryButtons()
    {
        if (service == null)
            return;

        if (!categoryOverflowLogged && service.Categories.Count > CategorySlotCount)
        {
            categoryOverflowLogged = true;
            Log.Warning("[REBIRTH Crafting] Personal recipe categories exceed authored strip capacity: "
                + service.Categories.Count + " > " + CategorySlotCount + ".");
        }

        int visibleCount = Math.Min(CategorySlotCount, service.Categories.Count);
        int regionWidth = ViewComponent != null ? Math.Max(1, ViewComponent.Size.x) : 380;
        int stripWidth = Math.Max(1, regionWidth - 20);
        int slotWidth = visibleCount > 0 ? Math.Max(26, stripWidth / visibleCount) : stripWidth;

        for (int i = 0; i < CategorySlotCount; i++)
        {
            bool visible = i < visibleCount;
            XUiController button = categoryButtons[i];
            XUiV_Sprite icon = categoryIcons[i];
            XUiV_Sprite bg = categoryBackgrounds[i];
            XUiV_Sprite frame = categoryFrames[i];

            if (button != null && button.ViewComponent != null)
            {
                button.ViewComponent.IsVisible = visible;
                button.ViewComponent.Enabled = visible;
                button.ViewComponent.IsNavigatable = visible;
                button.ViewComponent.IsSnappable = visible;
                button.ViewComponent.Position = new Vector2i(10 + i * slotWidth, -44);
                button.ViewComponent.Size = new Vector2i(Math.Max(24, slotWidth - 2), 34);
            }

            if (!visible)
            {
                if (icon != null) icon.IsVisible = false;
                if (bg != null) bg.IsVisible = false;
                if (frame != null) frame.IsVisible = false;
                continue;
            }

            RebirthCraftingRecipeCatalogueService.Category data = service.Categories[i];
            bool selected = !showFavorites && string.IsNullOrEmpty(searchInput != null ? searchInput.Text : string.Empty)
                && string.Equals(category, data.Name, StringComparison.OrdinalIgnoreCase);

            if (button != null && button.ViewComponent != null)
                button.ViewComponent.ToolTip = data.DisplayName ?? data.Name;

            if (bg != null)
            {
                bg.IsVisible = true;
                bg.Position = new Vector2i(10 + i * slotWidth, -44);
                bg.Size = new Vector2i(Math.Max(24, slotWidth - 2), 34);
                bg.Color = selected
                    ? new Color32(38, 32, 18, 255)
                    : new Color32(20, 20, 24, 255);
            }
            if (frame != null)
            {
                frame.IsVisible = true;
                frame.Position = new Vector2i(10 + i * slotWidth, -44);
                frame.Size = new Vector2i(Math.Max(24, slotWidth - 2), 34);
                frame.Color = selected
                    ? new Color32(204, 167, 56, 255)
                    : new Color32(64, 64, 72, 255);
            }
            if (icon != null)
            {
                icon.IsVisible = !string.IsNullOrEmpty(data.Icon);
                icon.SpriteName = data.Icon ?? string.Empty;
                icon.Position = new Vector2i(10 + i * slotWidth + Math.Max(0, (slotWidth - 26) / 2), -49);
                icon.Size = new Vector2i(24, 24);
                icon.Color = selected
                    ? new Color32(204, 167, 56, 255)
                    : new Color32(235, 235, 240, 255);
            }
        }
    }

    private void RefreshFilterVisuals()
    {
        string search = searchInput != null ? searchInput.Text : string.Empty;
        bool searching = !string.IsNullOrEmpty(search);

        if (searchPlaceholder != null)
        {
            string placeholder = ingredientFilter != null ? "Recipes using " + ingredientFilterName : defaultSearchPlaceholder;
            if (searchPlaceholder.Text != placeholder) searchPlaceholder.SetTextImmediately(placeholder);
            searchPlaceholder.IsVisible = !searching;
        }

        if (favoriteFilterBackground != null)
            favoriteFilterBackground.Color = showFavorites
                ? new Color32(38, 32, 18, 255)
                : new Color32(20, 20, 24, 255);
        if (favoriteFilterFrame != null)
            favoriteFilterFrame.Color = showFavorites
                ? new Color32(204, 167, 56, 255)
                : new Color32(64, 64, 72, 255);
        if (favoriteFilterStar != null)
            favoriteFilterStar.Color = showFavorites
                ? new Color32(204, 167, 56, 255)
                : new Color32(225, 225, 230, 255);

        RefreshCategoryButtons();
    }

    private void ApplyGeometry(bool force)
    {
        if (ViewComponent == null)
            return;

        int width = Math.Max(260, ViewComponent.Size.x);
        int height = Math.Max(300, ViewComponent.Size.y);
        int layoutKey = width * 4096 + height;
        if (!force && layoutKey == lastLayoutWidth)
            return;
        lastLayoutWidth = layoutKey;

        int availableForRows = Math.Max(64, height - 130);
        int previousStride = currentRowStride;
        VisibleRows = Mathf.Clamp(availableForRows / 64, 1, VisibleRowCount);
        length = VisibleRows;
        currentRowStride = Mathf.Clamp(availableForRows / VisibleRows, 64, 80);
        currentRowHeight = Math.Max(22, currentRowStride - RowGap);
        currentScrollViewportHeight = currentRowStride * VisibleRows - RowGap;

        // Preserve the same logical list position if responsive layout changes row height.
        if (previousStride > 0 && previousStride != currentRowStride)
        {
            float logicalCurrent = scrollOffsetPixels / previousStride;
            float logicalTarget = targetScrollOffsetPixels / previousStride;
            scrollOffsetPixels = logicalCurrent * currentRowStride;
            targetScrollOffsetPixels = logicalTarget * currentRowStride;
            authoritativeScrollTargetPixels = authoritativeScrollTargetPixels / previousStride * currentRowStride;
        }
        ClampScrollOffsets();

        int rowWidth = Math.Max(210, width - 44);
        if (scrollViewport != null && scrollViewport.ViewComponent != null)
        {
            scrollViewport.ViewComponent.Position = new Vector2i(10, RowsStartY);
            scrollViewport.ViewComponent.Size = new Vector2i(rowWidth, currentScrollViewportHeight);
        }
        for (int i = 0; i < rows.Length; i++)
        {
            XUiC_RebirthCraftingRecipeEntry row = rows[i];
            if (row.ViewComponent != null)
            {
                row.ViewComponent.Size = new Vector2i(rowWidth, currentRowHeight);
                // Position is finalized by ApplyRows so fractional scrolling is retained.
            }
            row.ApplyResponsiveGeometry(rowWidth, currentRowHeight);
        }

        if (favoriteFilter != null && favoriteFilter.ViewComponent != null)
        {
            favoriteFilter.ViewComponent.Position = new Vector2i(10, -82);
            favoriteFilter.ViewComponent.Size = new Vector2i(32, 34);
        }
        SetRect(favoriteFilterBackground, 10, -82, 32, 34);
        SetRect(favoriteFilterFrame, 10, -82, 32, 34);
        if (favoriteFilterStar != null)
        {
            favoriteFilterStar.Position = new Vector2i(15, -87);
            favoriteFilterStar.Size = new Vector2i(22, 22);
        }

        if (searchInput != null && searchInput.ViewComponent != null)
        {
            searchInput.ViewComponent.Position = new Vector2i(50, -82);
            searchInput.ViewComponent.Size = new Vector2i(Math.Max(150, width - 62), 34);
        }
        if (searchPlaceholder != null)
        {
            searchPlaceholder.Position = new Vector2i(60, -99);
            searchPlaceholder.Size = new Vector2i(Math.Max(120, width - 94), 24);
        }

        if (scrollTrack != null && scrollTrack.ViewComponent != null)
        {
            scrollTrack.ViewComponent.Position = new Vector2i(width - 24, RowsStartY);
            scrollTrack.ViewComponent.Size = new Vector2i(ScrollbarWidth, currentScrollViewportHeight);
        }

        if (emptyState != null)
        {
            emptyState.Position = new Vector2i(16, RowsStartY - 112);
            emptyState.Size = new Vector2i(Math.Max(180, width - 58), 70);
        }

        RefreshCategoryButtons();
        rowsDirty = true;
        UpdateScrollbarGeometry();
    }

    private float MaxPixelOffset
    {
        get
        {
            int extraRows = Math.Max(0, (recipeInfos != null ? recipeInfos.Count : 0) - VisibleRows);
            return Math.Max(0f, extraRows * currentRowStride);
        }
    }

    private void UpdateSmoothScroll(float dt)
    {
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (pagingNow && (RebirthConsoleInputGuardRuntime.BlocksGameplayInput() || xui?.DragAndDropWindow?.IsEmpty() == false)) return;
        if (pagingMode != pagingNow) { pagingMode = pagingNow; draggingScrollbar = false; }
        ClampScrollOffsets();
        if (draggingScrollbar)
            return;
        if (Math.Abs(scrollOffsetPixels - targetScrollOffsetPixels) < 0.05f)
        {
            if (Math.Abs(scrollOffsetPixels - targetScrollOffsetPixels) > 0f)
            {
                scrollOffsetPixels = targetScrollOffsetPixels;
                rowsDirty = true;
            }
            return;
        }

        float speed = Math.Max(360f, currentRowStride * 20f);
        scrollOffsetPixels = Mathf.MoveTowards(scrollOffsetPixels, targetScrollOffsetPixels, Math.Max(0f, dt) * speed);
        UpdateFirstVisibleIndex();
        rowsDirty = true;
    }

    private void SetScrollTarget(float value, bool immediate)
    {
        SetScrollTarget(value, immediate, true);
    }

    private void SetScrollTarget(float value, bool immediate, bool updateAuthority)
    {
        if (updateAuthority)
            authoritativeScrollTargetPixels = Math.Max(0f, value);

        // Render state must respect the current range, but the authoritative user target above
        // survives a transient zero-range rebuild and is reapplied when recipes return.
        float requested = updateAuthority ? authoritativeScrollTargetPixels : value;
        float clamped = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.SnapAbsolute(requested, MaxPixelOffset, VisibleRows * currentRowStride, true) : Mathf.Clamp(requested, 0f, MaxPixelOffset);
        targetScrollOffsetPixels = clamped;
        if (immediate || RebirthScrollbarPagingPolicy.Enabled)
            scrollOffsetPixels = clamped;
        UpdateFirstVisibleIndex();
        rowsDirty = true;
        UpdateScrollbarGeometry();
    }

    private void ClampScrollOffsets()
    {
        float max = MaxPixelOffset;
        scrollOffsetPixels = Mathf.Clamp(scrollOffsetPixels, 0f, max);
        targetScrollOffsetPixels = Mathf.Clamp(targetScrollOffsetPixels, 0f, max);
        if (RebirthScrollbarPagingPolicy.Enabled) targetScrollOffsetPixels = scrollOffsetPixels = RebirthScrollbarPagingPolicy.SnapAbsolute(targetScrollOffsetPixels, max, VisibleRows * currentRowStride, true);
        UpdateFirstVisibleIndex();
    }

    private void UpdateFirstVisibleIndex()
    {
        int maxIndex = Math.Max(0, (recipeInfos != null ? recipeInfos.Count : 0) - VisibleRows);
        firstVisibleIndex = currentRowStride > 0
            ? Mathf.Clamp(Mathf.FloorToInt((scrollOffsetPixels + 0.001f) / currentRowStride), 0, maxIndex)
            : 0;
    }

    private void EnsureRecipeIndexVisible(int index, bool immediate)
    {
        if (index < 0 || currentRowStride <= 0)
            return;
        float desired = targetScrollOffsetPixels;
        float rowTop = index * currentRowStride;
        float rowBottom = rowTop + currentRowHeight;
        if (rowTop < desired)
            desired = rowTop;
        else if (rowBottom > desired + currentScrollViewportHeight)
            desired = rowBottom - currentScrollViewportHeight;
        if (RebirthScrollbarPagingPolicy.Enabled) desired = RebirthScrollbarPagingPolicy.EnsureVisible(targetScrollOffsetPixels, rowTop, rowBottom, MaxPixelOffset, currentScrollViewportHeight, VisibleRows * currentRowStride);
        SetScrollTarget(desired, immediate);
    }

    private void UpdateScrollbarGeometry()
    {
        int count = recipeInfos != null ? recipeInfos.Count : 0;
        bool needed = count > VisibleRows && MaxPixelOffset > 0.5f;
        if (scrollTrack != null && scrollTrack.ViewComponent != null)
            scrollTrack.ViewComponent.IsVisible = needed;
        if (scrollThumb != null && scrollThumb.ViewComponent != null)
            scrollThumb.ViewComponent.IsVisible = needed;
        if (!needed || scrollTrack == null || scrollTrack.ViewComponent == null ||
            scrollThumb == null || scrollThumb.ViewComponent == null)
            return;

        int trackHeight = currentScrollViewportHeight;
        int thumbHeight = Mathf.Clamp(
            Mathf.RoundToInt(trackHeight * (VisibleRows / (float)Math.Max(VisibleRows, count))),
            MinScrollbarThumb, trackHeight);
        int travel = Math.Max(0, trackHeight - thumbHeight);
        int travelY = MaxPixelOffset > 0f
            ? Mathf.RoundToInt(travel * (scrollOffsetPixels / MaxPixelOffset))
            : 0;
        int x = scrollTrack.ViewComponent.Position.x + 2;
        int y = RowsStartY - travelY;
        scrollThumb.ViewComponent.Position = new Vector2i(x, y);
        scrollThumb.ViewComponent.Size = new Vector2i(Math.Max(8, ScrollbarWidth - 4), thumbHeight);

        if (count != lastRecipeCount)
            lastRecipeCount = count;
    }

    private void ScrollThumb_OnDrag(XUiController sender, EDragType dragType, Vector2 delta)
    {
        ClaimScrollAuthority("thumb-drag");
        if (MaxPixelOffset <= 0f)
            return;
        if (dragType == EDragType.DragStart)
        {
            draggingScrollbar = true;
            dragAccumulated = 0f;
            dragStartOffset = scrollOffsetPixels;
        }
        if (!draggingScrollbar)
            return;

        if (dragType != EDragType.DragEnd)
            dragAccumulated += -delta.y;
        int thumbHeight = scrollThumb != null && scrollThumb.ViewComponent != null ? scrollThumb.ViewComponent.Size.y : MinScrollbarThumb;
        int travel = Math.Max(1, currentScrollViewportHeight - thumbHeight);
        float before = authoritativeScrollTargetPixels;
        float desired = dragStartOffset + dragAccumulated * (MaxPixelOffset / travel);
        SetScrollTarget(desired, true, true);
        TraceScrollChange("thumb-drag:" + dragType, before, targetScrollOffsetPixels);
        if (dragType == EDragType.DragEnd)
            draggingScrollbar = false;
    }

    private void ScrollTrack_OnPress(XUiController sender, int mouseButton)
    {
        ClaimScrollAuthority("track-press");
        if ((mouseButton != 0 && mouseButton != -1) || MaxPixelOffset <= 0f ||
            scrollTrack == null || scrollTrack.ViewComponent == null)
            return;
        Collider collider = scrollTrack.ViewComponent.UiTransform != null
            ? scrollTrack.ViewComponent.UiTransform.GetComponent<Collider>()
            : null;
        Camera camera = UICamera.currentCamera;
        if (collider == null || camera == null)
        {
            float fallbackBefore = authoritativeScrollTargetPixels;
            SetScrollTarget(authoritativeScrollTargetPixels + currentRowStride * 2f, false, true);
            TraceScrollChange("track-fallback", fallbackBefore, targetScrollOffsetPixels);
            return;
        }

        Vector2 mouse = UICamera.currentTouch != null ? UICamera.currentTouch.pos : (Vector2)Input.mousePosition;
        // The thumb sits inside the track. Do not let a thumb press bubble to the track and
        // overwrite the drag destination before dragging begins.
        if (scrollThumb != null && scrollThumb.ViewComponent != null && IsMouseInsideColliderTree(scrollThumb.ViewComponent.UiTransform))
            return;
        Bounds bounds = collider.bounds;
        float y1 = camera.WorldToScreenPoint(bounds.min).y;
        float y2 = camera.WorldToScreenPoint(bounds.max).y;
        float bottom = Mathf.Min(y1, y2);
        float top = Mathf.Max(y1, y2);
        if (top - bottom < 0.01f)
            return;
        float normalized = 1f - Mathf.Clamp01((mouse.y - bottom) / (top - bottom));
        float before = authoritativeScrollTargetPixels;
        SetScrollTarget(normalized * MaxPixelOffset, false, true);
        TraceScrollChange("track-click", before, targetScrollOffsetPixels);
    }

    private void TraceScrollChange(string reason, float before, float after)
    {
        if ((!RebirthPersonalCraftingState.LayoutDebugEnabled && !RebirthLogSettings.CraftingUiLoggingEnabled) || Math.Abs(before - after) < 0.05f)
            return;

        Log.Out("[REBIRTH Crafting RecipeScroll] inputOffsetPx=" + before.ToString("0.0")
            + " refreshReason=" + (string.IsNullOrEmpty(reason) ? "unknown" : reason)
            + " resultingOffsetPx=" + after.ToString("0.0")
            + " maxOffsetPx=" + MaxPixelOffset.ToString("0.0")
            + " authority=" + authoritativeScrollTargetPixels.ToString("0.0")
            + " firstRow=" + firstVisibleIndex
            + " instance=" + GetHashCode()
            + " active=" + (activeInstance == this));
    }

    private void Subscribe()
    {
        if (subscribed || xui == null || xui.playerUI == null || xui.playerUI.entityPlayer == null)
            return;

        // Keep the actual event sources so detached close can clean the original subscriptions.
        var inventory = xui.PlayerInventory;
        var player = xui.playerUI.entityPlayer;
        var tracker = xui.QuestTracker;
        var recipes = xui.Recipes;
        detachSubscriptions = () =>
        {
            RebirthSurvivorClientState.OwnerStateChanged -= OwnerProgressionChanged;
            if (inventory != null)
            {
                inventory.OnBackpackItemsChanged -= PlayerInventoryChanged;
                inventory.OnToolbeltItemsChanged -= PlayerInventoryChanged;
            }
            player.QuestChanged -= QuestChanged;
            player.QuestRemoved -= QuestChanged;
            if (tracker != null) tracker.OnTrackedChallengeChanged -= TrackedChallengeChanged;
            if (recipes != null) recipes.OnTrackedRecipeChanged -= TrackedRecipeChanged;
        };
        if (inventory != null)
        {
            inventory.OnBackpackItemsChanged += PlayerInventoryChanged;
            inventory.OnToolbeltItemsChanged += PlayerInventoryChanged;
        }
        RebirthSurvivorClientState.OwnerStateChanged += OwnerProgressionChanged;
        player.QuestChanged += QuestChanged;
        player.QuestRemoved += QuestChanged;
        if (tracker != null) tracker.OnTrackedChallengeChanged += TrackedChallengeChanged;
        if (recipes != null) recipes.OnTrackedRecipeChanged += TrackedRecipeChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        // Clear bookkeeping before cleanup; the current UI may already be detached or replaced.
        var detach = detachSubscriptions;
        detachSubscriptions = null;
        subscribed = false;
        detach?.Invoke();
    }

    // Owner knowledge/skills can arrive after inventory events without changing bag geometry.
    // Rebuild cached unlock flags through the existing availability path, preserving filters/scroll.
    private void OwnerProgressionChanged(RebirthSurvivorOwnerStateSnapshot ignored)
    {
        availabilityDirty = true;
        pendingRebuildReason = "owner-progression";
    }

    private void PlayerInventoryChanged()
    {
        availabilityDirty = true;
        pendingRebuildReason = "inventory-availability";
    }

    private void QuestChanged(Quest quest)
    {
        filterDirty = true;
        forceSelectionVisible = false;
        resetScrollOnNextRebuild = false;
        pendingRebuildReason = "quest-change";
    }

    private void TrackedChallengeChanged()
    {
        filterDirty = true;
        forceSelectionVisible = false;
        resetScrollOnNextRebuild = false;
        pendingRebuildReason = "challenge-track-change";
    }

    private void TrackedRecipeChanged()
    {
        filterDirty = true;
        forceSelectionVisible = false;
        resetScrollOnNextRebuild = false;
        pendingRebuildReason = "recipe-track-change";
    }


    private void TraceScrollSnapshot(string reason, bool force)
    {
        if (!RebirthLogSettings.CraftingUiLoggingEnabled)
            return;
        int count = recipeInfos != null ? recipeInfos.Count : 0;
        bool changed = Math.Abs(lastTraceCurrent - scrollOffsetPixels) > 0.25f
            || Math.Abs(lastTraceTarget - targetScrollOffsetPixels) > 0.25f
            || lastTraceFirst != firstVisibleIndex
            || lastTraceCount != count;
        if (!force && !changed && Time.realtimeSinceStartup < nextScrollTrace)
            return;
        nextScrollTrace = Time.realtimeSinceStartup + 1.0f;
        lastTraceCurrent = scrollOffsetPixels;
        lastTraceTarget = targetScrollOffsetPixels;
        lastTraceFirst = firstVisibleIndex;
        lastTraceCount = count;
        Log.Out("[REBIRTH Crafting RecipeScroll] snapshot instance=" + GetHashCode()
            + " active=" + (activeInstance == this)
            + " reason=" + (reason ?? "unknown")
            + " current=" + scrollOffsetPixels.ToString("0.0")
            + " target=" + targetScrollOffsetPixels.ToString("0.0")
            + " authority=" + authoritativeScrollTargetPixels.ToString("0.0")
            + " max=" + MaxPixelOffset.ToString("0.0")
            + " first=" + firstVisibleIndex
            + " count=" + count
            + " stride=" + currentRowStride
            + " filterDirty=" + filterDirty
            + " availabilityDirty=" + availabilityDirty
            + " resort=" + resortRecipes
            + " pageChanged=" + pageChanged
            + " resetPending=" + resetScrollOnNextRebuild
            + " pendingReason=" + (pendingRebuildReason ?? "<none>"));
    }

    private static string ControllerId(XUiController controller)
    {
        return controller != null && controller.ViewComponent != null && !string.IsNullOrEmpty(controller.ViewComponent.ID)
            ? controller.ViewComponent.ID
            : "<no-id>";
    }

    private XUiC_RebirthCraftingRecipeEntry FindVisibleEntry(Recipe recipe)
    {
        if (recipe == null || rows == null) return null;
        for (int i = 0; i < rows.Length; i++)
            if (rows[i] != null && rows[i].Recipe == recipe)
                return rows[i];
        return null;
    }

    private void PublishSelectionIfChanged(Recipe recipe)
    {
        if (lastPublishedRecipe == recipe) return;
        lastPublishedRecipe = recipe;
        RebirthSelectionChanged?.Invoke(recipe);
    }

    private void SetNativeCurrentRecipe(Recipe recipe)
    {
        if (NativeCurrentRecipeProperty != null && NativeCurrentRecipeProperty.CanWrite)
        {
            try
            {
                NativeCurrentRecipeProperty.SetValue(this, recipe, null);
                return;
            }
            catch (Exception e)
            {
                Log.Warning("[REBIRTH Crafting] Unable to update native CurrentRecipe contract: " + e.Message);
            }
        }
    }

    private void OpenControllerTree()
    {
        for (int i = 0; i < children.Count; i++)
            children[i].OnOpen();

        if (ViewComponent != null && !ViewComponent.IsVisible)
        {
            ViewComponent.OnOpen();
            ViewComponent.IsVisible = true;
        }
    }

    private void CloseControllerTree()
    {
        for (int i = 0; i < children.Count; i++)
            children[i].OnClose();

        if (ViewComponent != null && ViewComponent.IsVisible)
        {
            ViewComponent.OnClose();
            ViewComponent.IsVisible = false;
        }
    }

    private void UpdateControllerTree(float dt)
    {
        if (ViewComponent != null && WindowGroup != null && WindowGroup.isShowing && ViewComponent.IsVisible)
            ViewComponent.Update(dt);

        if (curInputStyle != lastInputStyle)
        {
            PlayerInputManager.InputStyle previous = lastInputStyle;
            lastInputStyle = curInputStyle;
            RefreshBindings();
        }

        for (int i = 0; i < children.Count; i++)
            children[i].Update(dt);
    }

    private T GetView<T>(string id) where T : XUiView
    {
        XUiController controller = GetChildById(id);
        return controller != null ? controller.ViewComponent as T : null;
    }

    private static void SetRect(XUiView view, int x, int y, int width, int height)
    {
        if (view == null) return;
        view.Position = new Vector2i(x, y);
        view.Size = new Vector2i(Math.Max(1, width), Math.Max(1, height));
    }
}
