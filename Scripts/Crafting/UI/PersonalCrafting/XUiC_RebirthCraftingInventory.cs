using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Rebirth Personal Crafting backpack presenter. This is intentionally an XUiC_Backpack subclass
/// so all native ItemStack transaction behavior is retained, but backend writes are narrowed to
/// one authoritative Bag slot so the authored 104-cell presentation can never resize the Bag.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingInventory : XUiC_Backpack
{
    private int physicalSlotCount;
    private XUiC_RebirthPersonalCrafting personalOwner;
    private int lastUnencumberedSlotCount = -1;
    private bool userLockMode;
    private bool traceProjectionRequested;
    private int lastTraceControllerCount = -1;
    private int lastTracePhysical = -1;
    private int[] projectedFingerprints = Array.Empty<int>();
    private bool nativeProjectionInitialized;
    private int lastProjectionChangedCount;
    private const float ProjectionCoalesceSeconds = 0f;
    private ItemStack[] pendingProjectionStacks;
    private float pendingProjectionDue;
    private int pendingProjectionRequests;
    private bool pendingProjectionTrace;
    private long projectionRevision;

    public long ProjectionRevision => projectionRevision;
    public int PhysicalSlotCount => physicalSlotCount;
    public bool UserLockMode => userLockMode;

    public override bool GetBindingValueInternal(ref string value, string name)
    {
        if (name == "userlockmode")
        {
            value = userLockMode ? "true" : "false";
            return true;
        }
        return base.GetBindingValueInternal(ref value, name);
    }

    public override void Init()
    {
        base.Init();
        personalOwner = GetParentByType<XUiC_RebirthPersonalCrafting>();
        EnsureItemControllers();
    }

    public override void OnOpen()
    {
        RebirthInventoryTiming.Start();
        traceProjectionRequested = true;
        EnsureItemControllers();
        base.OnOpen();
        EnsureItemControllers();
        RefreshAuthoritativePresentation(true);
        if (itemControllers == null || itemControllers.Length == 0)
            Log.Error("[REBIRTH Crafting Inventory] No repeated ItemStack controllers were created for the Personal Crafting inventory grid.");
        else if (RebirthPersonalCraftingState.LayoutDebugEnabled || RebirthLogSettings.CraftingUiLoggingEnabled)
            TraceInventory("open", GetSlots(), true);
    }

    public override void OnClose()
    {
        PersistUserLockedSlots();
        SetUserLockMode(false);
        base.OnClose();
    }

    public override void Update(float dt)
    {
        long profile=windowGroup?.Controller is XUiC_RebirthCookingStation?RebirthCookingDiagnostics.Begin():0;
        base.Update(dt);
        if (personalOwner != null && !personalOwner.State.IsOpen) return;
        RebirthCookingDiagnostics.Section("backpack native update",profile);
        long projectionProfile=profile==0?0:RebirthCookingDiagnostics.Begin();

        int physical = RebirthCraftingInventoryBridge.GetPhysicalSlotCount(xui);
        int unencumbered = RebirthCraftingInventoryBridge.GetUnencumberedSlotCount(xui);
        if (physical != physicalSlotCount || unencumbered != lastUnencumberedSlotCount)
            RefreshAuthoritativePresentation(true);

        // V3.2 may raise the same Bag mutation two or three times in the same frame/burst while
        // crafting. Wait a few milliseconds and project only the newest snapshot once. This keeps
        // item controls, hover borders and the scrolled grid transform from visibly repainting.
        FlushPendingProjection(false);

        // The native slot-lock overlay toggles XUiC_ItemStack.UserLockedSlot directly.
        // Persist those changes while lock mode is active, just as the native Backpack window does.
        if (userLockMode)
            PersistUserLockedSlots();
        RebirthCookingDiagnostics.Section("backpack projection",projectionProfile);
        RebirthCookingDiagnostics.Section("backpack",profile);
    }

    public override void SetStacks(ItemStack[] stackList)
    {
        // Native Backpack.Update retries forever if its capacity cache is never advanced.
        // Incremental projections bypass base.SetStacks, so maintain that cache explicitly.
        maxSlotCount = xui.playerUI.entityPlayer.CarryCapacity;
        EnsureItemControllers();
        // The first native projection initializes Backpack internals. Subsequent Bag notifications
        // are projected incrementally by Rebirth so crafting ingredient ticks do not make all 100
        // visible ItemStack controls flash/rebind at once.
        if (!nativeProjectionInitialized)
        {
            if (itemControllers != null)
            {
                var initialInfoWindow = RebirthInventoryInfoWindowLookup.Find(xui);
                for (int i = 0; i < itemControllers.Length; i++)
                    if (itemControllers[i] != null) itemControllers[i].InfoWindow = initialInfoWindow;
            }
            base.SetStacks(stackList);
            nativeProjectionInitialized = true;
            ApplyAuthoritativePresentation(stackList, true);
        }
        else
        {
            QueueProjection(stackList);
            return;
        }

        if (traceProjectionRequested || RebirthLogSettings.CraftingUiLoggingEnabled)
        {
            TraceInventory("SetStacks initial changed=" + lastProjectionChangedCount, stackList, traceProjectionRequested);
            traceProjectionRequested = false;
        }
    }


    private void QueueProjection(ItemStack[] stackList)
    {
        bool firstRequest = pendingProjectionStacks == null;
        pendingProjectionStacks = CloneStacks(stackList);
        if (firstRequest)
            pendingProjectionDue = Time.realtimeSinceStartup + ProjectionCoalesceSeconds;
        pendingProjectionRequests++;
        pendingProjectionTrace = pendingProjectionTrace || traceProjectionRequested || RebirthLogSettings.CraftingUiLoggingEnabled;
        traceProjectionRequested = false;
    }

    private void FlushPendingProjection(bool force)
    {
        if (pendingProjectionStacks == null)
            return;
        if (!force && Time.realtimeSinceStartup < pendingProjectionDue)
            return;

        ItemStack[] latest = pendingProjectionStacks;
        int requests = Math.Max(1, pendingProjectionRequests);
        bool trace = pendingProjectionTrace;
        pendingProjectionStacks = null;
        pendingProjectionRequests = 0;
        pendingProjectionTrace = false;

        ApplyAuthoritativePresentation(latest, false);
        if (trace)
            TraceInventory("SetStacks coalescedRequests=" + requests + " changed=" + lastProjectionChangedCount, latest, true);
    }

    private static ItemStack[] CloneStacks(ItemStack[] source)
    {
        if (source == null)
            return Array.Empty<ItemStack>();
        ItemStack[] copy = new ItemStack[source.Length];
        for (int i = 0; i < source.Length; i++)
            copy[i] = source[i] != null ? source[i].Clone() : ItemStack.Empty.Clone();
        return copy;
    }

    public override void HandleSlotChangedEvent(int slotNumber, ItemStack stack)
    {
        using var inventoryTiming = RebirthInventoryTiming.Measure(4);
        Bag bag = RebirthCraftingInventoryBridge.GetBag(xui);
        int physical = RebirthCraftingInventoryBridge.GetPhysicalSlotCount(xui);
        if (bag == null || slotNumber < 0 || slotNumber >= physical)
            return;

        // CRITICAL: never call base.HandleSlotChangedEvent here. The base grid serializes every
        // authored UI controller and would therefore turn the 104-cell presenter into real storage.
        // Updating the one real Bag index preserves native drag/drop semantics without changing size.
        bag.SetSlot(slotNumber, stack != null ? stack.Clone() : ItemStack.Empty.Clone());
    }

    public void RefreshAuthoritativePresentation(bool refreshStacks)
    {
        ItemStack[] slots = GetSlots();
        if (refreshStacks)
            SetStacks(slots);
        else
            ApplyAuthoritativePresentation(slots, false);
    }

    public void SetUserLockMode(bool enabled)
    {
        if (userLockMode == enabled)
            return;

        if (userLockMode)
            PersistUserLockedSlots();
        userLockMode = enabled;
        ApplyLockOverlayVisibility();
    }

    public void ToggleUserLockMode()
    {
        SetUserLockMode(!userLockMode);
    }

    public void SortAuthoritativeBackpack()
    {
        if (xui == null || xui.PlayerInventory == null)
            return;
        PersistUserLockedSlots();
        Bag bag = RebirthCraftingInventoryBridge.GetBag(xui);
        xui.PlayerInventory.SortStacks();
        RefreshAuthoritativePresentation(true);
    }

    public void PersistUserLockedSlots()
    {
        RebirthCraftingInventoryBridge.PersistLockedSlots(xui, itemControllers, physicalSlotCount);
    }

    private void ApplyAuthoritativePresentation(ItemStack[] stackList, bool forceRefresh)
    {
        using var inventoryTiming = RebirthInventoryTiming.Measure(3);
        EnsureItemControllers();
        if (itemControllers == null || itemControllers.Length == 0)
            return;

        int physical = stackList != null
            ? Math.Min(stackList.Length, RebirthSurvivorGearService.MaxPhysicalBagSlots)
            : 0;
        physical = Math.Min(physical, itemControllers.Length);
        int previousPhysical = physicalSlotCount;
        physicalSlotCount = physical;

        int unencumbered = RebirthCraftingInventoryBridge.GetUnencumberedSlotCount(xui);
        int previousUnencumbered = lastUnencumberedSlotCount;
        lastUnencumberedSlotCount = unencumbered;
        bool geometryStateChanged = previousPhysical != physical || previousUnencumbered != unencumbered;

        EnsureProjectionFingerprintCapacity(itemControllers.Length);
        int changed = 0;
        var infoWindow = RebirthInventoryInfoWindowLookup.Find(xui);
        for (int i = 0; i < itemControllers.Length; i++)
        {
            XUiC_ItemStack slot = itemControllers[i];
            if (slot == null)
                continue;

            bool authoritative = i < physical;
            bool wasVisible = slot.ViewComponent != null && slot.ViewComponent.IsVisible;
            if (slot.ViewComponent != null)
            {
                slot.ViewComponent.IsVisible = authoritative;
                slot.ViewComponent.Enabled = authoritative;
                if (slot.ViewComponent.UiTransform != null)
                {
                    slot.ViewComponent.UiTransform.gameObject.SetActive(authoritative);
                    if (!authoritative)
                        slot.ViewComponent.UiTransform.localScale = UnityEngine.Vector3.zero;
                }
            }

            slot.SlotNumber = i;
            slot.StackLocation = StackLocation;
            slot.InfoWindow = infoWindow;
            bool attributeLock = authoritative && i >= unencumbered;
            bool lockChanged = geometryStateChanged;
            if (slot.AttributeLock != attributeLock)
                slot.AttributeLock = attributeLock;

            ItemStack desired = authoritative && stackList != null && i < stackList.Length && stackList[i] != null ? stackList[i] : ItemStack.Empty;
            int fingerprint = authoritative ? BuildFingerprint(desired) : 0;
            bool presentationChanged = forceRefresh || geometryStateChanged || wasVisible != authoritative ||
                                       lockChanged || projectedFingerprints[i] != fingerprint;

            if (presentationChanged)
            {
                slot.SlotChangedEvent -= handleSlotChangedDelegate;
                slot.ItemStack = desired.Clone();
                if (authoritative)
                    slot.SlotChangedEvent += handleSlotChangedDelegate;
                slot.RefreshBindings();
                XUiC_RebirthCraftingInventorySlot rebirthSlot = slot as XUiC_RebirthCraftingInventorySlot;
                if (rebirthSlot != null)
                    rebirthSlot.ReapplyRebirthSlotPalette();
                projectedFingerprints[i] = fingerprint;
                changed++;
            }
            else if (authoritative)
            {
                // Keep the backend hook healthy without repainting an unchanged visual cell.
                slot.SlotChangedEvent -= handleSlotChangedDelegate;
                slot.SlotChangedEvent += handleSlotChangedDelegate;
            }
            else
            {
                slot.SlotChangedEvent -= handleSlotChangedDelegate;
            }
        }
        lastProjectionChangedCount = changed;
        if (changed > 0) unchecked { projectionRevision++; }

        RebirthCraftingInventoryBridge.ApplyLockedSlots(xui, itemControllers, physical);
        ApplyLockOverlayVisibility();

        XUiV_Grid grid = ViewComponent as XUiV_Grid;
        if (grid != null)
        {
            grid.Columns = windowGroup?.Controller is XUiC_RebirthCookingStation ? 26 : windowGroup?.Controller is XUiC_RebirthQuestTurnInWorkspace ? 12 : RebirthCraftingInventoryBridge.Columns;
            grid.Rows = (RebirthCraftingInventoryBridge.AuthoredSlotCount + grid.Columns - 1) / grid.Columns;
        }
    }


    private void EnsureProjectionFingerprintCapacity(int count)
    {
        if (projectedFingerprints != null && projectedFingerprints.Length == count)
            return;
        projectedFingerprints = new int[Math.Max(0, count)];
        for (int i = 0; i < projectedFingerprints.Length; i++)
            projectedFingerprints[i] = int.MinValue;
    }

    private static int BuildFingerprint(ItemStack stack)
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
            if (value.Metadata != null && value.Metadata.Count > 0)
            {
                var keys = new List<string>(value.Metadata.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int k = 0; k < keys.Count; k++)
                {
                    string key = keys[k] ?? string.Empty;
                    object metadata = value.Metadata[key];
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(key);
                    hash = hash * 31 + (metadata != null ? metadata.GetHashCode() : 0);
                }
            }
            ItemValue[] mods = value.modifications;
            hash = hash * 31 + (mods != null ? mods.Length : 0);
            for (int i = 0; mods != null && i < mods.Length; i++)
                hash = hash * 31 + (mods[i] != null ? mods[i].type : 0);
            ItemValue[] cosmetics = value.cosmeticMods;
            hash = hash * 31 + (cosmetics != null ? cosmetics.Length : 0);
            for (int i = 0; cosmetics != null && i < cosmetics.Length; i++)
                hash = hash * 31 + (cosmetics[i] != null ? cosmetics[i].type : 0);
            return hash;
        }
    }

    /// <summary>
    /// V3.2 normally populates XUiC_Backpack.itemControllers from repeat_content during Init.
    /// The custom grid uses a derived ItemStack controller, so retain a safe recovery path if a
    /// future parser/controller ordering leaves the inherited cache empty. Backend slot count is
    /// still bounded separately; discovering 104 presentation cells never creates Bag storage.
    /// </summary>
    private void EnsureItemControllers()
    {
        if (itemControllers != null && itemControllers.Length > 0)
            return;

        XUiC_ItemStack[] discovered = GetChildrenByType<XUiC_ItemStack>();
        if (discovered == null || discovered.Length == 0)
            return;

        itemControllers = discovered;
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH Crafting InventoryTrace] EnsureItemControllers recovered=" + itemControllers.Length
                + " viewType=" + (ViewComponent != null ? ViewComponent.GetType().Name : "<null>"));
        for (int i = 0; i < itemControllers.Length; i++)
        {
            XUiC_ItemStack slot = itemControllers[i];
            if (slot == null)
                continue;
            slot.SlotNumber = i;
            slot.StackLocation = StackLocation;
            slot.InfoWindow = RebirthInventoryInfoWindowLookup.Find(xui);
            // If base Init missed these repeated children, its slot-change hookup was missed too.
            slot.SlotChangedEvent -= handleSlotChangedDelegate;
            slot.SlotChangedEvent += handleSlotChangedDelegate;
        }
    }

    private void TraceInventory(string reason, ItemStack[] stackList, bool force)
    {
        if (!RebirthLogSettings.CraftingUiLoggingEnabled)
            return;

        int controllerCount = itemControllers != null ? itemControllers.Length : 0;
        int physical = RebirthCraftingInventoryBridge.GetPhysicalSlotCount(xui);
        bool stackProjection = !string.IsNullOrEmpty(reason) && reason.StartsWith("SetStacks", StringComparison.Ordinal);
        if (!force && controllerCount == lastTraceControllerCount && physical == lastTracePhysical &&
            (!stackProjection || lastProjectionChangedCount <= 0))
            return;

        lastTraceControllerCount = controllerCount;
        lastTracePhysical = physical;

        int sourceLength = stackList != null ? stackList.Length : -1;
        int sourceNonEmpty = 0;
        if (stackList != null)
        {
            for (int i = 0; i < stackList.Length; i++)
                if (stackList[i] != null && !stackList[i].IsEmpty()) sourceNonEmpty++;
        }

        int controllerNonEmpty = 0;
        int controllerVisible = 0;
        if (itemControllers != null)
        {
            for (int i = 0; i < itemControllers.Length; i++)
            {
                XUiC_ItemStack slot = itemControllers[i];
                if (slot == null) continue;
                if (!slot.ItemStack.IsEmpty()) controllerNonEmpty++;
                if (slot.ViewComponent != null && slot.ViewComponent.IsVisible) controllerVisible++;
            }
        }

        StringBuilder sample = new StringBuilder();
        int sampleCount = Math.Min(12, controllerCount);
        for (int i = 0; i < sampleCount; i++)
        {
            XUiC_ItemStack slot = itemControllers[i];
            if (i > 0) sample.Append(';');
            if (slot == null)
            {
                sample.Append(i).Append(":null");
                continue;
            }
            sample.Append(i)
                .Append(":vis=").Append(slot.ViewComponent != null && slot.ViewComponent.IsVisible)
                .Append(",empty=").Append(slot.ItemStack.IsEmpty())
                .Append(",count=").Append(slot.ItemStack.IsEmpty() ? 0 : slot.ItemStack.count)
                .Append(",slot=").Append(slot.SlotNumber);
        }

        XUiV_Grid grid = ViewComponent as XUiV_Grid;
        Log.Out("[REBIRTH Crafting InventoryTrace] reason=" + (reason ?? "unknown")
            + " bagPresent=" + (RebirthCraftingInventoryBridge.GetBag(xui) != null)
            + " sourceLen=" + sourceLength
            + " sourceNonEmpty=" + sourceNonEmpty
            + " physical=" + physical
            + " controllers=" + controllerCount
            + " controllerVisible=" + controllerVisible
            + " controllerNonEmpty=" + controllerNonEmpty
            + " grid=" + (grid != null)
            + " gridCols=" + (grid != null ? grid.Columns : -1)
            + " gridRows=" + (grid != null ? grid.Rows : -1)
            + " sample=[" + sample + "]");
    }

    private void ApplyLockOverlayVisibility()
    {
        if (itemControllers == null)
            return;

        for (int i = 0; i < itemControllers.Length; i++)
        {
            XUiC_ItemStack slot = itemControllers[i];
            if (slot == null)
                continue;
            XUiController overlay = slot.GetChildById("rectSlotLock");
            if (overlay != null && overlay.ViewComponent != null)
                overlay.ViewComponent.IsVisible = userLockMode && i < physicalSlotCount;
        }
    }
}

