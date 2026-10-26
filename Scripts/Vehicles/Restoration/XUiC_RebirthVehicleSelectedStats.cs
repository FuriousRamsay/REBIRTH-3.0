using UnityEngine;
using UnityEngine.Scripting;

// Selected-item presentation for the vehicle window. It reads the editor backpack's InspectionStack,
// which already holds either the selected bag item or the previewed installed vehicle part, so no
// second selection state exists.
//
// Preferred path: the vehicle window group hosts the native itemInfoPanel (Config/_Vehicles/xui.xml) and
// RebirthVehicleInfoActionsPatch is healthy, so the same stack is pushed into that shared pane (the ordinary
// selected-item pane used by the backpack and companion windows; icon, quality, full stats, mods, tooltip).
// While it is showing, the hand-built rows, icon, name and description are hidden.
// Fallback path: the stat rows the companion Equipment inspector uses (RebirthSelectedItemRows.Build) are
// painted under the vehicle window's selected-item preview.
[Preserve]
public sealed class XUiC_RebirthVehicleSelectedStats : XUiController
{
    private const int Rows = 5;
    private const int ModCells = 12; // 8 modifier cells + 4 cosmetic cells
    private const int ModSlots = 8;
    // Pane top-left inside the restoration window: vehicleBody (8,-70) + editorBackpack (1214,0) + 2 side, 4 top.
    private const int PaneOffsetX = 8 + 1214 + 2;
    private const int PaneOffsetY = -70 - 4;
    private XUiC_RebirthEditorBackpack backpack;
    private readonly XUiV_Label[] statNames = new XUiV_Label[Rows];
    private readonly XUiV_Label[] statValues = new XUiV_Label[Rows];
    private readonly XUiV_Sprite[] modIcons = new XUiV_Sprite[ModCells];
    private readonly string[] names = new string[Rows];
    private readonly string[] values = new string[Rows];
    private bool hasShown;
    private int shownType, shownCount, shownQuality, shownMods, shownHover;
    private float shownUse;
    private float nextRefresh;
    // Shared native pane state.
    private XUiC_ItemInfoWindow info;
    private XUiC_RebirthReadonlyItemSlot proxy;
    private XUiC_RebirthEditorBackpackSlot geometryCell;
    private XUiController legacyIcon, legacyName, legacyDescription, legacyPreview, legacyHeading, legacyRule, legacyBackground;
    private bool paneVisible;
    private Vector2i panePosition = new Vector2i(int.MinValue, int.MinValue);

    private bool Shared => info != null && RebirthVehicleInfoActionsPatch.Healthy;

    public override void Init()
    {
        base.Init();
        RebirthVehicleInfoActionsPatch.Install();
        for (int i = 0; i < ModCells; i++) modIcons[i] = GetChildById("vehicleSelMod" + i)?.ViewComponent as XUiV_Sprite;
        for (int i = 0; i < Rows; i++)
        {
            statNames[i] = GetChildById("vehicleSelStatName" + i)?.ViewComponent as XUiV_Label;
            statValues[i] = GetChildById("vehicleSelStatValue" + i)?.ViewComponent as XUiV_Label;
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        hasShown = false;
        nextRefresh = 0f;
        Paint(null);
        ResolveShared();
    }

    public override void OnClose()
    {
        HidePane();
        info = null;
        proxy = null;
        geometryCell = null;
        base.OnClose();
    }

    private void ResolveShared()
    {
        // Vehicle selection is rendered inside its existing pane. Hosting the
        // native floating itemInfoPanel here covered the pane and intercepted input.
        if (backpack == null) backpack = GetParentByType<XUiC_RebirthEditorBackpack>();
        XUiC_RebirthVehicleRestoration host = GetParentByType<XUiC_RebirthVehicleRestoration>();
        // Only the pane hosted by THIS window group counts; a pane of another group would be invisible here.
        info = windowGroup?.Controller?.GetChildByType<XUiC_ItemInfoWindow>();
        proxy = host?.GetChildById("vehicleSelectedNative") as XUiC_RebirthReadonlyItemSlot;
        geometryCell = backpack?.GetChildByType<XUiC_RebirthEditorBackpackSlot>();
        legacyIcon = backpack?.GetChildById("editorBagItemIcon");
        legacyName = backpack?.GetChildById("editorBagItemName");
        legacyDescription = backpack?.GetChildById("editorBagItemDescription");
        legacyPreview = backpack?.GetChildById("editorSelectionPreview");
        legacyHeading = backpack?.GetChildById("editorSelectedHeading");
        legacyRule = backpack?.GetChildById("editorSelectedRule");
        legacyBackground = backpack?.GetChildById("vehicleSelectedFallbackBg");
        // The proxy is required: an installed part has no ItemStack controller of its own.
        if (proxy == null) info = null;
        paneVisible = false;
        panePosition = new Vector2i(int.MinValue, int.MinValue);
        // First open: nothing is selected yet and the native pane may not have finished its own OnOpen, so only
        // hide it; ClearSelectedStacks is reserved for closing and for returning to an empty selection.
        if (info != null && info.ViewComponent != null) info.ViewComponent.IsVisible = false;
        ApplyLegacyVisibility();
    }

    // The pane window position comes from the panel layout, so it is placed over the selected-item area here,
    // relative to the restoration window, the same way the companion window does it.
    private void PlacePane()
    {
        XUiC_RebirthVehicleRestoration host = GetParentByType<XUiC_RebirthVehicleRestoration>();
        if (!Shared || info.ViewComponent == null || host?.ViewComponent == null) return;
        Vector2i origin = host.ViewComponent.Position;
        Vector2i target = new Vector2i(origin.x + PaneOffsetX, origin.y + PaneOffsetY);
        if (target.x == panePosition.x && target.y == panePosition.y) return;
        panePosition = target;
        info.ViewComponent.Position = target;
        info.ViewComponent.TryUpdatePosition();
    }

    private void HidePane()
    {
        paneVisible = false;
        if (info == null || !Shared) return;
        info.ClearSelectedStacks();
        if (info.ViewComponent != null) info.ViewComponent.IsVisible = false;
    }

    private static void SetShown(XUiController controller, bool show)
    {
        XUiView view = controller?.ViewComponent;
        if (view != null && view.IsVisible != show) view.IsVisible = show;
    }

    // The legacy selected-item widgets sit under the pane, so they are hidden only while it is showing.
    private void ApplyLegacyVisibility()
    {
        bool legacy = !paneVisible;
        SetShown(legacyBackground, legacy);
        SetShown(legacyHeading, legacy);
        SetShown(legacyRule, legacy);
        SetShown(legacyName, legacy);
        SetShown(legacyDescription, legacy);
        SetShown(legacyPreview, legacy);
        SetShown(this, legacy);
        // The icon is shown and hidden by the editor backpack itself; it is only suppressed under the pane.
        if (!legacy) SetShown(legacyIcon, false);
    }

    private void PushPane(ItemStack current)
    {
        if (info == null || proxy == null) { paneVisible = false; ApplyLegacyVisibility(); return; }
        XUiC_ItemStack selectedCell = backpack?.SelectedItem;
        bool cellSelected = selectedCell is XUiC_RebirthEditorBackpackSlot && (xui?.DragAndDropWindow == null || xui.DragAndDropWindow.IsEmpty())
            && ReferenceEquals(selectedCell.ItemStack, current);
        XUiC_ItemStack source;
        if (cellSelected) source = selectedCell;
        else
        {
            // Installed part (or an item currently on the drag cursor): a display-only proxy holds a copy.
            proxy.Show(current, geometryCell);
            source = proxy;
        }
        PlacePane();
        info.SetItemStack(source, true);
        if (info.ViewComponent != null) info.ViewComponent.IsVisible = true;
        paneVisible = true;
        ApplyLegacyVisibility();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        // Cheap per-frame guard: the editor backpack re-shows its icon on every selection or preview.
        if (paneVisible)
        {
            if (Shared) SetShown(legacyIcon, false);
            else { paneVisible = false; ApplyLegacyVisibility(); }
        }
        if (Time.realtimeSinceStartup < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartup + 0.2f;
        if (backpack == null) backpack = GetParentByType<XUiC_RebirthEditorBackpack>();
        ItemStack current = backpack?.InspectionStack;
        bool has = current != null && !current.IsEmpty() && current.itemValue?.ItemClass != null;
        // Cheap field comparison (no serialisation) so an idle window allocates nothing.
        if (!has)
        {
            if (!hasShown) return;
            hasShown = false;
            if (paneVisible)
            {
                HidePane();
                ApplyLegacyVisibility();
                // The editor backpack's empty-slot hint (expected part icon) was suppressed under the pane.
                if ((legacyDescription?.ViewComponent as XUiV_Label)?.Text == Localization.Get("xuiRebirthVehicleEmptyPartHint")) SetShown(legacyIcon, true);
            }
            Paint(null);
            return;
        }
        ItemValue value = current.itemValue;
        bool shared = Shared;
        // Same comparison source as the other selected-item panes: the stack under the pointer (bag cell or
        // installed part), compared against the inspected one by the native CanCompare rules in Build.
        // The shared pane does its own comparison, so the hover only matters for the fallback rows.
        ItemStack hovered = shared ? null : RebirthCharacterItemStatsTooltip.Stack(RebirthCharacterItemStatsTooltip.Hovered);
        bool compare = hovered != null && !hovered.IsEmpty() && hovered.itemValue?.ItemClass != null
            && !RebirthStationGridIngredients.IsSameStackSnapshot(current, hovered);
        int hoverType = compare ? hovered.itemValue.type * 31 + hovered.itemValue.Quality : -1;
        if (hasShown && shownType == value.type && shownCount == current.count && shownQuality == value.Quality
            && shownMods == value.ModificationCount && shownHover == hoverType && Mathf.Approximately(shownUse, value.UseTimes)
            && paneVisible == shared) return;
        hasShown = true;
        shownType = value.type; shownCount = current.count; shownQuality = value.Quality;
        shownMods = value.ModificationCount; shownUse = value.UseTimes; shownHover = hoverType;
        if (shared)
        {
            PushPane(current);
            return;
        }
        Paint(current, compare ? hovered : null);
    }

    private void Paint(ItemStack stack, ItemStack comparison = null)
    {
        for (int i = 0; i < Rows; i++) { names[i] = ""; values[i] = ""; }
        if (stack != null) RebirthSelectedItemRows.Build(xui, stack, comparison, comparison, Rows, names, values);
        for (int i = 0; i < Rows; i++) { Set(statNames[i], names[i]); Set(statValues[i], values[i]); }
        PaintMods(stack);
    }

    // Native modifier/cosmetic presentation: one cell per slot of the inspected item, the installed mod's icon
    // or a greyscale placeholder for an empty slot. Display only; installing/removing stays on the item's own flow.
    private void PaintMods(ItemStack stack)
    {
        ItemValue value = stack != null && !stack.IsEmpty() ? stack.itemValue : null;
        ItemValue[] mods = value?.modifications;
        ItemValue[] cosmetics = value?.cosmeticMods;
        for (int i = 0; i < ModCells; i++)
        {
            XUiV_Sprite cell = modIcons[i];
            if (cell == null) continue;
            ItemValue[] source = i < ModSlots ? mods : cosmetics;
            int index = i < ModSlots ? i : i - ModSlots;
            if (source == null || index >= source.Length) { cell.IsVisible = false; continue; }
            ItemValue mod = source[index];
            bool installed = mod != null && !mod.IsEmpty() && mod.ItemClass != null;
            cell.IsVisible = installed;
            if (!installed) continue;
            cell.UIAtlas = "ItemIconAtlas";
            cell.SetSpriteImmediately(mod.GetPropertyOverride("CustomIcon", mod.ItemClass.GetIconName()));
            cell.SetColorImmediately(mod.ItemClass.GetIconTint(mod));
        }
    }

    private static void Set(XUiV_Label label, string text)
    {
        if (label != null && !string.Equals(label.Text, text, System.StringComparison.Ordinal)) label.SetTextImmediately(text);
    }
}
