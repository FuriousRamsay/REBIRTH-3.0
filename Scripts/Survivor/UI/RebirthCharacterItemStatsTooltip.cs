using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthCharacterItemStatsTooltip
{
    public static XUiController Hovered;
    private sealed class SelectionOwners
    {
        internal XUiC_RebirthAssembleItem Editor;
        internal XUiC_RebirthCharacterBackpack Backpack;
        internal XUiC_RebirthCraftingItemContext Crafting;
        internal float RetryAt;
    }
    private static readonly ConditionalWeakTable<XUiController,SelectionOwners> SelectionCache=new ConditionalWeakTable<XUiController,SelectionOwners>();
    private static SelectionOwners Owners(XUiController surface)
    {
        var owners=SelectionCache.GetValue(surface,key=>new SelectionOwners());
        if(owners.Editor==null&&owners.Backpack==null&&owners.Crafting==null&&Time.realtimeSinceStartup>=owners.RetryAt)
        {
            owners.RetryAt=Time.realtimeSinceStartup+1f;
            if(surface is XUiC_RebirthItemEditorHeader)owners.Editor=surface.GetChildByType<XUiC_RebirthAssembleItem>();
            else if(surface is XUiC_RebirthSurvivorCharacter)owners.Backpack=surface.GetChildByType<XUiC_RebirthCharacterBackpack>();
            else if(surface is XUiC_RebirthPersonalCrafting || surface is XUiC_RebirthStationWorkspace || surface is XUiC_RebirthCookingStation)owners.Crafting=surface.GetChildByType<XUiC_RebirthCraftingItemContext>();
        }
        return owners;
    }

    // Pick the presented surface, not a hidden native inventory that still owns a lease.
    private sealed class SurfaceFrame { internal int Frame=-1; internal XUiController Surface; }
    private static readonly ConditionalWeakTable<XUi,SurfaceFrame> SurfaceFrames=new ConditionalWeakTable<XUi,SurfaceFrame>();
    public static XUiController ActiveSurface(XUi ui = null)
    {
        ui=ui??Hovered?.xui;
        if(ui==null)return ResolveSurface(null);
        var cached=SurfaceFrames.GetValue(ui,_=>new SurfaceFrame());
        if(cached.Frame!=Time.frameCount){cached.Frame=Time.frameCount;cached.Surface=ResolveSurface(ui);}
        return cached.Surface;
    }
    private static readonly ConditionalWeakTable<XUiController,SurfaceFrame> SurfaceRoots=new ConditionalWeakTable<XUiController,SurfaceFrame>();
    private static readonly string[] CardWindowIds = { "rebirthBackpackLibrary", "rebirthBackpackSellStash", "trader", "questTurnIn" };
    private static XUiController ResolveSurface(XUi ui)
    {
        ui = ui ?? Hovered?.xui;
        if(ui!=null)
        {
            foreach(string id in CardWindowIds)
                if(ui.playerUI.windowManager.IsWindowOpen(id))
                {
                    var root=ui.FindWindowGroupByName(id)?.Controller;
                    if(root==null)continue;
                    var rootCache=SurfaceRoots.GetValue(root,_=>new SurfaceFrame());
                    XUiController cardSurface=rootCache.Surface??(XUiController)root.GetChildByType<XUiC_RebirthBackpackLibrary>()
                        ?? (XUiController)root?.GetChildByType<XUiC_RebirthBackpackSellStash>()
                        ?? (XUiController)root?.GetChildByType<XUiC_RebirthTraderSurface>()
                        ?? (XUiController)root?.GetChildByType<XUiC_RebirthQuestTurnInSurface>();
                    if(cardSurface!=null){rootCache.Surface=cardSurface;return cardSurface;}
                }
        }
        var editor = XUiC_RebirthItemEditorHeader.ActiveInstance;
        if (editor?.IsEditorOpen == true && (ui == null || editor.xui == ui)) return editor;
        var context = RebirthContextNavigationService.Session;
        if (context != null && !context.Suspended && (ui == null || context.Ui == ui)) return context.Workspace;
        var station = XUiC_RebirthStationWorkspace.ActiveInstance;
        if (station?.WindowGroup?.isShowing == true && (ui == null || station.xui == ui)) return station;
        var cooking = XUiC_RebirthCookingWorkspace.ActiveInstance;
        if (cooking?.IsCookingOpen == true && (ui == null || cooking.xui == ui))
            return cooking.windowGroup?.Controller is XUiC_RebirthCookingStation milling && milling.UsesSharedMillingPresentation ? (XUiController)milling : cooking;
        var creative = XUiC_RebirthCreativeWorkspace.ActiveInstance;
        if (creative?.IsWorkspaceOpen == true && (ui == null || creative.xui == ui)) return creative;
        var character = XUiC_RebirthSurvivorCharacter.ActiveInstance;
        if (character?.IsCharacterWindowOpen == true && (ui == null || character.xui == ui)) return character;
        var crafting = XUiC_RebirthPersonalCrafting.ActiveInstance;
        return crafting?.State.IsOpen == true && (ui == null || crafting.xui == ui) ? crafting : null;
    }

    public static XUiController Selected
    {
        get
        {
            XUiController surface = ActiveSurface(Hovered?.xui);
            if (surface is XUiC_RebirthItemEditorHeader editor) return Owners(editor).Editor;
            if (surface is XUiC_RebirthContainerWorkspace context) return context.TooltipSelection;
            if (surface is XUiC_RebirthCreativeWorkspace creative) return creative.BackpackView?.SelectedItem;
            if (surface is XUiC_RebirthSurvivorCharacter character) return Owners(character).Backpack?.SelectedItem;
            if (surface is XUiC_RebirthCookingStation milling) return Owners(milling).Crafting?.SelectedSlot;
            if (surface is XUiC_RebirthStationWorkspace station) return Owners(station).Crafting?.SelectedSlot;
            if (surface is XUiC_RebirthPersonalCrafting crafting) return Owners(crafting).Crafting?.SelectedSlot;
            return null;
        }
    }
    public static bool Open => ActiveSurface() != null;
    public static ItemStack Stack(XUiController source) => source is XUiC_ItemStack slot ? slot.ItemStack
        : source is XUiC_EquipmentStack gear ? gear.ItemStack : source is XUiC_AssembleWindow assembly ? assembly.ItemStack : null;
    private static HashSet<int> ingredientTypes;
    private static int ingredientGeneration = -1;
    public static bool FoodPopup(ItemValue item)
    {
        if (item == null || item.IsEmpty()) return false;
        if (RebirthConsumableResolver.TryResolve(item, out var food) && (food.IsFood || food.IsDrink)) return true;
        if (ingredientTypes == null || ingredientGeneration != RebirthSurvivorDefinitionRegistry.Generation)
        {
            ingredientTypes = new HashSet<int>(XUiM_Recipes.GetRecipes().Where(RebirthCookingCatalogue.IsCooking)
                .SelectMany(r => r.ingredients).Select(i => i.itemValue.type));
            ingredientGeneration = RebirthSurvivorDefinitionRegistry.Generation;
        }
        return ingredientTypes.Contains(item.type);
    }
    public static string Build(XUiController source, ItemStack stack)
    {
        // Every nonempty item on these surfaces has a card, even when it has no numeric stats.
        return ActiveSurface(source?.xui) != null && stack != null && !stack.IsEmpty() ? "" : null;
    }
    public static void Hover(XUiController source, bool over)
    {
        if (over) Hovered = source;
        else if (Hovered == source) Hovered = null;
    }
}

/// <summary>
/// One template shared by Character, Crafting, Creative, both editors, the three cooking
/// stations and Loot/Storage. Single cards use the water-card geometry; comparison headers
/// are a separate fixed layout, never mutations of that single-card header.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCharacterStatsPopup : XUiController
{
    private sealed class RowViews
    {
        public XUiController Root;
        public XUiV_Sprite Fill, Icon;
        public XUiV_Label Title, Value, SelectedValue, Details, SelectedDetails;
    }
    private sealed class RowData
    {
        public string Title, Value, SelectedValue = "", Icon = "ui_game_symbol_tool", Atlas = "UIAtlas";
        public int Height = 34;
        public bool Heading;
    }
    // Keep the existing statistics budget; sale value has its own additional row.
    private const int StatisticsRowCapacity = 16;
    private readonly RowViews[] rows = new RowViews[StatisticsRowCapacity + 1];
    private XUiController surface, singleHeader, comparisonHeader;
    private XUiV_Texture background;
    private XUiView frame;
    private XUiV_Sprite divider, singleIcon;
    private XUiV_Label singleName, singleQuality, xmlName, footer;
    private XUiController pendingSource;
    private int pendingType, lastFingerprint = int.MinValue, renderedWidth, renderedHeight;
    private float hoverStarted, nextProjection;
    private bool renderedComparison, renderedFood, configured;

    public override void Init()
    {
        base.Init();
        for (XUiController p = Parent; p != null; p = p.Parent)
            if (p is XUiC_RebirthItemEditorHeader || p is XUiC_RebirthContainerWorkspace ||
                p is XUiC_RebirthCreativeWorkspace || p is XUiC_RebirthPersonalCrafting || p is XUiC_RebirthStationWorkspace || p is XUiC_RebirthCookingStation ||
                p is XUiC_RebirthSurvivorCharacter || p is XUiC_RebirthCookingWorkspace ||
                p is XUiC_RebirthBackpackLibrary || p is XUiC_RebirthBackpackSellStash ||
                p is XUiC_RebirthTraderSurface || p is XUiC_RebirthQuestTurnInSurface)
            { surface = p; break; }
        singleHeader = GetChildById("singlePopupHeader"); comparisonHeader = GetChildById("comparisonPopupHeader");
        background = View<XUiV_Texture>("statsPopupBg"); frame = GetChildById("statsPopupFrame")?.ViewComponent;
        divider = View<XUiV_Sprite>("statsPopupDivider"); singleIcon = GetChildById("singlePopupSlot")?.GetChildById("itemIcon")?.ViewComponent as XUiV_Sprite;
        singleName = View<XUiV_Label>("singlePopupName"); singleQuality = View<XUiV_Label>("singlePopupQuality");
        xmlName = View<XUiV_Label>("statsPopupXmlName"); footer = View<XUiV_Label>("statsPopupFooter");
        for (int i = 0; i < rows.Length; i++)
        {
            XUiController row = GetChildById("statsPopupRow" + i);
            rows[i] = new RowViews { Root = row, Fill = row?.GetChildById("rowFill")?.ViewComponent as XUiV_Sprite,
                Icon = row?.GetChildById("rowIcon")?.ViewComponent as XUiV_Sprite,
                Title = row?.GetChildById("rowTitle")?.ViewComponent as XUiV_Label,
                Value = row?.GetChildById("rowValue")?.ViewComponent as XUiV_Label,
                SelectedValue = row?.GetChildById("rowSelectedValue")?.ViewComponent as XUiV_Label,
                Details = row?.GetChildById("rowDetails")?.ViewComponent as XUiV_Label,
                SelectedDetails = row?.GetChildById("rowSelectedDetails")?.ViewComponent as XUiV_Label };
        }
        configured = surface != null && singleHeader != null && comparisonHeader != null && background != null &&
            frame != null && divider != null && singleIcon != null && singleName != null && singleQuality != null &&
            xmlName != null && footer != null && rows.All(r => r.Root != null && r.Fill != null && r.Icon != null &&
                r.Title != null && r.Value != null && r.SelectedValue != null && r.Details != null && r.SelectedDetails != null);
        if (!configured) Log.Warning("[REBIRTH Item Popup] shared template is incomplete; apply Fix25 source and XML together.");
    }
    private T View<T>(string id) where T : XUiView => GetChildById(id)?.ViewComponent as T;
    private void HideAndReset()
    {
        SetPopupVisible(false);
        pendingSource = null; pendingType = 0; lastFingerprint = int.MinValue; nextProjection = 0f;
    }
    public override void OnOpen() { base.OnOpen(); HideAndReset(); }
    public override void OnClose() { HideAndReset(); base.OnClose(); }
    private void SetPopupVisible(bool visible)
    {
        if (ViewComponent == null) return;
        if (ViewComponent.IsVisible != visible) ViewComponent.IsVisible = visible;
        // A card is read-only presentation, not a native inventory root. Sleep its actual
        // hierarchy as well as its view flag; the plain controller still checks hover above.
        var root = ViewComponent.UiTransform;
        if (root != null && root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
    }
    private static void SetSectionVisible(XUiController section, bool visible)
    {
        var view = section?.ViewComponent;
        if (view == null) return;
        if (view.IsVisible != visible) view.IsVisible = visible;
        var root = view.UiTransform;
        if (root != null && root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
    }
    public override void Update(float dt)
    {
        using var inventoryTiming = RebirthInventoryTiming.Measure(5);
        // Previously base.Update walked all 16 rows and both headers even with no hover.
        // Always evaluate the hover/delay; walk the card's children only when it is displayed.
        ProjectCard();
        if (ViewComponent?.IsVisible == true) base.Update(dt);
#if REBIRTH_UI_DIAGNOSTICS
        else if (surface is XUiC_RebirthCreativeWorkspace)
            RebirthCreativePerformance.CountHiddenPopup();
#endif
    }
    private void ProjectCard()
    {
        if (!configured) { HideAndReset(); return; }
        XUiController source = RebirthCharacterItemStatsTooltip.Hovered;
        ItemStack stack = RebirthCharacterItemStatsTooltip.Stack(source);
        Transform pointer = UICamera.hoveredObject != null ? UICamera.hoveredObject.transform : null;
        if (surface == null || RebirthCharacterItemStatsTooltip.ActiveSurface(xui) != surface ||
            source?.xui != xui || source?.ViewComponent?.UiTransform == null ||
            !source.ViewComponent.IsActiveInHierarchy || pointer == null ||
            !pointer.IsChildOf(source.ViewComponent.UiTransform) || stack == null || stack.IsEmpty() ||
            xui?.DragAndDropWindow == null || !xui.DragAndDropWindow.IsEmpty())
        { HideAndReset(); return; }

        // Container mirrors clone ItemValue during sync. An unchanged slot must NOT restart
        // its hover delay every time that happens (the old reference test prevented its popup).
        if (pendingSource != source || pendingType != stack.itemValue.type)
        {
            pendingSource = source; pendingType = stack.itemValue.type;
            hoverStarted = Time.realtimeSinceStartup; nextProjection = 0f; lastFingerprint = int.MinValue;
            SetPopupVisible(false);
        }
        if (Time.realtimeSinceStartup - hoverStarted < 1f) return;
        if (Time.realtimeSinceStartup < nextProjection)
        { if (ViewComponent.IsVisible) PositionPopup(source, renderedWidth, renderedHeight); return; }
        nextProjection = Time.realtimeSinceStartup + .15f;

        XUiController selected = RebirthCharacterItemStatsTooltip.Selected;
        ItemStack baseline = RebirthCharacterItemStatsTooltip.Stack(selected);
        bool food = RebirthCharacterItemStatsTooltip.FoodPopup(stack.itemValue);
        bool compare = selected != source && baseline != null && !baseline.IsEmpty() &&
            !RebirthStationGridIngredients.IsSameStackSnapshot(stack,baseline) &&
            XUiM_ItemStack.CanCompare(stack.itemValue.ItemClass, baseline.itemValue.ItemClass) &&
            food == RebirthCharacterItemStatsTooltip.FoodPopup(baseline.itemValue);
        bool showXmlName = surface is XUiC_RebirthCreativeWorkspace;
        int fingerprint = ProjectionFingerprint(stack, compare ? baseline : null, compare, showXmlName);
        unchecked
        {
            fingerprint = fingerprint * 31 + RebirthConsumableItemPresentation.LiquidFingerprint(stack);
            if (compare) fingerprint = fingerprint * 31 + RebirthConsumableItemPresentation.LiquidFingerprint(baseline);
            EntityPlayer popupPlayer = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
            fingerprint = fingerprint * 31 + RebirthWeaponSustainedDpsService.GetDisplayFingerprint(popupPlayer, stack.itemValue);
            if (compare) fingerprint = fingerprint * 31 + RebirthWeaponSustainedDpsService.GetDisplayFingerprint(popupPlayer, baseline.itemValue);
        }
        if (fingerprint == lastFingerprint && renderedComparison == compare && renderedFood == food && renderedWidth > 0)
        { PositionPopup(source, renderedWidth, renderedHeight); SetPopupVisible(true); return; }

        var data = food ? BuildFoodRows(stack, compare ? baseline : null)
            : BuildStatRows(stack, compare ? baseline : null);
        if (!food) AppendDamageModifiers(data, stack, compare ? baseline : null);
        // Native price calculation uses the normal markdown when no trader is selected.
        // This is an estimate: trader-specific rates and acceptance can differ.
        int saleEstimate, comparisonEstimate;
        if (RebirthItemSaleEstimate.TryGet(xui, stack, out saleEstimate))
        {
            data.Insert(0, new RowData {
                Title = Localization.Get("xuiRebirthEstimatedStackSaleShort"),
                Value = saleEstimate.ToString("N0", CultureInfo.InvariantCulture),
                SelectedValue = compare && RebirthItemSaleEstimate.TryGet(xui, baseline, out comparisonEstimate) ? comparisonEstimate.ToString("N0", CultureInfo.InvariantCulture) : "",
                Icon = "ui_game_symbol_coin"
            });
            if (data.Count > rows.Length) data.RemoveRange(rows.Length, data.Count - rows.Length);
        }
        int width = compare ? 586 : 440;
        int headerBottom = compare ? 180 : 106;
        int rowTop = headerBottom + (showXmlName ? 38 : 0) + 10;
        int contentHeight = data.Sum(r => r.Height);
        int height = rowTop + contentHeight + (food || data.Count == 0 ? 38 : 12);
        SetPopupVisible(false);
        SetSectionVisible(singleHeader, !compare);
        SetSectionVisible(comparisonHeader, compare);
        ViewComponent.Size = new Vector2i(width, height);
        background.AutoUnload = false;
        if (background.Texture != Texture2D.whiteTexture) background.Texture = Texture2D.whiteTexture;
        background.Color = new Color32(18, 18, 23, 255); background.Size = new Vector2i(width, height);
        frame.Size = new Vector2i(width, height);
        int pad = compare ? 14 : 18;
        Rect(divider, pad, -(rowTop - 10), width - pad * 2, 1);
        xmlName.IsVisible = showXmlName;
        Rect(xmlName, pad, -headerBottom, width - pad * 2, 34);
        xmlName.SetTextImmediately(showXmlName ? stack.itemValue.ItemClass.GetItemName() : "");
        if (compare)
        {
            Set("statsPopupName", stack.itemValue.ItemClass.GetLocalizedItemName());
            Set("statsPopupComparison", baseline.itemValue.ItemClass.GetLocalizedItemName());
            GetChildById("statsPopupSlot").RefreshBindingsSelfAndChildren();
            GetChildById("statsPopupSelectedSlot").RefreshBindingsSelfAndChildren();
        }
        else
        {
            singleIcon.SpriteName = stack.itemValue.GetPropertyOverride("CustomIcon", stack.itemValue.ItemClass.GetIconName());
            singleIcon.Color = stack.itemValue.ItemClass.GetIconTint(stack.itemValue);
            singleName.SetTextImmediately(stack.itemValue.ItemClass.GetLocalizedItemName());
            string quality = "Standard";
            if (food) { if (!stack.itemValue.TryGetMetadata("rebirth.cooking.quality", out quality) || string.IsNullOrWhiteSpace(quality)) quality = "Standard"; }
            else if (stack.itemValue.ItemClass.HasQuality && stack.itemValue.ItemClass.ShowQualityBar)
                quality = "Quality " + stack.itemValue.Quality.ToString(CultureInfo.InvariantCulture);
            singleQuality.SetTextImmediately(quality.ToUpperInvariant());
            singleQuality.Color = quality == "Burnt" || quality == "Overcooked"
                ? new Color32(204,107,100,255) : new Color32(181,140,255,255);
        }
        int y = rowTop;
        for (int i = 0; i < rows.Length; i++)
        {
            RowViews row = rows[i]; SetSectionVisible(row.Root, i < data.Count);
            if (i >= data.Count) continue;
            RowData d = data[i]; int rowWidth = width - pad * 2;
            Rect(row.Root.ViewComponent, pad, -y, rowWidth, d.Height);
            Rect(row.Fill, 0, 0, rowWidth, d.Height);
            row.Fill.Color = i % 2 == 0 ? new Color32(24,24,30,255) : new Color32(18,18,23,255);
            row.Icon.IsVisible = !compare && !d.Heading;
            if (!compare) { row.Icon.UIAtlas = d.Atlas; row.Icon.SpriteName = d.Icon; }
            Rect(row.Title, compare ? 0 : 38, -4, compare ? 178 : 162, d.Height - 6);
            Rect(row.Value, compare ? 186 : 202, -4, compare ? 204 : 202, d.Height - 6);
            row.SelectedValue.IsVisible = compare;
            Rect(row.SelectedValue, 402, -4, 156, d.Height - 6);
            row.Title.Color = d.Heading ? new Color32(181,140,255,255) : new Color32(255,255,255,255);
            if (d.Heading) Rect(row.Title, 0, -4, rowWidth, d.Height - 6);
            row.Title.SetTextImmediately(d.Title);
            bool dps = d.Title.IndexOf("DPS", StringComparison.Ordinal) >= 0;
            SetStatValue(row.Value, row.Details, d.Value, compare ? 186 : 202, compare ? 204 : 202, d.Height, dps, true);
            SetStatValue(row.SelectedValue, row.SelectedDetails, d.SelectedValue, 402, 156, d.Height, dps, compare);
            y += d.Height;
        }
        footer.IsVisible = food || data.Count == 0;
        Rect(footer, pad, -(y + 6), width - pad * 2, 24);
        footer.SetTextImmediately(food ? RebirthConsumableItemPresentation.Footer(stack.itemValue, compare)
            : data.Count == 0 ? "No display statistics for this item." : "");
        renderedWidth = width; renderedHeight = height; renderedComparison = compare; renderedFood = food;
        lastFingerprint = fingerprint;
        PositionPopup(source, width, height);
        FlushGeometry(this); // commit the entire new layout, not a partially repainted frame
        SetPopupVisible(true);
    }

    // Query the game's item/mod effects with each target material. Neutral targets are omitted.
    // This section belongs exclusively to the shared hover card, never selected-item panels.
    private static readonly string[] DamageTargets = { "earth", "stone", "metal", "organic", "wood", "cloth", "water", "terrGravel", "head" };
    private static float TargetModifier(ItemValue item, string target)
    {
        if (item == null || item.IsEmpty()) return 0f;
        var tags = item.ItemClass.ItemTags | FastTags<TagGroup.Global>.Parse("primary,physicalDamage");
        float baseline = EffectManager.GetValue(PassiveEffects.DamageModifier, item, 1f, tags: tags,
            calcEquipment: false, calcHoldingItem: false, calcProgression: false, calcBuffs: false, useMods: true);
        float targeted = EffectManager.GetValue(PassiveEffects.DamageModifier, item, 1f,
            tags: tags | FastTags<TagGroup.Global>.Parse(target),
            calcEquipment: false, calcHoldingItem: false, calcProgression: false, calcBuffs: false, useMods: true);
        return (targeted - baseline) * 100f;
    }
    private static string ModifierText(float value) => Math.Abs(value) < .05f ? "" :
        (value > 0 ? "[77CC77]+" : "[EE7777]") + value.ToString("0.#", CultureInfo.InvariantCulture) + "%[-]";
    private static void AppendDamageModifiers(List<RowData> data, ItemStack stack, ItemStack selected)
    {
        if (!RebirthWeaponSustainedDpsService.TryGetDisplayProfile(null, stack.itemValue, out var unused)) return;
        var modifiers = new List<RowData>();
        foreach (string target in DamageTargets)
        {
            float value = TargetModifier(stack.itemValue, target);
            float other = TargetModifier(selected?.itemValue, target);
            if (Math.Abs(value) < .05f && Math.Abs(other) < .05f) continue;
            modifiers.Add(new RowData { Title = Localization.Get("xuiRebirthDamageTarget_" + target),
                Value = ModifierText(value), SelectedValue = ModifierText(other) });
        }
        if (modifiers.Count == 0) return;
        data.Add(new RowData { Title = Localization.Get("xuiRebirthDamageModifiers"), Value = "", Heading = true, Height = 38 });
        data.AddRange(modifiers);
    }

    private List<RowData> BuildFoodRows(ItemStack stack, ItemStack selected)
    {
        System.Collections.Generic.List<RebirthConsumableItemPresentation.Row> presentation;
        var result = new List<RowData>();
        if (!RebirthConsumableItemPresentation.TryBuild(stack, selected, xui, out presentation))
            return result;
        foreach (var row in presentation)
            result.Add(new RowData { Title = row.Title, Value = row.Value,
                SelectedValue = row.SelectedValue, Atlas = row.Atlas, Icon = row.Icon });
        return result;
    }

    private List<RowData> BuildStatRows(ItemStack stack, ItemStack selected)
    {
        var result = new List<RowData>();
        // Selected panels and hover cards use exactly the same stat rows.
        if (RebirthWeaponDetailRows.TryGet(xui, stack, null, 0, out var firstTitle, out var firstValue))
        {
            for (int row = 0; row < 7; row++)
            {
                if (!RebirthWeaponDetailRows.TryGet(xui, stack, null, row, out var title, out var value) || string.IsNullOrEmpty(title)) continue;
                string selectedValue = "";
                if (selected != null && RebirthWeaponDetailRows.TryGet(xui, selected, null, row, out var selectedTitle, out var comparisonValue) && selectedTitle == title)
                    selectedValue = comparisonValue;
                result.Add(new RowData { Title = title, Value = value, SelectedValue = selectedValue, Height = 34,
                    Icon = title.IndexOf("Stamina", StringComparison.OrdinalIgnoreCase) >= 0 ? "ui_game_symbol_run" : "ui_game_symbol_tool" });
            }
            return result;
        }
        EntityPlayer player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        RebirthWeaponSustainedDpsService.Profile dps = null, selectedDps = null, baseDps = null, selectedBaseDps = null;
        bool hasDps = RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player, stack.itemValue, out dps) && dps != null && dps.Valid;
        bool hasSelectedDps = selected != null && RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player, selected.itemValue, out selectedDps) && selectedDps != null && selectedDps.Valid;
        ItemValue noModsForDps = hasDps && dps.Melee ? WithoutMods(stack.itemValue) : null;
        bool hasBaseDps = noModsForDps != null && RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player, noModsForDps, out baseDps) && baseDps != null && baseDps.Valid;
        ItemValue selectedNoModsForDps = hasSelectedDps && selectedDps.Melee ? WithoutMods(selected.itemValue) : null;
        bool hasSelectedBaseDps = selectedNoModsForDps != null && RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player, selectedNoModsForDps, out selectedBaseDps) && selectedBaseDps != null && selectedBaseDps.Valid;
        bool showDerivedDps = hasDps && !dps.Explosive;
        if (showDerivedDps)
        {
            if (dps.Ranged)
            {
                result.Add(new RowData {
                    Title = "Sustained DPS",
                    Value = RebirthWeaponSustainedDpsService.BuildUiValue(dps),
                    SelectedValue = hasSelectedDps ? RebirthWeaponSustainedDpsService.BuildUiValue(selectedDps) : "",
                    // Compact derived summary: DPS, total shot damage and effective reload time.
                    Height = dps.ReloadSeconds > 0f ? 54 : 38,
                    Icon = "ui_game_symbol_tool"
                });
            }
            else
            {
                result.Add(new RowData {
                    Title = "Normal Attack DPS",
                    Value = RebirthItemStatColors.MarkDerived(BuildMeleeDpsValue(dps.NormalAttackDps, hasBaseDps ? baseDps.NormalAttackDps : dps.NormalAttackDps),stack.itemValue,PassiveEffects.EntityDamage,PassiveEffects.AttacksPerMinute),
                    SelectedValue = hasSelectedDps && selectedDps.Melee ? RebirthItemStatColors.MarkDerived(BuildMeleeDpsValue(selectedDps.NormalAttackDps, hasSelectedBaseDps ? selectedBaseDps.NormalAttackDps : selectedDps.NormalAttackDps),selected.itemValue,PassiveEffects.EntityDamage,PassiveEffects.AttacksPerMinute) : "",
                    Height = 78, // Main value plus two separately sized supporting lines.
                    Icon = "ui_game_symbol_tool"
                });
                if (dps.PowerAttackDps > 0f || (hasSelectedDps && selectedDps.Melee && selectedDps.PowerAttackDps > 0f))
                    result.Add(new RowData {
                        Title = "Power Attack DPS",
                        Value = RebirthItemStatColors.MarkDerived(BuildMeleeDpsValue(dps.PowerAttackDps, hasBaseDps ? baseDps.PowerAttackDps : dps.PowerAttackDps),stack.itemValue,PassiveEffects.EntityDamage,PassiveEffects.AttacksPerMinute),
                        SelectedValue = hasSelectedDps && selectedDps.Melee ? RebirthItemStatColors.MarkDerived(BuildMeleeDpsValue(selectedDps.PowerAttackDps, hasSelectedBaseDps ? selectedBaseDps.PowerAttackDps : selectedDps.PowerAttackDps),selected.itemValue,PassiveEffects.EntityDamage,PassiveEffects.AttacksPerMinute) : "",
                        Height = 78, // Main value plus two separately sized supporting lines.
                        Icon = "ui_game_symbol_tool"
                    });
            }
        }

        var display = UIDisplayInfoManager.Current.GetDisplayStatsForTag(stack.itemValue.ItemClass.DisplayType);
        if (display == null) return result;
        ItemValue baseItem = WithoutMods(stack.itemValue), selectedBase = selected != null ? WithoutMods(selected.itemValue) : null;
        // Filter before taking the row budget so later useful stats move up without gaps.
        // Any REBIRTH combat profile owns its DPS presentation, so suppress the generic authored
        // DPS row for explosives too (a grenade must never show "Normal Attack DPS 0"). For ranged
        // weapons, Magazine Size and Rounds / Minute are already inputs to Sustained DPS and the
        // owner explicitly does not want them repeated as standalone popup rows.
        int nativeBudget = Math.Max(0, StatisticsRowCapacity - result.Count);
        foreach (var stat in display.DisplayStats.Where(s => s != null && s.StatType != PassiveEffects.RoundRayCount &&
            (!hasDps || !IsAuthoredDpsStat(s)) &&
            !(hasDps && dps.Ranged && IsRedundantRangedPopupStat(s)) &&
            !(hasDps && dps.Melee && IsRedundantMeleePopupStat(s)) &&
            RebirthConsumableItemPresentation.ShouldShowNativeStat(s, stack.itemValue, selected?.itemValue, xui)).Take(nativeBudget))
        {
            string mods = ModContribution(stack, stat), selectedMods = selected != null ? ModContribution(selected, stat) : "";
            string total = TotalText(stack.itemValue, stat);
            if (selected != null && Neutral(total).Replace("★", "").Trim() != Neutral(TotalText(selected.itemValue, stat)).Replace("★", "").Trim())
                total = CompareTotal(total, TotalNumber(stack.itemValue, stat), TotalNumber(selected.itemValue, stat), stat);
            bool ranged = IsRangedDamage(stack.itemValue, stat);
            string statId = stat.StatType.ToString();
            bool pairedMelee=hasDps&&dps.Melee&&(stat.StatType==PassiveEffects.BlockDamage||stat.StatType==PassiveEffects.StaminaLoss);
            string paired=pairedMelee?(stat.StatType==PassiveEffects.BlockDamage?dps.NormalBlockDamagePerAttack.ToString("0.#")+" ("+dps.PowerBlockDamagePerAttack.ToString("0.#")+")":dps.NormalStaminaCost.ToString("0.#")+" ("+dps.PowerStaminaCost.ToString("0.#")+")"):null;
            result.Add(new RowData {
                Title = IsShotDamage(stack.itemValue, stat) ? (stat.StatType == PassiveEffects.EntityDamage ? "Ranged Damage" : "Block Damage")
                    : stat.TitleOverride ?? UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType),
                Value = pairedMelee ? RebirthItemStatColors.Format(RebirthItemStatColors.WithBonus(paired,stack.itemValue,stat)) : ranged ? DamageBreakdown(stack.itemValue, stat, total) : Breakdown(baseItem, stat, total, mods),
                SelectedValue = selected == null ? "" : IsRangedDamage(selected.itemValue, stat)
                    ? DamageBreakdown(selected.itemValue, stat, TotalText(selected.itemValue, stat))
                    : Breakdown(selectedBase, stat, TotalText(selected.itemValue, stat), selectedMods),
                Height = ranged || (selected != null && IsRangedDamage(selected.itemValue,stat)) ? 92 : mods.Length > 0 || selectedMods.Length > 0 ? 74 : 34,
                Icon = statId.IndexOf("Health", StringComparison.OrdinalIgnoreCase) >= 0 || statId.IndexOf("Heal", StringComparison.OrdinalIgnoreCase) >= 0 || statId.IndexOf("Bleed", StringComparison.OrdinalIgnoreCase) >= 0 ? "ui_game_symbol_medical"
                    : statId.IndexOf("Stamina", StringComparison.OrdinalIgnoreCase) >= 0 ? "ui_game_symbol_run" : "ui_game_symbol_tool" });
        }
        return result;
    }

    private void PositionPopup(XUiController source, int width, int height)
    {
        if (Parent?.ViewComponent?.UiTransform == null || width <= 0 || height <= 0) return;
        var parent = Parent.ViewComponent;
        var point = parent.UiTransform.InverseTransformPoint(source.ViewComponent.UiTransform.position);
        float scale = Mathf.Min(1f, Mathf.Min(Mathf.Max(1,parent.Size.x - 24) / (float)width, Mathf.Max(1,parent.Size.y - 24) / (float)height));
        var targetScale = new Vector3(scale, scale, 1f);
        if (ViewComponent.UiTransform != null && ViewComponent.UiTransform.localScale != targetScale)
            ViewComponent.UiTransform.localScale = targetScale;
        int usedWidth = Mathf.CeilToInt(width * scale), usedHeight = Mathf.CeilToInt(height * scale);
        var next = new Vector2i(Mathf.Clamp((int)point.x - usedWidth - 12, 12, Math.Max(12, parent.Size.x - usedWidth - 12)),
            Mathf.Clamp((int)point.y, Math.Min(-12, -parent.Size.y + usedHeight + 12), -12));
        if (ViewComponent.Position != next) { ViewComponent.Position = next; ViewComponent.TryUpdatePosition(); }
    }
    private static void Rect(XUiView view, int x, int y, int w, int h)
    {
        if (view == null) return;
        view.Position = new Vector2i(x,y); view.Size = new Vector2i(w,h);
    }
    private static void FlushGeometry(XUiController controller)
    {
        if (controller.ViewComponent?.UiTransform != null) controller.ViewComponent.updateData();
        foreach (var child in controller.Children) FlushGeometry(child);
    }
    private static int ProjectionFingerprint(ItemStack hovered,ItemStack selected,bool compare,bool xmlName)
    {
        unchecked{return (((ItemFingerprint(hovered)*31+ItemFingerprint(selected))*31+(compare?1:0))*31+(xmlName?1:0));}
    }
    private static int ItemFingerprint(ItemStack stack)
    {
        if(stack==null||stack.IsEmpty()||stack.itemValue==null)return 0;
        unchecked
        {
            ItemValue value=stack.itemValue;int hash=17;
            hash=hash*31+value.type;hash=hash*31+value.Meta;hash=hash*31+value.Quality;hash=hash*31+value.UseTimes.GetHashCode();hash=hash*31+stack.count;
            if(value.Metadata!=null&&value.Metadata.Count>0)
            {
                var keys=new System.Collections.Generic.List<string>(value.Metadata.Keys);keys.Sort(System.StringComparer.Ordinal);
                for(int i=0;i<keys.Count;i++){string key=keys[i]??string.Empty;object metadata=value.Metadata[key];hash=hash*31+System.StringComparer.Ordinal.GetHashCode(key);hash=hash*31+(metadata!=null?metadata.GetHashCode():0);}
            }
            ItemValue[] mods=value.modifications;hash=hash*31+(mods!=null?mods.Length:0);
            for(int i=0;mods!=null&&i<mods.Length;i++)hash=hash*31+(mods[i]!=null?mods[i].type:0);
            ItemValue[] cosmetics=value.cosmeticMods;hash=hash*31+(cosmetics!=null?cosmetics.Length:0);
            for(int i=0;cosmetics!=null&&i<cosmetics.Length;i++)hash=hash*31+(cosmetics[i]!=null?cosmetics[i].type:0);
            return hash;
        }
    }
    // Never show a separately authored/passive DPS row beside REBIRTH's derived sustained DPS.
    // The displayed value must come from the same live calculation used by combat training.
    private static bool IsAuthoredDpsStat(DisplayInfoEntry stat)
    {
        if (stat == null) return false;
        string id = stat.StatType.ToString() ?? string.Empty;
        string title = stat.TitleOverride ?? string.Empty;
        return id.IndexOf("DamagePerSecond", StringComparison.OrdinalIgnoreCase) >= 0 ||
            id.IndexOf("DPS", StringComparison.OrdinalIgnoreCase) >= 0 ||
            title.IndexOf("Damage Per Second", StringComparison.OrdinalIgnoreCase) >= 0 ||
            title.IndexOf("DPS", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsRedundantRangedPopupStat(DisplayInfoEntry stat)
    {
        if (stat == null) return false;
        string id = stat.StatType.ToString();
        if (string.Equals(id, "MagazineSize", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "RoundsPerMinute", StringComparison.OrdinalIgnoreCase))
            return true;
        string title = stat.TitleOverride ?? UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType) ?? string.Empty;
        return string.Equals(title.Trim(), "Magazine Size", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(title.Trim(), "Rounds / Minute", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(title.Trim(), "Rounds/Minute", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(title.Trim(), "Stun Target", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedundantMeleePopupStat(DisplayInfoEntry stat)
    {
        if (stat == null) return false;
        string id = stat.StatType.ToString() ?? string.Empty;
        string title = stat.TitleOverride ?? UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType) ?? string.Empty;
        return id.IndexOf("EntityDamage", StringComparison.OrdinalIgnoreCase) >= 0 ||
            title.IndexOf("Melee Damage", StringComparison.OrdinalIgnoreCase) >= 0 ||
            title.IndexOf("Power Attack Damage", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsRangedDamage(ItemValue item, DisplayInfoEntry stat) =>
        (stat.StatType == PassiveEffects.EntityDamage || stat.StatType == PassiveEffects.BlockDamage) &&
        item.ItemClass.Actions != null && item.ItemClass.Actions.Length > 0 && item.ItemClass.Actions[0] is ItemActionRanged;
    private float Number(ItemValue item, DisplayInfoEntry stat, bool mods = true)
    {
        var tags = stat.TagsSet ? stat.Tags : XUiM_ItemStack.primaryFastTags | XUiM_ItemStack.physicalDamageFastTags;
        float value = string.IsNullOrEmpty(stat.CustomName)
            ? EffectManager.GetValue(stat.StatType,item,_entity:xui.playerUI.entityPlayer,tags:tags,calcEquipment:false,calcHoldingItem:false,calcProgression:false,calcBuffs:false,useMods:mods)
            : XUiM_ItemStack.GetCustomValue(stat,item,mods);
        XUiM_ItemStack.degradationMaxMod(stat.StatType,item,xui.playerUI.entityPlayer,tags,mods,ref value);
        return value;
    }
    private static bool IsShotDamage(ItemValue item, DisplayInfoEntry stat) => IsRangedDamage(item,stat) ||
        ((stat.StatType == PassiveEffects.EntityDamage || stat.StatType == PassiveEffects.BlockDamage) && item.ItemClass.DisplayType.StartsWith("ammo",System.StringComparison.OrdinalIgnoreCase));
    private float Pellets(ItemValue item, bool mods)
    {
        var stat = UIDisplayInfoManager.Current.GetDisplayStatsForTag(item.ItemClass.DisplayType)?.DisplayStats.FirstOrDefault(s => s.StatType == PassiveEffects.RoundRayCount);
        var tags = stat != null && stat.TagsSet ? stat.Tags : item.ItemClass.ItemTags;
        return Mathf.Max(1,EffectManager.GetValue(PassiveEffects.RoundRayCount,item,1,_entity:xui.playerUI.entityPlayer,tags:tags,calcEquipment:false,calcHoldingItem:false,calcProgression:false,calcBuffs:false,useMods:mods));
    }
    private float TotalNumber(ItemValue item, DisplayInfoEntry stat) => Number(item,stat) * (IsShotDamage(item,stat) ? Pellets(item,true) : 1);
    private string TotalText(ItemValue item, DisplayInfoEntry stat) => RebirthItemStatColors.Format(RebirthItemStatColors.WithBonus(IsShotDamage(item,stat) ? TotalNumber(item,stat).ToString("0.#") : StableTotal(item,stat),item,stat));
    private static string CompareTotal(string text, float value, float baseline, DisplayInfoEntry stat)
    {
        if (Mathf.Abs(value-baseline)<0.001f) return text;
        bool better = stat.NegativePreferred ? value < baseline : value > baseline;
        return (better ? "[8FD18F]" : "[CC6B64]") + text + (better ? " ▲" : " ▼") + "[-]";
    }
    private string DamageBreakdown(ItemValue item, DisplayInfoEntry stat, string total)
    {
        var player = xui.playerUI.entityPlayer;
        var context = player.MinEventContext;
        var oldItem = context.ItemValue; var oldSeed = context.Seed;
        var cachedItem = MinEventParams.CachedEventParam.ItemValue; var cachedSeed = MinEventParams.CachedEventParam.Seed;
        float weapon = 0, multiplier = 1;
        try
        {
            context.ItemValue = MinEventParams.CachedEventParam.ItemValue = item;
            context.Seed = MinEventParams.CachedEventParam.Seed = item.Seed + (item.Seed != 0 ? (int)stat.StatType : 0);
            var tags = stat.TagsSet ? stat.Tags : XUiM_ItemStack.primaryFastTags | XUiM_ItemStack.physicalDamageFastTags;
            item.ItemClass.Effects?.ModifyValue(player,stat.StatType,ref weapon,ref multiplier,item.Quality,tags);
            weapon *= multiplier;
        }
        finally
        {
            context.ItemValue = oldItem; context.Seed = oldSeed;
            MinEventParams.CachedEventParam.ItemValue = cachedItem; MinEventParams.CachedEventParam.Seed = cachedSeed;
        }
        var damage = new RebirthShotDamage(weapon,Number(item,stat,false),Number(item,stat),Pellets(item,false),Pellets(item,true));
        return total + "\n[8E8E8E][sub]Base: " + damage.Base.ToString("0.#") + "[/sub][-]\n[8E8E8E][sub]Ammo: " + Signed(damage.Ammo) + "[/sub][-]\n[8E8E8E][sub]Mods: " + Signed(damage.Mods) + "[/sub][-]";
    }
    private static string Signed(float value) => (value > 0 ? "+" : "") + value.ToString("0.#");
    // Supporting DPS text uses a real 18px label, not NGUI's tiny subscript scale.
    private static void SetStatValue(XUiV_Label main, XUiV_Label detail, string text, int x, int width, int height, bool split, bool visible)
    {
        text = text ?? "";
        int newline = split ? text.IndexOf('\n') : -1;
        main.IsVisible = visible;
        detail.IsVisible = visible && newline >= 0;
        Rect(main, x, -4, width, newline >= 0 ? 24 : height - 6);
        main.SetTextImmediately(newline >= 0 ? text.Substring(0, newline) : text);
        Rect(detail, x, -28, width, Math.Max(24, height - 30));
        detail.SetTextImmediately(newline >= 0 ? text.Substring(newline + 1).Replace("[sub]", "").Replace("[/sub]", "").Replace("[8E8E8E]", "").Replace("[-]", "") : "");
    }

    private static string BuildMeleeDpsValue(float totalDps, float baseDps)
    {
        float modDps = totalDps - baseDps;
        return totalDps.ToString("0.#", CultureInfo.InvariantCulture) +
            "\n[8E8E8E][sub]Base: " + baseDps.ToString("0.#", CultureInfo.InvariantCulture) + "[/sub][-]" +
            "\n[8E8E8E][sub]Mods: " + Signed(modDps) + "[/sub][-]";
    }
    private static string Neutral(string text) => Regex.Replace(text,@"\[(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8}|-)\]", "");
    // Presentation-only copies; never modify the inventory item's attachments.
    private static ItemValue WithoutMods(ItemValue item)
    {
        var copy = item.Clone();
        copy.modifications = System.Array.Empty<ItemValue>();
        copy.cosmeticMods = System.Array.Empty<ItemValue>();
        return copy;
    }
    private static string RemoveInspectSuffix(string text)
    {
        int suffix = text.IndexOf(" [00f0f0](", System.StringComparison.OrdinalIgnoreCase);
        return suffix < 0 ? text : text.Substring(0,suffix);
    }
    private string StableTotal(ItemValue item, DisplayInfoEntry stat) => RebirthItemStatColors.Format(Neutral(RemoveInspectSuffix(
        XUiM_ItemStack.GetStatItemValueTextWithModColoring(item,xui.playerUI.entityPlayer,stat))));
    private string Breakdown(ItemValue baseItem, DisplayInfoEntry stat, string total, string mods)
    {
        if (mods.Length == 0) return total;
        return "[BBBBBB]Base: [-]" + StableTotal(baseItem,stat) + mods + "\n[BBBBBB]Total: [-]" + total;
    }
    private string ModContribution(ItemStack item, DisplayInfoEntry stat)
    {
        if (stat.DisplayType == DisplayInfoEntry.DisplayTypes.Bool) return "";
        string formatted = RebirthItemStatColors.NativeValue(item,xui.playerUI.entityPlayer,stat);
        int start = formatted.IndexOf(" (");
        if (start < 0 || !formatted.EndsWith(")")) return "";
        return "\n[BBBBBB]Mods: [-]" + Neutral(formatted.Substring(start + 2, formatted.Length - start - 3));
    }
    private void Set(string id,string text) => (GetChildById(id).ViewComponent as XUiV_Label)?.SetTextImmediately(text);
}

// Read-only presentation: no cloned inventory controller or inventory transactions.
[Preserve]
public sealed class XUiC_RebirthStatsSlotVisual : XUiController
{
    private bool selectedPreview;
    public override void Update(float dt)
    {
        base.Update(dt);
        var source=selectedPreview?RebirthCharacterItemStatsTooltip.Selected:RebirthCharacterItemStatsTooltip.Hovered;
        if(source!=null)RebirthSelectedDurability.CopyNativePresentation(this,source,false,RebirthCharacterItemStatsTooltip.Stack(source));
    }
    public override bool ParseAttribute(string name, string value)
    {
        if (name == "selected_preview") { selectedPreview = value == "true"; return true; }
        return base.ParseAttribute(name, value);
    }

    public override bool GetBindingValueInternal(ref string value, string name)
    {
        var source = selectedPreview
            ? RebirthCharacterItemStatsTooltip.Selected
            : RebirthCharacterItemStatsTooltip.Hovered;
        if (source is XUiC_ItemStack || source is XUiC_EquipmentStack || source is XUiC_AssembleWindow)
        {
            var stack = RebirthCharacterItemStatsTooltip.Stack(source);
            var item = stack?.itemValue;
            bool has = stack != null && !stack.IsEmpty();
            switch (name)
            {
                case "itemicon": value = has ? item.GetPropertyOverride("CustomIcon",item.ItemClass.GetIconName()) : ""; return true;
                case "iconcolor": value = "255,255,255,255"; return true;
                case "hasdurability": value = (has && item.ItemClass.ShowQualityBar).ToString(); return true;
                case "durabilitycolor": var color = (Color32)QualityInfo.GetQualityColor(has ? item.Quality : 0); value = color.r+","+color.g+","+color.b+",255"; return true;
                case "durabilityfill": value = has && item.MaxUseTimes > 0 ? ((item.MaxUseTimes-item.UseTimes)/item.MaxUseTimesUI).ToString(System.Globalization.CultureInfo.InvariantCulture) : "1"; return true;
                case "itemcount": value = has ? (item.ItemClass.ShowQualityBar ? item.Quality.ToString() : stack.count > 1 ? stack.count.ToString() : "") : ""; return true;
            }
        }
        if (source is XUiC_ItemStack && source.GetBindingValue(ref value, name)) return true;
        // Register this binding even while the hidden popup has no hovered item.
        switch (name)
        {
            case "hasdurability": case "haspermadurability": case "hasitemtypeicon":
            case "isfavorite": case "isQuickSwap": case "isassemblelocked":
            case "userlockmode": case "userlockedslot":
                value = "false"; return true;
            case "itemicon": case "locktypeicon": case "itemtypeicon":
            case "itemcount": case "stacklockicon":
                value = ""; return true;
            case "iconcolor": case "itemtypeicontint": case "durabilitycolor": case "stacklockcolor":
                value = "255,255,255,255"; return true;
            case "durabilityfill": case "removeddurabilityfill":
                value = "0"; return true;
        }
        return base.GetBindingValueInternal(ref value, name);
    }
}


/// <summary>Read-only popup headers/rows: no dormant inventory or action logic is skipped.</summary>
[Preserve]
public sealed class XUiC_RebirthPopupSection : XUiController
{
    public override void Update(float dt)
    {
        using var inventoryTiming = RebirthInventoryTiming.Measure(5);
        var view = ViewComponent;
        if (view == null) { base.Update(dt); return; }
        bool visible = view.IsVisible;
        var root = view.UiTransform;
        if (!visible)
        {
            // Commit its visibility change, then leave the descendants alone. Their current
            // labels/icons remain cached and will be projected before this section is shown.
            if (root != null && root.gameObject.activeSelf)
            {
                view.Update(dt);
                root.gameObject.SetActive(false);
            }
            return;
        }
        if (root != null && !root.gameObject.activeSelf) root.gameObject.SetActive(true);
        base.Update(dt);
    }
}
