using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using System.Reflection.Emit;

// Native inventory transactions remain untouched. Adapt their window ownership and return route.
public static class RebirthItemWindowLifecycle
{
    private sealed class ReturnState
    {
        public XUiController Owner, Item;
        public float Offset;
        public bool Inventory;
        public long Token;
    }
    private static readonly Dictionary<XUi, ReturnState> Returns = new Dictionary<XUi, ReturnState>();
    private static long nextReturnToken;

    // Fix31: omitted at every call site (including message construction) in normal builds.
    [System.Diagnostics.Conditional("REBIRTH_UI_DIAGNOSTICS")]
    internal static void Trace(string message)
    {
#if REBIRTH_UI_DIAGNOSTICS
        if (RebirthSurvivorDebug.Enabled) Log.Out(message);
#endif
    }

    public static void Capture(XUiController item)
    {
        if (item == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        var character = item.GetParentByType<XUiC_RebirthSurvivorCharacter>();
        var crafting = item.GetParentByType<XUiC_RebirthPersonalCrafting>();
        if (crafting == null && item is XUiC_ItemStack belt && belt.StackLocation == XUiC_ItemStack.StackLocationTypes.ToolBelt &&
            XUiC_RebirthPersonalCrafting.ActiveInstance?.State.IsOpen == true)
            crafting = XUiC_RebirthPersonalCrafting.ActiveInstance;
        if (character == null && item is XUiC_ItemStack characterBelt && characterBelt.StackLocation == XUiC_ItemStack.StackLocationTypes.ToolBelt &&
            XUiC_RebirthSurvivorCharacter.ActiveInstance?.IsCharacterWindowOpen == true &&
            XUiC_RebirthSurvivorCharacter.ActiveInstance.xui == item.xui)
            character = XUiC_RebirthSurvivorCharacter.ActiveInstance;
        if (character == null && crafting == null) { RebirthItemWindowLifecycle.Trace("[REBIRTH ItemEditor] capture skipped: source=" + item.GetType().Name); return; }
        var ui = item.xui;
        if (ui.playerUI.windowManager.IsWindowOpen("assemble") || ui.playerUI.windowManager.IsWindowOpen("cosmetics")) return;
        Returns[ui] = new ReturnState {
            Owner = (XUiController)character ?? crafting, Item = item,
            Inventory = crafting != null && crafting.IsInventoryOnlyMode,
            Token = ++nextReturnToken,
            Offset = character != null ? character.GetChildByType<XUiC_RebirthCharacterBackpack>().ScrollOffset
                : crafting.GetChildByType<XUiC_RebirthCraftingInventoryScroll>().CaptureOffset()
        };
        if (item is XUiC_ItemStack slot) slot.InfoWindow = ui.GetChildByType<XUiC_ItemInfoWindow>();
        if (item is XUiC_EquipmentStack equipment) equipment.InfoWindow = ui.GetChildByType<XUiC_ItemInfoWindow>();
        RebirthItemWindowLifecycle.Trace("[REBIRTH ItemEditor] captured owner=" + Returns[ui].Owner.GetType().Name + " source=" + item.GetType().Name + " offset=" + Returns[ui].Offset);
    }

    public static bool HasReturn(XUi ui) => Returns.ContainsKey(ui);

    public static IEnumerator ReturnLater(XUi ui)
    {
        if (!Returns.TryGetValue(ui, out var state)) yield break;
        long token = state.Token;
        // Native Escape closes on press; the destination screens also react to release.
        // Do not let that same gesture close the destination we are about to restore.
        while (ui != null && ui.playerUI != null && ui.playerUI.entityPlayer != null &&
            (Input.GetKey(KeyCode.Escape) || ui.playerUI.playerInput.PermanentActions.Cancel.IsPressed ||
             ui.playerUI.playerInput.GUIActions.Cancel.IsPressed)) yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        if (ui == null || ui.playerUI == null || ui.playerUI.entityPlayer == null) yield break;
        ReturnState current;
        if (!Returns.TryGetValue(ui, out current) || current == null || current.Token != token)
        {
            RebirthItemWindowLifecycle.Trace("[REBIRTH ItemEditor] stale return suppressed token=" + token);
            yield break;
        }
        Returns.Remove(ui);
        string destination = state.Owner is XUiC_RebirthSurvivorCharacter ? "rebirthSurvivorCharacter" : "crafting";
        RebirthItemWindowLifecycle.Trace("[REBIRTH ItemEditor] restoring destination=" + destination);
        ui.playerUI.windowManager.Open(destination, true);
        // Child OnOpen callbacks reset selection and scrolling; restore after that lifecycle completes.
        yield return new WaitForEndOfFrame();
        if (state.Owner is XUiC_RebirthSurvivorCharacter character && character.IsCharacterWindowOpen)
            character.GetChildByType<XUiC_RebirthCharacterBackpack>().RestoreItem(state.Item, state.Offset);
        else if (state.Owner is XUiC_RebirthPersonalCrafting crafting && crafting.State.IsOpen)
        {
            if (state.Inventory) crafting.ShowInventorySurface(); else crafting.ShowCraftingSurface();
            crafting.GetChildByType<XUiC_RebirthCraftingItemContext>().SelectSlot(state.Item as XUiC_ItemStack);
            crafting.GetChildByType<XUiC_RebirthCraftingInventoryScroll>().RestoreOffset(state.Offset);
        }
        RebirthItemWindowLifecycle.Trace("[REBIRTH ItemEditor] restored destination=" + destination + " open=" + ui.playerUI.windowManager.IsWindowOpen(destination));
    }
}

[HarmonyPatch(typeof(ItemActionEntryAssemble), nameof(ItemActionEntryAssemble.OnActivated))]
public static class RebirthItemModifyCapturePatch
{
    public static void Prefix(ItemActionEntryAssemble __instance) => RebirthItemWindowLifecycle.Capture(__instance.ItemController);
}

[HarmonyPatch(typeof(XUiC_AssembleWindowGroup), nameof(XUiC_AssembleWindowGroup.OnClose))]
public static class RebirthItemModifyReturnPatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var native = AccessTools.Method(typeof(XUiC_AssembleWindowGroup), "showCraftingLater");
        var route = AccessTools.Method(typeof(RebirthItemModifyReturnPatch), nameof(ReturnAfterModify));
        bool replaced = false;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(native))
            {
                // Intercept the call in OnClose, not the tiny iterator factory that Mono can inline.
                instruction.opcode = OpCodes.Call;
                instruction.operand = route;
                replaced = true;
            }
            yield return instruction;
        }
        if (!replaced)
            Log.Warning("[REBIRTH ItemEditor] Native Modify return hook was not installed.");
        else
            RebirthItemWindowLifecycle.Trace("[REBIRTH ItemEditor] modify return call installed=True");
    }

    public static IEnumerator ReturnAfterModify(XUiC_AssembleWindowGroup group)
    {
        bool captured = RebirthItemWindowLifecycle.HasReturn(group.xui);
        RebirthItemWindowLifecycle.Trace("[REBIRTH ItemEditor] modify close captured=" + captured);
        return captured ? RebirthItemWindowLifecycle.ReturnLater(group.xui) : group.showCraftingLater();
    }
}

// Explicit entries capture before the native action closes its source window. This also covers
// runtimes where the small native OnActivated method was inlined before Harmony installed.
public sealed class RebirthModifyEntry : ItemActionEntryAssemble
{
    public RebirthModifyEntry(XUiController item) : base(item) { }
    public override void OnActivated() { RebirthItemWindowLifecycle.Capture(ItemController); base.OnActivated(); }
}

public sealed class RebirthCosmeticsEntry : ItemActionEntryShowCosmetics
{
    public RebirthCosmeticsEntry(XUiController item) : base(item) { }
    public override void OnActivated()
    {
        ItemValue value = ItemController is XUiC_ItemStack slot ? slot.ItemStack?.itemValue
            : (ItemController as XUiC_EquipmentStack)?.ItemValue;
        if (!(value?.ItemClass is ItemClassArmor armor)) return;
        RebirthItemWindowLifecycle.Capture(ItemController);
        RebirthItemWindowLifecycle.Trace("[REBIRTH ItemEditor] cosmetics open slot=" + armor.EquipSlot);
        XUiC_CharacterCosmeticWindowGroup.Open(ItemController.xui, armor.EquipSlot);
    }
}

// Preserve the native Recipes meaning: recipes which USE this item as an ingredient.
// Copy the source before retiring any UI, then hand it to the visible Rebirth catalogue.
public sealed class RebirthRecipesEntry : ItemActionEntryRecipes
{
    public RebirthRecipesEntry(XUiController item) : base(item) { }
    public override void OnActivated()
    {
        ItemStack selected = (ItemController as XUiC_ItemStack)?.ItemStack;
        if (selected != null && !selected.IsEmpty() &&
            RebirthCraftingNavigationService.OpenRecipesForItem(ItemController, selected.Clone())) return;
        base.OnActivated();
    }
}

public static class RebirthEditorActions
{
    public static void Adapt(XUiC_ItemActionList actions, XUiController item)
    {
        if (item == null) return;
        // Reusable study actions are not always exposed by the native consumable menu.
        // Use the item's authored action and replace native Use entries to avoid duplicates.
        if (RebirthLiteratureUseEntry.FindAction(item) != null)
        {
            for (int i = actions.itemActionEntries.Count - 1; i >= 0; i--)
                if (actions.itemActionEntries[i] is ItemActionEntryUse || actions.itemActionEntries[i] is RebirthLiteratureUseEntry)
                {
                    actions.itemActionEntries[i].DisableEvents();
                    actions.itemActionEntries.RemoveAt(i);
                }
            actions.AddActionListEntry(new RebirthLiteratureUseEntry(item));
        }
        bool cosmetic = false;
        for (int i = 0; i < actions.itemActionEntries.Count; i++)
        {
            var entry = actions.itemActionEntries[i];
            BaseItemActionEntry replacement = null;
            if (entry is ItemActionEntryRecipes && !(entry is RebirthRecipesEntry))
                replacement = new RebirthRecipesEntry(item);
            if (entry is ItemActionEntryAssemble) replacement = new RebirthModifyEntry(item);
            if (entry is ItemActionEntryShowCosmetics) { replacement = new RebirthCosmeticsEntry(item); cosmetic = true; }
            if (replacement == null) continue;
            entry.DisableEvents();
            actions.itemActionEntries[i] = replacement;
            replacement.ParentActionList = actions;
            replacement.RefreshEnabled();
        }
        if (!cosmetic && item is XUiC_ItemStack slot && slot.ItemStack?.itemValue?.ItemClass is ItemClassArmor)
            actions.AddActionListEntry(new RebirthCosmeticsEntry(item));
    }
}

[HarmonyPatch(typeof(XUiC_CharacterCosmeticWindowGroup), nameof(XUiC_CharacterCosmeticWindowGroup.OnClose))]
public static class RebirthCosmeticsReturnPatch
{
    public static void Postfix(XUiC_CharacterCosmeticWindowGroup __instance)
    {
        if (!RebirthItemWindowLifecycle.HasReturn(__instance.xui)) return;
        RebirthItemWindowLifecycle.Trace("[REBIRTH ItemEditor] cosmetics close: returning to source");
        GameManager.Instance.StartCoroutine(RebirthItemWindowLifecycle.ReturnLater(__instance.xui));
    }
}

[HarmonyPatch(typeof(XUiC_DragAndDropWindow), nameof(XUiC_DragAndDropWindow.Update))]
public static class RebirthCharacterDragMenuPatch
{
    public static void Prefix(XUiC_DragAndDropWindow __instance)
    {
        // Acquire ownership before native Update can return a newly picked-up item.
        if(RebirthWindowInventoryScope.OwnsCursor(__instance.xui))__instance.InMenu=true;
    }
}
public sealed class RebirthLiteratureUseEntry : ItemActionEntryUse
{
    public RebirthLiteratureUseEntry(XUiController controller)
        : base(controller, ItemActionEntryUse.ConsumeType.Heal) { }

    public static ItemActionEat FindAction(XUiController controller)
    {
        var slot = controller as XUiC_ItemStack;
        var actions = slot?.ItemStack?.itemValue?.ItemClass?.Actions;
        if (actions != null)
            foreach (var action in actions)
                if (action is ItemActionStudyLiteratureRebirth || action is ItemActionListenAudiobookRebirth)
                    return (ItemActionEat)action;
        return null;
    }

    public override void RefreshEnabled()
    {
        var ui = ItemController.xui;
        Enabled = !RebirthBackpackLibraryReservation.IsHeld(ui.playerUI?.entityPlayer)
            && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput()
            && ui.DragAndDropWindow.IsEmpty() && ui.AssembleItem.CurrentItem == null
            && ItemController is XUiC_ItemStack slot && !slot.ItemStack.IsEmpty()
            && FindAction(ItemController) != null;
    }

    public override void OnActivated()
    {
        RefreshEnabled();
        if (!Enabled) return;
        var slot = (XUiC_ItemStack)ItemController;
        FindAction(slot).ExecuteInstantAction(slot.xui.playerUI.entityPlayer, slot.ItemStack, false, slot);
    }
}
