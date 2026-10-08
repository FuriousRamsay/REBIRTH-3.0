using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// A presentation lease on an already-open native interaction. Native loot/bag controllers
/// continue to own the item arrays, network lock, callbacks, and final close. The lease hides
/// BOTH native groups (container AND backpack/inspector), not just the left-hand window.
/// </summary>
public static class RebirthContextNavigationService
{
    public enum ContextKind { None = 0, Looting = 1, Storage = 2 }
    public const string OverlayGroupId = "rebirthContextNavigation";
    public static bool IntegrationReady { get; internal set; }
    internal static RebirthContainerSession Session { get; private set; }
    private static bool returning, clearing;
    private static int suspendedFrame;
    private static float nextMaintain;
    private static readonly RebirthWindowHudScope Hud = new RebirthWindowHudScope();
    private static readonly string[] Destinations = {
        "map", "character", "skills", "quests", "challenges", "players", "journal",
        "crafting", "rebirthJournal", XUiC_RebirthSurvivorCharacter.WindowGroupId, "windowpaging"
    };

    public static bool IsActiveFor(XUi xui) => Session != null && Session.Ui == xui;
    public static bool IsSuspendedFor(XUi xui) => IsActiveFor(xui) && Session.Suspended;
    public static bool IsContextVisible(XUi xui) => IsActiveFor(xui) && !Session.Suspended;
    public static string PrimaryLabel => Session?.Kind == ContextKind.Storage ? "STORAGE" : "LOOT";
    public static string PrimaryIcon => Session?.Kind == ContextKind.Storage ? "ui_game_symbol_backpack" : "ui_game_symbol_loot_sack";
    public static XUiController SelectedItem => Session?.Workspace?.SelectedItem;

    public static void Begin(XUiController controller, ContextKind kind)
    {
        if (!IntegrationReady || controller?.xui?.playerUI == null ||
            RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth) return;
        XUiController root = (controller.windowGroup as XUiWindowGroup)?.Controller ?? controller;
        if (Session?.Owner == root) return;
        if (Session != null) CloseInteraction();
        // Resolve every required component BEFORE taking over. Unsupported capacity or a missing
        // XML/controller pair keeps the complete native UI, never a truncated/inert inventory.
        RebirthContainerSession candidate;
        string reason;
        if (!RebirthContainerSession.TryCreate(root, kind, out candidate, out reason))
        {
            Log.Warning("[REBIRTH Container UI] native presentation retained: " + reason);
            return;
        }
        Session = candidate;
        try
        {
            candidate.HideNativePresentation();
            candidate.Ui.playerUI.windowManager.Open(OverlayGroupId, false);
            // The backpack may already have pushed input before this lease was created.
            // Reset through the native manager after removing its stale ownership flag;
            // the HasActionSet patch prevents re-pushing that concealed group.
            candidate.BackpackGroup.bActionSetEnabled = false;
            candidate.Ui.playerUI.windowManager.ResetActionSets();
            RebirthPersonalCraftingHudSuppressionInstaller.EnsureInstalled();
            Hud.Maintain(candidate.Ui);
            nextMaintain = 0f;
            SetOverlayDestination(RebirthCraftingNavigationService.Destination.Crafting);
        }
        catch (Exception ex)
        {
            Clear();
            Log.Error("[REBIRTH Container UI] presentation failed; native UI restored: " + ex);
        }
    }

    public static void BeforeClose(XUiController controller)
    {
        if (Session?.Owner == controller) Session.FlushLocks();
    }
    public static void End(XUiController controller)
    {
        if (Session?.Owner == controller) Clear();
    }
    public static void CloseInteraction()
    {
        RebirthContainerSession s = Session;
        if (s == null) return;
        s.FlushLocks();
        s.Ui?.playerUI?.windowManager?.Close(s.Window);
        if (Session == s) Clear();
    }
    private static void Clear()
    {
        if (clearing) return;
        clearing = true;
        RebirthContainerSession s = Session;
        Session = null; // OnClose of the overlay must not recursively close the native owner.
        returning = false;
        try
        {
            s?.RestoreNativePresentation();
            GUIWindowManager manager = s?.Ui?.playerUI?.windowManager;
            if (manager != null && manager.IsWindowOpen(OverlayGroupId)) manager.Close(OverlayGroupId);
        }
        finally { Hud.Restore(); clearing = false; }
    }

    public static bool SuspendForExternalRoute(XUi xui)
    {
        RebirthContainerSession s = Session;
        if (!IsActiveFor(xui) || returning) return false;
        if (s.Suspended) return true;
        // Match a normal window transition: resolve a held cursor stack before retiring its
        // presentation. Native AddItem/DropItem decides where it goes; no proxy state is lost.
        xui.DragAndDropWindow?.PlaceItemBackInInventory();
        s.FlushLocks();
        s.Suspended = true;
        s.Workspace.SetPresented(false); // Hide the ENTIRE separate body window and its colliders.
        s.Window.isModal = false;
        s.Window.bActionSetEnabled = false;
        s.BackpackGroup.bActionSetEnabled = false;
        xui.playerUI.windowManager.ResetActionSets();
        suspendedFrame = Time.frameCount;
        return true;
    }

    public static bool ReturnToContext(XUi xui)
    {
        RebirthContainerSession s = Session;
        if (!IsActiveFor(xui)) return false;
        if (returning) return true;
        if (!s.Suspended) { SetOverlayDestination(RebirthCraftingNavigationService.Destination.Crafting); return true; }
        returning = true;
        try
        {
            GUIWindowManager manager = xui.playerUI.windowManager;
            for (int i = 0; i < Destinations.Length; ++i)
                if (manager.IsWindowOpen(Destinations[i])) manager.Close(Destinations[i]);
            // Some external pages close the shared native backpack group. Re-open it through its
            // normal lifecycle so item-info/actions and its inventory listeners are rebound.
            if (!manager.IsWindowOpen(s.BackpackGroup)) manager.Open(s.BackpackGroup, false);
            s.HideNativePresentation();
            s.Suspended = false;
            s.Window.isModal = true;
            s.Workspace.SetPresented(true);
            manager.ResetActionSets();
            SetOverlayDestination(RebirthCraftingNavigationService.Destination.Crafting);
            return true;
        }
        finally { returning = false; }
    }

    public static void SetOverlayDestination(RebirthCraftingNavigationService.Destination destination)
    {
        Session?.Overlay.Controller.GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(destination);
    }
    public static void AutoReturnIfExternalClosed(XUi xui)
    {
        if (!IsSuspendedFor(xui) || returning || Time.frameCount <= suspendedFrame + 2) return;
        GUIWindowManager manager = xui.playerUI.windowManager;
        for (int i = 0; i < Destinations.Length; ++i)
            if (manager.IsWindowOpen(Destinations[i])) return;
        ReturnToContext(xui);
    }
    internal static void Maintain(XUi ui)
    {
        RebirthContainerSession s = Session;
        if (s == null || s.Ui != ui) return;
        if (GameManager.Instance?.World == null) { Clear(); return; }
        // The bag group retains its native CheckUIInteraction callback. The native LootWindow's
        // UI Update is suppressed, so retain its exact distance rule here, including while parked.
        if (s.Loot != null && ui.LootContainer != null && ui.playerUI.entityPlayer != null)
        {
            Vector3 center = ui.LootContainer.ToWorldCenterPos();
            float limit = Constants.cCollectItemDistance + 30f;
            if (center != Vector3.zero && (ui.playerUI.entityPlayer.position - center).sqrMagnitude > limit * limit)
            { CloseInteraction(); return; }
        }
        if (Time.realtimeSinceStartup >= nextMaintain)
        {
            nextMaintain = Time.realtimeSinceStartup + .25f;
            s.HideNativePresentation();
            Hud.Maintain(ui);
        }
        AutoReturnIfExternalClosed(ui);
    }
    internal static bool SuppressNativeUpdate(XUiController controller)
    {
        RebirthContainerSession s = Session;
        return s != null && (controller == s.LootWindow || controller == s.BagContainer ||
            controller == s.BackpackWindow || controller == s.InfoWindow);
    }
}

/// <summary>Verified 3.2 b10 native surfaces; no guessed vehicle window type or id.</summary>
internal sealed class RebirthContainerSession
{
    internal XUi Ui;
    internal XUiController Owner;
    internal XUiWindowGroup Window, BackpackGroup, Overlay;
    internal XUiC_LootWindow LootWindow;
    internal XUiC_LootContainer Loot;
    internal XUiC_BagContainer BagContainer;
    internal XUiC_BackpackWindow BackpackWindow;
    internal XUiC_Backpack Backpack;
    internal XUiC_ItemInfoWindow InfoWindow;
    internal XUiC_ContainerStandardControls LeftControls, RightControls;
    internal XUiC_RebirthContainerWorkspace Workspace;
    internal RebirthContextNavigationService.ContextKind Kind;
    internal bool Suspended;
    private readonly List<HiddenWindow> hidden = new List<HiddenWindow>();
    private sealed class HiddenWindow
    {
        internal XUiView View;
        internal XUiWindowGroup Group;
        internal bool Visible, Enabled, Active;
    }

    internal static bool TryCreate(XUiController owner, RebirthContextNavigationService.ContextKind kind,
        out RebirthContainerSession result, out string reason)
    {
        result = null;
        reason = "missing native container, backpack, or workspace component";
        GUIWindow backpack, overlay;
        GUIWindowManager manager = owner.xui.playerUI.windowManager;
        if (manager == null || !manager.IsWindowOpen(owner.windowGroup) ||
            !manager.TryGetWindow("backpack", out backpack) || !manager.TryGetWindow(RebirthContextNavigationService.OverlayGroupId, out overlay)) return false;
        var s = new RebirthContainerSession { Ui = owner.xui, Owner = owner,
            Window = owner.windowGroup as XUiWindowGroup, BackpackGroup = backpack as XUiWindowGroup,
            Overlay = overlay as XUiWindowGroup, Kind = kind };
        if (s.Window == null || s.BackpackGroup?.Controller == null || s.Overlay?.Controller == null) return false;
        s.LootWindow = owner.GetChildByType<XUiC_LootWindow>();
        s.Loot = owner.GetChildByType<XUiC_LootContainer>();
        s.BagContainer = owner.GetChildByType<XUiC_BagContainer>();
        s.BackpackWindow = s.BackpackGroup.Controller.GetChildByType<XUiC_BackpackWindow>();
        s.Backpack = s.BackpackGroup.Controller.GetChildByType<XUiC_Backpack>();
        s.InfoWindow = s.BackpackGroup.Controller.GetChildByType<XUiC_ItemInfoWindow>();
        s.Workspace = s.Overlay.Controller.GetChildByType<XUiC_RebirthContainerWorkspace>();
        s.LeftControls = (s.LootWindow as XUiController ?? s.BagContainer)?.GetChildByType<XUiC_ContainerStandardControls>();
        s.RightControls = s.BackpackWindow?.GetChildByType<XUiC_ContainerStandardControls>();
        if (s.Backpack == null || s.InfoWindow == null || s.Workspace == null || !s.Workspace.CanBind ||
            s.LeftControls == null || s.RightControls == null || (s.Loot == null && s.BagContainer?.Bag == null)) return false;
        ItemStack[] left = s.GetSlots(false), right = s.GetSlots(true);
        if (left == null || right == null || left.Length > XUiC_RebirthContextGrid.ContainerPool ||
            right.Length > XUiC_RebirthContextGrid.BackpackPool ||
            left.Length > s.NativeGrid(false).GetItemStackControllers().Length ||
            right.Length > s.NativeGrid(true).GetItemStackControllers().Length)
        { reason = "physical inventory exceeds a presentation/native slot pool; capacity left untouched"; return false; }
        s.Capture(s.Window);
        s.Capture(s.BackpackGroup);
        result = s;
        return true;
    }
    internal XUiC_ItemStackGrid NativeGrid(bool backpack) => backpack ? (XUiC_ItemStackGrid)Backpack : Loot as XUiC_ItemStackGrid ?? BagContainer;
    internal XUiController NativeHeader(bool backpack) => backpack ? (XUiController)BackpackWindow : LootWindow as XUiController ?? BagContainer;
    internal XUiC_ContainerStandardControls Controls(bool backpack) => backpack ? RightControls : LeftControls;
    internal ItemStack[] GetSlots(bool backpack) => backpack ? Ui.PlayerInventory?.Backpack?.ItemGrid.items : Loot != null ? Loot.GetSlots() : BagContainer?.Bag?.ItemGrid.items;
    internal XUiC_ItemStack NativeSlot(bool backpack, int index)
    {
        var slots = NativeGrid(backpack)?.GetItemStackControllers();
        return slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
    }
    internal bool LockMode(bool backpack)
    {
        string value = "false";
        NativeHeader(backpack)?.GetBindingValue(ref value, "userlockmode");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
    internal string Binding(bool backpack, string name, string fallback)
    {
        string value = fallback;
        NativeHeader(backpack)?.GetBindingValue(ref value, name);
        return value;
    }
    internal void WriteSlot(bool backpack, int index, ItemStack stack)
    {
        ItemStack[] slots = GetSlots(backpack);
        if (slots == null || index < 0 || index >= slots.Length || Suspended) return;
        ItemStack copy = stack?.Clone() ?? ItemStack.Empty.Clone();
        if (backpack) Ui.PlayerInventory.Backpack.SetSlot(index, copy);
        else if (Loot != null) Loot.HandleLootSlotChangedEvent(index, copy);
        else BagContainer.HandleBagSlotChangedEvent(index, copy);
    }
    internal void SetLock(bool backpack, int index, bool locked)
    {
        XUiC_ItemStack source = NativeSlot(backpack, index);
        if (source == null || !LockMode(backpack)) return;
        source.UserLockedSlot = locked;
        var controls = Controls(backpack);
        controls.UpdateLockedSlotStates?.Invoke(controls);
    }
    internal void FlushLocks()
    {
        // Mirrors write the native physical index immediately. Do NOT overwrite a whole lock
        // array from a visible page, and do NOT manufacture a 198/110-slot backing inventory.
        if (GetSlots(false) != null) LeftControls?.UpdateLockedSlotStates?.Invoke(LeftControls);
        if (GetSlots(true) != null) RightControls?.UpdateLockedSlotStates?.Invoke(RightControls);
    }
    private void Capture(XUiWindowGroup group)
    {
        foreach (XUiV_Window window in group.Windows)
            hidden.Add(new HiddenWindow { View = window, Group = group, Visible = window.IsVisible,
                Enabled = window.Enabled, Active = window.UiTransform == null || window.UiTransform.gameObject.activeSelf });
    }
    internal void HideNativePresentation()
    {
        foreach (HiddenWindow h in hidden)
        {
            if (h.View == null) continue;
            if (h.View.IsVisible) h.View.IsVisible = false;
            if (h.View.Enabled) h.View.Enabled = false;
            if (h.View.UiTransform != null && h.View.UiTransform.gameObject.activeSelf)
                h.View.UiTransform.gameObject.SetActive(false);
        }
    }
    internal void RestoreNativePresentation()
    {
        GUIWindowManager manager = Ui?.playerUI?.windowManager;
        foreach (HiddenWindow h in hidden)
        {
            if (h.View == null) continue;
            // A normal native close has already hidden this view and released cursor bounds.
            // Restore the pre-lease state only for a group which is still logically open
            // (the exception/fallback path), never re-register a closed hidden window.
            if (manager != null) h.View.IsVisible = h.Visible && manager.IsWindowOpen(h.Group);
            h.View.Enabled = h.Enabled;
            if (h.View.UiTransform != null) h.View.UiTransform.gameObject.SetActive(h.Active);
        }
        hidden.Clear();
    }
}

[Preserve]
public sealed class XUiC_RebirthContextNavigationChrome : XUiController
{
    private Vector2i lastPosition = new Vector2i(int.MinValue, int.MinValue);
    private int lastWidth = -1;
    public override void OnOpen()
    {
        base.OnOpen(); lastWidth = -1; ApplyLayout();
        GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(RebirthCraftingNavigationService.Destination.Crafting);
    }
    public override void Update(float dt)
    {
        if (!RebirthContextNavigationService.IsActiveFor(xui)) return;
        RebirthContextNavigationService.Maintain(xui);
        if (!RebirthContextNavigationService.IsActiveFor(xui)) return;
        ApplyLayout(); base.Update(dt);
    }
    private void ApplyLayout()
    {
        if (xui == null || ViewComponent == null) return;
        Vector2i position, size;
        RebirthScreenLayout.GetScreenBounds(xui, out position, out size);
        if (lastWidth == size.x && lastPosition.x == position.x && lastPosition.y == position.y) return;
        lastPosition = position; lastWidth = size.x;
        ViewComponent.Position = position;
        ViewComponent.Size = new Vector2i(size.x, 60);
        int width = Math.Max(1, size.x - 16);
        Set("rebirthCraftingTopZone", new Vector2i(8, -8), new Vector2i(width, 52));
        Set("rebirthCraftingTopBackground", Vector2i.zero, new Vector2i(width, 52));
        Set("rebirthCraftingTopFrame", Vector2i.zero, new Vector2i(width, 52));
        RebirthPersonalCraftingLayoutService.ApplySharedTopLayout(this, width, false);
        ViewComponent.TryUpdatePosition();
    }
    private void Set(string id, Vector2i position, Vector2i size)
    {
        XUiView view = GetChildById(id)?.ViewComponent;
        if (view == null) return;
        view.Position = position; view.Size = size;
    }
}

// The loot lifecycle is openContainer, NOT OnOpen, in the supplied 3.2 b10 assembly.
[HarmonyPatch(typeof(XUiC_LootWindowGroup), "openContainer")]
public static class RebirthContextLootOpenPatch
{
    public static void Postfix(XUiC_LootWindowGroup __instance) => RebirthContextNavigationService.Begin(__instance, RebirthContextNavigationService.ContextKind.Looting);
}
[HarmonyPatch(typeof(XUiC_LootWindowGroup), nameof(XUiC_LootWindowGroup.OnClose))]
public static class RebirthContextLootClosePatch
{
    public static void Prefix(XUiC_LootWindowGroup __instance) => RebirthContextNavigationService.BeforeClose(__instance);
    public static void Postfix(XUiC_LootWindowGroup __instance) => RebirthContextNavigationService.End(__instance);
}
// EntityVehicle.StartInteraction("storage") also calls this native BagStorage path in b10.
[HarmonyPatch(typeof(XUiC_BagStorageWindowGroup), nameof(XUiC_BagStorageWindowGroup.OnOpen))]
public static class RebirthContextBagStorageOpenPatch
{
    public static void Postfix(XUiC_BagStorageWindowGroup __instance) => RebirthContextNavigationService.Begin(__instance, RebirthContextNavigationService.ContextKind.Storage);
}
[HarmonyPatch(typeof(XUiC_BagStorageWindowGroup), nameof(XUiC_BagStorageWindowGroup.OnClose))]
public static class RebirthContextBagStorageClosePatch
{
    public static void Prefix(XUiC_BagStorageWindowGroup __instance) => RebirthContextNavigationService.BeforeClose(__instance);
    public static void Postfix(XUiC_BagStorageWindowGroup __instance) => RebirthContextNavigationService.End(__instance);
}
[HarmonyPatch]
public static class RebirthContextNativePresentationUpdatePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type type in new[] { typeof(XUiC_LootWindow), typeof(XUiC_BagContainer), typeof(XUiC_BackpackWindow), typeof(XUiC_ItemInfoWindow) })
        {
            MethodInfo method = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                null, new[] { typeof(float) }, null);
            if (method == null) throw new MissingMethodException(type.FullName, "Update(float)");
            yield return method;
        }
    }
    public static bool Prefix(XUiController __instance) => !RebirthContextNavigationService.SuppressNativeUpdate(__instance);
}
[HarmonyPatch(typeof(XUiWindowGroup), nameof(XUiWindowGroup.HasActionSet))]
public static class RebirthContextParkedActionSetPatch
{
    public static void Postfix(XUiWindowGroup __instance, ref bool __result)
    {
        var s = RebirthContextNavigationService.Session;
        if (s != null && ((__instance == s.Window && s.Suspended) || __instance == s.BackpackGroup)) __result = false;
    }
}
[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
public static class RebirthContextHiddenTransferPatch
{
    public static bool Prefix(XUiC_ItemStack __instance)
    {
        // Native preferred-location routing consults an open BagStorage even when its panel is
        // invisible. Never let Shift-click on an external page secretly transfer into that bag.
        if (!RebirthContextNavigationService.IsSuspendedFor(__instance.xui)) return true;
        return __instance.StackLocation != XUiC_ItemStack.StackLocationTypes.Backpack &&
               __instance.StackLocation != XUiC_ItemStack.StackLocationTypes.ToolBelt;
    }
}
